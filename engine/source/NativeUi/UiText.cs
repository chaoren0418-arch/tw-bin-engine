using System.Drawing.Text;
namespace P06.NativeUi;

/// <summary>
/// The single place for native UI text (§3.2). Text is drawn on the 800×600 canvas and scales with its pixels; it is never
/// re-rendered at the window resolution. The original face/size is unverified (V4 (f)); until then: 新細明體 9pt, 1-bit grid fit.
/// </summary>
public static class UiText {
    public const string FaceName="新細明體";
    public const float PointSize=9f;
    public const TextRenderingHint Hint=TextRenderingHint.SingleBitPerPixelGridFit;
    static readonly Lazy<Font> font=new(()=>new Font(FaceName,PointSize,FontStyle.Regular,GraphicsUnit.Point));
    public static Font Font=>font.Value;
    static readonly StringFormat format=CreateFormat();
    static StringFormat CreateFormat(){var f=(StringFormat)StringFormat.GenericTypographic.Clone();f.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.MeasureTrailingSpaces;return f;}

    /// <summary>Draws one line at a logical point. shadow != null draws a 1-pixel drop shadow first (+1, +1).</summary>
    public static void Draw(Graphics g,string text,int x,int y,Color color,Color? shadow=null){
        if(string.IsNullOrEmpty(text))return;
        var old=g.TextRenderingHint;g.TextRenderingHint=Hint;
        try{
            if(shadow is Color s){using var sb=new SolidBrush(s);g.DrawString(text,Font,sb,x+1,y+1,format);}
            using var b=new SolidBrush(color);g.DrawString(text,Font,b,x,y,format);
        }finally{g.TextRenderingHint=old;}
    }
    public static Size Measure(Graphics g,string text){
        if(string.IsNullOrEmpty(text))return Size.Empty;
        var old=g.TextRenderingHint;g.TextRenderingHint=Hint;
        try{var s=g.MeasureString(text,Font,PointF.Empty,format);return new((int)Math.Ceiling(s.Width),(int)Math.Ceiling(s.Height));}
        finally{g.TextRenderingHint=old;}
    }
}
