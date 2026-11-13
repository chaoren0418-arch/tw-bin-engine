using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace P06.AssetPrep;

// T454 (#1039): the layer every SPR recipe shares. Rules: docs/reference/asset-recipes/sprites.md §2–§3 and §8,
// docs/design/asset-recipe.md 3.2. Nothing here carries frames, pixels or table content: only the format rules.
//   SpriteList   list.spz: gfx → sprite number, actions (frame sequences) and numeric fields of one entry
//   SprFile      one <sprite>-<sid>.spr decoded: frames (bounds, tiles) and 24×24 pixel blocks
//   SprSource    which container a file comes from: main Sprite first, otherwise unique among Sprite00–15
//   SprBody      the files of one sprite number, read on demand through the item context
//   SprTiming    the three duration formulas and the release point
//   SprJson      the frame / sequence JSON the product's readers (Assets, ObjectAppearance) load
// GroundSpriteReader (#190) is deliberately left alone: it keeps its own pins, its TW13081901.txt lookup and its
// stricter empty-frame rule. This decoder follows the same byte layout with the checks of sprites.md §8.2.

/// <summary>One token of an action: <c>&lt;base&gt;.&lt;frame&gt;:&lt;ticks&gt;&lt;events&gt;</c>. Events are the token's tail, copied, never interpreted.</summary>
public sealed record SprStep(int Base,int Frame,int Ticks,string Events){
    /// <summary>floor(ticks × 1000 ÷ 24): the per-step milliseconds of the baseline files.</summary>
    public int Milliseconds=>Ticks*1000/24;
}

/// <summary><c>&lt;id&gt;.&lt;name&gt;(&lt;directional&gt; &lt;count&gt;,&lt;steps…&gt;)</c>.</summary>
public sealed record SprAction(int Id,string Name,bool Directional,int Count,IReadOnlyList<SprStep> Steps){
    public int TotalTicks=>Steps.Sum(s=>s.Ticks);
    /// <summary>File number for a step: base + direction for a directional action, base otherwise.</summary>
    public int Sid(SprStep step,int direction)=>Directional?step.Base+direction:step.Base;
}

/// <summary>One <c>#gfx</c> entry of list.spz. Actions are parsed when asked for, so a malformed action the item does not use is harmless.</summary>
public sealed class SprEntry {
    static readonly Regex header=new(@"\A#(\d+)[ \t]+(\d+)(?:[ \t]*=[ \t]*(\d+))?",RegexOptions.CultureInvariant);
    // The id must not continue a number or a frame token (" 8.7:4"); the name starts with a letter and may contain spaces.
    static readonly Regex partHead=new(@"(?<![\w.])(\d+)\.([A-Za-z][^()]*)\z",RegexOptions.CultureInvariant);
    static readonly Regex actionBody=new(@"\A\s*(\d+)\s+(\d+)\s*,(.*)\z",RegexOptions.CultureInvariant|RegexOptions.Singleline);
    static readonly Regex stepToken=new(@"\A(\d+)\.(\d+):(\d+)(.*)\z",RegexOptions.CultureInvariant|RegexOptions.Singleline);
    static readonly char[] blank=[' ','\t','\r','\n'];
    readonly Dictionary<int,(string Name,string Body)> parts=[];
    public int Gfx{get;}
    /// <summary>The number after the gfx in the header (the entry's file count).</summary>
    public int Count{get;}
    /// <summary><c>#gfx count=sprite</c>: the number after '=', otherwise the gfx itself (sprites.md §3.1).</summary>
    public int Sprite{get;}
    /// <summary>SHA-256 of the entry's bytes, from <c>#</c> to just before the next line that starts an entry (the baseline's entryHash).</summary>
    public string Sha256{get;}

