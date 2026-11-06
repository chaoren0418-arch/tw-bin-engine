namespace P06;
// CHARPACK is a shared wire envelope. Flags do not prove a species/class (doppel NPCs can set PC).
public sealed record WorldObject(ushort X,ushort Y,uint ObjectId,ushort Gfx,byte Action,byte Direction,byte Light,byte Speed,uint WireExp,short Lawful,string Name,string Title,byte Flags,uint ClanId,string Clan,string Owner,byte Rank,byte HpByte,byte Brave,byte Level,byte Unknown,byte Tail1,byte Tail2) {
    // Only the observed inn-door gfx has proven persistent open/close actions. Null means unknown.
    public int? DoorAction {get;init;}=Gfx==2346 && Action is 28 or 29 ? Action : null;
    public CombatAnimation? Combat {get;init;}
    public bool Dead {get;init;}
    public int? HpPercent=>HpByte<=100?HpByte:null;
    public string Classification=> (Flags&4)!=0?"PC flag (not a class/species guarantee)":"non-PC flag";
    public ushort SourceX {get;init;}=X;
    public ushort SourceY {get;init;}=Y;
    public long MotionAt {get;init;}
    public byte MotionSpeed {get;init;}=Speed;
    public byte MotionBrave {get;init;}=Brave;
    public long UpdatedAt {get;init;}=Environment.TickCount64;
    public int? ReceivedAction {get;init;}
    public bool AppearancePending {get;init;}
    public bool ShopActive {get;init;}
    public string ShopText {get;init;}="";
    public long ShopAt {get;init;}
}
public sealed partial class WorldSession {
    readonly System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject> objects=new();
    public WorldObject[] Objects=>objects.Values.OrderBy(o=>o.ObjectId).ToArray();
    public uint? TargetId {get;private set;}
    public int TargetRevision {get;private set;}
    public WorldObject? Target=>TargetId is uint id&&objects.TryGetValue(id,out var o)?o:null;
    public int ObjectRevision {get;private set;}
    public int WorldEpoch {get;private set;}
    public int UnknownObjectUpdates {get;private set;}
    public void SelectTarget(uint? id){CloseInteraction("目標已改變，請重新對話。");TargetId=id is uint v&&objects.TryGetValue(v,out var selected)&&!selected.Dead?v:null;ObjectRevision++;TargetRevision++;}
    void ClearObjects(){ResetPickupPresentation();InvalidateBonusStats();ResetTrade("世界已清除，舊交易已失效。");CloseInteraction("世界已清除，舊對話已失效。");ResetOpacity();ResetGround();objects.Clear();TargetId=null;ObjectRevision++;WorldEpoch++;UnknownObjectUpdates=0;}
    void UpdateObject(uint id,Func<WorldObject,WorldObject> update){
        if(objects.TryGetValue(id,out var o)){objects[id]=update(o) with{UpdatedAt=Environment.TickCount64};ObjectRevision++;}
        else UnknownObjectUpdates++;
    }
    void ReceiveObject(LoginReader r,ushort x,ushort y,uint id){
        var o=new WorldObject(x,y,id,r.H(),r.C(),r.C(),r.C(),r.C(),r.D(),unchecked((short)r.H()),r.S(),r.S(),r.C(),r.D(),r.S(),r.S(),r.C(),r.C(),r.C(),r.C(),r.C(),r.C(),r.C());r.Finish();
        if(o.Direction>7||id==0)throw new ProtocolException("invalid world object identity/heading");
        if(ReceiveGround(o))return;objects.TryGetValue(id,out var previous);bool dead=o.Action==8||previous?.Dead==true;
        if(dead){o=o with{Dead=true,Combat=previous?.Combat?.Action==8?previous.Combat:new(8,Environment.TickCount64,false),ReceivedAction=8};if(TargetId==id)SelectTarget(null);}
        if(!dead&&o.Action==70)o=o with{ShopActive=true,ShopAt=Environment.TickCount64};
        objects[id]=o;ObjectRevision++;
    }
    bool ReceiveObjectDelta(byte opcode,LoginReader r){
        switch(opcode){
        case 10:{uint id=r.D();ushort x=r.H(),y=r.H();byte h=r.C(),tail=r.C();r.Finish();if(h>7||tail!=128)throw new ProtocolException("invalid move envelope");if(Actor?.ObjectId==id)CancelPickupPresentation("authoritative-move");int nx=x+MovementRules.Dx[h],ny=y+MovementRules.Dy[h];if(nx<0||nx>65535||ny<0||ny>65535)throw new ProtocolException("move range");if(Actor is {} own&&own.ObjectId==id){var moved=own with{X=(ushort)nx,Y=(ushort)ny,Direction=h};RecordAuthoritativePosition(moved,10);Actor=moved;Corrections++;MotionNotice="收到原服自身移動位置；輸入已停止";UpdateOpacity();return true;}UpdateObject(id,o=>o.Dead?o:o with{SourceX=x,SourceY=y,X=(ushort)nx,Y=(ushort)ny,Direction=h,MotionAt=Environment.TickCount64,MotionSpeed=o.Speed,MotionBrave=o.Brave,ReceivedAction=null,Combat=null,DoorAction=o.Gfx==2346?null:o.DoorAction});return true;}
        case 122:{uint id=r.D();byte h=r.C();r.Finish();if(h>7)throw new ProtocolException("invalid heading");if(Actor?.ObjectId==id){CancelPickupPresentation("authoritative-heading");Actor=Actor with{Direction=h};}else UpdateObject(id,o=>o with{Direction=h,MotionAt=0});return true;}
        case 120:{uint id=r.D();r.Finish();if(Actor?.ObjectId==id){pickupRemoved=true;CancelPickupPresentation("actor-removed");}RemoveGround(id);objects.TryRemove(id,out _);if(TargetId==id)SelectTarget(null);ObjectRevision++;return true;}
        case 158:{uint id=r.D();byte action=r.C();string shopText=action==70?r.S():"";if(action==71){r.H();r.H();}r.Finish();if(objects.TryGetValue(id,out var door)&&door.Gfx==2346){UpdateObject(id,o=>o with{DoorAction=!o.Dead&&action is 28 or 29?action:null,ReceivedAction=action,MotionAt=0,Combat=null,ShopActive=false,ShopText="",ShopAt=0});return true;}if(action==15){ReceivePickupAction(id);return true;}if(Actor?.ObjectId==id){pickupShop=action==70;CancelPickupPresentation("authoritative-action");}if(action is 1 or 2 or 8 or 18 or 19)AnimateCombat(id,action);else UpdateObject(id,o=>o with{ReceivedAction=action,MotionAt=0,ShopActive=!o.Dead&&action==70,ShopText=shopText,ShopAt=action==70?Environment.TickCount64:0,Combat=action==70?null:o.Combat});return true;}
        // POLY has incompatible constructors on this JAR. Invalidate the old image, retain raw wire, wait for a full pack.
        case 76:{uint id=r.D();r.H();r.H();UpdateObject(id,o=>o with{AppearancePending=true,MotionAt=0});if(Actor?.ObjectId==id){SelfAppearancePending=true;AppearanceRevision++;}Unhandled(opcode);return true;}
        default:return false;
        }
    }
}
