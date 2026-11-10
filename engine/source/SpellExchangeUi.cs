using P06.NativeUi;
namespace P06;

// R3-F（T286／#679）：拿掉 M7-G／R2-F 的暫行 WinForms 學魔法窗。op 175（以物品學魔法）與 op 44（金幣學魔法）的清單
// 改在原生 TRADE_WINDOW 模式 7 顯示（資料來源 SpellTradeSource.cs；TWP§1.1、§1.6；SS2§1.2）。
// 接法：TradeWindowPanel 的來源由 WeaponRepairUi.cs 的 AttachTradeWindow 接成 TradeWindowSources（修理＋商店）；
// 本檔在 NPC 面板同步時把 SpellTradeSource 放到最前面（不改 TradeWindowPanel、WeaponRepairUi 或 UiPanelRegistry）。
// R9-G（T367／#851，FU9§11）：模式 7 的列照原版畫物品圖示——word_136D548 是物品圖示編號表（SpellTradeProtocol.SpellIcons），
// 與背包同一條畫圖路（sub_891DF0）；所以直接沿用 WeaponRepairUi.cs 接上的 IconSource（ItemIconSource "<編號>.tbt"），不再包裝。
// 原生 UI 沒有啟動時：op 175 沒有清單窗（只有 NPC 面板的提示文字）；op 44 照舊列在 NPC 對話面板（NpcServices／NpcUi 的過渡清單）。
public sealed partial class ClientForm {
    SpellTradeSource? spellTradeSource;
    /// <summary>每次 NPC 面板同步時呼叫：原生 TRADE_WINDOW 已接上來源後，把學魔法來源插到最前面（只插一次）。</summary>
    void SyncSpellExchangeWindow(){
        var panel=NativeUiActive?nativeUiRoot?.Find(TradeWindowPanel.PanelId) as TradeWindowPanel:null;
        if(panel?.Source is TradeWindowSources sources&&!sources.Sources.Any(s=>s is SpellTradeSource)){
            spellTradeSource??=new SpellTradeSource(login,()=>World.Walking);
            panel.Source=new TradeWindowSources([spellTradeSource,..sources.Sources]);
        }
        // R4-C（T303／#715）：原生 TRADE_WINDOW 會畫 op 44 金幣學魔法清單時，NPC 面板的舊清單（ShopRows 數量框＋提交鈕）讓位：
        // 整個 WinForms NPC 面板這一輪不顯示（NpcUi.SyncInteraction 每次都重算 Visible，所以清單結束後自動回來）。
        // 以下情況照舊顯示 NPC 面板（備援入口）：原生 UI 關閉、TRADE_WINDOW 故障或沒接上學魔法來源、使用者關掉了這份清單、
        // op 175 清單在前（窗畫的是以物品學魔法）、或 WinForms 面板同時要顯示查詢頁（原生 HYPER_TEXT_WINDOW 不可用時）。
        if(SpellBuyOwnedByNativeWindow(panel))npcPanel.Visible=false;
    }
    bool SpellBuyOwnedByNativeWindow(TradeWindowPanel? panel){
        if(panel==null||nativeUiRoot==null||nativeUiRoot.Faulted.Contains(panel)||spellTradeSource==null)return false;
        if(panel.Source is not TradeWindowSources s||!s.Sources.Contains(spellTradeSource))return false;
        var w=login.World;
        if(login.State!=LoginState.InWorld||w.QueryVisible&&NativeHyperText()==null)return false;
        return SpellTradeOwnership.Owned(spellTradeSource.Current,w.SpellBuyTradeWindow(),panel.Dismissed);
    }
}

/// <summary>R4-C：學魔法舊清單讓位的判斷（拆出來給離線測試）。</summary>
public static class SpellTradeOwnership {
    /// <summary>窗目前要畫的就是 op 44 清單（不是 op 175）、而且這份清單沒有被關掉。</summary>
    public static bool Owned(TradeListView? shown,TradeListView? buy,Func<TradeListView,bool> dismissed)=>
        shown!=null&&buy!=null&&!SpellTradeProtocol.IsExchangeRevision(shown.Revision)&&shown.Mode==TradeWindowPanel.ModeSpell&&shown.Revision==buy.Revision&&!dismissed(shown);
}
