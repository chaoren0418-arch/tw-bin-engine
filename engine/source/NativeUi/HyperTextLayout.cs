using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
namespace P06.NativeUi;

// R9-F（T366／#850）：HYPER_TEXT_WINDOW 的行內排版器。依據 docs/reference/followups-9.md 第 10.1 節（FU9§10）、第 16.2 節 R9-F。
// 已驗證（引用 FU9§10，sub_8676F0 的 HTML 解析與排版）：
//   - 標籤表 21 個：body a b i p left center right justify scatter br font img var name username time date may subHtml u。
//   - <font fg bg face Size>：fg／bg 是十六進位數字，不收 0x／#（收到就當成無效、不改顏色）。
//   - 每個 <a> 開始時文字色設成常數 0x00B0FFFF；連結排在文字流裡（行內），不是一列一個。
// R10-B（T383／#891）：依 docs/reference/followups-11.md §1、§2（FU11 D1–D7）修正顏色。已驗證（引用 FU11§1）：
//   - 顏色位元組順序：fg／bg 的數值原封不動當 GDI COLORREF（0x00BBGGRR）（FU11§1 #1、#2，sub_864530、sub_866070 的 SetTextColor／
//     sub_86E960 的 CreateSolidBrush）。→ HyperTextColors.Order 預設 Bgr；連結常數 0x00B0FFFF＝RGB(FF,FF,B0) 淡黃（#5）。
//   - 沒有 fg 時的預設字色 0x00EEEDEE＝RGB(EE,ED,EE)（CTextAttr 建構子 sub_862970，#4）。
//   - 帶 link／action／url 屬性的 <a> 也設底線旗標（0x867B72），</a> 清掉（#13）。
//   - fg=""（空字串）＝有效、值 0（黑）（sub_864530，#15）。
//   - <img src="#N"|"pN" align valign link|action|url tooltip highlight>：#N 走 SurfList（312→1109、314→1107、331→601 換號）、
//     pN 走 PngList；有 link／action 的圖可點。
//   - <var src="#N">：N 是 S39 帶來的第 N 個資料字串，先 $ 展開、再換第一個 ^（換成空白，同 S161 的 sub_941E40）。
// 推測（真機再確認，M34）：
//   - 顏色位元組順序已由 FU11 靜態驗證為 COLORREF（見上）；M34 只需確認 GDI 畫面貼到螢幕時沒有再換通道。
//   - <p align>／</p>／<center> 是段落：前面有字就換行，並設對齊；<br> 一律換行（可產生空行）。原始檔裡的 \r\n 不算換行（模板都用 <br>）。
//   - justify／scatter 先當左對齊排（沒有追分散對齊的字距算法）；<p lgap agap>、<img align valign highlight>、face、Size 只存資料。
//   - <b> 以 1 px 位移重畫一次模擬粗體；<i> 只存資料（UiText 只有一套字型）；<u> 畫底線。bg 畫在字的範圍底下。
//   - <var textwidth="W">：這段字至少占 W px（不足補空白）；<var img>：資料字串是圖號時畫成 SurfList 圖，否則照字顯示。
//   - 同一列有圖時列高＝最高的圖（至少 15），字與圖都靠上。每列開頭的空白去掉（HTML 的空白收合，簡化版）。
//   - pN（PngList）圖的載入沒有接：當成沒有圖。沒有圖的可點圖改畫成文字標籤（「購買」／「出售」／alt；引擎自訂，避免功能消失）；
//     沒有圖、不可點的圖不畫（缺圖就不畫）。
//   - N ≥ 1000 的客戶端狀態變數：Q9-D（T439／#1010）起，引擎有設定的號碼輸出 567／568 或 573／574（HyperTextOptionVars）；
//     其他與 NpcTemplates 相同顯示「缺少參數」。<may>／<subHtml> 不輸出。
// Q9-C（T438／#1009）：依 docs/reference/followups-17.md §3.2、§3.4（已驗證）：<date>＝「%2d%s %2d%s」（月＋字串 762＋日＋字串 763）、
//   <time>＝「%02d:%02d」、<var src="#999">＝「%2d:%2d」、<var src="#998">＝玩家名稱；時間值經 sub_A23760 的 0x80000000 規則換算（HyperTextClock）。
// 選項編號：與 NpcTemplates.Render 完全相同（每個關閉的 <a> 與每個帶 link／action 的 <img> 依出現順序 0,1,2…），面板據此把
//   HyperTextView.Links 的 id 對到行內位置；對不上（數量或文字不同）就退回舊的「連結各占一列」排法。

