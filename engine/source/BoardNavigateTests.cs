using P06.NativeUi;
namespace P06;

// R9-N（T374／#858）board-navigate 離線測試：閱讀窗 672／676（followups-9 §3.2，已驗證）。
//   - 672＝上一篇：忙碌不動；索引 ≤ 0 關閱讀窗；否則讀索引 −1（C_BoardRead）。
//   - 676＝下一篇：下一列已載入就讀它；否則有下一頁就先載下一頁（C_BoardBack）；都沒有就關閱讀窗。
// .img 全部由本檔合成（只有尺寸）；不讀真實客戶端、不開窗、不連網路。
static class BoardNavigateTests {
    sealed class Host:IBoardPanelHost,IBoardContentHost {
        public BoardView? Board{get;set;}
        public uint? SelectedRow{get;set;}
        public readonly List<string> Calls=[];
        public void Press(string command)=>Calls.Add(command);
        public void SelectRow(uint? id)=>SelectedRow=id;
        public void Submit(string title,string content)=>Calls.Add("submit");
        public void Notice(string text)=>Calls.Add("notice:"+text);
    }
    // 伺服器送來的順序：新到舊（id 遞減）。
    static readonly BoardRow[] Rows=[new(30,"甲","2026/11/09","三"),new(20,"乙","2026/11/08","二"),new(10,"丙","2026/11/07","一")];
    static BoardArticle A(uint id)=>new(id,"甲","2026/11/09","標題","內容");
    static BoardView View(string kind,BoardArticle? article,bool canAct=true,bool canNext=false,BoardRow[]? rows=null)
        =>new(1,1,1,1,1,456,kind,"",rows??Rows,article,300,canAct,false,canNext);
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFFFF0000u,w*h).ToArray()));
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [656]=Surface(656,238,350),[670]=Surface(670,238,343),
        [658]=Surface(658,17,17),[660]=Surface(660,23,13),[662]=Surface(662,31,26),[664]=Surface(664,31,26),[666]=Surface(666,35,23),[668]=Surface(668,35,22),
        [672]=Surface(672,34,18),[674]=Surface(674,20,29),[676]=Surface(676,33,17),[678]=Surface(678,15,17)};
    static readonly IUiGameState State=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static void Click(IUiPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static int ButtonIndex(BoardButtonWindow w,int surf){for(int i=0;i<w.Buttons.Count;i++)if(w.Buttons[i].Surf==surf)return i;return -1;}

    [WorldModuleTest("board-navigate")]
    static void Run(WorldModuleTestContext t){
        // 1. 決策（BoardNavigation.Article）
        var read=BoardArticleStep.Read;var close=BoardArticleStep.CloseRead;var none=BoardArticleStep.None;var page=BoardArticleStep.NextPage;
        t.Equal((read,30u),BoardNavigation.Article(View("article",A(20)),false),"672 middle row → read index − 1 (30)");
        t.Equal((close,0u),BoardNavigation.Article(View("article",A(30)),false),"672 first row (index 0) → close the read window");
        t.Equal((read,10u),BoardNavigation.Article(View("article",A(20)),true),"676 middle row → read index + 1 (10)");
        t.Equal((read,20u),BoardNavigation.Article(View("article",A(30)),true),"676 first row → read row 1 (20)");
        t.Equal((close,0u),BoardNavigation.Article(View("article",A(10)),true),"676 last row, no next page → close the read window");
        t.Equal((page,0u),BoardNavigation.Article(View("article",A(10),canNext:true),true),"676 last row, next page → load the next page first");
        t.Equal((read,10u),BoardNavigation.Article(View("article",A(20),canNext:true),true),"676 loaded next row wins over the next page");
        t.Equal((close,0u),BoardNavigation.Article(View("article",A(30),canNext:true),false),"672 index 0 closes even with more pages");
        t.Equal((none,0u),BoardNavigation.Article(View("article",A(20),canAct:false),false),"672 busy → nothing");
        t.Equal((none,0u),BoardNavigation.Article(View("article",A(10),canAct:false,canNext:true),true),"676 busy → nothing");
        t.Equal((none,0u),BoardNavigation.Article(View("list",null),true),"no article → nothing");
        t.Equal((none,0u),BoardNavigation.Article(View("read-wait",A(20)),true),"waiting for an article → nothing");
        t.Equal((none,0u),BoardNavigation.Article(null,false),"no board → nothing");
        t.Equal((close,0u),BoardNavigation.Article(View("article",A(99)),true),"article not on this page → close");
        t.Equal((close,0u),BoardNavigation.Article(View("article",A(30),rows:[new(30,"甲","d","t")]),false),"single row, 672 → close");
        t.Equal((close,0u),BoardNavigation.Article(View("article",A(30),rows:[new(30,"甲","d","t")]),true),"single row, 676, no next page → close");

        // 2. 閱讀窗按鈕的接線
        var cmd=new UiNullCommands();var map=Surfaces();
        var host=new Host();var buttons=new BoardPanelSet(host);
        buttons.LoadSurfaces(id=>map.GetValueOrDefault(id));
        void Sync(){foreach(var p in buttons.Panels)p.Update(State);}
        int i672=ButtonIndex(buttons.Read,672),i676=ButtonIndex(buttons.Read,676);
        t.Check(buttons.Read.Buttons[i672].Role==BoardButtonRole.ArticlePrevious&&buttons.Read.Buttons[i676].Role==BoardButtonRole.ArticleNext,"672 = previous, 676 = next");
        t.Equal(new Rectangle(56,312,34,18),buttons.Read.ButtonRect(i672),"672 at (56,312)");
        t.Equal(new Rectangle(110,312,33,17),buttons.Read.ButtonRect(i676),"676 at (110,312)");
        Point p672=Center(buttons.Read.ButtonRect(i672)),p676=Center(buttons.Read.ButtonRect(i676));

        host.Board=View("article",A(20));Sync();t.Check(buttons.Read.Visible,"article → read window");
        Click(buttons.Read,p672,cmd);
        t.Check(host.Calls.SequenceEqual(new[]{"article-previous"})&&buttons.Read.Visible,"672 middle → host reads the previous article");
        host.Calls.Clear();Click(buttons.Read,p676,cmd);
        t.Check(host.Calls.SequenceEqual(new[]{"article-next"})&&buttons.Read.Visible,"676 middle → host reads the next article");

        host.Calls.Clear();host.Board=View("article",A(30));Sync();Click(buttons.Read,p672,cmd);Sync();
        t.Check(host.Calls.Count==0&&!buttons.Read.Visible&&buttons.List.Visible,"672 at index 0 → read window closed locally, no packet");

        host.Calls.Clear();host.Board=View("article",A(10));Sync();t.Check(buttons.Read.Visible,"another article reopens the read window");
        Click(buttons.Read,p676,cmd);Sync();
        t.Check(host.Calls.Count==0&&!buttons.Read.Visible&&buttons.List.Visible,"676 at the end without more pages → read window closed");

        host.Calls.Clear();host.Board=View("article",A(5),canNext:true,rows:[new(9,"a","d","t"),new(5,"b","d","t")]);Sync();
        Click(buttons.Read,p676,cmd);
        t.Check(host.Calls.SequenceEqual(new[]{"article-next"})&&buttons.Read.Visible,"676 at the end with a next page → host loads the next page");

        host.Calls.Clear();host.Board=View("article",A(20),canAct:false);Sync();
        Click(buttons.Read,p672,cmd);Click(buttons.Read,p676,cmd);Sync();
        t.Check(host.Calls.Count==0&&buttons.Read.Visible,"busy → 672 / 676 do nothing and the read window stays");

        // 3. 678／674 不受影響
        host.Board=View("article",A(1),rows:[new(1,"a","d","t")]);Sync();
        Click(buttons.Read,Center(buttons.Read.ButtonRect(ButtonIndex(buttons.Read,678))),cmd);Sync();
        t.Check(host.Calls.Count==0&&buttons.List.Visible,"678 still closes the read window only");
    }
}
