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
public sealed record MapLocalEffect(int Id,int X,int Y,int Offset,MapEffectAppearance Appearance);
public sealed record MapEffectVisual(string Block,int Generation,int Id,int X,int Y,string Frame,int Flags);
public sealed partial class Renderer {
    public MapEffectVisual[] MapEffectVisuals {get;private set;}=[];
    void ComposeMapEffects(WorldSession session,List<Draw> draws,long now){
        var visuals=new List<MapEffectVisual>();int serial=2000000;var states=session.Frames6016();
        if(session.MapCache is {} cache)foreach(var block in cache.Slots.OfType<OriginalMapBlock>())for(int ordinal=0;ordinal<block.Effects.Length;ordinal++){
            var effect=block.Effects[ordinal];var frame=WorldSession.MapEffectFrame(effect,block.Name,ordinal,cache.MapId,now-block.EffectsAt,states);visuals.Add(new(block.Name,block.Generation,effect.Id,effect.X,effect.Y,frame.Key,effect.Appearance.Flags));
            foreach(var b in frame.Blocks)draws.Add(new(2*(effect.X-Coordinates.MinX)+b.A,effect.Y-Coordinates.MinY+b.B,b.Z,serial++,"map-effect",b.Order,-1,effect.Offset,b.Tile));
        }
        MapAmbientSound.Observe(session,now);
        ClientSoundScene.Observe(session,now); // T172 背景音樂與 envsound.def
        ComposePacketEffects(session,draws,now);
        MapEffectVisuals=visuals.ToArray();
    }
}

public sealed record PacketEffect(long Number,int Epoch,ServerEffect Packet,long At,long Expires);
public sealed record PacketEffectVisual(long Number,int Gfx,int X,int Y,string Frame);

// Reuse verified packaged appearances; an unknown gfx never becomes a sound ID or a placeholder.
public sealed class PacketEffectAppearance
{
    public double Duration {get;}
    readonly Func<long,Frame> frame;
    public PacketEffectAppearance(MapEffectAppearance appearance)
    {
        Duration=appearance.Sequence.Sum(s=>(double)s.Ticks)*1000/24;
        frame=appearance.At;
    }
    public PacketEffectAppearance(ObjectAppearance appearance,string action)
    {
        Duration=appearance.Duration(action);
        frame=elapsed=>appearance.Sequences[(action,0)].All(s=>s.Milliseconds!=null)
            ?MagicMotion.PureFrame(appearance,action,elapsed)
            :appearance.At(action,0,elapsed%Duration);
    }
    public Frame At(long elapsed)=>frame(Math.Max(0,elapsed));
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
        if(packet.Target is uint target)
        {
            LastItemEffect=new{number=++ItemEffectNumber,actor=target,effect=packet.GfxId};InventoryRevision++;
            if(PacketEffectPosition(target)==null)
            {PacketMediaNotice=$"S55 特效 {packet.GfxId} 的對象不在可見世界；已略過";return;}
        }
        long now=PacketMediaClock();
        // Product policy: nonzero time is seconds; absent/zero uses one animation cycle.
        // The 15 s fallback only bounds missing assets until the renderer resolves the real cycle.
        long lifetime=packet.Time is >0?packet.Time.Value*1000L:15000;
        packetEffects.Add(new(++packetEffectNumber,WorldEpoch,packet,now,now+lifetime));
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
            var position=item.Packet.Target is uint target?PacketEffectPosition(target):(item.Packet.X,item.Packet.Y);
            if(position==null){packetEffects.Remove(item);continue;}
            var frame=appearance.At(now-item.At);
            result.Add(new(item.Number,item.Packet.GfxId,position.Value.Item1,position.Value.Item2,frame.Key));
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
    }
}
