using System.Text;
namespace P06.NativeUi;

// T440（#1011，FU17 Q9-A）zone-bookmark-groups 離線測試：書籤窗區域下拉改用參考客戶端 zone3(-c).xml＋zone3desc-c.tbl 的群組。
// XML、zone3desc 表、Tile／Text 封存、版面與圖片全部是本檔合成的假資料（名稱、座標是測試值，不是遊戲資料）；
// 不讀真實客戶端（只有 kor25 存在時做一次唯讀核對，CI 沒有就略過）、不開遊戲、網路、視窗、伺服器、DB。
static class ZoneBookmarkGroupTests {
    static byte[] Utf8(string s)=>Encoding.UTF8.GetBytes(s);
    static byte[] Big5(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}

    // 合成 zone3：id 1 兩筆（最後一筆的名稱勝出）、id 3 有矩形、id 2 名稱用 <name>（沒有 zone3desc）、id 0／沒有 bookmark 的不算。
    const string Xml="<?xml version=\"1.0\" encoding=\"UTF-8\"?><zoneinfo><effecttreelist><tree map=\"4\"/></effecttreelist><!-- c -->"+
        "<zone><name>A-first</name><zone3desc>1</zone3desc><map num=\"10\" left=\"100\" top=\"200\" right=\"110\" bottom=\"210\"/><bookmark id=\"1\"/></zone>"+
        "<zone><name>A-last</name><zone3desc>2</zone3desc><map num=\"11\"/><bookmark id=\"1\"/></zone>"+
        "<zone><name>Plain name</name><map num=\"12\"/><bookmark id=\"2\"/></zone>"+
        "<zone><name>Rect</name><zone3desc>3</zone3desc><map num=\"4\" left=\"32000\" top=\"32500\" right=\"32100\" bottom=\"32600\"/><bookmark id=\"3\"/></zone>"+
        "<zone><name>Rect2</name><zone3desc>3</zone3desc><map num=\"4\" left=\"33000\" top=\"33000\" right=\"33010\" bottom=\"33010\"/><bookmark id=\"3\"/></zone>"+
        "<zone><name>Empty desc</name><zone3desc>4</zone3desc><map num=\"13\"/><bookmark id=\"19\"/></zone>"+
        "<zone><name>No bookmark</name><zone3desc>1</zone3desc><map num=\"10\"/></zone>"+
        "<zone><name>Zero</name><map num=\"10\"/><bookmark id=\"0\"/></zone>"+
        "<zone><name>No map</name><bookmark id=\"5\"/></zone>"+
        "</zoneinfo>";
    // 第一行筆數 5；第 4 筆是空字串；第 2 筆帶色碼 \f2（DisplayText 去掉）。
    static byte[] Desc()=>Big5("5\r\n甲零\r\n甲一\r\n\\f2甲二\r\n丙三\r\n\r\n");

    static ServerStringTable DescTable()=>ServerStringTable.Parse(Desc());

    // Tile 封存（plain28，名稱明碼）與 Text 封存（列與內容 DES 編碼）。
    static (byte[] Idx,byte[] Pak) Tile(params (string Name,byte[] Data)[] files){
        using var idx=new MemoryStream();using var pak=new MemoryStream();using var w=new BinaryWriter(idx);
        w.Write((uint)files.Length);
        foreach(var (name,data) in files){w.Write((uint)pak.Position);var raw=new byte[20];Encoding.ASCII.GetBytes(name).CopyTo(raw,0);w.Write(raw);w.Write((uint)data.Length);pak.Write(data);}
        w.Flush();return (idx.ToArray(),pak.ToArray());
    }
    static TextArchive Text(params (string Name,byte[] Data)[] files){
        using var rows=new MemoryStream();using var pak=new MemoryStream();using var w=new BinaryWriter(rows);
        foreach(var (name,data) in files){w.Write((uint)pak.Position);var raw=new byte[20];Encoding.ASCII.GetBytes(name).CopyTo(raw,0);w.Write(raw);w.Write((uint)data.Length);pak.Write(TextArchive.Encode(data));}
        w.Flush();
        var idx=new byte[4+rows.Length];BitConverter.GetBytes((uint)files.Length).CopyTo(idx,0);TextArchive.Encode(rows.ToArray()).CopyTo(idx,4);
        return TextArchive.FromBytes(idx,pak.ToArray());
    }

