using System.Buffers.Binary;
using System.Text.Json;
namespace P06;
public sealed partial class LoginConnection {
    internal static async Task MagicTargetTests(string output){
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var fixture=Json.Read(Path.Combine(root,"fixtures/magic-r3-hidden-final.json"));
        var checks=new List<object>();void Check(bool ok,string name,object? detail=null){if(!ok)throw new Exception(name);checks.Add(new{name,detail});}
        using var c=new LoginConnection();using var o=new LoginConnection();
        byte[] Replay(LoginConnection connection,string packetKey,string actorKey){
            var actor=fixture.GetProperty(actorKey);string name=actor.S("Name");uint id=actor.GetProperty("ObjectId").GetUInt32();bool entered=false;byte[] own=[];
            foreach(var v in fixture.GetProperty(packetKey).EnumerateArray()){
                var b=Convert.FromHexString(v.GetString()!);if(b[0]==223)entered=true;
                if(entered){connection.World.ProbeSent=true;connection.World.Receive(b,name);}
                if(b[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5))==id)own=(byte[])b.Clone();
            }
            BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(1),actor.GetProperty("X").GetUInt16());BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(3),actor.GetProperty("Y").GetUInt16());own[12]=actor.GetProperty("Direction").GetByte();connection.World.Receive(own,name);connection.State=LoginState.InWorld;return own;
        }
        byte[] own=Replay(c,"packets","actor");Replay(o,"observerPackets","observerActor");string selected=c.World.Actor!.Name;
        foreach(var saved in fixture.GetProperty("objects").EnumerateArray()){
            var live=c.World.Objects.Single(v=>v.ObjectId==saved.GetProperty("ObjectId").GetUInt32());Check(live.X==saved.GetProperty("X").GetUInt16()&&live.Y==saved.GetProperty("Y").GetUInt16()&&live.Direction==saved.GetProperty("Direction").GetByte(),"original failed object position "+live.ObjectId);
        }
        var assets=new Assets(Path.Combine(root,"assets"));using var renderer=new Renderer(assets);var catalog=new ObjectAssets(assets.Root);var world=new World(assets);
        Check(true,"r6 native selection and clear use actual hidden form pixel gates",ClientForm.MagicSelectionRegression(assets,output+".native-selection"));
        Patch Compose(){world.Teleport(c.World.Actor!.X,c.World.Actor.Y);world.Face(c.World.Actor.Direction);return renderer.ComposeObjects(world,c.World,catalog,out _,out _,Environment.TickCount64+4000,historicalUntriggeredReplay:true);}
        uint[] Visible(Patch p)=>p.EntityOwners!.Where(v=>v.HasValue).Select(v=>v!.Value).Distinct().ToArray();
        var patch=Compose();var wolf=c.World.Objects.Single(v=>v.Gfx==1110&&!v.Dead);uint target=wolf.ObjectId;
        Check(target==2000042184&&wolf.X==32597&&wolf.Y==32949&&wolf.Direction==5,"real failed wolf final state");
        Check(!patch.EntityOwners!.Contains(target)&&MagicTargetPreparation.Choose(c.World,o.World,Visible(patch))==null,"zero-visible real candidate rejected before selection");
        int sends=0;c.sendMovementGate=(b,v,a)=>{if(!v())return Task.FromResult(false);Check(b[0]==6,"only admitted C6 test transport");sends++;a();return Task.FromResult(true);};
        c.World.SelectMagic(4);c.World.SelectTarget(null);
        var hidden=MagicTargetPreparation.Choose(c.World,o.World,Visible(patch));if(hidden!=null)c.World.SelectTarget(hidden.ObjectId);
        Check(!await c.CastSelectedMagic()&&sends==0&&c.World.TargetId==null,"hidden candidate produces no selection or C6 through original cast gate");
        // Explicit derived state: relocate only this offline replay model to a planned original-pose pair.
        var plan=MagicTargetPreparation.Plans().First();Check(plan.X==32600&&plan.Y==32949&&plan.TargetX==32600&&plan.TargetY==32948,"original map highest-visibility lure plan");
        int[] path=ObjectTests.Route(c,plan.X,plan.Y,[]);int x=c.World.Actor.X,y=c.World.Actor.Y;foreach(int h in path){Check(MovementRules.Passable(x,y,h),"original terrain plan step");x+=MovementRules.Dx[h];y+=MovementRules.Dy[h];}
        Check(path.Length>0&&path.Length<=MagicTargetPreparation.MaxMoves&&x==plan.X&&y==plan.Y,"failed-state waypoint reachable through original terrain",new{path,mainX=plan.X,mainY=plan.Y,targetX=plan.TargetX,targetY=plan.TargetY,ratio=plan.Ratio});
        byte[] npc=fixture.GetProperty("packets").EnumerateArray().Select(v=>Convert.FromHexString(v.GetString()!)).Last(b=>b[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5))==target);
        BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(1),(ushort)plan.X);BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(3),(ushort)plan.Y);c.World.Receive(own,selected);
        uint visibleTarget=target+1;BinaryPrimitives.WriteUInt32LittleEndian(npc.AsSpan(5),visibleTarget);
        BinaryPrimitives.WriteUInt16LittleEndian(npc.AsSpan(1),(ushort)plan.TargetX);BinaryPrimitives.WriteUInt16LittleEndian(npc.AsSpan(3),(ushort)plan.TargetY);npc[12]=4;c.World.Receive(npc,selected);o.World.Receive(npc,o.World.Actor!.Name);
        patch=Compose();int pixels=patch.EntityOwners!.Count(v=>v==visibleTarget);Check(pixels>0,"derived planned wolf has actual original-compositor selectable pixels",new{pixels});
        Check(!patch.EntityOwners!.Contains(target),"original hidden wolf remains hidden beside derived visible candidate");
        var chosen=MagicTargetPreparation.Choose(c.World,o.World,Visible(patch),target);Check(chosen?.ObjectId==visibleTarget,"visible candidate wins over first hidden and preferred hidden object");
        int pixel=Array.FindIndex(patch.EntityOwners!,id=>id==visibleTarget);Check(pixel>=0&&patch.EntityOwners![pixel]==chosen!.ObjectId,"existing actual pixel ownership gate resolves selected target");
        c.World.SelectTarget(chosen!.ObjectId);Check(await c.CastSelectedMagic()&&sends==1,"derived visible selection admits one original cast gate request");
        Check(MagicTargetPreparation.Choose(c.World,o.World,[])==null,"later occlusion re-evaluation cannot reuse selected ID");
        var lowHp=Convert.FromHexString(File.ReadLines(Path.Combine(root,"reference/magic/oracle.tsv")).Select(l=>l.Split('\t')).First(v=>v[0]=="hp")[2]);BinaryPrimitives.WriteUInt32LittleEndian(lowHp.AsSpan(1),149);c.World.Receive(lowHp,selected);
        var preparation=new MagicTargetPreparation(c,o,new InventoryCapture(c,o),()=>[]);bool stopped=false;try{await preparation.Prepare();}catch(Exception ex) when(ex.Message=="finite visible-target preparation stopped on actual damage/state/deadline"){stopped=true;}
        Check(stopped&&sends==1,"real preparation controller low-HP guard stops before any movement or cast");
        var health=new MagicPreparationHealth(521,700);Check(health.Observe(521,700)&&health.Observe(524,700)&&!health.Observe(522,700),"main HP 521 to 524 to 522 stops despite remaining above initial HP");
        health=new MagicPreparationHealth(521,680);Check(health.Observe(521,683)&&!health.Observe(521,681),"observer recovery followed by damage also stops above initial HP");
        health=new MagicPreparationHealth(521,700);Check(health.Observe(521,700)&&health.Observe(524,700)&&health.Observe(524,700),"unchanged HP and original natural recovery remain admitted");
        Check(MagicTargetPreparation.MaxWaypoints==3&&MagicTargetPreparation.MaxMoves==60&&MagicTargetPreparation.MaxMilliseconds==45000,"finite preparation limits");
        var r4=Json.Read(Path.Combine(root,"fixtures/magic-r4-staging-sequence.json"));fixture=r4.GetProperty("beforeFirstBolt");c.Cancel();o.Cancel();own=Replay(c,"packets","actor");byte[] observerOwn=Replay(o,"observerPackets","observerActor");selected=c.World.Actor!.Name;
        Check(c.World.Actor.X==32595&&c.World.Actor.Y==32948&&c.World.Status!.Hp==539,"r4 original before-first-bolt snapshot replayed");
        var stage=new MagicStagingGate();patch=Compose();Check(stage.Choose(c.World,o.World,Visible(patch))==null,"initial actual visible pixels cannot bypass missing staging");
        plan=MagicTargetPreparation.Plans().First();int ox=32598,oy=32947;stage.Plan(plan.X,plan.Y,ox,oy);
        npc=fixture.GetProperty("packets").EnumerateArray().Select(v=>Convert.FromHexString(v.GetString()!)).Last(b=>b[0]==87&&BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(9))==1110);target=BinaryPrimitives.ReadUInt32LittleEndian(npc.AsSpan(5));
        // Explicit synthetic boundary for an offline admitted case: original r4 object/actor packets at the source-backed plan.
        BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(1),(ushort)plan.X);BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(3),(ushort)plan.Y);c.World.Receive(own,selected);
        BinaryPrimitives.WriteUInt16LittleEndian(npc.AsSpan(1),(ushort)plan.TargetX);BinaryPrimitives.WriteUInt16LittleEndian(npc.AsSpan(3),(ushort)plan.TargetY);npc[12]=4;c.World.Receive(npc,selected);o.World.Receive(npc,o.World.Actor!.Name);
        patch=Compose();Check(patch.EntityOwners!.Contains(target),"derived r4 target uses positive real compositor pixels");
        Check(!stage.Complete(c.World.Actor!.X,c.World.Actor.Y,o.World.Actor!.X,o.World.Actor.Y)&&stage.Choose(c.World,o.World,Visible(patch))==null,"main alone at plan cannot admit first bolt before observer completes staging");
        int admitted=0,transport=0;c.sendMovementGate=(b,v,a)=>{if(!v())return Task.FromResult(false);Check(b[0]==6&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(3))==target,"r5 test transport exact original C6 target");transport++;a();return Task.FromResult(true);};c.World.SelectMagic(4);
        async Task<bool> StagedCast(){var choice=stage.Choose(c.World,o.World,Visible(Compose()));if(choice==null)return false;admitted++;c.World.SelectTarget(choice.ObjectId);return await c.CastSelectedMagic();}
        Check(!await StagedCast()&&admitted==0&&transport==0,"temporary visible candidate before full staging sends no first C6");
        BinaryPrimitives.WriteUInt16LittleEndian(observerOwn.AsSpan(1),(ushort)ox);BinaryPrimitives.WriteUInt16LittleEndian(observerOwn.AsSpan(3),(ushort)oy);o.World.Receive(observerOwn,o.World.Actor!.Name);c.World.Receive(observerOwn,selected);o.World.Receive(own,o.World.Actor!.Name);
        Check(stage.Complete(c.World.Actor!.X,c.World.Actor.Y,o.World.Actor!.X,o.World.Actor.Y)&&await StagedCast()&&admitted==1&&transport==1,"completed derived staging plus current real pixels admits one first bolt",new{main=c.World.Actor,observer=o.World.Actor,target,mainX=plan.X,mainY=plan.Y,observerX=ox,observerY=oy});
        health=new MagicPreparationHealth(r4.GetProperty("initialPreparationHp").GetUInt32(),o.World.Status!.Hp);bool damage=false;
        foreach(var packet in r4.GetProperty("hpPackets").EnumerateArray()){var b=Convert.FromHexString(packet.S("plainHex"));c.World.Receive(b,selected);if(!health.Observe(c.World.Status!.Hp,o.World.Status.Hp)){damage=true;stage.Stop();}}
        Check(damage&&c.World.Status!.Hp==526&&!await StagedCast()&&admitted==1&&transport==1,"actual r4 S225 HP539 to 526 latches stop; even positive pixels cannot send second C6");
        fixture=r4.GetProperty("afterFailure");c.Cancel();o.Cancel();_=Replay(c,"packets","actor");_=Replay(o,"observerPackets","observerActor");patch=Compose();
        Check(!patch.EntityOwners!.Contains(target),"r4 actual final wolf under original roof has zero settled selectable pixels");
        stage=new MagicStagingGate();stage.Plan(c.World.Actor!.X,c.World.Actor.Y,o.World.Actor!.X,o.World.Actor.Y);Check(stage.Complete(c.World.Actor.X,c.World.Actor.Y,o.World.Actor.X,o.World.Actor.Y),"explicit offline completed-position boundary isolates hidden gate");
        Check(!await StagedCast()&&admitted==1&&transport==1,"actual hidden r4 final target cannot send a second C6 even with position flag complete");
        Json.Save(output,new{status="pass",scope="Explicit historical P22 untriggered-state replay of actual r3 and r4 failed packet sequences, explicit final own-position replay boundaries and synthetic staged visible pairs. Original compositor, staging and cast gates; transport counter only. Settled frames are not contemporaneous screenshots. Includes offline native ClickScene/PickObject clear with child-control exclusion; actual Web DOM remains a live acceptance requirement. No live login or success claim.",checks});
    }
}
