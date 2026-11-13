using System.Buffers.Binary;
namespace P06;
/// <summary>三種 5-bit 色版混色。</summary>
public enum ChannelBlend { Screen, Multiply, Half }
/// <summary>
/// T459（#1044）：混色查表改由程式計算，不再讀 <c>assets/cache/*-channel.bin</c>。
/// 算式依 T448（#1033，docs/reference/asset-recipes/motion-blend.md）：原 BIN 在 VA 0x0097B700（File Offset 0x0057AB00）的
/// 產生函式於啟動時用兩層 0–31 迴圈算出各表；BIN 檔內沒有表的內容。下列整數式已在 T448 窮舉 1,024 格，與基線三個檔逐 byte 相同。
/// 表的版面與舊檔案相同：第 a*32+b 個值是 a、b 混色後的色版值（未移位版）。
/// </summary>
public static class BlendRules {
    public const int ChannelMax=31,ChannelValues=32,TableEntries=ChannelValues*ChannelValues,TableBytes=TableEntries*2;
    /// <summary>screen：hi + (31-hi)*lo/31。原碼是 double 的 trunc(((31.0-hi)/31.0)*lo+hi)，T448 已證明兩者 1,024 格全同；產品用整數式避免浮點。</summary>
    public static int Screen(int a,int b){Range(a,b);int hi=Math.Max(a,b),lo=Math.Min(a,b);return hi+(ChannelMax-hi)*lo/ChannelMax;}
    /// <summary>multiply：a*b/31（原碼 imul、cdq、idiv 31）。</summary>
    public static int Multiply(int a,int b){Range(a,b);return a*b/ChannelMax;}
    /// <summary>half：(a+b)/2（原碼 add、cdq、sub eax,edx、sar eax,1；輸入非負，等於向下取整）。</summary>
    public static int Half(int a,int b){Range(a,b);return (a+b)>>1;}
    public static int Channel(ChannelBlend kind,int a,int b)=>kind switch{
        ChannelBlend.Screen=>Screen(a,b),ChannelBlend.Multiply=>Multiply(a,b),ChannelBlend.Half=>Half(a,b),
        _=>throw new ArgumentOutOfRangeException(nameof(kind))};
    static void Range(int a,int b){if((uint)a>ChannelMax||(uint)b>ChannelMax)throw new ArgumentOutOfRangeException(nameof(a),"channel value must be 0..31");}

    // 啟動時（型別初始化）各算一次，與原碼在初始化時產生表的做法相同。
    static readonly ushort[] screen=Build(ChannelBlend.Screen),multiply=Build(ChannelBlend.Multiply),half=Build(ChannelBlend.Half);
    static ushort[] Build(ChannelBlend kind){var t=new ushort[TableEntries];for(int a=0;a<ChannelValues;a++)for(int b=0;b<ChannelValues;b++)t[a*ChannelValues+b]=(ushort)Channel(kind,a,b);return t;}
    static ushort[] Of(ChannelBlend kind)=>kind switch{ChannelBlend.Screen=>screen,ChannelBlend.Multiply=>multiply,ChannelBlend.Half=>half,_=>throw new ArgumentOutOfRangeException(nameof(kind))};
    /// <summary>算好的表（唯讀）；版面與 <c>cache/&lt;kind&gt;-channel.bin</c> 解出的 u16 陣列相同。</summary>
    public static IReadOnlyList<ushort> Table(ChannelBlend kind)=>Array.AsReadOnly(Of(kind));

    /// <summary>以 RGB555 三個色版各查一次表再組回去（與舊版讀檔的 ScreenBlend 相同的查法）。</summary>
    public static ushort Compose(ChannelBlend kind,ushort a,ushort b)=>Compose(Of(kind),a,b);
    /// <summary>用任一張 1,024 格的表組色；舊檔案解出的表也可以直接傳進來。</summary>
    public static ushort Compose(IReadOnlyList<ushort> t,ushort a,ushort b){
        if(t.Count!=TableEntries)throw new ArgumentException("channel table must have 1024 entries",nameof(t));
        return (ushort)(t[(a&31)*32+(b&31)]|(t[((a>>5)&31)*32+((b>>5)&31)]<<5)|(t[((a>>10)&31)*32+((b>>10)&31)]<<10));
    }

    /// <summary>檔案版契約：2,048 bytes，32×32 個 little-endian u16。準備器若仍要寫出 <c>cache/*-channel.bin</c>，用這個。</summary>
    public static byte[] ChannelFileBytes(ChannelBlend kind){var t=Of(kind);var bytes=new byte[TableBytes];for(int i=0;i<TableEntries;i++)BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i*2),t[i]);return bytes;}
    /// <summary>讀舊格式的檔案內容（與 T459 前 Renderer 的讀法相同：只接受 2,048 bytes）。</summary>
    public static ushort[] ParseChannelFile(ReadOnlySpan<byte> bytes){
        if(bytes.Length!=TableBytes)throw new InvalidDataException("channel table must be 2048 bytes");
        var t=new ushort[TableEntries];for(int i=0;i<TableEntries;i++)t[i]=BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(i*2));return t;
    }
    public static string FileName(ChannelBlend kind)=>kind switch{ChannelBlend.Screen=>"screen-channel.bin",ChannelBlend.Multiply=>"multiply-channel.bin",ChannelBlend.Half=>"half-channel.bin",_=>throw new ArgumentOutOfRangeException(nameof(kind))};
}