    internal SprEntry(ReadOnlySpan<byte> raw){
        Sha256=AssetPrepPaths.Sha256(raw);
        string text=Encoding.Latin1.GetString(raw);
        var h=header.Match(text);
        if(!h.Success||!int.TryParse(h.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int gfx)||!int.TryParse(h.Groups[2].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int count))
            throw new AssetPrepException(AssetPrepFailure.CorruptEntry,"list.spz：entry 表頭格式不支援",AssetPrepInput.TextContainer);
        int sprite=gfx;
        if(h.Groups[3].Success&&!int.TryParse(h.Groups[3].Value,NumberStyles.None,CultureInfo.InvariantCulture,out sprite))
            throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"list.spz：gfx {gfx} 的 sprite 編號格式不支援",AssetPrepInput.TextContainer);
        Gfx=gfx;Count=count;Sprite=sprite;
        // Cut the entry into "<id>.<name>( … )" parts first, then look parts up by id (sprites.md §8.3).
        int pos=h.Length;
        while(pos<text.Length){
            int open=text.IndexOf('(',pos);if(open<0)break;
            int start=open;while(start<text.Length&&text[start]=='(')start++;      // "66.shop loop((1 16,…" has two
            int close=text.IndexOf(')',start);if(close<0)break;
            var m=partHead.Match(text[pos..open]);
            if(m.Success&&int.TryParse(m.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int id))
                parts.TryAdd(id,(m.Groups[2].Value.Trim(),text[start..close]));
            pos=close+1;while(pos<text.Length&&text[pos]==')')pos++;
        }
    }

    public bool Has(int id)=>parts.ContainsKey(id);
    public IReadOnlyCollection<int> Ids=>parts.Keys;

    /// <summary>The action with this number, or null when the entry has none. A malformed body is refused (CorruptEntry).</summary>
    public SprAction? Action(int id){
        if(!parts.TryGetValue(id,out var part))return null;
        AssetPrepException Bad(string why)=>new(AssetPrepFailure.CorruptEntry,$"list.spz：gfx {Gfx} 的動作 {id}（{part.Name}）{why}",AssetPrepInput.TextContainer);
        var m=actionBody.Match(part.Body);
        if(!m.Success||m.Groups[1].Value is not ("0" or "1")||!int.TryParse(m.Groups[2].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int count))throw Bad("格式不支援");
        var steps=new List<SprStep>();
        foreach(string token in m.Groups[3].Value.Split(blank,StringSplitOptions.RemoveEmptyEntries)){
            var s=stepToken.Match(token);
            if(!s.Success||!int.TryParse(s.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int b)||!int.TryParse(s.Groups[2].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int frame)
                ||!int.TryParse(s.Groups[3].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int ticks)||b>ushort.MaxValue||frame>255||ticks>100000)throw Bad($"的步 \"{token}\" 格式不支援");
            steps.Add(new(b,frame,ticks,s.Groups[4].Value));
        }
        if(steps.Count==0)throw Bad("沒有任何步");
        return new(id,part.Name,m.Groups[1].Value=="1",count,steps);
    }

    /// <summary>A numeric field such as <c>101.shadow(3226)</c> or <c>105.clothes(6 201 204 …)</c>; null when absent or not all integers.</summary>
    public IReadOnlyList<int>? Field(int id){
        if(!parts.TryGetValue(id,out var part))return null;
        var values=new List<int>();
        foreach(string token in part.Body.Split(blank,StringSplitOptions.RemoveEmptyEntries)){
            if(!int.TryParse(token,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int v))return null;
            values.Add(v);
        }
        return values;
    }
}

/// <summary>The decoded animation table. An entry runs from a line starting with <c>#&lt;digit&gt;</c> to the next such line.</summary>
public sealed class SpriteList {
    // Original list.spz format constants (the same published values Afflictions.cs / the UI XML reader use; not credentials).
    static readonly byte[] SpzKey=Convert.FromHexString("DC8401212A40200ADD25B9A70DB9C94E"),SpzIv=Convert.FromHexString("3E0978AAC4D53063300C5F9A807F2246");
    const int SpzHeader=5;
    readonly byte[] data;
    readonly Dictionary<int,(int Start,int End)> ranges=[];
    readonly Dictionary<int,SprEntry> parsed=[];
    SpriteList(byte[] data){this.data=data;Sha256=AssetPrepPaths.Sha256(data);}
    public int Count=>ranges.Count;
    /// <summary>T457: the fully decoded table (the map effects cut their metadata bytes from it, maps.md「metadata」).</summary>
    public ReadOnlyMemory<byte> Decoded=>data;
    /// <summary>SHA-256 of the decoded table (the list.spz anchor value for the supported client).</summary>
    public string Sha256{get;}

