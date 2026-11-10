using System.Text;
namespace P06.NativeUi;

// R5-G（T312／#739）：原生公告板的背景、清單列、內文與發文窗。按鈕層仍是 M7-U 的 BoardPanels.cs（本任務不改）。
// 依據：TW1608262200.bin（SHA-256 e12949cf…41a2）Capstone 靜態反組譯，報告 docs/reports/issue-739.md。
// 三窗都在 sub_8C7C80 建立，不讀 XML，所以和 BoardPanels.cs 一樣由 BoardUi.cs 在開窗時掛上（不走 UiPanelRegistry）。
//
// 已驗證（反組譯）：
//   清單窗繪製 sub_7CC7C0（vtable 0x1172F44 +8）：
//     - 背景 surf 656（[this+0x44]==1 時 4732），矩形 (0,0,238,350) 畫在 UI 原點（dword_154524C／dword_1545250）。
//     - 每頁 8 列；第 i 列 y＝50＋30·i；選取列先在 (18, y) 畫 surf 657。
//     - 列文字（sub_83F260，座標＋UI 原點，寬度用完就截斷：雙位元組字 12 px、單位元組 6 px）調色盤 8：
//       編號 "%04d"（id % 10000）(30, y) 寬 24；作者 (60, y) 寬 72；日期 (135, y) 寬 48；標題 (30, y+12) 寬 160。
//     - 板名 [this+0x28] 非空時畫在 (30,15)（調色盤 14），選到時 (18,15) 畫 657。3.81 的 S_BOARD 沒有板名 → 不畫。
//   清單窗滑鼠 sub_7CD080（vtable +44）：只收 (0,0,238,350) 內；左鍵按下 → 列＝(y−50)/30（< 8 且有資料）選取，
//     再按同列＝取消；左鍵雙擊 → 選取並閱讀（sub_7CD970）；右鍵雙擊 → 關窗（sub_A59AB0）。只看 y，不看 x。
//   閱讀窗繪製 sub_7CCB40（vtable 0x1172EC4 +8）：背景 670（[+0x458]==1 時 4733）(0,0,238,343)；
//     字串 542「作者:」(20,20)、543「討論號碼:」(20,35)、541「題目:」(20,50)，調色盤 1，寬 180；
//     值畫在標籤後 (strlen(標籤)+1)·6 px：作者、"%04d - %s"（id % 10000、日期），調色盤 8，寬 180−標籤寬。
//     閱讀窗建構 sub_7CBA70：標題框 (20+標籤寬, 50) 180×16、內文框 (20,67) 180×228，字色調色盤 8。
//   發文窗繪製 sub_7CCD90（vtable 0x1172F04 +8）：背景 670（[+0x1c]==1 時 4733）；surf 672 (56,312)、676 (110,312)
//     當裝飾畫出；541「題目:」(20,20) 調色盤 1；費用不為 0 時 540「費用為金幣 %d。」(18,269) 寬 160 調色盤 8。
//     發文窗建構 sub_7CBE90：標題框 (20+標籤寬, 20) (180−標籤寬)×18、上限 20 byte；內文框 (20,38) 180×228、上限 512 byte；
//     按鈕 686 (159,302)＝送出（sub_7CDC70）、678 (213,297)＝關窗、674 (90,299)＝返回（sub_7CDDD0：關掉發文窗）。
//   清單窗 662「Write」回呼 sub_7CDE00：開發文窗（不是直接送出）。送出 sub_7CDC70：標題空白 → 系統訊息 544「請輸入題目。」，
//     窗不關；否則送 C_BOARD_WRITE，關發文窗並回到清單。
//   R7-F（#808）sub_7CDE00（VA 0x7CDE00–0x7CDED1）全文：
//     [this+0x40]≠0 → 什麼都不做；費用 word [this+0x20]≠0 且 sub_992F80（sub_4F9FF0 單例 +0x54 的道具容器，推定為背包金幣，
//     回傳 64 位元）＜ 費用 → sub_4EAFF0(189)「金幣不足。」經 sub_7DC850(字, palette[24], palette[14]) 印在聊天列，不開窗；
//     否則把 [this+0xc]、費用抄進發文窗（[+0x1a]＝0）並 sub_A59AB0 開窗。費用 0 時不看金幣。
//   UI 原點 dword_154524C／dword_1545250 在 800×600 為 0（ui-layout.md V5）→ 原版三窗都在螢幕 (0,0)。
//
// 推定／引擎決定（不是原版資料）：
//   - 窗的位置：跟 BoardPanels.cs 的按鈕窗共用 Origin（R7-F 起 DefaultOrigin＝(0,0)，同原版）。
//   - 金幣：引擎用背包金幣（ShopTrade.Holding）；背包還沒載入時不擋，交給伺服器。[this+0x40] 的寫入者沒查（不做）。
//   - 清單窗與閱讀窗的內容畫在 UiLayer.Bar：既有按鈕窗（Window 層）才能疊在背景上，且不多占 4 層視窗堆疊。
//     兩者合併成同一個面板後應改回 Window 層。發文窗要蓋住清單按鈕，所以在 Window 層。
//   - 多行內文框的行高 12 px、換行規則（寬 180 px，同 sub_83F260 的 12／6 px 字寬）沒有反組譯 sub_AC3BB0 確認（推定）。
//   - 發文窗的輸入焦點：點框切換、Tab 切換、標題框按 Enter 跳到內文（引擎決定）；字元走 IUiTextInput（WM_CHAR，含 IME）。
//   - 送出仍經 BoardUi 既有的確認框與長度／Big5 檢查（引擎安全規則，原版沒有確認框）。
//   - 4732／4733 背景的觸發條件（[+0x44]／[+0x458]／[+0x1c]）沒有查到寫入者，只記成常數，一律用 656／670。

