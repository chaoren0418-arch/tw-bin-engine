using System.Text.Json;
namespace P06;
public static class EquipmentScenario {
    static InventoryItem Item(LoginConnection c,int icon)=>c.World.Inventory.Single(i=>i.IconId==icon&&i.SupportedEquipment);
    public static async Task Run(LoginConnection c,LoginConnection o,RewardCase s,InventoryCapture log,Func<uint,Task> selectItem,Func<string,Task<bool>> use,Func<uint,Task> selectTarget,Func<string,Task<bool>> attack,Func<string,Task> shot,Func<Task>? focus=null){
        await ObjectTests.Wait(()=>c.World.InventoryLoaded);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>o.World.InventoryLoaded);
        if(s.Fresh&&(c.World.Inventory.Count(i=>i.SupportedEquipment)!=4||c.World.Inventory.Any(i=>i.Equipped==true)||c.World.Actor!.Weapon!=0))throw new Exception("fresh equipment fixture mismatch; do not reset");
        log.Check("equipment-initial");await shot("equipment-initial");
        async Task Toggle(int icon,bool equipped,string mode){
            await ObjectTests.Wait(()=>!c.MovementBusy);var before=Item(c,icon);if(before.Equipped==equipped)throw new Exception("unexpected saved equipment state "+icon);
            await selectItem(before.ObjectId);log.Check("before-equipment-"+mode,new{icon,equipped,before});
            if(!await use(mode))throw new Exception("equipment input rejected "+mode);
            if(await c.UseSelectedItem())throw new Exception("equipment repeated use admitted");
            await ObjectTests.Wait(()=>Item(c,icon).Equipped==equipped,5000);
            await ObjectTests.Wait(()=>!c.MovementBusy);log.Check("after-equipment-"+mode,new{icon,equipped});await shot("equipment-"+mode);
        }
        async Task Visual(int weapon){
            uint id=c.World.Actor!.ObjectId;
            await ObjectTests.Wait(()=>c.World.Actor!.Weapon==weapon&&o.World.Objects.Any(v=>v.ObjectId==id&&v.Action==weapon),5000);
            log.Check("both-peers-weapon-"+weapon);
        }
        async Task Reject(int icon,int message,string mode){
            await ObjectTests.Wait(()=>!c.MovementBusy);var before=c.World.Inventory;int ac=c.World.Status!.Ac;byte weapon=c.World.Actor!.Weapon;
            await selectItem(Item(c,icon).ObjectId);log.Check("before-original-refusal-"+message);
            if(!await use(mode))throw new Exception("refusal must reach original JAR");
            await ObjectTests.Wait(()=>c.World.LastServerMessage?.StartsWith("原服訊息 "+message)==true,5000);await ObjectTests.Wait(()=>!c.MovementBusy);
            if(before.Any(i=>c.World.Inventory.Single(j=>j.ObjectId==i.ObjectId).Equipped!=i.Equipped)||c.World.Status.Ac!=ac||c.World.Actor.Weapon!=weapon)throw new Exception("original refusal changed equipment");
            log.Check("original-refusal-"+message);await shot("equipment-refusal-"+message);
        }
        // Resumption retains all original saved state; normalize only through explicit original use requests.
        if(!s.Fresh)foreach(int icon in new[]{97,86,2,4})if(Item(c,icon).Equipped==true)await Toggle(icon,false,"game-resume-off-"+icon);
        await Toggle(2,true,"game-sword-on");await Visual(4);
        await Toggle(86,true,"web-armor-on");await Toggle(97,true,"game-shield-on");
        await Reject(4,128,"web-twohand-refused");await Toggle(97,false,"web-shield-off");
        await Toggle(2,false,"game-sword-off");await Visual(0);
        await Toggle(4,true,"web-twohand-on");await Visual(50);await Reject(97,129,"game-shield-refused");
        var origin=c.World.Actor!;int h=Enumerable.Range(0,8).First(i=>MovementRules.Passable(origin.X,origin.Y,i)&&!c.World.Objects.Any(v=>v.X==origin.X+MovementRules.Dx[i]&&v.Y==origin.Y+MovementRules.Dy[i]));
        await ObjectTests.Wait(()=>!c.MovementBusy);if(!await c.Move(h))throw new Exception("twohand original walk rejected");await Task.Delay(160);log.Check("twohand-original-walk");await shot("equipment-twohand-walk");await ObjectTests.Wait(()=>!c.MovementBusy);
        // The observer can still occupy the initial overlap cell. Continue from the actual
        // one-step destination; the later original route does not require returning there.
        await Toggle(2,true,"game-direct-switch-sword");await Visual(4);if(Item(c,4).Equipped!=false)throw new Exception("old weapon not removed by original switch");
        await Toggle(86,false,"web-armor-off");await Toggle(86,true,"game-armor-on-final");
        async Task AfterFirst(){
            await ObjectTests.Wait(()=>c.World.OwnCombat is {Action:2} hit&&Environment.TickCount64-hit.At<350,12000);log.Check("sword-original-damage");await shot("equipment-sword-damage");
            await Toggle(4,true,"web-combat-switch-twohand");await Visual(50);
            await ObjectTests.Wait(()=>c.World.OwnCombat is {Action:2} hit&&Environment.TickCount64-hit.At<350,12000);log.Check("twohand-original-damage");await shot("equipment-twohand-damage");
        }
        await RewardScenario.Run(c,o,s,log,selectTarget,attack,shot,focus,AfterFirst);
        if(Item(c,2).Equipped!=false||Item(c,4).Equipped!=true||Item(c,86).Equipped!=true||Item(c,97).Equipped!=false||c.World.Actor!.Weapon!=50)throw new Exception("equipment saved relogin mismatch");
        await Visual(50);log.Check("equipment-final-saved-relogin");await shot("equipment-relogin");
    }
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<RewardCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var log=new InventoryCapture(c,o);
        try{await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await Run(c,o,s,log,id=>{if(!c.World.SelectItem(id))throw new Exception("select item");return Task.CompletedTask;},_=>c.UseSelectedItem(),id=>{c.World.SelectTarget(id);if(c.World.TargetId!=id)throw new Exception("select target");return Task.CompletedTask;},_=>c.Attack(),_=>Task.CompletedTask);log.Save(s.Output,"pass");}
        catch(Exception ex){log.Save(s.Output,"fail",ex);throw;}finally{c.Cancel();o.Cancel();}
    }
}
