using P06;
using P06.AssetPrep;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// T458（#1043）：recipe terrain 的離線測試。規則：docs/reference/asset-recipes/terrain.md（T445）。
// 所有 S32／SEG 與 Tile.idx／Tile.pak 都由本檔合成，寫在 scratch/issue-1043/run-<時間>/ 下；
// 不讀任何客戶端、不需要遊戲、網路或 DB。測試本身不做遞迴刪除。
// 群組 terrain-recipe；同時掛在 map-provider、map-continuity 與預設群組之下。
partial class Checks {
    /// <summary>合成 S32：ground 全 0、cover 筆數 cover（測 attribute 位移）、attribute 由 set 指定（預設全 0）、其餘區段為空、沒有 effects tail 以外的資料。</summary>
    static byte[] TrS32(Action<ushort[]>? set=null,int cover=2,int trailing=0){
        var attr=new ushort[8192];set?.Invoke(attr);
        using var s=new MemoryStream();using var b=new BinaryWriter(s);
        for(int i=0;i<8192;i++)b.Write(0u);
        b.Write((ushort)cover);for(int i=0;i<cover;i++){b.Write((byte)1);b.Write((byte)2);b.Write(0xFFFFFFFFu);}
        foreach(ushort v in attr)b.Write(v);
        b.Write(0);b.Write(0);b.Write(0);b.Write((ushort)0);b.Write((ushort)0);
        for(int i=0;i<trailing;i++)b.Write((byte)0xEE);
        return s.ToArray();
    }
    /// <summary>合成 SEG：ground 全 0、cover 1 筆、attribute 8192 × u8（由 set 指定）。只到 attribute 層為止。</summary>
    static byte[] TrSeg(Action<byte[]>? set=null){
        var attr=new byte[8192];set?.Invoke(attr);
        using var s=new MemoryStream();using var b=new BinaryWriter(s);
        for(int i=0;i<8192;i++)b.Write((ushort)0);
        b.Write((ushort)1);b.Write(0x01020304u);
        b.Write(attr);
        return s.ToArray();
    }
    /// <summary>T1195：完整的合成 SEG（TrSeg 之後補 groups 0、triggers 0、refs 0、portals 0；沒有 effects 尾段）。.seg-only 區塊要能完整讀完。</summary>
    static byte[] TrSegFull(Action<byte[]>? set=null)=>[..TrSeg(set),0,0,0,0,0,0,0,0,0,0,0];
    // attribute 陣列 64 列 × 128 欄：L(lx,ly) 在偶數欄、R 在奇數欄。
    static void TrL<T>(T[] a,int lx,int ly,T v)=>a[ly*128+2*lx]=v;
    static void TrR<T>(T[] a,int lx,int ly,T v)=>a[ly*128+2*lx+1]=v;

    [CheckGroup("terrain-recipe",Order=1),CheckGroup("map-provider",Order=2),CheckGroup("map-continuity",Order=301),CheckGroup(CheckGroups.Default)]
    static void TerrainRecipeChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1043","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        var runner=AssetPrepRunner.Product();
        string Out(string engine,string relative)=>Path.Combine(engine,"assets",TerrainRecipe.RecipeId,relative.Replace('/',Path.DirectorySeparatorChar));
        AssetPrepRequest Request(string client,string engine,params string[] items)=>new(client,engine,TerrainRecipe.RecipeId,items);

