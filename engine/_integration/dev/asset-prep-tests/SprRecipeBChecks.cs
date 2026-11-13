using P06;
using P06.AssetPrep;
using System.Text.Json;

// T455（#1040）：players、shops、doors、appearance-lists、shadows 五個 recipe 與「讀先前 recipe 輸出」核心介面的離線測試。
// list.spz、Text、17 個 Sprite 容器與每一個 .spr 都由測試合成（像素是依編號算出的測試值，不是任何原客戶端的圖），
// 寫在 scratch/issue-1040/run-<時間>/ 下；不讀客戶端、不需要遊戲、網路或 DB。測試本身不做遞迴刪除。
// 驗收重點：每類輸出由產品既有的讀取程式（PlayerProfiles、PlayerShadowCatalog、OriginalAppearanceCatalog、ObjectAppearance）
// 載入；先後順序錯誤（前一個 recipe 沒發布、沒產生需要的檔、或之後被改掉）時明確失敗且不發布。
// 群組 asset-prep-spr-b；同時掛在 CI 已在跑的 native-ui-resources 之下（與 asset-prep-spr 相同做法，不改 build.yml）。
partial class Checks {
    /// <summary>Synthetic SPR whose every pixel lies inside its frame bounds (the players rule rejects the others).</summary>
    static SrSpr SbWide(int sprite,int sid,int frames){var s=SrMake(sprite,sid,frames);return s with{Frames=s.Frames.Select(f=>f with{Bounds=[-30,-30,80,60]}).ToArray()};}

    static bool SbFrameMatches(Frame frame,SrSpr spr,int f){
        var e=spr.Frames[f];
        return frame.Bounds.SequenceEqual(e.Bounds)&&frame.Blocks.Length==e.Tiles.Length&&frame.Blocks.Select((b,i)=>b.A==e.Tiles[i].A&&b.B==e.Tiles[i].B&&b.Z==e.Tiles[i].Z&&b.Order==i
            &&b.Tile.Pixels.Select(p=>(p.X,p.Y,p.Color)).SequenceEqual(spr.Blocks[e.Tiles[i].Block].OrderBy(p=>p.Y).ThenBy(p=>p.X))).All(x=>x);
    }

    [CheckGroup("asset-prep-spr-b",Order=1),CheckGroup("native-ui-resources",Order=63),CheckGroup(CheckGroups.Default)]
    static void SprRecipeBChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1040","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        string Active(string engine,string id)=>Path.Combine(engine,"assets",id);

        // --- 0. 自動發現與宣告的先後。
        var product=AssetRecipeDiscovery.Product;
        string[] ids=["players","shops","doors","appearance-lists","shadows"];
        Check(product.Errors.Count==0&&ids.All(id=>product.Recipes.Count(r=>r.Id==id&&r.Coverage==AssetRecipeCoverage.Partial)==1),"SB five T455 recipes discovered once, coverage partial");
        IReadOnlyList<string> Req(string id)=>product.Recipes.Single(r=>r.Id==id).Requires;
        Check(Req("shops").Count==0&&Req("doors").Count==0&&Req("players").SequenceEqual(new[]{"objects"})&&Req("appearance-lists").SequenceEqual(new[]{"objects"})&&Req("shadows").SequenceEqual(new[]{"objects","players"})&&Req("objects").Count==0,"SB declared order: shops/doors first, players and lists after objects, shadows after objects and players (asset-recipe.md 6)");

