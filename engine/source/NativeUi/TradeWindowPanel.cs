namespace P06.NativeUi;

// M7-S（T219／#526）：原生 TRADE_WINDOW（uiextra86-c.xml，WIN§1.5、WIN§2 T102／T134）。
// 目前只有修理模式 6 有來源（S83 D200 → TradeWindow 模式 6，sub_A59780／FO 0x658B80，已驗證）。
// 商店的模式 0／1／7／13–16 由誰觸發沒有追（WIN§3），所以不接商店，只做模式 6。
//
// 已驗證（XML 座標＋原客戶端 surf）：底圖 1983 (6,9)、關閉 X 1962 (225,15)、長捲軸 1970 (217,51)、上鈕 2042 (217,37)、
//   下鈕 2040 (217,275)、OK 1996 (60,292)、CANCEL 1994 (127,292)。
// 推測（標明，不是原版版面證據）：模式 6 只用 OK／CANCEL 兩個鈕；清單區＝底圖左緣到捲軸左緣、上鈕底到下鈕頂；
//   列高 RowHeight；名稱靠左、費用靠右；按下即觸發。
// 不畫（XML 沒有座標或依模式而定、未查證）：框 1984、拉桿 2044、TRADE_UP／DOWN 1985／1987、ADENA／TOTAL 1990／1991、BUY 1992、SELL 1998。

/// <summary>TRADE_WINDOW 的資料來源（由 ClientForm 接上既有的修理狀態與送出動作；面板不碰 LoginConnection）。</summary>
public interface ITradeWindowSource {
    /// <summary>目前要顯示的內容；null＝不開窗。</summary>
    TradeWindowView? Current {get;}
    /// <summary>OK：送出選取的列（既有送出路徑會再驗證一次）。</summary>
    void Submit(uint id,int revision);
    /// <summary>關閉 X／CANCEL／Esc。</summary>
    void Close();
}

public sealed class TradeWindowPanel:IUiPanel {
    public const string PanelId="TRADE_WINDOW";
    public const string LayoutName="uiextra86";
    /// <summary>推測：每列高度（原版列距未查證；UiText 9pt 一行約 12 px，另留間距）。</summary>
    public const int RowHeight=16;
    const string BaseName="SURF_RENEWAL_WIN_TRADE_BASE",QuitName="SURF_RENEWAL_BTN_QUIT_X",ScrollName="SURF_RENEWAL_WIN_SCROLLBAR_LONG",
        UpName="SURF_RENEWAL_WIN_INC_BUT_OFF",DownName="SURF_RENEWAL_WIN_DEC_BUT_OFF",OkName="SURF_RENEWAL_BTN_TRADE_OK_OFF",CancelName="SURF_RENEWAL_BTN_TRADE_CANCEL_OFF";

    /// <summary>XML 的一個元件：surf、相對窗原點的位置、圖（null＝缺圖不畫，命中範圍為空）。</summary>
    public sealed record Element(string Name,int Surf,Point Offset,UiSurface? Surface) {
        public Rectangle Local=>new(Offset,Surface==null?Size.Empty:new Size(Surface.Width,Surface.Height));
        public bool Drawn=>Surface!=null&&Surface.Width>0&&Surface.Height>0;
    }

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds{get;private set;}
    public bool Visible=>view!=null;
    public ITradeWindowSource? Source{get;set;}
    public Element? Base{get;private set;}
    public Element? Quit{get;private set;}
    public Element? ScrollBar{get;private set;}
    public Element? Up{get;private set;}
    public Element? Down{get;private set;}
    public Element? Ok{get;private set;}
    public Element? Cancel{get;private set;}
    /// <summary>清單區（邏輯座標，推測，見檔頭）。</summary>
    public Rectangle ListArea{get;private set;}
    public int VisibleRows=>Math.Max(0,ListArea.Height/RowHeight);

