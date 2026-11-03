from pathlib import Path
import hashlib,json
R=Path(__file__).resolve().parent;T=R/'audio-tests';T.mkdir(exist_ok=True)
p=R/'source/ClientAudioSettings.cs';source=p.read_text('utf-8');text=source.replace('namespace P06;','using System.Text.Json;\nusing TrackBar=P06.FakeTrackBar;using CheckBox=P06.FakeCheckBox;using Label=P06.FakeLabel;\nnamespace P06;').replace('public sealed partial class ClientForm {','public sealed class AudioClientExcerpt {')
extra='''
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
'''
text=text[:text.rfind('}')]+extra+'}\n'+'''
public sealed class FakeTrackBar {int value;public bool Enabled;public Action? Changed;public int Value{get=>value;set{if(this.value==value)return;this.value=value;Changed?.Invoke();}}}
public sealed class FakeCheckBox {bool value;public bool Enabled;public Action? Changed;public bool Checked{get=>value;set{if(this.value==value)return;this.value=value;Changed?.Invoke();}}}
public sealed class FakeLabel {public string Text="";}
public sealed class FakeLogin {public FakeWorld World=new();}
public sealed class FakeWorld {public Effect6016Host? Effects6016;public bool Renderable=true;}
'''
(T/'AudioClientExcerpt.cs').write_text(text,'utf-8')
(R/'evidence/audio-excerpt-pins.json').write_text(json.dumps(dict(source='source/ClientAudioSettings.cs',sourceSha256=hashlib.sha256(p.read_bytes()).hexdigest(),excerptSha256=hashlib.sha256((T/'AudioClientExcerpt.cs').read_bytes()).hexdigest(),scope='Verbatim client method bodies with type aliases for fake controls and surrounding world/focus/bridge; no Form construction.'),indent=2),'utf-8')
