using System.Globalization;
namespace P06.NativeUi;

// T331 (#769, P-D of docs/reference/pak-resources.md §6, row 4:GameRankingUI): the native ranking window GameRanking_Window.
// GameRankingUI.xml is not in 0-客戶端 (PAK§4: all paks checked); it is read at run time from the reference client through
// UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference Tile.pak; SHA-256 logged as "ui-xml-ref").
// Its 39 surfs go through SurfCatalog.Get (local Sprite*.pak first, then the T322/#760 reference fallback). Nothing is copied.
//
// Loading. UiRoot.Build is all-or-nothing, so the registration uses the host layout "uistatus86" (always in the local
// Tile.pak, same as LocationListPanel / OptionPanel) and Load reads GameRankingUI.xml itself: no reference client / no XML /
// malformed XML / no window background → Unavailable, the window never opens and the rest of the native UI keeps working.
//
// Opening. The d287 server has no 3.81 ranking packet; it answers /rank (and the ranking NPCs) with S39 HTML pages
// (docs/protocol/player-query.md: y_h_2 / y_k_2 / y_k_3 / y_w_1 / y_c_1). PlayerQuery.cs raises
// PlayerQueryPackets.RankingWindowRequested for a self-addressed ranking page; this panel claims it only when it is usable,
// so the existing HYPER_TEXT query page (and, with --classic-ui, the WinForms page) stays the fallback. NPC-addressed pages
// keep going to the NPC dialog. The event can come from the network thread: the page is queued under a lock and shown in
// Update (UI thread).
//
// Tabs ← server pages (推測 where marked; the original binder was not decompiled):
//   y_h_2 → Level_Control  (Rank, Name; Level/Class columns are not in the page → blank)
//   y_c_1 → Pledge_Control (Rank, "盟名(數值)" → Name / Point (推測: the value's meaning is unverified); #10–19 war labels dropped)
//   y_w_1 → Money_Control  (Ranker0_List: Rank, Name; Ranker1_List rows 11–20 are not sent → blank)
//   y_k_2 / y_k_3 → Kill_Control (Rank, "名字,次數" → Name / Point)
//   Personal_Control, Weapon_Control: no server page → only the background is drawn.
// Rows with a blank name (the server pads with one space) are not drawn. Diff_Image (rank change arrow) is filled at run
// time in the original from data this server never sends → not drawn. Update_Label ("Last Update") likewise.
// Page_Control: one server page = one page, "1 / 1" (or "0 / 0" for a tab without data); First/Prev/Next/Last do nothing.
//
// Layout rules (推測): coordinates are relative to the parent element (missing X/Y = 0); List rows (List<n>_Control, no X/Y)
// stack vertically by their Height; within one container Images are drawn first, then the other elements in XML order
// (each Control lists its Back_Image last, yet its labels must stay visible; the window lists Page_Control after the tab
// Controls, so the page buttons stay above the tab backgrounds); window size = BaseSurf image; Moveable="36" = the top 36 px
// drag the window; a tab button shows its PushSurf while its tab is active and its HighlightSurf while hovered; labels are
// drawn white, aligned inside their Width by Align (left/center/right).

public enum GameRankingTab { Personal, Level, Pledge, Weapon, Money, Kill }

/// <summary>One surf reference "5558;1" → Id 5558, Flag 1 (flag meaning 待驗證).</summary>
public sealed record GameRankingSurf(string Attr,int Id,int Flag);

public enum GameRankingPieceKind { Image, Button, Label }

