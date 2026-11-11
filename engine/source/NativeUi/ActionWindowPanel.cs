namespace P06.NativeUi;

// R8-D（T359／#823，FU8§4、gap-matrix 15.10 Q6-4）：動作窗。依本機 Tile.pak 的 ActionUI-c.xml（版面）＋Actions.xml（25 個動作）建原生視窗。
// 來源（本機 0-客戶端 Tile.idx，PakViewer 解出，不進 repo）：ActionUI-c.xml 解密 SHA-256 前 16 碼 ae467982a6880161、
//   Actions.xml 20c180afe54e6e10（與 FU8§0 相同）。TW16 沒有 ActionUI 類別（FU8§4.1）；3.81 用這兩份是 **推測**，版面要真機再確認。
//
// 已驗證（XML／資源）：
// - 根 ActionUI（Toggle="1"）；Window "Action_Window" BaseSurf 5141、FrameSurf 5142（本機 5141c／5142c.img 都是 243×195）、Moveable 20。
// - ActionIconGrid (21,45)，Row 4 × Column 6，UnitWidth／Height 32、UnitGap 1，Selection_Image surf 5333（34×34）。
// - Tab_Control (−2,20)：Total／Community／Action／Management 四顆 RadioButton（5144–5155，各 50×23）。
// - Exit_Button (228,11) 1962／1963；ScrollBar 在格子內 (202,8)，Up 2042／2043、Down 2040／2041、Move 2044／2045、底 5143。
// - Actions.xml 每項 action／category／control／icon（target、domain 只在 control="target" 時有）。
// - 圖示：25 個 icon 編號在本機 Sprite*.pak 都有 "<n>.tbt"（都約 1.9 KB，像 32×32 圖示）；同號的 .img 大小不一（12×12、67×54…，
//   5363 沒有 .img），所以圖示照背包圖示的查法讀 "<icon>.tbt"（ItemIconSource，與 BuffIconCatalog 同一組）。這個對應是 **推測**。
//
// 推測（標明，真機再確認）：
// - 視窗畫面位置：XML 沒給 X/Y，照 QUIT_WINDOW 已驗證的規則水平置中、y＝150（同 UserPlayStatsPanel）。
// - 外框疊在底圖上、都畫在視窗原點；ScrollBar 的 X/Y 相對於格子（換算後 (223,53)–(235,189)，剛好落在 243×195 內、
//   Up 14 高接底圖 (1,14) 91 高再接 Down (0,105)，幾何吻合）。
// - 分頁：Total＝全部動作（Actions.xml 順序），其他三頁＝該 category；開窗預設 Total；按下（513）就換頁（同 BehaviourUI 的 TabButton），
//   換頁時捲動與選取歸零。選中的分頁畫 PushSurf，滑鼠停留畫 HighlightSurf，其他畫 BaseSurf。
// - 格子：單擊選取（畫 5333 框，左上對齊格子 −1,−1），**雙擊（515）執行**（同 BehaviourUI 的 GridEvent→sub_7C7CB0）。
// - 捲動以列為單位（6 格一列），最大值＝列數 − 4；XML 的 MaxValue＝8、ScrollStart／ScrollHeight 只留資料。拇指在 Move_Button.Y 到
//   Down_Button.Y − 拇指高之間依比例移動；拖拇指、點軌道不做。滾輪在視窗內上下捲一列。
// - Esc：不由本窗攔（ActionUI 的 KeyboardEvent 內容沒查）；全域 ESC（R7-H，UiRoot.CloseAll）照樣會關它。
// 只做資料（標「真機再確認」）：格子 Drag="1"（把動作拖到巨集列，選做，未做）；toggle 類動作的開關狀態（不畫勾選：勾選圖沒有依據）。
// R10-E（T389／#904）：常駐開窗鈕 ActionUI_Button (774,470) 5156／5157 改由 ActionOpenButtonPanel 畫在 Bar 層、收點擊
//   （OpenButtonPressed 514／515 → 開關本窗，FU8§4.3）；位置與是否常駐仍待真機（M33）。
// R11-B（T416／#958）：常駐鈕滑鼠停留時畫 XML Tooltip 的字串（本機 string-c.tbl 第 3118 筆「行動視窗」，PakViewer 查過）；
//   框的尺寸與字色沿用 SummonPanel 的提示（sub_B0DAF0／sub_C00BF0），位置（鈕正下方置中、夾進畫面）與黑底是推測。
//
// 執行（原版 sub_ADC200 依名稱找處理函式，FU8§4.3）。路線表在 ActionRoutes（25 個動作各一列）。引擎依序：
// 1. 指令物件實作 IUiActionCommands 且 RunAction 回 true（R10-E：產品 NativeUiCommands 接表情、拾取、聊天指令、全部拾取；
//    R11-B：對象型動作進 R11-A 的選對象模式、選定玩家後直接送；隊長的組隊邀請可踢人；地面名稱／透明屍體開關）；
// 2. 面板自己能做的：Action_OpenBookmarkWin → 原生書籤窗（UiRoot.Toggle，同選單鈕）；對象型動作（邀請聊天／加好友／封鎖）沒有橋接時
//    → 把指令前綴放進原生聊天輸入列（退路）；HP／MP 回復顯示 → HpMpPanel 設定；地面名稱／透明屍體 → OptionPanel 的宿主；
// 3. Action_SocialAction1–4 → 開發工具「稱號／表情」分頁（沒有橋接時的退路，TitleAndEmote.cs）；
// 4. 其他 → 不送任何封包，記一次 log（Unhandled；原因在 ActionRoutes 的 Note）。
// 開關：Ctrl+D（FU4§1.1 TW16 的 BehaviourUI 鍵；3.81 是否同鍵待驗證）由 MenuButtonsPanel 經 UiRoot.Toggle 開關本窗。
// --classic-ui 沒有原生 UI 根：本窗不存在，WinForms 開發工具的「稱號／表情」分頁照舊是唯一入口。

public enum ActionCategory { Action, Community, Management }
public enum ActionControl { Action, Toggle, Target }
public enum ActionDomain { None, Global, View, Map }

/// <summary>Actions.xml 的一項。Targets＝target 四旗標（地圖物件、聊天窗、隊伍窗、寵物隊伍窗），沒寫＝空。</summary>
public sealed record ActionEntry(string Name,ActionCategory Category,ActionControl Control,int Icon,ActionDomain Domain,IReadOnlyList<bool> Targets,string Path);

