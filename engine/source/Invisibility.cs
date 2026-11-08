namespace P06;

public sealed partial class WorldSession {
    const byte InvisibleFlag=2;

    // S171: C(opcode), D(objectId), C(type: 0=visible, 1=invisible), zero padding.
    // Keep the state in S87 Flags so full packs, removals and world resets retire it.
    void ReceiveInvisibility(LoginReader r){
        uint id=r.D();byte type=r.C();r.Finish();
        if(id==0||type>1)throw new ProtocolException("invalid invisibility identity/type");
        byte Flags(byte flags)=>(byte)((flags&~InvisibleFlag)|(type==1?InvisibleFlag:0));
        if(Actor is {} own&&own.ObjectId==id){Actor=own with{Flags=Flags(own.Flags)};ObjectRevision++;}
        else UpdateObject(id,o=>o with{Flags=Flags(o.Flags)});
    }

    public OpacityValue? InvisibilityOpacity(Draw draw,OpacityValue? original){
        if(draw.Layer is not ("actor" or "shadow"))return original;
        bool self=draw.ObjectId==0||Actor?.ObjectId==draw.ObjectId;
        byte flags=self?Actor?.Flags??0:objects.TryGetValue(draw.ObjectId,out var other)?other.Flags:(byte)0;
        if((flags&InvisibleFlag)==0)return original;
        // AM§4(d), TW16 sub_940330/F00640: another object's invisibility uses flag 0x40, which is drawn
        // only when sub_F03F20 ($431 and related checks) holds. That condition is inferred, so no peer
        // exception is made: retained invisible peers and their shadows are not drawn at all.
        if(!self)return new OpacityValue(2,0,0);
        var value=original??new OpacityValue(Renderer.InitialFlags(draw),0,0);
        // Self uses flag 0x100: the 50% blend (VA 0x976FC0, (dst+src)/2), same as Renderer.Blend.
        return value.Transition?value with{Alpha=Math.Min(value.Alpha,16)}:value with{Flags=value.Flags|1};
    }
}
