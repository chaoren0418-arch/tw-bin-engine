namespace P06.NativeUi;

/// <summary>
/// EX_MACRO_WINDOW extended macro (Issue #466; docs/reference/ui-layout.md V5 (a) "擴充巨集"): frame 3226 at (485,475), 12 slots in
/// 4 columns × 3 rows, pitch 34, icon origin = EX_MACRO_FRAME + (10,20) (TW16 sub_ECEA70 / sub_ED4530; not in the XML).
/// Lives in the BAR_485 slot group (shown only when it is the current member; TW13 cycles the slot with Alt+F3, V5 (e)).
/// No F-key is bound to these slots in the traced code; click selects and double click uses (推測: same as the main box).
/// <para>R7-O (T353/#816): when WidgetSubSlot.xml is found (main Tile.pak, else the reference client) the box also draws its
/// ButtonLock (BaseSurf 3191 = locked / PushSurf 3192 = unlocked, at Window+(149,37); it toggles the same MacroBook lock as the
/// main box — 推測) and ButtonMode (BasePng 3350 / PushPng 3351 via OptionPngSource, ModeChange not wired, M30). Without the
/// file the uistatus86 look is unchanged.</para>
/// </summary>
public sealed class ExMacroPanel:IUiPanel {
    [UiPanel(70)] internal static UiPanelRegistration Registration=>new(ExMacroPanel.WindowId,"uistatus86",root=>new ExMacroPanel(root.SlotGroup(UiSlotGroup.Bar485),root.Resources));   // ui-macro-bar #466
    public const string WindowId="EX_MACRO_WINDOW";
    public const int Rows=3;
    /// <summary>Icon origin relative to EX_MACRO_FRAME (V5, verified constant in the BIN).</summary>
    public static readonly Size IconOffset=new(10,20);
    /// <summary>Select mark relative to the icon. Main box: SELECT_MARK − MACRO_ICON = (−2,−2) (verified); reused here (推測).</summary>
    public static readonly Size MarkOffset=new(-2,-2);

    readonly UiSlotGroup group;readonly UiResources? resources;
    UiSurface? frame,selectMark;
    Point frameAt,iconAt;
    IUiMacroState? macro;bool inWorld;
    WidgetSlotWindow? widget;bool modePressed;

    public ExMacroPanel(UiSlotGroup group,UiResources? resources=null){this.group=group;this.resources=resources;group.Add(WindowId);}
    public string Id=>WindowId;
    public UiLayer Layer=>UiLayer.Bar;
    public Rectangle Bounds{get;private set;}
    public bool Visible=>group.IsShown(WindowId)&&inWorld&&macro!=null&&frame!=null;
    public Point IconOrigin=>iconAt;
    public Rectangle FrameRect=>MacroLayout.Rect(frameAt,frame);
    /// <summary>WidgetSubSlot.xml look in use (R7-O); null = uistatus86 look.</summary>
    public WidgetSlotWindow? Widget=>widget;
    /// <summary>"absent", "refused: …" or the XML file name actually used.</summary>
    public string WidgetStatus{get;private set;}="absent";
    public Rectangle LockRect=>widget?.Lock?.Rect??Rectangle.Empty;
    public bool ModePressed=>modePressed;

    public void Load(UiLayout layout,SurfCatalog surfs){
        var (xml,png,error)=WidgetSlotLayout.Open(resources,WidgetSlotLayout.SubFile);
        Load(layout,surfs.Get,xml,png,error);
    }

