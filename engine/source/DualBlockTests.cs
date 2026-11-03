using System.Buffers.Binary;
namespace P06;
public static class DualBlockTests {
 public static void Run(string output){
  string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));Directory.CreateDirectory(output);
  var checks=new List<object>();void Check(bool pass,string name,object? detail=null){if(!pass)throw new Exception(name);checks.Add(new{name,detail});}
  var a=new Assets(Path.Combine(root,"assets"));using var r=new Renderer(a);var catalog=new ObjectAssets(a.Root);
  byte[] expected=File.ReadAllBytes(Path.Combine(root,"reference/dual-block/scene.rgb555")),mask=File.ReadAllBytes(Path.Combine(root,"reference/dual-block/scene.mask"));
  int diffs=0;Check(expected.Length==r.BaseColors.Length*2&&mask.Length==r.BaseColors.Length,"full original raster dimensions");
  for(int i=0;i<r.BaseColors.Length;i++)if(r.BaseColors[i]!=BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(i*2))||r.BaseMask[i]!=(mask[i]!=0))diffs++;
  Check(diffs==0,"all original dual-block RGB555 pixels and masks",new{pixels=r.BaseColors.Length,diffs,commands=a.Scene.Count,r.Width,r.Height});
  int vectors=0,seam=0;
  foreach(var line in File.ReadLines(Path.Combine(root,"reference/dual-block/movement.tsv"))){var p=line.Split('\t');int x=int.Parse(p[0]),y=int.Parse(p[1]),h=int.Parse(p[2]);bool inside=bool.Parse(p[3]),original=bool.Parse(p[4]);if(MovementRules.Passable(x,y,h)!=(inside&&original))throw new Exception("original terrain disagreement "+line);vectors++;if(y==32959&&MovementRules.Dy[h]>0||y==32960&&MovementRules.Dy[h]<0)seam++;}
  Check(vectors==65536&&seam==384,"all original full-map directional results including both seam directions",new{vectors,seam});
  for(int y=Coordinates.MinY;y<=Coordinates.MaxY;y++)for(int x=Coordinates.MinX;x<=Coordinates.MaxX;x++){var p=Coordinates.Screen(x,y);if(Coordinates.Pick(p.X,p.Y)!=new Point(x,y))throw new Exception("world inverse");}
  Check(!Coordinates.Inside(32580,33024)&&Coordinates.Inside(32580,32974),"town supported; unloaded outer boundary rejected");
  var camera=new Camera();camera.SetBounds(r.Width,r.Height);var town=Coordinates.Screen(32580,32974);camera.SetCenter(town);Check(camera.Center==town,"camera follows saved town without old canvas clamp");
  var raws=Json.Read(Path.Combine(root,"fixtures/combat-initialization.json")).GetProperty("rows").EnumerateArray().Select(v=>v.GetProperty("packet")).Where(v=>v.TryGetProperty("plainHex",out _)).Select(v=>Convert.FromHexString(v.S("plainHex"))).ToArray();
  checks.Add(LoginConnection.DualMovementGate(raws).GetAwaiter().GetResult());
  string name=LoginConnection.CapturedCharacterName(raws);uint old=BinaryPrimitives.ReadUInt32LittleEndian(raws.First(p=>p[0]==8).AsSpan(1));uint self=1900220001,target=1900229999;
  byte[] ownSeed=raws.First(p=>p[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(5))==old);
  var magic=File.ReadLines(Path.Combine(root,"reference/magic/oracle.tsv")).Select(l=>l.Split('\t')).ToArray();
  byte[] heal=Convert.FromHexString(magic.First(v=>v[0]=="heal")[2]),bolt=Convert.FromHexString(magic.First(v=>v[0]=="bolt"&&v[1]=="7,1900229999")[2]);
  byte[] arrow=Convert.FromHexString(File.ReadLines(Path.Combine(root,"reference/ranged/oracle.tsv")).First().Split('\t')[2]);
  foreach(var point in new[]{(32588,32930,"north"),(32588,32959,"seam-south"),(32588,32960,"seam-north"),(32580,32974,"town"),(32605,33006,"south")}){
   var s=new WorldSession();foreach(var p in raws.Where(p=>p[0]==8)){var b=(byte[])p.Clone();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(1),self);s.Receive(b,name);break;}
   s.Receive(Convert.FromHexString("CE00000000000000"),name);
   byte[] Actor(uint id,int x,int y){var b=(byte[])ownSeed.Clone();BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(1),(ushort)x);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(3),(ushort)y);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5),id);return b;}
   int x=point.Item1,y=point.Item2,ty=y+(point.Item3=="seam-north"?-6:6);s.Receive(Actor(self,x,y),name);s.Receive(Actor(target,x+3,ty),name);
   var w=new World(a);w.Teleport(x,y);Check(s.SupportedMap,"supported authoritative map "+point.Item3);
   var patch=r.ComposeObjects(w,s,catalog,out var visuals,out var missing);Check(visuals.Any(v=>v.ObjectId==target)&&missing.Length==0,"original other actor composed "+point.Item3);
   int visible=patch.EntityOwners!.Count(v=>v==target);int originalPixels=patch.Commands.Where(c=>c.ObjectId==target).Sum(c=>c.Tile.Pixels.Length);
   // Visibility may legitimately be hidden by original roofs. Selection must use actual top pixels only.
   if(visible>0){s.SelectTarget(target);Check(s.TargetId==target,"visible target accepted "+point.Item3);}
   checks.Add(new{name="original occlusion/pick mask "+point.Item3,detail=(object)new{visible,originalPixels,hidden=originalPixels-visible}});
   byte[] Spell(byte[] input){var b=(byte[])input.Clone();BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(2),self);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(6),target);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(20),(ushort)x);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(22),(ushort)y);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(24),(ushort)(x+3));BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(26),(ushort)ty);return b;}
   s.Receive(heal,name);long htime=s.MagicEffects.Last().At;_=r.ComposeObjects(w,s,catalog,out _,out _,htime);Check(r.MagicVisuals.Any(v=>v.Gfx==11722&&v.X==x&&v.Y==y),"original self-heal at actual tile "+point.Item3);
   s.Receive(Spell(bolt),name);long release=s.MagicEffects.Last().At+MagicRules.ReleaseMs;patch=r.ComposeObjects(w,s,catalog,out _,out _,release);
   Check(r.MagicVisuals.Any(v=>v.Gfx==167)&&patch.Commands.Any(c=>c.Layer=="magic")&&patch.Commands.Where(c=>c.Layer=="magic").All(c=>c.ObjectId==0),"original magic flight uses map depth and has no target pixels "+point.Item3);
   using(var full=(Bitmap)r.Background.Clone()){using(var g=Graphics.FromImage(full))using(var part=Renderer.BitmapOf(patch.Bounds.Width,patch.Bounds.Height,patch.Colors,patch.Mask))g.DrawImageUnscaled(part,patch.Bounds.Location);var foot=Coordinates.Screen(x,y);var crop=Rectangle.Intersect(new((int)foot.X-280,(int)foot.Y-200,560,400),new(0,0,r.Width,r.Height));using var pic=full.Clone(crop,full.PixelFormat);pic.Save(Path.Combine(output,point.Item3+"-synthetic-original-assets.png"));}
   s.Receive(Spell(arrow),name);long ar=s.Projectiles.Last().At+RangedRules.ReleaseMs;patch=r.ComposeObjects(w,s,catalog,out _,out _,ar);Check(r.ProjectileVisuals.Length>0&&patch.Commands.Any(c=>c.Layer=="projectile"),"original arrow flight in loaded region "+point.Item3);
   s.Reset();_=r.ComposeObjects(w,s,catalog,out _,out _,ar+3000);Check(r.MagicVisuals.Length==0&&r.ProjectileVisuals.Length==0,"new generation clears regional effects "+point.Item3);
  }
  Json.Save(Path.Combine(output,"result.json"),new{status="pass",kind="offline original pixel/wire fixture scenarios; no live login; untriggered opacity; resident blocks with verified cold-load order",checks});
 }
}
public sealed partial class LoginConnection {
 internal static async Task<object> DualMovementGate(byte[][] packets){
  using var c=new LoginConnection();string selected=CapturedCharacterName(packets);bool started=false;
  foreach(var p in packets){if(p[0]==223)started=true;if(started){c.World.ProbeSent=true;c.World.Receive(p,selected);}}c.World.Receive(LoginPackets.Pad([123,1,0,0,0]),selected);c.State=LoginState.InWorld;
  var own=(byte[])packets.First(p=>p[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(5))==c.World.Actor!.ObjectId).Clone();
  BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(1),32576);BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(3),32959);c.World.Receive(own,selected);
  var sent=new List<string>();c.sendMovementGate=(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(Convert.ToHexString(b));admit();return Task.FromResult(true);};
  if(!c.MovementAvailable)throw new Exception("seam initialization: "+System.Text.Json.JsonSerializer.Serialize(c.Snapshot()));
  if(!await c.Move(4)||c.World.Actor!.Y!=32960)throw new Exception("normal product south seam gate");
  if(await c.Move(0))throw new Exception("seam move bypassed original pace");await Task.Delay(MovementRules.StepMs+30);
  if(!await c.Move(0)||c.World.Actor!.Y!=32959||sent.Count!=2)throw new Exception("normal product north seam gate");
  return new{name="normal product bidirectional seam movement and pace",detail=new{sent,final=c.World.Actor,syntheticTransportOnly=true,rawBypass=false}};
 }
}
