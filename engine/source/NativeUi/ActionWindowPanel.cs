namespace P06.NativeUi;

// R8-D（T359／#823，FU8§4、gap-matrix 15.10 Q6-4）：動作窗。依本機 Tile.pak 的 ActionUI-c.xml（版面）＋Actions.xml（25 個動作）建原生視窗。
// 來源（本機 0-客戶端 Tile.idx，PakViewer 解出，不進 repo）：ActionUI-c.xml 解密 SHA-256 前 16 碼 ae467982a6880161、
//   Actions.xml 20c180afe54e6e10（與 FU8§0 相同）。TW16 沒有 ActionUI 類別（FU8§4.1）；3.81 用這兩份是 **推測**，版面要真機再確認。
//
// 已驗證（XML／資源）：
// - 根 ActionUI（Toggle="1"）；Window "Action_Window" BaseSurf 5141、FrameSurf 5142（本機 5141c／5142c.img 都是 243×195）、Moveable 20。
// - ActionIconGrid (21,45)，Row 4 × Column 6，UnitWidth／Height 32、UnitGap 1，Selection_Image surf 5333（34×34）。
// - Tab_Control (−2,20)：Total／Community／Action／Management 四顆 RadioButton（5144–5155，各 50×23）。
// - Exit_Button (228,11) 1962／1963；ScrollBar 在格子內 (202,8)，Up 2042／2043、Down 2040／2041、Move 2044／2045、底 5143。
// - Actions.xml 每項 action／category／control／icon（target、domain 只在 control="target" 時有）。
// - 圖示：25 個 icon 編號在本機 Sprite*.pak 都有 "<n>.tbt"（都約 1.9 KB，像 32×32 圖示）；同號的 .img 大小不一（12×12、67×54…，
//   5363 沒有 .img），所以圖示照背包圖示的查法讀 "<icon>.tbt"（ItemIconSource，與 BuffIconCatalog 同一組）。這個對應是 **推測**。
//
// 推測（標明，真機再確認）：
// - 視窗畫面位置：XML 沒給 X/Y，照 QUIT_WINDOW 已驗證的規則水平置中、y＝150（同 UserPlayStatsPanel）。
// - 外框疊在底圖上、都畫在視窗原點；ScrollBar 的 X/Y 相對於格子（換算後 (223,53)–(235,189)，剛好落在 243×195 內、
//   Up 14 高接底圖 (1,14) 91 高再接 Down (0,105)，幾何吻合）。
// - 分頁：Total＝全部動作（Actions.xml 順序），其他三頁＝該 category；開窗預設 Total；按下（513）就換頁（同 BehaviourUI 的 TabButton），
//   換頁時捲動與選取歸零。選中的分頁畫 PushSurf，滑鼠停留畫 HighlightSurf，其他畫 BaseSurf。
// - 格子：單擊選取（畫 5333 框，左上對齊格子 −1,−1），**雙擊（515）執行**（同 BehaviourUI 的 GridEvent→sub_7C7CB0）。
// - 捲動以列為單位（6 格一列），最大值＝列數 − 4；XML 的 MaxValue＝8、ScrollStart／ScrollHeight 只留資料。拇指在 Move_Button.Y 到
//   Down_Button.Y − 拇指高之間依比例移動；拖拇指、點軌道不做。滾輪在視窗內上下捲一列。
// - Esc：不由本窗攔（ActionUI 的 KeyboardEvent 內容沒查）；全域 ESC（R7-H，UiRoot.CloseAll）照樣會關它。
// 只做資料（標「真機再確認」）：常駐開窗鈕 ActionUI_Button (774,470) 5156／5157 與提示 3118（是否常駐待驗證，不畫、不收點擊）；
//   格子 Drag="1"（把動作拖到巨集列，待做）；toggle 類動作的開關狀態（原版切換的 byte 旗標沒接，不畫勾選）。
//
// 執行（原版 sub_ADC200 依名稱找處理函式，FU8§4.3）。引擎依序：
// 1. 指令物件實作 IUiActionCommands 且 RunAction 回 true（產品橋接 NativeUiCommands 尚未實作：不在本張可改範圍，交下一棒）；
// 2. 面板自己能做的：Action_OpenBookmarkWin → 原生書籤窗（UiRoot.Toggle，同選單鈕）；
// 3. Action_SocialAction1–4 → 開發工具「稱號／表情」分頁（過渡；表情目前只能從那裡送，TitleAndEmote.cs）；
// 4. 其他 → 不送任何封包，記一次 log（Unhandled）。control="target" 的「指定對象」模式不做。
// 開關：Ctrl+D（FU4§1.1 TW16 的 BehaviourUI 鍵；3.81 是否同鍵待驗證）由 MenuButtonsPanel 經 UiRoot.Toggle 開關本窗。
// --classic-ui 沒有原生 UI 根：本窗不存在，WinForms 開發工具的「稱號／表情」分頁照舊是唯一入口。

