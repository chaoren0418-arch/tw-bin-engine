namespace P06.NativeUi;

// R4-J（T301／#713）：原生書籤窗 CBookmarkWindowRenewal（BookmarkUI.xml 的 Bookmark_Window）。
// 依據：docs/reference/ui-layout.md 第 6 節（UI§6）、followups-3.md 第 3 節（FU3§3）、gap-matrix.md 第 13.5 節 R4-J。
// 版面全部在執行期從 Tile.pak 讀 BookmarkUI.xml（不複製遊戲資源）；元件位置＝XML 座標，巢狀元件相對父元件（同 SummonPanel）。
// 已驗證（XML，UI§6）：框 FrameSurf 4313（263×420，可拖曳 Moveable=20）、底 4312 (6,9)、關閉鈕 Exit_Button (249,15)、
//   記憶座標清單 MemoryPlace_List (22,162) 200×204（12 列 CheckButton，每列 Height 17，選取圖 PushSurf 4336）、
//   清單捲軸（WheelControl (23,163) 內的 ScrollBar (218,-5)：上鈕 (0,0)、下鈕 (0,196)、底 (0,14)、拉桿 (0,16)）、
//   確認 Confirm_Button (67,395)／取消 Cancel_Button (134,395)、筆數 BookmarkCount_Label (200,397) 20×15。
// 已驗證（BIN，FU3§3.1）：
//   - 同一個窗是一般書籤窗，也是傳送控制戒指的「選目的地」模式（sub_BDFE90(1)）。選好後送傳送；3.81 送 C6 HD（書籤地圖、書籤 id），
//     不送 TW16 的 hhh（FU3§3.2）——送包由宿主（RingWindows.cs → LoginConnection.SendTeleportDestination）負責，本面板不碰封包。
//   - ESC（鍵碼 27）由 sub_BDAA10 關窗。
//   - op 2 S_ADD_BOOKMARK 的收包 sub_931D60 把新書籤寫進這個窗後開窗（3.81 對應 S92；見下方 OpenOnSingleBookmark）。
// 推測（標明）：
//   - 框先畫、底 4312 再畫（框比底大 6／9 px，兩種疊法在框是外框時看起來一樣）；列文字白色、左邊留 3 px、垂直置中；
//     選取列畫 PushSurf 4336 在文字之下；送刪除後待確認的列文字灰色。
//   - 一般模式的確認鈕＝關窗（原版一般模式的 Confirm 事件內容沒追）；取消鈕／關閉鈕／ESC 在選目的地模式都是「取消，不送」。
//   - 選目的地模式在請求結束（送出成功或被清掉）時關窗。拉桿位置＝ScrollStart + (ScrollHeight − 拉桿高)·捲動/最大捲動。
//   - 清單預設順序沿用書籤資料的順序。
// R5-B（T307／#734）收尾：
//   - 刪除（已驗證：FU3§3.1 sub_BDC1F0 送 TW16 op 106 csd，先跳字串 846「要刪除所選擇的項目？」）：3.81 送 C3（書籤名稱），
//     由編輯宿主 IBookmarkEditHost（BookmarkUi.cs → LoginConnection.SendBookmark）送出。推測：觸發鍵＝Delete 鍵或在列上按右鍵
//     （sub_BDC1F0 的呼叫點沒追）；846 確認時 Confirm_Button／Enter／Y＝是，Cancel_Button／ESC／N＝否；提示字畫在調色盤那一列
//     (22,372)（846 原版是另一個訊息窗，樣子沒追，這裡是引擎自訂畫法）。
//   - 新增（已驗證：sub_4FE1D0／sub_502720 送 TW16 op 84 cs；3.81 送 C165）：XML 沒有輸入框，原版從哪裡輸入名稱沒追。
//     引擎自訂：Insert 鍵進入命名，字元走 IUiTextInput（WM_CHAR，含 IME），Enter 送出、ESC 放棄、Backspace 刪字；名稱規則同 WinForms 分頁。
//   - 排序 Sorting_Button (182,42)：按一下在「資料順序」與「名稱順序」之間切換（推測：原版排序鍵沒追）。
//   - 區域篩選 ZoneFilter_Combobox (129,141)：Expand_Button 展開 Expand_List（Y=19，每列 19，PageSize 7，選取圖 4320），
//     第 0 項 Caption="ALL"（XML），其餘是書籤裡出現過的地圖編號（推測：原版列的是區域名稱，名稱表沒追，暫顯示地圖編號）。
//     Text_Label 顯示目前的篩選（XML 的 String 878 內容沒追，ALL 時顯示 "ALL"）。
// 不做（待查證）：快速移動清單 QuickMove_List（5 列 String 0–4 的資料來源沒追，規則 5：不畫沒有資料的清單，標題也不畫）、
//   隨機傳送 RandomTeleport_Button、13 色調色盤 ColorPalette、CloningMemoryPlace_List、ESC 關窗時的 2847「座標記憶結束」。
//   這些元件不畫（規則 5：不畫不能用的鈕）。

