namespace P06.NativeUi;

// T408 (#925, R10-U; FU13 §5 D11–D12) 離線測試：登入窗 Back_Image 的 Png 動畫 1825–1842。只用合成版面、合成單色圖、可控時鐘與
// 不連網的假 ILoginSession；不讀主客戶端或參考客戶端（真的 1825–1842.png 只在 kor25／ly，執行期才讀）、不啟動遊戲、不連網路。
// CI 由 --module-tests 自動發現執行。
static class LoginBackImageTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.png","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    const string Xml="""
        <?xml version="1.0"?>
        <LoginUI>
          <Window Name = "Message_Window" X = "100" Y = "100" Modal = "1" BaseSurf = "92">
            <MultiLabel Name = "Message_Label" X = "10" Y = "10" Width = "180" Height = "40" VAlign = "center"/>
            <Button Name = "Ok_Button" X = "10" Y = "60" BaseSurf = "93" MouseEvent = "Ok_MessageWin"/>
            <Image Name = "Back_Image" X = "2" Y = "2" Surf = "91"/>
          </Window>
          <Window Name = "Login_Window" X = "0" Y = "0" Activate = "0">
            <InputBox Name = "ID_Input" X = "300" Y = "300" Width = "100" Height = "20" English = "1" LimitText = "5"/>
            <InputBox Name = "Password_Input" X = "300" Y = "330" Width = "100" Height = "30" Password = "1" LimitText = "4"/>
            <Button Name = "Login_Button" X = "300" Y = "370" BaseSurf = "10" MouseEvent = "Login_LoginWin"/>
            <Image Name = "Mark" X = "300" Y = "200" Surf = "13"/>
            <Image Name = "Back_Image" X = "0" Y = "0"/>
          </Window>
        </LoginUI>
        """;
    const uint Btn=0xFF00AA00,Mark=0xFF884400,MsgBase=0xFF404040,MsgBack=0xFF101010,Ok=0xFF0000AA;
    static readonly Dictionary<int,UiSurface> Surfs=new(){
        [10]=Surface(10,40,20,Btn),[13]=Surface(13,60,40,Mark),[91]=Surface(91,190,50,MsgBack),[92]=Surface(92,200,100,MsgBase),[93]=Surface(93,40,15,Ok)};
    static UiLayout Layout(string text=Xml)=>UiLayout.Parse(text,"LoginUI.xml");
    /// <summary>Synthetic frame colour of png id (distinct per id, opaque).</summary>
    static uint Color(int id)=>0xFF000000u|(uint)((id-LoginPanel.BackPngFirst+1)*0x0B0907);

    sealed class FakeSession:ILoginSession {
        public LoginState State{get;set;}=LoginState.Offline;
        public int Revision{get;set;}=3;
        public int SessionGeneration{get;set;}=7;
        public IReadOnlyList<LoginCharacter> Characters=>[];
        public Task Run(int port,string profileId,string account,string password,int timeoutMs=5000,CancellationToken token=default){
            State=LoginState.Connecting;return new TaskCompletionSource().Task;
        }
        public Task SelectCharacter(string name)=>Task.CompletedTask;
        public void Cancel(bool offline=false)=>State=offline?LoginState.Offline:LoginState.Cancelled;
    }
    sealed class Rig {
        public readonly FakeSession Session=new();public readonly LoginControl Control;public readonly LoginPanel Panel;
        public long Now=1000;public readonly List<int> Looked=[];
        public Rig(Func<int,UiSurface?>? png,string text=Xml,int w=800,int h=600){
            Control=new LoginControl(Session,()=>{},()=>{});
            Panel=new LoginPanel(null,()=>Control){CapsLock=()=>false};
            Panel.Load(Layout(text),id=>Surfs.GetValueOrDefault(id));
            if(png!=null)Panel.PngLookup=id=>{Looked.Add(id);return png(id);};
            Panel.Clock=()=>Now;
        }
        public void Show()=>Panel.Update(Front);
    }
    static Func<int,UiSurface?> AllFrames(int w=800,int h=600)=>id=>id is >=LoginPanel.BackPngFirst and <LoginPanel.BackPngFirst+LoginPanel.BackFrames?Surface(id,w,h,Color(id)):null;
    static readonly UiStaticGameState Front=new(InWorld:false),World=new(InWorld:true);
    static int Pixel(LoginPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(System.Drawing.Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>System.Drawing.Color.FromArgb(unchecked((int)v)).ToArgb();
    static readonly int Black=System.Drawing.Color.Black.ToArgb();

    [WorldModuleTest("login-back-image")]
    static void Run(WorldModuleTestContext t){
        Sequence(t);
        LoadAndNoLookup(t);
        Animation(t);
        MissingFrame(t);
        Restart(t);
        NoBackImage(t);
    }

    // FU13§5.1: frame + 1 every 200 ms (18 → 0), png = 1825 + frame → 1826 … 1842, 1825, 1826 …
    static void Sequence(WorldModuleTestContext t){
        t.Equal(1826,LoginPanel.BackPngAt(0),"first frame 1826");
        t.Equal(1826,LoginPanel.BackPngAt(199),"still 1826 before 200 ms");
        t.Equal(1827,LoginPanel.BackPngAt(200),"1827 at 200 ms");
        t.Equal(1842,LoginPanel.BackPngAt(16*200),"1842 is the 17th frame");
        t.Equal(1825,LoginPanel.BackPngAt(17*200+199),"1825 closes the round");
        t.Equal(1826,LoginPanel.BackPngAt(18*200),"round of 18 frames = 3.6 s");
        t.Equal(1826,LoginPanel.BackPngAt(-5),"clock going back → first frame");
        var round=Enumerable.Range(0,LoginPanel.BackFrames).Select(n=>LoginPanel.BackPngAt(n*200L)).ToList();
        t.Check(round.Distinct().Count()==18&&round.Min()==1825&&round.Max()==1842&&round[0]==1826&&round[^1]==1825,"one round uses 1825–1842 once each",string.Join(",",round));
        t.Equal(1826,LoginPanel.BackPngAt(18L*200*1_000_000),"long uptime wraps");
        t.Equal(1827,LoginPanel.BackPngAt(18L*200*1_000_000+200),"long uptime keeps the order");
    }

    static void LoadAndNoLookup(WorldModuleTestContext t){
        var r=new Rig(null);var p=r.Panel;
        t.Check(p.Available,"layout usable",p.Unavailable);
        t.Check(p.BackImage is {Name:"Back_Image",Offset:{IsEmpty:true},Base:null}&&p.BackImage.Path.Contains("Window[1]/Image["),"Login_Window Back_Image recorded",p.BackImage?.Path);
        t.Check(p.Login!.Pieces.All(x=>x.Name!="Back_Image"),"Back_Image is not a built piece");
        t.Check(p.Skipped.Any(s=>s.Contains("Window[1]/Image[")&&s.Contains("執行期")),"still listed as a run-time picture",p.Skipped);
        t.Check(p.MessageWindow!.Find("Back_Image") is {BaseSurface:not null},"Message_Window Back_Image keeps its Surf");
        t.Check(p.BackPng==null&&p.BackSurface==null,"hidden → no frame");
        r.Show();
        t.Equal(1826,p.BackPng,"shown → frame 1826");
        t.Check(p.BackSurface==null,"no png lookup → nothing to draw");
        t.Equal(new Rectangle(300,200,100,190),p.Bounds,"no background → bounds = pieces only (unchanged)");
        t.Equal(Black,Pixel(p,310,330),"nothing drawn under the boxes without a background");
        t.Check(p.Missing.Count==0,"no lookup is not a missing png",p.Missing);
    }

    static void Animation(WorldModuleTestContext t){
        var r=new Rig(AllFrames());var p=r.Panel;
        t.Check(r.Looked.Count==0,"nothing read before the screen shows");
        r.Show();
        t.Equal(1000L,p.BackStart,"start time = clock when the window shows");
        t.Equal(UiRoot.Canvas,p.Bounds,"800×600 background → screen covers the canvas");
        t.Equal(Argb(Color(1826)),Pixel(p,5,5),"frame 1826 drawn at once");
        t.Equal(Argb(Mark),Pixel(p,310,210),"pieces drawn above the background");
        t.Equal(Argb(Btn),Pixel(p,305,375),"button above the background");
        r.Now=1000+199;t.Equal(Argb(Color(1826)),Pixel(p,700,500),"1826 until 200 ms");
        r.Now=1000+200;t.Equal(Argb(Color(1827)),Pixel(p,700,500),"1827 at 200 ms");
        r.Now=1000+16*200;t.Equal(Argb(Color(1842)),Pixel(p,5,5),"1842");
        r.Now=1000+17*200;t.Equal(Argb(Color(1825)),Pixel(p,5,5),"1825 closes the round");
        r.Now=1000+18*200;t.Equal(Argb(Color(1826)),Pixel(p,5,5),"next round starts with 1826");
        for(int n=0;n<2*LoginPanel.BackFrames;n++){r.Now=1000+n*200L;Pixel(p,5,5);}
        t.Check(r.Looked.Count==18&&r.Looked.Distinct().Count()==18,"two rounds: each png read once, then cached",r.Looked.Count);
        t.Check(p.Missing.Count==0,"all frames found",p.Missing);
        // Message box (validation refusal): the background stays under Login_Window, the message window over it.
        p.Char('a');p.Char('!');p.SubmitLogin();
        t.Check(p.Message!=null&&r.Session.State==LoginState.Offline,"refused login → Message_Window",p.Message);
        r.Now=1000;
        t.Equal(Argb(MsgBack),Pixel(p,103,103),"message window above the background");
        t.Equal(Argb(Color(1826)),Pixel(p,5,5),"background still drawn under the message window");
        // Background at the Back_Image X/Y and Login_Window origin; smaller picture → bounds = union.
        var small=new Rig(AllFrames(50,40),Xml.Replace("<Window Name = \"Login_Window\" X = \"0\" Y = \"0\"","<Window Name = \"Login_Window\" X = \"10\" Y = \"20\"")
            .Replace("<Image Name = \"Back_Image\" X = \"0\" Y = \"0\"/>","<Image Name = \"Back_Image\" X = \"5\" Y = \"6\"/>"));
        small.Show();
        t.Equal(Rectangle.Union(new Rectangle(15,26,50,40),new Rectangle(310,220,100,190)),small.Panel.Bounds,"bounds = pieces ∪ background");
        t.Equal(Argb(Color(1826)),Pixel(small.Panel,15,26),"background at window origin + Back_Image X/Y");
        t.Equal(Black,Pixel(small.Panel,14,25),"nothing outside the picture");
    }

    static void MissingFrame(WorldModuleTestContext t){
        var all=AllFrames();
        var r=new Rig(id=>id==1827?null:all(id));var p=r.Panel;
        r.Show();
        t.Equal(Argb(Color(1826)),Pixel(p,5,5),"1826 drawn");
        r.Now=1000+200;
        t.Check(p.BackSurface==null,"1827 missing → no surface");
        t.Equal(new Rectangle(300,200,100,190),p.Bounds,"missing frame → bounds = pieces only");
        t.Equal(Black,Pixel(p,310,330),"missing frame → nothing drawn (no substitute)");
        Pixel(p,310,330);
        t.Check(p.Missing.Count==1&&p.Missing[0].StartsWith("png 1827 ",StringComparison.Ordinal)&&p.Missing[0].Contains("Window[1]/Image["),"missing png listed once",p.Missing);
        t.Check(r.Looked.Count(x=>x==1827)==1,"missing png looked up once");
        r.Now=1000+400;
        t.Equal(Argb(Color(1828)),Pixel(p,5,5),"next frame drawn again");
        var none=new Rig(_=>null);none.Show();
        for(int i=0;i<18;i++){none.Now=1000+i*200;Pixel(none.Panel,5,5);}
        t.Check(none.Panel.Missing.Count==18&&none.Panel.Available,"no reference client: 18 pngs missing, login screen still usable",none.Panel.Missing.Count);
        var thrower=new Rig(_=>throw new UiResourceException("bad png"));thrower.Show();
        t.Check(thrower.Panel.BackSurface==null&&thrower.Panel.Missing.Count==1,"undecodable png → not drawn, listed missing",thrower.Panel.Missing);
    }

    static void Restart(WorldModuleTestContext t){
        var r=new Rig(AllFrames());var p=r.Panel;
        r.Show();r.Now=1000+5*200;
        t.Equal(1831,p.BackPng,"running");
        p.Update(World);
        t.Check(!p.Visible&&p.BackPng==null&&p.Bounds.IsEmpty,"in world → hidden, no background");
        r.Now=50_000;p.Update(Front);
        t.Equal(50_000L,p.BackStart,"shown again → start time reset (sub_EEAC50)");
        t.Equal(1826,p.BackPng,"shown again → starts at 1826");
        r.Now=50_000+200;p.Update(Front);
        t.Equal(50_000L,p.BackStart,"Update while shown keeps the start time");
        t.Equal(1827,p.BackPng,"frame follows the clock");
    }

    static void NoBackImage(WorldModuleTestContext t){
        var r=new Rig(AllFrames(),Xml.Replace("<Image Name = \"Back_Image\" X = \"0\" Y = \"0\"/>",""));var p=r.Panel;
        r.Show();
        t.Check(p.Available&&p.BackImage==null&&p.BackPng==null&&p.BackSurface==null,"no Back_Image → no animation");
        t.Equal(new Rectangle(300,200,100,190),p.Bounds,"bounds unchanged");
        t.Check(r.Looked.Count==0,"no png read");
        var withSurf=new Rig(AllFrames(),Xml.Replace("<Image Name = \"Back_Image\" X = \"0\" Y = \"0\"/>","<Image Name = \"Back_Image\" X = \"0\" Y = \"0\" Surf = \"13\"/>"));
        withSurf.Show();
        t.Check(withSurf.Panel.BackImage==null&&withSurf.Panel.Login!.Find("Back_Image") is {BaseSurface:not null},"Back_Image with a Surf stays a normal image");
    }
}
