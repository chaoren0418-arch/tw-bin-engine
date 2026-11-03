using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    async Task MovementDemo(){
        var checks=new List<object>();try{
            await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);
            if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);charactersBox.SelectedIndex=0;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);await Task.Delay(1000);
            foreach(int h in MovementTests.PathTo(login.World.Actor!.X,login.World.Actor.Y,32588,32930)){if(!await login.Move(h))throw new Exception("UI test approach failed");await Until(()=>!World.Walking&&!login.MovementBusy,3000);}
            for(int h=0;h<8;h++){
                var from=login.World.Actor!;await web!.ExecuteScriptAsync($"document.getElementById('m{h}').click()");await Until(()=>World.Walking,2000);await Task.Delay(200);
                var predicted=login.World.Actor!;if(World.Direction!=h||predicted.Direction!=h||predicted.X!=from.X+MovementRules.Dx[h]||predicted.Y!=from.Y+MovementRules.Dy[h])throw new Exception("eight-direction presentation mismatch");
                var foot=Coordinates.Screen(from.X+MovementRules.Dx[h]*World.FrameIndex/4.0,from.Y+MovementRules.Dy[h]*World.FrameIndex/4.0);if(foot!=World.DisplayPosition)throw new Exception("foot anchor mismatch");
                CaptureEvidence($"direction-{h}-middle");checks.Add(new{kind="eight-direction-web-middle",heading=h,from,predicted,model=World.Snapshot(),footX=foot.X,footY=foot.Y});
                await Until(()=>!World.Walking&&!login.MovementBusy,3000);await web.ExecuteScriptAsync($"document.getElementById('m{(h+4)%8}').click()");await Until(()=>World.Walking,2000);await Until(()=>!World.Walking&&!login.MovementBusy,3000);
            }
            var original=login.World.Actor!;
            // Actual form controller used by keyboard handlers; no external key claim.
            ReturnGameFocus();HandleDown(Keys.Left);await Until(()=>World.Walking,2000);HandleUp(Keys.Left);await Task.Delay(200);
            if(!World.Walking||World.FrameIndex==0)throw new Exception("no walk animation");CaptureEvidence("keyboard-walk-mid");
            checks.Add(new{kind="keyboard-controller-middle",actor=login.World.Actor,render=World.Snapshot(),display=World.DisplayPosition});
            await Until(()=>!World.Walking&&!login.MovementBusy,3000);
            if(login.World.Actor!.X!=original.X-1||login.World.Actor.Y!=original.Y-1)throw new Exception("keyboard movement failed");
            // Undo via real local-page bridge command, not directly through login.
            await web!.ExecuteScriptAsync("document.getElementById('m3').click()");await Until(()=>World.Walking,2000);await Task.Delay(200);CaptureEvidence("web-walk-mid");await Until(()=>!World.Walking&&!login.MovementBusy,3000);
            await web.ExecuteScriptAsync("document.getElementById('d2').click()");await Task.Delay(200);if(login.World.Actor.Direction!=2||World.Direction!=2)throw new Exception("web turn failed");
            await Task.Delay(700);await FocusWebInput();if(GameInputAllowed||Input.Held.Count!=0)throw new Exception("text focus isolation failed");
            int n=login.World.PredictedSteps;await web.ExecuteScriptAsync("document.getElementById('notes').value='WASD12345678'; document.getElementById('notes').dispatchEvent(new KeyboardEvent('keydown',{key:'w',bubbles:true}))");await Task.Delay(700);if(login.World.PredictedSteps!=n)throw new Exception("text moved actor");
            ReturnGameFocus();HandleDown(Keys.Right);HandleDown(Keys.Right);await Until(()=>World.Walking,2000);ClearInput("test-focus-loss");await Task.Delay(1000);if(Input.Held.Count!=0||World.Walking)throw new Exception("clear input did not stop");
            checks.Add(new{kind="web-turn-text-focus-repeat-release",snapshot=login.Snapshot(),model=World.Snapshot()});
            int corrections=login.World.Corrections;await login.DriftProbe(2);await Until(()=>login.World.Corrections>corrections,3000);await Task.Delay(100);
            if(World.Walking||Input.Held.Count!=0||World.X!=login.World.Actor.X||World.Y!=login.World.Actor.Y)throw new Exception("UI correction mismatch");
            CaptureEvidence("corrected");await using(var f=File.Create(Path.Combine(evidence!,"movement-web.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);
            string dom=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('worldResult').innerText"))!;
            var a=login.World.Actor;if(!dom.Contains($"{a.X}, {a.Y}")||lastPaintActorId!=a.ObjectId||lastPaintMode!="online-supported")throw new Exception("DOM/draw disagree");
            var snapshot=login.Snapshot();await Until(()=>!login.MovementBusy,3000);
            int exitHeading=Enumerable.Range(0,8).First(h=>MovementRules.Passable(login.World.Actor!.X,login.World.Actor.Y,h));
            if(!await login.Move(exitHeading))throw new Exception("cancel-in-step setup failed");await Task.Delay(180);
            if(!World.Walking)throw new Exception("cancel test requires active animation");var exitEndpoint=login.World.Actor!;
            login.Cancel();await Task.Delay(250);if(login.World.Actor!=null||World.Walking||Input.Held.Count!=0)throw new Exception("cancel left input");
            await Task.Delay(2000);loginAccount.Text=Environment.GetEnvironmentVariable("P24_TEST_ACCOUNT")!;loginPassword.Text=Environment.GetEnvironmentVariable("P24_TEST_PASSWORD")!;StartLogin();
            await Until(()=>login.State==LoginState.CharacterList,15000);charactersBox.SelectedIndex=0;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);await Task.Delay(700);
            if(Input.Held.Count!=0||World.Walking||login.World.Actor!.X!=exitEndpoint.X||login.World.Actor.Y!=exitEndpoint.Y)throw new Exception("cancel/reconnect did not preserve accepted endpoint or cleared input");
            checks.Add(new{kind="cancel-during-walk-and-reconnect",exitEndpoint,relogin=login.Snapshot(),model=World.Snapshot()});
            CaptureEvidence("reconnected");
            Json.Save(Path.Combine(evidence!,"movement-ui-result.json"),new{status="pass",kind="actual WinForms/WebView2 with in-process keyboard-controller and DOM button events; not external native input",checks,snapshot,dom,paintMode="online-supported",model=World.Snapshot()});
        }catch(Exception ex){NetworkExitCode=3;Json.Save(Path.Combine(evidence!,"movement-ui-result.json"),new{status="fail",error=ex.ToString(),checks,snapshot=login.Snapshot(),model=World.Snapshot()});}
        finally{login.Cancel();Close();}
    }
}