    [WorldModuleTest("zone-bookmark-groups")]
    static void Run(WorldModuleTestContext t){
        Parse(t);
        Load(t);
        Panel(t);
        RealKor25(t);
    }

    static void Parse(WorldModuleTestContext t){
        var g=ZoneBookmarkGroups.Parse(Utf8(Xml),DescTable(),"Z:\\ref","zone3-c.xml")!;
        t.Check(g!=null,"有 bookmark 的 zone → 群組表");
        t.Check(g!.Groups.Select(x=>x.Id).SequenceEqual(new ushort[]{1,2,3,19}),"依 bookmark id 由小到大；id 0、沒有 bookmark、沒有 map 的不算",string.Join(",",g.Groups.Select(x=>x.Id)));
        t.Equal(9,g.ZoneCount,"zone 總數（含沒有 bookmark 的）");
        t.Equal("甲二",g.Find(1)?.Name,"同一 id 名稱取最後一筆（sub_F36F20），zone3desc 第 2 筆，色碼去掉");
        t.Equal(2,g.Find(1)?.Areas.Count,"群組收同一 id 的每一筆（sub_F36E50）");
        t.Equal("Plain name",g.Find(2)?.Name,"沒有 zone3desc → <name>");
        t.Equal("丙三",g.Find(3)?.Name,"zone3desc 3");
        t.Equal("Empty desc",g.Find(19)?.Name,"zone3desc 那一筆是空字串 → <name>（推測）");
        t.Check(g.Find(4)==null&&g.Find(5)==null,"不存在的 id → null");
        t.Equal("Z:\\ref",g.Root,"記下參考客戶端根目錄");
        t.Equal("zone3-c.xml",g.XmlName,"記下實際讀到的檔名");
        t.Equal(DescTable().Sha256,g.DescSha256,"記下 zone3desc 的 SHA-256");
        t.Check(g.XmlPlainSha256.Length==64&&!g.KnownKor25,"記下 XML 的 SHA-256；合成資料不是 kor25");

        // 篩選規則
        var a1=g.Find(1)!.Areas[0];
        t.Check(a1.HasRect&&a1.Contains(100,200)&&a1.Contains(109,209),"sub_500F80：left／top 含在內");
        t.Check(!a1.Contains(110,205)&&!a1.Contains(105,210)&&!a1.Contains(99,205),"sub_500F80：right／bottom 不含（半開區間）");
        t.Check(!g.Find(1)!.Areas[1].HasRect,"四個值都是 0 ＝沒有矩形（sub_579BD0）");
        var one=g.Find(1)!;
        t.Check(one.Matches(new(1,"x",10,105,205)),"地圖 10、在矩形內 → 符合");
        t.Check(!one.Matches(new(2,"x",10,1,1)),"地圖 10、在矩形外 → 不符合");
        t.Check(one.Matches(new(3,"x",11,1,1)),"地圖 11 沒有矩形 → 整張地圖符合（任一區域符合即可，推測）");
        t.Check(!one.Matches(new(4,"x",12,105,205)),"別張地圖 → 不符合");
        var three=g.Find(3)!;
        t.Check(three.Matches(new(5,"x",4,32050,32550))&&three.Matches(new(6,"x",4,33005,33005)),"同一群組的兩個矩形都算");
        t.Check(!three.Matches(new(7,"x",4,32500,32800)),"兩個矩形都不在 → 不符合");

        // 沒有任何 bookmark → null；格式錯誤 → 例外（Load 會接住）
        t.Check(ZoneBookmarkGroups.Parse(Utf8("<zoneinfo><zone><name>n</name><map num=\"1\"/></zone></zoneinfo>"),DescTable())==null,"沒有 bookmark → null（維持地圖編號）");
        t.Throws<System.Xml.XmlException>(()=>ZoneBookmarkGroups.Parse(Utf8("<zoneinfo><zone>"),DescTable()),"XML 壞掉 → XmlException");
        t.Throws<System.Xml.XmlException>(()=>ZoneBookmarkGroups.Parse(Utf8("<!DOCTYPE z [<!ENTITY e \"x\">]><zoneinfo/>"),DescTable()),"DTD 不處理");
        // zone3desc 編號超過筆數 → <name>
        var past=ZoneBookmarkGroups.Parse(Utf8("<zoneinfo><zone><name>Past</name><zone3desc>99</zone3desc><map num=\"1\"/><bookmark id=\"7\"/></zone></zoneinfo>"),DescTable());
        t.Equal("Past",past?.Find(7)?.Name,"zone3desc 超過筆數 → <name>");
    }

