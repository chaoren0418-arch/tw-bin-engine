using P06;
using P06.AssetPrep;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// T456（#1041）：地圖 provider（MapRawSource）與 recipe maps 的離線測試。
// 所有 S32／SEG／TIL／LAC 與 Tile.idx／Tile.pak 都由本檔合成，寫在 scratch/issue-1041/run-<時間>/ 下；
// 不讀任何客戶端、不需要遊戲、網路或 DB。測試本身不做遞迴刪除。
// 群組 map-provider；同時掛在 CI 已在跑的 map-continuity 與 native-ui-resources 之下。
partial class Checks {
    sealed record MpClient(string Root,Dictionary<string,byte[]> Files,Dictionary<string,byte[]> Tile);

    /// <summary>合成 S32：ground 全部同一值、可選 cover／object／portal、effects tail（-1 = 沒有 tail）、結尾多餘 bytes。</summary>
    static byte[] MpS32(uint ground=256u,uint? firstGround=null,int coverTile=-1,int objectTile=-1,(byte X,byte Y)? portal=null,int effects=0,int trailing=0,int bx=0x7e00,int by=0x7e00){
        using var s=new MemoryStream();using var b=new BinaryWriter(s);
        for(int i=0;i<8192;i++)b.Write(i==0&&firstGround is uint g?g:ground);
        if(coverTile>=0){b.Write((ushort)1);b.Write((byte)3);b.Write((byte)4);b.Write((uint)coverTile<<8);}else b.Write((ushort)0);
        for(int i=0;i<8192;i++)b.Write((ushort)3);
        if(objectTile>=0){b.Write(1);b.Write((ushort)7);b.Write((ushort)1);b.Write((byte)5);b.Write((byte)6);b.Write((byte)0);b.Write((uint)objectTile<<8);}else b.Write(0);
        b.Write(0);b.Write(0);
        if(portal is var (px,py)){b.Write((ushort)1);b.Write((byte)3);b.Write("ABC"u8);b.Write(px);b.Write(py);b.Write((ushort)0);b.Write(1);}else b.Write((ushort)0);
        if(effects>=0){b.Write((ushort)effects);for(int i=0;i<effects;i++){b.Write((ushort)(100+i));b.Write((ushort)((bx-0x7e00)*64));b.Write((ushort)((by-0x7e00)*64));}}
        for(int i=0;i<trailing;i++)b.Write((byte)0xEE);
        return s.ToArray();
    }
    /// <summary>合成 TIL：每塊都是 type 2 的 1×1 像素；diamonds 時 187／188 是 type 0／1 的完整菱形（verified-absent 區塊用）。</summary>
    static byte[] MpTil(int count,ushort color,bool diamonds=false){
        var blocks=new List<byte[]>();
        for(int i=0;i<count;i++){
            if(diamonds&&i is 187 or 188){
                var d=new List<byte>{(byte)(i-187)};
                for(int y=0;y<24;y++){int n=y<12?2*(y+1):2*(23-y);for(int j=0;j<n;j++){d.Add((byte)color);d.Add((byte)(color>>8));}}
                blocks.Add(d.ToArray());
            }else blocks.Add([2,0,0,1,1,1,0,1,(byte)color,(byte)(color>>8)]);
        }
        using var s=new MemoryStream();using var b=new BinaryWriter(s);b.Write(count);int at=0;
        foreach(var block in blocks){b.Write(at);at+=block.Length;}b.Write(at);foreach(var block in blocks)b.Write(block);
        return s.ToArray();
    }
    static string MpStem(int bx,int by)=>$"{bx:x4}{by:x4}";
    /// <summary>合成客戶端。change 可在寫出前改檔（模擬客戶端在準備後被改動）；tile 為 null 的 entry 不寫進 Tile.pak。</summary>
    static MpClient MpMakeClient(string root,Action<Dictionary<string,byte[]>,Dictionary<string,byte[]?>>? change=null,bool withMap=true,bool withTile=true){
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(int bx in Enumerable.Range(0x7ffb,4))foreach(int by in Enumerable.Range(0x8001,5)){
            string stem=MpStem(bx,by);
            files[$"map/0/{stem}.s32"]=stem switch{
                "7ffd8002"=>MpS32(coverTile:2,portal:(10,20),bx:bx,by:by),
                "7ffe8005"=>MpS32(effects:-1),
                "7ffb8001"=>MpS32(effects:2,bx:bx,by:by),
                "7ffe8003"=>MpS32(firstGround:uint.MaxValue),
                _=>MpS32(),
            };
        }
        files["map/4/7e007e00.S32"]=MpS32();
        files["map/4/7e017e00.s32"]=MpS32(objectTile:9999);
        files["map/4/7e027e00.seg"]=[1,2,3];
        files["map/4/7e007e01.s32.gz"]=[4,5,6];
        files["map/4/tempseg.s32"]=MpS32();
        files["map/4/800F7FF3E.s32"]=MpS32();
        files["map/7/7e007e00.s32"]=MpS32(ground:5u<<8);
        files["map/7/7e017e00.s32"]=MpS32(trailing:3);
        files["map/18/7e007e00.seg"]=[7,8,9];
        files["map/abc/7e007e00.s32"]=MpS32();
        var tile=new Dictionary<string,byte[]?>(StringComparer.Ordinal){
            ["1.til"]=MpTil(1,0x001F),["2.til"]=MpTil(1,0x03E0),["140.til"]=MpTil(189,0x7C00,true),["5.til"]=MpTil(1,0x7FFF),
            ["Water.lac"]=Encoding.ASCII.GetBytes("synthetic water"),["pier1.lac"]=Encoding.ASCII.GetBytes("synthetic pier 1"),["pier5.lac"]=Encoding.ASCII.GetBytes("synthetic pier 5"),
            ["Tile-x.xml"]=[1],
        };
        change?.Invoke(files,tile);
        Directory.CreateDirectory(root);
        if(withMap)foreach(var (path,data) in files){string full=Path.Combine(root,path.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllBytes(full,data);}
        var present=tile.Where(t=>t.Value!=null).ToDictionary(t=>t.Key,t=>t.Value!,StringComparer.Ordinal);
        if(withTile)Archive(root,"Tile",present.Select(t=>(t.Key,t.Value)).ToArray());
        return new(root,files,present);
    }
    static JsonNode MpJson(string path)=>JsonNode.Parse(File.ReadAllBytes(path))!;
    static string MpIntegrity(string schema,params (string Path,byte[] Data)[] files)=>JsonSerializer.Serialize(new{schema,files=files.Select(f=>new{path=f.Path,size=f.Data.Length,sha256=Sha(f.Data)}).ToArray()});
    static T MpThrows<T>(Action action,string label) where T:Exception{
        try{action();}catch(T ex){Check(true,label);return ex;}
        catch(Exception ex){throw new Exception($"{label}: expected {typeof(T).Name}, got {ex.GetType().Name}: {ex.Message}");}
        throw new Exception(label+": not refused");
    }

    [CheckGroup("map-provider",Order=1),CheckGroup("map-continuity",Order=300),CheckGroup(CheckGroups.Default)]
    static void MapProviderChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1041","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        var runner=AssetPrepRunner.Product();
        AssetPrepRequest Request(string client,string engine,params string[] items)=>new(client,engine,MapsRecipe.RecipeId,items.Length==0?[MapsRecipe.MapsItem,MapsRecipe.CacheItem]:items);
        string Out(string engine,string relative)=>Path.Combine(engine,"assets",MapsRecipe.RecipeId,relative.Replace('/',Path.DirectorySeparatorChar));

        // --- 0. 自動發現與純函式。
        Check(AssetRecipeDiscovery.Product.Errors.Count==0&&AssetRecipeDiscovery.Product.Recipes.Any(r=>r is MapsRecipe{Coverage:AssetRecipeCoverage.Partial}),"MP maps recipe discovered, coverage partial (terrain is T458)");
        Check(new MapsRecipe().Normalize(["maps","cache","maps"]).SequenceEqual(["cache","maps"]),"MP items normalised to cache, maps");
        Check(MapScan.BlockName("7FFD8002.S32") is {Stem:"7ffd8002",Bx:0x7ffd,By:0x8002,Seg:false}&&MapScan.BlockName("7fff8000.seg") is {Seg:true},"MP block names: 8 hex digits, extension case-insensitive");
        foreach(string bad in new[]{"tempseg.s32","800F7FF3E.s32","7ffd8002.s32.gz","7ffd800g.s32","7ffd8002.scc","7ffd8002s32"})Check(MapScan.BlockName(bad)==null,"MP not a block file: "+bad);
        foreach(var (dir,ok) in new[]{("0",true),("4",true),("12004",true),("abc",false),("012",false),("",false),("-1",false),("1234567890",false)})Check(MapScan.IsMapDirectory(dir,out _)==ok,$"MP map directory rule: \"{dir}\" → {ok}");

        // --- 1. 兩個輸出目錄：manifest 與收據逐 byte 相同、不含絕對路徑。
        var client=MpMakeClient(Case("client"));string clientBefore=ApTreeHash(client.Root);
        ApRefused(()=>runner.Run(Request(client.Root,Case("unused-engine"),"terrain")),AssetPrepFailure.InvalidRequest,"MP unknown item refused");
        string pair=Case("two-dirs"),engineA=Path.Combine(pair,"引擎 甲"),engineB=Path.Combine(pair,"engine-b");
        var a=runner.Run(Request(client.Root,engineA));
        var b=runner.Run(Request(client.Root,engineB,"cache","maps","cache"));
        Check(a.Outcome==AssetPrepOutcome.Published&&b.Outcome==AssetPrepOutcome.Published&&a.Produced.SequenceEqual(["cache","maps"]),"MP both directories published, items cache then maps");
        foreach(string rel in new[]{MapsRecipe.MapsManifestPath,MapsRecipe.CacheManifestPath,AssetReceipt.FileName})
            Check(File.ReadAllBytes(Out(engineA,rel)).AsSpan().SequenceEqual(File.ReadAllBytes(Out(engineB,rel))),"MP byte-identical in both directories: "+rel);
        Check(!Directory.Exists(Path.Combine(engineA,"assets",MapsRecipe.RecipeId,"maps","map"))&&!Directory.Exists(Path.Combine(engineA,"assets",MapsRecipe.RecipeId,"maps","tiles"))&&!Directory.Exists(Path.Combine(engineA,"assets",MapsRecipe.RecipeId,"cache","map")),"MP no S32 / TIL is copied into assets (provider reads the client)");
        Check(ApNames(Path.Combine(engineA,"assets",MapsRecipe.RecipeId)).SequenceEqual(["cache","maps",AssetReceipt.FileName]),"MP recipe directory holds only the two manifests and the receipt");
        foreach(string rel in new[]{MapsRecipe.MapsManifestPath,MapsRecipe.CacheManifestPath,AssetReceipt.FileName}){
            string text=File.ReadAllText(Out(engineA,rel));
            Check(!text.Contains(":\\",StringComparison.Ordinal)&&!text.Contains("引擎",StringComparison.Ordinal)&&!text.Contains("scratch",StringComparison.OrdinalIgnoreCase)&&!text.Contains(root,StringComparison.OrdinalIgnoreCase),"MP no absolute path in "+rel);
        }

        // --- 2. maps/manifest.json 欄位。
        var maps=MpJson(Out(engineA,MapsRecipe.MapsManifestPath));
        Check(maps["schema"]!.GetValue<string>()==MapsRecipe.MapsSchema,"MP maps schema p24-map-provider-v1");
        Check(maps.AsObject().Select(p=>p.Key).SequenceEqual(["schema","blocks","maps","tiles","missingTileIds","unsupportedMaps"]),"MP maps manifest top-level keys: "+string.Join(",",maps.AsObject().Select(p=>p.Key)));
        var blocks=maps["blocks"]!.AsArray();
        var expectedNames=Enumerable.Range(0x7ffb,4).SelectMany(bx=>Enumerable.Range(0x8001,5).Select(by=>(0,MpStem(bx,by)+".s32"))).Order().Concat(new[]{(4,"7e007e00.s32"),(4,"7e017e00.s32")}).ToArray();
        Check(blocks.Select(x=>(x!["mapId"]!.GetValue<int>(),x["name"]!.GetValue<string>())).SequenceEqual(expectedNames),"MP blocks: map 0 (20) and map 4 (2) in directory / name order; tempseg, 9-digit, .gz, non-decimal and seg-only directories left out");
        foreach(var x in blocks){
            int map=x!["mapId"]!.GetValue<int>();string name=x["name"]!.GetValue<string>(),path=x["path"]!.GetValue<string>();
            string source=client.Files.Keys.Single(k=>k.StartsWith($"map/{map}/",StringComparison.Ordinal)&&k.EndsWith("/"+name,StringComparison.OrdinalIgnoreCase));
            Check(path==source&&x["sha256"]!.GetValue<string>()==Sha(client.Files[source]),$"MP block {map}/{name}: path keeps the client's case ({path}) and SHA-256 is the file's");
            Check(x.AsObject().Select(p=>p.Key).SequenceEqual(["mapId","name","path","sha256","noEffectTail"]),$"MP block {map}/{name}: only product fields");
            Check(x["noEffectTail"]!.GetValue<bool>()==(name=="7ffe8005.s32"),$"MP block {map}/{name}: noEffectTail");
        }
        var mapRows=maps["maps"]!.AsArray();
        Check(mapRows.Select(m=>m!["mapId"]!.GetValue<int>()).SequenceEqual([0,4]),"MP maps[]: 0 and 4 (7 has a broken block, 18 has only .seg, abc is not decimal)");
        var m0=mapRows[0]!;var m4=mapRows[1]!;
        Check(m0.AsObject().Select(p=>p.Key).SequenceEqual(["mapId","minX","maxX","minY","maxY","width","height","verifiedAbsent","unsupportedBlocks"]),"MP maps[] keys: no name, terrain (T458) or server fields");
        Check(m0["minX"]!.GetValue<int>()==32448&&m0["maxX"]!.GetValue<int>()==32703&&m0["minY"]!.GetValue<int>()==32832&&m0["maxY"]!.GetValue<int>()==33151&&m0["width"]!.GetValue<int>()==256&&m0["height"]!.GetValue<int>()==320,"MP map 0 bounds from block names: x 32448–32703, y 32832–33151, 256 × 320");
        Check(m4["minX"]!.GetValue<int>()==0&&m4["maxX"]!.GetValue<int>()==191&&m4["minY"]!.GetValue<int>()==0&&m4["maxY"]!.GetValue<int>()==63,"MP map 4 bounds include the .seg-only block (x 0–191)");
        var va0=m0["verifiedAbsent"]!.AsArray().Select(v=>v!.GetValue<string>()).ToList();
        Check(va0.Count==52&&va0[0]=="7ff97fff"&&va0[1]=="7ff98000"&&va0[^1]=="80008007"&&!va0.Contains("7ffd8002"),"MP map 0 verifiedAbsent: bounds ± 2 blocks minus the 20 blocks = 52, bx outer: "+va0.Count);
        var va4=m4["verifiedAbsent"]!.AsArray().Select(v=>v!.GetValue<string>()).ToList();var ub4=m4["unsupportedBlocks"]!.AsArray().Select(v=>v!["name"]!.GetValue<string>()).ToList();
        Check(ub4.SequenceEqual(["7e007e01","7e027e00"])&&va4.Count==31&&!va4.Intersect(ub4).Any(),$"MP map 4: .seg-only and .s32.gz blocks unsupported, 31 verified absent ({string.Join(",",ub4)} / {va4.Count})");
        Check(m0["unsupportedBlocks"]!.AsArray().Count==0,"MP map 0 has no unsupported block");
        var tiles=maps["tiles"]!.AsArray();
        Check(tiles.Select(t=>t!["tile"]!.GetValue<int>()).SequenceEqual([1,2,140])&&tiles.All(t=>t!["sha256"]!.GetValue<string>()==Sha(client.Tile[t["tile"]!.GetValue<int>()+".til"])&&t["path"]!.GetValue<string>()==$"tiles/{t["tile"]!.GetValue<int>()}.til"),"MP tiles[]: ground, cover and object ids + 140, 65535 dropped, SHA-256 of the Tile entry; map 7's tile 5 not listed");
        Check(maps["missingTileIds"]!.AsArray().Select(v=>v!.GetValue<int>()).SequenceEqual([9999]),"MP missingTileIds: object tile 9999 is not in Tile.idx");
        var um=maps["unsupportedMaps"]!.AsArray();
        Check(um.Count==1&&um[0]!["mapId"]!.GetValue<int>()==7&&um[0]!["reason"]!.GetValue<string>().Contains("7e017e00.s32",StringComparison.Ordinal),"MP unsupportedMaps: map 7 (trailing data after the effect tail) — "+um.ToJsonString());
        byte[] raw=File.ReadAllBytes(Out(engineA,MapsRecipe.MapsManifestPath));
        Check(raw[0]=='{'&&raw.AsSpan(raw.Length-3).SequenceEqual("}\r\n"u8)&&raw.AsSpan().IndexOf("\r\n  \"blocks\""u8)>0&&raw.AsSpan().IndexOf((byte)'\n')==raw.AsSpan().IndexOf("\r\n"u8)+1,"MP maps manifest: indent 2, CRLF, trailing newline, no BOM");

        // --- 3. cache/manifest.json。
        var cacheJson=MpJson(Out(engineA,MapsRecipe.CacheManifestPath));
        Check(cacheJson["schema"]!.GetValue<string>()==MapsRecipe.CacheSchema&&cacheJson.AsObject().Select(p=>p.Key).SequenceEqual(["schema","maps","tiles","verifiedMissing"]),"MP cache manifest: schema, maps, tiles, verifiedMissing (no record / pixelModes / sourceArchives / scope / terrain)");
        var cm=cacheJson["maps"]!.AsArray();
        Check(cm.Select(x=>x!["name"]!.GetValue<string>()).SequenceEqual(MapsRecipe.CacheBlocks.Select(s=>s+".s32"))&&cm.Count==20,"MP cache maps: 20 blocks, bx outer");
        Check(cm.All(x=>x!["sha256"]!.GetValue<string>()==Sha(client.Files["map/0/"+x["name"]!.GetValue<string>()])&&x["size"]!.GetValue<long>()==client.Files["map/0/"+x["name"]!.GetValue<string>()].Length),"MP cache maps: SHA-256 and size of the client file");
        Check(cm.Single(x=>x!["name"]!.GetValue<string>()=="7ffb8001.s32")!["effectCount"]!.GetValue<int>()==2&&cm.Single(x=>x!["name"]!.GetValue<string>()=="7ffd8002.s32")!["portalCount"]!.GetValue<int>()==1&&cm.Single(x=>x!["name"]!.GetValue<string>()=="7ffe8005.s32")!["effectCount"]!.GetValue<int>()==0,"MP cache maps: effectCount and portalCount");
        Check(cacheJson["tiles"]!.AsArray().Select(t=>(t!["tile"]!.GetValue<int>(),t["sha256"]!.GetValue<string>(),t["size"]!.GetValue<int>())).SequenceEqual(new[]{1,2,140}.Select(i=>(i,Sha(client.Tile[i+".til"]),client.Tile[i+".til"].Length))),"MP cache tiles: 1, 2, 140 with SHA-256 and size");
        Check(cacheJson["verifiedMissing"]!.AsArray().Select(v=>v!.GetValue<string>()).SequenceEqual(MapsRecipe.CacheVerifiedMissing),"MP cache verifiedMissing: the four names are absent");

        // --- 4. 收據：只有 Tile（結構檢查，沒有整包 pin）與 map 來源。
        var receipt=AssetReceipt.TryLoad(Out(engineA,AssetReceipt.FileName))??throw new Exception("MP receipt does not load");
        Check(receipt.Inputs.Count==1&&receipt.Inputs[0] is {Name:"Tile"}&&receipt.Inputs[0].IndexSize==new FileInfo(Path.Combine(client.Root,"Tile.idx")).Length&&receipt.Inputs[0].IndexSha256==Sha(File.ReadAllBytes(Path.Combine(client.Root,"Tile.idx")))&&receipt.Inputs[0].PakSize==new FileInfo(Path.Combine(client.Root,"Tile.pak")).Length,"MP receipt inputs: Tile idx SHA-256 and pak size only, no Sprite container");
        var mapsItem=receipt.Completed.Single(c=>c.Item=="maps");
        Check(mapsItem.Sources.Any(s=>s is {Container:"map",Entry:"map/0/7ffd8002.s32"}&&s.Sha256==Sha(client.Files["map/0/7ffd8002.s32"]))&&mapsItem.Sources.Any(s=>s is {Container:"Tile",Entry:"140.til"})&&mapsItem.Sources.Any(s=>s is {Container:"map",Entry:"map/4/"}),"MP receipt sources: map files, directory listings and Tile entries");
        Check(mapsItem.Metadata["unsupportedMaps"]=="7"&&mapsItem.Metadata["missingTileIds"]=="9999"&&receipt.Status==AssetReceipt.Partial,"MP receipt metadata and partial status");

        // --- 5. 已是最新；客戶端改動後重新產生。
        var again=runner.Run(Request(client.Root,engineA));
        Check(again.Outcome==AssetPrepOutcome.UpToDate,"MP unchanged client: up to date");
        byte[] changed=MpS32(coverTile:2,portal:(11,20),bx:0x7ffd,by:0x8002);
        var changedClient=MpMakeClient(Case("client-changed"),(f,_)=>f["map/0/7ffd8002.s32"]=changed);
        var redo=runner.Run(Request(changedClient.Root,engineA));
        Check(redo.Outcome==AssetPrepOutcome.Published&&MpJson(Out(engineA,MapsRecipe.MapsManifestPath))["blocks"]!.AsArray().Single(x=>x!["name"]!.GetValue<string>()=="7ffd8002.s32")!["sha256"]!.GetValue<string>()==Sha(changed),"MP changed block: sources no longer verify, manifest re-made with the new SHA-256");

        // --- 6. 拒絕：缺 map／Tile、Tile 結構損壞、cache 缺區塊、SEG 錨點不符。
        ApRefused(()=>runner.Run(Request(MpMakeClient(Case("no-map"),withMap:false).Root,Case("e"))),AssetPrepFailure.MissingContainer,"MP client without map/ refused","map");
        ApRefused(()=>runner.Run(Request(MpMakeClient(Case("no-tile"),withTile:false).Root,Case("e"))),AssetPrepFailure.MissingContainer,"MP client without Tile.idx refused","Tile");
        var badIdx=MpMakeClient(Case("bad-idx"));File.WriteAllBytes(Path.Combine(badIdx.Root,"Tile.idx"),File.ReadAllBytes(Path.Combine(badIdx.Root,"Tile.idx"))[..^1]);
        ApRefused(()=>runner.Run(Request(badIdx.Root,Case("e"))),AssetPrepFailure.CorruptContainer,"MP Tile.idx size not count × 28 + 4 refused","Tile");
        ApRefused(()=>runner.Run(Request(MpMakeClient(Case("no-cache-block"),(f,_)=>f.Remove("map/0/7ffc8003.s32")).Root,Case("e"),"cache")),AssetPrepFailure.MissingEntry,"MP cache block missing refused","map");
        ApRefused(()=>runner.Run(Request(MpMakeClient(Case("no-cache-tile"),(_,t)=>t["2.til"]=null).Root,Case("e"),"cache")),AssetPrepFailure.MissingEntry,"MP cache TIL missing refused","Tile");
        ApRefused(()=>runner.Run(Request(MpMakeClient(Case("seg-anchor"),(f,_)=>{f["map/6/7ffe8000.s32"]=MpS32();f["map/6/7fff8000.seg"]=[1,2,3,4];}).Root,Case("e"),"maps")),AssetPrepFailure.ContainerMismatch,"MP map 6 SEG anchor mismatch refused","map/6");
        Check(ApTreeHash(client.Root)==clientBefore,"MP the client tree is unchanged by the runs (read-only)");

        // --- 7. provider：準備好的樹（integrity v2）直讀客戶端；與 r29 式的封裝樹畫出同一個場景。
        // terrain 由 T458 補上；這裡用合成 terrain 模擬，只為了讓產品讀取點能載入。
        byte[] terrain=Enumerable.Repeat((byte)3,256*320).ToArray();
        // engineA 在 5. 已換成 changedClient 的 manifest；engineB 仍是原本 client 的。
        var mapsNode=MpJson(Out(engineB,MapsRecipe.MapsManifestPath));
        mapsNode["maps"]!.AsArray().Single(m=>m!["mapId"]!.GetValue<int>()==0)!.AsObject()["terrain"]=new JsonObject{["path"]="terrain/0.bin",["sha256"]=Sha(terrain)};
        byte[] mapsBytes=Encoding.UTF8.GetBytes(mapsNode.ToJsonString());
        string txt=string.Concat(Enumerable.Range(0,512).Select(_=>string.Join(",",Enumerable.Repeat("3",512))+"\r\n"));byte[] txtBytes=Encoding.ASCII.GetBytes(txt);
        var cacheNode=MpJson(Out(engineB,MapsRecipe.CacheManifestPath));
        cacheNode.AsObject()["terrain"]=new JsonObject{["path"]="original-map0.txt",["sha256"]=Sha(txtBytes),["origin"]=new JsonArray(32256,32768),["size"]=new JsonArray(512,512)};
        byte[] cacheBytes=Encoding.UTF8.GetBytes(cacheNode.ToJsonString());
        void Save(string dir,string rel,byte[] data){string full=Path.Combine(dir,rel.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllBytes(full,data);}
        string prepared=Case("assets-v2"),packaged=Case("assets-v1");
        Save(prepared,"integrity.json",Encoding.UTF8.GetBytes(MpIntegrity(MapRawSource.IntegrityV2,("cache/manifest.json",cacheBytes),("maps/manifest.json",mapsBytes))));
        Save(packaged,"integrity.json",Encoding.UTF8.GetBytes(MpIntegrity(MapRawSource.IntegrityV1,("cache/manifest.json",cacheBytes),("maps/manifest.json",mapsBytes))));
        foreach(string dir in new[]{prepared,packaged}){Save(dir,"maps/manifest.json",mapsBytes);Save(dir,"maps/terrain/0.bin",terrain);Save(dir,"cache/manifest.json",cacheBytes);Save(dir,"cache/original-map0.txt",txtBytes);}
        foreach(var (path,data) in client.Files.Where(f=>f.Key.StartsWith("map/0/",StringComparison.Ordinal))){Save(packaged,"maps/"+path,data);Save(packaged,"cache/"+path,data);}
        foreach(string id in new[]{"1","2","140"}){Save(packaged,$"maps/tiles/{id}.til",client.Tile[id+".til"]);Save(packaged,$"cache/tiles/{id}.til",client.Tile[id+".til"]);}
        foreach(var (name,entry) in new[]{("Water.lac","Water.lac"),("Pier1.lac","pier1.lac"),("Pier5.lac","pier5.lac")})Save(packaged,"maps/ship/"+name,client.Tile[entry]);
        Check(MapRawSource.ModeOf(prepared)==MapSourceMode.Client&&MapRawSource.ModeOf(packaged)==MapSourceMode.Packaged&&MapRawSource.ModeOf(Case("no-integrity"))==MapSourceMode.Packaged,"MP mode: integrity v2 → client, v1 / none → packaged");
        Check(Directory.GetFiles(prepared,"*.s32",SearchOption.AllDirectories).Length==0&&Directory.GetFiles(prepared,"*.til",SearchOption.AllDirectories).Length==0,"MP prepared tree holds no S32 / TIL");
        string Scene(OriginalMapCache c)=>JsonSerializer.Serialize(c.SourceNodes())+"|"+JsonSerializer.Serialize(c.Scene.Select(d=>new{d.X,d.Y,d.Layer,d.Tile.Type}))+"|"+JsonSerializer.Serialize(c.Snapshot());
        foreach(int? map in new int?[]{0,null}){
            string label=map==null?"cache mode":"map 0";
            var direct=new OriginalMapCache(prepared,32600,32940,map,default,client.Root);
            var copied=new OriginalMapCache(packaged,32600,32940,map);
            Check(direct.Scene.Count>0&&direct.Slots.All(s=>s!=null)&&direct.Scene.Any(d=>d.Layer!="ground"||d.Tile.Type==2),$"MP {label}: prepared tree loads from TW_CLIENT_ROOT ({direct.Scene.Count} draws)");
            Check(Scene(direct)==Scene(copied),$"MP {label}: same scene, sources and snapshot as the packaged copy (behaviour unchanged)");
            direct.Shift(4);copied.Shift(4);
            Check(Scene(direct)==Scene(copied),$"MP {label}: same after a shift");
        }
        // 客戶端在準備後被改動：明確失敗。
        MpThrows<InvalidDataException>(()=>new OriginalMapCache(prepared,32600,32940,0,default,changedClient.Root),"MP changed block after preparation: load refused (SHA-256)");
        MpThrows<InvalidDataException>(()=>new OriginalMapCache(prepared,32600,32940,null,default,changedClient.Root),"MP changed block after preparation: cache mode refused");
        var tileChanged=MpMakeClient(Case("tile-changed"),(_,t)=>t["1.til"]=MpTil(1,0x0001));
        MpThrows<InvalidDataException>(()=>new OriginalMapCache(prepared,32600,32940,0,default,tileChanged.Root),"MP changed TIL entry: load refused (SHA-256)");
        var tileMissing=MpMakeClient(Case("tile-missing"),(_,t)=>t["2.til"]=null);
        MpThrows<FileNotFoundException>(()=>new OriginalMapCache(prepared,32600,32940,0,default,tileMissing.Root),"MP TIL entry missing from Tile.idx: load refused");
        MpThrows<DirectoryNotFoundException>(()=>new OriginalMapCache(prepared,32600,32940,0,default,Path.Combine(root,"no-such-client")),"MP missing client root: load refused");
        Check(new OriginalMapCache(packaged,32600,32940,0,default,tileChanged.Root).Scene.Count>0,"MP packaged (v1) tree never reads the client root");
        var cacheChanged=Encoding.UTF8.GetBytes(cacheNode.ToJsonString().Replace("\"verifiedMissing\"","\"verifiedMissing2\":[],\"verifiedMissing\""));Save(Path.Combine(root,"assets-v2-bad-cache"),"cache/manifest.json",cacheChanged);
        Save(Path.Combine(root,"assets-v2-bad-cache"),"integrity.json",Encoding.UTF8.GetBytes(MpIntegrity(MapRawSource.IntegrityV2,("cache/manifest.json",cacheBytes))));
        MpThrows<InvalidDataException>(()=>new OriginalMapCache(Path.Combine(root,"assets-v2-bad-cache"),32600,32940,null,default,client.Root),"MP prepared tree: cache manifest SHA-256 checked against integrity");

        // --- 8. 入口：準備好的樹從客戶端讀 portal 紀錄。
        var catalogType=typeof(OriginalMapCache).Assembly.GetType("P06.PortalCatalog")!;
        foreach(var (assets,clientRoot,label) in new[]{(prepared,client.Root,"prepared"),(packaged,(string?)null,"packaged")}){
            var cache=new OriginalMapCache(assets,32600,32940,0,default,clientRoot);
            var catalog=Activator.CreateInstance(catalogType,BindingFlags.Instance|BindingFlags.NonPublic,null,[assets,0,clientRoot],null)!;
            var contains=catalogType.GetMethod("Contains",BindingFlags.Instance|BindingFlags.NonPublic)!;
            Check((bool)contains.Invoke(catalog,[cache,32581,32916])!&&!(bool)contains.Invoke(catalog,[cache,32582,32916])!,$"MP {label}: portal cell read from the block (local 10,20)");
        }
        var staleCache=new OriginalMapCache(prepared,32600,32940,0,default,client.Root);
        var staleCatalog=Activator.CreateInstance(catalogType,BindingFlags.Instance|BindingFlags.NonPublic,null,[prepared,0,changedClient.Root],null)!;
        try{catalogType.GetMethod("Contains",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(staleCatalog,[staleCache,32581,32916]);throw new Exception("MP portal: changed client not refused");}
        catch(TargetInvocationException ex) when(ex.InnerException is InvalidDataException inner&&inner.Message.Contains("portal source hash mismatch",StringComparison.Ordinal)){Check(true,"MP portal: changed client block refused (portal source hash mismatch)");}

        // --- 9. 船運背景 LAC：準備好的樹從 Tile.pak 取 entry（名稱不分大小寫），pin 不符拒絕。
        var directSource=MapRawSource.Open(prepared,client.Root);var copiedSource=MapRawSource.Open(packaged);
        foreach(var (name,entry) in new[]{("Water.lac","Water.lac"),("Pier1.lac","pier1.lac"),("Pier5.lac","pier5.lac")}){
            string pin=Sha(client.Tile[entry]);
            Check(directSource.ShipLac(name,pin).AsSpan().SequenceEqual(client.Tile[entry])&&copiedSource.ShipLac(name,pin).AsSpan().SequenceEqual(client.Tile[entry]),$"MP ship {name}: same bytes from Tile.pak entry {entry} and from assets/maps/ship");
            var ex=MpThrows<InvalidDataException>(()=>directSource.ShipLac(name,new string('0',64)),$"MP ship {name}: pin mismatch refused");
            Check(ex.Message.Contains("ship backdrop integrity",StringComparison.Ordinal),$"MP ship {name}: same error text as before");
        }
        MpThrows<InvalidDataException>(()=>directSource.ShipLac("../Water.lac",new string('0',64)),"MP ship: LAC name with a path refused");
        MpThrows<InvalidDataException>(()=>directSource.Block("map/0/../../Tile.idx",new string('0',64)),"MP block path outside map/<dir>/<8 hex> refused");
    }
}
