using System.Buffers.Binary;
namespace P06;
// S87 has no item template/type discriminator. This is the server S_NPCPack_Item
// envelope policy, not a claim about the protected native client's classifier.
public sealed record GroundItem(uint ObjectId,ushort X,ushort Y,ushort Gfx,uint Count,string Name,WorldObject Envelope);
public static class GroundRules {
    public static byte PickupHeading(ushort x,ushort y,ushort tx,ushort ty,byte old){float dx=Math.Abs(x-tx),dy=Math.Abs(y-ty),distance=Math.Max(dx,dy);if(distance==0)return old;int ax=(int)Math.Floor(dx/distance+0.59f),ay=(int)Math.Floor(dy/distance+0.59f);int sx=ax==0?0:Math.Sign(tx-x),sy=ay==0?0:Math.Sign(ty-y);return (byte)Enumerable.Range(0,8).Single(i=>MovementRules.Dx[i]==sx&&MovementRules.Dy[i]==sy);}
    // R6-D (WO §2; UI §2 V5 (b): F4 = sub_500640): the first class-9 (ground item) object within 1 tile.
    // Engine scan order is ObjectId; the native visible-object order is unverified. Walls (Q6-7) are not checked.
    public static GroundItem? LootTarget(ushort x,ushort y,IEnumerable<GroundItem> items)=>
        items.OrderBy(g=>g.ObjectId).FirstOrDefault(g=>g.Count>0&&ApproachRules.Within(x,y,g.X,g.Y,ApproachRules.PickupRange));
    public static bool Droppable(InventoryItem i)=>GroundCatalog.CanDrop(i);
    // WO §2 step 4 (verified): one timestamp shared by every pickup path; now>=last+986 ms,
    // or 600 ms while dword_13CE514&0x10000. Not yet passed: no send and no queue.
    public const int PickupThrottleMs=986,PickupFastThrottleMs=600;
    public static int PickupThrottle(bool fastFlag)=>fastFlag?PickupFastThrottleMs:PickupThrottleMs;
    // WO §2 step 3 (verified): stack<=1, or the "loot all" option byte_135C1E4, sends the whole stack.
    // Otherwise the quantity prompt opens (null here).
    public static uint? DirectPickupCount(uint stack,bool lootAll)=>stack==0?null:stack<=1||lootAll?Math.Min(stack,(uint)int.MaxValue):null;
    // Prompt callback sub_500A00: blank input means the whole stack; 0 or more than held sends nothing.
    public static bool TryPromptCount(string? text,uint stack,out uint count){
        count=0;if(stack==0)return false;
        if(string.IsNullOrWhiteSpace(text)){count=Math.Min(stack,(uint)int.MaxValue);return true;}
        return GroundCatalog.TryCount(text.Trim(),stack,out count);
    }
    // Light and name are variable (lit items, charges, unidentified wands, capped counts).
    // Neither the inventory catalog nor availability of local artwork governs pickup.
    public static bool IsGround(WorldObject o)=>o.ObjectId!=0&&o.Action==0&&o.Direction==0&&o.Speed==0&&o.WireExp<=2000000000&&o.Lawful==0&&o.Title==""&&o.Flags==0&&o.ClanId==0&&o.Clan==""&&o.Owner==""&&o.Rank==0&&o.HpByte==255&&o.Brave==0&&o.Level==0&&o.Unknown==0&&o.Tail1==255&&o.Tail2==255;
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
    public string GroundNotice {get;internal set;}="點選地面物品拾取；距離超過 1 格會先自動走近";
    // byte_135C1E4: client-side "loot all" option (Action_LootAll / HyperActGetAll toggle it).
    public bool PickupAll {get;private set;}
    public bool TogglePickupAll(){PickupAll=!PickupAll;GroundNotice=PickupAll?"全部拾取：開（一律整疊）":"全部拾取：關（多個時詢問數量）";return PickupAll;}
    // Open quantity prompt (sub_4FD2F0): the displayed ground snapshot it answers for.
    public GroundQuantitySelection? PickupPrompt {get;private set;}
    internal void OpenPickupPrompt(GroundQuantitySelection selection,string name){PickupPrompt=selection;GroundNotice=$"拾取數量？{name}（持有 {selection.Available}；空白＝整疊，0 或超過不送）";}
    internal GroundQuantitySelection? TakePickupPrompt(){var p=PickupPrompt;PickupPrompt=null;return p;}
    // WO §2 step 2 (verified): before sending, the own actor turns locally toward an item that is not underfoot.
    internal void FacePickupTarget(GroundItem item){
        if(Actor is not {} a||(a.X==item.X&&a.Y==item.Y))return;byte heading=GroundRules.PickupHeading(a.X,a.Y,item.X,item.Y,a.Direction);
        if(heading!=a.Direction){LastPickupHeading=new{from=a.Direction,to=heading,item.X,item.Y,source="local turn before C_GET (WO §2 step 2)"};Actor=a with{Direction=heading};}
    }
    public bool SelectGround(uint? id){if(id!=null&&!ground.ContainsKey(id.Value))return false;SelectedGroundId=id;GroundSelectionRevision++;GroundRevision++;if(id!=null)SelectTarget(null);return true;}
    void ResetGround(){ground.Clear();SelectedGroundId=null;GroundRevision++;GroundSelectionRevision++;PickupActions=0;LastPickupActor=null;pendingPickup=null;LastPickupHeading=null;PickupPrompt=null;GroundNotice="等待原服地面物品";}
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
    public object GroundSnapshot()=>new{presentation=PickupPresentationSnapshot(),items=Ground,selectedId=SelectedGroundId,revision=GroundRevision,selectionRevision=GroundSelectionRevision,notice=GroundStatus,pickupActions=PickupActions,lastPickupActor=LastPickupActor,lastPickupHeading=LastPickupHeading,awaitingPickupAction=AwaitingPickupAction,pickupOutcomeTimedOut=PickupOutcomeTimedOut,pickupAll=PickupAll,pickupPrompt=PickupPrompt};
}
public sealed partial class LoginConnection {
    bool groundSending;int groundLease;long nextGroundAt;
    // Pickup throttle is WO §2's 986 ms (600 ms with the fast flag). Drop keeps the same local guard length.
    public const int GroundClickGuardMs=GroundRules.PickupThrottleMs;
    // R2-C (PG §4, T233): dword_13CE514 is the S139 server feature word; 0x10000 selects 600 ms.
    // No S139 (or a non-zero status) leaves the native bss value 0, i.e. 986 ms. Own haste no longer matters.
    public bool PickupFastFlag=>ServerFeatures?.FastPickup==true;
    public int PickupThrottleMs=>GroundRules.PickupThrottle(PickupFastFlag);
    public bool GroundBusy=>groundSending||World.AwaitingPickupAction||Environment.TickCount64<nextGroundAt;
    public bool GroundAvailable=>State==LoginState.InWorld&&!World.ActionLocked&&!World.TradeBlocksActions&&World.Ready&&!World.OwnDead&&World.Status?.Hp>0&&World.Actor!=null;
    // WO §2 step 1 / §4: byte_13CE51E (ghost) blocks every pickup path.
    public bool PickupAvailable=>GroundAvailable&&!World.IsGhost;
    public event Action<object>? GroundSent;
    public event Action<GroundQuantitySelection>? PickupPromptOpened;
    void ResetGroundSend(){groundLease++;groundSending=false;nextGroundAt=0;approach?.Cancel("連線重置");World.TakePickupPrompt();World.InvalidatePickupRequest();}
    // Click on a ground item (sub_F112F0 → sub_5001C0 class 9): approach to 1 tile, then whole stack
    // or quantity prompt. Returns true only when a C_GET was actually sent.
    public async Task<bool> PickupGround(uint id,Func<bool>? walking=null){
        World.TakePickupPrompt();
        if(!PickupAvailable){World.GroundNotice=World.IsGhost?"未送出：幽靈狀態不能拾取":"未送出：目前不能拾取";return false;}
        if(!World.Ground.Any(g=>g.ObjectId==id)||!World.SelectGround(id)){World.GroundNotice="未送出：地面物品已不存在";return false;}
        var item=World.SelectedGround!;int selection=World.GroundSelectionRevision;
        bool Valid()=>World.SelectedGroundId==id&&World.GroundSelectionRevision==selection&&World.SelectedGround is {} g&&g.X==item.X&&g.Y==item.Y&&PickupAvailable;
        var a=World.Actor!;
        if(ApproachRules.Within(a.X,a.Y,item.X,item.Y,ApproachRules.PickupRange))return await PickupArrived(id);
        World.GroundNotice="走近地面物品中；到達 1 格內後自動拾取";
        bool sent=await Approach.RunAsync(new Point(item.X,item.Y),ApproachRules.PickupRange,Valid,()=>PickupArrived(id),walking);
        if(!sent&&World.PickupPrompt==null&&Approach.Notice.Length>0&&!World.GroundNotice.StartsWith("未送出"))World.GroundNotice="未送出："+Approach.Notice;
        return sent;
    }
    Task<bool> PickupArrived(uint id){
        if(World.SelectedGroundId!=id||World.GroundQuantity(true) is not {} snapshot){World.GroundNotice="未送出：地面物品已不存在";return Task.FromResult(false);}
        if(GroundRules.DirectPickupCount(snapshot.Available,World.PickupAll) is uint count)return SendGround(true,count,()=>World.CurrentGroundQuantity(true,snapshot));
        World.OpenPickupPrompt(snapshot,World.SelectedGround!.Name);PickupPromptOpened?.Invoke(snapshot);return Task.FromResult(false);
    }
    // Prompt answer (sub_500A00). Shares the same throttle and distance gates through SendGround.
    public Task<bool> AnswerPickupPrompt(string? text){
        var prompt=World.TakePickupPrompt();
        if(prompt==null){World.GroundNotice="未送出：沒有等待中的拾取數量";return Task.FromResult(false);}
        if(!GroundRules.TryPromptCount(text,prompt.Available,out uint count)){World.GroundNotice="未送出：數量為 0、超過持有量或不是整數";return Task.FromResult(false);}
        if(!World.CurrentGroundQuantity(true,prompt)){World.GroundNotice="未送出：地面物品已變更，請重新點選";return Task.FromResult(false);}
        return SendGround(true,count,()=>World.CurrentGroundQuantity(true,prompt));
    }
    public void CancelPickupPrompt(){if(World.TakePickupPrompt()!=null)World.GroundNotice="已取消拾取數量輸入";}
    // Keyboard Action_Loot (sub_500640; F4 and P): first ground item within 1 tile, whole stack, never asks, never walks.
    // Turning (FacePickupTarget) and the 986/600 ms throttle are the same SendGround path as the click.
    public Task<bool> LootNearby(){
        World.TakePickupPrompt();
        if(!PickupAvailable||World.Actor is not {} a){World.GroundNotice=World.IsGhost?"未送出：幽靈狀態不能拾取":"未送出：目前不能拾取";return Task.FromResult(false);}
        var item=GroundRules.LootTarget(a.X,a.Y,World.Ground);
        if(item==null){World.GroundNotice="未送出：1 格內沒有地面物品";return Task.FromResult(false);}
        World.SelectGround(item.ObjectId);var snapshot=World.GroundQuantity(true)!;
        return SendGround(true,Math.Min(snapshot.Available,(uint)int.MaxValue),()=>World.CurrentGroundQuantity(true,snapshot));
    }
    public Task<bool> PickupSelected(uint count)=>SendGround(true,count);
    public Task<bool> DropSelected(uint count)=>SendGround(false,count);
    internal Task GroundOriginalProbe(byte opcode,ushort x,ushort y,uint id,uint count)=>sendWorld!(GroundRules.Request(opcode,x,y,id,count));
    async Task<bool> SendGround(bool pickup,uint count,Func<bool>? pointerCurrent=null){
        if(pointerCurrent?.Invoke()==false)return false;
        var item=World.SelectedItem;var g=World.SelectedGround;var actor=World.Actor;
        bool ValidItem()=>pickup?g!=null&&World.SelectedGround==g&&World.SelectedGroundId==g.ObjectId&&count<=g.Count:item!=null&&World.CanDropGroundItem(item)&&World.SelectedItem==item&&World.SelectedItemId==item.ObjectId&&count<=item.Count;
        if(!GroundAvailable||pickup&&World.IsGhost||MovementBusy||sendMovementGate==null||count==0||count>int.MaxValue||!ValidItem()){World.GroundNotice=pickup&&World.IsGhost?"未送出：幽靈狀態不能拾取":"未送出：選取／數量不符、此物品不可丟棄或忙碌";return false;}
        if(pickup&&!ApproachRules.Within(actor!.X,actor.Y,g!.X,g.Y,ApproachRules.PickupRange)){World.GroundNotice="未送出：距離超過 1 格（點選物品會先自動走近）";return false;}
        int epoch=generation,we=World.WorldEpoch,input=inputVersion,speedRevision=World.SpeedRevision,selection=pickup?World.GroundSelectionRevision:World.ItemSelectionRevision,contentRevision=pickup?World.GroundRevision:World.InventoryContentRevision,lease=++groundLease,correction=World.Corrections,appearance=World.AppearanceRevision;
        var sender=sendMovementGate;byte[] bytes=GroundRules.Request(pickup?(byte)112:(byte)25,pickup?g!.X:actor!.X,pickup?g!.Y:actor!.Y,pickup?g!.ObjectId:item!.ObjectId,count);groundSending=true;
        // Replies may change selection, counts, speed or appearance before send completion.
        // Only operation ownership remains a completion requirement after admission.
        bool CurrentOwner()=>epoch==generation&&we==World.WorldEpoch&&World.Actor?.ObjectId==actor!.ObjectId&&lease==groundLease;
        bool OtherActionsReady()=>!movementSending&&!combatSending&&!magicSending&&!ItemBusy&&!CombatBusy&&!MagicBusy&&!World.AwaitingPickupAction&&Environment.TickCount64>=MovementReadyAt&&Environment.TickCount64>=nextGroundAt;
        try{
            bool sent=await sender(bytes,()=>CurrentOwner()&&(pointerCurrent?.Invoke()??true)&&input==inputVersion&&speedRevision==World.SpeedRevision&&selection==(pickup?World.GroundSelectionRevision:World.ItemSelectionRevision)&&contentRevision==(pickup?World.GroundRevision:World.InventoryContentRevision)&&correction==World.Corrections&&appearance==World.AppearanceRevision&&GroundAvailable&&ValidItem()&&World.Actor?.X==actor!.X&&World.Actor.Y==actor.Y&&World.Actor.Gfx==actor.Gfx&&World.Actor.Weapon==actor.Weapon&&OtherActionsReady(),()=>{nextGroundAt=Environment.TickCount64+(pickup?PickupThrottleMs:GroundClickGuardMs);if(pickup){World.TrackPickupRequest(g!);World.FacePickupTarget(g!);}World.GroundNotice=(pickup?$"拾取 {count} 個":"手動丟棄")+"已送出；等待原服地面與背包回報";Set(State,World.GroundNotice);});
            if(!CurrentOwner()||!sent)return false;GroundSent?.Invoke(new{utc=DateTimeOffset.UtcNow,plainHex=Convert.ToHexString(bytes),pickup,count,worldEpoch=we,localClickGuardMs=pickup?PickupThrottleMs:GroundClickGuardMs});return true;
        }catch(Exception ex){if(CurrentOwner()){Cancel();Set(LoginState.Faulted,"地面操作失敗："+ex.Message);}return false;}
        finally{if(CurrentOwner())groundSending=false;}
    }
}
