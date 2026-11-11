using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// T435（#993，classic-ui-audit §3 G12–G15）native-yield 離線測試：原生 UI 開著時 WinForms 完全讓位、原生窗之間不重複。
// 只用合成封包、記憶體內假傳送端與假宿主；不讀主客戶端或參考客戶端、不開遊戲、網路或視窗。CI 由 --module-tests 自動發現執行。
static class NativeYieldTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Npc=456;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static byte[] BuyList(params (uint id,ushort icon,uint price,string name)[] rows)=>Packet(b=>{b.Write((byte)70);b.Write(Npc);b.Write((ushort)rows.Length);
        foreach(var r in rows){b.Write(r.id);b.Write(r.icon);b.Write(r.price);Text(b,r.name);b.Write((byte)0);}if(rows.Length>0)b.Write((ushort)7);});
    static byte[] Dialog(string html)=>Packet(b=>{b.Write((byte)39);b.Write(Npc);Text(b,html);b.Write((ushort)0);b.Write((ushort)0);});
    // 3.81 S_AuctionBoardRead：C39 D(board) S("agsel") S(house) H(n) S×n（RingAgitFollowupTests 同格式）。
    static byte[] Agsel(uint board,string house)=>Packet(b=>{b.Write((byte)39);b.Write(board);Text(b,"agsel");Text(b,house);b.Write((ushort)9);for(int i=0;i<9;i++)Text(b,"v"+i);});
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Npc);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,"NPC");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
        c.World.SelectTarget(Npc);Feed(c,LoginPackets.Pad([5,0]));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Talk(LoginConnection c)=>c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision).GetAwaiter().GetResult();
    static void Close(LoginConnection c)=>c.Interact("close",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision).GetAwaiter().GetResult();
    static readonly UiStaticGameState InWorld=new(InWorld:true),Outside=new();

    [WorldModuleTest("native-yield")]
    static void Run(WorldModuleTestContext t){
        try{
            Registration(t);
            LocationMarker(t);
            BoardConfirm(t);
            ShopYield(t);
            AgselOnce(t);
        }finally{AgitAuctionInfoWindow.Clear();AgitAuctionWindow.Clear();}
    }

    // 0. 兩個新面板都經自動發現註冊一次、層級正確、可關（IUiClosablePanel）。
    static void Registration(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Errors.Count==0,"[UiPanel] registrations valid",UiPanelRegistry.Errors);
        t.Equal(1,UiPanelRegistry.Panels.Count(r=>r.Id==LocationMarkerPanel.PanelId),"G12 marker panel registered once");
        t.Equal(1,UiPanelRegistry.Panels.Count(r=>r.Id==BoardConfirmPromptPanel.PanelId),"G13 board confirm panel registered once");
        var root=new UiRoot();
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==LocationMarkerPanel.PanelId).Create(root) is LocationMarkerPanel{Layer:UiLayer.World},"marker panel: World layer (never hit-tested)");
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==BoardConfirmPromptPanel.PanelId).Create(root) is BoardConfirmPromptPanel{Layer:UiLayer.Float},"board confirm: Float layer");
        t.Check(typeof(IUiClosablePanel).IsAssignableFrom(typeof(LocationMarkerPanel))&&typeof(IUiClosablePanel).IsAssignableFrom(typeof(BoardConfirmPromptPanel)),"both panels are IUiClosablePanel");
    }

    // 1. G12：⊕ 的計算共用一份；原生時 Label 不顯示，改由面板畫在畫布上（World 層，不攔滑鼠）。
    static void LocationMarker(WorldModuleTestContext t){
        var scene=UiRoot.WorldRect;var loc=new SharedLocation(4,32600,32940,0);
        PointF project(SharedLocation _)=>new(scene.Left+100,scene.Top+80);
        t.Check(LocationSharePresentation.Marker(null,4,scene,project)==null,"no selection → no marker");
        var other=LocationSharePresentation.Marker(loc,5,scene,project);
        t.Check(other is {Marker:null}&&other.Notice.StartsWith("分享座標（不同地圖）：",StringComparison.Ordinal),"other map → notice only",other);
        var same=LocationSharePresentation.Marker(loc,4,scene,project);
        var at=new Point(scene.Left+100,scene.Top+80);
        t.Check(same is {Marker:{} sm}&&sm==at&&same.Notice.StartsWith("分享座標：",StringComparison.Ordinal),"same map → marker at the projected point",same);
        t.Equal(new Rectangle(scene.Left+8,scene.Top+8,scene.Width-16,24),same!.NoticeBounds,"notice line inside the scene (logical coordinates when native)");
        var edge=LocationSharePresentation.Marker(loc,4,scene,_=>new(scene.Right+500,scene.Top-500));
        t.Check(edge is {Marker:{} e}&&e==new Point(scene.Right-12,scene.Top+12)&&edge.Notice.StartsWith("分享座標（視野外",StringComparison.Ordinal),"off-screen → clamped edge marker",edge);

        t.Equal((false,false),LocationShareUi.LabelsVisible(same,nativeOwns:true),"native: both WinForms labels hidden");
        t.Equal((true,true),LocationShareUi.LabelsVisible(same,nativeOwns:false),"classic: marker + notice labels");
        t.Equal((false,true),LocationShareUi.LabelsVisible(other,nativeOwns:false),"classic, other map: notice label only");
        t.Equal((false,false),LocationShareUi.LabelsVisible(null,nativeOwns:false),"classic, nothing selected: hidden");

        var panel=new LocationMarkerPanel();LocationMarkerView? current=same;bool closed=false;
        panel.Source=()=>current;panel.Closed=()=>{closed=true;current=null;};
        panel.Update(Outside);t.Check(!panel.Visible,"not in world → not drawn");
        panel.Update(InWorld);t.Check(panel.Visible&&panel.View==same&&panel.Bounds==UiRoot.WorldRect,"in world → drawn over the scene");
        t.Check(!panel.Mouse(new(UiMouseKind.Down,new(scene.Left+100,scene.Top+80),MouseButtons.Left,1,0,Keys.None),new UiNullCommands()),"mouse never handled");
        using(var canvas=new UiCanvas()){
            panel.Paint(canvas);bool ink=false;
            for(int dx=-10;dx<=10&&!ink;dx++)for(int dy=-10;dy<=10&&!ink;dy++)ink=canvas.Bitmap.GetPixel(at.X+dx,at.Y+dy).A>0;
            t.Check(ink,"⊕ painted around the marker point");
        }
        panel.Close();t.Check(closed&&!panel.Visible,"Close → selection cleared through Closed, nothing sent");
        panel.Update(InWorld);t.Check(!panel.Visible,"after Close: stays hidden until a new selection");
        t.Check(!LocationShareProtocol.Format(loc).Contains("⊕"),"chat text unchanged (marker is drawn, not text)");
    }

    sealed class ConfirmHost:IBoardConfirmHost {
        public BoardConfirm? PendingConfirm{get;set;}
        public readonly List<(int Id,bool Accept)> Answers=[];
        public void AnswerConfirm(int id,bool accept){Answers.Add((id,accept));if(PendingConfirm?.Id==id)PendingConfirm=null;}
    }
    sealed class Asking(YesNoPanelView? view):IYesNoSource {public YesNoPanelView? Current=>view;public void Answer(int revision,bool accept){}}

    // 2. G13：原生公告板的發文／刪文確認畫在聊天輸入列上，Y／N／Esc 回答；伺服器詢問時讓位。
    static void BoardConfirm(WorldModuleTestContext t){
        var article=new BoardArticle(77,"offline","11/11","題","內容");
        var list=new BoardView(1,1,1,5,1,Npc,"list","",[],null,300,true,false,false);
        var reading=list with{Kind="article",Article=article};
        var write=BoardLineConfirm.Ask(1,"write",list);
        t.Check(write is {Command:"write",Topic:null}&&write.Text.Contains("300")&&write.Text.EndsWith(BoardLineConfirm.Hint,StringComparison.Ordinal),"write confirm: fee in the text, engine hint",write);
        var delete=BoardLineConfirm.Ask(2,"delete",reading);
        t.Check(delete is {Command:"delete",Topic:77},"delete confirm remembers the article",delete);
        t.Equal(BoardLineConfirm.Question("write",300)+BoardLineConfirm.Hint,write.Text,"same question as the WinForms MessageBox");
        t.Check(BoardLineConfirm.Proceed(write,true,list)&&!BoardLineConfirm.Proceed(write,false,list),"write: sends only while the button rule allows it");
        t.Check(BoardLineConfirm.Proceed(delete,true,reading),"delete: same article → sends");
        t.Check(!BoardLineConfirm.Proceed(delete,true,reading with{Article=article with{Id=78}})&&!BoardLineConfirm.Proceed(delete,true,list),"delete: another article / back on the list → not sent");

        var yes=new YesNoPanel{Source=new Asking(null)};
        var host=new ConfirmHost();var p=new BoardConfirmPromptPanel(id=>id==YesNoPanel.PanelId?yes:null){Host=host};var cmd=new UiNullCommands();
        p.Update(InWorld);t.Check(!p.Visible&&p.Bounds.IsEmpty,"nothing pending → nothing drawn");
        host.PendingConfirm=write;
        p.Update(Outside);t.Check(!p.Visible,"not in world → hidden");
        p.Update(InWorld);t.Check(p.Visible&&p.Current==write&&p.Bounds==YesNoButtons.Line,"pending → drawn on the chat input line");
        t.Check(!p.Key(new(Keys.A,true,false),cmd)&&host.Answers.Count==0,"other keys pass through");
        t.Check(!p.Key(new(Keys.Y|Keys.Control,true,false),cmd)&&host.Answers.Count==0,"modified Y passes through");
        t.Check(p.Key(new(Keys.Y,true,false),cmd)&&host.Answers.SequenceEqual(new[]{(Id:1,Accept:true)}),"Y → accept once");
        host.PendingConfirm=delete;p.Update(InWorld);
        t.Check(p.Key(new(Keys.N,true,false),cmd)&&host.Answers[^1]==(2,false),"N → decline");
        host.PendingConfirm=delete;
        t.Check(p.Key(new(Keys.Escape,true,false),cmd)&&host.Answers[^1]==(2,false),"Esc → decline (nothing sent)");
        host.PendingConfirm=delete;
        t.Check(!p.Key(new(Keys.Y,true,true),cmd)&&host.Answers.Count==3,"key repeat ignored");
        // 伺服器詢問（S219）顯示中：讓位，不畫、不收鍵，確認保留。
        yes.Source=new Asking(new(1,"問題"));yes.Update(InWorld);p.Update(InWorld);
        t.Check(!p.Visible&&!p.Key(new(Keys.Y,true,false),cmd)&&host.PendingConfirm==delete,"server question shown → board prompt yields, kept");
        yes.Source=new Asking(null);yes.Update(InWorld);p.Update(InWorld);
        t.Check(p.Visible&&p.Current==delete,"question gone → board prompt back");
        p.Close();t.Check(host.Answers[^1]==(2,false)&&host.PendingConfirm==null,"stack Close → decline");
        var noHost=new BoardConfirmPromptPanel();noHost.Update(InWorld);t.Check(!noHost.Visible&&!noHost.Key(new(Keys.Y,true,false),cmd),"no host → inert");
    }

    // 3. G14：商店買賣 0／1 由原生 TRADE_WINDOW 畫著時，WinForms NPC 面板整個讓位；空清單與 --classic-ui 照舊。
    static void ShopYield(WorldModuleTestContext t){
        // 判斷表：原本 R4-I 的條件不變。
        t.Check(NpcPanelYield.OwnsAll(true,false,false,false,false,false),"R4-I: nothing open → native owns");
        t.Check(NpcPanelYield.OwnsAll(true,true,false,true,false,false),"R4-I: page only → native owns");
        t.Check(NpcPanelYield.OwnsAll(true,true,true,false,false,false),"R4-I: native warehouse → native owns");
        t.Check(!NpcPanelYield.OwnsAll(true,true,false,false,false,false),"list without native window → WinForms stays");
        t.Check(!NpcPanelYield.OwnsAll(false,true,false,true,false,false),"HyperText unavailable → WinForms stays");
        // G14
        t.Check(NpcPanelYield.OwnsAll(true,true,false,false,true,false),"G14: shop in TRADE_WINDOW → WinForms yields");
        t.Check(NpcPanelYield.OwnsAll(false,true,false,false,true,false),"G14: shop in TRADE_WINDOW, HyperText faulted, no query → yields");
        t.Check(!NpcPanelYield.OwnsAll(false,true,false,false,true,true),"G14: query page needs the WinForms panel (HyperText unavailable) → stays");
        t.Check(NpcPanelYield.OwnsAll(true,true,false,false,true,true),"G14: query in HyperText + shop in TRADE_WINDOW → yields");

        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;
        // --classic-ui：原生窗沒接（ShopNativeWindow=false）→ 舊清單、不讓位。
        t.Check(Talk(c),"talk sent");Feed(c,BuyList((11,3,500,"potion")));
        t.Check(w.ShopTradeWindow()==null&&w.ShopRows.Length==1,"classic: no TRADE_WINDOW, WinForms rows kept");
        Close(c);
        // 原生窗接上：有列 → TRADE_WINDOW 有內容 → 讓位。
        w.ShopNativeWindow=true;
        t.Check(Talk(c),"talk again");Feed(c,BuyList((11,3,500,"potion")));
        bool nativeShop=w.ShopTradeWindow()!=null;
        t.Check(nativeShop&&w.ShopRows.Length==0,"native: rows only in TRADE_WINDOW");
        t.Check(NpcPanelYield.OwnsAll(true,true,false,w.InteractionPage!=null,nativeShop,w.QueryVisible),"native shop: WinForms NPC panel hidden (G14)");
        Close(c);
        // 空清單：TRADE_WINDOW 不開，提示文字照舊只在 NPC 面板。
        t.Check(Talk(c),"talk third");Feed(c,BuyList());
        t.Check(w.InteractionKind=="buy"&&w.ShopTradeWindow()==null,"empty list: no TRADE_WINDOW");
        t.Check(!NpcPanelYield.OwnsAll(true,true,false,false,w.ShopTradeWindow()!=null,false),"empty list: WinForms keeps the notice");
        Close(c);
    }

    // 4. G15：同一個 S39 agsel 只認一次：追蹤器找出 agsel 頁；AuctionInfo 會顯示時 HyperText 不放這頁。
    static void AgselOnce(WorldModuleTestContext t){
        AgitAuctionInfoWindow.Clear();
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;var tracker=new AgselPageTracker();
        t.Check(Talk(c),"talk to the board");Feed(c,Dialog("agboard"));
        int boardPage=w.InteractionRevision;
        tracker.Observe(AgitAuctionInfoWindow.Current,w.InteractionNpc,w.InteractionPage!=null,w.InteractionRevision);
        t.Check(!tracker.IsAgselPage(boardPage),"no agsel yet → ordinary page");
        Feed(c,Agsel(Npc,"458757"));
        var req=AgitAuctionInfoWindow.Current;
        t.Check(req is {BoardId:Npc,HouseNumber:"458757"}&&w.InteractionPage!=null&&w.InteractionRevision!=boardPage,"agsel → info request and NPC page from the same S39",req);
        tracker.Observe(req,w.InteractionNpc,w.InteractionPage!=null,w.InteractionRevision);
        t.Check(tracker.IsAgselPage(w.InteractionRevision)&&!tracker.IsAgselPage(boardPage),"tracker: this page is the agsel page");
        t.Check(AgselPageTracker.HyperTextOmits(true,true),"AuctionInfo shows it → HyperText omits the page");
        t.Check(!AgselPageTracker.HyperTextOmits(true,false),"AuctionInfo unavailable / other board / Agitmap closed → HyperText keeps it (no regression)");
        t.Check(!AgselPageTracker.HyperTextOmits(false,true),"other page of the board → HyperText keeps it");
        tracker.Observe(req,w.InteractionNpc,w.InteractionPage!=null,w.InteractionRevision);
        t.Check(tracker.IsAgselPage(w.InteractionRevision),"same request seen again → unchanged");
        Feed(c,Dialog("agboard2"));
        tracker.Observe(AgitAuctionInfoWindow.Current,w.InteractionNpc,w.InteractionPage!=null,w.InteractionRevision);
        t.Check(!tracker.IsAgselPage(w.InteractionRevision),"next page → not agsel any more");
        // 另一個看板的 agsel：目前的對話不是那個看板 → 不認。
        Feed(c,Agsel(Npc+1,"1"));
        tracker.Observe(AgitAuctionInfoWindow.Current,w.InteractionNpc,w.InteractionPage!=null,w.InteractionRevision);
        t.Check(!tracker.IsAgselPage(w.InteractionRevision),"agsel of another board → current page untouched");
        AgitAuctionInfoWindow.Clear();
        tracker.Observe(null,w.InteractionNpc,w.InteractionPage!=null,w.InteractionRevision);
        t.Check(!tracker.IsAgselPage(w.InteractionRevision),"request cleared (reset) → nothing tracked");
        // 本機關掉 agsel 頁不送封包。
        int before=sent.Count;w.CloseInteraction("x");t.Equal(before,sent.Count,"local close of the omitted page sends nothing");
    }
}
