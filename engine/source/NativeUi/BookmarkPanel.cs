namespace P06.NativeUi;

// R4-J（T301／#713）：原生書籤窗 CBookmarkWindowRenewal（BookmarkUI.xml 的 Bookmark_Window）。
// 依據：docs/reference/ui-layout.md 第 6 節（UI§6）、followups-3.md 第 3 節（FU3§3）、followups-9.md 第 1 節（FU9§1）、
//   gap-matrix.md 第 13.5 節 R4-J、第 15.11 節。
// 版面全部在執行期從 Tile.pak 讀 BookmarkUI.xml（不複製遊戲資源）；元件位置＝XML 座標，巢狀元件相對父元件（同 SummonPanel）。
// 已驗證（XML，UI§6）：框 FrameSurf 4313（263×420，可拖曳 Moveable=20）、底 4312 (6,9)、關閉鈕 Exit_Button (249,15)、
//   記憶座標清單 MemoryPlace_List (22,162) 200×204（12 列 CheckButton，每列 Height 17，選取圖 PushSurf 4336）、
//   清單捲軸（WheelControl (23,163) 內的 ScrollBar (218,-5)：上鈕 (0,0)、下鈕 (0,196)、底 (0,14)、拉桿 (0,16)）、
//   快速移動清單 QuickMove_List (23,62) 120×75（5 列，每列 Height 15，選取圖 4345；標題 QuickMoveListTitle_Image 4314 (24,46)）、
//   隨機傳送 RandomTeleport_Button (129,42)（5128）、確認 Confirm_Button (67,395)／取消 Cancel_Button (134,395)、筆數 BookmarkCount_Label (200,397) 20×15。
// 已驗證（BIN，FU3§3.1、FU9§1）：
//   - 同一個窗是一般書籤窗（this+460＝1），也是傳送控制戒指的「選目的地」模式（460＝0，sub_E08A20）。選好後送傳送；3.81 送 C6 HD
//     （書籤地圖、書籤 id），不送 TW16 的 hhh（FU3§3.2）——送包由宿主（RingWindows.cs → LoginConnection.SendTeleportDestination）負責。
//   - ESC（鍵碼 27）由 sub_BDAA10 關窗。
//   - op 2 S_ADD_BOOKMARK 的收包 sub_931D60 把新書籤寫進這個窗後開窗（3.81 對應 S92；見下方 OpenOnSingleBookmark）。
// R9-A（T361／#845）依 FU9§1 改成原版操作（已驗證，除非另外標「推測」）：
//   - 刪除只有 Delete 鍵（sub_BDAA10 的 4142 → sub_BDA930 → 846 → 「是」回呼 sub_503EE0 → sub_BDC1F0）；原版沒有右鍵刪除（已拿掉）。
//     記憶清單可多選（最多 12 筆；Shift＝範圍、Ctrl＝切換，修飾鍵的對應是推測），「是」之後由大到小一筆一個 C3（3.81 C_DeleteBookmark 讀名稱），
//     由編輯宿主 IBookmarkEditHost（BookmarkUi.cs → LoginConnection.SendBookmark）依序送出。846 的畫法是引擎自訂（畫在調色盤那一列）。
//   - Insert 鍵（4141 → sub_BDDE20）＝把記憶清單選取那筆加進快速移動清單（最多 5 筆，第 6 筆顯示 2828）。3.81 沒有保存
//     （op 55 子碼 34 不支援），所以只在本次登入有效：換角色或重新登入就清空。快速移動清單上按 Delete＝只移出清單，不送封包
//     （推測：不經 846）。窗內不能新增書籤（新增入口是聊天指令 bookmark／qbookmark，R9-C）。
//   - 排序 Sorting（sub_BE0E00，this+624）：0＝伺服器順序、1＝名稱 strcmp，比較前先去掉色碼／圖示碼 \f? \a? \d?（sub_BE0C50 → sub_BDF3A0）；
//     關窗時歸 0。引擎以 CP950 位元組比較（同 strcmp），同名再依書籤 id。
//   - 區域下拉第 0 項＝字串 878「全部」（sub_F36F20）。其餘項目原版是全域區域表 dword_16568A8（zone3.xml）的 bookmark 群組（FU17§1）。
//     T440（#1011，FU17 Q9-A）：3.81 沒有 zone3.xml（不是 3.81 行為），照 TW16 改讀參考客戶端的 zone3(-c).xml＋zone3desc-c.tbl
//     （ZoneBookmarkGroups.cs）：下拉列 bookmark id 由小到大的群組名稱，篩選＝群組任一區域的地圖相同且（沒有矩形或座標在矩形內）。
//     讀不到參考客戶端（或還在背景讀取）時維持書籤裡出現過的地圖編號（FU9§1.1 第 4 項）。群組表讀完那一刻篩選歸回「全部」。
//   - 確認鈕：原版存檔（op 55）但不關窗；3.81 沒有東西可存，所以一般模式按確認什麼都不做、窗不關。2847／3106 只在 dirty 時出現，
//     引擎沒有改名／改順序，永遠不 dirty，不顯示。
//   - 選目的地模式：雙擊記憶清單或快速移動清單的列＝選它傳送（同確認鈕）。一般模式雙擊記憶清單＝改名（K1，3.81 不支援，不做）。
//   - 隨機傳送 RandomTeleport_Button 只在選目的地模式顯示（sub_BDC800），施放時用名稱 487、座標 0、id 0 的假書籤（3.81 TELEPORT.java
//     書籤查不到時 randomLocation(200)）。送 id 0 要宿主接受：IBookmarkWindowHost.AcceptsRandomTeleport＝false 的宿主不畫這顆鈕（規則 5）。
// R10-J（T394／#909，gap-matrix 15.12.6）書籤窗收尾：
//   - 隨機傳送：宿主 RingWindows.cs 宣告 AcceptsRandomTeleport；AbilityStatus.TeleportDestinationTarget 對 id 0 送 HD(地圖 0, 書籤 0)。
//     已驗證（3.81 原始碼）：C_UseSkill.java 對技能 5／69 `readH()` 讀了地圖就丟掉，只留 `readD()` 的書籤 id；
//     TELEPORT.java／MASS_TELEPORT.java 以 CharBookTable.getBookMark(pc, id) 查不到時走 randomLocation(200)（地圖可傳送時；否則 276）。
//   - qbookmark（sub_502720 設 this+486＝1，FU9§1.1 第 5 項）：下一次收到單筆書籤（S92）時，把新出現的書籤加進快速移動清單。
//     旗標由聊天指令經 ClientForm.OfferQuickBookmark（PlayerChat.cs）設定。已滿時顯示 2828（推測：同 Insert）；
//     換連線或換角色時旗標清掉（推測）；伺服器拒絕（214）不回 S92 時旗標保留到下一筆新書籤（推測：原版沒有逾時）。
//   - 從記憶清單拖到快速移動清單（FU9§1.1 第 5 項列的第二種加入方式）：在記憶清單的列按下左鍵、在快速移動清單範圍放開＝加入
//     （規則同 Insert：不重複、最多 5 筆、已送刪除的不加）。放開的位置不影響順序，一律加在最後（推測）；兩種模式都可以拖（推測）。
// 推測（標明）：
//   - 框先畫、底 4312 再畫；列文字白色、左邊留 3 px、垂直置中；選取列畫 PushSurf 在文字之下；送刪除後待確認的列文字灰色。
//   - 取消鈕／關閉鈕／ESC 在選目的地模式都是「取消，不送」。選目的地模式在請求結束（送出成功或被清掉）時關窗。
//   - 拉桿位置＝ScrollStart + (ScrollHeight − 拉桿高)·捲動/最大捲動。2828 提示畫在調色盤那一列（原版畫法沒追）。
// 不做（待查證）：13 色調色盤 ColorPalette、AddIcon（改名類，3.81 不支援）、CloningMemoryPlace_List（拖曳時跟著游標的那一列不畫）、
//   這些元件不畫（規則 5：不畫不能用的鈕）。

