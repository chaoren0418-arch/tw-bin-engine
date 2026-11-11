using System.Text.Json;
namespace P06;
public sealed record AudioSettingsCommand(int Id,double Gain,bool Muted) {
    public static AudioSettingsCommand Parse(JsonElement value){
        if(value.ValueKind!=JsonValueKind.Object)throw new FormatException("audio object");
        var names=value.EnumerateObject().Select(p=>p.Name).ToArray();
        if(!names.Order().SequenceEqual(new[]{"cmd","gain","id","muted","v"}))throw new FormatException("audio fields");
        var v=value.GetProperty("v");var id=value.GetProperty("id");var cmd=value.GetProperty("cmd");var gain=value.GetProperty("gain");var muted=value.GetProperty("muted");
        if(v.ValueKind!=JsonValueKind.Number||!v.TryGetInt32(out int version)||version!=1||id.ValueKind!=JsonValueKind.Number||!id.TryGetInt32(out int request)||request<1||cmd.ValueKind!=JsonValueKind.String||cmd.GetString()!="audioSettings"||gain.ValueKind!=JsonValueKind.Number||!gain.TryGetDouble(out double level)||!double.IsFinite(level)||level<0||level>1||muted.ValueKind is not (JsonValueKind.True or JsonValueKind.False))throw new FormatException("audio values");
        return new(request,level,muted.GetBoolean());
    }
}
public sealed record PlayerAudioState(double gain,bool muted,bool applied,string status,bool editable,int revision,int configurationRevision);
public sealed class PlayerAudioSettings {
    public double Gain {get;private set;}=.5;public bool Muted {get;private set;}
    public int Revision {get;private set;}public bool Rendering {get;private set;}
    public bool Change(double gain,bool muted,bool editable){
        if(!double.IsFinite(gain)||gain<0||gain>1)throw new ArgumentOutOfRangeException(nameof(gain));
        if(Rendering||!editable||Gain==gain&&Muted==muted)return false;
        Gain=gain;Muted=muted;Revision++;return true;
    }
    public PlayerAudioState Snapshot(Effect6016Host? host,bool closing){
        bool editable=!closing&&host is {Closed:false};
        bool applied=editable&&host!.ControlsApplied&&host.Gain==Gain&&host.Muted==Muted;
        string status=!editable?"closed":applied?"applied":host!.ControlsStatus;
        return new(Gain,Muted,applied,status,editable,Revision,host?.ControlsStateRevision??0);
    }
    public void Render(Effect6016Host? host,bool closing,Action<PlayerAudioState> render){
        bool prior=Rendering;Rendering=true;try{render(Snapshot(host,closing));}finally{Rendering=prior;}
    }
    // R10-I（T393／#908）：原生選項窗的音量捲軸（OptionUI Music／Effect／Env）都對到這一個主音量。
    // 值以 0.01 為一格（與 WinForms 主音量 TrackBar 0–100 相同）；捲軸本身的範圍（LScrollBar）沒追，推測為線性。
    /// <summary>拇指位移 position（0..travel）→ 主音量 0–1，四捨五入到 0.01；travel ≤ 0 時為 0。</summary>
    public static double SliderGain(int position,int travel)=>travel<=0?0:Math.Round(Math.Clamp(position,0,travel)/(double)travel,2,MidpointRounding.AwayFromZero);
    /// <summary>主音量 → 拇指位移（0..travel）。</summary>
    public static int SliderPosition(double gain,int travel)=>travel<=0||!double.IsFinite(gain)?0:(int)Math.Round(Math.Clamp(gain,0,1)*travel,MidpointRounding.AwayFromZero);
    public static string Notice(PlayerAudioState state)=>state.applied?(state.muted?"已套用靜音":"音量已套用"):state.status switch{"pending"=>"正在套用聲音設定…","failed"=>"聲音設定未套用","closed"=>"聲音設定暫不可用",_=>"聲音尚未就緒"};
}
