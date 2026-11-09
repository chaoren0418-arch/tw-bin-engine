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
public sealed record MenuButtonAction(int Action,string Title,string? DevToolsPage,string? Window=null,bool PlayStats=false);

/// <summary>One IMAGE_BUTTON as loaded: logical rectangle (null when the normal surf is missing) and its state surfaces.</summary>
public sealed record MenuButton(MenuButtonAction Action,int BaseSurf,Point Location,Rectangle? Rect,UiSurface? Normal,UiSurface? Pressed);

/// <summary>R3-A (#678): optional bridge command for menu action 7 / the play-stats window. false = not available (dev tools instead).</summary>
public interface IUiMenuCommands {
    bool TogglePlayStats();
}

public sealed class MenuButtonsPanel(UiRoot? root=null):IUiPanel {
    [UiPanel(130)] internal static UiPanelRegistration Registration=>new(MenuButtonsPanel.PanelId,"uistatus86",root=>new MenuButtonsPanel(root));   // ui-menu-buttons #467: after MacroPanel (buttons y=567 lie inside the 3176 frame; later Bar = painted/hit on top)
    public const string PanelId="RIGHT_WINDOW/BUTTON_2STAGE_LIST";
    public const string WindowNode="RIGHT_WINDOW",ListNode="BUTTON_2STAGE_LIST",ButtonNode="IMAGE_BUTTON";

    /// <summary>
    /// Original action → engine (R3-A #678; TW13 off_8F023C, FU4§1.1 — TW13 verified, 3.81 LiTo.bin packed so 推測 for 3.81):
    /// 13 Map → Field_Map (same as Ctrl+M); 3 Spell → SPELL_WINDOW; 4 Item → ITEM_WINDOW; 7 → CUserPlayStatsUI (play-stats
    /// window, NOT logout). No native counterpart registered yet (待驗證, dev tools instead): 1 NewMailWindow, 2 CRenewalStatusUI,
    /// 5 HTML option2/option2p (社群選項), 6 HTML option1 (功能選項). Page names are the existing dev-tools tab titles.
    /// </summary>
    public static readonly IReadOnlyDictionary<int,MenuButtonAction> Actions=new Dictionary<int,MenuButtonAction>{
        [13]=new(13,"Map","小地圖",FieldMapPanel.PanelId),
        [1]=new(1,"Mail","信件"),
        [2]=new(2,"Status","角色"),
        [3]=new(3,"Spell","魔法",SpellWindowPanel.WindowId),
        [4]=new(4,"Item","背包",ItemWindowPanel.WindowId),
        [5]=new(5,"Pledge","血盟"),
        [6]=new(6,"Option","選項"),
        [7]=new(7,"Quit",null,PlayStats:true),
    };

    /// <summary>
    /// Window hotkeys (FU4§1.1, TW13 sub_4D3910 WM_CHAR: Tab = Ctrl+I = 9 → InventoryWindow, Ctrl+S = 19 → SpellWindow).
    /// The original dispatches them globally after the window stack; this always-visible bar panel stands in for that dispatcher.
    /// The warehouse has no hotkey (opened only by the server list). Ctrl+A/H/V/C/Q/P are not wired yet (待辦).
    /// </summary>
    public static readonly IReadOnlyDictionary<Keys,int> Hotkeys=new Dictionary<Keys,int>{
        [Keys.Tab]=4,[Keys.Control|Keys.I]=4,[Keys.Control|Keys.S]=3,
    };
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
    /// </summary>
    public static void Invoke(MenuButtonAction action,IUiCommands commands,UiRoot? root){
        if(action.PlayStats){
            if(LegacyWindowPanel.FindCommands<IUiMenuCommands>(commands)?.TogglePlayStats()!=true)commands.ShowDevTools(action.DevToolsPage);
            return;
        }
        if(action.Window is {} id&&root?.Find(id) is {} panel&&!root.Faulted.Contains(panel)){
            switch(panel){
                case LegacyWindowPanel w:w.Toggle();return;
                // Original sub_77F510 shows message 1734 when there is no map; that message is not wired (待驗證), nothing opens.
                case FieldMapPanel f:f.ToggleFromMenu();return;
            }
        }
        commands.ShowDevTools(action.DevToolsPage);
    }

    /// <summary>Window hotkeys (see Hotkeys). Down toggles once (auto-repeat ignored); the matching up is swallowed too.</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!inWorld||!Hotkeys.TryGetValue(e.KeyData,out int action))return false;
        if(e.Down&&!e.Repeat)Invoke(Actions[action],commands,root);
        return true;
    }
}
