using System.Reflection;
using System.Text;
using P06.NativeUi;
namespace P06;

// R2-J（T263／#624）離線測試：合成封包、合成字串表、記憶體送出閘門；不開遊戲、網路、DB，也不讀客戶端素材。由 --module-tests 執行。
// 涵蓋：復活 322 走共用詢問槽、Esc 送拒絕、組隊／求婚沒有本地逾時、PartyUi／DuelUi 沒有回覆鈕、提示讀字串表、求婚入口、YES／NO 原生面板。
public sealed partial class WorldSession {
    internal void SeedYesNoFollowupTest(bool dead,ushort map=0,ushort x=32600,ushort y=32940){
        Entered=true;MapId=map;TimeReplies=1;CacheEnabled=false;
        Status=new(YesNoFollowupTests.Own,1,0,10,10,10,10,10,10,dead?0u:100u,100,100,100,10,0,0,0,0,0,0,0,0,0);
        Actor=new(x,y,YesNoFollowupTests.Own,61,0,0,0,0,0,0,YesNoFollowupTests.Name,"",0,0,"","",0,100,0,1,0,0,0);
        OwnDead=dead;
    }
}
public sealed partial class LoginConnection {
    internal void SeedYesNoFollowupTest(bool dead,List<byte[]> sent,ushort map=0,ushort x=32600,ushort y=32940){
        World.SeedYesNoFollowupTest(dead,map,x,y);State=LoginState.InWorld;
        sendMovementGate=(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);};
    }
}

