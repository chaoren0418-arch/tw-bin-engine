using System.Reflection;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R2-D（T254／#615）離線測試：exitghost、S85 幽靈略過外觀、QUIT_WINDOW 規則與狀態、UserPlayStatsUI／QUIT_WINDOW 原生面板。
// 合成封包、合成 XML／圖／字串表、假送出閘門與假時鐘；不開遊戲、網路、視窗，也不讀客戶端素材。
static class UserPlayStatsTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Own=123;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    // Own S87 (same field order as ghost-mode-tests GhostPack); flags 0x80 = ghost.
    static byte[] Self(byte flags,ushort gfx=0)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(Own);b.Write(gfx);
        b.Write((byte)0);b.Write((byte)2);b.Write((byte)7);b.Write((byte)1);b.Write(0x12345678u);b.Write((short)-123);
        Text(b,"offline");Text(b,"");b.Write(flags);b.Write(0u);Text(b,"");Text(b,"");
        b.Write((byte)0xb0);b.Write((byte)255);b.Write((byte)8);b.Write((byte)0);b.Write((byte)0);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] Death(uint id)=>LoginPackets.Pad([158,..BitConverter.GetBytes(id),8]);
    static byte[] Revive(uint id,uint gfx)=>LoginPackets.Pad([85,..BitConverter.GetBytes(id),0,0,0,0,0,..BitConverter.GetBytes(gfx)]);
    static LoginConnection Seed(byte flags,List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)4);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":10,\"MaxMp\":10}"));
        c.World.Receive(Self(flags),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static ServerStringTable Table(int count,params (int id,string text)[] rows){
        var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(count+"\r\n"+string.Join("\r\n",lines)));
    }

    [WorldModuleTest("user-play-stats")]
    static void Run(WorldModuleTestContext t){
        Registration(t);Ghost(t);Rules(t);QuitState(t);QuitPanel(t);StatsPanel(t);
    }

    static void Registration(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id==QuitWindowPanel.PanelId&&p.Layout=="uiextra86")==1,"QUIT_WINDOW registered once on uiextra86");
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id=="UserPlayStatsUI"&&p.Layout=="UserPlayUI")==1,"UserPlayStatsUI registered once on UserPlayUI");
        t.Check(UiPanelRegistry.Panels.Select(p=>p.Id).Distinct().Count()==UiPanelRegistry.Panels.Count,"registry ids stay unique");
        t.Check(typeof(ClientForm).GetMethod("HyperAction",F)!=null,"ClientForm HyperAction entry for R2-F");
    }

    static void Ghost(WorldModuleTestContext t){
        t.Check(GhostModePackets.IsExitAction("exitghost")&&GhostModePackets.IsExitAction(" exitghost ")&&!GhostModePackets.IsExitAction("ExitGhost")&&!GhostModePackets.IsExitAction(null),"exitghost matched by its registered name");
        var sent=new List<byte[]>();
        using(var c=Seed(0x04,sent)){
            t.Check(!c.World.IsGhost,"seed: normal actor");
            t.Check(!c.TryHyperAction("npcNameView",out _),"other HyperText actions are not handled here");
            t.Check(c.TryHyperAction("exitghost",out var none)&&!none.GetAwaiter().GetResult()&&sent.Count==0,"exitghost while not a ghost: handled, nothing sent");
            // S85 outside ghost mode: own appearance changes.
            c.World.Receive(Death(Own),"offline");t.Check(c.World.OwnDead,"S158 action 8 → own death");
            int before=c.World.OwnResurrections;c.World.Receive(Revive(Own,61),"offline");
            t.Check(!c.World.OwnDead&&c.World.Actor!.Gfx==61&&c.World.OwnResurrections==before+1,"S85 outside ghost applies own gfx");
        }
        sent.Clear();
        using(var c=Seed(0x84,sent)){
            t.Check(c.World.IsGhost&&c.World.Ready,"seed: ghost actor, world ready");
            t.Check(c.TryHyperAction("exitghost",out var sending)&&sending.GetAwaiter().GetResult(),"exitghost in ghost mode sends");
            t.Equal(1,sent.Count,"exactly one packet");
            t.Equal(Convert.ToHexString(GhostModePackets.Exit()),Convert.ToHexString(sent[0]),"exitghost sends C173 without fields");
            t.Check(c.TryHyperAction("exitghost",out var again)&&!again.GetAwaiter().GetResult()&&sent.Count==1,"repeat while waiting sends nothing");
            // S85 in ghost mode: death flag clears, own appearance change skipped (WO§4 sub_A43B50).
            c.World.Receive(Death(Own),"offline");ushort gfx=c.World.Actor!.Gfx;
            c.World.Receive(Revive(Own,61),"offline");
            t.Check(!c.World.OwnDead,"S85 in ghost mode still clears own death");
            t.Equal(gfx,c.World.Actor!.Gfx,"S85 in ghost mode keeps own gfx (GhostSkipsSelfAppearance)");
        }
    }

    static void Rules(WorldModuleTestContext t){
        t.Check(QuitWindowRules.CombatAllows(true,false,1000,1001),"dead always allowed (death pushes the combat stamp back 15 s)");
        t.Check(QuitWindowRules.CombatAllows(false,true,1000,1001),"safety zone allowed");
        t.Check(!QuitWindowRules.CombatAllows(false,false,1000,15999)&&QuitWindowRules.CombatAllows(false,false,1000,16000),"alive: 15,000 ms after last combat");
        t.Check(QuitWindowRules.CombatAllows(false,false,null,0),"unknown last combat → server decides");
        t.Equal(QuitWindowRules.RestartKind.DeadRestart,QuitWindowRules.Restart(true,true,false),"dead restart → C177 path");
        t.Equal(QuitWindowRules.RestartKind.CharacterSelect,QuitWindowRules.Restart(true,false,true),"alive restart → C7 path");
        t.Equal(QuitWindowRules.RestartKind.CombatBlocked,QuitWindowRules.Restart(true,false,false),"alive in combat → message 130");
        t.Equal(QuitWindowRules.RestartKind.Offline,QuitWindowRules.Restart(false,true,true),"after the timeout disconnect → offline");
        t.Equal(QuitWindowRules.QuitKind.Quit,QuitWindowRules.Quit(true,true),"quit in world → C122");
        t.Equal(QuitWindowRules.QuitKind.CombatBlocked,QuitWindowRules.Quit(true,false),"quit in combat → message 131");
        t.Equal(QuitWindowRules.QuitKind.Exit,QuitWindowRules.Quit(false,true),"quit when disconnected → exit");
        using(ServerMessageText.UseTable(null)){
            t.Equal("斷線。",QuitWindowRules.Message(47),"message 47 fallback (PBW§4)");
            t.Equal("戰鬥中，無法重新開始。",QuitWindowRules.Message(130),"message 130 fallback");
            t.Equal("戰鬥中無法離開遊戲。",QuitWindowRules.Message(131),"message 131 fallback");
            t.Equal(("經驗",false),UserPlayStatsText.Label(3123,"row"),"3123 without table → WO§6 name");
            t.Equal(("#3999",false),UserPlayStatsText.Label(3999,"row"),"unknown id without table → #id");
            t.Equal(("row",false),UserPlayStatsText.Label(null,"row"),"no string id → row name");
        }
        using(ServerMessageText.UseTable(Table(3200,(47,"\\f1表內斷線"),(3123,"表內經驗"),(3124,"  ")))){
            t.Equal("表內斷線",QuitWindowRules.Message(47),"message 47 read from the string table (colour control removed)");
            t.Equal(("表內經驗",true),UserPlayStatsText.Label(3123,"row"),"3123 read from the string table");
            t.Equal(("擊殺數",false),UserPlayStatsText.Label(3124,"row"),"blank table entry → fallback name");
        }
        using(ServerMessageText.UseTable(Table(100,(47,"短表")))){
            t.Equal(("金幣",false),UserPlayStatsText.Label(3125,"row"),"id past the table count → fallback name");
        }
    }

    static void QuitState(WorldModuleTestContext t){
        long now=0;var sent=new List<byte[]>();
        Task<bool> Sender(byte[] b,Func<bool> valid,Action admit){if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);}
        using(ServerMessageText.UseTable(null))
        using(var c=new LoginConnection()){
            t.Check(c.QuitWindowSnapshot()==null,"closed initially");
            c.SeedRecoveryTestWorld(Own,4,Sender);c.World.DeathClock=()=>now;
            c.World.Receive(Death(Own),"offline");
            t.Check(c.DeathSnapshot().WindowOpen&&c.QuitWindowSnapshot()==null,"death opens UserPlayStatsUI, not QUIT_WINDOW");
            now=501264;
            t.Check(c.CheckDeathTimeout().GetAwaiter().GetResult(),"timeout sends C122");
            t.Equal("7A00000000000000",Convert.ToHexString(sent.Single()),"one C122");
            var v=c.QuitWindowSnapshot();
            t.Check(v is {InWorld:false,Dead:false,Message:"斷線。"},"timeout opens QUIT_WINDOW with message 47, connection closed",v);
            t.Check(c.State==LoginState.Disconnected&&c.Detail.StartsWith("斷線。",StringComparison.Ordinal),"status shows message 47",c.Detail);
            t.Equal(v,c.QuitWindowSnapshot(),"window stays across polls");
            c.CloseQuitWindow();t.Check(c.QuitWindowSnapshot()==null,"cancel closes QUIT_WINDOW");
            c.OpenQuitWindow("x",false);int revision=c.QuitWindowSnapshot()!.Revision;t.Check(revision>v!.Revision,"each open has a new revision");
            c.Cancel();t.Check(c.QuitWindowSnapshot()==null,"a new session generation hides the window");
        }
        sent.Clear();
        using(var c=new LoginConnection()){
            c.SeedRecoveryTestWorld(Own,4,Sender);
            c.OpenQuitWindow("in world",true);
            t.Check(c.QuitWindowSnapshot() is {InWorld:true,Dead:false},"in-world view (ESC entry is unverified; state only)");
            c.World.Receive(Death(Own),"offline");t.Check(c.QuitWindowSnapshot() is {Dead:true},"dead flag follows the world");
            c.World.Receive(Revive(Own,61),"offline");t.Check(c.QuitWindowSnapshot()==null,"own S85 closes an open QUIT_WINDOW (sub_9496B0)");
            c.OpenQuitWindow("in world",true);
            t.Check(c.QuitGame().GetAwaiter().GetResult(),"QuitGame sends");
            t.Equal("7A00000000000000",Convert.ToHexString(sent.Single()),"QuitGame sends C122 once");
            t.Check(c.State==LoginState.Disconnected&&c.QuitWindowSnapshot()==null,"QuitGame closes the connection and the window");
            t.Check(!c.QuitGame().GetAwaiter().GetResult()&&sent.Count==1,"no second C122 when offline");
        }
    }

    // ---- 合成圖 ----
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static UiMouseEvent M(UiMouseKind kind,int x,int y)=>new(kind,new(x,y),kind==UiMouseKind.Move?MouseButtons.None:MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,int x,int y,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,x,y),cmd);p.Mouse(M(UiMouseKind.Up,x,y),cmd);}

    // Same element kinds and names as uiextra86 QUIT_WINDOW, deliberately different numbers.
    const string QuitXml="""
<root>
<QUIT_WINDOW x="0" y="0">
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_QUIT_BASE" surfNumber="21" xPos="2" yPos="3"/>
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_QUIT_FRAME" surfNumber="22"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_QUIT_X" surfNumber="30" xPos="90" yPos="5"/>
	<STAGE3_BTN surfName="SURF_RENEWAL_BTN_QUIT_RESTART" surfNumber="40" xRPos="10" yRPos="20"/>
	<STAGE3_BTN surfName="SURF_RENEWAL_BTN_QUIT_QUIT" surfNumber="50" xRPos="10" yRPos="40"/>
	<STAGE3_BTN surfName="SURF_RENEWAL_BTN_QUIT_CANCEL" surfNumber="60" xRPos="10" yRPos="60"/>
</QUIT_WINDOW>
<NEW_QUIT_WINDOW x="0" y="0"/>
</root>
""";
    static Func<int,UiSurface?> QuitSurfaces(bool missingHighlight=false)=>id=>id switch{
        21=>Surf(21,100,90,0xFF203040),22=>Surf(22,120,120,0xFF999999),30=>Surf(30,8,8,0xFF010101),
        40 or 50 or 60=>Surf(id,60,16,0xFF404040),41 or 51 or 61=>Surf(id,60,16,0xFF505050),
        42 or 52 or 62=>missingHighlight?null:Surf(id,60,16,0xFF606060),_=>null};
    sealed class FakeQuit:IQuitWindowSource {
        public QuitWindowView? Current{get;set;}
        public List<string> Calls{get;}=[];
        public void Restart()=>Calls.Add("restart");
        public void Quit()=>Calls.Add("quit");
        public void Close(){Calls.Add("close");}
    }

    static void QuitPanel(WorldModuleTestContext t){
        var layout=UiLayout.Parse(QuitXml,"synthetic-uiextra86.xml");
        var p=new QuitWindowPanel();p.Load(layout,QuitSurfaces());
        // Drawn union (frame has no coordinates → not drawn): x 2..102, y 3..93 → width = right edge 102.
        t.Equal(new Point(400-51,150),p.Origin,"origin (400 − width/2, 150) (PBW§4)");
        t.Equal(new Rectangle(349+2,150+3,100,90),p.Bounds,"bounds = drawn elements");
        t.Equal(new Point(10,20),p.Restart!.Offset,"STAGE3_BTN uses xRPos/yRPos");
        t.Check(p.Restart.Push?.Surf==41&&p.Restart.Highlight?.Surf==42,"STAGE3 states n+1 push, n+2 highlight");
        t.Check(!p.Visible,"hidden without source");
        t.Throws<UiLayoutException>(()=>new QuitWindowPanel().Load(UiLayout.Parse(QuitXml.Replace("SURF_RENEWAL_BTN_QUIT_QUIT","X"),"a.xml"),QuitSurfaces()),"missing quit button throws");
        t.Throws<UiLayoutException>(()=>new QuitWindowPanel().Load(UiLayout.Parse(QuitXml.Replace(" xRPos=\"10\" yRPos=\"60\"",""),"b.xml"),QuitSurfaces()),"button without coordinates throws");
        t.Throws<UiLayoutException>(()=>new QuitWindowPanel().Load(UiLayout.Parse("<root/>","c.xml"),QuitSurfaces()),"missing QUIT_WINDOW throws");

        var src=new FakeQuit();p.Source=src;var cmd=new UiNullCommands();
        src.Current=new(1,false,false,"斷線。");
        p.Update(new UiStaticGameState(InWorld:false));
        t.Check(p.Visible,"visible while disconnected (death-timeout window)");
        int rx=349+10+5,ry=150+20+5;
        p.Mouse(M(UiMouseKind.Move,rx,ry),cmd);t.Equal(42,p.StateSurface(p.Restart!)!.Surf,"hover → highlight image");
        p.Mouse(M(UiMouseKind.Down,rx,ry),cmd);t.Equal(41,p.StateSurface(p.Restart!)!.Surf,"pressed → push image");
        p.Mouse(M(UiMouseKind.Up,rx+200,ry),cmd);t.Check(src.Calls.Count==0,"release outside the button does nothing");
        t.Equal(40,p.StateSurface(p.Restart!)!.Surf,"released → base image");
        Click(p,rx,ry,cmd);t.Equal("restart",src.Calls.Last(),"restart click");
        Click(p,349+10+5,150+40+5,cmd);t.Equal("quit",src.Calls.Last(),"quit click");
        t.Check(p.Visible,"restart/quit leave closing to the source");
        Click(p,349+10+5,150+60+5,cmd);t.Equal("close",src.Calls.Last(),"cancel click closes");
        t.Check(!p.Visible,"hidden after cancel");
        p.Update(new UiStaticGameState());t.Check(!p.Visible,"same revision stays closed");
        src.Current=new(2,false,false,"斷線。");p.Update(new UiStaticGameState());t.Check(p.Visible,"new revision reopens");
        Click(p,349+90+2,150+5+2,cmd);t.Check(src.Calls.Last()=="close"&&!p.Visible,"close X closes");
        src.Current=new(3,false,false,"");p.Update(new UiStaticGameState());
        t.Check(p.Key(new(Keys.Escape,true,false),cmd)&&!p.Visible,"Esc closes");
        t.Check(!p.Key(new(Keys.Escape,true,false),cmd)&&!p.Mouse(M(UiMouseKind.Down,rx,ry),cmd),"hidden panel ignores input");
        src.Current=null;p.Update(new UiStaticGameState());t.Check(!p.Visible,"no view → hidden");

        var m=new QuitWindowPanel();m.Load(layout,QuitSurfaces(missingHighlight:true));m.Source=src;src.Current=new(9,false,false,"");m.Update(new UiStaticGameState());
        m.Mouse(M(UiMouseKind.Move,rx,ry),cmd);t.Equal(40,m.StateSurface(m.Restart!)!.Surf,"missing highlight → base image");
        using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(m.Bounds);m.Paint(canvas);canvas.Release();
        var px=canvas.Bitmap.GetPixel(349+2+1,150+3+1);
        t.Check(px.R==0x20&&px.G==0x30&&px.B==0x40,"base painted at origin + offset",px);
        t.Equal(Color.Black.ToArgb(),canvas.Bitmap.GetPixel(349,150).ToArgb(),"frame without coordinates not painted");
    }

    // Same schema as UserPlayUI.xml, deliberately different numbers.
    const string StatsXml="""
<?xml version="1.0"?>
<UserPlayStatsUI Toggle="true">
  <Window Name="UserPlayStats_Window" X="0" Y="0" BaseSurf="9001;1" FrameSurf="9002;1" Moveable="20">
    <Button Name="CloseWindow_Button" X="180" Y="5" BaseSurf="9010" PushSurf="9009" MouseEvent="CloseWindow"/>
    <Button Name="Restart_Button" X="50" Y="30" BaseSurf="9011" HighlightSurf="9013" PushSurf="9012;1" MouseEvent="Restart"/>
    <Button Name="Quit_Button" X="50" Y="50" BaseSurf="9014" HighlightSurf="9016" PushSurf="9015;1" MouseEvent="Quit"/>
    <Button Name="Cancel_Button" X="50" Y="70" BaseSurf="9017" MouseEvent="Cancel"/>
    <List Name="UserPlayStats_List" X="20" Y="100" Width="140" Height="40" Vertical="1" Gap="0">
      <List Name="RowA" Width="140" Height="14" Horizontal="1" Gap="3">
        <Image Name="Icon_Image" Surf="9020;1" Y="2"/>
        <Label Name="A_Label" Width="70" Height="12" String="3123" FontColor="1,2,3"/>
        <Label Name="A_Value" Width="60" Height="12" Caption="0" FontColor="4,5,6"/>
      </List>
      <List Name="RowB" Width="140" Height="14" Horizontal="1">
        <Label Name="B_Label" Width="71" Height="12" String="3999"/>
        <Label Name="B_Value" Width="61" Height="12" Caption="0"/>
      </List>
    </List>
  </Window>
</UserPlayStatsUI>
""";
    static Func<int,UiSurface?> StatsSurfaces=>id=>id switch{
        9001=>Surf(9001,200,160,0xFF102030),9002=>Surf(9002,200,160,0x00000000),9010=>Surf(9010,10,10,0xFF111111),
        9011 or 9014 or 9017=>Surf(id,80,16,0xFF404040),9012 or 9015=>Surf(id,80,16,0xFF505050),9013 or 9016=>Surf(id,80,16,0xFF606060),
        9020=>Surf(9020,6,6,0xFFAA0000),_=>null};
    sealed class FakeStats:IUserPlayStatsSource {
        public UserPlayStatsView? Current{get;set;}
        public List<string> Calls{get;}=[];
        public void Restart()=>Calls.Add("restart");
        public void Quit()=>Calls.Add("quit");
        public void Dismiss()=>Calls.Add("dismiss");
    }

    static void StatsPanel(WorldModuleTestContext t){
        var p=new UserPlayStatsPanel();
        using(ServerMessageText.UseTable(Table(3200,(3123,"表內經驗"))))p.Load(UiLayout.Parse(StatsXml,"UserPlayUI.xml"),StatsSurfaces);
        t.Equal("UserPlayStatsUI",p.Id,"panel id = XML root name");
        t.Equal(new Point(300,150),p.Origin,"origin centred (400 − 200/2), y 150 (inferred)");
        t.Equal(new Rectangle(300,150,200,160),p.Bounds,"bounds from base/frame surfaces");
        t.Check(p.FrameSurface?.Surf==9002,"frame surf 5323-equivalent loaded (FrameSurf)");
        t.Equal("表內經驗|#3999",string.Join("|",p.Rows.Select(r=>r.Label)),"row labels from the string table; unknown id → #id");
        t.Check(p.Rows[0].LabelFromTable&&!p.Rows[1].LabelFromTable,"label source recorded");
        var (icon,label,value)=p.RowRects(0);
        t.Equal(new Point(320,252),icon,"row icon at list X/Y + icon Y");
        t.Equal(new Rectangle(320+6+3,250,70,14),label,"label after icon + gap");
        t.Equal(new Rectangle(329+70+3,250,60,14),value,"value after label + gap");
        t.Equal(new Rectangle(320,264,71,14),p.RowRects(1).Label,"second row one row height lower, no icon");
        t.Check(!p.Visible,"hidden without source");
        t.Throws<UiLayoutException>(()=>new UserPlayStatsPanel().Load(UiLayout.Parse(StatsXml.Replace("Cancel_Button","X"),"x.xml"),StatsSurfaces),"missing cancel button throws");
        t.Throws<UiLayoutException>(()=>new UserPlayStatsPanel().Load(UiLayout.Parse(StatsXml.Replace(" X=\"0\" Y=\"0\"",""),"x.xml"),StatsSurfaces),"window without X/Y throws");

        var src=new FakeStats();p.Source=src;var cmd=new UiNullCommands();
        src.Current=new(true);p.Update(new UiStaticGameState(InWorld:true));t.Check(p.Visible,"visible while the death window is open");
        int rx=300+50+5,ry=150+30+5;
        p.Mouse(M(UiMouseKind.Move,rx,ry),cmd);t.Equal(9013,p.StateSurface(p.Restart!)!.Surf,"hover → HighlightSurf");
        p.Mouse(M(UiMouseKind.Down,rx,ry),cmd);t.Equal(9012,p.StateSurface(p.Restart!)!.Surf,"pressed → PushSurf");
        p.Mouse(M(UiMouseKind.Up,rx,ry),cmd);t.Equal("restart",src.Calls.Last(),"restart fires on release");
        src.Current=new(false);p.Update(new UiStaticGameState(InWorld:true));int calls=src.Calls.Count;
        Click(p,rx,ry,cmd);t.Equal(calls,src.Calls.Count,"restart unavailable → nothing fires");
        p.Mouse(M(UiMouseKind.Move,rx,ry),cmd);t.Equal(9011,p.StateSurface(p.Restart!)!.Surf,"unavailable restart keeps BaseSurf");
        Click(p,300+50+5,150+50+5,cmd);t.Equal("quit",src.Calls.Last(),"quit fires");
        Click(p,300+50+5,150+70+5,cmd);t.Equal("dismiss",src.Calls.Last(),"cancel dismisses");
        Click(p,300+180+2,150+5+2,cmd);t.Equal("dismiss",src.Calls.Last(),"close X dismisses");
        p.Mouse(M(UiMouseKind.Move,300+50+5,150+70+5),cmd);t.Equal(9017,p.StateSurface(p.Cancel!)!.Surf,"no HighlightSurf → BaseSurf");
        t.Check(p.Key(new(Keys.Escape,true,false),cmd)&&src.Calls.Last()=="dismiss","Esc dismisses");
        t.Check(p.IsDragHandle(new(300+5,150+5))&&!p.IsDragHandle(new(300+5,150+25))&&!p.IsDragHandle(new(300+182,150+7)),"Moveable=20: top strip drags, buttons do not");
        p.MoveTo(new(10,20));t.Equal(new Rectangle(10,20,200,160),p.Bounds,"MoveTo moves bounds");

        using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
        var px=canvas.Bitmap.GetPixel(12,22);t.Check(px.R==0x10&&px.G==0x20&&px.B==0x30,"base painted at the window origin",px);
        var ic=canvas.Bitmap.GetPixel(10+20+1,20+100+2+1);t.Check(ic.R==0xAA&&ic.G==0&&ic.B==0,"row icon painted",ic);
        src.Current=null;p.Update(new UiStaticGameState(InWorld:true));t.Check(!p.Visible&&!p.Mouse(M(UiMouseKind.Down,15,25),cmd),"closed → hidden, ignores input");
    }
}
