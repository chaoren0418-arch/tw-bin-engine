using System.Globalization;
using System.Text;
namespace P06.NativeUi;

/// <summary>
/// surf id → decoded UI image (docs/design/native-ui.md §2.3, docs/reference/ui-layout.md V4 (a)(e)).
/// SurfList.xml (Tile.pak) maps &lt;surf id="N" file="F"/&gt;. The file attribute is always used (never id); in the real list
/// every row that has a file has id == file, and 343 rows have no file at all (→ null).
/// Lookup follows HashFileManager::Read (sub_52D8A0): Fc.img in Sprite{h}, Fc.img in Sprite, F.img in Sprite{h}, F.img in Sprite,
/// with h = (sum of the candidate name bytes) % 16. Decoded surfaces live only in a memory LRU (default 32 MiB).
/// </summary>
public sealed class SurfCatalog {
    public const long DefaultCapacity=32L*1024*1024;
    public const string SurfListName="SurfList.xml";
    /// <summary>Localized .img suffix used by the Surf/Icon groups (LocalFileNonDashFormatter: "&lt;n&gt;c.img").</summary>
    public const string LocalizedImgSuffix="c";
    readonly UiXmlSource tile;
    readonly IReadOnlyList<ClientArchive> sprites;
    readonly object gate=new();
    Dictionary<int,string>? files;
    readonly Dictionary<int,LinkedListNode<UiSurface>> cache=new();
    readonly LinkedList<UiSurface> lru=new();
    readonly HashSet<int> reported=new();
    public long Capacity{get;}
    public long CachedBytes{get;private set;}
    public int CachedCount{get{lock(gate)return cache.Count;}}

    /// <param name="sprites">Index 0 = Sprite, index 1+h = Sprite{h:00} (17 archives).</param>
    public SurfCatalog(UiXmlSource tile,IReadOnlyList<ClientArchive> sprites,long capacity=DefaultCapacity){
        if(sprites.Count!=17)throw new ArgumentException("需要 Sprite 與 Sprite00–15 共 17 個封存檔",nameof(sprites));
        if(capacity<=0)throw new ArgumentOutOfRangeException(nameof(capacity));
        this.tile=tile;this.sprites=sprites;Capacity=capacity;
    }

    public static int NameHash(string fileName){int sum=0;foreach(byte b in Encoding.ASCII.GetBytes(fileName))sum+=b;return sum%16;}

    public IReadOnlyDictionary<int,string> Files{get{lock(gate)return LoadList();}}

    Dictionary<int,string> LoadList(){
        if(files!=null)return files;
        var layout=tile.Load(SurfListName);
        var table=new Dictionary<int,string>();
        var noFile=new HashSet<int>();
        foreach(var node in Descend(layout.Root).Where(n=>n.Name.Equals("surf",StringComparison.OrdinalIgnoreCase))){
            int id=node.Int("id");
            if(id<0)throw new UiLayoutException(layout.FileName,node.Path,"surf id 為負數");
            // Real SurfList.xml: 343 rows (id 6310–7246) carry only filedesc. What the BIN does with them is unverified,
            // so they resolve to "no image" instead of guessing id as the file name.
            if(node.Attr("file") is not {} attr){
                if(table.ContainsKey(id)||!noFile.Add(id))throw new UiLayoutException(layout.FileName,node.Path,$"surf id {id} 重複");
                continue;
            }
            string file=attr.Trim();
            if(noFile.Contains(id))throw new UiLayoutException(layout.FileName,node.Path,$"surf id {id} 重複");
            if(file.Length==0||file.Length>14||!file.All(c=>c is >='!' and <='~')||file.IndexOfAny(['/','\\','.'])>=0)
                throw new UiLayoutException(layout.FileName,node.Path,$"file=\"{file}\" 不是合法的圖檔主檔名");
            if(!table.TryAdd(id,file))throw new UiLayoutException(layout.FileName,node.Path,$"surf id {id} 重複");
        }
        if(table.Count==0)throw new UiLayoutException(layout.FileName,"","沒有任何 surf 對照");
        withoutFile=noFile;files=table;return table;
    }
    HashSet<int> withoutFile=[];
    /// <summary>surf ids listed in SurfList.xml without a file attribute (resolve to null).</summary>
    public IReadOnlyCollection<int> IdsWithoutFile{get{lock(gate){LoadList();return withoutFile;}}}
    static IEnumerable<UiNode> Descend(UiNode n){foreach(var c in n.Children){yield return c;foreach(var d in Descend(c))yield return d;}}

