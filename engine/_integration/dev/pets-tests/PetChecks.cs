using P06;
using System.Reflection;
using System.Text.Json;

// Linked into DemoChecks: runs the actual built product, no entry point, window or socket.
partial class Checks {
    static byte[] Companion(uint id=456,string owner="offline",byte hp=70,byte heading=0)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(id);b.Write((ushort)1110);
        b.Write((byte)0);b.Write(heading);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);Text(b,"夥伴");Text(b,"");b.Write((byte)0);b.Write(0u);
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
    static byte[] PetMenuPacket(string html="anicom",uint pet=456,string hp="70",string mp="10",string level="12",byte flag=0,ushort? count=null)=>Packet(b=>{
        b.Write((byte)39);b.Write(pet);Text(b,html);b.Write(flag);b.Write(count??(ushort)(html=="anicom"?10:6));
        foreach(string s in new[]{"$470",hp,"100",mp,"20",level})Text(b,s);
        if(html=="anicom")foreach(string s in new[]{"夥伴","$611","30","0"})Text(b,s);
    });
    static byte[] PetNpcDialog(string html,uint npc=456,params string[] args)=>Packet(b=>{
        b.Write((byte)39);b.Write(npc);Text(b,html);
        if(html=="RaiseAttr")return; // No flag/count in the original self-attribute offer.
        b.Write((ushort)(args.Length==0?0:1));b.Write((ushort)args.Length);
        foreach(string arg in args)Text(b,arg);
    });
    static void PetReject(Action action,string label){bool threw=false;try{action();}catch(ProtocolException){threw=true;}Check(threw,label);}
    static LoginReader PetReader(byte[] bytes){var r=new LoginReader(bytes);r.C();return r;}
    static async Task Pets(){
        Check(Convert.ToHexString(PetPackets.Information(0x12345678))=="2278563412000000","C34 D information query and padding");
        Check(Convert.ToHexString(PetPackets.Menu(0x12345678))=="6778563412000000","C103 D little endian and padding");
        Check(Convert.ToHexString(PetPackets.UseItem(0x12345678,0x16,255))=="681678563412FF00","C104 CDC and byte index boundary");
        foreach(string cmd in PetPackets.Commands){var r=PetReader(PetPackets.Command(456,cmd));Check(r.D()==456&&r.S()==cmd,"C125 command "+cmd);r.Finish();}
        foreach(int index in new[]{-1,256}){bool rejected=false;try{PetPackets.UseItem(456,2,index);}catch(ArgumentException){rejected=true;}Check(rejected,"C104 invalid index "+index);}
        using var c=Seed();Feed(c,Companion());
        Feed(c,PetList());Check(c.World.PetCollars.Length==2&&c.World.PetCollars[0].ItemId==1001,"S83 collars");
        int collarRevision=c.World.PetRevision;Call(c.World,"BeginWeaponRepair");Feed(c,PetList(200));
        Check(c.World.WeaponRepairPhase=="ready"&&c.World.WeaponRepairRows.Length==2,"S83 price 200 reaches weapon repair route");
        Check(c.World.PetCollars.Length==2&&c.World.PetRevision==collarRevision,"S83 repair list leaves pet collars untouched");
        c.World.CloseWeaponRepair();Feed(c,PetList(71));Check(c.World.PetCollars.Length==0,"S83 unknown price not a pet list");
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
        // A nonzero heading avoids the existing shared ground classifier's ambiguous zero-count envelope.
        Feed(c,Companion(457,"someone else"));Feed(c,Companion(458,"",255,3));
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
        foreach(string html in new[]{"anicom","moncom"}){
            var query=c.PetSnapshot(456).Stamp;
            Check(await c.SendPet(query,"info")&&sent!.SequenceEqual(PetPackets.Information(456)),"C34 guarded information query "+html);
            Check(c.PetSnapshot(456).Info==null&&!await c.SendPet(query,"info"),"query clears stale details and invalidates old click "+html);
            int beforeReply=c.World.PetRevision,npcRevision=c.World.InteractionRevision;
            Feed(c,PetMenuPacket(html));var info=c.PetSnapshot(456).Info;
            Check(info is {Hp:70,MaxHp:100,Mp:10,MaxMp:20,Level:12}&&info.Kind==(html=="anicom"?"pet":"summon"),"WorldSession.Receive S39 details "+html);
            Check(c.World.PetRevision>beforeReply&&c.World.InteractionRevision==npcRevision,"S39 updates pet UI revision without entering NPC parser "+html);
            Feed(c,PetMenuPacket(html,hp:"60",mp:"5",level:"13"));
            Check(c.PetSnapshot(456).Info is {Hp:60,Mp:5,Level:13},"S39 reply refreshes existing details "+html);
        }
        var savedInfo=c.World.PetInformation(456);revision=c.World.PetRevision;
        foreach(var invalid in new[]{PetMenuPacket()[..16],PetMenuPacket(flag:1),PetMenuPacket(count:6),PetMenuPacket("moncom",count:10),PetMenuPacket(hp:"101"),PetMenuPacket(mp:"21"),PetMenuPacket(level:"256"),PetMenuPacket(hp:"bad"),PetMenuPacket(pet:0)})
            PetReject(()=>Feed(c,invalid),"S39 invalid envelope/vitals rejected through Receive");
        Check(c.World.PetRevision==revision&&c.World.PetInformation(456)==savedInfo,"invalid S39 leaves previously published information intact");
        Feed(c,PetMenuPacket(pet:457));Feed(c,PetMenuPacket("moncom",458));Feed(c,PetMenuPacket(pet:999));
        Check(c.World.PetInformation(457)==null&&c.World.PetInformation(458)==null&&c.World.PetInformation(999)==null,"foreign/ownerless/missing S39 cannot publish pet information");
        Check(await c.SendPet(c.PetSnapshot(456).Stamp,"refresh")&&sent![0]==103,"C103 uses guarded transport");
        Feed(c,PetBagPacket());var before=c.PetSnapshot(456).Stamp;
        Check(!await c.SendPet(before,"item",0,999)&&!await c.SendPet(before,"item",2,2003),"wrong item ID and non-equipment rejected");
        foreach(var (index,type,item) in new[]{(1,(byte)2,2002u),(0,(byte)0x16,2001u)}){
            before=c.PetSnapshot(456).Stamp;
            Check(await c.SendPet(before,"item",index,item)&&sent!.SequenceEqual(PetPackets.UseItem(456,type,index)),"C104 exact server row index and type "+type);
            Check(c.World.PetInventory(456)==null&&c.World.PetInformation(456)==null&&!await c.SendPet(before,"item",index,item),"used bag and details invalidated until explicit reply "+type);
            // Original C104 success emits S250/37 equipment notice followed by S39.
            Feed(c,LoginPackets.Pad([250,37,type,..BitConverter.GetBytes(456u),(byte)index,unchecked((byte)-12)]));
            Check(c.World.PetInformation(456)==null,"equipment notice alone does not fabricate details "+type);
            int priorNpc=c.World.InteractionRevision;
            Feed(c,PetMenuPacket(hp:"65",mp:"7"));
            Check(c.State==LoginState.InWorld&&c.PetSnapshot(456).Info is {Hp:65,Mp:7}&&c.World.InteractionRevision==priorNpc,"C104 success reply reaches S39 pet information without NPC format error "+type);
            Check(c.World.PetInventory(456)==null,"C104 S39 does not fabricate refreshed bag contents "+type);
            Check(await c.SendPet(c.PetSnapshot(456).Stamp,"refresh"),"explicit bag refresh after C104 reply "+type);
            Feed(c,PetBagPacket());Check(c.PetSnapshot(456).Bag is {Ac:-12},"S176 repopulates equipment after C104 refresh "+type);
        }
        Feed(c,PetBagPacket());var queued=c.PetSnapshot(456).Stamp;
        Sender(c,(bytes,valid,admit)=>{Feed(c,PetBagPacket(ids:[2002,2001]));if(valid())throw new Exception("stale bag admitted");return Task.FromResult(false);});
        Check(!await c.SendPet(queued,"item",0,2001),"bag reorder at sender gate rejected");
        queued=c.PetSnapshot(456).Stamp;
        Sender(c,(bytes,valid,admit)=>{Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));Feed(c,Companion());Check(!valid(),"removed/recreated object invalidates stamp");return Task.FromResult(false);});
        Check(!await c.SendPet(queued,"stay"),"queued object incarnation cannot send");
        var old=c.PetSnapshot(456).Stamp;Feed(c,PetList());Feed(c,PetBagPacket());
        Feed(c,LoginPackets.Pad([206,4,0,0]));Check(c.World.PetCollars.Length==0&&c.World.PetInventory(456)==null&&c.World.PetInformation(456)==null&&!await c.SendPet(old,"stay"),"map invalidates all pet state and old commands");
        Feed(c,PetMenuPacket());Check(c.World.PetInformation(456)==null,"late S39 after map change ignored");
        c.World.Reset();Check(c.World.PetCompanions.Length==0&&c.World.PetCollars.Length==0,"reset clears pet state");
        using var other=Seed();Feed(other,Companion());var stale=other.PetSnapshot(456).Stamp;other.Cancel();
        Check(!await other.SendPet(stale,"stay"),"disconnect generation rejected");
        Check(!await other.SendPet(stale,"info"),"disconnect rejects old information query");
        using var npc=Seed();
        Sender(npc,(bytes,valid,admit)=>{if(!valid())return Task.FromResult(false);admit();return Task.FromResult(true);});
        Check(await npc.Interact("talk",npc.World.InteractionRevision,npc.World.WorldEpoch,npc.World.TargetRevision),"open ordinary NPC dialogue via existing request");
        int petRevision=npc.World.PetRevision;
        foreach(var packet in new[]{PetNpcDialog("ordinary"),PetNpcDialog("ordinary",456,"one","two"),PetNpcDialog("anicom-extra"),PetNpcDialog("moncom-extra")}){
            int prior=npc.World.InteractionRevision;Feed(npc,packet);
            Check(npc.World.InteractionKind=="dialog"&&npc.World.InteractionRevision>prior&&npc.World.PetRevision==petRevision,"non-pet S39 retains ordinary H-flag NPC route");
        }
        Feed(npc,Companion(789));
        var npcPage=npc.World.InteractionPage;int openNpcRevision=npc.World.InteractionRevision;
        foreach(string html in new[]{"anicom","moncom"}){
            Feed(npc,PetMenuPacket(html,789));
            Check(npc.World.PetInformation(789)?.Kind==(html=="anicom"?"pet":"summon")&&npc.World.InteractionKind=="dialog"&&npc.World.InteractionNpc==456&&npc.World.InteractionRevision==openNpcRevision&&npc.World.InteractionPage==npcPage,"pet S39 preserves an open unrelated NPC page "+html);
        }
        petRevision=npc.World.PetRevision;
        Feed(npc,PetNpcDialog(""));Check(npc.World.InteractionKind=="closed","empty S39 retains NPC close route");
        Set(npc.World,"Status",npc.World.Status! with{Level=51});
        Feed(npc,PetNpcDialog("RaiseAttr",123));
        Check(npc.World.BonusStatsOffered&&npc.World.BonusStatsPhase=="offered"&&npc.World.PetRevision==petRevision,"RaiseAttr retains self-stat route without generic or pet envelope");
        PetReject(()=>Feed(npc,PetNpcDialog("RaiseAttr",456)),"RaiseAttr foreign identity still rejected");
        // Exercise the lifecycle hook without constructing or showing the game form.
        var form=(ClientForm)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));
        GC.SuppressFinalize(form);
        using var tabs=new TabControl();using var content=new FlowLayoutPanel();using var timer=new System.Windows.Forms.Timer();
        using var existingPage=new TabPage("existing");tabs.TabPages.Add(existingPage);
        Set(form,"petContent",content);Set(form,"timer",timer);
        Call(form,"OnActivated",EventArgs.Empty);
        Check(content.Parent==null,"pet lifecycle tolerates absent normal panels");
        Set(form,"normalPanels",tabs);Call(form,"OnActivated",EventArgs.Empty);
        Check(tabs.TabPages.Count==2&&tabs.TabPages[0]==existingPage&&content.Parent==tabs.TabPages[1],"pet lifecycle preserves existing pages and attaches content");
        Call(form,"OnActivated",EventArgs.Empty);
        Check(tabs.TabPages.Count==2&&!content.IsHandleCreated&&!tabs.IsHandleCreated,"pet lifecycle attaches once without creating control handles");
    }
}
