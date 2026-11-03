using P06;
using P24.MapEffectEvents;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

string root=args[0],assets=args[1];int checks=0;var cases=new List<object>();
byte[] metadata=File.ReadAllBytes(Path.Combine(root,"fixtures/6016.json"));
var definition=Effect6016Definition.FromR27Metadata(metadata,"825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383");
var appearance=new MapEffectAppearance(metadata);
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
void Case(string name,Action body){int before=checks;body();cases.Add(new{name,checks=checks-before,status="pass"});}
void Set(object o,string name,object? value){var t=o.GetType();var field=t.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);if(field!=null)field.SetValue(o,value);else t.GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(o,value);}
object? Call(object o,string name,params object[] values)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(o,values);
OriginalMapBlock Block(string name="b")=>new(){Name=name,Sha256="synthetic-host-fixture",X=0,Y=0,Ground=[],Cover=[],Groups=[],Triggers=[],Attributes=[],Tiles=[],Effects=[new(20,1,2,0,appearance),new(6016,3,4,6,appearance)]};
(Effect6016Host host,FakeAudio fake,EffectHostLane lane,Clock clock,OriginalMapBlock block) Setup(bool delayed=false){
    var clock=new Clock();var lane=new EffectHostLane();var fake=new FakeAudio{DelayRegister=delayed};var host=new Effect6016Host(new(definition,()=>0),fake,lane,()=>clock.Now);
    host.SetControls(.5,false,true);lane.Drain();var block=Block();host.Synchronize(4,[WorldSession.Snapshot6016(block)],0);lane.Drain();return(host,fake,lane,clock,block);
}
void Wait(Func<bool> done){if(!SpinWait.SpinUntil(done,2000))throw new Exception("async observation did not finish");}
Case("original-ordinal-before-filter-and-retained-reference",()=>{
    var(h,f,l,c,b)=Setup();Check(h.Frames().Single().Owner.Key.RecordOrdinal==1,"ordinal must be original 1");var owner=h.Frames().Single().Owner;
    c.Now=209;h.Pump();Check(f.Plays.Count==1&&f.Plays[0].id==3332,"full int sound and timer-only event");
    h.Synchronize(4,[WorldSession.Snapshot6016(b)],209);Check(h.Frames().Single().Owner==owner&&h.Frames().Single().StartedAtMs==0,"retained block preserves phase");
    for(int i=0;i<5;i++)WorldSession.MapEffectFrame(b.Effects[1],b.Name,1,4,99999,h.Frames().ToDictionary(v=>v.Owner.Key));
    Check(f.Plays.Count==1&&h.Frames().Single().Frame==0,"draw reads shared frame without advance");
    c.Now=417;h.Pump();Check(h.Frames().Single().Frame==1&&f.Plays.Count==1,"24 Hz / 5 tick policy");
});
Case("retire-before-new-register-and-old-lifetime-fence",()=>{
    var(h,f,l,c,b)=Setup();var old=h.Frames().Single().Owner;f.Calls.Clear();var next=Block();h.Synchronize(4,[WorldSession.Snapshot6016(next)],0);
    Check(f.Calls.SequenceEqual(new[]{"retire","register"}),"retire enqueued before replacement register");Check(f.Retired.Contains(old),"retire facade called synchronously");Check(h.Frames().Single().Owner.Generation>old.Generation,"new generation");
    h.RetireLifetime(b,0);Check(h.Frames().Count==1,"late eviction of old reference cannot remove replacement");
    h.RetireLifetime(next,0);Check(h.Frames().Count==0,"real lifetime eviction immediate");
});
Case("late-register-marshals-to-lane-and-same-step-only",()=>{
    var(h,f,l,c,b)=Setup(true);c.Now=209;h.Pump();c.Now=220;h.Pump();Check(f.Plays.Count==0,"waiting does not block or submit");
    long observed=h.ObservedReplies;Task.Run(()=>f.Pending[0].SetResult(FakeAudio.Ok)).GetAwaiter().GetResult();Wait(()=>h.ObservedReplies>observed);
    Check(f.Plays.Count==0,"background completion cannot dispatch on worker thread");l.Drain();Check(f.Plays.Count==1,"same-step pending survived multiple timer samples");Check(f.CallThreads.All(t=>t==Environment.CurrentManagedThreadId),"all commands on host lane");
});
Case("stale-register-after-time-boundary-without-next-pump",()=>{
    var(h,f,l,c,b)=Setup(true);c.Now=209;h.Pump();c.Now=417;long n=h.ObservedReplies;f.Pending[0].SetResult(FakeAudio.Ok);Wait(()=>h.ObservedReplies>n);l.Drain();Check(f.Plays.Count==0,"TryDispatch checks real clock before next pump");
});
Case("late-register-after-clear-and-reload",()=>{
    var(h,f,l,c,b)=Setup(true);c.Now=209;h.Pump();h.Clear(c.Now);h.Synchronize(4,[WorldSession.Snapshot6016(Block())],c.Now);
    long n=h.ObservedReplies;f.Pending[0].SetResult(FakeAudio.Ok);Wait(()=>h.ObservedReplies>n);l.Drain();Check(f.Plays.Count==0,"old generation cannot submit after clear/reload");Check(h.Frames().Single().StartedAtMs==209,"replacement starts at commit");
});
Case("mute-unmute-and-missing-consume-no-backfill",()=>{
    var(h,f,l,c,b)=Setup(true);c.Now=209;h.Pump();h.SetControls(.5,true,true);l.Drain();h.SetControls(.5,false,true);l.Drain();
    long n=h.ObservedReplies;f.Pending[0].SetResult(FakeAudio.Ok);Wait(()=>h.ObservedReplies>n);l.Drain();Check(f.Plays.Count==0,"muted revision invalidates pending registration event even after unmute");
    c.Now=2709;h.Pump();Check(f.Plays.Count==1,"only next fresh loop dispatches");
    var missing=new Effect6016Host(new(definition,()=>0),null,new(),()=>c.Now);missing.SetControls(.5,false,true);missing.Synchronize(4,[WorldSession.Snapshot6016(b)],0);missing.Pump();Check(missing.Frames().Single().AbsoluteStep==13,"missing transport still advances controller");
});
Case("configure-register-play-failures-observed-without-retry",()=>{
    var(h,f,l,c,b)=Setup();f.PlayReply=new(false,"MissingSource");c.Now=209;h.Pump();h.Pump();Check(f.Plays.Count==1,"missing source no replay");Check(h.Observations.Any(o=>o.Operation=="play"&&o.Reply.Status=="MissingSource"),"reply details observable");
    f.ConfigureReply=new(false,"ControlsFailed");h.SetControls(.3,false,true);l.Drain();c.Now=2709;h.Pump();Check(f.Plays.Count==1&&!h.ControlsApplied,"failed gain does not pretend applied");
    var(h2,f2,l2,c2,b2)=Setup(true);c2.Now=209;h2.Pump();long n=h2.ObservedReplies;f2.Pending[0].SetException(new IOException("fake register failure"));Wait(()=>h2.ObservedReplies>n);l2.Drain();Check(f2.Plays.Count==0&&h2.Observations.Any(o=>o.Reply.Status=="TaskFailed"),"faulted register consumed and observed");
});
Case("invalid-gain-and-off-lane-rejected",()=>{
    var(h,f,l,c,b)=Setup();bool rejected=false;try{h.SetControls(double.NaN,false,true);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected&&h.Gain==.5,"invalid gain preserves settings");
    rejected=Task.Run(()=>{try{h.Pump();return false;}catch(InvalidOperationException){return true;}}).GetAwaiter().GetResult();Check(rejected,"wrong-lane mutation fails explicitly");
});
Case("bounded-shutdown-keeps-late-reply-observable",()=>{
    var(h,f,l,c,b)=Setup();f.DelayShutdown=true;var watch=Stopwatch.StartNew();var reply=h.ShutdownAsync(TimeSpan.FromMilliseconds(20)).GetAwaiter().GetResult();
    Check(watch.ElapsedMilliseconds<1500&&!reply.Success&&reply.Status=="CleanupPending","host deadline works even if fake never finishes");Check(h.Closed&&h.Frames().Count==0&&f.Retired.Count==1,"shutdown clears/retire before await");Check(!h.WhenReleased.IsCompleted,"timeout not claimed released");
    long n=h.ObservedReplies;f.ShutdownCompletion.SetResult(FakeAudio.Ok);Wait(()=>h.ObservedReplies>n);Check(h.Observations.Any(o=>o.Operation=="shutdown-transport"&&o.Reply.Success),"late shutdown result observed");
});