public enum ActionCategory { Action, Community, Management }
public enum ActionControl { Action, Toggle, Target }
public enum ActionDomain { None, Global, View, Map }

/// <summary>Actions.xml 的一項。Targets＝target 四旗標（地圖物件、聊天窗、隊伍窗、寵物隊伍窗），沒寫＝空。</summary>
public sealed record ActionEntry(string Name,ActionCategory Category,ActionControl Control,int Icon,ActionDomain Domain,IReadOnlyList<bool> Targets,string Path);

/// <summary>Actions.xml（ActionList/Action）。未知的 category／control／domain、缺 action 或 icon、名稱重複 → UiLayoutException。</summary>
public static class ActionCatalog {
    public const string FileName="Actions",RootNode="ActionList",ItemNode="Action";
    public static IReadOnlyList<ActionEntry> Parse(UiLayout layout){
        var root=layout.Root;
        if(root.Name!=RootNode)throw new UiLayoutException(layout.FileName,root.Path,$"根元素不是 {RootNode}");
        var list=new List<ActionEntry>();
        foreach(var n in root.ChildrenNamed(ItemNode)){
            string name=n.RequiredAttr("action").Trim();
            if(name.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"action 是空的");
            if(list.Any(a=>a.Name==name))throw new UiLayoutException(layout.FileName,n.Path,$"action {name} 重複");
            var category=n.RequiredAttr("category").Trim() switch{
                "action"=>ActionCategory.Action,"community"=>ActionCategory.Community,"management"=>ActionCategory.Management,
                var x=>throw new UiLayoutException(layout.FileName,n.Path,$"不明的 category {x}")};
            var control=n.RequiredAttr("control").Trim() switch{
                "action"=>ActionControl.Action,"toggle"=>ActionControl.Toggle,"target"=>ActionControl.Target,
                var x=>throw new UiLayoutException(layout.FileName,n.Path,$"不明的 control {x}")};
            var domain=(n.Attr("domain")?.Trim()) switch{
                null or ""=>ActionDomain.None,"global"=>ActionDomain.Global,"view"=>ActionDomain.View,"map"=>ActionDomain.Map,
                var x=>throw new UiLayoutException(layout.FileName,n.Path,$"不明的 domain {x}")};
            bool[] targets=[];
            if(n.Attr("target") is {} raw){
                var parts=raw.Split(',');
                if(parts.Length!=4||parts.Any(p=>p.Trim() is not ("0" or "1")))throw new UiLayoutException(layout.FileName,n.Path,$"target=\"{raw}\" 不是四個 0／1");
                targets=parts.Select(p=>p.Trim()=="1").ToArray();
            }
            int icon=n.Int("icon");
            if(icon<=0)throw new UiLayoutException(layout.FileName,n.Path,$"icon {icon} 不合法");
            list.Add(new(name,category,control,icon,domain,targets,n.Path));
        }
        if(list.Count==0)throw new UiLayoutException(layout.FileName,root.Path,"沒有任何 Action");
        return list;
    }
}

