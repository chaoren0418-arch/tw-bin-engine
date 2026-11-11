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
// R7-G（T346／#809）補查（Capstone 讀同一份 BIN；PakViewer 讀本機 Text.pak 的 string-c.tbl）：
//   - 897＝「不能一次販賣8樣以上的物品。」，沒有 %s，所以 sprintf(897, 934「販售」) 的結果就是 897 原文。
//     sub_A2BB90 VA 0xA2BC1C：sub_A11280(897) 永遠回 0（VA 0xA11280 xor al,al），所以一定印；
//     sub_7DC850(字, dword_1369D70[24], dword_1369D70[14], 7, 897, 0)＝聊天列、色 24（同公告板 189）。
//   - 1028＝「這是高價物品。請再次進行確認，確定要購買嗎？(Y/N)」：不是拒絕，是**確認**。sub_4FD770 呼叫
//     sub_AC98E0(dword_13CE3F4, 字, 1, 回呼 sub_4FE4A0, 1, 0)＝聊天輸入列提示（FU7§1.5：同刪除確認，不是彈出框）。
//     回呼 sub_4FE4A0：答覆是 Y（sub_A58380）→ sub_DE8E90（物件 0x1541D0C）組 op 0x59 並送出；否則不送。
//   引擎：897 用既有的聊天列入口 WorldSession.AddBoardChatNotice（色 24）；1028 放進聊天輸入列提示（playerChatNotice，
//   同 ItemTransfer 的刪除確認），並記下待確認的一包（PrivateShopConfirmCurrent）。答 Y 的入口是 AnswerPrivateShopConfirm；
//   R10-F（T390／#905）起，聊天輸入列的答覆由 PlayerChat.SendPlayerChat 分派到這裡（刪除確認之後），Y 送出原來那一包。
//   原生 ChatPanel 也畫這個提示（TradeUi.cs 的 ChatLinePrompts.Of：1028 的 Key 是 "shop1028"，Esc＝AnswerPrivateShopConfirm("")，不送）。
// 推測／未做（標明）：
//   - 1028 另外也寫到狀態列（Set(State,text)，引擎做法；原生聊天列接上後可考慮拿掉，#905 報告）。
//   - 3.81 S140 不帶貨幣種類；原版 TradeBox +164 預設 7（金幣），所以「持有」與 ADENA 圖照金幣畫。
public static class PrivateShopTrade {
    public const long MaxBuyTotal=99999;
    public const uint BuyQuantityCap=9999;
    public const ushort TotalMessage=1028,RowsMessage=897,RowsArgument=934;
    /// <summary>897 的 sub_7DC850 色：dword_1369D70[24]／[14]（VA 0xA2BC39–0xA2BC56）。</summary>
    public const int RowsColor=24,RowsSecondColor=14;
    /// <summary>本機 string-c.tbl 的原文（PakViewer 讀 Text.pak；字串表讀不到時用）。</summary>
    public const string TotalFallback="這是高價物品。請再次進行確認，確定要購買嗎？(Y/N)",RowsFallback="不能一次販賣8樣以上的物品。";
    /// <summary>TRADE_WINDOW 一列的數量上限：買＝min(9999, 清單上限)；賣＝清單上限（已含持有量與裝備檢查）。</summary>
    public static uint Limit(PrivateShopRow row,bool sell)=>sell?row.Maximum:Math.Min(BuyQuantityCap,row.Maximum);
    public static long Total(IEnumerable<(PrivateShopRow Row,uint Count)> rows)=>rows.Sum(r=>(long)r.Row.Price*r.Count);
    static string? Table(int id)=>ServerMessageText.Table is {} t&&id<t.Count&&t[id].Trim().Length>0?t[id]:null;
    public static string TotalText()=>Table(TotalMessage) is {} s?ServerMessageText.DisplayText(s):TotalFallback;
    public static string RowsText(){
        if(Table(RowsMessage) is {} s){string arg=Table(RowsArgument) is {} a?ServerMessageText.DisplayText(a):"";int at=s.IndexOf("%s",StringComparison.Ordinal);return ServerMessageText.DisplayText(at>=0?s[..at]+arg+s[(at+2)..]:s);}
        return RowsFallback;
    }
    /// <summary>模式 13 合計 &gt;99999：要先答 1028 的 Y/N（sub_A2BA10）。</summary>
    public static bool NeedsConfirm(bool sell,IReadOnlyList<(PrivateShopRow Row,uint Count)> rows)=>!sell&&Total(rows)>MaxBuyTotal;
    /// <summary>模式 14 一次 &gt;8 筆：不送，897 印在聊天列（sub_A2BB90）。</summary>
    public static bool TooManyRows(bool sell,IReadOnlyList<(PrivateShopRow Row,uint Count)> rows)=>sell&&rows.Count>PrivateShopPackets.MaxRows;
    /// <summary>直接送出前要停下的兩種情形（13 合計 &gt;99999 先確認、14 一次 &gt;8 筆不送）；回提示文字，可直接送時回 null。</summary>
    public static string? Refusal(bool sell,IReadOnlyList<(PrivateShopRow Row,uint Count)> rows)=>
        TooManyRows(sell,rows)?RowsText():NeedsConfirm(sell,rows)?TotalText():null;
}

