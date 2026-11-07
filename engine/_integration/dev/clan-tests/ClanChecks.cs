using P06;
using System.Buffers.Binary;

partial class Checks {
    static byte[] ClanUpdatePacket(uint id,string name,string title="",uint reserved=0,byte rank=3)=>Packet(b=>{b.Write((byte)72);b.Write(id);Text(b,name);Text(b,title);b.Write(reserved);b.Write(rank);});
    static byte[] EmblemPacket(uint id,int bits=24,bool topDown=false)=>Packet(b=>{
        int palette=bits<=8?1<<bits:0,stride=((16*bits+31)/32)*4,size=54+palette*4+stride*12;
        b.Write((byte)118);b.Write(id);b.Write((byte)'B');b.Write((byte)'M');b.Write(size);b.Write(0);b.Write(54+palette*4);b.Write(40);b.Write(16);b.Write(topDown?-12:12);b.Write((ushort)1);b.Write((ushort)bits);b.Write(0);b.Write(stride*12);b.Write(new byte[16]);
        for(int i=0;i<palette;i++){b.Write((byte)0);b.Write((byte)0);b.Write((byte)255);b.Write((byte)0);}
        for(int y=0;y<12;y++)for(int x=0;x<stride;x++)b.Write((byte)(bits==24||bits==32?x%(bits/8)==2?255:0:0));
    });
    static async Task ClanChecks(){
        void Reject(Action action,string label){bool failed=false;try{action();}catch(Exception ex) when(ex is ProtocolException or ArgumentException){failed=true;}Check(failed,label);}
        foreach(var command in new[]{ClanCommand.Join,ClanCommand.Leave,ClanCommand.Members}){
            var bytes=ClanProtocol.Encode(command);byte opcode=command==ClanCommand.Join?(byte)194:command==ClanCommand.Leave?(byte)61:(byte)68;
            Check(bytes.SequenceEqual(new byte[]{opcode,0,0,0,0,0,0,0}),"clan empty C format "+command);
        }
        foreach(var command in new[]{ClanCommand.Create,ClanCommand.Ban}){
            var bytes=ClanProtocol.Encode(command,"測試血盟");var r=new LoginReader(bytes);Check(r.C()==(command==ClanCommand.Create?222:69)&&r.S()=="測試血盟","clan Big5 C string "+command);r.Finish();
        }
        var query=ClanProtocol.Encode(ClanCommand.Emblem,emblemId:0x12345678);Check(query.SequenceEqual(Convert.FromHexString("4878563412000000")),"C72 little endian emblem ID");
        Reject(()=>ClanProtocol.Encode(ClanCommand.Create,"a\0b"),"reject NUL injection");Reject(()=>ClanProtocol.Encode(ClanCommand.Ban,"😀"),"reject unmappable name");Reject(()=>ClanProtocol.Encode(ClanCommand.Create,new string('a',33)),"bounded name");Reject(()=>ClanProtocol.Encode(ClanCommand.Emblem),"reject unknown emblem ID");
        var update=ClanProtocol.DecodeUpdate(ClanUpdatePacket(123,"血盟","稱號",0x1234,7));Check(update==new ClanUpdate(123,"血盟","稱號",0x1234,7),"S72 D S S D C layout; reserved is retained only as reserved");
        Reject(()=>ClanProtocol.DecodeUpdate(ClanUpdatePacket(123,"血盟")[..8]),"S72 truncated string");
        var attention=Packet(b=>{b.Write((byte)200);b.Write((ushort)2);b.Write(2u);Text(b,"盟一");Text(b,"盟二");b.Write((ushort)0);});
        Check(ClanProtocol.DecodeAttention(attention).Names.SequenceEqual(new[]{"盟一","盟二"}),"S200 H D repeated S H");
        Check(ClanProtocol.DecodeAttention(Packet(b=>{b.Write((byte)200);b.Write((ushort)2);b.Write(0u);b.Write((ushort)0);})).Names.Length==0,"S200 empty attention");
        Check(ClanProtocol.DecodeAttention(Packet(b=>{b.Write((byte)200);b.Write((ushort)1);b.Write((ushort)0);})).Type==1,"S200 non-list type has only H trailer");
        Reject(()=>ClanProtocol.DecodeAttention(Packet(b=>{b.Write((byte)200);b.Write((ushort)2);b.Write(uint.MaxValue);})),"S200 excessive count");
        foreach(int bits in new[]{1,4,8,16,24,32})foreach(bool topDown in new[]{false,true}){
            var emblem=ClanProtocol.DecodeEmblem(EmblemPacket(99,bits,topDown));Check(emblem.Id==99&&emblem.Pixels.Length==192&&emblem.Pixels.All(p=>p==(bits==16?unchecked((int)0xff000000):unchecked((int)0xffff0000))),$"S118 memory BMP {bits} bit topDown={topDown}");
        }
        var bad=EmblemPacket(99);BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(23),int.MaxValue);Reject(()=>ClanProtocol.DecodeEmblem(bad),"emblem oversized dimensions");
        Reject(()=>ClanProtocol.DecodeEmblem(EmblemPacket(99)[..64]),"emblem truncated pixels");
        bad=EmblemPacket(99);bad[^1]=1;Reject(()=>ClanProtocol.DecodeEmblem(bad),"emblem nonzero padding");
        using(var c=Seed()){
            var actor=c.World.Actor!;Set(c.World,"Actor",actor with{Clan="舊盟",ClanId=99});Feed(c,EmblemPacket(99));Check(c.World.FindClanEmblem(99)!=null,"S118 reaches session cache");
            Feed(c,ClanUpdatePacket(123,"舊盟",reserved:999));Check(c.World.Actor!.ClanId==99,"S72 same clan preserves S87 clan identity");
            Feed(c,ClanUpdatePacket(123,"新盟",reserved:888));Check(c.World.Actor!.Clan=="新盟"&&c.World.Actor.ClanId==0,"S72 changed clan drops old emblem identity");
            Feed(c,ClanUpdatePacket(123,""));Check(c.World.Actor!.Clan==""&&c.World.Actor.ClanId==0,"S72 leave clears name and emblem identity");
            Feed(c,ClanUpdatePacket(456,"客盟"));Check(c.World.Objects.Single(o=>o.ObjectId==456).Clan=="客盟","S72 other object");
            Feed(c,attention);Check(c.World.ClanAttentionNames.Length==2,"S200 dispatch");
            var copy=c.World.FindClanEmblem(99)!;copy.Pixels[0]=0;Check(c.World.FindClanEmblem(99)!.Pixels[0]!=0,"emblem snapshots cannot mutate cache");
            Feed(c,LoginPackets.Pad([118,99,0,0,0,1]));Check(c.World.ClanNotice.Contains("無法解析"),"unsupported image does not disconnect");
            c.World.Reset();Check(c.World.FindClanEmblem(99)==null&&c.World.ClanAttentionNames.Length==0,"world reset invalidates all clan caches");
        }
        foreach(var command in new[]{ClanCommand.Create,ClanCommand.Leave,ClanCommand.Ban,ClanCommand.Members,ClanCommand.Emblem})using(var c=Seed()){
            int sent=0;Sender(c,(bytes,current,admit)=>{if(!current())return Task.FromResult(false);sent++;admit();return Task.FromResult(true);});
            var before=c.World.Actor;Check(await c.SendClan(command,c.ClanSnapshot(),"測試",99)&&sent==1,"memory transport admits "+command);Check(c.World.Actor==before,"request does not invent membership "+command);
        }
        using(var c=Seed()){
            int sent=0;Sender(c,(bytes,current,admit)=>{if(!current())return Task.FromResult(false);sent++;admit();return Task.FromResult(true);});
            Check(!await c.SendClan(ClanCommand.Join,c.ClanSnapshot())&&sent==0,"join requires selected facing peer");
            var actor=c.World.Actor!;Set(c.World,"Actor",actor with{Direction=2});
            var objects=(System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject>)typeof(WorldSession).GetField("objects",F)!.GetValue(c.World)!;
            objects[456]=objects[456] with{Direction=6,Flags=4};
            Check(await c.SendClan(ClanCommand.Join,c.ClanSnapshot())&&sent==1,"C194 admits selected face-to-face recruit request without target payload");
            Sender(c,(bytes,current,admit)=>{objects[456]=objects[456] with{Direction=0};if(!current())return Task.FromResult(false);sent++;admit();return Task.FromResult(true);});
            Check(!await c.SendClan(ClanCommand.Join,c.ClanSnapshot())&&sent==1,"queued join rejects changed facing peer");
        }
        using(var c=Seed()){
            var release=new TaskCompletionSource();int sent=0;Sender(c,async(bytes,current,admit)=>{await release.Task;if(!current())return false;sent++;admit();return true;});
            var stamp=c.ClanSnapshot();var pending=c.SendClan(ClanCommand.Create,stamp,"測試");Check(!await c.SendClan(ClanCommand.Create,stamp,"測試"),"clan double submit rejected");
            Feed(c,LoginPackets.Pad([206,4,0,0]));release.SetResult();Check(!await pending&&sent==0,"queued clan command rejected after world change");
        }
        using(var c=Seed()){
            var stamp=c.ClanSnapshot();c.Cancel();Check(!await c.SendClan(ClanCommand.Create,stamp,"測試"),"old session clan command rejected");
        }
        await ClanInvitationChecks();ClanMemberChecks();
    }
    static byte[] ClanInvite(string name="target",ushort message=97,uint reserved=0)=>Packet(b=>{b.Write((byte)219);b.Write((ushort)0);b.Write(reserved);b.Write(message);Text(b,name);});
    static LoginConnection ClanSeed(){
        var c=Seed();Set(c.World,"Actor",c.World.Actor! with{Clan="測試盟",ClanId=9});
        var objects=(System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject>)typeof(WorldSession).GetField("objects",F)!.GetValue(c.World)!;
        objects[456]=objects[456] with{Flags=4};return c;
    }
    static async Task ClanInvitationChecks(){
        foreach(bool accept in new[]{false,true})using(var c=ClanSeed()){
            byte[]? sent=null;int sends=0;Sender(c,(b,current,admit)=>{if(!current())return Task.FromResult(false);sent=b;sends++;admit();return Task.FromResult(true);});
            Feed(c,ClanInvite());var view=c.ClanSnapshot();var actor=c.World.Actor;
            Check(view.Invitation?.Name=="target"&&view.Invitation.PeerId==456,"S219 message97 reaches clan "+accept);
            Check(await c.AnswerClanInvitation(view,accept),"clan answer admitted "+accept);
            Check(sent!.SequenceEqual(Convert.FromHexString(accept?"79000000000000610001000000000000":"79000000000000610000000000000000")),"d287 C121 exact zero-D answer "+accept);
            Check(c.World.PendingClanInvitation==null&&c.World.Actor==actor,"answer clears pending without invented membership "+accept);
            Check(!await c.AnswerClanInvitation(view,accept)&&sends==1,"answer cannot be replayed "+accept);
        }
        foreach(string reason in new[]{"offline","timeout","cancel","map","disconnect","replacement","trade","other-confirmation","membership"})using(var c=ClanSeed()){
            long now=1000;Set(c,"ActionClock",(Func<long>)(()=>now));Feed(c,ClanInvite());var view=c.ClanSnapshot();int sends=0;
            var release=new TaskCompletionSource();Sender(c,async(b,current,admit)=>{await release.Task;if(!current())return false;sends++;admit();return true;});
            var pending=c.AnswerClanInvitation(view,true);
            Check(!await c.AnswerClanInvitation(view,false),"duplicate queued answer blocked "+reason);
            switch(reason){
                case "offline":Feed(c,Packet(b=>{b.Write((byte)120);b.Write(456u);}));break;
                case "timeout":now+=300000;break;
                case "cancel":Feed(c,Packet(b=>{b.Write((byte)71);b.Write((ushort)99);b.Write((byte)0);}));break;
                case "map":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
                case "disconnect":c.Cancel();break;
                case "replacement":Feed(c,ClanInvite());break;
                case "trade":Feed(c,ClanInvite(message:252));break;
                case "other-confirmation":Feed(c,ClanInvite(message:622));break;
                case "membership":Feed(c,ClanUpdatePacket(123,"別盟"));break;
            }
            release.SetResult();Check(!await pending&&sends==0,"queued stale C121 never sent: "+reason);
            Check(reason=="replacement"?c.World.PendingClanInvitation?.Revision!=view.Invitation!.Revision:c.World.PendingClanInvitation==null,"pending invitation invalidated: "+reason);
        }
        using(var c=ClanSeed()){
            Feed(c,ClanInvite());var view=c.ClanSnapshot();Feed(c,Packet(b=>{b.Write((byte)71);b.Write((ushort)77);b.Write((byte)0);}));
            Check(c.World.PendingClanInvitation==view.Invitation,"informational HP message preserves invitation");
            Feed(c,ClanInvite(reserved:123));Check(c.World.PendingClanInvitation==null,"nonzero raw confirmation token unsupported");
            Feed(c,ClanInvite("missing"));Check(c.World.PendingClanInvitation==null,"unknown applicant cannot create pending answer");
            Feed(c,ClanInvite(message:252));Check(c.World.TradeState=="invited"&&c.World.TradePeerName=="target","S219 message252 trade behavior retained");
            Feed(c,ClanInvite());Check(c.World.TradeState=="attention"&&c.World.PendingClanInvitation==null,"message97 still invalidates outstanding trade invite");
        }
        using(var c=ClanSeed()){
            bool failed=false;try{Feed(c,ClanInvite()[..8]);}catch(ProtocolException){failed=true;}Check(failed,"truncated S219 rejected");
        }
    }
    static void ClanFixed(BinaryWriter b,string value,int length){var bytes=new byte[length];LoginPackets.TextEncoding.GetBytes(value).CopyTo(bytes,0);b.Write(bytes);}
    static byte[] ClanAnnouncementPacket(string name="測試盟")=>Packet(b=>{b.Write((byte)167);Text(b,name);Text(b,"盟主");b.Write(99u);b.Write(123456u);ClanFixed(b,"血盟公告",478);});
    static byte[] ClanMembersPacket(bool empty=false)=>Packet(b=>{
        b.Write((byte)170);b.Write((ushort)1);b.Write((byte)(empty?0:2));if(empty)return;
        foreach(var row in new[]{("盟主",(byte)10,123u),("成員",(byte)3,456u)}){Text(b,row.Item1);b.Write(row.Item2);b.Write((byte)50);ClanFixed(b,"備註",62);b.Write(row.Item3);b.Write((byte)1);}
    });
    static byte[] ClanOnlinePacket(params string[] names)=>Packet(b=>{b.Write((byte)250);b.Write((byte)171);b.Write((ushort)names.Length);foreach(string name in names){Text(b,name);b.Write((byte)0);}});
    static void ClanMemberChecks(){
        using var c=ClanSeed();
        Feed(c,ClanAnnouncementPacket());var info=c.ClanSnapshot().Announcement;
        Check(info==new ClanAnnouncement("測試盟","盟主",99,123456,"血盟公告"),"S167 names D D and fixed 478-byte Big5 announcement");
        Feed(c,ClanMembersPacket());var members=c.ClanSnapshot().Members;
        Check(members.Length==2&&members[1]==new ClanMember("成員",3,50,"備註",456,1),"S170 name rank level fixed notes D class; online unknown");
        Feed(c,ClanOnlinePacket("盟主"));members=c.ClanSnapshot().Members;
        Check(members[0].Online==true&&members[1].Online==false,"S250/sub171 H count S plus zero maps online by name");
        Feed(c,Packet(b=>{b.Write((byte)200);b.Write((ushort)2);b.Write(1u);Text(b,"關注盟");b.Write((ushort)0);}));
        Check(c.ClanSnapshot().Members.SequenceEqual(members)&&c.ClanSnapshot().Attention.Single()=="關注盟","S200 stays separate from members");
        Feed(c,ClanOnlinePacket());Check(c.ClanSnapshot().Members.All(m=>m.Online==false),"empty online list clears online status");
        Feed(c,ClanAnnouncementPacket());Feed(c,ClanOnlinePacket("成員"));Feed(c,ClanMembersPacket());
        Check(c.ClanSnapshot().Members[1].Online==true,"online list before members within S167 batch preserved");
        var before=c.ClanSnapshot();
        foreach(var packet in new[]{ClanAnnouncementPacket()[..32],ClanMembersPacket()[..24],ClanOnlinePacket("盟主")[..8],LoginPackets.Pad([250,171,255,255])}){
            bool failed=false;try{Feed(c,packet);}catch(ProtocolException){failed=true;}Check(failed,"malformed clan list rejected opcode "+packet[0]);
            Check(c.ClanSnapshot().Announcement==before.Announcement&&c.ClanSnapshot().Members.SequenceEqual(before.Members),"malformed list leaves prior snapshot intact");
        }
        var wrongTrailer=ClanOnlinePacket("a");wrongTrailer[6]=1;
        bool rejected=false;try{Feed(c,wrongTrailer);}catch(ProtocolException){rejected=true;}Check(rejected,"online extra C must be zero");
        Feed(c,LoginPackets.Pad([250,34,60,0]));Check(c.World.MagicBuffs.Length>0,"existing S250 magic dispatch retained");
        Feed(c,ClanMembersPacket(true));Check(c.ClanSnapshot().Members.Length==0,"empty S170 clears members");
        Feed(c,ClanMembersPacket());Feed(c,ClanUpdatePacket(123,""));Check(c.ClanSnapshot().Members.Length==0&&c.ClanSnapshot().Announcement==null,"leaving clan clears member snapshot");
        Feed(c,ClanAnnouncementPacket("別盟"));Feed(c,ClanMembersPacket());Check(c.ClanSnapshot().Members.Length==0,"unrelated S167 does not bind members to own clan");
        c.World.Reset();Check(c.ClanSnapshot().Members.Length==0,"world reset clears member snapshot");
    }
}
