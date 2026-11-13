using System.Buffers.Binary;
using System.Globalization;
using P06.NativeUi;
namespace P06.AssetPrep;

// T451 (#1036): first recipe, the TBT → PNG vertical slice of the "inventory" category.
// Item = one icon id (the S_ADD_INVENTORY gfx, ItemIconSource). Lookup and decoding are the ones ItemIconSource uses for
// the local client: BuffIconCatalog.Candidates ("<n>c.tbt" then "<n>.tbt", hashed Sprite archive then Sprite) and
// TbtImage.Decode. The reference-client fallback of ItemIconSource is deliberately not used: a preparer run reads the
// given TW_CLIENT_ROOT only, so an icon the client lacks is an error naming the candidates, not a substitution.
// Output: "<icon>.png" (the layout of the existing assets/inventory) and, in the receipt, the .tbt draw offset, the size
// and the SHA-256 of the decoded RGBA. Coverage is Partial: which icons the product needs and the replay of the 20
// historical files are not closed (portable-delivery.md, inventory row), so the receipt says "partial".

[AssetRecipe]
public sealed class InventoryIconRecipe:IAssetRecipe {
    public const string RecipeId="inventory";
    public const int MaxIcon=ushort.MaxValue;
    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;

    /// <summary>Icon id of an item token: plain decimal 1..65535 without sign, spaces or leading zeros; anything else is null.</summary>
    public static int? ParseIcon(string? item){
        if(item is not {Length:>0 and <=5}||item[0]=='0')return null;
        foreach(char c in item)if(c is <'0' or >'9')return null;
        int icon=int.Parse(item,NumberStyles.None,CultureInfo.InvariantCulture);
        return icon is >0 and <=MaxIcon?icon:null;
    }
    public static string FileName(int icon)=>icon.ToString(CultureInfo.InvariantCulture)+".png";

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        var icons=new SortedSet<int>();
        foreach(string item in items)icons.Add(ParseIcon(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"inventory：\"{item}\" 不是 1–{MaxIcon} 的圖示編號"));
        return icons.Select(i=>i.ToString(CultureInfo.InvariantCulture)).ToList();
    }

    public void Produce(string item,AssetRecipeItemContext context){
        int icon=ParseIcon(item)??throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"inventory：\"{item}\" 不是圖示編號");
        var tried=new List<string>();
        foreach(var (archive,name) in BuffIconCatalog.Candidates(icon)){
            string where=context.SpriteName(archive)+"/"+name;tried.Add(where);
            if(context.TryReadSprite(archive,name) is not {} payload)continue;
            TbtImage image;
            try{image=TbtImage.Decode(payload,where);}
            catch(UiResourceException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"inventory：圖示 {icon} 無法解碼（{ex.Message}）",context.SpriteName(archive),ex);}
            context.Write(FileName(icon),PngWriter.Encode(image.Width,image.Height,image.Argb));
            context.Set("offsetX",image.OffsetX.ToString(CultureInfo.InvariantCulture));
            context.Set("offsetY",image.OffsetY.ToString(CultureInfo.InvariantCulture));
            context.Set("width",image.Width.ToString(CultureInfo.InvariantCulture));
            context.Set("height",image.Height.ToString(CultureInfo.InvariantCulture));
            context.Set("rgbaSha256",AssetPrepPaths.Sha256(PngWriter.Rgba(image.Argb)));
            return;
        }
        throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"inventory：圖示 {icon} 不在 TW_CLIENT_ROOT 的 Sprite 容器（找過 {string.Join("、",tried)}）");
    }
}

/// <summary>
/// Minimal PNG encoder: 8-bit RGBA, no interlace, filter 0 on every row, deflate "stored" blocks. Nothing depends on a
/// compression library or on GDI+, so the same pixels give the same bytes on every machine and runtime version.
/// </summary>
public static class PngWriter {
    static readonly uint[] crcTable=BuildCrcTable();
    static uint[] BuildCrcTable(){
        var table=new uint[256];
        for(uint n=0;n<256;n++){uint c=n;for(int k=0;k<8;k++)c=(c&1)!=0?0xEDB88320u^(c>>1):c>>1;table[n]=c;}
        return table;
    }
    static uint Crc(ReadOnlySpan<byte> data){
        uint c=0xFFFFFFFFu;
        foreach(byte b in data)c=crcTable[(c^b)&0xFF]^(c>>8);
        return c^0xFFFFFFFFu;
    }
    static uint Adler(ReadOnlySpan<byte> data){
        uint a=1,b=0;
        foreach(byte v in data){a=(a+v)%65521;b=(b+a)%65521;}
        return b<<16|a;
    }
    static void Chunk(MemoryStream png,string type,ReadOnlySpan<byte> data){
        var body=new byte[4+data.Length];
        for(int i=0;i<4;i++)body[i]=(byte)type[i];
        data.CopyTo(body.AsSpan(4));
        Span<byte> word=stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word,(uint)data.Length);png.Write(word);
        png.Write(body);
        BinaryPrimitives.WriteUInt32BigEndian(word,Crc(body));png.Write(word);
    }

    /// <summary>0xAARRGGBB pixels (straight alpha) → R, G, B, A bytes.</summary>
    public static byte[] Rgba(ReadOnlySpan<uint> argb){
        var rgba=new byte[argb.Length*4];
        for(int i=0;i<argb.Length;i++){uint p=argb[i];rgba[i*4]=(byte)(p>>16);rgba[i*4+1]=(byte)(p>>8);rgba[i*4+2]=(byte)p;rgba[i*4+3]=(byte)(p>>24);}
        return rgba;
    }

    public static byte[] Encode(int width,int height,ReadOnlySpan<uint> argb){
        if(width<=0||height<=0||width>16384||height>16384||argb.Length!=width*height)throw new ArgumentException("PNG 尺寸與像素數不符");
        byte[] rgba=Rgba(argb);
        int stride=1+width*4;
        var raw=new byte[stride*height];                       // every row: filter byte 0, then RGBA
        for(int y=0;y<height;y++)rgba.AsSpan(y*width*4,width*4).CopyTo(raw.AsSpan(y*stride+1));

        using var zlib=new MemoryStream();
        zlib.WriteByte(0x78);zlib.WriteByte(0x01);              // deflate, 32K window, no preset dictionary
        Span<byte> header=stackalloc byte[5];
        for(int at=0;at<raw.Length;at+=65535){
            int length=Math.Min(65535,raw.Length-at);
            header[0]=(byte)(at+length==raw.Length?1:0);          // BFINAL, BTYPE 00 (stored)
            BinaryPrimitives.WriteUInt16LittleEndian(header[1..],(ushort)length);
            BinaryPrimitives.WriteUInt16LittleEndian(header[3..],(ushort)~length);
            zlib.Write(header);zlib.Write(raw,at,length);
        }
        Span<byte> word=stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word,Adler(raw));zlib.Write(word);

        using var png=new MemoryStream();
        png.Write([0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A]);
        Span<byte> ihdr=stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr,(uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[4..],(uint)height);
        ihdr[8]=8;ihdr[9]=6;ihdr[10]=0;ihdr[11]=0;ihdr[12]=0;   // 8 bits, colour type 6 (RGBA), deflate, adaptive filtering, no interlace
        Chunk(png,"IHDR",ihdr);
        Chunk(png,"IDAT",zlib.ToArray());
        Chunk(png,"IEND",[]);
        return png.ToArray();
    }
}
