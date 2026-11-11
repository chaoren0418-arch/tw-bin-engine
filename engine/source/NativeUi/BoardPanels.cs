namespace P06.NativeUi;

// M7-U (Issue #528, gap-matrix §7): native board windows. The original builds three hard-coded legacy windows in sub_8C7C80
// (VA 0x8C7C80 / FO 0x4C7080): BoardListWindow (sub_7CB710 / FO 0x3CAB10), BoardReadWindow (sub_7CBA70 / FO 0x3CAE70) and
// BoardWriteWindow (sub_7CBE90 / FO 0x3CB290). They read no XML, so these panels are attached at run time by the board
// window owner (BoardUi.cs) instead of UiPanelRegistry, which requires an XML layout.
//
// Evidence levels (docs/reference/windows.md §2 T130 is the only source):
//   Verified  — button surf ids and their offsets inside the list/read windows (WIN§2 T130). Painted.
//   Inferred  — window backgrounds (656 / 670), button meaning, write-window surfs (680–687), row/article text positions.
//               Data only: never painted (gap-matrix §7 rule). Button meaning is used only to route clicks to the existing
//               board actions, read from the words drawn on the original images (issue-528 report).
//   Verified (R7-F, #808) — the window origin: sub_7CC7C0 / sub_7CCB40 paint at the UI origin dword_154524C / dword_1545250,
//               which is 0 at 800×600 (ui-layout.md V5), so all three windows sit at screen (0,0) (issue-739 report).
//   Verified (R7-F, #808) — read-window 678 is bound to sub_A59AB0(read window) in sub_7CBA70 (VA 0x7CBDB2–0x7CBDDC):
//               it hides the read window only; the list window stays up. No packet.
//   Verified (R9-N, #858) — read-window 672 = previous article, 676 = next article (followups-9 §3.2, BoardNavigation).

public enum BoardButtonRole { Close, Read, Write, Delete, Previous, Next, BackToList, ArticlePrevious, ArticleNext }

/// <summary>One hard-coded button: surf id and offset inside its window (WIN§2 T130). Role is inferred from the image.</summary>
public sealed record BoardButton(int Surf,int X,int Y,BoardButtonRole Role);

public static class BoardWindowLayout {
    /// <summary>BoardListWindow buttons, WIN§2 T130 (verified surf + offset).</summary>
    public static readonly BoardButton[] List=[
        new(658,209,305,BoardButtonRole.Close),   // image: "X"
        new(660,34,332,BoardButtonRole.Read),     // image: "Read"
        new(662,139,304,BoardButtonRole.Write),   // image: "Write"
        new(664,171,304,BoardButtonRole.Delete),  // image: "Delete"
        new(666,64,311,BoardButtonRole.Previous), // image: flourish pointing left (direction inferred)
        new(668,99,311,BoardButtonRole.Next),     // image: flourish pointing right (direction inferred)
    ];
    /// <summary>BoardReadWindow buttons, WIN§2 T130 (verified surf + offset).</summary>
    public static readonly BoardButton[] Read=[
        new(672,56,312,BoardButtonRole.ArticlePrevious), // previous article (R9-N, followups-9 §3.2 sub_7CDEE0, verified)
        new(676,110,312,BoardButtonRole.ArticleNext),    // next article (R9-N, followups-9 §3.2 sub_7CD470, verified)
        new(678,213,297,BoardButtonRole.Close),          // image: "X"; sub_A59AB0(read window): closes the read window only
        new(674,90,299,BoardButtonRole.BackToList),      // image: upward arrow (inferred: back to the list)
    ];
    /// <summary>Inferred only (same colour key and a size that contains every button); not painted.</summary>
    public const int ListBackgroundSurf=656,ReadBackgroundSurf=670;
    /// <summary>Inferred only: "Delete", "Write", "Done", "O.K." images next to the read window; offsets unknown. Not painted.</summary>
    public static readonly int[] WriteWindowSurfs=[680,682,684,686];
    /// <summary>R7-F (#808): the original UI origin at 800×600 is (0,0) (ui-layout.md V5, issue-739 report).</summary>
    public static readonly Point DefaultOrigin=new(0,0);
}

