using System.Drawing.Imaging;
namespace P06.NativeUi;

// W2-I (Issue #562; docs/reference/windows-2.md §2, §9): the 3.81-generation legacy windows read at runtime from the local
// client's uimagicitem.xml (ITEM_WINDOW, SPELL_WINDOW) and uiextra86.xml (WAREHOUSE_WINDOW). This file holds the shared
// layout reader / window base and the item window; SpellWindowPanel.cs and WarehouseWindowPanel.cs hold the other two.
//
// What is drawn and where (windows-2.md §2.2 table, 已驗證): only elements whose xPos/yPos are in the XML, at
// window origin + (xPos, yPos), in XML order. Not in the XML and therefore NOT drawn (待驗證): the item / spell / warehouse
// cell grid and its icons, the scrollbar knob (no position), the warehouse 3003/3005 and mail 2303/2304 buttons (no
// position). Those parts are kept as data only (lists, selection, scroll offset) until their geometry is verified.

/// <summary>One element of a legacy window section, in XML order. Offset is null when the XML gives no xPos/yPos.</summary>
public sealed record LegacyElement(int Order,string Kind,string SurfName,int Surf,Point? Offset,string Path);

/// <summary>
/// A legacy "*_WINDOW" section: &lt;ITEM_WINDOW x y&gt; with flat children (SURF_IMAGE / IMAGE / IMAGE_BTN) identified by
/// surfName. Coordinates are xPos/yPos relative to the window (windows-2.md §2.2 lists them that way, e.g. base 2046 (6, 8)).
/// </summary>
public sealed class LegacyWindowLayout {
    public string FileName{get;}
    public string WindowId{get;}
    /// <summary>The section's own x/y (0, 0 in the real files); the runtime placement of the original is 待驗證.</summary>
    public Point XmlOrigin{get;}
    public IReadOnlyList<LegacyElement> Elements{get;}
    LegacyWindowLayout(string file,string id,Point origin,IReadOnlyList<LegacyElement> elements){FileName=file;WindowId=id;XmlOrigin=origin;Elements=elements;}

    public static LegacyWindowLayout Read(UiLayout layout,string windowId){
        var w=layout.Root.Child(windowId);
        var list=new List<LegacyElement>();
        foreach(var n in w.Children){
            var surfs=n.Surfs();
            if(surfs.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"surfNumber 是空的");
            int? x=n.TryInt("xPos"),y=n.TryInt("yPos");
            if(x.HasValue!=y.HasValue)throw new UiLayoutException(layout.FileName,n.Path,"xPos／yPos 只有其中一個");
            list.Add(new(list.Count,n.Name,(n.Attr("surfName")??"").Trim(),surfs[0],x.HasValue?new Point(x.Value,y!.Value):null,n.Path));
        }
        if(list.Count==0)throw new UiLayoutException(layout.FileName,w.Path,"視窗沒有任何元素");
        return new(layout.FileName,windowId,new(w.Int("x"),w.Int("y")),list);
    }

    /// <summary>The n-th element (0-based) with this surfName; missing → UiLayoutException (no default coordinates).</summary>
    public LegacyElement Find(string surfName,int occurrence=0){
        var e=Elements.Where(x=>x.SurfName==surfName).Skip(occurrence).FirstOrDefault();
        return e??throw new UiLayoutException(FileName,WindowId,$"缺少元素 surfName={surfName}（第 {occurrence+1} 個）");
    }
    public int Count(string surfName)=>Elements.Count(x=>x.SurfName==surfName);
    /// <summary>Element that must carry xPos/yPos.</summary>
    public LegacyElement Placed(string surfName,int occurrence=0){
        var e=Find(surfName,occurrence);
        return e.Offset!=null?e:throw new UiLayoutException(FileName,e.Path,$"{surfName} 沒有 xPos／yPos");
    }
}

/// <summary>
/// Shared body of the three legacy windows: a draggable UiLayer.Window panel that paints its placed parts in XML order and
/// handles the close button and the two scroll arrows. Hidden until opened (R3-A #678: menu buttons 3/4 and Tab／Ctrl+I／Ctrl+S
/// through MenuButtonsPanel, per TW13 — 推測 for the packed 3.81 LiTo.bin, FU4§1.2), and closed when the character leaves the world.
/// </summary>
public abstract class LegacyWindowPanel:IUiMovablePanel,IUiClosablePanel {
    /// <summary>A painted part: offset relative to the window origin; Show (null = always) selects one image of a state group.</summary>
    protected sealed record Part(int Order,string Name,Point Offset,UiSurface? Image,Func<bool>? Show=null);
    readonly List<Part> parts=[];
    Point origin;
    Rectangle extent;   // union of all part rectangles relative to the origin (stable, independent of Show)
    Part? close,scrollUp,scrollDown;
    bool inWorld,loaded;

    public abstract string Id{get;}
    /// <summary>Layout base name in Tile.pak (UiXmlSource tries "-c" first, then the plain name).</summary>
    public abstract string LayoutName{get;}
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds=>extent.IsEmpty?Rectangle.Empty:new(origin.X+extent.X,origin.Y+extent.Y,extent.Width,extent.Height);
    public virtual bool Visible=>loaded&&inWorld&&IsOpen;
    public bool IsOpen{get;private set;}
    public Point Origin=>origin;
    public LegacyWindowLayout? Window{get;private set;}
    public Rectangle CloseRect=>RectOf(close);
    /// <summary>Upper arrow (scroll towards the start) and lower arrow (towards the end), chosen by position, not by name.</summary>
    public Rectangle ScrollUpRect=>RectOf(scrollUp);
    public Rectangle ScrollDownRect=>RectOf(scrollDown);
    /// <summary>First visible entry. The step and the page size of the original list are 待驗證; one arrow click = one entry.</summary>
    public int ScrollOffset{get;private set;}

