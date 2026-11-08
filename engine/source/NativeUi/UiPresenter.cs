using System.Drawing.Drawing2D;
namespace P06.NativeUi;

/// <summary>Where the 800×600 canvas lands in a client area. Scale is the pixel multiplier; Destination is in client pixels.</summary>
public readonly record struct UiViewport(float Scale,int OffsetX,int OffsetY,int Width,int Height) {
    public Rectangle Destination=>new(OffsetX,OffsetY,Width,Height);
    public bool Integer=>Scale==MathF.Floor(Scale);

    /// <summary>
    /// Client pixel → logical pixel: lx = floor((mx-ox)/s), ly = floor((my-oy)/s) (§3.3). null when the point is on the
    /// black border (outside the 800×600 picture); such input reaches neither the UI nor the world.
    /// </summary>
    public Point? ToLogical(Point client){
        if(client.X<OffsetX||client.Y<OffsetY||client.X>=OffsetX+Width||client.Y>=OffsetY+Height)return null;
        int lx=(int)MathF.Floor((client.X-OffsetX)/Scale),ly=(int)MathF.Floor((client.Y-OffsetY)/Scale);
        // Non-integer fill can round the last column/row to 800/600; clamp onto the picture it was drawn from.
        return new(Math.Clamp(lx,0,UiRoot.LogicalWidth-1),Math.Clamp(ly,0,UiRoot.LogicalHeight-1));
    }
    /// <summary>Like ToLogical but clamps border points onto the canvas edge (used only while a drag/capture is active).</summary>
    public Point ToLogicalClamped(Point client){
        int lx=(int)MathF.Floor((client.X-OffsetX)/Scale),ly=(int)MathF.Floor((client.Y-OffsetY)/Scale);
        return new(Math.Clamp(lx,0,UiRoot.LogicalWidth-1),Math.Clamp(ly,0,UiRoot.LogicalHeight-1));
    }
    /// <summary>Logical point → top-left client pixel of that logical pixel.</summary>
    public Point ToClient(Point logical)=>new(OffsetX+(int)MathF.Floor(logical.X*Scale),OffsetY+(int)MathF.Floor(logical.Y*Scale));
}

/// <summary>
/// Presents the logical canvas in the window (§3.2). Default: integer scale, centred, black border, nearest neighbour:
/// s = max(1, floor(min(W/800, H/600))), ox = (W-800s)/2, oy = (H-600s)/2. Optional "fill" (setting, default off):
/// s = min(W/800, H/600), may be fractional, still nearest neighbour. Windows DPI does not take part (AutoScaleMode.None).
/// </summary>
public static class UiPresenter {
    public static UiViewport Compute(Size client,bool fill=false){
        int w=Math.Max(0,client.Width),h=Math.Max(0,client.Height);
        if(fill){
            float s=Math.Min(w/(float)UiRoot.LogicalWidth,h/(float)UiRoot.LogicalHeight);
            if(!(s>0))s=1;
            int dw=(int)MathF.Round(UiRoot.LogicalWidth*s),dh=(int)MathF.Round(UiRoot.LogicalHeight*s);
            return new(s,(w-dw)/2,(h-dh)/2,dw,dh);
        }
        int k=Math.Max(1,Math.Min(w/UiRoot.LogicalWidth,h/UiRoot.LogicalHeight));
        // Integer division of non-negative ints is floor; a client smaller than 800×600 is unsupported (MinimumSize) and gets s=1.
        return new(k,(w-UiRoot.LogicalWidth*k)/2,(h-UiRoot.LogicalHeight*k)/2,UiRoot.LogicalWidth*k,UiRoot.LogicalHeight*k);
    }

    /// <summary>One DrawImage of the whole canvas; everything outside the picture is black.</summary>
    public static void Present(Graphics g,Bitmap canvas,Size client,UiViewport view){
        var state=g.Save();
        try{
            g.ResetClip();g.ResetTransform();
            using(var region=new Region(new Rectangle(Point.Empty,client))){region.Exclude(view.Destination);g.FillRegion(Brushes.Black,region);}
            g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.SmoothingMode=SmoothingMode.None;
            // No ImageAttributes: a TileFlipXY wrap mode was measured to blend 1:1 output (255→253); plain NearestNeighbor + Half is exact.
            g.DrawImage(canvas,view.Destination,new Rectangle(0,0,canvas.Width,canvas.Height),GraphicsUnit.Pixel);
        }finally{g.Restore(state);}
    }
}
