using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using P06.AssetPrep;
using P06.NativeUi;
namespace P06;

/// <summary>
/// T469 (#1054, portable-delivery.md「本機生成與啟動」第 4 點): F9 panel item icons that assets/inventory does not have
/// are made on this machine from the pinned client's "&lt;icon&gt;.tbt" — the lookup and decode of ItemIconSource (the same
/// source the native windows draw from), encoded by the asset preparer's PngWriter, so the bytes equal what the
/// "inventory" recipe (InventoryIconRecipe) writes for the same .tbt: the decoded image as it is, the draw offset not
/// applied (the layout of the 20 historical PNGs). Nothing is written to disk; one PNG per decoded bitmap is kept in
/// memory for as long as the icon source keeps that bitmap. A missing or undecodable icon is null (the page gets 404).
/// </summary>
public static class WebIconGenerator {
    static readonly ConditionalWeakTable<Bitmap,byte[]> cache=new();

    /// <summary>Decoded icon bitmap, or null when the source has none or the entry cannot be read / decoded.</summary>
    public static Bitmap? Decoded(ItemIconSource source,ushort icon){
        try{return source.Get(icon)?.Bitmap;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){
            UiResourceLog.Write($"web-icon: 圖示 {icon} 無法讀取或解碼：{ex.Message}");return null;
        }
    }

    /// <summary>PNG bytes of the icon, or null (nothing is substituted).</summary>
    public static byte[]? Png(ItemIconSource source,ushort icon)=>Decoded(source,icon) is {} bitmap?cache.GetValue(bitmap,Encode):null;

    static byte[] Encode(Bitmap bitmap){
        lock(bitmap){
            int w=bitmap.Width,h=bitmap.Height;var pixels=new int[w*h];
            var bits=bitmap.LockBits(new(0,0,w,h),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            try{for(int y=0;y<h;y++)Marshal.Copy(bits.Scan0+y*bits.Stride,pixels,y*w,w);}
            finally{bitmap.UnlockBits(bits);}
            return PngWriter.Encode(w,h,MemoryMarshal.Cast<int,uint>(pixels));
        }
    }
}
