using System.Text.Encodings.Web;
using System.Text.Json;
namespace P06.AssetPrep;

// T451 (#1036): staging, receipt, single writer, resume and publish (docs/design/portable-delivery.md「本機生成與啟動」).
//
//   <engine>/runtime/assets-staging/<recipe-id>/            work in progress (+ prep-receipt.json, status "in-progress")
//   <engine>/runtime/assets-staging/<recipe-id>.lock        held with FileShare.None by the one writer
//   <engine>/runtime/assets-staging/<recipe-id>.previous/   the replaced active directory, only during a publish
//   <engine>/assets/<recipe-id>/                            active directory (+ prep-receipt.json, status "partial" / "complete")
//
// A run: check paths (no junction / symlink in the target or any ancestor) → take the lock → finish an interrupted publish →
// verify the input containers → keep the staged items whose outputs and sources still verify → produce the rest, recording
// each finished item in the receipt → verify the whole staging tree against the receipt → rename it into place.
// The active directory is only touched by that last rename, so a failed, cancelled or interrupted run leaves it as it was.
// The receipt has no path and no time in it: the same recipe, tool and input give the same bytes in any directory.
// There is no "ready" state here; whether the engine may start on these assets is decided elsewhere, once every category is closed.

public sealed record AssetReceiptOutput(string RelativePath,long Size,string Sha256);

public sealed record AssetReceiptItem(string Item,IReadOnlyList<AssetSource> Sources,IReadOnlyList<AssetReceiptOutput> Outputs,IReadOnlyDictionary<string,string> Metadata);

public sealed record AssetReceipt(string Schema,string ToolVersion,string RecipeId,int RecipeVersion,string Status,
    IReadOnlyList<AssetContainerRecord> Inputs,IReadOnlyList<string> Requested,IReadOnlyList<AssetReceiptItem> Completed) {
    public const string SchemaId="tw-asset-prep-receipt-v1";
    public const string FileName="prep-receipt.json";
    /// <summary>Staging only: the run has not finished.</summary>
    public const string InProgress="in-progress";
    /// <summary>Published by a recipe that covers only part of its category.</summary>
    public const string Partial="partial";
    /// <summary>Published by a recipe that covers its whole category.</summary>
    public const string Complete="complete";
    static readonly JsonSerializerOptions json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,NewLine="\n",Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};

    public bool IsFinal()=>Status is Partial or Complete;
    public byte[] ToJson()=>JsonSerializer.SerializeToUtf8Bytes(this,json);

    /// <summary>The receipt in a file, or null when it is missing, unreadable, of another schema or incomplete.</summary>
    public static AssetReceipt? TryLoad(string path){
        try{
            if(!File.Exists(path))return null;
            var r=JsonSerializer.Deserialize<AssetReceipt>(File.ReadAllBytes(path),json);
            bool valid=r is {Schema:SchemaId,ToolVersion:not null,RecipeId:not null,Status:not null,Inputs:not null,Requested:not null,Completed:not null}
                &&r.Inputs.All(i=>i is {Name:not null,IndexSha256:not null})
                &&r.Requested.All(x=>x!=null)
                &&r.Completed.All(c=>c is {Item:not null,Sources:not null,Outputs:not null,Metadata:not null}
                    &&c.Sources.All(s=>s is {Container:not null,Entry:not null,Sha256:not null})
                    &&c.Outputs.All(o=>o is {RelativePath:not null,Sha256:not null})
                    &&c.Metadata.All(m=>m.Value!=null));
            return valid?r:null;
        }catch(Exception ex) when(ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException){return null;}
    }
}

/// <summary>
/// T455 (#1040, asset-recipe.md 4.6): the published output of the recipes a recipe declares in <see cref="IAssetRecipe.Requires"/>
/// (shadows read the body profiles, the appearance lists read objects, players point at objects/0*.json …).
/// A prior output is taken from assets/&lt;id&gt;/ only when that recipe's receipt is final and lists the file with the size and
/// SHA-256 found on disk. Every file read is recorded as a source "assets/&lt;id&gt;" (entry = the relative path), so a resumed
/// or repeated run notices when the earlier recipe was run again with another result.
/// </summary>
public sealed class AssetPriorOutputs {
    public const string ContainerPrefix="assets/";
    readonly string activeRoot;
    readonly IReadOnlyCollection<string> allowed;
    readonly Dictionary<string,(AssetReceipt Receipt,Dictionary<string,AssetReceiptOutput> Outputs)> receipts=new(StringComparer.Ordinal);
    internal AssetPriorOutputs(string activeRoot,IReadOnlyCollection<string> allowed){this.activeRoot=activeRoot;this.allowed=allowed;}
    static AssetPrepException Wrong(string message)=>new(AssetPrepFailure.Prerequisite,message);

