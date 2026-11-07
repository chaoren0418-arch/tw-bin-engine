using System.Security.Cryptography;
namespace P06;
public sealed record MapLoadRequest(int Epoch,ushort Map,uint Actor,ushort X,ushort Y,int Revision);
public sealed class WorldEntryTiming(long startedAt,bool transition=false) {
    public const int CoreTimeoutMs=12000,ProbeTimeoutMs=8000,LocalTimeoutMs=30000,TotalTimeoutMs=60000;
    public long StartedAt {get;}=startedAt;
    public bool Transition {get;}=transition;
    public long? ProbeAt {get;private set;}
    public void ProbeStarted(long now){ProbeAt??=now;}
    public void Check(long now,bool core,bool ready,bool loading){
        if(ready&&!loading)return;
        if(now-StartedAt>=TotalTimeoutMs)throw new TimeoutException("world initialization total timeout");
        if(!Transition&&!core&&now-StartedAt>=CoreTimeoutMs)throw new TimeoutException("world core packet timeout");
        if(!Transition&&core&&!ready&&ProbeAt is long at&&now-at>=ProbeTimeoutMs)throw new TimeoutException("world time probe response timeout");
    }
}
public sealed partial class WorldSession {
    internal bool AsyncMapLoading;
    int mapLoadRevision;
    public MapLoadRequest? PendingMapLoad {get;private set;}
    void InvalidateMapLoad(){mapLoadRevision++;PendingMapLoad=null;}
    void RequestMapLoad(OwnActor actor,ushort map){
        if(PendingMapLoad is {} prior&&prior.Epoch==WorldEpoch&&prior.Map==map&&prior.Actor==actor.ObjectId&&prior.X==actor.X&&prior.Y==actor.Y)return;
        PendingMapLoad=new(WorldEpoch,map,actor.ObjectId,actor.X,actor.Y,++mapLoadRevision);MapResourceState="loading";MapResourceNotice=$"載入地圖 {map}…可取消連線。";
    }
    internal bool CurrentMapLoad(MapLoadRequest request)=>PendingMapLoad==request&&WorldEpoch==request.Epoch&&MapId==request.Map&&Actor?.ObjectId==request.Actor&&Actor.X==request.X&&Actor.Y==request.Y;
    internal bool CompleteMapLoad(MapLoadRequest request,OriginalMapCache cache){
        if(!CurrentMapLoad(request))return false;
        if(cache.MapId!=request.Map||!cache.Contains(request.X,request.Y))throw new InvalidDataException("prepared map identity mismatch");
        PendingMapLoad=null;MapCache=cache;sceneOpacity=new(cache.Catalog);long now=SceneClock();cache.ActivateEffects(now);Sync6016(now);sceneOpacity.Move(request.X,request.Y,now);MapResourceState="ready";MapResourceNotice=$"地圖 {request.Map} 已載入原始區塊。";RefreshArrivalNotice();ObserveOpacity("move");return true;
    }
    internal void FailMapLoad(MapLoadRequest request,Exception error){if(CurrentMapLoad(request)){PendingMapLoad=null;MapFailed(error);}}
}
public sealed partial class LoginConnection {
    internal Func<long> WorldClock=()=>Environment.TickCount64;
    internal Func<MapLoadRequest,CancellationToken,Task<OriginalMapCache>>? MapLoader=null;
    WorldEntryTiming? entryTiming;bool worldInitialized;
    CancellationTokenSource? localMapCancellation;
    MapLoadRequest? activeMapLoad;Task? localMapTask;long lastWorldProbe;bool worldProbeSending;
    public object? LastWorldDecodeFailure {get;private set;}
    public object? LastWorldEntryFailure {get;private set;}
    void ResetWorldEntry(){localMapCancellation?.Cancel();localMapCancellation=null;activeMapLoad=null;entryTiming=null;worldInitialized=false;lastWorldProbe=0;worldProbeSending=false;}
    object EntrySnapshot()=>new{startedAt=entryTiming?.StartedAt,transition=entryTiming?.Transition,probeAt=entryTiming?.ProbeAt,pendingMap=World.PendingMapLoad,coreTimeoutMs=WorldEntryTiming.CoreTimeoutMs,probeTimeoutMs=WorldEntryTiming.ProbeTimeoutMs,localTimeoutMs=WorldEntryTiming.LocalTimeoutMs,totalTimeoutMs=WorldEntryTiming.TotalTimeoutMs};
    void ApplyWorldPacket(byte[] plain,string selected,long sequence){
        int itemWorld=World.WorldEpoch;uint? itemActor=World.Actor?.ObjectId;
        try{World.ReceiveOwned(plain,selected,generation);if(itemWorld!=World.WorldEpoch||itemActor!=World.Actor?.ObjectId){ResetItemSend();ResetGroundSend();}}
        catch(Exception ex){LastWorldDecodeFailure=new{sequence,opcode=plain.Length==0?(int?)null:plain[0],length=plain.Length,sha256=Convert.ToHexString(SHA256.HashData(plain)),plainHex=Convert.ToHexString(plain),error=ex.Message,type=ex.GetType().FullName,stack=ex.StackTrace};throw;}
    }
    void TrackWorldEntry(){
        if(State is not (LoginState.EnteringWorld or LoginState.InWorld))return;
        if(World.Ready&&World.PendingMapLoad==null&&!World.TeleportPending){worldInitialized=true;entryTiming=null;}
        else entryTiming??=new(WorldClock(),worldInitialized);
    }
    void PublishWorldProgress(){TrackWorldEntry();Set(World.Ready&&World.PendingMapLoad==null||World.TeleportPending&&State==LoginState.InWorld?LoginState.InWorld:LoginState.EnteringWorld,World.PendingMapLoad!=null?World.MapResourceNotice:World.TeleportPending?World.TeleportNotice:World.Ready?World.MapResourceNotice:"接收世界初始化封包／等待時間探測回覆");}
    void StartPendingMapLoad(int epoch,CancellationToken token){
        var request=World.PendingMapLoad;
        if(request==activeMapLoad)return;
        localMapCancellation?.Cancel();activeMapLoad=request;
        if(request==null)return;
        var cts=CancellationTokenSource.CreateLinkedTokenSource(token);localMapCancellation=cts;
        localMapTask=LoadWorldMap(epoch,request,cts);
    }
    async Task LoadWorldMap(int epoch,MapLoadRequest request,CancellationTokenSource cts){
        try{
            var factory=MapLoader;var assetsRoot=World.MapAssetsRoot;
            var task=factory!=null?factory(request,cts.Token):Task.Run(()=>new OriginalMapCache(assetsRoot,request.X,request.Y,request.Map,cts.Token),cts.Token);
            var cache=await task.WaitAsync(TimeSpan.FromMilliseconds(WorldEntryTiming.LocalTimeoutMs),cts.Token);
            if(epoch!=generation||cts.IsCancellationRequested)return;
            lock(worldCaptureGate){if(World.CompleteMapLoad(request,cache))PublishWorldProgress();}
        }catch(OperationCanceledException) when(cts.IsCancellationRequested){}
        catch(TimeoutException){if(epoch==generation&&World.CurrentMapLoad(request)){LastWorldEntryFailure=new{phase="local-map",timing=EntrySnapshot(),error="local map loading timeout"};Cancel();Set(LoginState.Faulted,"地圖本機載入逾時；已取消，可重新登入。");}}
        catch(Exception ex){if(epoch==generation&&!cts.IsCancellationRequested){lock(worldCaptureGate){World.FailMapLoad(request,ex);PublishWorldProgress();}}}
        finally{cts.Cancel();if(ReferenceEquals(localMapCancellation,cts))localMapCancellation=null;cts.Dispose();}
    }
    async Task PumpWorldAsync(int epoch,CancellationToken token){
        token.ThrowIfCancellationRequested();if(epoch!=generation)return;
        World.CheckTeleportTimeout();TrackWorldEntry();StartPendingMapLoad(epoch,token);
        if(State is not (LoginState.EnteringWorld or LoginState.InWorld))return;
        long now=WorldClock();
        // Probe gets its own response deadline, and is never withheld by local cold loading.
        if(World.CoreComplete&&!worldProbeSending&&sendWorld is {} sender&&(!World.ProbeSent||now-lastWorldProbe>=(World.Ready?(ShipRules.Supported(World.MapId)?ShipRules.ProbeMs:15000):2000))){
            worldProbeSending=true;World.ProbeSent=true;entryTiming?.ProbeStarted(now);
            try{await sender(LoginPackets.Pad([95])).WaitAsync(TimeSpan.FromMilliseconds(WorldEntryTiming.ProbeTimeoutMs),token);if(epoch!=generation)return;World.KeepalivesSent++;lastWorldProbe=WorldClock();}
            finally{if(epoch==generation)worldProbeSending=false;}
        }
        if(epoch!=generation)return;
        await AdvanceShip();if(epoch!=generation)return;
        TrackWorldEntry();entryTiming?.Check(WorldClock(),World.CoreComplete,World.Ready&&!World.TeleportPending,World.PendingMapLoad!=null);
    }
}
