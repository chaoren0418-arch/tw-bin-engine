using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace P06;
// Displayed=false: the original client drops the packet (id >= table count); Text is then empty.
// Raw keeps the native formatter output including colour controls; Text is the plain display form.
public sealed record ServerMessageReport(ushort Id,string[] Arguments,string Text,bool Recognized,bool Displayed=true,string? Raw=null);

/// <summary>
/// Original string table <c>string-c.tbl</c> (Text.pak, CP950): first line = record count, then one record per line, id from 0.
/// Loaded read-only at run time from the original client; never copied into the repository.
/// Evidence: docs/reference/protocol-gaps.md §3 (sub_A121D0 "%s-%c.tbl", lookup sub_4EAFF0, count at dword_153C998).
/// </summary>
public sealed class ServerStringTable {
    public const string FileName="string-c.tbl";
    readonly string[] lines;
    public int Count {get;}
    public string Sha256 {get;}
    ServerStringTable(int count,string[] lines,string sha){Count=count;this.lines=lines;Sha256=sha;}
    static Encoding Cp950{get{Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950);}}
    /// <summary>Id &lt; Count; ids past the stored lines read as empty (sub_4EAFF0 returns "" when out of bounds).</summary>
    public string this[int id]=>id>=0&&id<Count?(id<lines.Length?lines[id]:""):throw new ArgumentOutOfRangeException(nameof(id));
    public static ServerStringTable Parse(byte[] decoded){
        string text=Cp950.GetString(decoded);
        var all=text.Split('\n');for(int i=0;i<all.Length;i++)all[i]=all[i].TrimEnd('\r');
        if(!int.TryParse(all[0].Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int count)||count<0||count>1_000_000)
            throw new InvalidDataException($"{FileName}：第一行不是筆數");
        return new(count,all[1..],Convert.ToHexString(SHA256.HashData(decoded)).ToLowerInvariant());
    }
    /// <summary>Reads the single <c>string-c.tbl</c> record of a Text archive.</summary>
    public static ServerStringTable Load(TextArchive archive)=>
        Parse(archive.Read(archive.Single(FileName)??throw new InvalidDataException($"Text 封存沒有 {FileName}")));
}