/// <summary>段落對齊（標籤表的 left／center／right／justify／scatter）。</summary>
public enum HyperTextAlign { Left, Center, Right, Justify, Scatter }
/// <summary>fg／bg 數值的位元組順序。Bgr＝GDI COLORREF 0x00BBGGRR（FU11 已驗證，預設）；Rgb 只留作對照。</summary>
public enum HyperTextColorOrder { Rgb, Bgr }
public enum HyperTextNodeKind { Text, Image, Break, Paragraph }

public static class HyperTextColors {
    /// <summary>每個 &lt;a&gt; 開始時的文字色常數（sub_8676F0 0x867D3B，已驗證）＝COLORREF→RGB(FF,FF,B0) 淡黃。</summary>
    public const uint LinkValue=0x00B0FFFF;
    /// <summary>沒有 fg 時的預設字色（CTextAttr 建構子 sub_862970 0x8629C6，FU11§1 #4，已驗證）＝RGB(EE,ED,EE)。</summary>
    public const uint DefaultValue=0x00EEEDEE;
    /// <summary>滑鼠下的文字連結色（sub_866070 0x866787–0x8667A4，FU11§1 #7，已驗證）＝RGB(0A,F0,32) 亮綠。</summary>
    public const uint HoverValue=0x0032F00A;
    /// <summary>底線色（sub_866070 0x866B16，FU11§1 #12，已驗證）＝RGB(6E,6E,46)，固定、不跟字色。</summary>
    public const uint UnderlineValue=0x00466E6E;
    /// <summary>底線最多畫到本文左界＋198（FU11§1 #12，已驗證）。</summary>
    public const int UnderlineMaxX=198;
    /// <summary>位元組順序：已驗證（FU11）＝COLORREF（Bgr）。</summary>
    public static HyperTextColorOrder Order{get;set;}=HyperTextColorOrder.Bgr;
    public static Color Decode(uint value,HyperTextColorOrder? order=null){
        byte hi=(byte)(value>>16),mid=(byte)(value>>8),lo=(byte)value;
        return (order??Order)==HyperTextColorOrder.Rgb?Color.FromArgb(255,hi,mid,lo):Color.FromArgb(255,lo,mid,hi);
    }
    public static Color Link=>Decode(LinkValue);
    public static Color Default=>Decode(DefaultValue);
    public static Color Hover=>Decode(HoverValue);
    public static Color Underline=>Decode(UnderlineValue);
    /// <summary>
    /// fg／bg 屬性：只收十六進位數字（不收 0x／#）；無效回 null（不改顏色）；沒有屬性（null）回 null。
    /// 空字串＝有效、值 0（黑）（sub_864530，FU11§1 #15，已驗證）。
    /// 引擎差異（邊角，維持）：先去頭尾空白；超過 6 位取低 24 位、超過 8 位當無效（原版是 32 位元溢位、不截位，FU11 D7）。
    /// </summary>
    public static uint? Parse(string? text){
        if(text==null)return null;
        if(text.Length==0)return 0;
        string s=text.Trim();
        if(s.Length is 0 or >8||!uint.TryParse(s,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out uint v))return null;
        return v&0xFFFFFF;
    }
}

/// <summary>字的樣式。Fg／Bg＝原始數值（未解碼）。</summary>
public sealed record HyperTextStyle(uint? Fg=null,uint? Bg=null,string? Face=null,int? Size=null,bool Bold=false,bool Italic=false,bool Underline=false) {
    public static readonly HyperTextStyle Plain=new();
}

/// <summary>
/// 文件的一個節點。Raw＝NpcTemplates 對同一段輸出的純文字（用來核對與 $ 展開）；Text＝畫面上要畫的字。
/// Link＝選項 id（-1＝不可點）；Soft＝段落換行（前面沒字就不換）。圖：Surf＝SurfList 換號後的圖號，Png＝pN。
/// </summary>
public sealed record HyperTextNode(HyperTextNodeKind Kind,string Raw,string Text,HyperTextStyle Style,int Link=-1,HyperTextAlign Align=HyperTextAlign.Left,
    int MinWidth=0,int? Surf=null,int? Png=null,string Src="",string Tooltip="",string Label="",bool Soft=false);

/// <summary>解析後的一頁。Plain＝與 NpcTemplates.Render 的 Text 相同的純文字；LinkCount＝選項數。</summary>
public sealed class HyperTextDocument {
    public IReadOnlyList<HyperTextNode> Nodes{get;}
    public string Plain{get;}
    public int LinkCount{get;}
    public HyperTextDocument(IReadOnlyList<HyperTextNode> nodes,int linkCount){Nodes=nodes;LinkCount=linkCount;Plain=HyperTextMarkup.Normalize(string.Concat(nodes.Select(n=>n.Raw)));}

