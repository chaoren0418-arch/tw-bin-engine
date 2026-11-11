using System.Text.Json;
using P06.NativeUi;

// W2-H（T239／#561）：血盟成員窗。依據 docs/reference/windows-2.md 第 3、9 節（T226／#533）。
// 協定（3.81 為準，server-protocol.md S_Pledge／S_PacketBox）：
//   S250 子碼 171：C(250) C(171) H(人數)，逐人 S(姓名) C(0)。TW16 sub_B2E3D0 收完後 vtable +68(1) 開窗 → 本模組以它為開窗點。
//     既有 WorldSession 主分派已把 171 交給 Clan.cs（ReceiveClanOnlineMembers），所以這裡只用「觀察」掛接，不搶封包。
//   S250 子碼 169：C(250) C(169) S(姓名) 62 byte Big5 備註（零填滿）。3.81 C_PledgeContent 寫備註後回送；TW16 sub_B2EE50。
//     主分派沒有任何 case 接 169，所以用消化型掛接。
//   S250 子碼 168：C(250) C(168) S(公告)（3.81 C_PledgeContent 類型 15 寫公告成功後回給本人）。TW16 sub_B2EF40（「RecvPledgeNoti」）
//     讀固定 212 byte 寫進公告欄並設為可見，不開窗；文字以 NUL 結束，以 3.81 的 S 讀顯示相同（推定，packetbox-windows.md 2.10）。
//     主分派沒有任何 case 接 168，所以用消化型掛接（T248／#609，PBW-C）。
//   167／170：3.81 把子碼直接寫成第一個 byte（不是正常 S250 包），不處理（windows-2.md 第 3 節）。
// 版面：本機 PledgeUI.xml（執行期從 Tile.pak 讀取，不進 repo）的 Nonoticeuserinfo_Window／Noticeuserinfo_Window。
//   原版 TW16 讀的是 RePledgeUI.xml（本機缺），3.81 世代資源只有 PledgeUI.xml，所以本窗以它為準（推定）。
// 只做協定、資料與已知座標；標「推測」的常數集中在 PledgePanel 頂端，查證後只改那裡。

namespace P06 {

/// <summary>S250/169 的一筆備註。</summary>
public sealed record PledgeMemo(string Name,string Memo);

/// <summary>
/// 血盟成員窗資料（唯讀快照）。Online=null 表示本次世界還沒收到 171（窗不開）；空陣列表示收到但沒人在線上。
/// OpenRevision 每收到一次 171 就加一，面板以它判斷「要開窗」。
/// Notice=null 表示本次世界還沒收到 168（公告欄留白，不推算）。
/// </summary>
public sealed record PledgeWindowState(long OpenRevision,long Revision,string Clan,IReadOnlyList<string>? Online,IReadOnlyDictionary<string,string> Memos,string? Notice=null) {
    public static readonly PledgeWindowState Empty=new(0,0,"",null,new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase));
    /// <summary>該成員的備註；伺服器沒送過就是空字串（不推算）。</summary>
    public string Memo(string name)=>Memos.TryGetValue(name,out var m)?m:"";
}

public static class PledgeProtocol {
    public const byte PacketBox=250,Announce=168,WriteNotes=169,OnlineMembers=171;
    /// <summary>3.81 S_Pledge(String,String)：備註固定 62 byte，Big5，零填滿。</summary>
    public const int NotesLength=62;

    /// <summary>3.81 S_PacketBox(168, announce)：C(250) C(168) S(公告)＋補齊。</summary>
    public static string DecodeNotice(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=PacketBox||r.C()!=Announce)throw new ProtocolException("pledge notice subcode");
        string text=r.S();
        r.Finish();
        return text;
    }
    /// <summary>3.81 S_PacketBox(168, announce) 的位元組（測試用）。</summary>
    public static byte[] EncodeNotice(string text)=>LoginPackets.Pad([PacketBox,Announce,..LoginPackets.TextEncoding.GetBytes(text),0]);

    public static PledgeMemo DecodeMemo(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=PacketBox||r.C()!=WriteNotes)throw new ProtocolException("pledge memo subcode");
        string name=r.S();
        if(string.IsNullOrWhiteSpace(name))throw new ProtocolException("pledge memo name");
        var bytes=new byte[NotesLength];for(int i=0;i<NotesLength;i++)bytes[i]=r.C();
        r.Finish();
        int end=Array.IndexOf(bytes,(byte)0);
        return new(name,LoginPackets.TextEncoding.GetString(bytes,0,end<0?NotesLength:end));
    }
    /// <summary>3.81 S_Pledge(name, notes) 的位元組（測試用，照伺服器原始碼欄位順序合成）。</summary>
    public static byte[] EncodeMemo(string name,string notes){
        var text=LoginPackets.TextEncoding.GetBytes(notes);
        if(text.Length>NotesLength)throw new ArgumentException("備註超過 62 byte。");
        var fixedText=new byte[NotesLength];text.CopyTo(fixedText,0);
        return LoginPackets.Pad([PacketBox,WriteNotes,..LoginPackets.TextEncoding.GetBytes(name),0,..fixedText]);
    }
    /// <summary>3.81 S_PacketBox(171, L1PcInstance[]) 的位元組（測試用）。</summary>
    public static byte[] EncodeOnline(params string[] names){
        var body=new List<byte>{PacketBox,OnlineMembers,(byte)(names.Length&0xff),(byte)(names.Length>>8)};
        foreach(var n in names){body.AddRange(LoginPackets.TextEncoding.GetBytes(n));body.Add(0);body.Add(0);}
        return LoginPackets.Pad(body.ToArray());
    }
}

public sealed partial class WorldSession {
    long pledgeOpenRevision,pledgeRevision;int pledgeEpoch=-1;
    string[]? pledgeOnline;string pledgeClan="";string? pledgeNotice;
    readonly Dictionary<string,string> pledgeMemos=new(StringComparer.OrdinalIgnoreCase);
    PledgeWindowState? pledgeView;

    void EnsurePledgeEpoch(){
        if(pledgeEpoch==WorldEpoch)return;
        pledgeEpoch=WorldEpoch;ClearPledgeWindow();
    }
    void ClearPledgeWindow(){
        if(pledgeOnline==null&&pledgeMemos.Count==0&&pledgeClan.Length==0&&pledgeNotice==null)return;
        pledgeOnline=null;pledgeClan="";pledgeNotice=null;pledgeMemos.Clear();pledgeRevision++;pledgeView=null;
    }

    /// <summary>171 是開窗點（TW16 sub_B2E3D0 → vtable +68(1)）。壞包交給主分派（Clan.cs）照原樣報錯，這裡不吞也不開窗。</summary>
    [WorldReceive(PledgeProtocol.PacketBox,Subtype=PledgeProtocol.OnlineMembers,Observe=true)]
    void ObservePledgeOnline(byte[] plain){
        string[] names;
        try{names=ClanProtocol.DecodeOnlineMembers(plain);}catch(ProtocolException){return;}
        EnsurePledgeEpoch();
        pledgeOnline=names;pledgeClan=Actor?.Clan??"";
        pledgeOpenRevision++;pledgeRevision++;pledgeView=null;
    }

    /// <summary>169：更新該成員的備註；不開窗。名稱不在目前在線名單時仍保留，之後出現時顯示。</summary>
    [WorldReceive(PledgeProtocol.PacketBox,Subtype=PledgeProtocol.WriteNotes)]
    bool ReceivePledgeMemo(byte[] plain){
        var memo=PledgeProtocol.DecodeMemo(plain);
        EnsurePledgeEpoch();
        pledgeMemos[memo.Name]=memo.Memo;pledgeRevision++;pledgeView=null;
        return true;
    }

    /// <summary>168：更新公告文字；不開窗（TW16 sub_B2EF40 只寫已存在的公告欄）。空字串＝伺服器送來空公告，照樣留白。</summary>
    [WorldReceive(PledgeProtocol.PacketBox,Subtype=PledgeProtocol.Announce)]
    bool ReceivePledgeNotice(byte[] plain){
        var text=PledgeProtocol.DecodeNotice(plain);
        EnsurePledgeEpoch();
        pledgeNotice=text;pledgeRevision++;pledgeView=null;
        return true;
    }

    [WorldReset]
    void ResetPledgeWindow(){ClearPledgeWindow();pledgeEpoch=-1;}

    /// <summary>目前的血盟成員窗資料。換世界或自己的血盟名稱改變（離盟、改盟）時清空。</summary>
    public PledgeWindowState PledgeWindow{get{
        EnsurePledgeEpoch();
        if(pledgeOnline!=null&&pledgeClan!=(Actor?.Clan??""))ClearPledgeWindow();
        return pledgeView??=new(pledgeOpenRevision,pledgeRevision,pledgeClan,pledgeOnline?.ToArray(),
            new Dictionary<string,string>(pledgeMemos,StringComparer.OrdinalIgnoreCase),pledgeNotice);
    }}

    [WorldSnapshot("pledgeWindow")]
    object? PledgeWindowSnapshot(){
        var p=PledgeWindow;
        return new{openRevision=p.OpenRevision,revision=p.Revision,clan=p.Clan,online=p.Online,notice=p.Notice,
            memos=p.Memos.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value)};
    }
}

}

namespace P06.NativeUi {

/// <summary>帶有血盟成員窗資料的遊戲狀態（產品端由 client bridge 包裝；離線檢查自己提供）。</summary>
public interface IUiPledgeState {
    PledgeWindowState? Pledge { get; }
    /// <summary>R3-A（#678）：關注血盟名單（op 78／3.81 S200 type 2，Clan.cs 已存）；null＝沒有提供。</summary>
    IReadOnlyList<string>? Attention => null;
}

/// <summary>
/// Decorator：共用 IUiGameState 加上血盟成員窗快照與關注血盟名單。所有 IUiGameState 成員原樣轉給 Base。
/// R3-A（#678）：產品端放在橋接鏈最內層（ILegacyWindowStateDecorator），由 LegacyWindowPanel.FindState 找到。
/// </summary>
public sealed class UiPledgeGameState(IUiGameState inner,PledgeWindowState? pledge,IReadOnlyList<string>? attention=null):IUiGameState,IUiPledgeState,ILegacyWindowStateDecorator {
    public IUiGameState Base=>inner;
    public IUiGameState Inner=>inner;
    public PledgeWindowState? Pledge=>pledge;
    public IReadOnlyList<string>? Attention=>attention;
    public bool InWorld=>inner.InWorld;
    public string? CharacterName=>inner.CharacterName;
    public int? Level=>inner.Level;
    public long? Hp=>inner.Hp;
    public long? MaxHp=>inner.MaxHp;
    public long? Mp=>inner.Mp;
    public long? MaxMp=>inner.MaxMp;
    public long Now=>inner.Now;

