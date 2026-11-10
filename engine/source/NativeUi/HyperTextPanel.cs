namespace P06.NativeUi;

// R4-I（T300／#712）：NPC 對話／HTML 頁的原生 HYPER_TEXT_WINDOW（TW16 HyperTextUI，sub_875810；S39 → sub_940030 → sub_BBCB00）。
// 依據：docs/reference/windows.md 1.5、2.1 節（WIN§2.1）、windows-2.md 5.1、5.3 節（WIN2）、followups-4.md 第 5 節（FU4§5）、
// gap-matrix.md 第 1 節 op 144／224、第 3 節 C_DIALOG、第 4.1 節 HyperTextUI。
// 已驗證（引用）：
//   - 一個 HYPER_TEXT_WINDOW 是 NPC HTML、修理空清單頁、查詢與排名結果、城堡 tax／withdraw／deposit 頁、S136 數字框頁的共用視窗
//     （WIN§2.1、WIN2§5.1、§5.3）。→ 本面板一次只顯示一頁：NPC 頁與查詢頁誰最新就顯示誰（HyperTextRouter，op 144 的差異）。
//   - 版面是 uiextra86.xml 第 3 段：底 1968 (6,9)、框 1969（畫在窗原點，sub_542370）、捲軸 1970 (217,51)、上 2042 (217,37)、
//     下 2040 (217,275)、拉桿 2044（無座標）、關閉 X 1962 (225,15)（FU4§5、WIN§1.5）。元件位置一律讀執行期的 XML，不寫死。
//   - S136 的數字框在 HTML 頁內（不是 InputWindow，WIN2§5.3）；城堡頁 tax／withdraw／deposit 也是同一窗的頁（WIN2§5.1）。
// 推測（標明，未逐行追 HyperTextUI 的繪製函式）：
//   - 文字起點 (21,44)、行高 15、一次 16 行：借用同版面的 sub_A559D0（FU4§5）；可用寬度 192（21 到捲軸 217 之前留 4）。
//   - 關閉鈕用 XML 的 1962 (225,15)（sub_A559D0 寫死的 9183 是那個窗自己的做法）；標題列 (1,1,239,20) 拖曳同 sub_A559D0。
//   - 窗原點用 XML 的 x／y；連結列用黃色（調色盤 13）、停用選項灰色（7）、提示文字灰色、本文白色（14）。
//   - 連結各占一列（引擎的對話模板把 HTML 連結拆成選項，原版是行內連結）：R9-F 之後只用在對不上 HTML 文件的頁。
//   - 捲動範圍 0..(行數−16)；滾輪、上下鈕步長 1 列。
// R9-E（T365／#849），依據 followups-9.md 第 10 節（FU9§10）。已驗證（引用）：
//   - ESC：sub_875F80（FO 0x475380）收到 27 就關窗。HyperText 窗不在 sub_A57C60 的 4 格堆疊裡，ESC 由窗自己處理。
//   - 數字框 CNumberInputFrame（sub_862250）：下限 1、上限 N。鍵盤 sub_872BE0：0–9 逐位輸入並夾在 [1, N]、Backspace 去掉最後一位、
//     Enter 結束；滑鼠 sub_872DC0：上下鈕 surf 5073，放開時 +1／−1，超過上限回下限、低於下限回上限（循環）。
//   - OK／Cancel：sub_875B20 每次畫圖前把兩鈕的可按狀態設成「頁內有數字框」；OK（sub_876010）只在數值在範圍內時送出。
// R9-E 推測（真機再確認）：
//   - 5073 本機只有一張 13×11 朝下三角（Sprite10.idx 的 5073.img）：下鈕原圖、上鈕上下翻轉；兩鈕排在數字框右邊。
//   - 4203／4205 等同上下鈕，推測是方向鍵 ↑／↓。下限用 max(1, Min)（城堡頁有自己的下限）；輸入夾值只夾 [1, N]（同原版），
//     低於 Min 時 OK 不送。Backspace 刪到空字串時保留空白（不補 1），OK 不送。
//   - OK 1114（按下 1115）(47,292)、Cancel 1111（按下 1112）(133,292)：座標取自參考客戶端 ly 的 HyperTextUI.xml（本機 24 個 idx 都沒有
//     這個 XML；不複製進專案），只在有數字框時畫與可按（停用外觀沒有查證，不畫）。本機缺這幾張圖時退回引擎自訂的「確定」／「取消」
//     文字鈕（畫在數字框那一列）。Cancel：城堡頁 → CancelNumber；S136 數量頁 → 關窗。
// 原版連結的滑鼠移入效果：R10-B 已做（Rich 列，見下方 R10-B）；舊排法的 Link 列不變色。
// R9-F（T366／#850）：行內排版器（NativeUi/HyperTextLayout.cs）。頁有原始 HTML 文件（view.Document，或 WorldSession 登記在
//   HyperTextDocuments.Shared、文字對得上的那份）且選項 id 對得上時，本文改用 RowKind.Rich 列：連結、<font>、<img>、<var>、
//   <b>/<i>/<u>/<p>/<br>、對齊都排在文字流裡，連結不再另起一列。高於 15 的列（圖）後面接 RowKind.Spacer 佔位，捲動仍以 15 px 為一格。
//   對不上（查詢頁的退回文字、沒有模板）時維持上面「連結各占一列」的舊排法。
// R10-B（T383／#891），依據 docs/reference/followups-11.md §1、§2（FU11 D1–D7）。只改 Rich 列（舊排法的顏色不動）。已驗證（引用 FU11§1）：
//   - 顏色＝COLORREF（HyperTextColors.Order＝Bgr）；連結 RGB(FF,FF,B0)；沒有 fg 的字 RGB(EE,ED,EE)（#2、#4、#5）。
//   - 滑鼠下的文字連結整個連結（同一個選項 id 的所有片段）畫 RGB(0A,F0,32)；圖片連結不變色；沒有「已點過」色（#7、#8、#9）。
//     原版每個滑鼠訊息（含 WM_MOUSEMOVE）先做命中測試、先清成 −1 再寫入，所以本面板每次 Move／Down／Up 都重算 HoverLink。
//   - 底線色固定 RGB(6E,6E,46)、x 最多到本文左界＋198；帶 link／action／url 的 <a> 也有底線（#12、#13）。
//   - bg 範圍＝片段寬 × 整列高（同列有圖時也用整列高）（#10）。
// 引擎自訂（原版繪字沒有停用分支，FU11 D4）：停用的連結畫灰、不變 hover 色、不可點。
// 推測：沒有圖、改畫成文字標籤的可點圖（引擎自訂）仍算圖片連結，不變 hover 色。

