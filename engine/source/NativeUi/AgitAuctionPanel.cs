using System.Globalization;
using System.Text.RegularExpressions;

// T323 (#761, P-D of docs/reference/pak-resources.md §6, row 1:0 S_AGIT_LIST / 4:AgitAuctionUI): the native agit auction
// window, window "Agitmap_Window" of AgitAuctionUI.xml, opened by S156 HouseList (AgitPackets.ParseHouseList, Agit.cs).
// AgitAuctionUI.xml is not in 0-客戶端 (PAK§4: all paks checked); it is read at run time from the reference client through
// UiResources.Shared.Xml (T319/#757: main Tile.pak first, then the reference Tile.pak — kor25 by default, PAK§3; SHA-256
// logged as "ui-xml-ref"). Surfs go through SurfCatalog.Get (local, then the T322/#760 reference fallback); Png attributes
// go through OptionPngSource (T330/#768: local Sprite*.pak, then the reference Sprite*.pak, SHA-256 logged as "option-png").
// Nothing is copied into the repository. Missing images are not drawn (no substitute) and are listed in Missing.
//
// Wiring. UiRoot.Build is all-or-nothing, so the registration uses the host layout "uistatus86" (always in the local Tile.pak,
// same as OptionPanel / GameRankingPanel) and Load reads AgitAuctionUI.xml itself: no reference client / no XML / malformed
// XML → Unavailable, the window never opens and the rest of the native UI keeps working. S156 reaches the panel through an
// observer on WorldSession (below: [WorldReceive(156, Observe=true)], so Agit.cs keeps consuming the packet unchanged) that
// posts an open request to AgitAuctionWindow; the panel polls it in Update (same pattern as AgitMapWindow / AgitMapPanel).
// With --classic-ui (or when the panel is unavailable) nothing changes: S156 stays data only (WorldSession.AgitHouseList and
// the "agit" snapshot), which is the existing behaviour — there was no WinForms auction list before this task.
//
// What is built (Agitmap_Window only):
//   BaseSurf 3961 + FrameSurf 2828 at the window origin; window size = declared Width/Height (550×316), else the BaseSurf
//   image; Moveable="20" = the top 20 px drag the window; Exit_Button (Exit_AgitWin) closes.
//   Auction_List: header buttons, Items_List rows (CheckButton, stacked by Height + Gap) with the cells Name_Label,
//   Price_Label, Deadline_Label filled from S156 (name, price, "月/日" deadline — formats 推測), ItemList_Scrollbar Up/Down
//   (and the wheel) scroll one row; the thumb (Move_Button) is placed by ScrollStart/ScrollHeight (drag 待做).
//   MapImage_Control: the four town Controls; only the town of the listed houses is shown. Town ← house id: region = id>>16
//   (8 Gludio, 4 Giran, 5 Heine, 7 Aden), button "Agithouse<n>_Image" = house number n+1 (推測: the XML has 6/45/11/67 buttons,
//   the same counts as the BIN AgitMapWindow tables, AgitMapLayout.MarkerCount; 3.81 house ids are region<<16|number).
//   Clicking a row or a house button selects that house (row and house drawn with the push image while selected).
//   List_Button (List_AgitWin) shows / hides Auction_List (推測: the list background png 3055 is 197×144 at (2,149) and
//   covers Giran / Gludio house buttons, so the map must be reachable without it); the list is shown on every new S156.
//   An image drawn above a button also hides it from the mouse. List_Button carries String 4391 of the reference client's text table: drawn only through TextResolver (null in the
//   product, as OptionPanel: the local string-c.tbl ids are not the reference client's).
// Paint order (推測, binder not decompiled): inside a container the children are drawn in reverse XML order (the XML lists
// Back_Image / ExitBack_Image / Back_Control last, yet they must stay below the buttons and labels), a Button's own labels
// right after it; hit-testing is the reverse of paint order (first XML child on top).
// Not built / not wired (待做, see docs/reports/issue-761.md): Bid_Window (bid amount; S136 agapply in the original),
// AuctionInfo_Window (MultiLabel + Bid button), the column sort buttons, the thumb drag, Back_Control
// (MapClick_AgitWin), and sending C125 "select" for the chosen house (the existing sender RingWindows.SendAgitSelect only
// accepts the current S187 request) — those clicks are logged once as "agit-auction-ui … 尚未接上".

