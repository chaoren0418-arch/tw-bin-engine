using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R2-N（T259／#620）個人商店開店設定窗接上產品的離線測試：合成 XML（照 WIN§1.1 元素名稱自行組出，不是原檔）、合成 surf、
// 記憶體內假傳送端；不開遊戲、網路、視窗或素材。由 --module-tests 自動執行。
static class PersonalShopIntegrationTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static InventoryItem Item(uint id,uint count,ushort icon=7,string name="測試物品")=>new(id,1,0,0,icon,1,count,0,name,"","");
    static ConcurrentDictionary<uint,InventoryItem> Inventory(WorldSession w)=>(ConcurrentDictionary<uint,InventoryItem>)typeof(WorldSession).GetField("inventory",F)!.GetValue(w)!;

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
    static void Click(PersonalShopPanel p,PersonalShopPanel.Part part,int index=0){
        var r=p.PartRect(part,index)!.Value;var at=new Point(r.X+r.Width/2,r.Y+r.Height/2);var c=new UiNullCommands();
        p.Mouse(new(UiMouseKind.Down,at,MouseButtons.Left,1,0,Keys.None),c);p.Mouse(new(UiMouseKind.Up,at,MouseButtons.Left,1,0,Keys.None),c);
    }

    /// <summary>假主機：候選物品走真的 WorldSession.PersonalShopCandidate；送出只記錄。</summary>
    sealed class FakeHost(WorldSession world):IPersonalShopHost {
        public int Generation{get;set;}=1;
        public bool InWorld{get;set;}=true;
        public bool CanSend{get;set;}=true;
        public uint? SelectedItemId{get;set;}
        public bool Accept{get;set;}=true;
        public List<byte[]> Sent{get;}=[];
        public Action? DuringSend{get;set;}
        public PersonalShopEntry? Candidate(uint objectId,uint price,uint count,bool sell)=>world.PersonalShopCandidate(objectId,price,count,sell);
        public Task<bool> Send(byte[] packet){DuringSend?.Invoke();if(Accept)Sent.Add(packet);return Task.FromResult(Accept);}
        public readonly Bitmap IconBitmap=new(4,4);
        public Bitmap? Icon(ushort iconId)=>iconId==7?IconBitmap:null;
    }

    [WorldModuleTest("personal-shop-integration")]
    static void Run(WorldModuleTestContext t){
        // ---- 1. 註冊表：一行、版面 PersonalShopUI、建出來就是 PersonalShopPanel。
        var reg=UiPanelRegistry.Panels.Where(r=>r.Id==PersonalShopPanel.WindowId).ToArray();
        t.Check(reg.Length==1&&reg[0].Layout==PersonalShopPanel.LayoutName,"registry: PersonalShop_Window registered once on PersonalShopUI");

        var world=new WorldSession();var inv=Inventory(world);
        foreach(var it in new[]{Item(500,10),Item(501,1,9,"樣本")})inv[it.ObjectId]=it;
        var host=new FakeHost(world);var ctl=new PersonalShopController(host);

        // ---- 2. 沒有面板時不能開窗。
        t.Check(!ctl.Open()&&ctl.Notice.Contains("原生 UI"),"open without attached panel refused");
        var panel=Loaded();ctl.Attach(panel);
        t.Check(panel.IconSource!=null&&ReferenceEquals(panel.IconSource(7),host.IconBitmap)&&panel.IconSource(8)==null,"icon source = host inventory icons");
        host.InWorld=false;t.Check(!ctl.Open()&&!panel.Visible,"open outside world refused");host.InWorld=true;
        t.Check(ctl.Open()&&panel.Visible&&ReferenceEquals(panel.Setup,ctl.Setup),"open shows panel with controller setup");

        // ---- 3. 從背包放入：先選格、再選背包物品。
        host.SelectedItemId=500;
        t.Check(!ctl.Place(30,5)&&ctl.Notice.Contains("格"),"place without slot selected refused");
        Click(panel,PersonalShopPanel.Part.SellSlot,2);
        t.Equal(((bool,int)?)(true,2),ctl.Setup.Selected,"panel click selects sell slot 2");
        host.SelectedItemId=null;t.Check(!ctl.Place(30,5)&&ctl.Notice.Contains("背包"),"place without inventory selection refused");
        host.SelectedItemId=999;t.Check(!ctl.Place(30,5)&&ctl.Setup.Sell[2]==null,"item not in inventory refused");
        host.SelectedItemId=500;t.Check(!ctl.Place(30,11)&&ctl.Setup.Sell[2]==null,"sell more than held refused");
        t.Check(!ctl.Place(0,1)&&ctl.Setup.Sell[2]==null,"price 0 refused");
        t.Check(ctl.Place(30,5)&&ctl.Setup.Sell[2]==new PersonalShopEntry(500,7,"測試物品",30,5),"sell slot 2 ← inventory 500 ×5 @30");
        Click(panel,PersonalShopPanel.Part.SellSlot,3);t.Check(!ctl.Place(30,1)&&ctl.Setup.Sell[3]==null,"same object twice in sell list refused");
        Click(panel,PersonalShopPanel.Part.BuySlot,0);host.SelectedItemId=501;
        t.Check(ctl.Place(100,20)&&ctl.Setup.Buy[0] is {ObjectId:501,Count:20,IconId:9},"buy slot 0 ← sample 501 ×20 (may exceed held)");
        Click(panel,PersonalShopPanel.Part.BuySlot,1);host.SelectedItemId=501;ctl.Place(100,1);
        t.Check(ctl.Setup.Buy[1]==null,"same sample twice in buy list refused");
        t.Check(ctl.ClearSlot()&&ctl.Setup.Buy[1]==null,"clear selected slot");
        t.Check(ctl.SetMessages("便宜賣","歡迎光臨")&&ctl.Setup.Messages.SequenceEqual(["便宜賣","歡迎光臨"]),"messages applied");
        t.Check(!ctl.SetMessages(new string('甲',21),"")&&ctl.Setup.Messages[0]=="便宜賣","message > 20 refused, old kept");

        // ---- 4. OK → Confirmed → 送 C38 type 0。
        Click(panel,PersonalShopPanel.Part.Ok);
        t.Equal(1,host.Sent.Count,"OK sends one packet");
        var open=host.Sent.Single();
        var expected=PersonalShopPackets.Open([new(500,7,"測試物品",30,5)],[new(501,9,"樣本",100,20)],PersonalShopPackets.Chat("便宜賣","歡迎光臨"));
        t.Check(open.SequenceEqual(expected),"sent bytes = C38 type 0 (1 sell, 1 buy, chat)",Convert.ToHexString(open));
        t.Check(ctl.OpenSent&&!panel.Visible&&!ctl.Sending,"after send: open flagged, setup window closed (推測)");
        t.Check(ctl.Setup.Sell[2]!=null,"setup kept for reopening");

        // ---- 5. 送出前重新核對背包：物品已用掉 → 不送。
        ctl.Open();inv.TryRemove(500,out _);
        Click(panel,PersonalShopPanel.Part.Ok);
        t.Check(host.Sent.Count==1&&ctl.Notice.Contains("測試物品")&&panel.Visible,"stale inventory entry blocks C38");
        inv[500]=Item(500,10);
        host.CanSend=false;Click(panel,PersonalShopPanel.Part.Ok);t.Check(host.Sent.Count==1,"cannot send → nothing sent");host.CanSend=true;
        host.Accept=false;Click(panel,PersonalShopPanel.Part.Ok);t.Check(host.Sent.Count==1&&panel.Visible&&ctl.Notice.Contains("未送出"),"sender refusal keeps window open");host.Accept=true;
        t.Check(!ctl.SendOpen(PersonalShopPackets.Close()).GetAwaiter().GetResult()&&host.Sent.Count==1,"SendOpen refuses a non-open packet");

        // ---- 6. 關店：C38 type 1。
        t.Check(ctl.CloseShop().GetAwaiter().GetResult()&&host.Sent.Count==2&&host.Sent[1].SequenceEqual(PersonalShopPackets.Close())&&!ctl.OpenSent,"close shop sends C38 type 1");
        host.CanSend=false;t.Check(!ctl.CloseShop().GetAwaiter().GetResult()&&host.Sent.Count==2,"close blocked when cannot send");host.CanSend=true;

        // ---- 7. 換世代（重登／換角）：清掉設定、關窗；送出中換世代 → 結果不算。
        ctl.Open();host.Generation=2;ctl.Update();
        t.Check(!panel.Visible&&ctl.Setup.Sell.All(e=>e==null)&&ctl.Setup.Buy.All(e=>e==null)&&ctl.Setup.Messages.All(m=>m=="")&&!ctl.OpenSent,"generation change clears setup and closes window");
        host.DuringSend=()=>host.Generation=3;
        t.Check(!ctl.CloseShop().GetAwaiter().GetResult()&&!ctl.Sending,"send that crosses a generation is not reported as success");
        host.DuringSend=null;

        // ---- 8. 換面板（原生 UI 重建）：舊面板 OK 不再送。
        var old=panel;var fresh=Loaded();ctl.Attach(fresh);ctl.Update();
        ctl.Open();Click(fresh,PersonalShopPanel.Part.SellSlot,0);host.SelectedItemId=500;ctl.Place(10,1);
        old.Open(ctl.Setup);int before=host.Sent.Count;Click(old,PersonalShopPanel.Part.Ok);
        t.Check(host.Sent.Count==before,"detached panel's OK is ignored");
        Click(fresh,PersonalShopPanel.Part.Ok);t.Check(host.Sent.Count==before+1&&host.Sent[^1][0]==PersonalShopPackets.Opcode,"attached panel's OK sends");

        // ---- 9. LoginConnection.SendPersonalShop：只放行 C38、要在世界中。
        var c=new LoginConnection();var wire=new List<byte[]>();
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);wire.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        t.Check(!c.SendPersonalShop(PersonalShopPackets.Close()).GetAwaiter().GetResult()&&wire.Count==0,"not in world → C38 not sent");
        t.Check(!c.PersonalShopCanSend,"not in world → cannot send");
        Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        // S87 自己出現（欄位同 AmountPromptTests.Appear）。
        byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
        c.World.Receive(LoginPackets.Pad([87,..BitConverter.GetBytes((ushort)32600),..BitConverter.GetBytes((ushort)32940),..BitConverter.GetBytes(123u),..BitConverter.GetBytes((ushort)0),0,0,0,0,
            ..BitConverter.GetBytes(0u),..BitConverter.GetBytes((ushort)0),..Str("offline"),..Str(""),0,..BitConverter.GetBytes(0u),..Str(""),..Str(""),0,100,0,0,0,255,255]),"offline");
        t.Check(c.PersonalShopCanSend,"in world (actor = status) → can send",new{c.State,c.World.Ready,actor=c.World.Actor?.ObjectId});
        t.Check(!c.SendPersonalShop([1,0,0,0,0,0,0,0]).GetAwaiter().GetResult()&&wire.Count==0,"non-C38 packet refused");
        t.Check(!c.SendPersonalShop([PersonalShopPackets.Opcode,2,0,0,0,0,0,0]).GetAwaiter().GetResult()&&wire.Count==0,"C38 type 2 refused");
        t.Check(c.SendPersonalShop(PersonalShopPackets.Close()).GetAwaiter().GetResult()&&wire.Count==1&&wire[0].SequenceEqual(PersonalShopPackets.Close()),"in world → C38 type 1 sent");
        t.Check(c.SendPersonalShop(expected).GetAwaiter().GetResult()&&wire.Count==2,"in world → C38 type 0 sent");
        t.Equal((PersonalShopEntry?)null,c.PersonalShopCandidate(999,1,1,true),"connection candidate goes through the world inventory");
    }
}