/// <summary>Actions.xml（ActionList/Action）。未知的 category／control／domain、缺 action 或 icon、名稱重複 → UiLayoutException。</summary>
public static class ActionCatalog {
    public const string FileName="Actions",RootNode="ActionList",ItemNode="Action";
    public static IReadOnlyList<ActionEntry> Parse(UiLayout layout){
        var root=layout.Root;
        if(root.Name!=RootNode)throw new UiLayoutException(layout.FileName,root.Path,$"根元素不是 {RootNode}");
        var list=new List<ActionEntry>();
        foreach(var n in root.ChildrenNamed(ItemNode)){
            string name=n.RequiredAttr("action").Trim();
            if(name.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"action 是空的");
            if(list.Any(a=>a.Name==name))throw new UiLayoutException(layout.FileName,n.Path,$"action {name} 重複");
            var category=n.RequiredAttr("category").Trim() switch{
                "action"=>ActionCategory.Action,"community"=>ActionCategory.Community,"management"=>ActionCategory.Management,
                var x=>throw new UiLayoutException(layout.FileName,n.Path,$"不明的 category {x}")};
            var control=n.RequiredAttr("control").Trim() switch{
                "action"=>ActionControl.Action,"toggle"=>ActionControl.Toggle,"target"=>ActionControl.Target,
                var x=>throw new UiLayoutException(layout.FileName,n.Path,$"不明的 control {x}")};
            var domain=(n.Attr("domain")?.Trim()) switch{
                null or ""=>ActionDomain.None,"global"=>ActionDomain.Global,"view"=>ActionDomain.View,"map"=>ActionDomain.Map,
                var x=>throw new UiLayoutException(layout.FileName,n.Path,$"不明的 domain {x}")};
            bool[] targets=[];
            if(n.Attr("target") is {} raw){
                var parts=raw.Split(',');
                if(parts.Length!=4||parts.Any(p=>p.Trim() is not ("0" or "1")))throw new UiLayoutException(layout.FileName,n.Path,$"target=\"{raw}\" 不是四個 0／1");
                targets=parts.Select(p=>p.Trim()=="1").ToArray();
            }
            int icon=n.Int("icon");
            if(icon<=0)throw new UiLayoutException(layout.FileName,n.Path,$"icon {icon} 不合法");
            list.Add(new(name,category,control,icon,domain,targets,n.Path));
        }
        if(list.Count==0)throw new UiLayoutException(layout.FileName,root.Path,"沒有任何 Action");
        return list;
    }
}

/// <summary>
/// R8-D：動作的對外執行（產品由 ClientForm 的 NativeUiCommands 實作，R10-E）。true＝已處理；false＝做不到，面板改走自己的退路。
/// </summary>
public interface IUiActionCommands {
    bool RunAction(ActionEntry action);
}

/// <summary>雙擊一格之後實際走了哪條路（檢查與 log 用）。ChatLine＝指令前綴放進聊天列；Toggled＝面板切換了既有設定。</summary>
public enum ActionRunResult { Bridge, Window, DevTools, Unhandled, ChatLine, Toggled }

/// <summary>
/// R10-E：一個動作接到引擎的哪條既有入口。Emote＝表情（Item 1–4）；Loot＝F4 拾取；ChatCommand＝當作在聊天列輸入 Argument 送出
/// （走既有斜線指令）；ChatLine＝把 Argument（指令前綴）放進聊天輸入列等使用者打名稱；Toggle＝切換既有設定（Argument＝設定名）；
/// Window＝開關原生視窗（Argument＝面板 Id）；None＝引擎沒有對應，不做（Note＝原因）。
/// </summary>
public enum ActionRouteKind { None, Emote, Loot, ChatCommand, ChatLine, Toggle, Window }
public sealed record ActionRoute(ActionRouteKind Kind,string? Argument=null,int Item=0,string? Note=null);

