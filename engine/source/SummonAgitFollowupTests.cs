using P06.NativeUi;
namespace P06;

// R10-O（T399／#914，gap-matrix 15.12.6）召喚與盟屋拍賣小項的離線測試：
// - 召喚窗：拖曳中畫 SummonDrag_Image（被拖的怪物圖，中心對齊游標、透明度＝XML 第三欄/32，推測）；
//   送出前用宿主目前的等級只檢查等級公式（sub_C00F50），不成立 → 宿主顯示 743、不送、關窗（FU9§3.1）。
// - 盟屋拍賣：原生 Bid_Window 可用時，HyperText 頁不為 agapply 再出數字框（WIN2§5.3）。
// XML、summonList、.img 全部由本檔合成（元素名稱照 SummonUI.xml 的結構，座標、尺寸是測試自訂值），不讀主客戶端或參考客戶端、
// 不開遊戲、網路或視窗、不寫檔。CI 由 --module-tests 自動發現執行（名稱 summon-agit-followup）。
static class SummonAgitFollowupTests {
    sealed class Host:ISummonUiHost {
        public SummonUiState State{get;set;}=new(false,0,"",null,null,false);
        public readonly List<string> Calls=[];
        public void Choose(SummonUiState shown,SummonMonster monster)=>Calls.Add($"choose:{shown.Revision}:{monster.Action}");
        public void Close(SummonUiState shown)=>Calls.Add($"close:{shown.Revision}");
        public void LevelTooLow(SummonUiState shown,SummonMonster monster)=>Calls.Add($"743:{shown.Revision}:{monster.Action}");
    }
    /// <summary>不實作 LevelTooLow：走介面預設（不做事）。</summary>
    sealed class PlainHost:ISummonUiHost {
        public SummonUiState State{get;set;}=new(false,0,"",null,null,false);
        public readonly List<string> Calls=[];
        public void Choose(SummonUiState shown,SummonMonster monster)=>Calls.Add("choose");
        public void Close(SummonUiState shown)=>Calls.Add("close");
    }
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Red=0xFFFF0000,Green=0xFF00FF00,Blue=0xFF0000FF,Gray=0xFF808080,White=0xFFFFFFFF;

