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
//     （推測：原版是從背包拖進窗，原生背包沒有跨窗拖曳）。
//   - IUiClosablePanel：被 4 層上限擠掉（或其他關窗）時送 C86（同關閉 X），並在這筆交易結束前不再畫；
//     TradeUi 這時把 WinForms 交易面板整個叫回來（原生 UI 下的備援入口）。
// R9-K（T371／#855，FU9§12.1、gap-matrix 15.11，4:ExchangeUI）：
//   - 已驗證（FU9§12.1，TW16 sub_82DF30／sub_82DB50／sub_82E560／sub_82E870）：堆疊先問字串 140「要給幾個？ (1-%u， Enter=全部，
//     Esc=取消)」，空白＝全部、須 ≤ 持有量；單件直接送；送出是 C37（3.81 C_TradeAddItem `D D`＝物件、數量）；自己已放 16 件以上印 255。
//     本機 string-c.tbl（Text.pak，PakViewer 唯讀解出）：140、255 原文與下方 fallback 相同。
//   - 裁定（#818）：R4-C 的金幣輸入框是引擎自加的（ADENA_EXCHANGE_WINDOW 不是金幣框，TW16 不讀那段），**已移除**；
//     金幣是背包裡的一疊，選它再點自己的清單＝同一個 140 提示。
//   - 未做：不可交易 941、魔法娃娃使用中 1181、寵物項鍊 1188 的本機檢查（引擎背包沒有這些旗標；伺服器仍會擋）。
// R10-F（T390／#905，gap-matrix 15.12.6，4:ExchangeUI）：原版的 140 提示在**聊天輸入列**（sub_AC98E0，FU9§12.1、FU12§1.4），
//   主控裁定（#880 第 1 點）照原版做。AskOnChatLine＝true（TradeUi.cs 在原生 CENTER_WINDOW 可用時設定）：提示交給聊天列
//   （LinePrompt → ChatPanel），本窗的文字框不畫 140、不接數字／Enter／Backspace，Esc 也讓給聊天列（Esc＝取消提示，不是取消交易）。
//   AskOnChatLine＝false（沒有原生聊天窗或它出錯時的備援）：照 R9-K 畫在文字框（2008），數字由本窗的 Char()／Key() 接。

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
        int Generation=0,bool CanAdd=false,TransactionItemChoice? Selected=null) {
    public IReadOnlyList<TransactionOffer> Side(byte side)=>Offers.Where(o=>o.Side==side).ToArray();
    public long Adena(byte side)=>Offers.Where(o=>o.Side==side).Sum(o=>o.Adena);

    /// <summary>交易窗開著的狀態：原服已送 S52，且還沒收到 S112 結果。</summary>
    public static bool WindowState(string state)=>state is "open" or "adding" or "confirmed" or "attention" or "cancelling";

    /// <summary>從 WorldSession 轉出；沒有開著的交易窗時為 null。resolve 把原服名稱（$n 等）轉成顯示文字。</summary>
    public static TransactionView? From(WorldSession w,Func<string,string> resolve){
        if(!w.TradeOpened||!WindowState(w.TradeState))return null;
        TransactionItemChoice? selected=w.SelectedItem is {Count:>0} s&&s.Equipped!=true
            ?new(s.ObjectId,ItemText.Name(resolve(s.Name),s.Count),s.Count,ItemIconSource.IconOf(s.IconId,s.Bless)):null;
        return new(w.TradeRevision,w.TradePeerName,resolve(w.TradeNotice),
            w.TradeOffers.Select(o=>new TransactionOffer(o.Side,resolve(o.Name),TransactionRules.Adena(o.Name)??0,ItemIconSource.IconOf(o.Icon,o.Bless))).ToArray(),
            w.TradeCanConfirm&&!w.TradeOwnConfirmed,w.TradeCanCancel,w.TradeOwnConfirmed,
            w.TradeGeneration,w.TradeCanAdd,selected);
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
    /// <summary>
    /// R9-K：140 提示的答覆（sub_82E560）：空白＝全部；否則 1…持有量。C37 的數量欄是 D，另外上限 int.MaxValue（同 TradeUi.Add）。
    /// 其他（0、非數字、超過持有）為 null，不送。
    /// </summary>
    public static uint? Amount(string text,uint held){
        if(held==0)return null;
        uint cap=Math.Min(held,(uint)int.MaxValue);
        if(text.Length==0)return held<=cap?held:null;
        if(!ulong.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out ulong v)||v==0)return null;
        return v<=cap?(uint)v:null;
    }
    /// <summary>R9-K：堆疊（數量 &gt;1）先問 140；單件直接送（FU9§12.1）。</summary>
    public static bool AsksAmount(uint held)=>held>1;
    /// <summary>R9-K：自己這邊已放 16 件以上時不送、印 255（sub_82E870）。</summary>
    public const int MaxOwnOffers=16;
    public static bool TooManyOffers(int own)=>own>=MaxOwnOffers;
}

