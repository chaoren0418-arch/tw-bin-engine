using System.Text.RegularExpressions;
namespace P06;

public enum AfflictionTint : byte { Normal, Poison, Gray }

public sealed partial class WorldSession {
    readonly Dictionary<uint,AfflictionTint> afflictionTints=[];
    int afflictionWorldEpoch;
    public ushort BlindMode {get;private set;}
    /// <summary>AM§1(f): the own TW16 byte_1542AA1 is 2 or 4 (推測: elf / dark elf). The packet that writes that byte is
    /// not identified (same gap as AmbienceFrame.NightVision), so the session never sets it; while it is true blind
    /// mode 2 uses level 3 of table mode 2 (unlit 6/31) instead of 4/31.</summary>
    public bool BlindClassBonus {get;internal set;}
    public bool AfflictionInputLocked=>paralysis.Any(mode=>mode!=6);

    bool afflictionObserving;

    // Tints and blindness are only written by S165/S47, so hooking on first use is early enough.
    void ObserveAfflictions(){
        if(afflictionObserving)return;
        afflictionObserving=true;
        // Reset() clears Status before ResetOpacity emits its existing lifecycle event.
        // Map/position changes keep Status and must retain self blindness and poison.
        OpacityObserved+=_=>{if(Status==null){afflictionTints.Clear();BlindMode=0;}};
        CombatSoundStateChanged+=PruneAfflictionTints;
    }

    void PruneAfflictionTints(){
        bool changed=afflictionWorldEpoch!=WorldEpoch;
        foreach(uint id in afflictionTints.Keys.ToArray())
            if(id!=Status?.ObjectId&&(changed||!objects.ContainsKey(id)))afflictionTints.Remove(id);
        afflictionWorldEpoch=WorldEpoch;
    }

    public AfflictionTint TintFor(uint objectId){
        if(afflictionTints.TryGetValue(objectId,out var tint))return tint;
        byte flags=Actor?.ObjectId==objectId?Actor.Flags:objects.TryGetValue(objectId,out var other)?other.Flags:(byte)0;
        return (flags&1)!=0?AfflictionTint.Poison:AfflictionTint.Normal;
    }

    // AM§4(a), TW16 sub_946B00: green >= 8 drops the whole packet; otherwise each nonzero byte sets its
    // flag and zero clears it. Gray outranks green in the draw priority, so both nonzero shows gray.
    void ReceivePoison(LoginReader r){
        uint id=r.D();byte green=r.C(),gray=r.C();r.Finish();
        if(green>=8)return;
        ObserveAfflictions();PruneAfflictionTints();
        if(id==0||id!=Status?.ObjectId&&!objects.ContainsKey(id))return;
        afflictionTints[id]=gray!=0?AfflictionTint.Gray:green!=0?AfflictionTint.Poison:AfflictionTint.Normal;
    }

    void ReceiveParalysis(LoginReader r,byte[] plain){
        // Keep the existing ActionLocked gate and S202/7 recovery-wrapper semantics.
        // All movement/attack sources, including queued sends, already use that gate.
        bool wasLocked=AfflictionInputLocked;
        ReceiveTeleport(202,r);
        if(AfflictionInputLocked&&!wasLocked){
            // Invalidate queued requests even if an unlock arrives before their send gate.
            AppearanceRevision++;
            CancelPickupPresentation("affliction");
        }
        // This case returns before the shared chain; S202/7 is the rejection reply for supplies.
        BasicSupplyReply(202,plain);
    }

    void ReceiveBlind(LoginReader r){
        ushort mode=r.H();r.Finish();
        // AM§4(b), TW16 sub_4FFA00: values above 2 are ignored, not a protocol fault.
        if(mode>2)return;
        ObserveAfflictions();BlindMode=mode;
    }
}

