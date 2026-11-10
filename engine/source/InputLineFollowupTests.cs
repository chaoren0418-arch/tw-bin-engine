using System.Reflection;
using System.Text;
using P06.NativeUi;
namespace P06;

// R7-B（T341／#804）離線測試：合成封包、記憶體送出閘門、假時鐘；不開遊戲、網路、DB，也不讀客戶端素材。由 --module-tests 執行。
// 涵蓋：復活 321 走共用輸入列（WO §6）；YES／NO 提示文字畫在輸入列 (192,581) 280×12、原生開啟時藏 WinForms 提示列；
// 立即型表情的 byte_153CE69（動作結束後 700 ms，0x4FBABC／0xA4B573）；S103 型 8 保留 +547 給表情節流（sub_9343C0、sub_4FAAF0）。
public sealed partial class WorldSession {
    internal void SeedInputLineTest(bool dead,ushort gfx,ushort map){
        Entered=true;MapId=map;TimeReplies=1;CacheEnabled=false;
        Status=new(InputLineFollowupTests.Own,50,0,10,10,10,10,10,10,dead?0u:100u,100,100,100,10,0,0,0,0,0,0,0,0,0);
        Actor=new(32600,32940,InputLineFollowupTests.Own,gfx,0,0,0,0,0,0,InputLineFollowupTests.Name,"",0,0,"","",0,100,0,50,0,0,0);
        OwnDead=dead;
    }
}
public sealed partial class LoginConnection {
    internal void SeedInputLineTest(List<byte[]> sent,bool dead=false,ushort gfx=61,ushort map=4){
        World.SeedInputLineTest(dead,gfx,map);State=LoginState.InWorld;
        sendMovementGate=(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);};
    }
}

static class InputLineFollowupTests {
    public const uint Own=0x7804,Other=0x7805;
    public const string Name="r7b-test";
    static byte[] Packet(Action<BinaryWriter> write){using var m=new MemoryStream();using(var b=new BinaryWriter(m,Encoding.UTF8,true))write(b);return LoginPackets.Pad(m.ToArray());}
    static byte[] Ask(ushort message,uint request=0)=>Packet(b=>{b.Write((byte)219);b.Write((ushort)0);b.Write(request);b.Write(message);});
    static byte[] Revive()=>Packet(b=>{b.Write((byte)85);b.Write(Own);b.Write((byte)0);b.Write(789u);b.Write(0u);});
    static byte[] Liquor(uint id,byte type)=>LoginPackets.Pad([103,..BitConverter.GetBytes(id),type]);
    static LoginConnection Seed(List<byte[]> sent,bool dead=false,ushort gfx=61,ushort map=4){var c=new LoginConnection();c.SeedInputLineTest(sent,dead,gfx,map);return c;}
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static void SetActor(WorldSession w,OwnActor a)=>typeof(WorldSession).GetProperty("Actor")!.SetValue(w,a);
    static int Unhandled(WorldSession w,byte opcode)=>((System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(w)!).GetValueOrDefault(opcode);

    [WorldModuleTest("input-line-followup")]
    static void Run(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){Resurrection321(t);LineLayout(t);Panel(t);}
        EmoteSettle(t);ThirdSpeed(t);HoldScope(t);
    }

