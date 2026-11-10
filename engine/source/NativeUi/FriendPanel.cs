using System.Globalization;
namespace P06.NativeUi;

// M7-V（T222／#529）：好友、封鎖、信件封鎖三窗（docs/reference/windows.md §1.2）。
// 三個 XML（FriendUI／ExcludeUI／MailExcludeUI）版面相同，只差根節點、視窗名、標題圖與事件名，所以共用一個面板類別。
// 版面一律在執行期從原客戶端 Tile.pak 讀取；這裡只寫元素名稱，不寫座標。
// 已驗證（XML）：底圖、框、關閉鈕、標題圖、14 列 RadioButton、Info 欄位置、捲軸與上下鈕、人數標籤的座標與 surf。
// 推測／待驗證（不畫或只照最小做法）：
//   - Info_Label 顯示什麼（上線狀態或備註）待驗證 → 不畫，只保留位置。
//   - 名稱與人數的字型、顏色、對齊待驗證 → 用 UiText 預設字、白字、左上 +2 px。
//   - Moveable="20" 推測為可拖曳的上方高度（px）。
//   - surf 寫法 "4990;1" 的第二個數字意義待驗證 → 只用第一個 surf。
//   - 開窗方式（快捷鍵或指令）待驗證（M37）→ 沿用 BuddyUi.cs 的過渡入口。
// R9-H（T368／#852）：清單列事件（FU9 §9.2 XML 窗事件表，已驗證，靜態）：
//   - FriendUI `FriendName` 4142（Delete，sub_B18F10）：Y/N 字串 1813「要刪除你所選擇的好友嗎?」→ 是 → cs 124（＝3.81 C_DelBuddy C202）。
//     1813 原文已用 docs/reference/rea-scripts/t267/text_tables.py 從 0-客戶端 Text.pak 的 string-c.tbl 讀出確認；執行期照字串表，
//     表不在時用同一句。提示畫在聊天輸入列 YesNoButtons.Line（FriendDeletePromptPanel）；Y＝是、N／Esc＝否（推測：原版
//     Y/N 提示的確切回答方式沿用 InputWindow 的 Y/N 規則，真機再確認）。
//   - FriendUI `FriendName` 515（雙擊，sub_B19030）：把 `"名稱 ` 放進聊天輸入列（密語前綴，PlayerChatProtocol 以 '"' 開頭解析成密語）。
//   - FriendUI 27（Esc，sub_B191F0）：關窗。ExcludeUI、MailExcludeUI 的 Esc 也只關本窗（推測：XML 同構；R7-H 全域 ESC 仍在後面）。
//   - ExcludeUI `ExcludeName` 4142（sub_B18D30）：不確認，直接送 cs 7（＝3.81 C_Exclude C171，3.81 是切換：已在名單＝移除）。
//   - MailExcludeUI 4142：TW16 只在旗標 0x2000 時送 csc 7，3.81 不支援信件封鎖 → 不送（FU9 §9.2）。
//   送封包經 IFriendWindowHost（由 NativeUi 外的轉接實作；面板不碰 LoginConnection）。沒有宿主時 Delete 不詢問、不送。

public enum FriendListKind { Friend, Exclude, MailExclude }

/// <summary>One list row. Online is only known for friends (S39 second column); the panel does not paint it (Info 待驗證).</summary>
public sealed record FriendListRow(string Name,bool? Online=null);

public sealed record FriendListData(FriendListRow[] Rows) {
    public static readonly FriendListData Empty=new([]);
}

/// <summary>Read-only adapter to the game model (implemented outside NativeUi; UI modules never touch LoginConnection).</summary>
public interface IFriendListSource {
    FriendListData Read(FriendListKind kind);
}

/// <summary>
/// R9-H（#852）：清單列事件的送出端（實作在 NativeUi 外，例如 LoginConnection.SendBuddy 的轉接）。
/// Friend → C202（BuddyAction.Delete）；Exclude → C171（BuddyAction.ToggleExclude）。MailExclude 不會呼叫。
/// 回傳 true＝已受理（開始送出）；false＝目前不能送（不在世界、名單已更新、名稱不合法）。
/// </summary>
public interface IFriendWindowHost {
    bool Remove(FriendListKind kind,string name);
}

