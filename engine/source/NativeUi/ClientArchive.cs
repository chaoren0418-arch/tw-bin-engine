using System.Security.Cryptography;
using System.Text;
namespace P06.NativeUi;

// C1 (Issue #461, docs/design/native-ui.md §2.1): shared read-only idx/pak reader for the native UI.
// Only hashes live in source; XML, .img and decoded pixels stay in TW_CLIENT_ROOT / memory.
//
// T479 (#1083, docs/design/asset-recipe.md 1.1 point 3, 1.2, §8 D1): no whole-container pin any more. The local Sprite03–14
// carry 36 unrelated appended .img (sprites.md §8.1), and the portable build must accept the user's own copy of the client.
// Every plain idx/pak reader (this ClientArchive, GroundSpriteReader, the preparer's PrepArchive, MapRawSource.PlainArchive)
// uses the same three layers:
//   1. structure (ClientArchiveIndex.Parse, refused): idx size = count × 28 + 4, every offset + size inside the pak, no
//      duplicate name (case-insensitive) in one container;
//   2. anchor entries (ClientArchivePin.Anchors, refused naming container and entry): payload SHA-256 of the few entries that
//      decide "is this the supported client version" (asset-recipe.md 1.2);
//   3. per entry: whoever already pins an entry (ground catalog, ship LAC, SEG, 6016…) keeps checking it as before.
// sprites.md §3.3 (main container first, alternates must agree) is SpriteContainerRule.Choose.

/// <summary>Resource data is missing, truncated, out of bounds or not the supported client version.</summary>
public class UiResourceException(string message,Exception? inner=null):IOException(message,inner);

/// <summary>One anchor entry: name (case-insensitive) and the SHA-256 of its stored bytes.</summary>
public sealed record ClientArchiveAnchor(string Entry,string Sha256);

/// <summary>
/// One idx/pak container by name, plus its anchor entries (asset-recipe.md 1.2). There is no whole-file hash or size:
/// a container with unrelated extra entries is accepted as long as the structure holds and every anchor matches.
/// </summary>
public sealed record ClientArchivePin(string Name,IReadOnlyList<ClientArchiveAnchor>? Anchors=null) {
    public IReadOnlyList<ClientArchiveAnchor> AnchorList=>Anchors??[];
}

public sealed record ClientArchiveEntry(string Archive,int Record,string Name,long Offset,int Size);