    public void Open(){if(loaded)IsOpen=true;}
    public void Close()=>IsOpen=false;
    public void Toggle(){if(IsOpen)Close();else Open();}

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>Same as Load(layout, surfs) with any surf source (offline checks pass synthetic surfaces).</summary>
    public void Load(UiLayout layout,Func<int,UiSurface?> surfs){
        parts.Clear();close=scrollUp=scrollDown=null;loaded=false;
        var w=LegacyWindowLayout.Read(layout,Id);
        Build(w,surfs);
        if(close==null)throw new UiLayoutException(w.FileName,w.WindowId,"沒有關閉鈕");
        extent=Rectangle.Empty;
        foreach(var p in parts)if(p.Image!=null)extent=extent.IsEmpty?LocalRect(p):Rectangle.Union(extent,LocalRect(p));
        parts.Sort((a,b)=>a.Order.CompareTo(b.Order));
        Window=w;origin=w.XmlOrigin;ScrollOffset=0;loaded=true;
    }
    /// <summary>Subclass adds its parts with AddPart / AddChrome.</summary>
    protected abstract void Build(LegacyWindowLayout w,Func<int,UiSurface?> surfs);

    protected Part AddPart(LegacyElement e,Func<int,UiSurface?> surfs,Func<bool>? show=null,Point? offset=null){
        var at=offset??e.Offset??throw new UiLayoutException(Window?.FileName??"",e.Path,$"{e.SurfName} 沒有 xPos／yPos");
        var p=new Part(e.Order,e.SurfName,at,surfs(e.Surf),show);parts.Add(p);return p;
    }
    /// <summary>
    /// Base, frame, close button, scrollbar and the two arrows. The frame element has no xPos/yPos in the XML; it is placed at
    /// the window origin (推測: an absent attribute reads as 0, the base sits inside it at (6, 8) / (6, 9)).
    /// </summary>
    protected void AddChrome(LegacyWindowLayout w,Func<int,UiSurface?> surfs,string baseName,string frameName,string closeName,string barName,string upName,string downName){
        AddPart(w.Placed(baseName),surfs);
        var frame=w.Find(frameName);AddPart(frame,surfs,offset:frame.Offset??Point.Empty);
        close=AddPart(w.Placed(closeName),surfs);
        AddPart(w.Placed(barName),surfs);
        var a=AddPart(w.Placed(upName),surfs);var b=AddPart(w.Placed(downName),surfs);
        (scrollUp,scrollDown)=a.Offset.Y<=b.Offset.Y?(a,b):(b,a);
    }

    static Rectangle LocalRect(Part p)=>p.Image==null?Rectangle.Empty:new(p.Offset,new(p.Image.Width,p.Image.Height));
    protected Rectangle RectOf(Part? p)=>p?.Image==null?Rectangle.Empty:new(origin.X+p.Offset.X,origin.Y+p.Offset.Y,p.Image.Width,p.Image.Height);
    /// <summary>Logical position of an XML offset (for parts with a missing surf, whose rectangle is empty).</summary>
    public Point At(Point offset)=>new(origin.X+offset.X,origin.Y+offset.Y);
    /// <summary>Parts drawn right now (XML order, after the Show filter) — for checks and diagnostics.</summary>
    public IReadOnlyList<(string Name,Rectangle Rect,int? Surf)> PaintedParts=>parts.Where(p=>p.Show?.Invoke()!=false).Select(p=>(p.Name,RectOf(p),p.Image?.Surf)).ToArray();