        // --- 合成的動畫表。61 是完整的職業外型；138 的像素落在 bounds 外；50、55 是怪物；85、90 是場景；2346 是門；70 的商店動作沒有方向。
        const string knight=" 0.walk(1 2,0.0:4<478 0.1:4) 1.attack(1 2,8.0:3 8.1:3!) 2.damage(1 1,16.0:2) 3.breath(1 2,24.0:4 24.1:4) 8.death(0 2,32.0:5 32.1:7)"+
            " 18.spell dir(1 2,40.0:2 40.1:3!) 19.spell nodir(0 2,48.0:2 48.1:4!) 20.walk bow(1 1,56.0:4) 21.attack bow(1 3,64.0:5 64.1:5! 64.2:5) 22.damage bow(1 1,72.0:3) 23.breath bow(1 1,88.0:4)";
        const string flat=" 0.walk(0 1,0.0:4) 1.attack(0 1,0.1:3!) 2.damage(0 1,0.2:2) 3.breath(0 1,0.3:4) 8.death(0 1,0.0:5) 18.spell dir(0 1,0.1:2!) 19.spell nodir(0 1,0.2:4!)";
        const string list=
            "S1223\r\n"+
            "#0\t1=3225 prince"+flat+" 20.walk bow(0 1,0.0:4) 21.attack bow(0 2,0.1:5 0.2:5!) 22.damage bow(0 1,0.2:3) 23.breath bow(0 1,0.3:4)"+
            " 46.walk x(0 1,0.3:4) 47.attack x(0 1,0.2:3!) 48.damage x(0 1,0.1:2) 49.breath x(0 1,0.0:4) 101.shadow(3226)\r\n"+
            "#50\t5=51 orc 0.walk(1 1,0.0:4) 1.attack(1 1,8.0:3!) 2.damage(0 1,16.0:2) 3.breath(0 1,16.1:6) 8.die(0 1,16.2:5) 101.shadow(57)\r\n"+
            "#55\t1 bat 0.walk(0 1,0.0:4) 3.breath(0 1,0.0:4)\r\n"+
            "#61\t96=3213 knight"+knight+" 70.act shop(1 2,80.0:4 80.1:4) 101.shadow(3214)\r\n"+
            "#70\t1 sign 70.act shop(0 1,0.0:4)\r\n"+
            "#85\t1 banner 3.breath(0 2,0.0:24 0.1:20)\r\n"+
            "#90\t1 well 0.walk(0 1,0.0:4) 3.breath(0 1,0.1:6)\r\n"+
            "#98\t1 nobody 3.breath(0 1,0.0:4) 101.shadow(99)\r\n"+
            "#138\t1=3217 elf"+flat+"\r\n"+
            "#2346\t1 door 28.open(0 1,0.1:4[8) 29.close(0 1,0.0:4[9)\r\n";
        var sprs=new Dictionary<string,SrSpr>();var files=new List<(int,string,byte[])>();
        void Put(int archive,int sprite,int sid,SrSpr spr){string name=$"{sprite}-{sid}.spr";sprs[name]=spr;files.Add((archive,name,SrEncode(spr)));}
        int[] knightSids=[..Enumerable.Range(0,33),..Enumerable.Range(40,9),..Enumerable.Range(56,40)];   // 0–32, 40–48, 56–95 (80–87 = shop)
        foreach(int sid in knightSids){Put(0,3213,sid,SbWide(3213,sid,3));if(sid is <80 or >87)Put(sid%3==0?5:0,3214,sid,SrMake(3214,sid,3));}   // some shadow files only in Sprite04
        Put(0,3225,0,SbWide(3225,0,4));Put(0,3226,0,SrMake(3226,0,4));
        for(int sid=0;sid<17;sid++){Put(0,51,sid,SrMake(51,sid,3));Put(0,57,sid,SrMake(57,sid,3));}
        Put(0,55,0,SrMake(55,0,1));Put(0,85,0,SrMake(85,0,2));Put(0,90,0,SrMake(90,0,2));Put(0,98,0,SrMake(98,0,1));
        Put(0,3217,0,SrMake(3217,0,4));                                      // tight bounds: pixels outside the frame
        Put(0,2346,0,SrMake(2346,0,2));Put(0,70,0,SrMake(70,0,1));
        string client=Case("client");var pins=SrClient(client,list,files);
        string clientBefore=ApTreeHash(client);
        var runner=AssetPrepRunner.Product();
        AssetPrepRequest Request(string recipe,string engine,params string[] items)=>new(client,engine,recipe,items){Pins=pins};

        string[] objectItems=["0","0-weapon-20","50","55","85","90","world-90","manifest"];
        string[] playerItems=["0-0","0-20","0-46","61-0","61-4","61-20","138-0","138-4","manifest"];
        string[] listItems=["monster-50","monster-55","world-85","world-90","encountered-monsters","world-appearances"];
        string[] shadowItems=["0","50","61","manifest"];

