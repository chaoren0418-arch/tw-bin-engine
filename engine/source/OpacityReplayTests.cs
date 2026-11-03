using System.Buffers.Binary;
using System.Text.Json;
namespace P06;
public sealed partial class LoginConnection {
    internal void OpacityReplayTransport(Action<byte[]> sent){State=LoginState.InWorld;sendMovementGate=(b,validate,apply)=>{if(!validate())return Task.FromResult(false);sent(b);apply();return Task.FromResult(true);};}
}
public static class OpacityReplayTests {
    public static void Run(string output){
        Directory.CreateDirectory(output);string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var a=new Assets(Path.Combine(root,"assets"));var catalog=new ObjectAssets(a.Root);var results=new List<object>();
        foreach(var item in Json.Read(Path.Combine(root,"reference/opacity/real-failure-native-states.json")).GetProperty("cases").EnumerateArray()){
            var f=Json.Read(Path.Combine(root,item.S("fixture")));if(item.GetProperty("part").ValueKind==JsonValueKind.String)f=f.GetProperty(item.S("part"));var actor=f.GetProperty("actor");string name=actor.S("Name");uint self=actor.GetProperty("ObjectId").GetUInt32();using var connection=new LoginConnection();var s=connection.World;long clock=0;s.SceneClock=()=>clock;byte[] own=[];bool entered=false;
            foreach(var v in f.GetProperty("packets").EnumerateArray()){byte[] b=Convert.FromHexString(v.GetString()!);if(b[0]==223)entered=true;if(entered){s.ProbeSent=true;s.Receive(b,name);}if(b[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5))==self)own=(byte[])b.Clone();}
            BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(1),actor.GetProperty("X").GetUInt16());BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(3),actor.GetProperty("Y").GetUInt16());own[12]=actor.GetProperty("Direction").GetByte();s.Receive(own,name);for(clock=50;clock<=1000;clock+=50)s.AdvanceScene();clock=1000; /* Historical settled fixture: explicit continuous50ms pumps. */
            var actual=JsonSerializer.SerializeToElement(s.Opacity!.Snapshot());var expected=item.GetProperty("expected");static string[] Rows(JsonElement e)=>e.GetProperty("states").EnumerateArray().Select(x=>JsonSerializer.Serialize(x)).Order().ToArray();
            if(!Rows(actual).SequenceEqual(Rows(expected)))throw new Exception("real-failure original native state mismatch "+item.S("name"));
            using var renderer=new Renderer(a);var world=new World(a);world.Teleport(s.Actor!.X,s.Actor.Y);world.Face(s.Actor.Direction);long frame=Environment.TickCount64+4000;var before=renderer.ComposeObjects(world,s,catalog,out _,out _,frame,true);var bp=before.EntityOwners!.Where(v=>v.HasValue).GroupBy(v=>v!.Value).ToDictionary(g=>g.Key,g=>g.Count());var center=Coordinates.Screen(s.Actor.X,s.Actor.Y);var crop=new Rectangle((int)center.X-360,(int)center.Y-240,720,480);
            using(var image=renderer.SceneView(before,crop))image.Save(Path.Combine(output,item.S("name")+"-historical-untriggered.png"));
            var after=renderer.ComposeObjects(world,s,catalog,out var visuals,out var missing,frame);var ap=after.EntityOwners!.Where(v=>v.HasValue).GroupBy(v=>v!.Value).ToDictionary(g=>g.Key,g=>g.Count());using(var image=renderer.SceneView(after,crop))image.Save(Path.Combine(output,item.S("name")+"-original-triggered.png"));
            var targets=s.Objects.Where(o=>o.Gfx==1110).Select(o=>new{o.ObjectId,o.X,o.Y,o.Direction,beforePixels=bp.GetValueOrDefault(o.ObjectId),afterPixels=ap.GetValueOrDefault(o.ObjectId)}).ToArray();
            if(targets.Any(t=>t.afterPixels<=0)||targets.Where(t=>t.beforePixels==0).Any(t=>t.afterPixels<=0))throw new Exception("original triggered replay failed to reveal expected wolf");
            // Explicit current-state negative: a source-proven no-trigger neighboring cell, no live teleport.
            BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(1),32593);BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(3),32948);s.Receive(own,name);for(clock=1050;clock<=2000;clock+=50)s.AdvanceScene();clock=2000; /* Explicit historical pump schedule, not one late call. */world.Teleport(32593,32948);
            var negative=renderer.ComposeObjects(world,s,catalog,out _,out _,frame);var hidden=targets.Where(t=>t.beforePixels==0).Select(t=>new{id=t.ObjectId,pixels=negative.EntityOwners!.Count(v=>v==t.ObjectId)}).ToArray();
            if(hidden.Any(t=>t.pixels!=0))throw new Exception("source no-trigger still-occluded counterexample changed");
            var visibleIds=negative.EntityOwners!.Where(v=>v.HasValue).Select(v=>v!.Value).ToArray();if(hidden.Any(t=>visibleIds.Contains(t.id)))throw new Exception("hidden target in visible gate");s.SelectMagic(4);s.SelectTarget(null);int c6=0;connection.OpacityReplayTransport(packet=>{if(packet[0]==6)c6++;});bool selectedHidden=false;foreach(var t in hidden)if(visibleIds.Contains(t.id)){s.SelectTarget(t.id);selectedHidden=true;}
            bool cast=connection.CastSelectedMagic().GetAwaiter().GetResult();if(cast||selectedHidden||s.TargetId!=null||c6!=0)throw new Exception("hidden replay admitted selection/action");
            using(var image=renderer.SceneView(negative,crop))image.Save(Path.Combine(output,item.S("name")+"-current-no-trigger-hidden.png"));
            results.Add(new{name=item.S("name"),originalNativeStateEqual=true,originalActor=actor,opacity=actual,targets,visuals,missing,currentNoTriggerNegative=new{derivedOwn=s.Actor,hidden,selectedHidden,cast,c6,sourceCellHasNoEntries=!OpacityCatalog.Shared.Value.Cells.ContainsKey((34,52))},backgroundUpdate=renderer.LastBackgroundUpdate});
        }
        Json.Save(Path.Combine(output,"result.json"),new{status="pass",kind="Original P22 failed raw replay, explicit final predicted own position and settled synthetic time; not P24 live success",results});
    }
}
