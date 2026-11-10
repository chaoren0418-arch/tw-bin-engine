namespace P06.NativeUi;

// T327 (#765) select-char-ui / create-char-ui 離線測試（選角與建角畫面）：版面 XML 與圖片全部是記憶體內合成資料，元素種類仿
// SelectUI.xml / CreateUI.xml，不是真實檔案；不讀真實客戶端或參考客戶端、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class SelectCharPanelTests {
    const uint BaseC=0xFF101010,MsgBaseC=0xFF202020,MsgBackC=0xFF303030,OkC=0xFF0000FF,CancelC=0xFF00FF00,SlotBackC=0xFF404040,SelectC=0xFF0000C0,SelectHiC=0xFF0000E0,
        SelectPushC=0xFF000060,CreateC=0xFF00C000,LockBackC=0xFF505050,LockC=0xFF606060,WaitC=0xFFFF0000,EnterC=0xFFFFFF00,EnterHiC=0xFFC0C000,DeleteC=0xFFFF00FF,
        UpC=0xFF00FFFF,DownC=0xFF008080,BackC=0xFF808000,InfoC=0xFF252525;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Dictionary<int,UiSurface> SelectSurfs()=>new(){
        [421]=Surface(421,400,300,BaseC),[488]=Surface(488,200,100,MsgBaseC),[487]=Surface(487,200,100,MsgBackC),[489]=Surface(489,30,10,OkC),[492]=Surface(492,30,10,CancelC),
        [429]=Surface(429,120,40,SlotBackC),[430]=Surface(430,120,40,SelectC),[431]=Surface(431,120,40,SelectHiC),[432]=Surface(432,120,40,SelectPushC),[426]=Surface(426,120,40,CreateC),
        [425]=Surface(425,120,40,LockBackC),[422]=Surface(422,30,20,LockC),[486]=Surface(486,10,10,WaitC),[404]=Surface(404,40,20,EnterC),[405]=Surface(405,40,20,EnterHiC),
        [407]=Surface(407,40,20,DeleteC),[411]=Surface(411,20,20,UpC),[413]=Surface(413,20,20,DownC),[415]=Surface(415,40,20,BackC),[410]=Surface(410,120,110,InfoC),
        // R9-O (#859): slot buttons use the class surf 6430 + 8×class + 4×sex (prince male 6430–6432; knight male pending 6441).
        [6430]=Surface(6430,120,40,SelectC),[6431]=Surface(6431,120,40,SelectHiC),[6432]=Surface(6432,120,40,SelectPushC),[6441]=Surface(6441,120,40,SelectC),
    };

    // Synthetic layout with the element kinds of SelectUI.xml: the message window, the select window with an Effect, a vertical
    // BigSlot_List of 2 Slot_Controls (Image → Select/WaitDelete/Create buttons with nested labels, image and tooltip; lock back
    // + lock button), the info List, Up/Down/Enter/Back/Delete, backgrounds last, and a name-change window that is not built.
    const string Slot="""
              <Control Name = "Slot_Control" Width = "120" Height = "50">
                <Image Name = "Slot_Back" Surf = "429;3">
                  <Button Name = "Select_Button" BaseSurf = "430;1" HighlightSurf = "431;1" PushSurf = "432;1" MouseEvent = "SelectChar_SelectWin">
                    <Label Name = "Level_Label" X = "40" Y = "10" String = "dummy;1" Align = "left" Width = "20" Height = "17"/>
                    <Label Name = "Name_Label" X = "60" Y = "10" String = "dummy;1" Align = "left" Width = "50" Height = "17"/>
                  </Button>
                  <Button Name = "WaitDelete_Button" BaseSurf = "430;1" HighlightSurf = "431;1" PushSurf = "432;1" MouseEvent = "SelectChar_SelectWin">
                    <Label Name = "Level_Label" X = "40" Y = "10" String = "dummy;1" Align = "left" Width = "20" Height = "17"/>
                    <Label Name = "Name_Label" X = "60" Y = "10" String = "dummy;1" Align = "left" Width = "50" Height = "17"/>
                    <Image Name = "WaitToDel_Image" X = "100" Y = "5" Surf = "486;1"/>
                    <Tooltip Name = "Desc_Tooltip" X = "10" Y = "-6" Width = "300" String = "3912"/>
                  </Button>
                  <Button Name = "Create_Button" BaseSurf = "426;1" HighlightSurf = "427;1" PushSurf = "428;1" MouseEvent = "CreateChar_SelectWin">
                    <Tooltip Name = "Desc_Tooltip" X = "10" Y = "-6" Width = "300" String = "3911"/>
                  </Button>
                </Image>
                <Image Name = "Slot_Lock_Back" Surf = "425;3">
                  <Button Name = "Lock_Button" BaseSurf = "422;1" MouseEvent = "SlotLock_SelectWin"/>
                </Image>
              </Control>
    """;
    static string Info(string name)=>$"""<Label Name = "{name}" Align = "left" Width = "100" Height = "20"/>""";
    static string MessageWindow(string msgBase)=>$"""
  <Window Name = "Message_Window" X = "100" Y = "100" Modal = "1" Top = "1" DarkBack = "1" Activate = "0" {msgBase}>
    <MultiLabel Name = "Message_Label" X = "10" Y = "10" Width = "180" Height = "50" VAlign = "center" LineMargin = "3"/>
    <Button Name = "Ok_Button" X = "20" Y = "80" BaseSurf = "489" HighlightSurf = "490" PushSurf = "491" MouseEvent = "Ok_MessageWin"/>
    <Button Name = "Cancel_Button" X = "150" Y = "80" BaseSurf = "492" MouseEvent = "Cancel_MessageWin"/>
    <Image Name = "Back_Image" Surf = "487;1"/>
  </Window>

""";
    static string MainWindow(string slots)=>$"""
  <Window Name = "Character_Select_Window" X = "0" Y = "0" BaseSurf = "421" Activate = "0">
    <Effect Name = "Character_Ani" X = "300" Y = "50"/>
    <List Name = "BigSlot_List" X = "10" Y = "10" Width = "100" Height = "200" Vertical = "1">
{slots}
    </List>
    <List Name = "CharacterInfo_List" X = "250" Y = "200" Width = "100" Height = "120" Vertical = "1">
      {Info("Name_Label")}{Info("Level_Label")}{Info("Class_Label")}{Info("Pledge_Label")}{Info("Alignment_Label")}{Info("Birthday_Label")}
    </List>
    <Image Name = "DeleteDelay_Image" X = "300" Y = "50"/>
    <Button Name = "Up_Button" X = "150" Y = "20" BaseSurf = "411;4" HighlightSurf = "411;4" PushSurf = "412;4" MouseEvent = "UpSlot_SelectWin"/>
    <Button Name = "Down_Button" X = "150" Y = "60" BaseSurf = "413;4" HighlightSurf = "413;4" PushSurf = "414;4" MouseEvent = "DownSlot_SelectWin"/>
    <Button Name = "Enter_Button" X = "250" Y = "150" BaseSurf = "404" HighlightSurf = "405" PushSurf = "406" MouseEvent = "Ok_SelectWin"/>
    <Button Name = "Back_Button" X = "10" Y = "270" BaseSurf = "415;4" MouseEvent = "Cancel_SelectWin"/>
    <Button Name = "Delete_Button" X = "100" Y = "270" BaseSurf = "407" HighlightSurf = "408" PushSurf = "409" MouseEvent = "DeleteChar_SelectWin"/>
    <Image Name = "CharacterInfo_Back" X = "240" Y = "190" Surf = "410"/>
    <Image Name = "Back_Image" X = "0" Y = "0"/>
  </Window>

""";
    const string NameWindow="""
  <Window Name = "CharacterName_Change_Window" X = "0" Y = "0" Activate = "0">
    <Image Name = "Edit_Back" X = "492" Y = "460" Surf = "989"/>
  </Window>

""";
    static string SelectXml(string root="SelectUI",string msgBase="BaseSurf = \"488;3\"",string slots=Slot+"\n"+Slot,bool main=true,bool message=true)=>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<"+root+">\n"+(message?MessageWindow(msgBase):"")+(main?MainWindow(slots):"")+NameWindow+"</"+root+">\n";
    static SelectCharPanel Select(string? xml=null,CreateCharPanel? create=null){
        var p=new SelectCharPanel();var s=SelectSurfs();
        p.Load(UiLayout.Parse(xml??SelectXml(),"SelectUI.xml"),id=>s.GetValueOrDefault(id));
        p.Create=create;
        return p;
    }

    internal sealed class Host:ICharSelectHost {
        public CharSelectView View;
        public readonly List<string> Calls=[];
        public LoginAction Result=new(true,"請求已送出，等待伺服器確認。");
        public Func<CharSelectView,string,CharSelectView>? AfterDelete;
        public Func<CharSelectView,CharacterCreation,CharSelectView>? AfterCreate;
        public CharacterCreation? Created;public int Fallbacks,Logouts;
        public Host(CharSelectView v){View=v;}
        public CharSelectView Snapshot()=>View;
        public Task<LoginAction> Enter(CharSelectView stamp,string name){Calls.Add($"enter:{name}:{stamp.Revision}");return Task.FromResult(Result);}
        public Task<LoginAction> Create(CharSelectView stamp,CharacterCreation request){
            Calls.Add($"create:{request.Name}:{stamp.Revision}");Created=request;
            if(Result.Accepted&&AfterCreate!=null)View=AfterCreate(View,request);
            return Task.FromResult(Result);
        }
        public Task<LoginAction> Delete(CharSelectView stamp,string name){
            Calls.Add($"delete:{name}:{stamp.Revision}");
            if(Result.Accepted&&AfterDelete!=null)View=AfterDelete(View,name);
            return Task.FromResult(Result);
        }
        public void Logout()=>Logouts++;
        public void CreateFallback()=>Fallbacks++;
    }
    internal static LoginCharacter Char(string name,byte cls=0,byte level=10)=>new(name,"測試盟",cls,0,-5,30,10,0,level,13,10,10,11,13,10,0,20261110,0);
    internal static CharSelectView ViewOf(int slots,params LoginCharacter[] cs)=>new(1,3,LoginState.CharacterList,cs,slots,cs.Length>0,cs.Length<slots,cs.Length>0,"");

    internal static UiMouseEvent M(UiMouseKind k,Point at,int clicks=1)=>new(k,at,MouseButtons.Left,clicks,0,Keys.None);
    internal static void Click(IUiPanel p,Point at,UiNullCommands c,int clicks=1){p.Mouse(M(UiMouseKind.Down,at,clicks),c);p.Mouse(M(UiMouseKind.Up,at,clicks),c);}
    internal static UiKeyEvent K(Keys k)=>new(k,true,false);
    internal static Bitmap Render(IUiPanel p){var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();var b=(Bitmap)canvas.Bitmap.Clone();canvas.Dispose();return b;}
    internal static int Pixel(IUiPanel p,int x,int y){using var b=Render(p);return b.GetPixel(x,y).ToArgb();}
    internal static bool Near(Color c,uint v){var e=Color.FromArgb(unchecked((int)v));return Math.Abs(c.R-e.R)<=2&&Math.Abs(c.G-e.G)<=2&&Math.Abs(c.B-e.B)<=2;}
    internal static bool Near(int argb,uint v)=>Near(Color.FromArgb(argb),v);
    internal static bool Inked(Bitmap b,Rectangle r,uint background){for(int x=r.Left;x<r.Right;x++)for(int y=r.Top;y<r.Bottom;y++)if(!Near(b.GetPixel(x,y),background))return true;return false;}

    [WorldModuleTest("select-char-ui")]
    static void RunSelect(WorldModuleTestContext t){
        Registration(t);
        SelectParsing(t);
        SelectPainting(t);
        SelectActions(t);
        SelectDelete(t);
        SelectBroken(t);
    }

    static void Registration(WorldModuleTestContext t){
        foreach(var (id,type) in new[]{(SelectCharPanel.PanelId,typeof(SelectCharPanel)),(CreateCharPanel.PanelId,typeof(CreateCharPanel))}){
            var regs=UiPanelRegistry.Panels.Where(r=>r.Id==id).ToArray();
            t.Check(regs.Length==1&&regs[0].Layout=="uistatus86",$"{id} registered once, on the host layout (Build never needs the reference XML)",string.Join(",",regs.Select(r=>r.Layout)));
            t.Check(!UiPanelRegistry.Errors.Any(e=>e.Contains(type.Name)),$"no discovery error for {type.Name}");
            var panel=regs[0].Create(new UiRoot());
            t.Check(panel.GetType()==type&&panel is IUiClosablePanel&&panel.Layer==UiLayer.Float&&!panel.Visible,$"{id}: closable float screen, hidden until a host is attached");
        }
        var root=new UiRoot();
        var select=(SelectCharPanel)UiPanelRegistry.Panels.Single(r=>r.Id==SelectCharPanel.PanelId).Create(root);
        var create=(CreateCharPanel)UiPanelRegistry.Panels.Single(r=>r.Id==CreateCharPanel.PanelId).Create(root);
        root.Add(select);root.Add(create);
        t.Check(ReferenceEquals(select.Create,create),"select screen finds the create screen on the root");
        t.Check(create is IUiTextInput,"create screen takes typed characters (Username_Edit)");
        select.Attach(new Host(ViewOf(4,Char("甲"))));
        t.Check(!select.Available&&!select.Visible,"unavailable (not loaded) → never shown, even with a host");
        var empty=new SelectCharPanel();empty.Load(UiLayout.Parse(SelectXml(),"SelectUI.xml"),(SurfCatalog)null!);
        t.Check(!empty.Available&&empty.Unavailable=="沒有 UiResources","product Load without UiResources → unavailable (WinForms path stays)");
        var empty2=new CreateCharPanel();empty2.Load(UiLayout.Parse("<CreateUI/>","CreateUI.xml"),(SurfCatalog)null!);
        t.Check(!empty2.Available&&empty2.Unavailable=="沒有 UiResources","create: product Load without UiResources → unavailable");
    }

    static void SelectParsing(WorldModuleTestContext t){
        var p=Select();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Equal("SelectUI.xml",p.LayoutFile,"layout file recorded");
        t.Equal(2,p.Rows,"two Slot_Controls = two visible rows");
        t.Equal(new Rectangle(0,0,400,300),p.Main!.Bounds,"select window: X/Y + BaseSurf size");
        t.Equal(new Rectangle(100,100,200,100),p.Message!.Bounds,"message window: X/Y + BaseSurf size (flag ;3 ignored)");
        var m=p.Main;
        var slot1=m.Pieces.First(x=>x.Name=="Select_Button"&&x.Slot==1);
        t.Equal(new Point(10,60),slot1.Offset,"List stacks Slot_Controls by Height: second row at Y 10+50");
        t.Equal("Slot_Back",slot1.Parent?.Name,"button nested in Slot_Back");
        t.Equal(new Point(50,70),m.Pieces.First(x=>x.Name=="Level_Label"&&x.Slot==1&&x.Parent?.Name=="Select_Button").Offset,"label relative to its button");
        t.Equal(new Point(110,15),m.Pieces.First(x=>x.Name=="WaitToDel_Image"&&x.Slot==0).Offset,"WaitToDel image relative to WaitDelete_Button");
        t.Equal(new Point(250,240),m.Pieces.First(x=>x.ListName==SelectCharPanel.InfoList&&x.Name=="Class_Label").Offset,"info List: third label at 200+2×20");
        int Index(string n)=>m.Pieces.ToList().FindIndex(x=>x.Name==n);
        t.Check(Index("CharacterInfo_Back")<Index("Enter_Button"),"later XML sibling (background) paints first");
        t.Check(m.Pieces.ToList().IndexOf(slot1.Parent!)<m.Pieces.ToList().IndexOf(slot1),"a parent paints before its children");
        t.Check(p.Skipped.Any(s=>s.Contains("Effect")),"Character_Ani effect skipped",string.Join(" | ",p.Skipped));
        t.Check(p.Skipped.Any(s=>s.Contains("Image[")&&s.Contains("沒有圖號")),"images without an id (DeleteDelay/Back_Image) skipped");
        t.Check(p.Skipped.Any(s=>s.Contains("CharacterName_Change_Window")),"name change window not built");
        t.Check(p.Skipped.Count(s=>s.Contains("Tooltip"))==4,"tooltips skipped (2 per slot)");
        t.Check(!p.Missing.Any(x=>x.Contains("989")),"surfs of skipped windows never looked up",string.Join(" | ",p.Missing));
        t.Check(p.Missing.Any(x=>x.StartsWith("surf 408 "))&&p.Missing.Any(x=>x.StartsWith("surf 427 ")),"missing highlight surfs listed with their path",string.Join(" | ",p.Missing));
        t.Check(p.Main.Base!=null&&p.Main.Base.Surf==421,"window BaseSurf resolved");
    }

    static void SelectPainting(WorldModuleTestContext t){
        var p=Select();var c=new UiNullCommands();
        var host=new Host(ViewOf(4,Char("甲",0,52),Char("乙",33,40),Char("丙",2,7)));
        t.Check(!p.Visible,"no host → hidden");
        p.Attach(host);
        t.Check(p.Visible&&p.Bounds==new Rectangle(0,0,400,300),"host on the character list → shown full screen");
        t.Equal("甲",p.Selected,"first character selected by default");
        t.Equal(4,p.TotalRows,"rows = max(visible rows, slots, characters)");t.Equal(2,p.MaxScroll,"scroll range");
        t.Check(p.Row(0)==SelectCharPanel.RowKind.Character&&p.Row(1)==SelectCharPanel.RowKind.Pending&&p.Row(3)==SelectCharPanel.RowKind.Empty&&p.Row(4)==SelectCharPanel.RowKind.Locked,"row kinds");
        using(var b=Render(p)){
            t.Check(Near(b.GetPixel(395,295),BaseC),"window base surf drawn",b.GetPixel(395,295));
            t.Check(Near(b.GetPixel(12,12),SelectPushC),"selected character's Select_Button uses PushSurf",b.GetPixel(12,12));
            t.Check(Near(b.GetPixel(12,62),SelectC),"7-day pending character → WaitDelete_Button",b.GetPixel(12,62));
            t.Check(Near(b.GetPixel(112,67),WaitC),"…with the WaitToDel image above it");
            t.Check(!Near(b.GetPixel(112,17),WaitC),"no WaitToDel image on a normal character");
            t.Check(Inked(b,new Rectangle(50,20,70,17),SelectPushC),"slot labels: level and name drawn");
            t.Check(Near(b.GetPixel(242,192),InfoC)&&Inked(b,new Rectangle(250,200,100,120),InfoC),"info list text drawn over its back");
            t.Check(Near(b.GetPixel(152,22),UpC),"Up button drawn");
        }
        t.Equal("Lv.52",p.LabelText(p.Main!.Pieces.First(x=>x.Name=="Level_Label"&&x.Slot==0&&x.Parent?.Name=="Select_Button")),"slot level label (R9-O: Lv.%d)");
        string Info(string n)=>p.LabelText(p.Main!.Pieces.First(x=>x.ListName==SelectCharPanel.InfoList&&x.Name==n))!;
        t.Check(Info("Name_Label")=="甲"&&Info("Level_Label")=="52"&&Info("Class_Label")=="王族"&&Info("Pledge_Label")=="測試盟"&&Info("Alignment_Label")=="-5"&&Info("Birthday_Label")=="2026.11.10","info labels from the selected character (R9-O: birthday YYYY.MM.DD)");
        t.Equal("騎士",SelectCharPanel.ClassName(33),"class 33 (pending deletion) = knight");
        p.Mouse(M(UiMouseKind.Move,new(15,75)),c);
        t.Check(Near(Pixel(p,12,62),SelectC),"R9-O: WaitDelete_Button has one surf (class surf + 3) in every state, also under the pointer");
        Click(p,new(15,75),c);
        t.Equal("乙",p.Selected,"click on row 1 selects it");
        t.Equal("騎士",Info("Class_Label"),"info follows the selection");
        Click(p,new(152,62),c);
        t.Equal(1,p.Scroll,"Down scrolls one row");
        t.Check(Near(Pixel(p,12,12),SelectC),"row 1 (selected, pending: WaitDelete surf) now in the first slot");
        Click(p,new(152,62),c);Click(p,new(152,62),c);
        t.Equal(2,p.Scroll,"scroll stops at the last row");
        using(var b=Render(p))t.Check(Near(b.GetPixel(12,62),CreateC),"free slot → Create_Button");
        Click(p,new(152,22),c);
        t.Equal(1,p.Scroll,"Up scrolls back");
        // One slot, one character: the second row is locked.
        var locked=Select();locked.Attach(new Host(ViewOf(1,Char("甲"))));
        using(var b=Render(locked)){
            t.Check(Near(b.GetPixel(12,62),LockC)&&Near(b.GetPixel(100,90),LockBackC),"rows beyond the slots: Slot_Lock_Back + Lock_Button");
        }
        t.Check(!locked.Enabled(locked.Main!.Pieces.First(x=>x.Name=="Lock_Button")),"slot purchase: no 3.81 packet → disabled");
        host.View=host.View with{State=LoginState.InWorld};p.Sync();
        t.Check(!p.Visible,"in world → hidden");
        host.View=host.View with{State=LoginState.CharacterList};p.Sync();
        t.Check(p.Visible,"back on the list (switch character) → shown again");
    }

    static void SelectActions(WorldModuleTestContext t){
        var p=Select();var c=new UiNullCommands();
        var host=new Host(ViewOf(4,Char("甲"),Char("乙")));p.Attach(host);
        Click(p,new(260,155),c);
        t.Equal("enter:甲:3",string.Join(",",host.Calls),"Enter_Button enters with the list stamp");
        host.Calls.Clear();
        Click(p,new(15,75),c);Click(p,new(15,75),c,2);
        t.Equal("enter:乙:3",string.Join(",",host.Calls),"double click on the selected row enters");
        host.Calls.Clear();host.Result=new(false,"請選擇目前伺服器清單中的角色。");
        p.Key(K(Keys.Enter),c);
        t.Check(host.Calls.Count==1&&p.Shown==CharSelectMessage.Notice&&p.MessageText=="請選擇目前伺服器清單中的角色。","Enter key; a refusal shows Message_Window");
        using(var b=Render(p))t.Check(Near(b.GetPixel(125,185),OkC)&&Inked(b,new Rectangle(110,110,180,50),MsgBackC),"message box drawn over the screen with its text");
        Click(p,new(260,155),c);
        t.Equal(1,host.Calls.Count,"main buttons disabled while the message is shown");
        Click(p,new(125,185),c);
        t.Check(p.Shown==CharSelectMessage.None,"Ok closes the message");
        host.Result=new(true,"ok");
        p.Key(K(Keys.Up),c);t.Equal("甲",p.Selected,"Up key moves the selection");
        p.Key(K(Keys.Down),c);t.Equal("乙",p.Selected,"Down key moves the selection");
        Click(p,new(20,275),c);
        t.Equal(1,host.Logouts,"Back_Button logs out");
        host.View=host.View with{State=LoginState.ManagingCharacters};p.Sync();
        t.Check(!p.Enabled(p.Main!.Pieces.First(x=>x.MouseEvent==SelectCharPanel.EnterEvent))&&p.Enabled(p.Main.Pieces.First(x=>x.MouseEvent==SelectCharPanel.BackEvent)),"busy: only Back stays enabled");
        host.View=host.View with{State=LoginState.CharacterList,CanCreate=false};p.Sync();
        t.Check(p.Mouse(M(UiMouseKind.Down,new(390,5)),c)&&p.Key(K(Keys.F1),c),"modal: mouse and keys stay here");
        // Create: the native create screen when usable, otherwise the WinForms dialog.
        var full=new Host(ViewOf(4,Char("甲")));var q=Select();q.Attach(full);
        Click(q,new(152,62),c);Click(q,new(15,75),c);
        t.Equal(1,full.Fallbacks,"no usable create screen → WinForms creation dialog");
        var create=CreateCharPanelTests.Create();var r=Select(create:create);r.Attach(full);
        Click(r,new(15,75),c);
        t.Check(create.Visible&&!r.Visible,"Create_Button opens the native create screen and the select screen hides");
        create.Close();
        t.Check(r.Visible,"create closed → select screen again");
        // New login generation: selection and messages reset.
        var g=new Host(ViewOf(4,Char("甲"),Char("乙")));var s=Select();s.Attach(g);
        Click(s,new(15,75),c);
        g.View=ViewOf(4,Char("丁")) with{Generation=2};s.Sync();
        t.Check(s.Selected=="丁"&&s.Scroll==0,"new generation → selection reset");
    }

    static void SelectDelete(WorldModuleTestContext t){
        var p=Select();var c=new UiNullCommands();
        var host=new Host(ViewOf(4,Char("甲"),Char("乙",34)));p.Attach(host);
        Click(p,new(110,275),c);
        t.Check(p.Shown==CharSelectMessage.ConfirmDelete&&p.MessageText==SelectCharPanel.DeleteText(Char("甲")),"Delete_Button → confirm with the WinForms text",p.MessageText);
        Click(p,new(255,185),c);
        t.Check(p.Shown==CharSelectMessage.None&&host.Calls.Count==0,"Cancel → nothing sent");
        Click(p,new(15,75),c);Click(p,new(110,275),c);
        t.Check(p.MessageText!.Contains("取消「乙」的七日刪除"),"pending character: cancel-deletion text");
        host.AfterDelete=(v,_)=>v with{State=LoginState.ManagingCharacters,Notice="正在處理刪除，等待伺服器結果。"};
        Click(p,new(125,185),c);
        t.Equal("delete:乙:3",string.Join(",",host.Calls),"Ok sends the delete with the list stamp");
        t.Check(p.Shown==CharSelectMessage.None,"waiting for the server: no message");
        host.View=ViewOf(4,Char("甲"),Char("乙")) with{Revision=4,Notice="角色清單已更新；標示七日待刪除的角色可再次按刪除以取消排程。"};p.Sync();
        t.Check(p.Shown==CharSelectMessage.Notice&&p.MessageText!.StartsWith("角色清單已更新"),"server result notice shown once the list is back");
        p.Key(K(Keys.Escape),c);
        t.Check(p.Shown==CharSelectMessage.None,"Esc closes the message");
        p.Sync();
        t.Check(p.Shown==CharSelectMessage.None,"notice shown once");
    }

    static void SelectBroken(WorldModuleTestContext t){
        void Bad(string xml,string label){var p=new SelectCharPanel();var s=SelectSurfs();t.Throws<UiLayoutException>(()=>p.Load(UiLayout.Parse(xml,"SelectUI.xml"),id=>s.GetValueOrDefault(id)),label);}
        Bad(SelectXml(root:"CreateUI"),"wrong root → UiLayoutException");
        Bad(SelectXml(main:false),"no select window → UiLayoutException");
        Bad(SelectXml(message:false),"no message window → UiLayoutException");
        Bad(SelectXml(slots:""),"no slot rows → UiLayoutException");
        Bad(SelectXml(msgBase:"BaseSurf = \"x\""),"non-numeric surf → UiLayoutException");
        var noBase=Select(SelectXml(msgBase:""));
        t.Check(noBase.Available&&noBase.Message!.Bounds==new Rectangle(100,100,200,100),"message box without BaseSurf → Back_Image size",noBase.Unavailable);
        var noSurf=new SelectCharPanel();noSurf.Load(UiLayout.Parse(SelectXml(),"SelectUI.xml"),_=>null);
        t.Check(!noSurf.Available&&noSurf.Missing.Count>0,"every image missing → no window size → unavailable, all listed",noSurf.Unavailable);
    }
}

