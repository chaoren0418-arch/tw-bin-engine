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
        MapEffectVisuals=visuals.ToArray();
    }
}
