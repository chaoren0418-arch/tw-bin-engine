using System.Text;
namespace P06.NativeUi;

// R9-O（T375／#859，FU9§14.1–14.2）離線測試：選角職業圖鈕（sub_EEDD90／sub_EEDE30／sub_EF13F0）、Lv.%d、生日 YYYY.MM.DD
// （sub_EECB10）；建角 Attack／Defense／Mix（CharAttrType.tbl，3.81 規則把關）、Gauge_Group（StatGuage.tbl）、能力場景背景
// 7156＋2×職業＋性別（sub_EE4030）。
// 版面 XML、圖片、兩張表與 Text.idx／Text.pak 全部是記憶體內合成資料（數字是測試用的假值，不是遊戲資料）；
// 本機有 0-客戶端／kor25 時才多核對一次真表的 SHA-256 與 3.81 檢查（CI 沒有會略過，不印表內容）。
// 不開遊戲、網路、視窗、伺服器、DB，不寫檔。由 --module-tests 自動發現執行。
static class CharScreenFollowupTests {
    static byte[] Cp950(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}
    static TextArchive Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return TextArchive.FromBytes([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static bool Near(Color c,uint v)=>SelectCharPanelTests.Near(c,v);
    static bool Near(int argb,uint v)=>SelectCharPanelTests.Near(argb,v);

    // 合成 CharAttrType.tbl：第 0–1 行是註解（第 1 行故意長得像資料，原版照行號略過）；空行、重複列（取第一列）、
    // 不合 3.81 的列（0/3 STR 30）、3.81 沒有的職業 7、非數字列（atoi → 0）。
    static byte[] FakeAttr=>Cp950(string.Join("\r\n",
        "12","; 合成表（假值）","9 9 1 1 1 1 1 1",
        "0 1 21 10 10 10 11 13","0 2 13 10 18 10 11 13","","0 3 30 10 10 10 11 13","0 1 13 18 10 10 11 13",
        "1 1 16 16 14 8 9 12","7 1 20 13 16 10 7 9","x y"));
    // 合成 StatGuage.tbl：第 0–2 行略過（第 2 行像資料）；職業 0 型態 t 的第 k 個值 = 10t + k + 1；15 個值的列（第 15 個丟掉）；
    // 只有 1 個值的短列（其餘 0）。
    static string GaugeRow(int cls,int type)=>$"{cls} {type} "+string.Join(" ",Enumerable.Range(0,14).Select(k=>10*type+k+1));
    static byte[] FakeGauge=>Cp950(string.Join("\r\n",
        new[]{"12","; 合成表（假值）","; class, type, 7..20","6 0 77 77 77 77 77 77 77 77 77 77 77 77 77 77"}
        .Concat(Enumerable.Range(0,6).Select(t=>GaugeRow(0,t)))
        .Concat(["5 5 1 2 3 4 5 6 7 8 9 10 11 12 13 14 99","1 0 50"])));

    [WorldModuleTest("char-screen-followup")]
    static void Run(WorldModuleTestContext t){
        AttrTable(t);
        GaugeTable(t);
        TableLoading(t);
        Presets(t);
        Gauges(t);
        Background(t);
        SelectFormulas(t);
        SelectPainting(t);
        LocalTables(t);
    }

    // ---- CharAttrType.tbl --------------------------------------------------------------------------------------------------
    static void AttrTable(WorldModuleTestContext t){
        var a=CharAttrTypeTable.Parse(FakeAttr);
        t.Equal(7,a.Rows.Count,"rows 2…count−1, empty lines skipped (sub_EE4110 i = 2)");
        t.Check(a.Find(9,9)==null,"row 1 is never read, even when it looks like data");
        t.Equal((byte)21,a.Find(0,1)!.Value.Str,"first row of (class, type) wins (sub_EE3D30)");
        t.Check(a.Preset(0,1)!.SequenceEqual(new byte[]{21,10,10,11,13,10}),"preset in CharacterCreation order: STR DEX CON WIS CHA INT (table: str dex con int wis cha)");
        t.Check(a.Preset(0,2)!.SequenceEqual(new byte[]{13,10,18,11,13,10}),"type 2 preset");
        t.Check(a.Preset(1,1)!.SequenceEqual(new byte[]{16,16,14,9,12,8}),"knight type 1 preset");
        t.Check(a.Preset(0,3)==null&&CharAttrTypeTable.Check381(a.Find(0,3)!.Value)!=null,"STR 30 > base 13 + 8 points → fails the 3.81 rules → not applied");
        t.Check(a.Preset(7,1)==null&&CharAttrTypeTable.Check381(a.Find(7,1)!.Value)=="3.81 沒有這個職業","class 7 (warrior): no 3.81 class");
        t.Check(a.Preset(2,1)==null,"no row → no preset");
        t.Check(a.Find(0,0) is {Str:0,Cha:0},"non-numeric row → atoi 0 (class 0, type 0, all 0)");
        t.Equal(0,CharTableText.Atoi("abc"),"atoi(\"abc\") = 0");t.Equal(-12,CharTableText.Atoi("  -12x"),"atoi skips blanks, reads the sign and stops at a non-digit");
        t.Check(a.Sha256.Length==64&&!CharTableText.Is(a.Sha256,CharAttrTypeTable.LocalSha256),"SHA-256 of the decoded table; the synthetic one is not the local pin");
    }

    // ---- StatGuage.tbl -----------------------------------------------------------------------------------------------------
    static void GaugeTable(WorldModuleTestContext t){
        var g=StatGaugeTable.Parse(FakeGauge);
        t.Equal(8,g.Rows.Count,"rows 3…count−1 (sub_EE4110 j = 3)");
        t.Equal(0,g.Percent(6,0,10),"row 2 is never read");
        t.Equal(1,g.Percent(0,0,7),"stat 7 = first value");t.Equal(14,g.Percent(0,0,20),"stat 20 = 14th value");
        t.Equal(37,g.Percent(0,3,13),"class 0, type 3, stat 13 → value 7 = 37");
        t.Check(g.Percent(0,0,6)==0&&g.Percent(0,0,21)==0,"stat outside 7–20 → 0 (sub_EE3F80)");
        t.Check(g.Percent(5,5,20)==14,"a row with 15 values: the 14 first are read, the 15th (99) dropped");
        t.Check(g.Percent(1,0,7)==50&&g.Percent(1,0,8)==0,"short row: missing values are 0");
        t.Equal(0,g.Percent(2,0,10),"no row → 0");
        t.Equal(18,StatGaugeTable.Width(57,33),"width = (int)(57 × 0.33f) = 18 (truncated)");
        t.Check(StatGaugeTable.Width(100,150)==100&&StatGaugeTable.Width(100,-5)==0,"width kept inside 0…image width");
        t.Check(StatGaugeTable.Bars(5).SequenceEqual(new[]{(0,0),(1,1),(4,5),(3,2),(5,3)}),"5 bars: types 0,1,4,3,5 ← STR,DEX,INT,CON,WIS (sub_EE5430)");
        t.Check(StatGaugeTable.Bars(6).SequenceEqual(new[]{(0,0),(1,1),(2,1),(3,2),(4,5),(5,3)}),"6 bars: types 0–5 ← STR,DEX,DEX,CON,INT,WIS");
        t.Equal(0,StatGaugeTable.Bars(4).Count,"other bar counts: nothing (the binary only handles 5 and 6)");
    }

    static void TableLoading(WorldModuleTestContext t){
        var p=new CreateCharPanel();
        p.LoadTables(Archive(("CharAttrType.tbl",FakeAttr)),"main",()=>Archive(("statguage.tbl",FakeGauge)),"ref");
        t.Check(p.AttrTable!=null&&p.AttrTable.Rows.Count==7,"CharAttrType.tbl from the main client's Text archive");
        t.Check(p.GaugeTable!=null&&p.GaugeSource=="參考客戶端","StatGuage.tbl not in the main client → the reference client's (name case-insensitive, as t89)");
        var q=new CreateCharPanel();
        q.LoadTables(Archive(("CharAttrType.tbl",FakeAttr),("StatGuage.tbl",FakeGauge)),"main",()=>throw new InvalidOperationException("reference must not be opened"),"ref");
        t.Equal("本機",q.GaugeSource,"main client has StatGuage.tbl → used first, reference never opened");
        var r=new CreateCharPanel();
        r.LoadTables(Archive(("other.tbl",new byte[]{0x30})),"main",null,null);
        t.Check(r.AttrTable==null&&r.GaugeTable==null&&r.GaugeSource==null,"no tables, no reference client → both null");
        var s=new CreateCharPanel();
        s.LoadTables(Archive(("CharAttrType.tbl",Cp950("not a count\r\n"))),"main",()=>null,"ref");
        t.Check(s.AttrTable==null&&s.GaugeTable==null,"a malformed table / unreadable reference → null (logged, no exception)");
        var u=new CreateCharPanel();
        u.LoadTables(null,"main",null,null);
        t.Check(u.AttrTable==null&&u.GaugeTable==null,"main Text archive unreadable → null");
    }

    // ---- create screen ---------------------------------------------------------------------------------------------------
    const uint MsgC=0xFF202020,BackC=0xFF101010,Back2C=0xFF103050,AtkC=0xFF0000A0,AtkHiC=0xFF0000C0,AtkPushC=0xFF0000E0,DefC=0xFF00A000,DefPushC=0xFF00E000,
        MixC=0xFFA00000,MixPushC=0xFFE00000,GaugeC=0xFFFFFF00,ClassC=0xFF300000,UpC=0xFF00FFFF;
    static Dictionary<int,UiSurface> CreateSurfs(){
        var d=new Dictionary<int,UiSurface>{
            [488]=Surface(488,200,100,MsgC),[418]=Surface(418,30,15,0xFF0080FF),[415]=Surface(415,30,15,0xFF808000),[498]=Surface(498,20,20,0xFF000020),[495]=Surface(495,20,20,0xFF002000),
            [527]=Surface(527,30,15,0xFF909090),[506]=Surface(506,8,8,UpC),[504]=Surface(504,8,8,0xFFFF00FF),[509]=Surface(509,100,4,GaugeC),
            [510]=Surface(510,20,20,AtkC),[511]=Surface(511,20,20,AtkHiC),[512]=Surface(512,20,20,AtkPushC),[513]=Surface(513,20,20,DefC),[515]=Surface(515,20,20,DefPushC),
            [516]=Surface(516,20,20,MixC),[518]=Surface(518,20,20,MixPushC),
            [7156]=Surface(7156,400,300,BackC),[7159]=Surface(7159,400,300,Back2C),
        };
        for(int i=0;i<7;i++){d[128+2*i]=Surface(128+2*i,20,20,ClassC);d[129+2*i]=Surface(129+2*i,20,20,ClassC);}
        return d;
    }
    const string OneGauge="""<Control Name = "Gauge_Group"><Image Name = "Gauge1" X = "60" Y = "60" Surf = "509"/></Control>""";
    const string PrinceTypes="""<Control Name = "Class_Prince"><Button Name = "Attack_Button" X = "10" Y = "40" BaseSurf = "510" MouseEvent = "Auto_StatSettingScene"/></Control>""";
    static string Gauges(int n)=>"""<Control Name = "Gauge_Group">"""+string.Concat(Enumerable.Range(0,n).Select(k=>$"""<Image Name = "Gauge{k+1}" X = "250" Y = "{200+8*k}" Surf = "509"/>"""))+"</Control>";
    const string ThreeTypes="""
        <Control Name = "Class_Prince">
          <Button Name = "Attack_Button" X = "10" Y = "40" BaseSurf = "510" HighlightSurf = "511" PushSurf = "512" MouseEvent = "Auto_StatSettingScene"/>
          <Button Name = "Defense_Button" X = "40" Y = "40" BaseSurf = "513" HighlightSurf = "514" PushSurf = "515" MouseEvent = "Auto_StatSettingScene"/>
          <Button Name = "Mix_Button" X = "70" Y = "40" BaseSurf = "516" HighlightSurf = "517" PushSurf = "518" MouseEvent = "Auto_StatSettingScene"/>
        </Control>
""";
    static CreateCharPanel Create(int gauges=5,bool attr=true,bool gauge=true){
        var xml=CreateCharPanelTests.Xml().Replace(OneGauge,Gauges(gauges)).Replace(PrinceTypes,ThreeTypes);
        var p=new CreateCharPanel();var s=CreateSurfs();
        p.Load(UiLayout.Parse(xml,"CreateUI.xml"),id=>s.GetValueOrDefault(id));
        p.AttrTable=attr?CharAttrTypeTable.Parse(FakeAttr):null;p.GaugeTable=gauge?StatGaugeTable.Parse(FakeGauge):null;
        return p;
    }
    static readonly Point Next=new(355,285),Prev=new(10,285),Reset=new(85,225),StrUp=new(98,104),Female=new(145,155),Attack=new(12,42),Defense=new(42,42),Mix=new(72,42);
    static Point ClassAt(int i)=>new(30+30*i+2,252);
    static void Click(IUiPanel p,Point at)=>SelectCharPanelTests.Click(p,at,new UiNullCommands());
    static int Pixel(IUiPanel p,int x,int y)=>SelectCharPanelTests.Pixel(p,x,y);

    static void Presets(WorldModuleTestContext t){
        var p=Create();var host=new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4));
        t.Check(p.Available&&p.Begin(host),"layout with three type buttons usable",p.Unavailable);
        t.Check(p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(0))&&p.StatType==0,"class scene: base stats, no type");
        Click(p,Next);
        t.Check(p.Scene==CreateScene.Stat&&p.StatType==1&&p.Stats.SequenceEqual(new byte[]{21,10,10,11,13,10}),"entering the stat scene applies Attack (sub_EE6DD0 → sub_EE6760(1))",string.Join(",",p.Stats));
        t.Equal(0,p.Remaining,"preset sums to 75");
        var atk=p.StatScene!.Find("Attack_Button","Class_Prince")!;var def=p.StatScene.Find("Defense_Button","Class_Prince")!;var mix=p.StatScene.Find("Mix_Button","Class_Prince")!;
        t.Check(CreateCharPanel.TypeOf(atk)==1&&CreateCharPanel.TypeOf(def)==2&&CreateCharPanel.TypeOf(mix)==3,"Attack/Defense/Mix = type 1/2/3");
        using(var b=SelectCharPanelTests.Render(p)){
            t.Check(Near(b.GetPixel(12,42),AtkPushC),"selected type's button drawn with its PushSurf",b.GetPixel(12,42));
            t.Check(Near(b.GetPixel(42,42),DefC),"other type buttons normal");
        }
        t.Check(p.Enabled(atk)&&p.Enabled(def)&&!p.Enabled(mix),"Mix row fails the 3.81 rules → disabled; the selected Attack stays enabled (press again = reset)");
        Click(p,Defense);
        t.Check(p.StatType==2&&p.Stats.SequenceEqual(new byte[]{13,10,18,11,13,10}),"Defense → the type 2 row");
        Click(p,Mix);
        t.Equal(2,p.StatType,"disabled Mix → no change");
        p.ApplyType(3);
        t.Check(p.StatType==2&&p.Stats[2]==18,"ApplyType with an invalid row → no change (3.81 rules kept)");
        Click(p,Defense);
        t.Check(p.StatType==0&&p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(0)),"same type again → reset (sub_EE5D60)");
        Click(p,Attack);Click(p,StrUp);
        t.Check(p.StatType==1&&p.Stats[0]==21,"Up after a preset (no points left) → no change; type kept");
        Click(p,Reset);
        t.Check(p.StatType==0&&p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(0)),"Reset_Button → type 0, base stats");
        Click(p,Prev);Click(p,ClassAt(1));
        t.Check(p.Class==1&&p.StatType==0&&p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(1)),"class change → type 0, base stats");
        Click(p,Next);
        t.Check(p.StatType==1&&p.Stats.SequenceEqual(new byte[]{16,16,14,9,12,8}),"knight: Attack row applied on entering the stat scene");
        Click(p,Prev);Click(p,ClassAt(2));Click(p,Next);
        t.Check(p.StatType==0&&p.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(2)),"elf: no row → base stats stay, type 0");
        // Sending the preset: the request carries the table values (CharacterCreation order).
        var q=Create();var h=new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4));q.Begin(h);Click(q,Next);
        foreach(char ch in "王子")q.Char(ch);
        q.Key(SelectCharPanelTests.K(Keys.Enter),new UiNullCommands());
        t.Check(h.Created==new CharacterCreation("王子",0,0,21,10,10,11,13,10),"OK sends the preset stats",h.Created);
        // No table: nothing applied, buttons disabled (the old behaviour).
        var n=Create(attr:false);n.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));Click(n,Next);
        t.Check(n.StatType==0&&n.Stats.SequenceEqual(CharacterManagementPackets.BaseStats(0))&&!n.Enabled(n.StatScene!.Find("Attack_Button","Class_Prince")!),"no CharAttrType.tbl → base stats, type buttons disabled");
    }

    static void Gauges(WorldModuleTestContext t){
        var p=Create(attr:false);p.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));Click(p,Next);
        t.Check(p.Gauges.Count==5&&p.Gauges.Select(g=>g.Name).SequenceEqual(new[]{"Gauge1","Gauge2","Gauge3","Gauge4","Gauge5"}),"five bars in XML (child) order");
        // Prince base STR 13, DEX 10, CON 10, WIS 11, INT 10 → value index = stat − 7; value = 10 × type + index + 1.
        int[] W()=>p.Gauges.Select(p.GaugeWidth).ToArray();
        t.Check(W().SequenceEqual(new[]{7,14,44,34,55}),"5 bars: STR→t0, DEX→t1, INT→t4, CON→t3, WIS→t5 (× 100 px)",string.Join(",",W()));
        using(var b=SelectCharPanelTests.Render(p)){
            t.Check(Near(b.GetPixel(256,200),GaugeC)&&!Near(b.GetPixel(257,200),GaugeC),"bar 1 drawn 7 px wide (clipped)",b.GetPixel(257,200));
            t.Check(Near(b.GetPixel(250+54,232),GaugeC)&&!Near(b.GetPixel(250+55,232),GaugeC),"bar 5 drawn 55 px wide");
        }
        Click(p,StrUp);
        t.Equal(8,p.GaugeWidth(p.Gauges[0]),"STR 14 → next value");
        var six=Create(gauges:6,attr:false);six.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));Click(six,Next);
        t.Check(six.Gauges.Select(six.GaugeWidth).SequenceEqual(new[]{7,14,24,34,44,55}),"6 bars: STR,DEX,DEX,CON,INT,WIS ← types 0–5",string.Join(",",six.Gauges.Select(six.GaugeWidth)));
        var none=Create(attr:false,gauge:false);none.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));Click(none,Next);
        t.Check(none.Gauges.All(g=>none.GaugeWidth(g)==0&&!none.ShownPiece(g)),"no StatGuage.tbl → every bar 0 → not drawn");
        using(var b=SelectCharPanelTests.Render(none))t.Check(!Near(b.GetPixel(251,200),GaugeC),"…nothing painted");
        var four=Create(gauges:4,attr:false);four.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));Click(four,Next);
        t.Check(four.Gauges.All(g=>four.GaugeWidth(g)==0),"4 bars: not a 5/6 group → nothing");
        t.Check(!p.Skipped.Any(s=>s.Contains("Gauge_Group")),"Gauge_Group is built");
    }

    static void Background(WorldModuleTestContext t){
        t.Check(CreateCharPanel.BackgroundSurf(0,0)==7156&&CreateCharPanel.BackgroundSurf(1,1)==7159&&CreateCharPanel.BackgroundSurf(6,1)==7169,"7156 + 2×class + sex (sub_EE4030)");
        t.Check(CreateCharPanel.BackgroundSurf(7,0)==7265&&CreateCharPanel.BackgroundSurf(7,1)==7266,"class 7: 7265 / 7266");
        var p=Create(attr:false,gauge:false);p.Begin(new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(4)));
        Click(p,ClassAt(1));Click(p,Female);Click(p,Next);
        static bool Under(Color c,uint v){var e=Color.FromArgb(unchecked((int)v));return Math.Abs(c.R-e.R)<=3&&Math.Abs(c.G-e.G)<=4&&Math.Abs(c.B-e.B)<=7;}
        using(var b=SelectCharPanelTests.Render(p))t.Check(Under(b.GetPixel(395,5),Back2C),"knight female → 7159 under the 16-alpha board",b.GetPixel(395,5));
        Click(p,Prev);Click(p,ClassAt(2));Click(p,Next);
        using(var b=SelectCharPanelTests.Render(p))t.Check(!Under(b.GetPixel(395,5),Back2C)&&!Under(b.GetPixel(395,5),BackC),"elf female 7161 missing → not drawn (no substitute)",b.GetPixel(395,5));
        t.Check(p.SurfaceOf(p.ClassScene!,p.ClassScene!.Pieces.First(x=>x.Kind==CharUiKind.Image))==p.ClassScene.Pieces.First(x=>x.Kind==CharUiKind.Image).Surface,"class scene images unchanged");
    }

    // ---- select screen ---------------------------------------------------------------------------------------------------
    static void SelectFormulas(WorldModuleTestContext t){
        t.Check(SelectCharPanel.ClassSurf(0,0,true)==6430&&SelectCharPanel.ClassSurf(2,1,true)==6450&&SelectCharPanel.ClassSurf(6,1,true)==6482,"BigSlot: 6430 + 8×class + 4×sex (sub_EEDD90, this+360 set)");
        t.Check(SelectCharPanel.ClassSurf(0,0,false)==7049&&SelectCharPanel.ClassSurf(3,1,false)==7070&&SelectCharPanel.ClassSurf(6,1,false)==7088,"Character_List: 7049 + 6×class + 3×sex");
        t.Check(SelectCharPanel.ClassSurf(7,0,true)==8365&&SelectCharPanel.ClassSurf(7,1,false)==8368,"class 7: 8365 / 8368");
        t.Check(SelectCharPanel.ClassSurf(8,0,true)==-1&&SelectCharPanel.ClassSurf(0,2,true)==-1&&SelectCharPanel.ClassSurf(7,2,true)==-1&&SelectCharPanel.ClassSurf(-1,0,true)==-1,"unknown class / sex → -1");
        t.Check(SelectCharPanel.WaitDeleteSurf(6430,0,0)==6433&&SelectCharPanel.WaitDeleteSurf(8365,7,0)==8371&&SelectCharPanel.WaitDeleteSurf(8368,7,1)==8372,"WaitDelete: class surf + 3; class 7: 8371 / 8372 (sub_EEDE30)");
        t.Equal((6430,6431,6432),SelectCharPanel.SlotSurfs(6430,false),"BigSlot row: normal c, highlight c+1, push c+2 (sub_EF13F0)");
        t.Equal((6432,6432,6432),SelectCharPanel.SlotSurfs(6430,true),"selected row: c+2 in every state");
        t.Equal((7060,7047,7047,7047),SelectCharPanel.CharacterListSurfs(1,1,true),"Character_List selected: Class_Image c+2, Select_Button 7047 ×3 (sub_EF0060)");
        t.Equal((7058,7045,7046,7047),SelectCharPanel.CharacterListSurfs(1,1,false),"Character_List: Class_Image c, Select_Button 7045/7046/7047");
        t.Equal((-1,7047,7047,7047),SelectCharPanel.CharacterListSurfs(9,0,true),"Character_List: unknown class keeps -1 (not +2)");
        t.Equal("2026.11.10",SelectCharPanel.Birthday(20261110),"birthday YYYY.MM.DD (sub_EECB10)");
        t.Equal("1999.12.31",SelectCharPanel.Birthday(19991231),"another date");
        t.Equal("1234.56.78",SelectCharPanel.Birthday(1234567890),"more than 8 digits: only the first 8 are used");
        t.Check(SelectCharPanel.Birthday(2026)=="2026."&&SelectCharPanel.Birthday(202611)=="2026.11."&&SelectCharPanel.Birthday(2026111)=="2026.11.1","shorter values stop where the C string ends");
        t.Check(SelectCharPanel.Birthday(0)=="0"&&SelectCharPanel.Birthday(123)=="123","fewer than 4 digits: the digits only");
        t.Equal("-1",SelectCharPanel.Birthday(uint.MaxValue),"%d of the 32-bit value (signed)");
        t.Equal("Lv.52",SelectCharPanel.SlotLevel(52),"slot level \"Lv.%d\"");
    }

    const uint SelBaseC=0xFF101010,SlotBackC=0xFF404040,P0C=0xFF0000A0,P0HiC=0xFF0000C0,P0PushC=0xFF0000E0,E1C=0xFF00A000,E1HiC=0xFF00C000,E1PushC=0xFF00E000,DkWaitC=0xFFA000A0;
    static Dictionary<int,UiSurface> SelectSurfs()=>new(){
        [900]=Surface(900,400,300,SelBaseC),[488]=Surface(488,200,100,0xFF202020),[901]=Surface(901,120,40,SlotBackC),
        [6430]=Surface(6430,120,40,P0C),[6431]=Surface(6431,120,40,P0HiC),[6432]=Surface(6432,120,40,P0PushC),
        [6450]=Surface(6450,120,40,E1C),[6451]=Surface(6451,120,40,E1HiC),[6452]=Surface(6452,120,40,E1PushC),[6465]=Surface(6465,120,40,DkWaitC),
    };
    const string Slot="""
          <Control Name = "Slot_Control" Width = "120" Height = "50">
            <Image Name = "Slot_Back" Surf = "901;3">
              <Button Name = "Select_Button" BaseSurf = "6430;1" HighlightSurf = "6431;1" PushSurf = "6432;1" MouseEvent = "SelectChar_SelectWin">
                <Label Name = "Level_Label" X = "40" Y = "10" String = "dummy;1" Align = "left" Width = "40" Height = "17"/>
                <Label Name = "Name_Label" X = "80" Y = "10" String = "dummy;1" Align = "left" Width = "40" Height = "17"/>
              </Button>
              <Button Name = "WaitDelete_Button" BaseSurf = "6430;1" HighlightSurf = "6431;1" PushSurf = "6432;1" MouseEvent = "SelectChar_SelectWin">
                <Label Name = "Level_Label" X = "40" Y = "10" String = "dummy;1" Align = "left" Width = "40" Height = "17"/>
              </Button>
              <Button Name = "Create_Button" BaseSurf = "926;1" MouseEvent = "CreateChar_SelectWin"/>
            </Image>
          </Control>
""";
    static string SelectXml=>$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <SelectUI>
          <Window Name = "Message_Window" X = "100" Y = "100" BaseSurf = "488;3">
            <Button Name = "Ok_Button" X = "20" Y = "80" BaseSurf = "489" MouseEvent = "Ok_MessageWin"/>
          </Window>
          <Window Name = "Character_Select_Window" X = "0" Y = "0" BaseSurf = "900" Activate = "0">
            <List Name = "BigSlot_List" X = "10" Y = "10" Vertical = "1">
        {Slot}{Slot}{Slot}{Slot}
            </List>
            <List Name = "CharacterInfo_List" X = "250" Y = "200" Vertical = "1">
              <Label Name = "Name_Label" Align = "left" Width = "100" Height = "20"/>
              <Label Name = "Birthday_Label" Align = "left" Width = "100" Height = "20"/>
            </List>
            <Button Name = "Enter_Button" X = "250" Y = "150" BaseSurf = "904" MouseEvent = "Ok_SelectWin"/>
            <Button Name = "Back_Button" X = "10" Y = "270" BaseSurf = "915" MouseEvent = "Cancel_SelectWin"/>
          </Window>
        </SelectUI>
        """;
    static LoginCharacter Char(string name,byte cls,byte sex,byte level=10,uint birthday=20261110)=>new(name,"盟",cls,sex,0,30,10,0,level,13,10,10,11,13,10,0,birthday,0);

    static void SelectPainting(WorldModuleTestContext t){
        var p=new SelectCharPanel();var s=SelectSurfs();
        p.Load(UiLayout.Parse(SelectXml,"SelectUI.xml"),id=>s.GetValueOrDefault(id));
        t.Check(p.Available&&p.Rows==4,"synthetic BigSlot layout usable",p.Unavailable);
        var host=new SelectCharPanelTests.Host(SelectCharPanelTests.ViewOf(6,Char("甲",0,0,52),Char("乙",2,1,7),Char("丙",36,0),Char("丁",5,0)));
        p.Attach(host);var c=new UiNullCommands();
        t.Equal((6432,6432,6432),p.RowSurfs(host.View.Characters[0])!.Value,"prince male, selected → 6432 ×3");
        t.Equal((6450,6451,6452),p.RowSurfs(host.View.Characters[1])!.Value,"elf female → 6450/6451/6452");
        t.Equal((6465,6465,6465),p.RowSurfs(host.View.Characters[2])!.Value,"pending dark elf (class 36 → 4) → 6462 + 3 in every state");
        using(var b=SelectCharPanelTests.Render(p)){
            t.Check(Near(b.GetPixel(12,12),P0PushC),"row 0 (selected prince) drawn with 6432",b.GetPixel(12,12));
            t.Check(Near(b.GetPixel(12,62),E1C),"row 1 (elf female) drawn with 6450",b.GetPixel(12,62));
            t.Check(Near(b.GetPixel(12,112),DkWaitC),"row 2 (pending) drawn with 6465 on WaitDelete_Button");
            t.Check(Near(b.GetPixel(12,162),SlotBackC),"row 3 (dragon knight male, 6470 missing) → slot button not drawn, Slot_Back shows");
        }
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,new(15,65)),c);
        t.Check(Near(Pixel(p,12,62),E1HiC),"hover on an unselected row → highlight c+1");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Down,new(15,65)),c);
        t.Check(Near(Pixel(p,12,62),E1PushC),"pressed → push c+2");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Up,new(15,65)),c);
        t.Equal("乙",p.Selected,"click selects the elf");
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,new(390,290)),c);
        using(var b=SelectCharPanelTests.Render(p)){
            t.Check(Near(b.GetPixel(12,12),P0C)&&Near(b.GetPixel(12,62),E1PushC),"selection moved: prince back to 6430, elf now 6452",b.GetPixel(12,62));
        }
        p.Mouse(SelectCharPanelTests.M(UiMouseKind.Move,new(15,115)),c);
        t.Check(Near(Pixel(p,12,112),DkWaitC),"pending row ignores hover");
        string L(int slot,string parent)=>p.LabelText(p.Main!.Pieces.First(x=>x.Name=="Level_Label"&&x.Slot==slot&&x.Parent?.Name==parent))!;
        t.Check(L(0,"Select_Button")=="Lv.52"&&L(2,"WaitDelete_Button")=="Lv.10","slot level labels \"Lv.%d\" (both buttons)");
        t.Equal("2026.11.10",p.LabelText(p.Main!.Pieces.First(x=>x.ListName==SelectCharPanel.InfoList&&x.Name=="Birthday_Label")),"info birthday YYYY.MM.DD");
    }

    // ---- 本機有客戶端時（CI 沒有，略過）：真表只在記憶體讀，核對 SHA-256 與 3.81 檢查，不印表內容 ----
    static void LocalTables(WorldModuleTestContext t){
        string main=GroundSpriteReader.ClientRoot,kor=RefClientXmlSource.DefaultRoot;
        if(File.Exists(Path.Combine(main,"Text.idx"))&&File.Exists(Path.Combine(main,"Text.pak"))){
            var a=TextArchive.Open(main);var e=a.Single(CharAttrTypeTable.FileName);
            t.Check(e!=null,"0-客戶端 Text.pak has CharAttrType.tbl");
            if(e!=null){
                var table=CharAttrTypeTable.Parse(a.Read(e));
                t.Check(CharTableText.Is(table.Sha256,CharAttrTypeTable.LocalSha256),"local CharAttrType.tbl SHA-256 = report",table.Sha256[..12]);
                t.Check(table.Rows.Count==21&&table.Rows.All(r=>CharAttrTypeTable.Check381(r)==null),"all 21 local rows pass the 3.81 rules",string.Join(",",table.Rows.Where(r=>CharAttrTypeTable.Check381(r)!=null).Select(r=>$"{r.Class}/{r.Type}")));
            }
            t.Check(a.Single(StatGaugeTable.FileName)==null,"0-客戶端 has no StatGuage.tbl (FU9§15)");
        }else Console.WriteLine("INFO char-screen-followup: 沒有 0-客戶端，略過本機 CharAttrType.tbl 核對");
        if(File.Exists(Path.Combine(kor,"Text.idx"))&&File.Exists(Path.Combine(kor,"Text.pak"))){
            var a=TextArchive.Open(kor);var e=a.Single(StatGaugeTable.FileName);
            t.Check(e!=null,"kor25 Text.pak has StatGuage.tbl");
            if(e!=null){
                var g=StatGaugeTable.Parse(a.Read(e));
                t.Check(CharTableText.Is(g.Sha256,StatGaugeTable.Kor25Sha256),"kor25 StatGuage.tbl SHA-256 = report",g.Sha256[..12]);
                t.Check(Enumerable.Range(0,7).All(cls=>Enumerable.Range(0,6).All(type=>g.Rows.Any(r=>r.Class==cls&&r.Type==type))),"kor25 table has a row for every 3.81 class × gauge type");
            }
        }else Console.WriteLine("INFO char-screen-followup: 沒有 kor25 參考客戶端，略過 StatGuage.tbl 核對");
    }
}