static class YesNoFollowupTests {
    public const uint Own=123,Peer=456;
    public const string Name="yes-no-test";
    static byte[] Packet(Action<BinaryWriter> write){using var m=new MemoryStream();using(var b=new BinaryWriter(m,Encoding.UTF8,true))write(b);return LoginPackets.Pad(m.ToArray());}
    static void Text(BinaryWriter b,string s){b.Write(LoginPackets.TextEncoding.GetBytes(s));b.Write((byte)0);}
    static byte[] Ask(ushort message,uint request=0,params string[] args)=>Packet(b=>{b.Write((byte)219);b.Write((ushort)0);b.Write(request);b.Write(message);foreach(var s in args)Text(b,s);});
    static byte[] Revive(uint id=Own)=>Packet(b=>{b.Write((byte)85);b.Write(id);b.Write((byte)0);b.Write(789u);b.Write(0u);});
    // Synthetic S87 for an adjacent player (same layout as the dev marriage fixture; values are test data).
    static byte[] Player(uint id,ushort x,ushort y,string name)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)1);
        b.Write((byte)0);b.Write((byte)4);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);
        Text(b,name);Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");
        b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static LoginConnection World(bool dead,List<byte[]> sent){var c=new LoginConnection();c.SeedYesNoFollowupTest(dead,sent);return c;}
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();

    [WorldModuleTest("yes-no-followup")]
    static void Run(WorldModuleTestContext t){
        // Without the original table (CI) the fallback wording is used; tests never read the client's Text.pak.
        using(ServerMessageText.UseTable(null)){Protocol(t);Resurrection(t);Escape(t);Party(t);Marriage(t);Ui(t);Panel(t);}
        StringTable(t);
    }

    static void Protocol(WorldModuleTestContext t){
        t.Check(YesNoProtocol.Generic(322)&&YesNoProtocol.ModuleOwned(322),"322 is generic and admitted by Resurrection (WO §6)");
        t.Check(YesNoProtocol.Special(321)&&!YesNoProtocol.Generic(321),"321 stays a special S_ASK case (WIN §2.1)");
        foreach(ushort m in new ushort[]{97,221,222})t.Check(!YesNoProtocol.Generic(m),"clan "+m+" still delegated (R2-K)");
        t.Equal((int?)null,YesNoProtocol.TimeoutMs(322),"322 has no timeout");
        t.Equal((int?)null,YesNoProtocol.TimeoutMs(953),"953 has no timeout");
        t.Equal((int?)null,YesNoProtocol.TimeoutMs(654),"654 has no timeout");
        t.Check(YesNoProtocol.Text(322,[]).StartsWith("有人要讓你復活",StringComparison.Ordinal),"322 fallback text");
    }

    static void Resurrection(WorldModuleTestContext t){
        foreach(bool accept in new[]{true,false}){
            var sent=new List<byte[]>();using var c=World(true,sent);
            c.World.Receive(Ask(322),"offline");var v=c.YesNoSnapshot();
            t.Check(v.Prompt is {Message:322,RequestId:0,Deadline:null},"dead S219/322 queued in the shared slot "+accept);
            var rez=c.ResurrectionSnapshot();
            t.Check(rez.Phase=="invited"&&!WorldSession.ResurrectionUsesPanel(rez),"322 does not open the resurrection panel "+accept);
            t.Check(Wait(c.AnswerYesNo(v,accept?"y":"n"))&&sent.Count==1&&sent[0].SequenceEqual(ResurrectionPackets.Answer(322,accept)),"Y/N answers C121 H0 D0 H322 C "+accept);
            t.Check(c.YesNoSnapshot().Prompt==null&&c.ResurrectionSnapshot().Phase==(accept?"accepting":"rejected"),"answer clears the slot and records the module phase "+accept);
            t.Check(!Wait(c.AnswerYesNo(v,"y"))&&sent.Count==1,"answer single use "+accept);
        }
        {
            var sent=new List<byte[]>();using var c=World(true,sent);
            c.World.Receive(Ask(321),"offline");
            // R7-B (T341/#804): 321 moved to the shared input line as well (WO §6); InputLineFollowupTests covers the answers.
            t.Check(c.YesNoSnapshot().Prompt?.Message==321&&!WorldSession.ResurrectionUsesPanel(c.ResurrectionSnapshot()),"321 uses the shared input line");
        }
        {
            var sent=new List<byte[]>();using var c=World(false,sent);
            c.World.Receive(Ask(322),"offline");
            t.Check(c.YesNoSnapshot().Prompt==null,"living role: 322 not queued");
        }
        {
            var sent=new List<byte[]>();using var c=World(true,sent);
            c.World.Receive(Ask(322),"offline");
            t.Check(Wait(c.AnswerResurrection(c.ResurrectionSnapshot(),true))&&c.YesNoSnapshot().Prompt==null,"module answer clears the shared slot");
        }
        {
            var sent=new List<byte[]>();using var c=World(true,sent);
            c.World.Receive(Ask(322),"offline");c.World.Receive(Revive(),"offline");
            t.Check(c.YesNoSnapshot().Prompt==null&&sent.Count==0,"S85 ends the 322 question without sending");
        }
        {
            var sent=new List<byte[]>();using var c=World(true,sent);
            c.World.Receive(Ask(322),"offline");var old=c.YesNoSnapshot();c.World.Receive(Ask(1000),"offline");
            t.Check(c.YesNoSnapshot().Prompt?.Message==1000&&c.ResurrectionSnapshot().Phase=="expired"&&!Wait(c.AnswerYesNo(old,"y"))&&sent.Count==0,"newer S219 replaces 322");
        }
    }

    static void Escape(WorldModuleTestContext t){
        t.Check(ClientForm.YesNoEscapeDeclines(Keys.Escape,true,true,true,false),"Esc with a prompt declines");
        t.Check(!ClientForm.YesNoEscapeDeclines(Keys.Escape,true,false,true,false),"Esc without a prompt keeps its old meaning");
        t.Check(!ClientForm.YesNoEscapeDeclines(Keys.Escape,true,true,true,true),"auto-repeat Esc ignored");
        t.Check(!ClientForm.YesNoEscapeDeclines(Keys.Escape,true,true,false,false),"Esc outside this form ignored");
        t.Check(!ClientForm.YesNoEscapeDeclines(Keys.Escape,false,true,true,false),"Esc out of world ignored");
        t.Check(!ClientForm.YesNoEscapeDeclines(Keys.Escape|Keys.Shift,true,true,true,false),"Shift+Esc ignored");
        t.Check(!ClientForm.YesNoEscapeDeclines(Keys.Enter,true,true,true,false),"Enter is not the Esc path");
        // Esc uses the same decline as N + Enter.
        var sent=new List<byte[]>();using var c=World(false,sent);
        c.World.Receive(Ask(1000,0,"甲方"),"offline");
        t.Check(Wait(c.AnswerYesNo(c.YesNoSnapshot(),false))&&sent.Single().SequenceEqual(YesNoProtocol.Answer(0,1000,false)),"decline sends C121 C0");
    }

    static void Party(WorldModuleTestContext t){
        t.Check(typeof(PartyInvitation).GetProperty("Expires")==null,"party invitation has no local expiry field");
        t.Check(typeof(WorldSession).GetField("PartyClock",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)==null,"party 30 s clock removed");
        var sent=new List<byte[]>();using var c=World(false,sent);
        c.World.Receive(Ask(953,77,"邀請者"),"offline");
        for(int i=0;i<3;i++)c.PartySnapshot();
        t.Check(c.PartySnapshot().Invitation is {Challenge:77}&&c.YesNoSnapshot().Prompt?.Message==953,"party invitation stays until answered or replaced");
        t.Check(Wait(c.AnswerYesNo(c.YesNoSnapshot(),"y"))&&sent.Single().SequenceEqual(PartyPackets.Answer(77,true)),"party answered from the shared slot");
    }

    static void Marriage(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=new LoginConnection();c.SeedYesNoFollowupTest(false,sent,4,33975,33364);
        long now=1000;c.World.InteractionClock=()=>now;
        c.World.Receive(Player(Peer,33975,33363,"伴侶"),"offline");
        c.World.Receive(Ask(654,0,"伴侶"),"offline");
        t.Check(c.MarriageSnapshot() is {Phase:"invited",CanAnswer:true},"marriage invitation fixture");
        now+=NpcInteractionTimeout()*4;
        t.Check(c.MarriageSnapshot() is {Phase:"invited",CanAnswer:true}&&c.YesNoSnapshot().Prompt?.Message==654,"marriage invitation has no local 15 s expiry");
        t.Check(Wait(c.AnswerYesNo(c.YesNoSnapshot(),"n"))&&sent.Single().SequenceEqual(MarriagePackets.Answer(false)),"marriage answered after the old limit");
    }
    static long NpcInteractionTimeout()=>WorldSession.InteractionTimeoutMs;

    static void Ui(WorldModuleTestContext t){
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        t.Check(typeof(PartyUi).GetField("accept",F)==null&&typeof(PartyUi).GetField("reject",F)==null,"PartyUi has no accept/reject buttons");
        t.Check(typeof(ClientForm).GetField("duelAccept",F)==null&&typeof(ClientForm).GetField("duelReject",F)==null,"DuelUi has no accept/reject buttons");
        t.Check(typeof(ClientForm).GetField("marriagePropose",F)?.FieldType==typeof(Button),"proposal entry button exists");
        t.Check(typeof(LoginConnection).GetMethod("ProposeMarriage")?.ReturnType==typeof(Task<bool>),"proposal API for the HTML action hook");
        // The entry uses the guarded send: outside the wedding area nothing is sent.
        var sent=new List<byte[]>();using var c=World(false,sent);
        t.Check(!Wait(c.ProposeMarriage())&&sent.Count==0,"proposal refused outside the wedding area");
        var w=new List<byte[]>();using var m=new LoginConnection();m.SeedYesNoFollowupTest(false,w,4,33975,33364);
        m.World.Receive(Player(Peer,33975,33363,"伴侶"),"offline");m.World.SelectTarget(Peer);
        t.Check(Wait(m.ProposeMarriage())&&w.Single().SequenceEqual(MarriagePackets.Propose())&&m.MarriageSnapshot().Phase=="proposing","proposal sends C50 once to the facing player");
    }

    sealed class Source:IYesNoSource {
        public YesNoPanelView? Current{get;set;}
        public List<(int Revision,bool Accept)> Answers{get;}=[];
        public void Answer(int revision,bool accept)=>Answers.Add((revision,accept));
    }
    static void Panel(WorldModuleTestContext t){
        var registrations=UiPanelRegistry.Panels.Where(p=>p.Id==YesNoPanel.PanelId).ToList();
        t.Equal(1,registrations.Count,"YesNoPanel registered once");
        var p=new YesNoPanel();var src=new Source();p.Source=src;var cmd=new UiNullCommands();
        t.Equal(UiLayer.Float,p.Layer,"Float layer (above the chat window)");
        // R7-B (T341/#804): the bounds also cover the input line (192,581) 280×12, where the question is drawn.
        t.Equal(Rectangle.Union(Rectangle.Union(YesNoButtons.Logical(true),YesNoButtons.Logical(false)),YesNoButtons.Line),p.Bounds,"bounds = YES (361,567) ∪ NO (401,567) ∪ input line");
        p.Update(new UiStaticGameState(InWorld:true));
        t.Check(!p.Visible&&!p.Key(new(Keys.Escape,true,false),cmd)&&!p.Mouse(new(UiMouseKind.Down,new(361,567),MouseButtons.Left,1,0,Keys.None),cmd),"hidden without a question");
        src.Current=new(7,"問題");p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible,"hidden out of world");
        p.Update(new UiStaticGameState(InWorld:true));t.Check(p.Visible,"shown with a question");
        using(var canvas=new UiCanvas()){try{p.Paint(canvas);t.Check(true,"paints");}catch(Exception ex){t.Check(false,"paints",ex.Message);}}
        t.Check(p.Mouse(new(UiMouseKind.Down,new(365,570),MouseButtons.Left,1,0,Keys.None),cmd)&&src.Answers.SequenceEqual([(7,true)]),"YES click answers accept");
        p.Update(new UiStaticGameState(InWorld:true));
        t.Check(p.Mouse(new(UiMouseKind.Down,new(405,570),MouseButtons.Left,1,0,Keys.None),cmd)&&src.Answers[^1]==(7,false),"NO click answers decline");
        p.Update(new UiStaticGameState(InWorld:true));int count=src.Answers.Count;
        t.Check(p.Mouse(new(UiMouseKind.Down,new(395,570),MouseButtons.Left,1,0,Keys.None),cmd)&&src.Answers.Count==count,"gap between buttons consumed, no answer");
        t.Check(!p.Mouse(new(UiMouseKind.Down,new(100,100),MouseButtons.Left,1,0,Keys.None),cmd),"outside passes through");
        t.Check(p.Mouse(new(UiMouseKind.Down,new(365,570),MouseButtons.Right,1,0,Keys.None),cmd)&&src.Answers.Count==count,"right click does not answer");
        t.Check(!p.Key(new(Keys.Escape,true,true),cmd)&&src.Answers.Count==count,"auto-repeat Esc ignored");
        t.Check(!p.Key(new(Keys.Enter,true,false),cmd),"Enter left to the chat line");
        t.Check(p.Key(new(Keys.Escape,true,false),cmd)&&src.Answers[^1]==(7,false)&&!p.Visible,"Esc answers decline once");
        t.Check(!p.Key(new(Keys.Escape,true,false),cmd)&&src.Answers.Count==count+1,"second Esc before the next update does not answer again");
    }

    static void StringTable(WorldModuleTestContext t){
        // Synthetic CP950 table (test data, not the original string-c.tbl): id 953 has a %0 placeholder and a colour control.
        var lines=new string[1000];for(int i=0;i<lines.Length;i++)lines[i]="";
        lines[953]="\\f3%0 想邀請你加入隊伍（測試字串）";lines[322]="測試復活詢問";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var table=ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(lines.Length+"\n"+string.Join("\n",lines)));
        using(ServerMessageText.UseTable(table)){
            t.Equal("甲 想邀請你加入隊伍（測試字串）"+YesNoProtocol.Hint,YesNoProtocol.Text(953,["甲"]),"953 formatted from the table");
            t.Equal("測試復活詢問"+YesNoProtocol.Hint,YesNoProtocol.Text(322,[]),"322 from the table");
            t.Equal(YesNoProtocol.FallbackText(654,["乙"])+YesNoProtocol.Hint,YesNoProtocol.Text(654,["乙"]),"empty table row falls back");
            t.Equal(YesNoProtocol.FallbackText(1500,[])+YesNoProtocol.Hint,YesNoProtocol.Text(1500,[]),"id past the table falls back");
        }
        using(ServerMessageText.UseTable(null))t.Equal("甲 邀請你加入隊伍。"+YesNoProtocol.Hint,YesNoProtocol.Text(953,["甲"]),"no table: engine wording");
    }
}
