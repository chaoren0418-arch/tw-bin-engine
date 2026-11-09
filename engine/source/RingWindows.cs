using System.Globalization;
using P06.NativeUi;
namespace P06;

// T276（#667）：控制戒指開窗（S36 type 1／5）與盟屋地圖 674 鈕的送出端與 ClientForm 轉接。
// 依據：docs/reference/followups-3.md 第 3、4 節。開窗判斷在 AbilityStatus.cs（WorldSession.TryOpenRingWindow），
// 本檔只把「選定之後」接到既有送包路徑：
//   - 傳送目的地：選定書籤 → 既有 CastSelectedMagic，目標 SkillTargetKind.Bookmark（3.81 C6 的 HD＝書籤地圖、書籤 id）。
//     不照抄 TW16 sub_BE0F10 的 hhh（地圖、x、y）：3.81 C_UseSkill 對技能 5／69 讀 H＋D 並以書籤 id 查 CharBookTable。
//   - 召喚：沿用既有本機召喚請求（Pets.cs SendSummonChoice，C6 D(action)），送出前的 743 等級檢查在 AbilityStatus.cs。
//   - 674：C125 D(S187 物件 id) S("select") S(十進位盟屋編號)；回包 S39 agsel 由既有 NPC 對話處理（NpcInteraction.cs）。

/// <summary>盟屋地圖 674 鈕的封包（TW16 sub_542BF0 `cdss` 227 ＝ 3.81 C125 C_NPCAction，Npc_AuctionBoard 多讀一個 S）。</summary>
public static class AgitSelectPackets {
    public const string Action="select";
    /// <summary>C(125) D(board) S("select") S("%u" houseNumber)。houseNumber＝S187 原值（區域&lt;&lt;16 | 編號）。</summary>
    public static byte[] Select(uint board,uint houseNumber){
        if(board==0)throw new ArgumentException("拍賣看板物件 id 不可為 0",nameof(board));
        return LoginPackets.Pad([125,..BitConverter.GetBytes(board),..LoginPackets.TextEncoding.GetBytes(Action),0,
            ..LoginPackets.TextEncoding.GetBytes(houseNumber.ToString(CultureInfo.InvariantCulture)),0]);
    }
}

/// <summary>
/// R5-D（T309／#736，FU3§3.1 的第二個條件）：sub_E08A20 在 0xE08AF2–0xE08B20 檢查
/// 「職業碼 byte_1542AA1 == 2、等級 ≥ 50（0xE08B0E cmp 0x32）且 byte_13A931C」，成立時和戴傳送控制戒指一樣開書籤窗。
/// FU3 依 SS2§7 推定職業 2 是王族；FU6§2 已查證 byte_1542AA1＝選角清單的職業，0＝王族、2＝妖精，所以這是**妖精**分支。
/// byte_13A931C 是客戶端本機設定（sub_59DD30 載入的設定檔，設定物件 +9；sub_89A5F0／sub_999B50 由位元 0x08 寫入），
/// 等級 &lt; 50 時 0xEC99EB／0xEED7B9 把它清成 0。選項名稱與預設值沒查（引擎預設關閉，推定）。
/// 這條路徑（0xE08B22 起）不檢查限制地圖：276 只在直接送 C6 的路徑（0xE08BA9）。
/// 3.81 TELEPORT.java 以書籤 id 查 CharBookTable，不檢查戒指，所以送出照舊用 HD。
/// </summary>
public static class ElfBookmarkTeleportRules {
    public const int ElfClass=2,MinLevel=50;
    public static bool Allows(int? characterClass,int? level,bool option)=>option&&characterClass==ElfClass&&level>=MinLevel;
}

public sealed partial class WorldSession {
    /// <summary>byte_13A931C（本機選項，預設關閉）；只有妖精 Lv ≥ 50 時有作用。</summary>
    public bool ElfBookmarkTeleportOption {get;set;}
    /// <summary>傳送控制戒指路徑（AbilityStatus.cs）沒開窗時，再看妖精分支；回 true＝已開窗，不送 C6。</summary>
    internal bool TryOpenElfTeleportWindow(int? characterClass){
        if(SelectedMagicId is not int id||!learnedMagic.Contains(id)||!RingWindowRules.TeleportSkill(id))return false;
        if(!ElfBookmarkTeleportRules.Allows(characterClass,Status?.Level,ElfBookmarkTeleportOption))return false;
        lock(abilityLock){if(teleportDestination==null)teleportDestination=new(++teleportDestinationRevision,id);}
        MagicNotice="已開啟傳送目的地選擇（妖精 Lv 50 以上的記憶座標傳送選項）；選定書籤後才送出。";
        return true;
    }
}

public sealed partial class LoginConnection {
    /// <summary>自己的職業（選角清單 S_CHARACTER_INFO 的職業欄，FU6§2：byte_1542AA1 在進入世界時由它鎖定）。</summary>
    internal int? OwnCharacterClass=>World.Actor?.Name is {} name?characters.FirstOrDefault(c=>c.Name==name)?.Class:null;
    bool TryOpenRingWindow(){
        if(State!=LoginState.InWorld)return false;
        int? cls=OwnCharacterClass;
        return CaptureWorld(()=>World.TryOpenRingWindow()||World.TryOpenElfTeleportWindow(cls));
    }
    /// <summary>目前的選傳送目的地請求（不在世界中為 null）。</summary>
    public TeleportDestinationRequest? TeleportDestinationSnapshot()=>State==LoginState.InWorld?World.TeleportDestination:null;
    public bool CloseTeleportDestination(long revision)=>World.CloseTeleportDestination(revision);

