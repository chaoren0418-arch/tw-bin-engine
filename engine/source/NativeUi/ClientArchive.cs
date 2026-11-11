using System.Security.Cryptography;
using System.Text;
namespace P06.NativeUi;

// C1 (Issue #461, docs/design/native-ui.md §2.1): shared read-only idx/pak reader for the native UI.
// Only hashes and sizes live in source; XML, .img and decoded pixels stay in TW_CLIENT_ROOT / memory.
// GroundSpriteReader keeps its own private reader on purpose (its pins and tests stay untouched).

/// <summary>Resource data is missing, truncated, out of bounds or does not match the pinned archive.</summary>
public class UiResourceException(string message,Exception? inner=null):IOException(message,inner);

/// <summary>Pinned identity of one idx/pak pair (SHA-256 of both files and the exact pak size).</summary>
public sealed record ClientArchivePin(string Name,string IndexSha256,string PakSha256,long PakSize);

public sealed record ClientArchiveEntry(string Archive,int Record,string Name,long Offset,int Size);

public static class UiResourcePins {
    public static string ClientRoot=>Environment.GetEnvironmentVariable("TW_CLIENT_ROOT")??@"D:\lineage開發\0-客戶端";
    // Tile: independently read for #461; matches docs/reference/ambience-and-map-events.md and portable-delivery.md.
    public static readonly ClientArchivePin Tile=new("Tile","9291fd800ac8cae6dc355c1d8bbb965880561de169fbc7ea22d174fff9ad24db","82e52f47317f9a55ef775bbfce3c5f26f42b31a98748ffaa84ce5e2c3ed60d4a",593320968L);
    // Sprite + Sprite00–15: same values as GroundSpriteReader.archivePins (#190), duplicated so that reader stays unchanged.
    // Index 0 = Sprite.pak, index 1+h = Sprite{h:00}.pak.
    public static readonly IReadOnlyList<ClientArchivePin> Sprites=[
        new("Sprite","327c06372d87f6cf6d5176da51da5ef1f94dbf388d1588b735c5965b96b6a52b","4dd4e5348fbad2e69bae9cc388be23d7becebe8423ad9f79f393c1457a69ee62",1904012586L),
        new("Sprite00","15b46ae6995b5b3d64a27a509c78847f0329ba55360a200961a7ecb73b13c96c","64741119e853880e0e31009cf06145a5f21520b5264d07ece5541ba701a01c9d",450577933L),
        new("Sprite01","1e4c819debe37449691142025692ac1ff1d1f82c148830c76338e7fdff8cdd8f","a46a5a85e56c03cc6b1d7ed7cddf3d59e8887612313842cde9822b76932b5ac5",422714941L),
        new("Sprite02","a0208077cfa7e76280641f1a2bf7f7aeacc43b6f9a16e21771ebd732e791ae5c","88858f8eaf57854d50ea21f2d84dd3d3e8c1d3fb30c5c583af2175fc465cde0c",412811463L),
        new("Sprite03","4deb893637d40e1a9e91844a4a3274f9de25e7af42197e0ac69773241f233bf6","c8ee2e21f78c69eae9307db0dd7e721f929581ad07c386e984ee4735febb6c59",422961934L),
        new("Sprite04","6ea2246d89bfd1dc7ee3d4b70a36f0bf700889d1eb5f4cad9ab633a72bf539f9","ece9df4053fb06eab08f183d8c0b555de0ec04262fdae87f9775d9ce81ca82eb",396502716L),
        new("Sprite05","f4c6d5d7974ad355b066faf620aea9428104fa64274b5eeb24ce60bf54030158","606792a3a484065de833b1fcdaa1dc64f9b134da38945fc76084ce536f4d89a1",370792959L),
        new("Sprite06","0248238be94b99e61d2c515d59066b445af9faf5b3430132bbdce706d6812bcf","5ab3fc86d66557ebb0810228174b3702d7e07cd4bd766dfb1874661f7567d861",399491486L),
        new("Sprite07","ce996c526474647be2986c2610afe2e6b193f9d24069033615ffafb3a9136a60","30a4c7eccb62b3bec7d04a2891b8239e9bd98b3266e7406d0290d5ec7ab82065",428993498L),
        new("Sprite08","4f96ce84bcb0f733842b28b3dcb39984828985f6af994ce88aae214162782022","3d23ee72b9d70c61d1cd156e2ad99e4a100d46b13324c6440deb8d6386f1d8ef",405690494L),
        new("Sprite09","37edc3cbfb37ae667aa61a6ff3c141b7a7b351f60b6990d3bcee5c5eb55f8f9a","7256514d893846361b5e6854f5129a3f9e662fa44e41c3a4d7b44c0ce4a98b36",409910541L),
        new("Sprite10","589c47ecb1a56dc9d0956ad7716e599a00f68290005f401bbc595879e253ecb1","966089b4aac89ccf139833492b96ddc8c8aa342d8d0506d74f9de43c1571a128",401362413L),
        new("Sprite11","4d560607d2b290992d0cd6beaa7556cd6d03a009346418915636c01e06c20e88","cbcab1f612218ada11589a678f9ea85a07e393f62dea24529aded1c92712343e",408627491L),
        new("Sprite12","ada81449a16e323f90539cdf0571e59f120c52629855a9fe536e19c8e34481fe","a5f735d10a891150da0b0f9b97c81a0c8e8f3ba269e1595e1cc2352bd8ac9a7c",392469796L),
        new("Sprite13","e03cf669b50ee8fb86ac4e3242fb68a211580f78f8929770a9077d4fa776203b","216e300c8b6d0d7d312f95f2dc4323f9246c1d14a6cba63a0fc7ed172510408f",404075215L),
        new("Sprite14","c6d10d9c14ecac20c5b96546f9b4494eb23cc9b28c1ab07a96070eb574e84c9a","ba14f7a681a690deda925f812bcbfeb6af392bab7b292a0e48742455f933dcc8",442915587L),
        new("Sprite15","874b53afe762c43415e25204f4e8fcf405f36adfaca6b9258bd84c1eb1d4c441","864800d2afbd8b38ed9bfcc08a0079b27fbf53f018de562f4cba6fe54dccabd2",484407291L),
    ];
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
/// One idx/pak pair. idx: u32 count, then count × (u32 offset, 20-byte NUL-terminated ASCII name, u32 size).
/// The idx is hashed on first lookup; the pak is opened read-only (FileShare.Read) and hashed on first read.
/// </summary>
public sealed class ClientArchive(string root,ClientArchivePin pin):IDisposable {
    public const int MaxEntrySize=8*1024*1024;
    const int RecordSize=28,NameSize=20,MaxRecords=1_000_000;
    readonly object gate=new();
    Dictionary<string,ClientArchiveEntry>? entries;
    FileStream? pak;
    public ClientArchivePin Pin=>pin;
    public string IndexPath=>Path.Combine(root,pin.Name+".idx");
    public string PakPath=>Path.Combine(root,pin.Name+".pak");
    void Require(bool ok,string what){if(!ok)throw new UiResourceException($"{pin.Name}: {what}");}
    static string Hash(ReadOnlySpan<byte> data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>All entries (loads and verifies the idx). Missing idx → empty.</summary>
    public IReadOnlyDictionary<string,ClientArchiveEntry> Entries{get{lock(gate)return Load();}}
    public ClientArchiveEntry? Find(string name){lock(gate)return Load().GetValueOrDefault(name);}

    Dictionary<string,ClientArchiveEntry> Load(){
        if(entries!=null)return entries;
        if(!File.Exists(IndexPath)){entries=new(StringComparer.OrdinalIgnoreCase);return entries;}
        byte[] data;
        using(var file=new FileStream(IndexPath,FileMode.Open,FileAccess.Read,FileShare.Read)){
            Require(file.Length>=4&&file.Length<=4+(long)RecordSize*MaxRecords,"idx 大小不合理");
            data=new byte[file.Length];file.ReadExactly(data);
        }
        Require(Hash(data)==pin.IndexSha256.ToLowerInvariant(),"idx 與鎖定版本不符（SHA-256）");
        uint count=BitConverter.ToUInt32(data,0);
        Require(count<=MaxRecords&&data.Length==4+(long)RecordSize*count,"idx 筆數×28+4 不等於檔案大小");
        var table=new Dictionary<string,ClientArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<count;i++){
            int at=4+i*RecordSize;
            long offset=BitConverter.ToUInt32(data,at);
            // A name may fill all 20 bytes without a NUL (real Tile.idx record 5420: "ReNStatusWinUI-c.xml").
            var raw=data.AsSpan(at+4,NameSize);int end=raw.IndexOf((byte)0);if(end<0)end=NameSize;
            Require(end>0,$"第 {i} 筆名稱為空");
            foreach(byte c in raw[..end])Require(c is >=32 and <127,$"第 {i} 筆名稱含不可列印字元");
            string name=Encoding.ASCII.GetString(raw[..end]);
            uint size=BitConverter.ToUInt32(data,at+4+NameSize);
            Require(offset+size<=pin.PakSize,$"{name} 越界（offset+size 超過 pak 大小）");
            // Duplicate names inside one archive are rejected instead of silently picking one.
            Require(table.TryAdd(name,new(pin.Name,i,name,offset,(int)Math.Min(size,int.MaxValue))),$"{name} 在同一 idx 重名");
        }
        entries=table;return table;
    }

