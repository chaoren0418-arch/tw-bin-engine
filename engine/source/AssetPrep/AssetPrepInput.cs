using P06.NativeUi;
namespace P06.AssetPrep;

// T451 (#1036): read-only input side of the preparer.
// - The client root is the caller's explicit absolute path. There is no default and no fallback to a development
//   machine, release or research directory (unlike UiResourcePins.ClientRoot, which the running product uses).
// - Container identity is checked by the existing readers: ClientArchive (idx SHA-256 on load, pak size + SHA-256 on first
//   read) and TextArchive (structure; Text has no whole-file pin, entries are pinned by decoded SHA-256).
// - Every refusal names the container (AssetPrepException.Container).

/// <summary>Pinned containers a preparer run may read. Index 0 = Sprite, 1+h = Sprite{h:00} (the UiResourcePins.Sprites layout).</summary>
public sealed record AssetPrepPins(IReadOnlyList<ClientArchivePin> Sprites) {
    public const int SpriteArchiveCount=17;
    /// <summary>The supported TW1608262200 client (same pins as the native UI).</summary>
    public static AssetPrepPins Tw1608262200{get;}=new(UiResourcePins.Sprites);
}

/// <summary>One container entry that went into an item: where it was and the SHA-256 of its stored bytes.</summary>
public sealed record AssetSource(string Container,string Entry,long Offset,int Size,string Sha256);

/// <summary>Identity of one input container as verified by this run. PakVerified = the whole pak was hashed (it is on first read).</summary>
public sealed record AssetContainerRecord(string Name,string IndexSha256,string PakSha256,long PakSize,bool PakVerified);

public sealed class AssetPrepInput:IDisposable {
    public const string TextContainer="Text";
    readonly ClientArchive[] sprites;
    readonly bool[] pakVerified;
    TextArchive? text;
    PlainArchive? tile;
    bool mapFiles;
    public string ClientRoot{get;}
    public AssetPrepPins Pins{get;}

    AssetPrepInput(string root,AssetPrepPins pins,ClientArchive[] sprites){ClientRoot=root;Pins=pins;this.sprites=sprites;pakVerified=new bool[sprites.Length];}

    /// <summary>The TW_CLIENT_ROOT environment variable, exactly as set. Not set → refused (no default path).</summary>
    public static string ClientRootFromEnvironment()=>
        Environment.GetEnvironmentVariable("TW_CLIENT_ROOT") is {Length:>0} value?value
            :throw new AssetPrepException(AssetPrepFailure.ClientRoot,"未設定 TW_CLIENT_ROOT；請指定自己的原客戶端目錄（絕對路徑）");

    public static string RequireClientRoot(string? clientRoot){
        string root=AssetPrepPaths.RequireAbsolute(clientRoot,AssetPrepFailure.ClientRoot,"原客戶端目錄（TW_CLIENT_ROOT）");
        if(!Directory.Exists(root))throw new AssetPrepException(AssetPrepFailure.ClientRoot,$"原客戶端目錄不存在：{root}");
        return root;
    }

    /// <summary>
    /// Opens every pinned Sprite container: both files must exist, the pak must have the pinned size and the idx the pinned
    /// SHA-256 and a valid structure. The pak SHA-256 is checked by ClientArchive on the first read of that container.
    /// </summary>
    public static AssetPrepInput Open(string? clientRoot,AssetPrepPins pins)=>Open(clientRoot,pins,AssetPrepSources.Sprites);

