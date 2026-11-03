using System.Buffers.Binary;
namespace P06;
public static class RangedRenderingTests {
    public static void Run(string output){
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var checks=new List<string>();void Check(bool b,string n){if(!b)throw new Exception(n);checks.Add(n);}
        var raws=Json.Read(Path.Combine(root,"fixtures/combat-initialization.json")).GetProperty("rows").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();
        string name=LoginConnection.CapturedCharacterName(raws);uint old=BinaryPrimitives.ReadUInt32LittleEndian(raws.First(p=>p[0]==8).AsSpan(1));var s=new WorldSession();
        foreach(var p in raws){if(p[0] is not (8 or 87))continue;int off=p[0]==8?1:5;if(BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(off))!=old)continue;var b=(byte[])p.Clone();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(off),1900210001);s.Receive(b,name);}
        var visual=File.ReadLines(Path.Combine(root,"reference/equipment/oracle.tsv")).Single(l=>l.StartsWith("visual\t20\t"));s.Receive(Convert.FromHexString(visual.Split('\t')[2]),name);
        var a=new Assets(Path.Combine(root,"assets"));var own=new World(a);own.Teleport(s.Actor!.X,s.Actor.Y);using var r=new Renderer(a);var catalog=new ObjectAssets(a.Root);
        var shot=Convert.FromHexString(File.ReadLines(Path.Combine(root,"reference/ranged/oracle.tsv")).First().Split('\t')[2]);
        // Synthetic overlap scheduling only; original serializer bytes, deliberately same arrival time.
        s.Receive(shot,name);s.Receive(shot,name);var p1=s.Projectiles[0];long release=s.Projectiles.Max(p=>p.At)+RangedRules.ReleaseMs;
        _=r.ComposeObjects(own,s,catalog,out _,out _,p1.At+RangedRules.ReleaseMs-1);Check(r.ProjectileVisuals.Length==0,"no projectile before original attack release marker");
        var patch=r.ComposeObjects(own,s,catalog,out _,out _,release);Check(r.ProjectileVisuals.Length==2,"multiple admitted reports retained; later packet cannot replace older flight");
        Check(patch.Commands.Any(d=>d.Layer=="projectile")&&patch.Commands.Where(d=>d.Layer=="projectile").All(d=>d.ObjectId==0),"projectiles share original depth compositor, cannot become target entities");
        Check(r.ProjectileVisuals.All(v=>v.Step==1),"first 16.16 step is immediate at release");
        _=r.ComposeObjects(own,s,catalog,out _,out _,release+3000);Check(r.ProjectileVisuals.Length==0,"arrival has no looping projectile ghost");
        var bow=catalog.Get(0,20)!;Check(RangedRules.ReleaseMs==791&&bow.Duration("attack")==1249&&CombatRules.Duration(20)==1200,"independent original presentation/release/server cadence");
        foreach(int d in Enumerable.Range(0,8)){var seq=bow.Sequences[("attack",d)];double ms=0;foreach(var step in seq){if(step.Milliseconds>0)Check(bow.At("attack",d,ms).Key==step.Key,"original integer tick boundary "+d+"/"+ms);ms+=step.Milliseconds??0;}Check(ms==1249,"all eight original attack durations");}
        s.Reset();_=r.ComposeObjects(own,s,catalog,out _,out _,release);Check(r.ProjectileVisuals.Length==0,"new world generation clears pending and released projectiles");
        Json.Save(output,new{status="pass",kind="synthetic scheduling and original asset timing/compositor; not live screen proof",checks});
    }
}
