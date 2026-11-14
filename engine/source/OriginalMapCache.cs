using System.Text.Json;
using System.Security.Cryptography;
namespace P06;
public sealed record MapTile(int X,int Y,int Z,int Tile,int Index,int Offset,string Layer,int SourceIndex,int Group=-1);
public sealed record MapGroup(int Ordinal,int Id,MapTile[] Objects);
public sealed record MapTrigger(int X,int Y,int Group,int Bits,int Offset);
public sealed class OriginalMapBlock {
    public required string Name;public required string Sha256;public required int X,Y;
    public required MapTile[] Ground,Cover;public required MapGroup[] Groups;public required MapTrigger[] Triggers;public required ushort[] Attributes;
    public MapLocalEffect[] Effects=[];public long EffectsAt=-1;
    public required Dictionary<(int,int),Tile> Tiles;public int Generation;public string GroupKey(int ordinal)=>$"{Name}@{Generation}:{ordinal}";
}
public sealed class OriginalMapStore {
    CancellationToken loadToken;readonly string root;readonly Dictionary<string,JsonElement> maps;readonly Dictionary<int,JsonElement> tileManifest;readonly HashSet<string> knownMissing;
    readonly byte[] terrain;readonly bool packagedMaps;readonly MapRawSource source;MapEffectCatalog? effectCatalog;readonly HashSet<string> unsupportedBlocks=[];public int MapId {get;} public MapBounds Bounds {get;} public int DiskLoads {get;private set;}public int TileDecodes {get;private set;}
    // T1195 (#1195): .seg-only blocks the r29 manifest still lists as unsupportedBlocks with sources[].sha256 (stem → SHA-256); read from TW_CLIENT_ROOT.
    readonly Dictionary<string,string> legacySeg=new(StringComparer.Ordinal);readonly string? clientRoot;
    public OriginalMapStore(string assetsRoot,int? mapId=null,CancellationToken token=default,string? clientRoot=null){loadToken=token;loadToken.ThrowIfCancellationRequested();source=MapRawSource.Open(assetsRoot,clientRoot);this.clientRoot=clientRoot??source.ClientRoot;
        MapId=mapId??0;Bounds=new(32256,32768,32767,33279);
        if(mapId.HasValue){
            packagedMaps=true;root=Path.Combine(assetsRoot,"maps");var integrity=Json.Read(Path.Combine(assetsRoot,"integrity.json"));var pins=integrity.GetProperty("files").EnumerateArray().Where(f=>f.S("path")=="maps/manifest.json").ToArray();if(pins.Length!=1)throw new InvalidDataException("map manifest integrity entry missing");var manifestBytes=Verified(Path.Combine(root,"manifest.json"),pins[0].S("sha256"));using var document=JsonDocument.Parse(manifestBytes);var all=document.RootElement.Clone();if(all.S("schema")!="p24-map-provider-v1")throw new InvalidDataException("map provider schema");
            var available=all.GetProperty("maps").EnumerateArray().Where(m=>m.I("mapId")==MapId).ToArray();if(available.Length!=1)throw new NotSupportedException("未封裝直接地圖與地形來源: "+MapId);
            var metadata=available[0];foreach(var row in metadata.GetProperty("unsupportedBlocks").EnumerateArray()){string blockName=row.S("name");if(SegBlocks.LegacySha256(MapId,row) is string legacySha)legacySeg[blockName]=legacySha;else unsupportedBlocks.Add(blockName);}Bounds=new(metadata.I("minX"),metadata.I("minY"),metadata.I("maxX"),metadata.I("maxY"));
            maps=all.GetProperty("blocks").EnumerateArray().Where(m=>m.I("mapId")==MapId).ToDictionary(m=>m.S("name"));tileManifest=all.GetProperty("tiles").EnumerateArray().ToDictionary(t=>t.I("tile"));
            knownMissing=metadata.GetProperty("verifiedAbsent").EnumerateArray().Select(v=>v.GetString()!).ToHashSet();var source=metadata.GetProperty("terrain");terrain=Verified(Path.Combine(root,source.S("path")),source.S("sha256"));if(terrain.Length!=(long)Bounds.Width*Bounds.Height)throw new InvalidDataException("map terrain dimensions");return;
        }
        root=Path.Combine(assetsRoot,"cache");var manifest=source.Mode==MapSourceMode.Packaged?Json.Read(Path.Combine(root,"manifest.json")):CacheManifest(assetsRoot);if(manifest.S("schema")!="p24-original-cache-v1")throw new InvalidDataException("cache schema");
        maps=manifest.GetProperty("maps").EnumerateArray().ToDictionary(m=>m.S("name"));tileManifest=manifest.GetProperty("tiles").EnumerateArray().ToDictionary(t=>t.I("tile"));
        knownMissing=manifest.GetProperty("verifiedMissing").EnumerateArray().Select(v=>v.GetString()!).ToHashSet();
        var t=manifest.GetProperty("terrain");var bytes=Verified(Path.Combine(root,t.S("path")),t.S("sha256"));var rows=System.Text.Encoding.UTF8.GetString(bytes).Split('\n',StringSplitOptions.RemoveEmptyEntries);terrain=rows.SelectMany(l=>l.Split(',',StringSplitOptions.RemoveEmptyEntries).Select(x=>byte.Parse(x.Trim()))).ToArray();if(terrain.Length!=512*512)throw new InvalidDataException("Original terrain size");
    }
    JsonElement CacheManifest(string assetsRoot){var integrity=Json.Read(Path.Combine(assetsRoot,"integrity.json"));var pins=integrity.GetProperty("files").EnumerateArray().Where(f=>f.S("path")=="cache/manifest.json").ToArray();if(pins.Length!=1)throw new InvalidDataException("cache manifest integrity entry missing");using var document=JsonDocument.Parse(Verified(Path.Combine(root,"manifest.json"),pins[0].S("sha256")));return document.RootElement.Clone();}
    byte[] Raw(Func<byte[]> read){loadToken.ThrowIfCancellationRequested();var b=read();loadToken.ThrowIfCancellationRequested();return b;}
    byte[] Verified(string path,string hash){loadToken.ThrowIfCancellationRequested();var b=File.ReadAllBytes(path);loadToken.ThrowIfCancellationRequested();if(!Convert.ToHexString(SHA256.HashData(b)).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original cache source hash: "+path);return b;}
    public void EndInitialLoad(){loadToken=default;}
    public bool Available(int bx,int by)=>maps.ContainsKey($"{bx:x4}{by:x4}.s32");
    public byte Terrain(int x,int y)=>Bounds.Contains(x,y)?terrain[(y-Bounds.MinY)*Bounds.Width+x-Bounds.MinX]:(byte)0;
    public OriginalMapBlock Load(int bx,int by,int generation){loadToken.ThrowIfCancellationRequested();
        string name=$"{bx:x4}{by:x4}";if(unsupportedBlocks.Contains(name))throw new NotSupportedException("原始區塊只有尚未支援的 SEG／壓縮格式: "+MapId+"/"+name);if(knownMissing.Contains(name))return Missing(bx,by,generation);
        // T1195: blocks[] first (the .s32 wins over a .seg, as sub_F0B570 opens .s32 before .seg); then a legacy .seg-only row of the r29 manifest.
        JsonElement entry=default;bool legacy=false;string? legacySha=null;
        if(!maps.TryGetValue(name+".s32",out entry)&&!maps.TryGetValue(name+".seg",out entry)&&!(legacy=legacySeg.TryGetValue(name,out legacySha)))throw new NotSupportedException("Original block outside packaged verified coverage: "+name);
        bool seg=legacy||entry.TryGetProperty("format",out var format)&&format.GetString()=="classic-seg";string sha=legacy?legacySha!:entry.S("sha256");
        if(seg)SegBlocks.CheckAnchor(MapId,name,sha);
        var bytes=Raw(()=>legacy?SegBlocks.ReadLegacy(clientRoot,MapId,name,sha):packagedMaps?source.Block(entry.S("path"),sha):source.CacheBlock(name+".s32",sha));using var r=new BinaryReader(new MemoryStream(bytes));
        int Count(int size,bool shortCount=false){int n=shortCount?r.ReadUInt16():r.ReadInt32();if(n<0||n>65536||r.BaseStream.Position+(long)n*size>bytes.Length)throw new InvalidDataException("S32 count");return n;}
        MapTile ReadTile(int x,int y,int z,string layer,int index,int group,int offset){uint v=seg?r.ReadUInt16():r.ReadUInt32();if(v==uint.MaxValue&&layer=="ground")return new(x,y,z,140,187+(x&1),offset,layer,index,group);if((v>>24)!=0)throw new NotSupportedException("S32 tile high byte");return new(x,y,z,(int)(v>>8),(int)(v&255),offset,layer,index,group);}
        var ground=new List<MapTile>();var cover=new List<MapTile>();var groups=new List<MapGroup>();var triggers=new List<MapTrigger>();
        for(int y=0;y<64;y++)for(int x=0;x<128;x++)ground.Add(ReadTile(x,y,0,"ground",ground.Count,-1,(int)r.BaseStream.Position));
        int nc=Count(seg?4:6,true);for(int i=0;i<nc;i++){int off=(int)r.BaseStream.Position,x=r.ReadByte(),y=r.ReadByte();cover.Add(ReadTile(x,y,0,"cover",i,-1,off));}
        var attrs=Enumerable.Range(0,8192).Select(_=>seg?(ushort)r.ReadByte():r.ReadUInt16()).ToArray();int ng=Count(4);
        for(int g=0;g<ng;g++){int id=r.ReadUInt16(),n=Count(seg?5:7,true);var objs=new List<MapTile>();for(int i=0;i<n;i++){int off=(int)r.BaseStream.Position,x=r.ReadByte(),y=r.ReadByte(),z=r.ReadByte();var obj=ReadTile(x,y,z,"objects",i,g,off);if(x!=205||y!=205)objs.Add(obj);}groups.Add(new(g,id,objs.ToArray()));}
        int nt=Count(5);for(int i=0;i<nt;i++){int off=(int)r.BaseStream.Position,x=r.ReadByte(),y=r.ReadByte(),g=r.ReadUInt16(),kind=r.ReadByte();if(g>=ng)throw new InvalidDataException("trigger group");triggers.Add(new(x,y,g,1<<(kind==0?0:1),off));}
        // T1195: sub_F0B570's SEG branch reads the portal section and the effects word only while data remains (VA 0xF0D10C portal count, 0xF0D71E effects word; docs/reports/issue-1195.md).
        int refs=seg?r.ReadByte():Count(4);r.BaseStream.Position+=(seg?1:4)*refs;int portals=seg&&r.BaseStream.Position==bytes.Length?0:Count(9,true);for(int i=0;i<portals;i++){int len=r.ReadByte();if(r.BaseStream.Position+len+8>bytes.Length)throw new InvalidDataException("S32 truncated portal");if(seg){r.BaseStream.Position+=len;int px=r.ReadByte(),py=r.ReadByte();if(px>=128||py>=64)throw new InvalidDataException("SEG portal coordinates");attrs[py*128+px]|=2;r.BaseStream.Position+=6;}else r.BaseStream.Position+=len+8;}
        bool noTail=packagedMaps&&!legacy&&entry.GetProperty("noEffectTail").GetBoolean();if(noTail&&r.BaseStream.Position!=bytes.Length)throw new InvalidDataException("S32 pinned absent-L8 profile mismatch");int effects=noTail||seg&&r.BaseStream.Position==bytes.Length?0:r.ReadUInt16();var localEffects=new List<MapLocalEffect>();
        // The SEG tail has the S32 layout, but the SEG branch maps effect ids to light radii with its own table (not MapEffectLightTable); none of the 46 real .seg-only blocks has one.
        if(seg&&effects!=0)throw new NotSupportedException($"SEG 區塊 {MapId}/{name} 有 effects 尾段 0x{effects:X4}（{effects&32767} 筆），SEG 的光源表與 S32 不同，尚未支援");
        if((effects&32767)!=0){if(!packagedMaps)throw new NotSupportedException("Nonzero map-local effects require map provider: "+name);effectCatalog??=new(root);for(int i=0;i<(effects&32767);i++){int offset=(int)r.BaseStream.Position,id=r.ReadUInt16(),x=r.ReadUInt16(),y=r.ReadUInt16();int extension=(effects&32768)!=0?r.ReadInt32():0;if(extension!=0)throw new NotSupportedException("Unresolved extended map effect field: "+extension);if(x<(bx-0x7e00)*64||x>=(bx-0x7e00+1)*64||y<(by-0x7e00)*64||y>=(by-0x7e00+1)*64)throw new InvalidDataException("map effect outside owning block");localEffects.Add(new(id,x,y,offset,effectCatalog.Get(id)));}}
        if(r.BaseStream.Position!=bytes.Length)throw new NotSupportedException("Unknown S32 trailing format: "+name);
        var tiles=new Dictionary<(int,int),Tile>();foreach(var set in ground.Concat(cover).Concat(groups.SelectMany(g=>g.Objects)).GroupBy(t=>t.Tile)){
            if(!tileManifest.TryGetValue(set.Key,out var tm))throw new InvalidDataException("未封裝原始 TIL "+set.Key);byte[] raw=Raw(()=>source.Tile(set.Key,tm.S("sha256"),!packagedMaps));using var tr=new BinaryReader(new MemoryStream(raw));int count=tr.ReadInt32();if(count<=0||count>65536)throw new InvalidDataException("TIL count");int[] offsets=Enumerable.Range(0,count+1).Select(_=>tr.ReadInt32()).ToArray();long start=tr.BaseStream.Position;
            foreach(int idx in set.Select(v=>v.Index).Distinct()){loadToken.ThrowIfCancellationRequested();
                if(idx<0||idx>=count)throw new InvalidDataException("TIL index");tr.BaseStream.Position=start+offsets[idx];int typ=tr.ReadByte();var pixels=new List<Pixel>();
                if(typ is not (0 or 1 or 2 or 3 or 6 or 7 or 8 or 9 or 16 or 17 or 18 or 19))throw new NotSupportedException("Original TIL type "+typ);
                if((typ&2)==0){for(int y=0;y<24;y++){int n=y<12?2*(y+1):2*(23-y),x=(typ&1)!=0?0:24-n;for(int i=0;i<n;i++)pixels.Add(new(x+i,y,tr.ReadUInt16()));}}
                else{int x0=tr.ReadByte(),y0=tr.ReadByte(),w=tr.ReadByte(),h=tr.ReadByte();if(w==0||h==0||x0+w>24||y0+h>24)throw new InvalidDataException("TIL dimensions");for(int y=y0;y<y0+h;y++){int x=x0,n=tr.ReadByte();for(int i=0;i<n;i++){int skip=tr.ReadByte(),run=tr.ReadByte();if((skip&1)!=0||run==0)throw new InvalidDataException("TIL run");x+=skip/2;if(x+run>x0+w)throw new InvalidDataException("TIL bounds");for(int j=0;j<run;j++)pixels.Add(new(x++,y,tr.ReadUInt16()));}}}
                int end=offsets.Where(o=>o>offsets[idx]).Min();if(tr.BaseStream.Position!=start+end)throw new InvalidDataException("TIL block tail");tiles[(set.Key,idx)]=new(typ,pixels.ToArray());TileDecodes++;
            }
        }
        DiskLoads++;return new(){Name=name,Sha256=sha,X=(bx-32512)*128,Y=(by-32256)*64,Ground=ground.ToArray(),Cover=cover.ToArray(),Groups=groups.ToArray(),Triggers=triggers.ToArray(),Attributes=attrs,Tiles=tiles,Generation=generation,Effects=localEffects.ToArray()};
    }
    OriginalMapBlock Missing(int bx,int by,int generation){
        var raw=Raw(()=>source.Tile(140,tileManifest[140].S("sha256"),!packagedMaps));using var r=new BinaryReader(new MemoryStream(raw));int n=r.ReadInt32();var offsets=Enumerable.Range(0,n+1).Select(_=>r.ReadInt32()).ToArray();long start=r.BaseStream.Position;var tiles=new Dictionary<(int,int),Tile>();
        foreach(int index in new[]{187,188}){r.BaseStream.Position=start+offsets[index];int typ=r.ReadByte();if(typ!=index-187)throw new InvalidDataException("Original missing tile type");var pixels=new List<Pixel>();for(int y=0;y<24;y++){int count=y<12?2*(y+1):2*(23-y),x=typ==1?0:24-count;for(int j=0;j<count;j++)pixels.Add(new(x+j,y,r.ReadUInt16()));}tiles[(140,index)]=new(typ,pixels.ToArray());TileDecodes++;}
        DiskLoads++;return new(){Name=$"{bx:x4}{by:x4}",Sha256="verified-absent-original-four-paths",X=(bx-32512)*128,Y=(by-32256)*64,Ground=Enumerable.Range(0,8192).Select(i=>new MapTile(i%128,i/128,0,140,187+(i&1),-1,"ground",i)).ToArray(),Cover=[],Groups=[],Triggers=[],Attributes=Enumerable.Repeat((ushort)1,8192).ToArray(),Tiles=tiles,Generation=generation};
    }
}
// T1195 (#1195): SEG blocks in general, not only the map 6 anchor.
// Original BIN TW1608262200.bin (SHA-256 e12949cf…09641a2, image base 0x400000): sub_F0B570 (VA 0xF0B570, RVA 0xB0B570, FO 0xB0A970)
// formats map\%d\%04x%04x.seg (VA 0x1180E24, FO 0xD7FA24) and map\%d\%04x%04x.s32 (VA 0x1180E38, FO 0xD7FA38), opens <s32>.gz / <s32> first and
// only when both are missing <seg>.gz / <seg>. The SEG branch has no map or name check: every .seg uses one layout (u16 ground tiles, u16 cover
// count + 4-byte records, u8 attributes, i32 groups with 5-byte members, i32 × 5-byte triggers, u8 refs count + u8 refs, then the portal and effects
// sections while data remains). Identity is the SHA-256 from the manifest; the anchor pin is kept as an extra check.
public static class SegBlocks {
    /// <summary>
    /// The r29 maps/manifest.json (read-only) lists .seg-only blocks in maps[].unsupportedBlocks as {name, sources:[{path:"…\map\&lt;id&gt;\&lt;name&gt;.seg", sha256}]}.
    /// Returns that SHA-256 (lower case) when the row has exactly one source and it is the block's own .seg in this map directory; null otherwise
    /// (rows without sources, such as the ones MapsRecipe wrote before version 3, or a .s32.gz source stay unsupported).
    /// </summary>
    public static string? LegacySha256(int mapId,JsonElement row){
        if(row.ValueKind!=JsonValueKind.Object||!row.TryGetProperty("name",out var n)||n.ValueKind!=JsonValueKind.String)return null;
        string name=n.GetString()!;if(name.Length!=8||!name.All(char.IsAsciiHexDigit))return null;
        if(!row.TryGetProperty("sources",out var sources)||sources.ValueKind!=JsonValueKind.Array||sources.GetArrayLength()!=1)return null;
        var source=sources[0];
        if(source.ValueKind!=JsonValueKind.Object||!source.TryGetProperty("path",out var p)||p.ValueKind!=JsonValueKind.String||!source.TryGetProperty("sha256",out var h)||h.ValueKind!=JsonValueKind.String)return null;
        var parts=p.GetString()!.Replace('\\','/').Split('/');string sha=h.GetString()!;
        if(parts.Length<3||!parts[^1].Equals(name+".seg",StringComparison.OrdinalIgnoreCase)||parts[^2]!=mapId.ToString(System.Globalization.CultureInfo.InvariantCulture)||!parts[^3].Equals("map",StringComparison.OrdinalIgnoreCase))return null;
        if(sha.Length!=64||!sha.All(char.IsAsciiHexDigit))return null;
        return sha.ToLowerInvariant();
    }
    /// <summary>TW_CLIENT_ROOT/map/&lt;mapId&gt;/&lt;name&gt;.seg (file name case-insensitive), refused unless its SHA-256 equals <paramref name="sha256"/>. Null root → TW_CLIENT_ROOT.</summary>
    public static byte[] ReadLegacy(string? clientRoot,int mapId,string name,string sha256){
        string root=clientRoot??NativeUi.UiResourcePins.ClientRoot;
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root))throw new DirectoryNotFoundException("SEG 區塊需要原客戶端目錄（TW_CLIENT_ROOT，絕對路徑）");
        if(!Directory.Exists(root))throw new DirectoryNotFoundException("原客戶端目錄不存在："+root);
        if(name.Length!=8||!name.All(char.IsAsciiHexDigit))throw new InvalidDataException("SEG 區塊名稱不合法："+name);
        string dir=mapId.ToString(System.Globalization.CultureInfo.InvariantCulture),relative=$"map/{dir}/{name}.seg";
        string file=MapRawSource.FindClientFile(Path.Combine(root,"map",dir),name+".seg")??throw new FileNotFoundException("原客戶端缺少 SEG 區塊 "+relative,relative);
        return MapRawSource.Verify(File.ReadAllBytes(file),sha256,"TW_CLIENT_ROOT/"+relative);
    }
    /// <summary>The map 6 anchor (asset-recipe.md §1.2) must keep its pinned SHA-256; other SEG blocks are identified by the manifest SHA alone.</summary>
    public static void CheckAnchor(int mapId,string name,string sha256){
        if(mapId==AssetPrep.MapScan.SegAnchorMap&&name==AssetPrep.MapScan.SegAnchorStem&&!sha256.Equals(AssetPrep.MapScan.SegAnchorSha256,StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("Unverified SEG profile");
    }
}
public sealed class OriginalMapCache {
    public event Action<OriginalMapBlock>? Evicting;
    public static readonly int[] SourceA=[0,1,1,3,2,2,0,0],SourceB=[1,0,3,1,3,0,2,1],DestA=[2,2,0,0,0,1,1,3],DestB=[3,3,2,2,1,3,3,2],ShiftX=[0,128,128,128,0,-128,-128,-128],ShiftY=[-64,-64,0,64,64,64,0,-64];
    readonly OriginalMapStore store;readonly Dictionary<(int,int),List<Draw>> cells=[];readonly Dictionary<(int,int),List<OpacityTrigger>> triggers=[];
    readonly Dictionary<int,(int Tile,int Index,string Source)> identities=[];int serial,generation;public int OriginX{get;private set;}public int OriginY{get;private set;}public int Revision{get;private set;}public int? LastDirection{get;private set;}
    public OriginalMapBlock?[] Slots {get;}=new OriginalMapBlock?[4];public IReadOnlyList<Draw> Scene{get;private set;}=[];
    public OpacityCatalog Catalog{get;private set;}=null!;public List<object> Events{get;}=[];
    public void ActivateEffects(long now){foreach(var block in Slots.OfType<OriginalMapBlock>())if(block.EffectsAt<0)block.EffectsAt=now;}
    public int MapId=>store.MapId;public MapBounds Bounds=>store.Bounds;
    public OriginalMapCache(string root,int x,int y,int? mapId=null,CancellationToken token=default,string? clientRoot=null){store=new(root,mapId,token,clientRoot);if(mapId.HasValue&&!store.Bounds.Contains(x,y))throw new InvalidDataException("角色座標超出地圖範圍");Cold(x,y);token.ThrowIfCancellationRequested();store.EndInitialLoad();}
    public static (int,int) ColdOrigin(int x,int y)=>(((2*x-32768+4-46)/128)*128,((y-22)/64)*64);
    public static int? Direction(int ox,int oy,int x,int y){int cx=2*x-32768+4,cy=y-22;if(cx-6>=ox+40&&cy>=oy&&cx+34<ox+256&&cy+43<oy+128)return null;cx-=6;if(cy+43>=oy+128)cy+=3;return cy<oy?(cx<ox+40?7:cx+40>=ox+256?1:0):cy+40>=oy+128?(cx<ox+40?5:cx+40>=ox+256?3:4):cx<ox+40?6:cx+40>=ox+256?2:throw new InvalidOperationException("Caller admitted undefined original ECX branch");}
    (int X,int Y) SlotOrigin(int s)=>(OriginX+(s&1)*128,OriginY+(s>>1)*64);
    public bool Contains(int x,int y)=>2*x-32768>=OriginX&&2*x-32768<OriginX+256&&y>=OriginY&&y<OriginY+128;
    public bool Passable(int x,int y,int h){if(h is <0 or >7)return false;int nx=x+MovementRules.Dx[h],ny=y+MovementRules.Dy[h];if(!Contains(x,y)||!Contains(nx,ny))return false;int a=store.Terrain(x,y),v=store.Terrain(nx,ny);return v!=0&&(v&128)==0&&(v&3)!=0&&(h switch{0=>(a&2)!=0,2=>(a&1)!=0,4=>(v&2)!=0,6=>(v&1)!=0,_=>(v&3)!=0});}
    void Cold(int x,int y){(OriginX,OriginY)=ColdOrigin(x,y);for(int s=3;s>=0;s--)Load(s);Publish();Events.Add(new{kind="cold",x,y,origin=new[]{OriginX,OriginY},order=new[]{3,2,1,0}});}
    void ClearRectangle(int s){var p=SlotOrigin(s);foreach(var k in cells.Keys.Where(k=>k.Item1>=p.X&&k.Item1<p.X+128&&k.Item2>=p.Y&&k.Item2<p.Y+64).ToArray())cells.Remove(k);foreach(var k in triggers.Keys.Where(k=>k.Item1>=p.X&&k.Item1<p.X+128&&k.Item2>=p.Y&&k.Item2<p.Y+64).ToArray())triggers.Remove(k);}
    void Insert(OriginalMapBlock b,MapTile t){var key=(b.X+t.X,b.Y+t.Y);if(!cells.TryGetValue(key,out var list))cells[key]=list=[];int id=++serial;var tile=b.Tiles[(t.Tile,t.Index)];var draw=new Draw(key.Item1-(2*Coordinates.MinX-32768),key.Item2-Coordinates.MinY,t.Z,id,t.Layer,t.SourceIndex,t.Group,t.Offset,tile);identities[id]=(t.Tile,t.Index,$"{b.Name}:{t.Layer}:{t.Offset}");if(t.Layer=="ground"){list.RemoveAll(c=>c.Layer=="ground");list.Insert(0,draw);}else list.Add(draw);}
    void Trigger(OriginalMapBlock b,MapTrigger t){var key=(b.X+t.X,b.Y+t.Y);if(!triggers.TryGetValue(key,out var list))triggers[key]=list=[];list.Add(new(b.GroupKey(t.Group),t.Bits,t.Offset));}
    void Load(int s){var p=SlotOrigin(s);var b=store.Load(p.X/128+32512,p.Y/64+32256,++generation);Slots[s]=b;int maxX=(s&1)==0?256:128,maxY=s<2?128:64;
        foreach(var t in b.Ground)Insert(b,t);foreach(var t in b.Triggers)if(t.X<maxX&&t.Y<maxY)Trigger(b,t);
        foreach(var t in b.Cover){var tile=b.Tiles[(t.Tile,t.Index)];Insert(b,(tile.Type&2)!=0?t:t with{Layer="ground"});}
        foreach(var t in b.Groups.SelectMany(g=>g.Objects))if(t.X<maxX&&t.Y<maxY)Insert(b,t);
        Events.Add(new{kind="load",slot=s,block=b.Name,source=b.Sha256,generation=b.Generation});
    }
    void RebuildBaseTriggers(int s){var b=Slots[s]!;var p=SlotOrigin(s);foreach(var k in triggers.Keys.Where(k=>k.Item1>=p.X&&k.Item1<p.X+128&&k.Item2>=p.Y&&k.Item2<p.Y+64).ToArray())triggers.Remove(k);foreach(var t in b.Triggers)if(t.X<128&&t.Y<64)Trigger(b,t);}
    void Reinsert(int s){var b=Slots[s]!;int mx=(s&1)==0?256:128,my=s<2?128:64;foreach(var t in b.Groups.SelectMany(g=>g.Objects))if((t.X>=128||t.Y>=64)&&t.X<mx&&t.Y<my)Insert(b,t);foreach(var t in b.Triggers)if((t.X>=128||t.Y>=64)&&t.X<mx&&t.Y<my)Trigger(b,t);}
    public bool Move(int x,int y){LastDirection=Direction(OriginX,OriginY,x,y);if(LastDirection is not int d)return false;Shift(d);return true;}
    public void Shift(int d){if(d is <0 or >7)throw new ArgumentOutOfRangeException();int a=SourceA[d],b=SourceB[d],c=DestA[d],e=DestB[d];var before=new[]{OriginX,OriginY};int[] evicted=(d&1)!=0?[c,b,e]:[c,e];foreach(int s in evicted){Evicting?.Invoke(Slots[s]!);Events.Add(new{kind="evict",slot=s,block=Slots[s]!.Name,generation=Slots[s]!.Generation});ClearRectangle(s);Slots[s]=null;}
        Slots[c]=Slots[a];Slots[a]=null;if((d&1)==0){Slots[e]=Slots[b];Slots[b]=null;}OriginX+=ShiftX[d];OriginY+=ShiftY[d];ClearRectangle(a);ClearRectangle(b);if((d&1)!=0)ClearRectangle(e);
        if((d&1)!=0&&d!=7){RebuildBaseTriggers(c);Reinsert(c);}else if(d is 2 or 4){RebuildBaseTriggers(c);RebuildBaseTriggers(e);Reinsert(c);Reinsert(e);}
        Load(a);Load(b);if((d&1)!=0)Load(e);Publish();Events.Add(new{kind="shift",direction=d,before,after=new[]{OriginX,OriginY},revision=Revision});
    }
    void Publish(){
        Scene=cells.Values.SelectMany(v=>v).OrderBy(c=>c,DrawComparer.Instance).ToArray();var nodes=Scene.Where(c=>c.Layer!="ground").Select(c=>new OpacityNode(c.Layer,c.Serial,(c.Tile.Type&16)!=0?8:(c.Tile.Type&4)!=0?256:(c.Tile.Type&32)!=0?128:0)).ToArray();
        var drawsBySerial=Scene.ToDictionary(c=>c.Serial);var first=new Dictionary<(int,int,int,int),int>();for(int i=0;i<nodes.Length;i++){var n=nodes[i];var draw=drawsBySerial[n.Serial];var t=identities[n.Serial];first.TryAdd((draw.X,draw.Y,t.Tile,t.Index),i);}
        var groups=new List<OpacityGroup>();foreach(var b in Slots.OfType<OriginalMapBlock>())foreach(var g in b.Groups){var ns=g.Objects.Select(t=>first.TryGetValue((b.X+t.X-(2*Coordinates.MinX-32768),b.Y+t.Y-Coordinates.MinY,t.Tile,t.Index),out int i)?i:-1).Where(i=>i>=0).ToArray();groups.Add(new(b.GroupKey(g.Ordinal),b.Name,g.Ordinal,g.Id,0,ns));}
        Catalog=new(nodes,groups,triggers.ToDictionary(p=>(p.Key.Item1-(2*Coordinates.MinX-32768),p.Key.Item2-Coordinates.MinY),p=>p.Value.ToArray()));Revision++;
        var live=Scene.Select(c=>c.Serial).ToHashSet();foreach(int id in identities.Keys.Where(k=>!live.Contains(k)).ToArray())identities.Remove(id);
    }
    public object Snapshot()=>new{mapId=MapId,bounds=Bounds,origin=new[]{OriginX,OriginY},revision=Revision,lastDirection=LastDirection,slots=Slots.Select((b,i)=>new{slot=i,block=b?.Name,sha256=b?.Sha256,generation=b?.Generation,effects=b?.Effects.Select(e=>new{e.Id,e.X,e.Y,e.Offset}).ToArray(),effectsAt=b?.EffectsAt}).ToArray(),nodes=Scene.Count,loads=store.DiskLoads,tileDecodes=store.TileDecodes,events=Events.ToArray()};
    public object[] SourceNodes()=>Scene.Select(c=>new{c.X,c.Y,c.Z,c.Serial,c.Layer,source=identities[c.Serial].Source,tile=identities[c.Serial].Tile,index=identities[c.Serial].Index}).Cast<object>().ToArray();
    public (int Tile,int Index,string Source)? Identity(Draw c)=>identities.TryGetValue(c.Serial,out var v)&&!c.Actor?v:null;
}

