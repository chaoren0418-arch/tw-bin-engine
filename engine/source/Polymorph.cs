namespace P06;

// S76 has two untagged serializers. Decode the complete envelope before changing state.
// The last H in the ordinary form is a takeoff flag, never an action or speed.
public sealed record PolymorphPacket(uint ObjectId,ushort Gfx,bool WeaponTakeoff,uint? OwnerId=null,string? OwnerName=null) {
    public static PolymorphPacket Parse(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=76)throw new ProtocolException("not a POLY packet");
        uint id=r.D();
        if(id==0)throw new ProtocolException("invalid POLY object identity");
        // Both forms can occupy 16 padded bytes; length alone cannot distinguish them.
        if(plain.Length==16&&plain.AsSpan(9).IndexOfAnyExcept((byte)0)<0){
            ushort gfx=r.H(),mode=r.H();r.Finish();
            if(mode is not (0 or 29))throw new ProtocolException("invalid POLY weapon flag");
            return new(id,gfx,mode==0);
        }
        uint owner=r.D();ushort disguise=r.H();string name=r.S();r.Finish();
        if(owner==0||name.Length==0||name.Any(char.IsControl))throw new ProtocolException("invalid POLY owner envelope");
        return new(id,disguise,false,owner,name);
    }
}

public sealed partial class WorldSession {
    void ReceivePolymorph(byte[] plain){
        var shape=PolymorphPacket.Parse(plain);
        if(Actor is {} actor&&actor.ObjectId==shape.ObjectId){
            // Gfx/Weapon are the shared current-appearance query for rendering and cadence.
            // Undo is another S76 with the server's base gfx; no local timer or class guess.
            Actor=actor with{Gfx=shape.Gfx,Weapon=shape.WeaponTakeoff?(byte)0:actor.Weapon};
            SelfAppearancePending=false;AppearanceRevision++;
            pendingAction=null;OwnCombat=OwnDead?OwnCombat:null;
            CancelPickupPresentation("polymorph");
            return;
        }
        UpdateObject(shape.ObjectId,o=>o with{
            Gfx=shape.Gfx,Action=shape.WeaponTakeoff?(byte)0:o.Action,AppearancePending=false,
            MotionAt=0,Combat=o.Dead?o.Combat:null,ReceivedAction=o.Dead?8:o.ShopActive?70:null,
            DoorAction=null
        });
        // Keep the same target identity, but invalidate queued actions against its old appearance.
        if(TargetId==shape.ObjectId){TargetRevision++;CloseInteraction("目標外觀已更新，請重新對話。");}
    }
}