// RGB555 formulas from docs/reference/ambience-and-map-events.md §4 (TW16, verified there).
public static class AfflictionColors {
    // Poison, VA 0x976CA0 with color 256 = (R0,G8,B0): c' = min(31, c + k*C/16), k = 16+|12-(t%24)|,
    // t advancing every 50 ms (VA 0xF06C60). Only green moves, by 8..14, as a 1.2 s triangle wave.
    public static int PoisonPulse(long clockMs){int t=(int)((clockMs/50%24+24)%24);return 16+Math.Abs(12-t);}
    // Paralysis gray, VA 0x977020 with 0x0C63 = (3,3,3): c' = C + c/2.
    public static ushort Tint(ushort color,AfflictionTint tint,long clockMs){
        int red=(color>>10)&31,green=(color>>5)&31,blue=color&31;
        return tint switch {
            AfflictionTint.Poison=>(ushort)((red<<10)|(Math.Min(31,green+PoisonPulse(clockMs)*8/16)<<5)|blue),
            AfflictionTint.Gray=>(ushort)((3+red/2<<10)|(3+green/2<<5)|(3+blue/2)),
            _=>color
        };
    }
    // Lighting table application (VA 0x9786F0): each 5-bit channel c' = c*f/31.
    public static ushort Scale(ushort color,int f)=>f>=31?color:(ushort)((((color>>10)&31)*f/31<<10)|(((color>>5)&31)*f/31<<5)|(color&31)*f/31);
    // Unlit coefficient for blind mode 2: level 1 of table mode 0 is 4/31 (AM§1(h), §4(b)).
    // With the class condition (AM§1(f), byte_1542AA1 ∈ {2,4}) it is level 3 of table mode 2: 6/31.
    public const int BlindTwoUnlit=4;
    /// <summary>AM§3(c), sub_F11FA0: while B == 2, objects whose list.spr field 102 (table 0x13CD838) is one of these
    /// also get light k=5.</summary>
    public static bool BlindTwoGlows(byte spriteType)=>spriteType is 5 or 6 or 10 or 12;
    public static ushort Darken(ushort color)=>Scale(color,BlindTwoUnlit);
    // Own light while blind uses light index k=5 (AM§3(c), §4(b)). The product stamps Source.lmp slot 5
    // (72×36 cells, sub_97D2F0) once the renderer has read it (R2-A, T251). Only when Source.lmp is not
    // available (CI, missing client) the former stand-in disc below is used: 5*24 px with a smooth edge.
    public const int BlindLightSource=5;
    public const float BlindLightRadius=5*24f;
    public static int BlindLight(double distance){
        double edge=Math.Clamp((distance/BlindLightRadius-.15)/.85,0,1);
        return (int)Math.Round((1-edge*edge*(3-2*edge))*255)>>3;
    }
    // Mode 1 is table level 0 (coefficient = light index). Mode 2 is level 1 of table mode 0 (AM§4(b));
    // the whole row is in AmbienceCurve's table (unlit entry 4, lit entry 31). With the class condition
    // (AM§1(f)/(h): byte_1542AA1 ∈ {2,4}) sub_F11FA0 uses level 3 of table mode 2 instead (unlit 6/31).
    public static int BlindCoefficient(ushort mode,int light,bool classBonus=false)=>
        mode==1?light:mode==2?AmbienceCurve.Coefficient(classBonus?2:0,classBonus?3:1,(byte)(Math.Clamp(light,0,31)<<3)):31;
    /// <summary>Light index (0–31, light &gt;&gt; 3) of a Source.lmp k=5 stamp centred at the own light centre, for a scene
    /// pixel offset (dx, dy) from that centre. Same cell mapping as AmbienceLightMap: the centre sits at (400, 240) of
    /// an 800×480 window, left ((x−w)&gt;&gt;1)&amp;~3, top (y−h)&gt;&gt;1, one cell = 2×2 pixels.</summary>
    public static int BlindLightStamp(AmbienceLightImages images,int dx,int dy){
        int w=images.Width(BlindLightSource),h=images.Height(BlindLightSource);if(w<=0)return 0;
        // Centre (400, 240) in full-resolution window pixels == (AmbienceLightMap.Width, AmbienceLightMap.Height).
        int left=((AmbienceLightMap.Width-w)>>1)&~3,top=(AmbienceLightMap.Height-h)>>1;
        int cx=((AmbienceLightMap.Width+dx)>>1)-left,cy=((AmbienceLightMap.Height+dy)>>1)-top;
        if((uint)cx>=(uint)w||(uint)cy>=(uint)h)return 0;
        return images.Image(BlindLightSource)[cy*w+cx]>>3;
    }
}

