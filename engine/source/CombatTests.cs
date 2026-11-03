using System.Text.Json;
using System.Buffers.Binary;
namespace P06;
public static class CombatTests {
    public static async Task NaturalRecovery(LoginConnection c,string output,string phase){
        if(c.State!=LoginState.InWorld||c.World.Status is not {Hp:>0}||c.World.OwnDead)throw new Exception("natural recovery requires a live connected role with original HP");
        uint required=Math.Min(c.World.Status.MaxHp,500u);var before=c.CaptureWorld(()=>c.Snapshot());await ObjectTests.WalkTo(c,32588,32930);long start=Environment.TickCount64;
        while(true){
            var vital=c.World.Status;if(c.State!=LoginState.InWorld||vital==null)throw new Exception("original connection ended during natural HP recovery: "+c.State);
            if(vital.Hp==0||c.World.OwnDead||Environment.TickCount64-start>900000)throw new Exception("bounded original natural HP recovery incomplete");
            if(vital.Hp>=required)break;
            Json.Save(output+".recovery-progress.json",new{phase,requiredHp=required,before,latest=c.CaptureWorld(()=>c.Snapshot()),elapsedMs=Environment.TickCount64-start,method="normal C_Move to safe point then only original heartbeat/HP packets; no healing request or SQL"});await Task.Delay(1000);
        }
        Json.Save(output+"."+phase+"-recovery.json",new{phase,requiredHp=required,before,after=c.CaptureWorld(()=>c.Snapshot()),elapsedMs=Environment.TickCount64-start,method="original natural HP recovery"});
    }
    public static async Task Approach(LoginConnection c,uint id){
        for(int n=0;n<90;n++){
            var a=c.World.Actor!;var t=c.World.Objects.Single(x=>x.ObjectId==id);if(Math.Max(Math.Abs(a.X-t.X),Math.Abs(a.Y-t.Y))<=1)return;
            var candidates=Enumerable.Range(0,8).Select(h=>(X:t.X+MovementRules.Dx[h],Y:t.Y+MovementRules.Dy[h])).Where(p=>Coordinates.Inside(p.X,p.Y)&&!c.World.Objects.Any(o=>!o.Dead&&o.X==p.X&&o.Y==p.Y)).OrderBy(p=>Math.Abs(a.X-p.X)+Math.Abs(a.Y-p.Y)).ToArray();
            if(candidates.Length==0)throw new Exception("no open adjacent target cell");
            await ObjectTests.WalkTo(c,candidates[0].X,candidates[0].Y);
        }throw new Exception("moving target approach exhausted");
    }
    public static async Task Probe(string path){
        var s=JsonSerializer.Deserialize<MovementCase>(File.ReadAllText(path))!;
        using var c=new LoginConnection();using var o=new LoginConnection();var packets=new List<object>();var observerPackets=new List<object>();var sent=new List<object>();var observerSent=new List<object>();var checks=new List<object>();int cg=0,og=0;
        c.PacketReceived+=p=>{if(JsonSerializer.SerializeToElement(p).S("kind")=="seed")cg++;packets.Add(new{generation=cg,utc=DateTimeOffset.UtcNow,packet=p});};
        o.PacketReceived+=p=>{if(JsonSerializer.SerializeToElement(p).S("kind")=="seed")og++;observerPackets.Add(new{generation=og,utc=DateTimeOffset.UtcNow,packet=p});};
        c.CombatSent+=p=>sent.Add(new{kind="attack",generation=cg,data=p});c.MovementSent+=p=>sent.Add(new{kind="movement",generation=cg,data=p});o.MovementSent+=p=>observerSent.Add(new{generation=og,data=p});
        object Capture(string phase)=>c.CaptureWorld(()=>o.CaptureWorld(()=>new{phase,main=c.Snapshot(),observer=o.Snapshot(),mainGeneration=cg,observerGeneration=og,mainPacketCount=packets.Count,observerPacketCount=observerPackets.Count,mainSentCount=sent.Count,observerSentCount=observerSent.Count,mainSequence=c.AppliedPacketSequence,observerSequence=o.AppliedPacketSequence}));
        try {
            await ObjectTests.Enter(c,s.Account,s.Password,s.Character);await ObjectTests.Enter(o,s.ObserverAccount,s.Password,s.ObserverCharacter);
            checks.Add(Capture("initial-saved-positions"));
            await NaturalRecovery(c,s.Output,"before");checks.Add(Capture("natural-recovery-before-combat"));
            if(await c.Attack())throw new Exception("attack accepted without target");
            await ObjectTests.WalkTo(c,32591,32945);await ObjectTests.WalkTo(o,32590,32946);
            uint self=c.World.Actor!.ObjectId,observer=o.World.Actor!.ObjectId;
            await ObjectTests.Wait(()=>c.World.Objects.Any(v=>v.ObjectId==observer&&v.X==o.World.Actor.X&&v.Y==o.World.Actor.Y)&&o.World.Objects.Any(v=>v.ObjectId==self&&v.X==c.World.Actor.X&&v.Y==c.World.Actor.Y));
            await ObjectTests.Wait(()=>c.World.Objects.Any(v=>v.Gfx==1110&&!v.Dead),90000);var target=c.World.Objects.First(v=>v.Gfx==1110&&!v.Dead);uint id=target.ObjectId;
            checks.Add(Capture("mutual-wire-visible-measurement-start"));
            if(Math.Max(Math.Abs(target.X-c.World.Actor.X),Math.Abs(target.Y-c.World.Actor.Y))<=1)throw new Exception("far-case requires actual distance >1");
            c.World.SelectTarget(id);await ObjectTests.Wait(()=>!c.MovementBusy);long reports=c.World.AttackReports;
            if(!await c.Attack())throw new Exception("far selected request not sent");
            await Task.Delay(CombatRules.AttackMs+400);checks.Add(new{kind="original-out-of-range-observation",targetBefore=target,attacksBefore=reports,state=Capture("after-far-request")});
            await Approach(c,id);await ObjectTests.Wait(()=>!c.MovementBusy);c.World.SelectTarget(id);
            uint hp=c.World.Status!.Hp;long first=c.World.AttackReports;
            if(!await c.Attack())throw new Exception("single melee not sent");
            if(await c.Attack()||await c.Move(0))throw new Exception("busy input queued/sent");
            await ObjectTests.Wait(()=>c.World.AttackReports>first&&c.World.LastAttack!=null);
            await ObjectTests.Wait(()=>c.World.Status!.Hp<hp||c.World.OwnDead,20000);
            if(c.World.OwnDead)throw new Exception("fixture player defeated before intended combat completion");
            checks.Add(new{kind="original-retaliation-HP-decrease",beforeHp=hp,afterHp=c.World.Status!.Hp,state=Capture("retaliation")});
            int attempts=1;
            while(c.World.Objects.Any(v=>v.ObjectId==id&&!v.Dead)){
                if(++attempts>60)throw new Exception("bounded single attack test attempts exhausted");
                await ObjectTests.Wait(()=>!c.MovementBusy);if(c.World.TargetId==null)break;
                if(!await c.Attack()){if(c.World.Status!.Hp==0)throw new Exception("original NPC retaliation reduced self HP to zero; combat stopped");if(c.World.TargetId==null)break;throw new Exception("explicit single attack not sent: "+c.World.CombatNotice);}
                await Task.Delay(CombatRules.AttackMs+50);
                if(c.State!=LoginState.InWorld||c.World.OwnDead)throw new Exception("combat disconnected or player died");
            }
            var corpse=c.World.Objects.FirstOrDefault(v=>v.ObjectId==id);if(corpse?.Dead!=true)throw new Exception("missing original corpse action before removal");
            if(c.World.TargetId!=null||await c.Attack())throw new Exception("dead target remained attackable");checks.Add(new{kind="original-death-corpse-target-cleared",id,attempts,corpse,state=Capture("corpse")});
            await ObjectTests.Wait(()=>c.World.Objects.All(v=>v.ObjectId!=id),30000);checks.Add(Capture("original-remove"));
            await ObjectTests.Wait(()=>!c.MovementBusy);int h=Enumerable.Range(0,8).First(d=>MovementRules.Passable(c.World.Actor!.X,c.World.Actor.Y,d)&&!c.World.Objects.Any(v=>v.X==c.World.Actor.X+MovementRules.Dx[d]&&v.Y==c.World.Actor.Y+MovementRules.Dy[d]));
            if(!await c.Move(h))throw new Exception("postcombat movement failed");await ObjectTests.Wait(()=>!c.MovementBusy);checks.Add(Capture("postcombat-movement"));
            await NaturalRecovery(c,s.Output,"after");checks.Add(Capture("natural-recovery-after-combat"));
            var endpoint=c.World.Actor;c.Cancel();await Task.Delay(2500);await ObjectTests.Enter(c,s.Account,s.Password,s.Character);
            if(c.World.TargetId!=null||c.World.AttackReports!=0||c.World.OwnCombat!=null||c.World.Actor!.X!=endpoint!.X||c.World.Actor.Y!=endpoint.Y)throw new Exception("reconnect ghost or endpoint mismatch");
            int sentBefore=sent.Count;await Task.Delay(CombatRules.AttackMs+300);if(sent.Count!=sentBefore)throw new Exception("ghost resend");checks.Add(Capture("reconnected-no-ghost"));
            var result=c.CaptureWorld(()=>o.CaptureWorld(()=>new{status="pass",checks=checks.ToArray(),packets=packets.ToArray(),observerPackets=observerPackets.ToArray(),sent=sent.ToArray(),observerSent=observerSent.ToArray(),final=c.Snapshot(),observerFinal=o.Snapshot(),captureBoundary=new{mainGeneration=cg,observerGeneration=og,mainSequence=c.AppliedPacketSequence,observerSequence=o.AppliedPacketSequence},testDriver="bounded explicit single commands; no product autoattack; normal C_Move setup; no SQL dynamic changes"}));Json.Save(s.Output,result);
        }catch(Exception ex){var failure=c.CaptureWorld(()=>o.CaptureWorld(()=>new{status="fail",error=ex.ToString(),checks=checks.ToArray(),packets=packets.ToArray(),observerPackets=observerPackets.ToArray(),sent=sent.ToArray(),observerSent=observerSent.ToArray(),last=c.Snapshot(),observer=o.Snapshot(),mainGeneration=cg,observerGeneration=og}));Json.Save(s.Output,failure);throw;}
        finally{c.Cancel();o.Cancel();}
    }
}
