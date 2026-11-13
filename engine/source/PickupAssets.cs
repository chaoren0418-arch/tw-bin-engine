using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace P06;

// Finite own-only presentation profiles. Body keys are local to a gfx/sprite group.
// T463 (#1048, B15): the two files are no longer embedded in the assembly. The preparer's "pickup" recipe
// (AssetPrep/PickupRecipe.cs) writes them below the local assets tree; they are checked against that tree's integrity.json.
// Which (gfx, weapon) pairs have a pickup profile is data (it follows the players manifest), not a table in code.
// Contract kept: base group gfx 0 / weapon 0 at objects/0.json, eight directions, four steps (metadataIndex 0–3, ticks 1, 1, 4, 4,
// step 0 from the body profile, steps 1–3 embedded), frameOrder [1, 2, 3, 0], composition [step 0, 1, 2, 3].
public sealed class PickupAssets {
    public const string BasePath="pickup/pickup-own-base15-gfx0-weapon0.json";
    public const string ExtensionPath="pickup/pickup-own-base15-profile-extension.json";
    public const string BaseSchema="pickup-own-base15-gfx0-weapon0-v1",ExtensionSchema="pickup-own-base15-profile-extension-v1";
    public const string BaseProfilePath="objects/0.json";
    static readonly Regex sha256=new(@"\A[0-9a-f]{64}\z",RegexOptions.CultureInvariant);
    internal sealed record Body(int Sprite,Dictionary<string,Frame> Frames,string[][] Keys);
    sealed record Profile(int Sprite,string Path,string Sha,int Group);
    readonly Dictionary<int,Body> bodies=[];
    readonly Dictionary<(int,int),Profile> profiles=[];
    /// <summary>True when this pickup data has a profile for the stance (the product shows no pickup animation otherwise).</summary>
    public bool Supports(int gfx,int weapon)=>profiles.ContainsKey((gfx,weapon));
    public int ProfileCount=>profiles.Count;
    public int GroupCount=>bodies.Count;
    static string Hash(JsonElement j,string name,string what){
        string value=j.S(name);if(!sha256.IsMatch(value))throw new InvalidDataException(what);return value;
    }
    static Body ReadBody(JsonElement j){
        Dictionary<string,Frame> frames=[];var keys=new string[8][];
        foreach(var f in j.GetProperty("frames").EnumerateArray()){
            var blocks=f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new Tile(2,b.GetProperty("pixels").EnumerateArray().Select(p=>{
                var v=p.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v.Length!=3||v[0]<0||v[0]>=24||v[1]<0||v[1]>=24||v[2]<0||v[2]>32767)throw new InvalidDataException("Pickup RGB555 pixel");return new Pixel(v[0],v[1],(ushort)v[2]);
            }).ToArray()))).ToArray();
            if(!frames.TryAdd(f.S("key"),new(f.S("key"),f.A("bounds"),blocks)))throw new InvalidDataException("Pickup duplicate frame");
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
    /// <summary>The base file alone (gfx 0 / weapon 0).</summary>
    public PickupAssets(byte[] bytes){
        using var doc=JsonDocument.Parse(bytes);var j=doc.RootElement;
        if(j.S("schema")!=BaseSchema||j.I("gfx")!=0||j.I("weapon")!=0||j.I("sprite")<0||j.S("baseProfilePath")!=BaseProfilePath||!j.A("frameOrder").SequenceEqual(new[]{1,2,3,0}))throw new InvalidDataException("Pickup resource identity");
        var body=ReadBody(j);
        bodies.Add(0,body);profiles.Add((0,0),new(body.Sprite,BaseProfilePath,Hash(j,"baseProfileSha256","Pickup resource identity"),0));
    }
    /// <summary>Base file plus the extension, which names the base by path and SHA-256.</summary>
    public PickupAssets(byte[] bytes,byte[] extension):this(bytes){
        using var doc=JsonDocument.Parse(extension);var j=doc.RootElement;var requires=j.GetProperty("requiresResource");
        if(j.S("schema")!=ExtensionSchema||!j.A("frameOrder").SequenceEqual(new[]{1,2,3,0})||requires.S("path")!=BasePath||requires.S("sha256")!=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant())throw new InvalidDataException("Pickup extension identity");
        foreach(var g in j.GetProperty("groups").EnumerateArray()){
            int gfx=g.I("gfx");var body=ReadBody(g);
            if(gfx<=0||body.Sprite<0||!bodies.TryAdd(gfx,body))throw new InvalidDataException("Pickup group identity");
        }
        foreach(var p in j.GetProperty("profiles").EnumerateArray()){
            int gfx=p.I("gfx"),weapon=p.I("weapon"),group=p.I("sequenceGroup"),sprite=p.I("sprite");
            if(weapon is <0 or >255||group!=gfx||!bodies.TryGetValue(group,out var body)||body.Sprite!=sprite||p.S("bodySource")!=(gfx==0?"r34-base-resource":"extension")
                ||!profiles.TryAdd((gfx,weapon),new(sprite,p.S("baseProfilePath"),Hash(p,"baseProfileSha256","Pickup profile identity"),group)))throw new InvalidDataException("Pickup profile identity");
        }
        if(bodies.Keys.Any(g=>!profiles.Values.Any(p=>p.Group==g)))throw new InvalidDataException("Pickup group without profile");
    }
    /// <summary>One prepared file of an assets tree, checked (size and SHA-256) against that tree's integrity.json.</summary>
    static byte[] Read(string root,JsonElement files,string relative){
        var pin=files.EnumerateArray().Where(f=>f.S("path")==relative).ToArray();
        if(pin.Length!=1)throw new InvalidDataException("Pickup assets not prepared: "+relative);
        byte[] bytes=File.ReadAllBytes(Path.Combine(root,relative));
        if(bytes.Length!=pin[0].GetProperty("size").GetInt64()||!Convert.ToHexString(SHA256.HashData(bytes)).Equals(pin[0].S("sha256"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Pickup resource integrity: "+relative);
        return bytes;
    }
    /// <summary>Loads pickup/* of an assets tree (the directory holding integrity.json).</summary>
    public static PickupAssets Load(string assetsRoot){
        string root=Path.GetFullPath(assetsRoot);
        try{
            var files=Json.Read(Path.Combine(root,"integrity.json")).GetProperty("files");
            return new(Read(root,files,BasePath),Read(root,files,ExtensionPath));
        }catch(Exception ex) when(ex is UnauthorizedAccessException or KeyNotFoundException or InvalidOperationException or FormatException){
            throw new InvalidDataException("Pickup assets: "+ex.Message,ex);
        }
    }
    /// <summary>T478 (#1074): one group built at runtime for a stance outside the prepared pickup data (PickupRecipe.RuntimeGroup).</summary>
    public sealed class Group {
        internal readonly Body Data;public readonly int Gfx;
        internal Group(int gfx,Body data){Gfx=gfx;Data=data;}
        public int Sprite=>Data.Sprite;
        public int FrameCount=>Data.Frames.Count;
        /// <summary>The four keys of a direction (step 0 from the body, steps 1–3 embedded).</summary>
        public IReadOnlyList<string> Keys(int direction)=>Data.Keys[direction];
    }
    /// <summary>T478: reads a runtime group with the same checks as an extension group (24 frames, 8 × 4 steps, ticks 1, 1, 4, 4).</summary>
    public static Group ReadGroup(byte[] json){
        using var doc=JsonDocument.Parse(json);var j=doc.RootElement;int gfx=j.I("gfx");
        var body=ReadBody(j);if(gfx<0||body.Sprite<0)throw new InvalidDataException("Pickup group identity");
        return new(gfx,body);
    }
    /// <summary>T478: the four frames of a runtime group for an appearance of the same gfx and sprite (null otherwise).</summary>
    public static Frame[]? Compose(Group group,ObjectAppearance appearance,int direction){
        if(direction is <0 or >7||appearance.Gfx!=group.Gfx||appearance.Sprite!=group.Sprite)return null;
        var keys=group.Data.Keys[direction];
        if(!appearance.Frames.TryGetValue(keys[0],out var baseFrame))return null;
        return [baseFrame,group.Data.Frames[keys[1]],group.Data.Frames[keys[2]],group.Data.Frames[keys[3]]];
    }
    public Frame[]? For(ObjectAppearance appearance,int direction,int weapon)=>Compose(appearance,direction,weapon,()=>PlayerProfiles.Get(appearance.Gfx,weapon));
    /// <summary>The same composition with the player profile given explicitly (For uses the process-wide PlayerProfiles); the offline tests use this.</summary>
    public Frame[]? For(ObjectAppearance appearance,int direction,int weapon,PlayerProfile? profile)=>Compose(appearance,direction,weapon,()=>profile);
    // Composition order: [step 0 from the body profile, embedded steps 1, 2, 3]; the presenter starts at index 1 and ends on 0.
    Frame[]? Compose(ObjectAppearance appearance,int direction,int weapon,Func<PlayerProfile?> player){
        if(direction is <0 or >7||!profiles.TryGetValue((appearance.Gfx,weapon),out var p)||appearance.Sprite!=p.Sprite||player() is not {} profile||profile.Path!=p.Path||!profile.Sha256.Equals(p.Sha,StringComparison.OrdinalIgnoreCase))return null;
        var body=bodies[p.Group];var keys=body.Keys[direction];
        if(!appearance.Frames.TryGetValue(keys[0],out var baseFrame))return null;
        return [baseFrame,body.Frames[keys[1]],body.Frames[keys[2]],body.Frames[keys[3]]];
    }
}
