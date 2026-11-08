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
