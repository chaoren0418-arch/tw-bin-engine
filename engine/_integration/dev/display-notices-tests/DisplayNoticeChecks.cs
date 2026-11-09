using P06;
using System.Drawing;

partial class Checks {
    // Every synthetic server packet enters the product's WorldSession.Receive via Feed.
    static byte[] IdentifyPacket(ushort desc,ushort message,byte count,params string[] values)=>Packet(b=>{b.Write((byte)245);b.Write(desc);b.Write(message);b.Write(count);foreach(string s in values)Text(b,s);});
    static byte[] RedPacket(ushort id,params string[] values)=>Packet(b=>{b.Write((byte)105);b.Write(id);b.Write((byte)values.Length);foreach(string s in values)Text(b,s);});
    static byte[] ChangeNamePacket(uint id,string name)=>Packet(b=>{b.Write((byte)46);b.Write(id);Text(b,name);});
    static byte[] PinkPacket(uint id,byte seconds)=>Packet(b=>{b.Write((byte)60);b.Write(id);b.Write(seconds);});
    static byte[] TrueTargetPacket(uint target,uint viewer,string message)=>Packet(b=>{b.Write((byte)11);b.Write(target);b.Write(viewer);Text(b,message);});
    static byte[] TrueTargetGfxPacket(uint target,ushort gfx)=>Packet(b=>{b.Write((byte)11);b.Write(target);b.Write(target);b.Write((byte)0);b.Write(gfx);});
    static void NoticeBad(LoginConnection c,byte[] bytes,string label){
        var before=(c.World.LastIdentifyDesc,c.World.ActiveRedNotice(),c.World.Target?.Name,c.World.IsPinkName(456),c.World.TrueTarget(456));bool rejected=false;
        try{Feed(c,bytes);}catch(ProtocolException){rejected=true;}
        Check(rejected,label+" rejected");Check(before==(c.World.LastIdentifyDesc,c.World.ActiveRedNotice(),c.World.Target?.Name,c.World.IsPinkName(456),c.World.TrueTarget(456)),label+" atomic");
    }
    static int NoticeUnhandled(LoginConnection c,byte opcode)=>((System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",F)!.GetValue(c.World)!).GetValueOrDefault(opcode);
    [CheckGroup("display-notices"),CheckGroup("map-continuity",Order=30),CheckGroup(CheckGroups.Default,Order=270)]
    static void DisplayNoticeChecks(){
        using(var c=Seed()){
            // S245: every branch of S_IdentifyDesc(item).
            Feed(c,IdentifyPacket(77,134,3,"$227 $1","3+2","5+2"));Check(c.World.LastIdentifyDesc is {DescId:77,MessageId:134,Recognized:true,Weight:null,Text:"$227 $1：小型怪物打擊3+2 大型怪物打擊5+2"},"notice S245 weapon 134");
            Feed(c,IdentifyPacket(78,135,2,"$2","4+1"));Check(c.World.LastIdentifyDesc?.Text=="$2：防御力4+1 防御具","notice S245 armor 135 has no weight string");
            Feed(c,IdentifyPacket(79,137,3,"$3","12","30"));Check(c.World.LastIdentifyDesc?.Text=="$3：可用次數12［重量30］","notice S245 wand 137");
            Feed(c,IdentifyPacket(80,136,3,"$4","9","2"));Check(c.World.LastIdentifyDesc?.Text=="$4：飽食度9［重量2］","notice S245 food 136");
            Feed(c,IdentifyPacket(81,138,2,"$5: $231 600","1"));Check(c.World.LastIdentifyDesc?.Text=="$5: $231 600：［重量1］","notice S245 light 138 keeps fuel tokens");
            Feed(c,IdentifyPacket(82,138,2,"$6","7"));Check(c.World.LastIdentifyDesc is {DescId:82,Text:"$6：［重量7］"},"notice S245 etc 138");
            Feed(c,IdentifyPacket(83,134,3,"骰子匕首","1+0","1+0","3"));Check(c.World.LastIdentifyDesc is {DescId:83,Weight:"3",Text:"骰子匕首：小型怪物打擊1+0 大型怪物打擊1+0［重量3］"}&&c.World.LastIdentifyDesc.Arguments.Length==3,"notice S245 test constructor uncounted weight");
            var kept=c.World.LastIdentifyDesc;Feed(c,Packet(b=>{b.Write((byte)245);b.Write((ushort)84);}));Check(ReferenceEquals(kept,c.World.LastIdentifyDesc),"notice S245 description id only shows nothing");
            Feed(c,IdentifyPacket(85,999,1,"%1"));Check(c.World.LastIdentifyDesc is {Recognized:false,Text:"原服訊息 999：%1"},"notice S245 unknown message stays labeled");
            Feed(c,IdentifyPacket(86,135,2,"%1","%0"));Check(c.World.LastIdentifyDesc?.Text=="%1：防御力%0 防御具","notice arguments are literal");
            Check(c.World.LastServerMessage==null&&c.World.LastServerReply==null,"notice S245 leaves server reply state");
            long at=c.World.LastIdentifyDesc!.At;Check(c.World.ActiveIdentifyDesc(at+DisplayNoticeText.IdentifyNoticeMs-1)!=null&&c.World.ActiveIdentifyDesc(at+DisplayNoticeText.IdentifyNoticeMs)==null,"notice S245 display window");

            // S105: every S_BlueMessage arity seen on the server.
            Feed(c,RedPacket(551,"5","100"));Check(c.World.ActiveRedNotice() is {Id:551,Recognized:true,Text:"你的PK次數已達5。達100次將下地獄。"},"notice S105 551");
            Check(ServerStatusMessages_CenterNotice(c)=="你的PK次數已達5。達100次將下地獄。","notice S105 center mount");
            Feed(c,RedPacket(552,"12","10"));Check(c.World.ActiveRedNotice()?.Text=="因為你已經殺了 12 人所以被打入地獄。 你將在這裡停留 10 分鐘。","notice S105 552");
            Feed(c,RedPacket(637,"300"));Check(c.World.ActiveRedNotice()?.Text=="你必須在此地停留 300 秒。","notice S105 637 one string");
            Feed(c,RedPacket(700));Check(c.World.ActiveRedNotice() is {Id:700,Recognized:false,Text:"原服訊息 700"}&&c.World.ActiveRedNotice()!.Arguments.Length==0,"notice S105 null info count zero");
            long red=c.World.ActiveRedNotice()!.At;Check(c.World.ActiveRedNotice(red+DisplayNoticeText.RedNoticeMs-1)!=null&&c.World.ActiveRedNotice(red+DisplayNoticeText.RedNoticeMs)==null,"notice S105 display window");
            Check(c.World.LastServerMessage==null&&c.World.LastServerReply==null,"notice S105 leaves server reply state");

            // S46: all three S_ChangeName forms.
            Feed(c,ChangeNamePacket(456,"小黑"));Check(c.World.Target?.Name=="小黑","notice S46 plain rename");
            Feed(c,ChangeNamePacket(456,"\\f=\\f3GM \\f=管理員"));Check(c.World.Target?.Name=="GM 管理員","notice S46 GM form strips colors");
            Feed(c,ChangeNamePacket(456,"\\f=\\f<GM \\f=管理員"));Check(c.World.Target?.Name=="GM 管理員","notice S46 GM mode 10");
            Feed(c,ChangeNamePacket(456,"\\f2爵位者"));Check(c.World.Target?.Name=="爵位者","notice S46 title color form");
            Feed(c,ChangeNamePacket(123,"\\f=\\f1GM \\f=offline"));Check(c.World.Actor?.Name=="offline","notice S46 keeps own identity");
            int unknown=c.World.UnknownObjectUpdates;Feed(c,ChangeNamePacket(999,"遠方"));Check(c.World.UnknownObjectUpdates==unknown+1,"notice S46 unknown object counted");
            Check(DisplayNoticeText.PlainName("甲\\f")=="甲\\f","notice incomplete color code stays literal");

            // S60: duration comes from the packet; 0 clears.
            long before=Environment.TickCount64;Feed(c,PinkPacket(456,180));long after=Environment.TickCount64;
            Check(c.World.IsPinkName(456),"notice S60 pink on");Check(c.World.NameColor(456,Color.Gold)==DisplayNoticeText.PinkName,"notice S60 name color query");
            Check(c.World.IsPinkName(456,before+179000)&&!c.World.IsPinkName(456,after+180000),"notice S60 uses server seconds");
            Check(c.World.NameColor(789,Color.Gold)==Color.Gold,"notice S60 other object keeps color");
            Feed(c,PinkPacket(456,255));Check(c.World.IsPinkName(456,before+254000),"notice S60 byte is unsigned");
            Feed(c,PinkPacket(456,0));Check(!c.World.IsPinkName(456)&&c.World.NameColor(456,Color.Gold)==Color.Gold,"notice S60 zero clears");
            Feed(c,PinkPacket(456,127));Check(c.World.IsPinkName(456,long.MaxValue-1),"notice S60 127 is permanent (no timer)");
            Check(c.World.NameColor555(456)==NameColors.Pink&&DisplayNoticeText.PinkName==NameColors.ToColor(0x767D),"notice S60 pink is name colour 0x767D");
            Feed(c,PinkPacket(456,0));Check(!c.World.IsPinkName(456),"notice S60 zero clears permanent pink");
            Feed(c,PinkPacket(123,180));Check(c.World.IsPinkName(123),"notice S60 own object");Feed(c,PinkPacket(123,0));

            // S11: both S_TrueTarget forms.
            Feed(c,TrueTargetPacket(456,123,"集火"));Check(c.World.TrueTarget(456) is {ObjectId:123,Message:"集火",Gfx:0},"notice S11 message form");
            Feed(c,TrueTargetPacket(456,123,""));Check(c.World.TrueTarget(456) is {Message:"",Gfx:0},"notice S11 empty message replaces mark");
            Feed(c,TrueTargetGfxPacket(456,1765));Check(c.World.TrueTarget(456) is {ObjectId:456,Message:"",Gfx:1765},"notice S11 gfx form");
            Feed(c,TrueTargetPacket(123,123,""));Check(c.World.TrueTarget(123) is {Gfx:0},"notice S11 self target padding is not gfx");
            Feed(c,TrueTargetPacket(999,123,"場外"));Check(c.World.TrueTarget(999)==null,"notice S11 absent target hidden");

            foreach(byte op in new byte[]{245,105,46,60,11})Check(NoticeUnhandled(c,op)==0,"notice dispatch handled "+op);

            NoticeBad(c,IdentifyPacket(1,135,2,"甲","乙")[..4],"notice S245 truncated");
            var tail=PinkPacket(456,9);tail[^1]=1;NoticeBad(c,tail,"notice S60 nonzero padding");
            NoticeBad(c,Packet(b=>{b.Write((byte)105);b.Write((ushort)637);b.Write((byte)2);Text(b,"123");}),"notice S105 missing string");
            NoticeBad(c,IdentifyPacket(1,135,2,"甲","乙","丙"),"notice S245 extra string on non-weapon");
            NoticeBad(c,Packet(b=>{b.Write((byte)46);b.Write(456u);b.Write(new byte[]{0x41,0x41,0x41});}),"notice S46 unterminated name");
            NoticeBad(c,Packet(b=>{b.Write((byte)11);b.Write(456u);b.Write(123u);b.Write((byte)0);b.Write((ushort)7);}),"notice S11 gfx with different viewer");

            // A pending-object removal and a new world both end object-scoped state.
            Feed(c,PinkPacket(456,180));Feed(c,TrueTargetPacket(456,123,"集火"));
            Feed(c,Packet(b=>{b.Write((byte)120);b.Write(456u);}));Check(c.World.TrueTarget(456)==null,"notice S11 ends when target leaves");
            Feed(c,Pack(456,32601));Feed(c,TrueTargetPacket(456,123,"集火"));Check(c.World.TrueTarget(456)!=null&&c.World.IsPinkName(456),"notice state before reset");
            c.World.Reset();Check(!c.World.IsPinkName(456)&&c.World.TrueTarget(456)==null&&c.World.ActiveRedNotice()==null&&c.World.ActiveIdentifyDesc()==null,"notice reset hides session state");
        }
    }
    static string? ServerStatusMessages_CenterNotice(LoginConnection c)=>(string?)typeof(WorldSession).Assembly.GetType("P06.ServerStatusMessages")!.GetMethod("CenterNotice")!.Invoke(null,[c.World]);
}
