using System.Text.Json;
namespace P06;
public sealed partial class LoginConnection {
    internal static async Task MovementGateRegression(string capture,string output){
        using var c=new LoginConnection();var packets=Json.Read(capture).GetProperty("packets").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();
        string selected=LoginConnection.CapturedCharacterName(packets);void Seed(){c.World.Reset();bool started=false;foreach(var p in packets){if(p[0]==223)started=true;if(started){c.World.ProbeSent=true;c.World.Receive(p,selected);}}c.State=LoginState.InWorld;}
        Seed();var origin=c.World.Actor!;int h=Enumerable.Range(0,8).First(d=>MovementRules.Passable(origin.X,origin.Y,d));
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int admitted=0;long admittedAt=0;
        c.sendMovementGate=async(bytes,valid,admit)=>{await gate.Task;if(!valid())return false;admitted++;admittedAt=Environment.TickCount64;admit();return true;};
        var queued=c.Move(h);await Task.Delay(MovementRules.StepMs+150);
        if(c.World.PredictedSteps!=0||c.nextMovementAt!=0||admitted!=0)throw new Exception("queued move consumed animation or pace");
        gate.SetResult();if(!await queued)throw new Exception("gate admission failed");
        long remaining=c.nextMovementAt-Environment.TickCount64;
        if(remaining<MovementRules.StepMs-100||await c.Move(h))throw new Exception("delayed gate compressed movement interval");
        await Task.Delay(MovementRules.StepMs+20);if(c.MovementBusy)throw new Exception("pace did not release");
        Seed();c.ResetMovementSend();gate=new(TaskCreationOptions.RunContinuationsAsynchronously);int initial=admitted;
        queued=c.Move(h);c.CancelMovementInput();gate.SetResult();if(await queued||c.World.PredictedSteps!=0||admitted!=initial||c.MovementBusy||c.nextMovementAt!=0)throw new Exception("cancelled queued move admitted or consumed pace");
        // A correction invalidates an unadmitted move built from an older position.
        gate=new(TaskCreationOptions.RunContinuationsAsynchronously);queued=c.Move(h);var own=packets.First(p=>p[0]==87&&System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(5))==c.World.Actor!.ObjectId);c.World.Receive(own,selected);gate.SetResult();if(await queued||admitted!=initial||c.World.PredictedSteps!=0)throw new Exception("correction admitted stale base");
        // Old async failure cannot cancel a replacement connection.
        gate=new(TaskCreationOptions.RunContinuationsAsynchronously);c.sendMovementGate=async(b,v,a)=>{await gate.Task;throw new IOException("old delayed send");};queued=c.Move(h);c.Cancel();Seed();int generation=c.generation;gate.SetResult();await queued;
        if(c.generation!=generation||c.State!=LoginState.InWorld||!c.World.Ready)throw new Exception("old send damaged replacement");
        var stateDrops=new List<string>();
        foreach(string change in new[]{"haste","brave","dead"}){
            Seed();c.ResetMovementSend();gate=new(TaskCreationOptions.RunContinuationsAsynchronously);initial=admitted;
            c.sendMovementGate=async(bytes,valid,admit)=>{await gate.Task;if(!valid())return false;admitted++;admit();return true;};
            queued=c.Move(h);byte[] statusPacket=new byte[change=="dead"?16:8];
            statusPacket[0]=change=="dead"?(byte)225:change=="haste"?(byte)255:(byte)67;
            if(change=="dead")System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(statusPacket.AsSpan(5),1000);
            else{System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(statusPacket.AsSpan(1),c.World.Actor!.ObjectId);statusPacket[5]=1;}
            c.World.Receive(statusPacket,selected);gate.SetResult();
            if(await queued||admitted!=initial||c.World.PredictedSteps!=0||c.MovementBusy)throw new Exception("state change admitted queued movement: "+change);
            stateDrops.Add(change);
        }
        Json.Save(output,new{status="pass",kind="explicit synthetic delayed transport gate over real captured initialization; no real server success claimed",delayedBeforeAdmissionMs=MovementRules.StepMs+150,remainingAtAdmission=remaining,admittedAt,queuedConsumesNothing=true,cancelDropsQueued=true,correctionDropsQueued=true,oldFailureIsolated=true,stateDrops});
    }
}
