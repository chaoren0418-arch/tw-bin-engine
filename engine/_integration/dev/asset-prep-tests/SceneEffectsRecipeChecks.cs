using P06;
using P06.AssetPrep;
using P24.MapEffectEvents;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// T457（#1042）：recipe scene 與 map-effects 的離線測試。S32、TIL、Tile／Text／17 個 Sprite 容器、list.spr／list.spz 與每個 .spr
// 都由本檔合成（像素是依編號算出的測試值，不是任何原客戶端的圖），寫在 scratch/issue-1042/run-<時間>/ 下；不讀客戶端、
// 不需要遊戲、網路或 DB。每次執行用新的 run 目錄，測試本身不做遞迴刪除。
// 群組 asset-prep-scene；同時掛在 CI 已在跑的 map-continuity 之下（與 map-provider 相同做法，不改 build.yml）。
partial class Checks {
    sealed record SeObject(byte X,byte Y,byte Z,byte Index,short Tile);

    /// <summary>合成 S32：ground 由函式給值、cover、群組物件、opacity trigger、effects tail（null = 沒有 tail）、結尾多餘 bytes。</summary>
    static byte[] SeS32(Func<int,uint> ground,(byte X,byte Y,byte Index,ushort Tile)[] cover,(ushort Id,SeObject[] Objects)[] groups,(byte X,byte Y,ushort Group,byte Type)[] triggers,int[]? effects,int trailing=0){
        using var s=new MemoryStream();using var b=new BinaryWriter(s);
        for(int i=0;i<8192;i++)b.Write(ground(i));
        b.Write((ushort)cover.Length);foreach(var c in cover){b.Write(c.X);b.Write(c.Y);b.Write(c.Index);b.Write(c.Tile);b.Write((byte)0);}
        for(int i=0;i<8192;i++)b.Write((ushort)0);
        b.Write(groups.Length);
        foreach(var (id,objects) in groups){b.Write(id);b.Write((ushort)objects.Length);foreach(var o in objects){b.Write(o.X);b.Write(o.Y);b.Write(o.Z);b.Write(o.Index);b.Write(o.Tile);b.Write((byte)0);}}
        b.Write(triggers.Length);foreach(var t in triggers){b.Write(t.X);b.Write(t.Y);b.Write(t.Group);b.Write(t.Type);}
        b.Write(0);b.Write((ushort)0);
        if(effects!=null){b.Write((ushort)effects.Length);foreach(int e in effects){b.Write((ushort)e);b.Write((ushort)1);b.Write((ushort)2);}}
        for(int i=0;i<trailing;i++)b.Write((byte)0xEE);
        return s.ToArray();
    }
    /// <summary>菱形塊（type 的 bit1 = 0）：24 列共 288 個 u16。</summary>
    static byte[] SeDiamond(byte type,int seed){
        var d=new List<byte>{type};
        for(int y=0;y<24;y++){int n=y<12?2*(y+1):2*(23-y);for(int j=0;j<n;j++){ushort c=(ushort)((seed*977+y*31+j*7)&0x7FFF);d.Add((byte)c);d.Add((byte)(c>>8));}}
        return d.ToArray();
    }
    /// <summary>RLE 塊（type 的 bit1 = 1）：x0=2、y0=3、高 2 列，每列一段（skip 2 → x 前進 1）。</summary>
    static byte[] SeRle(byte type,int seed){
        var d=new List<byte>{type,2,3,4,2};
        for(int y=0;y<2;y++){d.Add(1);d.Add(2);d.Add(3);for(int j=0;j<3;j++){ushort c=(ushort)((seed*131+y*17+j)&0x7FFF);d.Add((byte)c);d.Add((byte)(c>>8));}}
        return d.ToArray();
    }
    static byte[] SeTil(params byte[][] blocks){
        using var s=new MemoryStream();using var b=new BinaryWriter(s);b.Write(blocks.Length);int at=0;
        foreach(var block in blocks){b.Write(at);at+=block.Length;}b.Write(at);foreach(var block in blocks)b.Write(block);
        return s.ToArray();
    }

    /// <summary>scene 的合成客戶端：map/0 的北（7ffd8002.s32）、南（7ffd8003.S32，大寫副檔名）兩個區塊，Tile 有 tile 1／2／3。</summary>
    static string SeSceneClient(string root,bool withSouth=true,bool withTile3=true,Func<byte[],byte[]>? changeNorth=null){
        uint Ground(int i)=>i==0?(2u<<8)|0u:(1u<<8)|(uint)(i%4)|(i==5?7u<<24:0u);
        byte[] Block(int seed,int[]? effects)=>SeS32(Ground,[(10,4,0,3),(11,5,1,3)],
            [(7,[new(20,6,1,2,3),new(21,6,0,3,3)]),(9,[new(22,8,2,2,3)])],
            [(20,6,0,0),(21,62,1,1),(3,100,0,0)],effects);
        var map=Path.Combine(root,"map","0");Directory.CreateDirectory(map);
        byte[] north=Block(1,[]);if(changeNorth!=null)north=changeNorth(north);
        File.WriteAllBytes(Path.Combine(map,"7ffd8002.s32"),north);
        if(withSouth)File.WriteAllBytes(Path.Combine(map,"7ffd8003.S32"),Block(2,[42]));
        File.WriteAllBytes(Path.Combine(map,"7ffd8004.s32"),[1,2,3]);   // not a scene block: never read
        var tile=new List<(string,byte[])>{("1.til",SeTil(SeDiamond(0,1),SeDiamond(1,2),SeDiamond(0,3),SeDiamond(16,4))),("2.til",SeTil(SeDiamond(17,5)))};
        if(withTile3)tile.Add(("3.TIL",SeTil(SeRle(2,6),SeRle(3,7),SeRle(6,8),SeRle(7,9))));
        tile.Add(("Water.lac",[1]));
        Archive(root,"Tile",tile.ToArray());
        return root;
    }

