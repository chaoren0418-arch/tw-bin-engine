using P24.MapEffectEvents;
using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    // Default factory uses supervisor-accepted r3; injectable for managed host tests.
    public static Func<string,IEffectAudio?> Accepted6016TransportFactory {get;set;}=P24.HostAudio.AudioApiTransport.Create;
    const string Effect6016Pin="825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383";
    readonly EffectHostLane effectLane=new();readonly object audioLogGate=new();
    readonly List<Effect6016Host> retiredAudioHosts=[];
    readonly Dictionary<Effect6016Host,EffectAudioReply> audioMaintenanceSnapshots=[];
    Label? audioNotice;Effect6016Definition? effectDefinition;
    bool TerminalAudioState=>login.State is LoginState.Offline or LoginState.Cancelled or LoginState.Disconnected or LoginState.Rejected or LoginState.Faulted;
    void Record6016(EffectAudioObservation item){
        lock(audioLogGate){try{string directory=Path.Combine(WebEvidence,"audio");Directory.CreateDirectory(directory);File.AppendAllText(Path.Combine(directory,"host.jsonl"),JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,item})+Environment.NewLine);}catch(Exception ex){System.Diagnostics.Trace.WriteLine("Audio diagnostics: "+ex.Message);}}
    }
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
    void Init6016(){
        Ensure6016();
        FormClosed+=(_,_)=>{_=Shutdown6016();};
        login.Changed+=()=>{if(TerminalAudioState)_=Shutdown6016();else Ensure6016();};
        if(normalPanels==null)return;
        var page=new TabPage("聲音"){BackColor=Color.FromArgb(23,38,41),ForeColor=Color.White};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new(16),WrapContents=false};
        var label=new Label{Text="主音量 50%",AutoSize=true};
        var slider=new TrackBar{Minimum=0,Maximum=100,Value=50,TickFrequency=10,Width=280,AccessibleName="主音量"};
        var mute=new CheckBox{Text="靜音",AutoSize=true,AccessibleName="靜音"};
        audioGainControl=slider;audioMuteControl=mute;audioGainLabel=label;
        audioNotice=new Label{Text="聲音尚未就緒",AutoSize=true};
        slider.ValueChanged+=(_,_)=>SetAudioSettings(slider.Value/100d,masterMute);
        mute.CheckedChanged+=(_,_)=>SetAudioSettings(masterGain,mute.Checked);
        foreach(Control control in new Control[]{slider,mute})control.Enter+=(_,_)=>LeaveGameFocus("sound-controls");
        panel.Controls.AddRange([label,slider,mute,audioNotice]);page.Controls.Add(panel);normalPanels.TabPages.Add(page);
        SyncAudioViews();
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
