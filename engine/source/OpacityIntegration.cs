using System.Text.Json;
namespace P06;
public sealed record OpacityCase(string Account,string Password,string Character,string ObserverAccount,string ObserverCharacter,string Output,bool Fresh=true,string? RoutePath=null);
public sealed class OpacitySafety:IDisposable {
    readonly LoginConnection c,o;uint hp,ohp;readonly long started=Environment.TickCount64;public int Moves {get;private set;}
    bool damaged;void Vitals(){if(c.World.Status is {} a){if(a.Hp<hp)damaged=true;hp=Math.Max(hp,a.Hp);}if(o.World.Status is {} b){if(b.Hp<ohp)damaged=true;ohp=Math.Max(ohp,b.Hp);}}
    public OpacitySafety(LoginConnection main,LoginConnection observer){c=main;o=observer;hp=c.World.Status?.Hp??0;ohp=o.World.Status?.Hp??0;c.Changed+=Vitals;o.Changed+=Vitals;}
    public void Dispose(){c.Changed-=Vitals;o.Changed-=Vitals;}
    public void Check(){Vitals();if(damaged||c.State!=LoginState.InWorld||o.State!=LoginState.InWorld||c.World.OwnDead||o.World.OwnDead||c.World.Status!.Hp<150||o.World.Status!.Hp<150||Environment.TickCount64-started>90000)throw new Exception("finite noncombat cache stop: observed HP decrease/death/disconnect/lowHP/time");}
    public void Step(){Check();if(++Moves>50)throw new Exception("finite opacity movement limit");}
    public async Task Wait(Func<bool> ready,string why,int ms=2000){long until=Environment.TickCount64+ms;while(true){Check();if(ready())return;if(Environment.TickCount64>=until)throw new TimeoutException("finite opacity wait: "+why);await Task.Delay(20);}}
}
public static class OpacityScenario {
    public static void VerifyStationaryObserver(SceneOpacity state,OriginalMapCache cache){
        var reference=Json.Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../reference/cache/observer-recovery/observer-native-settled.json")));var expected=reference.GetProperty("snapshot");var actual=JsonSerializer.SerializeToElement(state.Snapshot());
        foreach(string key in new[]{"states","groups","timers","position"}){var a=actual.GetProperty(key).EnumerateArray().Select(v=>JsonSerializer.Serialize(v));var e=expected.GetProperty(key).EnumerateArray().Select(v=>JsonSerializer.Serialize(v));if(key is "states" or "groups"){a=a.Order();e=e.Order();}if(!a.SequenceEqual(e))throw new Exception("stationary observer differs from own original kernel: "+key);}
        if(!new[]{cache.OriginX,cache.OriginY}.SequenceEqual(reference.A("origin")))throw new Exception("stationary observer original cache origin");
    }

    public static async Task Enter(LoginConnection c,string account,string password,string name){_=c.Run(42061,"p24-real-jar-d287-client2",account,password,2000);await ObjectTests.Wait(()=>c.State is LoginState.CharacterList or LoginState.Faulted,2000);if(c.State!=LoginState.CharacterList)throw new Exception(c.Detail);await c.SelectCharacter(name);await ObjectTests.Wait(()=>c.State is LoginState.InWorld or LoginState.Faulted,2000);if(c.State!=LoginState.InWorld)throw new Exception(c.Detail);}

