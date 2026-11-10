using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R6-C（T333／#779）離線測試：合成封包＋合成 XML／圖（不讀真實客戶端），記憶體內假傳送端；不開遊戲、網路、視窗或素材。
// 涵蓋：S140 → TRADE_WINDOW 模式 13／14；舊清單在原生窗接上時讓位；多筆一包 C161（SP C_Result mode_buypc／mode_sellpc 的每筆 8 byte）；
// 13 合計 >99999 不送、14 一次 >8 筆不送（窗不關、數量不清）；數量上限；單價 5 級換色；面板左鈕。
static class PrivateShopTradeTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Owner=456,Gold=903;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    // S15 欄位順序同 Inventory.ReadItem（見 ShopTradeTests）。
    static byte[] Item(uint id,ushort icon,uint count,string name)=>Packet(b=>{b.Write((byte)15);b.Write(id);b.Write((ushort)0);b.Write((byte)0);b.Write((byte)0);b.Write(icon);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,name);b.Write((byte)0);b.Write(new byte[11]);});
    // S140 買（mode 0）：D owner, H n, n×(C order, C 未用, H icon, D 數量, D 單價, S 名稱, C 狀態 0)。
    static byte[] BuyList(params (byte order,ushort icon,uint listed,uint price,string name)[] rows)=>Packet(b=>{b.Write((byte)140);b.Write((byte)0);b.Write(Owner);b.Write((ushort)Math.Max(rows.Length,rows.Select(r=>r.order+1).DefaultIfEmpty(0).Max()));
        foreach(var r in rows){b.Write(r.order);b.Write((byte)0);b.Write(r.icon);b.Write(r.listed);b.Write(r.price);Text(b,r.name);b.Write((byte)0);}});
    // S140 賣（mode 1）：D owner, H 店主收購筆數, 每筆 13 byte (C order, D 物件, D 原訂收購量, D 單價)。
    static byte[] SellList(ushort declared,params (byte order,uint item,uint listed,uint price)[] rows)=>Packet(b=>{b.Write((byte)140);b.Write((byte)1);b.Write(Owner);b.Write(declared);
        foreach(var r in rows){b.Write(r.order);b.Write(r.item);b.Write(r.listed);b.Write(r.price);}});
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        // S87：動作 70＝擺攤中（ShopActive）。
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Owner);b.Write((ushort)1110);b.Write((byte)70);b.Write((byte)6);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);Text(b,"shopkeeper");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
        c.World.SelectTarget(Owner);Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,Item(Gold,5,500000,"$4"));
        for(uint i=0;i<9;i++)Feed(c,Item(901+i,(ushort)(20+i),50,"goods"+i));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static PrivateShopResult Query(LoginConnection c,bool sell)=>c.SendPrivateShop(true,sell,0,0,c.PrivateShopSnapshot()).GetAwaiter().GetResult();
    static PrivateShopResult Batch(LoginConnection c,int revision,params ShopChoice[] rows)=>c.SendPrivateShopBatch(rows,revision).GetAwaiter().GetResult();
    static string Hex(byte[] p)=>Convert.ToHexString(p);
    static byte[] Le(uint v)=>BitConverter.GetBytes(v);

    [WorldModuleTest("private-shop-trade")]
    static void Run(WorldModuleTestContext t){
        Packets(t);Buy(t);BuyTotal(t);Sell(t);Colors(t);Panel(t);
    }

    static void Packets(WorldModuleTestContext t){
        var a=new PrivateShopRow(0,2,0,0,"a",10,30,30);var b=new PrivateShopRow(1,5,0,0,"b",20,30,30);
        // 買：D owner, C 0, C size, C 0, 每筆 D order, D count。
        byte[] buy=PrivateShopPackets.SubmitMany(Owner,false,[(a,3),(b,7)]);
        t.Equal(Hex(LoginPackets.Pad([161,..Le(Owner),0,2,0,..Le(2),..Le(3),..Le(5),..Le(7)])),Hex(buy),"C161 buy: 2 rows × (D order, D count)");
        var s1=new PrivateShopRow(0,1,901,0,"s",4,50,50);var s2=new PrivateShopRow(1,6,902,0,"t",4,50,50);
        // 賣：每筆 D 物件, CH count, C order。
        t.Equal(Hex(LoginPackets.Pad([161,..Le(Owner),1,2,0,..Le(901),0x56,0x34,0x12,1,..Le(902),9,0,0,6])),Hex(PrivateShopPackets.SubmitMany(Owner,true,[(s1 with{Maximum=0x200000,ListedCount=0x200000},0x123456),(s2,9)])),"C161 sell: D item, CH count, C order per row");
        t.Equal(Hex(PrivateShopPackets.Submit(Owner,false,a,3)),Hex(PrivateShopPackets.SubmitMany(Owner,false,[(a,3)])),"single-row Submit = SubmitMany with one row");
        t.Throws<ArgumentException>(()=>PrivateShopPackets.SubmitMany(Owner,true,Enumerable.Range(0,9).Select(i=>(s1,1u)).ToArray()),"> 8 rows never encoded");
        t.Throws<ArgumentException>(()=>PrivateShopPackets.SubmitMany(Owner,false,[]),"empty batch never encoded");
        t.Throws<ArgumentException>(()=>PrivateShopPackets.SubmitMany(Owner,false,[(a,31)]),"count > maximum never encoded");
    }

    static void Buy(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;var src=new PrivateShopTradeSource(c,s=>"<"+s+">",()=>false);

        // 1. 原生窗未接：舊清單照舊，TRADE_WINDOW 不開。
        t.Equal(PrivateShopResult.Accepted,Query(c,false),"C47 buy query");
        Feed(c,BuyList((2,11,5,300,"potion"),(4,12,20000,8,"arrow")));
        t.Equal("list",w.PrivateShopPhase,"S140 buy list");
        t.Check(c.PrivateShopSnapshot().Rows.Length==2&&src.Current==null,"legacy: rows in old list, no TRADE_WINDOW");

        // 2. 原生窗接上：舊清單讓位；模式 13，上限＝min(9999, 清單上限)。
        w.PrivateShopNativeWindow=true;
        var snap=c.PrivateShopSnapshot();
        t.Check(snap.Rows.Length==0&&!snap.CanChoose&&!snap.CanSubmit,"native: legacy rows/choose/submit hidden",snap);
        t.Equal(PrivateShopResult.NotSent,c.SendPrivateShop(false,false,0,1,snap).GetAwaiter().GetResult(),"native: legacy single-row submit refused");
        var view=src.Current;
        t.Check(view is {Mode:TradeWindowPanel.ModePrivateBuy,ShowPrice:true,CanSubmit:true,Holding:500000}&&view.Revision==w.PrivateShopRevision,"S140 buy → TRADE_WINDOW mode 13",view);
        t.Check(view!.Items[0] is {Id:0,Icon:11,Price:300,Maximum:5,Name:"<potion>",Enabled:true},"row: id = row index, icon/price/name",view.Items[0]);
        t.Equal(9999u,view.Items[1].Maximum,"mode 13 cap 9999 (TradeBox +0x94)");

        // 3. 多筆一包 C161；送出後窗關、重送擋下。
        int before=sent.Count;src.Submit([new(0,2),new(1,100)],view.Revision);
        t.Equal(before+1,sent.Count,"one C161 for two rows");
        t.Equal(Hex(LoginPackets.Pad([161,..Le(Owner),0,2,0,..Le(2),..Le(2),..Le(4),..Le(100)])),Hex(sent[^1]),"buy rows use the server order");
        t.Equal("submitted",w.PrivateShopPhase,"phase submitted");
        t.Check(src.Current==null,"window closes after submit");
        t.Equal(PrivateShopResult.NotSent,Batch(c,view.Revision,new ShopChoice(0,1)),"stale revision never resends");
        t.Equal(before+1,sent.Count,"still one packet");

        // 4. 關閉：走 ClosePrivateShop。
        var sent2=new List<byte[]>();using var d=Seed(sent2);d.World.PrivateShopNativeWindow=true;var src2=new PrivateShopTradeSource(d,s=>s,()=>false);
        Query(d,false);Feed(d,BuyList((0,11,5,300,"potion")));t.Check(src2.Current!=null,"view before close");
        src2.Close();t.Equal("idle",d.World.PrivateShopPhase,"close ends the list");t.Check(src2.Current==null,"no view after close");
        // 忙碌（走路中）不送。
        Query(d,false);Feed(d,BuyList((0,11,5,300,"potion")));var busy=new PrivateShopTradeSource(d,s=>s,()=>true);int n=sent2.Count;busy.Submit([new(0,1)],d.World.PrivateShopRevision);
        t.Equal(n,sent2.Count,"busy source never sends");
        // 數量超過上限、未知列、重複列：整包不送。
        int rev=d.World.PrivateShopRevision;
        t.Equal(PrivateShopResult.NotSent,Batch(d,rev,new ShopChoice(0,6)),"over maximum");
        t.Equal(PrivateShopResult.NotSent,Batch(d,rev,new ShopChoice(7,1)),"unknown row");
        t.Equal(PrivateShopResult.NotSent,Batch(d,rev,new(0,1),new(0,1)),"duplicate row");
        t.Equal(n,sent2.Count,"invalid batches never send");
    }

    static void BuyTotal(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;w.PrivateShopNativeWindow=true;var src=new PrivateShopTradeSource(c,s=>s,()=>false);
        Query(c,false);Feed(c,BuyList((0,11,10,90000,"gem"),(1,12,20000,1,"pebble")));
        var view=src.Current!;int rev=view.Revision;
        // 合計 100,000 > 99,999：不送、窗不關、版本不變（數量保留）、提示換成字串 1028。
        t.Equal(PrivateShopResult.NotSent,Batch(c,rev,new ShopChoice(1,10000)),"over 9999 cap refused");
        t.Equal(PrivateShopResult.NotSent,Batch(c,rev,new(0,1),new(1,9999),new(1,1)),"duplicate row refused");
        string before=w.PrivateShopNotice;
        t.Equal(PrivateShopResult.NotSent,Batch(c,rev,new(0,1),new(1,9999),new(1,0)),"zero-count row refused");
        t.Equal(before,w.PrivateShopNotice,"invalid rows do not show the 1028 notice");
        t.Equal(PrivateShopResult.NotSent,Batch(c,rev,new ShopChoice(0,2)),"total 180000 refused");
        t.Equal(0,sent.Count,"no packet over 99999");
        t.Check(src.Current is {} v&&v.Revision==rev&&w.PrivateShopPhase=="list","window stays with the same revision");
        t.Check(w.PrivateShopNotice==PrivateShopTrade.TotalText(),"notice = string 1028 path",w.PrivateShopNotice);
        // 合計 90000＋9999＝99,999 剛好可送。
        t.Equal(PrivateShopResult.Accepted,Batch(c,rev,new(0,1),new(1,9999)),"total 99999 accepted");
        t.Equal(1,sent.Count,"one packet at the limit");
    }

    static void Sell(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;w.PrivateShopNativeWindow=true;var src=new PrivateShopTradeSource(c,s=>s,()=>false);
        t.Equal(PrivateShopResult.Accepted,Query(c,true),"C47 sell query");
        // 9 筆：901..909 各 50 個；店主原訂收購 30。
        Feed(c,SellList(9,Enumerable.Range(0,9).Select(i=>((byte)i,901u+(uint)i,30u,7u)).ToArray()));
        var view=src.Current;
        t.Check(view is {Mode:TradeWindowPanel.ModePrivateSell,CanSubmit:true}&&view.Items.Length==9,"S140 sell → TRADE_WINDOW mode 14",view);
        t.Check(view!.Items[0] is {Id:0,Icon:20,Price:7,Maximum:30,Name:"goods0"},"sell row: inventory icon/name, max = listed",view.Items[0]);
        int rev=view.Revision;
        // 一次 9 筆：不送、提示 897。
        t.Equal(PrivateShopResult.NotSent,Batch(c,rev,Enumerable.Range(0,9).Select(i=>new ShopChoice((uint)i,1)).ToArray()),"9 rows refused");
        t.Check(sent.Count==0&&w.PrivateShopNotice==PrivateShopTrade.RowsText()&&src.Current?.Revision==rev,"no packet; notice 897; window stays",w.PrivateShopNotice);
        // 8 筆可送；同一物件跨列加總超過持有量不送（另一個店）。
        t.Equal(PrivateShopResult.Accepted,Batch(c,rev,new(0,30),new(3,2)),"2 rows accepted");
        t.Equal(Hex(LoginPackets.Pad([161,..Le(Owner),1,2,0,..Le(901),30,0,0,0,..Le(904),2,0,0,3])),Hex(sent[^1]),"C161 sell rows: D item, CH count, C order");

        var sent2=new List<byte[]>();using var d=Seed(sent2);d.World.PrivateShopNativeWindow=true;
        Query(d,true);Feed(d,SellList(2,(0,901,40,1),(1,901,40,1)));
        int r2=d.World.PrivateShopRevision;
        t.Equal(PrivateShopResult.NotSent,Batch(d,r2,new(0,30),new(1,30)),"same item across rows above held (50) refused");
        t.Equal(PrivateShopResult.Accepted,Batch(d,r2,new(0,25),new(1,25)),"same item within held accepted");
        t.Equal(1,sent2.Count,"one packet");
    }

    static void Colors(WorldModuleTestContext t){
        t.Check(TradeWindowPanel.PriceColor(13,9999)==null&&TradeWindowPanel.PriceColor(14,0)==null,"< 1 萬 normal colour");
        foreach(var (price,palette,raw) in new[]{(10000u,11,0x63B6),(99999u,11,0x63B6),(100000u,40,0x5A36),(1000000u,29,0x5FBC),(10000000u,4,0x509E),(100000000u,34,0x5E8D),(2000000000u,34,0x5E8D)}){
            t.Equal<Color?>(ChatPalette.Color(palette),TradeWindowPanel.PriceColor(13,price),$"mode 13 price {price} → palette[{palette}]");
            t.Equal<Color?>(ChatPalette.Color(palette),TradeWindowPanel.PriceColor(14,price),$"mode 14 price {price} → palette[{palette}]");
            t.Equal((ushort)raw,ChatPalette.Raw(palette),$"palette[{palette}] RGB555 from BIN");
        }
        foreach(int mode in new[]{0,1,6,7})t.Check(TradeWindowPanel.PriceColor(mode,100000000)==null,$"mode {mode} has no price tiers");
    }

    // ---- 面板：合成 XML（座標照 TWP§1.3）與合成圖，接真的 PrivateShopTradeSource ----
    const string Xml="""
<root>
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
    static UiSurface Surf(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF000000u|(uint)(id*2654435761u&0x00FFFFFF),w*h).ToArray()));
    static UiSurface? Surfaces(int id)=>id switch{
        1983=>Surf(id,227,304),1984=>Surf(id,239,317),1962 or 1963=>Surf(id,8,8),1970=>Surf(id,12,224),2040 or 2041 or 2042 or 2043=>Surf(id,12,14),2044 or 2045=>Surf(id,11,16),
        1985 or 1987 or 555 or 557=>Surf(id,7,7),1990=>Surf(id,8,10),1991=>Surf(id,36,10),>=1992 and <=1999=>Surf(id,53,17),24=>Surf(id,12,12),_=>null};
    static UiMouseEvent M(UiMouseKind kind,Point at)=>new(kind,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(TradeWindowPanel p,Point at,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,at),cmd);p.Mouse(M(UiMouseKind.Up,at),cmd);}

    static void Panel(WorldModuleTestContext t){
        var p=new TradeWindowPanel();p.Load(UiLayout.Parse(Xml,"synthetic-uiextra86.xml"),Surfaces);
        var cmd=new UiNullCommands();var world=new UiStaticGameState(InWorld:true);var left=new Point(100+60+5,50+292+5);

        // 買（13）：左鈕 BUY；合計超過 99999 時按 BUY 不送、窗不關、數量保留；改數量後送出一包。
        var sent=new List<byte[]>();using var c=Seed(sent);c.World.PrivateShopNativeWindow=true;
        p.Source=new TradeWindowSources(new PrivateShopTradeSource(c,s=>s,()=>false));
        Query(c,false);Feed(c,BuyList((0,11,10,60000,"gem"),(3,12,10,100,"pebble")));
        p.Update(world);
        t.Check(p.Visible&&p.View?.Mode==TradeWindowPanel.ModePrivateBuy,"panel opens mode 13");
        t.Equal<int?>(1992,p.LeftButton?.Surf,"mode 13 left button BUY 1992");
        p.Adjust(0,+1);p.Adjust(0,+1);p.Adjust(1,+1);
        Click(p,left,cmd);p.Update(world);
        t.Check(sent.Count==0&&p.Visible&&p.Quantities[0]==2,"over 99999: nothing sent, window and quantities kept",p.Quantities);
        p.Adjust(0,-1);Click(p,left,cmd);p.Update(world);
        t.Equal(1,sent.Count,"BUY sends one C161");
        t.Equal(Hex(LoginPackets.Pad([161,..Le(Owner),0,2,0,..Le(0),..Le(1),..Le(3),..Le(1)])),Hex(sent[^1]),"panel rows → C161 orders 0 and 3");
        t.Check(!p.Visible,"window closes after submit");

        // 賣（14）：左鈕 SELL；CANCEL 關清單。
        var sent2=new List<byte[]>();using var d=Seed(sent2);d.World.PrivateShopNativeWindow=true;
        p.Source=new TradeWindowSources(new PrivateShopTradeSource(d,s=>s,()=>false));
        Query(d,true);Feed(d,SellList(1,(0,901,30,7)));p.Update(world);
        t.Equal<int?>(1998,p.LeftButton?.Surf,"mode 14 left button SELL 1998");
        Click(p,new(100+127+5,50+292+5),cmd);
        t.Check(!p.Visible&&d.World.PrivateShopPhase=="idle","CANCEL closes the private list");
    }
}