    /// <summary>T456: opens only what the recipe reads. Without <see cref="AssetPrepSources.Sprites"/> no Sprite container is opened or recorded.</summary>
    public static AssetPrepInput Open(string? clientRoot,AssetPrepPins pins,AssetPrepSources sources){
        string root=RequireClientRoot(clientRoot);
        if((sources&AssetPrepSources.Sprites)==0){
            var input=new AssetPrepInput(root,pins,[]);
            if((sources&AssetPrepSources.Tile)!=0)input.tile=OpenTile(root);
            if((sources&AssetPrepSources.MapFiles)!=0){
                if(!Directory.Exists(Path.Combine(root,MapContainer)))throw new AssetPrepException(AssetPrepFailure.MissingContainer,$"TW_CLIENT_ROOT 缺少 {MapContainer}/ 目錄（{root}）",MapContainer);
                AssetPrepPaths.RejectReparsePoints(Path.Combine(root,MapContainer),"原客戶端 map 目錄");
                input.mapFiles=true;
            }
            return input;
        }
        if(sources!=AssetPrepSources.Sprites)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"Sprite 與 Tile／map 來源不可在同一個 recipe 混用");
        if(pins.Sprites.Count!=AssetPrepPins.SpriteArchiveCount)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"Sprite 容器 pin 應為 {AssetPrepPins.SpriteArchiveCount} 個");
        var opened=new List<ClientArchive>();
        try{
            foreach(var pin in pins.Sprites){
                if(!AssetPrepPaths.IsItemToken(pin.Name))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"容器名稱 \"{pin.Name}\" 不合法");
                foreach(string ext in new[]{".idx",".pak"})
                    if(!File.Exists(Path.Combine(root,pin.Name+ext)))throw new AssetPrepException(AssetPrepFailure.MissingContainer,$"TW_CLIENT_ROOT 缺少容器檔 {pin.Name}{ext}（{root}）",pin.Name);
                long size=new FileInfo(Path.Combine(root,pin.Name+".pak")).Length;
                if(size!=pin.PakSize)throw new AssetPrepException(AssetPrepFailure.ContainerMismatch,$"容器 {pin.Name}.pak 大小 {size} 與支援版本 {pin.PakSize} 不符（截斷或不同版本）",pin.Name);
                var archive=new ClientArchive(root,pin);opened.Add(archive);
                Guard(pin.Name,()=>archive.Entries.Count);
            }
            return new(root,pins,opened.ToArray());
        }catch{foreach(var a in opened)a.Dispose();throw;}
    }

    static T Guard<T>(string container,Func<T> read){
        try{return read();}
        catch(UiResourceException ex){
            // ClientArchive words both pin checks (idx / pak SHA-256, pak size) with 「鎖定版本不符」; anything else is structural.
            var failure=ex.Message.Contains("鎖定版本不符",StringComparison.Ordinal)?AssetPrepFailure.ContainerMismatch:AssetPrepFailure.CorruptContainer;
            throw new AssetPrepException(failure,$"TW_CLIENT_ROOT 的容器 {container} 被拒絕：{ex.Message}",container,ex);
        }
        catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptContainer,$"TW_CLIENT_ROOT 的容器 {container} 被拒絕：{ex.Message}",container,ex);}
        catch(IOException ex){throw new AssetPrepException(AssetPrepFailure.Io,$"讀取容器 {container} 失敗：{ex.Message}",container,ex);}
        catch(UnauthorizedAccessException ex){throw new AssetPrepException(AssetPrepFailure.Io,$"讀取容器 {container} 失敗：{ex.Message}",container,ex);}
    }

    public int SpriteCount=>sprites.Length;
    public string SpriteName(int archive)=>sprites[archive].Pin.Name;

    /// <summary>Entry bytes from Sprite container <paramref name="archive"/>, or null when that container has no such entry.</summary>
    public (byte[] Data,AssetSource Source)? TryReadSprite(int archive,string name){
        if(archive<0||archive>=sprites.Length)throw new ArgumentOutOfRangeException(nameof(archive));
        var a=sprites[archive];
        return Guard<(byte[],AssetSource)?>(a.Pin.Name,()=>{
            if(a.Find(name) is not {} entry)return null;
            byte[] data=a.Read(entry);pakVerified[archive]=true;
            return (data,new AssetSource(a.Pin.Name,entry.Name,entry.Offset,entry.Size,AssetPrepPaths.Sha256(data)));
        });
    }

    /// <summary>
    /// A Text entry by name, decoded in memory and checked against its pinned decoded SHA-256 (portable-delivery.md: never
    /// reuse an old File Offset on another Text.pak; look the name up and verify the content).
    /// </summary>
    public (byte[] Decoded,AssetSource Source) ReadText(string name,string decodedSha256){
        foreach(string ext in new[]{".idx",".pak"})
            if(!File.Exists(Path.Combine(ClientRoot,TextContainer+ext)))throw new AssetPrepException(AssetPrepFailure.MissingContainer,$"TW_CLIENT_ROOT 缺少容器檔 {TextContainer}{ext}（{ClientRoot}）",TextContainer);
        return Guard(TextContainer,()=>{
            text??=TextArchive.Open(ClientRoot,TextContainer);
            var entry=text.Single(name)??throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"容器 {TextContainer} 沒有 {name}",TextContainer);
            byte[] decoded=text.Read(entry);string sha=AssetPrepPaths.Sha256(decoded);
            if(!sha.Equals(decodedSha256,StringComparison.OrdinalIgnoreCase))
                throw new AssetPrepException(AssetPrepFailure.ContainerMismatch,$"容器 {TextContainer} 的 {name} 與支援版本不符（解碼後 SHA-256 {sha}）",TextContainer);
            return (decoded,new AssetSource(TextContainer,entry.Name,entry.Offset,(int)entry.Size,sha));
        });
    }

    /// <summary>
    /// Re-reads a Sprite source recorded by an earlier, interrupted run: same container, entry, offset, size and bytes.
    /// False = the record no longer holds (the item is produced again). A container that fails its pin still throws.
    /// </summary>
    public bool Verify(AssetSource source){
        if(source.Container==MapRawSource.TileContainer||source.Container==MapContainer)return !string.IsNullOrEmpty(source.Entry)&&VerifyOther(source);
        int archive=Array.FindIndex(sprites,s=>s.Pin.Name==source.Container);
        if(archive<0||string.IsNullOrEmpty(source.Entry))return false;
        var a=sprites[archive];
        var entry=Guard(a.Pin.Name,()=>a.Find(source.Entry));
        if(entry==null||entry.Offset!=source.Offset||entry.Size!=source.Size)return false;
        return TryReadSprite(archive,source.Entry) is {} hit&&hit.Source.Sha256==source.Sha256;
    }

    /// <summary>Verified identity of every opened container (the receipt's input list). Tile has no pak SHA-256: it is never pinned whole.</summary>
    public IReadOnlyList<AssetContainerRecord> Containers(){
        var list=sprites.Select((a,i)=>new AssetContainerRecord(a.Pin.Name,a.Pin.IndexSha256.ToLowerInvariant(),a.Pin.PakSha256.ToLowerInvariant(),a.Pin.PakSize,pakVerified[i])).ToList();
        if(tile!=null)list.Add(new(tile.Name,tile.IndexSha256,"",tile.PakSize,false));
        return list;
    }

    // ---- T456 (#1041): Tile container and the loose map/ files. ----
    public const string MapContainer="map";

    static PlainArchive OpenTile(string root){
        foreach(string ext in new[]{".idx",".pak"})
            if(!File.Exists(Path.Combine(root,MapRawSource.TileContainer+ext)))throw new AssetPrepException(AssetPrepFailure.MissingContainer,$"TW_CLIENT_ROOT 缺少容器檔 {MapRawSource.TileContainer}{ext}（{root}）",MapRawSource.TileContainer);
        return Guard(MapRawSource.TileContainer,()=>PlainArchive.Open(root,MapRawSource.TileContainer));
    }
    PlainArchive TileArchive=>tile??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"recipe 沒有宣告 Tile 來源");
    string MapRoot=>mapFiles?Path.Combine(ClientRoot,MapContainer):throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"recipe 沒有宣告 map 來源");

    /// <summary>True when Tile.idx lists the entry (case-insensitive).</summary>
    public bool HasTile(string name)=>TileArchive.Find(name)!=null;
    /// <summary>A Tile entry by name (case-insensitive), or null when there is none.</summary>
    public (byte[] Data,AssetSource Source)? TryReadTile(string name){
        var archive=TileArchive;
        return Guard<(byte[],AssetSource)?>(archive.Name,()=>{
            if(archive.Find(name) is not {} entry)return null;
            byte[] data=archive.Read(entry);
            return (data,new AssetSource(archive.Name,entry.Name,entry.Offset,entry.Size,AssetPrepPaths.Sha256(data)));
        });
    }

    /// <summary>Directory names directly below TW_CLIENT_ROOT/map (ordinal order).</summary>
    public IReadOnlyList<string> MapDirectories()=>Guard(MapContainer,()=>Directory.EnumerateDirectories(MapRoot).Select(d=>Path.GetFileName(d)!).Order(StringComparer.Ordinal).ToList());
    /// <summary>File names (original case) in TW_CLIENT_ROOT/map/&lt;directory&gt; (ordinal order). Junctions are refused.</summary>
    public IReadOnlyList<string> MapFiles(string directory){
        if(!AssetPrepPaths.IsItemToken(directory))throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"map 目錄名稱 \"{directory}\" 不合法");
        return Guard(MapContainer,()=>{
            var dir=new DirectoryInfo(Path.Combine(MapRoot,directory));
            if((dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"{dir.FullName} 是 junction／symlink，拒絕讀取");
            return dir.EnumerateFiles().Select(f=>f.Name).Order(StringComparer.Ordinal).ToList();
        });
    }
    /// <summary>Source record of a directory listing: SHA-256 of the names (ordinal, one per line). Lets a resumed run notice added or removed files.</summary>
    public AssetSource MapListing(string directory){
        var names=MapFiles(directory);
        byte[] text=System.Text.Encoding.UTF8.GetBytes(string.Concat(names.Select(n=>n+"\n")));
        return new(MapContainer,$"{MapContainer}/{directory}/",0,names.Count,AssetPrepPaths.Sha256(text));
    }
    /// <summary>TW_CLIENT_ROOT/map/&lt;directory&gt;/&lt;file&gt;, file name as listed by <see cref="MapFiles"/>. The entry is the relative path.</summary>
    public (byte[] Data,AssetSource Source) ReadMapFile(string directory,string file){
        if(!AssetPrepPaths.IsItemToken(directory)||!AssetPrepPaths.IsItemToken(file))throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"map 檔名 \"{directory}/{file}\" 不合法");
        string relative=$"{MapContainer}/{directory}/{file}";
        return Guard(MapContainer,()=>{
            string path=Path.Combine(MapRoot,directory,file);
            if(!File.Exists(path))throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"TW_CLIENT_ROOT 缺少 {relative}",MapContainer);
            byte[] data=File.ReadAllBytes(path);
            return (data,new AssetSource(MapContainer,relative,0,data.Length,AssetPrepPaths.Sha256(data)));
        });
    }

    bool VerifyOther(AssetSource source){
        try{
            if(source.Container==MapRawSource.TileContainer&&tile!=null){
                var entry=tile.Find(source.Entry);
                return entry!=null&&entry.Offset==source.Offset&&entry.Size==source.Size&&AssetPrepPaths.Sha256(tile.Read(entry))==source.Sha256;
            }
            if(source.Container==MapContainer&&mapFiles){
                var parts=source.Entry.Split('/');
                if(parts.Length!=3||parts[0]!=MapContainer||!AssetPrepPaths.IsItemToken(parts[1]))return false;
                if(parts[2].Length==0)return MapListing(parts[1]) is var l&&l.Size==source.Size&&l.Sha256==source.Sha256;
                return AssetPrepPaths.IsItemToken(parts[2])&&ReadMapFile(parts[1],parts[2]).Source==source;
            }
            return false;
        }catch(Exception ex) when(ex is AssetPrepException or IOException or InvalidDataException or UnauthorizedAccessException){return false;}
    }

    public void Dispose(){foreach(var a in sprites)a.Dispose();}
}
