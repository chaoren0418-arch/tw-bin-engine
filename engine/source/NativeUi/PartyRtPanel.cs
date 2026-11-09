namespace P06.NativeUi;

/// <summary>
/// PARTY_PET_RT_WINDOW (Issue #471; docs/reference/ui-layout.md §6 and V5 (e)): frame 3220 (158×125) at (485,475), a member of the
/// BAR_485 slot group (mode 2, after the minimap and the extended macro; shown only when it is the current member).
/// The XML gives the frame at absolute xPos/yPos; the other children (tabs xRPos 11/36 yRPos 6, buttons 82/102/122 × 100, pet
/// base 3221 at 0,18) are taken relative to the frame. 推測, supported by: the tabs then land on y=481 like PARTY_INTERFACE's
/// tabs, and the buttons land exactly inside the 8th (button) cell of the 3220 artwork.
/// </summary>
public sealed class PartyRtPanel:IUiPanel {
    public const string WindowId="PARTY_PET_RT_WINDOW";
    public enum Page { Party=0, Pet=1 }
    /// <summary>Member cells 64×27, 2 columns × 4 rows, measured from the 3220 artwork (columns 15..78 / 80..143, rows every 26 px from 18; 推測).</summary>
    public static readonly Size CellOrigin=new(15,18),CellPitch=new(65,26);

    readonly UiSlotGroup group;
    PartyLayout.Button[] tabs=[],buttons=[];
    UiSurface? frame,petBase,cursor;
    PartyLayout.Gauge gauge=new(null,null);
    Point frameAt,petAt;
    readonly PartyLayout.Interaction act=new();
    PartyView? party;bool inWorld;

    public PartyRtPanel(UiSlotGroup group){this.group=group;group.Add(WindowId);}
    public string Id=>WindowId;
    public UiLayer Layer=>UiLayer.Bar;
    public Page Current{get;private set;}=Page.Party;
    public bool Visible=>group.IsShown(WindowId)&&inWorld&&party!=null&&frame!=null;
    public Rectangle Bounds=>PartyLayout.Rect(frameAt,frame);
    public IReadOnlyList<Rectangle> TabRects=>tabs.Select(t=>t.Rect).ToArray();
    public IReadOnlyList<Rectangle> ButtonRects=>buttons.Select(b=>b.Rect).ToArray();
    public Point PetOrigin=>petAt;
    public string? Selected=>act.Selected;
    public Rectangle Cell(int k)=>PartyLayout.Cell(frameAt+CellOrigin,CellPitch,k);
    public Rectangle GaugeRect(int k)=>PartyLayout.GaugeRect(Cell(k),gauge);
    public Rectangle FillRect(int k,byte? hp)=>PartyLayout.FillRect(Cell(k),gauge,hp);

    public void Load(UiLayout layout,SurfCatalog surfs){
        var w=layout.Root.Child(WindowId);int x=w.Int("x"),y=w.Int("y");
        var f=PartyLayout.Placed(w,"SURF_IMAGE","SURF_800_PARTY_RT_WINDOW");
        frameAt=new(x+f.Int("xPos"),y+f.Int("yPos"));frame=surfs.Get(PartyLayout.FirstSurf(f));
        var pet=PartyLayout.Find(w,"IMAGE","SURF_800_PET_RT_WINDOW");petAt=new(frameAt.X+pet.Int("xPos"),frameAt.Y+pet.Int("yPos"));petBase=surfs.Get(PartyLayout.FirstSurf(pet));
        tabs=[PartyLayout.LoadButton(PartyLayout.Find(w,"STAGE3_BTN","SURF_800_PARTY_RT_BTN_PARTY_STATUS"),frameAt.X,frameAt.Y,surfs,(int)Page.Party),
              PartyLayout.LoadButton(PartyLayout.Find(w,"STAGE3_BTN","SURF_800_PET_RT_BTN_PET_STATUS"),frameAt.X,frameAt.Y,surfs,(int)Page.Pet)];
        buttons=PartyLayout.LoadActions(w,frameAt.X,frameAt.Y,surfs);
        // The RT window's own XML has no 3211/3212 (only the cursor and the grey gauge 3225), so the gauge frame and red fill come
        // from PARTY_INTERFACE (推測). The grey gauge's meaning (offline? other map?) is not verified, so it is never drawn (report).
        gauge=PartyLayout.LoadGauge(layout.Root.Child(PartyPanel.WindowId),surfs);
        cursor=surfs.Get(PartyLayout.FirstSurf(PartyLayout.Find(w,"IMAGE","SURF_800_PARTY_CURSOR_SELECTED")));
    }

    public void Update(IUiGameState state){
        inWorld=state.InWorld;party=inWorld?PartyStates.StateOf(state)?.Party:null;
        act.Sync(party);
    }

    public void SetPage(Page page){Current=page;act.Release();}

    public void Paint(UiCanvas canvas){
        if(party==null)return;
        canvas.DrawSurface(frame,frameAt.X,frameAt.Y);
        if(Current==Page.Party){
            PartyLayout.PaintMembers(canvas,party,gauge,cursor,act.Selected,Cell);
            foreach(var b in buttons)canvas.DrawSurface(act.Pressed==b.Tag?b.Down:b.Normal,b.Rect.X,b.Rect.Y);
        }else canvas.DrawSurface(petBase,petAt.X,petAt.Y);   // pet list itself is not part of #471 (report)
        foreach(var t in tabs)canvas.DrawSurface(t.Tag==(int)Current?t.Down:t.Normal,t.Rect.X,t.Rect.Y);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(party==null||e.Button!=MouseButtons.Left)return false;
        if(e.Kind==UiMouseKind.Down)foreach(var t in tabs)if(!t.Rect.IsEmpty&&t.Rect.Contains(e.Location)){SetPage((Page)t.Tag);return true;}
        if(Current!=Page.Party)return e.Kind==UiMouseKind.Down;
        return act.Mouse(e,party,buttons,Cell,commands);
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
