namespace P06.NativeUi;

// R9-P（T376／#860）：釣魚取得清單窗 FishObtainUI（FU9 §6.1／§6.2／§14.3、gap-matrix 15.11）。
// R10-A（T382／#888，FU10§1）：清單照原版接上。
// 已驗證（靜態）：
//   - 和 UserObtainUI 是同一個建構子 sub_C03330 建的兩個實例（dword_1540860＝FishObtainUI，0xB5791B）；CFishingUI 的
//     FishingNet 鈕（sub_E65B00）不送封包，只開關這個窗；標題是 "[ %s ]"＋字串 3674。
//   - 事件：CloseFishWindow（關閉鈕）、FishItemListScroll（捲軸）、FishKeyboard（Esc 關窗）。
//   - FishObtainUI.xml 本機 0-客戶端 24 個 idx 都沒有；參考客戶端 5 套都有且內容相同（FU9 §6.2；R10-A 以 PakViewer 從
//     kor25 Tile.idx 重新解出核對：原檔 SHA-256 b66b93d28c830603cc5063bbf88e061af5e2fbe845892c2a24d80fed8b43d55c、
//     解密後 01d94c4464e3d798ee87af362d4e838555530f9dd3635d26f47323ab1045096a，6,370 byte），版面與 UserObtainUI.xml 相同：
//     根 FishObtainUI、視窗 UserObtain_Window、底 5194／框 5195、Moveable 20、關閉鈕 (225,15) 4429／4430、Total_Label (30,20)
//     String 3674、ObtainNewItem_List (18,37) 194×250 Gap 1，7 個 CheckButton 列（高 35；ItemIcon 34×35 → Gap 1 →
//     ItemInfo_List Y 2：名稱 (4,4) 寬 140、數量 (130,5) 寬 19 靠右、底色黑）；捲軸 ItemsList_Scrollbar 在 WheelControl
//     (18,37) 內 (199,0)：底圖 5196 (0,14)、上 4178／4179 (0,0)、下 4180／4181 (0,236)、拖曳鈕 4182／4183 (0,16)，
//     ScrollStart 16、ScrollHeight 224。執行期經 UiResources.Shared.Xml（本機 Tile.pak → 參考客戶端 Tile.pak）讀取，不複製進
//     repo；讀不到 → Unavailable，FishingNet 只記錄不開窗。
//   - 清單內容（FU10§1.3，sub_E65050）：自己的背包裡 desc id 在釣魚物品表（unk_1542864，FishObtainTable.cs）內的物品。
//     表只由 S5／S15 尾段 +10 旗標 bit 3 的物品填、世界重設才清；3.81 尾段只有 10 byte → 表恆空 → 清單恆空（只畫捲軸）。
// 引擎做法（推測，標明）：原版在自己的動作 71 時重填、S57／數量更新時同步；這裡每個畫面直接從目前背包過濾，穩定後結果相同。
//   列的數量欄畫背包數量（sub_C04B00「依數量差更新」的確切顯示沒查，真機再確認）。捲軸每格一列、拖曳鈕位置依比例（同 UserObtainPanel）。
// 列的 CheckButton 沒有回呼（ItemsListClick 不在這個類別處理，FU9 §6.1）→ 不做選取。Desc_Tooltip Opacity 0 → 不接。
// 字串 3674：本機 string-c.tbl 只有 3484 筆（text_tables.py 讀出），沒有 3674 → 標題不畫（不用替代字）；表裡有時才畫。
// 推測（標明，真機再確認）：開窗位置放在釣魚窗右側（放不下放左側，同 UserObtainPanel.Beside）；結束釣魚（釣魚窗的狀態變成
//   非釣魚中）或離開世界時關窗。
// --classic-ui 時沒有原生面板；舊式釣魚 HUD 不變。

