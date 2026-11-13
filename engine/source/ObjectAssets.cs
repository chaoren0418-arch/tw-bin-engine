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
    /// <summary>T478 (#1074): true when the appearance was built at runtime from TW_CLIENT_ROOT (on demand), not loaded from the prepared tree.</summary>
    public bool Runtime {get;private set;}
    public ObjectAppearance(string path):this(Json.Read(path)){}
    /// <summary>T478: an appearance file a recipe produced in memory (runtime on-demand path); read and checked exactly like a file.</summary>
    public static ObjectAppearance FromRuntimeJson(byte[] json){
        using var doc=System.Text.Json.JsonDocument.Parse(json);
        return new(doc.RootElement){Runtime=true};
    }
    ObjectAppearance(System.Text.Json.JsonElement j){
        int effectTileType=j.TryGetProperty("effectTileType",out var blend)?blend.GetInt32():2;if(effectTileType is not (2 or 6 or 18 or 34))throw new InvalidDataException("Unsupported effect blend");Gfx=j.I("gfx");Sprite=j.I("sprite");StepMs=j.I("stepMs");
        if(j.TryGetProperty("actionMs",out var durations))foreach(var a in durations.EnumerateObject())ActionMs[a.Name]=a.Value.GetDouble();
        foreach(var f in j.GetProperty("frames").EnumerateArray())Frames.Add(f.S("key"),new(f.S("key"),f.A("bounds"),f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new(effectTileType,b.GetProperty("pixels").EnumerateArray().Select(p=>{var v=p.EnumerateArray().Select(x=>x.GetInt32()).ToArray();return new Pixel(v[0],v[1],(ushort)v[2]);}).ToArray()))).ToArray()));
        foreach(var s in j.GetProperty("sequences").EnumerateArray())Sequences.Add((s.S("action"),s.I("direction")),s.GetProperty("sequence").EnumerateArray().Select(t=>new AnimationStep(t.S("key"),t.I("ticks"),t.TryGetProperty("milliseconds",out var ms)?ms.GetInt32():null,t.TryGetProperty("events",out var events)?events.GetString()??"":"")).ToArray());
        Validate();
    }
    void Validate(){
        if(StepMs<=0||Sequences.Count<8||Sequences.Count%8!=0||Sequences.Values.Any(s=>s.Length==0||s.Sum(t=>t.Ticks)<=0||s.Any(t=>t.Ticks<0||!Frames.ContainsKey(t.Key))))throw new InvalidDataException("object animation cache invalid");
    }
    ObjectAppearance(int gfx,int sprite,int stepMs){Gfx=gfx;Sprite=sprite;StepMs=stepMs;}
    /// <summary>
    /// T477 (#1073): the appearance of a monster / NPC built in memory from the objects recipe's creature model — no JSON in
    /// between. Same values as loading objects/&lt;gfx&gt;.json of the same gfx: blend 2 (creatures have no effectTileType),
    /// actionMs by the monster rule, steps without milliseconds, every frame of every file read (by file number, then frame).
    /// Pixel arrays are shared with the decoded SPR (read-only after decoding). Checked by the same rule as a file.
    /// </summary>
    public static ObjectAppearance FromCreature(AssetPrep.ObjectsRecipe.CreatureModel model){
        const int blend=2;
        var a=new ObjectAppearance(model.Entry.Gfx,model.Entry.Sprite,model.StepMs){Runtime=true};
        foreach(var (label,action) in model.Actions)a.ActionMs[label]=model.ActionMs(label,action);
        foreach(int sid in model.Body.Sids){
            var file=model.Body.File(sid);
            for(int f=0;f<file.Frames.Count;f++){
                string key=AssetPrep.SprJson.Key(sid,f);var frame=file.Frames[f];
                a.Frames.Add(key,new(key,(int[])frame.Bounds.Clone(),frame.Tiles.Select((t,i)=>new Block(t.A,t.B,t.Z,i,new(blend,file.Blocks[t.BlockIndex]))).ToArray()));
            }
        }
        foreach(var s in model.Sequences)a.Sequences.Add((s.Label,s.Direction),s.Keys.Select((key,i)=>{var step=s.Action.Steps[i];return new AnimationStep(key,step.Ticks,null,step.Events);}).ToArray());
        a.Validate();
        return a;
    }
    public Frame At(string action,int direction,double elapsed){var seq=Sequences[(action,direction)];if(seq.All(s=>s.Milliseconds!=null)){double length=seq.Sum(s=>s.Milliseconds!.Value),age=action=="breath"?elapsed%length:Math.Clamp(elapsed,0,length-.00001);foreach(var s in seq){if(age<s.Milliseconds!.Value)return Frames[s.Key];age-=s.Milliseconds.Value;}return Frames[seq[^1].Key];}double total=seq.Sum(s=>s.Ticks);double t=action!="breath"?Math.Clamp(elapsed/Duration(action),0,.999999)*total:(elapsed/(1000d/24))%total;foreach(var s in seq){if(t<s.Ticks)return Frames[s.Key];t-=s.Ticks;}return Frames[seq[^1].Key];}
}
public sealed class ObjectAssets {
    // T463: pickup/* of this assets tree (prepared by the "pickup" recipe), loaded once. A tree without them (for example the
    // r29 baseline) fails the same way every time without re-reading integrity.json on each frame.
    PickupAssets? pickupAssets;System.Runtime.ExceptionServices.ExceptionDispatchInfo? pickupFailure;
    PickupAssets Pickup(){
        if(pickupAssets!=null)return pickupAssets;
        pickupFailure?.Throw();
        try{return pickupAssets=PickupAssets.Load(Path.GetDirectoryName(root)!);}
        catch(Exception ex) when(ex is IOException or InvalidDataException or System.Text.Json.JsonException){pickupFailure=System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);throw;}
    }
    // T478 (#1074): a stance the prepared pickup data does not have, whose body was built on demand, gets its pickup group on
    // demand: it is supported when its list.spz entry has a usable action 15 (PickupRecipe.RuntimeGroup), not by a list.
    // While that group is pending the frames are null (the pickup is suppressed like any unready asset); unavailable = unsupported.
    readonly RuntimeAppearances? runtime;
    /// <summary>T478: the on-demand source for shops, doors and pickup groups (null = prepared tree only, as before).</summary>
    public RuntimeAppearances? Runtime=>runtime;
    internal bool PickupSupports(int gfx,int weapon){
        if(Pickup().Supports(gfx,weapon))return true;
        if(runtime==null||Get(gfx,weapon) is not {Runtime:true} body)return false;
        runtime.Pickup(body,weapon);
        return runtime.State(RuntimeAppearances.PickupKey(body,weapon))!=OnDemandState.Unavailable;
    }
    internal Frame[]? PickupFrames(ObjectAppearance appearance,int direction,int weapon=0){
        if(!ReferenceEquals(appearance,Get(appearance.Gfx,weapon)))return null;
        if(Pickup().Supports(appearance.Gfx,weapon))return Pickup().For(appearance,direction,weapon);
        return appearance.Runtime&&Runtime?.Pickup(appearance,weapon) is {} group?PickupAssets.Compose(group,appearance,direction):null;
    }
    readonly OriginalAppearanceCatalog shops,monsters,worldAppearances,doors;
    // T478: shops (action 70) and doors (actions 28 / 29) outside the prepared catalogs are built on demand; null while pending
    // (ShopPending / DoorPending tell the renderer to treat the object like AppearancePending) or unavailable (not drawn, as before).
    public ObjectAppearance? GetDoor(int gfx)=>doors.Get(gfx)??runtime?.Door(gfx);
    public ObjectAppearance? GetShop(int gfx)=>shops.Get(gfx)??runtime?.Shop(gfx);
    public bool DoorPending(int gfx)=>runtime?.Pending(RuntimeAppearances.DoorKey(gfx))==true;
    public bool ShopPending(int gfx)=>runtime?.Pending(RuntimeAppearances.ShopKey(gfx))==true;
    /// <summary>T478: true while a player stance of this gfx (prepared class gfx, stance outside the preheat list) is being built.</summary>
    public bool StancePending(int gfx,int weapon)=>RuntimeAppearances.Active!=null&&PlayerProfiles.IsCharacter(gfx)&&PlayerProfiles.Pending(gfx,weapon==8?0:weapon);
    public string DisplayName(WorldObject o)=>(string.IsNullOrWhiteSpace(o.Name)?$"未命名物件 #{o.ObjectId}":Name(o.Name))+(o.ShopActive?"（個人商店）":"");
    readonly Dictionary<(int,int),ObjectAppearance> players=[];
    ObjectAppearance Player(PlayerProfile profile){var key=(profile.Gfx,profile.Weapon);if(!players.TryGetValue(key,out var a)){a=PlayerProfiles.Load(Path.GetDirectoryName(root)!,profile);players[key]=a;}return a;}
    public ObjectAppearance? Get(int gfx,int weapon){
        if(PlayerProfiles.Get(gfx,weapon==8?0:weapon) is {} profile)return Player(profile);
        if(PlayerProfiles.IsCharacter(gfx))return null;
        return weapon is 0 or 8?Get(gfx):null;
    }
    readonly Dictionary<int,ObjectAppearance> cache=[];readonly string root;readonly HashSet<int> supported;readonly string[] names;
    // T477 (#1073): appearances of gfx outside the prepared tree, decoded on demand from TW_CLIENT_ROOT (memory only).
    readonly ObjectAppearanceOnDemand? onDemand;
    public ObjectAppearanceOnDemand? OnDemand=>onDemand;
    /// <summary>T477: true while the gfx is being decoded on demand. The renderer then treats the object like AppearancePending (not drawn, no 1120 substitute).</summary>
    public bool Pending(int gfx)=>onDemand?.State(gfx)==OnDemandState.Pending;
    public ObjectAssets(string assets,ObjectAppearanceOnDemand? onDemand):this(assets){this.onDemand=onDemand;}
    /// <summary>T478 (#1074): also builds shops, doors and pickup groups outside the prepared tree on demand.</summary>
    public ObjectAssets(string assets,ObjectAppearanceOnDemand? onDemand,RuntimeAppearances? runtime):this(assets,onDemand){this.runtime=runtime;}
    public ObjectAssets(string assets){root=Path.Combine(assets,"objects");supported=Json.Read(Path.Combine(root,"manifest.json")).A("gfx").ToHashSet();names=Json.Read(Path.Combine(root,"names.json")).EnumerateArray().Select(x=>x.GetString()!).ToArray();shops=new(assets,"shops/manifest.json","p24-original-shop-appearance-v1",["shop"]);monsters=new(assets,"encountered-monsters.json","p24-original-encountered-monsters-v1",["walk","breath","attack","damage","die"]);worldAppearances=new(assets,"world-appearances.json","p24-original-world-appearances-v1",["breath"]);doors=new(assets,"doors/manifest.json","p24-original-inn-door-v1",["open","close"]);}
    public ObjectAppearance? Get(int gfx){if(PlayerProfiles.Get(gfx,0) is {} profile)return Player(profile);if(monsters.Get(gfx) is {} monster)return monster;if(worldAppearances.Get(gfx) is {} scenery)return scenery;
        // T477: not prepared → on demand (creature rule only; player gfx stay with the prepared profiles until T478). Null while pending or unavailable.
        if(!supported.Contains(gfx))return onDemand==null||PlayerProfiles.IsCharacter(gfx)?null:onDemand.Request(gfx);if(!cache.TryGetValue(gfx,out var a)){a=new(Path.Combine(root,gfx+".json"));cache[gfx]=a;}return a;}
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
