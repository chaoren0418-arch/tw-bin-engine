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
//   - 連結各占一列（引擎的對話模板把 HTML 連結拆成選項，原版是行內連結）；數字框畫成引擎自訂的框＋「確定」／「取消」文字鈕
//     （原版數字框的外觀與位置沒有查證，沒有對應 surf，不畫替代圖）。
//   - 捲動範圍 0..(行數−16)；滾輪、上下鈕步長 1 列。
// 待驗證（不做）：ESC 是否關窗；原版連結的滑鼠移入效果；HTML 的顏色／圖片標籤；數字框的上下調整鈕。

/// <summary>頁上的一個連結／選項（Id＝引擎對話模板的選項 id）。</summary>
public sealed record HyperTextLink(int Id,string Label,bool Enabled);
public enum HyperTextSource { Npc, Query }
public enum HyperTextNumberKind { Amount, Castle }
/// <summary>頁內數字框：S136（Amount，回 C11）或城堡頁（Castle，回 C19／C56／C44，可取消）。Revision＝詢問版本。</summary>
public sealed record HyperTextNumberBox(HyperTextNumberKind Kind,long Revision,long Min,long Max,long Initial,bool Answerable,bool CanSend,string Range);
/// <summary>要顯示的一頁。Revision＝NPC 頁的 InteractionRevision 或查詢頁的 QueryRevision（宿主用來擋過期操作）。</summary>
public sealed record HyperTextView(HyperTextSource Source,int Revision,string Text,IReadOnlyList<HyperTextLink> Links,string Notice,HyperTextNumberBox? Number);

/// <summary>面板對外的動作。面板不碰 LoginConnection；過期、走路中、重複送出由宿主（NpcUi.cs）擋。</summary>
public interface IHyperTextHost {
    void Link(HyperTextView shown,int option);
    void Submit(HyperTextView shown,long value);
    void CancelNumber(HyperTextView shown);
    /// <summary>關窗：同一個視窗裡的頁（含被取代的另一頁）都關掉。</summary>
    void Close(HyperTextView shown);
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
    /// <summary>數字框列：輸入框寬、按鈕寬（引擎自訂，推測）。</summary>
    public const int InputWidth=96,ButtonWidth=36,ButtonGap=6,MaxDigits=10;
    public static Color TextColor=>ChatPalette.Color(ChatFormat.White);
    public static Color LinkColor=>ChatPalette.Color(ChatFormat.Yellow);
    public static Color DisabledColor=>ChatPalette.Color(ChatFormat.Grey);
    const string BaseName="SURF_RENEWAL_WIN_HYPER_TEXT_BASE",FrameName="SURF_RENEWAL_WIN_HYPER_TEXT_FRAME",CloseName="SURF_RENEWAL_BTN_QUIT_X",
        ScrollName="SURF_RENEWAL_WIN_SCROLLBAR_LONG",UpName="SURF_RENEWAL_WIN_INC_BUT_OFF",DownName="SURF_RENEWAL_WIN_DEC_BUT_OFF";

    public sealed record Element(string Name,int Surf,Point Offset,UiSurface? Surface) {
        public Rectangle Local=>new(Offset,Surface==null?Size.Empty:new Size(Surface.Width,Surface.Height));
        public bool Drawn=>Surface!=null&&Surface.Width>0&&Surface.Height>0;
    }
    public enum RowKind { Text, Link, Notice, Range, Number }
    /// <summary>畫面上的一列（內容座標：第 Index 列）。Link 列帶選項 id。</summary>
    public sealed record Row(RowKind Kind,string Text,int LinkId=-1,bool Enabled=true);

    readonly Func<string,int> measure;
    public HyperTextPanel(Func<string,int>? measure=null){this.measure=measure??DefaultMeasure;}
    static readonly Lazy<Graphics> measureGraphics=new(()=>Graphics.FromImage(new Bitmap(1,1)));
    static int DefaultMeasure(string s){lock(measureGraphics)return UiText.Measure(measureGraphics.Value,s).Width;}

    public IHyperTextHost? Host{get;private set;}
    public void Attach(IHyperTextHost host)=>Host=host;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.Window;
    public Point Origin{get;private set;}
    public Rectangle Bounds=>Visible&&!local.IsEmpty?new(Origin.X+local.X,Origin.Y+local.Y,local.Width,local.Height):Rectangle.Empty;
    public bool Visible=>view!=null;
    public Element? Base{get;private set;}
    public Element? Frame{get;private set;}
    public Element? ScrollBar{get;private set;}
    public Element? Up{get;private set;}
    public Element? Down{get;private set;}
    public Element? CloseButton{get;private set;}
    public HyperTextView? View=>view;
    public IReadOnlyList<Row> Rows=>rows;
    public int Scroll{get;private set;}
    public int MaxScroll=>Math.Max(0,rows.Count-VisibleLines);
    /// <summary>數字框目前的字（只收數字）。</summary>
    public string Input{get;private set;}="";
    /// <summary>數字框有輸入焦點（點框或新詢問出現時；按 Enter 送出或點窗內其他地方時放開）。</summary>
    public bool InputFocused{get;private set;}

