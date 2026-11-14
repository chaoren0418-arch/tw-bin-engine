using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace P06.NativeUi;

// R7-N (T354/#817, gap-matrix 15.6): one TextResolver for the P-D panels whose XML comes from the reference client.
//
// Which table (the "先裁定" item of 15.6): the reference client's own Text.pak, the same client RefClientXmlSource served
// the XML from. Reason (PakViewer, read-only, docs/reports/issue-817.md §1):
// - 0-客戶端 string-c.tbl has 3484 records; the String ids in these XMLs reach 4500 (ResetStatUI 4082, AgitAuctionUI 4391,
//   ReNStatusUIEx 4500 …) and Desc ids reach 21391, past the local desc-c.tbl (17001). The local table cannot serve them.
// - kor25 string-c.tbl (4703 records) and desc-c.tbl (22617) cover every id; where both tables have a record, 3304 of 3484
//   string rows and 15974 of 16945 desc rows are identical, and the ids checked against the windows read as their labels
//   (4082 "Tip. 可以使用空白鍵提升每一等級", 4391 "競標目錄", 3233–3235 "陣營／階段／數值").
// The tables are read at run time from <ref root>\Text.idx/Text.pak (TextArchive, decoded in memory only), identified by
// SHA-256 in the UI log ("ui-text-ref"); nothing is copied into the repository or written anywhere. No pin: the reference
// client is user-configurable (TW_REF_CLIENT / native-ui.json); the kor25 hashes are only compared and logged.
//
// Resolution rules:
// - kind "String" → string-c.tbl, "Desc" → desc-c.tbl (sub_A121D0 "%s-%c.tbl" with 'c'); any other kind → null.
// - value "N" or "N;flags…" → record N (the trailing fields are flags, as for the image refs); not a number → null.
// - N ≥ the table's count, an empty record, or a kor25 placeholder "dummyNNNN" → null (the label is not drawn; no invented text).
// - Colour/font controls (\fN, \FN, \<) are removed (ServerMessageText.DisplayText) and trailing blanks/tabs trimmed (推測:
//   presentation only; the engine's plain text widgets do not draw them).
// - No reference client (off / missing / same as the main client) → UiResources.Text is null → Resolver returns null and
//   the panels keep today's behaviour (labels with only String/Desc ids are not drawn).

/// <summary>One string table read from the reference client, with the hash that identifies it.</summary>
public sealed record RefClientTextTable(string FileName,ServerStringTable Table,string Root,int Record,uint StoredSize,string PlainSha256,bool KnownKor25);

public sealed class RefClientTextSource {
    public const string StringKind="String",DescKind="Desc";
    public const string StringFileName=ServerStringTable.FileName,DescFileName=TextArchive.DescFileName;
    public const string PlaceholderPrefix="dummy";
    /// <summary>SHA-256 of the decoded kor25 tables (D:\381懶人包kor25(原) Text.pak records 33458 / 4415, PakViewer, 2026-11-11).
    /// Compared and logged only; another reference client is allowed.</summary>
    public const string Kor25StringSha256="9b165baa8a64e84c6471e2ca092cb82aef1e6e80574690dc673946b66a6531b6";
    public const string Kor25DescSha256="67242c39b6ccaa77192015a3c8cb6e80b59083f7ef0e969f0290c1ba3568d354";

    readonly object gate=new();
    readonly Func<TextArchive> open;
    TextArchive? archive;bool archiveTried;
    readonly Dictionary<string,RefClientTextTable?> tables=new(StringComparer.Ordinal);
    readonly Dictionary<(string,string),string?> cache=new();
    string? unavailable;

    public string Root{get;}
    /// <summary>Where the root came from ("env", "settings", "default", "explicit" or "synthetic").</summary>
    public string Source{get;}
    /// <summary>Why nothing can be read (missing / unreadable Text.idx or Text.pak); null while usable or not yet tried.</summary>
    public string? Unavailable{get{lock(gate)return unavailable;}}

