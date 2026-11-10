namespace P06;

// R10-A（T382／#888，FU10§1）：釣魚取得清單用的 desc id 表（TW16 bss `unk_1542864`，vector<int>）。
// 已驗證（FU10§1.1–§1.5，TW16 BIN e12949cf…41a2）：
//   - 唯一寫入點 sub_E65030（VA 0xE65030／FO 0xA64430）＝push_back；呼叫者只有背包加入路徑：
//     S5 S_InvList（sub_92D360，0x92D74F）與 S15 S_AddItem（sub_92D8B0 0x92DEE3、sub_518CD0 0x51902F）。
//   - 寫入條件：封包尾段（1 byte 長度 n＋n byte，sub_926B10／sub_926A70 原樣 memcpy 進 24 byte 延伸區塊，其餘為 0）
//     的偏移 +10 64 位元旗標 & 8（sub_518C80，遮罩 {8,0}：.rdata FO 0xD81F20／0xD4F7C8）。成立 → 物品 desc id（objid 後的 H，
//     sub_C021B0＝item+0x26）以 movsx 符號延伸成 int 加進表。不排序、不去重；查詢 sub_E65420 只做線性「有沒有」。
//   - 清空：只有世界重設（sub_8C32B0 → sub_E65280 → sub_E65320）；結束釣魚不清。→ 這裡用 [WorldReset]。
//   - 3.81 的 S_InvList／S_AddItem 尾段固定 writeC(10)＋10 byte 0：+10 不在範圍內 → 旗標永遠是 0 → 表永遠是空的。
// 延伸區塊只有 24 byte；n 超過 24 時原版的寫法沒查（不在 3.81 範圍），這裡只用前 24 byte 判斷旗標。
// 引擎差異（標明）：n < 10 時 Inventory.cs 仍照舊讀 10 byte（既有欄位語意不變；3.81 不會送），旗標只看前 n byte。
public static class FishObtainTable {
    /// <summary>延伸區塊大小（sub_88ADE0 清 0 的範圍）。</summary>
    public const int BlockSize=24;
    /// <summary>旗標所在偏移（延伸區塊 +10 的 64 位元值）與遮罩低 32 位（asc_1183320＝{8,0}）。</summary>
    public const int FlagOffset=10,FlagMask=8;

    /// <summary>sub_518C80：尾段內容（不含長度 byte；只看前 length byte、最多 24 byte）的 +10 旗標 &amp; 8。</summary>
    public static bool Flag(int length,ReadOnlySpan<byte> content){
        int n=Math.Min(Math.Min(length,content.Length),BlockSize);
        return n>FlagOffset&&(content[FlagOffset]&FlagMask)!=0;
    }
    /// <summary>表裡存的值：16 位元 desc id 的符號延伸（movsx）。</summary>
    public static int Entry(ushort descriptionId)=>(short)descriptionId;
    /// <summary>sub_E65420：線性搜尋，有就 true。</summary>
    public static bool Contains(IReadOnlyList<int>? table,ushort descriptionId){
        if(table==null)return false;
        int v=Entry(descriptionId);
        for(int i=0;i<table.Count;i++)if(table[i]==v)return true;
        return false;
    }

    static int[] latest=[];
    /// <summary>最近一次變動後的表（給原生 FishObtainUI 讀；產品只有一個連線中的世界）。</summary>
    public static IReadOnlyList<int> Latest=>Volatile.Read(ref latest);
    /// <summary>表變動（加入或清空）。在收封包的執行緒觸發；處理器只能排隊。</summary>
    public static event Action<IReadOnlyList<int>>? Changed;
    internal static void Publish(int[] table){
        Volatile.Write(ref latest,table);
        if(Changed is not {} handlers)return;
        foreach(Action<IReadOnlyList<int>> h in handlers.GetInvocationList()){
            try{h(table);}catch(Exception ex){NativeUi.UiResourceLog.Write("fish-obtain 表事件失敗："+ex.Message);}
        }
    }
}

public sealed partial class WorldSession {
    readonly object fishObtainGate=new();
    readonly List<int> fishObtainIds=[];
    /// <summary>每次加入或清空 +1。</summary>
    public int FishObtainRevision {get;private set;}
    /// <summary>目前的表（複本，照加入順序，可能重複）。</summary>
    public int[] FishObtainIds {get{lock(fishObtainGate)return fishObtainIds.ToArray();}}
    public bool FishObtainContains(ushort descriptionId){lock(fishObtainGate)return FishObtainTable.Contains(fishObtainIds,descriptionId);}
    /// <summary>sub_E65050 的過濾：背包裡 desc id 在表內的物品（表空 → 空）。</summary>
    public InventoryItem[] FishObtainItems{get{
        int[] table=FishObtainIds;
        return table.Length==0?[]:Inventory.Where(i=>FishObtainTable.Contains(table,i.DescriptionId)).ToArray();
    }}

    /// <summary>sub_E65030：push_back（Inventory.cs 在 S5／S15 解析完、旗標成立時呼叫）。</summary>
    internal void FishObtainAdd(ushort descriptionId){
        int[] snapshot;
        lock(fishObtainGate){fishObtainIds.Add(FishObtainTable.Entry(descriptionId));FishObtainRevision++;snapshot=fishObtainIds.ToArray();}
        FishObtainTable.Publish(snapshot);
    }

    /// <summary>sub_E65320：只在世界重設時清空。</summary>
    [WorldReset]
    void ResetFishObtainTable(){
        lock(fishObtainGate){fishObtainIds.Clear();FishObtainRevision++;}
        FishObtainTable.Publish([]);
    }

    [WorldSnapshot("fishObtain")]
    object? FishObtainSnapshot(){int[] t=FishObtainIds;return new{revision=FishObtainRevision,count=t.Length,ids=t};}
}
