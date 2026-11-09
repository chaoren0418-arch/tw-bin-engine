using System.Reflection;
using P06.NativeUi;
namespace P06;

// R4-I（T300／#712）離線測試：原生 HYPER_TEXT_WINDOW（NativeUi/HyperTextPanel.cs）與 NpcUi.cs 的頁面分流。
// 只用合成 XML／合成圖／固定字寬；不開遊戲、網路、視窗、伺服器、DB，也不讀客戶端素材。
static class HyperTextPanelTests {
    [WorldModuleTest("hypertext-panel")]
    static void Run(WorldModuleTestContext t){
        Registration(t);Layout(t);PageAndLinks(t);Scrolling(t);NumberBox(t);CloseAndEvict(t);Router(t);Wiring(t);
    }

    sealed class Host:IHyperTextHost {
        public List<string> Calls{get;}=[];
        public void Link(HyperTextView shown,int option)=>Calls.Add($"link:{shown.Source}:{shown.Revision}:{option}");
        public void Submit(HyperTextView shown,long value)=>Calls.Add($"submit:{shown.Number?.Kind}:{shown.Number?.Revision}:{value}");
        public void CancelNumber(HyperTextView shown)=>Calls.Add($"cancel:{shown.Number?.Revision}");
        public void Close(HyperTextView shown)=>Calls.Add($"close:{shown.Source}:{shown.Revision}");
    }

