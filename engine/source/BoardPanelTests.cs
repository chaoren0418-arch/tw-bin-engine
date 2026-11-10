using P06.NativeUi;
namespace P06;

// R5-G（T312／#739）board-native-content 離線測試：原生公告板的背景、清單列、閱讀內文與發文窗（NativeUi/BoardPanel.cs）。
// .img 全部由本檔合成（只有尺寸照反組譯的繪製矩形，像素是單色），字串表強制不載入（用內建原文），
// 不讀真實客戶端、不啟動遊戲、不連網路。
static class BoardPanelTests {
    sealed class Host:IBoardPanelHost,IBoardContentHost {
        public BoardView? Board{get;set;}
        public uint? SelectedRow{get;set;}
        public readonly List<string> Calls=[];
        public void Press(string command)=>Calls.Add(command);
        public void SelectRow(uint? id)=>SelectedRow=id;
        public void Submit(string title,string content)=>Calls.Add($"submit:{title}|{content}");
        public void Notice(string text)=>Calls.Add("notice:"+text);
    }
    static readonly BoardRow[] Rows=[new(20,"作者甲","2026/11/09","第一篇標題"),new(123456,"abcdefghijklmn","26/11/08","second")];
    static BoardView View(string kind,BoardArticle? article=null,bool canAct=true,ushort fee=300)=>
        new(1,1,1,1,1,456,kind,"",Rows,article,fee,canAct,false,false);
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint ListBg=0xFF404000,ReadBg=0xFF004040,Mark=0xFFFF00FF,Btn=0xFFFF0000,Deco=0xFF00FF00;
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [656]=Surface(656,238,350,ListBg),[657]=Surface(657,10,10,Mark),[670]=Surface(670,238,343,ReadBg),
        [672]=Surface(672,34,18,Deco),[676]=Surface(676,33,17,Deco),[686]=Surface(686,40,20,Btn),[678]=Surface(678,15,17,Btn),[674]=Surface(674,20,29,Btn)};
    static readonly IUiGameState State=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at,MouseButtons b=MouseButtons.Left,int clicks=1)=>new(k,at,b,clicks,0,Keys.None);
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static void Click(IUiPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static int Pixel(IUiPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();
    static UiKeyEvent K(Keys k)=>new(k,true,false);

    [WorldModuleTest("board-native-content")]
    static void Run(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);
        var cmd=new UiNullCommands();

        // 1. 反組譯資料逐項比對
        t.Check(BoardContentLayout.ListSize==new Size(238,350)&&BoardContentLayout.ReadSize==new Size(238,343)&&BoardContentLayout.WriteSize==new Size(238,343),"window rects 238×350 / 238×343 (sub_7CC7C0, sub_7CCB40, sub_7CCD90)");
        t.Check(BoardContentLayout.ListBackground==656&&BoardContentLayout.ReadBackground==670&&BoardContentLayout.RowHighlight==657&&BoardContentLayout.ListBackgroundAlt==4732&&BoardContentLayout.ReadBackgroundAlt==4733,"background surfs 656/670 (alt 4732/4733), highlight 657");
        t.Check((BoardContentLayout.RowsPerPage,BoardContentLayout.RowX,BoardContentLayout.FirstRowY,BoardContentLayout.RowPitch,BoardContentLayout.HighlightX,BoardContentLayout.TitleLineDy)==(8,30,50,30,18,12),"rows: 8 per page at (30, 50+30i), highlight x 18, title line +12");
        t.Check((BoardContentLayout.IdDx,BoardContentLayout.IdWidth,BoardContentLayout.AuthorDx,BoardContentLayout.AuthorWidth,BoardContentLayout.DateDx,BoardContentLayout.DateWidth,BoardContentLayout.TitleWidth)==(0,24,30,72,105,48,160),"row columns: id 24 px, author +30/72, date +105/48, title 160");
        t.Check(BoardContentLayout.WriteButtons.Select(b=>(b.Surf,b.X,b.Y)).SequenceEqual(new[]{(686,159,302),(678,213,297),(674,90,299)}),"write buttons 686 (159,302) / 678 (213,297) / 674 (90,299) (sub_7CBE90)");
        t.Check(BoardContentLayout.ReadContentBox==new Rectangle(20,67,180,228)&&BoardContentLayout.WriteContentBox==new Rectangle(20,38,180,228),"content boxes (sub_7CBA70 / sub_7CBE90)");
        t.Check(BoardContentLayout.TitleMaxBytes==20&&BoardContentLayout.ContentMaxBytes==512,"input limits 20 / 512 bytes");

        // 2. sub_83F260 字寬與字串
        t.Equal("abcdefgh",BoardText.Clip("abcdefghij",48),"clip: 6 px per single-byte char");
        t.Equal("公告",BoardText.Clip("公告板測試",24),"clip: 12 px per double-byte char");
        t.Equal("公a",BoardText.Clip("公a告",23),"clip: stops when the next char does not fit");
        t.Equal("",BoardText.Clip("公",11),"clip: nothing when even one char does not fit");
        t.Equal(36,BoardText.LabelWidth("題目:"),"label width (strlen+1)*6");
        t.Equal(60,BoardText.LabelWidth("討論號碼:"),"label width of 543");
        t.Equal("3456",BoardText.Number(123456),"%04d of id % 10000");
        t.Equal("0007",BoardText.Number(7),"%04d pads");
        t.Equal("題目:",BoardText.Text(541),"541 without a table");
        t.Equal("請輸入題目。",BoardText.Text(544),"544 without a table");
        t.Check(BoardText.Wrap(new string('a',31),180).SequenceEqual(new[]{new string('a',30),"a"}),"wrap at 180 px");
        t.Check(BoardText.Wrap("x\r\ny",180).SequenceEqual(new[]{"x","y"}),"wrap at line breaks");

        // 3. 模式與可見
        var host=new Host();var buttons=new BoardPanelSet(host);var set=new BoardContentSet(buttons,host);
        var map=Surfaces();set.LoadSurfaces(id=>map.GetValueOrDefault(id));
        void Sync(){foreach(var p in set.Panels)p.Update(State);}
        t.Check(set.Panels.All(p=>p is IUiClosablePanel),"all content panels are closable");
        t.Check(set.List.Layer==UiLayer.Bar&&set.Read.Layer==UiLayer.Bar&&set.Write.Layer==UiLayer.Window,"list/read content on the bar layer, write window on the window layer");
        t.Check(set.Write is IUiTextInput,"write window takes text input");
        Sync();t.Check(!set.List.Visible&&!set.Read.Visible&&!set.Write.Visible,"no board → nothing");
        host.Board=View("list");Sync();
        var o=set.List.Origin;
        t.Check(set.List.Visible&&!set.Read.Visible&&o==BoardWindowLayout.DefaultOrigin&&set.List.Bounds==new Rectangle(o,new Size(238,350)),"list → list content at the button windows' origin");

        // 4. 清單列文字
        var texts=set.List.Texts();
        t.Equal(8,texts.Count,"2 rows × 4 texts");
        t.Equal(new BoardTextItem("0020",o.X+30,o.Y+50,8),texts[0],"row 0 number");
        t.Equal(new BoardTextItem("作者甲",o.X+60,o.Y+50,8),texts[1],"row 0 author");
        t.Equal(new BoardTextItem("2026/11/",o.X+135,o.Y+50,8),texts[2],"row 0 date clipped to 48 px");
        t.Equal(new BoardTextItem("第一篇標題",o.X+30,o.Y+62,8),texts[3],"row 0 title on the second line");
        t.Equal(new BoardTextItem("3456",o.X+30,o.Y+80,8),texts[4],"row 1 at y+30, id % 10000");
        t.Equal("abcdefghijkl",texts[5].Text,"row 1 author clipped to 72 px");

        // 5. 繪製：背景與選取標記
        t.Equal(Argb(ListBg),Pixel(set.List,o.X+5,o.Y+5),"list background 656 drawn at the origin");
        t.Equal(Argb(ListBg),Pixel(set.List,o.X+20,o.Y+52),"no highlight without a selection");
        host.SelectedRow=123456;
        t.Equal(1,set.List.SelectedIndex,"selection follows the host");
        t.Equal(Argb(Mark),Pixel(set.List,o.X+20,o.Y+82),"highlight 657 at (18, y) of the selected row");

        // 6. 滑鼠：選列、取消、雙擊閱讀、右鍵雙擊關窗
        host.SelectedRow=null;
        t.Equal(0,set.List.RowAt(new(o.X+230,o.Y+55)),"row hit uses y only");
        t.Equal(-1,set.List.RowAt(new(o.X+40,o.Y+49)),"above the first row → none");
        t.Equal(-1,set.List.RowAt(new(o.X+40,o.Y+115)),"row 2 has no data → none");
        Click(set.List,new(o.X+40,o.Y+85),cmd);t.Equal<uint?>(123456,host.SelectedRow,"click selects row 1");
        Click(set.List,new(o.X+40,o.Y+85),cmd);t.Equal<uint?>(null,host.SelectedRow,"click on the selected row clears it");
        Click(set.List,new(o.X+40,o.Y+130),cmd);t.Check(host.SelectedRow==null&&host.Calls.Count==0,"click on an empty row does nothing");
        t.Check(set.List.Mouse(M(UiMouseKind.Down,new(o.X+40,o.Y+55),clicks:2),cmd)&&host.SelectedRow==20&&host.Calls.SequenceEqual(new[]{"read"}),"double click selects and reads");
        host.Calls.Clear();
        set.List.Mouse(M(UiMouseKind.Down,new(o.X+40,o.Y+200),MouseButtons.Right,2),cmd);t.Check(host.Calls.SequenceEqual(new[]{"close"}),"right double click closes");
        host.Calls.Clear();
        t.Check(!set.List.Mouse(M(UiMouseKind.Down,new(o.X+240,o.Y+5)),cmd),"outside the window → not handled");
        t.Check(set.List.Mouse(M(UiMouseKind.Down,new(o.X+5,o.Y+5)),cmd)&&host.Calls.Count==0,"background press is swallowed");
        set.List.Close();t.Check(host.Calls.SequenceEqual(new[]{"close"}),"evicting the list closes the board");
        host.Calls.Clear();

        // 7. 閱讀窗
        var article=new BoardArticle(20,"作者甲","2026/11/09","標題文字",new string('b',31)+"\r\n第二行");
        host.Board=View("article",article);Sync();
        var r=set.Read.Origin;
        t.Check(set.Read.Visible&&!set.List.Visible,"article → read content");
        var rt=set.Read.Texts();
        t.Equal(new BoardTextItem("作者:",r.X+20,r.Y+20,1),rt[0],"542 label at (20,20), palette 1");
        t.Equal(new BoardTextItem("作者甲",r.X+56,r.Y+20,8),rt[1],"author after the label");
        t.Equal(new BoardTextItem("討論號碼:",r.X+20,r.Y+35,1),rt[2],"543 label at (20,35)");
        t.Equal(new BoardTextItem("0020 - 2026/11/09",r.X+80,r.Y+35,8),rt[3],"\"%04d - %s\" after 543");
        t.Equal(new BoardTextItem("題目:",r.X+20,r.Y+50,1),rt[4],"541 label at (20,50)");
        t.Equal(new BoardTextItem("標題文字",r.X+56,r.Y+50,8),rt[5],"title box at (20+label, 50)");
        t.Check(rt.Skip(6).Select(x=>(x.Text,x.X,x.Y)).SequenceEqual(new[]{(new string('b',30),r.X+20,r.Y+67),("b",r.X+20,r.Y+79),("第二行",r.X+20,r.Y+91)}),"content wrapped in (20,67) 180 px, 12 px lines");
        t.Equal(Argb(ReadBg),Pixel(set.Read,r.X+5,r.Y+5),"read background 670");
        t.Check(set.Read.Mouse(M(UiMouseKind.Down,new(r.X+5,r.Y+5)),cmd)&&host.Calls.Count==0,"read background press swallowed");

        // 8. 發文窗：開窗條件
        t.Check(set.OpenWrite()&&set.Write.Visible&&set.Write.Fee==300,"write opens from a shown article, fee kept");
        set.Write.Close();
        host.Board=View("list-wait");Sync();t.Check(!set.OpenWrite()&&!set.Write.Visible,"no write window while waiting");
        host.Board=View("list",canAct:false);Sync();t.Check(!set.OpenWrite(),"no write window when the board cannot act");
        host.Board=View("list");Sync();t.Check(set.OpenWrite()&&set.Write.Visible&&set.Write.Focus==0,"write opens from the list, title focused");

        // 9. 發文窗：版面
        var w=set.Write.Origin;
        t.Check(set.Write.TitleBox==new Rectangle(w.X+56,w.Y+20,144,18)&&set.Write.ContentBox==new Rectangle(w.X+20,w.Y+38,180,228),"title box (20+label,20) (180−label)×18, content box (20,38) 180×228");
        var wt=set.Write.Texts();
        t.Equal(new BoardTextItem("題目:",w.X+20,w.Y+20,1),wt[0],"541 at (20,20)");
        t.Equal(new BoardTextItem("費用為金幣 300。",w.X+18,w.Y+269,8),wt[1],"540 fee line at (18,269)");
        t.Equal(Argb(ReadBg),Pixel(set.Write,w.X+5,w.Y+5),"write background 670");
        t.Equal(Argb(Deco),Pixel(set.Write,w.X+60,w.Y+315),"672 decoration at (56,312)");
        t.Equal(Argb(Btn),Pixel(set.Write,w.X+165,w.Y+305),"686 button at (159,302)");
        host.Board=View("list",fee:0);Sync();set.OpenWrite();
        t.Check(set.Write.Texts().All(x=>!x.Text.Contains("費用")),"fee 0 → no fee line");

        // 10. 發文窗：輸入
        foreach(char c in "公告板測試公告板測試公")set.Write.Char(c);
        t.Equal("公告板測試公告板測試",set.Write.Title,"title stops at 20 bytes");
        set.Write.Key(K(Keys.Back),cmd);t.Equal("公告板測試公告板測",set.Write.Title,"backspace removes one char");
        t.Check(set.Write.Key(K(Keys.Enter),cmd)&&set.Write.Focus==1,"Enter in the title moves to the content");
        foreach(char c in "ab")set.Write.Char(c);set.Write.Key(K(Keys.Enter),cmd);set.Write.Char('c');
        t.Equal("ab\nc",set.Write.Content,"Enter in the content is a line break");
        t.Check(!set.Write.Key(K(Keys.A),cmd),"printable keys are left to WM_CHAR");
        set.Write.Key(K(Keys.Tab),cmd);t.Equal(0,set.Write.Focus,"Tab switches the box");
        Click(set.Write,Center(set.Write.ContentBox),cmd);t.Equal(1,set.Write.Focus,"click on the content box focuses it");
        Click(set.Write,Center(set.Write.TitleBox),cmd);t.Equal(0,set.Write.Focus,"click on the title box focuses it");

        // 11. 發文窗：送出、544、關閉
        host.Calls.Clear();
        Click(set.Write,Center(set.Write.ButtonRect(0)),cmd);
        t.Check(host.Calls.SequenceEqual(new[]{"submit:公告板測試公告板測|ab\r\nc"})&&!set.Write.Visible,"686 submits title and content (CRLF) and closes");
        host.Calls.Clear();set.OpenWrite();set.Write.Char(' ');
        Click(set.Write,Center(set.Write.ButtonRect(0)),cmd);
        t.Check(host.Calls.SequenceEqual(new[]{"notice:請輸入題目。"})&&set.Write.Visible,"blank title → 544, window stays");
        Click(set.Write,Center(set.Write.ButtonRect(1)),cmd);t.Check(!set.Write.Visible,"678 closes the write window");
        set.OpenWrite();Click(set.Write,Center(set.Write.ButtonRect(2)),cmd);t.Check(!set.Write.Visible&&host.Calls.Count==1,"674 goes back without sending");
        set.OpenWrite();set.Write.Key(K(Keys.Escape),cmd);t.Check(!set.Write.Visible,"Esc closes the write window");
        set.OpenWrite();var bp=Center(set.Write.ButtonRect(0));
        set.Write.Mouse(M(UiMouseKind.Down,bp),cmd);set.Write.Mouse(M(UiMouseKind.Up,Center(set.Write.ButtonRect(1))),cmd);
        t.Check(set.Write.Visible&&host.Calls.Count==1,"press on one button, release on another → nothing");
        t.Check(!set.Write.Char('x')||set.Write.Title=="x","typing goes to the open write window");
        host.Board=View("closed");Sync();t.Check(!set.Write.Visible&&!set.Write.IsOpen&&!set.Write.Char('y'),"closed board closes the write window");

        // 12. 疊放（同 BoardUi.AttachNativeBoard 的加入順序）
        host.Board=View("list");
        var root=new UiRoot();root.Add(set.List);root.Add(set.Read);foreach(var p in buttons.Panels)root.Add(p);root.Add(set.Write);
        root.Update(State);
        var rowPoint=new Point(o.X+40,o.Y+55);
        t.Check(root.HitTest(rowPoint)==set.List,"row point → list content");
        t.Check(!root.WindowStack.Contains(set.List),"list content takes no window-stack slot");
        set.OpenWrite();root.Update(State);
        t.Check(root.HitTest(rowPoint)==set.Write&&root.WindowStack.Contains(set.Write),"open write window is above the list");
        var order=root.PaintOrder().ToList();
        t.Check(order.IndexOf(set.List)<order.IndexOf(buttons.List)&&order.IndexOf(buttons.List)<order.IndexOf(set.Write),"paint order: list content < list buttons < write window");
    }
}
