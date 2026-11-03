namespace P06;
public sealed partial class Renderer {
    internal object CombatVisibilityPlan(ObjectAssets catalog){
        var rows=new List<(int X,int Y,int Tx,int Ty,double Own,double Target)>();
        int Count(Frame f)=>f.Blocks.SelectMany(b=>{var d=new Draw(b.A,b.B,b.Z,0,"actor",0,-1,-1,b.Tile);return b.Tile.Pixels.Select(p=>(d.Px+p.X,d.Py+p.Y));}).Distinct().Count();
        for(int tx=32596;tx<=32600;tx++)for(int ty=32946;ty<=32950;ty++)for(int h=0;h<8;h++){
            if((tx-32598)*(tx-32598)+(ty-32948)*(ty-32948)>4)continue;
            int x=tx-MovementRules.Dx[h],y=ty-MovementRules.Dy[h];if(!MovementRules.Passable(x,y,h))continue;
            double ownMin=1,targetMin=1;
            foreach(var pose in new[]{("breath","breath"),("attack","damage"),("damage","attack"),("breath","die")}){
                var own=catalog.Get(0)!;var npc=catalog.Get(1110)!;var of=own.At(pose.Item1,h,own.Duration(pose.Item1)/2);var nf=npc.At(pose.Item2,(h+4)%8,npc.Duration(pose.Item2)/2);
                var draws=of.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile)).Concat(nf.Blocks.Select(b=>new Draw(2*(tx-Coordinates.MinX)+b.A,ty-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile,123))).ToArray();
                var area=Rectangle.FromLTRB(draws.Min(v=>v.Px),draws.Min(v=>v.Py),draws.Max(v=>v.Px+24),draws.Max(v=>v.Py+24));
                var commands=assets.Scene.Where(c=>c.Px<area.Right&&c.Px+24>area.Left&&c.Py<area.Bottom&&c.Py+24>area.Top).Concat(draws).OrderBy(c=>c,DrawComparer.Instance).ThenBy(c=>c.ObjectId);
                var colors=new ushort[area.Width*area.Height];var mask=new bool[colors.Length];var owners=new int[colors.Length];var entities=new uint?[colors.Length];foreach(var d in commands)Blit(d,area,colors,mask,owners,entities);
                ownMin=Math.Min(ownMin,(double)owners.Where((v,i)=>v==-2&&entities[i]==null).Count()/Count(of));targetMin=Math.Min(targetMin,(double)entities.Count(v=>v==123)/Count(nf));
            }
            rows.Add((x,y,tx,ty,ownMin,targetMin));
        }
        return new{kind="explicit synthetic original-map composition planning only; no game input/target relocation and no runtime acceptance claim",spawn=new{x=32598,y=32948,movementDistance=2,metric="Euclidean getLineDistance; fixed-JAR NpcMove/Point"},candidates=rows.OrderByDescending(v=>Math.Min(v.Own,v.Target)).Take(40).Select(v=>new{ownX=v.X,ownY=v.Y,targetX=v.Tx,targetY=v.Ty,minimumOwnRatio=v.Own,minimumTargetRatio=v.Target})};
    }
}