    // 元素名稱與座標同本機 uiextra86-c.xml 的 HYPER_TEXT_WINDOW（版面數值，不是圖）；窗原點改成 (100,50) 以驗證位移。
    static string Xml(string extra="")=>$"""
<?xml version="1.0"?>
<root>
<HYPER_TEXT_WINDOW x = "100" y = "50">
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_HYPER_TEXT_BASE" surfNumber = "1968" xPos = "6" yPos = "9"/>
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_HYPER_TEXT_FRAME" surfNumber = "1969"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_QUIT_X" surfNumber = "1962" xPos = "225" yPos = "15"/>
	<IMAGE surfName = "SURF_RENEWAL_WIN_SCROLLBAR_LONG" surfNumber = "1970" xPos = "217" yPos = "51"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber = "2040" xPos = "217" yPos = "275"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber = "2042" xPos = "217" yPos = "37"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber = "2044"/>
{extra}</HYPER_TEXT_WINDOW>
</root>
""";
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Func<int,UiSurface?> Surfaces()=>id=>id switch{
        1968=>Surf(id,220,270,0xFF203040),1969=>Surf(id,240,4,0xFF999999),1962=>Surf(id,12,12,0xFFFF0000),
        1970=>Surf(id,8,220,0xFF111111),2040 or 2042=>Surf(id,8,12,0xFF222222),2044=>Surf(id,8,8,0xFF333333),_=>null};
    // 固定字寬：每字 12 px → 192 px 一行 16 字。
    static int Measure(string s)=>s.Length*12;
    static (HyperTextPanel p,Host h) New(){var p=new HyperTextPanel(Measure);p.Load(UiLayout.Parse(Xml(),"synthetic-uiextra86.xml"),Surfaces());var h=new Host();p.Attach(h);return (p,h);}
    static UiMouseEvent M(UiMouseKind kind,int x,int y,int delta=0)=>new(kind,new(x,y),kind is UiMouseKind.Move or UiMouseKind.Wheel?MouseButtons.None:MouseButtons.Left,1,delta,Keys.None);
    static void Click(IUiPanel p,Point at,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,at.X,at.Y),cmd);p.Mouse(M(UiMouseKind.Up,at.X,at.Y),cmd);}
    static Point Mid(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static HyperTextView Npc(int rev,string text="你好，旅行者。",HyperTextNumberBox? number=null,params HyperTextLink[] links)=>new(HyperTextSource.Npc,rev,text,links,"",number);

    static void Registration(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Errors.Count==0,"panel auto-discovery has no errors",UiPanelRegistry.Errors);
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id==HyperTextPanel.PanelId&&p.Layout=="uiextra86")==1,"HYPER_TEXT_WINDOW registered once on uiextra86");
        t.Check(UiPanelRegistry.Panels.Single(p=>p.Id==HyperTextPanel.PanelId).Create(new UiRoot()) is HyperTextPanel,"registration creates HyperTextPanel");
        t.Check(UiPanelRegistry.Panels.Select(p=>p.Id).Distinct().Count()==UiPanelRegistry.Panels.Count,"registry ids stay unique");
        var p=new HyperTextPanel(Measure);
        t.Check(p is IUiClosablePanel&&p is IUiMovablePanel&&p is IUiTextInput,"closable, movable, text input");
    }

    static void Layout(WorldModuleTestContext t){
        var (p,_)=New();
        t.Check(p.Id=="HYPER_TEXT_WINDOW"&&p.Layer==UiLayer.Window&&!p.Visible&&p.Bounds.IsEmpty,"window layer, hidden until a page");
        t.Equal(new Point(100,50),p.Origin,"origin = XML x/y");
        t.Equal(Point.Empty,p.Frame!.Offset,"frame 1969 at the window origin");
        t.Equal(new Point(6,9),p.Base!.Offset,"base 1968 (6,9)");
        t.Check(p.ScrollBar!.Offset==new Point(217,51)&&p.Up!.Offset==new Point(217,37)&&p.Down!.Offset==new Point(217,275),"scrollbar 1970 / up 2042 / down 2040 from XML");
        t.Check(p.CloseButton is {Surf:1962}&&p.CloseButton.Offset==new Point(225,15),"close X = XML 1962 (225,15)");
        t.Throws<UiLayoutException>(()=>new HyperTextPanel(Measure).Load(UiLayout.Parse("<root/>","a.xml"),Surfaces()),"missing HYPER_TEXT_WINDOW throws");
        t.Throws<UiLayoutException>(()=>new HyperTextPanel(Measure).Load(UiLayout.Parse(Xml().Replace(" xPos = \"225\" yPos = \"15\"",""),"b.xml"),Surfaces()),"close X without coordinates throws");
        t.Throws<UiLayoutException>(()=>new HyperTextPanel(Measure).Load(UiLayout.Parse(Xml("\t<IMAGE surfName = \"SURF_RENEWAL_WIN_SCROLLBAR_LONG\" surfNumber = \"1\" xPos = \"1\" yPos = \"1\"/>\n"),"c.xml"),Surfaces()),"duplicate element throws");
        p.Show(Npc(1));
        // 框 (0,0,240,4)、底 (6,9)–(226,279)、下鈕到 287、關閉 (225,15)–(237,27)。
        t.Equal(new Rectangle(100,50,240,287),p.Bounds,"bounds = drawn elements + title bar");
        t.Equal(new Point(121,94),p.LineAt(0),"line 0 at (21,44)");
        t.Equal(new Point(121,94+15*15),p.LineAt(15),"line 15 at (21,44+15·15)");
        using var canvas=new UiCanvas();
        canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
        var basePx=canvas.Bitmap.GetPixel(100+7,50+10);
        t.Check(basePx.R==0x20&&basePx.G==0x30&&basePx.B==0x40,"base painted at origin + (6,9)",basePx);
        t.Equal(Color.Red.ToArgb(),canvas.Bitmap.GetPixel(100+226,50+16).ToArgb(),"close X painted at (225,15)");
        t.Equal(Color.FromArgb(unchecked((int)0xFF111111)).ToArgb(),canvas.Bitmap.GetPixel(100+218,50+200).ToArgb(),"scrollbar painted");
    }

    static void PageAndLinks(WorldModuleTestContext t){
        var (p,h)=New();var cmd=new UiNullCommands();
        t.Equal("一二三四五六七八九十一二三四五六|七八",string.Join("|",p.Wrap("一二三四五六七八九十一二三四五六七八")),"wraps at 192 px (16 × 12)");
        t.Equal("甲||乙",string.Join("|",p.Wrap("甲\r\n\n乙\n\n")),"\\n breaks, \\r ignored, trailing blank lines dropped");
        p.Show(Npc(5,"歡迎光臨。",null,new HyperTextLink(1,"買東西",true),new HyperTextLink(2,"不能用",false)));
        t.Check(p.Visible,"page opens the window");
        var kinds=p.Rows.Select(r=>r.Kind).ToArray();
        t.Equal("Text,Text,Link,Link",string.Join(",",kinds),"text, blank, then one row per link");
        t.Check(p.Rows[2] is {LinkId:1,Enabled:true,Text:"買東西"}&&p.Rows[3] is {LinkId:2,Enabled:false},"link rows keep id and enabled");
        Click(p,Mid(p.LineRect(2)),cmd);
        t.Equal("link:Npc:5:1",h.Calls.LastOrDefault(),"click an enabled link → host.Link(page, id)");
        int before=h.Calls.Count;Click(p,Mid(p.LineRect(3)),cmd);
        t.Equal(before,h.Calls.Count,"disabled link does nothing");
        p.Mouse(M(UiMouseKind.Down,Mid(p.LineRect(2)).X,Mid(p.LineRect(2)).Y),cmd);p.Mouse(M(UiMouseKind.Up,Mid(p.LineRect(0)).X,Mid(p.LineRect(0)).Y),cmd);
        t.Equal(before,h.Calls.Count,"release outside the link does nothing");
        t.Equal(HyperTextPanel.LinkColor.ToArgb(),ChatPalette.Color(ChatFormat.Yellow).ToArgb(),"link colour = palette 13 (推測)");
        var notice=new HyperTextView(HyperTextSource.Query,9,"結果",[],"查詢結果頁的選項尚未支援。",null);
        p.Show(notice);
        t.Check(p.Rows.Last().Kind==HyperTextPanel.RowKind.Notice&&p.Rows.Count==3,"notice rows after a blank line");
        t.Check(!p.Mouse(M(UiMouseKind.Down,10,10),cmd),"press outside not taken");
    }

    static void Scrolling(WorldModuleTestContext t){
        var (p,_)=New();var cmd=new UiNullCommands();
        p.Show(Npc(1,string.Join("\n",Enumerable.Range(0,20).Select(i=>"第"+i+"行"))));
        t.Equal(20,p.Rows.Count,"20 rows");t.Equal(4,p.MaxScroll,"scroll range 0..rows−16");
        t.Check(p.Mouse(M(UiMouseKind.Wheel,150,150,-120),cmd)&&p.Scroll==1,"wheel down scrolls one row");
        for(int i=0;i<10;i++)Click(p,new(100+220,50+280),cmd);
        t.Equal(4,p.Scroll,"down button stops at the end");
        Click(p,new(100+220,50+40),cmd);t.Equal(3,p.Scroll,"up button scrolls one row");
        t.Equal("第3行",p.VisibleRows[0].Text,"first visible row = scroll position");
        p.Show(Npc(1,string.Join("\n",Enumerable.Range(0,20).Select(i=>"第"+i+"行"))));
        t.Equal(3,p.Scroll,"same page refresh keeps the scroll");
        p.Show(Npc(2,"短"));
        t.Equal(0,p.Scroll,"new page scrolls back to the top");
    }

    static void NumberBox(WorldModuleTestContext t){
        var (p,h)=New();var cmd=new UiNullCommands();
        var amount=new HyperTextNumberBox(HyperTextNumberKind.Amount,7,1,50,3,true,true,"可輸入 1～50");
        p.Show(Npc(4,"要做幾個？",amount));
        t.Equal("Text,Text,Range,Number",string.Join(",",p.Rows.Select(r=>r.Kind)),"number box rows after the text");
        t.Check(p.Input=="3"&&p.InputFocused,"initial value, focused when the prompt appears");
        t.Check(p.CancelRect.IsEmpty&&!p.SubmitRect.IsEmpty,"S136 box: OK only");
        t.Check(p.Key(new UiKeyEvent(Keys.Back,true,false),cmd)&&p.Input=="","Backspace deletes");
        t.Check(p.Char('4')&&p.Char('2')&&p.Input=="42","digits typed through WM_CHAR");
        t.Check(!p.Char('a')&&p.Input=="42","letters not taken");
        t.Check(p.Key(new UiKeyEvent(Keys.Enter,true,false),cmd),"Enter consumed while focused");
        t.Equal("submit:Amount:7:42",h.Calls.LastOrDefault(),"Enter submits value with the prompt revision");
        t.Check(!p.InputFocused&&!p.Key(new UiKeyEvent(Keys.Enter,true,false),cmd),"after submit the next Enter goes to the game/chat");
        Click(p,Mid(p.InputRect),cmd);t.Check(p.InputFocused,"click the box focuses it");
        p.Char('9');t.Equal("429",p.Input,"typed after click");
        t.Check(!p.CanSubmit,"429 > max 50 → cannot submit");
        int before=h.Calls.Count;Click(p,Mid(p.SubmitRect),cmd);t.Equal(before,h.Calls.Count,"OK with out-of-range value does nothing");
        Click(p,Mid(p.InputRect),cmd);p.Key(new UiKeyEvent(Keys.Back,true,false),cmd);Click(p,Mid(p.SubmitRect),cmd);
        t.Equal("submit:Amount:7:42",h.Calls.LastOrDefault(),"OK button submits");
        p.Show(Npc(4,"要做幾個？",amount with{CanSend=false}));
        t.Equal("42",p.Input,"same prompt revision keeps the typed value");
        t.Check(!p.CanSubmit,"host says cannot send → OK disabled");
        p.Show(Npc(4,"要做幾個？",amount with{Revision=8,Initial=10}));
        t.Equal("10",p.Input,"new prompt revision resets the value");
        var castle=new HyperTextNumberBox(HyperTextNumberKind.Castle,3,10,50,10,true,true,"稅率 10%～50%");
        p.Show(new HyperTextView(HyperTextSource.Query,11,"tax",[],"",castle));
        t.Check(!p.CancelRect.IsEmpty,"castle box has Cancel");
        Click(p,Mid(p.CancelRect),cmd);t.Equal("cancel:3",h.Calls.LastOrDefault(),"Cancel → host.CancelNumber");
        Click(p,Mid(p.SubmitRect),cmd);t.Equal("submit:Castle:3:10",h.Calls.LastOrDefault(),"castle OK submits the initial (minimum) value");
        p.Show(Npc(12,"沒有命令",amount with{Revision=9,Answerable=false}));
        t.Check(p.Input==""&&!p.InputFocused&&!p.Char('1'),"unanswerable prompt: no input");
        p.Show(Npc(13));
        t.Check(p.InputRect.IsEmpty&&!p.InputFocused,"page without prompt has no box");
    }

    static void CloseAndEvict(WorldModuleTestContext t){
        var (p,h)=New();var cmd=new UiNullCommands();
        p.Show(Npc(21));
        Click(p,Mid(p.CloseRect),cmd);
        t.Check(!p.Visible&&h.Calls.LastOrDefault()=="close:Npc:21","close X hides and asks the host to close the page");
        p.Show(Npc(21));t.Check(!p.Visible,"the dismissed page is not reopened while the host still has it");
        p.Show(Npc(22));t.Check(p.Visible,"a newer page opens again");
        ((IUiClosablePanel)p).Close();
        t.Equal("close:Npc:22",h.Calls.LastOrDefault(),"stack eviction (IUiClosablePanel.Close) closes the page too");
        p.Show(null);p.Show(Npc(22));t.Check(p.Visible,"after the host dropped the page, the same revision may show again");
        p.Update(new UiStaticGameState(InWorld:false));t.Check(!p.Visible,"leaving the world hides the window");
        // 拖曳：標題列，關閉鈕除外。
        p.Show(Npc(30));
        t.Check(p.IsDragHandle(new(100+50,50+5))&&!p.IsDragHandle(new(100+230,50+18))&&!p.IsDragHandle(new(100+50,50+100)),"title bar (1,1)–(239,20) drags, close X does not");
        p.MoveTo(new(200,100));t.Equal(new Point(200,100),p.Origin,"moved");
        // 堆疊：五個窗時最舊的被關（UiRoot.Evict → Close）。
        var root=new UiRoot();root.Add(p);
        var others=Enumerable.Range(0,4).Select(i=>new Dummy("W"+i)).ToArray();foreach(var d in others)root.Add(d);
        root.SyncWindows();foreach(var d in others){d.Open=true;root.SyncWindows();}
        t.Check(!p.Visible&&h.Calls.LastOrDefault()=="close:Npc:30","5th window evicts the page window through Close()");
    }
    sealed class Dummy(string id):IUiClosablePanel {
        public bool Open;
        public string Id=>id;public UiLayer Layer=>UiLayer.Window;public Rectangle Bounds=>Open?new(0,0,10,10):Rectangle.Empty;public bool Visible=>Open;
        public void Load(UiLayout layout,SurfCatalog surfs){}public void Update(IUiGameState state){}public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;public bool Key(UiKeyEvent e,IUiCommands commands)=>false;public void Close()=>Open=false;
    }

    static void Router(WorldModuleTestContext t){
        var r=new HyperTextRouter();
        t.Equal((HyperTextSource?)null,r.Pick(false,0,false,0),"nothing → no page");
        t.Equal((HyperTextSource?)HyperTextSource.Npc,r.Pick(true,1,false,0),"NPC page alone");
        t.Equal((HyperTextSource?)HyperTextSource.Query,r.Pick(true,1,true,5),"query page arrives → replaces the NPC page (single window)");
        t.Equal((HyperTextSource?)HyperTextSource.Query,r.Pick(true,1,true,5),"unchanged → stays on the query page");
        t.Equal((HyperTextSource?)null,r.Pick(true,1,false,0),"query closed → the replaced NPC page does not come back");
        t.Equal((HyperTextSource?)HyperTextSource.Npc,r.Pick(true,2,false,0),"new NPC page → shown");
        t.Equal((HyperTextSource?)HyperTextSource.Query,r.Pick(true,2,true,6),"a new query page replaces the NPC page again");
        r.Reset();
        t.Equal((HyperTextSource?)HyperTextSource.Query,r.Pick(true,3,true,7),"both new in the same frame → query page in front");
        t.Equal((HyperTextSource?)HyperTextSource.Npc,r.Pick(true,4,true,7),"NPC page changes → NPC in front, query replaced");
        t.Equal((HyperTextSource?)null,r.Pick(false,0,true,7),"NPC closed → replaced query page stays closed");
        t.Equal((HyperTextSource?)HyperTextSource.Query,r.Pick(false,0,true,8),"query page changes → shown again");
    }

    static void Wiring(WorldModuleTestContext t){
        const BindingFlags D=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        t.Check(typeof(ClientForm).GetMethod("SyncNativeHyperText",D)!=null,"ClientForm routes pages to HYPER_TEXT_WINDOW");
        t.Check(typeof(ClientForm).GetMethod("NativeHyperText",D)?.ReturnType==typeof(HyperTextPanel),"native page window lookup present");
        var host=typeof(ClientForm).GetNestedType("NativeHyperTextHost",BindingFlags.NonPublic);
        t.Check(host!=null&&typeof(IHyperTextHost).IsAssignableFrom(host),"host adapter implements IHyperTextHost");
    }
}
