using P06;
using System.Buffers.Binary;

partial class Checks {
    static byte[] DoorPack(byte action=29,uint id=700,ushort gfx=2346,ushort x=32600,ushort y=32939)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write(gfx);b.Write(action);
        b.Write(new byte[3]);b.Write(1u);b.Write(new byte[12]);b.Write((byte)255);
        b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] DoorAttribute(byte direction=0,byte passable=1,ushort x=32600,ushort y=32940)=>
        Packet(b=>{b.Write((byte)209);b.Write(x);b.Write(y);b.Write(direction);b.Write(passable);});
    static byte[] DoorAction(byte action,uint id=700)=>Packet(b=>{b.Write((byte)158);b.Write(id);b.Write(action);});
    static void DoorReject(Action action,string label){bool rejected=false;try{action();}catch(ProtocolException){rejected=true;}Check(rejected,label);}
    static async Task DoorChecks(){
        string fixtureRoot=Path.GetFullPath("scratch/issue-322/synthetic");
        mapFixture=Path.Combine(fixtureRoot,"assets");MakeMapFixture();
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(fixtureRoot,"build","offline-checks")+Path.DirectorySeparatorChar);
        Check(DoorPackets.Toggle(0x12345678,0x2345,0x3456).SequenceEqual(Convert.FromHexString("29452356347856341200000000000000")),"C41 HHD byte order and zero padding");
        bool zero=false;try{DoorPackets.Toggle(0,0,0);}catch(ArgumentOutOfRangeException){zero=true;}Check(zero,"C41 zero identity rejected");
        foreach(byte action in new byte[]{28,29,32,33,34,35,36,37})foreach(ushort gfx in new ushort[]{2346,99}){
            using var c=Seed();Feed(c,DoorPack(action,gfx:gfx));
            Check(c.World.IsDoor(700)&&c.World.Objects.Single(o=>o.ObjectId==700) is {} d&&d.DoorAction==action&&d.Dead==(action==37),$"S87 door action {action}, gfx {gfx}");
            Check(!c.World.DoorBlocks(32600,32941,0),"S87 never guesses collision");
        }
        using(var c=Seed()){
            foreach(int offset in new[]{12,13,14,15,19,23,28,30,31,32,33,34,35,36}){
                var packet=DoorPack();packet[offset]^=1;Check(!DoorPackets.IsPack(packet),"S87 signature discriminator "+offset);
            }
            Feed(c,Pack(700));Check(!c.World.IsDoor(700),"ordinary NPC is not a door");
            Feed(c,DoorPack());Feed(c,DoorAction(28));Check(c.World.Objects.Single(o=>o.ObjectId==700).DoorAction==28,"S158 open persists");
            Feed(c,DoorAction(29));Check(c.World.Objects.Single(o=>o.ObjectId==700).DoorAction==29,"S158 close persists");
            Feed(c,DoorAttribute());Feed(c,DoorAction(28));Check(c.World.DoorBlocks(32600,32941,0),"S158 does not unlock S209 collision");
            Feed(c,DoorAttribute(passable:0));Check(!c.World.DoorBlocks(32600,32941,0),"S209 open removes collision");
            Feed(c,DoorAction(37));Check(c.World.Objects.Single(o=>o.ObjectId==700).Dead,"S158 door destruction");
            Feed(c,DoorPack());Check(!c.World.Objects.Single(o=>o.ObjectId==700).Dead,"new full pack restores door incarnation");
            Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(700u)]));Check(!c.World.IsDoor(700),"S120 removes door identity");
            Feed(c,Pack(700));Feed(c,DoorAction(28));Check(!c.World.IsDoor(700),"reused object ID is not a door");
            Feed(c,DoorAction(70,456));Check(c.World.Objects.Single(o=>o.ObjectId==456).ShopActive,"non-door S158 still reaches existing dispatcher");
        }
        foreach(byte opcode in new byte[]{87,158,209}){
            var packet=opcode==87?DoorPack():opcode==158?DoorAction(28):DoorAttribute();
            for(int n=0;n<packet.Length;n++){
                using var c=Seed();Feed(c,DoorPack());var before=c.World.Objects.Single(o=>o.ObjectId==700);
                DoorReject(()=>Feed(c,packet[..n]),$"S{opcode} truncated {n}");
                Check(ReferenceEquals(before,c.World.Objects.Single(o=>o.ObjectId==700))&&c.World.DoorTiles.Length==0,"malformed packet is atomic");
            }
            using var bad=Seed();Feed(bad,DoorPack());packet[^1]=1;DoorReject(()=>Feed(bad,packet),"nonzero padding S"+opcode);
        }
        using(var c=Seed()){
            DoorReject(()=>Feed(c,DoorAttribute(direction:2)),"invalid S209 direction");
            DoorReject(()=>Feed(c,DoorAttribute(passable:2)),"invalid S209 passability");
            Check(c.World.DoorTiles.Length==0,"invalid attribute is atomic");
        }
        foreach(byte direction in new byte[]{0,1}){
            using var c=Seed();Feed(c,DoorAttribute(direction));
            Check(c.World.DoorTiles.Single()==new DoorTile(32600,32940,direction,true),"S209 preserves reported direction");
            for(int i=-1;i<=1;i++)for(int h=0;h<8;h++){
                int tx=32600+(direction==0?i:0),ty=32940+(direction==1?i:0);
                Check(c.World.DoorBlocks(tx-MovementRules.Dx[h],ty-MovementRules.Dy[h],h),$"closed span {direction}/{i}/{h}");
            }
            Check(!c.World.DoorBlocks(32610,32950,0),"unrelated tile unaffected");
            Feed(c,DoorAttribute(direction,0));Check(!c.World.DoorBlocks(32600,32941,0),"open restores original terrain policy");
            Feed(c,DoorAttribute(direction));Feed(c,MapPacket(0));Check(c.World.DoorTiles.Length==0&&!c.World.DoorBlocks(32600,32941,0),"same-map S206 retires attributes");
            Feed(c,DoorAttribute(direction));c.World.Reset();Check(c.World.DoorTiles.Length==0,"reset retires attributes");
        }
        foreach(string invalidation in new[]{"remove","replace","map","reset","focus","death","teleport"}){
            using var c=Seed();Feed(c,DoorPack());bool focused=true;int sent=0;
            var pointer=new PlayerPointerController(c,()=>focused,()=>false,_=>true);
            Sender(c,(b,v,a)=>{
                switch(invalidation){
                    case "remove":Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(700u)]));break;
                    case "replace":Feed(c,DoorPack());break;
                    case "map":Feed(c,MapPacket(0));break;
                    case "reset":c.World.Reset();break;
                    case "focus":focused=false;break;
                    case "death":Feed(c,DoorAction(37));break;
                    case "teleport":Feed(c,TeleportPacket());break;
                }
                if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);
            });
            Check(!await pointer.ActAsync(700,true)&&sent==0,"queued C41 invalidated by "+invalidation);
        }
        foreach(bool talk in new[]{false,true}){
            using var c=Seed();Feed(c,DoorPack());Feed(c,DoorAttribute());var original=c.World.Objects.Single(o=>o.ObjectId==700);int sent=0;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);Check(b.SequenceEqual(DoorPackets.Toggle(700,32600,32939)),"pointer sends C41 only");sent++;a();return Task.FromResult(true);});
            var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(await pointer.ActAsync(700,talk)&&sent==1,"one pointer door request "+talk);
            Check(ReferenceEquals(original,c.World.Objects.Single(o=>o.ObjectId==700))&&c.World.DoorBlocks(32600,32941,0),"C41 has no predicted animation/collision");
        }
        // Exercise the actual movement sender using the existing authored map fixture.
        using(var c=MapSeed()){
            int sent=0;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);});
            Feed(c,DoorAttribute(y:32939));Check(!await c.Move(0)&&sent==0,"keyboard movement blocked by S209");
            Feed(c,DoorAttribute(passable:0,y:32939));Check(await c.Move(0)&&sent==1,"keyboard movement allowed after S209 open");
        }
        using(var c=MapSeed()){
            int sent=0;Sender(c,(b,v,a)=>{Feed(c,DoorAttribute(y:32939));if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);});
            Check(!await c.Move(0)&&sent==0,"queued movement rechecks newly closed door");
        }
    }
}