public sealed class FishObtainPanel(UiResources? resources=null,Func<FishingPanel?>? fishing=null,Func<IReadOnlyList<int>?>? table=null,ItemIconSource? icons=null):IUiMovablePanel,IUiClosablePanel {
    [UiPanel(196)] internal static UiPanelRegistration Registration=>new(PanelId,FishingPanel.HostLayout,root=>new FishObtainPanel(root.Resources,()=>root.Find(FishingPanel.PanelId) as FishingPanel,()=>FishObtainTable.Latest,ItemIconSource.FromRoot(root)));   // R9-P (#860) / R10-A (#888): FishObtainUI.xml from the reference client, loaded inside Load
    public const string PanelId="FishObtainUI";
    public const string LayoutName="FishObtainUI",RootName="FishObtainUI",WindowName="UserObtain_Window";
    public const string CloseEvent="CloseFishWindow",ScrollEvent="FishItemListScroll";
    /// <summary>標題字串（sub_E65B00："[ %s ]"＋3674）。</summary>
    public const int TitleStringId=3674;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    /// <summary>Why the window cannot open; null when usable.</summary>
    public string? Unavailable{get;private set;}="not loaded";
    public bool Available=>Unavailable==null;
    public string? LayoutFile{get;private set;}
    public Point Origin{get;private set;}
    public Size Size{get;private set;}
    public Rectangle Bounds=>open&&!Size.IsEmpty?new(Origin,Size):Rectangle.Empty;
    public bool Visible=>open;
    public UiSurface? BaseSurface{get;private set;}
    public UiSurface? FrameSurface{get;private set;}
    public UserObtainPanel.Button? CloseButton{get;private set;}
    public UserObtainPanel.Button? Up{get;private set;}
    public UserObtainPanel.Button? Down{get;private set;}
    public UserObtainPanel.Button? Thumb{get;private set;}
    public UiSurface? ScrollBack{get;private set;}
    public Point ScrollBackAt{get;private set;}
    public int ScrollStart{get;private set;}
    public int ScrollHeight{get;private set;}
    public Point TitleAt{get;private set;}
    /// <summary>XML Total_Label 的 String（應為 3674）。</summary>
    public int? TitleString{get;private set;}
    /// <summary>ObtainNewItem_List 的位置（視窗內）。</summary>
    public Rectangle ListArea{get;private set;}
    /// <summary>清單列的幾何（視窗內），照 XML 的 CheckButton。</summary>
    public IReadOnlyList<UserObtainPanel.RowGeometry> Rows{get;private set;}=[];
    public int Moveable{get;private set;}
    public IReadOnlyList<string> Missing{get;private set;}=[];
    /// <summary>刻意不建的元素與理由。</summary>
    public IReadOnlyList<string> Skipped{get;private set;}=[];
    public UserObtainPanel.Button? Pressed{get;private set;}
    /// <summary>目前清單：背包中 desc id 在釣魚物品表內的物品（表空或窗關著 → 空）。</summary>
    public IReadOnlyList<InventoryItem> Entries=>entries;
    /// <summary>第一個顯示的物品索引（捲軸位置）。</summary>
    public int Scroll{get;private set;}
    public int MaxScroll=>Math.Max(0,entries.Count-Rows.Count);
    public ItemIconSource Icons{get;set;}=icons??ItemIconSource.None;

    bool open,moved;Point scrollAt;
    IReadOnlyList<InventoryItem> entries=[];

    public void Load(UiLayout host,SurfCatalog surfs){
        Clear();
        if(resources==null){Unavailable="沒有 UiResources";return;}
        UiLayout layout;
        try{layout=resources.Xml.Load(LayoutName);}
        catch(IOException ex){Unavailable=ex.Message;UiResourceLog.Write($"fish-obtain-ui 無法載入 {LayoutName}，FishingNet 不開窗：{ex.Message}");return;}
        try{Load(layout,surfs.Get);}
        catch(IOException ex){Clear();Unavailable=ex.Message;UiResourceLog.Write($"fish-obtain-ui {layout.FileName} 版面不符，停用：{ex.Message}");return;}
        if(Missing.Count>0)UiResourceLog.Write($"fish-obtain-ui 缺圖不畫：{string.Join("、",Missing)}");
        if(!Available)UiResourceLog.Write($"fish-obtain-ui 停用：{Unavailable}");
    }

