namespace P06.NativeUi;

// T288（#689）：血盟倉庫紀錄原生窗（TW16 sub_A559D0，全域 dword_153D218）。依據 docs/reference/followups-4.md 第 5 節（FU4§5）
// 與 followups-5.md 第 5 節（FU5§5）。開窗來源：S250/117 非空清單（ClanWarehouseHistory.cs → ClanWarehouseHistoryUi.cs）。
// 已驗證（FU4§5＋本機 uiextra86-c.xml 唯讀核對）：
//   - 版面借 uiextra86 的 HYPER_TEXT_WINDOW：底 1968 (6,9)、框 1969 畫在窗原點（sub_542370）、捲軸 1970 (217,51)、
//     上鈕 2042 (217,37)、下鈕 2040 (217,275)、拉桿 2044（無座標）。XML 的 IMAGE_BTN 1962 原版不用。
//   - 關閉鈕寫死：LImageButton surf 9183 在 (215,9)，回呼 sub_A56AE0＝切換本窗。標題列寫死：LTitleBar (1,1,239,20)，拖曳移動。
//   - 文字（sub_A55FE0）：一次 16 行，第 r 行在 (21, 44+15·r)，白色（調色盤 14）；捲軸位置＝第一行的行號，步長 1。
//     沒有紀錄時在 (21,73) 畫字串 1889。每筆 3 行字＋1 行空白（文字由 ClanWarehouseHistoryText 產生）。
// 本機缺圖（FU5§5 已核對）：surf 9183 在 0-客戶端 Sprite*.idx／Tile.idx 都沒有，所以關閉鈕**不畫**；點擊區仍在 (215,9)，
//   大小取 9183 的圖（有圖時）或 HYPER_TEXT_WINDOW 的 SURF_RENEWAL_BTN_QUIT_X 1962 的圖（推測，只當點擊區，不畫）。
// 推測（標明）：
//   - 窗原點用 XML 的 x／y（建構子的擺放未追）；標題列的 (1,1,239,20) 當成左、上、右、下。
//   - 捲動範圍限在 0..(行數−16)（原版 LScrollBar 範圍 0–299 是否隨筆數調整沒追，FU4§9）；拉桿 2044 沒有座標，不畫。
//   - 底圖不做半透明（原版依透明度設定 dword_1372264 選 sub_6ED600／sub_7D7740，引擎沒有此設定）。
// 不做（待驗證）：本窗在一般熱鍵之前吃哪些鍵（sub_8CA740 呼叫 vtable +40，內容沒展開），所以 Key 一律不吃。

/// <summary>紀錄窗要畫的內容：Lines＝全部畫面行（每筆 3 行＋1 行空白）；EmptyText＝沒有紀錄時畫的字串 1889。</summary>
public sealed record ClanWarehouseHistoryPage(IReadOnlyList<string> Lines,string EmptyText);

public sealed class ClanWarehouseHistoryPanel:IUiMovablePanel,IUiClosablePanel {
    [UiPanel(172)] internal static UiPanelRegistration Registration=>new(ClanWarehouseHistoryPanel.PanelId,ClanWarehouseHistoryPanel.LayoutName,_=>new ClanWarehouseHistoryPanel());   // T288 (#689): sub_A559D0 on HYPER_TEXT_WINDOW (uiextra86)
    /// <summary>引擎內的面板 id（XML 段是 HYPER_TEXT_WINDOW，留給之後的 HyperTextWindow 本身）。</summary>
    public const string PanelId="CLAN_WAREHOUSE_HISTORY_WINDOW";
    public const string LayoutName="uiextra86";
    public const string Section="HYPER_TEXT_WINDOW";
    public const int CloseSurf=9183;
    public static readonly Point CloseOffset=new(215,9);
    public static readonly Rectangle TitleBar=Rectangle.FromLTRB(1,1,239,20);
    public const int VisibleLines=16,LineHeight=15;
    public static readonly Point FirstLine=new(21,44),EmptyAt=new(21,73);
    /// <summary>文字色：調色盤 14（白）。</summary>
    public static Color TextColor=>ChatPalette.Color(ChatFormat.White);
    const string BaseName="SURF_RENEWAL_WIN_HYPER_TEXT_BASE",FrameName="SURF_RENEWAL_WIN_HYPER_TEXT_FRAME",XmlCloseName="SURF_RENEWAL_BTN_QUIT_X",
        ScrollName="SURF_RENEWAL_WIN_SCROLLBAR_LONG",UpName="SURF_RENEWAL_WIN_INC_BUT_OFF",DownName="SURF_RENEWAL_WIN_DEC_BUT_OFF";

    /// <summary>元件：surf、相對窗原點的位置、圖（null＝缺圖不畫，命中範圍為空）。</summary>
    public sealed record Element(string Name,int Surf,Point Offset,UiSurface? Surface) {
        public Rectangle Local=>new(Offset,Surface==null?Size.Empty:new Size(Surface.Width,Surface.Height));
        public bool Drawn=>Surface!=null&&Surface.Width>0&&Surface.Height>0;
    }

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds=>local.IsEmpty?Rectangle.Empty:new(Origin.X+local.X,Origin.Y+local.Y,local.Width,local.Height);
    public bool Visible=>page!=null;
    public Element? Base{get;private set;}
    public Element? Frame{get;private set;}
    public Element? ScrollBar{get;private set;}
    public Element? Up{get;private set;}
    public Element? Down{get;private set;}
    /// <summary>關閉鈕 9183（Surface null＝本機沒有圖，不畫）。</summary>
    public Element? CloseButton{get;private set;}
    /// <summary>關閉鈕點擊區（相對窗原點）；大小見檔頭。</summary>
    public Rectangle CloseLocal{get;private set;}
    public ClanWarehouseHistoryPage? Page=>page;
    /// <summary>捲軸位置＝畫面第一行的行號。</summary>
    public int Scroll{get;private set;}
    public int MaxScroll=>page==null?0:Math.Max(0,page.Lines.Count-VisibleLines);