/// <summary>
/// R8-B（T357／#821，FU8§2）：物品說明表 <c>ItemDesc-c.tbl</c>（開機時 <c>sub_87BA80("ItemDesc")</c> 讀進 <c>dword_13CD880</c>，筆數 <c>dword_13CDAA8</c>）。
/// 格式（本機 Text.pak 第 13249 筆，108,872 B，已驗證）：第一行是筆數（本機 14469），之後每行「說明編號 空白 說明」，編號不連續（本機 2048 行有編號）。
/// 有幾行編號後面沒有空白；那些編號都 ≥ 筆數，原版不印，所以「編號後的一個空白可有可無」的讀法不影響結果（推測）。
/// 同一編號出現兩次時取後面那行（推測；本機沒有重複）。執行期從客戶端唯讀解出，不寫入 repo。
/// </summary>
public sealed class ItemDescTable {
    public const string FileName="ItemDesc-c.tbl";
    /// <summary>本機解出內容的 SHA-256（Text.pak 第 13249 筆）。</summary>
    public const string Sha256Pin="3afae71a02e1a1f9dc7d9ef6884f71cfacf4a629eeb1e11471086bebb80c64d0";
    readonly Dictionary<int,string> entries;
    /// <summary>第一行的筆數；編號 ≥ 筆數不印。</summary>
    public int Count {get;}
    /// <summary>有編號的行數。</summary>
    public int Entries=>entries.Count;
    public string Sha256 {get;}
    ItemDescTable(int count,Dictionary<int,string> entries,string sha){Count=count;this.entries=entries;Sha256=sha;}
    /// <summary>原始文字（含色碼）；編號超出筆數或沒有這一行時是 null。</summary>
    public string? this[int id]=>id>=0&&id<Count&&entries.TryGetValue(id,out var s)?s:null;
    /// <summary>要印的說明：去掉色碼；沒有、或只有空白時是 null（原版不印）。</summary>
    public string? Text(int id)=>this[id] is {} raw&&ServerMessageText.DisplayText(raw) is {} text&&text.Trim().Length>0?text:null;
    public static ItemDescTable Parse(byte[] decoded){
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var all=Encoding.GetEncoding(950).GetString(decoded).Split('\n');
        if(!int.TryParse(all[0].Trim(),System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out int count)||count<0||count>1_000_000)
            throw new InvalidDataException($"{FileName}：第一行不是筆數");
        var map=new Dictionary<int,string>();
        for(int i=1;i<all.Length;i++){
            string line=all[i].TrimEnd('\r');int j=0;long id=0;
            while(j<line.Length&&char.IsAsciiDigit(line[j])){id=Math.Min(id*10+(line[j]-'0'),int.MaxValue);j++;}
            if(j==0)continue;
            if(j<line.Length&&line[j]==' ')j++;
            map[(int)id]=line[j..];
        }
        return new(count,map,Convert.ToHexString(SHA256.HashData(decoded)).ToLowerInvariant());
    }
    /// <summary>讀 Text 封存裡唯一的 <c>ItemDesc-c.tbl</c>，核 pin 後解析（只在記憶體解出）。</summary>
    public static ItemDescTable Load(TextArchive archive){
        var decoded=archive.Read(archive.Single(FileName)??throw new InvalidDataException($"Text 封存沒有 {FileName}"));
        if(!Convert.ToHexString(SHA256.HashData(decoded)).Equals(Sha256Pin,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException($"{FileName} pin 不符");
        return Parse(decoded);
    }
}

public static class ServerMessageText {
    // Without the original table (e.g. CI without the client) only these source-proven ids keep their previous text.
    // 125 = \f1你不能夠放棄此樣物品。 (work/issue-77/text-source.json)
    // 189 = \f1金幣不足。 ; 337 = \f1%0不足%s。 ; 403 = 獲得%0%o 。 .
    static string? Fallback(ushort id,string[] arguments)=>id switch{125 when arguments.Length==0=>"你不能夠放棄此樣物品。",189 when arguments.Length==0=>"金幣不足。",337 when arguments.Length==1&&!string.IsNullOrWhiteSpace(arguments[0])=>arguments[0]+"不足。",403 when arguments.Length==1&&arguments[0].Length>0=>"獲得"+arguments[0]+" 。",_=>null};

    static readonly object gate=new();
    static ServerStringTable? table,overrideTable;static bool loaded,overridden;
    public static string? TableError {get;private set;}
    /// <summary>The run-time table (lazy, read-only from <see cref="GroundSpriteReader.ClientRoot"/>); null when it cannot be read.</summary>
    public static ServerStringTable? Table{get{
        lock(gate){
            if(overridden)return overrideTable;
            if(!loaded){
                loaded=true;
                try{table=ServerStringTable.Load(TextArchive.Open(GroundSpriteReader.ClientRoot));TableError=null;}
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException or DecoderFallbackException){
                    table=null;TableError=ex.Message;System.Diagnostics.Trace.WriteLine("[server-message] "+FileName+" 無法載入，改用內建訊息："+ex.Message);
                }
            }
            return table;
        }
    }}
    const string FileName=ServerStringTable.FileName;
    /// <summary>Tests only: replace the table (null = no table) until the returned scope is disposed.</summary>
    internal static IDisposable UseTable(ServerStringTable? replacement){
        lock(gate){var previous=(overridden,overrideTable);overridden=true;overrideTable=replacement;return new Scope(()=>{lock(gate)(overridden,overrideTable)=previous;});}
    }
    sealed class Scope(Action dispose):IDisposable{Action? action=dispose;public void Dispose(){Interlocked.Exchange(ref action,null)?.Invoke();}}

    // R8-B（T357／#821）：ItemDesc-c.tbl，與 string-c.tbl 同樣延遲、唯讀載入；讀不到（例如 CI 沒有客戶端）是 null，鑑定只印第一行。
    static readonly object itemDescGate=new();
    static ItemDescTable? itemDesc,itemDescOverride;static bool itemDescLoaded,itemDescOverridden;
    public static string? ItemDescError {get;private set;}
    public static ItemDescTable? ItemDesc{get{
        lock(itemDescGate){
            if(itemDescOverridden)return itemDescOverride;
            if(!itemDescLoaded){
                itemDescLoaded=true;
                try{itemDesc=ItemDescTable.Load(TextArchive.Open(GroundSpriteReader.ClientRoot));ItemDescError=null;}
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException or DecoderFallbackException){
                    itemDesc=null;ItemDescError=ex.Message;System.Diagnostics.Trace.WriteLine("[server-message] "+ItemDescTable.FileName+" 無法載入，鑑定不印物品說明："+ex.Message);
                }
            }
            return itemDesc;
        }
    }}
    /// <summary>Tests only: replace the ItemDesc table (null = no table) until the returned scope is disposed.</summary>
    internal static IDisposable UseItemDesc(ItemDescTable? replacement){
        lock(itemDescGate){var previous=(itemDescOverridden,itemDescOverride);itemDescOverridden=true;itemDescOverride=replacement;return new Scope(()=>{lock(itemDescGate)(itemDescOverridden,itemDescOverride)=previous;});}
    }

