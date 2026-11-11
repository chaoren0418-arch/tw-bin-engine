namespace P06.NativeUi;

// ui-menu-buttons (Issue #467, docs/design/native-ui.md §5.3): the 8 menu buttons of RIGHT_WINDOW in uistatus86-c.xml
// (RIGHT_WINDOW/BUTTON_2STAGE_LIST/IMAGE_BUTTON: Map 13, Mail 1, Status 2, Spell 3, Item 4, Pledge 5, Option 6, Quit 7).
// Coordinates come only from the XML at run time; the macro frame (3176) belongs to the ui-macro-bar panel ("RIGHT_WINDOW").
// This panel's Bounds is the union of its button rectangles, so it never swallows clicks meant for the macro slots.

/// <summary>
/// Where an original menu action goes in this engine. Window = native panel id the button toggles (null = none registered);
/// DevToolsPage = dev-tools page used when no native window takes the click (transitional, not original behaviour).
/// PlayStats = action 7 (CUserPlayStatsUI, FU4§1.1): toggled through IUiMenuCommands.
/// </summary>
/// R4-A (#705): Mail = action 1 (NewMailWindow → WinForms MailDialog through IUiMenuWindowCommands.OpenMail);
/// Page = local HTML page of actions 5／6 (option2／option2p, option1; FU4§1.1) shown by IUiMenuWindowCommands.ToggleLocalPage.
/// R7-I (#811): Status = action 2 (CRenewalStatusUI): toggles the native RenewalStatusPanel through UiRoot.Toggle when it is
/// available; DevToolsPage only when it is not (no reference ReNStatusUIEx.xml, faulted, no root).
/// R9-Q (#861, #818 ruling): Option = action 6 (and Ctrl+V) toggles the native OptionPanel when it is available; HTML option1
/// (Page) only when it is not. --classic-ui builds no native UI (so no RIGHT_WINDOW and none of these hotkeys through this
/// panel); there the HTML pages are reached through the dev-tools 「NPC」 tab buttons and the classic Ctrl+H／V／C／Q／P,
/// Alt+X branch in DevToolsForm.cs (T433 #991, classic-ui-audit G6／G8), both calling the same ToggleMenuLocalPage.
public sealed record MenuButtonAction(int Action,string Title,string? DevToolsPage,string? Window=null,bool PlayStats=false,bool Mail=false,MenuLocalPage? Page=null,bool Status=false);

/// <summary>One IMAGE_BUTTON as loaded: logical rectangle (null when the normal surf is missing) and its state surfaces.</summary>
public sealed record MenuButton(MenuButtonAction Action,int BaseSurf,Point Location,Rectangle? Rect,UiSurface? Normal,UiSurface? Pressed);

/// <summary>R3-A (#678): optional bridge command for menu action 7 / the play-stats window. false = not available (dev tools instead).</summary>
public interface IUiMenuCommands {
    bool TogglePlayStats();
}

/// <summary>R4-A (#705): local HTML pages opened by the client itself (Text.pak help／option1／option2／option2p, FU4§1.1).</summary>
public enum MenuLocalPage { Help, Option1, Option2 }

/// <summary>R4-A (#705): what a non-window hotkey does (FU4§1.1 WM_CHAR table).</summary>
public enum MenuHotkeyCommand { Status, PlayStats, Help, Option1, Option2, Screenshot }

/// <summary>
/// R4-A (#705): optional bridge commands for mail, the local HTML pages and the screenshot. Kept apart from IUiMenuCommands so
/// command objects that only know play stats keep compiling. Every method returns false when it could not act (the caller then
/// falls back to the dev-tools page, or does nothing for the screenshot).
/// </summary>
public interface IUiMenuWindowCommands {
    /// <summary>Action 1: opens the mail window (WinForms MailDialog until a native RenewalMailUI exists, WIN2§8).</summary>
    bool OpenMail();
    /// <summary>Actions 5／6, Ctrl+H／V／C: shows the page, or closes it when the same page is already shown (the original toggles).</summary>
    bool ToggleLocalPage(MenuLocalPage page);
    /// <summary>Ctrl+P: saves the current 800×600 screen under &lt;stage&gt;/runtime/Capture (never the game folder).</summary>
    bool CaptureScreen();
}

