using System.Text.RegularExpressions;
namespace P06;
public sealed record AnimationStep(string Key,int Ticks,int? Milliseconds=null,string Events="");
public sealed class ObjectAppearance {
    public int Gfx {get;}
    public int Sprite {get;}
    public int StepMs {get;}
    public Dictionary<string,double> ActionMs {get;}=[];
    public double Duration(string action)=>ActionMs.TryGetValue(action,out double ms)?ms:action=="walk"?StepMs:Sequences[(action,0)].Sum(x=>x.Ticks)*1000d/24;
    public bool Supports(string action,int direction)=>Sequences.ContainsKey((action,direction));
    public Dictionary<string,Frame> Frames {get;}=[];
    public Dictionary<(string,int),AnimationStep[]> Sequences {get;}=[];
    public ObjectAppearance(string path){
        var j=Json.Read(path);int effectTileType=j.TryGetProperty("effectTileType",out var blend)?blend.GetInt32():2;if(effectTileType is not (2 or 6 or 18 or 34))throw new InvalidDataException("Unsupported effect blend");Gfx=j.I("gfx");Sprite=j.I("sprite");StepMs=j.I("stepMs");
        if(j.TryGetProperty("actionMs",out var durations))foreach(var a in durations.EnumerateObject())ActionMs[a.Name]=a.Value.GetDouble();
        foreach(var f in j.GetProperty("frames").EnumerateArray())Frames.Add(f.S("key"),new(f.S("key"),f.A("bounds"),f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new(effectTileType,b.GetProperty("pixels").EnumerateArray().Select(p=>{var v=p.EnumerateArray().Select(x=>x.GetInt32()).ToArray();return new Pixel(v[0],v[1],(ushort)v[2]);}).ToArray()))).ToArray()));
        foreach(var s in j.GetProperty("sequences").EnumerateArray())Sequences.Add((s.S("action"),s.I("direction")),s.GetProperty("sequence").EnumerateArray().Select(t=>new AnimationStep(t.S("key"),t.I("ticks"),t.TryGetProperty("milliseconds",out var ms)?ms.GetInt32():null,t.TryGetProperty("events",out var events)?events.GetString()??"":"")).ToArray());
        if(StepMs<=0||Sequences.Count<8||Sequences.Count%8!=0||Sequences.Values.Any(s=>s.Length==0||s.Sum(t=>t.Ticks)<=0||s.Any(t=>t.Ticks<0||!Frames.ContainsKey(t.Key))))throw new InvalidDataException("object animation cache invalid");
    }
    public Frame At(string action,int direction,double elapsed){var seq=Sequences[(action,direction)];if(seq.All(s=>s.Milliseconds!=null)){double length=seq.Sum(s=>s.Milliseconds!.Value),age=action=="breath"?elapsed%length:Math.Clamp(elapsed,0,length-.00001);foreach(var s in seq){if(age<s.Milliseconds!.Value)return Frames[s.Key];age-=s.Milliseconds.Value;}return Frames[seq[^1].Key];}double total=seq.Sum(s=>s.Ticks);double t=action!="breath"?Math.Clamp(elapsed/Duration(action),0,.999999)*total:(elapsed/(1000d/24))%total;foreach(var s in seq){if(t<s.Ticks)return Frames[s.Key];t-=s.Ticks;}return Frames[seq[^1].Key];}
}
public sealed class ObjectAssets {
    PickupAssets? pickupAssets;
    internal Frame[]? PickupFrames(ObjectAppearance appearance,int direction,int weapon=0)=>
        PickupAssets.Supports(appearance.Gfx,weapon)&&ReferenceEquals(appearance,Get(appearance.Gfx,weapon))
        ?(pickupAssets??=PickupAssets.Load()).For(appearance,direction,weapon):null;
    readonly OriginalAppearanceCatalog shops,monsters,worldAppearances,doors;
    public ObjectAppearance? GetDoor(int gfx)=>gfx==2346?doors.Get(gfx):null;
    public ObjectAppearance? GetShop(int gfx)=>shops.Get(gfx);
    public string DisplayName(WorldObject o)=>(string.IsNullOrWhiteSpace(o.Name)?$"未命名物件 #{o.ObjectId}":Name(o.Name))+(o.ShopActive?"（個人商店）":"");
    readonly Dictionary<(int,int),ObjectAppearance> players=[];
    ObjectAppearance Player(PlayerProfile profile){var key=(profile.Gfx,profile.Weapon);if(!players.TryGetValue(key,out var a)){a=PlayerProfiles.Load(Path.GetDirectoryName(root)!,profile);players[key]=a;}return a;}
    public ObjectAppearance? Get(int gfx,int weapon){
        if(PlayerProfiles.Get(gfx,weapon==8?0:weapon) is {} profile)return Player(profile);
        if(PlayerProfiles.IsCharacter(gfx))return null;
        return weapon is 0 or 8?Get(gfx):null;
    }
    readonly Dictionary<int,ObjectAppearance> cache=[];readonly string root;readonly HashSet<int> supported;readonly string[] names;
    public ObjectAssets(string assets){root=Path.Combine(assets,"objects");supported=Json.Read(Path.Combine(root,"manifest.json")).A("gfx").ToHashSet();names=Json.Read(Path.Combine(root,"names.json")).EnumerateArray().Select(x=>x.GetString()!).ToArray();shops=new(assets,"shops/manifest.json","p24-original-shop-appearance-v1",["shop"]);monsters=new(assets,"encountered-monsters.json","p24-original-encountered-monsters-v1",["walk","breath","attack","damage","die"]);worldAppearances=new(assets,"world-appearances.json","p24-original-world-appearances-v1",["breath"]);doors=new(assets,"doors/manifest.json","p24-original-inn-door-v1",["open","close"]);}
    public ObjectAppearance? Get(int gfx){if(PlayerProfiles.Get(gfx,0) is {} profile)return Player(profile);if(monsters.Get(gfx) is {} monster)return monster;if(worldAppearances.Get(gfx) is {} scenery)return scenery;if(!supported.Contains(gfx))return null;if(!cache.TryGetValue(gfx,out var a)){a=new(Path.Combine(root,gfx+".json"));cache[gfx]=a;}return a;}
    public string Name(string raw)=>Regex.Replace(raw,@"\$(\d+)",m=>int.TryParse(m.Groups[1].Value,out int id)&&id<names.Length?names[id]:m.Value);
}
// Separate action-70 catalog: a private shop is not a weapon-70 player profile.
public sealed class OriginalAppearanceCatalog {
    readonly string root;readonly string[] actions;readonly Dictionary<int,(string Path,string Sha,int Sprite)> profiles=[];readonly Dictionary<int,ObjectAppearance> loaded=[];
    static void Verify(string path,string sha){using var stream=File.OpenRead(path);if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).Equals(sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original appearance integrity: "+path);}
    public OriginalAppearanceCatalog(string assets,string relative,string schema,string[] actions){
        root=Path.GetFullPath(assets);this.actions=actions;var pin=Json.Read(Path.Combine(root,"integrity.json")).GetProperty("files").EnumerateArray().Single(f=>f.S("path")==relative);
        string path=Path.Combine(root,relative);Verify(path,pin.S("sha256"));var manifest=Json.Read(path);if(manifest.S("schema")!=schema)throw new InvalidDataException("Original appearance schema");
        foreach(var p in manifest.GetProperty("profiles").EnumerateArray()){string full=Path.GetFullPath(Path.Combine(root,p.S("path")));if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original appearance path");profiles.Add(p.I("gfx"),(full,p.S("sha256"),p.I("sprite")));}
    }
    public ObjectAppearance? Get(int gfx){if(!profiles.TryGetValue(gfx,out var p))return null;if(loaded.TryGetValue(gfx,out var a))return a;Verify(p.Path,p.Sha);a=new(p.Path);if(a.Gfx!=gfx||a.Sprite!=p.Sprite||actions.Any(action=>Enumerable.Range(0,8).Any(d=>!a.Supports(action,d))))throw new InvalidDataException("Incomplete original appearance");loaded.Add(gfx,a);return a;}
}
