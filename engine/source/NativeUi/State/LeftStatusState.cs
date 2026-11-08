using System.Globalization;
namespace P06.NativeUi;

// ui-left-status (Issue #464, docs/design/native-ui.md §5.3): read-only adapter from existing models to LEFT_WINDOW.
// Field inventory (IUiGameState only had Level/HP/MP; everything else is adapted here, no new packet parsing):
//   Level      ← OwnStatus.Level (S_OwnCharStatus / S113 EXP update)        已有
//   ExpPercent ← ExperienceState.Percent (OriginalExpTable; null when the 32-bit wire value cannot be placed) 已有
//   Ac         ← OwnStatus.Ac                                                已有
//   Mr, Sp     ← WorldSession.SpMr (S37)                                     已有；未收到 S37 → 空白
//   Lawful     ← OwnStatus.Lawful                                            已有
//   Weight     ← OwnStatus.Weight（原服 0–240 刻度，calcWeight240）           已有
//   Food       ← OwnStatus.Food                                              已有
//   Adena      ← Inventory 中 $4 金幣（DescriptionId 0、IconId 5）的數量總和；背包未載入 → 空白
// Anything the server did not send stays null and is drawn blank — never estimated (§5.1).
public enum UiAlignment { Chaotic, Lawful, Neutral }
public enum UiGaugeBand { Low, Mid, High }

public sealed record LeftStatusState(int? Level,decimal? ExpPercent,int? Ac,int? Mr,int? Lawful,int? Sp,int? Weight,int? Food,long? Adena) {
    public static readonly LeftStatusState Empty=new(null,null,null,null,null,null,null,null,null);

    /// <summary>Server weight scale (calcWeight240): 240 = 100 %. 已驗證（伺服器程式）。</summary>
    public const int WeightScale=240;
    /// <summary>
    /// Food scale: a full character reports 225 (P16 evidence, L1J max). 推測；the BIN's own scale is 待驗證.
    /// </summary>
    public const int FoodScale=225;
    /// <summary>Alignment icon thresholds mirror the name colour rule sub_A406C0 (≥500 lawful, &lt;0 chaotic). Icon rule 待驗證.</summary>
    public const int LawfulThreshold=500;
    /// <summary>
    /// Weight band thresholds on the 0–240 scale. 120 (50 %) is the server's regen-stop rule; 240 (100 %) is full.
    /// Which values the BIN uses to switch LOW/MID/HIGH_GAUGE_BAR is 待驗證 — change only here.
    /// </summary>
    public const int WeightMid=120,WeightHigh=240;
    /// <summary>Food bands: FOOD_LOW_GAUGE_BAR below FoodLow (server regen rule Food&lt;3), FOOD_MAX_BAR at FoodScale. 待驗證.</summary>
    public const int FoodLow=3;

    public static UiAlignment AlignmentOf(int lawful)=>lawful>=LawfulThreshold?UiAlignment.Lawful:lawful<0?UiAlignment.Chaotic:UiAlignment.Neutral;
    public static UiGaugeBand WeightBandOf(int weight)=>weight>=WeightHigh?UiGaugeBand.High:weight>=WeightMid?UiGaugeBand.Mid:UiGaugeBand.Low;
    /// <summary>Low = FOOD_LOW_GAUGE_BAR, Mid = FOOD_GUAGE_BAR (normal), High = FOOD_MAX_BAR.</summary>
    public static UiGaugeBand FoodBandOf(int food)=>food>=FoodScale?UiGaugeBand.High:food<FoodLow?UiGaugeBand.Low:UiGaugeBand.Mid;

    public UiAlignment? Alignment=>Lawful is int l?AlignmentOf(l):null;
    public UiGaugeBand? WeightBand=>Weight is int w?WeightBandOf(w):null;
    public UiGaugeBand? FoodBand=>Food is int f?FoodBandOf(f):null;

    /// <summary>Filled width of a gauge surface: width × value / scale, clamped to [0, width]. Value null → 0 (nothing drawn).</summary>
    public static int Fill(int width,decimal? value,decimal scale){
        if(value is not decimal v||scale<=0||width<=0)return 0;
        return (int)Math.Clamp(Math.Floor(width*v/scale),0,width);
    }

    // Text formats (推測 until compared with an original screenshot). Empty string = blank.
    static string I(long v)=>v.ToString(CultureInfo.InvariantCulture);
    static string Percent(int value,int scale)=>I(Math.Clamp((long)value*100/scale,0,100))+"%";
    public string LevelText=>Level is int v?I(v):"";
    /// <summary>Two decimals, truncated (never rounds 99.999 up to 100.00).</summary>
    public string ExpText=>ExpPercent is decimal p?(Math.Floor(p*100)/100).ToString("0.00",CultureInfo.InvariantCulture)+"%":"";
    public string AcText=>Ac is int v?I(v):"";
    public string MrText=>Mr is int v?I(v)+"%":"";
    public string LawfulText=>Lawful is int v?I(v):"";
    public string SpText=>Sp is int v?I(v):"";
    public string WeightText=>Weight is int v?Percent(v,WeightScale):"";
    public string FoodText=>Food is int v?Percent(v,FoodScale):"";
    public string AdenaText=>Adena is long v?I(v):"";

    public static bool IsAdena(InventoryItem i)=>i.DescriptionId==0&&i.IconId==5&&(i.Name=="$4"||i.Name.StartsWith("$4 (",StringComparison.Ordinal));

    /// <summary>Snapshot of an in-world session. Missing pieces stay null.</summary>
    public static LeftStatusState From(WorldSession w){
        var s=w.Status;var xp=w.Experience;var spMr=w.SpMr;
        long? adena=w.InventoryLoaded?w.Inventory.Where(IsAdena).Aggregate(0L,(n,i)=>n+i.Count):null;
        return new(s?.Level,xp is {} e&&s!=null&&e.Level==s.Level?e.Percent:null,s?.Ac,spMr?.Mr,s?.Lawful,spMr?.Sp,s?.Weight,s?.Food,adena);
    }
    /// <summary>Fallback when the state carries no left-status adapter: only the common Level is known.</summary>
    /// Bridge order: UiHpMpGameState (#465) → UiMacroGameState (#466) → UiLeftStatusGameState, so look through both wrappers.
    public static LeftStatusState From(IUiGameState state){
        IUiGameState? s=state;
        for(int depth=0;s!=null&&depth<8;depth++){
            if(s is ILeftStatusSource src)return src.LeftStatus;
            s=s switch{UiHpMpGameState h=>h.Base,UiMacroGameState m=>m.Inner,_=>null};
        }
        return Empty with{Level=state.Level};
    }
}

/// <summary>A game state that also carries the LEFT_WINDOW snapshot.</summary>
public interface ILeftStatusSource { LeftStatusState LeftStatus { get; } }

/// <summary>Decorator: the shared IUiGameState plus the LEFT_WINDOW snapshot (built by the client bridge once per frame).</summary>
public sealed class UiLeftStatusGameState(IUiGameState inner,LeftStatusState left):IUiGameState,ILeftStatusSource {
    public LeftStatusState LeftStatus=>left;
    /// <summary>Wrapped state (#471: the party state sits inside; PartyStates.StateOf unwraps through here).</summary>
    public IUiGameState Inner=>inner;
    public bool InWorld=>inner.InWorld;
    public string? CharacterName=>inner.CharacterName;
    public int? Level=>inner.Level;
    public long? Hp=>inner.Hp;
    public long? MaxHp=>inner.MaxHp;
    public long? Mp=>inner.Mp;
    public long? MaxMp=>inner.MaxMp;
    public long Now=>inner.Now;
}