/// <summary>
/// R10-E（T389／#904，FU8§4.3、FU9§9）：Actions.xml 25 個動作的路線表。原版處理函式見 FU8§4.3 表；推測的對應在 Note 標明。
/// </summary>
public static class ActionRoutes {
    public const string LootAllToggle="loot-all",HpMpRecoveryToggle="hpmp-recovery";
    /// <summary>R11-B：地面名稱、透明屍體兩個開關＝選項窗的 MouseEvent 名（IOptionWindowHost.Value／Set 的鍵）。</summary>
    public const string DropItemNamesToggle=OptionPanel.ShowDropItemNameEvent,CorpseTransparentToggle=OptionPanel.TransparentDeadMonsterEvent;
    public static readonly IReadOnlySet<string> DisplayToggles=new HashSet<string>(StringComparer.Ordinal){DropItemNamesToggle,CorpseTransparentToggle};
    /// <summary>本機 Actions.xml 的 25 個動作名稱（XML 順序；名稱是識別字，不是遊戲資源）。</summary>
    public static readonly IReadOnlyList<string> Known=[
        "Action_ShoutOfSurvival","Action_Loot","Action_SocialAction1","Action_SocialAction2","Action_SocialAction3","Action_SocialAction4",
        "Action_OpenMacroWin","Action_Trade","Action_InviteChatParty","Action_InviteParty","Action_AddFriend","Action_Exclude",
        "Action_OpenBookmarkWin","Action_OpenShop","Action_OpenPetManagementWin","Action_Report","Action_ViewDropItemName","Action_LootAll",
        "Action_CorpseTransparent","Action_HPMPRecoveryEffect","Action_ShowDisableStatusIcon","Action_DungeonTimeLimit","Action_BrodcastToPledge",
        "Action_PotionHelper","Action_NumberingMarker"];
    public static ActionRoute Of(string name)=>name switch{
        // sub_ADACB0／ADADE0／ADAED0／ADAFC0：C_ACTION 68／69／67／66，動作槽路徑（menu=false，AN§1.1 的重映射）。
        "Action_SocialAction1"=>new(ActionRouteKind.Emote,Item:1),
        "Action_SocialAction2"=>new(ActionRouteKind.Emote,Item:2),
        "Action_SocialAction3"=>new(ActionRouteKind.Emote,Item:3),
        "Action_SocialAction4"=>new(ActionRouteKind.Emote,Item:4),
        // sub_ADA840 → sub_500640（F4 鍵盤拾取，R6-D）。
        "Action_Loot"=>new(ActionRouteKind.Loot),
        // sub_ADB030 → sub_F43510（同 /trade，FU9§12）；sub_ADA960 → sub_4FFCD0("shop",1)（同 /shop，FU9§9.4）。
        "Action_Trade"=>new(ActionRouteKind.ChatCommand,"/trade"),
        "Action_OpenShop"=>new(ActionRouteKind.ChatCommand,"/shop"),
        // sub_ADA6B0（FU9§9.1 第 341 行）：隊長而對象已在隊上＝踢人，否則邀請。R11-B（#958）：產品 NativeUiCommands 在「是隊長、
        // 有隊伍」（PartyView.CanKick）時改用 R11-A 的選對象入口，點中隊員送 C255（同 /banish），點中別人照 R10-D 送 C230 type 0；
        // 不是隊長時照舊走不帶名稱的 /invite → World.BeginPartyInvitePick。
        "Action_InviteParty"=>new(ActionRouteKind.ChatCommand,"/invite",Note:"隊長點已在隊上的人＝踢人（NativeUiCommands，R11-B）"),
        // control="target"：原版選定對象後直接送（下面三個；sub_9FBC00 是送封包函式，第 2 個參數是 opcode，FU8§0 第 16 行）。
        // R11-B（#958）：產品 NativeUiCommands 用 R11-A 的選對象入口（LoginConnection.BeginObjectPick），點中玩家 → 當作在聊天列
        //   輸入「Argument＋名稱」送出（既有斜線指令，送 3.81 的封包）；沒有橋接（離線、不在世界中）時面板退回把前綴放進聊天列。
        // sub_ADA670 → sub_9FBC00(off_119F834,120,3,名稱)＝TW16 op 120（C_CHAT_PARTY_CONTROL）type 3＋名稱；3.81 C199 只有 type 0–2，
        //   所以改送 3.81 的聊天隊伍邀請 /chatinvite（C230 type 2，Party.cs）。兩者對應是推測。
        "Action_InviteChatParty"=>new(ActionRouteKind.ChatLine,"/chatinvite ",Note:"選定對象後送 /chatinvite 名稱（TW16 op 120 type 3 → 3.81 C230 type 2，推測）"),
        // sub_ADA220 → sub_B18300：送 cs 138（加好友＋名稱）＋c 56（查名單）（FU13 第 70 行）＝ /addbuddy 名稱（C207＋C4，Buddies.cs）。
        "Action_AddFriend"=>new(ActionRouteKind.ChatLine,"/addbuddy ",Note:"選定對象後送 /addbuddy 名稱（sub_ADA220 → sub_B18300，FU13 第 70 行）"),
        // 處理函式沒單獨追；會讀名稱的封鎖是 CommandAddExclude → sub_F40FC0(名稱)（FU13§3.2）。3.81 只有 C171 這條路 → /exclude 名稱。
        "Action_Exclude"=>new(ActionRouteKind.ChatLine,"/exclude ",Note:"選定對象後送 /exclude 名稱（C171；處理函式未單獨追，推測）"),
        "Action_OpenBookmarkWin"=>new(ActionRouteKind.Window,BookmarkWindowPanel.PanelId),
        // byte_135C1E4：WorldSession.TogglePickupAll（Ground.cs）。
        "Action_LootAll"=>new(ActionRouteKind.Toggle,LootAllToggle),
        // sub_ADA630 切換 ShowHPMPRecovery；對到 HpMpSettings.ShowRecovery（OptionUI HPMPBarRecoveryBtn 同一個值）是推測。
        "Action_HPMPRecoveryEffect"=>new(ActionRouteKind.Toggle,HpMpRecoveryToggle,Note:"ShowHPMPRecovery＝HpMpSettings.ShowRecovery 是推測"),
        "Action_ShoutOfSurvival"=>new(ActionRouteKind.None,Note:"處理函式與封包沒查（FU8§4.3 未列）"),
        "Action_OpenMacroWin"=>new(ActionRouteKind.None,Note:"原版開的巨集編輯窗引擎沒有"),
        "Action_OpenPetManagementWin"=>new(ActionRouteKind.None,Note:"target／map 類，寵物窗由伺服器狀態開啟，沒有手動入口"),
        // sub_ADABC0 → sub_9FBC00(off_119F96C,55,42,名稱)：直接送 TW16 op 55 type 42＋名稱（不是輸入框）；3.81 沒有對應的檢舉封包。
        "Action_Report"=>new(ActionRouteKind.None,Note:"sub_ADABC0 送 TW16 op 55 type 42＋名稱；3.81 沒有對應的檢舉封包"),
        // sub_ADB040／sub_ADA540 只切 byte_138BE7E／byte_138BFAA（FU8§4.3）＝選項窗 ShowDropItemNameBtn／TransparentDeadMonsterBtn 的
        // 同一個值（R10-I，ObjectRendering.cs，存 option-ui.txt）。R11-B：Argument＝OptionPanel 的 MouseEvent 名，經 IOptionWindowHost 切換。
        "Action_ViewDropItemName"=>new(ActionRouteKind.Toggle,DropItemNamesToggle,Note:"byte_138BE7E＝選項窗 ShowDropItemNameBtn（R10-I）"),
        "Action_CorpseTransparent"=>new(ActionRouteKind.Toggle,CorpseTransparentToggle,Note:"byte_138BFAA＝選項窗 TransparentDeadMonsterBtn（R10-I）"),
        "Action_ShowDisableStatusIcon"=>new(ActionRouteKind.None,Note:"對應的旗標沒查"),
        "Action_DungeonTimeLimit"=>new(ActionRouteKind.None,Note:"byte_1379424 的用途沒查"),
        "Action_BrodcastToPledge"=>new(ActionRouteKind.None,Note:"對應的旗標沒查"),
        "Action_PotionHelper"=>new(ActionRouteKind.None,Note:"處理函式沒查"),
        "Action_NumberingMarker"=>new(ActionRouteKind.None,Note:"target／view 類，處理函式沒查"),
        _=>new(ActionRouteKind.None,Note:"不在本機 Actions.xml 的 25 個動作內"),
    };
}

