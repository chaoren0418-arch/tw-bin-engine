using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R10-M（T397／#912，FU9§9.4、gap-matrix 15.12.6）個人商店收尾的離線測試：
// ① 已開店（自己的動作 70）時 OK 只關窗、Cancel＝關窗＋C38 type 1；沒開店時原流程不變。
// ② `/shop` 擺攤中又在釣魚 → 印 2120、不送；不釣魚 → C38 type 1＋903（既有）。
// 合成 XML（同 personal-shop-flow，只照 WIN§1.1 元素名稱與座標）＋合成 surf、記憶體內假傳送端；不讀客戶端、不連網路、不開視窗。
static class PersonalShopFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    const ushort X=32600,Y=32940;

    // ---- 設定窗（合成） --------------------------------------------------------------------------------------------
    static string Xml(){
        string Row(int r)=>$"<List Name=\"Vertical{r}_List\" Width=\"135\" Height=\"33\" Horizontal=\"1\" Gap=\"1\">"+string.Concat(Enumerable.Range(0,4).Select(i=>$"<ItemIcon Name=\"Item{i}_Icon\" Width=\"33\" Height=\"33\"/>"))+"</List>";
        string List(string name,int y)=>$"<List Name=\"{name}\" X=\"22\" Y=\"{y}\" Width=\"135\" Height=\"67\" Vertical=\"1\" Gap=\"1\">{Row(0)}{Row(1)}</List>";
        string poly=string.Concat(Enumerable.Range(0,3).Select(i=>$"<Image Name=\"Poly{i}_Image\" Surf=\"5752\"><CheckButton Name=\"Select_Button\" PushSurf=\"5736;4\"/></Image>"));
        return "<?xml version=\"1.0\"?><PersonalShopUI><Window Name=\"PersonalShop_Window\" X=\"0\" Y=\"0\" BaseSurf=\"5734;1\" FrameSurf=\"5735;1\">"
            +"<Button Name=\"Close_Button\" X=\"166\" Y=\"13\" BaseSurf=\"5391\"/>"+List("SellingItem_List",42)+List("BuyingItem_List",132)
            +"<Edit Name=\"Message1_Edit\" X=\"18\" Y=\"206\" Width=\"144\" Height=\"16\" LimitText=\"20\"/><Edit Name=\"Message2_Edit\" X=\"18\" Y=\"224\" Width=\"144\" Height=\"16\" LimitText=\"20\"/>"
            +$"<List Name=\"Poly_List\" X=\"17\" Y=\"243\" Width=\"144\" Height=\"62\" Horizontal=\"1\">{poly}</List>"
            +"<Control Name=\"Page_Control\" X=\"0\" Y=\"0\"><Button Name=\"Prev1page_Button\" X=\"19\" Y=\"311\" BaseSurf=\"5737\"/><Button Name=\"Prevpage_Button\" X=\"46\" Y=\"311\" BaseSurf=\"5737\"/>"
            +"<Button Name=\"Nextpage_Button\" X=\"119\" Y=\"311\" BaseSurf=\"5737\"/><Button Name=\"Next1page_Button\" X=\"137\" Y=\"311\" BaseSurf=\"5737\"/>"
            +"<Image Name=\"Page_Image\" X=\"66\" Y=\"308\" Surf=\"5741\"><Label Name=\"Page_Label\" Width=\"46\" Height=\"20\"/></Image></Control>"
            +"<Button Name=\"OK_Button\" X=\"30\" Y=\"334\" BaseSurf=\"5749\"/><Button Name=\"Cancel_Button\" X=\"98\" Y=\"334\" BaseSurf=\"5749\"/>"
            +"</Window></PersonalShopUI>";
    }
    static readonly Dictionary<int,(int W,int H)> Sizes=new(){[5734]=(184,332),[5735]=(184,332),[5391]=(8,8),[5736]=(48,62),[5752]=(48,62),[5741]=(46,19),[5737]=(13,13),[5749]=(53,17)};
    static UiSurface? Synthetic(int id)=>Sizes.TryGetValue(id,out var s)?new UiSurface(id,id+".img","synthetic",new UiImage(s.W,s.H,0,0,new uint[s.W*s.H])):null;
    static PersonalShopPanel Loaded(){var p=new PersonalShopPanel();p.Load(UiLayout.Parse(Xml(),"PersonalShopUI.xml"),Synthetic);return p;}
    static readonly UiNullCommands Cmd=new();
    static Point Center(PersonalShopPanel p,PersonalShopPanel.Part part){var r=p.PartRect(part)!.Value;return new(r.X+r.Width/2,r.Y+r.Height/2);}
    /// <summary>直接點設定窗（不經 UiRoot，繞過接手面板）。</summary>
    static void Click(PersonalShopPanel p,PersonalShopPanel.Part part){
        var at=Center(p,part);
        p.Mouse(new(UiMouseKind.Down,at,MouseButtons.Left,1,0,Keys.None),Cmd);p.Mouse(new(UiMouseKind.Up,at,MouseButtons.Left,1,0,Keys.None),Cmd);
    }
    /// <summary>經 UiRoot／UiInput 點（命中測試從 Float 層往下，跟產品一樣）。</summary>
    static void Click(UiInput input,Point at,Point? release=null){
        input.Mouse(UiMouseKind.Down,at,MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,release??at,MouseButtons.Left,1);
    }
    static InventoryItem Item(uint id,uint count,string name="測試物品")=>new(id,1,0,0,7,1,count,0,name,"","");
    static ConcurrentDictionary<uint,InventoryItem> Inventory(WorldSession w)=>(ConcurrentDictionary<uint,InventoryItem>)typeof(WorldSession).GetField("inventory",F)!.GetValue(w)!;

    sealed class FakeHost(WorldSession world):IPersonalShopHost {
        public int Generation{get;set;}=1;
        public bool InWorld{get;set;}=true;
        public bool CanSend{get;set;}=true;
        public uint? SelectedItemId{get;set;}
        public bool Open{get;set;}
        public List<byte[]> Sent{get;}=[];
        public PersonalShopEntry? Candidate(uint objectId,uint price,uint count,bool sell)=>world.PersonalShopCandidate(objectId,price,count,sell);
        public Task<bool> Send(byte[] packet){Sent.Add(packet);return Task.FromResult(true);}
        public Bitmap? Icon(ushort iconId)=>null;
        public uint? HeldCount(uint objectId)=>world.Inventory.FirstOrDefault(i=>i.ObjectId==objectId)?.Count;
        public int? MapId=>4;
        public bool ShopActive=>Open;
    }
    /// <summary>蓋在設定窗上的另一個視窗（只有範圍）。</summary>
    sealed class Cover(Rectangle bounds):IUiPanel {
        public bool Shown{get;set;}=true;
        public string Id=>"R10M_Cover";
        public UiLayer Layer=>UiLayer.Window;
        public Rectangle Bounds=>Shown?bounds:Rectangle.Empty;
        public bool Visible=>Shown;
        public int Clicks;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands){if(e.Kind==UiMouseKind.Up)Clicks++;return Bounds.Contains(e.Location);}
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }

    [WorldModuleTest("personal-shop-followup")]
    static void Run(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);   // 用內建原文（CI 沒有客戶端）
        using var noDesc=PlayerChatProtocol.UseDesc(null);
        Text(t);OkCancel(t);ShopCommand(t);
    }

    // ---- 1. 2120 字串：沒有表用本機 Text.pak 原文；有表照表並去色碼。
    static void Text(WorldModuleTestContext t){
        t.Equal(2120,PersonalShopText.Fishing,"字串編號 2120");
        t.Equal("釣魚的時候不可以開個人商店",PersonalShopText.Get(PersonalShopText.Fishing),"沒有字串表：本機原文");
        var lines=new string[2121];Array.Fill(lines,"");lines[2120]=@"\f1表內的 2120";
        using(ServerMessageText.UseTable(ServerStringTable.Parse(LoginPackets.TextEncoding.GetBytes("2121\n"+string.Join("\n",lines)))))
            t.Equal("表內的 2120",PersonalShopText.Get(PersonalShopText.Fishing),"有字串表：照表、去色碼");
    }

    // ---- 2. 已開店時的 OK／Cancel。
    static void OkCancel(WorldModuleTestContext t){
        var world=new WorldSession();Inventory(world)[500]=Item(500,10);
        var host=new FakeHost(world);var ctl=new PersonalShopController(host);var panel=Loaded();ctl.Attach(panel);
        var root=new UiRoot();root.Add(panel);
        var okGuard=new PersonalShopOpenGuardPanel(root,PersonalShopPanel.Part.Ok);var cancelGuard=new PersonalShopOpenGuardPanel(root,PersonalShopPanel.Part.Cancel);
        root.Add(okGuard);root.Add(cancelGuard);
        var input=new UiInput(root,Cmd);
        Point ok=Center(panel,PersonalShopPanel.Part.Ok),cancel=Center(panel,PersonalShopPanel.Part.Cancel);

        // 註冊（自動發現）：兩個接手面板各一次，共用 PersonalShopUI 版面；Float 層、可關閉。
        t.Check(UiPanelRegistry.Panels.Count(r=>r.Id==PersonalShopOpenGuardPanel.OkId&&r.Layout==PersonalShopPanel.LayoutName)==1
            &&UiPanelRegistry.Panels.Count(r=>r.Id==PersonalShopOpenGuardPanel.CancelId&&r.Layout==PersonalShopPanel.LayoutName)==1,"OK／Cancel 接手面板各註冊一次（自動發現）");
        t.Check(okGuard is IUiClosablePanel&&okGuard.Layer==UiLayer.Float&&cancelGuard.Id!=okGuard.Id,"接手面板：可關閉、Float 層、id 不同");

        // 沒開店：接手面板看不見；OK 照原流程送 C38 type 0，Cancel 只關窗。
        ctl.Open();ctl.Setup.Set(true,0,new(500,7,"測試物品",30,10));
        t.Check(!okGuard.Visible&&!cancelGuard.Visible&&okGuard.Bounds.IsEmpty,"沒開店：接手面板不可見、不攔");
        t.Check(root.HitTest(ok)==panel,"沒開店：OK 由設定窗接");
        Click(input,ok);
        t.Check(host.Sent.Count==1&&host.Sent[0].Length>1&&host.Sent[0][0]==PersonalShopPackets.Opcode&&host.Sent[0][1]==0&&!panel.Visible,"沒開店：OK → C38 type 0、關窗",host.Sent.Select(Convert.ToHexString));
        ctl.Open();Click(input,cancel);
        t.Check(host.Sent.Count==1&&!panel.Visible,"沒開店：Cancel 只關窗、不送");

        // 已開店：OK 只關窗、不送；設定保留。
        host.Open=true;ctl.Open();
        t.Check(okGuard.Visible&&okGuard.Bounds==panel.PartRect(PersonalShopPanel.Part.Ok)&&cancelGuard.Bounds==panel.PartRect(PersonalShopPanel.Part.Cancel),"已開店：接手面板蓋在 OK／Cancel 上");
        t.Check(root.HitTest(ok)==okGuard&&root.HitTest(cancel)==cancelGuard,"已開店：命中測試先到接手面板（Float 在 Window 之上）");
        Click(input,ok);
        t.Check(host.Sent.Count==1&&!panel.Visible&&ctl.Setup.Sell[0]?.ObjectId==500,"已開店：OK 只關窗、不送 C38、設定保留",ctl.Notice);
        t.Check(!okGuard.Visible,"窗關了：接手面板跟著不可見");

        // 已開店：Cancel → 關窗＋C38 type 1。
        ctl.Open();Click(input,cancel);
        t.Check(host.Sent.Count==2&&host.Sent[1].SequenceEqual(PersonalShopPackets.Close())&&!panel.Visible,"已開店：Cancel → C38 type 1、關窗",host.Sent.Select(Convert.ToHexString));
        t.Check(ctl.Notice.Contains("關店"),"提示已送出關店",ctl.Notice);

        // 按下後移出鈕再放開：不算點擊。
        ctl.Open();Click(input,cancel,new Point(cancel.X,cancel.Y-40));
        t.Check(host.Sent.Count==2&&panel.Visible,"Cancel 按下後移出再放開：不關店、不關窗");
        Click(input,ok,new Point(ok.X+200,ok.Y));
        t.Check(host.Sent.Count==2&&panel.Visible,"OK 按下後移出再放開：不動作");
        // 右鍵不算。
        input.Mouse(UiMouseKind.Down,cancel,MouseButtons.Right,1);input.Mouse(UiMouseKind.Up,cancel,MouseButtons.Right,1);
        t.Check(host.Sent.Count==2&&panel.Visible,"右鍵不關店");

        // 關閉鈕與 Esc：已開店也只關窗（原版 ClosePersonalShop）。
        Click(input,Center(panel,PersonalShopPanel.Part.Close));
        t.Check(host.Sent.Count==2&&!panel.Visible,"已開店：關閉鈕只關窗");
        ctl.Open();panel.Key(new(Keys.Escape,true,false),Cmd);
        t.Check(host.Sent.Count==2&&!panel.Visible,"已開店：Esc 只關窗");

        // 保險：繞過接手面板直接按設定窗的 OK（Confirmed）→ 仍只關窗、不送。
        ctl.Open();Click(panel,PersonalShopPanel.Part.Ok);
        t.Check(host.Sent.Count==2&&!panel.Visible,"已開店：Confirmed 走到 SendOpen 也只關窗");
        // 設定是空的（換角後）也一樣：OK 只關窗。
        ctl.Setup.Clear();ctl.Open();Click(input,ok);
        t.Check(host.Sent.Count==2&&!panel.Visible,"已開店、清單空：OK 仍只關窗");

        // 其他視窗蓋在設定窗上而且碰到 Cancel：接手面板讓開，點擊交給上面的視窗。
        ctl.Open();var cover=new Cover(new Rectangle(cancel.X-5,cancel.Y-5,20,20));root.Add(cover);root.BringToFront(cover);
        t.Check(cancelGuard.Bounds.IsEmpty&&!okGuard.Bounds.IsEmpty,"被蓋住的 Cancel 不接手；OK 沒被蓋照接手");
        Click(input,cancel);
        t.Check(cover.Clicks==1&&host.Sent.Count==2&&panel.Visible,"點擊給上面的視窗，不關店");
        cover.Shown=false;root.SyncWindows();
        t.Check(!cancelGuard.Bounds.IsEmpty,"蓋住的視窗關了：Cancel 恢復接手");

        // 不能送時（Sending／CanSend=false）：Cancel 只關窗，提示不能送。
        host.CanSend=false;Click(input,cancel);
        t.Check(host.Sent.Count==2&&!panel.Visible&&ctl.Notice.Contains("不能送出關店"),"不能送：關窗、不送、提示",ctl.Notice);
        host.CanSend=true;

        // 擺攤結束（動作不是 70）：回到原流程。
        host.Open=false;ctl.Open();
        t.Check(!okGuard.Visible&&!cancelGuard.Visible&&!ctl.OkWhileOpen()&&!ctl.CancelWhileOpen().GetAwaiter().GetResult()&&panel.Visible,"沒開店：OkWhileOpen／CancelWhileOpen 不動作");

        // 預設介面值：舊的 host 實作沒有 ShopActive → false。
        IPersonalShopHost plain=new PlainHost();
        t.Check(!plain.ShopActive,"IPersonalShopHost.ShopActive 預設 false");
    }
    sealed class PlainHost:IPersonalShopHost {
        public int Generation=>1;public bool InWorld=>true;public bool CanSend=>true;public uint? SelectedItemId=>null;
        public PersonalShopEntry? Candidate(uint objectId,uint price,uint count,bool sell)=>null;
        public Task<bool> Send(byte[] packet)=>Task.FromResult(false);
        public Bitmap? Icon(ushort iconId)=>null;
    }

    // ---- 3. `/shop`：擺攤中又在釣魚 → 2120、不送。
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Appear(uint id,ushort x,ushort y,string name)=>
        P([87],Le16(x),Le16(y),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,X,Y,"offline"),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Say(LoginConnection c,string text){
        Set(c,"playerChatNextAt",0L);var s=c.PlayerChatSnapshot();
        return c.SendPlayerChat(text,s.Generation,s.WorldEpoch,s.Actor).GetAwaiter().GetResult();
    }
    static string Notice(LoginConnection c)=>c.PlayerChatSnapshot().Notice;
    static byte[] Shop()=>P([119],Le32(Me),[70]);
    static byte[] Fish()=>P([158],Le32(Me),[71],Le16(X),Le16(Y));
    static byte[] FishEnd()=>P([71],Le16(1163),[0]);

    static void ShopCommand(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            c.World.Receive(Shop(),"offline");c.World.Receive(Fish(),"offline");
            t.Check(c.World.Actor?.Weapon==70&&c.ReadFishing().Active,"前置：擺攤中（動作 70）而且釣魚中（S158 動作 71）",new{c.World.Actor?.Weapon,c.ReadFishing().Active});
            t.Check(c.PersonalShopActive,"LoginConnection.PersonalShopActive＝動作 70");
            t.Check(!Say(c,"/shop")&&sent.Count==0&&Notice(c)=="釣魚的時候不可以開個人商店","擺攤＋釣魚：/shop 印 2120、不送",Notice(c));
            t.Check(!Say(c,"/商店")&&sent.Count==0,"中文別名同樣擋下");
            // 收竿已送、等伺服器確認：仍算釣魚中（推測）。
            c.World.FishingCancelSent();
            t.Check(c.ReadFishing() is {Active:true,CancelPending:true}&&!Say(c,"/shop")&&sent.Count==0,"收竿等待確認中：仍印 2120");
            // 釣魚結束（S71 1163）→ /shop 照舊關店。
            c.World.Receive(FishEnd(),"offline");
            t.Check(!c.ReadFishing().Active&&c.World.Actor?.Weapon==70,"S71 1163：釣魚結束、仍擺攤");
            t.Check(Say(c,"/shop")&&sent.Count==1&&sent[0].SequenceEqual(PersonalShopPackets.Close())&&Notice(c)=="結束個人商店模式。","不釣魚：C38 type 1＋903",Notice(c));
        }
        sent.Clear();
        using(var c=Seed(sent)){
            // 沒擺攤但在釣魚：原版只在擺攤分支看釣魚 → 照舊切換設定窗（離線沒有視窗：不送、提示）。
            c.World.Receive(Fish(),"offline");
            t.Check(c.ReadFishing().Active&&!c.PersonalShopActive,"前置：釣魚中、沒擺攤");
            t.Check(!Say(c,"/shop")&&sent.Count==0&&Notice(c).Contains("個人商店設定窗"),"沒擺攤：不印 2120，照舊開窗",Notice(c));
        }
    }
}