    /// <summary>Number of entries the arrows scroll through (0 = nothing to scroll).</summary>
    protected virtual int ScrollCount=>0;
    public void ScrollBy(int delta){ScrollOffset=Math.Clamp(ScrollOffset+delta,0,Math.Max(0,ScrollCount-1));}
    protected void ResetScroll()=>ScrollOffset=0;

    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        if(!inWorld)IsOpen=false;
        OnUpdate(state);
        ScrollBy(0);
    }
    protected virtual void OnUpdate(IUiGameState state){}

    public void Paint(UiCanvas canvas){
        foreach(var p in parts)if(p.Show?.Invoke()!=false)canvas.DrawSurface(p.Image,origin.X+p.Offset.X,origin.Y+p.Offset.Y);
        PaintContent(canvas);
    }
    /// <summary>R4-B (#706): cells, icons, counts and row text, drawn above the XML parts.</summary>
    protected virtual void PaintContent(UiCanvas canvas){}
    /// <summary>Window-relative point (for LegacyCellGrid hit tests).</summary>
    protected Point Local(Point logical)=>new(logical.X-origin.X,logical.Y-origin.Y);
    protected Rectangle Logical(Rectangle local)=>local.IsEmpty?Rectangle.Empty:new(origin.X+local.X,origin.Y+local.Y,local.Width,local.Height);

    /// <summary>Selection highlight (no surf exists for it in the legacy XML; a 1-pixel frame is drawn, 推測).</summary>
    protected static readonly Color SelectedColor=Color.FromArgb(255,230,120),TextColor=Color.White,TextShadow=Color.Black;
    protected static void DrawFrame(UiCanvas canvas,Rectangle r,Color color){
        if(r.Width<2||r.Height<2)return;
        using var b=new SolidBrush(color);var g=canvas.Graphics;
        g.FillRectangle(b,r.X,r.Y,r.Width,1);g.FillRectangle(b,r.X,r.Bottom-1,r.Width,1);
        g.FillRectangle(b,r.X,r.Y,1,r.Height);g.FillRectangle(b,r.Right-1,r.Y,1,r.Height);
    }
    /// <summary>One line of text, cut with "…" so it ends before maxRight (logical x).</summary>
    protected static void DrawClipped(UiCanvas canvas,string text,int x,int y,int maxRight,Color color){
        if(string.IsNullOrEmpty(text)||maxRight<=x)return;
        string s=text;
        while(s.Length>0&&x+UiText.Measure(canvas.Graphics,s).Width>maxRight)s=s.Length>1?s[..^2]+"…":"";
        canvas.DrawText(s,x,y,color,TextShadow);
    }
    protected static void DrawRightAligned(UiCanvas canvas,string text,int right,int y,Color color){
        if(string.IsNullOrEmpty(text))return;
        canvas.DrawText(text,right-UiText.Measure(canvas.Graphics,text).Width,y,color,TextShadow);
    }

    /// <summary>Clickable rectangles besides close/arrows (subclasses); they are never drag handles.</summary>
    protected virtual IEnumerable<Rectangle> Controls()=>[];
    protected virtual bool Click(Point p,IUiCommands commands)=>false;
    /// <summary>Left press inside the window that is not close/arrow; the event carries Clicks (2 = double click). Default → Click(point).</summary>
    protected virtual bool Click(UiMouseEvent e,IUiCommands commands)=>Click(e.Location,commands);
    /// <summary>Left button released (the window keeps the mouse captured after its own press, so drags end here).</summary>
    protected virtual void Release(Point p,IUiCommands commands){}
    /// <summary>Close button. Subclasses may also notify the game (e.g. warehouse cancel).</summary>
    protected virtual void CloseClicked(IUiCommands commands)=>Close();

    bool OnControl(Point p)=>CloseRect.Contains(p)||ScrollUpRect.Contains(p)||ScrollDownRect.Contains(p)||Controls().Any(r=>r.Contains(p));
    /// <summary>Drag from any point of the window that is not a control (推測: the original drag area is not traced).</summary>
    public bool IsDragHandle(Point location)=>Visible&&Bounds.Contains(location)&&!OnControl(location);
    public void MoveTo(Point location){var b=Bounds;origin=new(origin.X+location.X-b.X,origin.Y+location.Y-b.Y);}

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(e.Kind==UiMouseKind.Up&&e.Button==MouseButtons.Left){Release(e.Location,commands);return true;}   // captured: may be outside
        if(!Bounds.Contains(e.Location))return false;
        if(e.Kind!=UiMouseKind.Down||e.Button!=MouseButtons.Left)return true;   // the window swallows everything over it
        if(CloseRect.Contains(e.Location)){CloseClicked(commands);return true;}
        if(ScrollUpRect.Contains(e.Location)){ScrollBy(-1);return true;}
        if(ScrollDownRect.Contains(e.Location)){ScrollBy(1);return true;}
        Click(e,commands);
        return true;
    }
    /// <summary>
    /// No window hotkeys here: Tab／Ctrl+I, Ctrl+S (FU4§1.1) are dispatched by MenuButtonsPanel (R3-A #678), which stays visible
    /// while these windows are closed. Subclasses may take keys that act on their own content (R9-L: Delete in the item window).
    /// </summary>
    public virtual bool Key(UiKeyEvent e,IUiCommands commands)=>false;

    /// <summary>
    /// Walks the product's game-state decorators (HP/MP → macro → left status → chat → party → buff → red notice → the legacy
    /// window decorators that R3-A (#678) puts innermost) to find a module state.
    /// </summary>
    public static T? FindState<T>(IUiGameState? state) where T:class{
        for(int depth=0;state!=null&&depth<16;depth++){
            if(state is T t)return t;
            state=state switch{UiHpMpGameState h=>h.Base,UiMacroGameState m=>m.Inner,UiLeftStatusGameState l=>l.Inner,UiChatGameState c=>c.Inner,UiPartyGameState q=>q.Inner,UiBuffGameState b=>b.Inner,UiRedNoticeGameState n=>n.Inner,ILegacyWindowStateDecorator d=>d.Inner,_=>null};
        }
        return null;
    }
    /// <summary>Same walk for the command decorators (UiMacroCommands wraps the bridge commands).</summary>
    public static T? FindCommands<T>(IUiCommands? commands) where T:class{
        for(int depth=0;commands!=null&&depth<12;depth++){
            if(commands is T t)return t;
            commands=commands switch{UiMacroCommands m=>m.Inner,ILegacyWindowCommandsDecorator d=>d.Inner,_=>null};
        }
        return null;
    }
}

/// <summary>A game-state decorator for the legacy windows (lets several be stacked without knowing each other).</summary>
public interface ILegacyWindowStateDecorator { IUiGameState Inner{get;} }
public interface ILegacyWindowCommandsDecorator { IUiCommands Inner{get;} }

