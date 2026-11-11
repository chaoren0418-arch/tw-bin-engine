using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
namespace P06.NativeUi;

// T440（#1011，FU17 Q9-A，docs/reference/followups-17.md §1、§7）：書籤窗區域下拉的「區域群組」。
// 不是 3.81 行為：3.81 客戶端沒有 zone3.xml、zone3desc-c.tbl（FU17§1.4、§6）。主控裁定照 TW16 做，照 #533 的先例改讀參考客戶端
// （RefClientXmlSource／RefClientTextSource 同一套：TW_REF_CLIENT → native-ui.json refClient → kor25 預設）。
// 參考客戶端的檔案只在執行時讀進記憶體、記 SHA-256（UI 日誌 "zone-bookmark"），不複製、不寫檔。讀不到 → null，書籤窗維持地圖編號。
//
// 已驗證（TW16 BIN e12949cf…，FO＝VA−0x400C00）：
//   - 表是全域 dword_16568A8，由 sub_F2FE10 讀 "zone3.xml"（FileManager 先找 "-c"，同 RefClientXmlSource.TryRead）；每個 <zone> 交給
//     sub_F385F0 建 72 byte 紀錄：+0 名稱（<name>；有 <zone3desc>N</zone3desc> 時改用 sub_A6D780(N)）、+28 <map num>、
//     +36–+51 <map left top right bottom>、+66 <bookmark id>（FU17§1.1、§1.2）。
//   - 下拉 sub_F36F20：第 0 項 878「全部」；其餘依 bookmark id 由小到大，文字＝同一 id 最後一筆紀錄的 +0（後面蓋前面，FU17§1.3）。
//   - sub_F2F9D0（VA 0xF2F9D0／FO 0xB2EDD0）＝sub_F36E50(this+16, id)：走訪全部紀錄，把 +66（word）＝id 的**每一筆**收進 vector
//     （本任務以 Capstone 反組譯 0xF36E50–0xF36F1A）。所以一個群組是同一 bookmark id 的全部區域，不只最後一筆。
//   - sub_579BD0：矩形的兩個點都是 (0,0) ＝沒有矩形。sub_500F80：left ≤ x < right 且 top ≤ y < bottom（半開區間）。
// 推測（sub_BDC920 的迴圈沒有逐行追）：
//   - 書籤符合群組＝群組裡**任一**區域符合：地圖編號相同，且（沒有矩形，或書籤座標在矩形內）。
//   - sub_A6D780(N)＝zone3desc-c.tbl 第 N 筆（FU17§1.2）。該筆是空字串時用 <name>（引擎自訂，kor25 的 19 組都有字）。
//   - 參考客戶端有 zone3.xml 但沒有 zone3desc-c.tbl、或兩者任一讀不到 → 整個不用（維持地圖編號），不拿韓文 <name> 硬顯示。

/// <summary>一個 &lt;zone&gt;（只留篩選要用的欄位）。矩形四個值都是 0＝沒有矩形（sub_579BD0）。</summary>
public sealed record ZoneBookmarkArea(ushort BookmarkId,string Name,int Map,int Left,int Top,int Right,int Bottom){
    public bool HasRect=>!(Left==0&&Top==0&&Right==0&&Bottom==0);
    /// <summary>sub_500F80：left ≤ x &lt; right 且 top ≤ y &lt; bottom。</summary>
    public bool Contains(int x,int y)=>x>=Left&&y>=Top&&x<Right&&y<Bottom;
    /// <summary>地圖相同，且沒有矩形或座標在矩形內。</summary>
    public bool Matches(P06.Bookmark b)=>b.MapId==Map&&(!HasRect||Contains(b.X,b.Y));
}

/// <summary>同一 bookmark id 的全部區域（sub_F36E50）；Name＝最後一筆的名稱（sub_F36F20）。</summary>
public sealed record ZoneBookmarkGroup(ushort Id,string Name,IReadOnlyList<ZoneBookmarkArea> Areas){
    /// <summary>推測：任一區域符合就算（見檔頭）。</summary>
    public bool Matches(P06.Bookmark b)=>Areas.Any(a=>a.Matches(b));
}

