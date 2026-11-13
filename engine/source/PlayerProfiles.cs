using System.Security.Cryptography;
namespace P06;
// T461（#1046）：ServerActionMs／CastReleaseMs／ProjectileReleaseMs 只是準備器寫進 manifest 的紀錄（T470 與基線逐項比用），
// 可以缺；產品的冷卻、送包節流與放出點一律由 ActionTiming 照 R1／R2 從外觀檔的 list.spr ticks 計算，不讀這三欄。
// StepMs 是移動節流的 R1 值（40 × walk ticks）：manifest 先給（登入時不必解外觀檔），Load 時再與外觀的 walk ticks 對照。
// 職業 gfx 集合：產品不做「職業 → gfx」換算，gfx 一律來自伺服器封包（S87／S76）。IsCharacter／ForGfx 看的是已準備的 profile
// （預熱清單決定先解哪些，docs/design/asset-recipe.md 第 5 節），不是職業表；按需準備由 T477／T478 處理。
public sealed record PlayerProfile(int Gfx,int Weapon,string Path,string Sha256,int StepMs,Dictionary<string,int> ServerActionMs,Dictionary<string,int> CastReleaseMs,int? ProjectileReleaseMs){
    /// <summary>T478 (#1074): the appearance of a profile built on demand (memory only; Path is then the recipe's virtual path).</summary>
    public ObjectAppearance? Runtime {get;init;}
}
public static class PlayerProfiles {
    // TW16 sub_A3EF40 第 3 點（AN§3.4）：他人的 sprite 檔不存在時改畫 gfx 1120、動作 0。只用於他人
    // （ObjectRendering 的 ObjectDrawFallback）；自己沒有這條退回，仍由 AppearanceNotice 告知。
    public const int MissingSpriteGfx=1120;
    static readonly Lazy<Dictionary<(int,int),PlayerProfile>> profiles=new(()=>LoadManifest(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../assets"))));
    /// <summary>players/manifest.json of an assets tree, checked against that tree's integrity.json (T455: the preparer's tests load prepared trees with this same reader).</summary>
    public static Dictionary<(int,int),PlayerProfile> LoadManifest(string root){
        root=System.IO.Path.GetFullPath(root);const string relative="players/manifest.json";
        var pin=Json.Read(System.IO.Path.Combine(root,"integrity.json")).GetProperty("files").EnumerateArray().Single(f=>f.S("path")==relative);
        string path=System.IO.Path.Combine(root,relative);using(var stream=File.OpenRead(path)){if(stream.Length!=pin.GetProperty("size").GetInt64()||!Convert.ToHexString(SHA256.HashData(stream)).Equals(pin.S("sha256"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Player profile manifest integrity");}
        var manifest=Json.Read(path);if(manifest.S("schema")!="p24-player-profiles-v1")throw new InvalidDataException("Player profile schema");var result=new Dictionary<(int,int),PlayerProfile>();
        foreach(var p in manifest.GetProperty("profiles").EnumerateArray()){
            var profile=Row(p);
            string full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,profile.Path));
            if(!full.StartsWith(root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||profile.StepMs<=0)throw new InvalidDataException("Incomplete player profile");
            result.Add((profile.Gfx,profile.Weapon),profile);
        }
        return result;
    }
    /// <summary>One profile row of players/manifest.json (T478: also the row the players recipe records for an on-demand stance).</summary>
    public static PlayerProfile Row(System.Text.Json.JsonElement p)=>
        new(p.I("gfx"),p.I("weapon"),p.S("path"),p.S("sha256"),p.I("stepMs"),Recorded(p,"serverActionMs"),Recorded(p,"castReleaseMs"),p.TryGetProperty("projectileReleaseMs",out var release)&&release.ValueKind==System.Text.Json.JsonValueKind.Number?release.GetInt32():null);
    static Dictionary<string,int> Recorded(System.Text.Json.JsonElement p,string name)=>p.TryGetProperty(name,out var v)&&v.ValueKind==System.Text.Json.JsonValueKind.Object?v.EnumerateObject().ToDictionary(a=>a.Name,a=>a.Value.GetInt32()):[];
    // TW16 sub_A3EF40 (FO 0x63E340): a missing armed action falls back to the stance walk action,
    // then to stance 0. A profile always carries its own walk, so a missing (gfx, legal stance)
    // profile resolves to the same gfx bare-handed. Unknown gfx and illegal stances stay null.
    //
    // T478 (#1074): stances and gfx outside the prepared profiles are built on demand (RuntimeAppearances.Active, set by the
    // game window; null in offline tests = the prepared tree only, exactly as before). Order for (gfx, stance):
    //   prepared exact → on-demand exact (pending: null and Pending() is true; ready: its profile) → when that stance is
    //   unavailable, the same fallback as before: stance 0, prepared or on demand. Illegal stances stay null, never requested.
    // Which gfx are asked on demand: a gfx with prepared profiles (IsCharacter, a stance outside the preheat list) and the own
    // character's current gfx (ClaimOwn, from S87 / S76 — this covers polymorph forms). Other gfx (monsters, NPCs) are not turned
    // into player profiles: they stay with the creature rule of ObjectAssets (T477). This keeps what IsCharacter separates
    // (action-timing.md A3): "drawn with the player rule — a missing stance does not fall back to the creature appearance".
    static readonly System.Collections.Concurrent.ConcurrentDictionary<int,byte> ownGfx=new();
    /// <summary>T478: marks a gfx the server gave the own character (S87 / S76) as one to build with the player rule on demand.</summary>
    public static void ClaimOwn(int gfx){if(RuntimeAppearances.Active!=null)ownGfx.TryAdd(gfx,0);}
    static bool OnDemandFor(int gfx)=>IsCharacter(gfx)||ownGfx.ContainsKey(gfx);
    public static PlayerProfile? Get(int gfx,int weapon)=>Resolve(profiles.Value,RuntimeAppearances.Active,OnDemandFor,gfx,weapon,out _);
    /// <summary>T478: true while the profile that <see cref="Get"/> would return is still being built on demand.</summary>
    public static bool Pending(int gfx,int weapon){Resolve(profiles.Value,RuntimeAppearances.Active,OnDemandFor,gfx,weapon,out bool pending);return pending;}
    /// <summary>T478: the own character's profile (claims the gfx for on-demand building first).</summary>
    public static PlayerProfile? Own(int gfx,int weapon,out bool pending){ClaimOwn(gfx);return Resolve(profiles.Value,RuntimeAppearances.Active,OnDemandFor,gfx,weapon,out pending);}
    /// <summary>
    /// T478: the resolution rule itself, over a given prepared set (the product passes players/manifest.json; offline tests a
    /// synthetic set, so they never touch the process-wide manifest). <paramref name="onDemandFor"/> says which gfx may be built
    /// with the player rule on demand.
    /// </summary>
    public static PlayerProfile? Resolve(IReadOnlyDictionary<(int,int),PlayerProfile> prepared,RuntimeAppearances? od,Func<int,bool> onDemandFor,int gfx,int weapon,out bool pending){
        pending=false;
        if(prepared.GetValueOrDefault((gfx,weapon)) is {} exact)return exact;
        bool legal=weapon==0||PolymorphStances.Legal(weapon);
        bool onDemand=od!=null&&legal&&onDemandFor(gfx);
        if(onDemand){
            if(od!.Player(gfx,weapon) is {} built)return built.Profile;
            if(od.Pending(RuntimeAppearances.PlayerKey(gfx,weapon))){pending=true;return null;}
        }
        if(weapon==0||!legal)return null;
        if(prepared.GetValueOrDefault((gfx,0)) is {} bare)return bare;
        if(onDemand){
            if(od!.Player(gfx,0) is {} built)return built.Profile;
            pending=od.Pending(RuntimeAppearances.PlayerKey(gfx,0));
        }
        return null;
    }
    /// <summary>A gfx with prepared player profiles (the preheat list). Not a class table; see the T478 note above.</summary>
    public static bool IsCharacter(int gfx)=>profiles.Value.Keys.Any(k=>k.Item1==gfx);
    public static PlayerProfile? ForGfx(int gfx)=>profiles.Value.Values.Where(p=>p.Gfx==gfx).OrderBy(p=>p.Weapon).FirstOrDefault()
        ??(RuntimeAppearances.Active is {} od&&od.State(RuntimeAppearances.PlayerKey(gfx,0))==OnDemandState.Ready?od.Player(gfx,0)?.Profile:null);
    /// <summary>T478: the checks <see cref="Load"/> makes on a prepared file, for an appearance built on demand.</summary>
    public static void Check(ObjectAppearance appearance,PlayerProfile profile){
        if(appearance.Gfx!=profile.Gfx||appearance.StepMs!=profile.StepMs||new[]{"walk","breath","attack","damage","die","spellDir","spellNoDir"}.Any(action=>Enumerable.Range(0,8).Any(d=>!appearance.Supports(action,d))))throw new InvalidDataException("Incomplete player appearance");
        if(ActionTiming.ServerMs(appearance.Sequences[("walk",0)].Select(s=>new ActionStep(s.Ticks,"")).ToArray())!=profile.StepMs)throw new InvalidDataException("Player stepMs differs from list.spr walk ticks");
    }
    public static ObjectAppearance Load(string root,PlayerProfile profile){
        if(profile.Runtime is {} runtime)return runtime;
        string path=System.IO.Path.Combine(root,profile.Path);using(var stream=File.OpenRead(path))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(profile.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Player appearance integrity");
        var appearance=new ObjectAppearance(path);
        if(appearance.Gfx!=profile.Gfx||appearance.StepMs!=profile.StepMs||new[]{"walk","breath","attack","damage","die","spellDir","spellNoDir"}.Any(action=>Enumerable.Range(0,8).Any(d=>!appearance.Supports(action,d))))throw new InvalidDataException("Incomplete player appearance");
        // T461：stepMs 必須等於 R1（40 × walk 方向 0 的 list.spr ticks 總和）。
        if(ActionTiming.ServerMs(appearance.Sequences[("walk",0)].Select(s=>new ActionStep(s.Ticks,"")).ToArray())!=profile.StepMs)throw new InvalidDataException("Player stepMs differs from list.spr walk ticks");
        return appearance;
    }
}