    /// <summary>穿過產品的狀態包裝（LegacyWindowPanel.FindState 的完整鏈）找血盟資料；找不到回 null（窗不開）。</summary>
    public static PledgeWindowState? Of(IUiGameState? state)=>LegacyWindowPanel.FindState<IUiPledgeState>(state)?.Pledge;
    /// <summary>關注血盟名單；找不到或沒提供回 null。</summary>
    public static IReadOnlyList<string>? AttentionOf(IUiGameState? state)=>LegacyWindowPanel.FindState<IUiPledgeState>(state)?.Attention;
}

/// <summary>
/// R4-E（#708）：血盟窗的對外動作與額外資料。產品端由 ClanUi.cs 的 WorldPledgeSource 接到 LoginConnection（面板不碰 LoginConnection）。
/// 動作回傳 true＝已交給既有送出流程（結果以伺服器回報為準）；false＝本機條件不合，沒有送。
/// </summary>
public interface IPledgeWindowSource {
    /// <summary>S250/173 type 1 的「顯示盟徽」旗標；null＝本連線還沒收到。</summary>
    bool? ShowEmblem { get; }
    /// <summary>自己血盟的盟徽像素（沒有血盟或還沒收到 S118 是 null）。</summary>
    ClanEmblem? Emblem { get; }
    /// <summary>C78 C(15) S(公告)。</summary>
    bool SendNotice(string text);
    /// <summary>C63 C(1) C(階級) S(成員)。</summary>
    bool SetRank(string member,byte rank);
    /// <summary>C254 C(0x2E) C(0／1)。</summary>
    bool SetShowEmblem(bool show);
    /// <summary>選 16×12 BMP 後送 C18（產品端開檔案對話框）。</summary>
    bool UploadEmblem();
    // T316（#746，FU6§1、§3）：預設實作＝沒有接上（不送）。產品端 WorldPledgeSource 覆寫。
    /// <summary>C129 C(0) S(血盟名)：AddFocusPledge 的輸入框（字串 3361）確定後。</summary>
    bool AddAttention(string name)=>false;
    /// <summary>C129 C(1) S(血盟名)：DelFocusPledge 確認後，每個勾選的血盟各一包（sub_B2C2F0）。</summary>
    bool RemoveAttention(IReadOnlyList<string> names)=>false;
    /// <summary>ChangeWarState 值 1（宣戰）開輸入框 577 前的 478／785 檢查（sub_B34930）；false＝擋下（訊息已印在聊天欄）。</summary>
    bool WarPrecheck()=>true;
    /// <summary>C227 C(type) S(對方血盟)：輸入框 577／578／579 確定後。</summary>
    bool ChangeWar(ClanWarCommand command,string name)=>false;
    /// <summary>R7-K：C78 C(16) S(備註)——ModifyMemo 確定後，改自己的成員備註。預設＝沒有接上（不送）。</summary>
    bool SendMemo(string text)=>false;
    /// <summary>T423（#967）：聊天輸入列備註超過 60 byte 時，3328 印在聊天欄（sub_B34270 → sub_7DC850）。預設＝不印。</summary>
    void MemoNotice(string text){}
}

/// <summary>
/// 血盟成員窗（PledgeUI.xml）。171 開窗並列出上線成員；169 更新備註欄；168 更新公告欄（公告窗 Notice_Label）。
/// 只畫伺服器有送的資料：姓名（171）、備註（169）、公告（168）、自己的盟名（S72／角色資料）、自己的盟徽（S118）。
/// 職業圖、階級、創立日、聯盟欄 3.81 都送不到（167／170 格式壞掉），一律空白，不推算。
/// R4-E（#708）：公告窗的「公告」鈕（Writenotice_Button）進入編輯，「存檔」鈕（Savenotice_Button）或 Enter 送 C78 15；
/// 點成員列的階級欄開 ChangeRank_PopupWindow，點階級鈕送 C63；Pledge_Emblem 畫自己的盟徽；
/// 本機 PledgeUI.xml 沒有關注清單與「顯示盟徽」節點，所以視窗右側加一個引擎自訂的側欄（關注血盟 7 列、顯示盟徽勾選框、上傳盟徽鈕）。
/// T316（#746，FU6§1、§3）：側欄再加 AddFocusPledge（新增，輸入框 3361 → C129 子碼 0）、CheckFocusPledge（點清單列切換勾選，不送包）、
/// DelFocusPledge（刪除，確認框 4118 → 每個勾選列各送一包子碼 1）、ChangeWarState 三態（1 宣戰／2 停戰／3 投降，輸入框 577／579／578 → C227）。
/// 側欄的位置與按鈕外觀是引擎自訂（原版讀 RePledgeUI.xml，本機缺）；輸入框與確認框也畫在側欄內（原版是共用輸入框 sub_AC98E0）。
/// R7-K（T350／#813，WIN2§3）：ModifyMemo_Button（MouseEvent="ModifyMemo"，本機 PledgeUI.xml 兩個窗都在 (217,24)）進入自己那一列的備註編輯，
/// 再按一次或 Enter 送 C78 16（ClanMatchingProtocol.Notes）；畫面等伺服器回送 169 才更新，不先改本機備註。
/// 原版 ModifyMemo 的編輯框位置與按鍵沒查（推測：畫在自己那列的 Memo_Label）。排序不做。
/// </summary>
public sealed class PledgePanel:IUiMovablePanel,IUiClosablePanel,IUiTextInput {
    [UiPanel(175)] internal static UiPanelRegistration Registration=>new(PanelId,LayoutName,root=>new PledgePanel(root.Find));   // R3-A (#678): opened by S250/171
    public const string PanelId="PledgeUI",LayoutName="PledgeUI";
    public const string PlainWindowName="Nonoticeuserinfo_Window",NoticeWindowName="Noticeuserinfo_Window";
    // ---- 推測（查證後只改這裡）----
    /// <summary>文字高度：UiText 暫定 新細明體 9pt ≈ 12 px（V4 (f) 待驗證）；用於 VAlign="middle" 置中。</summary>
    public const int TextHeight=12;
    /// <summary>字色：XML 沒寫 FontColor 的 Label 用白色（推測）。</summary>
    public static readonly Color TextColor=Color.FromArgb(255,255,255,255);
    /// <summary>KeyboardEvent="CloseUI" 的實體按鍵：Esc（推測）。</summary>
    public const Keys CloseKey=Keys.Escape;
    /// <summary>公告 MultiLabel 的行距：等於 TextHeight（推測；109 px 高 ÷ 12 ＝ 9 行，與 Notice_Edit LimitLine="9" 吻合）。</summary>
    public const int NoticeLinePitch=TextHeight;
    /// <summary>
    /// 關注血盟清單（SS2§3，sub_B30040）：一次 7 列（圖示＋名稱），捲動上限＝(筆數 − 7) / 2（照反組譯寫法，意義待驗證）。
    /// 原版清單在 PledgeUI 子控制項 (3, 7)（RePledgeUI.xml，本機缺）；本機 PledgeUI.xml 沒有對應節點，所以畫在引擎自訂側欄（R4-E／T316 起，
    /// 見 AttentionRow／PaintSide）。原版座標與圖示待驗證。
    /// </summary>
    public const int AttentionRows=7;
    /// <summary>
    /// ChangeRank_PopupWindow 的 Rank0–3_Button（圖上文字依序 修習／一般／守護／副君主）→ C63 階級值 8／7／9／3（3.81 C_Rank 註解：
    /// 7 一般、8 修習騎士、9 守護騎士、3 副君主）。TW16 ChangeRank 事件的索引對應沒查（推測）。
    /// </summary>
    public static readonly byte[] PopupRanks=[8,7,9,3];
    /// <summary>公告編輯中的字色（推測：與一般文字區分）。</summary>
    public static readonly Color EditColor=Color.FromArgb(255,255,255,128);
    /// <summary>側欄（引擎自訂版面，原版讀 RePledgeUI.xml 的 PledgeOption_Window，本機缺圖）：寬、內距、列距與各區塊的 y。</summary>
    public const int SideWidth=112,SidePad=4,SideRowPitch=15,AttentionTop=20,FocusTop=AttentionTop+AttentionRows*SideRowPitch+4,
        WarTop=FocusTop+20,CheckTop=WarTop+22,UploadTop=CheckTop+17,InputTop=UploadTop+20,StatusTop=InputTop+20,SideHeight=StatusTop+40;
    /// <summary>T316：側欄輸入框最多收的字數（C129 名稱 16 byte；C227 名稱另由 ClanWarProtocol 檢查 32 byte）。</summary>
    public const int SideInputLimit=16;
    /// <summary>ChangeWarState 三個鈕依序代表的控制項值（1 宣戰、2 停戰、3 投降，FU6§3.2）。</summary>
    public static readonly int[] WarStates=[1,2,3];
    public static readonly Color SideBack=Color.FromArgb(230,20,26,34),SideBorder=Color.FromArgb(255,113,113,113),Dim=Color.FromArgb(255,150,150,150);
    // ----

    /// <summary>XML 中一個有圖的元件：視窗內相對座標＋第一張 surf（V4 前只用第 0 張）。</summary>
    public sealed record Placed(Point At,UiSurface? Surface) {
        public Rectangle Rect=>Surface==null?Rectangle.Empty:new(At,new(Surface.Width,Surface.Height));
    }
    public sealed record Column(string Name,int X,int Width);

    /// <summary>一個 Window 元素的版面（座標全部相對於視窗左上）。</summary>
    public sealed class WindowLayout {
        public required string Name{get;init;}
        public UiSurface? Base{get;init;}
        public UiSurface? Frame{get;init;}
        /// <summary>Moveable="20"：視窗頂端這麼高的區域可拖曳（推測：數值是拖曳帶高度）。</summary>
        public int DragHeight{get;init;}
        public required Placed Close{get;init;}
        public Rectangle PledgeLabel{get;init;}
        public required Placed NoticeButton{get;init;}
        public required Placed NoticeArrow{get;init;}
        /// <summary>公告文字區（Notice_Control＋Notice_Label，相對視窗）；只有公告窗有，無公告窗為 Empty。</summary>
        public Rectangle NoticeText{get;init;}
        public required IReadOnlyList<Placed> SortButtons{get;init;}
        public Point ListAt{get;init;}
        public int Rows{get;init;}
        public int RowPitch{get;init;}
        public int RowHeight{get;init;}
        public required IReadOnlyList<Column> Columns{get;init;}
        public Point ScrollAt{get;init;}
        public required Placed ScrollBackground{get;init;}
        public required Placed ScrollUp{get;init;}
        public required Placed ScrollThumb{get;init;}
        public required Placed ScrollDown{get;init;}
        public int ScrollStart{get;init;}
        public int ScrollHeight{get;init;}
        /// <summary>R4-E：Noticebutton_Control 的「公告」「存檔」鈕（同一位置，編輯中顯示存檔）；XML 沒有就是 null（不能編輯）。</summary>
        public Placed? WriteNotice{get;init;}
        public Placed? SaveNotice{get;init;}
        /// <summary>R4-E：Pledgename_Control 的 Pledge_Emblem（16×12）；XML 沒有就是 Empty。</summary>
        public Rectangle Emblem{get;init;}
        /// <summary>R7-K：ModifyMemo_Button（MouseEvent="ModifyMemo"）；XML 沒有就是 null（不能寫備註）。</summary>
        public Placed? ModifyMemo{get;init;}
        public Size Size=>Base==null?Size.Empty:new(Base.Width,Base.Height);
        public Column Col(string name)=>Columns.First(c=>c.Name==name);
    }

    public const string RankPopupName="ChangeRank_PopupWindow";
    /// <summary>3.81 公告上限 160 字（Notice_Edit LimitText="160"、C_PledgeContent 15）。</summary>
    public const int NoticeLimit=ClanMatchingProtocol.AnnounceMaxChars;
    /// <summary>
    /// R7-K：備註上限＝S250/169 的固定 62 byte（Big5）。3.81 C_PledgeContent 16 沒檢查長度，但回送的 S_Pledge(name,notes) 只有 62 byte，
    /// 超過就回不來；原版輸入框上限沒查（推定）。
    /// </summary>
    public const int MemoByteLimit=PledgeProtocol.NotesLength;
    /// <summary>
    /// T423（#967，FU15§2.3，已驗證）：ModifyMemo（sub_B2D740 → sub_B341F0）在**聊天輸入列**用字串 3325 問（sub_AC98E0，回呼 sub_B34270）；
    /// Enter 且文字不空：strlen &gt; 60（0x3C）→ 3328 印在聊天欄、不送；否則送 C78 16。62 是緩衝區大小，輸入上限是 60 byte。
    /// T437（#1008，FU17§4）：長度檢查通過後，sub_51F850(文字, "*") 做不雅字過濾（ObsceneFilter），送出過濾後的文字。沒有聊天窗時保留 R7-K 的窗內編輯（後備，62 byte，同樣過濾）。
    /// </summary>
    public const int MemoPromptMessage=3325,MemoTooLongMessage=3328,MemoInputLimit=60;
    /// <summary>本機 string-c.tbl 的 3325／3328；表讀不到時用同樣的字（參考客戶端 kor25／ly／t89 的字面略有不同）。</summary>
    public static string MemoPromptText=>MemoTableText(MemoPromptMessage,"請輸入目錄內容。(最多60個字/Ese=取消)");
    public static string MemoTooLongText=>MemoTableText(MemoTooLongMessage,"目錄內容太長。(最多60個字)");
    static string MemoTableText(int id,string fallback)=>ServerMessageText.Table is {} t&&id<t.Count&&ServerMessageText.DisplayText(t[id]).Trim() is {Length:>0} s?s:fallback;
    /// <summary>聊天輸入列備註（sub_B34270）：C78 16 的字元規則，加上輸入上限 60 byte（超過＝3328）。可以送回 null。</summary>
    public static string? MemoPromptError(string text){
        try{ClanMatchingProtocol.Notes(text);}catch(ArgumentException ex){return ex.Message.Replace("成員備註","備註");}
        return LoginPackets.TextEncoding.GetByteCount(text)>MemoInputLimit?MemoTooLongText:null;
    }

