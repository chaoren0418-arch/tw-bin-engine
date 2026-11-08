namespace P06.NativeUi;

/// <summary>
/// RIGHT_WINDOW macro box (Issue #466; docs/design/native-ui.md §5.3 ui-macro-bar; geometry docs/reference/ui-layout.md §2, V5 (a)(b)).
/// Frame 3176 at (643,476); 8 slots on F5–F12 in 4 columns × 2 rows, pitch 34 (TW16 sub_ED33F0 / TW13 sub_783A90);
/// 3 tabs (TAB_BUTTON actions 10–12, also F1–F3); SELECT_MARK 2331; MACRO_LOCK (ONOFF_BUTTON action 8).
/// The menu buttons of the same window (BUTTON_2STAGE_LIST, y=91) belong to ui-menu-buttons and are not drawn here.
/// </summary>
public sealed class MacroPanel:IUiPanel {
    public const string WindowId="RIGHT_WINDOW";
    /// <summary>Slot pitch in both directions and columns per row (V5 (a), verified).</summary>
    public const int Pitch=34,Columns=4;
    /// <summary>Tab actions 10/11/12 → pages 0/1/2 (ui-layout.md §2 RIGHT_WINDOW).</summary>
    public const int FirstTabAction=10,LockAction=8;
    /// <summary>
    /// Image offsets inside a surf range. ONOFF_BUTTON is "2 images per 1 button" (XML comment): a = off, a+1 = on (推測 —
    /// V4 traced only IMAGE_BUTTON/3Stage). TAB_BUTTON is "3 images per 1 button"; its state order is unverified (V4 §(b)
    /// last line), so the selected tab uses a+1 like a pressed button (推測). Change only these two constants once verified.
    /// </summary>
    public const int LockOnOffset=1,TabSelectedOffset=1;
    /// <summary>Item count text: palette[13] yellow on palette[0] black, capped at 99999 (V5 (a)).</summary>
    public static readonly Color CountColor=Color.FromArgb(255,255,255,0),CountShadow=Color.FromArgb(255,0,0,0);
    public const long CountCap=99999;

    sealed record Tab(int Page,Rectangle Rect,UiSurface? Normal,UiSurface? Selected);
    UiSurface? frame,selectMark,lockOff,lockOn;
    Point frameAt,selectAt,iconAt;
    Rectangle lockRect;
    Tab[] tabs=[];
    IUiMacroState? macro;bool inWorld;

    public string Id=>WindowId;
    public UiLayer Layer=>UiLayer.Bar;
    public Rectangle Bounds{get;private set;}
    public bool Visible=>inWorld&&macro!=null&&frame!=null;
    /// <summary>Logical rectangle of the frame image (643,476,157,124 with the real 3176).</summary>
    public Rectangle FrameRect=>frame==null?Rectangle.Empty:new(frameAt,new(frame.Width,frame.Height));
    public Rectangle LockRect=>lockRect;
    public Point IconOrigin=>iconAt;
    public Point SelectOrigin=>selectAt;
    public IReadOnlyList<Rectangle> TabRects=>tabs.Select(t=>t.Rect).ToArray();

    public void Load(UiLayout layout,SurfCatalog surfs){
        var w=layout.Root.Child(WindowId);int x=w.Int("x"),y=w.Int("y");
        var frameNode=w.Child("MACRO_MAIN_WINDOW/MACRO_FRAME");
        frame=surfs.Get(MacroLayout.FirstSurf(frameNode));frameAt=MacroLayout.At(frameNode,x,y);
        var mark=w.Child("ETC/SELECT_MARK");selectMark=surfs.Get(MacroLayout.FirstSurf(mark));selectAt=MacroLayout.At(mark,x,y);
        iconAt=MacroLayout.At(w.Child("ETC/MACRO_ICON"),x,y);   // surfNumber="" here: the icon is chosen at runtime
        var tabNodes=w.Child("BUTTON_TAB_LIST").ChildrenNamed("TAB_BUTTON").Where(n=>n.Attr("title")=="MACRO_SELECT").ToArray();
        var list=new List<Tab>();
        foreach(var n in tabNodes){
            int page=n.Int("action")-FirstTabAction;
            if(page is <0 or >=MacroBook.PageCount||list.Any(t=>t.Page==page))throw new UiLayoutException(layout.FileName,n.Path,$"MACRO_SELECT action {page+FirstTabAction} 不在 10–12 或重複");
            int a=MacroLayout.FirstSurf(n);var normal=surfs.Get(a);var at=MacroLayout.At(n,x,y);
            list.Add(new(page,MacroLayout.Rect(at,normal),normal,surfs.Get(a+TabSelectedOffset)));
        }
        if(list.Count!=MacroBook.PageCount)throw new UiLayoutException(layout.FileName,w.Path+"/BUTTON_TAB_LIST",$"MACRO_SELECT 分頁鈕應有 {MacroBook.PageCount} 個，實際 {list.Count}");
        tabs=list.OrderBy(t=>t.Page).ToArray();
        var locks=w.Child("BUTTON_ONOFF_LIST").ChildrenNamed("ONOFF_BUTTON").Where(n=>n.Attr("title")=="MACRO_LOCK").ToArray();
        if(locks.Length!=1)throw new UiLayoutException(layout.FileName,w.Path+"/BUTTON_ONOFF_LIST","MACRO_LOCK 應恰有 1 個");
        int lockSurf=MacroLayout.FirstSurf(locks[0]);lockOff=surfs.Get(lockSurf);lockOn=surfs.Get(lockSurf+LockOnOffset);
        lockRect=MacroLayout.Rect(MacroLayout.At(locks[0],x,y),lockOff);
        // MACRO_LOCK sits at xRPos=-9, i.e. left of the frame (over the BAR_485 slot's last 9 columns), so Bounds is the union.
        Bounds=MacroLayout.Union(FrameRect,lockRect);
    }

