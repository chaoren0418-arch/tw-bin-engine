using System.Reflection;
using P06.NativeUi;
namespace P06;

// R9-B（T362／#846）地圖窗右鍵選單與存點窗的離線測試（docs/reference/followups-9.md §7.2、docs/reports/issue-846.md）：
// - Field_Map：右鍵按下只記旗標、放開才在游標處出 MapCommunicationSelectWindow（2944／2947，位置夾在 560×440 再加 1）；
//   選單開著時在地圖窗內左／右鍵按下先收起；
// - 鈕 0：既有 MapSendEntry（聊天輸入列「/loc 」），1785 畫在聊天輸入列（MapSendPromptPanel），提示中畫 surf 150 標記；
// - 鈕 1：MapLocationSaveWindow（底 134 置中、調色盤 19×19／每列 8 格、輸入 12 字、確認 2951、Enter／ESC），存進本地清單（每角色 30 筆、1786）；
// - 缺圖不畫；離開世界全部收起；游標 → 地圖座標（sub_A66F50）與 ToPixel 互逆。
// 只用合成狀態與記憶體內面板／surf，不讀客戶端素材、不連線、不開視窗。由 --module-tests 執行（名稱 map-popup）。
static class MapPopupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;

    sealed class ChatCommands:IUiChatCommands {
        public readonly List<string> Calls=[];
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool BeginChatInput(){Calls.Add("begin");return true;}
        public void EndChatInput()=>Calls.Add("end");
        public Task<bool> SendChat(string text,PlayerChatView displayed){Calls.Add("send:"+text);return Task.FromResult(true);}
    }
    sealed class Game(bool inWorld,string name):IUiGameState {
        public bool InWorld=>inWorld;
        public string? CharacterName=>name;
        public int? Level=>1;
        public long? Hp=>1;public long? MaxHp=>1;public long? Mp=>1;public long? MaxMp=>1;
        public long Now=>0;
    }
    sealed class MapSource(Func<UiMapSnapshot?> read):IUiMapSource { public UiMapSnapshot? ReadMap()=>read(); }

    static readonly Dictionary<int,UiSurface> surfaces=[];
    static UiSurface? Surface(int id){
        if(surfaces.TryGetValue(id,out var s))return s;
        (int w,int h)=id switch{
            MapCommunicationPanel.SendSurf or MapCommunicationPanel.SendDownSurf or MapCommunicationPanel.SaveSurf or MapCommunicationPanel.SaveDownSurf=>(80,20),
            MapCommunicationPanel.SpacingSurf=>(80,22),MapCommunicationPanel.MarkerSurf=>(9,9),
            MapLocationSavePanel.BaseSurf=>(200,140),MapLocationSavePanel.SelectorSurf=>(19,19),MapLocationSavePanel.PaletteSurf=>(155,40),
            MapLocationSavePanel.ConfirmSurf or MapLocationSavePanel.ConfirmDownSurf=>(40,20),
            _=>(0,0)};
        if(w==0)return null;
        var px=new uint[w*h];Array.Fill(px,0xFF808080u);
        return surfaces[id]=new UiSurface(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,px));
    }
    static UiMouseEvent Mouse(UiMouseKind kind,Point at,MouseButtons button)=>new(kind,at,button,1,0,Keys.None);
    static Point Centre(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);

    [WorldModuleTest("map-popup")]
    static void Run(WorldModuleTestContext t){
        // 1. 自動發現：三個新面板各一行註冊、IUiClosablePanel、圖層。
        t.Check(UiPanelRegistry.Errors.Count==0,"[UiPanel] registrations valid",UiPanelRegistry.Errors);
        foreach(var id in new[]{MapCommunicationPanel.PanelId,MapSendPromptPanel.PanelId,MapLocationSavePanel.PanelId})
            t.Equal(1,UiPanelRegistry.Panels.Count(r=>r.Id==id),id+" registered once");
        var made=new UiRoot();
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==MapCommunicationPanel.PanelId).Create(made) is MapCommunicationPanel{Layer:UiLayer.Float},"menu panel: Float layer");
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==MapSendPromptPanel.PanelId).Create(made) is MapSendPromptPanel{Layer:UiLayer.Float},"1785 prompt panel: Float layer");
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==MapLocationSavePanel.PanelId).Create(made) is MapLocationSavePanel{Layer:UiLayer.Window},"save window: Window layer (window stack)");
        foreach(var type in new[]{typeof(MapCommunicationPanel),typeof(MapSendPromptPanel),typeof(MapLocationSavePanel)})
            t.Check(typeof(IUiClosablePanel).IsAssignableFrom(type),type.Name+" implements IUiClosablePanel");

        // 2. 組合：Field_Map、聊天、座標清單（裝 TransferMenu）、選單、1785 提示、存點窗都在同一個 UiRoot。
        UiMapSnapshot? current=new(4,32800,32790,3,[]);
        var source=new MapSource(()=>current);
        var root=new UiRoot();
        var field=new FieldMapPanel(new MapState(null,()=>source));
        var chat=new ChatPanel();
        var list=new LocationListPanel(()=>field.Bounds,root.Find);
        var menu=new MapCommunicationPanel(root.Find);menu.Load(Surface);
        var prompt=new MapSendPromptPanel(root.Find);
        var save=new MapLocationSavePanel();save.Load(Surface);
        root.Add(field);root.Add(chat);root.Add(list);root.Add(menu);root.Add(prompt);root.Add(save);
        typeof(FieldMapPanel).GetField("size",F)!.SetValue(field,new Size(161,121));field.MoveTo(new(5,250));
        var cmd=new ChatCommands();
        bool inWorld=true;string name="甲角色";
        IUiGameState State()=>new UiChatGameState(new Game(inWorld,name),new(inWorld?new PlayerChatView(1,1,123,1,true,"",[]):null,false));
        void Frame()=>root.Update(State());
        Frame();
        t.Check(ReferenceEquals(field.Popup,menu),"menu panel installs itself on Field_Map");
        t.Check(field.TransferHandler!=null,"list panel still installs the TransferMenu entry");
        field.Map.ToggleField();
        var fb=field.Bounds;
        t.Equal(new Rectangle(5,250,161,121),fb,"Field_Map open at (5,250)");

        // 3. 右鍵按下只記旗標（516），放開才出選單（517）；選單在游標＋1。
        var inside=new Point(fb.X+60,fb.Y+80);
        t.Check(field.Mouse(Mouse(UiMouseKind.Down,inside,MouseButtons.Right),cmd)&&field.RightPressed&&!menu.MenuOpen,"right press: flag only, no menu");
        t.Check(cmd.Calls.Count==0&&!chat.Input.Open,"right press sends nothing and opens no prompt");
        t.Check(field.Mouse(Mouse(UiMouseKind.Up,inside,MouseButtons.Right),cmd)&&!field.RightPressed&&menu.MenuOpen,"right release: menu shown, flag cleared");
        t.Equal(new Point(inside.X+1,inside.Y+1),menu.Location,"menu at cursor + 1 (sub_A66950)");
        t.Equal(new Rectangle(inside.X+1,inside.Y+1,80,42),menu.Bounds,"button 0 at (0,0), button 1 at (0, h128)");
        t.Equal(new Rectangle(inside.X+1,inside.Y+1+22,80,20),menu.ButtonRect(MapCommunicationPanel.SaveButton),"button 1 y = height of surf 128");
        t.Equal(new MapLocationTarget(4,32800,32790),menu.Target,"no png on screen → target is the player's position (推測 fallback)");
        root.Update(State());t.Check(menu.Visible&&root.HitTest(Centre(menu.ButtonRect(0)))==menu,"menu is hit before Field_Map (Float layer)");

        // 4. 選單開著時在地圖窗內按下（左／右）先收起。
        field.Mouse(Mouse(UiMouseKind.Down,new(fb.X+10,fb.Y+100),MouseButtons.Left),cmd);
        t.Check(!menu.MenuOpen,"left press on the map hides the menu");
        field.Mouse(Mouse(UiMouseKind.Up,new(fb.X+10,fb.Y+100),MouseButtons.Left),cmd);
        t.Check(menu.ShowMenu(inside,null,cmd)&&menu.MenuOpen,"menu shown again");
        field.Mouse(Mouse(UiMouseKind.Down,new(fb.X+10,fb.Y+100),MouseButtons.Right),cmd);
        t.Check(!menu.MenuOpen&&field.RightPressed,"right press hides the menu and arms the next release");
        field.Mouse(Mouse(UiMouseKind.Up,new(fb.X+10,fb.Y+100),MouseButtons.Right),cmd);
        t.Equal(new Point(fb.X+11,fb.Y+101),menu.Location,"…which reopens it at the new cursor");
        menu.HideMenu();
        // 位置夾在 560×440（再加 1）。
        menu.ShowMenu(new(700,520),null,cmd);
        t.Equal(new Point(561,441),menu.Location,"cursor clamped to 560×440 then +1");
        menu.HideMenu();
        // 放開在窗外不出選單。
        field.Mouse(Mouse(UiMouseKind.Down,inside,MouseButtons.Right),cmd);
        field.Mouse(Mouse(UiMouseKind.Up,new(fb.Right+40,fb.Y),MouseButtons.Right),cmd);
        t.Check(!menu.MenuOpen&&!field.RightPressed,"release outside the map window: no menu, flag cleared");
        // UiInput 路徑：按下被 Field_Map 收下（capture），放開送回 Field_Map。
        var input=new UiInput(root,cmd){Viewport=UiPresenter.Compute(new(800,600))};
        input.Mouse(UiMouseKind.Down,inside,MouseButtons.Right,1);input.Mouse(UiMouseKind.Up,inside,MouseButtons.Right,1);
        t.Check(menu.MenuOpen,"through UiInput: right press + release on Field_Map opens the menu");

        // 5. 鈕 0＝TransferMenu：收起選單、聊天列「/loc 」、+9 旗標（標記 150 在選單 −4）；1785 畫在聊天輸入列。
        cmd.Calls.Clear();
        var at=menu.Location;var b0=Centre(menu.ButtonRect(MapCommunicationPanel.SendButton));
        t.Check(menu.Mouse(Mouse(UiMouseKind.Down,b0,MouseButtons.Left),cmd),"button 0 press");
        t.Check(menu.MenuOpen&&!chat.Input.Open,"nothing happens until release");
        menu.Mouse(Mouse(UiMouseKind.Up,b0,MouseButtons.Left),cmd);
        t.Check(!menu.MenuOpen&&chat.Input.Open&&chat.Input.Text==MapSendEntry.Command&&cmd.Calls.SequenceEqual(["begin"]),"release on button 0: menu hidden, chat input \"/loc \"",cmd.Calls);
        t.Check(menu.Prompting&&menu.Marker==new Point(at.X-4,at.Y-4),"marker (surf 150) at menu − 4 while prompting",menu.Marker);
        Frame();
        t.Check(prompt.Prompting&&menu.Prompting,"1785 prompt panel follows the entry");
        t.Check(!list.Visible,"no prompt row beside Field_Map any more");
        var drawn=new List<ChatDraw>{new(ChatPanel.Bar,2273,new(129,578,346,16),null),new("INPUT",null,new(133,580,30,12),"/loc ")};
        t.Equal(new Rectangle(133+30+MapSendPromptPanel.Gap,580,129+346-MapSendPromptPanel.BarMargin-(133+30+MapSendPromptPanel.Gap),12),MapSendPromptPanel.PromptRect(drawn),"1785 on the chat input line, right of the typed text, inside the bar");
        t.Check(MapSendPromptPanel.PromptRect([]).IsEmpty,"no drawn bar → no prompt rectangle (nothing guessed)");
        t.Check(MapSendPromptPanel.PromptRect([drawn[0],new("INPUT",null,new(129,580,346,12),"x")]).IsEmpty,"input already fills the bar → no room, not drawn");
        t.Check(MapSendPromptPanel.Text.Contains("1785",StringComparison.Ordinal)||LocationShareProtocol.Strings!=null,"prompt text is string 1785 (placeholder without string-c.tbl)",MapSendPromptPanel.Text);
        // ESC 由聊天列結束提示 → 旗標與標記清掉。
        t.Check(chat.Key(new(Keys.Escape,true,false),cmd)&&!chat.Input.Open,"ESC closes the chat prompt");
        Frame();
        t.Check(!menu.Prompting&&menu.Marker==null&&!prompt.Prompting&&!prompt.Visible,"prompt over → marker and 1785 gone");

        // 6. 鈕 1＝MapLocationSaveWindow：置中、調色盤、12 字、Enter 存進本地清單。
        menu.ShowMenu(inside,new(32810,32795),cmd);
        t.Equal(new MapLocationTarget(4,32810,32795),menu.Target,"target = map point under the cursor when the png is shown");
        var b1=Centre(menu.ButtonRect(MapCommunicationPanel.SaveButton));
        menu.Mouse(Mouse(UiMouseKind.Down,b1,MouseButtons.Left),cmd);menu.Mouse(Mouse(UiMouseKind.Up,b1,MouseButtons.Left),cmd);
        t.Check(!menu.MenuOpen&&save.Visible&&!menu.Prompting,"button 1: menu hidden, save window open, no prompt");
        t.Equal(new Rectangle(300,195,200,140),save.Bounds,"base 134 centred at ((800−w)/2, (530−h)/2)");
        t.Equal(menu.Target,save.Target,"save window keeps the menu's point");
        t.Equal(0,save.Color,"palette index starts at 0");
        t.Equal(new Point(311,238),save.SelectorAt(0),"selector 138 at (11,43)");
        var cell=new Point(300+11+19*3+5,195+43+19*1+5);
        save.Mouse(Mouse(UiMouseKind.Down,cell,MouseButtons.Left),cmd);save.Mouse(Mouse(UiMouseKind.Up,cell,MouseButtons.Left),cmd);
        t.Equal(11,save.Color,"click (col 3, row 1) → index 3 + 8×1");
        t.Equal(new Point(311+57,238+19),save.SelectorAt(save.Color),"selector follows the index");
        save.Mouse(Mouse(UiMouseKind.Down,new(300+5,195+5),MouseButtons.Left),cmd);
        t.Equal(11,save.Color,"click outside the palette keeps the index");
        t.Equal(Rectangle.FromLTRB(311,238,300+155+8,195+40+40),save.PaletteRect,"palette rect (11,43)–(w155+8, h155+40)");
        foreach(char c in "一二三四五六七八九十甲乙丙")save.Char(c);
        t.Equal("一二三四五六七八九十甲乙",save.Memo,"memo limited to 12 characters");
        t.Check(save.Key(new(Keys.Back,true,false),cmd)&&save.Memo.Length==11,"Backspace edits");
        t.Check(!save.Char('\r'),"control characters ignored");
        cmd.Calls.Clear();
        t.Check(save.Key(new(Keys.Enter,true,false),cmd)&&!save.Visible,"Enter = confirm, window closes");
        t.Check(save.Book.For(name).SequenceEqual([new MapLocationMark(4,32810,32795,11,"一二三四五六七八九十甲")]),"point stored for this character (map, x, y, colour, memo)",save.Book.For(name));
        t.Check(cmd.Calls.SequenceEqual(["game-focus"]),"focus handed back to the game",cmd.Calls);
        t.Check(save.Book.For("乙角色").Count==0,"other characters have their own list");
        // 確認鈕 2951：按下換 2953，放開在鈕上才確認。
        t.Check(save.Open(new(4,1,2)),"save window opens again");
        save.Mouse(Mouse(UiMouseKind.Down,Centre(save.ConfirmRect),MouseButtons.Left),cmd);
        save.Mouse(Mouse(UiMouseKind.Up,new(save.ConfirmRect.Right+30,save.ConfirmRect.Y),MouseButtons.Left),cmd);
        t.Check(save.Visible&&save.Book.For(name).Count==1,"release off the confirm button: nothing");
        t.Equal(new Rectangle(300+124,195+104,40,20),save.ConfirmRect,"confirm button at (124,104)");
        save.Mouse(Mouse(UiMouseKind.Down,Centre(save.ConfirmRect),MouseButtons.Left),cmd);save.Mouse(Mouse(UiMouseKind.Up,Centre(save.ConfirmRect),MouseButtons.Left),cmd);
        t.Check(!save.Visible&&save.Book.For(name).Count==2,"confirm button stores and closes");
        // 上限 30 筆：滿了不存、窗不關、顯示 1786。
        for(int i=save.Book.For(name).Count;i<MapLocationBook.MaxPerCharacter;i++)save.Book.Add(name,new(4,i,i,0,""));
        save.Open(new(4,9,9));
        t.Check(!save.Confirm(cmd)&&save.Visible&&save.ShowsFull&&save.Book.For(name).Count==MapLocationBook.MaxPerCharacter,"31st point refused: window stays, 1786 shown");
        t.Check(MapLocationSavePanel.FullText.Contains("1786",StringComparison.Ordinal)||LocationShareProtocol.Strings!=null,"full text is string 1786",MapLocationSavePanel.FullText);
        save.Char('x');t.Check(!save.ShowsFull,"typing clears 1786");
        t.Check(save.Key(new(Keys.Escape,true,false),cmd)&&!save.Visible,"ESC closes");
        using(var canvas=new UiCanvas()){
            save.Open(new(4,9,9));
            canvas.Clear(Color.Transparent);canvas.Restrict(save.Bounds);save.Paint(canvas);canvas.Release();
            var sb=save.Bounds;int ink=0,outside=0;
            for(int y=0;y<UiRoot.LogicalHeight;y+=2)for(int x=0;x<UiRoot.LogicalWidth;x+=2){if(canvas.Bitmap.GetPixel(x,y).A==0)continue;if(sb.Contains(x,y))ink++;else outside++;}
            t.Check(ink>0&&outside==0,"save window paints only inside its bounds",new{ink,outside});
            save.Close();
        }

        // 7. 堆疊關窗：存點窗 Close；選單 Close 也清掉標記。
        save.Open(new(4,9,9));((IUiClosablePanel)save).Close();
        t.Check(!save.Visible&&save.Bounds.IsEmpty,"save window: stack close");
        menu.ShowMenu(inside,null,cmd);menu.Activate(MapCommunicationPanel.SendButton,cmd);
        t.Check(menu.Prompting,"prompt running");
        ((IUiClosablePanel)menu).Close();
        t.Check(!menu.MenuOpen&&menu.Marker==null,"menu close: menu and marker gone");
        chat.Key(new(Keys.Escape,true,false),cmd);Frame();

        // 8. 缺圖就不畫：沒有鈕圖不開選單；沒有 128 時鈕 1 沒有位置；沒有 134 存點窗不開。
        var bare=new MapCommunicationPanel(root.Find);bare.Update(State());
        t.Check(!bare.ShowMenu(inside,null,cmd)&&!bare.MenuOpen,"no button surfs → no menu");
        var noSpacing=new MapCommunicationPanel(root.Find);noSpacing.Load(id=>id==MapCommunicationPanel.SpacingSurf?null:Surface(id));noSpacing.Update(State());
        t.Check(noSpacing.ShowMenu(inside,null,cmd)&&noSpacing.ButtonRect(MapCommunicationPanel.SaveButton).IsEmpty&&!noSpacing.ButtonRect(MapCommunicationPanel.SendButton).IsEmpty,"no surf 128 → button 1 has no position (not guessed)");
        var bareSave=new MapLocationSavePanel();bareSave.Update(State());
        t.Check(!bareSave.Open(new(4,1,1))&&!bareSave.Visible,"no surf 134 → save window does not open");

        // 9. 離開世界：選單、存點窗收起；Field_Map 關掉時選單也收起。
        menu.ShowMenu(inside,null,cmd);save.Open(new(4,1,1));
        field.Map.CloseField();Frame();
        t.Check(!menu.MenuOpen,"Field_Map closed → menu hidden");
        t.Check(save.Visible,"save window is its own window (stays while Field_Map closes)");
        inWorld=false;Frame();
        t.Check(!menu.Visible&&!save.Visible&&!prompt.Visible,"left the world → everything hidden");
        t.Check(!menu.ShowMenu(inside,null,cmd)&&!save.Open(new(4,1,1)),"outside the world nothing opens");
        inWorld=true;Frame();
        t.Check(!field.Mouse(Mouse(UiMouseKind.Down,inside,MouseButtons.Right),cmd)&&!field.RightPressed,"Field_Map closed: right press not taken");

        // 10. sub_A66F50：游標 → 地圖座標與 FieldMapEntry.ToPixel 互逆。兩邊都截斷：像素誤差 < 1，換回座標的誤差 ≤ 1.5/f + 1。
        foreach(float ratio in new[]{12f,144f}){
            var entry=new FieldMapEntry(4,10001,ratio,new(32768,32768),new(275,158));
            int tolerance=(int)Math.Ceiling(1.5/entry.Factor)+1;bool ok=true;
            foreach(var (x,y) in new[]{(32768,32768),(32800,32790),(32700,32850),(32900,32700)}){
                var back=FieldMapPanel.ToMapPoint(entry,entry.ToPixel(x,y));
                if(Math.Abs(back.X-x)>tolerance||Math.Abs(back.Y-y)>tolerance){ok=false;t.Check(false,"round trip",new{ratio,x,y,back});}
            }
            t.Check(ok,$"ToMapPoint inverts ToPixel (ratio {ratio}, within ±{tolerance})");
        }
        t.Check(field.MapPointAt(inside)==null,"no png loaded → no map point (menu falls back to the player position)");
    }
}