/// <summary>頁上的一個連結／選項（Id＝引擎對話模板的選項 id）。</summary>
public sealed record HyperTextLink(int Id,string Label,bool Enabled);
public enum HyperTextSource { Npc, Query }
public enum HyperTextNumberKind { Amount, Castle }
/// <summary>頁內數字框：S136（Amount，回 C11）或城堡頁（Castle，回 C19／C56／C44，可取消）。Revision＝詢問版本。</summary>
public sealed record HyperTextNumberBox(HyperTextNumberKind Kind,long Revision,long Min,long Max,long Initial,bool Answerable,bool CanSend,string Range);
/// <summary>
/// R7-D（#806）：頁內血盟名輸入框（HTML 動作 surrender／cease／pledgefocusadd 的 578／579／3361）。Revision＝名稱框版本；
/// Prompt＝提示文字；CanSend＝宿主說現在可送。原版是共用 InputWindow（sub_AC98E0），畫在頁內是引擎自訂（推測，見 NpcInteraction.cs R7-D）。
/// </summary>
public sealed record HyperTextNameBox(long Revision,string Prompt,bool CanSend);
/// <summary>要顯示的一頁。Revision＝NPC 頁的 InteractionRevision 或查詢頁的 QueryRevision（宿主用來擋過期操作）。</summary>
/// <summary>R9-F：Document＝這頁的 HTML 文件（null＝面板依 Text 到 HyperTextDocuments 找）。</summary>
public sealed record HyperTextView(HyperTextSource Source,int Revision,string Text,IReadOnlyList<HyperTextLink> Links,string Notice,HyperTextNumberBox? Number,HyperTextNameBox? Name=null,HyperTextDocument? Document=null);

/// <summary>面板對外的動作。面板不碰 LoginConnection；過期、走路中、重複送出由宿主（NpcUi.cs）擋。</summary>
public interface IHyperTextHost {
    void Link(HyperTextView shown,int option);
    void Submit(HyperTextView shown,long value);
    void CancelNumber(HyperTextView shown);
    /// <summary>關窗：同一個視窗裡的頁（含被取代的另一頁）都關掉。</summary>
    void Close(HyperTextView shown);
    /// <summary>R7-D：名稱框按確定（名稱已去頭尾空白，可能是空字串：原版空白不送，由宿主決定）。預設不做事。</summary>
    void SubmitName(HyperTextView shown,string name){}
    /// <summary>R7-D：名稱框按取消。預設不做事。</summary>
    void CancelName(HyperTextView shown){}
}

/// <summary>
/// 單窗取代（op 144 的差異，gap-matrix 第 4.1 節）：NPC 頁與查詢頁共用一個窗，最新出現（版本改變）的那頁在前；被取代的頁
/// 在同一版本內不再浮出（原版內容已被蓋掉）。兩邊同一格都改變時查詢頁在前（伺服器主動頁是後到的封包，推測）。
/// </summary>
public sealed class HyperTextRouter {
    int lastNpc=int.MinValue,lastQuery=int.MinValue,buriedNpc=int.MinValue,buriedQuery=int.MinValue;
    public HyperTextSource? Front{get;private set;}
    public void Reset(){lastNpc=lastQuery=buriedNpc=buriedQuery=int.MinValue;Front=null;}
    public HyperTextSource? Pick(bool npcPage,int npcRevision,bool queryPage,int queryRevision){
        bool npcNew=npcPage&&npcRevision!=lastNpc,queryNew=queryPage&&queryRevision!=lastQuery;
        if(npcPage)lastNpc=npcRevision;else lastNpc=int.MinValue;
        if(queryPage)lastQuery=queryRevision;else lastQuery=int.MinValue;
        if(queryNew){if(npcPage)buriedNpc=npcRevision;Front=HyperTextSource.Query;}
        else if(npcNew){if(queryPage)buriedQuery=queryRevision;Front=HyperTextSource.Npc;}
        bool npcLive=npcPage&&buriedNpc!=npcRevision,queryLive=queryPage&&buriedQuery!=queryRevision;
        if(Front==HyperTextSource.Npc&&!npcLive)Front=queryLive?HyperTextSource.Query:null;
        else if(Front==HyperTextSource.Query&&!queryLive)Front=npcLive?HyperTextSource.Npc:null;
        else if(Front==null)Front=queryLive?HyperTextSource.Query:npcLive?HyperTextSource.Npc:null;
        return Front;
    }
}

