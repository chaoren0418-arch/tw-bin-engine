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

// R9-C（T363／#847，FU9§4、§9、§1.1）聊天指令與 S161 文字的離線測試：合成封包、記憶體內假傳送端，
// 不開遊戲、網路、視窗，不讀客戶端素材（字串表一律設成「沒有表」或合成的表）。由 --module-tests 執行（名稱 chat-command-r9c）。
static class ChatCommandR9cTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Peer=456,Vendor=789,Far=790,Npc=791;
    const ushort X=32600,Y=32940;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Pad(params byte[] b)=>LoginPackets.Pad(b);
    static byte[] Op(byte op,string name)=>LoginPackets.Pad([op,..LoginPackets.TextEncoding.GetBytes(name),0]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    /// <summary>S87 物件：動作 action（70＝擺攤）、朝向 dir、旗標 flags（4＝玩家）。</summary>
    static byte[] Appear(uint id,ushort x,ushort y,string name,byte flags=0,byte dir=0,byte action=0)=>
        P([87],Le16(x),Le16(y),Le32(id),Le16(0),[action,dir,0,0],Le32(0),Le16(0),Str(name),Str(""),[flags],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static ushort Ahead(ushort v,int[] d)=>(ushort)(v+d[0]);
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,X,Y,"me"),"offline"); // 朝向 0
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    /// <summary>聊天送出後有 1 秒防連點；測試逐條送，所以每次先歸零。</summary>
    static bool Say(LoginConnection c,string text){
        Set(c,"playerChatNextAt",0L);var s=c.PlayerChatSnapshot();
        return c.SendPlayerChat(text,s.Generation,s.WorldEpoch,s.Actor).GetAwaiter().GetResult();
    }
    static string Notice(LoginConnection c)=>c.PlayerChatSnapshot().Notice;
    static ServerStringTable SyntheticTable(int id,string text){
        var lines=new string[id+1];Array.Fill(lines,"");lines[id]=text;
        return ServerStringTable.Parse(LoginPackets.TextEncoding.GetBytes((id+1)+"\n"+string.Join("\n",lines)));
    }

    [WorldModuleTest("chat-command-r9c")]
    static void Run(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);
        using var noDesc=PlayerChatProtocol.UseDesc(null);
        Parse(t);CodeText(t);SystemText(t);Party(t);InvitePick(t);Buddies(t);Exclude(t);Trade(t);Shop(t);Bookmarks(t);StringTable(t);
    }

    // ① 指令字：英文（不分大小寫）＋字串表別名；以第一個空白切開。
    static void Parse(WorldModuleTestContext t){
        var cases=new (string Input,ChatFollowupKind Kind,string Arg)[]{
            ("/invite",ChatFollowupKind.Invite,""),("/INVITE 甲",ChatFollowupKind.Invite,"甲"),("/邀請隊員",ChatFollowupKind.Invite,""),
            ("/banish 甲",ChatFollowupKind.Banish,"甲"),("/驅逐隊員 甲",ChatFollowupKind.Banish,"甲"),("/outparty",ChatFollowupKind.OutParty,""),("/離開隊伍",ChatFollowupKind.OutParty,""),
            ("/chatinvite 乙",ChatFollowupKind.ChatInvite,"乙"),("/邀請聊天 乙",ChatFollowupKind.ChatInvite,"乙"),
            ("/buddy",ChatFollowupKind.Buddy,""),("/好友名單",ChatFollowupKind.Buddy,""),("/addbuddy 丙",ChatFollowupKind.AddBuddy,"丙"),("/新增好友 丙",ChatFollowupKind.AddBuddy,"丙"),
            ("/delbuddy 丙",ChatFollowupKind.DelBuddy,"丙"),("/刪除好友 丙",ChatFollowupKind.DelBuddy,"丙"),("/exclude 丁",ChatFollowupKind.Exclude,"丁"),("/斷絕 丁",ChatFollowupKind.Exclude,"丁"),
            ("/include 丁",ChatFollowupKind.Include,"丁"),("/包含 丁",ChatFollowupKind.Include,"丁"),("/trade",ChatFollowupKind.Trade,""),("/交換",ChatFollowupKind.Trade,""),
            ("/shop",ChatFollowupKind.Shop,""),("/商店",ChatFollowupKind.Shop,""),("/bookmark 家",ChatFollowupKind.Bookmark,"家"),("/記憶  家 門 ",ChatFollowupKind.Bookmark,"家 門"),
            ("/qbookmark 家",ChatFollowupKind.QBookmark,"家"),("/Trade\t",ChatFollowupKind.Trade,"")};
        foreach(var (input,kind,arg) in cases)
            t.Check(ChatFollowupCommands.Parse(input) is {} c&&c.Kind==kind&&c.Argument==arg,"指令 "+input,ChatFollowupCommands.Parse(input));
        foreach(var other in new[]{"/inviter","/invites","invite","/ invite","/chatparty","/chatbanish 甲","/記憶體","/qbookmarks","","/","你好 /trade","/duel"})
            t.Check(ChatFollowupCommands.Parse(other)==null,"不是 R9-C 指令："+other);
        t.Equal("指令 · 邀請隊員",PlayerChatProtocol.Describe("/invite"),"聊天列說明");
        t.Equal("指令 · 記憶座標",PlayerChatProtocol.Describe("/記憶 家"),"聊天列說明（中文）");
        t.Check(PlayerChatProtocol.Valid("/trade"),"有效輸入");
        t.Check(ClientChatCommands.Parse("/invite")==null&&!DuelChatCommand.Is("/trade")&&PlayerQueryPackets.Encode("/buddy",Me,null)==null,"不與既有指令重疊");
        t.Check(PlayerChatProtocol.InputHelp.Contains("/invite")&&PlayerChatProtocol.InputHelp.Contains("/bookmark"),"說明列出新指令");
    }

    // ② S161：換第一個 ^／!、去開頭 \fX（FU9§4.1）。
    static void CodeText(WorldModuleTestContext t){
        t.Equal(" a b^c!d",PlayerChatProtocol.CodeText("^a!b^c!d"),"只換第一個 ^ 與第一個 !");
        t.Equal("注意",PlayerChatProtocol.CodeText(@"\fY注意"),"開頭 \\fY 拿掉");
        t.Equal(@"甲\fY乙",PlayerChatProtocol.CodeText(@"甲\fY乙"),"中間的 \\f 不動");
        t.Equal(@"\f",PlayerChatProtocol.CodeText(@"\f"),"不足 3 字不動");
        t.Equal(@"\f x",PlayerChatProtocol.CodeText(@"\f!x"),"! 換成空白後第 3 字元 < '0'，不去色碼");
        t.Equal("",PlayerChatProtocol.CodeText(""),"空字串");
        t.Check(PlayerChatProtocol.TryDecode(P([161,0],Le32(5),Str("商人: 歡迎^光臨!")),out var line)&&line!.Text=="商人: 歡迎 光臨 ","type 0 套用",line);
        t.Check(PlayerChatProtocol.TryDecode(P([161,2],Le32(5),Str(@"\fR<守衛> 站住!")),out var shout)&&shout!.Text=="<守衛> 站住 "&&shout.Channel==PlayerChatChannel.Shout,"type 2 去色碼並換 !",shout);
        t.Check(!PlayerChatProtocol.TryDecode(P([161,0],Le32(0),Str("系統")),out _),"id 0 仍不當聊天");
    }

    // ③ id 0 系統文字（S_ServerMessage(String)、S_ToGmMessage 的 "\fY"+info）也套用；公告分類仍比對原字串。
    static void SystemText(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using var c=Seed(sent);
        c.World.Receive(P([161,0],Le32(0),Str(@"\fY系統!訊息^x")),"offline");
        t.Equal("系統 訊息 x",c.World.LastServerMessage,"id 0 去 \\fY、換 ! 與 ^");
        c.World.Receive(P([161,0],Le32(0),Str(@"\fR地面物品將在 30 秒後清除。")),"offline");
        t.Equal("地面物品將在 30 秒後清除。",c.World.LastAnnouncement,"公告分類照舊");
        t.Equal("系統 訊息 x",c.World.LastServerMessage,"公告不覆蓋伺服器訊息");
    }

    // ④ 組隊：/invite 帶名稱不送、/chatinvite → C230 2、/banish → C255、/outparty → C33。
    static void Party(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using var c=Seed(sent);
        t.Check(!Say(c,"/invite 甲")&&sent.Count==0&&Notice(c).Contains("3.81"),"以名稱邀請：3.81 不支援，不送",Notice(c));
        t.Check(Say(c,"/chatinvite 乙")&&sent.Count==1&&sent[0].SequenceEqual(LoginPackets.Pad([230,2,..LoginPackets.TextEncoding.GetBytes("乙"),0])),"/chatinvite → C230 type 2 S",sent.Select(Convert.ToHexString));
        t.Check(PartyPackets.ChatInvite("乙").SequenceEqual(sent[0]),"PartyPackets.ChatInvite 同一包");
        t.Check(!Say(c,"/chatinvite")&&sent.Count==1&&Notice(c).Contains("您想要邀請誰至聊天隊伍呢？"),"不帶名稱：印 960、不送",Notice(c));
        t.Check(!Say(c,"/chatinvite 甲 乙")&&sent.Count==1,"名稱含空白不送");
        t.Check(Say(c,"/banish 丙")&&sent.Count==2&&sent[1].SequenceEqual(Op(255,"丙")),"/banish → C255",sent.Select(Convert.ToHexString));
        t.Check(!Say(c,"/banish")&&sent.Count==2,"/banish 不帶名稱不送");
        t.Check(Say(c,"/離開隊伍")&&sent.Count==3&&sent[2].SequenceEqual(Pad(33)),"/outparty → C33");
        t.Check(Assert.Throws(()=>PartyPackets.ChatInvite("")),"C230 type 2 名稱不可空白");
    }

    // ⑤ /invite 不帶名稱：選目標模式（179），點中 30 格內的玩家才送 C230 type 0。
    static void InvitePick(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using var c=Seed(sent);var w=c.World;
        w.Receive(Appear(Peer,(ushort)(X+5),Y,"甲",flags:4),"offline");w.Receive(Appear(Far,(ushort)(X+31),Y,"遠",flags:4),"offline");w.Receive(Appear(Npc,(ushort)(X+2),Y,"怪"),"offline");
        t.Check(!w.PartyInvitePicking,"一開始不在選目標");
        t.Check(Say(c,"/invite")&&sent.Count==0&&w.PartyInvitePicking&&Notice(c).Contains("請選擇對象。"),"/invite 進入選目標、不送",Notice(c));
        t.Check(PartyInviteTargeting.Range==30&&PartyInviteTargeting.Cursor==179,"範圍 30、字串 179");
        t.Check(!c.PickPartyInviteTarget(Far).GetAwaiter().GetResult()&&sent.Count==0&&!w.PartyInvitePicking,"31 格：不送、結束選目標");
        Say(c,"/invite");
        t.Check(!c.PickPartyInviteTarget(Npc).GetAwaiter().GetResult()&&sent.Count==0,"非玩家不送");
        Say(c,"/invite");
        t.Check(!c.PickPartyInviteTarget(Me).GetAwaiter().GetResult()&&sent.Count==0,"自己不送");
        Say(c,"/邀請隊員");
        t.Check(c.PickPartyInviteTarget(Peer).GetAwaiter().GetResult()&&sent.Count==1&&sent[0].SequenceEqual(PartyPackets.Invite(Peer,0))&&!w.PartyInvitePicking,"點中玩家 → C230 type 0 D id",sent.Select(Convert.ToHexString));
        t.Check(!c.PickPartyInviteTarget(Peer).GetAwaiter().GetResult()&&sent.Count==1,"不在選目標時不送");
        Say(c,"/invite");w.Reset();
        t.Check(!w.PartyInvitePicking,"世界重置後選目標失效");
    }

    // ⑥ 好友：/buddy → C4；/addbuddy → C207＋C4（名稱錯誤印 53）；/delbuddy → C202＋C4。
    static void Buddies(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using var c=Seed(sent);
        t.Check(Say(c,"/buddy")&&sent.Count==1&&sent[0].SequenceEqual(Pad(4)),"/buddy → C4");
        t.Check(Say(c,"/addbuddy 丙")&&sent.Count==3&&sent[1].SequenceEqual(Op(207,"丙"))&&sent[2].SequenceEqual(Pad(4)),"/addbuddy → C207＋C4",sent.Select(Convert.ToHexString));
        t.Check(c.World.BuddyStale,"新增後名單標為待確認");
        t.Check(!Say(c,"/addbuddy")&&sent.Count==3&&Notice(c).StartsWith("無效的名字。"),"/addbuddy 不帶名稱 → 53",Notice(c));
        t.Check(!Say(c,"/新增好友 甲 乙")&&sent.Count==3&&Notice(c).StartsWith("無效的名字。"),"名稱含空白 → 53",Notice(c));
        t.Check(Say(c,"/delbuddy 丙")&&sent.Count==5&&sent[3].SequenceEqual(Op(202,"丙"))&&sent[4].SequenceEqual(Pad(4)),"/delbuddy → C202＋C4",sent.Select(Convert.ToHexString));
        t.Check(!Say(c,"/delbuddy")&&sent.Count==5,"/delbuddy 不帶名稱不送");
    }

    // ⑦ 封鎖：/exclude 名稱 → C171（M37 推測）；已在名單不送；/include 只對名單內的名稱送 C171（3.81 切換）。
    static void Exclude(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using var c=Seed(sent);var w=c.World;
        t.Check(!Say(c,"/exclude")&&sent.Count==0,"/exclude 不帶名稱不送");
        t.Check(Say(c,"/exclude 丁")&&sent.Count==1&&sent[0].SequenceEqual(Op(171,"丁"))&&w.ExcludePending,"/exclude 丁 → C171",sent.Select(Convert.ToHexString));
        t.Check(!Say(c,"/斷絕 戊")&&sent.Count==1,"等待回覆時不再切換");
        w.Receive(P([250,18],Str("丁")),"offline");
        t.Check(!w.ExcludePending&&w.IsExcluded("丁"),"S250/18 加入名單");
        t.Check(!Say(c,"/exclude 丁")&&sent.Count==1&&Notice(c).Contains("/include"),"已在名單：不送（C171 會變成解除）",Notice(c));
        t.Check(!Say(c,"/include 戊")&&sent.Count==1,"不在名單：/include 不送");
        t.Check(Say(c,"/包含 丁")&&sent.Count==2&&sent[1].SequenceEqual(Op(171,"丁")),"/include 丁 → C171 切換",sent.Select(Convert.ToHexString));
    }

    // ⑧ /trade：面前是擺攤玩家 → 989 不送；面對面玩家 → C2 並進入邀請中；面前沒人 → 照送 C2、狀態不動。
    static void Trade(WorldModuleTestContext t){
        ushort ax=Ahead(X,MovementRules.Dx),ay=Ahead(Y,MovementRules.Dy);
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            c.World.Receive(Appear(Vendor,ax,ay,"攤販",flags:4,dir:4,action:70),"offline");
            t.Check(c.World.PlayerAhead is {ShopActive:true},"面前是擺攤玩家",c.World.PlayerAhead);
            t.Check(!Say(c,"/trade")&&sent.Count==0&&Notice(c)=="無法與開設個人商店的玩家進行交易。","989、不送",Notice(c));
        }
        sent.Clear();
        using(var c=Seed(sent)){
            c.World.Receive(Appear(Peer,ax,ay,"甲",flags:4,dir:4),"offline");
            t.Check(Say(c,"/交換")&&sent.Count==1&&sent[0].SequenceEqual(TradePackets.Invite())&&c.World.TradeState=="inviting"&&c.World.TradePeerName=="甲","面對面 → C2、邀請中",new{c.World.TradeState,c.World.TradePeerName});
            t.Check(!Say(c,"/trade")&&sent.Count==1,"交易流程中不再送");
            t.Check(c.World.Target==null,"不需要選取目標");
        }
        sent.Clear();
        using(var c=Seed(sent)){
            c.World.Receive(Appear(Peer,ax,ay,"甲",flags:4,dir:0),"offline"); // 沒有面向自己
            t.Check(Say(c,"/trade")&&sent.Count==1&&sent[0].SequenceEqual(Pad(2))&&c.World.TradeState=="idle","沒有面向自己：照送 C2、狀態不動",c.World.TradeState);
        }
    }

    // ⑨ /shop：自己擺攤中 → C38 type 1＋903；否則切換設定窗（離線沒有視窗 → 不送、提示）。
    static void Shop(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using var c=Seed(sent);
        t.Check(!Say(c,"/shop")&&sent.Count==0&&Notice(c).Contains("個人商店設定窗"),"不在擺攤：只開窗（離線沒有視窗）",Notice(c));
        c.World.Receive(P([119],Le32(Me),[70]),"offline");
        t.Check(c.World.Actor?.Weapon==70,"S119 動作 70＝擺攤");
        t.Check(Say(c,"/商店")&&sent.Count==1&&sent[0].SequenceEqual(PersonalShopPackets.Close())&&Notice(c)=="結束個人商店模式。","擺攤中 → C38 type 1、903",Notice(c));
    }

    // ⑩ /bookmark 名稱 → C165；不帶名稱開窗；> 30 byte 印 204；/qbookmark 名稱同樣送 C165。
    static void Bookmarks(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using var c=Seed(sent);
        t.Check(Say(c,"/bookmark 家")&&sent.Count==1&&sent[0].SequenceEqual(BookmarkPackets.Add("家")),"/bookmark 家 → C165",sent.Select(Convert.ToHexString));
        t.Check(c.World.Bookmarks.Notice.Contains("記憶目前位置"),"書籤狀態記下已送出",c.World.Bookmarks.Notice);
        t.Check(Say(c,"/記憶 村莊 入口")&&sent.Count==2&&sent[1].SequenceEqual(BookmarkPackets.Add("村莊 入口")),"中文別名、名稱可含空白");
        t.Check(!Say(c,"/記憶 "+new string('長',16))&&sent.Count==2&&Notice(c)=="所標記地點的名稱太長。","32 byte → 204",Notice(c));
        t.Check(Say(c,"/記憶 "+new string('長',15))&&sent.Count==3,"30 byte 照送");
        t.Check(!Say(c,"/bookmark")&&sent.Count==3&&Notice(c).Contains("記憶座標視窗"),"不帶名稱：開窗（離線沒有視窗），不送",Notice(c));
        t.Check(Say(c,"/qbookmark 洞穴")&&sent.Count==4&&sent[3].SequenceEqual(BookmarkPackets.Add("洞穴")),"/qbookmark → C165");
        t.Check(!Say(c,"/qbookmark")&&sent.Count==4,"/qbookmark 不帶名稱不送");
    }

    // ⑪ 有字串表時照表（去掉色碼）。
    static void StringTable(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(SyntheticTable(ChatFollowupCommands.ShopTrade,@"\f1表內的 989")))
            t.Equal("表內的 989",ChatFollowupCommands.ShopTradeText,"989 照表");
        using(ServerMessageText.UseTable(SyntheticTable(ChatFollowupCommands.SelectTarget,@"\f1請選擇對象。")))
            t.Equal("請選擇對象。",ChatFollowupCommands.SelectTargetText,"179 照表並去色碼");
        using(ServerMessageText.UseTable(SyntheticTable(10,"x")))
            t.Equal("無法與開設個人商店的玩家進行交易。",ChatFollowupCommands.ShopTradeText,"表沒有這一筆：用本機原文");
    }

    static class Assert {
        public static bool Throws(Action a){try{a();return false;}catch(ArgumentException){return true;}}
    }
}