    public byte[] Read(ClientArchiveEntry entry){
        Require(entry.Archive==pin.Name,"entry 不屬於此封存檔");
        Require(entry.Size is >0 and <=MaxEntrySize,$"{entry.Name} 大小 {entry.Size} 超出 1–8 MiB 範圍");
        lock(gate){
            if(pak==null){
                FileStream candidate;
                try{candidate=new FileStream(PakPath,FileMode.Open,FileAccess.Read,FileShare.Read);}
                catch(FileNotFoundException ex){throw new UiResourceException($"{pin.Name}: 缺少 pak",ex);}
                try{
                    Require(candidate.Length==pin.PakSize,"pak 大小與鎖定版本不符");
                    Require(Convert.ToHexString(SHA256.HashData(candidate)).ToLowerInvariant()==pin.PakSha256.ToLowerInvariant(),"pak 與鎖定版本不符（SHA-256）");
                    pak=candidate;
                }catch{candidate.Dispose();throw;}
            }
            Require(entry.Offset>=0&&entry.Offset+entry.Size<=pak.Length,$"{entry.Name} 越界");
            pak.Position=entry.Offset;var payload=new byte[entry.Size];
            try{pak.ReadExactly(payload);}catch(EndOfStreamException ex){throw new UiResourceException($"{pin.Name}: {entry.Name} 截斷",ex);}
            return payload;
        }
    }
    public void Dispose(){lock(gate){pak?.Dispose();pak=null;}}
}