/// <summary>原生書籤窗的宿主（BookmarkUi.cs／RingWindows.cs 的 ClientForm 轉接）。面板只讀狀態、轉交動作，不碰 LoginConnection。</summary>
public interface IBookmarkWindowHost {
    /// <summary>目前的書籤清單（與 WinForms 分頁同一份 BookmarkView）。</summary>
    P06.BookmarkView Bookmarks{get;}
    /// <summary>傳送控制戒指的選目的地請求（S36 type 1＋技能 5／69）；null＝沒有。</summary>
    P06.TeleportDestinationRequest? Destination{get;}
    /// <summary>傳送送出中（確認鈕停用，擋重複送出）。</summary>
    bool Sending{get;}
    /// <summary>確認鈕：對 shown 這一次的請求以書籤 bookmarkId 施放一次；過期或不合法由宿主拒絕。bookmarkId 0＝隨機傳送。</summary>
    void Teleport(P06.TeleportDestinationRequest shown,uint bookmarkId);
    /// <summary>取消／關閉／ESC：結束 shown 這一次的請求，不送封包。</summary>
    void CancelTeleport(P06.TeleportDestinationRequest shown);
    /// <summary>R9-A：宿主的 Teleport 接受書籤 id 0（隨機傳送）。預設 false＝不畫隨機傳送鈕。</summary>
    bool AcceptsRandomTeleport=>false;
}

/// <summary>R5-B（#734）：新增／刪除書籤的宿主（BookmarkUi.cs 接 LoginConnection.SendBookmark）。和 IBookmarkWindowHost 分開，
/// 讓既有宿主（RingWindows.cs）不用改。沒接上時原生窗不提供刪除（WinForms 分頁仍可用）。</summary>
public interface IBookmarkEditHost {
    /// <summary>新增／刪除送出中（擋重複送出）。</summary>
    bool Sending{get;}
    /// <summary>以 shown 這份清單新增名稱 name 的書籤（C165）；過期或不合法由宿主拒絕。R9-A 起原生窗不呼叫（新增入口在聊天指令）。</summary>
    void Add(P06.BookmarkView shown,string name);
    /// <summary>以 shown 這份清單刪除書籤 bookmarkId（C3，送書籤名稱）；846 已確認。</summary>
    void Delete(P06.BookmarkView shown,uint bookmarkId);
    /// <summary>R9-A：多選刪除，ids 已照原版「由大到小」排好，一筆一個 C3。預設逐筆呼叫 Delete；真正的宿主要依序等前一筆送完。</summary>
    void DeleteMany(P06.BookmarkView shown,IReadOnlyList<uint> ids){foreach(var id in ids)Delete(shown,id);}
}

public sealed class BookmarkWindowPanel:IUiMovablePanel,IUiClosablePanel,IUiTextInput {
    [UiPanel(178)] internal static UiPanelRegistration Registration=>new(BookmarkWindowPanel.PanelId,BookmarkWindowPanel.LayoutName,r=>new BookmarkWindowPanel{ZoneGroups=ZoneBookmarkGroupSource.For(r.Resources) is {} z?()=>z.Current:null});   // R4-J (#713): CBookmarkWindowRenewal (BookmarkUI.xml); T440 (#1011) zone groups from the reference client
    public const string PanelId="Bookmark_Window",LayoutName="BookmarkUI";
    /// <summary>Bookmarks.ReceiveOne（S92＝3.81 的單筆書籤，對應 TW16 op 2）設定的提示文字；用來分辨單筆收包與整份清單（不改 Bookmarks.cs）。</summary>
    public const string SingleBookmarkNotice="已收到伺服器書籤資料。";
    /// <summary>字串編號（string-c.tbl）：878「全部」、2828 快速移動清單已滿、487「* 隨機座標 *」。</summary>
    public const int StringAll=878,StringQuickFull=2828,StringRandom=487;
    /// <summary>快速移動清單最多 5 筆（this+340，FU9§1.1 第 5 項）；記憶清單多選最多 12 筆。</summary>
    public const int QuickCapacity=5,MaxMarked=12;
    /// <summary>隨機傳送的書籤 id（原版假書籤 id 0）。</summary>
    public const uint RandomTeleportId=0;

    internal sealed record Part(string Key,Rectangle Rect,int Base,int? Push,int? Disable);

    Func<int,UiSurface?> surf=_=>null;
    bool usable;Point origin;Size size;int moveable;
    int frameSurf,baseSurf;Point baseAt;
    (Point At,int Surf)[] titles=[];
    Part exit=null!,confirm=null!,cancel=null!;
    Rectangle listRect;int rowHeight,rowCount,rowSurf;
    Part? up,down;(Point At,int Surf)? scrollBack;(Point At,int Surf)? thumb;Point barAt;int scrollStart,scrollHeight;
    Rectangle countLabel;
    // R5-B：排序鈕、區域篩選下拉（缺節點或缺圖＝不畫該件，不停用整窗）。
    Part? sorting;
    Part? zoneExpand;Rectangle zoneLabel;Point zoneListAt;int zoneListWidth,zoneRowHeight,zonePageSize;int? zoneRowSurf;(Point At,int Surf)? zoneBack;
    // R9-A：快速移動清單、隨機傳送鈕（同樣可有可無）。
    Rectangle quickRect;int quickRowHeight,quickRowCount;int? quickRowSurf;
    Part? random;

    bool open;P06.TeleportDestinationRequest? destination;long dismissedDestination=-1;
    uint? selected;int scroll;string? pressed;
    readonly List<uint> marked=[];
    (int Session,uint Actor,int Revision)? seen;
    P06.BookmarkView? view;
    P06.Bookmark[] rows=[];
    bool sortByName;ushort? zoneFilter;bool zoneOpen;int zoneScroll;
    // T440：區域群組表（null＝用地圖編號）。第一次拿到後固定不換。
    ZoneBookmarkGroups? zoneGroups;
    uint[]? confirmDelete;string? message;
    readonly List<uint> quick=[];(int Session,uint Actor)? quickOwner;uint? quickSelected;bool quickFocus;
    // R10-J：qbookmark 的 this+486 旗標（哪個連線、哪個角色要的）；拖曳中的記憶清單書籤 id。
    (int Session,uint Actor)? quickPending;uint? dragRow;

