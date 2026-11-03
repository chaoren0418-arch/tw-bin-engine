using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    Point VisiblePoint(uint id){
        RefreshScene();Update();var p=objectPatch??throw new Exception("no object patch");
        var candidates=new List<Point>();
        for(int y=0;y<p.Bounds.Height;y++)for(int x=0;x<p.Bounds.Width;x++)if(p.EntityOwners![y*p.Bounds.Width+x]==id){var v=Camera.ToView(new PointF(p.Bounds.X+x+.5f,p.Bounds.Y+y+.5f));if(v.X>2&&v.X<Camera.View.Width-2&&v.Y>150&&v.Y<Camera.View.Height+Camera.ViewOffset.Y-2)candidates.Add(new((int)v.X,(int)v.Y));}
        if(candidates.Count==0)throw new Exception("no visible pickable pixels for "+id);return candidates[candidates.Count/2];
    }
    async Task<WorldObject> VisibleNpc(int gfx){
        long end=Environment.TickCount64+30000;
        while(Environment.TickCount64<end){foreach(var o in login.World.Objects.Where(o=>o.Gfx==gfx)){try{_=VisiblePoint(o.ObjectId);return o;}catch(Exception){}}await Task.Delay(100);}
        throw new Exception("no actually visible original NPC gfx "+gfx);
    }
    void ClickScene(Point p)=>OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));
    async Task<string> TargetDom()=>JsonSerializer.Deserialize<string>(await web!.ExecuteScriptAsync("document.getElementById('targetResult').innerText"))!;
    async Task ObjectDemo(){
        var spec=JsonSerializer.Deserialize<MovementCase>(File.ReadAllText(Environment.GetEnvironmentVariable("P24_OBJECT_CASE")!))!;
        using var other=new LoginConnection();var packets=new List<object>();var otherPackets=new List<object>();var checks=new List<object>();
        login.PacketReceived+=p=>packets.Add(new{utc=DateTimeOffset.UtcNow,packet=p});other.PacketReceived+=p=>otherPackets.Add(new{utc=DateTimeOffset.UtcNow,packet=p});
        try{
            await Until(()=>webReady,15000);await Until(()=>login.State is LoginState.CharacterList or LoginState.Faulted,15000);if(login.State!=LoginState.CharacterList)throw new Exception(login.Detail);
            charactersBox.SelectedIndex=0;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);await ObjectTests.Enter(other,spec.ObserverAccount,spec.Password,spec.ObserverCharacter);await ObjectTests.WalkTo(login,32589,32948);await ObjectTests.WalkTo(other,32587,32949);await Task.Delay(1500);
            uint otherId=other.World.Actor!.ObjectId;RefreshScene();Update();CaptureEvidence("initial-multiple");
            foreach(int gfx in new[]{945,4038}){
                var obj=await VisibleNpc(gfx);Point p=VisiblePoint(obj.ObjectId);ClickScene(p);await Task.Delay(120);
                string dom=await TargetDom();if(login.World.TargetId!=obj.ObjectId||!dom.Contains(obj.ObjectId.ToString())||!dom.Contains(objectAssets!.Name(obj.Name)))throw new Exception("NPC target/DOM mismatch");
                var before=objectVisuals.Single(v=>v.ObjectId==obj.ObjectId);CaptureEvidence("selected-gfx-"+gfx);await Task.Delay(370);RefreshScene();var after=objectVisuals.Single(v=>v.ObjectId==obj.ObjectId);if(before.Frame==after.Frame)throw new Exception("NPC breath did not animate");
                checks.Add(new{kind="original-NPC-pixel-selection-and-animation",objectId=obj.ObjectId,gfx,sprite=before.Sprite,point=p,dom,before,after});
            }
            ClickScene(VisiblePoint(otherId));await Task.Delay(120);if(login.World.TargetId!=otherId)throw new Exception("other pixel selection failed");
            await Until(()=>!other.MovementBusy,3000);int moveHeading=Enumerable.Range(0,8).First(h=>MovementRules.Passable(other.World.Actor!.X,other.World.Actor.Y,h)&&!other.World.Objects.Any(v=>v.X==other.World.Actor.X+MovementRules.Dx[h]&&v.Y==other.World.Actor.Y+MovementRules.Dy[h]));if(!await other.Move(moveHeading))throw new Exception("other UI move rejected");await Until(()=>login.World.Objects.Any(x=>x.ObjectId==otherId&&x.MotionAt>0),3000);await Task.Delay(180);RefreshScene();CaptureEvidence("other-walk-middle");var walking=objectVisuals.Single(v=>v.ObjectId==otherId);if(!walking.Walking||walking.Action!="walk")throw new Exception("no other walk animation");checks.Add(new{kind="other-player-original-wire-mid-animation",visual=walking,target=login.World.Target});
            await Until(()=>!other.MovementBusy,3000);await other.Move((moveHeading+4)%8);await Task.Delay(750);await other.Turn(7);await Until(()=>login.World.Objects.Single(x=>x.ObjectId==otherId).Direction==7,3000);CaptureEvidence("other-turn");
            other.Cancel();await Until(()=>login.World.Objects.All(x=>x.ObjectId!=otherId),5000);await Task.Delay(150);if(login.World.TargetId!=null||!(await TargetDom()).Contains("未選取"))throw new Exception("logout target/DOM not cleared");checks.Add(new{kind="selected-original-player-logout-clears",dom=await TargetDom()});
            var npc=await VisibleNpc(945);ClickScene(VisiblePoint(npc.ObjectId));ClickScene(new Point(10,160));await Task.Delay(100);if(login.World.TargetId!=null)throw new Exception("blank click retained target");
            ClickScene(VisiblePoint(npc.ObjectId));int epoch=login.World.WorldEpoch;login.Cancel();await Task.Delay(200);if(login.World.Objects.Length!=0||login.World.TargetId!=null||!(await TargetDom()).Contains("未選取"))throw new Exception("disconnect ghosts");
            await Task.Delay(2300);loginAccount.Text=spec.Account;loginPassword.Text=spec.Password;StartLogin();await Until(()=>login.State==LoginState.CharacterList,15000);charactersBox.SelectedIndex=0;enterWorld.PerformClick();await Until(()=>login.State==LoginState.InWorld,15000);await Task.Delay(700);if(login.World.TargetId!=null||login.World.WorldEpoch<=epoch)throw new Exception("reconnect selection leak");CaptureEvidence("reconnected-clean");
            await using(var f=File.Create(Path.Combine(evidence!,"objects-web.png")))await web!.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);
            checks.Add(new{kind="blank-cancel-reconnect-clear",snapshot=ObjectUiSnapshot()});
            var delivery=login.CaptureWorld(()=>other.CaptureWorld(()=>new{status="pass",kind="actual WinForms OnMouseDown/controller and WebView2 DOM; no external input claim",checks=checks.ToArray(),packets=packets.ToArray(),otherPackets=otherPackets.ToArray(),final=ObjectUiSnapshot(),captureBoundary=new{appliedPacketSequence=login.AppliedPacketSequence,method="worldCaptureGate and immutable packet arrays"}}));Json.Save(Path.Combine(evidence!,"result.json"),delivery);
        }catch(Exception ex){NetworkExitCode=3;Json.Save(Path.Combine(evidence!,"result.json"),new{status="fail",error=ex.ToString(),checks,packets,otherPackets,snapshot=login.Snapshot(),visuals=objectVisuals});}
        finally{other.Cancel();login.Cancel();Close();}
    }
}
