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
        // 已驗證（FU4§4，T287／#680；R3-H 補做 T288／#689）：dword_13DC4E4 is the own party list and
        // sub_956D30 → sub_953C00 → sub_953950 only compares entry +0 with the object id. There is no clan or alliance test.
        // The 0x40 branch (sub_F03F20) needs S36 type 3 in 1..3, byte_1540830 == 0 and an icon-87 item named "$431"
        // (FU5§4: 沙哈的戒指). 3.81 never sends S36 type 3 and has no such item, so 0x40 peers are never drawn.
        if(!self&&!InvisiblePeerVisible(draw.ObjectId))return new OpacityValue(2,0,0);
        var value=original??new OpacityValue(Renderer.InitialFlags(draw),0,0);
        // Self uses flag 0x100: the 50% blend (VA 0x976FC0, (dst+src)/2), same as Renderer.Blend.
        return value.Transition?value with{Alpha=Math.Min(value.Alpha,16)}:value with{Flags=value.Flags|1};
    }

    /// <summary>sub_956D30 on dword_13DC4E4 (已驗證，FU4§4): the peer is in the own party (S_PARTY rows by object id).
    /// Clan and alliance members are not exceptions. Evaluated at draw time, while TW16 picks the flag when S171 arrives.</summary>
    public bool InvisiblePeerVisible(uint id){
        if(id==0||!objects.ContainsKey(id))return false;
        return partyMembers.Values.Any(m=>m.ObjectId==id);
    }
}
