using System.Text.Json;
namespace P06;
public sealed record WorldCase(string Account,string Password,string Character,string Output,int HoldMs=95000,bool Invalid=false,bool CancelEntering=false);
public static class WorldTests {
    public static async Task Probe(string config){
        var spec=JsonSerializer.Deserialize<WorldCase>(File.ReadAllText(config))!;
        using var c=new LoginConnection();var states=new List<object>();var packets=new List<object>();
        c.Changed+=()=>{lock(states)states.Add(new{utc=DateTimeOffset.UtcNow,snapshot=c.Snapshot()});};
        c.PacketReceived+=p=>{lock(packets)packets.Add(new{utc=DateTimeOffset.UtcNow,packet=p});};
        var running=c.Run(42061,"p24-real-jar-d287-client2",spec.Account,spec.Password,10000);
        try{
            await Wait(()=>c.State is LoginState.CharacterList or LoginState.Faulted or LoginState.Rejected,15000);
            if(c.State!=LoginState.CharacterList)throw new Exception("login failed "+c.Detail);
            bool invalidUiBlocked=false;try{await c.SelectCharacter("not_in_list");}catch(InvalidOperationException){invalidUiBlocked=true;}
            if(!invalidUiBlocked||c.State!=LoginState.CharacterList)throw new Exception("invalid UI selection was not rejected");
            if(spec.Invalid)await c.SelectForProbe(spec.Character);else await c.SelectCharacter(spec.Character);
            if(spec.CancelEntering){c.Cancel();await running;if(c.World.Actor!=null||c.Characters.Count!=0)throw new Exception("cancel retained data");}
            else if(spec.Invalid){await Wait(()=>c.State is LoginState.Disconnected or LoginState.Faulted,15000);if(c.World.Ready||c.World.Actor!=null)throw new Exception("invalid selection entered world");}
            else{
                await Wait(()=>c.State is LoginState.InWorld or LoginState.Faulted or LoginState.Disconnected,15000);
                if(c.State!=LoginState.InWorld)throw new Exception("world failed "+c.Detail);
                Json.Save(spec.Output+".ready.json",new{pid=Environment.ProcessId,snapshot=c.Snapshot()});
                long start=Environment.TickCount64;
                while(Environment.TickCount64-start<spec.HoldMs){await Task.Delay(500);if(c.State!=LoginState.InWorld)throw new Exception("lost world "+c.Detail);Json.Save(spec.Output+".live.tmp.json",new{elapsedMs=Environment.TickCount64-start,snapshot=c.Snapshot()});File.Move(spec.Output+".live.tmp.json",spec.Output+".live.json",true);}
                if(spec.HoldMs>=90000&&c.World.TimeReplies<6)throw new Exception("missing periodic live keepalive responses");
            }
            var last=c.Snapshot();c.Cancel();await running;
            if(c.World.Actor!=null||c.World.Status!=null||c.Characters.Count!=0)throw new Exception("cancel left stale generation");
            Json.Save(spec.Output,new{status="pass",invalidUiBlocked,spec.Invalid,spec.CancelEntering,last,cancelled=c.Snapshot(),states,packets});
        }catch(Exception ex){var last=c.Snapshot();c.Cancel();await running;Json.Save(spec.Output,new{status="fail",error=ex.ToString(),last,states,packets});throw;}
    }
    static async Task Wait(Func<bool> f,int ms){long end=Environment.TickCount64+ms;while(!f()){if(Environment.TickCount64>end)throw new TimeoutException();await Task.Delay(20);}}
}
public sealed partial class ClientForm {
    async Task WorldDemo(string scenario){
        object? longRun=null;var rawPackets=new List<object>();if(scenario=="real-world-long")login.PacketReceived+=p=>rawPackets.Add(p);
        try{
            await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);
            if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);
            if(scenario=="real-world-pending"&&(!pendingOfflineStepTest||World.Walking))throw new Exception("offline step was not stopped by login transition");
            int index=scenario.EndsWith("second")?1:0;charactersBox.SelectedIndex=index;enterWorld.PerformClick();
            await Until(()=>login.State is LoginState.InWorld or LoginState.Faulted or LoginState.Disconnected,15000);
            if(login.State!=LoginState.InWorld)throw new Exception(login.Detail);
            await Task.Delay(1500);
            if(scenario=="real-world-long"){
                int startPaint=paintCount,ticks=0;long start=Environment.TickCount64,last=start,maxGap=0;using var health=new System.Windows.Forms.Timer{Interval=20};health.Tick+=(_,_)=>{long now=Environment.TickCount64;maxGap=Math.Max(maxGap,now-last);last=now;ticks++;};health.Start();
                await Task.Delay(95000);await FocusWebInput();OnKeyDown(new KeyEventArgs(Keys.G));OnKeyUp(new KeyEventArgs(Keys.G));if(GameInputAllowed||login.World.AttackReports!=0)throw new Exception("long real UI focus isolation");ReturnGameFocus();RefreshScene();Update();health.Stop();
                if(login.State!=LoginState.InWorld||ticks<100||paintCount-startPaint<50||maxGap>3000||login.World.KeepalivesSent<6||login.World.TimeReplies<6)throw new Exception("long real UI heartbeat/paint/timer stalled");
                longRun=new{elapsedMs=Environment.TickCount64-start,ticks,paints=paintCount-startPaint,maxTimerGapMs=maxGap,focusRestored=GameInputAllowed,keepalive=login.World.KeepalivesSent,timeReplies=login.World.TimeReplies};
            }
            var actor=login.World.Actor!;
            RefreshScene();Update();
            if(login.World.Renderable){if(World.X!=actor.X||World.Y!=actor.Y||World.Direction!=actor.Direction||lastPaintMode!="online-supported"||lastPaintActorId!=actor.ObjectId)throw new Exception("rendered self does not match packets");}
            else if(lastPaintMode!="online-unsupported"||lastPaintActorId!=null)throw new Exception("unsupported world drew stale map/actor");
            int x=World.X,y=World.Y,d=World.Direction;
            foreach(var key in new[]{Keys.R,Keys.F2,Keys.F6}){HandleDown(key);HandleUp(key);}
            if(!login.World.Renderable)await web!.ExecuteScriptAsync("document.getElementById('d2').click()");await Task.Delay(300);
            if(World.X!=x||World.Y!=y||World.Direction!=d||World.Walking)throw new Exception("online input mutated presentation");
            loginAccount.Clear();CaptureEvidence(scenario);
            await using(var png=File.Create(Path.Combine(evidence!,scenario+"-web.png")))await web!.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,png);
            string dom=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('worldResult').innerText"))!;
            string position=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('position').innerText"))!;
            string direction=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('direction').innerText"))!;
            if(!position.Contains($"{actor.X}, {actor.Y}")||!direction.Contains($"D{actor.Direction}"))throw new Exception("web shows stale presentation coordinates");
            if(!dom.Contains(actor.Name)||!dom.Contains(actor.ObjectId.ToString()))throw new Exception("world data absent in DOM");
            var snap=login.CaptureWorld(()=>login.Snapshot());var capturedPackets=rawPackets.ToArray();var presentation=new{lastPaintMode,lastPaintActorId,position,direction,model=login.World.Renderable?World.Snapshot():null};login.Cancel();await Task.Delay(300);
            string cleared=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('worldResult').innerText"))!;
            if(cleared.Contains(actor.Name)||login.World.Actor!=null||lastPaintActorId!=null||lastPaintMode!="waiting")throw new Exception("UI stale after cancel");
            Json.Save(Path.Combine(evidence!,"world-ui-result.json"),new{status="pass",scenario,kind="actual WinForms/WebView2 in-process controller; not external keyboard",snapshot=snap,dom,cleared,presentation,longRun,packets=capturedPackets,pendingOfflineStepTest,offlineOnlyCommandsBlocked=true,model=World.Snapshot()});
        }catch(Exception ex){NetworkExitCode=3;Json.Save(Path.Combine(evidence!,"world-ui-result.json"),new{status="fail",error=ex.ToString(),snapshot=login.Snapshot()});}
        finally{login.Cancel();Close();}
    }
}
