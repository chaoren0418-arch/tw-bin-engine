namespace P06.NativeUi;

// T331 (#769) game-ranking-ui 離線測試：只用合成版面與合成 surf（純色方塊），不讀主客戶端或參考客戶端的
// GameRankingUI.xml、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class GameRankingPanelTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    // Synthetic layout with the element structure of the reference window (own coordinates and surf numbers).
    const string Xml="""
        <?xml version="1.0"?>
        <GameRankingUI>
          <Window Name = "GameRanking_Window" X = "100" Y = "50" BaseSurf = "900;1" Moveable = "20" MouseEvent = "Dummy">
            <Image Name = "Title_Image" X = "60" Y = "4" Surf = "901;1"/>
            <Button Name = "Exit_Button" X = "180" Y = "4" BaseSurf = "902" PushSurf = "903" MouseEvent = "Exit"/>
            <Button Name = "Level_TabButton" X = "10" Y = "24" BaseSurf = "910" HighlightSurf = "911" PushSurf = "912" MouseEvent = "TabClick"/>
            <Button Name = "Pledge_TabButton" X = "50" Y = "24" BaseSurf = "913" HighlightSurf = "914" PushSurf = "915" MouseEvent = "TabClick"/>
            <Button Name = "Money_TabButton" X = "90" Y = "24" BaseSurf = "916" HighlightSurf = "917" PushSurf = "918" MouseEvent = "TabClick"/>
            <Button Name = "Kill_TabButton" X = "130" Y = "24" BaseSurf = "919" HighlightSurf = "920" PushSurf = "999" MouseEvent = "TabClick"/>
            <Label Name = "Update_Label" Caption = "Last Update" X = "100" Y = "180" Align = "right" Width = "90" Height = "17"/>
            <Control Name = "Personal_Control" Activate = "0">
              <Label Name = "Name_Label" X = "40" Y = "60" Align = "left" Width = "70" Height = "17"/>
              <Image Name = "Back_Image" X = "5" Y = "40" Surf = "930"/>
            </Control>
            <Control Name = "Level_Control" Activate = "0">
              <List Name = "Ranker_List" X = "10" Y = "50" Width = "180" Height = "60" Vertical = "1">
                <Control Name = "List0_Control" Width = "180" Height = "20">
                  <Label Name = "Rank_Label" Caption = "1" X = "0" Y = "4" Align = "center" Width = "30" Height = "17"/>
                  <Label Name = "Name_Label" Caption = "Name" X = "30" Y = "4" Align = "center" Width = "80" Height = "17"/>
                  <Label Name = "Class_Label" Caption = "Class" X = "110" Y = "4" Align = "center" Width = "50" Height = "17"/>
                  <Image Name = "Diff_Image" X = "165" Y = "5" Surf = "940;1"/>
                </Control>
                <Control Name = "List1_Control" Width = "180" Height = "20">
                  <Label Name = "Rank_Label" Caption = "1" X = "0" Y = "4" Align = "center" Width = "30" Height = "17"/>
                  <Label Name = "Name_Label" Caption = "Name" X = "30" Y = "4" Align = "center" Width = "80" Height = "17"/>
                  <Image Name = "Diff_Image" X = "165" Y = "5" Surf = "941;1"/>
                </Control>
                <Control Name = "List2_Control" Width = "180" Height = "20">
                  <Label Name = "Rank_Label" Caption = "1" X = "0" Y = "4" Align = "center" Width = "30" Height = "17"/>
                  <Label Name = "Name_Label" Caption = "Name" X = "30" Y = "4" Align = "center" Width = "80" Height = "17"/>
                </Control>
              </List>
              <Image Name = "Back_Image" X = "5" Y = "40" Surf = "931"/>
            </Control>
            <Control Name = "Pledge_Control" Activate = "0">
              <List Name = "Ranker_List" X = "10" Y = "50" Width = "180" Height = "40" Vertical = "1">
                <Control Name = "List0_Control" Width = "180" Height = "20">
                  <Label Name = "Rank_Label" Caption = "1" X = "0" Y = "4" Align = "center" Width = "30" Height = "17"/>
                  <Label Name = "Name_Label" Caption = "Name" X = "30" Y = "4" Align = "left" Width = "80" Height = "17"/>
                  <Label Name = "Point_Label" Caption = "0" X = "110" Y = "4" Align = "right" Width = "60" Height = "17"/>
                </Control>
              </List>
              <Image Name = "Back_Image" X = "5" Y = "40" Surf = "932"/>
            </Control>
            <Control Name = "Money_Control" Activate = "0">
              <List Name = "Ranker0_List" X = "10" Y = "50" Width = "90" Height = "40" Vertical = "1">
                <Control Name = "List0_Control" Width = "90" Height = "20"><Label Name = "Rank_Label" X = "0" Y = "4" Width = "30" Height = "17"/><Label Name = "Name_Label" X = "30" Y = "4" Width = "60" Height = "17"/></Control>
                <Control Name = "List1_Control" Width = "90" Height = "20"><Label Name = "Rank_Label" X = "0" Y = "4" Width = "30" Height = "17"/><Label Name = "Name_Label" X = "30" Y = "4" Width = "60" Height = "17"/></Control>
              </List>
              <List Name = "Ranker1_List" X = "100" Y = "50" Width = "90" Height = "40" Vertical = "1">
                <Control Name = "List0_Control" Width = "90" Height = "20"><Label Name = "Rank_Label" X = "0" Y = "4" Width = "30" Height = "17"/><Label Name = "Name_Label" X = "30" Y = "4" Width = "60" Height = "17"/></Control>
              </List>
              <Image Name = "Back_Image" X = "5" Y = "40" Surf = "933"/>
            </Control>
            <Control Name = "Kill_Control" Activate = "0">
              <List Name = "Ranker_List" X = "10" Y = "50" Width = "180" Height = "40" Vertical = "1">
                <Control Name = "List0_Control" Width = "180" Height = "20">
                  <Label Name = "Rank_Label" X = "0" Y = "4" Align = "center" Width = "30" Height = "17"/>
                  <Label Name = "Name_Label" X = "30" Y = "4" Align = "center" Width = "80" Height = "17"/>
                  <Label Name = "Point" X = "110" Y = "4" Align = "center" Width = "60" Height = "17"/>
                </Control>
              </List>
              <Image Name = "Back_Image" X = "5" Y = "40" Surf = "934"/>
            </Control>
            <Control Name = "Page_Control">
              <Label Name = "PageLabel" Caption = "0 / 0" X = "80" Y = "160" Align = "center" Width = "40" Height = "17"/>
              <Button Name = "First_Button" X = "60" Y = "160" BaseSurf = "950;1" PushSurf = "951;1" MouseEvent = "FirstPage"/>
              <Button Name = "Next_Button" X = "125" Y = "160" BaseSurf = "952;1" PushSurf = "953;1" MouseEvent = "NextPage"/>
            </Control>
          </Window>
        </GameRankingUI>
        """;

    const uint Base=0xFF102030,Back=0xFF203040,Tab=0xFF400000,TabHi=0xFF800000,TabPush=0xFFFF0000,Exit=0xFF00FF00,PageBtn=0xFF0000FF;
    static readonly Dictionary<int,UiSurface> Surfs=Build();
    static Dictionary<int,UiSurface> Build(){
        var d=new Dictionary<int,UiSurface>{[900]=Surface(900,200,200,Base),[901]=Surface(901,40,12,0xFF999999),[902]=Surface(902,12,12,Exit),[903]=Surface(903,12,12,0xFF00AA00)};
        foreach(int id in new[]{910,913,916,919})d[id]=Surface(id,36,14,Tab);
        foreach(int id in new[]{911,914,917,920})d[id]=Surface(id,36,14,TabHi);
        foreach(int id in new[]{912,915,918})d[id]=Surface(id,36,14,TabPush);   // 999 (Kill push) deliberately missing
        foreach(int id in new[]{930,931,932,933,934})d[id]=Surface(id,190,140,Back);
        d[940]=Surface(940,8,8,0xFFFFFF00);d[941]=Surface(941,8,8,0xFFFFFF00);
        foreach(int id in new[]{950,951,952,953})d[id]=Surface(id,10,10,PageBtn);
        return d;
    }
    static UiSurface? Get(int id)=>Surfs.TryGetValue(id,out var s)?s:null;
    static UiLayout Layout(string text=Xml)=>UiLayout.Parse(text,"GameRankingUI.xml");
    static GameRankingPanel Loaded(Func<int,UiSurface?>? surf=null){var p=new GameRankingPanel();p.Load(Layout(),surf??(Func<int,UiSurface?>)Get);return p;}
    static readonly UiStaticGameState World=new(InWorld:true,CharacterName:"測試者",Level:52);
    static string[] Rows(int count,params string[] rows)=>rows.Concat(Enumerable.Repeat(" ",count-rows.Length)).ToArray();
    static UiMouseEvent M(UiMouseKind k,int x,int y,MouseButtons b=MouseButtons.Left)=>new(k,new Point(x,y),b,1,0,Keys.None);
    static void Click(GameRankingPanel p,int x,int y){var c=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
    static int Pixel(GameRankingPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}

    [WorldModuleTest("game-ranking-ui")]
    static void Run(WorldModuleTestContext t){
        try{
            Decode(t);
            LoadLayout(t);
            Broken(t);
            OpenAndCells(t);
            MouseAndPaint(t);
            Event(t);
            Registration(t);
        }finally{GameRankingPanel.Detach();}
    }

    static void Decode(WorldModuleTestContext t){
        var hero=GameRankingData.FromServer("y_h_2",Rows(10,"甲","乙").Append("全職業").ToArray())!;
        t.Equal(GameRankingTab.Level,hero.Tab,"y_h_2 → Level tab");
        t.Equal("英雄風雲榜・全職業",hero.Title,"y_h_2 title from #10");
        t.Equal(2,hero.Entries.Count,"blank padded rows dropped");
        t.Check(hero.Entries[1].Rank==2&&hero.Entries[1].Cells["Name_Label"]=="乙"&&hero.Entries[1].Cells["Rank_Label"]=="2","y_h_2 rank/name");
        t.Check(!hero.Entries[0].Cells.ContainsKey("Level_Label")&&!hero.Entries[0].Cells.ContainsKey("Class_Label"),"y_h_2 level/class not invented");
        var clan=Rows(10,"測試盟(3)","無括號").Concat(Enumerable.Repeat("宣戰",10)).ToArray();
        var pledge=GameRankingData.FromServer("y_c_1",clan)!;
        t.Check(pledge.Tab==GameRankingTab.Pledge&&pledge.Entries.Count==2,"y_c_1 → Pledge, war labels #10–19 dropped");
        t.Check(pledge.Entries[0].Cells["Name_Label"]=="測試盟"&&pledge.Entries[0].Cells["Point_Label"]=="3","y_c_1 name(value) split");
        t.Check(pledge.Entries[1].Cells["Name_Label"]=="無括號"&&!pledge.Entries[1].Cells.ContainsKey("Point_Label"),"y_c_1 no value kept raw");
        var kill=GameRankingData.FromServer("y_k_2",Rows(10,"甲,5"))!;
        t.Check(kill.Tab==GameRankingTab.Kill&&kill.Title=="殺人榜"&&kill.Entries[0].Cells["Name_Label"]=="甲"&&kill.Entries[0].Cells["Point"]=="5","y_k_2 name,count");
        var dead=GameRankingData.FromServer("y_k_3",Rows(10,"乙,2"))!;
        t.Check(dead.Tab==GameRankingTab.Kill&&dead.Title=="往生榜"&&dead.Entries[0].Cells["Point"]=="2","y_k_3 → Kill tab");
        var money=GameRankingData.FromServer("y_w_1",Rows(10,"富豪"))!;
        t.Check(money.Tab==GameRankingTab.Money&&money.Entries.Single().Cells.Count==2,"y_w_1 rank+name only");
        t.Check(GameRankingData.FromServer("y_w_1",Rows(9))==null,"wrong argument count → null");
        t.Check(GameRankingData.FromServer("y_h_2",Rows(10))==null,"y_h_2 without title → null");
        t.Check(GameRankingData.FromServer("y_who",Rows(9))==null,"other page → null");
    }

    static void LoadLayout(WorldModuleTestContext t){
        var p=Loaded();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Equal(new Rectangle(100,50,200,200),p.Bounds,"window X/Y + BaseSurf size");
        t.Equal(20,p.MoveableHeight,"Moveable");
        t.Check(p.Pages.Select(x=>x.Tab).SequenceEqual([GameRankingTab.Personal,GameRankingTab.Level,GameRankingTab.Pledge,GameRankingTab.Money,GameRankingTab.Kill]),"tab controls in XML order");
        var level=p.Pages.Single(x=>x.Tab==GameRankingTab.Level);
        t.Equal(3,level.Lists.Single().Rows.Count,"three list rows");
        var row2=level.Lists[0].Rows[2].Cells["Name_Label"];
        t.Equal(new Point(10+30,50+40+4),row2.Offset,"rows stack by Height (List X/Y + n×Height + label X/Y)");
        t.Equal(931,level.Images.Single().Base!.Id,"Back_Image kept as page image");
        var money=p.Pages.Single(x=>x.Tab==GameRankingTab.Money);
        t.Check(money.Lists.Count==2&&money.Lists[1].Offset==new Point(100,50),"Money two lists");
        t.Check(p.WindowPieces[0].Name=="Title_Image","images first in window order");
        t.Check(p.WindowPieces.Any(x=>x.Name=="PageLabel")&&p.WindowPieces.Any(x=>x.Name=="Next_Button"&&x.Offset==new Point(125,160)),"Page_Control pieces flattened");
        t.Check(p.Missing.SequenceEqual(["surf 999 (GameRankingUI/Window/Button[4]/@PushSurf)"]),"missing surf listed",p.Missing);
        t.Check(p.Skipped.Any(s=>s.Contains("Update_Label")||s.EndsWith("Label[0]"))&&p.Skipped.Count(s=>s.Contains("名次變動圖"))==2,"Update_Label and Diff_Image skipped",p.Skipped);
        t.Check(!p.Visible,"closed after load");
        t.Check(GameRankingPanel.SurfAttr(Layout().Child("Window"),"BaseSurf")==new GameRankingSurf("BaseSurf",900,1),"surf;flag parsed");
    }

    static void Broken(WorldModuleTestContext t){
        var noBase=Loaded(id=>id==900?null:Get(id));
        t.Check(!noBase.Available&&noBase.Unavailable!.Contains("BaseSurf")&&!noBase.Open(),"missing BaseSurf → unavailable, no substitute");
        t.Throws<UiLayoutException>(()=>new GameRankingPanel().Load(Layout(Xml.Replace("\"GameRanking_Window\"","\"Other\"")),Get),"wrong window name");
        t.Throws<UiLayoutException>(()=>new GameRankingPanel().Load(Layout(Xml.Replace("BaseSurf = \"900;1\"","BaseSurf = \"x\"")),Get),"bad surf attr");
        t.Throws<UiLayoutException>(()=>new GameRankingPanel().Load(Layout(Xml.Replace("Name = \"List2_Control\" Width = \"180\" Height = \"20\"","Name = \"List2_Control\" Width = \"180\"")),Get),"row without Height");
        t.Throws<UiLayoutException>(()=>new GameRankingPanel().Load(Layout("<GameRankingUI><Window Name=\"GameRanking_Window\" X=\"0\" Y=\"0\" BaseSurf=\"900\"/></GameRankingUI>"),Get),"no tab controls");
        var host=new GameRankingPanel();host.Load(Layout(),(SurfCatalog)null!);   // returns before touching the catalog
        t.Check(!host.Available&&host.Unavailable=="沒有 UiResources"&&!GameRankingPanel.IsAttached(host),"product Load without resources → unavailable, not attached, no exception");
    }

    static void OpenAndCells(WorldModuleTestContext t){
        var p=Loaded();
        t.Check(p.Offer("y_k_2",Rows(10,"甲,5","乙,4")),"kill page claimed");
        t.Check(!p.Visible,"opens only in Update");
        p.Update(World);
        t.Check(p.Visible&&p.ActiveTab==GameRankingTab.Kill,"opened on Kill tab");
        var cells=p.ActiveCells().ToList();
        t.Equal(3,cells.Count,"only one row slot in Kill list (rank,name,point)");
        t.Check(cells.Any(c=>c.Cell.Name=="Point"&&c.Text=="5"),"point cell");
        t.Equal("1 / 1",p.PageText,"page text with data");
        t.Check(p.ShowTab(GameRankingTab.Personal)&&!p.ActiveCells().Any()&&p.PageText=="0 / 0","Personal tab: no server data");
        t.Check(!p.ShowTab(GameRankingTab.Weapon),"tab not in layout");
        t.Check(!p.Offer("y_who",Rows(9)),"non-ranking page not claimed");
        t.Check(p.Offer("y_h_2",Rows(10,"甲","乙","丙","丁").Append("全職業").ToArray()),"hero page claimed");
        p.Update(World);
        t.Check(p.ActiveTab==GameRankingTab.Level&&p.ActiveCells().Count(c=>c.Cell.Name=="Name_Label")==3,"4 rows → 3 slots drawn");
        t.Check(p.Data.ContainsKey(GameRankingTab.Kill)&&p.Data.ContainsKey(GameRankingTab.Level),"data kept per tab");
        var center=p.Pages.Single(x=>x.Tab==GameRankingTab.Level).Lists[0].Rows[0].Cells["Name_Label"];
        t.Equal(30+(80-20)/2,GameRankingPanel.AlignX(center,20),"center align");
        var pledge=p.Pages.Single(x=>x.Tab==GameRankingTab.Pledge).Lists[0].Rows[0].Cells;
        t.Equal(110+60-10,GameRankingPanel.AlignX(pledge["Point_Label"],10),"right align");
        t.Equal(30,GameRankingPanel.AlignX(pledge["Name_Label"],10),"left align");
        t.Equal(30,GameRankingPanel.AlignX(center,200),"too wide → label left");
        p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible&&p.Data.Count==0,"leaving the world closes and clears");
        t.Check(!Loaded(id=>id==900?null:Get(id)).Offer("y_k_2",Rows(10,"甲,5")),"unavailable panel does not claim");
        var noKill=new GameRankingPanel();noKill.Load(Layout(Xml.Replace("Kill_Control","Unknown_Control")),Get);
        t.Check(noKill.Available&&!noKill.Offer("y_k_2",Rows(10,"甲,5"))&&noKill.Skipped.Any(s=>s.Contains("不認得的分頁")),"no tab for the page → not claimed");
    }

    static void MouseAndPaint(WorldModuleTestContext t){
        var p=Loaded();
        p.Offer("y_c_1",Rows(10,"測試盟(3)").Concat(Enumerable.Repeat("x",10)).ToArray());p.Update(World);
        t.Equal(Base,(uint)Pixel(p,100+195,50+195),"background drawn");
        t.Equal(Back,(uint)Pixel(p,100+5+1,50+40+139),"page Back_Image drawn under the list");
        t.Equal(TabPush,(uint)Pixel(p,100+50+1,50+24+1),"active tab uses PushSurf");
        t.Equal(Tab,(uint)Pixel(p,100+10+1,50+24+1),"inactive tab uses BaseSurf");
        t.Equal(PageBtn,(uint)Pixel(p,100+125+1,50+160+1),"page buttons above the tab background");
        t.Equal(Exit,(uint)Pixel(p,100+180+1,50+4+1),"exit button");
        p.Mouse(M(UiMouseKind.Move,100+10+1,50+24+1,MouseButtons.None),new UiNullCommands());
        t.Equal(TabHi,(uint)Pixel(p,100+10+1,50+24+1),"hovered tab uses HighlightSurf");
        Click(p,100+10+1,50+24+1);
        t.Equal(GameRankingTab.Level,p.ActiveTab,"tab click switches");
        Click(p,100+130+1,50+24+1);
        t.Equal(GameRankingTab.Kill,p.ActiveTab,"Kill tab selected");
        t.Equal(Tab,(uint)Pixel(p,100+130+1,50+24+1),"missing PushSurf falls back to BaseSurf");
        Click(p,100+125+1,50+160+1);
        t.Check(p.Unhandled.Contains("NextPage")&&p.Visible,"page buttons not wired (logged)");
        t.Check(p.IsDragHandle(new Point(100+2,50+2))&&!p.IsDragHandle(new Point(100+181,50+5))&&!p.IsDragHandle(new Point(100+2,50+30)),"drag strip = Moveable, buttons excluded");
        p.MoveTo(new Point(10,10));
        t.Equal(new Rectangle(10,10,200,200),p.Bounds,"moved");
        var c=new UiNullCommands();
        p.Mouse(M(UiMouseKind.Down,10+181,10+5),c);p.Mouse(M(UiMouseKind.Move,10+100,10+100),c);p.Mouse(M(UiMouseKind.Up,10+100,10+100),c);
        t.Check(p.Visible,"release outside the button does not fire");
        Click(p,10+181,10+5);
        t.Check(!p.Visible,"Exit closes");
        t.Check(!p.Mouse(M(UiMouseKind.Down,50,50),c),"closed window ignores mouse");
        t.Check(p.Toggle()&&p.Visible&&p.Toggle()&&!p.Visible,"toggle");
        IUiClosablePanel closable=p;p.Open();closable.Close();
        t.Check(!p.Visible,"IUiClosablePanel.Close");
    }

    static void Event(WorldModuleTestContext t){
        GameRankingPanel.Detach();
        string[] args=Rows(10,"甲,5");string[] lines=["排名：殺人榜","No.1 甲（5）"];
        _=GameRankingPanel.Registration;   // static constructor subscribes once
        t.Check(!PlayerQueryPackets.RequestRankingWindow(1,"y_k_2",args,lines),"no panel attached (--classic-ui) → query page fallback");
        var p=Loaded();p.Attach();
        t.Check(GameRankingPanel.IsAttached(p),"attached");
        t.Check(PlayerQueryPackets.RequestRankingWindow(1,"y_k_2",args,lines),"attached usable panel claims the page");
        p.Update(World);
        t.Check(p.Visible&&p.ActiveTab==GameRankingTab.Kill,"opened from the event");
        t.Check(!PlayerQueryPackets.RequestRankingWindow(1,"y_k_2",Rows(9),lines),"malformed page → fallback");
        var broken=Loaded(id=>id==900?null:Get(id));broken.Attach();
        t.Check(!PlayerQueryPackets.RequestRankingWindow(1,"y_k_2",args,lines),"unavailable panel → fallback");
        EventHandler<RankingWindowEventArgs> thrower=(_,_)=>throw new InvalidOperationException("synthetic");
        GameRankingPanel.Detach();
        PlayerQueryPackets.RankingWindowRequested+=thrower;
        try{t.Check(!PlayerQueryPackets.RequestRankingWindow(1,"y_k_2",args,lines),"throwing handler → fallback, not fatal");}
        finally{PlayerQueryPackets.RankingWindowRequested-=thrower;}
        var e=new RankingWindowEventArgs(7,"y_w_1",args,lines);
        t.Check(e.ObjectId==7&&e.Html=="y_w_1"&&e.Lines.Count==2&&!e.Handled,"event args");
    }

    static void Registration(WorldModuleTestContext t){
        var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==GameRankingPanel.PanelId);
        t.Check(reg!=null&&reg.Layout==GameRankingPanel.HostLayout,"[UiPanel] discovered with host layout uistatus86");
        t.Check(reg?.Create(new UiRoot()) is GameRankingPanel {Layer:UiLayer.Window} and IUiClosablePanel and IUiMovablePanel,"window, closable, movable");
        t.Check(UiPanelRegistry.Errors.Count==0,"no discovery errors",UiPanelRegistry.Errors);
    }
}