namespace P06 {
    /// <summary>One S156 open request; Revision grows monotonically within the process.</summary>
    public sealed record AgitAuctionOpenRequest(long Revision,AgitHouseList List);

    /// <summary>
    /// T323 (#761): S156 open requests for the native AgitAuctionPanel (polled in Update; no event subscription, no leaks).
    /// Written by the WorldSession observer below; cleared by WorldSession.Reset.
    /// </summary>
    public static class AgitAuctionWindow {
        static readonly object gate=new();
        static long revision;
        static AgitAuctionOpenRequest? current;
        public static AgitAuctionOpenRequest? Current{get{lock(gate)return current;}}
        public static AgitAuctionOpenRequest Request(AgitHouseList list){lock(gate)return current=new(++revision,list);}
        public static void Clear(){lock(gate)current=null;}
    }

    public sealed partial class WorldSession {
        /// <summary>Observer only: Agit.cs ReceiveAgitHouseList still consumes S156. A malformed packet posts nothing.</summary>
        [WorldReceive(AgitPackets.HouseListOpcode,Observe=true)]
        void ObserveAgitAuctionWindow(byte[] plain){
            AgitHouseList list;
            try{list=AgitPackets.ParseHouseList(plain);}catch(ProtocolException){return;}
            AgitAuctionWindow.Request(list);
        }

        [WorldReset]
        void ResetAgitAuctionWindow()=>AgitAuctionWindow.Clear();

        [WorldSnapshot("agitAuctionWindow")]
        object? AgitAuctionWindowSnapshot()=>AgitAuctionWindow.Current is {} r?new{revision=r.Revision,boardId=r.List.BoardId,houses=r.List.Houses.Count}:null;
    }
}

namespace P06.NativeUi {
    /// <summary>"BasePng=\"2978;1\"" → Attr BasePng, Id 2978, Flag 1 (Flag 0 when absent; meaning 待驗證).</summary>
    public sealed record AgitAuctionImageRef(string Attr,int Id,int Flag) {
        public bool IsSurf=>Attr.EndsWith("Surf",StringComparison.Ordinal);
        public override string ToString()=>$"{(IsSurf?"surf":"png")} {Id}";
    }

    public enum AgitAuctionPieceKind { Image, Button, Label, Row, Cell, Thumb }

    /// <summary>
    /// A drawable / clickable element. Offset is relative to the window origin. Surface null = missing image (not drawn).
    /// Region/Number: a town house button (0 = not a house). Row: list row index (Row and Cell pieces). Cell: label name.
    /// </summary>
    public sealed record AgitAuctionPiece(AgitAuctionPieceKind Kind,string Path,string? Name,Point Offset,Size DeclaredSize,
        AgitAuctionImageRef? Base,UiSurface? BaseSurface,UiSurface? HighlightSurface,UiSurface? PushSurface,string? MouseEvent,string Align,
        int Region=0,int Number=0,int Row=-1,string? TextKind=null,string? TextValue=null,bool InList=false) {
        public Size ImageSize=>BaseSurface==null?Size.Empty:new(BaseSurface.Width,BaseSurface.Height);
        public Rectangle Local=>new(Offset,DeclaredSize.Width>0&&DeclaredSize.Height>0?DeclaredSize:ImageSize);
        public bool Clickable=>Kind is AgitAuctionPieceKind.Button or AgitAuctionPieceKind.Row or AgitAuctionPieceKind.Thumb&&Local.Width>0&&Local.Height>0;
        public uint HouseId=>Region>0&&Number>0?(uint)(Region<<16|Number):0;
    }

    /// <summary>The vertical scroll bar of the list: track origin (window-relative), ScrollStart, ScrollHeight.</summary>
    public sealed record AgitAuctionScroll(Point Origin,int Start,int Height);

