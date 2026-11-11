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
    public const string Action="select",ApplyAction="apply";
    /// <summary>C(125) D(board) S("select") S("%u" houseNumber)。houseNumber＝S187 原值（區域&lt;&lt;16 | 編號），或 S156 的盟屋 id（同一種值）。</summary>
    public static byte[] Select(uint board,uint houseNumber)=>Board(board,Action,houseNumber);
    /// <summary>
    /// R7-C（#805）：拍賣資訊窗的 Bid 鈕。C(125) D(board) S("apply") S("%u" houseNumber)；3.81 Npc_AuctionBoard 的 "apply" 分支
    /// 回 S_ApplyAuction（S136 agapply），或 518／519／521 訊息。原版鈕是否送這個是推測（伺服器只認這個動作名）。
    /// </summary>
    public static byte[] Apply(uint board,uint houseNumber)=>Board(board,ApplyAction,houseNumber);
    static byte[] Board(uint board,string action,uint houseNumber){
        if(board==0)throw new ArgumentException("拍賣看板物件 id 不可為 0",nameof(board));
        return LoginPackets.Pad([125,..BitConverter.GetBytes(board),..LoginPackets.TextEncoding.GetBytes(action),0,
            ..LoginPackets.TextEncoding.GetBytes(houseNumber.ToString(CultureInfo.InvariantCulture)),0]);
    }
}

/// <summary>
/// T434（#992，classic-ui-audit G10）：classic「盟屋拍賣」分頁的列文字與「新請求才叫出分頁」的判斷（拆出來給離線測試）。
/// 列格式（推測，原生清單的欄位同樣是推測）：名稱、面積、截止月/日、價格。
/// </summary>
public sealed class ClassicAgitTracker {
    long? list,map;
    /// <summary>S156 或 S187 有新的 Revision（不是 null、跟上次不同）→ true（要把分頁叫出來）。同一筆請求不再叫。</summary>
    public bool Observe(long? listRevision,long? mapRevision){
        bool fresh=listRevision is long l&&l!=list||mapRevision is long m&&m!=map;
        if(listRevision!=null)list=listRevision;
        if(mapRevision!=null)map=mapRevision;
        return fresh;
    }
    public static string Row(AgitHouseEntry h,Func<string,string>? names=null)=>
        $"{(names==null?h.Name:names(h.Name))}　面積 {h.Area}　截止 {h.Month}/{h.Day}　{h.Price.ToString("N0",CultureInfo.InvariantCulture)} 金幣";
    public static string MapText(AgitHouseMap m)=>m.InOriginalRange
        ?$"區域 {m.Region}、編號 {m.Number}（物件 {m.ObjectId}）"
        :$"盟屋編號 {m.HouseNumber} 不在原版範圍（區域 < 9、編號 1–70），不送查詢。";
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
    /// <summary>
    /// R10-I（T393／#908）：0xEC99EB／0xEED7B9 在自己的等級 &lt; 50 時把 byte_13A931C 清成 0（不看職業）。
    /// 還沒收到自己的狀態時不動。回 true＝這次清掉（呼叫端要存檔）。
    /// </summary>
    public bool ClearElfTeleportOptionBelowLevel(){
        if(!ElfBookmarkTeleportOption||Status is not {} s||s.Level>=ElfBookmarkTeleportRules.MinLevel)return false;
        ElfBookmarkTeleportOption=false;return true;
    }
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

    /// <summary>
    /// R7-C（#805）：拍賣清單窗選盟屋 → C125 select（S156 的看板 id）。shown 已不是目前的 S156、盟屋不在清單、互動送出中或動作鎖定時不送。
    /// </summary>
    public Task<bool> SendAgitAuctionSelect(AgitAuctionOpenRequest shown,uint houseId)=>
        SendAgitBoard(shown.List.BoardId,houseId,AgitSelectPackets.Select,()=>AgitAuctionWindow.Current?.Revision==shown.Revision,"盟屋拍賣查詢");
    /// <summary>R7-C（#805）：拍賣資訊窗的 Bid 鈕 → C125 apply。agsel 已被取代、盟屋編號不是數字或不在目前的 S156 清單時不送。</summary>
    public Task<bool> SendAgitAuctionApply(AgitAuctionInfoRequest shown)=>shown.HouseId is uint house
        ?SendAgitBoard(shown.BoardId,house,AgitSelectPackets.Apply,()=>AgitAuctionInfoWindow.Current?.Revision==shown.Revision,"盟屋拍賣參加")
        :Task.FromResult(false);

