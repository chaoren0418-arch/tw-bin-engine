using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using P06.AssetPrep;
namespace P06;

// T478 (#1074, B15 D8): runtime on-demand preparation for the SPR readers T477 did not cover (docs/design/asset-recipe.md 5
// point 2): player stances outside the preheat list (including S76 polymorph forms), shadows, private shops (action 70),
// doors (actions 28 / 29), magic (objects/magic-<gfx>.json, also MapEffects' packet effects) and projectiles, and the
// pickup frames (action 15) of a stance the prepared pickup data does not have.
// - Same mechanism as T477 (ObjectOnDemand.cs): one background worker, one item at a time, an item is queued once; the
//   caller never decodes; until an item is ready the product keeps its pending presentation (SelfAppearancePending for the
//   own character, not drawn for others / shops / doors / effects, no shadow, no pickup animation).
// - Each item is built by running the preparer's own recipe (PlayersRecipe, ShopsRecipe, DoorsRecipe, ObjectsRecipe magic- /
//   projectile-, ShadowsRecipe, PickupRecipe) in a memory context (AssetRecipeItemContext.ForRuntimeMemory): the same
//   list.spz anchor, container rule, conversion, timing formulas (action-timing.md R1 / R2: stepMs and serverActionMs =
//   40 × ticks; castReleaseMs / projectileReleaseMs = floor sum before the first '!') and refusals. The bytes it "writes" are
//   read once by the product's own readers (ObjectAppearance, PlayerProfiles.Row / Check, PlayerShadowCatalog.ReadFrames,
//   PickupAssets.ReadGroup) and dropped; only the parsed result is kept. Nothing reaches engine/assets or engine/runtime.
// - Decoded .spr files are cached per recipe by SprDecodeKey (container, idx offset, idx size, recipe id/version, list.spz SHA).
// - An item that cannot be built is unavailable with its reason, never retried and never substituted (sprites.md §3.3).
// Known limit (contract L5): stances and polymorph forms outside the baseline get R1 / R2 values computed from list.spz; they
// cannot be compared with the server's spr_action. No client-side table is added to "fix" them.
public sealed class RuntimeAppearances:IDisposable {
    /// <summary>A player stance built on demand: the manifest row the players recipe records, and its appearance.</summary>
    public sealed record PlayerBuild(PlayerProfile Profile,ObjectAppearance Appearance);

    /// <summary>What a build step may use: one memory context per recipe item, sharing that recipe's decode cache.</summary>
    public sealed class Build {
        readonly RuntimeAppearances owner;readonly AssetPrepInput input;
        internal readonly List<AssetSource> Sources=[];
        internal Build(RuntimeAppearances owner,AssetPrepInput input){this.owner=owner;this.input=input;}
        public AssetRecipeItemContext Context(IAssetRecipe recipe,string item)=>
            AssetRecipeItemContext.ForRuntimeMemory(item,input,owner.CacheFor(recipe,input),owner.stop.Token);
        internal void Record(AssetRecipeItemContext context){foreach(var s in context.Sources)if(!Sources.Contains(s))Sources.Add(s);}
    }

    sealed class Slot(string key,Func<Build,object> build){
        public readonly string Key=key;public readonly Func<Build,object> BuildItem=build;
        public int State;                       // OnDemandState; written last (Volatile)
        public object? Value;public string? Reason;public AssetPrepFailure? Failure;
        public IReadOnlyList<AssetSource> Sources=[];
        public int Attempts;
        public readonly TaskCompletionSource Done=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    readonly ConcurrentDictionary<string,Slot> slots=new(StringComparer.Ordinal);
    readonly ConcurrentQueue<Slot> queue=new();
    readonly ConcurrentDictionary<string,SprDecodeCache> caches=new(StringComparer.Ordinal);
    readonly object inputGate=new();
    readonly CancellationTokenSource stop=new();
    readonly string? clientRoot;readonly AssetPrepPins pins;readonly Action<string>? beforeDecode;
    int draining,attempts;
    AssetPrepInput? input;Exception? environmentFailure;bool disposed;

