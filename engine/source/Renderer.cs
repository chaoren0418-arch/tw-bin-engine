using System.Drawing.Imaging;
namespace P06;
public sealed record Patch(Rectangle Bounds,ushort[] Colors,bool[] Mask,int[] Owners,List<Draw> Commands,uint?[]? EntityOwners=null){
    // Built once after composition from final opaque-pixel ownership, never from bounds.
    public IReadOnlySet<uint> VisibleObjectIds {get;}=Collect(EntityOwners);
    static HashSet<uint> Collect(uint?[]? owners){var result=new HashSet<uint>();if(owners!=null)foreach(var id in owners)if(id is uint value)result.Add(value);return result;}
}
public sealed partial class Renderer:IDisposable {
    // Paint-only: Camera and hit testing keep the true scene position.
    public static void ApplyScreenShake(Graphics graphics,Point offset){if(!offset.IsEmpty)graphics.TranslateTransform(offset.X,offset.Y);}
    /// <summary>Screen-shake hook (R2-O / PBW-A will set it): a paint-only offset in view pixels applied by PaintScene
    /// before the world is drawn. Empty = no shake.</summary>
    public Func<Point>? ScreenShake {get;set;}
    public int Width=>Bounds.Width;
    public int Height=>Bounds.Height;
    public Rectangle Bounds=>cacheBounds??new(0,0,assets.CanvasWidth,assets.CanvasHeight);
    Rectangle? cacheBounds;OriginalMapCache? mapCache;int mapCacheRevision=-1;Bitmap? cacheBackground;ushort[]? cacheBaseColors;bool[]? cacheBaseMask;
    IReadOnlyList<Draw> ActiveScene=>mapCache?.Scene??assets.Scene;
    ushort[] CurrentBaseColors=>cacheBaseColors??BaseColors;bool[] CurrentBaseMask=>cacheBaseMask??BaseMask;
    void BindCache(OriginalMapCache? cache){if(cache==mapCache&&(cache?.Revision??-1)==mapCacheRevision)return;mapCache=cache;mapCacheRevision=cache?.Revision??-1;cacheBackground?.Dispose();cacheBackground=null;cacheBaseColors=null;cacheBaseMask=null;cacheBounds=null;dynamicBackground?.Dispose();dynamicBackground=null;dynamicColors=null;dynamicMask=null;backgroundValues=null;backgroundState=null;backgroundRevision=-1;
        if(cache==null)return;var scene=cache.Scene;cacheBounds=Rectangle.FromLTRB(scene.Min(c=>c.Px),scene.Min(c=>c.Py),scene.Max(c=>c.Px+24),scene.Max(c=>c.Py+24));cacheBaseColors=new ushort[Width*Height];cacheBaseMask=new bool[Width*Height];foreach(var c in scene)Blit(c,Bounds,cacheBaseColors,cacheBaseMask,null);cacheBackground=BitmapOf(Width,Height,cacheBaseColors,cacheBaseMask);
    }
    readonly Assets assets;
    public ushort[] BaseColors {get;}
    public bool[] BaseMask {get;}
    readonly Bitmap background;
    public Bitmap Background=>dynamicBackground??cacheBackground??background;
    internal void ReleaseMap(){BindCache(null);MapEffectVisuals=[];}
    internal void RenderCache(OriginalMapCache cache,SceneOpacity? opacity=null){BindCache(cache);SceneBackground(opacity);}
    public Renderer(Assets a) {assets=a;BaseColors=new ushort[Width*Height];BaseMask=new bool[Width*Height];foreach(var c in a.Scene)Blit(c,new(0,0,Width,Height),BaseColors,BaseMask,null);background=BitmapOf(Width,Height,BaseColors,BaseMask);}
    public static ushort Blend(ushort a,ushort b)=>(ushort)((((a&31)+(b&31))>>1)|(((((a>>5)&31)+((b>>5)&31))>>1)<<5)|(((((a>>10)&31)+((b>>10)&31))>>1)<<10));
    public static int InitialFlags(Draw c)=>c.Layer=="shadow"?5:c.Layer=="ground"?0:(c.Tile.Type&16)!=0?8:(c.Tile.Type&4)!=0?256:(c.Tile.Type&32)!=0?128:0;
    // T459（#1044）：screen 表改由 BlendRules 在啟動時依原碼算式算出，不再讀 assets/cache/screen-channel.bin。
    public static ushort ScreenBlend(ushort a,ushort b)=>BlendRules.Compose(ChannelBlend.Screen,a,b);
    public static ushort MultiplyBlend(ushort a,ushort b)=>(ushort)(((a&31)*(b&31)/31)|((((a>>5)&31)*((b>>5)&31)/31)<<5)|((((a>>10)&31)*((b>>10)&31)/31)<<10));
    public static int Argb(ushort c) {int r=(c>>10)&31,g=(c>>5)&31,b=c&31;return unchecked((int)0xff000000)|((r*8+r/4)<<16)|((g*8+g/4)<<8)|(b*8+b/4);}
    // Sparse original SPR runs define coverage. Zero RGB555 is a valid black pixel.
    public static int MaskedArgb(ushort color,bool covered)=>covered?Argb(color):0;
    public static Bitmap BitmapOf(int w,int h,ushort[] colors,bool[] mask) {
        var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);var bits=b.LockBits(new(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        unsafe {for(int y=0;y<h;y++){int* row=(int*)((byte*)bits.Scan0+y*bits.Stride);for(int x=0;x<w;x++)row[x]=MaskedArgb(colors[y*w+x],mask[y*w+x]);}}
        b.UnlockBits(bits);return b;
    }
    public List<Draw> Actor(World w){var frame=w.Frame;var draws=frame.Blocks.Select(b=>new Draw(2*(w.X-Coordinates.MinX)+b.A,w.Y-Coordinates.MinY+b.B,b.Z,1000000+b.Order,"actor",b.Order,-1,-1,b.Tile)).ToList();draws.AddRange(ShadowDraws(w.OnlineAppearance,frame,w.X,w.Y));return draws;}
    public Patch Compose(World w) {
        BindCache(null);SceneBackground(null);var actor=Actor(w);int left=actor.Min(x=>x.Px),top=actor.Min(x=>x.Py),right=actor.Max(x=>x.Px+24),bottom=actor.Max(x=>x.Py+24);
        var r=Rectangle.Intersect(new(left,top,right-left,bottom-top),new(0,0,Width,Height));
        var commands=assets.Scene.Where(c=>c.Px<r.Right&&c.Px+24>r.Left&&c.Py<r.Bottom&&c.Py+24>r.Top).Concat(actor).OrderBy(c=>c,DrawComparer.Instance).ToList();
        var colors=new ushort[r.Width*r.Height];var mask=new bool[colors.Length];var owners=Enumerable.Repeat(-1,colors.Length).ToArray();
        foreach(var c in commands)Blit(c,r,colors,mask,owners);
        return new(r,colors,mask,owners,commands);
    }
    public static void Blit(Draw c,Rectangle r,ushort[] colors,bool[] mask,int[]? owners,uint?[]? entityOwners=null,OpacityValue? opacity=null) {
        // Original VA F00640 skips ground tiles with bit 8; the ship LAC is drawn beneath.
        if(c.Layer=="ground"&&(c.Tile.Type&8)!=0)return;
        int px=c.Px,py=c.Py;var state=opacity??new OpacityValue(InitialFlags(c),0,0);if(state.Hidden)return;bool blend=state.Half;int weight=state.Transition?state.Alpha:31;bool screen=(state.Flags&8)!=0,multiply=(state.Flags&128)!=0&&(state.Transition||!blend);if((weight<=0||weight>31)&&!screen&&!multiply)return;
        foreach(var p in c.Tile.Pixels) {
            int x=px+p.X-r.Left,y=py+p.Y-r.Top;if((uint)x>=(uint)r.Width||(uint)y>=(uint)r.Height)continue;
            int i=y*r.Width+x;colors[i]=screen?ScreenBlend(p.Color,colors[i]):multiply?MultiplyBlend(p.Color,colors[i]):state.Transition?Weighted(p.Color,colors[i],weight):blend?Blend(p.Color,colors[i]):p.Color;mask[i]=true;bool opaque=!screen&&!multiply&&!blend&&weight==31;if(owners!=null&&(opaque||owners[i]<0))owners[i]=c.Actor?-2:c.Layer=="objects"?c.Serial:-1;
            if(entityOwners!=null&&c.Layer!="shadow"&&(opaque||c.Actor))entityOwners[i]=c.Actor&&c.ObjectId!=0?c.ObjectId:null;
        }
    }
    public Bitmap Full(World w) {var p=Compose(w);var c=(ushort[])BaseColors.Clone();var m=(bool[])BaseMask.Clone();for(int y=0;y<p.Bounds.Height;y++){Array.Copy(p.Colors,y*p.Bounds.Width,c,(y+p.Bounds.Y)*Width+p.Bounds.X,p.Bounds.Width);Array.Copy(p.Mask,y*p.Bounds.Width,m,(y+p.Bounds.Y)*Width+p.Bounds.X,p.Bounds.Width);}return BitmapOf(Width,Height,c,m);}
    public void Dispose(){dynamicBackground?.Dispose();cacheBackground?.Dispose();background.Dispose();}
}