/// <summary>Data for the item window: the existing inventory model (Inventory.cs), read-only.</summary>
public interface IUiItemWindowState {
    /// <summary>null before the server inventory is known (nothing is listed, nothing is estimated).</summary>
    IReadOnlyList<InventoryItem>? Items{get;}
    /// <summary>The game's selected item (WorldSession.SelectedItemId), shared with the WinForms inventory list.</summary>
    uint? SelectedId=>null;
    /// <summary>Selects an item in the game model (local state only, no packet: WorldSession.SelectItem). false = not taken.</summary>
    bool Select(uint? id)=>false;
}
public sealed record UiItemWindowData(IReadOnlyList<InventoryItem>? Items,uint? SelectedId=null,Func<uint?,bool>? Selector=null):IUiItemWindowState {
    /// <summary>Adapter over WorldSession (Inventory.cs): items only once the server list arrived; selection = WorldSession.SelectItem.</summary>
    public static UiItemWindowData From(WorldSession world)=>new(world.InventoryLoaded?world.Inventory:null,world.SelectedItemId,world.SelectItem);
    public bool Select(uint? id)=>Selector?.Invoke(id)??false;
}
public sealed class UiItemWindowGameState(IUiGameState inner,IUiItemWindowState items):IUiGameState,IUiItemWindowState,ILegacyWindowStateDecorator {
    public IUiGameState Inner=>inner;
    public bool InWorld=>inner.InWorld;public string? CharacterName=>inner.CharacterName;public int? Level=>inner.Level;
    public long? Hp=>inner.Hp;public long? MaxHp=>inner.MaxHp;public long? Mp=>inner.Mp;public long? MaxMp=>inner.MaxMp;public long Now=>inner.Now;
    public IReadOnlyList<InventoryItem>? Items=>items.Items;
    public uint? SelectedId=>items.SelectedId;
    public bool Select(uint? id)=>items.Select(id);
}

/// <summary>
/// ITEM_WINDOW of uimagicitem.xml (windows-2.md §2.2): base 2046 (6, 8), frame 2047, close 2037 (179, 15), scrollbar 2039
/// (171, 58), arrows 2042 (171, 44) / 2040 (171, 225), knob 2044 (no position → not drawn), four tab images 2048–2051 all at
/// (27, 235), trash 2052 (142, 260), chest 2054 (117, 260).
/// </summary>
/// <remarks>
/// R9-L（T372／#856，FU9§13）背包操作（TW16 RenewalInventoryUI 為準，3.81 LiTo.bin 有殼）：
/// - 格子雙擊 → 使用（sub_9A6FB0 → sub_9930B0 → C208，已驗證；觸發事件是雙擊屬推測）。引擎：同一格第二下（Clicks ≥ 2）→ 宿主
///   Use（選取後走既有 UseSelectedItem，同 U 鍵；U 鍵保留）。
/// - 垃圾桶 InvDelButton（DeleteItem → sub_9A54D0：514 且有選取）與 Delete 鍵（4142 → 同一個 sub_9A52C0，已驗證）→ 宿主 Delete
///   （LoginConnection.BeginInventoryDelete：單件 668 → alertitems.tbl 內再 3122；堆疊先數量 3054 → alertitems.tbl 內再 4114）。
///   聊天輸入列開著時 Delete 鍵給聊天列。沒有選取或沒有宿主 → 不詢問、不送。
/// - 寶箱 2054：TW16 窗沒有對應事件、3.81 用途待驗證 → 只畫圖，不做（FU9§13.3）。
/// - Esc（27 關窗）不在本組；全域 ESC（R7-H）仍會關本窗。
/// </remarks>
public interface IItemWindowHost {
    /// <summary>雙擊使用。true＝已受理（選取並開始送出）。</summary>
    bool Use(uint objectId);
    /// <summary>垃圾桶／Delete：開始刪除詢問。true＝已受理。</summary>
    bool Delete(uint objectId);
}

public sealed class ItemWindowPanel(ItemIconSource? icons=null,Func<IItemWindowHost?>? host=null,Func<string,IUiPanel?>? find=null):LegacyWindowPanel {
    [UiPanel(175)] internal static UiPanelRegistration Registration=>new(WindowId,Layout,root=>new ItemWindowPanel(ItemIconSource.FromRoot(root),find:root.Find));   // R3-A (#678): menu 4 / Tab / Ctrl+I; R4-B (#706): icons = <icon>.tbt; R9-L (#856): host + chat lookup
    /// <summary>R9-L（#856）：雙擊使用與刪除的送出端（ClientForm.InitItemTransfer 設定）；null → 不使用、不刪除。</summary>
    public static IItemWindowHost? Host{get;set;}
    readonly Func<IItemWindowHost?> host=host??(()=>Host);
    public const string WindowId="ITEM_WINDOW",Layout="uimagicitem";
    public const int TabCount=4;
    /// <summary>Tab images in XML order: 全部／裝備／任務／其他 (from the surfName suffixes ALL/EQUIP/QUEST/ETC).</summary>
    public static readonly string[] TabNames=["SURF_RENEWAL_BTN_ITEM_TAB_ALL","SURF_RENEWAL_BTN_ITEM_TAB_EQUIP","SURF_RENEWAL_BTN_ITEM_TAB_QUEST","SURF_RENEWAL_BTN_ITEM_TAB_ETC"];
    public override string Id=>WindowId;
    public override string LayoutName=>Layout;
    Part? trash,chest;Part[] tabParts=[];
    IReadOnlyList<InventoryItem>? items;
    IUiItemWindowState? source;uint? localSelection;

    /// <summary>Icon source (Sprite "&lt;icon&gt;.tbt"); None draws no icons (counts and selection still drawn).</summary>
    public ItemIconSource Icons{get;set;}=icons??ItemIconSource.None;
    /// <summary>"$n" name resolver (ItemText.Resolve by default; offline checks pass their own table).</summary>
    public Func<string,string> ResolveName{get;set;}=ItemText.Resolve;