    /// <param name="clientRoot">The original client (absolute). Opened on the first build; a failure is remembered, not retried.</param>
    /// <param name="pins">Anchors of the supported client (the product uses <see cref="AssetPrepPins.Tw1608262200"/>).</param>
    /// <param name="beforeDecode">Test hook: called on the worker thread with the item key before it is built.</param>
    public RuntimeAppearances(string? clientRoot,AssetPrepPins pins,Action<string>? beforeDecode=null){
        this.clientRoot=clientRoot;this.pins=pins;this.beforeDecode=beforeDecode;
    }
    static readonly Lazy<RuntimeAppearances> product=new(()=>new(P06.NativeUi.UiResourcePins.ClientRoot,AssetPrepPins.Tw1608262200));
    /// <summary>The running product's instance (TW_CLIENT_ROOT, the supported client's anchors).</summary>
    public static RuntimeAppearances Product=>product.Value;
    /// <summary>
    /// The instance the static readers use (PlayerProfiles, the renderer's shadows / magic / projectiles). Null — the default,
    /// and what every offline test sees unless it sets one — means no on-demand preparation: the prepared tree only, as before T478.
    /// The game window binds <see cref="Product"/>.
    /// </summary>
    public static RuntimeAppearances? Active {get;set;}

    // ---- Item keys (one slot each). ----
    public static string PlayerKey(int gfx,int weapon)=>Inv($"players/{gfx}-{weapon}");
    /// <summary>#1243: the lenient player build of a (gfx, stance) the players recipe refuses (see <see cref="Lenient"/>).</summary>
    public static string LenientKey(int gfx,int weapon)=>Inv($"players-lenient/{gfx}-{weapon}");
    public static string ShopKey(int gfx)=>Inv($"shops/{gfx}");
    public static string DoorKey(int gfx)=>Inv($"doors/{gfx}");
    public static string MagicKey(int gfx)=>Inv($"objects/magic-{gfx}");
    public static string ProjectileKey(int gfx)=>Inv($"objects/projectile-{gfx}");
    public static string PickupKey(ObjectAppearance body,int weapon)=>Inv($"pickup/{body.Gfx}-{weapon}-{body.Sprite}-{KeySet(body)}");
    public static string ShadowKey(ObjectAppearance body)=>Inv($"shadows/{body.Gfx}-{body.Sprite}-{KeySet(body)}");
    static string Inv(FormattableString s)=>s.ToString(CultureInfo.InvariantCulture);
    // A shadow / pickup group belongs to one body: the set of keys its sequences use (hashed, kept per instance).
    static readonly ConditionalWeakTable<ObjectAppearance,string> keySets=new();
    static string KeySet(ObjectAppearance body)=>keySets.GetValue(body,b=>{
        var keys=b.Sequences.Values.SelectMany(s=>s.Select(t=>t.Key)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",keys))))[..16].ToLowerInvariant();
    });

    // ---- Typed requests: the value when ready, otherwise null (the first request queues the item). ----
    public PlayerBuild? Player(int gfx,int weapon)=>Request(PlayerKey(gfx,weapon),b=>BuildPlayer(b,gfx,weapon)) as PlayerBuild;
    /// <summary>
    /// #1243 (f1211-player-appearance): a (gfx, stance) the players recipe refuses — a polymorph form without all seven player
    /// actions (gfx 1062 has no 18 / 19, 1461 only 0 / 3 / 4), or gfx 138 whose SPR has opaque pixels outside the stored frame
    /// bounds (L7) — built from the list.spz actions the entry does have, like the original client which draws any gfx from its
    /// sprite table (FU14 §1.1 / AN§3.4: a missing action falls back, it does not stop the character). Same stance actions
    /// (weapon + 0..3, 8, 18, 19), the creature model's frames and times (walk ×40 = R1; without a walk, stance 0 uses the breath
    /// length, as ObjectsRecipe does for creatures); no bounds refusal (the renderer draws tiles, not the bounds rectangle).
    /// Asked by <see cref="PlayerProfiles.Resolve"/> only after the player rule is unavailable. Memory only; never written to the
    /// prepared tree, so players/manifest.json and the T470 comparison (L7) are unchanged.
    /// </summary>
    public PlayerBuild? Lenient(int gfx,int weapon)=>Request(LenientKey(gfx,weapon),b=>BuildLenient(b,gfx,weapon)) as PlayerBuild;
    public ObjectAppearance? Shop(int gfx)=>Request(ShopKey(gfx),b=>BuildShop(b,gfx)) as ObjectAppearance;
    public ObjectAppearance? Door(int gfx)=>Request(DoorKey(gfx),b=>BuildDoor(b,gfx)) as ObjectAppearance;
    public ObjectAppearance? Magic(int gfx)=>Request(MagicKey(gfx),b=>BuildMagic(b,gfx)) as ObjectAppearance;
    public IReadOnlyDictionary<string,Frame>? Projectile(int gfx)=>Request(ProjectileKey(gfx),b=>BuildProjectile(b,gfx)) as IReadOnlyDictionary<string,Frame>;
    /// <summary>Shadow frames for a body appearance (101.shadow sprite, the keys the body sequences use).</summary>
    public IReadOnlyDictionary<string,Frame>? Shadow(ObjectAppearance body)=>Request(ShadowKey(body),b=>BuildShadow(b,body)) as IReadOnlyDictionary<string,Frame>;
    /// <summary>The pickup group (action 15) of a stance the prepared pickup data does not have.</summary>
    public PickupAssets.Group? Pickup(ObjectAppearance body,int weapon)=>Request(PickupKey(body,weapon),b=>BuildPickup(b,body)) as PickupAssets.Group;

    object? Request(string key,Func<Build,object> build){
        var slot=slots.GetOrAdd(key,k=>new Slot(k,build));
        if(Volatile.Read(ref slot.State)==(int)OnDemandState.None&&Interlocked.CompareExchange(ref slot.State,(int)OnDemandState.Pending,(int)OnDemandState.None)==(int)OnDemandState.None){
            queue.Enqueue(slot);Pump();
        }
        return Volatile.Read(ref slot.State)==(int)OnDemandState.Ready?slot.Value:null;
    }
    public OnDemandState State(string key)=>slots.TryGetValue(key,out var s)?(OnDemandState)Volatile.Read(ref s.State):OnDemandState.None;
    public bool Pending(string key)=>State(key)==OnDemandState.Pending;
    public string? Reason(string key)=>slots.TryGetValue(key,out var s)&&State(key)==OnDemandState.Unavailable?s.Reason:null;
    public AssetPrepFailure? Failure(string key)=>slots.TryGetValue(key,out var s)&&State(key)==OnDemandState.Unavailable?s.Failure:null;
    public IReadOnlyList<AssetSource> Sources(string key)=>slots.TryGetValue(key,out var s)&&State(key)==OnDemandState.Ready?s.Sources:[];
    public int Attempts=>Volatile.Read(ref attempts);
    public int AttemptsFor(string key)=>slots.TryGetValue(key,out var s)?Volatile.Read(ref s.Attempts):0;
    /// <summary>The decoded-SPR cache of one recipe (null until that recipe has built something).</summary>
    public SprDecodeCache? Cache(string recipeId)=>caches.GetValueOrDefault(recipeId);
    public string? EnvironmentFailure=>Volatile.Read(ref environmentFailure)?.Message;
    /// <summary>Waits until the item is no longer pending (tests and tools; the renderer never waits).</summary>
    public bool Wait(string key,TimeSpan timeout)=>!slots.TryGetValue(key,out var s)||s.Done.Task.Wait(timeout);

    void Pump(){
        if(Interlocked.CompareExchange(ref draining,1,0)!=0)return;
        Task.Factory.StartNew(Drain,CancellationToken.None,TaskCreationOptions.LongRunning|TaskCreationOptions.DenyChildAttach,TaskScheduler.Default);
    }
    void Drain(){
        while(true){
            while(queue.TryDequeue(out var slot))Decode(slot);
            Volatile.Write(ref draining,0);
            if(queue.IsEmpty||Interlocked.CompareExchange(ref draining,1,0)!=0)return;
        }
    }
    void Decode(Slot slot){
        Interlocked.Increment(ref attempts);Interlocked.Increment(ref slot.Attempts);
        try{
            stop.Token.ThrowIfCancellationRequested();
            beforeDecode?.Invoke(slot.Key);
            lock(inputGate){
                stop.Token.ThrowIfCancellationRequested();
                var build=new Build(this,Input());
                slot.Value=slot.BuildItem(build);slot.Sources=build.Sources.ToArray();
                Finish(slot,OnDemandState.Ready);
            }
        }
        catch(AssetPrepException ex){Unavailable(slot,ex.Failure,ex.Message);}
        catch(OperationCanceledException){Unavailable(slot,AssetPrepFailure.InvalidRequest,"按需解碼已停止");}
        catch(Exception ex){Unavailable(slot,AssetPrepFailure.CorruptEntry,$"{slot.Key} 無法建立：{ex.GetType().Name}：{ex.Message}");}
    }
    AssetPrepInput Input(){
        if(environmentFailure!=null)throw environmentFailure;
        try{
            input??=AssetPrepInput.Open(clientRoot,pins,AssetPrepSources.Sprites);
            _=input.ReadSpriteList();
            return input;
        }
        catch(Exception ex){
            input?.Dispose();input=null;
            Volatile.Write(ref environmentFailure,ex is AssetPrepException?ex:new AssetPrepException(AssetPrepFailure.Io,$"原客戶端無法開啟：{ex.Message}",inner:ex));
            P06.NativeUi.UiResourceLog.Write($"runtime-appearances disabled: {ex.Message}");
            throw environmentFailure!;
        }
    }
    SprDecodeCache CacheFor(IAssetRecipe recipe,AssetPrepInput opened)=>caches.GetOrAdd(recipe.Id,_=>new SprDecodeCache(recipe.Id,recipe.Version,opened.ReadSpriteList().List.Sha256));
    void Unavailable(Slot slot,AssetPrepFailure failure,string reason){
        slot.Failure=failure;slot.Reason=reason;slot.Value=null;Finish(slot,OnDemandState.Unavailable);
        // #1243: every refused item is written once to native-ui.log (an item is never retried), so a missing appearance is visible.
        P06.NativeUi.UiResourceLog.Write($"runtime-appearances unavailable {slot.Key} ({failure}): {reason}");
    }
    static void Finish(Slot slot,OnDemandState state){Volatile.Write(ref slot.State,(int)state);slot.Done.TrySetResult();}

    // ---- Builders: the recipe's own Produce in a memory context, then the product's own reader. ----
    static AssetPrepException Refused(string reason)=>new(AssetPrepFailure.Unavailable,reason,AssetPrepInput.TextContainer);
    static (AssetRecipeItemContext Context,byte[] Data) Run(Build b,IAssetRecipe recipe,string item,string path){
        var context=b.Context(recipe,item);
        try{recipe.Produce(item,context);}
        finally{b.Record(context);}
        // The T455 recipes turn an Unavailable refusal into a manifest row; on demand it is the item's reason.
        if(context.Metadata.TryGetValue("unavailable",out string? why))throw Refused(why);
        if(!context.Captured.TryGetValue(path,out var data))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"{recipe.Id} 沒有產生 {path}");
        return (context,data);
    }