/// <summary>What a board panel needs from the existing board window (BoardUi). UI modules never touch LoginConnection.</summary>
public interface IBoardPanelHost {
    /// <summary>Current board snapshot, or null when no board window is open.</summary>
    BoardView? Board { get; }
    /// <summary>
    /// Same actions as the existing board window: "close", "read", "write", "delete", "previous", "next"; R9-N adds the
    /// read window's "article-previous" / "article-next" (672 / 676, BoardNavigation).
    /// </summary>
    void Press(string command);
}

/// <summary>The three windows plus the shared "which window is up" state.</summary>
public sealed class BoardPanelSet {
    public IBoardPanelHost Host{get;}
    public BoardListWindow List{get;}
    public BoardReadWindow Read{get;}
    public BoardWriteWindow Write{get;}
    public BoardPanelSet(IBoardPanelHost host){Host=host;List=new(this);Read=new(this);Write=new(this);}
    public IEnumerable<IUiPanel> Panels=>[List,Read,Write];

    BoardView? view;bool listOverArticle;uint shownArticle;
    /// <summary>"none", "list" or "read" for the current snapshot.</summary>
    public string Mode{get;private set;}="none";
    internal void Sync(){
        view=Host.Board;
        if(view is not {Kind:not "closed"}){Mode="none";listOverArticle=false;shownArticle=0;return;}
        if(view.Article is {} a&&a.Id!=shownArticle){shownArticle=a.Id;listOverArticle=false;}
        Mode=view.Kind=="article"&&view.Article!=null&&!listOverArticle?"read":"list";
    }
    /// <summary>Original "back to list" (inferred) is a local view switch: no packet.</summary>
    internal void ShowList(){listOverArticle=true;Mode=view is {Kind:not "closed"}?"list":"none";}
    /// <summary>
    /// T426 (#970, FU15§2.11 已驗證): the list window's [this+0x40] "waiting for the server" — set when a list / read request
    /// is sent (sub_7CD880, sub_7CDA70, sub_7CD970, sub_7CDBD0, sub_7CD490), cleared when the list (sub_7CD600) or the
    /// article (sub_7CD9F0) arrives. Engine counterpart: the board kinds "list-wait" / "read-wait" (Board.cs BoardSent →
    /// ReceiveBoardList / ReceiveBoardRead). While waiting the list mouse (sub_7CD080) does nothing and 662 Write
    /// (sub_7CDE00) does not open the write window.
    /// </summary>
    public static bool IsWaiting(BoardView? v)=>v is {Kind:"list-wait" or "read-wait"};
    public bool Waiting=>IsWaiting(Host.Board);
    public void LoadSurfaces(SurfCatalog surfs){foreach(var p in Panels)p.Load(null!,surfs);}
    internal void LoadSurfaces(Func<int,UiSurface?> surf){List.Load(surf);Read.Load(surf);}
}

