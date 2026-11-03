namespace P06;
public sealed record BonusAttribute(string Key,string Label,byte? Value,bool Selected);
public sealed record BonusStatsView(int Generation,int Epoch,int Revision,uint ActorId,string Phase,string? Selected,string Notice,int? Remaining,bool CanSelect,bool CanSubmit,bool CanRefresh,BonusAttribute[] Attributes);
public static class BonusStatsRules {
    // Original RaiseAttr-c.html; fixed d287 C_Attr switch479 accepts these six strings.
    public static readonly string[] Keys=["str","dex","con","int","wis","cha"];
    public static readonly string[] Labels=["力量","敏捷","體質","智力","精神","魅力"];
    public static bool Valid(string key)=>Keys.Contains(key,StringComparer.Ordinal);
    public static byte Value(OwnStatus s,string key)=>key switch{"str"=>s.Strength,"dex"=>s.Dexterity,"con"=>s.Constitution,"int"=>s.Intelligence,"wis"=>s.Wisdom,"cha"=>s.Charisma,_=>throw new ArgumentException("attribute")};
    public static byte[] Allocate(uint ownId,string key){if(ownId==0||!Valid(key))throw new ArgumentException("attribute identity");return LoginPackets.Pad([121,0,0,..BitConverter.GetBytes(ownId),0xDF,1,1,..System.Text.Encoding.ASCII.GetBytes(key),0]);}
    // Explicit user query only: L1ActionPc.action("power") emits RaiseAttr iff power() is true.
    public static byte[] Query(uint ownId){if(ownId==0)throw new ArgumentException("identity");return NpcPackets.Action(ownId,"power");}
}
public sealed partial class WorldSession {
    public string BonusStatsNotice {get;private set;}="";
    public string BonusStatsPhase {get;private set;}="idle";
    public string? SelectedBonusAttribute {get;private set;}
    public int BonusStatsRevision {get;private set;}
    uint bonusOwner;int bonusEpoch;long? bonusWaitingAt;
    internal Func<long> BonusStatsClock=()=>Environment.TickCount64;
    public bool BonusStatsWaiting=>BonusStatsPhase is "querying" or "waiting";
    public bool BonusStatsOffered=>BonusStatsPhase=="offered"&&bonusOwner!=0&&Status?.ObjectId==bonusOwner&&bonusEpoch==WorldEpoch&&Status.Level>=51;
    void ResetBonusStats(){bonusOwner=0;bonusWaitingAt=null;SelectedBonusAttribute=null;BonusStatsPhase="idle";BonusStatsNotice="";BonusStatsRevision++;}
    void InvalidateBonusStats(){if(bonusOwner==0)return;bonusOwner=0;bonusWaitingAt=null;SelectedBonusAttribute=null;BonusStatsPhase="stale";BonusStatsNotice="角色或世界已改變，請重新查詢能力點；舊操作不會重送。";BonusStatsRevision++;}
    internal void CheckBonusStats(){
        if(bonusOwner!=0&&(Status?.ObjectId!=bonusOwner||bonusEpoch!=WorldEpoch||Status.Level<51||Status.Hp==0||OwnDead)){InvalidateBonusStats();return;}
        if(BonusStatsWaiting&&bonusWaitingAt is long started&&BonusStatsClock()-started>=15000){bonusWaitingAt=null;SelectedBonusAttribute=null;BonusStatsPhase="uncertain";BonusStatsNotice="等待原服回覆已逾時；結果與剩餘點數未知。請核對能力值，再手動查詢；不會自動重送。";BonusStatsRevision++;}
    }
    void ReceiveBonusOffer(uint id){
        if(Status==null||Status.ObjectId!=id||id==0)throw new ProtocolException("bonus stat identity");
        CheckBonusStats();
        // A repeated/late offer must never release a submitted request or queue another point.
        if(BonusStatsPhase is not ("idle" or "querying" or "offered"))return;
        if(Status.Level<51||Status.Hp==0||OwnDead){InvalidateBonusStats();BonusStatsNotice="能力點提示與目前角色狀態不符，請重新查詢。";BonusStatsRevision++;return;}
        if(BonusStatsOffered)return;
        bonusOwner=id;bonusEpoch=WorldEpoch;bonusWaitingAt=null;SelectedBonusAttribute=null;BonusStatsPhase="offered";BonusStatsNotice="請選擇您所要提昇的屬性。您所選擇的屬性將提昇１點。剩餘點數：原服未提供。";BonusStatsRevision++;
    }
    internal bool ChooseBonusAttribute(string key){CheckBonusStats();if(!BonusStatsOffered||!BonusStatsRules.Valid(key))return false;SelectedBonusAttribute=key;BonusStatsRevision++;return true;}
    internal void BeginBonusStats(bool query){bonusOwner=Status!.ObjectId;bonusEpoch=WorldEpoch;bonusWaitingAt=BonusStatsClock();BonusStatsPhase=query?"querying":"waiting";BonusStatsNotice=query?"已查詢可分配狀態；等待原服提示。沒有回覆不代表剩餘點數為零。":"已送出一次能力分配；等待原服能力更新，不預先增加數值，也不會自動重送。";SelectedBonusAttribute=null;BonusStatsRevision++;}
    bool ReceiveBonusStats(byte opcode,LoginReader r,byte[] plain){
        if(opcode!=155)return false;
        if(plain.Length!=8)throw new ProtocolException("own stat2 length");
        byte str=r.C(),intel=r.C(),wis=r.C(),dex=r.C(),con=r.C(),cha=r.C(),weight=r.C();r.Finish();
        if(Status==null)throw new ProtocolException("own stat2 before identity");
        CheckBonusStats();Status=Status with{Strength=str,Intelligence=intel,Wisdom=wis,Dexterity=dex,Constitution=con,Charisma=cha,Weight=weight};MagicRevision++;
        if(BonusStatsPhase=="waiting"){bonusWaitingAt=null;BonusStatsPhase="review";BonusStatsNotice="收到原服能力更新；請核對目前數值。剩餘點數未提供，下一次分配前請手動查詢。保存結果仍需重新登入核對。";BonusStatsRevision++;}
        return true;
    }
    void BonusStatsServerMessage(ushort id,string[] args){
        CheckBonusStats();if(id!=481||args.Length!=0||BonusStatsPhase!="waiting")return;
        bonusWaitingAt=null;BonusStatsPhase="rejected";SelectedBonusAttribute=null;BonusStatsNotice="原服回覆：屬性最大值只能到35。 請重試一次。請手動查詢後重新選擇。";BonusStatsRevision++;
    }
}
public sealed partial class LoginConnection {
    bool bonusSending;int bonusSendLease;
    void ResetBonusStatsSend(){bonusSendLease++;bonusSending=false;}
    bool BonusStatsAccess=>State==LoginState.InWorld&&World.Ready&&World.Status is {Level:>=51,Hp:>0} s&&World.Actor?.ObjectId==s.ObjectId&&!World.OwnDead&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy&&!MagicBusy&&!ItemBusy&&!interactionSending&&World.InteractionKind is "closed" or "timedout";
    public BonusStatsView BonusStatsSnapshot(){
        World.CheckBonusStats();bool access=BonusStatsAccess&&!bonusSending;var s=World.Status;
        return new(SessionGeneration,World.WorldEpoch,World.BonusStatsRevision,s?.ObjectId??0,World.BonusStatsPhase,World.SelectedBonusAttribute,World.BonusStatsNotice.Length>0?World.BonusStatsNotice:s is {Level:<51}?"等級51起，依原服提示分配能力點。":"可手動查詢原服是否允許分配；剩餘點數未提供。",null,access&&World.BonusStatsOffered,access&&World.BonusStatsOffered&&World.SelectedBonusAttribute!=null,access&&!World.BonusStatsWaiting,BonusStatsRules.Keys.Select((key,i)=>new BonusAttribute(key,BonusStatsRules.Labels[i],s==null?null:BonusStatsRules.Value(s,key),World.SelectedBonusAttribute==key)).ToArray());
    }
    bool BonusStamp(int generation,int epoch,int revision,uint own)=>generation==SessionGeneration&&epoch==World.WorldEpoch&&revision==World.BonusStatsRevision&&own!=0&&own==World.Status?.ObjectId&&own==World.Actor?.ObjectId;
    public bool SelectBonusAttribute(string key,int generation,int epoch,int revision,uint own){World.CheckBonusStats();return !bonusSending&&BonusStatsAccess&&BonusStamp(generation,epoch,revision,own)&&World.ChooseBonusAttribute(key);}
    public async Task<bool> SendBonusStats(bool query,string? key,int generation,int epoch,int revision,uint own){
        World.CheckBonusStats();if(bonusSending||!BonusStatsAccess||!BonusStamp(generation,epoch,revision,own)||World.BonusStatsWaiting||sendMovementGate==null)return false;
        if(!query&&(!World.BonusStatsOffered||key==null||!BonusStatsRules.Valid(key)||World.SelectedBonusAttribute!=key))return false;
        if(query&&key!=null)return false;
        var sender=sendMovementGate;int lease=++bonusSendLease;byte[] packet=query?BonusStatsRules.Query(own):BonusStatsRules.Allocate(own,key!);bonusSending=true;
        try{
            bool Valid(){World.CheckBonusStats();return lease==bonusSendLease&&BonusStatsAccess&&BonusStamp(generation,epoch,revision,own)&&!World.BonusStatsWaiting&&(query||World.BonusStatsOffered&&World.SelectedBonusAttribute==key);}
            bool sent=await sender(packet,Valid,()=>{World.BeginBonusStats(query);Set(State,World.BonusStatsNotice);});
            return sent&&generation==SessionGeneration;
        }catch(Exception ex){if(generation==SessionGeneration){Cancel();Set(LoginState.Faulted,"能力點請求傳送失敗："+ex.Message);}return false;}
        finally{if(lease==bonusSendLease)bonusSending=false;}
    }
}
