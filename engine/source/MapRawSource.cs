using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using P06.NativeUi;
namespace P06;

// T456 (#1041, docs/design/asset-recipe.md §3.3): where the raw map files come from.
//
//   Packaged  the assets tree is the r29 baseline (integrity.json schema p06-assets-integrity-v1, or a test fixture without
//             a schema): S32/SEG under assets/maps/<blocks[].path>, TIL under assets/maps/tiles (cache: assets/cache/tiles,
//             assets/cache/map/0), LAC under assets/maps/ship. This is the behaviour before T456, unchanged.
//   Client    the assets tree was made by the local preparer (integrity.json schema tw-assets-integrity-v2): nothing raw is
//             copied. S32/SEG are read from TW_CLIENT_ROOT/<blocks[].path> (map/<dir>/<file>), TIL and the three LAC files
//             from Tile.idx / Tile.pak by entry name (case-insensitive).
//
// The mode is decided by the integrity schema alone; one tree never mixes the two and nothing falls back to the other side.
// Every file is still checked against the SHA-256 the caller takes from maps/manifest.json (blocks, TIL), cache/manifest.json
// or the pins in code (SEG, LAC): a client changed after preparation fails the load. Tile.pak is not pinned as a whole
// (asset-recipe.md §1.1 point 3); its idx is only checked for structure.
public enum MapSourceMode { Packaged, Client }

/// <summary>
/// Read-only idx/pak pair checked for structure only: idx size = count × 28 + 4, every entry inside the pak, no duplicate
/// name (case-insensitive). Used by the map provider (Tile) and the preparer; it records the idx SHA-256 but pins nothing.
/// </summary>
public sealed class PlainArchive {
    public const int RecordSize=ClientArchiveIndex.RecordSize,NameSize=ClientArchiveIndex.NameSize,MaxRecords=ClientArchiveIndex.MaxRecords;
    public string Name{get;}
    public string IndexPath{get;}
    public string PakPath{get;}
    public string IndexSha256{get;}
    public long PakSize{get;}
    public IReadOnlyDictionary<string,ClientArchiveEntry> Entries{get;}
    PlainArchive(string name,string index,string pak,string sha,long size,Dictionary<string,ClientArchiveEntry> entries){Name=name;IndexPath=index;PakPath=pak;IndexSha256=sha;PakSize=size;Entries=entries;}

    /// <summary>Opens root/name.idx + root/name.pak. Missing file → FileNotFoundException; bad structure → InvalidDataException.</summary>
    public static PlainArchive Open(string root,string name){
        string index=Path.Combine(root,name+".idx"),pak=Path.Combine(root,name+".pak");
        foreach(string file in new[]{index,pak})if(!File.Exists(file))throw new FileNotFoundException($"{name}: 缺少 {Path.GetFileName(file)}",file);
        long pakSize=new FileInfo(pak).Length;
        // T479: the shared structure rule (ClientArchiveIndex); its refusal is reported as InvalidDataException as before.
        byte[] data;Dictionary<string,ClientArchiveEntry> table;
        try{data=ClientArchiveIndex.ReadIndex(name,index);table=ClientArchiveIndex.Parse(name,data,pakSize);}
        catch(ClientArchiveFormatException ex){throw new InvalidDataException(ex.Message,ex);}
        return new(name,index,pak,ClientArchiveIndex.Sha256(data),pakSize,table);
    }
    public ClientArchiveEntry? Find(string entry)=>Entries.GetValueOrDefault(entry);
    public byte[] Read(ClientArchiveEntry entry){
        if(entry.Archive!=Name)throw new InvalidDataException($"{entry.Name} 不屬於 {Name}");
        using var pak=new FileStream(PakPath,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(pak.Length!=PakSize)throw new InvalidDataException($"{Name}: pak 大小在開啟後改變");
        var payload=new byte[entry.Size];pak.Position=entry.Offset;
        try{pak.ReadExactly(payload);}catch(EndOfStreamException ex){throw new InvalidDataException($"{Name}: {entry.Name} 截斷",ex);}
        return payload;
    }
}

public sealed class MapRawSource {
    public const string IntegrityV1="p06-assets-integrity-v1",IntegrityV2="tw-assets-integrity-v2",TileContainer="Tile";
    static readonly Regex blockPath=new(@"\Amap/[0-9]{1,9}/[0-9A-Fa-f]{8}\.(s32|seg)\z",RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);
    static readonly Regex lacName=new(@"\A[A-Za-z0-9]{1,16}\.lac\z",RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);
    readonly object gate=new();
    PlainArchive? tile;
    public MapSourceMode Mode{get;}
    public string AssetsRoot{get;}
    /// <summary>Client mode only: the original client the files are read from.</summary>
    public string? ClientRoot{get;}
    MapRawSource(MapSourceMode mode,string assets,string? client){Mode=mode;AssetsRoot=assets;ClientRoot=client;}