/// <summary>
/// R8-D：動作的對外執行（產品由 ClientForm 的橋接指令實作；尚未接）。true＝已處理；false＝做不到，面板改走自己的退路。
/// </summary>
public interface IUiActionCommands {
    bool RunAction(ActionEntry action);
}

/// <summary>雙擊一格之後實際走了哪條路（檢查與 log 用）。</summary>
public enum ActionRunResult { Bridge, Window, DevTools, Unhandled }

public sealed class ActionWindowPanel(UiResources? resources=null,UiRoot? root=null,ItemIconSource? icons=null):IUiMovablePanel,IUiClosablePanel {
    [UiPanel(191)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,r=>new ActionWindowPanel(r.Resources,r,ItemIconSource.FromRoot(r)));   // R8-D (#823): ActionUI-c.xml + Actions.xml (local Tile.pak), loaded inside Load
    public const string PanelId="Action_Window";
    public const string LayoutName="ActionUI",RootNode="ActionUI",WindowNode="Window",WindowName="Action_Window";
    /// <summary>UiRoot.Build 用的宿主版面（UiRoot.Build 全有或全無）；真正的版面在 Load 內自己讀，讀不到只停用本窗。</summary>
    public const string HostLayout="uistatus86";
    /// <summary>推測：沿用 QUIT_WINDOW 已驗證的畫面位置（400 − 寬/2, 150）。</summary>
    public const int ScreenY=150;
    /// <summary>開關鍵（FU4§1.1 TW16 WM_CHAR 4＝Ctrl+D → BehaviourUI；3.81 待驗證）。MenuButtonsPanel 負責分派。</summary>
    public const Keys Hotkey=Keys.Control|Keys.D;
    public const string EmoteDevToolsPage="稱號／表情";
    public const string OpenBookmarkAction="Action_OpenBookmarkWin";
    public static readonly IReadOnlySet<string> SocialActions=new HashSet<string>(StringComparer.Ordinal){"Action_SocialAction1","Action_SocialAction2","Action_SocialAction3","Action_SocialAction4"};

    /// <summary>XML 的一顆按鈕（相對視窗原點）。Base 缺圖＝不畫、不能點。</summary>
    public sealed record Part(string Name,Point Offset,UiSurface? Base,UiSurface? Push,UiSurface? Highlight,string? MouseEvent) {
        public Rectangle Local=>Base==null?Rectangle.Empty:new(Offset,new Size(Base.Width,Base.Height));
    }
    /// <summary>一個分頁鈕。Category null＝Total（全部）。</summary>
    public sealed record Tab(Part Button,ActionCategory? Category);
    /// <summary>常駐開窗鈕 ActionUI_Button（只做資料：是否常駐待驗證）。</summary>
    public sealed record OpenButtonSpec(Point Location,int? BaseSurf,int? HighlightSurf,int? TooltipString);
    /// <summary>ScrollBar（Offset 相對視窗原點）。MaxValue／ScrollStart／ScrollHeight 只留資料。</summary>
    public sealed record ScrollSpec(Point Offset,Size Size,int MaxValue,int ScrollStart,int ScrollHeight,Point BackgroundAt,UiSurface? Background,Part? Up,Part? Down,Part? Thumb);