/// <summary>A drawable element. Offset is relative to the window origin; Surface null = missing surf (not drawn).</summary>
public sealed record GameRankingPiece(GameRankingPieceKind Kind,string Path,string? Name,Point Offset,Size DeclaredSize,
    GameRankingSurf? Base,UiSurface? BaseSurface,UiSurface? HighlightSurface,UiSurface? PushSurface,string? MouseEvent,string Align) {
    public Size ImageSize=>BaseSurface==null?Size.Empty:new(BaseSurface.Width,BaseSurface.Height);
    public Rectangle Local=>new(Offset,DeclaredSize.Width>0&&DeclaredSize.Height>0?DeclaredSize:ImageSize);
    public bool Clickable=>Kind==GameRankingPieceKind.Button&&Local.Width>0&&Local.Height>0;
}

/// <summary>One List row: its label cells by Name (window-relative rectangles) and the skipped Diff_Image.</summary>
public sealed record GameRankingRowSlot(IReadOnlyDictionary<string,GameRankingPiece> Cells);
public sealed record GameRankingList(string Name,Point Offset,IReadOnlyList<GameRankingRowSlot> Rows);

/// <summary>A tab Control: images (background), free labels (Personal_Control) and lists.</summary>
public sealed record GameRankingPage(GameRankingTab Tab,string Name,IReadOnlyList<GameRankingPiece> Images,IReadOnlyList<GameRankingPiece> Labels,IReadOnlyList<GameRankingList> Lists);

/// <summary>One decoded row: 1-based rank and text by label Name (e.g. "Name_Label", "Point_Label").</summary>
public sealed record GameRankingEntry(int Rank,IReadOnlyDictionary<string,string> Cells);

/// <summary>A server ranking page mapped onto one tab (see the file header).</summary>
public sealed record GameRankingData(GameRankingTab Tab,string Html,string Title,IReadOnlyList<GameRankingEntry> Entries) {
    public const string RankCell="Rank_Label",NameCell="Name_Label";

    /// <summary>S39 page id + arguments (as decoded by PlayerQueryPackets.DecodeRankHtml) → tab data; null for other pages.</summary>
    public static GameRankingData? FromServer(string html,IReadOnlyList<string> args){
        int expected=html switch{"y_h_2"=>11,"y_c_1"=>20,"y_k_2" or "y_k_3" or "y_w_1"=>10,_=>-1};
        if(expected<0||args.Count!=expected)return null;
        (GameRankingTab Tab,string Title,string? Point) map=html switch{
            "y_h_2"=>(GameRankingTab.Level,"英雄風雲榜・"+args[10].Trim(),null),
            "y_c_1"=>(GameRankingTab.Pledge,"血盟風雲榜","Point_Label"),
            "y_w_1"=>(GameRankingTab.Money,"財富風雲榜",null),
            "y_k_2"=>(GameRankingTab.Kill,"殺人榜","Point"),
            _=>(GameRankingTab.Kill,"往生榜","Point")};
        var (tab,title,point)=map;
        var entries=new List<GameRankingEntry>();
        for(int i=0;i<10;i++){
            string row=args[i].Trim();if(row.Length==0)continue;
            var cells=new Dictionary<string,string>(StringComparer.Ordinal){[RankCell]=(i+1).ToString(CultureInfo.InvariantCulture)};
            string name=row;string? value=null;
            if(html is "y_k_2" or "y_k_3"&&row.Split(',') is [var n,var c]){name=n.Trim();value=c.Trim();}
            else if(html=="y_c_1"&&row.EndsWith(')')&&row.LastIndexOf('(') is int open and >0){name=row[..open].Trim();value=row[(open+1)..^1].Trim();}
            cells[NameCell]=name;
            if(point!=null&&value!=null)cells[point]=value;
            entries.Add(new(i+1,cells));
        }
        return new(tab,html,title,entries);
    }
}

