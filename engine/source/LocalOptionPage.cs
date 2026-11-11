namespace P06;

// ---- R13-A（T442／#1021，gap-matrix 15.15.5／15.15.6 列 1:144）：選單開的本機選項頁（help、option1、option2／option2p）----
// 原版：選單鈕 6／Ctrl+V 開的是客戶端自己的 HTML 頁（Text.pak），頁上的 <a link>／<img link> 在本機換頁，
// 頁上的 HyperAct 由客戶端自己執行（HyperAct 工廠 sub_BB5180 註冊的名稱，伺服器不認這些字串）。
// 執行函式（RTTI .?AVHyperAct* 的 vtable 槽 1，T1021 用 IDA 在 TW16 IDB 對過；反編譯見 docs/reports/issue-1021.md）：
//   accepttrade    HyperActAcceptTrade   vtable 0x11A98F0 → sub_581790：byte_136E99D 取反，重開 option13（FU7§1.1）
//   getall         HyperActGetAll        vtable 0x11A94A0 → sub_582E30：byte_135C1E4 取反，重開 option13（world-objects.md 第 81 行）
//   bubbleon       HyperActBubble        vtable 0x11A94AC → sub_581B90：byte_13AAF28 取反，重開 option12（FU16）
//   showMargetMsg  HyperActShowMarketMsg vtable 0x11A956C → sub_584BE0：byte_136E99F 取反，重開 option12（FU16）
//   showhpmp       HyperActShowHPMP      vtable 0x11A9518 → sub_584B50：byte_138BE7C 取反，重開 option11（ui-layout.md）
//   showfloathpmp  HyperActShowFloatHPMP vtable 0x11A9524 → sub_584B10：**不改任何旗標**，只重開 option11
//   dowhisper      HyperActWhisper       vtable 0x11A94D0 → sub_585440 → sub_4F9A90 → sub_F4A0A0（同 /whisper on|off：184,2）
//   dotradechat    HyperActDoTradeChat   vtable 0x11A94C4 → sub_5828D0 → sub_4F9A60 → sub_F4A7B0（同 /tradechat on|off：184,6）
//   重開頁 = sub_874130 清頁後呼叫 HYPER_TEXT_WINDOW vtable +96（頁名, 0, 0）。
// 引擎：本機執行後重畫「目前這頁」（任務規定；原版重開的頁就是動作所在的頁，option11／12／13）。
// 另外沿用引擎已有、已查證的客戶端 HyperAct（伺服器頁上本來就這樣處理，不送 C125）：
//   optionb 的 addbuddy／delbuddy／buddy（R10-C／R11-C，BuddyHyperAction）、optionplp 的 surrender／cease／pledgefocusadd（R7-D，HtmlClientAction）。
//   這幾個會開頁內名稱框或送自己的封包，做完不重畫（重畫會讓名稱框失效）。
// 停用（照原樣顯示、不送 C125；原版怎麼處理沒查到或引擎沒有對應設定，推測）：dochat（sub_582750 → sub_F45680，unk_1656948 沒追）、
//   randomParty（byte_13CE519）、cPartyReject（byte_13CE51A）、telcon、infracon、option11 的其他顯示選項、option14 的視窗模式、
//   optionpl／optionpt／optiona／optionap 的血盟、組隊、同盟動作等其他所有動作。
public enum LocalOptionAct { None, AcceptTrade, GetAll, BubbleOn, ShowMarketMsg, ShowHpMp, ShowFloatHpMp, Whisper, TradeChat, Client }

public static class LocalOptionPage {
    /// <summary>頁上有停用的動作時附加的說明。</summary>
    public const string DisabledNotice="本機選項頁：原版處理方式沒有查到的選項維持停用，不送出（推測）。";
    static readonly Dictionary<string,LocalOptionAct> acts=new(StringComparer.OrdinalIgnoreCase){
        ["accepttrade"]=LocalOptionAct.AcceptTrade,["getall"]=LocalOptionAct.GetAll,["bubbleon"]=LocalOptionAct.BubbleOn,
        ["showMargetMsg"]=LocalOptionAct.ShowMarketMsg,["showhpmp"]=LocalOptionAct.ShowHpMp,["showfloathpmp"]=LocalOptionAct.ShowFloatHpMp,
        ["dowhisper"]=LocalOptionAct.Whisper,["dotradechat"]=LocalOptionAct.TradeChat};
    /// <summary>已查證、引擎能執行的動作名稱（原版大小寫）。</summary>
    public static IReadOnlyCollection<string> Names=>acts.Keys;
    /// <summary>動作字串 → 本機動作；不是本組的回 None（頁上停用）。動作名稱比對不分大小寫（同 checkpk／accepttrade）。</summary>
    public static LocalOptionAct Of(string? action)=>action!=null&&acts.TryGetValue(action.Trim(),out var a)?a
        :BuddyHyperActs.Classify(action)!=null||HtmlActions.Normalize(action)!=null?LocalOptionAct.Client:LocalOptionAct.None;
    /// <summary>引擎此刻能不能執行；showhpmp 要有原生 UI 的 HP/MP 視窗（--classic-ui 沒有），沒有就停用。</summary>
    public static bool Available(LocalOptionAct act)=>act switch{
        LocalOptionAct.None=>false,
        LocalOptionAct.ShowHpMp=>HpMpPanel?.Invoke()!=null,
        _=>true};

