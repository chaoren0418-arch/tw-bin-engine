using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace P06;

// Read-only runtime resources. No extracted sprites, text tables or BIN are shipped.
// Format evidence and bounds: docs/protocol/attack-skill.md.
public sealed class EffectSpriteCatalog(string clientRoot) {
    sealed record Entry(string Pak,uint Offset,uint Size);
    readonly Dictionary<string,Dictionary<string,Entry>> archives=[];
    readonly Dictionary<int,string> metadata=[];
    readonly Dictionary<int,EffectSprite> effects=[];
    readonly Dictionary<(int,int,int),Frame[]> sprites=[];
    bool metadataLoaded;
    static void Require(bool ok,string message){if(!ok)throw new InvalidDataException("特效素材："+message);}
    static byte[] ReadFile(string path,int limit){var file=new FileInfo(path);Require(file.Length<=limit,"檔案超出限制");return File.ReadAllBytes(path);}
    Dictionary<string,Entry> Index(string stem){
        if(archives.TryGetValue(stem,out var cached))return cached;
        string path=Path.Combine(clientRoot,stem+".idx");var data=ReadFile(path,32*1024*1024);
        Require(data.Length>=4,"索引截斷");uint count=BinaryPrimitives.ReadUInt32LittleEndian(data);
        Require(count<=1000000&&data.Length==4L+28L*count,"僅支援 plain28 索引");
        byte[] rows=data[4..];var result=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
        string pak=Path.Combine(clientRoot,stem+".pak");long size=new FileInfo(pak).Length;
        for(int i=0;i<count;i++){
            var row=rows.AsSpan(i*28,28);uint offset=BinaryPrimitives.ReadUInt32LittleEndian(row),length=BinaryPrimitives.ReadUInt32LittleEndian(row[24..]);
            string name=Encoding.ASCII.GetString(row.Slice(4,20)).TrimEnd('\0');
            Require(name.Length>0&&name.All(c=>c is >= ' ' and <= '~')&&(long)offset+length<=size,"索引範圍或名稱錯誤");
            Require(result.TryAdd(name,new(pak,offset,length)),"索引名稱重複");
        }
        archives[stem]=result;return result;
    }
    static byte[] Payload(Entry entry){
        Require(entry.Size<=16*1024*1024,"單筆資源超出限制");var data=new byte[entry.Size];
        using var stream=File.OpenRead(entry.Pak);Require((long)entry.Offset+entry.Size<=stream.Length,"資源截斷");
        stream.Position=entry.Offset;stream.ReadExactly(data);return data;
    }
    void LoadMetadata(){
        if(metadataLoaded)return;
        // Text contains repeated unrelated dialog names; only the animation tables are read (TextArchive.Single).
        var text=TextArchive.Open(clientRoot);byte[] raw;
        if(text.Single("list.spz") is {} packed){
            raw=text.Read(packed);Require(raw.AsSpan().StartsWith("S1223"u8),"未知 list.spz 格式");
            using var aes=Aes.Create();aes.Key=Convert.FromHexString("DC8401212A40200ADD25B9A70DB9C94E");
            byte[] iv=Convert.FromHexString("3E0978AAC4D53063300C5F9A807F2246");int length=(raw.Length-5)/16*16;
            var decoded=(byte[])raw.Clone();aes.DecryptCbc(raw.AsSpan(5,length),iv,PaddingMode.None).CopyTo(decoded,5);
            for(int i=5+length;i<raw.Length;i++)decoded[i]=(byte)(raw[i]^(length>0?raw[i-16]:iv[i-5]));raw=decoded;
        }else {var plain=text.Single("list.spr");Require(plain!=null,"缺少動畫表");raw=text.Read(plain!);}
        string source=Encoding.ASCII.GetString(raw).Replace("\r","");
        var entries=Regex.Matches(source,@"(?m)^#(\d+)\s");Require(entries.Count>0,"動畫表無 gfx");
        for(int i=0;i<entries.Count;i++){
            var m=entries[i];metadata.Add(int.Parse(m.Groups[1].Value),source[m.Index..(i+1<entries.Count?entries[i+1].Index:source.Length)]);
        }
        metadataLoaded=true;
    }
    public EffectSprite Get(int gfx){
        if(effects.TryGetValue(gfx,out var cached))return cached;LoadMetadata();
        int resolved=gfx;var seen=new HashSet<int>();string? entry;
        while(true){
            Require(seen.Add(resolved)&&seen.Count<=16,"gfx alias 循環");Require(metadata.TryGetValue(resolved,out entry),$"缺少 gfx {resolved}");
            var alias=Regex.Match(entry!,@"^#\d+\s+\d+=(\d+)");
            if(!alias.Success||Regex.IsMatch(entry!,@"(?<![\d.])0\.[^(]*\("))break;
            resolved=int.Parse(alias.Groups[1].Value);
        }
        var effect=EffectSprite.Parse(resolved,entry!) with{Gfx=gfx};if(effects.Count>=128)effects.Clear();effects[gfx]=effect;return effect;
    }
    public Frame Frame(EffectSprite effect,int direction,long age,bool loop=false,int? step=null){
        var token=effect.At(age,loop,step);int sid=token.SubId+(effect.Directional?direction%effect.Directions:0);
        var key=(effect.Sprite,sid,effect.TileType);
        if(!sprites.TryGetValue(key,out var frames)){
            string name=$"{effect.Sprite}-{sid}.spr";Entry? found=null;
            // Match existing asset preparation: base archive first, then numbered archives.
            foreach(string stem in new[]{"Sprite"}.Concat(Enumerable.Range(0,16).Select(i=>$"Sprite{i:00}"))){
                if(!File.Exists(Path.Combine(clientRoot,stem+".idx")))continue;
                if(Index(stem).TryGetValue(name,out found))break;
            }
            Require(found!=null,$"缺少 {name}");frames=Decode(Payload(found!),sid,effect.TileType);
            if(sprites.Count>=128)sprites.Clear();sprites[key]=frames;
        }
        Require(token.Frame<frames.Length,"動畫影格超出 SPR");return frames[token.Frame];
    }
    public static Frame[] Decode(byte[] payload,int subId,int tileType){
        Require(payload.Length is >0 and <=8*1024*1024,"SPR 大小錯誤");
        using var r=new BinaryReader(new MemoryStream(payload));int first=r.ReadByte();bool indexed=first==255;ushort[] palette=[];
        if(indexed){int count=r.ReadByte();if(count==0)count=256;palette=Enumerable.Range(0,count).Select(_=>r.ReadUInt16()).ToArray();first=r.ReadByte();}
        Require(first is >0 and <=127&&palette.All(c=>(c&0x8000)==0),"SPR palette/frame count");
        var layouts=new List<(int[] Bounds,List<(int A,int B,int Z,int Index)> Tiles)>();long pixels=0;
        for(int i=0;i<first;i++){
            int[] bounds=[r.ReadInt16(),r.ReadInt16(),r.ReadInt16(),r.ReadInt16()];ushort u=r.ReadUInt16(),v=r.ReadUInt16();int count=r.ReadInt16();
            bool empty=count==0&&bounds.SequenceEqual(new[]{192,120,-128,-120})&&u==65535&&v==0;
            Require(empty||count is >0 and <=4096&&bounds[2]>=bounds[0]&&bounds[3]>=bounds[1]&&bounds[2]-bounds[0]<2048&&bounds[3]-bounds[1]<2048,"SPR bounds");
            pixels+=empty?0:(bounds[2]-bounds[0]+1)*(bounds[3]-bounds[1]+1);Require(pixels<=16*1024*1024,"SPR decoded size");
            var tiles=new List<(int,int,int,int)>();for(int t=0;t<count;t++)tiles.Add((r.ReadSByte(),r.ReadSByte(),r.ReadByte(),r.ReadUInt16()));layouts.Add((bounds,tiles));
        }
        uint blocks=r.ReadUInt32();Require(blocks is >0 and <=10000,"SPR blocks");var offsets=new uint[blocks];for(int i=0;i<blocks;i++)offsets[i]=r.ReadUInt32();
        uint expanded=r.ReadUInt32();long start=r.BaseStream.Position;int size=0;var decoded=new List<Pixel[]>();
        for(int i=0;i<blocks;i++){
            Require(offsets[i]==r.BaseStream.Position-start,"SPR block offset");int sx=r.ReadByte(),sy=r.ReadByte(),width=r.ReadByte(),lines=r.ReadByte();
            Require(width>0&&lines>0&&sx+width<=24&&sy+lines<=24,"SPR block bounds");size+=4;var points=new List<Pixel>();
            for(int y=sy;y<sy+lines;y++){
                int x=sx,segments=r.ReadByte();size++;
                for(int j=0;j<segments;j++){
                    int skip=r.ReadByte(),run=r.ReadByte();size+=2;Require(skip%2==0,"SPR odd skip");x+=skip/2;Require(run>0&&x+run<=sx+width,"SPR run");
                    for(int k=0;k<run;k++){
                        ushort color;if(indexed){int p=r.ReadByte();Require(p<palette.Length,"SPR palette index");color=palette[p];}else color=r.ReadUInt16();
                        Require((color&0x8000)==0,"SPR RGB555");points.Add(new(x++,y,color));size+=2;
                    }
                }
            }
            decoded.Add(points.ToArray());
        }
        Require(r.BaseStream.Position==payload.Length&&size==expanded,"SPR trailing bytes/expanded size");
        return layouts.Select((f,i)=>new Frame($"{subId}:{i}",f.Bounds,f.Tiles.Select((t,order)=>{
            Require(t.Index<decoded.Count,"SPR block index");int half=(int)Math.Floor(t.A/2d),x=24*(t.B+t.A-half),y=12*(t.B-half);
            Require(decoded[t.Index].All(p=>x+p.X>=f.Bounds[0]&&x+p.X<=f.Bounds[2]&&y+p.Y>=f.Bounds[1]&&y+p.Y<=f.Bounds[3]),"SPR opaque bounds");
            return new Block(t.A,t.B,t.Z,order,new(tileType,decoded[t.Index]));
        }).ToArray())).ToArray();
    }
}
// Hit: list.spr frame flag `!` (36-byte frame +22 bit1, REA B07).
// Events: the frame's raw token text (`!`, `]N`, `{N`, `}N`, sounds), consumed by SubEffects.cs.
public sealed record EffectStep(int SubId,int Frame,int Milliseconds,bool Hit=false,string Events="");
public sealed record EffectSprite(int Gfx,int Sprite,bool Directional,int Directions,int TileType,int Kind,int Impact,EffectStep[] Steps){
    public int Duration=>Steps.Sum(s=>s.Milliseconds);
    // Offsets from effect start of every displayed `!` frame, in the same display order as At():
    // 8ABC10 advances the cursor before the first display, the terminal wrap shows index0 last.
    public long[] HitOffsets(){
        var result=new List<long>();long t=0;
        for(int i=1;i<=Steps.Length;i++){var s=Steps[i%Steps.Length];if(s.Hit)result.Add(t);t+=s.Milliseconds;}
        return result.ToArray();
    }
    // Latest `!` time not after now (null before the first one, or when the sprite has none).
    public long? LatestHit(long start,long now){long? hit=null;foreach(long o in HitOffsets())if(start+o<=now)hit=start+o;return hit;}
    public EffectStep At(long age,bool loop=false,int? step=null){
        // Native pure effects advance the metadata cursor before the first display.
        if(step is int n)return Steps[((n%Steps.Length)+Steps.Length)%Steps.Length];
        long left=loop?Math.Max(0,age)%Duration:Math.Clamp(age,0,Duration-1);
        for(int i=1;i<=Steps.Length;i++){var s=Steps[i%Steps.Length];if(left<s.Milliseconds)return s;left-=s.Milliseconds;}return Steps[0];
    }
    public static EffectSprite Parse(int gfx,string entry){
        var header=Regex.Match(entry,@"^#\d+\s+(\d+)(?:=(\d+))?");
        var action=Regex.Match(entry,@"(?<![\d.])0\.[^(]*\((\d+)\s+(\d+),([^)]*)\)");
        if(!header.Success||!action.Success)throw new InvalidDataException($"特效 gfx {gfx} 缺少 action0");
        int sprite=header.Groups[2].Success?int.Parse(header.Groups[2].Value):gfx;
        var tokens=Regex.Matches(action.Groups[3].Value,@"(\d+)\.(\d+):(\d+)([^\s]*)");
        if(tokens.Count!=int.Parse(action.Groups[2].Value)||tokens.Count is <1 or >4096)throw new InvalidDataException("特效動畫長度");
        var fpsMatch=Regex.Match(entry,@"(?<![\d.])110\.[^(]*\((\d+)\)");int fps=fpsMatch.Success?int.Parse(fpsMatch.Groups[1].Value):24;
        if(fps is <1 or >1000)throw new InvalidDataException("特效 fps");
        var steps=tokens.Select(t=>new EffectStep(int.Parse(t.Groups[1].Value),int.Parse(t.Groups[2].Value),checked(1000*int.Parse(t.Groups[3].Value)/fps),t.Groups[4].Value.Contains('!'),t.Groups[4].Value)).ToArray();
        if(steps.Sum(s=>s.Milliseconds)<=0)throw new InvalidDataException("特效空動畫");
        var effect=Regex.Match(entry,@"(?<![\d.])109\.[^(]*\((\d+)\s+(-?\d+)\)");
        var attr=Regex.Match(entry,@"(?<![\d.])104\.[^(]*\((\d+)\)");int flags=attr.Success?int.Parse(attr.Groups[1].Value):0;
        // Same native blend mapping as MapEffectAppearance: screen / multiply / half.
        int tileType=(flags&8)!=0?18:(flags&128)!=0?34:(flags&256)!=0?6:2;
        bool directional=action.Groups[1].Value!="0";int count=int.Parse(header.Groups[1].Value);
        return new(gfx,sprite,directional,directional&&count==4?4:8,tileType,effect.Success?int.Parse(effect.Groups[1].Value):0,effect.Success?int.Parse(effect.Groups[2].Value):-1,steps);
    }
}
public sealed record MagicVisual(long Number,int Gfx,int X,int Y,int Direction,string Frame,string Phase,long At);
public static class MagicMotion {
    // 8AB390 initializes frame index0; 8ABC10 immediately calls A43560 (++index),
    // then schedules the selected frame duration. The terminal wrap displays index0 once.
    public static Frame PureFrame(ObjectAppearance a,string action,long elapsed){var seq=a.Sequences[(action,0)];double duration=a.Duration(action);return a.At(action,0,(elapsed+seq[0].Milliseconds!.Value)%duration);}
    // Fixed BIN effect4:8211E0 resolves direction &3;821360 steps every60ms, not bow30ms.
    public static (int X,int Y,int Direction,int Step,bool Arrived) At(MagicEffect p,int tx,int ty,long release,long now){
        int ax=Math.Abs(tx-p.SourceX),ay=Math.Abs(ty-p.SourceY),major=Math.Max(ax,ay),direction=ProjectileMotion.Direction(p.SourceX,p.SourceY,tx,ty,p.Heading)%4;
        if(major==0)return(tx,ty,direction,0,true);
        int vx=(ax<<16)/major*Math.Sign(tx-p.SourceX)*2,vy=(ay<<16)/major*Math.Sign(ty-p.SourceY);
        uint fx=unchecked((uint)(2*p.SourceX-32768)<<16),fy=(uint)p.SourceY<<16;int remaining=Math.Max(2*ax,ay),steps=(int)Math.Min(128,(now-release)/60+1),ix=2*p.SourceX-32768,iy=p.SourceY;
        for(int n=1;n<=steps;n++){fx=unchecked(fx+(uint)vx);fy=unchecked(fy+(uint)vy);ix=(int)(unchecked(fx+0x8000u)>>16)&~1;iy=(int)(unchecked(fy+0x8000u)>>16);remaining--;if(ix==2*tx-32768&&iy==ty||remaining<=0)return((ix+32768)/2,iy,direction,n,true);}
        return((ix+32768)/2,iy,direction,steps,false);
    }
    // Kind 7 (sub_81C490, recursive sub_81C9D0): tiles advanced from the origin toward the target,
    // using the same one-tile major-axis stepping. Data helper only; the renderer draws kind 7 with
    // EruptionMotion.Plan (T269, segment timing and two-step gfx, ProjectileRendering.cs).
    public static (int X,int Y)[] EruptionPath(int sx,int sy,int tx,int ty){
        var e=new MagicEffect(0,0,0,0,0,0,0,(ushort)sx,(ushort)sy,(ushort)tx,(ushort)ty,0);var path=new List<(int,int)>();
        for(long t=0;path.Count<128;t+=60){var m=At(e,tx,ty,0,t);path.Add((m.X,m.Y));if(m.Arrived)break;}
        return path.ToArray();
    }
}
// R2-H / T256 (#617): the hardcoded legacy-asset gfx (ComposeMagic) follow the same caster `!` timing as
// the native classes (SV#5). S55 effects and casters without a captured action start at once (§2.1, §2.5).
public static class LegacyMagic {
    public static bool Handles(MagicEffect e)=>e.RangeType==null&&e.Gfx is 11722 or 221 or 2165 or 189 or 191 or 197 or 167;
    // R4-G (gap-matrix 5, 109 kind): S30 builds its object from the gfx's 109 kind (sub_930A20 switch), not from the
    // legacy asset. When the runtime list.spr gives a non-default kind (2 Lightning, 3/4/5 flying, 6, 7, 8 = no object),
    // the legacy gfx goes through the native class path; kind 5 then flies and kind 8 draws nothing (caster `!` hits).
    // 167 keeps its own flying + 219 landing path. Without readable metadata the legacy asset path stays.
    public static bool UsesNativeClass(MagicEffect e,EffectSprite? native)=>e.Gfx!=167&&native!=null&&native.Kind is >=2 and <=8;
    public static long Start(MagicEffect e)=>(e.ActionAt??e.At)+(e.Timing?.ReleaseMs??0);
    // Hit time: the effect's own `!` frames when the runtime list.spr is readable (no `!` = no hit, §3);
    // without native metadata there is no effect object to consult, so the caster `!` hits (sub_A4B060).
    public static long? Hit(EffectSprite? native,long start,long now)=>now<start?null:native!=null?native.LatestHit(start,now):start;
}
// SV#12 (§2.5): while any effect with S42 type (+9) != 0 is alive, the global light L becomes min(15, L+5).
// S106 gfx 2028–2056 carry the same flag. R4-G: the renderer feeds the live count into Ambience.Begin/EndLightEffect.
public static class MagicBrighten {
    public const int Boost=5,Max=15;
    public static int Apply(int level,int active)=>active>0?Math.Min(Max,level+Boost):level;
    public static bool LocationEffect(int gfx)=>gfx is >=2028 and <=2056;
}
/// <summary>Which native class increments the light counter `0x13CE258` (FU5§3).</summary>
public enum LightClass {None,Effect,Lightning}
// R4-G, docs/reference/followups-5.md §3 (TW16 static analysis):
// - default `Effect` (vtable +144 = sub_8ABC10, also ManyEffect): only when `+9` (LightFlag) != 0; +1 when the frame
//   index +12 becomes 1, -1 when it finishes. sub_8ABC10 advances the cursor before the first display, so index 1
//   is reached at the start (offset 0). With fewer than 2 frames index 1 is never reached: no light.
// - kind 2 `Lightning` (sub_8ACB20): ignores `+9`; +1 when the index becomes 2 (Steps[1] ms after the start), -1 when
//   it finishes; fewer than 3 frames = no light.
// - The native defect (finish without a prior +1 drives the counter negative) is not copied: End only after Begin.
// - Flying classes (3/4/5), kind 6 EarthRising and kind 7 Eruption have their own step functions: no light (FU5 lists
//   only sub_8ABC10 and sub_8ACB20 as callers of the counter).
public static class SkillLight {
    public static LightClass Class(int kind,bool range)=>kind==2?LightClass.Lightning
        :MagicClass.Flies(kind,range)||!range&&kind is 6 or 7 or 8?LightClass.None:LightClass.Effect;
    /// <summary>`+9` of a skill effect: S42 = packet type (TYPE_DIR 8 lights, TYPE_NODIR 0 does not); S30 = the byte
    /// after gfx, which AttackSkillPackets only accepts as 0 for magic (3.81 S_UseAttackSkill writes 0).</summary>
    public static byte Flag(MagicEffect e)=>e.RangeType??0;
    /// <summary>R5-C：S250 子碼 83 值 3（3.81 S_GlobalEffect(3)）的煙火，sub_9357E0 case 83 以 `+9`＝1 建預設 Effect（FU5§3.2）。</summary>
    public const byte FireworkFlag=1;
    public static byte Flag(MagicEffect e,bool firework)=>firework?FireworkFlag:Flag(e);
    /// <summary>S106 (S_EFFECT_LOC, sub_934530): gfx 2028–2056 build the Effect with `+9` = 1, everything else 0.</summary>
    public static byte LocationFlag(int gfx)=>MagicBrighten.LocationEffect(gfx)?(byte)1:(byte)0;
    /// <summary>Offset from the effect start where the counter is incremented; null = this object never lights.</summary>
    public static long? BeginAfter(EffectSprite effect,LightClass c,byte flag)=>c switch{
        LightClass.Effect when flag!=0&&effect.Steps.Length>=2=>0,
        LightClass.Lightning when effect.Steps.Length>=3=>effect.Steps[1].Milliseconds,
        _=>null};
    /// <summary>True while the object holds its +1 (from BeginAfter until it finishes playing once).</summary>
    public static bool Lit(EffectSprite effect,LightClass c,byte flag,long age)=>BeginAfter(effect,c,flag) is long b&&age>=b&&age<effect.Duration;
    /// <summary>Same rule for a one-shot Effect whose frame offsets are known only as a duration (packaged S106 asset):
    /// the cursor reaches 1 at the start (the frame-count check cannot be made without list.spr: 推測).</summary>
    public static bool Lit(double duration,byte flag,long age)=>flag!=0&&age>=0&&age<duration;
}
// Native 109 class names used by the renderer (skills-visual.md §2.2).
public static class MagicClass {
    // Hardcoded kind 3 override (gfx 7972) and S42 757->10771 are TW16 specific and unverified
    // for 3.81 (SV#14); deliberately not applied.
    public static bool Flies(int kind,bool range)=>range?kind==5:kind is 3 or 4 or 5;
    // S30 kinds that never build an effect object (kind 8: `case 8: break`). The caster `!` frame then
    // hits the attached list directly (sub_A4B060). Kind 7 hits from its terminal EarthRising (T269).
    public static bool CasterHits(int kind,bool range)=>!range&&kind==8;
}
public sealed record UndrawnMagic(long Number,int Gfx,int Kind,(int X,int Y)[] Path,int EndGfx,string Reason);
public sealed partial class Renderer {
    EffectSpriteCatalog? effectSprites;
    readonly Dictionary<int,string> effectResourceErrors=[];
    public IReadOnlyDictionary<int,string> EffectResourceErrors=>effectResourceErrors;
    EffectSprite? NativeEffect(int gfx){
        if(effectResourceErrors.ContainsKey(gfx))return null;
        try{return (effectSprites??=new(GroundSpriteReader.ClientRoot)).Get(gfx);}
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException or OverflowException){effectResourceErrors[gfx]=ex.Message;return null;}
    }
    // depth: extra layer depth (EarthDepth, gfx 10147/10145), added to every block's Z and capped at 255.
    Frame? DrawNativeEffect(int gfx,int direction,int x,int y,long age,WorldSession session,List<Draw> draws,string layer,bool loop=false,int? step=null,int depth=0){
        var effect=NativeEffect(gfx);if(effect==null||age<0||!loop&&age>=effect.Duration||!session.SceneContains(x,y))return null;
        try{
            var frame=effectSprites!.Frame(effect,direction,age,loop,step);
            draws.AddRange(frame.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,EarthDepth.Z(b.Z,depth),1200000+b.Order,layer,b.Order,-1,-1,b.Tile)));return frame;
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException){effectResourceErrors[gfx]=ex.Message;return null;}
    }
    static bool MagicCasterBlocked(MagicEffect e,WorldSession session){
        var caster=session.Objects.FirstOrDefault(o=>o.ObjectId==e.Caster);
        return e.Caster==session.Actor?.ObjectId
            ?session.OwnDead||session.SelfAppearancePending||e.Timing is {} ownTiming&&!ownTiming.Matches(session.Actor!.Gfx,session.Actor.Weapon)
            :caster!=null&&(caster.Dead||caster.AppearancePending||e.Timing is {} otherTiming&&!otherTiming.Matches(caster.Gfx,caster.Action));
    }
    void MagicHit(WorldSession session,uint id,long at){
        if(id==0||session.IsHitReactionSuppressed(id))return;
        if(!projectileHits.TryGetValue(id,out var hit)||hit.At<at)projectileHits[id]=new(2,at,false);
    }
    // No effect object: the caster `!` frame hits every nonzero entry (sub_A4B060 loop uses entry i).
    void CasterFrameHits(MagicEffect e,WorldSession session,long release){
        if(e.RangeType!=null){foreach(var t in session.RangeTargets(e.Number))if(t.HitFlag!=0)MagicHit(session,t.ObjectId,release);}
        else if(e.DamageWord>0)MagicHit(session,e.Target,release);
    }
    // skills-visual.md §2–§4: every class starts at the caster `!` frame (SV#5). Default classes hit
    // on the effect's own `!` frames; flying classes hit on arrival.
    void ComposeNativeMagic(MagicEffect e,WorldSession session,List<Draw> draws,List<MagicVisual> visuals,List<UndrawnMagic> undrawn,long now){
        if(now<e.At)return;
        // R5-C：S_GlobalEffect(3) 煙火由 sub_8AB390 直接建預設 Effect（不看 list.spr 109 kind），`+9`＝1。
        bool firework=session.IsFireworkEffect(e.Number);
        var effect=NativeEffect(e.Gfx);bool range=e.RangeType!=null;int kind=firework?0:effect?.Kind??-1;
        bool flies=effect!=null&&MagicClass.Flies(kind,range);
        // S30 case 6 builds the EarthRising on receipt: dword_13CCBA4 += 29 (TWP §3.2).
        if(effect!=null&&!range&&kind==6)EarthRisingBuilt(e.Number,-1);
        if(!magicFlights.ContainsKey(e.Number)&&(flies||e.Timing!=null)&&MagicCasterBlocked(e,session))return;
        // Caster `!` frame. A caster without a captured action (absent, caster0) starts at once (§2.1).
        long release=(e.ActionAt??e.At)+(e.Timing?.ReleaseMs??0);if(now<release)return;
        if(effect==null||MagicClass.CasterHits(kind,range)){CasterFrameHits(e,session,release);return;}
        // T269: S30 kind 7 Eruption segments, then the terminal EarthRising hits (ProjectileRendering.cs).
        if(!range&&kind==7){ComposeEruption(e,effect,session,draws,visuals,undrawn,release,now);return;}
        var arrow=new ProjectileReport(e.Number,e.Caster,e.Target,e.Gfx,e.Heading,e.SourceX,e.SourceY,e.TargetX,e.TargetY,e.At);
        bool Arrived(int tx,int ty,long tick)=>kind==3?ProjectileMotion.At(arrow,tx,ty,release,tick)==null:MagicMotion.At(e,tx,ty,release,tick).Arrived;
        if(!magicFlights.TryGetValue(e.Number,out var flight)){
            int tx,ty;
            // S42 default classes: packet x,y (caster). S30 default: constructed at the packet target.
            if(range&&!flies){tx=e.SourceX;ty=e.SourceY;}
            else if(!flies&&kind!=6){tx=e.TargetX;ty=e.TargetY;}
            // Flying classes and kind 6 (81CF30) re-aim at the target's current position at start (§2.3).
            else{var aim=session.VisibleTargetPosition(e.Target);tx=aim?.X??e.TargetX;ty=aim?.Y??e.TargetY;}
            long impact=release;
            if(flies)while(impact<release+6000&&!Arrived(tx,ty,impact))impact+=kind==3?30:60;
            flight=(tx,ty,release,impact);magicFlights[e.Number]=flight;
        }
        if(!flies){
            if(effect.LatestHit(release,now) is long hitAt){
                if(!range){if(e.DamageWord>0)MagicHit(session,e.Target,hitAt);}
                else{
                    // Original defect kept on purpose (§2.4, 0x8AC26E imul eax,0): any nonzero entry
                    // triggers the hit on entry 0 only, so only the FIRST target reacts.
                    var list=session.RangeTargets(e.Number);
                    if(list.Count>0&&list.Any(t=>t.HitFlag!=0))MagicHit(session,list[0].ObjectId,hitAt);
                }
            }
            // R4-G (FU5§3): default Effect with +9 != 0 from index 1, kind 2 Lightning from index 2, until it finishes.
            if(SkillLight.Lit(effect,SkillLight.Class(kind,range),SkillLight.Flag(e,firework),now-release))magicBrightening++;
            int depth=!range&&kind==6&&EarthDepth.Stacks(e.Gfx)?EarthDepthAt(e.Number,-1,effect,now-release):0;   // R6-F: read on every frame advance
            var still=DrawNativeEffect(e.Gfx,e.Heading,flight.X,flight.Y,now-release,session,draws,"magic",depth:depth);
            if(still!=null)visuals.Add(new(e.Number,e.Gfx,flight.X,flight.Y,e.Heading,still.Key,"effect",release));
            // Default Effect class: sub_8ABC10 spawns `]` children at the effect's own position (effects-2.md §2.1(a)).
            // T269: S30 kind 2 Lightning (8ACB20) and kind 6 EarthRising (81C7C0) never process `]` (TWP §3.1).
            // R5-C（T308）查證：S42 不建 Lightning。sub_92FE40（S_ATTACK_MANY，VA 0x92FE40–0x930A20）的直接呼叫裡沒有
            // sub_8AB900（Lightning 建構子），只有 sub_83CB50（kind 5）、3 處 sub_8AB710（預設類別）與 3 處 sub_8AB390；
            // 所以 S42 的 kind 2 gfx 是預設 Effect，`]` 照常處理（SV§2.2 表「S42 建構：同預設」）。gap-matrix 14.2
            // 第 5 點「range 為真時 kind 2 也不處理 `]`」與此不符，維持 range 時處理 `]`（報告 issue-735.md）。
            if(now-release<effect.Duration&&(range||kind is not (2 or 6)))ObserveEffectFrames(session,$"magic:{e.Number}",ActionTimeline.Of(effect),now-release,flight.X,flight.Y,e.Heading,now);
            return;
        }
        var motion=MagicMotion.At(e,flight.X,flight.Y,release,Math.Min(now,flight.Impact));
        int x=motion.X,y=motion.Y,d=ProjectileMotion.Direction(e.SourceX,e.SourceY,flight.X,flight.Y,e.Heading);
        if(kind==3&&ProjectileMotion.At(arrow,flight.X,flight.Y,release,now) is {} arrowMotion){x=arrowMotion.X;y=arrowMotion.Y;}
        if(now<flight.Impact){
            // Flying classes do not touch the light counter (FU5§3: only sub_8ABC10 / sub_8ACB20 do).
            var fly=DrawNativeEffect(e.Gfx,d,x,y,now-release,session,draws,"magic",true);
            if(fly!=null)visuals.Add(new(e.Number,e.Gfx,x,y,d,fly.Key,"fly",release));
            // Flying classes never process `]` (TWP §3.1, T266): recorded as data, never spawned.
            WithholdFlightFrames(session,$"magic-fly:{e.Number}",effect,now-release,x,y,d,now);
            return;
        }
        var landings=new List<(int X,int Y)>();
        if(kind==5){
            // sub_83CE90: on arrival every target with damage is hit and every target gets the param gfx.
            if(range){
                var known=magicLandings??=[];
                if(!known.TryGetValue(e.Number,out var at)){
                    at=session.RangeTargets(e.Number).Select(t=>session.VisibleTargetPosition(t.ObjectId)).Where(p=>p!=null).Select(p=>((int)p!.Value.X,(int)p.Value.Y)).ToArray();
                    known[e.Number]=at;
                }
                foreach(var t in session.RangeTargets(e.Number))if(t.HitFlag!=0)MagicHit(session,t.ObjectId,flight.Impact);
                landings.AddRange(at);
            }else{if(e.DamageWord>0)MagicHit(session,e.Target,flight.Impact);landings.Add((flight.X,flight.Y));}
        }else{
            if(e.DamageWord>0)MagicHit(session,e.Target,flight.Impact);
            // Kind 4 places the param gfx; the kind 3 class never reads param (SV#10, 2349 no 219).
            if(kind==4)landings.Add((flight.X,flight.Y));
        }
        if(effect.Impact<0)return;
        var impactEffect=NativeEffect(effect.Impact);int landing=0;
        foreach(var (lx,ly) in landings){
            var frame=DrawNativeEffect(effect.Impact,d,lx,ly,now-flight.Impact,session,draws,"magic");
            if(frame!=null)visuals.Add(new(e.Number,effect.Impact,lx,ly,d,frame.Key,"impact",flight.Impact));
            // Landing param gfx is a one-shot effect; its own `]` frames spawn children (EF2§2.1(a)).
            ObserveLandingFrames(session,$"magic-impact:{e.Number}:{landing++}",impactEffect,now-flight.Impact,lx,ly,d,now);
        }
    }
    // One-shot landing / legacy effects: `]` children at the effect's position while it plays.
    void ObserveLandingFrames(WorldSession session,string owner,EffectSprite? effect,long age,int x,int y,int direction,long now){
        if(effect!=null&&age>=0&&age<effect.Duration)ObserveEffectFrames(session,owner,ActionTimeline.Of(effect),age,x,y,direction,now);
    }
    int magicBrightening;
    /// <summary>SV#12／FU5§3：目前握著提亮計數的特效數（原版 `0x13CE258`）：S30／S42 技能特效與 S106 gfx 2028–2056。</summary>
    public int MagicBrighteningEffects {get;private set;}
    /// <summary>SV#12：把明暗 L 套上特效提亮（有任何一個存活時 min(15, L+5)）。純函式；產品路徑已由
    /// <see cref="SyncLightEffects"/> 經 Ambience.BeginLightEffect／EndLightEffect 套到明暗，不要再對 Ambience 的 L 套一次。</summary>
    public int MagicLightLevel(int level)=>MagicBrighten.Apply(level,MagicBrighteningEffects);
    // Begin/End calls this renderer currently holds on that Ambience (lazy: constructor-free test renderers).
    Ambience? lightAmbience;int lightHeld;
    /// <summary>R4-G：讓 Ambience 的計數等於目前提亮中的特效數（每個 +1 對應一次 Begin，結束對應一次 End）。</summary>
    void SyncLightEffects(Ambience ambience,int active){
        if(!ReferenceEquals(ambience,lightAmbience)){lightAmbience=ambience;lightHeld=0;}
        while(lightHeld<active){ambience.BeginLightEffect();lightHeld++;}
        while(lightHeld>active){ambience.EndLightEffect();lightHeld--;}
    }
    // S106 (op 106 here; S_EFFECT_LOC sub_934530) builds a default Effect with +9 = 1 for gfx 2028–2056 (FU5§3.2).
    int LocationLights(WorldSession session,long now){
        int lit=0;
        foreach(var p in session.PacketEffects){
            if(p.Packet.Target!=null)continue;byte flag=SkillLight.LocationFlag(p.Packet.GfxId);if(flag==0)continue;
            long age=now-p.At;if(age<0)continue;
            // Runtime list.spr first (frame-accurate); else the packaged appearance that ComposePacketEffects draws.
            var native=effectResourceErrors==null?null:NativeEffect(p.Packet.GfxId);
            if(native!=null?SkillLight.Lit(native,LightClass.Effect,flag,age):packetEffectAssets?.Get(p.Packet.GfxId) is {} a&&SkillLight.Lit(a.Duration,flag,age))lit++;
        }
        return lit;
    }
    readonly Dictionary<int,ObjectAppearance> magicAppearances=[];
    readonly Dictionary<long,(int X,int Y,long Release,long Impact)> magicFlights=[];
    // Lazily created: constructor-free test renderers do not run field initializers.
    Dictionary<long,(int X,int Y)[]>? magicLandings;
    int magicEpoch=-1;
    public MagicVisual[] MagicVisuals {get;private set;}=[];
    // Effects whose drawing is deliberately withheld (unverified presentation), as data.
    public UndrawnMagic[] UndrawnMagicEffects {get;private set;}=[];
    ObjectAppearance MagicAppearance(int gfx){if(!magicAppearances.TryGetValue(gfx,out var a)){a=new(Path.Combine(assets.Root,$"objects/magic-{gfx}.json"));magicAppearances[gfx]=a;}return a;}
    void ComposeMagic(WorldSession session,List<Draw> draws,long now){
        if(magicEpoch!=session.WorldEpoch){magicFlights.Clear();magicLandings?.Clear();ClearEruptions();magicEpoch=session.WorldEpoch;}var visuals=new List<MagicVisual>();var undrawn=new List<UndrawnMagic>();
        // R5-C（sub_A40250）：施法者動作播完的排隊封包在這一格播出（與收包同一個 UI 執行緒）。
        session.RunQueuedSkillCasts(session.ActionClock());
        magicBrightening=0;
        foreach(var e in session.MagicEffects){
            if(!LegacyMagic.Handles(e)||effectResourceErrors!=null&&LegacyMagic.UsesNativeClass(e,NativeEffect(e.Gfx))){
                ComposeNativeMagic(e,session,draws,visuals,undrawn,now);continue;
            }
            int gfx=e.Gfx,x=e.TargetX,y=e.TargetY,d=e.Heading;long at=e.At;Frame? frame=null;string phase="heal";
            if(now<at)continue;
            if(gfx!=167){
                // R2-H: start at the caster `!` (SV#5) instead of the receipt time; once started the
                // effect is no longer gated by the caster (same as ComposeNativeMagic).
                if(!magicFlights.TryGetValue(e.Number,out var started)){
                    if(e.Timing!=null&&MagicCasterBlocked(e,session))continue;
                    long start=LegacyMagic.Start(e);if(now<start)continue;
                    started=(x,y,start,start);magicFlights[e.Number]=started;
                }
                at=started.Release;phase=gfx==11722?"heal":"effect";
                // Constructor-free test renderers have no resource state: treat as no native metadata.
                var native=effectResourceErrors==null?null:NativeEffect(gfx);
                if(e.Target!=0&&e.DamageWord>0&&LegacyMagic.Hit(native,at,now) is long hitAt)MagicHit(session,e.Target,hitAt);
                var a=MagicAppearance(gfx);if(now-at>=a.Duration(phase))continue;
                ObserveEffectFrames(session,$"magic:{e.Number}",native!=null?ActionTimeline.Of(native):ActionTimeline.Pure(a.Sequences[(phase,0)],false),now-at,x,y,d,now);
                frame=MagicMotion.PureFrame(a,phase,now-at);
            }
            else if(gfx==167){
                long release=(e.ActionAt??e.At)+(e.Caster==0?0:e.Timing?.ReleaseMs??MagicRules.ReleaseFor(e.CasterGfx));
                if(now<release)continue;
                if(!magicFlights.TryGetValue(e.Number,out var flight)){
                    var caster=session.Objects.FirstOrDefault(o=>o.ObjectId==e.Caster);if(e.Caster==session.Actor?.ObjectId?session.OwnDead||session.SelfAppearancePending||e.Timing is {} ownTiming&&!ownTiming.Matches(session.Actor!.Gfx,session.Actor.Weapon):caster!=null&&(caster.Dead||caster.AppearancePending||e.Timing is {} otherTiming&&!otherTiming.Matches(caster.Gfx,caster.Action)))continue;
                    var target=session.Objects.FirstOrDefault(o=>o.ObjectId==e.Target);int tx=target?.X??e.TargetX,ty=target?.Y??e.TargetY;long impact=release;
                    while(impact<release+6000&&!MagicMotion.At(e,tx,ty,release,impact).Arrived)impact+=60;
                    flight=(tx,ty,release,impact);magicFlights[e.Number]=flight;
                }
                var motion=MagicMotion.At(e,flight.X,flight.Y,flight.Release,Math.Min(now,flight.Impact));x=motion.X;y=motion.Y;d=motion.Direction;
                if(now<flight.Impact){phase="fly";frame=MagicAppearance(167).Frames[$"{d}:{(motion.Step-1)%4}"];at=flight.Release;}
                else {gfx=219;phase="impact";at=flight.Impact;var a=MagicAppearance(gfx);if(now-at>=a.Duration("impact"))continue;frame=MagicMotion.PureFrame(a,"impact",now-at);if(e.Target!=0&&e.DamageWord>0&&(!projectileHits.TryGetValue(e.Target,out var hit)||hit.At<at))projectileHits[e.Target]=new(2,at,false);
                    if(effectResourceErrors!=null)ObserveLandingFrames(session,$"magic-impact:{e.Number}:0",NativeEffect(219),now-at,x,y,d,now);}
            }
            if(frame==null||!session.SceneContains(x,y))continue;visuals.Add(new(e.Number,gfx,x,y,d,frame.Key,phase,at));draws.AddRange(frame.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1200000+b.Order,"magic",b.Order,-1,-1,b.Tile)));
        }
        MagicVisuals=visuals.ToArray();UndrawnMagicEffects=undrawn.ToArray();MagicBrighteningEffects=magicBrightening+LocationLights(session,now);
        SyncLightEffects(session.Ambience,MagicBrighteningEffects);
        foreach(var k in magicFlights.Where(x=>now-x.Value.Release>15000).Select(x=>x.Key).ToArray()){magicFlights.Remove(k);magicLandings?.Remove(k);ForgetEarthDepth(k);}
    }
}
