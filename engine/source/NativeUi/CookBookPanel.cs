namespace P06.NativeUi;

// R3-C（T279／#672）：原生料理窗 COOK_BOOK_WINDOW（uiextra86-c.xml；WIN2§7 W2-F，gap-matrix 第 4.2 節 CCookWindow）。
// 開窗來源：S250 52（Cooking.cs，第二欄 1＝開／切換，value＋1＝料理書等級）。
// 已驗證（WIN2§7＋XML）：底 2254 (6,8)、框 2255（無座標）、關閉 X 1962 (179,15)、捲軸 2039 (171,55)、上鈕 2042 (171,41)、
//   下鈕 2040 (171,222)、拉桿 2044（無座標）、等級圖 SURF_RENEWAL_IMG_COOK_BOOK_1..11＝2545–2555 都在 (116,257)、量表 2256 (35,251)。
// 推測（標明）：窗原點用 XML 的 x／y（建構子 sub_A5AAC0 的擺放未查）；框 2255、拉桿 2044 在 XML 沒有座標，比照 TRADE_WINDOW／QUIT_WINDOW 不畫。
// 已驗證（FU7§3）：等級圖畫第 value＋1 張（sub_A5C5E0 取 2545＋L−1，L＝value＋1）。
//
// R6-F（T336／#782；docs/reference/followups-7.md 第 3 節，本輪 IDA 唯讀再核對，查詢在 docs/reports/issue-782.md）：
// - 已驗證 清單類別 CCookListBox：sub_8C7C80 以 sub_A5AA10(29,31,165,233) 建立 → sub_88C0E0(…, 34, 34, 0) 存 this[8]＝欄距 34、this[9]＝列距 34。
// - 已驗證 版面 sub_A5BA50（FO 0x65AE50）：格子起點＝窗 +29、+31；4 欄（(i−捲動) % 4 == 3 換列）；一次最多 24 格（6 列）。
//   捲動 this[10] 一定是 4 的倍數（超過筆數時改成 4×(n/4)）；捲軸回呼 sub_A5E6F0 設 this[10]＝4×位置 → 一次捲 4 格（一列）。
// - 已驗證 選取格（項目 +8）先畫 surf 24 在 (格−1, 格−1)，再畫圖示（sub_892340，背包圖示同一套 id，Cooking.cs RecipeIcons）。
// - 已驗證 滑過（this[19]）：
//   - 名稱位置 x＝格 x（6×byte 數超過 796 時左移），y＝格 y＋32（格 y＋44＞600 時改成格 y−12）。
//   - 滑過的格「同時是選取格」且有說明行時畫提示框：寬 142、高 12＋12×行數＋10，x＝清單左緣；
//     y ≤ 清單頂＋161 時底不超過清單頂＋330，否則 y −＝ 40＋12×(行數＋1)、不小於清單頂＋10。
//     框：sub_9709D0 底，上／左 2 px 用調色盤 7，下／右 2 px 用調色盤 9；字從 (+5,+5) 起、每行 12 px：名稱，然後說明字串的每一行。
//   - 否則只在 (x, y) 畫名稱（sub_841770，外框色＝調色盤 0）。
//   說明字串＝string-c.tbl 1144–1151／1211–1218／1394–1401（sub_89AAE0 以 23 byte 切行）。
// - 已驗證 量表 sub_A5C5E0：只在倒數中（dword_153F288＞0）畫 2256 的 (0,0)–(寬,8)，寬＝50×(剩餘>>1)／總量。3.81 下不開始倒數 → 不畫。
// 推測（標明）：
//   - 格子命中範圍＝34×34（ItemListBox 的命中函式沒追）；滑鼠位置 → 滑過格，離開格子 → 沒有滑過。
//   - 點一下選取、在選取格上再點一下（或雙擊）→ 製作（原版 sub_A5B9C0 對 +8 選取格呼叫 sub_A5D2A0；觸發它的事件沒追）。
//   - 最大捲動＝最後一頁剛好填滿（原版的捲軸範圍設定沒追）。
//   - 畫面座標用 800×600 邏輯畫布、偏移 0（dword_154524C／dword_1545250）。
//   - 字串表讀不到時，提示框的說明改用 recipe.tbl 的材料（引擎文字）；surf 24 缺圖時選取格畫 1 px 黃框（引擎替代，不是原版圖）。
//   - 調色盤 7／9 的實際色、sub_9709D0 的底色、字色 word_153F240[1] 未查；用 ChatPalette 7／9、半透明黑、白字。
// IUiClosablePanel：4 層堆疊滿時被擠出 → 與關閉 X 相同（本機關窗，不送封包）。
// 不做（待驗證）：拖曳（原版拖曳後清單命中框改成 +21,+42–+155,+244，sub_A5D3B0）、Esc 關窗。

