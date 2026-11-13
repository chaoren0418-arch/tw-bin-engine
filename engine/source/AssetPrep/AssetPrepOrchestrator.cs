using System.Text.Json;
using P06.NativeUi;
namespace P06.AssetPrep;

/// <summary>Additional set anchors (asset-recipe.md §1.2). Alternate hashes are for synthetic offline fixtures only.</summary>
public sealed record AssetPrepSetPins {
    public string DescSha256{get;init;}=TextArchive.DescSha256;
    public string SpellDescSha256{get;init;}=ClientSpellTable.NamesSha256;
    public IReadOnlyList<ClientArchiveAnchor> Tile{get;init;}=UiResourcePins.Tile.AnchorList;
    public string SegSha256{get;init;}=MapScan.SegAnchorSha256;
}

public sealed record AssetPrepProgress(string Phase,string Recipe,int Completed,int Total,int Produced,int Reused,long EstimatedRequiredBytes,long AvailableBytes);
public sealed record AssetPrepSetRequest(string? ClientRoot,string EngineRoot) {
    public AssetPrepPins Pins{get;init;}=AssetPrepPins.Tw1608262200;
    public AssetPrepSetPins SetPins{get;init;}=new();
    public CancellationToken Cancel{get;init;}
    /// <summary>Synchronous progress, safe for a UI to marshal; exceptions stop the run before publication.</summary>
    public Action<AssetPrepProgress>? Progress{get;init;}
    /// <summary>Estimated final output bytes (contract estimate: about 245 MB + 0.5 GB). Staging and candidate both need space.</summary>
    public long EstimatedOutputBytes{get;init;}=800L*1024*1024;
    public long SpaceReserveBytes{get;init;}=16L*1024*1024;
    /// <summary>Injectable volume free-space probe for offline tests. Default reads DriveInfo.AvailableFreeSpace.</summary>
    public Func<string,long>? AvailableSpace{get;init;}
}
public sealed record AssetPrepSetResult(AssetPrepOutcome Outcome,string ActiveDirectory,AssetSetReceipt Receipt,int Produced,int Reused);

/// <summary>
/// T467: the contract's thirteen recipes, one shared assets tree, one writer and one two-rename commit.
/// Per-recipe staging remains durable between attempts; no recipe publishes into active on its own.
/// A ready receipt means the full preheat request completed, including explicit unavailable rows, not all possible gfx.
/// The launcher entry point is T468; this API deliberately has no startup side effects.
/// </summary>
public sealed class AssetPrepOrchestrator {
    // Contract §6 order. Implementations are discovered by [AssetRecipe]; inventory is a library slice, never in this set.
    public static IReadOnlyList<string> RecipeOrder{get;}=Array.AsReadOnly(new[]{"text","character","objects","doors","shops","terrain","scene","map-effects","players","appearance-lists","maps","shadows","pickup"});
    readonly IReadOnlyList<IAssetRecipe> recipes;
    readonly AssetPrepRunner runner;
    readonly Dictionary<string,IReadOnlyList<string>> requests;

