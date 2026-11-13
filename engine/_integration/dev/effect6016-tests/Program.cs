using P06;
using P24.Audio;
using P24.HostAudio;
using P24.MapEffectEvents;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

// No ClientForm/LoginConnection, sockets, services or database. Fixtures are generated
// under the caller's scratch directory; native mode plays only synthetic sine waves.
static class Program
{
    static int checks;
    static readonly List<object> cases=[];
    static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,IncludeFields=true};
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static void Case(string name,Action run){int before=checks;run();cases.Add(new{name,checks=checks-before,status="pass"});}
    static void Set(object o,string name,object value){var f=o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);if(f!=null)f.SetValue(o,value);else o.GetType().GetProperty(name)!.SetValue(o,value);}
    static void Wait(Func<bool> done){if(!SpinWait.SpinUntil(done,5000))throw new TimeoutException("6016 reply deadline");}
    static string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static BlockSnapshot Block(string key,object? lifetime=null,int ordinal=0)=>new(key,lifetime??new object(),[new(ordinal,6016,32635,32782)]);
    static byte[] Metadata()=>JsonSerializer.SerializeToUtf8Bytes(new{
        id=6016,tickRate=24,flags=0,
        sequence=Effect6016Definition.SequenceFrames.Select((frame,i)=>new{frame,ticks=5,events=i==1?"<3332<1340<1340":""}),
        frames=Enumerable.Range(0,12).Select(index=>new{index})});
    static Effect6016Definition Definition(){var b=Metadata();return Effect6016Definition.FromR27Metadata(b,Sha(b));}
    static WorldSession World(Effect6016Host h,Clock clock){var w=new WorldSession();Set(w,"SceneClock",(Func<long>)(()=>clock.Now));w.Attach6016(h);return w;}
    static (Effect6016Host h,MemoryAudio a,Clock c) Setup(Func<int>? slot=null){var c=new Clock();var a=new MemoryAudio();var h=new Effect6016Host(new(Definition(),slot??(()=>0)),a,new(),()=>c.Now);h.SetControls(.5,false,true);h.Pump();return(h,a,c);}
    static void Managed()
    {
        Case("enter-retain-leave-reenter-and-map206",()=>{
            var(h,a,c)=Setup();var w=World(h,c);var b=Block("harbor");h.Synchronize(4,[b],0);h.Pump();var first=h.Frames().Single();
            c.Now=208;h.Pump();Check(a.Plays.Count==0,"before event boundary");c.Now=209;h.Pump();Check(a.Plays.Count==1,"enter full int3332");
            Check(a.Plays[0].id==3332,"sound ID never narrowed to byte");
            h.Synchronize(4,[b],209);Check(h.Frames().Single().Owner==first.Owner&&h.Frames().Single().StartedAtMs==0,"retained phase");
            h.RetireLifetime(b.Lifetime,209);Check(h.Frames().Count==0&&a.Retired.Contains(first.Owner),"leave retires current owner");
            h.Synchronize(4,[Block("harbor")],209);h.Pump();var second=h.Frames().Single();Check(second.Owner.Generation>first.Owner.Generation,"reenter new generation");
            h.RetireLifetime(b.Lifetime,209);Check(h.Frames().Count==1,"old eviction cannot remove new lifetime");
            w.Receive(LoginPackets.Pad([206,0,0,0]),"offline-6016");Check(h.Frames().Count==0&&a.Retired.Contains(second.Owner),"actual S206 immediately retires");
            c.Now=10000;h.Pump();Check(a.Plays.Count==1,"no post-map events");
            Check(h.Observations.Any(o=>o.Operation=="play"&&o.Owner==first.Owner&&o.Candidate?.SoundId==3332),"play observation retains exact world owner and candidate");
            Check(h.Observations.Any(o=>o.Operation=="retire"&&o.Owner==first.Owner),"retire observation carries same owner");
        });
        Case("shared-cooldown-different-id-overlap-and-no-paint-events",()=>{
            int n=0;var(h,a,c)=Setup(()=>new[]{0,0,1}[n++%3]);h.Synchronize(4,[Block("a"),Block("b"),Block("c")],0);h.Pump();
            c.Now=209;h.Pump();Check(a.Plays.Select(p=>p.id).SequenceEqual(new[]{3332,1340}),"same ID cooldown, different IDs overlap");
            Check(h.Observations.Count(o=>o.Operation=="dispatch"&&o.Reply.Status=="Cooldown")==1,"shared cooldown observable");
            for(int i=0;i<10;i++){h.Frames();h.Pump();}Check(a.Plays.Count==2,"repeat pump/frame read no replay");
            c.Now=2709;h.Pump();Check(a.Plays.Count==4,"new cycle admits same IDs again");
            c.Now=60000;h.Pump();Check(a.Plays.Count==6,"long pause coalesces events");
        });
        Case("delayed-registration-retire-mute-and-shutdown-fences",()=>{
            var(h,a,c)=Setup();a.Delay=true;var b=Block("a");h.Synchronize(4,[b],0);c.Now=209;h.Pump();
            h.Clear(c.Now);a.Complete();h.Pump();Check(a.Plays.Count==0,"late registration after retirement cannot play");
            h.Synchronize(4,[Block("a")],209);c.Now=418;h.Pump();h.SetControls(.5,true,true);h.SetControls(.5,false,true);a.Complete();h.Pump();
            Check(a.Plays.Count==0,"mute/restore discards pending event");c.Now=2918;h.Pump();Check(a.Plays.Count==1,"restored fresh cycle plays");
            var result=h.ShutdownAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();Check(result.Success&&h.Closed&&h.Frames().Count==0,"shutdown retires");
            c.Now=10000;h.Pump();Check(a.Plays.Count==1,"closed host cannot dispatch");
        });
        Case("pending-configuration-and-stale-step-no-backfill",()=>{
            var(h,a,c)=Setup();a.Delay=true;h.Synchronize(4,[Block("a")],0);c.Now=209;h.Pump();c.Now=417;a.Complete();h.Pump();
            Check(a.Plays.Count==0,"late registration after step boundary is stale");a.Delay=false;c.Now=2709;h.Pump();Check(a.Plays.Count==1,"fresh cycle after stale completion");
            h.SetControls(.5,true,true);c.Now=5209;h.Pump();h.SetControls(.5,false,true);h.Pump();Check(a.Plays.Count==1,"muted cycle never backfilled");
            c.Now=7709;h.Pump();Check(a.Plays.Count==2,"next unmuted cycle dispatches");
        });
    }
    // T460（#1045）：合成「客戶端根目錄」——Sound/<id>.wav（正弦波），pin 與產品 Effect6016Pins 同形（mp3 不可用、wav 有大小與 SHA），
    // 只是大小／SHA 換成合成檔的值。不再產生 audio-manifest.json。
    static (string root,AssetPin[] pins) SyntheticAudio(string scratch)
    {
        string root=Path.Combine(scratch,"synthetic-client");Directory.CreateDirectory(Path.Combine(root,"Sound"));var pins=new List<AssetPin>();
        foreach(int id in new[]{3332,1340}){
            string path=Path.Combine(root,$"Sound/{id}.wav");
            using(var w=new BinaryWriter(File.Create(path))){int count=22050*8;w.Write("RIFF"u8);w.Write(36+count*2);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(22050);w.Write(44100);w.Write((short)2);w.Write((short)16);w.Write("data"u8);w.Write(count*2);for(int i=0;i<count;i++)w.Write((short)(6000*Math.Sin(2*Math.PI*(id==3332?440:660)*i/22050)));}
            var bytes=File.ReadAllBytes(path);
            pins.Add(new(id,"mp3",$"Sound/{id}.mp3",null,null));
            pins.Add(new(id,"wav",$"Sound/{id}.wav",bytes.Length,Sha(bytes)));
        }
        return(root,pins.ToArray());
    }
    static object ReadonlyMap(string assets)
    {
        var cache=new OriginalMapCache(assets,32635,32782,4);
        var(h,a,c)=Setup();var w=World(h,c);Set(w,"MapCache",cache);
        var sync=typeof(WorldSession).GetMethod("Sync6016",BindingFlags.Instance|BindingFlags.NonPublic)!;
        sync.Invoke(w,[0L]);h.Pump();var harbor=h.Frames().Single(x=>x.Owner.Key.BlockKey=="7ffd8000"&&x.Owner.Key.RecordOrdinal==1);
        Check(harbor.X==32635&&harbor.Y==32782,"real map4 placement");
        Check(cache.Slots.Single(b=>b?.Name=="7ffd8000")!.Effects[1].Offset==162845,"real File Offset / original ordinal");
        var placements=h.Frames();var blocks=cache.Slots.OfType<OriginalMapBlock>().Select(b=>new{b.Name,b.Sha256}).ToArray();
        // Ground passability is only a static candidate, never a safe-route guarantee.
        var standing=Enumerable.Range(32625,21).SelectMany(x=>Enumerable.Range(32772,21).Select(y=>(x,y)))
            .Where(p=>Enumerable.Range(0,8).Any(d=>cache.Passable(p.x,p.y,d))).OrderBy(p=>Math.Abs(p.x-32635)+Math.Abs(p.y-32782)).Take(5)
            .Select(p=>new{x=p.x,y=p.y}).ToArray();
        c.Now=209;h.Pump();Check(a.Plays.Count>0,"real map snapshot dispatches");
        var route=new List<object>();
        for(int x=32634;x>=32507&&h.Frames().Count>0;x--){if(cache.Move(x,32782)){sync.Invoke(w,[c.Now]);h.Pump();route.Add(new{x,y=32782,cache.OriginX,cache.OriginY,owners=h.Frames().Count});}}
        Check(h.Frames().Count==0,"west cache eviction eventually removes all6016");
        return new{scope="唯讀 r33 素材＋目前產品解析/快取，未啟動遊戲；路線只有快取與地形計算",blocks,placements,standing,westCacheTransitions=route};
    }
    static object Native(string scratch)
    {
        var(root,pins)=SyntheticAudio(scratch);
        // 產品工廠的契約與根目錄檢查（合成檔不符產品 pin，所以真正播放走同一條 DirectShow 鏈＋合成 pin）。
        Check(AudioPlayer.TryCreate(root,AudioApiTransport.AudioContract,32,out var product,out var error),error);
        Check(product!.ShutdownWithinAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult().Success,"product factory released");
        Check(!AudioPlayer.TryCreate(root,"p24-audio-assets-v2",32,out _,out _),"old manifest contract rejected");
        var player=AudioPlayer.NativeForTests(AudioAssets.FromClientRoot(root,pins),32);
        // Same T064 factory/lifetime, no combat Play calls and no game files needed.
        var keepalive=AudioPlayer.CreateCombat(root);var c=new Clock();int slot=0;
        var h=new Effect6016Host(new(Definition(),()=>slot++%2),new AudioApiTransport(player!),new(),()=>c.Now);
        var w=World(h,c);var stages=new List<object>();string[]? instances=null;
        void Barrier(string op,int before){Wait(()=>h.Observations.Count(x=>x.Operation==op)>before);h.Pump();}
        void Controls(bool mute){int before=h.Observations.Count(x=>x.Operation=="configure");h.SetControls(.5,mute,true);Barrier("configure",before);Check(h.ControlsApplied,"native controls");Check(keepalive.ConfigureAsync(.5,mute,true).GetAwaiter().GetResult().Success,"T064 lifetime controls");}
        void Enter(long now){int before=h.Observations.Count(x=>x.Operation=="register");c.Now=now;h.Synchronize(4,[Block("a"),Block("b")],now);Wait(()=>h.Observations.Count(x=>x.Operation=="register")==before+2);h.Pump();}
        void Event(long now,int count){c.Now=now;h.Pump();Wait(()=>h.Observations.Count(x=>x.Operation=="play")>=count);Check(h.Observations.Where(x=>x.Operation=="play").All(x=>x.Reply.Success),"all synthetic native plays succeed");}
        void Measure(string name,bool audible){var samples=Meter71.Sample((uint)Environment.ProcessId,1);float peak=samples.Max(x=>x.peak??-1);Check(samples.All(x=>x.sessions>0&&x.peak!=null),name+" own session exists");Check(audible?peak>0:peak==0,name+" peak");instances??=samples[0].instances;Check(samples.All(x=>x.instances.Order().SequenceEqual(instances.Order())),name+" same T064 session identity");stages.Add(new{name,peak,samples});}
        try{
            Controls(false);Enter(0);Event(209,2);Measure("enter",true);
            Event(2709,4);Check(h.Observations.Where(x=>x.Operation=="play").Select(x=>(AudioReply)x.Reply.Detail!).Max(x=>x.RemainingVoices)==4,"four simultaneous voices, no stealing/restart");Measure("overlap",true);
            int retires=h.Observations.Count(x=>x.Operation=="retire");
            h.RetireLifetime(GetLifetime(h,"a"),c.Now);Barrier("retire",retires);
            var remaining=(AudioReply)h.Observations.Last(x=>x.Operation=="retire").Reply.Detail!;Check(remaining.RemainingVoices==2,"retire only matching owner voices");Measure("other-owner-retained",true);
            h.Clear(c.Now);Wait(()=>h.Observations.Count(x=>x.Operation=="retire")>retires+1);Thread.Sleep(300);Measure("leave",false);
            Enter(5000);Event(5209,6);Measure("reenter",true);
            Controls(true);Thread.Sleep(300);Measure("muted",false);c.Now=7709;h.Pump();Check(h.Observations.Count(x=>x.Operation=="play")==6,"mute suppresses new scene events");
            Controls(false);Measure("restored-before-fresh-event",false);Event(10209,8);Measure("restored-fresh-event",true);
            retires=h.Observations.Count(x=>x.Operation=="retire");w.Receive(LoginPackets.Pad([206,0,0,0]),"offline-6016");Wait(()=>h.Observations.Count(x=>x.Operation=="retire")==retires+2);Thread.Sleep(300);Measure("map206",false);
            Check(h.Frames().Count==0,"map packet clears owners");
            Enter(13000);Event(13209,10);Measure("before-live-shutdown",true);
            Check(h.ShutdownAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult().Success,"scene native released");Check(h.WhenReleased.IsCompleted,"scene release task complete");Thread.Sleep(300);Measure("scene-shutdown-keepalive",false);
        }finally{h.ShutdownAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();Check(keepalive.ShutdownWithinAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult().Success,"T064 session shutdown released");}
        return new{scope="合成 PCM／合成區塊，實際產品 host→AudioApiTransport→DirectShow；T064 零PCM保活；非正式世界驗收",stages,events=h.Observations};
    }
    static object GetLifetime(Effect6016Host h,string key)=>((IReadOnlyList<BlockSnapshot>)typeof(Effect6016Host).GetField("committedBlocks",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(h)!).Single(b=>b.BlockKey==key).Lifetime;
    static int Main(string[] args)
    {
        if(args.Length<2){Console.Error.WriteLine("Effect6016Checks <scratch-directory> <result.json> [--native] [--assets-root path]");return 2;}
        try{Directory.CreateDirectory(args[0]);Managed();int mapArg=Array.IndexOf(args,"--assets-root");object? map=mapArg>=0?ReadonlyMap(args[mapArg+1]):null;object? native=args.Contains("--native")?Native(args[0]):null;
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{status="pass",checks,cases,map,native,productSha256=Sha(File.ReadAllBytes(typeof(WorldSession).Assembly.Location)),audioSha256=Sha(File.ReadAllBytes(typeof(AudioPlayer).Assembly.Location)),gameStarted=false,network=false,database=false},JsonOptions));Console.WriteLine($"PASS 6016: {checks} checks");return 0;
        }catch(Exception ex){File.WriteAllText(args[1],JsonSerializer.Serialize(new{status="failed",checks,cases,error=ex.ToString()},JsonOptions));Console.Error.WriteLine(ex);return 1;}
    }
    sealed class Clock {public long Now;}
    sealed class MemoryAudio:IEffectAudio
    {
        public bool Delay;public List<(int id,EffectOwner owner)> Plays=[];public List<EffectOwner> Retired=[];readonly List<TaskCompletionSource<EffectAudioReply>> pending=[];
        static EffectAudioReply Ok=>new(true,"MemoryOnly");
        public void Complete(){foreach(var p in pending)p.TrySetResult(Ok);pending.Clear();}
        public Task<EffectAudioReply> RegisterAsync(EffectOwner o){if(!Delay)return Task.FromResult(Ok);var p=new TaskCompletionSource<EffectAudioReply>();pending.Add(p);return p.Task;}
        public Task<EffectAudioReply> RetireAsync(EffectOwner o){Retired.Add(o);return Task.FromResult(Ok);}
        public Task<EffectAudioReply> PlayAsync(int id,EffectOwner o){Plays.Add((id,o));return Task.FromResult(Ok);}
        public Task<EffectAudioReply> ConfigureAsync(double gain,bool muted,bool enabled)=>Task.FromResult(Ok);
        public Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan budget)=>Task.FromResult(Ok);
        public Task WhenReleased=>Task.CompletedTask;public EffectAudioReply LastMaintenance=>Ok;
    }
}