    WindowLayout? plain,notice;
    IReadOnlyList<Placed> rankButtons=[];
    bool open,showNotice,inWorld,editing;
    long seenOpen;
    Point origin;
    int scroll,attentionScroll;
    PledgeWindowState? data;
    IReadOnlyList<string> attention=[];
    readonly System.Text.StringBuilder draft=new();
    string? popupMember;Point popupAt;
    string status="";
    Bitmap? emblemBitmap;int[]? emblemPixels;
    // T316：關注清單的勾選（CheckFocusPledge）與側欄的輸入框／確認框。
    public enum SideInputKind { None, AddFocus, DeleteConfirm, War }
    readonly HashSet<string> checkedAttention=new(StringComparer.OrdinalIgnoreCase);
    SideInputKind sideInput;ClanWarCommand sideWar;
    readonly System.Text.StringBuilder sideDraft=new();
    // R7-K：ModifyMemo 編輯中的草稿；characterName＝自己的角色名（IUiGameState.CharacterName），用來找自己那一列。
    bool memoEditing;string? characterName;
    // T423（#967）：找聊天窗（CENTER_WINDOW），ModifyMemo 在聊天輸入列問 3325；null＝沒有（保留 R7-K 窗內編輯）。
    readonly Func<string,IUiPanel?>? find;
    public PledgePanel(Func<string,IUiPanel?>? find=null){this.find=find;}
    readonly System.Text.StringBuilder memoDraft=new();

    /// <summary>R4-E：對外動作（產品端每個 timer tick 由 ClanUi.cs 接上；離線檢查自己給）。null＝不能送，只顯示。</summary>
    public IPledgeWindowSource? Source{get;set;}
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public WindowLayout? Current=>showNotice?notice:plain;
    /// <summary>視窗本體（XML 底圖）。</summary>
    public Rectangle WindowBounds=>Current is {Base:not null} w?new(origin,w.Size):Rectangle.Empty;
    /// <summary>視窗本體＋右側引擎自訂側欄（UiRoot 以 Bounds 裁切與點擊判定）。</summary>
    public Rectangle Bounds=>WindowBounds.IsEmpty?Rectangle.Empty:Rectangle.Union(WindowBounds,SideRect);
    /// <summary>R4-E 側欄（關注血盟、顯示盟徽、上傳盟徽）；緊貼視窗右緣。</summary>
    public Rectangle SideRect=>WindowBounds is {IsEmpty:false} b?new(b.Right,b.Top,SideWidth,SideHeight):Rectangle.Empty;
    Rectangle Side(int x,int y,int width,int height)=>SideRect is {IsEmpty:false} s?new(s.X+x,s.Y+y,width,height):Rectangle.Empty;
    /// <summary>關注清單第 i 列（圖示槽 16 px＋名稱）。</summary>
    public Rectangle AttentionRow(int i)=>Side(SidePad,AttentionTop+i*SideRowPitch,SideWidth-2*SidePad-12,SideRowPitch);
    public Rectangle AttentionUpRect=>Side(SideWidth-SidePad-10,AttentionTop,10,10);
    public Rectangle AttentionDownRect=>Side(SideWidth-SidePad-10,AttentionTop+AttentionRows*SideRowPitch-10,10,10);
    public Rectangle ShowEmblemRect=>Side(SidePad,CheckTop,11,11);
    public Rectangle UploadRect=>Side(SidePad,UploadTop,SideWidth-2*SidePad,16);
    public Rectangle StatusRect=>Side(SidePad,StatusTop,SideWidth-2*SidePad,36);
    /// <summary>T316：AddFocusPledge／DelFocusPledge 鈕（引擎自訂位置）。</summary>
    public Rectangle FocusAddRect=>Side(SidePad,FocusTop,(SideWidth-2*SidePad-4)/2,16);
    public Rectangle FocusDeleteRect=>Side(SidePad+(SideWidth-2*SidePad-4)/2+4,FocusTop,(SideWidth-2*SidePad-4)/2,16);
    /// <summary>T316：ChangeWarState 第 i 個鈕（WarStates[i]：1 宣戰、2 停戰、3 投降）。</summary>
    public Rectangle WarStateRect(int i){int w=(SideWidth-2*SidePad-4)/3;return i is <0 or >2?Rectangle.Empty:Side(SidePad+i*(w+2),WarTop,w,16);}
    /// <summary>T316：側欄輸入框（輸入中才畫草稿）。</summary>
    public Rectangle SideInputRect=>Side(SidePad,InputTop,SideWidth-2*SidePad,16);
    /// <summary>關注清單第 i 列的勾選框（列左邊的圖示槽）。</summary>
    public Rectangle AttentionCheckRect(int i)=>AttentionRow(i) is {IsEmpty:false} r?new(r.X+2,r.Y+2,11,11):Rectangle.Empty;
    public SideInputKind SideInput=>sideInput;
    public string SideDraft=>sideDraft.ToString();
    /// <summary>勾選中的關注血盟（依清單順序）。</summary>
    public IReadOnlyList<string> CheckedAttention=>attention.Where(checkedAttention.Contains).ToArray();
    /// <summary>
    /// 側欄提示文字：有 string-c.tbl 就照表；沒有時用 FU6 記下的本機表文字（3361、577、578、579）。
    /// 4118（刪除確認）本機表沒有（3484 筆），用編號占位。
    /// </summary>
    public static string PromptText(int id){
        if(ServerMessageText.Table is {} t&&id<t.Count)return ServerMessageText.DisplayText(t[id]);
        return id switch{
            ClanProtocol.AttentionInputText=>"請輸入想要查看的血盟名。",ClanWarProtocol.DeclareInputText=>"請輸入要宣戰的對象血盟名稱。",
            ClanWarProtocol.SurrenderInputText=>"請輸入要投降的血盟名稱。",ClanWarProtocol.CeaseInputText=>"請輸入要停戰的血盟名稱。",
            _=>$"〔字串 {id}〕"};
    }
    /// <summary>最近一次血盟窗動作的結果（引擎自訂提示；伺服器結果另由聊天區／WinForms 血盟窗顯示）。</summary>
    public string Status=>status;
    public bool EditingNotice=>editing;
    public string NoticeDraft=>draft.ToString();
    /// <summary>R7-K：ModifyMemo 編輯中。</summary>
    public bool EditingMemo=>memoEditing;
    public string MemoDraft=>memoDraft.ToString();
    /// <summary>R7-K：ModifyMemo_Button（邏輯座標）；目前窗沒有這個節點就是 Empty。</summary>
    public Rectangle ModifyMemoRect=>Current is {ModifyMemo:{} m}?Abs(m.Rect):Rectangle.Empty;
    /// <summary>自己在上線名單中的索引；不在名單（或不知道角色名）是 -1。</summary>
    public int OwnIndex=>string.IsNullOrEmpty(characterName)?-1:Members.ToList().FindIndex(n=>string.Equals(n,characterName,StringComparison.OrdinalIgnoreCase));
    /// <summary>自己那一列的備註欄（目前可見才有；否則 Empty，草稿改畫在側欄輸入框）。</summary>
    public Rectangle OwnMemoRect{get{
        int k=OwnIndex;if(k<0||Current is not {} w||k<scroll||k>=scroll+w.Rows)return Rectangle.Empty;
        return Cell(k-scroll,"Memo_Label");
    }}
    /// <summary>目前顯示的「公告」或「存檔」鈕（只有公告窗）；沒有就是 Empty。</summary>
    public Rectangle NoticeEditRect=>Current is {WriteNotice:{} wr,SaveNotice:{} sv}?Abs((editing?sv:wr).Rect):Rectangle.Empty;
    public Rectangle EmblemRect=>Current is {} w?Abs(w.Emblem):Rectangle.Empty;
    public IReadOnlyList<Placed> RankButtons=>rankButtons;
    /// <summary>改階級彈出窗的對象（null＝沒開）。</summary>
    public string? RankPopupMember=>popupMember;
    public Rectangle RankButtonRect(int i)=>popupMember==null||i<0||i>=rankButtons.Count?Rectangle.Empty:
        new(origin.X+popupAt.X+rankButtons[i].At.X,origin.Y+popupAt.Y+rankButtons[i].At.Y,rankButtons[i].Rect.Width,rankButtons[i].Rect.Height);
    public bool Visible=>open&&inWorld&&data?.Online!=null&&Current is {Base:not null};
    public bool IsOpen=>open;
    public bool ShowingNotice=>showNotice;
    public int Scroll=>scroll;
    public Point Origin=>origin;
    public IReadOnlyList<string> Members=>data?.Online??[];
    /// <summary>R3-A（#678）：關注血盟名單（S200 type 2；沒收到是空的）。</summary>
    public IReadOnlyList<string> Attention=>attention;
    public int AttentionScroll=>attentionScroll;
    public int AttentionMaxScroll=>Math.Max(0,(attention.Count-AttentionRows)/2);
    /// <summary>目前這一頁的關注血盟（從第 AttentionScroll 筆起最多 7 筆，sub_B30040(a2)）。</summary>
    public IReadOnlyList<string> AttentionPage=>attention.Skip(attentionScroll).Take(AttentionRows).ToArray();
    public void ScrollAttention(int rows)=>attentionScroll=Math.Clamp(attentionScroll+rows,0,AttentionMaxScroll);
    public int MaxScroll=>Current is {} w?Math.Max(0,Members.Count-w.Rows):0;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>離線檢查用：surf 由呼叫端提供（合成圖）。</summary>
    public void Load(UiLayout layout,Func<int,UiSurface?> surf){
        if(layout.Root.Name!=LayoutName)throw new UiLayoutException(layout.FileName,layout.Root.Path,$"根元素應為 {LayoutName}");
        plain=LoadWindow(layout.Root,PlainWindowName,surf);
        notice=LoadWindow(layout.Root,NoticeWindowName,surf);
        // R4-E：ChangeRank_PopupWindow 的 Rank0–3_Button（相對彈出窗左上）；窗或鈕缺就沒有改階級入口（WinForms 血盟窗仍有）。
        var ranks=new List<Placed>();
        if(Optional(layout.Root,"Window",RankPopupName) is {} pw)
            for(int i=0;i<PopupRanks.Length;i++){
                if(Optional(pw,"Button",$"Rank{i}_Button") is not {} b)break;
                ranks.Add(Place(b,Point.Empty,"BaseSurf",surf));
            }
        rankButtons=ranks;
        // XML 的視窗座標是 (0,0)；原版開窗位置（置中或記憶位置）待驗證，先用 XML 值。
        origin=new(Named(layout.Root,"Window",PlainWindowName).TryInt("X")??0,Named(layout.Root,"Window",PlainWindowName).TryInt("Y")??0);
    }