    /// <summary>Client when assets/integrity.json has schema tw-assets-integrity-v2; Packaged otherwise (v1, no schema, or no integrity file).</summary>
    public static MapSourceMode ModeOf(string assetsRoot){
        string path=Path.Combine(assetsRoot,"integrity.json");
        if(!File.Exists(path))return MapSourceMode.Packaged;
        using var document=JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.TryGetProperty("schema",out var schema)&&schema.ValueKind==JsonValueKind.String&&schema.GetString()==IntegrityV2?MapSourceMode.Client:MapSourceMode.Packaged;
    }

    /// <param name="clientRoot">Client mode: the original client. Null → TW_CLIENT_ROOT as the running product sees it (UiResourcePins.ClientRoot).</param>
    public static MapRawSource Open(string assetsRoot,string? clientRoot=null){
        var mode=ModeOf(assetsRoot);
        if(mode==MapSourceMode.Packaged)return new(mode,assetsRoot,null);
        string root=clientRoot??UiResourcePins.ClientRoot;
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root))throw new DirectoryNotFoundException("地圖原檔需要原客戶端目錄（TW_CLIENT_ROOT，絕對路徑）");
        if(!Directory.Exists(root))throw new DirectoryNotFoundException("原客戶端目錄不存在："+root);
        return new(mode,assetsRoot,Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
    }

    public static byte[] Verify(byte[] bytes,string sha256,string what){
        if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original cache source hash: "+what);
        return bytes;
    }

    /// <summary>A file of the client directory; the last segment is matched case-insensitively (map 4 has upper-case .S32).</summary>
    public static string? FindClientFile(string directory,string fileName){
        string exact=Path.Combine(directory,fileName);
        if(File.Exists(exact))return exact;
        if(!Directory.Exists(directory))return null;
        var hits=Directory.EnumerateFiles(directory).Where(f=>Path.GetFileName(f).Equals(fileName,StringComparison.OrdinalIgnoreCase)).ToArray();
        return hits.Length==1?hits[0]:null;
    }

    byte[] ReadClient(string relative,string sha){
        if(!blockPath.IsMatch(relative))throw new InvalidDataException("地圖區塊路徑不合法："+relative);
        var parts=relative.Split('/');
        string file=FindClientFile(Path.Combine(ClientRoot!,parts[0],parts[1]),parts[2])??throw new FileNotFoundException("原客戶端缺少地圖區塊 "+relative,relative);
        return Verify(File.ReadAllBytes(file),sha,"TW_CLIENT_ROOT/"+relative);
    }

    PlainArchive Tile(){lock(gate)return tile??=PlainArchive.Open(ClientRoot!,TileContainer);}
    byte[] ReadTileEntry(string entry){
        var archive=Tile();
        var found=archive.Find(entry)??throw new FileNotFoundException($"{TileContainer} 沒有 {entry}",entry);
        return archive.Read(found);
    }
    byte[] ReadTileEntry(string entry,string sha)=>Verify(ReadTileEntry(entry),sha,$"{TileContainer}/{entry}");

    /// <summary>A block listed in maps/manifest.json: blocks[].path is map/&lt;dir&gt;/&lt;file&gt; in both modes (below assets/maps or TW_CLIENT_ROOT).</summary>
    public byte[] Block(string path,string sha256)=>Mode==MapSourceMode.Packaged
        ?Verify(File.ReadAllBytes(Path.Combine(AssetsRoot,"maps",path)),sha256,Path.Combine(AssetsRoot,"maps",path))
        :ReadClient(path,sha256);
    /// <summary>A map 0 block listed in cache/manifest.json (name = &lt;bx&gt;&lt;by&gt;.s32).</summary>
    public byte[] CacheBlock(string name,string sha256)=>Mode==MapSourceMode.Packaged
        ?Verify(File.ReadAllBytes(Path.Combine(AssetsRoot,"cache","map/0",name)),sha256,Path.Combine(AssetsRoot,"cache","map/0",name))
        :ReadClient("map/0/"+name,sha256);
    /// <summary>&lt;id&gt;.til: assets/maps/tiles (cache: assets/cache/tiles) or the Tile.pak entry.</summary>
    public byte[] Tile(int id,string sha256,bool cache=false){
        if(id is <0 or >65535)throw new InvalidDataException("TIL id "+id);
        if(Mode==MapSourceMode.Client)return ReadTileEntry(id+".til",sha256);
        string path=Path.Combine(AssetsRoot,cache?"cache":"maps","tiles",id+".til");
        return Verify(File.ReadAllBytes(path),sha256,path);
    }
    /// <summary>Water.lac / Pier1.lac / Pier5.lac: assets/maps/ship or the Tile.pak entry (stored there as pier1.lac, pier5.lac).</summary>
    public byte[] ShipLac(string name,string sha256){
        if(!lacName.IsMatch(name))throw new InvalidDataException("LAC 名稱不合法："+name);
        byte[] b=Mode==MapSourceMode.Client?ReadTileEntry(name):File.ReadAllBytes(Path.Combine(AssetsRoot,"maps/ship",name));
        if(!Convert.ToHexString(SHA256.HashData(b)).Equals(sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("ship backdrop integrity: "+name);
        return b;
    }
}
