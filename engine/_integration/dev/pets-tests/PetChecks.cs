using P06;
using System.Reflection;
using System.Text.Json;

// Linked into DemoChecks: runs the actual built product, no entry point, window or socket.
partial class Checks {
    static byte[] Companion(uint id=456,string owner="offline",byte hp=70)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(id);b.Write((ushort)1110);
        b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,"夥伴");Text(b,"");b.Write((byte)0);b.Write(0u);
        Text(b,"");Text(b,owner);b.Write((byte)0);b.Write(hp);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] PetList(uint price=70,uint[]? ids=null)=>Packet(b=>{
        ids??=[1001,1002];b.Write((byte)83);b.Write(price);b.Write((ushort)ids.Length);
        foreach(uint id in ids){b.Write(id);b.Write((byte)1);}
    });
    static byte[] PetBagPacket(uint pet=456,byte type=11,uint[]? ids=null)=>Packet(b=>{
        ids??=[2001,2002,2003];b.Write((byte)176);b.Write(pet);b.Write((ushort)ids.Length);b.Write(type);
        for(int i=0;i<ids.Length;i++){b.Write(ids[i]);b.Write((byte)(i==0?0x16:i==1?2:0));b.Write((ushort)(40+i));b.Write((byte)1);b.Write(2u);b.Write((byte)(i==1?3:0));Text(b,i==0?"鋼牙":i==1?"寵物盔甲":"肉");}
        if(type==11)b.Write(unchecked((byte)-12));else b.Write(30u);
    });
    static byte[] PetMenuPacket(string html="anicom",uint pet=456)=>Packet(b=>{
        b.Write((byte)39);b.Write(pet);Text(b,html);b.Write((byte)0);b.Write((ushort)(html=="anicom"?10:6));
        foreach(string s in new[]{"$470","70","100","10","20","12"})Text(b,s);
        if(html=="anicom")foreach(string s in new[]{"夥伴","$611","30","0"})Text(b,s);
    });
    static void PetReject(Action action,string label){bool threw=false;try{action();}catch(ProtocolException){threw=true;}Check(threw,label);}
    static LoginReader PetReader(byte[] bytes){var r=new LoginReader(bytes);r.C();return r;}
    static async Task Pets(){
        Check(Convert.ToHexString(PetPackets.Menu(0x12345678))=="6778563412000000","C103 D little endian and padding");
        Check(Convert.ToHexString(PetPackets.UseItem(0x12345678,0x16,255))=="681678563412FF00","C104 CDC and byte index boundary");
        foreach(string cmd in PetPackets.Commands){var r=PetReader(PetPackets.Command(456,cmd));Check(r.D()==456&&r.S()==cmd,"C125 command "+cmd);r.Finish();}
        foreach(int index in new[]{-1,256}){bool rejected=false;try{PetPackets.UseItem(456,2,index);}catch(ArgumentException){rejected=true;}Check(rejected,"C104 invalid index "+index);}
        using var c=Seed();Feed(c,Companion());
        Feed(c,PetList());Check(c.World.PetCollars.Length==2&&c.World.PetCollars[0].ItemId==1001,"S83 collars");
        Feed(c,PetList(200));Check(c.World.PetCollars.Length==0,"S83 repair not a pet list");
        Feed(c,PetList(ids:[]));Check(c.World.PetCollars.Length==0,"S83 empty list");
        PetReject(()=>Feed(c,PetList(ids:[1,1])),"S83 duplicate rejected");
        PetReject(()=>Feed(c,PetList()[..8]),"S83 truncated rejected");
        Feed(c,PetBagPacket());var bag=c.World.PetInventory(456)!;
        Check(bag.Ac==-12&&bag.Items.Length==3&&bag.Items[0].Type==0x16&&bag.Items[1].Equipped&&!bag.Items[2].Usable,"S176 signed AC and item fields");
        bag.Items[0]=bag.Items[2];Check(c.World.PetInventory(456)!.Items[0].ItemId==2001,"pet bag caller cannot mutate stored ordering");
        int revision=c.World.PetRevision;
        PetReject(()=>Feed(c,PetBagPacket(ids:[1,1])),"S176 duplicate rejected");
        PetReject(()=>Feed(c,PetBagPacket()[..16]),"S176 truncated rejected");
        Check(c.World.PetRevision==revision&&c.World.PetInventory(456)!.Items.Length==3,"malformed packet atomically preserves state");
        Feed(c,PetBagPacket(999));Check(c.World.PetInventory(999)==null,"foreign pet inventory ignored");
        Feed(c,PetBagPacket(type:3));Check(c.World.PetInventory(456)!.Ac==-12,"warehouse still uses existing S176 parser");
        Feed(c,PetBagPacket(ids:[]));Check(c.World.PetInventory(456)!.Items.Length==0,"S176 empty bag AC");
        Feed(c,Companion(457,"someone else"));Feed(c,Companion(458,"",255));
        Check(c.World.PetCompanions.Select(o=>o.ObjectId).SequenceEqual(new[]{456u}),"foreign pet and ownerless S_FollowerPack not controllable");
        Check(c.World.Objects.Any(o=>o.ObjectId==458&&o.HpPercent==null),"S_FollowerPack retains existing rendering object and hidden HP");
        Feed(c,LoginPackets.Pad([237,..BitConverter.GetBytes(456u),40]));
        Check(c.PetSnapshot(456).HpPercent==40,"S237 HP shares world state");
        Feed(c,Companion(owner:"\fRoffline"));Check(c.World.PetCompanions.Any(o=>o.ObjectId==456),"server owner color prefix");
        foreach(string html in new[]{"anicom","moncom"}){
            var r=PetReader(PetMenuPacket(html));uint id=r.D();string key=r.S();var info=PetPackets.ReadInfo(id,key,r);
            Check(info.Hp==70&&info.MaxHp==100&&info.Mp==10&&info.Level==12&&info.Kind==(html=="anicom"?"pet":"summon"),"S39 companion layout "+html);
        }
        int sends=0;byte[]? sent=null;
        Sender(c,(bytes,valid,admit)=>{if(!valid())return Task.FromResult(false);sent=bytes;sends++;admit();return Task.FromResult(true);});
        foreach(string cmd in PetPackets.Commands){var stamp=c.PetSnapshot(456).Stamp;Check(await c.SendPet(stamp,cmd),"admit "+cmd);Check(!await c.SendPet(stamp,cmd),"old command cannot repeat "+cmd);}
        Check(sends==6,"six explicit commands only");
        Check(await c.SendPet(c.PetSnapshot(456).Stamp,"refresh")&&sent![0]==103,"C103 uses guarded transport");
        Feed(c,PetBagPacket());var before=c.PetSnapshot(456).Stamp;
        Check(!await c.SendPet(before,"item",0,999)&&!await c.SendPet(before,"item",2,2003),"wrong item ID and non-equipment rejected");
        Check(await c.SendPet(before,"item",1,2002)&&sent![0]==104&&sent[1]==2&&sent[6]==1,"C104 exact server row index");
        Check(c.World.PetInventory(456)==null&&!await c.SendPet(before,"item",1,2002),"used bag invalidated until explicit refresh reply");
        Feed(c,PetBagPacket());var queued=c.PetSnapshot(456).Stamp;
        Sender(c,(bytes,valid,admit)=>{Feed(c,PetBagPacket(ids:[2002,2001]));if(valid())throw new Exception("stale bag admitted");return Task.FromResult(false);});
        Check(!await c.SendPet(queued,"item",0,2001),"bag reorder at sender gate rejected");
        queued=c.PetSnapshot(456).Stamp;
        Sender(c,(bytes,valid,admit)=>{Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));Feed(c,Companion());Check(!valid(),"removed/recreated object invalidates stamp");return Task.FromResult(false);});
        Check(!await c.SendPet(queued,"stay"),"queued object incarnation cannot send");
        var old=c.PetSnapshot(456).Stamp;Feed(c,PetList());Feed(c,PetBagPacket());
        Feed(c,LoginPackets.Pad([206,4,0,0]));Check(c.World.PetCollars.Length==0&&c.World.PetInventory(456)==null&&!await c.SendPet(old,"stay"),"map invalidates all pet state and old commands");
        c.World.Reset();Check(c.World.PetCompanions.Length==0&&c.World.PetCollars.Length==0,"reset clears pet state");
        using var other=Seed();Feed(other,Companion());var stale=other.PetSnapshot(456).Stamp;other.Cancel();
        Check(!await other.SendPet(stale,"stay"),"disconnect generation rejected");
    }
}
