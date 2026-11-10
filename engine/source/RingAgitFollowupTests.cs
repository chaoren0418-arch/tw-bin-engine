using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R7-C（T342／#805）ring-agit-followup 離線測試：戒指開窗不查限制地圖、盟屋拍賣 C125 select／apply、AuctionInfo_Window（S39 agsel）、
// Bid_Window（S136 agapply）。只用合成封包、合成版面（自寫的小 XML，結構同 AgitAuctionUI.xml）與合成單色圖；
// 不讀主客戶端或參考客戶端、不開遊戲、網路或視窗。CI 由 --module-tests 自動發現執行。
static class RingAgitFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Board=0x12345678,House=7u<<16|5,Other=7u<<16|6;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static string Hex(byte[]? b)=>b==null?"(null)":Convert.ToHexString(b);
    static byte[] Ability(byte type,byte on)=>P([36,type,on]);
    static byte[] Appear(uint id,ushort x,string name)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),
        Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static OwnStatus Status(int level)=>JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{Me},\"Level\":{level},\"Charisma\":25,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}}")!;
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();

    static LoginConnection Seed(List<byte[]> log,params int[] skills){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",Status(44));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");
        var learned=(HashSet<int>)typeof(WorldSession).GetField("learnedMagic",F)!.GetValue(c.World)!;foreach(int s in skills)learned.Add(s);
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }

    /// <summary>S156：C(156) D(board) H(n) {D(id) S(name) H(area) C(month) C(day) D(price)}。</summary>
    static byte[] HouseList(uint board,params uint[] houses)=>P([156],Le32(board),Le16((ushort)houses.Length),
        houses.SelectMany(h=>(byte[])[..Le32(h),..Str("屋"+(h&0xFFFF)),..Le16(78),12,25,..Le32(2_000_000)]).ToArray());
    /// <summary>S39 S_AuctionBoardRead：C(39) D(board) S(html) S(house) H(n) S×n（server-protocol.md）。</summary>
    static byte[] Agsel(uint board,string house,string html="agsel",int n=9)=>P([39],Le32(board),Str(html),Str(house),Le16((ushort)n),
        Enumerable.Range(0,n).SelectMany(i=>Str("v"+i)).ToArray());

    [WorldModuleTest("ring-agit-followup")]
    static void RunAll(WorldModuleTestContext t){
        try{
            RingBlockedMap(t);
            Packets(t);
            AgselObserver(t);
            Senders(t);
            ListPanel(t);
            InfoPanel(t);
            BidPanel(t);
            Wiring(t);
        }finally{AgitAuctionWindow.Clear();AgitAuctionInfoWindow.Clear();}
    }

    // 1. 戒指：限制地圖（5、6、83、84、≥0x4000）也開窗、不顯示 276（#736 0xE08B22）。
    static void RingBlockedMap(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log,5,69);var w=c.World;
        string blocked=ServerMessageText.Decode(RingWindowRules.TeleportBlockedMessage,[]).Text;
        w.Receive(Ability(1,1),"offline");
        foreach(ushort map in new ushort[]{5,6,83,84,0x4000}){
            Set(w,"MapId",map);w.SelectMagic(5);
            t.Check(w.TryOpenRingWindow()&&w.TeleportDestination is {Skill:5}&&log.Count==0,$"map {map}: ring opens the destination window, nothing sent");
            t.Check(w.MagicNotice!=blocked,$"map {map}: no message 276",w.MagicNotice);
            c.CloseTeleportDestination(w.TeleportDestination!.Revision);
        }
        t.Check(RingWindowRules.TeleportBlocked(84)&&RingWindowRules.TeleportBlockedMessage==276,"rule constants kept for the direct C6 path");
        // 沒戴戒指：照舊不攔截。
        w.Receive(Ability(1,0),"offline");
        t.Check(!w.TryOpenRingWindow()&&w.TeleportDestination==null,"no ring: no interception");
        w.Reset();
    }

    // 2. C125 apply：C(125) D(board) S("apply") S("%u")，補到 8 的倍數。select 不變。
    static void Packets(WorldModuleTestContext t){
        t.Equal("7D78563412"+"6170706C7900"+"34353837353700"+"000000000000",Hex(AgitSelectPackets.Apply(Board,House)),"C125 D(board) S(apply) S(458757), padded to 24");
        t.Equal("7D78563412"+"73656C65637400"+"34353837353700"+"0000000000",Hex(AgitSelectPackets.Select(Board,House)),"select unchanged");
        t.Throws<ArgumentException>(()=>AgitSelectPackets.Apply(0,House),"apply: board 0 rejected");
        t.Check(AgitAuctionInfoPackets.TryParse(Agsel(Board,"458757")) is {BoardId:Board,HouseNumber:"458757",Data.Length:9},"agsel parsed");
        t.Check(AgitAuctionInfoPackets.TryParse(Agsel(Board,"458757","agapply"))==null,"other S39 page → null");
        t.Check(AgitAuctionInfoPackets.TryParse(Agsel(0,"458757"))==null,"board 0 → null");
        var trunc=Agsel(Board,"458757");trunc=trunc[..^9];
        t.Check(AgitAuctionInfoPackets.TryParse(trunc)==null,"truncated agsel → null (no exception)");
        t.Check(AgitAuctionInfoPackets.TryParse([39])==null,"empty S39 → null");
        t.Check(new AgitAuctionInfoRequest(1,Board,"458757",[]).HouseId==House&&new AgitAuctionInfoRequest(1,Board,"x1",[]).HouseId==null,"house id parsed from the decimal string");
    }

    // 3. S39 agsel 觀察者：送出開窗請求；NPC 對話照舊處理同一個封包；Reset 清掉。
    static void AgselObserver(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;
        AgitAuctionInfoWindow.Clear();
        var reg=WorldExtensionRegistry.Default;
        t.Check(reg.Errors.Count==0&&reg.Entries.Any(e=>e.Kind=="observe"&&e.Opcode==39&&e.Method=="WorldSession.ObserveAgitAuctionInfo"),"S39 agsel observer registered (auto discovery)",reg.Errors);
        w.Receive(Agsel(Board,"458757"),"offline");   // 3.81 S_AuctionBoardRead 的真實格式：NPC 對話不可丟 ProtocolException
        t.Check(true,"real agsel layout accepted by the NPC dialog parser (no ProtocolException)");
        var info=AgitAuctionInfoWindow.Current;
        t.Check(info is {BoardId:Board,HouseNumber:"458757"}&&info.Data.SequenceEqual(Enumerable.Range(0,9).Select(i=>"v"+i)),"agsel posts an info request",info);
        long rev=info!.Revision;
        w.Receive(P([39],Le32(Board),Str("y_other"),Le16(0),Le16(0)),"offline");   // 一般 S39：D S(html) H(flag) H(count)
        t.Check(AgitAuctionInfoWindow.Current?.Revision==rev,"other S39 page posts nothing");
        w.Reset();
        t.Check(AgitAuctionInfoWindow.Current==null,"reset clears the info request");
    }

    // 4. 送出端：select／apply 只對目前的 S156／agsel、清單裡的盟屋送一次。
    static void Senders(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;
        AgitAuctionWindow.Clear();AgitAuctionInfoWindow.Clear();
        w.Receive(HouseList(Board,House,Other),"offline");
        var shown=AgitAuctionWindow.Current!;
        t.Check(shown.List.BoardId==Board&&w.AgitHouseList?.BoardId==Board,"S156 posted and stored");
        t.Check(Run(c.SendAgitAuctionSelect(shown,House)),"select admitted");
        t.Equal(Hex(AgitSelectPackets.Select(Board,House)),Hex(log.LastOrDefault()),"sent C125 D(board) S(select) S(458757)");
        t.Check(!Run(c.SendAgitAuctionSelect(shown,9u<<16|1))&&log.Count==1,"house not in the list: not sent");
        Set(w,"TeleportState","recovery");
        t.Check(w.ActionLocked&&!Run(c.SendAgitAuctionSelect(shown,Other))&&log.Count==1,"action locked (teleport pending): not sent");
        Set(w,"TeleportState","idle");
        w.Receive(Agsel(Board,House.ToString()),"offline");
        var info=AgitAuctionInfoWindow.Current!;
        t.Check(Run(c.SendAgitAuctionApply(info)),"apply admitted");
        t.Equal(Hex(AgitSelectPackets.Apply(Board,House)),Hex(log.LastOrDefault()),"sent C125 D(board) S(apply) S(458757)");
        t.Check(!Run(c.SendAgitAuctionApply(info with{HouseNumber="abc"}))&&log.Count==2,"non-numeric house: not sent");
        w.Receive(Agsel(Board,Other.ToString()),"offline");
        t.Check(!Run(c.SendAgitAuctionApply(info))&&log.Count==2,"superseded agsel: old page not sent");
        t.Check(!Run(c.SendAgitAuctionApply(AgitAuctionInfoWindow.Current! with{BoardId=Board+1}))&&log.Count==2,"other board: not sent");
        w.Receive(HouseList(Board,Other),"offline");
        t.Check(!Run(c.SendAgitAuctionSelect(shown,Other))&&log.Count==2,"superseded S156: old list not sent");
        w.Reset();
        t.Check(!Run(c.SendAgitAuctionSelect(shown,House))&&log.Count==2,"after reset: not sent");
    }

    // ---- panels (synthetic layout, same element structure as AgitAuctionUI.xml) ----
    const string Xml="""
        <?xml version="1.0"?>
        <AgitAuctionUI>
          <Window Name = "Bid_Window" X="0" Y="0" BaseSurf = "800;1" FrameSurf = "801;1" Moveable = "20" Modal="1" MouseEvent="Dummy">
            <Label Name = "PriceTitle_Label" X = "21" Y = "25" Width = "148" Height = "12" String = "4029;1" FontColor = "168,144,136"/>
            <Label Name = "CurPrice_Label" X = "19" Y = "44" Width = "148" Height = "12" Align="center"/>
            <Button Name = "Minus_Button" X="50" Y = "65" BaseSurf="810;1" PushSurf="811;1" MouseEvent="Minus_BidWin"/>
            <Edit Name = "Price_Edit" X = "65" Y = "64" Width = "56" Align="center" Height = "13" LimitText = "10" OnlyNumber = "1" MouseEvent = "PriceEdit_BidWin"/>
            <Button Name = "Plus_Button" X="124" Y = "65" BaseSurf="810;1" PushSurf="811;1" MouseEvent="Plus_BidWin"/>
            <Button Name = "Ok_Button" X="34" Y="88" BaseSurf="812;1" HighlightSurf="813;1" PushSurf="811;1" MouseEvent="Ok_BidWin"/>
            <Button Name = "Cancel_Button" X="99" Y="88" BaseSurf="812;1" PushSurf="811;1" MouseEvent="Cancel_BidWin"/>
            <Button Name = "Close_Button" X="165" Y="9" BaseSurf="814;1" PushSurf="999;1" MouseEvent="Exit_BidWin"/>
          </Window>
          <Window Name = "AuctionInfo_Window" X = "171" Y = "153" Activate = "0" DarkBack = "1" MouseEvent = "Dummy">
            <MultiLabel Name = "Info_Label" X = "10" Y = "0" Width = "260" Height = "90" LineMargin = "2"/>
            <Button Name = "Bid_Button" X = "82" Y = "96" BasePng = "820" HighlightPng = "821" PushPng = "822;1" MouseEvent = "Bid_AuctionWin"/>
          </Window>
          <Window Name = "Agitmap_Window" X = "100" Y = "50" Width = "300" Height = "200" Moveable = "20" BaseSurf = "900" MouseEvent = "Dummy">
            <Button Name = "Exit_Button" X = "280" Y = "4" BasePng = "910;1" MouseEvent = "Exit_AgitWin"/>
            <Control Name = "Auction_List" X = "2" Y = "100">
              <List Name = "Items_List" X = "0" Y = "21" Width = "207" Height = "24" Vertical = "1" Gap = "0">
                <CheckButton Name = "Item0_Button" Width ="180" Height="12" MouseEvent = "Item_AuctionList">
                  <Label Name="Name_Label" X = "0" Y = "0" Width = "77" Height = "12" Caption = "Name;1"/>
                </CheckButton>
                <CheckButton Name = "Item1_Button" Width ="180" Height="12" MouseEvent = "Item_AuctionList">
                  <Label Name="Name_Label" X = "0" Y = "0" Width = "77" Height = "12" Caption = "Name;1"/>
                </CheckButton>
              </List>
            </Control>
            <Control Name = "MapImage_Control">
              <Control Name = "Adenmap_Image">
                <Button Name = "Agithouse4_Image" X = "260" Y = "30" BasePng = "950;1" MouseEvent = "Auction_AgitWin"/>
              </Control>
            </Control>
          </Window>
        </AgitAuctionUI>
        """;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint BidBase=0xFF203040,BidOk=0xFF00AA00,BidOkHi=0xFF00FF00;
    static readonly Dictionary<int,UiSurface> Surfs=new(){[800]=Surface(800,190,120,BidBase),[801]=Surface(801,4,4,0xFF0A0A0A),
        [810]=Surface(810,12,12,0xFF808080),[811]=Surface(811,12,12,0xFF404040),[812]=Surface(812,50,18,BidOk),[813]=Surface(813,50,18,BidOkHi),[814]=Surface(814,12,12,0xFF800000),
        [900]=Surface(900,300,200,0xFF102030)};
    static readonly Dictionary<int,UiSurface> Pngs=new(){[820]=Surface(820,60,18,0xFFAA5500),[821]=Surface(821,60,18,0xFFFF8800),[822]=Surface(822,60,18,0xFF553300),
        [910]=Surface(910,12,12,0xFF00FF00),[950]=Surface(950,8,8,0xFF800000)};
    static UiLayout Layout(string text=Xml)=>UiLayout.Parse(text,"AgitAuctionUI.xml");
    static readonly UiStaticGameState InWorld=new(InWorld:true),Outside=new();
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,Rectangle r,UiNullCommands? c=null){c??=new();var at=new Point(r.X+r.Width/2,r.Y+r.Height/2);p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static UiKeyEvent K(Keys k)=>new(k,true,false);
    static int Pixel(IUiPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    sealed class Host:IAgitAuctionHost {
        public List<(AgitAuctionOpenRequest Shown,uint House)> Selects=[];
        public List<AgitAuctionInfoRequest> Applies=[];
        public List<(AmountOffer Offer,long Value)> Bids=[];
        public string? Info="甲屋\n亞丁 第 5 間\n價格 2000000";
        public AmountOffer? BidOffer{get;set;}
        public bool CanBid{get;set;}=true;
        public void Select(AgitAuctionOpenRequest shown,uint houseId)=>Selects.Add((shown,houseId));
        public void Apply(AgitAuctionInfoRequest shown)=>Applies.Add(shown);
        public string? InfoText(AgitAuctionInfoRequest shown)=>Info;
        public void Bid(AmountOffer shown,long value)=>Bids.Add((shown,value));
    }
    static AgitHouseList List(uint board=Board)=>new(board,[new(House,"甲屋",78,12,25,2_000_000),new(Other,"乙屋",78,12,25,3_000_000)]);

    // 5. 拍賣清單窗：點列／盟屋鈕 → 宿主收到 select（S156 看板＋盟屋 id）；沒有宿主照舊只記 log。
    static void ListPanel(WorldModuleTestContext t){
        AgitAuctionOpenRequest? req=new(500,List());
        var p=new AgitAuctionPanel(null,()=>req);p.Load(Layout(),id=>Surfs.GetValueOrDefault(id),id=>Pngs.GetValueOrDefault(id));
        t.Check(p.Available,"list window loads",p.Unavailable);
        p.Update(InWorld);
        var row0=p.Pieces.First(x=>x.Kind==AgitAuctionPieceKind.Row&&x.Row==0);
        Click(p,p.At(row0));
        t.Check(p.SelectedHouse==House&&p.Unhandled.Contains("select"),"no host: row selects, select logged");
        var host=new Host();p.Attach(host);
        var row1=p.Pieces.First(x=>x.Kind==AgitAuctionPieceKind.Row&&x.Row==1);
        Click(p,p.At(row1));
        t.Check(host.Selects.Count==1&&host.Selects[0].Shown==req&&host.Selects[0].House==Other&&p.SelectedHouse==Other,"row 1 → host.Select(S156, 乙屋)",host.Selects);
        var houseButton=p.Pieces.First(x=>x.HouseId==House);
        Click(p,p.At(houseButton));
        t.Check(host.Selects.Count==2&&host.Selects[1].House==House,"house button → host.Select(甲屋)");
        Click(p,p.At(p.Pieces.First(x=>x.HouseId==House&&x.Kind==AgitAuctionPieceKind.Button)) with{X=0,Y=0});
        t.Check(host.Selects.Count==2,"click outside the window sends nothing");
    }

    // 6. 拍賣資訊窗（AuctionInfo_Window）。
    static void InfoPanel(WorldModuleTestContext t){
        AgitAuctionOpenRequest? auction=new(600,List());AgitAuctionInfoRequest? info=null;
        var p=new AgitAuctionInfoPanel(null,()=>auction,()=>info);var host=new Host();p.Attach(host);var cmd=new UiNullCommands();
        p.Load(Layout(),id=>Surfs.GetValueOrDefault(id),id=>Pngs.GetValueOrDefault(id));
        t.Check(p.Available&&p.Window is {Origin.X:171,Origin.Y:153},"info window loads at XML X/Y (171,153)",p.Unavailable);
        t.Equal(new Size(270,114),p.Window!.Size,"no declared size / base image: union of Info_Label (10..270 × 0..90) and Bid_Button (82,96 60×18) (推測)");
        t.Check(p is IUiClosablePanel&&p.Layer==UiLayer.Window,"closable window");
        p.Update(InWorld);t.Check(!p.Visible,"no agsel: closed");
        info=new(1,Board,House.ToString(),["甲屋"]);p.Update(InWorld);
        t.Check(p.Visible&&p.Shown==info&&p.Text==host.Info,"agsel for the shown board opens with the host text");
        t.Equal(new Rectangle(171,153,270,114),p.Bounds,"bounds");
        t.Equal(Argb(0xFFAA5500),Pixel(p,171+82+5,153+96+5),"Bid_Button drawn");
        Click(p,p.Window.At(p.Window.Find("Bid_Button")!),cmd);
        t.Check(host.Applies.Count==1&&host.Applies[0]==info,"Bid_Button → host.Apply(agsel)");
        t.Check(p.Key(K(Keys.Escape),cmd)&&!p.Visible&&cmd.Calls.Contains("game-focus"),"Esc closes and returns focus");
        p.Update(InWorld);t.Check(!p.Visible,"closed page is not reopened");
        info=new(2,Board+1,House.ToString(),[]);p.Update(InWorld);
        t.Check(!p.Visible,"agsel of another board: not shown here");
        info=new(3,Board,House.ToString(),[]);p.Update(InWorld);
        t.Check(p.Visible,"next agsel reopens");
        auction=null;p.Update(InWorld);
        t.Check(!p.Visible,"auction window closed → info closes");
        auction=new(601,List());info=new(4,Board,House.ToString(),[]);host.Info=null;p.Update(InWorld);
        t.Check(p.Visible&&p.Text==null&&p.Unhandled.Contains("agsel-text"),"missing template: window opens, Info_Label not drawn, logged");
        p.Update(Outside);t.Check(!p.Visible,"leaving the world closes");
        var noHost=new AgitAuctionInfoPanel(null,()=>auction,()=>new(9,Board,"1",[]));noHost.Load(Layout(),id=>Surfs.GetValueOrDefault(id),id=>Pngs.GetValueOrDefault(id));
        noHost.Update(InWorld);Click(noHost,noHost.Window!.At(noHost.Window.Find("Bid_Button")!));
        t.Check(noHost.Unhandled.Contains("apply"),"no host: Bid logged");
        var broken=new AgitAuctionInfoPanel(null,()=>auction,()=>info);broken.Load(Layout(Xml.Replace("MultiLabel Name = \"Info_Label\"","Label Name = \"Info_Label\"")),id=>Surfs.GetValueOrDefault(id),id=>Pngs.GetValueOrDefault(id));
        t.Check(!broken.Available,"Info_Label not a MultiLabel → unavailable",broken.Unavailable);
        t.Throws<UiLayoutException>(()=>new AgitAuctionInfoPanel().Load(Layout(Xml.Replace("X = \"171\" Y = \"153\"","Y = \"153\"")),_=>null,_=>null),"no X → no invented origin");
        var none=new AgitAuctionInfoPanel();none.Load(Layout(),(SurfCatalog)null!);
        t.Check(!none.Available&&none.Unavailable=="沒有 UiResources","no UiResources → unavailable");
    }

    // 7. 出價窗（Bid_Window，S136 agapply）。
    static void BidPanel(WorldModuleTestContext t){
        var p=new AgitBidPanel();var host=new Host();p.Attach(host);var cmd=new UiNullCommands();
        p.Load(Layout(),id=>Surfs.GetValueOrDefault(id),id=>Pngs.GetValueOrDefault(id));
        t.Check(p.Available&&p.Window is {Origin.X:0,Origin.Y:0,MoveableHeight:20},"bid window loads at XML X/Y (0,0)",p.Unavailable);
        t.Equal(new Size(190,120),p.Window!.Size,"size = BaseSurf image");
        t.Check(p.Missing.Count==1&&p.Missing[0].StartsWith("surf 999 ",StringComparison.Ordinal),"missing image listed, not drawn",p.Missing);
        t.Check(p is IUiClosablePanel&&p is IUiMovablePanel&&p is IUiTextInput,"closable, movable, text input");
        t.Check(p.Window.Find("PriceTitle_Label") is {TextKind:"String",TextValue:"4029;1"} title&&title.Color==Color.FromArgb(168,144,136),"title = String 4029 with FontColor");
        p.Update(InWorld);t.Check(!p.Visible,"no offer: closed");
        host.BidOffer=new(Board,100000,100000,2000000000,"request","request 1",[],5,1);p.Update(InWorld);
        t.Check(!p.Visible,"S136 of another page: not the bid window");
        var offer=new AmountOffer(Board,100000,100000,2000000000,"agapply","agapply 458757",[],6,1);
        host.BidOffer=offer;p.Update(InWorld);
        t.Check(p.Visible&&p.Shown==offer&&p.Input=="100000","agapply opens with the suggested value");
        t.Equal(Argb(BidBase),Pixel(p,100,30),"base image drawn");
        t.Equal(Argb(BidOk),Pixel(p,34+3,88+3),"Ok button drawn");
        t.Check(p.Char('5')&&p.Input=="1000005","digit typed");
        t.Check(!p.Char('a')&&p.Input=="1000005","letters refused");
        t.Check(p.Key(K(Keys.Back),cmd)&&p.Input=="100000","Backspace");
        for(int i=0;i<8;i++)p.Char('9');
        t.Equal(10,p.Input.Length,"LimitText 10");
        while(p.Input.Length>0)p.Key(K(Keys.Back),cmd);
        p.Char('5');
        Click(p,p.Window.At(p.Window.Find("Ok_Button")!),cmd);
        t.Check(host.Bids.Count==0&&p.Visible,"below the lower bound: Ok sends nothing");
        p.Key(K(Keys.Back),cmd);foreach(char ch in "150000")p.Char(ch);
        host.CanBid=false;Click(p,p.Window.At(p.Window.Find("Ok_Button")!),cmd);
        t.Check(host.Bids.Count==0,"host cannot send: nothing");
        host.CanBid=true;Click(p,p.Window.At(p.Window.Find("Ok_Button")!),cmd);
        t.Check(host.Bids.Count==1&&host.Bids[0]==(offer,150000L),"Ok → host.Bid(offer, 150000)",host.Bids);
        t.Check(p.Key(K(Keys.Enter),cmd)&&host.Bids.Count==2,"Enter = Ok");
        Click(p,p.Window.At(p.Window.Find("Plus_Button")!),cmd);
        t.Check(p.Unhandled.Contains("Plus_BidWin")&&p.Input=="150000","Plus: step not traced, logged, value unchanged");
        Click(p,p.Window.At(p.Window.Find("Cancel_Button")!),cmd);
        t.Check(!p.Visible&&host.Bids.Count==2&&cmd.Calls.Contains("game-focus"),"Cancel closes, nothing sent");
        p.Update(InWorld);t.Check(!p.Visible,"cancelled offer not reopened");
        var next=offer with{Revision=7};host.BidOffer=next;p.Update(InWorld);
        t.Check(p.Visible&&p.Shown==next,"next agapply reopens");
        t.Check(p.Key(K(Keys.Escape),cmd)&&!p.Visible,"Esc closes");
        host.BidOffer=offer with{Revision=8};p.Update(InWorld);
        host.BidOffer=null;p.Update(InWorld);
        t.Check(!p.Visible,"offer answered or gone → closes");
        host.BidOffer=offer with{Revision=9};p.Update(InWorld);
        t.Check(p.IsDragHandle(new(100,5))&&!p.IsDragHandle(new(100,50)),"Moveable 20 px strip drags");
        p.MoveTo(new(200,150));t.Equal(new Rectangle(200,150,190,120),p.Bounds,"moved");
        p.Close();t.Check(!p.Visible&&p.Bounds.IsEmpty,"IUiClosablePanel.Close");
        host.BidOffer=offer with{Revision=10,Max=0};p.Update(InWorld);
        t.Check(p.Visible&&p.Input==""&&!p.Char('1')&&!p.CanSubmit,"unanswerable offer: shown, no input");
        var broken=new AgitBidPanel();broken.Load(Layout(Xml.Replace("<Edit Name = \"Price_Edit\"","<Label Name = \"Price_Edit\"")),id=>Surfs.GetValueOrDefault(id),id=>Pngs.GetValueOrDefault(id));
        t.Check(!broken.Available,"no Price_Edit → unavailable",broken.Unavailable);
        var noImage=new AgitBidPanel();noImage.Load(Layout(),_=>null,_=>null);
        t.Check(noImage.Available&&noImage.Window!.Size==new Size(169,77),"base image missing: size from the children, nothing substituted",noImage.Window?.Size);
    }

    // 8. 自動發現、快照說明。
    static void Wiring(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Panels.Count(r=>r.Id==AgitAuctionInfoPanel.PanelId)==1&&UiPanelRegistry.Panels.Count(r=>r.Id==AgitBidPanel.PanelId)==1,"both windows registered once");
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==AgitBidPanel.PanelId).Create(new UiRoot()) is AgitBidPanel,"registration creates AgitBidPanel");
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==AgitAuctionInfoPanel.PanelId).Create(new UiRoot()) is AgitAuctionInfoPanel,"registration creates AgitAuctionInfoPanel");
        t.Check(UiPanelRegistry.Panels.Select(r=>r.Id).Distinct().Count()==UiPanelRegistry.Panels.Count,"registry ids stay unique");
        var w=new WorldSession();
        var snap=JsonSerializer.SerializeToElement(typeof(WorldSession).GetMethod("AgitSnapshot",F)!.Invoke(w,null));
        string ui=snap.GetProperty("ui").GetString()!;
        t.Check(!ui.Contains("data only (XML missing)")&&ui.Contains("Bid_Window")&&ui.Contains("AuctionInfo_Window"),"Agit.cs:111 snapshot text corrected",ui);
    }
}
