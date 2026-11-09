using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R2-G（T255／#616）聊天收尾的離線測試：合成封包、記憶體內假傳送端，不開遊戲、網路、視窗或素材。由 --module-tests 執行（名稱 chat-format）。
static class ChatFormatTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Local(byte type,string text)=>P([81,type],Le32(Me),Str(text),type==2?[..Le16(32600),..Le16(32940)]:[]);
    static byte[] Global(byte type,string text,params byte[] tail)=>P([243,type],Str(text),tail);
    static PlayerChatLine Decode(byte[] packet){if(!PlayerChatProtocol.TryDecode(packet,out var line))throw new InvalidOperationException("decode");return line!;}
    static int Rgb(Color c)=>c.ToArgb()&0xFFFFFF;
    static string Runs(IEnumerable<ChatRun> runs)=>string.Join("|",runs.Select(r=>$"{r.Text}:{r.Color}"));

    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Appear(uint id,ushort x)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str("offline"),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Say(LoginConnection c,string text){var s=c.PlayerChatSnapshot();return c.SendPlayerChat(text,s.Generation,s.WorldEpoch,s.Actor).GetAwaiter().GetResult();}

    [WorldModuleTest("chat-format")]
    static void Run(WorldModuleTestContext t){
        // 1. 調色盤：96 筆，原版 RGB555 換算；各頻道預設色等於 ui-layout.md §3 表。
        t.Equal(96,ChatPalette.Count,"palette has 96 entries");
        t.Equal(0xFFFFFF,Rgb(ChatPalette.Color(ChatFormat.White)),"14 white");
        t.Equal(0xFFFF00,Rgb(ChatPalette.Color(ChatFormat.Yellow)),"13 yellow");
        t.Equal(0xFF0000,Rgb(ChatPalette.Color(ChatFormat.Red)),"3 red");
        t.Equal(0xBDBDBD,Rgb(ChatPalette.Color(ChatFormat.Grey)),"7 pledge second colour #BDBDBD");
        t.Equal(0xFFFFFF,Rgb(ChatPalette.Color(200)),"out of range falls back to white");
        var table=new Dictionary<PlayerChatChannel,int>{
            [PlayerChatChannel.Normal]=0xFFFFFF,[PlayerChatChannel.Shout]=0xFFE6CE,[PlayerChatChannel.Global]=0xADEFEF,
            [PlayerChatChannel.Whisper]=0xFFCECE,[PlayerChatChannel.WhisperTo]=0xFFF7B5,[PlayerChatChannel.Clan]=0xC5EFB5,
            [PlayerChatChannel.Party]=0xFFB500,[PlayerChatChannel.Trade]=0xBDA56B,[PlayerChatChannel.Prince]=0xFF84FF,
            [PlayerChatChannel.ChatParty]=0xBDEFE6,[PlayerChatChannel.Alliance]=0xCED6FF,[PlayerChatChannel.ClanNotice]=0x00BD00};
        foreach(var (channel,rgb) in table)t.Equal(rgb,Rgb(PlayerHudChatPanel.ChannelColor(channel)),"channel colour "+channel);

        // 2. 喊話：line.Text 保留伺服器 `<name>`，顯示改 `[name]`。
        var shout=Decode(Local(2,"<玩家> 喊話"));
        t.Equal(PlayerChatChannel.Shout,shout.Channel,"S81/2 shout");
        t.Equal("<玩家> 喊話",shout.Text,"model keeps server text");
        t.Equal("[玩家] 喊話",ChatFormat.DisplayText(shout),"shout shown as [name]");
        t.Equal("<玩家> 喊話",ChatFormat.DisplayText(new PlayerChatLine(1,0,"<玩家> 喊話",PlayerChatChannel.Normal)),"only shout is rewritten");
        t.Equal("<>x",ChatFormat.ShoutText("<>x"),"empty name untouched");
        t.Equal("abc",ChatFormat.ShoutText("abc"),"no bracket untouched");
        t.Equal("[a] b> c",ChatFormat.ShoutText("<a> b> c"),"first '> ' ends the name");

        // 3. `*` 開頭與血盟 `(`：S_SAY 0x94BA36、S_MESSAGE 0x94525D／0x9452DE。
        t.Equal(ChatFormat.Yellow,ChatFormat.TextColor(Decode(Local(0,"*玩家: 嗨"))),"normal '*' → yellow 13");
        t.Equal(ChatFormat.White,ChatFormat.TextColor(Decode(Local(0,"**玩家: 嗨"))),"normal '**' stays white");
        t.Equal(ChatFormat.White,ChatFormat.TextColor(Decode(Local(0,"玩家: *嗨"))),"'*' later stays white");
        t.Equal(ChatFormat.Red,ChatFormat.TextColor(Decode(Global(3,"[******] 公告"))),"world text[1]=='*' → red 3");
        t.Equal(44,ChatFormat.TextColor(Decode(Global(3,"[玩家] *公告"))),"world default 44");
        t.Equal(47,ChatFormat.TextColor(Decode(Global(4,"{(玩家)} 盟"))),"pledge text[1]=='(' → 47");
        t.Equal(11,ChatFormat.TextColor(Decode(Global(4,"{玩家} 盟"))),"pledge default 11");
        t.Equal(ChatFormat.Grey,ChatFormat.SecondColor(Decode(Global(4,"{玩家} 盟"))),"pledge second colour 7");
        t.Equal(ChatFormat.White,ChatFormat.SecondColor(Decode(Global(3,"[玩家] 全"))),"others second colour 14");

        // 4. type 18／25：字串後 H 色號；0 或 ≥96 不顯示（Printer 範圍 1–95）。
        var horn=Decode(Global(18,"[玩家] 號角",5,0));
        t.Equal(PlayerChatChannel.Global,horn.Channel,"18 → world tab");
        t.Equal<int?>(5,horn.ColorIndex,"18 colour index");
        t.Equal(5,ChatFormat.TextColor(horn),"18 uses own colour");
        var colour=Decode(Global(25,"[玩家] 彩色",95,0));
        t.Equal(95,ChatFormat.TextColor(colour),"25 colour 95");
        t.Check(!PlayerChatProtocol.TryDecode(Global(25,"[玩家] 彩色",96,0),out _),"25 colour 96 rejected");
        t.Check(!PlayerChatProtocol.TryDecode(Global(18,"[玩家] 號角",0,0),out _),"18 colour 0 rejected");
        t.Check(!PlayerChatProtocol.TryDecode(Global(18,"黃金號角未納入"),out _),"18 without colour (zero padding) rejected");
        t.Equal<int?>(null,Decode(Global(3,"[a] b")).ColorIndex,"other types have no colour index");
        t.Check(!PlayerChatProtocol.TryDecode(Global(19,"[a] b",1,0),out _),"19 still not chat");

        // 5. `\f`／`\b`／`\d`／`\i` 色碼。
        var coded=new PlayerChatLine(1,0,"[a] x\\fRy\\b5z\\i3w\\d2v",PlayerChatChannel.Global);
        t.Equal("[a] x:44|yz\\i3wv:34",Runs(ChatFormat.Runs(coded)),"\\fR → palette 34; \\b stripped; \\i kept; \\d stripped");
        t.Equal("a\\f!b",ChatFormat.DisplayText(new PlayerChatLine(1,0,"a\\f!b")),"\\f below '0' kept literally");
        t.Equal("a:14|b:89",Runs(ChatFormat.Runs(new PlayerChatLine(1,0,"a\\f\u0089b"))),"\\f 0x89 → palette 89 (last code char 0x8F = 95)");
        t.Equal("a\\f\u0090b",ChatFormat.DisplayText(new PlayerChatLine(1,0,"a\\f\u0090b")),"\\f ≥ 0x90 kept literally");
        t.Equal("x\\f",ChatFormat.DisplayText(new PlayerChatLine(1,0,"x\\f")),"truncated code kept");
        t.Equal("\\fRx\\bYy\\f8\\fa",ChatFormat.EncodeColorCodes("\\f0x\\b7y\\f8\\fa"),"input \\f0–\\f7／\\b0–\\b7 → digit+0x22");
        t.Equal("plain",ChatFormat.EncodeColorCodes("plain"),"no codes unchanged");
        t.Check(PlayerChatProtocol.Encode("!\\f3嗨").SequenceEqual(P([136,2],Str("\\fU嗨"))),"shout body sent with translated code");
        t.Equal(":14",Runs(ChatFormat.Runs(new PlayerChatLine(1,0,""))),"empty line one empty run");

        // 6. `$` 交易前綴：C40 type 12。
        t.Equal(new PlayerChatInput(PlayerChatChannel.Trade,"賣劍"),PlayerChatProtocol.ParseInput("$賣劍"),"$ → trade");
        t.Check(PlayerChatProtocol.Encode("$賣劍").SequenceEqual(P([40,12],Str("賣劍"))),"$ encodes C40 type 12");
        t.Check(PlayerChatProtocol.Encode("&全體").SequenceEqual(P([40,3],Str("全體"))),"& still C40 type 3");
        t.Throws<ArgumentException>(()=>PlayerChatProtocol.Encode("$"),"$ needs a body");

        // 7. C199 聊天隊伍指令。
        t.Check(ChatPartyCommands.Encode("/chatoutparty")!.SequenceEqual(P([199,1])),"/chatoutparty → C199 type 1");
        t.Check(ChatPartyCommands.Encode("/chatparty")!.SequenceEqual(P([199,2])),"/chatparty → C199 type 2");
        t.Check(ChatPartyCommands.Encode("/chatbanish 測試者")!.SequenceEqual(P([199,0],Str("測試者"))),"/chatbanish → C199 type 0 + name");
        t.Equal(8,ChatPartyCommands.Encode("/chatparty")!.Length,"C199 padded to 8");
        t.Equal(null,ChatPartyCommands.Encode("/chatpartyx"),"/chatpartyx not a command");
        t.Equal(null,ChatPartyCommands.Encode("chatparty"),"plain text not a command");
        t.Throws<ArgumentException>(()=>ChatPartyCommands.Encode("/chatbanish"),"/chatbanish needs a name");
        t.Throws<ArgumentException>(()=>ChatPartyCommands.Encode("/chatbanish 甲 乙"),"name without spaces");

        // 8. 聊天列的檢查（HUD 與原生輸入列共用）。
        t.Check(PlayerChatProtocol.Valid("/off 全體"),"/off 全體 valid");
        t.Check(PlayerChatProtocol.Describe("/off 全體").Contains("全體聊天：關"),"/off describes the toggle");
        t.Check(!PlayerChatProtocol.Valid("/on 血盟"),"/on unknown channel invalid");
        t.Check(!PlayerChatProtocol.Valid("/on"),"bare /on invalid");
        t.Check(PlayerChatProtocol.Valid("/chatbanish 甲")&&!PlayerChatProtocol.Valid("/chatbanish"),"chat-party commands validated");
        t.Check(PlayerChatProtocol.Valid("/loc"),"/loc valid");
        t.Check(PlayerChatProtocol.Describe("$嗨").StartsWith("交易",StringComparison.Ordinal),"$ shows trade");
        var line=new ChatInputLine();line.Begin(false);foreach(char ch in "/off 密語")line.Type(ch);
        t.Check(line.Valid,"native input accepts /off 密語");
        line.Clear();foreach(char ch in "/off 血盟")line.Type(ch);
        t.Check(!line.Valid,"native input rejects /off 血盟");

        // 9. 實際送出流程：/on、/off 走 C26，不送 C136；C199 走同一個送出閘門。
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            t.Check(Say(c,"/off 全體"),"/off 全體 accepted");
            t.Check(sent.Count>=1&&sent[0].SequenceEqual(ChatChannelPackets.Toggle(ChatChannelKind.World,false)),"first packet is C26 0/0",sent.Select(Convert.ToHexString).ToArray());
            t.Check(!sent.Any(b=>b[0] is 136 or 40),"no chat packet for /off");
            t.Check(!c.World.ChatChannels.World,"world channel now off");
            t.Check(c.PlayerChatSnapshot().Notice.StartsWith("全體聊天：關",StringComparison.Ordinal),"notice reports the toggle",c.PlayerChatSnapshot().Notice);
            t.Equal(0,c.World.PlayerChatLines.Length,"no local echo");
            sent.Clear();
            t.Check(!Say(c,"/on 血盟"),"unknown channel not sent");
            t.Equal(0,sent.Count,"nothing sent for bad channel");
            t.Equal(ChatChannelCommands.Help,c.PlayerChatSnapshot().Notice,"bad channel notice is the help text");
            t.Check(Say(c,"/chatparty"),"/chatparty sent");
            t.Check(sent.Count==1&&sent[0].SequenceEqual(P([199,2])),"C199 type 2 through the chat gate");
            t.Check(c.PlayerChatSnapshot().Notice.Contains("聊天隊伍"),"chat-party notice");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            var s=c.PlayerChatSnapshot();
            t.Check(!c.SendPlayerChat("/off 全體",s.Generation+1,s.WorldEpoch,s.Actor).GetAwaiter().GetResult()&&sent.Count==0,"stale session: /off not sent");
            t.Check(Say(c,"$賣劍")&&sent.Count==1&&sent[0].SequenceEqual(P([40,12],Str("賣劍"))),"$ sent as C40/12");
        }

        // 10. 原生聊天窗的列：顯示文字與顏色段，換行時顏色段跟著切。
        var view=new ChatViewState(new ChatHeights(130,240,36));
        var rows=view.Rows([new PlayerChatLine(1,0,"<甲> abc\\fRdef",PlayerChatChannel.Shout)],5,_=>1);
        t.Equal("[甲] a|bcdef",string.Join("|",rows.Select(r=>r.Text)),"rows = display text wrapped at 5");
        t.Equal("[甲] a:33",Runs(rows[0].Runs),"row 0 one shout run");
        t.Equal("bc:33|def:34",Runs(rows[1].Runs),"row 1 split by \\fR");
        t.Equal("x:5",Runs(ChatViewState.Slice([new ChatRun("x",5)],0,1)),"slice whole");
        t.Equal(":5",Runs(ChatViewState.Slice([new ChatRun("x",5)],1,0)),"empty slice keeps colour");
        t.Equal(":44",Runs(new ChatRow(1,PlayerChatChannel.Global,"").Runs),"default row run uses channel colour");
    }
}
