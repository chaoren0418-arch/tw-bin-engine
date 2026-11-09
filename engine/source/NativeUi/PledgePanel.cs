using System.Text.Json;
using P06.NativeUi;

// W2-H（T239／#561）：血盟成員窗。依據 docs/reference/windows-2.md 第 3、9 節（T226／#533）。
// 協定（3.81 為準，server-protocol.md S_Pledge／S_PacketBox）：
//   S250 子碼 171：C(250) C(171) H(人數)，逐人 S(姓名) C(0)。TW16 sub_B2E3D0 收完後 vtable +68(1) 開窗 → 本模組以它為開窗點。
//     既有 WorldSession 主分派已把 171 交給 Clan.cs（ReceiveClanOnlineMembers），所以這裡只用「觀察」掛接，不搶封包。
//   S250 子碼 169：C(250) C(169) S(姓名) 62 byte Big5 備註（零填滿）。3.81 C_PledgeContent 寫備註後回送；TW16 sub_B2EE50。
//     主分派沒有任何 case 接 169，所以用消化型掛接。
//   167／170：3.81 把子碼直接寫成第一個 byte（不是正常 S250 包），不處理（windows-2.md 第 3 節）。
// 版面：本機 PledgeUI.xml（執行期從 Tile.pak 讀取，不進 repo）的 Nonoticeuserinfo_Window／Noticeuserinfo_Window。
//   原版 TW16 讀的是 RePledgeUI.xml（本機缺），3.81 世代資源只有 PledgeUI.xml，所以本窗以它為準（推定）。
// 只做協定、資料與已知座標；標「推測」的常數集中在 PledgePanel 頂端，查證後只改那裡。

namespace P06 {

/// <summary>S250/169 的一筆備註。</summary>
public sealed record PledgeMemo(string Name,string Memo);

/// <summary>
/// 血盟成員窗資料（唯讀快照）。Online=null 表示本次世界還沒收到 171（窗不開）；空陣列表示收到但沒人在線上。
/// OpenRevision 每收到一次 171 就加一，面板以它判斷「要開窗」。
/// </summary>
public sealed record PledgeWindowState(long OpenRevision,long Revision,string Clan,IReadOnlyList<string>? Online,IReadOnlyDictionary<string,string> Memos) {
    public static readonly PledgeWindowState Empty=new(0,0,"",null,new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase));
    /// <summary>該成員的備註；伺服器沒送過就是空字串（不推算）。</summary>
    public string Memo(string name)=>Memos.TryGetValue(name,out var m)?m:"";
}

public static class PledgeProtocol {
    public const byte PacketBox=250,WriteNotes=169,OnlineMembers=171;
    /// <summary>3.81 S_Pledge(String,String)：備註固定 62 byte，Big5，零填滿。</summary>
    public const int NotesLength=62;

    public static PledgeMemo DecodeMemo(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=PacketBox||r.C()!=WriteNotes)throw new ProtocolException("pledge memo subcode");
        string name=r.S();
        if(string.IsNullOrWhiteSpace(name))throw new ProtocolException("pledge memo name");
        var bytes=new byte[NotesLength];for(int i=0;i<NotesLength;i++)bytes[i]=r.C();
        r.Finish();
        int end=Array.IndexOf(bytes,(byte)0);
        return new(name,LoginPackets.TextEncoding.GetString(bytes,0,end<0?NotesLength:end));
    }
    /// <summary>3.81 S_Pledge(name, notes) 的位元組（測試用，照伺服器原始碼欄位順序合成）。</summary>
    public static byte[] EncodeMemo(string name,string notes){
        var text=LoginPackets.TextEncoding.GetBytes(notes);
        if(text.Length>NotesLength)throw new ArgumentException("備註超過 62 byte。");
        var fixedText=new byte[NotesLength];text.CopyTo(fixedText,0);
        return LoginPackets.Pad([PacketBox,WriteNotes,..LoginPackets.TextEncoding.GetBytes(name),0,..fixedText]);
    }
    /// <summary>3.81 S_PacketBox(171, L1PcInstance[]) 的位元組（測試用）。</summary>
    public static byte[] EncodeOnline(params string[] names){
        var body=new List<byte>{PacketBox,OnlineMembers,(byte)(names.Length&0xff),(byte)(names.Length>>8)};
        foreach(var n in names){body.AddRange(LoginPackets.TextEncoding.GetBytes(n));body.Add(0);body.Add(0);}
        return LoginPackets.Pad(body.ToArray());
    }
}

public sealed partial class WorldSession {
    long pledgeOpenRevision,pledgeRevision;int pledgeEpoch=-1;
    string[]? pledgeOnline;string pledgeClan="";
    readonly Dictionary<string,string> pledgeMemos=new(StringComparer.OrdinalIgnoreCase);
    PledgeWindowState? pledgeView;

    void EnsurePledgeEpoch(){
        if(pledgeEpoch==WorldEpoch)return;
        pledgeEpoch=WorldEpoch;ClearPledgeWindow();
    }
    void ClearPledgeWindow(){
        if(pledgeOnline==null&&pledgeMemos.Count==0&&pledgeClan.Length==0)return;
        pledgeOnline=null;pledgeClan="";pledgeMemos.Clear();pledgeRevision++;pledgeView=null;
    }

