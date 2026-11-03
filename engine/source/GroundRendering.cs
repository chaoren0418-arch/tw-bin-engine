namespace P06;
public sealed class GroundAppearance {
    public Frame Frame {get;}
    public GroundAppearance(string root,int gfx=36){var f=Json.Read(Path.Combine(root,$"ground/{gfx}.json"));Frame=new(f.S("key"),f.A("bounds"),f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new(2,b.GetProperty("pixels").EnumerateArray().Select(p=>{var v=p.EnumerateArray().Select(x=>x.GetInt32()).ToArray();return new Pixel(v[0],v[1],(ushort)v[2]);}).ToArray()))).ToArray());}
}
public sealed partial class Renderer {
    readonly Dictionary<int,GroundAppearance> groundAppearances=[];
    void ComposeGround(WorldSession session,ObjectAssets catalog,List<Draw> draws,List<ObjectVisual> visuals){
        foreach(var item in session.Ground){
            if(item.Gfx is not (36 or 66)||!session.SceneContains(item.X,item.Y))continue;
            if(!groundAppearances.TryGetValue(item.Gfx,out var app)){app=new(assets.Root,item.Gfx);groundAppearances[item.Gfx]=app;}var f=app.Frame;
            var blocks=f.Blocks.Select(b=>new Draw(2*(item.X-Coordinates.MinX)+b.A,item.Y-Coordinates.MinY+b.B,b.Z,900000+b.Order,"actor",b.Order,-1,-1,b.Tile,item.ObjectId)).ToList();draws.AddRange(blocks);
            var bounds=Rectangle.FromLTRB(blocks.Min(b=>b.Px),blocks.Min(b=>b.Py),blocks.Max(b=>b.Px+24),blocks.Max(b=>b.Py+24));
            visuals.Add(new(item.ObjectId,item.Gfx,item.Gfx,catalog.Name(item.Name),item.X,item.Y,"ground","0:0",Coordinates.Screen(item.X,item.Y),bounds,false,0));
        }
    }
}