    public static async Task Run(LoginConnection c,LoginConnection o,OpacityCase spec,InventoryCapture log,Func<int,string,OpacitySafety,Task> move,Func<string,Task> shot,Func<uint,string,OpacitySafety,Task>? select=null){
        using var guard=new OpacitySafety(c,o);
        await ObjectTests.Wait(()=>c.World.InventoryLoaded&&c.World.MagicListReceived,2000);await Enter(o,spec.ObserverAccount,spec.Password,spec.ObserverCharacter);await ObjectTests.Wait(()=>o.World.InventoryLoaded,2000);
        uint self=c.World.Actor!.ObjectId,observer=o.World.Actor!.ObjectId;var observerStart=o.World.Actor;var initialItems=c.World.Inventory;var initialXp=c.World.Experience!.Total;int initialEpoch=c.World.WorldEpoch;
        var plan=Json.Read(spec.RoutePath??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../reference/opacity/live-route-plan.json")));
        var initial=plan.A("initial");if(c.World.Actor.X!=initial[0]||c.World.Actor.Y!=initial[1])throw new Exception("plan initial saved state mismatch");
        var expectedObserver=plan.A("observerStationary");if(observerStart!.X!=expectedObserver[0]||observerStart.Y!=expectedObserver[1])throw new Exception("approved stationary observer mismatch");
        if(!plan.GetProperty("initialMutualVisibility").GetBoolean()){
            if(spec.Fresh||!plan.TryGetProperty("prelude",out var prelude)||prelude.GetArrayLength()!=1)throw new Exception("only explicit saved-state one-step original visibility reentry allowed");
            var step=prelude[0];var from=step.A("source");var to=step.A("destination");int h=step.I("heading");guard.Step();
            if(c.World.Actor.X!=from[0]||c.World.Actor.Y!=from[1]||!(c.World.MapCache?.Passable(from[0],from[1],h)??MovementRules.Passable(from[0],from[1],h))||c.World.Objects.Any(v=>!v.Dead&&v.X==to[0]&&v.Y==to[1]))throw new Exception("visibility reentry terrain/occupancy changed");
            int before=c.World.PredictedSteps,corrections=c.World.Corrections;log.Check("opacity-before-move",new{leg=-1,mode="native",h,from,to,kind="explicit one-step saved-state reentry; peer original S87 or S10 required"});await move(h,"native",guard);
            await guard.Wait(()=>c.World.PredictedSteps==before+1&&o.World.Objects.Any(v=>v.ObjectId==self&&v.X==to[0]&&v.Y==to[1]&&v.Direction==h),"original peer reappearance packet");
            if(c.World.Corrections!=corrections)throw new Exception("visibility reentry correction");log.Check("opacity-original-peer-position",new{leg=-1,mode="native",h,to,predictionIsNotAck=true,peer=o.World.Objects.Single(v=>v.ObjectId==self)});
            await guard.Wait(()=>!c.MovementBusy,"visibility reentry cadence");
        }
        await guard.Wait(()=>o.World.Objects.Any(v=>v.ObjectId==self)&&c.World.Objects.Any(v=>v.ObjectId==observer),"mutual original packets");
        int forbidden=0;void Sent(object p){var j=JsonSerializer.SerializeToElement(p);if(j.TryGetProperty("plainHex",out var h)&&Convert.FromHexString(h.GetString()!)[0] is 6 or 164 or 229 or 123)forbidden++;}
        c.WorldPacketSent+=Sent;o.WorldPacketSent+=Sent;
        try{
            await guard.Wait(()=>{o.World.AdvanceScene();return JsonSerializer.SerializeToElement(o.World.Opacity!.Snapshot()).GetProperty("timers").GetArrayLength()==0;},"own original stationary observer settle");VerifyStationaryObserver(o.World.Opacity!,o.World.MapCache!);
            log.Check("opacity-initial-both-peers",new{spec.Fresh,observerStationary=true});await shot("00-initial");
            int leg=0;
            foreach(var route in plan.GetProperty("legs").EnumerateArray()){
                string mode=leg%2==0?"native":"web";var start=route.A("start");if(c.World.Actor.X!=start[0]||c.World.Actor.Y!=start[1])throw new Exception("route requires exact current saved start; no teleport/reset");
                foreach(var step in route.GetProperty("steps").EnumerateArray()){
                    guard.Step();await guard.Wait(()=>!c.MovementBusy,"before next step");int h=step.I("heading");var from=step.A("source");var to=step.A("destination");
                    if(c.World.Actor.X!=from[0]||c.World.Actor.Y!=from[1]||!(c.World.MapCache?.Passable(from[0],from[1],h)??MovementRules.Passable(from[0],from[1],h))||c.World.Objects.Any(v=>!v.Dead&&v.X==to[0]&&v.Y==to[1]))throw new Exception("original terrain/current occupancy changed; preserve stop");
                    int before=c.World.PredictedSteps,corrections=c.World.Corrections;log.Check("opacity-before-move",new{leg,mode,h,from,to});await move(h,mode,guard);
                    await guard.Wait(()=>c.World.PredictedSteps==before+1,"one admitted step");
                    await guard.Wait(()=>o.World.Objects.Any(v=>v.ObjectId==self&&v.X==to[0]&&v.Y==to[1]&&v.Direction==h),"original observer position packet");
                    if(c.World.Corrections!=corrections||c.World.Actor.X!=to[0]||c.World.Actor.Y!=to[1])throw new Exception("original position correction; preserve failure");
                    log.Check("opacity-original-peer-position",new{leg,mode,h,to,predictionIsNotAck=true,peer=o.World.Objects.Single(v=>v.ObjectId==self)});
                    await guard.Wait(()=>!c.MovementBusy,"settled original cadence");
                    c.World.AdvanceScene();o.World.AdvanceScene();await shot($"step-{guard.Moves:00}");guard.Check();
                }
                c.World.AdvanceScene();o.World.AdvanceScene();log.Check("opacity-leg-settled",new{leg,mode,sourceTriggers=route.GetProperty("triggers").Clone()});await shot($"{leg+1:00}-leg-{leg}");
                if(leg==(plan.TryGetProperty("selectLeg",out var selectLeg)?selectLeg.GetInt32():0)&&select!=null){await select(observer,"native",guard);await select(observer,"web",guard);log.Check("opacity-both-real-visible-selections",new{observer,forbidden});}
                if(o.World.Actor!.X!=observerStart.X||o.World.Actor.Y!=observerStart.Y||o.World.PredictedSteps!=0)throw new Exception("stationary observer changed");
                VerifyStationaryObserver(o.World.Opacity!,o.World.MapCache!);
                if(forbidden!=0)throw new Exception("pure movement/selection emitted action packet");leg++;
            }
            var endpoint=c.World.Actor;var endStatus=c.World.Status;log.Check("opacity-before-normal-logout",new{moves=guard.Moves,forbidden});c.Cancel();o.Cancel();if(c.World.Opacity!=null||o.World.Opacity!=null)throw new Exception("cancel retained scene state");await Task.Delay(3000);
            await Enter(c,spec.Account,spec.Password,spec.Character);await Enter(o,spec.ObserverAccount,spec.Password,spec.ObserverCharacter);await ObjectTests.Wait(()=>c.World.InventoryLoaded&&o.World.InventoryLoaded,2000);await Task.Delay(550);c.World.AdvanceScene();o.World.AdvanceScene();
            if(c.World.WorldEpoch<=initialEpoch||c.World.Actor!.X!=endpoint.X||c.World.Actor.Y!=endpoint.Y||c.World.TargetId!=null||o.World.Actor!.X!=observerStart.X||o.World.Actor.Y!=observerStart.Y||c.World.Experience!.Total!=initialXp||c.World.Inventory.Length!=initialItems.Length||initialItems.Any(i=>!c.World.Inventory.Any(v=>v.ObjectId==i.ObjectId&&v.Count==i.Count&&v.Equipped==i.Equipped))||forbidden!=0)throw new Exception("normal relogin saved scene/resources mismatch");
            var state=JsonSerializer.SerializeToElement(c.World.Opacity!.Snapshot());if(state.GetProperty("groups").GetArrayLength()!=0||state.GetProperty("states").GetArrayLength()!=0)throw new Exception("generation opacity ghost");
            VerifyStationaryObserver(o.World.Opacity!,o.World.MapCache!);guard.Check();log.Check("opacity-normal-relogin-saved",new{endpoint,endStatus,observerStart,moves=guard.Moves,forbidden});await shot("08-relogin");guard.Check();
        }finally{c.WorldPacketSent-=Sent;o.WorldPacketSent-=Sent;}
    }
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<OpacityCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var log=new InventoryCapture(c,o,true);string folder=Path.GetDirectoryName(s.Output)!;var a=new Assets(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets")));
        using var renderer=new Renderer(a);using var otherRenderer=new Renderer(a);var own=new World(a);var other=new World(a);var catalog=new ObjectAssets(a.Root);
        Task Shot(string name){
            foreach(var entry in new[]{(c,renderer,own,"main"),(o,otherRenderer,other,"observer")})entry.Item1.CaptureWorld(()=>{var actor=entry.Item1.World.Actor!;entry.Item3.AllowStreaming=entry.Item1.World.MapCache!=null;entry.Item3.Teleport(actor.X,actor.Y);entry.Item3.Face(actor.Direction);var patch=entry.Item2.ComposeObjects(entry.Item3,entry.Item1.World,catalog,out var visuals,out var missing);var center=Coordinates.Screen(c.World.Actor!.X,c.World.Actor.Y);using var image=entry.Item2.SceneView(patch,new((int)center.X-360,(int)center.Y-240,720,480));image.Save(Path.Combine(folder,name+"-"+entry.Item4+"-renderer.png"));var cachePixels=entry.Item2.CaptureCacheFrame(patch,entry.Item1.World.Opacity,new((int)center.X-360,(int)center.Y-240,720,480),Path.Combine(folder,name+"-"+entry.Item4));log.Check(name+"-"+entry.Item4+"-renderer",new{kind="actual connection through same product renderer, no native window",cachePixels,visuals,missing,backgroundUpdate=entry.Item2.LastBackgroundUpdate,opacity=entry.Item1.World.Opacity!.Snapshot(),pixels=patch.EntityOwners!.Where(v=>v.HasValue).GroupBy(v=>v!.Value).Select(g=>new{id=g.Key,pixels=g.Count()})});return true;});return Task.CompletedTask;
        }
        try{await Enter(c,s.Account,s.Password,s.Character);await Run(c,o,s,log,async(h,mode,g)=>{if(!await c.Move(h))throw new Exception("CLI normal move rejected");},Shot);log.Save(s.Output,"pass");}catch(Exception e){log.Save(s.Output,"fail",e);throw;}finally{c.Cancel();o.Cancel();}
    }
}


