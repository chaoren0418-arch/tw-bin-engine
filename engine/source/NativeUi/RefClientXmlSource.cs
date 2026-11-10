using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace P06.NativeUi;

// T319 (#757, P-A of docs/reference/pak-resources.md §6): UI XML that the local 3.81 client (0-客戶端) does not ship
// (LoginUI, SelectUI, CreateUI, RenewalMailUI, ResetStatUI, StatInfoUI, ReNStatusUIEx, AgitAuctionUI, FishingUI, OptionUI,
// GameRankingUI …, PAK§4) is read at run time from a local reference client's Tile.idx/Tile.pak — default kor25, PAK§3 —
// following the #533 precedent of TW_SPELLLIST_CLIENT (ActionCadence.cs). The XML is decrypted in memory and identified
// by SHA-256 (idx, stored blob, decrypted text); nothing is copied into the repository or written anywhere.
// The main client always wins: UiXmlSource only asks this source after neither "<name>-c.xml" nor "<name>.xml" exists
// in the pinned 0-客戶端 Tile.pak. When the reference client is missing, disabled or has no such entry, the result is
// null and the caller keeps its existing fallback (WinForms window / chat line); no substitute layout is invented.

/// <summary>One XML found in the reference client, with the hashes that identify it.</summary>
public sealed record RefClientXml(byte[] Plain,string ResolvedName,string Root,string IndexSha256,string BlobSha256,string PlainSha256,bool Encrypted);

public sealed class RefClientXmlSource {
    public const string EnvironmentVariable="TW_REF_CLIENT";
    /// <summary>Key in &lt;stage&gt;/runtime/native-ui.json: a path string, or false / "" to turn the reference client off.</summary>
    public const string SettingsKey="refClient";
    /// <summary>PAK§3: kor25 is the primary reference client (same default as TW_SPELLLIST_CLIENT).</summary>
    public const string DefaultRoot=@"D:\381懶人包kor25(原)";
    const int RecordSize=28,NameSize=20,MaxRecords=1_000_000;

    readonly object gate=new();
    Dictionary<string,(string Name,long Offset,int Size)>? entries;
    HashSet<string>? duplicates;
    string? indexSha256;
    string? unavailable;
    readonly Dictionary<string,RefClientXml?> cache=new(StringComparer.OrdinalIgnoreCase);
    byte[]? idxImage,pakImage; // offline tests only (FromImages)

    public string Root{get;}
    /// <summary>Where the root came from: "env", "settings" or "default".</summary>
    public string Source{get;}
    public RefClientXmlSource(string root,string source="explicit"){Root=Path.GetFullPath(root);Source=source;}
    public string IndexPath=>Path.Combine(Root,"Tile.idx");
    public string PakPath=>Path.Combine(Root,"Tile.pak");
    /// <summary>Why nothing can be read (missing / unreadable Tile.idx or Tile.pak); null while usable or not yet tried.</summary>
    public string? Unavailable{get{lock(gate)return unavailable;}}
    /// <summary>Every XML served so far (for diagnostics and the report: name → hashes).</summary>
    public IReadOnlyList<RefClientXml> Served{get{lock(gate)return cache.Values.OfType<RefClientXml>().DistinctBy(x=>x.ResolvedName,StringComparer.OrdinalIgnoreCase).ToArray();}}

