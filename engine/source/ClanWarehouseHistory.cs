namespace P06;

// W2-G（T238／#560）：血盟倉庫紀錄，只做協定與資料，0 筆時顯示訊息 1889。
// 依據：docs/reference/windows-2.md 第 7 節（原版 S_EVENT 子碼 117：`D(筆數)`＋逐筆 `scsdd`；筆數 0 顯示訊息 1889，否則開
//       `sub_A559D0` 的視窗 `dword_153D218`）；docs/reference/server-protocol.md S_PledgeWarehouseHistory
//       （C(250) C(117) D(n) {S(角色名) C(type) S(物品名) D(數量) D(距今分鐘)}×n），兩邊完全一致。
// 視窗元件（按鈕 surf 9183）等原生 UI（T190），本檔不做畫面；R3-C（#672）由 ClanWarehouseHistoryUi.cs 訂閱
// ClanWarehouseHistoryReceived，先用 WinForms 暫行清單顯示，原生版面等 Q3-6。
// 訊息 1889 的文字：走 ServerMessageText（T228 字串表，讀本機客戶端）；讀不到字串表時為「原服訊息 1889」編號占位。

/// <summary>
/// 一筆血盟倉庫紀錄（`scsdd`）。Type 保留原值：伺服器寫 L1ClanWarehouseHistory.get_type()，
/// 0／1 對應存入／領出是推定（未查證），由畫面任務查證後再定文字。ItemName 可能是 `$編號` 名稱代碼，保留原文。
/// </summary>
public sealed record ClanWarehouseRecord(string CharName,byte Type,string ItemName,int Count,int MinutesAgo);

/// <summary>S250/117 一次收到的整份清單；Empty＝筆數 0（原版顯示訊息 1889，不開窗）。</summary>
public sealed record ClanWarehouseHistory(IReadOnlyList<ClanWarehouseRecord> Records){
    public bool Empty=>Records.Count==0;
}

public static class ClanWarehouseHistoryPackets {
    public const byte Opcode=250;
    public const byte Subtype=117;
    /// <summary>筆數 0 時原版顯示的訊息編號（windows-2.md 第 7 節）。</summary>
    public const ushort EmptyMessageId=1889;
    // 每筆最少 11 byte：S 空字串 1＋C 1＋S 空字串 1＋D 4＋D 4。筆數超過剩餘長度可容納的上限即視為格式錯誤，避免巨大配置。
    const int MinRecordBytes=11;

    /// <summary>解析 S250/117：C(250) C(117) D(n) {S C S D D}×n，8 byte 對齊。</summary>
    public static ClanWarehouseHistory Parse(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=Opcode||r.C()!=Subtype)throw new ProtocolException("not S250/117 clan warehouse history");
        uint n=r.D();
        if(n>(uint)(r.Remaining/MinRecordBytes))throw new ProtocolException($"clan warehouse history count {n} exceeds packet length");
        var list=new List<ClanWarehouseRecord>((int)n);
        for(int i=0;i<n;i++){
            string who=r.S();byte type=r.C();string item=r.S();int count=unchecked((int)r.D());int minutes=unchecked((int)r.D());
            list.Add(new(who,type,item,count,minutes));
        }
        r.Finish();
        return new(list);
    }
}

public sealed partial class WorldSession {
    readonly object clanWarehouseHistoryGate=new();
    ClanWarehouseHistory? clanWarehouseHistory;
    ServerMessageReport? clanWarehouseHistoryNotice;
    int clanWarehouseHistoryCount;

    /// <summary>收到非空清單時觸發（原版此時開窗）；給後續的原生面板使用。0 筆不觸發，改顯示訊息 1889。</summary>
    public event Action<ClanWarehouseHistory>? ClanWarehouseHistoryReceived;

    public ClanWarehouseHistory? ClanWarehouseHistory{get{lock(clanWarehouseHistoryGate)return clanWarehouseHistory;}}
    /// <summary>最近一次 0 筆時的訊息 1889；收到非空清單後清除。</summary>
    public ServerMessageReport? ClanWarehouseHistoryNotice{get{lock(clanWarehouseHistoryGate)return clanWarehouseHistoryNotice;}}

