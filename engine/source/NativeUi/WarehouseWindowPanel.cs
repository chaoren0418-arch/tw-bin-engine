namespace P06.NativeUi;

// W2-I (Issue #562; docs/reference/windows-2.md §2.1 item 5, §2.2): WAREHOUSE_WINDOW of uiextra86.xml.
// Shared reader/base: ItemWindowPanel.cs. No packet handling here: the warehouse list (S_RETRIEVE_LIST, engine op 176) and
// deposit/withdraw requests stay in NpcServices.cs; this panel only reads the existing interaction model.

/// <summary>One open warehouse list: "withdraw" (rows from S_RETRIEVE_LIST) or "deposit" (rows from the inventory).</summary>
public sealed record UiWarehouseView(string Kind,int Revision,IReadOnlyList<ShopRow> Rows,uint FeePerRow) {
    /// <summary>Adapter over WorldSession (NpcServices.cs / NpcInteraction.cs); null when no warehouse list is open.</summary>
    public static UiWarehouseView? From(WorldSession world)=>world.InteractionKind is "withdraw" or "deposit"
        ?new(world.InteractionKind,world.InteractionRevision,world.ShopRows,world.InteractionKind=="withdraw"?world.ShopRows.FirstOrDefault()?.Price??0:0):null;
}
public interface IUiWarehouseState { UiWarehouseView? Warehouse{get;} }
public sealed class UiWarehouseGameState(IUiGameState inner,UiWarehouseView? warehouse):IUiGameState,IUiWarehouseState,ILegacyWindowStateDecorator {
    public IUiGameState Inner=>inner;
    public bool InWorld=>inner.InWorld;public string? CharacterName=>inner.CharacterName;public int? Level=>inner.Level;
    public long? Hp=>inner.Hp;public long? MaxHp=>inner.MaxHp;public long? Mp=>inner.Mp;public long? MaxMp=>inner.MaxMp;public long Now=>inner.Now;
    public UiWarehouseView? Warehouse=>warehouse;
}
/// <summary>Outward warehouse actions; the product forwards them to the existing NPC service submit/close paths.</summary>
public interface IUiWarehouseCommands {
    void SubmitWarehouse(int revision,IReadOnlyList<ShopChoice> choices);
    void CancelWarehouse(int revision);
}
public enum WarehouseSortColumn { None, Name, Amount }

/// <summary>
/// WAREHOUSE_WINDOW (windows-2.md §2.2): base 3001 (6, 9), frame 3002, exit 3031 (225, 15), scrollbar 3024 (217, 61), arrows 3025
/// (217, 47) / 3027 (217, 285), total 3007 (19, 25), name 3008 (117, 35), amount 3009 (162, 35), sort buttons 3010/3012 at
/// (150, 37) and (206, 37), tabs 3020–3023 at (17, 300), OK 3014 (60, 326), cancel 3017 (127, 326). Elements without a position
/// (knob 3029, 3003/3005, mail 2303/2304) are not drawn. Opens by itself when a warehouse list arrives.
/// </summary>
public sealed class WarehouseWindowPanel:LegacyWindowPanel {
    public const string WindowId="WAREHOUSE_WINDOW",Layout="uiextra86";
    public const int TabCount=4;
    public static readonly string[] TabNames=["SURF_RENEWAL_WAREHOUSE_TAB_ALL","SURF_RENEWAL_WAREHOUSE_TAB_EQUIP","SURF_RENEWAL_WAREHOUSE_TAB_QUEST","SURF_RENEWAL_WAREHOUSE_TAB_ETC"];
    const string SortUp="SURF_RENEWAL_WAREHOUSE_SORT_UP_OFF",SortDown="SURF_RENEWAL_WAREHOUSE_SORT_DOWN_OFF";
    public override string Id=>WindowId;
    public override string LayoutName=>Layout;
    Part? ok,cancel,nameSort,amountSort;Part[] tabParts=[];
    UiWarehouseView? view;int? shownRevision;
    readonly Dictionary<uint,uint> selection=[];

