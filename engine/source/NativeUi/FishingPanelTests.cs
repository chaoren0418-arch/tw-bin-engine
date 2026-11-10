namespace P06 {
    // T329 (#767) test seam: own actor for the fishing-ui world integration check (same pattern as RecoveryTests).
    public sealed partial class WorldSession {
        internal void SeedFishingWindowTestActor(uint id)=>Actor=new OwnActor(32600,32940,id,61,0,0,0,0,0,0,"釣客","",0,0,"","",0,100,0,1,0,0,0);
    }
}

namespace P06.NativeUi {
    // T329 (#767) fishing-ui 離線測試：只用合成版面與合成 surf（純色方塊），不讀主客戶端或參考客戶端的 FishingUI.xml、
    // 不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
    static class FishingPanelTests {
        static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

        // Synthetic layout with the element structure of the reference window (own coordinates and surf numbers).
        const string Xml="""
            <?xml version="1.0"?>
            <FishingUI>
              <Window Name="Fishing_Window" X="100" Y="50" BaseSurf = "800;1" FrameSurf = "800;1" Moveable = "10" MouseEvent = "Dummy" Top="1" AlphaWin="0">
                <Image Name = "SpeedReel_Image" X = "20" Y = "4" Surf = "801;1">
                  <Tooltip Name = "Desc_Tooltip" X="20" Y="20" String = "1" MostTop="1"/>
                </Image>
                <Label Name = "BaitTemp_Image" X = "30" Y = "4" Width = "10" Height = "8">
                  <Tooltip Name = "Desc_Tooltip" X="20" Y="20" String = "2" MostTop="1"/>
                </Label>
                <Label Name = "BaitCount_Label" X = "45" Y = "4" Width = "20" Height = "8" Caption="0" HAlign="left" VAlign="middle"/>
                <Button Name = "FishingNet_Button" X = "70" Y = "0" BaseSurf = "802;1" PushSurf = "803;1" HighLightSurf="804;1" MouseEvent = "FishingNet">
                  <Tooltip Name = "Desc_Tooltip" X="20" Y="20" String = "3" MostTop="1"/>
                </Button>
                <Button Name = "QuitFishing_Button" X = "86" Y = "2" BaseSurf = "805" PushSurf = "806" MouseEvent = "QuitFishing">
                  <Tooltip Name = "Desc_Tooltip" X="20" Y="20" String = "4" MostTop="1"/>
                </Button>
                <AniGauge Name = "ReelGuage_Image" X = "10" Y = "14" Surf = "807"/>
              </Window>
            </FishingUI>
            """;