    (AssetReceipt Receipt,Dictionary<string,AssetReceiptOutput> Outputs) Load(string recipeId){
        if(receipts.TryGetValue(recipeId,out var hit))return hit;
        if(!allowed.Contains(recipeId))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe 沒有宣告依賴 {recipeId}，不能讀它的輸出");
        string directory=Path.Combine(activeRoot,AssetPrepPaths.RequireRecipeId(recipeId));
        if(AssetReceipt.TryLoad(Path.Combine(directory,AssetReceipt.FileName)) is not {} receipt||!receipt.IsFinal()||receipt.RecipeId!=recipeId)
            throw Wrong($"先後順序錯誤：需要先完成 recipe {recipeId}（{directory} 沒有已發布的收據）");
        var outputs=new Dictionary<string,AssetReceiptOutput>(StringComparer.Ordinal);
        foreach(var output in receipt.Completed.SelectMany(c=>c.Outputs))outputs.TryAdd(output.RelativePath,output);
        hit=(receipt,outputs);receipts[recipeId]=hit;return hit;
    }
    /// <summary>Stops the run unless every required recipe has a final receipt (checked before any item is produced).</summary>
    internal void RequireAll(){foreach(string id in allowed)Load(id);}

    /// <summary>The relative paths a required recipe published, ordinal order.</summary>
    public IReadOnlyList<string> Outputs(string recipeId)=>Load(recipeId).Outputs.Keys.Order(StringComparer.Ordinal).ToList();
    // Enumeration is an input too: a new body profile must invalidate a shadow that read the earlier file set.
    internal AssetSource Listing(string recipeId){
        var rows=Load(recipeId).Outputs.Values.OrderBy(o=>o.RelativePath,StringComparer.Ordinal).ToArray();
        return new(ContainerPrefix+recipeId,"@outputs",0,rows.Length,AssetPrepPaths.Sha256(JsonSerializer.SerializeToUtf8Bytes(rows)));
    }

    /// <summary>A published file of a required recipe, checked against that recipe's receipt.</summary>
    public (byte[] Data,AssetSource Source) Read(string recipeId,string relativePath){
        var (_,outputs)=Load(recipeId);
        string relative=AssetPrepPaths.SafeRelative(relativePath);
        if(!outputs.TryGetValue(relative,out var expected))throw Wrong($"先後順序錯誤：recipe {recipeId} 沒有產生 {relative}（先用 {recipeId} 準備它）");
        string full=AssetPrepPaths.Combine(Path.Combine(activeRoot,recipeId),relative);
        var file=new FileInfo(full);
        if(!file.Exists||(file.Attributes&FileAttributes.ReparsePoint)!=0||file.Length!=expected.Size||file.Length>int.MaxValue)
            throw Wrong($"recipe {recipeId} 的 {relative} 不存在或與它的收據不符（要重新準備 {recipeId}）");
        byte[] data=File.ReadAllBytes(full);string sha=AssetPrepPaths.Sha256(data);
        if(sha!=expected.Sha256)throw Wrong($"recipe {recipeId} 的 {relative} 與它的收據不符（要重新準備 {recipeId}）");
        return (data,new AssetSource(ContainerPrefix+recipeId,relative,0,data.Length,sha));
    }

    public static bool IsPrior(AssetSource source)=>source.Container.StartsWith(ContainerPrefix,StringComparison.Ordinal);
    /// <summary>A recorded prior source still holds: the required recipe is still published with the same file.</summary>
    internal bool Verify(AssetSource source){
        string id=source.Container[ContainerPrefix.Length..];
        if(!allowed.Contains(id))return false;
        try{return source.Entry=="@outputs"?Listing(id)==source:Read(id,source.Entry).Source==source;}
        catch(AssetPrepException ex) when(ex.Failure==AssetPrepFailure.Prerequisite||ex.Failure==AssetPrepFailure.UnsafePath){return false;}
    }
}