    /// <summary>171 是開窗點（TW16 sub_B2E3D0 → vtable +68(1)）。壞包交給主分派（Clan.cs）照原樣報錯，這裡不吞也不開窗。</summary>
    [WorldReceive(PledgeProtocol.PacketBox,Subtype=PledgeProtocol.OnlineMembers,Observe=true)]
    void ObservePledgeOnline(byte[] plain){
        string[] names;
        try{names=ClanProtocol.DecodeOnlineMembers(plain);}catch(ProtocolException){return;}
        EnsurePledgeEpoch();
        pledgeOnline=names;pledgeClan=Actor?.Clan??"";
        pledgeOpenRevision++;pledgeRevision++;pledgeView=null;
    }

    /// <summary>169：更新該成員的備註；不開窗。名稱不在目前在線名單時仍保留，之後出現時顯示。</summary>
    [WorldReceive(PledgeProtocol.PacketBox,Subtype=PledgeProtocol.WriteNotes)]
    bool ReceivePledgeMemo(byte[] plain){
        var memo=PledgeProtocol.DecodeMemo(plain);
        EnsurePledgeEpoch();
        pledgeMemos[memo.Name]=memo.Memo;pledgeRevision++;pledgeView=null;
        return true;
    }

    [WorldReset]
    void ResetPledgeWindow(){ClearPledgeWindow();pledgeEpoch=-1;}

    /// <summary>目前的血盟成員窗資料。換世界或自己的血盟名稱改變（離盟、改盟）時清空。</summary>
    public PledgeWindowState PledgeWindow{get{
        EnsurePledgeEpoch();
        if(pledgeOnline!=null&&pledgeClan!=(Actor?.Clan??""))ClearPledgeWindow();
        return pledgeView??=new(pledgeOpenRevision,pledgeRevision,pledgeClan,pledgeOnline?.ToArray(),
            new Dictionary<string,string>(pledgeMemos,StringComparer.OrdinalIgnoreCase));
    }}

    [WorldSnapshot("pledgeWindow")]
    object? PledgeWindowSnapshot(){
        var p=PledgeWindow;
        return new{openRevision=p.OpenRevision,revision=p.Revision,clan=p.Clan,online=p.Online,
            memos=p.Memos.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value)};
    }
}

}

namespace P06.NativeUi {

/// <summary>帶有血盟成員窗資料的遊戲狀態（產品端由 client bridge 包裝；離線檢查自己提供）。</summary>
public interface IUiPledgeState { PledgeWindowState? Pledge { get; } }

/// <summary>Decorator：共用 IUiGameState 加上血盟成員窗快照。所有 IUiGameState 成員原樣轉給 Base。</summary>
public sealed class UiPledgeGameState(IUiGameState inner,PledgeWindowState? pledge):IUiGameState,IUiPledgeState {
    public IUiGameState Base=>inner;
    public PledgeWindowState? Pledge=>pledge;
    public bool InWorld=>inner.InWorld;
    public string? CharacterName=>inner.CharacterName;
    public int? Level=>inner.Level;
    public long? Hp=>inner.Hp;
    public long? MaxHp=>inner.MaxHp;
    public long? Mp=>inner.Mp;
    public long? MaxMp=>inner.MaxMp;
    public long Now=>inner.Now;

    /// <summary>穿過已知的狀態包裝（HpMp／巨集／本包裝）找血盟資料；找不到回 null（窗不開）。</summary>
    public static PledgeWindowState? Of(IUiGameState? state){
        for(int depth=0;state!=null&&depth<8;depth++){
            if(state is IUiPledgeState p)return p.Pledge;
            state=state switch{UiHpMpGameState h=>h.Base,UiMacroGameState m=>m.Inner,_=>null};
        }
        return null;
    }
}

/// <summary>
/// 血盟成員窗（PledgeUI.xml）。171 開窗並列出上線成員；169 更新備註欄。
/// 只畫伺服器有送的資料：姓名（171）、備註（169）、自己的盟名（S72／角色資料）。
/// 職業圖、階級、公告、創立日、聯盟欄 3.81 都送不到（167／170 格式壞掉），一律空白，不推算。
/// 排序、改階級彈出窗、寫備註、寫公告會送封包或語義待驗證，本任務不做（只畫按鈕底圖）。
/// </summary>
public sealed class PledgePanel:IUiMovablePanel {
    public const string PanelId="PledgeUI",LayoutName="PledgeUI";
    public const string PlainWindowName="Nonoticeuserinfo_Window",NoticeWindowName="Noticeuserinfo_Window";
    // ---- 推測（查證後只改這裡）----
    /// <summary>文字高度：UiText 暫定 新細明體 9pt ≈ 12 px（V4 (f) 待驗證）；用於 VAlign="middle" 置中。</summary>
    public const int TextHeight=12;
    /// <summary>字色：XML 沒寫 FontColor 的 Label 用白色（推測）。</summary>
    public static readonly Color TextColor=Color.FromArgb(255,255,255,255);
    /// <summary>KeyboardEvent="CloseUI" 的實體按鍵：Esc（推測）。</summary>
    public const Keys CloseKey=Keys.Escape;
    // ----

    /// <summary>XML 中一個有圖的元件：視窗內相對座標＋第一張 surf（V4 前只用第 0 張）。</summary>
    public sealed record Placed(Point At,UiSurface? Surface) {
        public Rectangle Rect=>Surface==null?Rectangle.Empty:new(At,new(Surface.Width,Surface.Height));
    }
    public sealed record Column(string Name,int X,int Width);

