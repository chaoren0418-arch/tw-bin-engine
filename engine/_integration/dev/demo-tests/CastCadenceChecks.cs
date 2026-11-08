using P06;
using System.Reflection;

// T217 (#523, M7-Q): SV§1.1 C6 sends never start a local action or turn the caster;
// SV§1.2 spell reuse = own list.spr action 18/19 frame total x speed factor + skill extra delay.
// Synthetic packets/timing only (CombatAdmissionFixture); no game assets, sockets or server.
partial class Checks {
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,F)!.GetValue(o)!;
    static byte[] SpellReply(byte action)=>Packet(b=>{
        b.Write((byte)30);b.Write(action);b.Write(123u);b.Write(456u);b.Write((ushort)10);b.Write((byte)2);
        b.Write(338u);b.Write((ushort)167);b.Write((byte)0);b.Write((ushort)32600);b.Write((ushort)32940);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(0u);b.Write((byte)0);
    });

    static void CastCadenceFormula(){
        // Shape of a real profile (61/0 spellDir: server 1240, list.spr total 1290, haste per-frame 3/4 sum 967).
        var t=new ActionCadence(61,0,18,1240,1290,1290,707,10,100,false){HasteAnimationMs=967,HasteReleaseMs=530};
        Check(t.NativeCastMs==1290&&t.CastWaitMs==1300,"cast reuse = list.spr total + skill delay (no local margin)");
        Check((t with{Haste=true}).CastWaitMs==977,"haste cast reuse uses per-frame x3/4 list.spr total");
        Check((t with{Slow=true}).CastWaitMs==2590,"slow cast reuse doubles list.spr total");
        Check(t.CastSpeedWaitMs(2)==2590&&t.CastSpeedWaitMs(0)==1300&&t.CastSpeedWaitMs(1)==977,"speed-selected cast reuse");
        Check(t.WaitMs==1340,"melee/server cadence WaitMs unchanged");
        var shortSeq=t with{NativeBaseMs=600,HasteAnimationMs=450};
        Check(shortSeq.CastWaitMs==1240&&(shortSeq with{Haste=true}).CastWaitMs==936&&(shortSeq with{Slow=true}).CastWaitMs==2480,"server action window is only a floor");
        Check((t with{ReuseMs=2000}).CastWaitMs==3290,"skill extra delay adds to frame total");
    }

    static async Task CastCadenceChecks(){
        CastCadenceFormula();
        // No local predicted action, heading or pickup cancel on C6; server S30 drives the pose.
        using(var c=Seed()){
            LearnBolt(c);long now=50_000;Set(c,"ActionClock",(Func<long>)(()=>now));
            Feed(c,Pack(456,32601,32941));Check(c.World.SelectMagicTarget(456),"diagonal target selected");
            byte heading=c.World.Actor!.Direction;var before=c.World.OwnCombat;int sends=0;
            Sender(c,(bytes,valid,admit)=>{if(!valid())return Task.FromResult(false);sends++;admit();return Task.FromResult(true);});
            Check(await c.CastSelectedMagic()&&sends==1,"C6 sent");
            Check(c.World.OwnCombat==before&&c.World.OwnCombat==null,"C6 send starts no local cast action");
            Check(c.World.Actor!.Direction==heading,"C6 send does not turn the caster");
            Check(Field<object?>(c.World,"pendingAction")==null,"C6 send leaves no pending prediction");
            var a=c.World.Actor!;var expected=ActionCadence.Capture(a.Gfx,a.Weapon,18,a.Speed,a.Brave,MagicSpells.Get(4)!.ReuseMs,100);
            Check(Field<long>(c,"nextMagicAt")==now+expected.CastWaitMs,"cooldown uses CastWaitMs");
            Check(expected.CastWaitMs==1320&&expected.WaitMs==1420,"fixture: floor 1320 without local +100 margin");
            now+=1319;Check(c.MagicBusy,"still cooling down 1 ms before end");
            now+=1;Check(!c.MagicBusy,"cooldown ends at list.spr/server total");
            Feed(c,SpellReply(18));
            Check(c.World.OwnCombat is {Action:18,Predicted:false}&&c.World.OwnCombat.Timing?.Action==18,"server S30 starts the cast action");
        }
        // Authoritative slow after the cast re-extends with the slow cast reuse (not slow WaitMs).
        using(var c=Seed()){
            LearnBolt(c);long now=80_000;Set(c,"ActionClock",(Func<long>)(()=>now));
            Sender(c,(bytes,valid,admit)=>{if(!valid())return Task.FromResult(false);admit();return Task.FromResult(true);});
            Check(await c.CastSelectedMagic(),"cast before slow");
            SlowSpeed(c,2);
            var t=Field<ActionCadence>(c,"lastMagicTiming");
            Check(t.CastSpeedWaitMs(2)==2640&&Field<long>(c,"nextMagicAt")==80_000+2640,"slow re-extends cast reuse from list.spr x2 / server floor");
        }
        // Melee keeps its own prediction path (out of M7-Q scope).
        using(var c=Seed()){
            Sender(c,(bytes,valid,admit)=>{if(!valid())return Task.FromResult(false);admit();return Task.FromResult(true);});
            Check(await c.Attack()&&c.World.OwnCombat is {Action:1,Predicted:true},"melee prediction unchanged");
        }
    }
}
