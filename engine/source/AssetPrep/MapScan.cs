using System.Buffers.Binary;
using System.Globalization;
namespace P06.AssetPrep;

// T456 (#1041): the map directory scan shared by the `maps` recipe and (T458) the `terrain` recipe
// (docs/design/asset-recipe.md §6: both scan map/ and compute bounds with the same code).
// Rules: docs/reference/asset-recipes/terrain.md「選取」「座標與 bounds」, maps.md「maps/manifest.json」, and the provisional
// selection rule D3 in asset-recipe.md §8:
//   - a map directory is a decimal directory name with at least one block file <8 hex>.s32 (extension case-insensitive);
//   - block files are <8 hex>.s32 / <8 hex>.seg only (tempseg.s32, 800F7FF3E.s32 … are never read: the product looks blocks
//     up as <bx><by>.s32 and cannot reach them);
//   - when a block has both, the .s32 is used (sub_F0B570 opens .s32 before .seg); since T1195 (#1195) every .seg-only block is a block of
//     the plan (format classic-seg), identified by its SHA-256; the map 6 anchor 7fff8000 additionally keeps its pinned SHA-256;
//   - bounds come from every selected block name (.s32 and .seg together).
// Parse is the product reader's section layout (OriginalMapStore.Load), structural only: it never decodes pixels. For a SEG, as in the
// SEG branch of sub_F0B570, the portal section and the effects word are read only while data remains.

/// <summary>A block file name: &lt;bx:x4&gt;&lt;by:x4&gt;.s32 or .seg (case-insensitive). Stem is lower-case.</summary>
public sealed record MapBlockName(string Stem,int Bx,int By,bool Seg){
    public int X0=>(Bx-0x7E00)*64;
    public int Y0=>(By-0x7E00)*64;
}

/// <summary>Structural facts of one S32/SEG block.</summary>
public sealed record MapBlockScan(bool Seg,long Size,IReadOnlySet<int> TileIds,int PortalCount,bool NoEffectTail,bool ExtendedEffects,IReadOnlyList<int> EffectIds);

/// <summary>One selected map directory. Blocks = the files the product reads (stem → original file name), .seg-only blocks included since T1195;
/// SegOnly = .seg files without an .s32 that are not blocks (always empty since T1195; kept for the terrain read).</summary>
public sealed record MapDirectoryPlan(int MapId,string Directory,IReadOnlyDictionary<string,string> Blocks,IReadOnlyDictionary<string,string> SegOnly,
    MapBounds Bounds,IReadOnlySet<string> LowerFileNames);

public static class MapScan {
    /// <summary>The SEG anchor (asset-recipe.md §1.2; also checked by SegBlocks.CheckAnchor in OriginalMapCache.cs / Portals.cs). Other SEG blocks are supported by SHA-256 since T1195.</summary>
    public const int SegAnchorMap=6;
    public const string SegAnchorStem="7fff8000",SegAnchorSha256="aad5ff598f9daeb6574fcef8fdaa1b3b2963d6c0d60d8840057ef48d39101a45";
    /// <summary>The four names a block could have; none present → verified absent (maps.md「verifiedAbsent」).</summary>
    public static readonly IReadOnlyList<string> ExistenceSuffixes=[".s32.gz",".s32",".seg.gz",".seg"];

    /// <summary>Decimal directory name without a leading zero (except "0") that fits an int.</summary>
    public static bool IsMapDirectory(string name,out int mapId){
        mapId=-1;
        if(name is not {Length:>0 and <=9}||!name.All(char.IsAsciiDigit)||(name.Length>1&&name[0]=='0'))return false;
        return int.TryParse(name,NumberStyles.None,CultureInfo.InvariantCulture,out mapId);
    }

    public static MapBlockName? BlockName(string fileName){
        if(fileName.Length!=12||fileName[8]!='.')return null;
        string ext=fileName[8..].ToLowerInvariant();
        if(ext is not (".s32" or ".seg"))return null;
        string stem=fileName[..8];
        if(!stem.All(char.IsAsciiHexDigit))return null;
        stem=stem.ToLowerInvariant();
        return new(stem,int.Parse(stem[..4],NumberStyles.HexNumber,CultureInfo.InvariantCulture),int.Parse(stem[4..],NumberStyles.HexNumber,CultureInfo.InvariantCulture),ext==".seg");
    }

    /// <summary>terrain.md「座標與 bounds」: min of x0 / y0, max of x0 / y0 plus 63.</summary>
    public static MapBounds Bounds(IEnumerable<MapBlockName> blocks){
        var list=blocks.ToList();
        if(list.Count==0)throw new ArgumentException("沒有區塊，無法推導 bounds",nameof(blocks));
        return new(list.Min(b=>b.X0),list.Min(b=>b.Y0),list.Max(b=>b.X0)+63,list.Max(b=>b.Y0)+63);
    }

