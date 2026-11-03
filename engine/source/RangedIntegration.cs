using System.Text.Json;
namespace P06;
public static class RangedScenario {
    public static uint Arrows(LoginConnection c)=>c.World.Inventory.Where(i=>i.SupportedArrow).Aggregate(0u,(n,i)=>n+i.Count);
    static async Task Walk(LoginConnection c,int x,int y,InventoryCapture log){
        for(int n=1;;n++){try{await ObjectTests.WalkTo(c,x,y);return;}catch(Exception e) when(e.Message=="no legal test route"&&n<30){log.Check("route-occupied",new{x,y,n});await Task.Delay(1000);}}
    }
    public static async Task Run(LoginConnection c,LoginConnection o,RewardCase s,InventoryCapture log,Func<uint,Task> selectItem,Func<string,Task<bool>> use,Func<uint,Task> selectTarget,Func<string,Task<bool>> attack,Func<uint,Task> selectGround,Func<bool,uint,string,Task<bool>> ground,Func<string,Task> shot,Func<Task>? focus=null){
        await ObjectTests.Wait(()=>c.World.InventoryLoaded);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>o.World.InventoryLoaded);
        if(s.Fresh&&(Arrows(c)!=80||c.World.Inventory.Count(i=>i.SupportedEquipment)!=5||c.World.Inventory.Any(i=>i.Equipped==true)||c.World.Actor!.Weapon!=0||o.World.Inventory.Length!=0))throw new Exception("fresh ranged fixture mismatch; never reset");
        log.Check("ranged-initial",new{s.Fresh});await shot("ranged-initial");
        async Task Equip(int icon,string mode){await ObjectTests.Wait(()=>!c.MovementBusy);var item=c.World.Inventory.Single(i=>i.IconId==icon&&i.SupportedEquipment);if(item.Equipped==true)return;await selectItem(item.ObjectId);log.Check("before-equip-"+icon);if(!await use(mode))throw new Exception("equip not admitted");await ObjectTests.Wait(()=>c.World.Inventory.Single(i=>i.ObjectId==item.ObjectId).Equipped==true);await ObjectTests.Wait(()=>!c.MovementBusy);log.Check("after-equip-"+icon);}
        // A resumed role keeps its saved inventory. Any incompatible shield is removed by original C164.
        var shield=c.World.Inventory.Single(i=>i.IconId==97&&i.SupportedEquipment);
        if(shield.Equipped==true){await selectItem(shield.ObjectId);if(!await use("game-resume-shield-off"))throw new Exception("shield removal");await ObjectTests.Wait(()=>!c.MovementBusy&&c.World.Inventory.Single(i=>i.ObjectId==shield.ObjectId).Equipped==false);}
        await Equip(278,"game-bow-on");await Equip(86,"web-armor-on");
        uint self=c.World.Actor!.ObjectId;await ObjectTests.Wait(()=>c.World.Actor.Weapon==20&&o.World.Objects.Any(v=>v.ObjectId==self&&v.Action==20));
        log.Check("both-peers-original-bow20");await shot("ranged-bow-standing");
        await Walk(c,32588,32930,log);await Walk(o,32589,32930,log);await ObjectTests.Wait(()=>!c.MovementBusy&&!o.MovementBusy);
        uint finite=Arrows(c);if(finite==0)throw new Exception("finite arrows exhausted; prior run requires a review-only continuation");
        // Original fixed config/ItemDropTable refuses arrow40743 with S71 message125.
        // Preserve this real refusal; never alter whitelist or manufacture a ground arrow.
        await ObjectTests.Wait(()=>!c.MovementBusy);var stack=c.World.Inventory.Single(i=>i.SupportedArrow);await selectItem(stack.ObjectId);log.Check("before-original-arrow-drop-refusal",new{finite,stack});
        if(!await ground(false,finite,"game-original-arrow-drop-refusal"))throw new Exception("drop refusal must reach original JAR");
        await ObjectTests.Wait(()=>c.World.LastServerMessage=="原服訊息 125");await ObjectTests.Wait(()=>!c.MovementBusy);
        if(Arrows(c)!=finite||c.World.Ground.Any(g=>g.Gfx==66))throw new Exception("original refused drop changed finite arrow state");
        log.Check("original-arrow-drop-refused-125");await shot("ranged-drop-refused");
        await ObjectTests.Wait(()=>!c.MovementBusy);await selectTarget(o.World.Actor!.ObjectId);uint observerId=o.World.Actor.ObjectId;uint rangeAmmo=Arrows(c);
        await Walk(o,32610,32930,log);await ObjectTests.Wait(()=>c.World.Objects.All(v=>v.ObjectId!=observerId));
        if(c.World.TargetId!=null||await c.Attack()||Arrows(c)!=rangeAmmo)throw new Exception("out-of-screen removed target still admitted");
        log.Check("original-range-AOI-removal-local-stale-refusal",new{observer=o.World.Actor,main=c.World.Actor,meaning="original visibility removed target beyond screen; no fabricated server range refusal packet"});
        c.World.SelectTarget(null);if(await c.Attack())throw new Exception("no selection admitted");
        await Walk(c,32595,32948,log);await Walk(o,32593,32949,log);await ObjectTests.Wait(()=>!c.MovementBusy&&!o.MovementBusy);
        if(focus!=null)await focus();
        bool Ready(WorldObject v)=>v.Gfx==1110&&!v.Dead&&Math.Max(Math.Abs(v.X-c.World.Actor!.X),Math.Abs(v.Y-c.World.Actor.Y))>1&&RangedRules.InScreen(c.World.Actor.X,c.World.Actor.Y,v.X,v.Y)&&(v.MotionAt==0||Environment.TickCount64-v.MotionAt>=640)&&o.World.Objects.Any(z=>z.ObjectId==v.ObjectId);
        await ObjectTests.Wait(()=>c.World.Objects.Any(Ready),90000);uint id=c.World.Objects.First(Ready).ObjectId;
        var xp=c.World.Experience!;var gold=RewardScenario.Gold(c);bool rangedProved=false;int shots=0;
        while(c.World.Objects.Any(v=>v.ObjectId==id&&!v.Dead)){
            if(++shots>40)throw new Exception("bounded explicit test shot limit");await ObjectTests.Wait(()=>!c.MovementBusy);
            if(c.World.OwnDead||c.World.Status!.Hp<150||Arrows(c)==0)throw new Exception("stop bounded test: health/death/finite ammo");
            if(c.World.TargetId!=id)await selectTarget(id);uint count=Arrows(c);long report=c.World.AttackReports,observerReport=o.World.AttackReports;
            log.Check("before-single-arrow-"+shots,new{id,count});if(!await attack(shots%2==1?"game-single":"web-single"))throw new Exception("explicit shot not admitted; preserve saved state");
            if(await c.Attack())throw new Exception("busy duplicate admitted");
            await ObjectTests.Wait(()=>c.World.Projectiles.Any(p=>p.Attacker==self&&p.Number>report)&&o.World.Projectiles.Any(p=>p.Attacker==self&&p.Number>observerReport)&&Arrows(c)==count-1,7000);
            var p=c.World.Projectiles.Last(p=>p.Attacker==self);var op=o.World.Projectiles.Last(p=>p.Attacker==self);
            if(p.Attacker!=self||op.Attacker!=self||p.SourceX!=op.SourceX||p.SourceY!=op.SourceY||p.TargetX!=op.TargetX||p.TargetY!=op.TargetY)throw new Exception("dual peer arrow mismatch");
            int distance=Math.Max(Math.Abs(p.TargetX-p.SourceX),Math.Abs(p.TargetY-p.SourceY));rangedProved|=distance>1;
            log.Check("original-single-arrow-"+shots,new{countBefore=count,countAfter=Arrows(c),distance,projectile=p,observerProjectile=op});
            await shot("ranged-shot-"+shots);await ObjectTests.Wait(()=>!c.MovementBusy);
        }
        if(!rangedProved||!c.World.Objects.Any(v=>v.ObjectId==id&&v.Dead))throw new Exception("real distance>1/death not proved");
        await ObjectTests.Wait(()=>o.World.Objects.Any(v=>v.ObjectId==id&&v.Dead));if(c.World.TargetId!=null||await c.Attack())throw new Exception("dead target admitted");
        await ObjectTests.Wait(()=>c.World.Experience!.Total>xp.Total&&RewardScenario.Gold(c).Count>gold.Count,10000);
        log.Check("original-ranged-kill-reward",new{id,shots,xpBefore=xp,goldBefore=gold,finite,remaining=Arrows(c)});await shot("ranged-natural-reward");
        await ObjectTests.Wait(()=>c.World.Objects.All(v=>v.ObjectId!=id)&&o.World.Objects.All(v=>v.ObjectId!=id),30000);if(await c.Attack())throw new Exception("removed target admitted");log.Check("original-corpse-removed");
        // Consume the remaining finite arrows through individually admitted original shots in the original safety zone.
        // This bounded controller is a test driver; product input remains one explicit request, no retry/chase loop.
        await Walk(c,32588,32930,log);await Walk(o,32589,32930,log);await ObjectTests.Wait(()=>!c.MovementBusy&&!o.MovementBusy);
        int drainShots=0;uint safeHp=o.World.Status!.Hp;
        while(Arrows(c)>0){
            if(++drainShots>80)throw new Exception("finite exhaustion bound");await ObjectTests.Wait(()=>!c.MovementBusy);await selectTarget(o.World.Actor!.ObjectId);
            uint count=Arrows(c);long seq=c.World.AttackReports,oseq=o.World.AttackReports;log.Check("before-finite-safe-shot-"+drainShots,new{count});
            if(!await attack(drainShots%2==1?"game-finite-single":"web-finite-single"))throw new Exception("finite single not admitted");
            await ObjectTests.Wait(()=>Arrows(c)==count-1&&c.World.Projectiles.Any(p=>p.Attacker==self&&p.Number>seq)&&o.World.Projectiles.Any(p=>p.Attacker==self&&p.Number>oseq),7000);
            if(o.World.Status!.Hp!=safeHp)throw new Exception("safe-zone target HP changed; stop test");
            log.Check("original-finite-safe-shot-"+drainShots,new{countBefore=count,countAfter=Arrows(c)});
            if(count is 1 or 2||drainShots==1)await shot("ranged-finite-"+count);
        }
        await ObjectTests.Wait(()=>!c.MovementBusy);await selectTarget(o.World.Actor!.ObjectId);long before=c.World.AttackReports,otherBefore=o.World.AttackReports,at=Environment.TickCount64;
        log.Check("before-original-noammo");if(!await attack("web-noammo"))throw new Exception("noammo must reach original server");
        await ObjectTests.Wait(()=>c.World.OwnCombat is {Action:1,Predicted:false} a&&a.At>=at,5000);await ObjectTests.Wait(()=>!c.MovementBusy);
        if(c.World.AttackReports!=before||o.World.AttackReports!=otherBefore||Arrows(c)!=0)throw new Exception("original noammo changed projectile/count");
        log.Check("original-noammo-action-without-projectile",new{finite,combatShots=shots,drainShots});await shot("ranged-no-ammo-action");
        await ObjectTests.Wait(()=>!c.MovementBusy);var saved=c.World.Inventory;var actor=c.World.Actor!;var exp=c.World.Experience!;var hp=c.World.Status!;
        log.Check("before-normal-save-disconnect",new{hp});c.Cancel();o.Cancel();await Task.Delay(3000);
        await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>c.World.InventoryLoaded&&o.World.InventoryLoaded);
        if(c.World.Inventory.Length!=saved.Length||saved.Any(i=>!c.World.Inventory.Any(j=>i.ObjectId==j.ObjectId&&i.Count==j.Count&&i.Equipped==j.Equipped))||c.World.Actor!.X!=actor.X||c.World.Actor.Y!=actor.Y||c.World.Actor.Weapon!=20||c.World.Experience!.Total!=exp.Total||o.World.Inventory.Length!=0)throw new Exception("normal saved relogin mismatch");
        log.Check("normal-ranged-relogin-saved",new{hpAtExit=hp,hpAfterLogin=c.World.Status});await shot("ranged-relogin");await Task.Delay(1200);log.Check("no-delayed-attack-after-relogin");
    }
    public static async Task Probe(string path){var s=JsonSerializer.Deserialize<RewardCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var log=new InventoryCapture(c,o);try{await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await Run(c,o,s,log,id=>{if(!c.World.SelectItem(id))throw new Exception("item selection");return Task.CompletedTask;},_=>c.UseSelectedItem(),id=>{c.World.SelectTarget(id);if(c.World.TargetId!=id)throw new Exception("target selection");return Task.CompletedTask;},_=>c.Attack(),id=>{if(!c.World.SelectGround(id))throw new Exception("ground selection");return Task.CompletedTask;},(pick,n,mode)=>pick?c.PickupSelected(n):c.DropSelected(n),_=>Task.CompletedTask);log.Save(s.Output,"pass");}catch(Exception e){log.Save(s.Output,"fail",e);throw;}finally{c.Cancel();o.Cancel();}}
}