    static readonly Regex token=new(@"\$(\d+)",RegexOptions.Compiled);
    /// <summary>
    /// 把文件對到畫面要顯示的文字 shown（宿主會先做 $ 名稱展開）。Plain 相同 → 原文件；Plain 裡有 $N 時依 shown 對出每個 $N 的展開值，
    /// 套到各節點後再核對一次；對不上回 null（面板退回舊排法）。
    /// </summary>
    public HyperTextDocument? ResolveTo(string shown){
        if(Plain==shown)return this;
        var tokens=token.Matches(Plain);
        if(tokens.Count==0)return null;
        var pattern=new StringBuilder("^");int at=0;
        foreach(Match m in tokens){pattern.Append(Regex.Escape(Plain[at..m.Index])).Append("(.*?)");at=m.Index+m.Length;}
        pattern.Append(Regex.Escape(Plain[at..])).Append('$');
        Match hit;
        try{hit=Regex.Match(shown,pattern.ToString(),RegexOptions.Singleline,TimeSpan.FromMilliseconds(50));}catch(RegexMatchTimeoutException){return null;}
        if(!hit.Success)return null;
        var map=new Dictionary<string,string>();
        for(int i=0;i<tokens.Count;i++)map.TryAdd(tokens[i].Value,hit.Groups[i+1].Value);
        string Sub(string s)=>token.Replace(s,m=>map.TryGetValue(m.Value,out var v)?v:m.Value);
        var resolved=new HyperTextDocument(Nodes.Select(n=>n with{Raw=Sub(n.Raw),Text=Sub(n.Text),Label=Sub(n.Label),Tooltip=Sub(n.Tooltip)}).ToArray(),LinkCount);
        return resolved.Plain==shown?resolved:null;
    }
}

/// <summary>HTML → HyperTextDocument。標記只當資料；參數只插進文字，不再當標記解析（同 NpcTemplates）。</summary>
public static class HyperTextMarkup {
    // 與 NpcTemplates 相同的切法，保證純文字與選項編號一致。
    static readonly Regex tags=new(@"(<[^>]*>)",RegexOptions.Compiled);
    static readonly Regex attributes=new("([a-zA-Z]+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))",RegexOptions.Compiled);
    static readonly Regex tagName=new(@"^<\s*(/?[a-zA-Z]+)",RegexOptions.Compiled);
    internal static string Normalize(string raw)=>Regex.Replace(raw,@"\n\s*\n\s*\n","\n\n").Trim();
    static Dictionary<string,string> Attr(string token){var a=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(Match m in attributes.Matches(token))a[m.Groups[1].Value]=WebUtility.HtmlDecode(m.Groups[2].Success?m.Groups[2].Value:m.Groups[3].Success?m.Groups[3].Value:m.Groups[4].Value);return a;}
    /// <summary>沒有值的屬性（例如 &lt;var img src="#0"&gt; 的 img）。</summary>
    static bool Flag(string token,string name){
        string inner=Regex.Replace(token,"\"[^\"]*\"|'[^']*'","\"\"");
        inner=tagName.Replace(inner,"");
        return Regex.IsMatch(inner,@"(^|\s)"+name+@"(\s|/?>|$)",RegexOptions.IgnoreCase)&&!Regex.IsMatch(inner,@"(^|\s)"+name+@"\s*=",RegexOptions.IgnoreCase);
    }
    /// <summary>SurfList 換號（FU9§10，已驗證的三個）；其他照原號（推測）。</summary>
    public static int SurfList(int n)=>n switch{312=>1109,314=>1107,331=>601,_=>n};
    /// <summary>align 屬性值 → 對齊；不認得的（例如模板裡的 "lef"）回 Left。</summary>
    public static HyperTextAlign ParseAlign(string? value)=>(value??"").Trim().ToLowerInvariant() switch{
        "center"=>HyperTextAlign.Center,"right"=>HyperTextAlign.Right,"justify"=>HyperTextAlign.Justify,"scatter"=>HyperTextAlign.Scatter,_=>HyperTextAlign.Left};
    /// <summary>var 資料字串：換第一個 ^ 成空白（以字為單位，DBCS 安全；原版用位元組 strchr）。</summary>
    public static string ReplaceFirstCaret(string s){int i=s.IndexOf('^');return i<0?s:s[..i]+" "+s[(i+1)..];}
    static string Display(string s)=>s.Replace("\r","").Replace("\n","").Replace('\t',' ');

    sealed record FontEntry(HyperTextStyle Style,bool SetsFg);

