namespace P06.NativeUi;

/// <summary>
/// Field_Map window (MapSystem-c.xml, ui-layout.md §5): frame 2816 at pos (5,250), title 2815, exit 2801 (146,6),
/// zoom 2803 (129,6); 160×120 png view centred on the player, party dots 2826, 8-direction arrow 2818–2825.
/// Maps outside the Field_Map list use the Dungern_Map chrome (title 2888, no zoom) and show no map content.
/// Opened/closed by Ctrl+M (V5 (b)) or the Map menu button (action 13, MenuButtonsPanel → ToggleFromMenu, R3-A #678).
/// Created by MiniMapPanel.Create (one registry line for the module) and loaded from MiniMapPanel.Load.
/// R5-H (T313 #740): right click = TransferMenu (send location to a character, <see cref="TransferMenu"/>).
/// op 132 S_SHOW_MAP (PG§5) maps to 3.81 S187 HouseMap, which already opens AgitMapWindow (W2-C #550, AgitMapPanel), not this window.
/// </summary>
public sealed class FieldMapPanel(MapState map):IUiMovablePanel,IUiClosablePanel {
    public const string PanelId="Field_Map";
    public const Keys ToggleKey=Keys.Control|Keys.M;
    SurfCatalog? surfs;
    MapWindowLayout? fieldChrome,dungeonChrome;
    Point location;Size size;
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public MapState Map=>map;
    /// <summary>Closed → empty Bounds (nothing painted or hit) while staying Visible, so Ctrl+M still reaches the panel.</summary>
    public Rectangle Bounds=>map.FieldOpen?new(location,size):Rectangle.Empty;
    public bool Visible=>true;
    /// <summary>Field chrome for listed maps, Dungern_Map chrome otherwise. (Members are not named "field": in C# 14 that is a keyword inside accessors.)</summary>
    public MapWindowLayout? Chrome=>map.Kind==MapViewKind.Field?fieldChrome:dungeonChrome;

    public void Load(UiLayout layout,SurfCatalog surfs){
        var data=MapSystemData.Parse(layout);
        map.Load(data);this.surfs=surfs;fieldChrome=data.Field;dungeonChrome=data.Dungeon;location=data.Field.Pos;
        var frame=surfs.Get(data.Field.Frame.Surf);
        size=frame==null?Size.Empty:new(frame.Width,frame.Height);
    }
    public void Update(IUiGameState state)=>map.Update(state);

    public void Paint(UiCanvas canvas){
        if(!map.FieldOpen||Chrome is not {} chrome)return;
        Draw(canvas,chrome.Frame);
        void Buttons(){Draw(canvas,chrome.Title);Draw(canvas,chrome.Exit);if(chrome.Zoom!=null)Draw(canvas,chrome.Zoom);}
        if(map.View(location,false) is {} view)map.PaintMap(canvas,surfs,view,Buttons);else Buttons();
    }
    void Draw(UiCanvas canvas,MapSystemImage image)=>canvas.DrawSurface(surfs?.Get(image.Surf),location.X+image.Pos.X,location.Y+image.Pos.Y);

    /// <summary>Logical rectangle of a chrome image (empty when its surf is missing).</summary>
    public Rectangle Rect(MapSystemImage? image){
        if(image==null||surfs?.Get(image.Surf) is not {} s)return Rectangle.Empty;
        return new(location.X+image.Pos.X,location.Y+image.Pos.Y,s.Width,s.Height);
    }
    public Rectangle ExitRect=>Rect(Chrome?.Exit);
    public Rectangle ZoomRect=>Rect(Chrome?.Zoom);

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!map.FieldOpen)return false;
        if(e.Kind==UiMouseKind.Down&&e.Button==MouseButtons.Left){
            if(ExitRect.Contains(e.Location)){map.CloseField();commands.ReturnGameFocus();return true;}
            // Zoom: the original WorldMapWindow / VillageMapWindow are never constructed (gap-matrix 14.6 Q4-9, FU6§7),
            // so the click is consumed and nothing opens.
            if(ZoomRect.Contains(e.Location))return true;
        }
        // R5-H (T313 #740): right click on the map window = TransferMenu (send-location prompt). Inferred binding, see below.
        if(e.Kind==UiMouseKind.Down&&e.Button==MouseButtons.Right&&Bounds.Contains(e.Location)&&!ExitRect.Contains(e.Location)&&!ZoomRect.Contains(e.Location)){
            TransferMenu(commands);return true;
        }
        return Bounds.Contains(e.Location)||e.Kind!=UiMouseKind.Down;
    }

    /// <summary>
    /// TransferMenu handler (WO §5: map-window event table sub_8DE770 registers TransferMenu next to MapName / QuestToggle /
    /// ShowBookmark / SaveMenu; a click goes to sub_8E0620, which opens the chat-line prompt 1785, Enter sends C_CHANNEL 55/11
    /// = 3.81 C254/11). Installed at run time by LocationListPanel (MapSendEntry), which can reach the chat panel; null = no
    /// entry (offline, or no chat panel). 推測: which control raises TransferMenu is not in the decrypted UI XML, so the engine
    /// uses a right click on the map window; replace only <see cref="Mouse"/> once the original control is found.
    /// </summary>
    public Func<IUiCommands,bool>? TransferHandler{get;set;}
    /// <summary>TransferMenu: false = no current map, window closed, or no handler (nothing happens, nothing is sent).</summary>
    public bool TransferMenu(IUiCommands commands)=>map.FieldOpen&&map.Current!=null&&TransferHandler?.Invoke(commands)==true;
    /// <summary>Menu button 13 (R3-A #678, TW13 sub_77F510): same toggle as Ctrl+M. false = no current map (nothing opens).</summary>
    public bool ToggleFromMenu(){
        if(map.Current==null)return false;
        map.ToggleField();return true;
    }
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(e.KeyData!=ToggleKey||map.Current==null)return false;
        if(e.Down&&!e.Repeat)map.ToggleField();
        return true;
    }

    /// <summary>
    /// R7-H (#810): stack close (pushed out by a 5th window, global ESC; sub_A57C60 vtable+36) = the exit button without the
    /// focus hand-back. Panel stays Visible with empty Bounds, so Ctrl+M opens it again.
    /// </summary>
    public void Close(){if(map.FieldOpen)map.CloseField();}

    public bool IsDragHandle(Point p)=>map.FieldOpen&&Bounds.Contains(p)&&!ExitRect.Contains(p)&&!ZoomRect.Contains(p);
    public void MoveTo(Point p)=>location=p;
}