    public void Update(IUiGameState state){inWorld=state.InWorld;macro=state as IUiMacroState;}

    /// <summary>Top-left of slot k's icon: (iconX + 34·(k%4), iconY + 34·(k/4)).</summary>
    public Point SlotIcon(int k)=>new(iconAt.X+Pitch*(k%Columns),iconAt.Y+Pitch*(k/Columns));
    public Point SlotMark(int k)=>new(selectAt.X+Pitch*(k%Columns),selectAt.Y+Pitch*(k/Columns));
    /// <summary>Click → slot (TW13 sub_782740): (mx−markX)/34 + 4·((my−markY)/34), only inside the 4×2 grid.</summary>
    public int SlotAt(Point p){
        int dx=p.X-selectAt.X,dy=p.Y-selectAt.Y;
        if(dx<0||dy<0||dx>=Pitch*Columns||dy>=Pitch*(MacroBook.SlotsPerPage/Columns))return -1;
        return dx/Pitch+Columns*(dy/Pitch);
    }

    public void Paint(UiCanvas canvas){
        if(macro==null)return;var book=macro.Macros;
        canvas.DrawSurface(frame,frameAt.X,frameAt.Y);
        foreach(var t in tabs)canvas.DrawSurface(t.Page==book.Page?t.Selected:t.Normal,t.Rect.X,t.Rect.Y);
        for(int k=0;k<MacroBook.SlotsPerPage;k++)MacroLayout.PaintSlot(canvas,macro,book.Binding(book.Page,k),SlotIcon(k),SlotMark(k));
        if(!book.SelectionInExtended){var m=SlotMark(book.Selected);canvas.DrawSurface(selectMark,m.X,m.Y);}
        canvas.DrawSurface(book.Locked?lockOn:lockOff,lockRect.X,lockRect.Y);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(macro==null||e.Kind!=UiMouseKind.Down||e.Button!=MouseButtons.Left)return false;
        var book=macro.Macros;
        if(!lockRect.IsEmpty&&lockRect.Contains(e.Location)){book.ToggleLock();return true;}
        foreach(var t in tabs)if(!t.Rect.IsEmpty&&t.Rect.Contains(e.Location)){book.SetPage(t.Page);return true;}
        int k=SlotAt(e.Location);if(k<0)return false;
        book.Select(k);
        // Double click uses the slot like its F-key (TW13 sub_782740 case 515 → sub_782480).
        if(e.Clicks>=2)MacroBook.TryActivate(macro,book.Binding(book.Page,k),commands);
        return true;
    }

    /// <summary>
    /// F1–F3 = tab, F5–F12 = slot of the current tab (V5 (b)). Only without modifiers (Ctrl+F is the dev teleport list, Alt+F2/F3
    /// are window/slot modes) and only with game keyboard focus. Auto-repeat and key-up are swallowed so nothing else sees them.
    /// </summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(macro==null||!inWorld||e.Modifiers!=Keys.None)return false;
        int page=MacroBook.KeyPage(e.KeyCode),slot=MacroBook.KeySlot(e.KeyCode);
        if(page<0&&slot<0)return false;
        if(!e.Down)return true;
        if(!macro.InputAllowed)return false;
        if(e.Repeat)return true;
        var book=macro.Macros;
        if(page>=0){book.SetPage(page);return true;}
        book.Select(slot);   // 推測: the key also moves the select mark (sub_782480 argument 0 not traced)
        MacroBook.TryActivate(macro,book.Binding(book.Page,slot),commands);
        return true;
    }
}

/// <summary>Shared layout/paint helpers of the macro panels.</summary>
static class MacroLayout {
    public static int FirstSurf(UiNode n){var s=n.Surfs();return s.Length>0?s[0]:throw new UiLayoutException(n.Layout.FileName,n.Path,"surfNumber 是空的");}
    /// <summary>xRPos/yRPos are relative to the window; the parser adds the window x/y (V5 (a)).</summary>
    public static Point At(UiNode n,int x,int y)=>new(x+n.Int("xRPos"),y+n.Int("yRPos"));
    public static Rectangle Rect(Point at,UiSurface? s)=>s==null?Rectangle.Empty:new(at,new(s.Width,s.Height));
    public static Rectangle Union(Rectangle a,Rectangle b)=>a.IsEmpty?b:b.IsEmpty?a:Rectangle.Union(a,b);

    /// <summary>
    /// One slot: item icon (existing engine icon) and, for items with count &gt; 1, the count right-aligned at
    /// x = markX + 27 − 5·digits, y = markY + 22 (V5 (a)). Magic has no verified icon source yet → nothing drawn.
    /// Unresolved bindings (item gone, spell not learned) draw nothing.
    /// </summary>
    public static void PaintSlot(UiCanvas canvas,IUiMacroState state,PlayerHudBinding? binding,Point icon,Point mark){
        if(state.Inventory?.Resolve(binding) is not {} entry)return;
        if(entry.IconId is ushort id&&state.ItemIcon(id) is {} bitmap)canvas.DrawBitmap(bitmap,icon.X,icon.Y);
        if(entry.Binding.Kind==PlayerHudKind.Item&&entry.Count is long count&&count>1){
            string text=Math.Min(count,MacroPanel.CountCap).ToString(System.Globalization.CultureInfo.InvariantCulture);
            canvas.DrawText(text,mark.X+27-5*text.Length,mark.Y+22,MacroPanel.CountColor,MacroPanel.CountShadow);
        }
    }
}
