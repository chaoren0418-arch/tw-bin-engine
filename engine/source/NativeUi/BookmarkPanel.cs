namespace P06.NativeUi;

// ui-bookmark（#472）：BookmarkWindowUI（BookmarkUI.xml，CBookmarkWindowRenewal VA 0xBD9F50／FO 0x7D9350，gap-matrix §4）。
// 版面全部在執行期從 Tile.pak 的 BookmarkUI.xml 讀（台版沒有 -c 檔，退回無後綴檔，UI§1）；這裡只寫元素名稱，不寫座標。
// 已驗證（XML，UI§6）：框 4313（263×420）、底圖 4312 (6,9)、關閉鈕 (249,15)、兩個標題圖、快速移動清單 (23,62) 120×75 五列、
//   記憶座標清單 (22,162) 200×204 十二列×17、捲軸（WheelControl (23,163) 內 X=218）、區域下拉 (129,141)、隨機傳送 (129,42)、
//   排序 (182,42)、13 色調色盤 (19,368) 間距 16、確認 (67,395)／取消 (134,395)、數量標籤 (200,397) 的 surf 與座標。
// 推測（報告已列）：Moveable="20" 是可拖曳的上方高度；"4313;1" 只取第一個數字；文字白色、左上 +2；
//   排序鈕＝在伺服器順序與名稱順序間切換（只改顯示）；區域下拉＝依地圖編號篩選（區域名稱表未查證，顯示地圖編號）；
//   確認／取消都關窗（取消同時放棄調色盤選擇）；選取列後按 Delete＝刪除（走 T124 的 SendBookmark）；展開清單畫在最上層。
// 待驗證（不做）：快速移動清單的內容（3.81 伺服器沒有對應資料 → 空白、不可選）、隨機傳送鈕的效果（不送封包）、
//   調色盤顏色的用途（3.81 書籤沒有顏色欄位 → 只在窗內標示選取，不送出）、Tooltip、下拉清單自己的捲軸（Activate="0"，
//   以滑鼠滾輪捲動）、開窗快捷鍵（過渡：由開發工具的記憶座標分頁開關）。新增書籤需要輸入名稱，此 XML 沒有輸入欄，仍在 T124 分頁。

/// <summary>A button read from the XML: point relative to its parent, surf ids (first number of "id;n").</summary>
public sealed record BookmarkButton(Point At,int Base,int? Push,int? Highlight);