    public static HyperTextDocument Parse(string html,string[] args,string npc,string player){
        var nodes=new List<HyperTextNode>();int links=0;
        var fonts=new Stack<FontEntry>();int bold=0,italic=0,underline=0;
        Dictionary<string,string>? anchor=null;List<int> anchorNodes=[];int anchorFontDepth=0;
        var align=HyperTextAlign.Left;var pAlign=new Stack<HyperTextAlign>();
        HyperTextStyle Style(){
            var top=fonts.Count>0?fonts.Peek().Style:HyperTextStyle.Plain;
            uint? fg=top.Fg;
            if(anchor!=null){
                // 連結色在 <a> 開始時設定；<a> 之後再開的 <font fg> 才蓋過它。
                fg=HyperTextColors.LinkValue;
                foreach(var f in fonts.Take(Math.Max(0,fonts.Count-anchorFontDepth)))if(f.SetsFg){fg=f.Style.Fg;break;}
            }
            // R10-B：帶 link／action／url 的 <a> 也有底線（0x867B72，FU11§1 #13）。
            bool anchorLine=anchor!=null&&(anchor.ContainsKey("link")||anchor.ContainsKey("action")||anchor.ContainsKey("url"));
            return top with{Fg=fg,Bold=bold>0,Italic=italic>0,Underline=underline>0||anchorLine};
        }
        void Add(HyperTextNode n){if(anchor!=null&&n.Kind is HyperTextNodeKind.Text or HyperTextNodeKind.Image&&n.Link<0)anchorNodes.Add(nodes.Count);nodes.Add(n);}
        void Text(string raw,string? text=null,int minWidth=0){if(raw.Length==0&&minWidth==0)return;Add(new(HyperTextNodeKind.Text,raw,Display(text??raw),Style(),Align:align,MinWidth:minWidth));}
        void Break(bool soft,string raw){nodes.Add(new(HyperTextNodeKind.Break,raw,"",HyperTextStyle.Plain,Align:align,Soft:soft));}
        foreach(string part in tags.Split(html)){
            if(!part.StartsWith('<')){Text(WebUtility.HtmlDecode(part));continue;}
            string tag=tagName.Match(part).Groups[1].Value.ToLowerInvariant();var a=Attr(part);
            switch(tag){
            case "a":
                // NpcTemplates：<a> 沒關又開新的 → 前一個不成選項（它的字維持不可點）。
                anchor=a;anchorNodes=[];anchorFontDepth=fonts.Count;break;
            case "/a":
                if(anchor!=null){int id=links++;foreach(int i in anchorNodes)nodes[i]=nodes[i] with{Link=id};anchor=null;anchorNodes=[];}
                break;
            case "img":{
                string src=a.GetValueOrDefault("src","").Trim();int? surf=null,png=null;
                if(src.StartsWith('#')&&int.TryParse(src.AsSpan(1),out int n)&&n>=0)surf=SurfList(n);
                else if(src.Length>1&&src[0] is 'p' or 'P'&&int.TryParse(src.AsSpan(1),out int pn)&&pn>=0)png=pn;
                int link=-1;string label="";
                if(a.ContainsKey("action")||a.ContainsKey("link")){
                    string cmd=a.GetValueOrDefault("action","");
                    label=(cmd switch{"buy"=>"購買","sell"=>"出售",_=>a.GetValueOrDefault("alt","原圖示 "+a.GetValueOrDefault("src",""))}).Trim();
                    link=links++;
                }
                Add(new(HyperTextNodeKind.Image,"","",Style(),link,align,Surf:surf,Png:png,Src:src,Tooltip:a.GetValueOrDefault("tooltip",""),Label:label));
                break;}
            case "var":{
                string src=a.GetValueOrDefault("src","");
                int minWidth=int.TryParse(a.GetValueOrDefault("textwidth",""),out int tw)&&tw>0?tw:0;
                string value;
                if(src.StartsWith('#')&&int.TryParse(src.AsSpan(1),out int i)&&i>=0){
                    // 原版 0x8676F0：缺 #0 時用目前對話對象的名字；明確給的空字串維持空白（同 NpcTemplates）。
                    // Q9-C：#998＝玩家名稱、#999＝「%2d:%2d」遊戲時間（客戶端變數，不讀參數；同 NpcTemplates）。
                    if(i==HyperTextClock.PlayerNameVar)value=player;
                    else if(i==HyperTextClock.ClockVar)value=HyperTextClock.VarTime(HyperTextClock.Value);
                    // Q9-D：1002–1029 引擎有設定的選項值（567／568、573／574）；沒有的照舊（同 NpcTemplates）。
                    else if(i>=1000&&HyperTextOptionVars.Value(i) is {} option)value=option;
                    else if(i<args.Length)value=args[i];else if(i==0&&npc.Length>0)value=npc;else value=$"〔缺少參數 #{i}〕";
                }else value="〔不支援的參數〕";
                if(Flag(part,"img")&&int.TryParse(value.Trim(),out int img)&&img>=0){Add(new(HyperTextNodeKind.Image,value,"",Style(),-1,align,Surf:SurfList(img),Src:"#"+img));break;}
                Text(value,ReplaceFirstCaret(value),minWidth);break;}
            case "name":Text(npc);break;
            case "username":Text(player);break;
            case "date":Text(HyperTextClock.Date(HyperTextClock.Value));break;   // Q9-C：case 17
            case "time":Text(HyperTextClock.Time(HyperTextClock.Value));break;   // Q9-C：case 16
            case "br":Break(false,"\n");break;
            case "p":
                Break(true,"\n");pAlign.Push(align);
                if(a.TryGetValue("align",out var pa))align=ParseAlign(pa);
                nodes.Add(new(HyperTextNodeKind.Paragraph,"","",HyperTextStyle.Plain,Align:align));break;
            case "/p":Break(true,"\n");align=pAlign.Count>0?pAlign.Pop():HyperTextAlign.Left;nodes.Add(new(HyperTextNodeKind.Paragraph,"","",HyperTextStyle.Plain,Align:align));break;
            case "center":Break(true,"");pAlign.Push(align);align=HyperTextAlign.Center;nodes.Add(new(HyperTextNodeKind.Paragraph,"","",HyperTextStyle.Plain,Align:align));break;
            case "/center":Break(true,"");align=pAlign.Count>0?pAlign.Pop():HyperTextAlign.Left;nodes.Add(new(HyperTextNodeKind.Paragraph,"","",HyperTextStyle.Plain,Align:align));break;
            case "font":{
                var cur=fonts.Count>0?fonts.Peek().Style:HyperTextStyle.Plain;
                uint? fg=HyperTextColors.Parse(a.GetValueOrDefault("fg")),bg=HyperTextColors.Parse(a.GetValueOrDefault("bg"));
                int? size=int.TryParse(a.GetValueOrDefault("size",""),out int sz)&&sz>0?sz:cur.Size;
                fonts.Push(new(cur with{Fg=fg??cur.Fg,Bg=bg??cur.Bg,Face=a.TryGetValue("face",out var face)&&face.Length>0?face:cur.Face,Size=size},fg!=null));
                break;}
            case "/font":if(fonts.Count>0)fonts.Pop();break;
            case "b":bold++;break;case "/b":bold=Math.Max(0,bold-1);break;
            case "i":italic++;break;case "/i":italic=Math.Max(0,italic-1);break;
            case "u":underline++;break;case "/u":underline=Math.Max(0,underline-1);break;
            default:break;   // body／html／pre／may／subHtml 等：不輸出（同 NpcTemplates）
            }
        }
        return new(nodes,links);
    }
}

