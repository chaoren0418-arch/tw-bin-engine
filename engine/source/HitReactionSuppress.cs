namespace P06;

public sealed partial class WorldSession {
    // S242/sub2 is a complete login-time GFX snapshot, not an inventory update.
    // Publish only after the entire packet (including padding) has been validated.
    HashSet<ushort> hitReactionSuppressGfx=[];
    bool ReceiveHitReactionSuppress(LoginReader r){
        r.C(); // sub2; the shared dispatcher has already checked it.
        int count=r.H();
        if(count>512)throw new ProtocolException("hit reaction suppress count exceeds 512");
        var next=new HashSet<ushort>();
        for(int i=0;i<count;i++)next.Add(r.H());
        r.Finish();
        hitReactionSuppressGfx=next;
        return true;
    }

    // ClientLogin=2 supplies GFX IDs only. CHARPACK flags cannot prove NPC/PC
    // identity, so use the current wire appearance rather than guessing species.
    public bool IsHitReactionSuppressed(uint objectId){
        ushort? gfx=Actor?.ObjectId==objectId?Actor.Gfx:
            objects.TryGetValue(objectId,out var target)?target.Gfx:null;
        return gfx is ushort value&&hitReactionSuppressGfx.Contains(value);
    }
}
