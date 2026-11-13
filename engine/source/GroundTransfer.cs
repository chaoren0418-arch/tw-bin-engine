using System.Text.Json;
namespace P06;
public static class GroundTransfer {
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<GroundCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var log=new InventoryCapture(c,o);
        try{
            await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>c.World.InventoryLoaded&&o.World.InventoryLoaded);
            if(GroundScenario.Meat(c)!=8||GroundScenario.Meat(o)!=0)throw new Exception("finite transfer must be existing8 to empty recipient");log.Check("finite-transfer-initial");
            await ObjectTests.WalkTo(o,32583,32930);await ObjectTests.WalkTo(c,32582,32930);await ObjectTests.Wait(()=>!c.MovementBusy&&!o.MovementBusy);
            c.World.SelectItem(c.World.Inventory.Single(GroundScenario.ScenarioItem).ObjectId);if(!await c.DropSelected(8))throw new Exception("finite transfer original drop");
            await ObjectTests.Wait(()=>GroundScenario.Meat(c)==0&&c.World.Ground.Any(g=>g.Count==8)&&o.World.Ground.Any(g=>g.Count==8));var ground=c.World.Ground.Single(g=>g.Count==8);if(!o.World.Ground.Any(g=>g.ObjectId==ground.ObjectId))throw new Exception("recipient lacks own ground packet");log.Check("finite-transfer-ground-eight");
            o.World.SelectGround(ground.ObjectId);if(!await o.PickupSelected(8))throw new Exception("finite transfer original pickup");await ObjectTests.Wait(()=>GroundScenario.Meat(o)==8&&c.World.Ground.All(g=>g.ObjectId!=ground.ObjectId)&&o.World.Ground.All(g=>g.ObjectId!=ground.ObjectId));await ObjectTests.Wait(()=>o.World.Actor!.Direction==6&&c.World.Objects.Any(v=>v.ObjectId==o.World.Actor.ObjectId&&v.Direction==6));log.Check("finite-transfer-received-eight");
            c.Cancel();o.Cancel();await Task.Delay(3000);await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>c.World.InventoryLoaded&&o.World.InventoryLoaded);
            if(GroundScenario.Meat(c)!=0||GroundScenario.Meat(o)!=8||c.World.Ground.Length!=0||o.World.Ground.Length!=0)throw new Exception("finite transfer normal saved relogin");log.Check("finite-transfer-normal-relogin");log.Save(s.Output,"pass");
        }catch(Exception ex){log.Save(s.Output,"fail",ex);throw;}finally{c.Cancel();o.Cancel();}
    }
}