    // WO §6: 321 goes straight to sub_4FC710's input line (322 reads one `s` first). Answer = C121 H0 D0 H321 C.
    static void Resurrection321(WorldModuleTestContext t){
        t.Check(YesNoProtocol.ModuleOwned(321)&&YesNoProtocol.Special(321),"321 is admitted by Resurrection, still an S_ASK special id");
        foreach(bool accept in new[]{true,false}){
            var sent=new List<byte[]>();using var c=Seed(sent,dead:true);
            c.World.Receive(Ask(321),"offline");var v=c.YesNoSnapshot();
            t.Check(v.Prompt is {Message:321,RequestId:0,Deadline:null}&&v.Prompt.Arguments.Length==0,"dead S219/321 queued in the shared slot "+accept,v.Prompt);
            t.Check(v.Prompt!.Text.StartsWith("有人要讓你復活",StringComparison.Ordinal),"321 fallback question text",v.Prompt.Text);
            var rez=c.ResurrectionSnapshot();
            t.Check(rez is {Phase:"invited",Message:321}&&!WorldSession.ResurrectionUsesPanel(rez)&&rez.Notice.Contains("Y／N"),"no module panel; notice points to the input line",rez);
            t.Check(Wait(c.AnswerYesNo(v,accept?"Y":"n"))&&sent.Count==1&&sent[0].SequenceEqual(ResurrectionPackets.Answer(321,accept)),"Y/N + Enter answers C121 321 "+accept);
            t.Check(c.YesNoSnapshot().Prompt==null&&c.ResurrectionSnapshot().Phase==(accept?"accepting":"rejected"),"answer clears the slot and records the phase "+accept);
            t.Check(!Wait(c.AnswerYesNo(v,"y"))&&sent.Count==1,"answer is single use "+accept);
        }
        {   // YES／NO button path (bool overload) is the same send.
            var sent=new List<byte[]>();using var c=Seed(sent,dead:true);c.World.Receive(Ask(321),"offline");
            t.Check(Wait(c.AnswerYesNo(c.YesNoSnapshot(),true))&&sent.Single().SequenceEqual(ResurrectionPackets.Answer(321,true)),"button accept sends the same C121");
        }
        {
            var sent=new List<byte[]>();using var c=Seed(sent);c.World.Receive(Ask(321),"offline");
            t.Check(c.YesNoSnapshot().Prompt==null&&sent.Count==0,"living role: 321 not queued");
        }
        {
            var sent=new List<byte[]>();using var c=Seed(sent,dead:true);c.World.Receive(Ask(321),"offline");c.World.Receive(Revive(),"offline");
            t.Check(c.YesNoSnapshot().Prompt==null&&sent.Count==0,"S85 ends the 321 question without sending");
        }
        {
            var sent=new List<byte[]>();using var c=Seed(sent,dead:true);c.World.Receive(Ask(321),"offline");var old=c.YesNoSnapshot();
            c.World.Receive(Ask(322),"offline");
            t.Check(c.YesNoSnapshot().Prompt?.Message==322&&!Wait(c.AnswerYesNo(old,"y"))&&sent.Count==0,"newer 322 replaces 321; the old view cannot answer");
            c.World.Receive(Ask(321),"offline");
            t.Check(c.YesNoSnapshot().Prompt?.Message==321&&c.ResurrectionSnapshot().Message==321,"newer 321 replaces 322");
            c.World.Receive(Ask(1000),"offline");
            t.Check(c.YesNoSnapshot().Prompt?.Message==1000&&c.ResurrectionSnapshot().Phase=="expired","other S219 expires 321");
        }
        {   // The module's own answer (still public) also clears the line.
            var sent=new List<byte[]>();using var c=Seed(sent,dead:true);c.World.Receive(Ask(321),"offline");
            t.Check(Wait(c.AnswerResurrection(c.ResurrectionSnapshot(),false))&&c.YesNoSnapshot().Prompt==null,"module answer clears the shared slot");
        }
    }

    // WO §6 / WIN2 §5.3: sub_7026C0 → sub_AC3A40(192, 581, 280, 12).
    static void LineLayout(WorldModuleTestContext t){
        t.Equal(new Rectangle(192,581,280,12),YesNoButtons.Line,"input line (192,581) 280×12");
        t.Check(!YesNoButtons.WinFormsLineVisible(true,true),"native panel up: WinForms prompt label hidden");
        t.Check(YesNoButtons.WinFormsLineVisible(true,false),"legacy HUD or faulted panel: label shown");
        t.Check(!YesNoButtons.WinFormsLineVisible(false,false)&&!YesNoButtons.WinFormsLineVisible(false,true),"no prompt: label hidden");
        string full=YesNoProtocol.Text(321,[]);
        t.Equal(YesNoProtocol.FallbackText(321,[]),YesNoProtocol.LineText(full),"line text drops the engine typing hint");
        t.Equal("plain",YesNoProtocol.LineText("plain"),"text without the hint unchanged");
    }

