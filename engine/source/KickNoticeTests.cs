using System.Text;
using System.Text.Json;
namespace P06;

// T273（#651）離線測試：3.81 S227 S_Disconnect → 踢除通知（字串 64）。依據 docs/reference/send-sites-2.md §9。
// 只用合成封包與合成字串表（假文字，不是遊戲字串）；不開遊戲、網路、視窗、伺服器、DB。由 --module-tests 執行。
static class KickNoticeTests {
    static byte[] Cp950(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}
    static ServerStringTable Strings(int count,params (int id,string text)[] rows){
        var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        return ServerStringTable.Parse(Cp950(count+"\r\n"+string.Join("\r\n",lines)));
    }
    /// <summary>3.81 S_Disconnect()：C(227) H(0x01F4) D(0)，再補到 8 的倍數。</summary>
    static byte[] Disconnect()=>LoginPackets.Pad([227,0xF4,0x01,0,0,0,0]);

    [WorldModuleTest("kick-notice")]
    static void Run(WorldModuleTestContext t){
        Rules(t);Registration(t);
        using(ServerMessageText.UseTable(Strings(400,(64,"\\f3假斷線字"),(359,"不該用到"))))WithTable(t);
        using(ServerMessageText.UseTable(null))WithoutTable(t);
        using(ServerMessageText.UseTable(Strings(64)))ShortTable(t);
        using(ServerMessageText.UseTable(Strings(100,(64,"  "))))BlankEntry(t);
    }

    static void Rules(WorldModuleTestContext t){
        t.Equal((byte)227,KickNoticeRules.Opcode,"3.81 S_Disconnect opcode 227");
        t.Equal(64,KickNoticeRules.StringId,"原因不在 TW16 表內 → 字串 64");
        t.Equal("連線中斷。",KickNoticeRules.Fallback,"沒有字串表時的字");
        t.Equal((byte?)0xF4,KickNoticeRules.Reason(Disconnect()),"3.81 第一個資料 byte 是 0xF4");
        t.Equal((byte?)null,KickNoticeRules.Reason([227]),"只有 opcode → 沒有原因");
        var p=Disconnect();t.Check(p.Length%8==0&&p[0]==227&&p[1]==0xF4&&p[2]==0x01&&p[3..7].All(b=>b==0),"合成封包＝C(227) H(500) D(0)",Convert.ToHexString(p));
    }

    static void Registration(WorldModuleTestContext t){
        var s=new WorldSession();
        t.Check(s.ExtensionEntries.Any(e=>e is {Kind:"observe",Opcode:227,Subtype:-1,Method:"WorldSession.ObserveKick"}),"S227 以觀察型處理器註冊（不改 WorldSession.cs）",s.ExtensionEntries.Where(e=>e.Opcode==227).ToArray());
        t.Check(!s.ExtensionEntries.Any(e=>e is {Kind:"receive",Opcode:227}),"不註冊消化型（核心 case 227 照舊處理）");
        t.Check(s.ExtensionEntries.Any(e=>e is {Kind:"snapshot",Key:"kick"}),"快照 key kick");
        t.Check(!WorldExtensionRegistry.Default.Errors.Any(e=>e.Contains("Kick",StringComparison.Ordinal)),"沒有註冊錯誤",WorldExtensionRegistry.Default.Errors);
    }

    static void WithTable(WorldModuleTestContext t){
        t.Equal("假斷線字",KickNoticeRules.Text(),"字串表第 64 筆，去掉色碼");
        var s=new WorldSession();
        t.Equal((KickNotice?)null,s.Kick,"還沒收到 → 沒有通知");
        t.Check(Snapshot(s).ValueKind==JsonValueKind.Null,"快照 kick＝null");
        t.Throws<ProtocolException>(()=>s.Receive(Disconnect(),""),"核心仍結束連線（ProtocolException，對應原版開框前的收尾）");
        t.Equal(new KickNotice(1,0xF4,64,"假斷線字"),s.Kick,"記下通知：原因 0xF4、字串 64");
        var j=Snapshot(s);
        t.Check(j.ValueKind==JsonValueKind.Object&&j.GetProperty("revision").GetInt32()==1&&j.GetProperty("reason").GetInt32()==0xF4&&j.GetProperty("stringId").GetInt32()==64&&j.GetProperty("text").GetString()=="假斷線字","快照內容",j.ToString());
        s.Reset();
        t.Equal(1,s.Kick?.Revision??0,"Reset() 不清通知（LoginConnection 在例外後立刻 Reset，畫面之後才開框）");
        t.Throws<ProtocolException>(()=>s.Receive(Disconnect(),""),"第二次踢除");
        t.Equal(2,s.Kick?.Revision??0,"第二次 → Revision 2");
        var other=new WorldSession();
        t.Equal((KickNotice?)null,other.Kick,"另一個 session 不受影響");
        t.Throws<ProtocolException>(()=>other.Receive(LoginPackets.Pad([227]),""),"只有 opcode 的 S227");
        t.Equal(new KickNotice(1,0,64,"假斷線字"),other.Kick,"補齊的 0 也是原因表外 → 字串 64");
    }

    static void WithoutTable(WorldModuleTestContext t){
        t.Equal("連線中斷。",KickNoticeRules.Text(),"沒有字串表 → 備用字");
        var s=new WorldSession();
        t.Throws<ProtocolException>(()=>s.Receive(Disconnect(),""),"沒有字串表也結束連線");
        t.Equal("連線中斷。",s.Kick?.Text,"沒有字串表 → 通知用備用字");
    }

    static void ShortTable(WorldModuleTestContext t)=>t.Equal("連線中斷。",KickNoticeRules.Text(),"表只有 64 筆（沒有第 64 筆）→ 備用字");
    static void BlankEntry(WorldModuleTestContext t)=>t.Equal("連線中斷。",KickNoticeRules.Text(),"第 64 筆空白 → 備用字");

    static JsonElement Snapshot(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("kick");
}
