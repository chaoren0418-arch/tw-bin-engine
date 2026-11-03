namespace P06;

// OWN-BASE-15-001-SUPERVISOR-R1 is a finite engine presentation policy.
// Native private flags, variant selection, FIFO and runtime timing remain unproven.
internal sealed record PickupOwner(int Session,int World,ushort? Map,uint Actor,int Appearance,int Correction,int Speed,
    ushort Gfx,byte Weapon,byte Direction,ushort X,ushort Y);
internal sealed record PickupContext(ObjectAppearance Appearance,Frame[] Frames,string? Blocked);
internal sealed record PickupInstance(long Serial,long Receipt,PickupOwner Owner,Frame[] Frames,byte Speed,
    int Index,long Baseline,long Due);
public sealed partial class WorldSession {
    internal Func<int>? PickupSessionGeneration;
    internal Func<long,PickupContext?>? PickupRenderContext;
    int? pickupReceiptSession;
    long pickupSerial,pickupReceiptSerial;
    bool pickupShop,pickupRemoved;
    PickupInstance? pickup;
    public object? LastPickupPresentation {get;private set;}
    public object PickupPresentationSnapshot()=>new{policyId="OWN-BASE-15-001-SUPERVISOR-R1",sourceEligibility="native-unproven",runtimeTimingCertified=false,
        active=pickup==null?null:new{pickup.Serial,pickup.Receipt,pickup.Owner,pickup.Index,frame=pickup.Frames[pickup.Index].Key,pickup.Baseline,pickup.Due},last=LastPickupPresentation};
    void PickupReport(string state,string reason,long serial,long receipt)=>LastPickupPresentation=new{state,reason,serial,receipt,actor=Actor?.ObjectId,gfx=Actor?.Gfx,weapon=Actor?.Weapon};
    internal void CancelPickupPresentation(string reason){if(pickup is {} p){pickup=null;PickupReport("cancelled",reason,p.Serial,p.Receipt);}}
    void ResetPickupPresentation(){CancelPickupPresentation("world-reset");pickupShop=false;pickupRemoved=false;}
    internal void ReceiveOwned(byte[] plain,string selected,int session){
        var previous=pickupReceiptSession;pickupReceiptSession=session;
        try{Receive(plain,selected);}finally{pickupReceiptSession=previous;}
    }
    PickupOwner? CapturePickupOwner()=>Actor is {} a&&PickupSessionGeneration is {} generation
        ?new(generation(),WorldEpoch,MapId,a.ObjectId,AppearanceRevision,Corrections,SpeedRevision,a.Gfx,a.Weapon,a.Direction,a.X,a.Y):null;
    string? PickupGuard()=>Actor is not {} a||Status?.ObjectId!=a.ObjectId?"actor-identity":
        !Ready||!Renderable?"world-unavailable":OwnDead||Status.Hp==0||a.HpPercent==0?"health/death":SelfAppearancePending?"appearance-pending":
        !PickupAssets.Supports(a.Gfx,a.Weapon)||a.Direction>7||a.Speed is not (0 or 1)||a.Brave!=0?"unsupported-profile/speed":
        ActionLocked?"locked":pickupShop?"shop":pickupRemoved?"removed":null;
    PickupContext? PickupContextAt(long now,out string? reason){
        reason=PickupGuard();if(reason!=null)return null;
        try{var context=PickupRenderContext?.Invoke(now);reason=context==null?"renderer/assets-unavailable":context.Blocked;return context;}
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or System.Text.Json.JsonException){reason="asset-integrity/key";return null;}
    }
    // Called only by the parsed authoritative S158/15 path, after pending heading handling.
    void ReceivePickupPresentation(uint id){
        if(Actor?.ObjectId!=id)return;long receipt=++pickupReceiptSerial,now=ActionClock();
        var owner=CapturePickupOwner();var context=PickupContextAt(now,out var reason);
        if(owner==null||pickupReceiptSession!=owner.Session)reason="receipt-session-unbound";
        if(reason!=null||context==null){CancelPickupPresentation(reason??"assets-unavailable");PickupReport("suppressed",reason??"assets-unavailable",pickupSerial,receipt);return;}
        long due=now+(Actor!.Speed==0?41:30)+1;
        pickup=new(++pickupSerial,receipt,owner!,context.Frames,Actor.Speed,1,due,due);
        PickupReport("started","authoritative-own-receipt",pickup.Serial,receipt);
    }
    internal void ValidatePickupPresentation(long now){
        if(pickup is not {} p)return;
        if(p.Owner!=CapturePickupOwner()){CancelPickupPresentation("owner/revision/position");return;}
        var context=PickupContextAt(now,out var reason);
        if(reason!=null||context==null)CancelPickupPresentation(reason??"assets-unavailable");
    }
    // A callback carries its instance serial. A late old callback cannot advance or clear a replacement.
    internal Frame? AdvancePickupPresentation(long serial,long now){
        if(pickup is not {} p||p.Serial!=serial)return null;
        ValidatePickupPresentation(now);if(pickup!=p)return null;
        if(now>=p.Due){
            if(p.Index==0){pickup=null;PickupReport("finished","terminal-hold",p.Serial,p.Receipt);return null;}
            int next=p.Index==3?0:p.Index+1;
            if(next==0)p=p with{Index=0,Due=now+584};
            else{int duration=p.Speed==0?166:124;long late=now-p.Baseline;
                p=p with{Index=next,Due=now+Math.Max(1,duration-late)+1,Baseline=p.Baseline+duration+1};}
            pickup=p;
        }
        return p.Frames[p.Index];
    }
    internal Frame? PickupFrame(long now)=>pickup is {} p?AdvancePickupPresentation(p.Serial,now):null;
}

public sealed partial class Renderer {
    // This selector is also used by existing own combat rendering. Keep duration/priority unchanged.
    CombatAnimation? ActiveOwnCombat(WorldSession session,ObjectAppearance appearance,long now){
        if(session.Actor is not {} a||WithProjectileHit(a.ObjectId,session.OwnCombat,now,session.OwnDead) is not {} combat)return null;
        string name=OwnCombatName(combat.Action);
        return (combat.Timing==null||combat.Timing.Matches(a.Gfx,a.Weapon))&&appearance.Supports(name,a.Direction)&&
            (name=="die"||now-combat.At<(combat.Timing?.AnimationMs??appearance.Duration(name)))?combat:null;
    }
    static string OwnCombatName(int action)=>action switch{1=>"attack",2=>"damage",8=>"die",18=>"spellDir",19=>"spellNoDir",_=>"unsupported"};
    void BindPickupPresentation(World own,WorldSession session,ObjectAssets catalog){
        session.PickupRenderContext=now=>{
            if(session.Actor is not {} a||catalog.Get(a.Gfx,a.Weapon) is not {} appearance||catalog.PickupFrames(appearance,a.Direction,a.Weapon) is not {} frames)return null;
            // Resolve pending projectile impacts using the very same compositor logic before admission.
            ComposeProjectile(session,[],now);
            string? blocked=own.Walking?"walking":ActiveOwnCombat(session,appearance,now)!=null?"active-combat/projectile":null;
            return new(appearance,frames,blocked);
        };
    }
}