/// <summary>原生書籤窗的宿主（BookmarkUi.cs／RingWindows.cs 的 ClientForm 轉接）。面板只讀狀態、轉交動作，不碰 LoginConnection。</summary>
public interface IBookmarkWindowHost {
    /// <summary>目前的書籤清單（與 WinForms 分頁同一份 BookmarkView）。</summary>
    P06.BookmarkView Bookmarks{get;}
    /// <summary>傳送控制戒指的選目的地請求（S36 type 1＋技能 5／69）；null＝沒有。</summary>
    P06.TeleportDestinationRequest? Destination{get;}
    /// <summary>傳送送出中（確認鈕停用，擋重複送出）。</summary>
    bool Sending{get;}
    /// <summary>確認鈕：對 shown 這一次的請求以書籤 bookmarkId 施放一次；過期或不合法由宿主拒絕。</summary>
    void Teleport(P06.TeleportDestinationRequest shown,uint bookmarkId);
    /// <summary>取消／關閉／ESC：結束 shown 這一次的請求，不送封包。</summary>
    void CancelTeleport(P06.TeleportDestinationRequest shown);
}

/// <summary>R5-B（#734）：新增／刪除書籤的宿主（BookmarkUi.cs 接 LoginConnection.SendBookmark）。和 IBookmarkWindowHost 分開，
/// 讓既有宿主（RingWindows.cs）不用改。沒接上時原生窗不提供新增／刪除（WinForms 分頁仍可用）。</summary>
public interface IBookmarkEditHost {
    /// <summary>新增／刪除送出中（擋重複送出）。</summary>
    bool Sending{get;}
    /// <summary>以 shown 這份清單新增名稱 name 的書籤（C165）；過期或不合法由宿主拒絕。</summary>
    void Add(P06.BookmarkView shown,string name);
    /// <summary>以 shown 這份清單刪除書籤 bookmarkId（C3，送書籤名稱）；846 已確認。</summary>
    void Delete(P06.BookmarkView shown,uint bookmarkId);
}

public sealed class BookmarkWindowPanel:IUiMovablePanel,IUiClosablePanel,IUiTextInput {
    [UiPanel(178)] internal static UiPanelRegistration Registration=>new(BookmarkWindowPanel.PanelId,BookmarkWindowPanel.LayoutName,_=>new BookmarkWindowPanel());   // R4-J (#713): CBookmarkWindowRenewal (BookmarkUI.xml)
    public const string PanelId="Bookmark_Window",LayoutName="BookmarkUI";
    /// <summary>Bookmarks.ReceiveOne（S92＝3.81 的單筆書籤，對應 TW16 op 2）設定的提示文字；用來分辨單筆收包與整份清單（不改 Bookmarks.cs）。</summary>
    public const string SingleBookmarkNotice="已收到伺服器書籤資料。";

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
    string zoneAll="ALL";