/// <summary>Layout read from BookmarkUI.xml. Every point/rectangle is relative to the window's top-left.</summary>
public sealed record BookmarkWindowLayout(
    Point Origin,int Moveable,int Frame,Point BaseAt,int Base,
    BookmarkButton Exit,Point QuickTitleAt,int QuickTitle,Point MemoryTitleAt,int MemoryTitle,
    Rectangle Combo,Rectangle ComboLabel,BookmarkButton ComboExpand,Rectangle ComboList,int ComboRowHeight,int ComboRows,int ComboPageSize,Point ComboBackAt,int ComboBack,int ComboRowPush,
    Rectangle Quick,int QuickRowHeight,int QuickRows,int QuickRowPush,
    BookmarkButton Sorting,BookmarkButton Random,
    Rectangle Memory,int MemoryRowHeight,int MemoryRows,int MemoryRowPush,
    Rectangle Wheel,Point Scroll,int ScrollStart,int ScrollHeight,Point ScrollBackAt,int ScrollBack,BookmarkButton Up,BookmarkButton Down,BookmarkButton Thumb,
    Point Palette,IReadOnlyList<BookmarkButton> Colors,Point PaletteImageAt,int PaletteImage,
    BookmarkButton Confirm,BookmarkButton Cancel,Rectangle Count) {

    public const string RootName="BookmarkWindowUI",WindowName="Bookmark_Window";
    public const int ColorCount=13;

    public static BookmarkWindowLayout Read(UiLayout layout){
        var root=layout.Root;
        if(root.Name!=RootName)throw new UiLayoutException(layout.FileName,root.Path,$"根節點應為 {RootName}");
        var w=Named(root,"Window",WindowName);
        var baseImage=Named(w,"Image","Base_Image");
        var combo=Named(w,"ComboBox","ZoneFilter_Combobox");var label=Named(combo,"Label","Text_Label");
        var expandList=Named(combo,"UpDownList","Expand_List");
        var (comboRowH,comboItems)=Rows(expandList,"CheckButton");
        var comboRect=Rect(combo);var comboList=new Rectangle(At(expandList),new(expandList.Int("Width"),expandList.Int("Height")));
        int comboRows=comboList.Height/comboRowH;   // Height 114 / 19 = 6 rows inside the XML box (PageSize says 7; kept, not used)
        if(comboRows<1)throw new UiLayoutException(layout.FileName,expandList.Path,"下拉清單高度不足一列");
        var comboBack=Named(expandList,"Image","Background_Image");
        var quick=Named(w,"MovableList","QuickMove_List");var (quickH,quickItems)=Rows(quick,"CheckButton");
        var memory=Named(w,"MovableList","MemoryPlace_List");var (memH,memItems)=Rows(memory,"CheckButton");
        var memRect=Rect(memory);
        if(memItems.Count*memH>memRect.Height)throw new UiLayoutException(layout.FileName,memory.Path,"列高總和超過清單高度");
        var wheel=Named(w,"WheelControl","MemoryPlaceList_WheelArea");var sb=Named(wheel,"ScrollBar","MemoryPlaceList_Scrollbar");
        var sbBack=Named(sb,"Image","Background_Image");
        var palette=Named(w,"Control","ColorPalette");
        var colors=Enumerable.Range(0,ColorCount).Select(i=>Button(Named(palette,"Button",$"ColorPalette{i}_Button"))).ToArray();
        var paletteImage=Named(palette,"Image","ColorPaletteImage");
        return new(At(w),w.TryInt("Moveable")??0,Surf(w,"FrameSurf"),At(baseImage),Surf(baseImage,"Surf"),
            Button(Named(w,"Button","Exit_Button")),
            At(Named(w,"Image","QuickMoveListTitle_Image")),Surf(Named(w,"Image","QuickMoveListTitle_Image"),"Surf"),
            At(Named(w,"Image","MemoryZoneListTitle_Image")),Surf(Named(w,"Image","MemoryZoneListTitle_Image"),"Surf"),
            comboRect,Rect(label),Button(Named(combo,"Button","Expand_Button")),comboList,comboRowH,comboRows,expandList.TryInt("PageSize")??comboRows,
            At(comboBack),Surf(comboBack,"Surf"),Surf(comboItems[0],"PushSurf"),
            Rect(quick),quickH,quickItems.Count,Surf(quickItems[0],"PushSurf"),
            Button(Named(w,"Button","Sorting_Button")),Button(Named(w,"Button","RandomTeleport_Button")),
            memRect,memH,memItems.Count,Surf(memItems[0],"PushSurf"),
            Rect(wheel),At(sb),sb.Int("ScrollStart"),sb.Int("ScrollHeight"),At(sbBack),Surf(sbBack,"Surf"),
            Button(Named(sb,"Button","Up_Button")),Button(Named(sb,"Button","Down_Button")),Button(Named(sb,"Button","Move_Button")),
            At(palette),colors,At(paletteImage),Surf(paletteImage,"Surf"),
            Button(Named(w,"Button","Confirm_Button")),Button(Named(w,"Button","Cancel_Button")),Rect(Named(w,"Label","BookmarkCount_Label")));
    }
    /// <summary>All rows of a list must share one height and one selection surf (the panel draws them as a uniform list).</summary>
    static (int Height,List<UiNode> Items) Rows(UiNode list,string element){
        var items=list.ChildrenNamed(element).ToList();
        if(items.Count==0)throw new UiLayoutException(list.Layout.FileName,list.Path,$"清單沒有任何 {element}");
        int h=items[0].Int("Height");
        if(h<=0)throw new UiLayoutException(list.Layout.FileName,items[0].Path,"列高需為正數");
        foreach(var i in items)if(i.Int("Height")!=h||i.Attr("PushSurf")!=items[0].Attr("PushSurf"))throw new UiLayoutException(list.Layout.FileName,i.Path,"各列高度或選取圖不一致");
        return (h,items);
    }
    // Children without X/Y sit at the parent's origin (Expand_List omits X, scroll bar images omit X/Y).
    static Point At(UiNode n)=>new(n.TryInt("X")??0,n.TryInt("Y")??0);
    static Rectangle Rect(UiNode n)=>new(At(n),new(n.Int("Width"),n.Int("Height")));
    // "4313;1", "4312;0" (Base_Image really has ";0") or "3667": only the surf id; the second number is 待驗證 (V4).
    static int Surf(UiNode n,string attr)=>SummonPanel.SurfId(n,attr);
    static int? OptSurf(UiNode n,string attr)=>n.Attr(attr)==null?null:Surf(n,attr);
    static BookmarkButton Button(UiNode n)=>new(At(n),Surf(n,"BaseSurf"),OptSurf(n,"PushSurf"),OptSurf(n,"HighlightSurf"));
    static UiNode Named(UiNode parent,string element,string name){
        var m=parent.ChildrenNamed(element).Where(c=>c.Attr("Name")==name).Take(2).ToList();
        if(m.Count!=1)throw new UiLayoutException(parent.Layout.FileName,$"{parent.Path}/{element}[@Name={name}]",m.Count==0?"缺少節點":"節點不唯一");
        return m[0];
    }
}

