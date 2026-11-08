namespace P06.NativeUi;

/// <summary>
/// Field_Map window (MapSystem-c.xml, ui-layout.md §5): frame 2816 at pos (5,250), title 2815, exit 2801 (146,6),
/// zoom 2803 (129,6); 160×120 png view centred on the player, party dots 2826, 8-direction arrow 2818–2825.
/// Maps outside the Field_Map list use the Dungern_Map chrome (title 2888, no zoom) and show no map content.
/// Opened/closed by Ctrl+M (V5 (b)) or MapState.ToggleField (Map button, action 13, belongs to ui-macro-bar).
/// Created by MiniMapPanel.Create (one registry line for the module) and loaded from MiniMapPanel.Load.
/// </summary>
public sealed class FieldMapPanel(MapState map):IUiMovablePanel {
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
            // Zoom opens the large World/Village map in the original; that window is not part of this module.
            if(ZoomRect.Contains(e.Location))return true;
        }
        return Bounds.Contains(e.Location)||e.Kind!=UiMouseKind.Down;
    }
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(e.KeyData!=ToggleKey||map.Current==null)return false;
        if(e.Down&&!e.Repeat)map.ToggleField();
        return true;
    }

    public bool IsDragHandle(Point p)=>map.FieldOpen&&Bounds.Contains(p)&&!ExitRect.Contains(p)&&!ZoomRect.Contains(p);
    public void MoveTo(Point p)=>location=p;
}
