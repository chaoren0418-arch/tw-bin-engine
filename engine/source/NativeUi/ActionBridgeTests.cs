namespace P06.NativeUi;

// R10-E（T389／#904）action-bridge 離線測試：只用合成版面、合成 25 項動作清單（名稱是識別字）與合成 surf（純色方塊），
// 不讀主客戶端資源、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
// 範圍：ActionRoutes 路線表（25 個動作）、面板退路（聊天列前綴、HP／MP 回復顯示）、常駐開窗鈕 ActionOpenButtonPanel、
//   S48 顯示方式的裁定（LoginNewsRoute）、產品 NativeUiCommands 實作 IUiActionCommands。
static class ActionBridgeTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    const string Ui="""
        <?xml version="1.0"?>
        <ActionUI Toggle="true">
          <Button Name="ActionUI_Button" X="774" Y="470" MostTop="1" BaseSurf="960;1" HighlightSurf="961;1" MouseEvent="OpenButtonPressed">
            <Tooltip Name="Desc_Tooltip" String="77"/>
          </Button>
          <Window Name="Action_Window" BaseSurf="900;1" Moveable="10" KeyboardEvent="KeyboardEvent" MouseEvent="Dummy">
            <ActionIconGrid Name="Grid" X="5" Y="25" Row="4" Column="6" UnitGap="1" UnitWidth="10" UnitHeight="10" Drag="1" MouseEvent="GridEvent"/>
            <Button Name="Exit_Button" X="90" Y="2" BaseSurf="920" PushSurf="921" MouseEvent="ExitButtonPressed"/>
          </Window>
        </ActionUI>
        """;
    static string List()=>"<?xml version=\"1.0\"?>\n<ActionList>\n"+string.Concat(ActionRoutes.Known.Select((n,i)=>
        $"  <Action action=\"{n}\" category=\"action\" control=\"action\" icon=\"{1000+i}\"/>\n"))+"</ActionList>\n";

    const uint ButtonBase=0xFF336699,ButtonHover=0xFF99CCFF;
    static readonly Dictionary<int,UiSurface> Surfs=new(){
        [900]=Surface(900,100,80,0xFF102030),[920]=Surface(920,6,6,0xFF404040),[921]=Surface(921,6,6,0xFFC0C0C0),
        [960]=Surface(960,20,24,ButtonBase),[961]=Surface(961,20,24,ButtonHover)};
    static UiSurface? Get(int id)=>Surfs.TryGetValue(id,out var s)?s:null;
    static UiLayout L(string text,string name)=>UiLayout.Parse(text,name);
    static ActionWindowPanel Loaded(UiRoot? root,Func<int,UiSurface?>? surf=null){
        var p=new ActionWindowPanel(null,root);p.Load(L(Ui,"ActionUI-c.xml"),L(List(),"Actions.xml"),surf??Get);return p;
    }
    static ActionEntry A(ActionWindowPanel p,string name)=>p.Actions.Single(a=>a.Name==name);
    static readonly UiStaticGameState World=new(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,int x,int y,int clicks=1,MouseButtons b=MouseButtons.Left)=>new(k,new Point(x,y),b,clicks,0,Keys.None);

    /// <summary>產品橋接的替身：只接 NativeUiCommands 會接的四種路線，其他回 false（交給面板）。</summary>
    sealed class Bridge:IUiChatCommands,IUiActionCommands {
        public readonly List<string> Calls=[];
        public bool AllowChat=true,TakeAll;
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool BeginChatInput(){Calls.Add("begin");return AllowChat;}
        public void EndChatInput()=>Calls.Add("end");
        public Task<bool> SendChat(string text,PlayerChatView displayed){Calls.Add("send:"+text);return Task.FromResult(true);}
        public bool RunAction(ActionEntry action){
            var r=ActionRoutes.Of(action.Name);
            bool take=TakeAll||r.Kind is ActionRouteKind.Emote or ActionRouteKind.Loot or ActionRouteKind.ChatCommand||r is {Kind:ActionRouteKind.Toggle,Argument:ActionRoutes.LootAllToggle};
            if(take)Calls.Add("run:"+action.Name);
            return take;
        }
    }

    [WorldModuleTest("action-bridge")]
    static void Run(WorldModuleTestContext t){
        Routes(t);
        PanelFallbacks(t);
        OpenButton(t);
        LoginNews(t);
        Product(t);
    }

    // 1. 路線表：25 個動作各一列；聊天指令與前綴都是既有斜線指令。
    static void Routes(WorldModuleTestContext t){
        var known=ActionRoutes.Known;
        t.Equal(25,known.Count,"25 actions (local Actions.xml)");
        t.Equal(25,known.Distinct(StringComparer.Ordinal).Count(),"names distinct");
        var kinds=known.GroupBy(n=>ActionRoutes.Of(n).Kind).ToDictionary(g=>g.Key,g=>g.Count());
        int K(ActionRouteKind k)=>kinds.TryGetValue(k,out int n)?n:0;
        t.Check(K(ActionRouteKind.Emote)==4&&K(ActionRouteKind.Loot)==1&&K(ActionRouteKind.ChatCommand)==3&&K(ActionRouteKind.ChatLine)==3
            &&K(ActionRouteKind.Toggle)==2&&K(ActionRouteKind.Window)==1&&K(ActionRouteKind.None)==11,"route counts 4/1/3/3/2/1/11",kinds);
        t.Check(known.Where(n=>ActionRoutes.Of(n).Kind==ActionRouteKind.None).All(n=>ActionRoutes.Of(n).Note is {Length:>0}),"every unwired action says why");
        for(int i=1;i<=4;i++)t.Check(ActionRoutes.Of("Action_SocialAction"+i) is {Kind:ActionRouteKind.Emote,Item:var item}&&item==i,"social action "+i+" → emote item "+i);
        t.Check(ActionWindowPanel.SocialActions.All(n=>ActionRoutes.Of(n).Kind==ActionRouteKind.Emote),"social actions are the emote routes");
        t.Check(ActionRoutes.Of("Action_Loot").Kind==ActionRouteKind.Loot,"loot → F4 path");
        t.Check(ChatFollowupCommands.Parse(ActionRoutes.Of("Action_Trade").Argument!) is {Kind:ChatFollowupKind.Trade,Argument:""},"trade → /trade");
        t.Check(ChatFollowupCommands.Parse(ActionRoutes.Of("Action_OpenShop").Argument!) is {Kind:ChatFollowupKind.Shop,Argument:""},"open shop → /shop");
        t.Check(ChatFollowupCommands.Parse(ActionRoutes.Of("Action_InviteParty").Argument!) is {Kind:ChatFollowupKind.Invite,Argument:""},"invite party → /invite without a name (R10-D pick entry)");
        foreach(var (name,kind) in new[]{("Action_InviteChatParty",ChatFollowupKind.ChatInvite),("Action_AddFriend",ChatFollowupKind.AddBuddy),("Action_Exclude",ChatFollowupKind.Exclude)}){
            var r=ActionRoutes.Of(name);
            t.Check(r.Kind==ActionRouteKind.ChatLine&&r.Argument!.EndsWith(' ')&&ChatFollowupCommands.Parse(r.Argument+"甲") is {} c&&c.Kind==kind&&c.Argument=="甲",name+" → chat-line prefix + name parses as "+kind);
        }
        t.Check(ActionRoutes.Of("Action_OpenBookmarkWin") is {Kind:ActionRouteKind.Window,Argument:BookmarkWindowPanel.PanelId},"bookmark → native bookmark window");
        t.Check(ActionRoutes.Of("Action_LootAll") is {Kind:ActionRouteKind.Toggle,Argument:ActionRoutes.LootAllToggle},"loot all → pickup-all toggle");
        t.Check(ActionRoutes.Of("Action_HPMPRecoveryEffect") is {Kind:ActionRouteKind.Toggle,Argument:ActionRoutes.HpMpRecoveryToggle,Note:{}},"HP/MP recovery → HpMp setting (推測, noted)");
        t.Check(ActionRoutes.Of("Action_BookmarkHere") is {Kind:ActionRouteKind.None,Note:{}},"unknown action → none");
        var p=Loaded(null);
        t.Check(p.Available&&p.Actions.Select(a=>a.Name).SequenceEqual(known),"synthetic 25-item list loads in order",p.Unavailable);
    }

    // 2. 面板：橋接優先；聊天列前綴與 HP／MP 回復顯示由面板做；沒接的不送、記 log。
    static void PanelFallbacks(WorldModuleTestContext t){
        var root=new UiRoot();var chat=new ChatPanel();root.Add(chat);var hp=new HpMpPanel();root.Add(hp);
        var p=Loaded(root);root.Add(p);chat.Update(World);
        var b=new Bridge();
        foreach(var name in ActionRoutes.Known){
            var r=ActionRoutes.Of(name);var result=p.Execute(A(p,name),b);
            var expected=r.Kind switch{
                ActionRouteKind.Emote or ActionRouteKind.Loot or ActionRouteKind.ChatCommand=>ActionRunResult.Bridge,
                ActionRouteKind.Toggle=>r.Argument==ActionRoutes.LootAllToggle?ActionRunResult.Bridge:ActionRunResult.Toggled,
                ActionRouteKind.ChatLine=>ActionRunResult.ChatLine,
                _=>ActionRunResult.Unhandled};   // bookmark: no bookmark window in this root
            t.Equal(expected,result,name+" → "+expected);
        }
        t.Check(!b.Calls.Any(c=>c.StartsWith("dev-tools",StringComparison.Ordinal)),"bridge present: no dev-tools fallback",b.Calls);
        t.Check(p.Unhandled.Count==12&&p.Unhandled.Contains("Action_Report")&&p.Unhandled.Contains("Action_OpenBookmarkWin")&&!p.Unhandled.Contains("Action_Loot"),"11 unwired + bookmark without window logged",p.Unhandled);

        // 聊天列前綴：開輸入列、取代草稿；不送封包。
        chat.Input.Close();chat.Input.Clear();b.Calls.Clear();
        t.Equal(ActionRunResult.ChatLine,p.Execute(A(p,"Action_AddFriend"),b),"add friend → chat line");
        t.Check(chat.Input.Open&&chat.Input.Text=="/addbuddy "&&b.Calls.SequenceEqual(["begin"]),"input opened with /addbuddy prefix, nothing sent",b.Calls);
        chat.Input.Type('x');
        t.Equal(ActionRunResult.ChatLine,p.Execute(A(p,"Action_Exclude"),b),"exclude → chat line");
        t.Check(chat.Input.Text=="/exclude "&&b.Calls.Count==1,"draft replaced; input already open → no second BeginChatInput",b.Calls);
        chat.Input.Pending=true;
        t.Equal(ActionRunResult.Unhandled,p.Execute(A(p,"Action_InviteChatParty"),b),"input line sending → not touched");
        t.Equal("/exclude ",chat.Input.Text,"pending line kept");
        chat.Input.Pending=false;chat.Input.Close();chat.Input.Clear();b.AllowChat=false;
        t.Equal(ActionRunResult.Unhandled,p.Execute(A(p,"Action_InviteChatParty"),b),"chat line may not open → unhandled");
        t.Check(!chat.Input.Open&&chat.Input.Text=="","closed line untouched");
        t.Equal(ActionRunResult.Unhandled,p.Execute(A(p,"Action_AddFriend"),new UiNullCommands()),"no chat commands → unhandled");
        var bare=Loaded(new UiRoot());
        t.Equal(ActionRunResult.Unhandled,bare.Execute(A(bare,"Action_AddFriend"),new Bridge()),"no chat window in the root → unhandled");

        // HP／MP 回復顯示：切換 HpMpPanel 的 ShowRecovery（推測對應）。
        bool before=hp.Settings.ShowRecovery;
        t.Equal(ActionRunResult.Toggled,p.Execute(A(p,"Action_HPMPRecoveryEffect"),new UiNullCommands()),"HP/MP recovery toggled by the panel");
        t.Check(hp.Settings.ShowRecovery==!before,"ShowRecovery flipped");
        p.Execute(A(p,"Action_HPMPRecoveryEffect"),new UiNullCommands());
        t.Check(hp.Settings.ShowRecovery==before,"flipped back");
        t.Equal(ActionRunResult.Unhandled,bare.Execute(A(bare,"Action_HPMPRecoveryEffect"),new UiNullCommands()),"no HP/MP window → unhandled");

        // 橋接接下就不走面板退路。
        var all=new Bridge{TakeAll=true};chat.Input.Close();chat.Input.Clear();
        t.Equal(ActionRunResult.Bridge,p.Execute(A(p,"Action_AddFriend"),all),"bridge first");
        t.Check(!chat.Input.Open&&all.Calls.SequenceEqual(["run:Action_AddFriend"]),"chat line untouched when the bridge takes it",all.Calls);
        // 沒有橋接時表情照舊退到開發工具。
        var none=new UiNullCommands();
        t.Equal(ActionRunResult.DevTools,p.Execute(A(p,"Action_SocialAction3"),none),"no bridge: emote → dev-tools page");
        t.Equal(ActionRunResult.Unhandled,p.Execute(A(p,"Action_Trade"),none),"no bridge: trade not sent");
    }

    static uint Pixel(IUiPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return (uint)canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static void Click(IUiPanel p,int x,int y,IUiCommands c,int clicks=1){p.Mouse(M(UiMouseKind.Down,x,y,clicks),c);p.Mouse(M(UiMouseKind.Up,x,y,clicks),c);}

    // 3. 常駐開窗鈕。
    static void OpenButton(WorldModuleTestContext t){
        var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==ActionOpenButtonPanel.PanelId);
        t.Check(reg!=null&&reg.Layout==ActionWindowPanel.HostLayout,"[UiPanel] discovered (host layout uistatus86)");
        t.Check(reg?.Create(new UiRoot()) is ActionOpenButtonPanel{Layer:UiLayer.Bar,Visible:false} and IUiClosablePanel,"Bar layer, closable, hidden without an action window");
        t.Check(UiPanelRegistry.Errors.Count==0,"no discovery errors",UiPanelRegistry.Errors);

        var root=new UiRoot();var w=Loaded(root);root.Add(w);var btn=new ActionOpenButtonPanel(root);root.Add(btn);var c=new UiNullCommands();
        t.Check(w.OpenButtonBase?.Surf==960&&w.OpenButtonHighlight?.Surf==961&&w.OpenButtonMissing.Count==0&&w.Missing.Count==0,"open button surfaces loaded apart from Missing",w.Missing);
        t.Check(!btn.Visible,"hidden before entering the world");
        btn.Update(World);
        t.Check(btn.Visible&&btn.Bounds==new Rectangle(774,470,20,24),"resident at (774,470), BaseSurf size",btn.Bounds);
        t.Equal(ButtonBase,Pixel(btn,780,480),"BaseSurf drawn");
        btn.Mouse(M(UiMouseKind.Move,780,480,0,MouseButtons.None),c);
        t.Check(btn.Hover,"hover");
        t.Equal(ButtonHover,Pixel(btn,780,480),"HighlightSurf while hovered");
        btn.Mouse(M(UiMouseKind.Move,10,10,0,MouseButtons.None),c);
        t.Equal(ButtonBase,Pixel(btn,780,480),"back to BaseSurf");
        t.Check(!btn.Mouse(M(UiMouseKind.Down,10,10),c),"click outside not taken");

        Click(btn,780,480,c);
        t.Check(w.Visible&&root.WindowStack.Contains(w),"click (514) opens the action window through UiRoot.Toggle");
        Click(btn,780,480,c);
        t.Check(!w.Visible&&!root.WindowStack.Contains(w),"click again closes it");
        btn.Mouse(M(UiMouseKind.Down,780,480),c);btn.Mouse(M(UiMouseKind.Up,10,10),c);
        t.Check(!w.Visible,"release outside the button does nothing");
        btn.Mouse(M(UiMouseKind.Down,780,480,1,MouseButtons.Right),c);btn.Mouse(M(UiMouseKind.Up,780,480,1,MouseButtons.Right),c);
        t.Check(!w.Visible,"right button does nothing");
        Click(btn,780,480,c,1);Click(btn,780,480,c,2);
        t.Check(w.Visible,"double click = 514, 515, 514 → three toggles → open");
        IUiClosablePanel closable=btn;closable.Close();
        t.Check(btn.Visible&&w.Visible,"Close only clears press state; button stays, window untouched");
        t.Check(root.CloseAll().SequenceEqual([w])&&btn.Visible,"global ESC closes the window, not the resident button");
        t.Check(c.Calls.Count==0,"no dev-tools fallback",c.Calls);

        btn.Update(new UiStaticGameState(InWorld:false));
        t.Check(!btn.Visible&&!btn.Mouse(M(UiMouseKind.Down,780,480),c),"outside the world: hidden, no input");

        var r2=new UiRoot();var w2=Loaded(r2,id=>id==960?null:Get(id));r2.Add(w2);var b2=new ActionOpenButtonPanel(r2);r2.Add(b2);b2.Update(World);
        t.Check(w2.Available&&w2.OpenButtonMissing.SequenceEqual(["surf 960 (ActionUI/ActionUI_Button)"])&&w2.Missing.Count==0,"missing button surf listed apart; window still usable",w2.OpenButtonMissing);
        t.Check(!b2.Visible&&b2.Bounds.IsEmpty,"no BaseSurf → button not drawn (no substitute)");
        var r3=new UiRoot();var b3=new ActionOpenButtonPanel(r3);r3.Add(b3);b3.Update(World);
        t.Check(!b3.Visible,"no action window → no button");
        var r4=new UiRoot();var w4=Loaded(r4,id=>id is 900?null:Get(id));r4.Add(w4);var b4=new ActionOpenButtonPanel(r4);r4.Add(b4);b4.Update(World);
        t.Check(!w4.Available&&!b4.Visible,"unavailable action window → no button");
    }

    // 4. S48：裁定（#880 第 2 點）照 TW16 用模態訊息框；Notice_Window 只在沒有訊息框時當備援。
    static void LoginNews(WorldModuleTestContext t){
        t.Equal(ClientForm.LoginNewsDisplay.MessageBox,ClientForm.LoginNewsRoute(true,true),"modal box available → message box even with Notice_Window");
        t.Equal(ClientForm.LoginNewsDisplay.MessageBox,ClientForm.LoginNewsRoute(true,false),"message box");
        t.Equal(ClientForm.LoginNewsDisplay.NoticeWindow,ClientForm.LoginNewsRoute(false,true),"no modal box → Notice_Window fallback");
        t.Equal(ClientForm.LoginNewsDisplay.MessageBox,ClientForm.LoginNewsRoute(false,false),"nothing native → message box");
        t.Check(ClientForm.LoginNewsModalBoxAvailable,"product: modal box always available");
        t.Equal(ClientForm.LoginNewsDisplay.MessageBox,ClientForm.LoginNewsRoute(ClientForm.LoginNewsModalBoxAvailable,true),"product default = TW16 modal message box");
    }

    // 5. 產品橋接型別。
    static void Product(WorldModuleTestContext t){
        t.Check(typeof(IUiActionCommands).IsAssignableFrom(typeof(ClientForm.NativeUiCommands)),"NativeUiCommands implements IUiActionCommands");
        t.Check(typeof(IUiChatCommands).IsAssignableFrom(typeof(ClientForm.NativeUiCommands)),"still the chat commands (UiChatGameState.Commands finds it)");
    }
}
