using P06;using P24.MapEffectEvents;using System.Text.Json;using System.Security.Cryptography;
string root=args[0];int assertions=0;var cases=new List<object>();
var definition=Effect6016Definition.FromR27Metadata(File.ReadAllBytes(Path.Combine(root,"fixtures/6016.json")),"825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383");
void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
void Case(string name,Action body){int start=assertions;body();cases.Add(new{name,assertions=assertions-start,status="pass"});}
string Command(double gain,bool mute,int id=1)=>JsonSerializer.Serialize(new{v=1,id,cmd="audioSettings",gain,muted=mute});
(Effect6016Host host,FakeAudio fake,EffectHostLane lane,AudioClientExcerpt client) Setup(){var f=new FakeAudio();var l=new EffectHostLane();var h=new Effect6016Host(new(definition),f,l,()=>0);h.SetControls(.5,false,true);l.Drain();return(h,f,l,new(h));}
Case("strict-command-schema-and-value-rejection",()=>{
 var valid=Command(.23,true);using(var d=JsonDocument.Parse(valid)){var c=AudioSettingsCommand.Parse(d.RootElement);Check(c.Gain==.23&&c.Muted,"valid exact command");}
 string[] bad=["[]","{}",valid.Replace("\"gain\":0.23","\"gain\":\"0.23\""),valid.Replace("\"gain\":0.23","\"gain\":null"),valid.Replace("\"gain\":0.23","\"gain\":-0.01"),valid.Replace("\"gain\":0.23","\"gain\":1.01"),valid.Replace("\"gain\":0.23","\"gain\":1e999"),valid.Replace("\"muted\":true","\"muted\":1"),valid.Replace("\"muted\":true","\"muted\":\"true\""),valid.Replace("\"id\":1","\"id\":0"),valid.Replace("\"id\":1","\"id\":1.2"),valid.Replace("\"v\":1","\"v\":2"),valid.Replace("audioSettings","audioOther"),valid[..^1]+",\"gain\":0.2}",valid[..^1]+",\"extra\":true}"];
 foreach(var json in bad){bool rejected=false;try{using var d=JsonDocument.Parse(json);AudioSettingsCommand.Parse(d.RootElement);}catch(FormatException){rejected=true;}Check(rejected,"strict rejection: "+json);}
});
Case("web-to-native-immediate-gate-and-no-recursive-events",()=>{
 var(h,f,l,c)=Setup();int n=f.Calls.Count;f.Delay=true;c.Web(Command(.257,true));
 Check(c.View.gain==26&&c.View.muted&&c.State.gain==.257,"native rounded control does not overwrite exact shared gain");
 Check(f.Calls.Count==n+1&&f.Calls[^1]==(.257,true,true),"one immediate Configure despite two control events during render");
 Check(!c.GameFocus&&c.Held==0,"web input clears game focus/held keys");Check(!c.State.applied&&c.State.status=="pending"&&!c.Replies[^1].text.Contains("已套用"),"pending reply cannot claim applied");
 int revision=c.State.revision;c.Render();Check(c.State.revision==revision&&f.Calls.Count==n+1,"render is side-effect free");
});
Case("native-to-web-same-model-and-async-signature",()=>{
 var(h,f,l,c)=Setup();f.Delay=true;c.NativeGain(70);c.NativeMute(true);Check(c.States.Last().gain==.7&&c.States.Last().muted,"native changes immediately publish shared state");
 string before=JsonSerializer.Serialize(c.State);long n=h.ObservedReplies;f.Pending[^1].SetResult(new(true,"Configured"));Check(SpinWait.SpinUntil(()=>h.ObservedReplies>n,2000),"config observed");l.Drain();c.Render();
 Check(c.State.applied&&c.View.notice=="已套用靜音","only successful current Configure claims applied");Check(before!=JsonSerializer.Serialize(c.State),"async completion changes serialized signature without input/world change");
});
Case("failed-and-out-of-order-replies-do-not-falsely-apply",()=>{
 var(h,f,l,c)=Setup();f.Delay=true;c.Web(Command(.2,false));c.Web(Command(.3,false,2));long n=h.ObservedReplies;
 f.Pending[1].SetResult(new(false,"ControlsFailed"));Check(SpinWait.SpinUntil(()=>h.ObservedReplies>n,2000),"failure observed");l.Drain();c.Render();Check(!c.State.applied&&c.State.status=="failed"&&c.View.notice=="聲音設定未套用","failure visible");
 n=h.ObservedReplies;f.Pending[0].SetResult(new(true,"Configured"));Check(SpinWait.SpinUntil(()=>h.ObservedReplies>n,2000),"old reply observed");l.Drain();Check(!c.State.applied&&c.State.gain==.3,"old success cannot mark newer request applied");
});
Case("closed-or-closing-host-panel-cannot-revive-and-relogin-preserves-preference",()=>{
 var(h,f,l,c)=Setup();c.NativeGain(65);h.ShutdownAsync(TimeSpan.FromMilliseconds(20)).GetAwaiter().GetResult();int n=f.Calls.Count;c.Web(Command(.9,true));c.NativeGain(90);Check(c.State.gain==.65&&!c.State.editable&&f.Calls.Count==n,"closed host gets no Configure or settings mutation");
 var newHost=new Effect6016Host(new(definition),f,l,()=>0);c.Attach(newHost);c.webClosing=true;c.Web(Command(.2,true));Check(c.State.gain==.65&&f.Calls.Count==n,"closing guard rejects request without creating host");
 c.webClosing=false;c.NativeMute(true);Check(c.State.gain==.65&&c.State.muted&&f.Calls.Last()==(.65,true,true),"normal relogin uses preserved settings");
});
Case("unavailable-and-render-exception-restores-guard",()=>{
 var settings=new PlayerAudioSettings();var host=new Effect6016Host(new(definition),null,new(),()=>0);host.SetControls(.5,false,true);Check(!settings.Snapshot(host,false).applied&&settings.Snapshot(host,false).status=="unavailable","missing transport never applied");
 try{settings.Render(host,false,_=>throw new Exception("fake control failure"));}catch(Exception){}Check(!settings.Rendering&&settings.Change(.8,false,true),"render failure cannot permanently suppress user edits");
});
Case("exact-request-acknowledgements-and-noop-without-state-change",()=>{
 var(h,f,l,c)=Setup();int n=f.Calls.Count;string initial=JsonSerializer.Serialize(c.State);
 c.Web(Command(.5,false,101));Check(f.Calls.Count==n&&JsonSerializer.Serialize(c.State)==initial,"no-op retains identical state and does not Configure");
 Check(c.AudioReplies[^1].id==101&&c.AudioReplies[^1].state.applied,"no-op still explicitly acknowledged with exact request and applied state");
 f.Delay=true;c.Web(Command(.6,false,102));c.Web(Command(.8,false,103));
 Check(c.AudioReplies[^2].id==102&&c.AudioReplies[^2].state.gain==.6,"first reply frozen to first accepted request");
 Check(c.AudioReplies[^1].id==103&&c.AudioReplies[^1].state.gain==.8&&!c.AudioReplies[^1].state.applied,"latest request snapshot distinct and not prematurely applied");
 c.webClosing=true;c.Web(Command(.9,true,104));Check(c.AudioReplies[^1].id==104&&!c.AudioReplies[^1].state.editable,"closing refusal includes exact correlated state");
});
File.WriteAllText(Path.Combine(root,"evidence/audio-settings-result.json"),JsonSerializer.Serialize(new{status="pass",cases=cases.Count,assertions,details=cases,productDllSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Effect6016Host).Assembly.Location))).ToLowerInvariant(),scope="Actual product model/parser/host and verbatim client methods with managed control/bridge/world fakes; no GUI/COM/playback"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {cases.Count} audio cases / {assertions} checks");
sealed class FakeAudio:IEffectAudio {
 public bool Delay;public List<(double,bool,bool)> Calls=[];public List<TaskCompletionSource<EffectAudioReply>> Pending=[];
 static Task<EffectAudioReply> Ok()=>Task.FromResult(new EffectAudioReply(true,"FakeSuccess"));
 public Task<EffectAudioReply> ConfigureAsync(double g,bool m,bool e){Calls.Add((g,m,e));if(!Delay)return Ok();var t=new TaskCompletionSource<EffectAudioReply>(TaskCreationOptions.RunContinuationsAsynchronously);Pending.Add(t);return t.Task;}
 public Task<EffectAudioReply> RegisterAsync(EffectOwner o)=>Ok();public Task<EffectAudioReply> RetireAsync(EffectOwner o)=>Ok();public Task<EffectAudioReply> PlayAsync(int id,EffectOwner o)=>Ok();public Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan t)=>Ok();public Task WhenReleased=>Task.CompletedTask;public EffectAudioReply LastMaintenance=>new(true,"ExplicitFake");
}