/// <summary>
/// R11-B（T416／#958）：對象型動作（control="target"）選定對象後要送什麼。產品 NativeUiCommands 用；純函式，離線測試直接驗。
/// 對象規則（推測，原版的對象檢查沒逐一追）：玩家旗標（Flags &amp; 4，同 sub_954FC0 的組隊檢查）、外觀已到、不是自己、名稱是合法角色名；
/// 距離不限（三個動作的 domain 是 global）。
/// </summary>
public static class ActionTargets {
    public const string InvitePartyAction="Action_InviteParty";
    /// <summary>選對象模式的動作名（PlayerPointer 的結果訊息與滑鼠提示用）；名稱取自既有聊天指令的說明（PlayerChat.cs）。</summary>
    public static string PickName(string action)=>action switch{
        "Action_InviteChatParty"=>"邀請聊天","Action_AddFriend"=>"新增好友","Action_Exclude"=>"斷絕",InvitePartyAction=>"組隊邀請",_=>action};
    /// <summary>進入選對象模式時的提示。</summary>
    public static string PickNotice(string action)=>ChatFollowupCommands.SelectTargetText+"（"+PickName(action)+"：點選玩家；點地面取消）";
    /// <summary>可當對象的玩家名稱；不合格（不是玩家、自己、外觀未到、名稱不合法）→ null。</summary>
    public static string? PlayerName(WorldObject? o,uint self)=>
        o is {AppearancePending:false}&&o.ObjectId!=0&&o.ObjectId!=self&&(o.Flags&4)!=0&&PartyPackets.ValidName(o.Name)?o.Name:null;
    /// <summary>送出的聊天列文字（"/addbuddy " ＋ 名稱）。</summary>
    public static string Command(string prefix,string name)=>prefix.TrimEnd()+" "+name;
    /// <summary>
    /// sub_ADA6B0 的踢人分支：自己是隊長（shown.CanKick）而點中的人已在隊上 → 要踢的名稱；否則 null（照常邀請）。
    /// 隊員以物件 id 比對（名單只有名稱的列 id＝0，改以名稱比對）；點到自己不算。
    /// </summary>
    public static string? PartyKick(PartyView shown,WorldObject? o){
        if(!shown.CanKick||o==null||o.ObjectId==0||o.ObjectId==shown.Owner||o.Name==shown.Leader)return null;
        return shown.Members.FirstOrDefault(m=>m.ObjectId!=0?m.ObjectId==o.ObjectId:m.Name==o.Name&&o.Name.Length>0)?.Name;
    }
    static WorldObject? Find(WorldSession w,uint id)=>w.Objects.FirstOrDefault(o=>o.ObjectId==id);
    /// <summary>
    /// ③ 選對象回呼：點中的是合格玩家才當作在聊天列輸入「前綴＋名稱」送出（SendPlayerChat：同 ChatCommand 路線的閘門與斜線指令，
    /// 送 3.81 的封包）；點中自己／非玩家、輸入列正等答覆、聊天列冷卻 → 不送、回 false。只在 UI 執行緒用。
    /// </summary>
    public static async Task<bool> SendToTarget(LoginConnection login,string prefix,uint id){
        var w=login.World;
        if(id==w.Status?.ObjectId||PlayerName(Find(w,id),w.Actor?.ObjectId??0) is not {} name)return false;
        if(ChatLinePrompts.Of(login)!=null)return false;
        var current=login.PlayerChatSnapshot();
        if(!current.CanSend)return false;
        return await login.SendPlayerChat(Command(prefix,name),current.Generation,current.WorldEpoch,current.Actor);
    }
    /// <summary>
    /// ② 隊長的組隊邀請回呼（sub_ADA6B0）：點中已在隊上的人＝踢人（C255，SendParty "kick"）；否則照 R10-D 的組隊選對象
    /// （PickPartyInviteTarget：30 格內的玩家才送 C230 type 0）。回傳是否送出。
    /// </summary>
    public static async Task<bool> InviteOrKick(LoginConnection login,uint id){
        var shown=login.PartySnapshot();
        if(PartyKick(shown,Find(login.World,id)) is {} member)return await login.SendParty("kick",shown,member);
        login.World.BeginPartyInvitePick();
        return await login.PickPartyInviteTarget(id);
    }
}

public sealed class ActionWindowPanel(UiResources? resources=null,UiRoot? root=null,ItemIconSource? icons=null):IUiMovablePanel,IUiClosablePanel {
    [UiPanel(191)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,r=>new ActionWindowPanel(r.Resources,r,ItemIconSource.FromRoot(r)));   // R8-D (#823): ActionUI-c.xml + Actions.xml (local Tile.pak), loaded inside Load
    public const string PanelId="Action_Window";
    public const string LayoutName="ActionUI",RootNode="ActionUI",WindowNode="Window",WindowName="Action_Window";
    /// <summary>UiRoot.Build 用的宿主版面（UiRoot.Build 全有或全無）；真正的版面在 Load 內自己讀，讀不到只停用本窗。</summary>
    public const string HostLayout="uistatus86";
    /// <summary>推測：沿用 QUIT_WINDOW 已驗證的畫面位置（400 − 寬/2, 150）。</summary>
    public const int ScreenY=150;
    /// <summary>開關鍵（FU4§1.1 TW16 WM_CHAR 4＝Ctrl+D → BehaviourUI；3.81 待驗證）。MenuButtonsPanel 負責分派。</summary>
    public const Keys Hotkey=Keys.Control|Keys.D;
    public const string EmoteDevToolsPage="稱號／表情";
    public const string OpenBookmarkAction="Action_OpenBookmarkWin";
    public static readonly IReadOnlySet<string> SocialActions=new HashSet<string>(StringComparer.Ordinal){"Action_SocialAction1","Action_SocialAction2","Action_SocialAction3","Action_SocialAction4"};

    /// <summary>XML 的一顆按鈕（相對視窗原點）。Base 缺圖＝不畫、不能點。</summary>
    public sealed record Part(string Name,Point Offset,UiSurface? Base,UiSurface? Push,UiSurface? Highlight,string? MouseEvent) {
        public Rectangle Local=>Base==null?Rectangle.Empty:new(Offset,new Size(Base.Width,Base.Height));
    }
    /// <summary>一個分頁鈕。Category null＝Total（全部）。</summary>
    public sealed record Tab(Part Button,ActionCategory? Category);
    /// <summary>常駐開窗鈕 ActionUI_Button（只做資料：是否常駐待驗證）。</summary>
    public sealed record OpenButtonSpec(Point Location,int? BaseSurf,int? HighlightSurf,int? TooltipString);
    /// <summary>ScrollBar（Offset 相對視窗原點）。MaxValue／ScrollStart／ScrollHeight 只留資料。</summary>
    public sealed record ScrollSpec(Point Offset,Size Size,int MaxValue,int ScrollStart,int ScrollHeight,Point BackgroundAt,UiSurface? Background,Part? Up,Part? Down,Part? Thumb);