    static string Hash(ReadOnlySpan<byte> data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>Default settings file, same place UiClientBridge reads: &lt;stage&gt;/runtime/native-ui.json.</summary>
    public static string DefaultSettingsPath=>Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../..")),"runtime",UiOptions.SettingsFileName);

    /// <summary>
    /// Precedence: TW_REF_CLIENT (non-empty) → native-ui.json "refClient" (string path; false or "" = off) → kor25 default.
    /// A malformed settings file or a refClient value of another type is ignored (logged), like UiOptions.Resolve.
    /// Returns null when turned off.
    /// </summary>
    public static (string? Root,string Source) ResolveRoot(string? environment,string? settingsPath){
        if(!string.IsNullOrWhiteSpace(environment))return (environment.Trim(),"env");
        if(settingsPath!=null&&File.Exists(settingsPath)){
            try{
                using var doc=JsonDocument.Parse(File.ReadAllText(settingsPath));
                if(doc.RootElement.ValueKind==JsonValueKind.Object&&doc.RootElement.TryGetProperty(SettingsKey,out var v)){
                    if(v.ValueKind==JsonValueKind.False)return (null,"settings");
                    if(v.ValueKind==JsonValueKind.String)return string.IsNullOrWhiteSpace(v.GetString())?(null,"settings"):(v.GetString()!.Trim(),"settings");
                    UiResourceLog.Write($"{UiOptions.SettingsFileName} 的 {SettingsKey} 不是字串或 false，忽略");
                }
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException){UiResourceLog.Write($"{UiOptions.SettingsFileName} 無法讀取，參考客戶端用預設：{ex.Message}");}
        }
        return (DefaultRoot,"default");
    }

    /// <summary>The configured reference client, or null when turned off, not absolute, or the same folder as the main client.</summary>
    public static RefClientXmlSource? Configured(string? mainRoot=null,string? settingsPath=null){
        var (root,source)=ResolveRoot(Environment.GetEnvironmentVariable(EnvironmentVariable),settingsPath??DefaultSettingsPath);
        if(root==null){UiResourceLog.Write("ref-client off");return null;}
        if(!Path.IsPathFullyQualified(root)){UiResourceLog.Write($"ref-client 路徑不是絕對路徑，忽略：{root}");return null;}
        var full=Path.GetFullPath(root);
        if(mainRoot!=null&&string.Equals(Path.TrimEndingDirectorySeparator(full),Path.TrimEndingDirectorySeparator(Path.GetFullPath(mainRoot)),StringComparison.OrdinalIgnoreCase)){
            UiResourceLog.Write("ref-client 與主客戶端是同一資料夾，忽略");return null;
        }
        return new RefClientXmlSource(full,source);
    }

    /// <summary>
    /// FileManager order (sub_52D6D0): "&lt;stem&gt;-c.&lt;ext&gt;" then the plain name. Null when the reference client is
    /// missing or unreadable, the name is absent or stored twice, or the entry is out of range / not XML.
    /// </summary>
    public RefClientXml? TryRead(string fileName){
        if(string.IsNullOrWhiteSpace(fileName)||fileName.IndexOfAny(['/','\\'])>=0)return null;
        string name=Path.HasExtension(fileName)?fileName:fileName+".xml";
        lock(gate){
            if(cache.TryGetValue(name,out var hit))return hit;
            RefClientXml? found=null;
            if(Load() is {} table){
                string localized=UiXmlSource.LocalizedName(name);
                foreach(var candidate in localized.Equals(name,StringComparison.OrdinalIgnoreCase)?new[]{name}:new[]{localized,name}){
                    if(duplicates!.Contains(candidate)){UiResourceLog.Write($"ref-client {candidate} 在 Tile.idx 重名，不使用");break;}
                    if(!table.TryGetValue(candidate,out var e))continue;
                    found=ReadEntry(e.Name,e);break;
                }
            }
            if(found!=null)UiResourceLog.Write($"ui-xml-ref {name} -> {Root}\\Tile.pak:{found.ResolvedName} plain sha256 {found.PlainSha256} (blob {found.BlobSha256}, idx {found.IndexSha256}, {Source})");
            else if(unavailable==null)UiResourceLog.Write($"ui-xml-ref {name}: 參考客戶端也沒有");
            cache[name]=found;return found;
        }
    }

    Dictionary<string,(string Name,long Offset,int Size)>? Load(){
        if(entries!=null)return entries;
        if(unavailable!=null)return null;
        try{
            if(idxImage!=null)return Index(idxImage);
            if(!File.Exists(IndexPath)||!File.Exists(PakPath)){unavailable=$"{Root} 沒有 Tile.idx／Tile.pak";UiResourceLog.Write("ref-client "+unavailable);return null;}
            byte[] idx;
            using(var file=new FileStream(IndexPath,FileMode.Open,FileAccess.Read,FileShare.Read)){
                if(file.Length<4||file.Length>4+(long)RecordSize*MaxRecords)throw new InvalidDataException("Tile.idx 大小不合理");
                idx=new byte[file.Length];file.ReadExactly(idx);
            }
            return Index(idx);
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){
            unavailable=ex.Message;UiResourceLog.Write("ref-client 無法讀取："+ex.Message);return null;
        }
    }

    /// <summary>Parses an idx image (u32 count, count × (u32 offset, 20-byte name, u32 size)). Internal for the offline tests.</summary>
    internal Dictionary<string,(string Name,long Offset,int Size)> Index(byte[] idx){
        if(idx.Length<4)throw new InvalidDataException("Tile.idx 太短");
        uint count=BitConverter.ToUInt32(idx,0);
        if(count>MaxRecords||(long)count*RecordSize+4!=idx.Length)throw new InvalidDataException("Tile.idx 筆數×28+4 不等於檔案大小");
        var table=new Dictionary<string,(string,long,int)>(StringComparer.OrdinalIgnoreCase);var dup=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<count;i++){
            int at=4+i*RecordSize;var raw=idx.AsSpan(at+4,NameSize);int end=raw.IndexOf((byte)0);if(end<0)end=NameSize;
            if(end==0)continue;
            bool printable=true;foreach(byte c in raw[..end])if(c is <32 or >=127){printable=false;break;}
            if(!printable)continue; // a foreign client's odd names are skipped, never fatal
            string name=Encoding.ASCII.GetString(raw[..end]);
            uint size=BitConverter.ToUInt32(idx,at+4+NameSize);
            if(!table.TryAdd(name,(name,BitConverter.ToUInt32(idx,at),(int)Math.Min(size,int.MaxValue))))dup.Add(name);
        }
        indexSha256=Hash(idx);duplicates=dup;entries=table;return table;
    }

