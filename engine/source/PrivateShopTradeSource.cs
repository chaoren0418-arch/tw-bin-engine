using P06.NativeUi;
namespace P06;

// R6-C（T333／#779）：私商清單（S140，TW16 op 29 S_PERSONAL_SHOP_LIST）改用原生 TRADE_WINDOW 模式 13（買）／14（賣），
// 照 ShopTradeSource 的做法（FU7§8；WIN§2 T102；TWP§1.1–1.6）。
// 已驗證（Capstone 讀 TW1608262200.bin，SHA-256 e12949cf…09641a2；VA＝FO＋0x400C00）：
//   - 送出 sub_A2BA80（VA 0xA2BAE0–0xA2BB14）：模式 13 → sub_A2BA10(0)、模式 14 → sub_A2BA10(1)；失敗時窗不關。
//   - 模式 13（sub_A2BA10，VA 0xA2BA1C–0xA2BA38）：sub_A2A9D0 合計＝Σ 數量(+0x5D0)×單價(+0x5CC)，>99999（0x1869F）不送，
//     呼叫 sub_4FD770 顯示字串 1028（0x404）。
//   - 模式 14（sub_A2BB90，VA 0xA2BBCF–0xA2BC66）：sub_A2AA40 數出的筆數 >8 不送，sprintf(字串 897, 字串 934) 印在聊天列（sub_7DC850）。
//   - 數量上限（sub_A2AD00，VA 0xA2ADAE–0xA2AE1F）：min(TradeBox+0x94, TradeItem+0x128)；模式 13 的 +0x94＝9999、模式 14 為 0（＝只看物品數量）。
//     依單價的 9999／999／20／1 只在模式 0（VA 0xA2AE22 起每段都比 [+0x14]==0）。
//   - 封包：SP C_Result mode_buypc／mode_sellpc（每筆 8 byte；見 PrivateShopPackets.SubmitMany）。
// 推測／未做（標明）：
//   - 字串 897 以 C sprintf 套 934；這裡把 897 的第一個 %s 換成 934 的字。實際字樣待真機核對。
//   - 897 原版印在聊天列、1028 原版開提示框；引擎放在私商提示與狀態列（不另開窗）。
//   - 3.81 S140 不帶貨幣種類；原版 TradeBox +164 預設 7（金幣），所以「持有」與 ADENA 圖照金幣畫。
public static class PrivateShopTrade {
    public const long MaxBuyTotal=99999;
    public const uint BuyQuantityCap=9999;
    public const ushort TotalMessage=1028,RowsMessage=897,RowsArgument=934;
    /// <summary>TRADE_WINDOW 一列的數量上限：買＝min(9999, 清單上限)；賣＝清單上限（已含持有量與裝備檢查）。</summary>
    public static uint Limit(PrivateShopRow row,bool sell)=>sell?row.Maximum:Math.Min(BuyQuantityCap,row.Maximum);
    public static long Total(IEnumerable<(PrivateShopRow Row,uint Count)> rows)=>rows.Sum(r=>(long)r.Row.Price*r.Count);
    static string? Table(int id)=>ServerMessageText.Table is {} t&&id<t.Count&&t[id].Trim().Length>0?t[id]:null;
    public static string TotalText()=>Table(TotalMessage) is {} s?ServerMessageText.DisplayText(s):$"合計超過 {MaxBuyTotal:N0}，本次未送出（原服字串 {TotalMessage}）。";
    public static string RowsText(){
        if(Table(RowsMessage) is {} s){string arg=Table(RowsArgument) is {} a?ServerMessageText.DisplayText(a):"";int at=s.IndexOf("%s",StringComparison.Ordinal);return ServerMessageText.DisplayText(at>=0?s[..at]+arg+s[(at+2)..]:s);}
        return $"一次最多只能選 {PrivateShopPackets.MaxRows} 筆，本次未送出（原服字串 {RowsMessage}／{RowsArgument}）。";
    }
    /// <summary>原版不送的兩種情形（13 合計 &gt;99999、14 一次 &gt;8 筆）；回提示文字，可送時回 null。</summary>
    public static string? Refusal(bool sell,IReadOnlyList<(PrivateShopRow Row,uint Count)> rows)=>
        sell?(rows.Count>PrivateShopPackets.MaxRows?RowsText():null):(Total(rows)>MaxBuyTotal?TotalText():null);
}

