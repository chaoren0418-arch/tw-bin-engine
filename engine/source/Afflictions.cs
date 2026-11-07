namespace P06;

public enum AfflictionTint : byte { Normal, Poison, Gray }

public sealed partial class WorldSession {
    readonly Dictionary<uint,AfflictionTint> afflictionTints=[];
    int afflictionWorldEpoch;
    public ushort BlindMode {get;private set;}
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
    // The 6/31 class bonus depends on an inferred class meaning and is not applied.
    public const int BlindTwoUnlit=4;
    public static ushort Darken(ushort color)=>Scale(color,BlindTwoUnlit);
    // Own light while blind uses light index k=5. source.lmp is unread, so the disc reuses the engine's
    // existing light radius for value 5 and its smooth edge; the shape is a presentation stand-in.
    public static readonly float BlindLightRadius=AmbiencePainter.Radius(5,1f);
    public static int BlindLight(double distance){
        double edge=Math.Clamp((distance/BlindLightRadius-.15)/.85,0,1);
        return (int)Math.Round((1-edge*edge*(3-2*edge))*255)>>3;
    }
    // Mode 1 is table level 0 (coefficient = light index, verified). Mode 2 is level 1: only its unlit
    // entry 4 is documented, so the rise to 31 is interpolated linearly (inferred).
    public static int BlindCoefficient(ushort mode,int light)=>mode==1?light:mode==2?BlindTwoUnlit+(31-BlindTwoUnlit)*light/31:31;
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
        if(blind!=0)ApplyBlindLight(session,blind,area,colors,commands);
        return new(area,colors,mask,owners,commands,entityOwners);
    }

    // AM§4(b): while blind every pixel goes through the lighting table, and the only light left is the
    // own k=5 disc. Mode 2's extra light on some object kinds rests on an inferred kind meaning: not done.
    static void ApplyBlindLight(WorldSession session,ushort blind,Rectangle area,ushort[] colors,List<Draw> commands){
        var center=BlindLightCenter(session,commands);
        int unlit=AfflictionColors.BlindCoefficient(blind,0);
        for(int i=0;i<colors.Length;i++){
            int f=unlit;
            if(center is PointF c){double dx=area.X+i%area.Width+.5-c.X,dy=area.Y+i/area.Width+.5-c.Y;
                if(Math.Abs(dx)<AfflictionColors.BlindLightRadius&&Math.Abs(dy)<AfflictionColors.BlindLightRadius)
                    f=AfflictionColors.BlindCoefficient(blind,AfflictionColors.BlindLight(Math.Sqrt(dx*dx+dy*dy)));}
            colors[i]=AfflictionColors.Scale(colors[i],f);
        }
    }
    static PointF? BlindLightCenter(WorldSession session,List<Draw> commands){
        uint ownId=session.Actor?.ObjectId??0;
        var own=commands.Where(d=>d.Layer=="actor"&&(d.ObjectId==0||d.ObjectId==ownId)).ToList();
        if(own.Count>0){int left=own.Min(d=>d.Px),top=own.Min(d=>d.Py),right=own.Max(d=>d.Px+24),bottom=own.Max(d=>d.Py+24);return new((left+right)/2f,(top+bottom)/2f);}
        return session.Actor is {} actor?Coordinates.Screen(actor.X,actor.Y):null;
    }
}