/// <summary>反組譯得到的公告板版面（座標都是窗內偏移）。</summary>
public static class BoardContentLayout {
    public static readonly Size ListSize=new(238,350),ReadSize=new(238,343),WriteSize=new(238,343);
    public const int ListBackground=656,ListBackgroundAlt=4732,ReadBackground=670,ReadBackgroundAlt=4733,RowHighlight=657;
    public const int RowsPerPage=8,RowX=30,FirstRowY=50,RowPitch=30,HighlightX=18,TitleLineDy=12;
    public const int IdDx=0,IdWidth=24,AuthorDx=30,AuthorWidth=72,DateDx=105,DateWidth=48,TitleWidth=160;
    /// <summary>原版調色盤 dword_1369D70 的索引：1＝標籤、8＝列與內文、14＝板名。</summary>
    public const int LabelColor=1,ValueColor=8,BoardTitleColor=14;
    // 閱讀窗
    public static readonly Point AuthorLabel=new(20,20),NumberLabel=new(20,35),TitleLabel=new(20,50);
    public const int LabelWidth=180,ReadTitleBoxY=50,ReadTitleBoxHeight=16;
    public static readonly Rectangle ReadContentBox=new(20,67,180,228);
    // 發文窗
    public static readonly Point WriteTitleLabel=new(20,20),FeeAt=new(18,269);
    public const int WriteTitleBoxY=20,WriteTitleBoxHeight=18,FeeWidth=160,TitleMaxBytes=20,ContentMaxBytes=512;
    public static readonly Rectangle WriteContentBox=new(20,38,180,228);
    /// <summary>發文窗當裝飾畫出的圖（sub_7CCD90 用 sub_542370 直接畫，不是按鈕）。</summary>
    public static readonly (int Surf,Point At)[] WriteDecorations=[(672,new(56,312)),(676,new(110,312))];
    /// <summary>發文窗按鈕（sub_7CBE90）。</summary>
    public static readonly BoardWriteButton[] WriteButtons=[
        new(686,159,302,BoardWriteAction.Submit),new(678,213,297,BoardWriteAction.Close),new(674,90,299,BoardWriteAction.Back)];
    /// <summary>推定：多行內文框的行高（沒有反組譯 sub_AC3BB0 確認）。</summary>
    public const int ContentLineHeight=12;
}

public enum BoardWriteAction { Submit, Close, Back }
public sealed record BoardWriteButton(int Surf,int X,int Y,BoardWriteAction Action);
/// <summary>一段要畫的文字（螢幕邏輯座標、調色盤索引）；Paint 與離線測試共用。</summary>
public sealed record BoardTextItem(string Text,int X,int Y,int Color);