    /// <summary>sub_942B40 general path: the packet is dropped when id &gt;= table count (only known with the table).</summary>
    public static bool IsDropped(ushort id)=>Table is {} t&&id>=t.Count;

    public static ServerMessageReport Decode(ushort id,string[] arguments){
        var args=arguments.ToArray();
        if(Table is {} t){
            if(id>=t.Count)return new(id,args,"",false,false);
            string raw=Format(t[id],args);
            return new(id,args,DisplayText(raw),true,true,raw);
        }
        string? known=Fallback(id,args);
        return new(id,args,known??$"原服訊息 {id}"+(args.Length>0?"："+string.Join("、",args):""),known!=null);
    }

    // Original BIN sub_933850 (VA 0x933850 / FO 0x532C50), CP950 path (dword_1379208 != 949):
    //   '%' + digit n : argument n when n < count, otherwise nothing. The argument is copied literally (never re-parsed);
    //                   its first '^' becomes ' ' (sub_941E40) and "\<" is appended (strcat_s with the "\" + '<' string).
    //   '%' + d/o/s   : nothing (only the CP949 branch emits Korean particles).
    //   '%' + other   : that character alone ("%%" -> "%", "%n" -> "n"); '%' at the end stops the output.
    // Operating on decoded chars equals the native byte loop: '%', digits and ASCII never occur as Big5 trail bytes.
    // Not modelled: the 256-byte per-argument / 512-byte output buffers, and the argument name lookup at 0x5417C0
    // ("$nnn" tokens stay literal and are resolved by the display layer, as before).
    public static string Format(string template,IReadOnlyList<string> arguments){
        var sb=new StringBuilder(template.Length+16);
        for(int i=0;i<template.Length;i++){
            char c=template[i];
            if(c!='%'){sb.Append(c);continue;}
            if(++i>=template.Length)break;
            char k=template[i];
            if(k is >= '0' and <= '9'){
                int n=k-'0';if(n>=arguments.Count)continue;
                string a=arguments[n];int caret=a.IndexOf('^');
                if(caret>=0)a=string.Concat(a.AsSpan(0,caret)," ",a.AsSpan(caret+1));
                sb.Append(a).Append("\\<");
            }
            else if(k is 'd' or 'o' or 's'){}
            else sb.Append(k);
        }
        return sb.ToString();
    }
    // Presentation only: colour / font controls (\fN, \FN, \<) are not drawn by the engine's plain text widgets.
    static readonly Regex Control=new(@"\\[fF].|\\<",RegexOptions.Singleline|RegexOptions.CultureInvariant);
    public static string DisplayText(string raw)=>Control.Replace(raw,"");
}