/// <summary>R9-K：交易數量的字串（本機 string-c.tbl 有：140、255；讀不到表時用同樣的原文）。</summary>
public static class TradeAmountText {
    public const int AskId=140,TooManyId=255;
    public const string AskFallback="要給幾個？ (1-%u， Enter=全部，Esc=取消)",TooManyFallback="你一次不能交換１６個以上的物品。";
    static string? Table(int id)=>ServerMessageText.Table is {} t&&id<t.Count&&t[id].Trim().Length>0?t[id]:null;
    /// <summary>140，%u＝持有量。</summary>
    public static string Ask(uint held)=>ServerMessageText.DisplayText((Table(AskId)??AskFallback).Replace("%u",held.ToString(CultureInfo.InvariantCulture))).Trim();
    /// <summary>255（原文開頭的 \f1 色碼拿掉）。</summary>
    public static string TooMany()=>ServerMessageText.DisplayText(Table(TooManyId)??TooManyFallback).Trim();
}

/// <summary>R9-K：等待答覆的 140 提示：哪一筆交易（Generation）、哪個物件、問的時候的持有量、名稱。</summary>
public sealed record TransactionAmountPrompt(int Generation,uint Item,string Name,uint Held) {
    public string Text=>TradeAmountText.Ask(Held);
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
    /// <summary>R9-K：140 答覆最多位數（uint 上限 10 位）。</summary>
    public const int MaxDigits=10;
    const string BaseName="SURF_RENEWAL_WIN_TRANSACT_BASE",CloseName="SURF_RENEWAL_BTN_QUIT_X",TextName="SURF_RENEWAL_WIN_TRANSACT_TEXT_FRAME",
        OkName="SURF_RENEWAL_BTN_TRANSACT_OK",CancelName="SURF_RENEWAL_BTN_TRANSACT_CANCEL",ListNode="ITEM_LIST_ADJUST";
    static readonly Color PromptColor=Color.FromArgb(255,230,120);

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
    /// <summary>R9-K：等待答覆的 140 提示（null＝沒有）與已輸入的數字。</summary>
    public TransactionAmountPrompt? Prompt{get;private set;}
    public string PromptInput{get;private set;}="";
    /// <summary>R9-K：上一次加物品沒送出的原因（255、數量不合法；文字框顯示，下一次操作或新的版本清掉）。</summary>
    public string? Refusal{get;private set;}
    /// <summary>滑鼠所在的格子／列（文字框改畫它的名稱）。</summary>
    public TransactionOffer? HoverOffer{get;private set;}
    /// <summary>Close() 之後，這筆交易（Generation）結束前不再畫；TradeUi 依此叫回 WinForms 面板。</summary>
    public bool Dismissed=>dismissed!=null;
    /// <summary>R10-F：true＝140 在聊天輸入列問（原版）；false＝備援，畫在本窗文字框（R9-K）。</summary>
    public bool AskOnChatLine{get;set;}
    /// <summary>R10-F：交給聊天列的 140 提示（AskOnChatLine 且有提示時；答覆＝AnswerPrompt、Esc＝CancelAmount）。</summary>
    public ChatLinePrompt? LinePrompt=>AskOnChatLine&&view!=null&&Prompt is {} p?new(p,p.Text,AnswerPrompt,()=>CancelAmount()):null;
    /// <summary>R10-F：聊天列送來的 140 答覆（sub_82E560：空白＝全部，前後空白不算）。</summary>
    public bool AnswerPrompt(string typed){
        if(Prompt==null)return false;
        PromptInput=typed.Trim();return SubmitAmount();
    }

