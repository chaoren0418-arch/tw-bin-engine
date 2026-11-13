using System.Text;
namespace P06.AssetPrep;

// T451 (#1036) / T454 (#1039): read-only input side of the preparer.
// - The client root is the caller's explicit absolute path. There is no default and no fallback to a development
//   machine, release or research directory (unlike UiResourcePins.ClientRoot, which the running product uses).
// - Input validation has three layers (docs/design/asset-recipe.md 1.1). A whole-pak SHA is NOT a refusal criterion:
//   another copy of the supported client may carry unrelated extra entries (the local Sprite03–14 do).
//     1. structure (refused): idx size = count × 28 + 4, every entry inside the pak, no duplicate name in one container;
//     2. anchor entries (refused, naming the entry): decoded SHA-256 written in the program (list.spz, pinned Text entries);
//     3. per entry: container, name, offset, size and payload SHA-256 go into the receipt. That is the provenance record
//        and what a resumed run re-checks; it is not compared with an expected value.
// - Every refusal names the container (AssetPrepException.Container).

/// <summary>Anchor hashes of the supported client version (asset-recipe.md 1.2). Only hashes, no content.</summary>
/// <param name="ListSpzSha256">SHA-256 of Text/list.spz after the DES and the inner AES layer.</param>
public sealed record AssetPrepPins(string ListSpzSha256) {
    public const int SpriteArchiveCount=17;
    /// <summary>The supported TW1608262200 client.</summary>
    public static AssetPrepPins Tw1608262200{get;}=new("e8cec0388d6e9fdf518493fba1674953141c023765e02c210c05a23dfbbe6242"){
        ListSprSha256="523c61ad5cc362a56a36f58b262c84efa8b4435f017e7074d35b5834e8b9f127",
        Effect6016Sha256=P24.MapEffectEvents.Effect6016Definition.AcceptedMetadataSha256,
    };
    /// <summary>T457: decoded SHA-256 of Text/list.spr (asset-recipe.md §1.2 anchor; the map effects need it equal to list.spz per entry).
    /// Null = the recipes that need it refuse to run.</summary>
    public string? ListSprSha256{get;init;}
    /// <summary>T457: whole-file SHA-256 of the produced maps/effects/6016.json (asset-recipe.md §1.2 output anchor = the
    /// ClientEffect6016 pin, never changed). Null only in synthetic tests that have no 6016 at all.</summary>
    public string? Effect6016Sha256{get;init;}
    /// <summary>Index 0 = Sprite (the main container), 1+h = Sprite{h:00}.</summary>
    public static string SpriteName(int archive)=>archive==0?"Sprite":"Sprite"+(archive-1).ToString("00",System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>One container entry that went into an item: where it was and the SHA-256 of its bytes (Text entries: of the decoded bytes).</summary>
public sealed record AssetSource(string Container,string Entry,long Offset,int Size,string Sha256);

/// <summary>Identity of one input container as seen by this run: idx size and SHA-256, pak size. The pak is not hashed whole.</summary>
public sealed record AssetContainerRecord(string Name,long IndexSize,string IndexSha256,long PakSize);

/// <summary>One plain (Sprite-style) idx/pak pair, structure-checked on open. Read-only; the pak stays open with FileShare.Read.</summary>
sealed class PrepArchive:IDisposable {
    public const int MaxEntrySize=8*1024*1024;
    const int RecordSize=28,NameSize=20,MaxRecords=1_000_000;
    public sealed record Entry(int Record,string Name,long Offset,long Size);
    readonly Dictionary<string,Entry> entries;
    readonly string pakPath;
    FileStream? pak;
    public string Name{get;}
    public long IndexSize{get;}
    public string IndexSha256{get;}
    public long PakSize{get;}
    PrepArchive(string name,string pakPath,long indexSize,string indexSha,long pakSize,Dictionary<string,Entry> entries){
        Name=name;this.pakPath=pakPath;IndexSize=indexSize;IndexSha256=indexSha;PakSize=pakSize;this.entries=entries;
    }
    static AssetPrepException Corrupt(string name,string what)=>new(AssetPrepFailure.CorruptContainer,$"TW_CLIENT_ROOT 的容器 {name} 被拒絕：{what}",name);

    public static PrepArchive Open(string root,string name){
        string idxPath=Path.Combine(root,name+".idx"),pakPath=Path.Combine(root,name+".pak");
        foreach(string ext in new[]{".idx",".pak"})
            if(!File.Exists(Path.Combine(root,name+ext)))throw new AssetPrepException(AssetPrepFailure.MissingContainer,$"TW_CLIENT_ROOT 缺少容器檔 {name}{ext}（{root}）",name);
        long pakSize=new FileInfo(pakPath).Length;
        byte[] data;
        using(var file=new FileStream(idxPath,FileMode.Open,FileAccess.Read,FileShare.Read)){
            if(file.Length<4||file.Length>4+(long)RecordSize*MaxRecords)throw Corrupt(name,"idx 大小不合理");
            data=new byte[file.Length];file.ReadExactly(data);
        }
        uint count=BitConverter.ToUInt32(data,0);
        if(count>MaxRecords||data.Length!=4+(long)RecordSize*count)throw Corrupt(name,"idx 筆數×28+4 不等於檔案大小");
        var table=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<count;i++){
            int at=4+i*RecordSize;
            long offset=BitConverter.ToUInt32(data,at);
            var raw=data.AsSpan(at+4,NameSize);int end=raw.IndexOf((byte)0);if(end<0)end=NameSize;
            if(end==0)throw Corrupt(name,$"第 {i} 筆名稱為空");
            foreach(byte c in raw[..end])if(c is <32 or >=127)throw Corrupt(name,$"第 {i} 筆名稱含不可列印字元");
            string entry=Encoding.ASCII.GetString(raw[..end]);
            long size=BitConverter.ToUInt32(data,at+4+NameSize);
            if(offset+size>pakSize)throw Corrupt(name,$"{entry} 越界（offset+size 超過 {name}.pak 的大小 {pakSize}；截斷或不同版本）");
            // Duplicate names inside one container are refused instead of silently picking one.
            if(!table.TryAdd(entry,new(i,entry,offset,size)))throw Corrupt(name,$"{entry} 在同一 idx 重名");
        }
        return new(name,pakPath,data.Length,AssetPrepPaths.Sha256(data),pakSize,table);
    }
    public Entry? Find(string name)=>entries.GetValueOrDefault(name);
    public byte[] Read(Entry entry){
        if(entry.Size is <=0 or >MaxEntrySize)throw Corrupt(Name,$"{entry.Name} 大小 {entry.Size} 超出 1–8 MiB 範圍");
        pak??=new FileStream(pakPath,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(pak.Length!=PakSize||entry.Offset+entry.Size>pak.Length)throw Corrupt(Name,$"{Name}.pak 在執行中被改動（{entry.Name} 越界）");
        pak.Position=entry.Offset;var payload=new byte[entry.Size];
        try{pak.ReadExactly(payload);}catch(EndOfStreamException){throw Corrupt(Name,$"{entry.Name} 截斷");}
        return payload;
    }
    public void Dispose(){pak?.Dispose();pak=null;}
}

public sealed class AssetPrepInput:IDisposable {
    public const string TextContainer="Text",ListSpz="list.spz";
    readonly PrepArchive[] sprites;
    TextArchive? text;
    (SpriteList List,AssetSource Source)? spriteList;
    PrepArchive? tile;
    bool mapFiles;
    public string ClientRoot{get;}
    public AssetPrepPins Pins{get;}

    AssetPrepInput(string root,AssetPrepPins pins,PrepArchive[] sprites){ClientRoot=root;Pins=pins;this.sprites=sprites;}

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
    /// Opens the 17 Sprite containers: both files must exist and the idx must pass the structure check against its pak.
    /// Nothing is compared with a pinned whole-file hash; anchors are checked when a recipe reads them.
    /// </summary>
    public static AssetPrepInput Open(string? clientRoot,AssetPrepPins pins)=>Open(clientRoot,pins,AssetPrepSources.Sprites);

    /// <summary>T456: opens only what the recipe reads. Without <see cref="AssetPrepSources.Sprites"/> no Sprite container is opened or recorded.</summary>
    public static AssetPrepInput Open(string? clientRoot,AssetPrepPins pins,AssetPrepSources sources){
        string root=RequireClientRoot(clientRoot);
        if(pins.ListSpzSha256 is not {Length:64})throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"list.spz 的錨點必須是 SHA-256");
        // T457: map-effects reads the Sprite containers and the map/ block files together (its ids come from the .s32 effect tails).
        if(sources==0||(sources&~(AssetPrepSources.Sprites|AssetPrepSources.Tile|AssetPrepSources.MapFiles))!=0)
            throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe 宣告的來源不支援：{sources}");
        var opened=new List<PrepArchive>();
        AssetPrepInput? input=null;
        try{
            if((sources&AssetPrepSources.Sprites)!=0)
                for(int i=0;i<AssetPrepPins.SpriteArchiveCount;i++){
                    string name=AssetPrepPins.SpriteName(i);
                    opened.Add(Guard(name,()=>PrepArchive.Open(root,name)));
                }
            input=new AssetPrepInput(root,pins,opened.ToArray());
            if((sources&AssetPrepSources.Tile)!=0)input.tile=Guard(MapRawSource.TileContainer,()=>PrepArchive.Open(root,MapRawSource.TileContainer));
            if((sources&AssetPrepSources.MapFiles)!=0){
                if(!Directory.Exists(Path.Combine(root,MapContainer)))throw new AssetPrepException(AssetPrepFailure.MissingContainer,$"TW_CLIENT_ROOT 缺少 {MapContainer}/ 目錄（{root}）",MapContainer);
                AssetPrepPaths.RejectReparsePoints(Path.Combine(root,MapContainer),"原客戶端 map 目錄");
                input.mapFiles=true;
            }
            return input;
        }catch{if(input!=null)input.Dispose();else foreach(var a in opened)a.Dispose();throw;}
    }

    static T Guard<T>(string container,Func<T> read){
        try{return read();}
        catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptContainer,$"TW_CLIENT_ROOT 的容器 {container} 被拒絕：{ex.Message}",container,ex);}
        catch(IOException ex){throw new AssetPrepException(AssetPrepFailure.Io,$"讀取容器 {container} 失敗：{ex.Message}",container,ex);}
        catch(UnauthorizedAccessException ex){throw new AssetPrepException(AssetPrepFailure.Io,$"讀取容器 {container} 失敗：{ex.Message}",container,ex);}
    }

    public int SpriteCount=>sprites.Length;
    public string SpriteName(int archive)=>sprites[archive].Name;

    /// <summary>Entry bytes from Sprite container <paramref name="archive"/>, or null when that container has no such entry.</summary>
    public (byte[] Data,AssetSource Source)? TryReadSprite(int archive,string name){
        if(archive<0||archive>=sprites.Length)throw new ArgumentOutOfRangeException(nameof(archive));
        var a=sprites[archive];
        return Guard<(byte[],AssetSource)?>(a.Name,()=>{
            if(a.Find(name) is not {} entry)return null;
            byte[] data=a.Read(entry);
            return (data,new AssetSource(a.Name,entry.Name,entry.Offset,(int)entry.Size,AssetPrepPaths.Sha256(data)));
        });
    }

    /// <summary>Text.idx / Text.pak of the client, opened once per run. The structure is checked by TextArchive; there is no whole-file pin.</summary>
    TextArchive Text(){
        foreach(string ext in new[]{".idx",".pak"})
            if(!File.Exists(Path.Combine(ClientRoot,TextContainer+ext)))throw new AssetPrepException(AssetPrepFailure.MissingContainer,$"TW_CLIENT_ROOT 缺少容器檔 {TextContainer}{ext}（{ClientRoot}）",TextContainer);
        return Guard(TextContainer,()=>text??=TextArchive.Open(ClientRoot,TextContainer));
    }

    // T453 (#1038): Text entries by index order, for recipes that take many entries by name pattern (the dialog templates).
    // Text.idx legitimately repeats names, so an entry is identified by its index row, never by name alone.
    /// <summary>Every Text index row, in index order.</summary>
    public IReadOnlyList<TextArchive.Entry> TextEntries()=>Text().Entries;

    /// <summary>One Text entry (a row of <see cref="TextEntries"/>), decoded in memory. Not compared with any pin: the SHA-256 of the decoded bytes goes into the source record.</summary>
    public (byte[] Decoded,AssetSource Source) ReadTextEntry(TextArchive.Entry entry){
        var archive=Text();
        if(entry==null||entry.Record<0||entry.Record>=archive.Entries.Count||archive.Entries[entry.Record]!=entry)
            throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"容器 {TextContainer}：{entry?.Name} 不是這個索引的 entry",TextContainer);
        return Guard(TextContainer,()=>{
            byte[] decoded=archive.Read(entry);
            return (decoded,new AssetSource(TextContainer,entry.Name,entry.Offset,(int)entry.Size,AssetPrepPaths.Sha256(decoded)));
        });
    }

    Dictionary<(string Name,uint Offset,uint Size),TextArchive.Entry>? textRows;
    /// <summary>A Text source still holds when the index has a row with the same name, offset and size whose decoded bytes have the recorded SHA-256.</summary>
    bool VerifyText(AssetSource source){
        if(string.IsNullOrEmpty(source.Entry)||source.Offset is <0 or >uint.MaxValue||source.Size<0)return false;
        var archive=Text();
        if(textRows==null){
            var rows=new Dictionary<(string,uint,uint),TextArchive.Entry>();
            foreach(var row in archive.Entries)rows.TryAdd((row.Name,row.Offset,row.Size),row);   // equal rows hold the same bytes
            textRows=rows;
        }
        return textRows.TryGetValue((source.Entry,(uint)source.Offset,(uint)source.Size),out var entry)&&ReadTextEntry(entry).Source.Sha256==source.Sha256;
    }

    /// <summary>
    /// A Text entry by name, decoded in memory and checked against its pinned decoded SHA-256 (portable-delivery.md: never
    /// reuse an old File Offset on another Text.pak; look the name up and verify the content).
    /// </summary>
    public (byte[] Decoded,AssetSource Source) ReadText(string name,string decodedSha256)=>
        Guard(TextContainer,()=>{
            var archive=Text();
            var entry=archive.Single(name)??throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"容器 {TextContainer} 沒有 {name}",TextContainer);
            byte[] decoded=archive.Read(entry);string sha=AssetPrepPaths.Sha256(decoded);
            if(!sha.Equals(decodedSha256,StringComparison.OrdinalIgnoreCase))
                throw new AssetPrepException(AssetPrepFailure.ContainerMismatch,$"容器 {TextContainer} 的 {name} 與支援版本不符（解碼後 SHA-256 {sha}）",TextContainer);
            return (decoded,new AssetSource(TextContainer,entry.Name,entry.Offset,(int)entry.Size,sha));
        });

    /// <summary>
    /// The animation table: Text/list.spz, DES then AES decoded, checked against the anchor and parsed once per run.
    /// list.spz is the only table used (asset-recipe.md 1.1 point 5); there is no fallback to list.spr or TW13081901.txt.
    /// The recorded source SHA-256 is the one of the fully decoded table.
    /// </summary>
    public (SpriteList List,AssetSource Source) ReadSpriteList()=>
        spriteList??=Guard(TextContainer,()=>{
            var archive=Text();
            var entry=archive.Single(ListSpz)??throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"容器 {TextContainer} 沒有 {ListSpz}",TextContainer);
            byte[] decoded=SpriteList.DecodeSpz(archive.Read(entry));string sha=AssetPrepPaths.Sha256(decoded);
            if(!sha.Equals(Pins.ListSpzSha256,StringComparison.OrdinalIgnoreCase))
                throw new AssetPrepException(AssetPrepFailure.ContainerMismatch,$"容器 {TextContainer} 的 {ListSpz} 與支援版本不符（解碼後 SHA-256 {sha}）",TextContainer);
            return (SpriteList.Parse(decoded),new AssetSource(TextContainer,entry.Name,entry.Offset,(int)entry.Size,sha));
        });

    /// <summary>
    /// Re-reads a source recorded by an earlier, interrupted run: same container, entry, offset, size and bytes.
    /// False = the record no longer holds (the item is produced again). A structurally broken container or a failed
    /// anchor still throws.
    /// </summary>
    public bool Verify(AssetSource source){
        if(source.Container==MapRawSource.TileContainer||source.Container==MapContainer)return !string.IsNullOrEmpty(source.Entry)&&VerifyOther(source);
        if(string.IsNullOrEmpty(source.Entry))return false;
        if(source.Container==TextContainer){
            // list.spz records the SHA-256 of the fully decoded table (DES + AES), not of the Text-decoded entry.
            if(source.Entry.Equals(ListSpz,StringComparison.OrdinalIgnoreCase))return ReadSpriteList().Source==source;
            return VerifyText(source);
        }
        int index=Array.FindIndex(sprites,s=>s.Name==source.Container);
        if(index<0)return false;
        var found=sprites[index].Find(source.Entry);
        if(found==null||found.Offset!=source.Offset||found.Size!=source.Size)return false;
        return TryReadSprite(index,source.Entry) is {} hit&&hit.Source.Sha256==source.Sha256;
    }

    /// <summary>Identity of every opened container (the receipt's input list): Sprite containers, or Tile for the map recipes. No pak is hashed whole.</summary>
    public IReadOnlyList<AssetContainerRecord> Containers()=>
        sprites.Append(tile).OfType<PrepArchive>().Select(a=>new AssetContainerRecord(a.Name,a.IndexSize,a.IndexSha256,a.PakSize)).ToList();

    // ---- T456 (#1041): Tile container and the loose map/ files. ----
    public const string MapContainer="map";

    // Tile is opened with the same structure check as the Sprite containers (PrepArchive); there is no whole-pak pin.
    PrepArchive TileArchive=>tile??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"recipe 沒有宣告 Tile 來源");
    string MapRoot=>mapFiles?Path.Combine(ClientRoot,MapContainer):throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"recipe 沒有宣告 map 來源");

    /// <summary>True when Tile.idx lists the entry (case-insensitive).</summary>
    public bool HasTile(string name)=>TileArchive.Find(name)!=null;
    /// <summary>A Tile entry by name (case-insensitive), or null when there is none.</summary>
    public (byte[] Data,AssetSource Source)? TryReadTile(string name){
        var archive=TileArchive;
        return Guard<(byte[],AssetSource)?>(archive.Name,()=>{
            if(archive.Find(name) is not {} entry)return null;
            byte[] data=archive.Read(entry);
            return (data,new AssetSource(archive.Name,entry.Name,entry.Offset,(int)entry.Size,AssetPrepPaths.Sha256(data)));
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

    public void Dispose(){foreach(var a in sprites)a.Dispose();tile?.Dispose();}
}
