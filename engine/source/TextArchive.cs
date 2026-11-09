using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
namespace P06;

// T228 (#546): shared read-only reader for the original client's Text.idx / Text.pak.
// Extracted from EffectSpriteCatalog (MagicRendering.cs); format evidence: docs/protocol/attack-skill.md,
// docs/reference/protocol-gaps.md §3. Nothing is extracted to disk: payloads are decoded in memory only.
//   idx: u32 count, then count × 28-byte rows (u32 offset, 20-byte NUL-padded ASCII name, u32 size);
//        the rows (not the count) are DES-ECB encoded with the original Text archive constant.
//   pak: each payload is DES-ECB encoded the same way; a trailing partial block (< 8 bytes) is plain.
// Text.idx legitimately repeats names (unrelated dialog files), so lookups that need one file use Single().
public sealed class TextArchive {
    public sealed record Entry(int Record,string Name,uint Offset,uint Size);
    public const int MaxRecords=1_000_000,MaxIndexBytes=32*1024*1024,MaxPayloadBytes=16*1024*1024;
    const int RowSize=28,NameSize=20;
    // Original Text archive format constant (not an account credential).
    static readonly byte[] Key=Convert.FromHexString("7E214023255E243C");
    readonly Func<Entry,byte[]> read;
    public IReadOnlyList<Entry> Entries {get;}
    TextArchive(IReadOnlyList<Entry> entries,Func<Entry,byte[]> read){Entries=entries;this.read=read;}
    static void Require(bool ok,string message){if(!ok)throw new InvalidDataException("Text 封存："+message);}

    static byte[] Crypt(ReadOnlySpan<byte> data,bool encrypt){
        using var des=DES.Create();des.Key=Key;
        var result=data.ToArray();int length=data.Length/8*8;
        (encrypt?des.EncryptEcb(data[..length],PaddingMode.None):des.DecryptEcb(data[..length],PaddingMode.None)).CopyTo(result,0);
        return result;
    }
    /// <summary>Decodes Text idx rows or a Text pak payload.</summary>
    public static byte[] Decode(ReadOnlySpan<byte> data)=>Crypt(data,false);
    /// <summary>Inverse of <see cref="Decode"/>; only used to build synthetic test archives.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> data)=>Crypt(data,true);

    static List<Entry> ParseIndex(byte[] data,long pakSize){
        Require(data.Length>=4,"索引截斷");uint count=BinaryPrimitives.ReadUInt32LittleEndian(data);
        Require(count<=MaxRecords&&data.Length==4L+RowSize*(long)count,"僅支援 plain28 索引");
        byte[] rows=Decode(data.AsSpan(4));var result=new List<Entry>((int)count);
        for(int i=0;i<count;i++){
            var row=rows.AsSpan(i*RowSize,RowSize);uint offset=BinaryPrimitives.ReadUInt32LittleEndian(row),length=BinaryPrimitives.ReadUInt32LittleEndian(row[24..]);
            var raw=row.Slice(4,NameSize);int end=raw.IndexOf((byte)0);if(end<0)end=NameSize;
            string name=Encoding.ASCII.GetString(raw[..end]);
            Require(name.Length>0&&name.All(c=>c is >= ' ' and <= '~')&&(long)offset+length<=pakSize,"索引範圍或名稱錯誤");
            result.Add(new(i,name,offset,length));
        }
        return result;
    }

    /// <summary>Opens <c>{root}/{stem}.idx</c> and <c>.pak</c> read-only. The pak is reopened per read (FileShare.Read).</summary>
    public static TextArchive Open(string root,string stem="Text"){
        string idx=Path.Combine(root,stem+".idx"),pak=Path.Combine(root,stem+".pak");
        var file=new FileInfo(idx);Require(file.Exists,"缺少 "+stem+".idx");Require(file.Length<=MaxIndexBytes,"索引超出限制");
        byte[] data;using(var s=new FileStream(idx,FileMode.Open,FileAccess.Read,FileShare.Read)){data=new byte[s.Length];s.ReadExactly(data);}
        Require(File.Exists(pak),"缺少 "+stem+".pak");long pakSize=new FileInfo(pak).Length;
        return new(ParseIndex(data,pakSize),e=>{
            var payload=new byte[e.Size];
            using var stream=new FileStream(pak,FileMode.Open,FileAccess.Read,FileShare.Read);
            Require((long)e.Offset+e.Size<=stream.Length,"資源截斷");stream.Position=e.Offset;stream.ReadExactly(payload);return payload;
        });
    }
    /// <summary>In-memory archive (synthetic tests).</summary>
    public static TextArchive FromBytes(byte[] index,byte[] pak)=>new(ParseIndex(index,pak.Length),e=>pak.AsSpan((int)e.Offset,(int)e.Size).ToArray());

    /// <summary>The only record with this name (case-insensitive); null when absent; error when the name repeats.</summary>
    public Entry? Single(string name){
        Entry? found=null;
        foreach(var e in Entries)if(e.Name.Equals(name,StringComparison.OrdinalIgnoreCase)){Require(found==null,"索引名稱重複："+name);found=e;}
        return found;
    }
    /// <summary>Raw (still encoded) payload bytes.</summary>
    public byte[] ReadRaw(Entry entry){Require(entry.Size<=MaxPayloadBytes,"單筆資源超出限制");return read(entry);}
    /// <summary>Decoded payload bytes.</summary>
    public byte[] Read(Entry entry)=>Decode(ReadRaw(entry));

    // T272 (#649): MapSystem region names. CMapSystemXML (sub_A61D30, VA 0xA61D30 / FO 0x661130) loads its own
    // MapDesc-c.tbl (this+0x48); sub_A730A0 looks mapDesc ids up there, not in string-c.tbl (docs/reference/sprites-and-strings.md §4.1).
    // Same layout as string-c.tbl (first line = count, one record per line). Pin = SHA-256 of the decoded local payload (375 records).
    public const string MapDescFileName="MapDesc-c.tbl";
    public const string MapDescSha256="2bd9791403059b996404f8574b7f75cd3535560a0f006640c7766ab5e120a8d9";
    /// <summary>Reads and pin-checks the single <c>MapDesc-c.tbl</c> record (decoded in memory only).</summary>
    public ServerStringTable ReadMapDesc(){
        var entry=Single(MapDescFileName)??throw new InvalidDataException("Text 封存沒有 "+MapDescFileName);
        var decoded=Read(entry);
        Require(Convert.ToHexString(SHA256.HashData(decoded)).Equals(MapDescSha256,StringComparison.OrdinalIgnoreCase),MapDescFileName+" pin 不符");
        return ServerStringTable.Parse(decoded);
    }
}
