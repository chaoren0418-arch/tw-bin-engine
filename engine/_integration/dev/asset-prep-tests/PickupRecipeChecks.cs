using P06;
using P06.AssetPrep;
using System.Text;
using System.Text.Json;

// T463（#1048）：pickup recipe 的離線測試。list.spz、Text、17 個 Sprite 容器與每一個 .spr 都由測試合成（像素是依編號算出的
// 測試值，不是任何原客戶端的圖），寫在 scratch/issue-1048/run-<時間>/ 下；不讀客戶端、不需要遊戲、網路或 DB。測試本身不做遞迴刪除。
// 驗收重點：追加幀（每方向第 1–3 步）由 SPR 產生、第 0 步留在 body profile；frames 依方向 0–7、每方向第 1、2、3 步；
// 產品的 PickupAssets 從 assets 樹（integrity.json 核對）載入，合成順序是 [第 0 步（body profile）, 1, 2, 3]；
// 哪些 (gfx, weapon) 有 pickup profile 跟著 players manifest 走，動作 15 不合規或 body 沒有第 0 步幀的不收。
// 群組 asset-prep-pickup；同時掛在 CI 已在跑的 native-ui-resources 之下（與 asset-prep-spr-b 相同做法，不改 build.yml）。
partial class Checks {
    [CheckGroup("asset-prep-pickup",Order=1),CheckGroup("native-ui-resources",Order=64),CheckGroup(CheckGroups.Default)]
    static void PickupRecipeChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1048","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        string Active(string engine,string id)=>Path.Combine(engine,"assets",id);

        // --- 0. 自動發現、先後與預熱清單。
        var product=AssetRecipeDiscovery.Product;
        var recipe=product.Recipes.SingleOrDefault(r=>r.Id=="pickup");
        Check(product.Errors.Count==0&&recipe is {Coverage:AssetRecipeCoverage.Partial}&&recipe.Requires.SequenceEqual(new[]{"objects","players"}),"PK pickup recipe discovered once, partial, after objects and players (asset-recipe.md 6 order 3)");
        Check(PreheatList.Items("pickup").SequenceEqual(new[]{"pickup"})&&recipe!.Normalize(PreheatList.Items("pickup")).SequenceEqual(new[]{"pickup"}),"PK preheat list: the single item pickup");

        // --- 合成的動畫表。0 與 61 有合規的 15.get；37 的 ticks 不是 1,1,4,4；48 沒有動作 15；1186 的第 0 步幀不在 body profile 裡。
        const string body=" 0.walk(1 1,0.0:4) 1.attack(1 1,8.0:3!) 2.damage(1 1,16.0:2) 3.breath(1 1,0.1:4) 8.death(1 1,16.1:5) 18.spell dir(1 1,8.1:2!) 19.spell nodir(1 1,8.2:2!)";
        const string list=
            "S1223\r\n"+
            "#0\t1=3225 prince"+body+" 46.walk x(1 1,0.0:4) 47.attack x(1 1,8.0:3!) 48.damage x(1 1,16.0:2) 49.breath x(1 1,0.1:4) 15.get(1 4,0.0:1 88.0:1 88.1:4<206 88.2:4)\r\n"+
            "#37\t1=3474 elf"+body+" 15.get(1 4,0.0:1 24.0:2 24.1:4<206 24.2:4)\r\n"+
            "#48\t1=3215 wizard"+body+"\r\n"+
            "#61\t1=3213 knight"+body+" 20.walk bow(1 1,0.0:4) 21.attack bow(1 2,8.0:5 8.1:5!) 22.damage bow(1 1,16.0:3) 23.breath bow(1 1,0.2:4) 15.get(1 4,8.0:1 24.0:1 24.1:4<206 24.2:4)\r\n"+
            "#1186\t1=3221 darkelf"+body+" 15.get(1 4,40.0:1 24.0:1 24.1:4<206 24.2:4)\r\n";
        var sprs=new Dictionary<string,SrSpr>();var files=new List<(int,string,byte[])>();
        void Put(int archive,int sprite,int sid,SrSpr spr){string name=$"{sprite}-{sid}.spr";sprs[name]=spr;files.Add((archive,name,SrEncode(spr)));}
        foreach(int sprite in new[]{3225,3474,3215,3213,3221})for(int sid=0;sid<24;sid++)Put(0,sprite,sid,SbWide(sprite,sid,3));
        for(int d=0;d<8;d++){
            Put(0,3225,88+d,SrMake(3225,88+d,3,emptyAt:d==2?1:-1));          // 90:1 is an empty frame (bounds kept, no blocks)
            Put(d%2==0?0:7,3213,24+d,SrMake(3213,24+d,3));                     // half of the knight's pickup files only in Sprite06
            Put(0,3221,24+d,SrMake(3221,24+d,3));Put(0,3221,40+d,SrMake(3221,40+d,1));
        }
        string client=Case("client");var pins=SrClient(client,list,files);
        string clientBefore=ApTreeHash(client);
        var runner=AssetPrepRunner.Product();
        AssetPrepRequest Request(string id,string engine,params string[] items)=>new(client,engine,id,items){Pins=pins};
        string[] objectItems=["0","manifest"];
        string[] playerItems=["0-0","0-46","37-0","48-0","61-0","61-20","1186-0","manifest"];