    /// <summary>Inner layer of list.spz: after the Text DES layer the payload is <c>S1223</c>, AES-128-CBC over the whole
    /// 16-byte blocks that follow, and a tail XORed with the previous cipher block (same as SpriteTypeTable.Load).</summary>
    public static byte[] DecodeSpz(byte[] raw){
        if(!raw.AsSpan().StartsWith("S1223"u8))throw new AssetPrepException(AssetPrepFailure.CorruptEntry,"list.spz：未知格式（開頭不是 S1223）",AssetPrepInput.TextContainer);
        using var aes=Aes.Create();aes.Key=SpzKey;
        int length=(raw.Length-SpzHeader)/16*16;
        var decoded=(byte[])raw.Clone();
        aes.DecryptCbc(raw.AsSpan(SpzHeader,length),SpzIv,PaddingMode.None).CopyTo(decoded,SpzHeader);
        for(int i=SpzHeader+length;i<raw.Length;i++)decoded[i]=(byte)(raw[i]^(length>0?raw[i-16]:SpzIv[i-SpzHeader]));
        return decoded;
    }
    /// <summary>Inverse of <see cref="DecodeSpz"/>; only used to build synthetic test tables.</summary>
    public static byte[] EncodeSpz(byte[] plain){
        if(!plain.AsSpan().StartsWith("S1223"u8))throw new ArgumentException("list.spz 的內容必須以 S1223 開頭");
        using var aes=Aes.Create();aes.Key=SpzKey;
        int length=(plain.Length-SpzHeader)/16*16;
        var raw=(byte[])plain.Clone();
        aes.EncryptCbc(plain.AsSpan(SpzHeader,length),SpzIv,PaddingMode.None).CopyTo(raw,SpzHeader);
        for(int i=SpzHeader+length;i<plain.Length;i++)raw[i]=(byte)(plain[i]^(length>0?raw[i-16]:SpzIv[i-SpzHeader]));
        return raw;
    }

    public static SpriteList Parse(byte[] decoded){
        var list=new SpriteList(decoded);
        var starts=new List<(int At,int Gfx)>();
        for(int i=0;i+1<decoded.Length;i++){
            if(decoded[i]!='#'||(i>0&&decoded[i-1]!='\n')||decoded[i+1] is <(byte)'0' or >(byte)'9')continue;
            long gfx=0;int j=i+1;
            for(;j<decoded.Length&&decoded[j] is >=(byte)'0' and <=(byte)'9'&&gfx<=int.MaxValue;j++)gfx=gfx*10+(decoded[j]-'0');
            if(gfx>int.MaxValue)throw new AssetPrepException(AssetPrepFailure.CorruptEntry,"list.spz：gfx 編號超出範圍",AssetPrepInput.TextContainer);
            starts.Add((i,(int)gfx));
        }
        for(int k=0;k<starts.Count;k++){
            // One gfx, two entries: refused rather than picking one.
            if(!list.ranges.TryAdd(starts[k].Gfx,(starts[k].At,k+1<starts.Count?starts[k+1].At:decoded.Length)))
                throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"list.spz：gfx {starts[k].Gfx} 有兩筆 entry",AssetPrepInput.TextContainer);
        }
        if(list.ranges.Count==0)throw new AssetPrepException(AssetPrepFailure.CorruptEntry,"list.spz：沒有任何 entry",AssetPrepInput.TextContainer);
        return list;
    }

    public SprEntry? Find(int gfx){
        if(parsed.TryGetValue(gfx,out var hit))return hit;
        if(!ranges.TryGetValue(gfx,out var range))return null;
        hit=new SprEntry(data.AsSpan(range.Start,range.End-range.Start));
        if(hit.Gfx!=gfx)throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"list.spz：gfx {gfx} 的 entry 表頭不一致",AssetPrepInput.TextContainer);
        parsed[gfx]=hit;return hit;
    }
    public SprEntry Require(int gfx)=>Find(gfx)??throw new AssetPrepException(AssetPrepFailure.Unavailable,$"list.spz 沒有 gfx {gfx} 的 entry",AssetPrepInput.TextContainer);
}

public sealed record SprTile(int A,int B,int Z,int BlockIndex);

/// <summary>One frame. Bounds = left, top, right, bottom as stored. An empty frame has no tiles and keeps its stored bounds.</summary>
public sealed record SprFrame(int[] Bounds,bool Empty,IReadOnlyList<SprTile> Tiles);