/// <summary>
/// Q9-C（T438／#1009）：HTML 的 &lt;date&gt;／&lt;time&gt;／&lt;var src="#999"&gt; 用的遊戲時間（docs/reference/followups-17.md §3.2，已驗證）。
/// 換算 sub_A23760（FO 0x622B60）：值 &lt; 0x80000000 直接當 time_t；否則減 2019686400（正好 23376 日）、年份 +64；再交給 sub_A23F20（推測 gmtime，UTC）。
/// 時間來源：原版是全域 dword_154334C（寫入者推測是 S_TIME）。引擎取最後一個 S123（WorldSession.ServerTime）或 S8（Status.Time）的值，
///   由 NpcTemplates.cs 的觀察器記下目前的 session（推測；與 Ambience 同樣兩個來源，真機再確認）。都還沒收到時用 0（原版全域初值，推測）。
/// CodePage 1252 的 strftime("%b %d") 分支不做（本客戶端是 CP950）。
/// </summary>
public static class HyperTextClock {
    public const uint HighBase=0x80000000;
    public const long HighOffset=2019686400;
    public const int PlayerNameVar=998,ClockVar=999;
    /// <summary>字串表 762「月」、763「日」（§3.2）；沒有表（例如 CI）時用這兩個已驗證的字。</summary>
    public const int MonthString=762,DayString=763;
    public readonly record struct GameTime(int Year,int Month,int Day,int Hour,int Minute,int Second);
    /// <summary>sub_A23760 ＋ gmtime。</summary>
    public static GameTime Convert(uint value){
        bool high=value>=HighBase;
        var d=DateTime.UnixEpoch.AddSeconds(high?value-HighOffset:value);
        return new(d.Year+(high?64:0),d.Month,d.Day,d.Hour,d.Minute,d.Second);
    }
    static string Str(int id,string fallback)=>ServerMessageText.Table is {} t?(id<t.Count?t[id]:""):fallback;
    /// <summary>&lt;date&gt;（case 17）：「%2d%s %2d%s」＝月（寬 2）＋762＋空白＋日（寬 2）＋763。</summary>
    public static string Date(uint value){var g=Convert(value);return string.Create(CultureInfo.InvariantCulture,$"{g.Month,2}{Str(MonthString,"月")} {g.Day,2}{Str(DayString,"日")}");}
    /// <summary>&lt;time&gt;（case 16）：「%02d:%02d」（時:分）。</summary>
    public static string Time(uint value){var g=Convert(value);return string.Create(CultureInfo.InvariantCulture,$"{g.Hour:00}:{g.Minute:00}");}
    /// <summary>&lt;var src="#999"&gt;：「%2d:%2d」（時:分，空白補寬）。</summary>
    public static string VarTime(uint value){var g=Convert(value);return string.Create(CultureInfo.InvariantCulture,$"{g.Hour,2}:{g.Minute,2}");}

