namespace P06.NativeUi;

// R9-P（T376／#860）離線測試：FishingNet 開關 FishObtainUI（清單不開）、QuitFishing 先問 1516。
// 只用合成版面與合成 surf（純色方塊），不讀主客戶端或參考客戶端的 XML、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class FishingFollowupTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    // 釣魚窗：與 FishingPanelTests 相同結構的合成版面（自訂座標與 surf 編號）。
    const string FishingXml="""
        <?xml version="1.0"?>
        <FishingUI>
          <Window Name="Fishing_Window" X="100" Y="50" BaseSurf = "800;1" FrameSurf = "800;1" Moveable = "10" MouseEvent = "Dummy">
            <Button Name = "FishingNet_Button" X = "70" Y = "0" BaseSurf = "802;1" PushSurf = "803;1" MouseEvent = "FishingNet"/>
            <Button Name = "QuitFishing_Button" X = "86" Y = "2" BaseSurf = "805" PushSurf = "806" MouseEvent = "QuitFishing"/>
          </Window>
        </FishingUI>
        """;
    // FishObtainUI：參考客戶端 XML 的元素結構（座標照 FU9 §6.2 記載；surf 用合成編號），只留一列清單。
    const string ObtainXml="""
        <?xml version="1.0"?>
        <FishObtainUI Toggle="true">
          <Window Name="UserObtain_Window" X="0" Y="0" BaseSurf = "900;1" FrameSurf="901;1" Moveable = "20" MouseEvent = "Dummy" KeyboardEvent="FishKeyboard">
            <Button Name = "CloseWindow_Button" X = "225" Y = "15" BaseSurf = "902" PushSurf = "903" MouseEvent = "CloseFishWindow"/>
            <Label Name="Total_Label" X = "30" Y="20" Height="13" Width="20" String="3674;1"/>
            <List Name = "ObtainNewItem_List" X = "18" Y = "37" Width = "194" Height = "250" Vertical = "1" Gap = "1">
              <CheckButton Name = "Item0_Button" Width ="194" Height="35" MouseEvent = "ItemsListClick" Activate = "0"/>
            </List>
            <WheelControl Name="ItemsList_WheelArea" X = "18" Y = "37" Width = "194" Height = "250">
              <ScrollBar Name = "ItemsList_Scrollbar" X = "199" Y = "0" ScrollStart = "16" ScrollHeight = "224" Vertical = "1" MouseEvent="FishItemListScroll">
                <Image Name = "Background_Image" Surf = "904;1" X ="0" Y="14"/>
              </ScrollBar>
            </WheelControl>
          </Window>
        </FishObtainUI>
        """;

    const uint FishBase=0xFF102030,ObtainBase=0xFF203040,ObtainFrame=0xFF304050,Close=0xFF00FF00,ClosePush=0xFF008800,Scroll=0xFFFF00FF;
    static readonly Dictionary<int,UiSurface> Surfs=new(){
        [800]=Surface(800,100,24,FishBase),[802]=Surface(802,12,12,0xFF400000),[803]=Surface(803,12,12,0xFFFF0000),
        [805]=Surface(805,10,10,0xFF00FFFF),[806]=Surface(806,10,10,0xFF008888),
        [900]=Surface(900,240,300,ObtainBase),[901]=Surface(901,8,8,ObtainFrame),[902]=Surface(902,10,10,Close),[903]=Surface(903,10,10,ClosePush),
        [904]=Surface(904,12,200,Scroll)};
    static UiSurface? Get(int id)=>Surfs.TryGetValue(id,out var s)?s:null;
    static readonly UiStaticGameState World=new(InWorld:true,CharacterName:"釣客",Level:52);
    static FishingWindowEventArgs E(bool active,int revision,bool pending=false)=>new(7,active,pending,"",revision);
    static UiMouseEvent M(UiMouseKind k,int x,int y)=>new(k,new Point(x,y),MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,int x,int y){var c=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
    static UiKeyEvent K(Keys k)=>new(k,true,false);
    static uint Pixel(IUiPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return (uint)canvas.Bitmap.GetPixel(x,y).ToArgb();}

    static FishObtainPanel Obtain(Func<FishingPanel?>? fishing=null,Func<int,UiSurface?>? surf=null){
        var o=new FishObtainPanel(null,fishing);o.Load(UiLayout.Parse(ObtainXml,"FishObtainUI.xml"),surf??Get);return o;
    }
    static FishingPanel Fishing(Func<bool> quit,FishObtainPanel? obtain){
        var p=new FishingPanel(null,quit,obtain==null?null:()=>obtain);p.Load(UiLayout.Parse(FishingXml,"FishingUI.xml"),Get);
        p.Offer(E(true,1));p.Update(World);return p;
    }
    const int NetX=100+70+1,NetY=50+1,QuitX=100+86+1,QuitY=50+3;

    [WorldModuleTest("fishing-followup")]
    static void Run(WorldModuleTestContext t){
        try{
            ObtainLayout(t);
            NetToggle(t);
            QuitConfirm(t);
            Prompt(t);
            Registration(t);
        }finally{FishingPanel.Detach();}
    }

    static void ObtainLayout(WorldModuleTestContext t){
        var o=Obtain();
        t.Check(o.Available&&!o.Visible,"synthetic FishObtainUI usable, closed after load",o.Unavailable);
        t.Equal(new Size(240,300),o.Size,"size = union of BaseSurf / FrameSurf");
        t.Equal(20,o.Moveable,"Moveable");
        t.Check(o.CloseButton is {At.X:225,At.Y:15} b&&b.Base?.Surf==902&&b.Push?.Surf==903,"close button (225,15)");
        t.Check(o.TitleString==FishObtainPanel.TitleStringId&&o.TitleAt==new Point(30,20),"Total_Label String 3674 at (30,20)");
        t.Equal(new Rectangle(18,37,194,250),o.ListArea,"list area position kept");
        // R10-A（#888）：清單與捲軸照原版接上；這份合成版面的那一列沒有子節點 → 只有那一列記為不建。
        t.Check(o.Rows.Count==0&&o.ScrollBack?.Surf==904&&o.Skipped.Count==1&&o.Skipped[0].Contains("CheckButton[0]"),"list built from XML, scrollbar kept (R10-A)",o.Skipped);
        t.Check(o.Missing.Count==0,"no missing surf",o.Missing);
        if(ServerMessageText.Table is not {} table||FishObtainPanel.TitleStringId>=table.Count)
            t.Check(o.Title==null,"string 3674 not in the table → no title (no substitute)");
        var noClose=Obtain(surf:id=>id==902?null:Get(id));
        t.Check(noClose.Available&&noClose.Missing.Any(m=>m.Contains("902")),"missing close surf listed, not drawn");
        var noBase=Obtain(surf:id=>id is 900 or 901?null:Get(id));
        t.Check(!noBase.Available&&!noBase.Open(Rectangle.Empty),"no BaseSurf / FrameSurf → unavailable");
        t.Throws<UiLayoutException>(()=>new FishObtainPanel().Load(UiLayout.Parse(ObtainXml.Replace("<FishObtainUI Toggle=\"true\">","<UserObtainUI>").Replace("</FishObtainUI>","</UserObtainUI>"),"x.xml"),Get),"wrong root");
        t.Throws<UiLayoutException>(()=>new FishObtainPanel().Load(UiLayout.Parse(ObtainXml.Replace("\"UserObtain_Window\"","\"Other\""),"x.xml"),Get),"wrong window name");
        var host=new FishObtainPanel();host.Load(UiLayout.Parse(ObtainXml,"x.xml"),(SurfCatalog)null!);
        t.Check(!host.Available&&host.Unavailable=="沒有 UiResources","product Load without resources → unavailable");
    }

    static void NetToggle(WorldModuleTestContext t){
        FishingPanel? fishing=null;
        var o=Obtain(()=>fishing);
        int quits=0;fishing=Fishing(()=>{quits++;return true;},o);
        Click(fishing,NetX,NetY);
        t.Check(o.Visible&&quits==0&&!fishing.Unhandled.Contains("FishingNet"),"FishingNet opens FishObtainUI, sends nothing");
        t.Equal(new Point(200,50),o.Origin,"opens right of the fishing window (推測)");
        t.Equal(ObtainBase,Pixel(o,200+18+50,50+37+50),"list area not drawn (base only)");
        t.Equal(Scroll,Pixel(o,200+18+199+2,50+37+14+5),"scrollbar background drawn (R10-A; empty table → no rows)");
        t.Equal(ObtainFrame,Pixel(o,200+1,50+1),"frame drawn over base");
        t.Equal(Close,Pixel(o,200+225+1,50+15+1),"close button drawn");
        Click(fishing,NetX,NetY);
        t.Check(!o.Visible,"FishingNet again closes it");
        Click(fishing,NetX,NetY);
        Click(o,200+225+1,50+15+1);
        t.Check(!o.Visible,"CloseFishWindow button closes");
        Click(fishing,NetX,NetY);
        t.Check(o.Key(K(Keys.Escape),new UiNullCommands())&&!o.Visible,"FishKeyboard Esc closes");
        Click(fishing,NetX,NetY);
        t.Check(o.IsDragHandle(new Point(200+5,50+5))&&!o.IsDragHandle(new Point(200+5,50+25)),"drag strip = Moveable 20");
        o.MoveTo(new Point(10,10));o.Update(World);
        t.Check(o.Visible&&o.Bounds==new Rectangle(10,10,240,300),"moved, still open while fishing");
        fishing.Offer(E(false,2));fishing.Update(World);o.Update(World);
        t.Check(!o.Visible,"fishing end closes FishObtainUI (推測)");
        fishing.Offer(E(true,3));fishing.Update(World);Click(fishing,NetX,NetY);
        t.Equal(new Point(200,50),o.Origin,"reopened next to the fishing window again");
        o.Update(new UiStaticGameState(InWorld:false));
        t.Check(!o.Visible,"leaving the world closes it");
        var broken=Obtain(surf:id=>id is 900 or 901?null:Get(id));
        var f2=Fishing(()=>true,broken);Click(f2,NetX,NetY);
        t.Check(!broken.Visible&&f2.Unhandled.Contains("FishingNet"),"unavailable FishObtainUI → FishingNet logged only");
        var f3=Fishing(()=>true,null);Click(f3,NetX,NetY);
        t.Check(f3.Unhandled.Contains("FishingNet"),"no FishObtainUI panel → logged only");
    }

    static void QuitConfirm(WorldModuleTestContext t){
        int quits=0;var p=Fishing(()=>{quits++;return true;},null);
        Click(p,QuitX,QuitY);
        t.Check(quits==0&&p.PendingQuit is {} q&&q.Text==FishingQuitText.Confirm,"QuitFishing asks 1516, sends nothing");
        int rev=p.PendingQuit!.Revision;
        Click(p,QuitX,QuitY);
        t.Equal(rev,p.PendingQuit!.Revision,"second click while asking does not re-ask");
        t.Check(!p.AnswerQuit(rev+1,true)&&quits==0&&p.PendingQuit!=null,"stale revision ignored");
        t.Check(p.AnswerQuit(rev,false)&&quits==0&&p.PendingQuit==null&&p.CanQuit,"No → nothing sent, can ask again");
        t.Check(!p.AnswerQuit(rev,true)&&quits==0,"answer after the question is gone does nothing");
        Click(p,QuitX,QuitY);
        t.Check(p.AnswerQuit(p.PendingQuit!.Revision,true)&&quits==1&&!p.CanQuit,"Yes → C62 route once");
        Click(p,QuitX,QuitY);
        t.Check(p.PendingQuit==null&&quits==1,"no question while C62 is pending");
        var end=Fishing(()=>{quits++;return true;},null);
        Click(end,QuitX,QuitY);int r2=end.PendingQuit!.Revision;
        end.Offer(E(false,2));end.Update(World);
        t.Check(end.PendingQuit==null&&!end.AnswerQuit(r2,true)&&quits==1,"fishing end retires the question");
        var server=Fishing(()=>{quits++;return true;},null);
        Click(server,QuitX,QuitY);
        server.Offer(E(true,2,pending:true));server.Update(World);
        t.Check(server.PendingQuit==null,"C62 sent elsewhere (HUD) retires the question");
        var closed=Fishing(()=>true,null);Click(closed,QuitX,QuitY);closed.Close();
        t.Check(closed.PendingQuit==null,"closing the window retires the question");
        var away=Fishing(()=>true,null);Click(away,QuitX,QuitY);away.Update(new UiStaticGameState(InWorld:false));
        t.Check(away.PendingQuit==null,"leaving the world retires the question");
        if(ServerMessageText.Table is not {} table||FishingQuitText.ConfirmId>=table.Count)
            t.Equal("你要取消自動釣魚嗎？",FishingQuitText.Confirm,"1516 fallback = 0-客戶端 string-c.tbl text");
    }

    static void Prompt(WorldModuleTestContext t){
        int quits=0;var p=Fishing(()=>{quits++;return true;},null);
        var panels=new Dictionary<string,IUiPanel>{[FishingPanel.PanelId]=p};
        var prompt=new FishingQuitPromptPanel(id=>panels.GetValueOrDefault(id));
        var c=new UiNullCommands();
        prompt.Update(World);
        t.Check(!prompt.Visible&&!prompt.Key(K(Keys.Y),c),"no question → hidden, keys pass");
        Click(p,QuitX,QuitY);
        t.Check(prompt.Visible&&prompt.Bounds==YesNoButtons.Line&&prompt.Current?.Text==FishingQuitText.Confirm,"1516 on the input line");
        t.Check(!prompt.Key(K(Keys.Y|Keys.Control),c)&&!prompt.Key(new UiKeyEvent(Keys.Y,true,true),c)&&p.PendingQuit!=null,"modifier / repeat ignored");
        t.Check(!prompt.Key(K(Keys.A),c)&&p.PendingQuit!=null,"other keys pass");
        t.Check(prompt.Key(K(Keys.N),c)&&quits==0&&!prompt.Visible,"N = no");
        Click(p,QuitX,QuitY);
        t.Check(prompt.Key(K(Keys.Escape),c)&&quits==0&&!prompt.Visible,"Esc = no");
        Click(p,QuitX,QuitY);
        t.Check(!prompt.Mouse(M(UiMouseKind.Down,YesNoButtons.Line.X+1,YesNoButtons.Line.Y+1),c),"mouse never captured");
        prompt.Update(new UiStaticGameState(InWorld:false));
        t.Check(!prompt.Visible,"not in world → hidden");
        prompt.Update(World);
        t.Check(prompt.Key(K(Keys.Y),c)&&quits==1&&!prompt.Visible,"Y = yes → C62 route");
        var orphan=new FishingQuitPromptPanel();orphan.Update(World);
        t.Check(!orphan.Visible&&!orphan.Key(K(Keys.Y),c),"no fishing panel → nothing");
    }

    static void Registration(WorldModuleTestContext t){
        var obtain=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==FishObtainPanel.PanelId);
        t.Check(obtain!=null&&obtain.Layout==FishingPanel.HostLayout,"FishObtainUI discovered with host layout uistatus86");
        t.Check(obtain?.Create(new UiRoot()) is FishObtainPanel {Layer:UiLayer.Window} and IUiClosablePanel and IUiMovablePanel,"window, closable, movable");
        var prompt=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==FishingQuitPromptPanel.PanelId);
        t.Check(prompt?.Create(new UiRoot()) is FishingQuitPromptPanel {Layer:UiLayer.Float},"1516 prompt discovered (Float)");
        t.Check(UiPanelRegistry.Errors.Count==0,"no discovery errors",UiPanelRegistry.Errors);
    }
}
