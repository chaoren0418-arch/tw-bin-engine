using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R10-F（T390／#905，gap-matrix 15.12.6；1:29、4:ExchangeUI）離線測試：聊天輸入列的確認與提示。
// 涵蓋：1028 的答覆經 SendPlayerChat 分派到 AnswerPrivateShopConfirm（Y 送原來那一包、N 不送、舊的戳記不答）；
// ChatLinePrompts 把刪除確認／1028 交給原生聊天列；原生 ChatPanel 在 CHAT_BAR 畫提示與 playerChatNotice；
// 有提示時空白也能送出、同一個 Enter 就答、Esc＝取消；交易 140 改在聊天列問（AskOnChatLine），交易窗不再吃數字與 Esc；
// 沒有聊天列時（AskOnChatLine=false）保留 R9-K 的文字框備援。
// 只用合成封包／XML／surf 與記憶體送出端；不開遊戲、伺服器、DB，不讀客戶端資源。由 --module-tests 執行（名稱 chat-line-confirm）。
static class ChatLineConfirmTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Owner=456,Gold=999,Me=123,Item=901;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    // 封包格式同 PrivateShopFollowupTests（S15 背包、S140 買清單、S87 擺攤中的店主）。
    static byte[] ItemPacket(uint id,ushort icon,uint count,string name)=>Packet(b=>{b.Write((byte)15);b.Write(id);b.Write((ushort)0);b.Write((byte)0);b.Write((byte)0);b.Write(icon);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,name);b.Write((byte)0);b.Write(new byte[11]);});
    static byte[] BuyList(params (byte order,ushort icon,uint listed,uint price,string name)[] rows)=>Packet(b=>{b.Write((byte)140);b.Write((byte)0);b.Write(Owner);b.Write((ushort)rows.Length);
        foreach(var r in rows){b.Write(r.order);b.Write((byte)0);b.Write(r.icon);b.Write(r.listed);b.Write(r.price);Text(b,r.name);b.Write((byte)0);}});
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Owner);b.Write((ushort)1110);b.Write((byte)70);b.Write((byte)6);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);Text(b,"shopkeeper");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
        c.World.SelectTarget(Owner);Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,ItemPacket(Gold,5,500000,"$4"));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    /// <summary>私商買清單開著，按下合計 180000 的一包 → 1028 等待答覆。</summary>
    static LoginConnection Pending(List<byte[]> sent){
        var c=Seed(sent);c.World.PrivateShopNativeWindow=true;
        c.SendPrivateShop(true,false,0,0,c.PrivateShopSnapshot()).GetAwaiter().GetResult();
        Feed(c,BuyList((0,11,10,90000,"gem")));
        c.SendPrivateShopBatch([new ShopChoice(0,2)],c.World.PrivateShopRevision).GetAwaiter().GetResult();
        return c;
    }
    static bool Say(LoginConnection c,string text,int? world=null)=>c.SendPlayerChat(text,c.SessionGeneration,world??c.World.WorldEpoch,Me).GetAwaiter().GetResult();
    static string Hex(byte[] p)=>Convert.ToHexString(p);

    sealed class ChatCommands:IUiChatCommands {
        public readonly List<string> Calls=[];
        public Func<string,PlayerChatView,Task<bool>>? Send;
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools");
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool BeginChatInput(){Calls.Add("begin");return true;}
        public void EndChatInput()=>Calls.Add("end");
        public Task<bool> SendChat(string text,PlayerChatView displayed){Calls.Add("send:"+text);return Send?.Invoke(text,displayed)??Task.FromResult(true);}
    }
    static IUiGameState State(string notice="",PlayerChatView? view=null)=>new UiChatGameState(new UiStaticGameState(InWorld:true),new(view??new PlayerChatView(1,1,Me,1,true,notice,[]),false));
    static UiKeyEvent Down(Keys k)=>new(k,true,false);
    static UiKeyEvent Up(Keys k)=>new(k,false,false);

    [WorldModuleTest("chat-line-confirm")]
    static void Run(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){Dispatch1028(t);PromptSources(t);}
        ChatPanelPrompt(t);ChatPanelPaint(t);
        using(ServerMessageText.UseTable(null))TradeOnChatLine(t);
    }

    // 1. SendPlayerChat 的確認分派：1028 等待時這一列是答覆（PlayerChat.cs）。
    static void Dispatch1028(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Pending(sent);int before=sent.Count;
        t.Check(c.PrivateShopConfirmCurrent!=null&&c.PlayerChatSnapshot().Notice==PrivateShopTrade.TotalText(),"1028 pending in the input-line prompt");
        t.Check(!Say(c,"y",world:c.World.WorldEpoch+1)&&sent.Count==before&&c.PrivateShopConfirmCurrent!=null,"stale stamp: not answered, still pending");
        t.Check(Say(c,"n")&&sent.Count==before&&c.PrivateShopConfirmCurrent==null,"N through the chat line → consumed, nothing sent");
        t.Check(!c.PlayerChatSnapshot().Notice.Contains("訊息已送出"),"N is not sent as chat",c.PlayerChatSnapshot().Notice);
        c.SendPrivateShopBatch([new ShopChoice(0,2)],c.World.PrivateShopRevision).GetAwaiter().GetResult();
        t.Check(c.PrivateShopConfirmCurrent!=null,"asks again");
        t.Check(Say(c,"y")&&sent.Count==before+1,"Y through the chat line → one packet");
        t.Equal(Hex(LoginPackets.Pad([161,..BitConverter.GetBytes(Owner),0,1,0,..BitConverter.GetBytes(0u),..BitConverter.GetBytes(2u)])),Hex(sent[^1]),"Y sends the confirmed batch (C161 buy row 0 × 2)");
        t.Check(c.PrivateShopConfirmCurrent==null&&c.World.PrivateShopPhase=="submitted","after Y: nothing pending, submitted");
        // 空白答覆＝不是 Y：不送。
        var sent2=new List<byte[]>();using var d=Pending(sent2);int n=sent2.Count;
        t.Check(Say(d,"")&&sent2.Count==n&&d.PrivateShopConfirmCurrent==null,"blank answer → consumed, not sent");
    }

    // 2. ChatLinePrompts：刪除確認、1028 交給聊天列（答覆走 SendChat，Esc 取消）。
    static void PromptSources(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Pending(sent);
        var p=ChatLinePrompts.Of(c);
        t.Check(p is {Local:false}&&p.Text==PrivateShopTrade.TotalText(),"1028 → chat-line prompt answered through SendChat",p);
        t.Check(Equals(p!.Key,ChatLinePrompts.Of(c)!.Key),"same pending confirm → same key");
        p.Cancel!();
        t.Check(c.PrivateShopConfirmCurrent==null&&ChatLinePrompts.Of(c)==null,"Esc (Cancel) drops 1028, nothing sent",sent.Count);
        using var idle=Seed([]);
        t.Check(ChatLinePrompts.Of(idle)==null,"nothing pending → no prompt");
    }

    // 3. 原生 ChatPanel：提示時的開啟、送出、Esc（不需版面）。
    static void ChatPanelPrompt(WorldModuleTestContext t){
        var chat=new ChatPanel();var cmd=new ChatCommands();
        var answers=new List<string>();int cancels=0;int key=1;
        ChatLinePrompt? current=null;
        ChatLinePrompt Local()=>new(key,"要給幾個？",s=>{answers.Add(s);current=null;return true;},()=>{cancels++;current=null;});
        chat.LinePrompts=()=>current;
        // 沒有提示：空白 Enter 不送（照舊）。
        chat.Update(State());
        chat.Key(Down(Keys.Enter),cmd);chat.Key(Up(Keys.Enter),cmd);
        t.Check(chat.Input.Open&&cmd.Calls.SequenceEqual(["begin"]),"no prompt: Enter opens the line");
        chat.Key(Down(Keys.Enter),cmd);chat.Key(Up(Keys.Enter),cmd);
        t.Check(!cmd.Calls.Any(x=>x.StartsWith("send:",StringComparison.Ordinal)),"no prompt: blank line is not sent",cmd.Calls);
        chat.Key(Down(Keys.Escape),cmd);cmd.Calls.Clear();
        // 本機提示：同一個 Enter 打開並答覆（空白＝全部），答完關閉。
        current=Local();chat.Update(State());
        t.Check(chat.Prompt is {Local:true},"prompt picked up in Update");
        t.Check(chat.Key(Down(Keys.Enter),cmd)&&answers.SequenceEqual([""])&&!chat.Input.Open&&cmd.Calls.SequenceEqual(["begin","end"]),"prompt: one Enter opens and answers blank",cmd.Calls);
        t.Check(!cmd.Calls.Any(x=>x.StartsWith("send:",StringComparison.Ordinal)),"local answer never goes to SendChat");
        chat.Key(Up(Keys.Enter),cmd);
        // 打字打開：數字鍵打開輸入列，字元走 Char()。
        key=2;current=Local();chat.Update(State());cmd.Calls.Clear();
        t.Check(!chat.Key(Down(Keys.D3),cmd)&&chat.Input.Open&&cmd.Calls.SequenceEqual(["begin"]),"digit key opens the line and passes on to WM_CHAR",cmd.Calls);
        chat.Char('3');chat.Char('0');
        chat.Key(Down(Keys.Enter),cmd);chat.Key(Up(Keys.Enter),cmd);
        t.Check(answers.SequenceEqual(["","30"])&&!chat.Input.Open&&chat.Input.Text=="","typed amount answered, line cleared and closed",answers);
        t.Check(!ChatPanel.PromptOpensOn(Keys.Control|Keys.D3)&&!ChatPanel.PromptOpensOn(Keys.Up)&&ChatPanel.PromptOpensOn(Keys.NumPad5)&&ChatPanel.PromptOpensOn(Keys.Y),"only plain letters/digits open the line");
        // Esc：取消提示（輸入列關著或開著都一樣）。
        key=3;current=Local();chat.Update(State());cmd.Calls.Clear();
        t.Check(chat.Key(Down(Keys.Escape),cmd)&&cancels==1&&answers.Count==2&&!chat.Input.Open,"Esc with the line closed → Cancel");
        key=4;current=Local();chat.Update(State());chat.Key(Down(Keys.D1),cmd);chat.Char('1');
        t.Check(chat.Key(Down(Keys.Escape),cmd)&&cancels==2&&!chat.Input.Open&&chat.Input.Text==""&&cmd.Calls.Contains("end"),"Esc with the line open → Cancel, closed",cmd.Calls);
        // 新提示清掉草稿。
        current=null;chat.Update(State());chat.Key(Down(Keys.Enter),cmd);chat.Key(Up(Keys.Enter),cmd);chat.Char('x');
        key=5;current=Local();chat.Update(State());
        t.Check(chat.Input.Text=="","a new prompt clears the draft");
        chat.Key(Down(Keys.Escape),cmd);
        // 送出型提示（刪除、1028）：答覆走 SendChat，空白也送，送出後關閉。
        int sendKey=6;var server=new ChatLinePrompt(sendKey,"確定要購買嗎？(Y/N)",null,()=>cancels++);current=server;
        chat.Update(State());cmd.Calls.Clear();
        chat.Key(Down(Keys.Y),cmd);chat.Char('y');
        chat.Key(Down(Keys.Enter),cmd);
        t.Check(cmd.Calls.SequenceEqual(["begin","send:y","end"])&&!chat.Input.Open,"server prompt: Y goes through SendChat, line closes",cmd.Calls);
        chat.Key(Up(Keys.Enter),cmd);cmd.Calls.Clear();
        t.Check(chat.Key(Down(Keys.Enter),cmd)&&cmd.Calls.SequenceEqual(["begin","send:","end"]),"server prompt: blank Enter is sent as the answer",cmd.Calls);
        chat.Key(Up(Keys.Enter),cmd);
        // SendChat 沒送成（戳記舊）：輸入列不關。
        cmd.Send=(_,_)=>Task.FromResult(false);cmd.Calls.Clear();
        chat.Key(Down(Keys.Enter),cmd);
        t.Check(chat.Input.Open&&!cmd.Calls.Contains("end"),"not sent → line stays open",cmd.Calls);
        chat.Key(Up(Keys.Enter),cmd);chat.Key(Down(Keys.Escape),cmd);
    }

    // 4. CHAT_BAR 上畫提示／playerChatNotice（版面同 ChatFollowupTests 的 CENTER_WINDOW 最小版面）。
    static readonly (string Path,int? X,int? Y,bool Abs,int Surf)[] Fixture=[
        ("CHAT_MAIN_WINDOW/CHAT_WINDOW",0,600,true,3279),("CHAT_MAIN_WINDOW/CHAT_BORN",0,600,true,3280),
        ("CHAT_MAIN_WINDOW/LCHAT_WINDOW",0,600,true,3288),("CHAT_MAIN_WINDOW/LCHAT_BORN",0,600,true,3289),
        ("CHAT_MAIN_WINDOW/RCHAT_WINDOW",260,600,true,3290),("CHAT_MAIN_WINDOW/RCHAT_BORN",260,600,true,3291),
        ("CHAT_MAIN_WINDOW/CHAT_BAR",10,114,false,2273),
        ("CHAT_STEP_CONTROL/CHAT_UP",345,115,false,2274),("CHAT_STEP_CONTROL/CHAT_DOWN",345,115,false,2277),
        ("CHAT_SCROLL_CONTROL/SCROLL_UP_OFF",346,30,false,2284),("CHAT_SCROLL_CONTROL/SCROLL_UP_ON",null,null,false,2285),
        ("CHAT_SCROLL_CONTROL/SCROLL_DOWN_OFF",346,107,false,2286),("CHAT_SCROLL_CONTROL/SCROLL_DOWN_ON",null,null,false,2287),
        ("CHAT_SCROLL_CONTROL/SCROLL_KNOB",null,null,false,2288),
        ("CHAT_ICON/CHAT_ICON_FRAME",164,4,false,3285),("CHAT_ICON/CHAT_ICON_ACTIVE_ON",299,5,false,3286),("CHAT_ICON/CHAT_ICON_ACTIVE",299,5,false,3287),
        ("CHAT_ICON/GENERAL_CHAT_ON",179,5,false,2290),("CHAT_ICON/GENERAL_CHAT",179,5,false,2291),
        ("CHAT_ICON/PARTY_CHAT_ON",198,5,false,2292),("CHAT_ICON/PARTY_CHAT",198,5,false,2293),
        ("CHAT_ICON/PLEDGE_CHAT_ON",221,5,false,2294),("CHAT_ICON/PLEDGE_CHAT",221,5,false,2295),
        ("CHAT_ICON/TRADE_CHAT_ON",240,5,false,2296),("CHAT_ICON/TRADE_CHAT",240,5,false,2297),
        ("CHAT_ICON/SYSTEM_CHAT_ON",258,5,false,2975),("CHAT_ICON/SYSTEM_CHAT",258,5,false,2976),
        ("CHANNEL_ICON/GENERAL_CHANNEL",8,115,false,2977),("CHANNEL_ICON/GENERAL_CHANNEL_ON",null,null,false,2978),
        ("CHANNEL_ICON/BLOOD_CHANNEL",8,100,false,2980),("CHANNEL_ICON/BLOOD_CHANNEL_ON",null,null,false,2981),
        ("CHANNEL_ICON/PARTY_CHANNEL",8,85,false,2983),("CHANNEL_ICON/PARTY_CHANNEL_ON",null,null,false,2984),
        ("CHANNEL_ICON/WHOLE_CHANNEL",8,70,false,2986),("CHANNEL_ICON/WHOLE_CHANNEL_ON",null,null,false,2987),
        ("CHANNEL_ICON/TRADE_CHANNEL",8,55,false,2989),("CHANNEL_ICON/TRADE_CHANNEL_ON",null,null,false,2990),
        ("CHANNEL_ICON/CHANNEL_CHANNEL",8,40,false,2992),("CHANNEL_ICON/CHANNEL_CHANNEL_ON",null,null,false,2993),
        ("CHANNEL_ICON/ALLIANCE_CHANNEL",8,25,false,2995),("CHANNEL_ICON/ALLIANCE_CHANNEL_ON",null,null,false,2996),
        ("CHANNEL_ICON/UPPER_CHANNEL",8,10,false,2998),("CHANNEL_ICON/UPPER_CHANNEL_ON",null,null,false,2999),
    ];
    static UiLayout Layout(){
        var sb=new System.Text.StringBuilder("<MainStatusConfigulation><CENTER_WINDOW x=\"129\" y=\"470\" stepSize=\"36\">");
        foreach(var g in Fixture.GroupBy(f=>f.Path.Split('/')[0])){
            sb.Append($"<{g.Key}>");
            foreach(var f in g)sb.Append($"<{f.Path.Split('/')[1]} surfNumber=\"{f.Surf}\"{(f.X is int fx?$" xRPos=\"{fx}\"":"")}{(f.Y is int fy?(f.Abs?$" yPos=\"{fy}\"":$" yRPos=\"{fy}\""):"")}/>");
            sb.Append($"</{g.Key}>");
        }
        sb.Append("</CENTER_WINDOW></MainStatusConfigulation>");
        return UiLayout.Parse(sb.ToString(),"uistatus86-c.xml");
    }
    static readonly Dictionary<int,UiSurface> surfaces=[];
    static UiSurface? Surface(int id){
        if(surfaces.TryGetValue(id,out var s))return s;
        (int w,int h)=id switch{3279 or 3280=>(362,240),3288 or 3289=>(260,240),3290 or 3291=>(259,240),2273=>(346,16),_=>(14,12)};
        return surfaces[id]=new UiSurface(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,new uint[w*h]));
    }
    static IReadOnlyList<ChatDraw> Paint(ChatPanel p){using var c=new UiCanvas();p.Paint(c);return p.Drawn.ToArray();}

    static void ChatPanelPaint(WorldModuleTestContext t){
        var chat=new ChatPanel{CharWidth=_=>6};chat.Load(Layout(),Surface);var cmd=new ChatCommands();
        ChatLinePrompt? current=null;chat.LinePrompts=()=>current;
        // 沒有提示：輸入列關著時畫 playerChatNotice。
        chat.Update(State(notice:"訊息已送出；聊天內容以伺服器回傳為準。"));
        var d=Paint(chat);
        t.Check(d.SingleOrDefault(x=>x.Element=="NOTICE") is {Text:"訊息已送出；聊天內容以伺服器回傳為準。"} n&&n.Rect.Location==new Point(143,586),"closed line: notice drawn in CHAT_BAR (10,114)+(4,2)",d.Select(x=>x.Element));
        t.Check(!d.Any(x=>x.Element is "PROMPT" or "INPUT"),"no prompt, no input drawn");
        chat.Update(State());
        t.Check(!Paint(chat).Any(x=>x.Element is "NOTICE" or "PROMPT"),"empty notice → nothing drawn");
        // 提示優先於 notice；太長只畫開頭（346−8＝338 px／6＝56 字）。
        string longText=new string('甲',80);
        current=new(1,longText);chat.Update(State(notice:"舊訊息"));
        d=Paint(chat);
        t.Check(d.SingleOrDefault(x=>x.Element=="PROMPT") is {Text:{Length:56}}&&!d.Any(x=>x.Element=="NOTICE"),"prompt wins over the notice; head that fits",d.Select(x=>x.Element+":"+x.Text?.Length));
        // 輸入列打開：提示在輸入文字前面。
        current=new(2,"要給幾個？ (1-5， Enter=全部，Esc=取消)");chat.Update(State());
        chat.Key(Down(Keys.D2),cmd);chat.Char('2');
        d=Paint(chat);
        t.Check(d.SingleOrDefault(x=>x.Element=="PROMPT") is {Text:"要給幾個？ (1-5， Enter=全部，Esc=取消) "} pr&&d.SingleOrDefault(x=>x.Element=="INPUT") is {Text:"2"} inp&&inp.Rect.X==pr.Rect.Right,"open line: prompt then typed text",d.Select(x=>x.Element+":"+x.Text));
        // 長提示＋輸入：只畫尾端（游標在尾端）。
        current=new(3,longText);chat.Update(State());chat.Char('9');
        d=Paint(chat);
        var shown=string.Concat(d.Where(x=>x.Element is "PROMPT" or "INPUT").Select(x=>x.Text));
        t.Check(shown.EndsWith(" 9",StringComparison.Ordinal)&&shown.Length==56,"long prompt + input: tail that fits",shown.Length);
        chat.Key(Down(Keys.Escape),cmd);
    }

    // 5. 交易 140 在聊天列問（4:ExchangeUI）。
    sealed class FakeSource:ITransactionSource {
        public TransactionView? Current{get;set;}
        public readonly List<string> Calls=[];
        public void Confirm(int revision)=>Calls.Add("confirm:"+revision);
        public void Cancel(int revision)=>Calls.Add("cancel:"+revision);
        public void Add(int revision,uint item,uint count)=>Calls.Add($"add:{revision}:{item}:{count}");
    }
    static Func<int,UiSurface?> TradeSurfaces()=>LegacyWindowTestKit.Surfaces(new(){[2000]=(180,270),[2001]=(200,290),[1962]=(14,14),[2008]=(160,30),[2002]=(50,18),[2003]=(50,18),[2004]=(50,18),[2005]=(50,18),[2006]=(50,18),[2007]=(50,18)}).Get;
    static TransactionPanel TradePanel(){var p=new TransactionPanel();p.Load(UiLayout.Parse(TransactionPanelTests.Xml,"uiextra86-c.xml"),TradeSurfaces());return p;}

    static void TradeOnChatLine(WorldModuleTestContext t){
        var stack=new TransactionItemChoice(Item,"test item",5,0);
        var f=new FakeSource{Current=new TransactionView(1,"x","交易中",[],true,true,false,Generation:3,CanAdd:true,Selected:stack)};
        var trade=TradePanel();trade.Source=f;trade.AskOnChatLine=true;trade.Update(new UiStaticGameState(InWorld:true));
        var chat=new ChatPanel();chat.LinePrompts=()=>trade.LinePrompt;var cmd=new ChatCommands();
        t.Check(trade.LinePrompt==null,"no prompt before adding");
        t.Check(!trade.AddSelected()&&trade.Prompt!=null&&trade.LinePrompt is {Local:true} lp&&lp.Text==TradeAmountText.Ask(5),"stack → 140 handed to the chat line",trade.LinePrompt);
        t.Check(trade.NoticeText!=TradeAmountText.Ask(5),"text frame no longer shows 140",trade.NoticeText);
        t.Check(!trade.Char('3')&&!trade.Key(Down(Keys.Enter),cmd)&&!trade.Key(Down(Keys.Back),cmd),"trade window passes digits / Enter / Backspace on");
        t.Check(!trade.Key(Down(Keys.Escape),cmd)&&f.Calls.Count==0,"trade window passes Esc on (no C86)");
        chat.Update(State());
        chat.Key(Down(Keys.D3),cmd);chat.Char('3');chat.Key(Down(Keys.Enter),cmd);chat.Key(Up(Keys.Enter),cmd);
        t.Check(f.Calls.SequenceEqual(["add:1:901:3"])&&trade.Prompt==null,"chat line answer 3 → C37 item × 3",f.Calls);
        // 空白＝全部。
        trade.AddSelected();chat.Update(State());
        chat.Key(Down(Keys.Enter),cmd);chat.Key(Up(Keys.Enter),cmd);
        t.Check(f.Calls.SequenceEqual(["add:1:901:3","add:1:901:5"]),"blank chat line answer → whole stack",f.Calls);
        // 不合法：不送，原因畫在交易窗文字框。
        trade.AddSelected();chat.Update(State());
        chat.Key(Down(Keys.D9),cmd);chat.Char('9');chat.Key(Down(Keys.Enter),cmd);chat.Key(Up(Keys.Enter),cmd);
        t.Check(f.Calls.Count==2&&trade.Refusal is {} r&&r.Contains("數量不合法"),"9 > held → not sent, reason in the text frame",trade.Refusal);
        // Esc：只取消提示，不取消交易；之後 Esc 才是 C86。
        trade.AddSelected();chat.Update(State());
        t.Check(chat.Key(Down(Keys.Escape),cmd)&&trade.Prompt==null&&f.Calls.Count==2,"Esc on the chat line = cancel 140 only");
        chat.Update(State());
        t.Check(trade.Key(Down(Keys.Escape),cmd)&&f.Calls[^1]=="cancel:1","Esc without a prompt → C86 as before");
        // 交易結束：提示跟著消失。
        trade.AddSelected();f.Current=null;trade.Update(new UiStaticGameState(InWorld:true));chat.Update(State());
        t.Check(trade.LinePrompt==null&&chat.Prompt==null,"trade gone → chat-line prompt gone");
        // 備援（沒有原生聊天列）：140 照 R9-K 畫在文字框、本窗接數字。
        var g=new FakeSource{Current=new TransactionView(1,"x","",[],true,true,false,Generation:4,CanAdd:true,Selected:stack)};
        var old=TradePanel();old.Source=g;old.Update(new UiStaticGameState(InWorld:true));
        old.AddSelected();
        t.Check(!old.AskOnChatLine&&old.LinePrompt==null&&old.NoticeText==TradeAmountText.Ask(5)&&old.Char('2')&&old.Key(Down(Keys.Enter),cmd)&&g.Calls.SequenceEqual(["add:1:901:2"]),"fallback: text-frame prompt and digits kept",g.Calls);
    }
}
