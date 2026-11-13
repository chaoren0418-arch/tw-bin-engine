using System.Collections.Concurrent;
using System.Globalization;
using P06.AssetPrep;
namespace P06;

// T477 (#1073, B15 D8): runtime on-demand appearances for objects, monsters and NPCs (docs/design/asset-recipe.md 5 point 2,
// docs/reference/asset-recipes/action-timing.md「B」).
// - When the server (S87) shows a gfx the prepared tree does not have, ObjectAssets asks here. The gfx is queued for a
//   background worker; until it is done the object is drawn like AppearancePending (not drawn, no 1120 substitute).
// - The worker reads TW_CLIENT_ROOT exactly like the preparer: the same AssetPrepInput (structure check, list.spz anchor),
//   the same container rule (main Sprite first, alternates must be identical) and the same objects recipe conversion
//   (ObjectsRecipe.Creature: creature actions, contract 3.2 times). The model becomes an ObjectAppearance in memory; no JSON.
// - Nothing is written: not engine/assets (preparation is its only writer, 1.3 point 1), not engine/runtime. A disk cache was
//   not added (see docs/reports/issue-1073.md: the first-encounter cost has not been measured on a real machine yet).
// - Decoded .spr files are cached by (container, idx offset, idx size, recipe id/version, list.spz SHA-256); several gfx share
//   one sprite, so the gfx number is never the cache key. The per-gfx table below only remembers the outcome.
// - A gfx that cannot be built (no list.spz entry, no creature action, a missing file, alternates that differ, a broken file,
//   a step past a file's frames) is marked unavailable with the reason and never tried again; nothing is substituted
//   (sprites.md §3.3). A client root that cannot be opened makes every gfx unavailable with that one reason.
// - One worker, one decode at a time; a gfx is queued once however many times it is asked for.
// Not here (T478): player stances, shadows, shops, doors, magic and projectiles. gfx 0 and the clothes composite (234) follow
// other rules and are refused rather than built with the creature rule.

public enum OnDemandState{None,Pending,Ready,Unavailable}

public sealed class ObjectAppearanceOnDemand:IDisposable {
    sealed class Slot(int gfx){
        public readonly int Gfx=gfx;
        public int State;                       // OnDemandState; written last (Volatile) so the fields below are visible
        public ObjectAppearance? Appearance;
        public string? Reason;
        public AssetPrepFailure? Failure;
        public IReadOnlyList<AssetSource> Sources=[];
        public int Attempts;
        public readonly TaskCompletionSource Done=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    readonly ConcurrentDictionary<int,Slot> slots=new();
    readonly ConcurrentQueue<Slot> queue=new();
    readonly object inputGate=new();
    readonly CancellationTokenSource stop=new();
    readonly string? clientRoot;
    readonly AssetPrepPins pins;
    readonly Action<int>? beforeDecode;
    int draining,attempts;
    AssetPrepInput? input;
    SprDecodeCache? cache;
    Exception? environmentFailure;
    bool disposed;

    /// <summary>Recipe version of the conversion used here (objects); part of the decode cache key.</summary>
    public static readonly int RecipeVersion=new ObjectsRecipe().Version;

    /// <param name="clientRoot">The original client (absolute). Opened on the first decode; failures are remembered, not retried.</param>
    /// <param name="pins">Anchors of the supported client (the product uses <see cref="AssetPrepPins.Tw1608262200"/>).</param>
    /// <param name="beforeDecode">Test hook: called on the worker thread before a gfx is decoded.</param>
    public ObjectAppearanceOnDemand(string? clientRoot,AssetPrepPins pins,Action<int>? beforeDecode=null){
        this.clientRoot=clientRoot;this.pins=pins;this.beforeDecode=beforeDecode;
    }
    static readonly Lazy<ObjectAppearanceOnDemand> product=new(()=>new(P06.NativeUi.UiResourcePins.ClientRoot,AssetPrepPins.Tw1608262200));
    /// <summary>The running product's instance: TW_CLIENT_ROOT as the other runtime readers see it, the supported client's anchors.</summary>
    public static ObjectAppearanceOnDemand Product=>product.Value;

