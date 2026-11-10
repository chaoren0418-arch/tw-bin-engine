using System.Text;
namespace P06.NativeUi;

// R7-N（T354／#817）離線測試：RefClientTextSource（參考客戶端 Text.pak 的 string-c.tbl／desc-c.tbl → TextResolver）與
// 重置窗 Faulted 時改開 WinForms 的判斷（CharResetFallback）。
// 只用合成的 Text.idx／Text.pak（記憶體內，假文字，不是遊戲字串）；不開遊戲、網路、視窗、伺服器、DB，不寫檔。
// 本機有 kor25 參考客戶端時才多核對一次真表的 SHA-256 與筆數（CI 沒有會略過）。由 --module-tests 執行。
static class RefClientTextTests {
    static byte[] Cp950(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}
    static byte[] Table(int count,params (int Id,string Text)[] rows){
        var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        return Cp950(count+"\r\n"+string.Join("\r\n",lines));
    }
    static TextArchive Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return TextArchive.FromBytes([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }
    static string Root=>Path.Combine(AppContext.BaseDirectory,"ref-client-text-synthetic");

    // 合成表：string-c.tbl 5000 筆、desc-c.tbl 300 筆（實際編號只是測試用，文字都是假字）。
    static byte[] FakeStrings=>Table(5000,(0,"零"),(3233,"假陣營"),(4082,"Tip. 假提示\t"),(4391,"\\f3假清單"),(4118,"dummy4120"),(4200,"   "));
    static byte[] FakeDesc=>Table(300,(5,"假說明"),(299,"\\F2假尾"));

    [WorldModuleTest("ref-client-text")]
    static void Run(WorldModuleTestContext t){
        Rules(t);Resolve(t);Missing(t);Fallback(t);LocalReference(t);
    }

    // ---- 編號解析與一筆的顯示文字 ----
    static void Rules(WorldModuleTestContext t){
        t.Equal(4391,RefClientTextSource.RecordId("4391;1"),"\"4391;1\" → 4391（後面是旗標）");
        t.Equal(3233,RefClientTextSource.RecordId(" 3233 "),"前後空白");
        t.Check(RefClientTextSource.RecordId("abc")==null&&RefClientTextSource.RecordId("")==null&&RefClientTextSource.RecordId("-1")==null,"不是非負整數 → null");
        var table=ServerStringTable.Parse(FakeStrings);
        t.Equal("Tip. 假提示",RefClientTextSource.Text(table,4082),"尾端 tab 去掉");
        t.Equal("假清單",RefClientTextSource.Text(table,4391),"色碼 \\f3 去掉");
        t.Check(RefClientTextSource.Text(table,4118)==null,"kor25 佔位字 dummyNNNN → null（不畫）");
        t.Check(RefClientTextSource.Text(table,4200)==null&&RefClientTextSource.Text(table,1)==null,"空白或空字串 → null");
        t.Check(RefClientTextSource.Text(table,5000)==null&&RefClientTextSource.Text(table,-1)==null,"編號超出筆數 → null");
        t.Equal("string-c.tbl",RefClientTextSource.StringFileName,"String → string-c.tbl");
        t.Equal("desc-c.tbl",RefClientTextSource.DescFileName,"Desc → desc-c.tbl");
    }

    // ---- 合成封存：String／Desc 都查得到 ----
    static void Resolve(WorldModuleTestContext t){
        var src=RefClientTextSource.FromArchive(Root,Archive(("other.html",Cp950("x")),(RefClientTextSource.StringFileName,FakeStrings),(RefClientTextSource.DescFileName,FakeDesc)));
        t.Equal("假陣營",src.Resolve("String","3233"),"String 3233");
        t.Equal("假清單",src.Resolve("String","4391;1"),"String 4391;1（旗標忽略）");
        t.Equal("假說明",src.Resolve("Desc","5"),"Desc 5 → desc-c.tbl");
        t.Equal("假尾",src.Resolve("Desc","299"),"Desc 色碼 \\F2 去掉");
        t.Check(src.Resolve("Desc","3233")==null,"Desc 不查 string-c.tbl（筆數外 → null）");
        t.Check(src.Resolve("Caption","3233")==null&&src.Resolve("string","3233")==null,"其他 kind（大小寫不同也算）→ null");
        t.Check(src.Resolve("String","x")==null,"值不是數字 → null");
        t.Equal("假陣營",src.Resolve("String","3233"),"第二次（快取）同樣結果");
        var served=src.Served;
        t.Equal(2,served.Count,"讀過兩張表");
        var s=served.Single(x=>x.FileName==RefClientTextSource.StringFileName);
        t.Equal(5000,s.Table.Count,"string-c.tbl 筆數＝第一行");
        t.Equal(1,s.Record,"記下 Text.idx 的第幾筆");
        t.Check(s.PlainSha256.Length==64&&s.PlainSha256==s.Table.Sha256,"記 SHA-256（解出內容）");
        t.Check(!s.KnownKor25,"合成表不是已知的 kor25 版本");
        t.Check(src.Unavailable==null,"封存可讀");
        Func<string,string,string?> resolver=src.Resolve;
        t.Equal("Tip. 假提示",resolver("String","4082"),"當成面板的 TextResolver 用（ResetStatUI Tip_Label 4082）");
    }

    // ---- 缺表、缺封存、沒有參考客戶端 ----
    static void Missing(WorldModuleTestContext t){
        var onlyString=RefClientTextSource.FromArchive(Root,Archive((RefClientTextSource.StringFileName,FakeStrings)));
        t.Equal("假陣營",onlyString.Resolve("String","3233"),"只有 string-c.tbl 時 String 照常");
        t.Check(onlyString.Resolve("Desc","5")==null&&onlyString.Table("Desc")==null,"沒有 desc-c.tbl → Desc 一律 null");
        var dup=RefClientTextSource.FromArchive(Root,Archive((RefClientTextSource.StringFileName,FakeStrings),(RefClientTextSource.StringFileName,FakeStrings)));
        t.Check(dup.Resolve("String","3233")==null,"string-c.tbl 重名 → 不用（null）");
        var bad=RefClientTextSource.FromArchive(Root,Archive((RefClientTextSource.StringFileName,Cp950("abc\r\nx"))));
        t.Check(bad.Resolve("String","0")==null,"第一行不是筆數 → null");
        var absent=new RefClientTextSource(Path.Combine(AppContext.BaseDirectory,"ref-client-text-absent-"+Environment.ProcessId));
        t.Check(absent.Resolve("String","3233")==null,"資料夾沒有 Text.idx → null");
        t.Check(absent.Unavailable!=null,"記下無法讀取的原因",absent.Unavailable);
        t.Check(RefClientTextSource.For(null)==null,"沒有參考客戶端 XML 來源 → 沒有字串來源");
        t.Check(RefClientTextSource.Resolver(null)==null,"沒有 UiResources → TextResolver 維持 null");
        var xml=new RefClientXmlSource(Root,"settings");
        var forXml=RefClientTextSource.For(xml);
        t.Check(forXml!=null&&forXml.Root==xml.Root&&forXml.Source=="settings","字串表跟 XML 用同一套參考客戶端");
    }

    // ---- 重置窗 Faulted → 改開 WinForms ----
    static CharResetView View(int generation,string phase)=>new(generation,1,1,7,0,phase,40,51,100,50,10,ResetStats.From([12,12,12,12,12,12]),0,null,true,"");
    static void Fallback(WorldModuleTestContext t){
        t.Equal(CharResetFallbackStep.Keep,CharResetFallback.Decide(true,true,View(3,"initial"),3),"原生窗正常 → 繼續看");
        foreach(var phase in new[]{"initial","level","elixir","finishing","saving","review"})
            t.Equal(CharResetFallbackStep.OpenWinForms,CharResetFallback.Decide(true,false,View(3,phase),3),"重置中（"+phase+"）原生窗 Faulted → 改開 WinForms");
        t.Equal(CharResetFallbackStep.Stop,CharResetFallback.Decide(true,false,View(3,"ended"),3),"重置已結束 → 不再開窗");
        t.Equal(CharResetFallbackStep.Stop,CharResetFallback.Decide(true,false,View(3,"idle"),3),"沒有重置 → 不再開窗");
        t.Equal(CharResetFallbackStep.Stop,CharResetFallback.Decide(true,false,View(4,"level"),3),"換了登入 → 不再開窗");
        t.Equal(CharResetFallbackStep.Stop,CharResetFallback.Decide(false,true,View(3,"level"),3),"玩家或窗本身已關窗 → 停止");
        t.Equal(CharResetFallbackStep.Stop,CharResetFallback.Decide(false,false,View(3,"level"),3),"關窗後才 Faulted → 不另開");
    }

    // ---- 本機有 kor25 時（CI 沒有，略過）：真表只在記憶體讀，核對 SHA-256 與筆數，不印遊戲文字 ----
    static void LocalReference(WorldModuleTestContext t){
        string root=RefClientXmlSource.DefaultRoot;
        if(!File.Exists(Path.Combine(root,"Text.idx"))||!File.Exists(Path.Combine(root,"Text.pak"))){Console.WriteLine("INFO ref-client-text: 沒有 kor25 參考客戶端，略過真表核對");return;}
        var src=new RefClientTextSource(root,"default");
        var s=src.Table("String");var d=src.Table("Desc");
        t.Check(s!=null&&d!=null,"kor25 string-c.tbl／desc-c.tbl 讀得到");
        if(s==null||d==null)return;
        t.Check(s.KnownKor25&&d.KnownKor25,"kor25 兩張表 SHA-256 與報告一致",s.PlainSha256[..12]+" / "+d.PlainSha256[..12]);
        t.Check(s.Table.Count>4500&&d.Table.Count>21391,"筆數涵蓋 XML 用到的最大編號（String 4500、Desc 21391）",$"{s.Table.Count} / {d.Table.Count}");
        t.Check(src.Resolve("String","4082") is {Length:>0}&&src.Resolve("String","4391") is {Length:>0},"ResetStatUI 4082、AgitAuctionUI 4391 有字");
        t.Check(src.Resolve("String","4118")==null,"kor25 4118 是 dummy → 不畫");
    }
}