    /// <summary>
    /// Selected tab. The four images share one position, so each is read as the whole tab strip with one tab lit and only the
    /// selected one is drawn (推測). R4-B: a click on the strip picks the tab by equal quarters of its width (推測).
    /// </summary>
    public int Tab{get;private set;}
    public void SetTab(int tab){if(tab is <0 or >=TabCount)throw new ArgumentOutOfRangeException(nameof(tab));if(tab!=Tab){Tab=tab;ResetScroll();}}
    public Rectangle TabStripRect=>tabParts.Length==0?Rectangle.Empty:RectOf(tabParts[Tab]);
    public Rectangle TrashRect=>RectOf(trash);
    public Rectangle ChestRect=>RectOf(chest);
    public IReadOnlyList<InventoryItem>? Items=>items;

    /// <summary>
    /// Cell grid (R4-B, 推測 — the XML has no cell geometry): 33-pixel cells between the base's left edge and the scrollbar, and
    /// between the upper arrow and the tab strip. With the local uimagicitem.xml that is 5 × 5 cells from (6, 57).
    /// </summary>
    public LegacyCellGrid Grid{get;private set;}=LegacyCellGrid.Empty;

    /// <summary>
    /// Entries of the current tab. 全部 = every item; 裝備 = EquipmentRules.IsEquipment(use type), the engine's existing rule;
    /// 其他 = everything that is not equipment. 任務 is empty: 3.81 S_AddItem carries no quest attribute and the server sends no
    /// quest-item flag (推測; the TW16 quest flag lives in the protobuf item data, PBW§3), so nothing can be placed there.
    /// </summary>
    public IReadOnlyList<InventoryItem> TabItems=>items==null?[]:Tab switch{
        1=>items.Where(i=>EquipmentRules.IsEquipment(i.UseType)).ToArray(),
        2=>Array.Empty<InventoryItem>(),
        3=>items.Where(i=>!EquipmentRules.IsEquipment(i.UseType)).ToArray(),
        _=>items};
    /// <summary>Arrow scroll is by grid row: ScrollOffset = first visible row.</summary>
    protected override int ScrollCount=>Grid.ScrollPositions(TabItems.Count);
    /// <summary>Items on the visible page, slot order (row-major).</summary>
    public IReadOnlyList<InventoryItem> PageItems=>TabItems.Skip(ScrollOffset*Grid.Columns).Take(Grid.PageSize).ToArray();
    /// <summary>Selected item: the game's selection when the state provides it, else the window's own (offline checks).</summary>
    public uint? SelectedId=>source?.SelectedId??localSelection;
    /// <summary>Logical rectangle of a visible slot.</summary>
    public Rectangle CellRect(int slot)=>Logical(Grid.CellRect(slot));
    public InventoryItem? ItemAt(Point logical)=>Grid.SlotAt(Local(logical)) is int s&&s<PageItems.Count?PageItems[s]:null;
    /// <summary>Display name of an item ("$n" resolved, trailing " (count)" removed) — for row text and tooltips.</summary>
    public string NameOf(InventoryItem item)=>ItemText.Name(ResolveName(item.Name),item.Count);

    protected override void Build(LegacyWindowLayout w,Func<int,UiSurface?> surfs){
        AddChrome(w,surfs,"SURF_RENEWAL_WIN_ITEM_BASE","SURF_RENEWAL_WIN_ITEM_FRAME","SURF_RENEWAL_BTN_PET_ITEM_X","SURF_RENEWAL_WIN_SCROLLBAR","SURF_RENEWAL_WIN_INC_BUT_OFF","SURF_RENEWAL_WIN_DEC_BUT_OFF");
        tabParts=new Part[TabCount];
        for(int k=0;k<TabCount;k++){int tab=k;tabParts[k]=AddPart(w.Placed(TabNames[k]),surfs,()=>Tab==tab);}
        trash=AddPart(w.Placed("SURF_RENEWAL_BTN_ITEM_WASTE_OFF"),surfs);
        chest=AddPart(w.Placed("SURF_RENEWAL_BTN_ITEM_CHEST_OFF"),surfs);
        var up=w.Placed("SURF_RENEWAL_WIN_INC_BUT_OFF").Offset!.Value;var down=w.Placed("SURF_RENEWAL_WIN_DEC_BUT_OFF").Offset!.Value;
        Grid=LegacyCellGrid.Fit(w.Placed("SURF_RENEWAL_WIN_ITEM_BASE").Offset!.Value.X,Math.Min(up.Y,down.Y),w.Placed("SURF_RENEWAL_WIN_SCROLLBAR").Offset!.Value.X,
            w.Placed(TabNames[0]).Offset!.Value.Y,new(LegacyCellGrid.IconPitch,LegacyCellGrid.IconPitch));
        if(Grid.PageSize==0)throw new UiLayoutException(w.FileName,w.WindowId,"格子區沒有空間（底圖左緣／捲軸／上箭頭／分頁的位置不合理）");
        Tab=0;localSelection=null;
    }
    protected override void OnUpdate(IUiGameState state){
        source=FindState<IUiItemWindowState>(state);items=source?.Items;
        if(localSelection is uint id&&items?.Any(i=>i.ObjectId==id)!=true)localSelection=null;
    }