    public static readonly IReadOnlyDictionary<string,ActionCategory?> TabCategories=new Dictionary<string,ActionCategory?>(StringComparer.Ordinal){
        ["Total_Button"]=null,["Community_Button"]=ActionCategory.Community,["Action_Button"]=ActionCategory.Action,["Management_Button"]=ActionCategory.Management,
    };

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>不能開窗的原因（沒有資源、XML 讀不到或不合、底圖與外框都缺）；null＝可用。</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public Point Origin{get;private set;}
    public Size Size{get;private set;}
    public Rectangle Bounds=>Available?new(Origin,Size):Rectangle.Empty;
    public bool Visible=>open&&Available;
    public int MoveableHeight{get;private set;}
    public UiSurface? BaseSurface{get;private set;}
    public UiSurface? FrameSurface{get;private set;}
    public IReadOnlyList<ActionEntry> Actions{get;private set;}=[];
    public IReadOnlyList<Tab> Tabs{get;private set;}=[];
    public Part? Exit{get;private set;}
    public ScrollSpec? Scroll{get;private set;}
    public LegacyCellGrid Grid{get;private set;}=LegacyCellGrid.Empty;
    public UiSurface? SelectionSurface{get;private set;}
    public bool GridDrag{get;private set;}
    public OpenButtonSpec? OpenButton{get;private set;}
    /// <summary>R10-E：開窗鈕的底圖／停留圖（ActionOpenButtonPanel 畫）；缺圖＝null，記在 OpenButtonMissing。</summary>
    public UiSurface? OpenButtonBase{get;private set;}
    public UiSurface? OpenButtonHighlight{get;private set;}
    public IReadOnlyList<string> OpenButtonMissing{get;private set;}=[];
    /// <summary>XML 有寫但找不到的圖（不畫）："surf 5141 (ActionUI/Window[0]/@BaseSurf)"。</summary>
    public IReadOnlyList<string> Missing{get;private set;}=[];
    public int ActiveTab{get;private set;}
    public int ScrollRow{get;private set;}
    public ActionEntry? Selected{get;private set;}
    public IReadOnlyCollection<string> Unhandled=>unhandled;
    /// <summary>最近一次雙擊的結果（檢查用）。</summary>
    public (ActionEntry Action,ActionRunResult Result)? LastRun{get;private set;}
    bool open,placed;Part? pressed,hover;bool pressedInside;
    readonly HashSet<string> unhandled=new(StringComparer.Ordinal);
    ItemIconSource Icons=>icons??ItemIconSource.None;

    /// <summary>目前分頁的動作（Total＝全部，依 Actions.xml 順序）。</summary>
    public IReadOnlyList<ActionEntry> TabActions=>Tabs.Count==0?Actions:Tabs[ActiveTab].Category is ActionCategory c?Actions.Where(a=>a.Category==c).ToArray():Actions;
    public int MaxScroll=>Grid.Columns==0?0:Math.Max(0,Grid.RowsFor(TabActions.Count)-Grid.Rows);

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout ui,actions;
        try{ui=resources.Xml.Load(LayoutName);actions=resources.Xml.Load(ActionCatalog.FileName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"action-ui 無法載入 {LayoutName}／{ActionCatalog.FileName}，動作窗停用：{ex.Message}");return;}
        try{Load(ui,actions,surfs.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"action-ui {ui.FileName}／{actions.FileName} 版面不符，動作窗停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"action-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(OpenButtonMissing.Count>0)UiResourceLog.Write($"action-ui 開窗鈕缺圖不畫：{string.Join("、",OpenButtonMissing)}");
        if(Unavailable!=null)UiResourceLog.Write($"action-ui {Unavailable}");
    }

    void Clear(){
        open=false;placed=false;pressed=null;hover=null;pressedInside=false;Unavailable="not loaded";Origin=Point.Empty;Size=Size.Empty;MoveableHeight=0;
        BaseSurface=null;FrameSurface=null;Actions=[];Tabs=[];Exit=null;Scroll=null;Grid=LegacyCellGrid.Empty;SelectionSurface=null;GridDrag=false;
        OpenButton=null;OpenButtonBase=null;OpenButtonHighlight=null;OpenButtonMissing=[];Missing=[];ActiveTab=0;ScrollRow=0;Selected=null;LastRun=null;
    }

    /// <summary>離線測試用：surf 由呼叫端提供（合成圖）。版面不合 → UiLayoutException。</summary>
    internal void Load(UiLayout ui,UiLayout actions,Func<int,UiSurface?> surf){
        Clear();
        var root=ui.Root;
        if(root.Name!=RootNode)throw new UiLayoutException(ui.FileName,root.Path,$"根元素不是 {RootNode}");
        var missing=new List<string>();
        UiSurface? Get(UiNode n,string attr){
            if(OptionPanel.ImageAttr(n,attr) is not {} r)return null;
            var s=surf(r.Id);if(s==null)missing.Add($"surf {r.Id} ({n.Path}/@{attr})");
            return s;
        }
        Part PartOf(UiNode n,Point parent)=>new(n.Attr("Name")??n.Name,new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0)),
            Get(n,"BaseSurf"),Get(n,"PushSurf"),Get(n,"HighlightSurf")??Get(n,"HighLightSurf"),n.Attr("MouseEvent"));

        if(root.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")=="ActionUI_Button") is {} ob){
            OpenButton=new(new(ob.Int("X"),ob.Int("Y")),OptionPanel.ImageAttr(ob,"BaseSurf")?.Id,OptionPanel.ImageAttr(ob,"HighlightSurf")?.Id,
                ob.ChildrenNamed("Tooltip").FirstOrDefault()?.TryInt("String"));
            // R10-E：開窗鈕的圖另記（不進 Missing：缺了只是不畫開窗鈕，本窗照常）。
            var obMissing=new List<string>();
            UiSurface? O(int? id){if(id is not int v)return null;var s=surf(v);if(s==null)obMissing.Add($"surf {v} (ActionUI/ActionUI_Button)");return s;}
            OpenButtonBase=O(OpenButton.BaseSurf);OpenButtonHighlight=O(OpenButton.HighlightSurf);OpenButtonMissing=obMissing;
        }

        var w=root.ChildrenNamed(WindowNode).SingleOrDefault(n=>n.Attr("Name")==WindowName)
            ??throw new UiLayoutException(ui.FileName,root.Path,$"沒有 Window {WindowName}");
        BaseSurface=Get(w,"BaseSurf");FrameSurface=Get(w,"FrameSurf");MoveableHeight=w.TryInt("Moveable")??0;