/// <summary>料理窗的一格：Index＝C_USE_ITEM 送的編號；Name、Materials 已代換成顯示文字；Icon＝背包圖示 id；TextId＝說明字串（0＝沒有）；Seconds＝欄 4。</summary>
public sealed record CookBookRecipe(int Index,string Name,string Materials,int Icon=0,int TextId=0,int Seconds=0);

/// <summary>料理窗目前要顯示的內容。Revision 每次 S250 52 或關窗都會變。Recipes null＝沒有清單（表讀不到）。GaugePixels＝倒數量表寬（0＝不畫）。</summary>
public sealed record CookBookView(long Revision,byte Value,int Level,IReadOnlyList<CookBookRecipe>? Recipes=null,int GaugePixels=0);

/// <summary>料理窗資料來源（P06.WorldCookBookSource 轉接 WorldSession）。</summary>
public interface ICookBookSource {
    /// <summary>null＝不開窗。</summary>
    CookBookView? Current {get;}
    /// <summary>關閉 X：只關 revision 這一次開的窗（本機動作，不送封包）。</summary>
    void Close(long revision);
    /// <summary>製作 revision 這一次開的窗裡編號 recipe 的料理；回 true＝已交出請求（送不送由道具使用閘門決定）。</summary>
    bool Cook(long revision,int recipe)=>false;
}

/// <summary>提示框（Box 空＝只畫名稱）：Lines[0]＝名稱，之後是說明字串的行；Text＝第一行左上角。</summary>
public sealed record CookTooltip(int Index,Rectangle Box,Point Text,IReadOnlyList<string> Lines) {
    public bool Detailed=>!Box.IsEmpty;
    public Rectangle Area=>Detailed?Box:new(Text.X,Text.Y,6*BoardText.Bytes(Lines[0]),CookBookPanel.LineHeight);
}

