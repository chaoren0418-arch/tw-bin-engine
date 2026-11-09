namespace P06;

// W2-G（T238／#560）：血盟倉庫紀錄，只做協定與資料，0 筆時顯示訊息 1889。
// 依據：docs/reference/windows-2.md 第 7 節（原版 S_EVENT 子碼 117：`D(筆數)`＋逐筆 `scsdd`；筆數 0 顯示訊息 1889，否則開
//       `sub_A559D0` 的視窗 `dword_153D218`）；docs/reference/server-protocol.md S_PledgeWarehouseHistory
//       （C(250) C(117) D(n) {S(角色名) C(type) S(物品名) D(數量) D(距今分鐘)}×n），兩邊完全一致。
// 本檔不畫畫面。ClanWarehouseHistoryUi.cs 訂閱 ClanWarehouseHistoryReceived：原生 UI 開著時開原生窗
// NativeUi/ClanWarehouseHistoryPanel.cs（T288／#689，FU4§5 版面），沒開時用 WinForms 暫行清單（R3-C #672）。
// 訊息 1889 的文字：走 ServerMessageText（T228 字串表，讀本機客戶端）；讀不到字串表時為「原服訊息 1889」編號占位。
// T288（#689）依 FU4§5（docs/reference/followups-4.md 第 5 節，T287／#680）：
//   已驗證：類型 0＝存放、1＝領出；sub_A55FE0 每筆畫 3 行字（下一筆前空 1 行）：
//     1. string[1880]「%s將道具 %s」或 string[1881]「%s將金幣 %s」（物品名稱等於 $4 時），參數＝角色名稱、
//        string[1883]「存放至血盟倉庫中。」（類型 0）或 string[1882]「從血盟倉庫領出。」（類型 1）。
//     2. string[1884]「[%s]x(%d)」（物品名稱展開、數量），金幣用 string[1885]「[%d] 金幣」。
//     3. string[1886]「(動作時間已過：%d分)」；分鐘 ≥ 60 用 1887（小時），小時 ≥ 24 用 1888（日）。
//   推測（標明）：類型不是 0 的值都走 1882；小時／日用整數除法（分鐘 / 60、小時 / 24）。

/// <summary>
/// 一筆血盟倉庫紀錄（`scsdd`）。Type 保留原值：0＝存放、1＝領出（已驗證，FU4§5）。ItemName 可能是 `$編號` 名稱代碼，保留原文。
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
            ui="native window sub_A559D0 on HYPER_TEXT_WINDOW (NativeUi/ClanWarehouseHistoryPanel.cs, T288 #689) when native UI is on, otherwise the WinForms list; type 0 = deposit, 1 = withdraw (FU4§5); surf 9183 missing locally (FU5§5), close button not drawn",
        };
    }
}

/// <summary>
/// T288（#689）：sub_A55FE0 的紀錄文字（FU4§5）。字串從執行期 string-c.tbl 讀；讀不到時用本機 string-c.tbl
/// （decoded SHA-256 4ebbc1a7…43e9）1880–1889 的原文。格式是 C 的 %s／%d（不是 sub_933850 的 %0）。
/// </summary>
public static class ClanWarehouseHistoryText {
    public const int ItemLine=1880,AdenaLine=1881,WithdrawText=1882,DepositText=1883,ItemCount=1884,AdenaCount=1885,
        Minutes=1886,Hours=1887,Days=1888,Empty=1889;
    /// <summary>每筆 3 行字＋1 行空白（FU4§5）。</summary>
    public const int TextLinesPerRecord=3,LinesPerRecord=4;
    /// <summary>金幣的物品名稱代碼（sub_A55FE0 比 $4，與 desc 表第 4 行「金幣」相同）。</summary>
    public const string AdenaName="$4";
    static readonly Dictionary<int,string> Fallback=new(){
        [ItemLine]="%s將道具 %s",[AdenaLine]="%s將金幣 %s",[WithdrawText]="從血盟倉庫領出。",[DepositText]="存放至血盟倉庫中。",
        [ItemCount]="[%s]x(%d)",[AdenaCount]="[%d] 金幣",[Minutes]="(動作時間已過：%d分)",[Hours]="(動作時間已過：%d小時)",
        [Days]="(動作時間已過：%d日)",[Empty]="目前沒有血盟倉庫使用的記錄。"};