public sealed partial class WorldSession {
    /// <summary>true＝原生 TRADE_WINDOW 已接上，私商買賣列只在原生窗（模式 13／14）；false＝舊的 WinForms／web 清單。</summary>
    public bool PrivateShopNativeWindow {get;set;}
    /// <summary>只改提示、不動版本（版本一變，原生窗會把已輸入的數量清掉）。</summary>
    internal void PrivateShopRefused(string notice)=>PrivateShopNotice=notice;
    /// <summary>
    /// 原生窗送出的多筆：每筆對到目前清單的列（列號＝TradeListItem.Id）、數量 1..上限；賣出另核對背包持有量（同一物件跨列加總）與未裝備。
    /// 任一筆不合就整包不送（回 null）。
    /// </summary>
    internal (PrivateShopRow Row,uint Count)[]? PrivatePurchaseRows(IReadOnlyList<ShopChoice> choices){
        CheckPrivateShop();
        if(PrivateShopPhase!="list"||!PrivateShopIdentity||choices.Count==0||choices.Select(c=>c.Id).Distinct().Count()!=choices.Count)return null;
        var rows=new List<(PrivateShopRow,uint)>();var held=new Dictionary<uint,long>();
        foreach(var c in choices){
            var row=PrivateShopRows.FirstOrDefault(r=>(uint)r.Id==c.Id);
            if(row==null||c.Count==0||c.Count>PrivateShopTrade.Limit(row,PrivateShopSell))return null;
            if(PrivateShopSell){
                if(!inventory.TryGetValue(row.ItemId,out var item)||ItemEquipped(item)==true)return null;
                long sum=held.GetValueOrDefault(row.ItemId)+c.Count;if(sum>item.Count)return null;held[row.ItemId]=sum;
            }
            rows.Add((row,c.Count));
        }
        return rows.ToArray();
    }
}

public sealed partial class LoginConnection {
    /// <summary>私商的 TRADE_WINDOW 內容；原生窗沒接上、清單不是 list 或是空的時為 null（窗不開）。</summary>
    public TradeListView? PrivateShopTradeWindow(Func<string,string>? names=null){
        World.CheckPrivateShop();
        if(State!=LoginState.InWorld||!World.PrivateShopNativeWindow||World.PrivateShopPhase!="list"||World.PrivateShopRows.Length==0)return null;
        bool sell=World.PrivateShopSell;var name=names??(s=>s);
        var items=World.PrivateShopRows.Select(r=>{uint max=PrivateShopTrade.Limit(r,sell);return new TradeListItem((uint)r.Id,name(r.Name),r.Icon,r.Price,max,max>0);}).ToArray();
        return new(sell?TradeWindowPanel.ModePrivateSell:TradeWindowPanel.ModePrivateBuy,World.PrivateShopRevision,items,ShopTrade.Holding(World),true,
            PrivateShopAccess&&!privateShopSending&&!World.PrivateShopPending,World.PrivateShopDisplayNotice);
    }
    /// <summary>
    /// 原生窗左鈕：所有數量 &gt;0 的列一包 C161（SubmitMany）。原版不送的兩種情形只改提示；其餘驗證同單筆 SendPrivateShop
    /// （店主、距離、版本、忙碌、背包），送出閘門前再驗一次。
    /// </summary>
    public async Task<PrivateShopResult> SendPrivateShopBatch(IReadOnlyList<ShopChoice> choices,int revision){
        World.CheckPrivateShop();
        int generation=SessionGeneration,epoch=World.WorldEpoch,target=World.TargetRevision;uint owner=World.TargetId??0;bool sell=World.PrivateShopSell;
        bool Ready()=>PrivateShopAccess&&World.PrivateShopNativeWindow&&!World.PrivateShopPending&&revision==World.PrivateShopRevision&&generation==SessionGeneration&&epoch==World.WorldEpoch&&target==World.TargetRevision&&owner!=0&&owner==World.TargetId&&sell==World.PrivateShopSell;
        if(privateShopSending||sendMovementGate==null||!Ready())return PrivateShopResult.NotSent;
        if(World.PrivatePurchaseRows(choices) is not {} rows)return PrivateShopResult.NotSent;
        if(PrivateShopTrade.Refusal(sell,rows) is string refusal){World.PrivateShopRefused(refusal);Set(State,refusal);return PrivateShopResult.NotSent;}
        byte[] packet=PrivateShopPackets.SubmitMany(owner,sell,rows);
        var sender=sendMovementGate;int lease=++privateShopLease;privateShopSending=true;bool admitted=false;
        try{
            bool Valid(){World.CheckPrivateShop();return lease==privateShopLease&&Ready()&&World.PrivatePurchaseRows(choices) is {} again&&again.SequenceEqual(rows);}
            bool sent=await sender(packet,Valid,()=>{admitted=true;World.BeginPrivateSubmit();Set(State,World.PrivateShopNotice);});
            return admitted?(sent&&generation==SessionGeneration?PrivateShopResult.Accepted:PrivateShopResult.Unknown):PrivateShopResult.NotSent;
        }
        catch(Exception ex){var result=admitted?PrivateShopResult.Unknown:PrivateShopResult.NotSent;if(generation==SessionGeneration){Cancel();Set(LoginState.Faulted,PrivateShopFeedback.Text(result,"")+" 傳送錯誤："+ex.Message);}return result;}
        finally{if(lease==privateShopLease)privateShopSending=false;}
    }
}

/// <summary>私商的 TRADE_WINDOW 資料來源（模式 13／14）：內容取自 LoginConnection.PrivateShopTradeWindow；送出走 SendPrivateShopBatch，關閉走 ClosePrivateShop。</summary>
public sealed class PrivateShopTradeSource(LoginConnection login,Func<string,string> names,Func<bool> busy):ITradeWindowSource {
    public TradeListView? Current=>login.PrivateShopTradeWindow(names);
    public void Submit(IReadOnlyList<ShopChoice> choices,int revision){if(busy()||choices.Count==0)return;_=login.SendPrivateShopBatch(choices,revision);}
    public void Close()=>login.World.ClosePrivateShop();
}
