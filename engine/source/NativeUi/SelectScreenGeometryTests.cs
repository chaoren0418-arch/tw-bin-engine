namespace P06.NativeUi;

// #1206 (#1186 SM07) 離線測試：原生 UI 開啟、State=CharacterList、3 個角色時，原生選角畫面要整個畫出來並可操作。
// 版面是記憶體內合成的 XML，座標照 kor25 SelectUI.xml（plain sha256 b4eb1fa7…）的 Character_Select_Window／Message_Window，
// 而且跟兩個客戶端一樣，64xx 的 surf 全部是 60×80 的圖（6421、6488 也是）。不讀真實客戶端、不啟動遊戲、不連網路；
// CI 由 --module-tests 自動發現執行（WorldModuleTest；SelectCharPanel.Load(UiLayout, …) 是 internal，DemoChecks 看不到）。
static class SelectScreenGeometryTests {
    const uint PortraitC=0xFF803030,WorldC=0xFFFFFFFF;
    static UiSurface Portrait(int id)=>new(id,$"{id}","synthetic",new UiImage(60,80,0,0,Enumerable.Repeat(PortraitC,60*80).ToArray()));
    static UiSurface? Surf(int id)=>id is >=6400 and <6500?Portrait(id):null;

    static string Slot=>"""
            <Control Name="Slot_Control" Width="337" Height="124">
                <Image Name="Slot_Back" Surf="6429;3">
                    <Button Name="Select_Button" BaseSurf="6430;1" HighlightSurf="6431;1" PushSurf="6432;1" MouseEvent="SelectChar_SelectWin">
                        <Label Name="Level_Label" X="82" Y="32" String="dummy;1" Align="left" Width="50" Height="17"/>
                        <Label Name="Name_Label" X="117" Y="32" String="dummy;1" Align="left" Width="70" Height="17"/>
                    </Button>
                    <Button Name="WaitDelete_Button" BaseSurf="6430;1" HighlightSurf="6431;1" PushSurf="6432;1" MouseEvent="SelectChar_SelectWin">
                        <Label Name="Level_Label" X="82" Y="32" String="dummy;1" Align="left" Width="50" Height="17"/>
                        <Label Name="Name_Label" X="117" Y="32" String="dummy;1" Align="left" Width="70" Height="17"/>
                        <Image Name="WaitToDel_Image" X="254" Y="24" Surf="6486;1"/>
                    </Button>
                    <Button Name="Create_Button" BaseSurf="6426;1" HighlightSurf="6427;1" PushSurf="6428;1" MouseEvent="CreateChar_SelectWin"/>
                </Image>
                <Image Name="Slot_Lock_Back" Surf="6425;3">
                    <Button Name="Lock_Button" BaseSurf="6422;1" HighlightSurf="6423;1" PushSurf="6424;1" MouseEvent="SlotLock_SelectWin"/>
                </Image>
            </Control>
""";
    static string Info(string n)=>$"""<Label Name="{n}" Align="left" Width="150" Height="26" FontSize="12"/>""";
    static string Xml=>$"""
<?xml version="1.0" encoding="UTF-8"?>
<SelectUI>
    <Window Name="Message_Window" X="171" Y="153" Modal="1" Top="1" DarkBack="1" Activate="0" BaseSurf="6488;3">
        <MultiLabel Name="Message_Label" X="29" Y="31" FontSize="12" Width="394" Height="190" VAlign="center" LineMargin="3"/>
        <Button Name="Ok_Button" X="47" Y="237" BaseSurf="6489" HighlightSurf="6490" PushSurf="6491" MouseEvent="Ok_MessageWin"/>
        <Button Name="Cancel_Button" X="262" Y="237" BaseSurf="6492" HighlightSurf="6493" PushSurf="6494" MouseEvent="Cancel_MessageWin"/>
        <Image Name="Back_Image" Surf="6487;1"/>
    </Window>
    <Window Name="Character_Select_Window" X="0" Y="0" BaseSurf="6421" Activate="0">
        <Effect Name="Character_Ani" X="529" Y="91"/>
        <List Name="BigSlot_List" X="31" Y="28" Width="237" Height="496" Vertical="1">
{Slot}{Slot}{Slot}{Slot}
        </List>
        <List Name="CharacterInfo_List" X="582" Y="407" Width="250" Height="162" Vertical="1">
            {Info("Name_Label")}{Info("Level_Label")}{Info("Class_Label")}{Info("Pledge_Label")}{Info("Alignment_Label")}{Info("Birthday_Label")}
        </List>
        <Image Name="DeleteDelay_Image" X="529" Y="91"/>
        <Button Name="Up_Button" X="375" Y="218" BaseSurf="6411;4" HighlightSurf="6411;4" PushSurf="6412;4" MouseEvent="UpSlot_SelectWin"/>
        <Button Name="Down_Button" X="375" Y="271" BaseSurf="6413;4" HighlightSurf="6413;4" PushSurf="6414;4" MouseEvent="DownSlot_SelectWin"/>
        <Button Name="Enter_Button" X="550" Y="354" BaseSurf="6404" HighlightSurf="6405" PushSurf="6406" MouseEvent="Ok_SelectWin"/>
        <Button Name="Back_Button" X="14" Y="535" BaseSurf="6415;4" HighlightSurf="6416;4" PushSurf="6417;4" MouseEvent="Cancel_SelectWin"/>
        <Button Name="Delete_Button" X="284" Y="535" BaseSurf="6407" HighlightSurf="6408" PushSurf="6409" MouseEvent="DeleteChar_SelectWin"/>
        <Image Name="CharacterInfo_Back" X="435" Y="371" Surf="6410"/>
        <Image Name="Back_Image" X="0" Y="0"/>
    </Window>
</SelectUI>
""";