public sealed class BookmarkWindowPanel:IUiMovablePanel {
    [UiPanel(150)] internal static UiPanelRegistration Registration=>new(PanelId,LayoutName,_=>new BookmarkWindowPanel());   // ui-bookmark #472
    public const string PanelId=BookmarkWindowLayout.WindowName,LayoutName="BookmarkUI";

    Func<int,UiSurface?> surf=_=>null;
    BookmarkWindowLayout? layout;
    Point location;Size size;bool visible;
    string? pressed,hover;
    BookmarkUiState data=BookmarkUiState.Empty;
    IReadOnlyList<BookmarkRow> rows=[];
    IReadOnlyList<ushort?> filterOptions=[null];

    public IBookmarkUiHost? Host{get;private set;}
    public void Attach(IBookmarkUiHost host)=>Host=host;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds=>new(location,size);
    public bool Visible=>visible&&layout!=null;
    public BookmarkWindowLayout Layout=>layout??throw new InvalidOperationException($"{Id} 尚未載入版面");
    /// <summary>Memory-place rows after the zone filter and sort, in display order.</summary>
    public IReadOnlyList<BookmarkRow> Rows=>rows;
    public BookmarkUiState Data=>data;
    /// <summary>First row index shown in list row 0.</summary>
    public int Top{get;private set;}
    public int MaxTop=>layout==null?0:Math.Max(0,rows.Count-layout.MemoryRows);
    public uint? SelectedId{get;private set;}
    public BookmarkRow? Selected=>SelectedId is uint id?rows.FirstOrDefault(r=>r.Id==id):null;
    public bool SortByName{get;private set;}
    public ushort? Filter{get;private set;}
    public IReadOnlyList<ushort?> FilterOptions=>filterOptions;
    public bool ComboOpen{get;private set;}
    public int ComboTop{get;private set;}
    public int? SelectedColor{get;private set;}

    public void Load(UiLayout xml,SurfCatalog surfs)=>Load(xml,surfs.Get);
    /// <summary>Offline checks may pass their own surfaces; null = surf missing → that part is not drawn and not clickable.</summary>
    public void Load(UiLayout xml,Func<int,UiSurface?> surfaces){
        var l=BookmarkWindowLayout.Read(xml);
        surf=surfaces;
        // The window has no BaseSurf: its size is the frame surf (4313 = 263×420, UI§6). Missing frame → the window is disabled.
        if(surf(l.Frame) is not {} frame){
            layout=null;size=Size.Empty;visible=false;
            UiResourceLog.Write($"panel {PanelId} 停用：缺少視窗框 surf {l.Frame}");
            return;
        }
        layout=l;location=l.Origin;size=new(frame.Width,frame.Height);
        visible=false;pressed=hover=null;Top=0;SelectedId=null;ComboOpen=false;ComboTop=0;SelectedColor=null;
    }

    public void Show(){if(layout!=null)visible=true;}
    public void Hide(){visible=false;pressed=hover=null;ComboOpen=false;SelectedColor=null;}
    public void Toggle(){if(visible)Hide();else Show();}

    public void Update(IUiGameState state){
        if(!state.InWorld){Hide();data=BookmarkUiState.Empty;}
        else data=Host?.State??BookmarkUiState.Empty;
        filterOptions=BookmarkListView.FilterOptions(data.Rows);
        if(!filterOptions.Contains(Filter))Filter=null;
        rows=BookmarkListView.Apply(data.Rows,Filter,SortByName);
        Top=Math.Clamp(Top,0,MaxTop);
        ComboTop=Math.Clamp(ComboTop,0,ComboMaxTop);
        if(SelectedId is uint id&&!rows.Any(r=>r.Id==id))SelectedId=null;
    }
    public void ScrollTo(int top)=>Top=Math.Clamp(top,0,MaxTop);
    void Refilter(){rows=BookmarkListView.Apply(data.Rows,Filter,SortByName);Top=Math.Clamp(Top,0,MaxTop);if(SelectedId is uint id&&!rows.Any(r=>r.Id==id))SelectedId=null;}