public static class UiResourcePins {
    public static string ClientRoot=>Environment.GetEnvironmentVariable("TW_CLIENT_ROOT")??@"D:\lineage開發\0-客戶端";
    /// <summary>
    /// Tile anchors = the three Tile rows of asset-recipe.md 1.2 (Water.lac, Pier1.lac, Pier5.lac; the SHA-256 that
    /// ShipBackdrop.cs pins, re-read on the local client for T479). They identify the supported Tile version.
    /// </summary>
    public static readonly ClientArchivePin Tile=new("Tile",[
        new("Water.lac","36055e0acb7e5234c1f1cce27d41ca102be736991fd29b0f5071ca7d4fc7f2b3"),
        new("Pier1.lac","2202bf29f48675004fb2e2e73d6296ccd2c30e0789d1d5ba882a57bd67609f0a"),
        new("Pier5.lac","c5e619634f9cb34c6c7e7f5fd0b340c213fe3f6fd78edf336e363bb558c52fd4"),
    ]);
    /// <summary>Sprite + Sprite00–15 (index 0 = Sprite.pak, 1+h = Sprite{h:00}.pak). asset-recipe.md 1.2 has no anchor inside
    /// these containers (the SPR version anchor is Text/list.spz); entries read from them are checked by their readers.</summary>
    public static readonly IReadOnlyList<ClientArchivePin> Sprites=Enumerable.Range(0,17).Select(i=>new ClientArchivePin(SpriteName(i))).ToArray();
    public static string SpriteName(int archive)=>archive==0?"Sprite":"Sprite"+(archive-1).ToString("00",System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>An idx/pak container failed the structure check. <see cref="Exception.Message"/> starts with the container name.
/// It is a <see cref="UiResourceException"/> (an IOException); readers that report InvalidDataException wrap it.</summary>
public sealed class ClientArchiveFormatException(string container,string? entry,string detail)
    :UiResourceException($"{container}: {detail}") {
    public string Container=>container;
    /// <summary>The entry the failure is about, or null for container-level faults (idx size, count).</summary>
    public string? Entry=>entry;
    /// <summary>The message without the container prefix.</summary>
    public string Detail=>detail;
}

/// <summary>
/// The structure rule of a plain idx/pak pair (asset-recipe.md 1.1 point 3, layer 1), shared by every reader.
/// idx: u32 count, then count × (u32 offset, 20-byte ASCII name, NUL-terminated or filling all 20 bytes, u32 size).
/// </summary>
public static class ClientArchiveIndex {
    public const int RecordSize=28,NameSize=20,MaxRecords=1_000_000;

    /// <summary>The whole idx file (read-only, FileShare.Read) after a size sanity check.</summary>
    public static byte[] ReadIndex(string container,string indexPath){
        using var file=new FileStream(indexPath,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length<4||file.Length>4+(long)RecordSize*MaxRecords)throw new ClientArchiveFormatException(container,null,"idx 大小不合理");
        var data=new byte[file.Length];file.ReadExactly(data);return data;
    }

    /// <summary>
    /// Entries by name (case-insensitive). Refuses: idx size ≠ count × 28 + 4, an empty or non-printable name, an entry whose
    /// offset + size exceeds <paramref name="pakSize"/>, a size over 2 GiB, a duplicate name in this container.
    /// </summary>
    public static Dictionary<string,ClientArchiveEntry> Parse(string container,ReadOnlySpan<byte> idx,long pakSize){
        if(idx.Length<4)throw new ClientArchiveFormatException(container,null,"idx 大小不合理");
        uint count=BitConverter.ToUInt32(idx[..4]);
        if(count>MaxRecords||idx.Length!=4+(long)RecordSize*count)throw new ClientArchiveFormatException(container,null,"idx 筆數×28+4 不等於檔案大小");
        var table=new Dictionary<string,ClientArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<count;i++){
            int at=4+i*RecordSize;
            long offset=BitConverter.ToUInt32(idx.Slice(at,4));
            // A name may fill all 20 bytes without a NUL (real Tile.idx record 5420: "ReNStatusWinUI-c.xml").
            var raw=idx.Slice(at+4,NameSize);int end=raw.IndexOf((byte)0);if(end<0)end=NameSize;
            if(end==0)throw new ClientArchiveFormatException(container,null,$"第 {i} 筆名稱為空");
            foreach(byte c in raw[..end])if(c is <32 or >=127)throw new ClientArchiveFormatException(container,null,$"第 {i} 筆名稱含不可列印字元");
            string name=Encoding.ASCII.GetString(raw[..end]);
            long size=BitConverter.ToUInt32(idx.Slice(at+4+NameSize,4));
            if(offset+size>pakSize)throw new ClientArchiveFormatException(container,name,$"{name} 越界（offset+size 超過 {container}.pak 的大小 {pakSize}；截斷或不同版本）");
            if(size>int.MaxValue)throw new ClientArchiveFormatException(container,name,$"{name} 大小 {size} 超過 2 GiB");
            // Duplicate names inside one container are refused instead of silently picking one.
            if(!table.TryAdd(name,new(container,i,name,offset,(int)size)))throw new ClientArchiveFormatException(container,name,$"{name} 在同一 idx 重名");
        }
        return table;
    }

    public static string Sha256(ReadOnlySpan<byte> data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
}

/// <summary>sprites.md §3.3: which Sprite container a file is taken from (this product's choice, not the original lookup order).</summary>
public static class SpriteContainerRule {
    /// <summary>
    /// <paramref name="read"/>(0) is the main container: when it has the name, it wins and no other container is read.
    /// Otherwise containers 1..count-1 are read in order; every copy found must be byte-identical. Two different contents
    /// → <paramref name="conflict"/>(first, other) is thrown (never one picked). Null when no container has the name.
    /// </summary>
    public static (int Archive,byte[] Data)? Choose(int count,Func<int,byte[]?> read,Func<int,int,Exception> conflict){
        if(count<=0)return null;
        if(read(0) is {} main)return (0,main);
        (int Archive,byte[] Data)? found=null;
        for(int i=1;i<count;i++){
            if(read(i) is not {} data)continue;
            if(found==null){found=(i,data);continue;}
            if(!found.Value.Data.AsSpan().SequenceEqual(data))throw conflict(found.Value.Archive,i);
        }
        return found;
    }
}

/// <summary>Debug-only log lines (e.g. ui-xml-fallback, missing surf). The original client logs none of these.</summary>
/// <remarks>T404 (#919): with <c>--evidence &lt;dir&gt;</c> on the command line every line is also appended, with a local
/// timestamp, to <c>&lt;dir&gt;/native-ui.log</c> for the real-machine smoke (gap-matrix 15.12.6/15.12.7). Without the
/// argument nothing changes. A failed write is swallowed (only remembered in <see cref="LastEvidenceError"/>).</remarks>
public static class UiResourceLog {
    public const string EvidenceFileName="native-ui.log";
    static readonly object gate=new();
    static readonly Queue<string> recent=new();
    static readonly System.Text.UTF8Encoding utf8=new(false);
    static string? evidenceDirectory;static bool evidenceResolved;
    /// <summary>Value after <c>--evidence</c> in <paramref name="argv"/>, or null when absent/empty.</summary>
    public static string? EvidenceArgument(IReadOnlyList<string> argv){
        for(int i=0;i+1<argv.Count;i++)if(argv[i]=="--evidence")return string.IsNullOrWhiteSpace(argv[i+1])?null:argv[i+1];
        return null;
    }
    /// <summary>Directory receiving native-ui.log; resolved once from the process command line. Settable for tests (null = off).</summary>
    public static string? EvidenceDirectory{
        get{lock(gate)return ResolveEvidence();}
        set{lock(gate){evidenceDirectory=string.IsNullOrWhiteSpace(value)?null:value;evidenceResolved=true;}}
    }
    public static string? LastEvidenceError{get;private set;}
    static string? ResolveEvidence(){
        if(!evidenceResolved){
            try{evidenceDirectory=EvidenceArgument(Environment.GetCommandLineArgs());}catch(Exception){evidenceDirectory=null;}
            evidenceResolved=true;
        }
        return evidenceDirectory;
    }
    public static void Write(string line){
        lock(gate){
            recent.Enqueue(line);while(recent.Count>200)recent.Dequeue();
            if(ResolveEvidence() is string dir)AppendEvidence(dir,line);
        }
        System.Diagnostics.Trace.WriteLine("[native-ui] "+line);
    }
    static void AppendEvidence(string dir,string line){
        try{
            string text=DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz",System.Globalization.CultureInfo.InvariantCulture)
                +" [native-ui] "+(line??"").Replace("\r\n"," ").Replace('\r',' ').Replace('\n',' ')+"\n";
            Directory.CreateDirectory(dir);
            using var file=new FileStream(Path.Combine(dir,EvidenceFileName),FileMode.Append,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete);
            file.Write(utf8.GetBytes(text));
            LastEvidenceError=null;
        }catch(Exception ex){LastEvidenceError=ex.GetType().Name+": "+ex.Message;} // never affects the game
    }
    public static string[] Recent(){lock(gate)return recent.ToArray();}
}

/// <summary>
/// One idx/pak pair, checked by the T479 three layers (see the top of this file). On first lookup the pak is opened
/// read-only (FileShare.Read, kept open so it cannot be replaced) and the idx is structure-checked against its size.
/// On first read every anchor entry of the pin is read and compared. Nothing is hashed whole.
/// </summary>
public sealed class ClientArchive(string root,ClientArchivePin pin):IDisposable {
    public const int MaxEntrySize=8*1024*1024;
    readonly object gate=new();
    Dictionary<string,ClientArchiveEntry>? entries;
    FileStream? pak;
    bool anchored;
    public ClientArchivePin Pin=>pin;
    public string IndexPath=>Path.Combine(root,pin.Name+".idx");
    public string PakPath=>Path.Combine(root,pin.Name+".pak");
    void Require(bool ok,string what){if(!ok)throw new UiResourceException($"{pin.Name}: {what}");}

    /// <summary>All entries (loads and structure-checks the idx). Missing idx → empty.</summary>
    public IReadOnlyDictionary<string,ClientArchiveEntry> Entries{get{lock(gate)return Load();}}
    public ClientArchiveEntry? Find(string name){lock(gate)return Load().GetValueOrDefault(name);}

    Dictionary<string,ClientArchiveEntry> Load(){
        if(entries!=null)return entries;
        if(!File.Exists(IndexPath)){entries=new(StringComparer.OrdinalIgnoreCase);return entries;}
        FileStream candidate;
        try{candidate=new FileStream(PakPath,FileMode.Open,FileAccess.Read,FileShare.Read);}
        catch(FileNotFoundException ex){throw new UiResourceException($"{pin.Name}: 缺少 pak",ex);}
        try{
            entries=ClientArchiveIndex.Parse(pin.Name,ClientArchiveIndex.ReadIndex(pin.Name,IndexPath),candidate.Length);
            pak=candidate;
        }catch{candidate.Dispose();throw;}
        return entries;
    }

    /// <summary>Layer 2: every anchor must be listed and hold the pinned bytes. Refusal names the container and the entry.</summary>
    void VerifyAnchors(){
        if(anchored)return;
        var table=Load();
        foreach(var anchor in pin.AnchorList){
            var entry=table.GetValueOrDefault(anchor.Entry)??throw new UiResourceException($"{pin.Name}: 缺少錨點 entry {anchor.Entry}（不是支援的客戶端版本）");
            string sha=ClientArchiveIndex.Sha256(ReadRaw(entry));
            Require(sha.Equals(anchor.Sha256,StringComparison.OrdinalIgnoreCase),$"錨點 entry {entry.Name} 與支援版本不符（SHA-256 {sha}）");
        }
        anchored=true;
    }

    public byte[] Read(ClientArchiveEntry entry){
        Require(entry.Archive==pin.Name,"entry 不屬於此封存檔");
        Require(entry.Size is >0 and <=MaxEntrySize,$"{entry.Name} 大小 {entry.Size} 超出 1–8 MiB 範圍");
        lock(gate){
            VerifyAnchors();
            return ReadRaw(entry);
        }
    }

    byte[] ReadRaw(ClientArchiveEntry entry){
        Require(entry.Size is >0 and <=MaxEntrySize,$"{entry.Name} 大小 {entry.Size} 超出 1–8 MiB 範圍");
        Load();
        if(pak==null)throw new UiResourceException($"{pin.Name}: 缺少 idx");
        Require(entry.Offset>=0&&entry.Offset+entry.Size<=pak.Length,$"{entry.Name} 越界");
        pak.Position=entry.Offset;var payload=new byte[entry.Size];
        try{pak.ReadExactly(payload);}catch(EndOfStreamException ex){throw new UiResourceException($"{pin.Name}: {entry.Name} 截斷",ex);}
        return payload;
    }
    public void Dispose(){lock(gate){pak?.Dispose();pak=null;entries=null;anchored=false;}}
}