    ClanWarehouseHistoryPage? page;Rectangle local;Element? pressed;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>離線測試用：surf 由呼叫端提供（合成圖）。</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var window=layout.Root.Child(Section);
        Element Find(string name,bool atOrigin=false){
            var matches=window.Children.Where(c=>c.Attr("surfName")==name).Take(2).ToList();
            if(matches.Count!=1)throw new UiLayoutException(layout.FileName,window.Path+"/@surfName="+name,matches.Count==0?"缺少元件":"元件不唯一");
            var n=matches[0];int[] ids=n.Surfs();
            if(ids.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"surfNumber 空白");
            // 框 1969 沒有座標：FU4§5 已驗證 sub_542370 把它畫在窗原點；其他元件缺座標時 Int() 丟例外，不推定 0。
            var at=atOrigin&&n.Attr("xPos")==null&&n.Attr("yPos")==null?Point.Empty:new Point(n.Int("xPos"),n.Int("yPos"));
            return new(name,ids[0],at,surf(ids[0]));
        }
        Base=Find(BaseName);Frame=Find(FrameName,atOrigin:true);ScrollBar=Find(ScrollName);Up=Find(UpName);Down=Find(DownName);
        var xmlClose=Find(XmlCloseName);
        CloseButton=new("LImageButton",CloseSurf,CloseOffset,surf(CloseSurf));
        var closeSize=CloseButton.Drawn?CloseButton.Local.Size:xmlClose.Drawn?xmlClose.Local.Size:Size.Empty;
        CloseLocal=closeSize.IsEmpty?Rectangle.Empty:new(CloseOffset,closeSize);
        var parts=new[]{Base,Frame,ScrollBar,Up,Down}.Where(e=>e.Drawn).Select(e=>e.Local).Append(TitleBar);
        if(!CloseLocal.IsEmpty)parts=parts.Append(CloseLocal);
        local=parts.Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        Origin=new(window.Int("x"),window.Int("y"));
        page=null;Scroll=0;pressed=null;
    }

    /// <summary>收到新的 S250/117 清單：整份取代、捲回第一行並開窗（原版每次都填整份資料後開窗）。</summary>
    public void Show(ClanWarehouseHistoryPage content){page=content;Scroll=0;pressed=null;}
    /// <summary>關閉鈕／堆疊滿時關窗（本機動作，不送封包）。</summary>
    public void Close(){page=null;pressed=null;}

    public void Update(IUiGameState state){if(!state.InWorld)Close();}

    public Rectangle At(Rectangle r)=>r.IsEmpty?Rectangle.Empty:new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);
    public Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:At(e.Local);
    public Rectangle CloseRect=>At(CloseLocal);
    /// <summary>畫面第 r 行（0..15）的左上角（邏輯座標）。</summary>
    public Point LineAt(int r)=>new(Origin.X+FirstLine.X,Origin.Y+FirstLine.Y+LineHeight*r);
    /// <summary>目前畫出的行（已套用捲動）。</summary>
    public IReadOnlyList<string> VisibleText=>page==null?[]:page.Lines.Skip(Scroll).Take(VisibleLines).ToArray();

    public void Paint(UiCanvas canvas){
        if(page==null)return;
        // 底、框、捲軸、上下鈕；關閉鈕有圖才畫（本機 9183 缺圖）；最後畫字。
        foreach(var e in new[]{Base,Frame,ScrollBar,Up,Down,CloseButton})
            if(e is {Drawn:true})canvas.DrawSurface(e.Surface,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
        var color=TextColor;
        if(page.Lines.Count==0){canvas.DrawText(page.EmptyText,Origin.X+EmptyAt.X,Origin.Y+EmptyAt.Y,color);return;}
        var lines=VisibleText;
        for(int r=0;r<lines.Count;r++){var p=LineAt(r);canvas.DrawText(lines[r],p.X,p.Y,color);}
    }

    void ScrollBy(int delta)=>Scroll=Math.Clamp(Scroll+delta,0,MaxScroll);

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(page==null)return false;
        bool inside=Bounds.Contains(e.Location);
        switch(e.Kind){
            case UiMouseKind.Wheel:
                if(!inside)return false;
                if(e.Delta!=0)ScrollBy(-Math.Sign(e.Delta));
                return true;
            case UiMouseKind.Down:
                if(e.Button!=MouseButtons.Left)return inside;
                pressed=CloseRect.Contains(e.Location)?CloseButton:At(Up).Contains(e.Location)?Up:At(Down).Contains(e.Location)?Down:null;
                return inside;
            case UiMouseKind.Up:{
                var was=pressed;pressed=null;
                if(was==null)return inside;
                if(was==CloseButton&&CloseRect.Contains(e.Location))Close();
                else if(was==Up&&At(Up).Contains(e.Location))ScrollBy(-1);
                else if(was==Down&&At(Down).Contains(e.Location))ScrollBy(1);
                return true;
            }
            default:return inside||pressed!=null;
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;

    /// <summary>標題列 (1,1)–(239,20)，關閉鈕除外。</summary>
    public bool IsDragHandle(Point location)=>page!=null&&At(TitleBar).Contains(location)&&!CloseRect.Contains(location);
    public void MoveTo(Point location){Origin=new(location.X-local.X,location.Y-local.Y);}
}