/// <summary>sub_83F260 的字寬規則與公告板用到的字串表文字。</summary>
public static class BoardText {
    public const int StringFee=540,StringTitle=541,StringAuthor=542,StringNumber=543,StringNeedTitle=544;
    /// <summary>R7-F：sub_7CDE00 金幣不足時印的字串 189（「\f1金幣不足。」）。</summary>
    public const int StringNoGold=189;
    /// <summary>sub_7CDE00 呼叫 sub_7DC850 的第 1／第 2 色：palette[24]、palette[14]。</summary>
    public const int NoGoldColor=24,NoGoldSecondColor=14;
    /// <summary>189 的顯示文字：和伺服器訊息同一張表、同樣去掉 \f 色碼（ServerMessageText；沒有表時用已查證的原文）。</summary>
    public static string NoGoldText=>ServerMessageText.Decode(StringNoGold,[]).Text;
    static readonly Encoding Cp950=CreateCp950();
    static Encoding CreateCp950(){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950);}
    static readonly Dictionary<int,string> Fallback=new(){
        [StringFee]="費用為金幣 %d。",[StringTitle]="題目:",[StringAuthor]="作者:",[StringNumber]="討論號碼:",[StringNeedTitle]="請輸入題目。"};
    /// <summary>字串表第 id 筆（string-c.tbl，sub_4EAFF0）；表不存在或空白時用本機表的原文。</summary>
    public static string Text(int id){
        if(ServerMessageText.Table is {} t&&id>=0&&id<t.Count){string s=t[id];if(s.Trim().Length>0)return s;}
        return Fallback.GetValueOrDefault(id,"");
    }
    /// <summary>CP950 位元組數（無法編碼的字算 1 byte，和 '?' 相同）。</summary>
    public static int Bytes(string s)=>Cp950.GetByteCount(s);
    /// <summary>單字寬度：雙位元組 12 px，其他 6 px（sub_83F260 以 IsDBCSLeadByteEx 判斷）。</summary>
    public static int CharWidth(char c)=>c<0x80?6:Cp950.GetByteCount(c.ToString())>1?12:6;
    /// <summary>sub_83F260：寬度扣到負數就停，之前的字全畫。</summary>
    public static string Clip(string text,int width){
        int i=0;
        for(;i<text.Length;i++){
            int w=char.IsSurrogatePair(text,i)?12:CharWidth(text[i]);
            if((width-=w)<0)break;
            if(char.IsSurrogatePair(text,i))i++;
        }
        return text[..Math.Min(i,text.Length)];
    }
    /// <summary>標籤後的值的起點：(strlen(標籤)+1)·6（sub_7CCB40、sub_7CBA70）。</summary>
    public static int LabelWidth(string label)=>(Bytes(label)+1)*6;
    /// <summary>推定的多行換行：遇換行字元換行，超過寬度換行。</summary>
    public static IReadOnlyList<string> Wrap(string text,int width){
        var lines=new List<string>();
        foreach(var raw in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n')){
            string rest=raw;
            do{
                string part=Clip(rest,width);
                if(part.Length==0&&rest.Length>0)part=rest[..(char.IsSurrogatePair(rest,0)?2:1)];
                lines.Add(part);rest=rest[part.Length..];
            }while(rest.Length>0);
        }
        return lines;
    }
    /// <summary>"%04d"：id % 10000，補零到 4 位。</summary>
    public static string Number(uint id)=>(id%10000).ToString("0000",System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>內容面板需要的、按鈕層（IBoardPanelHost）以外的動作。實作在 BoardUi.cs。</summary>
public interface IBoardContentHost {
    /// <summary>既有公告板視窗目前選取的文章編號（閱讀鈕用它）。</summary>
    uint? SelectedRow { get; }
    void SelectRow(uint? id);
    /// <summary>送出文章：走既有的確認與長度／Big5 檢查。</summary>
    void Submit(string title,string content);
    /// <summary>原版印到系統訊息的提示（例如 544）。</summary>
    void Notice(string text);
    /// <summary>R7-F：背包金幣；null＝不知道（背包未載入），不擋。</summary>
    long? Gold=>null;
    /// <summary>R7-F：sub_7CDE00 經 sub_7DC850 印在聊天列的訊息（189）。預設同 Notice。</summary>
    void ChatNotice(string text,int color)=>Notice(text);
}

/// <summary>三個內容面板；位置、模式跟按鈕層 BoardPanelSet 共用。</summary>
public sealed class BoardContentSet {
    public BoardPanelSet Buttons{get;}
    public IBoardContentHost Host{get;}
    public BoardListContent List{get;}
    public BoardReadContent Read{get;}
    public BoardWriteContent Write{get;}
    public BoardContentSet(BoardPanelSet buttons,IBoardContentHost host){Buttons=buttons;Host=host;List=new(this);Read=new(this);Write=new(this);}
    public IEnumerable<IUiPanel> Panels=>[List,Read,Write];
    public void LoadSurfaces(SurfCatalog surfs)=>LoadSurfaces(surfs.Get);
    internal void LoadSurfaces(Func<int,UiSurface?> surf){List.Load(surf);Read.Load(surf);Write.Load(surf);}
    internal BoardView? View=>Buttons.Host.Board;
    /// <summary>
    /// 原版 662 回呼 sub_7CDE00：開發文窗。只有清單或文章已到時才開。
    /// R7-F：費用不為 0 且金幣不足 → 聊天列印 189，不開窗（費用 0 不看金幣；金幣不明時不擋）。
    /// </summary>
    public bool OpenWrite(){
        Buttons.Sync();
        if(View is not {Kind:"list" or "article",CanAct:true} v)return false;
        if(ShortOfGold(v.Fee,Host.Gold)){Host.ChatNotice(BoardText.NoGoldText,BoardText.NoGoldColor);return false;}
        Write.Open(v.Fee);return true;
    }
    /// <summary>sub_7CDE00 的比較：fee≠0 且 gold＜fee（無號 64 位元）。</summary>
    public static bool ShortOfGold(ushort fee,long? gold)=>fee!=0&&gold is {} g&&g<fee;
}

/// <summary>BoardListWindow 的背景與清單列（sub_7CC7C0）。</summary>
public sealed class BoardListContent(BoardContentSet set):IUiClosablePanel {
    UiSurface? background,highlight;
    public const string PanelId="BoardListWindow.Content";
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Bar;
    public Point Origin=>set.Buttons.List.Origin;
    public Rectangle Bounds=>new(Origin,BoardContentLayout.ListSize);
    public bool Visible=>set.Buttons.Mode=="list";
    public UiSurface? Background=>background;
    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(surfs.Get);
    internal void Load(Func<int,UiSurface?> surf){background=surf(BoardContentLayout.ListBackground);highlight=surf(BoardContentLayout.RowHighlight);}
    public void Update(IUiGameState state)=>set.Buttons.Sync();
    public IReadOnlyList<BoardRow> Rows=>set.View?.Rows.Take(BoardContentLayout.RowsPerPage).ToArray()??[];
    public int SelectedIndex{get{var id=set.Host.SelectedRow;if(id==null)return -1;var rows=Rows;for(int i=0;i<rows.Count;i++)if(rows[i].Id==id)return i;return -1;}}
    public int RowY(int i)=>Origin.Y+BoardContentLayout.FirstRowY+BoardContentLayout.RowPitch*i;
    /// <summary>sub_7CD080：列＝(y−50)/30，只看 y；沒有資料的列不算。</summary>
    public int RowAt(Point p){
        if(!Bounds.Contains(p))return -1;
        int dy=p.Y-Origin.Y-BoardContentLayout.FirstRowY;if(dy<0)return -1;
        int i=dy/BoardContentLayout.RowPitch;
        return i<BoardContentLayout.RowsPerPage&&i<Rows.Count?i:-1;
    }
    public IReadOnlyList<BoardTextItem> Texts(){
        var list=new List<BoardTextItem>();var rows=Rows;int x=Origin.X+BoardContentLayout.RowX;
        for(int i=0;i<rows.Count;i++){
            int y=RowY(i);var r=rows[i];
            list.Add(new(BoardText.Clip(BoardText.Number(r.Id),BoardContentLayout.IdWidth),x+BoardContentLayout.IdDx,y,BoardContentLayout.ValueColor));
            list.Add(new(BoardText.Clip(r.Author,BoardContentLayout.AuthorWidth),x+BoardContentLayout.AuthorDx,y,BoardContentLayout.ValueColor));
            list.Add(new(BoardText.Clip(r.Date,BoardContentLayout.DateWidth),x+BoardContentLayout.DateDx,y,BoardContentLayout.ValueColor));
            list.Add(new(BoardText.Clip(r.Title,BoardContentLayout.TitleWidth),x,y+BoardContentLayout.TitleLineDy,BoardContentLayout.ValueColor));
        }
        return list;
    }
    public void Paint(UiCanvas canvas){
        canvas.DrawSurface(background,Origin.X,Origin.Y);
        int sel=SelectedIndex;
        if(sel>=0)canvas.DrawSurface(highlight,Origin.X+BoardContentLayout.HighlightX,RowY(sel));
        foreach(var t in Texts())if(t.Text.Length>0)canvas.DrawText(t.Text,t.X,t.Y,ChatPalette.Color(t.Color));
    }
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        bool inside=Bounds.Contains(e.Location);
        if(!Visible||!inside)return false;
        if(e.Kind==UiMouseKind.Down&&e.Button==MouseButtons.Left){
            int i=RowAt(e.Location);
            if(i>=0){
                uint id=Rows[i].Id;
                if(e.Clicks>=2){set.Host.SelectRow(id);set.Buttons.Host.Press("read");}
                else set.Host.SelectRow(set.Host.SelectedRow==id?null:id);
            }
        }else if(e.Kind==UiMouseKind.Down&&e.Button==MouseButtons.Right&&e.Clicks>=2)set.Buttons.Host.Press("close");
        return true;
    }
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    /// <summary>視窗堆疊擠出時：關掉整個公告板（同 658 X）。</summary>
    public void Close()=>set.Buttons.Host.Press("close");
}

