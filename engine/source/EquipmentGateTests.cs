namespace P06;
public sealed partial class LoginConnection {
    internal static async Task EquipmentGateTests(string output){
        using var c=new LoginConnection();var checks=new List<string>();
        var rows=Json.Read(Path.Combine(AppContext.BaseDirectory,"../../fixtures/combat-initialization.json")).GetProperty("rows").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();string selected=CapturedCharacterName(rows);
        void Feed(byte[] p)=>c.World.Receive(p,selected);
        void Seed(){
            c.Cancel();bool world=false;foreach(var p in rows){if(p[0]==223)world=true;if(world){c.World.ProbeSent=true;Feed(p);}}Feed(LoginPackets.Pad([123,1,0,0,0]));c.State=LoginState.InWorld;
            byte[] record=[..BitConverter.GetBytes(100u),0,0,1,0,2,0,1,..BitConverter.GetBytes(1u),1,..LoginPackets.TextEncoding.GetBytes("+0 $0"),0,0,10,0,0,0,0,0,0,0,0,0,0];
            Feed(LoginPackets.Pad([5,1,..record]));c.World.SelectItem(100);
        }
        foreach(string change in new[]{"input","selection","name-state","remove","map","death","generation"}){
            Seed();if(!c.ItemAvailable||c.World.SelectedItem!.Equipped!=false)throw new Exception("equipment gate fixture");int sent=0;var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            c.sendMovementGate=async(b,v,a)=>{await gate.Task;if(!v())return false;sent++;a();return true;};var task=c.UseSelectedItem();int ac=c.World.Status!.Ac;
            if(c.World.SelectedItem.Equipped!=false||c.World.Actor!.Weapon!=0)throw new Exception("optimistic equipment");
            switch(change){case "input":c.CancelMovementInput();break;case "selection":c.World.SelectItem(null);c.World.SelectItem(100);break;case "name-state":Feed(LoginPackets.Pad([100,..BitConverter.GetBytes(100u),..LoginPackets.TextEncoding.GetBytes("+0 $0 ($9)"),0]));break;case "remove":Feed(LoginPackets.Pad([57,..BitConverter.GetBytes(100u)]));break;case "map":Feed(Convert.FromHexString("CE00000000000000"));break;case "death":Feed(LoginPackets.Pad([158,..BitConverter.GetBytes(c.World.Actor!.ObjectId),8]));break;case "generation":Seed();break;}
            gate.SetResult();if(await task||sent!=0||c.World.Status!.Ac!=ac)throw new Exception("stale equipment admitted "+change);checks.Add("drops pending equipment after "+change);
        }
        Seed();int admitted=0;c.sendMovementGate=(b,v,a)=>{if(!v())return Task.FromResult(false);admitted++;a();return Task.FromResult(true);};
        c.nextCombatAt=Environment.TickCount64+2000;if(await c.UseSelectedItem())throw new Exception("combat busy equipment");c.nextCombatAt=0;c.nextMovementAt=Environment.TickCount64+2000;if(await c.UseSelectedItem())throw new Exception("movement busy equipment");c.nextMovementAt=0;
        if(!await c.UseSelectedItem()||await c.UseSelectedItem()||admitted!=1||c.World.SelectedItem!.Equipped!=false||c.World.Actor!.Weapon!=0)throw new Exception("single equipment semantics");
        checks.Add("busy rejected; rapid duplicate rejected; one original164 request; no predicted equip or attributes");c.Cancel();if(c.ItemBusy||c.World.EquipmentSlots.Length!=0)throw new Exception("cancel retains equipment");
        Json.Save(output,new{status="pass",kind="explicit synthetic equipment and delayed send gate; not live refusal proof",checks});
    }
}
