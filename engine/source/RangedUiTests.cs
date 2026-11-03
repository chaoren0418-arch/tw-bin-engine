using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    LoginConnection? rangedObserver;InventoryCapture? rangedCapture;
    void PrepareRangedCapture(){rangedObserver=new();rangedCapture=new(login,rangedObserver);}
    async Task RangedDemo(){
        var s=JsonSerializer.Deserialize<RewardCase>(File.ReadAllText(Environment.GetEnvironmentVariable("P24_RANGED_CASE")!))!;using var o=rangedObserver??throw new Exception("capture must precede first seed");var log=rangedCapture!;
        using var observerRenderer=new Renderer(World.Assets);var observerWorld=new World(World.Assets);var observerAssets=new ObjectAssets(World.Assets.Root);
        void ObserverShot(string name){
            if(o.World.Actor is not {} a)return;
            var picture=o.CaptureWorld(()=>{
                observerWorld.Teleport(a.X,a.Y);observerWorld.Face(a.Direction);long now=Environment.TickCount64;
                var p=observerRenderer.ComposeObjects(observerWorld,o.World,observerAssets,out var visuals,out var missing);
                using var full=(Bitmap)observerRenderer.Background.Clone();using(var g=Graphics.FromImage(full))using(var patch=Renderer.BitmapOf(p.Bounds.Width,p.Bounds.Height,p.Colors,p.Mask))g.DrawImageUnscaled(patch,p.Bounds.Location);
                var center=Coordinates.Screen(login.World.Actor!.X,login.World.Actor.Y);var crop=Rectangle.Intersect(new((int)center.X-360,(int)center.Y-260,720,480),new(0,0,full.Width,full.Height));
                return (bitmap:full.Clone(crop,System.Drawing.Imaging.PixelFormat.Format32bppArgb),data:(object)new{kind="actual observer connection rendered by the same product compositor; no observer native window",monotonicMs=now,appliedSequence=o.AppliedPacketSequence,projectiles=observerRenderer.ProjectileVisuals,visuals,world=o.World.Snapshot(),crop});
            });
            using(picture.bitmap)picture.bitmap.Save(Path.Combine(evidence!,name+"-observer-renderer.png"),System.Drawing.Imaging.ImageFormat.Png);log.Check("observer-renderer-"+name,picture.data);
        }
        async Task Shot(string name){
            if(name.StartsWith("ranged-shot-")){
                long deadline=Environment.TickCount64+2200;
                while(Environment.TickCount64<deadline){RefreshScene();if(renderer.ProjectileVisuals.Length>0)break;await Task.Delay(5);}
                if(renderer.ProjectileVisuals.Length>0){ObserverShot(name+"-flight");Update();CaptureEvidence(name+"-flight");log.Check("ui-original-flight-"+name,new{visuals=renderer.ProjectileVisuals,objects=ObjectUiSnapshot()});}
                else log.Check("ui-flight-not-visible-"+name,new{reason="original short flight/target/death timing; not asserted as visible"});
            }
            ObserverShot(name);SyncRewards();SyncInventory();lastWebState="";PublishWebState();await web!.ExecuteScriptAsync("document.getElementById('inventoryPanel').scrollIntoView({block:'start'})");await Task.Delay(40);RefreshScene();Update();CaptureEvidence(name);await using var f=File.Create(Path.Combine(evidence!,name+"-web.png"));await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);string dom=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('experienceResult').innerText"))!;if(!dom.Contains(login.World.Experience!.Total!.ToString()!))throw new Exception("Web EXP missing");log.Check("ui-"+name,new{kind="WinForms controller and actual WebView2 DOM; not external native input",gameText=rewardsLabel.Text,dom,visual=renderer.OwnCombatVisual,objects=ObjectUiSnapshot(),inventoryDom=await web.ExecuteScriptAsync("document.getElementById('inventoryPanel').innerText"),changesDom=await web.ExecuteScriptAsync("document.getElementById('rewardChanges').innerText")});}
        async Task SelectItem(uint id){SyncInventory();inventoryList.SelectedItem=inventoryList.Items.Cast<InventoryItem>().Single(i=>i.ObjectId==id);await Task.Delay(50);if(login.World.SelectedItemId!=id)throw new Exception("real inventory controller selection failed");}
        async Task GameFocusReady(){
            log.Check("ui-game-focus-before",new{GameInputAllowed,gameFocus,webFocused=web?.ContainsFocus,active=ActiveForm==this,Focused,ContainsFocus});
            long started=Environment.TickCount64,deadline=started+30000;
            for(int attempt=1;attempt<=15&&Environment.TickCount64<deadline;attempt++){
                if(login.State!=LoginState.InWorld||login.World.OwnDead)throw new Exception("focus wait stopped: disconnected or dead");
                long retryAt=Math.Min(deadline,Environment.TickCount64+2000);
                BringToFront();Activate();bool foregroundRequested=SetForegroundWindow(Handle);ReturnGameFocus();await Task.Delay(200);
                var actual=new{attempt,elapsedMs=Environment.TickCount64-started,GameInputAllowed,gameFocus,webFocused=web?.ContainsFocus,active=ActiveForm==this,Focused,ContainsFocus,foregroundRequested,ownHandle=Handle.ToInt64(),foregroundHandle=GetForegroundWindow().ToInt64()};
                log.Check("ui-game-focus-after-activation",actual);WebLog("ranged-test-focus-wait",actual);
                while(Environment.TickCount64<retryAt){
                    if(login.State!=LoginState.InWorld||login.World.OwnDead)throw new Exception("focus wait stopped: disconnected or dead");
                    if(GameInputAllowed){log.Check("ui-game-focus-ready",new{attempt,elapsedMs=Environment.TickCount64-started,GameInputAllowed,gameFocus,webFocused=web?.ContainsFocus,active=ActiveForm==this,Focused,ContainsFocus,ownHandle=Handle.ToInt64(),foregroundHandle=GetForegroundWindow().ToInt64()});return;}
                    await Task.Delay(50);
                }
            }
            log.Check("ui-game-focus-timeout",new{elapsedMs=Environment.TickCount64-started,GameInputAllowed,gameFocus,webFocused=web?.ContainsFocus,active=ActiveForm==this,Focused,ContainsFocus,ownHandle=Handle.ToInt64(),foregroundHandle=GetForegroundWindow().ToInt64()});
            throw new Exception("actual game focus unavailable after bounded 30-second normal activation attempts");
        }
        async Task<bool> Use(string mode){
            int count=0;void Sent(object p)=>count++;login.ItemSent+=Sent;
            try{if(mode.StartsWith("web")){lastWebState="";PublishWebState();await Task.Delay(80);await web!.ExecuteScriptAsync("document.getElementById('useItem').click();document.getElementById('useItem').click()");}else{await GameFocusReady();OnKeyDown(new KeyEventArgs(Keys.U));OnKeyDown(new KeyEventArgs(Keys.U));await Task.Delay(30);OnKeyUp(new KeyEventArgs(Keys.U));}for(int n=0;n<20&&count==0;n++)await Task.Delay(20);if(count>1)throw new Exception("repeated ranged input sent twice");return count==1;}finally{login.ItemSent-=Sent;}
        }
        bool initialSelection=true;
        async Task Select(uint id){
            int limit=initialSelection?8:1;initialSelection=false;
            for(int attempt=1;attempt<=limit;attempt++){
                await Until(()=>!World.Walking,5000);
                await Until(()=>login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Objects.Any(v=>v.ObjectId==id),5000);
                if(login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Objects.SingleOrDefault(v=>v.ObjectId==id) is not {Dead:false})throw new Exception("selection stopped: disconnected, dead or original object absent");
                RefreshScene();ClickScene(VisiblePoint(id));await Task.Delay(40);
                if(login.World.TargetId==id)return;
                // Before the first attack only, original teleport can remove a successfully clicked NPC.
                // Wait for its own original pack and click its new visible pixels; never restore a stale target.
                if(attempt==limit||login.World.TargetId!=null||login.World.Objects.Any(v=>v.ObjectId==id))throw new Exception("original visible pixel target selection failed");
                log.Check("initial-selection-cleared-by-original-removal",new{id,attempt,limit});await Task.Delay(100);
            }
        }
        async Task<bool> Attack(string mode){int count=0;void Sent(object p)=>count++;login.CombatSent+=Sent;try{if(mode.StartsWith("web")){lastWebState="";PublishWebState();await Task.Delay(80);await web!.ExecuteScriptAsync("document.getElementById('attack').click();document.getElementById('attack').click()");}else{await GameFocusReady();OnKeyDown(new KeyEventArgs(Keys.G));OnKeyDown(new KeyEventArgs(Keys.G));await Task.Delay(30);OnKeyUp(new KeyEventArgs(Keys.G));}for(int n=0;n<20&&count==0;n++)await Task.Delay(20);if(count>1)throw new Exception("held/repeated input sent twice");return count==1;}finally{login.CombatSent-=Sent;}}
        async Task SelectGround(uint id){RefreshScene();ClickScene(VisiblePoint(id));await Task.Delay(30);if(login.World.SelectedGroundId!=id)throw new Exception("original ground pixel selection failed");}
        async Task<bool> Ground(bool pickup,uint count,string mode){int sent=0;void Sent(object p)=>sent++;login.GroundSent+=Sent;try{SyncGround();SyncInventory();lastWebState="";PublishWebState();if(mode.StartsWith("web")){await Task.Delay(100);string input=pickup?"pickupCount":"dropCount",button=pickup?"pickup":"dropItem";await web!.ExecuteScriptAsync($"document.getElementById('{input}').value='{count}';document.getElementById('{button}').click();document.getElementById('{button}').click()");}else if(pickup){pickupCount.Value=count;await GameFocusReady();OnKeyDown(new KeyEventArgs(Keys.P));OnKeyDown(new KeyEventArgs(Keys.P));await Task.Delay(40);OnKeyUp(new KeyEventArgs(Keys.P));}else{dropCount.Value=count;dropButton.PerformClick();dropButton.PerformClick();}await Until(()=>sent>0,4000);await Task.Delay(50);if(sent!=1)throw new Exception("repeated ground send");return true;}finally{login.GroundSent-=Sent;}}
        async Task Focus(){int count=0;void Sent(object p)=>count++;login.CombatSent+=Sent;try{await FocusWebInput();OnKeyDown(new KeyEventArgs(Keys.G));OnKeyUp(new KeyEventArgs(Keys.G));await Task.Delay(150);if(count!=0)throw new Exception("Web typing attacked");int items=0;void ItemSent(object p)=>items++;login.ItemSent+=ItemSent;try{OnKeyDown(new KeyEventArgs(Keys.U));OnKeyUp(new KeyEventArgs(Keys.U));await Task.Delay(150);if(items!=0)throw new Exception("Web typing equipped item");}finally{login.ItemSent-=ItemSent;}log.Check("web-typing-G-no-attack");ReturnGameFocus();}finally{login.CombatSent-=Sent;}}
        try{await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);charactersBox.SelectedItem=s.Character;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);await RangedScenario.Run(login,o,s,log,SelectItem,Use,Select,Attack,SelectGround,Ground,Shot,Focus);log.Save(s.Output,"pass");}
        catch(Exception ex){NetworkExitCode=7;log.Save(s.Output,"fail",ex);}finally{login.Cancel();o.Cancel();Close();}
    }
}
