namespace P06;
public sealed partial class Renderer {
    SceneOpacity? backgroundState;int backgroundRevision=-1;Bitmap? dynamicBackground;ushort[]? dynamicColors;bool[]? dynamicMask;
    OpacityValue[]? backgroundValues;
    public object? LastBackgroundUpdate {get;private set;}
    internal ushort[] SceneColors=>dynamicColors??CurrentBaseColors;
    internal bool[] SceneMask=>dynamicMask??CurrentBaseMask;
    internal void SceneBackground(SceneOpacity? state){
        if(state==backgroundState&&(state?.Revision??-1)==backgroundRevision)return;
        var clock=System.Diagnostics.Stopwatch.StartNew();
        if(state!=backgroundState){dynamicBackground?.Dispose();dynamicBackground=null;dynamicColors=null;dynamicMask=null;backgroundValues=null;}
        backgroundState=state;backgroundRevision=state?.Revision??-1;if(state==null)return;
        if(backgroundValues==null){backgroundValues=ActiveScene.Select(c=>new OpacityValue(InitialFlags(c),0,0)).ToArray();dynamicColors=(ushort[])CurrentBaseColors.Clone();dynamicMask=(bool[])CurrentBaseMask.Clone();dynamicBackground=(Bitmap)(cacheBackground??background).Clone();}
        static (bool,bool,int) Mode(OpacityValue s)=>(s.Hidden,s.Half,s.Transition?s.Alpha:31);
        Rectangle dirty=Rectangle.Empty;int changed=0;
        for(int i=0;i<ActiveScene.Count;i++){var c=ActiveScene[i];var value=state.Get(c);if(Mode(value)!=Mode(backgroundValues[i])){var rect=new Rectangle(c.Px,c.Py,24,24);dirty=dirty.IsEmpty?rect:Rectangle.Union(dirty,rect);changed++;}backgroundValues[i]=value;}
        if(!dirty.IsEmpty){
            dirty=Rectangle.Intersect(dirty,Bounds);var colors=new ushort[dirty.Width*dirty.Height];var mask=new bool[colors.Length];
            for(int i=0;i<ActiveScene.Count;i++){var c=ActiveScene[i];if(c.Px<dirty.Right&&c.Px+24>dirty.Left&&c.Py<dirty.Bottom&&c.Py+24>dirty.Top)Blit(c,dirty,colors,mask,null,null,backgroundValues[i]);}
            for(int y=0;y<dirty.Height;y++){Array.Copy(colors,y*dirty.Width,dynamicColors!,(dirty.Top-Bounds.Top+y)*Width+dirty.Left-Bounds.Left,dirty.Width);Array.Copy(mask,y*dirty.Width,dynamicMask!,(dirty.Top-Bounds.Top+y)*Width+dirty.Left-Bounds.Left,dirty.Width);}
            var bits=dynamicBackground!.LockBits(new Rectangle(dirty.X-Bounds.X,dirty.Y-Bounds.Y,dirty.Width,dirty.Height),System.Drawing.Imaging.ImageLockMode.WriteOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            unsafe{for(int y=0;y<dirty.Height;y++){int* row=(int*)((byte*)bits.Scan0+y*bits.Stride);for(int x=0;x<dirty.Width;x++){int i=y*dirty.Width+x;row[x]=MaskedArgb(colors[i],mask[i]);}}}dynamicBackground.UnlockBits(bits);
        }
        LastBackgroundUpdate=new{milliseconds=clock.Elapsed.TotalMilliseconds,changed,dirty,pixels=dirty.Width*dirty.Height,revision=backgroundRevision};
    }
    public static ushort Weighted(ushort source,ushort destination,int weight){
        if(weight is <0 or >31)throw new ArgumentOutOfRangeException(nameof(weight));int other=31-weight;
        return (ushort)((((destination&31)*other+(source&31)*weight)/31)|(((((destination>>5)&31)*other+((source>>5)&31)*weight)/31)<<5)|(((((destination>>10)&31)*other+((source>>10)&31)*weight)/31)<<10));
    }
    public Bitmap SceneView(Patch patch,Rectangle area){
        area=Rectangle.Intersect(area,Bounds);if(HasShipBackdrop)return ShipSceneView(patch,area);var colors=new ushort[area.Width*area.Height];var mask=new bool[colors.Length];var bg=dynamicColors??CurrentBaseColors;var bm=dynamicMask??CurrentBaseMask;
        for(int y=0;y<area.Height;y++){Array.Copy(bg,(area.Y-Bounds.Y+y)*Width+area.X-Bounds.X,colors,y*area.Width,area.Width);Array.Copy(bm,(area.Y-Bounds.Y+y)*Width+area.X-Bounds.X,mask,y*area.Width,area.Width);}
        var overlap=Rectangle.Intersect(area,patch.Bounds);for(int y=overlap.Top;y<overlap.Bottom;y++){int src=(y-patch.Bounds.Top)*patch.Bounds.Width+overlap.Left-patch.Bounds.Left,dst=(y-area.Top)*area.Width+overlap.Left-area.Left;Array.Copy(patch.Colors,src,colors,dst,overlap.Width);Array.Copy(patch.Mask,src,mask,dst,overlap.Width);}
        return BitmapOf(area.Width,area.Height,colors,mask);
    }
}


