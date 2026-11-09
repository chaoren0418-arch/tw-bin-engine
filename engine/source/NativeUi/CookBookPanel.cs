namespace P06.NativeUi;

// R3-C（T279／#672）：原生料理窗 COOK_BOOK_WINDOW（uiextra86-c.xml；WIN2§7 W2-F，gap-matrix 第 4.2 節 CCookWindow）。
// 開窗來源：S250 52（Cooking.cs，第二欄 1＝開／切換，value＋1＝料理書等級）。
// 已驗證（WIN2§7＋XML）：底 2254 (6,8)、框 2255（無座標）、關閉 X 1962 (179,15)、捲軸 2039 (171,55)、上鈕 2042 (171,41)、
//   下鈕 2040 (171,222)、拉桿 2044（無座標）、等級圖 SURF_RENEWAL_IMG_COOK_BOOK_1..11＝2545–2555 都在 (116,257)、量表 2256 (35,251)。
// 推測（標明）：11 張等級圖一次只畫一張，第 value＋1 張（value 超出 0–10 時不畫）；窗原點用 XML 的 x／y（建構子 sub_A5AAC0 的擺放未查）；
//   框 2255、拉桿 2044 在 XML 沒有座標，比照 TRADE_WINDOW／QUIT_WINDOW 不畫（不推定 0,0）。
// R4-D（T295／#707）：
//   - 料理清單：資料來自原客戶端 recipe.tbl（Cooking.cs CookRecipeTable，名稱經道具名稱表代換）。
//     推測：清單區＝底圖左緣＋8 到捲軸左緣−2、上鈕頂到下鈕底；每列 15 px（比照 sub_A55FE0 的行距）；白字，選取列黃字。
//   - 捲動：上鈕／下鈕／滾輪一次一列，範圍 0..(筆數−可見列數)。拉桿 2044 沒有座標，不畫。
//   - 選取與製作：點一下選取；在選取列上再點一下（或雙擊）→ 對料理書送 C_USE_ITEM(164) D C(1) C(編號)（由 Source 轉給 LoginConnection.CookRecipe）。
//     原版的製作操作方式未查（推測），材料文字不在本窗畫（版面沒有位置），WinForms 暫行清單有。
//   - 量表數值：推測填充比例＝等級／11（由左往右裁切 2256）；原版量表的資料來源未查。
//   - IUiClosablePanel：4 層堆疊滿時被擠出 → 與關閉 X 相同（本機關窗，不送封包）。
// 不做（待驗證）：拖曳、Esc 關窗。

/// <summary>料理窗的一道食譜：Index＝C_USE_ITEM 送的編號；Name、Materials 已代換成顯示文字。</summary>
public sealed record CookBookRecipe(int Index,string Name,string Materials);

/// <summary>料理窗目前要顯示的內容。Revision 每次 S250 52 或關窗都會變。Recipes null＝沒有清單（表讀不到）。</summary>
public sealed record CookBookView(long Revision,byte Value,int Level,IReadOnlyList<CookBookRecipe>? Recipes=null);

/// <summary>料理窗資料來源（P06.WorldCookBookSource 轉接 WorldSession）。</summary>
public interface ICookBookSource {
    /// <summary>null＝不開窗。</summary>
    CookBookView? Current {get;}
    /// <summary>關閉 X：只關 revision 這一次開的窗（本機動作，不送封包）。</summary>
    void Close(long revision);
    /// <summary>製作 revision 這一次開的窗裡編號 recipe 的料理；回 true＝已交出請求（送不送由道具使用閘門決定）。</summary>
    bool Cook(long revision,int recipe)=>false;
}

