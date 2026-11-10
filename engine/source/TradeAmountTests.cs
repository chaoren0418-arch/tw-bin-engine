using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R9-K（T371／#855）離線測試：交易窗加物品的數量提示（FU9§12.1、gap-matrix 15.11，4:ExchangeUI）。
// 堆疊先問字串 140（Enter 空白＝全部、Esc＝取消提示）再送 C37；單件直接送；自己已放 16 件以上印 255；金幣走同一個提示、金幣框已移除（#818 裁定）。
// 只用合成封包／XML／surf 與記憶體送出端；不開遊戲、伺服器、DB，不讀客戶端資源。
static class TradeAmountTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Peer=456,Item=901,Gold=902,Sword=903;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static byte[] Open(string peer)=>Packet(b=>{b.Write((byte)52);Text(b,peer);});
    static byte[] Offer(byte side,string name,ushort icon=7,byte bless=0)=>Packet(b=>{b.Write((byte)35);b.Write(side);b.Write(icon);Text(b,name);b.Write(bless);});
    /// <summary>S15 一件物品（欄位同 trade-followup 測試）。</summary>
    static byte[] AddItem(uint id,ushort desc,byte use,ushort icon,uint count,string name)=>Packet(b=>{b.Write((byte)15);b.Write(id);b.Write(desc);b.Write(use);b.Write((byte)0);b.Write(icon);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,name);b.Write((byte)0);b.Write(new byte[11]);});
    static byte[] Pc87(uint id,string name)=>Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(id);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,name);Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);});
    static string Resolve(string s)=>s.Replace("$4","金幣");

    /// <summary>交易：玩家 "target" 相鄰；背包 5 個 test item、1000 金幣、1 把長劍；已接受、S52 開窗。</summary>
    static LoginConnection SeedTrade(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        Feed(c,Pc87(Peer,"target"));Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,AddItem(Item,0,1,7,5,"test item (5)"));Feed(c,AddItem(Gold,0,0,5,1000,"$4 (1000)"));Feed(c,AddItem(Sword,0,1,8,1,"長劍"));
        Set(c.World,"tradeEpoch",c.World.WorldEpoch);Set(c.World,"TradePeerName","target");Set(c.World,"TradePeerId",(uint?)Peer);Set(c.World,"TradeState","accepting");
        Feed(c,Open("target"));
        return c;
    }
    sealed class LiveSource(LoginConnection c):ITransactionSource {
        public TransactionView? Current=>TransactionView.From(c.World,Resolve);
        void Run(string cmd,int revision,uint item=0,uint count=0)=>c.Trade(cmd,revision,c.World.WorldEpoch,c.World.TradeGeneration,item,count).GetAwaiter().GetResult();
        public void Confirm(int revision)=>Run("confirm",revision);
        public void Cancel(int revision)=>Run("cancel",revision);
        public void Add(int revision,uint item,uint count)=>Run("add",revision,item,count);
    }
    sealed class FakeSource:ITransactionSource {
        public TransactionView? Current{get;set;}
        public readonly List<string> Calls=[];
        public void Confirm(int revision)=>Calls.Add("confirm:"+revision);
        public void Cancel(int revision)=>Calls.Add("cancel:"+revision);
        public void Add(int revision,uint item,uint count)=>Calls.Add($"add:{revision}:{item}:{count}");
    }
    static UiMouseEvent M(UiMouseKind kind,Point at)=>new(kind,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,Point at,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,at),cmd);p.Mouse(M(UiMouseKind.Up,at),cmd);}
    static bool Press(TransactionPanel p,Keys key,IUiCommands cmd)=>p.Key(new UiKeyEvent(key,true,false),cmd);
    static void Type(TransactionPanel p,string text){foreach(char ch in text)p.Char(ch);}
    static Func<int,UiSurface?> Surfaces()=>LegacyWindowTestKit.Surfaces(new(){[2000]=(180,270),[2001]=(200,290),[1962]=(14,14),[2008]=(160,30),[2002]=(50,18),[2003]=(50,18),[2004]=(50,18),[2005]=(50,18),[2006]=(50,18),[2007]=(50,18)}).Get;
    static TransactionPanel Panel(){var p=new TransactionPanel();p.Load(UiLayout.Parse(TransactionPanelTests.Xml,"uiextra86-c.xml"),Surfaces());return p;}
    static readonly Point Own=new(60,60);
    static readonly UiStaticGameState InWorld=new(InWorld:true);

    [WorldModuleTest("trade-amount")]
    static void Checks(WorldModuleTestContext t){
        Rules(t);Strings(t);AdenaBoxRemoved(t);LiveStack(t);LiveAdena(t);InvalidAndEscape(t);SingleAndLimit(t);PromptLifetime(t);
    }

    static void Rules(WorldModuleTestContext t){
        t.Equal<uint?>(5,TransactionRules.Amount("",5),"blank = all (sub_82E560)");
        t.Equal<uint?>(3,TransactionRules.Amount("3",5),"typed amount within held");
        t.Equal<uint?>(5,TransactionRules.Amount("5",5),"typed amount = held");
        t.Equal<uint?>(null,TransactionRules.Amount("6",5),"above held → not sent");
        t.Equal<uint?>(null,TransactionRules.Amount("0",5),"zero → not sent");
        t.Equal<uint?>(null,TransactionRules.Amount("x",5),"not a number → not sent");
        t.Equal<uint?>(null,TransactionRules.Amount("",0),"nothing held → not sent");
        t.Equal<uint?>(null,TransactionRules.Amount("",3000000000),"blank above int.MaxValue → not sent (C37 count limit)");
        t.Equal<uint?>(int.MaxValue,TransactionRules.Amount("2147483647",3000000000),"int.MaxValue typed → sent");
        t.Equal<uint?>(null,TransactionRules.Amount("99999999999",3000000000),"overlong number → not sent");
        t.Check(!TransactionRules.AsksAmount(1)&&TransactionRules.AsksAmount(2),"single item sent directly; stack asks 140");
        t.Check(!TransactionRules.TooManyOffers(15)&&TransactionRules.TooManyOffers(16),"16 own offers already placed → 255");
    }

    static void Strings(WorldModuleTestContext t){
        t.Equal(140,TradeAmountText.AskId,"ask = string 140");
        t.Equal(255,TradeAmountText.TooManyId,"too many = string 255");
        t.Equal("要給幾個？ (1-%u， Enter=全部，Esc=取消)",TradeAmountText.AskFallback,"140 fallback = local string-c.tbl text (FU9§12.1)");
        string ask=TradeAmountText.Ask(1000);
        t.Check(ask.Contains("1-1000")&&ask.Contains("Enter=全部")&&!ask.Contains("%u"),"140 with %u = held",ask);
        string many=TradeAmountText.TooMany();
        t.Check(many.Contains("１６")&&!many.Contains('\f'),"255 text without colour code",many);
    }

    static void AdenaBoxRemoved(WorldModuleTestContext t){
        // #818 裁定：引擎自加的金幣框照原版移除（ADENA_EXCHANGE_WINDOW 不是金幣框）。
        t.Check(typeof(TransactionPanel).GetProperty("AdenaBox")==null&&typeof(TransactionPanel).GetMethod("SubmitAdena")==null&&typeof(TransactionPanel).GetProperty("AdenaInput")==null,"panel has no adena box");
        t.Check(typeof(TransactionView).GetProperty("AdenaHeld")==null&&typeof(TransactionView).GetProperty("AdenaId")==null,"view has no adena-box fields");
        t.Check(typeof(TransactionRules).GetMethod("AdenaAmount")==null,"no adena-box rule");
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id==TransactionPanel.PanelId)==1,"TRANSACTION_WINDOW still registered once (auto-discovery)");
        t.Check(typeof(IUiClosablePanel).IsAssignableFrom(typeof(TransactionPanel))&&typeof(IUiTextInput).IsAssignableFrom(typeof(TransactionPanel)),"closable + text input (140 digits)");
        // 金幣框的位置（舊版 (82,124)–(166,138)）點下去不再有任何作用。
        var f=new FakeSource{Current=new TransactionView(1,"x","",[],true,true,false,Generation:1,CanAdd:true)};
        var p=Panel();p.Source=f;p.Update(InWorld);var cmd=new UiNullCommands();
        Click(p,new(100,130),cmd);
        t.Check(!p.Char('3')&&p.Prompt==null&&f.Calls.Count==0,"old adena box area: no focus, nothing sent");
    }

    static void LiveStack(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedTrade(sent);
        var p=Panel();p.Source=new LiveSource(c);var cmd=new UiNullCommands();p.Update(InWorld);
        c.World.SelectItem(Item);p.Update(InWorld);
        t.Check(!p.Char('3'),"digits before a prompt are not taken");
        Click(p,Own,cmd);
        t.Check(sent.Count==0&&p.Prompt is {Item:Item,Held:5,Name:"test item"},"stack → 140 prompt, nothing sent",p.Prompt);
        t.Equal(TradeAmountText.Ask(5),p.NoticeText,"text frame shows 140");
        t.Check(!p.Char('a')&&p.Char('3')&&p.PromptInput=="3","digits via WM_CHAR, letters refused",p.PromptInput);
        t.Check(Press(p,Keys.Enter,cmd)&&sent.Count==1&&sent[0].SequenceEqual(TradePackets.Add(Item,3)),"Enter → C37 item + typed amount",sent.Select(Convert.ToHexString));
        t.Check(p.Prompt==null&&p.PromptInput=="","prompt closed after answering");
        t.Equal("adding",c.World.TradeState,"trade waits for S35");
        Feed(c,Offer(0,"test item (3)"));p.Update(InWorld);
        t.Check(c.World.TradeState=="open"&&p.VisibleOffers(0).Single().Count==3,"S35 side 0 → listed with count 3");
    }

    static void LiveAdena(WorldModuleTestContext t){
        // 金幣：選背包裡的金幣，點自己的清單＝同一個 140 提示（FU9§12.1：沒有獨立的金幣輸入框）。
        var sent=new List<byte[]>();using var c=SeedTrade(sent);
        var p=Panel();p.Source=new LiveSource(c);var cmd=new UiNullCommands();
        c.World.SelectItem(Gold);p.Update(InWorld);
        Click(p,Own,cmd);
        t.Check(sent.Count==0&&p.Prompt is {Item:Gold,Held:1000},"adena stack → 140 prompt",p.Prompt);
        t.Check(p.NoticeText.Contains("1-1000"),"140 shows the held adena",p.NoticeText);
        t.Check(p.Char('0')&&p.PromptInput=="0"&&p.Char('3')&&p.PromptInput=="3","leading zero replaced",p.PromptInput);
        Type(p,"00");
        t.Check(Press(p,Keys.Back,cmd)&&p.PromptInput=="30","Backspace removes one digit",p.PromptInput);
        p.Char('0');
        t.Check(Press(p,Keys.Enter,cmd)&&sent.Count==1&&sent[0].SequenceEqual(TradePackets.Add(Gold,300)),"Enter → C37 adena + amount",sent.Select(Convert.ToHexString));
        Feed(c,Offer(0,"$4 (300)"));p.Update(InWorld);
        t.Equal(300L,p.View!.Adena(0),"S35 adena row counted in the own total");
        // 空白 Enter＝全部。
        Click(p,Own,cmd);
        t.Check(p.Prompt is {Item:Gold}&&Press(p,Keys.Enter,cmd)&&sent.Count==2&&sent[1].SequenceEqual(TradePackets.Add(Gold,1000)),"blank Enter → whole adena stack",sent.Select(Convert.ToHexString));
    }

    static void InvalidAndEscape(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedTrade(sent);
        var p=Panel();p.Source=new LiveSource(c);var cmd=new UiNullCommands();
        c.World.SelectItem(Gold);p.Update(InWorld);
        Click(p,Own,cmd);Type(p,"5000");
        t.Check(Press(p,Keys.Enter,cmd)&&sent.Count==0&&p.Prompt==null,"more than held → not sent, prompt ends");
        t.Check(p.Refusal is {} r&&r.Contains("數量不合法")&&p.NoticeText==r,"reason shown in the text frame",p.NoticeText);
        Click(p,Own,cmd);
        t.Check(p.Prompt!=null&&p.Refusal==null,"new prompt clears the reason");
        t.Check(Press(p,Keys.Escape,cmd)&&p.Prompt==null&&sent.Count==0&&c.World.TradeState=="open","Esc during 140 = cancel the prompt only (no C86)");
        t.Check(Press(p,Keys.Escape,cmd)&&sent.Count==1&&sent[0].SequenceEqual(TradePackets.Cancel()),"Esc without a prompt → C86 as before");
    }

    static void SingleAndLimit(WorldModuleTestContext t){
        // 單件直接送（不問 140）。
        {
            var sent=new List<byte[]>();using var c=SeedTrade(sent);
            var p=Panel();p.Source=new LiveSource(c);var cmd=new UiNullCommands();
            c.World.SelectItem(Sword);p.Update(InWorld);
            Click(p,Own,cmd);
            t.Check(p.Prompt==null&&sent.Count==1&&sent[0].SequenceEqual(TradePackets.Add(Sword,1)),"single item → C37 count 1 directly",sent.Select(Convert.ToHexString));
        }
        // 自己已放 16 件：不送、印 255；15 件還可以加。
        var own15=Enumerable.Range(0,15).Select(i=>new TransactionOffer(0,"物品"+i,0)).ToArray();
        var own16=own15.Append(new TransactionOffer(0,"物品15",0)).ToArray();
        var single=new TransactionItemChoice(Sword,"長劍",1,0);var stack=new TransactionItemChoice(Item,"test item",5,0);
        var f=new FakeSource{Current=new TransactionView(7,"x","",own15,true,true,false,Generation:2,CanAdd:true,Selected:single)};
        var q=Panel();q.Source=f;q.Update(InWorld);
        t.Check(q.AddSelected()&&f.Calls.SequenceEqual(["add:7:903:1"]),"15 placed → single item still sent",f.Calls);
        f.Current=f.Current with{Revision=8,Offers=own16};q.Update(InWorld);
        t.Check(!q.AddSelected()&&f.Calls.Count==1&&q.Refusal==TradeAmountText.TooMany()&&q.NoticeText==q.Refusal,"16 placed → not sent, 255 in the text frame",q.NoticeText);
        f.Current=f.Current with{Revision=9,Selected=stack};q.Update(InWorld);
        t.Check(q.Refusal==null,"new revision clears the reason");
        q.AddSelected();
        t.Check(q.Prompt!=null&&!q.SubmitAmount()&&f.Calls.Count==1&&q.Refusal==TradeAmountText.TooMany(),"16 placed: stack asks 140, the send step prints 255 (sub_82E870)");
    }

    static void PromptLifetime(WorldModuleTestContext t){
        var stack=new TransactionItemChoice(Item,"test item",5,0);
        var f=new FakeSource{Current=new TransactionView(1,"x","",[],true,true,false,Generation:3,CanAdd:true,Selected:stack)};
        var p=Panel();p.Source=f;p.Update(InWorld);var cmd=new UiNullCommands();
        t.Check(!p.AddSelected()&&p.Prompt is {Generation:3,Item:Item,Held:5},"AddSelected on a stack opens the prompt (false = not sent yet)");
        p.Char('2');
        f.Current=f.Current with{Revision=2};p.Update(InWorld);
        t.Check(p.Prompt!=null&&p.PromptInput=="2","peer adds an item (new revision): prompt kept");
        t.Check(p.SubmitAmount()&&f.Calls.SequenceEqual(["add:2:901:2"]),"answer uses the current revision",f.Calls);
        // 不能加（加入中／已確定）時提示作廢。
        p.AddSelected();f.Current=f.Current with{Revision=3,CanAdd=false};p.Update(InWorld);
        t.Check(p.Prompt==null&&!p.Char('1')&&!Press(p,Keys.Enter,cmd)&&f.Calls.Count==1,"CanAdd false drops the prompt, Enter not taken");
        // 換一筆交易（新 generation）時提示作廢。
        f.Current=f.Current with{Revision=4,CanAdd=true};p.Update(InWorld);p.AddSelected();
        f.Current=f.Current with{Revision=1,Generation=4};p.Update(InWorld);
        t.Check(p.Prompt==null,"new trade generation drops the prompt");
        // 關窗（IUiClosablePanel）清掉提示，送 C86。
        p.AddSelected();((IUiClosablePanel)p).Close();
        t.Check(p.Prompt==null&&!p.Visible&&f.Calls[^1]=="cancel:1","Close drops the prompt and sends C86",f.Calls);
        // 交易結束（沒有 view）。
        var g=new FakeSource{Current=f.Current with{Generation=5}};var r=Panel();r.Source=g;r.Update(InWorld);r.AddSelected();
        g.Current=null;r.Update(InWorld);
        t.Check(r.Prompt==null&&!r.Visible,"trade ends → prompt gone");
        // 沒有來源時不開提示。
        var s=Panel();s.Update(InWorld);t.Check(!s.AddSelected()&&s.Prompt==null,"no source → nothing");
    }
}