/// <summary>1813 Y/N 詢問：Revision 每次新詢問 +1，回答時用來確認還是同一個詢問。</summary>
public sealed record FriendDeleteConfirm(int Revision,string Name,string Text);

/// <summary>What differs between the three XML files (windows.md §1.2 table).</summary>
public sealed record FriendWindowSpec(FriendListKind Kind,string Layout,string RootName,string WindowName,string TitleImageName,string NameEvent) {
    public static readonly FriendWindowSpec Friend=new(FriendListKind.Friend,"FriendUI","FriendUI","Friend_Window","Friend_Image","FriendName");
    public static readonly FriendWindowSpec Exclude=new(FriendListKind.Exclude,"ExcludeUI","ExcludeUI","Exclude_Window","Exclude_Image","ExcludeName");
    public static readonly FriendWindowSpec MailExclude=new(FriendListKind.MailExclude,"MailExcludeUI","MailExcludeUI","Mailexclude_Window","Mailexclude_Image","MailExcludeName");
    public static IReadOnlyList<FriendWindowSpec> All{get;}=[Friend,Exclude,MailExclude];
    public static FriendWindowSpec For(FriendListKind kind)=>All.First(s=>s.Kind==kind);
    /// <summary>Panel id = the XML window name.</summary>
    public string Id=>WindowName;
}

public static class FriendPanels {
    /// <summary>Set by the client form (BuddyUi.cs). null → the windows show an empty list.</summary>
    public static IFriendListSource? Source{get;set;}
    [UiPanel(120)]   // M7-V (#529): Friend_Window嚗xclude_Window嚗ailexclude_Window
    public static IReadOnlyList<UiPanelRegistration> Registrations{get;}=
        FriendWindowSpec.All.Select(s=>new UiPanelRegistration(s.Id,s.Layout,root=>new FriendPanel(s,()=>Source,()=>Host,root.Find))).ToArray();
    /// <summary>R9-H（#852）：Delete 的送出端。null → Delete 不詢問、不送（接線見 docs/reports/issue-852.md）。</summary>
    public static IFriendWindowHost? Host{get;set;}

    /// <summary>String 1813（string-c.tbl，FU9 §9.2）。</summary>
    public const int DeleteConfirmId=1813;
    /// <summary>0-客戶端 string-c.tbl 1813 原文（text_tables.py 讀出）；字串表不在時用。</summary>
    public const string DeleteConfirmFallback="要刪除你所選擇的好友嗎?";
    public static string DeleteConfirmText=>ServerMessageText.Table is {} t&&DeleteConfirmId<t.Count
        &&ServerMessageText.DisplayText(t[DeleteConfirmId]).Trim() is {Length:>0} s?s:DeleteConfirmFallback;
    /// <summary>密語前綴：`"名稱 `（sub_B19030）。</summary>
    public static string WhisperPrefix(string name)=>"\""+name+" ";
}

/// <summary>
/// R9-H（#852）：1813 Y/N 提示畫在聊天輸入列（YesNoButtons.Line，InputWindow 放提示的位置；WO §6、WIN2 §5.3）。
/// 推測（真機再確認）：好友刪除的 Y/N 與伺服器詢問用同一個輸入列提示；Y＝是，N／Esc＝否。
/// 伺服器詢問（YesNoPanel）顯示中時本面板讓位（不畫、不收鍵），詢問保留到它結束。聊天輸入列開著時不收 Y／N（字給聊天列）。
/// 滑鼠一律不攔。
/// </summary>
public sealed class FriendDeletePromptPanel(Func<string,IUiPanel?>? find=null):IUiPanel {
    public const string PanelId="R9H_FriendDeletePrompt";
    [UiPanel(195)] internal static UiPanelRegistration Registration=>new(PanelId,"uistatus86",root=>new FriendDeletePromptPanel(root.Find));   // R9-H (#852): string 1813 Y/N on the chat input line

    bool inWorld;
    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Float;
    FriendPanel? Friend=>find?.Invoke(FriendWindowSpec.Friend.Id) as FriendPanel;
    bool Yielding=>find?.Invoke(YesNoPanel.PanelId) is {Visible:true};
    bool ChatOpen=>find?.Invoke(ChatPanel.WindowName) is ChatPanel{Input.Open:true};
    public FriendDeleteConfirm? Current=>inWorld&&!Yielding?Friend?.PendingDelete:null;
    public bool Visible=>Current!=null;
    public Rectangle Bounds=>Visible?YesNoButtons.Line:Rectangle.Empty;