    /// <summary>一個 Window 元素的版面（座標全部相對於視窗左上）。</summary>
    public sealed class WindowLayout {
        public required string Name{get;init;}
        public UiSurface? Base{get;init;}
        public UiSurface? Frame{get;init;}
        /// <summary>Moveable="20"：視窗頂端這麼高的區域可拖曳（推測：數值是拖曳帶高度）。</summary>
        public int DragHeight{get;init;}
        public required Placed Close{get;init;}
        public Rectangle PledgeLabel{get;init;}
        public required Placed NoticeButton{get;init;}
        public required Placed NoticeArrow{get;init;}
        public required IReadOnlyList<Placed> SortButtons{get;init;}
        public Point ListAt{get;init;}
        public int Rows{get;init;}
        public int RowPitch{get;init;}
        public int RowHeight{get;init;}
        public required IReadOnlyList<Column> Columns{get;init;}
        public Point ScrollAt{get;init;}
        public required Placed ScrollBackground{get;init;}
        public required Placed ScrollUp{get;init;}
        public required Placed ScrollThumb{get;init;}
        public required Placed ScrollDown{get;init;}
        public int ScrollStart{get;init;}
        public int ScrollHeight{get;init;}
        public Size Size=>Base==null?Size.Empty:new(Base.Width,Base.Height);
        public Column Col(string name)=>Columns.First(c=>c.Name==name);
    }

    WindowLayout? plain,notice;
    bool open,showNotice,inWorld;
    long seenOpen;
    Point origin;
    int scroll;
    PledgeWindowState? data;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public WindowLayout? Current=>showNotice?notice:plain;
    public Rectangle Bounds=>Current is {Base:not null} w?new(origin,w.Size):Rectangle.Empty;
    public bool Visible=>open&&inWorld&&data?.Online!=null&&Current is {Base:not null};
    public bool IsOpen=>open;
    public bool ShowingNotice=>showNotice;
    public int Scroll=>scroll;
    public Point Origin=>origin;
    public IReadOnlyList<string> Members=>data?.Online??[];
    public int MaxScroll=>Current is {} w?Math.Max(0,Members.Count-w.Rows):0;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    /// <summary>離線檢查用：surf 由呼叫端提供（合成圖）。</summary>
    public void Load(UiLayout layout,Func<int,UiSurface?> surf){
        if(layout.Root.Name!=LayoutName)throw new UiLayoutException(layout.FileName,layout.Root.Path,$"根元素應為 {LayoutName}");
        plain=LoadWindow(layout.Root,PlainWindowName,surf);
        notice=LoadWindow(layout.Root,NoticeWindowName,surf);
        // XML 的視窗座標是 (0,0)；原版開窗位置（置中或記憶位置）待驗證，先用 XML 值。
        origin=new(Named(layout.Root,"Window",PlainWindowName).TryInt("X")??0,Named(layout.Root,"Window",PlainWindowName).TryInt("Y")??0);
    }

    static WindowLayout LoadWindow(UiNode root,string name,Func<int,UiSurface?> get){
        var w=Named(root,"Window",name);
        var nb=Named(w,"Button","Pledgenotice_Button");var noticeButton=Place(nb,Point.Empty,"BaseSurf",get);
        var ctl=Named(w,"Control","Pledgename_Control");var lbl=Named(ctl,"Label","Pledge_Label");
        var sortList=Named(w,"List","Sort_List");var sortAt=XY(sortList);var sorts=new List<Placed>();int sx=0;
        foreach(var b in sortList.ChildrenNamed("Button")){
            var p=Place(b,new(sortAt.X+sx,sortAt.Y),"BaseSurf",get);sorts.Add(p);
            sx+=b.TryInt("Width")??p.Surface?.Width??throw new UiLayoutException(b.Layout.FileName,b.Path,"排序鈕沒有寬度也沒有 surf");
        }
        var list=Named(w,"List","Userinfo_List");
        var rows=list.ChildrenNamed("List").ToArray();
        if(rows.Length==0)throw new UiLayoutException(list.Layout.FileName,list.Path,"Userinfo_List 沒有列");
        var row=rows[0];int gap=row.TryInt("Gap")??0,cx=0;var cols=new List<Column>();
        foreach(var c in row.Children){string n=c.RequiredAttr("Name");int width=c.Int("Width");cols.Add(new(n,cx,width));cx+=width+gap;}
        foreach(var need in new[]{"Class_Image","Name_Label","Rank_Label","Memo_Label"})
            if(cols.All(c=>c.Name!=need))throw new UiLayoutException(row.Layout.FileName,row.Path,$"缺少欄位 {need}");
        var sb=Named(w,"ScrollBar","Userinfo_Scrollbar");var sbAt=XY(sb);
        return new WindowLayout{
            Name=name,
            Base=get(SurfOf(w,"BaseSurf")),
            Frame=w.Attr("FrameSurf") is null?null:get(SurfOf(w,"FrameSurf")),
            DragHeight=w.TryInt("Moveable")??0,
            Close=Place(Named(w,"Button","Closeui_Button"),Point.Empty,"BaseSurf",get),
            PledgeLabel=new(XY(ctl).X+XY(lbl).X,XY(ctl).Y+XY(lbl).Y,lbl.Int("Width"),lbl.Int("Height")),
            NoticeButton=noticeButton,
            // 子按鈕的 X/Y 相對於父按鈕（Noticetri_Button X=200 落在 4455 條的右端，推測）。
            NoticeArrow=Place(Named(nb,"Button","Noticetri_Button"),noticeButton.At,"BaseSurf",get),
            SortButtons=sorts,
            ListAt=XY(list),Rows=rows.Length,
            // 垂直清單沒寫 Gap：列距＝列高＋清單 Gap（預設 0，推測）。
            RowHeight=row.Int("Height"),RowPitch=row.Int("Height")+(list.TryInt("Gap")??0),
            Columns=cols,
            ScrollAt=sbAt,
            ScrollBackground=Place(Named(sb,"Image","Background_Image"),sbAt,"Surf",get),
            ScrollUp=Place(Named(sb,"Button","Up_Button"),sbAt,"BaseSurf",get),
            ScrollThumb=Place(Named(sb,"Button","Move_Button"),sbAt,"BaseSurf",get),
            ScrollDown=Place(Named(sb,"Button","Down_Button"),sbAt,"BaseSurf",get),
            ScrollStart=sb.Int("ScrollStart"),ScrollHeight=sb.Int("ScrollHeight"),
        };
    }