    public RefClientTextSource(string root,string source="explicit"):this(Path.GetFullPath(root),source,null){}
    RefClientTextSource(string root,string source,Func<TextArchive>? open){Root=root;Source=source;this.open=open??(()=>TextArchive.Open(Root));}
    /// <summary>Test seam: an in-memory Text archive instead of Root\Text.idx/Text.pak.</summary>
    internal static RefClientTextSource FromArchive(string root,TextArchive archive)=>new(root,"synthetic",()=>archive);

    /// <summary>The text source of the reference client the XML source reads (null when there is none).</summary>
    public static RefClientTextSource? For(RefClientXmlSource? xml)=>xml==null?null:new RefClientTextSource(xml.Root,xml.Source);

    /// <summary>
    /// The one-line wiring for a P-D panel registration: the resources' reference text source, or null (no reference client:
    /// labels stay undrawn as before). Starts loading the two tables in the background so the first paint does not wait.
    /// </summary>
    public static Func<string,string,string?>? Resolver(UiResources? resources){
        if(resources?.Text is not {} text)return null;
        text.Preload();
        return text.Resolve;
    }

    int preloading;
    /// <summary>Loads both tables on the thread pool once (errors are logged by the load itself).</summary>
    public void Preload(){if(Interlocked.Exchange(ref preloading,1)==0)ThreadPool.QueueUserWorkItem(_=>{Table(StringKind);Table(DescKind);});}

    /// <summary>Every table read so far (diagnostics and the report).</summary>
    public IReadOnlyList<RefClientTextTable> Served{get{lock(gate)return tables.Values.OfType<RefClientTextTable>().ToArray();}}

    static string? FileFor(string kind)=>kind switch{StringKind=>StringFileName,DescKind=>DescFileName,_=>null};

    /// <summary>The table behind a TextKind ("String" / "Desc"); null for another kind or when it cannot be read (logged once).</summary>
    public RefClientTextTable? Table(string kind){
        if(FileFor(kind) is not {} file)return null;
        lock(gate){
            if(tables.TryGetValue(file,out var hit))return hit;
            RefClientTextTable? found=null;
            if(Archive() is {} a){
                try{
                    var entry=a.Single(file);
                    if(entry==null)UiResourceLog.Write($"ui-text-ref {file}: 參考客戶端 {Root}\\Text.pak 沒有");
                    else{
                        var plain=a.Read(entry);
                        var table=ServerStringTable.Parse(plain);
                        bool known=table.Sha256.Equals(file==StringFileName?Kor25StringSha256:Kor25DescSha256,StringComparison.OrdinalIgnoreCase);
                        found=new(file,table,Root,entry.Record,entry.Size,table.Sha256,known);
                        UiResourceLog.Write($"ui-text-ref {file} -> {Root}\\Text.pak:#{entry.Record} plain sha256 {table.Sha256} (count {table.Count}, {entry.Size} B, {Source}{(known?", 與 kor25 相同":", 不是已知的 kor25 版本")})");
                    }
                }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException or DecoderFallbackException){
                    UiResourceLog.Write($"ui-text-ref {file} 無法讀取，字串不畫：{ex.Message}");
                }
            }
            tables[file]=found;return found;
        }
    }

    TextArchive? Archive(){
        if(archiveTried)return archive;
        archiveTried=true;
        try{archive=open();}
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException){
            unavailable=ex.Message;UiResourceLog.Write($"ui-text-ref 參考客戶端 {Root} 的 Text.idx／Text.pak 無法讀取，字串不畫：{ex.Message}");
        }
        return archive;
    }

    /// <summary>"4391;1" → 4391; null when the leading field is not a non-negative number.</summary>
    public static int? RecordId(string value){
        if(value==null)return null;
        int cut=value.IndexOf(';');
        var head=(cut<0?value:value[..cut]).Trim();
        return int.TryParse(head,NumberStyles.None,CultureInfo.InvariantCulture,out int id)?id:null;
    }

    /// <summary>Display text of a table record (see the rules at the top of the file); null = not drawn.</summary>
    public static string? Text(ServerStringTable table,int id){
        if(id<0||id>=table.Count)return null;
        var raw=table[id];
        if(raw.StartsWith(PlaceholderPrefix,StringComparison.OrdinalIgnoreCase))return null;
        var text=ServerMessageText.DisplayText(raw).TrimEnd(' ','\t','\r','\n');
        return text.Trim().Length==0?null:text;
    }

    /// <summary>TextResolver: (TextKind, TextValue) → text, or null when it cannot be resolved.</summary>
    public string? Resolve(string kind,string value){
        if(kind==null||value==null)return null;
        lock(gate){if(cache.TryGetValue((kind,value),out var hit))return hit;}
        string? text=RecordId(value) is {} id&&Table(kind) is {} t?Text(t.Table,id):null;
        lock(gate){cache[(kind,value)]=text;}
        return text;
    }

    /// <summary>#1208: true when the record behind (kind, value) exists and is a "dummyNNNN" placeholder (not drawn by Text).</summary>
    public bool IsPlaceholder(string kind,string value){
        if(kind==null||value==null||RecordId(value) is not {} id||Table(kind) is not {} t||id>=t.Table.Count)return false;
        return t.Table[id].StartsWith(PlaceholderPrefix,StringComparison.OrdinalIgnoreCase);
    }
}

