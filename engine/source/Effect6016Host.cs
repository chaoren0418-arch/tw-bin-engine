using System.Collections.Concurrent;
using P24.MapEffectEvents;
namespace P06;

public sealed record EffectAudioReply(bool Success,string Status,object? Detail=null);
// Calls enqueue synchronously, including the transport's immediate retire/revision fence.
public interface IEffectAudio {
    Task<EffectAudioReply> RegisterAsync(EffectOwner owner);
    Task<EffectAudioReply> RetireAsync(EffectOwner owner);
    Task<EffectAudioReply> PlayAsync(int id,EffectOwner owner);
    Task<EffectAudioReply> ConfigureAsync(double gain,bool muted,bool enabled);
    Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan budget);
    Task WhenReleased {get;}
    EffectAudioReply LastMaintenance {get;}
}
public sealed class EffectHostLane {
    readonly int thread=Environment.CurrentManagedThreadId;
    readonly ConcurrentQueue<Action> queue=new();
    public void Verify(){if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("6016 host requires its UI lane");}
    public void Post(Action action)=>queue.Enqueue(action);
    public void Drain(){Verify();while(queue.TryDequeue(out var action))action();}
}
public sealed record EffectAudioObservation(string Operation,EffectAudioReply Reply);
public sealed class Effect6016Host {
    sealed class Registration {public bool? Ready;public EventRequest? Pending;}
    readonly Effect6016Controller controller;readonly IEffectAudio? audio;readonly Func<long> clock;
    readonly Func<EffectCandidate,bool> audible;readonly Dictionary<EffectOwner,Registration> owners=[];
    readonly ConcurrentQueue<EffectAudioObservation> observations=new();
    readonly Action<EffectAudioObservation>? observer;
    long observed;int revision;bool configured,allowed,muted,closed;double gain=.5;
    Task<EffectAudioReply>? shutdown;
    int? committedMap;IReadOnlyList<BlockSnapshot> committedBlocks=[];
    public EffectHostLane Lane {get;}
    public bool Closed=>closed;
    public bool ControlsApplied=>configured;
    public string ControlsStatus {get;private set;}="unavailable";
    public int ControlsStateRevision {get;private set;}
    public double Gain=>gain;public bool Muted=>muted;
    public long ObservedReplies=>Interlocked.Read(ref observed);
    public EffectAudioObservation[] Observations=>observations.ToArray();
    public EffectAudioReply? LastMaintenance=>audio?.LastMaintenance;
    public Task WhenReleased=>audio?.WhenReleased??Task.CompletedTask;
    public Effect6016Host(Effect6016Controller controller,IEffectAudio? audio,EffectHostLane lane,Func<long> clock,Func<EffectCandidate,bool>? audible=null,Action<EffectAudioObservation>? observer=null){
        this.controller=controller;this.audio=audio;Lane=lane;this.clock=clock;this.audible=audible??(_=>true);this.observer=observer;lane.Verify();
    }
    void Record(string operation,EffectAudioReply reply){
        var item=new EffectAudioObservation(operation,reply);observations.Enqueue(item);Interlocked.Increment(ref observed);
        while(observations.Count>256)observations.TryDequeue(out _);
        // Diagnostics sink must be thread-safe; a diagnostic failure cannot break gameplay.
        try{observer?.Invoke(item);}catch{}
    }
    Task<EffectAudioReply> Invoke(string operation,Func<Task<EffectAudioReply>> call,Action<EffectAudioReply>? onLane=null){
        Task<EffectAudioReply> task;try{task=call();}catch(Exception ex){task=Task.FromResult(new EffectAudioReply(false,"HostCallFailed",ex.ToString()));}
        return Observe();
        async Task<EffectAudioReply> Observe(){
            EffectAudioReply reply;try{reply=await task.ConfigureAwait(false);}catch(Exception ex){reply=new(false,"TaskFailed",ex.ToString());}
            if(onLane!=null)Lane.Post(()=>onLane(reply));Record(operation,reply);return reply;
        }
    }
    EventGate Gate(EffectCandidate candidate)=>new(!closed&&allowed&&!muted,audio!=null&&configured,audible(candidate));
    public void SetControls(double nextGain,bool nextMuted,bool worldAllowed){
        Lane.Verify();if(!double.IsFinite(nextGain)||nextGain<0||nextGain>1)throw new ArgumentOutOfRangeException(nameof(nextGain));
        if(closed)return;
        if(revision!=0&&gain==nextGain&&muted==nextMuted&&allowed==worldAllowed)return;
        gain=nextGain;muted=nextMuted;allowed=worldAllowed;configured=false;int current=++revision;
        ControlsStatus="pending";ControlsStateRevision++;
        foreach(var registration in owners.Values)registration.Pending=null; // No mute/unmute backfill.
        if(audio==null){ControlsStatus="unavailable";Record("configure",new(false,"Unavailable"));return;}
        _=Invoke("configure",()=>audio.ConfigureAsync(gain,muted,allowed),reply=>{if(!closed&&revision==current){configured=reply.Success;ControlsStatus=reply.Success?"applied":"failed";ControlsStateRevision++;}});
    }
    public void Synchronize(int map,IReadOnlyList<BlockSnapshot> blocks,long now){Lane.Verify();if(closed)return;var delta=controller.Synchronize(map,blocks,now);committedMap=map;committedBlocks=blocks.ToArray();Apply(delta);}
    public void RetireLifetime(object lifetime,long now){Lane.Verify();if(committedMap is not int map)return;Synchronize(map,committedBlocks.Where(b=>!ReferenceEquals(b.Lifetime,lifetime)).ToArray(),now);}
    public void Clear(long now){Lane.Verify();committedMap=null;committedBlocks=[];Apply(controller.Clear(now));}
    void Apply(LifecycleDelta delta){
        foreach(var owner in delta.Retired){owners.Remove(owner);if(audio!=null)_=Invoke("retire",()=>audio.RetireAsync(owner));}
        foreach(var owner in delta.Created){
            var state=new Registration();owners.Add(owner,state);
            if(audio==null){state.Ready=false;continue;}
            _=Invoke("register",()=>audio.RegisterAsync(owner),reply=>{
                if(closed||!controller.IsCurrent(owner)||!owners.TryGetValue(owner,out var current)||!ReferenceEquals(current,state))return;
                state.Ready=reply.Success;var pending=state.Pending;state.Pending=null;
                if(reply.Success&&pending!=null)Dispatch(pending);
            });
        }
    }
    public IReadOnlyList<EffectFrameState> Frames(){Lane.Verify();return controller.Frames();}
    public void Pump(){
        Lane.Verify();Lane.Drain();if(closed)return;
        foreach(var decision in controller.Advance(clock(),Gate)){
            if(decision.Request is not {} request)continue;
            if(!owners.TryGetValue(request.Candidate.Owner,out var state))continue;
            if(state.Ready==true)Dispatch(request);else if(state.Ready==null)state.Pending=request;
        }
    }
    void Dispatch(EventRequest request){
        var reply=controller.TryDispatch(request,clock(),Gate,candidate=>{_=Invoke("play",()=>audio!.PlayAsync(candidate.SoundId,candidate.Owner));});
        Record("dispatch",new(reply.Status==DispatchStatus.Submitted,reply.Status.ToString(),reply.Error));
    }
    public Task<EffectAudioReply> ShutdownAsync(TimeSpan budget){
        Lane.Verify();if(budget<=TimeSpan.Zero||budget.TotalMilliseconds>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(budget));
        if(shutdown!=null)return shutdown;
        closed=true;configured=false;ControlsStatus="closed";ControlsStateRevision++;++revision;Clear(clock());
        if(audio==null)return shutdown=Task.FromResult(new EffectAudioReply(true,"NoTransport"));
        // Invoke synchronously so the transport fence is published before yielding.
        var operation=Invoke("shutdown-transport",()=>audio.ShutdownWithinAsync(budget));
        return shutdown=Bounded();
        async Task<EffectAudioReply> Bounded(){
            try{return await operation.WaitAsync(budget).ConfigureAwait(false);}
            catch(TimeoutException){var reply=new EffectAudioReply(false,"CleanupPending",audio.LastMaintenance);Record("shutdown-deadline",reply);return reply;}
        }
    }
}
