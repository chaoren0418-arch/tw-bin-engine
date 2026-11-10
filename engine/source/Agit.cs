namespace P06;

// M7-D（T206／#512）：盟屋清單與地圖，只做協定與資料。
// 依據：docs/reference/server-protocol.md S_AuctionBoard（S156 HOUSELIST）、S_HouseMap（S187 HOUSEMAP）；
//       docs/reference/windows-2.md 第 5.2 節（原版 op 0 `sub_92E550`、op 18 `sub_92E710`）。
// 本檔只做協定與資料，不做畫面：
//   S156 的拍賣窗由 NativeUi/AgitAuctionPanel.cs 畫（T323／#761；XML `AgitAuctionUI` 本機沒有，執行期讀參考客戶端，PAK§4），
//   R7-C（#805）在同檔補上選盟屋送 C125 select、AuctionInfo_Window（S39 agsel）與 Bid_Window（S136 agapply）；
//   S187 的地圖窗由 W2-C（T232／#550）的 NativeUi/AgitMapPanel.cs 畫；本檔只多送開窗事件（AgitMapWindow）。

/// <summary>S156 每一筆盟屋：盟屋 id、名稱、面積、拍賣截止月／日、價格（`dshccd`）。</summary>
public sealed record AgitHouseEntry(uint HouseId,string Name,ushort Area,byte Month,byte Day,int Price);

/// <summary>S156 HouseList：拍賣公告板物件 id 與盟屋清單。</summary>
public sealed record AgitHouseList(uint BoardId,IReadOnlyList<AgitHouseEntry> Houses);

/// <summary>
/// S187 HouseMap：物件 id 與盟屋編號。原版把第二個 D 拆成「區域＝高 16 位元」「編號＝低 16 位元」
/// （windows-2.md 5.2：區域 &lt; 9、編號 1–70；推測，依變數配置）。超出範圍不丟例外，只以 InOriginalRange 標示。
/// </summary>
public sealed record AgitHouseMap(uint ObjectId,uint HouseNumber){
    public int Region=>(int)(HouseNumber>>16);
    public int Number=>(int)(HouseNumber&0xFFFF);
    public bool InOriginalRange=>Region<9&&Number is >=1 and <=70;
}

/// <summary>S187 開窗請求：Revision 在行程內單調遞增，同一筆請求被關掉後不會重開，下一個 S187 才重開。</summary>
public sealed record AgitMapOpenRequest(long Revision,AgitHouseMap Map);

/// <summary>
/// W2-C（T232／#550）S187 的開窗事件。原生面板（NativeUi/AgitMapPanel.cs）由 UiRoot 建立、拿不到 WorldSession，
/// 所以由收包處理寫入這裡、面板每格 Update 讀 Current（不訂閱，避免事件洩漏）。WorldSession.Reset 時清掉（窗關閉）。
/// 產品只有一個 WorldSession；離線測試可替面板注入自己的來源。
/// </summary>
public static class AgitMapWindow {
    static readonly object gate=new();
    static long revision;
    static AgitMapOpenRequest? current;
    public static AgitMapOpenRequest? Current{get{lock(gate)return current;}}
    public static AgitMapOpenRequest Request(AgitHouseMap map){lock(gate)return current=new(++revision,map);}
    public static void Clear(){lock(gate)current=null;}
}

public static class AgitPackets {
    public const byte HouseListOpcode=156;
    public const byte HouseMapOpcode=187;

    /// <summary>解析 S156：C(op) D(board) H(count) {D(id) S(name) H(area) C(month) C(day) D(price)}×count，8 byte 對齊。</summary>
    public static AgitHouseList ParseHouseList(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=HouseListOpcode)throw new ProtocolException("not S156 HouseList");
        uint board=r.D();int count=r.H();
        var houses=new List<AgitHouseEntry>(count);
        for(int i=0;i<count;i++){
            uint id=r.D();string name=r.S();ushort area=r.H();byte month=r.C();byte day=r.C();int price=unchecked((int)r.D());
            houses.Add(new(id,name,area,month,day,price));
        }
        r.Finish();
        return new(board,houses);
    }

    /// <summary>解析 S187：C(op) D(objectId) D(houseNumber)，8 byte 對齊。</summary>
    public static AgitHouseMap ParseHouseMap(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=HouseMapOpcode)throw new ProtocolException("not S187 HouseMap");
        uint obj=r.D();uint number=r.D();
        r.Finish();
        return new(obj,number);
    }
}

public sealed partial class WorldSession {
    readonly object agitGate=new();
    AgitHouseList? agitHouseList;
    AgitHouseMap? agitHouseMap;
    int agitHouseListCount,agitHouseMapCount;

    /// <summary>收到 S156 後觸發；給後續的拍賣清單畫面（WinForms／HTML）使用。</summary>
    public event Action<AgitHouseList>? AgitHouseListReceived;
    /// <summary>收到 S187 後觸發；給後續的盟屋地圖窗（W2-C）使用。</summary>
    public event Action<AgitHouseMap>? AgitHouseMapReceived;

    public AgitHouseList? AgitHouseList{get{lock(agitGate)return agitHouseList;}}
    public AgitHouseMap? AgitHouseMap{get{lock(agitGate)return agitHouseMap;}}

    [WorldReceive(AgitPackets.HouseListOpcode)]
    bool ReceiveAgitHouseList(byte[] plain){
        var list=AgitPackets.ParseHouseList(plain);
        lock(agitGate){agitHouseList=list;agitHouseListCount++;}
        AgitHouseListReceived?.Invoke(list);
        return true;
    }

    [WorldReceive(AgitPackets.HouseMapOpcode)]
    bool ReceiveAgitHouseMap(byte[] plain){
        var map=AgitPackets.ParseHouseMap(plain);
        lock(agitGate){agitHouseMap=map;agitHouseMapCount++;}
        AgitHouseMapReceived?.Invoke(map);
        AgitMapWindow.Request(map);   // W2-C（#550）：開窗事件，原生 AgitMapPanel 輪詢
        return true;
    }

    [WorldReset]
    void ResetAgit(){lock(agitGate){agitHouseList=null;agitHouseMap=null;agitHouseListCount=0;agitHouseMapCount=0;}AgitMapWindow.Clear();}

    [WorldSnapshot("agit")]
    object? AgitSnapshot(){
        lock(agitGate)return new{
            houseListPackets=agitHouseListCount,
            houseMapPackets=agitHouseMapCount,
            houseList=agitHouseList is {} l?new{boardId=l.BoardId,houses=l.Houses.Select(h=>new{houseId=h.HouseId,name=h.Name,area=h.Area,month=h.Month,day=h.Day,price=h.Price}).ToArray()}:null,
            houseMap=agitHouseMap is {} m?new{objectId=m.ObjectId,houseNumber=m.HouseNumber,region=m.Region,number=m.Number,inOriginalRange=m.InOriginalRange}:null,
            ui="AgitAuctionUI: native panels Agitmap_Window / AuctionInfo_Window / Bid_Window from the reference client XML (#761, #805; data only with --classic-ui or when the XML is unavailable); AgitMapWindow: native panel AgitMap_Window (#550)",
            mapWindowRevision=AgitMapWindow.Current?.Revision,
        };
    }
}
