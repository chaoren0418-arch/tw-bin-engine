using System.Buffers.Binary;
namespace P06;
public static class MagicRenderingTests {
 public static void Run(string output){
  string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var checks=new List<string>();void Check(bool b,string n){if(!b)throw new Exception(n);checks.Add(n);}
  var raws=Json.Read(Path.Combine(root,"fixtures/combat-initialization.json")).GetProperty("rows").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();
  string name=LoginConnection.CapturedCharacterName(raws);uint old=BinaryPrimitives.ReadUInt32LittleEndian(raws.First(p=>p[0]==8).AsSpan(1));var s=new WorldSession();
  foreach(var p in raws){if(p[0] is not (8 or 87))continue;int off=p[0]==8?1:5;if(BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(off))!=old)continue;var b=(byte[])p.Clone();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(off),1900220001);s.Receive(b,name);}
  var assets=new Assets(Path.Combine(root,"assets"));var own=new World(assets);own.Teleport(s.Actor!.X,s.Actor.Y);using var r=new Renderer(assets);var catalog=new ObjectAssets(assets.Root);
  var vectors=File.ReadLines(Path.Combine(root,"reference/magic/oracle.tsv")).Select(x=>x.Split('\t')).ToArray();byte[] heal=Convert.FromHexString(vectors.First(v=>v[0]=="heal")[2]);byte[] bolt=Convert.FromHexString(vectors.First(v=>v[0]=="bolt"&&v[1]=="7,1900229999")[2]);
  s.Receive(heal,name);var he=s.MagicEffects.Last();_=r.ComposeObjects(own,s,catalog,out _,out _,he.At);Check(r.MagicVisuals.Single().Frame=="0:0","native pure-effect immediate index increment starts physical heal frame0");
  var healAsset=new ObjectAppearance(Path.Combine(assets.Root,"objects/magic-11722.json"));Check(healAsset.Duration("heal")==2625,"original heal total ticks");
  Check(MagicMotion.PureFrame(healAsset,"heal",2500).Key=="0:20","native terminal index0 metadata is physical frame20");
  foreach(var q in new[]{(219,"impact"),(11722,"heal")}){var a=new ObjectAppearance(Path.Combine(assets.Root,$"objects/magic-{q.Item1}.json"));var seq=a.Sequences[(q.Item2,0)];long at=0;foreach(int i in Enumerable.Range(1,seq.Length-1).Append(0)){Check(MagicMotion.PureFrame(a,q.Item2,at).Key==seq[i].Key,"original pure-effect boundary "+q.Item1+"/"+at);at+=seq[i].Milliseconds!.Value;}Check(at==a.Duration(q.Item2),"pure-effect one finite cycle "+q.Item1);}
  _=r.ComposeObjects(own,s,catalog,out _,out _,he.At+2625);Check(r.MagicVisuals.Length==0,"heal expires without ghost");
  s.Receive(bolt,name);s.Receive(bolt,name);var e=s.MagicEffects.Last();long release=e.At+MagicRules.ReleaseMs;
  _=r.ComposeObjects(own,s,catalog,out _,out _,s.MagicEffects.Where(v=>v.Gfx==167).Min(v=>v.At)+MagicRules.ReleaseMs-1);Check(!r.MagicVisuals.Any(v=>v.Gfx==167),"no light bolt before original release marker");
  var patch=r.ComposeObjects(own,s,catalog,out _,out _,release);Check(r.MagicVisuals.Count(v=>v.Gfx==167)==2,"overlapping admitted magic reports remain independent");Check(r.MagicVisuals.Where(v=>v.Gfx==167).All(v=>v.Frame.EndsWith(":0")),"native immediate flight step and frame0");
  Check(patch.Commands.Where(v=>v.Layer=="magic").All(v=>v.ObjectId==0),"magic uses depth compositor without selectable entity pixels");
  _=r.ComposeObjects(own,s,catalog,out _,out _,release+3000);Check(r.MagicVisuals.Length==0,"flight and impact expire");
  for(int d=0;d<8;d++){var m=MagicMotion.At(e,e.SourceX+MovementRules.Dx[d]*6,e.SourceY+MovementRules.Dy[d]*6,release,release);Check(m.Direction==d%4&&m.Step==1&&!m.Arrived,"four original directional sprites, eight flight directions "+d);}
  s.Receive(bolt,name);s.Reset();_=r.ComposeObjects(own,s,catalog,out _,out _,release);Check(r.MagicVisuals.Length==0,"generation reset clears pending and released magic");
  Check(MagicRules.ReleaseMs==916&&MagicRules.CastMs(4)==1320&&catalog.Get(0)!.Duration("spellDir")==1374,"separate original release, server cadence and presentation duration");
  Json.Save(output,new{status="pass",kind="synthetic lifecycle and original resource boundaries; not live screen evidence",checks});
 }
}