public sealed partial class Renderer {
    Patch ComposeAfflictionPatch(WorldSession session,Rectangle area,List<Draw> commands,SceneOpacity? opacity){
        ushort blind=session.BlindMode;
        if(blind!=0){
            // Cover the complete map background too, including pixels outside actor bounds.
            area=Bounds;
            var mapCommands=ActiveScene.ToHashSet();
            commands=ActiveScene.Concat(commands.Where(c=>!mapCommands.Contains(c)))
                .OrderBy(c=>c,DrawComparer.Instance).ThenBy(c=>c.ObjectId).ToList();
        }
        var tints=commands.Where(c=>c.Actor).Select(c=>c.ObjectId).Distinct()
            .ToDictionary(id=>id,id=>session.TintFor(id==0?session.Actor?.ObjectId??0:id));
        long clock=session.MagicStatusClock();
        var colors=new ushort[area.Width*area.Height];var mask=new bool[colors.Length];
        var owners=Enumerable.Repeat(-1,colors.Length).ToArray();var entityOwners=new uint?[colors.Length];
        FillShipBackdrop(area,colors,mask);
        foreach(var command in commands){
            var draw=command;
            if(draw.Actor&&tints[draw.ObjectId] is var tint&&tint!=AfflictionTint.Normal)
                draw=draw with{Tile=draw.Tile with{Pixels=draw.Tile.Pixels.Select(p=>p with{Color=AfflictionColors.Tint(p.Color,tint,clock)}).ToArray()}};
            Blit(draw,area,colors,mask,owners,entityOwners,session.InvisibilityOpacity(command,opacity?.Get(command)));
        }
        if(blind!=0)ApplyBlindLight(session,blind,area,colors,commands,LoadedAmbienceImages,OwnCombatVisual?.Foot,blind==2?BlindSpriteTypes():null,blindLightMap);
        return new(area,colors,mask,owners,commands,entityOwners);
    }

    // ---- R3-H (T283/#676): blind mode 2 object lights (AM§3(c), §4(b)). ----
    readonly AmbienceLightMap blindLightMap=new();
    SpriteTypeTable? blindSpriteTypes;bool blindSpriteTypesTried;
    /// <summary>Replaces the client list.spr field 102 table for the blind mode 2 object lights (tests).</summary>
    public SpriteTypeTable? SpriteTypesOverride {get;set;}
    /// <summary>Why the blind mode 2 object lights are off (list.spr unreadable); null when loaded or not yet needed.</summary>
    public string? BlindSpriteTypeNotice {get;private set;}
    // Read once, the first time blind mode 2 is composed; a failure is remembered and only the own light remains.
    SpriteTypeTable? BlindSpriteTypes(){
        if(SpriteTypesOverride is {} fixedTable)return fixedTable;
        if(blindSpriteTypesTried)return blindSpriteTypes;
        blindSpriteTypesTried=true;
        try{blindSpriteTypes=SpriteTypeTable.Load(GroundSpriteReader.ClientRoot);BlindSpriteTypeNotice=null;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or OverflowException or System.Security.Cryptography.CryptographicException){
            BlindSpriteTypeNotice="list.spr 欄位 102 無法讀取，盲目 2 只留自己的光："+ex.Message;
            System.Diagnostics.Trace.WriteLine(BlindSpriteTypeNotice);
        }
        return blindSpriteTypes;
    }

