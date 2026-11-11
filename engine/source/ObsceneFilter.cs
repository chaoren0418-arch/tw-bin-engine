using System.Security.Cryptography;
using System.Text;
namespace P06;

/// <summary>
/// T437（#1008，FU17§4.1）：不雅字過濾 <c>sub_51F850(out, 文字, "*")</c> → <c>sub_51F5D0</c>（TW16 FO 0x11E9D0）。
/// 表＝<c>obscene-&lt;語系&gt;.tbl</c>（Text.pak；TW16 <c>sub_A14550</c> 以 <c>sub_A11F50("obscene")</c> 載入，存在 <c>dword_153C9F0</c>）。
/// 格式（本機 0-客戶端 Text.pak 第 24094 筆 <c>obscene-c.tbl</c>，930 B，CP950，已驗證）：第一行是筆數（本機 124），之後一行一個詞
/// （本機只有 123 行，其中 1 行空白；第 124 筆讀成空字串）。執行期從客戶端唯讀解出，不寫入 repo。
/// 做法（已驗證）：依表的順序找第一個在文字裡出現的詞（跳過空字串）→ 換成「字數」個 <c>*</c>（<c>mbslen</c>，中文一個字算一個）→ 從頭再找，直到找不到。
/// 推測（待驗證）：
/// 1. 比對（<c>sub_51F900</c>）：ASCII 英文字母不分大小寫（同表的 <c>sub_93F430</c> 用 strnicmp），中文逐位元組相同；只在字的邊界開始比（DBCS 前導位元組一次跳兩個）。
/// 2. 順序（<c>qsort(…, sub_A11230)</c>，比較函式沒追）：位元組長的詞優先，同長照表的順序。
/// 3. 詞照原樣（只去掉行尾 \r），前後空白保留（本機有 2 個詞帶尾端空白）。
/// 4. 全是 <c>*</c> 的詞跳過（原版會無限循環；本機表沒有這種詞）。
/// </summary>
public sealed class ObsceneFilter {
    /// <summary>語系字母（TW＝c；sub_A121D0 的 "%s-%c.tbl"）。</summary>
    public const char Language='c';
    public static string FileNameFor(char language)=>$"obscene-{language}.tbl";
    public static readonly string FileName=FileNameFor(Language);
    /// <summary>本機解出內容的 SHA-256（0-客戶端 Text.pak 第 24094 筆 obscene-c.tbl）。</summary>
    public const string Sha256Pin="7e90a29e72ddae7b982fb1bbd40d833bbf1a8be287c228fc77b4786ad7bc4151";

    readonly string[] words;
    /// <summary>第一行的筆數。</summary>
    public int Count {get;}
    /// <summary>排序後實際用來比對的詞（不含空字串、全 * 的詞）。</summary>
    public IReadOnlyList<string> Words=>words;
    ObsceneFilter(int count,string[] words){Count=count;this.words=words;}

    static Encoding Cp950{get{Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950);}}

    /// <summary>解析解出的表：第一行筆數，之後一行一個詞（只去掉行尾 \r）；第一行不是數字 → InvalidDataException。</summary>
    public static ObsceneFilter Parse(byte[] decoded){
        var all=Cp950.GetString(decoded).Split('\n');
        for(int i=0;i<all.Length;i++)all[i]=all[i].TrimEnd('\r');
        if(!int.TryParse(all[0].Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int count)||count<0||count>1_000_000)
            throw new InvalidDataException($"{FileName}：第一行不是筆數");
        // sub_4EAFF0(i)，i < 筆數；超出實際行數讀成空字串（跳過）。
        var list=new List<string>();
        for(int i=1;i<all.Length&&i<=count;i++){
            string w=all[i];
            if(w.Length==0||w.All(c=>c=='*'))continue;
            list.Add(w);
        }
        // 推測：長的優先（位元組長度），同長保持表的順序（OrderBy 是穩定排序）。
        var sorted=list.Select((w,i)=>(w,i)).OrderByDescending(x=>Cp950.GetByteCount(x.w)).ThenBy(x=>x.i).Select(x=>x.w).ToArray();
        return new(count,sorted);
    }
    /// <summary>合成表（測試）：直接給詞，照 Parse 同樣排序。</summary>
    public static ObsceneFilter FromWords(params string[] words)=>Parse(Cp950.GetBytes(words.Length+"\r\n"+string.Join("\r\n",words)));

    /// <summary>讀 Text 封存裡唯一的 <c>obscene-c.tbl</c>，核 pin 後解析（只在記憶體解出）。</summary>
    public static ObsceneFilter Load(TextArchive archive){
        var decoded=archive.Read(archive.Single(FileName)??throw new InvalidDataException($"Text 封存沒有 {FileName}"));
        if(!Convert.ToHexString(SHA256.HashData(decoded)).Equals(Sha256Pin,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException($"{FileName} pin 不符");
        return Parse(decoded);
    }

    /// <summary>sub_51F5D0：反覆把第一個找到的詞換成字數個 *，直到沒有。</summary>
    public string Filter(string text){
        if(text.Length==0||words.Length==0)return text;
        // 每次替換至少把一個非 * 字元變成 *，所以一定會停；上限只是保險。
        for(int guard=text.Length+1;guard>0;guard--){
            int at=-1;string? hit=null;
            foreach(var w in words){at=Find(text,w);if(at>=0){hit=w;break;}}
            if(hit==null)return text;
            text=string.Concat(text.AsSpan(0,at),new string('*',hit.Length),text.AsSpan(at+hit.Length));
        }
        return text;
    }
    /// <summary>sub_51F740／sub_51F900：從字的邊界找第一個位置；ASCII 字母不分大小寫（推測）。字串以 char 為單位＝CP950 的 DBCS 字邊界。</summary>
    static int Find(string text,string word){
        for(int i=0;i+word.Length<=text.Length;i++){
            int k=0;
            while(k<word.Length&&Same(text[i+k],word[k]))k++;
            if(k==word.Length)return i;
        }
        return -1;
    }
    static bool Same(char a,char b)=>a==b||(a<128&&b<128&&char.ToLowerInvariant(a)==char.ToLowerInvariant(b));

    static readonly object gate=new();
    static ObsceneFilter? table,overrideTable;static bool loaded,overridden;
    public static string? LoadError{get;private set;}
    /// <summary>執行期的表（延遲、唯讀，從 GroundSpriteReader.ClientRoot）；讀不到（CI 沒有客戶端、pin 不符）是 null＝不過濾。</summary>
    public static ObsceneFilter? Current{get{
        lock(gate){
            if(overridden)return overrideTable;
            if(!loaded){
                loaded=true;
                try{table=Load(TextArchive.Open(GroundSpriteReader.ClientRoot));LoadError=null;}
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException){
                    table=null;LoadError=ex.Message;System.Diagnostics.Trace.WriteLine("[obscene] "+FileName+" 無法載入，備註不過濾："+ex.Message);
                }
            }
            return table;
        }
    }}
    /// <summary>Tests only: replace the table (null = no table) until the returned scope is disposed.</summary>
    internal static IDisposable Use(ObsceneFilter? replacement){
        lock(gate){var previous=(overridden,overrideTable);overridden=true;overrideTable=replacement;return new Scope(()=>{lock(gate)(overridden,overrideTable)=previous;});}
    }
    sealed class Scope(Action dispose):IDisposable{Action? action=dispose;public void Dispose(){Interlocked.Exchange(ref action,null)?.Invoke();}}
    /// <summary>送出前的過濾：有表就過濾，沒有表照原文。</summary>
    public static string Apply(string text)=>Current is {} f?f.Filter(text):text;
}