    public static readonly IReadOnlyDictionary<string,ActionCategory?> TabCategories=new Dictionary<string,ActionCategory?>(StringComparer.Ordinal){
        ["Total_Button"]=null,["Community_Button"]=ActionCategory.Community,["Action_Button"]=ActionCategory.Action,["Management_Button"]=ActionCategory.Management,
    };

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>不能開窗的原因（沒有資源、XML 讀不到或不合、底圖與外框都缺）；null＝可用。</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public Point Origin{get;private set;}
    public Size Size{get;private set;}
    public Rectangle Bounds=>Available?new(Origin,Size):Rectangle.Empty;
    public bool Visible=>open&&Available;
    public int MoveableHeight{get;private set;}
    public UiSurface? BaseSurface{get;private set;}
    public UiSurface? FrameSurface{get;private set;}
    public IReadOnlyList<ActionEntry> Actions{get;private set;}=[];
    public IReadOnlyList<Tab> Tabs{get;private set;}=[];
    public Part? Exit{get;private set;}
    public ScrollSpec? Scroll{get;private set;}
    public LegacyCellGrid Grid{get;private set;}=LegacyCellGrid.Empty;
    public UiSurface? SelectionSurface{get;private set;}
    public bool GridDrag{get;private set;}
    public OpenButtonSpec? OpenButton{get;private set;}
    /// <summary>XML 有寫但找不到的圖（不畫）："surf 5141 (ActionUI/Window[0]/@BaseSurf)"。</summary>
    public IReadOnlyList<string> Missing{get;private set;}=[];
    public int ActiveTab{get;private set;}
    public int ScrollRow{get;private set;}
    public ActionEntry? Selected{get;private set;}
    public IReadOnlyCollection<string> Unhandled=>unhandled;
    /// <summary>最近一次雙擊的結果（檢查用）。</summary>
    public (ActionEntry Action,ActionRunResult Result)? LastRun{get;private set;}
    bool open,placed;Part? pressed,hover;bool pressedInside;
    readonly HashSet<string> unhandled=new(StringComparer.Ordinal);
    ItemIconSource Icons=>icons??ItemIconSource.None;

    /// <summary>目前分頁的動作（Total＝全部，依 Actions.xml 順序）。</summary>
    public IReadOnlyList<ActionEntry> TabActions=>Tabs.Count==0?Actions:Tabs[ActiveTab].Category is ActionCategory c?Actions.Where(a=>a.Category==c).ToArray():Actions;
    public int MaxScroll=>Grid.Columns==0?0:Math.Max(0,Grid.RowsFor(TabActions.Count)-Grid.Rows);

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout ui,actions;
        try{ui=resources.Xml.Load(LayoutName);actions=resources.Xml.Load(ActionCatalog.FileName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"action-ui 無法載入 {LayoutName}／{ActionCatalog.FileName}，動作窗停用：{ex.Message}");return;}
        try{Load(ui,actions,surfs.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"action-ui {ui.FileName}／{actions.FileName} 版面不符，動作窗停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"action-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(Unavailable!=null)UiResourceLog.Write($"action-ui {Unavailable}");
    }

    void Clear(){
        open=false;placed=false;pressed=null;hover=null;pressedInside=false;Unavailable="not loaded";Origin=Point.Empty;Size=Size.Empty;MoveableHeight=0;
        BaseSurface=null;FrameSurface=null;Actions=[];Tabs=[];Exit=null;Scroll=null;Grid=LegacyCellGrid.Empty;SelectionSurface=null;GridDrag=false;
        OpenButton=null;Missing=[];ActiveTab=0;ScrollRow=0;Selected=null;LastRun=null;
    }

    /// <summary>離線測試用：surf 由呼叫端提供（合成圖）。版面不合 → UiLayoutException。</summary>
    internal void Load(UiLayout ui,UiLayout actions,Func<int,UiSurface?> surf){
        Clear();
        var root=ui.Root;
        if(root.Name!=RootNode)throw new UiLayoutException(ui.FileName,root.Path,$"根元素不是 {RootNode}");
        var missing=new List<string>();
        UiSurface? Get(UiNode n,string attr){
            if(OptionPanel.ImageAttr(n,attr) is not {} r)return null;
            var s=surf(r.Id);if(s==null)missing.Add($"surf {r.Id} ({n.Path}/@{attr})");
            return s;
        }
        Part PartOf(UiNode n,Point parent)=>new(n.Attr("Name")??n.Name,new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0)),
            Get(n,"BaseSurf"),Get(n,"PushSurf"),Get(n,"HighlightSurf")??Get(n,"HighLightSurf"),n.Attr("MouseEvent"));

