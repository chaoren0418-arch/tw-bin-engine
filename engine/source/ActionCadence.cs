using System.Security.Cryptography;
namespace P06;

// Server action window, original frame schedule, skill reuse and local margin are distinct.
// Speed0/2 retain r30 behavior. Speed1/Brave0 rounds each native frame separately.
public sealed record ActionCadence(int Gfx,int Weapon,int Action,int ServerBaseMs,int NativeBaseMs,int LegacyAnimationMs,int? ReleaseBaseMs,int ReuseMs,int MarginMs,bool Slow) {
    public bool Haste {get;init;}
    public int HasteAnimationMs {get;init;}
    public int? HasteReleaseMs {get;init;}
    public static int HasteFrameMs(int ms)=>ms>=0?checked(3*ms)>>2:throw new ArgumentOutOfRangeException(nameof(ms));
    public int AnimationMs=>Slow?checked(NativeBaseMs*2):Haste?HasteAnimationMs:LegacyAnimationMs;
    public int? ReleaseMs=>Slow?(ReleaseBaseMs is int ms?checked(ms*2):null):Haste?HasteReleaseMs:ReleaseBaseMs;
    public int ServerIntervalMs=>Slow?checked(ServerBaseMs*2):Haste?Math.Max(1,(int)(ServerBaseMs*0.755)):ServerBaseMs;
    public int WaitMs=>Math.Max(Math.Max(ServerIntervalMs,ReuseMs),Slow||Haste?AnimationMs:0)+MarginMs;
    public int SlowWaitMs=>(this with{Slow=true}).WaitMs;
    public int NormalWaitMs=>(this with{Slow=false,Haste=false}).WaitMs;
    public bool Matches(int gfx,int weapon)=>Gfx==gfx&&Weapon==weapon;
    public static string Name(int action)=>action switch{1=>"attack",18=>"spellDir",19=>"spellNoDir",_=>throw new ArgumentOutOfRangeException(nameof(action))};
    sealed record Metadata(int Native,int Legacy,int? Release,int HasteNative,int? HasteRelease);
    static readonly Dictionary<(int Gfx,int Weapon,int Action),Metadata> metadata=[];
    static readonly object gate=new();
    public static ActionCadence Capture(int gfx,int weapon,int action,byte speed,byte brave,int reuse=0,int margin=0){
        if(!NormalSpeed.Supported(speed,brave))throw new NotSupportedException("Unsupported action speed");
        var profile=PlayerProfiles.Get(gfx,weapon)??throw new NotSupportedException("Unsupported action appearance");
        var key=(gfx,weapon,action);Metadata data;
        lock(gate){
            if(!metadata.TryGetValue(key,out data!)){
                string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets",profile.Path));
                var bytes=File.ReadAllBytes(path);if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(profile.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Action profile integrity");
                var json=System.Text.Json.JsonDocument.Parse(bytes).RootElement;string name=Name(action);
                var steps=json.GetProperty("sequences").EnumerateArray().Single(s=>s.S("action")==name&&s.I("direction")==0).GetProperty("sequence");
                int native=0,hasteNative=0;int? release=null,hasteRelease=null;
                foreach(var step in steps.EnumerateArray()){
                    if(release==null&&step.TryGetProperty("events",out var events)&&events.GetString()!.Contains('!')){release=native;hasteRelease=hasteNative;}
                    int frameMs=step.TryGetProperty("milliseconds",out var ms)?ms.GetInt32():step.I("ticks")*1000/24;
                    native+=frameMs;hasteNative+=HasteFrameMs(frameMs);
                }
                int legacy=json.GetProperty("actionMs").GetProperty(name).GetInt32();
                if(native<=0||legacy<=0)throw new InvalidDataException("Empty action timing");
                data=new(native,legacy,release,hasteNative,hasteRelease);metadata.Add(key,data);
            }
        }
        return new(gfx,weapon,action,profile.ServerActionMs[Name(action)],data.Native,data.Legacy,data.Release,reuse,margin,speed==2){Haste=speed==1,HasteAnimationMs=data.HasteNative,HasteReleaseMs=data.HasteRelease};
    }
    public Frame Frame(ObjectAppearance appearance,int direction,double elapsed){
        string name=Name(Action);if(!Slow&&!Haste)return appearance.At(name,direction,elapsed);
        if(Haste&&!Slow){
            double fastAge=Math.Clamp(elapsed,0,HasteAnimationMs-.00001);var fastSequence=appearance.Sequences[(name,direction)];
            foreach(var step in fastSequence){int ms=HasteFrameMs(step.Milliseconds??step.Ticks*1000/24);if(fastAge<ms)return appearance.Frames[step.Key];fastAge-=ms;}
            return appearance.Frames[fastSequence[^1].Key];
        }
        // Some legacy profiles omit milliseconds: use the original per-frame integer rule.
        double age=Math.Clamp(elapsed/2,0,NativeBaseMs-.00001);var sequence=appearance.Sequences[(name,direction)];
        foreach(var step in sequence){int ms=step.Milliseconds??step.Ticks*1000/24;if(age<ms)return appearance.Frames[step.Key];age-=ms;}
        return appearance.Frames[sequence[^1].Key];
    }
}

public sealed partial class WorldSession {
    internal Func<long> ActionClock=()=>Environment.TickCount64;
    sealed record PendingAction(CombatAnimation Animation,int Epoch,int Appearance,int Correction,uint Target);
    PendingAction? pendingAction;
    internal CombatAnimation? ActionFor(uint id)=>Actor?.ObjectId==id?OwnCombat:Objects.FirstOrDefault(o=>o.ObjectId==id)?.Combat;
    internal void PredictAction(int action,int heading,uint target,ActionCadence timing){
        if(Actor is not {} actor)return;
        CancelPickupPresentation("predicted-action");Actor=actor with{Direction=(byte)heading};var animation=new CombatAnimation(action,ActionClock(),true){Timing=timing};OwnCombat=animation;
        pendingAction=new(animation,WorldEpoch,AppearanceRevision,Corrections,target);
    }
    CombatAnimation CaptureCombat(uint id,int action,bool predicted,uint? target){
        if(Actor is {} own&&own.ObjectId==id&&action is 1 or 18 or 19&&pendingAction is {} pending){
            pendingAction=null;
            if(!OwnDead&&pending.Epoch==WorldEpoch&&pending.Appearance==AppearanceRevision&&pending.Correction==Corrections&&pending.Animation.Action==action&&(target==null||target==pending.Target)&&pending.Animation.Timing?.Matches(own.Gfx,own.Weapon)==true)
                return pending.Animation with{Predicted=false};
        }
        ActionCadence? timing=null;
        if(action is 1 or 18 or 19){
            var other=Objects.FirstOrDefault(o=>o.ObjectId==id);
            int gfx=Actor?.ObjectId==id?Actor.Gfx:other?.Gfx??-1,weapon=Actor?.ObjectId==id?Actor.Weapon:other?.Action??-1;
            byte speed=Actor?.ObjectId==id?Actor.Speed:other?.Speed??255,brave=Actor?.ObjectId==id?Actor.Brave:other?.Brave??255;
            if(NormalSpeed.Supported(speed,brave)&&PlayerProfiles.Get(gfx,weapon)!=null)timing=ActionCadence.Capture(gfx,weapon,action,speed,brave,margin:action==1?CombatRules.ClockMargin(weapon):100);
        }
        return new(action,ActionClock(),predicted){Timing=timing};
    }
}

public sealed partial class LoginConnection {
    internal Func<long> ActionClock=()=>Environment.TickCount64;
    ActionCadence? lastCombatTiming,lastMagicTiming;long lastCombatAdmission=-1,lastMagicAdmission=-1;int combatTimingEpoch,magicTimingEpoch;
    void ExtendActionCooldown(){
        if(World.Actor is not {Brave:0} actor||actor.Speed is not (0 or 2))return;
        if(lastCombatTiming is {} combat&&lastCombatAdmission>=0&&combatTimingEpoch==World.WorldEpoch)nextCombatAt=Math.Max(nextCombatAt,lastCombatAdmission+(actor.Speed==2?combat.SlowWaitMs:combat.NormalWaitMs));
        if(lastMagicTiming is {} magic&&lastMagicAdmission>=0&&magicTimingEpoch==World.WorldEpoch)nextMagicAt=Math.Max(nextMagicAt,lastMagicAdmission+(actor.Speed==2?magic.SlowWaitMs:magic.NormalWaitMs));
    }
    bool ActionAdmissionReady=>!movementSending&&!itemSending&&!groundSending&&!GroundBusy&&!ItemBusy&&MovementClock()>=MovementReadyAt&&ActionClock()>=nextCombatAt&&ActionClock()>=nextMagicAt;
}