        // --- 1. 先後順序錯誤：明確失敗，什麼都不發布。
        {
            string engine=Case("order");
            void Refused(string label,string recipe,string mustMention,params string[] items){
                var ex=ApRefused(()=>runner.Run(Request(recipe,engine,items)),AssetPrepFailure.Prerequisite,label);
                Check(ex.Message.Contains(mustMention,StringComparison.Ordinal),label+": message names "+mustMention+" — "+ex.Message);
                Check(!Directory.Exists(Active(engine,recipe)),label+": nothing published");
            }
            Refused("SB players before objects","players","objects","61-0","manifest");
            Refused("SB appearance lists before objects","appearance-lists","objects","monster-50","encountered-monsters");
            Refused("SB shadows before objects and players","shadows","objects","61");
            runner.Run(Request("objects",engine,"50"));
            Refused("SB players: the inherited objects/0.json was not prepared by objects","players","objects/0.json","0-0");
            Refused("SB list member whose objects file was not prepared","appearance-lists","objects/85.json","world-85");
            Refused("SB shadows after objects but before players","shadows","players","50");
            runner.Run(Request("players",engine,"61-4","manifest"));
            Refused("SB shadows for a gfx without any published body profile","shadows","gfx 98","98");
        }

        // --- 2. 正常順序：兩個目錄（其一含中文與空白）逐 byte 相同；請求順序不影響結果。
        string pair=Case("two-dirs"),engineA=Path.Combine(pair,"引擎 甲"),engineB=Path.Combine(pair,"engine-b");
        foreach(string engine in new[]{engineA,engineB}){
            string[] R(string[] items)=>engine==engineA?items:items.Reverse().ToArray();
            runner.Run(Request("objects",engine,R(objectItems)));
            Check(runner.Run(Request("shops",engine,R(["61","70","manifest"]))).Produced.SequenceEqual(new[]{"61","70","manifest"}),"SB shops items in canonical order, manifest last");
            runner.Run(Request("doors",engine,R(["2346","manifest"])));
            var p=runner.Run(Request("players",engine,R(playerItems)));
            Check(p.Produced.SequenceEqual(new[]{"0-0","0-20","0-46","61-0","61-4","61-20","138-0","138-4","manifest"}),"SB players items by gfx then weapon: "+string.Join(",",p.Produced));
            Check(runner.Run(Request("appearance-lists",engine,R(listItems))).Produced.SequenceEqual(new[]{"monster-50","monster-55","world-85","world-90","encountered-monsters","world-appearances"}),"SB appearance-lists: members, then the two lists");
            runner.Run(Request("shadows",engine,R(shadowItems)));
        }
        foreach(string id in ids)Check(ApTreeHash(Active(engineA,id))==ApTreeHash(Active(engineB,id)),"SB "+id+": outputs and receipt byte-identical in two directories");
        Check(ApNames(Path.Combine(Active(engineA,"players"),"players")).SequenceEqual(new[]{"0-46.json","61-0.json","61-20.json","manifest.json"}),"SB players writes the own profiles and the manifest only (inherited and unavailable stances write no file)");
        Check(ApNames(Path.Combine(Active(engineA,"shops"),"shops")).SequenceEqual(new[]{"61.json","manifest.json"})&&ApNames(Path.Combine(Active(engineA,"doors"),"doors")).SequenceEqual(new[]{"2346.json","manifest.json"})
            &&ApNames(Active(engineA,"appearance-lists")).SequenceEqual(new[]{"encountered-monsters.json",AssetReceipt.FileName,"world-appearances.json"})
            &&ApNames(Path.Combine(Active(engineA,"shadows"),"shadows")).SequenceEqual(new[]{"0.json","50.json","61.json","manifest.json"}),"SB shops, doors, lists and shadows write exactly their files (no ground recipe)");
        foreach(string id in ids)foreach(string file in Directory.GetFiles(Active(engineA,id),"*.json",SearchOption.AllDirectories)){
            string text=File.ReadAllText(file);
            Check(!text.Contains(":\\",StringComparison.Ordinal)&&!text.Contains("scratch",StringComparison.OrdinalIgnoreCase)&&!text.Contains("className",StringComparison.Ordinal)&&!text.Contains("source_offset",StringComparison.Ordinal)&&!text.Contains("Policy",StringComparison.Ordinal),"SB "+id+"/"+Path.GetFileName(file)+": no path, className, policy text or provenance-only field");
        }