    async Task<bool> SendAgitBoard(uint board,uint house,Func<uint,uint,byte[]> build,Func<bool> current,string what){
        bool Listed()=>World.AgitHouseList is {} l&&l.BoardId==board&&l.Houses.Any(h=>h.HouseId==house);
        if(State!=LoginState.InWorld||board==0||interactionSending||MovementBusy||sendMovementGate is not {} sender
            ||World.ActionLocked||World.TradeBlocksActions||!current()||!Listed())return false;
        byte[] packet=build(board,house);
        int session=generation;interactionSending=true;
        bool Valid()=>session==generation&&State==LoginState.InWorld&&current()&&Listed()&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy;
        try{return await sender(packet,Valid,()=>{})&&session==generation;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,$"{what}傳送失敗，已斷線；不會自動重送。");}return false;}
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
        AttachAgitAuctionHost();
        if(NativeUiRoot?.Find(OptionPanel.PanelId) is OptionPanel option&&option.Host==null)option.Attach(new OptionWindowHost(this));
        SyncElfTeleportOption();
        SyncClassicAgit();
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
        /// <summary>R10-J（#909）：SendTeleportDestination 接受書籤 id 0（AbilityStatus.TeleportDestinationTarget 送 HD(0,0)，3.81 TELEPORT 隨機傳送）。</summary>
        public bool AcceptsRandomTeleport=>true;
    }
    bool elfTeleportOptionLoaded;
    /// <summary>
    /// R10-I（T393／#908）：EnableElfTelControl 的存檔與等級規則。第一次呼叫時從 option-ui.txt 載入（原版 sub_59DD30 讀設定檔），
    /// 之後每次（書籤計時器）檢查等級 &lt; 50 就清成 0 並存檔（0xEC99EB／0xEED7B9）。原生 UI 關閉（--classic-ui）時同樣載入與檢查。
    /// </summary>
    void SyncElfTeleportOption(){
        if(!elfTeleportOptionLoaded){
            elfTeleportOptionLoaded=true;_=nameDisplayConfigured;
            if(OptionSettingsFile.Read(OptionSettingsFile.ElfTeleportKey) is bool saved)login.World.ElfBookmarkTeleportOption=saved;
        }
        if(login.World.ClearElfTeleportOptionBelowLevel())OptionSettingsFile.Write(OptionSettingsFile.ElfTeleportKey,false);
    }
    /// <summary>
    /// R9-Q（T377／#861，FU9§14.4）：原生選項窗勾選框 → ClientForm 既有設定，點一下就生效。
    /// ShowNpcNameBtn＝「全部顯示名稱」（0x138BE7F，與 WinForms 勾選框同一個值與同一個檔）；CheckMute＝主音量靜音（SetAudioSettings，
    /// 聲音關閉時不套用）；EnableElfTelControlBtn＝byte_13A931C → WorldSession.ElfBookmarkTeleportOption。
    /// R10-I（#908）：EnableElfTelControl 存進 option-ui.txt，等級 &lt; 50 時勾不起來（立刻清回 0，回 false）；
    /// ShowDropItemNameBtn／TransparentDeadMonsterBtn → ObjectRendering.cs 的兩個顯示開關（也存 option-ui.txt）；
    /// Music／Effect／Env 音量捲軸 → 各自的類別音量（R11-F，#962；PlayerAudioSettings.BarCategory）。
    /// </summary>
    internal sealed class OptionWindowHost(ClientForm form):IOptionWindowHost {
        public bool? Value(string mouseEvent)=>mouseEvent switch{
            OptionPanel.ShowNpcNameEvent=>form.showAllNames,
            OptionPanel.MuteEvent=>form.audioSettings.Muted,
            OptionPanel.ElfTeleportEvent=>form.login.World.ElfBookmarkTeleportOption,
            OptionPanel.ShowDropItemNameEvent=>form.ShowDropItemNames,
            OptionPanel.TransparentDeadMonsterEvent=>form.TransparentDeadMonsters,
            _=>null};
        public bool Set(string mouseEvent,bool value){
            switch(mouseEvent){
                case OptionPanel.ShowNpcNameEvent:
                    form.showAllNames=value;NameDisplayOptions.SaveShowAll(value);form.Invalidate();return true;
                case OptionPanel.MuteEvent:
                    form.SetAudioSettings(form.audioSettings.Gain,value);return form.audioSettings.Muted==value;
                case OptionPanel.ElfTeleportEvent:{
                    var w=form.login.World;w.ElfBookmarkTeleportOption=value;w.ClearElfTeleportOptionBelowLevel();
                    OptionSettingsFile.Write(OptionSettingsFile.ElfTeleportKey,w.ElfBookmarkTeleportOption);
                    return w.ElfBookmarkTeleportOption==value;
                }
                case OptionPanel.ShowDropItemNameEvent:form.ShowDropItemNames=value;return true;
                case OptionPanel.TransparentDeadMonsterEvent:form.TransparentDeadMonsters=value;return true;
                default:return false;
            }
        }
        // R11-F（T420／#962）：Music／Effect／Env 三條捲軸各自對到 MusicVolume／SoundVolume／EnvSoundVolume 類別音量
        // （產品唯一一份，ClientSoundScene.Volumes＝form.SoundVolumes），不再共用主音量；主音量與 CheckMute 照舊（SetAudioSettings）。
        public double? Level(string bar)=>PlayerAudioSettings.BarLevel(form.SoundVolumes,bar);
        public bool SetLevel(string bar,double value)=>PlayerAudioSettings.SetBarLevel(form.SoundVolumes,bar,value);
    }
    sealed class AgitMapHost(ClientForm form):IAgitMapHost {
        bool sending;
        public void Select(AgitMapOpenRequest shown){
            if(sending||form.World.Walking)return;
            sending=true;form.ClearInput("agit-select");_ = Run();
            async Task Run(){try{await form.login.SendAgitSelect(shown);}finally{sending=false;}}
        }
    }

    // R7-C（#805）：AgitAuctionUI 三個原生窗（拍賣清單、拍賣資訊、出價）共用一個宿主。--classic-ui 時原生 UI 不建立，
    // S156／agsel 照舊只有資料與 NPC 對話頁，agapply 照舊走 NPC 對話面板的數字框（AmountPrompt.cs）。
    AgitAuctionHost? agitAuctionHost;
    void AttachAgitAuctionHost(){
        if(NativeUiRoot is not {} root)return;
        agitAuctionHost??=new(this);
        if(root.Find(AgitAuctionPanel.PanelId) is AgitAuctionPanel list&&list.Host==null)list.Attach(agitAuctionHost);
        if(root.Find(AgitAuctionInfoPanel.PanelId) is AgitAuctionInfoPanel info&&info.Host==null)info.Attach(agitAuctionHost);
        if(root.Find(AgitBidPanel.PanelId) is AgitBidPanel bid&&bid.Host==null)bid.Attach(agitAuctionHost);
    }
    // ---- T434（#992，classic-ui-audit G10）：盟屋拍賣清單 S156 與盟屋地圖 S187 的 WinForms 入口（classic 才用） ----------------
    // 原生 AgitAuctionPanel／AgitMapPanel 沒有或不能用時（--classic-ui、參考客戶端沒有 AgitAuctionUI.xml、面板故障），在服務分頁
    // 「盟屋拍賣」列出 S156 的盟屋，選一筆送 C125 select（既有 SendAgitAuctionSelect，與原生清單同一條路）；S187 只有一間盟屋，
    // 給一顆「查詢這間盟屋」鈕（既有 SendAgitSelect＝674 鈕）。回包 S39 agsel 照舊由 NPC 對話面板顯示；出價 agapply 照舊走
    // NPC 面板的數字框（AmountPrompt.cs）。新的 S156／S187 到了就把分頁叫出來（原版是自動開窗）。原生面板可用時這頁不出現。
    TabPage? classicAgitPage;
    readonly ListBox classicAgitHouses=new(){Width=420,Height=180,AccessibleName="盟屋拍賣清單"};
    readonly Button classicAgitSelect=new(){Text="查詢選取的盟屋",AutoSize=true};
    readonly Label classicAgitMap=new(){AutoSize=true,ForeColor=Color.White,MaximumSize=new(420,0)};
    readonly Button classicAgitMapSelect=new(){Text="查詢這間盟屋",AutoSize=true};
    readonly Label classicAgitNotice=new(){AutoSize=true,ForeColor=Color.White,MaximumSize=new(420,0)};
    readonly ClassicAgitTracker classicAgitTracker=new();
    AgitAuctionOpenRequest? classicAgitList;AgitMapOpenRequest? classicAgitHouse;bool classicAgitSending;
    bool NativeAgitAuctionUsable()=>NativeUiActive&&NativeUiRoot is {} root&&root.Find(AgitAuctionPanel.PanelId) is AgitAuctionPanel p&&p.Available&&!root.Faulted.Contains(p);
    bool NativeAgitMapUsable()=>NativeUiActive&&NativeUiRoot is {} root&&root.Find(AgitMapPanel.PanelId) is AgitMapPanel p&&!root.Faulted.Contains(p);
    void SyncClassicAgit(){
        if(normalPanels==null)return;
        if(classicAgitPage==null){
            classicAgitPage=new TabPage("盟屋拍賣"){BackColor=Color.FromArgb(23,38,41),AutoScroll=true};
            var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new(8)};
            flow.Controls.AddRange([new Label{Text="盟屋拍賣清單（S156）",AutoSize=true,ForeColor=Color.Khaki},classicAgitHouses,classicAgitSelect,
                new Label{Text="盟屋地圖（S187）",AutoSize=true,ForeColor=Color.Khaki},classicAgitMap,classicAgitMapSelect,classicAgitNotice]);
            classicAgitPage.Controls.Add(flow);classicAgitPage.Enter+=(_,_)=>LeaveGameFocus("classic-agit");
            classicAgitSelect.Click+=(_,_)=>{if(classicAgitList is {} shown&&classicAgitHouses.SelectedIndex is int i&&i>=0&&i<shown.List.Houses.Count)ClassicAgitSend("agit-auction-select",()=>login.SendAgitAuctionSelect(shown,shown.List.Houses[i].HouseId));};
            classicAgitMapSelect.Click+=(_,_)=>{if(classicAgitHouse is {} shown)ClassicAgitSend("agit-select",()=>login.SendAgitSelect(shown));};
            classicAgitHouses.SelectedIndexChanged+=(_,_)=>SyncClassicAgitButtons();
        }
        bool inWorld=login.State==LoginState.InWorld;
        var list=inWorld&&!NativeAgitAuctionUsable()?AgitAuctionWindow.Current:null;
        var house=inWorld&&!NativeAgitMapUsable()?AgitMapWindow.Current:null;
        bool shown=normalPanels.TabPages.Contains(classicAgitPage);
        if(list==null&&house==null){if(shown)normalPanels.TabPages.Remove(classicAgitPage);classicAgitList=null;classicAgitHouse=null;return;}
        if(!shown)normalPanels.TabPages.Add(classicAgitPage);
        if(!ReferenceEquals(list,classicAgitList)){
            classicAgitList=list;classicAgitHouses.Items.Clear();
            if(list!=null)classicAgitHouses.Items.AddRange(list.List.Houses.Select(h=>(object)ClassicAgitTracker.Row(h,ResolveNpcText)).ToArray());
        }
        if(!ReferenceEquals(house,classicAgitHouse)){classicAgitHouse=house;classicAgitMap.Text=house==null?"（沒有）":ClassicAgitTracker.MapText(house.Map);}
        SyncClassicAgitButtons();
        if(classicAgitTracker.Observe(list?.Revision,house?.Revision)){
            normalPanels.SelectedTab=classicAgitPage;
            if(PlayerHudActive)ShowPlayerService(classicAgitPage.Text);else ShowDevTools(normalPanels);
        }
    }
    void SyncClassicAgitButtons(){
        bool can=!classicAgitSending&&!World.Walking&&login.State==LoginState.InWorld;
        classicAgitSelect.Enabled=can&&classicAgitList!=null&&classicAgitHouses.SelectedIndex>=0;
        classicAgitMapSelect.Enabled=can&&classicAgitHouse is {Map.InOriginalRange:true,Map.ObjectId:not 0};
    }
    void ClassicAgitSend(string tag,Func<Task<bool>> send){
        if(classicAgitSending||World.Walking)return;
        classicAgitSending=true;ClearInput(tag);SyncClassicAgitButtons();_=Run();
        async Task Run(){
            bool sent=false;
            try{sent=await send();}
            finally{classicAgitSending=false;classicAgitNotice.Text=sent?"已送出查詢；盟屋資訊會出現在 NPC 對話面板。":"目前不能送出（清單已更新、互動中或動作鎖定）。";SyncClassicAgitButtons();}
        }
    }

    /// <summary>原生出價窗正在顯示這一筆 agapply（AmountPrompt.cs 用來藏 WinForms 數字列，避免兩個輸入框）。</summary>
    internal bool NativeAgitBidShowing(AmountOffer offer)=>
        NativeUiActive&&NativeUiRoot is {} root&&root.Find(AgitBidPanel.PanelId) is AgitBidPanel p&&!root.Faulted.Contains(p)&&p.Shown?.Revision==offer.Revision;

    sealed class AgitAuctionHost(ClientForm form):IAgitAuctionHost,IAgitAuctionChatHost {
        /// <summary>
        /// T429（#986）：sub_545920（FO 0x144D20）印 4381 用 sub_7DC850(字串, palette[24], palette[14], -1, -1, 0)
        /// （VA 0x5459A3–0x5459D7：4*0x18、4*0xE 取 0x1369D70 色表），第一色 palette[24]，與信件結果（MailChatColor）同一格。
        /// </summary>
        internal const int BidChatColor=24;
        bool sending;
        /// <summary>T429（#986）：出價窗 Ok 低於最小值時的 4381 印到聊天欄（與看板／信件同一個入口 AddBoardChatNotice）。</summary>
        public void ChatNotice(string text){
            if(form.login.State!=LoginState.InWorld)return;
            form.login.CaptureWorld(()=>{form.login.World.AddBoardChatNotice(text,BidChatColor);return true;});
        }
        void Send(string tag,Func<Task<bool>> send){
            if(sending||form.World.Walking)return;
            sending=true;form.ClearInput(tag);_ = Run();
            async Task Run(){try{await send();}finally{sending=false;}}
        }
        public void Select(AgitAuctionOpenRequest shown,uint houseId)=>Send("agit-auction-select",()=>form.login.SendAgitAuctionSelect(shown,houseId));
        public void Apply(AgitAuctionInfoRequest shown)=>Send("agit-auction-apply",()=>form.login.SendAgitAuctionApply(shown));
        /// <summary>agsel 模板（dialogs/templates.json，與 NPC 對話同一份）帶入 9 個字串；$編號 以 NPC 名稱表解開。缺模板回 null。</summary>
        public string? InfoText(AgitAuctionInfoRequest shown){
            var w=form.login.World;
            if(w.DialogTemplates is not {} templates)return null;
            var page=templates.Render(AgitAuctionInfoPackets.Html,shown.Data,"",w.Actor?.Name??"");
            return page.Text.StartsWith("缺少此對話的原始繁中模板",StringComparison.Ordinal)?null:form.ResolveNpcText(page.Text);
        }
        public AmountOffer? BidOffer=>form.login.AmountPromptSnapshot().Prompt is {Html:AgitBidPanel.ApplyHtml} o?o:null;
        public bool CanBid=>!sending&&!form.World.Walking&&form.login.AmountPromptSnapshot().CanSend;
        public void Bid(AmountOffer shown,long value){
            if(sending||form.World.Walking)return;
            var v=form.login.AmountPromptSnapshot();
            if(v.Prompt?.Revision!=shown.Revision||!v.CanSend)return;
            Send("agit-auction-bid",()=>form.login.SendAmountReply(v,value));
        }
    }
}