    /// <summary>Offline checks: images from the caller; widgetXml null = uistatus86 look only.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf,UiLayout? widgetXml,Func<int,UiSurface?> png,string? readError=null){
        widget=null;modePressed=false;WidgetStatus=readError==null?"absent":"refused: "+readError;
        var w=layout.Root.Child(WindowId);int x=w.Int("x"),y=w.Int("y");
        var frameNode=w.Child("MAIN_WINDOW/EX_MACRO_FRAME");
        int frameSurf=MacroLayout.FirstSurf(frameNode);
        frame=surf(frameSurf);frameAt=MacroLayout.At(frameNode,x,y);
        iconAt=frameAt+IconOffset;
        // The extended box has no SELECT_MARK of its own; byte_13CE5EA moves the main box's mark here.
        selectMark=surf(MacroLayout.FirstSurf(layout.Root.Child(MacroPanel.WindowId+"/ETC/SELECT_MARK")));
        Bounds=FrameRect;
        if(widgetXml==null)return;
        try{widget=WidgetSlotWindow.Parse(widgetXml,WidgetSlotLayout.SubWindow,frameAt,frameSurf,surf,png);}
        catch(UiLayoutException ex){WidgetStatus="refused: "+ex.Message;UiResourceLog.Write($"widget-slot {widgetXml.FileName} 不符，維持 uistatus86 外觀：{ex.Message}");return;}
        WidgetStatus=widgetXml.FileName;
        Bounds=new[]{widget.Lock?.Rect??Rectangle.Empty,widget.CornerRect,widget.Mode?.Rect??Rectangle.Empty}.Aggregate(FrameRect,MacroLayout.Union);
        if(widget.Missing.Count>0)UiResourceLog.Write($"widget-slot {widgetXml.FileName} 缺圖不畫：{string.Join("、",widget.Missing)}");
    }

    public void Update(IUiGameState state){inWorld=state.InWorld;macro=MacroBook.StateOf(state);}

    public Point SlotIcon(int k)=>new(iconAt.X+MacroPanel.Pitch*(k%MacroPanel.Columns),iconAt.Y+MacroPanel.Pitch*(k/MacroPanel.Columns));
    public Point SlotMark(int k)=>SlotIcon(k)+MarkOffset;
    public int SlotAt(Point p){
        int dx=p.X-iconAt.X,dy=p.Y-iconAt.Y;
        if(dx<0||dy<0||dx>=MacroPanel.Pitch*MacroPanel.Columns||dy>=MacroPanel.Pitch*Rows)return -1;
        return dx/MacroPanel.Pitch+MacroPanel.Columns*(dy/MacroPanel.Pitch);
    }

    public void Paint(UiCanvas canvas){
        if(macro==null)return;var book=macro.Macros;
        canvas.DrawSurface(frame,frameAt.X,frameAt.Y);
        for(int k=0;k<MacroBook.ExtendedCount;k++)MacroLayout.PaintSlot(canvas,macro,book.Extended(k),SlotIcon(k),SlotMark(k));
        if(book.SelectionInExtended){var m=SlotMark(book.SelectedExtended);canvas.DrawSurface(selectMark,m.X,m.Y);}
        if(widget==null)return;
        // WidgetSubSlot.xml order: ButtonLock, then ButtonMode (child order = draw order, 推測).
        if(widget.Lock is {} l)canvas.DrawSurface(book.Locked?l.Base:l.Push,l.Rect.X,l.Rect.Y);
        canvas.DrawSurface(widget.Corner,widget.CornerRect.X,widget.CornerRect.Y);
        widget.Mode?.Paint(canvas,modePressed);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(e.Kind==UiMouseKind.Up&&modePressed){modePressed=false;return true;}
        if(macro==null||e.Kind!=UiMouseKind.Down||e.Button!=MouseButtons.Left)return false;
        var book=macro.Macros;
        if(widget?.Mode is {} mode&&mode.Hit(e.Location)){modePressed=true;WidgetSlotLayout.ModeChangeNotWired(WidgetSlotLayout.SubFile);return true;}
        if(widget?.Lock is {} l&&l.Hit(e.Location)){book.ToggleLock();return true;}
        int k=SlotAt(e.Location);if(k<0)return false;
        book.SelectExtended(k);
        if(e.Clicks>=2)MacroBook.TryActivate(macro,book.Extended(k),commands);
        return true;
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