// Read real packaged map metadata and execute actual WorldSession/cache hooks without GUI or sockets.
OriginalMapCache cache=new(assets,32635,32782,4);
WorldSession World(Effect6016Host host,Clock clock){var w=new WorldSession{CacheEnabled=true};Set(w,"SceneClock",(Func<long>)(()=>clock.Now));Set(w,"MapAssetsRoot",assets);w.Attach6016(host);return w;}
void Install(WorldSession w,OriginalMapCache value,long now=0){Set(w,"MapCache",value);Call(w,"Sync6016",now);}
Case("real-cache-original-record-and-renderer-phase",()=>{
    var block=cache.Slots.OfType<OriginalMapBlock>().Single(b=>b.Name=="7ffd8000");Check(block.Effects[1].Id==6016&&block.Effects[1].Offset==162845,"real parser preserved ordinal and offset");
    var(h,f,l,c,b)=Setup();h.Clear(0);var w=World(h,c);Install(w,cache);l.Drain();var frame=h.Frames().Single(s=>s.Owner.Key.BlockKey==block.Name&&s.Owner.Key.RecordOrdinal==1);Check(frame.X==32635&&frame.Y==32782,"real committed placement");
    c.Now=209;h.Pump();Check(WorldSession.MapEffectFrame(block.Effects[1],block.Name,1,4,999999,w.Frames6016()).Key=="0","actual draw selector shares controller phase");
    Check(WorldSession.MapEffectFrame(block.Effects[1],block.Name,1,4,0,new Dictionary<EffectKey,EffectFrameState>()).Key=="11","missing state uses quiet initial frame");
    w.Reset();Check(h.Frames().Count==0,"real WorldSession.Reset clears immediately");
});
Case("real-map-commit-rejects-stale-load-before-register",()=>{
    var(h,f,l,c,b)=Setup();h.Clear(0);var w=World(h,c);
    var actor=new OwnActor(32635,32782,42,0,0,0,0,0,0,0,"fixture","",0,0,"","",0,0,0,0,0,0,0);Set(w,"Actor",actor);Set(w,"MapId",(ushort)4);
    var request=new MapLoadRequest(w.WorldEpoch,4,42,32635,32782,1);Set(w,"PendingMapLoad",request);int count=f.Calls.Count;
    Check(!(bool)Call(w,"CompleteMapLoad",request with{Revision=0},cache)!&&f.Calls.Count==count,"stale async load must not register");
    Check((bool)Call(w,"CompleteMapLoad",request,cache)!&&h.Frames().Count>0,"accepted commit registers");
    Call(w,"MapFailed",new IOException("explicit synthetic load failure"));Check(h.Frames().Count==0&&w.MapCache==null,"real load failure clears");
});
Case("real-reset-opacity-and-map-packet-clear",()=>{
    var(h,f,l,c,b)=Setup();h.Clear(0);var w=World(h,c);Install(w,cache);Call(w,"ResetOpacity");Check(h.Frames().Count==0,"ResetOpacity clears");Install(w,cache);
    w.Receive(LoginPackets.Pad([206,0,0,0]),"fixture");Check(h.Frames().Count==0&&w.MapId==0,"actual map-change packet retires before new load");
});
Case("real-eviction-fence-before-load-and-failed-shift-clear",()=>{
    var(h,f,l,c,b)=Setup();h.Clear(0);var w=World(h,c);Install(w,cache);int slot=Array.FindIndex(cache.Slots,b=>b?.Name=="7ffd8000");
    int direction=Enumerable.Range(0,8).First(d=>new[]{OriginalMapCache.DestA[d],OriginalMapCache.DestB[d]}.Contains(slot));bool seen=false;
    void Fail(OriginalMapBlock block){if(block.Name!="7ffd8000")return;seen=true;Check(!h.Frames().Any(s=>s.Owner.Key.BlockKey==block.Name),"evict fence before native-style load work");throw new IOException("synthetic load boundary failure");}
    cache.Evicting+=Fail;try{cache.Shift(direction);}catch(IOException ex){Call(w,"MapFailed",ex);}finally{cache.Evicting-=Fail;}
    Check(seen&&h.Frames().Count==0&&w.MapCache==null,"partial failed shift all owners cleared");
});
Case("candidate-no-native-transport-loaded",()=>Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name=="P24.Audio"),"fake-only product tests must not load native audio module"));
Case("configure-completion-order-and-unavailable-event-consumption",()=>{
    var(h,f,l,c,b)=Setup();f.DelayConfigure=true;h.SetControls(.3,false,true);h.SetControls(.4,false,true);
    c.Now=209;h.Pump();Check(f.Plays.Count==0,"pending configure is unavailable and consumes event");
    long n=h.ObservedReplies;f.Configurations[1].SetResult(FakeAudio.Ok);Wait(()=>h.ObservedReplies>n);l.Drain();Check(h.ControlsApplied,"new controls succeeded");
    n=h.ObservedReplies;f.Configurations[0].SetResult(new(false,"ControlsFailed"));Wait(()=>h.ObservedReplies>n);l.Drain();Check(h.ControlsApplied&&h.Gain==.4,"old configure result cannot reverse new state");
    h.Pump();Check(f.Plays.Count==0,"unavailable event not backfilled");
});
Case("retire-cleanup-failure-and-synchronous-play-throw-observed",()=>{
    var(h,f,l,c,b)=Setup();f.ThrowPlay=true;c.Now=209;h.Pump();Check(h.Observations.Any(o=>o.Operation=="play"&&o.Reply.Status=="HostCallFailed"),"synchronous transport fault observable");
    f.RetireReply=new(false,"CleanupPending");h.Clear(209);Check(h.Observations.Any(o=>o.Operation=="retire"&&o.Reply.Status=="CleanupPending"),"retire cleanup failure observable");Check(h.Frames().Count==0,"failed cleanup cannot keep actor current");
});
Case("other-effect-clock-unchanged-and-long-stall-no-replay-backlog",()=>{
    var(h,f,l,c,b)=Setup();var other=b.Effects[0];Check(ReferenceEquals(WorldSession.MapEffectFrame(other,b.Name,0,4,900,h.Frames().ToDictionary(v=>v.Owner.Key)),other.Appearance.At(900)),"non-6016 clock preserved");
    c.Now=60000;h.Pump();Check(f.Plays.Count==1,"long stall coalesces to latest event once");h.Pump();Check(f.Plays.Count==1,"repeated timer no replay");
});
Case("accepted-r3-default-factory-bound-without-creating-player",()=>{
    var factory=ClientForm.Accepted6016TransportFactory;
    Check(factory!=null&&factory.Method.DeclaringType?.FullName=="P24.HostAudio.AudioApiTransport","normal host factory is accepted r3 bridge, not unavailable stub");
    var expected=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"scope-update-r3.json"))).RootElement.GetProperty("acceptedAudioDll").GetString();
    Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"build/host/P24.Audio.dll")))).ToLowerInvariant()==expected,"rebuilt r3 module exactly accepted DLL");
    Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"assets/audio-manifest.json")))).ToLowerInvariant()=="87c6cd6867e0d81bdc627c1cac0a922891d650018211b58525de9226926ad9fe","trusted manifest copied intact");
});
var result=new{status="pass",cases=cases.Count,assertions=checks,details=cases,productDllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(WorldSession).Assembly.Location))).ToLowerInvariant(),gui=false,com=false,audioPlayback=false,sockets=false,services=false,database=false,scope="Actual isolated product DLL with explicit fake IEffectAudio; real map parser/lifecycle calls; no native acceptance"};
File.WriteAllText(Path.Combine(root,"evidence/host-result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {cases.Count} cases / {checks} checks");

sealed class Clock {public long Now;}
sealed class FakeAudio:IEffectAudio {
    public static EffectAudioReply Ok=>new(true,"FakeSuccess");
    public bool DelayRegister,DelayShutdown,DelayConfigure,ThrowPlay;public EffectAudioReply ConfigureReply=Ok,PlayReply=Ok,RetireReply=Ok;
    public List<TaskCompletionSource<EffectAudioReply>> Configurations=[];
    public List<string> Calls=[];public List<int> CallThreads=[];public List<(int id,EffectOwner owner)> Plays=[];public List<EffectOwner> Retired=[];
    public List<TaskCompletionSource<EffectAudioReply>> Pending=[];
    public TaskCompletionSource<EffectAudioReply> ShutdownCompletion=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Released=new(TaskCreationOptions.RunContinuationsAsynchronously);
    void Note(string name){Calls.Add(name);CallThreads.Add(Environment.CurrentManagedThreadId);}
    public Task<EffectAudioReply> RegisterAsync(EffectOwner owner){Note("register");if(!DelayRegister)return Task.FromResult(Ok);var t=new TaskCompletionSource<EffectAudioReply>(TaskCreationOptions.RunContinuationsAsynchronously);Pending.Add(t);return t.Task;}
    public Task<EffectAudioReply> RetireAsync(EffectOwner owner){Note("retire");Retired.Add(owner);return Task.FromResult(RetireReply);}
    public Task<EffectAudioReply> PlayAsync(int id,EffectOwner owner){Note("play");if(ThrowPlay)throw new IOException("explicit fake Play throw");Plays.Add((id,owner));return Task.FromResult(PlayReply);}
    public Task<EffectAudioReply> ConfigureAsync(double gain,bool muted,bool enabled){Note("configure");if(!DelayConfigure)return Task.FromResult(ConfigureReply);var t=new TaskCompletionSource<EffectAudioReply>(TaskCreationOptions.RunContinuationsAsynchronously);Configurations.Add(t);return t.Task;}
    public Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan budget){Note("shutdown");if(DelayShutdown)return ShutdownCompletion.Task;Released.TrySetResult();return Task.FromResult(Ok);}
    public Task WhenReleased=>Released.Task;public EffectAudioReply LastMaintenance=>new(true,"ExplicitFake");
}
