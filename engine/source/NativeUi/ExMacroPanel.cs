namespace P06.NativeUi;

/// <summary>
/// EX_MACRO_WINDOW extended macro (Issue #466; docs/reference/ui-layout.md V5 (a) "擴充巨集"): frame 3226 at (485,475), 12 slots in
/// 4 columns × 3 rows, pitch 34, icon origin = EX_MACRO_FRAME + (10,20) (TW16 sub_ECEA70 / sub_ED4530; not in the XML).
/// Lives in the BAR_485 slot group (shown only when it is the current member; TW13 cycles the slot with Alt+F3, V5 (e)).
/// No F-key is bound to these slots in the traced code; click selects and double click uses (推測: same as the main box).
/// </summary>
public sealed class ExMacroPanel:IUiPanel {
    [UiPanel(70)] internal static UiPanelRegistration Registration=>new(ExMacroPanel.WindowId,"uistatus86",root=>new ExMacroPanel(root.SlotGroup(UiSlotGroup.Bar485)));   // ui-macro-bar #466
    public const string WindowId="EX_MACRO_WINDOW";
    public const int Rows=3;
    /// <summary>Icon origin relative to EX_MACRO_FRAME (V5, verified constant in the BIN).</summary>
    public static readonly Size IconOffset=new(10,20);
    /// <summary>Select mark relative to the icon. Main box: SELECT_MARK − MACRO_ICON = (−2,−2) (verified); reused here (推測).</summary>
    public static readonly Size MarkOffset=new(-2,-2);

    readonly UiSlotGroup group;
    UiSurface? frame,selectMark;
    Point frameAt,iconAt;
    IUiMacroState? macro;bool inWorld;

    public ExMacroPanel(UiSlotGroup group){this.group=group;group.Add(WindowId);}
    public string Id=>WindowId;
    public UiLayer Layer=>UiLayer.Bar;
    public Rectangle Bounds{get;private set;}
    public bool Visible=>group.IsShown(WindowId)&&inWorld&&macro!=null&&frame!=null;
    public Point IconOrigin=>iconAt;

    public void Load(UiLayout layout,SurfCatalog surfs){
        var w=layout.Root.Child(WindowId);int x=w.Int("x"),y=w.Int("y");
        var frameNode=w.Child("MAIN_WINDOW/EX_MACRO_FRAME");
        frame=surfs.Get(MacroLayout.FirstSurf(frameNode));frameAt=MacroLayout.At(frameNode,x,y);
        iconAt=frameAt+IconOffset;
        // The extended box has no SELECT_MARK of its own; byte_13CE5EA moves the main box's mark here.
        selectMark=surfs.Get(MacroLayout.FirstSurf(layout.Root.Child(MacroPanel.WindowId+"/ETC/SELECT_MARK")));
        Bounds=MacroLayout.Rect(frameAt,frame);
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
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(macro==null||e.Kind!=UiMouseKind.Down||e.Button!=MouseButtons.Left)return false;
        int k=SlotAt(e.Location);if(k<0)return false;
        var book=macro.Macros;book.SelectExtended(k);
        if(e.Clicks>=2)MacroBook.TryActivate(macro,book.Extended(k),commands);
        return true;
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