    static WindowLayout LoadWindow(UiNode root,string name,Func<int,UiSurface?> get){
        var w=Named(root,"Window",name);
        var nb=Named(w,"Button","Pledgenotice_Button");var noticeButton=Place(nb,Point.Empty,"BaseSurf",get);
        var ctl=Named(w,"Control","Pledgename_Control");var lbl=Named(ctl,"Label","Pledge_Label");
        var sortList=Named(w,"List","Sort_List");var sortAt=XY(sortList);var sorts=new List<Placed>();int sx=0;
        foreach(var b in sortList.ChildrenNamed("Button")){
            var p=Place(b,new(sortAt.X+sx,sortAt.Y),"BaseSurf",get);sorts.Add(p);
            sx+=b.TryInt("Width")??p.Surface?.Width??throw new UiLayoutException(b.Layout.FileName,b.Path,"排序鈕沒有寬度也沒有 surf");
        }
        var list=Named(w,"List","Userinfo_List");
        var rows=list.ChildrenNamed("List").ToArray();
        if(rows.Length==0)throw new UiLayoutException(list.Layout.FileName,list.Path,"Userinfo_List 沒有列");
        var row=rows[0];int gap=row.TryInt("Gap")??0,cx=0;var cols=new List<Column>();
        foreach(var c in row.Children){string n=c.RequiredAttr("Name");int width=c.Int("Width");cols.Add(new(n,cx,width));cx+=width+gap;}
        foreach(var need in new[]{"Class_Image","Name_Label","Rank_Label","Memo_Label"})
            if(cols.All(c=>c.Name!=need))throw new UiLayoutException(row.Layout.FileName,row.Path,$"缺少欄位 {need}");
        var sb=Named(w,"ScrollBar","Userinfo_Scrollbar");var sbAt=XY(sb);
        var noticeText=Rectangle.Empty;
        if(name==NoticeWindowName){
            // 公告窗必須有公告區（本機 PledgeUI.xml：Notice_Control (21,63) 225×112，Notice_Label 220×109 靠上靠左）；缺就報錯，不補座標。
            var nc=Named(w,"Control","Notice_Control");var nl=Named(nc,"MultiLabel","Notice_Label");
            noticeText=new(XY(nc).X+XY(nl).X,XY(nc).Y+XY(nl).Y,nl.Int("Width"),nl.Int("Height"));
        }
        // R4-E：選用節點（W2-H 的合成 XML 沒有；本機 PledgeUI.xml 的公告窗兩個都有，無公告窗只有盟徽）。
        Placed? write=null,save=null;
        if(Optional(w,"Control","Noticebutton_Control") is {} nbc){
            var at=XY(nbc);
            if(Optional(nbc,"Button","Writenotice_Button") is {} wb)write=Place(wb,at,"BaseSurf",get);
            if(Optional(nbc,"Button","Savenotice_Button") is {} svb)save=Place(svb,at,"BaseSurf",get);
            if(write==null||save==null)write=save=null;   // 只有其中一個：無法切換，當作沒有
        }
        var emblem=Rectangle.Empty;
        if(Optional(ctl,"Emblem","Pledge_Emblem") is {} em){var p=XY(em);emblem=new(XY(ctl).X+p.X,XY(ctl).Y+p.Y,ClanEmblemProtocol.Width,ClanEmblemProtocol.Height);}
        var memo=Optional(w,"Button","ModifyMemo_Button") is {} mb?Place(mb,Point.Empty,"BaseSurf",get):null;
        return new WindowLayout{
            WriteNotice=write,SaveNotice=save,Emblem=emblem,ModifyMemo=memo,
            Name=name,
            Base=get(SurfOf(w,"BaseSurf")),
            Frame=w.Attr("FrameSurf") is null?null:get(SurfOf(w,"FrameSurf")),
            DragHeight=w.TryInt("Moveable")??0,
            Close=Place(Named(w,"Button","Closeui_Button"),Point.Empty,"BaseSurf",get),
            PledgeLabel=new(XY(ctl).X+XY(lbl).X,XY(ctl).Y+XY(lbl).Y,lbl.Int("Width"),lbl.Int("Height")),
            NoticeButton=noticeButton,
            // 子按鈕的 X/Y 相對於父按鈕（Noticetri_Button X=200 落在 4455 條的右端，推測）。
            NoticeArrow=Place(Named(nb,"Button","Noticetri_Button"),noticeButton.At,"BaseSurf",get),
            NoticeText=noticeText,
            SortButtons=sorts,
            ListAt=XY(list),Rows=rows.Length,
            // 垂直清單沒寫 Gap：列距＝列高＋清單 Gap（預設 0，推測）。
            RowHeight=row.Int("Height"),RowPitch=row.Int("Height")+(list.TryInt("Gap")??0),
            Columns=cols,
            ScrollAt=sbAt,
            ScrollBackground=Place(Named(sb,"Image","Background_Image"),sbAt,"Surf",get),
            ScrollUp=Place(Named(sb,"Button","Up_Button"),sbAt,"BaseSurf",get),
            ScrollThumb=Place(Named(sb,"Button","Move_Button"),sbAt,"BaseSurf",get),
            ScrollDown=Place(Named(sb,"Button","Down_Button"),sbAt,"BaseSurf",get),
            ScrollStart=sb.Int("ScrollStart"),ScrollHeight=sb.Int("ScrollHeight"),
        };
    }

