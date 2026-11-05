namespace P06;
public sealed class GroundAppearance {
    public Frame Frame {get;}
    public GroundAppearance(string root,int gfx=36){Frame=GroundSpriteReader.Read(GroundCatalog.Find(gfx)??throw new InvalidDataException("地面物品未核定"));}
}
public sealed partial class Renderer {
    readonly Dictionary<int,GroundAppearance> groundAppearances=[];
    readonly HashSet<int> missingGroundAppearances=[];
    void ComposeGround(WorldSession session,ObjectAssets catalog,List<Draw> draws,List<ObjectVisual> visuals){
        foreach(var item in session.Ground){
            if(GroundCatalog.Find(item.Gfx)==null||!GroundRules.IsGround(item.Envelope)||!session.SceneContains(item.X,item.Y))continue;
            if(missingGroundAppearances.Contains(item.Gfx))continue;
            if(!groundAppearances.TryGetValue(item.Gfx,out var app)){
                try{app=new(assets.Root,item.Gfx);groundAppearances[item.Gfx]=app;}
                catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException){missingGroundAppearances.Add(item.Gfx);session.GroundNotice=$"原圖缺件或 pin 不符（gfx {item.Gfx}）；未繪製，請核對客戶端資源";continue;}
            }var f=app.Frame;
            var blocks=f.Blocks.Select(b=>new Draw(2*(item.X-Coordinates.MinX)+b.A,item.Y-Coordinates.MinY+b.B,b.Z,900000+b.Order,"actor",b.Order,-1,-1,b.Tile,item.ObjectId)).ToList();draws.AddRange(blocks);
            var bounds=Rectangle.FromLTRB(blocks.Min(b=>b.Px),blocks.Min(b=>b.Py),blocks.Max(b=>b.Px+24),blocks.Max(b=>b.Py+24));
            visuals.Add(new(item.ObjectId,item.Gfx,item.Gfx,catalog.Name(item.Name),item.X,item.Y,"ground","0:0",Coordinates.Screen(item.X,item.Y),bounds,false,0));
        }
    }
}