    /// <summary>Candidate (archive, name) order for a SurfList file name.</summary>
    public IEnumerable<(ClientArchive Archive,string Name)> Candidates(string file){
        foreach(var name in new[]{file+LocalizedImgSuffix+".img",file+".img"}){
            yield return (sprites[1+NameHash(name)],name);
            yield return (sprites[0],name);
        }
    }

    /// <summary>Resolve without decoding; null when the surf id or every candidate file is missing.</summary>
    public (string File,ClientArchive Archive,ClientArchiveEntry Entry)? Resolve(int surf){
        lock(gate){
            if(!LoadList().TryGetValue(surf,out var file))return null;
            foreach(var (archive,name) in Candidates(file)){var e=archive.Find(name);if(e!=null)return (file,archive,e);}
            return null;
        }
    }

    /// <summary>Decoded surface or null (missing surf → one log line per id; callers then skip the element, no placeholder).</summary>
    public UiSurface? Get(int surf){
        lock(gate){
            if(cache.TryGetValue(surf,out var hit)){lru.Remove(hit);lru.AddFirst(hit);return hit.Value;}
            var found=Resolve(surf);
            if(found==null){
                if(reported.Add(surf))UiResourceLog.Write(LoadList().TryGetValue(surf,out var f)?$"surf {surf}: 圖檔不在 Sprite*.pak（file={f}）"
                    :withoutFile.Contains(surf)?$"surf {surf}: SurfList.xml 此列沒有 file 屬性":$"surf {surf}: SurfList.xml 沒有此 id");
                return null;
            }
            var (_,archive,entry)=found.Value;
            var image=UiImage.Decode(archive.Read(entry),$"{archive.Pin.Name}/{entry.Name}");
            var surface=new UiSurface(surf,entry.Name,archive.Pin.Name,image);
            var node=lru.AddFirst(surface);cache[surf]=node;CachedBytes+=surface.Cost;
            // Evict least-recently-used entries (never the one just added); evicted bitmaps are left to the GC.
            while(CachedBytes>Capacity&&lru.Last!=node){var last=lru.Last!;lru.RemoveLast();cache.Remove(last.Value.Surf);CachedBytes-=last.Value.Cost;}
            return surface;
        }
    }
    public bool IsCached(int surf){lock(gate)return cache.ContainsKey(surf);}
}

/// <summary>Tile + Sprite archives, XML source and surf catalog for one client root.</summary>
public sealed class UiResources:IDisposable {
    public string Root{get;}
    public ClientArchive Tile{get;}
    public IReadOnlyList<ClientArchive> Sprites{get;}
    public UiXmlSource Xml{get;}
    public SurfCatalog Surfs{get;}
    public UiResources(string root,ClientArchivePin tilePin,IReadOnlyList<ClientArchivePin> spritePins,long capacity=SurfCatalog.DefaultCapacity){
        if(spritePins.Count!=17)throw new ArgumentException("需要 17 個 Sprite pin",nameof(spritePins));
        Root=Path.GetFullPath(root);
        Tile=new ClientArchive(Root,tilePin);
        Sprites=spritePins.Select(p=>new ClientArchive(Root,p)).ToArray();
        Xml=new UiXmlSource(Tile);
        Surfs=new SurfCatalog(Xml,Sprites,capacity);
    }
    /// <summary>Pinned main client plus (T319 #757) the configured reference client for UI XML the main client lacks.</summary>
    public static UiResources Open(string? root=null){
        var res=new UiResources(root??UiResourcePins.ClientRoot,UiResourcePins.Tile,UiResourcePins.Sprites);
        res.Xml.Reference=RefClientXmlSource.Configured(res.Root);
        return res;
    }
    static readonly Lazy<UiResources> shared=new(()=>Open(),LazyThreadSafetyMode.ExecutionAndPublication);
    public static UiResources Shared=>shared.Value;
    public void Dispose(){Tile.Dispose();foreach(var s in Sprites)s.Dispose();}
}
