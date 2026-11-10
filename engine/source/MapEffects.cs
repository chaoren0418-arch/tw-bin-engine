using System.Text.Json;
using System.Security.Cryptography;
namespace P06;
public sealed record MapEffectStep(int Frame,int Ticks,string Events);
public sealed class MapEffectAppearance {
    public int Id {get;}public int Flags {get;}public MapEffectStep[] Sequence {get;}public Frame[] Frames {get;}
    public MapEffectAppearance(byte[] bytes){
        using var document=JsonDocument.Parse(bytes);var j=document.RootElement;Id=j.I("id");Flags=j.I("flags");if(j.I("tickRate")!=24||Flags is not(0 or 8 or 128 or 256))throw new InvalidDataException("map effect timing/flags");
        int type=Flags switch{8=>18,128=>34,256=>6,_=>2};
        Frames=j.GetProperty("frames").EnumerateArray().Select(f=>new Frame(f.I("index").ToString(),f.A("bounds"),f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("order"),new Tile(type,b.GetProperty("pixels").EnumerateArray().Select(p=>{var v=p.EnumerateArray().Select(n=>n.GetInt32()).ToArray();if(v.Length!=3||v[0] is <0 or >23||v[1] is <0 or >23||v[2] is <0 or >32767)throw new InvalidDataException("map effect RGB555 pixel");return new Pixel(v[0],v[1],(ushort)v[2]);}).ToArray()))).ToArray())).ToArray();
        Sequence=j.GetProperty("sequence").EnumerateArray().Select(s=>new MapEffectStep(s.I("frame"),s.I("ticks"),s.S("events"))).ToArray();
        if(Frames.Length is <1 or >127||Sequence.Length==0||Sequence.Any(s=>s.Ticks<=0||s.Ticks>10000||s.Frame<0||s.Frame>=Frames.Length)||Frames.Any(f=>f.Blocks.Length is <1 or >4096))throw new InvalidDataException("map effect frames/sequence");
    }
    public Frame At(long elapsed){double tick=Math.Max(0,elapsed)*24d/1000%Sequence.Sum(s=>s.Ticks);foreach(var s in Sequence){if(tick<s.Ticks)return Frames[s.Frame];tick-=s.Ticks;}return Frames[Sequence[^1].Frame];}
    ActionTimeline? timeline;
    /// <summary>各格開始時間（毫秒，與 At 同一尺度）與 token，循環播放。</summary>
    public ActionTimeline Timeline=>timeline??=Build();
    ActionTimeline Build(){double t=0;var steps=new List<TimelineStep>();foreach(var s in Sequence){steps.Add(new(t,s.Events));t+=s.Ticks*1000d/24;}return new(steps.ToArray(),t,true);}
}
public sealed class MapEffectCatalog {
    readonly string root;readonly Dictionary<int,JsonElement> entries;readonly Dictionary<int,MapEffectAppearance> cache=[];
    public MapEffectCatalog(string mapsRoot){
        root=Path.Combine(mapsRoot,"effects");var integrity=Json.Read(Path.Combine(mapsRoot,"../integrity.json"));var pins=integrity.GetProperty("files").EnumerateArray().Where(f=>f.S("path")=="maps/effects/manifest.json").ToArray();if(pins.Length!=1)throw new InvalidDataException("map effect manifest integrity entry missing");
        using var doc=JsonDocument.Parse(Verified(Path.Combine(root,"manifest.json"),pins[0].S("sha256")));var j=doc.RootElement;if(j.S("schema")!="p24-map-effects-v1")throw new InvalidDataException("map effect schema");entries=j.GetProperty("effects").EnumerateArray().ToDictionary(e=>e.I("id"),e=>e.Clone());
    }
    static byte[] Verified(string path,string hash){var data=File.ReadAllBytes(path);if(!Convert.ToHexString(SHA256.HashData(data)).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("map effect source hash: "+path);return data;}
    public MapEffectAppearance Get(int id){if(cache.TryGetValue(id,out var hit))return hit;if(!entries.TryGetValue(id,out var e))throw new NotSupportedException("尚未封裝原始地圖特效 "+id);var result=new MapEffectAppearance(Verified(Path.Combine(root,id+".json"),e.S("sha256")));if(result.Id!=id)throw new InvalidDataException("map effect identity");cache[id]=result;return result;}
}
/// <summary>A map-local effect from the .s32 tail. Light is the light-map index k recorded at load (EF2§1.1(b)).</summary>
public sealed record MapLocalEffect(int Id,int X,int Y,int Offset,MapEffectAppearance Appearance){
    public byte Light {get;}=MapEffectLightTable.Radius(Id);
}
public sealed record MapEffectVisual(string Block,int Generation,int Id,int X,int Y,string Frame,int Flags);
/// <summary>A map effect acting as a light source (k &gt; 0) at world tile (X, Y).</summary>
public sealed record MapEffectLight(int Id,int X,int Y,byte Light);

/// <summary>TW16 .s32 map effect id → light radius index k (docs/reference/effects-2.md §1.1(b)): sub_F0B570 passes it as
/// the 7th sub_A4E5D0 argument, stored at actor+9 like the S40 light. Tables word_138BE2C (FO 0xF8A62C, k 8) and
/// word_138BE44 (FO 0xF8A644, k 11) via F112A0/F11250; the ranges are immediate compares in F0B570. 3442–3446 ("dot range
/// five…thirteen") are 1-pixel invisible light points, k = 2·id − 6879; no extra halo is drawn for them.</summary>
public static class MapEffectLightTable {
    static readonly HashSet<int> eight=[3552,3737,3601,3604,3607,3610,3613,3616,3619,3622,3625,3628];
    static readonly HashSet<int> eleven=[1992,1993,2088,2094,2097,2214,2215,2217,2219,2221,2223,3550,3580,3736];
    public static byte Radius(int id)=>id switch{
        _ when eight.Contains(id)=>8,
        _ when eleven.Contains(id)=>11,
        1884 or 1885 or >=1901 and <=1987 or 2209=>5,
        >=1892 and <=1897=>7,
        2195 or >=2197 and <=2202=>9,
        149 or 1522=>13,
        1886 or 2196=>14,
        >=3442 and <=3446=>(byte)(2*id-6879),
        _=>0};
}
public sealed partial class Renderer {
    public MapEffectVisual[] MapEffectVisuals {get;private set;}=[];
    /// <summary>Map effects with k &gt; 0 from the last composition, for ComposeAmbienceLights.</summary>
    public MapEffectLight[] MapEffectLightSources {get;private set;}=[];
    void ComposeMapEffects(WorldSession session,List<Draw> draws,long now){
        var visuals=new List<MapEffectVisual>();var lights=new List<MapEffectLight>();int serial=2000000;var states=session.Frames6016();
        if(session.MapCache is {} cache)foreach(var block in cache.Slots.OfType<OriginalMapBlock>())for(int ordinal=0;ordinal<block.Effects.Length;ordinal++){
            var effect=block.Effects[ordinal];var frame=WorldSession.MapEffectFrame(effect,block.Name,ordinal,cache.MapId,now-block.EffectsAt,states);visuals.Add(new(block.Name,block.Generation,effect.Id,effect.X,effect.Y,frame.Key,effect.Appearance.Flags));
            if(effect.Light>0)lights.Add(new(effect.Id,effect.X,effect.Y,effect.Light));
            foreach(var b in frame.Blocks)draws.Add(new(2*(effect.X-Coordinates.MinX)+b.A,effect.Y-Coordinates.MinY+b.B,b.Z,serial++,"map-effect",b.Order,-1,effect.Offset,b.Tile));
            ObserveMapEffectFrames(session,$"map-effect:{cache.MapId}:{block.Name}:{block.Generation}:{ordinal}",effect,now-block.EffectsAt,now);
        }
        MapAmbientSound.Observe(session,now);
        ClientSoundScene.Observe(session,now); // T172 背景音樂與 envsound.def
        ComposePacketEffects(session,draws,now);
        MapEffectVisuals=visuals.ToArray();MapEffectLightSources=lights.ToArray();
    }
    // R5-C（T308；TWP§3.4、EF2§2.1）：`.s32` 地圖特效在原版是 UserObject（sub_F0E9B0→sub_A4E5D0→sub_A51920），
    // 待機動畫（「+194＋3」動作）經佇列走到每格回呼 sub_A4B060，`]N` 那格在特效當下位置建 gfx N 的預設 Effect。
    // 引擎取捨（推定）：
    // - 方向取 0（.s32 尾段沒有方向欄；UserObject +8 的初值沒查）。
    // - 6016（狀態驅動的影格，WorldEffect6016.cs）不是循環待機動畫，不處理。
    // - 只處理在可見場景內的特效：原版 FPS≥5 時畫面外也照播，但子特效本來就只畫在場景內，這樣可以不讓畫面外的
    //   地圖特效占滿子特效的 256 筆上限（SubEffectSet.MaxLive）。
    internal void ObserveMapEffectFrames(WorldSession session,string owner,MapLocalEffect effect,long age,long now){
        if(effect.Id==6016||age<0||!session.SceneContains(effect.X,effect.Y))return;
        var timeline=effect.Appearance.Timeline;if(!timeline.HasTokens)return;
        ObserveEffectFrames(session,owner,timeline,age,effect.X,effect.Y,0,now);
    }
}

/// <param name="Direction">R6-A（T332，SV§2.5）：S55 收包當下對象的朝向（0..7）；S106 與其他來源為 0。</param>
/// <param name="Follows">R6-A：只有 <see cref="PacketEffectRules.Following"/> 的 gfx 每格重讀對象位置。</param>
/// <param name="Origin">R6-A：S55 收包當下對象的位置；不跟隨的特效一直畫在這裡。</param>
public sealed record PacketEffect(long Number,int Epoch,ServerEffect Packet,long At,long Expires){
    public int Direction {get;init;}
    public bool Follows {get;init;}
    public (int X,int Y)? Origin {get;init;}
}
public sealed record PacketEffectVisual(long Number,int Gfx,int X,int Y,string Frame,int Direction=0);

/// <summary>R6-A（T332）：SV§2.5「S_EFFECT（S55）收包立刻在物件位置以物件朝向播放，不等動作；
/// 6985、7179、8150、8152、8981、7834、7836 會跟著物件」。其他 gfx 留在收包位置。</summary>
public static class PacketEffectRules
{
    public static readonly IReadOnlySet<int> Following=new HashSet<int>{6985,7179,8150,8152,8981,7834,7836};
    public static bool Follows(int gfx)=>Following.Contains(gfx);
}

// Reuse verified packaged appearances; an unknown gfx never becomes a sound ID or a placeholder.
public sealed class PacketEffectAppearance
{
    public double Duration {get;}
    // Frame start offsets with their tokens, same selection as At(); the default Effect class handles `]` (SubEffects.cs).
    public ActionTimeline Timeline=>TimelineFor(0);
    readonly Func<long,int,Frame> frame;
    readonly Func<int,ActionTimeline> timelineOf;
    readonly Dictionary<int,ActionTimeline> timelines=[];
    public PacketEffectAppearance(MapEffectAppearance appearance)
    {
        Duration=appearance.Sequence.Sum(s=>(double)s.Ticks)*1000/24;
        frame=(elapsed,_)=>appearance.At(elapsed); // .s32 地圖特效素材沒有方向。
        timelineOf=_=>appearance.Timeline;
    }
    public PacketEffectAppearance(ObjectAppearance appearance,string action)
    {
        Duration=appearance.Duration(action);
        // R6-A：依朝向取序列；素材沒有該方向時退回方向 0（與既有行為相同）。
        int Facing(int direction)=>appearance.Supports(action,direction)?direction:0;
        frame=(elapsed,direction)=>
        {
            int d=Facing(direction);var sequence=appearance.Sequences[(action,d)];
            return sequence.All(s=>s.Milliseconds!=null)
                ?appearance.At(action,d,(elapsed+sequence[0].Milliseconds!.Value)%Duration) // 同 MagicMotion.PureFrame，換成方向 d。
                :appearance.At(action,d,elapsed%Duration);
        };
        timelineOf=direction=>
        {
            var sequence=appearance.Sequences[(action,Facing(direction))];
            if(sequence.All(s=>s.Milliseconds!=null))return ActionTimeline.Pure(sequence,true);
            var once=ActionTimeline.Of(sequence,false,Duration);return new(once.Steps,once.Length,true);
        };
    }
    public Frame At(long elapsed)=>At(elapsed,0);
    public Frame At(long elapsed,int direction)=>frame(Math.Max(0,elapsed),direction is >=0 and <=7?direction:0);
    public ActionTimeline TimelineFor(int direction)
    {
        direction=direction is >=0 and <=7?direction:0;
        if(!timelines.TryGetValue(direction,out var timeline))timelines[direction]=timeline=timelineOf(direction);
        return timeline;
    }
}

public sealed class PacketEffectAssets(string root)
{
    readonly Dictionary<int,PacketEffectAppearance?> cache=[];
    MapEffectCatalog? maps;
    public PacketEffectAppearance? Get(int gfx)
    {
        if(cache.TryGetValue(gfx,out var hit))return hit;
        PacketEffectAppearance? result=null;
        try
        {
            string path=Path.Combine(root,"objects",$"magic-{gfx}.json");
            if(File.Exists(path))
            {
                var integrity=Json.Read(Path.Combine(root,"integrity.json"));
                var pin=integrity.GetProperty("files").EnumerateArray().Single(f=>f.S("path")==$"objects/magic-{gfx}.json");
                using var source=File.OpenRead(path);
                if(!Convert.ToHexString(SHA256.HashData(source)).Equals(pin.S("sha256"),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("packet effect integrity");
                var appearance=new ObjectAppearance(path);
                string? action=MagicSpells.AttachedEffect(appearance);
                if(appearance.Gfx==gfx&&action!=null)result=new(appearance,action);
            }
            else result=new((maps??=new(Path.Combine(root,"maps"))).Get(gfx));
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or InvalidOperationException or JsonException or KeyNotFoundException)
        {System.Diagnostics.Trace.WriteLine($"Packet gfx {gfx} unavailable: {ex.Message}");}
        cache[gfx]=result;
        return result;
    }
}

public sealed partial class WorldSession
{
    readonly List<PacketEffect> packetEffects=[];
    long packetEffectNumber;
    public PacketEffect[] PacketEffects {get{PrunePacketMedia();return packetEffects.ToArray();}}

    internal (int X,int Y)? PacketEffectPosition(uint id)
    {
        if(Actor?.ObjectId==id)return SelfAppearancePending?null:(Actor.X,Actor.Y);
        var obj=Objects.FirstOrDefault(o=>o.ObjectId==id&&!o.AppearancePending);
        return obj==null?null:(obj.X,obj.Y);
    }

    // R6-A（SV§2.5）：S55 以對象收包當下的朝向播放。
    internal int PacketEffectDirection(uint id)
    {
        int direction=Actor?.ObjectId==id?Actor.Direction:Objects.FirstOrDefault(o=>o.ObjectId==id)?.Direction??0;
        return direction is >=0 and <=7?direction:0;
    }

    internal void ReceiveEffectPacket(byte[] plain)
    {
        ReceiveEffectPacketCore(plain);
        BasicSupplyReply(plain[0],plain);
    }

    void ReceiveEffectPacketCore(byte[] plain)
    {
        var packet=SoundPackets.Effect(plain);
        EnsurePacketMedia();
        // Preserve the established S55 item notifications, renderer and sprite sound events.
        // Only this path owns these six 8-byte variants, so there is no second gfx/audio dispatch.
        if(plain[0]==55&&plain.Length==8&&MagicSpells.AttachedEffect(packet.GfxId))
        {
            var reader=new LoginReader(plain);reader.C();ReceiveMagic(55,reader,plain);return;
        }
        (int X,int Y)? origin=null;int direction=0;bool follows=false;
        if(packet.Target is uint target)
        {
            LastItemEffect=new{number=++ItemEffectNumber,actor=target,effect=packet.GfxId};InventoryRevision++;
            origin=PacketEffectPosition(target);
            if(origin==null)
            {PacketMediaNotice=$"S55 特效 {packet.GfxId} 的對象不在可見世界；已略過";return;}
            // R6-A（SV§2.5）：收包當下的朝向與位置；只有 7 個 gfx 之後每格跟著對象。
            direction=PacketEffectDirection(target);follows=PacketEffectRules.Follows(packet.GfxId);
        }
        long now=PacketMediaClock();
        // Product policy: nonzero time is seconds; absent/zero uses one animation cycle.
        // The 15 s fallback only bounds missing assets until the renderer resolves the real cycle.
        long lifetime=packet.Time is >0?packet.Time.Value*1000L:15000;
        packetEffects.Add(new(++packetEffectNumber,WorldEpoch,packet,now,now+lifetime){Direction=direction,Follows=follows,Origin=origin});
        if(packetEffects.Count>256)packetEffects.RemoveAt(0);
        PacketMediaNotice=$"S{plain[0]} 圖像特效 {packet.GfxId}；time={packet.Time?.ToString()??"無"}";
    }

    public PacketEffectVisual[] PacketEffectFrames(long now,Func<int,PacketEffectAppearance?> resolve,Action<Frame,int,int>? draw=null)
    {
        var result=new List<PacketEffectVisual>();
        foreach(var item in PacketEffects)
        {
            var appearance=resolve(item.Packet.GfxId);
            if(appearance==null)
            {
                MagicNotice=PacketMediaNotice=$"圖像特效 {item.Packet.GfxId} 缺少已核定素材；未顯示";
                packetEffects.Remove(item);continue;
            }
            long end=item.Packet.Time is >0?item.Expires:item.At+(long)Math.Ceiling(appearance.Duration);
            if(now>=end){packetEffects.Remove(item);continue;}
            // R6-A：對象離開仍照舊結束特效（PrunePacketMedia）；只有跟隨的 gfx 每格重讀對象位置，其他畫在收包位置。
            (int X,int Y)? position=item.Packet.Target is uint target
                ?PacketEffectPosition(target) is {} current?(item.Follows||item.Origin==null?current:item.Origin.Value):null
                :(item.Packet.X,item.Packet.Y);
            if(position==null){packetEffects.Remove(item);continue;}
            var frame=appearance.At(now-item.At,item.Direction);
            result.Add(new(item.Number,item.Packet.GfxId,position.Value.Item1,position.Value.Item2,frame.Key,item.Direction));
            draw?.Invoke(frame,position.Value.Item1,position.Value.Item2);
        }
        return result.ToArray();
    }
}

public sealed partial class Renderer
{
    PacketEffectAssets? packetEffectAssets;
    public PacketEffectVisual[] PacketEffectVisuals {get;private set;}=[];
    void ComposePacketEffects(WorldSession session,List<Draw> draws,long now)
    {
        if(session.PacketEffects.Length==0){PacketEffectVisuals=[];return;}
        packetEffectAssets??=new(assets.Root);
        int serial=2500000;
        PacketEffectVisuals=session.PacketEffectFrames(now,packetEffectAssets.Get,(frame,x,y)=>
        {
            if(!session.SceneContains(x,y))return;
            foreach(var b in frame.Blocks)draws.Add(new(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,serial++,"packet-effect",b.Order,-1,-1,b.Tile));
        });
        // S55/S106 build the default Effect class (effects-2.md §4.1): its `]` frames spawn sub-effects
        // in the effect's direction (R6-A: S55 = target heading at receipt; S106 = 0).
        if(PacketEffectVisuals.Length==0)return;
        var started=session.PacketEffects.ToDictionary(p=>p.Number,p=>p.At);
        foreach(var v in PacketEffectVisuals)
            if(started.TryGetValue(v.Number,out long at)&&packetEffectAssets.Get(v.Gfx)?.TimelineFor(v.Direction) is {HasTokens:true} timeline)
                ObserveEffectFrames(session,$"packet:{v.Number}",timeline,now-at,v.X,v.Y,v.Direction,now);
    }
}
