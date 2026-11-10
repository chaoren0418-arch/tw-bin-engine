namespace P06.NativeUi;

// T326 (#764) login-ui 離線測試：只用合成版面、合成單色圖與不連網的假 ILoginSession；不讀主客戶端或參考客戶端的
// LoginUI.xml、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class LoginPanelTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    // Synthetic layout with the element structure of the reference LoginUI.xml (own coordinates and image numbers).
    const string Xml="""
        <?xml version="1.0"?>
        <LoginUI>
          <Window Name = "Message_Window" X = "100" Y = "100" Modal = "1" Top = "1" DarkBack = "1" Activate = "0" BaseSurf = "92;3">
            <MultiLabel Name = "Message_Label" X = "10" Y = "10" FontSize = "14" Width = "180" Height = "40" VAlign = "center"/>
            <Button Name = "Ok_Button" X = "10" Y = "60" BaseSurf = "93" HighlightSurf = "94" PushSurf = "95" MouseEvent = "Ok_MessageWin"/>
            <Button Name = "Cancel_Button" X = "120" Y = "60" BaseSurf = "96" PushSurf = "98" MouseEvent = "Cancel_MessageWin"/>
            <Image Name = "Back_Image" X = "2" Y = "2" Surf = "91"/>
          </Window>
          <Window Name = "Notice_Window" X = "10" Y = "10" Modal = "1" BaseSurf = "21;3">
            <ScrollBar Name = "Notice_Scrollbar" X = "1" Y = "1" Width = "5" Height = "5"/>
          </Window>
          <Window Name = "Login_Window" X = "0" Y = "0" Activate = "0">
            <InputBox Name = "ID_Input" X = "300" Y = "300" Width = "100" Height = "20" English = "1" LimitText = "5" FontSize = "14"/>
            <InputBox Name = "Password_Input" X = "300" Y = "330" Width = "100" Height = "30" Password = "1" LimitText = "4" FontSize = "14"/>
            <Button Name = "Login_Button" X = "300" Y = "370" BaseSurf = "10;1" HighlightSurf = "11;1" PushSurf = "12;1" MouseEvent = "Login_LoginWin"/>
            <Image Name = "Mark" X = "300" Y = "200" Surf = "13;1"/>
            <Image Name = "EditBox_Back" X = "295" Y = "295" Surf = "16;1"/>
            <Button Name = "Exit_Button" X = "700" Y = "560" BaseSurf = "17;4" HighlightSurf = "18;4" PushSurf = "18;4" MouseEvent = "Exit_LoginWin"/>
            <Image Name = "Exit_Button_Multi" X = "700" Y = "560" Surf = "19;3"/>
            <Image Name = "Gone" X = "10" Y = "10" Surf = "99"/>
            <Image Name = "Back_Image" X = "0" Y = "0"/>
          </Window>
          <Window Name = "Credit_Window" X = "0" Y = "0" Activate = "0" BaseSurf = "31">
            <TextScroller X = "1" Y = "1" Width = "1" Height = "1" TextFile = "credits" FontSize = "14"/>
          </Window>
        </LoginUI>
        """;

    const uint LoginBtn=0xFF00AA00,LoginHi=0xFF00FF00,LoginPush=0xFF005500,Mark=0xFF884400,Edit=0xFF202040,Exit=0xFF990000,ExitMulti=0xFF330000,
        MsgBase=0xFF404040,MsgBack=0xFF101010,Ok=0xFF0000AA,OkHi=0xFF0000FF,Cancel=0xFFAAAA00;
    static readonly Dictionary<int,UiSurface> Surfs=new(){
        [10]=Surface(10,40,20,LoginBtn),[11]=Surface(11,40,20,LoginHi),[12]=Surface(12,40,20,LoginPush),[13]=Surface(13,60,40,Mark),
        [16]=Surface(16,120,70,Edit),[17]=Surface(17,60,20,Exit),[18]=Surface(18,60,20,0xFFFF0000),[19]=Surface(19,60,20,ExitMulti),
        [91]=Surface(91,190,50,MsgBack),[92]=Surface(92,200,100,MsgBase),[93]=Surface(93,40,15,Ok),[94]=Surface(94,40,15,OkHi),
        [95]=Surface(95,40,15,0xFF000055),[96]=Surface(96,40,15,Cancel),[98]=Surface(98,40,15,0xFF555500),
        [21]=Surface(21,5,5,0xFFFFFFFF),[31]=Surface(31,5,5,0xFFFFFFFF)};   // 99 deliberately missing
    static UiLayout Layout(string text=Xml)=>UiLayout.Parse(text,"LoginUI.xml");

    /// <summary>Socket-free session: Run only records the call and moves to Connecting (the task never completes on its own).</summary>
    sealed class FakeSession:ILoginSession {
        public LoginState State{get;set;}=LoginState.Offline;
        public int Revision{get;set;}=3;
        public int SessionGeneration{get;set;}=7;
        public IReadOnlyList<LoginCharacter> Characters=>[];
        public readonly List<(int Port,string Profile,string Account,string Password)> Runs=[];
        public Task Run(int port,string profileId,string account,string password,int timeoutMs=5000,CancellationToken token=default){
            Runs.Add((port,profileId,account,password));State=LoginState.Connecting;return new TaskCompletionSource().Task;
        }
        public Task SelectCharacter(string name)=>Task.CompletedTask;
        public void Cancel(bool offline=false)=>State=offline?LoginState.Offline:LoginState.Cancelled;
    }
    sealed class Rig {
        public readonly FakeSession Session=new();public int Prepared;public readonly LoginControl Control;public readonly LoginPanel Panel;
        public Rig(Func<int,UiSurface?>? surf=null,string text=Xml){
            Control=new LoginControl(Session,()=>Prepared++,()=>{});
            Panel=new LoginPanel(null,()=>Control){CapsLock=()=>false};
            Panel.Load(Layout(text),surf??(id=>Surfs.GetValueOrDefault(id)));
        }
    }
    static readonly UiStaticGameState Front=new(InWorld:false);
    static UiMouseEvent M(UiMouseKind k,int x,int y,MouseButtons b=MouseButtons.Left)=>new(k,new Point(x,y),b,1,0,Keys.None);
    static void Click(LoginPanel p,int x,int y,UiNullCommands? c=null){c??=new();p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
    static bool Key(LoginPanel p,Keys k,UiNullCommands? c=null){c??=new();bool used=p.Key(new UiKeyEvent(k,true,false),c);p.Key(new UiKeyEvent(k,false,false),c);return used;}
    static void Type(LoginPanel p,params Keys[] keys){foreach(var k in keys)Key(p,k);}
    static int Pixel(LoginPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("login-ui")]
    static void Run(WorldModuleTestContext t){
        var previous=LoginControl.NativeEntry;
        try{
            LoadLayout(t);
            Broken(t);
            ShowAndHide(t);
            Typing(t);
            LoginFlow(t);
            MouseAndPaint(t);
            Helpers(t);
            Entry(t);
            Registration(t);
        }finally{LoginControl.NativeEntry=previous;}
    }

    static void LoadLayout(WorldModuleTestContext t){
        var p=new Rig().Panel;
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Check(!p.Visible&&p.Bounds.IsEmpty,"hidden after load");
        var w=p.Login!;
        t.Equal(Point.Empty,w.Origin,"Login_Window X/Y");
        t.Check(w.BaseImage==null&&w.Size.IsEmpty,"Login_Window has no BaseSurf and no size");
        var id=w.Find(LoginPanel.IdInput)!;var pw=w.Find(LoginPanel.PasswordInput)!;
        t.Check(id.Kind==LoginPieceKind.Input&&id.Local==new Rectangle(300,300,100,20)&&id.Limit==5&&!id.Password,"ID_Input geometry / LimitText");
        t.Check(pw.Local==new Rectangle(300,330,100,30)&&pw.Limit==4&&pw.Password,"Password_Input geometry / Password=1");
        var login=w.Find("Login_Button")!;
        t.Check(login.Base==new LoginImageRef("BaseSurf",10,1)&&login.HighlightSurface?.Surf==11&&login.PushSurface?.Surf==12&&login.MouseEvent==LoginPanel.LoginEvent,"Login_Button images / event");
        t.Equal(new Rectangle(300,370,40,20),login.Local,"button size = its BaseSurf");
        // Reverse XML order: Exit_Button_Multi (later child) painted before Exit_Button; ID_Input (first child) last.
        int I(string n)=>w.Pieces.ToList().FindIndex(x=>x.Name==n);
        t.Check(I("Exit_Button_Multi")<I("Exit_Button")&&I("EditBox_Back")<I("Password_Input")&&I("Mark")<I("Login_Button")&&I(LoginPanel.IdInput)==w.Pieces.Count-1,"children painted in reverse XML order");
        t.Check(p.Missing.Count==1&&p.Missing[0].StartsWith("surf 99 ",StringComparison.Ordinal)&&p.Missing[0].EndsWith("/@Surf)",StringComparison.Ordinal),"missing surf listed",p.Missing);
        t.Check(w.Find("Gone") is {BaseSurface:null}&&w.Find("Gone")!.Local.IsEmpty,"missing image kept, not drawn, no area");
        t.Check(p.Skipped.Any(s=>s.Contains("Window[2]/Image[")&&s.Contains("執行期")),"Back_Image without Surf skipped",p.Skipped);
        t.Check(p.Skipped.Any(s=>s.Contains("Notice_Window"))&&p.Skipped.Any(s=>s.Contains("Credit_Window")),"other windows skipped",p.Skipped);
        t.Equal(new Rectangle(295,200,465,380),w.Extent,"window extent = union of built pieces (no declared size)");
        var m=p.MessageWindow!;
        t.Check(m.Origin==new Point(100,100)&&m.Size==new Size(200,100)&&m.BaseImage?.Base is {Id:92,Flag:3},"Message_Window origin, size from BaseSurf, flag kept");
        t.Check(m.Find(LoginPanel.MessageLabel) is {Kind:LoginPieceKind.Label} l&&l.Local==new Rectangle(10,10,180,40),"Message_Label");
        t.Check(m.Find("Cancel_Button")?.HighlightSurface==null&&m.Find("Ok_Button")?.HighlightSurface!=null,"highlight optional");
        t.Equal(new LoginImageRef("Surf",7000,0),LoginPanel.ImageAttr(Layout("<A><Image Surf=\"7000\"/></A>").Root.Children[0],"Surf"),"id without flag");
    }

    static void Broken(WorldModuleTestContext t){
        t.Throws<UiLayoutException>(()=>new Rig(text:Xml.Replace("\"Login_Window\"","\"Other\"")),"no Login_Window");
        t.Throws<UiLayoutException>(()=>new Rig(text:Xml.Replace("\"Password_Input\"","\"ID_Input\"")),"two ID inputs");
        t.Throws<UiLayoutException>(()=>new Rig(text:Xml.Replace("Password = \"1\"","Password = \"0\"")),"password box without Password=1");
        t.Throws<UiLayoutException>(()=>new Rig(text:Xml.Replace("LimitText = \"5\"","")),"InputBox without LimitText");
        t.Throws<UiLayoutException>(()=>new Rig(text:Xml.Replace("Width = \"100\" Height = \"20\"","")),"InputBox without size");
        t.Throws<UiLayoutException>(()=>new Rig(text:Xml.Replace("BaseSurf = \"10;1\"","BaseSurf = \"x\"")),"bad surf number");
        t.Throws<UiLayoutException>(()=>new Rig(text:Xml.Replace("Name = \"Login_Window\" X = \"0\"","Name = \"Login_Window\"")),"window X missing → no invented origin");
        var noMsg=new Rig(id=>id==92?null:Surfs.GetValueOrDefault(id)).Panel;
        t.Check(noMsg.Available&&noMsg.MessageWindow==null&&noMsg.Missing.Any(x=>x.StartsWith("surf 92 ",StringComparison.Ordinal)),"Message_Window BaseSurf missing → no message window, login still usable");
        var noImages=new Rig(_=>null).Panel;
        t.Check(noImages.Available&&noImages.Login!.Extent==new Rectangle(300,300,100,60),"all surfs missing → only the input boxes remain (nothing substituted)");
        var empty=new LoginPanel(null,()=>null);
        t.Throws<UiLayoutException>(()=>empty.Load(Layout("<LoginUI><Window Name=\"Login_Window\" X=\"0\" Y=\"0\"/></LoginUI>"),_=>null),"no inputs");
        var host=new LoginPanel(null,()=>null);host.Load(Layout(),(SurfCatalog)null!);   // returns before touching the catalog
        host.Update(Front);
        t.Check(!host.Available&&host.Unavailable=="沒有 UiResources"&&!host.Visible,"product Load without resources → unavailable, never shown");
    }

    static void ShowAndHide(WorldModuleTestContext t){
        var r=new Rig();var p=r.Panel;
        var none=new LoginPanel(null,()=>null);none.Load(Layout(),id=>Surfs.GetValueOrDefault(id));none.Update(Front);
        t.Check(!none.Visible,"no LoginControl → hidden");
        p.Update(Front);
        t.Check(p.Visible&&p.Focus==LoginPanel.IdInput,"Offline (front) → shown, ID box focused");
        t.Equal(new Rectangle(295,200,465,380),p.Bounds,"bounds = extent (world outside stays reachable)");
        p.Update(new UiStaticGameState(InWorld:true));
        t.Check(!p.Visible,"in world → hidden");
        foreach(var s in new[]{LoginState.CharacterList,LoginState.EnteringWorld,LoginState.InWorld,LoginState.ManagingCharacters,LoginState.SwitchingCharacter}){
            r.Session.State=s;p.Update(Front);
            t.Check(!p.Visible,$"{s} → hidden");
        }
        foreach(var s in new[]{LoginState.Offline,LoginState.Connecting,LoginState.AwaitSeed,LoginState.Authenticating,LoginState.AwaitList,LoginState.ReadingCharacters,LoginState.Rejected,LoginState.Disconnected,LoginState.Cancelled,LoginState.Faulted})
            t.Check(LoginPanel.ShowsIn(s),$"{s} shows the login screen");
        r.Session.State=LoginState.Offline;p.Update(Front);
        IUiClosablePanel closable=p;closable.Close();
        p.Update(Front);
        t.Check(!p.Visible,"Close hides while the state stays the same");
        r.Session.State=LoginState.Cancelled;p.Update(Front);
        t.Check(p.Visible,"state changed → shown again");
    }

    static void Typing(WorldModuleTestContext t){
        var r=new Rig();var p=r.Panel;var cmd=new UiNullCommands();
        t.Check(!p.Key(new UiKeyEvent(Keys.A,true,false),cmd),"hidden → keys not used");
        p.Update(Front);
        Type(p,Keys.A,Keys.B|Keys.Shift,Keys.D1,Keys.NumPad2);
        t.Equal("aB12",p.Account,"letters / shift / digits / numpad");
        Type(p,Keys.C,Keys.D);
        t.Equal("aB12c",p.Account,"LimitText 5");
        Key(p,Keys.Back);
        t.Equal("aB12",p.Account,"Backspace");
        t.Check(Key(p,Keys.Up),"arrow keys swallowed while typing (no offline-preview walk)");
        t.Check(!Key(p,Keys.F5)&&!Key(p,Keys.A|Keys.Control),"F-keys and Ctrl chords go to the client");
        Key(p,Keys.Tab);
        t.Equal(LoginPanel.PasswordInput,p.Focus,"Tab → password box");
        p.CapsLock=()=>true;
        Type(p,Keys.X,Keys.D1|Keys.Shift,Keys.Y|Keys.Shift,Keys.Z,Keys.Q);
        t.Equal("X!yZ",p.Password,"caps lock / shifted digit / LimitText 4");
        t.Equal("****",p.Display(p.Login!.Find(LoginPanel.PasswordInput)!),"password drawn as '*'");
        t.Equal("aB12",p.Display(p.Login!.Find(LoginPanel.IdInput)!),"account drawn as typed");
        Key(p,Keys.Tab);
        t.Check(p.Char('z')&&p.Account=="aB12z"&&!p.Char('\b')&&!p.Char('中'),"WM_CHAR: printable ASCII only");
        Key(p,Keys.Escape,cmd);
        t.Check(p.Focus==null&&cmd.Calls.Contains("game-focus"),"Esc leaves the boxes and returns game focus");
        t.Check(!p.Key(new UiKeyEvent(Keys.A,true,false),cmd)&&!p.Char('a')&&p.Account=="aB12z","no focus → keys go to the game");
        Key(p,Keys.Enter);
        t.Check(r.Session.Runs.Count==0,"Enter without focus does nothing");
    }

    static void LoginFlow(WorldModuleTestContext t){
        var r=new Rig();var p=r.Panel;
        p.Update(Front);
        Type(p,Keys.T,Keys.E,Keys.S,Keys.T);
        Key(p,Keys.Enter);
        t.Equal(LoginPanel.PasswordInput,p.Focus,"Enter in the ID box → password box");
        Type(p,Keys.P,Keys.W);
        Key(p,Keys.Enter);
        t.Check(r.Session.Runs.Count==1&&r.Session.Runs[0]==(ProductEntry.Port,ProductEntry.Profile,"test","pw"),"Enter → LoginControl.Start with the normal-entry endpoint",r.Session.Runs);
        t.Check(r.Prepared==1&&p.Password==""&&p.Account=="test"&&p.Attempted,"beforeStart ran, password cleared, attempt recorded");
        p.Update(Front);
        t.Check(p.Visible&&p.Message==null,"Connecting → still shown, no message");
        r.Session.State=LoginState.Rejected;p.Update(Front);
        t.Check(p.Message==ProductEntry.StateName(LoginState.Rejected)&&!p.Attempted,"Rejected after a native login → Message_Window");
        t.Equal(UiRoot.Canvas,p.Bounds,"message window is modal (whole canvas)");
        t.Check(Key(p,Keys.A)&&p.Account=="test","modal message takes the keys");
        Key(p,Keys.Enter);
        t.Check(p.Message==null&&p.Focus==LoginPanel.PasswordInput,"Enter closes the message, focus back to the password box");
        p.Update(Front);
        t.Check(p.Message==null,"message not repeated for the same end state");
        // Refused by LoginControl (here: already running) → its message.
        Type(p,Keys.P);
        var refused=p.SubmitLogin();
        t.Check(refused is {Accepted:false}&&p.Message==refused.Value.Message&&r.Session.Runs.Count==1,"refused start → LoginControl message in Message_Window");
        Key(p,Keys.Escape);
        t.Check(p.Message==null,"Esc closes the message");
        // Validation refusal from LoginPackets (account must be ASCII letters/digits).
        var v=new Rig();v.Panel.Update(Front);
        Type(v.Panel,Keys.A,Keys.D1|Keys.Shift,Keys.Tab,Keys.B,Keys.Enter);
        t.Check(v.Session.Runs.Count==0&&v.Panel.Message==LoginPackets.LoginValidationError("a!","b"),"validation message shown, nothing sent");
        // Cancelled after a native login: no message.
        var c=new Rig();c.Panel.Update(Front);
        Type(c.Panel,Keys.A,Keys.Tab,Keys.B,Keys.Enter);
        c.Session.State=LoginState.Cancelled;c.Panel.Update(Front);
        t.Check(c.Panel.Message==null&&!c.Panel.Attempted,"cancel → no message");
        // No message window (BaseSurf missing): refusal only logged.
        var n=new Rig(id=>id==92?null:Surfs.GetValueOrDefault(id));n.Panel.Update(Front);
        Type(n.Panel,Keys.Tab,Keys.B,Keys.Enter);
        t.Check(n.Panel.Message==null&&n.Panel.Unhandled.Contains("message")&&n.Panel.Bounds!=UiRoot.Canvas,"no message window → not modal, logged");
        // Character list → hidden, attempt forgotten.
        c.Session.State=LoginState.CharacterList;c.Panel.Update(Front);
        t.Check(!c.Panel.Visible,"character list → login screen hidden");
    }

    static void MouseAndPaint(WorldModuleTestContext t){
        var r=new Rig();var p=r.Panel;var cmd=new UiNullCommands();
        p.Update(Front);
        t.Equal(Argb(Mark),Pixel(p,301,201),"Image drawn");
        t.Equal(Argb(Edit),Pixel(p,296,296),"edit box image drawn");
        t.Equal(Argb(Exit),Pixel(p,701,561),"Exit_Button above Exit_Button_Multi (reverse XML order)");
        t.Equal(Argb(LoginBtn),Pixel(p,301,371),"Login_Button base");
        t.Equal(Color.Black.ToArgb(),Pixel(p,11,11),"missing surf not drawn (nothing substituted)");
        p.Mouse(M(UiMouseKind.Move,301,371,MouseButtons.None),cmd);
        t.Equal(Argb(LoginHi),Pixel(p,301,371),"hover → HighlightSurf");
        p.Mouse(M(UiMouseKind.Down,301,371),cmd);
        t.Equal(Argb(LoginPush),Pixel(p,301,371),"held → PushSurf");
        p.Mouse(M(UiMouseKind.Move,10,10),cmd);p.Mouse(M(UiMouseKind.Up,10,10),cmd);
        t.Check(r.Session.Runs.Count==0,"release outside the button does not fire");
        t.Check(p.Mouse(M(UiMouseKind.Down,350,335),cmd)&&p.Focus==LoginPanel.PasswordInput,"click on the password box focuses it");
        p.Mouse(M(UiMouseKind.Up,350,335),cmd);
        Type(p,Keys.Q);
        Click(p,350,305,cmd);
        t.Equal(LoginPanel.IdInput,p.Focus,"click on the ID box");
        Type(p,Keys.U);
        Click(p,701,561,cmd);
        t.Check(p.Unhandled.Contains("Exit_LoginWin")&&p.Visible,"Exit_Button not wired (logged)");
        Click(p,320,380,cmd);
        t.Check(r.Session.Runs.Count==1&&r.Session.Runs[0].Account=="u"&&r.Session.Runs[0].Password=="q","Login_Button logs in");
        r.Session.State=LoginState.Faulted;p.Update(Front);
        t.Check(p.Message==ProductEntry.StateName(LoginState.Faulted),"Faulted → message");
        t.Equal(Argb(MsgBack),Pixel(p,103,103),"message Back_Image over its BaseSurf");
        t.Equal(Argb(MsgBase),Pixel(p,101,101),"message BaseSurf at the window origin");
        t.Equal(Argb(Ok),Pixel(p,111,161),"Ok_Button drawn");
        t.Equal(Argb(LoginBtn),Pixel(p,301,371),"login window still drawn under the message (buttons idle)");
        t.Check(p.Mouse(M(UiMouseKind.Down,301,371),cmd)&&p.ButtonAt(new Point(301,371))==null,"modal: login window takes no clicks");
        p.Mouse(M(UiMouseKind.Up,301,371),cmd);
        t.Check(r.Session.Runs.Count==1&&p.Message!=null,"no login while the message shows");
        p.Mouse(M(UiMouseKind.Move,111,161,MouseButtons.None),cmd);
        t.Equal(Argb(OkHi),Pixel(p,111,161),"Ok hover");
        Click(p,111,161,cmd);
        t.Check(p.Message==null,"Ok closes the message");
        r.Session.State=LoginState.Disconnected;p.Update(Front);
        t.Check(p.Message==null,"no attempt pending → no message");
        t.Check(!p.Mouse(M(UiMouseKind.Down,5,5),cmd),"outside the extent → not used");
        p.Mouse(M(UiMouseKind.Up,5,5),cmd);
        var d=new Rig();d.Panel.Update(Front);
        Type(d.Panel,Keys.A,Keys.Tab,Keys.B);
        Click(d.Panel,320,380,cmd);
        d.Session.State=LoginState.Disconnected;d.Panel.Update(Front);
        t.Check(d.Panel.Message==ProductEntry.StateName(LoginState.Disconnected),"Disconnected after login → message");
        Click(d.Panel,221,161,cmd);
        t.Check(d.Panel.Message==null,"Cancel closes the message too");
    }

    static void Helpers(WorldModuleTestContext t){
        t.Equal<char?>('a',LoginPanel.KeyChar(Keys.A,false),"a");
        t.Equal<char?>('A',LoginPanel.KeyChar(Keys.A|Keys.Shift,false),"Shift+A");
        t.Equal<char?>('a',LoginPanel.KeyChar(Keys.A|Keys.Shift,true),"Shift+A with caps");
        t.Equal<char?>('@',LoginPanel.KeyChar(Keys.D2|Keys.Shift,false),"Shift+2");
        t.Equal<char?>('0',LoginPanel.KeyChar(Keys.D0,true),"0");
        t.Equal<char?>('_',LoginPanel.KeyChar(Keys.OemMinus|Keys.Shift,false),"Shift+-");
        t.Check(LoginPanel.KeyChar(Keys.Left,false)==null&&LoginPanel.KeyChar(Keys.ShiftKey|Keys.Shift,false)==null,"non-character keys");
        static int W(string s)=>s.Length*10;
        t.Check(LoginPanel.Wrap("登入遭拒絕",30,W).SequenceEqual(["登入遭","拒絕"]),"character wrap");
        t.Check(LoginPanel.Wrap("ab\r\ncd",100,W).SequenceEqual(["ab","cd"]),"explicit line breaks");
        t.Check(LoginPanel.Wrap("x",0,W).SequenceEqual(["x"]),"a too-narrow label still shows one character per line");
    }

    static void Entry(WorldModuleTestContext t){
        var s=new FakeSession();
        var a=new LoginControl(s,()=>{},()=>{});
        t.Check(ReferenceEquals(LoginControl.NativeEntry,a),"constructed LoginControl becomes the native entry");
        var b=new LoginControl(new FakeSession(),()=>{},()=>{});
        t.Check(ReferenceEquals(LoginControl.NativeEntry,b),"latest one wins (ClientForm builds one)");
        t.Equal(LoginState.Offline,b.SessionState,"SessionState forwards the session");
        var p=new LoginPanel(null){CapsLock=()=>false};p.Load(Layout(),id=>Surfs.GetValueOrDefault(id));p.Update(Front);
        t.Check(p.Visible,"default source reads LoginControl.NativeEntry");
        LoginControl.NativeEntry=null;p.Update(Front);
        t.Check(!p.Visible,"no entry → hidden");
        var direct=a.StartNative("abc","def");
        t.Check(direct.Accepted&&s.Runs.Single()==(ProductEntry.Port,ProductEntry.Profile,"abc","def"),"StartNative = Start with ProductEntry profile/port");
        t.Check(LoginControl.ValidEndpoint(ProductEntry.Profile,ProductEntry.Port),"normal-entry endpoint is valid");
    }

    static void Registration(WorldModuleTestContext t){
        var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==LoginPanel.PanelId);
        t.Check(reg!=null&&reg.Layout==LoginPanel.HostLayout,"[UiPanel] discovered with host layout uistatus86");
        t.Check(reg?.Create(new UiRoot()) is LoginPanel {Layer:UiLayer.Float} and IUiClosablePanel and IUiTextInput,"float layer, closable, text input");
        t.Check(UiPanelRegistry.Errors.Count==0,"no discovery errors",UiPanelRegistry.Errors);
    }
}