    public void Load(UiLayout layout,SurfCatalog surfs){}
    public void Update(IUiGameState state)=>inWorld=state.InWorld;
    public void Paint(UiCanvas canvas){
        if(Current is not {} c)return;
        var line=YesNoButtons.Line;var g=canvas.Graphics;var saved=g.Save();
        try{
            g.IntersectClip(line);var size=UiText.Measure(g,c.Text);
            canvas.DrawText(c.Text,line.X,line.Y+Math.Max(0,(line.Height-size.Height)/2),Color.White,Color.Black);
        }finally{g.Restore(saved);}
    }
    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(Current is not {} c||!e.Down||e.Repeat||Friend is not {} f)return false;
        if((e.KeyData&Keys.Modifiers)!=Keys.None)return false;
        var key=e.KeyData&Keys.KeyCode;
        if(ChatOpen)return false;   // 聊天列開著：Y／N 是打字、Esc 先關聊天列
        if(key==Keys.Escape)return f.AnswerDelete(c.Revision,false);
        if(key==Keys.Y)return f.AnswerDelete(c.Revision,true);
        if(key==Keys.N)return f.AnswerDelete(c.Revision,false);
        return false;
    }
}

/// <summary>Surf attribute of these XML files: "4990;1" or "4429". Only the first number is used (second number 待驗證).</summary>
public readonly record struct FriendSurf(int Id,int Count) {
    public static FriendSurf Parse(string raw){
        var parts=raw.Trim().Split(';');
        if(parts.Length>2)throw new FormatException("surf 格式不合法");
        static int P(string s)=>int.TryParse(s.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out int v)?v:throw new FormatException("surf 不是非負整數");
        int id=P(parts[0]),count=parts.Length==2?P(parts[1]):1;
        if(count<1)throw new FormatException("surf 數量需 ≥ 1");
        return new(id,count);
    }
    public static FriendSurf Of(UiNode node,string attr){
        string raw=node.RequiredAttr(attr);
        try{return Parse(raw);}catch(FormatException ex){throw new UiLayoutException(node.Layout.FileName,node.Path,$"屬性 {attr}=\"{raw}\"：{ex.Message}",ex);}
    }
}