/// <summary>Shared button window: verified buttons only, at Origin + WIN§2 offset; a missing surf draws and hits nothing.</summary>
public abstract class BoardButtonWindow:IUiClosablePanel {
    readonly BoardButton[] buttons;readonly UiSurface?[] surfaces;
    protected BoardPanelSet Set{get;}
    int pressed=-1;
    protected BoardButtonWindow(BoardPanelSet set,BoardButton[] buttons){Set=set;this.buttons=buttons;surfaces=new UiSurface?[buttons.Length];}
    public abstract string Id{get;}
    protected abstract string Mode{get;}
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}=BoardWindowLayout.DefaultOrigin;
    public IReadOnlyList<BoardButton> Buttons=>buttons;
    public Rectangle Bounds{get;private set;}
    public bool Visible=>!Bounds.IsEmpty&&Set.Mode==Mode;
    /// <summary>Board windows read no XML (hard-coded in the BIN); layout is ignored and may be null.</summary>
    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(surfs.Get);
    /// <summary>Same as Load, from any surface source (offline tests use synthetic surfaces).</summary>
    internal void Load(Func<int,UiSurface?> surf){
        for(int i=0;i<buttons.Length;i++)surfaces[i]=surf(buttons[i].Surf);
        Bounds=buttons.Select((_,i)=>ButtonRect(i)).Where(r=>!r.IsEmpty).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
    }
    public Rectangle ButtonRect(int index)=>surfaces[index] is {} s?new(Origin.X+buttons[index].X,Origin.Y+buttons[index].Y,s.Width,s.Height):Rectangle.Empty;
    public int HitButton(Point p){for(int i=0;i<buttons.Length;i++)if(ButtonRect(i).Contains(p))return i;return -1;}
    public void Update(IUiGameState state){Set.Sync();if(!Visible)pressed=-1;}
    public void Paint(UiCanvas canvas){for(int i=0;i<buttons.Length;i++)canvas.DrawSurface(surfaces[i],Origin.X+buttons[i].X,Origin.Y+buttons[i].Y);}
    /// <summary>Press on a button, release on the same button = activate. Presses between buttons are swallowed (inside Bounds).</summary>
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(e.Button!=MouseButtons.Left&&e.Kind is UiMouseKind.Down or UiMouseKind.Up)return false;
        int hit=HitButton(e.Location);
        switch(e.Kind){
            case UiMouseKind.Down:pressed=hit;return true;
            case UiMouseKind.Up:{
                int was=pressed;pressed=-1;
                if(was>=0&&was==hit&&Visible)Activate(buttons[was].Role);
                return true;
            }
            default:return Bounds.Contains(e.Location);
        }
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    /// <summary>
    /// IUiClosablePanel (R7-F): the same action as this window's X button (list 658 closes the board, read 678 only the
    /// read window). Does nothing while the window is not up.
    /// </summary>
    public void Close(){Set.Sync();if(Visible)CloseWindow();pressed=-1;}
    /// <summary>What this window's X button does.</summary>
    protected abstract void CloseWindow();
    void Activate(BoardButtonRole role){
        switch(role){
            case BoardButtonRole.Close:CloseWindow();break;
            case BoardButtonRole.Read:Set.Host.Press("read");break;
            case BoardButtonRole.Write:if(!Set.Waiting)Set.Host.Press("write");break;   // sub_7CDE00: [this+0x40]≠0 → nothing
            case BoardButtonRole.Delete:Set.Host.Press("delete");break;
            case BoardButtonRole.Previous:Set.Host.Press("previous");break;
            case BoardButtonRole.Next:Set.Host.Press("next");break;
            case BoardButtonRole.BackToList:Set.ShowList();break;
            case BoardButtonRole.ArticlePrevious:Navigate(false);break;
            case BoardButtonRole.ArticleNext:Navigate(true);break;
        }
    }
    /// <summary>
    /// R9-N (#858, followups-9 §3.2): 672 / 676. Closing the read window is local (same as 678); reading the neighbour row
    /// or loading the next page goes to the host ("article-previous" / "article-next"), which re-checks with its own
    /// snapshot and busy state. Busy (no CanAct) → nothing.
    /// </summary>
    void Navigate(bool next){
        var (step,_)=BoardNavigation.Article(Set.Host.Board,next);
        switch(step){
            case BoardArticleStep.CloseRead:Set.ShowList();break;
            case BoardArticleStep.Read or BoardArticleStep.NextPage:Set.Host.Press(next?"article-next":"article-previous");break;
        }
    }
}

public sealed class BoardListWindow(BoardPanelSet set):BoardButtonWindow(set,BoardWindowLayout.List) {
    public override string Id=>"BoardListWindow";
    protected override string Mode=>"list";
    /// <summary>658 "X": closes the whole board (same as the existing board window's close).</summary>
    protected override void CloseWindow()=>Set.Host.Press("close");
}
public sealed class BoardReadWindow(BoardPanelSet set):BoardButtonWindow(set,BoardWindowLayout.Read) {
    public override string Id=>"BoardReadWindow";
    protected override string Mode=>"read";
    /// <summary>678 "X" → sub_A59AB0(read window): only the read window goes away, the list stays. Local, no packet.</summary>
    protected override void CloseWindow()=>Set.ShowList();
}

/// <summary>
/// BoardWriteWindow (sub_7CBE90 / FO 0x3CB290): no surf or offset is verified, so it is data only and never visible.
/// Writing stays in the existing board window (BoardUi) until WIN§2 gains the write-window layout.
/// </summary>
public sealed class BoardWriteWindow(BoardPanelSet set):IUiPanel {
    public BoardPanelSet Set{get;}=set;
    public string Id=>"BoardWriteWindow";
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds=>Rectangle.Empty;
    public bool Visible=>false;
    public IReadOnlyList<int> InferredSurfs=>BoardWindowLayout.WriteWindowSurfs;
    public void Load(UiLayout layout,SurfCatalog surfs){}
    public void Update(IUiGameState state){}
    public void Paint(UiCanvas canvas){}
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