    /// <summary>直接子元素中 element 名稱＝elem 且 Name 屬性＝name 的唯一一個；缺或重複就丟 UiLayoutException。</summary>
    static UiNode Named(UiNode parent,string elem,string name){
        var m=parent.ChildrenNamed(elem).Where(c=>c.Attr("Name")==name).Take(2).ToArray();
        if(m.Length!=1)throw new UiLayoutException(parent.Layout.FileName,parent.Path+"/"+elem+"[@Name="+name+"]",m.Length==0?"缺少節點":"節點不唯一");
        return m[0];
    }
    /// <summary>PledgeUI.xml 的 X／Y；清單內元件沒寫時視為 0（相對父元件，推測）。</summary>
    static Point XY(UiNode n)=>new(n.TryInt("X")??0,n.TryInt("Y")??0);
    /// <summary>"4450;1" → 4450（分號後的數字意義待驗證，只取第一個）；"5726~5728" 也只取第一張。</summary>
    public static int SurfOf(UiNode n,string attr){
        string raw=n.RequiredAttr(attr);string first=raw.Split(';')[0];
        int[] s;
        try{s=UiNode.ParseSurfRange(first);}catch(FormatException ex){throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr}=\"{raw}\"："+ex.Message,ex);}
        return s.Length>0?s[0]:throw new UiLayoutException(n.Layout.FileName,n.Path,$"屬性 {attr} 是空的");
    }
    static Placed Place(UiNode n,Point parent,string attr,Func<int,UiSurface?> get){
        var at=XY(n);return new(new(parent.X+at.X,parent.Y+at.Y),get(SurfOf(n,attr)));
    }

    public void Update(IUiGameState state){
        inWorld=state.InWorld;
        data=inWorld?UiPledgeGameState.Of(state):null;
        if(data?.Online==null){open=false;scroll=0;return;}
        if(data.OpenRevision!=seenOpen){
            seenOpen=data.OpenRevision;
            // 每次 171 都開窗並回到清單頂端（TW16 sub_B2E3D0 先清清單再逐筆加入，推測捲動位置也歸零）。
            open=true;scroll=0;
        }
        scroll=Math.Clamp(scroll,0,MaxScroll);
    }

    /// <summary>邏輯座標（視窗左上＋相對座標）。</summary>
    Rectangle Abs(Rectangle r)=>r.IsEmpty?r:new(origin.X+r.X,origin.Y+r.Y,r.Width,r.Height);
    public Rectangle CloseRect=>Current is {} w?Abs(w.Close.Rect):Rectangle.Empty;
    public Rectangle NoticeRect=>Current is {} w?Abs(w.NoticeButton.Rect):Rectangle.Empty;
    public Rectangle NoticeArrowRect=>Current is {} w?Abs(w.NoticeArrow.Rect):Rectangle.Empty;
    public Rectangle ScrollUpRect=>Current is {} w?Abs(w.ScrollUp.Rect):Rectangle.Empty;
    public Rectangle ScrollDownRect=>Current is {} w?Abs(w.ScrollDown.Rect):Rectangle.Empty;
    /// <summary>第 i 個可見列的某一欄（邏輯座標）。</summary>
    public Rectangle Cell(int row,string column){
        if(Current is not {} w)return Rectangle.Empty;var c=w.Col(column);
        return new(origin.X+w.ListAt.X+c.X,origin.Y+w.ListAt.Y+row*w.RowPitch,c.Width,w.RowHeight);
    }
    /// <summary>捲軸拉桿的 y（相對視窗）：ScrollStart 起，在 ScrollHeight 內依捲動比例移動（推測）。</summary>
    public int ThumbY{get{
        if(Current is not {} w)return 0;
        int h=w.ScrollThumb.Surface?.Height??0,max=MaxScroll;
        int travel=Math.Max(0,w.ScrollHeight-h);
        return w.ScrollAt.Y+w.ScrollStart+(max==0?0:travel*scroll/max);
    }}

    public void Paint(UiCanvas canvas){
        if(Current is not {} w||data?.Online is not {} members)return;
        int ox=origin.X,oy=origin.Y;
        canvas.DrawSurface(w.Base,ox,oy);
        canvas.DrawSurface(w.Frame,ox,oy);
        Draw(canvas,w.Close);Draw(canvas,w.NoticeButton);Draw(canvas,w.NoticeArrow);
        foreach(var s in w.SortButtons)Draw(canvas,s);
        var label=Abs(w.PledgeLabel);
        canvas.DrawText(data.Clan,label.X,Middle(label),TextColor);
        for(int i=0;i<w.Rows;i++){
            int k=scroll+i;if(k>=members.Count)break;
            string name=members[k];
            var nc=Cell(i,"Name_Label");
            int tw=UiText.Measure(canvas.Graphics,name).Width;
            canvas.DrawText(name,nc.X+Math.Max(0,(nc.Width-tw)/2),Middle(nc),TextColor);
            var mc=Cell(i,"Memo_Label");
            canvas.DrawText(data.Memo(name),mc.X,Middle(mc),TextColor);
        }
        Draw(canvas,w.ScrollBackground);Draw(canvas,w.ScrollUp);Draw(canvas,w.ScrollDown);
        canvas.DrawSurface(w.ScrollThumb.Surface,ox+w.ScrollThumb.At.X,oy+ThumbY);
    }
    void Draw(UiCanvas canvas,Placed p)=>canvas.DrawSurface(p.Surface,origin.X+p.At.X,origin.Y+p.At.Y);
    static int Middle(Rectangle r)=>r.Y+(r.Height-TextHeight)/2;

    public void Close(){open=false;showNotice=false;}

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(!Visible)return false;
        bool inside=Bounds.Contains(e.Location);
        if(e.Kind==UiMouseKind.Wheel){if(!inside)return false;ScrollBy(e.Delta>0?-1:e.Delta<0?1:0);return true;}
        if(e.Kind!=UiMouseKind.Down||e.Button!=MouseButtons.Left)return inside;
        if(Hit(CloseRect,e.Location)){Close();return true;}
        if(Hit(NoticeRect,e.Location)||Hit(NoticeArrowRect,e.Location)){
            // OpenNotice／CloseNotice：兩個 Window 互換（XML 事件名）；公告內容 3.81 送不到（167），公告區留白。
            var at=origin;showNotice=!showNotice;origin=UiInput.ClampInside(at,Bounds.Size);scroll=Math.Clamp(scroll,0,MaxScroll);return true;
        }
        if(Hit(ScrollUpRect,e.Location)){ScrollBy(-1);return true;}
        if(Hit(ScrollDownRect,e.Location)){ScrollBy(1);return true;}
        return inside;   // 視窗 MouseEvent="Dummy"：窗內點擊不傳給世界
    }
    static bool Hit(Rectangle r,Point p)=>!r.IsEmpty&&r.Contains(p);
    public void ScrollBy(int rows)=>scroll=Math.Clamp(scroll+rows,0,MaxScroll);

    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(!Visible||e.KeyCode!=CloseKey||e.Modifiers!=Keys.None)return false;
        if(e.Down&&!e.Repeat)Close();
        return true;
    }

    public bool IsDragHandle(Point location)=>Visible&&Current is {} w&&Bounds.Contains(location)&&location.Y-origin.Y<w.DragHeight&&!Hit(CloseRect,location);
    public void MoveTo(Point location)=>origin=location;
}