        // --- 1. 先後順序錯誤、缺基底、不合法項目：明確失敗，什麼都不發布。
        {
            string engine=Case("order");
            var ex=ApRefused(()=>runner.Run(Request("pickup",engine,"pickup")),AssetPrepFailure.Prerequisite,"PK pickup before objects and players");
            Check(!Directory.Exists(Active(engine,"pickup")),"PK nothing published when the order is wrong");
            runner.Run(Request("objects",engine,objectItems));
            ApRefused(()=>runner.Run(Request("pickup",engine,"pickup")),AssetPrepFailure.Prerequisite,"PK pickup after objects but before players");
            runner.Run(Request("players",engine,"61-0","manifest"));
            ex=ApRefused(()=>runner.Run(Request("pickup",engine,"pickup")),AssetPrepFailure.Prerequisite,"PK players without the gfx 0 / weapon 0 base");
            Check(ex.Message.Contains("gfx 0 weapon 0",StringComparison.Ordinal)&&!Directory.Exists(Active(engine,"pickup")),"PK the refusal names the missing base and publishes nothing — "+ex.Message);
            foreach(string item in new[]{"0","base","pickup-1","manifest"})ApRefused(()=>runner.Run(Request("pickup",engine,item)),AssetPrepFailure.InvalidRequest,$"PK item \"{item}\"");
        }