    static PlayerBuild BuildPlayer(Build b,int gfx,int weapon){
        // gfx 0 stances 0 / 4 / 20 / 50 are objects' files (PlayersRecipe.InheritedPath); they are always in the preheat list.
        if(PlayersRecipe.InheritedPath(gfx,weapon) is {} inherited)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"gfx {gfx} weapon {weapon} 由 objects 準備（{inherited}），不按需產生");
        var recipe=new PlayersRecipe();string item=Inv($"{gfx}-{weapon}");string path=Inv($"players/{gfx}-{weapon}.json");
        var (context,data)=Run(b,recipe,item,path);
        using var row=JsonDocument.Parse(context.Metadata[SprManifest.Row]);
        var profile=PlayerProfiles.Row(row.RootElement);
        if(profile.Gfx!=gfx||profile.Weapon!=weapon||profile.Path!=path||profile.Sha256!=AssetPrepPaths.Sha256(data))throw new InvalidDataException("on-demand player row differs from its file");
        var appearance=ObjectAppearance.FromRuntimeJson(data);
        PlayerProfiles.Check(appearance,profile);
        return new(profile with{Runtime=appearance},appearance);
    }
    /// <summary>Virtual path of a lenient build (nothing exists there; <see cref="PlayerProfile.Runtime"/> carries the appearance).</summary>
    public static string LenientPath(int gfx,int weapon)=>Inv($"runtime/players-lenient/{gfx}-{weapon}");
    static PlayerBuild BuildLenient(Build b,int gfx,int weapon){
        if(PlayersRecipe.InheritedPath(gfx,weapon) is {} inherited)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"gfx {gfx} weapon {weapon} 由 objects 準備（{inherited}），不按需產生");
        var context=b.Context(new PlayersRecipe(),Inv($"{gfx}-{weapon}"));
        try{
            var entry=context.ReadSpriteList().Find(gfx)??throw Refused($"list.spz has no entry for gfx {gfx}");
            var actions=SprCreature.PlayerActions(weapon).Select(a=>(a.Label,Action:entry.Action(a.Action))).Where(a=>a.Action!=null).Select(a=>(a.Label,Action:a.Action!)).ToArray();
            if(actions.Length==0)throw Refused($"list.spz 的 gfx {gfx} 沒有姿勢 {weapon} 的任何玩家動作（{weapon}–{weapon+3}、8、18、19）");
            SprAction? Of(string label)=>actions.FirstOrDefault(a=>a.Label==label).Action;
            // A stance other than 0 without its own walk is not this stance (PlayerProfiles.Resolve then tries stance 0).
            int stepMs=Of("walk") is {} walk?SprTiming.Times40(walk):weapon==0&&Of("breath") is {} breath?(int)SprTiming.Exact(breath)
                :throw Refused($"list.spz 的 gfx {gfx} 沒有姿勢 {weapon} 的 walk（動作 {weapon}）");
            var body=new SprBody(context,entry.Sprite);
            var sequences=actions.SelectMany(a=>body.Sequences(a.Label,a.Action)).ToArray();
            var appearance=ObjectAppearance.FromCreature(new ObjectsRecipe.CreatureModel(entry,stepMs,actions,body,sequences));
            if(appearance.Gfx!=gfx||appearance.StepMs!=stepMs)throw new InvalidDataException("lenient player appearance identity");
            // actionMs as a players file has it (sprites.md §4.2: walk ×40, the other six the floor sum), not the monster rule.
            foreach(var (label,action) in actions)appearance.ActionMs[label]=label=="walk"?SprTiming.Times40(action):SprTiming.Floor(action);
            return new(new PlayerProfile(gfx,weapon,LenientPath(gfx,weapon),"",stepMs,[],[],null){Runtime=appearance},appearance);
        }
        finally{b.Record(context);}
    }
    static ObjectAppearance Checked(ObjectAppearance a,int gfx,params string[] actions){
        if(a.Gfx!=gfx||actions.Any(action=>Enumerable.Range(0,8).Any(d=>!a.Supports(action,d))))throw new InvalidDataException("Incomplete original appearance");
        return a;
    }
    static ObjectAppearance BuildShop(Build b,int gfx)=>Checked(ObjectAppearance.FromRuntimeJson(Run(b,new ShopsRecipe(),Inv($"{gfx}"),Inv($"shops/{gfx}.json")).Data),gfx,"shop");
    static ObjectAppearance BuildDoor(Build b,int gfx)=>Checked(ObjectAppearance.FromRuntimeJson(Run(b,new DoorsRecipe(),Inv($"{gfx}"),Inv($"doors/{gfx}.json")).Data),gfx,"open","close");
    static ObjectAppearance BuildMagic(Build b,int gfx){
        string name=Inv($"magic-{gfx}");
        return Checked(ObjectAppearance.FromRuntimeJson(Run(b,new ObjectsRecipe(),name,"objects/"+name+".json").Data),gfx);
    }
    static IReadOnlyDictionary<string,Frame> BuildProjectile(Build b,int gfx){
        string name=Inv($"projectile-{gfx}");
        using var doc=JsonDocument.Parse(Run(b,new ObjectsRecipe(),name,"objects/"+name+".json").Data);
        if(doc.RootElement.I("gfx")!=gfx)throw new InvalidDataException("projectile identity");
        return ProjectileFrames(doc.RootElement);
    }
    static SprManifest.Appearance Body(ObjectAppearance body)=>new(body.Gfx,null,body.Sprite,body.StepMs,null,body.Frames.Count,
        body.Sequences.OrderBy(s=>s.Key.Item1,StringComparer.Ordinal).ThenBy(s=>s.Key.Item2).Select(s=>(s.Key.Item1,s.Key.Item2,s.Value.Select(t=>t.Key).ToArray())).ToList());
    static IReadOnlyDictionary<string,Frame> BuildShadow(Build b,ObjectAppearance body){
        var recipe=new ShadowsRecipe();string item=Inv($"{body.Gfx}");
        var context=b.Context(recipe,item);
        try{ShadowsRecipe.Runtime(context,body.Gfx,[("runtime",Body(body))]);}finally{b.Record(context);}
        using var doc=JsonDocument.Parse(context.Captured[Inv($"shadows/{body.Gfx}.json")]);
        return PlayerShadowCatalog.ReadFrames(doc.RootElement,body.Gfx,body.Sprite);
    }
    static PickupAssets.Group BuildPickup(Build b,ObjectAppearance body){
        var recipe=new PickupRecipe();
        var context=b.Context(recipe,PickupRecipe.Item);
        try{PickupRecipe.RuntimeGroup(context,body.Gfx,body.Sprite,body.Frames.Keys.ToHashSet(StringComparer.Ordinal));}finally{b.Record(context);}
        return PickupAssets.ReadGroup(context.Captured[PickupRecipe.RuntimeGroupPath]);
    }

    /// <summary>The frames of an objects/projectile-&lt;gfx&gt;.json document (prepared, or produced in memory on demand); tile type 2.</summary>
    public static Dictionary<string,Frame> ProjectileFrames(JsonElement j){
        var frames=new Dictionary<string,Frame>();
        foreach(var f in j.GetProperty("frames").EnumerateArray())frames.Add(f.S("key"),new(f.S("key"),f.A("bounds"),f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new(2,b.GetProperty("pixels").EnumerateArray().Select(pix=>{var n=pix.EnumerateArray().Select(x=>x.GetInt32()).ToArray();return new Pixel(n[0],n[1],(ushort)n[2]);}).ToArray()))).ToArray()));
        return frames;
    }

    public void Dispose(){        lock(inputGate){
            if(disposed)return;disposed=true;
            stop.Cancel();input?.Dispose();input=null;
        }
    }
}