/// <summary>Layout read from the XML. Every point/rectangle is relative to the window's top-left.</summary>
public sealed record FriendWindowLayout(
    Point Origin,int Moveable,FriendSurf Base,FriendSurf Frame,
    Point CloseAt,FriendSurf CloseBase,FriendSurf ClosePush,
    Point TitleAt,FriendSurf Title,
    Rectangle List,Size Row,int RowCount,FriendSurf RowPush,Rectangle Info,
    Rectangle Scroll,int ScrollStart,int ScrollHeight,Point ScrollBackAt,FriendSurf ScrollBack,
    Point UpAt,FriendSurf UpBase,FriendSurf UpPush,Point ThumbAt,FriendSurf ThumbBase,FriendSurf ThumbPush,Point DownAt,FriendSurf DownBase,FriendSurf DownPush,
    Rectangle Count) {

    public static FriendWindowLayout Read(UiLayout layout,FriendWindowSpec spec){
        var root=layout.Root;
        if(root.Name!=spec.RootName)throw new UiLayoutException(layout.FileName,root.Path,$"根節點應為 {spec.RootName}");
        var w=root.Child("Window");
        if(w.Attr("Name")!=spec.WindowName)throw new UiLayoutException(layout.FileName,w.Path,$"視窗名稱應為 {spec.WindowName}");
        var close=Named(w,"Button","Closewindow_Button");
        var title=Named(w,"Image",spec.TitleImageName);
        var list=Named(w,"List","Item_List");
        var rows=list.ChildrenNamed("RadioButton").ToList();
        if(rows.Count==0)throw new UiLayoutException(layout.FileName,list.Path,"清單沒有任何 RadioButton");
        var row=rows[0];var rowSize=new Size(row.Int("Width"),row.Int("Height"));
        if(rowSize.Width<=0||rowSize.Height<=0)throw new UiLayoutException(layout.FileName,row.Path,"列尺寸需為正數");
        foreach(var r in rows)if(r.Int("Width")!=rowSize.Width||r.Int("Height")!=rowSize.Height||r.Attr("PushSurf")!=row.Attr("PushSurf"))
            throw new UiLayoutException(layout.FileName,r.Path,"各列尺寸或選取圖不一致");
        var info=Named(row,"Label","Info_Label");
        var listRect=new Rectangle(list.Int("X"),list.Int("Y"),list.Int("Width"),list.Int("Height"));
        if(rows.Count*rowSize.Height>listRect.Height)throw new UiLayoutException(layout.FileName,list.Path,"列高總和超過清單高度");
        var sb=Named(w,"ScrollBar","Bestlist_Scrollbar");
        var back=Named(sb,"Image","Background_Image");var up=Named(sb,"Button","Up_Button");var thumb=Named(sb,"Button","Move_Button");var down=Named(sb,"Button","Down_Button");
        var count=Named(w,"Label","Count_Label");
        return new(new(w.Int("X"),w.Int("Y")),w.TryInt("Moveable")??0,FriendSurf.Of(w,"BaseSurf"),FriendSurf.Of(w,"FrameSurf"),
            At(close),FriendSurf.Of(close,"BaseSurf"),FriendSurf.Of(close,"PushSurf"),
            At(title),FriendSurf.Of(title,"Surf"),
            listRect,rowSize,rows.Count,FriendSurf.Of(row,"PushSurf"),
            new(info.TryInt("X")??0,info.TryInt("Y")??0,info.Int("Width"),info.Int("Height")),
            new(sb.Int("X"),sb.Int("Y"),sb.Int("Width"),sb.Int("Height")),sb.Int("ScrollStart"),sb.Int("ScrollHeight"),At(back),FriendSurf.Of(back,"Surf"),
            At(up),FriendSurf.Of(up,"BaseSurf"),FriendSurf.Of(up,"PushSurf"),At(thumb),FriendSurf.Of(thumb,"BaseSurf"),FriendSurf.Of(thumb,"PushSurf"),
            At(down),FriendSurf.Of(down,"BaseSurf"),FriendSurf.Of(down,"PushSurf"),
            new(count.Int("X"),count.Int("Y"),count.Int("Width"),count.Int("Height")));
    }
    // Children without X/Y sit at the parent's origin (Up_Button, RadioButton, Info_Label omit them in all three files).
    static Point At(UiNode n)=>new(n.TryInt("X")??0,n.TryInt("Y")??0);
    static UiNode Named(UiNode parent,string element,string name){
        var m=parent.ChildrenNamed(element).Where(c=>c.Attr("Name")==name).Take(2).ToList();
        if(m.Count!=1)throw new UiLayoutException(parent.Layout.FileName,$"{parent.Path}/{element}[@Name={name}]",m.Count==0?"缺少節點":"節點不唯一");
        return m[0];
    }
}

/// <summary>FriendUI／ExcludeUI／MailExcludeUI：14 visible rows, vertical scroll bar, count label (windows.md §1.2).</summary>
public sealed class FriendPanel:IUiMovablePanel,IUiClosablePanel {
    enum Press { None, Close, Up, Down, Track }
    readonly Func<IFriendListSource?> source;
    readonly Func<IFriendWindowHost?> host;
    readonly Func<string,IUiPanel?>? find;
    Func<int,UiSurface?> surf=_=>null;
    FriendWindowLayout? layout;
    Point location;Size size;Press pressed;bool visible;
    FriendListData data=FriendListData.Empty;
    bool inWorld,canChat;int confirmRevision;

