namespace P06.NativeUi;

// R3-C（T279／#672）：原生料理窗 COOK_BOOK_WINDOW（uiextra86-c.xml；WIN2§7 W2-F，gap-matrix 第 4.2 節 CCookWindow）。
// 開窗來源：S250 52（Cooking.cs，第二欄 1＝開／切換，value＋1＝料理書等級）。
// 已驗證（WIN2§7＋XML）：底 2254 (6,8)、框 2255（無座標）、關閉 X 1962 (179,15)、捲軸 2039 (171,55)、上鈕 2042 (171,41)、
//   下鈕 2040 (171,222)、拉桿 2044（無座標）、等級圖 SURF_RENEWAL_IMG_COOK_BOOK_1..11＝2545–2555 都在 (116,257)、量表 2256 (35,251)。
// 推測（標明）：11 張等級圖一次只畫一張，第 value＋1 張（value 超出 0–10 時不畫）；窗原點用 XML 的 x／y（建構子 sub_A5AAC0 的擺放未查）；
//   框 2255、拉桿 2044 在 XML 沒有座標，比照 TRADE_WINDOW／QUIT_WINDOW 不畫（不推定 0,0）；量表照 XML 靜態畫出，填充比例的來源未查。
// 不做（待驗證，不以假資料冒充）：料理清單內容與捲動、量表數值、製作鈕（3.81 製作走 C_USE_ITEM，不經本窗）、拖曳、Esc 關窗。

/// <summary>料理窗目前要顯示的內容。Revision 每次 S250 52 或關窗都會變。</summary>
public sealed record CookBookView(long Revision,byte Value,int Level);

/// <summary>料理窗資料來源（P06.WorldCookBookSource 轉接 WorldSession）。</summary>
public interface ICookBookSource {
    /// <summary>null＝不開窗。</summary>
    CookBookView? Current {get;}
    /// <summary>關閉 X：只關 revision 這一次開的窗（本機動作，不送封包）。</summary>
    void Close(long revision);
}

public sealed class CookBookPanel:IUiPanel {
    [UiPanel(170)] internal static UiPanelRegistration Registration=>new(CookBookPanel.PanelId,CookBookPanel.LayoutName,_=>new CookBookPanel());   // R3-C (#672): COOK_BOOK_WINDOW (uiextra86)
    public const string PanelId="COOK_BOOK_WINDOW";
    public const string LayoutName="uiextra86";
    public const int LevelImages=11;
    const string BaseName="SURF_RENEWAL_WIN_COOK_BOOK_BASE",CloseName="SURF_RENEWAL_BTN_QUIT_X",ScrollName="SURF_RENEWAL_WIN_SCROLLBAR",
        UpName="SURF_RENEWAL_WIN_INC_BUT_OFF",DownName="SURF_RENEWAL_WIN_DEC_BUT_OFF",GaugeName="SURF_RENEWAL_IMG_COOK_BOOK_GUAGE",LevelPrefix="SURF_RENEWAL_IMG_COOK_BOOK_";