/// <summary>W2-H 離線測試：合成封包（照 3.81 S_Pledge／S_PacketBox 欄位）與合成 XML／surf，不讀客戶端、不連線。</summary>
static class PledgePanelTests {
    const string Xml="""
<?xml version="1.0"?>
<PledgeUI Toggle="true">
  <Window Name = "ChangeRank_PopupWindow" X = "0" Y = "0" Width="100" Height="73"/>
  <Window Name = "Noticeuserinfo_Window" X = "0" Y = "0" BaseSurf = "4512;1" FrameSurf = "4487;1" Moveable = "20">
    <Button Name = "Closeui_Button" X = "260" Y = "13" BaseSurf = "4429" PushSurf = "4430"/>
    <Control Name = "Pledgename_Control" X = "19" Y = "25" Width = "189" Height = "20">
      <Label Name = "Pledge_Label" X = "17" Y="-2" Width = "169" Height = "20"/>
    </Control>
    <Button Name = "Pledgenotice_Button" X = "16" Y = "41" BaseSurf = "4455" PushSurf = "4455">
      <Button Name = "Noticetri_Button" X = "200" Y = "0" BaseSurf = "4456"/>
    </Button>
    <List Name = "Sort_List" X = "16" Y = "181" Width = "208" Height = "21" Horizontal = "1">
      <Button Name = "Classsort_Button" BaseSurf = "4458"/>
      <Button Name = "Namesort_Button" BaseSurf = "4460"/>
    </List>
    <List Name = "Userinfo_List" X = "21" Y = "202" Width = "214" Height = "222" Vertical = "1">
      <List Name = "Userinfo0_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1">
        <Image Name = "Class_Image" Surf = "4546;4" Width = "19"/>
        <Label Name = "Name_Label" Width = "74" Height = "15"/>
        <Label Name = "Rank_Label" Width = "35" Height = "15"/>
        <Label Name = "Memo_Label" Width = "97" Height = "15"/>
      </List>
      <List Name = "Userinfo1_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1"/>
    </List>
    <ScrollBar Name = "Userinfo_Scrollbar" X = "255" Y = "67" Width = "12" Height = "293" ScrollStart = "14" ScrollHeight = "262">
      <Image Name = "Background_Image" Y = "14" Surf = "4443;1"/>
      <Button Name = "Up_Button" BaseSurf = "4446;1"/>
      <Button Name = "Move_Button" Y = "14" BaseSurf = "4448;1"/>
      <Button Name = "Down_Button" Y = "277" BaseSurf = "4444;1"/>
    </ScrollBar>
  </Window>
  <Window Name = "Nonoticeuserinfo_Window" X = "0" Y = "0" BaseSurf = "4450;1" FrameSurf = "4451;1" Moveable = "20">
    <Button Name = "Closeui_Button" X = "260" Y = "13" BaseSurf = "4429" PushSurf = "4430"/>
    <Control Name = "Pledgename_Control" X = "19" Y = "25" Width = "189" Height = "20">
      <Label Name = "Pledge_Label" X = "17" Y = "-2" Width = "169" Height = "20"/>
    </Control>
    <Button Name = "Pledgenotice_Button" X = "16" Y = "41" BaseSurf = "4455" PushSurf = "4455">
      <Button Name = "Noticetri_Button" X = "200" Y = "0" BaseSurf = "4494"/>
    </Button>
    <List Name = "Sort_List" X = "16" Y = "61" Width = "208" Height = "21" Horizontal = "1">
      <Button Name = "Classsort_Button" BaseSurf = "4458"/>
      <Button Name = "Namesort_Button" BaseSurf = "4460"/>
    </List>
    <List Name = "Userinfo_List" X = "21" Y = "82" Width = "204" Height = "339" Vertical = "1">
      <List Name = "Userinfo0_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1">
        <Image Name = "Class_Image" Surf = "4546;4" Width = "19"/>
        <Label Name = "Name_Label" Width = "74" Height = "15"/>
        <Label Name = "Rank_Label" Width = "35" Height = "15"/>
        <Label Name = "Memo_Label" Width = "97" Height = "15"/>
      </List>
      <List Name = "Userinfo1_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1"/>
      <List Name = "Userinfo2_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1"/>
    </List>
    <ScrollBar Name = "Userinfo_Scrollbar" X = "255" Y = "67" Width = "12" Height = "293" ScrollStart = "14" ScrollHeight = "262">
      <Image Name = "Background_Image" Y = "14" Surf = "4443;1"/>
      <Button Name = "Up_Button" BaseSurf = "4446;1"/>
      <Button Name = "Move_Button" Y = "14" BaseSurf = "4448;1"/>
      <Button Name = "Down_Button" Y = "277" BaseSurf = "4444;1"/>
    </ScrollBar>
  </Window>
</PledgeUI>
""";