    static SelectCharPanel Load(UiRoot? root=null){
        var p=new SelectCharPanel(null,root);
        p.Load(UiLayout.Parse(Xml,"SelectUI.xml"),Surf);
        return p;
    }
    static Bitmap Compose(UiRoot root){
        using var canvas=new UiCanvas();
        // The world layer stands in for PaintScene's "等待真實世界初始化": white everywhere under the panels.
        root.Compose(canvas,(g,r)=>{using var b=new SolidBrush(Color.FromArgb(unchecked((int)WorldC)));g.FillRectangle(b,r);});
        return (Bitmap)canvas.Bitmap.Clone();
    }
    static bool Near(Color c,uint v)=>SelectCharPanelTests.Near(c,v);

    [WorldModuleTest("select-char-geometry")]
    static void Run(WorldModuleTestContext t){
        var c=new UiNullCommands();
        // ---- 1. layout: the 60×80 BaseSurf no longer decides the screen ------------------------------------------------
        var probe=Load();
        t.Check(probe.Available,"real-geometry SelectUI with 60×80 surfs is usable",probe.Unavailable);
        t.Equal(new Size(60,80),probe.Main!.Base!.Bitmap.Size,"BaseSurf 6421 is a 60×80 picture (as in both clients)");
        t.Equal(new Rectangle(0,0,800,600),probe.Main.Bounds,"Character_Select_Window without Width/Height → full 800×600 screen");
        t.Equal(new Rectangle(171,153,423,317),probe.Message!.Bounds,"Message_Window grows to hold Message_Label (29+394) and Ok/Cancel (237+80)");
        t.Equal(4,probe.Rows,"four Slot_Controls = four rows");

        // ---- 2. native UI on, CharacterList, 3 characters: shown, painted over the world, hit-tested everywhere ---------
        var root=new UiRoot();var p=Load(root);root.Add(p);
        var host=new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4,SelectCharPanelTests.Char("甲",0,52),SelectCharPanelTests.Char("乙",1,40),SelectCharPanelTests.Char("丙",3,7)));
        t.Check(!p.Visible&&!root.Usable(p),"no host attached → hidden");
        p.Attach(host);root.Update(new UiStaticGameState(false,null,null,null,null,null,null,0));
        t.Check(p.Visible&&root.Usable(p)&&!root.Faulted.Contains(p),"CharacterList + host → visible and usable");
        t.Equal(new Rectangle(0,0,800,600),p.Bounds,"panel bounds = full screen");
        t.Check(ReferenceEquals(root.HitTest(new Point(700,500)),p)&&ReferenceEquals(root.HitTest(new Point(150,300)),p),"hit test reaches the screen outside the 60×80 BaseSurf (was: world)");
        using(var b=Compose(root)){
            t.Check(Near(b.GetPixel(30,40),PortraitC),"BaseSurf drawn at the window origin as in the XML",b.GetPixel(30,40));
            t.Check(Near(b.GetPixel(150,95),0xFF000000)&&Near(b.GetPixel(480,300),0xFF000000)&&Near(b.GetPixel(790,590),0xFF000000),"world layer (waiting text) covered: black where no piece is drawn",b.GetPixel(150,95));
            t.Check(SelectCharPanelTests.Inked(b,new Rectangle(31+117,28+32,70,17),0xFF000000),"row 0 Name_Label drawn (outside the old 60×80 clip)");
            t.Check(SelectCharPanelTests.Inked(b,new Rectangle(31+117,28+248+32,70,17),0xFF000000),"row 2 Name_Label drawn");
            t.Check(Near(b.GetPixel(31+5,28+372+5),PortraitC),"row 3 (free slot) Create_Button drawn");
            t.Check(SelectCharPanelTests.Inked(b,new Rectangle(582,407,150,26),0xFF000000),"info Name_Label (X 582, outside the old clip) drawn");
            t.Check(Near(b.GetPixel(550+5,354+5),PortraitC),"Enter_Button drawn");
        }

        // ---- 3. operable: Up/Down, Enter, Delete → Message_Window Ok, Create on an empty slot → fallback -------------------
        t.Equal("甲",p.Selected,"first character selected");
        p.Key(SelectCharPanelTests.K(Keys.Down),c);t.Equal("乙",p.Selected,"Down key selects the next character");
        p.Key(SelectCharPanelTests.K(Keys.Down),c);p.Key(SelectCharPanelTests.K(Keys.Up),c);t.Equal("乙",p.Selected,"Up key moves back");
        p.Key(SelectCharPanelTests.K(Keys.Enter),c);
        t.Equal("enter:乙:3",string.Join(",",host.Calls),"Enter key enters with the list stamp");
        host.Calls.Clear();
        SelectCharPanelTests.Click(p,new(31+10,28+10),c);
        t.Equal("甲",p.Selected,"click on row 0's Select_Button selects it");
        SelectCharPanelTests.Click(p,new(284+10,560),c);
        t.Check(p.Shown==CharSelectMessage.ConfirmDelete,"Delete_Button (Y 535, outside the old clip) opens the confirm");
        t.Check(p.Bounds.Contains(171+47+5,153+237+5)&&p.Message!.Bounds.Contains(171+47+5,153+237+5),"Ok_Button lies inside Message_Window");
        SelectCharPanelTests.Click(p,new(171+47+5,153+237+5),c);
        t.Equal("delete:甲:3",string.Join(",",host.Calls),"Ok sends the delete");
        host.View=SelectCharPanelTests.ViewOf(4,SelectCharPanelTests.Char("甲",0,52),SelectCharPanelTests.Char("乙",1,40),SelectCharPanelTests.Char("丙",3,7));p.Sync();
        SelectCharPanelTests.Click(p,new(31+5,28+372+5),c);SelectCharPanelTests.Click(p,new(31+5,28+372+5),c,2);
        t.Equal(1,host.Fallbacks,"double click on the free slot without a usable create screen → WinForms creation dialog");

        // ---- 4. the legacy waiting text and the classic path -----------------------------------------------------------
        t.Check(!ClientForm.WaitingTextShown(true,false,true),"native select shown → PaintScene does not draw 等待真實世界初始化");
        t.Check(ClientForm.WaitingTextShown(true,false,false),"classic / no native select → waiting text as before");
        t.Check(!ClientForm.WaitingTextShown(true,true,false)&&!ClientForm.WaitingTextShown(false,false,false),"renderable world or offline → no waiting text (unchanged)");
        t.Check(!UiOptions.Resolve(false,true,null,true).Enabled,"--classic-ui → native UI off, so AttachNativeCharacterScreens never attaches (NativeUiActive false)");
        t.Check(!ClientForm.NativePanelUsable(false,root,SelectCharPanel.PanelId)&&ClientForm.NativePanelUsable(true,root,SelectCharPanel.PanelId),"native select usable only while the native UI is active");
        host.View=host.View with{State=LoginState.InWorld};root.Update(new UiStaticGameState(true,null,null,null,null,null,null,0));
        t.Check(!p.Visible&&p.Bounds.IsEmpty&&root.HitTest(new Point(400,300))==null,"in world → hidden, input goes to the world again");

        // ---- 5. diagnostics line --------------------------------------------------------------------------------------
        host.View=host.View with{State=LoginState.CharacterList};p.Sync();
        string line=ClientForm.NativeSelectTrace(LoginState.CharacterList,p,false);
        t.Check(line.StartsWith("select-char-ui 狀態 ")&&line.Contains("host=True")&&line.Contains("view=CharacterList")&&line.Contains("characters=3")&&line.Contains("visible=True")&&line.Contains("bounds=0,0,800x600")&&line.Contains("main=800x600"),"trace line lists host / view / visible / bounds",line);

        // ---- 6. helpers ------------------------------------------------------------------------------------------------
        var tiny=new CharUiWindow("w",new(10,10),Size.Empty,null,[]);
        t.Equal(Size.Empty,SelectCharPanel.CoverPieces(tiny).Size,"unsized window stays unsized (→ unavailable as before)");
        var declared=new CharUiWindow("w",Point.Empty,new(400,300),null,[]);
        t.Equal(new Size(400,300),SelectCharPanel.FullScreen(declared,true).Size,"declared Width/Height is kept");
        t.Equal(new Size(700,550),SelectCharPanel.FullScreen(declared with{Origin=new(100,50)},false).Size,"full screen from a non-zero origin");
    }
}