        // --- 合成客戶端。
        // map 9：bounds x 0–191、y 0–127。
        //   7e007e00.s32  (x 0–63,    y 0–63)    規則用的格子
        //   7e017e00.S32  (x 64–127,  y 0–63)    大寫副檔名；跨區塊的 A
        //   7e007e01.seg  (x 0–63,    y 64–127)  只有 .seg：要讀（跨區塊的 D）；T1195 起是完整 SEG（classic-seg 區塊）
        //   7e017e01      (x 64–127,  y 64–127)  沒有檔案：L = R = 1
        //   7e027e00.s32 ＋ .seg (x 128–191, y 0–63)  兩種都有：用 .s32（全部阻擋），.seg 全部可走
        // map 0：bx 7ffc–7ffe × by 8001–8004，全部可走，7ffd8002 的 (5,7) 是 0x40。
        // map 11：attribute 層截斷；map 12：.seg-only 截斷；map 13：effects tail 之後有多餘資料（產品讀不了）。
        byte[] p=TrS32(a=>{
            TrL(a,20,10,(ushort)1);
            TrL(a,30,10,(ushort)1);TrL(a,30,11,(ushort)1);
            TrR(a,40,10,(ushort)1);
            TrR(a,49,10,(ushort)1);TrR(a,50,10,(ushort)1);
            TrL(a,10,20,(ushort)0x40);TrL(a,12,20,(ushort)0x04);TrL(a,14,20,(ushort)0x08);TrL(a,16,20,(ushort)0x48);
            TrL(a,18,20,(ushort)0x60);TrL(a,22,20,(ushort)0x20);TrL(a,24,20,(ushort)0x41);TrL(a,26,20,(ushort)0x02);TrL(a,28,20,(ushort)0x0140);
            TrR(a,63,30,(ushort)1);
            TrL(a,5,63,(ushort)1);
        });
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal){
            ["map/9/7e007e00.s32"]=p,
            ["map/9/7e017e00.S32"]=TrS32(a=>TrR(a,0,30,(ushort)1),cover:0),
            ["map/9/7e007e01.seg"]=TrSegFull(a=>TrL(a,5,0,(byte)1)),
            ["map/9/7e027e00.s32"]=TrS32(a=>Array.Fill(a,(ushort)1)),
            ["map/9/7e027e00.seg"]=TrSeg(),
            ["map/9/tempseg.s32"]=[1],
            ["map/11/7e007e00.s32"]=TrS32()[..40000],
            ["map/12/7e007e00.s32"]=TrS32(),
            ["map/12/7e007e01.seg"]=TrSeg()[..9000],
            ["map/13/7e007e00.s32"]=TrS32(trailing:2),
        };
        foreach(int bx in Enumerable.Range(0x7ffc,3))foreach(int by in Enumerable.Range(0x8001,4))
            files[$"map/0/{bx:x4}{by:x4}.s32"]=bx==0x7ffd&&by==0x8002?TrS32(a=>TrL(a,5,7,(ushort)0x40)):TrS32();
        string MakeClient(string dir,Action<Dictionary<string,byte[]>>? change=null){
            var f=new Dictionary<string,byte[]>(files,StringComparer.Ordinal);change?.Invoke(f);
            foreach(var (path,data) in f){string full=Path.Combine(dir,path.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllBytes(full,data);}
            Archive(dir,"Tile",("140.til",MpTil(189,0x7C00,true)));
            return dir;
        }
        string client=MakeClient(Case("client"));string clientBefore=ApTreeHash(client);

        // --- 0. 自動發現、項目。
        Check(AssetRecipeDiscovery.Product.Errors.Count==0&&AssetRecipeDiscovery.Product.Recipes.Any(r=>r is TerrainRecipe{Coverage:AssetRecipeCoverage.Complete,Sources:AssetPrepSources.MapFiles}),"TR terrain recipe discovered, reads map/ only");
        ApRefused(()=>runner.Run(Request(client,Case("e"),"9")),AssetPrepFailure.InvalidRequest,"TR unknown item refused");

        // --- 1. 兩個輸出目錄逐 byte 相同、不含絕對路徑。
        string engineA=Path.Combine(Case("two-dirs"),"引擎 甲"),engineB=Path.Combine(Case("two-dirs"),"engine-b");
        var runA=runner.Run(Request(client,engineA,"terrain0","maps","original-map0"));
        var runB=runner.Run(Request(client,engineB,"original-map0","maps","terrain0","maps"));
        Check(runA.Outcome==AssetPrepOutcome.Published&&runB.Outcome==AssetPrepOutcome.Published&&runA.Produced.SequenceEqual(["maps","original-map0","terrain0"]),"TR both directories published, items maps, original-map0, terrain0");
        string[] outputs=["maps/terrain/0.bin","maps/terrain/9.bin",TerrainRecipe.Terrain0Path,TerrainRecipe.CacheTextPath,AssetReceipt.FileName];
        foreach(string rel in outputs)Check(File.ReadAllBytes(Out(engineA,rel)).AsSpan().SequenceEqual(File.ReadAllBytes(Out(engineB,rel))),"TR byte-identical in both directories: "+rel);
        string receiptText=File.ReadAllText(Out(engineA,AssetReceipt.FileName));
        Check(!receiptText.Contains(":\\",StringComparison.Ordinal)&&!receiptText.Contains("引擎",StringComparison.Ordinal)&&!receiptText.Contains(root,StringComparison.OrdinalIgnoreCase),"TR no absolute path in the receipt");
        Check(ApNames(Path.Combine(engineA,"assets",TerrainRecipe.RecipeId,"maps","terrain")).SequenceEqual(["0.bin","9.bin"]),"TR maps/terrain holds 0.bin and 9.bin only (11, 12, 13 unsupported: no file, never all walkable)");
        Check(Directory.GetFiles(Path.Combine(engineA,"assets"),"*.s32",SearchOption.AllDirectories).Length==0,"TR no S32 copied");

        // --- 2. map 9：規則逐條。
        byte[] t9=File.ReadAllBytes(Out(engineA,"maps/terrain/9.bin"));
        var bounds9=new MapBounds(0,0,191,127);
        Check(t9.Length==bounds9.Width*bounds9.Height,"TR map 9 size = 192 × 128 (bounds include the .seg-only block and the gap): "+t9.Length);
        int T(int x,int y)=>t9[y*192+x];
        void Cell(int x,int y,int expected,string why)=>Check(T(x,y)==expected,$"TR ({x},{y}) = {expected}: {why} — got {T(x,y)}");
        Cell(10,10,15,"all four attributes walkable → bits 0–3");
        Cell(20,10,7,"C = L(x,y) blocked, D walkable → bit1 set, bit3 clear");
        Cell(20,9,7,"D = L(x,y+1) blocked → bit1 set, bit3 clear");
        Cell(30,10,5,"C and D blocked → bit1 and bit3 clear");
        Cell(40,10,11,"B = R(x,y) blocked → bit0 set, bit2 clear");
        Cell(41,10,11,"A = R(x−1,y) blocked → bit0 set, bit2 clear");
        Cell(50,10,10,"A and B blocked → bit0 and bit2 clear");
        Cell(10,20,31,"L = 0x40 → zone 16");
        Cell(12,20,31,"L & 0x04 → zone 16");
        Cell(14,20,47,"L & 0x08 → zone 32");
        Cell(16,20,31,"L = 0x48 → zone 16 wins over 32");
        Cell(18,20,15,"L = 0x60 → no zone ((L & 0xF0) != 0x40)");
        Cell(22,20,15,"L = 0x20 → no zone");
        Cell(24,20,23,"L = 0x41: bit 0 blocks C (passage 7) and zone 16");
        Cell(26,20,15,"L = 0x02: only bit 0 of the attribute is a blocker");
        Cell(28,20,31,"S32 u16 attribute 0x0140: zone looks at the low byte");
        Cell(64,30,10,"A = R(63,y) in the neighbour block (.S32 upper-case read)");
        Cell(5,63,5,"D = L(x,64) in the .seg-only block (u8 attributes after the SEG cover)");
        Cell(100,100,0,"cell without a block: L = R = 1 → 0");
        Cell(64,100,1,"cell without a block next to a block: A walkable → bit0 (not forced to 0)");
        Cell(100,63,7,"D in a missing block counts as blocked");
        Cell(150,30,0,"block with .s32 and .seg uses the .s32 (blocked); the .seg would give 15");
        foreach(var (x,y) in new[]{(0,10),(10,0),(191,30),(10,127),(0,0),(191,127)})Cell(x,y,0,"outer ring is 0");
        Cell(1,10,15,"next to the outer ring is not zeroed");
        Check(t9.All(v=>(v&0xC0)==0),"TR bit6–7 never set");

        // --- 3. map 0 → terrain0.bin 與 cache/original-map0.txt。
        var bounds0=new MapBounds(32512,32832,32703,33087);
        byte[] t0=File.ReadAllBytes(Out(engineA,"maps/terrain/0.bin"));
        Check(t0.Length==bounds0.Width*bounds0.Height&&t0[(32903-32832)*192+32581-32512]==31,"TR map 0: 192 × 256, marker (32581,32903) = 31");
        byte[] w0=File.ReadAllBytes(Out(engineA,TerrainRecipe.Terrain0Path));
        Check(w0.Length==8192,"TR terrain0.bin: 64 × 128 = 8,192 bytes");
        bool sameWindow=true;for(int y=32896;y<=33023;y++)for(int x=32576;x<=32639;x++)if(w0[(y-32896)*64+x-32576]!=t0[(y-bounds0.MinY)*bounds0.Width+x-bounds0.MinX])sameWindow=false;
        Check(sameWindow&&w0[7*64+5]==31&&w0.Count(v=>v==15)==8191,"TR terrain0.bin = map 0 terrain x 32576–32639, y 32896–33023");
        byte[] txt=File.ReadAllBytes(Out(engineA,TerrainRecipe.CacheTextPath));
        string text=Encoding.ASCII.GetString(txt);var rows=text.Split("\r\n");
        Check(text.EndsWith("\r\n",StringComparison.Ordinal)&&rows.Length==513&&rows[^1]==""&&rows[..^1].All(r=>r.Split(',').Length==512&&!r.EndsWith(',')&&!r.Contains('\n')),"TR original-map0.txt: 512 CRLF rows (last one too) of 512 comma-separated values, no trailing comma");
        int V(int x,int y)=>int.Parse(rows[y-32768].Split(',')[x-32256],System.Globalization.CultureInfo.InvariantCulture);
        Check(V(32581,32903)==31&&V(32513,32900)==15&&V(32512,32900)==0&&V(32300,32800)==0&&V(32767,33279)==0,"TR original-map0.txt: origin 32256,32768; map 0 values, outer ring and cells outside the bounds are 0");

        // --- 4. 收據。
        var receipt=AssetReceipt.TryLoad(Out(engineA,AssetReceipt.FileName))??throw new Exception("TR receipt does not load");
        var mapsItem=receipt.Completed.Single(c=>c.Item=="maps");
        Check(receipt.Status==AssetReceipt.Complete&&receipt.Inputs.Count==0,"TR receipt complete, no container inputs (map/ files only)");
        Check(mapsItem.Metadata["maps"]=="2"&&mapsItem.Metadata["unsupportedMaps"]=="11,12,13"&&mapsItem.Metadata["zoneLimitMaps"]=="0","TR receipt metadata: 2 maps, unsupported 11,12,13, known limit L3 maps produced: 0 — "+string.Join(";",mapsItem.Metadata.Select(m=>m.Key+"="+m.Value)));
        Check(mapsItem.Sources.Any(s=>s is {Container:"map",Entry:"map/9/7e007e01.seg"}&&s.Sha256==Sha(files["map/9/7e007e01.seg"]))&&mapsItem.Sources.Any(s=>s is {Container:"map",Entry:"map/9/7e017e00.S32"}),"TR receipt sources: client files with SHA-256 (the .seg-only block, original case)");
        Check(runner.Run(Request(client,engineA,"terrain0","maps","original-map0")).Outcome==AssetPrepOutcome.UpToDate,"TR unchanged client: up to date");

        // --- 5. maps recipe：terrain 欄位與 terrain recipe 一致；不支援的地圖同一份清單。
        string engineM=Case("maps-recipe");
        Check(runner.Run(new(client,engineM,MapsRecipe.RecipeId,[MapsRecipe.MapsItem])).Outcome==AssetPrepOutcome.Published,"TR maps recipe published for the same client");
        byte[] mapsBytes=File.ReadAllBytes(Path.Combine(engineM,"assets",MapsRecipe.RecipeId,"maps","manifest.json"));
        var manifest=JsonNode.Parse(mapsBytes)!;
        var rowsM=manifest["maps"]!.AsArray();
        Check(rowsM.Select(m=>m!["mapId"]!.GetValue<int>()).SequenceEqual([0,9]),"TR maps manifest lists maps 0 and 9 only");
        foreach(var m in rowsM){
            int id=m!["mapId"]!.GetValue<int>();var t=m["terrain"]!;
            Check(t.AsObject().Select(k=>k.Key).SequenceEqual(["path","sha256"])&&t["path"]!.GetValue<string>()==$"terrain/{id}.bin"&&t["sha256"]!.GetValue<string>()==Sha(File.ReadAllBytes(Out(engineA,$"maps/terrain/{id}.bin"))),$"TR maps manifest terrain of map {id}: path and SHA-256 of the terrain recipe's file (no sourcePath)");
        }
        Check(rowsM[1]!["minX"]!.GetValue<int>()==0&&rowsM[1]!["maxX"]!.GetValue<int>()==191&&rowsM[1]!["minY"]!.GetValue<int>()==0&&rowsM[1]!["maxY"]!.GetValue<int>()==127,"TR maps manifest bounds of map 9 match the terrain size");
        var um=manifest["unsupportedMaps"]!.AsArray();
        Check(um.Select(u=>u!["mapId"]!.GetValue<int>()).SequenceEqual([11,12,13])&&um.All(u=>u!["reason"]!.GetValue<string>().Contains("無法解析",StringComparison.Ordinal)),"TR maps manifest unsupportedMaps 11 (truncated attribute layer), 12 (truncated .seg-only), 13 (trailing data) — "+um.ToJsonString());
        Check(um[1]!["reason"]!.GetValue<string>().Contains("7e007e01.seg",StringComparison.Ordinal),"TR map 12 reason names the .seg-only block");

        // --- 6. 產品讀取點：準備好的樹（integrity v2）裡，不支援的地圖明確失敗；支援的地圖載入 terrain。
        string prepared=Case("assets-v2");
        void Save(string rel,byte[] data){string full=Path.Combine(prepared,rel.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllBytes(full,data);}
        Save("integrity.json",Encoding.UTF8.GetBytes(MpIntegrity(MapRawSource.IntegrityV2,("maps/manifest.json",mapsBytes))));
        Save("maps/manifest.json",mapsBytes);
        foreach(string id in new[]{"0","9"})Save($"maps/terrain/{id}.bin",File.ReadAllBytes(Out(engineA,$"maps/terrain/{id}.bin")));
        foreach(int id in new[]{11,12,13})MpThrows<NotSupportedException>(()=>new OriginalMapCache(prepared,32,32,id,default,client),$"TR product: unsupported map {id} refused (not all walkable)");
        var store9=new OriginalMapStore(prepared,9,default,client);
        Check(store9.Bounds==bounds9&&store9.Terrain(20,10)==7&&store9.Terrain(14,20)==47&&store9.Terrain(100,100)==0&&store9.Terrain(-1,10)==0,"TR product: OriginalMapStore loads the prepared terrain of map 9 (bounds, values, 0 outside)");
        Save("maps/terrain/9.bin",new byte[t9.Length]);
        MpThrows<InvalidDataException>(()=>new OriginalMapStore(prepared,9,default,client),"TR product: terrain file not matching the manifest SHA-256 refused");

        // --- 7. 拒絕：map 0 沒有或不支援時，terrain0／original-map0 不產生。
        ApRefused(()=>runner.Run(Request(MakeClient(Case("no-map0"),f=>{foreach(var k in f.Keys.Where(k=>k.StartsWith("map/0/",StringComparison.Ordinal)).ToList())f.Remove(k);}),Case("e"),"terrain0")),AssetPrepFailure.MissingEntry,"TR terrain0 without map 0 refused","map");
        ApRefused(()=>runner.Run(Request(MakeClient(Case("bad-map0"),f=>f["map/0/7ffd8003.s32"]=TrS32()[..1000]),Case("e"),"original-map0")),AssetPrepFailure.Unavailable,"TR original-map0 with an unreadable map 0 block refused","map");
        Check(ApTreeHash(client)==clientBefore,"TR the client tree is unchanged by the runs (read-only)");

        // --- 8. 純函式：attribute 層位置與截斷。
        var attrs=TerrainRules.Attributes(TrS32(a=>TrR(a,63,63,(ushort)0xBEEF),cover:3),false);
        Check(attrs[8191]==0xBEEF&&attrs.Take(8191).All(v=>v==0),"TR S32 attribute layer after ground (8192 × u32), u16 n and n × 6 cover bytes");
        var segAttrs=TerrainRules.Attributes(TrSeg(a=>a[1]=0x41),true);
        Check(segAttrs[1]==0x41&&segAttrs.Length==8192,"TR SEG attribute layer after ground (8192 × u16), u16 n and n × 4 cover bytes (u8 values)");
        MpThrows<InvalidDataException>(()=>TerrainRules.Attributes(new byte[100],false),"TR S32 shorter than the ground layer refused");
        MpThrows<InvalidDataException>(()=>TerrainRules.Attributes(TrSeg()[..^1],true),"TR SEG one byte short of the attribute layer refused");
        Check(TerrainRules.Text([1,2,3,40,5,255],3).AsSpan().SequenceEqual("1,2,3\r\n40,5,255\r\n"u8),"TR text format: decimal, comma, CRLF on every row");
        Check(TerrainRules.Build(new MapBounds(0,0,63,63),new Dictionary<(int,int),ushort[]>()).All(v=>v==0),"TR map without any readable attribute: every cell blocked, never walkable");
    }
}