/// <summary>A decoded .spr (sprites.md §3.4). Pixel coordinates are inside the 24×24 block; colours are RGB555.</summary>
public sealed class SprFile {
    public IReadOnlyList<SprFrame> Frames{get;}
    public IReadOnlyList<Pixel[]> Blocks{get;}
    public string Sha256{get;}
    SprFile(IReadOnlyList<SprFrame> frames,IReadOnlyList<Pixel[]> blocks,string sha){Frames=frames;Blocks=blocks;Sha256=sha;}

    ref struct Reader(ReadOnlySpan<byte> data,string what){
        readonly ReadOnlySpan<byte> data=data;
        readonly string what=what;
        public int Position;
        ReadOnlySpan<byte> Take(int n){
            if(Position+n>data.Length)throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"{what}：SPR 截斷");
            var s=data.Slice(Position,n);Position+=n;return s;
        }
        public int U8()=>Take(1)[0];
        public int I8()=>(sbyte)Take(1)[0];
        public int U16()=>BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
        public int I16()=>BinaryPrimitives.ReadInt16LittleEndian(Take(2));
        public uint U32()=>BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    }

    /// <summary>Decodes a whole file. <paramref name="what"/> names it in refusals (CorruptEntry).</summary>
    public static SprFile Decode(byte[] payload,string what){
        void Require(bool ok,string why){if(!ok)throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"{what}：SPR {why}");}
        Require(payload.Length is >0 and <=PrepArchive.MaxEntrySize,"大小不合理");
        var r=new Reader(payload,what);
        int first=r.U8();bool indexed=first==255;ushort[] palette=[];
        int n=first;
        if(indexed){
            int count=r.U8();if(count==0)count=256;
            palette=new ushort[count];
            for(int i=0;i<count;i++){palette[i]=(ushort)r.U16();Require((palette[i]&0x8000)==0,"調色盤顏色最高位不為 0");}
            n=r.U8();
        }
        Require(n>0,"沒有任何幀");
        var frames=new List<SprFrame>(n);int totalTiles=0;
        for(int f=0;f<n;f++){
            int[] bounds=[r.I16(),r.I16(),r.I16(),r.I16()];int u=r.U16(),v=r.U16(),tileCount=r.I16();
            // sprites.md §8.2: empty = no tiles, u = 65535, v = 0 and inverted bounds; the bounds are kept as stored.
            bool empty=tileCount==0&&u==65535&&v==0&&bounds[2]<bounds[0]&&bounds[3]<bounds[1];
            Require(empty||(bounds[2]>=bounds[0]&&bounds[3]>=bounds[1]&&bounds[2]-bounds[0]<2048&&bounds[3]-bounds[1]<2048&&tileCount is >0 and <=4096),$"第 {f} 幀的表頭不合法");
            totalTiles+=tileCount;Require(totalTiles<=65536,"tile 總數過多");
            var tiles=new SprTile[tileCount];
            for(int j=0;j<tileCount;j++){int a=r.I8(),b=r.I8(),z=r.U8(),index=r.U16();tiles[j]=new(a,b,z,index);}
            frames.Add(new(bounds,empty,tiles));
        }
        uint blockCount=r.U32();Require(blockCount<=65536,"block 數過多");
        Require(blockCount>0||frames.All(f=>f.Empty),"沒有任何 block");
        var offsets=new uint[blockCount];for(int i=0;i<blockCount;i++)offsets[i]=r.U32();
        uint expected=r.U32();int start=r.Position;long expanded=0;int totalPixels=0;
        var blocks=new List<Pixel[]>((int)blockCount);
        for(int i=0;i<blockCount;i++){
            Require(offsets[i]==r.Position-start,$"block {i} 的 offset 與實際位置不符");
            int sx=r.U8(),sy=r.U8(),w=r.U8(),lines=r.U8();
            Require(w>0&&lines>0&&sx+w<=24&&sy+lines<=24,$"block {i} 超出 24×24");
            expanded+=4;var pixels=new List<Pixel>();
            for(int y=sy;y<sy+lines;y++){
                int x=sx,segments=r.U8();expanded++;
                for(int j=0;j<segments;j++){
                    int skip=r.U8(),run=r.U8();expanded+=2;
                    Require(skip%2==0,$"block {i} 的 skip 不是偶數");x+=skip/2;
                    Require(run>0&&x+run<=sx+w,$"block {i} 的像素超出寬度");
                    for(int k=0;k<run;k++){
                        int color;
                        if(indexed){int index=r.U8();Require(index<palette.Length,$"block {i} 的調色盤索引越界");color=palette[index];}
                        else color=r.U16();
                        Require((color&0x8000)==0,$"block {i} 的顏色最高位不為 0");
                        Require(++totalPixels<=4_000_000,"像素數過多");
                        pixels.Add(new(x++,y,(ushort)color));expanded+=2;
                    }
                }
            }
            blocks.Add(pixels.ToArray());
        }
        Require(r.Position==payload.Length,"檔尾有多餘 bytes");
        Require(expanded==expected,"展開大小不符");
        Require(frames.All(f=>f.Tiles.All(t=>t.BlockIndex<blocks.Count)),"tile 指向不存在的 block");
        return new(frames,blocks,AssetPrepPaths.Sha256(payload));
    }

    /// <summary>Canvas position of a tile's block origin: x = 24 × (b + a − floor(a/2)), y = 12 × (b − floor(a/2)).</summary>
    public static (int X,int Y) Origin(int a,int b){int half=(int)Math.Floor(a/2d);return (24*(b+a-half),12*(b-half));}

    /// <summary>True when every pixel of the frame lies inside its bounds (the check the players recipe needs, sprites.md §4.2).</summary>
    public bool PixelsInsideBounds(int frame)=>FirstPixelOutsideBounds(frame)==null;
    /// <summary>T455: canvas position of the first pixel (tile order, then block order) outside the frame's bounds; null when there is none.</summary>
    public (int X,int Y)? FirstPixelOutsideBounds(int frame){
        var f=Frames[frame];
        foreach(var tile in f.Tiles){
            var (ox,oy)=Origin(tile.A,tile.B);
            foreach(var p in Blocks[tile.BlockIndex]){
                int x=ox+p.X,y=oy+p.Y;
                if(x<f.Bounds[0]||x>f.Bounds[2]||y<f.Bounds[1]||y>f.Bounds[3])return (x,y);
            }
        }
        return null;
    }
}