/// <summary>BoardReadWindow 的背景、標籤與內文（sub_7CCB40、sub_7CBA70）。</summary>
public sealed class BoardReadContent(BoardContentSet set):IUiClosablePanel {
    UiSurface? background;
    public const string PanelId="BoardReadWindow.Content";
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Bar;
    public Point Origin=>set.Buttons.Read.Origin;
    public Rectangle Bounds=>new(Origin,BoardContentLayout.ReadSize);
    public bool Visible=>set.Buttons.Mode=="read"&&set.View?.Article!=null;
    public UiSurface? Background=>background;
    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(surfs.Get);
    internal void Load(Func<int,UiSurface?> surf)=>background=surf(BoardContentLayout.ReadBackground);
    public void Update(IUiGameState state)=>set.Buttons.Sync();
    public IReadOnlyList<BoardTextItem> Texts(){
        var list=new List<BoardTextItem>();
        if(set.View?.Article is not {} a)return list;
        int ox=Origin.X,oy=Origin.Y;
        void Labelled(int stringId,Point at,string? value){
            string label=BoardText.Text(stringId);int w=BoardText.LabelWidth(label);
            list.Add(new(BoardText.Clip(label,BoardContentLayout.LabelWidth),ox+at.X,oy+at.Y,BoardContentLayout.LabelColor));
            if(value!=null)list.Add(new(BoardText.Clip(value,BoardContentLayout.LabelWidth-w),ox+at.X+w,oy+at.Y,BoardContentLayout.ValueColor));
        }
        Labelled(BoardText.StringAuthor,BoardContentLayout.AuthorLabel,a.Author);
        Labelled(BoardText.StringNumber,BoardContentLayout.NumberLabel,BoardText.Number(a.Id)+" - "+a.Date);
        Labelled(BoardText.StringTitle,BoardContentLayout.TitleLabel,null);
        // 標題框：sub_7CBA70 在 (20+標籤寬, 50) 180×16。
        int tx=BoardContentLayout.TitleLabel.X+BoardText.LabelWidth(BoardText.Text(BoardText.StringTitle));
        list.Add(new(BoardText.Clip(a.Title,BoardContentLayout.LabelWidth),ox+tx,oy+BoardContentLayout.ReadTitleBoxY,BoardContentLayout.ValueColor));
        var box=BoardContentLayout.ReadContentBox;
        var lines=BoardText.Wrap(a.Content,box.Width);int max=box.Height/BoardContentLayout.ContentLineHeight;
        for(int i=0;i<lines.Count&&i<max;i++)list.Add(new(lines[i],ox+box.X,oy+box.Y+i*BoardContentLayout.ContentLineHeight,BoardContentLayout.ValueColor));
        return list;
    }
    public void Paint(UiCanvas canvas){
        canvas.DrawSurface(background,Origin.X,Origin.Y);
        foreach(var t in Texts())if(t.Text.Length>0)canvas.DrawText(t.Text,t.X,t.Y,ChatPalette.Color(t.Color));
    }
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>Visible&&Bounds.Contains(e.Location);
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    public void Close()=>set.Buttons.Host.Press("close");
}

