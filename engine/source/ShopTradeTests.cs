using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R2-E（T262／#623）離線測試：合成封包＋合成 XML／圖（不讀真實客戶端），記憶體內假傳送端；不開遊戲、網路、視窗或素材。
// 涵蓋：S70／S65 → TRADE_WINDOW 模式 0／1 → C161；舊清單在原生窗接上時讓位；修理列上限＝背包數量；面板版面、按鈕兩態、數量規則。
static class ShopTradeTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Npc=456,Weapon=901,Arrow=902,Gold=903;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    // S15 欄位順序同 Inventory.ReadItem：D id, H desc, C use, C charge, H icon, C bless, D count, C flags, S name, C statusLen, 11 bytes。
    static byte[] Item(uint id,byte use,ushort icon,uint count,string name)=>Packet(b=>{b.Write((byte)15);b.Write(id);b.Write((ushort)0);b.Write(use);b.Write((byte)0);b.Write(icon);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,name);b.Write((byte)0);b.Write(new byte[11]);});
    // S70：D npc, H n, n×(D id, H icon, D price, S name, C statusLen＋bytes), H currency（n>0 時）。
    static byte[] BuyList(ushort currency,params (uint id,ushort icon,uint price,string name)[] rows)=>Packet(b=>{b.Write((byte)70);b.Write(Npc);b.Write((ushort)rows.Length);
        foreach(var r in rows){b.Write(r.id);b.Write(r.icon);b.Write(r.price);Text(b,r.name);b.Write((byte)0);}if(rows.Length>0)b.Write(currency);});
    // S65：D npc, H n, n×(D 物品, D 單價)。
    static byte[] SellList(params (uint id,uint price)[] rows)=>Packet(b=>{b.Write((byte)65);b.Write(Npc);b.Write((ushort)rows.Length);foreach(var r in rows){b.Write(r.id);b.Write(r.price);}});
    static byte[] RepairList(params (uint id,byte damage)[] rows)=>Packet(b=>{b.Write((byte)83);b.Write(200u);b.Write((ushort)rows.Length);foreach(var (id,damage) in rows){b.Write(id);b.Write(damage);}});
    static byte[] Dialog(string html)=>Packet(b=>{b.Write((byte)39);b.Write(Npc);Text(b,html);b.Write((ushort)0);b.Write((ushort)0);});
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Npc);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,"NPC");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
        c.World.SelectTarget(Npc);Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,Item(Weapon,1,0,1,"test weapon"));Feed(c,Item(Arrow,0,7,50,"arrow"));Feed(c,Item(Gold,0,5,12345,"$4"));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Talk(LoginConnection c)=>c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision).GetAwaiter().GetResult();
    static void Close(LoginConnection c)=>c.Interact("close",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision).GetAwaiter().GetResult();

    [WorldModuleTest("shop-trade")]
    static void Run(WorldModuleTestContext t){
        Shop(t);Repair(t);Panel(t);
    }

    static void Shop(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;
        t.Equal<long?>(12345,ShopTrade.Holding(w),"holding = inventory adena");
        var src=new ShopTradeSource(c,s=>"<"+s+">",()=>false);

        // 1. 原生窗未接：照舊給舊清單，TRADE_WINDOW 不開。
        t.Check(Talk(c),"talk sent");
        Feed(c,BuyList(7,(11,3,500,"potion")));
        t.Equal("buy",w.InteractionKind,"S70 opens buy list");
        t.Check(w.ShopRows.Length==1&&w.CanSubmitShop,"legacy list keeps rows and submit");
        t.Check(w.ShopTradeWindow()==null&&src.Current==null,"legacy: no TRADE_WINDOW view");
        Close(c);

        // 2. 原生窗接上：舊清單讓位，TRADE_WINDOW 模式 0，單筆上限依單價（TWP§1.5）。
        w.ShopNativeWindow=true;
        t.Check(Talk(c),"talk again");
        Feed(c,BuyList(7,(11,3,500,"potion"),(12,4,5000,"scroll"),(13,5,100000,"sword"),(14,6,5000000,"armor")));
        t.Check(w.ShopRows.Length==0&&!w.CanSubmitShop,"native: legacy rows hidden, legacy submit disabled");
        var view=src.Current;
        t.Check(view is {Mode:TradeWindowPanel.ModeBuy,ShowPrice:true,CanSubmit:true,Holding:12345}&&view.Revision==w.InteractionRevision,"S70 → TRADE_WINDOW mode 0",view);
        t.Equal("9999,999,20,1",string.Join(",",view!.Items.Select(i=>i.Maximum)),"buy limits by price");
        t.Check(view.Items[0] is {Id:11,Icon:3,Price:500,Name:"<potion>",Enabled:true},"buy row fields and resolved name",view.Items[0]);
        t.Equal(1u,TradeWindowPanel.BuyLimit(10000000),"≥ 1000 萬 limit 1");t.Equal(9999u,TradeWindowPanel.BuyLimit(999),"< 1000 limit 9999");t.Equal(20u,TradeWindowPanel.BuyLimit(2999999),"< 300 萬 limit 20");

        // 3. 送出：一包 C161（type 0、多筆），之後窗關（對話進入 submitted）。
        int before=sent.Count;src.Submit([new(11,3),new(13,2)],view.Revision);
        t.Equal(before+1,sent.Count,"one C161");
        t.Equal(Convert.ToHexString(NpcPackets.Shop(Npc,false,[new(11,3),new(13,2)])),Convert.ToHexString(sent[^1]),"C161 buy: npc, type 0, rows");
        t.Equal("submitted",w.InteractionKind,"buy submitted");
        t.Check(src.Current==null,"window closes after submit");
        src.Submit([new(11,1)],view.Revision);t.Equal(before+1,sent.Count,"stale revision never resends");
        Close(c);

        // 4. 貨幣不是金幣：可看不可送，不畫合計。
        t.Check(Talk(c),"talk for other currency");Feed(c,BuyList(9,(15,3,10,"token item")));
        t.Check(src.Current is {CanSubmit:false,Holding:null},"non-adena currency: submit disabled, no total",src.Current);
        before=sent.Count;src.Submit([new(15,1)],w.InteractionRevision);t.Equal(before,sent.Count,"non-adena currency never sends");
        Close(c);

        // 5. 空清單：不開窗。
        t.Check(Talk(c),"talk for empty");Feed(c,BuyList(7));t.Check(src.Current==null,"empty buy list → no window");Close(c);

        // 6. 賣：S65 → 模式 1，上限＝背包數量；送出走 ValidateService，C161 type 1。不跳確認（FU3§6.1）。
        t.Check(Talk(c),"talk for sell");Feed(c,SellList((Arrow,4),(Weapon,100)));
        t.Equal("sell",w.InteractionKind,"S65 opens sell list");
        t.Check(w.ShopRows.Length==0&&!w.CanSubmitService,"native: legacy sell rows hidden");
        view=src.Current;
        t.Check(view is {Mode:TradeWindowPanel.ModeSell,CanSubmit:true}&&view.Items.Length==2,"S65 → TRADE_WINDOW mode 1",view);
        t.Check(view!.Items[0] is {Id:Arrow,Icon:7,Price:4,Maximum:50,Name:"<arrow>"},"sell row: inventory icon/name, max = count",view.Items[0]);
        before=sent.Count;src.Submit([new(Arrow,50)],view.Revision);
        t.Equal(Convert.ToHexString(NpcPackets.Shop(Npc,true,[new(Arrow,50)])),Convert.ToHexString(sent[^1]),"C161 sell: type 1");
        t.Equal(before+1,sent.Count,"one sell packet");
        Close(c);

        // 7. 關閉：走 Interact close。
        t.Check(Talk(c),"talk for close");Feed(c,BuyList(7,(11,3,500,"potion")));t.Check(src.Current!=null,"view before close");
        src.Close();t.Equal("closed",w.InteractionKind,"source close ends interaction");t.Check(src.Current==null,"no view after close");

        // 8. 多來源：取第一個有內容的來源。
        var a=new FakeSource();var b=new FakeSource{Current=Sample(0,3,1)};var both=new TradeWindowSources(a,b);
        t.Check(both.Current?.Revision==3,"composite picks the source with content");both.Submit([new(1,1)],3);both.Close();
        t.Check(a.Calls.Count==0&&b.Calls.SequenceEqual(new[]{"submit:1x1:3","close"}),"composite forwards to the active source",b.Calls);
    }

    static void Repair(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;
        c.WireWeaponRepairDialog();w.WeaponRepairNativeWindow=true;
        t.Check(Talk(c),"talk");Feed(c,Dialog("touma1"));
        Set(w,"InteractionPage",new NpcPage("修理武器嗎？",[new NpcOption(1,"修理","action",WeaponRepairPackets.FixAction,true)],""));
        t.Check(c.Interact("option",w.InteractionRevision,w.WorldEpoch,w.TargetRevision,1).GetAwaiter().GetResult(),"fix option");
        Feed(c,RepairList((Weapon,3)));
        var view=RepairTrade.View(w);
        t.Check(view is {Mode:TradeWindowPanel.ModeRepair,CanSubmit:true,Holding:12345}&&view.Items.Single() is {Id:Weapon,Price:600,Maximum:1,Enabled:true},"repair → mode 6, cost 600, max = inventory count 1 (FU3§6.2)",view);
    }

    // ---- 面板：合成 XML（窗原點刻意與真實檔不同；元件座標照 TWP§1.3）與合成圖 ----
    const string Xml="""
<root>
<OTHER_WINDOW x="1" y="1"/>
<TRADE_WINDOW x="100" y="50">
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_TRADE_BASE" surfNumber="1983" xPos="6" yPos="9"/>
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_TRADE_FRAME" surfNumber="1984"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_QUIT_X" surfNumber="1962" xPos="225" yPos="15"/>
	<IMAGE surfName="SURF_RENEWAL_WIN_SCROLLBAR_LONG" surfNumber="1970" xPos="217" yPos="51"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber="2040" xPos="217" yPos="275"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber="2042" xPos="217" yPos="37"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber="2044"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_TRADE_UP_OFF" surfNumber="1985"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_TRADE_DOWN_OFF" surfNumber="1987"/>
	<IMAGE surfName="SURF_RENEWAL_IMG_TRADE_ADENA" surfNumber="1990" xPos="19" yPos="23"/>
	<IMAGE surfName="SURF_RENEWAL_IMG_TRADE_TOTAL" surfNumber="1991" xPos="19" yPos="23"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_TRADE_BUY_OFF" surfNumber="1992" xPos="60" yPos="292"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_TRADE_CANCEL_OFF" surfNumber="1994" xPos="127" yPos="292"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_TRADE_OK_OFF" surfNumber="1996" xPos="60" yPos="292"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_TRADE_SELL_OFF" surfNumber="1998" xPos="60" yPos="292"/>
</TRADE_WINDOW>
</root>
""";
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    // 框：只有 1 px 邊是不透明，內部透明（底圖才看得到）。
    static UiSurface FrameSurf(int w,int h)=>new(1984,"1984.img","synthetic",new UiImage(w,h,0,0,Enumerable.Range(0,w*h).Select(i=>i%w==0||i%w==w-1||i/w==0||i/w==h-1?0xFF00FF00u:0u).ToArray()));
    static uint Color(int id)=>0xFF000000u|(uint)(id*2654435761u&0x00FFFFFF);
    static Func<int,UiSurface?> Surfaces(bool noFrame=false)=>id=>id switch{
        1983=>Surf(id,227,304,0xFF204080),1984=>noFrame?null:FrameSurf(239,317),1962 or 1963=>Surf(id,8,8,Color(id)),1970=>Surf(id,12,224,Color(id)),
        2040 or 2041 or 2042 or 2043=>Surf(id,12,14,Color(id)),2044 or 2045=>Surf(id,11,16,Color(id)),1985 or 1987 or 555 or 557=>Surf(id,7,7,Color(id)),
        1990=>Surf(id,8,10,Color(id)),1991=>Surf(id,36,10,Color(id)),>=1992 and <=1999=>Surf(id,53,17,Color(id)),24=>Surf(id,12,12,Color(id)),_=>null};
    sealed class FakeSource:ITradeWindowSource {
        public TradeListView? Current{get;set;}
        public List<string> Calls{get;}=[];
        public void Submit(IReadOnlyList<ShopChoice> choices,int revision)=>Calls.Add($"submit:{string.Join(",",choices.Select(c=>$"{c.Id}x{c.Count}"))}:{revision}");
        public void Close()=>Calls.Add("close");
    }
    static TradeListView Sample(int mode,int revision,int rows,int disabled=-1,bool canSubmit=true,long? holding=1000)=>
        new(mode,revision,Enumerable.Range(0,rows).Select(i=>new TradeListItem((uint)(1000+i),"item"+i,0,(uint)(100*(i+1)),i==0?3u:5u,i!=disabled)).ToArray(),holding,true,canSubmit,"");
    static UiMouseEvent M(UiMouseKind kind,int x,int y)=>new(kind,new(x,y),MouseButtons.Left,1,0,Keys.None);
    static void Click(TradeWindowPanel p,Point at,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,at.X,at.Y),cmd);p.Mouse(M(UiMouseKind.Up,at.X,at.Y),cmd);}
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static int Argb(UiCanvas canvas,int x,int y)=>canvas.Bitmap.GetPixel(x,y).ToArgb();

    static void Panel(WorldModuleTestContext t){
        var layout=UiLayout.Parse(Xml,"synthetic-uiextra86.xml");
        var p=new TradeWindowPanel();p.Load(layout,Surfaces());
        t.Equal(new Point(100,50),p.Origin,"origin from TRADE_WINDOW x/y");
        t.Equal(new Rectangle(100,50,239,317),p.Bounds,"window size = frame 1984 (239×317) at (0,0)");
        t.Check(p.Frame!.Offset==Point.Empty&&p.Knob!.Offset==Point.Empty&&p.Plus!.Drawn,"frame/knob/±: elements without coordinates loaded");
        t.Equal(1993,p.Buy!.Pressed!.Surf,"pressed image = surf n+1");
        var noFrame=new TradeWindowPanel();noFrame.Load(layout,Surfaces(noFrame:true));
        t.Equal(new Rectangle(106,59,227,304),noFrame.Bounds,"missing frame → union of drawn elements");
        t.Throws<UiLayoutException>(()=>new TradeWindowPanel().Load(UiLayout.Parse(Xml.Replace("SURF_RENEWAL_BTN_TRADE_SELL_OFF","X"),"a.xml"),Surfaces()),"missing SELL element throws");
        t.Throws<UiLayoutException>(()=>new TradeWindowPanel().Load(UiLayout.Parse(Xml.Replace(" xPos=\"127\" yPos=\"292\"",""),"b.xml"),Surfaces()),"positioned element without coordinates throws");

        var src=new FakeSource();p.Source=src;var cmd=new UiNullCommands();var world=new UiStaticGameState(InWorld:true);
        p.Update(world);t.Check(!p.Visible,"no view → hidden");
        // 左鈕依模式（TWP§1.2）。
        foreach(var (mode,surf) in new[]{(0,1992),(1,1998),(6,1996),(7,1992)}){src.Current=Sample(mode,100+mode,2);p.Update(world);t.Equal<int?>(surf,p.LeftButton?.Surf,$"mode {mode} left button");}

        src.Current=Sample(0,5,10,disabled:1);p.Update(world);
        t.Check(p.Visible&&p.Quantities.Count==10&&p.Quantities.All(q=>q==0),"new list: all quantities 0");
        // 列版面：列原點 (19, 38+36k)；點選用 ListBox (16,34) 格高 37。
        t.Equal(new Point(119,88),p.RowOrigin(0),"row 0 origin");t.Equal(new Point(119,88+36*6),p.RowOrigin(6),"row 6 origin");
        t.Equal(0,p.RowAt(new(116,84)),"ListBox top-left → row 0");t.Equal(-1,p.RowAt(new(116,83)),"above ListBox → none");
        t.Equal(1,p.RowAt(new(150,84+37)),"y 34+37 → row 1 (pitch 37)");t.Equal(-1,p.RowAt(new(313,90)),"x ≥ 213 → none");
        var (minus0,plus0)=p.RowButtons(0);
        t.Equal(new Rectangle(119+180,88+19,12,12),plus0,"＋ at (180,19) of row, 12×12");
        t.Equal(119+176-TradeWindowPanel.TextWidth("0")-13,minus0.X,"－ at quantity left − 13");

        // ＋／－：放開才算；＋到上限回 0；－到 0 跳上限（row 0 上限 3）。
        p.Mouse(M(UiMouseKind.Down,plus0.X+1,plus0.Y+1),cmd);t.Equal(0u,p.Quantities[0],"press alone does not add");
        p.Mouse(M(UiMouseKind.Up,plus0.X+1,plus0.Y+1),cmd);t.Equal(1u,p.Quantities[0],"release inside adds 1");
        p.Mouse(M(UiMouseKind.Down,plus0.X+1,plus0.Y+1),cmd);p.Mouse(M(UiMouseKind.Move,10,10),cmd);p.Mouse(M(UiMouseKind.Up,10,10),cmd);
        t.Equal(1u,p.Quantities[0],"release outside cancels");
        Click(p,Center(p.RowButtons(0).Plus),cmd);Click(p,Center(p.RowButtons(0).Plus),cmd);t.Equal(3u,p.Quantities[0],"up to max 3");
        Click(p,Center(p.RowButtons(0).Plus),cmd);t.Equal(0u,p.Quantities[0],"＋ past max wraps to 0");
        Click(p,Center(p.RowButtons(0).Minus),cmd);t.Equal(3u,p.Quantities[0],"－ at 0 jumps to max");
        Click(p,Center(p.RowButtons(1).Plus),cmd);t.Equal(0u,p.Quantities[1],"disabled row ignores ＋");

        // 鍵盤：選列後數字 ×10＋數字（夾到上限）、Backspace ÷10。
        p.Mouse(M(UiMouseKind.Down,150,84+37*2+5),cmd);t.Equal(2,p.Selected,"click selects row 2");
        t.Check(p.Key(new(Keys.D4,true,false),cmd)&&p.Quantities[2]==4,"digit 4 → 4");
        p.Key(new(Keys.NumPad7,true,false),cmd);t.Equal(5u,p.Quantities[2],"47 clamps to max 5");
        p.Key(new(Keys.Back,true,false),cmd);t.Equal(0u,p.Quantities[2],"backspace /10");
        p.Key(new(Keys.D2,true,false),cmd);
        t.Equal(3*100+2*300L,p.TotalPrice,"total = Σ quantity × price");

        // 左鈕：按下畫 n+1，放開在鈕內送出所有數量 >0 的列。
        var buy=new Rectangle(160,342,53,17);
        p.Mouse(M(UiMouseKind.Down,170,350),cmd);
        using(var canvas=new UiCanvas()){canvas.Clear(System.Drawing.Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            t.Equal((int)Color(1993),Argb(canvas,buy.X+1,buy.Y+1),"pressed BUY draws 1993");}
        t.Check(src.Calls.Count==0,"no submit on press");
        p.Mouse(M(UiMouseKind.Up,170,350),cmd);
        t.Equal("submit:1000x3,1002x2:5",src.Calls.LastOrDefault(),"release submits all rows with quantity > 0");
        src.Current=Sample(0,5,10,disabled:1,canSubmit:false);p.Update(world);int calls=src.Calls.Count;Click(p,new(170,350),cmd);
        t.Equal(calls,src.Calls.Count,"CanSubmit=false never submits");

        // 畫：底圖、框、TOTAL、拉桿、已選標記、數量底色。
        src.Current=Sample(0,6,10);p.Update(world);Click(p,Center(p.RowButtons(0).Plus),cmd);p.Mouse(M(UiMouseKind.Down,150,84+2),cmd);
        using(var canvas=new UiCanvas()){canvas.Clear(System.Drawing.Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            t.Equal(unchecked((int)0xFF00FF00),Argb(canvas,100,50),"frame at origin");
            t.Equal(unchecked((int)0xFF204080),Argb(canvas,107,300),"base visible through frame");
            t.Equal((int)Color(1991),Argb(canvas,119+1,73+1),"TOTAL 1991 at (19,23)");
            t.Equal((int)Color(24),Argb(canvas,117,86),"selected row marker surf 24 at row − 2");
            t.Equal((int)Color(2044),Argb(canvas,317+1,101+1),"knob at track top when scroll 0");
            t.Check(Argb(canvas,100+24+100,88+30)!=unchecked((int)0xFF204080),"quantity > 0 row filled");
            t.Equal(unchecked((int)0xFF204080),Argb(canvas,100+24+100,88+36+30),"quantity 0 row not filled");
            t.Equal(System.Drawing.Color.Black.ToArgb(),Argb(canvas,99,49),"nothing outside bounds");}

        // 捲動：下鈕放開才捲；拉桿依 捲動列／(筆數−1)。
        Click(p,new(323,330),cmd);t.Equal(1,p.Scroll,"down button scrolls on release");
        for(int i=0;i<10;i++)p.Mouse(new(UiMouseKind.Wheel,new(150,120),MouseButtons.None,0,-120,Keys.None),cmd);
        t.Equal(3,p.Scroll,"wheel clamps at rows − 7");
        t.Equal(new Rectangle(317,101+(224-16)*3/9,11,16),p.KnobRect,"knob y = track + span × scroll / (n − 1)");
        Click(p,new(323,92),cmd);t.Equal(2,p.Scroll,"up button scrolls");
        t.Equal(2,p.RowAt(new(150,85)),"row index follows scroll");

        // 拖曳：標題列 (1,1,239,20)，關閉 X 不算。
        t.Check(p.IsDragHandle(new(150,55))&&!p.IsDragHandle(new(327,67))&&!p.IsDragHandle(new(150,75)),"title strip is the drag handle");
        p.MoveTo(new(200,80));t.Check(p.Origin==new Point(200,80)&&p.Bounds==new Rectangle(200,80,239,317),"MoveTo moves origin and bounds");
        p.MoveTo(new(100,50));

        // 新清單重設；關閉 X／CANCEL／Esc。
        src.Current=Sample(0,7,3);p.Update(world);t.Check(p.Selected==-1&&p.Scroll==0&&p.Quantities.All(q=>q==0),"new revision resets");
        Click(p,new(328,68),cmd);t.Equal("close",src.Calls.Last(),"quit X closes on release");t.Check(!p.Visible,"hidden after close");
        p.Update(world);t.Check(!p.Visible,"same revision stays closed");
        src.Current=Sample(1,7,3);p.Update(world);t.Check(p.Visible,"same revision, other mode → new list");
        Click(p,new(230,350),cmd);t.Equal("close",src.Calls.Last(),"CANCEL closes");
        src.Current=Sample(0,8,3);p.Update(world);
        t.Check(p.Key(new(Keys.Escape,true,false),cmd)&&!p.Visible,"Esc closes");
        t.Check(!p.Key(new(Keys.Escape,true,false),cmd)&&!p.Mouse(M(UiMouseKind.Down,150,120),cmd),"hidden panel ignores input");
        p.Update(new UiStaticGameState(InWorld:false));t.Check(!p.Visible,"not in world → hidden");
    }
}