public sealed class HyperTextPanel:IUiMovablePanel,IUiClosablePanel,IUiTextInput {
    [UiPanel(173)] internal static UiPanelRegistration Registration=>new(HyperTextPanel.PanelId,HyperTextPanel.LayoutName,_=>new HyperTextPanel());   // R4-I (#712): HYPER_TEXT_WINDOW (uiextra86)
    public const string PanelId="HYPER_TEXT_WINDOW";
    public const string LayoutName="uiextra86";
    public const string Section="HYPER_TEXT_WINDOW";
    public static readonly Rectangle TitleBar=Rectangle.FromLTRB(1,1,239,20);
    public const int VisibleLines=16,LineHeight=15,TextWidth=192;
    public static readonly Point FirstLine=new(21,44);
    /// <summary>數字框列：輸入框寬、按鈕寬（引擎自訂，推測）。R9-E：框縮成 72，右邊放 5073 上下鈕，文字鈕排在上下鈕之後。</summary>
    public const int InputWidth=72,ButtonWidth=36,ButtonGap=6,MaxDigits=10;
    /// <summary>R9-E：數字框上下鈕 surf 5073（FU9§10，已驗證）；排在框右邊、間隔 2（推測）。</summary>
    public const int SpinSurf=5073,SpinGap=2;
    /// <summary>R9-E：OK／Cancel 圖鈕（surf 與座標依 ly HyperTextUI.xml，座標是推測）。</summary>
    public const int OkSurf=1114,OkDownSurf=1115,CancelSurf=1111,CancelDownSurf=1112;
    public static readonly Point OkOffset=new(47,292),CancelOffset=new(133,292);
    public static Color TextColor=>ChatPalette.Color(ChatFormat.White);
    public static Color LinkColor=>ChatPalette.Color(ChatFormat.Yellow);
    public static Color DisabledColor=>ChatPalette.Color(ChatFormat.Grey);
    const string BaseName="SURF_RENEWAL_WIN_HYPER_TEXT_BASE",FrameName="SURF_RENEWAL_WIN_HYPER_TEXT_FRAME",CloseName="SURF_RENEWAL_BTN_QUIT_X",
        ScrollName="SURF_RENEWAL_WIN_SCROLLBAR_LONG",UpName="SURF_RENEWAL_WIN_INC_BUT_OFF",DownName="SURF_RENEWAL_WIN_DEC_BUT_OFF";

    public sealed record Element(string Name,int Surf,Point Offset,UiSurface? Surface) {
        public Rectangle Local=>new(Offset,Surface==null?Size.Empty:new Size(Surface.Width,Surface.Height));
        public bool Drawn=>Surface!=null&&Surface.Width>0&&Surface.Height>0;
    }
    public enum RowKind { Text, Link, Notice, Range, Number, Name, Rich, Spacer }
    /// <summary>R7-D 名稱框列：輸入框寬（引擎自訂，推測；輸入框＋確定＋取消＝192）。</summary>
    public const int NameInputWidth=108;
    /// <summary>畫面上的一列（內容座標：第 Index 列）。Link 列帶選項 id；R9-F Rich 列帶排好的一列（Spacer＝上一個 Rich 列的延伸）。</summary>
    public sealed record Row(RowKind Kind,string Text,int LinkId=-1,bool Enabled=true,HyperTextLine? Line=null);

    readonly Func<string,int> measure;
    public HyperTextPanel(Func<string,int>? measure=null){this.measure=measure??DefaultMeasure;}
    static readonly Lazy<Graphics> measureGraphics=new(()=>Graphics.FromImage(new Bitmap(1,1)));
    static int DefaultMeasure(string s){lock(measureGraphics)return UiText.Measure(measureGraphics.Value,s).Width;}

    /// <summary>R9-F：找 HTML 文件的地方（測試可換成自己的）。</summary>
    public HyperTextDocuments Documents{get;set;}=HyperTextDocuments.Shared;
    /// <summary>R9-F：目前這頁用的 HTML 文件（已 $ 展開）；null＝舊排法。</summary>
    public HyperTextDocument? Document{get;private set;}
    Func<int,UiSurface?> surfSource=_=>null;readonly Dictionary<int,UiSurface?> surfCache=[];
    UiSurface? Surface(int id){if(!surfCache.TryGetValue(id,out var s))surfCache[id]=s=surfSource(id);return s;}

