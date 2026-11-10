using P06.NativeUi;
namespace P06;

// R9-A（T361／#845）bookmark-window-original 離線測試：書籤窗照原版操作（FU9§1）。
// 拿掉右鍵刪除；Delete 多選（846 後由大到小一筆一個 C3）；Insert＝加進快速移動清單（本次登入有效，第 6 筆 2828）；
// 排序去色碼；區域第 0 項 878；確認鈕不關窗；選目的地雙擊；隨機傳送鈕（id 0）。
// XML 與 .img 全部由本檔合成（元素名稱與座標照 BookmarkUI.xml，視窗原點 (10,20)），不讀真實客戶端、不啟動遊戲、不連網路；
// 字串表以 ServerMessageText.UseTable(null) 固定成「沒有表」，用面板內建的原文。
static class BookmarkWindowOriginalTests {
    sealed class Host:IBookmarkWindowHost {
        public BookmarkView Bookmarks{get;set;}=View(1);
        public TeleportDestinationRequest? Destination{get;set;}
        public bool Sending{get;set;}
        public bool AcceptsRandomTeleport{get;set;}
        public readonly List<string> Calls=[];
        public void Teleport(TeleportDestinationRequest shown,uint bookmarkId)=>Calls.Add($"teleport:{shown.Revision}:{bookmarkId}");
        public void CancelTeleport(TeleportDestinationRequest shown)=>Calls.Add($"cancel:{shown.Revision}");
    }
    /// <summary>有 DeleteMany 的宿主（同 BookmarkUi.cs 的 BookmarkEditHost）。</summary>
    sealed class Editor:IBookmarkEditHost {
        public bool Sending{get;set;}
        public readonly List<string> Calls=[];
        public void Add(BookmarkView shown,string name)=>Calls.Add($"add:{shown.Revision}:{name}");
        public void Delete(BookmarkView shown,uint bookmarkId)=>Calls.Add($"delete:{shown.Revision}:{bookmarkId}");
        public void DeleteMany(BookmarkView shown,IReadOnlyList<uint> ids)=>Calls.Add($"many:{shown.Revision}:{string.Join(",",ids)}");
    }
    /// <summary>沒實作 DeleteMany 的宿主：介面預設逐筆呼叫 Delete。</summary>
    sealed class PlainEditor:IBookmarkEditHost {
        public bool Sending=>false;
        public readonly List<string> Calls=[];
        public void Add(BookmarkView shown,string name)=>Calls.Add("add");
        public void Delete(BookmarkView shown,uint bookmarkId)=>Calls.Add($"delete:{bookmarkId}");
    }
    static readonly Bookmark[] Sample=[new(101,"Gludio",4,1,1),new(102,"Aden",0,1,1),new(103,"Kent",4,1,1),new(104,"Oren",57,1,1),new(105,"Bear",0,1,1),
        new(106,"Giran",4,1,1),new(107,"Heine",4,1,1)];
    static BookmarkView View(int revision,Bookmark[]? items=null,int session=1,uint actor=7)=>new(session,1,revision,actor,true,50,"已收到伺服器書籤清單。",items??Sample);

    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Frame=0xFF808080,Base=0xFF202020,Btn=0xFFFF0000,Row=0xFF0000FF,Ok=0xFF00FF00,QuickRow=0xFF00FFFF,QuickTitle=0xFF406080,Random=0xFFFFA0A0;
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [1]=Surface(1,263,420,Frame),[2]=Surface(2,251,407,Base),[3]=Surface(3,12,12,Btn),[6]=Surface(6,50,12,QuickTitle),[7]=Surface(7,50,12,Base),
        [8]=Surface(8,200,17,Row),[16]=Surface(16,60,18,Ok),[19]=Surface(19,60,18,Ok),[20]=Surface(20,60,18,Btn),
        [45]=Surface(45,120,15,QuickRow),[50]=Surface(50,40,16,Random),
    };
    static string Layout(bool quick=true,bool random=true){
        var rows=string.Concat(Enumerable.Range(0,12).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" Caption=\"temp{i}\" PushSurf=\"8;4\" Activate=\"0\" Height=\"17\"/>"));
        var quickRows=string.Concat(Enumerable.Range(0,5).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" String=\"{i}\" PushSurf=\"45;4\" Activate=\"1\" Height=\"15\" MouseEvent=\"QuickList\"/>"));
        return "<?xml version=\"1.0\"?><BookmarkWindowUI Toggle=\"true\">"+
            "<Window Name=\"Bookmark_Window\" X=\"10\" Y=\"20\" FrameSurf=\"1;1\" Moveable=\"20\">"+
            "<Image Name=\"Base_Image\" Surf=\"2;0\" X=\"6\" Y=\"9\"/>"+
            "<Button Name=\"Exit_Button\" X=\"249\" Y=\"15\" BaseSurf=\"3\"/>"+
            "<Image Name=\"QuickMoveListTitle_Image\" Surf=\"6;1\" X=\"24\" Y=\"46\"/>"+
            "<Image Name=\"MemoryZoneListTitle_Image\" Surf=\"7;1\" X=\"24\" Y=\"145\"/>"+
            (quick?"<MovableList Name=\"QuickMove_List\" X=\"23\" Y=\"62\" Width=\"120\" Height=\"75\" Vertical=\"1\" MouseEvent=\"QuickMoveList\">"+quickRows+"</MovableList>":"")+
            (random?"<Button Name=\"RandomTeleport_Button\" BaseSurf=\"50\" PushSurf=\"51;1\" HighlightSurf=\"52\" X=\"129\" Y=\"42\" MouseEvent=\"RandomTeleport\"/>":"")+
            "<MovableList Name=\"MemoryPlace_List\" X=\"22\" Y=\"162\" Width=\"200\" Height=\"204\" Vertical=\"1\">"+rows+"</MovableList>"+
            "<Button Name=\"Confirm_Button\" BaseSurf=\"16\" DisableSurf=\"19\" X=\"67\" Y=\"395\"/>"+
            "<Button Name=\"Cancel_Button\" BaseSurf=\"20\" X=\"134\" Y=\"395\"/>"+
            "<Label Name=\"BookmarkCount_Label\" X=\"200\" Y=\"397\" Width=\"20\" Height=\"15\"/>"+
            "</Window></BookmarkWindowUI>";
    }
    static BookmarkWindowPanel Panel(Host host,IBookmarkEditHost? editor,string? xml=null){
        var map=Surfaces();var p=new BookmarkWindowPanel();
        p.Load(UiLayout.Parse(xml??Layout(),"BookmarkUI.xml"),id=>map.GetValueOrDefault(id));
        p.Attach(host);if(editor!=null)p.AttachEditor(editor);
        p.Update(InWorld);p.Toggle();p.Update(InWorld);
        return p;
    }
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true),OutOfWorld=new UiStaticGameState(InWorld:false);
    static UiMouseEvent M(UiMouseKind k,Point at,MouseButtons b=MouseButtons.Left,int clicks=1,Keys mods=Keys.None)=>new(k,at,b,clicks,0,mods);
    static void Click(BookmarkWindowPanel p,Rectangle r,UiNullCommands c,Keys mods=Keys.None,int clicks=1){var at=Center(r);p.Mouse(M(UiMouseKind.Down,at,clicks:clicks,mods:mods),c);p.Mouse(M(UiMouseKind.Up,at,clicks:clicks,mods:mods),c);}
    static void DoubleClick(BookmarkWindowPanel p,Rectangle r,UiNullCommands c){Click(p,r,c);Click(p,r,c,clicks:2);}
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static UiKeyEvent K(Keys k)=>new(k,true,false);
    static string Names(IEnumerable<Bookmark> rows)=>string.Join(",",rows.Select(r=>r.Name));
    static int Pixel(BookmarkWindowPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("bookmark-window-original")]
    static void Run(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);
        var cmd=new UiNullCommands();

        // 0. 字串（本機 Text.pak string-c.tbl 查到的原文；表不存在時用這份）
        t.Equal("全部",BookmarkWindowPanel.Text(BookmarkWindowPanel.StringAll),"878 = 全部");
        t.Equal("快速移動目錄最多只能登錄5個為止。",BookmarkWindowPanel.Text(BookmarkWindowPanel.StringQuickFull),"2828 text");
        t.Equal(0u,BookmarkWindowPanel.RandomTeleportId,"random teleport = bookmark id 0");

        // 1. 沒有右鍵刪除（sub_BDE250 只處理左鍵）
        var host=new Host();var ed=new Editor();var p=Panel(host,ed);
        p.Mouse(M(UiMouseKind.Down,Center(p.RowRect(2)),MouseButtons.Right),cmd);p.Mouse(M(UiMouseKind.Up,Center(p.RowRect(2)),MouseButtons.Right),cmd);
        t.Check(p.Selected==null&&p.ConfirmingDelete==null&&ed.Calls.Count==0,"right click: no select, no 846, nothing sent");

        // 2. Delete 多選：Ctrl 切換、Shift 範圍、最多 12；846「是」→ 由大到小
        Click(p,p.RowRect(0),cmd);Click(p,p.RowRect(2),cmd,Keys.Control);Click(p,p.RowRect(4),cmd,Keys.Control);
        t.Check(p.Marked.SequenceEqual([101u,103u,105u])&&p.Selected==105,"ctrl-click marks rows 0,2,4",p.Marked);
        t.Equal(Argb(Row),Pixel(p,200,182+17*2+2),"marked row 2 draws PushSurf");
        t.Equal(Argb(Row),Pixel(p,200,182+2),"marked row 0 draws PushSurf");
        Click(p,p.RowRect(2),cmd,Keys.Control);
        t.Check(p.Marked.SequenceEqual([101u,105u]),"ctrl-click again unmarks",p.Marked);
        p.Key(K(Keys.Delete),cmd);
        t.Check(p.ConfirmingDeleteIds is {} c1&&c1.SequenceEqual([105u,101u])&&ed.Calls.Count==0,"846 for both, high index first",p.ConfirmingDeleteIds);
        p.Key(K(Keys.Y),cmd);
        t.Check(ed.Calls.SequenceEqual(["many:1:105,101"]),"yes → DeleteMany in descending order",ed.Calls);
        ed.Calls.Clear();
        Click(p,p.RowRect(1),cmd);Click(p,p.RowRect(3),cmd,Keys.Shift);
        t.Check(p.Marked.SequenceEqual([102u,103u,104u])&&p.Selected==102,"shift-click marks the range, anchor stays",p.Marked);
        Click(p,p.RowRect(0),cmd,Keys.Shift);
        t.Check(p.Marked.SequenceEqual([102u,101u]),"shift range upwards from the anchor",p.Marked);
        p.Key(K(Keys.Delete),cmd);p.Key(K(Keys.Escape),cmd);
        t.Check(p.ConfirmingDelete==null&&ed.Calls.Count==0&&p.Visible,"ESC = no");
        Click(p,p.RowRect(3),cmd);
        t.Check(p.Marked.SequenceEqual([104u]),"plain click = single selection",p.Marked);
        p.Key(K(Keys.Delete),cmd);p.Key(K(Keys.Enter),cmd);
        t.Check(ed.Calls.SequenceEqual(["delete:1:104"]),"single selection → one Delete",ed.Calls);
        // 多選中有一筆已送刪除 → 不進 846
        host.Bookmarks=View(2,[..Sample.Select(b=>b.Id==103?b with{DeletePending=true}:b)]);p.Update(InWorld);
        Click(p,p.RowRect(1),cmd);Click(p,p.RowRect(2),cmd,Keys.Control);p.Key(K(Keys.Delete),cmd);
        t.Check(p.ConfirmingDelete==null,"marked set with a delete-pending bookmark is refused");
        // 確認中有一筆消失 → 確認取消
        host.Bookmarks=View(3);p.Update(InWorld);
        Click(p,p.RowRect(1),cmd);Click(p,p.RowRect(2),cmd,Keys.Control);p.Key(K(Keys.Delete),cmd);
        t.Check(p.ConfirmingDeleteIds?.Count==2,"two pending");
        host.Bookmarks=View(4,[..Sample.Where(b=>b.Id!=102)]);p.Update(InWorld);
        t.Check(p.ConfirmingDelete==null,"one confirmed bookmark vanished → whole confirm dropped");
        // 最多 12 筆
        var many=Enumerable.Range(1,15).Select(i=>new Bookmark((uint)(300+i),"M"+i,4,1,1)).ToArray();
        host.Bookmarks=View(5,many);p.Update(InWorld);
        Click(p,p.RowRect(0),cmd);for(int i=0;i<3;i++)p.Mouse(M(UiMouseKind.Wheel,Center(p.ListRect)) with{Delta=-120},cmd);
        Click(p,p.RowRect(11),cmd,Keys.Shift);
        t.Check(p.Marked.Count==BookmarkWindowPanel.MaxMarked&&p.Marked[0]==301,"shift range capped at 12",p.Marked.Count);
        for(int i=0;i<3;i++)p.Mouse(M(UiMouseKind.Wheel,Center(p.ListRect)) with{Delta=120},cmd);
        // 介面預設的 DeleteMany：逐筆 Delete
        var plain=new PlainEditor();var pp=Panel(new Host(),plain);
        Click(pp,pp.RowRect(0),cmd);Click(pp,pp.RowRect(1),cmd,Keys.Control);pp.Key(K(Keys.Delete),cmd);pp.Key(K(Keys.Y),cmd);
        t.Check(plain.Calls.SequenceEqual(["delete:102","delete:101"]),"default DeleteMany = Delete per id, descending",plain.Calls);
        // 選目的地模式：只單選
        var dh=new Host();var d=Panel(dh,new Editor());dh.Destination=new(5,5);d.Update(InWorld);
        Click(d,d.RowRect(0),cmd);Click(d,d.RowRect(2),cmd,Keys.Control);
        t.Check(d.Marked.SequenceEqual([103u])&&d.Selected==103,"destination mode: ctrl-click is a plain click",d.Marked);

        // 3. Insert＝加進快速移動清單（本次登入有效，最多 5 筆）
        var qh=new Host();var qe=new Editor();var q=Panel(qh,qe);
        t.Check(q.HasQuickList,"QuickMove_List read from the layout");
        t.Equal(new Rectangle(33,82,120,15),q.QuickRowRect(0),"QuickMove_List (23,62) row 0, height 15");
        t.Equal(Argb(QuickTitle),Pixel(q,36,68),"quick list title drawn when the list is usable");
        q.Key(K(Keys.Insert),cmd);
        t.Check(q.QuickIds.Count==0,"Insert without a selection adds nothing");
        Click(q,q.RowRect(0),cmd);q.Key(K(Keys.Insert),cmd);
        t.Check(q.QuickIds.SequenceEqual([101u])&&qe.Calls.Count==0,"Insert adds the selected bookmark, no packet",q.QuickIds);
        q.Key(K(Keys.Insert),cmd);
        t.Check(q.QuickIds.Count==1,"same bookmark not added twice");
        t.Equal("Gludio",Names(q.QuickRows),"quick row shows the bookmark name");
        foreach(int r in new[]{1,2,3,4}){Click(q,q.RowRect(r),cmd);q.Key(K(Keys.Insert),cmd);}
        t.Check(q.QuickIds.Count==5&&q.Message==null,"5 entries");
        Click(q,q.RowRect(5),cmd);q.Key(K(Keys.Insert),cmd);
        t.Check(q.QuickIds.Count==5&&q.Message=="快速移動目錄最多只能登錄5個為止。","6th → 2828",q.Message);
        Click(q,q.QuickRowRect(1),cmd);
        t.Check(q.QuickFocus&&q.QuickSelected==102,"click a quick row → quick list focus",q.QuickSelected);
        t.Equal(Argb(QuickRow),Pixel(q,140,82+15+2),"selected quick row draws PushSurf 4345");
        q.Key(K(Keys.Delete),cmd);
        t.Check(q.QuickIds.SequenceEqual([101u,103u,104u,105u])&&q.ConfirmingDelete==null&&qe.Calls.Count==0,"Delete on the quick list only removes it (no 846, no C3)",q.QuickIds);
        Click(q,q.RowRect(0),cmd);
        t.Check(!q.QuickFocus,"clicking the memory list moves focus back");
        // 書籤被刪掉 → 從快速移動清單消失；離開世界又回同一角色 → 保留；換角色或重新連線 → 清空
        qh.Bookmarks=View(2,[..Sample.Where(b=>b.Id!=103)]);q.Update(InWorld);
        t.Check(q.QuickIds.SequenceEqual([101u,104u,105u]),"deleted bookmark leaves the quick list",q.QuickIds);
        q.Update(OutOfWorld);q.Update(InWorld);
        t.Check(q.QuickIds.Count==3,"same session and character → kept");
        qh.Bookmarks=View(1,actor:8);q.Update(InWorld);
        t.Check(q.QuickIds.Count==0,"other character → cleared (3.81 cannot save the quick list)");
        t.Check(!q.Visible,"leaving the world closed the window");
        q.Toggle();Click(q,q.RowRect(0),cmd);q.Key(K(Keys.Insert),cmd);
        t.Check(q.QuickIds.Count==1,"new entry for the new character");
        qh.Bookmarks=View(1,session:2,actor:8);q.Update(InWorld);
        t.Check(q.QuickIds.Count==0,"new login session → cleared");
        // 沒有 QuickMove_List：Insert 不動作、標題不畫
        var nq=Panel(new Host(),null,Layout(quick:false));
        Click(nq,nq.RowRect(0),cmd);nq.Key(K(Keys.Insert),cmd);
        t.Check(!nq.HasQuickList&&nq.QuickIds.Count==0&&nq.QuickRowRect(0).IsEmpty,"no QuickMove_List → Insert does nothing");
        t.Equal(Argb(Base),Pixel(nq,36,68),"quick title not drawn without the list");

        // 4. 排序：名稱去色碼後 strcmp；關窗歸 0
        var named=new Bookmark[]{new(1,"\\f3Zeta",4,1,1),new(2,"Alpha",4,1,1),new(3,"\\a1\\d5Beta",4,1,1),new(4,"\\fYAlpha",4,1,1)};
        t.Equal("Beta",BookmarkWindowPanel.SortName("\\a1\\d5Beta"),"color and icon codes stripped");
        t.Check(BookmarkWindowPanel.CompareNames("\\f3Zeta","Alpha")>0&&BookmarkWindowPanel.CompareNames("a","B")>0,"strcmp order (byte values, case-sensitive)");
        var sh=new Host{Bookmarks=View(1,named)};var s=Panel(sh,null,Layout().Replace("</Window>","<Button Name=\"Sorting_Button\" BaseSurf=\"16\" X=\"182\" Y=\"42\"/></Window>"));
        t.Equal("\\f3Zeta,Alpha,\\a1\\d5Beta,\\fYAlpha",Names(s.Rows),"server order by default");
        Click(s,s.SortRect,cmd);
        t.Equal("Alpha,\\fYAlpha,\\a1\\d5Beta,\\f3Zeta",Names(s.Rows),"name order ignores color codes; equal names by id");
        s.Toggle();s.Toggle();
        t.Check(!s.SortByName&&Names(s.Rows).StartsWith("\\f3Zeta",StringComparison.Ordinal),"closing resets the sort (this+624 = 0)");

        // 5. 區域第 0 項＝878
        t.Equal("全部",p.ZoneText,"zone item 0 text = 878");
        t.Equal("全部",BookmarkWindowPanel.ZoneItemText(null),"null item = 878");
        t.Equal("57",BookmarkWindowPanel.ZoneItemText(57),"other items keep the map id (zone table source not verified)");

        // 6. 確認鈕不關窗（一般模式）
        var ch=new Host();var cp=Panel(ch,new Editor());Click(cp,cp.RowRect(0),cmd);cmd.Calls.Clear();
        Click(cp,cp.ConfirmRect,cmd);
        t.Check(cp.Visible&&ch.Calls.Count==0&&cmd.Calls.Count==0,"normal confirm keeps the window open, sends nothing",cmd.Calls);

        // 7. 選目的地雙擊
        var th=new Host();var tp=Panel(th,null);
        DoubleClick(tp,tp.RowRect(1),cmd);
        t.Check(th.Calls.Count==0&&tp.Visible,"normal mode double-click does nothing (rename not supported by 3.81)");
        th.Destination=new(9,5);tp.Update(InWorld);
        Click(tp,tp.RowRect(0),cmd);Click(tp,tp.RowRect(2),cmd,clicks:2);
        t.Check(th.Calls.Count==0&&tp.Selected==103,"double-click on a different row only selects it",th.Calls);
        DoubleClick(tp,tp.RowRect(3),cmd);
        t.Check(th.Calls.SequenceEqual(["teleport:9:104"]),"double-click a memory row → teleport once",th.Calls);
        th.Calls.Clear();th.Sending=true;DoubleClick(tp,tp.RowRect(4),cmd);
        t.Check(th.Calls.Count==0,"no teleport while sending");
        th.Sending=false;
        // 快速移動清單：選目的地模式雙擊＝傳送；確認鈕也用快速移動清單的選取
        th.Destination=null;tp.Update(InWorld);tp.Toggle();tp.Update(InWorld);
        Click(tp,tp.RowRect(5),cmd);tp.Key(K(Keys.Insert),cmd);Click(tp,tp.RowRect(6),cmd);tp.Key(K(Keys.Insert),cmd);
        th.Destination=new(10,5);tp.Update(InWorld);
        DoubleClick(tp,tp.QuickRowRect(1),cmd);
        t.Check(th.Calls.SequenceEqual(["teleport:10:107"]),"double-click a quick row in destination mode → teleport",th.Calls);
        Click(tp,tp.QuickRowRect(0),cmd);Click(tp,tp.ConfirmRect,cmd);
        t.Check(th.Calls.Last()=="teleport:10:106","confirm uses the quick-list selection when it has focus",th.Calls);

        // 8. 隨機傳送鈕：只在選目的地模式、宿主接受 id 0 時畫
        var rh=new Host{AcceptsRandomTeleport=true};var rp=Panel(rh,null);
        t.Check(!rp.RandomVisible&&rp.RandomRect.IsEmpty,"normal mode: random teleport hidden (sub_BDC800)");
        rh.Destination=new(11,5);rp.Update(InWorld);
        t.Equal(new Rectangle(139,62,40,16),rp.RandomRect,"RandomTeleport_Button (129,42) + origin");
        t.Equal(Argb(Random),Pixel(rp,141,64),"random teleport drawn");
        Click(rp,rp.RandomRect,cmd);
        t.Check(rh.Calls.SequenceEqual(["teleport:11:0"]),"click → teleport with bookmark id 0",rh.Calls);
        rh.Sending=true;Click(rp,rp.RandomRect,cmd);
        t.Check(rh.Calls.Count==1,"no second send while sending");
        rh.Sending=false;
        var xh=new Host();var xp=Panel(xh,null);xh.Destination=new(12,5);xp.Update(InWorld);
        t.Check(!xp.RandomVisible&&xp.RandomRect.IsEmpty,"host that rejects id 0 (RingWindows today) → button not drawn (rule 5)");
        t.Equal(Argb(Base),Pixel(xp,141,64),"no button pixels");
        Click(xp,new Rectangle(139,62,40,16),cmd);
        t.Check(xh.Calls.Count==0,"clicking where the hidden button would be sends nothing");
        var nh=new Host{AcceptsRandomTeleport=true};var nr=Panel(nh,null,Layout(random:false));nh.Destination=new(13,5);nr.Update(InWorld);
        t.Check(nr.DestinationMode&&!nr.RandomVisible,"layout without RandomTeleport_Button → no button");
    }
}
