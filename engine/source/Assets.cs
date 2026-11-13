using System.Security.Cryptography;
using System.Text.Json;
namespace P06;
public readonly record struct Pixel(int X,int Y,ushort Color);
public sealed record Tile(int Type,Pixel[] Pixels);
public sealed record Draw(int X,int Y,int Z,int Serial,string Layer,int SourceIndex,int Group,int Offset,Tile Tile,uint ObjectId=0) {
    public int Px => 24*(X-(int)Math.Floor(X/2.0)+Y);
    public int Py => 12*(Y-(int)Math.Floor(X/2.0))+756;
    public bool Actor => Layer=="actor";
}
public sealed record Block(int A,int B,int Z,int Order,Tile Tile);
public sealed record Frame(string Key,int[] Bounds,Block[] Blocks);
public sealed record Direction(int Id,int Dx,int Dy,int Sx,int Sy);
public static class Json {
    public static JsonElement Read(string path) { using var d=JsonDocument.Parse(File.ReadAllBytes(path));return d.RootElement.Clone(); }
    public static int I(this JsonElement e,string p)=>e.GetProperty(p).GetInt32();
    public static string S(this JsonElement e,string p)=>e.GetProperty(p).GetString()!;
    public static int[] A(this JsonElement e,string p)=>e.GetProperty(p).EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    public static void Save(string path,object data) { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);File.WriteAllText(path,JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true})); }
}
public sealed class Assets {
    public string Root {get;}
    public List<Draw> Scene {get;}=[];
    public Dictionary<string,Frame> Frames {get;}=[];
    public Dictionary<(string,int),string[]> Sequences {get;}=[];
    public Direction[] Directions {get;}
    public int FrameMs {get;}
    public int[] Spawn {get;}
    public int CanvasWidth {get;}
    public int CanvasHeight {get;}
    public Assets(string root) {
        Root=Path.GetFullPath(root);VerifyIntegrity(Root);
        (CanvasWidth,CanvasHeight)=LoadScene(root,Scene);
        LoadCharacter(root,Frames,Sequences);
        // T459（#1044）：方向、每格毫秒與離線起點是產品規則（MotionRules），不再讀 motion.json。
        FrameMs=MotionRules.FrameMs;Spawn=MotionRules.Spawn();Directions=MotionRules.Directions();
    }
    /// <summary>scene/manifest.json, tile-pixels.json and draw-commands.json (p22-dual-map-v1); returns the canvas size.
    /// Also used by the scene recipe checks (T457) on a prepared directory.</summary>
    public static (int Width,int Height) LoadScene(string root,List<Draw> scene) {
        var manifest=Json.Read(Path.Combine(root,"scene/manifest.json"));
        if(manifest.S("schema")!="p22-dual-map-v1" || !manifest.A("world_inclusive_bounds").SequenceEqual(new[]{Coordinates.MinX,Coordinates.MinY,Coordinates.MaxX,Coordinates.MaxY}) || !manifest.A("pixel_origin").SequenceEqual(new[]{0,756}) || manifest.I("terrain_stride")!=Coordinates.Columns || manifest.I("terrain_rows")!=Coordinates.Rows)throw new InvalidDataException("Unsupported scene format/bounds");
        var canvas=manifest.A("canvas_size");if(canvas.Length!=2||canvas.Any(v=>v<=0)||((long)canvas[0]*canvas[1])>16000000)throw new InvalidDataException("Invalid scene canvas");int width=canvas[0],height=canvas[1];
        var tiles=new Dictionary<(int,int),Tile>();
        foreach(var t in Json.Read(Path.Combine(root,"scene/tile-pixels.json")).EnumerateArray())tiles.Add((t.I("tile"),t.I("index")),new(t.I("type"),ReadPixels(t)));
        foreach(var c in Json.Read(Path.Combine(root,"scene/draw-commands.json")).EnumerateArray()) {
            var layer=c.S("layer");if(layer is not ("ground" or "cover" or "objects"))throw new InvalidDataException("Unknown draw layer");
            var t=tiles[(c.I("tile"),c.I("index"))];if((layer=="ground" && t.Type is not (0 or 1 or 16 or 17))||(layer!="ground"&&t.Type is not (2 or 3 or 6 or 7)))throw new InvalidDataException("Unsupported tile type/layer combination");
            scene.Add(new(c.I("x"),c.I("y"),c.TryGetProperty("z",out var z)?z.GetInt32():0,c.I("serial"),layer,c.I("source_index"),c.TryGetProperty("group",out var g)?g.GetInt32():-1,c.I("offset"),t));
        }
        if(scene.Count!=manifest.I("draw_count") || !scene.SequenceEqual(scene.OrderBy(x=>x,DrawComparer.Instance)) || scene.Any(c=>c.Px<0||c.Py<0||c.Px+24>width||c.Py+24>height))throw new InvalidDataException("Scene command count/order/bounds mismatch");
        return (width,height);
    }
    /// <summary>character/blocks.json and character/animation.json (also used by the asset-preparer checks, T454).</summary>
    public static void LoadCharacter(string root,Dictionary<string,Frame> frames,Dictionary<(string,int),string[]> sequences) {
        var blocks=Json.Read(Path.Combine(root,"character/blocks.json"));if(blocks.S("schema")!="p06-spr-blocks-v1")throw new InvalidDataException("Unsupported SPR block schema");
        foreach(var f in blocks.GetProperty("frames").EnumerateArray())frames.Add(f.S("key"),new(f.S("key"),f.A("bounds"),f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new(2,ReadPixels(b)))).ToArray()));
        var anim=Json.Read(Path.Combine(root,"character/animation.json"));if(anim.S("schema")!="p04-character-v1")throw new InvalidDataException("Unsupported animation schema");
        foreach(var m in anim.GetProperty("mapping").EnumerateArray()) {
            var keys=m.GetProperty("sequence").EnumerateArray().Select(s=>s.S("key")).ToArray();
            if(keys.Any(k=>!frames.ContainsKey(k)))throw new InvalidDataException("Animation references absent frame");
            sequences.Add((m.S("action"),m.I("direction")),keys);
        }
        if(frames.Count!=96||sequences.Count!=16)throw new InvalidDataException("Incomplete character");
    }
    static Pixel[] ReadPixels(JsonElement t)=>t.GetProperty("pixels").EnumerateArray().Select(p=>{
        var v=p.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        if(v.Length!=3||v[0]<0||v[0]>=24||v[1]<0||v[1]>=24||v[2]<0||v[2]>32767)throw new InvalidDataException("Invalid RGB555 sparse pixel");
        return new Pixel(v[0],v[1],(ushort)v[2]);}).ToArray();
    public static void VerifyIntegrity(string root) {
        var m=Json.Read(Path.Combine(root,"integrity.json"));
        if(m.S("schema")!="p06-assets-integrity-v1")throw new InvalidDataException("Unsupported asset integrity schema");
        foreach(var f in m.GetProperty("files").EnumerateArray()) {
            var p=Path.GetFullPath(Path.Combine(root,f.S("path")));
            if(!p.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Asset path escapes root");
            // Streaming map resources are verified when opened; unrelated missing maps must not prevent offline startup.
            if(f.S("path").StartsWith("maps/",StringComparison.Ordinal))continue;
            if(!File.Exists(p))throw new FileNotFoundException("Required asset missing: "+f.S("path"));
            using var stream=File.OpenRead(p);
            if(stream.Length!=f.GetProperty("size").GetInt64()||!Convert.ToHexString(SHA256.HashData(stream)).Equals(f.S("sha256"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Asset integrity mismatch: "+f.S("path"));
        }
    }
}
public sealed class DrawComparer:IComparer<Draw> {
    public static readonly DrawComparer Instance=new();
    public int Compare(Draw? a,Draw? b) {
        if(a==null||b==null)throw new ArgumentException();
        int c=a.Py.CompareTo(b.Py);if(c!=0)return c;c=a.Px.CompareTo(b.Px);if(c!=0)return c;
        c=(a.Layer=="ground"?0:1).CompareTo(b.Layer=="ground"?0:1);if(c!=0)return c;
        c=a.Z.CompareTo(b.Z);return c!=0?c:b.Serial.CompareTo(a.Serial);
    }
}
