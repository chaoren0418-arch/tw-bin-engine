using System.Reflection;
using P06.NativeUi;
namespace P06;

// R5-H（T313／#740）地圖窗發送座標的離線測試：Field_Map 的 TransferMenu 入口（右鍵）→ 原生聊天輸入列「/loc 」＋1785 提示列，
// 輸入名稱、Enter 走既有 SendChat；ESC 取消；放大鈕（世界／村莊地圖，Q4-9 原版不建立）不開任何窗；
// op 132 S_SHOW_MAP 的 3.81 對應 S187 已由 AgitMapWindow（#550）接收。
// 只用合成狀態與記憶體內面板，不讀客戶端素材、不連線、不開視窗。由 --module-tests 執行（名稱 map-send）。
static class MapSendTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;

    sealed class ChatCommands:IUiChatCommands {
        public readonly List<string> Calls=[];
        public bool Allow=true;
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool BeginChatInput(){Calls.Add("begin");return Allow;}
        public void EndChatInput()=>Calls.Add("end");
        public Task<bool> SendChat(string text,PlayerChatView displayed){Calls.Add("send:"+text);return Task.FromResult(true);}
    }
    sealed class MapGame(bool inWorld):IUiGameState {
        public bool InWorld=>inWorld;
        public string? CharacterName=>"offline";
        public int? Level=>1;
        public long? Hp=>1;public long? MaxHp=>1;public long? Mp=>1;public long? MaxMp=>1;
        public long Now=>0;
    }
    static UiMouseEvent Mouse(UiMouseKind kind,Point at,MouseButtons button)=>new(kind,at,button,1,0,Keys.None);
    static PlayerChatView ChatView(bool canSend)=>new(1,1,123,1,canSend,"",[]);

    [WorldModuleTest("map-send")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：清單面板仍只一行，工廠帶 root 查找（裝得上入口）；S187（op 132 S_SHOW_MAP 的 3.81 對應）已註冊在 Agit。
        var reg=UiPanelRegistry.Panels.Single(r=>r.Id==LocationListPanel.PanelId);
        t.Check(UiPanelRegistry.Errors.Count==0,"[UiPanel] registrations valid",UiPanelRegistry.Errors);
        t.Check(reg.Create(new UiRoot()) is LocationListPanel{Entry:not null},"registered list panel carries the TransferMenu entry");
        t.Check(new LocationListPanel(()=>Rectangle.Empty).Entry==null,"offline construction without root finder: no entry");
        t.Check(WorldExtensionRegistry.Default.Entries.Any(e=>e.Kind=="receive"&&e.Opcode==AgitPackets.HouseMapOpcode&&e.Method=="WorldSession.ReceiveAgitHouseMap"),"S187 (TW16 op 132 S_SHOW_MAP) already received by Agit (#550)");

        // 2. 組合：UiRoot 內有 Field_Map、聊天面板、清單面板；清單面板 Update 時把入口裝到 Field_Map。
        UiMapSnapshot? current=new(4,32800,32800,3,[]);
        var source=new MapSource(()=>current);
        var root=new UiRoot();
        var field=new FieldMapPanel(new MapState(null,()=>source));
        var chat=new ChatPanel();
        root.Add(field);root.Add(chat);
        var list=new LocationListPanel(()=>field.Bounds,root.Find);root.Add(list);
        typeof(FieldMapPanel).GetField("size",F)!.SetValue(field,new Size(161,121));field.MoveTo(new(5,250));
        var cmd=new ChatCommands();
        IUiGameState State(bool inWorld=true,bool canSend=true,bool gameFocus=false)=>new UiChatGameState(new MapGame(inWorld),new(inWorld?ChatView(canSend):null,gameFocus));
        void Frame(IUiGameState s){field.Update(s);chat.Update(s);list.Update(s);}

        Frame(State());
        t.Check(field.TransferHandler!=null,"list panel installs the TransferMenu handler on Field_Map");
        var handler=field.TransferHandler;Frame(State());
        t.Check(ReferenceEquals(handler,field.TransferHandler)||handler==field.TransferHandler,"handler installed once (not replaced every frame)");
        t.Check(!field.Map.FieldOpen&&!field.TransferMenu(cmd)&&cmd.Calls.Count==0,"Field_Map closed → TransferMenu does nothing");
        field.Map.ToggleField();
        var b=field.Bounds;
        t.Equal(new Rectangle(5,250,161,121),b,"Field_Map open at (5,250)");
        t.Check(!list.Visible&&list.Bounds.IsEmpty,"no prompt and no rows → list hidden");

        // 3. 右鍵 = TransferMenu：開聊天輸入列、填「/loc 」，清單面板顯示 1785 提示列（不送封包）。
        var inside=new Point(b.X+60,b.Y+80);
        t.Check(field.Mouse(Mouse(UiMouseKind.Down,inside,MouseButtons.Right),cmd),"right click on Field_Map consumed");
        t.Check(cmd.Calls.SequenceEqual(["begin"])&&chat.Input.Open&&chat.Input.Text==MapSendEntry.Command,"TransferMenu opens chat input with \"/loc \"",cmd.Calls);
        Frame(State());
        t.Check(list.Prompting&&list.Visible,"prompt row shown while entering the recipient");
        t.Equal(1,(list.Bounds.Height-2*LocationListPanel.Padding)/LocationListPanel.RowHeight,"one prompt row, no list rows");
        t.Check(list.Bounds.Left>=b.Right&&list.Bounds.Right<=UiRoot.LogicalWidth&&list.Bounds.Width==LocationListPanel.PromptWidth,"prompt beside Field_Map, inside 800×600",list.Bounds);
        t.Check(LocationListPanel.PromptRowText.Contains("1785",StringComparison.Ordinal)||LocationShareProtocol.Strings!=null,"prompt is string 1785 (numbered placeholder without string-c.tbl)",LocationListPanel.PromptRowText);
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Transparent);canvas.Restrict(list.Bounds);list.Paint(canvas);canvas.Release();
            int ink=0,outside=0;var lb=list.Bounds;
            for(int y=0;y<UiRoot.LogicalHeight;y++)for(int x=0;x<UiRoot.LogicalWidth;x++){var px=canvas.Bitmap.GetPixel(x,y);if(px.A==0)continue;if(lb.Contains(x,y))ink++;else outside++;}
            t.Check(ink>0&&outside==0,"prompt drawn only inside bounds",new{ink,outside});
            t.Check(Enumerable.Range(lb.X,lb.Width).All(x=>canvas.Bitmap.GetPixel(x,lb.Top).A==0),"top padding row stays transparent (text only, no background or frame)");
        }
        t.Check(list.RowAt(new(list.Bounds.X+5,list.Bounds.Y+LocationListPanel.Padding+3))==null,"prompt row is not a selectable list row");

        // 4. 輸入名稱、Enter → 既有 SendChat 送「/loc 名稱」（之後由 SendPlayerChat → SendLocation 送 C254/11）。
        foreach(char ch in "甲乙")chat.Char(ch);
        t.Check(chat.Key(new(Keys.Enter,true,false),cmd),"Enter handled by chat panel");
        t.Check(cmd.Calls.Contains("send:/loc 甲乙"),"Enter sends \"/loc 甲乙\" through the existing chat path",cmd.Calls);
        chat.Key(new(Keys.Enter,false,false),cmd);
        t.Check(chat.Input.Text.Length==0,"sent line cleared");
        Frame(State());
        t.Check(!list.Prompting,"prompt row gone after sending");

        // 5. ESC 取消；已開輸入列（有草稿）時入口取代草稿、不再呼叫 BeginChatInput。
        cmd.Calls.Clear();
        if(!chat.Input.Open){chat.Input.Begin(false);}
        foreach(char ch in "草稿")chat.Input.Type(ch);
        t.Check(field.TransferMenu(cmd)&&chat.Input.Text==MapSendEntry.Command&&!cmd.Calls.Contains("begin"),"open input: draft replaced by \"/loc \"",cmd.Calls);
        t.Check(chat.Key(new(Keys.Escape,true,false),cmd)&&!chat.Input.Open&&cmd.Calls.Contains("end"),"ESC closes the prompt like the chat line");
        Frame(State());t.Check(!list.Prompting&&!list.Visible,"prompt hidden after ESC");

        // 6. 拒絕：不能發送、聊天列不讓開、不在世界、沒有目前地圖 → 什麼都不做。
        chat.Input.Clear();cmd.Calls.Clear();
        Frame(State(canSend:false));
        t.Check(!field.TransferMenu(cmd)&&!chat.Input.Open&&cmd.Calls.Count==0,"CanSend false → nothing opens");
        Frame(State());cmd.Allow=false;
        t.Check(!field.TransferMenu(cmd)&&!chat.Input.Open&&cmd.Calls.SequenceEqual(["begin"]),"BeginChatInput refused → input stays closed",cmd.Calls);
        cmd.Allow=true;cmd.Calls.Clear();
        t.Check(!field.TransferMenu(new UiNullCommands())&&!chat.Input.Open,"commands without chat bridge → nothing");
        current=null;Frame(State(inWorld:false));
        t.Check(!field.Map.FieldOpen&&!field.TransferMenu(cmd)&&!list.Visible&&cmd.Calls.Count==0,"left the world → Field_Map closed, entry inert");
        current=new(4,32800,32800,3,[]);Frame(State());field.Map.ToggleField();

        // 7. 其他滑鼠：左鍵不觸發入口；放大鈕（Q4-9：原版不建立世界／村莊地圖）、關閉鈕上的右鍵不觸發；窗外右鍵不吃。
        cmd.Calls.Clear();
        field.Mouse(Mouse(UiMouseKind.Down,inside,MouseButtons.Left),cmd);field.Mouse(Mouse(UiMouseKind.Up,inside,MouseButtons.Left),cmd);
        t.Check(!chat.Input.Open&&cmd.Calls.Count==0,"left click on the map does not start TransferMenu");
        t.Check(!field.Mouse(Mouse(UiMouseKind.Down,new(b.Right+30,b.Y+5),MouseButtons.Right),cmd)&&!chat.Input.Open,"right click outside Field_Map not consumed");
        t.Check(field.ZoomRect.IsEmpty&&field.ExitRect.IsEmpty,"without surfs exit/zoom have no rect (checked with chrome in MiniMapChecks)");

        // 8. 原生 UI 關閉時的入口仍是 /loc 聊天指令（WinForms HUD）。
        t.Check(PlayerChatProtocol.Valid("/loc 甲"),"/loc remains a valid chat command (fallback entry when native UI is off)");
    }
    sealed class MapSource(Func<UiMapSnapshot?> read):IUiMapSource { public UiMapSnapshot? ReadMap()=>read(); }
}
