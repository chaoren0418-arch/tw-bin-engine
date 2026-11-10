namespace P06.NativeUi;

// T328 (#766) renewal-status-panel 離線測試（狀態窗）：版面 XML 與圖片全部是記憶體內合成資料，元素種類仿 ReNStatusUIEx.xml，
// 不是真實檔案；不讀真實客戶端或參考客戶端、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class RenewalStatusPanelTests {
    const uint EquipBaseC=0xFF202020,StatusBaseC=0xFF303030,CloseC=0xFF0000FF,ClosePushC=0xFF000080,TabAC=0xFF008000,TabBC=0xFF00FF00,
        SlotC=0xFFFF0000,GaugeC=0xFFFFFF00,UpC=0xFF00FFFF,UpPushC=0xFF008080,DownC=0xFFFF00FF,CheckC=0xFF800000,CheckPushC=0xFFFF8000,AlignC=0xFF808080;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Dictionary<int,UiSurface> Pngs()=>new(){
        [910]=Surface(910,180,200,EquipBaseC),[903]=Surface(903,180,200,StatusBaseC),[899]=Surface(899,100,6,GaugeC),
        [906]=Surface(906,8,8,UpC),[908]=Surface(908,8,8,UpPushC),[901]=Surface(901,8,8,DownC),[904]=Surface(904,10,10,CheckC),[905]=Surface(905,10,10,CheckPushC),
    };
    static Dictionary<int,UiSurface> Surfs()=>new(){
        [902]=Surface(902,10,10,CloseC),[903]=Surface(903,10,10,ClosePushC),[366]=Surface(366,30,12,TabAC),[368]=Surface(368,40,12,TabBC),
        [479]=Surface(479,32,32,SlotC),[455]=Surface(455,6,6,AlignC),
    };

    // Synthetic layout with the element kinds of ReNStatusUIEx.xml: two tab pages with Close, ItemIcon+Tooltip, slot Images,
    // an Activate="0" image, a missing surf, a horizontal List of RadioButton tabs, Gauge, value Labels with Up/Down buttons,
    // a String-id label, a CheckButton, and the karma tooltip window.
    static string Xml(string equipBase="BasePng = \"910;1\"",string statusBase="BasePng = \"903;1\"")=>$"""
        <?xml version="1.0"?>
        <RenewalStatusUI Toggle="true">
          <Window Name="Equip_Window" X="100" Y="50" {equipBase} Moveable = "20" MouseEvent = "Dummy">
            <Button Name = "CloseWindow_Button" X = "162" Y = "9" BaseSurf = "902" PushSurf = "903" MouseEvent = "CloseWindow"/>
            <ItemIcon Name = "Helmet_Icon" Width = "32" Height = "32" X = "75" Y = "33" MouseEvent = "RemoveItem">
              <Tooltip Name = "Desc_Tooltip" VAlign="bottom" OpacityRate = "25"/>
            </ItemIcon>
            <Image Name = "Helmet_Icon" Width = "32" Height = "32" X = "74" Y = "32" Surf="479;1"/>
            <Image Name="OpenRRuneSlot_Image" X="120" Y="100" Surf="479" Activate="0"/>
            <Image Name="Gone_Image" X="10" Y="100" Surf="777"/>
            <Label Name = "RemainTimeSlot59_Label" X = "121" Y = "52" Width = "32" Height = "14" String="1" HAlign="center" VAlign="middle"/>
            <List Name = "Maintab_List" X = "17" Y = "170" Width = "149" Height = "22" Horizontal = "1">
              <RadioButton Name = "EquipTab_Button" BaseSurf = "366" PushSurf = "366" DisableSurf = "367" MouseEvent = "ChangeTab">
                <Tooltip Name = "Desc_Tooltip" String="882" X = "1" Y = "20"/>
              </RadioButton>
              <RadioButton Name = "StatusTab_Button" BaseSurf = "368" PushSurf = "368" DisableSurf = "369" MouseEvent = "ChangeTab"/>
            </List>
          </Window>
          <Window Name="Status_Window" X="0" Y="0" {statusBase} Moveable = "20" MouseEvent = "Dummy">
            <Button Name = "CloseWindow_Button" X = "162" Y = "9" BaseSurf = "902" PushSurf = "903" MouseEvent = "CloseWindow"/>
            <Label Name = "HpGauge_Label" X = "60" Y = "52" Width = "100" Height = "10" String="1;2" HAlign="center" VAlign="middle"/>
            <Gauge Name = "HpGauge_Image" X = "58" Y = "51" Png="899;1"/>
            <Gauge Name = "LevGauge_Image" X = "58" Y = "34" Png="899;1"/>
            <Label Name = "Str_Label" X = "44" Y = "122" Width = "35" Height = "12" HAlign="center" VAlign="middle" Caption="11">
              <Button Name = "Up_Button" X = "37" Y = "1" BasePng = "906" HighlightPng = "906" PushPng = "908;1" DisablePng = "907" MouseEvent = "StatPointUP"/>
              <Button Name = "Down_Button" X = "37" Y = "1" BasePng = "901" PushPng = "902;1" MouseEvent = "StatPointDown"/>
              <Tooltip Name="Desc_Tooltip" X="1" Y="13"/>
            </Label>
            <Label Name = "Elixir_Label" X = "56" Y = "90" Width = "33" Height = "12" Caption = "0"/>
            <Label Name = "Mystery_Label" X = "56" Y = "100" Caption = "5"/>
            <Label Name = "Title_Label" X = "3" Y = "4" String="3233" FontColor="191,221,255"/>
            <Image Name = "Align_Image" X = "122" Y = "182" Surf="455;1"/>
            <CheckButton Name = "StatusBonus_Button" X = "22" Y = "150" BasePng = "904" PushPng = "905" MouseEvent="ShowStatInfoTooltip"/>
            <List Name = "Maintab_List" X = "15" Y = "170" Horizontal = "1">
              <RadioButton Name = "EquipTab_Button" BaseSurf = "366" PushSurf = "366" MouseEvent = "ChangeTab"/>
              <RadioButton Name = "StatusTab_Button" BaseSurf = "368" PushSurf = "368" MouseEvent = "ChangeTab"/>
            </List>
          </Window>
          <Window Name="KarmaTooltip_Window" X="0" Y="0" BaseSurf = "375;1" Moveable = "0" MouseEvent = "Dummy">
            <Label Name = "KarmaSide_Label" X = "3" Y = "4" String="3233"/>
          </Window>
        </RenewalStatusUI>
        """;
    static UiLayout Layout(string xml)=>UiLayout.Parse(xml,"ReNStatusUIEx-c.xml");
    static RenewalStatusPanel Panel(string? xml=null){
        var p=new RenewalStatusPanel();var png=Pngs();var surf=Surfs();
        p.Load(Layout(xml??Xml()),id=>surf.GetValueOrDefault(id),id=>png.GetValueOrDefault(id));
        return p;
    }
    static RenewalStatusView View(bool canRaise=false)=>new(52,30,120,10,40,18,12,13,14,15,16,-1200,7,8,9,10,null,canRaise);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(RenewalStatusPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static Bitmap Render(RenewalStatusPanel p){var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();var b=(Bitmap)canvas.Bitmap.Clone();canvas.Dispose();return b;}
    static int Pixel(RenewalStatusPanel p,int x,int y){using var b=Render(p);return b.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();
    static StatusPiece Named(RenewalStatusPanel p,string page,string name)=>p.Pages.Single(x=>x.Name==page).Pieces.First(x=>x.Name==name);

    [WorldModuleTest("renewal-status-panel")]
    static void Run(WorldModuleTestContext t){
        Registration(t);
        Parsing(t);
        Values(t);
        Painting(t);
        Input(t);
        Unavailable(t);
        Confirm(t);
    }

    static void Registration(WorldModuleTestContext t){
        var regs=UiPanelRegistry.Panels.Where(r=>r.Id==RenewalStatusPanel.PanelId).ToArray();
        t.Check(regs.Length==1&&regs[0].Layout==RenewalStatusPanel.HostLayout,"RenewalStatusUI registered once, on the host layout (Build never needs ReNStatusUIEx.xml)",string.Join(",",regs.Select(r=>r.Layout)));
        t.Check(!UiPanelRegistry.Errors.Any(e=>e.Contains(nameof(RenewalStatusPanel))),"no discovery error for RenewalStatusPanel");
        var panel=regs[0].Create(new UiRoot());
        t.Check(panel is RenewalStatusPanel&&panel is IUiClosablePanel&&panel is IUiMovablePanel&&panel.Layer==UiLayer.Window,"window layer, closable, movable");
        t.Check(!panel.Visible&&((RenewalStatusPanel)panel).Unavailable!=null,"closed and unavailable until loaded");
    }

    static void Parsing(WorldModuleTestContext t){
        var p=Panel();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Equal("ReNStatusUIEx-c.xml",p.LayoutFile,"layout file recorded");
        t.Equal("Equip_Window,Status_Window",string.Join(",",p.Pages.Select(x=>x.Name)),"two tab pages, XML order; karma tooltip window not built");
        t.Check(p.Skipped.Any(s=>s.Contains("Window[2]")&&s.Contains("Karma")),"karma tooltip window listed as skipped",string.Join(" | ",p.Skipped));
        t.Equal(new Point(100,50),p.Origin,"origin = first window X/Y");
        t.Equal(new Size(180,200),p.Pages[0].Size,"page size = BasePng image");
        t.Equal(20,p.Pages[1].MoveableHeight,"Moveable=20");
        t.Check(p.Missing.Contains("surf 777 (RenewalStatusUI/Window[0]/Image[2])"),"missing surf listed with its path",string.Join(" | ",p.Missing));
        t.Check(p.Missing.Contains("png 907 (RenewalStatusUI/Window[1]/Label[1]/Button[0])")==false,"DisablePng is not resolved");
        t.Check(!p.Missing.Any(m=>m.Contains("375")),"karma tooltip surf not resolved (window not built)");
        t.Check(!p.Missing.Any(m=>m.Contains("367")||m.Contains("369")),"DisableSurf not resolved");
        t.Equal(1,p.Slots.Count,"ItemIcon kept as a slot");
        t.Equal(new Rectangle(75,33,32,32),p.Slots[0].HitLocal,"slot rect from X/Y/Width/Height");
        t.Check(p.Skipped.Any(s=>s.EndsWith("ItemIcon[0]/Tooltip[0]")),"slot tooltip skipped");
        var tabs=p.Pages[0].Pieces.Where(x=>x.Element=="RadioButton").ToArray();
        t.Equal(new Point(17,170),tabs[0].Offset,"first tab at the List origin");
        t.Equal(new Point(47,170),tabs[1].Offset,"horizontal List: second tab after the first one's width (30)");
        t.Check(!Named(p,"Equip_Window","OpenRRuneSlot_Image").Active,"Activate=\"0\" kept inactive");
        var up=Named(p,"Status_Window","Up_Button");
        t.Equal(new Point(81,123),up.Offset,"Up button relative to its label (44+37, 122+1)");
        t.Equal("Str_Label",up.Owner,"Up button owned by Str_Label");
        t.Check(up.PushSurface!=null&&up.Image is {Id:906,IsSurf:false},"Up button: png 906, push 908");
        t.Check(p.Pages[1].Pieces.All(x=>x.Name!="Mystery_Label"),"label with only a Caption (not a known value) is not built");
        t.Check(p.Skipped.Any(s=>s.Contains("Label[3]")&&s.Contains("Caption")),"…and listed as skipped",string.Join(" | ",p.Skipped));
        var title=Named(p,"Status_Window","Title_Label");
        t.Check(title is {TextKind:"String",TextValue:"3233"}&&title.Color==Color.FromArgb(191,221,255),"String-id label keeps id and FontColor");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("<RenewalStatusUI Toggle=\"true\">","<Other>").Replace("</RenewalStatusUI>","</Other>")),"wrong root → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("Name=\"Status_Window\"","Name=\"Equip_Window\"")),"duplicate page → UiLayoutException");
    }

    static void Values(WorldModuleTestContext t){
        var p=Panel();
        p.Show(View());
        t.Equal("30/120",p.LabelText(Named(p,"Status_Window","HpGauge_Label")),"HP label = cur/max");
        t.Equal("18",p.LabelText(Named(p,"Status_Window","Str_Label")),"Str label = value, never the Caption 11");
        t.Check(p.LabelText(Named(p,"Status_Window","Elixir_Label"))==null,"unknown value (elixir) blank");
        t.Check(p.LabelText(Named(p,"Equip_Window","RemainTimeSlot59_Label"))==null,"slot remaining time blank (String=\"1\" is a placeholder)");
        t.Check(p.LabelText(Named(p,"Status_Window","Title_Label"))==null,"String id without TextResolver → not drawn");
        p.TextResolver=(k,v)=>k=="String"&&v=="3233"?"陣營":null;
        t.Equal("陣營",p.LabelText(Named(p,"Status_Window","Title_Label")),"String id through TextResolver");
        t.Equal(0.25,RenewalStatusPanel.GaugeRatio("HpGauge_Image",View()),"HP gauge ratio 30/120");
        t.Check(RenewalStatusPanel.GaugeRatio("LevGauge_Image",View())==null,"level gauge not drawn (exp % unknown)");
        t.Check(RenewalStatusPanel.GaugeRatio("HpGauge_Image",View() with{MaxHp=0})==null,"max 0 → not drawn");
        var own=new OwnStatus(1,52,0,18,12,14,13,15,16,30,120,10,40,-10,0,0,0,-1200,8,9,10,7,99);
        var v=RenewalStatusPanel.ViewOf(own,true,"str");
        t.Check(v is {Level:52,Hp:30,MaxHp:120,Str:18,Int:12,Wis:14,Dex:13,Con:15,Cha:16,Lawful:-1200,FireResist:8,WaterResist:9,WindResist:10,EarthResist:7,PkCount:null,CanRaise:true,Selected:"str"},"OwnStatus → view (PK count not taken from Kills)",v);
        t.Check(RenewalStatusPanel.ViewOf(null,true) is {Level:null,Str:null,CanRaise:true},"no status → blank values");
    }

    static void Painting(WorldModuleTestContext t){
        var p=Panel();
        t.Check(!p.Visible&&p.Open()&&p.Visible,"opens");
        t.Equal("Equip_Window",p.ActivePage?.Name,"first page on open");
        t.Equal(Argb(EquipBaseC),Pixel(p,100+5,50+150),"equip base png at origin");
        t.Equal(Argb(SlotC),Pixel(p,100+74+5,50+32+5),"slot frame surf drawn at X/Y");
        t.Equal(Argb(EquipBaseC),Pixel(p,100+120+5,50+100+5),"inactive image not drawn");
        t.Equal(Argb(EquipBaseC),Pixel(p,100+10+2,50+100+2),"missing surf draws nothing (no substitute)");
        t.Equal(Argb(CloseC),Pixel(p,100+162+2,50+9+2),"close button surf");
        t.Equal(Argb(TabBC),Pixel(p,100+47+2,50+170+2),"second tab image after the first");
        t.Check(p.ShowPage("Status_Window"),"switch to status page");
        p.Show(View());
        t.Equal(Argb(StatusBaseC),Pixel(p,100+5,50+150),"status base png");
        t.Equal(Argb(GaugeC),Pixel(p,100+58+10,50+51+2),"HP gauge filled up to 25%");
        t.Equal(Argb(StatusBaseC),Pixel(p,100+58+95,50+51+2),"HP gauge empty past 25% (right of the centred HP text)");
        t.Equal(Argb(StatusBaseC),Pixel(p,100+58+10,50+34+2),"level gauge not drawn");
        t.Equal(Argb(StatusBaseC),Pixel(p,100+81+2,50+123+2),"Up/Down buttons hidden while raising is not possible");
        p.Show(View(canRaise:true));
        t.Equal(Argb(UpC),Pixel(p,100+81+2,50+123+2),"Up button drawn while bonus points can be chosen; Down only during a preview");
        using(var bmp=Render(p)){
            bool text=false;for(int x=104;x<100+44+35;x++)for(int y=50+122;y<50+134;y++)if(bmp.GetPixel(x,y).ToArgb()!=Argb(StatusBaseC))text=true;
            t.Check(text,"Str value text drawn inside its label box");
        }
    }

    static void Input(WorldModuleTestContext t){
        var p=Panel();var c=new UiNullCommands();p.Open();
        var keys=new List<string>();p.StatPointConfirm=keys.Add;
        Click(p,new(100+47+2,50+170+2),c);
        t.Equal("Status_Window",p.ActivePage?.Name,"StatusTab_Button → Status_Window");
        Click(p,new(100+81+2,50+123+2),c);
        t.Equal(0,keys.Count,"hidden Up button does not fire");
        p.Show(View(canRaise:true));
        p.Mouse(M(UiMouseKind.Down,new(100+81+2,50+123+2)),c);
        t.Equal(Argb(UpPushC),Pixel(p,100+81+2,50+123+2),"Up push image while held");
        p.Mouse(M(UiMouseKind.Up,new(100+81+2,50+123+2)),c);
        t.Check(keys.Count==0&&p.Pending=="str","Up click → local preview only (Pending str, nothing confirmed)",p.Pending);
        p.Answer(false);
        p.Mouse(M(UiMouseKind.Down,new(100+81+2,50+123+2)),c);
        p.Show(View());
        p.Mouse(M(UiMouseKind.Up,new(100+81+2,50+123+2)),c);
        t.Check(keys.Count==0&&p.Pending==null,"raising ended while held → no click, no preview");
        Click(p,new(100+22+2,50+150+2),c);
        t.Check(p.StatInfoChecked&&p.Unhandled.Contains("ShowStatInfoTooltip"),"check button toggles and is reported unhandled");
        t.Equal(Argb(CheckPushC),Pixel(p,100+22+2,50+150+2),"checked → push image");
        t.Check(p.IsDragHandle(new(100+50,50+5))&&!p.IsDragHandle(new(100+50,50+30))&&!p.IsDragHandle(new(100+162+2,50+9+2)),"drag strip = Moveable 20 px, not over the close button");
        p.MoveTo(new(300,200));
        t.Equal(new Rectangle(300,200,180,200),p.Bounds,"moved");
        Click(p,new(300+15+2,200+170+2),c);
        t.Equal("Equip_Window",p.ActivePage?.Name,"EquipTab_Button → Equip_Window");
        Click(p,new(300+162+2,200+9+2),c);
        t.Check(!p.Visible,"CloseWindow closes");
        t.Check(c.Calls.Count==0,"no command calls");
        p.Open();p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible&&p.View==RenewalStatusView.Empty,"leaving the world closes and clears values");
        var root=new UiRoot();root.Add(p);
        t.Equal(UiToggleResult.Opened,root.Toggle(p,()=>p.Open()),"UiRoot.Toggle opens");
        t.Equal(UiToggleResult.Closed,root.Toggle(p,()=>p.Open()),"UiRoot.Toggle again closes");
    }

    static void Unavailable(WorldModuleTestContext t){
        var none=Panel(Xml(equipBase:"BasePng=\"9999\"",statusBase:"BasePng=\"9998\""));
        t.Check(!none.Available&&!none.Open()&&!none.Visible,"no base image on any page → unavailable, never opens",none.Unavailable);
        var one=Panel(Xml(equipBase:"BasePng=\"9999\""));
        t.Check(one.Available&&one.Pages[0].Size==new Size(180,200)&&one.Pages[0].BaseImage?.Surface==null,"missing equip base borrows the status page size, base not drawn");
        t.Check(one.Missing.Any(m=>m.StartsWith("png 9999")),"missing base listed");
        var empty=new RenewalStatusPanel();
        t.Check(!empty.Available&&!empty.Open(),"never loaded → unavailable");
    }

    // T378 (#862, FU9§14.5): Up = local +1 preview + Y/N; 是 → StatPointConfirm, 否／Down／Esc／close → preview dropped.
    static void Confirm(WorldModuleTestContext t){
        var p=Panel();var c=new UiNullCommands();p.Open();p.ShowPage("Status_Window");p.Show(View(canRaise:true));
        var keys=new List<string>();p.StatPointConfirm=keys.Add;
        var up=new Point(100+81+2,50+123+2);
        static UiKeyEvent K(Keys k)=>new(k,true,false);
        t.Check(p.ConfirmText==null&&p.Bounds==new Rectangle(100,50,180,200),"no preview: no Y/N, window bounds only");
        Click(p,up,c);
        t.Equal("str",p.Pending,"Up → preview str");
        t.Equal("19",p.LabelText(Named(p,"Status_Window","Str_Label")),"previewed stat shows value+1 (18 → 19)");
        t.Equal("12",p.LabelText(Named(p,"Status_Window","Int_Label")),"other stats unchanged");
        t.Check(!p.Shown(Named(p,"Status_Window","Up_Button"))&&p.Shown(Named(p,"Status_Window","Down_Button")),"during the preview: Up hidden, Down shown on the chosen stat");
        t.Equal(Argb(DownC),Pixel(p,100+81+2,50+123+2),"Down button drawn in place of Up");
        t.Equal(0,keys.Count,"preview sends nothing");
        t.Check(p.Bounds==new Rectangle(100,50,180,200+RenewalStatusPanel.ConfirmHeight)&&p.ConfirmBounds==new Rectangle(100,250,180,RenewalStatusPanel.ConfirmHeight),"Y/N strip under the window",p.Bounds);
        using(ServerMessageText.UseTable(null))t.Equal("請再次確認是否要提升力量？(Y/N)",p.ConfirmText,"no table → local row 929 text with the stat name");
        using(ServerMessageText.UseTable(Table(RenewalStatusPanel.ConfirmStringId,@"請確認\f2%s\f>好嗎")))t.Equal("請確認力量好嗎",p.ConfirmText,"table row 929, %s = stat name, colour codes dropped");
        t.Equal("請再次確認是否要提升體質？(Y/N)",WithoutTable(()=>RenewalStatusPanel.ConfirmTextFor("con")),"label per key (con → 體質)");
        // 否: NO button.
        Click(p,p.ConfirmButton(false).Location+new Size(2,2),c);
        t.Check(p.Pending==null&&keys.Count==0&&p.LabelText(Named(p,"Status_Window","Str_Label"))=="18","NO → preview dropped, value restored, nothing sent");
        t.Check(p.Shown(Named(p,"Status_Window","Up_Button"))&&!p.Shown(Named(p,"Status_Window","Down_Button")),"Up back, Down hidden");
        // 是: YES button.
        Click(p,up,c);Click(p,p.ConfirmButton(true).Location+new Size(2,2),c);
        t.Check(p.Pending==null&&string.Join(",",keys)=="str","YES → StatPointConfirm(\"str\") once",keys);
        // Down button = 否.
        Click(p,up,c);Click(p,up,c);
        t.Check(p.Pending==null&&keys.Count==1,"Down button cancels the preview");
        // Keys: Y／Enter = 是, N／Esc = 否; other keys pass through; no preview → keys not taken.
        Click(p,up,c);
        t.Check(!p.Key(K(Keys.A),c)&&p.Pending=="str","other key not taken");
        t.Check(!p.Key(new(Keys.Y,true,true),c)&&p.Pending=="str","auto-repeat ignored");
        t.Check(p.Key(K(Keys.Escape),c)&&p.Pending==null&&keys.Count==1,"Esc → 否");
        Click(p,up,c);t.Check(p.Key(K(Keys.N),c)&&p.Pending==null&&keys.Count==1,"N → 否");
        Click(p,up,c);t.Check(p.Key(K(Keys.Y),c)&&p.Pending==null&&keys.Count==2,"Y → 是");
        Click(p,up,c);t.Check(p.Key(K(Keys.Enter),c)&&p.Pending==null&&keys.Count==3,"Enter → 是");
        t.Check(!p.Key(K(Keys.Escape),c),"no preview: Esc not taken (window close path unchanged)");
        // Clicks inside the strip but off the buttons are swallowed (do not reach the world).
        Click(p,up,c);
        t.Check(p.Mouse(M(UiMouseKind.Down,new(102,250+2)),c)&&p.Pending=="str","strip click off the buttons: taken, preview kept");
        // Raising ends, close, leaving the world → preview dropped, nothing sent.
        p.Show(View());t.Check(p.Pending==null&&keys.Count==3,"raising no longer possible → preview dropped");
        p.Show(View(canRaise:true));Click(p,up,c);p.Close();t.Check(p.Pending==null&&keys.Count==3,"close → preview dropped");
        p.Open();p.ShowPage("Status_Window");Click(p,up,c);p.Update(new UiStaticGameState(InWorld:false));t.Check(p.Pending==null&&keys.Count==3,"leaving the world → preview dropped");
        t.Check(!p.Answer(true)&&keys.Count==3,"Answer without a preview does nothing");
        t.Check(c.Calls.Count==0,"no command calls");
    }
    static ServerStringTable Table(int id,string text){
        var lines=new string[id+1];Array.Fill(lines,"");lines[id]=text;
        return ServerStringTable.Parse(LoginPackets.TextEncoding.GetBytes((id+1)+"\n"+string.Join("\n",lines)));
    }
    static string WithoutTable(Func<string> f){using(ServerMessageText.UseTable(null))return f();}}