    public IBookmarkWindowHost? Host{get;private set;}
    public void Attach(IBookmarkWindowHost host)=>Host=host;
    /// <summary>R5-B：刪除宿主（null＝原生窗不提供刪除）。</summary>
    public IBookmarkEditHost? Editor{get;private set;}
    public void AttachEditor(IBookmarkEditHost editor)=>Editor=editor;
    /// <summary>字串 846（FU3§3.1 記載的原文）。</summary>
    public const string DeletePrompt="要刪除所選擇的項目？";

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds=>Visible?new(origin,size):Rectangle.Empty;
    public bool Visible=>usable&&open;
    internal bool Usable=>usable;
    /// <summary>選目的地模式（傳送控制戒指）中。</summary>
    internal bool DestinationMode=>Visible&&destination!=null;
    internal P06.TeleportDestinationRequest? ShownDestination=>destination;
    internal uint? Selected=>selected;
    /// <summary>記憶清單多選中的書籤 id（照點選順序）；單選時就是 Selected 一筆。</summary>
    internal IReadOnlyList<uint> Marked=>marked;
    internal int Scroll=>scroll;
    internal int RowCount=>rowCount;
    /// <summary>畫面上的清單：先依區域篩選，再依排序鈕排序。</summary>
    internal IReadOnlyList<P06.Bookmark> Rows=>rows;
    internal int MaxScroll=>Math.Max(0,Rows.Count-rowCount);
    internal string CountText=>view==null?"":$"{view.Items.Length}/{view.Capacity}";
    internal bool ConfirmEnabled=>confirmDelete!=null?Editor is {Sending:false}:destination==null||(Host is {Sending:false}&&TargetRow is {DeletePending:false});
    internal bool SortByName=>sortByName;
    internal ushort? ZoneFilter=>zoneFilter;
    internal bool ZoneListOpen=>Visible&&zoneOpen;
    /// <summary>
    /// 下拉的項目：null＝878「全部」。有區域群組表時其餘是 bookmark id（由小到大，sub_F36F20）；
    /// 沒有時是書籤出現過的地圖編號（由小到大）。
    /// </summary>
    internal IReadOnlyList<ushort?> ZoneItems=>zoneGroups is {} g?[null,..g.Groups.Select(x=>(ushort?)x.Id)]:[null,..(view?.Items??[]).Select(b=>b.MapId).Distinct().Order().Select(m=>(ushort?)m)];
    internal string ZoneText=>ZoneLabel(zoneFilter);
    /// <summary>T440：區域群組表的來源（宿主設定；null 或回 null＝用地圖編號）。Update 時取一次，拿到就固定。</summary>
    public Func<ZoneBookmarkGroups?>? ZoneGroups{get;set;}
    /// <summary>目前用的區域群組表；null＝地圖編號模式。</summary>
    internal ZoneBookmarkGroups? ZoneGroupTable=>zoneGroups;
    /// <summary>下拉項目的文字：群組模式＝群組名稱，否則地圖編號。</summary>
    internal string ZoneLabel(ushort? item)=>item is ushort id&&zoneGroups?.Find(id) is {} g?g.Name:ZoneItemText(item);
    bool ZoneMatches(P06.Bookmark b,ushort z)=>zoneGroups is {} g?g.Find(z)?.Matches(b)==true:b.MapId==z;
    /// <summary>846 確認中的第一筆書籤 id（多選時見 ConfirmingDeleteIds）。</summary>
    internal uint? ConfirmingDelete=>confirmDelete is {Length:>0} d?d[0]:null;
    /// <summary>846 確認中的全部書籤 id（已排成送出順序：畫面順序由大到小）。</summary>
    internal IReadOnlyList<uint>? ConfirmingDeleteIds=>confirmDelete;
    /// <summary>窗內提示（2828 等）；null＝沒有。</summary>
    internal string? Message=>message;
    /// <summary>快速移動清單（本次登入有效）：書籤 id，照加入順序。</summary>
    internal IReadOnlyList<uint> QuickIds=>quick;
    /// <summary>快速移動清單畫面上的列（已不存在的書籤不列）。</summary>
    internal IReadOnlyList<P06.Bookmark> QuickRows=>quick.Select(id=>view?.Items.FirstOrDefault(b=>b.Id==id)).OfType<P06.Bookmark>().ToArray();
    internal uint? QuickSelected=>quickSelected;
    internal bool QuickFocus=>quickFocus;
    internal bool HasQuickList=>quickRowCount>0&&quickRowHeight>0&&quickRect.Width>0;
    /// <summary>R10-J：qbookmark 旗標（this+486）設著，等下一筆新書籤。</summary>
    internal bool QuickPending=>quickPending!=null;
    /// <summary>R10-J：記憶清單上按下、還沒放開的書籤 id（拖到快速移動清單用）。</summary>
    internal uint? DragRow=>dragRow;
    internal bool RandomVisible=>DestinationMode&&Host is {AcceptsRandomTeleport:true}&&random is {Rect:{Width:>0,Height:>0}};
    bool CanEdit=>destination==null&&Editor is {Sending:false}&&view is {CanEdit:true};
    // Rectangle.IsEmpty 要 X／Y 也是 0 才成立；缺圖的鈕有位置、沒有大小，所以看寬高。
    bool HasSorting=>sorting is {Rect:{Width:>0,Height:>0}};
    bool HasZone=>zoneExpand is {Rect:{Width:>0,Height:>0}};
    P06.Bookmark? SelectedRow=>selected is uint id?Rows.FirstOrDefault(r=>r.Id==id):null;
    P06.Bookmark? QuickSelectedRow=>quickSelected is uint id?QuickRows.FirstOrDefault(r=>r.Id==id):null;
    /// <summary>確認鈕要用的書籤：焦點在快速移動清單時用它的選取，否則用記憶清單的選取。</summary>
    P06.Bookmark? TargetRow=>quickFocus?QuickSelectedRow:SelectedRow;

    // ---- 字串 ----
    static readonly Dictionary<int,string> Fallback=new(){[StringAll]="全部",[StringQuickFull]="快速移動目錄最多只能登錄5個為止。",[StringRandom]="* 隨機座標 *"};
    /// <summary>字串表第 id 筆（去掉色碼）；表不存在或空白時用本機 Text.pak string-c.tbl 查到的原文（FU9 與本任務報告）。</summary>
    internal static string Text(int id){
        if(P06.ServerMessageText.Table is {} t&&id>=0&&id<t.Count){string s=P06.ServerMessageText.DisplayText(t[id]).Trim();if(s.Length>0)return s;}
        return Fallback.GetValueOrDefault(id,"");
    }
    internal static string ZoneItemText(ushort? item)=>item is ushort m?m.ToString(System.Globalization.CultureInfo.InvariantCulture):Text(StringAll);

