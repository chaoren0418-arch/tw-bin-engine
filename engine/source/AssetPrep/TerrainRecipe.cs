using System.Buffers.Binary;
using System.Globalization;
using System.Text;
namespace P06.AssetPrep;

// T458 (#1043): recipe `terrain` (docs/design/asset-recipe.md §3.4, §6; rules: docs/reference/asset-recipes/terrain.md, T445).
// The terrain byte of every cell is derived from the attribute layer of the client's own S32 / SEG blocks:
//   item "maps"          → maps/terrain/<mapId>.bin for every map the shared scan (MapScan, T456) selects
//   item "terrain0"      → terrain0.bin             (map 0, x 32576–32639, y 32896–33023, 64 columns × 128 rows)
//   item "original-map0" → cache/original-map0.txt  (map 0, x 32256–32767, y 32768–33279 as decimal CSV, CRLF rows)
// The `maps` recipe writes maps[].terrain and the cache terrain entry with the SHA-256 of the same bytes: both call
// MapTerrain.Read / TerrainRules, so the two recipes cannot disagree on a value or on which map is unsupported.
// Known limit L3 (asset-recipe.md §7): the zone bits 4–5 of maps 0, 4, 42, 70, 244, 440, 2101, 2151 differ from the r29
// baseline in 13,567 cells; the product reads only bit 0, bit 1 and bit 7, which match on all 713 maps. Every map is
// produced anyway (§3.4); the receipt lists the L3 maps it produced.
// A map with a block that cannot be read (D3) gets no terrain file and is listed as unsupported, never "all walkable".

/// <summary>terrain.md「attribute 層位置」「attribute → terrain byte」. Pure functions, no I/O.</summary>
public static class TerrainRules {
    /// <summary>Attribute of a cell inside the bounds that has no block file: blocked, no zone.</summary>
    public const ushort MissingAttribute=1;
    public const int AttributeCount=8192;
    public static readonly MapBounds Terrain0Window=new(32576,32896,32639,33023);
    public static readonly MapBounds CacheWindow=new(32256,32768,32767,33279);
    public const int CacheMapId=0;
    /// <summary>L3: maps whose zone bits (4–5) cannot match the r29 baseline (terrain.md「不符的地圖」).</summary>
    public static readonly IReadOnlyList<int> ZoneLimitMaps=[0,4,42,70,244,440,2101,2151];

    /// <summary>
    /// The attribute layer of one block: S32 = ground 8192 × u32, u16 n, cover n × 6, then 8192 × u16;
    /// SEG = ground 8192 × u16, u16 n, cover n × 4, then 8192 × u8. Throws InvalidDataException when the file ends first.
    /// </summary>
    public static ushort[] Attributes(ReadOnlySpan<byte> data,bool seg){
        int ground=AttributeCount*(seg?2:4);
        if(data.Length<ground+2)throw new InvalidDataException($"區塊在 ground 層截斷（{data.Length} bytes）");
        int cover=BinaryPrimitives.ReadUInt16LittleEndian(data[ground..]);
        long start=ground+2L+(long)cover*(seg?4:6),end=start+AttributeCount*(seg?1:2);
        if(end>data.Length)throw new InvalidDataException($"區塊在 attribute 層截斷（需要 {end} bytes，只有 {data.Length}）");
        var attr=new ushort[AttributeCount];int p=(int)start;
        for(int i=0;i<AttributeCount;i++)attr[i]=seg?data[p+i]:BinaryPrimitives.ReadUInt16LittleEndian(data[(p+2*i)..]);
        return attr;
    }

    /// <summary>Zone bits from L(x,y) of a cell that has a block: (L &amp; 0xF0) == 0x40 or L &amp; 4 → 16; else L &amp; 8 → 32; else 0.</summary>
    public static byte Zone(int l)=>(l&0xF0)==0x40||(l&0x04)!=0?(byte)16:(l&0x08)!=0?(byte)32:(byte)0;