        var g=w.Child("ActionIconGrid");
        int gx=g.Int("X"),gy=g.Int("Y"),rows=g.Int("Row"),cols=g.Int("Column"),gap=g.Int("UnitGap"),uw=g.Int("UnitWidth"),uh=g.Int("UnitHeight");
        if(rows<=0||cols<=0||uw<=0||uh<=0||gap<0)throw new UiLayoutException(ui.FileName,g.Path,"格子的列、欄、格寬高或間距不合法");
        Grid=new(new(gx,gy),new(uw,uh),new(uw+gap,uh+gap),cols,rows);
        GridDrag=g.TryInt("Drag")==1;
        if(g.ChildrenNamed("Image").FirstOrDefault(n=>n.Attr("Name")=="Selection_Image") is {} sel)SelectionSurface=Get(sel,"Surf");
        if(g.ChildrenNamed("ScrollBar").FirstOrDefault() is {} sb){
            var at=new Point(gx+sb.Int("X"),gy+sb.Int("Y"));
            Part? B(string name)=>sb.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")==name) is {} b?PartOf(b,at):null;
            var bg=sb.ChildrenNamed("Image").FirstOrDefault(n=>n.Attr("Name")=="Background_Image");
            Scroll=new(at,new(sb.TryInt("Width")??0,sb.TryInt("Height")??0),sb.TryInt("MaxValue")??0,sb.TryInt("ScrollStart")??0,sb.TryInt("ScrollHeight")??0,
                bg==null?at:new(at.X+(bg.TryInt("X")??0),at.Y+(bg.TryInt("Y")??0)),bg==null?null:Get(bg,"Surf"),B("Up_Button"),B("Down_Button"),B("Move_Button"));
        }
        if(w.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")=="Exit_Button") is {} ex)Exit=PartOf(ex,Point.Empty);
        if(w.ChildrenNamed("Control").FirstOrDefault(c=>c.Attr("Name")=="Tab_Control") is {} tc){
            var at=new Point(tc.TryInt("X")??0,tc.TryInt("Y")??0);
            Tabs=tc.ChildrenNamed("RadioButton").Where(r=>TabCategories.ContainsKey(r.Attr("Name")??"")).Select(r=>new Tab(PartOf(r,at),TabCategories[r.Attr("Name")!])).ToArray();
        }

        Actions=ActionCatalog.Parse(actions);
        Missing=missing;
        var size=BaseSurface??FrameSurface;
        if(size==null){Unavailable=$"{ui.FileName}: 底圖與外框都缺，無法定出視窗範圍";return;}
        Size=new(size.Width,size.Height);
        Origin=new(UiRoot.LogicalWidth/2-Size.Width/2,ScreenY);
        Unavailable=null;
    }

    // ---- open / close
    public bool Open(){
        if(!Available)return false;
        if(!open){open=true;ActiveTab=0;ScrollRow=0;Selected=null;pressed=null;hover=null;}
        return true;
    }
    public void Toggle(){if(open)Close();else Open();}
    public void Close(){open=false;pressed=null;hover=null;pressedInside=false;}
    void IUiClosablePanel.Close()=>Close();

    /// <summary>可開的動作窗（已註冊、版面載入成功、沒停用）；否則 null。</summary>
    public static ActionWindowPanel? Window(UiRoot? root)=>
        root?.Find(PanelId) is ActionWindowPanel p&&p.Available&&!root.Faulted.Contains(p)?p:null;

    public bool IsDragHandle(Point location)=>Visible&&MoveableHeight>0&&Bounds.Contains(location)&&location.Y<Origin.Y+MoveableHeight&&PartAt(location)==null;
    public void MoveTo(Point location){Origin=location;placed=true;}
    public bool Moved=>placed;

    public void Update(IUiGameState state){
        if(!state.InWorld&&open)Close();
    }