    [CheckGroup("asset-prep-scene",Order=1),CheckGroup("map-continuity",Order=310),CheckGroup(CheckGroups.Default)]
    static void SceneEffectsRecipeChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1042","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        var runner=AssetPrepRunner.Product();
        var product=AssetRecipeDiscovery.Product;
        Check(product.Errors.Count==0&&product.Recipes.Count(r=>r is SceneRecipe{Id:"scene",Coverage:AssetRecipeCoverage.Complete,Sources:AssetPrepSources.Tile|AssetPrepSources.MapFiles})==1
            &&product.Recipes.Count(r=>r is MapEffectsRecipe{Id:"map-effects",Coverage:AssetRecipeCoverage.Complete,Sources:AssetPrepSources.Sprites|AssetPrepSources.MapFiles})==1,"SE scene and map-effects recipes discovered once with their sources");
        Check(AssetPrepPins.Tw1608262200.Effect6016Sha256==Effect6016Definition.AcceptedMetadataSha256&&Effect6016Definition.AcceptedMetadataSha256=="825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383"
            &&AssetPrepPins.Tw1608262200.ListSprSha256=="523c61ad5cc362a56a36f58b262c84efa8b4435f017e7074d35b5834e8b9f127","SE product anchors: 6016 pin unchanged (same constant as ClientEffect6016), list.spr anchor");

