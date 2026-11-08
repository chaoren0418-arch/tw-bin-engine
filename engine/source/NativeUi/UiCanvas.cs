using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace P06.NativeUi;

/// <summary>
/// The 800×600 logical canvas (§3.2): one off-screen 32bpp PArgb bitmap per frame, composed bottom-up and then presented once by
/// UiPresenter. While a panel paints, the clip is that panel's Bounds, so a module cannot draw outside its own rectangle.
/// </summary>
public sealed class UiCanvas:IDisposable {
    public Bitmap Bitmap{get;}
    public Graphics Graphics{get;}
    public Rectangle Clip{get;private set;}=UiRoot.Canvas;
    public UiCanvas(){
        Bitmap=new Bitmap(UiRoot.LogicalWidth,UiRoot.LogicalHeight,PixelFormat.Format32bppPArgb);
        Graphics=Graphics.FromImage(Bitmap);
        Reset();
    }
    /// <summary>Original pixels only: 1:1 copies, no smoothing, no blending modes beyond source-over.</summary>
    void Reset(){
        Graphics.ResetTransform();Graphics.ResetClip();Clip=UiRoot.Canvas;
        Graphics.InterpolationMode=InterpolationMode.NearestNeighbor;Graphics.PixelOffsetMode=PixelOffsetMode.Half;
        Graphics.SmoothingMode=SmoothingMode.None;Graphics.CompositingMode=CompositingMode.SourceOver;Graphics.TextRenderingHint=UiText.Hint;
    }
    public void Clear(Color color){Reset();Graphics.Clear(color);}
    /// <summary>Restricts drawing to rect ∩ canvas until the next Restrict/Release.</summary>
    public void Restrict(Rectangle rect){Reset();Clip=Rectangle.Intersect(rect,UiRoot.Canvas);Graphics.SetClip(Clip);}
    public void Release()=>Reset();

    /// <summary>Draws a surface 1:1 at a logical point. null (missing surf) draws nothing — no placeholder (rule 5, §2.3).</summary>
    public void DrawSurface(UiSurface? surface,int x,int y){if(surface!=null)DrawBitmap(surface.Bitmap,x,y);}
    /// <summary>Draws part of a surface 1:1 (e.g. a gauge cropped to a width). Empty/out-of-range sources draw nothing.</summary>
    public void DrawSurface(UiSurface? surface,Rectangle source,int x,int y){if(surface!=null)DrawBitmap(surface.Bitmap,source,x,y);}
    public void DrawBitmap(Bitmap bitmap,int x,int y)=>DrawBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height),x,y);
    public void DrawBitmap(Bitmap bitmap,Rectangle source,int x,int y){
        source=Rectangle.Intersect(source,new Rectangle(0,0,bitmap.Width,bitmap.Height));
        if(source.Width<=0||source.Height<=0)return;
        Graphics.DrawImage(bitmap,new Rectangle(x,y,source.Width,source.Height),source,GraphicsUnit.Pixel);
    }
    public void DrawText(string text,int x,int y,Color color,Color? shadow=null)=>UiText.Draw(Graphics,text,x,y,color,shadow);
    public void Dispose(){Graphics.Dispose();Bitmap.Dispose();}
}