    RefClientXml? ReadEntry(string name,(string Name,long Offset,int Size) e){
        try{
            if(pakImage!=null)return ReadEntry(name,e,new MemoryStream(pakImage,false));
            using var pak=new FileStream(PakPath,FileMode.Open,FileAccess.Read,FileShare.Read);
            return ReadEntry(name,e,pak);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){UiResourceLog.Write($"ref-client {name} 讀取失敗：{ex.Message}");return null;}
    }

    internal RefClientXml? ReadEntry(string name,(string Name,long Offset,int Size) e,Stream pak){
        if(e.Size is <=0 or >ClientArchive.MaxEntrySize||e.Offset<0||e.Offset+e.Size>pak.Length){UiResourceLog.Write($"ref-client {name} 越界或大小不合理（{e.Offset}+{e.Size}）");return null;}
        var blob=new byte[e.Size];pak.Position=e.Offset;
        try{pak.ReadExactly(blob);}catch(EndOfStreamException){UiResourceLog.Write($"ref-client {name} 截斷");return null;}
        bool encrypted=UiXmlSource.IsEncrypted(blob);
        byte[] plain;
        if(encrypted)plain=UiXmlSource.Decrypt(blob);
        else if(LooksLikeXml(blob))plain=blob;
        else{UiResourceLog.Write($"ref-client {name} 不是 XML（既非 X?xm 加密也不是 <）");return null;}
        return new(plain,name,Root,indexSha256!,Hash(blob),Hash(plain),encrypted);
    }

    static bool LooksLikeXml(ReadOnlySpan<byte> blob){
        if(blob.Length>=3&&blob[0]==0xEF&&blob[1]==0xBB&&blob[2]==0xBF)blob=blob[3..];
        foreach(byte b in blob){if(b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')continue;return b==(byte)'<';}
        return false;
    }

    /// <summary>Test seam: serve from an in-memory idx/pak image instead of Root\Tile.idx/Tile.pak.</summary>
    internal static RefClientXmlSource FromImages(string root,byte[] idx,byte[] pak)=>new(root,"synthetic"){idxImage=idx,pakImage=pak};
}
