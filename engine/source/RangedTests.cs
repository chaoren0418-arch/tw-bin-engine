using System.Buffers.Binary;
using System.Text.Json;
namespace P06;
public static class RangedTests {
    public static void Run(string folder,string output){
        var checks=new List<string>();void Check(bool b,string n){if(!b)throw new Exception(n);checks.Add(n);}
        var w=new WorldSession();int vectors=0,truncated=0;
        foreach(var line in File.ReadAllLines(Path.Combine(folder,"oracle.tsv"))){
            var cols=line.Split('\t');byte[] b=Convert.FromHexString(cols[2]);w.Reset();w.Receive(b,"");var shot=w.LastProjectile!;
            Check(w.AttackReports==1&&shot.Gfx==66&&shot.SourceX==32592&&shot.SourceY==32944&&shot.TargetX==32598&&shot.TargetY==32948,"original projectile envelope "+vectors);
            if(cols[0]=="shot"){var v=cols[1].Split(',').Select(int.Parse).ToArray();Check(shot.Heading==v[0]&&w.LastAttack!.DamageWord==unchecked((ushort)v[1])&&shot.Target==v[2],"original unsigned damage/target/heading "+vectors);}
            Check(w.Objects.Length==0&&w.OwnCombat==null&&w.Inventory.Length==0,"unknown objects do not create actor or debit inventory "+vectors);
            for(int n=1;n<b.Length;n++){w.Reset();bool threw=false;try{w.Receive(b[..n],"");}catch(ProtocolException){threw=true;}Check(threw&&w.AttackReports==0&&w.LastProjectile==null,"atomic truncated ranged "+vectors+"/"+n);truncated++;}
            vectors++;
        }
        int ranges=0;foreach(var l in File.ReadAllLines(Path.Combine(folder,"range.tsv"))){var c=l.Split('\t');int x=int.Parse(c[0]),y=int.Parse(c[1]);Check(RangedRules.InScreen(32592,32944,32592+x,32944+y)==bool.Parse(c[2]),"original screen boundary "+x+","+y);ranges++;}
        var assets=new ObjectAssets(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets")));
        w.Reset();uint[] expected=[80,79,79,1,0,0,4];int ammoIndex=0;
        foreach(var line in File.ReadAllLines(Path.Combine(folder,"ammo.tsv"))){var b=Convert.FromHexString(line.Split('\t')[1]);w.Receive(b,"");uint count=w.Inventory.Where(i=>i.SupportedArrow).Aggregate(0u,(n,i)=>n+i.Count);Check(count==expected[ammoIndex],"original absolute arrow update/delete/late-unknown/add "+ammoIndex);if(count>0)Check(w.Inventory.Single().UseType==254&&w.Inventory.Single().IconId==7,"original arrow fingerprint");ammoIndex++;}
        Check(CombatRules.Duration(20)==1200,"original bow cadence1200");
        foreach(int d in Enumerable.Range(0,8))foreach(string action in new[]{"walk","breath","attack","damage","die"})Check(assets.Get(0,20)!.Supports(action,d),"original bow resources "+action+"/"+d);
        var npc=new WorldObject(32598,32948,1900249999,1110,0,0,0,0,0,0,"$907","",0,0,"","",0,255,0,0,0,255,255);
        Check(CombatRules.Attack(npc,20)[0]==123&&CombatRules.Attack(npc,4)[0]==229,"weapon selects original ranged/melee opcode");
        Json.Save(output,new{status="pass",kind="original JAR serializer and Point vectors; synthetic inputs, no live gameplay claim",vectors,truncated,ranges,checks});
    }
}
