using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace P06.AssetPrep;

// T457 (#1042): recipe `map-effects` (docs/design/asset-recipe.md §1.2, §3.5, §6; docs/reference/asset-recipes/maps.md
// 「maps/effects/<id>.json 與 manifest.json」). Inputs: the effect tails of every listed .s32 (same scan and D3 selection as the
// `maps` recipe: MapScan), the 17 Sprite containers (<id>-0.spr) and Text list.spr / list.spz (both anchored).
// Output (assets-tree relative, §4.5):
//   maps/effects/<id>.json   compact, fixed key order, CRLF at the end (B against the baseline)
//   maps/effects/manifest.json  p24-map-effects-v1; source = {archive, name, size, sha256} (no path, no record position)
// An id the rules cannot produce goes to unavailable[] with the baseline's reason strings (L6); nothing is substituted.
// 6016.json is an output anchor: its whole-file SHA-256 must equal the product pin (Effect6016Definition.AcceptedMetadataSha256,
// ClientEffect6016.cs). A different result stops the recipe; the pin is never changed (§3.5).
[AssetRecipe]
public sealed class MapEffectsRecipe:IAssetRecipe,IAssetRecipeSources {
    public const string RecipeId="map-effects",EffectsItem="effects";
    public const string EffectsDirectory="maps/effects",ManifestPath="maps/effects/manifest.json",Schema="p24-map-effects-v1";
    public const string EventPolicy="Original event tokens preserved; sound and child emission not executed by this visual-only slice.";
    public const string Scope="Original stationary metadata sequences and source pixels, not live animation acceptance.";
    public const int TickRate=24;
    public const int Anchor6016=6016;

    public string Id=>RecipeId;
    public int Version=>1;
    public AssetRecipeCoverage Coverage=>AssetRecipeCoverage.Complete;
    public AssetPrepSources Sources=>AssetPrepSources.Sprites|AssetPrepSources.MapFiles;

