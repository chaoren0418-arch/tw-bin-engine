using System.Reflection;
using System.Text.Json;
namespace P06;

// R6-E（T335／#781，FU7§1.1、§1.4、§1.5、§6.2）聊天指令與本地確認的離線測試：合成封包、記憶體內假傳送端，
// 不開遊戲、網路、視窗，不讀客戶端素材（字串表用合成的表或明確設成「沒有表」）。由 --module-tests 執行（名稱 chat-command-followup）。
static class ChatCommandFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Item=0x12345678,Single=0x22222222;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Appear(uint id,ushort x)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str("offline"),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    // S15 新增背包物品（同 item-transfer-tests 的形狀）：D id、H desc、C use、C 0、H icon、C bless、D count、C 0、S name、C 0、11 byte。
    static byte[] Inventory(uint id,uint count,string name)=>P([15],Le32(id),Le16(1234),[0,0],Le16(12),[1],Le32(count),[0],Str(name),[0],new byte[11]);
    static byte[] TradeAsk(uint challenge,string peer)=>P([219],Le16(0),Le32(challenge),Le16(252),Str(peer));
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Say(LoginConnection c,string text){var s=c.PlayerChatSnapshot();return c.SendPlayerChat(text,s.Generation,s.WorldEpoch,s.Actor).GetAwaiter().GetResult();}
    static string Notice(LoginConnection c)=>c.PlayerChatSnapshot().Notice;
    /// <summary>C244 封包裡的角色設定（opcode 1 + 長度 4 + 資料）。</summary>
    static CharacterConfig? SavedConfig(byte[] packet){
        if(packet[0]!=244)return null;
        int length=BitConverter.ToInt32(packet,1)-3;return new CharacterConfig(packet.AsSpan(5,length));
    }
    static ServerStringTable SyntheticTable(int id,string text){
        var lines=new string[id+1];Array.Fill(lines,"");lines[id]=text;
        return ServerStringTable.Parse(LoginPackets.TextEncoding.GetBytes((id+1)+"\n"+string.Join("\n",lines)));
    }

    [WorldModuleTest("chat-command-followup")]
    static void Run(WorldModuleTestContext t){
        Settings(t);Channels(t);AcceptTrade(t);Queries(t);DeletePrompt(t);
    }

    // ① 交易詢問旗標跟 C244 角色設定一起存：原版位元 0x40，0x20＝記錄過；沒記錄過＝1。
    static void Settings(WorldModuleTestContext t){
        var all=ChatChannelSettings.Default;
        t.Equal<bool?>(null,all.AcceptTrade,"default has no recorded trade flag");
        t.Check(all.AcceptsTrade,"default accepts trade questions (byte_136E99D = 1)");
        t.Equal((byte)0x07,all.Flags,"unrecorded flag keeps old v2 byte");
        t.Equal((byte)0x67,(all with{AcceptTrade=true}).Flags,"accept = 0x20|0x40");
        t.Equal((byte)0x27,(all with{AcceptTrade=false}).Flags,"refuse = 0x20 only");
        t.Equal<ChatChannelSettings?>(all with{AcceptTrade=false},ChatChannelSettings.FromFlags(0x27),"0x27 round-trip");
        t.Equal<ChatChannelSettings?>(new(false,true,false,true),ChatChannelSettings.FromFlags(0x62),"0x62 round-trip");
        t.Check(ChatChannelSettings.FromFlags(0x07) is {AcceptTrade:null,AcceptsTrade:true},"old v2 byte without 0x20 = accept");
        t.Equal(null,ChatChannelSettings.FromFlags(0x47),"0x40 without 0x20 rejected");
        t.Equal(null,ChatChannelSettings.FromFlags(0x08),"unknown 0x08 still rejected");
        t.Equal(null,ChatChannelSettings.FromFlags(0x80),"unknown 0x80 still rejected");
        var slots=new PlayerHudBinding?[CharacterConfig.SlotCount];slots[2]=new(PlayerHudKind.Item,77);
        var saved=CharacterConfig.FromSlots(slots,all with{AcceptTrade=false});
        var back=new CharacterConfig(saved.Data);
        t.Check(back.Editable&&back.Channels is {AcceptTrade:false}&&back.Slots!.SequenceEqual(slots),"TWCC v2 keeps trade flag and slots");
        t.Equal((byte)0x27,saved.Data[^1],"TWCC v2 flags byte");
        var s=new ChatChannelSettings(true,false,true,false).With(ChatChannelKind.Whisper,true);
        t.Equal<bool?>(false,s.AcceptTrade,"channel toggle keeps trade flag");
        t.Check(ChatChannelPackets.Sync(s.With(ChatChannelKind.World,false))[0].SequenceEqual(ChatChannelPackets.Merged(new(false,true,true))),"trade flag never goes on the wire");
    }

    // ② 頻道指令＝原版語法；`/on 頻道` 已移除。
    static void Channels(WorldModuleTestContext t){
        t.Equal(new ChatChannelCommand(2,true),ChatChannelCommands.Parse("/whisper on"),"/whisper on");
        t.Equal(new ChatChannelCommand(2,false),ChatChannelCommands.Parse("/WHISPER OFF"),"stricmp on/off and command");
        t.Equal(new ChatChannelCommand(2,false),ChatChannelCommands.Parse("/悄悄話 關"),"/悄悄話 關 (369, 402)");
        t.Equal(new ChatChannelCommand(2,true),ChatChannelCommands.Parse("/悄悄話  開 "),"/悄悄話 開 with spaces");
        t.Equal(new ChatChannelCommand(6,true),ChatChannelCommands.Parse("/tradechat on"),"/tradechat → 6");
        t.Equal(new ChatChannelCommand(3,false),ChatChannelCommands.Parse("/receive off"),"/receive → 3");
        t.Equal(new ChatChannelCommand(3,true),ChatChannelCommands.Parse("/收到 開"),"/收到 → 3 (508)");
        t.Equal<ChatChannelKind?>(ChatChannelKind.Trade,ChatChannelCommands.Parse("/tradechat off")!.Channel,"6 is a 3.81 channel");
        t.Equal<ChatChannelKind?>(null,ChatChannelCommands.Parse("/receive on")!.Channel,"3 is ignored by 3.81");
        t.Check(ChatChannelCommands.IsCommand("/whisper")&&ChatChannelCommands.IsCommand("/tradechat x"),"command word alone is a command");
        t.Throws<ArgumentException>(()=>ChatChannelCommands.Parse("/whisper"),"argument required");
        t.Throws<ArgumentException>(()=>ChatChannelCommands.Parse("/tradechat 全體"),"bad argument");
        foreach(string text in new[]{"/on 全體","/off 密語","/on","/whisperx on","/tradechatter on","whisper on","/say /whisper on"})
            t.Check(!ChatChannelCommands.IsCommand(text)&&ChatChannelCommands.Parse(text)==null,"not a channel command: "+text);

        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            t.Check(Say(c,"/悄悄話 關")&&sent.Count>=1&&sent[0].SequenceEqual(ChatChannelPackets.Toggle(ChatChannelKind.Whisper,false)),"/悄悄話 關 sends C26 2/0",sent.Select(Convert.ToHexString).ToArray());
            t.Check(!c.World.ChatChannels.Whisper&&Notice(c).StartsWith("密語聊天：關",StringComparison.Ordinal),"whisper off applied",Notice(c));
            sent.Clear();
            t.Check(Say(c,"/tradechat off")&&sent.Count>=1&&sent[0].SequenceEqual(ChatChannelPackets.Toggle(ChatChannelKind.Trade,false)),"/tradechat off sends C26 6/0");
            sent.Clear();
            t.Check(Say(c,"/receive on")&&sent.Count==0&&Notice(c).Contains("未送出"),"/receive records only, nothing sent",Notice(c));
            t.Check(!Say(c,"/whisper maybe")&&sent.Count==0&&Notice(c)==ChatChannelCommands.Help,"bad argument shows help");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            t.Check(Say(c,"/on 全體")&&sent.Count==1&&sent[0][0]==136,"old /on 全體 is ordinary chat now");
            t.Check(c.World.ChatChannels.World,"old syntax changes no channel");
        }
    }

    // ① `/accepttrade`：取反、不送 C26／C121，存進 C244；旗標 0 時 S219/252 直接回拒絕。
    static void AcceptTrade(WorldModuleTestContext t){
        t.Check(TradeAskCommand.Is("/accepttrade")&&TradeAskCommand.Is("/AcceptTrade ")&&!TradeAskCommand.Is("/accepttrade on")&&!TradeAskCommand.Is("/accepttrader"),"/accepttrade only, no arguments");
        t.Check(WorldSession.IsAcceptTradeAction("accepttrade")&&WorldSession.IsAcceptTradeAction(" AcceptTrade ")&&!WorldSession.IsAcceptTradeAction("checkpk"),"HTML action name accepttrade");
        t.Check(PlayerChatProtocol.Valid("/accepttrade")&&PlayerChatProtocol.Describe("/accepttrade").Contains("交易詢問"),"chat line describes /accepttrade");
        var sent=new List<byte[]>();
        using var c=Seed(sent);
        t.Check(c.World.TradeAskWindow,"default: ask");
        t.Check(Say(c,"/accepttrade"),"/accepttrade accepted");
        t.Check(!c.World.TradeAskWindow,"flag now 0");
        t.Check(sent.All(b=>b[0] is not (26 or 121 or 136 or 40)),"no channel, answer or chat packet",sent.Select(Convert.ToHexString).ToArray());
        var config=sent.Select(SavedConfig).LastOrDefault(x=>x!=null);
        t.Check(config is {Channels.AcceptTrade:false},"C244 carries trade flag 0 (0x20 set, 0x40 clear)");
        t.Check(Notice(c).Contains("交易詢問：關"),"notice reports off",Notice(c));

        sent.Clear();
        c.World.Receive(TradeAsk(77,"peer"),"offline");
        t.Check(c.YesNoSnapshot().Prompt==null&&c.World.TradeState=="invited"&&c.World.TradeCancelRequired,"flag 0: no question, auto reject pending");
        t.Check(!c.Trade("accept",c.World.TradeRevision,c.World.WorldEpoch,c.World.TradeGeneration).GetAwaiter().GetResult()&&sent.Count==0,"flag 0: accept refused");
        c.CancelChangedTrade().GetAwaiter().GetResult(); // LoginConnection.cs:65 runs this after every applied packet.
        t.Check(sent.Count==1&&sent[0].SequenceEqual(TradePackets.Answer(77,false)),"flag 0: exactly one Answer(challenge,false)",sent.Select(Convert.ToHexString).ToArray());
        t.Check(c.World.TradeState=="cancelled"&&!c.World.TradeCancelRequired&&c.World.TradeNotice.Contains("自動拒絕"),"flag 0: trade closed with notice",c.World.TradeNotice);
        c.CancelChangedTrade().GetAwaiter().GetResult();
        t.Equal(1,sent.Count,"auto reject is not repeated");

        sent.Clear();
        t.Check(Say(c,"/accepttrade")&&c.World.TradeAskWindow,"second /accepttrade turns it back on");
        t.Check(sent.Select(SavedConfig).LastOrDefault(x=>x!=null) is {Channels.AcceptTrade:true},"C244 carries trade flag 1");
        c.World.Receive(TradeAsk(78,"peer"),"offline");
        t.Check(c.YesNoSnapshot().Prompt is {Message:252,RequestId:78}&&!c.World.TradeCancelRequired,"flag 1: shared Y/N question again");
    }

    // ③ `/checkpk` → C51；`/誰`、`/階級` 別名；`/check` 不是原版指令。
    static void Queries(WorldModuleTestContext t){
        t.Check(PlayerQueryPackets.Encode("/checkpk",Me,null)!.SequenceEqual(LoginPackets.Pad([51])),"/checkpk = field-less C51");
        t.Check(PlayerQueryPackets.Encode("/誰 甲",Me,null)!.SequenceEqual(PlayerQueryPackets.Who("甲")),"/誰 = /who (360)");
        t.Check(PlayerQueryPackets.Encode("/階級",Me,null)!.SequenceEqual(PlayerQueryPackets.Rank(Me)),"/階級 = /rank (779)");
        t.Equal(null,PlayerQueryPackets.Encode("/check",Me,456),"/check is not a command");
        t.Throws<ArgumentException>(()=>PlayerQueryPackets.Encode("/checkpk 甲",Me,null),"/checkpk takes no argument");
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            t.Check(Say(c,"/checkpk")&&sent.Count==1&&sent[0].SequenceEqual(LoginPackets.Pad([51])),"/checkpk through the chat gate");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            t.Check(Say(c,"/check")&&sent.Count==1&&sent[0][0]==136,"/check goes out as ordinary chat");
        }
    }

    // ④ 刪除物品：聊天輸入列 Y/N（3122），堆疊先問數量；3.81 C138 只能整疊。
    static void DeletePrompt(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){
            t.Equal("\"劍\" 刪除後，即無法再次復原，真的要刪除嗎？ (Y/N)",ItemDeleteText.Confirm("劍",1),"3122 fallback text");
            t.Check(ItemDeleteText.Confirm("箭",10).Contains("× 10")&&ItemDeleteText.Amount("箭",10).Contains("10"),"4114/4115 engine fallback text");
        }
        using(ServerMessageText.UseTable(SyntheticTable(ItemDeleteText.ConfirmOne,"「%s」合成3122\\f3紅 (Y/N)"))){
            t.Equal("「劍」合成3122紅 (Y/N)",ItemDeleteText.Confirm("劍",1),"3122 read from table, %s = name, colour code dropped");
            t.Check(ItemDeleteText.Confirm("箭",10).Contains("× 10"),"4114 beyond table falls back");
        }
        using var tableless=ServerMessageText.UseTable(null);
        var sent=new List<byte[]>();

        // Stack of 10: amount first; partial amounts cannot be honoured by 3.81 C138.
        using(var c=Seed(sent)){
            c.World.Receive(Inventory(Item,10,"箭"),"offline");c.World.SelectItem(Item);
            var request=c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!;
            var p=c.BeginItemDelete(request,"箭")!;
            t.Check(p.AskAmount&&Notice(c)==ItemDeleteText.Amount("箭",10),"stack asks amount on the input line",Notice(c));
            t.Check(Say(c,"3")&&sent.Count==0&&c.ItemDeletePromptCurrent==null&&Notice(c).Contains("整疊"),"partial amount refused, nothing sent",Notice(c));
            t.Check(Say(c,"hello")&&sent.Count==1&&sent[0][0]==136,"after the prompt the line is chat again");
            sent.Clear();
            c.BeginItemDelete(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!,"箭");
            t.Check(Say(c,"abc")&&sent.Count==0&&c.ItemDeletePromptCurrent==null,"non-number cancels");
            c.BeginItemDelete(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!,"箭");
            t.Check(Say(c,"11")&&sent.Count==0&&c.ItemDeletePromptCurrent==null,"more than held cancels");
            c.BeginItemDelete(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!,"箭");
            t.Check(Say(c,"10")&&sent.Count==0&&c.ItemDeletePromptCurrent is {AskAmount:false}&&Notice(c)==ItemDeleteText.Confirm("箭",10),"full amount moves to Y/N",Notice(c));
            t.Check(Say(c,"n")&&sent.Count==0&&c.ItemDeletePromptCurrent==null&&Notice(c).Contains("取消"),"N cancels without sending");
            var again=c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!;
            c.BeginItemDelete(again,"箭");Say(c,"10");
            t.Check(Say(c,"Y")&&sent.Count==1&&sent[0].SequenceEqual(ItemTransferPackets.Delete(Item)),"Y sends one C138",sent.Select(Convert.ToHexString).ToArray());
            t.Check(c.ItemDeletePromptCurrent==null&&Volatile.Read(ref again.Consumed)==1,"prompt and request consumed");
            t.Check(c.World.Inventory.Single().Count==10,"no local inventory prediction");
        }
        sent.Clear();

        // Single item: straight to 3122 Y/N.
        using(var c=Seed(sent)){
            c.World.Receive(Inventory(Single,1,"劍"),"offline");c.World.SelectItem(Single);
            var p=c.BeginItemDelete(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!,"劍")!;
            t.Check(!p.AskAmount&&Notice(c)==ItemDeleteText.Confirm("劍",1),"count 1 asks 3122 directly",Notice(c));
            t.Check(c.CancelItemDelete()&&c.ItemDeletePromptCurrent==null&&!c.CancelItemDelete(),"cancel clears the prompt once");
            t.Check(Say(c,"y")&&sent.Count==1&&sent[0][0]==136,"cancelled prompt does not eat the next line");
            sent.Clear();
            var first=c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!;c.BeginItemDelete(first,"劍");
            var second=c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!;c.BeginItemDelete(second,"劍");
            t.Check(Volatile.Read(ref first.Consumed)==1,"newer delete prompt retires the older request");
            Set(c.World,"WorldEpoch",c.World.WorldEpoch+1);
            t.Check(c.ItemDeletePromptCurrent==null,"world change expires the prompt");
            t.Check(!c.SendItemTransfer(second,true).GetAwaiter().GetResult()&&sent.Count==0,"expired prompt request cannot send");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            c.World.Receive(Inventory(Single,1,"劍"),"offline");c.World.SelectItem(Single);
            c.BeginItemDelete(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")!,"劍");
            var s=c.PlayerChatSnapshot();
            t.Check(!c.SendPlayerChat("y",s.Generation+1,s.WorldEpoch,s.Actor).GetAwaiter().GetResult()&&sent.Count==0&&c.ItemDeletePromptCurrent!=null,"stale chat stamp cannot answer");
        }
    }
}
