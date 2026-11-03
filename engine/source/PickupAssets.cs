using System.Security.Cryptography;
using System.Text.Json;
namespace P06;

// Finite own-only presentation profiles. Body keys are local to a gfx/sprite group.
internal sealed class PickupAssets {
    internal const string ResourceName="P06.Resources.PickupOwnBase15Gfx0Weapon0.json";
    internal const string ResourceSha="43b88cec329929484610aceae84566d1a922eb4b8ceba93e17dc44b65922ca90";
    internal const string BaseSha="27ab41e8a398eb180ce957d9215b2c9b5cf6bf5343b32f6c101757b8882efdf8";
    internal const string ExtensionName="P06.Resources.PickupOwnBase15ProfileExtension.json";
    internal const string ExtensionSha="aff2e04d4f9395f7993baf5818eb16218730b7def0a29c7d07e86c314db513ba";
    sealed record Body(int Sprite,Dictionary<string,Frame> Frames,string[][] Keys);
    sealed record Profile(int Sprite,string Path,string Sha,int Group);
    readonly Dictionary<int,Body> bodies=[];
    readonly Dictionary<(int,int),Profile> profiles=[];
    internal static bool Supports(int gfx,int weapon)=>gfx switch{
        0 => weapon is 0 or 4 or 20 or 46 or 50,
        1 => weapon is 0 or 4 or 20 or 50,
        37 => weapon is 0 or 4 or 20,
        48 => weapon is 0 or 4 or 20 or 50,
        61 => weapon is 0 or 4 or 20 or 50,
        734 => weapon is 0 or 4 or 20,
        1186 => weapon is 0 or 4 or 20,
        2786 => weapon is 0 or 4 or 20,
        2796 => weapon is 0 or 4 or 20,
        6650 => weapon is 0 or 20,
        6658 => weapon is 0 or 4 or 50,
        6661 => weapon is 0 or 4 or 50,
        6671 => weapon is 0 or 20,
        _ => false
    };
    static void Verify(byte[] bytes,int size,string sha){
        if(bytes.Length!=size||!Convert.ToHexString(SHA256.HashData(bytes)).Equals(sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Pickup resource integrity");
    }
    static Body ReadBody(JsonElement j){
        Dictionary<string,Frame> frames=[];var keys=new string[8][];
        foreach(var f in j.GetProperty("frames").EnumerateArray()){
            var blocks=f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new Tile(2,b.GetProperty("pixels").EnumerateArray().Select(p=>{
                var v=p.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v.Length!=3||v[0]<0||v[0]>=24||v[1]<0||v[1]>=24||v[2]<0||v[2]>32767)throw new InvalidDataException("Pickup RGB555 pixel");return new Pixel(v[0],v[1],(ushort)v[2]);
            }).ToArray()))).ToArray();frames.Add(f.S("key"),new(f.S("key"),f.A("bounds"),blocks));
        }
        foreach(var s in j.GetProperty("sequences").EnumerateArray()){
            int direction=s.I("direction");if(direction is <0 or >7||keys[direction]!=null)throw new InvalidDataException("Pickup direction");
            var steps=s.GetProperty("steps").EnumerateArray().ToArray();if(steps.Length!=4)throw new InvalidDataException("Pickup steps");
            keys[direction]=steps.Select((step,index)=>{
                string key=step.S("key");
                if(step.I("metadataIndex")!=index||step.I("ticks")!=(index<2?1:4)||step.S("storage")!=(index==0?"base-profile":"embedded")||index!=0&&!frames.ContainsKey(key)||index==0&&frames.ContainsKey(key))throw new InvalidDataException("Pickup frame reference");
                // Exact authenticated sequence keys; no sid/frame formula or event dispatch.
                return key;
            }).ToArray();
        }
        if(frames.Count!=24||keys.Any(k=>k==null)||keys.SelectMany(k=>k.Skip(1)).Distinct().Count()!=24)throw new InvalidDataException("Incomplete pickup resource");
        return new(j.I("sprite"),frames,keys);
    }
    internal PickupAssets(byte[] bytes){
        Verify(bytes,239766,ResourceSha);using var doc=JsonDocument.Parse(bytes);var j=doc.RootElement;
        if(j.S("schema")!="pickup-own-base15-gfx0-weapon0-v1"||j.I("gfx")!=0||j.I("weapon")!=0||j.I("sprite")!=3225||j.S("baseProfilePath")!="objects/0.json"||j.S("baseProfileSha256")!=BaseSha||!j.A("frameOrder").SequenceEqual(new[]{1,2,3,0}))throw new InvalidDataException("Pickup resource identity");
        bodies.Add(0,ReadBody(j));profiles.Add((0,0),new(3225,"objects/0.json",BaseSha,0));
    }
    internal PickupAssets(byte[] bytes,byte[] extension):this(bytes){
        Verify(extension,2473547,ExtensionSha);using var doc=JsonDocument.Parse(extension);var j=doc.RootElement;var requires=j.GetProperty("requiresResource");
        if(j.S("schema")!="pickup-own-base15-profile-extension-v1"||!j.A("frameOrder").SequenceEqual(new[]{1,2,3,0})||requires.S("logicalName")!=ResourceName||requires.S("sha256")!=ResourceSha)throw new InvalidDataException("Pickup extension identity");
        foreach(var g in j.GetProperty("groups").EnumerateArray())bodies.Add(g.I("gfx"),ReadBody(g));
        foreach(var p in j.GetProperty("profiles").EnumerateArray()){
            int gfx=p.I("gfx"),weapon=p.I("weapon"),group=p.I("sequenceGroup"),sprite=p.I("sprite");
            if(!Supports(gfx,weapon)||group!=gfx||!bodies.TryGetValue(group,out var body)||body.Sprite!=sprite||p.S("bodySource")!=(gfx==0?"r34-base-resource":"extension"))throw new InvalidDataException("Pickup profile identity");
            profiles.Add((gfx,weapon),new(sprite,p.S("baseProfilePath"),p.S("baseProfileSha256"),group));
        }
        if(bodies.Count!=13||profiles.Count!=42)throw new InvalidDataException("Incomplete pickup profiles");
    }
    static byte[] Resource(string name){using var stream=typeof(PickupAssets).Assembly.GetManifestResourceStream(name)??throw new InvalidDataException("Missing pickup resource");using var bytes=new MemoryStream();stream.CopyTo(bytes);return bytes.ToArray();}
    internal static PickupAssets Load()=>new(Resource(ResourceName),Resource(ExtensionName));
    internal Frame[]? For(ObjectAppearance appearance,int direction,int weapon){
        if(direction is <0 or >7||!profiles.TryGetValue((appearance.Gfx,weapon),out var p)||appearance.Sprite!=p.Sprite||PlayerProfiles.Get(appearance.Gfx,weapon) is not {} profile||profile.Path!=p.Path||!profile.Sha256.Equals(p.Sha,StringComparison.OrdinalIgnoreCase))return null;
        var body=bodies[p.Group];var keys=body.Keys[direction];
        if(!appearance.Frames.TryGetValue(keys[0],out var baseFrame))return null;
        return [baseFrame,body.Frames[keys[1]],body.Frames[keys[2]],body.Frames[keys[3]]];
    }
}