    public IReadOnlyList<string> Normalize(IReadOnlyList<string> items){
        foreach(string item in items)if(item!=EffectsItem)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {RecipeId}：項目只能是 {EffectsItem}，不接受 \"{item}\"");
        return items.Distinct(StringComparer.Ordinal).ToList();
    }

    public static string FilePath(int id)=>string.Create(CultureInfo.InvariantCulture,$"{EffectsDirectory}/{id}.json");

    sealed record Produced(int Id,string Sha256,int Flags,int Frames,AssetSource Source,string MetadataSha256,IReadOnlyList<MapEffectSprStep> Sequence);

    public void Produce(string item,AssetRecipeItemContext context){
        if(item!=EffectsItem)throw new AssetPrepException(AssetPrepFailure.InvalidRequest,$"recipe {RecipeId}：未知項目 {item}");
        var ids=ReferencedIds(context);
        byte[] listSpr=context.ReadListSpr();
        var spz=context.ReadSpriteList().Decoded;
        var produced=new List<Produced>();var unavailable=new List<(int Id,string Error)>();
        foreach(int id in ids){
            context.Cancel.ThrowIfCancellationRequested();
            MapEffectDocument doc;
            try{doc=Build(context,id,listSpr,spz.Span);}
            catch(MapEffectUnavailable ex){unavailable.Add((id,ex.Reason));continue;}
            byte[] bytes=doc.Json;string sha=AssetPrepPaths.Sha256(bytes);
            if(id==Anchor6016&&context.Pins.Effect6016Sha256 is {} pin&&!sha.Equals(pin,StringComparison.OrdinalIgnoreCase))
                throw new AssetPrepException(AssetPrepFailure.ContainerMismatch,$"{FilePath(id)} 與支援版本不符（SHA-256 {sha}，產品 pin {pin}；來源 {doc.Source.Container} 的 {doc.Source.Entry}）。pin 不改，recipe 停止",doc.Source.Container);
            context.Write(FilePath(id),bytes);
            produced.Add(new(id,sha,doc.Flags,doc.FrameCount,doc.Source,doc.MetadataSha256,doc.Sequence));
        }
        if(context.Pins.Effect6016Sha256!=null&&!produced.Any(p=>p.Id==Anchor6016)){
            string why=unavailable.FirstOrDefault(u=>u.Id==Anchor6016) is {Error:{} e}?$"不可用：{e}":"沒有任何列入的 .s32 引用它";
            throw new AssetPrepException(AssetPrepFailure.MissingEntry,$"{FilePath(Anchor6016)} 無法產生（{why}）；這是支援版本的錨點，recipe 停止",AssetPrepPins.SpriteName(0));
        }
        context.Set("ids",ids.Count.ToString(CultureInfo.InvariantCulture));
        context.Set("effects",produced.Count.ToString(CultureInfo.InvariantCulture));
        context.Set("unavailable",unavailable.Count.ToString(CultureInfo.InvariantCulture));
        foreach(var g in unavailable.GroupBy(u=>u.Error).OrderBy(g=>g.Key,StringComparer.Ordinal))
            context.Set("unavailable:"+g.Key,g.Count().ToString(CultureInfo.InvariantCulture));
        context.Write(ManifestPath,MapEffectJson.Write(w=>{
            w.WriteStartObject();
            w.WriteString("schema",Schema);
            w.WriteStartArray("effects");
            foreach(var p in produced){
                w.WriteStartObject();
                w.WriteNumber("id",p.Id);w.WriteString("path",string.Create(CultureInfo.InvariantCulture,$"{p.Id}.json"));w.WriteString("sha256",p.Sha256);
                w.WriteNumber("flags",p.Flags);w.WriteNumber("frames",p.Frames);
                w.WriteStartObject("source");
                w.WriteString("archive",p.Source.Container);w.WriteString("name",p.Source.Entry);w.WriteNumber("size",p.Source.Size);w.WriteString("sha256",p.Source.Sha256);
                w.WriteEndObject();
                w.WriteString("metadataSha256",p.MetadataSha256);
                MapEffectJson.Sequence(w,p.Sequence);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("unavailable");
            foreach(var (id,error) in unavailable){w.WriteStartObject();w.WriteNumber("id",id);w.WriteString("error",error);w.WriteEndObject();}
            w.WriteEndArray();
            w.WriteString("scope",Scope);
            w.WriteEndObject();
        }));
    }

    /// <summary>
    /// Effect ids of every listed .s32 (maps.md step 1). Same selection as the `maps` recipe (MapScan, rule D3): a map with any
    /// block the product reader would refuse is left out whole; .seg blocks are parsed for that check but give no ids.
    /// </summary>
    public static SortedSet<int> ReferencedIds(AssetRecipeItemContext context){
        var ids=new SortedSet<int>();
        foreach(string directory in context.MapDirectories()){
            context.Cancel.ThrowIfCancellationRequested();
            if(!MapScan.IsMapDirectory(directory,out _))continue;
            MapDirectoryPlan? plan;
            try{plan=MapScan.Plan(directory,context.MapFiles(directory));}
            catch(InvalidDataException ex){throw new AssetPrepException(AssetPrepFailure.CorruptEntry,ex.Message,AssetPrepInput.MapContainer,ex);}
            if(plan==null)continue;
            var mapIds=new HashSet<int>();bool failed=false;
            foreach(var (_,file) in plan.Blocks){
                bool seg=file.EndsWith(".seg",StringComparison.OrdinalIgnoreCase);
                try{var scan=MapScan.Parse(context.ReadMapFile(directory,file),seg);if(!seg)mapIds.UnionWith(scan.EffectIds);}
                catch(InvalidDataException){failed=true;break;}
            }
            if(!failed)ids.UnionWith(mapIds);
        }
        return ids;
    }

    sealed record MapEffectDocument(byte[] Json,int Flags,int FrameCount,AssetSource Source,string MetadataSha256,IReadOnlyList<MapEffectSprStep> Sequence);

    // Python `re` on bytes, written out with ASCII classes (\s = [ \t\n\r\f\v], \d = [0-9]) so Latin-1 text behaves the same.
    const string Ws="[ \\t\\n\\r\\f\\v]";
    static readonly Regex action3=new(@"(?<![0-9.])3\.[A-Za-z][^(]*\(([0-9]+)"+Ws+@"+([0-9]+),([^)]*)\)",RegexOptions.CultureInvariant);
    static readonly Regex action0=new(@"(?<![0-9.])0\.[A-Za-z][^(]*\(([0-9]+)"+Ws+@"+([0-9]+),([^)]*)\)",RegexOptions.CultureInvariant);
    static readonly Regex token=new(@"\A([0-9]+)\.([0-9]+):([0-9]+)(.*)\z",RegexOptions.CultureInvariant|RegexOptions.Singleline);
    static readonly Regex attr=new(@"104\.attr\(([0-9]+)\)",RegexOptions.CultureInvariant);
    static readonly char[] splitBlank=[' ','\t','\n','\r','\v','\f'];

    static MapEffectDocument Build(AssetRecipeItemContext context,int id,byte[] listSpr,ReadOnlySpan<byte> spz){
        // 1. Source SPR: every container with <id>-0.spr; two different contents → unavailable (never one picked).
        string name=string.Create(CultureInfo.InvariantCulture,$"{id}-0.spr");
        var found=new List<(byte[] Data,AssetSource Source)>();
        for(int archive=0;archive<context.SpriteCount;archive++)if(context.TryReadSpriteSource(archive,name) is {} hit)found.Add(hit);
        if(found.Count==0)throw new MapEffectUnavailable("no sprite candidate");
        if(found.Select(f=>f.Source.Sha256).Distinct(StringComparer.Ordinal).Count()>1)throw new MapEffectUnavailable("ambiguous archive content");
        // 2. Metadata: the same bytes in list.spr and list.spz.
        byte[] entry=MetadataEntry(listSpr,id);
        if(!entry.AsSpan().SequenceEqual(MetadataEntry(spz,id)))throw new MapEffectUnavailable("list.spr/SPZ disagree");
        string text=Encoding.Latin1.GetString(entry);
        // 3. Sequence: action 3, otherwise action 0; directional → unavailable.
        var animation=action3.Match(text);if(!animation.Success)animation=action0.Match(text);
        var sequence=new List<MapEffectSprStep>();
        if(animation.Success){
            if(!IsZero(animation.Groups[1].Value))throw new MapEffectUnavailable("unresolved stationary direction");
            foreach(string t in animation.Groups[3].Value.Split(splitBlank,StringSplitOptions.RemoveEmptyEntries)){
                var m=token.Match(t);
                if(!m.Success||!IsZero(m.Groups[1].Value)||!TryInt(m.Groups[3].Value,out int ticks)||ticks<=0||!TryInt(m.Groups[2].Value,out int frame))
                    throw new MapEffectUnavailable("nonzero SPR segment or unsupported sequence token");
                string events=m.Groups[4].Value;
                if(events.Any(c=>c>127))throw new MapEffectUnavailable("non-ASCII event token");
                sequence.Add(new(frame,ticks,events));
            }
            if(!TryInt(animation.Groups[2].Value,out int count)||sequence.Count!=count)throw new MapEffectUnavailable("");
        }
        int flags=0;
        if(attr.Match(text) is {Success:true} a&&!TryInt(a.Groups[1].Value,out flags))throw new MapEffectUnavailable("");
        if(flags is not (0 or 8 or 128 or 256))throw new MapEffectUnavailable("");
        // 4. SPR.
        var (data,source)=found[0];
        var spr=MapEffectSpr.Decode(data);
        if(!animation.Success){
            if(spr.Frames.Count!=1)throw new MapEffectUnavailable("multiple frames without animation metadata");
            sequence.Add(new(0,1,""));
        }
        if(sequence.Any(s=>s.Frame>=spr.Frames.Count))throw new MapEffectUnavailable("");
        string metadataSha=AssetPrepPaths.Sha256(entry);
        byte[] json=MapEffectJson.Write(w=>{
            w.WriteStartObject();
            w.WriteNumber("id",id);w.WriteNumber("flags",flags);
            MapEffectJson.Sequence(w,sequence);
            w.WriteStartArray("frames");
            for(int f=0;f<spr.Frames.Count;f++){
                var frame=spr.Frames[f];
                w.WriteStartObject();
                w.WriteNumber("index",f);
                w.WriteStartArray("bounds");foreach(int v in frame.Bounds)w.WriteNumberValue(v);w.WriteEndArray();
                w.WriteStartArray("blocks");
                for(int i=0;i<frame.Tiles.Count;i++){
                    var tile=frame.Tiles[i];
                    w.WriteStartObject();
                    w.WriteNumber("a",tile.A);w.WriteNumber("b",tile.B);w.WriteNumber("z",tile.Z);w.WriteNumber("order",i);
                    w.WriteStartArray("pixels");
                    foreach(var p in spr.Blocks[tile.BlockIndex]){w.WriteStartArray();w.WriteNumberValue(p.X);w.WriteNumberValue(p.Y);w.WriteNumberValue(p.Color);w.WriteEndArray();}
                    w.WriteEndArray();
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteString("entrySha256",metadataSha);w.WriteString("spriteSha256",source.Sha256);
            w.WriteNumber("tickRate",TickRate);w.WriteString("eventPolicy",EventPolicy);
            w.WriteEndObject();
        });
        return new(json,flags,spr.Frames.Count,source,metadataSha,sequence);
    }

    static bool IsZero(string digits)=>digits.Length>0&&digits.All(c=>c=='0');
    static bool TryInt(string digits,out int value)=>int.TryParse(digits,NumberStyles.None,CultureInfo.InvariantCulture,out value);
    static bool Blank(byte c)=>c is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or 0x0B or 0x0C;

    /// <summary>
    /// maps.md「metadata」: from a line that starts with <c>#&lt;id&gt;</c> plus one blank, up to (not including) the next line that
    /// starts with <c>#&lt;digits&gt;</c> plus a blank (the search starts right after the first blank, which counts as a line start).
    /// </summary>
    public static byte[] MetadataEntry(ReadOnlySpan<byte> data,int id){
        byte[] digits=Encoding.ASCII.GetBytes(id.ToString(CultureInfo.InvariantCulture));
        int start=-1;
        for(int i=0;i+digits.Length+1<data.Length;i++){
            if(data[i]!='#'||(i>0&&data[i-1]!='\n'))continue;
            if(data.Slice(i+1,digits.Length).SequenceEqual(digits)&&Blank(data[i+1+digits.Length])){start=i;break;}
        }
        if(start<0)throw new MapEffectUnavailable("no original animation metadata");
        int after=start+1+digits.Length+1,end=data.Length;
        for(int j=after;j<data.Length;j++){
            if(data[j]!='#'||(j>after&&data[j-1]!='\n'))continue;
            int k=j+1;while(k<data.Length&&data[k] is >=(byte)'0' and <=(byte)'9')k++;
            if(k>j+1&&k<data.Length&&Blank(data[k])){end=j;break;}
        }
        return data[start..end].ToArray();
    }
}

/// <summary>An id the map-effects rules cannot produce; <see cref="Reason"/> is the manifest's error string.</summary>
public sealed class MapEffectUnavailable(string reason):Exception(reason.Length>0?reason:"unavailable"){
    public string Reason{get;}=reason;
}

public sealed record MapEffectSprStep(int Frame,int Ticks,string Events);

/// <summary>Compact JSON of the effect files (json.dumps(separators=(',', ':'), ensure_ascii=False)) plus the CRLF the baseline files end with.</summary>
public static class MapEffectJson {
    public static byte[] Write(Action<Utf8JsonWriter> body){
        using var stream=new MemoryStream();
        using(var w=new Utf8JsonWriter(stream,SceneRecipe.CompactOptions))body(w);
        stream.Write("\r\n"u8);
        return stream.ToArray();
    }
    public static void Sequence(Utf8JsonWriter w,IEnumerable<MapEffectSprStep> steps){
        w.WriteStartArray("sequence");
        foreach(var s in steps){w.WriteStartObject();w.WriteNumber("frame",s.Frame);w.WriteNumber("ticks",s.Ticks);w.WriteString("events",s.Events);w.WriteEndObject();}
        w.WriteEndArray();
    }
}

/// <summary>
/// The SPR decoder of the map effects (maps.md step 6), stricter than SprFile: no empty frames, every pixel inside the frame's
/// bounds, every frame draws something. Failures carry the baseline's reason strings (MapEffectUnavailable).
/// </summary>
public static class MapEffectSpr {
    public sealed record Frame(int[] Bounds,IReadOnlyList<SprTile> Tiles);
    public sealed record Result(IReadOnlyList<Frame> Frames,IReadOnlyList<Pixel[]> Blocks,bool Indexed);

    public static Result Decode(byte[] data){
        int pos=0;
        static MapEffectUnavailable Bad(string why)=>new(why);
        void Need(int n){if(pos+n>data.Length)throw Bad($"truncated at {pos}");}
        int U8(){Need(1);return data[pos++];}
        int I8(){Need(1);return (sbyte)data[pos++];}
        int U16(){Need(2);int v=BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos));pos+=2;return v;}
        int I16(){Need(2);int v=BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(pos));pos+=2;return v;}
        uint U32(){Need(4);uint v=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos));pos+=4;return v;}
        int first=U8();bool indexed=first==255;int[] palette=[];int n=first;
        if(indexed){
            int count=U8();if(count==0)count=256;
            palette=new int[count];for(int i=0;i<count;i++)palette[i]=U16();
            n=U8();
            if(palette.Any(c=>(c&0x8000)!=0))throw Bad("unsupported palette high bit");
        }
        if(n is <1 or >127)throw Bad("invalid frame count");
        var frames=new List<Frame>();long total=0;
        for(int f=0;f<n;f++){
            // 7 values are read before any check (struct '<hhhhHHh').
            Need(14);int l=I16(),t=I16(),r=I16(),b=I16();U16();U16();int tiles=I16();
            if(!(r-l+1 is >0 and <=2048&&b-t+1 is >0 and <=2048))throw Bad("invalid frame dimensions");
            total+=(long)(r-l+1)*(b-t+1);
            if(total>16*1024*1024)throw Bad("decoded pixels exceed 64 MiB policy");
            if(tiles is <=0 or >4096)throw Bad("invalid tile count");
            var list=new SprTile[tiles];
            for(int j=0;j<tiles;j++){Need(5);int a=I8(),bb=I8(),mode=U8(),index=U16();list[j]=new(a,bb,mode,index);}
            frames.Add(new([l,t,r,b],list));
        }
        uint blockCount=U32();
        if(blockCount is 0 or >10000)throw Bad("invalid block table count");
        var offsets=new uint[blockCount];for(int i=0;i<blockCount;i++)offsets[i]=U32();
        uint expandedSize=U32();int dataStart=pos;
        if(offsets[0]!=0)throw Bad("invalid block offsets");
        for(int i=1;i<offsets.Length;i++)if(offsets[i-1]>=offsets[i])throw Bad("invalid block offsets");
        var blocks=new List<Pixel[]>();long expanded=0;
        for(int i=0;i<blockCount;i++){
            int start=pos;Need(4);int sx=U8(),sy=U8(),w=U8(),lines=U8();long ex=4;
            if(sx+w>24||sy+lines>24||w==0||lines==0)throw Bad("invalid block dimensions");
            var pixels=new List<Pixel>();
            for(int y=sy;y<sy+lines;y++){
                int x=sx,segments=U8();ex++;
                for(int s=0;s<segments;s++){
                    Need(2);int skip=U8(),run=U8();ex+=2;
                    if(skip%2!=0)throw Bad("odd byte skip unsupported");
                    x+=skip/2;
                    if(run==0||x+run>sx+w)throw Bad("run outside block");
                    for(int j=0;j<run;j++){
                        ex+=2;int color;
                        if(indexed){int pi=U8();if(pi>=palette.Length)throw Bad("palette index out of range");color=palette[pi];}
                        else{color=U16();if((color&0x8000)!=0)throw Bad("unsupported palette high bit");}
                        pixels.Add(new(x,y,(ushort)color));x++;
                    }
                }
            }
            if(offsets[i]!=start-dataStart)throw Bad("block offset disagrees with sequential parsing");
            blocks.Add(pixels.ToArray());expanded+=ex;
        }
        if(pos!=data.Length)throw Bad("trailing bytes");
        if(expanded!=expandedSize)throw Bad("expanded length mismatch");
        foreach(var frame in frames){
            int drawn=0;int l=frame.Bounds[0],t=frame.Bounds[1],r=frame.Bounds[2],b=frame.Bounds[3];
            foreach(var tile in frame.Tiles){
                if(tile.BlockIndex>=blocks.Count)throw Bad("illegal block index");
                int half=tile.A>>1;   // Python a // 2 (floor)
                int bx=24*(tile.B+tile.A-half),by=12*(tile.B-half);
                foreach(var p in blocks[tile.BlockIndex]){
                    if(!(l<=bx+p.X&&bx+p.X<=r&&t<=by+p.Y&&by+p.Y<=b))throw Bad("opaque pixel outside frame");
                    drawn++;
                }
            }
            if(drawn==0)throw Bad("empty decoded frame");
        }
        return new(frames,blocks,indexed);
    }
}
