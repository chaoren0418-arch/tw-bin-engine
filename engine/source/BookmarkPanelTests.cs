using P06.NativeUi;
namespace P06;

// R4-J（T301／#713）bookmark-window-panel 離線測試：XML 與 .img 全部由本檔合成（元素名稱照 BookmarkUI.xml 的結構，
// 座標取 ui-layout.md 第 6 節記載的值，視窗原點改成 (10,20) 以檢查偏移），不讀真實客戶端、不啟動遊戲、不連網路。
static class BookmarkPanelTests {
    sealed class Host:IBookmarkWindowHost {
        public BookmarkView Bookmarks{get;set;}=View(1,1,"已收到伺服器書籤清單。");
        public TeleportDestinationRequest? Destination{get;set;}
        public bool Sending{get;set;}
        public readonly List<string> Calls=[];
        public void Teleport(TeleportDestinationRequest shown,uint bookmarkId)=>Calls.Add($"teleport:{shown.Revision}:{bookmarkId}");
        public void CancelTeleport(TeleportDestinationRequest shown)=>Calls.Add($"cancel:{shown.Revision}");
    }
    static Bookmark[] Rows(int n,params uint[] pending)=>Enumerable.Range(1,n).Select(i=>new Bookmark((uint)(100+i),"B"+i,4,(ushort)(32000+i),(ushort)(32000+i),pending.Contains((uint)(100+i)))).ToArray();
    static BookmarkView View(int session,int revision,string notice,Bookmark[]? items=null,uint actor=7)=>new(session,1,revision,actor,true,50,notice,items??Rows(3));

    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Frame=0xFF808080,Base=0xFF202020,Exit=0xFFFF0000,Row=0xFF0000FF,Ok=0xFF00FF00,OkOff=0xFF404000,Cancel=0xFFFF00FF,Thumb=0xFF00FFFF,Bar=0xFF603030,Arrow=0xFFFFFF00;
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [1]=Surface(1,263,420,Frame),[2]=Surface(2,251,407,Base),[3]=Surface(3,12,12,Exit),[4]=Surface(4,12,12,Exit),[7]=Surface(7,50,12,Base),
        [8]=Surface(8,200,17,Row),[9]=Surface(9,12,180,Bar),[10]=Surface(10,12,14,Arrow),[12]=Surface(12,12,14,Arrow),[14]=Surface(14,12,10,Thumb),
        [16]=Surface(16,60,18,Ok),[19]=Surface(19,60,18,OkOff),[20]=Surface(20,60,18,Cancel),
    };
    static string Layout(string? drop=null){
        var rows=string.Concat(Enumerable.Range(0,12).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" Caption=\"temp{i}\" PushSurf=\"8;4\" Activate=\"0\" Height=\"17\" MouseEvent=\"MemoryPlaceListMouse\"/>"));
        string xml="<?xml version=\"1.0\"?><BookmarkWindowUI Toggle=\"true\">"+
            "<Window Name=\"Bookmark_Window\" X=\"10\" Y=\"20\" FrameSurf=\"1;1\" Moveable=\"20\" KeyboardEvent=\"BookmarkKeyBoard\" MouseEvent=\"Dummy\">"+
            "<Image Name=\"Base_Image\" Surf=\"2;0\" X=\"6\" Y=\"9\"/>"+
            "<Button Name=\"Exit_Button\" X=\"249\" Y=\"15\" BaseSurf=\"3\" PushSurf=\"4\" DisableSurf=\"5\" MouseEvent=\"CloseWindow\"/>"+
            "<Image Name=\"QuickMoveListTitle_Image\" Surf=\"6;1\" X=\"24\" Y=\"46\"/><Image Name=\"MemoryZoneListTitle_Image\" Surf=\"7;1\" X=\"24\" Y=\"145\"/>"+
            "<MovableList Name=\"MemoryPlace_List\" X=\"22\" Y=\"162\" Width=\"200\" Height=\"204\" Vertical=\"1\">"+rows+"</MovableList>"+
            "<WheelControl Name=\"MemoryPlaceList_WheelArea\" X=\"23\" Y=\"163\" Width=\"200\" Height=\"205\">"+
            "<ScrollBar Name=\"MemoryPlaceList_Scrollbar\" X=\"218\" Y=\"-5\" MaxValue=\"9\" ChangeValue=\"1\" ScrollStart=\"16\" ScrollHeight=\"182\" Vertical=\"1\" MouseEvent=\"Dummy\">"+
            "<Image Name=\"Background_Image\" Surf=\"9;1\" X=\"0\" Y=\"14\"/>"+
            "<Button Name=\"Up_Button\" X=\"0\" Y=\"0\" BaseSurf=\"10;1\" PushSurf=\"11;1\"/><Button Name=\"Down_Button\" X=\"0\" Y=\"196\" BaseSurf=\"12;1\" PushSurf=\"13;1\"/>"+
            "<Button Name=\"Move_Button\" X=\"0\" Y=\"16\" BaseSurf=\"14;1\" PushSurf=\"15;1\"/></ScrollBar></WheelControl>"+
            "<Button Name=\"Confirm_Button\" BaseSurf=\"16\" PushSurf=\"17;1\" HighlightSurf=\"18\" DisableSurf=\"19\" X=\"67\" Y=\"395\" MouseEvent=\"Confirm\"/>"+
            "<Button Name=\"Cancel_Button\" BaseSurf=\"20\" PushSurf=\"21;1\" HighlightSurf=\"22\" DisableSurf=\"23\" X=\"134\" Y=\"395\" MouseEvent=\"Cancel\"/>"+
            "<Label Name=\"BookmarkCount_Label\" X=\"200\" Y=\"397\" Width=\"20\" Height=\"15\" Caption=\"30/60\"/>"+
            "</Window></BookmarkWindowUI>";
        return drop==null?xml:xml.Replace($"Name=\"{drop}\"",$"Name=\"{drop}_x\"");
    }
    static BookmarkWindowPanel Panel(Host? host,Dictionary<int,UiSurface>? surfs=null){
        var map=surfs??Surfaces();var p=new BookmarkWindowPanel();
        p.Load(UiLayout.Parse(Layout(),"BookmarkUI.xml"),id=>map.GetValueOrDefault(id));
        if(host!=null)p.Attach(host);
        return p;
    }
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true),OutOfWorld=new UiStaticGameState(InWorld:false);
    static UiMouseEvent M(UiMouseKind k,Point at,int delta=0)=>new(k,at,MouseButtons.Left,1,delta,Keys.None);
    static void Click(BookmarkWindowPanel p,Rectangle r,UiNullCommands c){var at=Center(r);p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static UiKeyEvent K(Keys k)=>new(k,true,false);
    static int Pixel(BookmarkWindowPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("bookmark-window-panel")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊與介面
        t.Check(UiPanelRegistry.Panels.Count(r=>r.Id==BookmarkWindowPanel.PanelId&&r.Layout==BookmarkWindowPanel.LayoutName)==1,"Bookmark_Window registered once (BookmarkUI.xml)");
        t.Check(typeof(IUiClosablePanel).IsAssignableFrom(typeof(BookmarkWindowPanel))&&typeof(IUiMovablePanel).IsAssignableFrom(typeof(BookmarkWindowPanel)),"closable (4-window stack) and movable");

        // 2. 版面（UI§6 座標＋視窗原點 (10,20)）
        var host=new Host();var p=Panel(host);var cmd=new UiNullCommands();
        t.Check(p.Usable&&!p.Visible&&p.Bounds.IsEmpty,"usable, hidden until opened");
        p.Update(InWorld);
        t.Check(!p.Visible,"first bookmark list does not open the window");
        p.Toggle();
        t.Equal(new Rectangle(10,20,263,420),p.Bounds,"bounds = FrameSurf 4313 size at the window origin");
        t.Equal(new Rectangle(259,35,12,12),p.ExitRect,"Exit_Button (249,15)");
        t.Equal(new Rectangle(77,415,60,18),p.ConfirmRect,"Confirm_Button (67,395)");
        t.Equal(new Rectangle(144,415,60,18),p.CancelRect,"Cancel_Button (134,395)");
        t.Equal(12,p.RowCount,"12 rows (MemoryPlace_List CheckButton)");
        t.Equal(new Rectangle(32,182,200,17),p.RowRect(0),"row 0 at MemoryPlace_List (22,162), height 17");
        t.Equal(new Rectangle(32,369,200,17),p.RowRect(11),"row 11");
        t.Equal(new Rectangle(251,178,12,14),p.UpRect,"scroll up = WheelControl (23,163) + ScrollBar (218,-5)");
        t.Equal(new Rectangle(251,374,12,14),p.DownRect,"scroll down at bar + (0,196)");
        t.Equal("3/50",p.CountText,"count label n/capacity");
        t.Equal(Argb(Frame),Pixel(p,12,22),"frame 4313 drawn at origin");
        t.Equal(Argb(Base),Pixel(p,20,40),"base 4312 drawn at (6,9)");
        t.Equal(Argb(Exit),Pixel(p,262,38),"exit button drawn");
        t.Equal(Argb(Ok),Pixel(p,80,418),"confirm enabled in normal mode");
        t.Equal(Argb(Thumb),Pixel(p,253,200),"thumb at ScrollStart when not scrolled");
        Click(p,p.ExitRect,cmd);
        t.Check(!p.Visible&&cmd.Calls.SequenceEqual(["game-focus"])&&host.Calls.Count==0,"exit closes (normal mode, nothing sent)",cmd.Calls);

        // 3. op 2（3.81 S92）單筆書籤收包 → 開窗；整份清單、換角色時不開
        host.Bookmarks=View(1,2,BookmarkWindowPanel.SingleBookmarkNotice,Rows(4));p.Update(InWorld);
        t.Check(p.Visible&&!p.DestinationMode,"single bookmark (S92) opens the window");
        Click(p,p.ConfirmRect,cmd);
        t.Check(!p.Visible,"confirm in normal mode closes");
        host.Bookmarks=View(1,3,"已收到伺服器書籤清單。",Rows(4));p.Update(InWorld);
        t.Check(!p.Visible,"full list does not open");
        host.Bookmarks=View(2,1,BookmarkWindowPanel.SingleBookmarkNotice,Rows(4));p.Update(InWorld);
        t.Check(!p.Visible,"new session: first sighting does not open");
        host.Bookmarks=View(2,1,BookmarkWindowPanel.SingleBookmarkNotice,Rows(4));p.Update(InWorld);
        t.Check(!p.Visible,"same revision does not open");
        var s=new WorldSession();
        s.Receive(LoginPackets.Pad([92,(byte)'A',0,4,0,0x10,0x7D,0x20,0x7D,0x34,0x12,0,0]),"");
        t.Check(s.Bookmarks.Notice==BookmarkWindowPanel.SingleBookmarkNotice&&s.Bookmarks.TryGet(0x1234) is {Name:"A",MapId:4},"S92 sets the single-bookmark notice the panel watches",s.Bookmarks.Notice);

        // 4. 選目的地模式：新請求開窗、未選不送、選後送一次、請求結束關窗
        cmd.Calls.Clear();host.Calls.Clear();
        host.Bookmarks=View(2,2,"x",Rows(3,102));
        host.Destination=new(5,5);p.Update(InWorld);
        t.Check(p.Visible&&p.DestinationMode&&p.ShownDestination?.Revision==5,"destination request opens the window");
        t.Check(!p.ConfirmEnabled,"confirm disabled without a selection");
        t.Equal(Argb(OkOff),Pixel(p,80,418),"disabled confirm draws DisableSurf");
        Click(p,p.ConfirmRect,cmd);
        t.Check(host.Calls.Count==0,"confirm without selection sends nothing");
        Click(p,p.RowRect(1),cmd);
        t.Check(p.Selected==102&&!p.ConfirmEnabled,"delete-pending bookmark can be selected but not used");
        Click(p,p.RowRect(0),cmd);
        t.Check(p.Selected==101&&p.ConfirmEnabled,"row 0 selected");
        t.Equal(Argb(Row),Pixel(p,200,190),"selected row draws PushSurf 4336");
        Click(p,p.ConfirmRect,cmd);
        t.Check(host.Calls.SequenceEqual(["teleport:5:101"])&&p.Visible,"confirm teleports once with the bookmark id; window stays until the request ends",host.Calls);
        host.Sending=true;
        t.Check(!p.ConfirmEnabled,"sending disables confirm");
        host.Sending=false;host.Destination=null;p.Update(InWorld);
        t.Check(!p.Visible,"request ended (sent) closes the window");

        // 5. ESC／取消／堆疊擠出＝取消請求；同一請求不重開
        host.Calls.Clear();
        host.Destination=new(6,5);p.Update(InWorld);
        t.Check(p.Key(K(Keys.Escape),cmd)&&!p.Visible&&host.Calls.SequenceEqual(["cancel:6"]),"ESC cancels the request (sub_BDAA10)",host.Calls);
        p.Update(InWorld);
        t.Check(!p.Visible,"cancelled request stays closed");
        t.Check(!p.Key(K(Keys.Escape),cmd),"ESC not taken while closed");
        host.Destination=new(7,69);p.Update(InWorld);
        t.Check(p.Visible&&p.DestinationMode,"next request reopens");
        Click(p,p.CancelRect,cmd);
        t.Check(!p.Visible&&host.Calls.Last()=="cancel:7","cancel button cancels",host.Calls);
        host.Destination=new(8,5);p.Update(InWorld);
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&host.Calls.Last()=="cancel:8","IUiClosablePanel.Close cancels",host.Calls);
        host.Destination=new(9,5);p.Update(InWorld);
        Click(p,p.ExitRect,cmd);
        t.Check(!p.Visible&&host.Calls.Last()=="cancel:9","exit button cancels",host.Calls);
        host.Destination=new(10,5);p.Update(InWorld);
        p.Update(OutOfWorld);
        t.Check(!p.Visible&&host.Calls.Last()=="cancel:10","leaving the world closes",host.Calls);
        host.Destination=null;

        // 6. 捲動：15 筆 → 最多捲 3；上下鈕、滾輪、方向鍵
        var q=Panel(host=new Host{Bookmarks=View(1,1,"x",Rows(15))});
        q.Update(InWorld);q.Toggle();q.Update(InWorld);
        t.Equal(3,q.MaxScroll,"15 rows, 12 visible → max scroll 3");
        for(int i=0;i<4;i++)Click(q,q.DownRect,cmd);
        t.Equal(3,q.Scroll,"down button clamps at max");
        t.Equal(Argb(Thumb),Pixel(q,253,20+158+16+172+1),"thumb at the bottom when fully scrolled");
        q.Mouse(M(UiMouseKind.Wheel,Center(q.ListRect),120),cmd);
        t.Equal(2,q.Scroll,"wheel up scrolls back one");
        Click(q,q.RowRect(0),cmd);
        t.Check(q.Selected==103,"row click selects the scrolled row",q.Selected);
        Click(q,q.UpRect,cmd);Click(q,q.UpRect,cmd);
        t.Equal(0,q.Scroll,"up button");
        for(int i=0;i<12;i++)q.Key(K(Keys.Down),cmd);
        t.Check(q.Selected==115&&q.Scroll==3,"down key moves selection and keeps it visible",(q.Selected,q.Scroll));
        q.Key(K(Keys.Up),cmd);
        t.Check(q.Selected==114,"up key");
        host.Bookmarks=View(1,2,"x",Rows(5));q.Update(InWorld);
        t.Check(q.Selected==null&&q.Scroll==0,"selected bookmark gone → selection cleared, scroll clamped",(q.Selected,q.Scroll));

        // 7. 拖曳：標題列 20 px，按鈕除外
        t.Check(q.IsDragHandle(new(100,25))&&!q.IsDragHandle(new(100,45))&&!q.IsDragHandle(Center(q.ExitRect)),"drag strip = top 20 px (Moveable), not on buttons");
        q.MoveTo(new(100,50));
        t.Equal(new Rectangle(100,50,263,420),q.Bounds,"moved");
        t.Equal(new Rectangle(122,212,200,17),q.RowRect(0),"rows follow the window");

        // 8. 缺圖與缺節點
        var noRow=Surfaces();noRow.Remove(8);
        var r=Panel(new Host(),noRow);r.Toggle();
        t.Check(!r.Usable&&!r.Visible,"missing row surf → panel disabled");
        var noThumb=Surfaces();noThumb.Remove(14);
        t.Check(Panel(new Host(),noThumb).Usable,"missing scroll thumb only skips that part");
        t.Throws<UiLayoutException>(()=>new BookmarkWindowPanel().Load(UiLayout.Parse(Layout("Confirm_Button"),"BookmarkUI.xml"),_=>null),"missing Confirm_Button rejected (no invented layout)");
        t.Throws<UiLayoutException>(()=>new BookmarkWindowPanel().Load(UiLayout.Parse(Layout("MemoryPlace_List"),"BookmarkUI.xml"),_=>null),"missing MemoryPlace_List rejected");
        var noHost=Panel(null);noHost.Toggle();noHost.Update(InWorld);
        t.Check(!noHost.Visible,"no host → closed");
    }
}