        const uint Base=0xFF102030,Reel=0xFF00FFFF,Net=0xFF400000,NetPush=0xFFFF0000,Quit=0xFF00FF00,QuitPush=0xFF008800,Gauge=0xFFFFFF00;
        static readonly Dictionary<int,UiSurface> Surfs=new(){
            [800]=Surface(800,100,24,Base),[801]=Surface(801,8,8,Reel),[802]=Surface(802,12,12,Net),[803]=Surface(803,12,12,NetPush),
            [805]=Surface(805,10,10,Quit),[806]=Surface(806,10,10,QuitPush),[807]=Surface(807,60,6,Gauge)};   // 804 (net highlight) deliberately missing
        static UiSurface? Get(int id)=>Surfs.TryGetValue(id,out var s)?s:null;
        static UiLayout Layout(string text=Xml)=>UiLayout.Parse(text,"FishingUI.xml");
        static FishingPanel Loaded(Func<bool>? quit=null,Func<int,UiSurface?>? surf=null){var p=new FishingPanel(null,quit??(()=>false));p.Load(Layout(),surf??(Func<int,UiSurface?>)Get);return p;}
        static readonly UiStaticGameState World=new(InWorld:true,CharacterName:"釣客",Level:52);
        static FishingWindowEventArgs E(bool active,int revision,bool pending=false,uint actor=7,string notice="")=>new(actor,active,pending,notice,revision);
        static UiMouseEvent M(UiMouseKind k,int x,int y,MouseButtons b=MouseButtons.Left)=>new(k,new Point(x,y),b,1,0,Keys.None);
        static void Click(FishingPanel p,int x,int y){var c=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
        static uint Pixel(FishingPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return (uint)canvas.Bitmap.GetPixel(x,y).ToArgb();}

        [WorldModuleTest("fishing-ui")]
        static void Run(WorldModuleTestContext t){
            try{
                LoadLayout(t);
                Broken(t);
                OpenClose(t);
                MouseAndPaint(t);
                Event(t);
                WorldEvents(t);
                Registration(t);
            }finally{FishingPanel.Detach();}
        }

        static void LoadLayout(WorldModuleTestContext t){
            var p=Loaded();
            t.Check(p.Available,"synthetic layout usable",p.Unavailable);
            t.Equal(new Rectangle(100,50,100,24),p.Bounds,"window X/Y + BaseSurf size");
            t.Equal(10,p.MoveableHeight,"Moveable");
            t.Check(p.Pieces.Select(x=>x.Name).SequenceEqual(["SpeedReel_Image","FishingNet_Button","QuitFishing_Button"]),"images then buttons, XML order",p.Pieces.Select(x=>x.Name));
            var net=p.Pieces[1];
            t.Check(net.Offset==new Point(70,0)&&net.BaseSurface?.Surf==802&&net.PushSurface?.Surf==803&&net.HighlightSurface==null,"net button surfs (HighLightSurf spelling)");
            t.Equal(new Point(86,2),p.Pieces[2].Offset,"quit button position");
            t.Check(p.Missing.SequenceEqual(["surf 804 (FishingUI/Window/Button[0]/@HighLightSurf)"]),"missing surf listed",p.Missing);
            t.Check(p.Skipped.Count(s=>s.Contains("魚餌數量"))==2,"bait labels skipped (no data source)",p.Skipped);
            t.Check(p.Skipped.Any(s=>s.Contains("AniGauge")&&s.Contains("收竿量表")),"AniGauge skipped",p.Skipped);
            t.Check(p.Skipped.Count(s=>s.Contains("提示字串"))==4,"tooltips skipped",p.Skipped);
            t.Check(!p.Skipped.Any(s=>s.Contains("FrameSurf")),"FrameSurf = BaseSurf not reported");
            t.Check(!p.Visible,"closed after load");
            t.Check(FishingPanel.SurfAttr(Layout().Child("Window"),"BaseSurf")==new FishingSurf("BaseSurf",800,1),"surf;flag parsed");
            t.Check(FishingPanel.SurfAttr(Layout().Child("Window"),"Missing")==null,"absent attribute → null");
            var noPos=new FishingPanel(null,()=>false);noPos.Load(Layout(Xml.Replace("X=\"100\" Y=\"50\" ","")),Get);
            t.Equal(new Rectangle(0,0,100,24),noPos.Bounds,"missing window X/Y = 0 (reference XML uses 0,0)");
            var frame=new FishingPanel(null,()=>false);frame.Load(Layout(Xml.Replace("FrameSurf = \"800;1\"","FrameSurf = \"809\"")),Get);
            t.Check(frame.Available&&frame.Skipped.Any(s=>s.Contains("FrameSurf")),"different FrameSurf reported, not drawn");
        }

        static void Broken(WorldModuleTestContext t){
            var noBase=Loaded(surf:id=>id==800?null:Get(id));
            t.Check(!noBase.Available&&noBase.Unavailable!.Contains("BaseSurf")&&!noBase.Open(),"missing BaseSurf → unavailable, no substitute");
            t.Check(!noBase.Offer(E(true,1)),"unavailable panel ignores events");
            t.Throws<UiLayoutException>(()=>new FishingPanel().Load(Layout(Xml.Replace("\"Fishing_Window\"","\"Other\"")),Get),"wrong window name");
            t.Throws<UiLayoutException>(()=>new FishingPanel().Load(Layout(Xml.Replace("BaseSurf = \"800;1\"","BaseSurf = \"x\"")),Get),"bad surf attr");
            t.Throws<UiLayoutException>(()=>new FishingPanel().Load(Layout(Xml.Replace("PushSurf = \"806\"","PushSurf = \"806;y\"")),Get),"bad surf flag");
            t.Throws<UiLayoutException>(()=>new FishingPanel().Load(Layout("<FishingUI><Other/></FishingUI>"),Get),"no Window");
            var host=new FishingPanel();host.Load(Layout(),(SurfCatalog)null!);   // returns before touching the catalog
            t.Check(!host.Available&&host.Unavailable=="沒有 UiResources"&&!FishingPanel.IsAttached(host),"product Load without resources → unavailable, not attached, no exception");
        }

        static void OpenClose(WorldModuleTestContext t){
            var p=Loaded();
            t.Check(p.Offer(E(true,1,notice:"已開始釣魚")),"start queued");
            t.Check(!p.Visible,"opens only in Update");
            p.Update(World);
            t.Check(p.Visible&&p.State is {Active:true,Revision:1}&&p.CanQuit,"fishing start opens the window");
            p.Close();
            p.Offer(E(true,2,notice:"釣魚期間收到：測試"));p.Update(World);
            t.Check(!p.Visible&&p.State!.Revision==2,"notice while fishing does not reopen a closed window");
            p.Offer(E(false,3,notice:"釣魚已結束"));p.Update(World);
            t.Check(!p.Visible&&!p.CanQuit,"end keeps it closed");
            p.Offer(E(true,4));p.Update(World);
            t.Check(p.Visible,"next start reopens");
            p.Offer(E(true,2));p.Update(World);
            t.Equal(4,p.State!.Revision,"stale (older revision) change ignored");
            p.Offer(E(false,5));p.Offer(E(true,6));p.Update(World);
            t.Check(p.Visible&&p.State.Revision==6,"queued changes applied in order");
            p.Offer(E(false,7));p.Update(World);
            t.Check(!p.Visible,"fishing end closes the window");
            p.Offer(E(true,8));p.Update(World);
            p.Update(new UiStaticGameState(InWorld:false));
            t.Check(!p.Visible&&p.State==null,"leaving the world closes and clears");
            p.Offer(E(true,1,actor:9));p.Update(World);
            t.Check(p.Visible&&p.State!.Actor==9,"new session (revision restarts) opens again");
        }

        static void MouseAndPaint(WorldModuleTestContext t){
            int quits=0;bool accept=true;
            var p=Loaded(()=>{quits++;return accept;});
            p.Offer(E(true,1));p.Update(World);
            t.Equal(Base,Pixel(p,100+50,50+20),"background drawn");
            t.Equal(Reel,Pixel(p,100+20+1,50+4+1),"SpeedReel image drawn");
            t.Equal(Net,Pixel(p,100+70+1,50+0+1),"net button BaseSurf");
            t.Equal(Quit,Pixel(p,100+86+1,50+2+1),"quit button BaseSurf");
            t.Equal(Base,Pixel(p,100+10+1,50+14+1),"AniGauge not drawn");
            p.Mouse(M(UiMouseKind.Move,100+70+1,50+1,MouseButtons.None),new UiNullCommands());
            t.Equal(Net,Pixel(p,100+70+1,50+0+1),"missing HighLightSurf falls back to BaseSurf");
            var c=new UiNullCommands();
            p.Mouse(M(UiMouseKind.Down,100+70+1,50+1),c);
            t.Equal(NetPush,Pixel(p,100+70+1,50+0+1),"held button uses PushSurf");
            p.Mouse(M(UiMouseKind.Up,100+70+1,50+1),c);
            t.Check(p.Unhandled.Contains("FishingNet")&&quits==0,"FishingNet not wired (logged), sends nothing");
            p.Mouse(M(UiMouseKind.Down,100+86+1,50+3),c);p.Mouse(M(UiMouseKind.Move,100+5,50+20),c);p.Mouse(M(UiMouseKind.Up,100+5,50+20),c);
            t.Equal(0,quits,"release outside the button does not fire");
            accept=false;Click(p,100+86+1,50+3);
            t.Check(quits==1&&p.CanQuit&&p.Visible,"refused quit (no connected form) can be retried");
            accept=true;Click(p,100+86+1,50+3);
            t.Check(quits==2&&!p.CanQuit&&p.Visible,"quit sent once; window stays until the server confirms");
            t.Equal(QuitPush,Pixel(p,100+86+1,50+2+1),"pending quit drawn pushed");
            Click(p,100+86+1,50+3);
            t.Equal(2,quits,"duplicate quit blocked");
            p.Offer(E(true,2,pending:true));p.Update(World);
            t.Check(!p.CanQuit&&p.Visible,"server-side pending (C62 sent) keeps quit disabled");
            p.Offer(E(true,3,pending:false,notice:"收竿尚未確認"));p.Update(World);
            t.Check(p.CanQuit,"pending cleared → quit allowed again");
            var thrower=Loaded(()=>throw new InvalidOperationException("synthetic"));thrower.Offer(E(true,1));thrower.Update(World);
            Click(thrower,100+86+1,50+3);
            t.Check(thrower.CanQuit&&thrower.Visible,"throwing quit route logged, not fatal");
            t.Check(p.IsDragHandle(new Point(100+2,50+2))&&!p.IsDragHandle(new Point(100+71,50+2))&&!p.IsDragHandle(new Point(100+2,50+15)),"drag strip = Moveable, buttons excluded");
            p.MoveTo(new Point(10,10));
            t.Equal(new Rectangle(10,10,100,24),p.Bounds,"moved");
            t.Check(!p.Mouse(M(UiMouseKind.Down,500,500),c),"click outside not handled");
            IUiClosablePanel closable=p;closable.Close();
            t.Check(!p.Visible&&!p.Mouse(M(UiMouseKind.Down,15,15),c),"IUiClosablePanel.Close; closed window ignores mouse");
            t.Check(p.Open()&&p.Visible,"Open shows it again");
        }

        static void Event(WorldModuleTestContext t){
            FishingPanel.Detach();
            _=FishingPanel.Registration;   // static constructor subscribes once
            FishingWindow.Raise(E(true,1));   // no panel attached (--classic-ui): nothing happens
            var p=Loaded();p.Attach();
            t.Check(FishingPanel.IsAttached(p),"attached");
            FishingWindow.Raise(E(true,1));p.Update(World);
            t.Check(p.Visible,"opened from the event");
            var broken=Loaded(surf:id=>id==800?null:Get(id));broken.Attach();
            FishingWindow.Raise(E(true,1));broken.Update(World);
            t.Check(!broken.Visible,"unavailable panel never opens");
            FishingPanel.Detach();
            EventHandler<FishingWindowEventArgs> thrower=(_,_)=>throw new InvalidOperationException("synthetic");
            var seen=new List<FishingWindowEventArgs>();EventHandler<FishingWindowEventArgs> after=(_,e)=>seen.Add(e);
            FishingWindow.Changed+=thrower;FishingWindow.Changed+=after;
            try{FishingWindow.Raise(E(true,5));}
            finally{FishingWindow.Changed-=thrower;FishingWindow.Changed-=after;}
            t.Check(seen.Count==1&&seen[0].Revision==5,"throwing handler logged; later handlers still run");
            var e=new FishingWindowEventArgs(7,true,true,"n",3);
            t.Check(e.Actor==7&&e.Active&&e.CancelPending&&e.Notice=="n"&&e.Revision==3,"event args");
        }

        static byte[] Start(uint actor)=>LoginPackets.Pad([158,..BitConverter.GetBytes(actor),71,0x5A,0x7F,0xAC,0x80]);
        static byte[] Visual(uint actor)=>LoginPackets.Pad([119,..BitConverter.GetBytes(actor),0]);
        static byte[] Message(ushort id)=>LoginPackets.Pad([71,(byte)id,(byte)(id>>8),0]);

        static void WorldEvents(WorldModuleTestContext t){
            const uint Me=0x00112233,Other=0x00445566;
            FishingPanel.Detach();
            var seen=new List<FishingWindowEventArgs>();EventHandler<FishingWindowEventArgs> h=(_,e)=>{lock(seen)seen.Add(e);};
            FishingWindow.Changed+=h;
            try{
                var w=new WorldSession();w.SeedFishingWindowTestActor(Me);
                w.Receive(Start(Other),"");
                t.Equal(0,seen.Count,"other actor's fishing raises nothing");
                w.Receive(Start(Me),"");
                t.Check(seen.Count==1&&seen[0] is {Active:true,CancelPending:false}&&seen[0].Actor==Me&&seen[0].Notice.Contains("32602"),"S158 action 71 → open event",seen.Select(x=>x.Notice));
                w.FishingCancelSent();
                t.Check(seen.Count==2&&seen[1] is {Active:true,CancelPending:true}&&seen[1].Revision>seen[0].Revision,"C62 sent → pending event");
                w.Receive(Visual(Me),"");
                t.Check(seen.Count==3&&seen[2] is {Active:false,CancelPending:false},"S119 own → end event");
                w.Receive(Visual(Me),"");
                t.Equal(3,seen.Count,"no change → no event");
                w.Receive(Start(Me),"");w.Receive(Message(1163),"");
                t.Check(seen.Count==5&&!seen[4].Active&&seen[4].Notice.Contains("結束"),"S71 1163 → end event");
                var p=Loaded();
                foreach(var e in seen)p.Offer(e);
                p.Update(World);
                t.Check(!p.Visible&&p.State!.Revision==seen[4].Revision,"panel replays the real event sequence and ends closed");
            }finally{FishingWindow.Changed-=h;}
        }

        static void Registration(WorldModuleTestContext t){
            var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==FishingPanel.PanelId);
            t.Check(reg!=null&&reg.Layout==FishingPanel.HostLayout,"[UiPanel] discovered with host layout uistatus86");
            t.Check(reg?.Create(new UiRoot()) is FishingPanel {Layer:UiLayer.Window} and IUiClosablePanel and IUiMovablePanel,"window, closable, movable");
            t.Check(UiPanelRegistry.Errors.Count==0,"no discovery errors",UiPanelRegistry.Errors);
        }
    }
}