    // ---- geometry in logical 800×600 coordinates ----
    Point Abs(Point p)=>new(location.X+p.X,location.Y+p.Y);
    Rectangle Abs(Rectangle r)=>new(location.X+r.X,location.Y+r.Y,r.Width,r.Height);
    Size SurfSize(int id)=>surf(id) is {} u?new(u.Width,u.Height):Size.Empty;
    Rectangle ButtonRect(Point parent,BookmarkButton b)=>new(parent.X+b.At.X,parent.Y+b.At.Y,SurfSize(b.Base).Width,SurfSize(b.Base).Height);
    public Rectangle ExitRect=>ButtonRect(location,Layout.Exit);
    public Rectangle SortRect=>ButtonRect(location,Layout.Sorting);
    public Rectangle RandomRect=>ButtonRect(location,Layout.Random);
    public Rectangle ConfirmRect=>ButtonRect(location,Layout.Confirm);
    public Rectangle CancelRect=>ButtonRect(location,Layout.Cancel);
    public Rectangle CountRect=>Abs(Layout.Count);
    public Rectangle MemoryRowRect(int row){var l=Layout;return Abs(new Rectangle(l.Memory.X,l.Memory.Y+row*l.MemoryRowHeight,l.Memory.Width,l.MemoryRowHeight));}
    public Rectangle QuickRowRect(int row){var l=Layout;return Abs(new Rectangle(l.Quick.X,l.Quick.Y+row*l.QuickRowHeight,l.Quick.Width,l.QuickRowHeight));}
    public Point ComboAt=>Abs(Layout.Combo.Location);
    public Rectangle ComboExpandRect=>ButtonRect(ComboAt,Layout.ComboExpand);
    public Rectangle ComboLabelRect{get{var c=ComboAt;var r=Layout.ComboLabel;return new(c.X+r.X,c.Y+r.Y,r.Width,r.Height);}}
    public Rectangle ComboListRect{get{var c=ComboAt;var r=Layout.ComboList;return new(c.X+r.X,c.Y+r.Y,r.Width,r.Height);}}
    /// <summary>Row width = the row selection surf (4320 is 88 px; the XML list Width 55 is narrower than the drawn row), else Width.</summary>
    public Rectangle ComboRowRect(int row){var r=ComboListRect;int w=Math.Max(r.Width,SurfSize(Layout.ComboRowPush).Width);return new(r.X,r.Y+row*Layout.ComboRowHeight,w,Layout.ComboRowHeight);}
    public int ComboMaxTop=>layout==null?0:Math.Max(0,filterOptions.Count-layout.ComboRows);
    public Rectangle WheelRect=>Abs(Layout.Wheel);
    /// <summary>Scroll bar origin: WheelControl point + ScrollBar point (218, -5).</summary>
    public Point ScrollAt{get{var w=WheelRect;return new(w.X+Layout.Scroll.X,w.Y+Layout.Scroll.Y);}}
    public Rectangle UpRect=>ButtonRect(ScrollAt,Layout.Up);
    public Rectangle DownRect=>ButtonRect(ScrollAt,Layout.Down);
    /// <summary>Thumb travel: ScrollStart..ScrollStart+ScrollHeight below the scroll bar origin, as wide as the thumb.</summary>
    public Rectangle TrackRect{get{var s=ScrollAt;return new(s.X+Layout.Thumb.At.X,s.Y+Layout.ScrollStart,SurfSize(Layout.Thumb.Base).Width,Layout.ScrollHeight);}}
    int ThumbHeight=>Math.Min(SurfSize(Layout.Thumb.Base).Height,Layout.ScrollHeight);
    /// <summary>Thumb top relative to the scroll bar origin.</summary>
    public int ThumbY=>Layout.ScrollStart+(MaxTop==0?0:(Layout.ScrollHeight-ThumbHeight)*Top/MaxTop);
    public Point PaletteAt=>Abs(Layout.Palette);
    public Rectangle ColorRect(int i)=>ButtonRect(PaletteAt,Layout.Colors[i]);