    public sealed class AgitAuctionPanel(UiResources? resources=null,Func<AgitAuctionOpenRequest?>? source=null):IUiMovablePanel,IUiClosablePanel {
        [UiPanel(174)] internal static UiPanelRegistration Registration=>new(PanelId,HostLayout,root=>new AgitAuctionPanel(root.Resources));   // T323 (#761): AgitAuctionUI.xml from the reference client (P-A), loaded inside Load
        public const string PanelId="AgitAuctionUI";
        public const string LayoutName="AgitAuctionUI",WindowNode="Window",WindowName="Agitmap_Window";
        public const string ExitEvent="Exit_AgitWin",RowEvent="Item_AuctionList",HouseEvent="Auction_AgitWin",ScrollEvent="Scroll_AuctionList",ListEvent="List_AgitWin",ListControl="Auction_List";
        public const string NameCell="Name_Label",PriceCell="Price_Label",DeadlineCell="Deadline_Label";
        /// <summary>Layout handed to UiRoot.Build (always in the local Tile.pak); the real layout is LayoutName, read in Load.</summary>
        public const string HostLayout="uistatus86";
        public static readonly Color TextColor=Color.White;
        /// <summary>Town Control name → 3.81 house region (house id &gt;&gt; 16), see the file header (推測).</summary>
        public static readonly IReadOnlyDictionary<string,int> TownRegions=new Dictionary<string,int>(StringComparer.Ordinal){
            ["Gludiomap_Image"]=8,["Giranmap_Image"]=4,["Heinemap_Image"]=5,["Adenmap_Image"]=7};
        static readonly Regex HouseName=new(@"^Agithouse(\d{1,3})_Image$",RegexOptions.CultureInvariant);

        readonly Func<AgitAuctionOpenRequest?> source=source??(()=>AgitAuctionWindow.Current);

        public string Id=>PanelId;
        public UiLayer Layer=>UiLayer.Window;
        /// <summary>Why the window cannot open; null when usable.</summary>
        public string? Unavailable{get;private set;}="not loaded";
        public bool Available=>Unavailable==null;
        public string? LayoutFile{get;private set;}
        public Point Origin{get;private set;}
        public Size Size{get;private set;}
        public int MoveableHeight{get;private set;}
        public Rectangle Bounds=>open&&!Size.IsEmpty?new(Origin,Size):Rectangle.Empty;
        public bool Visible=>open;
        public AgitAuctionPiece? BaseImage{get;private set;}
        public AgitAuctionPiece? FrameImage{get;private set;}
        /// <summary>Every piece in paint order (all towns; Paint/hit-test filter by ActiveRegion).</summary>
        public IReadOnlyList<AgitAuctionPiece> Pieces{get;private set;}=[];
        public int RowCount{get;private set;}
        public AgitAuctionScroll? Scroll{get;private set;}
        /// <summary>Regions that have a town Control in the layout.</summary>
        public IReadOnlyList<int> Regions{get;private set;}=[];
        /// <summary>Images named in the XML but not found (not drawn): "surf 3961 (AgitAuctionUI/Window[2]/@BaseSurf)".</summary>
        public IReadOnlyList<string> Missing{get;private set;}=[];
        /// <summary>Elements deliberately not built (other windows, runtime captions, MultiLabel/Edit, click catchers), by path.</summary>
        public IReadOnlyList<string> Skipped{get;private set;}=[];
        /// <summary>(kind "String"/"Desc", id) → text; null result or null resolver = the label is not drawn.</summary>
        public Func<string,string,string?>? TextResolver{get;set;}
        public IReadOnlyCollection<string> Unhandled=>unhandled;