    protected override IEnumerable<Rectangle> Controls()=>[TabStripRect,Logical(Grid.Area),TrashRect];
    protected override bool Click(UiMouseEvent e,IUiCommands commands){
        var p=e.Location;
        if(LegacyStrip.SegmentAt(TabStripRect,TabCount,p) is int tab){SetTab(tab);return true;}
        if(TrashRect.Contains(p)){DeleteSelected();return true;}
        if(ItemAt(p) is {} item){
            uint? before=SelectedId;
            SelectItem(item.ObjectId);
            // R9-L：同一格的第二下（雙擊）→ 使用。第一下已選取，所以「之前已選這一件」才算。
            if(e.Clicks>=2&&before==item.ObjectId)UseItem(item.ObjectId);
            return true;
        }
        return false;
    }
    /// <summary>R9-L：Delete（4142）＝垃圾桶。窗開著、沒有修飾鍵、不是自動重複；聊天輸入列開著時不收（字給聊天列）。</summary>
    public override bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible||!e.Down||e.Repeat||e.Modifiers!=Keys.None||e.KeyCode!=Keys.Delete)return false;
        if(find?.Invoke(ChatPanel.WindowName) is ChatPanel{Input.Open:true})return false;
        return DeleteSelected();
    }
    /// <summary>目前選取（且在背包清單內）的物品交給宿主刪除；沒有選取或沒有宿主 → false。</summary>
    public bool DeleteSelected()=>SelectedId is uint id&&items?.Any(i=>i.ObjectId==id)==true&&host() is {} h&&h.Delete(id);
    bool UseItem(uint id)=>host() is {} h&&h.Use(id);
    /// <summary>Select through the game model when available (shared with the WinForms list and the U key), else locally.</summary>
    public void SelectItem(uint? id){
        if(id!=null&&items?.Any(i=>i.ObjectId==id)!=true)return;
        if(source!=null&&source.Select(id))return;
        localSelection=id;
    }

    protected override void PaintContent(UiCanvas canvas){
        var page=PageItems;uint? selected=SelectedId;
        for(int s=0;s<page.Count;s++){
            var cell=CellRect(s);var item=page[s];
            ItemIconSource.Draw(canvas,Icons.For(item),cell.Location);
            DrawRightAligned(canvas,ItemText.Count(item.Count),cell.Right,cell.Bottom-UiText.PixelHeight,TextColor);
            if(item.ObjectId==selected)DrawFrame(canvas,cell,SelectedColor);
        }
    }

    // ---------------------------------------------------------------- offline checks (synthetic XML and surfaces only)

    /// <summary>Synthetic ITEM_WINDOW with the element order and attribute values of the local uimagicitem.xml (windows-2.md §2.2).</summary>
    internal const string TestXml="""
<?xml version="1.0"?>
<ExtraWindowConfigulation>
<ITEM_WINDOW x = "0" y = "0">
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_ITEM_BASE" surfNumber = "2046" xPos = "6" yPos = "8"/>
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_ITEM_FRAME" surfNumber = "2047"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_PET_ITEM_X" surfNumber = "2037" xPos = "179" yPos = "15"/>
	<IMAGE surfName = "SURF_RENEWAL_WIN_SCROLLBAR" surfNumber = "2039" xPos = "171" yPos = "58"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber = "2040" xPos = "171" yPos = "225"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber = "2042" xPos = "171" yPos = "44"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber = "2044"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_ITEM_TAB_ALL" surfNumber = "2048" xPos = "27" yPos = "235"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_ITEM_TAB_EQUIP" surfNumber = "2049" xPos = "27" yPos = "235"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_ITEM_TAB_QUEST" surfNumber = "2050" xPos = "27" yPos = "235"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_ITEM_TAB_ETC" surfNumber = "2051" xPos = "27" yPos = "235"/>
	<IMAGE surfName = "SURF_RENEWAL_BTN_ITEM_WASTE_OFF" surfNumber = "2052" xPos = "142" yPos = "260"/>
	<IMAGE surfName = "SURF_RENEWAL_BTN_ITEM_CHEST_OFF" surfNumber = "2054" xPos = "117" yPos = "260"/>
</ITEM_WINDOW>
</ExtraWindowConfigulation>
""";

    [WorldModuleTest("native-item-window")]
    static void Checks(WorldModuleTestContext t){
        var surf=LegacyWindowTestKit.Surfaces(new(){
            [2046]=(170,260),[2047]=(200,290),[2037]=(12,12),[2039]=(10,160),[2040]=(10,12),[2042]=(10,12),[2044]=(10,10),
            [2048]=(130,20),[2049]=(130,20),[2050]=(130,20),[2051]=(130,20),[2052]=(22,22),[2054]=(22,22)});
        var layout=UiLayout.Parse(TestXml,"uimagicitem.xml");
        var w=LegacyWindowLayout.Read(layout,WindowId);
        t.Equal(13,w.Elements.Count,"13 elements in XML order");
        t.Check(w.Find("SURF_RENEWAL_WIN_KNOB_BUT_OFF").Offset==null&&w.Find("SURF_RENEWAL_WIN_ITEM_FRAME").Offset==null,"knob and frame carry no position");

        var p=new ItemWindowPanel();p.Load(layout,surf.Get);
        t.Check(p.Layer==UiLayer.Window&&p.Id=="ITEM_WINDOW"&&p.LayoutName=="uimagicitem","window layer, uimagicitem.xml");
        t.Equal(new Rectangle(0,0,200,290),p.Bounds,"bounds = frame at the window origin ∪ parts");
        t.Equal(new Rectangle(179,15,12,12),p.CloseRect,"close 2037 at (179,15)");
        t.Equal(new Rectangle(171,44,10,12),p.ScrollUpRect,"upper arrow = INC 2042 at (171,44)");
        t.Equal(new Rectangle(171,225,10,12),p.ScrollDownRect,"lower arrow = DEC 2040 at (171,225)");
        t.Equal(new Rectangle(27,235,130,20),p.TabStripRect,"tab strip at (27,235)");
        t.Equal(new Rectangle(142,260,22,22),p.TrashRect,"trash 2052 at (142,260)");
        t.Equal(new Rectangle(117,260,22,22),p.ChestRect,"chest 2054 at (117,260)");
        t.Check(p.PaintedParts.Select(x=>x.Surf).SequenceEqual(new int?[]{2046,2047,2037,2039,2040,2042,2048,2052,2054}),"painted in XML order; knob not drawn; only tab 0",p.PaintedParts.Select(x=>x.Surf).ToArray());
        p.SetTab(3);
        t.Check(p.PaintedParts.Count(x=>x.Surf is >=2048 and <=2051)==1&&p.PaintedParts.Any(x=>x.Surf==2051),"tab 3 → only 2051 drawn");
        t.Throws<ArgumentOutOfRangeException>(()=>p.SetTab(4),"tab 4 does not exist");
        p.SetTab(0);

        // Visibility: hidden until opened, closed on leaving the world.
        var cmd=new UiNullCommands();
        t.Check(!p.Visible,"hidden before Open");
        p.Open();p.Update(new UiStaticGameState(InWorld:true));
        t.Check(p.Visible,"open in the world → visible");
        p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible&&!p.IsOpen,"leaving the world closes it");

        // Data: inventory only through the decorator; null list → nothing listed.
        InventoryItem Item(uint id,byte use,string name)=>new(id,1,use,0,10,1,1,1,name,"","");
        var inv=new[]{Item(1,1,"劍"),Item(2,2,"盔甲"),Item(3,0,"藥水"),Item(4,0,"卷軸")};
        var basic=new UiStaticGameState(InWorld:true);
        IUiGameState stacked=new UiHpMpGameState(new UiItemWindowGameState(basic,new UiItemWindowData(inv)),[]);
        p.Open();p.Update(stacked);
        t.Equal(4,p.TabItems.Count,"tab 全部 lists every item (found through UiHpMpGameState)");
        p.SetTab(1);
        t.Check(p.TabItems.Select(i=>i.ObjectId).SequenceEqual(EquipmentRules.IsEquipment(2)?new uint[]{1,2}:new uint[]{1}),"tab 裝備 = EquipmentRules.IsEquipment");
        p.SetTab(2);t.Equal(0,p.TabItems.Count,"任務: 3.81 has no quest attribute → empty (R4-B)");
        p.SetTab(3);t.Equal(4-p.TabItems.Count,inv.Count(i=>EquipmentRules.IsEquipment(i.UseType)),"其他 = not equipment (R4-B)");
        p.SetTab(0);
        p.Update(new UiItemWindowGameState(basic,new UiItemWindowData(null)));
        t.Check(p.Items==null&&p.TabItems.Count==0,"inventory not received → empty, nothing estimated");
        p.Update(new UiItemWindowGameState(basic,new UiItemWindowData(inv)));

        // Input: arrows scroll by grid row (4 items fit one 5×5 page → nothing to scroll; row scrolling: LegacyWindowContentTests);
        // close hides; clicks inside are swallowed; outside not taken.
        LegacyWindowTestKit.Click(p,new(175,50),cmd);t.Equal(0,p.ScrollOffset,"up at 0 stays 0");
        for(int k=0;k<6;k++)LegacyWindowTestKit.Click(p,new(175,230),cmd);
        t.Equal(0,p.ScrollOffset,"one page of items → down stays 0");
        p.SetTab(1);t.Equal(0,p.ScrollOffset,"tab change resets the scroll");
        t.Check(LegacyWindowTestKit.Click(p,new(100,100),cmd)&&p.Visible,"click on the body is swallowed");
        t.Check(!p.Mouse(new UiMouseEvent(UiMouseKind.Down,new(300,300),MouseButtons.Left,1,0,Keys.None),cmd),"outside bounds not taken");
        t.Check(!p.Key(new UiKeyEvent(Keys.I,true,false),cmd)&&!p.Key(new UiKeyEvent(Keys.Tab,true,false),cmd),"the window itself takes no keys (hotkeys live in MenuButtonsPanel, R3-A #678)");
        t.Check(p.IsDragHandle(new(190,150))&&!p.IsDragHandle(new(100,100))&&!p.IsDragHandle(new(180,16))&&!p.IsDragHandle(new(175,230)),"frame drags; cells and controls do not");
        LegacyWindowTestKit.Click(p,new(180,16),cmd);
        t.Check(!p.Visible&&!p.IsOpen&&cmd.Calls.Count==0,"close button hides the window and sends nothing");

        // Drag through UiRoot/UiInput: window stays inside 800×600; parts move with it.
        var root=new UiRoot();root.Add(p);p.Open();root.Update(stacked);
        var input=new UiInput(root,cmd);
        input.Mouse(UiMouseKind.Down,new(190,150),MouseButtons.Left,1);
        input.Mouse(UiMouseKind.Move,new(400,300));input.Mouse(UiMouseKind.Up,new(900,700),MouseButtons.Left);
        t.Equal(new Rectangle(600,310,200,290),p.Bounds,"dragged and clamped to the canvas (800−200, 600−290)");
        t.Equal(new Rectangle(779,325,12,12),p.CloseRect,"close button moved with the window");
        t.Check(root.HitTest(new(650,400))==p&&root.HitTest(new(100,100))==null,"hit test follows the window");

        // Pixels: synthetic colours identify what was drawn; a missing surf draws nothing (no placeholder).
        using(var canvas=new UiCanvas()){
            root.Compose(canvas,null);
            t.Equal(LegacyWindowTestKit.ColourOf(2037),canvas.Bitmap.GetPixel(785,330).ToArgb(),"close drawn at its moved position");
            t.Equal(LegacyWindowTestKit.ColourOf(2049),canvas.Bitmap.GetPixel(600+27+5,310+235+5).ToArgb(),"selected tab (1) strip drawn");
        }
        var sparse=LegacyWindowTestKit.Surfaces(new(){[2047]=(200,290),[2037]=(12,12)});
        var q=new ItemWindowPanel();q.Load(layout,sparse.Get);q.Open();q.Update(basic);
        t.Check(q.TrashRect.IsEmpty&&q.ScrollUpRect.IsEmpty&&q.PaintedParts.Count(x=>x.Surf!=null)==2,"missing surfs → empty rectangles, not painted");
        using(var canvas=new UiCanvas()){
            var r=new UiRoot();r.Add(q);r.Update(basic);r.Compose(canvas,null);
            t.Equal(LegacyWindowTestKit.ColourOf(2047),canvas.Bitmap.GetPixel(150,270).ToArgb(),"only the frame where the trash would be");
        }

        // Missing data is an error, not a default.
        t.Throws<UiLayoutException>(()=>new ItemWindowPanel().Load(UiLayout.Parse(TestXml.Replace("SURF_RENEWAL_BTN_ITEM_TAB_QUEST","X"),"uimagicitem.xml"),surf.Get),"missing tab element → UiLayoutException");
        t.Throws<UiLayoutException>(()=>new ItemWindowPanel().Load(UiLayout.Parse(TestXml.Replace("xPos = \"142\" ",""),"uimagicitem.xml"),surf.Get),"half a position → UiLayoutException");
        t.Throws<UiLayoutException>(()=>new ItemWindowPanel().Load(UiLayout.Parse("<?xml version=\"1.0\"?><ExtraWindowConfigulation/>","uimagicitem.xml"),surf.Get),"no ITEM_WINDOW → UiLayoutException");
        t.Check(!new ItemWindowPanel().Visible,"an unloaded panel never shows");

        // Optional local render with the real client (off by default and in CI): NATIVE_UI_LEGACY_RENDER=<dir under scratch>.
        LegacyWindowTestKit.RealClientRender(t);
    }
}