    /// <summary>字串表第 id 筆（去掉顏色控制）；表裡沒有或空白時用內建原文。</summary>
    public static string Text(int id){
        if(ServerMessageText.Table is {} t&&id>=0&&id<t.Count){string s=t[id];if(s.Trim().Length>0)return s;}
        return Fallback.TryGetValue(id,out var f)?f:"";
    }

    /// <summary>C printf 的子集：%s（字串）、%d（整數）、%%；其他 % 序列照原樣輸出。參數依序取用，不夠時輸出空白。</summary>
    public static string CFormat(string template,params object[] args){
        var sb=new System.Text.StringBuilder(template.Length+16);int next=0;
        for(int i=0;i<template.Length;i++){
            char c=template[i];
            if(c!='%'||i+1>=template.Length){sb.Append(c);continue;}
            char k=template[i+1];
            if(k=='%'){sb.Append('%');i++;continue;}
            if(k is 's' or 'd'){
                object? a=next<args.Length?args[next]:null;next++;i++;
                sb.Append(k=='d'&&a is IFormattable f?f.ToString(null,System.Globalization.CultureInfo.InvariantCulture):a?.ToString());
                continue;
            }
            sb.Append(c);
        }
        return ServerMessageText.DisplayText(sb.ToString());
    }

    public static bool IsAdena(ClanWarehouseRecord r)=>r.ItemName==AdenaName;
    /// <summary>第 1 行。類型 0＝存放（1883），其他＝領出（1882；只有 1 是已驗證，其他值推測）。</summary>
    public static string ActionLine(ClanWarehouseRecord r)=>
        CFormat(Text(IsAdena(r)?AdenaLine:ItemLine),r.CharName,Text(r.Type==0?DepositText:WithdrawText));
    /// <summary>第 2 行。resolveName 把 $編號 展開（產品用客戶端字串表）。</summary>
    public static string ItemLineText(ClanWarehouseRecord r,Func<string,string>? resolveName=null)=>
        IsAdena(r)?CFormat(Text(AdenaCount),r.Count):CFormat(Text(ItemCount),resolveName?.Invoke(r.ItemName)??r.ItemName,r.Count);
    /// <summary>第 3 行。分鐘 &lt; 60 用 1886；否則小時（/60，推測整數除法）&lt; 24 用 1887；否則日（/24）用 1888。</summary>
    public static string TimeLine(int minutes){
        if(minutes<60)return CFormat(Text(Minutes),minutes);
        int hours=minutes/60;
        return hours<24?CFormat(Text(Hours),hours):CFormat(Text(Days),hours/24);
    }
    public static string[] RecordLines(ClanWarehouseRecord r,Func<string,string>? resolveName=null)=>
        [ActionLine(r),ItemLineText(r,resolveName),TimeLine(r.MinutesAgo)];
    /// <summary>整份清單的畫面行：每筆 3 行字＋1 行空白（最後一筆也帶空白行）。</summary>
    public static IReadOnlyList<string> Lines(ClanWarehouseHistory history,Func<string,string>? resolveName=null)=>
        history.Records.SelectMany(r=>RecordLines(r,resolveName).Append("")).ToArray();
    public static string EmptyText=>CFormat(Text(Empty));
}

/// <summary>暫行清單的一列（畫面文字）。</summary>
public sealed record ClanWarehouseHistoryRow(string CharName,string Type,string ItemName,string Count,string When);

/// <summary>
/// R3-C（T279／#672）WinForms 暫行清單（原生 UI 沒開時用）。T288（#689）：類型與時間改用 FU4§5 的原版文字。
/// </summary>
public static class ClanWarehouseHistoryView {
    public const string Title="血盟倉庫紀錄";
    public const string Footnote="原生 UI 沒開時的暫行清單；原生窗（HYPER_TEXT_WINDOW 版面）見 FU4§5。";
    public static readonly string[] Columns=["角色","類型","物品","數量","距今"];
    public static string TypeText(byte type)=>type switch{0=>"存放",1=>"領出",_=>$"類型 {type}"};
    public static string WhenText(int minutes)=>ClanWarehouseHistoryText.TimeLine(minutes);
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
