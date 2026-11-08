using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace P06.NativeUi;

/// <summary>
/// Decoded UI image (memory only). Bitmap is 32bpp PArgb. Owned by SurfCatalog's cache: callers must not Dispose it
/// and should not keep it beyond the current paint; an evicted surface is left to the GC (never disposed under a caller).
/// </summary>
public sealed class UiSurface {
    public int Surf{get;}
    public string FileName{get;}
    public string Archive{get;}
    public int Width{get;}
    public int Height{get;}
    /// <summary>Raw u16 flag field (V4 (a): only 0/1 observed; ≠0 enables the colour key).</summary>
    public ushort Flags{get;}
    public ushort TransparentColor{get;}
    public Bitmap Bitmap{get;}
    internal long Cost=>(long)Width*Height*4;
    internal UiSurface(int surf,string fileName,string archive,UiImage image){
        Surf=surf;FileName=fileName;Archive=archive;Width=image.Width;Height=image.Height;Flags=image.Flags;TransparentColor=image.TransparentColor;
        Bitmap=image.ToBitmap();
    }
}

/// <summary>.img decoder: u16 width, u16 height, u16 flags, u16 transparent colour, width×height RGB555 (little endian).</summary>
public sealed record UiImage(int Width,int Height,ushort Flags,ushort TransparentColor,uint[] Argb) {
    public const int MaxSide=4096;
    /// <summary>RGB555, R in the high bits (0x7C00); each 5-bit channel → v*255/31. Pixel == key → alpha 0 only when flags ≠ 0.</summary>
    public static uint ToArgb(ushort pixel){
        uint r=(uint)((pixel>>10)&31)*255/31,g=(uint)((pixel>>5)&31)*255/31,b=(uint)(pixel&31)*255/31;
        return 0xFF000000u|(r<<16)|(g<<8)|b;
    }
    public static UiImage Decode(ReadOnlySpan<byte> data,string name="img"){
        if(data.Length<8)throw new UiResourceException($"{name}: .img 標頭截斷");
        int w=BinaryPrimitives.ReadUInt16LittleEndian(data),h=BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        ushort flags=BinaryPrimitives.ReadUInt16LittleEndian(data[4..]),key=BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        if(w is <=0 or >MaxSide||h is <=0 or >MaxSide)throw new UiResourceException($"{name}: .img 尺寸 {w}×{h} 不合理");
        if(8L+(long)w*h*2!=data.Length)throw new UiResourceException($"{name}: 8+寬×高×2 ≠ 檔案大小（{w}×{h}，{data.Length} byte）");
        var argb=new uint[w*h];bool keyed=flags!=0;
        var px=data[8..];
        for(int i=0;i<argb.Length;i++){
            ushort p=BinaryPrimitives.ReadUInt16LittleEndian(px.Slice(i*2,2));
            // Premultiplied: a keyed pixel is fully transparent black.
            argb[i]=keyed&&p==key?0u:ToArgb(p);
        }
        return new(w,h,flags,key,argb);
    }
    public Bitmap ToBitmap(){
        var bitmap=new Bitmap(Width,Height,PixelFormat.Format32bppPArgb);
        var data=bitmap.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try{
            var row=new int[Width];
            for(int y=0;y<Height;y++){
                for(int x=0;x<Width;x++)row[x]=unchecked((int)Argb[y*Width+x]);
                Marshal.Copy(row,0,data.Scan0+y*data.Stride,Width);
            }
        }finally{bitmap.UnlockBits(data);}
        return bitmap;
    }
}
