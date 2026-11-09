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
        // AM§4(d), TW16 sub_940330/F00640: another object's invisibility uses flag 0x100 (50% blend, like self)
        // when dword_13DC4E4 && sub_956D30(id) holds, otherwise 0x40, drawn only when sub_F03F20 ($431) holds.
        // R2-A (T251): sub_956D30 is taken as "party member or same clan" (推測, AM§4(d) and m6-native-diff G7);
        // dword_13DC4E4 is assumed set and $431 is assumed false, so every other invisible peer stays hidden.
        if(!self&&!InvisiblePeerVisible(draw.ObjectId))return new OpacityValue(2,0,0);
        var value=original??new OpacityValue(Renderer.InitialFlags(draw),0,0);
        // Self uses flag 0x100: the 50% blend (VA 0x976FC0, (dst+src)/2), same as Renderer.Blend.
        return value.Transition?value with{Alpha=Math.Min(value.Alpha,16)}:value with{Flags=value.Flags|1};
    }

    /// <summary>Stand-in for sub_956D30 (推測): the peer is in the own party (S_PARTY rows by object id) or carries the
    /// own nonzero clan id. Evaluated at draw time, while TW16 picks the flag when S171 arrives.</summary>
    public bool InvisiblePeerVisible(uint id){
        if(id==0||!objects.TryGetValue(id,out var other))return false;
        if(partyMembers.Values.Any(m=>m.ObjectId==id))return true;
        return Actor is {ClanId:not 0} own&&other.ClanId==own.ClanId;
    }
}
