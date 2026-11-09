using System.Globalization;
using System.Text.RegularExpressions;
namespace P06.NativeUi;

// R3-E（T281／#674）：玩家交易原生窗 TRANSACTION_WINDOW（uiextra86-c.xml；WIN§1.5，gap-matrix 第 1 節 op 88／145／167、第 4.1 節 ExchangeUI）。
// 協定不在這裡：S52（op 88）開窗、S35（op 145）加物品、S112（op 167）結果都由 Trade.cs 處理；面板只讀 TransactionView，
// 按鈕經 ITransactionSource 轉回 Trade.cs 既有的 C71（確定）／C86（取消）入口，加物品（C37）仍走 TradeUi 的加入控制項。
//
// 已驗證（本機 uiextra86-c.xml 唯讀核對）：底 2000 (6,10)、框 2001（無座標）、關閉 X 1962 (179,15)、文字框 2008 (15,213)、
//   確定 STAGE3 2002 (36,252)、取消 STAGE3 2005 (104,252)、ITEM_LIST_ADJUST (26,49) tag 91。
// 推定（同檔兩窗吻合）：ITEM_LIST_ADJUST 的 tag＝上下兩個清單的垂直間距（EXCHANGE_WINDOW 兩條捲軸 61／185 差 124＝tag 124；
//   ADENA_EXCHANGE_WINDOW 81／225 差 144＝tag 144），所以上清單在 y=49、下清單在 y=49+91=140。
// 推測（標明，不是原版版面證據）：
//   - 上清單＝S35 side 0（自己）、下清單＝side 1（對方）（L1J S_TradeAddItem 的 0＝上段／1＝下段慣例）。
//   - 每個清單高＝文字框頂 − 下清單頂（兩清單同高）；寬＝底圖右緣 − 清單左緣 − (清單左緣 − 底圖左緣)；列高 RowHeight。
//   - 物品只畫名稱（原版畫物品圖示格；原生 UI 尚無物品圖示來源）。
//   - 金幣＝名稱為「$4 (數量)」的列（L1J 金幣名稱 $4 加數量），在各清單右下角顯示合計。
//   - 文字框畫交易提示；對方名稱畫在標題列；框 2001 沒有座標，比照 TRADE_WINDOW／QUIT_WINDOW 不畫。
//   - STAGE3_BTN：n＝平常、n+1＝按下、n+2＝停留（比照 QUIT_WINDOW）；自己已確定時確定鈕畫按下圖。
//   - 窗的起點取 XML x／y（原版建構子擺放位置未追）；標題列（清單以上、按鈕以外）可拖曳。
// 不做：從背包拖物品進窗（原生背包拖曳未實作）、金幣輸入框（原版是否有、怎麼送未查）。

/// <summary>交易窗中的一列（Side 0＝自己、1＝對方；Name 已解析成顯示文字）。</summary>
public sealed record TransactionOffer(byte Side,string Name,long Adena);

/// <summary>交易窗內容（由 Trade.cs 的狀態轉出，面板不碰 LoginConnection）。</summary>
public sealed record TransactionView(int Revision,string Peer,string Notice,IReadOnlyList<TransactionOffer> Offers,bool CanConfirm,bool CanCancel,bool OwnConfirmed) {
    public IReadOnlyList<TransactionOffer> Side(byte side)=>Offers.Where(o=>o.Side==side).ToArray();
    public long Adena(byte side)=>Offers.Where(o=>o.Side==side).Sum(o=>o.Adena);

    /// <summary>交易窗開著的狀態：原服已送 S52，且還沒收到 S112 結果。</summary>
    public static bool WindowState(string state)=>state is "open" or "adding" or "confirmed" or "attention" or "cancelling";