static class CreateCharPanelTests {
    const uint MsgBackC=0xFF303030,OkC=0xFF0000FF,ClassC=0xFF200000,ClassPushC=0xFF800000,MaleC=0xFF002000,MaleBackC=0xFF00A000,FemaleC=0xFF000020,TitleC=0xFFFF0000,
        SummaryC=0xFF404040,UpC=0xFF00FFFF,DownC=0xFFFF00FF,AttackPC=0xFFA0A000,AttackKC=0xFF00A0A0,BackC=0xFF101010,InputC=0xFF252525,NextC=0xFF0080FF,PrevC=0xFF808000;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Dictionary<int,UiSurface> Surfs(){
        var d=new Dictionary<int,UiSurface>{
            [488]=Surface(488,200,100,0xFF202020),[487]=Surface(487,200,100,MsgBackC),[489]=Surface(489,30,10,OkC),[492]=Surface(492,30,10,0xFF00FF00),
            [495]=Surface(495,20,20,MaleC),[498]=Surface(498,20,20,FemaleC),[501]=Surface(501,24,24,MaleBackC),[415]=Surface(415,30,15,PrevC),[418]=Surface(418,30,15,NextC),
            [224]=Surface(224,20,10,TitleC),[170]=Surface(170,20,10,TitleC),[504]=Surface(504,8,8,DownC),[506]=Surface(506,8,8,UpC),[524]=Surface(524,200,170,SummaryC),
            [510]=Surface(510,20,20,AttackPC),[513]=Surface(513,20,20,AttackKC),[519]=Surface(519,30,15,0xFF707070),[527]=Surface(527,30,15,0xFF909090),
            [526]=Surface(526,120,28,InputC),[156]=Surface(156,400,300,BackC),[7156]=Surface(7156,400,300,BackC),   // R9-O: stat scene background 7156 + 2×class + sex
        };
        for(int i=0;i<7;i++){d[128+2*i]=Surface(128+2*i,20,20,ClassC);d[129+2*i]=Surface(129+2*i,20,20,ClassPushC);}
        return d;
    }
    static readonly string[] ClassNames=["Prince","Knight","Elf","Magician","Darkelf","Dragon","Illusionist"];
    static string Stat(string name,int ctrlX,int x,int y)=>$"""
              <Control Name = "{name}" X = "{ctrlX}">
                <Label Name = "Point" X = "{x}" Y = "{y}" Align = "center" Caption = "0" Width = "22" Height = "20"/>
                <Button Name = "Down_Button" X = "{x+22}" Y = "{y+2}" BaseSurf = "504" HighlightSurf = "504" PushSurf = "505" MouseEvent = "Down_CustomizingWin"/>
                <Button Name = "Up_Button" X = "{x+34}" Y = "{y+2}" BaseSurf = "506" HighlightSurf = "506" PushSurf = "507" MouseEvent = "Up_CustomizingWin"/>
              </Control>
    """;
    // Synthetic layout with the element kinds of CreateUI.xml: message window; class scene with Class_Buttons (Warrior first),
    // sex buttons + backs, Prev/Next, screen/multi title, a missing class-name surf, Front/Back images without ids and a board;
    // stat scene with the offset Stat_Change_Control, Customizing_Win, Gauge_Group, per-class StatType buttons, the summary
    // back, Customizing/Reset/OK/Prev, the name Edit, its back, a board and the background.
    internal static string Xml(string root="CreateUI",bool edit=true,int classes=7,bool strength=true)=>$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <{root}>
          <Window Name = "Message_Window" X = "100" Y = "100" Modal = "1" Top = "1" DarkBack = "1" Activate = "0" BaseSurf = "488;3">
            <MultiLabel Name = "Message_Label" X = "10" Y = "10" Width = "180" Height = "50" VAlign = "center" LineMargin = "3"/>
            <Button Name = "Ok_Button" X = "20" Y = "80" BaseSurf = "489" HighlightSurf = "490" PushSurf = "491" MouseEvent = "Ok_MessageWin"/>
            <Button Name = "Cancel_Button" X = "150" Y = "80" BaseSurf = "492" MouseEvent = "Cancel_MessageWin"/>
            <Image Name = "Back_Image" Surf = "487;1"/>
          </Window>
          <Window Name = "Class_Select_Scene" X = "0" Y = "0" Width = "400" Height = "300" Activate = "0">
            <!-- 기존 클래스 -->
            <Control Name = "Class_Buttons">
              <Button Name = "Warrior_Button" X = "5" Y = "250" BaseSurf = "707;4" PushSurf = "708;4" MouseEvent = "Class_ClassSelectScene"/>
              {string.Concat(Enumerable.Range(0,classes).Select(i=>$"""<Button Name = "{ClassNames[i]}_Button" X = "{30+30*i}" Y = "250" BaseSurf = "{128+2*i};4" PushSurf = "{129+2*i};4" MouseEvent = "Class_ClassSelectScene"/>"""))}
            </Control>
            <Button Name = "Male_Button" X = "100" Y = "150" BaseSurf = "495;1" HighlightSurf = "496;1" PushSurf = "497;1" MouseEvent = "Male_ClassSelectScene"/>
            <Image Name = "Male_Back" X = "98" Y = "148" Surf = "501;3"/>
            <Button Name = "Female_Button" X = "140" Y = "150" BaseSurf = "498;1" HighlightSurf = "499;1" PushSurf = "500;1" MouseEvent = "Female_ClassSelectScene"/>
            <Image Name = "Female_Back" X = "138" Y = "148" Surf = "501;3"/>
            <Button Name = "Prev_Button" X = "5" Y = "280" BaseSurf = "415;4" MouseEvent = "Prev_ClassSelectScene"/>
            <Button Name = "Next_Button" X = "350" Y = "280" BaseSurf = "418;4" MouseEvent = "Next_ClassSelectScene"/>
            <Image Name = "Title_Screen" X = "5" Y = "5" Surf = "224;4"/>
            <Image Name = "Title_Multi" X = "5" Y = "5" Surf = "225;3"/>
            <Image Name = "ClassName_Screen" X = "20" Y = "100" Surf = "99;4"/>
            <Image Name = "Front_Image" X = "0" Y = "0"/>
            <Image Name = "Back_Image" X = "0" Y = "0"/>
            <BoardImage Name = "BG_Image" X = "0" Y = "0" Width = "400" Height = "300" CustomColor = "0,0,0" CustomAlpha = "31"/>
          </Window>
          <Window Name = "Stat_Setting_Scene" X = "0" Y = "0" Width = "400" Height = "300" Activate = "0">
            <Image Name = "Title_Screen" X = "5" Y = "5" Surf = "170;4"/>
            <Control Name = "Stat_Change_Control" X = "-100" Y = "-10">
        {(strength?Stat("Strength",12,150,110):"")}
        {Stat("Dexterity",12,150,130)}
        {Stat("Constitution",12,150,150)}
        {Stat("Wisdom",-9,230,110)}
        {Stat("Charisma",-9,230,130)}
        {Stat("Intelligence",-9,230,150)}
              <Label Name = "Remain_Point" X = "200" Y = "170" Align = "center" Caption = "0" Width = "32" Height = "22"/>
            </Control>
            <Control Name = "Customizing_Win">
              <Button Name = "Exit_Button" X = "300" Y = "10" BaseSurf = "502" HighlightSurf = "502" PushSurf = "503" MouseEvent = "Exit_CustomizingWin"/>
            </Control>
            <Control Name = "Gauge_Group"><Image Name = "Gauge1" X = "60" Y = "60" Surf = "509"/></Control>
            <Control Name = "StatType_Group">
              <Control Name = "Class_Prince"><Button Name = "Attack_Button" X = "10" Y = "40" BaseSurf = "510" MouseEvent = "Auto_StatSettingScene"/></Control>
              <Control Name = "Class_Knight"><Button Name = "Attack_Button" X = "10" Y = "40" BaseSurf = "513" MouseEvent = "Auto_StatSettingScene"/></Control>
            </Control>
            <Image Name = "Stat_Summary_Back" X = "2" Y = "30" Surf = "524;1"/>
            <Image Name = "Stat_Summary_Back_Multi" X = "2" Y = "30" Surf = "525;3"/>
            <Button Name = "Customizing_Button" X = "20" Y = "220" BaseSurf = "519" MouseEvent = "Customizing_StatSettingScene"/>
            <Button Name = "Reset_Button" X = "80" Y = "220" BaseSurf = "527" MouseEvent = "Reset_CustomizingWin"/>
            <Button Name = "OK_Button" X = "300" Y = "270" BaseSurf = "489" HighlightSurf = "490" PushSurf = "491" MouseEvent = "OK_StatSettingScene"/>
            <Button Name = "Prev_Button" X = "5" Y = "280" BaseSurf = "415;4" MouseEvent = "Prev_StatSettingScene"/>
            {(edit?"""<Edit Name = "Username_Edit" X = "150" Y = "275" Width = "100" Height = "20" LimitText = "12"/>""":"")}
            <Image Name = "Input_Back" X = "140" Y = "270" Surf = "526"/>
            <BoardImage Name = "BG_Image" X = "0" Y = "0" Width = "400" Height = "300" CustomColor = "0,0,0" CustomAlpha = "16"/>
            <Image Name = "Back_Image" X = "0" Y = "0" Surf = "156"/>
          </Window>
        </{root}>
        """;
    internal static CreateCharPanel Create(string? xml=null){
        var p=new CreateCharPanel();var s=Surfs();
        p.Load(UiLayout.Parse(xml??Xml(),"CreateUI.xml"),id=>s.GetValueOrDefault(id));
        return p;
    }
    static Point ClassAt(int i)=>new(30+30*i+2,252);
    // Stat_Change_Control (-100,-10): Strength Up = (12+150+34-100, 110+2-10) = (96,102); Wisdom Up = (-9+230+34-100,102) = (155,102).
    static readonly Point StrUp=new(98,104),StrDown=new(86,104),Reset=new(85,225),Ok=new(305,272),Prev=new(10,285),Next=new(355,285),MsgOk=new(125,185);

    [WorldModuleTest("create-char-ui")]
    static void Run(WorldModuleTestContext t){
        Parsing(t);
        ClassScene(t);
        StatScene(t);
        Sending(t);
        Broken(t);
    }

    static void Parsing(WorldModuleTestContext t){
        var p=Create();
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Equal(new Rectangle(0,0,400,300),p.ClassScene!.Bounds,"class scene: declared Width/Height");
        t.Equal(new Rectangle(100,100,200,100),p.Message!.Bounds,"message window: BaseSurf size");
        t.Check(p.Skipped.Any(s=>s.Contains("戰士"))&&!p.Missing.Any(m=>m.Contains("707")||m.Contains("708")),"Warrior_Button not built and its surfs never looked up",string.Join(" | ",p.Skipped));
        t.Check(p.Skipped.Any(s=>s.Contains("Customizing_Win"))&&!p.Missing.Any(m=>m.Contains("502")),"Customizing_Win not built");
        t.Check(!p.Skipped.Any(s=>s.Contains("Gauge_Group"))&&p.Missing.Any(m=>m.StartsWith("surf 509 ")),"R9-O: Gauge_Group built; its missing bar image listed",string.Join(" | ",p.Missing));
        t.Check(p.Skipped.Count(s=>s.Contains("multi"))==2,"multi variants not built");
        t.Check(p.Skipped.Count(s=>s.Contains("沒有圖號"))==2,"Front_Image / Back_Image without an id not built");
        t.Check(p.Missing.Contains("surf 99 (CreateUI/Window[1]/Image[4])"),"missing class-name surf listed with its path",string.Join(" | ",p.Missing));
        t.Equal(new Point(96,102),p.StatScene!.Find("Up_Button","Strength")!.Offset,"stat button: Stat_Change_Control(-100,-10) → Strength(12) → Up(184,112)");
        t.Equal(new Point(155,102),p.StatScene.Find("Up_Button","Wisdom")!.Offset,"negative Control X (-9)");
        t.Equal(12,p.StatScene.Pieces.Single(x=>x.Kind==CharUiKind.Edit).Limit,"Edit LimitText");
    }

    static void ClassScene(WorldModuleTestContext t){
        var p=Create();var c=new UiNullCommands();
        t.Check(!p.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(1,SelectCharPanelTests.Char("甲")))),"no free slot → Begin refuses");
        var host=new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4));
        t.Check(p.Begin(host)&&p.Visible&&p.Scene==CreateScene.Class,"Begin opens the class scene");
        t.Check(p.Class==0&&p.Sex==0&&p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(0)),"defaults: prince, male, base stats");
        using(var b=SelectCharPanelTests.Render(p)){
            t.Check(SelectCharPanelTests.Near(b.GetPixel(ClassAt(0).X,ClassAt(0).Y),ClassPushC)&&SelectCharPanelTests.Near(b.GetPixel(ClassAt(1).X,ClassAt(1).Y),ClassC),"selected class button drawn with PushSurf");
            t.Check(SelectCharPanelTests.Near(b.GetPixel(98,148),MaleBackC)&&!SelectCharPanelTests.Near(b.GetPixel(138,148),MaleBackC),"Male_Back only for the selected sex");
            t.Check(SelectCharPanelTests.Near(b.GetPixel(102,152),MaleC),"sex button over its back");
            t.Check(SelectCharPanelTests.Near(b.GetPixel(7,7),TitleC),"title drawn");
            t.Check(!SelectCharPanelTests.Near(b.GetPixel(7,252),ClassC),"no Warrior button");
        }
        SelectCharPanelTests.Click(p,ClassAt(1),c);
        t.Check(p.Class==1&&p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(1)),"Knight_Button → class 1, knight base stats");
        t.Check(SelectCharPanelTests.Near(SelectCharPanelTests.Pixel(p,ClassAt(1).X,ClassAt(1).Y),ClassPushC),"push image follows the class");
        SelectCharPanelTests.Click(p,new(145,155),c);
        t.Equal(1,p.Sex,"Female_Button → female");
        t.Check(SelectCharPanelTests.Near(SelectCharPanelTests.Pixel(p,138,148),MaleBackC),"Female_Back shown");
        p.Key(SelectCharPanelTests.K(Keys.Enter),c);
        t.Equal(CreateScene.Stat,p.Scene,"Enter = Next");
        p.Key(SelectCharPanelTests.K(Keys.Escape),c);
        t.Equal(CreateScene.Class,p.Scene,"Esc on the stat scene = Prev");
        SelectCharPanelTests.Click(p,Next,c);
        t.Equal(CreateScene.Stat,p.Scene,"Next_Button → stat scene");
        SelectCharPanelTests.Click(p,Prev,c);
        t.Equal(CreateScene.Class,p.Scene,"Prev on the stat scene → class scene");
        SelectCharPanelTests.Click(p,Prev,c);
        t.Check(!p.Visible,"Prev on the class scene → back to the select screen");
        t.Check(p.Begin(host)&&p.Class==0&&p.Sex==0,"opening again starts over");
        p.Key(SelectCharPanelTests.K(Keys.Escape),c);
        t.Check(!p.Visible,"Esc on the class scene closes");
    }

    static void StatScene(WorldModuleTestContext t){
        var p=Create();var c=new UiNullCommands();
        p.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));
        SelectCharPanelTests.Click(p,Next,c);
        t.Equal(8,p.Remaining,"prince: 75 − 67 = 8 points");
        using(var b=SelectCharPanelTests.Render(p)){
            t.Check(SelectCharPanelTests.Near(b.GetPixel(98,104),UpC)&&SelectCharPanelTests.Near(b.GetPixel(30,190),SummaryC),"stat buttons over the summary back");
            t.Check(SelectCharPanelTests.Near(b.GetPixel(12,42),AttackPC),"StatType buttons of the selected class only (prince)");
            t.Check(SelectCharPanelTests.Near(b.GetPixel(145,272),InputC),"name input back drawn");
            t.Check(SelectCharPanelTests.Inked(b,new Rectangle(62,100,22,20),SummaryC),"Strength point label drawn");
            var back=b.GetPixel(395,5);var raw=Color.FromArgb(unchecked((int)BackC));
            t.Check(back.R<=raw.R&&back.R>=raw.R-2,"background under a 16-alpha black board",back);
        }
        t.Equal("13",p.LabelText(p.StatScene!.Find("Point","Strength")!),"Strength point = base 13");
        t.Equal("8",p.LabelText(p.StatScene.Find("Remain_Point")!),"Remain_Point");
        for(int i=0;i<8;i++)SelectCharPanelTests.Click(p,StrUp,c);
        t.Check(p.Stats[0]==21&&p.Remaining==0,"Up × 8 → STR 21, no points left");
        SelectCharPanelTests.Click(p,StrUp,c);
        t.Equal((byte)21,p.Stats[0],"no Up without points");
        SelectCharPanelTests.Click(p,StrDown,c);
        t.Check(p.Stats[0]==20&&p.Remaining==1,"Down gives a point back");
        SelectCharPanelTests.Click(p,Reset,c);
        t.Check(p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(0)),"Reset_Button → base stats");
        SelectCharPanelTests.Click(p,StrDown,c);
        t.Equal((byte)13,p.Stats[0],"no Down below the base");
        t.Check(!p.Enabled(p.StatScene.Find("Attack_Button","Class_Prince")!)&&!p.Enabled(p.StatScene.Find("Customizing_Button")!),"Auto presets / Customizing: no engine tables → disabled");
        // Name input.
        t.Check(!p.Key(SelectCharPanelTests.K(Keys.A),c),"printable key left for WM_CHAR");
        foreach(char ch in "王子\tA")p.Char(ch);
        t.Equal("王子A",p.Name,"typed characters (control characters ignored)");
        p.Key(SelectCharPanelTests.K(Keys.Back),c);
        t.Equal("王子",p.Name,"Backspace");
        foreach(char ch in "1234567890ABC")p.Char(ch);
        t.Equal(12,p.Name.Length,"LimitText 12");
        t.Check(SelectCharPanelTests.Inked(SelectCharPanelTests.Render(p),new Rectangle(150,275,100,20),InputC),"name drawn in the edit");
        SelectCharPanelTests.Click(p,Prev,c);
        t.Check(!p.Char('X')&&p.Name.Length==12,"no typing on the class scene");
    }

    static void Sending(WorldModuleTestContext t){
        var c=new UiNullCommands();
        var p=Create();var host=new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4,SelectCharPanelTests.Char("甲")));
        p.Begin(host);SelectCharPanelTests.Click(p,ClassAt(2),c);SelectCharPanelTests.Click(p,Next,c);
        SelectCharPanelTests.Click(p,Ok,c);
        t.Check(p.MessageShown&&host.Calls.Count==0,"local Validate error (points left / no name) → message, nothing sent",p.MessageText);
        t.Check(SelectCharPanelTests.Inked(SelectCharPanelTests.Render(p),new Rectangle(110,110,180,50),MsgBackC),"message text drawn");
        SelectCharPanelTests.Click(p,Ok,c);
        t.Check(p.MessageShown,"scene buttons disabled while the message is shown");
        SelectCharPanelTests.Click(p,MsgOk,c);
        t.Check(!p.MessageShown,"Ok closes the message");
        // Elf base 11,12,12,12,9,12 (68) → 7 points.
        for(int i=0;i<7;i++)SelectCharPanelTests.Click(p,StrUp,c);
        foreach(char ch in "精靈一")p.Char(ch);
        host.AfterCreate=(v,_)=>v with{State=LoginState.ManagingCharacters,Notice="正在建立角色，等待伺服器結果。"};
        p.Key(SelectCharPanelTests.K(Keys.Enter),c);
        t.Equal("create:精靈一:3",string.Join(",",host.Calls),"Enter on the stat scene = OK → create with the list stamp");
        t.Check(host.Created==new CharacterCreation("精靈一",2,0,18,12,12,12,9,12),"request: class, sex and the six stats in CharacterCreation order",host.Created);
        t.Check(p.Visible&&!p.MessageShown,"waiting for the server");
        host.View=host.View with{State=LoginState.CharacterList,Notice="角色名稱已有人使用。"};p.Sync();
        t.Check(p.Visible&&p.MessageText=="角色名稱已有人使用。","server refusal notice shown, screen stays");
        SelectCharPanelTests.Click(p,MsgOk,c);
        host.Calls.Clear();
        host.AfterCreate=(v,r)=>v with{State=LoginState.CharacterList,Characters=[..v.Characters,SelectCharPanelTests.Char(r.Name,r.Class)],Notice="角色已建立，可從清單選擇進入世界。"};
        SelectCharPanelTests.Click(p,Ok,c);
        t.Check(host.Calls.Count==1&&!p.Visible,"server confirms (new character in the list) → back to the select screen");
        // Refused by the connection (stale stamp).
        var q=Create();var h2=new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)){Result=new(false,"角色清單已變更或沒有空位，請重新確認。")};
        q.Begin(h2);SelectCharPanelTests.Click(q,Next,c);
        for(int i=0;i<8;i++)SelectCharPanelTests.Click(q,StrUp,c);
        foreach(char ch in "王子")q.Char(ch);
        SelectCharPanelTests.Click(q,Ok,c);
        t.Check(h2.Calls.Count==1&&q.MessageText=="角色清單已變更或沒有空位，請重新確認。","refused send → message with the reason");
        h2.View=h2.View with{Generation=2};q.Sync();
        t.Check(!q.Visible,"new login generation → closed");
        var r2=Create();var h3=new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4));r2.Begin(h3);
        h3.View=h3.View with{State=LoginState.InWorld};r2.Sync();
        t.Check(!r2.Visible,"left the character list → closed");
        t.Check(r2.Mouse(SelectCharPanelTests.M(UiMouseKind.Down,new(1,1)),c)==false,"closed → input not taken");
    }

    static void Broken(WorldModuleTestContext t){
        void Bad(string xml,string label){var p=new CreateCharPanel();t.Throws<UiLayoutException>(()=>p.Load(UiLayout.Parse(xml,"CreateUI.xml"),_=>null),label);}
        Bad(Xml(root:"SelectUI"),"wrong root → UiLayoutException");
        Bad(Xml(classes:6),"a class button missing → UiLayoutException");
        Bad(Xml(strength:false),"a stat Control missing → UiLayoutException");
        Bad(Xml(edit:false),"no name Edit → UiLayoutException");
        var noSurf=new CreateCharPanel();noSurf.Load(UiLayout.Parse(Xml(),"CreateUI.xml"),_=>null);
        t.Check(!noSurf.Available,"message box without any image → unavailable",noSurf.Unavailable);
    }
}
