namespace P06;

// R10-R（T405／#922，FU14§2.2、§2.4）：list.spr 111 stride 的走路計數。
// - 他人：S_MOVE（opcode 10）以自動發現的觀察器接收（WorldExtensions.cs；不改 WorldSession.cs）。觀察器在主分派之前跑，
//   這裡只改 StrideStep／StrideCounter，主分派的 `o with{…}` 會保留這兩個欄位。條件與主分派相同：信封不合、死亡、自己都不動。
//   原版他人走一步：TW16 sub_A47D90 0xA48112–0xA481AA、3.81 sub_4E1870 0x4E1A13–0x4E1A76。
// - 自己：ClientLogin 的 MovePlanned（每送一步）呼叫 AdvanceOwnStride（3.81 sub_4E2150 0x4E2B95–0x4E2BEF）。
// - 沒有 stride 的 gfx 原版不動計數（++ 在 stride 分支裡）。
public sealed partial class WorldSession {
    /// <summary>gfx → list.spr 欄位。預設唯讀讀原客戶端（ListSprRuntime.Shared）；離線測試換成合成資料。</summary>
    internal Func<int,ListSprFields?> ListSprFieldsOf {get;set;}=gfx=>ListSprRuntime.Shared.Fields(gfx);
    int ownStrideCounter,ownStrideStep;
    /// <summary>自己的 +532（前進後）與目前這一步用的值。</summary>
    public int OwnStrideCounter=>ownStrideCounter;
    public int OwnStrideStep=>ownStrideStep;

    [WorldReceive(10,Observe=true)]
    void ObserveStrideMove(byte[] plain){
        uint id;byte h,tail;
        try{var r=new LoginReader(plain);r.C();id=r.D();r.H();r.H();h=r.C();tail=r.C();}
        catch(ProtocolException){return;}
        if(h>7||tail!=128||id==0||Actor?.ObjectId==id||!objects.TryGetValue(id,out var o)||o.Dead)return;
        if(ListSprLayers.StrideAdvance(o.StrideCounter,ListSprFieldsOf(o.Gfx)) is not {} s)return;
        objects[id]=o with{StrideStep=s.Step,StrideCounter=s.Next};
    }
    /// <summary>自己每送一步前進一次（沒有 stride 的 gfx 不動）。</summary>
    internal void AdvanceOwnStride(int gfx){
        if(ListSprLayers.StrideAdvance(ownStrideCounter,ListSprFieldsOf(gfx)) is {} s){ownStrideStep=s.Step;ownStrideCounter=s.Next;}
    }
    /// <summary>自己這一步的 stride 動作號；沒有 stride 或目前姿勢＝8 時 null。</summary>
    internal int? OwnStridePose(int gfx,int weapon)=>ListSprLayers.StridePose(ListSprFieldsOf(gfx),ownStrideStep,weapon);

    [WorldReset]
    void ResetOwnStride(){ownStrideCounter=0;ownStrideStep=0;}

    [WorldSnapshot("stride")]
    object? StrideSnapshot()=>new{own=new{counter=ownStrideCounter,step=ownStrideStep},others=objects.Values.Where(o=>o.StrideCounter!=0||o.StrideStep!=0).OrderBy(o=>o.ObjectId).Select(o=>new{id=o.ObjectId,counter=o.StrideCounter,step=o.StrideStep}).ToArray()};
}
