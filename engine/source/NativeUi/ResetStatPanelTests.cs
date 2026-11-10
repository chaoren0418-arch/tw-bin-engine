namespace P06.NativeUi;

// T325 (#763) reset-stat-ui / stat-info-ui 離線測試（能力值重置窗與能力資訊窗）：版面 XML 與圖片全部是記憶體內合成資料，
// 元素種類仿 ResetStatUI.xml / StatInfoUI.xml，不是真實檔案；不讀真實客戶端或參考客戶端、不啟動遊戲、不連網路。
// CI 由 --module-tests 自動發現執行。
static class ResetStatPanelTests {
    const uint BackC=0xFF101010,MsgBaseC=0xFF202020,MsgBackC=0xFF303030,OkC=0xFF0000FF,OkHiC=0xFF0000A0,OkPushC=0xFF000080,CancelC=0xFF00FF00,
        UpC=0xFF00FFFF,UpPushC=0xFF008080,DownC=0xFFFF00FF,DownPushC=0xFF800080,L1C=0xFFFFFF00,L10C=0xFF808000,MainOkC=0xFFFF8000,MainOkHiC=0xFFFFA000,
        TitleC=0xFFFF0000,StatBackC=0xFF404040,ProgOnC=0xFF00C000,ProgOffC=0xFF006000,ArrowOnC=0xFFC0C0FF,ArrowOffC=0xFF6060A0,InfoBaseC=0xFF252525;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Dictionary<int,UiSurface> Surfs()=>new(){
        [488]=Surface(488,200,100,MsgBaseC),[487]=Surface(487,200,100,MsgBackC),[489]=Surface(489,30,10,OkC),[490]=Surface(490,30,10,OkHiC),[491]=Surface(491,30,10,OkPushC),
        [492]=Surface(492,30,10,CancelC),[504]=Surface(504,8,8,DownC),[505]=Surface(505,8,8,DownPushC),[506]=Surface(506,8,8,UpC),[507]=Surface(507,8,8,UpPushC),
        [670]=Surface(670,50,20,TitleC),[681]=Surface(681,40,12,L1C),[684]=Surface(684,40,12,L10C),[687]=Surface(687,40,12,MainOkC),[688]=Surface(688,40,12,MainOkHiC),
        [673]=Surface(673,10,10,ProgOffC),[674]=Surface(674,10,10,ProgOnC),[675]=Surface(675,10,10,ProgOffC),[676]=Surface(676,10,10,ProgOnC),
        [677]=Surface(677,10,10,ProgOffC),[678]=Surface(678,10,10,ProgOnC),[679]=Surface(679,6,6,ArrowOffC),[680]=Surface(680,6,6,ArrowOnC),[156]=Surface(156,400,300,BackC),
    };
    static Dictionary<int,UiSurface> Pngs()=>new(){[933]=Surface(933,150,80,StatBackC),[911]=Surface(911,120,100,InfoBaseC)};