/// <summary>What a recipe may do while producing one item: read the client's containers (every entry read is recorded as a source) and write below its own staging directory.</summary>
public sealed class AssetRecipeItemContext {
    readonly AssetPrepInput input;
    readonly AssetPriorOutputs prior;
    readonly Func<string,AssetReceiptItem?> completed;
    readonly string? directory;
    readonly HashSet<string> taken;
    readonly List<AssetSource> sources=[];
    readonly List<AssetReceiptOutput> outputs=[];
    readonly SortedDictionary<string,string> metadata=new(StringComparer.Ordinal);
    public string Item{get;}
    public CancellationToken Cancel{get;}
    /// <summary>Every item of this run in canonical order (what Normalize returned). A run publishes only when all of them are done.</summary>
    public IReadOnlyList<string> Requested{get;}
    /// <summary>T477: decoded-SPR cache shared across items; only the runtime on-demand context has one (preparation decodes per item, as before).</summary>
    public SprDecodeCache? DecodeCache{get;private init;}
    /// <summary>T477: true for the read-only context of the runtime on-demand path (every Write is refused).</summary>
    public bool ReadOnly=>directory==null;
    internal AssetRecipeItemContext(string item,AssetPrepInput input,AssetPriorOutputs prior,Func<string,AssetReceiptItem?> completed,string? directory,HashSet<string> taken,IReadOnlyList<string> requested,CancellationToken cancel){
        Item=item;this.input=input;this.prior=prior;this.completed=completed;this.directory=directory;this.taken=taken;Requested=requested;Cancel=cancel;
    }
    /// <summary>
    /// T477 (#1073): a context for the runtime on-demand path. It reads the client's containers exactly like a preparation
    /// item (same anchor, same container rule, every entry recorded in <see cref="Sources"/>) but has no staging directory:
    /// any Write throws, so nothing can reach engine/assets (asset-recipe.md 1.3 point 1: preparation is its only writer).
    /// No prior recipe outputs are readable.
    /// </summary>
    public static AssetRecipeItemContext ForRuntime(string item,AssetPrepInput input,SprDecodeCache? cache,CancellationToken cancel)=>
        new(item,input,new AssetPriorOutputs(input.ClientRoot,Array.Empty<string>()),_=>null,null,[],[item],cancel){DecodeCache=cache};
    /// <summary>
    /// T478 (#1074): the runtime on-demand context that runs a recipe's own Produce and keeps what it writes in memory
    /// (<see cref="Captured"/>) instead of on disk. Same reads as <see cref="ForRuntime"/>; there is still no directory, so
    /// nothing can reach engine/assets or engine/runtime. The captured bytes are parsed once by the product's readers and dropped.
    /// </summary>
    public static AssetRecipeItemContext ForRuntimeMemory(string item,AssetPrepInput input,SprDecodeCache? cache,CancellationToken cancel)=>
        new(item,input,new AssetPriorOutputs(input.ClientRoot,Array.Empty<string>()),_=>null,null,[],[item],cancel){DecodeCache=cache,captured=new(StringComparer.Ordinal)};
    Dictionary<string,byte[]>? captured;
    /// <summary>T478: the files a memory context was given (relative path → bytes); empty for every other context.</summary>
    public IReadOnlyDictionary<string,byte[]> Captured=>captured??(IReadOnlyDictionary<string,byte[]>)new Dictionary<string,byte[]>();
    /// <summary>T478: the facts the recipe recorded for this item (manifest row, unavailable reason, …).</summary>
    public IReadOnlyDictionary<string,string> Metadata=>metadata;
    /// <summary>T477: the entries read so far for this item (container, name, offset, size, SHA-256).</summary>
    public IReadOnlyList<AssetSource> Sources=>sources;
    /// <summary>T477: the recorded source of a Sprite entry this item read from container <paramref name="archive"/>.</summary>
    internal AssetSource SpriteSource(int archive,string name){
        string container=input.SpriteName(archive);
        return sources.LastOrDefault(s=>s.Container==container&&s.Entry.Equals(name,StringComparison.OrdinalIgnoreCase))
            ??throw new InvalidOperationException($"{container} 的 {name} 沒有被這個項目讀過");
    }

    /// <summary>T455: an item of this run that is already finished (produced now or reused), with its outputs and metadata;
    /// null when it is not done yet. Manifest items, ordered last by Normalize, are built from these records.</summary>
    public AssetReceiptItem? Completed(string item)=>completed(item);
    /// <summary>T455: the relative paths a required recipe (<see cref="IAssetRecipe.Requires"/>) published.</summary>
    public IReadOnlyList<string> PriorOutputs(string recipeId){
        var listing=prior.Listing(recipeId);if(!sources.Contains(listing))sources.Add(listing);
        return prior.Outputs(recipeId);
    }
    /// <summary>T455: one published file of a required recipe, verified against its receipt and recorded as a source.</summary>
    public byte[] ReadPrior(string recipeId,string relativePath){
        var hit=prior.Read(recipeId,relativePath);
        if(!sources.Contains(hit.Source))sources.Add(hit.Source);
        return hit.Data;
    }