    [WorldReceive(ClanWarehouseHistoryPackets.Opcode,Subtype=ClanWarehouseHistoryPackets.Subtype)]
    bool ReceiveClanWarehouseHistory(byte[] plain){
        var history=ClanWarehouseHistoryPackets.Parse(plain);
        ServerMessageReport? notice=history.Empty?ServerMessageText.Decode(ClanWarehouseHistoryPackets.EmptyMessageId,[]):null;
        lock(clanWarehouseHistoryGate){clanWarehouseHistory=history;clanWarehouseHistoryNotice=notice;clanWarehouseHistoryCount++;}
        if(notice!=null){
            // 只走畫面中央提示（與 S_RedMessage 同一通道），不動 LastServerMessage，避免打斷進行中的血盟／交易流程。
            // 原版 1889 用哪個訊息通道是推定，待真機或 T228 後確認。
            var(text,recognized)=DisplayNoticeText.Format(ClanWarehouseHistoryPackets.EmptyMessageId,[]);
            redNotice=new(ClanWarehouseHistoryPackets.EmptyMessageId,[],text,recognized,Environment.TickCount64);
        }
        else ClanWarehouseHistoryReceived?.Invoke(history);
        return true;
    }

    [WorldReset]
    void ResetClanWarehouseHistory(){lock(clanWarehouseHistoryGate){clanWarehouseHistory=null;clanWarehouseHistoryNotice=null;clanWarehouseHistoryCount=0;}}

    [WorldSnapshot("clanWarehouseHistory")]
    object? ClanWarehouseHistorySnapshot(){
        lock(clanWarehouseHistoryGate)return new{
            packets=clanWarehouseHistoryCount,
            records=clanWarehouseHistory?.Records.Select(x=>new{charName=x.CharName,type=x.Type,itemName=x.ItemName,count=x.Count,minutesAgo=x.MinutesAgo}).ToArray(),
            notice=clanWarehouseHistoryNotice is {} n?new{id=n.Id,text=n.Text,recognized=n.Recognized}:null,
            ui="transitional WinForms list (ClanWarehouseHistoryUi.cs, R3-C #672); native window sub_A559D0 (surf 9183) waits for layout Q3-6; message 1889 text from T228 string table",
        };
    }
}

/// <summary>暫行清單的一列（畫面文字）。</summary>
public sealed record ClanWarehouseHistoryRow(string CharName,string Type,string ItemName,string Count,string When);

/// <summary>
/// R3-C（T279／#672）：把 S250/117 的紀錄轉成暫行 WinForms 清單的文字。原生窗 sub_A559D0 的版面等 Q3-6。
/// 只顯示原服送來的值：類型 0／1 是存入還是領出未查證，照原值顯示；分鐘數不換算成時／日（原版的換算未查證）。
/// </summary>
public static class ClanWarehouseHistoryView {
    public const string Title="血盟倉庫紀錄";
    public const string Footnote="暫行清單：原版視窗版面待查證（Q3-6）；類型 0／1 的意義待查證，照原服數值顯示。";
    public static readonly string[] Columns=["角色","類型","物品","數量","距今"];
    public static string TypeText(byte type)=>$"類型 {type}";
    public static string WhenText(int minutes)=>$"{minutes} 分鐘前";
    /// <summary>resolveName：物品名的 $編號 名稱代碼轉文字（產品用客戶端字串表；轉不了時原文保留）。</summary>
    public static IReadOnlyList<ClanWarehouseHistoryRow> Rows(ClanWarehouseHistory history,Func<string,string>? resolveName=null)=>
        history.Records.Select(r=>new ClanWarehouseHistoryRow(r.CharName,TypeText(r.Type),resolveName?.Invoke(r.ItemName)??r.ItemName,
            r.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),WhenText(r.MinutesAgo))).ToArray();
}

/// <summary>
/// 訂閱 ClanWarehouseHistoryReceived（在網路執行緒觸發），只留最新一份；UI 執行緒的 tick 用 Take() 取走。
/// 0 筆不會進來（WorldSession 改顯示訊息 1889）。
/// </summary>
public sealed class ClanWarehouseHistoryInbox:IDisposable {
    readonly WorldSession world;ClanWarehouseHistory? pending;int received;
    public ClanWarehouseHistoryInbox(WorldSession world){this.world=world;world.ClanWarehouseHistoryReceived+=OnReceived;}
    void OnReceived(ClanWarehouseHistory history){Volatile.Write(ref pending,history);Interlocked.Increment(ref received);}
    /// <summary>收到的非空清單總數（含已被取代的）。</summary>
    public int Received=>Volatile.Read(ref received);
    public ClanWarehouseHistory? Take()=>Interlocked.Exchange(ref pending,null);
    /// <summary>丟掉還沒顯示的清單（離開世界時）。</summary>
    public void Clear()=>Interlocked.Exchange(ref pending,null);
    public void Dispose()=>world.ClanWarehouseHistoryReceived-=OnReceived;
}