    /// <summary>Local render check only: keep the window open without a warehouse list.</summary>
    internal bool RenderWithoutData{get;set;}
    public UiWarehouseView? View=>view;
    public override bool Visible=>base.Visible&&(view!=null||RenderWithoutData);
    public Rectangle OkRect=>RectOf(ok);
    public Rectangle CancelRect=>RectOf(cancel);
    public Rectangle NameSortRect=>RectOf(nameSort);
    public Rectangle AmountSortRect=>RectOf(amountSort);
    public Rectangle TabStripRect=>tabParts.Length==0?Rectangle.Empty:RectOf(tabParts[Tab]);

    /// <summary>Selected tab; one image per tab at one position (推測 strip, as in the item window). Not clickable (待驗證).</summary>
    public int Tab{get;private set;}
    public void SetTab(int tab){if(tab is <0 or >=TabCount)throw new ArgumentOutOfRangeException(nameof(tab));if(tab!=Tab){Tab=tab;ResetScroll();}}
    /// <summary>
    /// Sort state. Each column has an UP and a DOWN image at the same position; UP is drawn while that column sorts ascending
    /// (推測 from the names). Clicking a column's button sorts by it, a second click flips the direction. Default: server order.
    /// </summary>
    public WarehouseSortColumn SortColumn{get;private set;}
    public bool NameAscending{get;private set;}=true;
    public bool AmountAscending{get;private set;}=true;

    /// <summary>
    /// Rows in display order. ShopRow carries no use type, so the 裝備／任務／其他 tabs cannot be split (待驗證) and list every row.
    /// Sorting is ordinal by name, or by count; ties keep the server order.
    /// </summary>
    public IReadOnlyList<ShopRow> Rows{get{
        if(view==null)return [];
        IEnumerable<ShopRow> rows=view.Rows;
        return SortColumn switch{
            WarehouseSortColumn.Name=>(NameAscending?rows.OrderBy(r=>r.Name,StringComparer.Ordinal):rows.OrderByDescending(r=>r.Name,StringComparer.Ordinal)).ToArray(),
            WarehouseSortColumn.Amount=>(AmountAscending?rows.OrderBy(r=>r.Available):rows.OrderByDescending(r=>r.Available)).ToArray(),
            _=>view.Rows,
        };
    }}
    protected override int ScrollCount=>view?.Rows.Count??0;
    public IReadOnlyDictionary<uint,uint> Selection=>selection;
    /// <summary>Total fee of the selection: withdraw charges the per-row fee once per selected row (NpcServices: not per count).</summary>
    public long SelectedFee=>view?.Kind=="withdraw"?(long)view.FeePerRow*selection.Count:0;

    /// <summary>Selects count of a row (0 clears it). Rejects unknown rows and counts above what the list allows.</summary>
    public bool Select(uint id,uint count){
        var row=view?.Rows.FirstOrDefault(r=>r.Id==id);
        if(row==null||count>row.Available)return false;
        if(count==0)selection.Remove(id);else selection[id]=count;
        return true;
    }
    public IReadOnlyList<ShopChoice> Choices=>view==null?[]:view.Rows.Where(r=>selection.ContainsKey(r.Id)).Select(r=>new ShopChoice(r.Id,selection[r.Id])).ToArray();

