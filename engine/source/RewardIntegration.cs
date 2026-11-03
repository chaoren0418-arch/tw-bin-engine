using System.Text.Json;
namespace P06;
public sealed record RewardCase(string Account,string Password,string Character,string ObserverAccount,string ObserverCharacter,string Output,bool Fresh=true,bool MagicVisualReview=false,bool MagicFlightOnly=false,bool ResumeRecovery=false);
public static class RewardScenario {
    public static InventoryItem Gold(LoginConnection c)=>c.World.Inventory.Single(i=>i.DescriptionId==0&&i.IconId==5&&(i.Name=="$4"||i.Name.StartsWith("$4 (",StringComparison.Ordinal)));
    static async Task Walk(LoginConnection c,int x,int y,InventoryCapture log){
        for(int attempt=1;attempt<=30;attempt++){
            try{await ObjectTests.WalkTo(c,x,y);return;}
            catch(Exception ex) when(ex.Message=="no legal test route"){
                log.Check("bounded-route-occupied",new{x,y,attempt,actor=c.World.Actor,occupants=c.World.Objects.Where(v=>v.X==x&&v.Y==y).ToArray()});
                if(attempt==30)throw;await Task.Delay(1500);
            }
        }
    }
    public static async Task Run(LoginConnection c,LoginConnection o,RewardCase s,InventoryCapture log,Func<uint,Task> select,Func<string,Task<bool>> attack,Func<string,Task> shot,Func<Task>? focus=null,Func<Task>? afterFirstAttack=null){
        await ObjectTests.Wait(()=>c.World.InventoryLoaded);if(o.State!=LoginState.InWorld)await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>o.World.InventoryLoaded);
        var initial=c.World.Experience!;var observerInitial=o.World.Experience!;var gold=Gold(c);
        if(initial.Total==null||observerInitial.Total==null)throw new Exception("initial EXP unknown");
        if(s.Fresh&&(initial.Total!=416461882||gold.Count!=3||o.World.Inventory.Length!=0))throw new Exception("fresh finite fixture mismatch; no reset");
        log.Check("initial-exp-and-finite-inventory");await shot("reward-initial");
        async Task HealIfLow(uint threshold){
            if(c.World.Status!.Hp>=threshold)return;
            await ObjectTests.Wait(()=>!c.MovementBusy);
            var potion=c.World.Inventory.FirstOrDefault(i=>i.SupportedPotion&&i.Count>0)??throw new Exception("finite potions exhausted; no refill");
            c.World.SelectItem(potion.ObjectId);uint hp=c.World.Status.Hp;long effect=c.World.ItemEffectNumber;log.Check("before-finite-potion",new{hp,potion});
            if(!await c.UseSelectedItem())throw new Exception("finite potion rejected");
            await ObjectTests.Wait(()=>c.World.Inventory.All(i=>i.ObjectId!=potion.ObjectId)||c.World.Inventory.Any(i=>i.ObjectId==potion.ObjectId&&i.Count==potion.Count-1),5000);
            await ObjectTests.Wait(()=>c.World.ItemEffectNumber>effect&&c.World.Status!.Hp>hp,5000);
            log.Check("after-finite-potion",new{hpBefore=hp,hpAfter=c.World.Status.Hp});await ObjectTests.Wait(()=>!c.MovementBusy);
        }
        await HealIfLow(400);
        if(await c.Attack())throw new Exception("attack without target admitted");
        await Walk(c,32591,32945,log);await Walk(o,32590,32946,log);
        await ObjectTests.Wait(()=>c.World.Objects.Any(v=>v.Gfx==1110&&!v.Dead),90000);uint id=c.World.Objects.First(v=>v.Gfx==1110&&!v.Dead).ObjectId;
        await Walk(c,32599,32948,log);await ObjectTests.Wait(()=>!c.MovementBusy);
        bool Settled(WorldObject v)=>v.MotionAt==0||Environment.TickCount64-v.MotionAt>=640;
        await ObjectTests.Wait(()=>c.World.Objects.Any(v=>v.ObjectId==id&&!v.Dead&&Math.Max(Math.Abs(v.X-c.World.Actor!.X),Math.Abs(v.Y-c.World.Actor.Y))==1&&Settled(v))&&o.World.Objects.Any(v=>v.ObjectId==id),90000);
        bool firstAttack=true;
        async Task Single(string mode){
            for(int retry=1;retry<=10;retry++){
                await ObjectTests.Wait(()=>!c.MovementBusy);
                if(firstAttack){
                    // A roaming original NPC can remove/reappear between initial click and admission.
                    // Before any sent attack only: remain in the legal home-adjacent position,
                    // wait for the same original visible actor, then make a NEW explicit selection.
                    await ObjectTests.Wait(()=>c.State!=LoginState.InWorld||c.World.OwnDead||c.World.Objects.Any(v=>v.ObjectId==id&&!v.Dead&&Math.Max(Math.Abs(v.X-c.World.Actor!.X),Math.Abs(v.Y-c.World.Actor.Y))==1&&Settled(v)),10000);
                    if(c.State!=LoginState.InWorld||c.World.OwnDead)throw new Exception("first attack selection stopped");
                    if(retry>1)log.Check("before-first-attack-fresh-selection",new{id,retry,previousTarget=c.World.TargetId});await select(id);
                }else await CombatTests.Approach(c,id);
                await ObjectTests.Wait(()=>!c.MovementBusy);
                if(await attack(mode)){firstAttack=false;return;}
                log.Check("single-input-not-admitted",new{mode,retry,target=c.World.Target,notice=c.World.CombatNotice,c.CombatAvailable,c.MovementAvailable,c.MovementBusy,c.CombatBusy,c.ItemBusy,c.GroundBusy});await Task.Delay(700);
            }
            throw new Exception("bounded explicit input admission exhausted");
        }
        log.Check("original-home-ready-for-explicit-selection",new{id});
        await Single("game-home");
        await ObjectTests.Wait(()=>!c.MovementBusy);
        if(afterFirstAttack!=null)await afterFirstAttack();
        // The original AI may choose diagonal pursuit. No P24 proof requires forcing a chase path.
        // Keep the actual legal adjacent home position; original scenery occlusion stays unchanged.
        await select(id);log.Check("visible-melee-selected",new{id});await shot("reward-selected");if(focus!=null)await focus();
        int attempts=1;
        while(c.World.Objects.Any(v=>v.ObjectId==id&&!v.Dead)){
            if(++attempts>60)throw new Exception("bounded explicit single attack limit");
            await ObjectTests.Wait(()=>!c.MovementBusy);
            if(c.State!=LoginState.InWorld||c.World.OwnDead||c.World.Status!.Hp==0)throw new Exception("self dead/disconnected; stop");
            await HealIfLow(250);
            if(c.World.TargetId!=id)throw new Exception("target removed/changed before death; no automatic reselect");
            log.Check("before-single-attack-"+attempts,new{id});
            await Single(attempts%2==0?"game-single":"web-single");
            if(await c.Attack())throw new Exception("busy duplicate sent");
            await Task.Delay(CombatRules.AttackMs+80);log.Check("after-single-attack-"+attempts,new{id});
        }
        if(!c.World.Objects.Any(v=>v.ObjectId==id&&v.Dead))throw new Exception("original death missing");
        await ObjectTests.Wait(()=>o.World.Objects.Any(v=>v.ObjectId==id&&v.Dead),5000);
        if(c.World.TargetId!=null||await c.Attack())throw new Exception("dead target still attackable");
        log.Check("original-death-both-peers",new{id,attempts});
        await ObjectTests.Wait(()=>c.World.Experience?.Total>initial.Total&&Gold(c).Count>gold.Count,10000);
        log.Check("original-exp-and-natural-inventory",new{id,expBefore=initial,expAfter=c.World.Experience,goldBefore=gold,goldAfter=Gold(c),observerBefore=observerInitial,observerAfter=o.World.Experience});await shot("reward-natural-loot");
        await ObjectTests.Wait(()=>c.World.Objects.All(v=>v.ObjectId!=id)&&o.World.Objects.All(v=>v.ObjectId!=id),30000);log.Check("original-corpse-removed");
        if(await c.Attack())throw new Exception("removed target attack sent");
        // Stay in the verified outdoor approach area; the original town bots can occupy 32584/32930.
        // Observer needs no post-kill movement. Both save their actual normal positions.
        await Walk(c,32591,32945,log);await ObjectTests.Wait(()=>!c.MovementBusy&&!o.MovementBusy);
        var saved=c.World.Inventory;var savedObserver=o.World.Inventory;var xp=c.World.Experience!;var oxp=o.World.Experience!;var actor=c.World.Actor!;
        log.Check("before-normal-save-disconnect");c.Cancel();o.Cancel();await Task.Delay(3000);
        await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>c.World.InventoryLoaded&&o.World.InventoryLoaded);
        bool Same(InventoryItem[] a,InventoryItem[] b)=>a.Length==b.Length&&a.All(i=>b.Any(j=>i.ObjectId==j.ObjectId&&i.Count==j.Count&&i.DescriptionId==j.DescriptionId&&i.IconId==j.IconId));
        if(c.World.Experience is not {} actualXp||actualXp.Total!=xp.Total||actualXp.Level!=xp.Level||o.World.Experience?.Total!=oxp.Total||!Same(saved,c.World.Inventory)||!Same(savedObserver,o.World.Inventory)||c.World.Actor!.X!=actor.X||c.World.Actor.Y!=actor.Y)throw new Exception("normal saved relogin mismatch");
        if(c.World.ExperienceChanges!=0||c.World.InventoryChanges.Length!=0||c.World.TargetId!=null)throw new Exception("relogin stale reward/target");
        log.Check("normal-relogin-saved");await shot("reward-relogin");await Task.Delay(1200);log.Check("no-delayed-request-after-relogin");
    }
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<RewardCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var log=new InventoryCapture(c,o);
        try{await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await Run(c,o,s,log,id=>{c.World.SelectTarget(id);if(c.World.TargetId!=id)throw new Exception("select target failed");return Task.CompletedTask;},_=>c.Attack(),_=>Task.CompletedTask);log.Save(s.Output,"pass");}
        catch(Exception ex){log.Save(s.Output,"fail",ex);throw;}finally{c.Cancel();o.Cancel();}
    }
}
public sealed partial class ClientForm {
    LoginConnection? rewardObserver;InventoryCapture? rewardCapture;
    void PrepareRewardCapture(){rewardObserver=new();rewardCapture=new(login,rewardObserver);}
    async Task RewardDemo(){
        var s=JsonSerializer.Deserialize<RewardCase>(File.ReadAllText(Environment.GetEnvironmentVariable("P24_REWARD_CASE")!))!;using var o=rewardObserver??throw new Exception("capture must precede first seed");var log=rewardCapture!;
        async Task Shot(string name){SyncRewards();SyncInventory();lastWebState="";PublishWebState();await web!.ExecuteScriptAsync("document.getElementById('rewardsPanel').scrollIntoView({block:'start'})");await Task.Delay(250);RefreshScene();Update();CaptureEvidence(name);await using var f=File.Create(Path.Combine(evidence!,name+"-web.png"));await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);string dom=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('experienceResult').innerText"))!;if(!dom.Contains(login.World.Experience!.Total!.ToString()!))throw new Exception("Web EXP missing");log.Check("ui-"+name,new{kind="WinForms controller and actual WebView2 DOM; not external native input",gameText=rewardsLabel.Text,dom,inventoryDom=await web.ExecuteScriptAsync("document.getElementById('inventoryPanel').innerText"),changesDom=await web.ExecuteScriptAsync("document.getElementById('rewardChanges').innerText")});}
        bool initialSelection=true;
        async Task Select(uint id){
            int limit=initialSelection?8:1;initialSelection=false;
            for(int attempt=1;attempt<=limit;attempt++){
                await Until(()=>!World.Walking,5000);
                await Until(()=>login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Objects.Any(v=>v.ObjectId==id),5000);
                if(login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Objects.SingleOrDefault(v=>v.ObjectId==id) is not {Dead:false})throw new Exception("selection stopped: disconnected, dead or original object absent");
                RefreshScene();ClickScene(VisiblePoint(id));await Task.Delay(40);
                if(login.World.TargetId==id)return;
                // Before the first attack only, original teleport can remove a successfully clicked NPC.
                // Wait for its own original pack and click its new visible pixels; never restore a stale target.
                if(attempt==limit||login.World.TargetId!=null||login.World.Objects.Any(v=>v.ObjectId==id))throw new Exception("original visible pixel target selection failed");
                log.Check("initial-selection-cleared-by-original-removal",new{id,attempt,limit});await Task.Delay(100);
            }
        }
        async Task<bool> Attack(string mode){int count=0;void Sent(object p)=>count++;login.CombatSent+=Sent;try{if(mode.StartsWith("web")){lastWebState="";PublishWebState();await Task.Delay(80);await web!.ExecuteScriptAsync("document.getElementById('attack').click();document.getElementById('attack').click()");}else{ReturnGameFocus();OnKeyDown(new KeyEventArgs(Keys.G));OnKeyDown(new KeyEventArgs(Keys.G));await Task.Delay(30);OnKeyUp(new KeyEventArgs(Keys.G));}for(int n=0;n<20&&count==0;n++)await Task.Delay(20);if(count>1)throw new Exception("held/repeated input sent twice");return count==1;}finally{login.CombatSent-=Sent;}}
        async Task Focus(){int count=0;void Sent(object p)=>count++;login.CombatSent+=Sent;try{await FocusWebInput();OnKeyDown(new KeyEventArgs(Keys.G));OnKeyUp(new KeyEventArgs(Keys.G));await Task.Delay(150);if(count!=0)throw new Exception("Web typing attacked");log.Check("web-typing-G-no-attack");ReturnGameFocus();}finally{login.CombatSent-=Sent;}}
        try{await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);charactersBox.SelectedItem=s.Character;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);await RewardScenario.Run(login,o,s,log,Select,Attack,Shot,Focus);log.Save(s.Output,"pass");}
        catch(Exception ex){NetworkExitCode=7;log.Save(s.Output,"fail",ex);}finally{login.Cancel();o.Cancel();Close();}
    }
}