    int teleportDestinationSending=-1;
    /// <summary>
    /// 對 revision 這一次的請求，以書籤 bookmarkId 施放一次（C6 HD）。送出成功才關窗；失敗保留窗，可再選一次。
    /// 請求過期、書籤不存在、已送出刪除或正在送出時不送。
    /// </summary>
    public async Task<bool> SendTeleportDestination(long revision,uint bookmarkId){
        if(State!=LoginState.InWorld||teleportDestinationSending==generation)return false;
        var target=CaptureWorld(()=>World.TeleportDestinationTarget(revision,bookmarkId));
        if(target==null)return false;
        int session=generation;teleportDestinationSending=session;
        try{
            bool cast=await CastSelectedMagic(target);
            if(cast&&session==generation)World.CloseTeleportDestination(revision);
            return cast&&session==generation;
        }
        finally{if(teleportDestinationSending==session)teleportDestinationSending=-1;}
    }

    /// <summary>674 鈕：對 shown 這一次的 S187 送一次 C125 select。S187 已被新的取代、互動送出中或動作鎖定時不送。</summary>
    public async Task<bool> SendAgitSelect(AgitMapOpenRequest shown){
        var map=shown.Map;
        if(State!=LoginState.InWorld||map.ObjectId==0||!map.InOriginalRange||World.AgitHouseMap!=map||interactionSending||MovementBusy
            ||sendMovementGate is not {} sender||World.ActionLocked||World.TradeBlocksActions)return false;
        byte[] packet=AgitSelectPackets.Select(map.ObjectId,map.HouseNumber);
        int session=generation;interactionSending=true;
        bool Valid()=>session==generation&&State==LoginState.InWorld&&World.AgitHouseMap==map&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy;
        try{return await sender(packet,Valid,()=>{})&&session==generation;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"盟屋拍賣查詢傳送失敗，已斷線；不會自動重送。");}return false;}
        finally{if(session==generation)interactionSending=false;}
    }
}

public sealed partial class ClientForm {
    long ringDestinationShown=-1;
    /// <summary>
    /// 由 BookmarkUi.cs 的計時器呼叫：接上 674 鈕與原生書籤窗的宿主、同步選目的地模式。
    /// R4-J（#713）：原生 UI 開啟且原生書籤窗可用時，選目的地由原生窗處理（面板自己在新請求時開窗），WinForms 分頁不進入該模式；
    /// 原生 UI 關閉（或書籤窗缺圖停用、出錯）時，WinForms「記憶座標」分頁進入選目的地模式，並在新請求時真的把分頁視窗叫出來。
    /// </summary>
    void SyncRingWindows(){
        if(NativeUiRoot?.Find(AgitMapPanel.PanelId) is AgitMapPanel agit&&agit.Host==null)agit.Attach(new AgitMapHost(this));
        var native=NativeBookmarkWindow();
        if(native!=null&&native.Host==null)native.Attach(new BookmarkWindowHost(this));
        if(bookmarkPanel is not {} panel||panel.IsDisposed)return;
        var q=login.TeleportDestinationSnapshot();
        panel.UpdateDestination(native==null?q:null);
        if(q!=null&&q.Revision!=ringDestinationShown){
            ringDestinationShown=q.Revision;
            if(native==null)ShowBookmarkTab(panel);
        }
    }
    /// <summary>可用的原生書籤窗（原生 UI 啟用、面板圖齊全、未因例外停用）；否則 null（走 WinForms 分頁）。</summary>
    BookmarkWindowPanel? NativeBookmarkWindow()=>
        NativeUiActive&&NativeUiRoot is {} root&&root.Find(BookmarkWindowPanel.PanelId) is BookmarkWindowPanel p&&p.Usable&&!root.Faulted.Contains(p)?p:null;
    /// <summary>切到「記憶座標」分頁並把分頁所在的視窗叫出來（PR #700 只切分頁，視窗沒開時看不到）。</summary>
    void ShowBookmarkTab(BookmarkPanel panel){
        if(panel.Parent is not TabPage page||normalPanels==null)return;
        normalPanels.SelectedTab=page;
        if(PlayerHudActive)ShowPlayerService(page.Text);
        else ShowDevTools(normalPanels);
    }
    sealed class BookmarkWindowHost(ClientForm form):IBookmarkWindowHost {
        bool sending;
        public BookmarkView Bookmarks{get{var view=form.login.BookmarkSnapshot();return view with{CanEdit=view.CanEdit&&!form.World.Walking};}}
        public TeleportDestinationRequest? Destination=>form.login.TeleportDestinationSnapshot();
        public bool Sending=>sending;
        public void Teleport(TeleportDestinationRequest shown,uint bookmarkId){
            if(sending||form.World.Walking)return;
            sending=true;form.ClearInput("bookmark-teleport");_ = Run();
            async Task Run(){try{await form.login.SendTeleportDestination(shown.Revision,bookmarkId);}finally{sending=false;}}
        }
        public void CancelTeleport(TeleportDestinationRequest shown)=>form.login.CloseTeleportDestination(shown.Revision);
    }
    sealed class AgitMapHost(ClientForm form):IAgitMapHost {
        bool sending;
        public void Select(AgitMapOpenRequest shown){
            if(sending||form.World.Walking)return;
            sending=true;form.ClearInput("agit-select");_ = Run();
            async Task Run(){try{await form.login.SendAgitSelect(shown);}finally{sending=false;}}
        }
    }
}
