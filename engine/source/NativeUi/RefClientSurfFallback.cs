using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace P06.NativeUi;

// T322 (#760, P-B of docs/reference/pak-resources.md §6): surfs that the local 3.81 client (0-客戶端) does not ship —
// 9183/9184 (RenewalMailUI close button) and the LoginUI / SelectUI / CreateUI / ResetStatUI images (PAK§4.1) — are read at
// run time from the reference client chosen for P-A (RefClientXmlSource: TW_REF_CLIENT → native-ui.json "refClient" →
// kor25 default, PAK§3), following the #533 precedent. Nothing is copied into the repository or written anywhere: the
// reference Sprite*.idx is read (and hashed) in memory, the one payload is read from Sprite*.pak by offset, and the idx
// and payload SHA-256 go to the log. The reference .pak files (0.4–1.9 GB each) are deliberately not hashed whole.
//
// Order, from SurfCatalog.Get only after the pinned local lookup failed:
//   1. File name = SurfList.xml "file" (main client's list) or, when the row has no file / the id is absent, the id itself.
//      The id form follows the surf loader sub_E55DD0, which formats "%d.img" (VA 0x11DFA2C, docs/reports/issue-758.md);
//      SurfList rows that do carry a file always have file == id. Still inference for the 343 file-less rows — 推測.
//   2. If the name came from the id: the local pinned Sprite archives first (main client wins, e.g. 6523.img / 6525.img).
//   3. The reference client's Sprite{h}.idx / Sprite.idx, same HashFileManager order as SurfCatalog.Candidates
//      ("<F>c.img" then "<F>.img", hashed archive then Sprite). Only .img — a same-numbered .tbt is an Icon (T320/#758).
// Anything missing, duplicated, out of range or undecodable → null; the caller keeps its existing fallback (skip the
// element / WinForms window). No substitute picture is drawn.

/// <summary>One surf payload found in the reference client, with the hashes that identify it.</summary>
public sealed record RefClientSurf(byte[] Data,string Archive,string Name,string Root,string IndexSha256,string BlobSha256);

public sealed class RefClientSurfFallback {
    public const int ArchiveCount=17;
    const int RecordSize=28,NameSize=20,MaxRecords=1_000_000;
    sealed class Index(Dictionary<string,(long Offset,int Size)> entries,HashSet<string> duplicates,string sha256){
        public Dictionary<string,(long Offset,int Size)> Entries=>entries;public HashSet<string> Duplicates=>duplicates;public string Sha256=>sha256;
    }
    readonly object gate=new();
    readonly Index?[] indexes=new Index?[ArchiveCount];
    readonly string?[] unavailable=new string?[ArchiveCount];
    readonly Dictionary<string,RefClientSurf?> cache=new(StringComparer.OrdinalIgnoreCase);

    public string Root{get;}
    /// <summary>Where the root came from ("env", "settings", "default" from RefClientXmlSource, or "explicit").</summary>
    public string Source{get;}
    public RefClientSurfFallback(string root,string source="explicit"){Root=Path.GetFullPath(root);Source=source;}
    /// <summary>Same reference client as the P-A XML source; null when that is off / not configured.</summary>
    public static RefClientSurfFallback? For(RefClientXmlSource? xml)=>xml==null?null:new(xml.Root,xml.Source);

    /// <summary>Index 0 = Sprite, index 1+h = Sprite{h:00} (same layout as SurfCatalog / UiResourcePins.Sprites).</summary>
    public static string ArchiveName(int index)=>index==0?"Sprite":$"Sprite{index-1:00}";
    public string IndexPath(int index)=>Path.Combine(Root,ArchiveName(index)+".idx");
    public string PakPath(int index)=>Path.Combine(Root,ArchiveName(index)+".pak");
    /// <summary>Why an archive cannot be read (missing / malformed idx or pak); null while usable or not yet tried.</summary>
    public string? Unavailable(int index){lock(gate)return unavailable[index];}
    /// <summary>Every surf payload served so far (diagnostics and the report).</summary>
    public IReadOnlyList<RefClientSurf> Served{get{lock(gate)return cache.Values.OfType<RefClientSurf>().ToArray();}}

