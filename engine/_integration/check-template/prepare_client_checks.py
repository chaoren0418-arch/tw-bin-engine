from pathlib import Path
import json,hashlib
R=Path(__file__).resolve().parent;T=R/'client-tests';T.mkdir(exist_ok=True)
p=R/'source/ClientEffect6016.cs';text=p.read_text('utf-8-sig')
def method(start,end):return text[text.index(start):text.index(end)]
parts=[method('    void Ensure6016(){','    void Init6016(){'),method('    void Apply6016Controls()=>','    void Pump6016(){'),method('    void Pump6016(){','    Task<EffectAudioReply> Shutdown6016(){'),text[text.index('    Task<EffectAudioReply> Shutdown6016(){'):text.rfind('}')]]
prefix='''using P24.MapEffectEvents;
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
}
'''
prefix=prefix[:prefix.rfind('}')]
suffix='''}
public sealed class FakeLabel {public string Text="";}
public sealed class FakeLogin {public FakeWorld World=new();public bool Terminal=false;}
public sealed class FakeWorld {
    public Effect6016Host? Effects6016;public bool Renderable=true;public long Now;
    public long SceneClock()=>Now;
    public void Attach6016(Effect6016Host host){Effects6016?.Clear(SceneClock());Effects6016=host;}
}
public sealed class FakeViewWorld {public FakeAssets Assets=new();}
public sealed class FakeAssets {public string Root="unused-explicit-fake";}
'''
(T/'ClientExcerpt.cs').write_text(prefix+'\n'.join(parts)+suffix,'utf-8')
(R/'evidence/client-excerpt-pins.json').write_text(json.dumps(dict(source='source/ClientEffect6016.cs',sha256=hashlib.sha256(p.read_bytes()).hexdigest(),excerptSha256=hashlib.sha256((T/'ClientExcerpt.cs').read_bytes()).hexdigest(),methods=['Ensure6016','Apply6016Controls','Pump6016','Shutdown6016'],actualHostAssembly='build/host/P11.ClientEngine.dll',boundary='Exact production method bodies; fake world/UI wrappers; no Form instantiated.'),indent=2),'utf-8')
