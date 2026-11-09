using System.Globalization;
using System.Text.RegularExpressions;
namespace P06.NativeUi;

// R3-E（T281／#674）：玩家交易原生窗 TRANSACTION_WINDOW（uiextra86-c.xml；WIN§1.5，gap-matrix 第 1 節 op 88／145／167、第 4.1 節 ExchangeUI）。
// 協定不在這裡：S52（op 88）開窗、S35（op 145）加物品、S112（op 167）結果都由 Trade.cs 處理；面板只讀 TransactionView，
// 按鈕經 ITransactionSource 轉回 Trade.cs 既有的 C71（確定）／C86（取消）／C37（加物品）入口。
//
// 已驗證（本機 uiextra86-c.xml 唯讀核對）：底 2000 (6,10)、框 2001（無座標）、關閉 X 1962 (179,15)、文字框 2008 (15,213)、
//   確定 STAGE3 2002 (36,252)、取消 STAGE3 2005 (104,252)、ITEM_LIST_ADJUST (26,49) tag 91。
// 推定（同檔兩窗吻合）：ITEM_LIST_ADJUST 的 tag＝上下兩個清單的垂直間距（EXCHANGE_WINDOW 兩條捲軸 61／185 差 124＝tag 124；
//   ADENA_EXCHANGE_WINDOW 81／225 差 144＝tag 144），所以上清單在 y=49、下清單在 y=49+91=140。
// 推測（標明，不是原版版面證據）：
//   - 上清單＝S35 side 0（自己）、下清單＝side 1（對方）（L1J S_TradeAddItem 的 0＝上段／1＝下段慣例）。
//   - 每個清單高＝文字框頂 − 下清單頂（兩清單同高）；寬＝底圖右緣 − 清單左緣 − (清單左緣 − 底圖左緣)；列高 RowHeight。
//   - 金幣＝名稱為「$4 (數量)」的列（L1J 金幣名稱 $4 加數量），在各清單右下角顯示合計。
//   - 文字框畫交易提示；對方名稱畫在標題列；框 2001 沒有座標，比照 TRADE_WINDOW／QUIT_WINDOW 不畫。
//   - STAGE3_BTN：n＝平常、n+1＝按下、n+2＝停留（比照 QUIT_WINDOW）；自己已確定時確定鈕畫按下圖。
//   - 窗的起點取 XML x／y（原版建構子擺放位置未追）；標題列（清單以上、按鈕以外）可拖曳。
// R4-C（T303／#715，gap-matrix 13.5）：
//   - 物品圖示：有圖示來源（R4-B ItemIconSource，Sprite "<icon>.tbt"）時，兩個清單改畫 33 px 圖示格（LegacyCellGrid.Fit 填滿清單區，
//     推測，原版格子幾何未追）；S35 的 icon＋bless 照 FU5§4 的 IconOf 轉換；堆疊數量畫在格子右下；滑過的格子名稱畫在文字框。
//     沒有圖示來源（離線測試、資源未載入）時照舊畫文字列。
//   - 加物品（C37）：原生背包 ITEM_WINDOW 點選的物品（WorldSession.SelectedItemId，與 WinForms 背包共用）＋點自己的清單區
//     ＝加入整疊（推測：原版是從背包拖進窗，原生背包沒有跨窗拖曳；部分數量請用金幣框或 WinForms 備援）。
//   - 金幣輸入框：在上下清單之間的空隙（推測位置；原版 ADENA_EXCHANGE_WINDOW 的版面未接）；點框輸入數字、Enter 送 C37
//     （金幣物件＋數量，上限＝背包金幣），Backspace 刪一位、Esc 離開輸入。
//   - IUiClosablePanel：被 4 層上限擠掉（或其他關窗）時送 C86（同關閉 X），並在這筆交易結束前不再畫；
//     TradeUi 這時把 WinForms 交易面板整個叫回來（原生 UI 下的備援入口）。

/// <summary>交易窗中的一列（Side 0＝自己、1＝對方；Name 已解析成顯示文字；Icon＝IconOf 之後的背包圖示 gfx，0＝無）。</summary>
public sealed record TransactionOffer(byte Side,string Name,long Adena,int Icon=0) {
    /// <summary>名稱尾端「 (數量)」的數量（沒有＝1；推測，同背包名稱慣例）。</summary>
    public uint Count=>TransactionRules.Count(Name);
    /// <summary>不含尾端數量的名稱。</summary>
    public string BareName=>ItemText.Name(Name,Count);
}

