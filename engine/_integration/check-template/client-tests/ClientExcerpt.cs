using P24.MapEffectEvents;
namespace P06;
// Verbatim production method bodies; only surrounding UI/world/diagnostic objects are managed fakes.
public sealed class ClientExcerpt {
    public static Func<string,IEffectAudio?> Accepted6016TransportFactory {get;set;}=_=>null;
    const string Effect6016Pin="825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383";
    readonly EffectHostLane effectLane=new();
    readonly List<Effect6016Host> retiredAudioHosts=[];
    readonly Dictionary<Effect6016Host,EffectAudioReply> audioMaintenanceSnapshots=[];
    double masterGain=.5;bool masterMute=false;
    Effect6016Definition? effectDefinition;
    void SyncAudioViews(){} // UI-only rendering excluded from existing F1/F2 harness.
    readonly FakeLogin login=new();readonly FakeViewWorld World=new();
    bool webClosing;public bool Disposing {get;set;}public bool IsDisposed {get;set;}
    bool TerminalAudioState=>login.Terminal;
    readonly System.Collections.Concurrent.ConcurrentQueue<EffectAudioObservation> recorded=new();
    void Record6016(EffectAudioObservation item)=>recorded.Enqueue(item);
    public ClientExcerpt(Effect6016Definition definition){effectDefinition=definition;}
    public Effect6016Host Host=>login.World.Effects6016!;
    public FakeWorld State=>login.World;
    public void Ensure()=>Ensure6016();
    public void Pump()=>Pump6016();
    public Task<EffectAudioReply> Logout(){login.Terminal=true;return Shutdown6016();}
    public void Reconnect(){login.Terminal=false;Ensure6016();}
    public Task<EffectAudioReply> BeginClose(){webClosing=true;return Shutdown6016();}
    void Ensure6016(){
        if(webClosing||Disposing||IsDisposed)return;
        if(login.World.Effects6016 is {Closed:false})return;
        try{
            effectDefinition??=Effect6016Definition.FromR27Metadata(File.ReadAllBytes(Path.Combine(World.Assets.Root,"maps/effects/6016.json")),Effect6016Pin);
            IEffectAudio? transport=null;try{transport=Accepted6016TransportFactory?.Invoke(World.Assets.Root);}catch(Exception ex){Record6016(new("create",new(false,"Unavailable",ex.ToString())));}
            var host=new Effect6016Host(new(effectDefinition),transport,effectLane,()=>login.World.SceneClock(),observer:Record6016);
            login.World.Attach6016(host);host.SetControls(masterGain,masterMute,false);
        }catch(Exception ex){Record6016(new("metadata",new(false,"Unavailable",ex.ToString())));}
    }

    void Apply6016Controls()=>login.World.Effects6016?.SetControls(masterGain,masterMute,login.World.Renderable&&!TerminalAudioState&&!webClosing);

    void Pump6016(){
        Apply6016Controls(); // Publish current world/control gate before any registration completion can dispatch.
        effectLane.Drain();
        if(login.World.Effects6016 is {} host){
            host.Pump();
            SyncAudioViews();
        }
        foreach(var tracked in retiredAudioHosts.Concat(login.World.Effects6016 is {} current?new[]{current}:Array.Empty<Effect6016Host>()).Distinct()){
            if(tracked.LastMaintenance is {} maintenance&&(!audioMaintenanceSnapshots.TryGetValue(tracked,out var prior)||!Equals(maintenance,prior))){audioMaintenanceSnapshots[tracked]=maintenance;Record6016(new("maintenance",maintenance));}
        }
        foreach(var retired in retiredAudioHosts.Where(host=>host.WhenReleased.IsCompletedSuccessfully).ToArray()){
            Record6016(new("released",new(true,"Released")));retiredAudioHosts.Remove(retired);audioMaintenanceSnapshots.Remove(retired);
        }
    }

    Task<EffectAudioReply> Shutdown6016(){
        if(login.World.Effects6016 is not {} host)return Task.FromResult(new EffectAudioReply(true,"NoHost"));
        if(!host.Closed)retiredAudioHosts.Add(host);
        return host.ShutdownAsync(TimeSpan.FromMilliseconds(750));
    }
}
public sealed class FakeLabel {public string Text="";}
public sealed class FakeLogin {public FakeWorld World=new();public bool Terminal=false;}
public sealed class FakeWorld {
    public Effect6016Host? Effects6016;public bool Renderable=true;public long Now;
    public long SceneClock()=>Now;
    public void Attach6016(Effect6016Host host){Effects6016?.Clear(SceneClock());Effects6016=host;}
}
public sealed class FakeViewWorld {public FakeAssets Assets=new();}
public sealed class FakeAssets {public string Root="unused-explicit-fake";}