    /// <summary>Passage bits 0–3 from A = R(x−1,y), B = R(x,y), C = L(x,y), D = L(x,y+1) (only bit 0 of each is used).</summary>
    public static byte Passage(int a,int b,int c,int d){
        bool A=(a&1)!=0,B=(b&1)!=0,C=(c&1)!=0,D=(d&1)!=0;
        return (byte)((!(A&&B)?1:0)|(!(C&&D)?2:0)|(!A&&!B?4:0)|(!C&&!D?8:0));
    }

    /// <summary>
    /// Terrain of one map: height × width bytes, row-major, index (y − minY) × width + (x − minX). blocks is keyed by (bx, by)
    /// of the block name; a cell without a block uses attribute 1 for L and R and has zone 0; the outer ring is 0.
    /// </summary>
    public static byte[] Build(MapBounds bounds,IReadOnlyDictionary<(int Bx,int By),ushort[]> blocks){
        int w=bounds.Width,h=bounds.Height;
        if(w<=0||h<=0||(long)w*h>int.MaxValue)throw new ArgumentException("bounds 不合理",nameof(bounds));
        int bx0=Floor64(bounds.MinX),by0=Floor64(bounds.MinY),gw=Floor64(bounds.MaxX)-bx0+1,gh=Floor64(bounds.MaxY)-by0+1;
        var grid=new ushort[]?[gw*gh];
        foreach(var ((bx,by),attr) in blocks){
            if(attr.Length!=AttributeCount)throw new ArgumentException($"區塊 {bx:x4}{by:x4} 的 attribute 數不是 {AttributeCount}",nameof(blocks));
            int gx=bx-0x7E00-bx0,gy=by-0x7E00-by0;
            if(gx<0||gy<0||gx>=gw||gy>=gh)throw new ArgumentException($"區塊 {bx:x4}{by:x4} 在 bounds 之外",nameof(blocks));
            grid[gy*gw+gx]=attr;
        }
        ushort[]? Block(int x,int y){
            if(!bounds.Contains(x,y))return null;
            return grid[(Floor64(y)-by0)*gw+Floor64(x)-bx0];
        }
        int L(int x,int y)=>Block(x,y) is {} a?a[Mod64(y)*128+2*Mod64(x)]:MissingAttribute;
        int R(int x,int y)=>Block(x,y) is {} a?a[Mod64(y)*128+2*Mod64(x)+1]:MissingAttribute;
        var terrain=new byte[w*h];
        for(int y=bounds.MinY+1;y<bounds.MaxY;y++)
            for(int x=bounds.MinX+1;x<bounds.MaxX;x++){
                int l=L(x,y);
                byte v=Passage(R(x-1,y),R(x,y),l,L(x,y+1));
                if(Block(x,y)!=null)v|=Zone(l);
                terrain[(y-bounds.MinY)*w+x-bounds.MinX]=v;
            }
        return terrain;
    }

    /// <summary>A rectangle of a map's terrain; cells outside the map's bounds are 0.</summary>
    public static byte[] Window(byte[] terrain,MapBounds bounds,MapBounds window){
        if(terrain.Length!=(long)bounds.Width*bounds.Height)throw new ArgumentException("terrain 長度與 bounds 不符",nameof(terrain));
        var result=new byte[window.Width*window.Height];
        for(int y=window.MinY;y<=window.MaxY;y++)
            for(int x=window.MinX;x<=window.MaxX;x++)
                if(bounds.Contains(x,y))result[(y-window.MinY)*window.Width+x-window.MinX]=terrain[(y-bounds.MinY)*bounds.Width+x-bounds.MinX];
        return result;
    }

    /// <summary>cache/original-map0.txt: each row the decimal values joined by ',', no trailing comma, every row (the last too) ends with CRLF.</summary>
    public static byte[] Text(byte[] cells,int width){
        if(width<=0||cells.Length%width!=0)throw new ArgumentException("寬度不合理",nameof(width));
        var s=new StringBuilder(cells.Length*3);
        for(int i=0;i<cells.Length;i+=width){
            for(int j=0;j<width;j++){if(j>0)s.Append(',');s.Append(cells[i+j].ToString(CultureInfo.InvariantCulture));}
            s.Append("\r\n");
        }
        return Encoding.ASCII.GetBytes(s.ToString());
    }

