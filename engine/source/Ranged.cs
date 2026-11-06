namespace P06;
public static class RangedRules {
    static readonly Lazy<int> release=new(()=>Json.Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets/objects/0-weapon-20.json"))).I("projectileReleaseMs"));
    public static int ReleaseMs=>release.Value;
    public static int? ReleaseFor(int gfx)=>PlayerProfiles.Get(gfx,20)?.ProjectileReleaseMs;
    // Fixed JAR Point.isInScreen: Manhattan radius with asymmetric distance19 edge.
    // This describes the server range for the supported original range=-1 bow.
    public static bool InScreen(int x,int y,int tx,int ty){int d=Math.Abs(tx-x)+Math.Abs(ty-y);if(d>19)return false;if(d<=18)return true;int edge=Math.Abs(tx-(x-18))+Math.Abs(ty-(y-18));return edge>=19&&edge<=52;}
}
public sealed partial class WorldSession {
    public ulong ArrowCount=>Inventory.Where(i=>i.SupportedArrow).Aggregate(0UL,(sum,i)=>sum+i.Count);
    public bool HasAttackAmmunition=>Actor?.Weapon!=20||ArrowCount>0;
}
