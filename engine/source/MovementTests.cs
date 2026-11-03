using System.Text.Json;
using System.Buffers.Binary;
namespace P06;
public sealed record MovementCase(string Account,string Password,string Character,string ObserverAccount,string ObserverCharacter,string Output,int[] Route);
public static class MovementTests {
    internal static int[] PathTo(int x,int y,int tx,int ty){
        var queue=new Queue<(int X,int Y,int[] Path)>();var seen=new HashSet<(int,int)>();queue.Enqueue((x,y,[]));seen.Add((x,y));
        while(queue.Count>0){var a=queue.Dequeue();if(a.X==tx&&a.Y==ty)return a.Path;for(int h=0;h<8;h++){int nx=a.X+MovementRules.Dx[h],ny=a.Y+MovementRules.Dy[h];if(nx==32586&&ny==32930)continue;if(MovementRules.Passable(a.X,a.Y,h)&&seen.Add((nx,ny)))queue.Enqueue((nx,ny,[..a.Path,h]));}}
        throw new Exception("no static test route to center");
    }
    static async Task Wait(Func<bool> f,int ms=15000){long end=Environment.TickCount64+ms;while(!f()){if(Environment.TickCount64>end)throw new TimeoutException();await Task.Delay(10);}}
    static async Task Enter(LoginConnection c,string account,string password,string name){_=c.Run(42061,"p24-real-jar-d287-client2",account,password,10000);await Wait(()=>c.State is LoginState.CharacterList or LoginState.Faulted);if(c.State!=LoginState.CharacterList)throw new Exception(c.Detail);await c.SelectCharacter(name);await Wait(()=>c.State is LoginState.InWorld or LoginState.Faulted);if(c.State!=LoginState.InWorld)throw new Exception(c.Detail);}
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<MovementCase>(File.ReadAllText(path))!;
        using var c=new LoginConnection();using var o=new LoginConnection();var mainPackets=new List<object>();var observerPackets=new List<object>();var sent=new List<object>();var observerSent=new List<object>();var checks=new List<object>();
        var moves=new List<(uint Id,int X,int Y,int H)>();var turns=new List<(uint Id,int H)>();
        c.PacketReceived+=p=>{lock(mainPackets)mainPackets.Add(new{utc=DateTimeOffset.UtcNow,packet=p});};
        c.MovementSent+=p=>{lock(sent)sent.Add(p);};
        o.MovementSent+=p=>{lock(observerSent)observerSent.Add(p);};
        o.PacketReceived+=p=>{lock(observerPackets)observerPackets.Add(new{utc=DateTimeOffset.UtcNow,packet=p});var j=JsonSerializer.SerializeToElement(p);if(!j.TryGetProperty("plainHex",out var h))return;byte[] b=Convert.FromHexString(h.GetString()!);if(b[0]==10){if(b.Length!=16||b[10]!=128)throw new Exception("observer move format");lock(moves)moves.Add((BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(1)),BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(5)),BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(7)),b[9]));}if(b[0]==122){lock(turns)turns.Add((BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(1)),b[5]));}};
        object? start=null,finish=null,relogin=null;try{
            await Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await Enter(c,s.Account,s.Password,s.Character);await Task.Delay(1200);start=c.Snapshot();var id=c.World.Actor!.ObjectId;
            var persistedStart=new{main=c.World.Actor,observer=o.World.Actor};uint observerId=o.World.Actor!.ObjectId;
            // Setup is normal C_Move, not an observed test step: the saved roles may initially be outside each other's AOI.
            await ObjectTests.WalkTo(o,32586,32930);await ObjectTests.WalkTo(c,32588,32930);
            await Wait(()=>!c.MovementBusy&&!o.MovementBusy);
            // A prediction is not an ACK. Both endpoints must also be present in the opposite connection's original JAR object packets.
            await Wait(()=>c.World.Objects.Any(v=>v.ObjectId==observerId&&v.X==32586&&v.Y==32930)&&o.World.Objects.Any(v=>v.ObjectId==id&&v.X==32588&&v.Y==32930));
            var setup=c.CaptureWorld(()=>o.CaptureWorld(()=>new{kind="normal-C_Move-setup-with-mutual-original-wire-visibility",persistedStart,main=c.World.Actor,observer=o.World.Actor,mainViewOfObserver=c.World.Objects.Single(v=>v.ObjectId==observerId),observerViewOfMain=o.World.Objects.Single(v=>v.ObjectId==id),mainAppliedSequence=c.AppliedPacketSequence,observerAppliedSequence=o.AppliedPacketSequence,mainSetupSentCount=sent.Count,observerSetupSentCount=observerSent.Count,mainSetupPacketCount=mainPackets.Count,observerSetupPacketCount=observerPackets.Count}));checks.Add(setup);
            Json.Save(s.Output+".ready.json",new{main=c.Snapshot(),observer=o.Snapshot(),setup});
            foreach(int h in s.Route){
                await Wait(()=>!c.MovementBusy);var a=c.World.Actor!;int n;lock(moves)n=moves.Count;
                if(!await c.Move(h))throw new Exception("local route rejected "+a.X+","+a.Y+" h"+h+" "+c.World.MotionNotice);
                // Independent oracle only reads original-JAR broadcasts; never writes into product state.
                await Wait(()=>{lock(moves)return moves.Skip(n).Any(v=>v.Id==id&&v.X==a.X&&v.Y==a.Y&&v.H==h);});
                await Wait(()=>!c.MovementBusy);var b=c.World.Actor!;if(b.X!=a.X+MovementRules.Dx[h]||b.Y!=a.Y+MovementRules.Dy[h])throw new Exception("prediction diverged");
                checks.Add(new{kind="accepted-move",heading=h,sourceX=a.X,sourceY=a.Y,targetX=b.X,targetY=b.Y,oracle="original JAR observer 0x0A source+heading"});
            }
            // A real nearby observer occupies a statically walkable tile: normal product Move must be corrected.
            await Wait(()=>!c.MovementBusy);var approach=c.World.Actor!;int mn;lock(moves)mn=moves.Count;
            if(!await c.Move(6))throw new Exception("block approach failed");await Wait(()=>{lock(moves)return moves.Skip(mn).Any(v=>v.Id==id&&v.X==approach.X&&v.Y==approach.Y&&v.H==6);});
            await Wait(()=>!c.MovementBusy);var blockedBase=c.World.Actor!;int cn=c.World.Corrections;
            if(!await c.Move(6))throw new Exception("expected statically legal occupied tile");await Wait(()=>c.World.Corrections>cn);
            if(c.World.Actor!.X!=blockedBase.X||c.World.Actor.Y!=blockedBase.Y)throw new Exception("living blocker did not converge");
            checks.Add(new{kind="real-living-block",source=blockedBase,observer=o.World.Actor,corrected=c.World.Actor});
            await Wait(()=>!c.MovementBusy);mn=moves.Count;if(!await c.Move(2))throw new Exception("block recovery failed");await Wait(()=>{lock(moves)return moves.Skip(mn).Any(v=>v.Id==id&&v.X==blockedBase.X&&v.Y==blockedBase.Y&&v.H==2);});
            for(int h=0;h<8;h++){
                await Wait(()=>!c.MovementBusy);int n;lock(turns)n=turns.Count;if(!await c.Turn(h))throw new Exception("turn rejected locally");int expected=h;
                await Wait(()=>{lock(turns)return turns.Skip(n).Any(v=>v.Id==id&&v.H==expected);});checks.Add(new{kind="accepted-turn",heading=h});
            }
            await Wait(()=>!c.MovementBusy);var before=c.World.Actor!;int corrections=c.World.Corrections;
            if(!await c.DriftProbe(2))throw new Exception("negative seam failed");
            await Wait(()=>c.World.Corrections>corrections);
            var corrected=c.World.Actor!;if(corrected.X!=before.X||corrected.Y!=before.Y||corrected.Direction!=before.Direction)throw new Exception("correction did not converge");
            checks.Add(new{kind="real-server-drift-rejection",before,corrected,corrections=c.World.Corrections});
            await Wait(()=>!c.MovementBusy);int recovery=Enumerable.Range(0,8).First(h=>MovementRules.Passable(corrected.X,corrected.Y,h));int count;lock(moves)count=moves.Count;
            if(!await c.Move(recovery))throw new Exception("recovery failed locally");await Wait(()=>{lock(moves)return moves.Skip(count).Any(v=>v.Id==id&&v.X==corrected.X&&v.Y==corrected.Y&&v.H==recovery);});await Wait(()=>!c.MovementBusy);
            // Rapid repeated input is discarded by pacing; no queued motion survives cancellation.
            bool a1=await c.Turn(5);bool a2=await c.Turn(6);bool a3=await c.Move(0);if(!a1||a2||a3)throw new Exception("repeat pacing failed");await Task.Delay(700);
            finish=c.Snapshot();var endpoint=c.World.Actor!;c.Cancel();await Task.Delay(2500);await Enter(c,s.Account,s.Password,s.Character);relogin=c.Snapshot();
            if(c.World.Actor!.X!=endpoint.X||c.World.Actor.Y!=endpoint.Y||c.World.Actor.Direction!=endpoint.Direction)throw new Exception("relogin endpoint mismatch");
            checks.Add(new{kind="socket-close-save-and-relogin",endpoint,received=c.World.Actor});
            c.Cancel();o.Cancel();await Task.Delay(500);Json.Save(s.Output,new{status="pass",start,finish,relogin,checks,mainPackets,observerPackets,sent,observerSent,observerRole="normal C_Move setup followed by original-JAR visibility-gated independent observer; no injection into main client"});
        }catch(Exception ex){Json.Save(s.Output,new{status="fail",error=ex.ToString(),start,finish,relogin,last=c.Snapshot(),checks,mainPackets,observerPackets,sent,observerSent});throw;}
        finally{c.Cancel();o.Cancel();}
    }
}
