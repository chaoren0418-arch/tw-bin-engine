using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R7-J（T349／#812）離線測試：合成封包、記憶體內假傳送端；不開遊戲、網路、視窗或素材。
// 涵蓋：C230 type 3 的線上格式（docs/protocol/party.md:21，伺服器 C_CreateParty case 3 讀 D(objid)）；
// 委任只給隊長、只給有物件 ID 的隊員（S39 只有名字的列 ID=0 不送）、不能委任自己；送出後不本地改隊長，等 S250/106；
// 舊快照（版本變了）不重送；面板委任鈕（PartyLayout.Interaction）沒選人不送、選了送 "delegate" 與名字、非隊長不送、忙碌不送。
static class PartyDelegateTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    // S250/104・105：D(id) S(name) C(hp) D(map) H(x) H(y)；104 前面多一個 count。
    static byte[] Row(byte sub,uint id,string name)=>Packet(b=>{b.Write((byte)250);b.Write(sub);if(sub==104)b.Write((byte)1);b.Write(id);Text(b,name);b.Write((byte)80);b.Write(4u);b.Write((ushort)32601);b.Write((ushort)32940);});
    static byte[] NewLeader(uint id,string name)=>Packet(b=>{b.Write((byte)250);b.Write((byte)106);b.Write(id);Text(b,name);});
    static byte[] Roster(string leader,string names)=>Packet(b=>{b.Write((byte)39);b.Write(123u);Text(b,"party");b.Write((ushort)1);b.Write((ushort)2);Text(b,leader);Text(b,names);});
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)4);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Send(LoginConnection c,PartyView v,string name)=>c.SendParty("delegate",v,name).GetAwaiter().GetResult();
    static string Hex(byte[] p)=>Convert.ToHexString(p);

    [WorldModuleTest("party-delegate")]
    static void Run(WorldModuleTestContext t){Wire(t);Session(t);Panel(t);}

    static void Wire(WorldModuleTestContext t){
        t.Equal("E603785634120000",Hex(PartyPackets.Delegate(0x12345678)),"C230 03 D(objid) little-endian, padded to 8");
        t.Equal(8,PartyPackets.Delegate(1).Length,"C230/3 is one 8-byte block");
        bool zero=false;try{PartyPackets.Delegate(0);}catch(ArgumentException){zero=true;}
        t.Check(zero,"object id 0 refused");
        t.Equal((byte)3,PartyPackets.DelegateType,"type 3 = leader transfer (party.md:21)");
        var m=new PartyMember[]{new(11,"Alpha",100),new(0,"NoId",null),new(77,"Self",100)};
        PartyView V(bool leader)=>new(1,1,1,77,m,leader?"Me":"Alpha","",null,true,true,leader);
        t.Equal(11u,PartyPackets.DelegateTarget(V(true),"Alpha"),"leader → member id");
        t.Equal(0u,PartyPackets.DelegateTarget(V(false),"Alpha"),"not leader → not eligible");
        t.Equal(0u,PartyPackets.DelegateTarget(V(true),"NoId"),"name-only member (id 0) → not eligible");
        t.Equal(0u,PartyPackets.DelegateTarget(V(true),"Self"),"own object id → not eligible");
        t.Equal(0u,PartyPackets.DelegateTarget(V(true),"Nobody"),"unknown name → not eligible");
    }

    static void Session(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        t.Check(c.World.Ready,"seed: world ready");
        t.Check(!Send(c,c.PartySnapshot(),"隊員")&&sent.Count==0,"no party → nothing sent");
        // 105：自己是隊長（新隊員資料只傳給隊長）。
        Feed(c,Row(105,456,"隊員"));Feed(c,Row(104,789,"二號"));
        var v=c.PartySnapshot();t.Check(v.CanKick&&v.Members.Length==2,"leader with two members",v);
        t.Check(!Send(c,v,"不存在")&&sent.Count==0,"unknown name → nothing sent");
        t.Check(Send(c,v,"二號"),"delegate sent");
        t.Equal(1,sent.Count,"one packet");
        t.Equal(Hex(LoginPackets.Pad([230,3,..BitConverter.GetBytes(789u)])),Hex(sent[^1]),"C230 03 + member id 789");
        var after=c.PartySnapshot();
        t.Check(after.Leader=="offline"&&after.CanKick,"leader unchanged until the server answers (no optimistic change)",after.Leader);
        t.Check(after.Notice.Contains("委任隊長"),"notice names the delegate request",after.Notice);
        t.Check(!Send(c,v,"二號")&&sent.Count==1,"stale snapshot (revision changed) → no replay");
        // 伺服器重組隊伍後送 106：隊長換人 → 不再能委任。
        Feed(c,NewLeader(789,"二號"));var w=c.PartySnapshot();
        t.Check(w.Leader=="二號"&&!w.CanKick,"S250/106 moves leadership",w.Leader);
        t.Check(!Send(c,w,"隊員")&&sent.Count==1,"not leader → nothing sent");
        // S39 只有名字的隊員沒有物件 ID → 不送。
        var sent2=new List<byte[]>();using var d=Seed(sent2);
        Feed(d,Roster("offline","offline 遠方 "));var x=d.PartySnapshot();
        t.Check(x.CanKick&&x.Members.Single() is {Name:"遠方",ObjectId:0},"S39 name-only member, own leadership",x.Members);
        t.Check(!Send(d,x,"遠方")&&sent2.Count==0,"member without object id → nothing sent");
        // 死亡／忙碌時 PartyAccess 關閉 → 不送。
        var sent3=new List<byte[]>();using var e=Seed(sent3);Feed(e,Row(105,456,"隊員"));
        Set(e.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":0,\"MaxHp\":100}"));
        t.Check(!Send(e,e.PartySnapshot(),"隊員")&&sent3.Count==0,"HP 0 → nothing sent");
    }

    static void Panel(WorldModuleTestContext t){
        var log=new List<(string Cmd,string Name)>();
        var cmd=new UiMacroCommands(new UiPartyCommands(new UiNullCommands(),(c,v,n)=>log.Add((c,n))),(_,_)=>{});   // product nesting
        var m=new PartyMember[]{new(11,"Alpha",100),new(12,"Bravo",50),new(0,"Charlie",null)};
        PartyView V(bool available=true,bool leader=true)=>new(1,1,1,77,m,leader?"Me":"Alpha","",null,available,true,leader);
        var act=new PartyLayout.Interaction();
        t.Check(!act.Fire(PartyLayout.Delegate,V(),cmd)&&log.Count==0,"no selection → nothing sent");
        act.Select("Bravo");
        t.Check(act.Fire(PartyLayout.Delegate,V(),cmd)&&log.Count==1&&log[0]==("delegate","Bravo"),"selected Bravo → \"delegate\" Bravo",log);
        t.Check(!act.Fire(PartyLayout.Delegate,V(leader:false),cmd)&&log.Count==1,"not leader → nothing sent");
        t.Check(!act.Fire(PartyLayout.Delegate,V(available:false),cmd)&&log.Count==1,"busy (Available false) → nothing sent");
        act.Select("Charlie");
        t.Check(!act.Fire(PartyLayout.Delegate,V(),cmd)&&log.Count==1,"member without object id → nothing sent");
        t.Check(!act.Fire(PartyLayout.Delegate,V(),new UiNullCommands())&&log.Count==1,"no party commands → nothing sent");
        // 既有兩鈕不受影響。
        act.Select("Bravo");
        t.Check(act.Fire(PartyLayout.Ban,V(),cmd)&&log[^1]==("kick","Bravo"),"ban still kicks");
        t.Check(act.Fire(PartyLayout.Leave,V(),cmd)&&log[^1]==("leave",""),"leave still leaves");
    }
}
