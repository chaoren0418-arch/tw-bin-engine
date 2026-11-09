using P06.NativeUi;
namespace P06;

// M7-S（T219／#526）：移除「武器修理與 PK 查詢」專用 Form 與 HUD「修理」鈕（WIN§2 T134：原版沒有專用視窗）。
// 修理從 NPC 對話頁的 fix 動作開始（C118）；可修清單在原生 TRADE_WINDOW 模式 6 顯示（原生 UI 開啟時），
// 原生 UI 關閉時列在同一個 NPC 對話頁（過渡）；空清單開 touma3／gayle5。PK 查詢走對話頁的 checkpk（M7-R）。
// R2-E（T262／#623）：同一個 TRADE_WINDOW 也接上商店（ShopTradeSource，模式 0／1）；修理列的數量上限＝背包中該物品數量（FU3§6.2）。
public sealed partial class ClientForm {
    void InitWeaponRepairService() {
        login.WireWeaponRepairDialog();
        var timer=new System.Windows.Forms.Timer{Interval=200};
        timer.Tick+=(_,_)=>AttachTradeWindow();timer.Start();
        Disposed+=(_,_)=>timer.Dispose();
    }
    /// <summary>原生 UI 啟動後，把 TRADE_WINDOW 接上修理與商店；原生 UI 沒有啟動時，修理清單改列在 NPC 對話頁、商店照舊用舊清單。</summary>
    void AttachTradeWindow() {
        var panel=NativeUiActive?nativeUiRoot?.Find(TradeWindowPanel.PanelId) as TradeWindowPanel:null;
        if(panel!=null&&panel.Source==null){
            panel.Source=new TradeWindowSources(new RepairTradeSource(this),new ShopTradeSource(login,ResolveNpcText,()=>World.Walking));
            // R4-C（#715）：圖示先用 R4-B 的 ItemIconSource（執行期讀原客戶端 Sprite "<icon>.tbt"），讀不到再用舞台的背包 PNG。
            var icons=ItemIconSource.FromRoot(nativeUiRoot);
            panel.IconSource=id=>icons.Get(id)?.Bitmap??ItemIcon(id);
        }
        bool native=panel!=null&&!nativeUiRoot!.Faulted.Contains(panel);
        login.World.WeaponRepairNativeWindow=native;login.World.ShopNativeWindow=native;
        if(panel?.Visible==true)Invalidate();
    }
    sealed class RepairTradeSource(ClientForm form):ITradeWindowSource {
        public TradeListView? Current=>form.login.State==LoginState.InWorld?RepairTrade.View(form.login.World,form.ResolveNpcText):null;
        // R4-C（T303／#715，TWP§1.6）：多選時每一列各送一包 C20（WeaponRepair.cs SendWeaponRepairBatch；一列時同單件路徑）。
        public void Submit(IReadOnlyList<ShopChoice> choices,int revision){if(!form.World.Walking&&choices.Count>0)_=form.login.SendWeaponRepairBatch(RepairTrade.Items(choices),revision);}
        public void Close()=>form.login.World.CloseWeaponRepair();
    }
}

/// <summary>修理（TRADE_WINDOW 模式 6）的列：名稱與可修狀態取自 WeaponRepairWindow，費用取自 S83 的耐久 × 200，上限＝背包數量。</summary>
public static class RepairTrade {
    /// <summary>左鈕送出的列 → 要修的物件（每列一件、一包 C20；數量欄只表示有選，不重複送同一件）。</summary>
    public static uint[] Items(IReadOnlyList<ShopChoice> choices)=>choices.Where(c=>c.Count>0&&c.Id!=0).Select(c=>c.Id).Distinct().ToArray();
    public static TradeListView? View(WorldSession world,Func<string,string>? names=null) {
        if(world.WeaponRepairWindow(names) is not {} v)return null;
        var costs=world.WeaponRepairRows.ToDictionary(r=>r.ObjectId,r=>r.Cost);
        var inventory=world.Inventory;
        var items=v.Rows.Select(r=>{
            var item=inventory.FirstOrDefault(i=>i.ObjectId==r.Id);uint max=item?.Count??0;
            return new TradeListItem(r.Id,r.Name,item?.IconId??0,costs.GetValueOrDefault(r.Id),max,r.Enabled&&max>0);
        }).ToArray();
        return new(v.Mode,v.Revision,items,ShopTrade.Holding(world),true,true,v.Notice);
    }
}