    /// <summary>
    /// The appearance when it is ready; otherwise null. The first request queues the gfx for the background worker
    /// (never decodes on the caller's thread); later requests only look the outcome up.
    /// </summary>
    public ObjectAppearance? Request(int gfx){
        var slot=slots.GetOrAdd(gfx,g=>new Slot(g));
        if(Volatile.Read(ref slot.State)==(int)OnDemandState.None&&Interlocked.CompareExchange(ref slot.State,(int)OnDemandState.Pending,(int)OnDemandState.None)==(int)OnDemandState.None){
            queue.Enqueue(slot);Pump();
        }
        return Volatile.Read(ref slot.State)==(int)OnDemandState.Ready?slot.Appearance:null;
    }
    public OnDemandState State(int gfx)=>slots.TryGetValue(gfx,out var s)?(OnDemandState)Volatile.Read(ref s.State):OnDemandState.None;
    /// <summary>Why the gfx is unavailable (null otherwise).</summary>
    public string? Reason(int gfx)=>slots.TryGetValue(gfx,out var s)&&State(gfx)==OnDemandState.Unavailable?s.Reason:null;
    public AssetPrepFailure? Failure(int gfx)=>slots.TryGetValue(gfx,out var s)&&State(gfx)==OnDemandState.Unavailable?s.Failure:null;
    /// <summary>The client entries the gfx was built from (list.spz and each .spr read), once it is ready.</summary>
    public IReadOnlyList<AssetSource> Sources(int gfx)=>slots.TryGetValue(gfx,out var s)&&State(gfx)==OnDemandState.Ready?s.Sources:[];
    /// <summary>Decode attempts so far (all gfx / one gfx). A gfx is attempted at most once.</summary>
    public int Attempts=>Volatile.Read(ref attempts);
    public int AttemptsFor(int gfx)=>slots.TryGetValue(gfx,out var s)?Volatile.Read(ref s.Attempts):0;
    /// <summary>The decoded-SPR cache (null until the client root has been opened).</summary>
    public SprDecodeCache? Cache=>Volatile.Read(ref cache);
    /// <summary>Why the client root could not be used (every gfx is then unavailable); null otherwise.</summary>
    public string? EnvironmentFailure=>Volatile.Read(ref environmentFailure)?.Message;
    /// <summary>Waits until the gfx is no longer pending (tests and tools; the renderer never waits).</summary>
    public bool Wait(int gfx,TimeSpan timeout)=>!slots.TryGetValue(gfx,out var s)||s.Done.Task.Wait(timeout);

    void Pump(){
        if(Interlocked.CompareExchange(ref draining,1,0)!=0)return;
        Task.Factory.StartNew(Drain,CancellationToken.None,TaskCreationOptions.LongRunning|TaskCreationOptions.DenyChildAttach,TaskScheduler.Default);
    }
    void Drain(){
        while(true){
            while(queue.TryDequeue(out var slot))Decode(slot);
            Volatile.Write(ref draining,0);
            // A request that enqueued after the last TryDequeue but saw draining = 1 relies on this re-check.
            if(queue.IsEmpty||Interlocked.CompareExchange(ref draining,1,0)!=0)return;
        }
    }

    void Decode(Slot slot){
        Interlocked.Increment(ref attempts);Interlocked.Increment(ref slot.Attempts);
        int gfx=slot.Gfx;
        try{
            stop.Token.ThrowIfCancellationRequested();
            beforeDecode?.Invoke(gfx);
            if(gfx is <0 or >ushort.MaxValue){Unavailable(slot,AssetPrepFailure.InvalidRequest,$"gfx {gfx} 超出範圍");return;}
            if(!ObjectsRecipe.CreatureRule(gfx)){Unavailable(slot,AssetPrepFailure.InvalidRequest,$"gfx {gfx} 的外觀不是生物規則（玩家本體或 clothes 疊圖），執行期不按需產生");return;}
            lock(inputGate){
                stop.Token.ThrowIfCancellationRequested();
                var opened=Input();
                var context=AssetRecipeItemContext.ForRuntime(gfx.ToString(CultureInfo.InvariantCulture),opened,cache,stop.Token);
                var entry=context.ReadSpriteList().Require(gfx);
                var appearance=ObjectAppearance.FromCreature(ObjectsRecipe.Creature(context,entry));
                slot.Appearance=appearance;slot.Sources=context.Sources.ToArray();
                Finish(slot,OnDemandState.Ready);
            }
        }
        catch(AssetPrepException ex){Unavailable(slot,ex.Failure,ex.Message);}
        catch(OperationCanceledException){Unavailable(slot,AssetPrepFailure.InvalidRequest,"按需解碼已停止");}
        // Anything else (an appearance that fails the reader's own check, an I/O error): unavailable, remembered, logged once.
        catch(Exception ex){Unavailable(slot,AssetPrepFailure.CorruptEntry,$"gfx {gfx} 無法建立外觀：{ex.GetType().Name}：{ex.Message}");}
    }

    /// <summary>Opens TW_CLIENT_ROOT once (17 Sprite containers + list.spz with its anchor). A failure is kept: no retry.</summary>
    AssetPrepInput Input(){
        if(environmentFailure!=null)throw environmentFailure;
        try{
            input??=AssetPrepInput.Open(clientRoot,pins,AssetPrepSources.Sprites);
            var list=input.ReadSpriteList().List;
            if(cache==null)Volatile.Write(ref cache,new SprDecodeCache(ObjectsRecipe.RecipeId,RecipeVersion,list.Sha256));
            return input;
        }
        catch(Exception ex){
            input?.Dispose();input=null;
            Volatile.Write(ref environmentFailure,ex is AssetPrepException?ex:new AssetPrepException(AssetPrepFailure.Io,$"原客戶端無法開啟：{ex.Message}",inner:ex));
            P06.NativeUi.UiResourceLog.Write($"on-demand-appearance disabled: {ex.Message}");
            throw environmentFailure!;
        }
    }

    void Unavailable(Slot slot,AssetPrepFailure failure,string reason){
        slot.Failure=failure;slot.Reason=reason;slot.Appearance=null;
        Finish(slot,OnDemandState.Unavailable);
    }
    static void Finish(Slot slot,OnDemandState state){
        Volatile.Write(ref slot.State,(int)state);
        slot.Done.TrySetResult();
    }

    public void Dispose(){
        lock(inputGate){
            if(disposed)return;disposed=true;
            stop.Cancel();input?.Dispose();input=null;
        }
    }
}