    // Synthetic layout with the element kinds of ResetStatUI.xml: the modal message window (BaseSurf with flag, MultiLabel,
    // Ok/Cancel, Back_Image last), the main window with screen/multi titles, value labels, a Stat_Change Control holding six
    // stat Controls (Point + Down/Up), a png back, a Customizing_Win, Level/OK buttons, progress On/Off images, Msg/Tip
    // labels, a missing surf, a BoardImage and the background last.
    static string Stat(string name,int x,int y)=>$"""
            <Control Name = "{name}" X = "0">
              <Label Name = "Point" X = "{x}" Y = "{y}" Align = "center" Caption = "0" Width = "22" Height = "20"/>
              <Button Name = "Down_Button" X = "{x+22}" Y = "{y+2}" BaseSurf = "504" HighlightSurf = "504" PushSurf = "505" MouseEvent = "Down_ResetStatWin"/>
              <Button Name = "Up_Button" X = "{x+34}" Y = "{y+2}" BaseSurf = "506" HighlightSurf = "506" PushSurf = "507" MouseEvent = "Up_ResetStatWin"/>
            </Control>
    """;
    static string Xml(string mainSize="Width = \"400\" Height = \"300\"",string msgBase="BaseSurf = \"488;3\"",string extra="")=>$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <ResetStatUI>
          <Window Name = "Message_Window" X = "100" Y = "100" Modal = "1" Top = "1" DarkBack = "1" Activate = "0" {msgBase}>
            <MultiLabel Name = "Message_Label" X = "10" Y = "10" Width = "180" Height = "50" VAlign = "center" LineMargin = "3"/>
            <Button Name = "Ok_Button" X = "20" Y = "80" BaseSurf = "489" HighlightSurf = "490" PushSurf = "491" MouseEvent = "Ok_MessageWin"/>
            <Button Name = "Cancel_Button" X = "150" Y = "80" BaseSurf = "492" MouseEvent = "Cancel_MessageWin"/>
            <Image Name = "Back_Image" Surf = "487;1"/>
          </Window>
          <Window Name = "ResetStat_Win" X = "0" Y = "0" {mainSize} Activate = "0">
            <Image Name = "Title_Screen" X = "5" Y = "5" Surf = "670;4"/>
            <Image Name = "Title_Multi" X = "5" Y = "5" Surf = "671;3"/>
            <Label Name = "Level" X = "60" Y = "40" Align = "left" Caption = "0" Width = "22" Height = "20"/>
            <Label Name = "HitPoint" X = "60" Y = "60" Align = "left" Caption = "0" Width = "22" Height = "20"/>
            <Control Name = "Stat_Change_Control" X = "-100" Y = "-10">
        {Stat("Strength",300,130)}
        {Stat("Dexterity",300,150)}
        {Stat("Constitution",300,170)}
        {Stat("Intelligence",360,130)}
        {Stat("Wisdom",360,150)}
        {Stat("Charisma",360,170)}
              <Label Name = "Remain_Point" X = "420" Y = "150" Align = "center" Caption = "0" Width = "32" Height = "22"/>
              <Image Name = "Back_Image" X = "290" Y = "120" Png = "933;1"/>
            </Control>
            <Control Name = "Customizing_Win">
              <Button Name = "Exit_Button" X = "300" Y = "10" BaseSurf = "502" MouseEvent = "Exit_CustomizingWin"/>
            </Control>
            <Button Name = "10Level_Button" X = "10" Y = "200" BaseSurf = "684" HighlightSurf = "685" PushSurf = "686;1" MouseEvent = "Level10_ResetStatWin"/>
            <Button Name = "1Level_Button" X = "60" Y = "200" BaseSurf = "681" PushSurf = "683;1" MouseEvent = "Level1_ResetStatWin"/>
            <Button Name = "OK_Button" X = "110" Y = "200" BaseSurf = "687" HighlightSurf = "688" PushSurf = "689;1" MouseEvent = "OK_ResetStatWin"/>
            <Image Name = "Progress_Reset_On" X = "20" Y = "260" Surf = "674"/>
            <Image Name = "Progress_Reset_Off" X = "20" Y = "260" Surf = "673"/>
            <Image Name = "Progress_LevelUp_On" X = "60" Y = "260" Surf = "676"/>
            <Image Name = "Progress_LevelUp_Off" X = "60" Y = "260" Surf = "675"/>
            <Image Name = "Progress_Done_On" X = "100" Y = "260" Surf = "678"/>
            <Image Name = "Progress_Done_Off" X = "100" Y = "260" Surf = "677"/>
            <Image Name = "Progress_Arrow1_On" X = "40" Y = "262" Surf = "680;4"/>
            <Image Name = "Progress_Arrow1_Off" X = "40" Y = "262" Surf = "679;4"/>
            <Image Name = "Progress_Arrow2_On" X = "80" Y = "262" Surf = "680;4"/>
            <Image Name = "Progress_Arrow2_Off" X = "80" Y = "262" Surf = "679;4"/>
            <MultiLabel Name = "Msg_Label" X = "200" Y = "220" Width = "150" Height = "40" VAlign = "center" LineMargin = "3"/>
            <Label Name = "Tip_Label" X = "100" Y = "280" Align = "center" String = "4082" Width = "200" Height = "20"/>
            <Image Name = "Msg_Back_screen" X = "198" Y = "218" Surf = "734;4"/>
            <Image Name = "Msg_Back_multi" X = "198" Y = "218" Surf = "735;3"/>
            {extra}
            <BoardImage Name = "BG_Image" X = "0" Y = "0" Width = "400" Height = "300" CustomColor= "0,0,0" CustomAlpha = "16"/>
            <Image Name = "Back_Image" X = "0" Y = "0" Surf = "156"/>
          </Window>
        </ResetStatUI>
        """;
    static UiLayout Layout(string xml)=>UiLayout.Parse(xml,"ResetStatUI.xml");
    static ResetStatPanel Panel(string? xml=null){
        var p=new ResetStatPanel();var s=Surfs();var g=Pngs();
        p.Load(Layout(xml??Xml()),id=>s.GetValueOrDefault(id),id=>g.GetValueOrDefault(id));
        return p;
    }

    sealed class Host:IResetStatHost {
        public CharResetView View;
        public readonly List<(string Command,int Step,ResetStats Stats,int Revision)> Sent=[];
        public bool Accept=true;public int Cancels;
        public Func<CharResetView,string,ResetStats,int,CharResetView>? After;
        public Host(CharResetView v){View=v;}
        public CharResetView Snapshot()=>View;
        public Task<bool> Send(CharResetView stamp,string command,ResetStats stats,int step){
            Sent.Add((command,step,stats,stamp.Revision));
            if(Accept&&After!=null)View=After(View,command,stats,step);
            return Task.FromResult(Accept);
        }
        public void Cancel()=>Cancels++;
    }
    // Initial offer: base 10/10/10/10/10/10 (sum 60) → 15 points to place; CanAct.
    static CharResetView Initial(int rev=1)=>new(3,7,rev,99,0,"initial",1,60,14,2,10,new(10,10,10,10,10,10),15,null,true,"分配出生能力後確認；原服收到後即開始重置。");
    static CharResetView Level(int level,int max=60,int rev=5,ResetStats? stats=null)=>new(3,7,rev,99,0,"level",level,max,30,8,9,stats??new(18,12,12,11,12,10),Math.Max(0,max-Math.Max(50,level)),null,true,"逐步恢復等級；51 級起每級選擇一項能力。");

    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static Bitmap Render(IUiPanel p){var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();var b=(Bitmap)canvas.Bitmap.Clone();canvas.Dispose();return b;}
    static int Pixel(IUiPanel p,int x,int y){using var b=Render(p);return b.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();
    static ResetPiece Named(ResetStatPanel p,string name,string? owner=null)=>p.Main!.Pieces.First(x=>x.Name==name&&(owner==null||x.Owner==owner));
    // Stat_Change_Control is at (-100,-10): stat "Strength" Up button at (300+34-100, 130+2-10) = (234,122).

    [WorldModuleTest("reset-stat-ui")]
    static void RunReset(WorldModuleTestContext t){
        Registration(t);
        Parsing(t);
        Painting(t);
        Editing(t);
        Leveling(t);
        Leaving(t);
        Broken(t);
    }

    static void Registration(WorldModuleTestContext t){
        foreach(var (id,type) in new[]{(ResetStatPanel.PanelId,typeof(ResetStatPanel)),(StatInfoPanel.PanelId,typeof(StatInfoPanel))}){
            var regs=UiPanelRegistry.Panels.Where(r=>r.Id==id).ToArray();
            t.Check(regs.Length==1&&regs[0].Layout=="uistatus86",$"{id} registered once, on the host layout (Build never needs the reference XML)",string.Join(",",regs.Select(r=>r.Layout)));
            t.Check(!UiPanelRegistry.Errors.Any(e=>e.Contains(type.Name)),$"no discovery error for {type.Name}");
            var panel=regs[0].Create(new UiRoot());
            t.Check(panel.GetType()==type&&panel is IUiClosablePanel&&!panel.Visible,$"{id}: closable, closed until opened");
        }
        var reset=(ResetStatPanel)UiPanelRegistry.Panels.Single(r=>r.Id==ResetStatPanel.PanelId).Create(new UiRoot());
        t.Check(reset.Layer==UiLayer.Float&&!reset.Available,"reset window: float layer (modal, full screen), unavailable until loaded");
        t.Check(!reset.Begin(new Host(Initial()))&&!reset.Visible,"unavailable → Begin refuses (WinForms dialog keeps the offer)");
        var info=UiPanelRegistry.Panels.Single(r=>r.Id==StatInfoPanel.PanelId).Create(new UiRoot());
        t.Check(info.Layer==UiLayer.Window&&info is IUiMovablePanel,"stat info: window layer, movable");
        var empty=new ResetStatPanel();empty.Load(Layout(Xml()),null!);
        t.Check(!empty.Available&&empty.Unavailable=="沒有 UiResources","product Load without UiResources → unavailable");
    }

    static void Parsing(WorldModuleTestContext t){
        var p=Panel();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Equal("ResetStatUI.xml",p.LayoutFile,"layout file recorded");
        t.Equal(new Rectangle(0,0,400,300),p.Main!.Bounds,"main window: X/Y + declared Width/Height");
        t.Equal(new Rectangle(100,100,200,100),p.Message!.Bounds,"message window: X/Y + BaseSurf size (flag ;3 ignored)");
        t.Check(p.Main.Pieces.All(x=>x.Name!="Title_Multi"&&x.Name!="Msg_Back_multi"),"multi variants not built");
        t.Check(p.Skipped.Count(s=>s.Contains("multi"))==2,"…and listed as skipped",string.Join(" | ",p.Skipped));
        t.Check(p.Skipped.Any(s=>s.Contains("Customizing_Win")),"Customizing_Win skipped (no values in the engine)");
        t.Check(!p.Missing.Any(m=>m.Contains("502")),"Customizing_Win images never resolved");
        t.Check(p.Missing.Contains("surf 734 (ResetStatUI/Window[1]/Image[12])"),"missing surf listed with its path",string.Join(" | ",p.Missing));
        t.Check(p.Missing.Any(m=>m.StartsWith("surf 685 "))&&p.Missing.Any(m=>m.StartsWith("surf 686 ")),"missing highlight/push surfs listed");
        t.Check(!p.Missing.Any(m=>m.StartsWith("surf 671 ")),"multi image not resolved");
        var up=Named(p,"Up_Button","Strength");
        t.Equal(new Point(234,122),up.Offset,"stat button: Window → Stat_Change_Control(-100,-10) → Strength(0) → Up(334,132)");
        t.Check(up.Highlight==up.Surface&&up.Push!=null,"HighlightSurf = BaseSurf reuses the same image");
        t.Equal(new Point(200,120),Named(p,"Point","Strength").Offset,"Point label offset");
        t.Equal(6,p.Main.Pieces.Count(x=>x.Name=="Point"),"six stat Point labels");
        t.Check(ResetStatPanel.StatControls.All(k=>p.Main.Pieces.Count(x=>x.Owner==k.Key)==3),"each stat Control owns Point + Down + Up");
        t.Equal(ResetPieceKind.Board,Named(p,"BG_Image").Kind,"BoardImage built");
        t.Check(Named(p,"Tip_Label") is {TextKind:"String",TextValue:"4082"},"Tip label keeps its String id");
        t.Check(ResetStatPanel.ImageRef(Layout("<A S=\"10911;2;28\"/>").Root,"S") is {Id:10911,Flag:2},"image ref with two flags (StatInfoUI BasePng)");
        t.Throws<UiLayoutException>(()=>ResetStatPanel.ImageRef(Layout("<A S=\"x;1\"/>").Root,"S"),"non-numeric image id → UiLayoutException");
    }

    static void Painting(WorldModuleTestContext t){
        var p=Panel();var host=new Host(Initial());
        t.Check(p.Begin(host)&&p.Visible,"Begin opens on the initial offer");
        t.Equal(new Rectangle(0,0,400,300),p.Bounds,"bounds = main window while open");
        using(var b=Render(p)){
            var back=b.GetPixel(390,5);var raw=Color.FromArgb(unchecked((int)BackC));
            t.Check(back.R<=raw.R&&back.R>=raw.R-2,"background surf under a 16-alpha black board",back);
            t.Check(Near(b.GetPixel(5+2,5+2),TitleC),"Title_Screen drawn above the background");
            t.Check(Near(b.GetPixel(330,185),StatBackC),"Stat_Change png back drawn above the board",b.GetPixel(330,185));
            t.Check(Near(b.GetPixel(234+2,122+2),UpC),"Up button over the stat back");
            t.Check(Near(b.GetPixel(20+2,260+2),ProgOnC)&&Near(b.GetPixel(60+2,260+2),ProgOffC)&&Near(b.GetPixel(100+2,260+2),ProgOffC),"initial: Reset On, LevelUp/Done Off");
            t.Check(Near(b.GetPixel(40+2,262+2),ArrowOffC)&&Near(b.GetPixel(80+2,262+2),ArrowOffC),"arrows Off");
            bool text=false;for(int x=200;x<350;x++)for(int y=220;y<260;y++)if(!Near(b.GetPixel(x,y),BackC))text=true;
            t.Check(text,"Msg_Label shows the phase notice");
            bool tip=false;for(int x=100;x<300;x++)for(int y=280;y<300;y++)if(!Near(b.GetPixel(x,y),BackC))tip=true;
            t.Check(!tip,"Tip_Label: String id without TextResolver → not drawn");
        }
        t.Equal("1",p.LabelText(Named(p,"Level")),"Level label = view level");
        t.Equal("14",p.LabelText(Named(p,"HitPoint")),"HitPoint label = view HP");
        t.Equal("15",p.LabelText(Named(p,"Remain_Point")),"Remain_Point = 75 − base sum");
        t.Equal("10",p.LabelText(Named(p,"Point","Wisdom")),"stat Point = draft value");
        p.TextResolver=(k,v)=>k=="String"&&v=="4082"?"提示":null;
        t.Equal("提示",p.LabelText(Named(p,"Tip_Label")),"Tip label through TextResolver");
        var c=new UiNullCommands();
        p.Mouse(M(UiMouseKind.Move,new(110+2,200+2)),c);
        t.Check(Near(Pixel(p,110+2,200+2),MainOkC),"OK button disabled (points left) → no highlight");
        p.Mouse(M(UiMouseKind.Move,new(234+2,122+2)),c);
        t.Check(Near(Pixel(p,234+2,122+2),UpC),"Up highlight = same image");
        p.Mouse(M(UiMouseKind.Down,new(234+2,122+2)),c);
        t.Check(Near(Pixel(p,234+2,122+2),UpPushC),"Up push image while held");
        p.Mouse(M(UiMouseKind.Up,new(234+2,122+2)),c);
        host.View=Level(20);p.Sync();
        using(var b=Render(p))t.Check(Near(b.GetPixel(20+2,260+2),ProgOnC)&&Near(b.GetPixel(60+2,260+2),ProgOnC)&&Near(b.GetPixel(40+2,262+2),ArrowOnC)&&Near(b.GetPixel(100+2,260+2),ProgOffC)&&Near(b.GetPixel(80+2,262+2),ArrowOffC),"level: Reset+LevelUp On, arrow 1 On, Done Off");
        host.View=Level(20) with{Phase="waiting"};p.Sync();
        t.Equal(1,p.Stage,"waiting keeps the last stage");
        host.View=Level(60) with{Phase="elixir",Points=3};p.Sync();
        t.Equal(2,p.Stage,"elixir → Done stage");
    }
    static bool Near(Color c,uint argb){var e=Color.FromArgb(unchecked((int)argb));return Math.Abs(c.R-e.R)<=3&&Math.Abs(c.G-e.G)<=3&&Math.Abs(c.B-e.B)<=3;}
    static bool Near(int c,uint argb)=>Near(Color.FromArgb(c),argb);

    static void Editing(WorldModuleTestContext t){
        var p=Panel();var host=new Host(Initial());var c=new UiNullCommands();p.Begin(host);
        var str=new Point(234+2,122+2);var strDown=new Point(222+2,122+2);
        Click(p,strDown,c);
        t.Equal(10,p.Draft.Str,"Down at base value does nothing");
        for(int i=0;i<15;i++)Click(p,str,c);
        t.Equal(25,p.Draft.Str,"Up raises STR by one per click (15 points)");
        t.Equal(0,p.Remaining,"no points left");
        Click(p,new(294+2,122+2),c);
        t.Equal(10,p.Draft.Int,"Up refused with 0 points left (Intelligence)");
        Click(p,strDown,c);
        t.Check(p.Draft.Str==24&&p.Remaining==1,"Down returns a point");
        Click(p,str,c);
        // Wisdom (row 2 of the right column, wire index 2) and Dexterity (row 2 left, wire index 3) map to the right fields.
        Click(p,new(234+2-12,142+2),c);
        t.Equal(10,p.Draft.Dex,"Down on Dexterity at base does nothing");
        t.Equal(0,host.Sent.Count,"nothing sent while editing");
        Click(p,new(110+2,200+2),c);
        t.Equal(ResetMessage.Confirm,p.Shown,"OK with 0 left → confirm box");
        t.Equal(ResetStatPanel.ConfirmText,p.LabelText(p.Message!.Pieces.First(x=>x.Name=="Message_Label")),"confirm text");
        using(var b=Render(p))t.Check(Near(b.GetPixel(100+20+2,100+80+2),OkC)&&Near(b.GetPixel(100+5,100+70),MsgBackC),"message box drawn over the main window (Back_Image under the buttons)");
        Click(p,str,c);
        t.Equal(25,p.Draft.Str,"main window does not take clicks while the box is shown");
        Click(p,new(100+150+2,100+80+2),c);
        t.Check(p.Shown==ResetMessage.None&&host.Sent.Count==0,"Cancel closes the box, nothing sent");
        host.After=(v,cmd,s,step)=>v with{Phase="waiting",Revision=v.Revision+1};
        Click(p,new(110+2,200+2),c);Click(p,new(100+20+2,100+80+2),c);
        t.Check(host.Sent.Count==1&&host.Sent[0] is {Command:"initial",Step:0,Revision:1}&&host.Sent[0].Stats==new ResetStats(25,10,10,10,10,10),"Ok sends initial with the draft and the stamped revision",host.Sent.Count==1?host.Sent[0]:null);
        t.Check(p.Visible&&p.View?.Phase=="waiting"&&p.Error==null,"window stays open while waiting");
        host.View=Level(1,stats:new(25,10,10,10,10,10)) with{Revision=3};p.Sync();
        t.Equal(25,p.Draft.Str,"new revision reloads the values");
        // Rejected send.
        var q=Panel();var h2=new Host(Initial()){Accept=false};q.Begin(h2);
        for(int i=0;i<15;i++)Click(q,str,c);
        Click(q,new(110+2,200+2),c);Click(q,new(100+20+2,100+80+2),c);
        t.Check(h2.Sent.Count==1&&q.Error==ResetStatPanel.RejectedText&&q.Visible,"rejected send → error shown, window stays");
        // Elixir phase: Points = elixir points; base = current stats.
        var e=Panel();var h3=new Host(Level(60) with{Phase="elixir",Points=2});e.Begin(h3);
        Click(e,str,c);Click(e,str,c);Click(e,str,c);
        t.Check(e.Draft.Str==20&&e.Remaining==0,"elixir: only the elixir points can be placed",e.Draft);
        Click(e,new(110+2,200+2),c);Click(e,new(100+20+2,100+80+2),c);
        t.Check(h3.Sent.Count==1&&h3.Sent[0].Command=="elixir"&&h3.Sent[0].Stats.Str==20,"elixir confirm sends elixir");
    }

    static void Leveling(WorldModuleTestContext t){
        var p=Panel();var host=new Host(Level(20));var c=new UiNullCommands();p.Begin(host);
        var l1=new Point(60+2,200+2);var l10=new Point(10+2,200+2);var ok=new Point(110+2,200+2);var str=new Point(234+2,122+2);
        Click(p,l1,c);
        t.Check(host.Sent.Count==1&&host.Sent[0] is {Command:"level",Step:0},"1Level below 50 → level step 0");
        Click(p,l10,c);
        t.Check(host.Sent.Count==2&&host.Sent[1] is {Command:"level",Step:7},"10Level → level step 7");
        Click(p,str,c);Click(p,ok,c);
        t.Check(host.Sent.Count==2&&p.Shown==ResetMessage.None,"below 50: Up and OK do nothing");
        host.View=Level(45,rev:6);p.Sync();
        Click(p,l10,c);
        t.Equal(2,host.Sent.Count,"10Level refused when it would pass 50");
        host.View=Level(52,rev:7);p.Sync();
        Click(p,l1,c);
        t.Equal(2,host.Sent.Count,"1Level refused from 50 (an attribute must be chosen)");
        Click(p,new(294+2,122+2),c);
        t.Check(host.Sent.Count==3&&host.Sent[2] is {Command:"level",Step:2},"Up on Intelligence at 52 → level step 2 (wire index 1 + 1)",host.Sent.Count>2?host.Sent[2]:null);
        host.View=Level(52,rev:8,stats:new(35,12,12,11,12,10));p.Sync();
        Click(p,str,c);
        t.Equal(3,host.Sent.Count,"STR already 35 → refused");
        host.View=Level(60,rev:9);p.Sync();
        Click(p,ok,c);
        t.Equal(ResetMessage.Confirm,p.Shown,"max level → OK opens the confirm box");
        Click(p,new(100+20+2,100+80+2),c);
        t.Check(host.Sent.Count==4&&host.Sent[3].Command=="finish","confirm at max level sends finish");
        host.View=Level(60,rev:10) with{CanAct=false};p.Sync();
        Click(p,ok,c);
        t.Check(p.Shown==ResetMessage.None&&host.Sent.Count==4,"CanAct false → nothing");
        host.View=Level(60,rev:11) with{Phase="ended",CanAct=false};p.Sync();
        Click(p,ok,c);
        t.Check(!p.Visible&&c.Calls.Contains("game-focus"),"ended → OK closes and returns game focus");
    }

    static void Leaving(WorldModuleTestContext t){
        var p=Panel();var host=new Host(Initial());var c=new UiNullCommands();p.Begin(host);
        var esc=new UiKeyEvent(Keys.Escape,true,false);
        t.Check(p.Key(new UiKeyEvent(Keys.A,true,false),c),"modal: other keys are swallowed");
        t.Check(p.Key(esc,c)&&p.Shown==ResetMessage.Leave,"Esc during a reset → leave prompt");
        t.Equal(ResetStatPanel.LeaveText,p.LabelText(p.Message!.Pieces.First(x=>x.Name=="Message_Label")),"leave text");
        p.Key(esc,c);
        t.Check(p.Shown==ResetMessage.None&&p.Visible,"Esc again closes the prompt only");
        p.Key(esc,c);Click(p,new(100+20+2,100+80+2),c);
        t.Check(host.Cancels==1&&!p.Visible,"Ok on the leave prompt → host Cancel and close");
        t.Check(p.Mouse(M(UiMouseKind.Down,new(5,5)),c)==false,"closed: no input taken");
        var q=Panel();var h2=new Host(Initial());q.Begin(h2);
        h2.View=Initial() with{Generation=4};q.Sync();
        t.Check(!q.Visible,"new login generation → closes (same as the WinForms dialog)");
        var r=Panel();var h3=new Host(Initial());r.Begin(h3);
        h3.View=Initial() with{Epoch=8};r.Sync();
        t.Check(r.Visible,"initial offer follows the arrival epoch");
        h3.View=Level(1) with{Epoch=8};r.Sync();
        t.Check(r.Visible,"…and stays bound to it");
        h3.View=Level(1) with{Epoch=9};r.Sync();
        t.Check(!r.Visible,"world change after the offer → closes");
        var s=Panel();var h4=new Host(Initial() with{Phase="ended",CanAct=false});s.Begin(h4);
        s.Key(esc,c);
        t.Check(!s.Visible&&h4.Cancels==0,"Esc after the reset ended → just closes");
        t.Check(s.Mouse(M(UiMouseKind.Down,new(390,290)),c)==false,"closed → mouse not taken");
        var u=Panel();u.Begin(new Host(Initial()));
        t.Check(u.Mouse(M(UiMouseKind.Down,new(390,290)),c),"open → every press is taken (modal)");
    }

    static void Broken(WorldModuleTestContext t){
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("<ResetStatUI>","<Other>").Replace("</ResetStatUI>","</Other>")),"wrong root → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("Name = \"Message_Window\"","Name = \"Other_Window\"")),"no message window → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("Name = \"ResetStat_Win\"","Name = \"Other_Win\"")),"no main window → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("Name = \"Charisma\"","Name = \"Luck\"")),"a stat Control missing → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("Surf = \"156\"","Surf = \"abc\"")),"bad image id → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("Window Name = \"ResetStat_Win\" X = \"0\" ","Window Name = \"ResetStat_Win\" ")),"main window without X → UiLayoutException");
        var sized=Panel(Xml(mainSize:""));
        t.Check(sized.Available&&sized.Main!.Size==new Size(400,300),"no declared size → main Back_Image size");
        var nosize=Panel(Xml(mainSize:"").Replace("Surf = \"156\"","Surf = \"999\""));
        t.Check(!nosize.Available&&!nosize.Begin(new Host(Initial())),"no size and no back image → unavailable");
        var nomsg=Panel(Xml(msgBase:"BaseSurf = \"998\"").Replace("Surf = \"487;1\"","Surf = \"997\""));
        t.Check(!nomsg.Available&&nomsg.Missing.Any(m=>m.StartsWith("surf 998")),"message box images all missing → unavailable (no confirm possible)",nomsg.Unavailable);
        var backOnly=Panel(Xml(msgBase:"BaseSurf = \"998\""));
        t.Check(backOnly.Available&&backOnly.Message!.Size==new Size(200,100),"message BaseSurf missing → Back_Image size, listed missing");
    }

    // ---- StatInfoUI ---------------------------------------------------------------------------------------------------------

    static string InfoXml(string window="BasePng = \"911;2;28\"",string list="Vertical=\"1\"")=>$"""
        <?xml version="1.0"?>
        <StatInfoUI Toggle="true">
          <Window Name="BonusStatusTooltip_Window" X="0" Y="0" {window} Moveable = "90" MouseEvent = "Dummy" KeyboardEvent="CloseStatInfoWindow">
            <List Name="StatList" X="10" Y="4" Width="100" Height="90" {list}>
              <Control Name="StatDmg" Width="100" Height="15">
                <Label Name="DmgBonus_Label" Width="60" Height="14" VAlign="middle" String="3857" FontColor="150,150,150"/>
                <Label Name="DmgBonus_Value" X="61" Width="30" Height="14" VAlign="middle" HAlign="center" Caption="0" FontColor="150,150,150"/>
              </Control>
              <Control Name="StatHit" Width="100" Height="15">
                <Label Name="HitBonus_Label" Width="60" Height="14" VAlign="middle" String="3858" FontColor="150,150,150"/>
                <Label Name="HitBonus_Value" X="61" Width="30" Height="14" VAlign="middle" HAlign="center" Caption="0" FontColor="150,150,150"/>
              </Control>
              <Control Name="StatCarry" Width="100" Height="15">
                <Label Name="CarryBonus_Label" Width="60" Height="14" VAlign="middle" String="3860"/>
                <Label Name="CarryBonus_Value" X="61" Width="30" Height="14" VAlign="middle" HAlign="center" Caption="0"/>
              </Control>
            </List>
          </Window>
        </StatInfoUI>
        """;
    static StatInfoPanel Info(string? xml=null,UiRoot? root=null){
        var p=new StatInfoPanel(null,root);var g=Pngs();
        p.Load(Layout(xml??InfoXml()),_=>null,id=>g.GetValueOrDefault(id));
        return p;
    }

    [WorldModuleTest("stat-info-ui")]
    static void RunInfo(WorldModuleTestContext t){
        var p=Info();
        t.Check(p.Available,"synthetic StatInfoUI usable",p.Unavailable);
        t.Equal(new Size(120,100),p.Size,"size = BasePng image (two flags ignored)");
        t.Equal(90,p.MoveableHeight,"Moveable height");
        t.Equal("StatDmg,StatHit,StatCarry",string.Join(",",p.Rows.Select(r=>r.Name)),"rows in XML order");
        t.Equal(new Point(10,4),p.Rows[0].LabelOffset,"first row at the List origin");
        t.Equal(new Point(10,19),p.Rows[1].LabelOffset,"vertical List: next row after Height 15");
        t.Equal(new Point(71,34),p.Rows[2].ValueOffset,"value label X relative to its row");
        t.Check(p.Rows[0] is {LabelKind:"String",LabelValue:"3857",ValueName:"DmgBonus_Value",ValueHAlign:"center"}&&p.Rows[0].LabelColor==Color.FromArgb(150,150,150),"row keeps String id, value name, alignment, colour");
        t.Check(p.LabelText(p.Rows[0])==null&&p.ValueText(p.Rows[0])==null,"no resolver / no values → blank (Caption \"0\" never drawn)");
        p.TextResolver=(k,v)=>k=="String"&&v=="3857"?"傷害":null;p.ValueProvider=n=>n=="DmgBonus_Value"?"+2":null;
        t.Check(p.LabelText(p.Rows[0])=="傷害"&&p.ValueText(p.Rows[0])=="+2","resolver and value provider used");
        t.Check(!p.Visible&&p.Open()&&p.Visible&&p.Bounds==new Rectangle(0,0,120,100),"opens at the XML origin");
        using(var b=Render(p)){
            t.Check(Near(b.GetPixel(115,95),InfoBaseC),"base png drawn");
            bool text=false;for(int x=10;x<70;x++)for(int y=4;y<18;y++)if(!Near(b.GetPixel(x,y),InfoBaseC))text=true;
            t.Check(text,"resolved label text drawn in its row");
        }
        var c=new UiNullCommands();
        t.Check(p.Mouse(M(UiMouseKind.Down,new(50,50)),c)&&!p.Mouse(M(UiMouseKind.Down,new(150,50)),c),"presses inside swallowed (Dummy), outside not");
        t.Check(p.IsDragHandle(new(50,50))&&!p.IsDragHandle(new(50,95)),"drag strip = Moveable 90");
        p.MoveTo(new(300,200));
        t.Equal(new Rectangle(300,200,120,100),p.Bounds,"moved");
        t.Check(!p.Key(new UiKeyEvent(Keys.A,true,false),c)&&p.Key(new UiKeyEvent(Keys.Escape,true,false),c)&&!p.Visible,"Esc closes (CloseStatInfoWindow); other keys pass");
        t.Check(p.Toggle()&&p.Visible&&p.Toggle()&&!p.Visible,"Toggle");

        // Follows RenewalStatusPanel's StatusBonus_Button.
        var root=new UiRoot();
        var status=new RenewalStatusPanel();
        status.Load(UiLayout.Parse("""
            <RenewalStatusUI>
              <Window Name="Status_Window" X="100" Y="50" BasePng="903" Moveable="20">
                <CheckButton Name="StatusBonus_Button" X="10" Y="10" BasePng="904" PushPng="904" MouseEvent="ShowStatInfoTooltip"/>
              </Window>
            </RenewalStatusUI>
            ""","ReNStatusUIEx.xml"),_=>null,id=>id switch{903=>Surface(903,180,200,BackC),904=>Surface(904,10,10,OkC),_=>null});
        var info=Info(root:root);root.Add(status);root.Add(info);
        var world=new UiStaticGameState(InWorld:true);
        info.Update(world);
        t.Check(!info.Visible,"closed while the status window is closed");
        status.Open();info.Update(world);
        t.Check(!info.Visible,"status open but bonus button unchecked → closed");
        Click(status,new(100+10+2,50+10+2),c);info.Update(world);
        t.Check(info.Visible&&info.Bounds==new Rectangle(280,50,120,100),"checked → opens at the status window's right edge",info.Bounds);
        info.Key(new UiKeyEvent(Keys.Escape,true,false),c);info.Update(world);
        t.Check(!info.Visible,"closed by Esc stays closed while still checked");
        Click(status,new(100+10+2,50+10+2),c);info.Update(world);
        Click(status,new(100+10+2,50+10+2),c);info.Update(world);
        t.Check(info.Visible,"uncheck + check again → opens again");
        status.MoveTo(new(700,0));Click(status,new(700+10+2,10+2),c);info.Update(world);Click(status,new(700+10+2,10+2),c);info.Update(world);
        t.Equal(new Rectangle(680,0,120,100),info.Bounds,"kept inside 800×600");
        status.Close();info.Update(world);
        t.Check(!info.Visible,"status window closed → closes");
        info.Open();info.Update(new UiStaticGameState(InWorld:false));
        t.Check(!info.Visible,"leaving the world closes");

        t.Throws<UiLayoutException>(()=>Info(InfoXml().Replace("BonusStatusTooltip_Window","Other_Window")),"no window → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Info(InfoXml().Replace("<StatInfoUI Toggle=\"true\">","<Other>").Replace("</StatInfoUI>","</Other>")),"wrong root → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Info(InfoXml().Replace("Width=\"100\" Height=\"15\"","Width=\"100\"")),"vertical row without Height → UiLayoutException");
        var flat=Info(InfoXml(list:""));
        t.Check(flat.Rows.All(r=>r.LabelOffset==new Point(10,4)),"non-vertical List does not stack");
        var nobase=Info(InfoXml(window:"BasePng = \"9999\""));
        t.Check(!nobase.Available&&!nobase.Open()&&nobase.Missing.Any(m=>m.StartsWith("png 9999")),"base png missing → unavailable, listed missing");
        var none=new StatInfoPanel();none.Load(Layout(InfoXml()),null!);
        t.Check(!none.Available&&none.Unavailable=="沒有 UiResources","product Load without UiResources → unavailable");
    }
}