/// <summary>從參考客戶端 zone3(-c).xml＋zone3desc-c.tbl 建出的區域群組表，含識別來源的 SHA-256。</summary>
public sealed class ZoneBookmarkGroups {
    public const string XmlFileName="zone3.xml",DescFileName="zone3desc-c.tbl";
    /// <summary>kor25（D:\381懶人包kor25(原)）的 SHA-256（PakViewer 解出，2026-11-11）。只比對、記日誌；其他參考客戶端也接受。</summary>
    public const string Kor25XmlPlainSha256="7064426c18bca53b3cf3346ce080772038780250192daf3c19137a697b2a23f2",
        Kor25DescSha256="7fe1ef1cb918c20e03b01a5d2e18b6e94f6e8acd39a195a5be381340babfa42c";

    readonly Dictionary<ushort,ZoneBookmarkGroup> byId;
    /// <summary>依 bookmark id 由小到大。</summary>
    public IReadOnlyList<ZoneBookmarkGroup> Groups{get;}
    public string Root{get;}
    public string XmlName{get;}
    public string XmlPlainSha256{get;}
    public string DescSha256{get;}
    /// <summary>XML 裡的 &lt;zone&gt; 總數（含沒有 bookmark 的）。</summary>
    public int ZoneCount{get;}
    public bool KnownKor25=>XmlPlainSha256.Equals(Kor25XmlPlainSha256,StringComparison.OrdinalIgnoreCase)&&DescSha256.Equals(Kor25DescSha256,StringComparison.OrdinalIgnoreCase);

    ZoneBookmarkGroups(IReadOnlyList<ZoneBookmarkGroup> groups,int zoneCount,string root,string xmlName,string xmlSha,string descSha){
        Groups=groups;byId=groups.ToDictionary(g=>g.Id);ZoneCount=zoneCount;Root=root;XmlName=xmlName;XmlPlainSha256=xmlSha;DescSha256=descSha;
    }
    public ZoneBookmarkGroup? Find(ushort id)=>byId.GetValueOrDefault(id);

    static string Sha(ReadOnlySpan<byte> data)=>Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    static int Int(XmlElement e,string attr)=>int.TryParse(e.GetAttribute(attr),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int v)?v:0;
    static XmlElement? Child(XmlElement e,string name)=>e.ChildNodes.OfType<XmlElement>().FirstOrDefault(c=>c.Name==name);

