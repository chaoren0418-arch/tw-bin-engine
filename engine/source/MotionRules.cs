namespace P06;
/// <summary>
/// T459（#1044）：motion.json 改成產品規則，不再讀檔。依 T448（#1033，docs/reference/asset-recipes/motion-blend.md）。
/// - 方向：MovementRules.Dx／Dy（方向 0 是世界 y−1，之後順時針每 45° 一格）；BIN 0x01146030 的靜態建構以 (2*dx, dy) 佐證。
/// - 畫面位移：等角投影，一格寬 48、高 24：Sx = 24*(dx+dy)、Sy = 12*(dy-dx)；BIN 0x01143F10 的四分之一步表是它的 1/4。
/// - FrameMs：list.spr #3225 walk 每格 4 tick，floor(4*1000/24) = 166。只用在離線預覽；連線時 World 用 NetworkStepMs/4。
/// - Spawn：離線預覽起點（32588, 32928），是 P06 研究時目視選定的道路邊，<b>不是</b>原客戶端資料。
/// </summary>
public static class MotionRules {
    public const int DirectionCount=8;
    public const int WalkTicksPerFrame=4,TicksPerSecond=24;
    public const int FrameMs=WalkTicksPerFrame*1000/TicksPerSecond;
    public const int TileHalfWidth=24,TileHalfHeight=12;
    public const int SpawnX=32588,SpawnY=32928;
    public static Direction ForHeading(int d){
        if((uint)d>=DirectionCount)throw new ArgumentOutOfRangeException(nameof(d));
        int dx=MovementRules.Dx[d],dy=MovementRules.Dy[d];
        return new(d,dx,dy,TileHalfWidth*(dx+dy),TileHalfHeight*(dy-dx));
    }
    /// <summary>每次回傳新陣列；Assets 各自持有一份。</summary>
    public static Direction[] Directions()=>Enumerable.Range(0,DirectionCount).Select(ForHeading).ToArray();
    public static int[] Spawn()=>[SpawnX,SpawnY];
}
