using System.Buffers.Binary;
namespace P06;
// S87 has no item template/type discriminator. Only catalog envelopes with template and sprite evidence are supported.
public sealed record GroundItem(uint ObjectId,ushort X,ushort Y,ushort Gfx,uint Count,string Name,WorldObject Envelope);
public static class GroundRules {
    public static byte PickupHeading(ushort x,ushort y,ushort tx,ushort ty,byte old){float dx=Math.Abs(x-tx),dy=Math.Abs(y-ty),distance=Math.Max(dx,dy);if(distance==0)return old;int ax=(int)Math.Floor(dx/distance+0.59f),ay=(int)Math.Floor(dy/distance+0.59f);int sx=ax==0?0:Math.Sign(tx-x),sy=ay==0?0:Math.Sign(ty-y);return (byte)Enumerable.Range(0,8).Single(i=>MovementRules.Dx[i]==sx&&MovementRules.Dy[i]==sy);}
    public static bool Droppable(InventoryItem i)=>GroundCatalog.CanDrop(i);
    public static bool IsGround(WorldObject o){var row=GroundCatalog.Find(o.Gfx);return row!=null&&o.ObjectId!=0&&o.Action==0&&o.Direction==0&&o.Light==0&&o.Speed==0&&o.WireExp<=2000000000&&(row.Stackable||o.WireExp<=1)&&o.Lawful==0&&o.Name==row.CountName(o.WireExp)&&o.Title==""&&o.Flags==0&&o.ClanId==0&&o.Clan==""&&o.Owner==""&&o.Rank==0&&o.HpByte==255&&o.Brave==0&&o.Level==0&&o.Unknown==0&&o.Tail1==255&&o.Tail2==255;}
    public static byte[] Request(byte opcode,ushort x,ushort y,uint id,uint count){
        if(opcode is not (25 or 112)||id==0||count==0||count>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(count));
        byte[] b=new byte[16];b[0]=opcode;BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(1),x);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(3),y);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5),id);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(9),count);return b;
    }
}
public sealed partial class WorldSession {
    readonly System.Collections.Concurrent.ConcurrentDictionary<uint,GroundItem> ground=new();
    public GroundItem[] Ground=>ground.Values.OrderBy(x=>x.ObjectId).ToArray();
    public uint? SelectedGroundId {get;private set;}
    public GroundItem? SelectedGround=>SelectedGroundId is uint id&&ground.TryGetValue(id,out var i)?i:null;
    public int GroundRevision {get;private set;}
    public int GroundSelectionRevision {get;private set;}
    public long PickupActions {get;private set;}
    public uint? LastPickupActor {get;private set;}
    (ushort X,ushort Y,ushort TargetX,ushort TargetY,byte Heading,int Epoch,long Until)? pendingPickup;
    public bool AwaitingPickupAction=>pendingPickup!=null;
    public bool PickupOutcomeTimedOut=>pendingPickup is {} p&&Environment.TickCount64>=p.Until;
    public string GroundStatus=>PickupOutcomeTimedOut?"拾取結果未確認；操作已停止。\n請等候原服或斷線重登。":GroundNotice;
    public object? LastPickupHeading {get;private set;}
    public string GroundNotice {get;internal set;}="選取地面物品，單次拾取；不自動行走";
    public bool SelectGround(uint? id){if(id!=null&&!ground.ContainsKey(id.Value))return false;SelectedGroundId=id;GroundSelectionRevision++;GroundRevision++;if(id!=null)SelectTarget(null);return true;}
    void ResetGround(){ground.Clear();SelectedGroundId=null;GroundRevision++;GroundSelectionRevision++;PickupActions=0;LastPickupActor=null;pendingPickup=null;LastPickupHeading=null;GroundNotice="等待原服地面物品";}
    internal void InvalidatePickupRequest(){pendingPickup=null;}
    internal void TrackPickupRequest(GroundItem item){if(Actor is {} a)pendingPickup=(a.X,a.Y,item.X,item.Y,a.Direction,WorldEpoch,Environment.TickCount64+3000);}
    void ReceivePickupAction(uint id){
        PickupActions++;LastPickupActor=id;
        if(Actor?.ObjectId==id&&pendingPickup is {} p){
            if(p.Epoch==WorldEpoch&&Actor.X==p.X&&Actor.Y==p.Y){byte heading=GroundRules.PickupHeading(p.X,p.Y,p.TargetX,p.TargetY,p.Heading);LastPickupHeading=new{from=Actor.Direction,to=heading,p.TargetX,p.TargetY,source="own original S158/action15 plus pending C112; original self S122 is excluded",originalActionNumber=PickupActions};Actor=Actor with{Direction=heading};}
            pendingPickup=null;
        }
        GroundNotice="原服已回報拾取動作；數量以地面／背包回包為準";
        ReceivePickupPresentation(id);
    }
    void RemoveGround(uint id){if(ground.TryRemove(id,out _)){GroundRevision++;if(SelectedGroundId==id)SelectGround(null);GroundNotice="原服已移除地面物品";}}
    bool ReceiveGround(WorldObject o){
        if(!GroundRules.IsGround(o)){RemoveGround(o.ObjectId);return false;}
        objects.TryRemove(o.ObjectId,out _);if(TargetId==o.ObjectId)SelectTarget(null);
        if(o.WireExp==0){RemoveGround(o.ObjectId);return true;}
        ground[o.ObjectId]=new(o.ObjectId,o.X,o.Y,o.Gfx,o.WireExp,o.Name,o);GroundRevision++;GroundNotice="收到原服地面物品／數量";return true;
    }
    public object GroundSnapshot()=>new{presentation=PickupPresentationSnapshot(),items=Ground,selectedId=SelectedGroundId,revision=GroundRevision,selectionRevision=GroundSelectionRevision,notice=GroundStatus,pickupActions=PickupActions,lastPickupActor=LastPickupActor,lastPickupHeading=LastPickupHeading,awaitingPickupAction=AwaitingPickupAction,pickupOutcomeTimedOut=PickupOutcomeTimedOut};
}
public sealed partial class LoginConnection {
    bool groundSending;int groundLease;long nextGroundAt;
    // Local click guard; not a change to the original server's item/animation rules.
    public const int GroundClickGuardMs=900;
    public bool GroundBusy=>groundSending||World.AwaitingPickupAction||Environment.TickCount64<nextGroundAt;
    public bool GroundAvailable=>State==LoginState.InWorld&&!World.ActionLocked&&!World.TradeBlocksActions&&World.Ready&&!World.OwnDead&&World.Status?.Hp>0&&World.Actor!=null;
    public event Action<object>? GroundSent;
    void ResetGroundSend(){groundLease++;groundSending=false;nextGroundAt=0;World.InvalidatePickupRequest();}
    public Task<bool> PickupSelected(uint count)=>SendGround(true,count);
    public Task<bool> DropSelected(uint count)=>SendGround(false,count);
    internal Task GroundOriginalProbe(byte opcode,ushort x,ushort y,uint id,uint count)=>sendWorld!(GroundRules.Request(opcode,x,y,id,count));
    async Task<bool> SendGround(bool pickup,uint count,Func<bool>? pointerCurrent=null){
        if(pointerCurrent?.Invoke()==false)return false;
        var item=World.SelectedItem;var g=World.SelectedGround;var actor=World.Actor;
        bool ValidItem()=>pickup?g!=null&&World.SelectedGround==g&&World.SelectedGroundId==g.ObjectId&&count<=g.Count:item!=null&&World.CanDropGroundItem(item)&&World.SelectedItem==item&&World.SelectedItemId==item.ObjectId&&count<=item.Count;
        if(!GroundAvailable||MovementBusy||sendMovementGate==null||count==0||count>int.MaxValue||!ValidItem()){World.GroundNotice="未送出：選取／數量不符、此物品不可丟棄或忙碌";return false;}
        if(pickup&&Math.Max(Math.Abs(actor!.X-g!.X),Math.Abs(actor.Y-g.Y))>3){World.GroundNotice="未送出：距離超過 3 格，請自行靠近";return false;}
        int epoch=generation,we=World.WorldEpoch,input=inputVersion,speedRevision=World.SpeedRevision,selection=pickup?World.GroundSelectionRevision:World.ItemSelectionRevision,contentRevision=pickup?World.GroundRevision:World.InventoryContentRevision,lease=++groundLease,correction=World.Corrections,appearance=World.AppearanceRevision;
        var sender=sendMovementGate;byte[] bytes=GroundRules.Request(pickup?(byte)112:(byte)25,pickup?g!.X:actor!.X,pickup?g!.Y:actor!.Y,pickup?g!.ObjectId:item!.ObjectId,count);groundSending=true;
        // Replies may change selection, counts, speed or appearance before send completion.
        // Only operation ownership remains a completion requirement after admission.
        bool CurrentOwner()=>epoch==generation&&we==World.WorldEpoch&&World.Actor?.ObjectId==actor!.ObjectId&&lease==groundLease;
        bool OtherActionsReady()=>!movementSending&&!combatSending&&!magicSending&&!ItemBusy&&!CombatBusy&&!MagicBusy&&!World.AwaitingPickupAction&&Environment.TickCount64>=MovementReadyAt&&Environment.TickCount64>=nextGroundAt;
        try{
            bool sent=await sender(bytes,()=>CurrentOwner()&&(pointerCurrent?.Invoke()??true)&&input==inputVersion&&speedRevision==World.SpeedRevision&&selection==(pickup?World.GroundSelectionRevision:World.ItemSelectionRevision)&&contentRevision==(pickup?World.GroundRevision:World.InventoryContentRevision)&&correction==World.Corrections&&appearance==World.AppearanceRevision&&GroundAvailable&&ValidItem()&&World.Actor?.X==actor!.X&&World.Actor.Y==actor.Y&&World.Actor.Gfx==actor.Gfx&&World.Actor.Weapon==actor.Weapon&&OtherActionsReady(),()=>{nextGroundAt=Environment.TickCount64+GroundClickGuardMs;if(pickup)World.TrackPickupRequest(g!);World.GroundNotice=(pickup?"單次拾取":"手動丟棄")+"已送出；等待原服地面與背包回報";Set(State,World.GroundNotice);});
            if(!CurrentOwner()||!sent)return false;GroundSent?.Invoke(new{utc=DateTimeOffset.UtcNow,plainHex=Convert.ToHexString(bytes),pickup,count,worldEpoch=we,localClickGuardMs=GroundClickGuardMs});return true;
        }catch(Exception ex){if(CurrentOwner()){Cancel();Set(LoginState.Faulted,"地面操作失敗："+ex.Message);}return false;}
        finally{if(CurrentOwner())groundSending=false;}
    }
}
