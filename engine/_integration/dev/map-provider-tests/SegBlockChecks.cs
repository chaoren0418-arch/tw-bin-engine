using P06;
using P06.AssetPrep;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

// T1195（#1195）：只有 .seg 的地圖區塊（原 BIN sub_F0B570 的 SEG 分支沒有名稱檢查）改依 manifest SHA-256 載入。
// 全部 S32／SEG／TIL 都由本檔與 MapProviderChecks 的 MpSeg／MpS32／MpTil 合成（不含任何真實 .seg），寫在 scratch/issue-1195/run-<時間>/ 下；
// 不讀客戶端、不需要遊戲、網路或 DB。測試本身不做遞迴刪除。
//   (a) 非錨點 SEG 依 manifest（blocks[]，classic-seg）SHA 可載入；(b) SHA 不符拒絕；
//   (c) r29 式 manifest：unsupportedBlocks＋sources[].sha256 的 .seg 從 TW_CLIENT_ROOT 讀；(d) MapScan／maps recipe 產生 classic-seg 區塊列；
//   (e) map 6 錨點 7fff8000 的 pin 不變；(f) S32 區塊行為不變。
partial class Checks {
    [CheckGroup("map-provider",Order=3),CheckGroup("map-continuity",Order=302),CheckGroup(CheckGroups.Default)]
    static void SegBlockChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1195","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        var runner=AssetPrepRunner.Product();
        void Save(string dir,string rel,byte[] data){string full=Path.Combine(dir,rel.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllBytes(full,data);}

        // 合成客戶端（MpMakeClient 的 map 0／4／7／18 之外再加 map 4 的三個 .seg-only 區塊）：
        //   7e027e00.seg  cover＋object（tile 2）＋portal (12,34)
        //   7e017e01.seg  檔案在 refs 之後就結束（沒有 portal 區段，原 BIN 照樣讀）
        //   7e027e01.seg  effects 尾段 1 筆（SEG 光源表與 S32 不同，產品仍拒絕，但錯誤要帶區塊名與值）
        byte[] seg=MpSeg(coverTile:2,objectTile:2,portal:(12,34)),segShort=MpSeg(ground:2<<8,portalSection:false),segEffects=MpSeg(effects:1);
        var client=MpMakeClient(Case("client"),(f,_)=>{f["map/4/7e017e01.seg"]=segShort;f["map/4/7e027e01.seg"]=segEffects;});
        string clientBefore=ApTreeHash(client.Root);

        // --- (d) MapScan 與 maps recipe：.seg-only 區塊是 classic-seg 區塊。
        var plan=MapScan.Plan("4",["7e007e00.S32","7e017e00.s32","7e027e00.seg","7e017e01.SEG","7e037e00.s32","7e037e00.seg"])!;
        Check(plan.Blocks["7e027e00"]=="7e027e00.seg"&&plan.Blocks["7e017e01"]=="7e017e01.SEG"&&plan.Blocks["7e037e00"]=="7e037e00.s32"&&plan.SegOnly.Count==0,"SEG plan: .seg-only blocks are blocks (original case kept); a block with both uses the .s32; SegOnly empty");
        Check(MapScan.Coverage(plan).UnsupportedBlocks.Count==0,"SEG plan: no .seg-only block left in unsupportedBlocks");
        var scan=MapScan.Parse(segShort,true);
        Check(scan.NoEffectTail&&scan.PortalCount==0&&scan.TileIds.SetEquals([2]),"SEG scan: a SEG that ends after the refs has no portal section and no effect tail (sub_F0B570 reads them only while data remains)");
        Check(MapScan.Parse(seg,true) is {PortalCount:1,NoEffectTail:true} s1&&s1.TileIds.SetEquals([1,2]),"SEG scan: portal and tiles of the full synthetic SEG");
        MpThrows<InvalidDataException>(()=>MapScan.Parse([..seg,0xEE],true),"SEG scan: trailing byte after the SEG portal section refused");
        string engine=Case("engine");
        Check(runner.Run(new(client.Root,engine,MapsRecipe.RecipeId,[MapsRecipe.MapsItem])).Outcome==AssetPrepOutcome.Published,"SEG maps recipe published");
        Check(runner.Run(new(client.Root,engine,TerrainRecipe.RecipeId,[TerrainRecipe.MapsItem])).Outcome==AssetPrepOutcome.Published,"SEG terrain recipe published");
        byte[] mapsBytes=File.ReadAllBytes(Path.Combine(engine,"assets",MapsRecipe.RecipeId,"maps","manifest.json"));
        byte[] terrain4=File.ReadAllBytes(Path.Combine(engine,"assets",TerrainRecipe.RecipeId,"maps","terrain","4.bin"));
        var maps=JsonNode.Parse(mapsBytes)!;
        JsonNode Row(string name)=>maps["blocks"]!.AsArray().Single(b=>b!["mapId"]!.GetValue<int>()==4&&b["name"]!.GetValue<string>()==name)!;
        foreach(var (name,data) in new[]{("7e027e00.seg",seg),("7e017e01.seg",segShort),("7e027e01.seg",segEffects)}){
            var row=Row(name);
            Check(row["format"]!.GetValue<string>()=="classic-seg"&&row["sha256"]!.GetValue<string>()==Sha(data)&&row["path"]!.GetValue<string>()=="map/4/"+name,$"SEG manifest row {name}: format classic-seg, SHA-256 and path of the client file");
        }
        Check(!Row("7e027e01.seg")["noEffectTail"]!.GetValue<bool>()&&Row("7e027e00.seg")["noEffectTail"]!.GetValue<bool>(),"SEG manifest noEffectTail: false only for the SEG with an effects word");
        var m4=maps["maps"]!.AsArray().Single(m=>m!["mapId"]!.GetValue<int>()==4)!;
        Check(m4["unsupportedBlocks"]!.AsArray().Select(u=>u!["name"]!.GetValue<string>()).SequenceEqual(["7e007e01"]),"SEG manifest map 4: only the .s32.gz block is unsupported");

        // --- (a) 準備好的樹（integrity v2，blocks[] classic-seg）：非錨點 SEG 依 SHA 載入。
        string prepared=Case("assets-v2");
        Save(prepared,"integrity.json",Encoding.UTF8.GetBytes(MpIntegrity(MapRawSource.IntegrityV2,("maps/manifest.json",mapsBytes))));
        Save(prepared,"maps/manifest.json",mapsBytes);Save(prepared,"maps/terrain/4.bin",terrain4);
        var direct=new OriginalMapStore(prepared,4,default,client.Root);
        var b1=direct.Load(0x7e02,0x7e00,1);
        Check(b1.Sha256==Sha(seg)&&b1.Ground.Length==8192&&b1.Ground.All(t=>t.Tile==1&&t.Index==0)&&b1.Cover.Single() is {X:3,Y:4,Tile:2,Index:0},"SEG (a) prepared tree: non-anchor SEG loads by manifest SHA-256 (u16 ground tile = v>>8, cover x, y, u16)");
        Check(b1.Groups.Single() is {Id:7} g&&g.Objects.Single() is {X:5,Y:6,Z:1,Tile:2}&&b1.Attributes.Count(a=>a==1)==8191&&b1.Attributes[34*128+12]==(1|2),"SEG (a) prepared tree: group member after the 205,205 placeholder, u8 attributes, portal cell flagged |= 2");
        Check(b1.Tiles.ContainsKey((1,0))&&b1.Tiles.ContainsKey((2,0)),"SEG (a) prepared tree: TIL of the SEG tiles decoded");
        var bShort=direct.Load(0x7e01,0x7e01,2);
        Check(bShort.Ground.All(t=>t.Tile==2)&&bShort.Cover.Length==0&&bShort.Effects.Length==0,"SEG (a) prepared tree: SEG ending after the refs loads (no portal section, no effects)");
        var effectsEx=MpThrows<NotSupportedException>(()=>direct.Load(0x7e02,0x7e01,3),"SEG (a) prepared tree: SEG effects tail still refused");
        Check(effectsEx.Message.Contains("4/7e027e01",StringComparison.Ordinal)&&effectsEx.Message.Contains("0x0001",StringComparison.Ordinal),"SEG effects refusal names the block and the effects value: "+effectsEx.Message);

        // --- (f) S32 不變：同一棵樹的 S32 區塊照舊。
        var s32=direct.Load(0x7e00,0x7e00,4);
        Check(s32.Sha256==Sha(client.Files["map/4/7e007e00.S32"])&&s32.Ground.All(t=>t.Tile==1)&&s32.Attributes.All(a=>a==3),"SEG (f) S32 block of the same map unchanged (u32 ground, u16 attributes)");
        MpThrows<NotSupportedException>(()=>direct.Load(0x7e00,0x7e01,5),"SEG (f) .s32.gz-only block still unsupported");

        // --- (b) SHA 不符：客戶端的 .seg 在準備後被改動 → 拒絕。
        var changed=MpMakeClient(Case("client-changed"),(f,_)=>{f["map/4/7e017e01.seg"]=segShort;f["map/4/7e027e01.seg"]=segEffects;f["map/4/7e027e00.seg"]=MpSeg(coverTile:2,objectTile:2,portal:(13,34));});
        var hashEx=MpThrows<InvalidDataException>(()=>new OriginalMapStore(prepared,4,default,changed.Root).Load(0x7e02,0x7e00,1),"SEG (b) prepared tree: changed .seg refused");
        Check(hashEx.Message.StartsWith("Original cache source hash",StringComparison.Ordinal),"SEG (b) refusal is the source hash check: "+hashEx.Message);

        // --- (c) r29 式封裝樹（integrity v1）：.seg-only 區塊仍在 unsupportedBlocks，帶 sources[].sha256；從 TW_CLIENT_ROOT 讀。
        var legacyMaps=JsonNode.Parse(mapsBytes)!;
        var legacyBlocks=legacyMaps["blocks"]!.AsArray();
        foreach(var row in legacyBlocks.Where(b=>b!["name"]!.GetValue<string>().EndsWith(".seg",StringComparison.Ordinal)).ToList())legacyBlocks.Remove(row);
        var l4=legacyMaps["maps"]!.AsArray().Single(m=>m!["mapId"]!.GetValue<int>()==4)!;
        JsonObject Legacy(string name,string sourcePath,string sha)=>new(){["name"]=name,["sources"]=new JsonArray(new JsonObject{["path"]=sourcePath,["sha256"]=sha})};
        var ub=l4["unsupportedBlocks"]!.AsArray();
        ub.Add(Legacy("7e027e00",@"D:\elsewhere\client\map\4\7e027e00.seg",Sha(seg)));
        ub.Add(Legacy("7e017e01",@"D:\elsewhere\client\map\4\7e017e01.seg",Sha(segShort).ToUpperInvariant()));
        ub.Add(Legacy("7e027e01",@"D:\elsewhere\client\map\4\7e027e01.seg",new string('0',64)));   // SHA 不符
        ub.Add(new JsonObject{["name"]="7e037e00"});                                                     // 沒有 sources：維持不支援
        ub.Add(Legacy("7e037e01",@"D:\elsewhere\client\map\4\7e037e01.s32.gz",Sha(seg)));               // 來源不是 .seg：維持不支援
        ub.Add(Legacy("7e047e00",@"D:\elsewhere\client\map\3\7e047e00.seg",Sha(seg)));                   // 別張地圖的路徑：維持不支援
        // map 6 錨點名稱、但 SHA 不是 pin（合成檔）。
        var l6=JsonNode.Parse(l4.ToJsonString())!.AsObject();l6["mapId"]=6;
        l6["unsupportedBlocks"]=new JsonArray(Legacy("7fff8000",@"D:\elsewhere\client\map\6\7fff8000.seg",Sha(seg)));l6["verifiedAbsent"]=new JsonArray();
        legacyMaps["maps"]!.AsArray().Add(l6);
        byte[] legacyBytes=Encoding.UTF8.GetBytes(legacyMaps.ToJsonString());
        string packaged=Case("assets-v1");
        Save(packaged,"integrity.json",Encoding.UTF8.GetBytes(MpIntegrity(MapRawSource.IntegrityV1,("maps/manifest.json",legacyBytes))));
        Save(packaged,"maps/manifest.json",legacyBytes);Save(packaged,"maps/terrain/4.bin",terrain4);
        foreach(var b in legacyBlocks.Where(b=>b!["mapId"]!.GetValue<int>()==4))Save(packaged,"maps/"+b!["path"]!.GetValue<string>(),client.Files[b["path"]!.GetValue<string>()]);
        foreach(string id in new[]{"1","2","140"})Save(packaged,$"maps/tiles/{id}.til",client.Tile[id+".til"]);
        Check(Directory.GetFiles(packaged,"*.seg",SearchOption.AllDirectories).Length==0,"SEG (c) packaged tree holds no .seg (the legacy path reads the client)");
        var copied=new OriginalMapStore(packaged,4,default,client.Root);
        var c1=copied.Load(0x7e02,0x7e00,1);
        string Shape(OriginalMapBlock b)=>System.Text.Json.JsonSerializer.Serialize(new{b.Name,b.Sha256,b.X,b.Y,b.Ground,b.Cover,b.Groups,b.Triggers,b.Attributes,tiles=b.Tiles.Keys.OrderBy(k=>k).Select(k=>$"{k.Item1}:{k.Item2}")});
        Check(Shape(c1)==Shape(b1),"SEG (c) legacy r29-style row (unsupportedBlocks + sources[].sha256) loads the same block as the prepared tree");
        Check(Shape(copied.Load(0x7e01,0x7e01,2))==Shape(bShort),"SEG (c) legacy row with an upper-case SHA-256 loads");
        Check(copied.Load(0x7e00,0x7e00,4) is {} c32&&Shape(c32)==Shape(s32),"SEG (f) S32 block of the packaged tree unchanged");
        var legacyHash=MpThrows<InvalidDataException>(()=>copied.Load(0x7e02,0x7e01,3),"SEG (c) legacy row whose SHA-256 does not match the client file refused");
        Check(legacyHash.Message.Contains("TW_CLIENT_ROOT/map/4/7e027e01.seg",StringComparison.Ordinal),"SEG (c) legacy refusal names the client path: "+legacyHash.Message);
        foreach(var (bx,by,why) in new[]{(0x7e03,0x7e00,"row without sources"),(0x7e03,0x7e01,"source not a .seg"),(0x7e04,0x7e00,"source in another map directory"),(0x7e00,0x7e01,".s32.gz block")}){
            var ex=MpThrows<NotSupportedException>(()=>copied.Load(bx,by,9),$"SEG (c) still unsupported: {why}");
            Check(ex.Message.Contains("SEG／壓縮格式",StringComparison.Ordinal),$"SEG (c) {why}: same refusal text as before");
        }
        MpThrows<DirectoryNotFoundException>(()=>new OriginalMapStore(packaged,4,default,Path.Combine(root,"no-such-client")).Load(0x7e02,0x7e00,1),"SEG (c) legacy row without the client root refused");
        string emptyClient=Case("empty-client");Directory.CreateDirectory(emptyClient);
        MpThrows<FileNotFoundException>(()=>new OriginalMapStore(packaged,4,default,emptyClient).Load(0x7e02,0x7e00,1),"SEG (c) legacy row whose .seg is missing from the client refused");

        // --- (e) map 6 錨點：名稱 7fff8000 在 map 6 必須是 pin 的 SHA；別張地圖同名不受影響。
        var anchorEx=MpThrows<NotSupportedException>(()=>new OriginalMapStore(packaged,6,default,client.Root).Load(0x7fff,0x8000,1),"SEG (e) map 6 7fff8000 with a SHA-256 other than the pin refused");
        Check(anchorEx.Message=="Unverified SEG profile","SEG (e) same refusal text as before: "+anchorEx.Message);
        SegBlocks.CheckAnchor(6,"7fff8000",MapScan.SegAnchorSha256);SegBlocks.CheckAnchor(6,"7fff8000",MapScan.SegAnchorSha256.ToUpperInvariant());SegBlocks.CheckAnchor(4,"7fff8000",Sha(seg));SegBlocks.CheckAnchor(6,"7ffe8000",Sha(seg));
        Check(true,"SEG (e) anchor pin accepted; other names and maps not pinned");
        MpThrows<NotSupportedException>(()=>SegBlocks.CheckAnchor(6,"7fff8000",Sha(seg)),"SEG (e) anchor pin mismatch refused");
        Check(SegBlocks.LegacySha256(4,JsonDocumentElement(Legacy("7e027e00",@"x\map\4\7E027E00.SEG",Sha(seg).ToUpperInvariant())))==Sha(seg)&&SegBlocks.LegacySha256(4,JsonDocumentElement(Legacy("7e027e00",@"x/map/4/7e027e00.seg","12")))==null&&SegBlocks.LegacySha256(4,JsonDocumentElement(Legacy("7e027e0g",@"x\map\4\7e027e0g.seg",Sha(seg))))==null,"SEG legacy row rule: own .seg in map/<id>, 64 hex SHA-256 (lower-cased), 8 hex name");

        // --- 入口：兩種樹都從 SEG 讀 portal 紀錄（ReadBlock），SHA 不符拒絕。
        var catalogType=typeof(OriginalMapCache).Assembly.GetType("P06.PortalCatalog")!;
        var readBlock=catalogType.GetMethod("ReadBlock",BindingFlags.Instance|BindingFlags.NonPublic)!;
        foreach(var (assets,block,label) in new[]{(prepared,b1,"prepared"),(packaged,c1,"packaged legacy")}){
            var catalog=Activator.CreateInstance(catalogType,BindingFlags.Instance|BindingFlags.NonPublic,null,[assets,4,client.Root],null)!;
            var cells=(HashSet<(int,int)>)readBlock.Invoke(catalog,[block])!;
            Check(cells.SetEquals([(12,34)]),$"SEG portal {label}: portal cell read from the non-anchor SEG");
            var none=(HashSet<(int,int)>)readBlock.Invoke(catalog,[label=="prepared"?bShort:copied.Load(0x7e01,0x7e01,2)])!;
            Check(none.Count==0,$"SEG portal {label}: SEG without a portal section has no portal");
            var stale=Activator.CreateInstance(catalogType,BindingFlags.Instance|BindingFlags.NonPublic,null,[assets,4,changed.Root],null)!;
            try{readBlock.Invoke(stale,[block]);throw new Exception($"SEG portal {label}: changed client not refused");}
            catch(TargetInvocationException ex) when(ex.InnerException is InvalidDataException inner&&inner.Message=="portal source hash mismatch"){Check(true,$"SEG portal {label}: changed .seg refused (portal source hash mismatch)");}
        }
        Check(ApTreeHash(client.Root)==clientBefore,"SEG the client tree is unchanged by the runs (read-only)");
    }
    static System.Text.Json.JsonElement JsonDocumentElement(JsonNode node){using var d=System.Text.Json.JsonDocument.Parse(node.ToJsonString());return d.RootElement.Clone();}
}