/// <summary>
/// BoardWriteWindow（sub_7CBE90、sub_7CCD90）：背景、標籤、費用行、標題與內文輸入框和 686／678／674 三顆鈕。
/// 由清單窗的 Write 鈕開啟（BoardContentSet.OpenWrite）。
/// </summary>
public sealed class BoardWriteContent(BoardContentSet set):IUiClosablePanel,IUiTextInput {
    UiSurface? background;readonly UiSurface?[] decorations=new UiSurface?[BoardContentLayout.WriteDecorations.Length];
    readonly UiSurface?[] buttons=new UiSurface?[BoardContentLayout.WriteButtons.Length];
    readonly StringBuilder title=new(),content=new();
    bool open;int pressed=-1;
    public const string PanelId="BoardWriteWindow.Content";
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin=>set.Buttons.List.Origin;
    public Rectangle Bounds=>new(Origin,BoardContentLayout.WriteSize);
    public bool Visible=>open&&set.Buttons.Mode!="none";
    public bool IsOpen=>open;
    public ushort Fee{get;private set;}
    public string Title=>title.ToString();
    public string Content=>content.ToString();
    /// <summary>0＝標題框，1＝內文框。</summary>
    public int Focus{get;private set;}
    public UiSurface? Background=>background;
    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(surfs.Get);
    internal void Load(Func<int,UiSurface?> surf){
        background=surf(BoardContentLayout.ReadBackground);
        for(int i=0;i<decorations.Length;i++)decorations[i]=surf(BoardContentLayout.WriteDecorations[i].Surf);
        for(int i=0;i<buttons.Length;i++)buttons[i]=surf(BoardContentLayout.WriteButtons[i].Surf);
    }
    public void Open(ushort fee){open=true;Fee=fee;title.Clear();content.Clear();Focus=0;pressed=-1;}
    public void Close(){open=false;pressed=-1;}
    public void Update(IUiGameState state){set.Buttons.Sync();if(set.Buttons.Mode=="none")Close();}