/// <summary>R7-G（#809）：等待聊天輸入列答覆的 1028 高價確認；清單版本、連線世代、世界、目標任一變了就失效。</summary>
public sealed record PrivateShopConfirm(int Id,ShopChoice[] Choices,int Revision,int Generation,int Epoch,int TargetRevision,uint Owner,string Text);

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
    public Task<PrivateShopResult> SendPrivateShopBatch(IReadOnlyList<ShopChoice> choices,int revision)=>SendPrivateShopBatch(choices,revision,false);
    async Task<PrivateShopResult> SendPrivateShopBatch(IReadOnlyList<ShopChoice> choices,int revision,bool confirmed){
        World.CheckPrivateShop();
        int generation=SessionGeneration,epoch=World.WorldEpoch,target=World.TargetRevision;uint owner=World.TargetId??0;bool sell=World.PrivateShopSell;
        bool Ready()=>PrivateShopAccess&&World.PrivateShopNativeWindow&&!World.PrivateShopPending&&revision==World.PrivateShopRevision&&generation==SessionGeneration&&epoch==World.WorldEpoch&&target==World.TargetRevision&&owner!=0&&owner==World.TargetId&&sell==World.PrivateShopSell;
        if(privateShopSending||sendMovementGate==null||!Ready())return PrivateShopResult.NotSent;
        if(World.PrivatePurchaseRows(choices) is not {} rows)return PrivateShopResult.NotSent;
        // R7-G（#809）：897 印在聊天列（色 24）；1028 是輸入列的 Y/N 確認，答 Y 才送（confirmed）。兩者都不關窗、不動版本。
        if(PrivateShopTrade.TooManyRows(sell,rows)){string text=PrivateShopTrade.RowsText();World.PrivateShopRefused(text);World.AddBoardChatNotice(text,PrivateShopTrade.RowsColor);Set(State,text);return PrivateShopResult.NotSent;}
        if(!confirmed&&PrivateShopTrade.NeedsConfirm(sell,rows)){
            string text=PrivateShopTrade.TotalText();
            privateShopConfirm=new(++privateShopConfirmIds,choices.ToArray(),revision,generation,epoch,target,owner,text);
            World.PrivateShopRefused(text);playerChatNotice=text;Set(State,text);return PrivateShopResult.NotSent;
        }
        privateShopConfirm=null;
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

    PrivateShopConfirm? privateShopConfirm;int privateShopConfirmIds;
    /// <summary>R7-G（#809）：目前等待答覆的 1028 高價確認（清單、世代、世界、目標都還是同一個時才有效）。</summary>
    public PrivateShopConfirm? PrivateShopConfirmCurrent{get{
        World.CheckPrivateShop();
        if(privateShopConfirm is {} p&&(p.Generation!=SessionGeneration||p.Epoch!=World.WorldEpoch||p.TargetRevision!=World.TargetRevision||p.Owner!=World.TargetId
            ||p.Revision!=World.PrivateShopRevision||World.PrivateShopPhase!="list"||World.PrivateShopSell||!World.PrivateShopNativeWindow))privateShopConfirm=null;
        return privateShopConfirm;
    }}
    /// <summary>
    /// 1028 的答覆（sub_4FE4A0）：Y 開頭（YesNoProtocol.Accepts，同 sub_A58380）→ 用原來那一包再走一次 SendPrivateShopBatch（所有驗證照舊，只跳過 1028）；
    /// 其他答覆不送。沒有待確認時回 NotSent。呼叫者：輸入列的分派 PlayerChat.SendPlayerChat（R10-F #905），
    /// 以及原生聊天列提示的 Esc（TradeUi.cs，傳空字串＝不送）。
    /// </summary>
    public async Task<PrivateShopResult> AnswerPrivateShopConfirm(string typed){
        if(PrivateShopConfirmCurrent is not {} p)return PrivateShopResult.NotSent;
        privateShopConfirm=null;
        if(!YesNoProtocol.Accepts(typed)){if(playerChatNotice==p.Text)playerChatNotice="";Changed?.Invoke();return PrivateShopResult.NotSent;}
        if(playerChatNotice==p.Text)playerChatNotice="";
        return await SendPrivateShopBatch(p.Choices,p.Revision,true);
    }
    /// <summary>R7-G（#809）：原生私商窗（模式 13／14）開著＝WinForms 私商面板讓位。</summary>
    public bool PrivateShopLegacyYields=>PrivateShopTradeWindow()!=null;
}

/// <summary>私商的 TRADE_WINDOW 資料來源（模式 13／14）：內容取自 LoginConnection.PrivateShopTradeWindow；送出走 SendPrivateShopBatch，關閉走 ClosePrivateShop。</summary>
public sealed class PrivateShopTradeSource(LoginConnection login,Func<string,string> names,Func<bool> busy):ITradeWindowSource {
    public TradeListView? Current=>login.PrivateShopTradeWindow(names);
    public void Submit(IReadOnlyList<ShopChoice> choices,int revision){if(busy()||choices.Count==0)return;_=login.SendPrivateShopBatch(choices,revision);}
    public void Close()=>login.World.ClosePrivateShop();
}
