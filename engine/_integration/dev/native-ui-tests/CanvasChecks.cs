using P06;
using P06.NativeUi;
using System.Drawing.Imaging;
using System.Reflection;

// C2 ui-core-canvas (Issue #463). Offline only: synthetic panels, synthetic archives under scratch/issue-463/, no real client.
partial class Checks {
    sealed class FakePanel(string id,UiLayer layer,Rectangle bounds,bool handles=true,Color? fill=null):IUiPanel {
        public string Id=>id;
        public UiLayer Layer=>layer;
        public Rectangle Bounds{get;set;}=bounds;
        public bool Visible{get;set;}=true;
        public bool Handles{get;set;}=handles;
        public bool HandlesKeys{get;set;}
        public bool ThrowOnPaint{get;set;}
        public Func<bool>? VisibleWhen{get;set;}
        public List<UiMouseEvent> Mice{get;}=[];
        public List<UiKeyEvent> KeyLog{get;}=[];
        public UiLayout? Layout;public SurfCatalog? Surfs;public IUiGameState? State;
        bool IUiPanel.Visible=>VisibleWhen?.Invoke()??Visible;
        public void Load(UiLayout layout,SurfCatalog surfs){Layout=layout;Surfs=surfs;}
        public void Update(IUiGameState state)=>State=state;
        public void Paint(UiCanvas canvas){
            if(ThrowOnPaint)throw new InvalidOperationException("paint boom");
            // Deliberately paints the whole canvas: the clip must keep it inside Bounds.
            if(fill is Color c){using var b=new SolidBrush(c);canvas.Graphics.FillRectangle(b,UiRoot.Canvas);}
        }
        public bool Mouse(UiMouseEvent e,IUiCommands commands){Mice.Add(e);return Handles;}
        public bool Key(UiKeyEvent e,IUiCommands commands){KeyLog.Add(e);return HandlesKeys;}
    }
    sealed class FakeWindow(string id,Rectangle bounds):IUiMovablePanel {
        public string Id=>id;public UiLayer Layer=>UiLayer.Window;public Rectangle Bounds{get;private set;}=bounds;public bool Visible=>true;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>true;
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
        public bool IsDragHandle(Point p)=>p.Y<Bounds.Y+20;
        public void MoveTo(Point p)=>Bounds=new(p,Bounds.Size);
    }
    /// <summary>Loads LEFT_WINDOW x/y from the layout and its first surf, like a real module would.</summary>
    sealed class LayoutPanel:IUiPanel {
        public string Id=>"LEFT_WINDOW";public UiLayer Layer=>UiLayer.Bar;public Rectangle Bounds{get;private set;}public bool Visible=>true;
        public UiSurface? Surface;
        public void Load(UiLayout layout,SurfCatalog surfs){
            var n=layout.Root.Child("LEFT_WINDOW");
            Surface=surfs.Get(n.Surfs("surfNumber")[0]);
            Bounds=new(n.Int("x"),n.Int("y"),Surface?.Width??0,Surface?.Height??0);
        }
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas)=>canvas.DrawSurface(Surface,Bounds.X,Bounds.Y);
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }
    static Point Client(UiViewport v,int lx,int ly)=>v.ToClient(new(lx,ly));

    static void NativeUiCanvasChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-463","synthetic");
        Directory.CreateDirectory(root);

        // --- §3.2 presenter: integer scale, centred.
        void Scale(int w,int h,float s,int ox,int oy,string label){
            var v=UiPresenter.Compute(new(w,h));
            Check(v.Scale==s&&v.OffsetX==ox&&v.OffsetY==oy&&v.Width==(int)(800*s)&&v.Height==(int)(600*s)&&v.Integer,$"{label}: {w}×{h} → s={v.Scale} offset=({v.OffsetX},{v.OffsetY})");
        }
        Scale(800,600,1,0,0,"acceptance 800×600");
        Scale(1600,1200,2,0,0,"acceptance 1600×1200");
        Scale(1920,1080,1,560,240,"acceptance 1920×1080");
        Scale(1700,1300,2,50,50,"integer floor 1700×1300");
        Scale(2400,1800,3,0,0,"integer 2400×1800");
        Scale(2560,1440,2,480,120,"integer 2560×1440 (height bound)");
        Scale(1599,1199,1,399,299,"just under 2× stays 1×");
        Scale(799,599,1,0,0,"smaller than minimum clamps to s=1 (unsupported, no crash)");
        {
            var f=UiPresenter.Compute(new(1920,1080),fill:true);
            Check(Math.Abs(f.Scale-1.8f)<1e-6&&f.Width==1440&&f.Height==1080&&f.OffsetX==240&&f.OffsetY==0&&!f.Integer,"optional fill 1920×1080 → s=1.8, 1440×1080 at (240,0)");
            Check(UiPresenter.Compute(new(800,600),fill:true)==UiPresenter.Compute(new(800,600)),"fill at 800×600 equals integer 1×");
            Check(f.ToLogical(new(240,0))==new Point(0,0)&&f.ToLogical(new(1679,1079))==new Point(799,599)&&f.ToLogical(new(239,10))==null&&f.ToLogical(new(1680,10))==null,"fill mapping edges and borders");
            Check(f.ToLogical(new(240+9,0))==new Point(5,0),"fill mapping floor((mx-ox)/s): 9/1.8 = 5");
        }

        // --- §3.3 mapping lx=floor((mx-ox)/s), border → null.
        {
            var v=UiPresenter.Compute(new(1920,1080));
            Check(v.ToLogical(new(560,240))==new Point(0,0)&&v.ToLogical(new(1359,839))==new Point(799,599),"1920×1080 corners map to (0,0)/(799,599)");
            Check(v.ToLogical(new(559,500))==null&&v.ToLogical(new(1360,500))==null&&v.ToLogical(new(900,239))==null&&v.ToLogical(new(900,840))==null&&v.ToLogical(new(0,0))==null,"1920×1080 black border → no logical point");
            var d=UiPresenter.Compute(new(1600,1200));
            Check(d.ToLogical(new(0,0))==new Point(0,0)&&d.ToLogical(new(1,1))==new Point(0,0)&&d.ToLogical(new(2,3))==new Point(1,1)&&d.ToLogical(new(1599,1199))==new Point(799,599),"2× mapping floors to the logical pixel");
            var o=UiPresenter.Compute(new(1700,1300));
            Check(o.ToLogical(new(50,50))==new Point(0,0)&&o.ToLogical(new(49,50))==null&&o.ToLogical(new(51+2*100,50+2*7))==new Point(100,7),"2× with offset (50,50)");
            Check(o.ToLogicalClamped(new(0,0))==new Point(0,0)&&o.ToLogicalClamped(new(5000,5000))==new Point(799,599),"clamped mapping (capture only) stays on the canvas");
            Check(o.ToClient(new(100,7))==new Point(250,64),"logical → client");
        }

        // --- presenter pixels: nearest neighbour, black border, one image.
        {
            using var canvas=new UiCanvas();canvas.Clear(Color.Black);
            var colors=new[]{Color.FromArgb(255,255,0,0),Color.FromArgb(255,0,255,0),Color.FromArgb(255,0,0,255),Color.FromArgb(255,255,255,0)};
            var probes=new[]{new Point(0,0),new Point(799,0),new Point(0,599),new Point(799,599),new Point(400,300),new Point(401,300),new Point(123,456)};
            for(int i=0;i<probes.Length;i++)canvas.Bitmap.SetPixel(probes[i].X,probes[i].Y,colors[i%colors.Length]);
            foreach(var (w,h) in new[]{(800,600),(1600,1200),(1920,1080),(1700,1300)}){
                var v=UiPresenter.Compute(new(w,h));
                using var target=new Bitmap(w,h,PixelFormat.Format32bppArgb);
                using(var g=Graphics.FromImage(target)){g.Clear(Color.Magenta);UiPresenter.Present(g,canvas.Bitmap,new(w,h),v);}
                int s=(int)v.Scale;bool ok=true;
                for(int i=0;i<probes.Length;i++){
                    var c=colors[i%colors.Length].ToArgb();var p=Client(v,probes[i].X,probes[i].Y);
                    for(int dy=0;dy<s;dy++)for(int dx=0;dx<s;dx++){bool same=target.GetPixel(p.X+dx,p.Y+dy).ToArgb()==c;if(!same)Console.Error.WriteLine($"DIAG {w}x{h} probe {probes[i]} at {p.X+dx},{p.Y+dy} got {target.GetPixel(p.X+dx,p.Y+dy)} want {colors[i%colors.Length]}");ok&=same;}
                }
                // Neighbour of a probe (not set) stays black: nearest neighbour, no smoothing bleed.
                var n=Client(v,402,300);ok&=target.GetPixel(n.X,n.Y).ToArgb()==Color.Black.ToArgb();
                Check(ok,$"{w}×{h}: every logical pixel becomes an exact {s}×{s} block");
                bool border=true;
                if(v.OffsetX>0)border&=target.GetPixel(0,h/2).ToArgb()==Color.Black.ToArgb()&&target.GetPixel(v.OffsetX-1,h/2).ToArgb()==Color.Black.ToArgb()&&target.GetPixel(v.OffsetX+v.Width,h/2).ToArgb()==Color.Black.ToArgb();
                if(v.OffsetY>0)border&=target.GetPixel(w/2,0).ToArgb()==Color.Black.ToArgb()&&target.GetPixel(w/2,v.OffsetY+v.Height).ToArgb()==Color.Black.ToArgb();
                bool anyMagenta=false;for(int y=0;y<h;y+=37)for(int x=0;x<w;x+=41)anyMagenta|=target.GetPixel(x,y).ToArgb()==Color.Magenta.ToArgb();
                Check(border&&!anyMagenta,$"{w}×{h}: border filled black, nothing of the old frame left");
            }
        }

        // --- layers, hit test from the float layer down, invisible panels ignored.
        var bar=new FakePanel("BAR",UiLayer.Bar,new(0,470,800,130),handles:false,fill:Color.Blue);
        var w1=new FakePanel("W1",UiLayer.Window,new(100,100,200,150),fill:Color.Green);
        var w2=new FakePanel("W2",UiLayer.Window,new(150,120,200,150),fill:Color.Yellow);
        var fl=new FakePanel("FLOAT",UiLayer.Float,new(160,130,20,20),fill:Color.White);
        var hidden=new FakePanel("HIDDEN",UiLayer.Float,UiRoot.Canvas,fill:Color.Red){Visible=false};
        var ui=new UiRoot();
        // Registered out of layer order on purpose.
        foreach(var p in new IUiPanel[]{fl,w1,bar,hidden,w2})ui.Add(p);
        Throws<InvalidOperationException>(()=>ui.Add(new FakePanel("W1",UiLayer.Bar,Rectangle.Empty)),"duplicate panel id rejected");
        Check(ui.PaintOrder().Select(p=>p.Id).SequenceEqual(new[]{"BAR","W1","W2","FLOAT","HIDDEN"}),"paint order: bar < windows (z-order) < float");
        Check(ui.HitTest(new(165,135))==fl,"float layer hit first");
        Check(ui.HitTest(new(155,125))==w2,"later window on top");
        Check(ui.HitTest(new(105,105))==w1,"lower window where uncovered");
        Check(ui.HitTest(new(10,500))==bar,"bar layer");
        Check(ui.HitTest(new(10,10))==null,"nothing hit → world");
        Check(ui.HitTest(new(0,469))==null&&ui.HitTest(new(0,470))==bar&&ui.HitTest(new(799,599))==bar&&ui.HitTest(new(800,599))==null,"bounds edges (Rectangle.Contains)");

        // --- compose: world inside WorldRect, every panel clipped to its Bounds.
        {
            Rectangle? worldRect=null;
            using var canvas=new UiCanvas();
            ui.Compose(canvas,(g,rect)=>{worldRect=rect;using var b=new SolidBrush(Color.FromArgb(255,90,10,10));g.FillRectangle(b,new Rectangle(-50,-50,2000,2000));});
            Color At(int x,int y)=>canvas.Bitmap.GetPixel(x,y);
            Check(worldRect==UiRoot.WorldRect&&UiRoot.WorldRect==new Rectangle(0,0,800,600),"world painter gets WorldRect = 800×600 (V5 (c) default)");
            Check(At(10,10).ToArgb()==Color.FromArgb(255,90,10,10).ToArgb()&&At(799,469).ToArgb()==Color.FromArgb(255,90,10,10).ToArgb(),"world visible outside panels");
            Check(At(10,500).ToArgb()==Color.Blue.ToArgb()&&At(799,599).ToArgb()==Color.Blue.ToArgb(),"bar painted in its bounds (covers the world)");
            Check(At(105,105).ToArgb()==Color.Green.ToArgb()&&At(155,125).ToArgb()==Color.Yellow.ToArgb()&&At(165,135).ToArgb()==Color.White.ToArgb(),"layer/z-order paint: W1 < W2 < float");
            Check(At(99,99).ToArgb()!=Color.Green.ToArgb()&&At(400,300).ToArgb()!=Color.Red.ToArgb(),"panel paint clipped to Bounds; hidden panel not painted");
            ui.BringToFront(w1);ui.Compose(canvas,null);
            Check(canvas.Bitmap.GetPixel(155,125).ToArgb()==Color.Green.ToArgb()&&ui.HitTest(new(155,125))==w1,"BringToFront changes paint and hit order");
            ui.BringToFront(w2);
        }
        // --- faulted module: logged and switched off, no crash, others still painted.
        {
            var bad=new FakePanel("BAD",UiLayer.Float,new(0,0,50,50),fill:Color.Red){ThrowOnPaint=true};
            var r=new UiRoot();r.Add(bar);r.Add(bad);
            using var canvas=new UiCanvas();r.Compose(canvas,null);
            Check(r.Faulted.Contains(bad)&&UiResourceLog.Recent().Any(l=>l.Contains("panel BAD Paint 失敗"))&&canvas.Bitmap.GetPixel(10,500).ToArgb()==Color.Blue.ToArgb(),"panel exception → that panel disabled and logged, frame still composed");
            Check(r.HitTest(new(10,10))==null,"faulted panel no longer hit");
        }

        // --- UiInput: border dropped, world fall-through, capture, focus, keys.
        {
            var cmd=new UiNullCommands();var input=new UiInput(ui,cmd){Viewport=UiPresenter.Compute(new(1920,1080))};var v=input.Viewport;
            int before=w1.Mice.Count+w2.Mice.Count+bar.Mice.Count+fl.Mice.Count;
            var r=input.Mouse(UiMouseKind.Down,new(10,10),MouseButtons.Left,1);
            Check(r.Route==UiRoute.Outside&&r.Logical==null&&w1.Mice.Count+w2.Mice.Count+bar.Mice.Count+fl.Mice.Count==before,"click on black border reaches neither UI nor world");
            r=input.Mouse(UiMouseKind.Down,new(559,500),MouseButtons.Left,1);
            Check(r.Route==UiRoute.Outside,"border pixel just left of the picture dropped");
            r=input.Mouse(UiMouseKind.Down,Client(v,10,10),MouseButtons.Left,1);
            Check(r.Route==UiRoute.World&&r.Logical==new Point(10,10),"no panel hit → world gets logical (10,10)");
            r=input.Mouse(UiMouseKind.Down,Client(v,10,500),MouseButtons.Right,1);
            Check(r.Route==UiRoute.Panel&&bar.Mice.Last().Location==new Point(10,500)&&bar.Mice.Last().Button==MouseButtons.Right&&input.Captured==null,"bar hit but unhandled: world still blocked, no capture");
            r=input.Mouse(UiMouseKind.Down,Client(v,105,105),MouseButtons.Left,2);
            Check(r.Route==UiRoute.Panel&&input.Captured==w1&&input.Focused==w1&&w1.Mice.Last().Clicks==2&&ui.HitTest(new(155,125))==w1,"handled press captures, focuses and raises the window");
            input.Mouse(UiMouseKind.Move,new(5,5));
            Check(w1.Mice.Last().Kind==UiMouseKind.Move&&w1.Mice.Last().Location==new Point(0,0),"captured move outside the picture is clamped and still goes to the captor");
            input.Mouse(UiMouseKind.Up,Client(v,700,10),MouseButtons.Left);
            Check(w1.Mice.Last().Kind==UiMouseKind.Up&&input.Captured==null,"release ends capture");
            r=input.Mouse(UiMouseKind.Move,Client(v,700,10));
            Check(r.Route==UiRoute.World&&w1.Mice.Last().Kind==UiMouseKind.Up,"after release, moves go to the world again");
            // Keys: focused first, then from the top; unhandled → game.
            w1.HandlesKeys=false;fl.HandlesKeys=false;w2.HandlesKeys=false;bar.HandlesKeys=false;
            Check(!input.Key(new(Keys.F5,true,false))&&w1.KeyLog.Count==1&&fl.KeyLog.Count==1,"unhandled key offered to focus then others, then returned to the game");
            bar.HandlesKeys=true;
            Check(input.Key(new(Keys.F5,true,false))&&bar.KeyLog.Last().KeyCode==Keys.F5,"a panel may take a key");
            w1.HandlesKeys=true;int barKeys=bar.KeyLog.Count;
            Check(input.Key(new(Keys.Control|Keys.M,true,false))&&w1.KeyLog.Last().Modifiers==Keys.Control&&bar.KeyLog.Count==barKeys,"focused panel gets the key first");
            input.Mouse(UiMouseKind.Down,Client(v,10,10),MouseButtons.Left,1);
            Check(input.Focused==null,"clicking the world clears UI focus");
            w1.HandlesKeys=bar.HandlesKeys=false;
            // Hidden panel never receives input even though it covers the canvas.
            Check(hidden.Mice.Count==0&&hidden.KeyLog.Count==0,"invisible panel gets no input");
            ui.BringToFront(w2);
        }
        // --- drag: logical movement, window kept entirely inside 800×600.
        {
            var win=new FakeWindow("BOOK",new(300,200,200,150));var r=new UiRoot();r.Add(win);
            var input=new UiInput(r,new UiNullCommands()){Viewport=UiPresenter.Compute(new(800,600))};
            input.Mouse(UiMouseKind.Down,new(310,205),MouseButtons.Left,1);
            Check(input.Dragging,"press on the drag handle starts a drag");
            input.Mouse(UiMouseKind.Move,new(320,215));
            Check(win.Bounds.Location==new Point(310,210),"drag moves by the logical delta");
            input.Mouse(UiMouseKind.Move,new(5000,5000));
            Check(win.Bounds.Location==new Point(600,450),"drag past bottom-right keeps the window inside (600,450)");
            input.Mouse(UiMouseKind.Move,new(-5000,-5000));
            Check(win.Bounds.Location==new Point(0,0),"drag past top-left keeps the window inside (0,0)");
            input.Mouse(UiMouseKind.Up,new(0,0),MouseButtons.Left);
            Check(!input.Dragging&&win.Bounds.Location==new Point(0,0),"release ends the drag");
            input.Mouse(UiMouseKind.Down,new(10,100),MouseButtons.Left,1);
            Check(!input.Dragging&&input.Captured==win,"press outside the handle is a normal (captured) click");
            input.Mouse(UiMouseKind.Up,new(10,100),MouseButtons.Left);
            // 2×: 20 client pixels = 10 logical pixels.
            input.Viewport=UiPresenter.Compute(new(1600,1200));
            input.Mouse(UiMouseKind.Down,new(20,20),MouseButtons.Left,1);input.Mouse(UiMouseKind.Move,new(40,60));input.Mouse(UiMouseKind.Up,new(40,60),MouseButtons.Left);
            Check(win.Bounds.Location==new Point(10,20),"2× drag moves in logical pixels");
            Check(UiInput.ClampInside(new(10,10),new(900,700))==new Point(0,0),"window larger than the canvas pins to (0,0)");
            input.Mouse(UiMouseKind.Down,new(40,60),MouseButtons.Left,1);Check(input.Dragging,"second drag started");input.Reset();
            Check(!input.Dragging&&input.Captured==null,"Reset (focus loss) drops drag and capture");
        }

        // --- BAR_485 slot group (V5 (e) order; one shown at a time).
        {
            var r=new UiRoot();var g=r.SlotGroup(UiSlotGroup.Bar485);
            Check(ReferenceEquals(g,r.SlotGroup("BAR_485"))&&UiSlotGroup.Bar485Location==new Point(485,475),"BAR_485 group shared by name, at (485,475)");
            g.Add("PARTY_PET_RT_WINDOW");g.Add("MINI_MAP_WINDOW");g.Add("EX_MACRO_WINDOW");
            Check(g.Members.SequenceEqual(new[]{"MINI_MAP_WINDOW","EX_MACRO_WINDOW","PARTY_PET_RT_WINDOW"}),"members kept in V5 mode order regardless of registration order");
            g.Reset();Check(g.Current=="MINI_MAP_WINDOW"&&g.IsShown("MINI_MAP_WINDOW")&&!g.IsShown("EX_MACRO_WINDOW"),"reset → mode 0 (minimap)");
            Check(g.Cycle()=="EX_MACRO_WINDOW"&&g.Cycle()=="PARTY_PET_RT_WINDOW"&&g.Cycle()=="MINI_MAP_WINDOW","cycle 0→1→2→0 (Alt+F3 order, missing members skipped)");
            int changes=0;g.Changed+=_=>changes++;g.Show("PARTY_PET_RT_WINDOW");g.Show("PARTY_PET_RT_WINDOW");
            Check(changes==1,"Changed fires once per actual switch");
            Throws<InvalidOperationException>(()=>g.Add("MINI_MAP_WINDOW"),"duplicate member rejected");
            Throws<InvalidOperationException>(()=>g.Show("NOPE"),"unknown member rejected");
            var a=new FakePanel("MINI_MAP_WINDOW",UiLayer.Bar,new(485,475,150,125)){VisibleWhen=()=>g.IsShown("MINI_MAP_WINDOW")};
            var b=new FakePanel("PARTY_PET_RT_WINDOW",UiLayer.Bar,new(485,475,150,125)){VisibleWhen=()=>g.IsShown("PARTY_PET_RT_WINDOW")};
            r.Add(a);r.Add(b);
            Check(r.HitTest(new(500,500))==b,"only the shown member is hit");
            g.Show("MINI_MAP_WINDOW");Check(r.HitTest(new(500,500))==a,"switching the slot switches the hit panel");
            Check(new UiSlotGroup("X").Cycle()==null,"empty group cycle → null");
        }

        // --- registry contract.
        Check(UiPanelRegistry.Panels.Select(p=>p.Id).Distinct().Count()==UiPanelRegistry.Panels.Count&&UiPanelRegistry.Panels.All(p=>p.Layout.Length>0&&p.Id.Length>0),"registry ids unique, every entry names its XML");

        // --- switch: default off; settings file / --native-ui turn it on.
        {
            string dir=Path.Combine(root,"options");Directory.CreateDirectory(dir);string file=Path.Combine(dir,UiOptions.SettingsFileName);
            File.Delete(file);
            Check(UiOptions.Resolve(false,file)==UiOptions.Off&&!UiOptions.Resolve(false,null).Enabled,"default off (no settings file)");
            File.WriteAllText(file,"{\"nativeUi\":true}");var o=UiOptions.Resolve(false,file);
            Check(o.Enabled&&!o.Fill&&o.Source=="settings","settings nativeUi=true → on, fill off by default");
            File.WriteAllText(file,"{\"nativeUi\":true,\"nativeUiFill\":true}");Check(UiOptions.Resolve(false,file).Fill,"settings nativeUiFill=true → fill");
            File.WriteAllText(file,"{\"nativeUi\":\"yes\"}");Check(!UiOptions.Resolve(false,file).Enabled,"non-boolean value counts as off");
            File.WriteAllText(file,"{nativeUi:");Check(!UiOptions.Resolve(false,file).Enabled,"malformed settings counts as off");
            File.WriteAllText(file,"{\"nativeUi\":false}");var a=UiOptions.Resolve(true,file);
            Check(a.Enabled&&a.Source=="argument","--native-ui turns it on regardless of the file");
            bool saved=UiOptions.CommandLine;
            try{
                UiOptions.CommandLine=false;
                Check(UiOptions.Strip(["--normal-product"]).SequenceEqual(new[]{"--normal-product"})&&!UiOptions.CommandLine,"Strip leaves other arguments alone");
                Check(UiOptions.Strip(["--native-ui"]).Length==0&&UiOptions.CommandLine,"Strip removes --native-ui (normal entry sees no arguments) and records it");
                Check(ProductEntry.Normal(UiOptions.Strip(["--native-ui"])),"--native-ui alone still opens the normal product");
            }finally{UiOptions.CommandLine=saved;}
            File.Delete(file);
        }

        // --- switch off: the ClientForm branches are inert (legacy behaviour unchanged).
        {
            const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var form=(ClientForm)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));GC.SuppressFinalize(form);
            Check((UiStartState)typeof(ClientForm).GetProperty("NativeUiState",F)!.GetValue(form)! ==UiStartState.Off,"form default: native UI off");
            var e=new MouseEventArgs(MouseButtons.Left,1,12,34,0);
            Check(ReferenceEquals(typeof(ClientForm).GetMethod("NativeUiMouse",F)!.Invoke(form,[e,UiMouseKind.Down]),e),"switch off: mouse event passes through untouched (client coordinates)");
            Check((bool)typeof(ClientForm).GetMethod("NativeUiKey",F)!.Invoke(form,[Keys.F5,true,false])! ==false,"switch off: no key is taken by the native UI");
            typeof(ClientForm).GetField("nativeUiState",F)!.SetValue(form,UiStartState.Active);
            Check((Rectangle)typeof(ClientForm).GetProperty("PlayerSceneBounds",F)!.GetValue(form)! ==UiRoot.WorldRect,"switch on and active: world scene bounds = WorldRect");
            typeof(ClientForm).GetField("nativeUiState",F)!.SetValue(form,UiStartState.Failed);
            Check(!(bool)typeof(ClientForm).GetProperty("NativeUiActive",F)!.GetValue(form)!,"failed start is not active (legacy HUD path)");
        }

        // --- Build from synthetic, pinned resources; pin mismatch → whole native UI refused (caller falls back to the legacy HUD).
        {
            string dir=Path.Combine(root,"build");if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
            string surfList="<?xml version=\"1.0\"?><SurfList><surf id=\"3168\" file=\"3168\"/><surf id=\"42\" file=\"42\"/></SurfList>";
            string layout="<?xml version=\"1.0\"?><Status><LEFT_WINDOW x = \"0\" y=\"475\" surfNumber=\"3168~3169\"/></Status>";
            var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)),("uistatus86-c.xml",Xml(layout)));
            var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
            items[1+SurfCatalog.NameHash("3168c.img")].Add(("3168c.img",Img(4,3,0,0,0x7C00)));
            for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));   // every archive must have a readable entry
            var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
            UiRoot built;
            using(var res=new UiResources(dir,tilePin,spritePins)){
                var regs=new[]{new UiPanelRegistration("LEFT_WINDOW","uistatus86",_=>new LayoutPanel())};
                built=UiRoot.Build(res,regs);
                var panel=(LayoutPanel)built.Find("LEFT_WINDOW")!;
                Check(panel.Bounds==new Rectangle(0,475,4,3)&&panel.Surface!.FileName=="3168c.img","Build: panel loaded from its XML (x/y) and first surf of the range");
                using var canvas=new UiCanvas();built.Compose(canvas,null);
                Check(canvas.Bitmap.GetPixel(0,475).ToArgb()==Color.FromArgb(255,255,0,0).ToArgb()&&canvas.Bitmap.GetPixel(3,477).ToArgb()==Color.FromArgb(255,255,0,0).ToArgb()&&canvas.Bitmap.GetPixel(4,475).ToArgb()==Color.Black.ToArgb(),"surface drawn 1:1 at its logical position");
                Throws<InvalidOperationException>(()=>UiRoot.Build(res,[new UiPanelRegistration("OTHER","uistatus86",_=>new LayoutPanel())]),"registration id must match the panel id");
                Throws<UiResourceException>(()=>UiRoot.Build(res,[new UiPanelRegistration("LEFT_WINDOW","missing",_=>new LayoutPanel())]),"missing layout refuses the whole native UI");
            }
            {
                using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.DrawSurface(null,0,0);
                Check(canvas.Bitmap.GetPixel(0,0).ToArgb()==Color.Black.ToArgb(),"null surface draws nothing (no placeholder)");
            }
            const int k=6;   // Sprite05: any archive works, each carries a readable pad entry
            var badPak=spritePins.Select((p,i)=>i==k?p with{PakSha256=new string('f',64)}:p).ToArray();
            using(var res=new UiResources(dir,tilePin,badPak))Throws<UiResourceException>(()=>UiRoot.Build(res,[]),"Sprite pak pin mismatch refuses the native UI even before any surf is used","與鎖定版本不符");
            var badIdx=spritePins.Select((p,i)=>i==16?p with{IndexSha256=new string('e',64)}:p).ToArray();
            using(var res=new UiResources(dir,tilePin,badIdx))Throws<UiResourceException>(()=>UiRoot.VerifyResources(res),"Sprite idx pin mismatch refused");
            using(var res=new UiResources(dir,tilePin with{PakSha256=new string('d',64)},spritePins))Throws<UiResourceException>(()=>UiRoot.VerifyResources(res),"Tile pak pin mismatch refused");
            File.Delete(Path.Combine(dir,"Sprite09.idx"));
            using(var res=new UiResources(dir,tilePin,spritePins))Throws<UiResourceException>(()=>UiRoot.VerifyResources(res),"missing Sprite idx refused","缺少 idx");
            Check(UiRoot.PinMismatchMessage=="原客戶端 UI 資源與鎖定版本不符","fallback message text (§2.1)");
        }

        // --- text: one place, 1-bit grid fit, drawn on the logical canvas.
        {
            using var canvas=new UiCanvas();canvas.Clear(Color.Black);
            canvas.Restrict(new(10,10,100,20));canvas.DrawText("天堂 Lineage 123",10,10,Color.White,Color.Black);canvas.Release();
            int lit=0;for(int y=0;y<600;y++)for(int x=0;x<800;x+=1){var c=canvas.Bitmap.GetPixel(x,y);if(c.R>0&&!(x>=10&&x<110&&y>=10&&y<30))throw new Exception($"text pixel outside clip at {x},{y}");if(c.R>0)lit++;}
            Check(lit>0&&UiText.Hint==System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit&&UiText.Measure(canvas.Graphics,"天堂").Width>0,"UiText draws inside the clip with 1-bit grid fit");
            bool binary=true;for(int y=10;y<30;y++)for(int x=10;x<110;x++){var c=canvas.Bitmap.GetPixel(x,y);binary&=(c.R==0||c.R==255)&&c.R==c.G&&c.G==c.B;}
            Check(binary,"text pixels are not anti-aliased (only white/black)");
        }
    }
}