public sealed class GameRankingPanel(UiResources? resources=null):IUiMovablePanel,IUiClosablePanel {
    [UiPanel(177)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,root=>new GameRankingPanel(root.Resources));   // T331 (#769): GameRankingUI.xml from the reference client (P-A), loaded inside Load
    public const string PanelId="GameRanking_Window";
    public const string LayoutName="GameRankingUI",WindowNode="Window",WindowName="GameRanking_Window";
    public const string ExitEvent="Exit",TabEvent="TabClick",TabSuffix="_TabButton",ControlSuffix="_Control",PageControl="Page_Control",PageLabel="PageLabel";
    /// <summary>Layout handed to UiRoot.Build (always in the local Tile.pak); the real layout is LayoutName, read in Load.</summary>
    public const string HostLayout="uistatus86";
    public static readonly Color TextColor=Color.White;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>Why the window cannot open; null when usable.</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public Point Origin{get;private set;}
    public Size Size{get;private set;}
    public int MoveableHeight{get;private set;}
    public Rectangle Bounds=>Size.IsEmpty?Rectangle.Empty:new(Origin,Size);
    public bool Visible=>open;
    public GameRankingPiece? Background{get;private set;}
    /// <summary>Window-level images, buttons (tabs, Exit) and the Page_Control pieces, in paint order.</summary>
    public IReadOnlyList<GameRankingPiece> WindowPieces{get;private set;}=[];
    public IReadOnlyList<GameRankingPage> Pages{get;private set;}=[];
    public GameRankingTab? ActiveTab{get;private set;}
    public GameRankingPage? ActivePage=>ActiveTab is {} t?Pages.FirstOrDefault(p=>p.Tab==t):null;
    /// <summary>Surfs named in the XML but not found (not drawn): "surf 5567 (GameRankingUI/Window/Image[0])".</summary>
    public IReadOnlyList<string> Missing{get;private set;}=[];
    /// <summary>Elements deliberately not built (runtime-filled images/labels, unknown elements), by path.</summary>
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    /// <summary>Latest server data per tab (cleared when leaving the world).</summary>
    public IReadOnlyDictionary<GameRankingTab,GameRankingData> Data=>data;
    public IReadOnlyCollection<string> Unhandled=>unhandled;

    bool open;GameRankingPiece? pressed,hover;bool pressedInside;
    readonly Dictionary<GameRankingTab,GameRankingData> data=new();
    readonly HashSet<string> unhandled=new(StringComparer.Ordinal);
    readonly object gate=new();GameRankingData? pending;

    // ---- event wiring (PlayerQueryPackets.RankingWindowRequested) ----
    static readonly object currentGate=new();
    static WeakReference<GameRankingPanel>? current;
    static GameRankingPanel(){PlayerQueryPackets.RankingWindowRequested+=OnRankingRequested;}
    static void OnRankingRequested(object? sender,RankingWindowEventArgs e){
        if(e.Handled)return;
        GameRankingPanel? panel=null;
        lock(currentGate)current?.TryGetTarget(out panel);
        if(panel!=null&&panel.Offer(e.Html,e.Arguments))e.Handled=true;
    }
    /// <summary>Makes this panel the one that receives ranking pages (the latest loaded, usable panel wins).</summary>
    internal void Attach(){lock(currentGate)current=new(this);}
    internal static void Detach(){lock(currentGate)current=null;}
    internal static bool IsAttached(GameRankingPanel panel){lock(currentGate)return current!=null&&current.TryGetTarget(out var p)&&ReferenceEquals(p,panel);}

