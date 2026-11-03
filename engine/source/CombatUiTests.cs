using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    async Task CombatDemo(){
        var spec=JsonSerializer.Deserialize<MovementCase>(File.ReadAllText(Environment.GetEnvironmentVariable("P24_COMBAT_CASE")!))!;
        using var other=new LoginConnection();var packets=new List<object>();var otherPackets=new List<object>();var sent=new List<object>();var otherSent=new List<object>();var checks=new List<object>();int generation=1;
        login.PacketReceived+=p=>packets.Add(new{generation,utc=DateTimeOffset.UtcNow,packet=p});other.PacketReceived+=p=>otherPackets.Add(new{generation=1,utc=DateTimeOffset.UtcNow,packet=p});
        login.CombatSent+=p=>sent.Add(new{kind="attack",generation,data=p});login.MovementSent+=p=>sent.Add(new{kind="movement",generation,data=p});other.MovementSent+=p=>otherSent.Add(p);
        int AttackCount()=>sent.Count(x=>JsonSerializer.SerializeToElement(x).S("kind")=="attack");
        int OpaqueCount(Frame f)=>f.Blocks.SelectMany(b=>{var draw=new Draw(b.A,b.B,b.Z,0,"actor",0,-1,-1,b.Tile);return b.Tile.Pixels.Select(p=>(draw.Px+p.X,draw.Py+p.Y));}).Distinct().Count();
        void Shot(string name){RefreshScene();Update();
            var ownFrame=renderer.OwnCombatVisual is {} self?objectAssets!.Get(self.Gfx)!.Frames[self.Frame]:World.Frame;
            int visible=objectPatch?.Owners.Where((owner,i)=>owner==-2&&objectPatch.EntityOwners![i]==null).Count()??0;double ownRatio=(double)visible/OpaqueCount(ownFrame);
            var visibility=objectVisuals.Where(v=>v.Gfx==1110).Select(v=>new{v.ObjectId,v.Action,v.Frame,visible=objectPatch!.EntityOwners!.Count(x=>x==v.ObjectId),source=OpaqueCount(objectAssets!.Get(v.Gfx)!.Frames[v.Frame])}).ToArray();
            CaptureEvidence(name);checks.Add(new{kind=name,state=ObjectUiSnapshot(),selfVisiblePixels=visible,selfVisibleRatio=ownRatio,targetVisibility=visibility,sequence=login.AppliedPacketSequence,packetCount=packets.Count,sentCount=sent.Count});
            if(name is "original-own-attack" or "original-own-hit-HP" or "original-target-hit" or "original-death-corpse" or "original-death-later-frame")if(ownRatio<.6||visibility.Length==0||visibility.Any(v=>v.visible<.6*v.source))throw new Exception("main combat shape obscured; screenshot retained, not accepted: "+name);
        }
        try{
            await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);
            charactersBox.SelectedItem=spec.Character;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);
            await ObjectTests.Enter(other,spec.ObserverAccount,spec.Password,spec.ObserverCharacter);await CombatTests.NaturalRecovery(login,spec.Output,"before");await ObjectTests.WalkTo(login,32591,32945);await ObjectTests.WalkTo(other,32590,32946);
            await Until(()=>login.World.Objects.Any(v=>v.ObjectId==other.World.Actor!.ObjectId)&&other.World.Objects.Any(v=>v.ObjectId==login.World.Actor!.ObjectId),10000);
            await Until(()=>login.World.Objects.Any(v=>v.Gfx==1110&&!v.Dead),90000);var npc=login.World.Objects.First(v=>v.Gfx==1110&&!v.Dead);uint id=npc.ObjectId;
            await CombatTests.Approach(login,id);await Until(()=>!login.MovementBusy&&!World.Walking,10000);
            var visible=await VisibleNpc(1110);id=visible.ObjectId;await CombatTests.Approach(login,id);await Until(()=>!login.MovementBusy&&!World.Walking,10000);ClickScene(VisiblePoint(id));await Task.Delay(80);if(login.World.TargetId!=id||!(await TargetDom()).Contains(id.ToString()))throw new Exception("actual pixel selection/DOM failed");Shot("initial-partially-visible-werewolf");
            // NpcMove uses Euclidean home distance, checked after the movement broadcast.
            // Align at home before aggro, then use exact axial C_Move steps; a diagonal shortest path
            // could pull the original NPC to (32600,32947), sqrt(5)>2, and trigger its original teleport.
            bool NpcSettled(WorldObject v)=>v.MotionAt==0||Environment.TickCount64-v.MotionAt>=640; // original fixture move interval
            await ObjectTests.WalkTo(login,32599,32948);await Until(()=>!login.MovementBusy&&!World.Walking,10000);
            await Until(()=>login.World.Objects.Any(v=>v.ObjectId==id&&!v.Dead&&v.X==32598&&v.Y==32948&&NpcSettled(v))&&other.World.Objects.Any(v=>v.ObjectId==id&&v.X==32598&&v.Y==32948),90000);
            void AxisCheckpoint(string kind)=>checks.Add(login.CaptureWorld(()=>other.CaptureWorld(()=>new{kind,state=ObjectUiSnapshot(),sequence=login.AppliedPacketSequence,packetCount=packets.Count,sentCount=sent.Count,observer=other.Snapshot(),observerSequence=other.AppliedPacketSequence,observerPacketCount=otherPackets.Count})));
            // Original home teleport can Remove/CharPack the same ID and correctly clear selection.
            ClickScene(VisiblePoint(id));if(login.World.TargetId!=id)throw new Exception("home-return actual pixel reselection failed");
            AxisCheckpoint("original-axis-home-before-single-hit");
            if(!await login.Attack())throw new Exception("initial single hit to selected target rejected");await Until(()=>!login.MovementBusy,5000);
            foreach(int x in new[]{32600,32601}){
                if(login.World.Actor is not {Y:32948}||login.World.Actor.X!=x-1)throw new Exception("axis lure lost original setup position");
                if(!await login.Move(2))throw new Exception("normal east-axis C_Move rejected");await Until(()=>!login.MovementBusy&&!World.Walking,10000);
                await Until(()=>login.World.Objects.Any(v=>v.ObjectId==id&&v.X==x-1&&v.Y==32948&&NpcSettled(v))&&other.World.Objects.Any(v=>v.ObjectId==login.World.Actor!.ObjectId&&v.X==x&&v.Y==32948)&&other.World.Objects.Any(v=>v.ObjectId==id&&v.X==x-1&&v.Y==32948),10000);
                AxisCheckpoint("original-axis-east-"+x);
            }
            var stopped=login.World.Objects.Single(v=>v.ObjectId==id);
            await ObjectTests.WalkTo(login,32600,32947);await Until(()=>!login.MovementBusy&&!World.Walking,10000);
            await Until(()=>login.World.Objects.Any(v=>v.ObjectId==id&&v.X==32600&&v.Y==32948)&&other.World.Objects.Any(v=>v.ObjectId==login.World.Actor!.ObjectId&&v.X==32600&&v.Y==32947),5000);
            checks.Add(new{kind="normal-final-visible-position",targetBefore=stopped,actor=login.World.Actor,targetAfter=login.World.Objects.Single(v=>v.ObjectId==id)});
            ClickScene(VisiblePoint(id));RefreshScene();
            int ownVisible=objectPatch!.Owners.Where((owner,i)=>owner==-2&&objectPatch.EntityOwners![i]==null).Count();if(ownVisible<50)throw new Exception("main still occluded; cannot claim visible attack UI");Shot("selected-werewolf");
            int count=AttackCount();await FocusWebInput();OnKeyDown(new KeyEventArgs(Keys.G));OnKeyUp(new KeyEventArgs(Keys.G));await Task.Delay(150);if(AttackCount()!=count)throw new Exception("typing G attacked");checks.Add(new{kind="WebView-input-G-does-not-attack",count});
            await web!.ExecuteScriptAsync("window.scrollTo(0,0)");ReturnGameFocus();await Until(()=>!login.MovementBusy,5000);OnKeyDown(new KeyEventArgs(Keys.G));await Until(()=>AttackCount()==count+1,3000);OnKeyDown(new KeyEventArgs(Keys.G));Shot("original-own-attack");OnKeyUp(new KeyEventArgs(Keys.G));
            if(renderer.OwnCombatVisual?.Action!="attack")throw new Exception("own attack pixels absent");
            await Until(()=>login.World.Objects.Any(v=>v.ObjectId==id&&v.Combat?.Action==2),4000);Shot("original-target-hit");
            if(objectVisuals.Single(v=>v.ObjectId==id).Action!="damage")throw new Exception("target damage animation absent");
            uint hp=login.World.Status!.Hp;await Until(()=>login.World.OwnCombat?.Action==2&&login.World.Status!.Hp<hp,15000);Shot("original-own-hit-HP");
            if(renderer.OwnCombatVisual?.Action!="damage")throw new Exception("own damage animation absent");
            int attempts=0;
            while(login.World.Objects.Any(v=>v.ObjectId==id&&!v.Dead)){
                if(++attempts>60)throw new Exception("UI finite single attacks exhausted");await Until(()=>!login.MovementBusy&&!World.Walking,5000);
                if(login.World.TargetId==null)break;int before=AttackCount();await web!.ExecuteScriptAsync("document.getElementById('attack').click()");await Until(()=>AttackCount()>before||login.World.TargetId==null,4000);await Task.Delay(CombatRules.AttackMs+50);
                if(login.World.Status!.Hp==0)throw new Exception("original NPC defeated UI player");
            }
            if(!login.World.Objects.Any(v=>v.ObjectId==id&&v.Dead)||login.World.TargetId!=null)throw new Exception("original death/target clear missing");Shot("original-death-corpse");
            if(objectVisuals.Single(v=>v.ObjectId==id).Action!="die"||!(await TargetDom()).Contains("未選取"))throw new Exception("corpse pixels/DOM missing");
            double deathDuration=objectAssets!.Get(1110)!.Duration("die");
            await Until(()=>login.World.Objects.All(v=>v.ObjectId!=id)||login.World.Objects.Any(v=>v.ObjectId==id&&v.Combat is {} animation&&Environment.TickCount64-animation.At>=deathDuration),30000);
            if(login.World.Objects.Any(v=>v.ObjectId==id))Shot("original-death-later-frame");else checks.Add(new{kind="original-remove-before-later-frame",id,deathDuration});
            await Until(()=>login.World.Objects.All(v=>v.ObjectId!=id),30000);Shot("original-corpse-removed");
            int endCount=AttackCount();await web!.ExecuteScriptAsync("document.getElementById('attack').click()");await Task.Delay(150);if(AttackCount()!=endCount)throw new Exception("no target web attack sent");
            await using(var f=File.Create(Path.Combine(evidence!,"combat-web.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);
            await CombatTests.NaturalRecovery(login,spec.Output,"after");
            string dom=JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync("document.getElementById('combatResult').innerText"))!;
            var delivery=login.CaptureWorld(()=>other.CaptureWorld(()=>new{status="pass",kind="actual WinForms key/mouse controller and WebView2 DOM over original JAR, not external native input injection",checks=checks.ToArray(),packets=packets.ToArray(),observerPackets=otherPackets.ToArray(),sent=sent.ToArray(),observerSent=otherSent.ToArray(),final=ObjectUiSnapshot(),observerFinal=other.Snapshot(),dom,captureBoundary=new{mainSequence=login.AppliedPacketSequence,observerSequence=other.AppliedPacketSequence}}));Json.Save(Path.Combine(evidence!,"result.json"),delivery);
        }catch(Exception ex){try{Shot("failure");}catch{}NetworkExitCode=3;Json.Save(Path.Combine(evidence!,"result.json"),login.CaptureWorld(()=>other.CaptureWorld(()=>new{status="fail",error=ex.ToString(),checks=checks.ToArray(),packets=packets.ToArray(),observerPackets=otherPackets.ToArray(),sent=sent.ToArray(),observerSent=otherSent.ToArray(),snapshot=login.Snapshot(),visuals=objectVisuals,ownVisual=renderer.OwnCombatVisual})));}
        finally{other.Cancel();login.Cancel();Close();}
    }
}