    static string Hash(ReadOnlySpan<byte> data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>Candidate (archive index, name) order for a surf file name; identical to SurfCatalog.Candidates.</summary>
    public static IEnumerable<(int Archive,string Name)> Candidates(string file){
        foreach(var name in new[]{file+SurfCatalog.LocalizedImgSuffix+SurfCatalog.SurfExtension,file+SurfCatalog.SurfExtension}){
            yield return (1+SurfCatalog.NameHash(name),name);
            yield return (0,name);
        }
    }

    /// <summary>The raw .img payload for a SurfList file name, or null (not in the reference client / unreadable).</summary>
    public RefClientSurf? TryRead(string file){
        if(!ValidName(file))return null;
        lock(gate){
            if(cache.TryGetValue(file,out var hit))return hit;
            RefClientSurf? found=null;
            foreach(var (a,name) in Candidates(file)){
                if(Load(a) is not {} index)continue;
                if(index.Duplicates.Contains(name)){UiResourceLog.Write($"surf-ref {ArchiveName(a)}/{name} 在 idx 重名，不使用");continue;}
                if(!index.Entries.TryGetValue(name,out var e))continue;
                found=ReadEntry(a,name,e,index.Sha256);
                if(found!=null)break;
            }
            cache[file]=found;return found;
        }
    }

    static bool ValidName(string file)=>file.Length is >0 and <=14&&file.All(c=>c is >='!' and <='~')&&file.IndexOfAny(['/','\\','.'])<0;

    Index? Load(int a){
        if(indexes[a] is {} done)return done;
        if(unavailable[a]!=null)return null;
        try{
            if(!File.Exists(IndexPath(a))||!File.Exists(PakPath(a))){unavailable[a]=$"{Root} 沒有 {ArchiveName(a)}.idx／.pak";UiResourceLog.Write("surf-ref "+unavailable[a]);return null;}
            byte[] idx;
            using(var file=new FileStream(IndexPath(a),FileMode.Open,FileAccess.Read,FileShare.Read)){
                if(file.Length<4||file.Length>4+(long)RecordSize*MaxRecords)throw new InvalidDataException($"{ArchiveName(a)}.idx 大小不合理");
                idx=new byte[file.Length];file.ReadExactly(idx);
            }
            uint count=BitConverter.ToUInt32(idx,0);
            if(count>MaxRecords||(long)count*RecordSize+4!=idx.Length)throw new InvalidDataException($"{ArchiveName(a)}.idx 筆數×28+4 不等於檔案大小");
            var table=new Dictionary<string,(long,int)>(StringComparer.OrdinalIgnoreCase);var dup=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<count;i++){
                int at=4+i*RecordSize;var raw=idx.AsSpan(at+4,NameSize);int end=raw.IndexOf((byte)0);if(end<0)end=NameSize;
                if(end==0)continue;
                bool printable=true;foreach(byte c in raw[..end])if(c is <32 or >=127){printable=false;break;}
                if(!printable)continue; // a foreign client's odd names are skipped, never fatal
                string name=Encoding.ASCII.GetString(raw[..end]);
                uint size=BitConverter.ToUInt32(idx,at+4+NameSize);
                if(!table.TryAdd(name,(BitConverter.ToUInt32(idx,at),(int)Math.Min(size,int.MaxValue))))dup.Add(name);
            }
            return indexes[a]=new Index(table,dup,Hash(idx));
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){
            unavailable[a]=ex.Message;UiResourceLog.Write("surf-ref 無法讀取："+ex.Message);return null;
        }
    }

    RefClientSurf? ReadEntry(int a,string name,(long Offset,int Size) e,string indexSha){
        try{
            using var pak=new FileStream(PakPath(a),FileMode.Open,FileAccess.Read,FileShare.Read);
            if(e.Size is <=0 or >ClientArchive.MaxEntrySize||e.Offset<0||e.Offset+e.Size>pak.Length){
                UiResourceLog.Write($"surf-ref {ArchiveName(a)}/{name} 越界或大小不合理（{e.Offset}+{e.Size}）");return null;
            }
            var blob=new byte[e.Size];pak.Position=e.Offset;pak.ReadExactly(blob);
            return new(blob,ArchiveName(a),name,Root,indexSha,Hash(blob));
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){
            UiResourceLog.Write($"surf-ref {ArchiveName(a)}/{name} 讀取失敗：{ex.Message}");return null;
        }
    }

    /// <summary>
    /// Hook for SurfCatalog.Get after the pinned local lookup failed (see the order at the top of this file). Returns the
    /// decoded surface (Archive = "Sprite00" for a local id-named hit, "ref:Sprite00" for a reference hit) or null.
    /// </summary>
    public UiSurface? Serve(SurfCatalog catalog,int surf){
        bool listed=catalog.Files.TryGetValue(surf,out var listedFile);
        string file=listed?listedFile!:surf.ToString(CultureInfo.InvariantCulture);
        if(!listed){
            foreach(var (archive,name) in catalog.Candidates(file)){
                if(archive.Find(name) is not {} entry)continue;
                var local=new UiSurface(surf,entry.Name,archive.Pin.Name,UiImage.Decode(archive.Read(entry),$"{archive.Pin.Name}/{entry.Name}"));
                UiResourceLog.Write($"surf {surf}: SurfList.xml 無 file，依 %d.img 用本機 {archive.Pin.Name}/{entry.Name}");
                return local;
            }
        }
        if(TryRead(file) is not {} found)return null;
        try{
            var surface=new UiSurface(surf,found.Name,"ref:"+found.Archive,UiImage.Decode(found.Data,$"{Root}\\{found.Archive}.pak:{found.Name}"));
            UiResourceLog.Write($"surf-ref {surf} -> {Root}\\{found.Archive}.pak:{found.Name} sha256 {found.BlobSha256} (idx {found.IndexSha256}, {Source})");
            return surface;
        }catch(UiResourceException ex){UiResourceLog.Write($"surf-ref {surf} {found.Archive}/{found.Name} 不是可解的 .img：{ex.Message}");return null;}
    }
}