    protected override void Build(LegacyWindowLayout w,Func<int,UiSurface?> surfs){
        AddChrome(w,surfs,"SURF_RENEWAL_WAREHOUSE_BASE","SURF_RENEWAL_WAREHOUSE_FRAME","SURF_RENEWAL_WAREHOUSE_EXIT_OFF","SURF_RENEWAL_WAREHOUSE_SCROLL_BAR","SURF_RENEWAL_WAREHOUSE_SCROLL_UP_OFF","SURF_RENEWAL_WAREHOUSE_SCROLL_DOWN_OFF");
        AddPart(w.Placed("SURF_RENEWAL_WAREHOUSE_TOTAL"),surfs);
        ok=AddPart(w.Placed("SURF_RENEWAL_WAREHOUSE_OK_OFF"),surfs);
        cancel=AddPart(w.Placed("SURF_RENEWAL_WAREHOUSE_CANCEL_OFF"),surfs);
        AddPart(w.Placed("SURF_RENEWAL_WAREHOUSE_NAME"),surfs);
        AddPart(w.Placed("SURF_RENEWAL_WAREHOUSE_AMOUNT"),surfs);
        if(w.Count(SortUp)!=2||w.Count(SortDown)!=2)throw new UiLayoutException(w.FileName,w.WindowId,"排序鈕應有名稱與數量兩組（UP／DOWN 各 2 個）");
        // XML order: name column's UP/DOWN at (150, 37) first, then the amount column's at (206, 37).
        nameSort=AddPart(w.Placed(SortUp,0),surfs,()=>NameAscending);
        AddPart(w.Placed(SortDown,0),surfs,()=>!NameAscending);
        amountSort=AddPart(w.Placed(SortUp,1),surfs,()=>AmountAscending);
        AddPart(w.Placed(SortDown,1),surfs,()=>!AmountAscending);
        tabParts=new Part[TabCount];
        for(int k=0;k<TabCount;k++){int tab=k;tabParts[k]=AddPart(w.Placed(TabNames[k]),surfs,()=>Tab==tab);}
        Tab=0;SortColumn=WarehouseSortColumn.None;NameAscending=AmountAscending=true;view=null;shownRevision=null;selection.Clear();
    }

    protected override void OnUpdate(IUiGameState state){
        var next=FindState<IUiWarehouseState>(state)?.Warehouse;
        if(next==null){view=null;shownRevision=null;selection.Clear();if(!RenderWithoutData)Close();return;}
        if(next.Revision!=shownRevision){
            // A new list (new interaction revision): open, reset selection/scroll/tab/sort.
            shownRevision=next.Revision;selection.Clear();ResetScroll();Tab=0;SortColumn=WarehouseSortColumn.None;NameAscending=AmountAscending=true;
            view=next;Open();
        }else view=next;
        foreach(var id in selection.Keys.ToArray())if(next.Rows.FirstOrDefault(r=>r.Id==id) is not {} row||selection[id]>row.Available)selection.Remove(id);
    }

    protected override IEnumerable<Rectangle> Controls()=>[OkRect,CancelRect,NameSortRect,AmountSortRect];
    protected override bool Click(Point p,IUiCommands commands){
        if(NameSortRect.Contains(p)){Sort(WarehouseSortColumn.Name);return true;}
        if(AmountSortRect.Contains(p)){Sort(WarehouseSortColumn.Amount);return true;}
        if(CancelRect.Contains(p)){CloseClicked(commands);return true;}
        if(OkRect.Contains(p)){Submit(commands);return true;}
        return false;
    }
    void Sort(WarehouseSortColumn column){
        if(SortColumn==column){if(column==WarehouseSortColumn.Name)NameAscending=!NameAscending;else AmountAscending=!AmountAscending;}
        else SortColumn=column;
        ResetScroll();
    }
    /// <summary>
    /// OK: with a selection, forwards it once through IUiWarehouseCommands. Without those commands (the bridge does not provide
    /// them yet) the dev-tools NPC page is opened instead — a transition, not original behaviour. Nothing is sent twice.
    /// </summary>
    void Submit(IUiCommands commands){
        if(view==null||selection.Count==0)return;
        if(FindCommands<IUiWarehouseCommands>(commands) is {} wc){wc.SubmitWarehouse(view.Revision,Choices);selection.Clear();}
        else commands.ShowDevTools("npc");
    }
    /// <summary>Exit / cancel: tells the game (when it can listen) and hides the window until the next list.</summary>
    protected override void CloseClicked(IUiCommands commands){
        if(view!=null)FindCommands<IUiWarehouseCommands>(commands)?.CancelWarehouse(view.Revision);
        selection.Clear();Close();
    }

    // ---------------------------------------------------------------- offline checks (synthetic XML and surfaces only)