    static string Item(string name,int baseSurf)=>
        $"<ListItem Name=\"{name}\" BaseSurf=\"{baseSurf}\" OverSurf=\"30;4\" DisableOverSurf=\"13\" MouseEvent=\"X\"><Label Name=\"Level_Label\" X=\"12\" Y=\"2\" Width=\"6\" Height=\"6\" Caption=\"?\"/><Image Name=\"LevelLabel_Image\" X=\"1\" Y=\"14\" Surf=\"31;1\"/><Tooltip Name=\"Desc_Tooltip\" X=\"1\" Y=\"1\" Desc=\"0\"/></ListItem>";
    const string DragXml="<Image Name=\"SummonDrag_Image\" X=\"0\" Y=\"0\" Surf=\"0;2;13\" Activate=\"0\"/>";
    static string Layout(string? drag=DragXml)=>"<?xml version=\"1.0\"?><SummonUI Toggle=\"true\">"+(drag??"")+
        "<Window Name=\"SummonMonster_Window\" X=\"40\" Y=\"30\" BaseSurf=\"1;1\" FrameSurf=\"2;1\" Moveable=\"12\">"+
        "<Button Name=\"CloseWindow_Button\" X=\"90\" Y=\"2\" BaseSurf=\"3\" PushSurf=\"4\"/>"+
        "<Image Name=\"SummonTitle_Image\" X=\"5\" Y=\"14\" Surf=\"5;1\"/>"+
        "<List Name=\"QuickSummonLevel_List\" X=\"5\" Y=\"20\" Width=\"60\" Height=\"20\" Horizontal=\"1\" Gap=\"2\">"+
        string.Concat(Enumerable.Range(0,3).Select(i=>Item($"QuickSummon{i}_Button",20)))+"</List>"+
        "<List Name=\"Level_List\" X=\"5\" Y=\"50\" Width=\"60\" Height=\"60\" Vertical=\"1\" Gap=\"3\">"+
        string.Concat(Enumerable.Range(0,6).Select(r=>$"<List Name=\"LevelSub{r}_List\" Width=\"60\" Height=\"7\" Horizontal=\"1\" Gap=\"4\"><Button Name=\"Level0_Button\" BaseSurf=\"{40+2*r};1\" PushSurf=\"60;1\"/><Button Name=\"Level1_Button\" BaseSurf=\"{41+2*r};1\" PushSurf=\"60;1\"/></List>"))+"</List>"+
        "<Control Name=\"SummonLevelList_Control\" X=\"4\" Y=\"45\" Activate=\"0\"><List Name=\"SummonLevel_List\" X=\"1\" Y=\"2\" Width=\"60\" Height=\"40\" Vertical=\"1\" Gap=\"0\">"+
        string.Concat(Enumerable.Range(0,2).Select(r=>$"<List Name=\"SummonLevel{r}_List\" Width=\"60\" Height=\"18\" Horizontal=\"1\" Gap=\"0\">"+
            string.Concat(Enumerable.Range(0,3).Select(c=>Item($"Summon{c}_Button",21)))+"</List>"))+"</List></Control>"+
        "<Button Name=\"Ok_Button\" X=\"10\" Y=\"110\" BaseSurf=\"6\" PushSurf=\"8;1\"/>"+
        "<Button Name=\"Cancel_Button\" X=\"40\" Y=\"110\" BaseSurf=\"9\" PushSurf=\"10;1\"/>"+
        "<Button Name=\"Back_Button\" X=\"40\" Y=\"110\" BaseSurf=\"11\" PushSurf=\"12;1\" Activate=\"0\"/>"+
        "<Label Name=\"Charisma_Label\" X=\"70\" Y=\"14\" Width=\"16\" Height=\"8\" Caption=\"0\"/>"+
        "<Image Name=\"CharismaText_Image\" X=\"60\" Y=\"15\" Surf=\"14;1\"/><Image Name=\"CharismaLabel_Image\" X=\"69\" Y=\"13\" Surf=\"15;1\"/>"+
        "</Window></SummonUI>";
    // 12 個等級 28、32 … 72（等級 L 的 action%256＝(L−28)/4+7）。等級 28：id 1（action 7、surf 100）、id 2（action 263、surf 101 缺圖）。
    static string List()=>"<?xml version=\"1.0\"?><summonlist>"+
        "<summon level=\"28\"><monster id=\"1\" level=\"28\" action=\"7\" desc=\"0\" surf=\"100\" minCha=\"8\"/>"+
        "<monster id=\"2\" level=\"28\" action=\"263\" desc=\"0\" surf=\"101\" minCha=\"8\"/></summon>"+
        string.Concat(Enumerable.Range(1,11).Select(i=>$"<summon level=\"{28+4*i}\"><monster id=\"{10+i}\" level=\"{28+4*i}\" action=\"{7+i}\" desc=\"0\" surf=\"{110+i}\" minCha=\"8\"/></summon>"))+
        "</summonlist>";
    static Dictionary<int,UiSurface> Surfaces(){
        var d=new Dictionary<int,UiSurface>{[1]=Surface(1,100,120,Gray),[3]=Surface(3,8,8,Red),[6]=Surface(6,20,8,Blue),[9]=Surface(9,20,8,Red),[11]=Surface(11,20,8,Green),
            [20]=Surface(20,18,16,Blue),[21]=Surface(21,18,18,Red),[30]=Surface(30,18,18,White),[100]=Surface(100,10,8,Green)};
        for(int i=40;i<52;i++)d[i]=Surface(i,10,7,Blue);
        return d;
    }
    static SummonPanel Panel(ISummonUiHost host,ISummonQuickStore? store=null,string? layout=null){
        var map=Surfaces();var p=new SummonPanel{QuickStore=store??new SummonQuickMemory()};
        p.Load(UiLayout.Parse(layout??Layout(),"SummonUI.xml"),UiLayout.Parse(List(),"summonList.xml"),id=>map.GetValueOrDefault(id));
        p.Attach(host);return p;
    }
    static readonly IUiGameState World=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Down(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Down,at),c);
    static void Up(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Up,at),c);
    static void Move(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Move,at),c);
    static void Click(SummonPanel p,Point at,UiNullCommands c){Down(p,at,c);Up(p,at,c);}
    static Point C(SummonPanel p,string key){var r=p.PartRect(key);return new(r.X+r.Width/2,r.Y+r.Height/2);}

    [WorldModuleTest("summon-agit-followup")]
    static void Run(WorldModuleTestContext t){
        DragAttribute(t);DragImage(t);LevelCheck(t);AgitApply(t);
    }

    // 1. SummonDrag_Image 的 Surf：第三欄＝透明度（/32，推測）；只有 surf → 32；元素不存在 → null；第三欄不合法 → 版面錯誤。
    static void DragAttribute(WorldModuleTestContext t){
        UiNode? Drag(string xml)=>UiLayout.Parse("<SummonUI>"+xml+"</SummonUI>","SummonUI.xml").Root.ChildrenNamed("Image").FirstOrDefault(c=>c.Attr("Name")==SummonPanel.DragImageName);
        t.Equal((int?)13,SummonPanel.DragAlphaOf(Drag(DragXml)),"Surf 0;2;13 → alpha 13/32");
        t.Equal((int?)32,SummonPanel.DragAlphaOf(Drag("<Image Name=\"SummonDrag_Image\" Surf=\"0\"/>")),"surf field only → opaque");
        t.Equal((int?)null,SummonPanel.DragAlphaOf(Drag("<Image Name=\"Other\" Surf=\"0;2;13\"/>")),"no SummonDrag_Image → null");
        t.Throws<UiLayoutException>(()=>SummonPanel.DragAlphaOf(Drag("<Image Name=\"SummonDrag_Image\" Surf=\"0;2;x\"/>")),"non-number alpha rejected");
        t.Throws<UiLayoutException>(()=>SummonPanel.DragAlphaOf(Drag("<Image Name=\"SummonDrag_Image\" Surf=\"0;2;40\"/>")),"alpha above 32 rejected");
        var p=Panel(new Host());
        t.Equal((int?)13,p.DragAlpha,"panel reads SummonDrag_Image from the layout root");
        t.Equal((int?)null,Panel(new Host(),layout:Layout(drag:null)).DragAlpha,"layout without SummonDrag_Image → no drag image");
    }

    // 2. 拖曳中：怪物圖中心對齊游標、在最上層、半透明；Bounds 涵蓋它；沒移出按下的格子、放開後、缺圖、XML 沒有元素 → 不畫。
    static void DragImage(WorldModuleTestContext t){
        var host=new Host{State=new(true,1,"server",30,20,true)};var p=Panel(host);var cmd=new UiNullCommands();
        p.Update(World);Click(p,C(p,"level:11"),cmd);
        t.Equal((int?)28,p.SelectedLevel,"level 28 page");
        Down(p,C(p,"monster:0"),cmd);
        t.Check(p.Dragging?.Id==1&&p.DragImage==null,"press only: drag candidate, no drag image yet");
        var far=new Point(300,300);Move(p,far,cmd);
        t.Equal((Rectangle?)new Rectangle(295,296,10,8),p.DragImage,"moved out: monster surf (10×8) centred on the cursor");
        t.Check(p.Bounds.Contains(p.DragImage!.Value)&&p.Bounds.Contains(p.Window),"Bounds covers the window and the drag image");
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Blue);canvas.Restrict(p.Bounds);p.Paint(canvas);
            var c=canvas.Bitmap.GetPixel(300,300);
            t.Check(c.R==0&&Math.Abs(c.G-255*13/32)<=2&&Math.Abs(c.B-255*19/32)<=2,"drag image blended at 13/32 over the background",c);
            t.Equal(Color.Blue.ToArgb(),canvas.Bitmap.GetPixel(290,300).ToArgb(),"nothing outside the drag image");
        }
        var inside=new Point(125,120);Move(p,inside,cmd);   // 視窗底圖（灰）上，沒有其他元件
        t.Equal((Rectangle?)new Rectangle(120,116,10,8),p.DragImage,"drag image follows the cursor over the window");
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Blue);canvas.Restrict(p.Bounds);p.Paint(canvas);
            var c=canvas.Bitmap.GetPixel(inside.X,inside.Y);
            int g=255*13/32+128*19/32,rb=128*19/32;
            t.Check(Math.Abs(c.R-rb)<=2&&Math.Abs(c.G-g)<=2&&Math.Abs(c.B-rb)<=2,"drag image blended above the window base",c);
            t.Equal(Color.FromArgb(unchecked((int)Gray)).ToArgb(),canvas.Bitmap.GetPixel(118,120).ToArgb(),"window base untouched beside it");
        }
        Up(p,far,cmd);
        t.Check(p.DragImage==null&&p.Bounds==p.Window&&host.Calls.Count==0,"released: no drag image, nothing sent",host.Calls);
        Down(p,C(p,"monster:1"),cmd);Move(p,far,cmd);
        t.Check(p.Dragging?.Id==2&&p.DragImage==null,"monster surf missing (101) → no drag image (no placeholder)");
        Up(p,far,cmd);
        var plain=Panel(new Host{State=new(true,1,"server",30,20,true)},layout:Layout(drag:null));plain.Update(World);
        Click(plain,C(plain,"level:11"),cmd);Down(plain,C(plain,"monster:0"),cmd);Move(plain,far,cmd);
        t.Check(plain.Dragging?.Id==1&&plain.DragImage==null,"no SummonDrag_Image in the layout → drag works, nothing drawn");
        // 拖到快速格照舊放進去。
        Up(plain,C(plain,"quick:0"),cmd);t.Check(plain.Quick[0]?.Id==1,"drop into a quick slot still assigns it");
    }

    // 3. 送出檢查（sub_C00F50）：宿主目前的等級不過等級公式 → 743、不送、關窗（請宿主關請求）；revision 換了或沒有等級 → 不動作。
    static void LevelCheck(WorldModuleTestContext t){
        t.Check(SummonPanel.SendAllowed(28,new(1,28,"7",0,0,99))&&!SummonPanel.SendAllowed(24,new(1,28,"7",0,0,99)),"level formula only: lv 28 sends action 7, lv 24 refuses");
        t.Check(SummonPanel.SendAllowed(28,new(1,28,"263",0,0,99)),"charisma not checked at send (minCha 99)");
        t.Check(!SummonPanel.SendAllowed(null,new(1,28,"7",0,0,0)),"no level → not sent (not guessed)");
        t.Check(!SummonPanel.SendAllowed(31,new(1,32,"8",0,0,0))&&SummonPanel.SendAllowed(32,new(1,32,"8",0,0,0)),"lv 31 refuses action 8, lv 32 allows it");

        var store=new SummonQuickMemory();store.Save([1,null,null]);
        var host=new Host{State=new(true,1,"local",30,20,true)};var p=Panel(host,store);var cmd=new UiNullCommands();
        p.Update(World);
        host.State=host.State with{Level=24};   // 面板這一幀還是 30（格子可用），宿主目前的等級已經掉到 24
        Down(p,C(p,"quick:0"),cmd);
        t.Check(host.Calls.SequenceEqual(["743:1:7","close:1"]),"current level fails the formula → 743, nothing sent, request closed",host.Calls);
        t.Check(!p.Visible,"window closes after 743");
        Up(p,C(p,"quick:0"),cmd);t.Equal(2,host.Calls.Count,"release after it does nothing more");

        host.Calls.Clear();host.State=new(true,2,"local",30,20,true);p.Update(World);
        host.State=host.State with{Revision=3};
        Down(p,C(p,"quick:0"),cmd);
        t.Check(host.Calls.Count==0&&p.Visible,"host request changed since the frame → nothing",host.Calls);
        host.State=new(true,4,"local",30,20,true);p.Update(World);host.State=host.State with{Level=null};
        Down(p,C(p,"quick:0"),cmd);
        t.Check(host.Calls.Count==0&&p.Visible,"host lost the level → nothing (not guessed)",host.Calls);
        host.State=host.State with{Level=30};
        Click(p,C(p,"level:11"),cmd);Click(p,C(p,"monster:0"),cmd);
        t.Check(host.Calls.SequenceEqual(["choose:4:7"])&&!p.Visible,"level fine → summons as before",host.Calls);

        // 宿主沒實作 LevelTooLow：介面預設不做事，面板照樣不送並關窗。
        var plainHost=new PlainHost{State=new(true,1,"server",30,20,true)};var q=Panel(plainHost,store);q.Update(World);
        plainHost.State=plainHost.State with{Level=24};Down(q,C(q,"quick:0"),cmd);
        t.Check(plainHost.Calls.SequenceEqual(["close"])&&!q.Visible,"default LevelTooLow: not sent, request closed",plainHost.Calls);

        t.Equal(ServerMessageText.Decode(RingWindowRules.SummonLevelMessage,[]).Text,ClientForm.SummonLevelText,"host chat text = message 743");
        t.Check(ClientForm.SummonLevelText.Length>0,"743 text not empty",ClientForm.SummonLevelText);
    }

    // 4. agapply 只交給原生 Bid_Window（WIN2§5.3）：可用時 HyperText 頁不出數字框；原生 UI 關閉／出價窗不可用時照舊；其他 S136 不受影響。
    static void AgitApply(WorldModuleTestContext t){
        t.Check(ClientForm.HyperTextOmitsNumber(AgitBidPanel.ApplyHtml,nativeBidAvailable:true),"agapply + native Bid_Window → page has no number box");
        t.Check(!ClientForm.HyperTextOmitsNumber(AgitBidPanel.ApplyHtml,nativeBidAvailable:false),"agapply without the native Bid_Window (classic UI / faulted / no XML) → page box kept");
        foreach(var html in new[]{"agsell","itemcount","howmanymake",""})
            t.Check(!ClientForm.HyperTextOmitsNumber(html,nativeBidAvailable:true),"other S136 pages keep the page box: "+html);
        t.Check(!ClientForm.HyperTextOmitsNumber("AGAPPLY",nativeBidAvailable:true),"html id compared exactly (same as AgitBidPanel.Update)");
        // 出價窗沒載入（沒有 UiResources）→ Available=false → 頁面數字框保留。
        t.Check(!new AgitBidPanel().Available,"Bid_Window not loaded → not available");
    }
}