    /// <summary>Null when the directory is not a map under rule D3 (not decimal, or no &lt;8 hex&gt;.s32).</summary>
    public static MapDirectoryPlan? Plan(string directory,IReadOnlyList<string> files){
        if(!IsMapDirectory(directory,out int mapId))return null;
        var s32=new SortedDictionary<string,string>(StringComparer.Ordinal);var seg=new SortedDictionary<string,string>(StringComparer.Ordinal);var names=new List<MapBlockName>();
        foreach(string file in files){
            if(BlockName(file) is not {} name)continue;
            var target=name.Seg?seg:s32;
            if(!target.TryAdd(name.Stem,file))throw new InvalidDataException($"map/{directory} 有兩個只差大小寫的區塊檔 {target[name.Stem]}、{file}");
            names.Add(name);
        }
        if(s32.Count==0)return null;
        var blocks=new SortedDictionary<string,string>(s32,StringComparer.Ordinal);var segOnly=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var (stem,file) in seg){
            if(s32.ContainsKey(stem))continue;
            blocks[stem]=file;   // T1195: .seg-only → classic-seg block (was segOnly / unsupportedBlocks except the map 6 anchor)
        }
        return new(mapId,directory,blocks,segOnly,Bounds(names),files.Select(f=>f.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>
    /// maps.md「verifiedAbsent」「unsupportedBlocks」: every stem with bx from minX/64 + 0x7E00 − 2 to maxX/64 + 0x7E00 + 2 (bx outer,
    /// both ends included) and by likewise, that is not a block of the plan: absent when none of the four names exists, otherwise unsupported.
    /// </summary>
    public static (List<string> VerifiedAbsent,List<string> UnsupportedBlocks) Coverage(MapDirectoryPlan plan){
        var absent=new List<string>();var unsupported=new List<string>();var b=plan.Bounds;
        for(int bx=b.MinX/64+0x7E00-2;bx<=b.MaxX/64+0x7E00+2;bx++)
            for(int by=b.MinY/64+0x7E00-2;by<=b.MaxY/64+0x7E00+2;by++){
                string stem=$"{bx:x4}{by:x4}";
                if(plan.Blocks.ContainsKey(stem))continue;
                if(ExistenceSuffixes.Any(s=>plan.LowerFileNames.Contains(stem+s)))unsupported.Add(stem);else absent.Add(stem);
            }
        return (absent,unsupported);
    }

    /// <summary>Section walk of one block (maps.md「共用的 S32 區段順序」; SEG as OriginalMapStore reads it). Throws InvalidDataException.</summary>
    public static MapBlockScan Parse(byte[] data,bool seg){
        int p=0;var tiles=new HashSet<int>();
        void Need(long n){if(n<0||p+n>data.Length)throw new InvalidDataException($"區塊在 {p} 截斷");}
        int U8(){Need(1);return data[p++];}
        int U16(){Need(2);int v=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(p));p+=2;return v;}
        uint U32(){Need(4);uint v=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(p));p+=4;return v;}
        int Count(string what){Need(4);int n=BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(p));p+=4;if(n is <0 or >65536)throw new InvalidDataException($"{what} 筆數 {n} 不合理");return n;}
        void Tile(uint v){tiles.Add(seg?(int)(v>>8):(int)((v>>8)&0xFFFF));}
        Need(8192L*(seg?2:4));
        for(int i=0;i<8192;i++)Tile(seg?(uint)U16():U32());
        int cover=U16();Need((long)cover*(seg?4:6));
        for(int i=0;i<cover;i++){p+=2;Tile(seg?(uint)U16():U32());}
        Need(8192L*(seg?1:2));p+=8192*(seg?1:2);
        int groups=Count("groups");
        for(int g=0;g<groups;g++){U16();int n=U16();Need((long)n*(seg?5:7));for(int i=0;i<n;i++){p+=3;Tile(seg?(uint)U16():U32());}}
        int triggers=Count("opacity triggers");Need(5L*triggers);p+=5*triggers;
        int refs=seg?U8():Count("tile refs");Need((long)refs*(seg?1:4));p+=refs*(seg?1:4);
        int portals=seg&&p==data.Length?0:U16();
        for(int i=0;i<portals;i++){int len=U8();Need(len+8);p+=len+8;}
        bool noTail=p==data.Length;bool extended=false;var effects=new List<int>();
        if(!noTail){
            int n=U16();extended=(n&0x8000)!=0;n&=0x7FFF;int size=extended?10:6;Need((long)n*size);
            for(int i=0;i<n;i++)effects.Add(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(p+i*size)));
            p+=n*size;
        }
        if(p!=data.Length)throw new InvalidDataException($"區塊結尾多出 {data.Length-p} bytes");
        return new(seg,data.Length,tiles,portals,noTail,extended,effects);
    }
}
