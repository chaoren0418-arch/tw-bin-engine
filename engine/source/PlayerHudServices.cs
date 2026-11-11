namespace P06;

public sealed class PlayerHudServiceTracker {
    PlayerHudStamp stamp;int npc=-1,trade=-1,shop=-1,target=-1,query=-1;
    /// <param name="queryRevision">T433（#991，classic-ui-audit G7）：S39 伺服器主動頁（排名、/buddy、伺服器頁）的 QueryRevision。</param>
    /// <param name="queryActive">伺服器主動頁正在顯示（QueryVisible 且 QueryActionable）；選單本機 HTML 頁不算，它自己會開 NPC 分頁。</param>
    public string? Observe(PlayerHudStamp current,int npcRevision,bool npcActive,int tradeRevision,bool tradeActive,int shopRevision,bool shopActive,int targetRevision,bool shopTarget,int queryRevision=-1,bool queryActive=false){
        if(current!=stamp){stamp=current;npc=trade=shop=target=query=-1;}
        string? show=null;
        if(npc!=npcRevision&&npcActive||query!=queryRevision&&queryActive)show="NPC";
        if(shop!=shopRevision&&shopActive||target!=targetRevision&&shopTarget)show="個人商店";
        if(trade!=tradeRevision&&tradeActive)show="交易";
        npc=npcRevision;trade=tradeRevision;shop=shopRevision;target=targetRevision;query=queryRevision;return show;
    }
}
public sealed partial class ClientForm {
    readonly PlayerHudServiceTracker playerServices=new();
    // Reuse server-guarded native service controls in a normal player window, never behind F9.
    public void ShowPlayerService(string name){
        if(!PlayerHudActive||normalPanels==null)return;
        var page=normalPanels.TabPages.Cast<TabPage>().FirstOrDefault(p=>p.Text==name);
        if(page==null)return;
        playerHud?.CloseDrawer();ClearInput("player-service");playerLegacyOpen=true;
        normalPanels.SelectedTab=page;normalPanels.AccessibleName="玩家服務視窗："+name;
        SyncNormalPanels();normalPanels.BringToFront();SyncPlayerViewport();
    }
    void SyncPlayerServices(){
        if(!PlayerHudActive)return;
        var w=login.World;
        string? page=playerServices.Observe(PlayerHudActions.Stamp(login),w.InteractionRevision,w.InteractionKind!="closed"||w.InteractionHasResult,w.TradeRevision,w.TradeState!="idle",w.PrivateShopRevision,w.PrivateShopPhase is not ("idle" or "closed"),w.TargetRevision,w.Target?.ShopActive==true,
            w.QueryRevision,w.QueryVisible&&w.QueryActionable);
        if(page!=null)ShowPlayerService(page);
        npcTalk.AccessibleName="與選取目標對話";npcClose.AccessibleName="關閉 NPC 對話";npcSubmit.AccessibleName=npcSubmit.Text;
        foreach(var pair in shopInputs){var row=w.ShopRows.FirstOrDefault(x=>x.Id==pair.Id);pair.Input.AccessibleName=(row==null?"商品":ResolveNpcText(row.Name))+"，數量";}
        tradeInventory.AccessibleName="交易物品";tradeQuantity.AccessibleName="交易數量";tradeMine.AccessibleName="我方提供的物品";tradeTheirs.AccessibleName="對方提供的物品";
        privateShopQuantity.AccessibleName="個人商店本次數量";privateShopSubmit.AccessibleName="確認個人商店交易一次";
    }
}
