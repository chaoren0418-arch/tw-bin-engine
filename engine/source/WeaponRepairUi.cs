using P06.NativeUi;
namespace P06;

// M7-S（T219／#526）：移除「武器修理與 PK 查詢」專用 Form 與 HUD「修理」鈕（WIN§2 T134：原版沒有專用視窗）。
// 修理從 NPC 對話頁的 fix 動作開始（C118）；可修清單在原生 TRADE_WINDOW 模式 6 顯示（原生 UI 開啟時），
// 原生 UI 關閉時列在同一個 NPC 對話頁（過渡）；空清單開 touma3／gayle5。PK 查詢走對話頁的 checkpk（M7-R）。
public sealed partial class ClientForm {
    void InitWeaponRepairService() {
        login.WireWeaponRepairDialog();
        var timer=new System.Windows.Forms.Timer{Interval=200};
        timer.Tick+=(_,_)=>AttachTradeWindow();timer.Start();
        Disposed+=(_,_)=>timer.Dispose();
    }
    /// <summary>原生 UI 啟動後，把 TRADE_WINDOW 接上修理狀態；原生 UI 沒有啟動時，清單改列在 NPC 對話頁。</summary>
    void AttachTradeWindow() {
        var panel=NativeUiActive?nativeUiRoot?.Find(TradeWindowPanel.PanelId) as TradeWindowPanel:null;
        if(panel!=null&&panel.Source==null)panel.Source=new RepairTradeSource(this);
        login.World.WeaponRepairNativeWindow=panel!=null&&!nativeUiRoot!.Faulted.Contains(panel);
        if(panel?.Visible==true)Invalidate();
    }
    sealed class RepairTradeSource(ClientForm form):ITradeWindowSource {
        public TradeWindowView? Current=>form.login.State==LoginState.InWorld?form.login.World.WeaponRepairWindow(form.ResolveNpcText):null;
        public void Submit(uint id,int revision){if(!form.World.Walking)_=form.login.SendWeaponRepair(false,id,revision);}
        public void Close()=>form.login.World.CloseWeaponRepair();
    }
}
