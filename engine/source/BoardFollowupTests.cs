using System.Text;
using P06.NativeUi;
namespace P06;

// R7-F（T345／#808）board-followup 離線測試：公告板收尾。
//   - 窗位置 (0,0)（UI 原點 dword_154524C／dword_1545250 在 800×600 為 0）；
//   - 閱讀窗 678 只關閱讀窗（sub_7CBA70 把 678 綁到 sub_A59AB0(閱讀窗)），不送包；
//   - 662 開發文窗前的金幣檢查（sub_7CDE00）：費用≠0 且金幣＜費用 → 聊天列印 189，不開窗；
//   - 清單／閱讀按鈕窗實作 IUiClosablePanel。
// .img 全部由本檔合成（只有尺寸），字串表用合成表或強制不載入；不讀真實客戶端、不開窗、不連網路。
static class BoardFollowupTests {
    sealed class Host:IBoardPanelHost,IBoardContentHost {
        public BoardView? Board{get;set;}
        public uint? SelectedRow{get;set;}
        public long? Gold{get;set;}
        public readonly List<string> Calls=[];
        public void Press(string command)=>Calls.Add(command);
        public void SelectRow(uint? id)=>SelectedRow=id;
        public void Submit(string title,string content)=>Calls.Add($"submit:{title}|{content}");
        public void Notice(string text)=>Calls.Add("notice:"+text);
        public void ChatNotice(string text,int color)=>Calls.Add($"chat:{color}:{text}");
    }
    /// <summary>只實作必要成員：Gold／ChatNotice 走介面預設（不知道金幣 → 不擋；聊天訊息退回 Notice）。</summary>
    sealed class PlainHost:IBoardPanelHost,IBoardContentHost {
        public BoardView? Board{get;set;}
        public uint? SelectedRow{get;set;}
        public readonly List<string> Calls=[];
        public void Press(string command)=>Calls.Add(command);
        public void SelectRow(uint? id)=>SelectedRow=id;
        public void Submit(string title,string content)=>Calls.Add("submit");
        public void Notice(string text)=>Calls.Add("notice:"+text);
    }
    static readonly BoardRow[] Rows=[new(20,"作者甲","2026/11/09","第一篇標題")];
    static BoardView View(string kind,BoardArticle? article=null,ushort fee=300)=>new(1,1,1,1,1,456,kind,"",Rows,article,fee,true,false,false);
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFFFF0000u,w*h).ToArray()));
    // 按鈕圖的寬高照真實圖（同 BoardPanelChecks），背景照反組譯的繪製矩形。
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [656]=Surface(656,238,350),[657]=Surface(657,10,10),[670]=Surface(670,238,343),
        [658]=Surface(658,17,17),[660]=Surface(660,23,13),[662]=Surface(662,31,26),[664]=Surface(664,31,26),[666]=Surface(666,35,23),[668]=Surface(668,35,22),
        [672]=Surface(672,34,18),[674]=Surface(674,20,29),[676]=Surface(676,33,17),[678]=Surface(678,15,17),[686]=Surface(686,40,20)};
    static readonly IUiGameState State=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static void Click(IUiPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static int ButtonIndex(BoardButtonWindow w,int surf){for(int i=0;i<w.Buttons.Count;i++)if(w.Buttons[i].Surf==surf)return i;return -1;}

    [WorldModuleTest("board-followup")]
    static void Run(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var map=Surfaces();
        var host=new Host();var buttons=new BoardPanelSet(host);var set=new BoardContentSet(buttons,host);
        buttons.LoadSurfaces(id=>map.GetValueOrDefault(id));set.LoadSurfaces(id=>map.GetValueOrDefault(id));
        void Sync(){foreach(var p in buttons.Panels.Concat(set.Panels))p.Update(State);}

        // 1. 窗位置 (0,0)
        t.Equal(new Point(0,0),BoardWindowLayout.DefaultOrigin,"board origin is the UI origin (0,0) at 800×600");
        host.Board=View("list");Sync();
        t.Check(buttons.List.Origin==Point.Empty&&buttons.Read.Origin==Point.Empty&&set.List.Origin==Point.Empty&&set.Read.Origin==Point.Empty&&set.Write.Origin==Point.Empty,"all board windows at (0,0)");
        t.Equal(new Rectangle(0,0,238,350),set.List.Bounds,"list content (0,0,238,350) on screen");
        t.Equal(new Rectangle(209,305,17,17),buttons.List.ButtonRect(ButtonIndex(buttons.List,658)),"658 at screen (209,305)");
        t.Equal(new Rectangle(213,297,15,17),buttons.Read.ButtonRect(ButtonIndex(buttons.Read,678)),"678 at screen (213,297)");
        t.Equal(new BoardTextItem("0020",30,50,8),set.List.Texts()[0],"first row number at screen (30,50)");

        // 2. 按鈕窗實作 IUiClosablePanel
        t.Check(buttons.List is IUiClosablePanel&&buttons.Read is IUiClosablePanel,"list/read button windows are closable");
        t.Check(buttons.List.Visible&&!buttons.Read.Visible,"list is up");
        host.Calls.Clear();((IUiClosablePanel)buttons.Read).Close();
        t.Check(host.Calls.Count==0&&buttons.List.Visible,"closing the hidden read window does nothing");
        ((IUiClosablePanel)buttons.List).Close();
        t.Check(host.Calls.SequenceEqual(new[]{"close"}),"closing the list window closes the board (same as 658)");
        host.Calls.Clear();

        // 3. 閱讀窗 678：只關閱讀窗
        var a20=new BoardArticle(20,"作者甲","2026/11/09","標題","內容");
        host.Board=View("article",a20);Sync();
        t.Check(buttons.Read.Visible&&set.Read.Visible&&!buttons.List.Visible,"article → read window");
        Click(buttons.Read,Center(buttons.Read.ButtonRect(ButtonIndex(buttons.Read,678))),cmd);Sync();
        t.Check(host.Calls.Count==0,"678 sends nothing and does not close the board");
        t.Check(!buttons.Read.Visible&&!set.Read.Visible&&buttons.List.Visible&&set.List.Visible,"678 → read window gone, list window stays");
        host.Board=View("article",a20);Sync();t.Check(buttons.List.Visible,"the same article stays closed");
        host.Board=View("article",a20 with{Id=21});Sync();t.Check(buttons.Read.Visible,"another article opens the read window again");
        ((IUiClosablePanel)buttons.Read).Close();Sync();
        t.Check(host.Calls.Count==0&&buttons.List.Visible&&!buttons.Read.Visible,"IUiClosablePanel.Close on the read window = 678");
        // 658 仍關整個公告板
        Click(buttons.List,Center(buttons.List.ButtonRect(ButtonIndex(buttons.List,658))),cmd);
        t.Check(host.Calls.SequenceEqual(new[]{"close"}),"658 still closes the whole board");
        host.Calls.Clear();

        // 4. sub_7CDE00 金幣檢查
        t.Check(!BoardContentSet.ShortOfGold(0,0)&&!BoardContentSet.ShortOfGold(0,null),"fee 0 → never short");
        t.Check(BoardContentSet.ShortOfGold(300,299)&&BoardContentSet.ShortOfGold(1,0),"gold < fee → short");
        t.Check(!BoardContentSet.ShortOfGold(300,300)&&!BoardContentSet.ShortOfGold(300,long.MaxValue),"gold ≥ fee → enough");
        t.Check(!BoardContentSet.ShortOfGold(300,null),"unknown gold (inventory not loaded) → not blocked");
        t.Check(BoardText.StringNoGold==189&&BoardText.NoGoldColor==24&&BoardText.NoGoldSecondColor==14,"189, palette[24] / palette[14] (sub_7DC850 args)");
        using(ServerMessageText.UseTable(null)){
            t.Equal("金幣不足。",BoardText.NoGoldText,"189 without a table = verified original text");
            host.Board=View("list",fee:300);Sync();host.Gold=299;
            t.Check(!set.OpenWrite()&&!set.Write.Visible&&!set.Write.IsOpen,"short of gold → write window not opened");
            t.Check(host.Calls.SequenceEqual(new[]{"chat:24:金幣不足。"}),"short of gold → 189 on the chat line, colour 24");
            host.Calls.Clear();host.Gold=300;
            t.Check(set.OpenWrite()&&set.Write.Visible&&host.Calls.Count==0,"gold == fee → write window opens, no message");
            set.Write.Close();host.Gold=null;
            t.Check(set.OpenWrite()&&set.Write.Visible,"unknown gold → opens (server decides)");
            set.Write.Close();host.Board=View("list",fee:0);Sync();host.Gold=0;
            t.Check(set.OpenWrite()&&set.Write.Visible&&host.Calls.Count==0,"fee 0 → opens even with no gold");
            set.Write.Close();host.Board=View("list-wait",fee:300);Sync();host.Gold=0;
            t.Check(!set.OpenWrite()&&host.Calls.Count==0,"no board data yet → nothing (not even 189)");
        }
        // 字串表有 189 時用表的字，並去掉 \f 色碼（同伺服器訊息）。
        var tbl=new StringBuilder("200\n");for(int i=0;i<189;i++)tbl.Append('\n');tbl.Append("\\f1測試金幣不夠。\n");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using(ServerMessageText.UseTable(ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(tbl.ToString())))){
            t.Equal("測試金幣不夠。",BoardText.NoGoldText,"189 from the string table, \\f1 removed");
        }

        // 5. 介面預設：沒有金幣來源的 host 不擋；聊天訊息退回 Notice
        {
            var plain=new PlainHost{Board=View("list",fee:300)};var pb=new BoardPanelSet(plain);var pc=new BoardContentSet(pb,plain);
            pb.LoadSurfaces(id=>map.GetValueOrDefault(id));pc.LoadSurfaces(id=>map.GetValueOrDefault(id));
            t.Check(pc.OpenWrite()&&pc.Write.Visible,"host without gold → write opens (default Gold = null)");
            using(ServerMessageText.UseTable(null)){((IBoardContentHost)plain).ChatNotice("x",24);}
            t.Check(plain.Calls.SequenceEqual(new[]{"notice:x"}),"default ChatNotice falls back to Notice");
        }

        // 6. 聊天列：WorldSession.AddBoardChatNotice
        {
            var w=new WorldSession();int lines=w.PlayerChatLines.Length,rev=w.PlayerChatRevision;
            w.AddBoardChatNotice("金幣不足。",BoardText.NoGoldColor);
            var last=w.PlayerChatLines[^1];
            t.Check(w.PlayerChatLines.Length==lines+1&&w.PlayerChatRevision==rev+1,"board notice adds one chat line");
            t.Check(last.Text=="金幣不足。"&&last.ColorIndex==24&&last.Speaker==0,"board notice: text, colour 24, no speaker");
            w.AddBoardChatNotice("",24);t.Equal(lines+1,w.PlayerChatLines.Length,"empty notice adds nothing");
            for(int i=0;i<120;i++)w.AddBoardChatNotice("n"+i,null);
            t.Check(w.PlayerChatLines.Length<=100&&w.PlayerChatLines[^1].Text=="n119","chat lines stay bounded at 100");
        }

        // 7. UiRoot：按鈕窗可關、在視窗堆疊裡
        {
            host.Board=View("list");host.Calls.Clear();
            var root=new UiRoot();root.Add(set.List);root.Add(set.Read);foreach(var p in buttons.Panels)root.Add(p);root.Add(set.Write);
            root.Update(State);
            t.Check(root.WindowStack.Contains(buttons.List),"list button window is in the window stack");
            t.Equal<IUiPanel?>(buttons.List,root.HitTest(Center(buttons.List.ButtonRect(ButtonIndex(buttons.List,658)))),"658 hit at (0,0)-based position");
        }
    }
}
