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
        byte flags=draw.ObjectId==0?Actor?.Flags??0:objects.TryGetValue(draw.ObjectId,out var other)?other.Flags:(byte)0;
        if((flags&InvisibleFlag)==0)return original;
        var value=original??new OpacityValue(Renderer.InitialFlags(draw),0,0);
        // Visibility permission belongs to the server: S120 removes hidden peers;
        // a retained S87/S171 invisible actor is drawn with the existing half blend.
        return value.Transition?value with{Alpha=Math.Min(value.Alpha,16)}:value with{Flags=value.Flags|1};
    }
}