public sealed class MenuButtonsPanel(UiRoot? root=null):IUiPanel {
    [UiPanel(130)] internal static UiPanelRegistration Registration=>new(MenuButtonsPanel.PanelId,"uistatus86",root=>new MenuButtonsPanel(root));   // ui-menu-buttons #467: after MacroPanel (buttons y=567 lie inside the 3176 frame; later Bar = painted/hit on top)
    public const string PanelId="RIGHT_WINDOW/BUTTON_2STAGE_LIST";
    public const string WindowNode="RIGHT_WINDOW",ListNode="BUTTON_2STAGE_LIST",ButtonNode="IMAGE_BUTTON";

    /// <summary>
    /// Original action → engine (R3-A #678; TW13 off_8F023C, FU4§1.1 — TW13 verified, 3.81 LiTo.bin packed so 推測 for 3.81):
    /// 13 Map → Field_Map (same as Ctrl+M); 3 Spell → SPELL_WINDOW; 4 Item → ITEM_WINDOW; 7 → CUserPlayStatsUI (play-stats
    /// window, NOT logout). R4-A (#705): 1 NewMailWindow → mail dialog (WinForms, RenewalMailUI XML missing locally, WIN2§8);
    /// 5 HTML option2 (王族 option2p, 社群選項) and 6 HTML option1 (功能選項) → the same-named Text.pak page in the NPC panel until
    /// R4-I's native HYPER_TEXT_WINDOW; 2 CRenewalStatusUI → native RenewalStatusPanel (R7-I #811; ReNStatusUIEx.xml is read
    /// from the reference client, #766), dev-tools 「角色」 page only while that window is unavailable.
    /// DevToolsPage is the fallback when the bridge cannot act (dev-tools tab title; no such tab → logged as 待驗證 by the bridge).
    /// </summary>
    public static readonly IReadOnlyDictionary<int,MenuButtonAction> Actions=new Dictionary<int,MenuButtonAction>{
        [13]=new(13,"Map","小地圖",FieldMapPanel.PanelId),
        [1]=new(1,"Mail","信件",Mail:true),
        [2]=new(2,"Status","角色",Status:true),
        [3]=new(3,"Spell","魔法",SpellWindowPanel.WindowId),
        [4]=new(4,"Item","背包",ItemWindowPanel.WindowId),
        [5]=new(5,"Pledge","血盟",Page:MenuLocalPage.Option2),
        [6]=new(6,"Option","選項",Page:MenuLocalPage.Option1),
        [7]=new(7,"Quit",null,PlayStats:true),
    };

    /// <summary>
    /// Window hotkeys (FU4§1.1, TW13 sub_4D3910 WM_CHAR: Tab = Ctrl+I = 9 → InventoryWindow, Ctrl+S = 19 → SpellWindow).
    /// The original dispatches them globally after the window stack; this always-visible bar panel stands in for that dispatcher.
    /// The warehouse has no hotkey (opened only by the server list). The other keys are in CommandHotkeys.
    /// </summary>
    public static readonly IReadOnlyDictionary<Keys,int> Hotkeys=new Dictionary<Keys,int>{
        [Keys.Tab]=4,[Keys.Control|Keys.I]=4,[Keys.Control|Keys.S]=3,
    };

    /// <summary>
    /// R4-A (#705), FU4§1.1 WM_CHAR table (TW13 sub_4D3910; TW16 sub_8C14F0): Ctrl+A (1) CRenewalStatusUI, Ctrl+Q (17) and
    /// Alt+X (0x8058) CUserPlayStatsUI, Ctrl+H (8, Ctrl held — Backspace is not Ctrl+H here) HTML help, Ctrl+V (22) HTML option1,
    /// Ctrl+C (3) HTML option2／option2p, Ctrl+P (16) screenshot. Ctrl+O／B／E are not wired (no engine window).
    /// Ctrl+D (4) is handled apart from this table (see <see cref="ActionWindowPanel.Hotkey"/>, R8-D #823).
    /// </summary>
    public static readonly IReadOnlyDictionary<Keys,MenuHotkeyCommand> CommandHotkeys=new Dictionary<Keys,MenuHotkeyCommand>{
        [Keys.Control|Keys.A]=MenuHotkeyCommand.Status,
        [Keys.Control|Keys.Q]=MenuHotkeyCommand.PlayStats,[Keys.Alt|Keys.X]=MenuHotkeyCommand.PlayStats,
        [Keys.Control|Keys.H]=MenuHotkeyCommand.Help,
        [Keys.Control|Keys.V]=MenuHotkeyCommand.Option1,[Keys.Control|Keys.C]=MenuHotkeyCommand.Option2,
        [Keys.Control|Keys.P]=MenuHotkeyCommand.Screenshot,
    };

