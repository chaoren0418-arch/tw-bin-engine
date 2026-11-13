namespace P06;
public static class RangedRules {
    // T461：箭矢放出點照 R2（弓的攻擊＝動作 21，第一個 '!' 之前的 floor(ticks×1000/24) 總和），由 list.spr ticks 計算，
    // 不讀 0-weapon-20.json 的 projectileReleaseMs 欄位。基線兩者相同（791）。
    static readonly Lazy<int> release=new(()=>ActionTiming.ReleaseMs(ActionTiming.StepsOfObject("objects/0-weapon-20.json","attack"))??throw new InvalidDataException("gfx 0 bow attack has no release marker"));
    public static int ReleaseMs=>release.Value;
    /// <summary>
    /// 沒有 ActionCadence 時的放箭點：gfx 的弓 profile 照 R2 算。沒有弓的 profile（Get 退回空手時也算沒有）、攻擊沒有 '!'、
    /// 或 profile 讀不了時 null（呼叫端照舊以 0 處理）。
    /// </summary>
    public static int? ReleaseFor(int gfx){
        if(PlayerProfiles.Get(gfx,20) is not {Weapon:20} profile)return null;
        try{return ActionTiming.ProjectileReleaseMs(profile);}
        catch(Exception e) when(e is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException){return null;}
    }
    // Fixed JAR Point.isInScreen: Manhattan radius with asymmetric distance19 edge.
    // This describes the server range for the supported original range=-1 bow.
    public static bool InScreen(int x,int y,int tx,int ty){int d=Math.Abs(tx-x)+Math.Abs(ty-y);if(d>19)return false;if(d<=18)return true;int edge=Math.Abs(tx-(x-18))+Math.Abs(ty-(y-18));return edge>=19&&edge<=52;}
}
public sealed partial class WorldSession {
    public ulong ArrowCount=>Inventory.Where(i=>i.SupportedArrow).Aggregate(0UL,(sum,i)=>sum+i.Count);
    public bool HasAttackAmmunition=>Actor?.Weapon!=20||ArrowCount>0;
}
