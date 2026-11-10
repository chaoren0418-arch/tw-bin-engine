namespace P06.NativeUi;

// R10-N（T398／#913）離線測試：選角上／下鈕整頁翻頁與 6411–6414 換圖（sub_EF13F0／sub_EF13A0／sub_EECF90／sub_EEDF00／
// sub_EEEE60）；建角職業頁 Back_Image 的 18 張 png 循環（sub_EE3A00）與換職業／性別時 Front_Image 的 31 步交叉淡入
// （sub_EE5F50／sub_EE6450／sub_EE3BE0，png 1573＋36×職業＋18×性別＝sub_EE3F40）。
// 版面 XML、圖片與時鐘全部是記憶體內合成資料（顏色與大小是測試用假值，不是遊戲資料）。
// 不開遊戲、網路、視窗、伺服器、DB，不讀客戶端，不寫檔。由 --module-tests 自動發現執行。
static class CharScreenFollowup2Tests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static bool Near(int argb,uint v)=>SelectCharPanelTests.Near(argb,v);
    static void Click(IUiPanel p,Point at)=>SelectCharPanelTests.Click(p,at,new UiNullCommands());
    static int Pixel(IUiPanel p,int x,int y)=>SelectCharPanelTests.Pixel(p,x,y);

    [WorldModuleTest("char-screen-followup2")]
    static void Run(WorldModuleTestContext t){
        PageFormulas(t);
        Paging(t);
        PageButtonPainting(t);
        PageKeys(t);
        PageRange(t);
        ClassPngFormula(t);
        ClassLoop(t);
        ClassFade(t);
        ClassPainting(t);
    }

    // ---- select screen: paging --------------------------------------------------------------------------------------------
    const uint SelBaseC=0xFF101010,XmlC=0xFFFF8000,UpA=0xFF00FFFF,UpB=0xFF00C0C0,DnA=0xFF008080,DnB=0xFF004040,RowC=0xFF0000A0;
    static Dictionary<int,UiSurface> SelectSurfs(bool all=true){
        var d=new Dictionary<int,UiSurface>{
            [900]=Surface(900,400,300,SelBaseC),[488]=Surface(488,200,100,0xFF202020),[901]=Surface(901,120,40,0xFF404040),[926]=Surface(926,120,40,0xFF00C000),
            [904]=Surface(904,40,20,0xFFFFFF00),[915]=Surface(915,40,20,0xFF808000),
            // XML surfs of Up/Down (hit area); TW16 replaces them with 6411–6414.
            [11]=Surface(11,20,20,XmlC),[12]=Surface(12,20,20,XmlC),[13]=Surface(13,20,20,XmlC),[14]=Surface(14,20,20,XmlC),
            [6411]=Surface(6411,20,20,UpA),[6413]=Surface(6413,20,20,DnA),[6414]=Surface(6414,20,20,DnB),
            [6430]=Surface(6430,120,40,RowC),[6431]=Surface(6431,120,40,RowC),[6432]=Surface(6432,120,40,RowC),
        };
        if(all)d[6412]=Surface(6412,20,20,UpB);
        return d;
    }
    const string Slot="""
          <Control Name = "Slot_Control" Width = "120" Height = "50">
            <Image Name = "Slot_Back" Surf = "901;3">
              <Button Name = "Select_Button" BaseSurf = "6430;1" HighlightSurf = "6431;1" PushSurf = "6432;1" MouseEvent = "SelectChar_SelectWin"/>
              <Button Name = "WaitDelete_Button" BaseSurf = "6430;1" MouseEvent = "SelectChar_SelectWin"/>
              <Button Name = "Create_Button" BaseSurf = "926;1" MouseEvent = "CreateChar_SelectWin"/>
            </Image>
            <Image Name = "Slot_Lock_Back" Surf = "901;3">
              <Button Name = "Lock_Button" BaseSurf = "926;1" MouseEvent = "SlotLock_SelectWin"/>
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
            <Button Name = "Up_Button" X = "150" Y = "20" BaseSurf = "11;4" HighlightSurf = "11;4" PushSurf = "12;4" MouseEvent = "UpSlot_SelectWin"/>
            <Button Name = "Down_Button" X = "150" Y = "60" BaseSurf = "13;4" HighlightSurf = "13;4" PushSurf = "14;4" MouseEvent = "DownSlot_SelectWin"/>
            <Button Name = "Enter_Button" X = "250" Y = "150" BaseSurf = "904" MouseEvent = "Ok_SelectWin"/>
            <Button Name = "Back_Button" X = "10" Y = "270" BaseSurf = "915" MouseEvent = "Cancel_SelectWin"/>
          </Window>
        </SelectUI>
        """;
    static readonly string[] Names=["甲","乙","丙","丁","戊","己","庚","辛","壬","癸"];
    static SelectCharPanel Select(int slots,int chars,bool all=true){
        var p=new SelectCharPanel();var s=SelectSurfs(all);
        p.Load(UiLayout.Parse(SelectXml,"SelectUI.xml"),id=>s.GetValueOrDefault(id));
        p.Attach(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(slots,Names.Take(chars).Select(n=>SelectCharPanelTests.Char(n)).ToArray())));
        return p;
    }
    static readonly Point Up=new(152,22),Down=new(152,62);

    static void PageFormulas(WorldModuleTestContext t){
        t.Equal((6411,6411,6411),SelectCharPanel.UpSurfs(0),"page 0: Up 6411 ×3 (sub_EF13F0)");
        t.Equal((6413,6414,6414),SelectCharPanel.DownSurfs(0),"page 0: Down 6413 / 6414 / 6414");
        t.Equal((6411,6412,6412),SelectCharPanel.UpSurfs(1),"page 1: Up 6411 / 6412 / 6412");
        t.Equal((6413,6413,6413),SelectCharPanel.DownSurfs(1),"page 1: Down 6413 ×3");
        t.Check(SelectCharPanel.UpSurfs(-1)==SelectCharPanel.UpSurfs(0)&&SelectCharPanel.DownSurfs(2)==SelectCharPanel.DownSurfs(1),"page ≤ 0 / > 0 split");
        t.Equal(1,SelectCharPanel.Tw16LastPage,"TW16 pages 0–1 (sub_EECF90: page < 1)");
    }

    static void Paging(WorldModuleTestContext t){
        var p=Select(8,6);
        t.Check(p.Available&&p.Rows==4,"synthetic 4-row BigSlot layout usable",p.Unavailable);
        t.Check(p.Page==0&&p.Scroll==0&&p.MaxPage==1&&p.MaxScroll==4,"8 slots, 4 rows → pages 0–1",(p.Page,p.MaxPage));
        var up=p.Main!.Find("Up_Button")!;var down=p.Main.Find("Down_Button")!;
        t.Check(!p.Enabled(up)&&p.Enabled(down),"page 0: Up does nothing, Down works");
        Click(p,Down);
        t.Check(p.Page==1&&p.Scroll==4,"Down → page 1 (whole page of 4 rows, sub_EECF90)",p.Scroll);
        t.Check(p.Row(p.Scroll+0)==SelectCharPanel.RowKind.Character&&p.Row(p.Scroll+2)==SelectCharPanel.RowKind.Empty,"page 1 shows rows 4–7 (2 characters, 2 free slots)");
        Click(p,Down);
        t.Equal(1,p.Page,"Down on the last page: no change");
        Click(p,new(15,15));
        t.Equal("戊",p.Selected,"slot 0 on page 1 = row 4 (sub_EEC3B0: index + 4 × page)");
        Click(p,Up);
        t.Check(p.Page==0&&p.Scroll==0,"Up → page 0 (sub_EF13A0)");
        Click(p,Up);
        t.Equal(0,p.Page,"Up on page 0: no change");
        t.Equal("戊",p.Selected,"paging does not change the selection");
        p.Mouse(new UiMouseEvent(UiMouseKind.Wheel,new(60,60),MouseButtons.None,0,-120,Keys.None),new UiNullCommands());
        t.Equal(1,p.Page,"wheel down → next page (推測)");
        p.Mouse(new UiMouseEvent(UiMouseKind.Wheel,new(60,60),MouseButtons.None,0,120,Keys.None),new UiNullCommands());
        t.Equal(0,p.Page,"wheel up → previous page");
    }

    static void PageButtonPainting(WorldModuleTestContext t){
        var p=Select(8,6);var c=new UiNullCommands();
        using(var b=SelectCharPanelTests.Render(p)){
            t.Check(Near(b.GetPixel(Up.X,Up.Y).ToArgb(),UpA),"page 0: Up drawn with 6411, not its XML surf",b.GetPixel(Up.X,Up.Y));
            t.Check(Near(b.GetPixel(Down.X,Down.Y).ToArgb(),DnA),"page 0: Down normal 6413");
        }
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,Down),c);
        t.Check(Near(Pixel(p,Down.X,Down.Y),DnB),"page 0: Down highlight 6414");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,Up),c);
        t.Check(Near(Pixel(p,Up.X,Up.Y),UpA),"page 0: Up highlight is 6411 too");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Down,Down),c);
        t.Check(Near(Pixel(p,Down.X,Down.Y),DnB),"page 0: Down pressed 6414");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Up,Down),c);
        t.Equal(1,p.Page,"release → page 1");
        t.Equal((6413,6413,6413),p.PageButtonSurfs(p.Main!.Find("Down_Button")!)!.Value,"page 1: Down 6413 in every state");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,Down),c);
        t.Check(Near(Pixel(p,Down.X,Down.Y),DnA),"page 1: Down stays 6413 under the pointer");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,Up),c);
        t.Check(Near(Pixel(p,Up.X,Up.Y),UpB),"page 1: Up highlight 6412");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,new(390,290)),c);
        t.Check(Near(Pixel(p,Up.X,Up.Y),UpA),"page 1: Up normal 6411");
        t.Check(p.PageButtonSurfs(p.Main.Find("Enter_Button")!)==null,"other buttons keep their XML surfs");
        // 6412 missing: not drawn, no substitute (the XML surf is not used either).
        var q=Select(8,6,all:false);Click(q,Down);
        q.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,Up),c);
        t.Check(Near(Pixel(q,Up.X,Up.Y),SelBaseC),"missing 6412 → nothing drawn (window base shows)",Pixel(q,Up.X,Up.Y));
    }

    static void PageKeys(WorldModuleTestContext t){
        var p=Select(8,6);var c=new UiNullCommands();
        for(int i=0;i<3;i++)p.Key(SelectCharPanelTests.K(Keys.Down),c);
        t.Check(p.Selected=="丁"&&p.Page==0,"Down key to index 3: still page 0");
        p.Key(SelectCharPanelTests.K(Keys.Down),c);
        t.Check(p.Selected=="戊"&&p.Page==1,"index 4 → page 1 (sub_EEEE60: page = index / 4)",p.Page);
        p.Key(SelectCharPanelTests.K(Keys.Up),c);
        t.Check(p.Selected=="丁"&&p.Page==0,"Up key back to index 3 → page 0");
        p.Key(SelectCharPanelTests.K(Keys.PageDown),c);
        t.Check(p.Page==1&&p.Selected=="丁","PageDown pages (0x1022), selection kept");
        p.Key(SelectCharPanelTests.K(Keys.PageDown),c);
        t.Equal(1,p.Page,"PageDown on the last page: no change");
        p.Key(new UiKeyEvent(Keys.PageUp,true,true),c);
        t.Equal(0,p.Page,"PageUp pages back (also on key repeat)");
    }

    static void PageRange(WorldModuleTestContext t){
        var few=Select(4,3);
        t.Equal(1,few.MaxPage,"4 slots: TW16 still has two pages");
        Click(few,Down);
        t.Check(few.Page==1&&Enumerable.Range(4,4).All(r=>few.Row(r)==SelectCharPanel.RowKind.Locked),"page 1 of a 4-slot list: every row locked (sub_EF13F0: index ≥ slots)");
        var many=Select(10,10);
        t.Equal(2,many.MaxPage,"engine only: 10 rows need a third page (3.81 has 8 slots)");
        Click(many,Down);Click(many,Down);
        t.Check(many.Page==2&&many.Scroll==8,"…reachable with Down",many.Scroll);
        t.Equal((6413,6413,6413),SelectCharPanel.DownSurfs(many.Page),"pages > 0 use the page-1 surfs");
    }

    // ---- create screen: class scene pictures ------------------------------------------------------------------------------
    static void ClassPngFormula(WorldModuleTestContext t){
        t.Check(CreateCharPanel.ClassScenePng(0,0)==1573&&CreateCharPanel.ClassScenePng(0,1)==1591&&CreateCharPanel.ClassScenePng(1,0)==1609,"1573 + 36×class + 18×sex (sub_EE3F40)");
        t.Check(CreateCharPanel.ClassScenePng(6,1)==1807&&CreateCharPanel.ClassScenePng(6,1)+CreateCharPanel.Frames-1==1824,"illusionist female: 1807–1824");
        t.Check(CreateCharPanel.ClassScenePng(7,0)==1862&&CreateCharPanel.ClassScenePng(7,1)==1880,"class 7: 1862 / 1880");
        t.Check(CreateCharPanel.Frames==18&&CreateCharPanel.FrameMs==200&&CreateCharPanel.FadeSteps==31&&CreateCharPanel.FadeStepMs==19,"18 frames > 200 ms; fade 31 steps × 19 ms");
    }

    static readonly Point Next=new(355,285),Prev=new(10,285),Male=new(102,152),Female=new(142,152);
    static Point ClassAt(int i)=>new(30+30*i+2,252);
    sealed class Clock { public long Now=1000; }
    static CreateCharPanel Create(Clock clock,Func<int,UiSurface?>? png=null){
        var p=CreateCharPanelTests.Create();p.Clock=()=>clock.Now;p.PngLookup=png;
        p.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));
        return p;
    }
    static void Tick(CreateCharPanel p,Clock c,long ms){c.Now+=ms;p.Advance(c.Now);}

    static void ClassLoop(WorldModuleTestContext t){
        var clk=new Clock();var p=Create(clk);
        t.Check(p.Visible&&p.Scene==CreateScene.Class,"create screen open on the class scene");
        var front=p.ClassScene!.Find("Front_Image");var back=p.ClassScene.Find("Back_Image");
        t.Check(front is {Image:null,Surface:null}&&back is {Image:null,Surface:null},"class scene Front_Image / Back_Image built without an id");
        t.Check(p.StatScene!.Find("Back_Image") is {Image:not null},"stat scene Back_Image keeps its Surf (not a runtime picture)");
        t.Check(p.BackPng==1573&&p.BackMode==0&&p.Frame==0&&!p.FrontShown&&p.Fade==0,"Begin: Back_Image png 1573, mode 0, frame 0, no fade (sub_EE5F50 a3 = 0)");
        t.Check(back!=null&&p.ShownPiece(back)&&front!=null&&!p.ShownPiece(front),"Back_Image shown, Front_Image hidden");
        p.Advance(clk.Now);
        t.Check(p.Frame==1&&p.BackPng==1574,"time starts at 0 → the first update changes at once (sub_EE3A00)",p.BackPng);
        Tick(p,clk,100);t.Equal(1574,p.BackPng,"100 ms: no change");
        Tick(p,clk,100);t.Equal(1574,p.BackPng,"exactly 200 ms: no change (> 200)");
        Tick(p,clk,1);t.Check(p.Frame==2&&p.BackPng==1575,"201 ms → next frame");
        for(int i=0;i<15;i++)Tick(p,clk,201);
        t.Check(p.Frame==17&&p.BackPng==1590,"frame 17 = 1590 (last of 18)",p.BackPng);
        Tick(p,clk,201);
        t.Check(p.Frame==0&&p.BackPng==1573,"after 17 → frame 0 (loop)");
        Click(p,Next);
        t.Equal(CreateScene.Stat,p.Scene,"Next → stat scene");
        Tick(p,clk,500);
        t.Equal(1573,p.BackPng,"stat scene: the loop stops (sub_EE3A00 checks scene 1)");
        Tick(p,clk,500);Click(p,Prev);
        t.Check(p.Scene==CreateScene.Class&&p.BackPng==1573&&p.Frame==0&&p.BackMode==0&&!p.FrontShown,"Prev → class scene reset to frame 0 without a fade (sub_EE6DD0 scene 1)");
    }

    static void ClassFade(WorldModuleTestContext t){
        var clk=new Clock();var p=Create(clk);
        for(int i=0;i<3;i++)Tick(p,clk,201);
        int old=p.BackPng;
        Click(p,ClassAt(1));
        t.Check(p.Class==1&&p.Fade==31&&p.BackMode==2&&p.BackLevel==31&&p.BackPng==old,"knight: fade 31, Back mode 2 level 31, Back keeps its png (sub_EE5F50 a3 = 1)",(p.Fade,p.BackPng,old));
        t.Check(p.FrontShown&&p.FrontPng==1609&&p.FrontLevel==0,"Front_Image: new base 1609, level 0, shown");
        t.Check(p.ShownPiece(p.ClassScene!.Find("Front_Image")!),"Front_Image piece shown during the fade");
        Tick(p,clk,18);
        t.Check(p.Fade==31&&p.BackLevel==31,"18 ms: the 19 ms timer has not fired");
        Tick(p,clk,1);
        t.Check(p.Fade==30&&p.BackLevel==30&&p.FrontLevel==1,"19 ms: count 30 → Back 30, Front 31 − 30 (sub_EE3BE0)");
        Tick(p,clk,300);
        t.Check(p.Fade==29&&p.BackPng==old,"one step per firing, re-armed from now; the loop is paused while Back is mode 2",(p.Fade,p.BackPng));
        Tick(p,clk,10);
        t.Equal(29,p.Fade,"next step only 19 ms after the last firing");
        int steps=0;while(p.Fade>0&&steps<100){Tick(p,clk,19);steps++;}
        t.Check(steps==29&&p.Fade==0,"31 steps in total",steps);
        t.Check(p.BackMode==0&&p.BackPng==1609&&p.Frame==0&&!p.FrontShown,"fade end: Back mode 0 with Front's png, frame 0, Front hidden");
        Tick(p,clk,1);
        t.Check(p.Frame==1&&p.BackPng==1610,"time reset to 0 → the loop moves on at the next update");
        // Sex change fades too; the same sex again does nothing.
        Click(p,Male);
        t.Equal(0,p.Fade,"same sex again → no fade (sub_EE47B0 / sub_EE3CF0 check)");
        Click(p,Female);
        t.Check(p.Sex==1&&p.Fade==31&&p.FrontPng==1627,"female: fade to 1609 + 18 = 1627 (sub_EE6450 a3 = 1)",p.FrontPng);
        Click(p,ClassAt(1));
        t.Check(p.Fade==31&&p.FrontPng==1627,"same class again → no new fade");
        Tick(p,clk,19);Tick(p,clk,19);
        int backDuring=p.BackPng;
        Click(p,ClassAt(2));
        t.Check(p.Fade==31&&p.BackLevel==31&&p.FrontLevel==0&&p.FrontPng==1663&&p.BackPng==backDuring,"class change mid-fade: restart at 31; Back keeps the old picture, Front = elf female 1663");
        p.Close();
        t.Check(p.Fade==0&&!p.FrontShown,"Close cancels the fade (sub_EE3AE0)");
        Tick(p,clk,1000);
        t.Equal(backDuring,p.BackPng,"closed: nothing advances");
        p.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));
        t.Check(p.Class==0&&p.Sex==0&&p.BackPng==1573&&p.BackMode==0&&p.Fade==0,"Begin again: prince male, frame 0");
    }

    const uint PrinceC=0xFFC00000,KnightC=0xFF0000C0;
    static void ClassPainting(WorldModuleTestContext t){
        var pngs=new Dictionary<int,UiSurface>{[1573]=Surface(1573,400,300,PrinceC),[1609]=Surface(1609,400,300,KnightC)};
        var asked=new List<int>();
        var clk=new Clock();var p=Create(clk,id=>{asked.Add(id);return pngs.GetValueOrDefault(id);});
        Color At()=>Color.FromArgb(Pixel(p,300,50));
        t.Check(SelectCharPanelTests.Near(At(),PrinceC),"Back_Image png 1573 drawn (mode 0, opaque)",At());
        Click(p,ClassAt(1));
        t.Check(SelectCharPanelTests.Near(At(),PrinceC),"fade start: Back level 31 = opaque, Front level 0 = invisible",At());
        for(int i=0;i<15;i++)Tick(p,clk,19);
        var mid=At();
        t.Check(p.BackLevel==16&&p.FrontLevel==15&&mid.R is >25 and <180&&mid.B is >50 and <180,"mid fade: both pictures blend (level ÷ 31, 推測)",mid);
        while(p.Fade>0)Tick(p,clk,19);
        t.Check(SelectCharPanelTests.Near(At(),KnightC),"fade end: knight png 1609 opaque",At());
        Tick(p,clk,1);
        var missing=At();
        t.Check(p.BackPng==1610&&!SelectCharPanelTests.Near(missing,KnightC)&&!SelectCharPanelTests.Near(missing,PrinceC),"png 1610 missing → not drawn (no substitute)",missing);
        t.Equal(1,asked.Count(id=>id==1610),"a missing png is looked up once");
        // No png source (e.g. no reference client): nothing drawn, no exception.
        var none=Create(new Clock());
        t.Check(!SelectCharPanelTests.Near(Color.FromArgb(Pixel(none,300,50)),PrinceC),"no png source → nothing drawn");
    }
}
