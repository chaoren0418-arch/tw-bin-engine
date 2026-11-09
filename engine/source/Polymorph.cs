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

// docs/reference/actions-and-names.md 3.1: legal +194 stance values from sub_A4ADD0 (FO 0x64A1D0).
public static class PolymorphStances {
    static readonly HashSet<int> legal=[0,4,11,20,24,40,46,50,54,58,62,83];
    public static bool Legal(int stance)=>legal.Contains(stance);
}

// docs/reference/actions-and-names.md 3.2 step 2/4 and 3.5. Only verified constants and transitions.
public static class SpecialShapes {
    public const int HorseHelmA=6080,HorseHelmB=6094,HorseEffect=6099;
    public static bool HorseHelm(int gfx)=>gfx is HorseHelmA or HorseHelmB;
    // listSprField113 is list.spr field 113 (0x13CD870[gfx]); no verified list.spr mapping exists yet,
    // so product callers pass 0 and only the 6080/6094 constant rule is active.
    public static int[] AttachedEffects(int gfx,int listSprField113=0){
        var result=new List<int>();
        if(HorseHelm(gfx))result.Add(HorseEffect);
        if(listSprField113!=0&&!result.Contains(listSprField113))result.Add(listSprField113);
        return result.ToArray();
    }
    // sub_A43560 (FO 0x642960): at each action-loop end 6080/6094 swap stance 0<->20 and 24<->62.
    public static int LoopEndStance(int gfx,int stance)=>!HorseHelm(gfx)?stance:stance switch{0=>20,20=>0,24=>62,62=>24,_=>stance};
    public static int DisplayStance(int gfx,int stance,long completedLoops)=>completedLoops%2==0?stance:LoopEndStance(gfx,stance);
    // list.spr field 108 (0x13CD850[gfx]) == 2: action 44 end -> stance 4 (or 11 if field 111), +199=2;
    // action 45 end -> stance 0, +199=1. Returns null when the rule does not apply.
    public static (int Stance,int Flag199)? ActionEnd(int listSprField108,int listSprField111,int action)=>
        listSprField108!=2?null:action switch{44=>(listSprField111!=0?11:4,2),45=>(0,1),_=>null};
}

public sealed record SpecialShapeAnchor(uint ObjectId,int Gfx,int EffectGfx,int X,int Y);

public sealed partial class WorldSession {
    // Visible owners of attached special-shape effects; renderer resolves verified assets only.
    public SpecialShapeAnchor[] SpecialShapeAnchors(){
        var result=new List<SpecialShapeAnchor>();
        if(Actor is {} a&&!SelfAppearancePending)foreach(int e in SpecialShapes.AttachedEffects(a.Gfx))result.Add(new(a.ObjectId,a.Gfx,e,a.X,a.Y));
        foreach(var o in Objects)if(!o.AppearancePending)foreach(int e in SpecialShapes.AttachedEffects(o.Gfx))result.Add(new(o.ObjectId,o.Gfx,e,o.X,o.Y));
        return result.ToArray();
    }
    void ReceivePolymorph(byte[] plain){
        var shape=PolymorphPacket.Parse(plain);
        // R2-I（WO §4）：原版 sub_A43B50 在幽靈中對自己的外觀變更直接略過；封包照常解碼（格式錯誤仍要報）。
        if(GhostSkipsSelfAppearance(shape.ObjectId))return;
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

public sealed record SpecialShapeEffectVisual(uint ObjectId,int EffectGfx,int X,int Y,string Frame);
public sealed partial class Renderer {
    public SpecialShapeEffectVisual[] SpecialShapeEffectVisuals {get;private set;}=[];
    // Attachment point for 3.2/3.5 effects (6099 on 6080/6094). Missing verified assets draw nothing.
    void ComposeSpecialShapeEffects(WorldSession session,List<Draw> draws,long now){
        var anchors=session.SpecialShapeAnchors();if(anchors.Length==0){SpecialShapeEffectVisuals=[];return;}
        packetEffectAssets??=new(assets.Root);var visuals=new List<SpecialShapeEffectVisual>();int serial=2700000;
        foreach(var anchor in anchors){
            if(!session.SceneContains(anchor.X,anchor.Y)||packetEffectAssets.Get(anchor.EffectGfx) is not {} appearance)continue;
            var frame=appearance.At(now);visuals.Add(new(anchor.ObjectId,anchor.EffectGfx,anchor.X,anchor.Y,frame.Key));
            foreach(var b in frame.Blocks)draws.Add(new(2*(anchor.X-Coordinates.MinX)+b.A,anchor.Y-Coordinates.MinY+b.B,b.Z,serial++,"special-shape-effect",b.Order,-1,-1,b.Tile));
        }
        SpecialShapeEffectVisuals=visuals.ToArray();
    }
}