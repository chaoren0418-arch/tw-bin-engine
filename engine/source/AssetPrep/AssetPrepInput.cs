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
    public static AssetPrepInput Open(string? clientRoot,AssetPrepPins pins){
        string root=RequireClientRoot(clientRoot);
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
        int archive=Array.FindIndex(sprites,s=>s.Pin.Name==source.Container);
        if(archive<0||string.IsNullOrEmpty(source.Entry))return false;
        var a=sprites[archive];
        var entry=Guard(a.Pin.Name,()=>a.Find(source.Entry));
        if(entry==null||entry.Offset!=source.Offset||entry.Size!=source.Size)return false;
        return TryReadSprite(archive,source.Entry) is {} hit&&hit.Source.Sha256==source.Sha256;
    }

    /// <summary>Verified identity of every Sprite container (the receipt's input list).</summary>
    public IReadOnlyList<AssetContainerRecord> Containers()=>
        sprites.Select((a,i)=>new AssetContainerRecord(a.Pin.Name,a.Pin.IndexSha256.ToLowerInvariant(),a.Pin.PakSha256.ToLowerInvariant(),a.Pin.PakSize,pakVerified[i])).ToList();

    public void Dispose(){foreach(var a in sprites)a.Dispose();}
}
