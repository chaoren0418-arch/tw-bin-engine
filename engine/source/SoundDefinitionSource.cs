using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace P06;

// T172（#446）：執行期以唯讀方式從使用者原客戶端的 Text.idx／Text.pak 取出 music.def、envsound.def。
// 格式依 docs/reference/sound.md §6.1（T171）：idx 首 4 bytes 為筆數，其後整段 DES-ECB；每列 28 bytes
// = uint32 offset + name[20]（NUL 截斷，可滿 20 bytes）+ uint32 size。payload 也做 DES-ECB，不足 8 bytes 的尾端原樣保留。
// 不寫檔、不快取到磁碟、不封裝任何解碼內容。
public sealed record SoundDefinitions(MusicDefinition? Music,EnvSoundDefinition? EnvSound,string Status);

public static class SoundDefinitionSource
{
    public const string MusicName="music.def",EnvSoundName="envsound.def";
    const int RowSize=28,MaxRows=1_000_000,MaxPayload=4*1024*1024;
    // 原版 Text 容器格式常數（不是帳號密碼）。與 MagicRendering.cs 相同。
    static readonly byte[] Key=Convert.FromHexString("7E214023255E243C");

    public static byte[] Transform(byte[] data,bool encrypt)
    {
        using var des=DES.Create();des.Key=Key;
        var result=(byte[])data.Clone();int length=data.Length/8*8;
        if(length>0)(encrypt?des.EncryptEcb(data.AsSpan(0,length),PaddingMode.None):des.DecryptEcb(data.AsSpan(0,length),PaddingMode.None)).CopyTo(result,0);
        return result;
    }

    // 回傳 name → (offset,size)。名稱比對不分大小寫（原版容器查找的大小寫規則未核定；兩個目標檔名都是小寫）。
    public static Dictionary<string,(uint Offset,uint Size)> Index(byte[] idx,long pakLength,params string[] wanted)
    {
        if(idx.Length<4)throw new InvalidDataException("Text.idx 截斷");
        uint count=BinaryPrimitives.ReadUInt32LittleEndian(idx);
        if(count>MaxRows||idx.Length!=4L+RowSize*(long)count)throw new InvalidDataException("Text.idx 不是 plain28 格式");
        byte[] rows=Transform(idx[4..],false);
        var result=new Dictionary<string,(uint,uint)>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<count;i++)
        {
            var row=rows.AsSpan(i*RowSize,RowSize);var raw=row.Slice(4,20);int nul=raw.IndexOf((byte)0);
            string name=Encoding.ASCII.GetString(nul<0?raw:raw[..nul]);
            if(!wanted.Contains(name,StringComparer.OrdinalIgnoreCase))continue;
            uint offset=BinaryPrimitives.ReadUInt32LittleEndian(row),size=BinaryPrimitives.ReadUInt32LittleEndian(row[24..]);
            if((long)offset+size>pakLength||size>MaxPayload)throw new InvalidDataException($"Text.idx {name} 範圍超出 Text.pak");
            if(!result.TryAdd(name,(offset,size)))throw new InvalidDataException($"Text.idx {name} 重複");
        }
        return result;
    }

    public static SoundDefinitions Load(string clientRoot)
    {
        string idxPath=Path.Combine(clientRoot,"Text.idx"),pakPath=Path.Combine(clientRoot,"Text.pak");
        try
        {
            var idxInfo=new FileInfo(idxPath);
            if(!idxInfo.Exists||!File.Exists(pakPath))return new(null,null,"Text.idx／Text.pak 不存在");
            if(idxInfo.Length>32*1024*1024)return new(null,null,"Text.idx 過大");
            byte[] idx=File.ReadAllBytes(idxPath);
            using var pak=new FileStream(pakPath,FileMode.Open,FileAccess.Read,FileShare.Read);
            var index=Index(idx,pak.Length,MusicName,EnvSoundName);
            string? Read(string name)
            {
                if(!index.TryGetValue(name,out var entry))return null;
                var data=new byte[entry.Size];pak.Position=entry.Offset;pak.ReadExactly(data);
                return Encoding.ASCII.GetString(Transform(data,false));
            }
            string? music=Read(MusicName),env=Read(EnvSoundName);
            var result=new SoundDefinitions(music==null?null:MusicDefinition.Parse(music),env==null?null:EnvSoundDefinition.Parse(env),
                music==null||env==null?$"缺少 {(music==null?MusicName:"")} {(env==null?EnvSoundName:"")}".Trim():"ready");
            return result;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or CryptographicException or EndOfStreamException)
        { return new(null,null,"讀取失敗："+ex.Message); }
    }
}