    /// <summary>Scene-pixel centres of the blind lights: the own light first, then (mode 2 with a type table) every other
    /// object drawn in this composition whose list.spr field 102 glows (AM§3(c)), each k=5, until the TW16 budget
    /// (sum of k ≥ 700) is reached. Other objects are centred on the foot of their current tile (推測: while walking
    /// the native centre also follows the step offset); the native ±35/±15 field filter is not applied, the stamp
    /// simply clips at the 800×480 window, as in WorldSession.AmbienceLights.</summary>
    internal static List<PointF> BlindLightCenters(WorldSession session,ushort blind,List<Draw> commands,PointF? own,SpriteTypeTable? types){
        var result=new List<PointF>();
        if(own is PointF self)result.Add(self);
        if(blind!=2||types==null)return result;
        uint ownId=session.Actor?.ObjectId??0;
        var drawn=commands.Where(d=>d.Actor&&d.ObjectId!=0&&d.ObjectId!=ownId).Select(d=>d.ObjectId).ToHashSet();
        int budget=own!=null?AfflictionColors.BlindLightSource:0;
        foreach(var o in session.Objects){
            if(budget>=AmbienceCurve.SourceBudget)break;
            if(!drawn.Contains(o.ObjectId)||o.AppearancePending||!AfflictionColors.BlindTwoGlows(types[o.Gfx]))continue;
            result.Add(Coordinates.Screen(o.X,o.Y));budget+=AfflictionColors.BlindLightSource;
        }
        return result;
    }

    // AM§4(b): while blind every pixel goes through the lighting table. The lights left are the own k=5 light and,
    // in mode 2, k=5 on the objects of list.spr type {5,6,10,12} (AM§3(c)). With Source.lmp they are stamped into a
    // 400×240 light map centred on the own displayed foot (overlaps keep the maximum, sub_97D2F0), like every other
    // light-map source; without it the stand-in disc is used around each centre (maximum of the discs).
    internal static void ApplyBlindLight(WorldSession session,ushort blind,Rectangle area,ushort[] colors,List<Draw> commands,AmbienceLightImages? images,PointF? foot=null,SpriteTypeTable? types=null,AmbienceLightMap? map=null){
        bool stamp=images!=null&&images.Width(AfflictionColors.BlindLightSource)>0;
        var ownCenter=stamp&&foot!=null?foot:BlindLightCenter(session,commands);
        var centers=BlindLightCenters(session,blind,commands,ownCenter,types);
        bool bonus=blind==2&&session.BlindClassBonus;
        int unlit=AfflictionColors.BlindCoefficient(blind,0,bonus);
        if(centers.Count==0){for(int i=0;i<colors.Length;i++)colors[i]=AfflictionColors.Scale(colors[i],unlit);return;}
        if(stamp){
            map??=new();map.Clear();
            // Window origin: the first centre (own) sits at (400, 240) of the 800×480 window.
            int wx=(int)MathF.Floor(centers[0].X)-AmbienceLightMap.Width,wy=(int)MathF.Floor(centers[0].Y)-AmbienceLightMap.Height;
            var image=images!.Image(AfflictionColors.BlindLightSource);int w=images.Width(AfflictionColors.BlindLightSource),h=images.Height(AfflictionColors.BlindLightSource);
            foreach(var c in centers)map.Stamp((int)MathF.Floor(c.X)-wx,(int)MathF.Floor(c.Y)-wy,image,w,h);
            for(int i=0;i<colors.Length;i++){
                int vx=area.X+i%area.Width-wx,vy=area.Y+i/area.Width-wy;
                int light=vx<0||vy<0?0:map[vx>>1,vy>>1]>>3;
                colors[i]=AfflictionColors.Scale(colors[i],AfflictionColors.BlindCoefficient(blind,light,bonus));
            }
            return;
        }
        for(int i=0;i<colors.Length;i++){
            int light=0;
            foreach(var c in centers){
                double dx=area.X+i%area.Width+.5-c.X,dy=area.Y+i/area.Width+.5-c.Y;
                if(Math.Abs(dx)<AfflictionColors.BlindLightRadius&&Math.Abs(dy)<AfflictionColors.BlindLightRadius)
                    light=Math.Max(light,AfflictionColors.BlindLight(Math.Sqrt(dx*dx+dy*dy)));
            }
            colors[i]=AfflictionColors.Scale(colors[i],light==0?unlit:AfflictionColors.BlindCoefficient(blind,light,bonus));
        }
    }
    static PointF? BlindLightCenter(WorldSession session,List<Draw> commands){
        uint ownId=session.Actor?.ObjectId??0;
        var own=commands.Where(d=>d.Layer=="actor"&&(d.ObjectId==0||d.ObjectId==ownId)).ToList();
        if(own.Count>0){int left=own.Min(d=>d.Px),top=own.Min(d=>d.Py),right=own.Max(d=>d.Px+24),bottom=own.Max(d=>d.Py+24);return new((left+right)/2f,(top+bottom)/2f);}
        return session.Actor is {} actor?Coordinates.Screen(actor.X,actor.Y):null;
    }
}