/// <summary>背包裡可以加進交易的物品（原生 ITEM_WINDOW 點選的那一個）。</summary>
public sealed record TransactionItemChoice(uint Id,string Name,uint Count,int Icon);

/// <summary>交易窗內容（由 Trade.cs 的狀態轉出，面板不碰 LoginConnection）。</summary>
public sealed record TransactionView(int Revision,string Peer,string Notice,IReadOnlyList<TransactionOffer> Offers,bool CanConfirm,bool CanCancel,bool OwnConfirmed,
        int Generation=0,bool CanAdd=false,TransactionItemChoice? Selected=null,long AdenaHeld=0,uint? AdenaId=null) {
    public IReadOnlyList<TransactionOffer> Side(byte side)=>Offers.Where(o=>o.Side==side).ToArray();
    public long Adena(byte side)=>Offers.Where(o=>o.Side==side).Sum(o=>o.Adena);

    /// <summary>交易窗開著的狀態：原服已送 S52，且還沒收到 S112 結果。</summary>
    public static bool WindowState(string state)=>state is "open" or "adding" or "confirmed" or "attention" or "cancelling";

    /// <summary>從 WorldSession 轉出；沒有開著的交易窗時為 null。resolve 把原服名稱（$n 等）轉成顯示文字。</summary>
    public static TransactionView? From(WorldSession w,Func<string,string> resolve){
        if(!w.TradeOpened||!WindowState(w.TradeState))return null;
        var inventory=w.InventoryLoaded?w.Inventory:[];
        var gold=inventory.Where(LeftStatusState.IsAdena).ToArray();
        TransactionItemChoice? selected=w.SelectedItem is {Count:>0} s&&s.Equipped!=true
            ?new(s.ObjectId,ItemText.Name(resolve(s.Name),s.Count),s.Count,ItemIconSource.IconOf(s.IconId,s.Bless)):null;
        return new(w.TradeRevision,w.TradePeerName,resolve(w.TradeNotice),
            w.TradeOffers.Select(o=>new TransactionOffer(o.Side,resolve(o.Name),TransactionRules.Adena(o.Name)??0,ItemIconSource.IconOf(o.Icon,o.Bless))).ToArray(),
            w.TradeCanConfirm&&!w.TradeOwnConfirmed,w.TradeCanCancel,w.TradeOwnConfirmed,
            w.TradeGeneration,w.TradeCanAdd,selected,gold.Aggregate(0L,(n,i)=>n+i.Count),
            gold.OrderByDescending(i=>i.Count).FirstOrDefault()?.ObjectId);
    }
}

public static class TransactionRules {
    /// <summary>推測：金幣的原服名稱（L1J 金幣 name id $4，可堆疊物品在名稱後加「 (數量)」）。</summary>
    public const string AdenaName="$4";
    static readonly Regex adena=new(@"^\$4 \((\d{1,19})\)$",RegexOptions.CultureInvariant);
    static readonly Regex count=new(@" \((\d{1,10})\)$",RegexOptions.CultureInvariant);
    /// <summary>名稱是金幣時回傳數量（無數量＝1）；其他物品為 null。</summary>
    public static long? Adena(string rawName){
        if(rawName==AdenaName)return 1;
        var m=adena.Match(rawName);
        return m.Success&&long.TryParse(m.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out long n)?n:null;
    }
    /// <summary>名稱尾端「 (n)」的數量；沒有或不合理時為 1。</summary>
    public static uint Count(string name){
        var m=count.Match(name);
        return m.Success&&uint.TryParse(m.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out uint n)&&n>0?n:1;
    }
    /// <summary>金幣框的數量：1…min(持有, int.MaxValue)；其他（空白、0、超過持有）為 null，不送。</summary>
    public static uint? AdenaAmount(string text,long held){
        if(text.Length==0||!ulong.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out ulong v)||v==0)return null;
        return v<=(ulong)Math.Min(held,int.MaxValue)?(uint)v:null;
    }
}