    /// <summary>Text.pak page name (without "-c.html"): option2p for class type 0 (王族, FU4§1.1 menu 5), option2 otherwise.</summary>
    public static string PageKey(MenuLocalPage page,int? classType)=>page switch{
        MenuLocalPage.Help=>"help",MenuLocalPage.Option1=>"option1",
        _=>classType==0?"option2p":"option2"};

    /// <summary>Screenshot folder: &lt;stage&gt;/runtime/Capture (the original writes Capture\ under the game folder; we never do).</summary>
    public static string CaptureDirectory(string stageRoot)=>Path.Combine(stageRoot,"runtime","Capture");

    /// <summary>
    /// Screenshot file: local time yyyyMMdd-HHmmss-fff.png, "-2", "-3"… when the name is taken. The original's file name and
    /// format were not checked (it prints "Saved current screen to %s"; 待驗證).
    /// </summary>
    public static string CapturePath(string stageRoot,DateTime now,Func<string,bool> exists){
        string dir=CaptureDirectory(stageRoot),stem=now.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture);
        string path=Path.Combine(dir,stem+".png");
        for(int i=2;exists(path);i++)path=Path.Combine(dir,$"{stem}-{i}.png");
        return path;
    }
    readonly List<MenuButton> buttons=[];
    MenuButton? pressed;bool pressedInside;bool inWorld;
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Bar;
    public Rectangle Bounds{get;private set;}
    public bool Visible=>inWorld&&!Bounds.IsEmpty;
    public IReadOnlyList<MenuButton> Buttons=>buttons;
    /// <summary>Button currently held down (left press not yet released); for checks and diagnostics.</summary>
    public MenuButton? Held=>pressed;

    /// <summary>
    /// V4 (b) (TW13 verified, TW16 inferred; docs/reference/ui-layout.md §1): IMAGE_BUTTON in BUTTON_2STAGE_LIST is
    /// 3Stage(a, a+1) → normal a, hover a+1, pressed a+1. surfNumber is parsed like the BIN's atoi: only the first number.
    /// </summary>
    public static (int Normal,int Pressed) StateSurfs(int baseSurf)=>(baseSurf,baseSurf+1);

    public void Load(UiLayout layout,SurfCatalog surfs){
        buttons.Clear();pressed=null;
        var window=layout.Root.Child(WindowNode);
        int ox=window.Int("x"),oy=window.Int("y");
        var list=window.Child(ListNode);
        foreach(var n in list.ChildrenNamed(ButtonNode)){
            int action=n.Int("action");
            if(!Actions.TryGetValue(action,out var mapped))throw new UiLayoutException(layout.FileName,n.Path,$"不明的選單 action {action}");
            var range=n.Surfs();
            if(range.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"選單按鈕沒有 surfNumber");
            var (a,b)=StateSurfs(range[0]);
            var location=new Point(ox+n.Int("xRPos"),oy+n.Int("yRPos"));
            var normal=surfs.Get(a);var down=surfs.Get(b);
            // No normal surf → size unknown: not drawn and not clickable (no invented hit box, rule 5).
            Rectangle? rect=normal==null?null:new Rectangle(location,new Size(normal.Width,normal.Height));
            buttons.Add(new MenuButton(mapped,range[0],location,rect,normal,down));
        }
        if(buttons.Count==0)throw new UiLayoutException(layout.FileName,list.Path,"沒有任何 IMAGE_BUTTON");
        var dup=buttons.GroupBy(x=>x.Action.Action).FirstOrDefault(g=>g.Count()>1);
        if(dup!=null)throw new UiLayoutException(layout.FileName,list.Path,$"action {dup.Key} 重複");
        Bounds=buttons.Where(x=>x.Rect!=null).Select(x=>x.Rect!.Value).DefaultIfEmpty(Rectangle.Empty).Aggregate(Rectangle.Union);
    }

    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        if(!inWorld){pressed=null;pressedInside=false;}
    }

    public void Paint(UiCanvas canvas){
        foreach(var b in buttons){
            if(b.Rect==null)continue;
            // A missing state surf draws nothing (no substitute image).
            canvas.DrawSurface(b==pressed&&pressedInside?b.Pressed:b.Normal,b.Location.X,b.Location.Y);
        }
    }

    public MenuButton? ButtonAt(Point p)=>buttons.FirstOrDefault(b=>b.Rect is Rectangle r&&r.Contains(p));

    /// <summary>
    /// TW13 sub_50ACA0: left down → pressed; left up → clear and fire the click. The click fires only when released over the
    /// same button (inferred). Hover (a+1) is not drawn: UiInput gives panels no "mouse left" event, so a hover image could stick.
    /// </summary>
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        switch(e.Kind){
            case UiMouseKind.Down:
                if(e.Button!=MouseButtons.Left)return false;
                pressed=ButtonAt(e.Location);pressedInside=pressed!=null;
                return pressed!=null;
            case UiMouseKind.Move:
                if(pressed!=null)pressedInside=pressed.Rect!.Value.Contains(e.Location);
                return pressed!=null;
            case UiMouseKind.Up:
                if(pressed==null||e.Button!=MouseButtons.Left)return false;
                var fired=pressed.Rect!.Value.Contains(e.Location)?pressed:null;
                pressed=null;pressedInside=false;
                if(fired!=null)Invoke(fired.Action,commands,root);
                return true;
            default:return false;
        }
    }

    /// <summary>Button click without a root (no native windows to toggle): play stats through the bridge, otherwise dev tools.</summary>
    public static void Invoke(MenuButtonAction action,IUiCommands commands)=>Invoke(action,commands,null);

    /// <summary>
    /// Toggles the native window of the action when it is registered and usable; otherwise opens the dev-tools page (transition,
    /// 待驗證). Never logs out (action 7 was a logout before #678; the original opens the play-stats window).
    /// R7-I (#811): native windows go through UiRoot.Toggle (sub_A57C60 for one window, R5-A #733): a window in the stack
    /// closes, a window pushed out of the stack (evicted / refused) comes back on top instead of being closed, and the
    /// 509 rule is applied before the window opens.
    /// </summary>
    public static void Invoke(MenuButtonAction action,IUiCommands commands,UiRoot? root){
        if(action.PlayStats){
            if(LegacyWindowPanel.FindCommands<IUiMenuCommands>(commands)?.TogglePlayStats()!=true)commands.ShowDevTools(action.DevToolsPage);
            return;
        }
        if(action.Status){
            if(StatusWindow(root) is {} status)ToggleWindow(root!,status,()=>status.Open(),()=>status.Toggle());
            else commands.ShowDevTools(action.DevToolsPage);
            return;
        }
        // R9-Q (#861, #818 ruling): action 6 / Ctrl+V open the native OptionUI when it can open; otherwise HTML option1 below.
        if(action.Page==MenuLocalPage.Option1&&OptionWindow(root) is {} option){
            ToggleWindow(root!,option,()=>option.Open(),()=>option.Toggle());
            return;
        }
        // R4-A (#705): mail and the local HTML pages go through the bridge; not available → the dev-tools page as before.
        if(action.Mail||action.Page!=null){
            var bridge=LegacyWindowPanel.FindCommands<IUiMenuWindowCommands>(commands);
            bool done=action.Mail?bridge?.OpenMail()==true:bridge?.ToggleLocalPage(action.Page!.Value)==true;
            if(!done)commands.ShowDevTools(action.DevToolsPage);
            return;
        }
        if(action.Window is {} id&&root?.Find(id) is {} panel&&!root.Faulted.Contains(panel)){
            switch(panel){
                case LegacyWindowPanel w:ToggleWindow(root,w,w.Open,w.Toggle);return;
                // Original sub_77F510 shows message 1734 when there is no map; that message is not wired (待驗證), nothing opens.
                case FieldMapPanel f:ToggleWindow(root,f,()=>f.ToggleFromMenu(),()=>f.ToggleFromMenu());return;
            }
        }
        commands.ShowDevTools(action.DevToolsPage);
    }

    /// <summary>R7-I (#811): the native status window when it can open (registered, reference XML loaded, not faulted); else null.</summary>
    public static RenewalStatusPanel? StatusWindow(UiRoot? root)=>
        root?.Find(RenewalStatusPanel.PanelId) is RenewalStatusPanel p&&p.Available&&!root.Faulted.Contains(p)?p:null;

    /// <summary>R9-Q (#861): the native option window when it can open (registered, reference OptionUI.xml loaded, not faulted); else null.</summary>
    public static OptionPanel? OptionWindow(UiRoot? root)=>
        root?.Find(OptionPanel.PanelId) is OptionPanel p&&p.Available&&!root.Faulted.Contains(p)?p:null;

    /// <summary>
    /// R7-I (#811): root.Toggle with the panel's own open. Only when the root could not even try (the panel is not one of its
    /// windows) does the panel toggle itself as before; once open has run, the root's answer stands (an Unavailable result
    /// after open = the panel did not open, e.g. Field_Map without a current map — nothing else happens).
    /// </summary>
    public static UiToggleResult ToggleWindow(UiRoot root,IUiPanel panel,Action open,Action ownToggle){
        bool opened=false;
        var result=root.Toggle(panel,()=>{opened=true;open();});
        if(result==UiToggleResult.Unavailable&&!opened&&!root.Faulted.Contains(panel))ownToggle();
        return result;
    }

    /// <summary>
    /// R4-A (#705): runs one CommandHotkeys entry. Ctrl+A／Ctrl+Q／Alt+X／Ctrl+V／Ctrl+C reuse the menu actions 2／7／6／5 (same
    /// fallbacks); Ctrl+H has no menu button (action 0 is commented out in the XML) and falls back to the dev-tools NPC page;
    /// Ctrl+P without a bridge does nothing.
    /// </summary>
    public static void Run(MenuHotkeyCommand command,IUiCommands commands,UiRoot? root){
        switch(command){
            case MenuHotkeyCommand.Status:Invoke(Actions[2],commands,root);break;
            case MenuHotkeyCommand.PlayStats:Invoke(Actions[7],commands,root);break;
            case MenuHotkeyCommand.Option1:Invoke(Actions[6],commands,root);break;
            case MenuHotkeyCommand.Option2:Invoke(Actions[5],commands,root);break;
            case MenuHotkeyCommand.Help:
                if(LegacyWindowPanel.FindCommands<IUiMenuWindowCommands>(commands)?.ToggleLocalPage(MenuLocalPage.Help)!=true)commands.ShowDevTools("NPC");
                break;
            case MenuHotkeyCommand.Screenshot:LegacyWindowPanel.FindCommands<IUiMenuWindowCommands>(commands)?.CaptureScreen();break;
        }
    }

    /// <summary>Window hotkeys (see Hotkeys, CommandHotkeys). Down acts once (auto-repeat ignored); the matching up is swallowed too.</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!inWorld)return false;
        if(Hotkeys.TryGetValue(e.KeyData,out int action)){
            if(e.Down&&!e.Repeat)Invoke(Actions[action],commands,root);
            return true;
        }
        if(CommandHotkeys.TryGetValue(e.KeyData,out var command)){
            if(e.Down&&!e.Repeat)Run(command,commands,root);
            return true;
        }
        // R8-D (#823): Ctrl+D (TW16 WM_CHAR 4 → BehaviourUI, FU4§1.1; 3.81 推測) toggles the native action window through
        // UiRoot.Toggle. No usable action window (no ActionUI XML, faulted, no root) → not taken, the key goes on as before.
        if(e.KeyData==ActionWindowPanel.Hotkey&&ActionWindowPanel.Window(root) is {} actions){
            if(e.Down&&!e.Repeat)ToggleWindow(root!,actions,()=>actions.Open(),actions.Toggle);
            return true;
        }
        return false;
    }
}
