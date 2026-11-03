using System.Text.Json;
using TrackBar=P06.FakeTrackBar;using CheckBox=P06.FakeCheckBox;using Label=P06.FakeLabel;
namespace P06;
public sealed class AudioClientExcerpt {
    readonly PlayerAudioSettings audioSettings=new();
    double masterGain=>audioSettings.Gain;bool masterMute=>audioSettings.Muted;
    TrackBar? audioGainControl;CheckBox? audioMuteControl;Label? audioGainLabel;
    PlayerAudioState AudioSettingsWeb()=>audioSettings.Snapshot(login.World.Effects6016,webClosing||Disposing||IsDisposed);
    void SetAudioSettings(double gain,bool muted){
        if(!audioSettings.Change(gain,muted,AudioSettingsWeb().editable))return;
        Apply6016Controls();SyncAudioViews();PublishWebState();
    }
    void SyncAudioViews(){
        audioSettings.Render(login.World.Effects6016,webClosing||Disposing||IsDisposed,state=>{
            if(audioGainControl!=null){audioGainControl.Value=(int)Math.Round(state.gain*100);audioGainControl.Enabled=state.editable;}
            if(audioMuteControl!=null){audioMuteControl.Checked=state.muted;audioMuteControl.Enabled=state.editable;}
            if(audioGainLabel!=null)audioGainLabel.Text=$"主音量 {state.gain*100:0.##}%";
            if(audioNotice!=null)audioNotice.Text=PlayerAudioSettings.Notice(state);
        });
    }
    void DispatchAudioSettings(AudioSettingsCommand command){
        // Uses the existing authenticated local-page dispatch boundary; never creates/revives a host.
        if(!AudioSettingsWeb().editable){Reply(command.Id,"聲音設定暫不可用",AudioSettingsWeb());PublishWebState();return;}
        LeaveGameFocus("sound-settings");SetAudioSettings(command.Gain,command.Muted);
        var state=AudioSettingsWeb();Reply(command.Id,state.applied?PlayerAudioSettings.Notice(state):"設定已收到，尚未套用",state);PublishWebState();
    }

    readonly FakeLogin login=new();public bool webClosing,Disposing,IsDisposed;
    Label? audioNotice=new();public bool GameFocus=true;public int Held=2,Published;
    public List<(int id,string text)> Replies=[];public List<PlayerAudioState> States=[];
    public AudioClientExcerpt(Effect6016Host host){login.World.Effects6016=host;audioGainControl=new();audioMuteControl=new();audioGainLabel=new();audioGainControl.Changed=()=>SetAudioSettings(audioGainControl.Value/100d,masterMute);audioMuteControl.Changed=()=>SetAudioSettings(masterGain,audioMuteControl.Checked);SyncAudioViews();}
    bool TerminalAudioState=>false;
    void Apply6016Controls()=>login.World.Effects6016?.SetControls(masterGain,masterMute,login.World.Renderable&&!TerminalAudioState&&!webClosing);
    public List<(int id,PlayerAudioState state)> AudioReplies=[];
    void Reply(int id,string status,PlayerAudioState? audio=null){Replies.Add((id,status));if(audio!=null)AudioReplies.Add((id,audio));}
    void PublishWebState(){Published++;States.Add(AudioSettingsWeb());}
    void LeaveGameFocus(string why){GameFocus=false;Held=0;}
    public void Web(string json){using var doc=JsonDocument.Parse(json);DispatchAudioSettings(AudioSettingsCommand.Parse(doc.RootElement));}
    public void NativeGain(int gain)=>audioGainControl!.Value=gain;
    public void NativeMute(bool mute)=>audioMuteControl!.Checked=mute;
    public void Render()=>SyncAudioViews();
    public void Attach(Effect6016Host host)=>login.World.Effects6016=host;
    public PlayerAudioState State=>AudioSettingsWeb();
    public (int gain,bool muted,string notice) View=>(audioGainControl!.Value,audioMuteControl!.Checked,audioNotice!.Text);
}

public sealed class FakeTrackBar {int value;public bool Enabled;public Action? Changed;public int Value{get=>value;set{if(this.value==value)return;this.value=value;Changed?.Invoke();}}}
public sealed class FakeCheckBox {bool value;public bool Enabled;public Action? Changed;public bool Checked{get=>value;set{if(this.value==value)return;this.value=value;Changed?.Invoke();}}}
public sealed class FakeLabel {public string Text="";}
public sealed class FakeLogin {public FakeWorld World=new();}
public sealed class FakeWorld {public Effect6016Host? Effects6016;public bool Renderable=true;}
