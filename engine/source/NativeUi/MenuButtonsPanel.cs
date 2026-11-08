namespace P06.NativeUi;

// ui-menu-buttons (Issue #467, docs/design/native-ui.md §5.3): the 8 menu buttons of RIGHT_WINDOW in uistatus86-c.xml
// (RIGHT_WINDOW/BUTTON_2STAGE_LIST/IMAGE_BUTTON: Map 13, Mail 1, Status 2, Spell 3, Item 4, Pledge 5, Option 6, Quit 7).
// Coordinates come only from the XML at run time; the macro frame (3176) belongs to the ui-macro-bar panel ("RIGHT_WINDOW").
// This panel's Bounds is the union of its button rectangles, so it never swallows clicks meant for the macro slots.

/// <summary>Where an original menu action goes in this engine. Page = dev-tools page hint (transitional, not original).</summary>
public sealed record MenuButtonAction(int Action,string Title,string? DevToolsPage,bool Logout);

/// <summary>One IMAGE_BUTTON as loaded: logical rectangle (null when the normal surf is missing) and its state surfaces.</summary>
public sealed record MenuButton(MenuButtonAction Action,int BaseSurf,Point Location,Rectangle? Rect,UiSurface? Normal,UiSurface? Pressed);

public sealed class MenuButtonsPanel:IUiPanel {
    public const string PanelId="RIGHT_WINDOW/BUTTON_2STAGE_LIST";
    public const string WindowNode="RIGHT_WINDOW",ListNode="BUTTON_2STAGE_LIST",ButtonNode="IMAGE_BUTTON";

    /// <summary>
    /// Original action → existing engine function. Only Quit has a native equivalent (IUiCommands.Logout = legacy HUD logout).
    /// Every other action opens the transitional dev-tools window (§4 "過渡"); the page names are the existing dev-tools tab
    /// titles where one exists ("角色"／"魔法"／"背包"／"血盟"). Not original behaviour — reported in docs/reports/issue-467.md.
    /// </summary>
    public static readonly IReadOnlyDictionary<int,MenuButtonAction> Actions=new Dictionary<int,MenuButtonAction>{
        [13]=new(13,"Map","小地圖",false),
        [1]=new(1,"Mail","信件",false),
        [2]=new(2,"Status","角色",false),
        [3]=new(3,"Spell","魔法",false),
        [4]=new(4,"Item","背包",false),
        [5]=new(5,"Pledge","血盟",false),
        [6]=new(6,"Option","選項",false),
        [7]=new(7,"Quit",null,true),
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
                if(fired!=null)Invoke(fired.Action,commands);
                return true;
            default:return false;
        }
    }

    public static void Invoke(MenuButtonAction action,IUiCommands commands){
        if(action.Logout)commands.Logout();
        else commands.ShowDevTools(action.DevToolsPage);
    }

    // Ctrl+M (same as Map) belongs to ui-minimap; this panel takes no keys.
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
