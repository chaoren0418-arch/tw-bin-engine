using System.Text.Json;
namespace P06;
public sealed record InventoryCase(string Account,string Password,string Character,string ObserverAccount,string ObserverCharacter,string Output,int InitialCount=6,bool ReviewOnly=false);
public sealed class InventoryCapture {
    readonly LoginConnection c,o;readonly List<object> packets=[],observerPackets=[],sent=[],observerSent=[],checks=[],opacityTrace=[],observerOpacityTrace=[];int cg,og;
    public InventoryCapture(LoginConnection main,LoginConnection observer,bool captureOpacity=false){c=main;o=observer;
        if(captureOpacity){c.World.OpacityObserved+=v=>opacityTrace.Add(JsonSerializer.SerializeToElement(v));o.World.OpacityObserved+=v=>observerOpacityTrace.Add(JsonSerializer.SerializeToElement(v));}
        c.PacketReceived+=p=>{if(JsonSerializer.SerializeToElement(p).S("kind")=="seed")cg++;packets.Add(new{generation=cg,utc=DateTimeOffset.UtcNow,packet=p});};
        o.PacketReceived+=p=>{if(JsonSerializer.SerializeToElement(p).S("kind")=="seed")og++;observerPackets.Add(new{generation=og,utc=DateTimeOffset.UtcNow,packet=p});};
        c.WorldPacketSent+=p=>sent.Add(new{generation=cg,packet=p});o.WorldPacketSent+=p=>observerSent.Add(new{generation=og,packet=p});
    }
    public object Snapshot()=>c.CaptureWorld(()=>o.CaptureWorld(()=>new{main=c.Snapshot(),observer=o.Snapshot(),mainGeneration=cg,observerGeneration=og,mainSequence=c.AppliedPacketSequence,observerSequence=o.AppliedPacketSequence,mainPacketCount=packets.Count,observerPacketCount=observerPackets.Count,mainSentCount=sent.Count,observerSentCount=observerSent.Count}));
    public void Check(string phase,object? data=null)=>checks.Add(new{phase,data,state=Snapshot(),utc=DateTimeOffset.UtcNow});
    public void Save(string output,string status,Exception? error=null)=>c.CaptureWorld(()=>o.CaptureWorld(()=>{var final=Snapshot();c.Cancel();o.Cancel();Json.Save(output,new{status,error=error?.ToString(),kind="P24 original fixed JAR inventory: finite initial fixture, actual encrypted loopback protocol; no SQL product dependency",checks=checks.ToArray(),opacityTrace=opacityTrace.ToArray(),observerOpacityTrace=observerOpacityTrace.ToArray(),packets=packets.ToArray(),observerPackets=observerPackets.ToArray(),sent=sent.ToArray(),observerSent=observerSent.ToArray(),final,closed=c.Snapshot(),observerClosed=o.Snapshot()});return true;}));
}
public static class InventoryScenario {
    static async Task Walk(LoginConnection c,int x,int y){
        long until=Environment.TickCount64+90000;
        while(true){try{await ObjectTests.WalkTo(c,x,y);return;}catch(Exception ex) when(ex.Message=="no legal test route"&&Environment.TickCount64<until){await Task.Delay(150);}}
    }
    public static async Task Run(LoginConnection c,LoginConnection o,InventoryCase s,InventoryCapture log,Func<uint,Task> select,Func<string,Task<bool>> use,Func<string,Task> shot,Func<Task>? inputNegatives=null,Func<uint,Task>? targetSelect=null){
        await ObjectTests.Wait(()=>c.World.InventoryLoaded);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);
        var potion=c.World.Inventory.Single(i=>i.SupportedPotion);uint iid=potion.ObjectId;if(s.InitialCount is not (4 or 5 or 6)||potion.Count!=s.InitialCount)throw new Exception("finite initial/resume count mismatch; will not grant/reset");
        await select(iid);log.Check("initial-list");await shot("inventory-initial");
        var unsupported=c.World.Inventory.First(i=>!i.SupportedUse);await select(unsupported.ObjectId);if(await c.UseSelectedItem())throw new Exception("unsupported request sent");await select(iid);
        if(inputNegatives!=null)await inputNegatives();
        async Task Use(string mode,uint before){
            await ObjectTests.Wait(()=>!c.MovementBusy);var hp=c.World.Status;long effects=c.World.ItemEffectNumber;log.Check("before-use-"+mode,new{beforeHp=hp,beforeCount=before,effects});
            if(!await use(mode))throw new Exception("use not sent "+mode+": "+c.World.ItemNotice);
            if(await c.UseSelectedItem())throw new Exception("rapid repeat admitted");
            await ObjectTests.Wait(()=>before==1?c.World.Inventory.All(i=>i.ObjectId!=iid):c.World.Inventory.Any(i=>i.ObjectId==iid&&i.Count==before-1),5000);
            await ObjectTests.Wait(()=>c.World.ItemEffectNumber>effects,5000);await Task.Delay(180);log.Check("after-use-"+mode,new{beforeHp=hp,afterHp=c.World.Status,beforeCount=before});await shot("inventory-"+mode);
        }
        if(c.World.Status!.Hp!=c.World.Status.MaxHp)throw new Exception("initial role not full HP; do not reset");
        if(s.InitialCount==6){await Use("full-health",6);if(c.World.Status!.Hp!=c.World.Status.MaxHp)throw new Exception("unexpected initial HP");}
        else log.Check("resume-after-preserved-consumption",new{count=s.InitialCount,previousCapture="resume.json identifies immutable failed boundary capture; no refill"});
        if(s.InitialCount>=5){
        await ObjectTests.Wait(()=>!c.MovementBusy);log.Check("before-original-cooldown-probe");
        if(!await use("cooldown-first"))throw new Exception("cooldown first not sent");await ObjectTests.Wait(()=>c.World.Inventory.Single(i=>i.ObjectId==iid).Count==4,5000);await c.ProbeOriginalItemDelay(iid);await Task.Delay(1400);
        if(c.World.Inventory.Single(i=>i.ObjectId==iid).Count!=4)throw new Exception("original immediate repeated request did not consume exactly once");log.Check("original-cooldown-rejected-second",new{requests=2,consumed=1});
        }
        await Walk(c,32591,32945);await Walk(o,32590,32946);
        await ObjectTests.Wait(()=>c.World.Objects.Any(v=>v.Gfx==1110&&!v.Dead),90000);uint npc=c.World.Objects.First(v=>v.Gfx==1110&&!v.Dead).ObjectId;
        await Walk(c,32599,32948);await ObjectTests.Wait(()=>!c.MovementBusy);
        bool Settled(WorldObject v)=>v.MotionAt==0||Environment.TickCount64-v.MotionAt>=640;
        await ObjectTests.Wait(()=>c.World.Objects.Any(v=>v.ObjectId==npc&&!v.Dead&&v.X==32598&&v.Y==32948&&Settled(v))&&o.World.Objects.Any(v=>v.ObjectId==npc&&v.X==32598&&v.Y==32948),90000);
        if(targetSelect!=null)await targetSelect(npc);else c.World.SelectTarget(npc);log.Check("original-axis-home-before-single-hit");
        if(!await c.Attack())throw new Exception("initial original single attack rejected");if(await c.UseSelectedItem())throw new Exception("item accepted during combat busy");
        await ObjectTests.Wait(()=>!c.MovementBusy);
        foreach(int x in new[]{32600,32601}){
            if(c.World.Actor is not {Y:32948}||c.World.Actor.X!=x-1)throw new Exception("axis setup lost");if(!await c.Move(2))throw new Exception("axis movement rejected");await ObjectTests.Wait(()=>!c.MovementBusy);
            await ObjectTests.Wait(()=>c.World.Objects.Any(v=>v.ObjectId==npc&&v.X==x-1&&v.Y==32948&&Settled(v))&&o.World.Objects.Any(v=>v.ObjectId==c.World.Actor!.ObjectId&&v.X==x&&v.Y==32948)&&o.World.Objects.Any(v=>v.ObjectId==npc&&v.X==x-1&&v.Y==32948),10000);log.Check("original-axis-east-"+x);
        }
        await Walk(c,32600,32947);await ObjectTests.Wait(()=>!c.MovementBusy);
        await ObjectTests.Wait(()=>c.World.Objects.Any(v=>v.ObjectId==npc&&v.X==32600&&v.Y==32948)&&o.World.Objects.Any(v=>v.ObjectId==c.World.Actor!.ObjectId&&v.X==32600&&v.Y==32947),5000);
        log.Check("visible-retaliation-start");await shot("inventory-original-retaliation");
        await ObjectTests.Wait(()=>c.World.Status!.Hp<=c.World.Status.MaxHp-180||c.World.OwnDead,120000);
        if(c.World.OwnDead||c.World.Status!.Hp<200)throw new Exception("unsafe original damage state; no reset");log.Check("original-retaliation-deficit");await shot("inventory-damaged");
        // Normal movement retreats before measuring medicine, separating later HP from continued NPC attacks.
        await Walk(c,32588,32930);await ObjectTests.Wait(()=>!c.MovementBusy);await Task.Delay(1100);log.Check("safe-before-medicine");
        uint damaged=c.World.Status!.Hp;await select(iid);await Use("game-healing",4);if(c.World.Status!.Hp<=damaged)throw new Exception("no original HP recovery after game use");
        uint firstHp=c.World.Status.Hp;await Use("web-healing",3);if(c.World.Status.Hp<=firstHp)throw new Exception("no original HP recovery after web use");
        await Use("remaining-one",2);await Use("depleted",1);
        if(c.World.SelectedItemId!=null||await c.UseSelectedItem())throw new Exception("depleted selection/request");log.Check("depleted-no-ghost");
        var endpoint=c.World.Actor;var savedItems=c.World.Inventory;uint hpAtExit=c.World.Status!.Hp;log.Check("before-normal-disconnect");
        c.Cancel();await Task.Delay(2500);await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Wait(()=>c.World.InventoryLoaded);
        if(c.World.Inventory.Any(i=>i.ObjectId==iid)||c.World.SelectedItemId!=null||c.World.Actor!.X!=endpoint!.X||c.World.Actor.Y!=endpoint.Y||c.World.Inventory.Length!=savedItems.Length||c.World.Inventory.Any(i=>!savedItems.Any(x=>x.ObjectId==i.ObjectId&&x.Count==i.Count)))throw new Exception("relogin inventory/position mismatch");
        log.Check("normal-relogin-saved",new{hpAtExit});await shot("inventory-relogin");await Task.Delay(1200);log.Check("no-delayed-use-after-relogin");
    }
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<InventoryCase>(File.ReadAllText(path))!;using var c=new LoginConnection();using var o=new LoginConnection();var log=new InventoryCapture(c,o);
        try{await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await Run(c,o,s,log,id=>{if(!c.World.SelectItem(id))throw new Exception("selection");return Task.CompletedTask;},_=>c.UseSelectedItem(),_=>Task.CompletedTask);log.Save(s.Output,"pass");}
        catch(Exception ex){log.Save(s.Output,"fail",ex);throw;}finally{c.Cancel();o.Cancel();}
    }
}
public sealed partial class ClientForm {
    LoginConnection? inventoryObserver;InventoryCapture? inventoryCapture;
    void PrepareInventoryCapture(){inventoryObserver=new LoginConnection();inventoryCapture=new InventoryCapture(login,inventoryObserver);}
    async Task InventoryDemo(){
        var s=JsonSerializer.Deserialize<InventoryCase>(File.ReadAllText(Environment.GetEnvironmentVariable("P24_INVENTORY_CASE")!))!;using var o=inventoryObserver??throw new Exception("inventory capture must attach before StartLogin");var log=inventoryCapture!;
        async Task Shot(string name){if(web!=null){await web.ExecuteScriptAsync("document.getElementById('inventoryPanel').scrollIntoView({block:'start'})");await Task.Delay(120);}RefreshScene();Update();CaptureEvidence(name);if(web!=null){await using var file=File.Create(Path.Combine(evidence!,name+"-web.png"));await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,file);}log.Check("ui-"+name,new{kind="actual WinForms controllers and WebView2 DOM; not external native input",gameVisible=inventoryPanel.Visible,dom=web==null?"":await web.ExecuteScriptAsync("document.getElementById('inventoryPanel').innerText")});}
        async Task Select(uint id){SyncInventory();var item=inventoryList.Items.Cast<InventoryItem>().Single(i=>i.ObjectId==id);inventoryList.SelectedItem=item;await Task.Delay(50);if(login.World.SelectedItemId!=id)throw new Exception("native item selection");}
        async Task<bool> Use(string mode){
            int sent=0;void Sent(object p)=>sent++;login.ItemSent+=Sent;
            try{
                if(mode=="web-healing"){
                    uint id=login.World.SelectedItemId!.Value;await web!.ExecuteScriptAsync($"document.querySelector('[data-item-id=\"{id}\"]').click()");await Task.Delay(80);await web.ExecuteScriptAsync("document.getElementById('useItem').click()");
                }else if(mode=="game-healing"){
                    ReturnGameFocus();OnKeyDown(new KeyEventArgs(Keys.U));OnKeyDown(new KeyEventArgs(Keys.U));await Task.Delay(50);OnKeyUp(new KeyEventArgs(Keys.U));
                }else {SyncInventory();useItemButton.PerformClick();}
                await Until(()=>sent==1,4000);await Task.Delay(50);if(sent!=1)throw new Exception("held/repeat duplicate");return true;
            }finally{login.ItemSent-=Sent;}
        }
        async Task FocusNegative(){int sent=0;void Sent(object p)=>sent++;login.ItemSent+=Sent;try{await FocusWebInput();OnKeyDown(new KeyEventArgs(Keys.U));OnKeyUp(new KeyEventArgs(Keys.U));await Task.Delay(100);if(sent!=0)throw new Exception("U typing used medicine");log.Check("web-input-U-isolated");ReturnGameFocus();}finally{login.ItemSent-=Sent;}}
        try{
            await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);
            charactersBox.SelectedItem=s.Character;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);
            if(s.ReviewOnly){await Until(()=>login.World.InventoryLoaded);if(login.World.Inventory.Any(i=>i.SupportedPotion))throw new Exception("review expected depleted inventory");lastWebState="";PublishWebState();await Task.Delay(200);string reviewDom=await web!.ExecuteScriptAsync("document.getElementById('inventoryPanel').innerText");if(!reviewDom.Contains("金幣")||!reviewDom.Contains("鑑定卷軸"))throw new Exception("original inventory names not ready before first render");log.Check("read-only-post-depletion-review");await Shot("inventory-saved-depleted-visible");log.Save(s.Output,"pass");return;}
            await InventoryScenario.Run(login,o,s,log,Select,Use,Shot,FocusNegative,id=>{ClickScene(VisiblePoint(id));if(login.World.TargetId!=id)throw new Exception("actual target pixel selection");return Task.CompletedTask;});log.Save(s.Output,"pass");
        }catch(Exception ex){NetworkExitCode=6;log.Save(s.Output,"fail",ex);}finally{login.Cancel();o.Cancel();Close();}
    }
}
