using System.Text.Json;
namespace P06;
public static class ObjectTests {
    public static async Task Wait(Func<bool> f,int ms=15000){long end=Environment.TickCount64+ms;while(!f()){if(Environment.TickCount64>end)throw new TimeoutException("object scenario wait expired");await Task.Delay(20);}}
    public static async Task Enter(LoginConnection c,string account,string password,string name){_=c.Run(42061,"p24-real-jar-d287-client2",account,password,10000);await Wait(()=>c.State is LoginState.CharacterList or LoginState.Faulted);if(c.State!=LoginState.CharacterList)throw new Exception(c.Detail);await c.SelectCharacter(name);await Wait(()=>c.State is LoginState.InWorld or LoginState.Faulted);if(c.State!=LoginState.InWorld)throw new Exception(c.Detail);}
    internal static int[] Route(LoginConnection c,int tx,int ty,HashSet<(int,int)> extra){
        var a=c.World.Actor!;var blocked=c.World.Objects.Select(o=>((int)o.X,(int)o.Y)).Concat(extra).ToHashSet();var q=new Queue<(int X,int Y,int[] Path)>();var seen=new HashSet<(int,int)>();q.Enqueue((a.X,a.Y,[]));seen.Add((a.X,a.Y));
        while(q.Count>0){var p=q.Dequeue();if(p.X==tx&&p.Y==ty)return p.Path;for(int h=0;h<8;h++){int nx=p.X+MovementRules.Dx[h],ny=p.Y+MovementRules.Dy[h];if(MovementRules.Passable(p.X,p.Y,h)&&!blocked.Contains((nx,ny))&&seen.Add((nx,ny)))q.Enqueue((nx,ny,[..p.Path,h]));}}
        throw new Exception("no legal test route");
    }
    public static async Task WalkTo(LoginConnection c,int x,int y){var avoid=new HashSet<(int,int)>();int attempts=0;while(c.World.Actor!.X!=x||c.World.Actor.Y!=y){if(++attempts>180)throw new Exception("route attempts exhausted");var path=Route(c,x,y,avoid);int h=path[0];await Wait(()=>!c.MovementBusy);var a=c.World.Actor;int corrections=c.World.Corrections;if(!await c.Move(h))throw new Exception("route local rejected");await Task.Delay(MovementRules.StepMs+80);if(c.World.Corrections>corrections)avoid.Add((a.X+MovementRules.Dx[h],a.Y+MovementRules.Dy[h]));}}
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<MovementCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var packets=new List<object>();var observerPackets=new List<object>();var sent=new List<object>();var checks=new List<object>();
        c.PacketReceived+=p=>{lock(packets)packets.Add(new{utc=DateTimeOffset.UtcNow,packet=p});};o.PacketReceived+=p=>{lock(observerPackets)observerPackets.Add(new{utc=DateTimeOffset.UtcNow,packet=p});};o.MovementSent+=p=>{lock(sent)sent.Add(p);};
        try{
            await Enter(c,s.Account,s.Password,s.Character);await WalkTo(c,32588,32930);await Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await WalkTo(o,32586,32930);await Task.Delay(1500);uint id=o.World.Actor!.ObjectId;await Wait(()=>c.World.Objects.Any(x=>x.ObjectId==id));
            var self=c.World.Actor!;var initial=c.CaptureWorld(()=>c.Snapshot());
            if(!c.World.Objects.Any(x=>x.Gfx==1307)||!c.World.Objects.Any(x=>x.Gfx==695))throw new Exception("two original NPC appearances absent");
            checks.Add(new{kind="initial-other-player-and-two-original-NPC",snapshot=initial});
            var occupied=o.World.Objects.Select(v=>((int)v.X,(int)v.Y)).ToHashSet();
            var centers=(from x in Enumerable.Range(32578,28) from y in Enumerable.Range(32919,24) where !occupied.Contains((x,y))&&Math.Abs(x-self.X)<=8&&Math.Abs(y-self.Y)<=8&&Enumerable.Range(0,8).All(h=>MovementRules.Passable(x,y,h)&&MovementRules.Passable(x+MovementRules.Dx[h],y+MovementRules.Dy[h],(h+4)%8)&&!occupied.Contains((x+MovementRules.Dx[h],y+MovementRules.Dy[h]))) orderby Math.Abs(x-32586)+Math.Abs(y-32930) select (X:x,Y:y)).ToArray();
            if(centers.Length==0)throw new Exception("no clear eight-direction test center");var center=centers[0];await WalkTo(o,center.X,center.Y);checks.Add(new{kind="normal-movement-to-clear-eight-direction-center",center,observer=o.World.Actor});
            foreach(int h in new[]{0,4,1,5,2,6,3,7}){
                await Wait(()=>!o.MovementBusy);var from=o.World.Actor!;int nx=from.X+MovementRules.Dx[h],ny=from.Y+MovementRules.Dy[h];int correction=o.World.Corrections;
                if(!await o.Move(h))throw new Exception("eight-direction move locally rejected");
                await Wait(()=>o.World.Corrections>correction||c.World.Objects.Any(v=>v.ObjectId==id&&v.X==nx&&v.Y==ny&&v.Direction==h));
                if(o.World.Corrections>correction)throw new Exception("dynamic occupancy changed at eight-direction center; original rejection retained");
                checks.Add(new{kind="other-move",heading=h,from,received=c.World.Objects.Single(x=>x.ObjectId==id)});await Task.Delay(720);await WalkTo(o,center.X,center.Y);
            }
            for(int h=7;h>=0;h--){await Wait(()=>!o.MovementBusy);if(!await o.Turn(h))throw new Exception("turn rejected");await Wait(()=>c.World.Objects.Any(v=>v.ObjectId==id&&v.Direction==h));checks.Add(new{kind="other-turn",heading=h,received=c.World.Objects.Single(x=>x.ObjectId==id)});}
            if(c.World.Actor!=self)throw new Exception("other updates moved self");
            c.World.SelectTarget(id);await WalkTo(o,32615,32930);await Wait(()=>c.World.Objects.All(x=>x.ObjectId!=id));if(c.World.TargetId!=null)throw new Exception("AOI left target");checks.Add(new{kind="original-AOI-removal",observer=o.World.Actor,main=c.Snapshot()});
            await WalkTo(o,32586,32930);await Wait(()=>c.World.Objects.Any(x=>x.ObjectId==id));c.World.SelectTarget(id);o.Cancel();await Wait(()=>c.World.Objects.All(x=>x.ObjectId!=id));if(c.World.TargetId!=null)throw new Exception("logout left target");checks.Add(new{kind="original-logout-removal",main=c.Snapshot()});
            int epoch=c.World.WorldEpoch;var endpoint=c.World.Actor;c.Cancel();if(c.World.Objects.Length!=0||c.World.TargetId!=null)throw new Exception("cancel ghost");await Task.Delay(2500);await Enter(c,s.Account,s.Password,s.Character);if(c.World.WorldEpoch<=epoch||c.World.TargetId!=null||c.World.Objects.Any(x=>x.ObjectId==id))throw new Exception("generation ghosts");checks.Add(new{kind="reconnect-clean-generation",endpoint,snapshot=c.Snapshot()});
            var delivery=c.CaptureWorld(()=>o.CaptureWorld(()=>new{status="pass",checks=checks.ToArray(),packets=packets.ToArray(),observerPackets=observerPackets.ToArray(),sent=sent.ToArray(),final=c.Snapshot(),captureBoundary=new{appliedPacketSequence=c.AppliedPacketSequence,method="main packet event + apply + snapshot/array capture share worldCaptureGate; immutable arrays serialized after release"},observerRole="synthetic test driver uses original login/movement; main registry only consumes its own original JAR wire"}));Json.Save(s.Output,delivery);
        }catch(Exception ex){Json.Save(s.Output,new{status="fail",error=ex.ToString(),checks,packets,observerPackets,sent,last=c.Snapshot(),observer=o.Snapshot()});throw;}
        finally{c.Cancel();o.Cancel();}
    }
}