    // ---- 排序（sub_BE0C50 → sub_BDF3A0：先去色碼，再 strcmp） ----
    static readonly System.Text.Encoding Cp950=CreateCp950();
    static System.Text.Encoding CreateCp950(){System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);return System.Text.Encoding.GetEncoding(950);}
    static readonly System.Text.RegularExpressions.Regex ColorCode=new(@"\\[fad].?",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    /// <summary>排序用的名稱：去掉 \f?（色碼）、\a?（色碼）、\d?（圖示碼）。</summary>
    internal static string SortName(string name)=>ColorCode.Replace(name,"");
    /// <summary>strcmp 的順序：CP950 位元組逐一比（無號）。</summary>
    internal static int CompareNames(string a,string b){
        var x=Cp950.GetBytes(SortName(a));var y=Cp950.GetBytes(SortName(b));
        return ((ReadOnlySpan<byte>)x).SequenceCompareTo(y);
    }

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>測試與正式共用：surf id → 圖（可為 null）。必要的圖缺任一張 → 停用並記一行日誌（規則 5，不畫替代圖）。</summary>
    internal void Load(UiLayout layout,Func<int,UiSurface?> surfs){
        surf=surfs;
        var window=Direct(layout.Root,"Window",PanelId);
        origin=new(window.Int("X"),window.Int("Y"));moveable=window.Int("Moveable");
        frameSurf=SummonPanel.SurfId(window,"FrameSurf");
        var missing=new List<string>();
        Size? Measure(int id)=>surf(id) is {Width:>0,Height:>0} s?new Size(s.Width,s.Height):null;
        Point Pos(UiNode n,Point parent)=>new(parent.X+n.Int("X"),parent.Y+n.Int("Y"));
        int? Optional(UiNode n,string attr)=>n.Attr(attr)!=null?SummonPanel.SurfId(n,attr):null;
        Part Button(UiNode n,Point parent,string key,bool required){
            int b=SummonPanel.SurfId(n,"BaseSurf");var s=Measure(b);
            if(s==null&&required)missing.Add($"{n.Path} BaseSurf {b}");
            return new(key,new(Pos(n,parent),s??Size.Empty),b,Optional(n,"PushSurf"),Optional(n,"DisableSurf"));
        }
        var zero=Point.Empty;
        var frame=Measure(frameSurf);
        if(frame==null)missing.Add($"{window.Path} FrameSurf {frameSurf}");
        size=frame??Size.Empty;
        var baseImage=Direct(window,"Image","Base_Image");baseSurf=SummonPanel.SurfId(baseImage,"Surf");baseAt=Pos(baseImage,zero);
        if(Measure(baseSurf)==null)missing.Add($"{baseImage.Path} Surf {baseSurf}");
        exit=Button(Direct(window,"Button","Exit_Button"),zero,"exit",true);
        confirm=Button(Direct(window,"Button","Confirm_Button"),zero,"confirm",true);
        cancel=Button(Direct(window,"Button","Cancel_Button"),zero,"cancel",true);

        var list=Direct(window,"MovableList","MemoryPlace_List");
        listRect=new(Pos(list,zero),new(list.Int("Width"),list.Int("Height")));
        var rows=list.ChildrenNamed("CheckButton").ToList();
        if(rows.Count==0)throw new UiLayoutException(layout.FileName,list.Path,"記憶座標清單沒有任何列");
        rowCount=rows.Count;rowHeight=rows[0].Int("Height");
        if(rowHeight<=0||rows.Any(r=>r.Int("Height")!=rowHeight))throw new UiLayoutException(layout.FileName,list.Path,"記憶座標清單的列高不一致或不是正數");
        rowSurf=SummonPanel.SurfId(rows[0],"PushSurf");
        if(Measure(rowSurf)==null)missing.Add($"{rows[0].Path} PushSurf {rowSurf}");

        // 捲軸：WheelControl MemoryPlaceList_WheelArea → ScrollBar MemoryPlaceList_Scrollbar（缺圖只是不畫該件，不停用整窗）。
        up=down=null;scrollBack=thumb=null;
        var wheel=window.ChildrenNamed("WheelControl").Where(n=>n.Attr("Name")=="MemoryPlaceList_WheelArea").ToList();
        if(wheel.Count==1){
            var bar=Direct(wheel[0],"ScrollBar","MemoryPlaceList_Scrollbar");var at=Pos(bar,Pos(wheel[0],zero));
            up=Button(Direct(bar,"Button","Up_Button"),at,"up",false);down=Button(Direct(bar,"Button","Down_Button"),at,"down",false);
            barAt=at;var move=Direct(bar,"Button","Move_Button");thumb=(Pos(move,at),SummonPanel.SurfId(move,"BaseSurf"));
            var back=bar.ChildrenNamed("Image").FirstOrDefault(n=>n.Attr("Name")=="Background_Image");
            if(back!=null)scrollBack=(new(at.X+(back.TryInt("X")??0),at.Y+(back.TryInt("Y")??0)),SummonPanel.SurfId(back,"Surf"));
            scrollStart=bar.TryInt("ScrollStart")??0;scrollHeight=bar.TryInt("ScrollHeight")??0;
        }
        var count=window.ChildrenNamed("Label").FirstOrDefault(n=>n.Attr("Name")=="BookmarkCount_Label");
        countLabel=count==null?Rectangle.Empty:new(Pos(count,zero),new(count.Int("Width"),count.Int("Height")));

        // R5-B：排序鈕與區域篩選（可有可無：缺節點或缺圖只是不畫該件）。
        // 這兩件的子節點常省略 X／Y（例如 Expand_List 只有 Y="19"），省略＝0；版面有問題只略過這兩件、記一行日誌，不停用整窗。
        UiNode? Find(UiNode parent,string element,string name)=>parent.ChildrenNamed(element).FirstOrDefault(n=>n.Attr("Name")==name);
        Point Loose(UiNode n,Point parent)=>new(parent.X+(n.TryInt("X")??0),parent.Y+(n.TryInt("Y")??0));
        Part? LooseButton(UiNode n,Point parent,string key){
            int b=SummonPanel.SurfId(n,"BaseSurf");
            return new(key,new(Loose(n,parent),Measure(b)??Size.Empty),b,Optional(n,"PushSurf"),Optional(n,"DisableSurf"));
        }
        sorting=null;zoneExpand=null;zoneLabel=Rectangle.Empty;zoneRowSurf=null;zoneBack=null;zoneRowHeight=zonePageSize=zoneListWidth=0;
        try{
            sorting=Find(window,"Button","Sorting_Button") is {} sb?LooseButton(sb,zero,"sort"):null;
        }catch(UiLayoutException ex){sorting=null;UiResourceLog.Write($"panel {PanelId} 排序鈕略過：{ex.Message}");}
        try{
        if(Find(window,"ComboBox","ZoneFilter_Combobox") is {} combo&&Find(combo,"Button","Expand_Button") is {} eb&&Find(combo,"UpDownList","Expand_List") is {} el){
            var at=Loose(combo,zero);
            zoneExpand=LooseButton(eb,at,"zone");
            if(Find(combo,"Label","Text_Label") is {} tl)zoneLabel=new(Loose(tl,at),new(tl.TryInt("Width")??0,tl.TryInt("Height")??0));
            zoneListAt=Loose(el,at);zoneListWidth=el.TryInt("Width")??0;zonePageSize=Math.Max(1,el.TryInt("PageSize")??1);
            var items=el.ChildrenNamed("CheckButton").ToList();
            if(items.Count>0){
                zoneRowHeight=items[0].TryInt("Height")??0;
                zoneRowSurf=Optional(items[0],"PushSurf");
            }
            if(el.ChildrenNamed("Image").FirstOrDefault(n=>n.Attr("Name")=="Background_Image") is {} zb)
                zoneBack=(new(zoneListAt.X+(zb.TryInt("X")??0),zoneListAt.Y+(zb.TryInt("Y")??0)),SummonPanel.SurfId(zb,"Surf"));
            if(zoneRowHeight<=0||zoneListWidth<=0)zoneExpand=null;
        }
        }catch(UiLayoutException ex){zoneExpand=null;UiResourceLog.Write($"panel {PanelId} 區域篩選略過：{ex.Message}");}

        // R9-A：快速移動清單 QuickMove_List、隨機傳送鈕 RandomTeleport_Button（缺節點或版面有問題＝不畫該件）。
        quickRect=Rectangle.Empty;quickRowHeight=quickRowCount=0;quickRowSurf=null;random=null;
        try{
            if(Find(window,"MovableList","QuickMove_List") is {} ql){
                var qrows=ql.ChildrenNamed("CheckButton").ToList();
                int h=qrows.Count>0?qrows[0].TryInt("Height")??0:0;
                if(h>0&&qrows.All(r=>(r.TryInt("Height")??0)==h)){
                    quickRect=new(Loose(ql,zero),new(ql.TryInt("Width")??0,ql.TryInt("Height")??0));
                    quickRowHeight=h;quickRowCount=Math.Min(qrows.Count,QuickCapacity);quickRowSurf=Optional(qrows[0],"PushSurf");
                }
            }
        }catch(UiLayoutException ex){quickRowCount=0;UiResourceLog.Write($"panel {PanelId} 快速移動清單略過：{ex.Message}");}
        try{
            random=Find(window,"Button","RandomTeleport_Button") is {} rb?LooseButton(rb,zero,"random"):null;
        }catch(UiLayoutException ex){random=null;UiResourceLog.Write($"panel {PanelId} 隨機傳送鈕略過：{ex.Message}");}
        // 標題：記憶座標清單一定畫；快速移動清單的標題只在清單可用時畫。
        titles=window.ChildrenNamed("Image").Where(n=>n.Attr("Name") is "MemoryZoneListTitle_Image"||(HasQuickList&&n.Attr("Name")=="QuickMoveListTitle_Image"))
            .Select(n=>(Pos(n,zero),SummonPanel.SurfId(n,"Surf"))).ToArray();

        usable=missing.Count==0;
        if(!usable)UiResourceLog.Write($"panel {PanelId} 停用：缺少版面所需的 surf（{string.Join("；",missing)}）");
        open=false;destination=null;selected=null;marked.Clear();scroll=0;pressed=null;
        sortByName=false;zoneFilter=null;zoneOpen=false;zoneScroll=0;ResetEdit();this.rows=view?.Items??[];
        quickSelected=null;quickFocus=false;dragRow=null;
    }
    void ResetEdit(){confirmDelete=null;message=null;}

    static UiNode Direct(UiNode parent,string element,string name){
        var found=parent.ChildrenNamed(element).Where(c=>c.Attr("Name")==name).Take(2).ToList();
        if(found.Count!=1)throw new UiLayoutException(parent.Layout.FileName,parent.Path,$"{element} Name=\"{name}\" {(found.Count==0?"缺少":"不唯一")}");
        return found[0];
    }

    // ---- 狀態 ----
    public void Update(IUiGameState state){
        if(!state.InWorld||Host is not {} host){if(open)CloseLocal();view=null;seen=null;rows=[];return;}
        var v=host.Bookmarks;var q=host.Destination;
        // 原版 sub_931D60：單筆書籤收包後開窗。整份清單、第一次看到這個角色、或換角色時不開。
        bool sameActor=seen is {} s&&s.Session==v.Session&&s.Actor==v.ActorId;
        bool single=sameActor&&seen!.Value.Revision!=v.Revision&&v.Notice==SingleBookmarkNotice&&v.ActorId!=0;
        if(single)OpenNormal();
        if(!sameActor){selected=null;marked.Clear();scroll=0;zoneFilter=null;zoneOpen=false;zoneScroll=0;dragRow=null;ResetEdit();}
        // 快速移動清單只在本次登入有效（3.81 沒有保存）：換連線或換角色就清空。離開世界又回到同一個連線、同一個角色不清。
        if(quickOwner is not {} o||o.Session!=v.Session||o.Actor!=v.ActorId){quick.Clear();quickSelected=null;quickFocus=false;quickOwner=(v.Session,v.ActorId);}
        // R10-J：qbookmark 旗標。別的連線或角色要的 → 丟掉；同一角色收到單筆書籤（S92）→ 新出現的書籤加進快速移動清單，旗標清掉。
        if(quickPending is {} qp&&(qp.Session!=v.Session||qp.Actor!=v.ActorId))quickPending=null;
        if(quickPending!=null&&single){
            var before=(view?.Items??[]).Select(b=>b.Id).ToHashSet();
            var added=v.Items.Where(b=>!b.DeletePending&&!before.Contains(b.Id)).Select(b=>b.Id).ToArray();
            if(added.Length>0){quickPending=null;view=v;foreach(var id in added)AddQuickId(id);}
        }
        seen=(v.Session,v.ActorId,v.Revision);view=v;
        // T440：群組表第一次讀到 → 換成群組模式，篩選歸回全部（地圖編號和 bookmark id 不能混用）。
        if(zoneGroups==null&&ZoneGroups?.Invoke() is {} groups){zoneGroups=groups;zoneFilter=null;zoneScroll=0;}
        // 地圖編號模式：該地圖已沒有書籤 → 回到全部。群組模式的項目是固定表，沒有符合的書籤時清單是空的（推測同原版）。
        if(zoneGroups==null&&zoneFilter is ushort z&&!v.Items.Any(b=>b.MapId==z))zoneFilter=null;
        quick.RemoveAll(id=>!v.Items.Any(b=>b.Id==id));
        if(quickSelected is uint qs&&!quick.Contains(qs))quickSelected=null;
        Refresh();
        if(confirmDelete is {} del&&!del.All(id=>v.Items.Any(r=>r.Id==id&&!r.DeletePending)))confirmDelete=null;
        // 選目的地：新請求開窗（同一請求被取消後不重開）；請求結束時，由它開的窗一起關。
        if(q!=null&&q.Revision!=dismissedDestination&&q.Revision!=destination?.Revision){destination=q;open=true;pressed=null;ResetEdit();}
        else if(q==null&&destination!=null){destination=null;open=false;pressed=null;}
    }
    /// <summary>重算畫面清單（篩選、排序），並修正選取與捲動。</summary>
    void Refresh(){
        IEnumerable<P06.Bookmark> list=view?.Items??[];
        if(zoneFilter is ushort z)list=list.Where(b=>ZoneMatches(b,z));
        if(sortByName)list=list.Order(Comparer<P06.Bookmark>.Create((a,b)=>{int c=CompareNames(a.Name,b.Name);return c!=0?c:a.Id.CompareTo(b.Id);}));
        rows=list.ToArray();
        if(selected is uint id&&!rows.Any(r=>r.Id==id))selected=null;
        marked.RemoveAll(m=>!rows.Any(r=>r.Id==m));
        if(selected==null)marked.Clear();
        scroll=Math.Clamp(scroll,0,MaxScroll);
        zoneScroll=Math.Clamp(zoneScroll,0,Math.Max(0,ZoneItems.Count-zonePageSize));
    }
    void OpenNormal(){if(!open){open=true;pressed=null;}}
    /// <summary>XML Toggle="true"：開／關一般模式（熱鍵或選單入口由其他任務接；選目的地模式中＝取消）。</summary>
    public void Toggle(){if(open)CloseLocal();else if(usable)OpenNormal();}
    /// <summary>關閉鈕、取消鈕、ESC、4 層堆疊擠出：選目的地模式中通知宿主取消（不送封包）。</summary>
    public void Close()=>CloseLocal();
    void CloseLocal(){
        if(destination is {} q){dismissedDestination=q.Revision;destination=null;Host?.CancelTeleport(q);}
        open=false;pressed=null;zoneOpen=false;ResetEdit();
        if(sortByName){sortByName=false;Refresh();}   // 原版關窗時 this+624 歸 0
    }

    // ---- 刪除（Delete 鍵 → 846） ----
    /// <summary>Delete 鍵：焦點在記憶清單時，選取的書籤（可多選）進入 846 確認；焦點在快速移動清單時只移出清單。</summary>
    internal bool BeginDelete(){
        if(quickFocus)return RemoveQuick();
        if(!CanEdit||SelectedRow==null)return false;
        IReadOnlyList<uint> ids=marked.Count>0?marked:[selected!.Value];
        var targets=Rows.Select((r,i)=>(Row:r,Index:i)).Where(x=>ids.Contains(x.Row.Id)).ToList();
        if(targets.Count==0||targets.Any(x=>x.Row.DeletePending))return false;
        message=null;confirmDelete=targets.OrderByDescending(x=>x.Index).Select(x=>x.Row.Id).ToArray();   // 由大到小送出
        return true;
    }
    void AnswerDelete(bool yes){
        if(confirmDelete is not {} ids)return;
        if(!yes){confirmDelete=null;return;}
        if(Editor is not {Sending:false} editor||view is not {} v)return;
        confirmDelete=null;
        if(ids.Length==1)editor.Delete(v,ids[0]);else editor.DeleteMany(v,ids);
    }

    // ---- 快速移動清單（Insert 鍵，本次登入有效） ----
    /// <summary>Insert 鍵（4141 → sub_BDDE20）：記憶清單選取那筆加進快速移動清單；已滿顯示 2828。</summary>
    internal bool AddQuick()=>SelectedRow is {} row&&AddQuickId(row.Id);
    /// <summary>把書籤 id 加進快速移動清單（Insert、拖曳、qbookmark 共用）：要有清單、書籤存在且沒送刪除、不重複；已滿顯示 2828。</summary>
    bool AddQuickId(uint id){
        if(!HasQuickList||view?.Items.FirstOrDefault(b=>b.Id==id) is not {DeletePending:false})return false;
        if(quick.Contains(id))return false;
        if(quick.Count>=QuickCapacity){message=Text(StringQuickFull);return false;}
        quick.Add(id);message=null;return true;
    }
    /// <summary>
    /// R10-J：聊天指令 qbookmark（sub_502720 設 this+486＝1）。session／actor＝送出時的連線（BookmarkView.Session）與角色；
    /// 下一次同一角色收到新書籤時加進快速移動清單。版面沒有快速移動清單時不設（回 false）。
    /// </summary>
    public bool RequestQuickOnNextBookmark(int session,uint actor){
        if(!usable||!HasQuickList||actor==0)return false;
        quickPending=(session,actor);return true;
    }
    bool RemoveQuick(){
        if(quickSelected is not uint id||!quick.Remove(id))return false;
        quickSelected=null;message=null;return true;
    }

    /// <summary>846 確認中，Y／N 字元不漏到聊天輸入列（WM_CHAR）。窗內不再命名（新增入口在聊天指令）。</summary>
    public bool Char(char c)=>Visible&&confirmDelete!=null&&c is 'y' or 'Y' or 'n' or 'N';

    // ---- 幾何 ----
    Rectangle Abs(Rectangle r)=>r.IsEmpty?Rectangle.Empty:new(r.X+origin.X,r.Y+origin.Y,r.Width,r.Height);
    internal Rectangle ExitRect=>Abs(exit.Rect);
    internal Rectangle ConfirmRect=>Abs(confirm.Rect);
    internal Rectangle CancelRect=>Abs(cancel.Rect);
    internal Rectangle UpRect=>up==null?Rectangle.Empty:Abs(up.Rect);
    internal Rectangle DownRect=>down==null?Rectangle.Empty:Abs(down.Rect);
    internal Rectangle ListRect=>Abs(listRect);
    /// <summary>畫面第 r 列（0..RowCount−1）的範圍（邏輯座標）。</summary>
    internal Rectangle RowRect(int r)=>Abs(new(listRect.X,listRect.Y+rowHeight*r,listRect.Width,rowHeight));
    /// <summary>快速移動清單第 r 列（0..4）的範圍；清單不可用時是空的。</summary>
    internal Rectangle QuickRowRect(int r)=>HasQuickList?Abs(new(quickRect.X,quickRect.Y+quickRowHeight*r,quickRect.Width,quickRowHeight)):Rectangle.Empty;
    /// <summary>快速移動清單整塊（QuickMove_List 的 Width×Height）；拖曳的放開範圍。清單不可用時是空的。</summary>
    internal Rectangle QuickListRect=>HasQuickList?Abs(quickRect):Rectangle.Empty;
    internal Rectangle RandomRect=>RandomVisible?Abs(random!.Rect):Rectangle.Empty;
    internal Point? ThumbAt{get{
        if(thumb is not (Point at,int id)||surf(id) is not {Height:>0} s)return null;
        int range=Math.Max(0,scrollHeight-s.Height),max=MaxScroll;
        return new(origin.X+at.X,origin.Y+barAt.Y+scrollStart+(max==0?0:range*scroll/max));
    }}

    internal Rectangle SortRect=>HasSorting?Abs(sorting!.Rect):Rectangle.Empty;
    internal Rectangle ZoneExpandRect=>HasZone?Abs(zoneExpand!.Rect):Rectangle.Empty;
    internal Rectangle ZoneLabelRect=>HasZone?Abs(zoneLabel):Rectangle.Empty;
    /// <summary>展開清單第 r 列（0..PageSize−1）的範圍（邏輯座標）。</summary>
    internal Rectangle ZoneRowRect(int r)=>HasZone?Abs(new(zoneListAt.X,zoneListAt.Y+zoneRowHeight*r,zoneListWidth,zoneRowHeight)):Rectangle.Empty;
    internal Rectangle ZoneListRect=>HasZone?Abs(new(zoneListAt.X,zoneListAt.Y,zoneListWidth,zoneRowHeight*Math.Min(zonePageSize,ZoneItems.Count))):Rectangle.Empty;
    internal int ZoneScroll=>zoneScroll;

    string? HitKey(Point p){
        if(zoneOpen&&HasZone){
            var items=ZoneItems;
            for(int r=0;r<zonePageSize;r++)if(ZoneRowRect(r).Contains(p))return zoneScroll+r<items.Count?"zone:"+(zoneScroll+r):"zone-list";
        }
        if(ExitRect.Contains(p))return "exit";
        if(SortRect.Contains(p))return "sort";
        if(RandomRect.Contains(p))return "random";
        if(ZoneExpandRect.Contains(p)||ZoneLabelRect.Contains(p))return "zone";
        if(ConfirmRect.Contains(p))return "confirm";
        if(CancelRect.Contains(p))return "cancel";
        if(UpRect.Contains(p))return "up";
        if(DownRect.Contains(p))return "down";
        for(int r=0;r<quickRowCount;r++)if(QuickRowRect(r).Contains(p))return r<QuickRows.Count?"quick:"+r:null;
        for(int r=0;r<rowCount;r++)if(RowRect(r).Contains(p))return scroll+r<Rows.Count?"row:"+(scroll+r):null;
        return null;
    }

    // ---- 繪製 ----
    public void Paint(UiCanvas canvas){
        if(!Visible)return;
        void Draw(int? id,Point at){if(id is int i)canvas.DrawSurface(surf(i),origin.X+at.X,origin.Y+at.Y);}
        void Button(Part? p,bool enabled=true){
            if(p==null)return;
            int id=!enabled&&p.Disable is int d?d:enabled&&pressed==p.Key&&p.Push is int push?push:p.Base;
            Draw(id,p.Rect.Location);
        }
        void Row(P06.Bookmark row,Rectangle rect,int? selectedSurf,bool isSelected){
            if(isSelected&&selectedSurf is int ss)canvas.DrawSurface(surf(ss),rect.X,rect.Y);
            var m=UiText.Measure(canvas.Graphics,row.Name);
            canvas.DrawText(row.Name,rect.X+3,rect.Y+(rect.Height-m.Height)/2,row.DeletePending?Color.Gray:Color.White);
        }
        Draw(frameSurf,Point.Empty);Draw(baseSurf,baseAt);
        foreach(var (at,id) in titles)Draw(id,at);
        Button(exit);
        if(scrollBack is (Point bat,int bid))Draw(bid,bat);
        Button(up);Button(down);
        if(ThumbAt is Point tp&&thumb is (Point _,int tid))canvas.DrawSurface(surf(tid),tp.X,tp.Y);
        var rows=Rows;
        for(int r=0;r<rowCount&&scroll+r<rows.Count;r++){
            var row=rows[scroll+r];
            Row(row,RowRect(r),rowSurf,marked.Contains(row.Id)||row.Id==selected);
        }
        if(HasQuickList){
            var qrows=QuickRows;
            for(int r=0;r<quickRowCount&&r<qrows.Count;r++)Row(qrows[r],QuickRowRect(r),quickRowSurf,qrows[r].Id==quickSelected);
        }
        Button(confirm,ConfirmEnabled);Button(cancel);
        if(!countLabel.IsEmpty&&CountText.Length>0){
            var m=UiText.Measure(canvas.Graphics,CountText);var c=Abs(countLabel);
            canvas.DrawText(CountText,c.X+(c.Width-m.Width)/2,c.Y+(c.Height-m.Height)/2,Color.White);
        }
        if(HasSorting)Button(sorting);
        if(RandomVisible)Button(random,Host is {Sending:false});
        if(HasZone){
            Button(zoneExpand);
            var lr=ZoneLabelRect;
            if(!lr.IsEmpty){var m=UiText.Measure(canvas.Graphics,ZoneText);canvas.DrawText(ZoneText,lr.X+(lr.Width-m.Width)/2,lr.Y+(lr.Height-m.Height)/2,Color.White);}
        }
        // 846 確認／2828 提示：畫在調色盤那一列（調色盤不畫），引擎自訂畫法。
        if(confirmDelete!=null)canvas.DrawText(DeletePrompt,origin.X+PromptAt.X,origin.Y+PromptAt.Y,Color.White,Color.Black);
        else if(message!=null)canvas.DrawText(message,origin.X+PromptAt.X,origin.Y+PromptAt.Y,Color.FromArgb(255,96,96),Color.Black);
        // 展開清單最後畫（蓋在記憶座標清單上）。
        if(ZoneListOpen&&HasZone){
            if(zoneBack is (Point zat,int zid))Draw(zid,zat);
            var items=ZoneItems;
            for(int r=0;r<zonePageSize&&zoneScroll+r<items.Count;r++){
                var rect=ZoneRowRect(r);var item=items[zoneScroll+r];
                if(item==zoneFilter&&zoneRowSurf is int zs)canvas.DrawSurface(surf(zs),rect.X,rect.Y);
                string text=ZoneLabel(item);
                var size=UiText.Measure(canvas.Graphics,text);
                canvas.DrawText(text,rect.X+3,rect.Y+(rect.Height-size.Height)/2,Color.White);
            }
        }
    }
    /// <summary>提示列位置（視窗內座標）：ColorPalette (19,368) 那一列再往內 3／4 px（推測，見檔頭）。</summary>
    internal static readonly Point PromptAt=new(22,372);

    // ---- 輸入 ----
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Wheel){
            if(!inside)return false;
            if(e.Delta!=0){
                if(ZoneListOpen&&ZoneListRect.Contains(e.Location))zoneScroll=Math.Clamp(zoneScroll-Math.Sign(e.Delta),0,Math.Max(0,ZoneItems.Count-zonePageSize));
                else ScrollBy(-Math.Sign(e.Delta));
            }
            return true;
        }
        // 原版 sub_BDE250 只處理左鍵（512–515），沒有右鍵刪除。
        if(e.Button!=MouseButtons.Left)return inside;
        if(e.Kind==UiMouseKind.Down){
            pressed=HitKey(e.Location);dragRow=null;
            if(zoneOpen&&pressed is not ("zone" or "zone-list")&&pressed?.StartsWith("zone:",StringComparison.Ordinal)!=true){zoneOpen=false;}   // 點下拉以外＝收起
            if(pressed is {} k&&k.StartsWith("row:",StringComparison.Ordinal)){
                int index=int.Parse(k[4..],System.Globalization.CultureInfo.InvariantCulture);pressed=null;
                uint? before=selected;bool wasFocus=!quickFocus;
                Select(index,e.Modifiers);
                if(confirmDelete==null&&index<Rows.Count)dragRow=Rows[index].Id;   // R10-J：可能是拖曳的起點
                if(e.Clicks>=2&&wasFocus&&before==selected)DoubleClick(commands);
            }else if(pressed is {} qk&&qk.StartsWith("quick:",StringComparison.Ordinal)){
                int index=int.Parse(qk[6..],System.Globalization.CultureInfo.InvariantCulture);pressed=null;
                var qrows=QuickRows;uint? before=quickSelected;bool wasFocus=quickFocus;
                if(index<qrows.Count){quickSelected=qrows[index].Id;quickFocus=true;}
                if(e.Clicks>=2&&wasFocus&&before==quickSelected)DoubleClick(commands);
            }
            return inside;
        }
        if(e.Kind==UiMouseKind.Up){
            // R10-J：記憶清單按下、在快速移動清單放開＝加進快速移動清單（同 Insert 的規則）。
            if(dragRow is uint drag){dragRow=null;if(QuickListRect.Contains(e.Location)&&confirmDelete==null){pressed=null;AddQuickId(drag);return true;}}
            string? key=HitKey(e.Location),was=pressed;pressed=null;
            if(key!=null&&key==was)Activate(key,commands);
            return true;
        }
        return inside;
    }
    /// <summary>記憶清單點選：一般＝單選；Ctrl＝切換這筆（一般模式，最多 12 筆）；Shift＝從上次點的那筆到這筆（推測對應）。</summary>
    void Select(int index,Keys modifiers=Keys.None){
        if(index<0||index>=Rows.Count)return;
        uint id=Rows[index].Id;quickFocus=false;
        bool multi=destination==null;   // 選目的地只用一筆
        if(multi&&(modifiers&Keys.Shift)!=0&&SelectedRow is {} anchor){
            int from=Rows.ToList().FindIndex(r=>r.Id==anchor.Id);
            int lo=Math.Min(from,index),hi=Math.Max(from,index);
            marked.Clear();
            // 從起點往點選方向數，最多 12 筆。
            int step=index>=from?1:-1;
            for(int i=from;marked.Count<MaxMarked&&i>=lo&&i<=hi;i+=step)marked.Add(Rows[i].Id);
            return;   // Shift 不移動起點
        }
        if(multi&&(modifiers&Keys.Control)!=0&&selected!=null){
            if(marked.Count==0)marked.Add(selected.Value);
            if(marked.Remove(id)){if(selected==id)selected=marked.Count>0?marked[^1]:null;return;}
            if(marked.Count>=MaxMarked)return;
            marked.Add(id);selected=id;return;
        }
        selected=id;marked.Clear();marked.Add(id);
    }
    /// <summary>雙擊：選目的地模式＝以這筆傳送（同確認鈕）。一般模式雙擊記憶清單＝改名（3.81 不支援，不做）。</summary>
    void DoubleClick(IUiCommands commands){
        if(destination!=null&&confirmDelete==null)Activate("confirm",commands);
    }
    void ScrollBy(int delta)=>scroll=Math.Clamp(scroll+delta,0,MaxScroll);
    void Activate(string key,IUiCommands commands){
        if(key.StartsWith("zone:",StringComparison.Ordinal)){
            var items=ZoneItems;int i=int.Parse(key[5..],System.Globalization.CultureInfo.InvariantCulture);
            if(i<items.Count){zoneFilter=items[i];zoneOpen=false;scroll=0;Refresh();}
            return;
        }
        switch(key){
            case "cancel" when confirmDelete!=null:AnswerDelete(false);break;
            case "confirm" when confirmDelete!=null:AnswerDelete(true);break;
            case "exit" or "cancel":CloseLocal();commands.ReturnGameFocus();break;
            case "up":ScrollBy(-1);break;
            case "down":ScrollBy(1);break;
            case "sort":sortByName=!sortByName;Refresh();break;
            case "zone":zoneOpen=!zoneOpen;break;
            case "random":
                if(RandomVisible&&destination is {} rq&&Host is {Sending:false} rh)rh.Teleport(rq,RandomTeleportId);
                break;
            case "confirm":
                if(!ConfirmEnabled)break;
                // 選目的地：成功時請求結束 → Update 關窗；失敗保留，可再選。一般模式：原版存檔但不關窗，3.81 沒有東西可存 → 不動作。
                if(destination is {} q&&TargetRow is {} row)Host?.Teleport(q,row.Id);
                break;
        }
    }

    /// <summary>ESC（sub_BDAA10）關窗；Delete＝刪除（846）；Insert＝加進快速移動清單；上下鍵移動選取（MemoryPlaceKeyBoard，推測）。</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(!e.Down)return false;
        if(confirmDelete!=null){
            // 846 確認中：Enter／Y＝是，Esc／N＝否；其他鍵吃掉（避免方向鍵換了選取卻刪到原本那筆）。
            if(e.Repeat)return true;
            if(e.KeyData is Keys.Enter or Keys.Y)AnswerDelete(true);
            else if(e.KeyData is Keys.Escape or Keys.N)AnswerDelete(false);
            return true;
        }
        if(zoneOpen&&e.KeyData==Keys.Escape){zoneOpen=false;return true;}
        switch(e.KeyData){
            case Keys.Delete:BeginDelete();return true;
            case Keys.Insert:AddQuick();return true;
            case Keys.Escape:CloseLocal();commands.ReturnGameFocus();return true;
            case Keys.Up or Keys.Down:{
                var rows=Rows;if(rows.Count==0)return true;
                int at=selected is uint id?rows.ToList().FindIndex(r=>r.Id==id):-1;
                at=Math.Clamp(at<0?(e.KeyData==Keys.Down?0:rows.Count-1):at+(e.KeyData==Keys.Down?1:-1),0,rows.Count-1);
                Select(at);
                if(at<scroll)scroll=at;else if(at>=scroll+rowCount)scroll=at-rowCount+1;
                return true;
            }
        }
        return false;
    }

    public bool IsDragHandle(Point location)=>Visible&&Bounds.Contains(location)&&location.Y-origin.Y<moveable&&HitKey(location)==null;
    public void MoveTo(Point location)=>origin=location;
}