    // ---- geometry (canvas coordinates)
    Rectangle At(Part p)=>p.Local.IsEmpty?Rectangle.Empty:new(Origin.X+p.Local.X,Origin.Y+p.Local.Y,p.Local.Width,p.Local.Height);
    IEnumerable<Part> Parts{get{
        foreach(var t in Tabs)yield return t.Button;
        if(Exit!=null)yield return Exit;
        if(Scroll?.Up!=null)yield return Scroll.Up;
        if(Scroll?.Down!=null)yield return Scroll.Down;
    }}
    public Part? PartAt(Point p)=>Parts.FirstOrDefault(x=>At(x).Contains(p));
    /// <summary>第 slot 格（0 起、本頁、列優先）在畫布上的矩形。</summary>
    public Rectangle CellRect(int slot){var r=Grid.CellRect(slot);return new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);}
    /// <summary>畫布上的點落在哪個動作（格子之間的間距、空格＝null）。</summary>
    public ActionEntry? ActionAt(Point p){
        if(Grid.SlotAt(new(p.X-Origin.X,p.Y-Origin.Y)) is not int slot)return null;
        int index=ScrollRow*Grid.Columns+slot;var list=TabActions;
        return index<list.Count?list[index]:null;
    }
    /// <summary>本頁第 slot 格的動作（沒有＝null）。</summary>
    public ActionEntry? ActionInSlot(int slot){int index=ScrollRow*Grid.Columns+slot;var list=TabActions;return slot>=0&&slot<Grid.PageSize&&index<list.Count?list[index]:null;}
    /// <summary>拇指位置（相對視窗原點）：Move_Button.Y 到 Down_Button.Y − 拇指高之間依捲動比例（推測）。</summary>
    public Point? ThumbOffset{get{
        if(Scroll?.Thumb is not {Base:{} tb} thumb)return null;
        int top=thumb.Offset.Y,bottom=Scroll.Down is {} d?d.Offset.Y-tb.Height:top;
        int max=MaxScroll;
        return new(thumb.Offset.X,max==0||bottom<=top?top:top+(bottom-top)*ScrollRow/max);
    }}

    public void SelectTab(int index){
        if(index<0||index>=Tabs.Count||index==ActiveTab)return;
        ActiveTab=index;ScrollRow=0;Selected=null;
    }
    public void ScrollBy(int rows)=>ScrollRow=Math.Clamp(ScrollRow+rows,0,MaxScroll);

    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        canvas.DrawSurface(BaseSurface,Origin.X,Origin.Y);
        canvas.DrawSurface(FrameSurface,Origin.X,Origin.Y);
        for(int i=0;i<Tabs.Count;i++){
            var b=Tabs[i].Button;
            var s=i==ActiveTab?b.Push??b.Base:hover==b?b.Highlight??b.Base:b.Base;
            canvas.DrawSurface(s,Origin.X+b.Offset.X,Origin.Y+b.Offset.Y);
        }
        if(Scroll is {} sc){
            canvas.DrawSurface(sc.Background,Origin.X+sc.BackgroundAt.X,Origin.Y+sc.BackgroundAt.Y);
            foreach(var b in new[]{sc.Up,sc.Down})if(b!=null)canvas.DrawSurface(pressed==b&&pressedInside?b.Push??b.Base:b.Base,Origin.X+b.Offset.X,Origin.Y+b.Offset.Y);
            if(ThumbOffset is Point th)canvas.DrawSurface(sc.Thumb!.Base,Origin.X+th.X,Origin.Y+th.Y);
        }
        for(int slot=0;slot<Grid.PageSize;slot++){
            if(ActionInSlot(slot) is not {} a)continue;
            var cell=CellRect(slot);
            ItemIconSource.Draw(canvas,Icons.Get(a.Icon),cell.Location);
            if(a==Selected&&SelectionSurface!=null)canvas.DrawSurface(SelectionSurface,cell.X-1,cell.Y-1);
        }
        if(Exit!=null)canvas.DrawSurface(pressed==Exit&&pressedInside?Exit.Push??Exit.Base:Exit.Base,Origin.X+Exit.Offset.X,Origin.Y+Exit.Offset.Y);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        bool inside=Bounds.Contains(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:
                hover=PartAt(e.Location);
                if(pressed!=null)pressedInside=At(pressed).Contains(e.Location);
                return inside||pressed!=null;
            case UiMouseKind.Wheel:
                if(!inside)return false;
                if(e.Delta!=0)ScrollBy(e.Delta>0?-1:1);
                return true;
            case UiMouseKind.Down:{
                if(!inside)return false;
                if(e.Button!=MouseButtons.Left)return true;
                var part=PartAt(e.Location);
                for(int i=0;i<Tabs.Count;i++)if(Tabs[i].Button==part){SelectTab(i);return true;}
                if(part!=null){
                    pressed=part;pressedInside=true;
                    if(part==Scroll?.Up)ScrollBy(-1);else if(part==Scroll?.Down)ScrollBy(1);
                    return true;
                }
                if(ActionAt(e.Location) is {} a){
                    if(e.Clicks>=2&&a==Selected)Execute(a,commands);
                    else Selected=a;
                }
                return true;
            }
            case UiMouseKind.Up:{
                var was=pressed;bool fire=was!=null&&At(was).Contains(e.Location);
                pressed=null;pressedInside=false;
                if(fire&&was==Exit)Close();
                return was!=null||inside;
            }
            default:return inside;
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;

    /// <summary>雙擊一格（見檔頭「執行」）。不送封包；送封包的動作都要經過 IUiActionCommands。</summary>
    public ActionRunResult Execute(ActionEntry action,IUiCommands commands){
        var result=Run(action,commands);
        LastRun=(action,result);
        if(result==ActionRunResult.Unhandled&&unhandled.Add(action.Name))
            UiResourceLog.Write($"action-ui {action.Name}（{action.Control}）尚未接上，未執行"+(ActionRoutes.Of(action.Name).Note is {} note?"："+note:""));
        return result;
    }

    ActionRunResult Run(ActionEntry action,IUiCommands commands){
        if(LegacyWindowPanel.FindCommands<IUiActionCommands>(commands)?.RunAction(action)==true)return ActionRunResult.Bridge;
        var route=ActionRoutes.Of(action.Name);
        if(action.Name==OpenBookmarkAction&&root?.Find(BookmarkWindowPanel.PanelId) is BookmarkWindowPanel bm&&!root.Faulted.Contains(bm)){
            var r=MenuButtonsPanel.ToggleWindow(root,bm,()=>{if(!bm.Visible)bm.Toggle();},bm.Toggle);
            if(r is UiToggleResult.Opened or UiToggleResult.Closed)return ActionRunResult.Window;
        }
        if(route is {Kind:ActionRouteKind.ChatLine,Argument:{} prefix}&&ChatLine(prefix,commands))return ActionRunResult.ChatLine;
        if(route is {Kind:ActionRouteKind.Toggle,Argument:ActionRoutes.HpMpRecoveryToggle}&&root!=null&&OptionPanel.HpMpWindow(root) is {} hp){
            hp.Apply(hp.Settings with{ShowRecovery=!hp.Settings.ShowRecovery});
            return ActionRunResult.Toggled;
        }
        // R11-B：地面名稱／透明屍體 → 選項窗宿主（產品＝ClientForm.OptionWindowHost，同一個值、同一個存檔）。宿主沒接上 → Unhandled。
        if(route is {Kind:ActionRouteKind.Toggle,Argument:{} option}&&ActionRoutes.DisplayToggles.Contains(option)&&ToggleOption(option))return ActionRunResult.Toggled;
        if(SocialActions.Contains(action.Name)){commands.ShowDevTools(EmoteDevToolsPage);return ActionRunResult.DevTools;}
        return ActionRunResult.Unhandled;
    }

    /// <summary>R11-B：經 OptionPanel 的宿主把顯示開關反過來；沒有選項窗、宿主沒接上、宿主不持有或不套用 → false。</summary>
    bool ToggleOption(string option)=>root?.Find(OptionPanel.PanelId) is OptionPanel {Host:{} host}&&host.Value(option) is bool on&&host.Set(option,!on);

    /// <summary>
    /// R10-E：把指令前綴（"/addbuddy " 等）放進原生聊天輸入列（取代草稿，同 FriendPanel.Whisper），不送封包；之後打名稱、Enter 走既有
    /// ChatPanel → SendChat → 斜線指令。沒有聊天窗、輸入列送出中、輸入列正等提示的答覆（R10-F）、聊天列不能開 → false。
    /// </summary>
    bool ChatLine(string prefix,IUiCommands commands){
        if(root?.Find(ChatPanel.WindowName) is not ChatPanel chat||root.Faulted.Contains(chat)||chat.Input.Pending||chat.Prompt!=null||UiChatGameState.Commands(commands) is not {} c)return false;
        if(!chat.Input.Open){
            if(!c.BeginChatInput())return false;
            chat.Input.Begin(false);
        }
        chat.Input.Clear();
        foreach(char ch in prefix)chat.Input.Type(ch);
        return chat.Input.Text==prefix;
    }
}

