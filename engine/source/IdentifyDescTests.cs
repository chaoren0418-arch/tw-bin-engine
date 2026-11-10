using System.Text;
namespace P06;

// R8-B（T357／#821）離線測試：S_IDENTIFY_CODE（3.81 S245 S_IdentifyDesc）照 FU8§2（docs/reference/followups-8.md）印在聊天欄。
//   1063–1072 → 928；134＋4139–4142 只印說明；其他先印 string-c.tbl 那一筆、再印 ItemDesc-c.tbl 的說明；訊息 0 也印；不接重量。
// 只用合成封包、合成字串表與合成說明表（假文字，不是遊戲字串）；不開遊戲、網路、視窗、伺服器、DB。
// 只有本機有原客戶端時才多核對一次 ItemDesc-c.tbl 的 pin 與筆數（CI 沒有客戶端會略過）。由 --module-tests 執行。
public sealed partial class WorldSession {
    internal void SeedIdentifyDesc(){Entered=true;MapId=0;}
}

static class IdentifyDescTests {
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    /// <summary>3.81 一般形式：H(說明編號) H(訊息) C(n) S×n；values 可以比 n 多（S_IdentifyDesc() 的重量字串）。</summary>
    static byte[] Identify(ushort desc,ushort message,byte count,params string[] values)=>Packet(b=>{b.Write((byte)245);b.Write(desc);b.Write(message);b.Write(count);foreach(var v in values)Text(b,v);});
    /// <summary>只寫說明編號的物品（其餘補 0）。</summary>
    static byte[] DescOnly(ushort desc)=>Packet(b=>{b.Write((byte)245);b.Write(desc);});
    static byte[] Cp950(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}
    static ServerStringTable Strings(int count,params (int id,string text)[] rows){
        var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        return ServerStringTable.Parse(Cp950(count+"\r\n"+string.Join("\r\n",lines)));
    }
    static ItemDescTable Items(string body)=>ItemDescTable.Parse(Cp950(body));
    static (byte[] Index,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return ([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }
    static WorldSession Session(){var s=new WorldSession();s.SeedIdentifyDesc();return s;}
    static PlayerChatLine[] Feed(WorldSession s,byte[] packet){int before=s.PlayerChatLines.Length;s.Receive(packet,"");return s.PlayerChatLines[before..];}
    static string[] Texts(PlayerChatLine[] lines)=>lines.Select(l=>l.Text).ToArray();

    // 合成表：string-c.tbl 1000 筆、ItemDesc-c.tbl 筆數 5000、desc-c.tbl 300 筆。文字都是測試用的假字。
    static ServerStringTable FakeStrings()=>Strings(1000,(0,"零號"),(134,"\\f1%0：武%1／%2"),(135,"\\f1%0：防%1"),(138,"\\f1%0 [重%1]"),(928,"\\f1%0：牙"));
    const string FakeItemBody="5000\r\n77 說明七七\r\n84 只有說明\r\n90 \r\n1063 牙一\r\n1072 牙十\r\n4139 元素一\r\n4142 元素四\r\n4999 \\f3最後\r\n5001 超出筆數\r\n";
    static ServerStringTable FakeDesc()=>Strings(300,(227,"假短劍"));

    [WorldModuleTest("identify-desc")]
    static void Run(WorldModuleTestContext t){
        Rules(t);Parse(t);Load(t);
        using(ServerMessageText.UseTable(FakeStrings()))using(ServerMessageText.UseItemDesc(Items(FakeItemBody)))using(PlayerChatProtocol.UseDesc(FakeDesc()))WithTables(t);
        using(ServerMessageText.UseTable(null))using(ServerMessageText.UseItemDesc(null))using(PlayerChatProtocol.UseDesc(null))WithoutTables(t);
        LocalClient(t);
    }

    // ---- sub_940070 的分支常數 ----
    static void Rules(WorldModuleTestContext t){
        t.Check(IdentifyDescRules.ChatColorIndex==24&&IdentifyDescRules.ChatSecondColorIndex==14,"sub_7DC850(文字, palette[24], palette[14])");
        foreach(ushort d in new ushort[]{1063,1068,1072})t.Equal((ushort)928,IdentifyDescRules.MessageFor(d,134),"說明 "+d+" → 928");
        foreach(ushort d in new ushort[]{1062,1073,77})t.Equal((ushort)134,IdentifyDescRules.MessageFor(d,134),"說明 "+d+" 不換訊息");
        t.Equal((ushort)928,IdentifyDescRules.MessageFor(1065,0),"1063–1072 連訊息 0 也換成 928");
        foreach(ushort d in new ushort[]{4139,4140,4141,4142})t.Check(IdentifyDescRules.DescriptionOnly(d,134),"134＋說明 "+d+" 只印說明");
        t.Check(!IdentifyDescRules.DescriptionOnly(4138,134)&&!IdentifyDescRules.DescriptionOnly(4143,134),"4138／4143 不是特例");
        t.Check(!IdentifyDescRules.DescriptionOnly(4139,135)&&!IdentifyDescRules.DescriptionOnly(4139,0),"只有訊息 134 才是特例");
    }

    // ---- ItemDesc-c.tbl 格式：第一行筆數，之後「編號 空白 說明」 ----
    static void Parse(WorldModuleTestContext t){
        var table=Items("6\r\n1 甲說明\r\n3乙\r\n4 \r\n2 \\f3紅字\r\n沒有編號\r\n7 超出\r\n\r\n");
        t.Equal(6,table.Count,"第一行＝筆數");
        t.Equal(5,table.Entries,"有編號的行（含超出筆數的那行）");
        t.Equal("甲說明",table[1],"編號後一個空白");
        t.Equal("乙",table[3],"編號後沒有空白也讀");
        t.Equal("紅字",table.Text(2),"色碼去掉");
        t.Equal("\\f3紅字",table[2],"原始文字保留色碼");
        t.Check(table.Text(4)==null,"空白說明不印");
        t.Check(table[0]==null&&table.Text(5)==null,"沒有那一行 → null");
        t.Check(table[7]==null,"編號 ≥ 筆數 → null（原版不印）");
        t.Check(table[-1]==null,"負編號 → null");
        t.Equal("後",Items("3\r\n1 前\r\n1 後").Text(1),"重複編號取後面那行");
        t.Throws<InvalidDataException>(()=>Items("abc\r\n1 x"),"第一行不是筆數");
        t.Throws<InvalidDataException>(()=>Items(""),"空檔");
    }
    static void Load(WorldModuleTestContext t){
        t.Equal("ItemDesc-c.tbl",ItemDescTable.FileName,"sub_87BA80(\"ItemDesc\") → ItemDesc-c.tbl");
        var invented=Cp950("2\r\n1 假");
        var (idx,pak)=Archive(("string-c.tbl",invented),("ItemDesc-c.tbl",invented));
        t.Throws<InvalidDataException>(()=>ItemDescTable.Load(TextArchive.FromBytes(idx,pak)),"合成 ItemDesc-c.tbl 過不了 pin");
        var (idx2,pak2)=Archive(("string-c.tbl",invented),("ItemDesc-e.tbl",invented));
        t.Throws<InvalidDataException>(()=>ItemDescTable.Load(TextArchive.FromBytes(idx2,pak2)),"沒有 ItemDesc-c.tbl（只有其他語系）");
    }

    // ---- 有字串表與說明表 ----
    static void WithTables(WorldModuleTestContext t){
        var s=Session();int revision=s.PlayerChatRevision;
        var weapon=Feed(s,Identify(77,134,3,"$227","3+2","5+2"));
        t.Check(Texts(weapon).SequenceEqual(["假短劍：武3+2／5+2","說明七七"]),"134：先印格式化訊息、再印說明（$227 用 desc 展開，\\f1 與 \\< 去掉）",Texts(weapon));
        t.Check(weapon.All(l=>l.ColorIndex==24&&l.Speaker==0),"兩行都是 palette[24]、系統行",weapon);
        t.Check(s.PlayerChatRevision!=revision,"聊天欄版本前進");
        t.Check(s.ActiveIdentifyDesc() is {DescId:77},"診斷紀錄仍更新");

        var tooth=Feed(s,Identify(1063,134,3,"獸牙","1+0","2+0"));
        t.Check(Texts(tooth).SequenceEqual(["獸牙：牙","牙一"]),"1063 → 928「%0：牙」＋說明",Texts(tooth));
        var tooth10=Feed(s,Identify(1072,135,2,"獸牙十","4+0"));
        t.Check(Texts(tooth10).SequenceEqual(["獸牙十：牙","牙十"]),"1072 不管原訊息都換 928",Texts(tooth10));

        var element=Feed(s,Identify(4139,134,3,"精靈武器","1+0","1+0"));
        t.Check(Texts(element).SequenceEqual(["元素一"]),"134＋4139 只印說明",Texts(element));
        var element4=Feed(s,Identify(4142,134,3,"精靈武器","1+0","1+0"));
        t.Check(Texts(element4).SequenceEqual(["元素四"]),"134＋4142 只印說明",Texts(element4));
        var elementArmor=Feed(s,Identify(4139,135,2,"精靈盔甲","3+0"));
        t.Check(Texts(elementArmor).SequenceEqual(["精靈盔甲：防3+0","元素一"]),"4139 但訊息 135：兩行都印",Texts(elementArmor));
        var elementNoDesc=Feed(s,Identify(4140,134,3,"精靈武器","1+0","1+0"));
        t.Equal(0,elementNoDesc.Length,"134＋4140 但沒有說明 → 什麼都不印");

        var kept=s.LastIdentifyDesc;
        var zero=Feed(s,DescOnly(84));
        t.Check(Texts(zero).SequenceEqual(["零號","只有說明"]),"訊息 0：照印第 0 筆、再印說明",Texts(zero));
        t.Check(ReferenceEquals(kept,s.LastIdentifyDesc),"訊息 0 不改診斷紀錄");

        var weight=Feed(s,Identify(77,134,3,"骰子匕首","1+0","1+0","3"));
        t.Check(Texts(weight).SequenceEqual(["骰子匕首：武1+0／1+0","說明七七"]),"S_IdentifyDesc() 的重量字串不接",Texts(weight));
        t.Check(!Texts(weight).Any(x=>x.Contains('重')),"沒有重量字");

        t.Check(Texts(Feed(s,Identify(91,138,2,"雜物","1"))).SequenceEqual(["雜物 [重1]"]),"說明表沒有這一筆 → 只印第一行");
        t.Check(Texts(Feed(s,Identify(90,138,2,"雜物","1"))).SequenceEqual(["雜物 [重1]"]),"說明空白 → 只印第一行");
        t.Check(Texts(Feed(s,Identify(5001,138,2,"雜物","1"))).SequenceEqual(["雜物 [重1]"]),"說明編號 ≥ 筆數 → 只印第一行");
        t.Check(Texts(Feed(s,Identify(4999,138,2,"雜物","1"))).SequenceEqual(["雜物 [重1]","最後"]),"說明的色碼去掉");
        t.Check(Texts(Feed(s,Identify(77,1500,1,"x"))).SequenceEqual(["說明七七"]),"訊息 ≥ 字串表筆數：原版查不到字，只印說明");
        t.Check(Texts(Feed(s,Identify(77,135,2,"%1","%0"))).SequenceEqual(["%1：防%0","說明七七"]),"參數照字面，不再套一次");

        int count=s.PlayerChatLines.Length;var last=s.LastIdentifyDesc;bool rejected=false;
        try{s.Receive(Identify(77,134,3,"只有一個"),"");}catch(Exception){rejected=true;}
        t.Check(rejected&&s.PlayerChatLines.Length==count&&ReferenceEquals(last,s.LastIdentifyDesc),"壞封包：不印、不改紀錄");
    }

    // ---- 沒有字串表與說明表（CI） ----
    static void WithoutTables(WorldModuleTestContext t){
        var s=Session();
        t.Check(Texts(Feed(s,Identify(77,134,3,"$227","3+2","5+2"))).SequenceEqual(["$227：小型怪物打擊3+2 大型怪物打擊5+2"]),"沒有表：伺服器範本一行，沒有說明，$ 不展開");
        t.Check(Texts(Feed(s,Identify(1063,134,3,"獸牙","1+0","2+0"))).SequenceEqual(["獸牙：寵物專用牙"]),"沒有表：928 範本");
        t.Equal(0,Feed(s,DescOnly(84)).Length,"沒有表：訊息 0 不知道字，也沒有說明 → 不印");
        t.Equal(0,Feed(s,Identify(4139,134,3,"精靈武器","1+0","1+0")).Length,"沒有表：4139 只印說明，說明讀不到 → 不印");
        t.Check(Texts(Feed(s,Identify(78,999,1,"x"))).SequenceEqual(["原服訊息 999：x"]),"沒有表：未知訊息照舊標示");
    }

    static void LocalClient(WorldModuleTestContext t){
        string root=GroundSpriteReader.ClientRoot;
        if(!File.Exists(Path.Combine(root,"Text.idx"))||!File.Exists(Path.Combine(root,"Text.pak")))return;
        var table=ItemDescTable.Load(TextArchive.Open(root));
        t.Check(table.Sha256==ItemDescTable.Sha256Pin&&table.Count==14469&&table.Entries==2048,"本機 ItemDesc-c.tbl pin、筆數 14469、2048 行",(table.Count,table.Entries));
        t.Check(new[]{1063,1072,4139,4140,4141,4142}.All(id=>table.Text(id)!=null),"本機 1063／1072／4139–4142 都有說明");
    }
}