    static UiSurface Img(int surf,int w,int h)=>new(surf,surf+".img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF204060u,w*h).ToArray()));
    static readonly Dictionary<int,(int w,int h)> Sizes=new(){
        [4450]=(277,423),[4451]=(277,423),[4512]=(277,423),[4487]=(277,423),[4429]=(12,12),[4455]=(200,16),[4456]=(12,16),[4494]=(12,16),
        [4458]=(20,21),[4460]=(76,21),[4443]=(12,263),[4446]=(12,14),[4448]=(12,20),[4444]=(12,14),
    };
    static UiSurface? Get(int surf)=>Sizes.TryGetValue(surf,out var s)?Img(surf,s.w,s.h):null;
    static JsonElement Snap(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("pledgeWindow");
    static int Unhandled(WorldSession s,string key)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("unhandledSubtypes").TryGetProperty(key,out var v)?v.GetInt32():0;
    static UiMouseEvent Click(Point p)=>new(UiMouseKind.Down,p,MouseButtons.Left,1,0,Keys.None);
    static IUiGameState World(PledgeWindowState? p,bool inWorld=true)=>new UiPledgeGameState(new UiStaticGameState(inWorld),p);

    [WorldModuleTest("pledge-panel")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：169 消化、171 只觀察（主分派的 Clan.cs 照常收）、快照與重置；登錄表無錯誤。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:169,Method:"WorldSession.ReceivePledgeMemo"}),"S250/169 registered");
        t.Check(entries.Any(e=>e is {Kind:"observe",Opcode:250,Subtype:171,Method:"WorldSession.ObservePledgeOnline"}),"S250/171 observed");
        t.Check(!entries.Any(e=>e is {Kind:"receive",Opcode:250,Subtype:167 or 170 or 171}),"167/170/171 not consumed by extensions");
        t.Check(entries.Any(e=>e.Kind=="snapshot"&&e.Key=="pledgeWindow"),"snapshot registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetPledgeWindow"),"reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);

