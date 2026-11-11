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
    // R10-I（T393／#908）：原生選項窗的音量捲軸換算。R11-F（T420／#962）起三條捲軸各自對到一類音量（見 BarCategory），不再共用主音量。
    // 值以 0.01 為一格（與 WinForms 主音量 TrackBar 0–100 相同）；捲軸本身的範圍（LScrollBar）沒追，推定為線性。
    /// <summary>拇指位移 position（0..travel）→ 音量 0–1，四捨五入到 0.01；travel ≤ 0 時為 0。</summary>
    public static double SliderGain(int position,int travel)=>travel<=0?0:Math.Round(Math.Clamp(position,0,travel)/(double)travel,2,MidpointRounding.AwayFromZero);
    /// <summary>主音量 → 拇指位移（0..travel）。</summary>
    public static int SliderPosition(double gain,int travel)=>travel<=0||!double.IsFinite(gain)?0:(int)Math.Round(Math.Clamp(gain,0,1)*travel,MidpointRounding.AwayFromZero);
    /// <summary>
    /// R11-F（T420／#962，gap-matrix 15.13.5／15.13.6，#952 裁定第 1 點「照原版，三條分開」）：OptionUI 音量捲軸 → lineage.cfg 類別音量
    /// （sound.md 4.1）。Music＝MusicVolume（類別 3，音樂串流）、Effect＝SoundVolume（類別 0，影格／單次音效、UI）、
    /// Env＝EnvSoundVolume（類別 1，envsound.def、`^`、模式 1 封包）。對應依控件名與設定鍵名（推定：sub_919F30 的寫入目標沒在 IDA 追）。
    /// Voice（類別 2）不接（3.81 沒有語音聊天）。主音量（WinForms／網頁面板）與靜音照舊是另一層，最終音量＝類別音量 × 主音量。
    /// </summary>
    public static SoundCategory? BarCategory(string bar)=>bar switch{
        NativeUi.OptionPanel.MusicVolumeBar=>SoundCategory.Music,
        NativeUi.OptionPanel.EffectVolumeBar=>SoundCategory.Sound,
        NativeUi.OptionPanel.EnvVolumeBar=>SoundCategory.EnvSound,
        _=>null};
    /// <summary>捲軸目前值＝對應類別音量；不是這三條回 null。</summary>
    public static double? BarLevel(SoundVolumes volumes,string bar)=>BarCategory(bar) is {} c?volumes[c]:null;
    /// <summary>
    /// 捲軸設定對應類別音量（只改這一類，其他兩類不動）。不是這三條或值不在 0..1 → false、不改。
    /// 類別音量是設定值（原版存 lineage.cfg），與聲音裝置是否開著無關，所以聲音關閉時也照樣記下；已在播的聲音在下一次更新
    /// （BackgroundMusic.Update、MapEnvSound.Update、MapAmbientSound 的 SetScale、戰鬥音效 ApplyLevels）就換成新音量。
    /// </summary>
    public static bool SetBarLevel(SoundVolumes volumes,string bar,double value){
        if(BarCategory(bar) is not {} c||!double.IsFinite(value)||value<0||value>1)return false;
        volumes[c]=value;return volumes[c]==value;
    }
    public static string Notice(PlayerAudioState state)=>state.applied?(state.muted?"已套用靜音":"音量已套用"):state.status switch{"pending"=>"正在套用聲音設定…","failed"=>"聲音設定未套用","closed"=>"聲音設定暫不可用",_=>"聲音尚未就緒"};
}
