using System.Buffers.Binary;
namespace P06;
public sealed record CombatAnimation(int Action,long At,bool Predicted){
    static long nextInstance;
    public long Instance {get;}=Interlocked.Increment(ref nextInstance);
    public ActionCadence? Timing{get;init;}
}
public sealed record AttackReport(long Number,uint Attacker,uint Target,int Action,ushort DamageWord,int Heading,ushort X,ushort Y,int Flags);
public sealed record ProjectileReport(long Number,uint Attacker,uint Target,int Gfx,int Heading,ushort SourceX,ushort SourceY,ushort TargetX,ushort TargetY,long At,ushort DamageWord=0,ushort CasterGfx=0){public ActionCadence? Timing{get;init;}public long? ActionAt{get;init;}}
public static class CombatRules {
    // Filled from the original spr_action row and SprTable.calcActionSpeed; verified by asset preparation.
    static readonly Lazy<int> attackMs=new(()=>Json.Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets/objects/0.json"))).GetProperty("actionMs").GetProperty("attack").GetInt32());
    public static int AttackMs=>attackMs.Value;
    // TickCount64 is quantized on Windows. A local 32ms input margin prevents a
    // 1200ms tick delta from admitting an actual wall interval below the original 1200ms.
    public static int ClockMargin(int weapon)=>weapon==20?32:0;
    static readonly Dictionary<int,int> armedMs=[];
    public static int Duration(int weapon){if(weapon==20)return 1200;if(weapon==0)return AttackMs;if(weapon is not (4 or 20 or 50))throw new InvalidDataException("unsupported weapon cadence");if(!armedMs.TryGetValue(weapon,out int ms)){ms=Json.Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,$"../../assets/objects/0-weapon-{weapon}.json"))).GetProperty("actionMs").GetProperty("attack").GetInt32();armedMs[weapon]=ms;}return ms;}
    public static int DurationFor(int gfx,int weapon)=>PlayerProfiles.Get(gfx,weapon)?.ServerActionMs["attack"]??throw new InvalidDataException("unsupported player attack cadence");
    public static byte[] Attack(WorldObject target,int weapon=0){byte[] b=new byte[16];b[0]=(byte)(weapon==20?123:229);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1),target.ObjectId);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(5),target.X);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(7),target.Y);return b;}
}
public sealed partial class WorldSession {
    public CombatAnimation? OwnCombat {get;private set;}
    public long AttackReports {get;private set;}
    public AttackReport? LastAttack {get;private set;}
    public AttackReport? LastIncomingAttack {get;private set;}
    public ProjectileReport? LastProjectile {get;private set;}
    readonly System.Collections.Concurrent.ConcurrentDictionary<long,ProjectileReport> projectileReports=new();
    public ProjectileReport[] Projectiles=>projectileReports.Values.OrderBy(p=>p.Number).ToArray();
    public string CombatNotice {get;internal set;}="選取可見目標後按 G：依原武器單次攻擊";
    public bool OwnDead {get;private set;}
    void ResetCombat(){ResetRecovery();pendingAction=null;OwnCombat=null;LastAttack=null;LastIncomingAttack=null;LastProjectile=null;projectileReports.Clear();AttackReports=0;OwnDead=false;CombatNotice="選取可見目標後按 G：依原武器單次攻擊";}
    internal void PredictAttack(int heading){if(Actor==null)return;CancelPickupPresentation("predicted-attack");Actor=Actor with{Direction=(byte)heading};OwnCombat=new(1,Environment.TickCount64,true);CombatNotice="單次攻擊已送出；動畫為預測，等待原服回報";}
    void AnimateCombat(uint id,int action,bool predicted=false,uint? target=null){
        var animation=CaptureCombat(id,action,predicted,target);
        if(Actor?.ObjectId==id){CancelPickupPresentation("combat-action");if(OwnDead&&action!=8)return;if(OwnDead&&OwnCombat?.Action==8)return;OwnCombat=animation;if(action==8){OwnDead=true;pendingAction=null;AppearanceRevision++;SelectTarget(null);CloseInteraction();}}
        else UpdateObject(id,o=>o.Dead?o:o with{Combat=animation,ReceivedAction=action,MotionAt=0,Dead=action==8,ShopActive=false});
        if(action==8&&TargetId==id)SelectTarget(null);
    }
    bool ReceiveCombat(byte opcode,LoginReader r,byte[] plain){
        if(opcode==30){
            if(plain.Length>=17&&BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(13))==338){
                if(plain.Length!=40)throw new ProtocolException("truncated arrow envelope");
                int arrowAction=r.C();uint arrowAttacker=r.D(),arrowTarget=r.D();ushort damage=r.H();int arrowHeading=r.C();uint marker=r.D();ushort gfx=r.H();byte flight=r.C();ushort sx=r.H(),sy=r.H(),tx=r.H(),ty=r.H();uint reserved=r.D();byte end=r.C();r.Finish();
                if(arrowHeading>7||flight!=127||reserved!=0||end!=0)throw new ProtocolException("invalid arrow envelope");
                if(gfx!=66||arrowAction!=1){Unhandled(opcode);CombatNotice="收到未支援投射物；保留原封包";return true;}
                LastAttack=new(++AttackReports,arrowAttacker,arrowTarget,arrowAction,damage,arrowHeading,tx,ty,flight);
                if(arrowTarget==Actor?.ObjectId)LastIncomingAttack=LastAttack;
                ushort casterGfx=Actor?.ObjectId==arrowAttacker?Actor.Gfx:Objects.FirstOrDefault(o=>o.ObjectId==arrowAttacker)?.Gfx??ushort.MaxValue;
                AnimateCombat(arrowAttacker,arrowAction,target:arrowTarget);var arrowAnimation=ActionFor(arrowAttacker);
                LastProjectile=new(AttackReports,arrowAttacker,arrowTarget,gfx,arrowHeading,sx,sy,tx,ty,ActionClock(),damage,casterGfx){Timing=arrowAnimation?.Timing,ActionAt=arrowAnimation?.At};
                projectileReports[LastProjectile.Number]=LastProjectile;
                foreach(var old in projectileReports.Values.Where(p=>LastProjectile.At-p.At>10000))projectileReports.TryRemove(old.Number,out _);
                if(Actor?.ObjectId==arrowAttacker)Actor=Actor with{Direction=(byte)arrowHeading};
                else UpdateObject(arrowAttacker,o=>o with{Direction=(byte)arrowHeading});
                // Actor timing was captured before constructing the projectile report.
                // Original BIN7C3AB0 -> 759B30 -> 758050 -> A49520 applies hurt at arrival.
                // The compositor schedules that visual; HP/death still follow original server packets immediately.
                CombatNotice=$"原服遠程回報 #{AttackReports}：箭矢 {gfx}，傷害欄 {damage}；箭數依原背包回包更新";
                return true;
            }
            if(plain.Length<24)throw new ProtocolException("truncated attack envelope");
            if(plain.Length!=24||plain[13]!=0||plain[14]!=0||plain[15]!=0||plain[16]!=0){Unhandled(opcode);CombatNotice="收到未支援的原服遠程／技能／其他攻擊變體；保留原封包";return true;}
            int action=r.C();uint attacker=r.D(),target=r.D();ushort word=r.H();int heading=r.C();ushort x=r.H(),y=r.H();int flags=r.C();r.Finish();
            if(heading>7)throw new ProtocolException("attack heading");
            LastAttack=new(++AttackReports,attacker,target,action,word,heading,x,y,flags);
            if(target==Actor?.ObjectId)LastIncomingAttack=LastAttack;
            if(Actor?.ObjectId==attacker)Actor=Actor with{Direction=(byte)heading};
            else if(objects.ContainsKey(attacker))UpdateObject(attacker,o=>o with{Direction=(byte)heading});
            AnimateCombat(attacker,action,target:target);
            if(word>0&&target!=0)AnimateCombat(target,2);
            CombatNotice=$"原服攻擊回報 #{AttackReports}：{attacker} → {target}，傷害欄 {word}（非 HP 扣除量）";
            return true;
        }
        if(opcode==237){uint id=r.D();byte percent=r.C();r.Finish();if(Actor?.ObjectId==id)Actor=Actor with{HpPercent=percent};else UpdateObject(id,o=>o with{HpByte=percent});return true;}
        return false;
    }
    public object CombatSnapshot()=>new{reports=AttackReports,lastAttack=LastAttack,lastProjectile=LastProjectile,projectiles=Projectiles,ownAnimation=OwnCombat,ownDead=OwnDead,restartRequested=RestartRequested,arrowCount=ArrowCount,notice=CombatNotice,damageMeaning="ClientLogin=2 original attack damage word, not independently established HP loss; zero does not distinguish miss from zero damage"};
}
public sealed partial class LoginConnection {
    long nextCombatAt;bool combatSending;int combatLease;
    public event Action<object>? CombatSent;
    public bool CombatBusy=>combatSending||ActionClock()<nextCombatAt;
    public bool CombatAvailable=>MovementAvailable&&!World.OwnDead&&World.HasAttackAmmunition&&World.Target is {Dead:false,AppearancePending:false};
    void ApplyCombatAcknowledgement(long reportsBefore){if(!World.OwnDead&&World.AttackReports>reportsBefore&&World.LastAttack?.Attacker==World.Actor?.ObjectId&&World.Actor is {} actor&&World.AppearanceProfile!=null)nextCombatAt=Math.Max(nextCombatAt,ActionClock()+(World.OwnCombat?.Timing?.WaitMs??(CombatRules.DurationFor(actor.Gfx,actor.Weapon)+CombatRules.ClockMargin(actor.Weapon))));}
    void ResetCombatSend(){combatLease++;combatSending=false;nextCombatAt=0;lastCombatTiming=null;lastCombatAdmission=-1;}
    public Task<bool> Attack()=>AttackCore(null);
    async Task<bool> AttackCore(Func<bool>? pointerCurrent){
        if(pointerCurrent?.Invoke()==false)return false;
        if(!World.HasAttackAmmunition){World.CombatNotice="未送出：沒有可用箭矢；請補充箭矢後再射擊。";return false;}
        if(World.Target is not {Dead:false,AppearancePending:false}){World.CombatNotice="未送出：未選取有效目標；請點中敵人的可見圖像，確認黃色選取圈後按 G。";return false;}
        if(!CombatAvailable||MovementBusy||sendMovementGate==null){World.CombatNotice="未送出：動作冷卻／忙碌，或目前速度／外觀不支援；請停下並等待可操作後再按 G。";return false;}
        var target=World.Target!;var actor=World.Actor!;int epoch=generation,worldEpoch=World.WorldEpoch,selection=World.TargetRevision,input=inputVersion,correction=World.Corrections,appearanceRevision=World.AppearanceRevision,speedRevision=World.SpeedRevision,lease=++combatLease;
        var timing=ActionCadence.Capture(actor.Gfx,actor.Weapon,1,actor.Speed,actor.Brave,margin:CombatRules.ClockMargin(actor.Weapon));int duration=timing.ServerIntervalMs,guard=timing.MarginMs;if(duration<=0)throw new InvalidDataException("missing attack cadence");
        var sender=sendMovementGate;byte[] bytes=CombatRules.Attack(target,actor.Weapon);combatSending=true;
        bool CompletionCurrent()=>epoch==generation&&lease==combatLease&&worldEpoch==World.WorldEpoch&&input==inputVersion&&selection==World.TargetRevision&&World.TargetId==target.ObjectId&&World.Target is {Dead:false,AppearancePending:false}&&correction==World.Corrections&&appearanceRevision==World.AppearanceRevision&&!World.SelfAppearancePending&&!World.OwnDead&&World.Status is {Hp:>0}&&World.Actor?.ObjectId==actor.ObjectId&&World.Actor?.Gfx==actor.Gfx&&World.Actor?.Weapon==actor.Weapon;
        try {
            bool sent=await sender(bytes,()=>CompletionCurrent()&&(pointerCurrent?.Invoke()??true)&&epoch==generation&&worldEpoch==World.WorldEpoch&&selection==World.TargetRevision&&input==inputVersion&&correction==World.Corrections&&appearanceRevision==World.AppearanceRevision&&speedRevision==World.SpeedRevision&&CombatAvailable&&World.Actor?.Gfx==actor.Gfx&&World.Actor?.Weapon==actor.Weapon&&World.TargetId==target.ObjectId&&World.Target?.X==target.X&&World.Target?.Y==target.Y&&!magicSending&&ActionAdmissionReady,()=>{
                lastCombatTiming=timing;lastCombatAdmission=ActionClock();combatTimingEpoch=World.WorldEpoch;nextCombatAt=lastCombatAdmission+timing.WaitMs;
                int dx=Math.Sign(target.X-actor.X),dy=Math.Sign(target.Y-actor.Y);
                int h=Enumerable.Range(0,8).FirstOrDefault(i=>MovementRules.Dx[i]==dx&&MovementRules.Dy[i]==dy,actor.Direction);
                World.PredictAction(1,h,target.ObjectId,timing);World.CombatNotice="單次攻擊已送出；動畫為預測，等待原服回報";Set(State,World.CombatNotice);
            });
            if(!CompletionCurrent())return false;
            if(!sent){World.CombatNotice=World.Target?.X!=target.X||World.Target?.Y!=target.Y?"未送出：目標已移動；請走近、停下後再按 G，不會自動追擊。":"未送出：動作狀態已改變，請等待可操作後再按 G。";return false;}
            nextCombatAt=Math.Max(nextCombatAt,ActionClock()+timing.WaitMs);
            CombatSent?.Invoke(new{utc=DateTimeOffset.UtcNow,monotonicMs=Environment.TickCount64,plainHex=Convert.ToHexString(bytes),worldEpoch,target=target.ObjectId,attackMs=duration,localClockMarginMs=guard});return true;
        }catch(Exception ex){if(CompletionCurrent()){Cancel();Set(LoginState.Faulted,"攻擊傳送失敗："+ex.Message);}return false;}
        finally{if(epoch==generation&&lease==combatLease)combatSending=false;}
    }
}