    static void Load(WorldModuleTestContext t){
        t.Check(ZoneBookmarkGroups.Load(null)==null,"沒有參考客戶端 → null");
        var (idx,pak)=Tile(("zone3.xml",Utf8("<zoneinfo><zone><name>plain</name><map num=\"1\"/><bookmark id=\"9\"/></zone></zoneinfo>")),("zone3-c.xml",Utf8(Xml)));
        var xml=RefClientXmlSource.FromImages("Z:\\synthetic-ref",idx,pak);
        var text=Text(("other.tbl",Big5("1\r\nx\r\n")),(ZoneBookmarkGroups.DescFileName,Desc()));
        var g=ZoneBookmarkGroups.Load(xml,()=>text);
        t.Check(g is {XmlName:"zone3-c.xml"},"讀 zone3.xml 時先找 zone3-c.xml（FileManager 順序）",g?.XmlName);
        t.Check(g!=null&&g.Groups.Count==4&&g.Find(1)?.Name=="甲二","從合成 Tile／Text 封存讀出群組");
        t.Equal(Path.GetFullPath("Z:\\synthetic-ref"),g?.Root,"根目錄記錄");

        var noDesc=ZoneBookmarkGroups.Load(RefClientXmlSource.FromImages("Z:\\r",idx,pak),()=>Text(("other.tbl",Big5("1\r\nx\r\n"))));
        t.Check(noDesc==null,"Text.pak 沒有 zone3desc-c.tbl → null（不拿韓文 <name> 硬顯示）");
        var (idx2,pak2)=Tile(("WidgetSlot.xml",Utf8("<a/>")));
        t.Check(ZoneBookmarkGroups.Load(RefClientXmlSource.FromImages("Z:\\r",idx2,pak2),()=>text)==null,"Tile.pak 沒有 zone3 → null");
        t.Check(ZoneBookmarkGroups.Load(RefClientXmlSource.FromImages("Z:\\r",idx,pak),()=>throw new InvalidDataException("壞的 Text.idx"))==null,"Text 封存打不開 → null（不丟例外）");
        var (idx3,pak3)=Tile(("zone3-c.xml",Utf8("<zoneinfo><zone>")));
        t.Check(ZoneBookmarkGroups.Load(RefClientXmlSource.FromImages("Z:\\r",idx3,pak3),()=>text)==null,"XML 壞掉 → null（不丟例外）");
        var missing=new RefClientXmlSource(Path.Combine(AppContext.BaseDirectory,"zone-bookmark-absent-"+Environment.ProcessId));
        t.Check(ZoneBookmarkGroups.Load(missing)==null,"參考客戶端資料夾不存在 → null");

        // 背景來源：LoadNow 同步讀完；讀到 null 也算讀完；load 丟例外 → null，不結束程序。
        var src=new ZoneBookmarkGroupSource(()=>g);
        t.Check(ReferenceEquals(src.LoadNow(),g)&&ReferenceEquals(src.Current,g),"來源讀完後 Current 固定回同一份");
        var none=new ZoneBookmarkGroupSource(()=>null);
        t.Check(none.LoadNow()==null&&none.Current==null,"讀不到 → Current null");
        var boom=new ZoneBookmarkGroupSource(()=>throw new InvalidOperationException("boom"));
        t.Check(boom.LoadNow()==null,"load 丟例外 → null");
        t.Check(ZoneBookmarkGroupSource.For(null)==null,"沒有 UiResources → 沒有來源（書籤窗維持地圖編號）");
    }