        // --- 2. 正常順序：兩個目錄（其一含中文與空白）逐 byte 相同。
        string pair=Case("two-dirs"),engineA=Path.Combine(pair,"引擎 甲"),engineB=Path.Combine(pair,"engine-b");
        foreach(string engine in new[]{engineA,engineB}){
            runner.Run(Request("objects",engine,objectItems));
            runner.Run(Request("players",engine,engine==engineA?playerItems:playerItems.Reverse().ToArray()));
            var r=runner.Run(Request("pickup",engine,"pickup","pickup"));
            Check(r.Produced.SequenceEqual(new[]{"pickup"}),"PK one item (duplicates collapse)");
        }
        Check(ApTreeHash(Active(engineA,"pickup"))==ApTreeHash(Active(engineB,"pickup")),"PK outputs and receipt byte-identical in two directories");
        Check(ApNames(Path.Combine(Active(engineA,"pickup"),"pickup")).SequenceEqual(new[]{"pickup-own-base15-gfx0-weapon0.json","pickup-own-base15-profile-extension.json"}),"PK writes exactly the two pickup files");
        var receipt=AssetReceipt.TryLoad(Path.Combine(Active(engineA,"pickup"),AssetReceipt.FileName))!;
        var done=receipt.Completed.Single();
        Check(done.Metadata["groups"]=="2"&&done.Metadata["profiles"]=="4"&&done.Metadata["frames"]=="48","PK receipt: 2 groups, 4 profiles, 48 embedded frames");
        Check(done.Metadata.TryGetValue("skipped:37-0",out var s37)&&s37.Contains("ticks",StringComparison.Ordinal)&&done.Metadata.TryGetValue("skipped:48-0",out var s48)&&s48.Contains("no action 15",StringComparison.Ordinal)
            &&done.Metadata.TryGetValue("skipped:1186-0",out var s1186)&&s1186.Contains("40:0",StringComparison.Ordinal),"PK stances that cannot take the rule are skipped with a reason (ticks / no action 15 / step-0 frame not in the body)");
        Check(done.Sources.Any(s=>s.Container=="assets/players"&&s.Entry=="players/manifest.json")&&done.Sources.Any(s=>s.Container=="assets/objects"&&s.Entry=="objects/0.json")&&done.Sources.Any(s=>s.Container=="assets/players"&&s.Entry=="players/61-20.json")
            &&done.Sources.Any(s=>s.Entry=="3213-25.spr"&&s.Container=="Sprite06"),"PK receipt records the players manifest, the body profiles read and the SPR entries (alternate container included)");
        foreach(string file in Directory.GetFiles(Active(engineA,"pickup"),"*.json",SearchOption.AllDirectories)){
            string text=File.ReadAllText(file);
            Check(!text.Contains(client,StringComparison.OrdinalIgnoreCase)&&!text.Contains(JsonSerializer.Serialize(client)[1..^1],StringComparison.OrdinalIgnoreCase)&&!text.Contains(root,StringComparison.OrdinalIgnoreCase)&&!text.Contains(JsonSerializer.Serialize(root)[1..^1],StringComparison.OrdinalIgnoreCase)
                &&!text.Contains("source_offset",StringComparison.Ordinal)&&!text.Contains("block_index",StringComparison.Ordinal)&&!text.Contains("logicalName",StringComparison.Ordinal),"PK "+Path.GetFileName(file)+": no path, no provenance-only block field, no embedded-resource name");
        }

