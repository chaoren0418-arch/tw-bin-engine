using System.Text;
using P06.NativeUi;
namespace P06;

// T431（#988，R12-D，gap-matrix 15.14.6；FU15§2.7）離線測試：信件結果字串。
// 原版 op 99 的 sub_941000（FO 0x540400，IDA 反編譯見 docs/reports/issue-988.md）照子類印字串表 string-c.tbl 的編號：
//   0x20：結果位元組 1 → 1239，否則血盟信匣（低 4 位 1）→「400 1261」（"%s %s"），否則 1240；
//   0x30：1243／1244；0x40：1241／1242；0x50：428；清單（0x00）、本文（0x10）不印。
// 顏色 sub_7DC800／sub_7DC850 第 1 色 palette[24]、第 2 色 palette[14]。
// 只用合成字串表（或強制不載入表）、合成封包與記憶體內狀態；不讀真實客戶端、不開窗、不開遊戲、不連網路。
static class MailNoticeStringTests {
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true);

    /// <summary>合成 string-c.tbl：第 i 筆是 "S{i}"；1243 帶 \f1 色碼（顯示時要去掉）。</summary>
    static ServerStringTable Table(){
        var sb=new StringBuilder("1300\n");
        for(int i=0;i<1300;i++)sb.Append(i==1243?"\\f1S1243":"S"+i).Append('\n');
        return ServerStringTable.Parse(Encoding.ASCII.GetBytes(sb.ToString()));
    }
    static byte[] Ack(byte type,uint id,byte result)=>LoginPackets.Pad([186,type,(byte)id,(byte)(id>>8),(byte)(id>>16),(byte)(id>>24),result]);
    static byte[] List(params uint[] ids){
        var b=new List<byte>{186,0,(byte)ids.Length,0};
        foreach(uint id in ids){b.AddRange(BitConverter.GetBytes(id));b.AddRange([0,26,11,11,(byte)'A',0]);b.AddRange(Encoding.Unicode.GetBytes("t"));b.AddRange([0,0]);}
        return LoginPackets.Pad(b.ToArray());
    }
    static byte[] Body(uint id)=>LoginPackets.Pad([186,0x10,(byte)id,(byte)(id>>8),(byte)(id>>16),(byte)(id>>24),(byte)'x',0,0,0,0]);

    [WorldModuleTest("mail-notice-strings")]
    static void Strings(WorldModuleTestContext t){
        // ① sub_941000 的分支（類型 → 字串編號）。
        t.Equal(new MailNoticeLine(1239),MailNoticeLine.For(0x20,1),"0x20 result 1 → 1239");
        t.Equal(new MailNoticeLine(1240),MailNoticeLine.For(0x20,0),"0x20 normal box → 1240");
        t.Equal(new MailNoticeLine(1261,400),MailNoticeLine.For(0x21,0),"0x21 clan box → 400 + 1261");
        t.Equal(new MailNoticeLine(1239),MailNoticeLine.For(0x21,1),"0x21 result 1 → 1239 (checked before the box)");
        foreach(byte box in new byte[]{0,1,2}){
            t.Equal(new MailNoticeLine(1243),MailNoticeLine.For((byte)(0x30+box),1),$"0x3{box} result 1 → 1243");
            t.Equal(new MailNoticeLine(1244),MailNoticeLine.For((byte)(0x30+box),0),$"0x3{box} result 0 → 1244");
            t.Equal((MailNoticeLine?)null,MailNoticeLine.For(box,0),$"list 0x0{box} → nothing");
            t.Equal((MailNoticeLine?)null,MailNoticeLine.For((byte)(0x10+box),0),$"body 0x1{box} → nothing");
        }
        t.Equal(new MailNoticeLine(1241),MailNoticeLine.For(0x40,1),"0x40 result 1 → 1241");
        t.Equal(new MailNoticeLine(1242),MailNoticeLine.For(0x40,0),"0x40 result 0 → 1242");
        t.Equal(new MailNoticeLine(428),MailNoticeLine.For(0x50,0),"0x50 → 428");
        t.Equal(24,ClientForm.MailChatColor,"first colour palette[24]");
        t.Equal(14,ClientForm.MailChatSecondColor,"second colour palette[14]");

        // ② 文字：字串表（去色碼）；"%s %s" 是 400 在前；超出筆數是空字串。
        using(ServerMessageText.UseTable(Table())){
            t.Equal("S1240",new MailNoticeLine(1240).Text,"table text");
            t.Equal("S1243",new MailNoticeLine(1243).Text,"colour control removed");
            t.Equal("S400 S1261",new MailNoticeLine(1261,400).Text,"%s %s: 400 first, then 1261");
            t.Equal("",MailNoticeLine.String(5000),"id past the table → empty");
        }
        // 沒有表（CI）：用 PakViewer 核對過的本機 string-c.tbl 原文。
        using(ServerMessageText.UseTable(null)){
            t.Equal("已將信件送出了。",new MailNoticeLine(1239).Text,"fallback 1239");
            t.Equal("對方的信箱已經滿了，所以無法再寄信到對方信箱。",new MailNoticeLine(1240).Text,"fallback 1240");
            t.Equal("信件已被儲存。",new MailNoticeLine(1241).Text,"fallback 1241");
            t.Equal("無法再儲存信件。",new MailNoticeLine(1242).Text,"fallback 1242");
            t.Equal("信件已被刪除了。",new MailNoticeLine(1243).Text,"fallback 1243");
            t.Equal("無法刪除信件。",new MailNoticeLine(1244).Text,"fallback 1244");
            t.Equal("血盟 信箱已滿，無法再收信件。",new MailNoticeLine(1261,400).Text,"fallback 400 + 1261");
            t.Equal("您收到鴿子信差給你的信件。",new MailNoticeLine(428).Text,"fallback 428");
        }
    }

    [WorldModuleTest("mail-notice-world")]
    static void WorldFlow(WorldModuleTestContext t){
        using var _=ServerMessageText.UseTable(Table());
        var w=new WorldSession();
        var v0=w.MailSnapshot();
        t.Check(v0.Notice==""&&v0.Line==null&&v0.NoticeSerial==0&&v0.Status.Length>0,"start: no result line, engine status only",v0);

        // 清單、本文：不印（Notice 不變、serial 不變）。
        w.ReceiveMail(List(41,42,43,44));
        var v1=w.MailSnapshot();
        t.Check(v1.Headers.Length==4&&v1.Notice==""&&v1.NoticeSerial==0&&v1.Status=="","list: rows only, no line",v1);
        w.ReceiveMail(Body(41));
        var v2=w.MailSnapshot();
        t.Check(v2.ReadingId==41&&v2.Notice==""&&v2.NoticeSerial==0,"body: no line",v2);

        // 刪除兩封：同一字串兩次，serial 各加 1。
        w.ReceiveMail(Ack(0x30,41,1));
        var v3=w.MailSnapshot();
        t.Check(v3.Line==new MailNoticeLine(1243)&&v3.Notice=="S1243"&&v3.NoticeSerial==1&&v3.Headers.Length==3&&v3.ReadingId==null,"delete 41 → 1243, row and body removed",v3);
        w.ReceiveMail(Ack(0x31,42,1));
        var v4=w.MailSnapshot();
        t.Check(v4.Notice=="S1243"&&v4.NoticeSerial==2&&v4.Headers.Length==2,"delete 42 → 1243 again, serial 2",v4);
        w.ReceiveMail(Ack(0x30,999,1));
        t.Check(w.MailSnapshot() is {Notice:"S1243",NoticeSerial:3,Headers.Length:2},"unknown id: line still printed (sub_941000 does not look at the list), rows unchanged");
        w.ReceiveMail(Ack(0x30,43,0));
        t.Check(w.MailSnapshot() is {Notice:"S1244",NoticeSerial:4,Headers.Length:2},"result 0 → 1244, row kept");
        w.ReceiveMail(Ack(0x40,43,1));
        t.Check(w.MailSnapshot() is {Notice:"S1241",Headers.Length:2},"store → 1241, row kept");
        w.ReceiveMail(Ack(0x40,43,0));
        t.Equal("S1242",w.MailSnapshot().Notice,"store result 0 → 1242");
        w.ReceiveMail(LoginPackets.Pad([186,0x20]));
        t.Equal("S1240",w.MailSnapshot().Notice,"S186 0x20 (3.81: mailbox full, no result byte) → 1240");
        w.ReceiveMail(LoginPackets.Pad([186,0x21]));
        t.Equal("S400 S1261",w.MailSnapshot().Notice,"S186 0x21 → 400 + 1261");

        // 引擎自己的訊息只進 Status，不動 Notice。
        int serial=w.MailSnapshot().NoticeSerial;
        w.ReceiveMail(LoginPackets.Pad([186,0x77]));
        var v5=w.MailSnapshot();
        t.Check(v5.Notice=="S400 S1261"&&v5.NoticeSerial==serial&&v5.Status.Contains("無法解析"),"unparsable reply → Status only",v5);
        w.ReceiveMail(Ack(0x40,43,1));
        t.Check(w.MailSnapshot() is {Status:"",Notice:"S1241"},"next result clears the engine status");

        // --classic-ui 窗內文字：有引擎訊息時顯示它，否則顯示原版字串；最近伺服器訊息照舊附在後面。
        t.Equal("S1241",MailDialog.NoticeText(w.MailSnapshot(),null,s=>s),"classic: original string");
        t.Equal("S1241\n最近伺服器訊息：m",MailDialog.NoticeText(w.MailSnapshot(),"m",s=>s),"classic: server message appended");
        w.MailStatus("已送出信件操作；等待伺服器回包。");
        t.Equal("已送出信件操作；等待伺服器回包。",MailDialog.NoticeText(w.MailSnapshot(),null,s=>s),"classic: engine status while there is one");
    }

    sealed class Host(WorldSession world):IMailPanelHost {
        public MailView? View=>world.MailSnapshot();
        public readonly List<string> Lines=[];
        public Task<bool> Run(MailView shown,string command,byte box,uint id,string receiver="",string subject="",string body="")=>Task.FromResult(true);
        public void ChatNotice(string text)=>Lines.Add(text);
    }

    [WorldModuleTest("mail-notice-relay")]
    static void Relay(WorldModuleTestContext t){
        using var _=ServerMessageText.UseTable(null);
        var w=new WorldSession();var host=new Host(w);
        var panel=new MailPanel{Host=host};
        panel.Update(InWorld);
        t.Equal(0,host.Lines.Count,"first frame: baseline");
        w.ReceiveMail(List(1,2));w.ReceiveMail(Body(1));panel.Update(InWorld);
        t.Equal(0,host.Lines.Count,"list and body replies → no chat line");
        w.MailStatus("已要求清單；空信箱可能不回包，未收到回應不代表沒有信。");panel.Update(InWorld);
        t.Equal(0,host.Lines.Count,"engine status → no chat line");
        w.ReceiveMail(Ack(0x30,1,1));panel.Update(InWorld);
        w.ReceiveMail(Ack(0x30,2,1));panel.Update(InWorld);
        t.Check(host.Lines.SequenceEqual(["信件已被刪除了。","信件已被刪除了。"]),"two deletions → 1243 twice",host.Lines);
        panel.Update(InWorld);
        t.Equal(2,host.Lines.Count,"next frame: not repeated");
        w.ReceiveMail(LoginPackets.Pad([186,0x21]));
        w.ReceiveMail(LoginPackets.Pad([186,0x20]));panel.Update(InWorld);
        t.Check(host.Lines.Skip(2).SequenceEqual(["血盟 信箱已滿，無法再收信件。","對方的信箱已經滿了，所以無法再寄信到對方信箱。"]),"two replies inside one frame → two lines, in order",host.Lines);
        for(int i=0;i<WorldSession.MailRecentLines+3;i++)w.ReceiveMail(Ack(0x40,2,0));
        panel.Update(InWorld);
        t.Equal(4+WorldSession.MailRecentLines,host.Lines.Count,"more replies than Recent keeps in one frame → the last MailRecentLines lines");

        // 請求進行中：新的回包行（serial 變了）照印；Absorb 不吞新行；serial 沒變的改動照舊吸收。
        var r=new MailNoticeRelay();
        var s=new MailStamp(1,1,1);
        MailView V(string n,int serial)=>new(s,serial,[],null,"",n,NoticeSerial:serial);
        t.Equal((string?)null,r.Next(V("",0),false),"baseline");
        t.Equal("A",r.Next(V("A",1),true),"new reply line while in flight → printed");
        t.Equal("A",r.Next(V("A",2),false),"same text, new serial → printed again");
        r.Absorb(V("A",3));
        t.Equal("A",r.Next(V("A",3),false),"Absorb does not swallow a reply line Next has not seen");
        r.Absorb(V("B",3));
        t.Equal((string?)null,r.Next(V("B",3),false),"change without a new serial is still absorbed");
        t.Equal((string?)null,r.Next(V("C",3),true),"change without a new serial while in flight → absorbed");
    }
}