    // ---- 書籤窗整合 ----
    sealed class Host:IBookmarkWindowHost {
        public BookmarkView Bookmarks{get;set;}=View(1);
        public TeleportDestinationRequest? Destination=>null;
        public bool Sending=>false;
        public void Teleport(TeleportDestinationRequest shown,uint bookmarkId){}
        public void CancelTeleport(TeleportDestinationRequest shown){}
    }
    static readonly Bookmark[] Sample=[new(101,"InRect",10,105,205),new(102,"OutRect",10,1,1),new(103,"Map11",11,7,7),new(104,"Map4",4,32050,32550),new(105,"Other",57,1,1)];
    static BookmarkView View(int revision,Bookmark[]? items=null)=>new(1,1,revision,7,true,50,"已收到伺服器書籤清單。",items??Sample);
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF808080u,w*h).ToArray()));
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [1]=Surface(1,263,420),[2]=Surface(2,251,407),[3]=Surface(3,12,12),[8]=Surface(8,200,17),[16]=Surface(16,60,18),[20]=Surface(20,60,18),
        [40]=Surface(40,105,19),[42]=Surface(42,59,114),[43]=Surface(43,55,19),
    };
    static string Layout(){
        var rows=string.Concat(Enumerable.Range(0,12).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" PushSurf=\"8;4\" Height=\"17\"/>"));
        var zoneRows=string.Concat(Enumerable.Range(0,10).Select(i=>$"<CheckButton Name=\"Item{i}_Button\" PushSurf=\"43;4\" Height=\"19\"/>"));
        return "<?xml version=\"1.0\"?><BookmarkWindowUI Toggle=\"true\"><Window Name=\"Bookmark_Window\" X=\"10\" Y=\"20\" FrameSurf=\"1;1\" Moveable=\"20\">"+
            "<Image Name=\"Base_Image\" Surf=\"2;0\" X=\"6\" Y=\"9\"/><Button Name=\"Exit_Button\" X=\"249\" Y=\"15\" BaseSurf=\"3\"/>"+
            "<ComboBox Name=\"ZoneFilter_Combobox\" X=\"129\" Y=\"141\" Width=\"105\" Height=\"134\"><Label Name=\"Text_Label\" String=\"878\" X=\"20\" Y=\"0\" Width=\"50\" Height=\"20\"/>"+
            "<Button Name=\"Expand_Button\" X=\"0\" Y=\"0\" BaseSurf=\"40\"/><UpDownList Name=\"Expand_List\" Y=\"19\" PageSize=\"7\" Width=\"55\" Height=\"114\"><Image Name=\"Background_Image\" X=\"-2\" Surf=\"42\"/>"+zoneRows+"</UpDownList></ComboBox>"+
            "<MovableList Name=\"MemoryPlace_List\" X=\"22\" Y=\"162\" Width=\"200\" Height=\"204\">"+rows+"</MovableList>"+
            "<Button Name=\"Confirm_Button\" BaseSurf=\"16\" X=\"67\" Y=\"395\"/><Button Name=\"Cancel_Button\" BaseSurf=\"20\" X=\"134\" Y=\"395\"/>"+
            "</Window></BookmarkWindowUI>";
    }
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true);
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static void Click(BookmarkWindowPanel p,Rectangle r,UiNullCommands c){var at=Center(r);p.Mouse(new(UiMouseKind.Down,at,MouseButtons.Left,1,0,Keys.None),c);p.Mouse(new(UiMouseKind.Up,at,MouseButtons.Left,1,0,Keys.None),c);}
    static string Names(BookmarkWindowPanel p)=>string.Join(",",p.Rows.Select(r=>r.Name));
    static BookmarkWindowPanel Open(Host host,Func<ZoneBookmarkGroups?>? groups){
        var map=Surfaces();var p=new BookmarkWindowPanel{ZoneGroups=groups};
        p.Load(UiLayout.Parse(Layout(),"BookmarkUI.xml"),id=>map.GetValueOrDefault(id));
        p.Attach(host);p.Update(InWorld);p.Toggle();p.Update(InWorld);
        return p;
    }

    static void Panel(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var g=ZoneBookmarkGroups.Parse(Utf8(Xml),DescTable())!;

        // 沒有群組表：維持地圖編號（既有行為）
        var plain=Open(new Host(),null);
        t.Check(plain.ZoneGroupTable==null&&plain.ZoneItems.SequenceEqual(new ushort?[]{null,4,10,11,57}),"沒有群組表 → 全部＋地圖編號",plain.ZoneItems);
        var nul=Open(new Host(),()=>null);
        t.Check(nul.ZoneGroupTable==null&&nul.ZoneItems.Count==5,"來源回 null（讀不到或還在讀）→ 地圖編號");

        // 有群組表：下拉＝全部＋bookmark id 1、2、3、19 的名稱
        var p=Open(new Host(),()=>g);
        t.Check(ReferenceEquals(p.ZoneGroupTable,g),"群組表接上");
        t.Check(p.ZoneItems.SequenceEqual(new ushort?[]{null,1,2,3,19}),"下拉＝全部＋bookmark id 由小到大",p.ZoneItems);
        t.Equal("全部",p.ZoneText,"第 0 項仍是 878");
        t.Equal("甲二",p.ZoneLabel(1),"項目文字＝群組名稱");
        t.Equal("Plain name",p.ZoneLabel(2),"<name> 群組");
        Click(p,p.ZoneExpandRect,cmd);Click(p,p.ZoneRowRect(1),cmd);
        t.Check(p.ZoneFilter==1&&p.ZoneText=="甲二","選 id 1 → 標籤顯示群組名稱",(p.ZoneFilter,p.ZoneText));
        t.Equal("InRect,Map11",Names(p),"id 1：地圖 10 的矩形內＋地圖 11 整張");
        Click(p,p.ZoneExpandRect,cmd);Click(p,p.ZoneRowRect(3),cmd);
        t.Equal("Map4",Names(p),"id 3：地圖 4 的矩形內");
        Click(p,p.ZoneExpandRect,cmd);Click(p,p.ZoneRowRect(2),cmd);
        t.Check(p.ZoneFilter==2&&p.Rows.Count==0,"id 2：沒有符合的書籤 → 清單空，篩選保留（群組表是固定的）");
        var host=(Host)p.Host!;host.Bookmarks=View(2,[..Sample.Where(b=>b.Id!=101)]);p.Update(InWorld);
        t.Check(p.ZoneFilter==2,"書籤變動不會把群組篩選歸回全部");
        Click(p,p.ZoneExpandRect,cmd);Click(p,p.ZoneRowRect(0),cmd);
        t.Check(p.ZoneFilter==null&&p.Rows.Count==4,"全部 → 全部書籤");

        // 背景讀取：先是地圖編號，讀完那一刻換成群組，原本的地圖篩選歸回全部
        ZoneBookmarkGroups? ready=null;
        var late=Open(new Host(),()=>ready);
        Click(late,late.ZoneExpandRect,cmd);Click(late,late.ZoneRowRect(2),cmd);
        t.Check(late.ZoneFilter==10&&Names(late)=="InRect,OutRect","讀完前：地圖編號篩選（地圖 10）",late.ZoneFilter);
        ready=g;late.Update(InWorld);
        t.Check(late.ZoneGroupTable==g&&late.ZoneFilter==null&&late.Rows.Count==5,"讀完 → 群組模式，篩選歸回全部");
        t.Check(late.ZoneItems.SequenceEqual(new ushort?[]{null,1,2,3,19}),"之後下拉是群組");
        ready=null;late.Update(InWorld);
        t.Check(late.ZoneGroupTable==g,"拿到後固定，不再換回地圖編號");
        t.Equal("57",BookmarkWindowPanel.ZoneItemText(57),"地圖編號模式的文字不變");
    }

    // kor25 存在時（本機）唯讀核對一次；CI 沒有參考客戶端 → 略過。
    static void RealKor25(WorldModuleTestContext t){
        string root=RefClientXmlSource.DefaultRoot;
        if(!File.Exists(Path.Combine(root,"Tile.idx"))||!File.Exists(Path.Combine(root,"Text.idx"))){Console.WriteLine("INFO zone-bookmark-groups: 沒有 kor25 參考客戶端，略過真表核對");return;}
        var g=ZoneBookmarkGroups.Load(new RefClientXmlSource(root,"default"));
        t.Check(g!=null,"kor25：讀得到群組表");
        if(g==null)return;
        t.Check(g.KnownKor25,"kor25：XML 與 zone3desc 的 SHA-256 與記錄相同",(g.XmlPlainSha256,g.DescSha256));
        t.Check(g.Groups.Select(x=>(int)x.Id).SequenceEqual(Enumerable.Range(1,19)),"kor25：bookmark id 1–19");
        t.Equal("說話之島",g.Find(1)?.Name,"kor25：id 1＝zone3desc 0");
        t.Equal("甘特之家",g.Find(14)?.Name,"kor25：id 14＝zone3desc 3");
    }
}
