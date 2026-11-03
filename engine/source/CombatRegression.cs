using System.Buffers.Binary;
namespace P06;
public sealed partial class LoginConnection {
    internal static async Task CombatRegression(string capture,string output){
        var packets=Json.Read(capture).GetProperty("packets").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();
        string selected=CapturedCharacterName(packets);using var c=new LoginConnection();var checks=new List<string>();
        var npc=packets.First(p=>p[0]==87&&BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(9))==1110);uint id=BinaryPrimitives.ReadUInt32LittleEndian(npc.AsSpan(5));
        void Seed(){c.Cancel();bool entered=false;foreach(var p in packets){if(p[0]==223)entered=true;if(entered){c.World.ProbeSent=true;c.World.Receive(p,selected);if(c.World.Ready)break;}}c.State=LoginState.InWorld;c.World.Receive(npc,selected);c.World.SelectTarget(id);}
        byte[] Action(uint target,int action)=>LoginPackets.Pad([158,..BitConverter.GetBytes(target),(byte)action]);
        byte[] Hit(uint attacker,uint target,int word=3)=>LoginPackets.Pad([30,1,..BitConverter.GetBytes(attacker),..BitConverter.GetBytes(target),(byte)word,0,3,0,0,0,0,0]);
        Seed();uint self=c.World.Actor!.ObjectId;
        var corpse=(byte[])npc.Clone();corpse[11]=8;c.World.Receive(corpse,selected);c.World.SelectTarget(id);
        if(!c.World.Objects.Single(x=>x.ObjectId==id).Dead||c.World.TargetId!=null)throw new Exception("initial corpse selected");
        c.World.Receive(npc,selected);c.World.Receive(Action(id,2),selected);c.World.Receive(Hit(self,id),selected);
        var dead=c.World.Objects.Single(x=>x.ObjectId==id);if(!dead.Dead||dead.Combat?.Action!=8)throw new Exception("late full/action/hit resurrected corpse");
        var at=dead.Combat.At;c.World.Receive(Action(id,8),selected);if(c.World.Objects.Single(x=>x.ObjectId==id).Combat!.At!=at)throw new Exception("duplicate death restarted animation");checks.Add("full-corpse, late-full, late-hit, duplicate-death remain dead and unselected");
        Seed();c.World.Receive(Action(self,8),selected);if(c.World.Status!.Hp==0||c.MovementAvailable||c.CombatAvailable)throw new Exception("death-before-HP still controllable");c.World.Receive(Hit(id,self),selected);if(c.World.OwnCombat?.Action!=8)throw new Exception("late self-hit replaced death");checks.Add("self death before HP stops movement/attack; late hit preserves death");
        foreach(int size in new[]{1,8,16}){Seed();try{c.World.Receive(WithOpcode(new byte[size],30),selected);throw new Exception("truncated attack accepted");}catch(ProtocolException){}}
        checks.Add("truncated attack envelopes rejected before mutation");
        foreach(byte flight in new byte[]{0,127}){Seed();byte[] skill=new byte[40];Hit(self,id).CopyTo(skill,0);BinaryPrimitives.WriteUInt32LittleEndian(skill.AsSpan(13),flight==0?4500001u:338u);BinaryPrimitives.WriteUInt16LittleEndian(skill.AsSpan(17),66);skill[19]=flight;BinaryPrimitives.WriteUInt16LittleEndian(skill.AsSpan(20),32598);BinaryPrimitives.WriteUInt16LittleEndian(skill.AsSpan(22),32948);BinaryPrimitives.WriteUInt16LittleEndian(skill.AsSpan(24),32599);BinaryPrimitives.WriteUInt16LittleEndian(skill.AsSpan(26),32948);c.World.Receive(skill,selected);if(flight==0?(c.World.AttackReports!=0||c.World.OwnCombat!=null):(c.World.AttackReports!=1||c.World.LastProjectile?.Gfx!=66))throw new Exception("long skill/arrow classification");}
        checks.Add("original long magic skill retained unsupported; S30 D338 C127 gfx66 decoded as ranged, not melee");
        Seed();c.World.Receive(LoginPackets.Pad([237,..BitConverter.GetBytes(id),255]),selected);if(c.World.Target?.HpPercent!=null||c.World.Target?.HpByte!=255)throw new Exception("original HP meter hide sentinel misread");checks.Add("original hideTrackedHpBars byte255 preserved as unknown-hidden, not death or percentage");
        Seed();long reportsBefore=c.World.AttackReports;c.World.Receive(Hit(self,id),selected);c.ApplyCombatAcknowledgement(reportsBefore);long deadline=c.nextCombatAt;await Task.Delay(25);var unknownShape=Hit(self,id);unknownShape[13]=1;reportsBefore=c.World.AttackReports;c.World.Receive(unknownShape,selected);c.ApplyCombatAcknowledgement(reportsBefore);if(c.nextCombatAt!=deadline||c.World.AttackReports!=reportsBefore)throw new Exception("unsupported 24-byte variant reused old self confirmation");checks.Add("unsupported 24-byte variant after self attack cannot extend cooldown from stale LastAttack");
        Seed();int count=c.World.Objects.Length;c.World.Receive(Hit(uint.MaxValue,uint.MaxValue-1),selected);if(c.World.Objects.Length!=count||c.World.OwnCombat!=null)throw new Exception("unknown combat IDs created entity");checks.Add("unknown IDs do not create entities or animate self");
        foreach(string change in new[]{"cancel-input","selection-roundtrip","target-move","remove","map","own-death","disconnect-replace"}){
            Seed();int admitted=0;var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            c.sendMovementGate=async(bytes,valid,admit)=>{await gate.Task;if(!valid())return false;admitted++;admit();return true;};var task=c.Attack();
            if(c.World.OwnCombat!=null||c.nextCombatAt!=0)throw new Exception("waiting attack predicted or consumed cadence");
            if(change=="cancel-input")c.CancelMovementInput();
            if(change=="selection-roundtrip"){c.World.SelectTarget(null);c.World.SelectTarget(id);}
            if(change=="target-move"){var moved=(byte[])npc.Clone();BinaryPrimitives.WriteUInt16LittleEndian(moved.AsSpan(1),(ushort)(c.World.Target!.X+1));c.World.Receive(moved,selected);}
            if(change=="remove")c.World.Receive(LoginPackets.Pad([120,..BitConverter.GetBytes(id)]),selected);
            if(change=="map")c.World.Receive(Convert.FromHexString("CE00000000000000"),selected);
            if(change=="own-death")c.World.Receive(Action(self,8),selected);
            if(change=="disconnect-replace")Seed();
            gate.SetResult();if(await task||admitted!=0)throw new Exception("stale queued attack admitted "+change);checks.Add("delayed-gate drops "+change);
        }
        Seed();var delay=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);c.sendMovementGate=async(b,v,a)=>{await delay.Task;if(!v())return false;a();return true;};var queued=c.Attack();await Task.Delay(CombatRules.AttackMs+20);if(c.World.OwnCombat!=null||c.nextCombatAt!=0)throw new Exception("queued attack animation");delay.SetResult();if(!await queued||c.nextCombatAt-Environment.TickCount64<CombatRules.AttackMs-100||await c.Attack())throw new Exception("cadence compressed or repeat accepted");checks.Add("delayed admission starts full original sprite cadence, busy repeat discarded");
        var catalog=new ObjectAssets(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets")));foreach(int gfx in new[]{0,1110})foreach(string action in new[]{"attack","damage","die"})for(int d=0;d<8;d++){var a=catalog.Get(gfx)!;_=a.At(action,d,0);_=a.At(action,d,a.Duration(action)/2);_=a.At(action,d,a.Duration(action)*3);}
        if(catalog.Get(65535)!=null)throw new Exception("missing appearance substituted");checks.Add("original attack/damage/death 8 directions, zero-tick markers, terminal frames, missing gfx");
        Json.Save(output,new{status="pass",kind="explicit synthetic negative packets and delayed gate using original captured initialization; not dynamic original-server proof",checks,attackMs=CombatRules.AttackMs});
    }
    internal static string CapturedCharacterName(byte[][] packets){uint id=BinaryPrimitives.ReadUInt32LittleEndian(packets.First(p=>p[0]==8).AsSpan(1));byte[] own=packets.First(p=>p[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(5))==id);int end=Array.IndexOf(own,(byte)0,21);return LoginPackets.TextEncoding.GetString(own,21,end-21);}
    static byte[] WithOpcode(byte[] b,byte opcode){b[0]=opcode;return b;}
}