    /// <summary>Queues a server page for the next Update. false (not claimed) when unusable or the page is not a ranking page.</summary>
    public bool Offer(string html,IReadOnlyList<string> args){
        if(!Available||GameRankingData.FromServer(html,args) is not {} page)return false;
        if(Pages.All(p=>p.Tab!=page.Tab))return false;   // the layout has no tab for it: leave it to the query page
        lock(gate)pending=page;
        return true;
    }

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"ranking-ui 無法載入 {LayoutName}，排名窗停用（查詢頁照舊）：{ex.Message}");return;}
        // #1245：主客戶端沒有這份版面 → 版面來自參考客戶端，surf 編號也只在參考客戶端有意義（見 SurfSource）。
        bool fromReference=!MainClientHas(resources.Xml,LayoutName);
        var surf=SurfSource(fromReference,surfs.Get,surfs.Reference is {} r?ReferenceSurfs(surfs,r):null);
        try{Load(layout,surf);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"ranking-ui {layout.FileName} 版面不符，排名窗停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"ranking-ui 缺圖不畫：{string.Join("、",Missing)}");
        // #1245 診斷：一行記下視窗範圍與底圖實際來源（retest 用來確認不是 32×32 的本機同號圖）。
        UiResourceLog.Write($"ranking-ui 版面與 surf 取自{(fromReference?"參考客戶端":"主客戶端")}；視窗 {Origin.X},{Origin.Y} {Size.Width}x{Size.Height}，"
            +$"底圖 {Background?.BaseSurface?.Archive??"-"}/{Background?.BaseSurface?.FileName??"-"}");
        if(Available)Attach();else UiResourceLog.Write($"ranking-ui 停用：{Unavailable}");
    }

    /// <summary>主客戶端 Tile.pak 有沒有這份版面（"-c" 或原名；與 UiXmlSource.TryReadPlain 的順序相同）。</summary>
    static bool MainClientHas(UiXmlSource xml,string baseName){
        string name=baseName+".xml";
        return xml.Archive.Find(UiXmlSource.LocalizedName(name))!=null||xml.Archive.Find(name)!=null;
    }

    /// <summary>
    /// #1245（f1211 SM31）：參考客戶端的版面只用參考客戶端的 surf，不先查主客戶端。已驗證（PakViewer 唯讀比對）：0-客戶端的
    /// Sprite*.pak 對 GameRankingUI 用到的 39 個 surf 編號都有同號 .img，但全是 2,056 B 的 32×32 小圖（4645／4646 是 136 B），
    /// 參考客戶端 kor25 才是真正的視窗圖（例：底圖 5558c.img 305,536 B＝422×362）。先查本機時視窗被裁成 32×32，
    /// 開了卻幾乎看不到（#1211 m7-SM31-rank*-after.png (189,48) 的小圖）。參考客戶端沒有的 surf → null（不畫、不改用本機同號圖）。
    /// 版面在主客戶端時照舊用 SurfCatalog.Get。
    /// </summary>
    internal static Func<int,UiSurface?> SurfSource(bool fromReference,Func<int,UiSurface?> main,Func<int,UiSurface?>? reference)
        =>!fromReference?main:reference??(_=>null);

    /// <summary>參考客戶端的 surf（檔名照 SurfList.xml 的 file，沒有這列就用編號；同 RefClientSurfFallback.Serve），這次載入內快取。</summary>
    static Func<int,UiSurface?> ReferenceSurfs(SurfCatalog surfs,RefClientSurfFallback reference){
        var memo=new Dictionary<int,UiSurface?>();
        return id=>{
            if(memo.TryGetValue(id,out var hit))return hit;
            string file=surfs.Files.TryGetValue(id,out var f)?f:id.ToString(CultureInfo.InvariantCulture);
            UiSurface? s=null;
            if(reference.TryRead(file) is {} found){
                try{s=new UiSurface(id,found.Name,"ref:"+found.Archive,UiImage.Decode(found.Data,$"{reference.Root}\\{found.Archive}.pak:{found.Name}"));}
                catch(UiResourceException ex){UiResourceLog.Write($"ranking-ui surf {id} {found.Archive}/{found.Name} 不是可解的 .img：{ex.Message}");}
            }
            return memo[id]=s;
        };
    }

    void Clear(){
        open=false;pressed=hover=null;pressedInside=false;LayoutFile=null;Origin=Point.Empty;Size=Size.Empty;MoveableHeight=0;Background=null;
        WindowPieces=[];Pages=[];ActiveTab=null;Missing=[];Skipped=[];Unavailable="not loaded";data.Clear();lock(gate)pending=null;
    }

    /// <summary>Offline checks: surfs come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        Clear();
        var window=layout.Root.Child(WindowNode);
        if(window.Attr("Name")!=WindowName)throw new UiLayoutException(layout.FileName,window.Path,$"Window Name 不是 {WindowName}");
        var missing=new List<string>();var skipped=new List<string>();
        UiSurface? Resolve(GameRankingSurf? r,string path){
            if(r==null)return null;
            var s=surf(r.Id);
            if(s==null)missing.Add($"surf {r.Id} ({path})");
            return s;
        }
        var baseRef=SurfAttr(window,"BaseSurf");
        var baseSurface=Resolve(baseRef,window.Path+"/@BaseSurf");
        var pieces=new List<GameRankingPiece>();var pages=new List<GameRankingPage>();
        foreach(var c in Ordered(window)){
            if(c.Name=="Control"&&c.Attr("Name") is {} name&&name!=PageControl){
                string stem=name.EndsWith(ControlSuffix,StringComparison.Ordinal)?name[..^ControlSuffix.Length]:name;
                if(!Enum.TryParse<GameRankingTab>(stem,false,out var tab)||!Enum.IsDefined(tab)){skipped.Add(c.Path+"（不認得的分頁）");continue;}
                if(pages.Any(p=>p.Tab==tab))throw new UiLayoutException(layout.FileName,c.Path,$"分頁 {name} 重複");
                pages.Add(Page(tab,name,c,Rel(c,Point.Empty),skipped,Resolve));
            }else if(c.Name=="Control")foreach(var k in Ordered(c))Element(k,Rel(c,Point.Empty),pieces,skipped,Resolve);
            else Element(c,Point.Empty,pieces,skipped,Resolve);
        }
        if(pages.Count==0)throw new UiLayoutException(layout.FileName,window.Path,"沒有任何分頁 Control");
        LayoutFile=layout.FileName;Origin=new(window.Int("X"),window.Int("Y"));MoveableHeight=window.TryInt("Moveable")??0;
        Background=baseRef==null?null:new(GameRankingPieceKind.Image,window.Path,window.Attr("Name"),Point.Empty,Size.Empty,baseRef,baseSurface,null,null,null,"left");
        Size=baseSurface==null?Size.Empty:new(baseSurface.Width,baseSurface.Height);
        WindowPieces=pieces;Pages=pages;Missing=missing;Skipped=skipped;
        // #1245：視窗範圍＝底圖大小，繪製會裁在範圍內。底圖小到連視窗自己的鈕／標題都落在外面，表示底圖不是這份版面的圖
        // （例：本機同號的 32×32 小圖）；這時停用，讓 /rank 回到查詢頁，不開一個看不見的窗。
        Unavailable=Size.IsEmpty?$"{layout.FileName}: 視窗底圖 BaseSurf 缺，無法定出視窗範圍（不用替代圖）"
            :pieces.FirstOrDefault(p=>p.Offset.X<0||p.Offset.Y<0||p.Offset.X>=Size.Width||p.Offset.Y>=Size.Height) is {} outside
            ?$"{layout.FileName}: 視窗底圖 surf {baseRef!.Id} 只有 {Size.Width}x{Size.Height}，{outside.Name??outside.Path} ({outside.Offset.X},{outside.Offset.Y}) 落在底圖外（底圖與版面不是同一個客戶端？）"
            :null;
    }

    /// <summary>Images first, then the rest, each in XML order (see the file header).</summary>
    static IEnumerable<UiNode> Ordered(UiNode parent){var all=parent.Children;return all.Where(c=>c.Name=="Image").Concat(all.Where(c=>c.Name!="Image"));}
    static Point Rel(UiNode n,Point parent)=>new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0));

    /// <summary>"5558;1" → (5558, 1); "4645" → (4645, 0). Attribute absent → null.</summary>
    public static GameRankingSurf? SurfAttr(UiNode n,string attr){
        var raw=n.Attr(attr);if(raw==null)return null;
        var parts=raw.Split(';');
        static bool P(string s,out int v)=>int.TryParse(s.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out v);
        if(parts.Length>2||!P(parts[0],out int id)||id<=0)throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是 surf 編號");
        int flag=0;if(parts.Length==2&&!P(parts[1],out flag))throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 的旗標不是整數");
        return new(attr,id,flag);
    }

    static GameRankingPiece Piece(GameRankingPieceKind kind,UiNode n,Point at,GameRankingSurf? r=null,UiSurface? s=null,UiSurface? hi=null,UiSurface? push=null)
        =>new(kind,n.Path,n.Attr("Name"),at,new(n.TryInt("Width")??0,n.TryInt("Height")??0),r,s,hi,push,n.Attr("MouseEvent"),(n.Attr("Align")??"left").Trim().ToLowerInvariant());

    static void Element(UiNode c,Point origin,List<GameRankingPiece> into,List<string> skipped,Func<GameRankingSurf?,string,UiSurface?> resolve){
        var at=Rel(c,origin);
        switch(c.Name){
            case "Image":{
                var r=SurfAttr(c,"Surf");
                if(r==null){skipped.Add(c.Path+"（沒有 Surf）");return;}
                into.Add(Piece(GameRankingPieceKind.Image,c,at,r,resolve(r,c.Path)));return;
            }
            case "Button":{
                var b=SurfAttr(c,"BaseSurf");var h=SurfAttr(c,"HighlightSurf");var p=SurfAttr(c,"PushSurf");
                into.Add(Piece(GameRankingPieceKind.Button,c,at,b,resolve(b,c.Path+"/@BaseSurf"),resolve(h,c.Path+"/@HighlightSurf"),resolve(p,c.Path+"/@PushSurf")));return;
            }
            case "Label" when c.Attr("Name")==PageLabel:into.Add(Piece(GameRankingPieceKind.Label,c,at));return;
            // Update_Label and other free labels carry design-time captions the original overwrites: not drawn.
            default:skipped.Add(c.Path);return;
        }
    }

    static GameRankingPage Page(GameRankingTab tab,string name,UiNode control,Point origin,List<string> skipped,Func<GameRankingSurf?,string,UiSurface?> resolve){
        var images=new List<GameRankingPiece>();var labels=new List<GameRankingPiece>();var lists=new List<GameRankingList>();
        foreach(var c in Ordered(control)){
            switch(c.Name){
                case "Image":Element(c,origin,images,skipped,resolve);break;
                // Personal_Control labels (own ranking values) have no data source here: kept for the layout, not drawn.
                case "Label":labels.Add(Piece(GameRankingPieceKind.Label,c,Rel(c,origin)));break;
                case "List":{
                    var at=Rel(c,origin);var rows=new List<GameRankingRowSlot>();int y=0;
                    foreach(var row in c.Children){
                        if(row.Name!="Control"){skipped.Add(row.Path);continue;}
                        var rowAt=new Point(at.X+(row.TryInt("X")??0),at.Y+y+(row.TryInt("Y")??0));
                        var cells=new Dictionary<string,GameRankingPiece>(StringComparer.Ordinal);
                        foreach(var k in row.Children){
                            if(k.Name=="Label"&&k.Attr("Name") is {} cell&&!cells.ContainsKey(cell))cells[cell]=Piece(GameRankingPieceKind.Label,k,Rel(k,rowAt));
                            else skipped.Add(k.Path+(k.Name=="Image"?"（名次變動圖，伺服器沒有資料）":""));
                        }
                        rows.Add(new(cells));
                        int h=row.TryInt("Height")??0;
                        if(h<=0)throw new UiLayoutException(control.Layout.FileName,row.Path,"清單列沒有 Height");
                        y+=h;
                    }
                    lists.Add(new(c.Attr("Name")??c.Path,at,rows));break;
                }
                default:skipped.Add(c.Path);break;
            }
        }
        return new(tab,name,images,labels,lists);
    }

    public void Update(IUiGameState state){
        if(!state.InWorld){if(open)Close();data.Clear();lock(gate)pending=null;return;}
        GameRankingData? page;
        lock(gate){page=pending;pending=null;}
        if(page!=null&&Available){
            data[page.Tab]=page;Open(page.Tab);
            UiResourceLog.Write($"ranking-ui 開窗 {page.Html} → {ActiveTab} 分頁，{page.Entries.Count} 列；範圍 {Bounds.X},{Bounds.Y} {Bounds.Width}x{Bounds.Height}");   // #1245 診斷
        }
    }

    /// <summary>Opens on a tab (default: the tab of the latest data, else Level). false when unavailable.</summary>
    public bool Open(GameRankingTab? tab=null){
        if(!Available)return false;
        var want=tab??data.Keys.Cast<GameRankingTab?>().LastOrDefault()??GameRankingTab.Level;
        if(Pages.All(p=>p.Tab!=want))want=Pages[0].Tab;
        open=true;pressed=null;pressedInside=false;ActiveTab=want;
        return true;
    }
    public void Close(){open=false;pressed=hover=null;pressedInside=false;}
    public bool Toggle(){if(open){Close();return true;}return Open();}

    /// <summary>Tab a button selects ("Level_TabButton" → Level), or null.</summary>
    public GameRankingTab? TabOf(GameRankingPiece p){
        if(p.Kind!=GameRankingPieceKind.Button||p.Name is not {} n||!n.EndsWith(TabSuffix,StringComparison.Ordinal))return null;
        return Enum.TryParse<GameRankingTab>(n[..^TabSuffix.Length],false,out var t)&&Pages.Any(pg=>pg.Tab==t)?t:null;
    }
    public bool ShowTab(GameRankingTab tab){
        if(!open||Pages.All(p=>p.Tab!=tab))return false;
        ActiveTab=tab;return true;
    }

    /// <summary>"1 / 1" when the active tab has server rows, else "0 / 0" (one server page = one page).</summary>
    public string PageText=>ActiveTab is {} t&&data.TryGetValue(t,out var d)&&d.Entries.Count>0?"1 / 1":"0 / 0";

    /// <summary>Text per cell of the active tab: (window-relative label piece, text). Rows beyond the data are blank.</summary>
    public IEnumerable<(GameRankingPiece Cell,string Text)> ActiveCells(){
        if(ActivePage is not {} page||!data.TryGetValue(page.Tab,out var d))yield break;
        int index=0;
        foreach(var list in page.Lists)foreach(var row in list.Rows){
            if(index>=d.Entries.Count)yield break;
            var entry=d.Entries[index++];
            foreach(var (cell,text) in entry.Cells)if(row.Cells.TryGetValue(cell,out var piece)&&text.Length>0)yield return (piece,text);
        }
    }

    public Rectangle At(GameRankingPiece p)=>new(Origin.X+p.Local.X,Origin.Y+p.Local.Y,p.Local.Width,p.Local.Height);
    /// <summary>Topmost clickable piece at a logical point (later in paint order = on top).</summary>
    public GameRankingPiece? ButtonAt(Point location)=>open?WindowPieces.Where(p=>p.Clickable).LastOrDefault(p=>At(p).Contains(location)):null;

    public void Paint(UiCanvas canvas){
        if(!open)return;
        canvas.DrawSurface(Background?.BaseSurface,Origin.X,Origin.Y);
        bool pageDrawn=false;
        // Window order (images first): Title, Exit, tabs, then the active tab Control, then Page_Control.
        foreach(var p in WindowPieces){
            if(!pageDrawn&&p.Kind!=GameRankingPieceKind.Image&&IsPageControl(p)){PaintPage(canvas);pageDrawn=true;}
            PaintPiece(canvas,p);
        }
        if(!pageDrawn)PaintPage(canvas);
    }
    static bool IsPageControl(GameRankingPiece p)=>p.Path.Contains("/Control[",StringComparison.Ordinal);

    void PaintPage(UiCanvas canvas){
        if(ActivePage is not {} page)return;
        foreach(var img in page.Images)canvas.DrawSurface(img.BaseSurface,Origin.X+img.Offset.X,Origin.Y+img.Offset.Y);
        foreach(var (cell,text) in ActiveCells())DrawLabel(canvas,cell,text);
    }

    void PaintPiece(UiCanvas canvas,GameRankingPiece p){
        int x=Origin.X+p.Offset.X,y=Origin.Y+p.Offset.Y;
        switch(p.Kind){
            case GameRankingPieceKind.Image:canvas.DrawSurface(p.BaseSurface,x,y);break;
            case GameRankingPieceKind.Label:if(p.Name==PageLabel)DrawLabel(canvas,p,PageText);break;
            case GameRankingPieceKind.Button:canvas.DrawSurface(ButtonSurface(p),x,y);break;
        }
    }

    /// <summary>Push while held or while its tab is active; Highlight while hovered; a missing state image falls back to Base.</summary>
    public UiSurface? ButtonSurface(GameRankingPiece p){
        bool held=p==pressed&&pressedInside;
        bool active=ActiveTab!=null&&TabOf(p)==ActiveTab;
        if((held||active)&&p.PushSurface!=null)return p.PushSurface;
        if(p==hover&&p.HighlightSurface!=null)return p.HighlightSurface;
        return p.BaseSurface;
    }

    /// <summary>Left x of a text of this width inside the label (Align left/center/right; wider text starts at the label's left).</summary>
    public static int AlignX(GameRankingPiece label,int textWidth){
        int w=label.DeclaredSize.Width;
        if(w<=0||textWidth>=w)return label.Offset.X;
        return label.Align switch{"center"=>label.Offset.X+(w-textWidth)/2,"right"=>label.Offset.X+w-textWidth,_=>label.Offset.X};
    }
    void DrawLabel(UiCanvas canvas,GameRankingPiece label,string text){
        int width=UiText.Measure(canvas.Graphics,text).Width;
        canvas.DrawText(text,Origin.X+AlignX(label,width),Origin.Y+label.Offset.Y,TextColor);
    }

    public bool IsDragHandle(Point location)=>open&&Bounds.Contains(location)&&location.Y<Origin.Y+MoveableHeight&&ButtonAt(location)==null;
    public void MoveTo(Point location)=>Origin=location;

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Move)hover=ButtonAt(e.Location);
        if(e.Kind==UiMouseKind.Wheel||e.Button!=MouseButtons.Left&&e.Kind!=UiMouseKind.Move)return inside||pressed!=null;
        switch(e.Kind){
            case UiMouseKind.Down:
                pressed=ButtonAt(e.Location);pressedInside=pressed!=null;
                return inside;
            case UiMouseKind.Move:
                if(pressed!=null)pressedInside=At(pressed).Contains(e.Location);
                return inside||pressed!=null;
            case UiMouseKind.Up:{
                var was=pressed;pressed=null;pressedInside=false;
                if(was!=null&&At(was).Contains(e.Location))Fire(was);
                return inside||was!=null;
            }
            default:return inside;
        }
    }

    void Fire(GameRankingPiece p){
        if(p.MouseEvent==ExitEvent){Close();return;}
        if(p.MouseEvent==TabEvent&&TabOf(p) is {} tab){ActiveTab=tab;return;}
        string key=p.MouseEvent??p.Name??p.Path;
        if(unhandled.Add(key))UiResourceLog.Write($"ranking-ui {key} 尚未接上（伺服器只回一頁）");
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
