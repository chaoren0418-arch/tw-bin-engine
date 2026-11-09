namespace P06.NativeUi;

// R3-I (Issue #677): native UI window stack, max 4 like sub_A57C60 (VA 0xA57C60 / FO 0x657060, dword_153D220[4]).
// Offline only: synthetic panels, no client resources.
static class WindowStackTests {
    class StackWindow(string id,Rectangle bounds):IUiPanel {
        public string Id=>id;
        public virtual UiLayer Layer=>UiLayer.Window;
        Rectangle rect=bounds;
        /// <summary>FieldMapPanel style: always Visible, closed = empty Bounds (so it still gets its toggle key).</summary>
        public bool FieldMapStyle{get;set;}
        public Rectangle Bounds{get=>FieldMapStyle&&!Open?Rectangle.Empty:rect;set=>rect=value;}
        public bool Open{get;set;}
        public bool ToggleOnF9{get;set;}
        public bool ThrowOnPaint{get;set;}
        public int Keys9;
        public bool Visible=>FieldMapStyle||Open;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){if(ThrowOnPaint)throw new InvalidOperationException("stack paint boom");}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>true;
        public bool Key(UiKeyEvent e,IUiCommands commands){
            if(!ToggleOnF9||e.KeyCode!=Keys.F9||!e.Down)return false;
            Keys9++;Open=!Open;return true;
        }
    }
    sealed class ClosableStackWindow(string id,Rectangle bounds):StackWindow(id,bounds),IUiClosablePanel {
        public int Closes;
        public void Close(){Closes++;Open=false;}
    }
    sealed class StackBar(string id):StackWindow(id,new(0,470,800,130)) {
        public override UiLayer Layer=>UiLayer.Bar;
    }

    [WorldModuleTest("native-ui-window-stack")]
    static void Run(WorldModuleTestContext t){
        t.Check(UiRoot.MaxWindows==4,"window stack holds 4 windows (dword_153D220[4])");
        static string Ids(UiRoot r)=>string.Join(",",r.WindowStack.Select(p=>p.Id));
        // Five windows at distinct spots, each with an uncovered probe point at (x+5, 15).
        StackWindow W(string id,int i)=>new(id,new(i*150,10,140,100));
        var state=new UiStaticGameState(InWorld:true);

        // --- the 5th open window pushes out the oldest (slot 3); newest is slot 0.
        {
            var ws=new[]{W("A",0),W("B",1),W("C",2),W("D",3),W("E",4)};
            var r=new UiRoot();foreach(var w in ws)r.Add(w);
            var gone=new List<string>();r.WindowEvicted+=p=>gone.Add(p.Id);
            r.Update(state);
            t.Check(r.WindowStack.Count==0,"closed windows are not in the stack");
            foreach(var w in ws.Take(4)){w.Open=true;r.Update(state);}
            t.Check(Ids(r)=="D,C,B,A"&&gone.Count==0,"4 open windows fit: [0]=newest D … [3]=oldest A");
            ws[4].Open=true;r.Update(state);
            t.Check(Ids(r)=="E,D,C,B","5th open (E) → E on top, stack E,D,C,B");
            t.Check(gone.SequenceEqual(new[]{"A"})&&r.IsEvicted(ws[0])&&!r.Usable(ws[0]),"oldest (A) pushed out exactly once and no longer usable");
            t.Check(r.HitTest(new(5,15))==null&&r.HitTest(new(155,15))==ws[1],"evicted window is not hit; the others still are");
            t.Check(!r.PaintOrder().Where(r.Usable).Contains(ws[0]),"evicted window is not painted");
            t.Check(UiResourceLog.Recent().Any(l=>l.Contains("關閉最舊的 A")),"eviction logged");
            r.Update(state);
            t.Check(gone.Count==1&&Ids(r)=="E,D,C,B","repeated sync is idempotent");

            // --- BringToFront (sub_A596B0) keeps the others' order; the next eviction takes the bottom one, not the raised one.
            r.BringToFront(ws[1]);
            t.Check(Ids(r)=="B,E,D,C","BringToFront(B): B,E,D,C (others keep their order)");
            var f=W("F",0);f.Bounds=new(0,300,140,100);r.Add(f);f.Open=true;r.Update(state);
            t.Check(Ids(r)=="F,B,E,D"&&gone.Last()=="C","6th open (F) pushes out C (bottom after the raise), not the raised B");

            // --- a window closing by itself leaves the stack; evicted ones are not brought back (original compacts the array).
            ws[3].Open=false;r.Update(state);
            t.Check(Ids(r)=="F,B,E"&&r.IsEvicted(ws[0])&&r.IsEvicted(ws[2]),"D closes → F,B,E; evicted A and C stay out");
            // --- an evicted window that closes itself can open again, on top.
            ws[0].Open=false;r.Update(state);
            t.Check(!r.IsEvicted(ws[0]),"evicted A closed by itself → no longer evicted");
            ws[0].Open=true;r.Update(state);
            t.Check(Ids(r)=="A,F,B,E"&&gone.Count==2,"A reopens on top; 4 open → nobody pushed out");
            ws[3].Open=true;r.Update(state);
            t.Check(Ids(r)=="D,A,F,B"&&gone.Last()=="E","D reopens → E (oldest) pushed out");
        }

        // --- closable windows are closed (vtable+36), not just hidden.
        {
            var c=new ClosableStackWindow("OLD",new(0,10,140,100));
            var r=new UiRoot();r.Add(c);
            var others=Enumerable.Range(1,4).Select(i=>W("N"+i,i)).ToArray();foreach(var w in others)r.Add(w);
            c.Open=true;r.Update(state);
            foreach(var w in others)w.Open=true;r.Update(state);
            t.Check(c.Closes==1&&!c.Open&&!r.IsEvicted(c)&&Ids(r)=="N4,N3,N2,N1","IUiClosablePanel: oldest closed through Close(), stack N4..N1");
            c.Open=true;r.Update(state);
            t.Check(Ids(r)=="OLD,N4,N3,N2"&&r.IsEvicted(others[0]),"reopened OLD goes on top and pushes out N1");
        }

        // --- what counts as open: Visible and non-empty Bounds (FieldMapPanel style); other layers are not limited.
        {
            var r=new UiRoot();
            var ws=Enumerable.Range(0,5).Select(i=>W("W"+i,i)).ToArray();foreach(var w in ws){w.Open=true;r.Add(w);}
            ws[4].Bounds=Rectangle.Empty;
            var bars=Enumerable.Range(0,6).Select(i=>new StackBar("BAR"+i){Open=true}).ToArray();foreach(var b in bars)r.Add(b);
            r.Update(state);
            t.Check(Ids(r)=="W3,W2,W1,W0"&&!ws.Any(r.IsEvicted),"visible window with empty Bounds is closed (does not take a slot)");
            t.Check(bars.All(r.Usable)&&r.WindowStack.All(p=>p.Layer==UiLayer.Window),"bar layer is not limited by the window stack");
            ws[4].Bounds=new(600,10,140,100);r.Update(state);
            t.Check(Ids(r)=="W4,W3,W2,W1"&&r.IsEvicted(ws[0]),"Bounds becoming non-empty opens it → W0 pushed out");
        }

        // --- first sync without Update (Compose / hit test only) uses registration order; faulted windows leave the stack.
        {
            var r=new UiRoot();
            var ws=Enumerable.Range(0,5).Select(i=>W("P"+i,i)).ToArray();foreach(var w in ws){w.Open=true;r.Add(w);}
            t.Check(r.HitTest(new(5,15))==null&&r.HitTest(new(605,15))==ws[4],"lazy sync on hit test: P0 (first registered) pushed out, P4 on top");
            ws[2].ThrowOnPaint=true;
            using(var canvas=new UiCanvas())r.Compose(canvas,null);
            t.Check(r.Faulted.Contains(ws[2])&&Ids(r)=="P4,P3,P1","faulted window leaves the stack; P0 stays out");
        }

        // --- UiInput: clicking a lower window raises it (sub_A596B0); an evicted window still gets its own toggle key last.
        {
            var ws=Enumerable.Range(0,5).Select(i=>W("K"+i,i)).ToArray();
            var r=new UiRoot();foreach(var w in ws){w.Open=true;r.Add(w);}
            ws[0].FieldMapStyle=true;
            r.Update(state);
            var input=new UiInput(r,new UiNullCommands()){Viewport=UiPresenter.Compute(new(800,600))};
            input.Mouse(UiMouseKind.Down,new(155,15),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(155,15),MouseButtons.Left);
            t.Check(Ids(r)=="K1,K4,K3,K2"&&input.Focused==ws[1],"click on K1 raises it to slot 0");
            ws[0].ToggleOnF9=true;
            t.Check(input.Key(new(Keys.F9,true,false))&&ws[0].Keys9==1&&!ws[0].Open,"evicted K0 still receives its toggle key (closes)");
            r.Update(state);t.Check(!r.IsEvicted(ws[0]),"after closing, K0 is no longer evicted");
            t.Check(input.Key(new(Keys.F9,true,false))&&ws[0].Open,"toggle again → K0 open");
            r.Update(state);
            t.Check(Ids(r)=="K0,K1,K4,K3"&&r.IsEvicted(ws[2]),"reopened K0 on top; oldest K2 pushed out");
            ws[0].ToggleOnF9=false;ws[2].ToggleOnF9=false;
            t.Check(!input.Key(new(Keys.F9,true,false))&&ws[2].Keys9==0,"evicted window that does not handle the key: key goes back to the game");
        }
    }
}