        // 把各 recipe 的輸出組成一棵 assets 樹（T467 的工作的縮小版），附 integrity.json，給產品的讀取程式用。
        string tree=Case("tree");
        var integrity=new List<object>();
        foreach(string id in new[]{"objects","shops","doors","players","appearance-lists","shadows"})
            foreach(string file in Directory.GetFiles(Active(engineA,id),"*",SearchOption.AllDirectories)){
                string rel=Path.GetRelativePath(Active(engineA,id),file).Replace('\\','/');
                if(rel==AssetReceipt.FileName)continue;
                string target=Path.Combine(tree,rel);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
                byte[] data=File.ReadAllBytes(file);integrity.Add(new{path=rel,size=data.Length,sha256=Sha(data)});
            }
        File.WriteAllBytes(Path.Combine(tree,"integrity.json"),JsonSerializer.SerializeToUtf8Bytes(new{schema="p06-assets-integrity-v1",files=integrity}));
        JsonElement Raw(string rel)=>Json.Read(Path.Combine(tree,rel));
        string[] seven=["walk","breath","attack","damage","die","spellDir","spellNoDir"];

        // --- 3. players：PlayerProfiles 的 manifest 讀法與 Load（含 integrity、stepMs、七個動作 × 8 方向的檢查）。
        {
            var profiles=PlayerProfiles.LoadManifest(tree);
            Check(profiles.Keys.Order().SequenceEqual(new[]{(0,0),(0,20),(0,46),(61,0),(61,20)}),"SB players manifest: five available profiles load through PlayerProfiles.LoadManifest");
            Check(profiles[(0,0)].Path=="objects/0.json"&&profiles[(0,20)].Path=="objects/0-weapon-20.json"&&profiles[(0,46)].Path=="players/0-46.json"&&profiles[(61,20)].Path=="players/61-20.json","SB gfx 0 stances 0/20 point at objects/0*.json (inherited); 0-46 and gfx 61 have their own files");
            var k0=profiles[(61,0)];var k20=profiles[(61,20)];
            Check(k0.StepMs==320&&k0.ServerActionMs.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>(x.Key,x.Value)).SequenceEqual(new[]{("attack",240),("spellDir",200),("spellNoDir",240),("walk",320)})
                &&k0.CastReleaseMs["spellDir"]==83&&k0.CastReleaseMs["spellNoDir"]==83&&k0.ProjectileReleaseMs==null,"SB 61-0: stepMs and serverActionMs = 40 × ticks, castReleaseMs = floor sum before '!', no projectile");
            Check(k20.StepMs==160&&k20.ServerActionMs["attack"]==600&&k20.ProjectileReleaseMs==208&&k20.ServerActionMs["walk"]==160,"SB 61-20 (bow): serverActionMs.attack 600, projectileReleaseMs 208");
            Check(profiles[(0,0)].StepMs==160&&profiles[(0,20)].ProjectileReleaseMs==208&&profiles[(0,46)].ServerActionMs["attack"]==120,"SB inherited and 0-46 rows computed with the same rules");
            var a61=PlayerProfiles.Load(tree,k0);var a61b=PlayerProfiles.Load(tree,k20);var a0=PlayerProfiles.Load(tree,profiles[(0,0)]);var a46=PlayerProfiles.Load(tree,profiles[(0,46)]);
            Check(a61.Gfx==61&&a61.Sprite==3213&&a0.Sprite==3225&&a46.StepMs==160,"SB PlayerProfiles.Load accepts every profile (integrity, gfx, stepMs, 7 actions × 8 directions)");
            Check(a61.ActionMs["walk"]==320&&a61.ActionMs["attack"]==125+125&&a61.ActionMs["damage"]==83&&a61.ActionMs["breath"]==332&&a61.ActionMs["die"]==208+291&&a61.ActionMs["spellDir"]==83+125&&a61.ActionMs["spellNoDir"]==83+166,"SB players actionMs: walk ×40, the other six floor sums");
            Check(a61.Sequences[("walk",3)].All(s=>s.Milliseconds==null)&&a61.Sequences[("attack",3)].All(s=>s.Milliseconds!=null)&&a61.Sequences[("walk",3)].Select(s=>s.Key).SequenceEqual(new[]{"3:0","3:1"})&&a61.Sequences[("die",6)].Select(s=>s.Key).SequenceEqual(new[]{"32:0","32:1"})&&a61.Sequences[("walk",0)][0].Events=="<478","SB players sequences: walk steps without milliseconds, others with; keys sid:frame; events copied");
            Check(a61.Frames.Count==42*3&&a61.Frames.All(kv=>{var k=kv.Key.Split(':');return SbFrameMatches(kv.Value,sprs[$"3213-{k[0]}.spr"],int.Parse(k[1]));}),"SB 61-0 frames: every frame of every file the stances use, equal to the synthetic SPR (no shop files)");
            var m=Raw("players/manifest.json");
            Check(m.S("schema")=="p24-player-profiles-v1"&&m.GetProperty("profiles").EnumerateArray().Where(p=>p.TryGetProperty("inherited",out _)).Select(p=>p.S("path")).SequenceEqual(new[]{"objects/0.json","objects/0-weapon-20.json"})
                &&m.GetProperty("profiles").EnumerateArray().Single(p=>p.I("gfx")==61&&p.I("weapon")==0).I("frames")==a61.Frames.Count,"SB players manifest: schema, inherited rows, frame count");
            var unavailable=m.GetProperty("unavailable").EnumerateArray().Select(u=>(u.I("gfx"),u.I("weapon"),u.S("reason"))).ToArray();
            Check(unavailable.Length==3&&unavailable[0] is (61,4,"missing complete action 4/walk")&&unavailable[1].Item1==138&&unavailable[1].Item2==0&&unavailable[1].Item3.StartsWith("opaque pixel outside frame: ",StringComparison.Ordinal)&&unavailable[2] is (138,4,"missing complete action 4/walk"),
                "SB players unavailable (L7 kinds): missing stance action; opaque pixel outside the frame bounds — "+string.Join(" | ",unavailable.Select(u=>u.Item3)));
            var receipt=AssetReceipt.TryLoad(Path.Combine(Active(engineA,"players"),AssetReceipt.FileName))!;
            var inheritedSources=receipt.Completed.Single(c=>c.Item=="0-0").Sources;
            Check(inheritedSources.Any(s=>s.Container=="assets/objects"&&s.Entry=="objects/0.json"&&s.Sha256==Sha(File.ReadAllBytes(Path.Combine(tree,"objects","0.json")))),"SB receipt records the objects output read (container assets/objects, path, SHA-256)");
        }

        // --- 4. shops、doors：OriginalAppearanceCatalog（ObjectAssets 的讀法）。
        {
            var shops=new OriginalAppearanceCatalog(tree,"shops/manifest.json","p24-original-shop-appearance-v1",["shop"]);
            var shop=shops.Get(61)!;
            Check(shop.Sprite==3213&&shop.StepMs==332&&shop.Duration("shop")==332&&shop.Sequences.Count==8&&shop.Sequences[("shop",5)].Select(s=>(s.Key,s.Milliseconds)).SequenceEqual(new[]{("85:0",(int?)166),("85:1",(int?)166)})&&shop.Frames.Count==24
                &&shop.Frames.All(kv=>{var k=kv.Key.Split(':');return SbFrameMatches(kv.Value,sprs[$"3213-{k[0]}.spr"],int.Parse(k[1]));}),"SB shops/61.json loads: action 70 in 8 directions, floor length, frames of 3213-80..87 only");
            var sm=Raw("shops/manifest.json");
            Check(sm.I("sourceAction")==70&&sm.GetProperty("profiles")[0].I("durationMs")==332&&sm.GetProperty("profiles")[0].S("metadataSha256")==Raw("players/61-0.json").S("entryHash")&&sm.GetProperty("unavailable").EnumerateArray().Single().I("gfx")==70&&shops.Get(70)==null,"SB shops manifest: duration, entry hash, gfx 70 (non-directional shop action) unavailable");
            Check(!Raw("shops/61.json").GetProperty("sequences")[0].GetProperty("sequence")[0].TryGetProperty("events",out _),"SB shop steps without events carry no events key");
            var doors=new OriginalAppearanceCatalog(tree,"doors/manifest.json","p24-original-inn-door-v1",["open","close"]);
            var door=doors.Get(2346)!;
            Check(door.StepMs==167&&door.Frames.Count==2&&Enumerable.Range(0,8).All(d=>door.Sequences[("open",d)].Single() is {Key:"0:1",Ticks:4,Events:"[8"}&&door.Sequences[("close",d)].Single() is {Key:"0:0",Events:"[9"})
                &&door.Frames.All(kv=>SbFrameMatches(kv.Value,sprs["2346-0.spr"],int.Parse(kv.Key.Split(':')[1]))),"SB doors/2346.json loads: stepMs 167, open 0:1 / close 0:0 for all 8 headings, 2 frames");
            Check(Raw("doors/2346.json").GetProperty("sourceActions").EnumerateArray().Select(x=>x.GetInt32()).SequenceEqual(new[]{28,29})&&Raw("doors/2346.json").GetProperty("nonDirectional").GetBoolean(),"SB doors file: sourceActions [28,29], nonDirectional");
        }

        // --- 5. 兩份清冊：encountered-monsters、world-appearances。
        {
            var monsters=new OriginalAppearanceCatalog(tree,"encountered-monsters.json","p24-original-encountered-monsters-v1",["walk","breath","attack","damage","die"]);
            var world=new OriginalAppearanceCatalog(tree,"world-appearances.json","p24-original-world-appearances-v1",["breath"]);
            Check(monsters.Get(50) is {Sprite:51,StepMs:160}&&monsters.Get(55)==null,"SB encountered-monsters: 50 loads; 55 (no attack/damage/die) is not listed");
            var em=Raw("encountered-monsters.json");var row=em.GetProperty("profiles").EnumerateArray().Single();
            Check(row.S("path")=="objects/50.json"&&row.I("frames")==17*3&&row.I("durationMs")==160&&row.S("metadataSha256")==Raw("objects/50.json").S("entryHash")&&row.S("sha256")==Sha(File.ReadAllBytes(Path.Combine(tree,"objects","50.json")))
                &&row.GetProperty("supportedActions").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"walk","breath","attack","damage","die"}),"SB monster row: path, SHA-256 of the objects file, frames, duration, entry hash, supported actions");
            var un=em.GetProperty("unavailable").EnumerateArray().Single();
            Check(un.I("gfx")==55&&un.S("reason").Contains("attack",StringComparison.Ordinal),"SB monster without the required actions becomes an unavailable row: "+un.S("reason"));
            Check(world.Get(85) is {StepMs:1833}&&world.Get(90) is {Sprite:90},"SB world-appearances: 85 and 90 load");
            var wm=Raw("world-appearances.json").GetProperty("profiles").EnumerateArray().ToArray();
            Check(wm.Select(r=>r.S("path")).SequenceEqual(new[]{"objects/85.json","objects/world-90.json"})&&wm[0].GetProperty("supportedActions").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"breath"})&&!Raw("world-appearances.json").TryGetProperty("unavailable",out _),"SB world rows prefer objects/world-<gfx>.json; supported actions; no empty unavailable key");
        }

        // --- 6. shadows：PlayerShadowCatalog；sprite 取 101.shadow，幀 key 是 body 序列實際用到的聯集。
        {
            var shadows=new PlayerShadowCatalog(tree);
            var body61=new[]{"players/61-0.json","players/61-20.json"}.Select(p=>new ObjectAppearance(Path.Combine(tree,p))).ToArray();
            var used=body61.SelectMany(a=>a.Sequences.Values.SelectMany(s=>s.Select(t=>t.Key))).ToHashSet();
            Check(used.Count==124&&used.All(k=>shadows.Get(61,3213,k) is {} f&&SbFrameMatches(f,sprs[$"3214-{k.Split(':')[0]}.spr"],int.Parse(k.Split(':')[1]))),"SB shadows/61.json: each of the 124 keys the body sequences use gives the 3214 frame with the same sid:frame (incl. files only in an alternate)");
            Check(shadows.Get(61,3213,"80:0")==null&&shadows.Get(61,3213,"0:2")==null&&shadows.Get(61,9999,"0:0")==null,"SB keys the bodies never use (shop file, unused frame) and other body sprites stay unshadowed");
            var s61=Raw("shadows/61.json");
            Check(s61.S("schema")=="p24-original-shadow-frames-v1"&&s61.I("flags")==5&&s61.I("shadowSprite")==3214&&s61.GetProperty("frames").GetArrayLength()==124
                &&s61.GetProperty("frames").EnumerateArray().Select(f=>SprManifest.ParseKey(f.S("key"))!.Value).SequenceEqual(used.Select(k=>SprManifest.ParseKey(k)!.Value).Order()),"SB shadow frames in (sid, frame) order, flags 5");
            Check(Raw("shadows/50.json").I("shadowSprite")==57&&shadows.Get(50,51,"16:2") is not null,"SB 50: shadow sprite 57 from 101.shadow, not body + 1");
            var rows=Raw("shadows/manifest.json").GetProperty("profiles").EnumerateArray().ToDictionary(r=>r.I("gfx"));
            Check(rows[0].GetProperty("sourceBodyProfiles").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"objects/0.json","objects/0-weapon-20.json","players/0-46.json"})&&rows[0].I("bodySprite")==3225&&rows[0].I("shadowSprite")==3226
                &&rows[61].GetProperty("sourceBodyProfiles").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"players/61-0.json","players/61-20.json"})&&rows[61].I("requiredKeys")==124&&rows[61].I("providedKeys")==124
                &&rows[50].GetProperty("actions").EnumerateArray().Select(x=>x.GetString()).SequenceEqual(new[]{"attack","breath","damage","die","walk"}),"SB shadows manifest: body profiles from objects then players, actions, key counts");
            Check(shadows.Get(0,3225,"0:3") is not null,"SB gfx 0 shadow covers objects/0*.json and players/0-46.json bodies");
        }

        // --- 7. 前一個 recipe 之後被改掉：沿用前會重驗；不符就重做，需要的檔不見了就明確失敗；改回來後又是最新。
        {
            Check(runner.Run(Request("players",engineA,playerItems)).Outcome==AssetPrepOutcome.UpToDate,"SB players again with objects unchanged: up to date (prior sources verify)");
            runner.Run(Request("objects",engineA,objectItems.Where(i=>i!="0-weapon-20").ToArray()));
            var ex=ApRefused(()=>runner.Run(Request("players",engineA,playerItems)),AssetPrepFailure.Prerequisite,"SB players after objects dropped 0-weapon-20");
            Check(ex.Message.Contains("objects/0-weapon-20.json",StringComparison.Ordinal)&&Directory.Exists(Active(engineA,"players")),"SB the refusal names objects/0-weapon-20.json; the published players directory is left as it was");
            runner.Run(Request("objects",engineA,objectItems));
            Check(runner.Run(Request("players",engineA,playerItems)).Outcome==AssetPrepOutcome.UpToDate,"SB objects restored byte-identically: players up to date again");
            string engine=Case("resume");
            foreach(var (id,items) in new[]{("objects",objectItems),("players",playerItems)})runner.Run(Request(id,engine,items));
            int done=0;
            Throws<InvalidOperationException>(()=>runner.Run(Request("shadows",engine,shadowItems) with{ItemCompleted=_=>{if(++done==2)throw new InvalidOperationException("synthetic interrupt");}}),"SB shadows interrupted after two items","synthetic interrupt");
            var resumed=runner.Run(Request("shadows",engine,shadowItems));
            Check(resumed.Reused.SequenceEqual(new[]{"0","50"})&&resumed.Produced.SequenceEqual(new[]{"61","manifest"}),"SB shadows resume reuses the items whose objects / players sources still verify: reused "+string.Join(",",resumed.Reused));
            Check(ApTreeHash(Active(engine,"shadows"))==ApTreeHash(Active(engineB,"shadows")),"SB resumed shadows byte-identical to an uninterrupted run");
        }

        // --- 8. 不合法項目；預熱清單（asset-recipe.md 5、D5）每個 recipe 都接受。
        {
            string engine=Case("invalid");
            foreach(var (id,item) in new[]{("players","61"),("players","61-256"),("players","0-01"),("shops","shop-61"),("doors","-1"),("shadows","manifest-1"),("appearance-lists","monster-"),("appearance-lists","50")})
                ApRefused(()=>runner.Run(Request(id,engine,item)),AssetPrepFailure.InvalidRequest,$"SB {id} item \"{item}\"");
            var counts=new Dictionary<string,int>{["objects"]=53,["players"]=58,["shops"]=12,["doors"]=2,["shadows"]=18,["appearance-lists"]=27};
            foreach(var (id,count) in counts){
                var items=PreheatList.Items(id);var normalized=product.Recipes.Single(r=>r.Id==id).Normalize(items);
                Check(items.Count==count&&normalized.Count==count,$"SB preheat list for {id}: {items.Count} items, all accepted by Normalize");
            }
            Check(PreheatList.Items("character").Count==0&&PreheatList.PlayerStances.Sum(p=>p.Value.Length)==57,"SB preheat list: 57 player stances (42 profiles + 15 unavailable of the baseline)");
        }

        // --- 9. 整輪下來原客戶端沒有任何變動。
        Check(ApTreeHash(client)==clientBefore,"SB client directory is byte-identical after every run (read-only input)");
    }
}