/// <summary>交易窗的資料與動作（TradeUi.cs 的 ClientForm 接到 Trade.cs 既有入口）。</summary>
public interface ITransactionSource {
    TransactionView? Current {get;}
    /// <summary>確定鈕：C71（Trade.cs "confirm"，revision 不符時不送）。</summary>
    void Confirm(int revision);
    /// <summary>取消鈕、關閉 X、Esc：C86（Trade.cs "cancel"）。</summary>
    void Cancel(int revision);
    /// <summary>R4-C：加物品 C37（Trade.cs "add"：物件 id＋數量；資格由 Trade.cs 再驗一次）。預設不做事（只讀來源）。</summary>
    void Add(int revision,uint item,uint count){}
}

public sealed class TransactionPanel(ItemIconSource? icons=null):IUiMovablePanel,IUiClosablePanel,IUiTextInput {
    [UiPanel(185)] internal static UiPanelRegistration Registration=>new(TransactionPanel.PanelId,TransactionPanel.LayoutName,root=>new TransactionPanel(ItemIconSource.FromRoot(root)));   // R3-E (#674): TRANSACTION_WINDOW (uiextra86); R4-C (#715): icons
    public const string PanelId="TRANSACTION_WINDOW";
    public const string LayoutName="uiextra86";
    /// <summary>推測：清單列高（UiText 9pt 一行約 12 px）。</summary>
    public const int RowHeight=12;
    /// <summary>推測：金幣框（上下清單間的空隙）寬、高、最多位數。</summary>
    public const int AdenaBoxWidth=84,AdenaBoxHeight=14,MaxDigits=10;
    const string BaseName="SURF_RENEWAL_WIN_TRANSACT_BASE",CloseName="SURF_RENEWAL_BTN_QUIT_X",TextName="SURF_RENEWAL_WIN_TRANSACT_TEXT_FRAME",
        OkName="SURF_RENEWAL_BTN_TRANSACT_OK",CancelName="SURF_RENEWAL_BTN_TRANSACT_CANCEL",ListNode="ITEM_LIST_ADJUST";
    static readonly Color BoxColor=Color.FromArgb(200,180,120),BoxFocusColor=Color.FromArgb(255,230,120),DisabledColor=Color.Gray;

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
    /// <summary>R4-C：物品圖示來源；None＝文字列（R3-E 的畫法）。</summary>
    public ItemIconSource Icons{get;set;}=icons??ItemIconSource.None;
    public bool IconMode=>!ReferenceEquals(Icons,ItemIconSource.None)&&ownGrid.PageSize>0&&peerGrid.PageSize>0;
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
    /// <summary>各清單的捲動（0＝自己、1＝對方）：文字列＝列數，圖示格＝格子列數。</summary>
    public int Scroll(byte side)=>scroll[side];
    /// <summary>金幣框目前的文字（數字）與是否在輸入中。</summary>
    public string AdenaInput{get;private set;}="";
    public bool AdenaFocused{get;private set;}
    /// <summary>滑鼠所在的格子／列（文字框改畫它的名稱）。</summary>
    public TransactionOffer? HoverOffer{get;private set;}
    /// <summary>Close() 之後，這筆交易（Generation）結束前不再畫；TradeUi 依此叫回 WinForms 面板。</summary>
    public bool Dismissed=>dismissed!=null;

