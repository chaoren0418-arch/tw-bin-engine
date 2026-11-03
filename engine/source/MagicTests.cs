using System.Buffers.Binary;
namespace P06;
public sealed partial class LoginConnection {
    internal static async Task MagicTests(string output){
        var checks=new List<string>();void Check(bool b,string n){if(!b)throw new Exception(n);checks.Add(n);}
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));
        var rows=Json.Read(Path.Combine(root,"fixtures/p17-inventory-generation1.json")).GetProperty("packets").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();string selected=CapturedCharacterName(rows);
        byte[] npc=rows.First(p=>p[0]==87&&BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(9))==1110);uint id=BinaryPrimitives.ReadUInt32LittleEndian(npc.AsSpan(5));
        var vectors=File.ReadAllLines(Path.Combine(root,"reference/magic/oracle.tsv")).Select(l=>l.Split('\t')).ToArray();byte[] learned=Convert.FromHexString(vectors.First(v=>v[0]=="add"&&v[1]=="9")[2]);
        using var c=new LoginConnection();
        void Seed(){c.Cancel();bool entered=false;foreach(var b in rows){if(b[0]==223)entered=true;if(entered){c.World.ProbeSent=true;c.World.Receive(b,selected);if(c.World.Ready)break;}}c.State=LoginState.InWorld;c.World.Receive(npc,selected);c.World.Receive(learned,selected);c.World.SelectMagic(4);c.World.SelectTarget(id);}
        int truncated=0;
        foreach(var v in vectors){Seed();var w=c.World;byte[] b=Convert.FromHexString(v[2]);w.Receive(b,selected);switch(v[0]){
            case "single-add":Check(w.LearnedMagic.Any(x=>x.Id==int.Parse(v[1])),"original single add40");break;
            case "single-del":Check(!w.LearnedMagic.Any(x=>x.Id==int.Parse(v[1])),"original single delete40");break;
            case "add":int mask=int.Parse(v[1]);Check(w.LearnedMagic.Select(x=>x.Id).Order().SequenceEqual(Enumerable.Range(1,8).Where(i=>((mask|9)&(1<<(i-1)))!=0)),"add masks union "+mask);w.Receive(b,selected);Check(w.LearnedMagic.Select(x=>x.Id).Distinct().Count()==w.LearnedMagic.Length,"idempotent mask "+mask);break;
            case "tier":Check(w.LearnedMagic.Any(x=>x.Id==int.Parse(v[1])*8+8),"tier is not length "+v[1]);break;
            case "del":Check(!w.LearnedMagic.Any(x=>x.Id==1)&&w.LearnedMagic.Any(x=>x.Id==4),"original deletion mask");break;
            case "mp":case "hp":var values=v[1].Split(',').Select(uint.Parse).ToArray();Check(v[0]=="mp"?w.Status!.Mp==values[0]&&w.Status.MaxMp==values[1]:w.Status!.Hp==values[0]&&w.Status.MaxHp==values[1],"absolute32 vitals "+v[0]+v[1]);w.Receive(b,selected);Check(v[0]=="mp"?w.Status!.Mp==values[0]:w.Status!.Hp==values[0],"duplicate absolute vitals");break;
            case "bolt":Check(w.MagicEffects.Last() is {Gfx:167,Action:18,SourceX:32592,SourceY:32944,TargetX:32598,TargetY:32948},"original attack serializer "+v[1]);Check(w.MagicEffects.Last().DamageWord==unchecked((ushort)int.Parse(v[1].Split(',')[0])),"unsigned magic damage word");break;
            case "heal":Check(w.MagicEffects.Last().Gfx==11722,"original heal serializer");break;
        }
        if(v[0] is "add" or "del" or "tier" or "bolt" or "single-add" or "single-del")for(int n=1;n<b.Length;n++){Seed();bool threw=false;try{c.World.Receive(b[..n],selected);}catch(ProtocolException){threw=true;}if(b.Length==40&&n==32&&v[0].StartsWith("single-")){Check(!threw,"32byte prefix is valid28-mask packet, not a malformed whole frame");continue;}Check(threw&&c.World.MagicEffectCount==0&&c.World.LearnedMagic.Length==2,"atomic truncated "+v[0]+"/"+n);truncated++;}}
        foreach(var v in vectors.Where(v=>v[0] is "single-add" or "single-del" or "add" or "bolt")){Seed();var bad=Convert.FromHexString(v[2]);bad[^1]=1;bool threw=false;try{c.World.Receive(bad,selected);}catch(ProtocolException){threw=true;}Check(threw&&c.World.LearnedMagic.Length==2&&c.World.MagicEffectCount==0,"nonzero padding rejected before state mutation "+v[0]+v[1]);}
        foreach(string change in new[]{"input","skill","target","target-move","remove","map","death","generation","weapon"}){
            Seed();var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int sent=0;uint mp=c.World.Status!.Mp,hp=c.World.Status.Hp;
            c.sendMovementGate=async(b,v,a)=>{await gate.Task;if(!v())return false;sent++;a();return true;};var pending=c.CastSelectedMagic();
            Check(c.World.Status.Mp==mp&&c.World.Status.Hp==hp&&c.World.MagicEffectCount==0,"no prediction before gate "+change);
            switch(change){case "input":c.CancelMovementInput();break;case "skill":c.World.SelectMagic(1);break;case "target":c.World.SelectTarget(null);break;case "target-move":var moved=(byte[])npc.Clone();BinaryPrimitives.WriteUInt16LittleEndian(moved.AsSpan(1),(ushort)(c.World.Target!.X+1));c.World.Receive(moved,selected);break;case "remove":c.World.Receive(LoginPackets.Pad([120,..BitConverter.GetBytes(id)]),selected);break;case "map":c.World.Receive(Convert.FromHexString("CE00000000000000"),selected);break;case "death":c.World.Receive(LoginPackets.Pad([158,..BitConverter.GetBytes(c.World.Actor!.ObjectId),8]),selected);break;case "generation":Seed();break;case "weapon":c.World.Receive(LoginPackets.Pad([119,..BitConverter.GetBytes(c.World.Actor!.ObjectId),4]),selected);break;}
            gate.SetResult();Check(!await pending&&sent==0,"stale cast rejected "+change);
        }
        Seed();int sends=0;c.sendMovementGate=(b,v,a)=>{if(!v())return Task.FromResult(false);Check(b[0]==6&&b[1]==0&&b[2]==3&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(3))==id,"original C6 fields");sends++;a();return Task.FromResult(true);};
        var status=c.World.Status;Check(await c.CastSelectedMagic()&&!await c.CastSelectedMagic()&&sends==1&&c.World.Status==status,"one explicit cast, no predicted MP/HP");Check(!await c.Attack()&&!await c.Move(2)&&!await c.UseSelectedItem(),"cast busy gates combat/movement/item");
        Seed();c.World.SelectMagic(1);Check(MagicRules.Request(1,c.World.Actor!.ObjectId,1,2).AsSpan(7).ToArray().All(x=>x==0),"heal self no invented coordinates");
        Check(!c.World.SelectMagic(219),"unlearned selection rejected");c.World.SelectMagic(4);c.World.SelectTarget(null);Check(!await c.CastSelectedMagic(),"attack requires target");
        Seed();var mp0=Convert.FromHexString(vectors.First(v=>v[0]=="mp"&&v[1]=="0,120")[2]);var mp1=(byte[])mp0.Clone();BinaryPrimitives.WriteUInt32LittleEndian(mp1.AsSpan(1),1);c.World.Receive(mp0,selected);c.World.Receive(mp1,selected);c.World.Receive(mp1,selected);Check(c.World.Status!.Mp==1,"original absolute MP recovery and duplicate");
        var assets=new ObjectAssets(Path.Combine(root,"assets"));foreach(int weapon in new[]{0,4,20,50})foreach(int d in Enumerable.Range(0,8))foreach(string action in new[]{"spellDir","spellNoDir"})Check(assets.Get(0,weapon)!.Supports(action,d),"original cast pose "+weapon+"/"+d+"/"+action);
        Json.Save(output,new{status="pass",kind="synthetic gates and original JAR serializer vectors; no live branch claims",vectors=vectors.Length,truncated,checks});
    }
}