/// <summary>Synthetic surfaces and helpers for the legacy-window checks (no client files are read).</summary>
static class LegacyWindowTestKit {
    public sealed class SurfaceSet(Dictionary<int,UiSurface> map){public UiSurface? Get(int surf)=>map.GetValueOrDefault(surf);}
    /// <summary>Distinct opaque RGB555 colour per surf id.</summary>
    public static ushort Rgb555(int surf)=>(ushort)(0x0421+(surf*2654435761u>>17)%0x7BDE);
    public static int ColourOf(int surf)=>unchecked((int)UiImage.ToArgb(Rgb555(surf)));
    public static SurfaceSet Surfaces(Dictionary<int,(int W,int H)> sizes)=>new(sizes.ToDictionary(kv=>kv.Key,kv=>{
        var argb=Enumerable.Repeat(UiImage.ToArgb(Rgb555(kv.Key)),kv.Value.W*kv.Value.H).ToArray();
        return new UiSurface(kv.Key,kv.Key+".img","synthetic",new UiImage(kv.Value.W,kv.Value.H,0,0,argb));
    }));
    public static bool Click(IUiPanel p,Point at,IUiCommands cmd)=>p.Mouse(new UiMouseEvent(UiMouseKind.Down,at,MouseButtons.Left,1,0,Keys.None),cmd);

