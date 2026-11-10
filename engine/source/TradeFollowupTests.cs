using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R4-C（T303／#715）離線測試：TRANSACTION_WINDOW 的物品圖示格、從原生背包選取加物品（C37；R9-K 起堆疊先問 140，金幣框已移除）、
// IUiClosablePanel（交易窗送 C86 並讓位給 WinForms；TRADE_WINDOW 記下關閉的清單）、修理多選每列一包 C20（TWP§1.6）、
// 學魔法舊清單讓位的判斷。只用合成封包／XML／surf／圖示與記憶體送出端；不開遊戲、伺服器、DB，不讀客戶端資源。
static class TradeFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Peer=456,Item=901,Gold=902,Npc=456,WeaponA=911,WeaponB=912;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static byte[] Open(string peer)=>Packet(b=>{b.Write((byte)52);Text(b,peer);});
    static byte[] Offer(byte side,string name,ushort icon=7,byte bless=0)=>Packet(b=>{b.Write((byte)35);b.Write(side);b.Write(icon);Text(b,name);b.Write(bless);});
    /// <summary>S15 一件物品（欄位同 transaction-window／trade-window 測試）。</summary>
    static byte[] AddItem(uint id,ushort desc,byte use,ushort icon,uint count,string name)=>Packet(b=>{b.Write((byte)15);b.Write(id);b.Write(desc);b.Write(use);b.Write((byte)0);b.Write(icon);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,name);b.Write((byte)0);b.Write(new byte[11]);});
    static byte[] Npc87(uint id,string name)=>Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(id);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,name);Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);});
    static string Resolve(string s)=>s.Replace("$4","金幣");

    static LoginConnection World(Func<byte[],Func<bool>,Action,Task<bool>> sender){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static Func<byte[],Func<bool>,Action,Task<bool>> Recorder(List<byte[]> sent,Action<int>? after=null)=>(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);after?.Invoke(sent.Count);return Task.FromResult(true);};

    /// <summary>交易：玩家 "target" 相鄰；背包 5 個 test item 與 1000 金幣；已接受、等 S52。</summary>
    static LoginConnection SeedTrade(List<byte[]> sent){
        var c=World(Recorder(sent));
        Feed(c,Npc87(Peer,"target"));Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,AddItem(Item,0,1,7,5,"test item (5)"));Feed(c,AddItem(Gold,0,0,5,1000,"$4 (1000)"));
        Set(c.World,"tradeEpoch",c.World.WorldEpoch);Set(c.World,"TradePeerName","target");Set(c.World,"TradePeerId",(uint?)Peer);Set(c.World,"TradeState","accepting");
        return c;
    }
    sealed class LiveSource(LoginConnection c):ITransactionSource {
        public readonly List<string> Calls=[];
        public TransactionView? Current=>TransactionView.From(c.World,Resolve);
        bool Run(string cmd,int revision,uint item=0,uint count=0){Calls.Add($"{cmd}:{item}:{count}");return c.Trade(cmd,revision,c.World.WorldEpoch,c.World.TradeGeneration,item,count).GetAwaiter().GetResult();}
        public void Confirm(int revision)=>Run("confirm",revision);
        public void Cancel(int revision)=>Run("cancel",revision);
        public void Add(int revision,uint item,uint count)=>Run("add",revision,item,count);
    }
    static UiMouseEvent M(UiMouseKind kind,Point at)=>new(kind,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,Point at,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,at),cmd);p.Mouse(M(UiMouseKind.Up,at),cmd);}
    static Func<int,UiSurface?> Surfaces()=>LegacyWindowTestKit.Surfaces(new(){[2000]=(180,270),[2001]=(200,290),[1962]=(14,14),[2008]=(160,30),[2002]=(50,18),[2003]=(50,18),[2004]=(50,18),[2005]=(50,18),[2006]=(50,18),[2007]=(50,18)}).Get;
    static TransactionPanel Panel(ItemIconSource? icons=null){var p=new TransactionPanel(icons);p.Load(UiLayout.Parse(TransactionPanelTests.Xml,"uiextra86-c.xml"),Surfaces());return p;}
    static int IconColour(int icon)=>LegacyWindowTestKit.ColourOf(icon+50000);
    static ItemIconSource Icons(List<int> requested)=>new(icon=>{
        requested.Add(icon);
        var bmp=new Bitmap(ItemIconSource.IconSize,ItemIconSource.IconSize,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bmp))g.Clear(Color.FromArgb(IconColour(icon)));
        return new ItemIcon(icon,bmp,Point.Empty,"synthetic");
    });

    [WorldModuleTest("trade-followup")]
    static void Checks(WorldModuleTestContext t){
        // R9-K（#855）：金幣框已移除（原 AdenaBox 測試），數量提示見 TradeAmountTests。
        Contracts(t);Rules(t);AddFromInventory(t);CloseTransaction(t);IconCells(t);CloseTradeWindow(t);RepairBatch(t);SpellYield(t);
    }

    static void Contracts(WorldModuleTestContext t){
        t.Check(typeof(IUiClosablePanel).IsAssignableFrom(typeof(TransactionPanel))&&typeof(IUiTextInput).IsAssignableFrom(typeof(TransactionPanel)),"TransactionPanel: closable + text input (140 amount prompt)");
        t.Check(typeof(IUiClosablePanel).IsAssignableFrom(typeof(TradeWindowPanel)),"TradeWindowPanel: closable");
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id==TransactionPanel.PanelId)==1&&UiPanelRegistry.Panels.Count(p=>p.Id==TradeWindowPanel.PanelId)==1,"both panels still registered once");
    }

    static void Rules(WorldModuleTestContext t){
        t.Equal(5u,TransactionRules.Count("test item (5)"),"count suffix");
        t.Equal(1u,TransactionRules.Count("長劍"),"no suffix → 1");
        t.Equal(1u,TransactionRules.Count("x (0)"),"zero suffix → 1");
        t.Equal("test item",new TransactionOffer(0,"test item (5)",0).BareName,"bare name drops the count");
    }

    static void AddFromInventory(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedTrade(sent);Feed(c,Open("target"));
        var v=TransactionView.From(c.World,Resolve)!;
        t.Check(v.CanAdd&&v.Selected==null&&v.Generation==c.World.TradeGeneration,"view: can add, nothing selected",v);
        c.World.SelectItem(Item);
        v=TransactionView.From(c.World,Resolve)!;
        t.Check(v.Selected is {Id:Item,Count:5,Name:"test item"},"native inventory selection (WorldSession.SelectedItemId) → Selected",v.Selected);

        var p=Panel();var src=new LiveSource(c);p.Source=src;var world=new UiStaticGameState(InWorld:true);var cmd=new UiNullCommands();
        p.Update(world);
        p.Mouse(M(UiMouseKind.Move,new(60,60)),cmd);
        t.Check(p.NoticeText.Contains("test item")&&p.NoticeText.Contains("5"),"hover own list with a selection → text frame says what will be added",p.NoticeText);
        Click(p,new(60,60),cmd);
        // R9-K（#855）：堆疊先問 140，空白 Enter＝全部。
        t.Check(sent.Count==0&&p.Prompt is {Item:Item,Held:5},"click own list with a stack → 140 prompt first, nothing sent",p.Prompt);
        p.Key(new UiKeyEvent(Keys.Enter,true,false),cmd);
        t.Check(sent.Count==1&&sent[0].SequenceEqual(TradePackets.Add(Item,5))&&sent[0][0]==37,"blank Enter → C37 selected item, whole stack",sent.Select(Convert.ToHexString));
        t.Equal("adding",c.World.TradeState,"trade waits for S35");
        p.Update(world);Click(p,new(60,60),cmd);
        t.Equal(1,sent.Count,"while adding (CanAdd false) a second click sends nothing");
        Feed(c,Offer(0,"test item (5)"));p.Update(world);
        t.Check(c.World.TradeState=="open"&&p.VisibleOffers(0).Single().Name=="test item (5)","S35 side 0 → open again, row listed");
        Click(p,new(60,150),cmd);
        t.Equal(1,sent.Count,"click on the peer list adds nothing");
        c.World.SelectItem(null);p.Update(world);Click(p,new(60,60),cmd);
        t.Equal(1,sent.Count,"no selection → nothing sent");
        t.Check(!p.AddSelected(),"AddSelected without selection → false");
    }

    static void CloseTransaction(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedTrade(sent);Feed(c,Open("target"));
        var p=Panel();var src=new LiveSource(c);p.Source=src;var world=new UiStaticGameState(InWorld:true);p.Update(world);
        t.Check(p.Visible&&!p.Dismissed,"open trade visible");
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&p.Dismissed&&sent.Single().SequenceEqual(TradePackets.Cancel()),"Close (UiRoot eviction) → C86 once, hidden",sent.Select(Convert.ToHexString));
        t.Equal("cancelling",c.World.TradeState,"trade state still waits for S112");
        p.Update(world);
        t.Check(!p.Visible&&p.Dismissed,"same trade (generation) stays hidden → TradeUi shows the WinForms panel");
        p.Close();t.Equal(1,sent.Count,"closing a hidden window sends nothing");
        Feed(c,LoginPackets.Pad([112,1]));p.Update(world);
        t.Check(!p.Visible&&!p.Dismissed,"S112 ends the trade → dismissal cleared");
        // 新的一筆交易（新 generation）照常顯示。
        c.World.ResetTrade();Set(c.World,"tradeEpoch",c.World.WorldEpoch);Set(c.World,"TradePeerName","target");Set(c.World,"TradePeerId",(uint?)Peer);Set(c.World,"TradeState","accepting");
        Feed(c,Open("target"));p.Update(world);
        t.Check(p.Visible,"next trade shows the native window again");

        // 取消中（CanCancel=false）被關：不再送，只隱藏。
        var fake=new FakeSource{Current=new TransactionView(3,"x","",[],false,false,false,Generation:9)};
        var q=Panel();q.Source=fake;q.Update(world);q.Close();
        t.Check(!q.Visible&&fake.Calls.Count==0,"close while cancelling → no packet");
        fake.Current=fake.Current with{Revision=4};q.Update(world);
        t.Check(!q.Visible,"later revisions of the same generation stay hidden");
        fake.Current=null;q.Update(world);fake.Current=new TransactionView(1,"x","",[],true,true,false,Generation:10);q.Update(world);
        t.Check(q.Visible,"new generation visible");
    }
    sealed class FakeSource:ITransactionSource {
        public TransactionView? Current{get;set;}
        public readonly List<string> Calls=[];
        public void Confirm(int revision)=>Calls.Add("confirm:"+revision);
        public void Cancel(int revision)=>Calls.Add("cancel:"+revision);
        public void Add(int revision,uint item,uint count)=>Calls.Add($"add:{revision}:{item}:{count}");
    }

    static void IconCells(WorldModuleTestContext t){
        var requested=new List<int>();var p=Panel(Icons(requested));
        t.Check(p.IconMode,"icon source → icon cells");
        t.Check(p.Grid(0) is {Columns:4,Rows:2}&&p.Grid(1) is {Columns:4,Rows:2},"33 px cells: 4 × 2 per list (LegacyCellGrid.Fit, 推測)",p.Grid(0));
        t.Equal(new Rectangle(30,53,32,32),p.CellRect(0,0),"first own cell centred in the own list area");
        t.Check(!Panel().IconMode,"no icon source → text rows (R3-E)");

        var fake=new FakeSource();p.Source=fake;var world=new UiStaticGameState(InWorld:true);var cmd=new UiNullCommands();
        var offers=Enumerable.Range(0,10).Select(i=>new TransactionOffer(0,"物品"+i+(i==9?" (12)":""),0,100+i)).Append(new TransactionOffer(1,"藥水",0,300)).ToArray();
        fake.Current=new TransactionView(1,"target","提示",offers[..1],true,true,false,CanAdd:true);p.Update(world);
        fake.Current=fake.Current with{Revision=2,Offers=offers};p.Update(world);
        t.Check(p.Scroll(0)==1&&p.VisibleOffers(0).Count==6&&p.VisibleOffers(0)[0].Name=="物品4","10 own items: 3 grid rows, 2 visible → scrolled to the last page (by row of 4)",p.VisibleOffers(0).Select(o=>o.Name));
        t.Check(p.Mouse(new UiMouseEvent(UiMouseKind.Wheel,new(60,60),MouseButtons.None,0,120,Keys.None),cmd)&&p.Scroll(0)==0&&p.VisibleOffers(0)[0].Name=="物品0","wheel scrolls one grid row");
        p.Mouse(new UiMouseEvent(UiMouseKind.Wheel,new(60,60),MouseButtons.None,0,-120,Keys.None),cmd);
        p.Mouse(M(UiMouseKind.Move,new(46+33,69)),cmd);
        t.Check(p.HoverOffer?.Name=="物品5"&&p.NoticeText=="物品5","hover a cell → its name in the text frame",p.NoticeText);
        p.Mouse(M(UiMouseKind.Move,new(46,69+91)),cmd);
        t.Equal("藥水",p.HoverOffer?.Name,"peer cell hover");
        p.Mouse(M(UiMouseKind.Move,new(5,5)),cmd);
        t.Equal("提示",p.NoticeText,"no hover → notice");
        using(var canvas=new UiCanvas()){
            var root=new UiRoot();root.Add(p);root.Update(world);root.Compose(canvas,null);
            t.Check(!root.Faulted.Contains(p),"icon paint does not fault");
            t.Equal(IconColour(104),canvas.Bitmap.GetPixel(30+10,53+10).ToArgb(),"own slot 0 = icon of 物品4");
            t.Equal(IconColour(105),canvas.Bitmap.GetPixel(30+33+10,53+10).ToArgb(),"own slot 1 = icon of 物品5");
            t.Equal(IconColour(300),canvas.Bitmap.GetPixel(30+10,53+91+10).ToArgb(),"peer slot 0 = icon of 藥水");
        }
        t.Check(requested.Contains(109),"stack row icon requested too");
        // S35 icon＋bless → FU5§4 IconOf（487＋bless 0 → 1617）。
        var sent=new List<byte[]>();using var c=SeedTrade(sent);Feed(c,Open("target"));Feed(c,Offer(1,"x",487,0));Feed(c,Offer(1,"y",487,1));
        var v=TransactionView.From(c.World,Resolve)!;
        t.Check(v.Side(1).Select(o=>o.Icon).SequenceEqual(new[]{1617,487}),"S35 icon through ItemIconSource.IconOf",v.Offers);
    }

    sealed class FakeTrade:ITradeWindowSource {
        public TradeListView? Current{get;set;}
        public int Closed;
        public void Submit(IReadOnlyList<ShopChoice> choices,int revision){}
        public void Close()=>Closed++;
    }
    static void CloseTradeWindow(WorldModuleTestContext t){
        var src=new FakeTrade{Current=new TradeListView(6,3,[new TradeListItem(1,"劍",0,200,1,true)],1000,true,true,"")};
        var p=new TradeWindowPanel{Source=src};var world=new UiStaticGameState(InWorld:true);
        p.Update(world);t.Check(p.Visible,"repair list shown");
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&src.Closed==1&&p.Dismissed(src.Current),"Close (UiRoot eviction) → source closed once, list remembered as closed");
        p.Update(world);t.Check(!p.Visible,"same (mode, revision) stays hidden");
        src.Current=src.Current with{Revision=4};p.Update(world);
        t.Check(p.Visible&&!p.Dismissed(src.Current),"new revision shows again");
    }

    /// <summary>修理：NPC 相鄰、兩把武器；對話頁 fix → C118 → S83 兩列。</summary>
    static LoginConnection SeedRepair(List<byte[]> sent,Action<int>? after=null){
        var c=World(Recorder(sent,after));
        Feed(c,Npc87(Npc,"NPC"));c.World.SelectTarget(Npc);Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,AddItem(WeaponA,0,1,0,1,"weapon a"));Feed(c,AddItem(WeaponB,0,1,0,1,"weapon b"));
        c.WireWeaponRepairDialog();c.World.WeaponRepairNativeWindow=true;
        c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision).GetAwaiter().GetResult();
        Feed(c,Packet(b=>{b.Write((byte)39);b.Write(Npc);Text(b,"touma1");b.Write((ushort)0);b.Write((ushort)0);}));
        Set(c.World,"InteractionPage",new NpcPage("修理武器嗎？",[new NpcOption(1,"修理","action",WeaponRepairPackets.FixAction,true)],""));
        c.Interact("option",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision,1).GetAwaiter().GetResult();
        Feed(c,Packet(b=>{b.Write((byte)83);b.Write(200u);b.Write((ushort)2);b.Write(WeaponA);b.Write((byte)3);b.Write(WeaponB);b.Write((byte)2);}));
        return c;
    }
    static void RepairBatch(WorldModuleTestContext t){
        t.Check(RepairTrade.Items([new(WeaponA,1),new(0,1),new(WeaponA,1),new(WeaponB,0),new(WeaponB,1)]).SequenceEqual(new[]{WeaponA,WeaponB}),"choices → distinct non-zero items with a quantity");

        var sent=new List<byte[]>();using var c=SeedRepair(sent);
        var view=RepairTrade.View(c.World);
        t.Check(view is {Mode:6,Items.Length:2}&&view.Items.All(i=>i.Enabled),"seed: TRADE_WINDOW mode 6 with two repairable rows",view);
        int before=sent.Count;
        t.Check(c.SendWeaponRepairBatch([WeaponA,WeaponB],view!.Revision).GetAwaiter().GetResult(),"batch accepted");
        t.Check(sent.Count==before+2&&sent[before].SequenceEqual(WeaponRepairPackets.Repair(WeaponA,Npc))&&sent[before+1].SequenceEqual(WeaponRepairPackets.Repair(WeaponB,Npc)),"one C20 per row, in row order (TWP§1.6)",sent.Skip(before).Select(Convert.ToHexString));
        t.Check(c.World.WeaponRepairPhase=="submitted"&&c.World.WeaponRepairBatchProgress==(2,2)&&RepairTrade.View(c.World)==null,"submitted; progress 2/2; window closes",c.World.WeaponRepairBatchProgress);
        t.Check(!c.SendWeaponRepairBatch([WeaponA,WeaponB],view.Revision).GetAwaiter().GetResult()&&sent.Count==before+2,"stale revision → nothing resent");

        // 原服在第一包後就回 464（轉 review）：剩下的包照原版一次送完。
        var sent2=new List<byte[]>();LoginConnection? d=null;int mark=-1;
        d=SeedRepair(sent2,n=>{if(n==mark+1)Set(d!.World,"LastServerReply",new ServerMessageReport(464,[],"修理完成",true));});
        using(d){
            var v2=RepairTrade.View(d.World)!;mark=sent2.Count;
            t.Check(d.SendWeaponRepairBatch([WeaponA,WeaponB],v2.Revision).GetAwaiter().GetResult()&&sent2.Count==mark+2,"server reply between packets does not stop the batch",sent2.Skip(mark).Select(Convert.ToHexString));
            t.Equal("review",d.World.WeaponRepairPhase,"reply moved the list to review");
        }

        // 清單在中途關閉（換目標、交易等由 CheckWeaponRepair 關）：後面的包不送。
        var sent3=new List<byte[]>();LoginConnection? e=null;int mark3=-1;
        e=SeedRepair(sent3,n=>{if(n==mark3+1)e!.World.CloseWeaponRepair();});
        using(e){
            var v3=RepairTrade.View(e.World)!;mark3=sent3.Count;
            t.Check(!e.SendWeaponRepairBatch([WeaponA,WeaponB],v3.Revision).GetAwaiter().GetResult()&&sent3.Count==mark3+1,"closed mid-batch → remaining C20 not sent");
        }

        // 一列：照舊單件路徑；不可修的物品讓整批不送。
        var sent4=new List<byte[]>();using var g=SeedRepair(sent4);var v4=RepairTrade.View(g.World)!;int b4=sent4.Count;
        t.Check(!g.SendWeaponRepairBatch([WeaponA,999],v4.Revision).GetAwaiter().GetResult()&&sent4.Count==b4,"a row that cannot be repaired → whole batch refused, nothing sent");
        t.Check(g.SendWeaponRepairBatch([WeaponB],v4.Revision).GetAwaiter().GetResult()&&sent4.Count==b4+1&&sent4[^1].SequenceEqual(WeaponRepairPackets.Repair(WeaponB,Npc))&&g.World.WeaponRepairBatchProgress==(0,0),"single row = the one-item path");
    }

    static void SpellYield(WorldModuleTestContext t){
        TradeListView V(int revision)=>new(TradeWindowPanel.ModeSpell,revision,[],1000,true,true,"");
        var buy=V(SpellTradeProtocol.BuyRevision(5));
        t.Check(SpellTradeOwnership.Owned(buy,buy,_=>false),"native window shows the op 44 list → NPC panel yields");
        t.Check(!SpellTradeOwnership.Owned(buy,buy,_=>true),"list closed in the native window → NPC panel shown again");
        t.Check(!SpellTradeOwnership.Owned(null,buy,_=>false)&&!SpellTradeOwnership.Owned(buy,null,_=>false),"no list → no yield");
        var exchange=V(SpellTradeProtocol.ExchangeRevision(3));
        t.Check(!SpellTradeOwnership.Owned(exchange,buy,_=>false),"op 175 list in front → op 44 handling unchanged");
        t.Check(!SpellTradeOwnership.Owned(V(SpellTradeProtocol.BuyRevision(4)),buy,_=>false),"different list revision → no yield");
        t.Check(!SpellTradeOwnership.Owned(buy with{Mode=TradeWindowPanel.ModeBuy},buy,_=>false),"not mode 7 → no yield");
        var form=typeof(ClientForm);
        t.Check(form.GetMethod("SpellBuyOwnedByNativeWindow",F)!=null,"ClientForm wires the yield in SyncSpellExchangeWindow");
    }
}
