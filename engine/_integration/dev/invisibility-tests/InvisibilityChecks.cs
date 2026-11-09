using P06;
using System.Drawing;

partial class Checks {
    static byte[] InvisPacket(uint id,byte type)=>Packet(b=>{b.Write((byte)171);b.Write(id);b.Write(type);});
    static byte[] InvisPack(uint id,byte flags)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(id);
        b.Write((ushort)(id==123?0:1110));b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);
        Text(b,id==123?"offline":"target");Text(b,"");b.Write(flags);b.Write(0u);Text(b,"");Text(b,"");
        b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static Draw InvisDraw(uint id=0,string layer="actor")=>new(0,0,0,0,layer,0,-1,-1,new Tile(0,[new Pixel(0,0,31)]),id);
    static void InvisReject(WorldSession world,byte[] packet,string label){
        var own=world.Actor;var peers=world.Objects;int revision=world.ObjectRevision;
        bool rejected=false;try{world.Receive(packet,"offline");}catch(ProtocolException){rejected=true;}
        Check(rejected&&world.Actor==own&&world.Objects.SequenceEqual(peers)&&world.ObjectRevision==revision,label);
    }
    [CheckGroup("invisibility"),CheckGroup("map-continuity",Order=230),CheckGroup(CheckGroups.Default,Order=460)]
    static void InvisibilityChecks(){
        using var c=Seed();var world=c.World;var ownDraw=InvisDraw();var peerDraw=InvisDraw(456);
        // One S format, both type variants. No C packet belongs to this receive-only module.
        Feed(c,InvisPack(0x01020304,0));Feed(c,Convert.FromHexString("AB04030201010000"));
        Check(world.Objects.Single(o=>o.ObjectId==0x01020304).Flags==2,"invisibility S171 independent little-endian D/C vector");
        Feed(c,Convert.FromHexString("AB04030201000000"));
        Check(world.Objects.Single(o=>o.ObjectId==0x01020304).Flags==0,"invisibility S171 type 0 vector");
        Check(world.InvisibilityOpacity(ownDraw,null)==null&&world.InvisibilityOpacity(peerDraw,null)==null,"visible actors keep original opacity");
        var status=world.Status;
        foreach(uint id in new uint[]{123,456}){
            Feed(c,InvisPack(id,0xFD));int revision=world.ObjectRevision;
            Feed(c,InvisPacket(id,1));
            Check((id==123?world.Actor!.Flags:world.Objects.Single(o=>o.ObjectId==id).Flags)==0xFF,"S171 sets only invisible bit "+id);
            Check(world.ObjectRevision==revision+1,"S171 invalidates visual revision "+id);
            var draw=id==123?ownDraw:peerDraw;
            // AM§4(d): self is a 50% blend; another object's invisibility is not drawn (no $431 exception).
            bool Invisible(OpacityValue? v)=>id==123?v is {Half:true,Hidden:false}:v is {Hidden:true};
            Check(Invisible(world.InvisibilityOpacity(draw,null)),"self half opacity, retained peer not drawn "+id);
            Feed(c,InvisPacket(id,1));
            Check((id==123?world.Actor!.Flags:world.Objects.Single(o=>o.ObjectId==id).Flags)==0xFF,"duplicate S171 does not toggle "+id);
            Feed(c,Move(id,32600,32940,2));
            Check(Invisible(world.InvisibilityOpacity(draw,null)),"movement retains invisibility "+id);
            Feed(c,InvisPacket(id,0));
            Check((id==123?world.Actor!.Flags:world.Objects.Single(o=>o.ObjectId==id).Flags)==0xFD,"S171 clears only invisible bit "+id);
            Check(world.InvisibilityOpacity(draw,null)==null,"S171 type 0 restores original rendering "+id);
            Feed(c,InvisPack(id,2));
            Check(Invisible(world.InvisibilityOpacity(draw,null)),"S87 invisible flag works without S171 "+id);
            if(id==123)Check(Invisible(world.InvisibilityOpacity(InvisDraw(123),null)),"own draw tagged with own ID is still self");
            Feed(c,InvisPack(id,0));
            Check(world.InvisibilityOpacity(draw,null)==null,"full S87 visible replacement clears state "+id);
        }
        Check(world.Status==status,"invisibility leaves HP/MP and status untouched");
        int appearance=world.AppearanceRevision;Feed(c,InvisPacket(123,1));Feed(c,InvisPacket(456,1));
        Check(world.AppearanceRevision==appearance,"opacity change does not invalidate motion/appearance admission");
        foreach(var packet in new[]{InvisPacket(123,0),InvisPacket(456,1)}){
            for(int length=1;length<packet.Length;length++)InvisReject(world,packet[..length],"S171 rejects truncation "+packet[5]+"/"+length);
            for(int offset=6;offset<8;offset++){var bad=packet.ToArray();bad[offset]=1;InvisReject(world,bad,"S171 rejects padding "+offset);}
            InvisReject(world,[..packet,0],"S171 rejects extra byte");
            InvisReject(world,[..packet,0,0,0,0,0,0,0,0],"S171 rejects extra cipher block");
        }
        InvisReject(world,InvisPacket(0,1),"S171 rejects zero identity");
        foreach(byte type in new byte[]{2,3,128,255})InvisReject(world,InvisPacket(123,type),"S171 rejects unsupported type "+type);
        int unknown=world.UnknownObjectUpdates;Feed(c,InvisPacket(uint.MaxValue,1));
        Check(world.UnknownObjectUpdates==unknown+1&&!world.Objects.Any(o=>o.ObjectId==uint.MaxValue),"unknown S171 never creates a visible object");
        Feed(c,InvisPack(uint.MaxValue,0));
        Check(world.InvisibilityOpacity(InvisDraw(uint.MaxValue),null)==null,"unknown delta is not cached for a future incarnation");

        // Execute the unchanged product blitter with synthetic pixels, including hit ownership.
        foreach(var draw in new[]{ownDraw,peerDraw}){
            ushort[] colors=[0];bool[] mask=[false];int[] owners=[-1];uint?[] entities=[null];
            Renderer.Blit(draw,new Rectangle(draw.Px,draw.Py,1,1),colors,mask,owners,entities,world.InvisibilityOpacity(draw,null));
            if(draw.ObjectId==0)Check(colors[0]==15&&mask[0]&&entities[0]==null,"invisible self uses existing RGB555 half blend");
            else Check(colors[0]==0&&!mask[0]&&owners[0]==-1&&entities[0]==null,"invisible peer leaves no pixel, depth or picking");
        }
        var hidden=new OpacityValue(2,0,0);var transition=new OpacityValue(0x400,23,1);
        Check(world.InvisibilityOpacity(ownDraw,hidden) is {Hidden:true}&&world.InvisibilityOpacity(peerDraw,hidden) is {Hidden:true},"invisibility cannot reveal already hidden draw");
        Check(world.InvisibilityOpacity(ownDraw,transition) is {Transition:true,Alpha:16},"self invisibility caps existing transition opacity");
        Check(world.InvisibilityOpacity(ownDraw,transition with{Alpha=7}) is {Alpha:7},"self invisibility does not brighten dim transition");
        Check(world.InvisibilityOpacity(peerDraw,transition) is {Hidden:true,Transition:false},"peer invisibility hides even during a transition");
        Check(world.InvisibilityOpacity(InvisDraw(456,"shadow"),null) is {Hidden:true}&&world.InvisibilityOpacity(InvisDraw(0,"shadow"),null) is {Half:true},"actor shadow follows its owner");
        foreach(string layer in new[]{"ground","objects","magic","projectile"})
            Check(world.InvisibilityOpacity(InvisDraw(456,layer),transition)==transition&&world.InvisibilityOpacity(InvisDraw(456,layer),null)==null,"unrelated layer unchanged "+layer);
        world.SelectTarget(456);Feed(c,Packet(b=>{b.Write((byte)120);b.Write(456u);}));
        Check(!world.Objects.Any(o=>o.ObjectId==456)&&world.TargetId==null,"server S120 hides peer and clears selection");
        Feed(c,InvisPacket(456,1));Feed(c,InvisPack(456,0));
        Check(world.InvisibilityOpacity(peerDraw,null)==null,"S120 then late delta then same-ID S87 has no stale invisibility");
        Feed(c,InvisPacket(456,1));Feed(c,Packet(b=>{b.Write((byte)206);b.Write((ushort)0);b.Write((byte)0);}));
        Check(world.Actor==null&&world.Objects.Length==0,"same-map reset retires all invisible actors");
        Feed(c,InvisPacket(123,1));Feed(c,InvisPack(123,0));Feed(c,InvisPack(456,0));
        Check(world.InvisibilityOpacity(ownDraw,null)==null&&world.InvisibilityOpacity(peerDraw,null)==null,"map reentry does not inherit late old-world delta");
        Feed(c,InvisPacket(123,1));Feed(c,InvisPacket(456,1));world.Reset();
        Check(world.InvisibilityOpacity(ownDraw,null)==null&&world.Objects.Length==0,"reset/character switch retires invisibility");
        Set(world,"Status",status);Feed(c,InvisPack(123,0));Feed(c,InvisPack(456,0));
        Check(world.InvisibilityOpacity(ownDraw,null)==null&&world.InvisibilityOpacity(peerDraw,null)==null,"reconnect with reused IDs has no stale invisibility");
    }
}
