using System.Reflection;
using P06.NativeUi;
namespace P06;

// T429（#986，R12-B，FU15§2.10）bid-close 離線測試：盟屋拍賣出價窗（AgitBidPanel）Ok／Enter 送出後馬上關窗
// （sub_545920 送 sub_9FBC00 之後呼叫 [this+0x154]→vtable+0x8C(0,1,1)），同一筆 S136 不再自己開；夾值與 4381 不關窗；
// 產品宿主 AgitAuctionHost 實作 IAgitAuctionChatHost，4381 的聊天欄顏色＝palette[24]（sub_7DC850 第一色）。
// 只用合成版面（自寫的小 XML，結構同 AgitAuctionUI.xml 的 Bid_Window）與合成單色圖；不讀主客戶端或參考客戶端、
// 不開遊戲、網路或視窗（產品宿主只用反射檢查型別，不建立 ClientForm）。CI 由 --module-tests 自動發現執行。
static class BidCloseTests {
    const string Xml="""
        <?xml version="1.0"?>
        <AgitAuctionUI>
          <Window Name = "Bid_Window" X="0" Y="0" BaseSurf = "800;1" Moveable = "20" MouseEvent="Dummy">
            <Label Name = "CurPrice_Label" X = "19" Y = "44" Width = "148" Height = "12" Align="center"/>
            <Edit Name = "Price_Edit" X = "65" Y = "64" Width = "56" Align="center" Height = "13" LimitText = "10" OnlyNumber = "1" MouseEvent = "PriceEdit_BidWin"/>
            <Button Name = "Ok_Button" X="34" Y="88" BaseSurf="812;1" MouseEvent="Ok_BidWin"/>
            <Button Name = "Cancel_Button" X="99" Y="88" BaseSurf="812;1" MouseEvent="Cancel_BidWin"/>
          </Window>
        </AgitAuctionUI>
        """;
    const string BelowText="（合成）低於最小值";
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF808080u,w*h).ToArray()));
    static readonly Dictionary<int,UiSurface> Surfs=new(){[800]=Surface(800,190,120),[812]=Surface(812,50,18)};
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static readonly UiStaticGameState InWorld=new(InWorld:true);

    sealed class Host:IAgitAuctionHost,IAgitAuctionChatHost {
        public List<(AmountOffer Offer,long Value)> Bids=[];
        public List<string> Chat=[];
        public AmountOffer? BidOffer{get;set;}
        public bool CanBid{get;set;}=true;
        public void Select(AgitAuctionOpenRequest shown,uint houseId){}
        public void Apply(AgitAuctionInfoRequest shown){}
        public string? InfoText(AgitAuctionInfoRequest shown)=>null;
        public void Bid(AmountOffer shown,long value)=>Bids.Add((shown,value));
        public void ChatNotice(string text)=>Chat.Add(text);
    }

    static AmountOffer Offer(long revision,int initial=150,int min=100,int max=200)=>new(0x1234,initial,min,max,AgitBidPanel.ApplyHtml,"agapply 458757",[],revision,1);
    static AgitBidPanel Open(Host host,AmountOffer offer){
        var p=new AgitBidPanel();p.Attach(host);
        p.TextResolver=(k,v)=>k==RefClientTextSource.StringKind&&v==AgitBidPanel.BelowMinimumString?BelowText:null;
        p.Load(UiLayout.Parse(Xml,"AgitAuctionUI.xml"),id=>Surfs.GetValueOrDefault(id),_=>null);
        host.BidOffer=offer;p.Update(InWorld);
        return p;
    }
    static void Click(AgitBidPanel p,string button,UiNullCommands c){var r=p.Window!.At(p.Window.Find(button)!);var at=new Point(r.X+r.Width/2,r.Y+r.Height/2);p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static void Type(AgitBidPanel p,string s,UiNullCommands c){for(int i=0;i<32&&p.Input.Length>0;i++)p.Key(new(Keys.Back,true,false),c);foreach(char ch in s)p.Char(ch);}

    [WorldModuleTest("bid-close")]
    static void RunAll(WorldModuleTestContext t){
        CloseOnSend(t);
        StaysOpen(t);
        ProductHost(t);
    }

    // 1. 範圍內 Ok／Enter：送一次、馬上關窗；同一筆 S136 留著也不重開；下一筆 S136 重開。
    static void CloseOnSend(WorldModuleTestContext t){
        var host=new Host();var cmd=new UiNullCommands();
        var first=Offer(1);var p=Open(host,first);
        t.Check(p.Visible&&p.Shown==first&&p.Input=="150","agapply opens with the suggested value");
        Click(p,"Ok_Button",cmd);
        t.Check(host.Bids.Count==1&&host.Bids[0]==(first,150L),"Ok in range → host.Bid once",host.Bids);
        t.Check(!p.Visible&&p.Shown==null&&p.Bounds.IsEmpty,"Ok sent → window closed right away (FU15§2.10), not waiting for the S136 to go away");
        p.Update(InWorld);
        t.Check(!p.Visible,"same S136 still current (server has not answered): not reopened");
        Click(p,"Ok_Button",cmd);p.Key(new(Keys.Enter,true,false),cmd);
        t.Check(host.Bids.Count==1,"closed window: Ok / Enter send nothing more (no duplicate bid)");
        t.Check(!p.Submit(),"Submit on a closed window → false");
        var second=Offer(2,initial:180);host.BidOffer=second;p.Update(InWorld);
        t.Check(p.Visible&&p.Shown==second&&p.Input=="180","next agapply reopens");
        t.Check(p.Key(new(Keys.Enter,true,false),cmd)&&host.Bids.Count==2&&host.Bids[1]==(second,180L)&&!p.Visible,"Enter = Ok: sends, then closes",host.Bids);
        var third=Offer(3);host.BidOffer=third;p.Update(InWorld);
        Type(p,"100",cmd);
        t.Check(p.Submit()&&!p.Visible&&host.Bids[^1]==(third,100L),"Submit at the minimum: true, closed");
        var fourth=Offer(4);host.BidOffer=fourth;p.Update(InWorld);
        Type(p,"200",cmd);
        t.Check(p.Submit()&&!p.Visible&&host.Bids[^1]==(fourth,200L),"Submit at the maximum: true, closed");
        t.Check(host.Chat.Count==0,"no 4381 for bids inside the range");
    }

    // 2. 不送的情況都不關窗：低於最小（設最小＋4381）、高於最大（設最大）、宿主不能送、不能回答的 S136。
    static void StaysOpen(WorldModuleTestContext t){
        var host=new Host();var cmd=new UiNullCommands();
        var p=Open(host,Offer(10));
        Type(p,"5",cmd);Click(p,"Ok_Button",cmd);
        t.Check(p.Visible&&p.Input=="100"&&host.Bids.Count==0,"below the minimum: not sent, window stays open, edit = minimum");
        t.Check(host.Chat.Count==1&&host.Chat[0]==BelowText,"below the minimum: 4381 to the chat host",host.Chat);
        Type(p,"999",cmd);p.Key(new(Keys.Enter,true,false),cmd);
        t.Check(p.Visible&&p.Input=="200"&&host.Bids.Count==0,"above the maximum (Enter): not sent, stays open, edit = maximum");
        host.CanBid=false;Type(p,"150",cmd);
        t.Check(!p.Submit()&&p.Visible&&host.Bids.Count==0,"host cannot send now: not sent, stays open");
        host.CanBid=true;
        t.Check(p.Submit()&&!p.Visible&&host.Bids.Count==1,"host can send again: sent and closed");
        var dead=Open(new Host(),Offer(11,initial:0,min:10,max:5));
        t.Check(dead.Visible&&!dead.Submit()&&dead.Visible,"unanswerable S136: Ok does nothing, stays open");
        // Cancel 照舊關窗、不送（不受本任務影響）。
        var c=Open(host,Offer(12));Click(c,"Cancel_Button",cmd);
        t.Check(!c.Visible&&host.Bids.Count==1&&cmd.Calls.Contains("game-focus"),"Cancel closes, nothing sent, focus returned");
    }

    // 3. 產品宿主：RingWindows.cs 的 ClientForm.AgitAuctionHost 實作 IAgitAuctionChatHost；顏色 palette[24]（sub_545920）。
    static void ProductHost(WorldModuleTestContext t){
        var type=typeof(ClientForm).GetNestedType("AgitAuctionHost",BindingFlags.NonPublic);
        t.Check(type!=null,"ClientForm.AgitAuctionHost found");
        if(type==null)return;
        t.Check(typeof(IAgitAuctionHost).IsAssignableFrom(type)&&typeof(IAgitAuctionChatHost).IsAssignableFrom(type),"product host implements IAgitAuctionChatHost (4381 reaches the chat bar)");
        var color=type.GetField("BidChatColor",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public);
        int? value=color is {IsLiteral:true}&&color.GetRawConstantValue() is int v?v:null;
        t.Check(value==24,"4381 colour = palette[24] (sub_7DC850 first colour)",value);
        t.Check(value==ClientForm.MailChatColor,"same palette slot as the mail result lines (MailChatColor)");
    }
}