    public IHyperTextHost? Host{get;private set;}
    public void Attach(IHyperTextHost host)=>Host=host;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds{get{
        if(!Visible||local.IsEmpty)return Rectangle.Empty;
        var r=local;
        // R9-E：OK／Cancel 圖鈕只在有數字框時出現（ly 座標在 uiextra86 底圖之下，推測）。
        if(HasNumber)foreach(var e in new[]{OkButton,CancelButton})if(e is {Drawn:true})r=Rectangle.Union(r,e.Local);
        return new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);
    }}
    public bool Visible=>view!=null;
    public Element? Base{get;private set;}
    public Element? Frame{get;private set;}
    public Element? ScrollBar{get;private set;}
    public Element? Up{get;private set;}
    public Element? Down{get;private set;}
    public Element? CloseButton{get;private set;}
    /// <summary>R9-E：OK 圖鈕 1114 (47,292)；本機沒有圖時 Drawn＝false，退回數字框列的文字鈕。</summary>
    public Element? OkButton{get;private set;}
    /// <summary>R9-E：Cancel 圖鈕 1111 (133,292)。</summary>
    public Element? CancelButton{get;private set;}
    UiSurface? okDown,cancelDown,spinDown;Bitmap? spinUp;
    /// <summary>R9-E：數字框上下鈕的圖（5073）都在才畫、才可點（缺圖就不畫；方向鍵照樣可用）。</summary>
    public bool SpinDrawn=>spinDown is {Width:>0,Height:>0}&&spinUp!=null;
    /// <summary>R9-E：頁內有數字框（OK／Cancel 可按的條件，sub_875B20）。</summary>
    public bool HasNumber=>view?.Number!=null;
    public HyperTextView? View=>view;
    public IReadOnlyList<Row> Rows=>rows;
    public int Scroll{get;private set;}
    public int MaxScroll=>Math.Max(0,rows.Count-VisibleLines);
    /// <summary>數字框目前的字（只收數字）。</summary>
    public string Input{get;private set;}="";
    /// <summary>數字框有輸入焦點（點框或新詢問出現時；按 Enter 送出或點窗內其他地方時放開）。</summary>
    public bool InputFocused{get;private set;}
    /// <summary>R7-D 名稱框目前的字。</summary>
    public string NameInput{get;private set;}="";
    /// <summary>R7-D 名稱框有輸入焦點（新框出現或點框時；Enter／Esc 或點窗內其他地方放開）。</summary>
    public bool NameFocused{get;private set;}

    HyperTextView? view;(HyperTextSource,int)? dismissed;long numberRevision=long.MinValue,nameRevision=long.MinValue;
    List<Row> rows=[];Rectangle local;string? pressed;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var window=layout.Root.Child(Section);
        surfSource=surf;surfCache.Clear();Document=null;Lines=[];richKey=null;
        Element Find(string name,bool atOrigin=false){
            var matches=window.Children.Where(c=>c.Attr("surfName")==name).Take(2).ToList();
            if(matches.Count!=1)throw new UiLayoutException(layout.FileName,window.Path+"/@surfName="+name,matches.Count==0?"缺少元件":"元件不唯一");
            var n=matches[0];int[] ids=n.Surfs();
            if(ids.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"surfNumber 空白");
            var at=atOrigin&&n.Attr("xPos")==null&&n.Attr("yPos")==null?Point.Empty:new Point(n.Int("xPos"),n.Int("yPos"));
            return new(name,ids[0],at,surf(ids[0]));
        }
        Base=Find(BaseName);Frame=Find(FrameName,atOrigin:true);ScrollBar=Find(ScrollName);Up=Find(UpName);Down=Find(DownName);CloseButton=Find(CloseName);
        OkButton=new("OKBtn",OkSurf,OkOffset,surf(OkSurf));CancelButton=new("CancelBtn",CancelSurf,CancelOffset,surf(CancelSurf));
        okDown=surf(OkDownSurf);cancelDown=surf(CancelDownSurf);
        spinDown=surf(SpinSurf);spinUp?.Dispose();spinUp=null;
        if(spinDown is {Width:>0,Height:>0}){spinUp=(Bitmap)spinDown.Bitmap.Clone();spinUp.RotateFlip(RotateFlipType.RotateNoneFlipY);}
        local=new[]{Base,Frame,ScrollBar,Up,Down,CloseButton}.Where(e=>e.Drawn).Select(e=>e.Local).Append(TitleBar).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        Origin=new(window.Int("x"),window.Int("y"));
        view=null;rows=[];Scroll=0;pressed=null;dismissed=null;Input="";InputFocused=false;numberRevision=long.MinValue;
        NameInput="";NameFocused=false;nameRevision=long.MinValue;
    }

    /// <summary>
    /// 宿主每格送目前要顯示的頁（null＝沒有頁）。換頁（來源或版本不同）時捲回頂端；同一頁只更新內容（例如可送出狀態）。
    /// 被使用者或堆疊關掉的那一頁，在宿主真的關掉它之前不再打開。
    /// </summary>
    public void Show(HyperTextView? next){
        if(next==null){view=null;pressed=null;InputFocused=false;NameFocused=false;dismissed=null;Document=null;HoverLink=-1;return;}
        var key=(next.Source,next.Revision);
        if(dismissed==key)return;
        dismissed=null;
        bool newPage=view==null||(view.Source,view.Revision)!=key;
        view=next;
        if(next.Number is {} n&&n.Revision!=numberRevision){numberRevision=n.Revision;Input=n.Answerable?n.Initial.ToString():"";InputFocused=n.Answerable;}
        else if(next.Number==null){numberRevision=long.MinValue;Input="";InputFocused=false;}
        // 新的名稱框：清空並取得焦點（數字框讓出焦點）；同一個框只更新可送狀態。
        if(next.Name is {} nb&&nb.Revision!=nameRevision){nameRevision=nb.Revision;NameInput="";NameFocused=true;InputFocused=false;}
        else if(next.Name==null){nameRevision=long.MinValue;NameInput="";NameFocused=false;}
        rows=BuildRows(next);
        if(newPage){Scroll=0;pressed=null;HoverLink=-1;}
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
    }

    /// <summary>關閉鈕與堆疊滿（UiRoot.Evict）共用：請宿主關掉頁，面板先藏起來。</summary>
    public void Close(){
        if(view is not {} shown)return;
        dismissed=(shown.Source,shown.Revision);view=null;pressed=null;InputFocused=false;NameFocused=false;HoverLink=-1;
        Host?.Close(shown);
    }

    public void Update(IUiGameState state){if(!state.InWorld)Show(null);}

    /// <summary>
    /// R9-F：這頁的 HTML 文件。要同時符合：文字對得上（Plain＝Text，或 $ 展開後相同）、文件的選項數＝頁的選項數、
    /// 頁的選項 id 剛好是 0..n−1（NpcTemplates 的編號）。否則 null（舊排法）。
    /// </summary>
    HyperTextDocument? ResolveDocument(HyperTextView v){
        var doc=v.Document?.ResolveTo(v.Text)??(v.Document==null?Documents.Find(v.Text):null);
        if(doc==null||doc.LinkCount!=v.Links.Count)return null;
        var ids=v.Links.Select(l=>l.Id).OrderBy(i=>i).ToArray();
        for(int i=0;i<ids.Length;i++)if(ids[i]!=i)return null;
        return doc;
    }
    /// <summary>R9-F：排好的本文列（沒有文件時是空的）。</summary>
    public IReadOnlyList<HyperTextLine> Lines{get;private set;}=[];
    (string,HyperTextDocument?,string,HyperTextDocuments,int)? richKey;

    List<Row> BuildRows(HyperTextView v){
        var list=new List<Row>();
        // 宿主每格都 Show：同一頁（文字、文件、選項 id 都相同）沿用上次的對照與排版結果。
        var key=(v.Text,v.Document,string.Join(",",v.Links.Select(l=>l.Id)),Documents,Documents.Version);
        if(richKey!=key){richKey=key;Document=ResolveDocument(v);Lines=Document is {} d?HyperTextLayout.Build(d,TextWidth,measure,Surface,LineHeight):[];}
        if(Document!=null){
            foreach(var line in Lines){
                list.Add(new(RowKind.Rich,line.Text,Line:line));
                for(int extra=(line.Height+LineHeight-1)/LineHeight-1;extra>0;extra--)list.Add(new(RowKind.Spacer,""));
            }
        }else{
            foreach(var line in Wrap(v.Text))list.Add(new(RowKind.Text,line));
            if(v.Links.Count>0&&list.Count>0)list.Add(new(RowKind.Text,""));
            foreach(var link in v.Links)foreach(var line in Wrap(link.Label))list.Add(new(RowKind.Link,line,link.Id,link.Enabled));
        }
        if(v.Number is {} n){list.Add(new(RowKind.Text,""));list.Add(new(RowKind.Range,n.Range));list.Add(new(RowKind.Number,""));}
        if(v.Name is {} nb){list.Add(new(RowKind.Text,""));foreach(var line in Wrap(nb.Prompt))list.Add(new(RowKind.Text,line));list.Add(new(RowKind.Name,""));}
        if(v.Notice.Length>0){list.Add(new(RowKind.Text,""));foreach(var line in Wrap(v.Notice))list.Add(new(RowKind.Notice,line));}
        return list;
    }

    /// <summary>依可用寬度逐字換行（中文沒有空白可斷）；\n 強制換行，\r 忽略。</summary>
    public IReadOnlyList<string> Wrap(string text){
        var lines=new List<string>();
        foreach(var raw in text.Replace("\r","").Split('\n')){
            if(raw.Length==0){lines.Add("");continue;}
            int start=0;
            while(start<raw.Length){
                int end=start+1;
                while(end<raw.Length&&measure(raw[start..(end+1)])<=TextWidth)end++;
                lines.Add(raw[start..end]);start=end;
            }
        }
        while(lines.Count>0&&lines[^1].Length==0)lines.RemoveAt(lines.Count-1);
        return lines;
    }

    public Rectangle At(Rectangle r)=>r.IsEmpty?Rectangle.Empty:new(Origin.X+r.X,Origin.Y+r.Y,r.Width,r.Height);
    public Rectangle At(Element? e)=>e==null||!e.Drawn?Rectangle.Empty:At(e.Local);
    public Rectangle CloseRect=>At(CloseButton);
    /// <summary>畫面第 r 列（0..15）的左上角。</summary>
    public Point LineAt(int r)=>new(Origin.X+FirstLine.X,Origin.Y+FirstLine.Y+LineHeight*r);
    /// <summary>畫面第 r 列整列的點擊範圍（文字寬度內）。</summary>
    public Rectangle LineRect(int r){var p=LineAt(r);return new(p.X,p.Y,TextWidth,LineHeight);}
    /// <summary>內容第 index 列目前在畫面第幾列；不在畫面上回傳 null。</summary>
    public int? ScreenLine(int index)=>index>=Scroll&&index<Scroll+VisibleLines?index-Scroll:null;
    int NumberIndex=>rows.FindIndex(r=>r.Kind==RowKind.Number);
    Rectangle NumberPart(int offset,int width){if(ScreenLine(NumberIndex) is not int line)return Rectangle.Empty;var p=LineAt(line);return new(p.X+offset,p.Y,width,LineHeight-1);}
    public Rectangle InputRect=>NumberIndex<0?Rectangle.Empty:NumberPart(0,InputWidth);
    int SpinWidth=>spinDown?.Width??0;
    /// <summary>R9-E：文字鈕的起點（框＋兩個上下鈕之後；沒有上下鈕圖時緊接框）。</summary>
    int TextButtonsAt=>InputWidth+(SpinDrawn?SpinGap+SpinWidth*2+SpinGap:0)+ButtonGap;
    Rectangle SpinPart(int offset){
        if(!SpinDrawn||NumberIndex<0||ScreenLine(NumberIndex) is not int line)return Rectangle.Empty;
        var p=LineAt(line);int h=spinDown!.Height;
        return new(p.X+offset,p.Y+Math.Max(0,(LineHeight-1-h)/2),SpinWidth,h);
    }
    /// <summary>R9-E：5073 上鈕（+1，超過上限回下限）。</summary>
    public Rectangle SpinUpRect=>view?.Number is {Answerable:true}?SpinPart(InputWidth+SpinGap):Rectangle.Empty;
    /// <summary>R9-E：5073 下鈕（−1，低於下限回上限）。</summary>
    public Rectangle SpinDownRect=>view?.Number is {Answerable:true}?SpinPart(InputWidth+SpinGap+SpinWidth):Rectangle.Empty;
    /// <summary>OK：有 1114 圖時是圖鈕（只在有數字框時），否則是數字框列的文字鈕。</summary>
    public Rectangle SubmitRect=>!HasNumber?Rectangle.Empty:OkButton is {Drawn:true} ok?At(ok):NumberIndex<0?Rectangle.Empty:NumberPart(TextButtonsAt,ButtonWidth);
    /// <summary>Cancel：有 1111 圖時是圖鈕（S136 與城堡頁都有，只在有數字框時）；沒有圖時只有城堡頁有文字鈕。</summary>
    public Rectangle CancelRect=>!HasNumber?Rectangle.Empty:CancelButton is {Drawn:true} c?At(c)
        :view?.Number is {Kind:HyperTextNumberKind.Castle}&&NumberIndex>=0?NumberPart(TextButtonsAt+ButtonGap+ButtonWidth,ButtonWidth):Rectangle.Empty;
    /// <summary>R9-E：數字框輸入時的上限（[1, N]，sub_872BE0）。</summary>
    static long TypeMax(HyperTextNumberBox n)=>Math.Max(1,n.Max);
    /// <summary>R9-E：上下鈕循環的下限（max(1, Min)，推測）。</summary>
    static long SpinMin(HyperTextNumberBox n)=>Math.Min(Math.Max(1,n.Min),TypeMax(n));
    int NameIndex=>rows.FindIndex(r=>r.Kind==RowKind.Name);
    Rectangle NamePart(int offset,int width){if(ScreenLine(NameIndex) is not int line)return Rectangle.Empty;var p=LineAt(line);return new(p.X+offset,p.Y,width,LineHeight-1);}
    public Rectangle NameRect=>NameIndex<0?Rectangle.Empty:NamePart(0,NameInputWidth);
    public Rectangle NameSubmitRect=>NameIndex<0?Rectangle.Empty:NamePart(NameInputWidth+ButtonGap,ButtonWidth);
    public Rectangle NameCancelRect=>NameIndex<0?Rectangle.Empty:NamePart(NameInputWidth+ButtonGap*2+ButtonWidth,ButtonWidth);
    public IReadOnlyList<Row> VisibleRows=>rows.Skip(Scroll).Take(VisibleLines).ToArray();
    /// <summary>目前輸入可以送出：有詢問、可答、宿主說可送，且值在範圍內。</summary>
    public bool CanSubmit=>view?.Number is {Answerable:true,CanSend:true} n&&long.TryParse(Input,out long v)&&v>=Math.Max(1,n.Min)&&v<=n.Max;
    /// <summary>名稱框可以按確定（宿主說可送）。空白名稱也可按：原版確定後框照樣關、只是不送（FU6§1.1、§3.2）。</summary>
    public bool CanSubmitName=>view?.Name is {CanSend:true};
    /// <summary>名稱框最多幾個 Big5 位元組（同 C129 的 16 字緩衝）。</summary>
    public const int NameMaxBytes=HtmlActions.NameBytes;
    static int NameBytes(string s){try{return LoginPackets.TextEncoding.GetByteCount(s);}catch(System.Text.EncoderFallbackException){return int.MaxValue;}}

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        foreach(var e in new[]{Base,Frame,ScrollBar,Up,Down,CloseButton})
            if(e is {Drawn:true})canvas.DrawSurface(e.Surface,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
        PaintButtons(canvas);
        var visible=VisibleRows;
        for(int r=0;r<visible.Count;r++){
            var row=visible[r];var p=LineAt(r);
            switch(row.Kind){
                case RowKind.Number:PaintNumber(canvas);break;
                case RowKind.Name:PaintName(canvas);break;
                case RowKind.Link:canvas.DrawText(row.Text,p.X,p.Y,row.Enabled?LinkColor:DisabledColor);break;
                case RowKind.Notice:case RowKind.Range:canvas.DrawText(row.Text,p.X,p.Y,DisabledColor);break;
                case RowKind.Rich:case RowKind.Spacer:break;   // PaintRich
                default:canvas.DrawText(row.Text,p.X,p.Y,TextColor);break;
            }
        }
        PaintRich(canvas);
    }

    /// <summary>R9-F：本文區（16 列 × 15 px，寬 192）；Rich 列的圖與字裁在這裡面。</summary>
    public Rectangle TextArea=>new(Origin.X+FirstLine.X,Origin.Y+FirstLine.Y,TextWidth,LineHeight*VisibleLines);
    /// <summary>R9-F：與畫面重疊的 Rich 列（含從捲動位置上方延伸下來的高列）；Screen 可以是負的。</summary>
    IEnumerable<(int Screen,HyperTextLine Line)> RichInView(){
        int end=Math.Min(rows.Count,Scroll+VisibleLines);
        for(int i=0;i<end;i++)
            if(rows[i] is {Kind:RowKind.Rich,Line:{} line}&&i+Math.Max(1,(line.Height+LineHeight-1)/LineHeight)>Scroll)yield return (i-Scroll,line);
    }
    /// <summary>R9-F：行內連結的顏色：可按＝文件的字色（&lt;a&gt; 常數 0x00B0FFFF 或之後的 &lt;font fg&gt;），停用＝灰（引擎自訂）。</summary>
    public static Color RichLinkColor=>HyperTextColors.Link;
    /// <summary>R10-B：Rich 列沒有 fg 時的字色 RGB(EE,ED,EE)（FU11 D2，已驗證）。</summary>
    public static Color RichTextColor=>HyperTextColors.Default;
    /// <summary>R10-B：滑鼠下的文字連結 id（−1＝沒有；FU11 D3）。只認可按的文字片段，圖片連結不算。</summary>
    public int HoverLink{get;private set;}=-1;
    bool LinkEnabled(int id)=>view?.Links.Any(l=>l.Id==id&&l.Enabled)==true;
    /// <summary>R10-B：命中測試（同原版 sub_871260：先清成 −1，命中文字連結才寫入）。</summary>
    int HoverAt(Point p){
        if(Document==null||!TextArea.Contains(p))return -1;
        foreach(var (screen,line) in RichInView())foreach(var piece in line.Pieces)
            if(piece.Link>=0&&piece.Node.Kind==HyperTextNodeKind.Text&&PieceRect(screen,piece).Contains(p))return LinkEnabled(piece.Link)?piece.Link:-1;
        return -1;
    }
    internal Color PieceColor(HyperTextPiece piece){
        var fg=piece.Node.Style.Fg;
        if(piece.Link>=0){
            if(!LinkEnabled(piece.Link))return DisabledColor;   // 引擎自訂（FU11 D4）
            if(piece.Link==HoverLink&&piece.Node.Kind==HyperTextNodeKind.Text)return HyperTextColors.Hover;
            return fg is uint lf?HyperTextColors.Decode(lf):RichLinkColor;
        }
        return fg is uint f?HyperTextColors.Decode(f):RichTextColor;
    }
    /// <summary>R9-F：片段在畫面上的範圍（screen＝畫面第幾列，可為負）。</summary>
    public Rectangle PieceRect(int screen,HyperTextPiece piece){var o=LineAt(screen);return new(o.X+piece.X,o.Y+piece.Y,piece.Width,piece.Height);}
    /// <summary>R9-F：某個選項在畫面上看得到的第一個片段範圍（測試與點擊用；不在畫面上回 Empty）。</summary>
    public Rectangle LinkRect(int id){
        foreach(var (screen,line) in RichInView())foreach(var piece in line.Pieces)
            if(piece.Link==id&&Rectangle.Intersect(PieceRect(screen,piece),TextArea) is {IsEmpty:false} r)return r;
        return Rectangle.Empty;
    }
    void PaintRich(UiCanvas canvas){
        if(Document==null)return;
        var g=canvas.Graphics;var state=g.Save();
        try{
            g.SetClip(TextArea,System.Drawing.Drawing2D.CombineMode.Intersect);
            foreach(var (screen,line) in RichInView())foreach(var piece in line.Pieces){
                var r=PieceRect(screen,piece);
                if(piece.Image!=null){canvas.DrawSurface(piece.Image,r.X,r.Y);continue;}
                var style=piece.Node.Style;
                // R10-B：bg 用整列高（FU11 D6）。
                if(style.Bg is uint bg)using(var back=new SolidBrush(HyperTextColors.Decode(bg)))g.FillRectangle(back,r.X,r.Y,r.Width,Math.Max(LineHeight,line.Height));
                if(piece.Text.Length==0)continue;
                var color=PieceColor(piece);
                canvas.DrawText(piece.Text,r.X,r.Y,color);
                if(style.Bold)canvas.DrawText(piece.Text,r.X+1,r.Y,color);   // 推測：1 px 位移模擬粗體
                // R10-B：底線色固定 RGB(6E,6E,46)，x 最多到本文左界＋198（FU11 D5）。
                int underEnd=Math.Min(r.Right-1,TextArea.X+HyperTextColors.UnderlineMaxX);
                // 用 1 px 高的 FillRectangle（UiCanvas 是 PixelOffsetMode.Half，DrawLine 會落在兩列像素之間）。
                if(style.Underline&&underEnd>=r.X)using(var line1=new SolidBrush(HyperTextColors.Underline))g.FillRectangle(line1,r.X,r.Y+LineHeight-2,underEnd-r.X+1,1);
            }
        }finally{g.Restore(state);}
    }
    void PaintNumber(UiCanvas canvas){
        if(view?.Number is not {} n)return;
        var box=InputRect;
        using(var back=new SolidBrush(Color.Black))canvas.Graphics.FillRectangle(back,box);
        using(var pen=new Pen(InputFocused?LinkColor:DisabledColor))canvas.Graphics.DrawRectangle(pen,box.X,box.Y,box.Width-1,box.Height-1);
        canvas.DrawText(Input+(InputFocused?"_":""),box.X+3,box.Y+1,n.Answerable?TextColor:DisabledColor);
        if(SpinUpRect is {IsEmpty:false} up)canvas.DrawBitmap(spinUp!,up.X,up.Y);
        if(SpinDownRect is {IsEmpty:false} down)canvas.DrawSurface(spinDown,down.X,down.Y);
        // 有圖鈕時由 PaintButtons 畫；否則畫引擎自訂文字鈕。
        if(OkButton is not {Drawn:true}){var s=SubmitRect;if(!s.IsEmpty)canvas.DrawText("確定",s.X+4,s.Y+1,CanSubmit?LinkColor:DisabledColor);}
        if(CancelButton is not {Drawn:true}&&CancelRect is {IsEmpty:false} c)canvas.DrawText("取消",c.X+4,c.Y+1,LinkColor);
    }
    /// <summary>R9-E：OK／Cancel 圖鈕（只在有數字框時，按住時畫 1115／1112）。</summary>
    void PaintButtons(UiCanvas canvas){
        if(!HasNumber)return;
        if(OkButton is {Drawn:true} ok){var r=At(ok);canvas.DrawSurface(pressed=="submit"&&okDown!=null?okDown:ok.Surface,r.X,r.Y);}
        if(CancelButton is {Drawn:true} c){var r=At(c);canvas.DrawSurface(pressed=="cancel"&&cancelDown!=null?cancelDown:c.Surface,r.X,r.Y);}
    }
    // 名稱框外觀同數字框（引擎自訂框＋「確定」／「取消」文字鈕；原版 InputWindow 的外觀沒有在本窗查證）。
    void PaintName(UiCanvas canvas){
        if(view?.Name==null)return;
        var box=NameRect;
        using(var back=new SolidBrush(Color.Black))canvas.Graphics.FillRectangle(back,box);
        using(var pen=new Pen(NameFocused?LinkColor:DisabledColor))canvas.Graphics.DrawRectangle(pen,box.X,box.Y,box.Width-1,box.Height-1);
        canvas.DrawText(NameInput+(NameFocused?"_":""),box.X+3,box.Y+1,TextColor);
        var s=NameSubmitRect;canvas.DrawText("確定",s.X+4,s.Y+1,CanSubmitName?LinkColor:DisabledColor);
        var c=NameCancelRect;canvas.DrawText("取消",c.X+4,c.Y+1,LinkColor);
    }

    void ScrollBy(int delta)=>Scroll=Math.Clamp(Scroll+delta,0,MaxScroll);

    string? HitKey(Point p){
        if(CloseRect.Contains(p))return "close";
        if(At(Up).Contains(p))return "up";
        if(At(Down).Contains(p))return "down";
        if(InputRect.Contains(p))return "input";
        if(SpinUpRect.Contains(p))return "spin-up";
        if(SpinDownRect.Contains(p))return "spin-down";
        if(SubmitRect.Contains(p))return "submit";
        if(CancelRect.Contains(p))return "cancel";
        if(NameRect.Contains(p))return "name-input";
        if(NameSubmitRect.Contains(p))return "name-submit";
        if(NameCancelRect.Contains(p))return "name-cancel";
        for(int r=0;r<VisibleLines;r++){
            int index=Scroll+r;if(index>=rows.Count)break;
            if(rows[index] is {Kind:RowKind.Link,Enabled:true} link&&LineRect(r).Contains(p))return "link:"+link.LinkId;
        }
        // R9-F：行內連結（字或圖），只認本文區內、可按的。
        if(Document!=null&&TextArea.Contains(p))
            foreach(var (screen,line) in RichInView())foreach(var piece in line.Pieces)
                if(piece.Link>=0&&PieceRect(screen,piece).Contains(p)&&LinkEnabled(piece.Link))return "link:"+piece.Link;
        return null;
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        bool inside=Bounds.Contains(e.Location);
        // R10-B：每個滑鼠訊息先做 hover 命中測試（同原版 sub_871D60 → sub_871260，FU11§1 #7）。
        HoverLink=HoverAt(e.Location);
        switch(e.Kind){
            case UiMouseKind.Wheel:
                if(!inside)return false;
                if(e.Delta!=0){ScrollBy(-Math.Sign(e.Delta));HoverLink=HoverAt(e.Location);}
                return true;
            case UiMouseKind.Down:
                if(e.Button!=MouseButtons.Left)return inside;
                pressed=HitKey(e.Location);
                if(inside){InputFocused=pressed is "input" or "spin-up" or "spin-down"&&view.Number is {Answerable:true};NameFocused=pressed=="name-input"&&view.Name!=null;}
                return inside;
            case UiMouseKind.Up:{
                var was=pressed;pressed=null;
                if(was==null||HitKey(e.Location)!=was)return inside||was!=null;
                Activate(was);
                return true;
            }
            default:return inside||pressed!=null;
        }
    }

    void Activate(string key){
        if(view is not {} shown)return;
        switch(key){
            case "close":Close();break;
            case "up":ScrollBy(-1);break;
            case "down":ScrollBy(1);break;
            case "submit":Submit();break;
            case "spin-up":Step(1);break;
            case "spin-down":Step(-1);break;
            case "cancel":
                // R9-E：城堡頁回 CancelNumber（原有）；S136 數量頁的 Cancel 圖鈕關窗（推測）。
                if(shown.Number is {Kind:HyperTextNumberKind.Castle})Host?.CancelNumber(shown);else if(shown.Number!=null)Close();
                break;
            case "name-submit":SubmitName();break;
            case "name-cancel":CancelName();break;
            default:
                if(key.StartsWith("link:",StringComparison.Ordinal)&&int.TryParse(key.AsSpan(5),out int id)&&shown.Links.Any(l=>l.Id==id&&l.Enabled))Host?.Link(shown,id);
                break;
        }
    }

    void Submit(){
        if(view is not {} shown||!CanSubmit)return;
        InputFocused=false;
        Host?.Submit(shown,long.Parse(Input));
    }
    void SubmitName(){
        if(view is not {} shown||!CanSubmitName)return;
        NameFocused=false;
        Host?.SubmitName(shown,NameInput.Trim());
    }
    void CancelName(){
        if(view is not {Name:not null} shown)return;
        NameFocused=false;
        Host?.CancelName(shown);
    }

    /// <summary>
    /// 數字框有焦點時：Backspace 刪一字、Enter 送出（並放開焦點，下一個 Enter 照常開聊天）、↑／↓ 同上下鈕。其他鍵不吃。
    /// R7-D 名稱框有焦點時：Backspace 刪一字、Enter 確定、Esc 取消。R9-E：其餘情況窗開著時 Esc 關窗。
    /// </summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view!=null&&NameFocused&&view.Name!=null){
            if(!e.Down)return false;
            if(e.KeyData==Keys.Back){if(NameInput.Length>0)NameInput=NameInput[..^1];return true;}
            if(e.KeyData==Keys.Enter){if(!e.Repeat){if(CanSubmitName)SubmitName();else NameFocused=false;}return true;}
            if(e.KeyData==Keys.Escape){if(!e.Repeat)CancelName();return true;}
            return false;
        }
        if(view==null)return false;
        // R9-E：ESC（27）關窗（sub_875F80，已驗證）；名稱框有焦點時上面先處理成 CancelName。
        if(e.KeyData==Keys.Escape){if(!e.Down)return false;if(!e.Repeat)Close();return true;}
        if(!InputFocused||view.Number is not {Answerable:true})return false;
        if(!e.Down)return false;
        if(e.KeyData==Keys.Back){if(Input.Length>0)Input=Input[..^1];return true;}
        if(e.KeyData==Keys.Enter){if(!e.Repeat){if(CanSubmit)Submit();else InputFocused=false;}return true;}
        // R9-E：4203／4205 等同上下鈕（推測是方向鍵）。
        if(e.KeyData==Keys.Up){Step(1);return true;}
        if(e.KeyData==Keys.Down){Step(-1);return true;}
        return false;   // 數字走 WM_CHAR → Char()
    }

    /// <summary>
    /// R9-E：上下鈕 +1／−1（sub_872DC0）：超過上限回下限、低於下限回上限。空白時 +1 從下限開始、−1 從上限開始（推測）。
    /// </summary>
    public void Step(int delta){
        if(view?.Number is not {Answerable:true} n||delta==0)return;
        long lo=SpinMin(n),hi=TypeMax(n);
        long v;
        if(!long.TryParse(Input,out long cur))v=delta>0?lo:hi;
        else{v=cur+Math.Sign(delta);if(v>hi)v=lo;else if(v<lo)v=delta<0?hi:lo;}
        Input=v.ToString();
    }

    /// <summary>WM_CHAR：數字框有焦點時只收 0–9，逐位夾在 [1, N]（R9-E）；其他字不吃，交給聊天列。</summary>
    public bool Char(char c){
        // R7-D 名稱框：收一般字（控制字元與空白不收，C227 的名稱不可含空白），最多 16 個 Big5 位元組；遊戲字集不能表示的字不收。
        if(view!=null&&NameFocused&&view.Name!=null){
            if(char.IsControl(c)||char.IsWhiteSpace(c))return true;
            if(NameBytes(NameInput+c)<=NameMaxBytes)NameInput+=c;
            return true;
        }
        if(view==null||!InputFocused||view.Number is not {Answerable:true} n||c is <'0' or >'9')return false;
        // R9-E：逐位輸入並夾在 [1, N]（sub_872BE0，已驗證）；用 decimal 算避免 long 溢位。
        decimal next=(long.TryParse(Input,out long cur)?cur:0)*10m+(c-'0');
        Input=((long)Math.Clamp(next,1m,TypeMax(n))).ToString();
        return true;
    }

    public bool IsDragHandle(Point location)=>view!=null&&At(TitleBar).Contains(location)&&!CloseRect.Contains(location);
    public void MoveTo(Point location){Origin=new(location.X-local.X,location.Y-local.Y);}
}