    /// <param name="host">R9-H：Delete 的送出端（null＝不送）。</param>
    /// <param name="find">同一 UiRoot 的面板查找（雙擊找聊天面板）；null＝雙擊不放密語前綴。</param>
    public FriendPanel(FriendWindowSpec spec,Func<IFriendListSource?> source,Func<IFriendWindowHost?>? host=null,Func<string,IUiPanel?>? find=null){
        Spec=spec;this.source=source;this.host=host??(()=>null);this.find=find;
    }
    public FriendWindowSpec Spec{get;}
    public string Id=>Spec.Id;
    public UiLayer Layer=>UiLayer.Window;
    public Rectangle Bounds=>new(location,size);
    public bool Visible=>visible;
    public FriendWindowLayout Layout=>layout??throw new InvalidOperationException($"{Id} 尚未載入版面");
    /// <summary>First list index shown in row 0.</summary>
    public int Top{get;private set;}
    public string? SelectedName{get;private set;}
    /// <summary>R9-H：等待回答的 1813 Y/N（只有 FriendUI 會有）。</summary>
    public FriendDeleteConfirm? PendingDelete{get;private set;}
    public IReadOnlyList<FriendListRow> Rows=>data.Rows;
    public int MaxTop=>layout==null?0:Math.Max(0,data.Rows.Length-layout.RowCount);
    /// <summary>Text of Count_Label (format 推測: the number of names).</summary>
    public string CountText=>data.Rows.Length.ToString(CultureInfo.InvariantCulture);

    public void Load(UiLayout xml,SurfCatalog surfs)=>Load(xml,surfs.Get);
    /// <summary>Offline checks pass synthetic surfaces; null = surf missing → that part is not drawn.</summary>
    public void Load(UiLayout xml,Func<int,UiSurface?> surfaces){
        var l=FriendWindowLayout.Read(xml,Spec);
        surf=surfaces;layout=l;location=l.Origin;
        size=surf(l.Base.Id) is {} b?new(b.Width,b.Height):Extent(l);
        // Activate="0": the window starts closed (windows.md §1.2).
        visible=false;pressed=Press.None;Top=0;SelectedName=null;PendingDelete=null;
    }
    // Base surf missing: the window still needs a hit rectangle; use the extent of its XML elements (no invented size).
    static Size Extent(FriendWindowLayout l){
        var r=Rectangle.Union(Rectangle.Union(l.List,l.Scroll),l.Count);
        return new(r.Right,r.Bottom);
    }