public sealed class CookBookPanel(ItemIconSource? icons=null):IUiClosablePanel {
    [UiPanel(170)] internal static UiPanelRegistration Registration=>new(CookBookPanel.PanelId,CookBookPanel.LayoutName,root=>new CookBookPanel(ItemIconSource.FromRoot(root)));   // R3-C (#672): COOK_BOOK_WINDOW (uiextra86); R6-F (#782): icons
    public const string PanelId="COOK_BOOK_WINDOW";
    public const string LayoutName="uiextra86";
    public const int LevelImages=11;
    /// <summary>已驗證（sub_A5BA50／sub_A5AA10／sub_88C0E0）：格子起點、欄距＝列距 34、4 欄、一頁 24 格、一次捲 4 格。</summary>
    public const int GridLeft=29,GridTop=31,CellPitch=34,Columns=4,PageCells=24,PageRows=PageCells/Columns,ScrollStep=Columns;
    /// <summary>已驗證（sub_8C7C80）：清單的初始範圍 (29,31)–(165,233)（相對窗原點）。</summary>
    public static readonly Rectangle ListLocal=Rectangle.FromLTRB(29,31,165,233);
    /// <summary>已驗證（sub_A5BA50）：提示框寬、行高、內距、底部多出的高度、名稱右緣與下緣界線、說明字串切行 byte 數（sub_89AAE0 第 4 參數）。</summary>
    public const int TooltipWidth=142,LineHeight=12,TooltipPadding=5,TooltipExtra=10,RightLimit=796,BottomLimit=600,WrapBytes=23;
    /// <summary>已驗證：選取格框是 surf 24（sub_E538D0(24)），畫在格 −1。</summary>
    public const int SelectionSurf=24;
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
    /// <summary>窗本身（畫出的元件聯集）＋顯示中的提示框。</summary>
    public Rectangle Bounds{get;private set;}
    /// <summary>窗本身（畫出的元件聯集），命中判斷用。</summary>
    public Rectangle WindowBounds{get;private set;}
    public bool Visible=>view!=null;
    public ICookBookSource? Source{get;set;}
    public ItemIconSource Icons{get;set;}=icons??ItemIconSource.None;
    public Element? Base{get;private set;}
    public Element? CloseButton{get;private set;}
    public Element? ScrollBar{get;private set;}
    public Element? Up{get;private set;}
    public Element? Down{get;private set;}
    public Element? Gauge{get;private set;}
    public UiSurface? SelectionFrame{get;private set;}
    /// <summary>等級圖 1..11（索引 0＝SURF_RENEWAL_IMG_COOK_BOOK_1）。</summary>
    public IReadOnlyList<Element> Levels{get;private set;}=[];
    public CookBookView? View=>view;
    /// <summary>目前畫的等級圖（第 value＋1 張）；value 超出範圍時 null。</summary>
    public Element? LevelImage=>view!=null&&view.Value<Levels.Count?Levels[view.Value]:null;
    public IReadOnlyList<CookBookRecipe> Recipes=>view?.Recipes??[];
    /// <summary>捲動＝畫面第一格的清單索引（4 的倍數）。</summary>
    public int Scroll{get;private set;}
    /// <summary>推測：最後一頁剛好填滿時的捲動。</summary>
    public int MaxScroll=>Math.Max(0,(Recipes.Count+Columns-1)/Columns-PageRows)*Columns;
    /// <summary>選取的清單索引（-1＝沒選）。</summary>
    public int Selected{get;private set;}=-1;
    public CookBookRecipe? SelectedRecipe=>Selected>=0&&Selected<Recipes.Count?Recipes[Selected]:null;
    /// <summary>滑過的清單索引（-1＝沒有）。</summary>
    public int Hovered{get;private set;}=-1;
    /// <summary>量表要畫的寬（0＝不畫；只在製作倒數中 ＞0）。</summary>
    public int GaugePixels=>view==null?0:Math.Clamp(view.GaugePixels,0,CookingProtocol.GaugeWidth);
    CookBookView? view;long closedRevision=-1;Element? pressed;int pressedCell=-1;

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
        SelectionFrame=surf(SelectionSurf);
        var union=Fixed.Concat(Levels).Where(e=>e.Drawn).Select(e=>e.Local).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        WindowBounds=union.IsEmpty?Rectangle.Empty:new(Origin.X+union.X,Origin.Y+union.Y,union.Width,union.Height);
        view=null;ResetList();
    }
    IEnumerable<Element> Fixed=>new[]{Base,ScrollBar,Up,Down,Gauge,CloseButton}.OfType<Element>();
    void ResetList(){Scroll=0;Selected=-1;Hovered=-1;pressed=null;pressedCell=-1;UpdateBounds();}

    public void Update(IUiGameState state){
        var v=state.InWorld?Source?.Current:null;
        if(v!=null&&v.Revision==closedRevision)v=null;
        // 新的一次開窗（或換等級）：捲回頂端、清掉選取與滑過。
        bool reset=v==null||view==null||v.Revision!=view.Revision;
        view=v;
        if(reset)ResetList();
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
        if(Selected>=Recipes.Count)Selected=-1;
        if(Hovered>=Recipes.Count||!OnPage(Hovered))Hovered=-1;
        UpdateBounds();
    }

    public Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:new(Origin.X+e.Offset.X,Origin.Y+e.Offset.Y,e.Local.Width,e.Local.Height);
    /// <summary>畫面第 slot 格（0..23）的範圍（邏輯座標）。</summary>
    public Rectangle CellRect(int slot)=>new(Origin.X+GridLeft+slot%Columns*CellPitch,Origin.Y+GridTop+slot/Columns*CellPitch,CellPitch,CellPitch);
    bool OnPage(int index)=>index>=Scroll&&index<Scroll+PageCells&&index<Recipes.Count;
    /// <summary>點到的清單索引；不在任何一格上（或那格沒有料理）時 -1。</summary>
    public int CellAt(Point p){
        if(!WindowBounds.Contains(p))return -1;
        for(int s=0;s<PageCells;s++){int i=Scroll+s;if(i>=Recipes.Count)break;if(CellRect(s).Contains(p))return i;}
        return -1;
    }
    /// <summary>目前畫出的格（已套用捲動）。</summary>
    public IReadOnlyList<CookBookRecipe> VisibleRecipes=>Recipes.Skip(Scroll).Take(PageCells).ToArray();

    /// <summary>說明字串的行（string-c.tbl；讀不到時用材料；說明 id 0 → 沒有行）。</summary>
    public static IReadOnlyList<string> DescriptionLines(CookBookRecipe r){
        if(r.TextId<=0)return [];
        string fallback=r.Materials.Length>0?"材料:"+r.Materials:"";
        return Wrap(CookingProtocol.Text(r.TextId,fallback),WrapBytes);
    }
    /// <summary>以 CP950 byte 數切行（雙位元組字不切開）；空字串 → 沒有行。</summary>
    public static IReadOnlyList<string> Wrap(string text,int bytes){
        var lines=new List<string>();var line=new System.Text.StringBuilder();int used=0;
        foreach(var ch in text){
            int w=BoardText.Bytes(ch.ToString());
            if(used+w>bytes&&line.Length>0){lines.Add(line.ToString());line.Clear();used=0;}
            line.Append(ch);used+=w;
        }
        if(line.Length>0)lines.Add(line.ToString());
        return lines;
    }

    /// <summary>滑過格的提示（見檔頭 sub_A5BA50）；沒有滑過時 null。</summary>
    public CookTooltip? Tooltip{get{
        if(view==null||!OnPage(Hovered))return null;
        var r=Recipes[Hovered];var cell=CellRect(Hovered-Scroll);
        int nameWidth=6*BoardText.Bytes(r.Name);
        int x=cell.X,y=cell.Y+32;
        if(nameWidth+cell.X>RightLimit)x=RightLimit-nameWidth;
        if(cell.Y+44>BottomLimit)y=cell.Y-12;
        var lines=DescriptionLines(r);
        if(Hovered!=Selected||lines.Count==0)return new(Hovered,Rectangle.Empty,new(x,y),[r.Name]);
        int n=lines.Count,h=LineHeight+LineHeight*n+TooltipExtra,listTop=Origin.Y+GridTop;
        x=Origin.X+GridLeft;
        if(y<=listTop+161){if(y+h>listTop+330)y=listTop+330-h;}
        else{y=y-40-LineHeight*(n+1);if(y<listTop+10)y=listTop+10;}
        return new(Hovered,new(x,y,TooltipWidth,h),new(x+TooltipPadding,y+TooltipPadding),[r.Name,..lines]);
    }}
    void UpdateBounds(){
        var b=WindowBounds;
        if(Tooltip is {} tip)b=b.IsEmpty?tip.Area:Rectangle.Union(b,tip.Area);
        Bounds=b;
    }

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        // 底、捲軸、上下鈕、量表（只在倒數中）、等級圖、格子（選取框＋圖示）、關閉 X、提示。
        foreach(var e in new[]{Base,ScrollBar,Up,Down})
            if(e?.Surface!=null)canvas.DrawSurface(e.Surface,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
        if(Gauge is {Drawn:true} g&&GaugePixels>0)
            canvas.DrawSurface(g.Surface,new Rectangle(0,0,GaugePixels,CookingProtocol.GaugeHeight),Origin.X+g.Offset.X,Origin.Y+g.Offset.Y);
        if(LevelImage?.Surface is {} level)canvas.DrawSurface(level,Origin.X+LevelImage.Offset.X,Origin.Y+LevelImage.Offset.Y);
        var cells=VisibleRecipes;
        for(int s=0;s<cells.Count;s++){
            var rect=CellRect(s);
            if(Scroll+s==Selected){
                if(SelectionFrame!=null)canvas.DrawSurface(SelectionFrame,rect.X-1,rect.Y-1);
                else Outline(canvas,new(rect.X-1,rect.Y-1,ItemIconSource.IconSize+2,ItemIconSource.IconSize+2),SelectedColor);
            }
            ItemIconSource.Draw(canvas,Icons.Get(cells[s].Icon),rect.Location);
        }
        if(CloseButton?.Surface!=null)canvas.DrawSurface(CloseButton.Surface,Origin.X+CloseButton.Offset.X,Origin.Y+CloseButton.Offset.Y);
        if(Tooltip is {} tip){
            if(tip.Detailed){
                var b=tip.Box;
                using(var back=new SolidBrush(Color.FromArgb(192,0,0,0)))canvas.Graphics.FillRectangle(back,b);
                using var light=new SolidBrush(ChatPalette.Color(7));using var dark=new SolidBrush(ChatPalette.Color(9));
                var gfx=canvas.Graphics;
                gfx.FillRectangle(light,b.X,b.Y,b.Width-1,1);gfx.FillRectangle(light,b.X+1,b.Y+1,b.Width-2,1);
                gfx.FillRectangle(light,b.X,b.Y+1,1,b.Height-1);gfx.FillRectangle(light,b.X+1,b.Y+2,1,b.Height-2);
                gfx.FillRectangle(dark,b.X+1,b.Bottom-1,b.Width-1,1);gfx.FillRectangle(dark,b.X+2,b.Bottom-2,b.Width-2,1);
                gfx.FillRectangle(dark,b.Right-1,b.Y,1,b.Height-1);gfx.FillRectangle(dark,b.Right-2,b.Y+1,1,b.Height-2);
                for(int i=0;i<tip.Lines.Count;i++)canvas.DrawText(tip.Lines[i],tip.Text.X,tip.Text.Y+LineHeight*i,TextColor);
            }
            else canvas.DrawText(tip.Lines[0],tip.Text.X,tip.Text.Y,TextColor,ChatPalette.Color(0));
        }
    }

    static void Outline(UiCanvas canvas,Rectangle r,Color color){
        using var b=new SolidBrush(color);var g=canvas.Graphics;
        g.FillRectangle(b,r.X,r.Y,r.Width,1);g.FillRectangle(b,r.X,r.Bottom-1,r.Width,1);
        g.FillRectangle(b,r.X,r.Y,1,r.Height);g.FillRectangle(b,r.Right-1,r.Y,1,r.Height);
    }

    /// <summary>關閉 X／堆疊滿時關窗（本機動作，不送封包）。</summary>
    public void Close(){
        if(view==null)return;
        long revision=view.Revision;closedRevision=revision;view=null;ResetList();Source?.Close(revision);
    }

    void ScrollBy(int rows){Scroll=Math.Clamp(Scroll+rows*ScrollStep,0,MaxScroll);if(!OnPage(Hovered))Hovered=-1;UpdateBounds();}
    void Pick(int index,bool activate){
        if(index<0||view==null)return;
        if(activate&&index==Selected){Source?.Cook(view.Revision,Recipes[index].Index);return;}
        Selected=index;UpdateBounds();
    }
    void Hover(int index){if(index!=Hovered){Hovered=index;UpdateBounds();}}

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        bool inside=WindowBounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Move){Hover(CellAt(e.Location));return inside;}
        if(e.Kind==UiMouseKind.Wheel){
            if(!inside)return false;
            if(e.Delta!=0)ScrollBy(-Math.Sign(e.Delta));
            return true;
        }
        if(e.Button!=MouseButtons.Left)return inside||pressed!=null||pressedCell>=0;
        if(e.Kind==UiMouseKind.Down){
            pressed=At(CloseButton).Contains(e.Location)?CloseButton:At(Up).Contains(e.Location)?Up:At(Down).Contains(e.Location)?Down:null;
            pressedCell=pressed==null?CellAt(e.Location):-1;
            // 雙擊：第二下直接製作（第一下已選取）。
            if(pressedCell>=0&&e.Clicks>=2&&pressedCell==Selected){Pick(pressedCell,activate:true);pressedCell=-1;}
            return inside;
        }
        if(e.Kind==UiMouseKind.Up){
            var was=pressed;int cell=pressedCell;pressed=null;pressedCell=-1;
            if(was==CloseButton&&At(CloseButton).Contains(e.Location))Close();
            else if(was==Up&&At(Up).Contains(e.Location))ScrollBy(-1);
            else if(was==Down&&At(Down).Contains(e.Location))ScrollBy(1);
            else if(was==null&&cell>=0&&CellAt(e.Location)==cell)Pick(cell,activate:true);
            return true;
        }
        return inside;
    }

    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