    /// <summary>Synthetic WAREHOUSE_WINDOW with the element order and attribute values of the local uiextra86-c.xml.</summary>
    internal const string TestXml="""
<?xml version="1.0"?>
<ExtraWindowConfigulation>
<WAREHOUSE_WINDOW x = "0" y = "0">
	<SURF_IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_BASE" surfNumber = "3001" xPos = "6" yPos = "9"/>
	<SURF_IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_FRAME" surfNumber = "3002"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_EXIT_OFF" surfNumber = "3031" xPos = "225" yPos = "15"/>
	<IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_SCROLL_BAR" surfNumber = "3024" xPos = "217" yPos = "61"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_SCROLL_UP_OFF" surfNumber = "3025" xPos = "217" yPos = "47"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_SCROLL_DOWN_OFF" surfNumber = "3027" xPos = "217" yPos = "285"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_SCROLL_CONTROL_OFF" surfNumber = "3029"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_UP_OFF" surfNumber = "3003"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_DOWN_OFF" surfNumber = "3005"/>
	<IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_TOTAL" surfNumber = "3007" xPos = "19" yPos = "25"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_OK_OFF" surfNumber = "3014" xPos = "60" yPos = "326"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_CANCEL_OFF" surfNumber = "3017" xPos = "127" yPos = "326"/>
	<IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_NAME" surfNumber = "3008" xPos = "117" yPos = "35"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_SORT_UP_OFF" surfNumber = "3010" xPos = "150" yPos = "37"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_SORT_DOWN_OFF" surfNumber = "3012" xPos = "150" yPos = "37"/>
	<IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_AMOUNT" surfNumber = "3009" xPos = "162" yPos = "35"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_SORT_UP_OFF" surfNumber = "3010" xPos = "206" yPos = "37"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WAREHOUSE_SORT_DOWN_OFF" surfNumber = "3012" xPos = "206" yPos = "37"/>
	<IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_TAB_ALL" surfNumber = "3020" xPos = "17" yPos = "300"/>
	<IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_TAB_EQUIP" surfNumber = "3021" xPos = "17" yPos = "300"/>
	<IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_TAB_QUEST" surfNumber = "3022" xPos = "17" yPos = "300"/>
	<IMAGE surfName = "SURF_RENEWAL_WAREHOUSE_TAB_ETC" surfNumber = "3023" xPos = "17" yPos = "300"/>
	<IMAGE_BTN surfName = "SURF_MAIN_ICON_MAIL_OFF" surfNumber = "2303"/>
	<IMAGE_BTN surfName = "SURF_MAIN_ICON_MAIL_ON" surfNumber = "2304"/>
</WAREHOUSE_WINDOW>
</ExtraWindowConfigulation>
""";

    sealed class Commands:IUiCommands,IUiWarehouseCommands {
        public readonly List<string> Calls=[];
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public void SubmitWarehouse(int revision,IReadOnlyList<ShopChoice> choices)=>Calls.Add($"submit:{revision}:"+string.Join(",",choices.Select(c=>$"{c.Id}x{c.Count}")));
        public void CancelWarehouse(int revision)=>Calls.Add($"cancel:{revision}");
    }

