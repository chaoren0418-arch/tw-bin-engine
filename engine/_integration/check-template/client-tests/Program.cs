using P06;
using P24.MapEffectEvents;
using System.Text.Json;
using System.Security.Cryptography;
byte[]? localMetadata=LocalFixture.Load(args);if(localMetadata is null)return;
string root=args[0];int assertions=0;var cases=new List<object>();
var definition=Effect6016Definition.FromR27Metadata(localMetadata,"825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383");
void Check(bool yes,string why){assertions++;if(!yes)throw new Exception(why);}
void Case(string name,Action body){int start=assertions;body();cases.Add(new{name,assertions=assertions-start,status="pass"});}
void CompleteRegister(Effect6016Host host,FakeAudio fake){long count=host.ObservedReplies;fake.RegisterCompletion.SetResult(FakeAudio.Ok);Check(SpinWait.SpinUntil(()=>host.ObservedReplies>count,2000),"register callback queued");}
Case("F1-close-await-cannot-recreate-host",()=>{
 var transports=new List<FakeAudio>();ClientExcerpt.Accepted6016TransportFactory=_=>{var fake=new FakeAudio();transports.Add(fake);return fake;};
 var client=new ClientExcerpt(definition);client.Ensure();var first=client.Host;var closing=client.BeginClose();
 Check(first.Closed&&!closing.IsCompleted,"close synchronous fence before pending await");client.Reconnect();
 Check(transports.Count==1&&ReferenceEquals(first,client.Host),"nonterminal login change during close cannot replace host");
 transports[0].ShutdownCompletion.SetResult(FakeAudio.Ok);Check(closing.Wait(2000),"bounded close completed");
 Check(client.Host.Closed&&transports[0].ShutdownCalls==1,"awaited task covers sole host");client.Ensure();Check(transports.Count==1,"no recreation after close completes");
});
Case("F1-dispose-guard-and-normal-relogin-positive",()=>{
 int count=0;var transports=new List<FakeAudio>();ClientExcerpt.Accepted6016TransportFactory=_=>{count++;var f=new FakeAudio();transports.Add(f);return f;};
 var disposed=new ClientExcerpt(definition){Disposing=true};disposed.Ensure();Check(count==0,"dispose-start forbids creation");disposed.Disposing=false;disposed.IsDisposed=true;disposed.Ensure();Check(count==0,"disposed forbids creation");
 var client=new ClientExcerpt(definition);client.Ensure();var first=client.Host;var logout=client.Logout();transports[0].ShutdownCompletion.SetResult(FakeAudio.Ok);Check(logout.Wait(2000),"logout completes");client.Reconnect();
 Check(count==2&&!ReferenceEquals(first,client.Host)&&!client.Host.Closed,"normal relogin still creates new live host");
});
void RegisterScenario(bool renderable){
 var fake=new FakeAudio{DelayRegister=true};ClientExcerpt.Accepted6016TransportFactory=_=>fake;
 var client=new ClientExcerpt(definition);client.Ensure();client.Pump();client.Pump();Check(client.Host.ControlsApplied,"initial allowed controls ready");
 client.Host.Synchronize(4,[new BlockSnapshot("b",new object(),[new EffectPlacement(0,6016,1,1)])],0);
 client.State.Now=209;client.Pump();Check(fake.PlayCalls==0,"209ms request waits for register");
 client.State.Renderable=renderable;client.State.Now=220;CompleteRegister(client.Host,fake);fake.Calls.Clear();client.Pump();
 if(!renderable){Check(fake.PlayCalls==0,"F2 late register cannot use previous world gate");Check(fake.Calls.SequenceEqual(new[]{"configure:False"}),"disable published before draining; no play command");}
 else{Check(fake.PlayCalls==1,"normal ready register still plays same step");Check(fake.Calls.SequenceEqual(new[]{"play"}),"no unnecessary controls reset");}
 client.Pump();Check(fake.PlayCalls==(renderable?1:0),"repeated timer no replay");
}
Case("F2-unrenderable-before-register-completion-zero-play",()=>RegisterScenario(false));
Case("F2-normal-register-positive",()=>RegisterScenario(true));
var result=new{status="pass",cases=cases.Count,assertions,details=cases,productDllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Effect6016Host).Assembly.Location))).ToLowerInvariant(),scope="Actual product host/controller + verbatim client methods with explicit fake UI/world/transport; no Form/COM/audio/network/service/DB"};
File.WriteAllText(Path.Combine(root,"evidence/client-regression-result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {cases.Count} client cases / {assertions} checks");
sealed class FakeAudio:IEffectAudio {
 public static EffectAudioReply Ok=>new(true,"ExplicitFakeSuccess");public bool DelayRegister;public int ShutdownCalls,PlayCalls;public List<string> Calls=[];
 public TaskCompletionSource<EffectAudioReply> RegisterCompletion=new(TaskCreationOptions.RunContinuationsAsynchronously),ShutdownCompletion=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public TaskCompletionSource Released=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public Task<EffectAudioReply> RegisterAsync(EffectOwner owner)=>DelayRegister?RegisterCompletion.Task:Task.FromResult(Ok);
 public Task<EffectAudioReply> RetireAsync(EffectOwner owner){Calls.Add("retire");return Task.FromResult(Ok);}
 public Task<EffectAudioReply> PlayAsync(int id,EffectOwner owner){PlayCalls++;Calls.Add("play");return Task.FromResult(Ok);}
 public Task<EffectAudioReply> ConfigureAsync(double gain,bool muted,bool enabled){Calls.Add("configure:"+enabled);return Task.FromResult(Ok);}
 public Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan budget){ShutdownCalls++;return ShutdownCompletion.Task;}
 public Task WhenReleased=>Released.Task;public EffectAudioReply LastMaintenance=>Ok;
}