    /// <summary>The animation table (Text/list.spz), anchor-checked; recorded once as a source of this item.</summary>
    public SpriteList ReadSpriteList(){
        var hit=input.ReadSpriteList();
        if(!sources.Contains(hit.Source))sources.Add(hit.Source);
        return hit.List;
    }
    public int SpriteCount=>input.SpriteCount;
    public string SpriteName(int archive)=>input.SpriteName(archive);
    /// <summary>Entry bytes from a Sprite container (0 = Sprite, 1+h = Sprite{h:00}), or null when it has no such entry.</summary>
    public byte[]? TryReadSprite(int archive,string name){
        if(input.TryReadSprite(archive,name) is not {} hit)return null;
        if(!sources.Contains(hit.Source))sources.Add(hit.Source);
        return hit.Data;
    }
    /// <summary>T457: like <see cref="TryReadSprite"/>, with the source record (container, entry name as listed, offset, size, SHA-256).</summary>
    public (byte[] Data,AssetSource Source)? TryReadSpriteSource(int archive,string name){
        if(input.TryReadSprite(archive,name) is not {} hit)return null;
        if(!sources.Contains(hit.Source))sources.Add(hit.Source);
        return hit;
    }
    /// <summary>T457: the anchors of this run (asset-recipe.md §1.2).</summary>
    public AssetPrepPins Pins=>input.Pins;
    /// <summary>T457: Text/list.spr, decoded and checked against the list.spr anchor. No anchor in the pins → refused.</summary>
    public byte[] ReadListSpr()=>ReadText("list.spr",input.Pins.ListSprSha256??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"沒有 list.spr 的錨點",AssetPrepInput.TextContainer));
    /// <summary>A pinned Text entry, decoded.</summary>
    public byte[] ReadText(string name,string decodedSha256){
        var hit=input.ReadText(name,decodedSha256);
        sources.Add(hit.Source);return hit.Decoded;
    }
    /// <summary>Every Text index row, in index order (names can repeat).</summary>
    public IReadOnlyList<TextArchive.Entry> TextEntries()=>input.TextEntries();
    /// <summary>One Text index row, decoded; recorded as a source with the SHA-256 of the decoded bytes.</summary>
    public byte[] ReadTextEntry(TextArchive.Entry entry){
        var hit=input.ReadTextEntry(entry);
        sources.Add(hit.Source);return hit.Decoded;
    }

    // T456 (#1041): Tile entries and the loose map/ files, recorded as sources like the Sprite entries.
    public bool HasTile(string name)=>input.HasTile(name);
    /// <summary>A Tile entry by name (case-insensitive), or null when Tile.idx has none.</summary>
    public byte[]? TryReadTile(string name){
        if(input.TryReadTile(name) is not {} hit)return null;
        sources.Add(hit.Source);return hit.Data;
    }
    public IReadOnlyList<string> MapDirectories(){
        var listing=input.MapRootListing();if(!sources.Contains(listing))sources.Add(listing);
        return input.MapDirectories();
    }
    /// <summary>File names of TW_CLIENT_ROOT/map/&lt;directory&gt;; the listing itself is recorded as a source (added or removed files invalidate a resume).</summary>
    public IReadOnlyList<string> MapFiles(string directory){
        var listing=input.MapListing(directory);
        if(!sources.Contains(listing))sources.Add(listing);
        return input.MapFiles(directory);
    }
    public byte[] ReadMapFile(string directory,string file){
        var hit=input.ReadMapFile(directory,file);
        sources.Add(hit.Source);return hit.Data;
    }

    /// <summary>T478: a memory context keeps the file (same path rules: safe relative, written once); false for any other context.</summary>
    bool Capture(string relativePath,byte[] data){
        if(captured==null)return false;
        string relative=AssetPrepPaths.SafeRelative(relativePath);
        if(!captured.TryAdd(relative,data))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"輸出路徑 \"{relative}\" 被寫了兩次（項目 {Item}）");
        return true;
    }
    string Claim(string relativePath){
        if(directory==null)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"執行期按需解碼不寫檔：\"{relativePath}\" 被拒絕（engine/assets 只由準備模式寫入）");
        string relative=AssetPrepPaths.SafeRelative(relativePath);
        if(relative.StartsWith(AssetReceipt.FileName,StringComparison.OrdinalIgnoreCase))throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"輸出路徑 \"{relativePath}\" 被拒絕：保留給收據");
        string full=AssetPrepPaths.Combine(directory,relative);
        if(!taken.Add(relative))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"輸出路徑 \"{relative}\" 被寫了兩次（項目 {Item}）");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        AssetPrepPaths.RejectReparsePoints(full,"輸出");
        return relative;
    }
    /// <summary>Writes one output file. The path is relative to the recipe directory (see AssetPrepPaths.SafeRelative); a path can be written once per run.</summary>
    public void Write(string relativePath,ReadOnlySpan<byte> data){
        if(Capture(relativePath,data.ToArray()))return;
        string relative=Claim(relativePath);
        AssetPrepRunner.WriteFile(AssetPrepPaths.Combine(directory!,relative),data);
        outputs.Add(new(relative,data.Length,AssetPrepPaths.Sha256(data)));
    }
    /// <summary>T456: streams a large output straight to disk (no whole-file buffer); size and SHA-256 are taken from the written file.</summary>
    public void Write(string relativePath,Action<Stream> write){
        if(captured!=null){using var memory=new MemoryStream();write(memory);Capture(relativePath,memory.ToArray());return;}
        string relative=Claim(relativePath),full=AssetPrepPaths.Combine(directory!,relative);
        using(var file=new FileStream(full,FileMode.Create,FileAccess.Write,FileShare.None)){write(file);file.Flush(true);}
        outputs.Add(new(relative,new FileInfo(full).Length,AssetPrepPaths.Sha256File(full)));
    }
    /// <summary>Records a fact about the item in the receipt (for example the icon's draw offset).</summary>
    public void Set(string key,string value){
        if(string.IsNullOrEmpty(key)||value==null)throw new ArgumentException("metadata 的 key 與 value 不可空白");
        metadata[key]=value;
    }
    internal AssetReceiptItem Build()=>new(Item,sources.ToList(),outputs.ToList(),new SortedDictionary<string,string>(metadata,StringComparer.Ordinal));
}