    /// <summary>Clickable part under a logical point (null = none): fixed keys, "row:i", "color:i", "combo:i".</summary>
    public string? HitKey(Point p){
        if(layout==null)return null;
        if(ComboOpen){
            for(int i=0;i<layout.ComboRows;i++)if(ComboRowRect(i).Contains(p))return ComboTop+i<filterOptions.Count?"combo:"+(ComboTop+i):null;
            if(ComboListRect.Contains(p))return null;
        }
        if(ExitRect.Contains(p))return "exit";
        if(ComboExpandRect.Contains(p)||ComboLabelRect.Contains(p))return "expand";
        if(SortRect.Contains(p))return "sort";
        if(RandomRect.Contains(p))return "random";
        if(ConfirmRect.Contains(p))return "confirm";
        if(CancelRect.Contains(p))return "cancel";
        if(UpRect.Contains(p))return "up";
        if(DownRect.Contains(p))return "down";
        if(TrackRect.Contains(p))return "track";
        for(int i=0;i<BookmarkWindowLayout.ColorCount;i++)if(ColorRect(i).Contains(p))return "color:"+i;
        for(int i=0;i<layout.MemoryRows;i++)if(MemoryRowRect(i).Contains(p))return Top+i<rows.Count?"row:"+(Top+i):null;
        return null;
    }

