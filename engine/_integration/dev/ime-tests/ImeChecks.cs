using P06;
using P06.NativeUi;

// f1247-ime-path (#1271): offline checks of the pure IME rules (NativeUi/UiIme.cs). The Win32 side (NativeUiIme.cs) needs a real
// IME and window; that is f1247-ime-retest (I01, I02) and is not claimed here.
partial class Checks {
    [CheckGroup("ime",Order=1),CheckGroup(CheckGroups.Default)]
    static void ImeChecks(){
        // ---- caret → composition / candidate placement (UiPresenter viewport; PerMonitorV2: client pixels are physical) ----
        var caret=new Rectangle(200,580,1,16);
        var v96=UiPresenter.Compute(new(800,600));
        var p96=UiImePlacement.Compute(caret,v96,new(800,600));
        Check(v96.Scale==1&&p96.Composition==new Point(200,580)&&p96.Exclude==new Rectangle(200,580,1,16),"ime placement DPI 96 (800×600, s=1)");
        var v192=UiPresenter.Compute(new(1600,1200));
        var p192=UiImePlacement.Compute(caret,v192,new(1600,1200));
        Check(v192.Scale==2&&p192.Composition==new Point(400,1160)&&p192.Exclude==new Rectangle(400,1160,2,32),"ime placement DPI 192 (1600×1200, s=2)");
        // Centred picture with a black border: the offset is added once.
        var vb=UiPresenter.Compute(new(1700,1300));
        var pb=UiImePlacement.Compute(caret,vb,new(1700,1300));
        Check(vb.OffsetX==50&&vb.OffsetY==50&&pb.Composition==new Point(450,1210)&&pb.Exclude.Height==32,"ime placement centred viewport offset");
        // Fractional fill scale.
        var vf=UiPresenter.Compute(new(1200,900),fill:true);
        var pf=UiImePlacement.Compute(new(100,100,1,16),vf,new(1200,900));
        Check(pf.Composition==new Point(150,150)&&pf.Exclude.Height==24,"ime placement fill scale 1.5");
        // Clamping: caret past the canvas edge, negative caret, line running off the bottom, empty client.
        var pr=UiImePlacement.Compute(new(900,700,1,16),v96,new(800,600));
        Check(pr.Composition==new Point(799,599)&&pr.Exclude.Bottom<=600&&pr.Exclude.Width>=0,"ime placement clamps past right/bottom edge");
        var pn=UiImePlacement.Compute(new(-5,-9,1,16),v96,new(800,600));
        Check(pn.Composition==Point.Empty&&pn.Exclude.Height==7,"ime placement clamps a negative caret (line clipped to the canvas)");
        var pl=UiImePlacement.Compute(new(10,595,1,16),v96,new(800,600));
        Check(pl.Composition==new Point(10,595)&&pl.Exclude.Bottom==600,"ime placement clamps line bottom to the canvas");
        var pz=UiImePlacement.Compute(caret,UiPresenter.Compute(Size.Empty),Size.Empty);
        Check(pz.Composition==Point.Empty&&pz.Exclude.Width==0&&pz.Exclude.Height==0,"ime placement empty client stays at the origin");
        var p0=UiImePlacement.Compute(new(50,50,1,0),v96,new(800,600));
        Check(p0.Exclude.Height==1,"ime placement zero-height caret still excludes one row");

        // ---- one delivery per IME result character ----
        var typed=new List<char>();bool open=true;
        bool Deliver(char c){if(!open)return false;typed.Add(c);return true;}
        var gate=new UiImeCharGate();
        // Result string 中文: WM_IME_CHAR ×2; an IME that also posts WM_CHAR echoes is dropped once each.
        Check(gate.ImeChar('中',Deliver)&&gate.ImeChar('文',Deliver),"ime WM_IME_CHAR delivered");
        Check(gate.IsEcho('中')&&gate.IsEcho('文')&&!gate.IsEcho('文'),"ime WM_CHAR echo dropped once per character");
        Check(new string(typed.ToArray())=="中文"&&gate.Delivered==2&&gate.Dropped==2,"ime one Char() per character");
        Check(Convert.ToHexString(LoginPackets.TextEncoding.GetBytes(new string(typed.ToArray())))=="A4A4A4E5","ime typed 中文 encodes as CP950 A4A4 A4E5 (board C141 vector bytes)");
        // A real key press between the IME result and a typed character: the typed character is never mistaken for an echo.
        typed.Clear();gate.ImeChar('a',Deliver);gate.KeyDown('A');
        Check(!gate.IsEcho('a'),"ime real WM_KEYDOWN clears the echo list");
        gate.ImeChar('b',Deliver);gate.KeyDown(UiImeKeys.VK_PROCESSKEY);
        Check(gate.IsEcho('b'),"ime VK_PROCESSKEY does not clear the echo list");
        Check(!gate.IsEcho('z'),"ime an unrelated WM_CHAR is never dropped");
        // Not taken (no open text input): nothing recorded, so no later WM_CHAR is swallowed.
        open=false;var g2=new UiImeCharGate();
        Check(!g2.ImeChar('中',Deliver)&&!g2.IsEcho('中')&&g2.Delivered==0,"ime unused WM_IME_CHAR leaves no echo");
        g2.Reset();Check(!g2.IsEcho('a'),"ime reset clears");

        // ---- which key messages are the IME's ----
        Check(UiImeKeys.PassToIme(UiImeKeys.WM_KEYDOWN,UiImeKeys.VK_PROCESSKEY),"ime VK_PROCESSKEY keydown goes to TranslateMessage");
        Check(UiImeKeys.PassToIme(UiImeKeys.WM_SYSKEYDOWN,UiImeKeys.VK_PROCESSKEY),"ime VK_PROCESSKEY syskeydown goes to TranslateMessage");
        Check(!UiImeKeys.PassToIme(0x0102,UiImeKeys.VK_PROCESSKEY),"ime WM_CHAR 0xE5 is not a key message");
        var gameKeys=new[]{Keys.W,Keys.A,Keys.S,Keys.D,Keys.Up,Keys.Down,Keys.Left,Keys.Right,Keys.F1,Keys.F12,Keys.Space,Keys.Enter,Keys.Escape,Keys.D1,Keys.NumPad5,Keys.M,Keys.Tab,Keys.Back};
        Check(gameKeys.All(k=>!UiImeKeys.PassToIme(UiImeKeys.WM_KEYDOWN,(nint)(int)k)&&!UiImeKeys.PassToIme(UiImeKeys.WM_SYSKEYDOWN,(nint)(int)k)),"ime game hotkeys keep the existing ProcessCmdKey path");

        // ---- session enter / leave (game focus is never touched) ----
        var s=new UiImeSession();
        Check(s.Leave(false,true,1).Count==0&&!s.Active,"ime game focus: no session, nothing changed");
        Check(s.Enter(true,false,0).Count==0&&s.Active,"ime enter with a context: nothing to fix");
        Check(s.Enter(true,true,1).Count==0,"ime enter twice is ignored");
        var left=s.Leave(true,true,1);
        Check(left.SequenceEqual([UiImeAction.CancelComposition,UiImeAction.RestoreConversion,UiImeAction.Close])&&s.ConversionBefore==0&&!s.Active,
            "ime leave: cancel composition, restore conversion, close the IME opened in the chat line");
        Check(s.Enter(true,true,1).Count==0&&s.Leave(false,true,1).Count==0,"ime leave keeps an IME the user had open before");
        Check(s.Enter(false,false,0).SequenceEqual([UiImeAction.Associate]),"ime enter without a context associates the default one");
        Check(s.Leave(false,false,0).SequenceEqual([UiImeAction.RestoreDetached]),"ime leave restores a detached context");

        // ---- target lookup ----
        Check(typeof(IUiImeCaret).IsAssignableFrom(typeof(ChatPanel))&&typeof(IUiImeCaret).IsAssignableFrom(typeof(CreateCharPanel)),"ime chat line and create-character name report a caret");
        Check(new ChatPanel().ImeCaret==null,"ime chat line outside the world has no caret");
        var a=new FakeImePanel("a",null);var b=new FakeImePanel("b",new(1,2,1,16));var c=new FakeImePanel("c",new(3,4,1,16));
        Check(UiImeTargets.Find([a,b,c],_=>true) is {Panel.Id:"b"} hit&&hit.Caret==new Rectangle(1,2,1,16),"ime target = first usable panel with a caret");
        Check(UiImeTargets.Find([a,b,c],p=>p.Id!="b") is {Panel.Id:"c"},"ime target skips unusable panels");
        Check(UiImeTargets.Find([a,new FakeImePanel("x",null,true)],_=>true)==null,"ime target: none (a throwing caret is skipped)");
    }

    sealed class FakeImePanel(string id,Rectangle? caret,bool throws=false):IUiPanel,IUiImeCaret {
        public string Id=>id;
        public UiLayer Layer=>UiLayer.Window;
        public bool Visible=>true;
        public Rectangle Bounds=>Rectangle.Empty;
        public Rectangle? ImeCaret=>throws?throw new InvalidOperationException("fault"):caret;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }
}
