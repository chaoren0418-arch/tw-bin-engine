using System.Drawing.Text;
namespace P06.NativeUi;

/// <summary>
/// The single place for native UI text (§3.2). Text is drawn on the 800×600 canvas and scales with its pixels; it is never
/// re-rendered at the window resolution.
/// #1205: the original builds its UI font with CreateFontA(12,0,0,0,400,0,0,0,136,0,0,0,0,"MingLiU") (sub_8444A0, UI§1 V4,
/// same call as NativeNameFont): a 12-<b>pixel</b> cell, FW_NORMAL, CHINESEBIG5. The font is therefore sized in pixels
/// (GraphicsUnit.Pixel), never in points: Program.Main sets PerMonitorV2, so every new Bitmap — the canvas included —
/// carries the monitor DPI, and a 9 pt font became 24 px on a DPI 192 canvas (#1186 SM10/12/16/18/20/33).
/// </summary>
public static class UiText {
    /// <summary>Original face; when it is not installed the Big5 proportional sibling, then the old default, is used.</summary>
    public const string FaceName="MingLiU";
    public static readonly string[] FaceFallbacks=["MingLiU","細明體","PMingLiU","新細明體"];
    /// <summary>CreateFontA height 12 (cell height in pixels) — also the layout line height every panel is built on.</summary>
    public const int PixelHeight=12;
    public const byte CharSet=136;
    public const TextRenderingHint Hint=TextRenderingHint.SingleBitPerPixelGridFit;
    static readonly Lazy<Font> font=new(()=>new Font(ResolveFace(),PixelHeight,FontStyle.Regular,GraphicsUnit.Pixel,CharSet));
    public static Font Font=>font.Value;
    /// <summary>Family actually used (first installed of FaceFallbacks; GDI+ substitution otherwise).</summary>
    public static string FaceUsed=>Font.FontFamily.Name;
    static readonly StringFormat format=CreateFormat();
    static StringFormat CreateFormat(){var f=(StringFormat)StringFormat.GenericTypographic.Clone();f.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.MeasureTrailingSpaces;return f;}

    static string ResolveFace(){
        foreach(var name in FaceFallbacks){
            try{using var family=new FontFamily(name);return family.Name;}catch(ArgumentException){}
        }
        return FaceName;
    }

    /// <summary>Draws one line at a logical point. shadow != null draws a 1-pixel drop shadow first (+1, +1).</summary>
    public static void Draw(Graphics g,string text,int x,int y,Color color,Color? shadow=null){
        if(string.IsNullOrEmpty(text))return;
        var old=g.TextRenderingHint;g.TextRenderingHint=Hint;
        try{
            if(shadow is Color s){using var sb=new SolidBrush(s);g.DrawString(text,Font,sb,x+1,y+1,format);}
            using var b=new SolidBrush(color);g.DrawString(text,Font,b,x,y,format);
        }finally{g.TextRenderingHint=old;}
    }
    /// <summary>Size of one line in canvas pixels; independent of the Graphics DPI (pixel-unit font).</summary>
    public static Size Measure(Graphics g,string text){
        if(string.IsNullOrEmpty(text))return Size.Empty;
        var old=g.TextRenderingHint;g.TextRenderingHint=Hint;
        try{var s=g.MeasureString(text,Font,PointF.Empty,format);return new((int)Math.Ceiling(s.Width),(int)Math.Ceiling(s.Height));}
        finally{g.TextRenderingHint=old;}
    }
}
