namespace P06.NativeUi;

/// <summary>
/// Bar minimap MINI_MAP_WINDOW (uistatus86-c.xml): (485,475), base 3274 at (+12,+20) (135×101), frame 3275 (158×125).
/// Member of the BAR_485 slot (one of minimap / extended macro / party RT is shown). Content follows V5 (d): not a live
/// terrain thumbnail but the Field_Map png view (MapSystem ratio/baseLoc/basePixel) drawn at MINI_MAP_BASE + (0,5);
/// unlisted or restricted maps show only base and frame. Draw order (TW16 sub_A695E0 with a4=0): base, png, frame,
/// party dots, arrow. No key is used here: F5 stays a macro key and the map toggle is Ctrl+M (Field_Map).
/// </summary>
public sealed class MiniMapPanel:IUiPanel {
    [UiPanel(60)] internal static UiPanelRegistration Registration=>new(MiniMapPanel.PanelId,"uistatus86",root=>MiniMapPanel.Create(root));   // ui-minimap (#470); also adds Field_Map; BAR_485 member, before RIGHT_WINDOW
    public const string PanelId="MINI_MAP_WINDOW";
    /// <summary>V5 (d): the view is drawn at MINI_MAP_BASE + (0,5).</summary>
    public static readonly Size ViewOffset=new(0,5);
    readonly UiSlotGroup group;
    readonly UiXmlSource? xml;
    SurfCatalog? surfs;
    UiSurface? baseSurface,frameSurface;
    Point location,basePos,framePos;
    public MapState Map{get;}
    public FieldMapPanel Field{get;}

    public MiniMapPanel(UiSlotGroup group,MapState map,FieldMapPanel field,UiXmlSource? xml){
        this.group=group;this.xml=xml;Map=map;Field=field;
        group.Add(PanelId);
    }
    /// <summary>Registry factory: also adds the Field_Map window that shares this module's MapState.</summary>
    public static MiniMapPanel Create(UiRoot root,Func<IUiMapSource?>? source=null){
        var resources=root.Resources;
        var map=new MapState(resources==null?null:new MapPngSource(resources.Sprites),source);
        var field=new FieldMapPanel(map);root.Add(field);
        return new MiniMapPanel(root.SlotGroup(UiSlotGroup.Bar485),map,field,resources?.Xml);
    }

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Bar;
    public Rectangle Bounds=>new(location,frameSurface==null?Size.Empty:new Size(frameSurface.Width,frameSurface.Height));
    public bool Visible=>group.IsShown(PanelId);
    /// <summary>Logical rectangle of MINI_MAP_BASE (the 135×101 map area).</summary>
    public Rectangle BaseRect=>new(location.X+basePos.X,location.Y+basePos.Y,baseSurface?.Width??0,baseSurface?.Height??0);
    public Point ViewOrigin=>new(location.X+basePos.X+ViewOffset.Width,location.Y+basePos.Y+ViewOffset.Height);

    public void Load(UiLayout layout,SurfCatalog surfs){
        var n=layout.Root.Child(PanelId);
        location=new(n.Int("x"),n.Int("y"));
        var b=n.Child("MAIN_WINDOW/MINI_MAP_BASE");var f=n.Child("MAIN_WINDOW/MINI_MAP_FRAME");
        basePos=new(b.Int("xRPos"),b.Int("yRPos"));framePos=new(f.Int("xRPos"),f.Int("yRPos"));
        this.surfs=surfs;
        baseSurface=First(b,surfs);frameSurface=First(f,surfs);
        if(xml==null)throw new UiLayoutException(MapSystemData.FileName,"","沒有 UI 資源，無法載入 MapSystem");
        Field.Load(xml.Load(MapSystemData.FileName),surfs);
    }
    static UiSurface? First(UiNode n,SurfCatalog surfs){var s=n.Surfs();return s.Length>0?surfs.Get(s[0]):null;}

    public void Update(IUiGameState state)=>Map.Update(state);

    public void Paint(UiCanvas canvas){
        canvas.DrawSurface(baseSurface,location.X+basePos.X,location.Y+basePos.Y);
        void Frame()=>canvas.DrawSurface(frameSurface,location.X+framePos.X,location.Y+framePos.Y);
        if(Map.View(ViewOrigin,true) is {} view)Map.PaintMap(canvas,surfs,view,Frame);else Frame();
    }
    /// <summary>Clicks on the minimap are taken (they must not walk the character); the original does not switch on click.</summary>
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>e.Kind!=UiMouseKind.Wheel;
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