    TransactionView? view;Rectangle local;Rectangle ownLocal,peerLocal;readonly int[] scroll=new int[2];int shownRevision=-1;
    LegacyCellGrid ownGrid=LegacyCellGrid.Empty,peerGrid=LegacyCellGrid.Empty;
    int? dismissed;bool pressList;
    void ClearPrompt(){Prompt=null;PromptInput="";}

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
        // R4-C 推測：圖示格填滿各清單區。
        var pitch=new Size(LegacyCellGrid.IconPitch,LegacyCellGrid.IconPitch);
        ownGrid=ownLocal.IsEmpty?LegacyCellGrid.Empty:LegacyCellGrid.Fit(ownLocal.Left,ownLocal.Top,ownLocal.Right,ownLocal.Bottom,pitch);
        peerGrid=peerLocal.IsEmpty?LegacyCellGrid.Empty:LegacyCellGrid.Fit(peerLocal.Left,peerLocal.Top,peerLocal.Right,peerLocal.Bottom,pitch);
        view=null;shownRevision=-1;scroll[0]=scroll[1]=0;Hover=Pressed=null;HoverOffer=null;dismissed=null;ClearPrompt();Refusal=null;pressList=false;
    }
    IEnumerable<Element> All=>new[]{Base,CloseButton,TextFrame,Ok,Cancel}.OfType<Element>();
    IEnumerable<Element> Buttons=>new[]{CloseButton,Ok,Cancel}.OfType<Element>();

    /// <summary>清單區（邏輯座標；0＝自己（上）、1＝對方（下））。</summary>
    public Rectangle ListArea(byte side){var r=side==0?ownLocal:peerLocal;return r.IsEmpty?r:new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);}
    public int VisibleRows=>Math.Max(0,ownLocal.Height/RowHeight);
    /// <summary>圖示格（相對窗原點；0＝自己、1＝對方）。</summary>
    public LegacyCellGrid Grid(byte side)=>side==0?ownGrid:peerGrid;
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
        if(v==null){view=null;Hover=Pressed=null;HoverOffer=null;scroll[0]=scroll[1]=0;shownRevision=-1;ClearPrompt();Refusal=null;pressList=false;return;}
        bool fresh=view==null;view=v;
        if(fresh){scroll[0]=scroll[1]=0;ClearPrompt();Refusal=null;}
        // 新列進來時捲到最後，讓剛加入的物品看得到。
        if(v.Revision!=shownRevision){shownRevision=v.Revision;Refusal=null;if(!fresh){scroll[0]=MaxScroll(0);scroll[1]=MaxScroll(1);}}
        scroll[0]=Math.Clamp(scroll[0],0,MaxScroll(0));scroll[1]=Math.Clamp(scroll[1],0,MaxScroll(1));
        // 換了一筆交易，或已經不能加物品（加入中、已確定、取消中）：提示作廢。
        if(Prompt is {} p&&(p.Generation!=v.Generation||!v.CanAdd))ClearPrompt();
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
                        if(n.Length>0)canvas.DrawText(n,cell.Right-UiText.Measure(g,n).Width,cell.Bottom-UiText.PixelHeight,Color.White,Color.Black);
                    }
                    else canvas.DrawText(o.Name,area.X+2,area.Y+i++*RowHeight,Color.White,Color.Black);
                }
                long gold=view.Adena(side);
                if(gold>0){string text=gold.ToString("N0",CultureInfo.InvariantCulture);int w=UiText.Measure(g,text).Width;canvas.DrawText(text,area.Right-2-w,area.Bottom-RowHeight,Color.Gold,Color.Black);}
            }finally{g.Clip=old;}
        }
        if(TextFrame is {Drawn:true} tf){
            var r=At(tf);var old=g.Clip;g.SetClip(Rectangle.Intersect(r,canvas.Clip));
            try{
                if(Prompt!=null&&!AskOnChatLine){
                    // R9-K 備援：140 提示與輸入畫在文字框（原版在聊天輸入列，R10-F 改由聊天列問，見檔頭）。
                    canvas.DrawText(NoticeText,r.X+4,r.Y+3,PromptColor,Color.Black);
                    canvas.DrawText(PromptInput+"_",r.X+4,r.Y+3+RowHeight,Color.White,Color.Black);
                }
                else canvas.DrawText(NoticeText,r.X+4,r.Y+3,Color.White,Color.Black);
            }finally{g.Clip=old;}
        }
    }
    /// <summary>文字框：140 提示（等待答覆時）；滑過格子時是那一格的名稱；沒送出的原因；否則是交易提示（可加物品且背包有選取時另附提示）。</summary>
    public string NoticeText{get{
        if(view==null)return "";
        if(Prompt is {} p&&!AskOnChatLine)return p.Text;
        if(HoverOffer is {} h)return h.Name;
        if(Refusal is {} r)return r;
        if(view.CanAdd&&view.Selected is {} s&&HoverOwnList)return "點這裡加入："+s.Name+(s.Count>1?" × "+s.Count.ToString(CultureInfo.InvariantCulture):"");
        return view.Notice;
    }}
    bool HoverOwnList;

    void Fire(Element e){
        if(view==null)return;
        if(e==Ok){if(view.CanConfirm)Source?.Confirm(view.Revision);return;}
        if(view.CanCancel)Source?.Cancel(view.Revision);   // 取消鈕、關閉 X：送 C86，窗等原服 S112 才關
    }

    /// <summary>
    /// 把原生背包點選的物品加入交易（R9-K，sub_82DF30）：堆疊（含金幣）先開 140 提示、回 false（還沒送）；單件直接送 C37、回 true。
    /// 不能加、沒選、沒有來源時什麼都不做、回 false。
    /// </summary>
    public bool AddSelected(){
        if(view is not {CanAdd:true,Selected:{} s}||s.Count==0||Source==null)return false;
        Refusal=null;
        if(TransactionRules.AsksAmount(s.Count)){Prompt=new(view.Generation,s.Id,s.Name,s.Count);PromptInput="";return false;}
        ClearPrompt();
        return Send(s.Id,1);
    }
    /// <summary>
    /// 140 的答覆（Enter）：空白＝全部，否則 1…持有量；合法就送 C37（sub_82E870），不合法不送。兩種情形提示都結束（同聊天輸入列答一次就關）。
    /// </summary>
    public bool SubmitAmount(){
        if(Prompt is not {} p)return false;
        string typed=PromptInput;ClearPrompt();
        if(view is not {CanAdd:true} v||v.Generation!=p.Generation||Source==null)return false;
        if(TransactionRules.Amount(typed,p.Held) is not uint amount){Refusal="未送出：數量不合法（1-"+p.Held.ToString(CultureInfo.InvariantCulture)+"）。";return false;}
        return Send(p.Item,amount);
    }
    /// <summary>Esc：放棄 140 提示，不送（交易窗不關、不送 C86）。</summary>
    public bool CancelAmount(){if(Prompt==null)return false;ClearPrompt();return true;}
    /// <summary>送出前的 255 檢查（自己已放 16 件以上）；通過才送 C37（資格由 Trade.cs 再驗一次）。</summary>
    bool Send(uint item,uint count){
        if(view==null||Source==null)return false;
        if(TransactionRules.TooManyOffers(view.Side(0).Count)){Refusal=TradeAmountText.TooMany();return false;}
        Source.Add(view.Revision,item,count);return true;
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        var hit=ButtonAt(e.Location);
        switch(e.Kind){
            case UiMouseKind.Move:
                Hover=hit;HoverOffer=OfferAt(e.Location);HoverOwnList=ListArea(0).Contains(e.Location);
                return Bounds.Contains(e.Location)||Pressed!=null||pressList;
            case UiMouseKind.Wheel:
                for(byte side=0;side<2;side++)if(ListArea(side).Contains(e.Location)&&e.Delta!=0){scroll[side]=Math.Clamp(scroll[side]-Math.Sign(e.Delta),0,MaxScroll(side));return true;}
                return Bounds.Contains(e.Location);
            case UiMouseKind.Down:
                if(e.Button==MouseButtons.Left){
                    Hover=hit;Pressed=hit;
                    pressList=hit==null&&ListArea(0).Contains(e.Location);
                }
                return Bounds.Contains(e.Location);
            case UiMouseKind.Up:{
                var pressed=Pressed;bool list=pressList;Pressed=null;pressList=false;Hover=hit;
                if(pressed!=null&&pressed==hit)Fire(pressed);
                else if(list&&ListArea(0).Contains(e.Location))AddSelected();   // 提示中再點＝換成目前選取的物品重問
                return true;
            }
            default:return Bounds.Contains(e.Location);
        }
    }

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!e.Down)return false;
        // R10-F：140 在聊天列問時，數字、Enter、Backspace、Esc 都讓給聊天列（ChatPanel）。
        if(Prompt!=null&&AskOnChatLine)return false;
        if(Prompt!=null){
            if(e.KeyData==Keys.Back){if(PromptInput.Length>0)PromptInput=PromptInput[..^1];return true;}
            if(e.KeyData==Keys.Enter){if(!e.Repeat)SubmitAmount();return true;}
            if(e.KeyData==Keys.Escape){CancelAmount();return true;}   // 140 的 Esc＝取消提示，不是取消交易
            // 數字不在這裡吃掉：走 WM_CHAR → Char()（同 HyperTextPanel 的數字框）。
        }
        if(e.KeyCode!=Keys.Escape)return false;
        if(view.CanCancel)Source?.Cancel(view.Revision);
        return true;
    }
    /// <summary>140 答覆的數字（WM_CHAR）。沒有提示或不是數字時不接。</summary>
    public bool Char(char c){
        if(view==null||Prompt==null||AskOnChatLine||c is <'0' or >'9')return false;
        if(PromptInput.Length<MaxDigits)PromptInput=PromptInput=="0"?c.ToString():PromptInput+c;
        return true;
    }

    /// <summary>
    /// IUiClosablePanel（UiRoot 4 層上限，sub_A57C60 → vtable+36）：同關閉 X 送 C86（可取消時），並在這筆交易結束前不畫；
    /// 交易狀態本身不在這裡改，仍等原服 S112。
    /// </summary>
    public void Close(){
        if(view is not {} shown)return;
        // 先標記，再送取消：TradeUi 在送出後同步時就看得到 Dismissed，把 WinForms 面板叫回來。
        dismissed=shown.Generation;view=null;Hover=Pressed=null;HoverOffer=null;ClearPrompt();Refusal=null;pressList=false;
        if(shown.CanCancel)Source?.Cancel(shown.Revision);
    }

    /// <summary>標題列：清單以上、按鈕以外（推測）。</summary>
    public bool IsDragHandle(Point location){
        if(view==null||!Bounds.Contains(location)||ButtonAt(location)!=null)return false;
        return location.Y<Origin.Y+ListOffset.Y;
    }
    public void MoveTo(Point location){Origin=new(location.X-local.X,location.Y-local.Y);}
}