        /// <summary>The S156 list on screen (null when closed and never opened).</summary>
        public AgitAuctionOpenRequest? Shown=>shown;
        public IReadOnlyList<AgitHouseEntry> Houses=>shown?.List.Houses??[];
        public int ScrollOffset{get;private set;}
        public int MaxScroll=>Math.Max(0,Houses.Count-RowCount);
        /// <summary>Region whose town Control is shown (0 = none).</summary>
        public int ActiveRegion{get;private set;}
        public uint? SelectedHouse{get;private set;}
        /// <summary>Auction_List shown (List_Button toggles it, 推測; shown again on every new S156).</summary>
        public bool ListVisible{get;private set;}=true;

        bool open;AgitAuctionOpenRequest? shown;long dismissed=-1;
        AgitAuctionPiece? pressed,hover;bool pressedInside;
        readonly HashSet<string> unhandled=new(StringComparer.Ordinal);

        public void Load(UiLayout host,SurfCatalog surfs){
            Clear();
            if(resources==null){Unavailable="沒有 UiResources";return;}
            UiLayout layout;
            try{layout=resources.Xml.Load(LayoutName);}
            catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"agit-auction-ui 無法載入 {LayoutName}，盟屋拍賣窗停用（S156 照舊只存資料）：{ex.Message}");return;}
            var png=new OptionPngSource(resources.Sprites,resources.Xml.Reference);
            try{Load(layout,surfs.Get,png.Get);}
            catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"agit-auction-ui {layout.FileName} 版面不符，盟屋拍賣窗停用：{ex.Message}");return;}
            if(Missing.Count>0)UiResourceLog.Write($"agit-auction-ui 缺圖不畫：{string.Join("、",Missing)}");
            if(!Available)UiResourceLog.Write($"agit-auction-ui 停用：{Unavailable}");
        }

        void Clear(){
            open=false;shown=null;dismissed=-1;pressed=hover=null;pressedInside=false;LayoutFile=null;Origin=Point.Empty;Size=Size.Empty;MoveableHeight=0;
            BaseImage=FrameImage=null;Pieces=[];RowCount=0;Scroll=null;Regions=[];Missing=[];Skipped=[];ScrollOffset=0;ActiveRegion=0;SelectedHouse=null;ListVisible=true;
            Unavailable="not loaded";
        }

        /// <summary>Offline checks: images come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
        internal void Load(UiLayout layout,Func<int,UiSurface?> surf,Func<int,UiSurface?> png){
            Clear();
            var windows=layout.Root.ChildrenNamed(WindowNode).ToList();
            var window=windows.FirstOrDefault(w=>w.Attr("Name")==WindowName)??throw new UiLayoutException(layout.FileName,layout.Root.Path,$"沒有 Window Name={WindowName}");
            var missing=new List<string>();var skipped=new List<string>();
            foreach(var w in windows)if(w.Path!=window.Path)skipped.Add($"{w.Path}（{w.Attr("Name")}，未做）");
            UiSurface? Resolve(AgitAuctionImageRef? r,string path){
                if(r==null)return null;
                var s=r.IsSurf?surf(r.Id):png(r.Id);
                if(s==null)missing.Add($"{r} ({path})");
                return s;
            }
            var b=new Builder(layout.FileName,skipped,Resolve);
            var baseRef=FirstImage(window,"BaseSurf","BasePng");var frameRef=FirstImage(window,"FrameSurf","FramePng");
            var baseSurface=Resolve(baseRef,window.Path+"/@"+baseRef?.Attr);var frameSurface=Resolve(frameRef,window.Path+"/@"+frameRef?.Attr);
            b.Container(window,Point.Empty,0);
            if(b.Rows==0)throw new UiLayoutException(layout.FileName,window.Path,"沒有清單列（Items_List 的 CheckButton）");
            var declared=new Size(window.TryInt("Width")??0,window.TryInt("Height")??0);
            var size=declared.Width>0&&declared.Height>0?declared:baseSurface==null?Size.Empty:new Size(baseSurface.Width,baseSurface.Height);
            LayoutFile=layout.FileName;Origin=new(window.Int("X"),window.Int("Y"));Size=size;MoveableHeight=window.TryInt("Moveable")??0;
            BaseImage=baseRef==null?null:new(AgitAuctionPieceKind.Image,window.Path,window.Attr("Name"),Point.Empty,Size.Empty,baseRef,baseSurface,null,null,null,"left");
            FrameImage=frameRef==null?null:new(AgitAuctionPieceKind.Image,window.Path,window.Attr("Name"),Point.Empty,Size.Empty,frameRef,frameSurface,null,null,null,"left");
            Pieces=b.Pieces;RowCount=b.Rows;Scroll=b.Scroll;Regions=b.Regions;Missing=missing;Skipped=skipped;
            Unavailable=size.IsEmpty?$"{layout.FileName}: 沒有宣告大小而且底圖缺，無法定出視窗範圍（不用替代圖）":null;
        }

        static Point Rel(UiNode n,Point parent)=>new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0));

        /// <summary>"2978;1" → (2978, 1); "3056" → (3056, 0). Attribute absent → null.</summary>
        public static AgitAuctionImageRef? ImageAttr(UiNode n,string attr){
            var raw=n.Attr(attr);if(raw==null)return null;
            var parts=raw.Split(';');
            static bool P(string s,out int v)=>int.TryParse(s.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out v);
            if(parts.Length>2||!P(parts[0],out int id)||id<=0)throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 不是圖號");
            int flag=0;if(parts.Length==2&&!P(parts[1],out flag))throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\" 的旗標不是整數");
            return new(attr,id,flag);
        }
        static AgitAuctionImageRef? FirstImage(UiNode n,params string[] attrs){foreach(var a in attrs)if(ImageAttr(n,a) is {} r)return r;return null;}

        sealed class Builder(string file,List<string> skipped,Func<AgitAuctionImageRef?,string,UiSurface?> resolve) {
            public readonly List<AgitAuctionPiece> Pieces=[];
            public readonly List<int> Regions=[];
            public int Rows;
            public AgitAuctionScroll? Scroll;
            public bool HasList;bool inList;

            AgitAuctionPiece Piece(AgitAuctionPieceKind kind,UiNode n,Point at,int region=0,int number=0,int row=-1){
                var normal=FirstImage(n,"BasePng","BaseSurf","Png","Surf");var hi=FirstImage(n,"HighlightPng","HighlightSurf");var push=FirstImage(n,"PushPng","PushSurf");
                return new(kind,n.Path,n.Attr("Name"),at,new(n.TryInt("Width")??0,n.TryInt("Height")??0),normal,resolve(normal,n.Path+(normal==null?"":"/@"+normal.Attr)),
                    resolve(hi,n.Path+"/@"+hi?.Attr),resolve(push,n.Path+"/@"+push?.Attr),n.Attr("MouseEvent"),(n.Attr("Align")??"left").Trim().ToLowerInvariant(),region,number,row,InList:inList);
            }

            /// <summary>Children in reverse XML order (see the file header).</summary>
            public void Container(UiNode parent,Point origin,int region){
                foreach(var c in parent.Children.Reverse())Element(c,origin,region);
            }

            void Element(UiNode c,Point origin,int region){
                var at=Rel(c,origin);
                switch(c.Name){
                    case "Image":{
                        if(FirstImage(c,"Png","Surf")==null){skipped.Add(c.Path+"（沒有圖號）");return;}
                        Pieces.Add(Piece(AgitAuctionPieceKind.Image,c,at));return;
                    }
                    case "Button":{
                        int number=0;
                        if(region>0){
                            if(c.Attr("Name") is {} n&&HouseName.Match(n) is {Success:true} m)number=int.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)+1;
                            else{skipped.Add(c.Path+"（城鎮內不認得的按鈕）");return;}
                        }
                        Pieces.Add(Piece(AgitAuctionPieceKind.Button,c,at,number>0?region:0,number));
                        foreach(var k in c.Children)AddLabel(k,at);
                        return;
                    }
                    case "Label":AddLabel(c,origin);return;
                    case "Control":{
                        if(c.Attr("Name") is {} name&&TownRegions.TryGetValue(name,out int town)){
                            if(Regions.Contains(town))throw new UiLayoutException(file,c.Path,$"城鎮 {name} 重複");
                            Regions.Add(town);Container(c,at,town);return;
                        }
                        if(!c.Children.Any()){skipped.Add(c.Path+"（點擊底層，沒有圖）");return;}
                        if(c.Attr("Name")==ListControl){
                            if(HasList)throw new UiLayoutException(file,c.Path,$"{ListControl} 重複");
                            HasList=inList=true;
                            try{Container(c,at,region);}finally{inList=false;}
                            return;
                        }
                        Container(c,at,region);return;
                    }
                    case "List":ListRows(c,at);return;
                    case "ScrollBar":{
                        if(Scroll!=null)throw new UiLayoutException(file,c.Path,"捲軸重複");
                        Scroll=new(at,c.TryInt("ScrollStart")??0,c.TryInt("ScrollHeight")??0);
                        foreach(var k in c.Children.Reverse()){
                            if(k.Name!="Button"){skipped.Add(k.Path);continue;}
                            Pieces.Add(Piece(k.Attr("Name")=="Move_Button"?AgitAuctionPieceKind.Thumb:AgitAuctionPieceKind.Button,k,Rel(k,at)));
                        }
                        return;
                    }
                    // MultiLabel / Edit belong to the other windows; anything else is not built.
                    default:skipped.Add(c.Path);return;
                }
            }

            void AddLabel(UiNode c,Point origin){
                if(c.Name!="Label"){skipped.Add(c.Path);return;}
                string? kind=c.Attr("String")!=null?"String":c.Attr("Desc")!=null?"Desc":null;
                if(kind==null){skipped.Add(c.Path+"（Caption 執行期才填）");return;}
                Pieces.Add(Piece(AgitAuctionPieceKind.Label,c,Rel(c,origin)) with{TextKind=kind,TextValue=c.Attr(kind)!.Trim()});
            }

            void ListRows(UiNode list,Point at){
                if(Rows>0)throw new UiLayoutException(file,list.Path,"清單重複");
                int gap=list.TryInt("Gap")??0,y=0,index=0;
                foreach(var row in list.Children){
                    if(row.Name!="CheckButton"){skipped.Add(row.Path);continue;}
                    int h=row.TryInt("Height")??0;
                    if(h<=0)throw new UiLayoutException(file,row.Path,"清單列沒有 Height");
                    var rowAt=new Point(at.X+(row.TryInt("X")??0),at.Y+y+(row.TryInt("Y")??0));
                    Pieces.Add(Piece(AgitAuctionPieceKind.Row,row,rowAt,row:index));
                    foreach(var k in row.Children){
                        if(k.Name=="Label"&&k.Attr("Name") is NameCell or PriceCell or DeadlineCell)Pieces.Add(Piece(AgitAuctionPieceKind.Cell,k,Rel(k,rowAt),row:index));
                        else skipped.Add(k.Path);
                    }
                    y+=h+gap;index++;
                }
                Rows=index;
            }
        }

        // ---- data ----

        /// <summary>Cell text of one S156 entry (formats 推測: price as plain digits, deadline "月/日").</summary>
        public static string CellText(AgitHouseEntry house,string cell)=>cell switch{
            NameCell=>house.Name,
            PriceCell=>house.Price.ToString(CultureInfo.InvariantCulture),
            DeadlineCell=>string.Create(CultureInfo.InvariantCulture,$"{house.Month}/{house.Day}"),
            _=>""};

        public void Update(IUiGameState state){
            var req=state.InWorld?source():null;
            if(req==null){if(open)Close();shown=null;return;}
            if(!Available||req.Revision==dismissed||req.Revision==shown?.Revision)return;
            Show(req);
        }

        void Show(AgitAuctionOpenRequest req){
            shown=req;open=true;pressed=hover=null;pressedInside=false;ScrollOffset=0;SelectedHouse=null;ListVisible=true;
            ActiveRegion=req.List.Houses.Select(h=>(int)(h.HouseId>>16)).FirstOrDefault(r=>Regions.Contains(r));
        }

        /// <summary>Re-opens the last S156 list. false when unavailable or nothing was received.</summary>
        public bool Open(){
            if(!Available||shown==null)return false;
            dismissed=-1;open=true;pressed=hover=null;pressedInside=false;return true;
        }
        public void Close(){if(open&&shown!=null)dismissed=shown.Revision;open=false;pressed=hover=null;pressedInside=false;}
        public bool Toggle(){if(open){Close();return true;}return Open();}

        public bool ScrollBy(int rows){
            int next=Math.Clamp(ScrollOffset+rows,0,MaxScroll);
            if(next==ScrollOffset)return false;
            ScrollOffset=next;return true;
        }

        /// <summary>Selects a listed house and scrolls it into view. false when the house is not in the S156 list.</summary>
        public bool Select(uint houseId){
            int index=-1;for(int i=0;i<Houses.Count;i++)if(Houses[i].HouseId==houseId){index=i;break;}
            if(index<0)return false;
            SelectedHouse=houseId;
            if(index<ScrollOffset)ScrollOffset=index;else if(index>=ScrollOffset+RowCount)ScrollOffset=index-RowCount+1;
            return true;
        }

        /// <summary>The S156 entry shown in a list row (after scrolling), or null.</summary>
        public AgitHouseEntry? RowHouse(int row)=>row>=0&&row<RowCount&&ScrollOffset+row<Houses.Count?Houses[ScrollOffset+row]:null;

        // ---- geometry ----

        bool Live(AgitAuctionPiece p)=>(p.Region==0||p.Region==ActiveRegion)&&(ListVisible||!p.InList);
        /// <summary>Thumb offset: ScrollStart + (ScrollHeight − thumb height) × offset / max (top when nothing to scroll).</summary>
        Point PieceOffset(AgitAuctionPiece p){
            if(p.Kind!=AgitAuctionPieceKind.Thumb||Scroll is not {} s)return p.Offset;
            int travel=Math.Max(0,s.Height-p.Local.Height);
            int y=s.Origin.Y+s.Start+(MaxScroll==0?0:travel*ScrollOffset/MaxScroll);
            return new(p.Offset.X,y);
        }
        public Rectangle At(AgitAuctionPiece p){var o=PieceOffset(p);return new(Origin.X+o.X,Origin.Y+o.Y,p.Local.Width,p.Local.Height);}
        /// <summary>
        /// Topmost clickable live piece at a logical point (later in paint order = on top). An image drawn above a button
        /// hides it from the mouse too (the list background over the town map).
        /// </summary>
        public AgitAuctionPiece? ButtonAt(Point location){
            if(!open)return null;
            for(int i=Pieces.Count-1;i>=0;i--){
                var p=Pieces[i];
                if(!Live(p)||!At(p).Contains(location))continue;
                if(p.Clickable)return p;
                if(p.Kind==AgitAuctionPieceKind.Image&&p.BaseSurface!=null)return null;
            }
            return null;
        }

        // ---- paint ----

        public void Paint(UiCanvas canvas){
            if(!open)return;
            canvas.DrawSurface(BaseImage?.BaseSurface,Origin.X,Origin.Y);
            canvas.DrawSurface(FrameImage?.BaseSurface,Origin.X,Origin.Y);
            foreach(var p in Pieces){
                if(!Live(p))continue;
                var o=PieceOffset(p);int x=Origin.X+o.X,y=Origin.Y+o.Y;
                switch(p.Kind){
                    case AgitAuctionPieceKind.Image:canvas.DrawSurface(p.BaseSurface,x,y);break;
                    case AgitAuctionPieceKind.Button or AgitAuctionPieceKind.Thumb or AgitAuctionPieceKind.Row:canvas.DrawSurface(ButtonSurface(p),x,y);break;
                    case AgitAuctionPieceKind.Label:
                        if(TextResolver?.Invoke(p.TextKind!,p.TextValue!) is {Length:>0} label)DrawLabel(canvas,p,label);
                        break;
                    case AgitAuctionPieceKind.Cell:
                        if(RowHouse(p.Row) is {} house&&p.Name is {} cell&&CellText(house,cell) is {Length:>0} text)DrawLabel(canvas,p,text);
                        break;
                }
            }
        }

        bool IsSelected(AgitAuctionPiece p)=>SelectedHouse is uint sel&&(p.HouseId==sel||p.Kind==AgitAuctionPieceKind.Row&&RowHouse(p.Row)?.HouseId==sel);

        /// <summary>Push while held or selected; Highlight while hovered; a missing state image falls back to the normal one.</summary>
        public UiSurface? ButtonSurface(AgitAuctionPiece p){
            bool held=p==pressed&&pressedInside;
            if((held||IsSelected(p))&&p.PushSurface!=null)return p.PushSurface;
            if(p==hover&&p.HighlightSurface!=null)return p.HighlightSurface;
            return p.BaseSurface;
        }

        /// <summary>Left x (window-relative) of a text of this width inside the label (Align left/center/right).</summary>
        public static int AlignX(AgitAuctionPiece label,int textWidth){
            int w=label.DeclaredSize.Width;
            if(w<=0||textWidth>=w)return label.Offset.X;
            return label.Align switch{"center"=>label.Offset.X+(w-textWidth)/2,"right"=>label.Offset.X+w-textWidth,_=>label.Offset.X};
        }
        void DrawLabel(UiCanvas canvas,AgitAuctionPiece label,string text){
            int width=UiText.Measure(canvas.Graphics,text).Width;
            canvas.DrawText(text,Origin.X+AlignX(label,width),Origin.Y+label.Offset.Y,TextColor);
        }

        // ---- input ----

        public bool IsDragHandle(Point location)=>open&&Bounds.Contains(location)&&location.Y<Origin.Y+MoveableHeight&&ButtonAt(location)==null;
        public void MoveTo(Point location)=>Origin=location;

        public bool Mouse(UiMouseEvent e,IUiCommands commands){
            if(!open)return false;
            bool inside=Bounds.Contains(e.Location);
            if(e.Kind==UiMouseKind.Move)hover=ButtonAt(e.Location);
            if(e.Kind==UiMouseKind.Wheel){if(inside&&e.Delta!=0)ScrollBy(e.Delta>0?-1:1);return inside;}
            if(e.Button!=MouseButtons.Left&&e.Kind!=UiMouseKind.Move)return inside||pressed!=null;
            switch(e.Kind){
                case UiMouseKind.Down:
                    pressed=ButtonAt(e.Location);pressedInside=pressed!=null;
                    return inside;
                case UiMouseKind.Move:
                    if(pressed!=null)pressedInside=At(pressed).Contains(e.Location);
                    return inside||pressed!=null;
                case UiMouseKind.Up:{
                    var was=pressed;pressed=null;pressedInside=false;
                    if(was!=null&&At(was).Contains(e.Location))Fire(was,commands);
                    return inside||was!=null;
                }
                default:return inside;
            }
        }

        void Fire(AgitAuctionPiece p,IUiCommands commands){
            if(p.MouseEvent==ExitEvent){Close();commands.ReturnGameFocus();return;}
            if(p.Kind==AgitAuctionPieceKind.Row){
                if(RowHouse(p.Row) is {} house){Select(house.HouseId);NotWired("select","選取盟屋後送 C125 select");}
                return;
            }
            if(p.HouseId!=0){
                if(Select(p.HouseId))NotWired("select","選取盟屋後送 C125 select");
                return;
            }
            if(p.MouseEvent==ScrollEvent&&p.Name is "Up_Button" or "Down_Button"){ScrollBy(p.Name=="Up_Button"?-1:1);return;}
            if(p.MouseEvent==ListEvent){ListVisible=!ListVisible;return;}
            NotWired(p.MouseEvent??p.Name??p.Path,"");
        }

        void NotWired(string key,string what){
            if(unhandled.Add(key))UiResourceLog.Write($"agit-auction-ui {key} 尚未接上{(what.Length>0?"（"+what+"）":"")}");
        }

        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }
}