    /// <summary>
    /// Local-only: when NATIVE_UI_LEGACY_RENDER names a directory inside the repo's scratch/, load the three windows from the
    /// pinned client under TW_CLIENT_ROOT and write one PNG per window there (never into git). Skipped otherwise.
    /// </summary>
    public static void RealClientRender(WorldModuleTestContext t){
        string? dir=Environment.GetEnvironmentVariable("NATIVE_UI_LEGACY_RENDER");
        if(string.IsNullOrWhiteSpace(dir))return;
        dir=Path.GetFullPath(dir);
        if(!dir.Split(Path.DirectorySeparatorChar).Contains("scratch")){t.Check(false,"NATIVE_UI_LEGACY_RENDER must be under scratch/",dir);return;}
        Directory.CreateDirectory(dir);
        var res=UiResources.Shared;
        var lines=new List<string>();
        foreach(var p in new LegacyWindowPanel[]{new ItemWindowPanel(),new SpellWindowPanel(),new WarehouseWindowPanel()}){
            var layout=res.Xml.Load(p.LayoutName);p.Load(layout,res.Surfs);
            if(p is SpellWindowPanel s)s.SetClass(LegacySpellClass.General);
            if(p is WarehouseWindowPanel wh)wh.RenderWithoutData=true;
            p.Open();
            using var canvas=new UiCanvas();var root=new UiRoot();root.Add(p);root.Update(new UiStaticGameState(InWorld:true));
            root.Compose(canvas,null);
            var b=p.Bounds;using var crop=canvas.Bitmap.Clone(b.IsEmpty?new Rectangle(0,0,1,1):b,PixelFormat.Format32bppArgb);
            crop.Save(Path.Combine(dir,p.Id+".png"),ImageFormat.Png);
            lines.Add($"{p.Id} ({layout.FileName}) bounds={b}");
            foreach(var (name,rect,surf) in p.PaintedParts)lines.Add($"  {surf?.ToString()??"null",5} {name} {rect}");
            t.Check(p.PaintedParts.All(x=>x.Surf!=null),$"{p.Id}: every placed surf exists in the local client",p.PaintedParts.Where(x=>x.Surf==null).Select(x=>x.Name).ToArray());
        }
        File.WriteAllLines(Path.Combine(dir,"parts.txt"),lines);
    }
}
