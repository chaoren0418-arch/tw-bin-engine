using System.Diagnostics;
using System.Drawing.Imaging;
namespace P06;
public static class Tests {
    static readonly List<object> checks=[];
    static void Check(string name,bool ok,object? detail=null) {checks.Add(new{name,status=ok?"pass":"fail",detail});if(!ok)throw new Exception("Check failed: "+name);}
    static void Step(World w,double ms) {while(ms>0){double n=Math.Min(100,ms);w.Advance(n);ms-=n;}}
    static void SaveCrop(Bitmap image,Rectangle r,string path) {using var c=image.Clone(r,PixelFormat.Format32bppArgb);c.Save(path,ImageFormat.Png);}
    public static void Run(Assets a,string output) {
        Directory.CreateDirectory(output);var watch=Stopwatch.StartNew();
        try {
            using var r=new Renderer(a);
            var expected=File.ReadAllBytes(Path.Combine(a.Root,"../reference/dual-block/scene.rgb555"));int diff=0;
            for(int i=0;i<r.BaseColors.Length;i++)if(r.BaseColors[i]!=(expected[2*i]|expected[2*i+1]<<8))diff++;
            Check("independent original dual-block full RGB555 reference",diff==0,new{pixels=r.BaseColors.Length,differences=diff,commands=a.Scene.Count});
            int spritePixels=0;
            // T454 (#1039): the character PNG previews are no longer part of the assets tree (asset-recipe.md 3.2). The reference is
            // now an independent placement of every block pixel (x = 24·(b + a − ⌊a/2⌋), y = 12·(b − ⌊a/2⌋), sprites.md §3.4) inside
            // the frame bounds, compared with what Renderer.Blit draws.
            foreach(var f in a.Frames.Values) {
                var bounds=f.Bounds;var rect=new Rectangle(bounds[0],bounds[1],bounds[2]-bounds[0]+1,bounds[3]-bounds[1]+1);
                var colors=new ushort[rect.Width*rect.Height];var mask=new bool[colors.Length];
                foreach(var b in f.Blocks)Renderer.Blit(new(b.A,b.B,b.Z,b.Order,"actor",0,-1,0,b.Tile),new(rect.X,rect.Y+756,rect.Width,rect.Height),colors,mask,null);
                var expectedColors=new ushort[colors.Length];var expectedMask=new bool[colors.Length];int outside=0;
                foreach(var b in f.Blocks){int half=(int)Math.Floor(b.A/2.0),ox=24*(b.B+b.A-half),oy=12*(b.B-half);foreach(var p in b.Tile.Pixels){int x=ox+p.X-rect.X,y=oy+p.Y-rect.Y;if((uint)x>=(uint)rect.Width||(uint)y>=(uint)rect.Height){outside++;continue;}expectedColors[y*rect.Width+x]=p.Color;expectedMask[y*rect.Width+x]=true;}}
                int differences=0;
                for(int i=0;i<colors.Length;i++){if(mask[i]!=expectedMask[i]||(mask[i]&&colors[i]!=expectedColors[i]))differences++;if(mask[i])spritePixels++;}
                Check("original SPR block placement vs independent placement "+f.Key,differences==0&&outside==0,new{differences,outside});
            }
            Check("all source pixels retained",spritePixels==65805,new{spritePixels});
            // Mathematical contract tests use synthetic coordinates, separate from actual map/PNG reference above.
            int picks=0;for(int x=32575;x<=32640;x++)for(int y=32895;y<=32960;y++){var s=Coordinates.Screen(x,y);CheckPoint(Coordinates.Pick(s.X,s.Y),new(x,y));picks++;}
            Check("world center inverse incl outside block",true,new{picks});
            Check("negative block floor and boundary",Coordinates.Split(-1)==(32255,63)&&Coordinates.Split(32575)==(32764,63)&&Coordinates.Split(32640)==(32766,0));
            var camera=new Camera{View=new(1180,720),Follow=false};camera.SetCenter(new(1234,789));var vp=new PointF(176,224);var before=camera.ToScene(vp);camera.Scale(1.3f,vp);var after=camera.ToScene(vp);
            Check("zoom preserves cursor scene point",Math.Abs(before.X-after.X)<.001&&Math.Abs(before.Y-after.Y)<.001);
            camera.Pan(100000,100000);Check("camera clamp",camera.Center.X==0&&camera.Center.Y==0);
            var input=new InputState();input.Down(Keys.Up);input.Down(Keys.Right);Check("simultaneous diagonal",input.Direction==2);input.Up(Keys.Right);Check("individual release",input.Direction==1);input.Down(Keys.Down);Check("opposing cancel",input.Direction==null);input.Clear();Check("focus lifecycle clear",input.Held.Count==0&&input.Direction==null);
            var trajectories=new List<object>();
            for(int d=0;d<8;d++) {
                var w=new World(a);w.SetIntent(d);int startX=w.X,startY=w.Y;var delta=a.Directions[d];var positions=new List<object>();
                for(int k=0;k<12;k++){positions.Add(new{tick=k,model=w.Snapshot(),screen=w.DisplayPosition,centroid=Centroid(r,w)});Step(w,166);}
                Check($"D{d} three complete cycles",w.X==startX+3*delta.Dx&&w.Y==startY+3*delta.Dy&&w.FrameIndex==0&&w.Steps==3);
                w.SetIntent(null);Step(w,664);Check($"D{d} release completes active step and stops",!w.Walking&&w.Steps==4);
                int stopX=w.X,stopY=w.Y;Step(w,5000);Check($"D{d} idle remains at committed coordinate",w.X==stopX&&w.Y==stopY&&!w.Walking);
                var sample=new World(a);sample.SetIntent(d);var roots=new List<PointF>();
                for(int k=0;k<=8;k++){roots.Add(Centroid(r,sample));Step(sample,166);}
                // Independent visual feature: all opaque sprite centroid follows projection. This catches duplicated world translation or cycle reset.
                double maxResidual=0;for(int k=0;k<roots.Count;k++){double ex=roots[0].X+delta.Sx*k/4.0,ey=roots[0].Y+delta.Sy*k/4.0;maxResidual=Math.Max(maxResidual,Math.Sqrt(Math.Pow(roots[k].X-ex,2)+Math.Pow(roots[k].Y-ey,2)));}
                Check($"D{d} real-pixel root displacement no doubling/reset",maxResidual<6,new{maxCentroidResidualPixels=maxResidual,roots});
                trajectories.Add(new{direction=d,positions});
            }
            Json.Save(Path.Combine(output,"trajectories.json"),trajectories);
            var visual=Path.Combine(output,"visual");Directory.CreateDirectory(visual);
            var visualRecords=new List<object>();
            foreach(var (x,y) in new[]{(32588,32928),(32590,32929),(32590,32930),(32591,32929),(32589,32929),(32589,32930),(32590,32928),(32589,32937),(32590,32937),(32591,32937),(32590,32938),(32591,32938),(32592,32938),(32591,32939),(32590,32940),(32589,32941),(32591,32941),(32592,32941)}) {
                var w=new World(a);w.Teleport(x,y);w.Face(5);var patch=r.Compose(w);var bp=new ushort[patch.Colors.Length];var bm=new bool[bp.Length];var bo=Enumerable.Repeat(-1,bp.Length).ToArray();
                foreach(var cmd in patch.Commands.Where(c=>!c.Actor))Renderer.Blit(cmd,patch.Bounds,bp,bm,bo);
                var ac=new ushort[bp.Length];var am=new bool[bp.Length];foreach(var cmd in r.Actor(w))Renderer.Blit(cmd,patch.Bounds,ac,am,null);
                int hidden=0,front=0,visible=0;for(int i=0;i<bp.Length;i++)if(am[i]){if(patch.Owners[i]==-2){visible++;if(bo[i]>=0)front++;}else if(patch.Owners[i]>=0)hidden++;}
                using var full=r.Full(w);var center=Coordinates.Screen(x,y);var rect=new Rectangle((int)center.X-170,(int)center.Y-180,340,260);SaveCrop(full,rect,Path.Combine(visual,$"point-{x}-{y}.png"));
                visualRecords.Add(new{x,y,visible,hidden,frontOverlap=front,commands=patch.Commands.Where(c=>c.Layer=="objects").Select(c=>new{c.SourceIndex,c.Group,c.Offset,c.X,c.Y,c.Z})});
            }
            for(int d=0;d<8;d++) {
                var w=new World(a);w.Face(d);var beforeWalk=Centroid(r,w);var center=Coordinates.Screen(a.Spawn[0],a.Spawn[1]);using(var idle=r.Full(w))SaveCrop(idle,new((int)center.X-180,(int)center.Y-210,360,360),Path.Combine(visual,$"walk-{d}-idle.png"));
                w.SetIntent(d);var atWalk=Centroid(r,w);Check($"D{d} breath to walk visual continuity",Distance(beforeWalk,atWalk)<6,new{beforeWalk,atWalk});
                PointF lastWalk=new();
                for(int k=0;k<17;k++) {
                    if(k==8)w.SetIntent(null);if(k==11)lastWalk=Centroid(r,w);
                    if(k==12){var stop=Centroid(r,w);double quarter=Math.Sqrt(a.Directions[d].Sx*a.Directions[d].Sx+a.Directions[d].Sy*a.Directions[d].Sy)/4;Check($"D{d} walk to breath visible final quarter step",!w.Walking&&Distance(lastWalk,stop)<quarter+6,new{lastWalk,stop,expectedQuarterStep=quarter});}
                    using var full=r.Full(w);SaveCrop(full,new((int)center.X-180,(int)center.Y-210,360,360),Path.Combine(visual,$"walk-{d}-{k:00}.png"));Step(w,166);
                }
                var resting=Centroid(r,w);for(int next=0;next<8;next++){w.Face(next);Check($"D{d} stop to face D{next} retains root",Distance(resting,Centroid(r,w))<6);}
            }
            Json.Save(Path.Combine(output,"visual-records.json"),visualRecords);
            var longFrame=new World(a);longFrame.SetIntent(3);longFrame.Advance(10000);Check("long frame capped, no teleport",longFrame.FrameIndex==1&&longFrame.Steps==0&&longFrame.DiscardedMs==9750);longFrame.SetIntent(null);Step(longFrame,1000);Check("long frame release settles",!longFrame.Walking&&longFrame.Steps==1);
            var fine=new World(a);var coarse=new World(a);fine.SetIntent(3);coarse.SetIntent(3);for(int i=0;i<2000;i++)fine.Advance(1);for(int i=0;i<20;i++)coarse.Advance(100);Check("elapsed partition invariance",fine.X==coarse.X&&fine.Y==coarse.Y&&fine.FrameIndex==coarse.FrameIndex&&fine.Elapsed==coarse.Elapsed);
            var edge=new World(a);edge.Teleport(32576,32896);edge.SetIntent(0);Step(edge,2000);Check("core boundary refuses entire step",edge.X==32576&&edge.Y==32896&&!edge.Walking);bool rejected=false;try{edge.Teleport(32640,32900);}catch(ArgumentOutOfRangeException){rejected=true;}Check("outside teleport rejects",rejected);
            var turn=new World(a);turn.SetIntent(3);Step(turn,170);turn.SetIntent(7);Check("direction change deferred during step",turn.Direction==3);Step(turn,494);Check("direction change applies at boundary",turn.Direction==7&&turn.X==a.Spawn[0]+1&&turn.Y==a.Spawn[1]+1);
            // Real occlusion scan records exact world points and source command provenance; screenshots chosen afterward by visual inspection.
            var samples=new List<object>();
            for(int x=32584;x<=32616;x++)for(int y=32916;y<=32946;y++) {
                var w=new World(a);w.Teleport(x,y);w.Face(5);var p=r.Compose(w);var actorColors=new ushort[p.Colors.Length];var actorMask=new bool[p.Colors.Length];
                foreach(var c in r.Actor(w))Renderer.Blit(c,p.Bounds,actorColors,actorMask,null);
                int visible=0,hidden=0;var blockers=new Dictionary<int,int>();
                for(int i=0;i<p.Colors.Length;i++)if(actorMask[i]){if(p.Owners[i]==-2)visible++;else if(p.Owners[i]>=0){hidden++;blockers[p.Owners[i]]=blockers.GetValueOrDefault(p.Owners[i])+1;}}
                if(hidden>=25&&visible>=30) samples.Add(new{x,y,visible,hidden,groups=blockers.Keys.Select(i=>a.Scene.Single(c=>c.Layer=="objects"&&c.Serial==i).Group).Distinct().ToArray(),blockingCommands=blockers.Select(b=>new{sceneSerial=b.Key,pixels=b.Value,offset=a.Scene.Single(c=>c.Layer=="objects"&&c.Serial==b.Key).Offset}).ToArray()});
            }
            Check("real actor/map interleaved occlusion exists",samples.Count>10,new{samples=samples.Count});Json.Save(Path.Combine(output,"occlusion-candidates.json"),samples);
            // Small inspection sheet: world points over true assets, not synthetic success imagery.
            using(var overview=new Bitmap(r.Background)) {using var g=Graphics.FromImage(overview);using var font=new Font("Consolas",14);foreach(var (x,y) in new[]{(32594,32930),(32602,32926),(32605,32915),(32598,32927),(32591,32920),(32606,32934)}){var p=Coordinates.Screen(x,y);g.DrawEllipse(Pens.Yellow,p.X-8,p.Y-8,16,16);g.DrawString($"{x},{y}",font,Brushes.Yellow,p.X+10,p.Y);}overview.Save(Path.Combine(output,"landmark-locator.png"),ImageFormat.Png);}
            var spawn=new World(a);using(var image=r.Full(spawn)){SaveCrop(image,new(850,700,900,650),Path.Combine(output,"spawn-context.png"));image.Save(Path.Combine(output,"spawn-full.png"),ImageFormat.Png);}
            Json.Save(Path.Combine(output,"result.json"),new{status="pass",checks,elapsedMs=watch.ElapsedMilliseconds});
        }catch(Exception ex){Json.Save(Path.Combine(output,"result.json"),new{status="fail",checks,error=ex.ToString(),elapsedMs=watch.ElapsedMilliseconds});throw;}
    }
    static void CheckPoint(Point a,Point b){if(a!=b)throw new Exception($"Inverse failure {a} vs {b}");}
    static double Distance(PointF a,PointF b)=>Math.Sqrt(Math.Pow(a.X-b.X,2)+Math.Pow(a.Y-b.Y,2));
    public static PointF Centroid(Renderer r,World w) {double x=0,y=0;int n=0;foreach(var c in r.Actor(w))foreach(var p in c.Tile.Pixels){x+=c.Px+p.X;y+=c.Py+p.Y;n++;}return new((float)(x/n),(float)(y/n));}
}
