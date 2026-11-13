using P06;
using System.Text;

// T462（#1047）離線測試：技能名稱、MP、等級、「只能對自己」改讀客戶端 spelldesc-c.tbl／SpellExDesc-c.tbl 之後的行為。
// 全部用測試自編的合成表（名稱「測試甲」之類，不是遊戲資料）與記憶體內 Text 封存；不開遊戲、網路、視窗，不讀 SQL／JAR。
// 規則出處：docs/reference/asset-recipes/rule-tables.md 第 1、2 節（T446）。
partial class Checks {
    static Encoding SpellCp950=>CodePagesEncodingProvider.Instance.GetEncoding(950)!;
    /// <summary>合成的 spelldesc-c.tbl（與選用的 SpellExDesc-c.tbl）。</summary>
    static ClientSpellTable SpellTable(string names,string? detail=null)=>ClientSpellTable.Parse(SpellCp950.GetBytes(names),detail==null?null:SpellCp950.GetBytes(detail));
    static void SpellThrows(Action action,string label){
        try{action();}catch(InvalidDataException){Check(true,label);return;}
        throw new Exception(label);
    }
    static (byte[] Index,byte[] Pak) SpellArchive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return ([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }

    // 合成表：索引＝技能 id − 1。涵蓋一般行、括號前沒有空白、第三欄材料數、括號內不是數字、只有名稱、空名稱、亂行。
    const string SyntheticNames="16\r\n0 測試甲 (4/0)\r\n2 測試乙(8/0)\r\n3 測試丙 (3/0/1)\r\n7 測試丁 (HP-1/MP-1)\r\n8 測試戊\r\n9  \r\n不是一行\r\n";
    const string SyntheticDetail="16\r\n0 \\fH名稱：\\f>測試甲^\\fH對象：\\f>15格內的角色^\\fH效果：\\f>合成\r\n"
        +"2 \\fH名稱：\\f>測試乙^\\fH對象：\\f>施法者^\\fH效果：\\f>合成\r\n"
        +"3 \\fH對象：\\f>施法者\r\n"            // 技能 4 的 Wire 是 DHH：攻擊優先，不因說明變成 none
        +"7 \\fH對象：\\f>施法者、隊伍成員\r\n";   // 不是剛好「施法者」→ buff

    [CheckGroup("client-spells"),CheckGroup("combat-admission")]
    static async Task ClientSpells(){
        // 1. 解析
        var t=SpellTable(SyntheticNames,SyntheticDetail);
        Check(t.Declared==16&&t.Spells.Count==5,"spelldesc: 第一行筆數；空名稱與亂行跳過");
        Check(t.Get(1) is {Name:"測試甲",Mp:4}&&t.Get(3) is {Name:"測試乙",Mp:8}&&t.Get(4) is {Name:"測試丙",Mp:3},"spelldesc: 名稱、MP；括號前沒有空白也可；第三欄材料數不影響");
        Check(t.Get(8) is {Name:"測試丁",Mp:null}&&t.Get(9) is {Name:"測試戊",Mp:null},"spelldesc: 括號內不是數字或沒有括號 → MP 未知");
        Check(t.Get(2)==null&&t.Get(10)==null,"spelldesc: 表裡沒有的 id 不建項目");
        Check(t.CasterOnly(3)==true&&t.CasterOnly(1)==false&&t.CasterOnly(8)==false&&t.DetailError==null,"SpellExDesc: 只有剛好「對象：施法者」算只能對自己（色碼 \\fX 去掉）");
        var bare=SpellTable(SyntheticNames);
        Check(bare.CasterOnly(3)==null&&bare.DetailError!=null&&bare.Get(1)!=null,"沒有說明表：名稱仍可用，對象未知");
        SpellThrows(()=>SpellTable("x\r\n0 測試甲 (1/0)\r\n"),"第一行必須是筆數");
        SpellThrows(()=>SpellTable("2\r\n2 測試甲 (1/0)\r\n"),"索引超出宣告筆數拒絕");
        SpellThrows(()=>SpellTable("4\r\n1 測試甲 (1/0)\r\n1 測試乙 (1/0)\r\n"),"索引重複拒絕");
        SpellThrows(()=>ClientSpellTable.Parse([(byte)'2',13,10,(byte)'0',32,0xA4]),"被切斷的雙位元組字拒絕");

        // 2. 等級＝原版算式 ((id−1)>>3)+1（rule-tables.md 1.4），學費＝基價 × 等級²，第 16 級不給價
        Check(ClientSpellTable.Level(1)==1&&ClientSpellTable.Level(8)==1&&ClientSpellTable.Level(9)==2&&ClientSpellTable.Level(121)==16&&ClientSpellTable.Level(220)==28,"等級算式");
        var wide=SpellTable("220\r\n0 測試甲 (1/0)\r\n8 測試乙 (1/0)\r\n16 測試丙 (1/0)\r\n120 測試丁 (1/0)\r\n");
        var catalog=new SkillCatalog(wide);
        Check(catalog.Get(9) is {Name:"測試乙",Level:2}&&catalog.Price(1,100)==100&&catalog.Price(9,100)==400&&catalog.Price(17,100)==900,"SkillCatalog：名稱取表、學費＝基價×等級²");
        Check(catalog.Price(121,100)==null&&catalog.Price(2,100)==null&&catalog.Price(1,0)==null,"第 16 級、表裡沒有的 id、基價 0 → 沒有學費");

        // 3. MagicSpells 不讀 magic/catalog.json：名稱／MP 取表，動作看 Wire，冷卻＝原版預設，對象看說明
        using(ClientSpellTable.Use(t)){
            Check(MagicSpells.Get(1) is {Name:"測試甲",Mp:4,Action:19,Target:"buff"}&&MagicSpells.Get(1)!.ReuseMs==SpellListCooldowns.DefaultDelayMs,"技能 1：名稱、MP、無方向、buff、預設冷卻");
            Check(MagicSpells.Get(3) is {Name:"測試乙",Mp:8,Target:"none"}&&MagicRules.Describe(3).Target=="自己","技能 3：對象施法者 → none（提示「自己」）");
            Check(MagicSpells.Get(4) is {Action:18,Target:"attack",Mp:3}&&MagicRules.Describe(4).Target=="指定對象","技能 4：DHH 攻擊優先於說明");
            Check(MagicSpells.Get(8) is {Target:"buff",Mp:0},"技能 8：「施法者、隊伍成員」→ buff；MP 未知是 0（技能窗顯示空白）");
            Check(MagicSpells.Get(5)?.Target=="bookmark"&&MagicSpells.Get(58)?.Target=="location"&&MagicSpells.Get(116)?.Target=="name","特殊 Wire 不受說明影響");
            Check(MagicSpells.Get(0)==null&&MagicSpells.Get(221)==null,"支援範圍仍是 1–220");
        }
        using(ClientSpellTable.Use(bare))Check(MagicSpells.Get(3)?.Target=="buff","沒有說明表 → 不推測只能對自己");
        using(ClientSpellTable.Use(null)){
            Check(ClientSpellTable.Current==null&&ClientSpellTable.LoadError!=null,"測試可以停用客戶端表");
            Check(MagicSpells.Get(3) is {Name:"原服技能 3",Mp:0,Action:19,Target:"buff"}&&MagicSpells.Get(3)!.ReuseMs==SpellListCooldowns.DefaultDelayMs&&MagicRules.Describe(3).Supported,"沒有客戶端表：技能仍可施放，名稱明示是原服編號");
            Check(SkillCatalog.FromClient()==null,"沒有客戶端表 → 沒有技能目錄");
        }

        // 4. Text 封存：名稱表要過 pin；缺表拒絕。說明表 pin 不符只記錯誤（合成名稱表本來就過不了 pin）
        var (idx,pak)=SpellArchive(("string-c.tbl",[0x30]),("SPELLDESC-C.TBL",SpellCp950.GetBytes(SyntheticNames)),("SpellExDesc-c.tbl",SpellCp950.GetBytes(SyntheticDetail)));
        SpellThrows(()=>ClientSpellTable.Load(TextArchive.FromBytes(idx,pak)),"合成 spelldesc-c.tbl 過不了 pin");
        var (idx2,pak2)=SpellArchive(("string-c.tbl",[0x30]));
        SpellThrows(()=>ClientSpellTable.Load(TextArchive.FromBytes(idx2,pak2)),"沒有 spelldesc-c.tbl 拒絕");
        var (idx3,pak3)=SpellArchive(("spelldesc-c.tbl",[0x30]),("spelldesc-c.tbl",[0x31]));
        SpellThrows(()=>ClientSpellTable.Load(TextArchive.FromBytes(idx3,pak3)),"spelldesc-c.tbl 重複拒絕");
        Check(ClientSpellTable.NamesFileName=="spelldesc-c.tbl"&&ClientSpellTable.DetailFileName=="SpellExDesc-c.tbl","檔名");

        // 5. 學魔法清單（S41）：沒有客戶端表時明確提示不可用、全部停用；有表時名稱與學費取表
        await SkillList(null,"沒有客戶端表");
        await SkillList(wide,"有客戶端表");

        // 6. 有使用者客戶端時（CI 沒有）：核本機表的筆數與 rule-tables.md 2.2 的四筆
        string root=GroundSpriteReader.ClientRoot;
        if(File.Exists(Path.Combine(root,"Text.idx"))&&File.Exists(Path.Combine(root,"Text.pak"))){
            var real=ClientSpellTable.Load(TextArchive.Open(root));
            Check(real.Declared==220&&real.Spells.Count==183&&real.DetailError==null,"客戶端 spelldesc-c.tbl：220／183 筆，說明表可讀");
            Check(Enumerable.Range(1,220).Count(id=>real.CasterOnly(id)==true)==52,"客戶端 SpellExDesc-c.tbl：「對象：施法者」52 筆");
            Check(real.Get(1)?.Mp==4&&real.Get(3)?.Mp==8&&real.Get(4)?.Mp==3&&real.Get(8)?.Mp==10&&real.CasterOnly(3)==true&&real.CasterOnly(1)==false&&real.CasterOnly(8)==false,"四筆對上 T446 基線比對（MP、對象）");
        }
    }

    static async Task SkillList(ClientSpellTable? table,string label){
        using var scope=ClientSpellTable.Use(table);
        using var c=Seed();var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../.."));
        var scratch=Path.Combine(root,"scratch","issue-1047");Directory.CreateDirectory(scratch);
        var templates=Path.Combine(scratch,"npc.json");File.WriteAllText(templates,"{\"test\":\"<a action='buyspell'>學習</a>\"}");
        c.World.SkillCatalog=SkillCatalog.FromClient();c.World.DialogTemplates=new(templates);var sent=new List<byte[]>();
        Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
        Check(await Service(c,"talk"),label+": talk");
        Feed(c,Packet(b=>{b.Write((byte)39);b.Write(456u);Text(b,"test");b.Write((ushort)0);b.Write((ushort)0);}));
        Check(await Service(c,"option"),label+": buyspell");
        Feed(c,Packet(b=>{b.Write((byte)41);b.Write(100u);b.Write((ushort)2);b.Write(0u);b.Write(8u);}));
        var rows=c.World.ShopRows;
        if(table==null){
            Check(c.World.InteractionNotice==WorldSession.SkillCatalogUnavailableNotice&&c.World.InteractionNotice.Contains("spelldesc-c.tbl"),label+": 明確提示學習魔法不可用");
            Check(rows.Length==2&&rows.All(r=>r.Available==0&&r.Price==0)&&!c.World.ValidateService([new(0,1)]),label+": 沒有學費依據，全部停用");
        }else{
            Check(rows.Length==2&&rows[0].Name=="測試甲"&&rows[0].Price==100&&rows[1].Name=="測試乙"&&rows[1].Price==400,label+": 名稱與學費取表（等級 1、2）");
            Check(c.World.ValidateService([new(8,1)])&&!c.World.InteractionNotice.Contains("不可用"),label+": 可以學");
        }
    }
}
