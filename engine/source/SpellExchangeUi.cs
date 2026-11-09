using P06.NativeUi;
namespace P06;

// R3-F（T286／#679）：拿掉 M7-G／R2-F 的暫行 WinForms 學魔法窗。op 175（以物品學魔法）與 op 44（金幣學魔法）的清單
// 改在原生 TRADE_WINDOW 模式 7 顯示（資料來源 SpellTradeSource.cs；TWP§1.1、§1.6；SS2§1.2）。
// 接法：TradeWindowPanel 的來源由 WeaponRepairUi.cs 的 AttachTradeWindow 接成 TradeWindowSources（修理＋商店）；
// 本檔在 NPC 面板同步時把 SpellTradeSource 放到最前面（不改 TradeWindowPanel、WeaponRepairUi 或 UiPanelRegistry）。
// 模式 7 的列不畫物品圖示：原版用魔法圖示表 word_136D548（未對應到 surf），物品圖示會畫錯圖（推測，見報告）。
// 原生 UI 沒有啟動時：op 175 沒有清單窗（只有 NPC 面板的提示文字）；op 44 照舊列在 NPC 對話面板（NpcServices／NpcUi 的過渡清單）。
public sealed partial class ClientForm {
    SpellTradeSource? spellTradeSource;
    /// <summary>每次 NPC 面板同步時呼叫：原生 TRADE_WINDOW 已接上來源後，把學魔法來源插到最前面（只插一次）。</summary>
    void SyncSpellExchangeWindow(){
        var panel=NativeUiActive?nativeUiRoot?.Find(TradeWindowPanel.PanelId) as TradeWindowPanel:null;
        if(panel?.Source is not TradeWindowSources sources||sources.Sources.Any(s=>s is SpellTradeSource))return;
        spellTradeSource??=new SpellTradeSource(login,()=>World.Walking);
        panel.Source=new TradeWindowSources([spellTradeSource,..sources.Sources]);
        var icons=panel.IconSource;
        panel.IconSource=id=>panel.View?.Mode==TradeWindowPanel.ModeSpell?null:icons?.Invoke(id);
    }
}