/// <summary>Which container a file is taken from (sprites.md §3.3; this product's choice, not a claim about the original lookup order).</summary>
public static class SprSource {
    /// <summary>
    /// The main container (Sprite) wins when it has the name; the others are then not looked at. Otherwise every
    /// same-named file in Sprite00–15 must be byte-identical: two different contents make the file unavailable
    /// (refused, never one picked). Null when no container has the name. Every entry read is recorded as a source.
    /// </summary>
    public static byte[]? TryRead(AssetRecipeItemContext context,string name)=>TryChoose(context,name)?.Data;
    /// <summary>T477: like <see cref="TryRead"/>, with the container index the bytes were taken from.</summary>
    public static (int Archive,byte[] Data)? TryChoose(AssetRecipeItemContext context,string name)=>
        // T479: the rule itself is shared with the runtime readers (P06.NativeUi.SpriteContainerRule).
        P06.NativeUi.SpriteContainerRule.Choose(context.SpriteCount,archive=>context.TryReadSprite(archive,name),
            (first,other)=>new AssetPrepException(AssetPrepFailure.Unavailable,$"{name} 不在主包 Sprite，替代包 {context.SpriteName(first)} 與 {context.SpriteName(other)} 的內容不同；不選用任何一份",context.SpriteName(other)));
    public static byte[] Read(AssetRecipeItemContext context,string name)=>
        TryRead(context,name)??throw Missing(name);
    static AssetPrepException Missing(string name)=>new(AssetPrepFailure.MissingEntry,$"{name} 不在 TW_CLIENT_ROOT 的任何 Sprite 容器");

    /// <summary>
    /// T477: the chosen file decoded. With a <see cref="SprDecodeCache"/> on the context (runtime on-demand only) a file already
    /// decoded from the same container entry is reused; the key is the entry's location, never the gfx (asset-recipe.md 5).
    /// The bytes are still read so the main-first / alternates-identical rule is applied the same way every time.
    /// </summary>
    public static SprFile Decode(AssetRecipeItemContext context,string name){
        var chosen=TryChoose(context,name)??throw Missing(name);
        if(context.DecodeCache is not {} cache)return SprFile.Decode(chosen.Data,name);
        var source=context.SpriteSource(chosen.Archive,name);
        return cache.Get(new(source.Container,source.Offset,source.Size,cache.RecipeId,cache.RecipeVersion,cache.ListSpzSha256),()=>SprFile.Decode(chosen.Data,name));
    }
}