    public void Paint(UiCanvas canvas){
        if(!Visible||layout is not {} l)return;
        void Draw(int? id,Point at){if(id is int i)canvas.DrawSurface(surf(i),at.X,at.Y);}
        void Button(Point parent,BookmarkButton b,string key,bool active=true){
            int id=active&&pressed==key&&b.Push is int push?push:active&&hover==key&&b.Highlight is int hi?hi:b.Base;
            Draw(id,new(parent.X+b.At.X,parent.Y+b.At.Y));
        }
        // XML order: frame (window attribute), base image, exit, titles, combo, quick list, buttons, memory list, scroll bar, palette, confirm/cancel, count.
        Draw(l.Frame,location);Draw(l.Base,Abs(l.BaseAt));
        Button(location,l.Exit,"exit");
        Draw(l.QuickTitle,Abs(l.QuickTitleAt));Draw(l.MemoryTitle,Abs(l.MemoryTitleAt));
        Button(ComboAt,l.ComboExpand,"expand");
        var lr=ComboLabelRect;var caption=BookmarkListView.Caption(Filter);var cm=UiText.Measure(canvas.Graphics,caption);
        canvas.DrawText(caption,lr.X+(lr.Width-cm.Width)/2,lr.Y+(lr.Height-cm.Height)/2,Color.White);   // HAlign="center"
        // Quick-move list: content unverified on 3.81 → nothing drawn.
        Button(location,l.Sorting,"sort");
        Button(location,l.Random,"random",active:false);   // effect unverified: no press feedback, no action
        for(int i=0;i<l.MemoryRows;i++){
            int index=Top+i;if(index>=rows.Count)break;
            var row=rows[index];var r=MemoryRowRect(i);
            if(row.Id==SelectedId)Draw(l.MemoryRowPush,r.Location);
            canvas.DrawText(row.Name,r.X+2,r.Y+2,row.DeletePending?Color.Gray:Color.White);
        }
        var s=ScrollAt;
        Draw(l.ScrollBack,new(s.X+l.ScrollBackAt.X,s.Y+l.ScrollBackAt.Y));
        Button(s,l.Up,"up");Button(s,l.Down,"down");
        Draw(pressed=="track"&&l.Thumb.Push is int tp?tp:l.Thumb.Base,new(s.X+l.Thumb.At.X,s.Y+ThumbY));
        // ColorPaletteImage (4321, 215×21, opaque) is the whole strip and comes last in the XML; drawn last it would hide every
        // swatch, so it is painted first as the background (inferred order; the swatches 4322–4334 repeat its colours).
        var pa=PaletteAt;
        Draw(l.PaletteImage,new(pa.X+l.PaletteImageAt.X,pa.Y+l.PaletteImageAt.Y));
        for(int i=0;i<l.Colors.Count;i++){
            var c=l.Colors[i];var at=new Point(pa.X+c.At.X,pa.Y+c.At.Y);
            Draw(c.Base,at);if(SelectedColor==i)Draw(c.Push,at);
        }
        Button(location,l.Confirm,"confirm");Button(location,l.Cancel,"cancel");
        var n=CountRect;canvas.DrawText(data.CountText,n.X,n.Y+1,Color.White);
        if(ComboOpen){
            var lt=ComboListRect;Draw(l.ComboBack,new(lt.X+l.ComboBackAt.X,lt.Y+l.ComboBackAt.Y));
            for(int i=0;i<l.ComboRows;i++){
                int index=ComboTop+i;if(index>=filterOptions.Count)break;
                var r=ComboRowRect(i);
                if(Equals(filterOptions[index],Filter))Draw(l.ComboRowPush,r.Location);
                canvas.DrawText(BookmarkListView.Caption(filterOptions[index]),r.X+2,r.Y+3,Color.White);
            }
        }
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        switch(e.Kind){
            case UiMouseKind.Down:{
                if(!Bounds.Contains(e.Location))return false;
                if(e.Button!=MouseButtons.Left)return true;
                string? key=HitKey(e.Location);
                // An open drop-down takes the next press: a row or the expand button act; anything else only closes it.
                if(ComboOpen&&(key==null||!(key=="expand"||key.StartsWith("combo:",StringComparison.Ordinal)))){ComboOpen=false;return true;}
                pressed=key;
                switch(key){
                    case "up":ScrollTo(Top-1);break;
                    case "down":ScrollTo(Top+1);break;
                    case "track":Track(e.Y);break;
                    case {} r when r.StartsWith("row:",StringComparison.Ordinal):SelectedId=rows[int.Parse(r[4..])].Id;break;
                }
                return true;
            }
            case UiMouseKind.Move:
                if(pressed=="track")Track(e.Y);
                hover=Bounds.Contains(e.Location)?HitKey(e.Location):null;
                return pressed!=null||Bounds.Contains(e.Location);
            case UiMouseKind.Up:{
                string? was=pressed;pressed=null;
                if(was!=null&&e.Button==MouseButtons.Left&&HitKey(e.Location)==was)Activate(was);
                return was!=null||Bounds.Contains(e.Location);
            }
            case UiMouseKind.Wheel:{
                if(!Bounds.Contains(e.Location))return false;
                int steps=e.Delta/120;if(steps==0&&e.Delta!=0)steps=Math.Sign(e.Delta);
                if(ComboOpen&&ComboListRect.Contains(e.Location))ComboTop=Math.Clamp(ComboTop-steps,0,ComboMaxTop);
                else if(WheelRect.Contains(e.Location)||Rectangle.Union(UpRect,DownRect).Contains(e.Location))ScrollTo(Top-steps);
                return true;
            }
        }
        return false;
    }
    void Track(int y){
        if(MaxTop==0)return;
        var t=TrackRect;
        Top=Math.Clamp((int)Math.Round((double)(y-t.Y)*MaxTop/Math.Max(1,t.Height-1)),0,MaxTop);
    }
    void Activate(string key){
        switch(key){
            case "exit":case "cancel":Hide();return;
            case "confirm":Hide();return;
            case "expand":ComboOpen=!ComboOpen;if(ComboOpen)ComboTop=Math.Clamp(filterOptions.ToList().IndexOf(Filter),0,ComboMaxTop);return;
            case "sort":SortByName=!SortByName;Refilter();return;
            case "random":return;   // 待驗證: no 3.81 packet is known for this button
        }
        if(key.StartsWith("combo:",StringComparison.Ordinal)){
            int i=int.Parse(key[6..]);if(i<filterOptions.Count){Filter=filterOptions[i];Top=0;Refilter();}
            ComboOpen=false;return;
        }
        if(key.StartsWith("color:",StringComparison.Ordinal))SelectedColor=int.Parse(key[6..]);
    }

    /// <summary>Delete (inferred MemoryPlaceKeyBoard key) removes the selected bookmark via the host (T124 SendBookmark).</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible||!e.Down||e.Repeat||e.KeyCode!=Keys.Delete||e.Modifiers!=Keys.None)return false;
        if(Selected is not {DeletePending:false} row||!data.CanEdit||Host==null)return false;
        Host.Delete(data,row.Id);return true;
    }

    public bool IsDragHandle(Point p)=>Visible&&Bounds.Contains(p)&&p.Y-location.Y<Layout.Moveable&&HitKey(p)==null;
    public void MoveTo(Point p)=>location=p;
}