        SceneChecks(runner,Case);
        MapEffectChecks(runner,Case);
    }

    static void SceneChecks(AssetPrepRunner runner,Func<string,string> Case){
        AssetPrepRequest Request(string client,string engine,params string[] items)=>new(client,engine,SceneRecipe.RecipeId,items.Length==0?[SceneRecipe.SceneItem]:items);
        string Dir(string engine)=>Path.Combine(engine,"assets",SceneRecipe.RecipeId);
        string Out(string engine,string rel)=>Path.Combine(Dir(engine),rel.Replace('/',Path.DirectorySeparatorChar));

        string client=SeSceneClient(Case("scene-client"));string before=ApTreeHash(client);
        string pair=Case("scene-two-dirs"),engineA=Path.Combine(pair,"引擎 甲"),engineB=Path.Combine(pair,"engine-b");
        var a=runner.Run(Request(client,engineA));var b=runner.Run(Request(client,engineB,"scene","scene"));
        Check(a.Outcome==AssetPrepOutcome.Published&&b.Outcome==AssetPrepOutcome.Published&&a.Produced.SequenceEqual(["scene"]),"SE scene published in both directories");
        string[] four=[SceneRecipe.ManifestPath,SceneRecipe.DrawCommandsPath,SceneRecipe.TilePixelsPath,SceneRecipe.OpacityPath];
        foreach(string rel in four.Append(AssetReceipt.FileName))
            Check(File.ReadAllBytes(Out(engineA,rel)).AsSpan().SequenceEqual(File.ReadAllBytes(Out(engineB,rel))),"SE scene byte-identical in both directories: "+rel);
        Check(ApNames(Dir(engineA)).SequenceEqual([AssetReceipt.FileName,"scene"])&&ApNames(Path.Combine(Dir(engineA),"scene")).SequenceEqual(["draw-commands.json","manifest.json","opacity.json","tile-pixels.json"]),"SE scene: only the four product files (no map.json, tiles.json, region.rgb555 or PNG / atlas)");
        foreach(string rel in four.Append(AssetReceipt.FileName)){
            string text=File.ReadAllText(Out(engineA,rel));
            Check(!text.Contains(":\\",StringComparison.Ordinal)&&!text.Contains("引擎",StringComparison.Ordinal)&&!text.Contains("scratch",StringComparison.OrdinalIgnoreCase),"SE scene: no absolute path in "+rel);
        }
        Check(ApTreeHash(client)==before,"SE scene: client left untouched");

        // 格式：三個執行檔沒有任何換行（json.dumps separators=(',', ':')），manifest 全文與基線格式相同。
        foreach(string rel in four[..3]){var raw=File.ReadAllBytes(Out(engineA,rel));Check(raw.Length>0&&!raw.Contains((byte)'\n')&&!raw.Contains((byte)'\r')&&!raw.Contains((byte)' ')||rel==SceneRecipe.ManifestPath,"SE scene compact, no newline: "+rel);}
        const int drawCount=2*(8192+2+3);
        string expectedManifest="{\"schema\":\"p22-dual-map-v1\",\"map_id\":0,\"region\":\"complete original north and south blocks; other map0 blocks not loaded\",\"blocks\":[\"7ffd8002\",\"7ffd8003\"],"
            +"\"world_origin\":[32576,32896],\"world_inclusive_bounds\":[32576,32896,32639,33023],\"canvas_size\":[4608,2304],\"pixel_origin\":[0,756],\"draw_count\":"+drawCount+",\"terrain_stride\":64,\"terrain_rows\":128,"
            +"\"ordering\":\"bin-cell y,x; ground then z ascending; equal z reverse original input; cold-load south inserted before north (north serial offset100000)\","
            +"\"opacity_state\":\"original untriggered; metadata retained, dynamic triggers not implemented\",\"portals\":\"original metadata retained; no invented transitions\","
            +"\"edge_policy\":\"retain every source-owned pixel including external anchors; remaining neighboring blocks not loaded\"}";
        Check(File.ReadAllText(Out(engineA,SceneRecipe.ManifestPath))==expectedManifest,"SE scene manifest: exact bytes (fixed strings, canvas = max px + 24 × max py + 24 + 756, draw_count)");
        string commands=File.ReadAllText(Out(engineA,SceneRecipe.DrawCommandsPath));
        Check(commands.StartsWith("[{\"layer\":\"ground\",\"source_index\":126,\"serial\":100126,\"block\":\"7ffd8002\",\"original_source_index\":126,\"x\":126,\"y\":0,\"index\":2,\"tile\":1,\"uk\":0,\"offset\":504},{\"layer\":\"ground\",\"source_index\":127,",StringComparison.Ordinal),
            "SE draw-commands: first rows sorted by (py, px), key order layer, source_index, serial, block, original_source_index, x, y, index, tile, uk, offset");
        var cmd=JsonNode.Parse(commands)!.AsArray();
        Check(cmd.Count==drawCount,"SE draw-commands: 2 × (8192 ground + 2 cover + 3 objects)");
        var obj=cmd.First(c=>c!["layer"]!.GetValue<string>()=="objects"&&c["block"]!.GetValue<string>()=="7ffd8003")!.AsObject();
        Check(obj.Select(p=>p.Key).SequenceEqual(["layer","source_index","serial","block","original_source_index","x","y","z","index","tile","uk","group","group_ordinal","offset"]),"SE draw-commands objects row keys");
        Check(cmd.Where(c=>c!["block"]!.GetValue<string>()=="7ffd8003").All(c=>c!["serial"]!.GetValue<int>()==c["source_index"]!.GetValue<int>()&&c["y"]!.GetValue<int>()>=64)
            &&cmd.Where(c=>c!["block"]!.GetValue<string>()=="7ffd8002").All(c=>c!["serial"]!.GetValue<int>()==c["source_index"]!.GetValue<int>()+100000&&c["y"]!.GetValue<int>()<64),"SE draw-commands: north serial + 100000, south y + 64");
        var g5=cmd.Single(c=>c!["layer"]!.GetValue<string>()=="ground"&&c["block"]!.GetValue<string>()=="7ffd8002"&&c["source_index"]!.GetValue<int>()==5)!;
        Check(g5["uk"]!.GetValue<int>()==7&&g5["offset"]!.GetValue<int>()==20&&g5["index"]!.GetValue<int>()==1,"SE ground row: index = v & 0xFF, uk = v >> 24, offset = 4 × i");
        var cover=cmd.Where(c=>c!["layer"]!.GetValue<string>()=="cover"&&c["block"]!.GetValue<string>()=="7ffd8002").Select(c=>c!["offset"]!.GetValue<int>()).Order().ToArray();
        Check(cover.SequenceEqual([32770,32776]),"SE cover rows: File Offset of each 6-byte row");
        string pixels=File.ReadAllText(Out(engineA,SceneRecipe.TilePixelsPath));
        var tp=JsonNode.Parse(pixels)!.AsArray();
        Check(tp.Select(t=>(t!["tile"]!.GetValue<int>(),t["index"]!.GetValue<int>(),t["type"]!.GetValue<int>())).SequenceEqual(new[]{(1,0,0),(1,1,1),(1,2,0),(1,3,16),(2,0,17),(3,0,2),(3,1,3),(3,2,6),(3,3,7)}),"SE tile-pixels: every used (tile, index), tile then index ascending, with the block type");
        Check(tp[0]!["pixels"]!.AsArray().Count==288&&tp[0]!["pixels"]![0]!.ToJsonString()=="[22,0,"+((977+0)&0x7FFF)+"]"&&tp[1]!["pixels"]![0]!.ToJsonString()=="[0,0,"+((2*977)&0x7FFF)+"]","SE tile-pixels: diamond rows 2(y+1) / 2(23−y), start 24 − n (type & 1 = 0) or 0");
        Check(tp[5]!["pixels"]!.ToJsonString()=="[[3,3,"+((6*131)&0x7FFF)+"],[4,3,"+((6*131+1)&0x7FFF)+"],[5,3,"+((6*131+2)&0x7FFF)+"],[3,4,"+((6*131+17)&0x7FFF)+"],[4,4,"+((6*131+18)&0x7FFF)+"],[5,4,"+((6*131+19)&0x7FFF)+"]]","SE tile-pixels: RLE x0, y0, skip / 2, run");
        Check(pixels.StartsWith("[{\"tile\":1,\"index\":0,\"type\":0,\"pixels\":[[22,0,",StringComparison.Ordinal),"SE tile-pixels key order and compact form");

        // opacity.json：indent 2、CRLF、檔尾沒有換行；maps[] 沒有 path。
        byte[] op=File.ReadAllBytes(Out(engineA,SceneRecipe.OpacityPath));string opText=Encoding.UTF8.GetString(op);
        Check(opText.StartsWith("{\r\n  \"schema\": \"p23-original-opacity-v1\",\r\n  \"maps\": [\r\n    {\r\n      \"block\": \"7ffd8003\",\r\n      \"sha256\": \"",StringComparison.Ordinal)&&op[^1]==(byte)'}'&&!opText.Replace("\r\n","").Contains('\n'),"SE opacity: indent 2, CRLF, no final newline, south first");
        var opj=JsonNode.Parse(op)!;
        Check(opj.AsObject().Select(p=>p.Key).SequenceEqual(["schema","maps","groups","triggers","ordering","scope"]),"SE opacity top-level keys");
        var maps=opj["maps"]!.AsArray();
        Check(maps.All(m=>m!.AsObject().Select(p=>p.Key).SequenceEqual(["block","sha256","slot","ybase","groups","triggers"]))&&maps[0]!["sha256"]!.GetValue<string>()==Sha(File.ReadAllBytes(Path.Combine(client,"map","0","7ffd8003.S32")))&&maps[1]!["slot"]!.GetValue<int>()==0&&maps[0]!["ybase"]!.GetValue<int>()==64,"SE opacity maps[]: no path, file SHA-256, slot / ybase");
        var groups=opj["groups"]!.AsArray();
        Check(groups.Select(g=>g!["key"]!.GetValue<string>()).SequenceEqual(["7ffd8003:0","7ffd8003:1","7ffd8002:0","7ffd8002:1"])&&groups[0]!["objects"]!.AsArray().Count==2&&groups[0]!["objects"]![0]!["globalY"]!.GetValue<int>()==70&&groups[2]!["objects"]![0]!["globalY"]!.GetValue<int>()==6&&groups[1]!["id"]!.GetValue<int>()==9,"SE opacity groups: south then north, objects with globalY = y + ybase");
        var trig=opj["triggers"]!.AsArray();
        Check(trig.Count==6&&trig.Select(t=>t!["admittedByColdSlot"]!.GetValue<bool>()).SequenceEqual([true,true,false,true,true,true])&&trig[1]!["bits"]!.GetValue<int>()==2&&trig[1]!["coldSlot"]!.GetValue<int>()==2&&trig[3]!["key"]!.GetValue<string>()=="7ffd8002:0",
            "SE opacity triggers: bits = 1 << type, admitted when y < 64 (slot 2) / 128 (slot 0)");

        // 產品讀取程式：Assets.LoadScene 與 OpacityCatalog 載得進來。
        var scene=new List<Draw>();var (w,h)=Assets.LoadScene(Dir(engineA),scene);
        Check(w==4608&&h==2304&&scene.Count==drawCount,"SE product Assets.LoadScene loads the prepared scene (order, bounds and tile types accepted)");
        var catalog=new OpacityCatalog(Dir(engineA));
        Check(catalog.Groups.Count==4&&catalog.Cells.Values.Sum(c=>c.Length)==5&&catalog.Groups["7ffd8003:0"].Nodes.Length==2,"SE product OpacityCatalog loads opacity.json: 4 groups, 5 admitted triggers");

        // 續跑、拒絕。
        Check(runner.Run(Request(client,engineA)).Outcome==AssetPrepOutcome.UpToDate,"SE scene: same request on an unchanged client is up to date");
        ApRefused(()=>runner.Run(Request(client,Case("scene-bad-item"),"maps")),AssetPrepFailure.InvalidRequest,"SE scene: unknown item refused");
        ApRefused(()=>runner.Run(Request(SeSceneClient(Case("scene-no-south"),withSouth:false),Case("scene-no-south-engine"))),AssetPrepFailure.MissingEntry,"SE scene: missing 7ffd8003 refused","map");
        ApRefused(()=>runner.Run(Request(SeSceneClient(Case("scene-no-til"),withTile3:false),Case("scene-no-til-engine"))),AssetPrepFailure.MissingEntry,"SE scene: missing TIL refused","Tile");
        ApRefused(()=>runner.Run(Request(SeSceneClient(Case("scene-short"),changeNorth:n=>n[..40000]),Case("scene-short-engine"))),AssetPrepFailure.CorruptEntry,"SE scene: truncated block refused","map");
        ApRefused(()=>runner.Run(Request(SeSceneClient(Case("scene-notail"),changeNorth:n=>n[..^2]),Case("scene-notail-engine"))),AssetPrepFailure.CorruptEntry,"SE scene: block without effect tail refused","map");
        ApRefused(()=>runner.Run(Request(SeSceneClient(Case("scene-trailing"),changeNorth:n=>[..n,1]),Case("scene-trailing-engine"))),AssetPrepFailure.CorruptEntry,"SE scene: trailing bytes refused","map");
    }

    // ---- map-effects ----
    sealed record SeEffect(int Id,int Flags,(int Frame,int Ticks,string Events)[] Sequence,SrSpr Spr);

    /// <summary>合成的特效 SPR：每幀一個 tile（a = f % 3 − 1、b = 0）、一個 block、三個像素（依列、再依 x 排序，與解碼順序相同）。</summary>
    static SrSpr SeSpr(int id,int frames,bool indexed=false,int[]? bounds=null,bool outside=false){
        var list=new List<SrFrame>();var blocks=new List<(int,int,ushort)[]>();
        for(int f=0;f<frames;f++){
            int s=id*7+f;
            blocks.Add([(1+s%5,2,(ushort)(s*97&0x7FFF)),(2+s%5,2,(ushort)(s*31&0x7FFF)),(outside?20:4,5+f%3,(ushort)((s+3)*13&0x7FFF))]);
            int a=f%3-1;var (ox,oy)=SprFile.Origin(a,0);
            list.Add(new(bounds??[ox-1,oy,ox+(outside?5:23),oy+23],[(a,0,10+f,f)]));
        }
        return new(list.ToArray(),blocks.ToArray(),indexed);
    }
    /// <summary>測試端自己寫的期望輸出（與 Python json.dumps(separators=(',', ':')) 同格式、檔尾 CRLF），不經過 Utf8JsonWriter。</summary>
    static byte[] SeExpectedJson(SeEffect e,string entrySha,string spriteSha){
        var sb=new StringBuilder();
        sb.Append("{\"id\":").Append(e.Id).Append(",\"flags\":").Append(e.Flags).Append(",\"sequence\":[");
        sb.Append(string.Join(",",e.Sequence.Select(s=>$"{{\"frame\":{s.Frame},\"ticks\":{s.Ticks},\"events\":\"{s.Events}\"}}")));
        sb.Append("],\"frames\":[");
        sb.Append(string.Join(",",e.Spr.Frames.Select((f,i)=>$"{{\"index\":{i},\"bounds\":[{string.Join(",",f.Bounds)}],\"blocks\":["
            +string.Join(",",f.Tiles.Select((t,k)=>$"{{\"a\":{t.A},\"b\":{t.B},\"z\":{t.Z},\"order\":{k},\"pixels\":[{string.Join(",",e.Spr.Blocks[t.Block].Select(p=>$"[{p.X},{p.Y},{p.C}]"))}]}}"))+"]}")));
        sb.Append("],\"entrySha256\":\"").Append(entrySha).Append("\",\"spriteSha256\":\"").Append(spriteSha).Append("\",\"tickRate\":24,\"eventPolicy\":\"Original event tokens preserved; sound and child emission not executed by this visual-only slice.\"}\r\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
    static string SeEntry(string list,int id){
        int start=list.IndexOf($"\n#{id}\t",StringComparison.Ordinal)+1;int end=list.IndexOf("\n#",start,StringComparison.Ordinal);
        return list[start..(end<0?list.Length:end+1)];
    }

    static void MapEffectChecks(AssetPrepRunner runner,Func<string,string> Case){
        string seq6016=string.Join(" ",new[]{11,0,1,2,3,4,5,6,7,8,9,10}.Select((f,i)=>$"0.{f}:5"+(i==1?"<3332<1340<1340":"")));
        string Lines(bool spz)=>
            "#1\t1 pad 0.idle(0 1,0.0:4)\r\n"+
            "#85\t2 lamp 0.idle(0 2,0.0:4 0.1:4<320) 104.attr(8)\r\n"+
            "#701\t1 still\r\n"+
            "#702\t1 twoframes\r\n"+
            "#703\t2 turn 3.idle(1 2,0.0:4 0.1:4)\r\n"+
            "#704\t1 twin 3.idle(0 1,0.0:4)\r\n"+
            "#706\t1 badbounds 3.idle(0 1,0.0:4)\r\n"+
            (spz?"#707\t1 differs 3.idle(0 1,0.0:6)\r\n":"#707\t1 differs 3.idle(0 1,0.0:4)\r\n")+
            "#708\t3 alt 3.idle(0 3,0.0:2 0.2:2!]85 0.1:3) 104.attr(128)\r\n"+
            "#709\t1 outside 3.idle(0 1,0.0:4) 104.attr(256)\r\n"+
            "#710\t1 oddflag 3.idle(0 1,0.0:4) 104.attr(5)\r\n"+
            "#711\t1 shortseq 3.idle(0 2,0.0:4)\r\n"+
            "#6016\t12 bell 3.idle(0 12,"+seq6016+")\r\n"+
            "#9000\t1 tail\r\n";
        string sprText=Lines(false),spzText="S1223\r\n"+Lines(true);
        var effects=new Dictionary<int,SeEffect>{
            [85]=new(85,8,[(0,4,""),(1,4,"<320")],SeSpr(85,2,indexed:true)),
            [701]=new(701,0,[(0,1,"")],SeSpr(701,1)),
            [708]=new(708,128,[(0,2,""),(2,2,"!]85"),(1,3,"")],SeSpr(708,3)),
            [9000]=new(9000,0,[(0,1,"")],SeSpr(9000,1)),
            [6016]=new(6016,0,new[]{11,0,1,2,3,4,5,6,7,8,9,10}.Select((f,i)=>(f,5,i==1?"<3332<1340<1340":"")).ToArray(),SeSpr(6016,12,indexed:true)),
        };
        var files=new List<(int Archive,string Name,byte[] Data)>{
            (0,"85-0.spr",SrEncode(effects[85].Spr)),(0,"701-0.spr",SrEncode(effects[701].Spr)),(0,"702-0.spr",SrEncode(SeSpr(702,2))),
            (0,"703-0.spr",SrEncode(SeSpr(703,2))),(0,"704-0.spr",SrEncode(SeSpr(704,1))),(16,"704-0.spr",SrEncode(SeSpr(7040,1))),
            (0,"706-0.spr",SrEncode(SeSpr(706,1,bounds:[10,0,5,5]))),(0,"707-0.spr",SrEncode(SeSpr(707,1))),
            (4,"708-0.spr",SrEncode(effects[708].Spr)),(8,"708-0.spr",SrEncode(effects[708].Spr)),
            (0,"709-0.spr",SrEncode(SeSpr(709,1,outside:true))),(0,"710-0.spr",SrEncode(SeSpr(710,1))),(0,"711-0.spr",SrEncode(SeSpr(711,1))),
            (14,"6016-0.SPR",SrEncode(effects[6016].Spr)),(0,"9000-0.spr",SrEncode(SeSpr(9000,1))),(0,"9999-0.spr",SrEncode(SeSpr(9999,1))),
        };
        var expected=effects.ToDictionary(e=>e.Key,e=>{var f=files.First(x=>x.Name.StartsWith(e.Key+"-0.",StringComparison.Ordinal));return SeExpectedJson(e.Value,Sha(Encoding.ASCII.GetBytes(SeEntry(sprText,e.Key))),Sha(f.Data));});
        int[] tail=[6016,85,700,701,702,703,704,705,706,707,708,709,710,711,85];
        AssetPrepPins MakeClient(string dir,int[] ids,Func<AssetPrepPins,AssetPrepPins>? pins=null){
            var groups=files.GroupBy(f=>f.Archive).ToDictionary(g=>g.Key,g=>g.Select(f=>(f.Name,f.Data)).ToList());
            for(int i=0;i<17;i++){var items=groups.GetValueOrDefault(i)??[];items.Add(($"pad{i}.img",Img(1,1,0,0)));Archive(dir,SpriteName(i),items.ToArray());}
            byte[] spr=Encoding.ASCII.GetBytes(sprText),spz=Encoding.ASCII.GetBytes(spzText);
            TxArchive(dir,("list.spr",spr),("list.spz",SpriteList.EncodeSpz(spz)));
            byte[] Plain(int[]? e,int trailing=0)=>SeS32(_=>1u<<8,[],[],[],e,trailing);
            void Put(string rel,byte[] data){string full=Path.Combine(dir,rel.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllBytes(full,data);}
            Put("map/0/7e007e00.s32",Plain(ids));Put("map/0/7e017e00.S32",Plain(null));Put("map/0/7e027e00.s32",Plain([9000]));
            Put("map/0/tempseg.s32",Plain([9999]));                        // not a block name (D3): its ids are not read
            Put("map/5/7e007e00.s32",Plain([9999]));Put("map/5/7e017e00.s32",Plain([9999],trailing:3));   // map 5 is unsupported (D3): no ids
            var p=new AssetPrepPins(Sha(spz)){ListSprSha256=Sha(spr),Effect6016Sha256=Sha(expected[6016])};
            return pins?.Invoke(p)??p;
        }
        AssetPrepRequest Request(string client,string engine,AssetPrepPins pins)=>new(client,engine,MapEffectsRecipe.RecipeId,[MapEffectsRecipe.EffectsItem]){Pins=pins};
        string Dir(string engine)=>Path.Combine(engine,"assets",MapEffectsRecipe.RecipeId);
        string Out(string engine,string rel)=>Path.Combine(Dir(engine),rel.Replace('/',Path.DirectorySeparatorChar));

        // --- 純函式：metadata 切段、SPR 解碼的拒絕原因。
        byte[] sprBytes=Encoding.ASCII.GetBytes(sprText);
        Check(Encoding.ASCII.GetString(MapEffectsRecipe.MetadataEntry(sprBytes,85))=="#85\t2 lamp 0.idle(0 2,0.0:4 0.1:4<320) 104.attr(8)\r\n"&&Encoding.ASCII.GetString(MapEffectsRecipe.MetadataEntry(sprBytes,9000))=="#9000\t1 tail\r\n","SE metadata entry: from '#<id>' + blank to the next line starting '#<digits>' + blank");
        Check(Encoding.ASCII.GetString(MapEffectsRecipe.MetadataEntry("#7 a\n#70 b\n#7x c\n#8 d"u8,7))=="#7 a\n"&&Encoding.ASCII.GetString(MapEffectsRecipe.MetadataEntry("#70 b\n#7x\n#8 d"u8,70))=="#70 b\n#7x\n","SE metadata entry: '#70' is not '#7'; '#7x' does not end an entry");
        string Reason(Action a){try{a();return "ok";}catch(MapEffectUnavailable ex){return ex.Reason;}}
        Check(Reason(()=>MapEffectsRecipe.MetadataEntry(sprBytes,700))=="no original animation metadata","SE no metadata → 'no original animation metadata'");
        Check(Reason(()=>MapEffectSpr.Decode(SrEncode(SeSpr(706,1,bounds:[10,0,5,5]))))=="invalid frame dimensions"&&Reason(()=>MapEffectSpr.Decode(SrEncode(SeSpr(709,1,outside:true))))=="opaque pixel outside frame"
            &&Reason(()=>MapEffectSpr.Decode([..SrEncode(SeSpr(1,1)),0]))=="trailing bytes"&&Reason(()=>MapEffectSpr.Decode(SrEncode(SeSpr(1,1))[..18]))=="truncated at 15"&&Reason(()=>MapEffectSpr.Decode([0]))=="invalid frame count"
            &&Reason(()=>MapEffectSpr.Decode(SrEncode(new([new([0,0,23,23],[],true)],[[(0,0,1)]]))))=="invalid tile count","SE SPR refusals carry the baseline reason strings");

        // --- 兩個輸出目錄。
        string client=Case("fx-client");var pins=MakeClient(client,tail);string before=ApTreeHash(client);
        string pair=Case("fx-two-dirs"),engineA=Path.Combine(pair,"引擎 甲"),engineB=Path.Combine(pair,"engine-b");
        var a=runner.Run(Request(client,engineA,pins));var b=runner.Run(Request(client,engineB,pins));
        Check(a.Outcome==AssetPrepOutcome.Published&&b.Outcome==AssetPrepOutcome.Published,"SE map-effects published in both directories");
        var produced=new[]{85,701,708,6016,9000};
        string[] outputs=[..produced.Select(MapEffectsRecipe.FilePath),MapEffectsRecipe.ManifestPath];
        Check(ApNames(Path.Combine(Dir(engineA),"maps","effects")).SequenceEqual(produced.Select(i=>i+".json").Append("manifest.json").Order(StringComparer.Ordinal)),"SE map-effects: one file per available id plus the manifest");
        foreach(string rel in outputs.Append(AssetReceipt.FileName))Check(File.ReadAllBytes(Out(engineA,rel)).AsSpan().SequenceEqual(File.ReadAllBytes(Out(engineB,rel))),"SE map-effects byte-identical in both directories: "+rel);
        foreach(string rel in outputs.Append(AssetReceipt.FileName)){
            string text=File.ReadAllText(Out(engineA,rel));
            Check(!text.Contains(":\\",StringComparison.Ordinal)&&!text.Contains("引擎",StringComparison.Ordinal)&&!text.Contains("scratch",StringComparison.OrdinalIgnoreCase),"SE map-effects: no absolute path in "+rel);
        }
        Check(ApTreeHash(client)==before,"SE map-effects: client left untouched");
        foreach(var (id,bytes) in expected)Check(File.ReadAllBytes(Out(engineA,MapEffectsRecipe.FilePath(id))).AsSpan().SequenceEqual(bytes),$"SE effect {id}.json: exact bytes (json.dumps separators=(',', ':'), key order, CRLF at the end)");

        var manifestBytes=File.ReadAllBytes(Out(engineA,MapEffectsRecipe.ManifestPath));
        Check(manifestBytes.AsSpan().EndsWith("}\r\n"u8)&&manifestBytes.Count(c=>c==(byte)'\n')==1&&manifestBytes[0]==(byte)'{',"SE effects manifest: compact, one CRLF at the end");
        var m=JsonNode.Parse(manifestBytes)!;
        Check(m.AsObject().Select(p=>p.Key).SequenceEqual(["schema","effects","unavailable","scope"])&&m["schema"]!.GetValue<string>()=="p24-map-effects-v1","SE effects manifest top-level keys and schema");
        var rows=m["effects"]!.AsArray();
        Check(rows.Select(r=>r!["id"]!.GetValue<int>()).SequenceEqual(produced),"SE effects manifest: ids ascending (9000 from a block of the same map; tempseg / map 5 ids not read)");
        Check(rows.All(r=>r!.AsObject().Select(p=>p.Key).SequenceEqual(["id","path","sha256","flags","frames","source","metadataSha256","sequence"])&&r["source"]!.AsObject().Select(p=>p.Key).SequenceEqual(["archive","name","size","sha256"])),"SE effects manifest row keys; source = archive, name, size, sha256");
        var r6016=rows.Single(r=>r!["id"]!.GetValue<int>()==6016)!;var r708=rows.Single(r=>r!["id"]!.GetValue<int>()==708)!;
        Check(r6016["source"]!["archive"]!.GetValue<string>()=="Sprite13"&&r6016["source"]!["name"]!.GetValue<string>()=="6016-0.SPR"&&r6016["path"]!.GetValue<string>()=="6016.json"&&r6016["sha256"]!.GetValue<string>()==Sha(expected[6016])&&r6016["frames"]!.GetValue<int>()==12,
            "SE 6016 row: Sprite13, entry name as listed, SHA-256 of the output file");
        Check(r708["source"]!["archive"]!.GetValue<string>()=="Sprite03"&&r708["flags"]!.GetValue<int>()==128&&r708["sequence"]![1]!["events"]!.GetValue<string>()=="!]85","SE 708: identical copies in two alternates accepted, first container recorded; events copied");
        Check(rows.Single(r=>r!["id"]!.GetValue<int>()==85)!["metadataSha256"]!.GetValue<string>()==Sha(Encoding.ASCII.GetBytes(SeEntry(sprText,85))),"SE metadataSha256 = SHA-256 of the entry bytes");
        var un=m["unavailable"]!.AsArray().Select(u=>(u!["id"]!.GetValue<int>(),u["error"]!.GetValue<string>())).ToArray();
        Check(un.SequenceEqual(new[]{(700,"no original animation metadata"),(702,"multiple frames without animation metadata"),(703,"unresolved stationary direction"),(704,"ambiguous archive content"),
            (705,"no sprite candidate"),(706,"invalid frame dimensions"),(707,"list.spr/SPZ disagree"),(709,"opaque pixel outside frame"),(710,""),(711,"")}),"SE unavailable ids and reasons: "+string.Join(";",un.Select(u=>$"{u.Item1}={u.Item2}")));
        Check(m["scope"]!.GetValue<string>()=="Original stationary metadata sequences and source pixels, not live animation acceptance.","SE effects manifest scope string");

        // --- 產品讀取程式：MapEffectCatalog（integrity 取 manifest 的 SHA）、Effect6016Definition。
        string tree=Case("fx-tree");Directory.CreateDirectory(Path.Combine(tree,"maps","effects"));
        foreach(string rel in outputs)File.Copy(Out(engineA,rel),Path.Combine(tree,rel.Replace('/',Path.DirectorySeparatorChar)));
        File.WriteAllText(Path.Combine(tree,"integrity.json"),MpIntegrity("tw-assets-integrity-v2",(MapEffectsRecipe.ManifestPath,manifestBytes)));
        var catalog=new MapEffectCatalog(Path.Combine(tree,"maps"));
        var e85=catalog.Get(85);var e6016=catalog.Get(6016);
        Check(e85.Flags==8&&e85.Frames.Length==2&&e85.Sequence.Length==2&&e85.Frames[1].Blocks[0].Tile.Pixels.Length==3&&e6016.Frames.Length==12&&catalog.Get(9000).Sequence.Single().Ticks==1,"SE product MapEffectCatalog loads the prepared effects");
        Check(MpThrows<NotSupportedException>(()=>catalog.Get(700),"SE product MapEffectCatalog: unavailable id not packaged").Message.Contains("700",StringComparison.Ordinal),"SE unavailable id raises NotSupportedException");
        byte[] fx6016=File.ReadAllBytes(Path.Combine(tree,"maps","effects","6016.json"));
        Check(Effect6016Definition.FromR27Metadata(fx6016,Sha(fx6016)).MetadataSha256==Sha(fx6016),"SE product Effect6016Definition accepts the prepared 6016 shape");
        MpThrows<InvalidDataException>(()=>Effect6016Definition.FromR27Metadata(fx6016,Effect6016Definition.AcceptedMetadataSha256),"SE synthetic 6016 is refused by the real pin (the pin is not relaxed)");

        // --- 續跑、錨點、拒絕。
        Check(runner.Run(Request(client,engineA,pins)).Outcome==AssetPrepOutcome.UpToDate,"SE map-effects: unchanged client is up to date");
        string wrongPin=new('0',64);
        var ex6016=ApRefused(()=>runner.Run(Request(client,Case("fx-6016-pin"),pins with{Effect6016Sha256=wrongPin})),AssetPrepFailure.ContainerMismatch,"SE 6016 output differs from the pin → recipe stops","Sprite13");
        Check(ex6016.Message.Contains("6016.json",StringComparison.Ordinal)&&ex6016.Message.Contains("6016-0.SPR",StringComparison.Ordinal),"SE 6016 refusal names the output and the entry");
        string no6016=Case("fx-no-6016");var pinsNo=MakeClient(no6016,[85]);
        ApRefused(()=>runner.Run(Request(no6016,Case("fx-no-6016-engine"),pinsNo)),AssetPrepFailure.MissingEntry,"SE 6016 not referenced while the anchor is set → refused");
        Check(runner.Run(Request(no6016,Case("fx-no-anchor-engine"),pinsNo with{Effect6016Sha256=null})).Outcome==AssetPrepOutcome.Published,"SE without a 6016 anchor (synthetic only) the recipe runs");
        ApRefused(()=>runner.Run(Request(client,Case("fx-spr-anchor"),pins with{ListSprSha256=wrongPin})),AssetPrepFailure.ContainerMismatch,"SE list.spr anchor mismatch refused","Text");
        ApRefused(()=>runner.Run(Request(client,Case("fx-spz-anchor"),pins with{ListSpzSha256=wrongPin})),AssetPrepFailure.ContainerMismatch,"SE list.spz anchor mismatch refused","Text");
        ApRefused(()=>runner.Run(Request(client,Case("fx-no-spr-anchor"),pins with{ListSprSha256=null})),AssetPrepFailure.InvalidRequest,"SE no list.spr anchor refused");
        ApRefused(()=>runner.Run(new(client,Case("fx-bad-item"),MapEffectsRecipe.RecipeId,["6016"]){Pins=pins}),AssetPrepFailure.InvalidRequest,"SE map-effects: unknown item refused");
        File.WriteAllBytes(Path.Combine(client,"map","0","7e027e00.s32"),SeS32(_=>1u<<8,[],[],[],[9000,85]));
        var c=runner.Run(Request(client,engineA,pins));
        Check(c.Outcome==AssetPrepOutcome.Published&&c.Produced.SequenceEqual([MapEffectsRecipe.EffectsItem]),"SE map-effects: a changed block makes the resumed run produce again");
    }
}