    /// <summary>從 WorldSession 轉出；沒有開著的交易窗時為 null。resolve 把原服名稱（$n 等）轉成顯示文字。</summary>
    public static TransactionView? From(WorldSession w,Func<string,string> resolve){
        if(!w.TradeOpened||!WindowState(w.TradeState))return null;
        return new(w.TradeRevision,w.TradePeerName,resolve(w.TradeNotice),
            w.TradeOffers.Select(o=>new TransactionOffer(o.Side,resolve(o.Name),TransactionRules.Adena(o.Name)??0)).ToArray(),
            w.TradeCanConfirm&&!w.TradeOwnConfirmed,w.TradeCanCancel,w.TradeOwnConfirmed);
    }
}

public static class TransactionRules {
    /// <summary>推測：金幣的原服名稱（L1J 金幣 name id $4，可堆疊物品在名稱後加「 (數量)」）。</summary>
    public const string AdenaName="$4";
    static readonly Regex adena=new(@"^\$4 \((\d{1,19})\)$",RegexOptions.CultureInvariant);
    /// <summary>名稱是金幣時回傳數量（無數量＝1）；其他物品為 null。</summary>
    public static long? Adena(string rawName){
        if(rawName==AdenaName)return 1;
        var m=adena.Match(rawName);
        return m.Success&&long.TryParse(m.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out long n)?n:null;
    }
}

/// <summary>交易窗的資料與動作（TradeUi.cs 的 ClientForm 接到 Trade.cs 既有入口）。</summary>
public interface ITransactionSource {
    TransactionView? Current {get;}
    /// <summary>確定鈕：C71（Trade.cs "confirm"，revision 不符時不送）。</summary>
    void Confirm(int revision);
    /// <summary>取消鈕、關閉 X、Esc：C86（Trade.cs "cancel"）。</summary>
    void Cancel(int revision);
}

public sealed class TransactionPanel:IUiMovablePanel {
    [UiPanel(185)] internal static UiPanelRegistration Registration=>new(TransactionPanel.PanelId,TransactionPanel.LayoutName,_=>new TransactionPanel());   // R3-E (#674): TRANSACTION_WINDOW (uiextra86)
    public const string PanelId="TRANSACTION_WINDOW";
    public const string LayoutName="uiextra86";
    /// <summary>推測：清單列高（UiText 9pt 一行約 12 px）。</summary>
    public const int RowHeight=12;
    const string BaseName="SURF_RENEWAL_WIN_TRANSACT_BASE",CloseName="SURF_RENEWAL_BTN_QUIT_X",TextName="SURF_RENEWAL_WIN_TRANSACT_TEXT_FRAME",
        OkName="SURF_RENEWAL_BTN_TRANSACT_OK",CancelName="SURF_RENEWAL_BTN_TRANSACT_CANCEL",ListNode="ITEM_LIST_ADJUST";

    /// <summary>XML 元件：第一個 surf、相對窗原點的位置、狀態圖（平常／按下／停留；null＝缺圖）。</summary>
    public sealed record Element(string Name,int Surf,Point Offset,UiSurface? Surface,UiSurface? Push=null,UiSurface? Highlight=null) {
        public Rectangle Local=>new(Offset,Surface==null?Size.Empty:new Size(Surface.Width,Surface.Height));
        public bool Drawn=>Surface!=null&&Surface.Width>0&&Surface.Height>0;
    }

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds=>local.IsEmpty?Rectangle.Empty:new(Origin.X+local.X,Origin.Y+local.Y,local.Width,local.Height);
    public bool Visible=>view!=null;
    public ITransactionSource? Source{get;set;}
    public Element? Base{get;private set;}
    public Element? CloseButton{get;private set;}
    public Element? TextFrame{get;private set;}
    public Element? Ok{get;private set;}
    public Element? Cancel{get;private set;}
    /// <summary>ITEM_LIST_ADJUST 的位置（相對窗原點）與 tag（上下清單間距，推定）。</summary>
    public Point ListOffset{get;private set;}
    public int ListSpacing{get;private set;}
    public TransactionView? View=>view;
    public Element? Hover{get;private set;}
    public Element? Pressed{get;private set;}
    /// <summary>各清單的捲動列數（0＝自己、1＝對方）。</summary>
    public int Scroll(byte side)=>scroll[side];

