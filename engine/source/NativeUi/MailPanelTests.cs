namespace P06.NativeUi;

// T324 (#762) mail-panel 離線測試（信件三窗）：版面 XML、圖片與信件全部是記憶體內合成資料，元素種類仿 RenewalMailUI.xml，
// 不是真實檔案；不讀真實客戶端或參考客戶端、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class MailPanelTests {
    const uint ListBaseC=0xFF202020,ReadBaseC=0xFF303030,WriteBaseC=0xFF404040,CloseC=0xFF0000FF,ClosePushC=0xFF000080,
        CheckC=0xFF800000,CheckPushC=0xFFFF8000,ThumbC=0xFF00FFFF,ComboBackC=0xFF008000,ComboItemC=0xFF00FF00,ButtonC=0xFFFFFF00;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Dictionary<int,UiSurface> Pngs(){
        var d=new Dictionary<int,UiSurface>{
            [782]=Surface(782,200,220,ListBaseC),[811]=Surface(811,200,220,ReadBaseC),[810]=Surface(810,200,220,WriteBaseC),
            [746]=Surface(746,8,8,ButtonC),[747]=Surface(747,8,8,ButtonC),[750]=Surface(750,56,40,ComboBackC),[751]=Surface(751,50,10,ComboItemC),
            [748]=Surface(748,10,10,CheckC),[749]=Surface(749,10,10,CheckPushC),[787]=Surface(787,30,10,ButtonC),[783]=Surface(783,30,10,ButtonC),
            [729]=Surface(729,12,70,ListBaseC),
        };
        foreach(int id in new[]{794,796,806,791,800,892,797,803})d[id]=Surface(id,20,10,ButtonC);
        return d;
    }
    static Dictionary<int,UiSurface> Surfs()=>new(){
        [183]=Surface(183,10,10,CloseC),[184]=Surface(184,10,10,ClosePushC),
        [446]=Surface(446,12,10,ButtonC),[447]=Surface(447,12,10,ButtonC),[448]=Surface(448,12,10,ThumbC),[444]=Surface(444,12,10,ButtonC),
        [429]=Surface(429,12,10,ButtonC),[432]=Surface(432,12,10,ButtonC),
    };

    static string Row(int i)=>$"""
              <CheckButton Name = "MailInfo0{i}" BasePng = "748" PushPng = "749" Width = "10" Height = "15" HAlign = "center" VAlign = "middle" MouseEvent = "SelectMailItem">
                <Label Name = "From_Label" X = "12" Width = "60" Height = "15" VAlign = "middle" HAlign = "center" Caption = "IU" MouseEvent = "ReadMailEvent"/>
                <Label Name = "Subject_Label" X = "74" Width = "60" Height = "15" VAlign = "middle" HAlign = "center" Caption = "IU" MouseEvent = "ReadMailEvent"/>
                <Label Name = "Date_Label" X= "136" Width = "40" Height = "15" VAlign = "middle" HAlign = "center" Caption = "IU" MouseEvent = "ReadMailEvent"/>
              </CheckButton>
        """;
    static string Scroll(string name)=>$"""
            <ScrollBar Name = "{name}" X = "188" Y = "80" Width = "12" Height = "100" ScrollStart = "10" ScrollHeight = "80" Vertical = "1" MouseEvent = "Scroll">
              <Image Name = "Background_Image" Y = "10" Png = "729;1"/>
              <Button Name = "Up_Button" BaseSurf = "446;1" PushSurf = "447;1" MouseEvent = "ScrollButton"/>
              <Button Name = "Move_Button" Y = "10" BaseSurf = "448;1" MouseEvent = "ScrollButton"/>
              <Button Name = "Down_Button" Y = "90" BaseSurf = "444;1" MouseEvent = "ScrollButton"/>
            </ScrollBar>
        """;
    // Synthetic layout with the element kinds of RenewalMailUI.xml: three windows, close surfs, ComboBox with UpDownList,
    // horizontal button Lists with Gap, a sort List, a vertical MailList of row CheckButtons with labels, ScrollBars,
    // Include, read-only Edits / MultiEdit, prev/next surfs, a missing surf, write Edits with LimitText, pledge check, cost label.
    static string Xml(string listSize="Width = \"200\" Height = \"220\"",string writeWindow="WriteMail_Window")=>$"""
        <?xml version="1.0"?>
        <RenewalMailUI Toggle = "true">
          <Window Name = "MailList_Window" X = "10" Y = "20" {listSize} BasePng = "782;1" Moveable = "30" KeyboardEvent = "CloseMailUI" MouseEvent = "Dummy" Activate = "0">
            <Button Name = "Closeui_Button" X = "180" Y = "5" BaseSurf = "183;1" PushSurf = "184;1" MouseEvent = "CloseMailUI"/>
            <Label Name = "Count_Label" X = "20" Y = "200" Width = "100" HAlign = "right" Caption = "" FontColor = "144,136,123"/>
            <ComboBox Name = "ListType_Combobox" X = "10" Y = "40" Width = "60" Height = "60">
              <Label Name = "Text_Label" Caption = "item00" X = "2" Y = "0" Width = "50" Height = "16" HAlign = "center" VAlign = "middle"/>
              <Button Name = "Expand_Button" X = "0" Y = "2" BasePng = "746" HighlightPng="747" PushPng = "747" MouseEvent = "ComboBox_Expand"/>
              <UpDownList Name = "Expand_List" X = "0" Y = "19" PageSize = "0" Width = "56" Height = "40" Vertical = "1" Gap = "1">
                <Image Name = "Background_Image" X = "0" Y = "0" Png = "750"/>
                <CheckButton Name = "Item1_Button" X = "2" Width = "50" Height = "10" String = "4071" PushPng="751;4" Align = "center" MouseEvent = "ListType_Select"/>
                <CheckButton Name = "Item2_Button" X = "2" Width = "50" Height = "10" String = "4072" PushPng="751;4" Align = "center" MouseEvent = "ListType_Select"/>
                <CheckButton Name = "Item3_Button" X = "2" Width = "50" Height = "10" String = "4073" PushPng="751;4" Align = "center" MouseEvent = "ListType_Select"/>
              </UpDownList>
            </ComboBox>
            <List Name = "ButtonList" X = "100" Y = "40" Width = "90" Height = "10" Horizontal = "1" Gap="1">
              <Button Name = "Write_Button" BasePng = "794" PushPng = "796;1" MouseEvent = "WriteMail"/>
              <Button Name = "Store_Button" BasePng = "806" MouseEvent = "SaveMail"/>
              <Button Name = "Delete_Button" BasePng = "791" MouseEvent = "DeleteMail"/>
              <Button Name = "Exclude_Button" BasePng = "800" MouseEvent = "ExcludeMail"/>
            </List>
            <List Name = "SortList" X = "10" Y = "60" Width = "150" Height = "12" Horizontal = "1">
              <CheckButton Name = "AllSelectBtn" Y="1" BasePng = "748" PushPng = "749" Width = "10" Height = "10" MouseEvent = "AllSelect"/>
              <Button Name = "From_Button" BasePng = "787" PushPng = "787" MouseEvent = "SortMailItem"/>
              <Button Name = "Date_Button" BasePng = "783" PushPng = "783" MouseEvent = "SortMailItem"/>
            </List>
            <List Name = "MailList" X= "10" Y = "80" Width = "180" Height = "45" Vertical = "1">
        {Row(0)}
        {Row(1)}
        {Row(2)}
            </List>
        {Scroll("Userinfo_Scrollbar")}
            <Include Xml="CommunityTab.xml" Name="TabControl" X="27" Y="27" Width="232" Height="24"/>
          </Window>
          <Window Name = "ReadMail_Window" X = "0" Y = "0" Width = "200" Height = "220" BasePng = "811;1" Moveable = "30" KeyboardEvent = "CloseReadMailUI" MouseEvent = "Dummy" Activate = "0">
            <Button Name = "Closeui_Button" X = "180" Y = "5" BaseSurf = "183;1" PushSurf = "184;1" MouseEvent = "CloseReadMailUI"/>
            <List Name = "ReadButton_List" X = "100" Y = "40" Width = "70" Height = "10" Horizontal = "1" Gap="1">
              <Button Name = "Reply_Button" BasePng = "892" MouseEvent = "ReplyMail"/>
              <Button Name = "Store_Button" BasePng = "806" MouseEvent = "SaveMail"/>
              <Button Name = "Delete_Button" BasePng = "791" MouseEvent = "DeleteMail"/>
            </List>
            <Edit Name = "From_Label" X = "60" Y = "60" Width = "120" Height = "15" LimitText = "280" ReadOnly = "1"/>
            <Edit Name = "Subject_Label" X = "60" Y = "76" Width = "120" Height = "15" LimitText = "280" ReadOnly = "1"/>
            <MultiEdit Name = "MailText_Label" X = "20" Y = "95" Width = "150" Height = "100" VAlign = "top" HAlign = "left" ReadOnly = "1" LimitText = "256"/>
            <Button Name="Prev_Button" X="10" Y="40" BaseSurf="429;1" MouseEvent="ReadPrevMail"/>
            <Button Name="Next_Button" X="25" Y="40" BaseSurf="432;1" MouseEvent="ReadNextMail"/>
            <Button Name="Gone_Button" X="40" Y="40" BaseSurf="999" MouseEvent="Nothing"/>
        {Scroll("ReadMail_Scrollbar")}
          </Window>
          <Window Name = "{writeWindow}" X = "0" Y = "0" Width = "200" Height = "220" BasePng = "810;1" Moveable = "30" KeyboardEvent = "CloseWriteMailUI" MouseEvent = "Dummy" Activate = "0">
            <Button Name = "Closeui_Button" X = "180" Y = "5" BaseSurf = "183;1" PushSurf = "184;1" MouseEvent = "CloseWriteMailUI"/>
            <List Name = "WriteButton_List" X = "150" Y = "40" Width = "41" Height = "10" Horizontal = "1" Gap="1">
              <Button Name = "Send_Button" BasePng = "797" MouseEvent = "SendMail"/>
              <Button Name = "Cancel_Button" BasePng = "803" MouseEvent = "CloseWriteMailUI"/>
            </List>
            <Edit Name = "To_Label" X = "60" Y = "60" Width = "120" Height = "15" LimitText = "280" KeyboardEvent = "ToNextFocus"/>
            <Edit Name = "Subject_Label" X = "60" Y = "76" Width = "120" Height = "15" LimitText = "5" KeyboardEvent = "ToNextFocus"/>
            <MultiEdit Name = "MailText_Label" X = "20" Y = "95" Width = "150" Height = "100" VAlign = "top" HAlign = "left" LimitText = "256" KeyboardEvent = "ToNextFocus"/>
            <CheckButton Name = "CheckBox_Button" X = "100" Y = "200" Width = "10" Height = "10" BasePng = "748" PushPng = "749" MouseEvent = "CheckPledgeMail"/>
            <Label Name="Cost_Label" X="20" Y="40" FontColor="150,150,150"/>
          </Window>
        </RenewalMailUI>
        """;
    static UiLayout Layout(string xml)=>UiLayout.Parse(xml,"RenewalMailUI-c.xml");
    static MailPanel Panel(string? xml=null,FakeHost? host=null){
        var p=new MailPanel();var png=Pngs();var surf=Surfs();
        p.Load(Layout(xml??Xml()),id=>surf.GetValueOrDefault(id),id=>png.GetValueOrDefault(id));
        p.Host=host;
        return p;
    }
    static readonly MailStamp Stamp=new(1,1,7);
    static MailHeader H(uint id,string from,string subject,byte y,byte m,byte d)=>new(id,0,y,m,d,from,subject);
    static MailHeader[] Mails()=>[H(11,"Carol","c-sub",26,11,9),H(12,"alice","a-sub",26,11,10),H(13,"Bob","b-sub",0,0,0),H(14,"Dave","d",25,1,2)];
    static MailView View(MailHeader[]? headers=null,uint? reading=null,string body="",MailStamp? stamp=null)=>new(stamp??Stamp,1,headers??Mails(),reading,body,"");

    sealed class FakeHost:IMailPanelHost {
        public MailView? View{get;set;}
        public List<string> Calls{get;}=[];
        public bool Result=true;
        public TaskCompletionSource<bool>? Hold;
        public Task<bool> Run(MailView shown,string command,byte box,uint id,string receiver="",string subject="",string body=""){
            Calls.Add(command is "write" or "clan"?$"{command}:{box}:{receiver}|{subject}|{body}":$"{command}:{box}:{id}");
            return Hold?.Task??Task.FromResult(Result);
        }
    }
    static readonly UiStaticGameState InWorld=new(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at,int delta=0)=>new(k,at,k==UiMouseKind.Wheel?MouseButtons.None:MouseButtons.Left,1,delta,Keys.None);
    static void Click(MailPanel p,int x,int y,UiNullCommands c){var at=new Point(10+x,20+y);p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static UiKeyEvent K(Keys k)=>new(k,true,false);
    static void Type(MailPanel p,string s){foreach(char ch in s)p.Char(ch);}
    static Bitmap Render(MailPanel p){var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();var b=(Bitmap)canvas.Bitmap.Clone();canvas.Dispose();return b;}
    static int Pixel(MailPanel p,int x,int y){using var b=Render(p);return b.GetPixel(10+x,20+y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();
    static MailWindow Win(MailPanel p,string name)=>p.Windows.Single(w=>w.Name==name);
    static MailPiece Named(MailPanel p,string window,string name,int index=-1)=>Win(p,window).Pieces.First(x=>x.Name==name&&(index<0||x.Index==index));

    [WorldModuleTest("mail-panel")]
    static void Run(WorldModuleTestContext t){
        Registration(t);
        Parsing(t);
        Values(t);
        Painting(t);
        ListInput(t);
        ReadInput(t);
        WriteInput(t);
        Lifecycle(t);
        Unavailable(t);
    }

    static void Registration(WorldModuleTestContext t){
        var regs=UiPanelRegistry.Panels.Where(r=>r.Id==MailPanel.PanelId).ToArray();
        t.Check(regs.Length==1&&regs[0].Layout==MailPanel.HostLayout,"RenewalMailUI registered once, on the host layout (Build never needs RenewalMailUI.xml)",string.Join(",",regs.Select(r=>r.Layout)));
        t.Check(!UiPanelRegistry.Errors.Any(e=>e.Contains(nameof(MailPanel))),"no discovery error for MailPanel");
        var panel=regs[0].Create(new UiRoot());
        t.Check(panel is MailPanel&&panel is IUiClosablePanel&&panel is IUiMovablePanel&&panel is IUiTextInput&&panel.Layer==UiLayer.Window,"window layer, closable, movable, text input");
        t.Check(!panel.Visible&&((MailPanel)panel).Unavailable!=null&&!((MailPanel)panel).Open(),"closed and unavailable until loaded");
        t.Check(typeof(ClientForm).GetMethod("NativeMailPanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.ReturnType==typeof(MailPanel),"ClientForm.NativeMailPanel (MailUi.cs native split) exists");
    }

    static void Parsing(WorldModuleTestContext t){
        var p=Panel();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Equal("RenewalMailUI-c.xml",p.LayoutFile,"layout file recorded");
        t.Equal("MailList_Window,ReadMail_Window,WriteMail_Window",string.Join(",",p.Windows.Select(w=>w.Name)),"three windows in XML order");
        t.Equal(new Point(10,20),p.Origin,"origin = MailList_Window X/Y");
        t.Equal(new Size(200,220),Win(p,MailPanel.ListWindow).Size,"window size = declared Width/Height");
        t.Equal(30,Win(p,MailPanel.ListWindow).MoveableHeight,"Moveable=30");
        t.Equal("CloseWriteMailUI",Win(p,MailPanel.WriteWindow).KeyboardEvent,"KeyboardEvent kept");
        t.Check(p.Missing.Contains("surf 999 (RenewalMailUI/Window[1]/Button[3])"),"missing surf listed with its path",string.Join(" | ",p.Missing));
        t.Check(!p.Missing.Any(m=>m.Contains("183")||m.Contains("184")),"close surfs 183/184 resolved through the surf source");
        t.Check(p.Skipped.Any(s=>s.Contains("Include[0]")&&s.Contains("CommunityTab.xml")),"Include (CommunityTab.xml) not built, listed",string.Join(" | ",p.Skipped));
        t.Check(Named(p,MailPanel.ListWindow,"Closeui_Button") is {Image:{Id:183,IsSurf:true},PushImage:{Id:184}},"close button: BaseSurf 183 / PushSurf 184");
        t.Equal(new Point(121,40),Named(p,MailPanel.ListWindow,"Store_Button").Offset,"horizontal List: second button after 20 px + Gap 1");
        t.Equal(new Point(142,40),Named(p,MailPanel.ListWindow,"Delete_Button").Offset,"third button");
        t.Equal(new Point(10,61),Named(p,MailPanel.ListWindow,"AllSelectBtn").Offset,"AllSelect keeps its Y=1");
        t.Equal(new Point(20,60),Named(p,MailPanel.ListWindow,"From_Button").Offset,"sort button after the declared Width 10");
        var rows=Win(p,MailPanel.ListWindow).Pieces.Where(x=>x.Group=="row"&&x.Element=="CheckButton").ToArray();
        t.Equal("0,1,2",string.Join(",",rows.Select(r=>r.Index)),"three mail rows");
        t.Equal(new Point(10,95),rows[1].Offset,"vertical List: row 1 at Y+15");
        t.Equal(new Point(22,95),Named(p,MailPanel.ListWindow,"From_Label",1).Offset,"row label relative to its row");
        t.Equal("ReadMailEvent",Named(p,MailPanel.ListWindow,"Date_Label",2).MouseEvent,"row label keeps ReadMailEvent");
        t.Equal(new Point(10,59),Win(p,MailPanel.ListWindow).Pieces.Single(x=>x.Group=="combo"&&x.Kind==MailPieceKind.Image).Offset,"combo background at the UpDownList origin, not stacked");
        t.Equal(new Point(12,70),Named(p,MailPanel.ListWindow,"Item2_Button").Offset,"combo items stacked by Height 10 + Gap 1");
        t.Equal(1,Named(p,MailPanel.ListWindow,"Item2_Button").Index,"combo item index");
        t.Check(Named(p,MailPanel.ListWindow,"Text_Label").Group=="combo-text","combo text label");
        t.Check(Named(p,MailPanel.ListWindow,"Count_Label").Color==Color.FromArgb(144,136,123),"FontColor kept");
        t.Check(Win(p,MailPanel.ListWindow) is {ScrollStart:10,ScrollHeight:80},"ScrollStart/ScrollHeight kept");
        t.Check(Named(p,MailPanel.ReadWindow,"MailText_Label") is {Kind:MailPieceKind.Edit,Multiline:true,ReadOnly:true},"read body = read-only MultiEdit");
        t.Check(Named(p,MailPanel.WriteWindow,"Subject_Label") is {Kind:MailPieceKind.Edit,Limit:5,ReadOnly:false},"write subject Edit with LimitText");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("<RenewalMailUI Toggle = \"true\">","<Other>").Replace("</RenewalMailUI>","</Other>")),"wrong root → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Panel(Xml(writeWindow:"ReadMail_Window")),"duplicate window → UiLayoutException");
        t.Throws<UiLayoutException>(()=>Panel(Xml(writeWindow:"Other_Window")),"missing WriteMail_Window → UiLayoutException");
    }

    static void Values(WorldModuleTestContext t){
        var host=new FakeHost{View=View()};
        var p=Panel(host:host);
        t.Check(p.Open()&&p.Visible,"opens");
        t.Equal("list:0:0",string.Join(",",host.Calls),"open asks for the list of box 0");
        t.Equal(MailPanel.ListWindow,p.Active?.Name,"list window on open");
        t.Equal("Carol",p.LabelText(Named(p,MailPanel.ListWindow,"From_Label",0)),"row 0 sender");
        t.Equal("a-sub",p.LabelText(Named(p,MailPanel.ListWindow,"Subject_Label",1)),"row 1 subject");
        t.Equal("26/11/09",p.LabelText(Named(p,MailPanel.ListWindow,"Date_Label",0)),"date yy/mm/dd");
        t.Equal("",p.LabelText(Named(p,MailPanel.ListWindow,"Date_Label",2)),"no date (month 0) → blank");
        t.Equal("4",p.LabelText(Named(p,MailPanel.ListWindow,"Count_Label")),"count label = mails");
        t.Equal("一般",p.LabelText(Named(p,MailPanel.ListWindow,"Text_Label")),"combo text = box name");
        t.Equal("保管箱",p.ButtonText(Named(p,MailPanel.ListWindow,"Item3_Button")),"combo item 3 built-in name");
        p.TextResolver=(k,v)=>k=="String"&&v=="4071"?"一般信":null;
        t.Equal("一般信",p.LabelText(Named(p,MailPanel.ListWindow,"Text_Label")),"String 4071 through TextResolver");
        t.Equal("郵資 50 金幣",p.LabelText(Named(p,MailPanel.WriteWindow,"Cost_Label")),"postage 50");
        t.Check(p.LabelText(Named(p,MailPanel.ListWindow,"From_Label",0)) is not "IU","Caption placeholder never drawn");
    }

    static void Painting(WorldModuleTestContext t){
        var host=new FakeHost{View=View()};
        var p=Panel(host:host);p.Open();
        t.Equal(Argb(ListBaseC),Pixel(p,110,150),"list base png at origin");
        t.Equal(Argb(CloseC),Pixel(p,182,7),"close surf 9183 slot drawn");
        t.Equal(Argb(CheckC),Pixel(p,12,82),"row 0 check box");
        var c=new UiNullCommands();
        Click(p,12,82,c);
        t.Equal(Argb(CheckPushC),Pixel(p,12,82),"checked row → push image");
        t.Equal(Argb(ThumbC),Pixel(p,190,92),"scroll thumb at the top");
        Click(p,190,172,c);
        t.Equal(1,p.ListScroll,"down button scrolls one row (4 mails, 3 rows)");
        t.Equal(Argb(ThumbC),Pixel(p,190,162),"thumb at the bottom of the track (10 + 70)");
        t.Equal(Argb(CheckC),Pixel(p,12,82),"row 0 now shows mail 2 (not checked)");
        var two=Panel(host:new FakeHost{View=View(Mails()[..2])});two.Open();
        t.Equal(Argb(ListBaseC),Pixel(two,12,112),"empty row not drawn");
        Click(p,12,42,c);
        t.Check(p.ComboExpanded,"expand button opens the combo list");
        t.Equal(Argb(ComboItemC),Pixel(p,13,60),"selected box item drawn with push image");
        t.Equal(Argb(ComboBackC),Pixel(p,13,71),"combo background under the other items");
        p.ShowWindow(MailPanel.ReadWindow);
        t.Equal(Argb(ReadBaseC),Pixel(p,42,42),"missing surf draws nothing (no substitute)");
        t.Equal(Argb(CloseC),Pixel(p,182,7),"read window close surf");
    }

    static void ListInput(WorldModuleTestContext t){
        var host=new FakeHost{View=View()};var c=new UiNullCommands();
        var p=Panel(host:host);p.Open();host.Calls.Clear();
        Click(p,12,82,c);Click(p,12,112,c);
        t.Equal("11,13",string.Join(",",p.Checked.OrderBy(x=>x)),"rows 0 and 2 checked");
        Click(p,121+2,42,c);
        t.Equal("store:0:11,store:0:13",string.Join(",",host.Calls),"store sends the checked mails in row order");
        t.Equal(0,p.Checked.Count,"checks cleared after the sequence");
        host.Calls.Clear();
        Click(p,12,62,c);
        t.Equal(4,p.Checked.Count,"AllSelect checks every mail");
        Click(p,142+2,42,c);
        t.Equal("delete:0:11,delete:0:12,delete:0:13,delete:0:14",string.Join(",",host.Calls),"delete sends every checked mail");
        Click(p,12,62,c);Click(p,12,62,c);
        t.Equal(0,p.Checked.Count,"AllSelect twice clears");
        Click(p,25,62,c);
        t.Equal("alice,Bob,Carol,Dave",string.Join(",",p.Rows.Select(r=>r.Sender)),"sort by sender");
        Click(p,25,62,c);
        t.Equal("Dave,Carol,Bob,alice",string.Join(",",p.Rows.Select(r=>r.Sender)),"same sort button again → reversed");
        Click(p,55,62,c);
        t.Equal("13,14,11,12",string.Join(",",p.Rows.Select(r=>r.Id)),"sort by date (no date first)");
        host.Calls.Clear();
        Click(p,12,42,c);Click(p,15,84,c);
        t.Check(!p.ComboExpanded&&p.Box==2,"third combo item selects box 2 and closes the list");
        t.Equal("list:2:0",string.Join(",",host.Calls),"box change asks for its list");
        p.Mouse(M(UiMouseKind.Wheel,new(10+100,20+100),-120),c);
        t.Equal(1,p.ListScroll,"wheel down scrolls");
        p.Mouse(M(UiMouseKind.Wheel,new(10+100,20+100),120),c);
        t.Equal(0,p.ListScroll,"wheel up scrolls back");
        Click(p,163+2,42,c);
        t.Check(p.Unhandled.Contains("ExcludeMail"),"ExcludeMail reported unhandled");
        t.Check(c.Calls.Count==0,"no dev-tools / command calls");
    }

    static void ReadInput(WorldModuleTestContext t){
        var host=new FakeHost{View=View()};var c=new UiNullCommands();
        var p=Panel(host:host);p.Open();host.Calls.Clear();
        Click(p,22+5,95+5,c);
        t.Check(p.Active?.Name==MailPanel.ReadWindow&&p.ReadingId==12,"click on row 1 → read window for mail 12");
        t.Equal("read:0:12",string.Join(",",host.Calls),"read request");
        t.Equal("alice",p.EditText(Named(p,MailPanel.ReadWindow,"From_Label")),"sender in the read Edit");
        t.Equal("",p.EditText(Named(p,MailPanel.ReadWindow,"MailText_Label")),"body blank until the server sends it");
        host.View=View(reading:12,body:"hello\nworld");p.Update(InWorld);
        t.Equal("hello\nworld",p.EditText(Named(p,MailPanel.ReadWindow,"MailText_Label")),"body after S186 read");
        using(var bmp=Render(p)){
            bool text=false;for(int x=10+20;x<10+170;x++)for(int y=20+95;y<20+125;y++)if(bmp.GetPixel(x,y).ToArgb()!=Argb(ReadBaseC))text=true;
            t.Check(text,"body text drawn inside the MultiEdit");
        }
        host.Calls.Clear();
        Click(p,25+2,42,c);
        t.Check(p.ReadingId==13&&host.Calls.SequenceEqual(new[]{"read:0:13"}),"next mail",string.Join(",",host.Calls));
        Click(p,10+2,42,c);
        t.Equal(12u,p.ReadingId,"previous mail");
        Click(p,25,60,c);
        t.Check(p.Active?.Name==MailPanel.ReadWindow&&p.Focus==null,"read-only Edit takes no focus");
        host.Calls.Clear();
        Click(p,142+2,42,c);
        t.Check(host.Calls.SequenceEqual(new[]{"delete:0:12"})&&p.Active?.Name==MailPanel.ListWindow,"delete the mail being read, back to the list",string.Join(",",host.Calls));
        Click(p,22+5,80+5,c);
        Click(p,100+2,42,c);
        t.Check(p.Active?.Name==MailPanel.WriteWindow&&p.To=="Carol"&&p.Focus==MailPanel.SubjectEdit,"reply → write window to the sender, focus on subject");
        Click(p,182,7,c);
        t.Equal(MailPanel.ListWindow,p.Active?.Name,"CloseWriteMailUI → back to the list");
        Click(p,22+5,80+5,c);
        p.Key(K(Keys.Escape),c);
        t.Check(p.Visible&&p.Active?.Name==MailPanel.ListWindow,"Esc on the read window (CloseReadMailUI) → list");
        p.Key(K(Keys.Escape),c);
        t.Check(!p.Visible,"Esc on the list (CloseMailUI) closes");
    }

    static void WriteInput(WorldModuleTestContext t){
        var host=new FakeHost{View=View()};var c=new UiNullCommands();
        var p=Panel(host:host);p.Open();host.Calls.Clear();
        Click(p,100+2,42,c);
        t.Check(p.Active?.Name==MailPanel.WriteWindow&&p.Focus==MailPanel.ToEdit,"write → empty draft, focus on To");
        t.Check(!p.Key(K(Keys.A),c),"printable key left to WM_CHAR (Char)");
        Type(p,"Bob");
        t.Check(p.Key(K(Keys.Left),c)&&p.Key(K(Keys.Up),c),"arrows taken while editing (never reach the game)");
        p.Key(K(Keys.Tab),c);
        t.Equal(MailPanel.SubjectEdit,p.Focus,"Tab → next edit");
        Type(p,"hello world");
        t.Equal("hello",p.Subject,"LimitText 5 on the subject");
        p.Key(K(Keys.Enter),c);
        t.Equal(MailPanel.BodyEdit,p.Focus,"Enter on a single-line Edit → next (ToNextFocus)");
        Type(p,"hi");p.Key(K(Keys.Enter),c);Type(p,"yo!");p.Key(K(Keys.Back),c);
        t.Equal("hi\nyo",p.Body,"Enter in the body = new line; Back deletes");
        Click(p,25,62,c);
        t.Equal(null,p.Focus,"click on empty area drops focus");
        t.Check(!p.Char('x')&&p.Body=="hi\nyo","no focus → characters not taken");
        Click(p,100+2,202,c);
        t.Check(p.Clan&&p.LabelText(Named(p,MailPanel.WriteWindow,"Cost_Label"))=="郵資 1000 金幣","pledge check → clan mail, postage 1000");
        t.Equal(Argb(CheckPushC),Pixel(p,100+2,202),"pledge check drawn checked");
        Click(p,25+40,65,c);
        t.Equal(MailPanel.ToEdit,p.Focus,"click on an Edit focuses it");
        Click(p,150+2,42,c);
        t.Equal("clan:0:Bob|hello|hi\nyo",string.Join(",",host.Calls),"send → clan write with the draft");
        t.Check(p.Submitted&&p.Active?.Name==MailPanel.ListWindow,"sent → back to the list");
        host.Calls.Clear();
        Click(p,100+2,42,c);
        t.Check(!p.Submitted&&p.To==""&&!p.Clan,"new draft after writing again");
        Click(p,150+2,42,c);
        t.Check(host.Calls.Count==0&&p.Error!=null,"empty recipient → not sent, error kept",p.Error);
        Type(p,"Bob");host.Result=false;
        Click(p,150+2,42,c);
        t.Check(host.Calls.SequenceEqual(new[]{"write:0:Bob||"})&&!p.Submitted&&p.Active?.Name==MailPanel.WriteWindow,"server path refused → stays on the draft",string.Join(",",host.Calls));
        host.Result=true;host.Calls.Clear();
        host.Hold=new TaskCompletionSource<bool>();
        var context=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);   // the continuation runs inline on SetResult (no message pump here)
        try{
            Click(p,150+2,42,c);
            t.Check(p.Busy,"busy while the send is pending");
            Type(p,"zz");Click(p,150+2,42,c);
            t.Check(p.To=="Bob"&&host.Calls.Count==1,"no typing and no second send while busy",string.Join(",",host.Calls));
            host.Hold.SetResult(true);
        }finally{SynchronizationContext.SetSynchronizationContext(context);}
        t.Check(!p.Busy&&p.Submitted,"completed send marks the draft submitted");
        Click(p,100+2,42,c);
        p.Key(K(Keys.Escape),c);
        t.Equal(MailPanel.ListWindow,p.Active?.Name,"Esc while editing → CloseWriteMailUI → list");
    }

    static void Lifecycle(WorldModuleTestContext t){
        var host=new FakeHost{View=View()};var c=new UiNullCommands();
        var p=Panel(host:host);p.Open();
        t.Check(p.IsDragHandle(new(10+50,20+5))&&!p.IsDragHandle(new(10+50,20+40))&&!p.IsDragHandle(new(10+182,20+7)),"drag strip = Moveable 30 px, not over the close button");
        p.MoveTo(new(300,200));
        t.Equal(new Rectangle(300,200,200,220),p.Bounds,"moved");
        p.MoveTo(new(10,20));
        host.View=View(stamp:new MailStamp(1,2,7));p.Update(InWorld);
        t.Check(!p.Visible,"mailbox identity changed (new world epoch) → closed");
        p.Open();p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible&&p.View==null,"leaving the world closes and clears");
        Click(p,182,7,c);
        var root=new UiRoot();root.Add(p);
        t.Equal(UiToggleResult.Opened,root.Toggle(p,()=>p.Open()),"UiRoot.Toggle opens");
        t.Equal(UiToggleResult.Closed,root.Toggle(p,()=>p.Open()),"UiRoot.Toggle again closes");
        p.Open();Click(p,182,7,c);
        t.Check(!p.Visible,"CloseMailUI closes");
        var offline=Panel();
        t.Check(offline.Open()&&offline.View==null&&offline.Rows.Count==0,"no host → opens on an empty list and sends nothing");
    }

    static void Unavailable(WorldModuleTestContext t){
        t.Throws<UiLayoutException>(()=>Panel(Xml(listSize:"").Replace("BasePng = \"782;1\"","BasePng = \"9999\"")),"no size and no base image → UiLayoutException");
        var borrowed=Panel(Xml(listSize:""));
        t.Equal(new Size(200,220),Win(borrowed,MailPanel.ListWindow).Size,"no Width/Height → base image size");
        var noBase=Panel(Xml().Replace("BasePng = \"782;1\"","BasePng = \"9999\""));
        t.Check(noBase.Available&&noBase.Missing.Any(m=>m.StartsWith("png 9999")),"missing base with declared size: usable, base listed missing");
        var empty=new MailPanel();
        t.Check(!empty.Available&&!empty.Open(),"never loaded → unavailable");
    }
}
