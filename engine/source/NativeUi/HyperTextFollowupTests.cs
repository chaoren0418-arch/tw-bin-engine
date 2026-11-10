namespace P06.NativeUi;

// R9-E（T365／#849）離線測試：HYPER_TEXT_WINDOW 的 ESC 關窗、數字框 [1, N] 逐位夾值與 Backspace、5073 上下鈕循環、
// OK／Cancel 圖鈕（1114／1111）只在有數字框時可按。依據：docs/reference/followups-9.md 第 10 節、第 16.2 節 R9-E。
// 只用合成 XML／合成圖／固定字寬；不開遊戲、網路、視窗、伺服器、DB，也不讀客戶端素材。由 --module-tests 執行（名稱 hypertext-followup）。
static class HyperTextFollowupTests {
    [WorldModuleTest("hypertext-followup")]
    static void Run(WorldModuleTestContext t){
        Escape(t);Clamp(t);SpinKeys(t);SpinButtons(t);ImageButtons(t);FallbackRow(t);
    }

    sealed class Host:IHyperTextHost {
        public List<string> Calls{get;}=[];
        public void Link(HyperTextView shown,int option)=>Calls.Add($"link:{option}");
        public void Submit(HyperTextView shown,long value)=>Calls.Add($"submit:{shown.Number?.Kind}:{value}");
        public void CancelNumber(HyperTextView shown)=>Calls.Add($"cancel:{shown.Number?.Revision}");
        public void Close(HyperTextView shown)=>Calls.Add($"close:{shown.Source}:{shown.Revision}");
        public void CancelName(HyperTextView shown)=>Calls.Add("name-cancel");
    }