    TransactionView? view;Rectangle local;Rectangle ownLocal,peerLocal;readonly int[] scroll=new int[2];int shownRevision=-1;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var window=layout.Root.Child(PanelId);
        Element Find(string name,bool stage3){
            var matches=window.Children.Where(c=>c.Attr("surfName")==name).Take(2).ToList();
            if(matches.Count!=1)throw new UiLayoutException(layout.FileName,window.Path+"/@surfName="+name,matches.Count==0?"缺少元件":"元件不唯一");
            var n=matches[0];int[] ids=n.Surfs();
            if(ids.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"surfNumber 空白");
            // SURF_IMAGE／IMAGE／IMAGE_BTN 用 xPos/yPos；STAGE3_BTN 用 xRPos/yRPos（相對窗）。缺座標 → 丟例外，不推定 0。
            var at=n.Attr("xPos")!=null||n.Attr("yPos")!=null?new Point(n.Int("xPos"),n.Int("yPos")):new Point(n.Int("xRPos"),n.Int("yRPos"));
            int first=ids[0];
            return stage3?new(name,first,at,surf(first),surf(first+1),surf(first+2)):new(name,first,at,surf(first));
        }
        Base=Find(BaseName,false);CloseButton=Find(CloseName,false);TextFrame=Find(TextName,false);
        Ok=Find(OkName,true);Cancel=Find(CancelName,true);
        var lists=window.ChildrenNamed(ListNode).Take(2).ToList();
        if(lists.Count!=1)throw new UiLayoutException(layout.FileName,window.Path+"/"+ListNode,lists.Count==0?"缺少元件":"元件不唯一");
        ListOffset=new(lists[0].Int("xPos"),lists[0].Int("yPos"));ListSpacing=lists[0].Int("tag");
        if(ListSpacing<=0)throw new UiLayoutException(layout.FileName,lists[0].Path,"tag 應為正數（上下清單間距）");
        local=All.Where(e=>e.Drawn).Select(e=>e.Local).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        Origin=new(window.Int("x"),window.Int("y"));
        // 推測的清單區（檔頭）：兩清單同高＝文字框頂 − 下清單頂；寬依底圖左右對稱留白。
        int lowerTop=ListOffset.Y+ListSpacing,height=Math.Min(ListSpacing,TextFrame.Offset.Y-lowerTop);
        int width=Base.Drawn?Base.Local.Right-ListOffset.X-Math.Max(0,ListOffset.X-Base.Offset.X):0;
        if(height>0&&width>0){ownLocal=new(ListOffset.X,ListOffset.Y,width,height);peerLocal=new(ListOffset.X,lowerTop,width,height);}
        else ownLocal=peerLocal=Rectangle.Empty;
        view=null;shownRevision=-1;scroll[0]=scroll[1]=0;Hover=Pressed=null;
    }
    IEnumerable<Element> All=>new[]{Base,CloseButton,TextFrame,Ok,Cancel}.OfType<Element>();
    IEnumerable<Element> Buttons=>new[]{CloseButton,Ok,Cancel}.OfType<Element>();

    /// <summary>清單區（邏輯座標；0＝自己（上）、1＝對方（下））。</summary>
    public Rectangle ListArea(byte side){var r=side==0?ownLocal:peerLocal;return r.IsEmpty?r:new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);}
    public int VisibleRows=>Math.Max(0,ownLocal.Height/RowHeight);
    int MaxScroll(byte side)=>view==null?0:Math.Max(0,view.Side(side).Count-VisibleRows);

    public void Update(IUiGameState state){
        var v=state.InWorld?Source?.Current:null;
        if(v==null){view=null;Hover=Pressed=null;scroll[0]=scroll[1]=0;shownRevision=-1;return;}
        bool fresh=view==null;view=v;
        if(fresh)scroll[0]=scroll[1]=0;
        // 新列進來時捲到最後，讓剛加入的物品看得到。
        if(v.Revision!=shownRevision){shownRevision=v.Revision;if(!fresh){scroll[0]=MaxScroll(0);scroll[1]=MaxScroll(1);}}
        scroll[0]=Math.Clamp(scroll[0],0,MaxScroll(0));scroll[1]=Math.Clamp(scroll[1],0,MaxScroll(1));
    }

    public Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:new(Origin.X+e.Offset.X,Origin.Y+e.Offset.Y,e.Local.Width,e.Local.Height);
    public Element? ButtonAt(Point p)=>Buttons.FirstOrDefault(b=>At(b).Contains(p));
    public UiSurface? StateSurface(Element e){
        if(e==Ok&&view?.OwnConfirmed==true)return e.Push??e.Surface;
        if(Pressed==e&&Hover==e)return e.Push??e.Surface;
        if(Hover==e)return e.Highlight??e.Surface;
        return e.Surface;
    }

    /// <summary>某清單目前畫出的列（已套用捲動）。</summary>
    public IReadOnlyList<TransactionOffer> VisibleOffers(byte side)=>view==null?[]:view.Side(side).Skip(scroll[side]).Take(VisibleRows).ToArray();

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        foreach(var e in All)if(StateSurface(e) is {} s)canvas.DrawSurface(s,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
        var g=canvas.Graphics;
        if(Base!=null&&Base.Drawn)canvas.DrawText(view.Peer,Origin.X+ListOffset.X,Origin.Y+CloseButton!.Offset.Y,Color.White,Color.Black);
        for(byte side=0;side<2;side++){
            var area=ListArea(side);if(area.IsEmpty)continue;
            var old=g.Clip;g.SetClip(Rectangle.Intersect(area,canvas.Clip));
            try{
                int i=0;foreach(var o in VisibleOffers(side))canvas.DrawText(o.Name,area.X+2,area.Y+i++*RowHeight,Color.White,Color.Black);
                long gold=view.Adena(side);
                if(gold>0){string text=gold.ToString("N0",CultureInfo.InvariantCulture);int w=UiText.Measure(g,text).Width;canvas.DrawText(text,area.Right-2-w,area.Bottom-RowHeight,Color.Gold,Color.Black);}
            }finally{g.Clip=old;}
        }
        if(TextFrame is {Drawn:true} tf){
            var r=At(tf);var old=g.Clip;g.SetClip(Rectangle.Intersect(r,canvas.Clip));
            try{canvas.DrawText(view.Notice,r.X+4,r.Y+3,Color.White,Color.Black);}finally{g.Clip=old;}
        }
    }

    void Fire(Element e){
        if(view==null)return;
        if(e==Ok){if(view.CanConfirm)Source?.Confirm(view.Revision);return;}
        if(view.CanCancel)Source?.Cancel(view.Revision);   // 取消鈕、關閉 X：送 C86，窗等原服 S112 才關
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        var hit=ButtonAt(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:Hover=hit;return Bounds.Contains(e.Location)||Pressed!=null;
            case UiMouseKind.Wheel:
                for(byte side=0;side<2;side++)if(ListArea(side).Contains(e.Location)&&e.Delta!=0){scroll[side]=Math.Clamp(scroll[side]-Math.Sign(e.Delta),0,MaxScroll(side));return true;}
                return Bounds.Contains(e.Location);
            case UiMouseKind.Down:
                if(e.Button==MouseButtons.Left){Hover=hit;Pressed=hit;}
                return Bounds.Contains(e.Location);
            case UiMouseKind.Up:{
                var pressed=Pressed;Pressed=null;Hover=hit;
                if(pressed!=null&&pressed==hit)Fire(pressed);
                return true;
            }
            default:return Bounds.Contains(e.Location);
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!e.Down||e.KeyCode!=Keys.Escape)return false;
        if(view.CanCancel)Source?.Cancel(view.Revision);
        return true;
    }

    /// <summary>標題列：清單以上、按鈕以外（推測）。</summary>
    public bool IsDragHandle(Point location){
        if(view==null||!Bounds.Contains(location)||ButtonAt(location)!=null)return false;
        return location.Y<Origin.Y+ListOffset.Y;
    }
    public void MoveTo(Point location){Origin=new(location.X-local.X,location.Y-local.Y);}
}
