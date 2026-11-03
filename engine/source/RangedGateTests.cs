using System.Buffers.Binary;
namespace P06;
public sealed partial class LoginConnection {
    internal static async Task RangedGateTests(string output){
        using var c=new LoginConnection();var checks=new List<string>();string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));
        var rows=Json.Read(Path.Combine(root,"fixtures/p17-inventory-generation1.json")).GetProperty("packets").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();string selected=CapturedCharacterName(rows);
        var npc=rows.First(p=>p[0]==87&&BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(9))==1110);uint id=BinaryPrimitives.ReadUInt32LittleEndian(npc.AsSpan(5));
        byte[] Bow(uint oid,byte weapon)=>LoginPackets.Pad([119,..BitConverter.GetBytes(oid),weapon]);
        void Seed(){c.Cancel();bool entered=false;foreach(var b in rows){if(b[0]==223)entered=true;if(entered){c.World.ProbeSent=true;c.World.Receive(b,selected);if(c.World.Ready)break;}}c.State=LoginState.InWorld;c.World.Receive(npc,selected);c.World.Receive(Bow(c.World.Actor!.ObjectId,20),selected);c.World.Receive(Convert.FromHexString(File.ReadLines(Path.Combine(root,"reference/ranged/ammo.tsv")).First().Split('\t')[1]),selected);c.World.SelectTarget(id);}
        foreach(string change in new[]{"input","selection","target-move","remove","map","death","generation","weapon"}){
            Seed();var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int sent=0;
            c.sendMovementGate=async(b,v,a)=>{await gate.Task;if(!v())return false;if(b[0]!=123)throw new Exception("bow must encode C123");sent++;a();return true;};var pending=c.Attack();
            if(c.World.OwnCombat!=null||RangedScenario.Arrows(c)!=80)throw new Exception("queued attack predicted ammo/animation");
            switch(change){case "input":c.CancelMovementInput();break;case "selection":c.World.SelectTarget(null);c.World.SelectTarget(id);break;case "target-move":var moved=(byte[])npc.Clone();BinaryPrimitives.WriteUInt16LittleEndian(moved.AsSpan(1),(ushort)(c.World.Target!.X+1));c.World.Receive(moved,selected);break;case "remove":c.World.Receive(LoginPackets.Pad([120,..BitConverter.GetBytes(id)]),selected);break;case "map":c.World.Receive(Convert.FromHexString("CE00000000000000"),selected);break;case "death":c.World.Receive(LoginPackets.Pad([158,..BitConverter.GetBytes(c.World.Actor!.ObjectId),8]),selected);break;case "generation":Seed();break;case "weapon":c.World.Receive(Bow(c.World.Actor!.ObjectId,4),selected);break;}
            gate.SetResult();if(await pending||sent!=0)throw new Exception("stale ranged gate "+change);checks.Add("C123 delayed gate cancels "+change);
        }
        Seed();int sends=0;c.sendMovementGate=(b,v,a)=>{if(!v())return Task.FromResult(false);if(b[0]!=123)throw new Exception("wrong ranged opcode");sends++;a();return Task.FromResult(true);};
        if(!await c.Attack()||await c.Attack()||sends!=1||RangedScenario.Arrows(c)!=80)throw new Exception("explicit ranged one-shot/no predicted consumption");
        checks.Add("one explicit C123; rapid duplicate rejected; count remains80 until original inventory packet");
        c.World.Receive(Convert.FromHexString(File.ReadLines(Path.Combine(root,"reference/ranged/ammo.tsv")).Skip(1).First().Split('\t')[1]),selected);if(RangedScenario.Arrows(c)!=79)throw new Exception("original absolute consumption");
        Json.Save(output,new{status="pass",kind="synthetic delayed gates using original captured initialization and original serializer vectors; no live refusal claim",checks});
    }
}