    static readonly object gate=new();
    static WeakReference<WorldSession>? session;static bool statusLast;
    /// <summary>Q9-D：最後收到 S123／S8 的 session（HyperTextOptionVars 讀選項值用）；沒有回 null。</summary>
    internal static WorldSession? Session{get{lock(gate)return session!=null&&session.TryGetTarget(out var s)?s:null;}}
    static Func<uint?>? overrideSource;static bool overridden;
    /// <summary>NpcTemplates.cs 的 S123／S8 觀察器呼叫：記下 session 與最後一個時間封包是哪一種。</summary>
    internal static void Observed(WorldSession s,bool status){lock(gate){session=new(s);statusLast=status;}}
    /// <summary>目前的遊戲時間原始值；還沒收到任何時間封包時是 null。</summary>
    public static uint? Seconds{get{
        WorldSession? s;bool status;
        lock(gate){
            if(overridden)return overrideSource?.Invoke();
            if(session==null||!session.TryGetTarget(out s))return null;
            status=statusLast;
        }
        uint? server=s.ServerTime,own=s.Status?.Time;
        return status?own??server:server??own;
    }}
    public static uint Value=>Seconds??0;
    /// <summary>測試用：換成指定的時間來源，直到 Dispose。</summary>
    internal static IDisposable Use(Func<uint?> source){
        lock(gate){var previous=(overridden,overrideSource);overridden=true;overrideSource=source;return new Scope(()=>{lock(gate)(overridden,overrideSource)=previous;});}
    }
    sealed class Scope(Action dispose):IDisposable{Action? action=dispose;public void Dispose(){Interlocked.Exchange(ref action,null)?.Invoke();}}
}

/// <summary>
/// Q9-D（T439／#1010）：HTML 的 &lt;var src="#N"&gt;，N 在 1002–1029（3.81 範圍）裡引擎已有對應設定的選項值
/// （docs/reference/followups-17.md §3.4，TW16 sub_435640 同號 case；3.81 快照也有這些 case）。
/// 開關 → 字串 567「開」／568「關」；1019（573／574 那一組）→ 573「可使用」／574「無法使用」。字串表沒有時用這幾個已驗證的字。
/// 對應（引擎設定 ← 原版全域變數）：
///   1002 getall      ← WorldSession.PickupAll（byte_135C1E4，world-objects.md 已驗證同一個旗標）
///   1003 bubbleon    ← HeadBubbleOptions.BubbleFrames（byte_13AAF28，FU16 已驗證）
///   1005 dowhisper   ← WorldSession.ChatChannels.Whisper（byte_135C1E6＝/whisper 旗標，FU7§1.4 已驗證）
///   1019 accepttrade ← WorldSession.TradeAskWindow（byte_136E99D，FU7§1.1 已驗證；573／574）
///   1020 dotradechat ← WorldSession.ChatChannels.Trade（unk_1656980＝/tradechat 旗標，FU7§1.4 已驗證）
///   1023 showhpmp    ← HpMpSettings.ShowCharacterHpMp（byte_138BE7C，ui-layout.md 已驗證）
///   1024 showfloathpmp ← HpMpSettings.Show（byte_138BE7D FloatingHPMP.show；它怎麼控制浮動窗是 ui-layout.md 的待驗證）
///   1027 showMargetMsg ← HeadBubbleOptions.ShopMessages（byte_136E99F，FU16 已驗證）
/// 其他號碼（1004 dochat 的 unk_1656948 沒追、1009 語言過濾引擎一律開、1025 透明度、1028 組隊分配等引擎沒有設定）回 null，
/// 呼叫端照舊顯示「缺少參數」。1030 以上、2001 以上是 TW16 專有，不做。
/// 來源：世界值取 HyperTextClock 記下的 session（最後收到 S123／S8 的那個；推測與 NPC 對話是同一個 session）；
/// HP/MP 設定只在原生 UI 的 HpMpPanel 裡，由 HpMp 掛上（沒掛＝null＝缺少參數）。
/// </summary>
public static class HyperTextOptionVars {
    public const int OnString=567,OffString=568,UsableString=573,UnusableString=574;
    /// <summary>引擎有對應設定的號碼（遞增）。</summary>
    public static readonly int[] Supported=[1002,1003,1005,1019,1020,1023,1024,1027];
    /// <summary>用 573／574（可使用／無法使用）的號碼；其他用 567／568。</summary>
    public static bool UsesUsable(int id)=>id==1019;
    /// <summary>原生 UI 的 HP/MP 設定來源（例如 () =&gt; OptionPanel.HpMpWindow(root)?.Settings）；null＝沒有。</summary>
    public static Func<HpMpSettings?>? HpMp{get;set;}

