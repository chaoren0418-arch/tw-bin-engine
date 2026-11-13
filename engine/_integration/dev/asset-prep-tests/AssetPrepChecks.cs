using P06;
using P06.AssetPrep;
using P06.NativeUi;
using System.Diagnostics;
using System.Text;

// T451（#1036）：本機素材準備器核心的離線測試。所有 idx／pak／TBT／Text 都由本檔合成，寫在
// scratch/issue-1036/run-<時間>/ 下；不讀任何客戶端，不需要遊戲、網路或 DB。
// 每次執行用新的 run 目錄，測試本身不做遞迴刪除；建立的 junction 在結束時逐一以非遞迴方式移除。
// 群組 asset-prep；同時掛在 CI 已在跑的 native-ui-resources（合成 idx／pak 資源層）之下。
partial class Checks {
    /// <summary>有位移、每列一段、含 0 值（透明）像素的合成 .tbt。</summary>
    static byte[] ApTbt(int w,int h,int seed,byte ox,byte oy){
        var b=new List<byte>{ox,oy,(byte)w,(byte)h};
        for(int y=0;y<h;y++){
            int skip=y%3,count=w-skip-y%2;
            if(count<1){skip=0;count=w;}
            b.Add(1);b.Add((byte)(skip*2));b.Add((byte)count);
            for(int x=0;x<count;x++){
                ushort v=(ushort)((seed*131+y*37+x*7+1)&0x7FFF);if((x+y)%5==0)v=0;
                b.Add((byte)v);b.Add((byte)(v>>8));
            }
        }
        return b.ToArray();
    }
    /// <summary>17 個合成 Sprite 容器。名稱前綴 "main:" 的放進 Sprite.pak，其餘依 NameHash 放進 Sprite00–15。</summary>
    static AssetPrepPins ApClient(string dir,params (string Name,byte[] Data)[] entries){
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (name,data) in entries){
            if(name.StartsWith("main:",StringComparison.Ordinal))items[0].Add((name[5..],data));else items[Hn(name)].Add((name,data));
        }
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        return new(Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray());
    }
    static AssetPrepPins ApWithPin(AssetPrepPins pins,int index,Func<ClientArchivePin,ClientArchivePin> change)=>
        new(pins.Sprites.Select((p,i)=>i==index?change(p):p).ToArray());
    static AssetPrepRequest ApRequest(string? client,string engine,AssetPrepPins pins,params string[] items)=>
        new(client,engine,InventoryIconRecipe.RecipeId,items){Pins=pins};
    static AssetPrepException ApRefused(Action action,AssetPrepFailure failure,string label,string? container=null){
        try{action();}
        catch(AssetPrepException ex){
            Check(ex.Failure==failure,$"{label}: refused as {ex.Failure}, expected {failure} — {ex.Message}");
            if(container!=null)Check(ex.Container==container&&ex.Message.Contains(container,StringComparison.Ordinal),$"{label}: names container {container} — [{ex.Container}] {ex.Message}");
            return ex;
        }
        throw new Exception(label+": not refused");
    }
    static uint[] ApPngPixels(string path,out int w,out int h){
        using var stream=new MemoryStream(File.ReadAllBytes(path));
        using var bmp=new Bitmap(stream);
        w=bmp.Width;h=bmp.Height;var px=new uint[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)px[y*w+x]=unchecked((uint)bmp.GetPixel(x,y).ToArgb());
        return px;
    }
    static string ApTreeHash(string dir)=>string.Join("\n",Directory.GetFiles(dir,"*",SearchOption.AllDirectories).OrderBy(f=>f,StringComparer.Ordinal)
        .Select(f=>Path.GetRelativePath(dir,f)+":"+Sha(File.ReadAllBytes(f))));
    static string[] ApNames(string dir)=>Directory.GetFileSystemEntries(dir).Select(f=>Path.GetFileName(f)!).OrderBy(n=>n,StringComparer.Ordinal).ToArray();
    static bool ApJunction(string link,string target){
        if(!OperatingSystem.IsWindows()){Directory.CreateSymbolicLink(link,target);return true;}
        var info=new ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string a in new[]{"/c","mklink","/J",link,target})info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;
        string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();p.WaitForExit();
        bool ok=p.ExitCode==0&&Directory.Exists(link)&&(File.GetAttributes(link)&FileAttributes.ReparsePoint)!=0;
        if(!ok)Console.Error.WriteLine("mklink /J failed: "+output);
        return ok;
    }
    /// <summary>測試用 recipe：不讀輸入，由委派決定寫什麼。沒有 [AssetRecipe]，只會被明確傳給 AssetPrepRunner。</summary>
    sealed class ApProbeRecipe(string id,Action<string,AssetRecipeItemContext> produce,AssetRecipeCoverage coverage=AssetRecipeCoverage.Partial):IAssetRecipe{
        public string Id=>id;
        public int Version=>1;
        public AssetRecipeCoverage Coverage=>coverage;
        public IReadOnlyList<string> Normalize(IReadOnlyList<string> items)=>items.Distinct(StringComparer.Ordinal).ToList();
        public void Produce(string item,AssetRecipeItemContext context)=>produce(item,context);
    }

    [CheckGroup("asset-prep",Order=1),CheckGroup("native-ui-resources",Order=60),CheckGroup(CheckGroups.Default)]
    static void AssetPrepChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1036","run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(root);
        int caseNo=0;string Case(string tag)=>Path.Combine(root,$"{++caseNo:00}-{tag}");
        const string id=InventoryIconRecipe.RecipeId;
        string Active(string engine)=>Path.Combine(engine,"assets",id);
        string Staging(string engine)=>Path.Combine(engine,"runtime","assets-staging",id);
        string Receipt(string dir)=>Path.Combine(dir,AssetReceipt.FileName);

        // --- 0. 自動發現：產品組件只靠 [AssetRecipe] 註冊，沒有改 Program.cs 或註冊表。
        Check(AssetRecipeDiscovery.Product.Errors.Count==0,"AP discovery has no errors: "+string.Join(" | ",AssetRecipeDiscovery.Product.Errors));
        Check(AssetRecipeDiscovery.Product.Recipes.Any(r=>r is InventoryIconRecipe{Coverage:AssetRecipeCoverage.Partial}),"AP inventory recipe discovered, coverage partial");
        var runner=AssetPrepRunner.Product();

        // --- 1. 兩個輸出目錄：decoded RGBA、offset、收據完全相同；與 ItemIconSource 的解碼一致。
        string client=Case("client");
        byte[] t124=ApTbt(32,32,1,3,1),t40c=ApTbt(24,30,2,0,2),t40=Tbt(32,32,0x7C00),t7=ApTbt(17,9,3,5,0);
        var pins=ApClient(client,("124.tbt",t124),("40c.tbt",t40c),("40.tbt",t40),("main:7.tbt",t7),("900.tbt",Tbt(4,4,0x001F,2,6)));
        string clientBefore=ApTreeHash(client);
        string pair=Case("two-dirs"),engineA=Path.Combine(pair,"引擎 甲"),engineB=Path.Combine(pair,"engine-b");
        var a=runner.Run(ApRequest(client,engineA,pins,"124","7","40"));
        var b=runner.Run(ApRequest(client,engineB,pins,"40","124","7","7"));
        Check(a.Outcome==AssetPrepOutcome.Published&&b.Outcome==AssetPrepOutcome.Published,"AP both directories published");
        Check(a.Produced.SequenceEqual(new[]{"7","40","124"})&&b.Produced.SequenceEqual(a.Produced)&&a.Reused.Count==0,"AP items in canonical order regardless of request order: "+string.Join(",",a.Produced));
        Check(a.ActiveDirectory==Active(engineA)&&!Directory.Exists(Staging(engineA))&&!Directory.Exists(Staging(engineA)+".previous"),"AP published to assets/inventory; staging and swap directories are gone");
        Check(ApNames(Active(engineA)).SequenceEqual(new[]{"124.png","40.png","7.png",AssetReceipt.FileName})&&ApNames(Active(engineB)).SequenceEqual(ApNames(Active(engineA))),"AP active directory holds exactly the PNGs and the receipt: "+string.Join(",",ApNames(Active(engineA))));
        byte[] receiptA=File.ReadAllBytes(Receipt(Active(engineA))),receiptB=File.ReadAllBytes(Receipt(Active(engineB)));
        Check(receiptA.AsSpan().SequenceEqual(receiptB),"AP receipts of the two directories are byte-identical");
        string receiptText=Encoding.UTF8.GetString(receiptA);
        Check(a.Receipt.Status==AssetReceipt.Partial&&receiptText.Contains("\"status\": \"partial\"",StringComparison.Ordinal)&&!receiptText.Contains("ready",StringComparison.OrdinalIgnoreCase),"AP receipt is partial and never says ready");
        Check(!receiptText.Contains("引擎",StringComparison.Ordinal)&&!receiptText.Contains("scratch",StringComparison.OrdinalIgnoreCase)&&!receiptText.Contains(":\\",StringComparison.Ordinal),"AP receipt carries no path");
        var loaded=AssetReceipt.TryLoad(Receipt(Active(engineA)))??throw new Exception("AP receipt does not load");
        Check(loaded is {Schema:AssetReceipt.SchemaId,ToolVersion:AssetPrepRunner.ToolVersion,RecipeId:id,RecipeVersion:1}&&loaded.Requested.SequenceEqual(a.Produced),"AP receipt records schema, tool version, recipe and requested items");
        Check(loaded.Inputs.Count==17&&loaded.Inputs.Zip(pins.Sprites).All(p=>p.First.Name==p.Second.Name&&p.First.IndexSha256==p.Second.IndexSha256&&p.First.PakSha256==p.Second.PakSha256&&p.First.PakSize==p.Second.PakSize),"AP receipt records idx / pak SHA-256 and pak size of all 17 input containers");
        var hashed=new HashSet<string>{SpriteName(Hn("124.tbt")),SpriteName(Hn("40c.tbt")),"Sprite"};
        Check(loaded.Inputs.Where(i=>i.PakVerified).Select(i=>i.Name).ToHashSet().SetEquals(hashed),"AP receipt marks the paks that were read (and therefore hashed whole): "+string.Join(",",hashed));

        var archives=pins.Sprites.Select(p=>new ClientArchive(client,p)).ToArray();
        try{
            var live=ItemIconSource.FromSprites(archives);
            foreach(var (icon,tbt,where) in new[]{(124,t124,SpriteName(Hn("124.tbt"))+"/124.tbt"),(40,t40c,SpriteName(Hn("40c.tbt"))+"/40c.tbt"),(7,t7,"Sprite/7.tbt")}){
                var expected=TbtImage.Decode(tbt);
                string pngA=Path.Combine(Active(engineA),icon+".png"),pngB=Path.Combine(Active(engineB),icon+".png");
                foreach(string png in new[]{pngA,pngB}){
                    var px=ApPngPixels(png,out int w,out int h);
                    Check(w==expected.Width&&h==expected.Height&&px.AsSpan().SequenceEqual(expected.Argb),$"AP icon {icon}: PNG decodes to the TBT's RGBA ({w}×{h}) in {Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(png))))}");
                }
                Check(File.ReadAllBytes(pngA).AsSpan().SequenceEqual(File.ReadAllBytes(pngB)),$"AP icon {icon}: PNG bytes identical in both directories");
                var shown=live.Get(icon)??throw new Exception($"AP ItemIconSource has no icon {icon}");
                bool same=shown.Bitmap.Width==expected.Width&&shown.Bitmap.Height==expected.Height;
                for(int y=0;same&&y<expected.Height;y++)for(int x=0;x<expected.Width;x++)same&=unchecked((uint)shown.Bitmap.GetPixel(x,y).ToArgb())==expected.Argb[y*expected.Width+x];
                Check(same&&shown.Offset==new Point(expected.OffsetX,expected.OffsetY)&&shown.Where==where,$"AP icon {icon}: same pixels, offset and source as ItemIconSource ({shown.Where})");
                var item=loaded.Completed.Single(c=>c.Item==icon.ToString());
                Check(item.Metadata["offsetX"]==expected.OffsetX.ToString()&&item.Metadata["offsetY"]==expected.OffsetY.ToString()&&item.Metadata["width"]==expected.Width.ToString()&&item.Metadata["height"]==expected.Height.ToString(),$"AP icon {icon}: receipt records offset ({item.Metadata["offsetX"]},{item.Metadata["offsetY"]}) and size");
                Check(item.Metadata["rgbaSha256"]==Sha(PngWriter.Rgba(expected.Argb)),$"AP icon {icon}: receipt records the decoded RGBA SHA-256");
                var source=item.Sources.Single();var output=item.Outputs.Single();
                Check(source.Container+"/"+source.Entry==where&&source.Size==tbt.Length&&source.Sha256==Sha(tbt),$"AP icon {icon}: receipt records the source entry {where} and its SHA-256");
                Check(output.RelativePath==icon+".png"&&output.Size==new FileInfo(pngA).Length&&output.Sha256==Sha(File.ReadAllBytes(pngA)),$"AP icon {icon}: receipt records the output size and SHA-256");
            }
            Check(loaded.Completed.Single(c=>c.Item=="124").Metadata is var m124&&m124["offsetX"]=="3"&&m124["offsetY"]=="1","AP icon 124 offset (3,1) from the .tbt header");
        }finally{foreach(var archive in archives)archive.Dispose();}

        // 已是最新：不重寫正式目錄。
        var stamp=File.GetLastWriteTimeUtc(Receipt(Active(engineA)));
        var again=runner.Run(ApRequest(client,engineA,pins,"7","40","124"));
        Check(again.Outcome==AssetPrepOutcome.UpToDate&&again.Produced.Count==0&&again.Reused.Count==3&&File.GetLastWriteTimeUtc(Receipt(Active(engineA)))==stamp&&!Directory.Exists(Staging(engineA)),"AP identical request on a verified active directory: up to date, nothing rewritten");
        // 正式目錄被改壞：重新產生並替換。
        File.WriteAllBytes(Path.Combine(Active(engineA),"40.png"),[1,2,3]);
        var repaired=runner.Run(ApRequest(client,engineA,pins,"7","40","124"));
        Check(repaired.Outcome==AssetPrepOutcome.Published&&repaired.Produced.Count==3&&File.ReadAllBytes(Path.Combine(Active(engineA),"40.png")).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(Active(engineB),"40.png")))&&File.ReadAllBytes(Receipt(Active(engineA))).AsSpan().SequenceEqual(receiptA),"AP damaged active directory is rebuilt and replaced");
        // 換一組項目：整個目錄換成新版本，舊檔不殘留。
        var swapped=runner.Run(ApRequest(client,engineA,pins,"900"));
        Check(swapped.Outcome==AssetPrepOutcome.Published&&ApNames(Active(engineA)).SequenceEqual(new[]{"900.png",AssetReceipt.FileName})&&!Directory.Exists(Staging(engineA)+".previous"),"AP new item set replaces the active directory as a whole");
        Check(swapped.Receipt.Completed.Single().Metadata is var m900&&m900["offsetX"]=="2"&&m900["offsetY"]=="6","AP icon 900 offset (2,6)");
        // 新的一輪失敗：正式目錄維持原樣；staging 留下可辨識的未完成狀態；之後重跑沿用已完成的項目。
        byte[] activeBefore=File.ReadAllBytes(Receipt(Active(engineA)));
        var missing=ApRefused(()=>runner.Run(ApRequest(client,engineA,pins,"900","901")),AssetPrepFailure.MissingEntry,"AP icon in no container");
        Check(missing.Message.Contains("901c.tbt",StringComparison.Ordinal)&&missing.Message.Contains("Sprite/901.tbt",StringComparison.Ordinal),"AP missing icon lists the candidates it looked for: "+missing.Message);
        Check(File.ReadAllBytes(Receipt(Active(engineA))).AsSpan().SequenceEqual(activeBefore)&&ApNames(Active(engineA)).Length==2,"AP failed run leaves the published directory untouched");
        Check(AssetReceipt.TryLoad(Receipt(Staging(engineA))) is {Status:AssetReceipt.InProgress} half&&half.Completed.Select(c=>c.Item).SequenceEqual(new[]{"900"}),"AP failed run leaves an in-progress receipt in staging");
        var continued=runner.Run(ApRequest(client,engineA,pins,"900","7"));
        Check(continued.Outcome==AssetPrepOutcome.Published&&continued.Reused.SequenceEqual(new[]{"900"})&&continued.Produced.SequenceEqual(new[]{"7"})&&ApNames(Active(engineA)).SequenceEqual(new[]{"7.png","900.png",AssetReceipt.FileName}),"AP later run reuses the verified staged item and publishes");

        // --- 2. 輸入被拒：缺檔、錯 pin、截斷、越界；每一種都指出容器，而且沒有任何東西被發布。
        {
            string engine=Case("reject-engine");
            void Rejected(string label,AssetPrepFailure failure,string? container,string? c,AssetPrepPins p,params string[] items){
                ApRefused(()=>runner.Run(ApRequest(c,engine,p,items)),failure,label,container);
                Check(!Directory.Exists(Active(engine)),label+": nothing published");
            }
            Rejected("AP client root not given",AssetPrepFailure.ClientRoot,null,null,pins,"124");
            Rejected("AP client root empty",AssetPrepFailure.ClientRoot,null,"",pins,"124");
            Rejected("AP client root relative",AssetPrepFailure.ClientRoot,null,"client",pins,"124");
            Rejected("AP client root missing",AssetPrepFailure.ClientRoot,null,Path.Combine(root,"no-such-client"),pins,"124");
            // 支援版本（真實 pin）對上別的客戶端：回報第一個不符的容器，不改讀其他目錄。
            var version=ApRefused(()=>runner.Run(new AssetPrepRequest(client,engine,id,["124"])),AssetPrepFailure.ContainerMismatch,"AP supported-version pins against another client","Sprite");
            Check(version.Message.Contains("Sprite.pak",StringComparison.Ordinal)&&!version.Message.Contains("0-客戶端",StringComparison.Ordinal)&&!Directory.Exists(Active(engine)),"AP version mismatch names the container file and no development path: "+version.Message);

            string c1=Case("missing-pak");var p1=ApClient(c1,("124.tbt",t124));File.Delete(Path.Combine(c1,"Sprite05.pak"));
            Rejected("AP missing pak",AssetPrepFailure.MissingContainer,"Sprite05",c1,p1,"124");
            string c2=Case("missing-idx");var p2=ApClient(c2,("124.tbt",t124));File.Delete(Path.Combine(c2,"Sprite11.idx"));
            Rejected("AP missing idx",AssetPrepFailure.MissingContainer,"Sprite11",c2,p2,"124");

            int k=Hn("124.tbt");string kName=SpriteName(k);
            Rejected("AP wrong idx pin",AssetPrepFailure.ContainerMismatch,"Sprite02",client,ApWithPin(pins,3,p=>p with{IndexSha256=new string('0',64)}),"124");
            Rejected("AP wrong pak pin",AssetPrepFailure.ContainerMismatch,kName,client,ApWithPin(pins,k,p=>p with{PakSha256=new string('0',64)}),"124");
            Rejected("AP wrong pak size pin",AssetPrepFailure.ContainerMismatch,"Sprite15",client,ApWithPin(pins,16,p=>p with{PakSize=p.PakSize+1}),"124");

            string c3=Case("truncated-pak");var p3=ApClient(c3,("124.tbt",t124));
            string pak3=Path.Combine(c3,kName+".pak");File.WriteAllBytes(pak3,File.ReadAllBytes(pak3)[..^1]);
            Rejected("AP truncated pak",AssetPrepFailure.ContainerMismatch,kName,c3,p3,"124");
            string c4=Case("truncated-idx");var p4=ApClient(c4,("124.tbt",t124));
            string idx4=Path.Combine(c4,kName+".idx");byte[] cut=File.ReadAllBytes(idx4)[..^5];File.WriteAllBytes(idx4,cut);
            Rejected("AP truncated idx (pinned version)",AssetPrepFailure.ContainerMismatch,kName,c4,p4,"124");
            Rejected("AP truncated idx (structure)",AssetPrepFailure.CorruptContainer,kName,c4,ApWithPin(p4,k,p=>p with{IndexSha256=Sha(cut)}),"124");

            string c5=Case("out-of-range");var p5=ApClient(c5,("124.tbt",t124));
            byte[] idx5=[..BitConverter.GetBytes(1u),..IdxRecord(10,Name20("124.tbt"),1000)];
            var bad5=RawArchive(c5,kName,idx5,new byte[20]);
            Rejected("AP entry beyond the pak",AssetPrepFailure.CorruptContainer,kName,c5,ApWithPin(p5,k,_=>bad5),"124");
            string c6=Case("oversized-entry");var p6=ApClient(c6,("124.tbt",new byte[ClientArchive.MaxEntrySize+1]));
            Rejected("AP entry larger than the reader's limit",AssetPrepFailure.CorruptContainer,kName,c6,p6,"124");

            string c7=Case("truncated-tbt");var p7=ApClient(c7,("124.tbt",t124[..^7]),("125.tbt",t124.Append((byte)0).ToArray()),("126.tbt",new byte[]{0,0,4,1,1,2,4,1,0,2,0,3,0,4,0}));
            Rejected("AP truncated .tbt",AssetPrepFailure.CorruptEntry,kName,c7,p7,"124");
            Rejected("AP .tbt with trailing bytes",AssetPrepFailure.CorruptEntry,SpriteName(Hn("125.tbt")),c7,p7,"125");
            Rejected("AP .tbt row past its width",AssetPrepFailure.CorruptEntry,SpriteName(Hn("126.tbt")),c7,p7,"126");
            Rejected("AP missing icon",AssetPrepFailure.MissingEntry,null,client,pins,"4321");

            // 要求本身不合法：項目不是圖示編號（含路徑穿越）、空清單、未知或不合法的 recipe id、引擎目錄不是絕對路徑。
            foreach(string item in new[]{"../124","..","124/..","124.png","0","01","-1","65536","abc",""})Rejected("AP item \""+item+"\"",AssetPrepFailure.InvalidRequest,null,client,pins,item);
            Rejected("AP empty item list",AssetPrepFailure.InvalidRequest,null,client,pins);
            foreach(string recipe in new[]{"../inventory","inventory/..","..","inventory\\x","Inventory","","nul"})
                ApRefused(()=>runner.Run(new AssetPrepRequest(client,engine,recipe,["124"]){Pins=pins}),AssetPrepFailure.UnsafePath,"AP recipe id \""+recipe+"\"");
            ApRefused(()=>runner.Run(new AssetPrepRequest(client,engine,"no-such-recipe",["124"]){Pins=pins}),AssetPrepFailure.InvalidRequest,"AP unknown recipe");
            ApRefused(()=>runner.Run(ApRequest(client,"relative-engine",pins,"124")),AssetPrepFailure.InvalidRequest,"AP relative engine root");
            // 引擎目錄在原客戶端裡面：拒絕，而且沒有在客戶端建立任何東西。
            ApRefused(()=>runner.Run(ApRequest(client,Path.Combine(client,"engine"),pins,"124")),AssetPrepFailure.InvalidRequest,"AP engine root inside the client");
            Check(!Directory.Exists(Path.Combine(client,"engine"))&&!Directory.Exists(Active(engine)),"AP nothing created inside the client, nothing published by any rejected request");
        }

        // --- 3. 中斷與重試：未完成的 staging 不發布；重跑先驗已完成的項目，壞掉的重做，多出來的清掉。
        {
            string c=Case("resume-client");
            int[] icons=[11,12,13,14,15];
            var p=ApClient(c,icons.Select(i=>($"{i}.tbt",ApTbt(8+i,6+i%3,i,(byte)i,(byte)(i%4)))).ToArray());
            string engine=Case("resume-engine"),fresh=Case("resume-fresh");
            string[] items=icons.Select(i=>i.ToString()).ToArray();
            int completed=0;
            Throws<InvalidOperationException>(()=>runner.Run(ApRequest(c,engine,p,items) with{ItemCompleted=_=>{if(++completed==2)throw new InvalidOperationException("synthetic interrupt");}}),"AP run interrupted after two items","synthetic interrupt");
            Check(!Directory.Exists(Active(engine)),"AP unfinished staging is not published");
            Check(AssetReceipt.TryLoad(Receipt(Staging(engine))) is {Status:AssetReceipt.InProgress} staged&&staged.Completed.Select(x=>x.Item).SequenceEqual(new[]{"11","12"})&&staged.Requested.Count==5,"AP interrupted staging: in-progress receipt lists the two finished items");
            Check(ApNames(Staging(engine)).SequenceEqual(new[]{"11.png","12.png",AssetReceipt.FileName}),"AP interrupted staging holds the two finished PNGs: "+string.Join(",",ApNames(Staging(engine))));
            // 弄壞一個已完成的輸出，再丟進不相干的檔案與目錄。
            File.WriteAllBytes(Path.Combine(Staging(engine),"11.png"),[9,9,9]);
            File.WriteAllText(Path.Combine(Staging(engine),"stray.bin"),"x");
            Directory.CreateDirectory(Path.Combine(Staging(engine),"junk","deep"));File.WriteAllText(Path.Combine(Staging(engine),"junk","deep","old.png"),"x");
            var resumed=runner.Run(ApRequest(c,engine,p,items));
            Check(resumed.Outcome==AssetPrepOutcome.Published&&resumed.Reused.SequenceEqual(new[]{"12"})&&resumed.Produced.SequenceEqual(new[]{"11","13","14","15"}),"AP retry reuses the intact item and redoes the damaged one: reused "+string.Join(",",resumed.Reused)+"; produced "+string.Join(",",resumed.Produced));
            Check(ApNames(Active(engine)).SequenceEqual(new[]{"11.png","12.png","13.png","14.png","15.png",AssetReceipt.FileName}),"AP retry publishes exactly the requested files (stray file and directory removed): "+string.Join(",",ApNames(Active(engine))));
            var clean=runner.Run(ApRequest(c,fresh,p,items));
            Check(clean.Reused.Count==0&&ApTreeHash(Active(engine))==ApTreeHash(Active(fresh)),"AP resumed result is byte-identical to an uninterrupted run (PNGs and receipt)");

            // 取消：開始前與進行中。
            string cancelEngine=Case("cancel-engine");
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();
            Throws<OperationCanceledException>(()=>runner.Run(ApRequest(c,cancelEngine,p,items) with{Cancel=cancelled.Token}),"AP cancelled before start");
            Check(!Directory.Exists(Active(cancelEngine))&&!Directory.Exists(Staging(cancelEngine)),"AP cancelled before start: nothing staged or published");
            using var midway=new CancellationTokenSource();
            Throws<OperationCanceledException>(()=>runner.Run(ApRequest(c,cancelEngine,p,items) with{Cancel=midway.Token,ItemCompleted=_=>midway.Cancel()}),"AP cancelled after the first item");
            Check(!Directory.Exists(Active(cancelEngine))&&AssetReceipt.TryLoad(Receipt(Staging(cancelEngine))) is {Status:AssetReceipt.InProgress,Completed.Count:1},"AP cancelled run: one item staged, nothing published");
            var afterCancel=runner.Run(ApRequest(c,cancelEngine,p,items));
            Check(afterCancel.Outcome==AssetPrepOutcome.Published&&afterCancel.Reused.SequenceEqual(new[]{"11"})&&ApTreeHash(Active(cancelEngine))==ApTreeHash(Active(fresh)),"AP run after a cancel finishes from the staged item");
            // 來源變了（同一個 entry 的 bytes 不同，pin 也跟著換）：先前 staging 的東西不沿用。
            completed=0;string changedEngine=Case("changed-engine");
            Throws<InvalidOperationException>(()=>runner.Run(ApRequest(c,changedEngine,p,items) with{ItemCompleted=_=>{if(++completed==2)throw new InvalidOperationException("synthetic interrupt");}}),"AP second interrupted run","synthetic interrupt");
            string c2=Case("resume-client-2");
            var p2=ApClient(c2,icons.Select(i=>($"{i}.tbt",ApTbt(8+i,6+i%3,i+100,(byte)i,(byte)(i%4)))).ToArray());
            var other=runner.Run(ApRequest(c2,changedEngine,p2,items));
            Check(other.Reused.Count==0&&other.Produced.Count==5&&ApTreeHash(Active(changedEngine))!=ApTreeHash(Active(fresh)),"AP staged items of another input version are not reused");

            // 發布在兩次改名之間中斷：下一次執行先把舊的正式目錄放回去。
            Directory.Move(Active(fresh),Staging(fresh)+".previous");
            var recovered=runner.Run(ApRequest(c,fresh,p,items));
            Check(recovered.Outcome==AssetPrepOutcome.UpToDate&&!Directory.Exists(Staging(fresh)+".previous")&&ApTreeHash(Active(fresh))==ApTreeHash(Active(engine)),"AP interrupted publish: previous active directory restored");
            Directory.CreateDirectory(Staging(fresh)+".previous");File.WriteAllText(Path.Combine(Staging(fresh)+".previous","old.png"),"x");
            Check(runner.Run(ApRequest(c,fresh,p,items)).Outcome==AssetPrepOutcome.UpToDate&&!Directory.Exists(Staging(fresh)+".previous"),"AP leftover swap directory is removed once the active directory is in place");
            // 發布前的最後驗證：recipe 寫完之後檔案被改掉 → 不發布。
            string tamperEngine=Case("tamper-engine");
            var tamper=new AssetPrepRunner([new ApProbeRecipe("probe",(item,context)=>{
                context.Write(item+".bin",Encoding.ASCII.GetBytes(item));
                if(item=="b")File.WriteAllText(Path.Combine(tamperEngine,"runtime","assets-staging","probe","a.bin"),"changed");
            })]);
            ApRefused(()=>tamper.Run(new AssetPrepRequest(c,tamperEngine,"probe",["a","b"]){Pins=p}),AssetPrepFailure.Verification,"AP staged output changed before publish");
            Check(!Directory.Exists(Path.Combine(tamperEngine,"assets","probe")),"AP output that fails the final verification is not published");
        }

        // --- 4. 同時只允許一個 writer。
        {
            string engine=Case("lock-engine");
            var request=new AssetPrepRequest(client,engine,"probe",["a","b"]){Pins=pins};
            AssetPrepRunner? probe=null;AssetPrepException? busy=null;
            probe=new AssetPrepRunner([new ApProbeRecipe("probe",(item,context)=>{
                if(item=="a")busy=ApRefused(()=>probe!.Run(request),AssetPrepFailure.Busy,"AP second writer while the first is producing");
                context.Write("sub/"+item+".txt",Encoding.ASCII.GetBytes(item));
            },AssetRecipeCoverage.Complete)]);
            var first=probe.Run(request);
            string probeActive=Path.Combine(engine,"assets","probe");
            Check(busy!=null&&first.Outcome==AssetPrepOutcome.Published&&first.ActiveDirectory==probeActive&&File.ReadAllText(Path.Combine(probeActive,"sub","a.txt"))=="a"&&File.ReadAllText(Path.Combine(probeActive,"sub","b.txt"))=="b","AP first writer finishes after the second was refused; nested output paths work");
            Check(first.Receipt.Status==AssetReceipt.Complete&&!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(probeActive,AssetReceipt.FileName))).Contains("ready",StringComparison.OrdinalIgnoreCase),"AP a complete recipe says complete (still no ready)");
            string before=ApTreeHash(probeActive);
            using(AssetPrepLock.Acquire(Path.Combine(engine,"runtime","assets-staging","probe.lock"),"probe"))
                ApRefused(()=>probe!.Run(request with{Items=new[]{"c"}}),AssetPrepFailure.Busy,"AP writer refused while the lock is held");
            Check(ApTreeHash(probeActive)==before&&!Directory.Exists(Path.Combine(engine,"runtime","assets-staging","probe")),"AP refused writer changed nothing");
            Check(probe.Run(request).Outcome==AssetPrepOutcome.UpToDate,"AP lock is released when the writer ends");
        }

        // --- 5. 路徑穿越：recipe 的輸出路徑、recipe id。
        {
            string engine=Case("traversal-engine");
            foreach(string bad in new[]{"../escape.txt","..\\escape.txt","sub/../../escape.txt","sub\\..\\..\\..\\escape.txt","/escape.txt","\\escape.txt","C:\\escape.txt","C:escape.txt","escape.txt:stream","..","",AssetReceipt.FileName,AssetReceipt.FileName+".tmp","nul","sub/con.txt"}){
                var r=new AssetPrepRunner([new ApProbeRecipe("probe",(_,context)=>context.Write(bad,[1]))]);
                ApRefused(()=>r.Run(new AssetPrepRequest(client,engine,"probe",["a"]){Pins=pins}),AssetPrepFailure.UnsafePath,"AP output path \""+bad+"\"");
                Check(!Directory.Exists(Path.Combine(engine,"assets","probe")),"AP output path \""+bad+"\": nothing published");
            }
            Check(Directory.GetFiles(root,"escape.txt*",SearchOption.AllDirectories).Length==0,"AP no file escaped its recipe directory");
            var twice=new AssetPrepRunner([new ApProbeRecipe("probe",(_,context)=>{context.Write("same.bin",[1]);context.Write("SAME.bin",[2]);})]);
            ApRefused(()=>twice.Run(new AssetPrepRequest(client,engine,"probe",["a"]){Pins=pins}),AssetPrepFailure.InvalidRequest,"AP one output path written twice");
            foreach(string badId in new[]{"../probe","probe/x","..","Probe","probe.previous","probe.lock",""})
                ApRefused(()=>new AssetPrepRunner([new ApProbeRecipe(badId,(_,_)=>{})]),AssetPrepFailure.UnsafePath,"AP recipe with id \""+badId+"\"");
            ApRefused(()=>new AssetPrepRunner([new ApProbeRecipe("probe",(_,_)=>{}),new ApProbeRecipe("probe",(_,_)=>{})]),AssetPrepFailure.InvalidRequest,"AP duplicate recipe id");
        }

        // --- 6. Text 容器：依名稱找 entry，以解碼後 SHA-256 核對（不沿用舊 offset）。
        {
            string c=Case("text-client");var p=ApClient(c,("124.tbt",t124));
            byte[] table=Encoding.ASCII.GetBytes("3\nalpha\nbeta\ngamma\n"),packed=TextArchive.Encode(table);
            File.WriteAllBytes(Path.Combine(c,"Text.idx"),[..BitConverter.GetBytes(1u),..TextArchive.Encode(IdxRecord(0,Name20("probe-c.tbl"),(uint)packed.Length))]);
            File.WriteAllBytes(Path.Combine(c,"Text.pak"),packed);
            using(var input=AssetPrepInput.Open(c,p)){
                var hit=input.ReadText("probe-c.tbl",Sha(table));
                Check(hit.Decoded.AsSpan().SequenceEqual(table)&&hit.Source is {Container:"Text",Entry:"probe-c.tbl",Offset:0}&&hit.Source.Sha256==Sha(table),"AP Text entry decoded and matched to its pin");
                ApRefused(()=>input.ReadText("probe-c.tbl",new string('0',64)),AssetPrepFailure.ContainerMismatch,"AP Text entry of another version","Text");
                ApRefused(()=>input.ReadText("absent.tbl",Sha(table)),AssetPrepFailure.MissingEntry,"AP Text entry missing","Text");
            }
            File.WriteAllBytes(Path.Combine(c,"Text.idx"),[..BitConverter.GetBytes(2u),..TextArchive.Encode(IdxRecord(0,Name20("probe-c.tbl"),(uint)packed.Length))]);
            using(var input=AssetPrepInput.Open(c,p))ApRefused(()=>input.ReadText("probe-c.tbl",Sha(table)),AssetPrepFailure.CorruptContainer,"AP truncated Text index","Text");
            File.Delete(Path.Combine(c,"Text.pak"));
            using(var input=AssetPrepInput.Open(c,p))ApRefused(()=>input.ReadText("probe-c.tbl",Sha(table)),AssetPrepFailure.MissingContainer,"AP Text.pak missing","Text");
        }

        // --- 7. junction／symlink：目標或任何祖先是連結就拒絕，連結另一端不被寫入也不被刪除。
        var links=new List<string>();
        try{
            string j=Case("junction");Directory.CreateDirectory(j);
            string Sealed(string name){string dir=Path.Combine(j,name);Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"keep.txt"),"sealed");return dir;}
            bool Untouched(string dir)=>ApNames(dir).SequenceEqual(new[]{"keep.txt"})&&File.ReadAllText(Path.Combine(dir,"keep.txt"))=="sealed";
            void Link(string link,string target){
                Directory.CreateDirectory(Path.GetDirectoryName(link)!);
                Check(ApJunction(link,target),"AP junction created: "+Path.GetRelativePath(j,link));links.Add(link);
            }
            void Refuse(string label,string engine,string target){
                var ex=ApRefused(()=>runner.Run(ApRequest(client,engine,pins,"124")),AssetPrepFailure.UnsafePath,label);
                Check(ex.Message.Contains("junction",StringComparison.Ordinal)&&Untouched(target),label+": other side of the link untouched — "+ex.Message);
            }
            // (a) 開發機的樣子：engine/assets 是指向封存的 junction。
            string e1=Path.Combine(j,"e1"),s1=Sealed("sealed1");Link(Path.Combine(e1,"assets"),s1);
            Refuse("AP engine/assets is a junction",e1,s1);
            Check(!Directory.Exists(Path.Combine(e1,"runtime")),"AP junction refused before anything is staged");
            // (b) 正式目錄本身是 junction。
            string e2=Path.Combine(j,"e2"),s2=Sealed("sealed2");Link(Active(e2),s2);
            Refuse("AP active directory is a junction",e2,s2);
            // (c) 祖先 engine/runtime 是 junction。
            string e3=Path.Combine(j,"e3"),s3=Sealed("sealed3");Link(Path.Combine(e3,"runtime"),s3);
            Refuse("AP engine/runtime is a junction",e3,s3);
            // (d) staging 目錄本身是 junction。
            string e4=Path.Combine(j,"e4"),s4=Sealed("sealed4");Link(Staging(e4),s4);
            Refuse("AP staging directory is a junction",e4,s4);
            // (e) 引擎根目錄經由 junction 到達。
            string s5=Sealed("sealed5"),e5=Path.Combine(j,"e5-link");Link(e5,s5);
            Refuse("AP engine root reached through a junction",e5,s5);
            Refuse("AP engine root below a junction",Path.Combine(e5,"engine"),s5);
            // (f) 中斷後的 staging 裡被放了 junction：重跑不跟進去、不刪另一端、不發布。
            string e6=Path.Combine(j,"e6"),s6=Sealed("sealed6");
            Throws<InvalidOperationException>(()=>runner.Run(ApRequest(client,e6,pins,"124","7") with{ItemCompleted=_=>throw new InvalidOperationException("synthetic interrupt")}),"AP interrupted run before planting a junction","synthetic interrupt");
            Link(Path.Combine(Staging(e6),"planted"),s6);
            Refuse("AP junction inside staging",e6,s6);
            Check(!Directory.Exists(Active(e6)),"AP junction inside staging: nothing published");
            // (g) 發布後的正式目錄裡被放了 junction：換版本時不跟進去刪。
            string e7=Path.Combine(j,"e7"),s7=Sealed("sealed7");
            runner.Run(ApRequest(client,e7,pins,"124"));
            Link(Path.Combine(Active(e7),"planted"),s7);
            Refuse("AP junction inside the active directory",e7,s7);
            // (h) symlink（需要權限；建立不了就略過，不算通過也不算失敗）。
            string e8=Path.Combine(j,"e8"),s8=Sealed("sealed8");bool symlink=false;
            try{Directory.CreateDirectory(e8);Directory.CreateSymbolicLink(Path.Combine(e8,"assets"),s8);links.Add(Path.Combine(e8,"assets"));symlink=true;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Console.Error.WriteLine("AP symlink case skipped: "+ex.Message);}
            if(symlink)Refuse("AP engine/assets is a symlink",e8,s8);
        }finally{
            foreach(string link in links)try{if(Directory.Exists(link))Directory.Delete(link,false);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Console.Error.WriteLine("AP link not removed: "+link+": "+ex.Message);}
        }

        // --- 8. 整輪下來原客戶端沒有任何變動。
        Check(ApTreeHash(client)==clientBefore,"AP client directory is byte-identical after every run (read-only input)");
    }
}
