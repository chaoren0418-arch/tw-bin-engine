using P06.NativeUi;
namespace P06;

// T424（#968，FU15§2.10）bid-step 離線測試：盟屋拍賣出價窗（AgitBidPanel）的 Minus／Plus ±1、夾在 [最小, 最大]、
// 按住連續（時間是推測值 RepeatDelayMs／RepeatIntervalMs）、Ok 低於最小 → 設最小＋字串 4381、高於最大 → 設最大。
// 只用合成版面（自寫的小 XML，結構同 AgitAuctionUI.xml 的 Bid_Window）與合成單色圖；不讀主客戶端或參考客戶端、
// 不開遊戲、網路或視窗。CI 由 --module-tests 自動發現執行。
static class BidStepTests {
    const string Xml="""
        <?xml version="1.0"?>
        <AgitAuctionUI>
          <Window Name = "Bid_Window" X="0" Y="0" BaseSurf = "800;1" Moveable = "20" MouseEvent="Dummy">
            <Label Name = "CurPrice_Label" X = "19" Y = "44" Width = "148" Height = "12" Align="center"/>
            <Button Name = "Minus_Button" X="50" Y = "65" BaseSurf="810;1" PushSurf="811;1" MouseEvent="Minus_BidWin"/>
            <Edit Name = "Price_Edit" X = "65" Y = "64" Width = "56" Align="center" Height = "13" LimitText = "10" OnlyNumber = "1" MouseEvent = "PriceEdit_BidWin"/>
            <Button Name = "Plus_Button" X="124" Y = "65" BaseSurf="810;1" PushSurf="811;1" MouseEvent="Plus_BidWin"/>
            <Button Name = "Ok_Button" X="34" Y="88" BaseSurf="812;1" MouseEvent="Ok_BidWin"/>
            <Button Name = "Cancel_Button" X="99" Y="88" BaseSurf="812;1" MouseEvent="Cancel_BidWin"/>
          </Window>
        </AgitAuctionUI>
        """;
    const string BelowText="（合成）低於最小值";
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF808080u,w*h).ToArray()));
    static readonly Dictionary<int,UiSurface> Surfs=new(){[800]=Surface(800,190,120),[810]=Surface(810,12,12),[811]=Surface(811,12,12),[812]=Surface(812,50,18)};
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static UiStaticGameState At(long now)=>new(InWorld:true,Now:now);

    class Host:IAgitAuctionHost {
        public List<(AmountOffer Offer,long Value)> Bids=[];
        public AmountOffer? BidOffer{get;set;}
        public bool CanBid{get;set;}=true;
        public void Select(AgitAuctionOpenRequest shown,uint houseId){}
        public void Apply(AgitAuctionInfoRequest shown){}
        public string? InfoText(AgitAuctionInfoRequest shown)=>null;
        public void Bid(AmountOffer shown,long value)=>Bids.Add((shown,value));
    }
    sealed class ChatHost:Host,IAgitAuctionChatHost {
        public List<string> Chat=[];
        public void ChatNotice(string text)=>Chat.Add(text);
    }

    static AgitBidPanel Open(Host host,int initial,int min,int max,bool text=true){
        var p=new AgitBidPanel();p.Attach(host);
        if(text)p.TextResolver=(k,v)=>k==RefClientTextSource.StringKind&&v==AgitBidPanel.BelowMinimumString?BelowText:null;
        p.Load(UiLayout.Parse(Xml,"AgitAuctionUI.xml"),id=>Surfs.GetValueOrDefault(id),_=>null);
        host.BidOffer=new(0x1234,initial,min,max,AgitBidPanel.ApplyHtml,"agapply 458757",[],1,1);
        p.Update(At(0));
        return p;
    }
    static void Click(AgitBidPanel p,string button,UiNullCommands c){var at=Center(p.Window!.At(p.Window.Find(button)!));p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static void Type(AgitBidPanel p,string s,UiNullCommands c){while(p.Input.Length>0)p.Key(new(Keys.Back,true,false),c);foreach(char ch in s)p.Char(ch);}

    [WorldModuleTest("bid-step")]
    static void RunAll(WorldModuleTestContext t){
        Steps(t);
        Hold(t);
        Ok(t);
    }

    // 1. Minus／Plus：±1，Minus ≤ 最小 → 最小，Plus ≥ 最大 → 最大，再夾在 [最小, 最大]。
    static void Steps(WorldModuleTestContext t){
        var host=new Host();var cmd=new UiNullCommands();
        var p=Open(host,105,100,110);
        t.Check(p.Visible&&p.Input=="105","opens with the suggested value");
        Click(p,"Plus_Button",cmd);t.Equal("106",p.Input,"Plus +1");
        Click(p,"Minus_Button",cmd);Click(p,"Minus_Button",cmd);t.Equal("104",p.Input,"Minus −1 twice");
        t.Check(p.Unhandled.Count==0,"Minus/Plus no longer logged as not wired",p.Unhandled);
        Type(p,"110",cmd);Click(p,"Plus_Button",cmd);t.Equal("110",p.Input,"Plus at the maximum stays at the maximum");
        Type(p,"999",cmd);Click(p,"Plus_Button",cmd);t.Equal("110",p.Input,"Plus above the maximum → maximum");
        Type(p,"100",cmd);Click(p,"Minus_Button",cmd);t.Equal("100",p.Input,"Minus at the minimum stays at the minimum");
        Type(p,"7",cmd);Click(p,"Minus_Button",cmd);t.Equal("100",p.Input,"Minus below the minimum → minimum");
        Type(p,"7",cmd);Click(p,"Plus_Button",cmd);t.Equal("100",p.Input,"Plus below the minimum: +1 then clamped to the minimum");
        Type(p,"",cmd);Click(p,"Plus_Button",cmd);t.Equal("100",p.Input,"empty edit counts as 0 → clamped to the minimum (推測)");
        Type(p,"500",cmd);Click(p,"Minus_Button",cmd);t.Equal("110",p.Input,"Minus above the maximum: −1 then clamped to the maximum");
        t.Check(host.Bids.Count==0,"stepping never sends");
        // 最小值 0 → Lower＝1（AmountOffer.Lower）。
        var low=Open(new Host(),1,0,5);Type(low,"1",cmd);Click(low,"Minus_Button",cmd);
        t.Equal("1",low.Input,"Min 0: the lower bound is AmountOffer.Lower = 1");
        // 不能回答的 S136（Max < Lower）：不動。
        var dead=Open(new Host(),0,10,5);
        t.Check(dead.Visible&&dead.Input==""&&!dead.Step(1)&&!dead.Step(-1)&&dead.Input=="","unanswerable offer: Minus/Plus do nothing");
        // 大數字：long 運算、不溢位。
        var big=Open(new Host(),2_000_000_000,100_000,int.MaxValue);
        t.Check(big.Step(1)&&big.Input=="2000000001","+1 near int.MaxValue");
        Type(big,int.MaxValue.ToString(),cmd);t.Check(big.Step(1)&&big.Input==int.MaxValue.ToString(),"Plus at int.MaxValue stays");
        p.Close();t.Check(!p.Step(1),"closed window: no step");
    }

    // 2. 按住連續：按下後第一個 Update 起算，RepeatDelayMs 後每 RepeatIntervalMs 一步；移出按鈕暫停；放開時照舊再走一步。
    static void Hold(WorldModuleTestContext t){
        var host=new Host();var cmd=new UiNullCommands();
        var p=Open(host,1000,100,100_000);
        var plus=Center(p.Window!.At(p.Window.Find("Plus_Button")!));
        p.Mouse(M(UiMouseKind.Down,plus),cmd);
        t.Equal(1,p.HeldDirection,"Plus held → direction +1");
        p.Update(At(10_000));t.Equal("1000",p.Input,"first Update after the press only starts the clock");
        p.Update(At(10_000+AgitBidPanel.RepeatDelayMs-1));t.Equal("1000",p.Input,"before the repeat delay: no step");
        p.Update(At(10_000+AgitBidPanel.RepeatDelayMs));t.Equal("1001",p.Input,"repeat delay reached: one step");
        p.Update(At(10_000+AgitBidPanel.RepeatDelayMs+AgitBidPanel.RepeatIntervalMs*3));t.Equal("1004",p.Input,"one step per interval");
        p.Update(At(10_000+AgitBidPanel.RepeatDelayMs+AgitBidPanel.RepeatIntervalMs*1000));
        t.Equal((1004+AgitBidPanel.MaxRepeatPerUpdate).ToString(),p.Input,"a long stall is capped at MaxRepeatPerUpdate steps");
        string before=p.Input;
        p.Update(At(10_000+AgitBidPanel.RepeatDelayMs+AgitBidPanel.RepeatIntervalMs*1000+1));t.Equal(before,p.Input,"after the cap the schedule restarts from now");
        p.Mouse(M(UiMouseKind.Move,new(5,110)),cmd);
        t.Equal(0,p.HeldDirection,"pointer left the button → not held");
        p.Update(At(200_000));t.Equal(before,p.Input,"no repeat while outside");
        p.Mouse(M(UiMouseKind.Move,plus),cmd);
        p.Update(At(200_001));p.Update(At(200_001+AgitBidPanel.RepeatDelayMs-1));t.Equal(before,p.Input,"back on the button: the delay starts again");
        p.Mouse(M(UiMouseKind.Up,plus),cmd);
        t.Equal((long.Parse(before)+1).ToString(),p.Input,"release on the button steps once (514)");
        t.Equal(0,p.HeldDirection,"released → not held");
        p.Update(At(300_000));t.Equal((long.Parse(before)+1).ToString(),p.Input,"no repeat after release");
        // Minus 按住到最小值就停在最小值。
        var m=Open(new Host(),103,100,200);
        var minus=Center(m.Window!.At(m.Window.Find("Minus_Button")!));
        m.Mouse(M(UiMouseKind.Down,minus),cmd);t.Equal(-1,m.HeldDirection,"Minus held → direction −1");
        m.Update(At(0));m.Update(At(AgitBidPanel.RepeatDelayMs+AgitBidPanel.RepeatIntervalMs*8));
        t.Equal("100",m.Input,"held Minus stops at the minimum");
        // 按住期間關窗：不再連續。
        m.Close();t.Equal(0,m.HeldDirection,"closed → not held");
        // Ok 按住不連續。
        var o=Open(new Host(),150,100,200);var ok=Center(o.Window!.At(o.Window.Find("Ok_Button")!));
        o.Mouse(M(UiMouseKind.Down,ok),cmd);t.Equal(0,o.HeldDirection,"Ok held: no repeat direction");
        o.Update(At(0));o.Update(At(AgitBidPanel.RepeatDelayMs*4));t.Equal("150",o.Input,"Ok held: value unchanged");
    }

    // 3. Ok：低於最小 → 設最小＋4381（聊天欄）；高於最大 → 設最大；兩者都不送；範圍內照舊送。
    static void Ok(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var host=new ChatHost();var p=Open(host,150,100,200);
        Type(p,"5",cmd);Click(p,"Ok_Button",cmd);
        t.Check(host.Bids.Count==0&&p.Input=="100","Ok below the minimum: not sent, edit = minimum",p.Input);
        t.Check(host.Chat.Count==1&&host.Chat[0]==BelowText&&p.Notice==BelowText,"string 4381 to the chat bar",host.Chat);
        Type(p,"",cmd);p.Key(new(Keys.Enter,true,false),cmd);
        t.Check(host.Bids.Count==0&&p.Input=="100"&&host.Chat.Count==2,"empty edit + Enter: counts as 0 → minimum + 4381");
        Type(p,"999",cmd);Click(p,"Ok_Button",cmd);
        t.Check(host.Bids.Count==0&&p.Input=="200"&&host.Chat.Count==2,"Ok above the maximum: not sent, edit = maximum, no 4381");
        Click(p,"Ok_Button",cmd);
        t.Check(host.Bids.Count==1&&host.Bids[0].Value==200,"Ok at the maximum sends",host.Bids);
        // T429（#986）：送出後關窗（FU15§2.10），下一筆 S136 重開。
        host.BidOffer=host.BidOffer! with{Revision=2};p.Update(At(0));
        Type(p,"100",cmd);Click(p,"Ok_Button",cmd);
        t.Check(host.Bids.Count==2&&host.Bids[1].Value==100&&host.Chat.Count==2,"Ok at the minimum sends, no 4381");
        host.BidOffer=host.BidOffer! with{Revision=3};p.Update(At(0));
        host.CanBid=false;Type(p,"5",cmd);Click(p,"Ok_Button",cmd);
        t.Check(host.Bids.Count==2&&p.Input=="100"&&host.Chat.Count==3,"host cannot send: still clamps and prints 4381 (sub_545920 checks the value first)");
        // 宿主沒有聊天欄輸出（產品的 AgitAuctionHost 自 T429／#986 起已實作，這裡用合成宿主）：記一次 log，Notice 留字。
        var plain=new Host();var q=Open(plain,150,100,200);
        Type(q,"5",cmd);Click(q,"Ok_Button",cmd);
        t.Check(q.Input=="100"&&q.Notice==BelowText&&q.Unhandled.Contains("4381-chat")&&plain.Bids.Count==0,"no chat host: logged once, Notice kept",q.Unhandled);
        // 沒有參考客戶端字串：不印、不編字。
        var none=new ChatHost();var n=Open(none,150,100,200,text:false);
        Type(n,"5",cmd);Click(n,"Ok_Button",cmd);
        t.Check(n.Input=="100"&&n.Notice==null&&none.Chat.Count==0&&n.Unhandled.Contains("4381-text"),"no string 4381: nothing printed, logged");
        // 新的 S136 清掉 Notice。
        none.BidOffer=none.BidOffer! with{Revision=2};n.Update(At(0));
        t.Check(n.Visible&&n.Input=="150","next agapply reopens with the suggested value");
        t.Equal(AgitBidPanel.BelowMinimumString,"4381","FU15§2.10 string id");
    }
}
