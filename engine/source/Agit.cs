namespace P06;

// M7-D（T206／#512）：盟屋清單與地圖，只做協定與資料。
// 依據：docs/reference/server-protocol.md S_AuctionBoard（S156 HOUSELIST）、S_HouseMap（S187 HOUSEMAP）；
//       docs/reference/windows-2.md 第 5.2 節（原版 op 0 `sub_92E550`、op 18 `sub_92E710`）。
// 視窗（XML `AgitAuctionUI` 本機缺資源；舊式 `AgitMapWindow` 需原生 UI 畫布 T190）需先查證，本檔不做畫面；
// 由後續任務（W2-C，新檔 NativeUi/AgitMapPanel.cs）訂閱 AgitHouseListReceived／AgitHouseMapReceived 開窗。

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
        return true;
    }

    [WorldReset]
    void ResetAgit(){lock(agitGate){agitHouseList=null;agitHouseMap=null;agitHouseListCount=0;agitHouseMapCount=0;}}

    [WorldSnapshot("agit")]
    object? AgitSnapshot(){
        lock(agitGate)return new{
            houseListPackets=agitHouseListCount,
            houseMapPackets=agitHouseMapCount,
            houseList=agitHouseList is {} l?new{boardId=l.BoardId,houses=l.Houses.Select(h=>new{houseId=h.HouseId,name=h.Name,area=h.Area,month=h.Month,day=h.Day,price=h.Price}).ToArray()}:null,
            houseMap=agitHouseMap is {} m?new{objectId=m.ObjectId,houseNumber=m.HouseNumber,region=m.Region,number=m.Number,inOriginalRange=m.InOriginalRange}:null,
            ui="data only: AgitAuctionUI XML missing; AgitMapWindow needs native UI (T190)",
        };
    }
}