/// <summary>The one-writer lock of a recipe: an open handle with FileShare.None. A crashed writer releases it with its process.</summary>
public sealed class AssetPrepLock:IDisposable {
    readonly FileStream stream;
    AssetPrepLock(FileStream stream)=>this.stream=stream;
    public static AssetPrepLock Acquire(string path,string recipeId){
        AssetPrepPaths.RejectReparsePoints(path,"鎖定檔");
        try{return new(new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1,FileOptions.None));}
        catch(IOException ex) when(ex is not (FileNotFoundException or DirectoryNotFoundException or PathTooLongException)){
            throw new AssetPrepException(AssetPrepFailure.Busy,$"recipe {recipeId} 已有另一個準備程序正在寫入，本次不執行（{path}）",inner:ex);
        }
    }
    public void Dispose()=>stream.Dispose();
}

/// <param name="ClientRoot">The user's original client: explicit and absolute. Null, relative or missing is refused; nothing is substituted.</param>
/// <param name="EngineRoot">Absolute engine directory; output goes to its assets/ and runtime/assets-staging/.</param>
/// <param name="Items">What to prepare, in the recipe's own terms (inventory: icon ids).</param>
public sealed record AssetPrepRequest(string? ClientRoot,string EngineRoot,string RecipeId,IReadOnlyList<string> Items) {
    public AssetPrepPins Pins{get;init;}=AssetPrepPins.Tw1608262200;
    public CancellationToken Cancel{get;init;}
    /// <summary>Called after an item is produced and recorded in the staged receipt. An exception from it stops the run like any failure.</summary>
    public Action<string>? ItemCompleted{get;init;}
    /// <summary>Reports verified reused items as well as newly completed ones. Called after the checkpoint is durable.</summary>
    public Action<string,bool>? ItemProgress{get;init;}
    /// <summary>Called before producing an item (the set runner uses it to check cancellation and free space).</summary>
    public Action? BeforeItem{get;init;}
}

public enum AssetPrepOutcome {
    /// <summary>Verified recipe kept in staging for the set publisher; active was not touched.</summary>
    Staged,
    /// <summary>A newly verified directory replaced (or created) the active one.</summary>
    Published,
    /// <summary>The active directory already holds exactly this request and still verifies; nothing was written to it.</summary>
    UpToDate,
}

/// <param name="Produced">Items produced by this run.</param>
/// <param name="Reused">Items taken over from an interrupted run (or, for UpToDate, from the active directory) after verification.</param>
public sealed record AssetPrepResult(AssetPrepOutcome Outcome,string ActiveDirectory,AssetReceipt Receipt,IReadOnlyList<string> Produced,IReadOnlyList<string> Reused);

public sealed class AssetPrepRunner {
    /// <summary>Recorded in every receipt; staged work of another tool version is discarded.</summary>
    public const string ToolVersion="tw-asset-prep/3";   // 3 (T467): set publication; map/prior-output listings and aggregate resume dependencies
    readonly Dictionary<string,IAssetRecipe> recipes=new(StringComparer.Ordinal);