/// <summary>list.spr field 102 ("type") per gfx: TW16 byte table 0x13CD838, filled by the list.spr parser sub_87C890
/// (VA 0x87C890 / FO 0x47BC90), default 1 (docs/reference/rea-inventory.md B07). Read at run time from the original
/// client's Text archive (list.spz decrypted the same way as EffectSpriteCatalog.LoadMetadata, or plain list.spr);
/// nothing is copied. Only an entry's own `102.` field is used: whether an alias (`#N M=K`) inherits it is not
/// verified, so aliases keep the default unless they carry their own field.</summary>
public sealed class SpriteTypeTable {
    public const byte Default=1;
    readonly Dictionary<int,byte> types;
    SpriteTypeTable(Dictionary<int,byte> types){this.types=types;}
    public int Count=>types.Count;
    public byte this[int gfx]=>types.TryGetValue(gfx,out var value)?value:Default;
    /// <summary>Parse decoded list.spr text: entries start at a line `#gfx`, the field is `102.name(value)`.</summary>
    public static SpriteTypeTable Parse(string source){
        source=source.Replace("\r","");var result=new Dictionary<int,byte>();
        var entries=Regex.Matches(source,@"(?m)^#(\d+)\s");
        for(int i=0;i<entries.Count;i++){
            var m=entries[i];int end=i+1<entries.Count?entries[i+1].Index:source.Length;
            var field=Regex.Match(source[m.Index..end],@"(?<![\d.])102\.[^(\s]*\((\d+)\)");
            if(field.Success&&int.TryParse(m.Groups[1].Value,out int gfx)&&byte.TryParse(field.Groups[1].Value,out byte type))result[gfx]=type;
        }
        return new(result);
    }
    public static SpriteTypeTable FromTypes(IReadOnlyDictionary<int,byte> types)=>new(new(types));
    public static SpriteTypeTable Load(string clientRoot){
        var text=TextArchive.Open(clientRoot);byte[] raw;
        if(text.Single("list.spz") is {} packed){
            raw=text.Read(packed);
            if(!raw.AsSpan().StartsWith("S1223"u8))throw new InvalidDataException("未知 list.spz 格式");
            using var aes=System.Security.Cryptography.Aes.Create();aes.Key=Convert.FromHexString("DC8401212A40200ADD25B9A70DB9C94E");
            byte[] iv=Convert.FromHexString("3E0978AAC4D53063300C5F9A807F2246");int length=(raw.Length-5)/16*16;
            var decoded=(byte[])raw.Clone();aes.DecryptCbc(raw.AsSpan(5,length),iv,System.Security.Cryptography.PaddingMode.None).CopyTo(decoded,5);
            for(int i=5+length;i<raw.Length;i++)decoded[i]=(byte)(raw[i]^(length>0?raw[i-16]:iv[i-5]));raw=decoded;
        }else raw=text.Read(text.Single("list.spr")??throw new InvalidDataException("缺少動畫表 list.spr"));
        var table=Parse(System.Text.Encoding.ASCII.GetString(raw));
        if(table.Count==0)throw new InvalidDataException("list.spr 沒有欄位 102");
        return table;
    }
}
