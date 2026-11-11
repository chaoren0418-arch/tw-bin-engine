namespace P06.NativeUi;

// R11-D（T418／#960）離線測試：選角可以選空格（sub_EEC3B0：CreateChar_SelectWin 514 選列、515 開建角；sub_EEDF00／sub_EEEE60：
// 上下鍵在角色與空格之間移動；sub_EF0490：沒有選取時選第 0 列；sub_EF13F0：Create_Button 圖號由 PushSurf 推出），以及建角職業鈕
// 雙擊（sub_EE3410 515：不淡入、直接進能力頁）。
// 版面 XML、圖片與時鐘全部是記憶體內合成資料（顏色與大小是測試用假值，不是遊戲資料）。
// 不開遊戲、網路、視窗、伺服器、DB，不讀客戶端，不寫檔。由 --module-tests 自動發現執行。
static class CharScreenFollowup3Tests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static bool Near(int argb,uint v)=>SelectCharPanelTests.Near(argb,v);
    static readonly UiNullCommands Cmd=new();
    static void Click(IUiPanel p,Point at,int clicks=1)=>SelectCharPanelTests.Click(p,at,Cmd,clicks);
    static void DoubleClick(IUiPanel p,Point at){Click(p,at);Click(p,at,2);}
    static bool Key(IUiPanel p,Keys k)=>p.Key(SelectCharPanelTests.K(k),Cmd);
    static int Pixel(IUiPanel p,int x,int y)=>SelectCharPanelTests.Pixel(p,x,y);

    [WorldModuleTest("char-screen-followup3")]
    static void Run(WorldModuleTestContext t){
        CreateSurfFormula(t);
        EmptySlotClick(t);
        EmptySlotPainting(t);
        EmptySlotKeys(t);
        EmptySlotOpenCreate(t);
        EmptySlotSync(t);
        ClassDoubleClick(t);
    }

    // ---- select screen ----------------------------------------------------------------------------------------------------
    const uint SelBaseC=0xFF101010,SlotC=0xFF404040,RowC=0xFF0000A0,RowHiC=0xFF0000B0,RowPushC=0xFF0000C0,
        CreateC=0xFF00A000,CreateHiC=0xFF00B000,CreatePushC=0xFF00C000,LockC=0xFF606060;
    static Dictionary<int,UiSurface> SelectSurfs()=>new(){
        [900]=Surface(900,400,300,SelBaseC),[488]=Surface(488,200,100,0xFF202020),[901]=Surface(901,120,40,SlotC),[925]=Surface(925,120,40,LockC),
        [926]=Surface(926,120,40,CreateC),[927]=Surface(927,120,40,CreateHiC),[928]=Surface(928,120,40,CreatePushC),
        [904]=Surface(904,40,20,0xFFFFFF00),[907]=Surface(907,40,20,0xFFFF00FF),[915]=Surface(915,40,20,0xFF808000),
        [11]=Surface(11,20,20,0xFFFF8000),[13]=Surface(13,20,20,0xFFFF8000),   // Up/Down XML surfs (hit area only; drawn with 6411–6414)
        [6411]=Surface(6411,20,20,0xFF00FFFF),[6413]=Surface(6413,20,20,0xFF008080),
        [6430]=Surface(6430,120,40,RowC),[6431]=Surface(6431,120,40,RowHiC),[6432]=Surface(6432,120,40,RowPushC),
    };
    // Create_Button XML surfs 926 / 927 / 928 (base = push − 2, highlight = push − 1, like a kor25 triple).
    const string Slot="""
          <Control Name = "Slot_Control" Width = "120" Height = "50">
            <Image Name = "Slot_Back" Surf = "901;3">
              <Button Name = "Select_Button" BaseSurf = "6430;1" HighlightSurf = "6431;1" PushSurf = "6432;1" MouseEvent = "SelectChar_SelectWin"/>
              <Button Name = "WaitDelete_Button" BaseSurf = "6430;1" MouseEvent = "SelectChar_SelectWin"/>
              <Button Name = "Create_Button" BaseSurf = "926;1" HighlightSurf = "927;1" PushSurf = "928;1" MouseEvent = "CreateChar_SelectWin"/>
            </Image>
            <Image Name = "Slot_Lock_Back" Surf = "901;3">
              <Button Name = "Lock_Button" BaseSurf = "925;1" MouseEvent = "SlotLock_SelectWin"/>
            </Image>
          </Control>
""";
    static string SelectXml=>$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <SelectUI>
          <Window Name = "Message_Window" X = "100" Y = "100" BaseSurf = "488;3">
            <Button Name = "Ok_Button" X = "20" Y = "80" BaseSurf = "489" MouseEvent = "Ok_MessageWin"/>
          </Window>
          <Window Name = "Character_Select_Window" X = "0" Y = "0" BaseSurf = "900" Activate = "0">
            <List Name = "BigSlot_List" X = "10" Y = "10" Vertical = "1">
        {Slot}{Slot}{Slot}{Slot}
            </List>
            <List Name = "CharacterInfo_List" X = "250" Y = "200" Vertical = "1">
              <Label Name = "Name_Label" Align = "left" Width = "100" Height = "20"/>
            </List>
            <Button Name = "Up_Button" X = "150" Y = "20" BaseSurf = "11;4" MouseEvent = "UpSlot_SelectWin"/>
            <Button Name = "Down_Button" X = "150" Y = "60" BaseSurf = "13;4" MouseEvent = "DownSlot_SelectWin"/>
            <Button Name = "Enter_Button" X = "250" Y = "150" BaseSurf = "904" MouseEvent = "Ok_SelectWin"/>
            <Button Name = "Delete_Button" X = "300" Y = "150" BaseSurf = "907" MouseEvent = "DeleteChar_SelectWin"/>
            <Button Name = "Back_Button" X = "10" Y = "270" BaseSurf = "915" MouseEvent = "Cancel_SelectWin"/>
          </Window>
        </SelectUI>
        """;
    static readonly string[] Names=["甲","乙","丙","丁","戊","己","庚","辛"];
    static SelectCharPanel Select(SelectCharPanelTests.Host host,CreateCharPanel? create=null){
        var p=new SelectCharPanel();var s=SelectSurfs();
        p.Load(UiLayout.Parse(SelectXml,"SelectUI.xml"),id=>s.GetValueOrDefault(id));
        p.Create=create;p.Attach(host);
        return p;
    }
    static SelectCharPanelTests.Host HostOf(int slots,int chars)=>new(SelectCharPanelTests.ViewOf(slots,Names.Take(chars).Select(n=>SelectCharPanelTests.Char(n)).ToArray()));
    /// <summary>Slot k of the visible page (BigSlot_List at 10,10; rows 50 high).</summary>
    static Point SlotAt(int k)=>new(15,15+50*k);
    static int SlotPixel(IUiPanel p,int k)=>Pixel(p,12,12+50*k);

    static void CreateSurfFormula(WorldModuleTestContext t){
        t.Equal((926,927,928),SelectCharPanel.CreateSurfs(928,false),"empty row: normal push − 2, highlight push − 1, push (sub_EF13F0)");
        t.Equal((928,928,928),SelectCharPanel.CreateSurfs(928,true),"selected empty row: push in every state");
    }

    static void EmptySlotClick(WorldModuleTestContext t){
        var host=HostOf(8,2);var p=Select(host);
        t.Check(p.Available&&p.Rows==4,"synthetic 4-row layout usable",p.Unavailable);
        t.Check(p.Selected=="甲"&&p.SelectedEmpty==null&&p.SelectedRow==0,"first character selected by default");
        Click(p,SlotAt(2));
        t.Check(p.SelectedEmpty==2&&p.Selected==null&&p.SelectedRow==2,"click on an empty slot selects it (sub_EEC3B0 514 → sub_EF0060)",(p.SelectedEmpty,p.Selected));
        t.Check(host.Fallbacks==0&&host.Calls.Count==0,"a single click does not open the create screen");
        t.Check(p.SelectedCharacter==null,"no character selected");
        t.Check(!p.ShownPiece(p.Main!.Pieces.First(x=>x.ListName==SelectCharPanel.InfoList)),"info labels empty (sub_EECB10 sets \"\"): not drawn");
        t.Check(!p.Enabled(p.Main.Find("Enter_Button")!)&&!p.Enabled(p.Main.Find("Delete_Button")!),"Ok / Delete do nothing on an empty slot (sub_EED8A0 / sub_EEC6D0: class −1)");
        Click(p,new(260,155));
        t.Equal(0,host.Calls.Count,"Enter_Button with an empty slot selected: nothing sent");
        Click(p,SlotAt(1));
        t.Check(p.Selected=="乙"&&p.SelectedEmpty==null&&p.SelectedRow==1,"click on a character takes the selection back");
        // Page 1: slot 0 = row 4 (sub_EEC3B0: index + 4 × page).
        Click(p,new(152,62));Click(p,SlotAt(0));
        t.Check(p.Page==1&&p.SelectedEmpty==4,"page 1 slot 0 = row 4",p.SelectedEmpty);
        // Locked rows are never selected (sub_EEEE60: index < slots).
        var few=Select(HostOf(3,1));Click(few,new(152,62));
        Click(few,SlotAt(0));
        t.Check(few.Selected=="甲"&&few.SelectedEmpty==null,"click on a locked row: selection unchanged");
    }

    static void EmptySlotPainting(WorldModuleTestContext t){
        var p=Select(HostOf(8,2));
        t.Check(Near(SlotPixel(p,0),RowPushC),"selected character row: push surf",SlotPixel(p,0));
        t.Check(Near(SlotPixel(p,2),CreateC)&&Near(SlotPixel(p,3),CreateC),"unselected empty rows: push − 2 (926)");
        Click(p,SlotAt(2));
        t.Check(Near(SlotPixel(p,2),CreatePushC),"selected empty row: push 928",SlotPixel(p,2));
        t.Check(Near(SlotPixel(p,0),RowC),"the character row is no longer drawn selected",SlotPixel(p,0));
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,SlotAt(3)),Cmd);
        t.Check(Near(SlotPixel(p,3),CreateHiC),"pointer over an unselected empty row: push − 1 (927)",SlotPixel(p,3));
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,SlotAt(2)),Cmd);
        t.Check(Near(SlotPixel(p,2),CreatePushC),"pointer over the selected empty row: still 928");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Down,SlotAt(3)),Cmd);
        t.Check(Near(SlotPixel(p,3),CreatePushC),"pressed: push 928");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Up,SlotAt(3)),Cmd);
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,new(390,290)),Cmd);   // pointer off the slots (hover only follows Move)
        t.Check(p.SelectedEmpty==3&&Near(SlotPixel(p,2),CreateC),"release selects row 3; row 2 back to 926");
        // A missing derived surf is not drawn (no fallback to the XML BaseSurf).
        var s=SelectSurfs();s.Remove(927);
        var q=new SelectCharPanel();q.Load(UiLayout.Parse(SelectXml,"SelectUI.xml"),id=>s.GetValueOrDefault(id));q.Attach(HostOf(8,2));
        q.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,SlotAt(3)),Cmd);
        t.Check(Near(SlotPixel(q,3),SlotC),"927 missing → nothing drawn over Slot_Back",SlotPixel(q,3));
    }

    static void EmptySlotKeys(WorldModuleTestContext t){
        var p=Select(HostOf(8,2));
        Key(p,Keys.Down);t.Equal("乙",p.Selected,"Down: row 1 (character)");
        Key(p,Keys.Down);
        t.Check(p.SelectedEmpty==2&&p.Selected==null,"Down: row 2 = empty slot (sub_EEDF00 4136, characters and empty slots alike)");
        Key(p,Keys.Down);t.Equal((int?)3,p.SelectedEmpty,"Down: row 3");
        Key(p,Keys.Down);
        t.Check(p.SelectedEmpty==4&&p.Page==1,"row 4 → page 1 (sub_EEEE60: page = index / 4)",p.Page);
        for(int i=0;i<5;i++)Key(p,Keys.Down);
        t.Check(p.SelectedEmpty==7&&p.Page==1,"stops at the last slot (index < slots)",p.SelectedEmpty);
        Key(p,Keys.Up);Key(p,Keys.Up);Key(p,Keys.Up);Key(p,Keys.Up);
        t.Check(p.SelectedEmpty==3&&p.Page==0,"Up back to row 3 → page 0");
        Key(p,Keys.Up);Key(p,Keys.Up);
        t.Check(p.Selected=="乙"&&p.SelectedEmpty==null,"Up into the characters");
        Key(p,Keys.Up);Key(p,Keys.Up);
        t.Check(p.Selected=="甲"&&p.SelectedRow==0,"Up stops at row 0");
        var few=Select(HostOf(3,1));
        Key(few,Keys.Down);Key(few,Keys.Down);Key(few,Keys.Down);
        t.Check(few.SelectedEmpty==2,"3 slots: row 3 (locked) is never reached",few.SelectedEmpty);
        var none=Select(HostOf(4,0));
        t.Check(none.SelectedEmpty==0&&none.Selected==null,"no character: row 0 (an empty slot) selected on open (sub_EF0490)");
        Key(none,Keys.Down);t.Equal((int?)1,none.SelectedEmpty,"…and the keys move over empty slots");
    }

    static void EmptySlotOpenCreate(WorldModuleTestContext t){
        var host=HostOf(8,2);var p=Select(host);
        Click(p,SlotAt(2));Click(p,SlotAt(2),2);
        t.Check(host.Fallbacks==1&&p.SelectedEmpty==2,"double click on the empty slot opens the create screen (sub_EEC3B0 515 → sub_EEC4A0; WinForms fallback here)");
        Click(p,SlotAt(3),2);
        t.Check(host.Fallbacks==2&&p.SelectedEmpty==3,"a double click on another empty slot also selects it");
        Key(p,Keys.Enter);
        t.Equal(3,host.Fallbacks,"Enter on an empty slot opens the create screen (sub_EEDF00 13 → sub_EEC4A0)");
        t.Equal(0,host.Calls.Count,"…and never enters the world");
        Click(p,SlotAt(0));Key(p,Keys.Enter);
        t.Check(host.Calls.SequenceEqual(["enter:甲:3"])&&host.Fallbacks==3,"Enter on a character still enters",string.Join(",",host.Calls));
        // Native create screen.
        var create=CreateCharPanelTests.Create();create.Clock=()=>0;
        var h2=HostOf(8,2);var r=Select(h2,create);
        DoubleClick(r,SlotAt(3));
        t.Check(create.Visible&&!r.Visible&&h2.Fallbacks==0,"native create screen opens; the select screen hides");
        create.Close();
        t.Check(r.Visible&&r.SelectedEmpty==3,"create closed → back with the empty slot still selected");
        // CanCreate false (e.g. the movement gate is not ready): selection moves, nothing opens.
        var h3=HostOf(8,2);h3.View=h3.View with{CanCreate=false};var q=Select(h3);
        DoubleClick(q,SlotAt(2));Key(q,Keys.Enter);
        t.Check(q.SelectedEmpty==2&&h3.Fallbacks==0,"CanCreate false: the empty slot is selected, no create screen");
        // Busy (ManagingCharacters): no selection at all.
        var h4=HostOf(8,2);var b=Select(h4);h4.View=h4.View with{State=LoginState.ManagingCharacters};b.Sync();
        Click(b,SlotAt(2));
        t.Check(b.Selected=="甲"&&b.SelectedEmpty==null,"busy: Create_Button disabled like the other slot buttons");
    }

    static void EmptySlotSync(WorldModuleTestContext t){
        var host=HostOf(8,2);var p=Select(host);
        Click(p,SlotAt(3));
        host.View=host.View with{Revision=4};p.Sync();
        t.Equal((int?)3,p.SelectedEmpty,"list refresh: the empty-slot selection stays on its row");
        host.View=SelectCharPanelTests.ViewOf(8,Names.Take(4).Select(n=>SelectCharPanelTests.Char(n)).ToArray()) with{Revision=5};p.Sync();
        t.Check(p.Selected=="丁"&&p.SelectedEmpty==null,"a character now in that row takes the selection (TW16 index)",(p.Selected,p.SelectedEmpty));
        Click(p,SlotAt(0));Click(p,new(152,62));Click(p,SlotAt(3));
        t.Equal((int?)7,p.SelectedEmpty,"row 7 selected");
        host.View=SelectCharPanelTests.ViewOf(4,Names.Take(2).Select(n=>SelectCharPanelTests.Char(n)).ToArray()) with{Revision=6};p.Sync();
        t.Check(p.Selected=="甲"&&p.SelectedEmpty==null,"row became locked → first character",(p.Selected,p.SelectedEmpty));
        host.View=SelectCharPanelTests.ViewOf(4) with{Generation=2};p.Sync();
        t.Check(p.SelectedEmpty==0&&p.Selected==null&&p.Page==0,"new generation without characters → row 0");
    }

    // ---- create screen: class button double click ---------------------------------------------------------------------------
    static readonly Point Prev=new(10,285);
    static Point ClassAt(int i)=>new(30+30*i+2,252);
    static CreateCharPanel Create(){
        var p=CreateCharPanelTests.Create();p.Clock=()=>1000;
        p.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));
        return p;
    }

    static void ClassDoubleClick(WorldModuleTestContext t){
        var p=Create();
        t.Check(p.Visible&&p.Scene==CreateScene.Class&&p.Class==0,"create screen open on the class scene");
        Click(p,ClassAt(1),2);
        t.Check(p.Class==1&&p.Scene==CreateScene.Stat,"515 on the knight: class set, stat scene (sub_EE3410 515)",(p.Class,p.Scene));
        t.Check(p.Fade==0&&p.BackMode==0&&p.BackPng==1609&&!p.FrontShown&&p.Frame==0,"no fade: sub_EE5F50(class, 0) = plain knight base png",(p.Fade,p.BackPng));
        t.Check(p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(1)),"class change resets the stats to the knight base");
        Click(p,Prev);
        t.Check(p.Scene==CreateScene.Class&&p.Class==1&&p.BackPng==1609,"Prev back to the class scene keeps the knight");
        // The selected class again: no class change, still the stat scene.
        Click(p,ClassAt(1),2);
        t.Check(p.Scene==CreateScene.Stat&&p.Class==1&&p.Fade==0,"515 on the selected class: straight to the stat scene");
        // A real double click: the first release (514) changes the class with the fade; the 515 then only changes the scene.
        var q=Create();
        Click(q,ClassAt(2));
        t.Check(q.Class==2&&q.Scene==CreateScene.Class&&q.Fade==31,"first release (514): elf with the fade");
        Click(q,ClassAt(2),2);
        t.Check(q.Scene==CreateScene.Stat&&q.Class==2&&q.Fade==31,"515: same class → no sub_EE5F50, stat scene; the fade is not restarted",(q.Scene,q.Fade));
        Click(q,Prev);
        t.Check(q.Scene==CreateScene.Class&&q.Fade==0&&!q.FrontShown&&q.BackPng==1645,"Prev: class scene reset, elf base 1645 without a fade");
        // A single click still only selects (with the fade) and stays on the class scene.
        var r=Create();Click(r,ClassAt(3));
        t.Check(r.Scene==CreateScene.Class&&r.Class==3&&r.Fade==31,"single click unchanged: fade, class scene");
        // Double click mid-fade on another class: no fade, the running one is stopped.
        Click(r,ClassAt(4),2);
        t.Check(r.Scene==CreateScene.Stat&&r.Class==4&&r.Fade==0&&!r.FrontShown&&r.BackPng==1717,"515 on another class mid-fade: plain dark elf base 1717, fade stopped (推測)",(r.Fade,r.BackPng));
    }
}
