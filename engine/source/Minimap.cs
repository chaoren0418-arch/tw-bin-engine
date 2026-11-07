namespace P06;

public sealed record MinimapMarker(uint Id,string Name,int X,int Y,bool Self);

// Captured under LoginConnection.CaptureWorld. Scene and tiles are published cache data;
// the minimap never loads blocks, advances the world, or retains evicted map history.
public sealed record MinimapSnapshot(object CacheIdentity,int Revision,int Epoch,int MapId,
    IReadOnlyList<Draw> Scene,MinimapMarker[] Markers) {
    public static MinimapSnapshot? Capture(WorldSession world){
        if(!world.Ready||world.TeleportPending||world.MapResourceState!="ready"||
            world.MapCache is not {} cache||world.Actor is not {} actor||cache.MapId!=world.MapId||
            !cache.Bounds.Contains(actor.X,actor.Y)||!cache.Contains(actor.X,actor.Y))return null;
        var markers=world.Objects.Where(o=>o.ObjectId!=actor.ObjectId&&!o.Dead&&!o.AppearancePending&&
            (o.Flags&4)==0&&cache.Bounds.Contains(o.X,o.Y)&&cache.Contains(o.X,o.Y))
            .Select(o=>new MinimapMarker(o.ObjectId,o.Name,o.X,o.Y,false)).ToList();
        // S87 has no species/template ID. Non-PC flags include monsters and scenery;
        // do not infer friendly NPCs or party membership from names or gfx.
        markers.Add(new(actor.ObjectId,actor.Name,actor.X,actor.Y,true));
        return new(cache,cache.Revision,world.WorldEpoch,cache.MapId,cache.Scene,markers.ToArray());
    }
}

// Nearest-neighbour sampling of actual cached RGB555 pixels in existing scene order.
// Bounded output avoids allocating the full-resolution ~6000 x 3000 scene a second time.
public sealed class MinimapImage:IDisposable {
    public Bitmap Bitmap {get;}
    public Rectangle SourceBounds {get;}
    MinimapImage(Bitmap bitmap,Rectangle bounds){Bitmap=bitmap;SourceBounds=bounds;}
    public static MinimapImage? Create(IReadOnlyList<Draw> scene){
        var draws=scene.Where(d=>!d.Actor&&!(d.Layer=="ground"&&(d.Tile.Type&8)!=0)).ToArray();
        if(draws.Length==0)return null;
        var bounds=Rectangle.FromLTRB(draws.Min(d=>d.Px),draws.Min(d=>d.Py),draws.Max(d=>d.Px+24),draws.Max(d=>d.Py+24));
        double scale=Math.Min(1,512.0/Math.Max(bounds.Width,bounds.Height));
        int width=Math.Max(1,(int)Math.Ceiling(bounds.Width*scale)),height=Math.Max(1,(int)Math.Ceiling(bounds.Height*scale));
        var colors=new ushort[width*height];var mask=new bool[colors.Length];
        var tiles=new Dictionary<Tile,int[]>(ReferenceEqualityComparer.Instance);
        foreach(var draw in draws){
            if(!tiles.TryGetValue(draw.Tile,out var pixels)){
                pixels=Enumerable.Repeat(-1,24*24).ToArray();
                foreach(var p in draw.Tile.Pixels)if((uint)p.X<24&&(uint)p.Y<24)pixels[p.Y*24+p.X]=p.Color;
                tiles.Add(draw.Tile,pixels);
            }
            int left=Math.Max(0,(int)Math.Floor((draw.Px-bounds.Left)*width/(double)bounds.Width));
            int top=Math.Max(0,(int)Math.Floor((draw.Py-bounds.Top)*height/(double)bounds.Height));
            int right=Math.Min(width,(int)Math.Ceiling((draw.Px+24-bounds.Left)*width/(double)bounds.Width));
            int bottom=Math.Min(height,(int)Math.Ceiling((draw.Py+24-bounds.Top)*height/(double)bounds.Height));
            for(int y=top;y<bottom;y++)for(int x=left;x<right;x++){
                int sx=(int)Math.Floor(bounds.Left+(x+.5)*bounds.Width/width)-draw.Px;
                int sy=(int)Math.Floor(bounds.Top+(y+.5)*bounds.Height/height)-draw.Py;
                if((uint)sx>=24||(uint)sy>=24||pixels[sy*24+sx]<0)continue;
                int index=y*width+x;colors[index]=(ushort)pixels[sy*24+sx];mask[index]=true;
            }
        }
        return new(Renderer.BitmapOf(width,height,colors,mask),bounds);
    }
    public PointF Project(int x,int y){
        var p=Coordinates.Screen(x,y);
        return new((p.X-SourceBounds.Left)*Bitmap.Width/SourceBounds.Width,(p.Y-SourceBounds.Top)*Bitmap.Height/SourceBounds.Height);
    }
    public RectangleF Viewport(int zoom,MinimapMarker self){
        zoom=Math.Clamp(zoom,1,4);float width=Bitmap.Width/(float)zoom,height=Bitmap.Height/(float)zoom;var p=Project(self.X,self.Y);
        return new(Math.Clamp(p.X-width/2,0,Bitmap.Width-width),Math.Clamp(p.Y-height/2,0,Bitmap.Height-height),width,height);
    }
    public void Dispose()=>Bitmap.Dispose();
}

public sealed class Minimap:IDisposable {
    public bool IsOpen {get;private set;}=true;
    public int Zoom {get;private set;}=1;
    public MinimapSnapshot? Snapshot {get;private set;}
    public MinimapImage? Image {get;private set;}
    public void Toggle(){IsOpen=!IsOpen;if(!IsOpen)Clear();}
    public void ZoomBy(int direction)=>Zoom=Math.Clamp(direction>0?Zoom*2:direction<0?Zoom/2:Zoom,1,4);
    public void Update(MinimapSnapshot? snapshot){
        if(!IsOpen||snapshot==null){Clear();return;}
        if(Snapshot is not {} old||!ReferenceEquals(old.CacheIdentity,snapshot.CacheIdentity)||old.Revision!=snapshot.Revision||old.Epoch!=snapshot.Epoch||old.MapId!=snapshot.MapId){
            Clear();Image=MinimapImage.Create(snapshot.Scene);
        }
        Snapshot=snapshot;
    }
    public void Clear(){Image?.Dispose();Image=null;Snapshot=null;}
    public void Dispose()=>Clear();
}