    int TitleLabelWidth=>BoardText.LabelWidth(BoardText.Text(BoardText.StringTitle));
    public Rectangle TitleBox=>new(Origin.X+BoardContentLayout.WriteTitleLabel.X+TitleLabelWidth,Origin.Y+BoardContentLayout.WriteTitleBoxY,
        Math.Max(0,BoardContentLayout.LabelWidth-TitleLabelWidth),BoardContentLayout.WriteTitleBoxHeight);
    public Rectangle ContentBox{get{var b=BoardContentLayout.WriteContentBox;return new(Origin.X+b.X,Origin.Y+b.Y,b.Width,b.Height);}}
    public Rectangle ButtonRect(int i)=>buttons[i] is {} s?new(Origin.X+BoardContentLayout.WriteButtons[i].X,Origin.Y+BoardContentLayout.WriteButtons[i].Y,s.Width,s.Height):Rectangle.Empty;
    public int HitButton(Point p){for(int i=0;i<buttons.Length;i++)if(ButtonRect(i).Contains(p))return i;return -1;}

    public IReadOnlyList<BoardTextItem> Texts(){
        var list=new List<BoardTextItem>();int ox=Origin.X,oy=Origin.Y;
        string label=BoardText.Text(BoardText.StringTitle);
        list.Add(new(BoardText.Clip(label,BoardContentLayout.LabelWidth),ox+BoardContentLayout.WriteTitleLabel.X,oy+BoardContentLayout.WriteTitleLabel.Y,BoardContentLayout.LabelColor));
        if(Fee!=0)list.Add(new(BoardText.Clip(ClanWarehouseHistoryText.CFormat(BoardText.Text(BoardText.StringFee),(int)Fee),BoardContentLayout.FeeWidth),
            ox+BoardContentLayout.FeeAt.X,oy+BoardContentLayout.FeeAt.Y,BoardContentLayout.ValueColor));
        var tb=TitleBox;
        // 引擎：焦點框尾端畫 "_" 當游標（原版游標樣式待驗證）。
        list.Add(new(BoardText.Clip(Title+(Focus==0?"_":""),tb.Width),tb.X,tb.Y,BoardContentLayout.ValueColor));
        var cb=ContentBox;var lines=BoardText.Wrap(Content+(Focus==1?"_":""),cb.Width);int max=cb.Height/BoardContentLayout.ContentLineHeight;
        // 超出框的行數時顯示最後幾行，讓正在輸入的那一行看得到（引擎決定）。
        int first=Math.Max(0,lines.Count-max);
        for(int i=first;i<lines.Count;i++)list.Add(new(lines[i],cb.X,cb.Y+(i-first)*BoardContentLayout.ContentLineHeight,BoardContentLayout.ValueColor));
        return list;
    }
    public void Paint(UiCanvas canvas){
        canvas.DrawSurface(background,Origin.X,Origin.Y);
        for(int i=0;i<decorations.Length;i++)canvas.DrawSurface(decorations[i],Origin.X+BoardContentLayout.WriteDecorations[i].At.X,Origin.Y+BoardContentLayout.WriteDecorations[i].At.Y);
        for(int i=0;i<buttons.Length;i++)canvas.DrawSurface(buttons[i],Origin.X+BoardContentLayout.WriteButtons[i].X,Origin.Y+BoardContentLayout.WriteButtons[i].Y);
        foreach(var t in Texts())if(t.Text.Length>0)canvas.DrawText(t.Text,t.X,t.Y,ChatPalette.Color(t.Color));
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Button!=MouseButtons.Left&&e.Kind is UiMouseKind.Down or UiMouseKind.Up)return inside;
        switch(e.Kind){
            case UiMouseKind.Down:
                if(!inside)return false;
                pressed=HitButton(e.Location);
                if(pressed<0){if(TitleBox.Contains(e.Location))Focus=0;else if(ContentBox.Contains(e.Location))Focus=1;}
                return true;
            case UiMouseKind.Up:{
                int was=pressed;pressed=-1;
                if(was>=0&&was==HitButton(e.Location))Activate(BoardContentLayout.WriteButtons[was].Action);
                return inside||was>=0;
            }
            default:return inside||pressed>=0;
        }
    }
    void Activate(BoardWriteAction action){
        switch(action){
            case BoardWriteAction.Submit:Submit();break;
            case BoardWriteAction.Close:case BoardWriteAction.Back:Close();break;
        }
    }
    /// <summary>sub_7CDC70：標題空白 → 544，窗不關；否則交給既有送出流程並關窗。</summary>
    public void Submit(){
        if(!Visible)return;
        if(Title.Trim().Length==0){set.Host.Notice(BoardText.Text(BoardText.StringNeedTitle));return;}
        set.Host.Submit(Title,Content.Replace("\n","\r\n"));
        Close();
    }

    StringBuilder Focused=>Focus==0?title:content;
    int Limit=>Focus==0?BoardContentLayout.TitleMaxBytes:BoardContentLayout.ContentMaxBytes;
    bool Append(string s){
        var sb=Focused;
        if(BoardText.Bytes(sb.ToString())+BoardText.Bytes(s)>Limit)return false;
        sb.Append(s);return true;
    }
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(e.KeyCode is not (Keys.Back or Keys.Enter or Keys.Tab or Keys.Escape))return false;   // 可列印鍵走 WM_CHAR → Char()
        if(!e.Down)return true;
        switch(e.KeyCode){
            case Keys.Back:{var sb=Focused;if(sb.Length>0){int n=sb.Length>1&&char.IsLowSurrogate(sb[^1])?2:1;sb.Length-=n;}break;}
            case Keys.Tab:Focus=1-Focus;break;
            case Keys.Enter:if(Focus==0)Focus=1;else Append("\n");break;
            case Keys.Escape:Close();break;   // 引擎決定：Esc 關發文窗（原版待驗證）
        }
        return true;
    }
    public bool Char(char c){
        if(!Visible)return false;
        if(char.IsControl(c))return true;   // Back／Enter／Tab 已在 Key 處理
        Append(c.ToString());return true;
    }
}
