using System.Buffers.Binary;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
namespace P06.AssetPrep;

// T457 (#1042): recipe `scene` (docs/design/asset-recipe.md §3.5, §6; docs/reference/asset-recipes/maps.md「scene 的三個執行檔」
// 「scene/opacity.json」). Input: TW_CLIENT_ROOT/map/0/7ffd8002.s32 (north, number 0), 7ffd8003.s32 (south, number 1) and the
// TIL entries of Tile.pak they use. Output (assets-tree relative, §4.5):
//   scene/manifest.json, scene/draw-commands.json, scene/tile-pixels.json  (p22-dual-map-v1; compact, no newline at all → B)
//   scene/opacity.json                                                      (p23-original-opacity-v1; indent 2, CRLF, no final newline → S)
// The 9 preview / P05 files (map.json, tiles.json, region.rgb555, 6 PNG incl. atlas.png) are not produced: no product reader opens
// them (§2 row 13, §9 T457). opacity.json drops maps[].path (an absolute path in the baseline; the product never reads maps[]).
// Nothing here carries pixels or table content: only the S32 / TIL layouts and the fixed descriptive strings of the format.
[AssetRecipe]
public sealed class SceneRecipe:IAssetRecipe,IAssetRecipeSources {
    public const string RecipeId="scene",SceneItem="scene";
    public const string ManifestPath="scene/manifest.json",DrawCommandsPath="scene/draw-commands.json",TilePixelsPath="scene/tile-pixels.json",OpacityPath="scene/opacity.json";
    public const string SceneSchema="p22-dual-map-v1",OpacitySchema="p23-original-opacity-v1";
    public const string MapDirectory="0",North="7ffd8002",South="7ffd8003";
    /// <summary>Block number 0 = north, 1 = south (draw-commands: y + 64 × number, north serials + 100000).</summary>
    public static readonly IReadOnlyList<string> Blocks=[North,South];

    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Complete;
    public AssetPrepSources Sources=>AssetPrepSources.Tile|AssetPrepSources.MapFiles;

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        foreach(string item in items)if(item!=SceneItem)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {RecipeId}：項目只能是 {SceneItem}，不接受 \"{item}\"");
        return items.Distinct(StringComparer.Ordinal).ToList();
    }

    public void Produce(string item,AssetRecipeItemContext context){
        if(item!=SceneItem)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {RecipeId}：未知項目 {item}");
        var files=context.MapFiles(MapDirectory);
        var lower=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(string f in files)lower.TryAdd(f.ToLowerInvariant(),f);
        var parsed=new Dictionary<string,(SceneBlock Block,string Sha256)>(StringComparer.Ordinal);
        foreach(string stem in Blocks){
            context.Cancel.ThrowIfCancellationRequested();
            if(!lower.TryGetValue(stem+".s32",out string? file))throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"TW_CLIENT_ROOT 缺少 map/{MapDirectory}/{stem}.s32（scene 需要的兩個區塊之一）",AssetPrepInput.MapContainer);
            byte[] data=context.ReadMapFile(MapDirectory,file);
            SceneBlock block;
            try{block=SceneBlock.Parse(data);}
            catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"map/{MapDirectory}/{file} 無法解析：{ex.Message}",AssetPrepInput.MapContainer,ex);}
            parsed[stem]=(block,AssetPrepPaths.Sha256(data));
        }

        // draw-commands: each block's ground, cover, objects in source order, then a stable sort (maps.md step 3).
        var commands=new List<SceneCommand>();
        for(int number=0;number<Blocks.Count;number++){
            var b=parsed[Blocks[number]].Block;
            foreach(var (layer,rows) in new[]{("ground",b.Ground),("cover",b.Cover),("objects",b.Objects)})
                for(int n=0;n<rows.Count;n++)commands.Add(new(layer,n,n+(1-number)*100000,Blocks[number],rows[n],rows[n].Y+64*number));
        }
        commands=commands.OrderBy(c=>c.Py).ThenBy(c=>c.Px).ThenBy(c=>c.Layer=="ground"?0:1).ThenBy(c=>c.Cell.Z).ThenBy(c=>-c.Serial).ToList();

        // tile-pixels: every (tile, index) a command uses, tile ascending, index ascending.
        var used=new SortedDictionary<int,SortedSet<int>>();
        foreach(var c in commands){if(!used.TryGetValue(c.Cell.Tile,out var set))used[c.Cell.Tile]=set=[];set.Add(c.Cell.Index);}
        var tilePixels=new List<(int Tile,int Index,int Type,List<(int X,int Y,int C)> Pixels)>();
        foreach(var (tile,indexes) in used){
            context.Cancel.ThrowIfCancellationRequested();
            string name=string.Create(CultureInfo.InvariantCulture,$"{tile}.til");
            byte[] til=context.TryReadTile(name)??throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"容器 {MapRawSource.TileContainer} 沒有 {name}（scene 區塊需要）",MapRawSource.TileContainer);
            foreach(int index in indexes){
                try{var (type,pixels)=TilBlock.Decode(til,index);tilePixels.Add((tile,index,type,pixels));}
                catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"{MapRawSource.TileContainer} 的 {name} 第 {index} 塊無法解碼：{ex.Message}",MapRawSource.TileContainer,ex);}
            }
        }

        int maxPx=commands.Max(c=>c.Px),maxPy=commands.Max(c=>c.Py);
        context.Set("drawCount",commands.Count.ToString(CultureInfo.InvariantCulture));
        context.Set("tilePixelEntries",tilePixels.Count.ToString(CultureInfo.InvariantCulture));
        context.Set("tiles",used.Count.ToString(CultureInfo.InvariantCulture));

        context.Write(ManifestPath,Compact(w=>{
            w.WriteStartObject();
            w.WriteString("schema",SceneSchema);w.WriteNumber("map_id",0);
            w.WriteString("region","complete original north and south blocks; other map0 blocks not loaded");
            w.WriteStartArray("blocks");foreach(string s in Blocks)w.WriteStringValue(s);w.WriteEndArray();
            Ints(w,"world_origin",32576,32896);Ints(w,"world_inclusive_bounds",32576,32896,32639,33023);
            Ints(w,"canvas_size",maxPx+24,maxPy+24+756);Ints(w,"pixel_origin",0,756);
            w.WriteNumber("draw_count",commands.Count);w.WriteNumber("terrain_stride",64);w.WriteNumber("terrain_rows",128);
            w.WriteString("ordering","bin-cell y,x; ground then z ascending; equal z reverse original input; cold-load south inserted before north (north serial offset100000)");
            w.WriteString("opacity_state","original untriggered; metadata retained, dynamic triggers not implemented");
            w.WriteString("portals","original metadata retained; no invented transitions");
            w.WriteString("edge_policy","retain every source-owned pixel including external anchors; remaining neighboring blocks not loaded");
            w.WriteEndObject();
        }));
        context.Write(DrawCommandsPath,stream=>{
            using var w=new Utf8JsonWriter(stream,CompactOptions);
            w.WriteStartArray();
            foreach(var c in commands){
                w.WriteStartObject();
                w.WriteString("layer",c.Layer);w.WriteNumber("source_index",c.SourceIndex);w.WriteNumber("serial",c.Serial);w.WriteString("block",c.Block);w.WriteNumber("original_source_index",c.SourceIndex);
                Cell(w,c.Cell,c.Y);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
        context.Write(TilePixelsPath,stream=>{
            using var w=new Utf8JsonWriter(stream,CompactOptions);
            w.WriteStartArray();
            foreach(var t in tilePixels){
                w.WriteStartObject();
                w.WriteNumber("tile",t.Tile);w.WriteNumber("index",t.Index);w.WriteNumber("type",t.Type);
                w.WriteStartArray("pixels");
                foreach(var p in t.Pixels){w.WriteStartArray();w.WriteNumberValue(p.X);w.WriteNumberValue(p.Y);w.WriteNumberValue(p.C);w.WriteEndArray();}
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });

        // opacity.json: south (ybase 64, slot 2) first, then north (ybase 0, slot 0).
        var order=new[]{(Block:South,YBase:64,Slot:2),(Block:North,YBase:0,Slot:0)};
        int groups=0,triggers=0,admitted=0;
        context.Write(OpacityPath,stream=>{
            using var w=new Utf8JsonWriter(stream,new JsonWriterOptions{Indented=true,IndentSize=2,NewLine="\r\n",Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
            w.WriteStartObject();
            w.WriteString("schema",OpacitySchema);
            w.WriteStartArray("maps");
            foreach(var (stem,ybase,slot) in order){
                var (b,sha)=parsed[stem];
                w.WriteStartObject();
                w.WriteString("block",stem);w.WriteString("sha256",sha);w.WriteNumber("slot",slot);w.WriteNumber("ybase",ybase);
                w.WriteNumber("groups",b.Groups.Count);w.WriteNumber("triggers",b.Triggers.Count);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("groups");
            foreach(var (stem,ybase,_) in order){
                var b=parsed[stem].Block;
                foreach(var g in b.Groups){
                    groups++;
                    w.WriteStartObject();
                    w.WriteString("key",$"{stem}:{g.Ordinal.ToString(CultureInfo.InvariantCulture)}");w.WriteString("block",stem);
                    w.WriteNumber("ordinal",g.Ordinal);w.WriteNumber("id",g.Id);w.WriteNumber("offset",g.Offset);
                    w.WriteStartArray("objects");
                    foreach(var o in b.Objects.Where(o=>o.GroupOrdinal==g.Ordinal)){
                        w.WriteStartObject();Cell(w,o,o.Y);w.WriteNumber("globalY",o.Y+ybase);w.WriteEndObject();
                    }
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
            }
            w.WriteEndArray();
            w.WriteStartArray("triggers");
            foreach(var (stem,ybase,slot) in order){
                foreach(var t in parsed[stem].Block.Triggers){
                    triggers++;
                    bool admit=t.X<256&&t.Y<(slot==0?128:64);if(admit)admitted++;
                    w.WriteStartObject();
                    w.WriteNumber("x",t.X);w.WriteNumber("y",t.Y);w.WriteNumber("group_ordinal",t.GroupOrdinal);w.WriteNumber("type",t.Type);w.WriteNumber("offset",t.Offset);
                    w.WriteString("block",stem);w.WriteString("key",$"{stem}:{t.GroupOrdinal.ToString(CultureInfo.InvariantCulture)}");
                    w.WriteNumber("globalX",t.X);w.WriteNumber("globalY",t.Y+ybase);w.WriteNumber("bits",1<<t.Type);w.WriteNumber("coldSlot",slot);
                    w.WriteBoolean("admittedByColdSlot",admit);
                    w.WriteEndObject();
                }
            }
            w.WriteEndArray();
            w.WriteString("ordering","south then north insertion; source order within each block; multiplicity preserved");
            w.WriteString("scope","Two resident blocks. Every metadata row preserved. Original slot admission applied; drawable outside pixels retained. No full cache/streaming claim.");
            w.WriteEndObject();
        });
        context.Set("opacityGroups",groups.ToString(CultureInfo.InvariantCulture));
        context.Set("opacityTriggers",triggers.ToString(CultureInfo.InvariantCulture));
        context.Set("opacityAdmitted",admitted.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>json.dumps(separators=(',', ':'), ensure_ascii=False): no whitespace, no final newline.</summary>
    internal static JsonWriterOptions CompactOptions=>new(){Indented=false,Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    static byte[] Compact(Action<Utf8JsonWriter> body){
        using var stream=new MemoryStream();
        using(var w=new Utf8JsonWriter(stream,CompactOptions))body(w);
        return stream.ToArray();
    }
    static void Ints(Utf8JsonWriter w,string name,params int[] values){w.WriteStartArray(name);foreach(int v in values)w.WriteNumberValue(v);w.WriteEndArray();}
    /// <summary>The row's own fields in source order (ground / cover: x, y, index, tile, uk, offset; objects: x, y, z, index, tile, uk, group, group_ordinal, offset).</summary>
    static void Cell(Utf8JsonWriter w,SceneCell c,int y){
        w.WriteNumber("x",c.X);w.WriteNumber("y",y);
        if(c.Layer=="objects")w.WriteNumber("z",c.Z);
        w.WriteNumber("index",c.Index);w.WriteNumber("tile",c.Tile);w.WriteNumber("uk",c.Uk);
        if(c.Layer=="objects"){w.WriteNumber("group",c.Group);w.WriteNumber("group_ordinal",c.GroupOrdinal);}
        w.WriteNumber("offset",c.Offset);
    }

    sealed record SceneCommand(string Layer,int SourceIndex,int Serial,string Block,SceneCell Cell,int Y){
        public int Px=>24*(Cell.X-Cell.X/2+Y);
        public int Py=>12*(Y-Cell.X/2);
    }
}

/// <summary>One ground / cover / objects row of an S32 block. Z, Group and GroupOrdinal are only meaningful for objects. Offset = File Offset of the row.</summary>
public sealed record SceneCell(string Layer,int X,int Y,int Z,int Index,int Tile,int Uk,int Group,int GroupOrdinal,int Offset);
public sealed record SceneGroup(int Id,int Ordinal,int Count,int Offset);
public sealed record SceneTrigger(int X,int Y,int GroupOrdinal,int Type,int Offset);

/// <summary>Full section walk of one .s32 (maps.md「共用的 S32 區段順序」) keeping every row the scene files need. Throws InvalidDataException.</summary>
public sealed class SceneBlock {
    public List<SceneCell> Ground{get;}=[];
    public List<SceneCell> Cover{get;}=[];
    public List<SceneCell> Objects{get;}=[];
    public List<SceneGroup> Groups{get;}=[];
    public List<SceneTrigger> Triggers{get;}=[];

    public static SceneBlock Parse(byte[] b){
        var s=new SceneBlock();int p=0;
        void Need(long n){if(n<0||p+n>b.Length)throw new InvalidDataException($"truncated at {p}");}
        int I32(){Need(4);int v=BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));p+=4;return v;}
        int U16At(int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(at));
        if(b.Length<32768+2)throw new InvalidDataException("short");
        for(int i=0;i<8192;i++){
            uint v=BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4*i));
            s.Ground.Add(new("ground",i%128,i/128,0,(int)(v&0xFF),(int)((v>>8)&0xFFFF),(int)(v>>24),-1,-1,4*i));
        }
        p=32768;
        Need(2);int n=U16At(p);p+=2;Need(6L*n);
        for(int i=0;i<n;i++){s.Cover.Add(new("cover",b[p],b[p+1],0,b[p+2],U16At(p+3),b[p+5],-1,-1,p));p+=6;}
        Need(16384);p+=16384;
        int groups=I32();if(groups is <0 or >65536)throw new InvalidDataException("groups");
        for(int o=0;o<groups;o++){
            Need(4);int id=U16At(p),count=U16At(p+2),at=p;p+=4;Need(7L*count);
            s.Groups.Add(new(id,o,count,at));
            for(int i=0;i<count;i++){
                s.Objects.Add(new("objects",b[p],b[p+1],b[p+2],b[p+3],BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(p+4)),b[p+6],id,o,p));
                p+=7;
            }
        }
        int triggers=I32();if(triggers is <0 or >65536)throw new InvalidDataException("opacity");
        Need(5L*triggers);
        for(int i=0;i<triggers;i++){s.Triggers.Add(new(b[p],b[p+1],U16At(p+2),b[p+4],p));p+=5;}
        int refs=I32();if(refs is <0 or >65536)throw new InvalidDataException("refs");
        Need(4L*refs);p+=4*refs;
        Need(2);int portals=U16At(p);p+=2;
        for(int i=0;i<portals;i++){Need(1);int len=b[p];Need(1+len+8);p+=1+len+8;}
        if(p==b.Length)throw new InvalidDataException("no effect tail");
        Need(2);int tail=U16At(p);p+=2;int size=(tail&0x8000)!=0?10:6;tail&=0x7FFF;Need((long)size*tail);p+=size*tail;
        if(p!=b.Length)throw new InvalidDataException($"trailing {b.Length-p}");
        return s;
    }
}

/// <summary>One block of a .til (maps.md「tile-pixels.json」). Pixels are [x, y, rgb555] in the stored order. Throws InvalidDataException.</summary>
public static class TilBlock {
    public static (int Type,List<(int X,int Y,int C)> Pixels) Decode(byte[] til,int index){
        if(til.Length<4)throw new InvalidDataException("TIL 太短");
        int n=BinaryPrimitives.ReadInt32LittleEndian(til);
        if(n<=0||n>65536||4L+4L*(n+1)>til.Length)throw new InvalidDataException($"TIL 塊數 {n} 不合理");
        if(index<0||index>=n)throw new InvalidDataException($"TIL 只有 {n} 塊");
        int Offset(int i)=>BinaryPrimitives.ReadInt32LittleEndian(til.AsSpan(4+4*i));
        int start=Offset(index),end=int.MaxValue,dataStart=4+4*(n+1);
        // The block ends at the next larger offset of the table (offsets can repeat).
        for(int i=0;i<=n;i++){int o=Offset(i);if(o>start&&o<end)end=o;}
        if(start<0||end==int.MaxValue||(long)dataStart+end>til.Length)throw new InvalidDataException($"第 {index} 塊的 offset 不合理");
        var d=til.AsSpan(dataStart+start,end-start);
        if(d.Length<1)throw new InvalidDataException("空白的塊");
        int type=d[0],p=1;var pixels=new List<(int,int,int)>();
        int U16(ReadOnlySpan<byte> s){if(p+2>s.Length)throw new InvalidDataException("塊截斷");int v=BinaryPrimitives.ReadUInt16LittleEndian(s[p..]);p+=2;return v;}
        int U8(ReadOnlySpan<byte> s){if(p>=s.Length)throw new InvalidDataException("塊截斷");return s[p++];}
        if((type&2)==0){
            for(int y=0;y<24;y++){
                int count=y<12?2*(y+1):2*(23-y),x=(type&1)!=0?0:24-count;
                for(int i=0;i<count;i++)pixels.Add((x+i,y,U16(d)));
            }
        }else{
            if(d.Length<5)throw new InvalidDataException("塊截斷");
            int x0=d[1],y0=d[2],h=d[4];p=5;
            for(int y=y0;y<y0+h;y++){
                int x=x0,segments=U8(d);
                for(int j=0;j<segments;j++){
                    int skip=U8(d),run=U8(d);x+=skip/2;
                    for(int i=0;i<run;i++)pixels.Add((x+i,y,U16(d)));
                    x+=run;
                }
            }
        }
        if(p!=d.Length)throw new InvalidDataException("til block tail");
        return (type,pixels);
    }
}