    /// <summary>直接子元素中 element 名稱＝elem 且 Name 屬性＝name 的唯一一個；缺或重複就丟 UiLayoutException。</summary>
    static UiNode Named(UiNode parent,string elem,string name){
        var m=parent.ChildrenNamed(elem).Where(c=>c.Attr("Name")==name).Take(2).ToArray();
        if(m.Length!=1)throw new UiLayoutException(parent.Layout.FileName,parent.Path+"/"+elem+"[@Name="+name+"]",m.Length==0?"缺少節點":"節點不唯一");
        return m[0];
    }
    /// <summary>同 Named，但缺少時回 null（重複仍丟例外）。</summary>
    static UiNode? Optional(UiNode parent,string elem,string name){
        var m=parent.ChildrenNamed(elem).Where(c=>c.Attr("Name")==name).Take(2).ToArray();
        if(m.Length>1)throw new UiLayoutException(parent.Layout.FileName,parent.Path+"/"+elem+"[@Name="+name+"]","節點不唯一");
        return m.Length==1?m[0]:null;
    }
    /// <summary>PledgeUI.xml 的 X／Y；清單內元件沒寫時視為 0（相對父元件，推測）。</summary>
    static Point XY(UiNode n)=>new(n.TryInt("X")??0,n.TryInt("Y")??0);
    /// <summary>"4450;1" → 4450（分號後的數字意義待驗證，只取第一個）；"5726~5728" 也只取第一張。</summary>
    public static int SurfOf(UiNode n,string attr){
        string raw=n.RequiredAttr(attr);string first=raw.Split(';')[0];
        int[] s;
        try{s=UiNode.ParseSurfRange(first);}catch(FormatException ex){throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\"："+ex.Message,ex);}
        return s.Length>0?s[0]:throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr} 是空的");
    }
    static Placed Place(UiNode n,Point parent,string attr,Func<int,UiSurface?> get){
        var at=XY(n);return new(new(parent.X+at.X,parent.Y+at.Y),get(SurfOf(n,attr)));
    }

    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        characterName=inWorld?state.CharacterName:null;
        data=inWorld?UiPledgeGameState.Of(state):null;
        attention=inWorld?UiPledgeGameState.AttentionOf(state)??[]:[];
        attentionScroll=Math.Clamp(attentionScroll,0,AttentionMaxScroll);
        checkedAttention.RemoveWhere(n=>!attention.Contains(n,StringComparer.OrdinalIgnoreCase));
        if(data?.Online==null){open=false;scroll=0;ResetInteraction();return;}
        if(data.OpenRevision!=seenOpen){
            seenOpen=data.OpenRevision;
            // 每次 171 都開窗並回到清單頂端（TW16 sub_B2E3D0 先清清單再逐筆加入，推測捲動位置也歸零）。
            open=true;scroll=0;ResetInteraction();status="";
        }
        scroll=Math.Clamp(scroll,0,MaxScroll);
        if(popupMember!=null&&!Members.Contains(popupMember,StringComparer.OrdinalIgnoreCase))popupMember=null;
    }
    /// <summary>關窗、重開、離開世界：結束公告編輯（草稿丟掉）並關掉改階級彈出窗。</summary>
    void ResetInteraction(){editing=false;draft.Clear();popupMember=null;CancelSideInput();CancelMemoEdit();checkedAttention.Clear();}

    /// <summary>邏輯座標（視窗左上＋相對座標）。</summary>
    Rectangle Abs(Rectangle r)=>r.IsEmpty?r:new(origin.X+r.X,origin.Y+r.Y,r.Width,r.Height);
    public Rectangle CloseRect=>Current is {} w?Abs(w.Close.Rect):Rectangle.Empty;
    public Rectangle NoticeRect=>Current is {} w?Abs(w.NoticeButton.Rect):Rectangle.Empty;
    public Rectangle NoticeArrowRect=>Current is {} w?Abs(w.NoticeArrow.Rect):Rectangle.Empty;
    public Rectangle ScrollUpRect=>Current is {} w?Abs(w.ScrollUp.Rect):Rectangle.Empty;
    public Rectangle ScrollDownRect=>Current is {} w?Abs(w.ScrollDown.Rect):Rectangle.Empty;
    /// <summary>公告文字區（邏輯座標）；無公告窗時是 Empty。</summary>
    public Rectangle NoticeTextRect=>Current is {} w?Abs(w.NoticeText):Rectangle.Empty;
    /// <summary>目前要顯示的公告（沒收到 168 是空字串）。</summary>
    public string Notice=>data?.Notice??"";

    /// <summary>
    /// 公告文字分行：先依換行字元（\r\n、\n、\r）分段，每段依寬度逐字折行，最多 maxLines 行（超出的不畫）。
    /// 3.81 公告上限 160 字、Notice_Edit LimitLine="9"；MultiLabel 的實際折行規則待驗證，先用與聊天列相同的逐字折行（推測）。
    /// </summary>
    public static List<string> NoticeLines(string text,int maxWidth,Func<char,int> width,int maxLines){
        var lines=new List<string>();
        if(string.IsNullOrEmpty(text)||maxLines<=0)return lines;
        foreach(var para in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n'))
            foreach(var row in ChatRules.Wrap(para,maxWidth,width)){
                if(lines.Count>=maxLines)return lines;
                lines.Add(row);
            }
        return lines;
    }
    /// <summary>第 i 個可見列的某一欄（邏輯座標）。</summary>
    public Rectangle Cell(int row,string column){
        if(Current is not {} w)return Rectangle.Empty;var c=w.Col(column);
        return new(origin.X+w.ListAt.X+c.X,origin.Y+w.ListAt.Y+row*w.RowPitch,c.Width,w.RowHeight);
    }
    /// <summary>捲軸拉桿的 y（相對視窗）：ScrollStart 起，在 ScrollHeight 內依捲動比例移動（推測）。</summary>
    public int ThumbY{get{
        if(Current is not {} w)return 0;
        int h=w.ScrollThumb.Surface?.Height??0,max=MaxScroll;
        int travel=Math.Max(0,w.ScrollHeight-h);
        return w.ScrollAt.Y+w.ScrollStart+(max==0?0:travel*scroll/max);
    }}

    public void Paint(UiCanvas canvas){
        if(Current is not {} w||data?.Online is not {} members)return;
        int ox=origin.X,oy=origin.Y;
        canvas.DrawSurface(w.Base,ox,oy);
        canvas.DrawSurface(w.Frame,ox,oy);
        Draw(canvas,w.Close);Draw(canvas,w.NoticeButton);Draw(canvas,w.NoticeArrow);
        foreach(var s in w.SortButtons)Draw(canvas,s);
        var label=Abs(w.PledgeLabel);
        canvas.DrawText(data.Clan,label.X,Middle(label),TextColor);
        if(!w.Emblem.IsEmpty&&Source?.Emblem is {} emblem){var er=Abs(w.Emblem);canvas.DrawBitmap(EmblemBitmap(emblem),er.X,er.Y);}
        if(w.WriteNotice is {} wr&&w.SaveNotice is {} sv)Draw(canvas,editing?sv:wr);
        if(w.ModifyMemo is {} mm)Draw(canvas,mm);
        if(!w.NoticeText.IsEmpty&&(editing||!string.IsNullOrEmpty(data.Notice))){
            // Notice_Label：VAlign="top" HAlign="left"，從左上逐行往下。編輯中畫草稿＋游標（Notice_Edit 同一區）。
            var nt=Abs(w.NoticeText);var g=canvas.Graphics;
            string text=editing?draft.ToString()+"_":data.Notice!;
            var lines=NoticeLines(text,nt.Width,c=>UiText.Measure(g,c.ToString()).Width,nt.Height/NoticeLinePitch);
            for(int i=0;i<lines.Count;i++)canvas.DrawText(lines[i],nt.X,nt.Y+i*NoticeLinePitch,editing?EditColor:TextColor);
        }
        for(int i=0;i<w.Rows;i++){
            int k=scroll+i;if(k>=members.Count)break;
            string name=members[k];
            var nc=Cell(i,"Name_Label");
            int tw=UiText.Measure(canvas.Graphics,name).Width;
            canvas.DrawText(name,nc.X+Math.Max(0,(nc.Width-tw)/2),Middle(nc),TextColor);
            var mc=Cell(i,"Memo_Label");
            if(memoEditing&&k==OwnIndex)canvas.DrawText(memoDraft+"_",mc.X,Middle(mc),EditColor);   // R7-K 草稿
            else canvas.DrawText(data.Memo(name),mc.X,Middle(mc),TextColor);
        }
        Draw(canvas,w.ScrollBackground);Draw(canvas,w.ScrollUp);Draw(canvas,w.ScrollDown);
        canvas.DrawSurface(w.ScrollThumb.Surface,ox+w.ScrollThumb.At.X,oy+ThumbY);
        if(popupMember!=null)for(int i=0;i<rankButtons.Count;i++){var r=RankButtonRect(i);canvas.DrawSurface(rankButtons[i].Surface,r.X,r.Y);}
        PaintSide(canvas);
    }

    /// <summary>R4-E 側欄（引擎自訂）：關注血盟 7 列（sub_B30040 的圖示槽留白：3.81 只送名稱，沒有盟徽編號）、捲動鈕、顯示盟徽、上傳盟徽、結果提示。</summary>
    void PaintSide(UiCanvas canvas){
        var s=SideRect;if(s.IsEmpty)return;var g=canvas.Graphics;
        using(var back=new SolidBrush(SideBack))g.FillRectangle(back,s);
        using var pen=new Pen(SideBorder);
        g.DrawRectangle(pen,s.X,s.Y,s.Width-1,s.Height-1);
        canvas.DrawText("關注血盟",s.X+SidePad,s.Y+SidePad,TextColor);
        var page=AttentionPage;
        for(int i=0;i<page.Count;i++){
            var r=AttentionRow(i);canvas.DrawText(page[i],r.X+18,Middle(r),TextColor);
            var box=AttentionCheckRect(i);g.DrawRectangle(pen,box.X,box.Y,box.Width-1,box.Height-1);
            if(checkedAttention.Contains(page[i]))using(var mark=new SolidBrush(TextColor))g.FillRectangle(mark,box.X+3,box.Y+3,box.Width-6,box.Height-6);
        }
        if(attention.Count==0){var r=AttentionRow(0);canvas.DrawText("（沒有）",r.X+18,Middle(r),Dim);}
        Arrow(g,AttentionUpRect,true,attentionScroll>0);Arrow(g,AttentionDownRect,false,attentionScroll<AttentionMaxScroll);
        void SideButton(Rectangle r,string text,bool active){
            g.DrawRectangle(pen,r.X,r.Y,r.Width-1,r.Height-1);
            int tw=UiText.Measure(g,text).Width;canvas.DrawText(text,r.X+Math.Max(1,(r.Width-tw)/2),Middle(r),Source==null?Dim:active?EditColor:TextColor);
        }
        SideButton(FocusAddRect,"新增",sideInput==SideInputKind.AddFocus);
        SideButton(FocusDeleteRect,"刪除",sideInput==SideInputKind.DeleteConfirm);
        for(int i=0;i<WarStates.Length;i++){var c=ClanWarProtocol.WindowState(WarStates[i])!.Value;SideButton(WarStateRect(i),c switch{ClanWarCommand.Declare=>"宣戰",ClanWarCommand.Cease=>"停戰",_=>"投降"},sideInput==SideInputKind.War&&sideWar==c);}
        if(sideInput is SideInputKind.AddFocus or SideInputKind.War||memoEditing&&OwnMemoRect.IsEmpty){
            // R7-K：自己那列不在畫面上（不在名單）時，備註草稿畫在同一個輸入框。
            var ir=SideInputRect;g.DrawRectangle(pen,ir.X,ir.Y,ir.Width-1,ir.Height-1);
            canvas.DrawText((memoEditing?memoDraft:sideDraft)+"_",ir.X+2,Middle(ir),EditColor);
        }
        var cb=ShowEmblemRect;bool? show=Source?.ShowEmblem;
        g.DrawRectangle(pen,cb.X,cb.Y,cb.Width-1,cb.Height-1);
        if(show==true)using(var mark=new SolidBrush(TextColor))g.FillRectangle(mark,cb.X+3,cb.Y+3,cb.Width-6,cb.Height-6);
        canvas.DrawText("顯示盟徽",cb.Right+4,cb.Y+(cb.Height-TextHeight)/2,show==null?Dim:TextColor);
        var up=UploadRect;g.DrawRectangle(pen,up.X,up.Y,up.Width-1,up.Height-1);
        canvas.DrawText("上傳盟徽…",up.X+4,Middle(up),Source==null?Dim:TextColor);
        if(status.Length>0){
            var st=StatusRect;var lines=NoticeLines(status,st.Width,c=>UiText.Measure(g,c.ToString()).Width,st.Height/NoticeLinePitch);
            for(int i=0;i<lines.Count;i++)canvas.DrawText(lines[i],st.X,st.Y+i*NoticeLinePitch,EditColor);
        }
    }
    static void Arrow(Graphics g,Rectangle r,bool up,bool enabled){
        using var b=new SolidBrush(enabled?TextColor:Dim);
        var tri=up?new Point[]{new(r.Left,r.Bottom-1),new(r.Right-1,r.Bottom-1),new(r.Left+r.Width/2,r.Top)}:new Point[]{new(r.Left,r.Top),new(r.Right-1,r.Top),new(r.Left+r.Width/2,r.Bottom-1)};
        g.FillPolygon(b,tri);
    }
    Bitmap EmblemBitmap(ClanEmblem e){
        if(emblemBitmap!=null&&emblemPixels!=null&&emblemPixels.AsSpan().SequenceEqual(e.Pixels))return emblemBitmap;
        var b=new Bitmap(ClanEmblemProtocol.Width,ClanEmblemProtocol.Height);
        for(int y=0;y<ClanEmblemProtocol.Height;y++)for(int x=0;x<ClanEmblemProtocol.Width;x++)b.SetPixel(x,y,Color.FromArgb(e.Pixels[y*ClanEmblemProtocol.Width+x]));
        emblemBitmap?.Dispose();emblemBitmap=b;emblemPixels=(int[])e.Pixels.Clone();
        return b;
    }
    void Draw(UiCanvas canvas,Placed p)=>canvas.DrawSurface(p.Surface,origin.X+p.At.X,origin.Y+p.At.Y);
    static int Middle(Rectangle r)=>r.Y+(r.Height-TextHeight)/2;

    public void Close(){open=false;showNotice=false;ResetInteraction();}

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        bool inside=Bounds.Contains(e.Location);var at=e.Location;
        if(e.Kind==UiMouseKind.Wheel){
            if(!inside)return false;int step=e.Delta>0?-1:e.Delta<0?1:0;
            if(SideRect.Contains(at))ScrollAttention(step);else ScrollBy(step);
            return true;
        }
        if(e.Kind!=UiMouseKind.Down||e.Button!=MouseButtons.Left)return inside;
        if(popupMember!=null){
            // 彈出窗（Popup="1"）：點階級鈕送出；點其他地方只關掉彈出窗。
            for(int i=0;i<rankButtons.Count;i++)if(Hit(RankButtonRect(i),at)){SetRank(popupMember,PopupRanks[i]);popupMember=null;return true;}
            popupMember=null;return inside;
        }
        if(Hit(CloseRect,at)){Close();return true;}
        if(Hit(NoticeRect,at)||Hit(NoticeArrowRect,at)){
            // OpenNotice／CloseNotice：兩個 Window 互換（XML 事件名）；公告區顯示最近一次 168 的文字（沒收到就留白）。換窗時放棄編輯。
            var o=origin;showNotice=!showNotice;editing=false;draft.Clear();CancelMemoEdit();origin=UiInput.ClampInside(o,Bounds.Size);scroll=Math.Clamp(scroll,0,MaxScroll);return true;
        }
        if(Hit(NoticeEditRect,at)){if(editing)SaveNotice();else BeginNoticeEdit();return true;}
        if(Hit(ModifyMemoRect,at)){if(memoEditing)SaveMemo();else if(!AskMemo())BeginMemoEdit();return true;}   // R7-K MouseEvent="ModifyMemo"; T423: chat-line 3325 first
        if(Hit(ScrollUpRect,at)){ScrollBy(-1);return true;}
        if(Hit(ScrollDownRect,at)){ScrollBy(1);return true;}
        if(Current is {} w&&rankButtons.Count>0)
            for(int i=0;i<w.Rows;i++){
                int k=scroll+i;if(k>=Members.Count)break;
                if(Hit(Cell(i,"Rank_Label"),at)){OpenRankPopup(i);return true;}   // Rank_Label MouseEvent="OpenPopup"
            }
        if(Hit(AttentionUpRect,at)){ScrollAttention(-1);return true;}
        if(Hit(AttentionDownRect,at)){ScrollAttention(1);return true;}
        if(Hit(ShowEmblemRect,at)){ToggleShowEmblem();return true;}
        if(Hit(UploadRect,at)){UploadEmblem();return true;}
        // T316：關注與盟戰。
        var attentionPage=AttentionPage;
        for(int i=0;i<attentionPage.Count;i++)if(Hit(AttentionRow(i),at)){ToggleAttention(attentionPage[i]);return true;}
        if(Hit(FocusAddRect,at)){BeginAddAttention();return true;}
        if(Hit(FocusDeleteRect,at)){BeginDeleteAttention();return true;}
        for(int i=0;i<WarStates.Length;i++)if(Hit(WarStateRect(i),at)){BeginWarState(WarStates[i]);return true;}
        return inside;   // 視窗 MouseEvent="Dummy"：窗內點擊不傳給世界
    }

    // ---- T316（#746）：關注血盟與 ChangeWarState ----
    /// <summary>CheckFocusPledge（sub_B2B910 → sub_B2B8D0）：只切換該列的勾選，不送包。</summary>
    public bool ToggleAttention(string name){
        if(!attention.Contains(name,StringComparer.OrdinalIgnoreCase))return false;
        if(!checkedAttention.Remove(name))checkedAttention.Add(name);
        return true;
    }
    void BeginSideInput(SideInputKind kind,int prompt){editing=false;draft.Clear();popupMember=null;CancelMemoEdit();sideInput=kind;sideDraft.Clear();status=PromptText(prompt);}
    public void CancelSideInput(){sideInput=SideInputKind.None;sideDraft.Clear();}
    /// <summary>AddFocusPledge（sub_B2B040）：開輸入框，提示 3361。</summary>
    public bool BeginAddAttention(){
        if(!Visible)return false;
        if(Source==null){status="沒有接上遊戲連線，關注血盟未送出。";return false;}
        BeginSideInput(SideInputKind.AddFocus,ClanProtocol.AttentionInputText);return true;
    }
    /// <summary>DelFocusPledge（sub_B2C4D0）：開確認框（字串 4118，本機缺，用編號占位）；Enter 確定、Esc 取消。</summary>
    public bool BeginDeleteAttention(){
        if(!Visible)return false;
        if(Source==null){status="沒有接上遊戲連線，關注血盟未送出。";return false;}
        if(CheckedAttention.Count==0){status="請先勾選要刪除的關注血盟。";return false;}
        BeginSideInput(SideInputKind.DeleteConfirm,ClanProtocol.AttentionDeleteConfirmText);
        status+="（"+string.Join("、",CheckedAttention)+"）Enter 確定、Esc 取消。";return true;
    }
    /// <summary>ChangeWarState（sub_B2B830 → sub_B2B730）：1 宣戰（先查 478／785，再開 577）、2 停戰（579）、3 投降（578）；後兩者沒有本機檢查。</summary>
    public bool BeginWarState(int value){
        if(!Visible||ClanWarProtocol.WindowState(value) is not {} command)return false;
        if(Source is not {} s){status="沒有接上遊戲連線，盟戰請求未送出。";return false;}
        if(command==ClanWarCommand.Declare&&!s.WarPrecheck()){CancelSideInput();status="不能宣戰（原因見聊天欄）。";return false;}
        BeginSideInput(SideInputKind.War,ClanWarProtocol.InputText(command));sideWar=command;return true;
    }
    /// <summary>輸入框／確認框的「確定」：新增送子碼 0（名稱非空才送）；刪除每個勾選的血盟各送一包子碼 1；盟戰送 C227。</summary>
    public bool SubmitSideInput(){
        var kind=sideInput;string text=sideDraft.ToString().Trim();
        if(kind==SideInputKind.None)return false;
        if(Source is not {} s){CancelSideInput();status="沒有接上遊戲連線。";return false;}
        switch(kind){
        case SideInputKind.AddFocus:
            if(text.Length==0){CancelSideInput();status="";return false;}   // sub_B2AF90：名稱空白不送
            CancelSideInput();
            if(s.AddAttention(text)){status=$"已送出關注「{text}」；清單以伺服器回送為準。";return true;}
            status="目前不能送出關注請求。";return false;
        case SideInputKind.DeleteConfirm:{
            var names=CheckedAttention;CancelSideInput();
            if(names.Count==0){status="";return false;}
            // 原版送出後也把這幾列從清單拿掉；本窗的清單只照伺服器 S200 type 2 顯示，所以等伺服器回送新清單。
            if(s.RemoveAttention(names)){foreach(var n in names)checkedAttention.Remove(n);status=$"已送出刪除 {names.Count} 個關注血盟；清單以伺服器回送為準。";return true;}
            status="目前不能送出刪除請求。";return false;}
        default:{
            var command=sideWar;CancelSideInput();
            if(text.Length==0){status="";return false;}
            string label=command switch{ClanWarCommand.Declare=>"宣戰",ClanWarCommand.Surrender=>"投降",_=>"停戰"};
            if(s.ChangeWar(command,text)){status=$"已送出{label}（{text}）；結果以伺服器回報為準。";return true;}
            status=$"{label}未送出（原因見聊天欄）。";return false;}
        }
    }
    static bool Hit(Rectangle r,Point p)=>!r.IsEmpty&&r.Contains(p);
    public void ScrollBy(int rows)=>scroll=Math.Clamp(scroll+rows,0,MaxScroll);

    /// <summary>第 row 個可見列的階級欄下方開彈出窗（位置推測），整個彈出窗限制在視窗內。</summary>
    void OpenRankPopup(int row){
        if(Current is not {} w)return;
        var cell=Cell(row,"Rank_Label");
        int width=rankButtons.Max(b=>b.At.X+b.Rect.Width),height=rankButtons.Max(b=>b.At.Y+b.Rect.Height);
        popupAt=new(Math.Clamp(cell.X-origin.X,0,Math.Max(0,w.Size.Width-width)),Math.Clamp(cell.Bottom-origin.Y,0,Math.Max(0,w.Size.Height-height)));
        popupMember=Members[scroll+row];
    }

    // ---- R4-E 動作：本機只擋得到的條件（有沒有接上、字數與字元），身分與權限交給伺服器（盟主才能改公告／階級／盟徽）。----
    /// <summary>WriteNotice：以目前公告當草稿開始編輯（只在有 Noticebutton_Control 的公告窗）。</summary>
    public bool BeginNoticeEdit(){
        if(!Visible||!showNotice||NoticeEditRect.IsEmpty)return false;
        CancelMemoEdit();editing=true;draft.Clear();draft.Append(Notice.Length>NoticeLimit?Notice[..NoticeLimit]:Notice);return true;
    }
    // ---- R7-K（T350／#813）：ModifyMemo → C78 16 ----
    /// <summary>備註文字能不能送：C78 16 的字元規則（ClanMatchingProtocol.Notes）加上 169 回送的 62 byte 上限。不合回錯誤訊息。</summary>
    public static string? MemoError(string text){
        try{ClanMatchingProtocol.Notes(text);}catch(ArgumentException ex){return ex.Message.Replace("成員備註","備註");}
        return LoginPackets.TextEncoding.GetByteCount(text)>MemoByteLimit?$"備註最多 {MemoByteLimit} byte（約 {MemoByteLimit/2} 個中文字）。":null;
    }
    /// <summary>ModifyMemo（第一次按）：以自己目前的備註當草稿開始編輯；自己不在名單時仍可編輯，草稿畫在側欄輸入框。</summary>
    public bool BeginMemoEdit(){
        if(!Visible||ModifyMemoRect.IsEmpty)return false;
        if(Source==null){status="沒有接上遊戲連線，備註未送出。";return false;}
        if(string.IsNullOrEmpty(characterName)){status="不知道自己的角色名稱，不能寫備註。";return false;}
        editing=false;draft.Clear();popupMember=null;CancelSideInput();
        memoEditing=true;memoDraft.Clear();
        string current=data?.Memo(characterName)??"";
        if(MemoError(current)==null)memoDraft.Append(current);
        // 自己那列不在畫面上時捲過去（推測：原版行為沒查）。
        if(OwnIndex is >=0 and var k&&Current is {} w&&(k<scroll||k>=scroll+w.Rows))scroll=Math.Clamp(k,0,MaxScroll);
        status=$"輸入備註（最多 {MemoByteLimit} byte）；Enter 或再按備註鈕送出、Esc 取消。";
        return true;
    }
    /// <summary>ModifyMemo（確定）：送 C78 16；送出後結束編輯，等伺服器回送 169 才更新備註欄。</summary>
    public bool SaveMemo(){
        if(!memoEditing)return false;
        string text=memoDraft.ToString();
        if(MemoError(text) is {} error){status=error;return false;}
        if(Source is not {} s){status="沒有接上遊戲連線，備註未送出。";return false;}
        if(!s.SendMemo(ObsceneFilter.Apply(text))){status="目前不能送出備註（沒有血盟、正在送其他請求或角色狀態不允許）。";return false;}
        CancelMemoEdit();status="已送出成員備註；結果以伺服器回送為準。";return true;
    }
    public void CancelMemoEdit(){memoEditing=false;memoDraft.Clear();}
    /// <summary>
    /// T423（#967）：ModifyMemo → 聊天輸入列提示 3325（sub_B341F0）。有聊天窗、接上連線、知道自己的角色名才問；回傳 false＝沒問
    /// （呼叫端改用 R7-K 窗內編輯）。提示在離開世界或換角色後消失。輸入列一開始是空的（推測：原版沒帶入舊備註）。
    /// </summary>
    public bool AskMemo(){
        if(!Visible||ModifyMemoRect.IsEmpty||Source==null||string.IsNullOrEmpty(characterName)||ChatLineAsk.Chat(find) is not {} chat)return false;
        string me=characterName;
        editing=false;draft.Clear();popupMember=null;CancelSideInput();CancelMemoEdit();
        ChatLineAsk.Ask(chat,("pledge-memo",me),MemoPromptText,AnswerMemo,()=>inWorld&&characterName==me);
        status=MemoPromptText;return true;
    }
    /// <summary>
    /// T423：聊天輸入列的回答（sub_B34270，Enter）。空白＝不送；超過 60 byte＝3328 印在聊天欄、不送；其他送 C78 16，畫面等 169。回傳是否送出。
    /// </summary>
    public bool AnswerMemo(string typed){
        if(typed.Length==0){status="";return false;}
        if(MemoPromptError(typed) is {} error){
            status=error;
            if(error==MemoTooLongText)Source?.MemoNotice(error);
            return false;
        }
        if(Source is not {} s){status="沒有接上遊戲連線，備註未送出。";return false;}
        if(!s.SendMemo(ObsceneFilter.Apply(typed))){status="目前不能送出備註（沒有血盟、正在送其他請求或角色狀態不允許）。";return false;}
        status="已送出成員備註；結果以伺服器回送為準。";return true;
    }
    /// <summary>SaveNotice：送 C78 15；送出後結束編輯，畫面等伺服器回送 168 才更新（不先改本機公告）。</summary>
    public bool SaveNotice(){
        if(!editing)return false;
        string text=draft.ToString();
        try{ClanMatchingProtocol.Announce(text);}catch(ArgumentException ex){status=ex.Message;return false;}
        if(Source is not {} s){status="沒有接上遊戲連線，公告未送出。";return false;}
        if(!s.SendNotice(text)){status="目前不能送出（沒有血盟、正在送其他請求或角色狀態不允許）。";return false;}
        editing=false;draft.Clear();status="已送出血盟公告；結果以伺服器回送為準。";return true;
    }
    public void CancelNoticeEdit(){editing=false;draft.Clear();}
    bool SetRank(string member,byte rank){
        string label=ClanProtocol.RankName(rank);
        if(Source?.SetRank(member,rank)==true){status=$"已送出：{member} 設為{label}；結果以伺服器回報為準。";return true;}
        status=Source==null?"沒有接上遊戲連線，階級未送出。":"目前不能設定階級（沒有血盟或正在送其他請求）。";return false;
    }
    bool ToggleShowEmblem(){
        bool next=Source?.ShowEmblem!=true;
        if(Source?.SetShowEmblem(next)==true){status=(next?"已送出開啟":"已送出關閉")+"顯示盟徽；只有盟主能改，結果以伺服器回送為準。";return true;}
        status=Source==null?"沒有接上遊戲連線。":"目前不能設定顯示盟徽。";return false;
    }
    bool UploadEmblem(){
        if(Source?.UploadEmblem()==true){status="請選擇 16×12 盟徽 BMP。";return true;}
        status=Source==null?"沒有接上遊戲連線。":"目前不能上傳盟徽。";return false;
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(editing){
            // 編輯中：Esc 放棄、Enter 存檔（推測；Notice_Edit 的 Enter 行為沒查）、Backspace 刪字；可列印鍵要走 WM_CHAR → Char()。
            if(!e.Down)return e.KeyCode is Keys.Enter or Keys.Escape or Keys.Back;
            if(e.KeyData==Keys.Escape){if(!e.Repeat)CancelNoticeEdit();return true;}
            if(e.KeyData==Keys.Enter){if(!e.Repeat)SaveNotice();return true;}
            if(e.KeyData==Keys.Back){if(draft.Length>0)draft.Length--;return true;}
            return false;
        }
        if(memoEditing){
            // R7-K：備註編輯：Esc 取消、Enter 送出、Backspace 刪字（推測：與公告編輯相同；原版 ModifyMemo 的按鍵沒查）。
            if(!e.Down)return e.KeyCode is Keys.Enter or Keys.Escape or Keys.Back;
            if(e.KeyData==Keys.Escape){if(!e.Repeat){CancelMemoEdit();status="";}return true;}
            if(e.KeyData==Keys.Enter){if(!e.Repeat)SaveMemo();return true;}
            if(e.KeyData==Keys.Back){if(memoDraft.Length>0)memoDraft.Length--;return true;}
            return false;
        }
        if(sideInput!=SideInputKind.None){
            // T316：側欄輸入框／確認框：Enter 確定、Esc 取消、Backspace 刪字（推測：與公告編輯相同；原版共用輸入框的按鍵沒查）。
            if(!e.Down)return e.KeyCode is Keys.Enter or Keys.Escape or Keys.Back;
            if(e.KeyData==Keys.Escape){if(!e.Repeat){CancelSideInput();status="";}return true;}
            if(e.KeyData==Keys.Enter){if(!e.Repeat)SubmitSideInput();return true;}
            if(e.KeyData==Keys.Back){if(sideDraft.Length>0)sideDraft.Length--;return true;}
            return false;
        }
        if(e.KeyCode!=CloseKey||e.Modifiers!=Keys.None)return false;
        if(e.Down&&!e.Repeat){if(popupMember!=null)popupMember=null;else Close();}
        return true;
    }
    /// <summary>公告編輯中的字元（LimitText 160；代理字元不收，3.81 C_PledgeContent 會拒絕）；T316 側欄輸入框的字元（最多 16 字）。</summary>
    public bool Char(char c){
        if(Visible&&memoEditing){
            // R7-K：超過 62 byte 或不能以 Big5 編碼的字不收（MemoError 擋）。
            if(char.IsControl(c))return false;
            if(!char.IsSurrogate(c)&&MemoError(memoDraft.ToString()+c)==null)memoDraft.Append(c);
            return true;
        }
        if(Visible&&!editing&&sideInput is SideInputKind.AddFocus or SideInputKind.War){
            if(char.IsControl(c))return false;
            if(!char.IsSurrogate(c)&&sideDraft.Length<SideInputLimit)sideDraft.Append(c);
            return true;
        }
        if(!Visible||!editing||char.IsControl(c))return false;
        if(!char.IsSurrogate(c)&&draft.Length<NoticeLimit)draft.Append(c);
        return true;
    }

    public bool IsDragHandle(Point location)=>Visible&&Current is {} w&&WindowBounds.Contains(location)&&location.Y-origin.Y<w.DragHeight&&!Hit(CloseRect,location);
    public void MoveTo(Point location)=>origin=location;
}

