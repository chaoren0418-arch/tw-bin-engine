using P06.NativeUi;
namespace P06;

// T352 (#815, R7-M) login-followup 離線測試：LoginUI 的 Notice_Window（S48 公告）與 Exit 鈕。只用合成版面、合成單色圖與不連網的
// 假 ILoginSession；不讀主客戶端或參考客戶端、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class LoginFollowupTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    // Same element structure as the reference LoginUI.xml Notice_Window (own coordinates and image numbers).
    const string NoticeXml="""
          <Window Name = "Notice_Window" X = "200" Y = "100" Modal = "1" Activate = "0" BaseSurf = "21;3">
            <MultiLabel Name = "Message_Label" X = "10" Y = "10" FontSize = "14" Width = "150" Height = "60" VAlign = "top"/>
            <ScrollBar Name = "Notice_Scrollbar" X = "170" Y = "20" Width = "10" Height = "50" ScrollStart = "0" ScrollHeight = "60" Vertical = "1" MaxValue = "1" MouseEvent = "ScrollWheel_NoticeWin">
              <Button Name = "Up_Button" X = "0" Y = "-9" BaseSurf = "26;1" PushSurf = "27;1" MouseEvent = "ScrollUp_NoticeWin"/>
              <Button Name = "Down_Button" X = "0" Y = "60" BaseSurf = "28;1" PushSurf = "29;1" MouseEvent = "ScrollDown_NoticeWin"/>
              <Button Name = "Move_Button" X = "0" Y = "12" BaseSurf = "30;1" PushSurf = "31;1" MouseEvent = "ScrollMove_NoticeWin"/>
            </ScrollBar>
            <Button Name = "Skip_Button" X = "20" Y = "120" BaseSurf = "24;4" HighlightSurf = "25;4" PushSurf = "25;4" MouseEvent = "Skip_NoticeWin"/>
            <Button Name = "Ok_Button" X = "120" Y = "120" BaseSurf = "22;4" HighlightSurf = "23;4" PushSurf = "23;4" MouseEvent = "Ok_NoticeWin"/>
            <Image Name = "Frame_Image" X = "0" Y = "0" Surf = "20;1"/>
            <Image Name = "Back_Image" X = "10" Y = "10" Surf = "18;1"/>
          </Window>
        """;
    static string Xml(string notice=NoticeXml)=>$"""
        <?xml version="1.0"?>
        <LoginUI>
        {notice}
          <Window Name = "Login_Window" X = "0" Y = "0" Activate = "0">
            <InputBox Name = "ID_Input" X = "300" Y = "300" Width = "100" Height = "20" English = "1" LimitText = "5" FontSize = "14"/>
            <InputBox Name = "Password_Input" X = "300" Y = "330" Width = "100" Height = "30" Password = "1" LimitText = "4" FontSize = "14"/>
            <Button Name = "Login_Button" X = "300" Y = "370" BaseSurf = "10;1" MouseEvent = "Login_LoginWin"/>
            <Button Name = "Exit_Button" X = "700" Y = "560" BaseSurf = "17;4" HighlightSurf = "18;4" PushSurf = "18;4" MouseEvent = "Exit_LoginWin"/>
            <Button Name = "Credit_Button" X = "700" Y = "520" BaseSurf = "17;4" MouseEvent = "Credit_LoginWin"/>
            <Image Name = "Back_Image" X = "0" Y = "0"/>
          </Window>
        </LoginUI>
        """;

    const uint Base=0xFF404040,Frame=0xFF806000,Back=0xFF102030,Ok=0xFF0000AA,Skip=0xFF00AA00,Up=0xFFAA00AA,Down=0xFF00AAAA,Exit=0xFF990000;
    static readonly Dictionary<int,UiSurface> Surfs=new(){
        [10]=Surface(10,40,20,0xFF00AA00),[17]=Surface(17,60,20,Exit),[18]=Surface(18,150,60,Back),
        [20]=Surface(20,200,5,Frame),[21]=Surface(21,200,150,Base),[22]=Surface(22,40,15,Ok),[23]=Surface(23,40,15,0xFF0000FF),
        [24]=Surface(24,40,15,Skip),[25]=Surface(25,40,15,0xFF00FF00),[26]=Surface(26,10,8,Up),[27]=Surface(27,10,8,0xFFFF00FF),
        [28]=Surface(28,10,8,Down),[29]=Surface(29,10,8,0xFF00FFFF),[30]=Surface(30,10,10,0xFFFFFFFF),[31]=Surface(31,10,10,0xFFFFFFFF)};

    sealed class FakeSession:ILoginSession {
        public LoginState State{get;set;}=LoginState.AwaitList;
        public int Revision{get;set;}=3;
        public int SessionGeneration{get;set;}=7;
        public IReadOnlyList<LoginCharacter> Characters=>[];
        public int Runs;
        public Task Run(int port,string profileId,string account,string password,int timeoutMs=5000,CancellationToken token=default){Runs++;State=LoginState.Connecting;return new TaskCompletionSource().Task;}
        public Task SelectCharacter(string name)=>Task.CompletedTask;
        public void Cancel(bool offline=false)=>State=offline?LoginState.Offline:LoginState.Cancelled;
    }
    sealed class Rig {
        public readonly FakeSession Session=new();public readonly LoginControl Control;public readonly LoginPanel Panel;public int Confirms;
        public Rig(string? xml=null,Func<int,UiSurface?>? surf=null){
            Control=new LoginControl(Session,()=>{},()=>{});
            Panel=new LoginPanel(null,()=>Control){CapsLock=()=>false};
            Panel.Load(UiLayout.Parse(xml??Xml(),"LoginUI.xml"),surf??(id=>Surfs.GetValueOrDefault(id)));
        }
        public void Update()=>Panel.Update(Front);
        public bool Show(string text="公告"){bool ok=Panel.ShowNotice(text,()=>Confirms++);Update();return ok;}
    }
    sealed class ExitCommands:IUiCommands,IUiLoginCommands {
        public int Exits;public readonly List<string> Calls=[];
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools");
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public void ExitClient()=>Exits++;
    }
    static readonly UiStaticGameState Front=new(InWorld:false);
    static UiMouseEvent M(UiMouseKind k,int x,int y,MouseButtons b=MouseButtons.Left,int delta=0)=>new(k,new Point(x,y),b,1,delta,Keys.None);
    static void Click(LoginPanel p,int x,int y,IUiCommands? c=null){c??=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
    static bool Key(LoginPanel p,Keys k){var c=new UiNullCommands();bool used=p.Key(new UiKeyEvent(k,true,false),c);p.Key(new UiKeyEvent(k,false,false),c);return used;}
    static Bitmap Paint(LoginPanel p){var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap;}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("login-followup")]
    static void Run(WorldModuleTestContext t){
        var previous=LoginControl.NativeEntry;
        try{
            Layout(t);
            Unusable(t);
            NoticeFlow(t);
            Scrolling(t);
            Stale(t);
            NoticePaint(t);
            ExitButton(t);
            NewsText(t);
        }finally{LoginControl.NativeEntry=previous;}
    }

    static void Layout(WorldModuleTestContext t){
        var p=new Rig().Panel;
        t.Check(p.Available,"layout usable",p.Unavailable);
        var n=p.NoticeWindow;
        t.Check(n!=null,"Notice_Window built",p.Skipped);
        if(n==null)return;
        t.Equal(new Point(200,100),n.Origin,"Notice_Window X/Y");
        t.Equal(new Size(200,150),n.Size,"Notice_Window size = BaseSurf");
        t.Equal(21,n.BaseImage?.BaseSurface?.Surf??0,"BaseSurf 21");
        var label=n.Find(LoginPanel.MessageLabel);
        t.Check(label?.Kind==LoginPieceKind.Label&&label.Local==new Rectangle(10,10,150,60),"Message_Label geometry");
        t.Equal(new Rectangle(170,11,10,8),n.Find("Up_Button")?.Local??Rectangle.Empty,"Up_Button offset by ScrollBar X/Y (Y=-9)");
        t.Equal(new Rectangle(170,80,10,8),n.Find("Down_Button")?.Local??Rectangle.Empty,"Down_Button offset by ScrollBar X/Y");
        t.Check(n.Find("Move_Button")==null&&p.Skipped.Any(s=>s.Contains("ScrollBar")&&s.Contains("拇指")),"Move_Button (thumb) not built",p.Skipped);
        t.Check(n.Find("Ok_Button")?.MouseEvent==LoginPanel.NoticeOkEvent&&n.Find("Skip_Button")?.MouseEvent==LoginPanel.NoticeSkipEvent,"Ok / Skip events");
        t.Check(!p.Skipped.Any(s=>s.Contains("Notice_Window，")),"Notice_Window not listed as skipped",p.Skipped);
        int I(string name)=>n.Pieces.ToList().FindIndex(x=>x.Name==name);
        t.Check(I("Back_Image")<I("Frame_Image")&&I("Frame_Image")<I("Ok_Button"),"Notice_Window children painted in reverse XML order");
    }

    static void Unusable(WorldModuleTestContext t){
        // BaseSurf image missing → no size → not usable.
        var noBase=new Rig(surf:id=>id==21?null:Surfs.GetValueOrDefault(id));
        t.Check(noBase.Panel.Available&&noBase.Panel.NoticeWindow==null,"BaseSurf missing → no Notice_Window, login still usable");
        t.Check(noBase.Panel.Skipped.Any(s=>s.Contains(LoginPanel.NoticeWindowName)),"unusable Notice_Window listed in Skipped",noBase.Panel.Skipped);
        t.Check(!noBase.Show()&&noBase.Panel.Notice==null&&noBase.Confirms==0,"ShowNotice refused → caller keeps the message box");
        // No Message_Label.
        var noLabel=new Rig(Xml(NoticeXml.Replace("Message_Label","Other_Label")));
        t.Check(noLabel.Panel.NoticeWindow==null&&!noLabel.Show(),"no Message_Label → not usable");
        // No Notice_Window at all.
        var none=new Rig(Xml(""));
        t.Check(none.Panel.Available&&none.Panel.NoticeWindow==null&&!none.Show(),"no Notice_Window → not usable");
        // Login screen not in a state where it shows.
        var list=new Rig();list.Session.State=LoginState.CharacterList;
        t.Check(!list.Show()&&list.Panel.Notice==null,"CharacterList → refused");
        var inWorld=new Rig();inWorld.Session.State=LoginState.InWorld;
        t.Check(!inWorld.Show(),"InWorld → refused");
        // Panel without a layout.
        var empty=new LoginPanel(null,()=>new Rig().Control);
        t.Check(!empty.ShowNotice("x",()=>{}),"unloaded panel → refused");
    }

    static void NoticeFlow(WorldModuleTestContext t){
        var r=new Rig();var p=r.Panel;
        r.Update();
        t.Check(p.Visible&&p.Bounds!=UiRoot.Canvas,"login screen shows in AwaitList, not modal yet");
        t.Check(r.Show("第一行\r\n第二行"),"ShowNotice accepted");
        t.Check(p.Visible&&p.Notice=="第一行\r\n第二行"&&p.Bounds==UiRoot.Canvas,"Notice_Window open and modal");
        t.Check(p.ButtonAt(new Point(330,225))?.Name=="Ok_Button","Ok_Button hit at window origin + offset");
        t.Check(p.ButtonAt(new Point(310,375))==null,"Login_Button not reachable while the notice shows");
        // Modal: keys and characters do not reach the inputs, the login button does nothing.
        Key(p,Keys.A);
        t.Check(p.Account==""&&!p.Char('a')&&p.Account=="","typing ignored while the notice shows");
        Click(p,310,375);
        t.Check(r.Session.Runs==0&&p.Notice!=null,"no login while the notice shows");
        t.Check(Key(p,Keys.F5)==false,"F-keys still go to the client");
        // Ok click → confirm once.
        Click(p,330,225);
        t.Check(p.Notice==null&&r.Confirms==1&&p.Bounds!=UiRoot.Canvas,"Ok_Button confirms and closes");
        t.Equal(LoginPanel.IdInput,p.Focus,"focus back on ID_Input");
        Click(p,330,225);
        t.Equal(1,r.Confirms,"second click after close does not confirm again");
        // Release outside the button → nothing.
        r.Show();
        p.Mouse(M(UiMouseKind.Down,330,225),new UiNullCommands());p.Mouse(M(UiMouseKind.Up,100,50),new UiNullCommands());
        t.Check(p.Notice!=null&&r.Confirms==1,"release outside Ok does not confirm");
        // Skip → confirm.
        Click(p,230,225);
        t.Check(p.Notice==null&&r.Confirms==2,"Skip_Button confirms (推測: same as Ok)");
        // Enter / Esc / Close → confirm.
        r.Show();t.Check(Key(p,Keys.Enter)&&p.Notice==null&&r.Confirms==3,"Enter confirms");
        r.Show();t.Check(Key(p,Keys.Escape)&&p.Notice==null&&r.Confirms==4,"Esc confirms");
        r.Show();p.Close();r.Update();
        t.Check(p.Notice==null&&r.Confirms==5&&p.Visible,"Close() confirms the notice and keeps the login screen");
        // Repeat Enter does not confirm (held key from the login box).
        r.Show();p.Key(new UiKeyEvent(Keys.Enter,true,true),new UiNullCommands());
        t.Check(p.Notice!=null&&r.Confirms==5,"auto-repeat Enter ignored");
        Key(p,Keys.Enter);
        // A closed login screen (Close in AwaitList) shows again for the notice.
        var c=new Rig();c.Update();c.Panel.Close();c.Update();
        t.Check(!c.Panel.Visible,"closed login screen hidden");
        t.Check(c.Show()&&c.Panel.Visible&&c.Panel.Notice!=null,"notice re-opens a closed login screen");
        // The account typed before stays; focus goes to the password box after Ok.
        var a=new Rig();a.Update();Key(a.Panel,Keys.A);a.Show();Key(a.Panel,Keys.Enter);
        t.Check(a.Panel.Account=="a"&&a.Panel.Focus==LoginPanel.PasswordInput,"account kept, focus on Password_Input after the notice");
    }

    static void Scrolling(WorldModuleTestContext t){
        var r=new Rig();var p=r.Panel;r.Show();
        p.SetNoticeLines(10,3);
        t.Equal(7,p.NoticeMaxScroll,"max scroll = lines − visible");
        Click(p,372,113);
        t.Equal(0,p.NoticeScroll,"Up at the top stays 0");
        Click(p,372,182);
        t.Equal(1,p.NoticeScroll,"Down_Button scrolls one line");
        p.Mouse(M(UiMouseKind.Wheel,250,150,MouseButtons.None,-120),new UiNullCommands());
        t.Equal(2,p.NoticeScroll,"wheel down scrolls one line");
        p.Mouse(M(UiMouseKind.Wheel,250,150,MouseButtons.None,120),new UiNullCommands());
        t.Equal(1,p.NoticeScroll,"wheel up");
        Key(p,Keys.Down);t.Equal(2,p.NoticeScroll,"Down key");
        Key(p,Keys.Up);t.Equal(1,p.NoticeScroll,"Up key");
        Key(p,Keys.PageDown);t.Equal(4,p.NoticeScroll,"PageDown = visible lines");
        Key(p,Keys.PageDown);Key(p,Keys.PageDown);t.Equal(7,p.NoticeScroll,"clamped at max");
        Click(p,372,113);t.Equal(6,p.NoticeScroll,"Up_Button scrolls back");
        p.SetNoticeLines(4,3);t.Equal(1,p.NoticeScroll,"shorter text clamps the scroll");
        t.Check(r.Confirms==0&&p.Notice!=null,"scrolling does not confirm");
        // Wheel over the login screen without a notice does nothing harmful.
        var q=new Rig();q.Update();q.Panel.Mouse(M(UiMouseKind.Wheel,310,310,MouseButtons.None,-120),new UiNullCommands());
        t.Equal(0,q.Panel.NoticeScroll,"no notice → wheel does not scroll");
    }

    static void Stale(WorldModuleTestContext t){
        foreach(var s in new[]{LoginState.Cancelled,LoginState.Rejected,LoginState.Faulted,LoginState.Disconnected,LoginState.CharacterList}){
            var r=new Rig();r.Show();r.Session.State=s;r.Update();
            t.Check(r.Panel.Notice==null&&r.Confirms==0,$"session moved to {s} → notice dropped without confirm");
        }
        var n=new Rig();n.Show();n.Update();
        t.Check(n.Panel.Notice!=null,"still AwaitList → notice stays across updates");
        // A newer notice replaces the open one; the old confirm is not run.
        int first=0,second=0;var p=n.Panel;
        p.ShowNotice("一",()=>first++);p.ShowNotice("二",()=>second++);
        Key(p,Keys.Enter);
        t.Check(p.Notice==null&&first==0&&second==1&&n.Confirms==0,"newer notice replaces the old one");
        // A message window (refused login) shows above the notice; closing it leaves the notice.
        var m=new Rig();m.Show();
        t.Check(m.Panel.Notice!=null&&m.Panel.MessageWindow==null,"layout without Message_Window: notice only");
    }

    static void NoticePaint(WorldModuleTestContext t){
        var r=new Rig();var p=r.Panel;r.Show("A");
        using(var bmp=Paint(p)){
            t.Equal(Argb(Frame),bmp.GetPixel(205,102).ToArgb(),"Frame_Image above BaseSurf");
            t.Equal(Argb(Base),bmp.GetPixel(205,140).ToArgb(),"BaseSurf at the window origin");
            t.Equal(Argb(Back),bmp.GetPixel(355,165).ToArgb(),"Back_Image at 10,10");
            t.Equal(Argb(Ok),bmp.GetPixel(321,221).ToArgb(),"Ok_Button");
            t.Equal(Argb(Skip),bmp.GetPixel(221,221).ToArgb(),"Skip_Button");
            t.Equal(Argb(Up),bmp.GetPixel(371,112).ToArgb(),"Up_Button");
            t.Equal(Argb(Down),bmp.GetPixel(371,181).ToArgb(),"Down_Button");
            t.Equal(Argb(Exit),bmp.GetPixel(701,561).ToArgb(),"login window still drawn under the notice");
        }
        t.Equal(0,p.NoticeMaxScroll,"one line → nothing to scroll");
        r.Show(string.Join("\n",Enumerable.Range(1,30).Select(i=>"line "+i)));
        Paint(p).Dispose();
        t.Check(p.NoticeMaxScroll>0&&p.NoticeMaxScroll<30,"long news → scrollable (max from paint)",p.NoticeMaxScroll);
        p.ScrollNotice(1000);
        t.Equal(p.NoticeMaxScroll,p.NoticeScroll,"scroll clamped to the painted max");
        Paint(p).Dispose();
        t.Check(p.NoticeScroll==p.NoticeMaxScroll,"repaint keeps the scroll");
        // No notice → the notice window is not drawn.
        Key(p,Keys.Enter);
        using(var bmp=Paint(p))t.Equal(Argb(0xFF000000),bmp.GetPixel(205,140).ToArgb(),"closed notice not drawn");
    }

    static void ExitButton(WorldModuleTestContext t){
        var r=new Rig();var p=r.Panel;r.Update();
        var cmd=new ExitCommands();
        Key(p,Keys.A);
        Click(p,710,565,cmd);
        t.Equal(1,cmd.Exits,"Exit_Button → IUiLoginCommands.ExitClient");
        t.Check(!p.Unhandled.Contains(LoginPanel.ExitEvent),"Exit not logged as not wired");
        t.Check(p.Password==""&&p.Focus==null,"exit clears the password and the focus");
        // Through the macro decorator (the bridge wraps its commands in UiMacroCommands).
        var wrapped=new Rig();wrapped.Update();var inner=new ExitCommands();
        t.Check(wrapped.Panel.ExitClient(new WrapCommands(inner)),"found through a decorator");
        t.Equal(1,inner.Exits,"decorated exit");
        // Without the command (offline / old bridge): logged once, nothing else.
        var plain=new Rig();plain.Update();var none=new UiNullCommands();
        Click(plain.Panel,710,565,none);
        t.Check(plain.Panel.Unhandled.Contains(LoginPanel.ExitEvent)&&plain.Panel.Visible&&none.Calls.Count==0,"no IUiLoginCommands → logged only");
        // Other buttons are still not wired.
        Click(p,710,525,cmd);
        t.Check(p.Unhandled.Contains("Credit_LoginWin")&&cmd.Exits==1,"Credit_Button still not wired");
        // While the notice shows, Exit is not reachable (modal).
        var n=new Rig();n.Show();var c2=new ExitCommands();
        Click(n.Panel,710,565,c2);
        t.Check(c2.Exits==0&&n.Panel.Notice!=null,"Exit blocked by the modal notice");
        // Hidden screen → ExitClient refused.
        var h=new Rig();var c3=new ExitCommands();
        t.Check(!h.Panel.ExitClient(c3)&&c3.Exits==0,"hidden login screen → no exit");
    }
    /// <summary>A command decorator like UiMacroCommands (ILegacyWindowCommandsDecorator).</summary>
    sealed class WrapCommands(IUiCommands inner):IUiCommands,ILegacyWindowCommandsDecorator {
        public IUiCommands Inner=>inner;
        public void ShowDevTools(string? page=null)=>inner.ShowDevTools(page);
        public void ReturnGameFocus()=>inner.ReturnGameFocus();
        public void Logout()=>inner.Logout();
    }

    static void NewsText(WorldModuleTestContext t){
        // The bridge hands LoginNewsText.Display to the panel (same text as the message box); Wrap keeps its line breaks.
        string text=LoginNewsText.Display("一\n二\r\n三");
        t.Equal(3,LoginPanel.Wrap(text,1000,s=>s.Length).Count,"news line breaks kept in the notice");
        t.Equal("（伺服器公告為空）",LoginNewsText.Display("  "),"empty news text");
    }
}
