using P06;
using System.Buffers.Binary;

partial class Checks {
    static byte[] SuppressList(params ushort[] gfx)=>Packet(b=>{
        b.Write((byte)242);b.Write((byte)2);b.Write((ushort)gfx.Length);
        foreach(ushort value in gfx)b.Write(value);
    });
    static byte[] SuppressObject(uint id,ushort gfx){
        var packet=Pack(id);BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(9),gfx);return packet;
    }
    static byte[] SuppressAction(uint id,byte action)=>Packet(b=>{b.Write((byte)158);b.Write(id);b.Write(action);});
    static byte[] SuppressAttack(uint target,ushort damage=10)=>Packet(b=>{
        b.Write((byte)30);b.Write((byte)1);b.Write(123u);b.Write(target);b.Write(damage);
        b.Write((byte)2);b.Write((ushort)0);b.Write((ushort)0);b.Write((byte)0);
    });
    static void HitReactionSuppression(){
        CombatAdmissionFixture(); // Authored metadata only; no game assets or connection.
        using var c=Seed();var w=c.World;
        Check(!w.IsHitReactionSuppressed(456),"hit suppression defaults empty");
        Feed(c,SuppressAttack(456));
        Check(w.Target!.Combat?.Action==2&&w.OwnCombat?.Action==1,"baseline melee hurt and attacker animation");
        Feed(c,Pack(456));
        int inventoryRevision=w.InventoryRevision,contentRevision=w.InventoryContentRevision;
        int unknown=w.UnknownItemUpdates;string notice=w.ItemNotice;
        Feed(c,SuppressList(1110,65535,1110));
        Check(w.IsHitReactionSuppressed(456)&&!w.IsHitReactionSuppressed(123),"duplicate GFX accepted; membership uses target GFX");
        Check(!w.IsHitReactionSuppressed(0)&&!w.IsHitReactionSuppressed(99999),"unknown/zero object has no inferred GFX");
        Check(w.InventoryRevision==inventoryRevision&&w.InventoryContentRevision==contentRevision&&w.UnknownItemUpdates==unknown&&w.ItemNotice==notice,"sub2 has no inventory side effects");
        var hp=w.Status;Feed(c,SuppressAttack(456));
        Check(w.Target!.Combat==null&&w.Target.ReceivedAction==null,"listed melee target does not animate hurt");
        Check(w.LastAttack is {Target:456,DamageWord:10}&&w.AttackReports==2&&w.OwnCombat?.Action==1&&w.Status==hp,"target id, damage, attack animation and HP authority preserved");
        Feed(c,SuppressAction(456,18));var cast=w.Target!.Combat;
        Feed(c,SuppressAction(456,2));
        Check(cast?.Action==18&&ReferenceEquals(cast,w.Target!.Combat),"S158 hurt does not interrupt listed target casting");
        Feed(c,Packet(b=>{b.Write((byte)237);b.Write(456u);b.Write((byte)42);}));
        Check(w.Target!.HpPercent==42,"listed target HP updates still apply");
        Feed(c,SuppressAction(456,8));
        Check(w.Objects.Single(o=>o.ObjectId==456) is {Dead:true,Combat.Action:8},"listed target death still applies");
        Feed(c,SuppressObject(456,65535));w.SelectTarget(456);
        Check(w.IsHitReactionSuppressed(456),"unsigned maximum GFX decoded");
        Feed(c,SuppressObject(456,1111));
        Check(!w.IsHitReactionSuppressed(456),"replacement appearance checked live");
        Feed(c,SuppressAction(456,2));Check(w.Target!.Combat?.Action==2,"unlisted S158 hurt still animates");
        Feed(c,SuppressList(1111));
        Feed(c,SuppressObject(456,1110));
        Check(!w.IsHitReactionSuppressed(456),"new snapshot replaces rather than appends");
        Feed(c,SuppressList(0));Feed(c,SuppressAction(123,2));
        Check(w.IsHitReactionSuppressed(123)&&w.OwnCombat?.Action==1,"GFX-only contract also checks current own appearance");
        Feed(c,SuppressList());Feed(c,SuppressAction(123,2));
        Check(!w.IsHitReactionSuppressed(123)&&w.OwnCombat?.Action==2,"empty snapshot clears suppression");
        Feed(c,SuppressList(Enumerable.Range(1000,512).Select(i=>(ushort)i).ToArray()));
        Check(w.IsHitReactionSuppressed(456),"maximum 512-entry snapshot accepted");
        var badPadding=SuppressList(1111);badPadding[^1]=1;
        var oversized=SuppressList(Enumerable.Repeat((ushort)1111,513).ToArray());
        byte[][] malformed=[ [242,2], [242,2,1], [242,2,1,0,0x57],
            [242,2,3,0,0x57,4,0,0],badPadding,oversized,[..SuppressList(1111),0,0,0,0,0,0,0,0] ];
        foreach(var packet in malformed){
            bool rejected=false;try{Feed(c,packet);}catch(ProtocolException){rejected=true;}
            Check(rejected,"malformed sub2 rejected "+Convert.ToHexString(packet.AsSpan(0,Math.Min(packet.Length,8))));
            Check(w.IsHitReactionSuppressed(456),"malformed sub2 preserves previous complete snapshot");
        }
        Feed(c,LoginPackets.Pad([242,3]));
        Check(w.IsHitReactionSuppressed(456)&&w.InventoryRevision==inventoryRevision,"unknown subopcode leaves snapshot and inventory alone");
        Feed(c,SuppressAttack(0));
        Check(w.LastAttack?.Target==0&&w.Target!.Combat==null,"legacy zero target preserved without synthesized target");
        using(var isolated=Seed())Check(!isolated.World.IsHitReactionSuppressed(456),"separate sessions do not share snapshot");
        SuppressInventoryRegression(c);
    }
    static void SuppressInventoryRegression(LoginConnection c){
        var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");
        Feed(c,Packet(b=>{
            b.Write((byte)15);b.Write(900101u);b.Write(row.InventoryDesc);b.Write(row.WireUseType);b.Write((byte)0);
            b.Write(row.InventoryIcon);b.Write((byte)1);b.Write(10u);b.Write((byte)0);Text(b,"test");b.Write((byte)0);b.Write(new byte[11]);
        }));
        var bytes=Enumerable.Range(0,256).Select(i=>(byte)i).ToArray();
        Feed(c,Packet(b=>{
            b.Write((byte)242);b.Write((byte)1);b.Write(900101u);Text(b,"long status");b.Write(9u);b.Write((ushort)bytes.Length);b.Write(bytes);
        }));
        Check(c.World.Inventory.Single(i=>i.ObjectId==900101) is {Name:"long status",Count:9} item&&item.StatusHex==Convert.ToHexString(bytes),"S242/sub1 long item status unchanged");
        Check(c.World.IsHitReactionSuppressed(456),"sub1 does not change suppression snapshot");
    }
}