    sealed class Source:IYesNoSource {
        public YesNoPanelView? Current{get;set;}
        public List<(int Revision,bool Accept)> Answers{get;}=[];
        public void Answer(int revision,bool accept)=>Answers.Add((revision,accept));
    }
    static Point? FirstLit(UiCanvas canvas,Rectangle area){
        for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)if(canvas.Bitmap.GetPixel(x,y).A!=0)return new(x,y);
        return null;
    }
    static bool Lit(UiCanvas canvas,Rectangle area)=>FirstLit(canvas,area)!=null;
    static void Panel(WorldModuleTestContext t){
        var p=new YesNoPanel();var src=new Source();p.Source=src;var cmd=new UiNullCommands();
        t.Check(p.Bounds.Contains(YesNoButtons.Line)&&p.Bounds.Contains(YesNoPanel.ButtonBounds),"bounds (= paint clip) cover the input line and both buttons",p.Bounds);
        src.Current=new(3,"Revive? test question");p.Update(new UiStaticGameState(InWorld:true));
        using(var canvas=new UiCanvas()){
            canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            t.Check(Lit(canvas,YesNoButtons.Line),"question drawn inside the input line");
            // Inside the bounds but left of the buttons and above the line: nothing (the line clip holds the text).
            // 2 px margin to the YES button and to the line: GDI+ PixelOffsetMode.Half may touch the neighbouring pixel.
            var above=FirstLit(canvas,Rectangle.FromLTRB(192,567,359,579));
            t.Check(above==null,"nothing drawn above the line",above);
        }
        using(var canvas=new UiCanvas()){
            src.Current=new(4,"");p.Update(new UiStaticGameState(InWorld:true));
            canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            t.Check(!Lit(canvas,YesNoButtons.Line)&&Lit(canvas,YesNoButtons.Logical(true)),"empty question: buttons only");
        }
        src.Current=new(5,"Revive? test question");p.Update(new UiStaticGameState(InWorld:true));
        t.Check(!p.Mouse(new(UiMouseKind.Down,new(200,585),MouseButtons.Left,1,0,Keys.None),cmd)&&src.Answers.Count==0,"click on the input line passes through (chat／world)");
        t.Check(p.Mouse(new(UiMouseKind.Down,new(365,570),MouseButtons.Left,1,0,Keys.None),cmd)&&src.Answers.SequenceEqual([(5,true)]),"YES still answers");
    }

    // 0x4FBABC: immediate emote sets byte_153CE69; 0xA4B573: own action 66–69 end re-arms the 700 ms sub_A400C0 timer.
    static void EmoteSettle(WorldModuleTestContext t){
        t.Equal(700,TitleAndEmotePackets.SettleMs,"timer 0x2BC");
        {
            var sent=new List<byte[]>();var c=Seed(sent);long now=1000;c.ActionClock=()=>now;
            t.Check(Wait(c.SendEmote(c.CaptureTitleEmote(),1))&&sent.Count==1&&sent[0][1]==68,"class form immediate emote sent");
            t.Check(c.World.EmoteSettling&&!c.World.EmoteHeld,"byte_153CE69 set, byte_153CE6A not");
            t.Check(!c.World.EmoteInputPaused&&!c.World.MoveLocked,"while the emote plays the engine's own rule applies (not paused)");
            now=1500;c.World.EndEmote(Own);
            t.Check(c.World.EmoteInputPaused&&c.World.MoveLocked&&!c.World.ServerMoveLocked&&!c.MovementAvailable,"after the emote ends: input paused, not a server lock");
            now=2199;t.Check(c.World.EmoteSettling&&c.World.MoveLocked,"still set at 699 ms");
            now=2200;t.Check(!c.World.EmoteSettling&&!c.World.MoveLocked,"cleared 700 ms after the end");
        }
        {   // The timer only clears: a second emote started before it fires is cleared by the first timer (sub_A23B30 + sub_A231C0).
            var sent=new List<byte[]>();var c=Seed(sent);long now=1000;c.ActionClock=()=>now;var stamp=c.CaptureTitleEmote();
            Wait(c.SendEmote(stamp,1));now=1500;c.World.EndEmote(Own);
            now=2000;t.Check(Wait(c.SendEmote(stamp,2))&&sent.Count==2&&c.World.EmoteSettling,"second emote sets the flag again");
            now=2200;t.Check(!c.World.EmoteSettling,"first timer clears the flag of the second emote");
            now=2300;c.World.EndEmote(Own);t.Check(!c.World.EmoteInputPaused,"end of the second emote re-arms a timer but the flag is already 0");
        }
        {   // Lifetime prune (10 s) counts as the end.
            var sent=new List<byte[]>();var c=Seed(sent);long now=1000;c.ActionClock=()=>now;Wait(c.SendEmote(c.CaptureTitleEmote(),1));
            now=11000;c.World.ActiveEmotes(now);
            t.Check(c.World.ActiveEmotes(now).Length==0&&c.World.EmoteInputPaused,"pruned emote starts the 700 ms tail");
            now=11700;t.Check(!c.World.EmoteSettling,"tail over");
        }
        {   // Walking interrupts the emote (engine only): the flag is dropped, no pause mid-walk.
            var sent=new List<byte[]>();var c=Seed(sent);long now=1000;c.ActionClock=()=>now;Wait(c.SendEmote(c.CaptureTitleEmote(),1));
            c.World.InterruptEmote(Own);t.Check(!c.World.EmoteSettling&&!c.World.MoveLocked,"walking interrupt drops byte_153CE69");
        }
        {   // 推定: no local entry → the timer starts at once instead of leaving the flag set forever.
            var sent=new List<byte[]>();var c=Seed(sent);long now=5000;c.ActionClock=()=>now;
            c.World.StartEmoteSettle();t.Check(c.World.EmoteInputPaused,"flag without a playing emote pauses at once");
            now=5700;t.Check(!c.World.EmoteSettling,"and clears after 700 ms");
        }
        {   // 0x4FBADA: the hold path clears byte_153CE69 and sets byte_153CE6A instead.
            var sent=new List<byte[]>();var c=Seed(sent);long now=1000;c.ActionClock=()=>now;
            Wait(c.SendEmote(c.CaptureTitleEmote(),1));now=1500;c.World.EndEmote(Own);t.Check(c.World.EmoteSettling,"class emote flag set");
            SetActor(c.World,c.World.Actor! with{Gfx=6080});now=1600;
            t.Check(Wait(c.SendEmote(c.CaptureTitleEmote(),1))&&TitleAndEmotePackets.Plan(6080,1,true).Hold,"hold emote sent");
            t.Check(!c.World.EmoteSettling&&c.World.EmoteHeld&&c.World.EmoteInputPaused,"hold: 153CE69 cleared, 153CE6A set");
        }
        {   // Throttled emotes (sub_4FAAF0) never touch byte_153CE69.
            var sent=new List<byte[]>();var c=Seed(sent,gfx:3225);long now=1000;c.ActionClock=()=>now;
            t.Check(Wait(c.SendEmote(c.CaptureTitleEmote(),1))&&!c.World.EmoteSettling&&c.World.EmoteHeld,"throttled: hold only");
        }
        {   // A new world (0x8C47CD clears both flags) drops it.
            var sent=new List<byte[]>();var c=Seed(sent);long now=1000;c.ActionClock=()=>now;Wait(c.SendEmote(c.CaptureTitleEmote(),1));
            c.World.EndEmote(Own);c.World.Reset();t.Check(!c.World.EmoteSettling&&!c.World.EmoteInputPaused,"Reset drops byte_153CE69");
        }
    }

    // sub_9343C0: type 8 → +547＝1 then handled as type 0; type 0 → +547＝0. sub_4FAAF0: +547 → 2500×29>>5 = 2265 ms.
    static void ThirdSpeed(WorldModuleTestContext t){
        {
            var sent=new List<byte[]>();var c=Seed(sent,gfx:3225);long now=10000;c.ActionClock=()=>now;
            t.Check(!c.World.OwnThirdSpeed&&c.EmoteThrottleMs()==2500,"no +547: 2500 ms");
            int before=Unhandled(c.World,103);
            c.World.Receive(Liquor(Own,8),"offline");
            t.Check(c.World.OwnThirdSpeed&&c.EmoteThrottleMs()==2265&&Unhandled(c.World,103)==before,"own type 8: +547 kept, 2265 ms, handled");
            t.Equal(0,c.World.LiquorLevel,"type 8 is handled as type 0 (no drunkenness)");
            var stamp=c.CaptureTitleEmote();
            t.Check(Wait(c.SendEmote(stamp,1))&&sent.Count==1,"first throttled emote");
            now=12265;t.Check(!Wait(c.SendEmote(stamp,2))&&sent.Count==1,"2265 ms: not sent (jbe)");
            now=12266;t.Check(Wait(c.SendEmote(stamp,2))&&sent.Count==2,"2266 ms: sent");
            SetActor(c.World,c.World.Actor! with{Speed=1});t.Equal(1698,c.EmoteThrottleMs(),"+547 with haste: 2265×3/4");
            typeof(WorldSession).GetMethod("ClearObjects",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(c.World,null);
            t.Check(c.World.OwnThirdSpeed,"map change keeps +547 (the server does not resend type 8 after a teleport)");
            c.World.Receive(Liquor(Other,8),"offline");
            t.Check(c.World.OwnThirdSpeed&&Unhandled(c.World,103)==before+1,"other object's type 8 stays unhandled");
            c.World.Receive(Liquor(Other,0),"offline");t.Check(c.World.OwnThirdSpeed,"other object's type 0 keeps own +547");
            c.World.Receive(Liquor(Own,0),"offline");t.Check(!c.World.OwnThirdSpeed,"own type 0 clears +547");
            c.World.Receive(Liquor(Own,8),"offline");c.World.Reset();c.World.SeedInputLineTest(false,3225,4);
            t.Check(!c.World.OwnThirdSpeed,"Reset (new session) drops +547");
        }
        {   // Handled as 0: on map 67 the drunkenness floor is 1 (byte_13CE5E8).
            var sent=new List<byte[]>();var c=Seed(sent,map:67);
            c.World.Receive(Liquor(Own,8),"offline");
            t.Check(c.World.OwnThirdSpeed&&c.World.LiquorLevel==TimedBuffRules.MinimumLiquorLevel,"type 8 on map 67: level 1 like type 0");
        }
        {
            var sent=new List<byte[]>();var c=Seed(sent);c.World.Receive(Liquor(Own,2),"offline");c.World.Receive(Liquor(Own,8),"offline");
            t.Check(c.World.LiquorLevel==0&&c.World.OwnThirdSpeed,"type 8 ends own drunkenness (handled as 0)");
        }
    }

    // Records the current scope: the engine still routes the hold through MoveLocked → MovementAvailable (see issue-804 report).
    static void HoldScope(WorldModuleTestContext t){
        var sent=new List<byte[]>();var c=Seed(sent,gfx:6080);long now=1000;c.ActionClock=()=>now;
        Wait(c.SendEmote(c.CaptureTitleEmote(),1));
        t.Check(c.World.EmoteHeld&&c.World.MoveLocked&&!c.World.ServerMoveLocked,"hold still reaches MoveLocked (narrowing needs files outside R7-B)");
        now=2234;t.Check(!c.World.EmoteInputPaused&&!c.World.MoveLocked,"released after 1234 ms");
    }
}