        // --- 3. 檔案內容：序列、步序、frames 的順序與像素。
        JsonElement Doc(string rel)=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Active(engineA,"pickup"),rel))).RootElement;
        var b=Doc(PickupRecipe.BasePath);var x=Doc(PickupRecipe.ExtensionPath);
        string[] KeysOf(JsonElement group)=>group.GetProperty("frames").EnumerateArray().Select(f=>f.S("key")).ToArray();
        string[] Expected(int baseSid)=>Enumerable.Range(0,8).SelectMany(d=>Enumerable.Range(0,3).Select(f=>$"{baseSid+d}:{f}")).ToArray();
        Check(b.S("schema")=="pickup-own-base15-gfx0-weapon0-v1"&&b.I("gfx")==0&&b.I("weapon")==0&&b.I("sprite")==3225&&b.S("baseProfilePath")=="objects/0.json"
            &&b.S("baseProfileSha256")==Sha(File.ReadAllBytes(Path.Combine(Active(engineA,"objects"),"objects","0.json")))&&b.GetProperty("frameOrder").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(new[]{1,2,3,0}),
            "PK base identity: schema, gfx 0 / weapon 0, sprite from list.spz, objects/0.json and its SHA-256, frameOrder [1,2,3,0]");
        Check(KeysOf(b).SequenceEqual(Expected(88)),"PK base frames: direction 0–7, each direction steps 1, 2, 3 (88:0 88:1 88:2 89:0 …)");
        var seq=b.GetProperty("sequences").EnumerateArray().ToArray();
        Check(seq.Length==8&&seq.Select((s,d)=>s.I("direction")==d&&s.GetProperty("steps").EnumerateArray().Select(t=>(t.S("key"),t.I("ticks"),t.S("events"),t.I("metadataIndex"),t.S("storage")))
            .SequenceEqual(new[]{($"{d}:0",1,"",0,"base-profile"),($"{88+d}:0",1,"",1,"embedded"),($"{88+d}:1",4,"<206",2,"embedded"),($"{88+d}:2",4,"",3,"embedded")})).All(v=>v),
            "PK base sequences: 8 directions × 4 steps; step 0 base-profile (key d:0), steps 1–3 embedded; ticks 1,1,4,4; events copied");
        var groups=x.GetProperty("groups").EnumerateArray().ToArray();
        Check(x.S("schema")=="pickup-own-base15-profile-extension-v1"&&x.GetProperty("requiresResource").S("path")==PickupRecipe.BasePath
            &&x.GetProperty("requiresResource").S("sha256")==Sha(File.ReadAllBytes(Path.Combine(Active(engineA,"pickup"),PickupRecipe.BasePath))),"PK extension names the base file by path and SHA-256");
        Check(groups.Length==1&&groups[0].I("gfx")==61&&groups[0].I("sprite")==3213&&KeysOf(groups[0]).SequenceEqual(Expected(24))
            &&groups[0].GetProperty("sequences").EnumerateArray().All(s=>s.GetProperty("steps")[0].S("key")==$"{8+s.I("direction")}:0"),"PK extension groups: only 61 (37, 48 and 1186 skipped); its step 0 is the 8+d:0 frame of the body");
        var rows=x.GetProperty("profiles").EnumerateArray().Select(p=>(p.I("gfx"),p.I("weapon"),p.I("sequenceGroup"),p.I("sprite"),p.S("bodySource"),p.S("baseProfilePath"))).ToArray();
        Check(rows.SequenceEqual(new[]{(0,46,0,3225,"r34-base-resource","players/0-46.json"),(61,0,61,3213,"extension","players/61-0.json"),(61,20,61,3213,"extension","players/61-20.json")}),"PK extension profiles follow players/manifest.json (by gfx, weapon): "+string.Join(" ",rows));
        var blockKeys=b.GetProperty("frames")[0].GetProperty("blocks")[0].EnumerateObject().Select(p=>p.Name).ToArray();
        Check(blockKeys.SequenceEqual(new[]{"a","b","z","source_order","pixels"}),"PK blocks keep only a, b, z, source_order, pixels");
        var empty=b.GetProperty("frames").EnumerateArray().Single(f=>f.S("key")=="90:1");
        Check(empty.GetProperty("blocks").GetArrayLength()==0&&empty.A("bounds").SequenceEqual(new[]{352,240,-288,-240}),"PK empty SPR frame: bounds kept, no blocks");

        // 組一棵 assets 樹（T467 的縮小版），附 integrity.json，給產品的讀取程式用。
        string tree=Case("tree");var integrity=new List<object>();
        foreach(string id in new[]{"objects","players","pickup"})
            foreach(string file in Directory.GetFiles(Active(engineA,id),"*",SearchOption.AllDirectories)){
                string rel=Path.GetRelativePath(Active(engineA,id),file).Replace('\\','/');
                if(rel==AssetReceipt.FileName)continue;
                string target=Path.Combine(tree,rel);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
                byte[] data=File.ReadAllBytes(file);integrity.Add(new{path=rel,size=data.Length,sha256=Sha(data)});
            }
        File.WriteAllBytes(Path.Combine(tree,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new{schema="p06-assets-integrity-v1",files=integrity}));

        // --- 4. 產品 PickupAssets：從 assets 樹載入、適用清單、合成順序與像素。
        {
            var pickup=PickupAssets.Load(tree);
            Check(pickup.GroupCount==2&&pickup.ProfileCount==4,"PK PickupAssets.Load: 2 groups, 4 profiles from the prepared tree");
            Check(pickup.Supports(0,0)&&pickup.Supports(0,46)&&pickup.Supports(61,0)&&pickup.Supports(61,20)&&!pickup.Supports(61,4)&&!pickup.Supports(37,0)&&!pickup.Supports(48,0)&&!pickup.Supports(1186,0)&&!pickup.Supports(0,4),
                "PK Supports follows the prepared data (no table in code)");
            var profiles=PlayerProfiles.LoadManifest(tree);
            foreach(var (gfx,weapon,stepBase,embeddedBase,sprite) in new[]{(0,0,0,88,3225),(0,46,0,88,3225),(61,0,8,24,3213),(61,20,8,24,3213)}){
                var profile=profiles[(gfx,weapon)];var appearance=PlayerProfiles.Load(tree,profile);
                for(int d=0;d<8;d++){
                    var frames=pickup.For(appearance,d,weapon,profile);
                    Check(frames is {Length:4}&&ReferenceEquals(frames[0],appearance.Frames[$"{stepBase+d}:0"])
                        &&frames.Skip(1).Select(f=>f.Key).SequenceEqual(new[]{$"{embeddedBase+d}:0",$"{embeddedBase+d}:1",$"{embeddedBase+d}:2"})
                        &&frames.Skip(1).Select((f,i)=>SbFrameMatches(f,sprs[$"{sprite}-{embeddedBase+d}.spr"],i)).All(v=>v),
                        $"PK composition {gfx}-{weapon} direction {d}: [body step 0, embedded 1, 2, 3] with the synthetic SPR pixels");
                }
            }
            var p61=profiles[(61,0)];var a61=PlayerProfiles.Load(tree,p61);
            Check(pickup.For(a61,8,0,p61)==null&&pickup.For(a61,0,0,null)==null&&pickup.For(a61,0,0,p61 with{Sha256=new string('0',64)})==null&&pickup.For(a61,0,4,profiles[(61,0)])==null,
                "PK For refuses a bad direction, a missing or different player profile and an unsupported stance");
        }

        // --- 5. 產品拒收：沒準備、integrity 不符、extension 與基底不配。
        {
            string bare=Case("bare");Directory.CreateDirectory(bare);
            File.WriteAllBytes(Path.Combine(bare,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new{schema="p06-assets-integrity-v1",files=Array.Empty<object>()}));
            Throws<InvalidDataException>(()=>PickupAssets.Load(bare),"PK a tree without pickup/* (e.g. the r29 baseline) is refused","not prepared");
            string tampered=Path.Combine(tree,"pickup","pickup-own-base15-profile-extension.json");byte[] original=File.ReadAllBytes(tampered);
            File.WriteAllBytes(tampered,original.Append((byte)' ').ToArray());
            Throws<InvalidDataException>(()=>PickupAssets.Load(tree),"PK a changed pickup file fails integrity","integrity");
            File.WriteAllBytes(tampered,original);
            byte[] baseBytes=File.ReadAllBytes(Path.Combine(tree,"pickup","pickup-own-base15-gfx0-weapon0.json"));
            string ext=Encoding.UTF8.GetString(original);
            Throws<InvalidDataException>(()=>new PickupAssets(baseBytes,Encoding.UTF8.GetBytes(ext.Replace(Sha(baseBytes),new string('a',64)))),"PK extension whose requiresResource SHA is not the base","extension identity");
            Throws<InvalidDataException>(()=>new PickupAssets(baseBytes[..^1].Concat(new[]{(byte)' ',(byte)'}'}).ToArray(),original),"PK extension refuses a different base file","extension identity");
            Check(PickupAssets.Load(tree).ProfileCount==4,"PK the restored tree loads again");
        }

        // --- 6. 續跑：來源沒變就是最新；players 重產（少一個姿勢）後重做，適用清單跟著變。
        {
            Check(runner.Run(Request("pickup",engineA,"pickup")).Outcome==AssetPrepOutcome.UpToDate,"PK pickup again with players unchanged: up to date");
            runner.Run(Request("players",engineA,playerItems.Where(i=>i!="61-20").ToArray()));
            var again=runner.Run(Request("pickup",engineA,"pickup"));
            Check(again.Produced.SequenceEqual(new[]{"pickup"}),"PK players changed: pickup produced again");
            var rows2=Doc(PickupRecipe.ExtensionPath).GetProperty("profiles").EnumerateArray().Select(p=>(p.I("gfx"),p.I("weapon"))).ToArray();
            Check(rows2.SequenceEqual(new[]{(0,46),(61,0)}),"PK the profile list follows the new players manifest");
        }

        // --- 7. 整輪下來原客戶端沒有任何變動。
        Check(ApTreeHash(client)==clientBefore,"PK client directory is byte-identical after every run (read-only input)");
    }
}
