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
    static byte[] DecodeText(byte[] data){
        // Original Text archive format constant (not an account credential).
        using var des=DES.Create();des.Key=Convert.FromHexString("7E214023255E243C");
        var result=(byte[])data.Clone();int length=data.Length/8*8;
        des.DecryptEcb(data.AsSpan(0,length),PaddingMode.None).CopyTo(result,0);return result;
    }
    Dictionary<string,Entry> Index(string stem,bool text=false){
        if(archives.TryGetValue(stem,out var cached))return cached;
        string path=Path.Combine(clientRoot,stem+".idx");var data=ReadFile(path,32*1024*1024);
        Require(data.Length>=4,"索引截斷");uint count=BinaryPrimitives.ReadUInt32LittleEndian(data);
        Require(count<=1000000&&data.Length==4L+28L*count,"僅支援 plain28 索引");
        byte[] rows=text?DecodeText(data[4..]):data[4..];var result=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
        string pak=Path.Combine(clientRoot,stem+".pak");long size=new FileInfo(pak).Length;
        for(int i=0;i<count;i++){
            var row=rows.AsSpan(i*28,28);uint offset=BinaryPrimitives.ReadUInt32LittleEndian(row),length=BinaryPrimitives.ReadUInt32LittleEndian(row[24..]);
            string name=Encoding.ASCII.GetString(row.Slice(4,20)).TrimEnd('\0');
            Require(name.Length>0&&name.All(c=>c is >= ' ' and <= '~')&&(long)offset+length<=size,"索引範圍或名稱錯誤");
            // Text contains repeated unrelated dialog names; only animation tables are read.
            if(text&&name is not ("list.spz" or "list.spr"))continue;
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
        var index=Index("Text",true);byte[] raw;
        if(index.TryGetValue("list.spz",out var packed)){
            raw=DecodeText(Payload(packed));Require(raw.AsSpan().StartsWith("S1223"u8),"未知 list.spz 格式");
            using var aes=Aes.Create();aes.Key=Convert.FromHexString("DC8401212A40200ADD25B9A70DB9C94E");
            byte[] iv=Convert.FromHexString("3E0978AAC4D53063300C5F9A807F2246");int length=(raw.Length-5)/16*16;
            var decoded=(byte[])raw.Clone();aes.DecryptCbc(raw.AsSpan(5,length),iv,PaddingMode.None).CopyTo(decoded,5);
            for(int i=5+length;i<raw.Length;i++)decoded[i]=(byte)(raw[i]^(length>0?raw[i-16]:iv[i-5]));raw=decoded;
        }else {Require(index.ContainsKey("list.spr"),"缺少動畫表");raw=DecodeText(Payload(index["list.spr"]));}
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
public sealed record EffectStep(int SubId,int Frame,int Milliseconds);
public sealed record EffectSprite(int Gfx,int Sprite,bool Directional,int Directions,int TileType,int Kind,int Impact,EffectStep[] Steps){
    public int Duration=>Steps.Sum(s=>s.Milliseconds);
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
        var tokens=Regex.Matches(action.Groups[3].Value,@"(\d+)\.(\d+):(\d+)[^\s]*");
        if(tokens.Count!=int.Parse(action.Groups[2].Value)||tokens.Count is <1 or >4096)throw new InvalidDataException("特效動畫長度");
        var fpsMatch=Regex.Match(entry,@"(?<![\d.])110\.[^(]*\((\d+)\)");int fps=fpsMatch.Success?int.Parse(fpsMatch.Groups[1].Value):24;
        if(fps is <1 or >1000)throw new InvalidDataException("特效 fps");
        var steps=tokens.Select(t=>new EffectStep(int.Parse(t.Groups[1].Value),int.Parse(t.Groups[2].Value),checked(1000*int.Parse(t.Groups[3].Value)/fps))).ToArray();
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
}
public sealed partial class Renderer {
    EffectSpriteCatalog? effectSprites;
    readonly Dictionary<int,string> effectResourceErrors=[];
    public IReadOnlyDictionary<int,string> EffectResourceErrors=>effectResourceErrors;
    EffectSprite? NativeEffect(int gfx){
        if(effectResourceErrors.ContainsKey(gfx))return null;
        try{return (effectSprites??=new(GroundSpriteReader.ClientRoot)).Get(gfx);}
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException or OverflowException){effectResourceErrors[gfx]=ex.Message;return null;}
    }
    Frame? DrawNativeEffect(int gfx,int direction,int x,int y,long age,WorldSession session,List<Draw> draws,string layer,bool loop=false,int? step=null){
        var effect=NativeEffect(gfx);if(effect==null||age<0||!loop&&age>=effect.Duration||!session.SceneContains(x,y))return null;
        try{
            var frame=effectSprites!.Frame(effect,direction,age,loop,step);
            draws.AddRange(frame.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1200000+b.Order,layer,b.Order,-1,-1,b.Tile)));return frame;
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException){effectResourceErrors[gfx]=ex.Message;return null;}
    }
    void ComposeNativeMagic(MagicEffect e,WorldSession session,List<Draw> draws,List<MagicVisual> visuals,long now){
        if(now<e.At)return;
        var effect=NativeEffect(e.Gfx);if(effect==null)return;
        int gfx=e.Gfx,x=e.TargetX,y=e.TargetY,d=e.Heading;long at=e.At;string phase="effect";bool loop=false;
        if(e.RangeType==null&&effect.Kind is 3 or 4){
            var caster=session.Objects.FirstOrDefault(o=>o.ObjectId==e.Caster);
            if(!magicFlights.ContainsKey(e.Number)&&(e.Caster==session.Actor?.ObjectId
                ?session.OwnDead||session.SelfAppearancePending||e.Timing is {} ownTiming&&!ownTiming.Matches(session.Actor!.Gfx,session.Actor.Weapon)
                :caster!=null&&(caster.Dead||caster.AppearancePending||e.Timing is {} otherTiming&&!otherTiming.Matches(caster.Gfx,caster.Action))))return;
            long release=(e.ActionAt??e.At)+(e.Timing?.ReleaseMs??0);if(now<release)return;
            var arrow=new ProjectileReport(e.Number,e.Caster,e.Target,e.Gfx,e.Heading,e.SourceX,e.SourceY,e.TargetX,e.TargetY,e.At);
            bool Arrived(int tx,int ty,long tick)=>effect.Kind==3?ProjectileMotion.At(arrow,tx,ty,release,tick)==null:MagicMotion.At(e,tx,ty,release,tick).Arrived;
            if(!magicFlights.TryGetValue(e.Number,out var flight)){
                var target=session.Objects.FirstOrDefault(o=>o.ObjectId==e.Target);
                int tx=e.Target==session.Actor?.ObjectId?session.Actor.X:target?.X??e.TargetX;
                int ty=e.Target==session.Actor?.ObjectId?session.Actor.Y:target?.Y??e.TargetY;
                long impact=release;while(impact<release+6000&&!Arrived(tx,ty,impact))impact+=effect.Kind==3?30:60;
                flight=(tx,ty,release,impact);magicFlights[e.Number]=flight;
            }
            var motion=MagicMotion.At(e,flight.X,flight.Y,release,Math.Min(now,flight.Impact));
            x=motion.X;y=motion.Y;d=ProjectileMotion.Direction(e.SourceX,e.SourceY,flight.X,flight.Y,e.Heading);
            if(effect.Kind==3&&ProjectileMotion.At(arrow,flight.X,flight.Y,release,now) is {} arrowMotion){x=arrowMotion.X;y=arrowMotion.Y;}
            if(now<flight.Impact){at=release;phase="fly";loop=true;}
            else{
                x=flight.X;y=flight.Y;at=flight.Impact;phase="impact";
                if(e.DamageWord>0&&e.Target!=0&&(!projectileHits.TryGetValue(e.Target,out var hit)||hit.At<at))projectileHits[e.Target]=new(2,at,false);
                gfx=effect.Impact;if(gfx<0)return;
            }
        }else if(e.DamageWord>0&&e.Target!=0&&(!projectileHits.TryGetValue(e.Target,out var hit)||hit.At<at))projectileHits[e.Target]=new(2,at,false);
        var frame=DrawNativeEffect(gfx,d,x,y,now-at,session,draws,"magic",loop);
        if(frame!=null)visuals.Add(new(e.Number,gfx,x,y,d,frame.Key,phase,at));
    }
    readonly Dictionary<int,ObjectAppearance> magicAppearances=[];
    readonly Dictionary<long,(int X,int Y,long Release,long Impact)> magicFlights=[];
    int magicEpoch=-1;
    public MagicVisual[] MagicVisuals {get;private set;}=[];
    ObjectAppearance MagicAppearance(int gfx){if(!magicAppearances.TryGetValue(gfx,out var a)){a=new(Path.Combine(assets.Root,$"objects/magic-{gfx}.json"));magicAppearances[gfx]=a;}return a;}
    void ComposeMagic(WorldSession session,List<Draw> draws,long now){
        if(magicEpoch!=session.WorldEpoch){magicFlights.Clear();magicEpoch=session.WorldEpoch;}var visuals=new List<MagicVisual>();
        foreach(var e in session.MagicEffects){
            if(e.RangeType!=null||e.Gfx is not (11722 or 221 or 2165 or 189 or 191 or 197 or 167)){
                ComposeNativeMagic(e,session,draws,visuals,now);continue;
            }
            int gfx=e.Gfx,x=e.TargetX,y=e.TargetY,d=e.Heading;long at=e.At;Frame? frame=null;string phase="heal";
            if(now<at)continue;
            if(gfx!=167&&e.Target!=0&&e.DamageWord>0&&(!projectileHits.TryGetValue(e.Target,out var immediate)||immediate.At<at))projectileHits[e.Target]=new(2,at,false);
            if(gfx==11722){var a=MagicAppearance(gfx);if(now-at>=a.Duration("heal"))continue;frame=MagicMotion.PureFrame(a,"heal",now-at);}
            else if(gfx is 221 or 2165 or 189 or 191 or 197){phase="effect";var a=MagicAppearance(gfx);if(now-at>=a.Duration(phase))continue;frame=MagicMotion.PureFrame(a,phase,now-at);}
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
                else {gfx=219;phase="impact";at=flight.Impact;var a=MagicAppearance(gfx);if(now-at>=a.Duration("impact"))continue;frame=MagicMotion.PureFrame(a,"impact",now-at);if(e.Target!=0&&e.DamageWord>0&&(!projectileHits.TryGetValue(e.Target,out var hit)||hit.At<at))projectileHits[e.Target]=new(2,at,false);}
            }
            if(frame==null||!session.SceneContains(x,y))continue;visuals.Add(new(e.Number,gfx,x,y,d,frame.Key,phase,at));draws.AddRange(frame.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1200000+b.Order,"magic",b.Order,-1,-1,b.Tile)));
        }
        MagicVisuals=visuals.ToArray();foreach(var k in magicFlights.Where(x=>now-x.Value.Release>15000).Select(x=>x.Key).ToArray())magicFlights.Remove(k);
    }
}
