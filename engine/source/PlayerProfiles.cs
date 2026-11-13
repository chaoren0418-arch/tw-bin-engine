using System.Security.Cryptography;
namespace P06;
// T461（#1046）：ServerActionMs／CastReleaseMs／ProjectileReleaseMs 只是準備器寫進 manifest 的紀錄（T470 與基線逐項比用），
// 可以缺；產品的冷卻、送包節流與放出點一律由 ActionTiming 照 R1／R2 從外觀檔的 list.spr ticks 計算，不讀這三欄。
// StepMs 是移動節流的 R1 值（40 × walk ticks）：manifest 先給（登入時不必解外觀檔），Load 時再與外觀的 walk ticks 對照。
// 職業 gfx 集合：產品不做「職業 → gfx」換算，gfx 一律來自伺服器封包（S87／S76）。IsCharacter／ForGfx 看的是已準備的 profile
// （預熱清單決定先解哪些，docs/design/asset-recipe.md 第 5 節），不是職業表；按需準備由 T477／T478 處理。
public sealed record PlayerProfile(int Gfx,int Weapon,string Path,string Sha256,int StepMs,Dictionary<string,int> ServerActionMs,Dictionary<string,int> CastReleaseMs,int? ProjectileReleaseMs);
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
            var profile=new PlayerProfile(p.I("gfx"),p.I("weapon"),p.S("path"),p.S("sha256"),p.I("stepMs"),Recorded(p,"serverActionMs"),Recorded(p,"castReleaseMs"),p.TryGetProperty("projectileReleaseMs",out var release)&&release.ValueKind==System.Text.Json.JsonValueKind.Number?release.GetInt32():null);
            string full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,profile.Path));
            if(!full.StartsWith(root+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||profile.StepMs<=0)throw new InvalidDataException("Incomplete player profile");
            result.Add((profile.Gfx,profile.Weapon),profile);
        }
        return result;
    }
    static Dictionary<string,int> Recorded(System.Text.Json.JsonElement p,string name)=>p.TryGetProperty(name,out var v)&&v.ValueKind==System.Text.Json.JsonValueKind.Object?v.EnumerateObject().ToDictionary(a=>a.Name,a=>a.Value.GetInt32()):[];
    // TW16 sub_A3EF40 (FO 0x63E340): a missing armed action falls back to the stance walk action,
    // then to stance 0. A profile always carries its own walk, so a missing (gfx, legal stance)
    // profile resolves to the same gfx bare-handed. Unknown gfx and illegal stances stay null.
    public static PlayerProfile? Get(int gfx,int weapon)=>profiles.Value.GetValueOrDefault((gfx,weapon))
        ??(weapon!=0&&PolymorphStances.Legal(weapon)?profiles.Value.GetValueOrDefault((gfx,0)):null);
    public static bool IsCharacter(int gfx)=>profiles.Value.Keys.Any(k=>k.Item1==gfx);
    public static PlayerProfile? ForGfx(int gfx)=>profiles.Value.Values.Where(p=>p.Gfx==gfx).OrderBy(p=>p.Weapon).FirstOrDefault();
    public static ObjectAppearance Load(string root,PlayerProfile profile){
        string path=System.IO.Path.Combine(root,profile.Path);using(var stream=File.OpenRead(path))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(profile.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Player appearance integrity");
        var appearance=new ObjectAppearance(path);
        if(appearance.Gfx!=profile.Gfx||appearance.StepMs!=profile.StepMs||new[]{"walk","breath","attack","damage","die","spellDir","spellNoDir"}.Any(action=>Enumerable.Range(0,8).Any(d=>!appearance.Supports(action,d))))throw new InvalidDataException("Incomplete player appearance");
        // T461：stepMs 必須等於 R1（40 × walk 方向 0 的 list.spr ticks 總和）。
        if(ActionTiming.ServerMs(appearance.Sequences[("walk",0)].Select(s=>new ActionStep(s.Ticks,"")).ToArray())!=profile.StepMs)throw new InvalidDataException("Player stepMs differs from list.spr walk ticks");
        return appearance;
    }
}
