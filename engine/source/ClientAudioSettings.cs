namespace P06;
public sealed partial class ClientForm {
    readonly PlayerAudioSettings audioSettings=new();
    double masterGain=>audioSettings.Gain;bool masterMute=>audioSettings.Muted;
    TrackBar? audioGainControl;CheckBox? audioMuteControl;Label? audioGainLabel;
    PlayerAudioState AudioSettingsWeb()=>audioSettings.Snapshot(login.World.Effects6016,webClosing||Disposing||IsDisposed);
    void SetAudioSettings(double gain,bool muted){
        if(!audioSettings.Change(gain,muted,AudioSettingsWeb().editable))return;
        Apply6016Controls();PumpCombatSfx();SyncAudioViews();PublishWebState();
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
}