    /// <summary>
    /// 本機頁模式的選項：link 保留模板的判定（目標頁存在才可點）；action 只有 Available 的才可點，其他停用。
    /// 模板本身停用的（dynamic 頁、超長字串、帶 param）一律維持停用。
    /// </summary>
    public static NpcPage Prepare(NpcPage page,Func<LocalOptionAct,bool>? available=null){
        available??=Available;
        bool disabled=false;
        var options=page.Options.Select(o=>{
            if(o.Kind=="link"||!o.Enabled)return o;
            var act=o.Kind=="action"?Of(o.Value):LocalOptionAct.None;
            bool on=act!=LocalOptionAct.None&&available(act);   // 不是本組的動作一律停用，不看 available
            if(!on)disabled=true;
            return on?o:o with{Enabled=false};
        }).ToArray();
        string notice=string.Join("\n",new[]{page.Limitation,disabled?DisabledNotice:""}.Where(s=>s.Length>0));
        return page with{Options=options,Limitation=notice};
    }

    /// <summary>原生 UI 的 HP/MP 視窗來源（showhpmp 切換、var 1023／1024 讀值）；null＝沒有。</summary>
    internal static Func<NativeUi.HpMpPanel?>? HpMpPanel{get;set;}
    /// <summary>
    /// ApplyNativeUiStart 成功時掛上（gap-matrix 15.15.6 ③）：HyperTextOptionVars.HpMp＝() =&gt; OptionPanel.HpMpWindow(root)?.Settings，
    /// 同一個來源也給 showhpmp 切換用。
    /// </summary>
    public static void AttachHpMp(Func<NativeUi.HpMpPanel?> panel){
        HpMpPanel=panel;
        NativeUi.HyperTextOptionVars.HpMp=()=>panel()?.Settings;
    }
    /// <summary>showhpmp：byte_138BE7C（HpMpSettings.ShowCharacterHpMp）取反；沒有 HP/MP 視窗回 false。</summary>
    public static bool ToggleShowHpMp(){
        if(HpMpPanel?.Invoke() is not {} h)return false;
        h.Apply(h.Settings with{ShowCharacterHpMp=!h.Settings.ShowCharacterHpMp});
        return true;
    }
}

public sealed partial class WorldSession {
    /// <summary>R13-A：目前的查詢頁是選單開的本機頁（help／option*）。link 本機換頁、HyperAct 本機執行，不送 C125。</summary>
    public bool QueryLocal {get;private set;}
    /// <summary>
    /// 顯示本機頁（取代已顯示的查詢頁；NPC 對話／交易不動）。沒有參數、物件 id 為 0（不會組出 C125）。
    /// 頁版本 +1，舊頁的點擊因此失效。
    /// </summary>
    internal void ShowLocalOptionPage(string key){
        var page=RenderHyperText(key,[],Actor?.Name??"",null);
        QueryPage=LocalOptionPage.Prepare(page);QueryHtml=key;queryEpoch=WorldEpoch;queryObject=0;queryArguments=[];
        QueryActionable=true;QueryLocal=true;QueryRevision++;
    }
    /// <summary>動作做完後重畫同一頁（值要重新代入 var 1002–1029）。頁已換掉、關掉或世界改變就不畫，回 false。</summary>
    internal bool RedrawLocalOptionPage(string key,int epoch){
        if(!QueryLocal||!QueryVisible||epoch!=WorldEpoch||!string.Equals(QueryHtml,key,StringComparison.Ordinal))return false;
        ShowLocalOptionPage(key);return true;
    }
    /// <summary>本機頁上某個可用 action 的字串；不是本機頁、不存在或版本不符為 null。</summary>
    internal string? LocalOptionActionValue(int option,int revision)=>QueryLocal?FindQueryOption(option,revision,"action")?.Value:null;
}

public sealed partial class LoginConnection {
    /// <summary>
    /// R13-A：本機頁的 HyperAct。只執行 LocalOptionPage 的動作，不送 C125；做完重畫目前這頁。
    /// 回 true＝動作已執行（dowhisper／dotradechat 是 C26 已送出）。
    /// </summary>
    internal async Task<bool> LocalOptionAction(int option,int revision,int epoch){
        if(World.LocalOptionActionValue(option,revision) is not {} value)return false;
        var act=LocalOptionPage.Of(value);
        if(!LocalOptionPage.Available(act))return false;
        string key=World.QueryHtml;var stamp=PlayerHudActions.Stamp(this);
        bool done;
        switch(act){
        case LocalOptionAct.AcceptTrade:done=await ToggleAcceptTrade(stamp);break;
        case LocalOptionAct.GetAll:World.TogglePickupAll();done=true;break;
        case LocalOptionAct.BubbleOn:HeadBubbleOptions.BubbleFrames=!HeadBubbleOptions.BubbleFrames;done=true;break;
        case LocalOptionAct.ShowMarketMsg:HeadBubbleOptions.ShopMessages=!HeadBubbleOptions.ShopMessages;done=true;break;
        case LocalOptionAct.ShowHpMp:done=LocalOptionPage.ToggleShowHpMp();break;
        case LocalOptionAct.ShowFloatHpMp:done=true;break;   // sub_584B10：只重開頁
        case LocalOptionAct.Whisper:done=await SetChatChannel(stamp,ChatChannelKind.Whisper,!World.ChatChannels.Whisper);break;
        case LocalOptionAct.TradeChat:done=await SetChatChannel(stamp,ChatChannelKind.Trade,!World.ChatChannels.Trade);break;
        case LocalOptionAct.Client:
            // 同 QueryOption 對伺服器頁的分派（query:true＝名稱框開在這一頁）；不重畫。
            if(BuddyHyperAction(value,query:true) is {} buddy)return await buddy;
            return HtmlClientAction(value,query:true) is {} html&&await html;
        default:return false;
        }
        if(World.RedrawLocalOptionPage(key,epoch))Changed?.Invoke();
        return done;
    }
}