public sealed class CookBookPanel:IUiClosablePanel {
    [UiPanel(170)] internal static UiPanelRegistration Registration=>new(CookBookPanel.PanelId,CookBookPanel.LayoutName,_=>new CookBookPanel());   // R3-C (#672): COOK_BOOK_WINDOW (uiextra86)
    public const string PanelId="COOK_BOOK_WINDOW";
    public const string LayoutName="uiextra86";
    public const int LevelImages=11;
    /// <summary>推測：每列高度（px）與清單區左右留白。</summary>
    public const int RowHeight=15,ListInsetLeft=8,ListInsetRight=2;
    const string BaseName="SURF_RENEWAL_WIN_COOK_BOOK_BASE",CloseName="SURF_RENEWAL_BTN_QUIT_X",ScrollName="SURF_RENEWAL_WIN_SCROLLBAR",
        UpName="SURF_RENEWAL_WIN_INC_BUT_OFF",DownName="SURF_RENEWAL_WIN_DEC_BUT_OFF",GaugeName="SURF_RENEWAL_IMG_COOK_BOOK_GUAGE",LevelPrefix="SURF_RENEWAL_IMG_COOK_BOOK_";
    public static Color TextColor=>ChatPalette.Color(ChatFormat.White);
    public static readonly Color SelectedColor=Color.Yellow;

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
    /// <summary>清單區（相對窗原點，推測，見檔頭）。</summary>
    public Rectangle ListLocal{get;private set;}
    public int VisibleRows=>ListLocal.Height/RowHeight;
    public IReadOnlyList<CookBookRecipe> Recipes=>view?.Recipes??[];
    /// <summary>捲軸位置＝畫面第一列的清單索引。</summary>
    public int Scroll{get;private set;}
    public int MaxScroll=>Math.Max(0,Recipes.Count-VisibleRows);
    /// <summary>選取的清單索引（-1＝沒選）。</summary>
    public int Selected{get;private set;}=-1;
    public CookBookRecipe? SelectedRecipe=>Selected>=0&&Selected<Recipes.Count?Recipes[Selected]:null;
    /// <summary>推測：量表填充比例＝等級／11（0–1）。</summary>
    public double GaugeFill=>view==null?0:Math.Clamp(view.Level/(double)LevelImages,0,1);
    CookBookView? view;long closedRevision=-1;Element? pressed;int pressedRow=-1;

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
        int left=Base.Offset.X+ListInsetLeft,right=ScrollBar.Offset.X-ListInsetRight,top=Up.Offset.Y,bottom=Down.Offset.Y+Down.Local.Height;
        ListLocal=right>left&&bottom>top?Rectangle.FromLTRB(left,top,right,bottom):Rectangle.Empty;
        view=null;Scroll=0;Selected=-1;pressed=null;pressedRow=-1;
    }
    IEnumerable<Element> Fixed=>new[]{Base,ScrollBar,Up,Down,Gauge,CloseButton}.OfType<Element>();

    public void Update(IUiGameState state){
        var v=state.InWorld?Source?.Current:null;
        if(v!=null&&v.Revision==closedRevision)v=null;
        // 新的一次開窗（或換等級）：捲回頂端、清掉選取。
        if(v==null||view==null||v.Revision!=view.Revision){Scroll=0;Selected=-1;pressed=null;pressedRow=-1;}
        view=v;
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
        if(Selected>=Recipes.Count)Selected=-1;
    }

    public Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:new(Origin.X+e.Offset.X,Origin.Y+e.Offset.Y,e.Local.Width,e.Local.Height);
    /// <summary>畫面第 r 列（0..VisibleRows−1）的範圍（邏輯座標）。</summary>
    public Rectangle RowRect(int r)=>new(Origin.X+ListLocal.X,Origin.Y+ListLocal.Y+RowHeight*r,ListLocal.Width,RowHeight);
    /// <summary>點到的清單索引；不在任何一列上時 -1。</summary>
    public int RowAt(Point p){
        if(ListLocal.IsEmpty)return -1;
        for(int r=0;r<VisibleRows;r++){int i=Scroll+r;if(i>=Recipes.Count)break;if(RowRect(r).Contains(p))return i;}
        return -1;
    }
    /// <summary>目前畫出的列（已套用捲動）。</summary>
    public IReadOnlyList<CookBookRecipe> VisibleRecipes=>Recipes.Skip(Scroll).Take(VisibleRows).ToArray();

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        // 底、捲軸、上下鈕、量表（依數值裁切）、等級圖、清單文字、關閉 X（X 最後畫，避免被底圖蓋住）。
        foreach(var e in new[]{Base,ScrollBar,Up,Down})
            if(e?.Surface!=null)canvas.DrawSurface(e.Surface,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
        if(Gauge is {Drawn:true} g){
            int w=(int)Math.Round(g.Surface!.Width*GaugeFill);
            if(w>0)canvas.DrawSurface(g.Surface,new Rectangle(0,0,w,g.Surface.Height),Origin.X+g.Offset.X,Origin.Y+g.Offset.Y);
        }
        if(LevelImage?.Surface is {} level)canvas.DrawSurface(level,Origin.X+LevelImage.Offset.X,Origin.Y+LevelImage.Offset.Y);
        var rows=VisibleRecipes;
        for(int r=0;r<rows.Count;r++){var rect=RowRect(r);canvas.DrawText(rows[r].Name,rect.X,rect.Y,Scroll+r==Selected?SelectedColor:TextColor);}
        if(CloseButton?.Surface!=null)canvas.DrawSurface(CloseButton.Surface,Origin.X+CloseButton.Offset.X,Origin.Y+CloseButton.Offset.Y);
    }

    /// <summary>關閉 X／堆疊滿時關窗（本機動作，不送封包）。</summary>
    public void Close(){
        if(view==null)return;
        long revision=view.Revision;closedRevision=revision;view=null;pressed=null;pressedRow=-1;Selected=-1;Scroll=0;Source?.Close(revision);
    }

    void ScrollBy(int delta)=>Scroll=Math.Clamp(Scroll+delta,0,MaxScroll);
    void Pick(int index,bool activate){
        if(index<0||view==null)return;
        if(activate&&index==Selected){Source?.Cook(view.Revision,Recipes[index].Index);return;}
        Selected=index;
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Wheel){
            if(!inside)return false;
            if(e.Delta!=0)ScrollBy(-Math.Sign(e.Delta));
            return true;
        }
        if(e.Button!=MouseButtons.Left)return inside||pressed!=null||pressedRow>=0;
        if(e.Kind==UiMouseKind.Down){
            pressed=At(CloseButton).Contains(e.Location)?CloseButton:At(Up).Contains(e.Location)?Up:At(Down).Contains(e.Location)?Down:null;
            pressedRow=pressed==null?RowAt(e.Location):-1;
            // 雙擊：第二下直接製作（第一下已選取）。
            if(pressedRow>=0&&e.Clicks>=2&&pressedRow==Selected){Pick(pressedRow,activate:true);pressedRow=-1;}
            return inside;
        }
        if(e.Kind==UiMouseKind.Up){
            var was=pressed;int row=pressedRow;pressed=null;pressedRow=-1;
            if(was==CloseButton&&At(CloseButton).Contains(e.Location))Close();
            else if(was==Up&&At(Up).Contains(e.Location))ScrollBy(-1);
            else if(was==Down&&At(Down).Contains(e.Location))ScrollBy(1);
            else if(was==null&&row>=0&&RowAt(e.Location)==row)Pick(row,activate:true);
            return true;
        }
        return inside;
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
