using System.Drawing.Imaging;
namespace P06.NativeUi;

// M7-W（T223／#530）ui-summon 的離線測試：XML 與 .img 全部由本檔合成（元素名稱照 SummonUI.xml 的結構，座標與尺寸是測試自訂值），
// 不讀真實客戶端。只有設定環境變數 TW_NATIVE_UI_SUMMON_PNG（輸出 PNG 路徑，請放 scratch/）時，才額外用真實 Tile／Sprite 渲染一張。
static class SummonPanelTests {
    sealed class Host:ISummonUiHost {
        public SummonUiState State{get;set;}=new(false,0,"",null,null,false);
        public readonly List<string> Calls=[];
        public void Choose(SummonUiState shown,SummonMonster monster)=>Calls.Add($"choose:{shown.Revision}:{monster.Action}");
        public void Close(SummonUiState shown)=>Calls.Add($"close:{shown.Revision}");
    }
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Red=0xFFFF0000,Green=0xFF00FF00,Blue=0xFF0000FF,Gray=0xFF808080,White=0xFFFFFFFF;

    static string Item(string name,int baseSurf,string caption)=>
        $"<ListItem Name=\"{name}\" BaseSurf=\"{baseSurf}\" OverSurf=\"30;4\" DisableOverSurf=\"13\" MouseEvent=\"X\"><Label Name=\"Level_Label\" X=\"4\" Y=\"10\" Width=\"8\" Height=\"6\" Caption=\"{caption}\"/><Image Name=\"LevelLabel_Image\" X=\"3\" Y=\"9\" Surf=\"31;1\"/><Tooltip Name=\"Desc_Tooltip\" X=\"1\" Y=\"1\" Desc=\"1\"/></ListItem>";
    static string Layout(int windowBase=1)=>"<?xml version=\"1.0\"?><SummonUI Toggle=\"true\"><Image Name=\"SummonDrag_Image\" X=\"0\" Y=\"0\" Surf=\"0;2;13\" Activate=\"0\"/>"+
        $"<Window Name=\"SummonMonster_Window\" X=\"40\" Y=\"30\" BaseSurf=\"{windowBase};1\" FrameSurf=\"2;1\" Moveable=\"12\">"+
        "<Button Name=\"CloseWindow_Button\" X=\"90\" Y=\"2\" BaseSurf=\"3\" PushSurf=\"4\"/>"+
        "<Image Name=\"SummonTitle_Image\" X=\"5\" Y=\"14\" Surf=\"5;1\"/>"+
        "<List Name=\"QuickSummonLevel_List\" X=\"5\" Y=\"20\" Width=\"60\" Height=\"20\" Horizontal=\"1\" Gap=\"2\">"+Item("Q0",20,"?")+Item("Q1",20,"?")+Item("Q2",20,"?")+"</List>"+
        "<List Name=\"Level_List\" X=\"5\" Y=\"50\" Width=\"60\" Height=\"60\" Vertical=\"1\" Gap=\"3\">"+
        string.Concat(Enumerable.Range(0,6).Select(r=>$"<List Name=\"LevelSub{r}_List\" Width=\"60\" Height=\"7\" Horizontal=\"1\" Gap=\"4\"><Button Name=\"Level0_Button\" BaseSurf=\"{40+2*r};1\" PushSurf=\"60;1\"/><Button Name=\"Level1_Button\" BaseSurf=\"{41+2*r};1\" PushSurf=\"60;1\"/></List>"))+"</List>"+
        "<Control Name=\"SummonLevelList_Control\" X=\"4\" Y=\"45\" Activate=\"0\"><List Name=\"SummonLevel_List\" X=\"1\" Y=\"2\" Width=\"60\" Height=\"40\" Vertical=\"1\" Gap=\"0\">"+
        string.Concat(Enumerable.Range(0,2).Select(r=>$"<List Name=\"SummonLevel{r}_List\" Width=\"60\" Height=\"18\" Horizontal=\"1\" Gap=\"0\">"+Item("S0",21,"80")+Item("S1",21,"80")+Item("S2",21,"80")+"</List>"))+"</List></Control>"+
        "<Button Name=\"Ok_Button\" X=\"10\" Y=\"110\" BaseSurf=\"6\" HighlistSurf=\"7\" PushSurf=\"8;1\"/>"+
        "<Button Name=\"Cancel_Button\" X=\"40\" Y=\"110\" BaseSurf=\"9\" PushSurf=\"10;1\"/>"+
        "<Button Name=\"Back_Button\" X=\"40\" Y=\"110\" BaseSurf=\"11\" PushSurf=\"12;1\" Activate=\"0\"/>"+
        "<Label Name=\"Charisma_Label\" X=\"70\" Y=\"14\" Width=\"16\" Height=\"8\" Caption=\"0\"/>"+
        "<Image Name=\"CharismaText_Image\" X=\"60\" Y=\"15\" Surf=\"14;1\"/><Image Name=\"CharismaLabel_Image\" X=\"69\" Y=\"13\" Surf=\"15;1\"/>"+
        "</Window></SummonUI>";
    // 12 個等級（10、12…32），第一級 3 隻、第二級 1 隻、其餘各 1 隻。
    static string List(string? extra=null){
        var rows=new List<string>{"<summon level=\"10\"><monster id=\"1\" level=\"10\" action=\"7\" desc=\"1\" surf=\"100\" minCha=\"8\"/><monster id=\"2\" level=\"10\" action=\"263\" desc=\"2\" surf=\"101\" minCha=\"8\"/><monster id=\"3\" level=\"10\" action=\"519\" desc=\"3\" surf=\"102\" minCha=\"8\"/></summon>"};
        for(int i=1;i<12;i++)rows.Add($"<summon level=\"{10+2*i}\"><monster id=\"{10+i}\" level=\"{10+2*i}\" action=\"{100+i}\" desc=\"0\" surf=\"{110+i}\" minCha=\"{8+i}\"/></summon>");
        return "<?xml version=\"1.0\"?><summonlist>"+string.Concat(rows)+(extra??"")+"</summonlist>";
    }
    static Dictionary<int,UiSurface> Surfaces(){
        var d=new Dictionary<int,UiSurface>{[1]=Surface(1,100,120,Gray),[3]=Surface(3,8,8,Red),[4]=Surface(4,8,8,Green),[6]=Surface(6,20,8,Blue),[8]=Surface(8,20,8,Green),
            [9]=Surface(9,20,8,Red),[11]=Surface(11,20,8,Green),[20]=Surface(20,18,16,Blue),[21]=Surface(21,18,18,Red),[30]=Surface(30,18,18,White),[60]=Surface(60,10,7,Green)};
        for(int i=40;i<52;i++)d[i]=Surface(i,10,7,Blue);
        d[100]=Surface(100,6,6,Green);
        return d;
    }
    static SummonPanel Panel(Host host,Dictionary<int,UiSurface>? surfs=null,string? layout=null,string? list=null){
        var map=surfs??Surfaces();var p=new SummonPanel();
        p.Load(UiLayout.Parse(layout??Layout(),"SummonUI.xml"),UiLayout.Parse(list??List(),"summonList.xml"),id=>map.GetValueOrDefault(id));
        p.Attach(host);return p;
    }
    static readonly IUiGameState World=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,int x,int y)=>new(k,new(x,y),MouseButtons.Left,1,0,Keys.None);
    static void Click(SummonPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at.X,at.Y),c);p.Mouse(M(UiMouseKind.Up,at.X,at.Y),c);}
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);

    [WorldModuleTest("summon-panel")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊與 summonList 解析。
        t.Check(UiPanelRegistry.Panels.Count(r=>r.Id==SummonPanel.PanelId&&r.Layout==SummonPanel.LayoutName)==1,"ui-summon registered once (SummonUI.xml)");
        var catalog=SummonCatalog.Parse(UiLayout.Parse(List(),"summonList.xml"));
        t.Equal(14,catalog.Count,"14 synthetic monsters");
        t.Equal("10,12,14,16,18,20,22,24,26,28,30,32",string.Join(",",SummonCatalog.Levels(catalog)),"12 levels ascending");
        t.Check(catalog[1] is {Id:2,Level:10,Action:"263",Surf:101,MinCha:8},"monster attributes kept",catalog[1]);
        t.Throws<UiLayoutException>(()=>SummonCatalog.Parse(UiLayout.Parse(List("<summon level=\"40\"><monster id=\"99\" level=\"40\" action=\"7\" desc=\"0\" surf=\"1\" minCha=\"1\"/></summon>"),"summonList.xml")),"duplicate action rejected");
        t.Throws<UiLayoutException>(()=>SummonCatalog.Parse(UiLayout.Parse(List("<summon level=\"40\"><monster id=\"99\" level=\"41\" action=\"999\" desc=\"0\" surf=\"1\" minCha=\"1\"/></summon>"),"summonList.xml")),"level mismatch rejected");
        t.Throws<UiLayoutException>(()=>SummonCatalog.Parse(UiLayout.Parse(List("<summon level=\"40\"><monster id=\"99\" level=\"40\" action=\"a1\" desc=\"0\" surf=\"1\" minCha=\"1\"/></summon>"),"summonList.xml")),"non-numeric action rejected");
        t.Throws<UiLayoutException>(()=>SummonCatalog.Parse(UiLayout.Parse("<other/>","summonList.xml")),"wrong root rejected");
        t.Throws<UiLayoutException>(()=>new SummonPanel().Load(UiLayout.Parse(Layout(),"SummonUI.xml"),UiLayout.Parse(List("<summon level=\"40\"><monster id=\"99\" level=\"40\" action=\"999\" desc=\"0\" surf=\"1\" minCha=\"1\"/></summon>"),"summonList.xml"),_=>null),"13 levels vs 12 buttons rejected");
        t.Throws<UiLayoutException>(()=>new SummonPanel().Load(UiLayout.Parse(Layout().Replace("Name=\"Ok_Button\"","Name=\"Okay\""),"SummonUI.xml"),UiLayout.Parse(List(),"summonList.xml"),_=>null),"missing Ok_Button rejected (no invented layout)");
        t.Throws<UiLayoutException>(()=>new SummonPanel().Load(UiLayout.Parse(Layout().Replace("Horizontal=\"1\" Gap=\"2\"","Gap=\"2\""),"SummonUI.xml"),UiLayout.Parse(List(),"summonList.xml"),_=>null),"list without direction rejected");

        // 2. 版面：視窗位置＝XML (X,Y)，大小＝底圖；清單照 Gap 與底圖尺寸排列。
        var host=new Host();var p=Panel(host);var cmd=new UiNullCommands();
        t.Check(p.Usable,"all surfs present → usable");
        t.Equal(new Rectangle(40,30,100,120),p.Bounds,"bounds = window XML position + base surf size");
        t.Equal(new Rectangle(130,32,8,8),p.PartRect("close"),"close button at window + (90,2)");
        t.Equal(new Rectangle(45,50,18,16),p.PartRect("quick:0"),"quick slot 0");
        t.Equal(new Rectangle(65,50,18,16),p.PartRect("quick:1"),"quick slot 1 = +width+gap 2");
        t.Equal(new Rectangle(45,80,10,7),p.PartRect("level:0"),"level button 0 (row 0, col 0)");
        t.Equal(new Rectangle(59,80,10,7),p.PartRect("level:1"),"level button 1 = +10+gap 4 (row-major)");
        t.Equal(new Rectangle(45,90,10,7),p.PartRect("level:2"),"level button 2 = next row (+7+gap 3)");
        t.Equal(new Rectangle(59,130,10,7),p.PartRect("level:11"),"level button 11 = row 5, col 1");
        t.Equal(new Rectangle(45,77,18,18),p.PartRect("monster:0"),"monster slot 0 = window + control (4,45) + list (1,2)");
        t.Equal(new Rectangle(81,77,18,18),p.PartRect("monster:2"),"monster slot 2");
        t.Equal(new Rectangle(45,95,18,18),p.PartRect("monster:3"),"monster slot 3 = second row (+18)");
        t.Equal(new Rectangle(50,140,20,8),p.PartRect("ok"),"ok button");

        // 3. 可見性：宿主有請求且在世界中才顯示。
        p.Update(World);t.Check(!p.Visible,"hidden without a request");
        host.State=new(true,5,"server",28,25,true);
        p.Update(new UiStaticGameState());t.Check(!p.Visible,"hidden outside the world");
        p.Update(World);t.Check(p.Visible,"visible for an open request");

        // 4. 互動（R9-M #857 起：點怪物＝立即召喚並關窗、OK＝只關窗；停用＝等級公式或魅力。細節見 SummonWindowTests）。
        Click(p,Center(p.PartRect("ok")),cmd);
        t.Equal("close:5",host.Calls.LastOrDefault(),"ok only closes (sub_C006E0)");t.Check(!p.Visible,"hidden after ok");
        host.State=host.State with{Revision=6};p.Update(World);
        Click(p,Center(p.PartRect("level:11")),cmd);
        t.Equal((int?)10,p.SelectedLevel,"last level button = lowest summonList level (buttons run high → low)");
        t.Check(p.HitKey(Center(p.PartRect("cancel")))=="back","back replaces cancel in the monster list");
        Click(p,Center(p.PartRect("monster:1")),cmd);
        t.Equal("choose:6:263",host.Calls.LastOrDefault(),"click on monster slot 1 summons the second monster of the level");
        t.Check(!p.Visible,"…and closes the window");
        host.State=host.State with{Revision=7};p.Update(World);
        t.Check(p.Visible&&p.SelectedLevel==null,"new request revision shows again with the selection reset");
        Click(p,Center(p.PartRect("level:0")),cmd);
        t.Equal((int?)32,p.SelectedLevel,"first button = highest level");
        Click(p,Center(p.PartRect("monster:0")),cmd);
        t.Check(host.Calls.Count(c=>c.StartsWith("choose"))==1&&p.Visible,"monster failing the level formula cannot be summoned");
        t.Check(p.HitKey(Center(p.PartRect("monster:1")))==null,"empty slot not clickable");
        // Down 與 Up 在不同元件上不觸發。
        Click(p,Center(p.PartRect("back")),cmd);t.Check(p.SelectedLevel==null,"back returns to the level list");
        p.Mouse(M(UiMouseKind.Down,Center(p.PartRect("level:0")).X,Center(p.PartRect("level:0")).Y),cmd);p.Mouse(M(UiMouseKind.Up,Center(p.PartRect("level:1")).X,Center(p.PartRect("level:1")).Y),cmd);
        t.Check(p.SelectedLevel==null,"press and release on different buttons does nothing");
        t.Check(p.Mouse(M(UiMouseKind.Down,41,140),cmd),"click inside the window is consumed");
        t.Equal("quick:0",p.HitKey(Center(p.PartRect("quick:0"))),"quick slots are hit targets (R9-M: press summons, drop target)");
        // 取消送 Close 並立即隱藏。
        Click(p,Center(p.PartRect("cancel")),cmd);
        t.Equal("close:7",host.Calls.LastOrDefault(),"cancel closes the shown request");
        t.Check(!p.Visible,"hidden immediately after cancel");
        host.State=host.State with{Revision=8};p.Update(World);t.Check(p.Visible,"next request shows again");
        Click(p,Center(p.PartRect("close")),cmd);t.Equal("close:8",host.Calls.LastOrDefault(),"close button closes");
        host.State=host.State with{Revision=9,CanSend=false};p.Update(World);
        Click(p,Center(p.PartRect("level:11")),cmd);Click(p,Center(p.PartRect("monster:0")),cmd);
        t.Check(host.Calls.Count(c=>c.StartsWith("choose"))==1&&p.Visible,"no summon (window stays) while the host cannot send");

        // 5. 拖曳：上方 Moveable 高度是拖曳區（關閉鈕除外）。
        t.Check(p.IsDragHandle(new(50,35))&&!p.IsDragHandle(new(50,50))&&!p.IsDragHandle(Center(p.PartRect("close"))),"drag strip = top 12 px except close button");
        p.MoveTo(new(100,200));t.Equal(new Rectangle(100,200,100,120),p.Bounds,"MoveTo moves the window");
        t.Equal(new Rectangle(105,247,18,18),p.PartRect("monster:0"),"children follow the window");
        p.MoveTo(new(40,30));

        // 6. 繪製：1:1 原圖；滑鼠移上去的格疊 OverSurf（R9-M：不是選取）；缺 surf 不畫。
        Click(p,Center(p.PartRect("back")),cmd);Click(p,Center(p.PartRect("level:11")),cmd);
        p.Mouse(M(UiMouseKind.Move,41,140),cmd);t.Check(p.Hover==null,"mouse away from the slots: no hover");
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);
            t.Equal(Color.FromArgb(unchecked((int)Gray)).ToArgb(),canvas.Bitmap.GetPixel(40,30).ToArgb(),"window base drawn at origin");
            t.Equal(Color.Black.ToArgb(),canvas.Bitmap.GetPixel(140,30).ToArgb(),"nothing drawn outside the base");
            t.Equal(Color.FromArgb(unchecked((int)Green)).ToArgb(),canvas.Bitmap.GetPixel(46,78).ToArgb(),"monster surf (100) drawn in its slot");
            t.Equal(Color.FromArgb(unchecked((int)Red)).ToArgb(),canvas.Bitmap.GetPixel(64,78).ToArgb(),"slot of a monster whose surf is missing shows only the slot base");
            t.Equal(Color.FromArgb(unchecked((int)Red)).ToArgb(),canvas.Bitmap.GetPixel(60,80).ToArgb(),"unselected slot: no OverSurf");
        }
        p.Mouse(M(UiMouseKind.Move,Center(p.PartRect("monster:0")).X,Center(p.PartRect("monster:0")).Y),cmd);
        using(var canvas=new UiCanvas()){canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);
            t.Equal(Color.FromArgb(unchecked((int)White)).ToArgb(),canvas.Bitmap.GetPixel(60,80).ToArgb(),"hovered slot shows OverSurf");
            t.Equal(Color.FromArgb(unchecked((int)White)).ToArgb(),canvas.Bitmap.GetPixel(46,78).ToArgb(),"OverSurf drawn above the monster surf (R10-T FU13§4.2: monster = BaseSurf)");}

        // 7. 缺視窗底圖：面板停用（不畫替代圖），其他面板不受影響。
        var noBase=Surfaces();noBase.Remove(1);
        var broken=Panel(new Host{State=new(true,1,"server",50,10,true)},noBase);broken.Update(World);
        t.Check(!broken.Usable&&!broken.Visible,"missing window base disables only this panel");

        RealClient(t);
    }

    /// <summary>選用：TW_NATIVE_UI_SUMMON_PNG 指定輸出路徑時，以真實客戶端（TW_CLIENT_ROOT）建立面板並輸出 800×600 PNG。</summary>
    static void RealClient(WorldModuleTestContext t){
        string? output=Environment.GetEnvironmentVariable("TW_NATIVE_UI_SUMMON_PNG");
        if(string.IsNullOrWhiteSpace(output))return;
        using var res=UiResources.Open();
        var root=UiRoot.Build(res,UiPanelRegistry.Panels.Where(r=>r.Id==SummonPanel.PanelId).ToArray());
        var p=(SummonPanel)root.Find(SummonPanel.PanelId)!;var host=new Host{State=new(true,1,"server",72,44,true)};p.Attach(host);
        t.Check(p.Usable,"real client: every surf of SummonUI resolves");
        t.Equal(31,p.Monsters.Count,"real client: summonList has 31 monsters");
        root.Update(World);var cmd=new UiNullCommands();
        void Save(string suffix){using var canvas=new UiCanvas();root.Compose(canvas,null);string path=Path.ChangeExtension(output,null)+suffix+".png";Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);canvas.Bitmap.Save(path,ImageFormat.Png);}
        Save("-levels");
        Click(p,Center(p.PartRect("level:11")),cmd);Save("-level28");
        var first=Center(p.PartRect("monster:0"));p.Mouse(M(UiMouseKind.Move,first.X,first.Y),cmd);Save("-level28-hover");
        t.Equal("7",p.Monsters.First(m=>m.Level==28).Action,"real client: first monster of level 28 is action 7");
        var rects=new[]{"close","ok","cancel","quick:0","quick:1","quick:2","level:0","level:1","level:11","monster:0","monster:5"}.ToDictionary(k=>k,k=>p.PartRect(k).ToString());
        File.WriteAllText(Path.ChangeExtension(output,null)+"-rects.json",System.Text.Json.JsonSerializer.Serialize(new{bounds=p.Bounds.ToString(),rects},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
}