/// <summary>
/// R10-E（T389／#904）：ActionUI-c.xml 的常駐開窗鈕 ActionUI_Button（根元素下、MostTop="1"，FU8§4.2）。
/// 已驗證（XML）：(774,470)、BaseSurf 5156、HighlightSurf 5157、Tooltip String 3118、MouseEvent OpenButtonPressed；
///   FU8§4.3：OpenButtonPressed 收 514（放開）與 515（雙擊）→ 開關動作窗（vtable+76）。
/// 推測（真機再確認，M33）：在世界中常駐顯示；畫在 Bar 層（不進視窗堆疊，全域 ESC 不關它）；滑鼠停留畫 HighlightSurf。
/// R11-B（#958，#952 裁定 ④ 照原版做）：滑鼠停留時畫 Tooltip String（3118）的字串表文字（ServerMessageText.Table＝本機 string-c.tbl；
///   表讀不到時只有 3118 用 PakViewer 查到的原文「行動視窗」，其他編號不畫）。框寬高、字色、字的起點照 SummonPanel（sub_B0DAF0、
///   sub_C00BF0）；位置（鈕正下方置中、夾進畫面）、黑底不透明是推測（ActionUI-c.xml 的 Tooltip 沒有 X／Y／BackColor）。
/// 圖與座標由 ActionWindowPanel 讀（同一份 XML）；動作窗不可用或缺底圖 → 不畫、不收點擊。
/// IUiClosablePanel.Close 只清掉按住／停留狀態（鈕本身常駐，不會被關掉）。
/// </summary>
public sealed class ActionOpenButtonPanel(UiRoot? root=null):IUiClosablePanel {
    [UiPanel(192)] internal static UiPanelRegistration Registration=>new(PanelId,ActionWindowPanel.HostLayout,r=>new ActionOpenButtonPanel(r));   // R10-E (#904): ActionUI_Button, data from ActionWindowPanel
    public const string PanelId="ActionUI_Button";
    /// <summary>本機 string-c.tbl（Text.idx，PakViewer 解出）第 3118 筆；kor25 相同，t89 是「行動視窗 Ctrl+D」。</summary>
    public const int TooltipStringId=3118;
    public const string TooltipFallback="行動視窗";
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Bar;
    bool inWorld,hover,pressed;
    ActionWindowPanel? Window=>ActionWindowPanel.Window(root);
    /// <summary>鈕本身（收點擊的範圍）。</summary>
    public Rectangle ButtonRect=>Window is {OpenButton:{} spec,OpenButtonBase:{} s}?new(spec.Location,new Size(s.Width,s.Height)):Rectangle.Empty;
    /// <summary>繪製範圍：鈕＋顯示中的提示框。</summary>
    public Rectangle Bounds{get{var b=ButtonRect;return Tooltip is {} tip&&!b.IsEmpty?Rectangle.Union(b,tip.Box):b;}}
    public bool Visible=>inWorld&&!ButtonRect.IsEmpty;
    public bool Hover=>hover;

    /// <summary>字串表第 id 筆（去掉色碼）；表不存在或該筆空白時只有 3118 用查到的原文，其他 → null（不畫）。</summary>
    public static string? StringText(int id){
        if(ServerMessageText.Table is {} t&&id>=0&&id<t.Count&&ServerMessageText.DisplayText(t[id]).Trim() is {Length:>0} s)return s;
        return id==TooltipStringId?TooltipFallback:null;
    }
    /// <summary>XML Tooltip 的文字（沒有 Tooltip 或查不到 → null）。</summary>
    public string? TooltipText=>Window?.OpenButton?.TooltipString is int id?StringText(id):null;
    /// <summary>滑鼠停留在鈕上時的提示框（邏輯座標）；沒有停留或沒有文字 → null。</summary>
    public SummonTooltip? Tooltip{get{
        if(!hover||!inWorld||TooltipText is not {} text)return null;
        var b=ButtonRect;if(b.IsEmpty)return null;
        var lines=SummonPanel.TooltipLines(text);var s=SummonPanel.TooltipSize(lines);var canvas=UiRoot.Canvas;
        int x=Math.Clamp(b.X+(b.Width-s.Width)/2,canvas.Left,Math.Max(canvas.Left,canvas.Right-s.Width));
        int y=Math.Clamp(b.Bottom,canvas.Top,Math.Max(canvas.Top,canvas.Bottom-s.Height));
        // 鈕在畫面下緣、下方放不下時改放在鈕的上方（推測）。
        if(y<b.Bottom)y=Math.Max(canvas.Top,b.Top-s.Height);
        return new(new(new(x,y),s),lines);
    }}

    public void Load(UiLayout layout,SurfCatalog surfs){}
    public void Update(IUiGameState state){inWorld=state.InWorld;if(!inWorld)Close();}
    public void Close(){hover=false;pressed=false;}

    public void Paint(UiCanvas canvas){
        if(!Visible||Window is not {} w)return;
        var at=w.OpenButton!.Location;
        canvas.DrawSurface(hover?w.OpenButtonHighlight??w.OpenButtonBase:w.OpenButtonBase,at.X,at.Y);
        if(Tooltip is {} tip){
            canvas.Graphics.FillRectangle(Brushes.Black,tip.Box);
            for(int i=0;i<tip.Lines.Count;i++)
                canvas.DrawText(tip.Lines[i],tip.Box.X+SummonPanel.TooltipTextX,tip.Box.Y+SummonPanel.TooltipTextY+i*SummonPanel.TooltipLineHeight,SummonPanel.TooltipTextColor);
        }
    }

    /// <summary>開關動作窗（同 Ctrl+D：經 UiRoot.Toggle 進視窗堆疊）。</summary>
    void Fire(){
        if(root==null||Window is not {} w)return;
        MenuButtonsPanel.ToggleWindow(root,w,()=>w.Open(),w.Toggle);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible){hover=false;pressed=false;return false;}
        bool inside=ButtonRect.Contains(e.Location);   // 提示框不收點擊（滑鼠移到框上＝離開鈕，框就收起）
        switch(e.Kind){
            case UiMouseKind.Move:hover=inside;return inside||pressed;
            case UiMouseKind.Down:
                if(!inside)return false;
                if(e.Button==MouseButtons.Left){pressed=true;if(e.Clicks>=2)Fire();}   // 515 雙擊也切換一次
                return true;
            case UiMouseKind.Up:{
                bool was=pressed;pressed=false;
                if(was&&inside&&e.Button==MouseButtons.Left)Fire();   // 514 放開
                return was||inside;
            }
            default:return inside;
        }
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