    TradeWindowView? view;int shownRevision=-1,closedRevision=-1;
    public TradeWindowView? View=>view;
    public int Selected{get;private set;}=-1;
    public int Scroll{get;private set;}

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
            // 只畫有座標的元件；缺 xPos／yPos 時不推定 0（XML 未提供座標的元件不在本面板使用）。
            return new(name,ids[0],new(n.Int("xPos"),n.Int("yPos")),surf(ids[0]));
        }
        Base=Find(BaseName);Quit=Find(QuitName);ScrollBar=Find(ScrollName);Up=Find(UpName);Down=Find(DownName);Ok=Find(OkName);Cancel=Find(CancelName);
        var all=new[]{Base,Quit,ScrollBar,Up,Down,Ok,Cancel};
        var union=all.Where(e=>e.Drawn).Select(e=>e.Local).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        Bounds=union.IsEmpty?Rectangle.Empty:new(Origin.X+union.X,Origin.Y+union.Y,union.Width,union.Height);
        int top=Up.Offset.Y+Up.Local.Height,bottom=Down.Offset.Y,left=Base.Offset.X,right=ScrollBar.Offset.X;
        ListArea=right>left&&bottom>top?new(Origin.X+left,Origin.Y+top,right-left,bottom-top):Rectangle.Empty;
    }

    public void Update(IUiGameState state){
        var v=state.InWorld?Source?.Current:null;
        if(v!=null&&v.Revision==closedRevision)v=null;
        if(v!=null&&v.Revision!=shownRevision){shownRevision=v.Revision;Selected=-1;Scroll=0;}
        view=v;
        if(view==null){Selected=-1;Scroll=0;return;}
        if(Selected>=view.Rows.Length)Selected=-1;
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
    }
    int MaxScroll=>view==null?0:Math.Max(0,view.Rows.Length-VisibleRows);

    Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:new(Origin.X+e.Offset.X,Origin.Y+e.Offset.Y,e.Local.Width,e.Local.Height);
    /// <summary>清單區內某點對應的列索引；不在列上為 -1。</summary>
    public int RowAt(Point p){
        if(view==null||!ListArea.Contains(p))return -1;
        int row=(p.Y-ListArea.Y)/RowHeight;
        if(row>=VisibleRows)return -1;
        int index=Scroll+row;return index<view.Rows.Length?index:-1;
    }

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        foreach(var e in new[]{Base,ScrollBar,Up,Down,Quit,Ok,Cancel})if(e?.Surface!=null)canvas.DrawSurface(e.Surface,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
        if(ListArea.IsEmpty)return;
        var g=canvas.Graphics;var old=g.Clip;g.SetClip(Rectangle.Intersect(ListArea,canvas.Clip));
        try{
            for(int i=0;i<VisibleRows&&Scroll+i<view.Rows.Length;i++){
                var row=view.Rows[Scroll+i];int y=ListArea.Y+i*RowHeight+2;
                var color=!row.Enabled?Color.Gray:Scroll+i==Selected?Color.Yellow:Color.White;
                canvas.DrawText(row.Name,ListArea.X+4,y,color,Color.Black);
                int w=UiText.Measure(g,row.Detail).Width;
                canvas.DrawText(row.Detail,ListArea.Right-4-w,y,color,Color.Black);
            }
        }finally{g.Clip=old;}
    }

    void Close(){
        if(view==null)return;
        closedRevision=view.Revision;view=null;Selected=-1;Scroll=0;Source?.Close();
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        if(e.Kind==UiMouseKind.Wheel){if(e.Delta!=0)Scroll=Math.Clamp(Scroll-Math.Sign(e.Delta),0,MaxScroll);return true;}
        if(e.Kind!=UiMouseKind.Down)return true;
        if(e.Button!=MouseButtons.Left)return Bounds.Contains(e.Location);
        var p=e.Location;
        if(At(Quit).Contains(p)||At(Cancel).Contains(p)){Close();return true;}
        if(At(Up).Contains(p)){Scroll=Math.Max(0,Scroll-1);return true;}
        if(At(Down).Contains(p)){Scroll=Math.Min(MaxScroll,Scroll+1);return true;}
        if(At(Ok).Contains(p)){
            if(Selected>=0&&Selected<view.Rows.Length&&view.Rows[Selected] is {Enabled:true} row)Source?.Submit(row.Id,view.Revision);
            return true;
        }
        int index=RowAt(p);
        if(index>=0)Selected=index;
        return Bounds.Contains(p);
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!e.Down||e.KeyCode!=Keys.Escape)return false;
        Close();return true;
    }
}