    // Block index of a world coordinate (bounds are never negative for a real map, but stay exact for any value).
    static int Floor64(int v)=>v>>6;
    static int Mod64(int v)=>v&63;
}

/// <summary>One block the product reads (plan.Blocks) after the full section walk.</summary>
public sealed record MapBlockRead(string Stem,string File,bool Seg,string Sha256,MapBlockScan Scan);

/// <summary>Result of reading one map directory: Terrain is null exactly when Failures is not empty (the map is unsupported, D3).</summary>
public sealed record MapTerrainRead(MapDirectoryPlan Plan,IReadOnlyList<MapBlockRead> Blocks,IReadOnlyList<string> Failures,byte[]? Terrain){
    public string? FailureReason=>Failures.Count==0?null:$"{Failures.Count} 個區塊無法解析；第一個：{Failures[0]}";
}

/// <summary>The read shared by the `maps` and `terrain` recipes (asset-recipe.md §6: one scan for both).</summary>
public static class MapTerrain {
    /// <summary>
    /// Reads every block of the plan (full section walk, SEG anchor check) and every .seg-only block (attribute layer only:
    /// terrain.md「選檔」 uses them), then derives the terrain when nothing failed. A SEG in plan.Blocks other than the
    /// pinned one stops the recipe (ContainerMismatch), as before T458.
    /// </summary>
    public static MapTerrainRead Read(AssetRecipeItemContext context,MapDirectoryPlan plan){
        var blocks=new List<MapBlockRead>();var failures=new List<string>();var attrs=new Dictionary<(int,int),ushort[]>();
        foreach(var (stem,file) in plan.Blocks){
            context.Cancel.ThrowIfCancellationRequested();
            bool seg=file.EndsWith(".seg",StringComparison.OrdinalIgnoreCase);
            byte[] data=context.ReadMapFile(plan.Directory,file);string sha=AssetPrepPaths.Sha256(data);
            if(seg&&sha!=MapScan.SegAnchorSha256)
                throw new AssetPrepException(AssetPrepFailure.ContainerMismatch,$"map/{plan.Directory} 的 {file} 與支援版本不符（SHA-256 {sha}）",$"{AssetPrepInput.MapContainer}/{plan.Directory}");
            try{
                var scan=MapScan.Parse(data,seg);
                attrs[Key(stem)]=TerrainRules.Attributes(data,seg);
                blocks.Add(new(stem,file,seg,sha,scan));
            }catch(InvalidDataException ex){failures.Add($"{file}: {ex.Message}");}
        }
        foreach(var (stem,file) in plan.SegOnly){
            context.Cancel.ThrowIfCancellationRequested();
            try{attrs[Key(stem)]=TerrainRules.Attributes(context.ReadMapFile(plan.Directory,file),true);}
            catch(InvalidDataException ex){failures.Add($"{file}: {ex.Message}");}
        }
        byte[]? terrain=failures.Count==0?TerrainRules.Build(plan.Bounds,attrs):null;
        return new(plan,blocks,failures,terrain);
    }