    void Clear(){
        open=moved=false;Pressed=null;LayoutFile=null;Origin=Point.Empty;Size=Size.Empty;BaseSurface=FrameSurface=null;CloseButton=null;
        Up=Down=Thumb=null;ScrollBack=null;ScrollBackAt=Point.Empty;ScrollStart=ScrollHeight=0;scrollAt=Point.Empty;Rows=[];entries=[];Scroll=0;
        TitleAt=Point.Empty;TitleString=null;ListArea=Rectangle.Empty;Moveable=0;Missing=[];Skipped=[];Unavailable="not loaded";
    }

    /// <summary>Offline checks: surfs come from the caller (synthetic). Throws UiLayoutException on a layout that does not fit.</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        Clear();
        var root=layout.Root;
        if(root.Name!=RootName)throw new UiLayoutException(layout.FileName,root.Path,$"根節點應為 {RootName}");
        var window=root.Child("Window");
        if(window.Attr("Name")!=WindowName)throw new UiLayoutException(layout.FileName,window.Path,$"Window Name 不是 {WindowName}");
        var missing=new List<string>();var skipped=new List<string>();
        UiSurface? S(UiNode n,string attr){
            if(FishingPanel.SurfAttr(n,attr) is not {} r)return null;
            var s=surf(r.Id);
            if(s==null)missing.Add($"surf {r.Id} ({n.Path}/@{attr})");
            return s;
        }
        UserObtainPanel.Button B(UiNode n,Point offset)=>new(n.RequiredAttr("Name"),new(offset.X+n.Int("X"),offset.Y+n.Int("Y")),S(n,"BaseSurf"),S(n,"PushSurf"));
        BaseSurface=S(window,"BaseSurf");FrameSurface=S(window,"FrameSurf");Moveable=window.TryInt("Moveable")??0;
        var close=window.ChildrenNamed("Button").FirstOrDefault(b=>b.Attr("Name")=="CloseWindow_Button");
        CloseButton=close==null?null:B(close,Point.Empty);
        if(close!=null&&close.Attr("MouseEvent")!=CloseEvent)skipped.Add(close.Path+$"（事件 {close.Attr("MouseEvent")} 不是 {CloseEvent}）");
        var total=window.ChildrenNamed("Label").FirstOrDefault(l=>l.Attr("Name")=="Total_Label");
        TitleAt=total==null?Point.Empty:new(total.TryInt("X")??0,total.TryInt("Y")??0);
        TitleString=total?.Attr("String") is {} sid&&int.TryParse(new string(sid.Trim().TakeWhile(char.IsAsciiDigit).ToArray()),out int id)?id:null;
        var list=window.ChildrenNamed("List").FirstOrDefault(l=>l.Attr("Name")=="ObtainNewItem_List");
        var rows=new List<UserObtainPanel.RowGeometry>();
        if(list!=null){
            int lx=list.TryInt("X")??0,ly=list.TryInt("Y")??0,gap=list.TryInt("Gap")??0;
            ListArea=new(lx,ly,list.TryInt("Width")??0,list.TryInt("Height")??0);
            int y=ly;
            foreach(var row in list.ChildrenNamed("CheckButton")){
                int h=row.TryInt("Height")??0,w=row.TryInt("Width")??0;
                // 列結構和 UserObtainPanel 相同：Item_List → ItemIcon、ItemInfo_List（名稱、數量兩個 Label）。缺的列不建、記下理由。
                var inner=row.ChildrenNamed("List").FirstOrDefault();
                var icon=inner?.ChildrenNamed("ItemIcon").FirstOrDefault();
                var info=inner?.ChildrenNamed("List").FirstOrDefault();
                var labels=info?.ChildrenNamed("Label").ToList()??[];
                if(inner==null||icon==null||info==null||labels.Count<2){skipped.Add(row.Path+"（列缺 Item_List／ItemIcon／名稱與數量 Label，不建）");y+=h+gap;continue;}
                int iw=icon.TryInt("Width")??0,ih=icon.TryInt("Height")??0,ig=inner.TryInt("Gap")??0;
                int ix=lx+iw+ig,iy=y+(info.TryInt("Y")??0);
                var name=labels[0];var count=labels[1];
                rows.Add(new(new(lx,y,w,h),new(lx,y,iw,ih),new(ix+(name.TryInt("X")??0),iy+(name.TryInt("Y")??0)),name.TryInt("Width")??0,
                    new(ix+(count.TryInt("X")??0),iy+(count.TryInt("Y")??0),count.TryInt("Width")??0,count.TryInt("Height")??0)));
                y+=h+gap;
            }
        }
        Rows=rows;
        if(window.ChildrenNamed("WheelControl").FirstOrDefault() is {} wheel){
            if(wheel.ChildrenNamed("ScrollBar").FirstOrDefault() is {} bar){
                scrollAt=new((wheel.TryInt("X")??0)+(bar.TryInt("X")??0),(wheel.TryInt("Y")??0)+(bar.TryInt("Y")??0));
                ScrollStart=bar.TryInt("ScrollStart")??0;ScrollHeight=bar.TryInt("ScrollHeight")??0;
                if(bar.Attr("MouseEvent") is {} ev&&ev!=ScrollEvent)skipped.Add(bar.Path+$"（事件 {ev} 不是 {ScrollEvent}）");
                if(bar.ChildrenNamed("Image").FirstOrDefault() is {} back){ScrollBack=S(back,"Surf");ScrollBackAt=new(scrollAt.X+(back.TryInt("X")??0),scrollAt.Y+(back.TryInt("Y")??0));}
                foreach(var b in bar.ChildrenNamed("Button")){
                    var btn=B(b,scrollAt);
                    switch(btn.Name){case "Up_Button":Up=btn;break;case "Down_Button":Down=btn;break;case "Move_Button":Thumb=btn;break;}
                }
            }else skipped.Add(wheel.Path+"（沒有 ScrollBar）");
        }
        var rects=new[]{BaseSurface,FrameSurface}.OfType<UiSurface>().Select(s=>new Size(s.Width,s.Height)).ToList();
        Size=rects.Count==0?Size.Empty:new(rects.Max(s=>s.Width),rects.Max(s=>s.Height));
        LayoutFile=layout.FileName;Origin=new(window.TryInt("X")??0,window.TryInt("Y")??0);
        Missing=missing;Skipped=skipped;
        Unavailable=Size.IsEmpty?$"{layout.FileName}: 視窗底圖與外框都缺，無法定出視窗範圍（不用替代圖）":null;
    }

    /// <summary>"[ %s ]"＋字串 3674；字串表沒有這一筆 → null（不畫，不用替代字）。</summary>
    public string? Title{get{
        int id=TitleString??TitleStringId;
        if(ServerMessageText.Table is not {} t||id>=t.Count)return null;
        var s=ServerMessageText.DisplayText(t[id]).Trim();
        return s.Length==0?null:"[ "+s+" ]";
    }}

    /// <summary>sub_E65050 的過濾：背包（null＝還沒收到）中 desc id 在表內的物品，照背包順序。</summary>
    public static IReadOnlyList<InventoryItem> Filter(IReadOnlyList<InventoryItem>? items,IReadOnlyList<int>? table){
        if(items==null||table==null||table.Count==0)return [];
        return items.Where(i=>i.Count>0&&FishObtainTable.Contains(table,i.DescriptionId)).ToArray();
    }

    /// <summary>FishingNet：開著就關、關著就開。anchor＝釣魚窗範圍（擺位用，推測）。回傳開窗後的 Visible。</summary>
    public bool Toggle(Rectangle anchor){
        if(open){Close();return false;}
        return Open(anchor);
    }
    public bool Open(Rectangle anchor){
        if(!Available)return false;
        open=true;Pressed=null;Scroll=0;
        if(!moved&&!anchor.IsEmpty)Origin=UserObtainPanel.Beside(anchor,Size);
        return true;
    }
    public void Close(){open=false;moved=false;Pressed=null;Scroll=0;entries=[];}

    public void Update(IUiGameState state){
        if(!open)return;
        if(!state.InWorld){Close();return;}
        if(fishing?.Invoke() is {} f&&f.State is not {Active:true}){Close();return;}   // 推測：結束釣魚時關窗
        entries=Filter(LegacyWindowPanel.FindState<IUiItemWindowState>(state)?.Items,table?.Invoke());
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
    }

    public bool IsDragHandle(Point location)=>open&&Moveable>0&&Bounds.Contains(location)&&location.Y<Origin.Y+Moveable&&ButtonAt(location)==null;
    public void MoveTo(Point location){moved=true;Origin=location;}

    Rectangle At(Rectangle local)=>local.IsEmpty?Rectangle.Empty:new(Origin.X+local.X,Origin.Y+local.Y,local.Width,local.Height);
    IEnumerable<UserObtainPanel.Button> Buttons=>new[]{CloseButton,Up,Down}.OfType<UserObtainPanel.Button>();
    public UserObtainPanel.Button? ButtonAt(Point p)=>Buttons.FirstOrDefault(b=>At(b.Local).Contains(p));
    /// <summary>拖曳鈕位置（視窗內）：ScrollStart＋(ScrollHeight − 鈕高) × Scroll／MaxScroll（推測，同 UserObtainPanel）。</summary>
    public Point ThumbAt(){
        if(Thumb==null)return Point.Empty;
        int h=Thumb.Base?.Height??0,range=Math.Max(0,ScrollHeight-h);
        return new(Thumb.At.X,scrollAt.Y+ScrollStart+(MaxScroll==0?0:range*Scroll/MaxScroll));
    }
    public void ScrollBy(int delta)=>Scroll=Math.Clamp(Scroll+delta,0,MaxScroll);

    public void Paint(UiCanvas canvas){
        if(!open)return;
        canvas.DrawSurface(BaseSurface,Origin.X,Origin.Y);canvas.DrawSurface(FrameSurface,Origin.X,Origin.Y);
        if(Title is {} title)canvas.DrawText(title,Origin.X+TitleAt.X,Origin.Y+TitleAt.Y,Color.White);
        for(int i=0;i<Rows.Count&&Scroll+i<entries.Count;i++){
            var e=entries[Scroll+i];var g=Rows[i];
            ItemIconSource.Draw(canvas,Icons.For(e),new(Origin.X+g.Icon.X,Origin.Y+g.Icon.Y));
            string name=ItemText.Resolve(e.Name);
            if(name.Length>0)canvas.DrawText(name,Origin.X+g.Name.X,Origin.Y+g.Name.Y,Color.White);
            string count=e.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);int w=UiText.Measure(canvas.Graphics,count).Width;
            canvas.DrawText(count,Origin.X+g.Count.Right-w,Origin.Y+g.Count.Y,Color.White,Color.Black);
        }
        canvas.DrawSurface(ScrollBack,Origin.X+ScrollBackAt.X,Origin.Y+ScrollBackAt.Y);
        foreach(var b in Buttons)canvas.DrawSurface(Pressed==b?b.Push??b.Base:b.Base,Origin.X+b.At.X,Origin.Y+b.At.Y);
        if(Thumb!=null){var t=ThumbAt();canvas.DrawSurface(Thumb.Base,Origin.X+t.X,Origin.Y+t.Y);}
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!open)return false;
        bool inside=Bounds.Contains(e.Location);
        switch(e.Kind){
            case UiMouseKind.Wheel:
                if(!inside)return false;
                ScrollBy(e.Delta>0?-1:e.Delta<0?1:0);return true;
            case UiMouseKind.Down:
                if(e.Button==MouseButtons.Left)Pressed=ButtonAt(e.Location);
                return inside;
            case UiMouseKind.Up:{
                var was=Pressed;Pressed=null;
                if(was!=null&&ButtonAt(e.Location)==was){
                    if(was==CloseButton)Close();
                    else if(was==Up)ScrollBy(-1);
                    else if(was==Down)ScrollBy(1);
                }
                return inside||was!=null;
            }
            default:return inside||Pressed!=null;
        }
    }

    /// <summary>FishKeyboard：Esc 關窗（已驗證）。</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!open||!e.Down||e.KeyCode!=Keys.Escape)return false;
        Close();return true;
    }
}
