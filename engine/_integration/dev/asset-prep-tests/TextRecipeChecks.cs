using P06;
using P06.AssetPrep;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// T453（#1038）：文字類 recipe（text）的離線測試。Text.idx／Text.pak 與 17 個 Sprite 容器都由本檔合成，寫在
// scratch/issue-1038/run-<時間>/ 下；不讀任何客戶端，不需要遊戲、網路或 DB。內容全是測試自編的字串，沒有任何原客戶端的模板或名稱。
// 每次執行用新的 run 目錄，測試本身不做遞迴刪除。
// 群組 asset-prep-text；同時掛在 CI 已在跑的 native-ui-resources 之下（與 asset-prep 相同做法，不改 build.yml）。
partial class Checks {
    /// <summary>合成的 Text.idx／Text.pak（列與 payload 都照 TextArchive 的格式編碼）。entry 依給定順序排列，名稱可以重複。</summary>
    static void TxArchive(string dir,params (string Name,byte[] Data)[] items){
        using var pak=new MemoryStream();using var rows=new MemoryStream();
        foreach(var (name,data) in items){rows.Write(IdxRecord((uint)pak.Position,Name20(name),(uint)data.Length));pak.Write(TextArchive.Encode(data));}
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir,"Text.idx"),[..BitConverter.GetBytes((uint)items.Length),..TextArchive.Encode(rows.ToArray())]);
        File.WriteAllBytes(Path.Combine(dir,"Text.pak"),pak.ToArray());
    }
    static byte[] TxBig5(string text){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(text);}
    /// <summary>ObjectAssets 建構式需要的最小素材樹（四份空清冊加 integrity），names.json 由呼叫端放入。</summary>
    static string TxObjectTree(string root,byte[] names){
        var pins=new List<object>();
        void Save(string path,object data,bool pin=false){
            var bytes=JsonSerializer.SerializeToUtf8Bytes(data);string file=Path.Combine(root,path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllBytes(file,bytes);
            if(pin)pins.Add(new{path,sha256=Convert.ToHexString(SHA256.HashData(bytes)),size=bytes.Length});
        }
        Save("objects/manifest.json",new{gfx=Array.Empty<int>()});
        Save("shops/manifest.json",new{schema="p24-original-shop-appearance-v1",profiles=Array.Empty<object>()},true);
        Save("encountered-monsters.json",new{schema="p24-original-encountered-monsters-v1",profiles=Array.Empty<object>()},true);
        Save("world-appearances.json",new{schema="p24-original-world-appearances-v1",profiles=Array.Empty<object>()},true);
        Save("doors/manifest.json",new{schema="p24-original-inn-door-v1",profiles=Array.Empty<object>()},true);
        Save("integrity.json",new{schema="p06-assets-integrity-v1",files=pins});
        File.WriteAllBytes(Path.Combine(root,"objects","names.json"),names);
        return root;
    }

    [CheckGroup("asset-prep-text",Order=1),CheckGroup("native-ui-resources",Order=61),CheckGroup(CheckGroups.Default)]
    static void TextRecipeChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1038","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        const string id=TextRecipe.RecipeId;
        string Active(string engine)=>Path.Combine(engine,"assets",id);
        string Staging(string engine)=>Path.Combine(engine,"runtime","assets-staging",id);
        string Receipt(string dir)=>Path.Combine(dir,AssetReceipt.FileName);
        string NamesFile(string engine)=>Path.Combine(Active(engine),"objects","names.json");
        string TemplatesFile(string engine)=>Path.Combine(Active(engine),"dialogs","templates.json");

        // --- 0. 自動發現：產品組件只靠 [AssetRecipe] 註冊；產品用的錨點是 desc-c.tbl 的解碼後 SHA-256。
        Check(AssetRecipeDiscovery.Product.Errors.Count==0&&AssetRecipeDiscovery.Product.Recipes.Count(r=>r is TextRecipe{Id:"text",Coverage:AssetRecipeCoverage.Complete})==1,"TX text recipe discovered once, coverage complete");
        Check(AssetPrepRunner.Product().RecipeIds.Contains(id),"TX product runner knows the text recipe");

        // --- 合成的 Text：名稱表放在最前面（之後改 html 不會動到它的 offset）。
        byte[] desc=TxBig5("6\r\n劍\r\n\r\n a\"b\\c \r\n盾牌\tx\r\n$7\r\nlast");
        const string alphaText="<body>甲 & 'q' + <a action=\"buy\">買</a></body>";
        (string Name,byte[] Data)[] Entries(string beta)=>[
            ("desc-c.tbl",desc),
            ("alpha-c.html",TxBig5(alphaText+"\0\0")),
            ("Beta-c.htm",TxBig5(beta)),
            ("ALPHA-C.HTML",TxBig5(alphaText)),                         // 同鍵、內容相同（去掉結尾 NUL 後）：略過
            ("gamma-c.html",TxBig5("one")),
            ("list.spr",Encoding.ASCII.GetBytes("#0\t1")),
            ("gamma-c.html",TxBig5("two")),                             // 同名、內容不同：整個鍵移除
            ("gamma-c.html",TxBig5("one")),                             // 衝突鍵之後的同名 entry：跳過
            ("bad-c.html",[(byte)'<',0xA4,0x20,(byte)'x',0xA4]),        // 不是合法的 code page 950：跳過
            ("note-c .html",TxBig5("檔名含空白，不算模板")),
            ("readme.html",TxBig5("不是 -c 結尾")),
            ("delta-C.HTM",TxBig5("<p>丁\r\n第二行</p>")),
        ];
        var recipe=new TextRecipe(Sha(desc));
        var runner=new AssetPrepRunner([recipe]);
        string client=Case("client");var pins=ApClient(client);TxArchive(client,Entries("<p>beta</p>"));
        string clientBefore=ApTreeHash(client);
        AssetPrepRequest Request(string? c,string engine,params string[] items)=>new(c,engine,id,items){Pins=pins};

        // --- 1. 正常產生：兩個輸出目錄的檔案與收據逐 byte 相同；欄位、排序與序列化照契約。
        string pair=Case("two-dirs"),engineA=Path.Combine(pair,"引擎 甲"),engineB=Path.Combine(pair,"engine-b");
        var a=runner.Run(Request(client,engineA,"templates","names"));
        var b=runner.Run(Request(client,engineB,"names","templates","names"));
        Check(a.Outcome==AssetPrepOutcome.Published&&b.Outcome==AssetPrepOutcome.Published&&a.Produced.SequenceEqual(new[]{"names","templates"})&&b.Produced.SequenceEqual(a.Produced),"TX both directories published, items in canonical order: "+string.Join(",",a.Produced));
        Check(AssetPrepPaths.ListFiles(Active(engineA)).SequenceEqual(new[]{"dialogs/templates.json","objects/names.json",AssetReceipt.FileName})&&!Directory.Exists(Staging(engineA)),"TX output is exactly dialogs/templates.json, objects/names.json and the receipt: "+string.Join(",",AssetPrepPaths.ListFiles(Active(engineA))));
        Check(ApTreeHash(Active(engineA))==ApTreeHash(Active(engineB)),"TX outputs and receipts of the two directories are byte-identical");

        byte[] namesBytes=File.ReadAllBytes(NamesFile(engineA));
        string expectedNames="[\"劍\", \"\", \" a\\\"b\\\\c \", \"盾牌\\tx\", \"$7\", \"last\"]";
        Check(namesBytes.AsSpan().SequenceEqual(new UTF8Encoding(false).GetBytes(expectedNames)),"TX names.json: \", \" separator, non-ASCII as is, no BOM, no line break — "+Encoding.UTF8.GetString(namesBytes));
        byte[] templateBytes=File.ReadAllBytes(TemplatesFile(engineA));
        var expectedTemplates=new Dictionary<string,string>{["alpha"]=alphaText,["Beta"]="<p>beta</p>",["delta"]="<p>丁\r\n第二行</p>"};
        Check(templateBytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(expectedTemplates)),"TX templates.json equals JsonSerializer's default output of the expected dictionary (index order, first spelling of each key): "+Encoding.ASCII.GetString(templateBytes));
        Check(templateBytes.All(x=>x is >=0x20 and <0x7F)&&Encoding.ASCII.GetString(templateBytes).StartsWith("{\"alpha\":\"\\u003Cbody\\u003E\\u7532 \\u0026 \\u0027q\\u0027 \\u002B ",StringComparison.Ordinal),"TX templates.json is ASCII only, no white space, \\uXXXX escapes in upper case");

        var loaded=AssetReceipt.TryLoad(Receipt(Active(engineA)))??throw new Exception("TX receipt does not load");
        string receiptText=File.ReadAllText(Receipt(Active(engineA)));
        Check(loaded is {RecipeId:id,RecipeVersion:1,Status:AssetReceipt.Complete}&&loaded.Requested.SequenceEqual(TextRecipe.AllItems)&&!receiptText.Contains("ready",StringComparison.OrdinalIgnoreCase),"TX receipt: recipe text v1, complete, both items, no ready state");
        Check(!receiptText.Contains("引擎",StringComparison.Ordinal)&&!receiptText.Contains("scratch",StringComparison.OrdinalIgnoreCase)&&!receiptText.Contains(":\\",StringComparison.Ordinal),"TX receipt carries no path");
        var namesItem=loaded.Completed.Single(c=>c.Item=="names");var templatesItem=loaded.Completed.Single(c=>c.Item=="templates");
        Check(namesItem.Sources.Single() is {Container:"Text",Entry:"desc-c.tbl",Offset:0} ds&&ds.Size==desc.Length&&ds.Sha256==Sha(desc),"TX names: the receipt records desc-c.tbl with its offset, size and decoded SHA-256");
        Check(namesItem.Outputs.Single() is {RelativePath:"objects/names.json"} no&&no.Size==namesBytes.Length&&no.Sha256==Sha(namesBytes)&&namesItem.Metadata["declared"]=="6"&&namesItem.Metadata["names"]=="6","TX names: output and counts recorded");
        // 模板的來源：錨點加上 8 個 *-c.html／htm；三個同名的 gamma 各有自己的 offset。
        Check(templatesItem.Sources.Count==9&&templatesItem.Sources[0].Entry=="desc-c.tbl"&&templatesItem.Sources.All(s=>s.Container=="Text")
            &&templatesItem.Sources.Where(s=>s.Entry=="gamma-c.html").Select(s=>s.Offset).Distinct().Count()==3
            &&!templatesItem.Sources.Any(s=>s.Entry is "note-c .html" or "readme.html" or "list.spr"),"TX templates: every *-c.html / *-c.htm row is a recorded source (same-name rows by offset), other rows are not read: "+string.Join(",",templatesItem.Sources.Select(s=>s.Entry)));
        var tm=templatesItem.Metadata;
        Check(tm["candidates"]=="8"&&tm["templates"]=="3"&&tm["duplicateIdentical"]=="1"&&tm["invalidEncoding"]=="1"&&tm["invalidEncodingEntries"]=="bad-c.html"&&tm["conflictKeys"]=="1"&&tm["conflictKeyList"]=="gamma"&&tm["conflictSkipped"]=="1",
            "TX templates: classification counts in the receipt — "+string.Join(" ",tm.Select(m=>m.Key+"="+m.Value)));
        Check(templatesItem.Outputs.Single() is {RelativePath:"dialogs/templates.json"} to&&to.Sha256==Sha(templateBytes),"TX templates: output recorded");

        // --- 2. 既有讀取程式接受這兩個輸出。
        var templates=new NpcTemplates(TemplatesFile(engineA));
        var page=templates.Render("ALPHA",[],"npc","me");
        Check(page.Text.Contains("甲 & 'q' +",StringComparison.Ordinal)&&page.Options.Single() is {Kind:"action",Value:"buy",Enabled:true} option&&option.Label=="買","TX NpcTemplates reads templates.json: case-insensitive key, text and action option — "+page.Text);
        Check(templates.Render("beta",[],"npc","me").Text.Contains("beta",StringComparison.Ordinal)&&templates.Render("delta",[],"npc","me").Text.Contains("丁",StringComparison.Ordinal),"TX NpcTemplates renders the other keys");
        foreach(string absent in new[]{"gamma","bad","note","readme"})
            Check(templates.Render(absent,[],"npc","me").Text.StartsWith("缺少此對話",StringComparison.Ordinal),"TX key not in templates.json: "+absent);
        var objects=new ObjectAssets(TxObjectTree(Case("object-tree"),namesBytes));
        Check(objects.Name("$0|$1|$2|$3|$5|$6|$4")=="劍|| a\"b\\c |盾牌\tx|last|$6|$7","TX ObjectAssets reads names.json: index = name id, empty name kept, id past the table left as is — "+objects.Name("$0|$1|$2|$3|$5|$6|$4"));

        // --- 3. 已是最新、續跑、來源改變。
        var stamp=File.GetLastWriteTimeUtc(Receipt(Active(engineA)));
        var again=runner.Run(Request(client,engineA,"names","templates"));
        Check(again.Outcome==AssetPrepOutcome.UpToDate&&again.Reused.Count==2&&File.GetLastWriteTimeUtc(Receipt(Active(engineA)))==stamp,"TX identical request with unchanged Text sources: up to date, nothing rewritten");
        {
            string c=Case("resume-client");var p=ApClient(c);TxArchive(c,Entries("<p>beta</p>"));
            string engine=Case("resume-engine");
            AssetPrepRequest R(params string[] items)=>new(c,engine,id,items){Pins=p};
            Throws<InvalidOperationException>(()=>runner.Run(R("names","templates") with{ItemCompleted=_=>throw new InvalidOperationException("synthetic interrupt")}),"TX run interrupted after the first item","synthetic interrupt");
            Check(!Directory.Exists(Active(engine))&&AssetReceipt.TryLoad(Receipt(Staging(engine))) is {Status:AssetReceipt.InProgress} half&&half.Completed.Select(x=>x.Item).SequenceEqual(new[]{"names"}),"TX interrupted run: names staged, nothing published");
            // html 換了內容（名稱表沒動）：names 的來源驗得過所以沿用，templates 照新的來源產生。
            TxArchive(c,Entries("<p>changed</p>"));
            var resumed=runner.Run(R("names","templates"));
            Check(resumed.Outcome==AssetPrepOutcome.Published&&resumed.Reused.SequenceEqual(new[]{"names"})&&resumed.Produced.SequenceEqual(new[]{"templates"}),"TX retry reuses the item whose Text source still verifies: reused "+string.Join(",",resumed.Reused)+"; produced "+string.Join(",",resumed.Produced));
            Check(new NpcTemplates(TemplatesFile(engine)).Render("beta",[],"npc","me").Text.Contains("changed",StringComparison.Ordinal)&&File.ReadAllBytes(NamesFile(engine)).AsSpan().SequenceEqual(namesBytes),"TX resumed result uses the current sources");
            // 已發布之後來源又變：不算最新，整批重做。
            TxArchive(c,Entries("<p>third</p>"));
            var redo=runner.Run(R("names","templates"));
            Check(redo.Outcome==AssetPrepOutcome.Published&&redo.Produced.Count==2&&new NpcTemplates(TemplatesFile(engine)).Render("beta",[],"npc","me").Text.Contains("third",StringComparison.Ordinal),"TX a changed Text entry makes the published directory stale");
            // 來源換成另一個版本的名稱表：停止，舊的正式目錄維持原樣。
            string before=ApTreeHash(Active(engine));
            var other=Entries("<p>third</p>");other[0]=("desc-c.tbl",TxBig5("1\r\nother"));TxArchive(c,other);
            var refused=ApRefused(()=>runner.Run(R("names","templates")),AssetPrepFailure.ContainerMismatch,"TX desc-c.tbl of another version after a publish","Text");
            Check(refused.Message.Contains("desc-c.tbl",StringComparison.Ordinal)&&ApTreeHash(Active(engine))==before,"TX version mismatch names the entry and leaves the published directory untouched: "+refused.Message);
        }

        // --- 4. 被拒：版本不符、缺 entry、同名、截斷、越界、內容不合法；每一種都沒有任何東西被發布。
        {
            string engine=Case("reject-engine");
            AssetPrepException Rejected(string label,AssetPrepRunner r,string c,AssetPrepPins p,AssetPrepFailure failure,string? container,params string[] items){
                var ex=ApRefused(()=>r.Run(new AssetPrepRequest(c,engine,id,items){Pins=p}),failure,label,container);
                Check(!Directory.Exists(Active(engine)),label+": nothing published");
                return ex;
            }
            // 產品的 recipe（真實錨點）對上合成的 Text：兩個項目都停，訊息指出 entry，不含開發機路徑。
            var product=new AssetPrepRunner([new TextRecipe()]);
            foreach(string item in TextRecipe.AllItems){
                var ex=Rejected("TX supported-version anchor against another Text ("+item+")",product,client,pins,AssetPrepFailure.ContainerMismatch,"Text",item);
                Check(ex.Message.Contains("desc-c.tbl",StringComparison.Ordinal)&&!ex.Message.Contains("0-客戶端",StringComparison.Ordinal),"TX version mismatch names desc-c.tbl ("+item+"): "+ex.Message);
            }
            string Client(string tag,out AssetPrepPins p,params (string Name,byte[] Data)[] items){string c=Case(tag);p=ApClient(c);TxArchive(c,items);return c;}

            string c1=Client("no-desc",out var p1,("alpha-c.html",TxBig5("a")));
            foreach(string item in TextRecipe.AllItems)
                Check(Rejected("TX Text without desc-c.tbl ("+item+")",runner,c1,p1,AssetPrepFailure.MissingEntry,"Text",item).Message.Contains("desc-c.tbl",StringComparison.Ordinal),"TX missing anchor names desc-c.tbl ("+item+")");
            string c2=Client("two-desc",out var p2,("desc-c.tbl",desc),("alpha-c.html",TxBig5("a")),("DESC-C.TBL",desc));
            Check(Rejected("TX desc-c.tbl twice in the index",runner,c2,p2,AssetPrepFailure.CorruptContainer,"Text","names").Message.Contains("desc-c.tbl",StringComparison.Ordinal),"TX repeated anchor names desc-c.tbl");

            string c3=Client("truncated-idx",out var p3,Entries("b"));
            string idx3=Path.Combine(c3,"Text.idx");File.WriteAllBytes(idx3,File.ReadAllBytes(idx3)[..^5]);
            Rejected("TX truncated Text.idx",runner,c3,p3,AssetPrepFailure.CorruptContainer,"Text","names","templates");
            string c4=Client("count-past-rows",out var p4,Entries("b"));
            string idx4=Path.Combine(c4,"Text.idx");byte[] raw4=File.ReadAllBytes(idx4);BitConverter.GetBytes((uint)(Entries("b").Length+1)).CopyTo(raw4,0);File.WriteAllBytes(idx4,raw4);
            Rejected("TX Text.idx count past its rows",runner,c4,p4,AssetPrepFailure.CorruptContainer,"Text","templates");
            string c5=Client("truncated-pak",out var p5,Entries("b"));
            string pak5=Path.Combine(c5,"Text.pak");File.WriteAllBytes(pak5,File.ReadAllBytes(pak5)[..^1]);
            Rejected("TX truncated Text.pak (last entry past the end)",runner,c5,p5,AssetPrepFailure.CorruptContainer,"Text","names");
            string c6=Case("out-of-range");var p6=ApClient(c6);byte[] packed=TextArchive.Encode(desc);
            File.WriteAllBytes(Path.Combine(c6,"Text.idx"),[..BitConverter.GetBytes(2u),..TextArchive.Encode([..IdxRecord(0,Name20("desc-c.tbl"),(uint)packed.Length),..IdxRecord((uint)packed.Length-4,Name20("far-c.html"),1000)])]);
            File.WriteAllBytes(Path.Combine(c6,"Text.pak"),packed);
            Rejected("TX entry beyond Text.pak",runner,c6,p6,AssetPrepFailure.CorruptContainer,"Text","templates");
            string c7=Client("no-pak",out var p7,Entries("b"));File.Delete(Path.Combine(c7,"Text.pak"));
            Rejected("TX Text.pak missing",runner,c7,p7,AssetPrepFailure.MissingContainer,"Text","names");
            string c8=Client("no-idx",out var p8,Entries("b"));File.Delete(Path.Combine(c8,"Text.idx"));
            Rejected("TX Text.idx missing",runner,c8,p8,AssetPrepFailure.MissingContainer,"Text","templates");

            // 錨點相符但內容不是名稱表（只有在測試換錨點時才可能發生）：明確拒絕，不寫出半套。
            byte[] cut=[(byte)'2',13,10,(byte)'a',13,10,0xA4],noCount=TxBig5("甲\r\n乙");
            string c9=Client("desc-not-big5",out var p9,("desc-c.tbl",cut));
            Rejected("TX desc-c.tbl with a cut double-byte character",new AssetPrepRunner([new TextRecipe(Sha(cut))]),c9,p9,AssetPrepFailure.CorruptEntry,"Text","names");
            string c10=Client("desc-no-count",out var p10,("desc-c.tbl",noCount));
            Rejected("TX desc-c.tbl without a count line",new AssetPrepRunner([new TextRecipe(Sha(noCount))]),c10,p10,AssetPrepFailure.CorruptEntry,"Text","names");

            foreach(string item in new[]{"skills","magic","Names","names.json","../names","desc-c.tbl",""})
                Rejected("TX item \""+item+"\"",runner,client,pins,AssetPrepFailure.InvalidRequest,null,item);
            Rejected("TX empty item list",runner,client,pins,AssetPrepFailure.InvalidRequest,null);
        }

        // --- 5. 只要求一個項目：只產生那個檔。
        {
            string engine=Case("single-item");
            var only=runner.Run(Request(client,engine,"templates"));
            Check(only.Produced.SequenceEqual(new[]{"templates"})&&AssetPrepPaths.ListFiles(Active(engine)).SequenceEqual(new[]{"dialogs/templates.json",AssetReceipt.FileName})&&File.ReadAllBytes(TemplatesFile(engine)).AsSpan().SequenceEqual(templateBytes),"TX single item: only dialogs/templates.json, same bytes");
        }

        // --- 6. 整輪下來原客戶端沒有任何變動。
        Check(ApTreeHash(client)==clientBefore,"TX client directory is byte-identical after every run (read-only input)");
    }
}
