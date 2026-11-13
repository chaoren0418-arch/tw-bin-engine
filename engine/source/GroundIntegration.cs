using System.Text.Json;
namespace P06;
public sealed record GroundCase(string Account,string Password,string Character,string ObserverAccount,string ObserverCharacter,string Output,bool ResumeFinal=false);
public static class GroundScenario {
    // T464 (L2 = X): Droppable no longer singles out the scenario item; the live fixture is the "$23" stack (desc-c.tbl name token).
    public static bool ScenarioItem(InventoryItem i)=>GroundRules.Droppable(i)&&(i.Name=="$23"||i.Name.StartsWith("$23 (",StringComparison.Ordinal));
    public static uint Meat(LoginConnection c)=>c.World.Inventory.Where(ScenarioItem).Aggregate(0u,(n,i)=>n+i.Count);
    public static async Task Run(LoginConnection c,LoginConnection o,GroundCase s,InventoryCapture log,Func<uint,Task> selectItem,Func<uint,Task> selectGround,Func<bool,uint,string,Task<bool>> action,Func<string,Task> shot,Func<Task>? focusNegative=null){
        await ObjectTests.Wait(()=>c.World.InventoryLoaded);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>o.World.InventoryLoaded);
        if(s.ResumeFinal){
            await ObjectTests.WalkTo(c,32586,32930);await ObjectTests.Wait(()=>!c.MovementBusy);log.Check("recovery-normal-step-before-perception");
            await ObjectTests.Wait(()=>c.World.Ground.Any(i=>i.Count==8)&&o.World.Ground.Any(i=>i.Count==8),12000);
            if(Meat(c)!=0||Meat(o)!=0||c.World.Ground.SingleOrDefault()?.Count!=8||o.World.Ground.SingleOrDefault()?.Count!=8)throw new Exception("retained finite ground stack absent; no refill");log.Check("resume-retained-eight-on-ground");await ObjectTests.WalkTo(c,32586,32930);await ObjectTests.Wait(()=>!c.MovementBusy);await selectGround(c.World.Ground.Single().ObjectId);await shot("ground-recovery-selected");
            if(!await action(true,8,"game-pickup-recovery"))throw new Exception("recovery pickup not sent");await ObjectTests.Wait(()=>Meat(c)==8&&Meat(o)==0&&c.World.Ground.Length==0&&o.World.Ground.Length==0&&c.World.Actor!.Direction==2&&o.World.Objects.Any(v=>v.ObjectId==c.World.Actor.ObjectId&&v.Direction==2));log.Check("recovered-original-eight");await shot("ground-recovery-eight");
            c.Cancel();o.Cancel();await Task.Delay(3000);await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>c.World.InventoryLoaded&&o.World.InventoryLoaded);if(Meat(c)!=8||Meat(o)!=0||c.World.Ground.Length!=0||c.World.Actor!.Direction!=2)throw new Exception("recovery saved state");log.Check("recovery-normal-relogin-saved");await shot("ground-recovery-relogin");return;
        }
        if(Meat(c)!=8||Meat(o)!=0||c.World.Ground.Length!=0)throw new Exception("finite fixture not untouched; no refill/reset");
        await ObjectTests.WalkTo(c,32586,32930);await ObjectTests.WalkTo(o,32584,32930);await ObjectTests.Wait(()=>!o.MovementBusy);await Task.Delay(300);
        var initial=c.World.Inventory.Single(ScenarioItem);uint initialId=initial.ObjectId;
        void Check(string phase){
            c.CaptureWorld(()=>o.CaptureWorld(()=>{uint total=Meat(c)+Meat(o)+c.World.Ground.Aggregate(0u,(n,i)=>n+i.Count);if(total!=8)throw new Exception("meat conservation "+phase+" = "+total);if(c.World.Ground.Length!=o.World.Ground.Length||c.World.Ground.Any(g=>!o.World.Ground.Any(v=>v.ObjectId==g.ObjectId&&v.Count==g.Count&&v.X==g.X&&v.Y==g.Y)))throw new Exception("observer ground mismatch "+phase);log.Check(phase,new{total,initialId});return true;}));
        }
        async Task Ready(){await ObjectTests.Wait(()=>!c.MovementBusy&&!o.MovementBusy);}
        async Task Send(bool pick,uint count,string mode){await Ready();if(!await action(pick,count,mode))throw new Exception("action not sent "+mode);if(await (pick?c.PickupSelected(count):c.DropSelected(count)))throw new Exception("rapid repeat admitted");}
        await selectItem(initialId);Check("initial");await shot("ground-initial");
        if(await c.DropSelected(0)||await c.DropSelected(9))throw new Exception("invalid count sent");
        var gold=c.World.Inventory.First(i=>i.IconId==5);await selectItem(gold.ObjectId);if(await c.DropSelected(1))throw new Exception("unapproved drop admitted");await selectItem(initialId);log.Check("product-negative-zero-overcount-unapproved");
        if(focusNegative!=null)await focusNegative();
        // Original rejection: original config forbids gold; this test seam never appears in product controls.
        long invRevision=c.World.InventoryRevision;var actor=c.World.Actor!;await c.GroundOriginalProbe(25,actor.X,actor.Y,gold.ObjectId,1);await ObjectTests.Wait(()=>c.World.LastServerMessage=="原服訊息 125");await Task.Delay(200);if(c.World.Inventory.Single(i=>i.ObjectId==gold.ObjectId).Count!=gold.Count)throw new Exception("forbidden gold changed");log.Check("original-rejected-gold-125");
        await Send(false,4,"game-drop");await ObjectTests.Wait(()=>Meat(c)==4&&c.World.Ground.Length==1&&o.World.Ground.Length==1);Check("original-drop-four");var ground=c.World.Ground.Single();
        if(ground.X!=actor.X||ground.Y!=actor.Y||ground.Count!=4)throw new Exception("original drop location/count");
        await Ready();await ObjectTests.WalkTo(c,32587,32930);await Ready();await selectGround(ground.ObjectId);c.World.SelectTarget(ground.ObjectId);if(c.World.TargetId!=null||c.CombatAvailable)throw new Exception("ground became attack target");if(focusNegative!=null)await focusNegative();await shot("ground-dropped-four");
        await Send(true,1,"game-pickup");await ObjectTests.Wait(()=>Meat(c)==5&&c.World.Ground.SingleOrDefault()?.Count==3&&o.World.Ground.SingleOrDefault()?.Count==3);await ObjectTests.Wait(()=>c.World.PickupActions>0&&o.World.PickupActions>0);Check("original-partial-pickup-merged");if(c.World.Inventory.Single(ScenarioItem).ObjectId!=initialId)throw new Exception("existing stack ID unexpectedly changed");await shot("ground-partial-merged");
        await Ready();await ObjectTests.WalkTo(c,32586,32935);await Ready();await selectGround(ground.ObjectId);if(await c.PickupSelected(1))throw new Exception("distance local gate");log.Check("product-distance-blocked");
        await c.GroundOriginalProbe(112,ground.X,ground.Y,ground.ObjectId,1);await Task.Delay(1100);Check("original-distance-silent-rejection");if(Meat(c)!=5||c.World.Ground.Single().Count!=3)throw new Exception("original far request mutated state");
        await ObjectTests.WalkTo(c,32587,32930);await Ready();await selectGround(ground.ObjectId);o.World.SelectGround(ground.ObjectId);
        if(!await o.PickupSelected(3))throw new Exception("observer pickup not sent");await ObjectTests.Wait(()=>Meat(o)==3&&c.World.Ground.Length==0&&o.World.Ground.Length==0);await ObjectTests.Wait(()=>o.World.Actor!.Direction==2&&c.World.Objects.Any(v=>v.ObjectId==o.World.Actor.ObjectId&&v.Direction==2));Check("observer-new-inventory-object");log.Check("observer-own-action15-main-heading2",o.World.LastPickupHeading);
        if(c.World.SelectedGroundId!=null||await c.PickupSelected(1))throw new Exception("stale ground selection sent");log.Check("product-stale-selection-cleared");
        await c.GroundOriginalProbe(112,ground.X,ground.Y,ground.ObjectId,1);await Task.Delay(1000);Check("original-already-picked-silent-rejection");await shot("ground-observer-picked");
        await Ready();var gained=o.World.Inventory.Single(ScenarioItem);o.World.SelectItem(gained.ObjectId);if(!await o.DropSelected(3))throw new Exception("observer return drop");await ObjectTests.Wait(()=>Meat(o)==0&&c.World.Ground.SingleOrDefault()?.Count==3&&o.World.Ground.Length==1);Check("observer-original-return-drop");
        await Ready();if(!await c.Turn(0))throw new Exception("explicit pre-pickup heading setup");await Ready();await ObjectTests.Wait(()=>o.World.Objects.Any(v=>v.ObjectId==c.World.Actor!.ObjectId&&v.Direction==0));log.Check("before-pickup-heading-north");
        await selectGround(c.World.Ground.Single().ObjectId);await Send(true,3,"web-pickup");await ObjectTests.Wait(()=>Meat(c)==8&&c.World.Ground.Length==0&&o.World.Ground.Length==0);await ObjectTests.Wait(()=>c.World.Actor!.Direction==6&&o.World.Objects.Any(v=>v.ObjectId==c.World.Actor.ObjectId&&v.Direction==6));Check("web-return-merged");log.Check("confirmed-own-action15-observer-heading6",c.World.LastPickupHeading);await shot("ground-web-picked");
        await selectItem(c.World.Inventory.Single(ScenarioItem).ObjectId);await Send(false,8,"web-drop");await ObjectTests.Wait(()=>Meat(c)==0&&c.World.Ground.SingleOrDefault()?.Count==8&&o.World.Ground.SingleOrDefault()?.Count==8);Check("web-full-drop-inventory-removed");await shot("ground-web-full-drop");
        await Ready();await ObjectTests.WalkTo(c,32586,32930);await Ready();await selectGround(c.World.Ground.Single().ObjectId);await Send(true,8,"game-pickup-final");await ObjectTests.Wait(()=>Meat(c)==8&&c.World.Ground.Length==0&&o.World.Ground.Length==0);await ObjectTests.Wait(()=>c.World.Actor!.Direction==2&&o.World.Objects.Any(v=>v.ObjectId==c.World.Actor.ObjectId&&v.Direction==2));Check("game-full-pickup-new-bag-entry");await shot("ground-recovered-eight");
        await Ready();var finalItems=c.World.Inventory;var finalActor=c.World.Actor!;var observerActor=o.World.Actor!;log.Check("before-normal-disconnect");c.Cancel();o.Cancel();await Task.Delay(3000);
        await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);await ObjectTests.Wait(()=>c.World.InventoryLoaded&&o.World.InventoryLoaded);
        if(Meat(c)!=8||Meat(o)!=0||c.World.SelectedGroundId!=null||c.World.SelectedItemId!=null||c.World.Ground.Length!=0||c.World.Actor!.X!=finalActor.X||c.World.Actor.Y!=finalActor.Y||c.World.Actor.Direction!=finalActor.Direction||o.World.Actor!.X!=observerActor.X||o.World.Actor.Y!=observerActor.Y||o.World.Actor.Direction!=observerActor.Direction||c.World.Inventory.Any(i=>!finalItems.Any(v=>v.ObjectId==i.ObjectId&&v.Count==i.Count))||c.World.Inventory.Length!=finalItems.Length)throw new Exception("normal saved relogin mismatch");
        Check("normal-relogin-saved");await shot("ground-relogin");await Task.Delay(1200);Check("no-delayed-ground-send");
    }
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<GroundCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var log=new InventoryCapture(c,o);
        try{await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await Run(c,o,s,log,id=>{if(!c.World.SelectItem(id))throw new Exception("item select");return Task.CompletedTask;},id=>{if(!c.World.SelectGround(id))throw new Exception("ground select");return Task.CompletedTask;},(pick,n,mode)=>pick?c.PickupSelected(n):c.DropSelected(n),_=>Task.CompletedTask);log.Save(s.Output,"pass");}
        catch(Exception ex){log.Save(s.Output,"fail",ex);throw;}finally{c.Cancel();o.Cancel();}
    }
}
public sealed partial class ClientForm {
    LoginConnection? groundObserver;InventoryCapture? groundCapture;
    void PrepareGroundCapture(){groundObserver=new LoginConnection();groundCapture=new InventoryCapture(login,groundObserver);}
    async Task GroundDemo(){
        var s=JsonSerializer.Deserialize<GroundCase>(File.ReadAllText(Environment.GetEnvironmentVariable("P24_GROUND_CASE")!))!;using var o=groundObserver??throw new Exception("capture before StartLogin required");var log=groundCapture!;
        async Task Shot(string name){SyncGround();SyncInventory();lastWebState="";PublishWebState();await web!.ExecuteScriptAsync("document.getElementById('groundPanel').scrollIntoView({block:'center'})");await Task.Delay(200);RefreshScene();Update();CaptureEvidence(name);await using var f=File.Create(Path.Combine(evidence!,name+"-web.png"));await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);log.Check("ui-"+name,new{kind="WinForms controllers and actual WebView2 DOM, not external native input",gameVisible=groundPanel.Visible,dom=await web.ExecuteScriptAsync("document.getElementById('groundPanel').innerText")});}
        async Task SelectItem(uint id){SyncInventory();inventoryList.SelectedItem=inventoryList.Items.Cast<InventoryItem>().Single(i=>i.ObjectId==id);await Task.Delay(30);if(login.World.SelectedItemId!=id)throw new Exception("native inventory select");}
        async Task SelectGround(uint id){RefreshScene();var visual=objectVisuals.Single(v=>v.ObjectId==id);if(visual.Action!="ground")throw new Exception("missing original ground visual");ClickScene(VisiblePoint(id));await Task.Delay(30);if(login.World.SelectedGroundId!=id)throw new Exception("ground pixel selection");}
        async Task<bool> Action(bool pickup,uint count,string mode){int sent=0;void Sent(object p)=>sent++;login.GroundSent+=Sent;try{
            SyncGround();if(mode.StartsWith("web-")){
                string selector=pickup?"data-ground-id":"data-item-id";uint id=pickup?login.World.SelectedGroundId!.Value:login.World.SelectedItemId!.Value;
                await web!.ExecuteScriptAsync($"document.querySelector('[{selector}=\"{id}\"]').click()");await Task.Delay(80);
                string input=pickup?"pickupCount":"dropCount",button=pickup?"pickup":"dropItem";await web.ExecuteScriptAsync($"document.getElementById('{input}').value='{count}';document.getElementById('{button}').click();document.getElementById('{button}').click()");
            }else if(pickup){pickupCount.Text=count.ToString(System.Globalization.CultureInfo.InvariantCulture);ReturnGameFocus();OnKeyDown(new KeyEventArgs(Keys.P));OnKeyDown(new KeyEventArgs(Keys.P));await Task.Delay(40);OnKeyUp(new KeyEventArgs(Keys.P));}
            else {dropCount.Text=count.ToString(System.Globalization.CultureInfo.InvariantCulture);dropButton.PerformClick();dropButton.PerformClick();}
            await Until(()=>sent==1,4000);await Task.Delay(100);if(sent!=1)throw new Exception("ground held/repeated action sent twice");return true;
        }finally{login.GroundSent-=Sent;}}
        // T464 (L2 = X): every drop asks first; the demo answers Yes and records the prompt it was shown.
        dropConfirmAnswer=prompt=>{log.Check("drop-confirm-shown",new{prompt});return true;};
        async Task FocusNegative(){int sent=0;void Sent(object p)=>sent++;login.GroundSent+=Sent;try{await FocusWebInput();OnKeyDown(new KeyEventArgs(Keys.P));OnKeyUp(new KeyEventArgs(Keys.P));await Task.Delay(100);if(sent!=0)throw new Exception("P typing sent pickup");log.Check("web-input-P-isolated");ReturnGameFocus();}finally{login.GroundSent-=Sent;}}
        try{await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);charactersBox.SelectedItem=s.Character;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);await GroundScenario.Run(login,o,s,log,SelectItem,SelectGround,Action,Shot,FocusNegative);log.Save(s.Output,"pass");}
        catch(Exception ex){NetworkExitCode=6;log.Save(s.Output,"fail",ex);}finally{login.Cancel();o.Cancel();Close();}
    }
}
