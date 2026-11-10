namespace P06.NativeUi;

/// <summary>
/// Field_Map window (MapSystem-c.xml, ui-layout.md §5): frame 2816 at pos (5,250), title 2815, exit 2801 (146,6),
/// zoom 2803 (129,6); 160×120 png view centred on the player, party dots 2826, 8-direction arrow 2818–2825.
/// Maps outside the Field_Map list use the Dungern_Map chrome (title 2888, no zoom) and show no map content.
/// Opened/closed by Ctrl+M (V5 (b)) or the Map menu button (action 13, MenuButtonsPanel → ToggleFromMenu, R3-A #678).
/// Created by MiniMapPanel.Create (one registry line for the module) and loaded from MiniMapPanel.Load.
/// R9-B (T362 #846, FU9 §7.2, 已驗證 sub_A6F0B0): right button down only sets a flag, right button up opens
/// MapCommunicationSelectWindow at the cursor (<see cref="Popup"/>, MapCommunicationPanel.cs); while the menu is open a left or
/// right press inside the map window first hides it. Button 0 = TransferMenu (<see cref="TransferMenu"/>), button 1 =
/// MapLocationSaveWindow (MapLocationSavePanel.cs).
/// op 132 S_SHOW_MAP (FU9 §5) opens the legacy MapWindow (item map viewer, window 19), not this window; 3.81 never sends it
/// (MapR.java has S_UseMap commented out and only answers message 74), so the engine has no op 132 handler.
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
        // R9-B: sub_A6AA10 draws surf 150 at (menu x − 4, menu y − 4) while the send prompt it started is active.
        if(Popup?.Marker is Point m)canvas.DrawSurface(surfs?.Get(MapCommunicationPanel.MarkerSurf),m.X,m.Y);
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
        if(!map.FieldOpen){rightPressed=false;return false;}
        bool inside=Bounds.Contains(e.Location),onButton=ExitRect.Contains(e.Location)||ZoomRect.Contains(e.Location);
        // sub_A6F0B0: with the menu shown, WM_LBUTTONDOWN / WM_RBUTTONDOWN that the menu did not take hide it first, then
        // the press is handled as usual (the menu is a Float panel above, so a press that reaches here missed it).
        if(e.Kind==UiMouseKind.Down&&inside&&e.Button is MouseButtons.Left or MouseButtons.Right&&Popup is {MenuOpen:true} shown)shown.HideMenu();
        if(e.Kind==UiMouseKind.Down&&e.Button==MouseButtons.Left){
            if(ExitRect.Contains(e.Location)){map.CloseField();commands.ReturnGameFocus();return true;}
            // Zoom: the original WorldMapWindow / VillageMapWindow are never constructed (gap-matrix 14.6 Q4-9, FU6§7),
            // so the click is consumed and nothing opens.
            if(ZoomRect.Contains(e.Location))return true;
        }
        // R9-B: 516 only sets byte_153F2F5; 517 (inside the window, flag set) computes the map point under the cursor
        // (sub_A66F50) and shows MapCommunicationSelectWindow; the flag is cleared on every 517.
        if(e.Button==MouseButtons.Right){
            if(e.Kind==UiMouseKind.Down&&inside&&!onButton){rightPressed=true;return true;}
            if(e.Kind==UiMouseKind.Up){
                bool was=rightPressed;rightPressed=false;
                if(was&&inside&&!onButton)OpenPopup(e.Location,commands);
                return true;
            }
        }
        return inside||e.Kind!=UiMouseKind.Down;
    }
    bool rightPressed;
    /// <summary>R9-B: true between a right press on the map and its release (byte_153F2F5).</summary>
    public bool RightPressed=>rightPressed;
    /// <summary>
    /// MapCommunicationSelectWindow (MapCommunicationPanel), installed at run time by that panel (it finds this one through the
    /// root); null = no menu (offline construction), a right click then does nothing.
    /// </summary>
    public IFieldMapPopup? Popup{get;set;}
    /// <summary>Right button released on the map: the menu at the cursor. false = no menu panel, or it could not open.</summary>
    public bool OpenPopup(Point cursor,IUiCommands commands)=>map.FieldOpen&&map.Current!=null&&Popup?.ShowMenu(cursor,MapPointAt(cursor),commands)==true;

    /// <summary>
    /// sub_A66F50: map coordinate under a logical point of the map view (inverse of FieldMapEntry.ToPixel; dx = (px/2 − py)/f,
    /// dy = (px/2 + py)/f, truncated like the original float → int). null = no Field png on screen (dungeon chrome, no image).
    /// 推測: that sub_565DE0 yields the png pixel relative to the map's base pixel (its offset object was not traced).
    /// </summary>
    public Point? MapPointAt(Point logical){
        if(map.Entry is not {} entry||map.View(location,false) is not {} view)return null;
        var png=new Point(logical.X-view.Origin.X-MapViewLayout.HalfWidth+view.Player.X,logical.Y-view.Origin.Y-MapViewLayout.HalfHeight+view.Player.Y);
        return ToMapPoint(entry,png);
    }
    /// <summary>png pixel → map coordinate for one Field_Map row (round trip of FieldMapEntry.ToPixel within ±1: both truncate).</summary>
    public static Point ToMapPoint(FieldMapEntry entry,Point png){
        float f=entry.Factor,px=png.X-entry.BasePixel.X,py=png.Y-entry.BasePixel.Y;
        return new(entry.BaseLoc.X+(int)((px*0.5f-py)/f),entry.BaseLoc.Y+(int)((px*0.5f+py)/f));
    }

    /// <summary>
    /// TransferMenu handler (FU9 §7.2, 已驗證): MapCommunicationSelectWindow button 0 (sub_A762A0 case 0) hides the menu and opens
    /// the chat-line prompt 1785 (sub_AC98E0); Enter sends C_CHANNEL 55/11 = 3.81 C254/11. Installed at run time by
    /// LocationListPanel (MapSendEntry), which can reach the chat panel; null = no entry (offline, or no chat panel).
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
    public void Close(){if(map.FieldOpen)map.CloseField();rightPressed=false;}

    public bool IsDragHandle(Point p)=>map.FieldOpen&&Bounds.Contains(p)&&!ExitRect.Contains(p)&&!ZoomRect.Contains(p);
    public void MoveTo(Point p)=>location=p;
}