    HyperTextView? view;(HyperTextSource,int)? dismissed;long numberRevision=long.MinValue;
    List<Row> rows=[];Rectangle local;string? pressed;

    public void Load(UiLayout layout,SurfCatalog surfs)=>Load(layout,surfs.Get);
    internal void Load(UiLayout layout,Func<int,UiSurface?> surf){
        var window=layout.Root.Child(Section);
        Element Find(string name,bool atOrigin=false){
            var matches=window.Children.Where(c=>c.Attr("surfName")==name).Take(2).ToList();
            if(matches.Count!=1)throw new UiLayoutException(layout.FileName,window.Path+"/@surfName="+name,matches.Count==0?"缺少元件":"元件不唯一");
            var n=matches[0];int[] ids=n.Surfs();
            if(ids.Length==0)throw new UiLayoutException(layout.FileName,n.Path,"surfNumber 空白");
            var at=atOrigin&&n.Attr("xPos")==null&&n.Attr("yPos")==null?Point.Empty:new Point(n.Int("xPos"),n.Int("yPos"));
            return new(name,ids[0],at,surf(ids[0]));
        }
        Base=Find(BaseName);Frame=Find(FrameName,atOrigin:true);ScrollBar=Find(ScrollName);Up=Find(UpName);Down=Find(DownName);CloseButton=Find(CloseName);
        local=new[]{Base,Frame,ScrollBar,Up,Down,CloseButton}.Where(e=>e.Drawn).Select(e=>e.Local).Append(TitleBar).Aggregate(Rectangle.Empty,(a,r)=>a.IsEmpty?r:Rectangle.Union(a,r));
        Origin=new(window.Int("x"),window.Int("y"));
        view=null;rows=[];Scroll=0;pressed=null;dismissed=null;Input="";InputFocused=false;numberRevision=long.MinValue;
    }

    /// <summary>
    /// 宿主每格送目前要顯示的頁（null＝沒有頁）。換頁（來源或版本不同）時捲回頂端；同一頁只更新內容（例如可送出狀態）。
    /// 被使用者或堆疊關掉的那一頁，在宿主真的關掉它之前不再打開。
    /// </summary>
    public void Show(HyperTextView? next){
        if(next==null){view=null;pressed=null;InputFocused=false;dismissed=null;return;}
        var key=(next.Source,next.Revision);
        if(dismissed==key)return;
        dismissed=null;
        bool newPage=view==null||(view.Source,view.Revision)!=key;
        view=next;
        if(next.Number is {} n&&n.Revision!=numberRevision){numberRevision=n.Revision;Input=n.Answerable?n.Initial.ToString():"";InputFocused=n.Answerable;}
        else if(next.Number==null){numberRevision=long.MinValue;Input="";InputFocused=false;}
        rows=BuildRows(next);
        if(newPage){Scroll=0;pressed=null;}
        Scroll=Math.Clamp(Scroll,0,MaxScroll);
    }

    /// <summary>關閉鈕與堆疊滿（UiRoot.Evict）共用：請宿主關掉頁，面板先藏起來。</summary>
    public void Close(){
        if(view is not {} shown)return;
        dismissed=(shown.Source,shown.Revision);view=null;pressed=null;InputFocused=false;
        Host?.Close(shown);
    }

    public void Update(IUiGameState state){if(!state.InWorld)Show(null);}