    [WorldModuleTest("native-warehouse-window")]
    static void Checks(WorldModuleTestContext t){
        var surf=LegacyWindowTestKit.Surfaces(new(){
            [3001]=(220,320),[3002]=(250,360),[3031]=(14,14),[3024]=(10,220),[3025]=(10,12),[3027]=(10,12),[3029]=(10,10),[3003]=(8,8),[3005]=(8,8),
            [3007]=(60,14),[3014]=(50,18),[3017]=(50,18),[3008]=(30,12),[3009]=(30,12),[3010]=(9,9),[3012]=(9,9),
            [3020]=(180,20),[3021]=(180,20),[3022]=(180,20),[3023]=(180,20),[2303]=(16,16),[2304]=(16,16)});
        var layout=UiLayout.Parse(TestXml,"uiextra86-c.xml");
        var p=new WarehouseWindowPanel();p.Load(layout,surf.Get);
        t.Check(p.Id=="WAREHOUSE_WINDOW"&&p.LayoutName=="uiextra86"&&p.Layer==UiLayer.Window,"WAREHOUSE_WINDOW from uiextra86");
        t.Equal(new Rectangle(0,0,250,360),p.Bounds,"bounds = frame ∪ parts");
        t.Equal(new Rectangle(225,15,14,14),p.CloseRect,"exit 3031 at (225,15)");
        t.Check(p.ScrollUpRect==new Rectangle(217,47,10,12)&&p.ScrollDownRect==new Rectangle(217,285,10,12),"arrows 3025 (217,47) / 3027 (217,285)");
        t.Check(p.OkRect==new Rectangle(60,326,50,18)&&p.CancelRect==new Rectangle(127,326,50,18),"OK 3014 (60,326), cancel 3017 (127,326)");
        t.Check(p.NameSortRect==new Rectangle(150,37,9,9)&&p.AmountSortRect==new Rectangle(206,37,9,9),"sort buttons at (150,37) and (206,37)");
        t.Equal(new Rectangle(17,300,180,20),p.TabStripRect,"tabs at (17,300)");
        t.Check(p.PaintedParts.Select(x=>x.Surf).SequenceEqual(new int?[]{3001,3002,3031,3024,3025,3027,3007,3014,3017,3008,3010,3009,3010,3020}),"XML order; no knob/3003/3005/mail; UP images by default; tab 0",p.PaintedParts.Select(x=>x.Surf).ToArray());

        var basic=new UiStaticGameState(InWorld:true);
        p.Open();p.Update(basic);
        t.Check(!p.Visible&&!p.IsOpen,"no warehouse list → hidden even after Open");

        var rows=new ShopRow[]{new(11,5,30,"b 長劍",1,""),new(12,6,30,"a 箭",500,""),new(13,7,30,"c 藥水",20,"")};
        var view=new UiWarehouseView("withdraw",7,rows,30);
        IUiGameState S(UiWarehouseView? v)=>new UiHpMpGameState(new UiWarehouseGameState(basic,v),[]);
        p.Update(S(view));
        t.Check(p.Visible&&p.Rows.Select(r=>r.Id).SequenceEqual(new uint[]{11,12,13}),"a list opens the window; server order");
        t.Check(p.Select(12,500)&&p.Select(13,3)&&!p.Select(13,21)&&!p.Select(99,1)&&p.Selection.Count==2,"selection within Available only");
        t.Equal(60L,p.SelectedFee,"withdraw fee = per-row fee × selected rows (not × count)");
        t.Check(p.Select(13,0)&&p.Selection.Count==1&&p.Select(13,3),"count 0 clears a row");

        var cmd=new Commands();
        LegacyWindowTestKit.Click(p,new(152,39),cmd);
        t.Check(p.SortColumn==WarehouseSortColumn.Name&&p.NameAscending&&p.Rows.Select(r=>r.Id).SequenceEqual(new uint[]{12,11,13}),"name sort ascending (ordinal)");
        LegacyWindowTestKit.Click(p,new(152,39),cmd);
        t.Check(!p.NameAscending&&p.Rows.Select(r=>r.Id).SequenceEqual(new uint[]{13,11,12})&&p.PaintedParts.Count(x=>x.Surf==3012)==1&&p.PaintedParts.Count(x=>x.Surf==3010)==1,"second click → descending; name column shows DOWN 3012");
        LegacyWindowTestKit.Click(p,new(208,39),cmd);
        t.Check(p.SortColumn==WarehouseSortColumn.Amount&&p.Rows.Select(r=>r.Id).SequenceEqual(new uint[]{11,13,12}),"amount sort ascending");
        for(int k=0;k<5;k++)LegacyWindowTestKit.Click(p,new(220,290),cmd);
        t.Equal(2,p.ScrollOffset,"scroll clamps to rows − 1");

        LegacyWindowTestKit.Click(p,new(70,330),cmd);
        t.Check(cmd.Calls.SequenceEqual(new[]{"submit:7:12x500,13x3"})&&p.Selection.Count==0,"OK forwards the selection once, in server order");
        LegacyWindowTestKit.Click(p,new(70,330),cmd);
        t.Equal(1,cmd.Calls.Count,"OK with nothing selected sends nothing");
        var plain=new UiNullCommands();
        p.Select(11,1);LegacyWindowTestKit.Click(p,new(70,330),plain);
        t.Check(plain.Calls.SequenceEqual(new[]{"dev-tools:npc"})&&p.Selection.Count==1,"without warehouse commands → dev-tools NPC page (transition), selection kept");
        var wrapped=new UiMacroCommands(cmd,(_,_)=>{});
        LegacyWindowTestKit.Click(p,new(70,330),wrapped);
        t.Equal("submit:7:11x1",cmd.Calls[^1],"warehouse commands found under UiMacroCommands");

        // Same revision keeps state; rows that vanish leave the selection.
        p.Select(12,10);p.Select(13,5);
        p.Update(S(view with{Rows=[rows[0],rows[1] with{Available=5}]}));
        t.Check(p.Visible&&p.Selection.Count==0&&p.SortColumn==WarehouseSortColumn.Amount,"same revision: sort kept; vanished / over-count selections dropped");

        LegacyWindowTestKit.Click(p,new(140,330),cmd);
        t.Check(cmd.Calls[^1]=="cancel:7"&&!p.Visible,"cancel → CancelWarehouse(revision) and hidden");
        p.Update(S(view));
        t.Check(!p.Visible,"same list does not reopen after cancel");
        p.Update(S(new UiWarehouseView("deposit",8,rows,0)));
        t.Check(p.Visible&&p.SortColumn==WarehouseSortColumn.None&&p.ScrollOffset==0&&p.SelectedFee==0,"new revision reopens with reset state; deposit has no fee");
        LegacyWindowTestKit.Click(p,new(230,20),cmd);
        t.Check(cmd.Calls[^1]=="cancel:8"&&!p.Visible,"exit button = cancel");
        p.Update(S(new UiWarehouseView("withdraw",9,rows,30)));
        p.Update(S(null));
        t.Check(!p.Visible&&p.View==null,"list closed by the game → hidden");
        p.Update(S(new UiWarehouseView("withdraw",10,rows,30)));
        p.Update(new UiWarehouseGameState(new UiStaticGameState(InWorld:false),new UiWarehouseView("withdraw",10,rows,30)));
        t.Check(!p.Visible,"leaving the world hides it");

        using(var canvas=new UiCanvas()){
            var root=new UiRoot();root.Add(p);root.Update(S(new UiWarehouseView("withdraw",11,rows,30)));
            p.MoveTo(new(500,200));root.Compose(canvas,null);
            t.Equal(new Rectangle(500,200,250,360).Location,p.Bounds.Location,"moved");
            t.Equal(LegacyWindowTestKit.ColourOf(3014),canvas.Bitmap.GetPixel(500+65,200+330).ToArgb(),"OK painted at its moved position");
            t.Equal(LegacyWindowTestKit.ColourOf(3020),canvas.Bitmap.GetPixel(500+20,200+305).ToArgb(),"tab 0 painted");
        }

        t.Throws<UiLayoutException>(()=>new WarehouseWindowPanel().Load(UiLayout.Parse(TestXml.Replace("<IMAGE_BTN surfName = \"SURF_RENEWAL_WAREHOUSE_SORT_DOWN_OFF\" surfNumber = \"3012\" xPos = \"206\" yPos = \"37\"/>",""),"uiextra86-c.xml"),surf.Get),"missing amount sort DOWN → UiLayoutException");
        t.Throws<UiLayoutException>(()=>new WarehouseWindowPanel().Load(UiLayout.Parse(TestXml.Replace("SURF_RENEWAL_WAREHOUSE_OK_OFF","X"),"uiextra86-c.xml"),surf.Get),"missing OK → UiLayoutException");

        // UiWarehouseView.From over a fresh session: no interaction → null.
        t.Check(UiWarehouseView.From(new WorldSession())==null,"no warehouse interaction → no view");
    }
}
