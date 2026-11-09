using P06.NativeUi;
namespace P06;

// R5-B（T307／#734）bookmark-window-followup 離線測試：原生書籤窗的新增／刪除（846 確認）、排序鈕、區域篩選下拉與開窗入口。
// XML 與 .img 全部由本檔合成（元素名稱與座標照 BookmarkUI.xml 的結構，視窗原點 (10,20)），不讀真實客戶端、不啟動遊戲、不連網路。
static class BookmarkPanelFollowupTests {
    sealed class Host:IBookmarkWindowHost {
        public BookmarkView Bookmarks{get;set;}=View(1);
        public TeleportDestinationRequest? Destination{get;set;}
        public bool Sending=>false;
        public readonly List<string> Calls=[];
        public void Teleport(TeleportDestinationRequest shown,uint bookmarkId)=>Calls.Add($"teleport:{shown.Revision}:{bookmarkId}");
        public void CancelTeleport(TeleportDestinationRequest shown)=>Calls.Add($"cancel:{shown.Revision}");
    }
    sealed class Editor:IBookmarkEditHost {
        public bool Sending{get;set;}
        public readonly List<string> Calls=[];
        public void Add(BookmarkView shown,string name)=>Calls.Add($"add:{shown.Revision}:{name}");
        public void Delete(BookmarkView shown,uint bookmarkId)=>Calls.Add($"delete:{shown.Revision}:{bookmarkId}");
    }
    // 名稱刻意不照字母順序；地圖 4、0、4、57、0。
    static readonly Bookmark[] Sample=[new(101,"Gludio",4,1,1),new(102,"Aden",0,1,1),new(103,"Kent",4,1,1),new(104,"Oren",57,1,1),new(105,"Bear",0,1,1)];
    static BookmarkView View(int revision,Bookmark[]? items=null,bool canEdit=true,ushort capacity=50)=>new(1,1,revision,7,canEdit,capacity,"已收到伺服器書籤清單。",items??Sample);

    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Frame=0xFF808080,Base=0xFF202020,Btn=0xFFFF0000,Row=0xFF0000FF,Ok=0xFF00FF00,Sort=0xFFFFA000,Expand=0xFF00A0FF,ZoneBack=0xFF305030,ZoneRow=0xFFA000A0;
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [1]=Surface(1,263,420,Frame),[2]=Surface(2,251,407,Base),[3]=Surface(3,12,12,Btn),[7]=Surface(7,50,12,Base),
        [8]=Surface(8,200,17,Row),[16]=Surface(16,60,18,Ok),[19]=Surface(19,60,18,Ok),[20]=Surface(20,60,18,Btn),
        [30]=Surface(30,40,16,Sort),[40]=Surface(40,105,19,Expand),[42]=Surface(42,59,114,ZoneBack),[43]=Surface(43,55,19,ZoneRow),
    };
    static string Layout(bool sorting=true,bool zone=true){
        var rows=string.Concat(Enumerable.Range(0,12).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" Caption=\"temp{i}\" PushSurf=\"8;4\" Activate=\"0\" Height=\"17\"/>"));
        var zoneRows=string.Concat(Enumerable.Range(0,10).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" Caption=\"{(i==0?"ALL":"temp"+i)}\" PushSurf=\"43;4\" Height=\"19\"/>"));
        return "<?xml version=\"1.0\"?><BookmarkWindowUI Toggle=\"true\">"+
            "<Window Name=\"Bookmark_Window\" X=\"10\" Y=\"20\" FrameSurf=\"1;1\" Moveable=\"20\">"+
            "<Image Name=\"Base_Image\" Surf=\"2;0\" X=\"6\" Y=\"9\"/>"+
            "<Button Name=\"Exit_Button\" X=\"249\" Y=\"15\" BaseSurf=\"3\"/>"+
            "<Image Name=\"MemoryZoneListTitle_Image\" Surf=\"7;1\" X=\"24\" Y=\"145\"/>"+
            (zone?"<ComboBox Name=\"ZoneFilter_Combobox\" X=\"129\" Y=\"141\" Width=\"105\" Height=\"134\">"+
                "<Label Name=\"Text_Label\" String=\"878\" X=\"20\" Y=\"0\" Width=\"50\" Height=\"20\"/>"+
                "<Button Name=\"Expand_Button\" X=\"0\" Y=\"0\" BaseSurf=\"40\" HighlightSurf=\"41\"/>"+
                "<UpDownList Name=\"Expand_List\" Y=\"19\" PageSize=\"7\" Width=\"55\" Height=\"114\" Vertical=\"1\"><Image Name=\"Background_Image\" X=\"-2\" Surf=\"42\"/>"+zoneRows+"</UpDownList>"+
                "</ComboBox>":"")+
            (sorting?"<Button Name=\"Sorting_Button\" BaseSurf=\"30\" PushSurf=\"31;1\" HighlightSurf=\"32\" X=\"182\" Y=\"42\"/>":"")+
            "<MovableList Name=\"MemoryPlace_List\" X=\"22\" Y=\"162\" Width=\"200\" Height=\"204\" Vertical=\"1\">"+rows+"</MovableList>"+
            "<Button Name=\"Confirm_Button\" BaseSurf=\"16\" DisableSurf=\"19\" X=\"67\" Y=\"395\"/>"+
            "<Button Name=\"Cancel_Button\" BaseSurf=\"20\" X=\"134\" Y=\"395\"/>"+
            "<Label Name=\"BookmarkCount_Label\" X=\"200\" Y=\"397\" Width=\"20\" Height=\"15\"/>"+
            "</Window></BookmarkWindowUI>";
    }
    static BookmarkWindowPanel Panel(Host host,Editor? editor,string? xml=null){
        var map=Surfaces();var p=new BookmarkWindowPanel();
        p.Load(UiLayout.Parse(xml??Layout(),"BookmarkUI.xml"),id=>map.GetValueOrDefault(id));
        p.Attach(host);if(editor!=null)p.AttachEditor(editor);
        p.Update(InWorld);p.Toggle();p.Update(InWorld);
        return p;
    }
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at,MouseButtons b=MouseButtons.Left,int delta=0)=>new(k,at,b,1,delta,Keys.None);
    static void Click(BookmarkWindowPanel p,Rectangle r,UiNullCommands c){var at=Center(r);p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static UiKeyEvent K(Keys k,bool down=true)=>new(k,down,false);
    static string Names(BookmarkWindowPanel p)=>string.Join(",",p.Rows.Select(r=>r.Name));
    static int Pixel(BookmarkWindowPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("bookmark-window-followup")]
    static void Run(WorldModuleTestContext t){
        var cmd=new UiNullCommands();

        // 1. 介面：文字輸入（命名）＋可關閉；DeletePrompt＝字串 846
        t.Check(typeof(IUiTextInput).IsAssignableFrom(typeof(BookmarkWindowPanel))&&typeof(IUiClosablePanel).IsAssignableFrom(typeof(BookmarkWindowPanel)),"text input + closable");
        t.Equal("要刪除所選擇的項目？",BookmarkWindowPanel.DeletePrompt,"846 text (FU3§3.1)");

        // 2. 刪除：Delete 鍵 → 846 確認；否＝不送；是＝送一次
        var host=new Host();var ed=new Editor();var p=Panel(host,ed);
        t.Check(p.Visible&&!p.DestinationMode,"normal mode open");
        t.Check(p.Key(K(Keys.Delete),cmd)&&p.ConfirmingDelete==null,"Delete without a selection does nothing");
        Click(p,p.RowRect(1),cmd);
        t.Check(p.Selected==102,"row 1 selected (data order)",p.Selected);
        p.Key(K(Keys.Delete),cmd);
        t.Check(p.ConfirmingDelete==102&&ed.Calls.Count==0,"Delete → 846 confirm, nothing sent yet");
        t.Check(p.Key(K(Keys.Down),cmd)&&p.Selected==102,"arrow keys are swallowed while confirming");
        t.Check(p.Char('n'),"Y/N chars do not leak to chat while confirming");
        p.Key(K(Keys.Escape),cmd);
        t.Check(p.ConfirmingDelete==null&&p.Visible&&ed.Calls.Count==0,"ESC = no; window stays open");
        p.Key(K(Keys.Delete),cmd);p.Key(K(Keys.N),cmd);
        t.Check(p.ConfirmingDelete==null&&ed.Calls.Count==0,"N = no");
        p.Key(K(Keys.Delete),cmd);
        Click(p,p.CancelRect,cmd);
        t.Check(p.ConfirmingDelete==null&&p.Visible&&ed.Calls.Count==0,"cancel button = no (does not close the window)");
        p.Key(K(Keys.Delete),cmd);
        t.Check(p.ConfirmEnabled,"confirm button enabled while confirming");
        Click(p,p.ConfirmRect,cmd);
        t.Check(ed.Calls.SequenceEqual(["delete:1:102"])&&p.ConfirmingDelete==null&&p.Visible,"confirm button = yes → one delete with the shown revision",ed.Calls);
        p.Key(K(Keys.Delete),cmd);p.Key(K(Keys.Enter),cmd);
        t.Check(ed.Calls.Count==2&&ed.Calls[1]=="delete:1:102","Enter = yes",ed.Calls);
        p.Mouse(M(UiMouseKind.Down,Center(p.RowRect(3)),MouseButtons.Right),cmd);
        t.Check(p.Selected==104&&p.ConfirmingDelete==104,"right click on a row selects it and asks 846",(p.Selected,p.ConfirmingDelete));
        p.Key(K(Keys.Y),cmd);
        t.Check(ed.Calls.Last()=="delete:1:104","Y = yes",ed.Calls);
        ed.Sending=true;p.Key(K(Keys.Delete),cmd);
        t.Check(p.ConfirmingDelete==null,"no delete while a request is sending");
        ed.Sending=false;
        host.Bookmarks=View(2,[..Sample.Select(b=>b.Id==101?b with{DeletePending=true}:b)]);p.Update(InWorld);
        Click(p,p.RowRect(0),cmd);p.Key(K(Keys.Delete),cmd);
        t.Check(p.Selected==101&&p.ConfirmingDelete==null,"delete-pending bookmark cannot be deleted again");
        host.Bookmarks=View(3,canEdit:false);p.Update(InWorld);
        Click(p,p.RowRect(2),cmd);p.Key(K(Keys.Delete),cmd);
        t.Check(p.ConfirmingDelete==null,"CanEdit=false (walking, busy) → no delete");
        host.Bookmarks=View(4);p.Update(InWorld);
        Click(p,p.RowRect(2),cmd);p.Key(K(Keys.Delete),cmd);
        host.Bookmarks=View(5,[..Sample.Where(b=>b.Id!=103)]);p.Update(InWorld);
        t.Check(p.ConfirmingDelete==null,"confirmed bookmark vanished → confirm dropped");

        // 3. 新增：Insert → 命名（WM_CHAR）→ Enter 送出；ESC 放棄；名稱檢查
        ed.Calls.Clear();host.Bookmarks=View(6);p.Update(InWorld);
        t.Check(!p.Char('a'),"chars not taken outside naming (go to chat)");
        p.Key(K(Keys.Insert),cmd);
        t.Check(p.NamingText=="","Insert starts naming");
        foreach(char c in "Giran")p.Char(c);
        p.Key(K(Keys.Back),cmd);
        t.Equal("Gira",p.NamingText,"typing and backspace");
        t.Check(!p.Key(K(Keys.A),cmd),"printable keys left to WM_CHAR while naming");
        p.Char('n');p.Key(K(Keys.Enter),cmd);
        t.Check(ed.Calls.SequenceEqual(["add:6:Giran"])&&p.NamingText==null,"Enter sends one add (C165 via host)",ed.Calls);
        p.Key(K(Keys.Insert),cmd);foreach(char c in "aden")p.Char(c);p.Key(K(Keys.Enter),cmd);
        t.Check(ed.Calls.Count==1&&p.EditError!=null&&p.NamingText=="aden","duplicate name (case-insensitive) rejected locally",p.EditError);
        p.Key(K(Keys.Escape),cmd);
        t.Check(p.NamingText==null&&p.Visible,"ESC abandons naming, window stays");
        p.Key(K(Keys.Insert),cmd);p.Key(K(Keys.Enter),cmd);
        t.Check(ed.Calls.Count==1&&p.EditError!=null,"empty name rejected (BookmarkPackets.NameError)");
        Click(p,p.CancelRect,cmd);
        t.Check(p.NamingText==null&&p.Visible,"cancel button abandons naming");
        p.Key(K(Keys.Insert),cmd);p.Char('X');Click(p,p.ConfirmRect,cmd);
        t.Check(ed.Calls.Last()=="add:6:X","confirm button sends the name",ed.Calls);
        host.Bookmarks=View(7,capacity:5);p.Update(InWorld);
        p.Key(K(Keys.Insert),cmd);
        t.Check(p.NamingText==null,"full list (5/5) → no naming");
        host.Bookmarks=View(8);p.Update(InWorld);
        p.Key(K(Keys.Insert),cmd);p.Char('Q');
        host.Bookmarks=View(9,canEdit:false);p.Update(InWorld);
        t.Check(p.NamingText==null,"CanEdit drops → naming cancelled");

        // 4. 沒接編輯宿主：不提供新增／刪除；選目的地模式也不提供
        var bare=Panel(new Host(),null);Click(bare,bare.RowRect(0),cmd);
        bare.Key(K(Keys.Delete),cmd);bare.Key(K(Keys.Insert),cmd);
        t.Check(bare.ConfirmingDelete==null&&bare.NamingText==null,"no editor → no add/delete");
        var dh=new Host();var de=new Editor();var d=Panel(dh,de);
        dh.Destination=new(5,5);d.Update(InWorld);Click(d,d.RowRect(0),cmd);
        d.Key(K(Keys.Delete),cmd);d.Key(K(Keys.Insert),cmd);
        t.Check(d.DestinationMode&&d.ConfirmingDelete==null&&d.NamingText==null,"destination mode → no add/delete");
        // 確認中再進入選目的地：確認被清掉
        dh.Destination=null;d.Update(InWorld);d.Toggle();d.Update(InWorld);Click(d,d.RowRect(0),cmd);d.Key(K(Keys.Delete),cmd);
        t.Check(d.ConfirmingDelete==101,"confirm pending");
        dh.Destination=new(6,5);d.Update(InWorld);
        t.Check(d.DestinationMode&&d.ConfirmingDelete==null&&de.Calls.Count==0,"destination request clears the pending 846");

        // 5. 排序鈕 Sorting_Button (182,42)
        var s=Panel(new Host(),null);
        t.Equal(new Rectangle(192,62,40,16),s.SortRect,"Sorting_Button at (182,42) + origin");
        t.Equal("Gludio,Aden,Kent,Oren,Bear",Names(s),"default = data order");
        t.Equal(Argb(Sort),Pixel(s,195,65),"sorting button drawn");
        Click(s,s.SortRect,cmd);
        t.Check(s.SortByName,"sort toggled on");
        t.Equal("Aden,Bear,Gludio,Kent,Oren",Names(s),"name order");
        Click(s,s.RowRect(0),cmd);
        t.Check(s.Selected==102,"rows follow the sorted order",s.Selected);
        Click(s,s.SortRect,cmd);
        t.Equal("Gludio,Aden,Kent,Oren,Bear",Names(s),"second click = data order again");
        t.Check(s.Selected==102,"selection kept across sorting");

        // 6. 區域篩選 ZoneFilter_Combobox (129,141)
        var z=Panel(new Host(),null);
        t.Equal(new Rectangle(139,161,105,19),z.ZoneExpandRect,"Expand_Button at combobox (129,141)");
        t.Equal("ALL",z.ZoneText,"label shows ALL (Item0 Caption)");
        t.Check(z.ZoneItems.SequenceEqual(new ushort?[]{null,0,4,57}),"items = ALL + distinct map ids ascending",z.ZoneItems);
        t.Check(!z.ZoneListOpen,"closed by default");
        Click(z,z.ZoneExpandRect,cmd);
        t.Check(z.ZoneListOpen,"expand button opens the list");
        t.Equal(new Rectangle(139,180,55,19),z.ZoneRowRect(0),"Expand_List row 0 at Y=19, width 55, height 19");
        t.Equal(Argb(ZoneBack),Pixel(z,140,240),"list background (X=-2) drawn over the bookmark rows");
        Click(z,z.ZoneRowRect(2),cmd);
        t.Check(!z.ZoneListOpen&&z.ZoneFilter==4&&z.ZoneText=="4","pick map 4 → filter, list closes",(z.ZoneFilter,z.ZoneText));
        t.Equal("Gludio,Kent",Names(z),"only map-4 bookmarks");
        t.Equal("5/50",z.CountText,"count label stays total/capacity");
        Click(z,z.RowRect(1),cmd);
        t.Check(z.Selected==103,"row click in the filtered list",z.Selected);
        Click(z,z.ZoneExpandRect,cmd);
        t.Equal(Argb(ZoneRow),Pixel(z,180,180+19*2+2),"current filter row draws PushSurf");
        Click(z,z.RowRect(5),cmd);
        t.Check(!z.ZoneListOpen,"click outside the list closes it");
        Click(z,z.ZoneExpandRect,cmd);z.Key(K(Keys.Escape),cmd);
        t.Check(!z.ZoneListOpen&&z.Visible,"ESC closes the list first, window stays");
        Click(z,z.ZoneExpandRect,cmd);Click(z,z.ZoneRowRect(0),cmd);
        t.Check(z.ZoneFilter==null&&z.Rows.Count==5,"ALL restores the full list");
        Click(z,z.ZoneExpandRect,cmd);Click(z,z.ZoneRowRect(3),cmd);
        t.Equal("Oren",Names(z),"map 57");
        var zh=(Host)z.Host!;zh.Bookmarks=View(2,[..Sample.Where(b=>b.MapId!=57)]);z.Update(InWorld);
        t.Check(z.ZoneFilter==null&&z.Rows.Count==4,"filtered map gone → back to ALL");
        Click(z,z.SortRect,cmd);Click(z,z.ZoneExpandRect,cmd);Click(z,z.ZoneRowRect(1),cmd);
        t.Equal("Aden,Bear",Names(z),"filter and sort combine");
        // 展開清單超過 PageSize 7：滾輪捲動
        var many=Enumerable.Range(1,10).Select(i=>new Bookmark((uint)(200+i),"M"+i,(ushort)i,1,1)).ToArray();
        zh.Bookmarks=View(3,many);z.Update(InWorld);
        Click(z,z.ZoneExpandRect,cmd);
        z.Mouse(M(UiMouseKind.Wheel,Center(z.ZoneRowRect(1)),delta:-120),cmd);
        t.Equal(1,z.ZoneScroll,"wheel scrolls the expanded list");
        Click(z,z.ZoneRowRect(0),cmd);
        t.Check(z.ZoneFilter==1,"scrolled row 0 = map 1",z.ZoneFilter);

        // 7. 缺排序鈕／缺下拉：只是不畫該件，窗仍可用
        var plain=Panel(new Host(),null,Layout(sorting:false,zone:false));
        t.Check(plain.Usable&&plain.SortRect.IsEmpty&&plain.ZoneExpandRect.IsEmpty,"layout without sorting/zone filter stays usable");
        var noSurf=Surfaces();noSurf.Remove(30);noSurf.Remove(40);
        var ns=new BookmarkWindowPanel();ns.Load(UiLayout.Parse(Layout(),"BookmarkUI.xml"),id=>noSurf.GetValueOrDefault(id));
        t.Check(ns.Usable&&ns.SortRect.IsEmpty&&ns.ZoneExpandRect.IsEmpty,"missing sorting/expand surf → those parts skipped");

        // 8. 關窗時清掉暫態（確認、命名、展開）
        var w=Panel(new Host(),new Editor());Click(w,w.RowRect(0),cmd);w.Key(K(Keys.Delete),cmd);
        ((IUiClosablePanel)w).Close();
        t.Check(!w.Visible&&w.ConfirmingDelete==null,"close drops the pending 846");
        w.Toggle();w.Key(K(Keys.Insert),cmd);w.Toggle();
        t.Check(!w.Visible&&w.NamingText==null,"toggle close drops naming");
        t.Check(!w.Char('a'),"closed window takes no chars");
    }
}
