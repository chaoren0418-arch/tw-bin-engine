using System.Text.Json;
namespace P06;

// T461（#1046）：動作時序改由 list.spr 動作 metadata 計算，不再讀伺服器 spr_action 抄來的數字。
// 規則出處 docs/reference/asset-recipes/action-timing.md（T447）：
//   R1　伺服器動作間隔＝40 × Σticks（walk＝姿勢+0、attack＝姿勢+1、spellDir＝18、spellNoDir＝19）；stepMs 同樣是 40 × Σticks(walk)。
//   R2　放出時間點＝第一個事件含 '!' 的影格「之前」各影格 floor(ticks × 1000 ÷ 24) 的總和；沒有 '!' 時沒有放出點。
// 基線 r29 的 42 個 profile 以此算出 serverActionMs 168／168、stepMs 42／42、castReleaseMs 84／84、projectileReleaseMs 11／11 相符。
// 已知限制 L5（docs/design/asset-recipe.md 第 7 節，定案）：伺服器擋加速用的是它自己的 spr_action；基線以外的姿勢、
// 變身外型或伺服器改過的列沒有對照。照 R1 算出的值使用，真機遇到回退或擋動作時記進 Issue，不在客戶端加表補。
// 「40」不是客戶端資料：是基線自己記下的 framecount × 40（framerate 24）對照；客戶端原生每 tick 是 1000 ÷ 24。
/// <summary>list.spr 一個影格的時序 metadata：ticks（1/24 秒）與事件字串（含 '!' 表示放出點）。</summary>
public readonly record struct ActionStep(int Ticks,string Events);
public static class ActionTiming {
    public const int ServerTickMs=40,NativeTicksPerSecond=24;
    /// <summary>R1：40 × Σticks。ticks 為負或總和不大於 0 時丟 InvalidDataException（不填假值）。</summary>
    public static int ServerMs(IReadOnlyList<ActionStep> steps){
        if(steps.Count==0||steps.Any(s=>s.Ticks<0))throw new InvalidDataException("list.spr action ticks invalid");
        long ticks=steps.Sum(s=>(long)s.Ticks);
        if(ticks<=0||ticks*ServerTickMs>int.MaxValue)throw new InvalidDataException("list.spr action has no duration");
        return (int)(ticks*ServerTickMs);
    }
    /// <summary>單一影格的原生長度 floor(ticks × 1000 ÷ 24)。</summary>
    public static int FrameMs(int ticks)=>ticks>=0?ticks*1000/NativeTicksPerSecond:throw new InvalidDataException("list.spr action ticks invalid");
    /// <summary>R2：第一個含 '!' 的影格之前的 floor 總和；沒有 '!' 時 null。</summary>
    public static int? ReleaseMs(IReadOnlyList<ActionStep> steps){
        int total=0;
        foreach(var s in steps){if(s.Events.Contains('!'))return total;total+=FrameMs(s.Ticks);}
        return null;
    }
    /// <summary>R1 的動作號：walk＝姿勢+0、attack＝姿勢+1、spellDir＝18、spellNoDir＝19。</summary>
    public static int ActionNumber(string name,int weapon)=>name switch{"walk"=>weapon,"attack"=>weapon+1,"spellDir"=>18,"spellNoDir"=>19,_=>throw new ArgumentOutOfRangeException(nameof(name))};
    /// <summary>外觀 JSON（準備器由 list.spz 產生）的 `sequences[]` 中某動作方向 0 的影格；每格必須帶 `ticks`（list.spr 原值）。</summary>
    public static ActionStep[] Steps(JsonElement appearance,string action)=>
        Read(appearance.GetProperty("sequences").EnumerateArray().Single(s=>s.GetProperty("action").GetString()==action&&s.GetProperty("direction").GetInt32()==0).GetProperty("sequence"));
    static ActionStep[] Read(JsonElement sequence)=>sequence.EnumerateArray().Select(step=>new ActionStep(
        step.TryGetProperty("ticks",out var t)&&t.ValueKind==JsonValueKind.Number?t.GetInt32():throw new InvalidDataException("action timing needs list.spr ticks"),
        step.TryGetProperty("events",out var e)&&e.ValueKind==JsonValueKind.String?e.GetString()!:"")).ToArray();
    /// <summary>
    /// 某個外觀檔（players／objects 的 profile JSON）方向 0 各動作的影格 metadata，依完整路徑與 SHA-256 快取（只留 ticks／事件，
    /// 不留像素）。路徑相對於 assets 根目錄；SHA 不符、找不到動作或缺 ticks 時丟 InvalidDataException。
    /// </summary>
    sealed record Cached(string Sha256,Dictionary<string,JsonElement> Sequences);
    static readonly Dictionary<string,Cached> files=new(StringComparer.OrdinalIgnoreCase);
    static readonly object gate=new();
    public static string AssetsRoot=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets"));
    public static ActionStep[] StepsOf(string relative,string sha256,string action){
        Cached file;
        lock(gate){
            string full=Path.GetFullPath(Path.Combine(AssetsRoot,relative));
            if(!files.TryGetValue(full,out file!)||!file.Sha256.Equals(sha256,StringComparison.OrdinalIgnoreCase)){
                var bytes=File.ReadAllBytes(full);
                if(!Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).Equals(sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Action profile integrity");
                using var doc=JsonDocument.Parse(bytes);var sequences=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
                foreach(var s in doc.RootElement.GetProperty("sequences").EnumerateArray())
                    if(s.GetProperty("direction").GetInt32()==0&&!sequences.TryAdd(s.GetProperty("action").GetString()!,s.GetProperty("sequence").Clone()))throw new InvalidDataException("duplicate action sequence");
                file=new(sha256,sequences);files[full]=file;
            }
        }
        return file.Sequences.TryGetValue(action,out var seq)?Read(seq):throw new InvalidDataException($"profile has no {action} sequence");
    }
    /// <summary>R1：某 profile 某動作的伺服器間隔。</summary>
    public static int ServerMs(PlayerProfile profile,string action)=>ServerMs(StepsOf(profile,action));
    /// <summary>R2：施法放出點（spellDir／spellNoDir）。</summary>
    public static int? CastReleaseMs(PlayerProfile profile,string action="spellDir")=>ReleaseMs(StepsOf(profile,action));
    /// <summary>R2：弓的箭矢放出點（動作 21＝20+1）。只有弓的 profile 有；其他姿勢的攻擊雖然也帶 '!'，不是放箭點。</summary>
    public static int? ProjectileReleaseMs(PlayerProfile profile)=>profile.Weapon==20?ReleaseMs(StepsOf(profile,"attack")):null;
    /// <summary>
    /// 某 profile 某動作方向 0 的影格。#1243：按需建出的 profile（T478 的玩家規則、#1243 的寬鬆建置）沒有檔案，影格取自它帶的
    /// 外觀（同一段 list.spz ticks／事件）；沒有那個動作時與檔案一樣丟 InvalidDataException。
    /// </summary>
    public static ActionStep[] StepsOf(PlayerProfile profile,string action)=>profile.Runtime is {} runtime
        ?runtime.Sequences.TryGetValue((action,0),out var seq)?seq.Select(s=>new ActionStep(s.Ticks,s.Events)).ToArray():throw new InvalidDataException($"profile has no {action} sequence")
        :StepsOf(profile.Path,profile.Sha256,action);
    /// <summary>#1243：profile 有沒有某動作（準備好的 profile 一定有七個動作；寬鬆建置只有 gfx 本身有的）。</summary>
    public static bool Has(PlayerProfile profile,string action)=>profile.Runtime is {} runtime?runtime.Supports(action,0):true;
    /// <summary>不經 manifest 的外觀檔（objects/0.json 等）：只讀 sequences 的 ticks，不比 SHA（呼叫端原本的讀法也不比）。</summary>
    public static ActionStep[] StepsOfObject(string relative,string action)=>Steps(Json.Read(Path.Combine(AssetsRoot,relative)),action);}
