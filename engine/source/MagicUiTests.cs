using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    LoginConnection? magicObserver;InventoryCapture? magicCapture;
    void PrepareMagicCapture(){magicObserver=new();magicCapture=new(login,magicObserver);}
    async Task MagicDemo(){
        var s=JsonSerializer.Deserialize<RewardCase>(File.ReadAllText(Environment.GetEnvironmentVariable("P24_MAGIC_CASE")!))!;using var o=magicObserver??throw new Exception("capture must precede first seed");var log=magicCapture!;
        using var observerRenderer=new Renderer(World.Assets);var observerWorld=new World(World.Assets);var observerAssets=new ObjectAssets(World.Assets.Root);
        void ObserverShot(string name){
            if(o.World.Actor is not {} a)return;
            var picture=o.CaptureWorld(()=>{
                observerWorld.Teleport(a.X,a.Y);observerWorld.Face(a.Direction);long now=Environment.TickCount64;
                var p=observerRenderer.ComposeObjects(observerWorld,o.World,observerAssets,out var visuals,out var missing);
                using var full=(Bitmap)observerRenderer.Background.Clone();using(var g=Graphics.FromImage(full))using(var patch=Renderer.BitmapOf(p.Bounds.Width,p.Bounds.Height,p.Colors,p.Mask))g.DrawImageUnscaled(patch,p.Bounds.Location);
                var center=Coordinates.Screen(login.World.Actor!.X,login.World.Actor.Y);var crop=Rectangle.Intersect(new((int)center.X-360,(int)center.Y-260,720,480),new(0,0,full.Width,full.Height));
                return (bitmap:full.Clone(crop,System.Drawing.Imaging.PixelFormat.Format32bppArgb),data:(object)new{kind="actual observer connection rendered by the same product compositor; no observer native window",monotonicMs=now,appliedSequence=o.AppliedPacketSequence,magic=observerRenderer.MagicVisuals,visuals,world=o.World.Snapshot(),crop});
            });
            using(picture.bitmap)picture.bitmap.Save(Path.Combine(evidence!,name+"-observer-renderer.png"),System.Drawing.Imaging.ImageFormat.Png);log.Check("observer-renderer-"+name,picture.data);
        }
        async Task Shot(string name){
            if(name=="magic-self-heal")await Task.Delay(250);
            if(name.StartsWith("magic-bolt-")){
                long deadline=Environment.TickCount64+2200;
                while(Environment.TickCount64<deadline){if(login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Status!.Hp<150)throw new Exception("post-cast image wait stopped on actual state");RefreshScene();if(renderer.MagicVisuals.Any(v=>v.Gfx==167&&v.Phase=="fly"))break;await Task.Delay(5);}
                if(renderer.MagicVisuals.Any(v=>v.Gfx==167&&v.Phase=="fly")){ObserverShot(name+"-flight");Update();CaptureEvidence(name+"-flight");log.Check("ui-original-flight-"+name,new{visuals=renderer.MagicVisuals,objects=ObjectUiSnapshot()});}
                else log.Check("ui-flight-not-visible-"+name,new{reason="original short flight/target/death timing; not asserted as visible"});
            }
            ObserverShot(name);SyncRewards();SyncInventory();SyncMagic();lastWebState="";PublishWebState();await web!.ExecuteScriptAsync("document.getElementById('magicPanel').scrollIntoView({block:'start'})");await Task.Delay(40);RefreshScene();Update();CaptureEvidence(name);await using var f=File.Create(Path.Combine(evidence!,name+"-web.png"));await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);string dom=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('experienceResult').innerText"))!;if(!dom.Contains(login.World.Experience!.Total!.ToString()!))throw new Exception("Web EXP missing");log.Check("ui-"+name,new{kind="WinForms controller and actual WebView2 DOM; not external native input",gameText=rewardsLabel.Text,dom,visual=renderer.OwnCombatVisual,objects=ObjectUiSnapshot(),inventoryDom=await web.ExecuteScriptAsync("document.getElementById('inventoryPanel').innerText"),changesDom=await web.ExecuteScriptAsync("document.getElementById('rewardChanges').innerText")});}
        async Task SelectMagic(int id,string mode){SyncMagic();if(mode=="web"){lastWebState="";PublishWebState();await Task.Delay(80);await web!.ExecuteScriptAsync($"document.getElementById('magicSkills').value='{id}';document.getElementById('magicSkills').dispatchEvent(new Event('change'))");}else magicList.SelectedItem=magicList.Items.Cast<LearnedMagic>().Single(m=>m.Id==id);await Until(()=>login.World.SelectedMagicId==id,3000);}
        async Task GameFocusReady(){
            log.Check("ui-game-focus-before",new{GameInputAllowed,gameFocus,webFocused=web?.ContainsFocus,active=ActiveForm==this,Focused,ContainsFocus});
            uint focusHp=login.World.Status!.Hp;long started=Environment.TickCount64,deadline=started+2000;
            for(int attempt=1;attempt<=2&&Environment.TickCount64<deadline;attempt++){
                if(login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Status!.Hp<150||login.World.Status.Hp<focusHp){log.Check("ui-focus-finite-stop",new{focusHp,current=login.World.Status});throw new Exception("focus wait stopped: disconnected, dead, low HP or real damage");}
                long retryAt=Math.Min(deadline,Environment.TickCount64+1000);
                BringToFront();Activate();bool foregroundRequested=SetForegroundWindow(Handle);ReturnGameFocus();await Task.Delay(50);
                var actual=new{attempt,elapsedMs=Environment.TickCount64-started,GameInputAllowed,gameFocus,webFocused=web?.ContainsFocus,active=ActiveForm==this,Focused,ContainsFocus,foregroundRequested,ownHandle=Handle.ToInt64(),foregroundHandle=GetForegroundWindow().ToInt64()};
                log.Check("ui-game-focus-after-activation",actual);WebLog("magic-test-focus-wait",actual);
                while(Environment.TickCount64<retryAt){
                    if(login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Status!.Hp<150||login.World.Status.Hp<focusHp){log.Check("ui-focus-finite-stop",new{focusHp,current=login.World.Status});throw new Exception("focus wait stopped: disconnected, dead, low HP or real damage");}
                    if(GameInputAllowed&&GetForegroundWindow()==Handle){log.Check("ui-game-focus-ready",new{attempt,elapsedMs=Environment.TickCount64-started,GameInputAllowed,gameFocus,webFocused=web?.ContainsFocus,active=ActiveForm==this,Focused,ContainsFocus,ownHandle=Handle.ToInt64(),foregroundHandle=GetForegroundWindow().ToInt64()});return;}
                    await Task.Delay(50);
                }
            }
            log.Check("ui-game-focus-timeout",new{elapsedMs=Environment.TickCount64-started,GameInputAllowed,gameFocus,webFocused=web?.ContainsFocus,active=ActiveForm==this,Focused,ContainsFocus,ownHandle=Handle.ToInt64(),foregroundHandle=GetForegroundWindow().ToInt64()});
            throw new Exception("actual game focus unavailable after bounded two-second normal activation attempts");
        }
        uint[] VisibleTargets(){
            RefreshScene();if(World.Walking)return [];
            // VisiblePoint also enforces the actual game viewport, using the existing product pixel owners.
            return login.World.Objects.Where(v=>v.Gfx==1110&&!v.Dead&&HasVisibleObjectPixel(v.ObjectId)).Where(v=>{try{_=VisiblePoint(v.ObjectId);return true;}catch(Exception ex) when(ex.Message.StartsWith("no visible pickable pixels")){return false;}}).Select(v=>v.ObjectId).ToArray();
        }
        bool nativeTargetCovered=false,webTargetCovered=false;
        async Task Select(uint id,string mode){
            uint initialHp=login.World.Status!.Hp;long deadline=Environment.TickCount64+2000;
            while(true){
                if(login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Status!.Hp<150||login.World.Status.Hp<initialHp){log.Check("ui-visible-target-finite-stop",new{id,initialHp,current=login.World.Status});throw new Exception("target wait stopped on damage/death/disconnect/low HP");}
                RefreshScene();
                if(!World.Walking&&login.World.Objects.Any(v=>v.ObjectId==id&&!v.Dead)&&HasVisibleObjectPixel(id))break;
                if(Environment.TickCount64>=deadline)throw new Exception("visible target unavailable within two seconds");
                await Task.Delay(50);
            }
            int c6=0,c164=0,attack=0;uint? prior=login.World.TargetId;
            void MagicSent(object packet)=>c6++;void ItemSent(object packet)=>c164++;void AttackSent(object packet)=>attack++;
            void Quiet(){if(c6!=0||c164!=0||attack!=0)throw new Exception("target clear/selection emitted magic C6, item C164 or attack C229/C123");}
            login.MagicSent+=MagicSent;login.ItemSent+=ItemSent;login.CombatSent+=AttackSent;
            try{
                log.Check("ui-before-target-selection",new{mode,priorTarget=prior,requestedTarget=id,c6,c164,attack});
                if(mode=="web"){
                    Point empty=MagicEmptySelectionPoint();var clearScene=Camera.ToScene(empty);if(GetChildAtPoint(empty)!=null)throw new Exception("clear point covered by a native child control");ClickScene(empty);Quiet();
                    if(login.World.TargetId!=null)throw new Exception("normal no-entity game pixel did not clear target");
                    log.Check("ui-native-target-cleared",new{priorTarget=prior,currentTarget=login.World.TargetId,viewPoint=empty,scenePoint=clearScene,entityOwner=(uint?)null,childControlAtPoint=GetChildAtPoint(empty)?.Name,c6,c164,attack,method="actual ClickScene/PickObject no-entity original map pixel"});
                    lastWebState="";PublishWebState();await Task.Delay(50);Quiet();
                    _=VisiblePoint(id);if(login.World.TargetId!=null)throw new Exception("target changed before Web selection");
                    log.Check("ui-before-web-target-select",new{priorTarget=login.World.TargetId,requestedTarget=id,positivePixels=HasVisibleObjectPixel(id),c6,c164,attack});
                    await web!.ExecuteScriptAsync($"document.getElementById('magicTargets').value='{id}';document.getElementById('magicTargets').dispatchEvent(new Event('change'))");
                }else ClickScene(VisiblePoint(id));
                await Task.Delay(40);Quiet();
                if(login.World.TargetId!=id||!HasVisibleObjectPixel(id))throw new Exception("original visible pixel target selection failed");
                if(mode=="web")webTargetCovered=true;else nativeTargetCovered=true;
                log.Check(mode=="web"?"ui-actual-web-target-selected":"ui-actual-native-target-selected",new{priorTarget=mode=="web"?(uint?)null:prior,currentTarget=login.World.TargetId,requestedTarget=id,positivePixels=HasVisibleObjectPixel(id),c6,c164,attack});
            }finally{login.MagicSent-=MagicSent;login.ItemSent-=ItemSent;login.CombatSent-=AttackSent;}
        }
        async Task<bool> Cast(string mode){int count=0;void Sent(object p)=>count++;login.MagicSent+=Sent;try{if(mode.StartsWith("web")){lastWebState="";PublishWebState();await Task.Delay(80);await web!.ExecuteScriptAsync("document.getElementById('castMagic').click();document.getElementById('castMagic').click()");}else{await GameFocusReady();OnKeyDown(new KeyEventArgs(Keys.M));OnKeyDown(new KeyEventArgs(Keys.M));await Task.Delay(30);OnKeyUp(new KeyEventArgs(Keys.M));}for(int n=0;n<20&&count==0;n++)await Task.Delay(20);if(count>1)throw new Exception("held/repeated input sent twice");return count==1;}finally{login.MagicSent-=Sent;}}
        async Task RecoverSaved(){
            long end=Environment.TickCount64+600000;int potions=0,heals=0;uint lastHp=login.World.Status!.Hp;
            async Task RecoveryWait(Func<bool> ready,uint hpBefore){
                long deadline=Math.Min(end,Environment.TickCount64+7000);
                while(true){if(login.State!=LoginState.InWorld||login.World.OwnDead||login.World.Status!.Hp<hpBefore||Environment.TickCount64>=deadline)throw new Exception("finite recovery response stopped");if(ready())return;await Task.Delay(50);}
            }
            log.Check("exact-saved-recovery-start",new{maximumPotions=6,maximumSingleHeals=30,maximumSeconds=600,requiredHp=500,requiredMp=20});await Shot("magic-resume-town-before");
            while(login.World.Status!.Hp<500||login.World.Status.Mp<20){
                var status=login.World.Status;
                if(login.State!=LoginState.InWorld||login.World.OwnDead||status.Hp==0||status.Hp<lastHp||status.Food<3||status.Weight>=120||Environment.TickCount64>=end)throw new Exception("finite original recovery stopped: damage, original regen condition or deadline");
                lastHp=status.Hp;
                if(login.MovementBusy){await Task.Delay(50);continue;}
                var potion=login.World.Inventory.FirstOrDefault(i=>i.SupportedPotion&&i.Count>0);
                if(status.Hp<500&&potions<6&&potion!=null){
                    SyncInventory();inventoryList.SelectedItem=inventoryList.Items.Cast<InventoryItem>().Single(i=>i.ObjectId==potion.ObjectId);SyncInventory();
                    if(!useItemButton.Enabled)throw new Exception("original potion control unavailable");
                    log.Check("before-resume-original-potion",new{potions,potion});useItemButton.PerformClick();potions++;
                    await RecoveryWait(()=>login.World.Inventory.All(i=>i.ObjectId!=potion.ObjectId||i.Count<potion.Count),status.Hp);
                    await RecoveryWait(()=>login.World.Status!.Hp>status.Hp,status.Hp);log.Check("after-resume-original-potion");
                }else if(status.Hp<500&&heals<30&&status.Mp>=23){
                    await SelectMagic(1,"web");log.Check("before-resume-single-heal",new{heals,hp=status.Hp,mp=status.Mp});
                    if(!await Cast("web-resume-heal"))throw new Exception("original single recovery heal not admitted");heals++;
                    await RecoveryWait(()=>login.World.Status!.Mp<status.Mp&&login.World.Status.Hp>status.Hp,status.Hp);log.Check("after-resume-single-heal");
                }else await Task.Delay(250);
            }
            log.Check("exact-saved-recovery-ready",new{potions,heals,actual=login.World.Status});await Shot("magic-resume-town-recovered");
        }
        async Task Focus(){int count=0;void Sent(object p)=>count++;login.MagicSent+=Sent;try{await FocusWebInput();OnKeyDown(new KeyEventArgs(Keys.M));OnKeyUp(new KeyEventArgs(Keys.M));await Task.Delay(150);if(count!=0)throw new Exception("Web typing cast a spell");int items=0;void ItemSent(object p)=>items++;login.ItemSent+=ItemSent;try{OnKeyDown(new KeyEventArgs(Keys.U));OnKeyUp(new KeyEventArgs(Keys.U));await Task.Delay(150);if(items!=0)throw new Exception("Web typing equipped item");}finally{login.ItemSent-=ItemSent;}log.Check("web-typing-M-no-cast");ReturnGameFocus();}finally{login.MagicSent-=Sent;}}
        try{await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);charactersBox.SelectedItem=s.Character;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);await MagicScenario.Run(login,o,s,log,SelectMagic,Select,Cast,Shot,Focus,RecoverSaved,GameFocusReady,VisibleTargets);if(!s.MagicVisualReview&&(!nativeTargetCovered||!webTargetCovered))throw new Exception("full UI missing actual native or Web target selection");log.Check("ui-both-target-selection-paths",new{nativeTargetCovered,webTargetCovered});log.Save(s.Output,"pass");}
        catch(Exception ex){NetworkExitCode=7;log.Save(s.Output,"fail",ex);}finally{login.Cancel();o.Cancel();Close();}
    }
}
