using System.Text;
using P06.NativeUi;
namespace P06;

// T437（#1008，FU17§4.1）離線測試：合成的 obscene 表（自己編的詞，不是遊戲資料）、記憶體內 Text 封存、假傳送端；不開遊戲、網路、視窗或素材。
// 涵蓋：表格式（第一行筆數、空行與超出行數跳過、全 * 的詞跳過、只去掉 \r）；替換＝字數個 *（中文一個字算一個）、重複到沒有；
// 長的詞優先（推測）；ASCII 不分大小寫（推測）；pin 不符不載入；
// 血盟備註：聊天輸入列（AnswerMemo）送出過濾後文字、60 byte 上限照原文檢查（3328）；沒有表照原文送；有客戶端時核本機表的 pin 與筆數。
static class ObsceneFilterTests {
    static Encoding Cp950{get{Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950);}}
    static (byte[] Index,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return ([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }

    sealed class FakeSource:IPledgeWindowSource {
        public readonly List<string> Memos=[];
        public readonly List<string> Notices=[];
        public bool? ShowEmblem=>null;
        public ClanEmblem? Emblem=>null;
        public bool SendNotice(string text)=>false;
        public bool SetRank(string member,byte rank)=>false;
        public bool SetShowEmblem(bool show)=>false;
        public bool UploadEmblem()=>false;
        public bool SendMemo(string text){Memos.Add(text);return true;}
        public void MemoNotice(string text)=>Notices.Add(text);
    }

    [WorldModuleTest("obscene-filter")]
    static void Run(WorldModuleTestContext t){Table(t);Filter(t);Memo(t);Client(t);}

    static void Table(WorldModuleTestContext t){
        t.Equal("obscene-c.tbl",ObsceneFilter.FileName,"TW 語系檔名");
        t.Equal("obscene-k.tbl",ObsceneFilter.FileNameFor('k'),"其他語系檔名");
        // 筆數 6，但只有 5 行：空行、全 * 的詞跳過；第 6 筆讀成空字串。
        var f=ObsceneFilter.Parse(Cp950.GetBytes("6\r\n壞蛋\r\n\r\nfoo\r\n**\r\n笨 \r\n"));
        t.Equal(6,f.Count,"header count");
        t.Check(f.Words.SequenceEqual(["壞蛋","foo","笨 "]),"空行／全 * 跳過、尾端空白保留、長的優先（foo 與「笨 」同為 3 byte，照表的順序）",string.Join("|",f.Words));
        // 筆數比行數少：多的行不讀。
        t.Check(ObsceneFilter.Parse(Cp950.GetBytes("1\r\nab\r\ncd\r\n")).Words.SequenceEqual(["ab"]),"lines past the count ignored");
        t.Throws<InvalidDataException>(()=>ObsceneFilter.Parse(Cp950.GetBytes("x\r\nab\r\n")),"first line must be a count");
        // 封存：讀得到，但 pin 不符 → 不載入。
        var (idx,pak)=Archive(("string-c.tbl",[0x30]),("OBSCENE-C.TBL",Cp950.GetBytes("1\r\nab\r\n")));
        t.Throws<InvalidDataException>(()=>ObsceneFilter.Load(TextArchive.FromBytes(idx,pak)),"pin mismatch rejected");
        var (idx2,pak2)=Archive(("string-c.tbl",[0x30]));
        t.Throws<InvalidDataException>(()=>ObsceneFilter.Load(TextArchive.FromBytes(idx2,pak2)),"missing table rejected");
    }

    static void Filter(WorldModuleTestContext t){
        var f=ObsceneFilter.FromWords("壞蛋","蛋","bad");
        t.Equal("你是**嗎",f.Filter("你是壞蛋嗎"),"中文一個字一個 *");
        t.Equal("**與**與*",f.Filter("壞蛋與壞蛋與蛋"),"重複到沒有為止");
        t.Equal("*** guy ***",f.Filter("bad guy BaD"),"ASCII 不分大小寫（推測）");
        t.Equal("乾淨的備註",f.Filter("乾淨的備註"),"沒有詞不變");
        t.Equal("",f.Filter(""),"empty");
        // 長的優先：「壞蛋」整個換掉，而不是先換「蛋」。結果長度相同，但用來比對的順序要對。
        t.Check(f.Words.SequenceEqual(["壞蛋","bad","蛋"]),"byte length desc, table order on ties",string.Join("|",f.Words));
        // 替換後拼出新的詞也會再換（從頭再找）。
        var g=ObsceneFilter.FromWords("a*","b");
        t.Equal("***",g.Filter("ab*"),"重新找：b→* 之後 a* 成立");
        // 不會跨字邊界誤配：Big5 的尾位元組剛好是 ASCII 字母時不算（以字為單位比）。
        t.Equal(0x5C,(int)Cp950.GetBytes("許")[1],"許 的尾位元組是 0x5C");
        var h=ObsceneFilter.FromWords("\\");
        t.Equal("許",h.Filter("許"),"DBCS 尾位元組 0x5C 不算 '\\'");
        // 過濾只會讓位元組變少（每個字至少 1 byte → 1 個 *）。
        string s=new('壞',30);
        t.Check(Cp950.GetByteCount(ObsceneFilter.FromWords("壞").Filter(s))<=Cp950.GetByteCount(s),"filtered never longer in bytes");
    }

    static void Memo(WorldModuleTestContext t){
        var p=new PledgePanel();var src=new FakeSource();p.Source=src;
        using(ObsceneFilter.Use(ObsceneFilter.FromWords("壞蛋"))){
            t.Equal("**",ObsceneFilter.Apply("壞蛋"),"Apply uses the current table");
            t.Check(p.AnswerMemo("守城壞蛋"),"chat-line memo sent");
            t.Equal("守城**",src.Memos[^1],"C78 16 carries the filtered text");
            // 60 byte 上限照輸入原文檢查（sub_B34270：strlen > 0x3C 才 3328），過濾後變短也不送。
            t.Check(!p.AnswerMemo(new string('甲',29)+"壞蛋")&&src.Memos.Count==1,"over 60 bytes before filtering → not sent");
            t.Check(src.Notices.SequenceEqual([PledgePanel.MemoTooLongText]),"3328 printed",src.Notices);
            t.Check(p.AnswerMemo(new string('甲',28)+"壞蛋")&&src.Memos[^1]==new string('甲',28)+"**","exactly 60 bytes → filtered and sent",src.Memos);
            t.Check(!p.AnswerMemo("")&&src.Memos.Count==2,"empty not sent");
        }
        using(ObsceneFilter.Use(null)){
            t.Equal("壞蛋",ObsceneFilter.Apply("壞蛋"),"no table → unchanged");
            t.Check(p.AnswerMemo("壞蛋")&&src.Memos[^1]=="壞蛋","no table → original text sent");
        }
    }

    static void Client(WorldModuleTestContext t){
        string root=GroundSpriteReader.ClientRoot;
        if(!File.Exists(Path.Combine(root,"Text.idx")))return;   // CI 沒有客戶端：只跑合成測試。
        ObsceneFilter? f=null;
        try{f=ObsceneFilter.Load(TextArchive.Open(root));}catch(InvalidDataException ex){t.Check(false,"client obscene-c.tbl loads",ex.Message);return;}
        t.Equal(124,f.Count,"本機 obscene-c.tbl 筆數 124");
        t.Equal(122,f.Words.Count,"123 行扣掉 1 行空白");
    }
}