// #1208 (SM57 of #1186): kor25's desc-c.tbl / string-c.tbl hold "dummyNNNN" placeholders for every OptionUI.xml label of the
// option window except 18809 and the String ids 613/1994–1997/893–896 (Desc 18792–18814, 20818–20832, 20862, 20863, 21145,
// 21391; String 4042–4045, 4408), so the eight 常用選項 check buttons were drawn without names. The names are not run-time
// Captions: they are these Desc ids. Four other local clients' desc-c.tbl / string-c.tbl carry real text at exactly those
// ids, the same text in all four (小草, 超人760, 雷雨, 韓版; read-only, docs/reports/issue-1208.md), and every name matches its
// MouseEvent (18805 → ShowDropItemNameBtn …).
//
// Rule (only the option window uses it): the reference text wins; only when the reference record is a dummy placeholder
// is the same kind and id read from a second client ("fill"). The fill table is used only when its ids line up with the
// reference table: over the ids both tables have real text, at least MinAgreement must be identical (kor25 vs 小草: desc
// 97.9 %, string 90.8 %); otherwise nothing is filled (logged once). Fill root: TW_REF_TEXT_FILL (non-empty path; "off" =
// none) → default 小草 (a 3.81 client like kor25). The tables are read in memory only and identified by SHA-256 in the
// UI log ("ui-text-fill"); nothing is copied into the repository.
public sealed class RefClientTextFill {
    public const string EnvironmentVariable="TW_REF_TEXT_FILL";
    public const string DefaultRoot=@"D:\381懶人包小草(原)";
    /// <summary>SHA-256 of the decoded 小草 tables (Text.pak records 4339 / 32852, read-only, 2026-11-14). Compared and logged only.</summary>
    public const string XiaocaoDescSha256="eaf72c1a3d12785fc6b22f49e47b13fb5471ebc940eaec1d3c5864efe140bdb4";
    public const string XiaocaoStringSha256="6e14b267509cd3d9eefd0caf42b64c2277ccad19a5738ea7951470d2782c754d";
    /// <summary>Share of identical records (over the ids both tables have real text) needed before the fill table is used.</summary>
    public const double MinAgreement=0.8;
    /// <summary>At least this many shared records are needed to judge the alignment.</summary>
    public const int MinShared=100;

    readonly RefClientTextSource primary;
    readonly RefClientTextSource? fill;
    readonly object gate=new();
    readonly Dictionary<string,double?> agreement=new(StringComparer.Ordinal);
    readonly Dictionary<(string,string),string?> cache=new();

    public RefClientTextFill(RefClientTextSource primary,RefClientTextSource? fill){this.primary=primary;this.fill=fill;}
    public RefClientTextSource Primary=>primary;
    public RefClientTextSource? Fill=>fill;

