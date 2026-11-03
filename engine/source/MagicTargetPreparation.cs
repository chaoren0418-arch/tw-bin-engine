using System.Text.Json;
namespace P06;
internal sealed class MagicPreparationHealth(uint main,uint observer){
    uint lastMain=main,lastObserver=observer;
    internal bool Observe(uint main,uint observer){
        bool safe=main>=150&&observer>=150&&main>=lastMain&&observer>=lastObserver;
        if(safe){lastMain=main;lastObserver=observer;}return safe;
    }
}
// Test-only admission: a momentarily visible target cannot bypass completed precombat positioning.
internal sealed class MagicStagingGate {
    (int X,int Y,int Ox,int Oy)? plan;bool ready,failed;
    internal void Plan(int x,int y,int ox,int oy){plan=(x,y,ox,oy);ready=false;}
    internal bool Complete(int x,int y,int ox,int oy){ready=!failed&&plan==(x,y,ox,oy);return ready;}
    internal void Stop(){failed=true;ready=false;}
    internal WorldObject? Choose(WorldSession c,WorldSession o,IEnumerable<uint> pixels,uint? preferred=null){
        return ready&&!failed&&plan==(c.Actor!.X,c.Actor.Y,o.Actor!.X,o.Actor.Y)?MagicTargetPreparation.Choose(c,o,pixels,preferred):null;
    }
}
// Test controller only. The renderer, selection gates, terrain and original NPC AI are unchanged.
internal sealed class MagicTargetPreparation {
    internal const int MaxWaypoints=3,MaxMoves=60,MaxMilliseconds=45000;
    readonly LoginConnection main,observer;readonly InventoryCapture log;readonly Func<uint[]> visible;
    readonly MagicStagingGate staging=new();
    readonly HashSet<(int,int)> visited=[];int waypoints,moves;long usedMs;
    internal MagicTargetPreparation(LoginConnection c,LoginConnection o,InventoryCapture capture,Func<uint[]> pixels){main=c;observer=o;log=capture;visible=pixels;}
    internal static WorldObject? Choose(WorldSession c,WorldSession o,IEnumerable<uint> visibleIds,uint? preferred=null){
        var ids=visibleIds.ToHashSet();var a=c.Actor!;
        return c.Objects.Where(v=>v.Gfx==1110&&!v.Dead&&ids.Contains(v.ObjectId)&&Math.Max(Math.Abs(v.X-a.X),Math.Abs(v.Y-a.Y))<=10&&o.Objects.Any(z=>z.ObjectId==v.ObjectId&&!z.Dead)).OrderByDescending(v=>v.ObjectId==preferred).ThenBy(v=>v.ObjectId).FirstOrDefault();
    }
    internal WorldObject? Current(uint? preferred=null)=>staging.Choose(main.World,observer.World,visible(),preferred);
    internal static IEnumerable<(int X,int Y,int TargetX,int TargetY,double Ratio)> Plans(){
        var file=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../reference/magic/visibility-waypoints.json"));
        using var doc=JsonDocument.Parse(File.ReadAllText(file));
        foreach(var p in doc.RootElement.GetProperty("candidates").EnumerateArray()){
            double ratio=p.GetProperty("minimumTargetRatio").GetDouble();int tx=p.GetProperty("targetX").GetInt32(),ty=p.GetProperty("targetY").GetInt32();
            if(ratio>0&&(tx-32598)*(tx-32598)+(ty-32948)*(ty-32948)<=4)
                yield return(p.GetProperty("ownX").GetInt32(),p.GetProperty("ownY").GetInt32(),tx,ty,ratio);
        }
    }
    internal async Task<WorldObject> Prepare(uint? preferred=null){
        if(Current(preferred) is {} current)return current;
        long started=Environment.TickCount64;var health=new MagicPreparationHealth(main.World.Status!.Hp,observer.World.Status!.Hp);
        void Guard(){
            if(main.State!=LoginState.InWorld||observer.State!=LoginState.InWorld||main.World.OwnDead||observer.World.OwnDead||!health.Observe(main.World.Status!.Hp,observer.World.Status!.Hp)||usedMs+Environment.TickCount64-started>=MaxMilliseconds)
                throw new Exception("finite visible-target preparation stopped on actual damage/state/deadline");
        }
        async Task Wait(Func<bool> ready,int milliseconds){long end=Environment.TickCount64+Math.Min(milliseconds,2000);while(true){Guard();if(ready())return;if(Environment.TickCount64>=end)throw new Exception("finite target preparation movement did not settle");await Task.Delay(50);}}
        async Task Walk(LoginConnection c,int x,int y,HashSet<(int,int)> avoid){
            while(c.World.Actor!.X!=x||c.World.Actor.Y!=y){
                Guard();await Wait(()=>!c.MovementBusy,2000);
                if(moves>=MaxMoves)throw new Exception("finite target preparation sixty-move limit");
                var route=ObjectTests.Route(c,x,y,avoid);int h=route[0];var a=c.World.Actor;int corrections=c.World.Corrections;
                Guard();if(!await c.Move(h))throw new Exception("normal target preparation move rejected");moves++;
                log.Check("visible-target-normal-move",new{role=c==main?"main":"observer",moves,from=a,heading=h,waypoints});
                long settled=Environment.TickCount64+MovementRules.StepMs+80;await Wait(()=>Environment.TickCount64>=settled&&!c.MovementBusy,2000);
                if(c.World.Corrections>corrections)avoid.Add((a.X+MovementRules.Dx[h],a.Y+MovementRules.Dy[h]));
            }
        }
        try{
            Guard();log.Check("visible-target-preparation-start",new{waypoints,moves,usedMs,maximumWaypoints=MaxWaypoints,maximumMoves=MaxMoves,maximumMs=MaxMilliseconds,main=main.World.Actor,observer=observer.World.Actor});
            while(waypoints<MaxWaypoints){
                Guard();if(Current(preferred) is {} found)return found;
                // Original-map pose visibility only ranks potential lure positions; it never predicts an NPC move.
                var choices=new List<(int X,int Y,int Ox,int Oy,int Steps,double Ratio,int Tx,int Ty)>();
                foreach(var p in Plans().Where(p=>!visited.Contains((p.X,p.Y))).DistinctBy(p=>(p.X,p.Y))){
                    try{
                        var route=ObjectTests.Route(main,p.X,p.Y,[]);
                        var follow=(from dx in Enumerable.Range(-2,5) from dy in Enumerable.Range(-2,5) where Math.Max(Math.Abs(dx),Math.Abs(dy))==2 select(X:p.X+dx,Y:p.Y+dy)).Where(q=>(q.X,q.Y)!=(p.TargetX,p.TargetY)&&Coordinates.Inside(q.X,q.Y)).Select(q=>{try{return(q.X,q.Y,N:ObjectTests.Route(observer,q.X,q.Y,[(p.X,p.Y)]).Length);}catch{return(q.X,q.Y,N:999);}}).OrderBy(q=>q.N).First();
                        int steps=route.Length+follow.N;if(steps<=MaxMoves-moves)choices.Add((p.X,p.Y,follow.X,follow.Y,steps,p.Ratio,p.TargetX,p.TargetY));
                    }catch(Exception ex) when(ex.Message=="no legal test route"){}
                }
                var plan=choices.OrderByDescending(p=>p.Ratio).ThenBy(p=>p.Steps).FirstOrDefault();if(plan==default)throw new Exception("no finite reachable original visibility waypoint");
                visited.Add((plan.X,plan.Y));waypoints++;log.Check("visible-target-waypoint-plan",new{waypoints,mainX=plan.X,mainY=plan.Y,observerX=plan.Ox,observerY=plan.Oy,steps=plan.Steps,ratio=plan.Ratio,targetX=plan.Tx,targetY=plan.Ty,meaning="original terrain routes and synthetic original-pose visibility; NPC movement is not promised"});
                staging.Plan(plan.X,plan.Y,plan.Ox,plan.Oy);
                // Finish the main actor's source-backed location first, before any initial combat bolt.
                // Do not stop for temporary pixels en route, or leave an attacked main waiting for observer moves.
                await Walk(main,plan.X,plan.Y,[]);await Walk(observer,plan.Ox,plan.Oy,[(plan.X,plan.Y)]);
                await Wait(()=>!main.MovementBusy&&!observer.MovementBusy&&main.World.Objects.Any(v=>v.ObjectId==observer.World.Actor!.ObjectId&&v.X==plan.Ox&&v.Y==plan.Oy)&&observer.World.Objects.Any(v=>v.ObjectId==main.World.Actor!.ObjectId&&v.X==plan.X&&v.Y==plan.Y),2000);
                Guard();if(!staging.Complete(main.World.Actor!.X,main.World.Actor.Y,observer.World.Actor!.X,observer.World.Actor.Y))throw new Exception("original actor positions differ from completed staging plan");
                log.Check("visible-target-staging-complete",new{waypoints,moves,main=main.World.Actor,observer=observer.World.Actor,mainX=plan.X,mainY=plan.Y,observerX=plan.Ox,observerY=plan.Oy,mutualOriginalPeerPositions=true,order="main then observer; no temporary visibility shortcut"});
                long end=Environment.TickCount64+2000;
                while(true){Guard();if(Current(preferred) is {} candidate){log.Check("visible-target-preparation-ready",new{candidate,main=main.World.Actor,observer=observer.World.Actor,stagingCompleted=true,actualPositivePixels=true,waypoints,moves,elapsedMs=usedMs+Environment.TickCount64-started});return candidate;}if(Environment.TickCount64>=end)break;await Task.Delay(50);}
            }
            throw new Exception("finite visible-target preparation exhausted three waypoints");
        }catch{staging.Stop();throw;}finally{usedMs+=Environment.TickCount64-started;log.Check("visible-target-preparation-end",new{waypoints,moves,usedMs});}
    }
}