    /// <summary>XML 的一個元件：第一個 surf、相對窗原點的位置、圖（null＝缺圖不畫，命中範圍為空）。</summary>
    public sealed record Element(string Name,int Surf,Point Offset,UiSurface? Surface) {
        public Rectangle Local=>new(Offset,Surface==null?Size.Empty:new Size(Surface.Width,Surface.Height));
        public bool Drawn=>Surface!=null&&Surface.Width>0&&Surface.Height>0;
    }

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds{get;private set;}
    public bool Visible=>view!=null;
    public ICookBookSource? Source{get;set;}
    public Element? Base{get;private set;}
    public Element? CloseButton{get;private set;}
    public Element? ScrollBar{get;private set;}
    public Element? Up{get;private set;}
    public Element? Down{get;private set;}
    public Element? Gauge{get;private set;}
    /// <summary>等級圖 1..11（索引 0＝SURF_RENEWAL_IMG_COOK_BOOK_1）。</summary>
    public IReadOnlyList<Element> Levels{get;private set;}=[];
    public CookBookView? View=>view;
    /// <summary>目前畫的等級圖（推測：第 value＋1 張）；value 超出範圍時 null。</summary>
    public Element? LevelImage=>view!=null&&view.Value<Levels.Count?Levels[view.Value]:null;
    CookBookView? view;long closedRevision=-1;Element? pressed;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>離線測試用：surf 由呼叫端提供（合成圖）。</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var window=layout.Root.Child(PanelId);
        Origin=new(window.Int("x"),window.Int("y"));
        Element Find(string name){
            var matches=window.Children.Where(c=>c.Attr("surfName")==name).Take(2).ToList();
            if(matches.Count!=1)throw new UiLayoutException(layout.FileName,window.Path+"/@surfName="+name,matches.Count==0?"缺少元件":"元件不唯一");
            var n=matches[0];int[] ids=n.Surfs();
            if(ids.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"surfNumber 空白");
            // 只用有座標的元件；缺 xPos／yPos 時 Int() 丟例外，不推定 0。
            return new(name,ids[0],new(n.Int("xPos"),n.Int("yPos")),surf(ids[0]));
        }
        Base=Find(BaseName);CloseButton=Find(CloseName);ScrollBar=Find(ScrollName);Up=Find(UpName);Down=Find(DownName);Gauge=Find(GaugeName);
        Levels=Enumerable.Range(1,LevelImages).Select(i=>Find(LevelPrefix+i)).ToArray();
        // 已知元件之外的同名前綴（例如 _12）視為版面與本面板假設不符。
        if(window.Children.Any(c=>c.Attr("surfName") is {} s&&s.StartsWith(LevelPrefix,StringComparison.Ordinal)&&s!=GaugeName&&!Levels.Any(l=>l.Name==s)))
            throw new UiLayoutException(layout.FileName,window.Path,$"等級圖不是 {LevelImages} 張");
        var union=Fixed.Concat(Levels).Where(e=>e.Drawn).Select(e=>e.Local).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        Bounds=union.IsEmpty?Rectangle.Empty:new(Origin.X+union.X,Origin.Y+union.Y,union.Width,union.Height);
    }
    IEnumerable<Element> Fixed=>new[]{Base,ScrollBar,Up,Down,Gauge,CloseButton}.OfType<Element>();

    public void Update(IUiGameState state){
        var v=state.InWorld?Source?.Current:null;
        if(v!=null&&v.Revision==closedRevision)v=null;
        if(v==null)pressed=null;
        view=v;
    }

    public Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:new(Origin.X+e.Offset.X,Origin.Y+e.Offset.Y,e.Local.Width,e.Local.Height);

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        // 底、捲軸、上下鈕、量表、等級圖、關閉 X（X 最後畫，避免被底圖蓋住）。
        foreach(var e in new[]{Base,ScrollBar,Up,Down,Gauge,LevelImage,CloseButton})
            if(e?.Surface!=null)canvas.DrawSurface(e.Surface,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
    }

    void Close(){
        if(view==null)return;
        long revision=view.Revision;closedRevision=revision;view=null;pressed=null;Source?.Close(revision);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        if(e.Kind==UiMouseKind.Wheel)return Bounds.Contains(e.Location);
        if(e.Button!=MouseButtons.Left)return Bounds.Contains(e.Location)||pressed!=null;
        bool onClose=At(CloseButton).Contains(e.Location);
        if(e.Kind==UiMouseKind.Down){pressed=onClose?CloseButton:null;return Bounds.Contains(e.Location);}
        if(e.Kind==UiMouseKind.Up){
            var was=pressed;pressed=null;
            if(was!=null&&onClose)Close();
            return true;
        }
        return Bounds.Contains(e.Location);
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
