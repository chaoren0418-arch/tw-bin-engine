namespace P06;
public enum PrivateShopResult {NotSent,Accepted,Unknown}
public static class PrivateShopFeedback {
    public static string Text(PrivateShopResult result,string notice)=>result switch {
        PrivateShopResult.Accepted=>notice,
        PrivateShopResult.Unknown=>"請求可能已送出，結果未知；請核對原服背包與貨幣，不要依未送出推論重試。沒有自動重送。",
        _=>"商店操作已過期、數量無效、目標不可見、忙碌或此店查詢未結案；本次未送出。"
    };
}
public sealed record PrivateShopRow(int Id,byte Order,uint ItemId,ushort Icon,string Name,uint Price,uint ListedCount,uint Maximum);
public sealed record PrivateShopView(int Generation,int Epoch,int Revision,int TargetRevision,uint ActorId,uint OwnerId,string OwnerName,string Phase,bool Sell,string Notice,string ServerText,int? SelectedRow,uint Quantity,bool CanQuery,bool CanChoose,bool CanSubmit,PrivateShopRow[] Rows,bool CanQueryBuy,bool CanQuerySell);
public static class PrivateShopPackets {
    public static byte[] Query(uint owner,bool sell){if(owner==0)throw new ArgumentException("owner");return LoginPackets.Pad([47,(byte)(sell?1:0),..BitConverter.GetBytes(owner)]);}
    // One row only. The PC sell count is CH (24 bits), followed by the server's C order.
    // DE sell consumes D item + CH count and ignores the final order byte for this single row.
    public static byte[] Submit(uint owner,bool sell,PrivateShopRow row,uint count){
        if(owner==0||count==0||count>row.Maximum||count>int.MaxValue||(sell&&(row.ItemId==0||count>0xFFFFFF)))throw new ArgumentException("private shop quantity/identity");
        uint item=sell?row.ItemId:row.Order;uint packed=sell?count|((uint)row.Order<<24):count;
        return LoginPackets.Pad([161,..BitConverter.GetBytes(owner),(byte)(sell?1:0),1,0,..BitConverter.GetBytes(item),..BitConverter.GetBytes(packed)]);
    }
}
public sealed partial class WorldSession {
    public string PrivateShopPhase {get;private set;}="idle";
    public string PrivateShopNotice {get;private set;}="請明確選取可見的個人商店，再查詢買入或售出清單。";
    public string PrivateShopServerText {get;private set;}="";
    public int PrivateShopRevision {get;private set;}
    public bool PrivateShopSell {get;private set;}
    public PrivateShopRow[] PrivateShopRows {get;private set;}=[];
    public int? PrivateShopSelectedRow {get;private set;}
    public uint PrivateShopQuantity {get;private set;}
    readonly HashSet<(uint Owner,bool Sell)> privateUncertainQueries=[];
    public bool PrivateShopQueryUncertain(bool sell)=>TargetId is uint id&&privateUncertainQueries.Contains((id,sell));
    public string PrivateShopDisplayNotice=>PrivateQueryLocationNotice+"\n"+(!PrivateShopNotice.Contains(PrivateQueryUncertainNotice,StringComparison.Ordinal)&&(PrivateShopQueryUncertain(false)||PrivateShopQueryUncertain(true))?PrivateShopNotice+"\n"+PrivateQueryUncertainNotice:PrivateShopNotice);
    void AbandonPrivateQuery(){if(PrivateShopPhase=="querying"&&privateOwner!=0)privateUncertainQueries.Add((privateOwner,PrivateShopSell));}
    const string PrivateQueryLocationNotice="查詢前請站進原服允許擺攤的區域，再靠近並選取店主；只在店主附近不保證能查詢。";
    const string PrivateQueryUncertainNotice="此店有未結案查詢；本連線停用對應買入／售出查詢。無回覆不等於空清單，原因尚未確定。請取消連線並重新登入，走進允許擺攤區域後重新選店、手動查詢；只移動或關閉清單不會解除限制。沒有自動重送，也不代表能辨別晚到回包或鎖定價格。";
    uint privateOwner,privateActor;int privateEpoch,privateTargetRevision;ushort privateX,privateY;long privateShopAt,privateAt;InventoryItem[] privateSellItems=[];
    internal Func<long> PrivateShopClock=()=>Environment.TickCount64;
    public bool PrivateShopPending=>PrivateShopPhase is "querying" or "submitted";
    public object PrivateShopSnapshotModel()=>new{phase=PrivateShopPhase,revision=PrivateShopRevision,ownerId=privateOwner,sell=PrivateShopSell,selectedRow=PrivateShopSelectedRow,rows=PrivateShopRows,notice=PrivateShopNotice,serverText=PrivateShopServerText};
    public bool NearbyPrivateOwner=>Ready&&!OwnDead&&Status?.Hp>0&&Actor is {} a&&Target is {Dead:false,ShopActive:true} t&&t.ObjectId!=a.ObjectId&&Math.Abs(a.X-t.X)<=3&&Math.Abs(a.Y-t.Y)<=3;
    bool PrivateShopIdentity=>NearbyPrivateOwner&&privateOwner==TargetId&&privateActor==Actor?.ObjectId&&privateActor==Status?.ObjectId&&privateEpoch==WorldEpoch&&privateTargetRevision==TargetRevision&&Target?.X==privateX&&Target?.Y==privateY&&Target?.ShopAt==privateShopAt;
    void ResetPrivateShop(){privateUncertainQueries.Clear();privateOwner=0;PrivateShopPhase="idle";PrivateShopRows=[];PrivateShopSelectedRow=null;PrivateShopQuantity=0;PrivateShopServerText="";PrivateShopNotice="請明確選取可見的個人商店，再查詢買入或售出清單。";PrivateShopRevision++;}
    internal void ClosePrivateShop(){AbandonPrivateQuery();privateOwner=0;PrivateShopPhase="idle";PrivateShopRows=[];PrivateShopSelectedRow=null;PrivateShopQuantity=0;PrivateShopNotice=PrivateShopQueryUncertain(PrivateShopSell)?PrivateQueryUncertainNotice:"已關閉舊清單；已送出的請求無法撤回，請核對原服背包與貨幣再重新查詢。";PrivateShopRevision++;}
    void InvalidatePrivateShop(string notice){AbandonPrivateQuery();PrivateShopRows=[];PrivateShopSelectedRow=null;PrivateShopQuantity=0;PrivateShopPhase="stale";PrivateShopNotice=notice;privateOwner=0;PrivateShopRevision++;}
    internal void CheckPrivateShop(){
        if(privateOwner!=0&&!PrivateShopIdentity){InvalidatePrivateShop("店主、距離、角色或世界已改變，舊清單失效；沒有重送。請重新選店並查詢。");return;}
        if(PrivateShopPhase=="list"&&PrivateShopSell&&!Inventory.SequenceEqual(privateSellItems)){InvalidatePrivateShop("背包已改變，原售出選項失效；請重新查詢。");return;}
        if(PrivateShopPhase is "querying" or "submitted" or "list"&&PrivateShopClock()-privateAt>=(PrivateShopPhase=="list"?30000:15000)){
            AbandonPrivateQuery();PrivateShopRows=[];PrivateShopSelectedRow=null;PrivateShopQuantity=0;PrivateShopPhase="timeout";PrivateShopNotice=PrivateShopQueryUncertain(PrivateShopSell)?PrivateQueryUncertainNotice:"清單或等待回覆已逾時，交易結果未知；請核對背包與貨幣後手動重新查詢。沒有自動重送。";PrivateShopRevision++;
        }
    }
    internal void BeginPrivateQuery(bool sell){privateOwner=TargetId!.Value;privateActor=Status!.ObjectId;privateEpoch=WorldEpoch;privateTargetRevision=TargetRevision;privateX=Target!.X;privateY=Target.Y;privateShopAt=Target.ShopAt;privateAt=PrivateShopClock();PrivateShopSell=sell;PrivateShopPhase="querying";PrivateShopRows=[];PrivateShopSelectedRow=null;PrivateShopQuantity=0;PrivateShopServerText="";PrivateShopNotice="已查詢原服個人商店清單；尚未進行買賣。";PrivateShopRevision++;}
    internal bool ChoosePrivateRow(int row,uint count){CheckPrivateShop();var found=PrivateShopRows.FirstOrDefault(r=>r.Id==row);if(PrivateShopPhase!="list"||found==null||count>found.Maximum)return false;PrivateShopSelectedRow=row;PrivateShopQuantity=count;PrivateShopRevision++;return true;}
    internal bool ValidPrivateOrder(int row,uint count){CheckPrivateShop();var found=PrivateShopRows.FirstOrDefault(r=>r.Id==row);return PrivateShopPhase=="list"&&PrivateShopIdentity&&PrivateShopSelectedRow==row&&count>0&&found!=null&&count<=found.Maximum&&(!PrivateShopSell||inventory.TryGetValue(found.ItemId,out var i)&&i.Count>=count&&i.Equipped!=true);}
    internal void BeginPrivateSubmit(){PrivateShopPhase="submitted";privateAt=PrivateShopClock();PrivateShopRows=[];PrivateShopSelectedRow=null;PrivateShopQuantity=0;PrivateShopNotice="已提交一次；尚未確認交易結果。背包與貨幣只依原服回包更新，不會自動重送。";PrivateShopRevision++;}
    void PrivateShopInventoryChanged(byte opcode){
        CheckPrivateShop();if(PrivateShopPhase!="submitted"||opcode is not (5 or 15 or 24 or 242 or 57 or 100))return;
        PrivateShopPhase="review";PrivateShopNotice="送出後收到原服背包更新；請核對物品與貨幣，這不代表整筆交易成功。需要再次操作時，請手動重新查詢。";PrivateShopRevision++;
    }
    void PrivateShopMessage(){CheckPrivateShop();if(PrivateShopPhase is not ("querying" or "submitted"))return;PrivateShopServerText=LastServerMessage??"";PrivateShopRevision++;}
    bool ReceivePrivateShop(byte opcode,LoginReader r,byte[] plain){
        if(opcode!=140)return false;byte mode=r.C();uint owner=r.D();int declared=r.H();if(mode>1||owner==0||declared>256)throw new ProtocolException("private shop header");
        bool sell=mode==1;var rows=new List<PrivateShopRow>();var ids=new HashSet<(byte,uint)>();
        // S140 sell H is the owner's buy-list size, not necessarily the emitted matching inventory rows.
        // Each emitted sell row is fixed 13 bytes; 8-byte padding leaves a unique row count.
        int count=sell?(plain.Length-8)/13:declared;if(count>2048||declared==0&&count!=0)throw new ProtocolException("private shop rows");
        // PC owner inventory may lack entries still counted by Hdeclared. A buy row
        // needs at least 14 bytes; <=7 remaining can only be padding (Finish validates it).
        // Preserve the emitted order, including gaps; never fabricate missing entries.
        for(int n=0;n<count&&(sell||r.Remaining>7);n++){
            byte order=r.C();if(!sell&&order>=declared)throw new ProtocolException("private buy order");uint item=0,listed,price;ushort icon=0;string name;
            if(sell){item=r.D();listed=r.D();price=r.D();if(item==0)throw new ProtocolException("private sell identity");var held=inventory.GetValueOrDefault(item);name=held?.Name??"未持有物件 "+item;icon=held?.IconId??0;}
            else{r.C();icon=r.H();listed=r.D();price=r.D();name=r.S();if(r.C()!=0)throw new ProtocolException("unproven private shop status");}
            if(listed>int.MaxValue||price>int.MaxValue||!ids.Add((order,item)))throw new ProtocolException("private shop row range/duplicate");
            uint max=Math.Min(listed,sell?0xFFFFFFu:int.MaxValue);if(price>0)max=Math.Min(max,(uint)int.MaxValue/price);
            if(sell){var held=inventory.GetValueOrDefault(item);max=held==null||held.Equipped==true?0:Math.Min(max,held.Count);}
            rows.Add(new(n,order,item,icon,name,price,listed,max));
        }
        r.Finish();CheckPrivateShop();if(PrivateShopPhase!="querying"||!PrivateShopIdentity||owner!=privateOwner||sell!=PrivateShopSell)return true;
        privateSellItems=Inventory;privateAt=PrivateShopClock();PrivateShopRows=rows.ToArray();PrivateShopPhase=rows.Count==0?"empty":"list";PrivateShopSelectedRow=null;PrivateShopQuantity=0;
        PrivateShopNotice=rows.Count==0?"原服沒有列出可操作的商品；可手動重新查詢。":"請選一筆商品並輸入數量，再確認一次。清單未提供貨幣種類；店主可能變更價格與庫存，原服以送出時狀態處理。";PrivateShopRevision++;return true;
    }
}
public sealed partial class LoginConnection {
    bool privateShopSending;int privateShopLease;
    void ResetPrivateShopSend(){privateShopLease++;privateShopSending=false;}
    bool PrivateShopAccess=>State==LoginState.InWorld&&World.NearbyPrivateOwner&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy&&!interactionSending&&World.InteractionKind is "closed" or "timedout";
    bool PrivateStamp(int generation,int epoch,int revision,int target,uint own)=>generation==SessionGeneration&&epoch==World.WorldEpoch&&revision==World.PrivateShopRevision&&target==World.TargetRevision&&own!=0&&own==World.Status?.ObjectId&&own==World.Actor?.ObjectId;
    public PrivateShopView PrivateShopSnapshot(){World.CheckPrivateShop();bool access=PrivateShopAccess&&!privateShopSending;return new(SessionGeneration,World.WorldEpoch,World.PrivateShopRevision,World.TargetRevision,World.Status?.ObjectId??0,World.TargetId??0,World.Target?.Name??"未選店主",World.PrivateShopPhase,World.PrivateShopSell,World.PrivateShopDisplayNotice,World.PrivateShopServerText,World.PrivateShopSelectedRow,World.PrivateShopQuantity,access&&!World.PrivateShopPending&&(!World.PrivateShopQueryUncertain(false)||!World.PrivateShopQueryUncertain(true)),access&&World.PrivateShopPhase=="list",access&&World.PrivateShopSelectedRow is int row&&World.ValidPrivateOrder(row,1),World.PrivateShopRows,access&&!World.PrivateShopPending&&!World.PrivateShopQueryUncertain(false),access&&!World.PrivateShopPending&&!World.PrivateShopQueryUncertain(true));}
    public bool ChoosePrivateShop(int row,uint count,PrivateShopView stamp){World.CheckPrivateShop();return !privateShopSending&&PrivateShopAccess&&PrivateStamp(stamp.Generation,stamp.Epoch,stamp.Revision,stamp.TargetRevision,stamp.ActorId)&&stamp.OwnerId==World.TargetId&&World.ChoosePrivateRow(row,count);}
    public bool ClosePrivateShop(PrivateShopView stamp){if(!PrivateStamp(stamp.Generation,stamp.Epoch,stamp.Revision,stamp.TargetRevision,stamp.ActorId))return false;World.ClosePrivateShop();return true;}
    public async Task<PrivateShopResult> SendPrivateShop(bool query,bool sell,int row,uint count,PrivateShopView stamp){
        World.CheckPrivateShop();bool Stamp()=>PrivateStamp(stamp.Generation,stamp.Epoch,stamp.Revision,stamp.TargetRevision,stamp.ActorId)&&stamp.OwnerId==World.TargetId;
        if(privateShopSending||!PrivateShopAccess||!Stamp()||World.PrivateShopPending||query&&World.PrivateShopQueryUncertain(sell)||sendMovementGate==null)return PrivateShopResult.NotSent;
        if(!query&&(sell!=World.PrivateShopSell||!World.ValidPrivateOrder(row,count)))return PrivateShopResult.NotSent;
        var selected=query?null:World.PrivateShopRows.Single(r=>r.Id==row);byte[] packet=query?PrivateShopPackets.Query(stamp.OwnerId,sell):PrivateShopPackets.Submit(stamp.OwnerId,sell,selected!,count);
        var sender=sendMovementGate;int lease=++privateShopLease;privateShopSending=true;bool admitted=false;
        try{bool Valid(){World.CheckPrivateShop();return lease==privateShopLease&&PrivateShopAccess&&Stamp()&&!World.PrivateShopPending&&(!query||!World.PrivateShopQueryUncertain(sell))&&(query||sell==World.PrivateShopSell&&World.ValidPrivateOrder(row,count));}bool sent=await sender(packet,Valid,()=>{admitted=true;if(query)World.BeginPrivateQuery(sell);else World.BeginPrivateSubmit();Set(State,World.PrivateShopNotice);});return admitted?(sent&&stamp.Generation==SessionGeneration?PrivateShopResult.Accepted:PrivateShopResult.Unknown):PrivateShopResult.NotSent;}
        catch(Exception ex){var result=admitted?PrivateShopResult.Unknown:PrivateShopResult.NotSent;if(stamp.Generation==SessionGeneration){Cancel();Set(LoginState.Faulted,PrivateShopFeedback.Text(result,"")+" 傳送錯誤："+ex.Message);}return result;}
        finally{if(lease==privateShopLease)privateShopSending=false;}
    }
}