    public AssetPrepRunner(IEnumerable<IAssetRecipe> recipes){
        foreach(var recipe in recipes){
            string id=AssetPrepPaths.RequireRecipeId(recipe.Id);
            if(!this.recipes.TryAdd(id,recipe))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe id \"{id}\" 重複");
        }
    }
    /// <summary>Runner over the automatically discovered product recipes.</summary>
    public static AssetPrepRunner Product()=>new(AssetRecipeDiscovery.Product.Recipes);
    public IReadOnlyCollection<string> RecipeIds=>recipes.Keys.OrderBy(k=>k,StringComparer.Ordinal).ToList();

    internal static void WriteFile(string path,ReadOnlySpan<byte> data){
        using var file=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None);
        file.Write(data);file.Flush(true);
    }
    static void WriteReceipt(string directory,AssetReceipt receipt){
        string final=Path.Combine(directory,AssetReceipt.FileName),temporary=final+".tmp";
        WriteFile(temporary,receipt.ToJson());
        Retry(()=>File.Move(temporary,final,true));
    }
    /// <summary>A rename right after writing can meet a scanner's or indexer's short-lived handle; try a few times before giving up.</summary>
    static void Retry(Action rename){
        for(int attempt=1;;attempt++){
            try{rename();return;}
            catch(Exception ex) when(attempt<6&&ex is (IOException or UnauthorizedAccessException) and not (FileNotFoundException or DirectoryNotFoundException)){Thread.Sleep(50*attempt);}
        }
    }
    static string FinalStatus(IAssetRecipe recipe)=>recipe.Coverage==AssetRecipeCoverage.Complete?AssetReceipt.Complete:AssetReceipt.Partial;

    static bool SameIdentity(AssetReceipt receipt,IAssetRecipe recipe,IReadOnlyList<AssetContainerRecord> inputs)=>
        receipt.ToolVersion==ToolVersion&&receipt.RecipeId==recipe.Id&&receipt.RecipeVersion==recipe.Version&&receipt.Inputs.Count==inputs.Count
        &&receipt.Inputs.Zip(inputs).All(p=>p.First==p.Second);

    static bool OutputIntact(string directory,AssetReceiptOutput output){
        try{
            if(AssetPrepPaths.SafeRelative(output.RelativePath)!=output.RelativePath)return false;
            var file=new FileInfo(AssetPrepPaths.Combine(directory,output.RelativePath));
            return file.Exists&&(file.Attributes&FileAttributes.ReparsePoint)==0&&file.Length==output.Size&&AssetPrepPaths.Sha256File(file.FullName)==output.Sha256;
        }catch(AssetPrepException){return false;}
    }

    /// <summary>Null when the directory holds exactly the receipt's outputs (plus the receipt itself), each with the recorded size and SHA-256.</summary>
    public static string? TreeError(string directory,AssetReceipt receipt){
        var expected=new Dictionary<string,AssetReceiptOutput>(StringComparer.OrdinalIgnoreCase);
        foreach(var output in receipt.Completed.SelectMany(c=>c.Outputs))if(!expected.TryAdd(output.RelativePath,output))return $"收據重複列出 {output.RelativePath}";
        var files=AssetPrepPaths.ListFiles(directory).Where(f=>!f.Equals(AssetReceipt.FileName,StringComparison.OrdinalIgnoreCase)).ToList();
        foreach(string file in files)if(!expected.ContainsKey(file))return $"收據沒有記錄的檔案 {file}";
        foreach(var output in expected.Values)if(!OutputIntact(directory,output))return $"輸出 {output.RelativePath} 不存在或與收據的大小／SHA-256 不符";
        return null;
    }

    static IReadOnlyList<string> NormalizeItems(IAssetRecipe recipe,IReadOnlyList<string>? items){
        if(items is not {Count:>0})throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {recipe.Id}：沒有指定要準備的項目");
        foreach(string item in items)if(!AssetPrepPaths.IsItemToken(item))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {recipe.Id}：項目 \"{item}\" 不合法");
        var normalized=recipe.Normalize(items);
        if(normalized is not {Count:>0}||normalized.Any(i=>!AssetPrepPaths.IsItemToken(i))||normalized.Distinct(StringComparer.Ordinal).Count()!=normalized.Count)
            throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {recipe.Id}：Normalize 的結果不合法");
        return normalized.ToList();
    }

    public AssetPrepResult Run(AssetPrepRequest request)=>Run(request,false);
    // The set writer already holds the global lock. Dependencies are read only from this run's staged recipes.
    internal AssetPrepResult Stage(AssetPrepRequest request)=>Run(request,true);
    AssetPrepResult Run(AssetPrepRequest request,bool stageOnly){
        string id=AssetPrepPaths.RequireRecipeId(request.RecipeId);
        if(!recipes.TryGetValue(id,out var recipe))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"沒有 recipe \"{id}\"");
        var layout=new AssetPrepLayout(request.EngineRoot);
        string clientRoot=AssetPrepInput.RequireClientRoot(request.ClientRoot);
        string staging=layout.Staging(id),active=layout.Active(id),previous=layout.Previous(id);
        if(AssetPrepPaths.IsWithin(layout.EngineRoot,clientRoot)||AssetPrepPaths.IsWithin(clientRoot,layout.StagingRoot)||AssetPrepPaths.IsWithin(clientRoot,layout.ActiveRoot))
            throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"原客戶端目錄（{clientRoot}）與引擎輸出目錄（{layout.EngineRoot}）重疊；原客戶端一律唯讀");
        if(!AssetPrepPaths.SameVolume(staging,active))throw new AssetPrepException(AssetPrepFailure.UnsafePath,"staging 與正式目錄不在同一磁碟，無法以改名發布");
        var requested=NormalizeItems(recipe,request.Items);
        request.Cancel.ThrowIfCancellationRequested();
        void CheckTargets(){
            AssetPrepPaths.RejectReparsePoints(staging,"staging 目錄");
            AssetPrepPaths.RejectReparsePoints(previous,"交換目錄");
            AssetPrepPaths.RejectReparsePoints(active,"正式目錄");
        }
        CheckTargets();
        try{
            Directory.CreateDirectory(layout.StagingRoot);
            CheckTargets();
            using var setWriter=stageOnly?null:AssetPrepLock.Acquire(layout.SetLock,"asset-set");
            if(!stageOnly&&File.Exists(Path.Combine(layout.ActiveRoot,AssetSetReceipt.FileName)))
                throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"完整 assets 樹只能由總流程更新；單一 recipe 程式庫模式請使用獨立目錄");
            using var writer=AssetPrepLock.Acquire(layout.Lock(id),id);
            return RunLocked(request,recipe,layout,clientRoot,requested,CheckTargets,stageOnly);
        }
        catch(IOException ex){throw new AssetPrepException((ex.HResult&0xffff) is 112 or 39?AssetPrepFailure.DiskSpace:AssetPrepFailure.Io,$"recipe {id} 準備失敗（未發布）：{ex.Message}",inner:ex);}
        catch(UnauthorizedAccessException ex){throw new AssetPrepException(AssetPrepFailure.Io,$"recipe {id} 準備失敗（未發布）：{ex.Message}",inner:ex);}
    }

    AssetPrepResult RunLocked(AssetPrepRequest request,IAssetRecipe recipe,AssetPrepLayout layout,string clientRoot,IReadOnlyList<string> requested,Action checkTargets,bool stageOnly){
        string id=recipe.Id,staging=layout.Staging(id),active=layout.Active(id),previous=layout.Previous(id);

        // A publish that stopped between its two renames: put the old active directory back (or drop it when the new one is in place).
        if(!stageOnly&&Directory.Exists(previous)){
            if(Directory.Exists(active))AssetPrepPaths.DeleteTree(previous);
            else{Directory.CreateDirectory(layout.ActiveRoot);checkTargets();Retry(()=>Directory.Move(previous,active));}
        }

        using var input=AssetPrepInput.Open(clientRoot,request.Pins,(recipe as IAssetRecipeSources)?.Sources??AssetPrepSources.Sprites);
        var identity=stageOnly?input.SetContainers():input.Containers();
        // Order (asset-recipe.md 6): every recipe this one reads must already be published, before anything is produced.
        var requires=(recipe.Requires??[]).Select(AssetPrepPaths.RequireRecipeId).Distinct(StringComparer.Ordinal).ToList();
        if(requires.Contains(id))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {id} 不能依賴自己");
        var prior=new AssetPriorOutputs(stageOnly?layout.StagingRoot:layout.ActiveRoot,requires);
        prior.RequireAll();
        bool Verify(AssetSource source)=>AssetPriorOutputs.IsPrior(source)?prior.Verify(source):input.Verify(source);

        if(!stageOnly&&Directory.Exists(active)&&AssetReceipt.TryLoad(Path.Combine(active,AssetReceipt.FileName)) is {} current
            &&current.Status==FinalStatus(recipe)&&SameIdentity(current,recipe,identity)
            &&current.Requested.SequenceEqual(requested,StringComparer.Ordinal)&&current.Completed.Select(c=>c.Item).SequenceEqual(requested,StringComparer.Ordinal)
            &&TreeError(active,current)==null&&current.Completed.All(c=>c.Sources.All(Verify))){
            AssetPrepPaths.DeleteTree(staging);
            return new(AssetPrepOutcome.UpToDate,active,current,[],requested);
        }

        // Resume: keep what an earlier run finished, as long as its outputs and its sources still verify.
        var done=new Dictionary<string,AssetReceiptItem>(StringComparer.Ordinal);
        var taken=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(Directory.Exists(staging)&&AssetReceipt.TryLoad(Path.Combine(staging,AssetReceipt.FileName)) is {} earlier&&SameIdentity(earlier,recipe,identity)){
            var wanted=requested.ToHashSet(StringComparer.Ordinal);
            bool sameRequest=earlier.Requested.SequenceEqual(requested,StringComparer.Ordinal);
            foreach(var item in earlier.Completed){
                if(!wanted.Contains(item.Item)||done.ContainsKey(item.Item))continue;
                if(!sameRequest&&recipe.DependsOnRequest(item.Item))continue;
                if(item.Outputs.Any(o=>taken.Contains(o.RelativePath))||item.Outputs.Select(o=>o.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=item.Outputs.Count)continue;
                if(!item.Outputs.All(o=>OutputIntact(staging,o))||!item.Sources.All(Verify))continue;
                done[item.Item]=new(item.Item,item.Sources.ToList(),item.Outputs.ToList(),new SortedDictionary<string,string>(item.Metadata.ToDictionary(m=>m.Key,m=>m.Value),StringComparer.Ordinal));
                foreach(var output in item.Outputs)taken.Add(output.RelativePath);
            }
            // Manifests consume Completed() metadata, which is not an external source record. If any item is invalid,
            // rebuild these aggregate items too; otherwise a resumed manifest could keep the old item's hash/count.
            if(done.Count!=requested.Count)
                foreach(string item in done.Keys.Where(recipe.DependsOnRequest).ToArray()){
                    foreach(var output in done[item].Outputs)taken.Remove(output.RelativePath);
                    done.Remove(item);
                }
        }
        Directory.CreateDirectory(staging);
        checkTargets();
        foreach(string file in AssetPrepPaths.ListFiles(staging)){
            if(taken.Contains(file))continue;
            var stale=new FileInfo(Path.Combine(staging,file.Replace('/',Path.DirectorySeparatorChar)));
            if((stale.Attributes&FileAttributes.ReadOnly)!=0)stale.Attributes=FileAttributes.Normal;
            stale.Delete();
        }
        AssetPrepPaths.PruneEmptyDirectories(staging);

        AssetReceipt Snapshot(string status)=>new(AssetReceipt.SchemaId,ToolVersion,recipe.Id,recipe.Version,status,identity,requested,
            requested.Where(done.ContainsKey).Select(i=>done[i]).ToList());
        WriteReceipt(staging,Snapshot(AssetReceipt.InProgress));
        var produced=new List<string>();var reused=new List<string>();
        foreach(string item in requested){
            request.Cancel.ThrowIfCancellationRequested();
            if(done.ContainsKey(item)){reused.Add(item);request.ItemProgress?.Invoke(item,true);continue;}
            request.BeforeItem?.Invoke();
            var context=new AssetRecipeItemContext(item,input,prior,i=>done.GetValueOrDefault(i),staging,taken,requested,request.Cancel);
            recipe.Produce(item,context);
            done[item]=context.Build();
            WriteReceipt(staging,Snapshot(AssetReceipt.InProgress));
            produced.Add(item);
            request.ItemCompleted?.Invoke(item);
            request.ItemProgress?.Invoke(item,false);
        }

        // Everything is verified from disk before the active directory is touched.
        var final=Snapshot(FinalStatus(recipe));
        WriteReceipt(staging,final);
        if(AssetReceipt.TryLoad(Path.Combine(staging,AssetReceipt.FileName)) is not {} staged||!staged.ToJson().AsSpan().SequenceEqual(final.ToJson()))
            throw new AssetPrepException(AssetPrepFailure.Verification,$"recipe {id}：staging 的收據讀回後與寫入內容不同，未發布");
        if(TreeError(staging,final) is string error)throw new AssetPrepException(AssetPrepFailure.Verification,$"recipe {id}：{error}，未發布");
        request.Cancel.ThrowIfCancellationRequested();

        if(stageOnly)return new(AssetPrepOutcome.Staged,staging,final,produced,reused);

        Directory.CreateDirectory(layout.ActiveRoot);
        checkTargets();
        if(File.Exists(active))throw new AssetPrepException(AssetPrepFailure.UnsafePath,$"正式目錄的位置是一個檔案：{active}");
        bool replaced=Directory.Exists(active);
        if(replaced)Retry(()=>Directory.Move(active,previous));
        try{Retry(()=>Directory.Move(staging,active));}
        catch{
            if(replaced&&!Directory.Exists(active))Retry(()=>Directory.Move(previous,active));
            throw;
        }
        if(replaced)AssetPrepPaths.DeleteTree(previous);
        return new(AssetPrepOutcome.Published,active,final,produced,reused);
    }
}