    public AssetPrepOrchestrator(IEnumerable<IAssetRecipe> discovered){
        var all=discovered.ToDictionary(r=>r.Id,StringComparer.Ordinal);
        recipes=RecipeOrder.Select(id=>all.TryGetValue(id,out var r)?r:throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"總流程缺少 recipe {id}")).ToArray();
        var earlier=new HashSet<string>(StringComparer.Ordinal);
        foreach(var recipe in recipes){
            if(recipe.Version<=0||recipe.Requires.Any(id=>!earlier.Contains(id)))throw new AssetPrepException(AssetPrepFailure.Prerequisite,$"recipe {recipe.Id} 的版本或先後順序不符總流程契約");
            earlier.Add(recipe.Id);
        }
        runner=new(recipes);
        requests=recipes.ToDictionary(r=>r.Id,r=>r.Normalize(RequiredItems(r.Id)),StringComparer.Ordinal);
    }
    public static AssetPrepOrchestrator Product(){
        var found=AssetRecipeDiscovery.Product;
        if(found.Errors.Count!=0)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,string.Join("；",found.Errors));
        return new(found.Recipes);
    }
    public static IReadOnlyList<string> RequiredItems(string id)=>id switch{
        "text"=>TextRecipe.AllItems,
        "character"=>[CharacterRecipe.Item],
        "terrain"=>[TerrainRecipe.MapsItem,TerrainRecipe.CacheItem,TerrainRecipe.Terrain0Item],
        "scene"=>[SceneRecipe.SceneItem],
        "map-effects"=>[MapEffectsRecipe.EffectsItem],
        "maps"=>[MapsRecipe.CacheItem,MapsRecipe.MapsItem],
        _ when RecipeOrder.Contains(id)=>PreheatList.Items(id),
        _=>throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"{id} 不屬於總流程")
    };

    static void Move(string from,string to){
        for(int attempt=1;;attempt++){
            try{Directory.Move(from,to);return;}
            catch(IOException) when(attempt<6){Thread.Sleep(attempt*50);}
        }
    }
    static IReadOnlyList<AssetSetAnchor> ReadAnchors(AssetPrepInput input,AssetPrepSetPins pins){
        var list=new List<AssetSetAnchor>();
        void Record(AssetSource s,string expected){
            if(!s.Sha256.Equals(expected,StringComparison.OrdinalIgnoreCase))throw new AssetPrepException(AssetPrepFailure.ContainerMismatch,$"錨點 {s.Container}/{s.Entry} SHA-256 不符：{s.Sha256}",s.Container);
            list.Add(new(s.Container,s.Entry,s.Sha256));
        }
        Record(input.ReadSpriteList().Source,input.Pins.ListSpzSha256);
        Record(input.ReadText("list.spr",input.Pins.ListSprSha256??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"總流程需要 list.spr 錨點")).Source,input.Pins.ListSprSha256!);
        Record(input.ReadText(TextArchive.DescFileName,pins.DescSha256).Source,pins.DescSha256);
        Record(input.ReadText(ClientSpellTable.NamesFileName,pins.SpellDescSha256).Source,pins.SpellDescSha256);
        foreach(var anchor in pins.Tile.OrderBy(a=>a.Entry,StringComparer.Ordinal)){
            var hit=input.TryReadTile(anchor.Entry)??throw new AssetPrepException(AssetPrepFailure.MissingEntry,"Tile 缺少錨點 "+anchor.Entry,"Tile");
            Record(hit.Source,anchor.Sha256);
        }
        Record(input.ReadMapFile("6","7fff8000.seg").Source,pins.SegSha256);
        return list.OrderBy(a=>a.Container,StringComparer.Ordinal).ThenBy(a=>a.Entry,StringComparer.Ordinal).ToArray();
    }

    public AssetPrepSetResult Run(AssetPrepSetRequest request){
        var layout=new AssetPrepLayout(request.EngineRoot);
        string client=AssetPrepInput.RequireClientRoot(request.ClientRoot);
        if(AssetPrepPaths.IsWithin(layout.EngineRoot,client)||AssetPrepPaths.IsWithin(client,layout.EngineRoot))
            throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"原客戶端與引擎目錄不能重疊");
        if(request.EstimatedOutputBytes<0||request.SpaceReserveBytes<0||request.EstimatedOutputBytes>(long.MaxValue-request.SpaceReserveBytes)/2)
            throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"空間估計不可為負數或溢位");
        void Targets(){
            foreach(string p in new[]{layout.ActiveRoot,layout.StagingRoot,layout.SetCandidate,layout.SetPrevious})AssetPrepPaths.RejectReparsePoints(p,"總流程輸出");
            if(File.Exists(layout.ActiveRoot))throw new AssetPrepException(AssetPrepFailure.UnsafePath,"assets 位置是檔案");
        }
        Targets();request.Cancel.ThrowIfCancellationRequested();
        if(!AssetPrepPaths.SameVolume(layout.ActiveRoot,layout.SetCandidate))throw new AssetPrepException(AssetPrepFailure.UnsafePath,"staging 與 assets 不在同一磁碟");
        try{
            Directory.CreateDirectory(layout.StagingRoot);Targets();
            using var writer=AssetPrepLock.Acquire(layout.SetLock,"asset-set");
            Recover(layout,Targets);
            return RunLocked(request,layout,client,Targets);
        }catch(IOException ex){
            var failure=(ex.HResult&0xffff) is 112 or 39?AssetPrepFailure.DiskSpace:AssetPrepFailure.Io;
            throw new AssetPrepException(failure,"資產準備未完成；保留收據供續跑："+ex.Message,inner:ex);
        }catch(UnauthorizedAccessException ex){throw new AssetPrepException(AssetPrepFailure.Io,"資產準備無法寫入；保留收據供續跑："+ex.Message,inner:ex);}
        catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.Verification,"完整資產樹驗證失敗；未發布："+ex.Message,inner:ex);}
    }

    static void Recover(AssetPrepLayout layout,Action targets){
        if(!Directory.Exists(layout.SetPrevious))return;
        // Both trees may exist after the second rename. Never discard the old one before verifying the new commit.
        targets();AssetPrepPaths.ListFiles(layout.SetPrevious);
        if(Directory.Exists(layout.ActiveRoot)){
            try{AssetSetIntegrity.VerifyTree(layout.ActiveRoot);}
            catch(Exception ex) when(ex is IOException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException){
                string rejected=Path.Combine(layout.StagingRoot,"set.rejected");
                AssetPrepPaths.DeleteTree(rejected);AssetPrepPaths.ListFiles(layout.ActiveRoot);
                Move(layout.ActiveRoot,rejected);Move(layout.SetPrevious,layout.ActiveRoot);return;
            }
            AssetPrepPaths.DeleteTree(layout.SetPrevious);
        }else Move(layout.SetPrevious,layout.ActiveRoot);
    }

    AssetPrepSetResult RunLocked(AssetPrepSetRequest request,AssetPrepLayout layout,string client,Action targets){
        int completed=0,produced=0,reused=0,total=requests.Values.Sum(r=>r.Count);
        string phase="validate",category="";
        long estimate=checked(request.EstimatedOutputBytes*2+request.SpaceReserveBytes),available=0;
        void Space(long needed){
            request.Cancel.ThrowIfCancellationRequested();
            available=request.AvailableSpace?.Invoke(layout.EngineRoot)??new DriveInfo(Path.GetPathRoot(layout.EngineRoot)!).AvailableFreeSpace;
            estimate=needed;
            if(available<needed)throw new AssetPrepException(AssetPrepFailure.DiskSpace,$"磁碟空間不足：估計尚需 {needed} bytes，可用 {available} bytes；已完成項目可續跑");
        }
        void Progress()=>request.Progress?.Invoke(new(phase,category,completed,total,produced,reused,estimate,available));
        void Log(string state){
            string path=Path.Combine(layout.EngineRoot,"runtime","prep-log.json"),tmp=path+".tmp";
            AssetPrepPaths.RejectReparsePoints(path,"準備紀錄");AssetPrepPaths.RejectReparsePoints(tmp,"準備紀錄");
            AssetPrepRunner.WriteFile(tmp,JsonSerializer.SerializeToUtf8Bytes(new{state,phase,category,completed,total,produced,reused,estimatedRequiredBytes=estimate,availableBytes=available,clientRoot=client,utc=DateTime.UtcNow},AssetSetReceipt.JsonOptions));
            File.Move(tmp,path,true);
        }
        try{
            using var allInput=AssetPrepInput.Open(client,request.Pins,AssetPrepSources.Sprites|AssetPrepSources.Tile|AssetPrepSources.MapFiles);
            var inputs=allInput.SetContainers();var anchors=ReadAnchors(allInput,request.SetPins);
            // Candidate is disposable; recipe checkpoints are not. A failed copy can be retried without decoding again.
            AssetPrepPaths.DeleteTree(layout.SetCandidate);
            var results=new List<AssetPrepResult>();
            foreach(var recipe in recipes){
                phase="prepare";category=recipe.Id;
                // Progress is reported before expensive validation and after each durable item, including reused ones.
                Space(request.SpaceReserveBytes);Progress();Log("partial");
                var result=runner.Stage(new(client,layout.EngineRoot,recipe.Id,requests[recipe.Id]){
                    Pins=request.Pins,Cancel=request.Cancel,
                    BeforeItem=()=>Space(checked(request.SpaceReserveBytes+(long)((decimal)request.EstimatedOutputBytes*2*(total-completed)/Math.Max(1,total)))),
                    ItemProgress=(_,wasReused)=>{completed++;if(wasReused)reused++;else produced++;Progress();Log("partial");}
                });
                if(!result.Receipt.IsFinal()||!result.Receipt.Completed.Select(c=>c.Item).SequenceEqual(requests[recipe.Id]))
                    throw new AssetPrepException(AssetPrepFailure.Verification,$"{recipe.Id} 未完成完整預熱請求");
                results.Add(result);
            }
            phase="assemble";category="";
            long bytes=results.Sum(r=>r.Receipt.Completed.Sum(c=>c.Outputs.Sum(o=>o.Size)))+results.Sum(r=>(long)r.Receipt.ToJson().Length);
            Space(checked(bytes+request.SpaceReserveBytes));Progress();
            Directory.CreateDirectory(layout.SetCandidate);targets();
            var files=new SortedDictionary<string,AssetIntegrityFile>(StringComparer.Ordinal);
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase){AssetSetReceipt.FileName,AssetSetIntegrity.FileName};
            void Claim(string relative){
                if(AssetPrepPaths.SafeRelative(relative)!=relative||!names.Add(relative))throw new AssetPrepException(AssetPrepFailure.Verification,"跨 recipe 輸出衝突或保留路徑："+relative);
            }
            void Add(string relative,byte[] data){
                Claim(relative);string path=AssetPrepPaths.Combine(layout.SetCandidate,relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);AssetPrepPaths.RejectReparsePoints(path,"組裝輸出");
                AssetPrepRunner.WriteFile(path,data);files.Add(relative,new(relative,data.Length,AssetPrepPaths.Sha256(data)));
            }
            var summaries=new List<AssetSetRecipe>();
            foreach(var result in results){
                request.Cancel.ThrowIfCancellationRequested();var receipt=result.Receipt;
                if(AssetPrepRunner.TreeError(result.ActiveDirectory,receipt) is {} error)throw new AssetPrepException(AssetPrepFailure.Verification,error);
                foreach(var output in receipt.Completed.SelectMany(c=>c.Outputs)){
                    request.Cancel.ThrowIfCancellationRequested();string relative=output.RelativePath;
                    if(relative.StartsWith("receipts/",StringComparison.OrdinalIgnoreCase))throw new AssetPrepException(AssetPrepFailure.Verification,"receipts/ 保留給總流程");
                    Claim(relative);string path=AssetPrepPaths.Combine(layout.SetCandidate,relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);AssetPrepPaths.RejectReparsePoints(path,"組裝輸出");
                    using(var from=File.OpenRead(AssetPrepPaths.Combine(result.ActiveDirectory,relative)))
                    using(var to=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                        byte[] buffer=new byte[64*1024];int read;
                        while((read=from.Read(buffer))>0){request.Cancel.ThrowIfCancellationRequested();to.Write(buffer,0,read);}
                        to.Flush(true);
                    }
                    files.Add(relative,new(relative,output.Size,output.Sha256));
                }
                byte[] json=receipt.ToJson();Add("receipts/"+receipt.RecipeId+".json",json);
                summaries.Add(new(receipt.RecipeId,receipt.RecipeVersion,receipt.Status,receipt.Completed.Count,receipt.Completed.Sum(c=>c.Outputs.Count),receipt.Completed.Sum(c=>c.Outputs.Sum(o=>o.Size)),AssetPrepPaths.Sha256(json)));
            }
            // Verify sources again after all recipes: a long run must not commit a mixture of input revisions.
            phase="verify";Progress();
            using(var check=AssetPrepInput.Open(client,request.Pins,AssetPrepSources.Sprites|AssetPrepSources.Tile|AssetPrepSources.MapFiles)){
                if(!inputs.SequenceEqual(check.SetContainers())||!anchors.SequenceEqual(ReadAnchors(check,request.SetPins)))throw new AssetPrepException(AssetPrepFailure.Verification,"執行中輸入容器或錨點變更，請重試");
                foreach(var result in results){
                    request.Cancel.ThrowIfCancellationRequested();
                    var recipe=recipes.Single(r=>r.Id==result.Receipt.RecipeId);
                    var prior=new AssetPriorOutputs(layout.StagingRoot,recipe.Requires.ToArray());
                    foreach(var source in result.Receipt.Completed.SelectMany(c=>c.Sources).Distinct())
                        if(!(AssetPriorOutputs.IsPrior(source)?prior.Verify(source):check.Verify(source)))throw new AssetPrepException(AssetPrepFailure.Verification,$"來源已變更：{source.Container}/{source.Entry}");
                }
            }
            if(request.Pins.Effect6016Sha256 is {} effectPin){
                if(!files.TryGetValue("maps/effects/6016.json",out var effect)||effect.Sha256!=effectPin)throw new AssetPrepException(AssetPrepFailure.Verification,"maps/effects/6016.json 錨點不符");
                anchors=anchors.Append(new AssetSetAnchor("output","maps/effects/6016.json",effect.Sha256)).ToArray();
            }
            string preheat=AssetPrepPaths.Sha256(JsonSerializer.SerializeToUtf8Bytes(RecipeOrder.Select(id=>new{id,items=requests[id]}),AssetSetReceipt.JsonOptions));
            var final=new AssetSetReceipt(AssetSetReceipt.SchemaId,AssetPrepRunner.ToolVersion,"ready",inputs,anchors,summaries,AssetSetReceipt.DirectProviders(),AssetSetReceipt.Limits(results),preheat);
            byte[] finalBytes=final.ToJson(),integrity=new AssetSetIntegrity(AssetSetIntegrity.SchemaId,AssetPrepPaths.Sha256(finalBytes),files.Values.ToArray()).ToJson();
            byte[] partial=(final with{State="partial"}).ToJson();
            byte[] partialIntegrity=new AssetSetIntegrity(AssetSetIntegrity.SchemaId,AssetPrepPaths.Sha256(partial),files.Values.ToArray()).ToJson();
            AssetPrepRunner.WriteFile(Path.Combine(layout.SetCandidate,AssetSetReceipt.FileName),partial);
            AssetPrepRunner.WriteFile(Path.Combine(layout.SetCandidate,AssetSetIntegrity.FileName),partialIntegrity);
            AssetSetIntegrity.VerifyTree(layout.SetCandidate,partial,partialIntegrity,allowPartial:true);
            request.Cancel.ThrowIfCancellationRequested();
            AssetPrepRunner.WriteFile(Path.Combine(layout.SetCandidate,AssetSetReceipt.FileName),finalBytes);
            AssetPrepRunner.WriteFile(Path.Combine(layout.SetCandidate,AssetSetIntegrity.FileName),integrity);
            AssetSetIntegrity.VerifyTree(layout.SetCandidate,finalBytes,integrity);
            request.Cancel.ThrowIfCancellationRequested();
            bool identical=false;
            if(Directory.Exists(layout.ActiveRoot)){
                // Refuse reparse points anywhere in the old tree before touching it (including nested junctions).
                AssetPrepPaths.ListFiles(layout.ActiveRoot);
                try{AssetSetIntegrity.VerifyTree(layout.ActiveRoot,finalBytes,integrity);identical=true;}
                catch(Exception ex) when(ex is IOException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException){ }
            }
            phase="publish";Progress();request.Cancel.ThrowIfCancellationRequested();targets();
            AssetSetIntegrity.VerifyTree(layout.SetCandidate,finalBytes,integrity);
            if(identical){AssetPrepPaths.DeleteTree(layout.SetCandidate);Log("ready");return new(AssetPrepOutcome.UpToDate,layout.ActiveRoot,final,produced,reused);}
            bool replaced=Directory.Exists(layout.ActiveRoot);
            if(replaced)Move(layout.ActiveRoot,layout.SetPrevious);
            try{Move(layout.SetCandidate,layout.ActiveRoot);}
            catch{if(replaced&&!Directory.Exists(layout.ActiveRoot))Move(layout.SetPrevious,layout.ActiveRoot);throw;}
            // Commit is complete. Cleanup/logging failures must not report that publication failed.
            try{if(replaced)AssetPrepPaths.DeleteTree(layout.SetPrevious);Log("ready");}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or AssetPrepException){ }
            return new(AssetPrepOutcome.Published,layout.ActiveRoot,final,produced,reused);
        }catch{
            try{Log("partial");}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or AssetPrepException){ }
            throw;
        }
    }
}