/// <summary>
/// T477 (#1073): decoded-SPR cache of the runtime on-demand path. Key = (container, idx offset, idx size, recipe id and version,
/// SHA-256 of the decoded list.spz) — several gfx share one sprite, so the gfx number is never the key. Memory only; nothing
/// here touches a disk. Thread-safe; a file is decoded at most once per key even when two items ask at the same time.
/// </summary>
public sealed record SprDecodeKey(string Container,long Offset,int Size,string RecipeId,int RecipeVersion,string ListSpzSha256);
public sealed class SprDecodeCache(string recipeId,int recipeVersion,string listSpzSha256) {
    readonly System.Collections.Concurrent.ConcurrentDictionary<SprDecodeKey,Lazy<SprFile>> files=new();
    int hits,decodes;
    public string RecipeId=>recipeId;
    public int RecipeVersion=>recipeVersion;
    public string ListSpzSha256=>listSpzSha256;
    public int Count=>files.Count;
    /// <summary>Lookups answered from the cache.</summary>
    public int Hits=>Volatile.Read(ref hits);
    /// <summary>Files actually decoded (one per key).</summary>
    public int Decodes=>Volatile.Read(ref decodes);
    public IReadOnlyCollection<SprDecodeKey> Keys=>files.Keys.ToArray();
    public SprFile Get(SprDecodeKey key,Func<SprFile> decode){
        bool created=false;
        var lazy=files.GetOrAdd(key,_=>{created=true;return new Lazy<SprFile>(()=>{Interlocked.Increment(ref decodes);return decode();},LazyThreadSafetyMode.ExecutionAndPublication);});
        if(!created)Interlocked.Increment(ref hits);
        try{return lazy.Value;}
        // A refused file is not kept: the item that asked records its own refusal (the caller remembers per gfx).
        catch{files.TryRemove(new KeyValuePair<SprDecodeKey,Lazy<SprFile>>(key,lazy));throw;}
    }
}

/// <summary>The files of one sprite number (<c>&lt;sprite&gt;-&lt;sid&gt;.spr</c>), read and decoded once per item.</summary>
public sealed class SprBody(AssetRecipeItemContext context,int sprite) {
    readonly SortedDictionary<int,SprFile> files=[];
    public int Sprite=>sprite;
    public static string FileName(int sprite,int sid)=>string.Create(CultureInfo.InvariantCulture,$"{sprite}-{sid}.spr");
    public SprFile File(int sid){
        if(files.TryGetValue(sid,out var file))return file;
        context.Cancel.ThrowIfCancellationRequested();
        string name=FileName(sprite,sid);
        file=SprSource.Decode(context,name);
        files[sid]=file;return file;
    }
    /// <summary>The file numbers read so far, ascending.</summary>
    public IReadOnlyCollection<int> Sids=>files.Keys;

    /// <summary>
    /// The eight directions of one action: step keys are <c>"&lt;sid&gt;:&lt;frame&gt;"</c>. <paramref name="directionOf"/> maps the
    /// heading to the file offset (identity unless a rule says otherwise). A step that names a missing file or frame is refused.
    /// </summary>
    public SprSequence[] Sequences(string label,SprAction action,Func<int,int>? directionOf=null){
        var result=new SprSequence[8];
        for(int d=0;d<8;d++){
            var keys=new string[action.Steps.Count];
            for(int i=0;i<keys.Length;i++){
                var step=action.Steps[i];int sid=action.Sid(step,directionOf?.Invoke(d)??d);
                var file=File(sid);
                if(step.Frame>=file.Frames.Count)
                    throw new AssetPrepException(AssetPrepFailure.CorruptEntry,$"動作 {action.Id}（{action.Name}）的步 {step.Base}.{step.Frame} 超出 {FileName(sprite,sid)} 的 {file.Frames.Count} 幀");
                keys[i]=SprJson.Key(sid,step.Frame);
            }
            result[d]=new(label,d,action,keys);
        }
        return result;
    }
}

/// <summary>One (action label, direction) row of an output file.</summary>
public sealed record SprSequence(string Label,int Direction,SprAction Action,IReadOnlyList<string> Keys);

