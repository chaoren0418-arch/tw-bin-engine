namespace P06.NativeUi;

// R8-D（T359／#823）action-window 離線測試：只用合成版面、合成 Actions 清單與合成 surf／圖示（純色方塊），
// 不讀主客戶端的 ActionUI-c.xml／Actions.xml、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class ActionWindowTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    // 與本機 ActionUI-c.xml 相同的元素結構（座標、surf 編號是自己的）。
    const string Ui="""
        <?xml version="1.0"?>
        <ActionUI Toggle="true">
          <Button Name="ActionUI_Button" X="774" Y="470" MostTop="1" BaseSurf="960;1" HighlightSurf="961;1" MouseEvent="OpenButtonPressed">
            <Tooltip Name="Desc_Tooltip" String="77"/>
          </Button>
          <Window Name="Action_Window" BaseSurf="900;1" FrameSurf="901;1" Moveable="10" KeyboardEvent="KeyboardEvent" MouseEvent="Dummy">
            <ActionIconGrid Name="Grid" X="5" Y="25" Row="2" Column="3" UnitGap="1" UnitWidth="10" UnitHeight="10" Drag="1" MouseEvent="GridEvent">
              <Image Name="Selection_Image" Surf="950;1"/>
              <ScrollBar Name="ScrollBar" X="40" Y="0" Width="6" Height="26" ScrollStart="5" ScrollHeight="30" Vertical="1" MaxValue="8">
                <Image Name="Background_Image" X="0" Y="6" Surf="915;1"/>
                <Button Name="Up_Button" X="0" Y="0" BaseSurf="910;1" PushSurf="911;1" MouseEvent="Dummy"/>
                <Button Name="Down_Button" X="0" Y="20" BaseSurf="912;1" PushSurf="913;1" MouseEvent="Dummy"/>
                <Button Name="Move_Button" X="0" Y="6" BaseSurf="914;1" PushSurf="914;1" MouseEvent="Dummy"/>
              </ScrollBar>
            </ActionIconGrid>
            <Button Name="Exit_Button" X="90" Y="2" BaseSurf="920" PushSurf="921" MouseEvent="ExitButtonPressed"/>
            <Control Name="Tab_Control" X="-2" Y="12">
              <RadioButton Name="Total_Button"      X="7"  BaseSurf="930" PushSurf="931" HighlightSurf="932" MouseEvent="TabButtonPressed"/>
              <RadioButton Name="Community_Button"  X="21" BaseSurf="933" PushSurf="934" HighlightSurf="935" MouseEvent="TabButtonPressed"/>
              <RadioButton Name="Action_Button"     X="35" BaseSurf="936" PushSurf="937" HighlightSurf="938" MouseEvent="TabButtonPressed"/>
              <RadioButton Name="Management_Button" X="49" BaseSurf="939" PushSurf="940" HighlightSurf="941" MouseEvent="TabButtonPressed"/>
            </Control>
          </Window>
        </ActionUI>
        """;
    // 與本機 Actions.xml 相同的欄位（項目是自己挑的 8 個）。
    const string List="""
        <?xml version="1.0"?>
        <ActionList>
          <Action action="Action_SocialAction1"    category="action"     control="action" icon="1001"/>
          <Action action="Action_Loot"             category="action"     control="action" icon="1002"/>
          <Action action="Action_Trade"            category="community"  control="action" icon="1003"/>
          <Action action="Action_InviteParty"      category="community"  control="target" target="1,1,1,0" domain="view" icon="1004"/>
          <Action action="Action_OpenBookmarkWin"  category="management" control="action" icon="1005"/>
          <Action action="Action_ViewDropItemName" category="management" control="toggle" icon="1006"/>
          <Action action="Action_SocialAction2"    category="action"     control="action" icon="1007"/>
          <Action action="Action_AddFriend"        category="community"  control="target" target="1,1,1,0" domain="global" icon="1008"/>
        </ActionList>
        """;

    const uint Base=0xFF102030,TabBase=0xFF200000,TabPush=0xFFFF0000,TabHover=0xFF800000,Sel=0xFF00FF00,Icon=0xFF0000FF,ExitBase=0xFF404040,ExitPush=0xFFC0C0C0;
    static readonly Dictionary<int,UiSurface> Surfs=Build();
    static Dictionary<int,UiSurface> Build(){
        var d=new Dictionary<int,UiSurface>{
            [900]=Surface(900,100,80,Base),[915]=Surface(915,6,14,0xFF303030),[910]=Surface(910,6,6,0xFF505050),[911]=Surface(911,6,6,0xFF606060),
            [912]=Surface(912,6,6,0xFF505050),[913]=Surface(913,6,6,0xFF606060),[914]=Surface(914,6,4,0xFF707070),
            [920]=Surface(920,6,6,ExitBase),[921]=Surface(921,6,6,ExitPush),[950]=Surface(950,12,12,Sel)};   // 901 (frame) deliberately missing
        for(int i=0;i<4;i++){d[930+i*3]=Surface(930+i*3,12,8,TabBase);d[931+i*3]=Surface(931+i*3,12,8,TabPush);d[932+i*3]=Surface(932+i*3,12,8,TabHover);}
        return d;
    }
    static UiSurface? Get(int id)=>Surfs.TryGetValue(id,out var s)?s:null;
    static readonly ItemIconSource Icons=new(id=>id is >=1001 and <=1008&&id!=1002?new ItemIcon(id,Surface(id,10,10,Icon).Bitmap,Point.Empty,"synthetic"):null);   // 1002 missing (like 5363: only .tbt)
    static UiLayout L(string text,string name)=>UiLayout.Parse(text,name);
    static ActionWindowPanel Loaded(UiRoot? root=null,string ui=Ui,string list=List,Func<int,UiSurface?>? surf=null){
        var p=new ActionWindowPanel(null,root,Icons);p.Load(L(ui,"ActionUI-c.xml"),L(list,"Actions.xml"),surf??Get);return p;
    }
    static readonly UiStaticGameState World=new(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,int x,int y,int clicks=1,MouseButtons b=MouseButtons.Left,int delta=0)=>new(k,new Point(x,y),b,clicks,delta,Keys.None);
    static void Click(ActionWindowPanel p,int x,int y,IUiCommands c,int clicks=1){p.Mouse(M(UiMouseKind.Down,x,y,clicks),c);p.Mouse(M(UiMouseKind.Up,x,y,clicks),c);}
    static void DoubleClick(ActionWindowPanel p,int x,int y,IUiCommands c){Click(p,x,y,c,1);Click(p,x,y,c,2);}
    static uint Pixel(ActionWindowPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return (uint)canvas.Bitmap.GetPixel(x,y).ToArgb();}

    sealed class Bridge(bool accept):IUiCommands,IUiActionCommands {
        public readonly List<string> Calls=[];
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool RunAction(ActionEntry action){Calls.Add("run:"+action.Name);return accept;}
    }

    [WorldModuleTest("action-window")]
    static void Run(WorldModuleTestContext t){
        Catalog(t);
        LoadLayout(t);
        Broken(t);
        TabsAndScroll(t);
        MouseAndPaint(t);
        Execute(t);
        Hotkey(t);
        Registration(t);
    }

    static void Catalog(WorldModuleTestContext t){
        var a=ActionCatalog.Parse(L(List,"Actions.xml"));
        t.Equal(8,a.Count,"eight actions, XML order");
        t.Check(a[0] is {Name:"Action_SocialAction1",Category:ActionCategory.Action,Control:ActionControl.Action,Icon:1001,Domain:ActionDomain.None}&&a[0].Targets.Count==0,"plain action");
        t.Check(a[3] is {Control:ActionControl.Target,Domain:ActionDomain.View}&&a[3].Targets.SequenceEqual([true,true,true,false]),"target flags + domain");
        t.Check(a[5] is {Category:ActionCategory.Management,Control:ActionControl.Toggle},"toggle");
        t.Throws<UiLayoutException>(()=>ActionCatalog.Parse(L("<Other/>","x.xml")),"wrong root");
        t.Throws<UiLayoutException>(()=>ActionCatalog.Parse(L("<ActionList/>","x.xml")),"no Action");
        t.Throws<UiLayoutException>(()=>ActionCatalog.Parse(L(List.Replace("\"community\"  control=\"action\"","\"social\"  control=\"action\""),"x.xml")),"unknown category");
        t.Throws<UiLayoutException>(()=>ActionCatalog.Parse(L(List.Replace("control=\"toggle\"","control=\"switch\""),"x.xml")),"unknown control");
        t.Throws<UiLayoutException>(()=>ActionCatalog.Parse(L(List.Replace("domain=\"view\"","domain=\"world\""),"x.xml")),"unknown domain");
        t.Throws<UiLayoutException>(()=>ActionCatalog.Parse(L(List.Replace("target=\"1,1,1,0\" domain=\"view\"","target=\"1,1\" domain=\"view\""),"x.xml")),"target needs four flags");
        t.Throws<UiLayoutException>(()=>ActionCatalog.Parse(L(List.Replace("Action_Loot","Action_SocialAction1"),"x.xml")),"duplicate action");
        t.Throws<UiLayoutException>(()=>ActionCatalog.Parse(L(List.Replace("icon=\"1001\"","icon=\"x\""),"x.xml")),"bad icon");
    }

    static void LoadLayout(WorldModuleTestContext t){
        var p=Loaded();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Equal(new Rectangle(350,150,100,80),p.Bounds,"BaseSurf size, centred, y=150");
        t.Equal(10,p.MoveableHeight,"Moveable");
        t.Check(p.Grid==new LegacyCellGrid(new(5,25),new(10,10),new(11,11),3,2),"grid X/Y, Row × Column, unit + gap",p.Grid);
        t.Check(p.GridDrag,"Drag=1 kept as data");
        t.Check(p.Tabs.Select(x=>x.Category).SequenceEqual(new ActionCategory?[]{null,ActionCategory.Community,ActionCategory.Action,ActionCategory.Management}),"four tabs in XML order");
        t.Equal(new Point(5,12),p.Tabs[0].Button.Offset,"tab offset = Tab_Control + X");
        t.Check(p.Exit is {Offset.X:90,Offset.Y:2}&&p.Exit.Base?.Surf==920&&p.Exit.Push?.Surf==921,"exit button");
        t.Check(p.Scroll is {Offset.X:45,Offset.Y:25,MaxValue:8}&&p.Scroll.Up?.Offset==new Point(45,25)&&p.Scroll.Down?.Offset==new Point(45,45)&&p.Scroll.BackgroundAt==new Point(45,31),"scrollbar relative to the grid");
        t.Check(p.OpenButton==new ActionWindowPanel.OpenButtonSpec(new(774,470),960,961,77),"ActionUI_Button kept as data only");
        t.Check(p.SelectionSurface?.Surf==950,"selection image");
        t.Check(p.Missing.SequenceEqual(["surf 901 (ActionUI/Window[0]/@FrameSurf)"]),"missing frame listed",p.Missing);
        t.Equal(8,p.Actions.Count,"actions loaded");
        t.Check(!p.Visible,"closed after load");
    }

    static void Broken(WorldModuleTestContext t){
        var none=Loaded(surf:id=>id is 900 or 901?null:Get(id));
        t.Check(!none.Available&&none.Unavailable!.Contains("底圖")&&!none.Open()&&none.Bounds.IsEmpty,"no BaseSurf and no FrameSurf → unavailable, no substitute size");
        var frameOnly=Loaded(surf:id=>id==900?null:id==901?Surface(901,90,70,Base):Get(id));
        t.Equal(new Size(90,70),frameOnly.Size,"FrameSurf gives the size when BaseSurf is missing");
        t.Throws<UiLayoutException>(()=>Loaded(ui:Ui.Replace("<ActionUI Toggle=\"true\">","<Other>").Replace("</ActionUI>","</Other>")),"wrong root");
        t.Throws<UiLayoutException>(()=>Loaded(ui:Ui.Replace("\"Action_Window\"","\"Other_Window\"")),"no Action_Window");
        t.Throws<UiLayoutException>(()=>Loaded(ui:Ui.Replace("Row=\"2\"","Row=\"0\"")),"bad grid");
        t.Throws<UiLayoutException>(()=>Loaded(ui:Ui.Replace("BaseSurf=\"920\"","BaseSurf=\"x\"")),"bad surf");
        t.Throws<UiLayoutException>(()=>Loaded(list:"<ActionList/>"),"empty action list");
        var host=new ActionWindowPanel();host.Load(L(Ui,"uistatus86-c.xml"),(SurfCatalog)null!);   // returns before touching the catalog
        t.Check(!host.Available&&host.Unavailable=="沒有 UiResources","product Load without resources → unavailable, no exception");
    }

    static void TabsAndScroll(WorldModuleTestContext t){
        var p=Loaded();p.Open();
        t.Check(p.ActiveTab==0&&p.TabActions.Count==8,"Total = every action");
        t.Equal(1,p.MaxScroll,"8 actions in 3 columns = 3 rows, 2 shown → 1 scroll step");
        p.ScrollBy(5);t.Equal(1,p.ScrollRow,"scroll clamps at max");
        t.Equal("Action_InviteParty",p.ActionInSlot(0)?.Name,"scrolled one row (4th action first)");
        t.Check(p.ActionInSlot(4)?.Name=="Action_AddFriend"&&p.ActionInSlot(5)==null,"second visible row = 7th..8th, last slot empty");
        p.ScrollBy(-9);t.Equal(0,p.ScrollRow,"scroll clamps at 0");
        p.SelectTab(1);
        t.Check(p.TabActions.Select(a=>a.Name).SequenceEqual(["Action_Trade","Action_InviteParty","Action_AddFriend"]),"Community tab");
        p.SelectTab(2);
        t.Check(p.TabActions.Select(a=>a.Name).SequenceEqual(["Action_SocialAction1","Action_Loot","Action_SocialAction2"])&&p.MaxScroll==0,"Action tab fits");
        p.SelectTab(3);
        t.Check(p.TabActions.Select(a=>a.Name).SequenceEqual(["Action_OpenBookmarkWin","Action_ViewDropItemName"])&&p.ActionInSlot(2)==null,"Management tab, empty slots");
        p.Close();p.Open();
        t.Equal(0,p.ActiveTab,"reopen starts on Total");
    }

    static void MouseAndPaint(WorldModuleTestContext t){
        var p=Loaded();var c=new UiNullCommands();
        t.Check(!p.Mouse(M(UiMouseKind.Down,360,180),c),"closed window ignores mouse");
        p.Open();
        t.Equal(Base,Pixel(p,350+60,150+70),"background drawn");
        t.Equal(TabPush,Pixel(p,355+1,162+1),"active tab drawn with PushSurf");
        t.Equal(TabBase,Pixel(p,369+1,162+1),"other tab BaseSurf");
        p.Mouse(M(UiMouseKind.Move,369+1,162+1,0,MouseButtons.None),c);
        t.Equal(TabHover,Pixel(p,369+1,162+1),"hovered tab HighlightSurf");
        t.Equal(Icon,Pixel(p,355+1,175+1),"slot 0 icon (.tbt source)");
        t.Equal(Base,Pixel(p,366+1,175+1),"missing icon not drawn (no placeholder)");
        Click(p,355+1,175+1,c);
        t.Equal("Action_SocialAction1",p.Selected?.Name,"single click selects");
        t.Equal(Sel,Pixel(p,355-1,175-1),"selection frame at cell −1,−1");
        t.Check(c.Calls.Count==0&&p.LastRun==null,"single click does not run");
        Click(p,355+1+11,175+1,c,2);
        t.Check(p.Selected?.Name=="Action_Loot"&&p.LastRun==null,"double click on another cell only selects it");
        Click(p,355+5,162+3,c);
        t.Check(p.ActiveTab==0,"click on the active tab keeps it");
        Click(p,369+5,162+3,c);
        t.Check(p.ActiveTab==1&&p.Selected==null&&p.ScrollRow==0,"tab click switches page, clears selection");
        Click(p,355+5,162+3,c);
        p.Mouse(M(UiMouseKind.Down,395+1,195+1),c);
        t.Equal(1,p.ScrollRow,"Down_Button scrolls one row");
        t.Equal(0xFF606060u,Pixel(p,395+1,195+1),"held scroll button PushSurf");
        p.Mouse(M(UiMouseKind.Up,395+1,195+1),c);
        t.Equal(0xFF707070u,Pixel(p,395+1,150+41+1),"thumb at the bottom of its travel (Down.Y − thumb height)");
        p.Mouse(M(UiMouseKind.Wheel,360,180,0,MouseButtons.None,120),c);
        t.Equal(0,p.ScrollRow,"wheel up scrolls back");
        t.Equal(0xFF707070u,Pixel(p,395+1,150+31+1),"thumb at Move_Button.Y");
        t.Check(p.IsDragHandle(new Point(352,152))&&!p.IsDragHandle(new Point(441,153))&&!p.IsDragHandle(new Point(352,165)),"drag strip = Moveable, exit excluded");
        p.MoveTo(new Point(10,20));
        t.Check(p.Bounds==new Rectangle(10,20,100,80)&&p.Moved,"moved");
        p.MoveTo(new Point(350,150));
        p.Mouse(M(UiMouseKind.Down,440+1,152+1),c);
        t.Equal(ExitPush,Pixel(p,440+1,152+1),"held exit PushSurf");
        p.Mouse(M(UiMouseKind.Move,300,300,0),c);p.Mouse(M(UiMouseKind.Up,300,300),c);
        t.Check(p.Visible,"release outside exit does not close");
        Click(p,440+1,152+1,c);
        t.Check(!p.Visible,"Exit_Button closes");
        p.Open();IUiClosablePanel closable=p;closable.Close();
        t.Check(!p.Visible,"IUiClosablePanel.Close (stack / global ESC)");
        p.Open();
        t.Check(!p.Key(new UiKeyEvent(Keys.Escape,true,false),c),"ESC left to the global ESC (UiRoot.CloseAll)");
        p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible,"leaving the world closes it");
        t.Check(!c.Calls.Contains("logout"),"no logout");
    }

    static void Execute(WorldModuleTestContext t){
        var p=Loaded();var c=new UiNullCommands();p.Open();
        DoubleClick(p,355+1,175+1,c);
        t.Check(p.LastRun is {Result:ActionRunResult.DevTools}&&p.LastRun.Value.Action.Name=="Action_SocialAction1"&&c.Calls.SequenceEqual(["dev-tools:"+ActionWindowPanel.EmoteDevToolsPage]),"double click: social action → 稱號／表情 page (no bridge)",c.Calls);
        c.Calls.Clear();
        DoubleClick(p,355+1+11,175+1,c);
        t.Check(p.LastRun is {Result:ActionRunResult.Unhandled}&&c.Calls.Count==0&&p.Unhandled.Contains("Action_Loot"),"unwired action: nothing sent, logged");
        t.Equal(ActionRunResult.Unhandled,p.Execute(p.Actions[4],c),"bookmark action without a bookmark window in the root");
        var yes=new Bridge(true);
        t.Equal(ActionRunResult.Bridge,p.Execute(p.Actions[1],yes),"bridge takes the action");
        t.Check(yes.Calls.SequenceEqual(["run:Action_Loot"]),"bridge called once",yes.Calls);
        var no=new Bridge(false);
        t.Equal(ActionRunResult.DevTools,p.Execute(p.Actions[6],no),"bridge refuses → fallback");
        t.Check(no.Calls.SequenceEqual(["run:Action_SocialAction2","dev-tools:"+ActionWindowPanel.EmoteDevToolsPage]),"bridge first, then dev tools",no.Calls);
        var wrapped=new UiMacroCommands(yes,(_,_)=>{});yes.Calls.Clear();
        t.Equal(ActionRunResult.Bridge,p.Execute(p.Actions[2],wrapped),"bridge found through UiMacroCommands");
        t.Check(ActionWindowPanel.SocialActions.Count==4,"four social actions");
    }

    static void Hotkey(WorldModuleTestContext t){
        var root=new UiRoot();var p=Loaded(root);root.Add(p);
        var menu=new MenuButtonsPanel(root);menu.Update(World);var c=new UiNullCommands();
        var key=new UiKeyEvent(ActionWindowPanel.Hotkey,true,false);
        t.Check(ActionWindowPanel.Hotkey==(Keys.Control|Keys.D),"Ctrl+D");
        t.Check(!MenuButtonsPanel.CommandHotkeys.ContainsKey(ActionWindowPanel.Hotkey)&&!MenuButtonsPanel.Hotkeys.ContainsKey(ActionWindowPanel.Hotkey),"Ctrl+D not in the other hotkey tables");
        t.Check(menu.Key(key,c)&&p.Visible&&root.WindowStack.Contains(p),"Ctrl+D opens through UiRoot.Toggle");
        t.Check(menu.Key(new UiKeyEvent(ActionWindowPanel.Hotkey,false,false),c)&&menu.Key(new UiKeyEvent(ActionWindowPanel.Hotkey,true,true),c)&&p.Visible,"key up swallowed, auto-repeat ignored");
        t.Check(menu.Key(key,c)&&!p.Visible&&!root.WindowStack.Contains(p),"Ctrl+D again closes");
        t.Check(root.CloseAll().Count==0,"nothing left open");
        menu.Key(key,c);
        t.Check(root.CloseAll().SequenceEqual([p])&&!p.Visible,"global ESC closes the action window");
        t.Check(c.Calls.Count==0,"no dev-tools fallback when the window exists",c.Calls);
        var bare=new UiRoot();var unavailable=new ActionWindowPanel(null,bare);bare.Add(unavailable);
        var menu2=new MenuButtonsPanel(bare);menu2.Update(World);
        t.Check(!menu2.Key(key,c)&&c.Calls.Count==0,"unavailable action window: Ctrl+D not taken (as before)");
        t.Check(!new MenuButtonsPanel(null).Key(key,c),"no root: not taken");
        menu.Update(new UiStaticGameState(InWorld:false));
        t.Check(!menu.Key(key,c),"outside the world not taken");
    }

    static void Registration(WorldModuleTestContext t){
        var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==ActionWindowPanel.PanelId);
        t.Check(reg!=null&&reg.Layout==ActionWindowPanel.HostLayout,"[UiPanel] discovered with host layout uistatus86");
        t.Check(reg?.Create(new UiRoot()) is ActionWindowPanel {Layer:UiLayer.Window,Visible:false} and IUiClosablePanel and IUiMovablePanel,"window, closable, movable, closed");
        t.Check(UiPanelRegistry.Errors.Count==0,"no discovery errors",UiPanelRegistry.Errors);
    }
}