/// <summary>W2-H 離線測試：合成封包（照 3.81 S_Pledge／S_PacketBox 欄位）與合成 XML／surf，不讀客戶端、不連線。</summary>
static class PledgePanelTests {
    const string Xml="""
<?xml version="1.0"?>
<PledgeUI Toggle="true">
  <Window Name = "ChangeRank_PopupWindow" X = "0" Y = "0" Width="100" Height="73"/>
  <Window Name = "Noticeuserinfo_Window" X = "0" Y = "0" BaseSurf = "4512;1" FrameSurf = "4487;1" Moveable = "20">
    <Button Name = "Closeui_Button" X = "260" Y = "13" BaseSurf = "4429" PushSurf = "4430"/>
    <Control Name = "Pledgename_Control" X = "19" Y = "25" Width = "189" Height = "20">
      <Label Name = "Pledge_Label" X = "17" Y="-2" Width = "169" Height = "20"/>
    </Control>
    <Button Name = "Pledgenotice_Button" X = "16" Y = "41" BaseSurf = "4455" PushSurf = "4455">
      <Button Name = "Noticetri_Button" X = "200" Y = "0" BaseSurf = "4456"/>
    </Button>
    <Control Name = "Notice_Control" X = "21" Y = "63" Width = "225" Height = "112">
      <MultiLabel Name = "Notice_Label" Width = "220" Height = "109" VAlign = "top" HAlign = "left"/>
      <MultiEdit Name = "Notice_Edit" Width = "225" Height = "109" LimitText="160"  LimitLine = "9"/>
    </Control>
    <List Name = "Sort_List" X = "16" Y = "181" Width = "208" Height = "21" Horizontal = "1">
      <Button Name = "Classsort_Button" BaseSurf = "4458"/>
      <Button Name = "Namesort_Button" BaseSurf = "4460"/>
    </List>
    <List Name = "Userinfo_List" X = "21" Y = "202" Width = "214" Height = "222" Vertical = "1">
      <List Name = "Userinfo0_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1">
        <Image Name = "Class_Image" Surf = "4546;4" Width = "19"/>
        <Label Name = "Name_Label" Width = "74" Height = "15"/>
        <Label Name = "Rank_Label" Width = "35" Height = "15"/>
        <Label Name = "Memo_Label" Width = "97" Height = "15"/>
      </List>
      <List Name = "Userinfo1_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1"/>
    </List>
    <ScrollBar Name = "Userinfo_Scrollbar" X = "255" Y = "67" Width = "12" Height = "293" ScrollStart = "14" ScrollHeight = "262">
      <Image Name = "Background_Image" Y = "14" Surf = "4443;1"/>
      <Button Name = "Up_Button" BaseSurf = "4446;1"/>
      <Button Name = "Move_Button" Y = "14" BaseSurf = "4448;1"/>
      <Button Name = "Down_Button" Y = "277" BaseSurf = "4444;1"/>
    </ScrollBar>
  </Window>
  <Window Name = "Nonoticeuserinfo_Window" X = "0" Y = "0" BaseSurf = "4450;1" FrameSurf = "4451;1" Moveable = "20">
    <Button Name = "Closeui_Button" X = "260" Y = "13" BaseSurf = "4429" PushSurf = "4430"/>
    <Control Name = "Pledgename_Control" X = "19" Y = "25" Width = "189" Height = "20">
      <Label Name = "Pledge_Label" X = "17" Y = "-2" Width = "169" Height = "20"/>
    </Control>
    <Button Name = "Pledgenotice_Button" X = "16" Y = "41" BaseSurf = "4455" PushSurf = "4455">
      <Button Name = "Noticetri_Button" X = "200" Y = "0" BaseSurf = "4494"/>
    </Button>
    <List Name = "Sort_List" X = "16" Y = "61" Width = "208" Height = "21" Horizontal = "1">
      <Button Name = "Classsort_Button" BaseSurf = "4458"/>
      <Button Name = "Namesort_Button" BaseSurf = "4460"/>
    </List>
    <List Name = "Userinfo_List" X = "21" Y = "82" Width = "204" Height = "339" Vertical = "1">
      <List Name = "Userinfo0_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1">
        <Image Name = "Class_Image" Surf = "4546;4" Width = "19"/>
        <Label Name = "Name_Label" Width = "74" Height = "15"/>
        <Label Name = "Rank_Label" Width = "35" Height = "15"/>
        <Label Name = "Memo_Label" Width = "97" Height = "15"/>
      </List>
      <List Name = "Userinfo1_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1"/>
      <List Name = "Userinfo2_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1"/>
    </List>
    <ScrollBar Name = "Userinfo_Scrollbar" X = "255" Y = "67" Width = "12" Height = "293" ScrollStart = "14" ScrollHeight = "262">
      <Image Name = "Background_Image" Y = "14" Surf = "4443;1"/>
      <Button Name = "Up_Button" BaseSurf = "4446;1"/>
      <Button Name = "Move_Button" Y = "14" BaseSurf = "4448;1"/>
      <Button Name = "Down_Button" Y = "277" BaseSurf = "4444;1"/>
    </ScrollBar>
  </Window>
</PledgeUI>
""";