        // 2. 解碼：3.81 S_Pledge(name, notes) = FA A9 S(name) + 62 byte 零填滿。
        var memoPkt=PledgeProtocol.EncodeMemo("Alice","守城");
        t.Equal(72,memoPkt.Length,"169 = 2 + S(Alice)6 + 62, padded to 72");
        t.Equal("FAA9416C69636500",Convert.ToHexString(memoPkt,0,8),"169 header bytes");
        t.Equal(new PledgeMemo("Alice","守城"),PledgeProtocol.DecodeMemo(memoPkt),"decode 169");
        t.Equal(new PledgeMemo("Bob",""),PledgeProtocol.DecodeMemo(PledgeProtocol.EncodeMemo("Bob","")),"empty memo");
        var full=new string('a',62);
        t.Equal(full,PledgeProtocol.DecodeMemo(PledgeProtocol.EncodeMemo("Bob",full)).Memo,"62-byte memo without terminator");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeMemo([250,169,(byte)'A',0,1,2,3]),"truncated memo rejected");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeMemo(LoginPackets.Pad([250,169,0,..new byte[62]])),"blank name rejected");
        t.Throws<ProtocolException>(()=>PledgeProtocol.DecodeMemo(PledgeProtocol.EncodeOnline("A")),"wrong subcode rejected");
        t.Equal("FAAB0200416C6963650000426F620000",Convert.ToHexString(PledgeProtocol.EncodeOnline("Alice","Bob")),"171 synthetic bytes (C250 C171 H2, S C0 per member)");

        // 3. 會話：初始沒資料；169 不開窗；171 開窗並保留備註；171 仍交給 Clan.cs（不計 unhandled）。
        var s=new WorldSession();
        t.Check(s.PledgeWindow.Online==null&&s.PledgeWindow.OpenRevision==0,"initially no window data",s.PledgeWindow);
        s.Receive(PledgeProtocol.EncodeMemo("Alice","守城"),"");
        t.Equal(0,Unhandled(s,"0xFA/0xA9"),"169 consumed");
        t.Check(s.PledgeWindow is {Online:null,OpenRevision:0},"169 alone does not open",s.PledgeWindow);
        t.Equal("守城",s.PledgeWindow.Memo("alice"),"memo stored case-insensitively");
        s.Receive(PledgeProtocol.EncodeOnline("Alice","Bob"),"");
        t.Equal(0,Unhandled(s,"0xFA/0xAB"),"171 still handled by main dispatch");
        var pw=s.PledgeWindow;
        t.Check(pw.OpenRevision==1&&pw.Online is ["Alice","Bob"],"171 opens with online list",pw);
        t.Equal("守城",pw.Memo("Alice"),"memo kept after 171");
        t.Equal("",pw.Memo("Bob"),"no memo → blank");
        s.Receive(PledgeProtocol.EncodeMemo("Bob","補師"),"");
        t.Equal("補師",s.PledgeWindow.Memo("Bob"),"169 updates memo");
        t.Equal(1L,s.PledgeWindow.OpenRevision,"169 does not bump open revision");
        var snap=Snap(s);
        t.Equal(2,snap.GetProperty("online").GetArrayLength(),"snapshot online");
        t.Equal("補師",snap.GetProperty("memos").GetProperty("Bob").GetString(),"snapshot memo");
        // 壞的 171：觀察端不開窗，主分派照原樣報錯。
        t.Throws<ProtocolException>(()=>s.Receive([250,171,1,0,0,0,0,0],""),"bad 171 still rejected by main dispatch");
        t.Equal(1L,s.PledgeWindow.OpenRevision,"bad 171 does not open");
        t.Throws<ProtocolException>(()=>s.Receive([250,169,0,0,0,0,0,0],""),"bad 169 rejected");
        s.Receive(PledgeProtocol.EncodeOnline(),"");
        t.Check(s.PledgeWindow is {OpenRevision:2,Online.Count:0},"empty 171 opens with no members",s.PledgeWindow);
        s.Reset();
        t.Check(s.PledgeWindow is {Online:null} p0&&p0.Memos.Count==0,"Reset clears",s.PledgeWindow);

        // 4. 版面：合成 XML 載入兩個視窗，座標等於 XML（相對視窗）。
        var layout=UiLayout.Parse(Xml,"PledgeUI.xml");
        var panel=new PledgePanel();panel.Load(layout,Get);
        t.Check(!panel.Visible,"not visible before 171");
        panel.Update(World(new PledgeWindowState(0,0,"",["X"],new Dictionary<string,string>())));
        t.Check(!panel.Visible,"OpenRevision 0 does not open");
        var data=new PledgeWindowState(1,1,"紅血盟",["Alice","Bob","Carol","Dave","Eve"],new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["bob"]="補師"});
        panel.Update(World(data));
        t.Check(panel.Visible&&!panel.ShowingNotice,"171 opens the no-notice window");
        t.Equal(new Rectangle(0,0,277,423),panel.Bounds,"bounds = base 4450 at XML (0,0)");
        t.Equal(new Rectangle(260,13,12,12),panel.CloseRect,"close button (260,13)");
        t.Equal(new Rectangle(16,41,200,16),panel.NoticeRect,"notice bar (16,41)");
        t.Equal(new Rectangle(216,41,12,16),panel.NoticeArrowRect,"notice arrow relative to parent (16+200,41)");
        var w=panel.Current!;
        t.Equal(new Rectangle(36,23,169,20),w.PledgeLabel,"pledge label = control (19,25) + label (17,-2)");
        t.Equal(new Point(16,61),w.SortButtons[0].At,"sort button 0");
        t.Equal(new Point(36,61),w.SortButtons[1].At,"sort button 1 follows surf width");
        t.Equal(3,w.Rows,"rows from XML");
        t.Equal(new Rectangle(21+20,82,74,15),panel.Cell(0,"Name_Label"),"row 0 name column (class 19 + gap 1)");
        t.Equal(new Rectangle(21+131,82+15*2,97,15),panel.Cell(2,"Memo_Label"),"row 2 memo column");
        t.Equal(new Rectangle(255,67,12,14),panel.ScrollUpRect,"scroll up");
        t.Equal(new Rectangle(255,67+277,12,14),panel.ScrollDownRect,"scroll down");
        t.Equal(67+14,panel.ThumbY,"thumb at ScrollStart when scroll=0");

        // 5. 捲動：5 人、3 列 → 最多捲 2；拉桿到底。
        var cmd=new UiNullCommands();
        t.Equal(2,panel.MaxScroll,"max scroll");
        t.Check(panel.Mouse(Click(new(260,350)),cmd)&&panel.Scroll==1,"down button scrolls");
        panel.Mouse(new(UiMouseKind.Wheel,new(100,100),MouseButtons.None,0,-120,Keys.None),cmd);
        t.Equal(2,panel.Scroll,"wheel down scrolls");
        panel.ScrollBy(5);t.Equal(2,panel.Scroll,"scroll clamped");
        t.Equal(67+14+(262-20),panel.ThumbY,"thumb at bottom");
        t.Check(panel.Mouse(Click(new(260,70)),cmd)&&panel.Scroll==1,"up button scrolls");

        // 6. 畫面：只畫在 Bounds 內，缺 surf（4430 等）不畫也不丟例外。
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(panel.Bounds);panel.Paint(canvas);canvas.Release();
            t.Check(canvas.Bitmap.GetPixel(5,5).ToArgb()==Color.FromArgb(255,0x20,0x40,0x60).ToArgb(),"base surf painted at origin");
            t.Check(canvas.Bitmap.GetPixel(400,500).ToArgb()==Color.Black.ToArgb(),"nothing outside bounds");
        }

        // 7. 公告鈕切換視窗；拖曳帶；窗內點擊被吞；窗外不吞。
        t.Check(panel.Mouse(Click(new(20,45)),cmd)&&panel.ShowingNotice,"notice button switches to notice window");
        t.Equal(2,panel.Current!.Rows,"notice window rows");
        t.Equal(1,panel.Scroll,"scroll clamped to notice window");
        t.Check(panel.Mouse(Click(new(220,45)),cmd)&&!panel.ShowingNotice,"arrow switches back");
        t.Check(panel.IsDragHandle(new(100,10)),"top 20 px is drag handle");
        t.Check(!panel.IsDragHandle(new(100,25)),"below strip is not drag handle");
        t.Check(!panel.IsDragHandle(new(262,15)),"close button is not drag handle");
        panel.MoveTo(new(100,50));
        t.Equal(new Rectangle(100,50,277,423),panel.Bounds,"moved");
        t.Equal(new Rectangle(360,63,12,12),panel.CloseRect,"close follows window");
        t.Check(panel.Mouse(Click(new(150,200)),cmd),"click inside swallowed");
        t.Check(!panel.Mouse(Click(new(10,10)),cmd),"click outside not handled");
        t.Equal(0,cmd.Calls.Count,"no outward commands");

        // 8. 關閉：關閉鈕／Esc；同一個 OpenRevision 不再開；下一個 171 重開並捲回頂端。
        t.Check(panel.Mouse(Click(new(362,65)),cmd)&&!panel.Visible,"close button closes");
        panel.Update(World(data));
        t.Check(!panel.Visible,"same revision stays closed");
        panel.Update(World(data with{OpenRevision=2}));
        t.Check(panel.Visible&&panel.Scroll==0,"next 171 reopens at top");
        t.Check(!panel.Key(new(Keys.Escape|Keys.Shift,true,false),cmd),"shift+esc ignored");
        t.Check(panel.Key(new(Keys.Escape,true,false),cmd)&&!panel.Visible,"esc closes");
        t.Check(!panel.Key(new(Keys.Escape,true,false),cmd),"esc ignored when closed");

        // 9. 離開世界／沒有資料就關；穿過 HpMp／巨集包裝也找得到資料。
        panel.Update(World(data with{OpenRevision=3}));
        t.Check(panel.Visible,"reopened");
        panel.Update(World(data with{OpenRevision=3},inWorld:false));
        t.Check(!panel.Visible,"leaving world hides");
        panel.Update(World(null));
        t.Check(!panel.IsOpen,"no data closes");
        var wrapped=new UiHpMpGameState(World(data with{OpenRevision=4}),[]);
        t.Equal(4L,UiPledgeGameState.Of(wrapped)?.OpenRevision,"found through UiHpMpGameState");
        t.Equal((PledgeWindowState?)null,UiPledgeGameState.Of(new UiStaticGameState(true)),"plain state has no pledge data");

        // 10. 版面錯誤：缺視窗、壞 surf 都丟 UiLayoutException（不補預設座標）。
        t.Throws<UiLayoutException>(()=>new PledgePanel().Load(UiLayout.Parse("<PledgeUI/>","PledgeUI.xml"),Get),"missing windows rejected");
        t.Throws<UiLayoutException>(()=>new PledgePanel().Load(UiLayout.Parse("<Other/>","x.xml"),Get),"wrong root rejected");
        t.Throws<UiLayoutException>(()=>new PledgePanel().Load(UiLayout.Parse(Xml.Replace("BaseSurf = \"4450;1\"","BaseSurf = \";1\""),"PledgeUI.xml"),Get),"empty surf rejected");
    }
}

}