    /// <summary>The plans of every map directory, in the order of context.MapDirectories().</summary>
    public static IEnumerable<MapDirectoryPlan> Plans(AssetRecipeItemContext context){
        foreach(string directory in context.MapDirectories()){
            context.Cancel.ThrowIfCancellationRequested();
            if(!MapScan.IsMapDirectory(directory,out _))continue;
            MapDirectoryPlan? plan;
            try{plan=MapScan.Plan(directory,context.MapFiles(directory));}
            catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,ex.Message,AssetPrepInput.MapContainer,ex);}
            if(plan!=null)yield return plan;
        }
    }

    /// <summary>Map 0 for terrain0.bin and cache/original-map0.txt; refused (never filled in) when it is missing or unsupported.</summary>
    public static MapTerrainRead CacheMap(AssetRecipeItemContext context){
        string directory=TerrainRules.CacheMapId.ToString(CultureInfo.InvariantCulture);
        MapDirectoryPlan? plan=null;
        if(context.MapDirectories().Contains(directory,StringComparer.Ordinal)){
            try{plan=MapScan.Plan(directory,context.MapFiles(directory));}
            catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,ex.Message,AssetPrepInput.MapContainer,ex);}
        }
        if(plan==null)throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"TW_CLIENT_ROOT 的 map/{directory} 沒有任何 .s32 區塊",AssetPrepInput.MapContainer);
        var read=Read(context,plan);
        if(read.Terrain==null)throw new AssetPrepException(AssetPrepFailure.Unavailable,$"map {directory} 不支援，無法產生 terrain：{read.FailureReason}",AssetPrepInput.MapContainer);
        return read;
    }

    public static string TerrainPath(int mapId)=>$"terrain/{mapId.ToString(CultureInfo.InvariantCulture)}.bin";

    static (int,int) Key(string stem)=>(int.Parse(stem[..4],NumberStyles.HexNumber,CultureInfo.InvariantCulture),int.Parse(stem[4..],NumberStyles.HexNumber,CultureInfo.InvariantCulture));
}

[AssetRecipe]
public sealed class TerrainRecipe:IAssetRecipe,IAssetRecipeSources {
    public const string RecipeId="terrain",MapsItem="maps",Terrain0Item="terrain0",CacheItem="original-map0";
    public const string Terrain0Path="terrain0.bin",CacheTextPath="cache/original-map0.txt";
    public static string MapTerrainPath(int mapId)=>"maps/"+MapTerrain.TerrainPath(mapId);

    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Complete;
    public AssetPrepSources Sources=>AssetPrepSources.MapFiles;

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        foreach(string item in items)if(item is not (MapsItem or Terrain0Item or CacheItem))
            throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {RecipeId}：項目只能是 {MapsItem}、{CacheItem} 或 {Terrain0Item}，不接受 \"{item}\"");
        return items.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    public void Produce(string item,AssetRecipeItemContext context){
        switch(item){
            case MapsItem:ProduceMaps(context);break;
            case Terrain0Item:{
                var map0=MapTerrain.CacheMap(context);
                context.Write(Terrain0Path,TerrainRules.Window(map0.Terrain!,map0.Plan.Bounds,TerrainRules.Terrain0Window));
                break;
            }
            case CacheItem:{
                var map0=MapTerrain.CacheMap(context);
                context.Write(CacheTextPath,CacheText(map0));
                break;
            }
            default:throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {RecipeId}：未知項目 {item}");
        }
    }

    /// <summary>cache/original-map0.txt bytes (also hashed by the `maps` recipe for cache/manifest.json).</summary>
    public static byte[] CacheText(MapTerrainRead map0)=>TerrainRules.Text(TerrainRules.Window(map0.Terrain!,map0.Plan.Bounds,TerrainRules.CacheWindow),TerrainRules.CacheWindow.Width);

    static void ProduceMaps(AssetRecipeItemContext context){
        var produced=new List<int>();var unsupported=new List<int>();
        foreach(var plan in MapTerrain.Plans(context)){
            var read=MapTerrain.Read(context,plan);
            if(read.Terrain==null){unsupported.Add(plan.MapId);continue;}
            context.Write(MapTerrainPath(plan.MapId),read.Terrain);
            produced.Add(plan.MapId);
        }
        produced.Sort();unsupported.Sort();
        context.Set("maps",produced.Count.ToString(CultureInfo.InvariantCulture));
        context.Set("unsupportedMaps",string.Join(",",unsupported));
        context.Set("zoneLimitMaps",string.Join(",",TerrainRules.ZoneLimitMaps.Where(id=>produced.BinarySearch(id)>=0)));
    }
}