/// <summary>Durations (sprites.md §3.6, action-timing.md R1–R3). 24 ticks per second; the ×40 form is the server interval.</summary>
public static class SprTiming {
    /// <summary>40 × total ticks.</summary>
    public static int Times40(SprAction action)=>40*action.TotalTicks;
    /// <summary>Σ floor(ticks × 1000 ÷ 24) over the steps.</summary>
    public static int Floor(SprAction action)=>action.Steps.Sum(s=>s.Milliseconds);
    /// <summary>total ticks × 1000 ÷ 24, not rounded.</summary>
    public static double Exact(SprAction action)=>action.TotalTicks*1000d/24;
    /// <summary>Σ floor milliseconds of the steps before the first one whose events contain '!'; null when there is none.</summary>
    public static int? Release(SprAction action){
        int at=0;
        foreach(var step in action.Steps){if(step.Events.Contains('!'))return at;at+=step.Milliseconds;}
        return null;
    }
}

/// <summary>Output JSON shared by the SPR recipes: compact, fixed key order, invariant numbers — the same input gives the same bytes.</summary>
public static class SprJson {
    public static string Key(int sid,int frame)=>string.Create(CultureInfo.InvariantCulture,$"{sid}:{frame}");

    public static byte[] Write(Action<Utf8JsonWriter> body){
        using var stream=new MemoryStream();
        // Relaxed escaping keeps event strings such as "<479" literal; everything written here is ASCII.
        using(var writer=new Utf8JsonWriter(stream,new JsonWriterOptions{Indented=false,SkipValidation=false,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping})){body(writer);}
        return stream.ToArray();
    }

    static void Bounds(Utf8JsonWriter w,int[] bounds){
        w.WriteStartArray("bounds");foreach(int v in bounds)w.WriteNumberValue(v);w.WriteEndArray();
    }
    /// <summary>One block: a, b, z, source_order and the sparse pixels [x, y, rgb555] (what Assets / ObjectAppearance read).</summary>
    public static void Block(Utf8JsonWriter w,SprTile tile,int order,Pixel[] pixels){
        w.WriteStartObject();
        w.WriteNumber("a",tile.A);w.WriteNumber("b",tile.B);w.WriteNumber("z",tile.Z);w.WriteNumber("source_order",order);
        w.WriteStartArray("pixels");
        foreach(var p in pixels){w.WriteStartArray();w.WriteNumberValue(p.X);w.WriteNumberValue(p.Y);w.WriteNumberValue(p.Color);w.WriteEndArray();}
        w.WriteEndArray();
        w.WriteEndObject();
    }
    /// <summary>One frame object: key, bounds, optional extra properties, then blocks in tile order.</summary>
    public static void Frame(Utf8JsonWriter w,string key,SprFile file,int frame,Action<Utf8JsonWriter>? extra=null){
        var f=file.Frames[frame];
        w.WriteStartObject();
        w.WriteString("key",key);Bounds(w,f.Bounds);
        extra?.Invoke(w);
        w.WriteStartArray("blocks");
        for(int i=0;i<f.Tiles.Count;i++)Block(w,f.Tiles[i],i,file.Blocks[f.Tiles[i].BlockIndex]);
        w.WriteEndArray();
        w.WriteEndObject();
    }
    /// <summary>"frames": every file read so far, by file number, each with all its frames.</summary>
    public static void Frames(Utf8JsonWriter w,SprBody body){
        w.WriteStartArray("frames");
        foreach(int sid in body.Sids){var file=body.File(sid);for(int f=0;f<file.Frames.Count;f++)Frame(w,Key(sid,f),file,f);}
        w.WriteEndArray();
    }
    /// <summary>"sequences": action, direction and the steps (key, ticks, events; milliseconds where the rule asks for them).</summary>
    public static void Sequences(Utf8JsonWriter w,IEnumerable<SprSequence> sequences,Func<SprSequence,bool> withMilliseconds){
        w.WriteStartArray("sequences");
        foreach(var s in sequences){
            bool ms=withMilliseconds(s);
            w.WriteStartObject();
            w.WriteString("action",s.Label);w.WriteNumber("direction",s.Direction);
            w.WriteStartArray("sequence");
            for(int i=0;i<s.Keys.Count;i++){
                var step=s.Action.Steps[i];
                w.WriteStartObject();
                w.WriteString("key",s.Keys[i]);w.WriteNumber("ticks",step.Ticks);
                if(ms)w.WriteNumber("milliseconds",step.Milliseconds);
                w.WriteString("events",step.Events);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
    public static void NullableNumber(Utf8JsonWriter w,string name,int? value){
        if(value is {} v)w.WriteNumber(name,v);else w.WriteNull(name);
    }
}
