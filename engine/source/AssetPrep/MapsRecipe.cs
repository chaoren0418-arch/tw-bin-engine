using System.Text.Encodings.Web;
using System.Text.Json;
namespace P06.AssetPrep;

// T456 (#1041): recipe `maps` (docs/design/asset-recipe.md §3.3, §6; docs/reference/asset-recipes/maps.md).
// The raw files are NOT copied: S32/SEG stay in TW_CLIENT_ROOT/map, TIL and LAC in Tile.pak, and the product reads them
// through MapRawSource. This recipe only writes the two manifests that carry their SHA-256:
//   item "maps"  → maps/manifest.json   (p24-map-provider-v1: blocks[], maps[] with bounds / verifiedAbsent / unsupportedBlocks,
//                                        tiles[], missingTileIds, unsupportedMaps)
//   item "cache" → cache/manifest.json  (p24-original-cache-v1: the 20 map 0 blocks, their TIL, verifiedMissing)
// Output paths are relative to the assets tree (§4.5); assembling recipes into one tree is T467. With the single-recipe
// publish of T451 they land below assets/maps/.
// Not written here (T458): maps[].terrain and cache terrain (the terrain recipe adds them), so Coverage stays Partial and the
// product cannot load a prepared map until T458 is merged — expected by §6.
// Left out on purpose (§3.3): maps[].name (L4), copyTerrainMapId, clientMapDirectoryPresent, directTerrainFilePresent,
// terrain.sourcePath/sourceSha256, tiles[].record, originalSource, unsupportedBlocks[].sources, provenance, scope, nativeVa,
// nativeImageBase; cache: record, pixelModes, sourceArchives, scope. No absolute path, time or machine name is written.
[AssetRecipe]
public sealed class MapsRecipe:IAssetRecipe,IAssetRecipeSources {
    public const string RecipeId="maps",MapsItem="maps",CacheItem="cache";
    public const string MapsManifestPath="maps/manifest.json",CacheManifestPath="cache/manifest.json";
    public const string MapsSchema="p24-map-provider-v1",CacheSchema="p24-original-cache-v1";
    /// <summary>Added to every TIL list: tile 140 indices 187/188 draw verified-absent blocks and 0xFFFFFFFF ground cells.</summary>
    public const int MissingGroundTile=140;
    /// <summary>cache/manifest.json: map 0 bx 0x7FFB–0x7FFE (outer) × by 0x8001–0x8005, and the four names that must be absent.</summary>
    public static readonly IReadOnlyList<string> CacheBlocks=[..Enumerable.Range(0x7FFB,4).SelectMany(bx=>Enumerable.Range(0x8001,5).Select(by=>$"{bx:x4}{by:x4}"))];
    public static readonly IReadOnlyList<string> CacheVerifiedMissing=["7e007e00","7e017e00","7e007e01","7e017e01"];

    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Partial;
    public AssetPrepSources Sources=>AssetPrepSources.Tile|AssetPrepSources.MapFiles;

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        foreach(string item in items)if(item is not (MapsItem or CacheItem))throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {RecipeId}：項目只能是 {CacheItem} 或 {MapsItem}，不接受 \"{item}\"");
        return items.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    public void Produce(string item,AssetRecipeItemContext context){
        if(item==MapsItem)ProduceMaps(context);else if(item==CacheItem)ProduceCache(context);
        else throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {RecipeId}：未知項目 {item}");
    }

    internal static JsonWriterOptions JsonOptions=>new(){Indented=true,IndentSize=2,NewLine="\r\n",Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};

    sealed record BlockRow(int MapId,string Name,string Path,string Sha256,bool NoEffectTail,bool Seg);
    sealed record MapRow(int MapId,MapBounds Bounds,List<string> VerifiedAbsent,List<string> UnsupportedBlocks);

    static void ProduceMaps(AssetRecipeItemContext context){
        var blocks=new List<BlockRow>();var maps=new List<MapRow>();var unsupportedMaps=new List<(int MapId,string Reason)>();var tileIds=new SortedSet<int>{MissingGroundTile};
        foreach(string directory in context.MapDirectories()){
            context.Cancel.ThrowIfCancellationRequested();
            if(!MapScan.IsMapDirectory(directory,out _))continue;
            MapDirectoryPlan? plan;
            try{plan=MapScan.Plan(directory,context.MapFiles(directory));}
            catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,ex.Message,AssetPrepInput.MapContainer,ex);}
            if(plan==null)continue;
            var rows=new List<BlockRow>();var ids=new HashSet<int>();var failures=new List<string>();
            foreach(var (stem,file) in plan.Blocks){
                context.Cancel.ThrowIfCancellationRequested();
                bool seg=file.EndsWith(".seg",StringComparison.OrdinalIgnoreCase);
                byte[] data=context.ReadMapFile(directory,file);string sha=AssetPrepPaths.Sha256(data);
                if(seg&&sha!=MapScan.SegAnchorSha256)
                    throw new AssetPrepException(AssetPrepFailure.ContainerMismatch,$"map/{directory} 的 {file} 與支援版本不符（SHA-256 {sha}）",$"{AssetPrepInput.MapContainer}/{directory}");
                MapBlockScan scan;
                try{scan=MapScan.Parse(data,seg);}
                catch(InvalidDataException ex){failures.Add($"{file}: {ex.Message}");continue;}
                rows.Add(new(plan.MapId,stem+(seg?".seg":".s32"),$"{AssetPrepInput.MapContainer}/{directory}/{file}",sha,scan.NoEffectTail,seg));
                ids.UnionWith(scan.TileIds);
            }
            // D3: one block the product reader would refuse makes the whole map explicitly unsupported (never "all walkable").
            if(failures.Count>0){unsupportedMaps.Add((plan.MapId,$"{failures.Count} 個區塊無法解析；第一個：{failures[0]}"));continue;}
            blocks.AddRange(rows);tileIds.UnionWith(ids);
            var (absent,unsupported)=MapScan.Coverage(plan);
            maps.Add(new(plan.MapId,plan.Bounds,absent,unsupported));
        }
        tileIds.Remove(0xFFFF);
        var tiles=new List<(int Tile,string Sha256)>();var missing=new List<int>();
        foreach(int id in tileIds){
            context.Cancel.ThrowIfCancellationRequested();
            if(context.TryReadTile(id+".til") is {} data)tiles.Add((id,AssetPrepPaths.Sha256(data)));else missing.Add(id);
        }
        context.Set("blocks",blocks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        context.Set("maps",maps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        context.Set("tiles",tiles.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        context.Set("missingTileIds",string.Join(",",missing));
        context.Set("unsupportedMaps",string.Join(",",unsupportedMaps.Select(m=>m.MapId)));
        context.Write(MapsManifestPath,stream=>{
            using(var w=new Utf8JsonWriter(stream,JsonOptions)){
                w.WriteStartObject();
                w.WriteString("schema",MapsSchema);
                w.WriteStartArray("blocks");
                foreach(var b in blocks){
                    w.WriteStartObject();w.WriteNumber("mapId",b.MapId);w.WriteString("name",b.Name);w.WriteString("path",b.Path);w.WriteString("sha256",b.Sha256);w.WriteBoolean("noEffectTail",b.NoEffectTail);
                    if(b.Seg)w.WriteString("format","classic-seg");
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("maps");
                foreach(var m in maps.OrderBy(m=>m.MapId)){
                    w.WriteStartObject();w.WriteNumber("mapId",m.MapId);
                    w.WriteNumber("minX",m.Bounds.MinX);w.WriteNumber("maxX",m.Bounds.MaxX);w.WriteNumber("minY",m.Bounds.MinY);w.WriteNumber("maxY",m.Bounds.MaxY);
                    w.WriteNumber("width",m.Bounds.Width);w.WriteNumber("height",m.Bounds.Height);
                    w.WriteStartArray("verifiedAbsent");foreach(string s in m.VerifiedAbsent)w.WriteStringValue(s);w.WriteEndArray();
                    w.WriteStartArray("unsupportedBlocks");foreach(string s in m.UnsupportedBlocks){w.WriteStartObject();w.WriteString("name",s);w.WriteEndObject();}w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteStartArray("tiles");
                foreach(var (tile,sha) in tiles){w.WriteStartObject();w.WriteNumber("tile",tile);w.WriteString("sha256",sha);w.WriteString("path",$"tiles/{tile}.til");w.WriteEndObject();}
                w.WriteEndArray();
                w.WriteStartArray("missingTileIds");foreach(int id in missing)w.WriteNumberValue(id);w.WriteEndArray();
                w.WriteStartArray("unsupportedMaps");
                foreach(var (mapId,reason) in unsupportedMaps.OrderBy(m=>m.MapId)){w.WriteStartObject();w.WriteNumber("mapId",mapId);w.WriteString("reason",reason);w.WriteEndObject();}
                w.WriteEndArray();
                w.WriteEndObject();
            }
            stream.Write("\r\n"u8);
        });
    }

    static void ProduceCache(AssetRecipeItemContext context){
        const string directory="0";
        var files=context.MapFiles(directory);
        var lower=files.ToDictionary(f=>f.ToLowerInvariant(),f=>f,StringComparer.Ordinal);
        var rows=new List<(string Name,string Sha256,long Size,int Effects,int Portals)>();var ids=new SortedSet<int>{MissingGroundTile};
        foreach(string stem in CacheBlocks){
            context.Cancel.ThrowIfCancellationRequested();
            string name=stem+".s32";
            if(!lower.TryGetValue(name,out string? file))throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"TW_CLIENT_ROOT 缺少 map/0/{name}（cache/manifest.json 需要的 20 個區塊之一）",AssetPrepInput.MapContainer);
            byte[] data=context.ReadMapFile(directory,file);
            MapBlockScan scan;
            try{scan=MapScan.Parse(data,false);}
            catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"map/0/{file} 無法解析：{ex.Message}",AssetPrepInput.MapContainer,ex);}
            rows.Add((name,AssetPrepPaths.Sha256(data),data.Length,scan.EffectIds.Count,scan.PortalCount));
            ids.UnionWith(scan.TileIds);
        }
        ids.Remove(0xFFFF);
        var tiles=new List<(int Tile,string Sha256,long Size)>();
        foreach(int id in ids){
            byte[] data=context.TryReadTile(id+".til")??throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"容器 {MapRawSource.TileContainer} 沒有 {id}.til（cache 區塊需要）",MapRawSource.TileContainer);
            tiles.Add((id,AssetPrepPaths.Sha256(data),data.Length));
        }
        var missing=CacheVerifiedMissing.Where(stem=>!MapScan.ExistenceSuffixes.Any(s=>lower.ContainsKey(stem+s))).ToList();
        context.Set("blocks",rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        context.Set("tiles",tiles.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        context.Write(CacheManifestPath,stream=>{
            using var w=new Utf8JsonWriter(stream,JsonOptions);
            w.WriteStartObject();
            w.WriteString("schema",CacheSchema);
            w.WriteStartArray("maps");
            foreach(var r in rows){w.WriteStartObject();w.WriteString("name",r.Name);w.WriteString("sha256",r.Sha256);w.WriteNumber("size",r.Size);w.WriteNumber("effectCount",r.Effects);w.WriteNumber("portalCount",r.Portals);w.WriteEndObject();}
            w.WriteEndArray();
            w.WriteStartArray("tiles");
            foreach(var t in tiles){w.WriteStartObject();w.WriteNumber("tile",t.Tile);w.WriteString("sha256",t.Sha256);w.WriteNumber("size",t.Size);w.WriteEndObject();}
            w.WriteEndArray();
            w.WriteStartArray("verifiedMissing");foreach(string s in missing)w.WriteStringValue(s);w.WriteEndArray();
            w.WriteEndObject();
        });
    }
}