    TransactionView? view;Rectangle local;Rectangle ownLocal,peerLocal,boxLocal;readonly int[] scroll=new int[2];int shownRevision=-1;
    LegacyCellGrid ownGrid=LegacyCellGrid.Empty,peerGrid=LegacyCellGrid.Empty;
    int? dismissed;bool pressList,pressBox;

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
        // R4-C 推測：圖示格填滿各清單區；金幣框放在上清單底與下清單頂之間的空隙（靠右）。
        var pitch=new Size(LegacyCellGrid.IconPitch,LegacyCellGrid.IconPitch);
        ownGrid=ownLocal.IsEmpty?LegacyCellGrid.Empty:LegacyCellGrid.Fit(ownLocal.Left,ownLocal.Top,ownLocal.Right,ownLocal.Bottom,pitch);
        peerGrid=peerLocal.IsEmpty?LegacyCellGrid.Empty:LegacyCellGrid.Fit(peerLocal.Left,peerLocal.Top,peerLocal.Right,peerLocal.Bottom,pitch);
        int gap=peerLocal.Top-ownLocal.Bottom;
        boxLocal=!ownLocal.IsEmpty&&gap>=AdenaBoxHeight&&ownLocal.Width>=AdenaBoxWidth?new(ownLocal.Right-AdenaBoxWidth,ownLocal.Bottom+(gap-AdenaBoxHeight)/2,AdenaBoxWidth,AdenaBoxHeight):Rectangle.Empty;
        view=null;shownRevision=-1;scroll[0]=scroll[1]=0;Hover=Pressed=null;HoverOffer=null;dismissed=null;AdenaInput="";AdenaFocused=false;pressList=pressBox=false;
    }
    IEnumerable<Element> All=>new[]{Base,CloseButton,TextFrame,Ok,Cancel}.OfType<Element>();
    IEnumerable<Element> Buttons=>new[]{CloseButton,Ok,Cancel}.OfType<Element>();

    /// <summary>清單區（邏輯座標；0＝自己（上）、1＝對方（下））。</summary>
    public Rectangle ListArea(byte side){var r=side==0?ownLocal:peerLocal;return r.IsEmpty?r:new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);}
    public int VisibleRows=>Math.Max(0,ownLocal.Height/RowHeight);
    /// <summary>圖示格（相對窗原點；0＝自己、1＝對方）。</summary>
    public LegacyCellGrid Grid(byte side)=>side==0?ownGrid:peerGrid;
    /// <summary>金幣框（邏輯座標；版面放不下時為空）。</summary>
    public Rectangle AdenaBox=>boxLocal.IsEmpty?boxLocal:new(Origin.X+boxLocal.X,Origin.Y+boxLocal.Y,boxLocal.Width,boxLocal.Height);
    int PageSize(byte side)=>IconMode?Grid(side).PageSize:VisibleRows;
    int Step(byte side)=>IconMode?Grid(side).Columns:1;
    int MaxScroll(byte side){
        if(view==null)return 0;int n=view.Side(side).Count;
        return IconMode?Math.Max(0,Grid(side).RowsFor(n)-Grid(side).Rows):Math.Max(0,n-VisibleRows);
    }

    public void Update(IUiGameState state){
        var v=state.InWorld?Source?.Current:null;
        if(v!=null&&dismissed==v.Generation)v=null;   // Close() 之後這筆交易不再畫
        else if(v==null)dismissed=null;
        if(v==null){view=null;Hover=Pressed=null;HoverOffer=null;scroll[0]=scroll[1]=0;shownRevision=-1;AdenaInput="";AdenaFocused=false;pressList=pressBox=false;return;}
        bool fresh=view==null;view=v;
        if(fresh){scroll[0]=scroll[1]=0;AdenaInput="";AdenaFocused=false;}
        // 新列進來時捲到最後，讓剛加入的物品看得到。
        if(v.Revision!=shownRevision){shownRevision=v.Revision;if(!fresh){scroll[0]=MaxScroll(0);scroll[1]=MaxScroll(1);}}
        scroll[0]=Math.Clamp(scroll[0],0,MaxScroll(0));scroll[1]=Math.Clamp(scroll[1],0,MaxScroll(1));
        if(!v.CanAdd)AdenaFocused=false;
    }

    public Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:new(Origin.X+e.Offset.X,Origin.Y+e.Offset.Y,e.Local.Width,e.Local.Height);
    public Element? ButtonAt(Point p)=>Buttons.FirstOrDefault(b=>At(b).Contains(p));
    public UiSurface? StateSurface(Element e){
        if(e==Ok&&view?.OwnConfirmed==true)return e.Push??e.Surface;
        if(Pressed==e&&Hover==e)return e.Push??e.Surface;
        if(Hover==e)return e.Highlight??e.Surface;
        return e.Surface;
    }

    /// <summary>某清單目前畫出的列／格（已套用捲動）。</summary>
    public IReadOnlyList<TransactionOffer> VisibleOffers(byte side)=>view==null?[]:view.Side(side).Skip(scroll[side]*Step(side)).Take(PageSize(side)).ToArray();
    /// <summary>圖示格第 slot 格的邏輯矩形。</summary>
    public Rectangle CellRect(byte side,int slot){var r=Grid(side).CellRect(slot);return new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);}
    /// <summary>位置上的列／格（沒有為 null）。</summary>
    public TransactionOffer? OfferAt(Point p){
        if(view==null)return null;
        for(byte side=0;side<2;side++){
            if(!ListArea(side).Contains(p))continue;
            var shown=VisibleOffers(side);
            int? slot=IconMode?Grid(side).SlotAt(new(p.X-Origin.X,p.Y-Origin.Y)):(p.Y-ListArea(side).Y)/RowHeight;
            return slot is int s&&s>=0&&s<shown.Count?shown[s]:null;
        }
        return null;
    }

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        foreach(var e in All)if(StateSurface(e) is {} s)canvas.DrawSurface(s,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
        var g=canvas.Graphics;
        if(Base!=null&&Base.Drawn)canvas.DrawText(view.Peer,Origin.X+ListOffset.X,Origin.Y+CloseButton!.Offset.Y,Color.White,Color.Black);
        for(byte side=0;side<2;side++){
            var area=ListArea(side);if(area.IsEmpty)continue;
            var old=g.Clip;g.SetClip(Rectangle.Intersect(area,canvas.Clip));
            try{
                int i=0;
                foreach(var o in VisibleOffers(side)){
                    if(IconMode){
                        var cell=CellRect(side,i++);
                        ItemIconSource.Draw(canvas,Icons.Get(o.Icon),cell.Location);
                        string n=ItemText.Count(o.Count);
                        if(n.Length>0)canvas.DrawText(n,cell.Right-UiText.Measure(g,n).Width,cell.Bottom-UiText.Font.Height,Color.White,Color.Black);
                    }
                    else canvas.DrawText(o.Name,area.X+2,area.Y+i++*RowHeight,Color.White,Color.Black);
                }
                long gold=view.Adena(side);
                if(gold>0){string text=gold.ToString("N0",CultureInfo.InvariantCulture);int w=UiText.Measure(g,text).Width;canvas.DrawText(text,area.Right-2-w,area.Bottom-RowHeight,Color.Gold,Color.Black);}
            }finally{g.Clip=old;}
        }
        if(AdenaBox is {IsEmpty:false} box){
            bool usable=view.CanAdd&&view.AdenaId!=null&&view.AdenaHeld>0;
            string label="金幣";int lw=UiText.Measure(g,label).Width;
            canvas.DrawText(label,box.X-lw-3,box.Y+1,usable?Color.White:DisabledColor,Color.Black);
            using(var pen=new Pen(AdenaFocused?BoxFocusColor:usable?BoxColor:DisabledColor))g.DrawRectangle(pen,box.X,box.Y,box.Width-1,box.Height-1);
            var old=g.Clip;g.SetClip(Rectangle.Intersect(box,canvas.Clip));
            try{canvas.DrawText(AdenaInput+(AdenaFocused?"_":""),box.X+3,box.Y+1,usable?Color.White:DisabledColor,Color.Black);}finally{g.Clip=old;}
        }
        if(TextFrame is {Drawn:true} tf){
            var r=At(tf);var old=g.Clip;g.SetClip(Rectangle.Intersect(r,canvas.Clip));
            try{canvas.DrawText(NoticeText,r.X+4,r.Y+3,Color.White,Color.Black);}finally{g.Clip=old;}
        }
    }
    /// <summary>文字框：滑過格子時是那一格的名稱；否則是交易提示（可加物品且背包有選取時另附提示）。</summary>
    public string NoticeText{get{
        if(view==null)return "";
        if(HoverOffer is {} h)return h.Name;
        if(view.CanAdd&&view.Selected is {} s&&HoverOwnList)return "點這裡加入："+s.Name+(s.Count>1?" × "+s.Count.ToString(CultureInfo.InvariantCulture):"");
        return view.Notice;
    }}
    bool HoverOwnList;

    void Fire(Element e){
        if(view==null)return;
        if(e==Ok){if(view.CanConfirm)Source?.Confirm(view.Revision);return;}
        if(view.CanCancel)Source?.Cancel(view.Revision);   // 取消鈕、關閉 X：送 C86，窗等原服 S112 才關
    }

    /// <summary>把原生背包點選的物品整疊加入（C37）。false＝沒送（不能加、沒選、不在背包）。</summary>
    public bool AddSelected(){
        if(view is not {CanAdd:true,Selected:{} s}||s.Count==0||Source==null)return false;
        Source.Add(view.Revision,s.Id,s.Count);return true;
    }
    /// <summary>金幣框：送 C37（金幣物件＋框內數量）。數量不合法時不送、保留輸入。</summary>
    public bool SubmitAdena(){
        if(view is not {CanAdd:true,AdenaId:uint gold}||Source==null)return false;
        if(TransactionRules.AdenaAmount(AdenaInput,view.AdenaHeld) is not uint amount)return false;
        Source.Add(view.Revision,gold,amount);AdenaInput="";AdenaFocused=false;return true;
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        var hit=ButtonAt(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:
                Hover=hit;HoverOffer=OfferAt(e.Location);HoverOwnList=ListArea(0).Contains(e.Location);
                return Bounds.Contains(e.Location)||Pressed!=null||pressList||pressBox;
            case UiMouseKind.Wheel:
                for(byte side=0;side<2;side++)if(ListArea(side).Contains(e.Location)&&e.Delta!=0){scroll[side]=Math.Clamp(scroll[side]-Math.Sign(e.Delta),0,MaxScroll(side));return true;}
                return Bounds.Contains(e.Location);
            case UiMouseKind.Down:
                if(e.Button==MouseButtons.Left){
                    Hover=hit;Pressed=hit;
                    pressBox=hit==null&&AdenaBox.Contains(e.Location);
                    pressList=hit==null&&!pressBox&&ListArea(0).Contains(e.Location);
                    if(!pressBox&&Bounds.Contains(e.Location))AdenaFocused=false;
                }
                return Bounds.Contains(e.Location);
            case UiMouseKind.Up:{
                var pressed=Pressed;bool list=pressList,box=pressBox;Pressed=null;pressList=pressBox=false;Hover=hit;
                if(pressed!=null&&pressed==hit)Fire(pressed);
                else if(box&&AdenaBox.Contains(e.Location))AdenaFocused=view.CanAdd&&view.AdenaId!=null;
                else if(list&&ListArea(0).Contains(e.Location))AddSelected();
                return true;
            }
            default:return Bounds.Contains(e.Location);
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!e.Down)return false;
        if(AdenaFocused){
            if(e.KeyData==Keys.Back){if(AdenaInput.Length>0)AdenaInput=AdenaInput[..^1];return true;}
            if(e.KeyData==Keys.Enter){if(!e.Repeat)SubmitAdena();return true;}
            if(e.KeyData==Keys.Escape){AdenaFocused=false;return true;}
            // 數字不在這裡吃掉：走 WM_CHAR → Char()（同 HyperTextPanel 的數字框）。
        }
        if(e.KeyCode!=Keys.Escape)return false;
        if(view.CanCancel)Source?.Cancel(view.Revision);
        return true;
    }
    /// <summary>金幣框的數字（WM_CHAR）。不在輸入中或不是數字時不接。</summary>
    public bool Char(char c){
        if(view==null||!AdenaFocused||c is <'0' or >'9')return false;
        if(AdenaInput.Length<MaxDigits)AdenaInput=AdenaInput=="0"?c.ToString():AdenaInput+c;
        return true;
    }

    /// <summary>
    /// IUiClosablePanel（UiRoot 4 層上限，sub_A57C60 → vtable+36）：同關閉 X 送 C86（可取消時），並在這筆交易結束前不畫；
    /// 交易狀態本身不在這裡改，仍等原服 S112。
    /// </summary>
    public void Close(){
        if(view is not {} shown)return;
        // 先標記，再送取消：TradeUi 在送出後同步時就看得到 Dismissed，把 WinForms 面板叫回來。
        dismissed=shown.Generation;view=null;Hover=Pressed=null;HoverOffer=null;AdenaInput="";AdenaFocused=false;pressList=pressBox=false;
        if(shown.CanCancel)Source?.Cancel(shown.Revision);
    }

    /// <summary>標題列：清單以上、按鈕以外（推測）。</summary>
    public bool IsDragHandle(Point location){
        if(view==null||!Bounds.Contains(location)||ButtonAt(location)!=null)return false;
        return location.Y<Origin.Y+ListOffset.Y;
    }
    public void MoveTo(Point location){Origin=new(location.X-local.X,location.Y-local.Y);}
}