    /// <summary>TW_REF_TEXT_FILL (non-empty; "off" / "0" / "false" = none) → DefaultRoot. Null = no fill.</summary>
    public static (string? Root,string Source) ResolveRoot(string? environment){
        if(!string.IsNullOrWhiteSpace(environment)){
            var v=environment.Trim();
            return v.Equals("off",StringComparison.OrdinalIgnoreCase)||v=="0"||v.Equals("false",StringComparison.OrdinalIgnoreCase)?(null,"env"):(v,"env");
        }
        return (DefaultRoot,"default");
    }

    /// <summary>The fill source for a reference text source: null when turned off, not absolute, or the same folder.</summary>
    public static RefClientTextSource? ConfiguredFill(RefClientTextSource primary,string? environment){
        var (root,source)=ResolveRoot(environment);
        if(root==null){UiResourceLog.Write("ui-text-fill off");return null;}
        if(!Path.IsPathFullyQualified(root)){UiResourceLog.Write($"ui-text-fill 路徑不是絕對路徑，忽略：{root}");return null;}
        var full=Path.GetFullPath(root);
        if(string.Equals(Path.TrimEndingDirectorySeparator(full),Path.TrimEndingDirectorySeparator(primary.Root),StringComparison.OrdinalIgnoreCase))return null;
        if(!File.Exists(Path.Combine(full,"Text.idx"))){UiResourceLog.Write($"ui-text-fill {full} 沒有 Text.idx，佔位字不補");return null;}
        return new RefClientTextSource(full,source);
    }

    /// <summary>The option window's TextResolver: reference text, dummy placeholders filled from the fill client. Null = no reference.</summary>
    public static Func<string,string,string?>? Resolver(UiResources? resources){
        if(resources?.Text is not {} text)return null;
        text.Preload();
        var fill=ConfiguredFill(text,Environment.GetEnvironmentVariable(EnvironmentVariable));
        if(fill==null)return text.Resolve;
        fill.Preload();
        return new RefClientTextFill(text,fill).Resolve;
    }

    /// <summary>Share of identical records over the ids both tables have real (non-empty, non-placeholder) text; null = too few.</summary>
    public static double? Agreement(ServerStringTable a,ServerStringTable b){
        int shared=0,same=0;
        for(int i=0,n=Math.Min(a.Count,b.Count);i<n;i++){
            if(RefClientTextSource.Text(a,i) is not {} x||RefClientTextSource.Text(b,i) is not {} y)continue;
            shared++;if(x.Trim()==y.Trim())same++;
        }
        return shared<MinShared?null:(double)same/shared;
    }

    /// <summary>True when the fill table of this kind lines up with the reference table (computed once, logged).</summary>
    public bool Aligned(string kind){
        if(fill==null)return false;
        lock(gate){
            if(!agreement.TryGetValue(kind,out var share)){
                var p=primary.Table(kind);var f=fill.Table(kind);
                share=p!=null&&f!=null?Agreement(p.Table,f.Table):null;
                agreement[kind]=share;
                if(f!=null){
                    bool known=f.PlainSha256.Equals(f.FileName==RefClientTextSource.DescFileName?XiaocaoDescSha256:XiaocaoStringSha256,StringComparison.OrdinalIgnoreCase);
                    UiResourceLog.Write($"ui-text-fill {f.FileName} -> {f.Root}\\Text.pak:#{f.Record} plain sha256 {f.PlainSha256} (count {f.Table.Count}, {fill.Source}{(known?", 與小草相同":", 不是已知的小草版本")})，"
                        +(share is double s?$"與參考表相同 {s:P1}{(s>=MinAgreement?"，佔位字照此補":"，未達門檻，不補")}":"無法比對，不補"));
                }
            }
            return share>=MinAgreement;
        }
    }

    /// <summary>(TextKind, TextValue) → reference text; a dummy placeholder → the fill client's text at the same id (or null).</summary>
    public string? Resolve(string kind,string value){
        if(kind==null||value==null)return null;
        if(primary.Resolve(kind,value) is {} text)return text;
        if(fill==null)return null;
        lock(gate){if(cache.TryGetValue((kind,value),out var hit))return hit;}
        string? filled=primary.IsPlaceholder(kind,value)&&Aligned(kind)?fill.Resolve(kind,value):null;
        lock(gate){cache[(kind,value)]=filled;}
        return filled;
    }
}
