namespace P06;
public static class NormalSpeed {
    public static bool Supported(byte speed,byte brave)=>speed is 0 or 1 or 2&&brave==0;
    // Original BIN e12949cf, base0x400000, VA0xA407D0: Speed1 => (3*t)>>2; Speed2/Brave0 => 2*t.
    public static int AnimationMs(int basis,byte speed,byte brave=0){if(basis<=0||!Supported(speed,brave))throw new ArgumentOutOfRangeException(nameof(speed));return speed switch{0=>basis,2=>checked(2*basis),_=>Math.Max(1,(3*basis)>>2)};}
    // Fixed JAR d287 ActionIntervalCalculator uses 0.755 for haste and 2.0 for slow (native haste is 0.75).
    public static int SendIntervalMs(int basis,byte speed,byte brave=0)=>Math.Max(AnimationMs(basis,speed,brave),speed==1?Math.Max(1,(int)(basis*0.755)):basis);
}
public sealed partial class WorldSession {
    internal Func<long> SpeedClock=()=>Environment.TickCount64;
    long? speedDisplayUntil;
    internal event Action? SpeedChanged;
    public int SpeedRevision{get;private set;}
    public int? MovementAnimationMs=>Actor is {} a&&AppearanceProfile is {} p&&NormalSpeed.Supported(a.Speed,a.Brave)?NormalSpeed.AnimationMs(p.StepMs,a.Speed,a.Brave):null;
    public int? MovementIntervalMs=>Actor is {} a&&AppearanceProfile is {} p&&NormalSpeed.Supported(a.Speed,a.Brave)?NormalSpeed.SendIntervalMs(p.StepMs,a.Speed,a.Brave):null;
    public string SpeedNotice {
        get {
            if(Actor is not {} a||a.Speed==0&&a.Brave==0)return "";
            if(!NormalSpeed.Supported(a.Speed,a.Brave))return "目前速度狀態尚未支援步行。";
            string label=a.Speed==2?"緩速":"加速";long now=SpeedClock();
            return speedDisplayUntil is long end?(end>now?$"{label}中，約 {(end-now+999)/1000} 秒；以伺服器狀態為準。":$"{label}時間已到，等待伺服器更新狀態。"):$"{label}中；剩餘時間等待伺服器回報。";
        }
    }
    void ResetSpeedStatus(){speedDisplayUntil=null;SpeedRevision++;}
    void ReceiveSpeed(byte opcode,uint id,byte mode,ushort seconds){
        if(Actor is {} actor&&id==actor.ObjectId){
            SpeedRevision++;Actor=opcode==255?actor with{Speed=mode}:actor with{Brave=mode};
            if(opcode==255)speedDisplayUntil=mode is 1 or 2&&seconds>0?SpeedClock()+seconds*1000L:null;
            SpeedChanged?.Invoke();
            MotionNotice=NormalSpeed.Supported(Actor.Speed,Actor.Brave)?mode==0?"伺服器已更新速度狀態。":"伺服器已確認速度狀態；後續步行採用目前節奏。":"此速度狀態尚未支援在線步行";
        }else UpdateObject(id,o=>{
            var updated=opcode==255?o with{Speed=mode}:o with{Brave=mode};
            // An unsupported old step has no known duration. On supported recovery,
            // discard that step and show the authoritative position without inventing timing.
            return NormalSpeed.Supported(updated.Speed,updated.Brave)&&!NormalSpeed.Supported(updated.MotionSpeed,updated.MotionBrave)
                ?updated with{MotionAt=0}:updated;
        });
    }
}