    static readonly object gate=new();
    static bool overridden;static WorldSession? overrideWorld;static Func<HpMpSettings?>? overrideHpMp;
    static (WorldSession? World,Func<HpMpSettings?>? HpMp) Sources(){
        lock(gate)if(overridden)return (overrideWorld,overrideHpMp);
        return (HyperTextClock.Session,HpMp);
    }
    /// <summary>測試用：換成指定的世界與 HP/MP 來源，直到 Dispose。</summary>
    internal static IDisposable Use(WorldSession? world,Func<HpMpSettings?>? hpMp=null){
        lock(gate){
            var previous=(overridden,overrideWorld,overrideHpMp);overridden=true;overrideWorld=world;overrideHpMp=hpMp;
            return new Scope(()=>{lock(gate)(overridden,overrideWorld,overrideHpMp)=previous;});
        }
    }
    sealed class Scope(Action dispose):IDisposable{Action? action=dispose;public void Dispose(){Interlocked.Exchange(ref action,null)?.Invoke();}}

    /// <summary>選項目前的開關；沒有對應設定或沒有來源回 null。</summary>
    public static bool? Flag(int id){
        if(Array.BinarySearch(Supported,id)<0)return null;
        var (w,hp)=Sources();
        return id switch{
            1002=>w?.PickupAll,
            1003=>HeadBubbleOptions.BubbleFrames,
            1005=>w?.ChatChannels.Whisper,
            1019=>w?.TradeAskWindow,
            1020=>w?.ChatChannels.Trade,
            1023=>hp?.Invoke()?.ShowCharacterHpMp,
            1024=>hp?.Invoke()?.Show,
            1027=>HeadBubbleOptions.ShopMessages,
            _=>null};
    }
    /// <summary>&lt;var src="#N"&gt; 要顯示的字；null＝引擎沒有這個值（呼叫端顯示「缺少參數」）。</summary>
    public static string? Value(int id)=>Flag(id) is bool on?Text(UsesUsable(id)?(on?UsableString:UnusableString):(on?OnString:OffString)):null;
    static string Text(int id)=>ServerMessageText.Table is {} t?(id<t.Count?t[id]:""):id switch{
        OnString=>"開",OffString=>"關",UsableString=>"可使用",UnusableString=>"無法使用",_=>""};
}

/// <summary>排好的一個片段：X／Y 相對於該列左上角（已含對齊位移）。Text 是畫的字（圖為空字串；沒有圖的可點圖是標籤）。</summary>
public sealed record HyperTextPiece(int X,int Y,int Width,int Height,HyperTextNode Node,string Text,UiSurface? Image=null) {
    public int Link=>Node.Link;
    public Rectangle Local=>new(X,Y,Width,Height);
}
/// <summary>排好的一列。Height＝列高（至少 lineHeight，有圖時取最高的圖）。</summary>
public sealed record HyperTextLine(int Height,HyperTextAlign Align,IReadOnlyList<HyperTextPiece> Pieces) {
    public string Text=>string.Concat(Pieces.Select(p=>p.Text));
    public int ContentWidth=>Pieces.Count==0?0:Pieces.Max(p=>p.X+p.Width);
}