    static UiSurface Img(int surf,int w,int h)=>new(surf,surf+".img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF204060u,w*h).ToArray()));
    static readonly Dictionary<int,(int w,int h)> Sizes=new(){
        [4450]=(277,423),[4451]=(277,423),[4512]=(277,423),[4487]=(277,423),[4429]=(12,12),[4455]=(200,16),[4456]=(12,16),[4494]=(12,16),
        [4458]=(20,21),[4460]=(76,21),[4443]=(12,263),[4446]=(12,14),[4448]=(12,20),[4444]=(12,14),
    };
    static UiSurface? Get(int surf)=>Sizes.TryGetValue(surf,out var s)?Img(surf,s.w,s.h):null;
    static JsonElement Snap(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("pledgeWindow");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandledSubtypes").TryGetProperty(key,out var v)?v.GetInt32():0;
    static UiMouseEvent Click(Point p)=>new(UiMouseKind.Down,p,MouseButtons.Left,1,0,Keys.None);
    static IUiGameState World(PledgeWindowState? p,bool inWorld=true)=>new UiPledgeGameState(new UiStaticGameState(inWorld),p);

    [WorldModuleTest("pledge-panel")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：169 消化、171 只觀察（主分派的 Clan.cs 照常收）、快照與重置；登錄表無錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:169,Method:"WorldSession.ReceivePledgeMemo"}),"S250/169 registered");
        t.Check(entries.Any(e=>e is {Kind:"observe",Opcode:250,Subtype:171,Method:"WorldSession.ObservePledgeOnline"}),"S250/171 observed");
        t.Check(!entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:167 or 170 or 171}),"167/170/171 not consumed by extensions");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="pledgeWindow"),"snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetPledgeWindow"),"reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. 解碼：3.81 S_Pledge(name, notes) = FA A9 S(name) + 62 byte 零填滿。
        var memoPkt=PledgeProtocol.EncodeMemo("Alice","守城");
        t.Equal(72,memoPkt.Length,"169 = 2 + S(Alice)6 + 62, padded to 72");
        t.Equal("FAA9416C69636500",Convert.ToHexString(memoPkt,0,8),"169 header bytes");
        t.Equal(new PledgeMemo("Alice","守城"),PledgeProtocol.DecodeMemo(memoPkt),"decode 169");
        t.Equal(new PledgeMemo("Bob",""),PledgeProtocol.DecodeMemo(PledgeProtocol.EncodeMemo("Bob","")),"empty memo");
        var full=new string('a',62);
        t.Equal(full,PledgeProtocol.DecodeMemo(PledgeProtocol.EncodeMemo("Bob",full)).Memo,"62-byte memo without terminator");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeMemo([250,169,(byte)'A',0,1,2,3]),"truncated memo rejected");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeMemo(LoginPackets.Pad([250,169,0,..new byte[62]])),"blank name rejected");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeMemo(PledgeProtocol.EncodeOnline("A")),"wrong subcode rejected");
        t.Equal("FAAB0200416C6963650000426F620000",Convert.ToHexString(PledgeProtocol.EncodeOnline("Alice","Bob")),"171 synthetic bytes (C250 C171 H2, S C0 per member)");

        // 3. 會話：初始沒資料；169 不開窗；171 開窗並保留備註；171 仍交給 Clan.cs（不計 unhandled）。
        var s=new WorldSession();
        t.Check(s.PledgeWindow.Online==null&&s.PledgeWindow.OpenRevision==0,"initially no window data",s.PledgeWindow);
        s.Receive(PledgeProtocol.EncodeMemo("Alice","守城"),"");
        t.Equal(0,Unhandled(s,"0xFA/0xA9"),"169 consumed");
        t.Check(s.PledgeWindow is {Online:null,OpenRevision:0},"169 alone does not open",s.PledgeWindow);
        t.Equal("守城",s.PledgeWindow.Memo("alice"),"memo stored case-insensitively");
        s.Receive(PledgeProtocol.EncodeOnline("Alice","Bob"),"");
        t.Equal(0,Unhandled(s,"0xFA/0xAB"),"171 still handled by main dispatch");
        var pw=s.PledgeWindow;
        t.Check(pw.OpenRevision==1&&pw.Online is ["Alice","Bob"],"171 opens with online list",pw);
        t.Equal("守城",pw.Memo("Alice"),"memo kept after 171");
        t.Equal("",pw.Memo("Bob"),"no memo → blank");
        s.Receive(PledgeProtocol.EncodeMemo("Bob","補師"),"");
        t.Equal("補師",s.PledgeWindow.Memo("Bob"),"169 updates memo");
        t.Equal(1L,s.PledgeWindow.OpenRevision,"169 does not bump open revision");
        var snap=Snap(s);
        t.Equal(2,snap.GetProperty("online").GetArrayLength(),"snapshot online");
        t.Equal("補師",snap.GetProperty("memos").GetProperty("Bob").GetString(),"snapshot memo");
        // 壞的 171：觀察端不開窗，主分派照原樣報錯。
        t.Throws<ProtocolException>(()=>s.Receive([250,171,1,0,0,0,0,0],""),"bad 171 still rejected by main dispatch");
        t.Equal(1L,s.PledgeWindow.OpenRevision,"bad 171 does not open");
        t.Throws<ProtocolException>(()=>s.Receive([250,169,0,0,0,0,0,0],""),"bad 169 rejected");
        s.Receive(PledgeProtocol.EncodeOnline(),"");
        t.Check(s.PledgeWindow is {OpenRevision:2,Online.Count:0},"empty 171 opens with no members",s.PledgeWindow);
        s.Reset();
        t.Check(s.PledgeWindow is {Online:null} p0&&p0.Memos.Count==0,"Reset clears",s.PledgeWindow);

        // 4. 版面：合成 XML 載入兩個視窗，座標等於 XML（相對視窗）。
        var layout=UiLayout.Parse(Xml,"PledgeUI.xml");
        var panel=new PledgePanel();panel.Load(layout,Get);
        t.Check(!panel.Visible,"not visible before 171");
        panel.Update(World(new PledgeWindowState(0,0,"",["X"],new Dictionary<string,string>())));
        t.Check(!panel.Visible,"OpenRevision 0 does not open");
        var data=new PledgeWindowState(1,1,"紅血盟",["Alice","Bob","Carol","Dave","Eve"],new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["bob"]="補師"});
        panel.Update(World(data));
        t.Check(panel.Visible&&!panel.ShowingNotice,"171 opens the no-notice window");
        t.Equal(new Rectangle(0,0,277,423),panel.WindowBounds,"window bounds = base 4450 at XML (0,0)");
        t.Equal(new Rectangle(260,13,12,12),panel.CloseRect,"close button (260,13)");
        t.Equal(new Rectangle(16,41,200,16),panel.NoticeRect,"notice bar (16,41)");
        t.Equal(new Rectangle(216,41,12,16),panel.NoticeArrowRect,"notice arrow relative to parent (16+200,41)");
        var w=panel.Current!;
        t.Equal(new Rectangle(36,23,169,20),w.PledgeLabel,"pledge label = control (19,25) + label (17,-2)");
        t.Equal(new Point(16,61),w.SortButtons[0].At,"sort button 0");
        t.Equal(new Point(36,61),w.SortButtons[1].At,"sort button 1 follows surf width");
        t.Equal(3,w.Rows,"rows from XML");
        t.Equal(new Rectangle(21+20,82,74,15),panel.Cell(0,"Name_Label"),"row 0 name column (class 19 + gap 1)");
        t.Equal(new Rectangle(21+131,82+15*2,97,15),panel.Cell(2,"Memo_Label"),"row 2 memo column");
        t.Equal(new Rectangle(255,67,12,14),panel.ScrollUpRect,"scroll up");
        t.Equal(new Rectangle(255,67+277,12,14),panel.ScrollDownRect,"scroll down");
        t.Equal(67+14,panel.ThumbY,"thumb at ScrollStart when scroll=0");

        // 5. 捲動：5 人、3 列 → 最多捲 2；拉桿到底。
        var cmd=new UiNullCommands();
        t.Equal(2,panel.MaxScroll,"max scroll");
        t.Check(panel.Mouse(Click(new(260,350)),cmd)&&panel.Scroll==1,"down button scrolls");
        panel.Mouse(new(UiMouseKind.Wheel,new(100,100),MouseButtons.None,0,-120,Keys.None),cmd);
        t.Equal(2,panel.Scroll,"wheel down scrolls");
        panel.ScrollBy(5);t.Equal(2,panel.Scroll,"scroll clamped");
        t.Equal(67+14+(262-20),panel.ThumbY,"thumb at bottom");
        t.Check(panel.Mouse(Click(new(260,70)),cmd)&&panel.Scroll==1,"up button scrolls");

        // 6. 畫面：只畫在 Bounds 內，缺 surf（4430 等）不畫也不丟例外。
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(panel.Bounds);panel.Paint(canvas);canvas.Release();
            t.Check(canvas.Bitmap.GetPixel(5,5).ToArgb()==Color.FromArgb(255,0x20,0x40,0x60).ToArgb(),"base surf painted at origin");
            t.Check(canvas.Bitmap.GetPixel(400,500).ToArgb()==Color.Black.ToArgb(),"nothing outside bounds");
        }

        // 7. 公告鈕切換視窗；拖曳帶；窗內點擊被吞；窗外不吞。
        t.Check(panel.Mouse(Click(new(20,45)),cmd)&&panel.ShowingNotice,"notice button switches to notice window");
        t.Equal(2,panel.Current!.Rows,"notice window rows");
        t.Equal(1,panel.Scroll,"scroll clamped to notice window");
        t.Check(panel.Mouse(Click(new(220,45)),cmd)&&!panel.ShowingNotice,"arrow switches back");
        t.Check(panel.IsDragHandle(new(100,10)),"top 20 px is drag handle");
        t.Check(!panel.IsDragHandle(new(100,25)),"below strip is not drag handle");
        t.Check(!panel.IsDragHandle(new(262,15)),"close button is not drag handle");
        panel.MoveTo(new(100,50));
        t.Equal(new Rectangle(100,50,277,423),panel.WindowBounds,"moved");
        t.Equal(new Rectangle(360,63,12,12),panel.CloseRect,"close follows window");
        t.Check(panel.Mouse(Click(new(150,200)),cmd),"click inside swallowed");
        t.Check(!panel.Mouse(Click(new(10,10)),cmd),"click outside not handled");
        t.Equal(0,cmd.Calls.Count,"no outward commands");

        // 8. 關閉：關閉鈕／Esc；同一個 OpenRevision 不再開；下一個 171 重開並捲回頂端。
        t.Check(panel.Mouse(Click(new(362,65)),cmd)&&!panel.Visible,"close button closes");
        panel.Update(World(data));
        t.Check(!panel.Visible,"same revision stays closed");
        panel.Update(World(data with{OpenRevision=2}));
        t.Check(panel.Visible&&panel.Scroll==0,"next 171 reopens at top");
        t.Check(!panel.Key(new(Keys.Escape|Keys.Shift,true,false),cmd),"shift+esc ignored");
        t.Check(panel.Key(new(Keys.Escape,true,false),cmd)&&!panel.Visible,"esc closes");
        t.Check(!panel.Key(new(Keys.Escape,true,false),cmd),"esc ignored when closed");

        // 9. 離開世界／沒有資料就關；穿過 HpMp／巨集包裝也找得到資料。
        panel.Update(World(data with{OpenRevision=3}));
        t.Check(panel.Visible,"reopened");
        panel.Update(World(data with{OpenRevision=3},inWorld:false));
        t.Check(!panel.Visible,"leaving world hides");
        panel.Update(World(null));
        t.Check(!panel.IsOpen,"no data closes");
        var wrapped=new UiHpMpGameState(World(data with{OpenRevision=4}),[]);
        t.Equal(4L,UiPledgeGameState.Of(wrapped)?.OpenRevision,"found through UiHpMpGameState");
        t.Equal((PledgeWindowState?)null,UiPledgeGameState.Of(new UiStaticGameState(true)),"plain state has no pledge data");

        // 10. 版面錯誤：缺視窗、壞 surf 都丟 UiLayoutException（不補預設座標）。
        t.Throws<UiLayoutException>(()=>new PledgePanel().Load(UiLayout.Parse("<PledgeUI/>","PledgeUI.xml"),Get),"missing windows rejected");
        t.Throws<UiLayoutException>(()=>new PledgePanel().Load(UiLayout.Parse("<Other/>","x.xml"),Get),"wrong root rejected");
        t.Throws<UiLayoutException>(()=>new PledgePanel().Load(UiLayout.Parse(Xml.Replace("BaseSurf = \"4450;1\"","BaseSurf = \";1\""),"PledgeUI.xml"),Get),"empty surf rejected");
        t.Throws<UiLayoutException>(()=>new PledgePanel().Load(UiLayout.Parse(Xml.Replace("Name = \"Notice_Control\"","Name = \"Other_Control\""),"PledgeUI.xml"),Get),"notice window without Notice_Control rejected");

        // 11. T248（PBW-C）S250/168 公告：3.81 S_PacketBox(168, announce) = FA A8 S(公告) + 補齊。
        t.Check(entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:168,Method:"WorldSession.ReceivePledgeNotice"}),"S250/168 registered");
        var noticePkt=PledgeProtocol.EncodeNotice("今晚攻城");
        t.Equal(16,noticePkt.Length,"168 = 2 + S(8 Big5 bytes + NUL) = 11, padded to 16");
        t.Equal("FAA8",Convert.ToHexString(noticePkt,0,2),"168 header bytes");
        t.Equal("今晚攻城",PledgeProtocol.DecodeNotice(noticePkt),"decode 168");
        t.Equal("",PledgeProtocol.DecodeNotice(PledgeProtocol.EncodeNotice("")),"empty notice");
        t.Equal("a\nb",PledgeProtocol.DecodeNotice(PledgeProtocol.EncodeNotice("a\nb")),"newline kept");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeNotice([250,168,(byte)'A',(byte)'B']),"unterminated notice rejected");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeNotice([250,168,(byte)'A',0,0,0,0,0,1,0,0,0,0,0,0,0]),"trailing garbage rejected");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeNotice(PledgeProtocol.EncodeMemo("A","")),"wrong subcode rejected (169)");

        var s2=new WorldSession();
        t.Equal((string?)null,s2.PledgeWindow.Notice,"initially no notice");
        s2.Receive(PledgeProtocol.EncodeNotice("今晚攻城"),"");
        t.Equal(0,Unhandled(s2,"0xFA/0xA8"),"168 consumed");
        t.Check(s2.PledgeWindow is {Online:null,OpenRevision:0,Notice:"今晚攻城"},"168 stores notice without opening",s2.PledgeWindow);
        s2.Receive(PledgeProtocol.EncodeOnline("Alice"),"");
        t.Equal("今晚攻城",s2.PledgeWindow.Notice,"notice kept after 171");
        long rev=s2.PledgeWindow.Revision;
        s2.Receive(PledgeProtocol.EncodeNotice("改公告"),"");
        t.Check(s2.PledgeWindow is {Notice:"改公告",OpenRevision:1}&&s2.PledgeWindow.Revision==rev+1,"168 replaces notice, bumps revision only",s2.PledgeWindow);
        t.Equal("改公告",Snap(s2).GetProperty("notice").GetString(),"snapshot notice");
        s2.Receive(PledgeProtocol.EncodeNotice(""),"");
        t.Equal("",s2.PledgeWindow.Notice,"empty 168 clears text");
        t.Throws<ProtocolException>(()=>s2.Receive([250,168,(byte)'A',(byte)'B',(byte)'C',(byte)'D',(byte)'E',(byte)'F'],""),"bad 168 rejected");
        s2.Reset();
        t.Equal((string?)null,s2.PledgeWindow.Notice,"Reset clears notice");

        // 分行：換行字元分段、逐字折行、最多 maxLines 行。
        Func<char,int> w6=_=>6;
        t.Equal("abc|de",string.Join("|",PledgePanel.NoticeLines("abcde",18,w6,9)),"wrap at width");
        t.Equal("ab||c",string.Join("|",PledgePanel.NoticeLines("ab\r\n\nc",60,w6,9)),"line breaks kept (\\r\\n and blank lines)");
        t.Equal(9,PledgePanel.NoticeLines(new string('x',160),36,w6,9).Count,"limited to 9 lines");
        t.Equal(0,PledgePanel.NoticeLines("",60,w6,9).Count,"empty → no lines");

        // 版面與畫面：公告區只在公告窗；有公告時畫在 Notice_Label 內，沒有就留白。
        var np=new PledgePanel();np.Load(layout,Get);
        var nd=new PledgeWindowState(1,1,"紅血盟",["Alice"],new Dictionary<string,string>(),"今晚攻城\n全員集合");
        np.Update(World(nd));
        t.Equal(Rectangle.Empty,np.NoticeTextRect,"no-notice window has no notice area");
        t.Equal("今晚攻城\n全員集合",np.Notice,"panel exposes notice");
        np.Mouse(Click(new(20,45)),cmd);
        t.Check(np.ShowingNotice,"switched to notice window");
        t.Equal(new Rectangle(21,63,220,109),np.NoticeTextRect,"notice label = control (21,63) + label (0,0), 220×109");
        Rectangle area=np.NoticeTextRect;
        int Lit(PledgeWindowState st){
            np.Update(World(st));
            using var c=new UiCanvas();c.Clear(Color.Black);c.Restrict(np.Bounds);np.Paint(c);c.Release();
            int n=0;
            for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(c.Bitmap.GetPixel(x,y).ToArgb()==Color.White.ToArgb())n++;
            return n;
        }
        int withText=Lit(nd),blank=Lit(nd with{Revision=2,Notice=null});
        t.Check(withText>0&&blank==0,"notice text painted inside Notice_Label only when present",new{withText,blank});
    }
}

}