        if(root.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")=="ActionUI_Button") is {} ob)
            OpenButton=new(new(ob.Int("X"),ob.Int("Y")),OptionPanel.ImageAttr(ob,"BaseSurf")?.Id,OptionPanel.ImageAttr(ob,"HighlightSurf")?.Id,
                ob.ChildrenNamed("Tooltip").FirstOrDefault()?.TryInt("String"));

        var w=root.ChildrenNamed(WindowNode).SingleOrDefault(n=>n.Attr("Name")==WindowName)
            ??throw new UiLayoutException(ui.FileName,root.Path,$"沒有 Window {WindowName}");
        BaseSurface=Get(w,"BaseSurf");FrameSurface=Get(w,"FrameSurf");MoveableHeight=w.TryInt("Moveable")??0;

        var g=w.Child("ActionIconGrid");
        int gx=g.Int("X"),gy=g.Int("Y"),rows=g.Int("Row"),cols=g.Int("Column"),gap=g.Int("UnitGap"),uw=g.Int("UnitWidth"),uh=g.Int("UnitHeight");
        if(rows<=0||cols<=0||uw<=0||uh<=0||gap<0)throw new UiLayoutException(ui.FileName,g.Path,"格子的列、欄、格寬高或間距不合法");
        Grid=new(new(gx,gy),new(uw,uh),new(uw+gap,uh+gap),cols,rows);
        GridDrag=g.TryInt("Drag")==1;
        if(g.ChildrenNamed("Image").FirstOrDefault(n=>n.Attr("Name")=="Selection_Image") is {} sel)SelectionSurface=Get(sel,"Surf");
        if(g.ChildrenNamed("ScrollBar").FirstOrDefault() is {} sb){
            var at=new Point(gx+sb.Int("X"),gy+sb.Int("Y"));
            Part? B(string name)=>sb.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")==name) is {} b?PartOf(b,at):null;
            var bg=sb.ChildrenNamed("Image").FirstOrDefault(n=>n.Attr("Name")=="Background_Image");
            Scroll=new(at,new(sb.TryInt("Width")??0,sb.TryInt("Height")??0),sb.TryInt("MaxValue")??0,sb.TryInt("ScrollStart")??0,sb.TryInt("ScrollHeight")??0,
                bg==null?at:new(at.X+(bg.TryInt("X")??0),at.Y+(bg.TryInt("Y")??0)),bg==null?null:Get(bg,"Surf"),B("Up_Button"),B("Down_Button"),B("Move_Button"));
        }
        if(w.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")=="Exit_Button") is {} ex)Exit=PartOf(ex,Point.Empty);
        if(w.ChildrenNamed("Control").FirstOrDefault(c=>c.Attr("Name")=="Tab_Control") is {} tc){
            var at=new Point(tc.TryInt("X")??0,tc.TryInt("Y")??0);
            Tabs=tc.ChildrenNamed("RadioButton").Where(r=>TabCategories.ContainsKey(r.Attr("Name")??"")).Select(r=>new Tab(PartOf(r,at),TabCategories[r.Attr("Name")!])).ToArray();
        }

        Actions=ActionCatalog.Parse(actions);
        Missing=missing;
        var size=BaseSurface??FrameSurface;
        if(size==null){Unavailable=$"{ui.FileName}: 底圖與外框都缺，無法定出視窗範圍";return;}
        Size=new(size.Width,size.Height);
        Origin=new(UiRoot.LogicalWidth/2-Size.Width/2,ScreenY);
        Unavailable=null;
    }

    // ---- open / close
    public bool Open(){
        if(!Available)return false;
        if(!open){open=true;ActiveTab=0;ScrollRow=0;Selected=null;pressed=null;hover=null;}
        return true;
    }
    public void Toggle(){if(open)Close();else Open();}
    public void Close(){open=false;pressed=null;hover=null;pressedInside=false;}
    void IUiClosablePanel.Close()=>Close();

    /// <summary>可開的動作窗（已註冊、版面載入成功、沒停用）；否則 null。</summary>
    public static ActionWindowPanel? Window(UiRoot? root)=>
        root?.Find(PanelId) is ActionWindowPanel p&&p.Available&&!root.Faulted.Contains(p)?p:null;

    public bool IsDragHandle(Point location)=>Visible&&MoveableHeight>0&&Bounds.Contains(location)&&location.Y<Origin.Y+MoveableHeight&&PartAt(location)==null;
    public void MoveTo(Point location){Origin=location;placed=true;}
    public bool Moved=>placed;

    public void Update(IUiGameState state){
        if(!state.InWorld&&open)Close();
    }

    // ---- geometry (canvas coordinates)
    Rectangle At(Part p)=>p.Local.IsEmpty?Rectangle.Empty:new(Origin.X+p.Local.X,Origin.Y+p.Local.Y,p.Local.Width,p.Local.Height);
    IEnumerable<Part> Parts{get{
        foreach(var t in Tabs)yield return t.Button;
        if(Exit!=null)yield return Exit;
        if(Scroll?.Up!=null)yield return Scroll.Up;
        if(Scroll?.Down!=null)yield return Scroll.Down;
    }}
    public Part? PartAt(Point p)=>Parts.FirstOrDefault(x=>At(x).Contains(p));
    /// <summary>第 slot 格（0 起、本頁、列優先）在畫布上的矩形。</summary>
    public Rectangle CellRect(int slot){var r=Grid.CellRect(slot);return new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);}
    /// <summary>畫布上的點落在哪個動作（格子之間的間距、空格＝null）。</summary>
    public ActionEntry? ActionAt(Point p){
        if(Grid.SlotAt(new(p.X-Origin.X,p.Y-Origin.Y)) is not int slot)return null;
        int index=ScrollRow*Grid.Columns+slot;var list=TabActions;
        return index<list.Count?list[index]:null;
    }
    /// <summary>本頁第 slot 格的動作（沒有＝null）。</summary>
    public ActionEntry? ActionInSlot(int slot){int index=ScrollRow*Grid.Columns+slot;var list=TabActions;return slot>=0&&slot<Grid.PageSize&&index<list.Count?list[index]:null;}
    /// <summary>拇指位置（相對視窗原點）：Move_Button.Y 到 Down_Button.Y − 拇指高之間依捲動比例（推測）。</summary>
    public Point? ThumbOffset{get{
        if(Scroll?.Thumb is not {Base:{} tb} thumb)return null;
        int top=thumb.Offset.Y,bottom=Scroll.Down is {} d?d.Offset.Y-tb.Height:top;
        int max=MaxScroll;
        return new(thumb.Offset.X,max==0||bottom<=top?top:top+(bottom-top)*ScrollRow/max);
    }}