    bool open;P06.TeleportDestinationRequest? destination;long dismissedDestination=-1;
    uint? selected;int scroll;string? pressed;
    (int Session,uint Actor,int Revision)? seen;
    P06.BookmarkView? view;
    P06.Bookmark[] rows=[];
    bool sortByName;ushort? zoneFilter;bool zoneOpen;int zoneScroll;
    uint? confirmDelete;System.Text.StringBuilder? naming;string? editError;

    public IBookmarkWindowHost? Host{get;private set;}
    public void Attach(IBookmarkWindowHost host)=>Host=host;
    /// <summary>R5-B：新增／刪除宿主（null＝原生窗不提供新增／刪除）。</summary>
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
    internal int Scroll=>scroll;
    internal int RowCount=>rowCount;
    /// <summary>畫面上的清單：先依區域篩選，再依排序鈕排序。</summary>
    internal IReadOnlyList<P06.Bookmark> Rows=>rows;
    internal int MaxScroll=>Math.Max(0,Rows.Count-rowCount);
    internal string CountText=>view==null?"":$"{view.Items.Length}/{view.Capacity}";
    internal bool ConfirmEnabled=>confirmDelete!=null?Editor is {Sending:false}:destination==null||(Host is {Sending:false}&&SelectedRow is {DeletePending:false});
    internal bool SortByName=>sortByName;
    internal ushort? ZoneFilter=>zoneFilter;
    internal bool ZoneListOpen=>Visible&&zoneOpen;
    /// <summary>下拉的項目：null＝ALL，其餘是書籤出現過的地圖編號（由小到大）。</summary>
    internal IReadOnlyList<ushort?> ZoneItems=>[null,..(view?.Items??[]).Select(b=>b.MapId).Distinct().Order().Select(m=>(ushort?)m)];
    internal string ZoneText=>zoneFilter is ushort m?m.ToString(System.Globalization.CultureInfo.InvariantCulture):zoneAll;
    /// <summary>846 確認中的書籤 id。</summary>
    internal uint? ConfirmingDelete=>confirmDelete;
    /// <summary>命名中（Insert 鍵）的名稱；null＝沒有在命名。</summary>
    internal string? NamingText=>naming?.ToString();
    internal string? EditError=>editError;
    bool CanEdit=>destination==null&&Editor is {Sending:false}&&view is {CanEdit:true};
    // Rectangle.IsEmpty 要 X／Y 也是 0 才成立；缺圖的鈕有位置、沒有大小，所以看寬高。
    bool HasSorting=>sorting is {Rect:{Width:>0,Height:>0}};
    bool HasZone=>zoneExpand is {Rect:{Width:>0,Height:>0}};
    P06.Bookmark? SelectedRow=>selected is uint id?Rows.FirstOrDefault(r=>r.Id==id):null;

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
        // 只畫記憶座標清單的標題（快速移動清單沒做，它的標題 QuickMoveListTitle_Image 也不畫）。
        titles=window.ChildrenNamed("Image").Where(n=>n.Attr("Name")=="MemoryZoneListTitle_Image").Select(n=>(Pos(n,zero),SummonPanel.SurfId(n,"Surf"))).ToArray();
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
        sorting=null;zoneExpand=null;zoneLabel=Rectangle.Empty;zoneRowSurf=null;zoneBack=null;zoneRowHeight=zonePageSize=zoneListWidth=0;zoneAll="ALL";
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
                if(items[0].Attr("Caption") is {Length:>0} all)zoneAll=all;
            }
            if(el.ChildrenNamed("Image").FirstOrDefault(n=>n.Attr("Name")=="Background_Image") is {} zb)
                zoneBack=(new(zoneListAt.X+(zb.TryInt("X")??0),zoneListAt.Y+(zb.TryInt("Y")??0)),SummonPanel.SurfId(zb,"Surf"));
            if(zoneRowHeight<=0||zoneListWidth<=0)zoneExpand=null;
        }
        }catch(UiLayoutException ex){zoneExpand=null;UiResourceLog.Write($"panel {PanelId} 區域篩選略過：{ex.Message}");}

        usable=missing.Count==0;
        if(!usable)UiResourceLog.Write($"panel {PanelId} 停用：缺少版面所需的 surf（{string.Join("；",missing)}）");
        open=false;destination=null;selected=null;scroll=0;pressed=null;
        sortByName=false;zoneFilter=null;zoneOpen=false;zoneScroll=0;ResetEdit();this.rows=view?.Items??[];
    }
    void ResetEdit(){confirmDelete=null;naming=null;editError=null;}

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
        if(sameActor&&seen!.Value.Revision!=v.Revision&&v.Notice==SingleBookmarkNotice&&v.ActorId!=0)OpenNormal();
        if(!sameActor){selected=null;scroll=0;zoneFilter=null;zoneOpen=false;zoneScroll=0;ResetEdit();}
        seen=(v.Session,v.ActorId,v.Revision);view=v;
        if(zoneFilter is ushort z&&!v.Items.Any(b=>b.MapId==z))zoneFilter=null;   // 該地圖已沒有書籤 → 回到 ALL
        Refresh();
        if(confirmDelete is uint del&&!v.Items.Any(r=>r.Id==del&&!r.DeletePending))confirmDelete=null;
        if(!v.CanEdit&&naming!=null)naming=null;
        // 選目的地：新請求開窗（同一請求被取消後不重開）；請求結束時，由它開的窗一起關。
        if(q!=null&&q.Revision!=dismissedDestination&&q.Revision!=destination?.Revision){destination=q;open=true;pressed=null;ResetEdit();}
        else if(q==null&&destination!=null){destination=null;open=false;pressed=null;}
    }
    /// <summary>重算畫面清單（篩選、排序），並修正選取與捲動。</summary>
    void Refresh(){
        IEnumerable<P06.Bookmark> list=view?.Items??[];
        if(zoneFilter is ushort z)list=list.Where(b=>b.MapId==z);
        if(sortByName)list=list.OrderBy(b=>b.Name,StringComparer.Ordinal).ThenBy(b=>b.Id);
        rows=list.ToArray();
        if(selected is uint id&&!rows.Any(r=>r.Id==id))selected=null;
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
    }

    // ---- 新增／刪除（R5-B） ----
    /// <summary>Delete 鍵／右鍵：選取的書籤進入 846 確認。</summary>
    internal bool BeginDelete(){
        if(!CanEdit||SelectedRow is not {DeletePending:false} row)return false;
        naming=null;editError=null;confirmDelete=row.Id;return true;
    }
    void AnswerDelete(bool yes){
        if(confirmDelete is not uint id)return;
        if(!yes){confirmDelete=null;return;}
        if(Editor is not {Sending:false} editor||view is not {} v)return;
        confirmDelete=null;editor.Delete(v,id);
    }
    /// <summary>Insert 鍵：開始命名新書籤（引擎自訂入口）。</summary>
    internal bool BeginAdd(){
        if(!CanEdit||view is not {} v||v.Items.Length>=v.Capacity)return false;
        confirmDelete=null;editError=null;naming=new();return true;
    }
    void SubmitAdd(){
        if(naming is not {} n||Editor is not {Sending:false} editor||view is not {} v)return;
        string name=n.ToString();
        if(P06.BookmarkPackets.NameError(name) is {} error){editError=error;return;}
        if(v.Items.Any(b=>string.Equals(b.Name,name,StringComparison.OrdinalIgnoreCase))){editError="已有同名的書籤。";return;}
        naming=null;editError=null;editor.Add(v,name);
    }
    /// <summary>命名中的字元（WM_CHAR，IME 組字後）；名稱上限 64 字元，Big5 位元組上限由 NameError 檢查。</summary>
    public bool Char(char c){
        if(Visible&&confirmDelete!=null&&c is 'y' or 'Y' or 'n' or 'N')return true;   // 846 確認的 Y／N 不漏到聊天輸入列
        if(!Visible||naming==null||char.IsControl(c))return false;
        if(naming.Length<64)naming.Append(c);
        editError=null;return true;
    }

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
        if(ZoneExpandRect.Contains(p)||ZoneLabelRect.Contains(p))return "zone";
        if(ConfirmRect.Contains(p))return "confirm";
        if(CancelRect.Contains(p))return "cancel";
        if(UpRect.Contains(p))return "up";
        if(DownRect.Contains(p))return "down";
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
        Draw(frameSurf,Point.Empty);Draw(baseSurf,baseAt);
        foreach(var (at,id) in titles)Draw(id,at);
        Button(exit);
        if(scrollBack is (Point bat,int bid))Draw(bid,bat);
        Button(up);Button(down);
        if(ThumbAt is Point tp&&thumb is (Point _,int tid))canvas.DrawSurface(surf(tid),tp.X,tp.Y);
        var rows=Rows;
        for(int r=0;r<rowCount&&scroll+r<rows.Count;r++){
            var row=rows[scroll+r];var rect=RowRect(r);
            if(row.Id==selected)canvas.DrawSurface(surf(rowSurf),rect.X,rect.Y);
            var m=UiText.Measure(canvas.Graphics,row.Name);
            canvas.DrawText(row.Name,rect.X+3,rect.Y+(rect.Height-m.Height)/2,row.DeletePending?Color.Gray:Color.White);
        }
        Button(confirm,ConfirmEnabled);Button(cancel);
        if(!countLabel.IsEmpty&&CountText.Length>0){
            var m=UiText.Measure(canvas.Graphics,CountText);var c=Abs(countLabel);
            canvas.DrawText(CountText,c.X+(c.Width-m.Width)/2,c.Y+(c.Height-m.Height)/2,Color.White);
        }
        if(HasSorting)Button(sorting);
        if(HasZone){
            Button(zoneExpand);
            var lr=ZoneLabelRect;
            if(!lr.IsEmpty){var m=UiText.Measure(canvas.Graphics,ZoneText);canvas.DrawText(ZoneText,lr.X+(lr.Width-m.Width)/2,lr.Y+(lr.Height-m.Height)/2,Color.White);}
        }
        // 846 確認／命名提示：畫在調色盤那一列（調色盤不畫），引擎自訂畫法。
        string? prompt=confirmDelete!=null?DeletePrompt:naming!=null?"新書籤名稱："+naming+"_":null;
        if(prompt!=null)canvas.DrawText(prompt,origin.X+PromptAt.X,origin.Y+PromptAt.Y,Color.White,Color.Black);
        if(editError!=null)canvas.DrawText(editError,origin.X+PromptAt.X,origin.Y+PromptAt.Y-14,Color.FromArgb(255,96,96),Color.Black);
        // 展開清單最後畫（蓋在記憶座標清單上）。
        if(ZoneListOpen&&HasZone){
            if(zoneBack is (Point zat,int zid))Draw(zid,zat);
            var items=ZoneItems;
            for(int r=0;r<zonePageSize&&zoneScroll+r<items.Count;r++){
                var rect=ZoneRowRect(r);var item=items[zoneScroll+r];
                if(item==zoneFilter&&zoneRowSurf is int zs)canvas.DrawSurface(surf(zs),rect.X,rect.Y);
                string text=item is ushort m?m.ToString(System.Globalization.CultureInfo.InvariantCulture):zoneAll;
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
        // 右鍵在列上：選取並進入 846 確認（推測，見檔頭）。
        if(e.Button==MouseButtons.Right&&e.Kind==UiMouseKind.Down&&!zoneOpen&&HitKey(e.Location) is {} rk&&rk.StartsWith("row:",StringComparison.Ordinal)){
            Select(int.Parse(rk[4..],System.Globalization.CultureInfo.InvariantCulture));BeginDelete();return true;
        }
        if(e.Button!=MouseButtons.Left)return inside;
        if(e.Kind==UiMouseKind.Down){
            pressed=HitKey(e.Location);
            if(zoneOpen&&pressed is not ("zone" or "zone-list")&&pressed?.StartsWith("zone:",StringComparison.Ordinal)!=true){zoneOpen=false;}   // 點下拉以外＝收起
            if(pressed is {} k&&k.StartsWith("row:",StringComparison.Ordinal)){Select(int.Parse(k[4..],System.Globalization.CultureInfo.InvariantCulture));pressed=null;}
            return inside;
        }
        if(e.Kind==UiMouseKind.Up){
            string? key=HitKey(e.Location),was=pressed;pressed=null;
            if(key!=null&&key==was)Activate(key,commands);
            return true;
        }
        return inside;
    }
    void Select(int index){if(index>=0&&index<Rows.Count)selected=Rows[index].Id;}
    void ScrollBy(int delta)=>scroll=Math.Clamp(scroll+delta,0,MaxScroll);
    void Activate(string key,IUiCommands commands){
        if(key.StartsWith("zone:",StringComparison.Ordinal)){
            var items=ZoneItems;int i=int.Parse(key[5..],System.Globalization.CultureInfo.InvariantCulture);
            if(i<items.Count){zoneFilter=items[i];zoneOpen=false;scroll=0;Refresh();}
            return;
        }
        switch(key){
            case "cancel" when confirmDelete!=null:AnswerDelete(false);break;
            case "cancel" when naming!=null:naming=null;editError=null;break;
            case "confirm" when confirmDelete!=null:AnswerDelete(true);break;
            case "confirm" when naming!=null:SubmitAdd();break;
            case "exit" or "cancel":CloseLocal();commands.ReturnGameFocus();break;
            case "up":ScrollBy(-1);break;
            case "down":ScrollBy(1);break;
            case "sort":sortByName=!sortByName;Refresh();break;
            case "zone":zoneOpen=!zoneOpen;break;
            case "confirm":
                if(!ConfirmEnabled)break;
                if(destination is {} q){if(SelectedRow is {} row)Host?.Teleport(q,row.Id);}   // 成功時請求結束 → Update 關窗；失敗保留，可再選
                else{CloseLocal();commands.ReturnGameFocus();}
                break;
        }
    }

    /// <summary>ESC（sub_BDAA10）關窗；上下鍵移動選取（MemoryPlaceKeyBoard，推測）。</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible)return false;
        if(naming!=null){
            // 命名中：Esc 放棄、Enter 送出、Backspace 刪字；可列印鍵走 WM_CHAR → Char()（同 PledgePanel）。
            if(!e.Down)return e.KeyCode is Keys.Enter or Keys.Escape or Keys.Back;
            if(e.KeyData==Keys.Escape){naming=null;editError=null;return true;}
            if(e.KeyData==Keys.Enter){if(!e.Repeat)SubmitAdd();return true;}
            if(e.KeyData==Keys.Back){if(naming.Length>0)naming.Length--;editError=null;return true;}
            return false;
        }
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
            case Keys.Insert:BeginAdd();return true;
            case Keys.Escape:CloseLocal();commands.ReturnGameFocus();return true;
            case Keys.Up or Keys.Down:{
                var rows=Rows;if(rows.Count==0)return true;
                int at=selected is uint id?rows.ToList().FindIndex(r=>r.Id==id):-1;
                at=Math.Clamp(at<0?(e.KeyData==Keys.Down?0:rows.Count-1):at+(e.KeyData==Keys.Down?1:-1),0,rows.Count-1);
                selected=rows[at].Id;
                if(at<scroll)scroll=at;else if(at>=scroll+rowCount)scroll=at-rowCount+1;
                return true;
            }
        }
        return false;
    }

    public bool IsDragHandle(Point location)=>Visible&&Bounds.Contains(location)&&location.Y-origin.Y<moveable&&HitKey(location)==null;
    public void MoveTo(Point location)=>origin=location;
}
