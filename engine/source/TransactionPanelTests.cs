using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R3-E（T281／#674）離線測試：TRANSACTION_WINDOW 版面（合成 XML，照本機 uiextra86-c.xml 的元素順序與數值）、
// S52／S35／S112 → TransactionView、面板按鈕 → C71／C86。合成封包與記憶體送出端；不開遊戲、伺服器、DB，不讀客戶端資源。
static class TransactionPanelTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Peer=456;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static byte[] Open(string peer)=>Packet(b=>{b.Write((byte)52);Text(b,peer);});
    static byte[] Offer(byte side,string name,ushort icon=7)=>Packet(b=>{b.Write((byte)35);b.Write(side);b.Write(icon);Text(b,name);b.Write((byte)0);});
    static byte[] Result(byte status)=>LoginPackets.Pad([112,status]);

    /// <summary>In world, one player "target" adjacent (S87), trade already accepted and waiting for S52.</summary>
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Peer);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,"target");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
        Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,Packet(b=>{b.Write((byte)15);b.Write(901u);b.Write((ushort)0);b.Write((byte)1);b.Write((byte)0);b.Write((ushort)0);b.Write((byte)1);b.Write(5u);b.Write((byte)0);Text(b,"test item");b.Write((byte)0);b.Write(new byte[11]);}));
        Set(c.World,"tradeEpoch",c.World.WorldEpoch);Set(c.World,"TradePeerName","target");Set(c.World,"TradePeerId",(uint?)Peer);Set(c.World,"TradeState","accepting");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Run(LoginConnection c,string cmd,uint item=0,uint count=0)=>c.Trade(cmd,c.World.TradeRevision,c.World.WorldEpoch,c.World.TradeGeneration,item,count).GetAwaiter().GetResult();
    static string Resolve(string s)=>s.Replace("$4","金幣");

    [WorldModuleTest("transaction-window")]
    static void Checks(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id==TransactionPanel.PanelId&&p.Layout=="uiextra86")==1,"TRANSACTION_WINDOW registered once on uiextra86");
        Rules(t);Protocol(t);Layout(t);Panel(t);
    }

    static void Rules(WorldModuleTestContext t){
        t.Equal<long?>(1000,TransactionRules.Adena("$4 (1000)"),"adena name with count");
        t.Equal<long?>(1,TransactionRules.Adena("$4"),"adena name without count = 1");
        t.Equal<long?>(null,TransactionRules.Adena("$40 (3)"),"other $ ids are not adena");
        t.Equal<long?>(null,TransactionRules.Adena("長劍 (2)"),"ordinary item");
        t.Equal<long?>(null,TransactionRules.Adena("$4 (99999999999999999999)"),"overflow rejected");
        t.Check(TransactionView.WindowState("open")&&TransactionView.WindowState("cancelling")&&!TransactionView.WindowState("invited")&&!TransactionView.WindowState("completed"),"window states: S52 … S112 only");
    }

    static void Protocol(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        t.Check(c.World.TradePeerPresent,"seed: peer adjacent");
        t.Check(TransactionView.From(c.World,Resolve)==null,"no window before S52 (accepting)");
        Feed(c,Open("other"));
        t.Check(c.World.TradeState=="accepting"&&TransactionView.From(c.World,Resolve)==null,"S52 for another name ignored");
        Feed(c,Open("target"));
        var v=TransactionView.From(c.World,Resolve);
        t.Check(c.World.TradeOpened&&v!=null&&v.Peer=="target"&&v.Offers.Count==0&&v.CanConfirm&&v.CanCancel&&!v.OwnConfirmed,"S52 (op 88) opens the window",v);

        t.Check(Run(c,"add",901,2)&&sent[^1].SequenceEqual(TradePackets.Add(901,2)),"add sends C37 item+count");
        Feed(c,Offer(0,"test item (2)"));Feed(c,Offer(0,"$4 (1500)"));Feed(c,Offer(1,"$4 (20)"));Feed(c,Offer(1,"長劍"));
        v=TransactionView.From(c.World,Resolve)!;
        t.Check(v.Side(0).Select(o=>o.Name).SequenceEqual(new[]{"test item (2)","金幣 (1500)"})&&v.Side(1).Select(o=>o.Name).SequenceEqual(new[]{"金幣 (20)","長劍"}),"S35 (op 145) rows split by side, names resolved",v.Offers);
        t.Check(v.Adena(0)==1500&&v.Adena(1)==20,"adena totals per side");

        int before=sent.Count;
        t.Check(Run(c,"confirm")&&sent.Count==before+1&&sent[^1].SequenceEqual(TradePackets.Confirm())&&sent[^1][0]==71,"confirm sends C71");
        v=TransactionView.From(c.World,Resolve)!;
        t.Check(v.OwnConfirmed&&!v.CanConfirm&&v.CanCancel,"after confirm: OK disabled, cancel still allowed");
        Feed(c,Result(0));
        t.Check(c.World.TradeState=="completed"&&TransactionView.From(c.World,Resolve)==null,"S112 (op 167) status 0 closes the window");

        var sent2=new List<byte[]>();using var d=Seed(sent2);Feed(d,Open("target"));
        t.Check(Run(d,"cancel")&&sent2.Single().SequenceEqual(TradePackets.Cancel())&&sent2[0][0]==86,"cancel sends C86");
        t.Check(d.World.TradeState=="cancelling"&&TransactionView.From(d.World,Resolve) is {CanCancel:false,CanConfirm:false},"cancelling: window stays until S112, buttons disabled");
        Feed(d,Result(1));
        t.Check(d.World.TradeState=="cancelled"&&TransactionView.From(d.World,Resolve)==null,"S112 status 1 closes the window");

        var sent3=new List<byte[]>();using var e=Seed(sent3);Feed(e,Open("target"));
        Set(e.World,"WorldEpoch",e.World.WorldEpoch+1);
        t.Check(!e.World.TradeOpened&&TransactionView.From(e.World,Resolve)==null,"window dies with the world epoch");
    }

    /// <summary>Synthetic TRANSACTION_WINDOW with the element order and attribute values of the local uiextra86-c.xml.</summary>
    internal const string Xml="""
<?xml version="1.0"?>
<ExtraWindowConfigulation>
<TRANSACTION_WINDOW x = "0" y = "0">
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_TRANSACT_BASE" surfNumber = "2000" xPos = "6" yPos = "10"/>
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_TRANSACT_FRAME" surfNumber = "2001"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_QUIT_X" surfNumber = "1962" xPos = "179" yPos = "15"/>
	<IMAGE surfName = "SURF_RENEWAL_WIN_TRANSACT_TEXT_FRAME" surfNumber = "2008" xPos = "15" yPos = "213"/>
	<STAGE3_BTN surfName = "SURF_RENEWAL_BTN_TRANSACT_OK" surfNumber = "2002" xRPos = "36" yRPos = "252"/>
	<STAGE3_BTN surfName = "SURF_RENEWAL_BTN_TRANSACT_CANCEL" surfNumber = "2005" xRPos = "104" yRPos = "252"/>
	<ITEM_LIST_ADJUST surfNumber = "0" xPos = "26" yPos = "49" tag = "91"/>
</TRANSACTION_WINDOW>
</ExtraWindowConfigulation>
""";
    static Func<int,UiSurface?> Surfaces(bool missingHighlight=false){
        var sizes=new Dictionary<int,(int W,int H)>{[2000]=(180,270),[2001]=(200,290),[1962]=(14,14),[2008]=(160,30),[2002]=(50,18),[2003]=(50,18),[2004]=(50,18),[2005]=(50,18),[2006]=(50,18),[2007]=(50,18)};
        if(missingHighlight){sizes.Remove(2004);sizes.Remove(2007);}
        return LegacyWindowTestKit.Surfaces(sizes).Get;
    }

    static void Layout(WorldModuleTestContext t){
        var layout=UiLayout.Parse(Xml,"uiextra86-c.xml");
        var p=new TransactionPanel();p.Load(layout,Surfaces());
        t.Check(p.Id=="TRANSACTION_WINDOW"&&p.Layer==UiLayer.Window&&!p.Visible,"TRANSACTION_WINDOW window layer; hidden without source");
        t.Equal(new Rectangle(6,10,187,270),p.Bounds,"bounds = base ∪ X (frame 2001 has no coordinates, not drawn)");
        t.Equal(new Rectangle(179,15,14,14),p.At(p.CloseButton),"close X 1962 at (179,15)");
        t.Equal(new Rectangle(15,213,160,30),p.At(p.TextFrame),"text frame 2008 at (15,213)");
        t.Check(p.At(p.Ok)==new Rectangle(36,252,50,18)&&p.At(p.Cancel)==new Rectangle(104,252,50,18),"OK 2002 (36,252), cancel 2005 (104,252) via xRPos/yRPos");
        t.Check(p.ListOffset==new Point(26,49)&&p.ListSpacing==91,"ITEM_LIST_ADJUST (26,49) tag 91");
        t.Check(p.ListArea(0)==new Rectangle(26,49,140,73)&&p.ListArea(1)==new Rectangle(26,140,140,73),"own list at y=49, peer list at y=49+tag; height = text frame − lower list",new[]{p.ListArea(0),p.ListArea(1)});
        t.Equal(6,p.VisibleRows,"6 rows of 12 px per list");
        t.Check(p.Ok!.Push!=null&&p.Ok.Highlight!=null&&p.Cancel!.Push!=null,"STAGE3 n+1/n+2 images loaded");

        t.Throws<UiLayoutException>(()=>new TransactionPanel().Load(UiLayout.Parse(Xml.Replace("SURF_RENEWAL_BTN_TRANSACT_OK","X"),"a.xml"),Surfaces()),"missing OK throws");
        t.Throws<UiLayoutException>(()=>new TransactionPanel().Load(UiLayout.Parse(Xml.Replace(" xRPos = \"104\" yRPos = \"252\"",""),"b.xml"),Surfaces()),"button without coordinates throws");
        t.Throws<UiLayoutException>(()=>new TransactionPanel().Load(UiLayout.Parse(Xml.Replace("<ITEM_LIST_ADJUST surfNumber = \"0\" xPos = \"26\" yPos = \"49\" tag = \"91\"/>",""),"c.xml"),Surfaces()),"missing ITEM_LIST_ADJUST throws");
        t.Throws<UiLayoutException>(()=>new TransactionPanel().Load(UiLayout.Parse(Xml.Replace(" tag = \"91\"",""),"d.xml"),Surfaces()),"ITEM_LIST_ADJUST without tag throws");
        t.Throws<UiLayoutException>(()=>new TransactionPanel().Load(UiLayout.Parse("<root/>","e.xml"),Surfaces()),"missing TRANSACTION_WINDOW throws");
        var m=new TransactionPanel();m.Load(layout,Surfaces(missingHighlight:true));
        t.Check(m.Ok!.Highlight==null&&m.At(m.Ok)==new Rectangle(36,252,50,18),"missing hover image: button still placed");
    }

    sealed class FakeSource:ITransactionSource {
        public TransactionView? Current{get;set;}
        public readonly List<string> Calls=[];
        public void Confirm(int revision)=>Calls.Add("confirm:"+revision);
        public void Cancel(int revision)=>Calls.Add("cancel:"+revision);
    }
    static void Click(IUiPanel p,Point at,IUiCommands cmd){
        p.Mouse(new UiMouseEvent(UiMouseKind.Down,at,MouseButtons.Left,1,0,Keys.None),cmd);
        p.Mouse(new UiMouseEvent(UiMouseKind.Up,at,MouseButtons.Left,1,0,Keys.None),cmd);
    }

    static void Panel(WorldModuleTestContext t){
        var p=new TransactionPanel();p.Load(UiLayout.Parse(Xml,"uiextra86-c.xml"),Surfaces());
        var src=new FakeSource();var cmd=new UiNullCommands();var world=new UiStaticGameState(InWorld:true);
        p.Update(world);t.Check(!p.Visible,"no source → hidden");
        p.Source=src;p.Update(world);t.Check(!p.Visible,"source without trade → hidden");
        TransactionOffer O(byte side,string name,long gold=0)=>new(side,name,gold);
        var offers=new List<TransactionOffer>{O(0,"長劍"),O(0,"金幣 (300)",300),O(1,"藥水")};
        src.Current=new TransactionView(5,"target","原服已開啟交易。",offers.ToArray(),true,true,false);
        p.Update(world);
        t.Check(p.Visible&&p.VisibleOffers(0).Select(o=>o.Name).SequenceEqual(new[]{"長劍","金幣 (300)"})&&p.VisibleOffers(1).Single().Name=="藥水","open trade → visible, own list on top, peer below");
        p.Update(new UiStaticGameState(InWorld:false));t.Check(!p.Visible,"leaving the world hides it");
        p.Update(world);

        Click(p,new(40,255),cmd);
        t.Check(src.Calls.SequenceEqual(new[]{"confirm:5"}),"OK → Confirm(revision) once",src.Calls);
        Click(p,new(110,255),cmd);
        t.Equal("cancel:5",src.Calls[^1],"cancel button → Cancel(revision)");
        Click(p,new(182,18),cmd);
        t.Equal("cancel:5",src.Calls[^1],"close X → Cancel too (window waits for S112)");
        t.Check(p.Visible,"window stays open after cancel click until the game closes it");
        int n=src.Calls.Count;
        p.Mouse(new UiMouseEvent(UiMouseKind.Down,new(40,255),MouseButtons.Left,1,0,Keys.None),cmd);
        p.Mouse(new UiMouseEvent(UiMouseKind.Up,new(120,200),MouseButtons.Left,1,0,Keys.None),cmd);
        t.Equal(n,src.Calls.Count,"press on OK, release elsewhere → nothing");
        t.Check(p.Key(new UiKeyEvent(Keys.Escape,true,false),cmd)&&src.Calls[^1]=="cancel:5"&&src.Calls.Count==n+1,"Esc → Cancel");
        t.Check(!p.Key(new UiKeyEvent(Keys.A,true,false),cmd),"other keys pass through");

        src.Current=src.Current with{Revision=6,CanConfirm=false,OwnConfirmed=true};p.Update(world);
        n=src.Calls.Count;Click(p,new(40,255),cmd);
        t.Equal(n,src.Calls.Count,"own confirmed → OK sends nothing");
        t.Check(p.StateSurface(p.Ok!)==p.Ok!.Push,"own confirmed → OK drawn pressed (2003)");
        src.Current=src.Current with{Revision=7,CanCancel=false};p.Update(world);
        Click(p,new(110,255),cmd);t.Check(p.Key(new UiKeyEvent(Keys.Escape,true,false),cmd),"Esc still swallowed while cancelling");
        t.Equal(n,src.Calls.Count,"cancelling → cancel sends nothing more");

        // Hover/push images.
        src.Current=src.Current with{Revision=8,CanCancel=true,CanConfirm=true,OwnConfirmed=false};p.Update(world);
        p.Mouse(new UiMouseEvent(UiMouseKind.Move,new(110,255),MouseButtons.None,0,0,Keys.None),cmd);
        t.Check(p.StateSurface(p.Cancel!)==p.Cancel!.Highlight,"hover → n+2 image");
        p.Mouse(new UiMouseEvent(UiMouseKind.Down,new(110,255),MouseButtons.Left,1,0,Keys.None),cmd);
        t.Check(p.StateSurface(p.Cancel!)==p.Cancel!.Push,"press → n+1 image");
        p.Mouse(new UiMouseEvent(UiMouseKind.Up,new(300,400),MouseButtons.Left,1,0,Keys.None),cmd);

        // Scroll: 9 own rows, 6 visible; a new revision scrolls to the end; wheel scrolls one list only.
        var many=Enumerable.Range(1,9).Select(i=>O(0,"物品"+i)).Append(O(1,"藥水")).ToArray();
        src.Current=src.Current with{Revision=9,Offers=many};p.Update(world);
        t.Check(p.Scroll(0)==3&&p.VisibleOffers(0).First().Name=="物品4"&&p.Scroll(1)==0,"new rows → own list scrolled to the end");
        t.Check(p.Mouse(new UiMouseEvent(UiMouseKind.Wheel,new(60,60),MouseButtons.None,0,120,Keys.None),cmd)&&p.Scroll(0)==2,"wheel up over own list");
        p.Mouse(new UiMouseEvent(UiMouseKind.Wheel,new(60,150),MouseButtons.None,0,120,Keys.None),cmd);
        t.Check(p.Scroll(0)==2&&p.Scroll(1)==0,"wheel over peer list does not move own list (clamped at 0)");

        // Drag handle: title strip only; MoveTo moves Bounds.
        t.Check(p.IsDragHandle(new(60,20))&&!p.IsDragHandle(new(182,18))&&!p.IsDragHandle(new(60,60)),"drag handle = title strip, not the X or the lists");
        p.MoveTo(new(300,150));
        t.Check(p.Bounds.Location==new Point(300,150)&&p.At(p.Ok)==new Rectangle(330,392,50,18)&&p.ListArea(1)==new Rectangle(320,280,140,73),"MoveTo moves every part",p.Bounds);

        using(var canvas=new UiCanvas()){
            var root=new UiRoot();root.Add(p);root.Update(world);root.Compose(canvas,null);
            t.Check(!root.Faulted.Contains(p),"paint does not fault");
            t.Equal(LegacyWindowTestKit.ColourOf(2000),canvas.Bitmap.GetPixel(300+170,150+5).ToArgb(),"base 2000 painted at moved position");
            t.Equal(LegacyWindowTestKit.ColourOf(2002),canvas.Bitmap.GetPixel(330+45,392+15).ToArgb(),"OK 2002 painted (normal image)");
            t.Equal(LegacyWindowTestKit.ColourOf(2008),canvas.Bitmap.GetPixel(294+155,213+140+25).ToArgb(),"text frame 2008 painted");
        }

        src.Current=null;p.Update(world);
        t.Check(!p.Visible&&p.View==null,"S112 result (source null) → hidden");
    }
}