    public void SelectTab(int index){
        if(index<0||index>=Tabs.Count||index==ActiveTab)return;
        ActiveTab=index;ScrollRow=0;Selected=null;
    }
    public void ScrollBy(int rows)=>ScrollRow=Math.Clamp(ScrollRow+rows,0,MaxScroll);

    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        canvas.DrawSurface(BaseSurface,Origin.X,Origin.Y);
        canvas.DrawSurface(FrameSurface,Origin.X,Origin.Y);
        for(int i=0;i<Tabs.Count;i++){
            var b=Tabs[i].Button;
            var s=i==ActiveTab?b.Push??b.Base:hover==b?b.Highlight??b.Base:b.Base;
            canvas.DrawSurface(s,Origin.X+b.Offset.X,Origin.Y+b.Offset.Y);
        }
        if(Scroll is {} sc){
            canvas.DrawSurface(sc.Background,Origin.X+sc.BackgroundAt.X,Origin.Y+sc.BackgroundAt.Y);
            foreach(var b in new[]{sc.Up,sc.Down})if(b!=null)canvas.DrawSurface(pressed==b&&pressedInside?b.Push??b.Base:b.Base,Origin.X+b.Offset.X,Origin.Y+b.Offset.Y);
            if(ThumbOffset is Point th)canvas.DrawSurface(sc.Thumb!.Base,Origin.X+th.X,Origin.Y+th.Y);
        }
        for(int slot=0;slot<Grid.PageSize;slot++){
            if(ActionInSlot(slot) is not {} a)continue;
            var cell=CellRect(slot);
            ItemIconSource.Draw(canvas,Icons.Get(a.Icon),cell.Location);
            if(a==Selected&&SelectionSurface!=null)canvas.DrawSurface(SelectionSurface,cell.X-1,cell.Y-1);
        }
        if(Exit!=null)canvas.DrawSurface(pressed==Exit&&pressedInside?Exit.Push??Exit.Base:Exit.Base,Origin.X+Exit.Offset.X,Origin.Y+Exit.Offset.Y);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        bool inside=Bounds.Contains(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:
                hover=PartAt(e.Location);
                if(pressed!=null)pressedInside=At(pressed).Contains(e.Location);
                return inside||pressed!=null;
            case UiMouseKind.Wheel:
                if(!inside)return false;
                if(e.Delta!=0)ScrollBy(e.Delta>0?-1:1);
                return true;
            case UiMouseKind.Down:{
                if(!inside)return false;
                if(e.Button!=MouseButtons.Left)return true;
                var part=PartAt(e.Location);
                for(int i=0;i<Tabs.Count;i++)if(Tabs[i].Button==part){SelectTab(i);return true;}
                if(part!=null){
                    pressed=part;pressedInside=true;
                    if(part==Scroll?.Up)ScrollBy(-1);else if(part==Scroll?.Down)ScrollBy(1);
                    return true;
                }
                if(ActionAt(e.Location) is {} a){
                    if(e.Clicks>=2&&a==Selected)Execute(a,commands);
                    else Selected=a;
                }
                return true;
            }
            case UiMouseKind.Up:{
                var was=pressed;bool fire=was!=null&&At(was).Contains(e.Location);
                pressed=null;pressedInside=false;
                if(fire&&was==Exit)Close();
                return was!=null||inside;
            }
            default:return inside;
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;

    /// <summary>雙擊一格（見檔頭「執行」）。不送封包；送封包的動作都要經過 IUiActionCommands。</summary>
    public ActionRunResult Execute(ActionEntry action,IUiCommands commands){
        var result=Run(action,commands);
        LastRun=(action,result);
        if(result==ActionRunResult.Unhandled&&unhandled.Add(action.Name))UiResourceLog.Write($"action-ui {action.Name}（{action.Control}）尚未接上，未執行");
        return result;
    }

    ActionRunResult Run(ActionEntry action,IUiCommands commands){
        if(LegacyWindowPanel.FindCommands<IUiActionCommands>(commands)?.RunAction(action)==true)return ActionRunResult.Bridge;
        if(action.Name==OpenBookmarkAction&&root?.Find(BookmarkWindowPanel.PanelId) is BookmarkWindowPanel bm&&!root.Faulted.Contains(bm)){
            var r=MenuButtonsPanel.ToggleWindow(root,bm,()=>{if(!bm.Visible)bm.Toggle();},bm.Toggle);
            if(r is UiToggleResult.Opened or UiToggleResult.Closed)return ActionRunResult.Window;
        }
        if(SocialActions.Contains(action.Name)){commands.ShowDevTools(EmoteDevToolsPage);return ActionRunResult.DevTools;}
        return ActionRunResult.Unhandled;
    }
}
