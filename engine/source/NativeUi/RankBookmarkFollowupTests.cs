namespace P06.NativeUi;

// #1245（f1211-rank-bookmark-window，#1211 SM31／SM38）離線測試：版面、surf、封包全部合成，不讀主客戶端或參考客戶端、
// 不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
//   1. SM31 根因：參考客戶端的 GameRankingUI 若用主客戶端的同號 surf（0-客戶端全是 32×32 小圖），視窗被裁成 32×32、看不到。
//      現在：參考客戶端版面只取參考客戶端 surf；底圖小到放不下視窗自己的元件 → 停用，/rank 回到查詢頁。
//   2. 餵合成 /rank 回包（S39 y_h_2）→ 原生排名窗開啟、範圍是底圖大小。
//   3. SM38：C165 的回包是 S92 單筆書籤 → 清單讀回含新名稱，native-ui.log 有對應的診斷行。
static class RankBookmarkFollowupTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    // 座標照參考客戶端 GameRankingUI.xml 的視窗層（Window 189,48；Exit 408,14；分頁鈕 y=42；翻頁 y=316–317），surf 編號自訂。
    const string Xml="""
        <?xml version="1.0"?>
        <GameRankingUI>
          <Window Name = "GameRanking_Window" X = "189" Y = "48" BaseSurf = "900;1" Moveable = "36" MouseEvent = "Dummy">
            <Image Name = "Title_Image" X = "162" Y = "14" Surf = "901;1"/>
            <Button Name = "Exit_Button" X = "408" Y = "14" BaseSurf = "902" PushSurf = "903" MouseEvent = "Exit"/>
            <Button Name = "Level_TabButton" X = "83" Y = "42" BaseSurf = "910" HighlightSurf = "911" PushSurf = "912" MouseEvent = "TabClick"/>
            <Control Name = "Level_Control" Activate = "0">
              <List Name = "Ranker_List" X = "19" Y = "88" Width = "384" Height = "44" Vertical = "1">
                <Control Name = "List0_Control" Width = "384" Height = "22">
                  <Label Name = "Rank_Label" X = "0" Y = "4" Align = "center" Width = "37" Height = "17"/>
                  <Label Name = "Name_Label" X = "39" Y = "4" Align = "center" Width = "102" Height = "17"/>
                </Control>
                <Control Name = "List1_Control" Width = "384" Height = "22">
                  <Label Name = "Rank_Label" X = "0" Y = "4" Align = "center" Width = "37" Height = "17"/>
                  <Label Name = "Name_Label" X = "39" Y = "4" Align = "center" Width = "102" Height = "17"/>
                </Control>
              </List>
              <Image Name = "Back_Image" X = "18" Y = "68" Surf = "931"/>
            </Control>
            <Control Name = "Page_Control">
              <Label Name = "PageLabel" X = "181" Y = "316" Align = "center" Width = "40" Height = "17"/>
              <Button Name = "Next_Button" X = "248" Y = "317" BaseSurf = "952;1" MouseEvent = "NextPage"/>
            </Control>
          </Window>
        </GameRankingUI>
        """;
    static readonly int[] Ids=[900,901,902,903,910,911,912,931,952];
    // 參考客戶端：真正的視窗圖（底圖 422×362）。主客戶端：同號但全是 32×32 小圖（照 0-客戶端 Sprite*.pak 的實況）。
    static readonly Dictionary<int,UiSurface> Reference=Ids.ToDictionary(id=>id,id=>id==900?Surface(id,422,362,0xFF102030):id==931?Surface(id,386,240,0xFF203040):Surface(id,14,14,0xFF00FF00));
    static readonly Dictionary<int,UiSurface> Main=Ids.ToDictionary(id=>id,id=>Surface(id,32,32,0xFF777777));
    static UiSurface? Ref(int id)=>Reference.GetValueOrDefault(id);
    static UiSurface? Local(int id)=>Main.GetValueOrDefault(id);
    static UiLayout Layout()=>UiLayout.Parse(Xml,"GameRankingUI.xml");
    static GameRankingPanel Loaded(Func<int,UiSurface?> surf){var p=new GameRankingPanel();p.Load(Layout(),surf);return p;}
    static readonly UiStaticGameState World=new(InWorld:true,CharacterName:"測試者",Level:52);

    [WorldModuleTest("rank-bookmark-1245")]
    static void Run(WorldModuleTestContext t){
        try{
            SurfSource(t);
            SyntheticRankPacket(t);
        }finally{GameRankingPanel.Detach();}
        BookmarkReadback(t);
    }

    static void SurfSource(WorldModuleTestContext t){
        // 修正前的情形：參考客戶端版面＋主客戶端同號小圖 → 底圖 32×32，Exit (408,14) 落在外面 → 停用、不認領排名頁。
        var tiny=Loaded(GameRankingPanel.SurfSource(false,Local,Ref));
        t.Check(!tiny.Available&&tiny.Unavailable!.Contains("落在底圖外")&&tiny.Unavailable.Contains("32x32"),"32×32 base → unavailable (no invisible window)",tiny.Unavailable);
        t.Check(!tiny.Offer("y_h_2",Rows(10,"甲").Append("全職業").ToArray())&&!tiny.Open(),"unavailable → page not claimed (query page fallback)");
        // 修正：版面來自參考客戶端 → surf 也取參考客戶端，不先查主客戶端。
        var fixedPanel=Loaded(GameRankingPanel.SurfSource(true,Local,Ref));
        t.Check(fixedPanel.Available,"reference layout + reference surfs usable",fixedPanel.Unavailable);
        t.Equal(new Rectangle(189,48,422,362),fixedPanel.Bounds,"window = XML X/Y + reference BaseSurf size");
        t.Check(fixedPanel.Background?.BaseSurface==Reference[900],"background is the reference surf, not the local same-numbered one");
        // 參考客戶端沒有的 surf 不改用主客戶端的同號圖。
        var noRef=Loaded(GameRankingPanel.SurfSource(true,Local,null));
        t.Check(!noRef.Available&&noRef.Unavailable!.Contains("BaseSurf"),"no reference source → base missing → unavailable, never local",noRef.Unavailable);
        var partial=Loaded(GameRankingPanel.SurfSource(true,Local,id=>id==912?null:Ref(id)));
        t.Check(partial.Available&&partial.Missing.Any(m=>m.Contains("surf 912")),"surf missing in reference → listed missing, not taken from main",partial.Missing);
        // 版面在主客戶端時照舊用主客戶端。
        t.Check(GameRankingPanel.SurfSource(false,Local,Ref)(900)==Main[900]&&GameRankingPanel.SurfSource(true,Local,Ref)(900)==Reference[900],"source selection");
    }

    static string[] Rows(int count,params string[] rows)=>rows.Concat(Enumerable.Repeat(" ",count-rows.Length)).ToArray();

    /// <summary>S39 S_NPCTalkReturn：C(39) D(self) S(html) H(1) H(n) S×n（docs/protocol/player-query.md）。</summary>
    static byte[] S39(uint self,string html,string[] args){
        var b=new List<byte>{39};b.AddRange(BitConverter.GetBytes(self));
        void S(string s){b.AddRange(LoginPackets.TextEncoding.GetBytes(s));b.Add(0);}
        S(html);b.AddRange(BitConverter.GetBytes((ushort)1));b.AddRange(BitConverter.GetBytes((ushort)args.Length));
        foreach(var a in args)S(a);
        return LoginPackets.Pad(b.ToArray());
    }

    static void SyntheticRankPacket(WorldModuleTestContext t){
        GameRankingPanel.Detach();
        _=GameRankingPanel.Registration;   // static constructor subscribes once
        // /rank（C125 lv_ranking）的回包：y_h_2，#0–9 名次（空位一個空白），#10 職業標題。
        var packet=S39(0x1234,"y_h_2",Rows(10,"甲","乙").Append("全職業").ToArray());
        var rank=PlayerQueryPackets.DecodeRankHtml(packet);
        t.Check(rank is {ObjectId:0x1234,Html:"y_h_2"}&&rank.Value.Arguments.Length==11,"synthetic /rank S39 decodes as a ranking page");
        if(rank is not {} r)return;
        var tiny=Loaded(GameRankingPanel.SurfSource(false,Local,Ref));tiny.Attach();
        t.Check(!PlayerQueryPackets.RequestRankingWindow(r.ObjectId,r.Html,r.Arguments,r.Lines),"32×32 panel attached → not handled → query page");
        var p=Loaded(GameRankingPanel.SurfSource(true,Local,Ref));p.Attach();
        t.Check(PlayerQueryPackets.RequestRankingWindow(r.ObjectId,r.Html,r.Arguments,r.Lines),"usable panel claims the /rank page");
        t.Check(!p.Visible,"opens on the next Update (UI thread)");
        p.Update(World);
        t.Check(p.Visible&&p.ActiveTab==GameRankingTab.Level,"ranking window open on the Level tab");
        t.Equal(new Rectangle(189,48,422,362),p.Bounds,"open window has the full reference size");
        var names=p.ActiveCells().Where(c=>c.Cell.Name=="Name_Label").Select(c=>c.Text).ToArray();
        t.Check(names.SequenceEqual(["甲","乙"]),"rows drawn from the packet",names);
        t.Check(UiResourceLog.Recent().Any(l=>l.StartsWith("ranking-ui 開窗 y_h_2",StringComparison.Ordinal)&&l.Contains("422x362")),"open logged with bounds (retest evidence line)");
        // 空的排名（伺服器只回空白）也照樣開窗，頁碼 0 / 0。
        var empty=S39(0x1234,"y_h_2",Rows(10).Append("全職業").ToArray());
        var e=PlayerQueryPackets.DecodeRankHtml(empty)!.Value;
        p.Close();
        t.Check(PlayerQueryPackets.RequestRankingWindow(e.ObjectId,e.Html,e.Arguments,e.Lines),"empty ranking page still claimed");
        p.Update(World);
        t.Check(p.Visible&&p.PageText=="0 / 0"&&!p.ActiveCells().Any(),"empty ranking opens with no rows");
    }

    static void BookmarkReadback(WorldModuleTestContext t){
        var s=new WorldSession();
        // S64 整份清單（登入時）：0x2A 0x80 0x00 0x02、127 個 0xFF、H(上限 50) H(筆數 0)。
        var list=new List<byte>{64,0x2A,0x80,0x00,0x02};list.AddRange(Enumerable.Repeat((byte)0xFF,127));
        list.AddRange(BitConverter.GetBytes((ushort)50));list.AddRange(BitConverter.GetBytes((ushort)0));
        s.Receive(LoginPackets.Pad(list.ToArray()),"");
        t.Check(s.Bookmarks.Items.Length==0&&s.Bookmarks.Capacity==50,"S64 empty list",s.Bookmarks.Notice);
        t.Check(UiResourceLog.Recent().Any(l=>l=="bookmark S64 整份清單 0/50 筆"),"S64 logged");
        // C165 "SM1211" 的回包：S92 S(名稱) H(地圖) H(x) H(y) D(id)。
        static byte[] S92(string name,ushort map,ushort x,ushort y,uint id){
            var b=new List<byte>{92};b.AddRange(LoginPackets.TextEncoding.GetBytes(name));b.Add(0);
            b.AddRange(BitConverter.GetBytes(map));b.AddRange(BitConverter.GetBytes(x));b.AddRange(BitConverter.GetBytes(y));b.AddRange(BitConverter.GetBytes(id));
            return LoginPackets.Pad(b.ToArray());
        }
        s.Bookmarks.Sent(null);   // 送出 C165 後的狀態
        s.Receive(S92("SM1211",4,33000,32800,501),"");
        t.Check(s.Bookmarks.Items.Any(b=>b is {Id:501,Name:"SM1211",MapId:4,X:33000,Y:32800}),"S92 adds the bookmark: list reads back SM1211");
        t.Equal(BookmarkWindowPanel.SingleBookmarkNotice,s.Bookmarks.Notice,"single-bookmark notice kept (the window opens on it)");
        s.Receive(S92("SM1211Q",4,33001,32801,502),"");
        t.Check(s.Bookmarks.Items.Select(b=>b.Name).Order(StringComparer.Ordinal).SequenceEqual(["SM1211","SM1211Q"]),"second add (qbookmark) reads back too");
        t.Check(UiResourceLog.Recent().Any(l=>l.StartsWith("bookmark S92 單筆 id=501 名稱=SM1211 ",StringComparison.Ordinal)&&l.EndsWith("清單 1 筆",StringComparison.Ordinal)),"S92 logged with the list size");
        t.Check(UiResourceLog.Recent().Any(l=>l.Contains("名稱=SM1211Q")&&l.EndsWith("清單 2 筆",StringComparison.Ordinal)),"second S92 logged");
    }
}
