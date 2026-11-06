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
    }
}
