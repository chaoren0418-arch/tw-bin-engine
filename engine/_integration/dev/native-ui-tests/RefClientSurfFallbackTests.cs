using P06.NativeUi;

// T322（#760）P-B：參考客戶端 surf 備援（RefClientSurfFallback＋SurfCatalog.Get 的單一掛點）。
// 主客戶端與參考客戶端的 Tile／Sprite*.idx／.pak、SurfList.xml、.img、.tbt 全是本檔合成，寫在 scratch/issue-760/ 下；
// 不讀任何真實客戶端或參考客戶端、不啟動遊戲、不連網路。只刪本檔建立的檔案（不遞迴）。
partial class Checks {
    [CheckGroup("native-ui-ref-surf",Order=1),CheckGroup("native-ui-resources",Order=51),CheckGroup(CheckGroups.Default,Order=127)]
    static void NativeUiRefClientSurfFallbackChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-760","synthetic");
        string mainDir=Path.Combine(root,"main"),refDir=Path.Combine(root,"ref");
        foreach(var d in new[]{mainDir,refDir}){Directory.CreateDirectory(d);foreach(var f in Directory.GetFiles(d))File.Delete(f);}

        // 主客戶端：SurfList 的列
        //   100 file=100 → 本機 100.img（參考也有 100.img，本機要贏）
        //   200 file=200 → 本機沒有，參考有 200c.img（-c 優先）
        //   6523 無 file → 本機有 6523.img（依 %d.img 用本機，不問參考）
        //   7000 無 file → 本機沒有，參考有 7000.img
        //   8734 file=8734 → 參考只有 8734.tbt（Icon，不當 surf）→ null
        //   300 file=300 → 哪裡都沒有 → null
        //   555 file=555 → 參考 idx 裡 555.img 重名 → null
        //   556 file=556 → 參考 556.img 不是合法 .img → null
        //   9183／9184 不在 SurfList → 依 id；參考 Sprite{h} 有，9184 另在參考 Sprite.pak 放一張誘餌（雜湊封存優先）
        var tilePin=Archive(mainDir,"Tile",("SurfList.xml",Xml("""
            <?xml version="1.0"?>
            <!-- synthetic -->
            <SurfList><surf id="100" file="100"/><surf id="200" file="200"/><surf id="6523" filedesc="x"/><surf id="7000" filedesc="Account_Screen"/><surf id="8734" file="8734"/><surf id="300" file="300"/><surf id="555" file="555"/><surf id="556" file="556"/></SurfList>
            """.Trim())));
        var main=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        main[Hn("100.img")].Add(("100.img",Img(3,2,0,0,0x7C00)));
        main[Hn("6523.img")].Add(("6523.img",Img(4,4,0,0,0x03E0)));
        for(int i=0;i<17;i++)main[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var mainPins=Enumerable.Range(0,17).Select(i=>Archive(mainDir,SpriteName(i),main[i].ToArray())).ToArray();

        var refItems=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        byte[] r100=Img(9,9,0,0,0x001F),r200c=Img(5,3,1,0x0000,0x7C00),r200=Img(6,6,0,0),r6523=Img(8,8,0,0),r7000=Img(7,2,0,0,0x03E0),
            r9183=Img(16,12,1,0x0000,0x7FFF),r9184=Img(16,12,0,0,0x1234),decoy=Img(2,2,0,0),r555a=Img(2,2,0,0),r555b=Img(3,3,0,0),bad=[1,0,1,0,0,0,0,0];
        refItems[Hn("100.img")].Add(("100.img",r100));
        refItems[Hn("200c.img")].Add(("200c.img",r200c));
        refItems[Hn("200.img")].Add(("200.img",r200));
        refItems[Hn("6523.img")].Add(("6523.img",r6523));
        refItems[Hn("7000.img")].Add(("7000.img",r7000));
        refItems[Hn("8734.tbt")].Add(("8734.tbt",Tbt(8,8,0x7C00)));
        refItems[Hn("555.img")].Add(("555.img",r555a));refItems[Hn("555.img")].Add(("555.img",r555b));
        refItems[Hn("556.img")].Add(("556.img",bad));
        refItems[Hn("9183.img")].Add(("9183.img",r9183));
        refItems[Hn("9184.img")].Add(("9184.img",r9184));
        refItems[0].Add(("9184.img",decoy));
        for(int i=0;i<17;i++)refItems[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        for(int i=0;i<17;i++)Archive(refDir,SpriteName(i),refItems[i].ToArray());
        var refBytes=Enumerable.Range(0,17).Select(i=>(Sha(File.ReadAllBytes(Path.Combine(refDir,SpriteName(i)+".idx"))),Sha(File.ReadAllBytes(Path.Combine(refDir,SpriteName(i)+".pak"))))).ToArray();
        static string[] Lines(string prefix)=>UiResourceLog.Recent().Where(l=>l.StartsWith(prefix,StringComparison.Ordinal)).ToArray();

        // --- 1. 名稱順序與 SurfCatalog 相同；封存名稱與 UiResourcePins 相同。
        foreach(var f in new[]{"9183","200","7000"}){
            Check(RefClientSurfFallback.Candidates(f).Select(c=>(SpriteName(c.Archive),c.Name)).SequenceEqual(
                new SurfCatalog(new UiXmlSource(new ClientArchive(mainDir,tilePin)),mainPins.Select(p=>new ClientArchive(mainDir,p)).ToArray()).Candidates(f).Select(c=>(c.Archive.Pin.Name,c.Name))),
                "REF-SURF candidates for "+f+" match SurfCatalog.Candidates");
        }
        Check(Enumerable.Range(0,17).All(i=>RefClientSurfFallback.ArchiveName(i)==UiResourcePins.Sprites[i].Name),"REF-SURF archive names = UiResourcePins.Sprites order");

        // --- 2. 沒有掛參考客戶端：行為與先前相同（null，日誌不提參考）。
        using(var plain=new UiResources(mainDir,tilePin,mainPins)){
            Check(plain.Surfs.Reference==null,"REF-SURF no reference by default (synthetic UiResources)");
            Check(plain.Surfs.Get(9183)==null&&plain.Surfs.Get(200)==null&&plain.Surfs.Get(6523)==null,"REF-SURF without reference: missing surfs stay null");
            Check(Lines("surf 200:").LastOrDefault()=="surf 200: 圖檔不在 Sprite*.pak（file=200）","REF-SURF without reference: log unchanged");
            Check(plain.Surfs.Get(100) is {FileName:"100.img",Width:3},"REF-SURF without reference: local surf still served");
        }

        // --- 3. 掛上參考客戶端。
        using var res=new UiResources(mainDir,tilePin,mainPins);
        var fallback=new RefClientSurfFallback(refDir,"synthetic");
        res.Surfs.Reference=fallback;
        var cat=res.Surfs;

        Check(cat.Get(100) is {FileName:"100.img",Width:3,Height:2,Archive:var a100}&&a100==SpriteName(Hn("100.img")),"REF-SURF local hit wins over the reference (100)");
        var s200=cat.Get(200);
        Check(s200 is {FileName:"200c.img",Width:5,Height:3}&&s200.Archive=="ref:"+SpriteName(Hn("200c.img")),"REF-SURF 200: local missing → reference 200c.img (-c first) "+s200?.Archive);
        var s6523=cat.Get(6523);
        Check(s6523 is {FileName:"6523.img",Width:4}&&s6523.Archive==SpriteName(Hn("6523.img")),"REF-SURF 6523: no file attr → local %d.img before the reference "+s6523?.Archive);
        Check(Lines("surf 6523:").LastOrDefault()?.Contains("依 %d.img 用本機")==true,"REF-SURF 6523 log says local id-named file");
        var s7000=cat.Get(7000);
        Check(s7000 is {FileName:"7000.img",Width:7,Height:2}&&s7000.Archive=="ref:"+SpriteName(Hn("7000.img")),"REF-SURF 7000: no file attr, local missing → reference 7000.img");
        var s9183=cat.Get(9183);
        Check(s9183 is {FileName:"9183.img",Width:16,Height:12,Flags:1}&&s9183.Archive=="ref:"+SpriteName(Hn("9183.img")),"REF-SURF 9183: not in SurfList → reference by id");
        Check(s9183!=null&&s9183.Bitmap.GetPixel(0,0).A==255,"REF-SURF 9183 decoded with the normal .img decoder");
        var s9184=cat.Get(9184);
        Check(s9184 is {Width:16,Height:12}&&s9184.Archive=="ref:"+SpriteName(Hn("9184.img")),"REF-SURF 9184: hashed Sprite{h} before Sprite.pak decoy "+s9184?.Archive);
        Check(ReferenceEquals(s9183,cat.Get(9183))&&cat.IsCached(9183),"REF-SURF reference surface cached like a local one");

        string log9183=Lines("surf-ref 9183 ").LastOrDefault()??"";
        Check(log9183.Contains(Sha(r9183))&&log9183.Contains(refBytes[Hn("9183.img")].Item1)&&log9183.Contains("synthetic")&&log9183.Contains(Path.GetFullPath(refDir)),"REF-SURF log records root, payload SHA-256 and idx SHA-256: "+log9183);

        int c8734=Lines("surf 8734:").Length;
        Check(cat.Get(8734)==null,"REF-SURF 8734: reference has only 8734.tbt → null (no icon as surf)");
        var l8734=Lines("surf 8734:");
        Check(l8734.Length==c8734+1&&l8734[^1].EndsWith("；參考客戶端 "+Path.GetFullPath(refDir)+" 也沒有",StringComparison.Ordinal),"REF-SURF missing in both → log names the reference: "+string.Join(" | ",l8734));
        Check(cat.Get(300)==null,"REF-SURF 300: nowhere → null");
        Check(cat.Get(555)==null&&Lines("surf-ref "+SpriteName(Hn("555.img"))+"/555.img").Length>0,"REF-SURF 555: duplicate name in reference idx → null, logged");
        Check(cat.Get(556)==null&&Lines("surf-ref 556 ").Any(l=>l.Contains("不是可解的 .img")),"REF-SURF 556: undecodable reference .img → null, no exception");

        var served=fallback.Served.ToDictionary(s=>s.Name,StringComparer.OrdinalIgnoreCase);
        Check(served.TryGetValue("9184.img",out var f9184)&&f9184.BlobSha256==Sha(r9184)&&f9184.IndexSha256==refBytes[Hn("9184.img")].Item1&&f9184.Root==Path.GetFullPath(refDir),"REF-SURF Served records 9184 hashes");
        Check(!served.ContainsKey("100.img")&&!served.ContainsKey("6523.img"),"REF-SURF reference not read when the main client has the image");
        Check(fallback.TryRead("..\\x")==null&&fallback.TryRead("a.b")==null&&fallback.TryRead("")==null,"REF-SURF path-like names refused");
        Check(Enumerable.Range(0,17).All(i=>fallback.Unavailable(i)==null),"REF-SURF all synthetic reference archives usable");

        // --- 4. 參考客戶端不存在／關閉。
        Check(RefClientSurfFallback.For(null)==null,"REF-SURF For(null) → no fallback (ref-client off)");
        var viaXml=RefClientSurfFallback.For(new RefClientXmlSource(refDir,"settings"));
        Check(viaXml is {Source:"settings"}&&viaXml.Root==Path.GetFullPath(refDir),"REF-SURF For(xml source) shares root and source");
        using(var none=new UiResources(mainDir,tilePin,mainPins)){
            var missing=new RefClientSurfFallback(Path.Combine(root,"no-such-client"));
            none.Surfs.Reference=missing;
            Check(none.Surfs.Get(9183)==null,"REF-SURF missing reference client → null (caller keeps WinForms / skip)");
            Check(missing.Unavailable(Hn("9183.img"))!=null&&missing.Unavailable(0)!=null,"REF-SURF missing reference archives report a reason");
            Check(none.Surfs.Get(100) is {Width:3},"REF-SURF missing reference does not affect local surfs");
        }

        // --- 5. 參考檔案未被改動。
        Check(Enumerable.Range(0,17).All(i=>Sha(File.ReadAllBytes(Path.Combine(refDir,SpriteName(i)+".idx")))==refBytes[i].Item1
            &&Sha(File.ReadAllBytes(Path.Combine(refDir,SpriteName(i)+".pak")))==refBytes[i].Item2),"REF-SURF reference idx/pak untouched");
    }
}