public static class HyperTextLayout {
    /// <summary>
    /// 依寬度排版：字逐字換行（中文沒有空白可斷，同舊 Wrap）；圖整張放，放不下就換行；&lt;br&gt; 一律換行、段落換行只在前面有字時。
    /// 對齊在每列排完後套用（以該列第一個片段的段落對齊為準）。尾端空列去掉。
    /// </summary>
    public static IReadOnlyList<HyperTextLine> Build(HyperTextDocument doc,int width,Func<string,int> measure,Func<int,UiSurface?> surf,int lineHeight){
        var lines=new List<HyperTextLine>();
        var pieces=new List<HyperTextPiece>();int x=0;var lineAlign=HyperTextAlign.Left;bool started=false;
        void Finish(){
            int h=Math.Max(lineHeight,pieces.Count==0?0:pieces.Max(p=>p.Height));
            int content=pieces.Count==0?0:pieces.Max(p=>p.X+p.Width);
            int shift=lineAlign switch{HyperTextAlign.Center=>Math.Max(0,(width-content)/2),HyperTextAlign.Right=>Math.Max(0,width-content),_=>0};
            lines.Add(new(h,lineAlign,pieces.Select(p=>p with{X=p.X+shift}).ToArray()));
            pieces=[];x=0;started=false;
        }
        void Begin(HyperTextNode n){if(!started){lineAlign=n.Align;started=true;}}
        void PlaceText(HyperTextNode n,string text){
            int start=0,placedWidth=0;
            while(start<text.Length){
                if(x==0)while(start<text.Length&&text[start]==' ')start++;   // 列首空白收掉
                if(start>=text.Length)break;
                int end=start;
                while(end<text.Length&&x+measure(text[start..(end+1)])<=width)end++;
                if(end==start){
                    if(x>0){Finish();continue;}
                    end=start+1;   // 一個字都放不下：照放一個字（同舊 Wrap）
                }
                Begin(n);
                string chunk=text[start..end];int w=measure(chunk);
                pieces.Add(new(x,0,w,lineHeight,n,chunk));x+=w;placedWidth+=w;start=end;
                if(start<text.Length)Finish();
            }
            if(n.MinWidth>placedWidth){
                int pad=Math.Min(n.MinWidth-placedWidth,Math.Max(0,width-x));
                if(pad>0){Begin(n);pieces.Add(new(x,0,pad,lineHeight,n with{Link=-1},""));x+=pad;}
            }
        }
        foreach(var n in doc.Nodes){
            switch(n.Kind){
            case HyperTextNodeKind.Break:
                if(n.Soft){if(pieces.Count>0)Finish();}
                else{if(!started)lineAlign=n.Align;Finish();}
                break;
            case HyperTextNodeKind.Paragraph:if(pieces.Count==0){lineAlign=n.Align;started=false;}break;
            case HyperTextNodeKind.Image:{
                var img=n.Surf is int id?surf(id):null;
                if(img is {Width:>0,Height:>0}){
                    if(x>0&&x+img.Width>width)Finish();
                    Begin(n);pieces.Add(new(x,0,img.Width,img.Height,n,"",img));x+=img.Width;
                }else if(n.Link>=0&&n.Label.Length>0)PlaceText(n,n.Label);   // 引擎自訂：沒有圖的可點圖畫成文字標籤
                else if(n.Raw.Length>0)PlaceText(n,n.Raw);                     // <var img> 但不是圖號／沒有圖：照字顯示
                break;}
            default:PlaceText(n,n.Text);break;
            }
        }
        if(pieces.Count>0)Finish();
        while(lines.Count>0&&lines[^1].Pieces.Count==0)lines.RemoveAt(lines.Count-1);
        return lines;
    }
}

/// <summary>
/// 最近渲染過的頁（WorldSession.RenderHyperText 登記）。NpcUi.cs 建 HyperTextView 時只帶純文字（本組不改該檔），面板依文字找回文件；
/// 找不到或對不上時退回舊排法。最多留 16 頁，最新的優先。（後續若 HyperTextView 直接帶 Document，這裡就可以拿掉。）
/// </summary>
public sealed class HyperTextDocuments {
    public static HyperTextDocuments Shared{get;}=new();
    public const int Capacity=16;
    readonly LinkedList<HyperTextDocument> recent=new();
    readonly object gate=new();
    public void Register(HyperTextDocument doc){
        lock(gate){
            for(var n=recent.First;n!=null;n=n.Next)if(n.Value.Plain==doc.Plain){recent.Remove(n);break;}
            recent.AddFirst(doc);
            while(recent.Count>Capacity)recent.RemoveLast();
            Version++;
        }
    }
    public void Clear(){lock(gate){recent.Clear();Version++;}}
    /// <summary>每次登記或清空 +1（面板據此重新對照）。</summary>
    public int Version{get;private set;}
    public int Count{get{lock(gate)return recent.Count;}}
    /// <summary>找文字相同（或 $ 展開後相同）的最新文件；回傳已展開的文件。</summary>
    public HyperTextDocument? Find(string shown){
        HyperTextDocument[] snapshot;lock(gate)snapshot=recent.ToArray();
        foreach(var d in snapshot)if(d.Plain==shown)return d;
        foreach(var d in snapshot)if(d.ResolveTo(shown) is {} r)return r;
        return null;
    }
}

/// <summary>取 NpcTemplates 的原始 HTML（該類別的模板表是私有欄位；本組不改 NpcTemplates.cs，用反射讀，欄位改名時回 null → 舊排法）。</summary>
public static class NpcTemplateMarkup {
    static readonly FieldInfo? templatesField=typeof(NpcTemplates).GetField("templates",BindingFlags.NonPublic|BindingFlags.Instance);
    public static bool Available=>templatesField!=null&&templatesField.FieldType==typeof(Dictionary<string,string>);
    public static string? Markup(this NpcTemplates templates,string key)=>templatesField?.GetValue(templates) is Dictionary<string,string> d&&d.TryGetValue(key,out var html)?html:null;
}