    /// <summary>
    /// 解析 zone3 XML（已解密的位元組）與 zone3desc 表。沒有任何 &lt;bookmark id≠0&gt; 時回 null。
    /// 格式錯誤丟 XmlException／InvalidDataException（由 Load 接住、記日誌）。
    /// </summary>
    public static ZoneBookmarkGroups? Parse(byte[] xmlPlain,ServerStringTable desc,string root="",string xmlName=XmlFileName){
        var doc=new XmlDocument{XmlResolver=null};
        using(var reader=XmlReader.Create(new MemoryStream(xmlPlain,false),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,IgnoreComments=true}))doc.Load(reader);
        if(doc.DocumentElement is not {} top)throw new InvalidDataException("zone3：沒有根元素");
        var zones=top.ChildNodes.OfType<XmlElement>().Where(e=>e.Name=="zone").ToList();
        var areas=new List<ZoneBookmarkArea>();
        foreach(var z in zones){
            if(Child(z,"bookmark") is not {} bm||!ushort.TryParse(bm.GetAttribute("id"),NumberStyles.None,CultureInfo.InvariantCulture,out ushort id)||id==0)continue;
            if(Child(z,"map") is not {} map||!int.TryParse(map.GetAttribute("num"),NumberStyles.None,CultureInfo.InvariantCulture,out int num))continue;   // 沒有地圖編號＝不可能符合
            string name=Child(z,"name")?.InnerText.Trim()??"";
            if(Child(z,"zone3desc") is {} d&&int.TryParse(d.InnerText.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out int n)&&n<desc.Count){
                var text=ServerMessageText.DisplayText(desc[n]).Trim();
                if(text.Length>0)name=text;
            }
            areas.Add(new(id,name,num,Int(map,"left"),Int(map,"top"),Int(map,"right"),Int(map,"bottom")));
        }
        if(areas.Count==0)return null;
        var groups=areas.GroupBy(a=>a.BookmarkId).OrderBy(g=>g.Key)
            .Select(g=>{var list=g.ToArray();string name=list[^1].Name;return new ZoneBookmarkGroup(g.Key,name.Length>0?name:g.Key.ToString(CultureInfo.InvariantCulture),list);})
            .ToArray();
        return new(groups,zones.Count,root,xmlName,Sha(xmlPlain),desc.Sha256);
    }

    /// <summary>
    /// 從參考客戶端讀：XML 用 RefClientXmlSource（Tile.pak，"-c" 優先），表用 &lt;root&gt;\Text.idx／Text.pak（TextArchive）。
    /// 任一讀不到 → null（日誌寫原因），呼叫端維持地圖編號。openText 是測試用的接縫。
    /// </summary>
    public static ZoneBookmarkGroups? Load(RefClientXmlSource? xml,Func<TextArchive>? openText=null){
        if(xml==null)return null;
        try{
            if(xml.TryRead(XmlFileName) is not {} found){UiResourceLog.Write($"zone-bookmark 參考客戶端 {xml.Root} 沒有 {XmlFileName}，區域下拉維持地圖編號");return null;}
            var archive=(openText??(()=>TextArchive.Open(xml.Root)))();
            if(archive.Single(DescFileName) is not {} entry){UiResourceLog.Write($"zone-bookmark 參考客戶端 {xml.Root}\\Text.pak 沒有 {DescFileName}，區域下拉維持地圖編號");return null;}
            var desc=ServerStringTable.Parse(archive.Read(entry));
            var groups=Parse(found.Plain,desc,xml.Root,found.ResolvedName);
            if(groups==null){UiResourceLog.Write($"zone-bookmark {found.ResolvedName} 沒有任何 <bookmark>，區域下拉維持地圖編號");return null;}
            UiResourceLog.Write($"zone-bookmark {xml.Root}\\Tile.pak:{found.ResolvedName} plain sha256 {found.PlainSha256} (blob {found.BlobSha256}) + Text.pak:#{entry.Record} {DescFileName} sha256 {desc.Sha256}"
                +$"：{groups.Groups.Count} 組（{groups.ZoneCount} 個 zone，{xml.Source}{(groups.KnownKor25?"，與 kor25 相同":"，不是已知的 kor25 版本")}）");
            return groups;
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException or DecoderFallbackException or XmlException){
            UiResourceLog.Write($"zone-bookmark 無法讀取，區域下拉維持地圖編號：{ex.Message}");return null;
        }
    }
}

/// <summary>書籤窗用的非同步來源：第一次要時在執行緒集區讀一次，讀完前 Current＝null（窗先用地圖編號）。</summary>
public sealed class ZoneBookmarkGroupSource {
    readonly Func<ZoneBookmarkGroups?> load;
    int started;volatile bool done;ZoneBookmarkGroups? result;
    public ZoneBookmarkGroupSource(Func<ZoneBookmarkGroups?> load){this.load=load;}
    /// <summary>UiResources 的參考客戶端（Xml.Reference）；沒有參考客戶端 → null。</summary>
    public static ZoneBookmarkGroupSource? For(UiResources? resources)=>resources?.Xml.Reference is {} xml?new(()=>ZoneBookmarkGroups.Load(xml)):null;
    public void Preload(){if(Interlocked.Exchange(ref started,1)==0)ThreadPool.QueueUserWorkItem(_=>Run());}
    void Run(){
        ZoneBookmarkGroups? r=null;
        try{r=load();}
        catch(Exception ex){UiResourceLog.Write($"zone-bookmark 讀取失敗，區域下拉維持地圖編號：{ex.Message}");}   // 背景執行緒：不讓例外結束程序
        finally{result=r;done=true;}
    }
    /// <summary>讀完而且有資料才回傳；否則 null。第一次呼叫會開始背景讀取。</summary>
    public ZoneBookmarkGroups? Current{get{Preload();return done?result:null;}}
    /// <summary>測試用：同步讀完。</summary>
    internal ZoneBookmarkGroups? LoadNow(){if(Interlocked.Exchange(ref started,1)==0)Run();return done?result:null;}
}