    // 版面數值同本機 uiextra86 的 HYPER_TEXT_WINDOW（不是圖）；窗原點 (100,50)。
    const string Xml="""
<?xml version="1.0"?>
<root>
<HYPER_TEXT_WINDOW x = "100" y = "50">
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_HYPER_TEXT_BASE" surfNumber = "1968" xPos = "6" yPos = "9"/>
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_HYPER_TEXT_FRAME" surfNumber = "1969"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_QUIT_X" surfNumber = "1962" xPos = "225" yPos = "15"/>
	<IMAGE surfName = "SURF_RENEWAL_WIN_SCROLLBAR_LONG" surfNumber = "1970" xPos = "217" yPos = "51"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber = "2040" xPos = "217" yPos = "275"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber = "2042" xPos = "217" yPos = "37"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber = "2044"/>
</HYPER_TEXT_WINDOW>
</root>
""";
    const uint SpinTop=0xFFFF0000,SpinRest=0xFF0000FF,Ok=0xFF00FF00,OkDown=0xFF008000,Cancel=0xFFFFFF00,CancelDown=0xFF808000;
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    /// <summary>5073 合成圖：13×11（同本機 5073.img 的尺寸），第一列紅、其他藍，用來看上鈕是否翻轉。</summary>
    static UiSurface Spin()=>new(HyperTextPanel.SpinSurf,"5073.img","synthetic",new UiImage(13,11,0,0,Enumerable.Range(0,13*11).Select(i=>i<13?SpinTop:SpinRest).ToArray()));
    static Func<int,UiSurface?> Surfaces(bool spin,bool buttons)=>id=>id switch{
        1968=>Surf(id,220,270,0xFF203040),1969=>Surf(id,240,4,0xFF999999),1962=>Surf(id,12,12,0xFFFF00FF),
        1970=>Surf(id,8,220,0xFF111111),2040 or 2042=>Surf(id,8,12,0xFF222222),2044=>Surf(id,8,8,0xFF333333),
        HyperTextPanel.SpinSurf when spin=>Spin(),
        // 1114／1111 本機 46×15、1115／1112 本機 47×16（Sprite.idx 的 c 版 .img 檔頭）。
        HyperTextPanel.OkSurf when buttons=>Surf(id,46,15,Ok),HyperTextPanel.OkDownSurf when buttons=>Surf(id,47,16,OkDown),
        HyperTextPanel.CancelSurf when buttons=>Surf(id,46,15,Cancel),HyperTextPanel.CancelDownSurf when buttons=>Surf(id,47,16,CancelDown),
        _=>null};
    static (HyperTextPanel p,Host h) New(bool spin=true,bool buttons=true){
        var p=new HyperTextPanel(s=>s.Length*12);p.Load(UiLayout.Parse(Xml,"synthetic-uiextra86.xml"),Surfaces(spin,buttons));var h=new Host();p.Attach(h);return (p,h);
    }
    static UiMouseEvent M(UiMouseKind kind,Point at)=>new(kind,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,Rectangle r,IUiCommands cmd){var at=Mid(r);p.Mouse(M(UiMouseKind.Down,at),cmd);p.Mouse(M(UiMouseKind.Up,at),cmd);}
    static Point Mid(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static UiKeyEvent K(Keys k,bool down=true,bool repeat=false)=>new(k,down,repeat);
    static HyperTextNumberBox Amount(long rev,long max,long initial=1)=>new(HyperTextNumberKind.Amount,rev,1,max,initial,true,true,$"1～{max}");
    static HyperTextNumberBox Castle(long rev,long min,long max)=>new(HyperTextNumberKind.Castle,rev,min,max,min,true,true,$"{min}～{max}");
    static HyperTextView Page(int rev,HyperTextNumberBox? number=null,HyperTextNameBox? name=null)=>new(HyperTextSource.Npc,rev,"頁面",[new HyperTextLink(1,"選項",true)],"",number,name);

    static void Escape(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var (p,h)=New();
        t.Check(!p.Key(K(Keys.Escape),cmd),"no page: ESC not taken (global ESC handles it)");
        p.Show(Page(1));
        t.Check(!p.Key(K(Keys.Escape,down:false),cmd)&&p.Visible,"ESC key-up not taken");
        t.Check(p.Key(K(Keys.Escape,repeat:true),cmd)&&p.Visible,"ESC auto-repeat consumed, window stays");
        t.Check(p.Key(K(Keys.Escape),cmd)&&!p.Visible&&h.Calls.LastOrDefault()=="close:Npc:1","ESC (27) closes the window (sub_875F80) and asks the host to close the page");
        p.Show(Page(1));t.Check(!p.Visible,"ESC-dismissed page is not reopened while the host still has it");
        p.Show(Page(2,Amount(1,10)));
        t.Check(p.InputFocused&&p.Key(K(Keys.Escape),cmd)&&!p.Visible&&h.Calls.LastOrDefault()=="close:Npc:2","ESC with the number box focused also closes the window");
        p.Show(Page(3,null,new HyperTextNameBox(5,"名稱",true)));
        t.Check(p.Key(K(Keys.Escape),cmd)&&p.Visible&&h.Calls.LastOrDefault()=="name-cancel","name box focused: ESC stays CancelName (R7-D)");
        t.Check(p.Key(K(Keys.Escape),cmd)&&!p.Visible&&h.Calls.LastOrDefault()=="close:Npc:3","after the name box let go, the next ESC closes the window");
    }

    static void Clamp(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var (p,h)=New();
        p.Show(Page(1,Amount(1,50)));
        t.Equal("1",p.Input,"initial value 1");
        t.Check(p.Char('7')&&p.Input=="17","digit appended: 1 → 17");
        t.Check(p.Char('0')&&p.Input=="50","170 clamped to max 50");
        t.Check(p.Key(K(Keys.Back),cmd)&&p.Input=="5","Backspace drops the last digit");
        t.Check(p.Key(K(Keys.Back),cmd)&&p.Input==""&&!p.CanSubmit,"Backspace to empty; empty cannot be submitted");
        t.Check(p.Key(K(Keys.Back),cmd)&&p.Input=="","Backspace on empty stays empty");
        t.Check(p.Char('0')&&p.Input=="1","0 alone clamped to min 1");
        t.Check(p.Char('0')&&p.Input=="10","then 10");
        t.Check(!p.Char('-')&&!p.Char('a')&&p.Input=="10","non-digits not taken");
        p.Show(Page(2,Amount(2,long.MaxValue)));
        foreach(char c in "99999999999999999999999")p.Char(c);
        t.Equal(long.MaxValue.ToString(),p.Input,"long.MaxValue limit: no overflow, clamped to N");
        p.Show(Page(3,Castle(3,10,50)));
        p.Key(K(Keys.Back),cmd);p.Key(K(Keys.Back),cmd);
        t.Check(p.Char('3')&&p.Input=="3"&&!p.CanSubmit,"typing only clamps to [1, N]; 3 below the castle minimum 10 cannot be submitted");
        t.Check(p.Char('0')&&p.Input=="30"&&p.CanSubmit,"30 within 10..50");
        int before=h.Calls.Count;p.Key(K(Keys.Back),cmd);Click(p,p.SubmitRect,cmd);
        t.Equal(before,h.Calls.Count,"OK with a value below Min sends nothing");
        p.Show(Page(4,Amount(4,9,1) with{Answerable=false}));
        t.Check(!p.Char('5')&&p.Input=="","unanswerable prompt takes no digits");
    }

    static void SpinKeys(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var (p,_)=New(spin:false,buttons:false);
        p.Show(Page(1,Amount(1,5,5)));
        t.Check(p.SpinUpRect.IsEmpty&&p.SpinDownRect.IsEmpty&&!p.SpinDrawn,"5073 missing: no spin buttons drawn or hit");
        t.Check(p.Key(K(Keys.Up),cmd)&&p.Input=="1","↑ at max 5 wraps to min 1 (4203, 推測方向鍵)");
        t.Check(p.Key(K(Keys.Down),cmd)&&p.Input=="5","↓ at min 1 wraps to max 5");
        t.Check(p.Key(K(Keys.Down,repeat:true),cmd)&&p.Input=="4","↓ auto-repeat steps −1");
        p.Step(1);t.Equal("5",p.Input,"Step(+1)");
        p.Key(K(Keys.Back),cmd);p.Step(1);t.Equal("1",p.Input,"empty + up starts at the minimum");
        p.Key(K(Keys.Back),cmd);p.Step(-1);t.Equal("5",p.Input,"empty + down starts at the maximum");
        p.Show(Page(2,Castle(2,10,50)));
        p.Step(-1);t.Equal("50",p.Input,"castle: below Min 10 wraps to Max 50");
        p.Step(1);t.Equal("10",p.Input,"castle: above Max 50 wraps to Min 10");
        p.Key(K(Keys.Back),cmd);p.Key(K(Keys.Back),cmd);p.Char('3');p.Step(1);
        t.Equal("10",p.Input,"typed 3 (< Min) + up → Min");
        p.Show(Page(3,Amount(3,5) with{Answerable=false}));
        p.Step(1);t.Check(p.Input==""&&!p.Key(K(Keys.Up),cmd),"unanswerable: spin does nothing, ↑ not taken");
        p.Show(Page(4));
        t.Check(!p.Key(K(Keys.Up),cmd)&&!p.Key(K(Keys.Down),cmd),"page without a number box: arrows not taken");
    }

    static void SpinButtons(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var (p,_)=New();
        p.Show(Page(1,Amount(1,3,3)));
        var box=p.InputRect;var up=p.SpinUpRect;var down=p.SpinDownRect;
        t.Check(p.SpinDrawn&&up.Size==new Size(13,11)&&down.Size==new Size(13,11),"5073 up/down buttons 13×11",new{up,down});
        t.Check(up.X==box.Right+HyperTextPanel.SpinGap&&down.X==up.Right&&up.Y==down.Y&&up.Y>=box.Y&&up.Bottom<=box.Y+HyperTextPanel.LineHeight,"spin buttons right of the box, inside the number row",new{box,up,down});
        Click(p,up,cmd);t.Check(p.Input=="1"&&p.InputFocused,"click up at max → wraps to min (release +1), box keeps focus");
        Click(p,up,cmd);t.Equal("2",p.Input,"click up +1");
        Click(p,down,cmd);Click(p,down,cmd);t.Equal("3",p.Input,"click down 2→1→3 (wraps)");
        p.Mouse(M(UiMouseKind.Down,Mid(up)),cmd);p.Mouse(M(UiMouseKind.Up,Mid(box)),cmd);
        t.Equal("3",p.Input,"press up, release elsewhere: no step (release decides)");
        using var canvas=new UiCanvas();
        canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
        t.Equal(unchecked((int)SpinTop),canvas.Bitmap.GetPixel(down.X+6,down.Y).ToArgb(),"down button = 5073 as stored (top row on top)");
        t.Equal(unchecked((int)SpinTop),canvas.Bitmap.GetPixel(up.X+6,up.Bottom-1).ToArgb(),"up button = 5073 flipped vertically (推測)");
        t.Equal(unchecked((int)SpinRest),canvas.Bitmap.GetPixel(up.X+6,up.Y).ToArgb(),"up button top row is the flipped body");
        p.Show(Page(2));
        t.Check(p.SpinUpRect.IsEmpty&&p.SpinDownRect.IsEmpty,"no number box → no spin buttons");
    }

    static void ImageButtons(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var (p,h)=New();
        t.Check(p.OkButton is {Surf:1114,Drawn:true}&&p.CancelButton is {Surf:1111,Drawn:true},"OK 1114 / Cancel 1111 loaded");
        t.Check(p.OkButton!.Offset==new Point(47,292)&&p.CancelButton!.Offset==new Point(133,292),"OK (47,292), Cancel (133,292) (ly HyperTextUI.xml, 推測)");
        p.Show(Page(1));
        t.Check(!p.HasNumber&&p.SubmitRect.IsEmpty&&p.CancelRect.IsEmpty,"page without a number box: OK/Cancel not clickable (sub_875B20)");
        t.Equal(new Rectangle(100,50,240,287),p.Bounds,"bounds unchanged without a number box");
        int before=h.Calls.Count;Click(p,new Rectangle(100+47,50+292,46,15),cmd);
        t.Equal(before,h.Calls.Count,"click where OK would be does nothing");
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);p.Paint(canvas);
            t.Equal(Color.Black.ToArgb(),canvas.Bitmap.GetPixel(100+60,50+299).ToArgb(),"OK not drawn without a number box");
        }
        p.Show(Page(2,Amount(2,20,4)));
        t.Equal(new Rectangle(147,342,46,15),p.SubmitRect,"OK image button at origin + (47,292)");
        t.Equal(new Rectangle(233,342,46,15),p.CancelRect,"Cancel image button at origin + (133,292)");
        t.Equal(new Rectangle(100,50,240,307),p.Bounds,"bounds grow to include the buttons");
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            t.Equal(unchecked((int)Ok),canvas.Bitmap.GetPixel(147+5,342+5).ToArgb(),"OK 1114 drawn");
            t.Equal(unchecked((int)Cancel),canvas.Bitmap.GetPixel(233+5,342+5).ToArgb(),"Cancel 1111 drawn");
            var row=p.LineRect(p.Rows.ToList().FindIndex(r=>r.Kind==HyperTextPanel.RowKind.Number));
            t.Check(p.InputRect.Right<=row.Right,"number row has only the box and spin buttons");
        }
        p.Mouse(M(UiMouseKind.Down,Mid(p.SubmitRect)),cmd);
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            t.Equal(unchecked((int)OkDown),canvas.Bitmap.GetPixel(147+5,342+5).ToArgb(),"OK held → 1115");
        }
        p.Mouse(M(UiMouseKind.Up,Mid(p.SubmitRect)),cmd);
        t.Equal("submit:Amount:4",h.Calls.LastOrDefault(),"OK image button submits");
        Click(p,p.CancelRect,cmd);
        t.Check(!p.Visible&&h.Calls.LastOrDefault()=="close:Npc:2","S136 Cancel image button closes the window (推測)");
        p.Show(Page(3,Castle(9,10,50)));
        Click(p,p.CancelRect,cmd);
        t.Check(p.Visible&&h.Calls.LastOrDefault()=="cancel:9","castle Cancel → host.CancelNumber");
        p.Show(Page(3,Castle(9,10,50) with{CanSend=false}));
        before=h.Calls.Count;Click(p,p.SubmitRect,cmd);
        t.Check(!p.SubmitRect.IsEmpty&&h.Calls.Count==before,"OK clickable area present but host says cannot send → nothing sent");
        p.Show(Page(4));
        t.Check(p.SubmitRect.IsEmpty&&p.Bounds==new Rectangle(100,50,240,287),"number box gone → buttons gone");
    }

    static void FallbackRow(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var (p,h)=New(spin:true,buttons:false);
        p.Show(Page(1,Castle(1,10,50)));
        var box=p.InputRect;var down=p.SpinDownRect;var ok=p.SubmitRect;var cancel=p.CancelRect;
        t.Check(p.OkButton is {Drawn:false}&&!ok.IsEmpty&&ok.Y==box.Y,"1114 missing: OK falls back to the text button in the number row");
        t.Check(ok.X>down.Right&&cancel.X>ok.Right&&cancel.Right<=p.LineAt(0).X+HyperTextPanel.TextWidth,"text buttons after the spin buttons, within the text width",new{box,down,ok,cancel});
        Click(p,ok,cmd);t.Equal("submit:Castle:10",h.Calls.LastOrDefault(),"fallback OK submits");
        Click(p,cancel,cmd);t.Equal("cancel:1",h.Calls.LastOrDefault(),"fallback Cancel → CancelNumber");
        p.Show(Page(2,Amount(2,9)));
        t.Check(p.CancelRect.IsEmpty,"fallback: S136 box has OK only (unchanged)");
        var (q,_)=New(spin:false,buttons:false);
        q.Show(Page(1,Amount(1,9)));
        t.Equal(q.InputRect.Right+HyperTextPanel.ButtonGap,q.SubmitRect.X,"no 5073: text OK right after the box");
    }
}
