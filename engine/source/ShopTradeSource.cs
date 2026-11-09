using P06.NativeUi;
namespace P06;

// R2-E（T262／#623）：NPC 商店改用原生 TRADE_WINDOW（gap-matrix 11.5；TWP§1.1、§1.2、§1.5、§1.6、§1.9；FU3§6）。
// 已驗證：TradeWindow 保留的模式 0（買，BUY 1992）／1（賣，SELL 1998）送 op 89 `hcdch`＋每筆 8 byte，與 3.81 C161 C_Result
//   （D npc, C type, C size, C 未用, 每筆 D id, D count）逐欄相符（TWP§1.6）；買的單筆上限依單價 9999／999／20／1（TWP§1.5）。
// 已驗證：賣出確認清單（sub_A2C660，字串 3133）只由 S250 子碼 162 填寫，3.81 不送 162 → 不跳確認（FU3§6.1）。
// 收包不變：S70（NpcInteraction.cs）與 S65（NpcServices.cs）照舊解析進同一份商店列；送出照舊走 LoginConnection.Interact("shop")
//   （ValidateShop／ValidateService 驗證後送 C161）。本檔只多一個開關：原生窗接上時，商店的買賣列只給 TRADE_WINDOW，
//   不再給 WinForms／web 的舊清單（ShopRows 回空陣列、CanSubmitShop／CanSubmitService 為 false）。
// 推測（標明）：TW16 的 NPC 商店走 CNpcShop（XML 窗），3.81 窗的實際外觀未比對（TWP§1.8）；賣的模式也顯示單價（TradeBox +153 未查）。
public sealed partial class WorldSession {
    /// <summary>true＝原生 TRADE_WINDOW 已接上，商店買賣清單只在原生窗顯示；false＝舊清單（原生 UI 關閉時的過渡）。</summary>
    public bool ShopNativeWindow {get;set;}
    /// <summary>目前是不是由原生窗接手的商店清單（買或賣）。</summary>
    public bool ShopNativeActive=>ShopNativeWindow&&InteractionKind is "buy" or "sell";
    /// <summary>商店的 TRADE_WINDOW 內容；沒有可用清單時為 null（窗不開；空清單照舊只在 NPC 面板留文字）。</summary>
    public TradeListView? ShopTradeWindow(Func<string,string>? names=null){
        CheckInteraction();CheckNpcSale();
        if(!ShopNativeActive||!InteractionValid||shopRows.Length==0)return null;
        bool buy=InteractionKind=="buy";var name=names??(s=>s);
        var items=shopRows.Select(r=>{
            uint max=buy?Math.Min(TradeWindowPanel.BuyLimit(r.Price),r.Available):r.Available;
            return new TradeListItem(r.Id,name(r.Name),r.Icon,r.Price,max,max>0);
        }).ToArray();
        bool adena=shopCurrency==7;
        return new(buy?TradeWindowPanel.ModeBuy:TradeWindowPanel.ModeSell,InteractionRevision,items,adena?ShopTrade.Holding(this):null,true,
            buy?ShopSubmittable:ServiceSubmittable,InteractionNotice);
    }
}

public static class ShopTrade {
    /// <summary>「持有」＝背包裡的金幣（TWP§1.4：貨幣 7 讀背包金幣）；背包未載入為 null。</summary>
    public static long? Holding(WorldSession world)=>world.InventoryLoaded?world.Inventory.Where(LeftStatusState.IsAdena).Aggregate(0L,(n,i)=>n+i.Count):null;
}

/// <summary>商店的 TRADE_WINDOW 資料來源：內容取自 WorldSession.ShopTradeWindow；送出與關閉走既有的 Interact 路徑。</summary>
public sealed class ShopTradeSource(LoginConnection login,Func<string,string> names,Func<bool> busy):ITradeWindowSource {
    public TradeListView? Current=>login.State==LoginState.InWorld?login.World.ShopTradeWindow(names):null;
    public void Submit(IReadOnlyList<ShopChoice> choices,int revision){
        if(busy()||choices.Count==0)return;var w=login.World;
        _=login.Interact("shop",revision,w.WorldEpoch,w.TargetRevision,0,choices);
    }
    public void Close(){var w=login.World;_=login.Interact("close",w.InteractionRevision,w.WorldEpoch,w.TargetRevision);}
}
