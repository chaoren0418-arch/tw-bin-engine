using System.Text.Json;
namespace P06;
public sealed partial class LoginConnection {
    // Negative-test seam only. Product UI never calls it; no synthetic results or resource changes.
    internal async Task MagicProbe(int skill,uint target,ushort x,ushort y){if(State!=LoginState.InWorld||sendWorld==null)throw new Exception("probe requires real world");await sendWorld(MagicRules.Request(skill,target,x,y));}
    internal async Task MagicInvalidSkillProbe(){if(State!=LoginState.InWorld||sendWorld==null)throw new Exception("probe requires real world");await sendWorld(LoginPackets.Pad([6,27,4,0,0,0,0,0,0,0,0]));} // skill221 > original handler ceiling220; test-only.
}
public static class MagicScenario {
    // Test controller only: no server rule changes, no fallback or invented success.
    static async Task CombatWait(LoginConnection c,InventoryCapture log,Func<bool> ready,int milliseconds,bool stopOnDamage,string phase){
        uint hp=c.World.Status!.Hp;long end=Environment.TickCount64+milliseconds;
        while(true){
            if(c.State!=LoginState.InWorld||c.World.OwnDead||c.World.Status!.Hp<150||(stopOnDamage&&c.World.Status.Hp<hp)){
                log.Check("bounded-combat-wait-stop",new{phase,hpBefore=hp,current=c.World.Status,stopOnDamage});throw new Exception("finite combat wait stopped: "+phase);
            }
            if(ready())return;
            if(Environment.TickCount64>=end)throw new TimeoutException("finite combat wait deadline: "+phase);
            await Task.Delay(50);
        }
    }
    static async Task Walk(LoginConnection c,int x,int y,InventoryCapture log){for(int n=0;;n++){try{await ObjectTests.WalkTo(c,x,y);return;}catch(Exception e) when(e.Message=="no legal test route"&&n<20){log.Check("occupied-route",new{x,y,n});await Task.Delay(800);}}}
    public static async Task Run(LoginConnection c,LoginConnection o,RewardCase s,InventoryCapture log,Func<int,string,Task> selectMagic,Func<uint,string,Task> selectTarget,Func<string,Task<bool>> cast,Func<string,Task> shot,Func<Task>? focus=null,Func<Task>? recovery=null,Func<Task>? checkFocus=null,Func<uint[]>? visibleTargets=null){
        await ObjectTests.Wait(()=>c.World.InventoryLoaded&&c.World.MagicListReceived);log.Check("original-login-learned-list");
        if(!c.World.LearnedMagic.Select(x=>x.Id).SequenceEqual(new[]{1,4}))throw new Exception("original learned list mismatch");
        if(s.MagicVisualReview){await Review(c,o,s,log,selectMagic,selectTarget,cast,shot);return;}
        if(s.Fresh&&(c.World.Status!.MaxHp!=700||c.World.Status.MaxMp!=120||c.World.Inventory.Any(i=>i.Equipped==true)||c.World.Actor!.Weapon!=0))throw new Exception("fresh magic fixture mismatch");
        if(s.ResumeRecovery){if(recovery!=null)await recovery();else {if(c.World.Status!.Hp<500)throw new Exception("UI recovery controls required for injured saved role");long deadline=Environment.TickCount64+600000;uint initialHp=c.World.Status.Hp;while(c.World.Status.Mp<20){if(c.State!=LoginState.InWorld||c.World.OwnDead||c.World.Status.Hp<initialHp||c.World.Status.Food<3||c.World.Status.Weight>=120||Environment.TickCount64>=deadline)throw new Exception("finite original natural recovery stopped");await Task.Delay(50);}log.Check("original-natural-MP-resume-ready");}}
        await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>o.World.InventoryLoaded);
        if(o.World.Inventory.Length!=0||o.World.LearnedMagic.Length!=0)throw new Exception("observer fixture mismatch");
        if(s.ResumeRecovery){await Walk(c,32588,32930,log);await Walk(o,32589,32930,log);log.Check("original-normal-cross-block-resume");await shot("magic-resume-returned");}
        uint self=c.World.Actor!.ObjectId;await ObjectTests.Wait(()=>o.World.Objects.Any(v=>v.ObjectId==self));
        await selectMagic(1,"game");await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");uint hp=c.World.Status!.Hp,mp=c.World.Status.Mp;long e=c.World.MagicEffectCount,oe=o.World.MagicEffectCount;
        log.Check("before-original-self-heal",new{hp,mp,s.Fresh});if(!await cast("game-first-heal"))throw new Exception("first self heal not admitted");
        if(await c.CastSelectedMagic())throw new Exception("busy repeated cast admitted");
        await ObjectTests.Wait(()=>c.World.MagicEffects.Any(v=>v.Number>e&&v.Gfx==11722)&&o.World.MagicEffects.Any(v=>v.Number>oe&&v.Gfx==11722)&&c.World.Status!.Mp<mp,7000);
        if(hp<c.World.Status!.MaxHp)await ObjectTests.Wait(()=>c.World.Status!.Hp>hp,7000);log.Check("original-self-heal-both-peers",new{hpBefore=hp,mpBefore=mp,meaning="bounded wait for original HP commit/update after effect and MP; raw HP/MP and natural recovery reviewed independently"});await shot("magic-self-heal");await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");
        if(focus!=null)await focus();
        // Original silent rejection paths; exact raw outcome retained, never invent error packets.
        long before=c.World.MagicEffectCount;var vitals=c.World.Status;log.Check("before-original-unlearned-probe");await c.MagicProbe(2,self,c.World.Actor.X,c.World.Actor.Y);await Task.Delay(1700);if(c.World.MagicEffectCount!=before)throw new Exception("unlearned emitted supported effect");log.Check("after-original-unlearned-probe",new{vitalsBefore=vitals});
        log.Check("before-original-illegal-skill-probe");await c.MagicInvalidSkillProbe();await Task.Delay(1700);log.Check("after-original-illegal-skill-probe");
        log.Check("before-original-invalid-target-probe");await c.MagicProbe(4,uint.MaxValue,0,0);await Task.Delay(1700);log.Check("after-original-invalid-target-probe");
        if(checkFocus!=null)await checkFocus();
        await Walk(o,32589,32930,log);await Walk(c,32585,32948,log);await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");
        uint rangeHp=c.World.Status!.Hp;long rangeUntil=Environment.TickCount64+2000;
        WorldObject? far=null;while(Environment.TickCount64<rangeUntil){if(c.State!=LoginState.InWorld||c.World.OwnDead||c.World.Status!.Hp<150||c.World.Status.Hp<rangeHp)throw new Exception("range target wait stopped on actual damage/state");far=c.World.Objects.FirstOrDefault(v=>v.Gfx==1110&&!v.Dead&&Math.Max(Math.Abs(v.X-c.World.Actor!.X),Math.Abs(v.Y-c.World.Actor.Y))>10);if(far!=null)break;await Task.Delay(50);}
        if(far!=null){log.Check("before-original-out-of-range-probe",new{target=far,actor=c.World.Actor,range=10,meaning="real original target coordinates outside template range; raw outcome retained without inventing an error"});await c.MagicProbe(4,far.ObjectId,far.X,far.Y);long settle=Environment.TickCount64+1800;await CombatWait(c,log,()=>Environment.TickCount64>=settle,2000,false,"original-probe-settle");log.Check("after-original-out-of-range-probe");}
        else log.Check("range-live-branch-not-observed",new{reason="no original visible living wolf beyond10 in bounded observation; original source branch only"});
        // Test-only planning using original fixed-JAR static LOS oracle, never a product targeting rule.
        var blocked=File.ReadLines(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../reference/magic/los.tsv"))).Select(l=>l.Split('\t')).Where(v=>v[4]=="false").Select(v=>(X:int.Parse(v[0]),Y:int.Parse(v[1]),Tx:int.Parse(v[2]),Ty:int.Parse(v[3]))).ToHashSet();bool losSent=false;
        for(int attempt=1;attempt<=3&&!losSent;attempt++){
            var enemy=c.World.Objects.FirstOrDefault(v=>v.Gfx==1110&&!v.Dead);if(enemy==null)break;
            var candidates=blocked.Where(p=>p.Tx==enemy.X&&p.Ty==enemy.Y&&!c.World.Objects.Any(v=>v.X==p.X&&v.Y==p.Y)).OrderBy(p=>Math.Max(Math.Abs(p.X-c.World.Actor!.X),Math.Abs(p.Y-c.World.Actor.Y))).Take(10).ToArray();
            var destination=candidates.FirstOrDefault(p=>{try{ObjectTests.Route(c,p.X,p.Y,[]);return true;}catch{return false;}});if(destination==default)break;
            await Walk(c,destination.X,destination.Y,log);await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");enemy=c.World.Objects.FirstOrDefault(v=>v.ObjectId==enemy.ObjectId&&!v.Dead);
            if(enemy==null||!blocked.Contains((c.World.Actor!.X,c.World.Actor.Y,enemy.X,enemy.Y))){log.Check("LOS-plan-changed-before-send",new{attempt});continue;}
            long countBefore=c.World.MagicEffectCount;log.Check("before-original-static-LOS-probe",new{attempt,actor=c.World.Actor,target=enemy,staticOriginalLos=false,oracle="reference/magic/los.tsv",limitation="static original terrain oracle; dynamic doors not sampled"});
            await c.MagicProbe(4,enemy.ObjectId,enemy.X,enemy.Y);long settle=Environment.TickCount64+1800;await CombatWait(c,log,()=>Environment.TickCount64>=settle,2000,false,"original-probe-settle");log.Check("after-original-static-LOS-probe",new{target=c.World.Objects.FirstOrDefault(v=>v.ObjectId==enemy.ObjectId),damagingBolts=c.World.MagicEffects.Where(v=>v.Number>countBefore&&v.Gfx==167&&v.Target==enemy.ObjectId&&v.DamageWord>0).ToArray()});losSent=true;
        }
        if(!losSent)log.Check("static-LOS-live-probe-unavailable",new{reason="no stable reachable original static-blocked pair in three finite attempts; do not claim live LOS rejection"});
        await Walk(c,32595,32948,log);await Walk(o,32593,32949,log);await CombatWait(c,log,()=>!c.MovementBusy&&!o.MovementBusy,2000,false,"original-peers-busy-release");
        using var targetRenderer=visibleTargets==null?new Renderer(new Assets(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets")))):null;
        var targetAssets=visibleTargets==null?new ObjectAssets(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets"))):null;
        var targetWorld=visibleTargets==null?new World(new Assets(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets")))):null;
        uint[] RenderedTargets(){return c.CaptureWorld(()=>{var actor=c.World.Actor!;targetWorld!.Teleport(actor.X,actor.Y);targetWorld.Face(actor.Direction);var patch=targetRenderer!.ComposeObjects(targetWorld,c.World,targetAssets!,out _,out _);return patch.EntityOwners!.Where(id=>id.HasValue).Select(id=>id!.Value).Distinct().ToArray();});}
        var preparation=new MagicTargetPreparation(c,o,log,visibleTargets??RenderedTargets);
        uint target=(await preparation.Prepare()).ObjectId;var xp=c.World.Experience!;var gold=RewardScenario.Gold(c);int count=0;bool both=false;
        await selectMagic(4,"web");
        while(c.World.Experience!.Total==xp.Total){
            if(++count>45||c.World.OwnDead||c.World.Status!.Hp<150||c.World.Status.Mp<2)throw new Exception("bounded original magic combat stopped; preserve state");
            await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");
            var enemy=await preparation.Prepare(target);target=enemy.ObjectId;
            await selectTarget(target,count%2==1?"game":"web"); // Explicit full-flow coverage even when one target persists.
            if(preparation.Current(target)?.ObjectId!=target)throw new Exception("actual target became hidden before single cast; preserve failure");
            long mainBefore=c.World.MagicEffectCount,otherBefore=o.World.MagicEffectCount;log.Check("before-single-bolt-"+count,new{target});
            if(!await cast(count%2==1?"game-bolt":"web-bolt"))throw new Exception("single bolt not admitted");
            await CombatWait(c,log,()=>c.World.MagicEffects.Any(v=>v.Number>mainBefore&&v.Gfx==167&&v.Caster==self)&&o.World.MagicEffects.Any(v=>v.Number>otherBefore&&v.Gfx==167&&v.Caster==self),7000,false,"original-sent-bolt-response");
            both=true;log.Check("original-single-bolt-"+count);await shot("magic-bolt-"+count);
        }
        await CombatWait(c,log,()=>c.World.Objects.Any(v=>v.ObjectId==target&&v.Dead)&&o.World.Objects.Any(v=>v.ObjectId==target&&v.Dead),7000,false,"original-death-response");
        if(!both||c.World.TargetId!=null||await c.CastSelectedMagic())throw new Exception("dead target selection still admits");
        await CombatWait(c,log,()=>RewardScenario.Gold(c).Count>gold.Count,7000,false,"original-reward-response");log.Check("original-magic-kill-and-natural-rewards",new{target,casts=count,xpBefore=xp,goldBefore=gold});await shot("magic-kill-rewards");
        await Walk(c,32588,32930,log);await Walk(o,32589,32930,log);await CombatWait(c,log,()=>!c.MovementBusy&&!o.MovementBusy,2000,false,"original-peers-busy-release");
        // Bounded test-only exhaustion through original heal, including full-HP casts. No SQL drain/refill.
        await selectMagic(1,"web");int drain=0;bool rejected=false;
        while(drain++<90){
            await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");uint beforeMp=c.World.Status!.Mp;long beforeEffect=c.World.MagicEffectCount;log.Check("before-finite-heal-"+drain,new{beforeMp});
            if(!await cast(drain%2==1?"game-drain-heal":"web-drain-heal"))throw new Exception("finite heal not admitted");
            await CombatWait(c,log,()=>c.World.MagicEffectCount>beforeEffect&&c.World.Status!.Mp<beforeMp||c.World.LastServerMessage=="原服訊息 278",7000,false,"finite-heal-original-response");
            if(c.World.LastServerMessage=="原服訊息 278"&&c.World.MagicEffectCount==beforeEffect){rejected=true;log.Check("original-insufficient-MP-278",new{beforeMp,drain});await shot("magic-original-MP-refusal");break;}
            log.Check("original-finite-heal-"+drain,new{beforeMp});
        }
        if(!rejected)throw new Exception("bounded original MP exhaustion not reached");
        uint low=c.World.Status!.Mp;log.Check("before-natural-MP-recovery",new{low});await CombatWait(c,log,()=>c.World.Status!.Mp>low,90000,false,"original-natural-MP-recovery");log.Check("original-natural-MP-recovery",new{low,after=c.World.Status!.Mp});await shot("magic-natural-MP-recovery");
        await SaveRelogin(c,o,s,log,shot);
    }
    static async Task SaveRelogin(LoginConnection c,LoginConnection o,RewardCase s,InventoryCapture log,Func<string,Task> shot){
        await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");var inventory=c.World.Inventory;var actor=c.World.Actor!;var exp=c.World.Experience!;var status=c.World.Status!;log.Check("before-normal-save-disconnect");c.Cancel();o.Cancel();await Task.Delay(3000);
        await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>c.World.InventoryLoaded&&o.World.InventoryLoaded&&c.World.MagicListReceived);
        if(!c.World.LearnedMagic.Select(x=>x.Id).SequenceEqual(new[]{1,4})||c.World.Inventory.Length!=inventory.Length||inventory.Any(i=>!c.World.Inventory.Any(v=>v.ObjectId==i.ObjectId&&v.Count==i.Count&&v.Equipped==i.Equipped))||c.World.Actor!.X!=actor.X||c.World.Actor.Y!=actor.Y||c.World.Experience!.Total!=exp.Total)throw new Exception("saved relogin mismatch");
        log.Check("normal-magic-relogin-saved",new{statusAtDisconnect=status});await shot("magic-relogin");await Task.Delay(1500);log.Check("no-delayed-cast-after-relogin");
    }
    static async Task Review(LoginConnection c,LoginConnection o,RewardCase s,InventoryCapture log,Func<int,string,Task> selectMagic,Func<uint,string,Task> selectTarget,Func<string,Task<bool>> cast,Func<string,Task> shot){
        log.Check("visual-resource-review-only",new{meaning="same saved role, no reset; prior full gameplay acceptance remains in ui-r2; wait for original natural MP"});
        await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>o.World.InventoryLoaded);
        uint self=c.World.Actor!.ObjectId;await ObjectTests.Wait(()=>o.World.Objects.Any(v=>v.ObjectId==self));
        log.Check("review-before-natural-MP");await CombatWait(c,log,()=>c.World.Status!.Mp>=(s.MagicFlightOnly?2:6),180000,false,"review-original-natural-MP-recovery");log.Check("review-natural-MP-ready");long before,other;
        if(!s.MagicFlightOnly){await selectMagic(1,"web");before=c.World.MagicEffectCount;other=o.World.MagicEffectCount;uint mp=c.World.Status!.Mp;
        if(!await cast("web-review-heal"))throw new Exception("review heal not admitted");await ObjectTests.Wait(()=>c.World.MagicEffectCount>before&&o.World.MagicEffectCount>other&&c.World.Status!.Mp<mp);await shot("magic-self-heal");await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");}
        await Walk(c,32598,32941,log);await Walk(o,32596,32942,log);await CombatWait(c,log,()=>!c.MovementBusy&&!o.MovementBusy,2000,false,"original-peers-busy-release");
        bool Candidate(WorldObject v)=>v.Gfx==1110&&!v.Dead&&Math.Max(Math.Abs(v.X-c.World.Actor!.X),Math.Abs(v.Y-c.World.Actor.Y)) is >=3 and <=10&&o.World.Objects.Any(z=>z.ObjectId==v.ObjectId);
        await CombatWait(c,log,()=>c.World.Objects.Any(Candidate),2000,true,"first-visible-target");var target=c.World.Objects.First(Candidate);await selectMagic(4,"web");await selectTarget(target.ObjectId,"web");before=c.World.MagicEffectCount;other=o.World.MagicEffectCount;log.Check("review-before-visible-bolt",new{target});
        if(!await cast("web-review-bolt"))throw new Exception("review bolt not admitted");await ObjectTests.Wait(()=>c.World.MagicEffects.Any(v=>v.Number>before&&v.Gfx==167)&&o.World.MagicEffects.Any(v=>v.Number>other&&v.Gfx==167));await shot("magic-bolt-review");log.Check("review-after-visible-bolt");
        await CombatWait(c,log,()=>!c.MovementBusy,2000,false,"original-busy-release");await Walk(c,32588,32930,log);await Walk(o,32589,32930,log);await SaveRelogin(c,o,s,log,shot);
    }
    public static async Task Probe(string path){var s=JsonSerializer.Deserialize<RewardCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var log=new InventoryCapture(c,o);try{await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await Run(c,o,s,log,(id,mode)=>{if(!c.World.SelectMagic(id))throw new Exception("magic selection");return Task.CompletedTask;},(id,mode)=>{c.World.SelectTarget(id);if(c.World.TargetId!=id)throw new Exception("target selection");return Task.CompletedTask;},_=>c.CastSelectedMagic(),_=>Task.CompletedTask);log.Save(s.Output,"pass");}catch(Exception e){log.Save(s.Output,"fail",e);throw;}finally{c.Cancel();o.Cancel();}}
}