    public void Show(){if(layout!=null)visible=true;}
    /// <summary>關窗時未回答的 1813 一併取消（推測：原版 Y/N 是否在關窗後仍可回答沒查；取消＝不送，較安全）。</summary>
    public void Hide(){visible=false;pressed=Press.None;PendingDelete=null;}
    public void Toggle(){if(visible)Hide();else Show();}
    /// <summary>R7-H（#810）：堆疊關窗（被擠出、全域 ESC）＝關閉鈕（本機關窗，不送封包）。</summary>
    public void Close()=>Hide();

    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        canChat=inWorld&&UiChatGameState.From(state).View is {CanSend:true};
        var next=state.InWorld&&source() is {} s?s.Read(Spec.Kind):FriendListData.Empty;
        data=next;
        Top=Math.Clamp(Top,0,MaxTop);
        if(SelectedName!=null&&!Has(SelectedName))SelectedName=null;
        // 詢問中的名字已不在名單（伺服器更新、離開世界）→ 詢問作廢。
        if(PendingDelete is {} p&&(!inWorld||!Has(p.Name)))PendingDelete=null;
    }
    bool Has(string name)=>data.Rows.Any(r=>string.Equals(r.Name,name,StringComparison.OrdinalIgnoreCase));
    public void ScrollTo(int top)=>Top=Math.Clamp(top,0,MaxTop);

    // ---- geometry in logical 800×600 coordinates ----
    Point Abs(Point p)=>new(location.X+p.X,location.Y+p.Y);
    Rectangle Abs(Rectangle r)=>new(location.X+r.X,location.Y+r.Y,r.Width,r.Height);
    Size SurfSize(FriendSurf s)=>surf(s.Id) is {} u?new(u.Width,u.Height):Size.Empty;
    public Rectangle RowRect(int row){var l=Layout;return Abs(new Rectangle(l.List.X,l.List.Y+row*l.Row.Height,l.Row.Width,l.Row.Height));}
    public Rectangle InfoRect(int row){var r=RowRect(row);var i=Layout.Info;return new(r.X+i.X,r.Y+i.Y,i.Width,i.Height);}
    public Rectangle CountRect=>Abs(Layout.Count);
    public Rectangle ScrollRect=>Abs(Layout.Scroll);
    /// <summary>Close button: its base surf's size at the XML point (missing surf → not drawn, not clickable).</summary>
    public Rectangle CloseRect=>new(Abs(Layout.CloseAt),SurfSize(Layout.CloseBase));
    // Up/Down buttons span the scroll bar width; heights are the gaps the XML leaves around the track (ScrollStart, Down_Button.Y).
    public Rectangle UpRect{get{var l=Layout;var s=ScrollRect;return new(s.X+l.UpAt.X,s.Y+l.UpAt.Y,s.Width,l.ScrollStart-l.UpAt.Y);}}
    public Rectangle DownRect{get{var l=Layout;var s=ScrollRect;return new(s.X+l.DownAt.X,s.Y+l.DownAt.Y,s.Width,s.Height-l.DownAt.Y);}}
    public Rectangle TrackRect{get{var l=Layout;var s=ScrollRect;return new(s.X,s.Y+l.ScrollStart,s.Width,l.ScrollHeight);}}
    int ThumbHeight=>Math.Min(SurfSize(Layout.ThumbBase).Height,Layout.ScrollHeight);
    /// <summary>Thumb top relative to the scroll bar: ScrollStart at the top, ScrollStart+ScrollHeight-thumb at the bottom.</summary>
    public int ThumbY=>Layout.ScrollStart+(MaxTop==0?0:(Layout.ScrollHeight-ThumbHeight)*Top/MaxTop);

    public void Paint(UiCanvas canvas){
        if(!visible||layout is not {} l)return;
        canvas.DrawSurface(surf(l.Base.Id),location.X,location.Y);
        canvas.DrawSurface(surf(l.Frame.Id),location.X,location.Y);
        var t=Abs(l.TitleAt);canvas.DrawSurface(surf(l.Title.Id),t.X,t.Y);
        var c=Abs(l.CloseAt);canvas.DrawSurface(surf((pressed==Press.Close?l.ClosePush:l.CloseBase).Id),c.X,c.Y);
        for(int i=0;i<l.RowCount;i++){
            int index=Top+i;if(index>=data.Rows.Length)break;
            var row=data.Rows[index];var r=RowRect(i);
            if(string.Equals(row.Name,SelectedName,StringComparison.OrdinalIgnoreCase))canvas.DrawSurface(surf(l.RowPush.Id),r.X,r.Y);
            canvas.DrawText(row.Name,r.X+2,r.Y+2,Color.White);
        }
        var s=ScrollRect;
        canvas.DrawSurface(surf(l.ScrollBack.Id),s.X+l.ScrollBackAt.X,s.Y+l.ScrollBackAt.Y);
        canvas.DrawSurface(surf((pressed==Press.Up?l.UpPush:l.UpBase).Id),s.X+l.UpAt.X,s.Y+l.UpAt.Y);
        canvas.DrawSurface(surf((pressed==Press.Track?l.ThumbPush:l.ThumbBase).Id),s.X+l.ThumbAt.X,s.Y+ThumbY);
        canvas.DrawSurface(surf((pressed==Press.Down?l.DownPush:l.DownBase).Id),s.X+l.DownAt.X,s.Y+l.DownAt.Y);
        var n=CountRect;canvas.DrawText(CountText,n.X+2,n.Y+3,Color.White);
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!visible||layout==null)return false;
        switch(e.Kind){
            case UiMouseKind.Down:
                if(!Bounds.Contains(e.Location))return false;
                if(e.Button!=MouseButtons.Left)return true;
                if(CloseRect.Contains(e.Location))pressed=Press.Close;
                else if(UpRect.Contains(e.Location)){pressed=Press.Up;ScrollTo(Top-1);}
                else if(DownRect.Contains(e.Location)){pressed=Press.Down;ScrollTo(Top+1);}
                else if(TrackRect.Contains(e.Location)){pressed=Press.Track;Track(e.Y);}
                else{
                    var before=SelectedName;
                    // 515（雙擊，sub_B19030）：同一列第二下 → 聊天列放 `"名稱 `（只有 FriendUI）。
                    if(Select(e.Location) is {} name&&e.Clicks>=2&&Spec.Kind==FriendListKind.Friend&&string.Equals(before,name,StringComparison.OrdinalIgnoreCase))
                        Whisper(name,commands);
                }
                return true;
            case UiMouseKind.Move:
                if(pressed==Press.Track)Track(e.Y);
                return pressed!=Press.None||Bounds.Contains(e.Location);
            case UiMouseKind.Up:{
                var was=pressed;pressed=Press.None;
                if(was==Press.Close&&CloseRect.Contains(e.Location))Hide();
                return was!=Press.None||Bounds.Contains(e.Location);
            }
            case UiMouseKind.Wheel:
                if(!Bounds.Contains(e.Location))return false;
                int steps=e.Delta/120;if(steps==0&&e.Delta!=0)steps=Math.Sign(e.Delta);
                ScrollTo(Top-steps);return true;
        }
        return false;
    }
    void Track(int y){
        if(MaxTop==0)return;
        var t=TrackRect;
        Top=Math.Clamp((int)Math.Round((double)(y-t.Y)*MaxTop/Math.Max(1,t.Height-1)),0,MaxTop);
    }
    /// <summary>點到有名字的列 → 選取並回傳該名字；空列或列外 → null（選取不變）。</summary>
    string? Select(Point p){
        var l=Layout;
        for(int i=0;i<l.RowCount;i++){
            if(!RowRect(i).Contains(p))continue;
            int index=Top+i;if(index>=data.Rows.Length)return null;
            return SelectedName=data.Rows[index].Name;
        }
        return null;
    }

    /// <summary>
    /// 515：把 `"名稱 ` 放進原生聊天輸入列（取代原本的草稿；同 MapSendEntry 的作法），不送封包。之後打字、Enter 走既有
    /// ChatPanel → SendChat → 密語。聊天列不能開（不在世界、不能發話、送出中）→ false。
    /// </summary>
    public bool Whisper(string name,IUiCommands commands){
        if(!canChat||find?.Invoke(ChatPanel.WindowName) is not ChatPanel chat||chat.Input.Pending||UiChatGameState.Commands(commands) is not {} c)return false;
        if(!chat.Input.Open){
            if(!c.BeginChatInput())return false;
            chat.Input.Begin(false);
        }
        chat.Input.Clear();
        string prefix=FriendPanels.WhisperPrefix(name);
        foreach(char ch in prefix)chat.Input.Type(ch);
        return chat.Input.Text==prefix;
    }

    /// <summary>
    /// 4142（Delete）：FriendUI → 開 1813 Y/N；ExcludeUI → 直接送 C171；MailExcludeUI → 不送（3.81 不支援）。
    /// 沒有選取、沒有宿主、不在世界 → false（鍵交給其他人）。
    /// </summary>
    public bool Delete(){
        if(!visible||!inWorld||SelectedName is not {} name||!Has(name)||host() is not {} h)return false;
        switch(Spec.Kind){
            case FriendListKind.Friend:
                PendingDelete=new(++confirmRevision,name,FriendPanels.DeleteConfirmText);return true;
            case FriendListKind.Exclude:
                h.Remove(FriendListKind.Exclude,name);return true;
            default:return false;
        }
    }
    /// <summary>回答 1813：yes → C202；no → 什麼都不送。過期的 revision 不算（true＝這次回答被接受）。</summary>
    public bool AnswerDelete(int revision,bool yes){
        if(PendingDelete is not {} p||p.Revision!=revision)return false;
        PendingDelete=null;
        if(yes&&inWorld&&Has(p.Name))host()?.Remove(FriendListKind.Friend,p.Name);
        return true;
    }

    // KeyboardEvent：Delete（4142）與 Esc（27）。其他鍵交給遊戲。聊天輸入列開著時 Delete 不攔（避免打字時刪人）。
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!visible||layout==null||!e.Down||e.Repeat||(e.KeyData&Keys.Modifiers)!=Keys.None)return false;
        switch(e.KeyData&Keys.KeyCode){
            case Keys.Escape:Hide();return true;
            case Keys.Delete:
                if(find?.Invoke(ChatPanel.WindowName) is ChatPanel{Input.Open:true})return false;
                return Delete();
        }
        return false;
    }

    public bool IsDragHandle(Point p)=>visible&&layout!=null&&Bounds.Contains(p)&&p.Y-location.Y<Layout.Moveable&&!CloseRect.Contains(p);
    public void MoveTo(Point p)=>location=p;
}
