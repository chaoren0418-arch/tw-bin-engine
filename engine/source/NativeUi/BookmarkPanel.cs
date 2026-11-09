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
//   - 清單順序沿用書籤資料的順序（排序鈕 Sorting_Button 沒做）。
// 不做（報告已列，待驗證）：快速移動清單 QuickMove_List、區域篩選下拉 ZoneFilter_Combobox、隨機傳送 RandomTeleport_Button、
//   排序 Sorting_Button、13 色調色盤 ColorPalette、CloningMemoryPlace_List、ESC 關窗時的 2847「座標記憶結束」、
//   刪除（op 106 csd＋846 確認）與新增書籤的原生入口——新增／刪除仍在 WinForms「記憶座標」分頁（BookmarkUi.cs）。
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

public sealed class BookmarkWindowPanel:IUiMovablePanel,IUiClosablePanel {
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

    bool open;P06.TeleportDestinationRequest? destination;long dismissedDestination=-1;
    uint? selected;int scroll;string? pressed;
    (int Session,uint Actor,int Revision)? seen;
    P06.BookmarkView? view;

    public IBookmarkWindowHost? Host{get;private set;}
    public void Attach(IBookmarkWindowHost host)=>Host=host;

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
    internal IReadOnlyList<P06.Bookmark> Rows=>view?.Items??[];
    internal int MaxScroll=>Math.Max(0,Rows.Count-rowCount);
    internal string CountText=>view==null?"":$"{view.Items.Length}/{view.Capacity}";
    internal bool ConfirmEnabled=>destination==null||(Host is {Sending:false}&&SelectedRow is {DeletePending:false});
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

        usable=missing.Count==0;
        if(!usable)UiResourceLog.Write($"panel {PanelId} 停用：缺少版面所需的 surf（{string.Join("；",missing)}）");
        open=false;destination=null;selected=null;scroll=0;pressed=null;
    }

    static UiNode Direct(UiNode parent,string element,string name){
        var found=parent.ChildrenNamed(element).Where(c=>c.Attr("Name")==name).Take(2).ToList();
        if(found.Count!=1)throw new UiLayoutException(parent.Layout.FileName,parent.Path,$"{element} Name=\"{name}\" {(found.Count==0?"缺少":"不唯一")}");
        return found[0];
    }

    // ---- 狀態 ----
    public void Update(IUiGameState state){
        if(!state.InWorld||Host is not {} host){if(open)CloseLocal();view=null;seen=null;return;}
        var v=host.Bookmarks;var q=host.Destination;
        // 原版 sub_931D60：單筆書籤收包後開窗。整份清單、第一次看到這個角色、或換角色時不開。
        bool sameActor=seen is {} s&&s.Session==v.Session&&s.Actor==v.ActorId;
        if(sameActor&&seen!.Value.Revision!=v.Revision&&v.Notice==SingleBookmarkNotice&&v.ActorId!=0)OpenNormal();
        if(!sameActor){selected=null;scroll=0;}
        seen=(v.Session,v.ActorId,v.Revision);view=v;
        if(selected is uint id&&!v.Items.Any(r=>r.Id==id))selected=null;
        scroll=Math.Clamp(scroll,0,MaxScroll);
        // 選目的地：新請求開窗（同一請求被取消後不重開）；請求結束時，由它開的窗一起關。
        if(q!=null&&q.Revision!=dismissedDestination&&q.Revision!=destination?.Revision){destination=q;open=true;pressed=null;}
        else if(q==null&&destination!=null){destination=null;open=false;pressed=null;}
    }
    void OpenNormal(){if(!open){open=true;pressed=null;}}
    /// <summary>XML Toggle="true"：開／關一般模式（熱鍵或選單入口由其他任務接；選目的地模式中＝取消）。</summary>
    public void Toggle(){if(open)CloseLocal();else if(usable)OpenNormal();}
    /// <summary>關閉鈕、取消鈕、ESC、4 層堆疊擠出：選目的地模式中通知宿主取消（不送封包）。</summary>
    public void Close()=>CloseLocal();
    void CloseLocal(){
        if(destination is {} q){dismissedDestination=q.Revision;destination=null;Host?.CancelTeleport(q);}
        open=false;pressed=null;
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

    string? HitKey(Point p){
        if(ExitRect.Contains(p))return "exit";
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
    }

    // ---- 輸入 ----
    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Wheel){
            if(!inside)return false;
            if(e.Delta!=0)ScrollBy(-Math.Sign(e.Delta));
            return true;
        }
        if(e.Button!=MouseButtons.Left)return inside;
        if(e.Kind==UiMouseKind.Down){
            pressed=HitKey(e.Location);
            if(pressed is {} k&&k.StartsWith("row:",StringComparison.Ordinal)){Select(int.Parse(k[4..]));pressed=null;}
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
        switch(key){
            case "exit" or "cancel":CloseLocal();commands.ReturnGameFocus();break;
            case "up":ScrollBy(-1);break;
            case "down":ScrollBy(1);break;
            case "confirm":
                if(!ConfirmEnabled)break;
                if(destination is {} q){if(SelectedRow is {} row)Host?.Teleport(q,row.Id);}   // 成功時請求結束 → Update 關窗；失敗保留，可再選
                else{CloseLocal();commands.ReturnGameFocus();}
                break;
        }
    }

    /// <summary>ESC（sub_BDAA10）關窗；上下鍵移動選取（MemoryPlaceKeyBoard，推測）。</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible||!e.Down)return false;
        switch(e.KeyData){
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