    List<Row> BuildRows(HyperTextView v){
        var list=new List<Row>();
        foreach(var line in Wrap(v.Text))list.Add(new(RowKind.Text,line));
        if(v.Links.Count>0&&list.Count>0)list.Add(new(RowKind.Text,""));
        foreach(var link in v.Links)foreach(var line in Wrap(link.Label))list.Add(new(RowKind.Link,line,link.Id,link.Enabled));
        if(v.Number is {} n){list.Add(new(RowKind.Text,""));list.Add(new(RowKind.Range,n.Range));list.Add(new(RowKind.Number,""));}
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
    public Rectangle SubmitRect=>NumberIndex<0?Rectangle.Empty:NumberPart(InputWidth+ButtonGap,ButtonWidth);
    public Rectangle CancelRect=>view?.Number is {Kind:HyperTextNumberKind.Castle}&&NumberIndex>=0?NumberPart(InputWidth+ButtonGap*2+ButtonWidth,ButtonWidth):Rectangle.Empty;
    public IReadOnlyList<Row> VisibleRows=>rows.Skip(Scroll).Take(VisibleLines).ToArray();
    /// <summary>目前輸入可以送出：有詢問、可答、宿主說可送，且值在範圍內。</summary>
    public bool CanSubmit=>view?.Number is {Answerable:true,CanSend:true} n&&long.TryParse(Input,out long v)&&v>=Math.Max(1,n.Min)&&v<=n.Max;

    public void Paint(UiCanvas canvas){
        if(view==null)return;
        foreach(var e in new[]{Base,Frame,ScrollBar,Up,Down,CloseButton})
            if(e is {Drawn:true})canvas.DrawSurface(e.Surface,Origin.X+e.Offset.X,Origin.Y+e.Offset.Y);
        var visible=VisibleRows;
        for(int r=0;r<visible.Count;r++){
            var row=visible[r];var p=LineAt(r);
            switch(row.Kind){
                case RowKind.Number:PaintNumber(canvas);break;
                case RowKind.Link:canvas.DrawText(row.Text,p.X,p.Y,row.Enabled?LinkColor:DisabledColor);break;
                case RowKind.Notice:case RowKind.Range:canvas.DrawText(row.Text,p.X,p.Y,DisabledColor);break;
                default:canvas.DrawText(row.Text,p.X,p.Y,TextColor);break;
            }
        }
    }
    void PaintNumber(UiCanvas canvas){
        if(view?.Number is not {} n)return;
        var box=InputRect;
        using(var back=new SolidBrush(Color.Black))canvas.Graphics.FillRectangle(back,box);
        using(var pen=new Pen(InputFocused?LinkColor:DisabledColor))canvas.Graphics.DrawRectangle(pen,box.X,box.Y,box.Width-1,box.Height-1);
        canvas.DrawText(Input+(InputFocused?"_":""),box.X+3,box.Y+1,n.Answerable?TextColor:DisabledColor);
        var s=SubmitRect;canvas.DrawText("確定",s.X+4,s.Y+1,CanSubmit?LinkColor:DisabledColor);
        if(CancelRect is {IsEmpty:false} c)canvas.DrawText("取消",c.X+4,c.Y+1,LinkColor);
    }

    void ScrollBy(int delta)=>Scroll=Math.Clamp(Scroll+delta,0,MaxScroll);

    string? HitKey(Point p){
        if(CloseRect.Contains(p))return "close";
        if(At(Up).Contains(p))return "up";
        if(At(Down).Contains(p))return "down";
        if(InputRect.Contains(p))return "input";
        if(SubmitRect.Contains(p))return "submit";
        if(CancelRect.Contains(p))return "cancel";
        for(int r=0;r<VisibleLines;r++){
            int index=Scroll+r;if(index>=rows.Count)break;
            if(rows[index] is {Kind:RowKind.Link,Enabled:true} link&&LineRect(r).Contains(p))return "link:"+link.LinkId;
        }
        return null;
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands){
        if(view==null)return false;
        bool inside=Bounds.Contains(e.Location);
        switch(e.Kind){
            case UiMouseKind.Wheel:
                if(!inside)return false;
                if(e.Delta!=0)ScrollBy(-Math.Sign(e.Delta));
                return true;
            case UiMouseKind.Down:
                if(e.Button!=MouseButtons.Left)return inside;
                pressed=HitKey(e.Location);
                if(inside)InputFocused=pressed=="input"&&view.Number is {Answerable:true};
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
            case "cancel":Host?.CancelNumber(shown);break;
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

    /// <summary>數字框有焦點時：Backspace 刪一字、Enter 送出（並放開焦點，下一個 Enter 照常開聊天）。其他鍵不吃。</summary>
    public bool Key(UiKeyEvent e,IUiCommands commands){
        if(view==null||!InputFocused||view.Number is not {Answerable:true})return false;
        if(!e.Down)return false;
        if(e.KeyData==Keys.Back){if(Input.Length>0)Input=Input[..^1];return true;}
        if(e.KeyData==Keys.Enter){if(!e.Repeat){if(CanSubmit)Submit();else InputFocused=false;}return true;}
        return false;   // 數字走 WM_CHAR → Char()
    }

    /// <summary>WM_CHAR：數字框有焦點時只收 0–9（最多 10 位）；其他字不吃，交給聊天列。</summary>
    public bool Char(char c){
        if(view==null||!InputFocused||view.Number is not {Answerable:true}||c is <'0' or >'9')return false;
        if(Input.Length<MaxDigits)Input=Input=="0"?c.ToString():Input+c;
        return true;
    }

    public bool IsDragHandle(Point location)=>view!=null&&At(TitleBar).Contains(location)&&!CloseRect.Contains(location);
    public void MoveTo(Point location){Origin=new(location.X-local.X,location.Y-local.Y);}
}
