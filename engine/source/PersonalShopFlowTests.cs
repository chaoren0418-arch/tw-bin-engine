using System.Collections.Concurrent;
using System.Reflection;
using P06.NativeUi;
namespace P06;

// R9-J（T370／#854，FU9§9.4）個人商店流程的離線測試：背包選取＋點清單 → 898 單價 → 933 數量、雙擊移除、OK 檢查、
// 兩行訊息 0xFF 分隔／0x00 結尾、外型不送。合成 XML（只照 WIN§1.1 的元素名稱與座標）＋合成 surf；不讀客戶端、不連網路。
static class PersonalShopFlowTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static InventoryItem Item(uint id,uint count,ushort icon=7,string name="測試物品")=>new(id,1,0,0,icon,1,count,0,name,"","");
    static ConcurrentDictionary<uint,InventoryItem> Inventory(WorldSession w)=>(ConcurrentDictionary<uint,InventoryItem>)typeof(WorldSession).GetField("inventory",F)!.GetValue(w)!;
    static ConcurrentDictionary<uint,bool> Equipped(WorldSession w)=>(ConcurrentDictionary<uint,bool>)typeof(WorldSession).GetField("equipmentStates",F)!.GetValue(w)!;

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
    static Point Center(PersonalShopPanel p,PersonalShopPanel.Part part,int index){var r=p.PartRect(part,index)!.Value;return new(r.X+r.Width/2,r.Y+r.Height/2);}
    static void Click(PersonalShopPanel p,PersonalShopPanel.Part part,int index=0,int clicks=1){
        var at=Center(p,part,index);
        p.Mouse(new(UiMouseKind.Down,at,MouseButtons.Left,clicks,0,Keys.None),Cmd);p.Mouse(new(UiMouseKind.Up,at,MouseButtons.Left,clicks,0,Keys.None),Cmd);
    }
    static void DoubleClick(PersonalShopPanel p,PersonalShopPanel.Part part,int index){Click(p,part,index,1);Click(p,part,index,2);}
    static void Type(PersonalShopPanel p,string s){foreach(char c in s)p.Char(c);}
    static bool Key(PersonalShopPanel p,Keys k)=>p.Key(new(k,true,false),Cmd);

    sealed class FakeHost(WorldSession world):IPersonalShopHost {
        public int Generation{get;set;}=1;
        public bool InWorld{get;set;}=true;
        public bool CanSend{get;set;}=true;
        public uint? SelectedItemId{get;set;}
        public long? AdenaValue{get;set;}
        public int? Map{get;set;}=4;
        public int Begins,Ends;
        public List<byte[]> Sent{get;}=[];
        public PersonalShopEntry? Candidate(uint objectId,uint price,uint count,bool sell)=>world.PersonalShopCandidate(objectId,price,count,sell);
        public Task<bool> Send(byte[] packet){Sent.Add(packet);return Task.FromResult(true);}
        public Bitmap? Icon(ushort iconId)=>null;
        public uint? HeldCount(uint objectId)=>world.Inventory.FirstOrDefault(i=>i.ObjectId==objectId)?.Count;
        public long? Adena=>AdenaValue;
        public int? MapId=>Map;
        public void BeginTextInput()=>Begins++;
        public void EndTextInput()=>Ends++;
    }

    [WorldModuleTest("personal-shop-flow")]
    static void Run(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);   // 用內建原文（CI 沒有客戶端）

        // ---- 1. 訊息組裝：訊息1＋FF＋訊息2＋00；每行 ≤ 40 byte；外型不送。
        t.Equal("4142"+"FF"+"00",Convert.ToHexString(PersonalShopPackets.Chat("AB","")),"line 2 empty → separator still written");
        t.Equal("FF00",Convert.ToHexString(PersonalShopPackets.Chat("","")),"both empty → FF 00");
        t.Equal(40+1+0+1,PersonalShopPackets.Chat(new string('中',25),"").Length,"50-byte line cut to 40 bytes");
        var mixed=PersonalShopPackets.Chat("a"+new string('中',20),"");
        t.Check(mixed.Length==1+38+1+1&&mixed[^2]==PersonalShopPackets.MessageSeparator,"cut never splits a double-byte char (a + 19 中 = 39 bytes)",Convert.ToHexString(mixed));
        var s0=new PersonalShopSetup();s0.Set(true,0,new(500,7,"x",10,1));s0.SetMessage(0,"便宜");s0.SetMessage(1,"歡迎");
        var b0=s0.BuildOpen()!;s0.SelectPoly(2);var b2=s0.BuildOpen()!;
        t.Check(b0.SequenceEqual(b2),"poly selection does not change C38 (C_Shop does not read it)");
        int tail=2+2+12+2;var chat=PersonalShopPackets.Chat("便宜","歡迎");
        t.Check(b0.Skip(tail).Take(chat.Length).SequenceEqual(chat)&&chat[4]==0xFF&&chat[^1]==0,"C38 chat = 便宜 FF 歡迎 00",Convert.ToHexString(b0));

        // ---- 2. 字串（內建原文；有字串表時照表）。
        t.Equal("請設定1件物品的價格。",PersonalShopText.Get(PersonalShopText.Price),"898");
        t.Equal("想要販售幾個呢？(1-10，Enter=全部，Esc=取消)",PersonalShopText.Get(PersonalShopText.Count,PersonalShopText.Get(PersonalShopText.SellWord),10u),"933 with 934 and max, colour codes removed");
        t.Equal("1件物品的價格無法超過 10000000金幣。",PersonalShopText.Get(PersonalShopText.PriceTooHigh,PersonalShopText.PriceLimit),"900");

        // ---- 3. 背包選取＋點清單 → 898 → 933。
        var world=new WorldSession();var inv=Inventory(world);
        foreach(var it in new[]{Item(500,10),Item(501,1,9,"樣本"),Item(502,5,11,"箭"),Item(503,1,12,"劍"),Item(504,300,13,"寶石")})inv[it.ObjectId]=it;
        Equipped(world)[503]=true;
        var host=new FakeHost(world);var ctl=new PersonalShopController(host);var panel=Loaded();ctl.Attach(panel);
        t.Check(ReferenceEquals(panel.Prompt,ctl)&&panel is IUiTextInput,"controller is the panel's prompt input; panel takes typed characters");
        t.Check(ctl.Open(),"open");
        Click(panel,PersonalShopPanel.Part.SellSlot,0);
        t.Check(ctl.Prompt==null&&ctl.Notice.Contains("背包選取")&&host.Begins==0,"no inventory selection → no prompt, notice explains the substitute flow",ctl.Notice);
        host.SelectedItemId=500;Click(panel,PersonalShopPanel.Part.SellSlot,0);
        t.Check(ctl.Prompt is {Sell:true,Slot:0,ObjectId:500,CountStep:false,AskCount:true}&&host.Begins==1,"click empty sell slot → 898 price prompt, keyboard leaves the game");
        t.Equal("請設定1件物品的價格。 ",ctl.PromptLine,"prompt line = 898 + typed");
        Type(panel,"1x");t.Equal("1x",ctl.Prompt!.Typed,"characters go to the prompt");
        t.Check(Key(panel,Keys.Back)&&ctl.Prompt!.Typed=="1","Backspace in the prompt");
        Type(panel,"x");Key(panel,Keys.Enter);
        t.Check(ctl.Prompt is {CountStep:false,Typed:""}&&ctl.Notice=="只能輸入數字。","not a number → 938, prompt stays");
        Type(panel,"0");Key(panel,Keys.Enter);t.Equal("只能輸入數字。",ctl.Notice,"price 0 → 938");
        Type(panel,"10000000");Key(panel,Keys.Enter);
        t.Check(ctl.Prompt is {CountStep:false}&&ctl.Notice.StartsWith("1件物品的價格無法超過",StringComparison.Ordinal),"price ≥ 10,000,000 → 900");
        t.Equal("",ctl.Prompt!.Typed,"rejected input is cleared");
        Type(panel,"30");Key(panel,Keys.Enter);
        t.Check(ctl.Prompt is {CountStep:true,Price:30,MaxCount:10}&&ctl.Notice=="想要販售幾個呢？(1-10，Enter=全部，Esc=取消)","stack → 933 count prompt (販售, 1-held)",ctl.Notice);
        Type(panel,"11");Key(panel,Keys.Enter);t.Check(ctl.Prompt is {CountStep:true,Typed:""},"count > held → prompt again");
        Key(panel,Keys.Enter);
        t.Check(ctl.Prompt==null&&ctl.Setup.Sell[0]==new PersonalShopEntry(500,7,"測試物品",30,10)&&host.Ends==1,"Enter with nothing typed = all → sell slot 0 = 500 ×10 @30, keyboard back to the game");

        // 不是堆疊（數量 1）：單價後直接放入；收購單價超過持有金幣 → 936。
        host.AdenaValue=50;host.SelectedItemId=501;Click(panel,PersonalShopPanel.Part.BuySlot,0);
        Type(panel,"100");Key(panel,Keys.Enter);t.Check(ctl.Prompt is {CountStep:false}&&ctl.Notice=="金幣不足，無法購買。","buy price > adena → 936");
        Type(panel,"40");Key(panel,Keys.Enter);
        t.Check(ctl.Prompt==null&&ctl.Setup.Buy[0] is {ObjectId:501,Price:40,Count:1},"single item → no 933, count 1");
        // 堆疊收購：上限＝金幣 ÷ 單價（推測）。
        host.SelectedItemId=502;Click(panel,PersonalShopPanel.Part.BuySlot,1);Type(panel,"10");Key(panel,Keys.Enter);
        t.Check(ctl.Prompt is {CountStep:true,MaxCount:5}&&ctl.Notice.StartsWith("想要購買幾個呢",StringComparison.Ordinal),"stack buy → 933 (購買), max = 50/10",ctl.Notice);
        Type(panel,"3");Key(panel,Keys.Enter);t.Check(ctl.Setup.Buy[1] is {ObjectId:502,Count:3,Price:10},"buy slot 1 = 502 ×3 @10");

        // 放入檢查：940 已在清單、932 裝備中。
        host.SelectedItemId=500;Click(panel,PersonalShopPanel.Part.SellSlot,1);
        t.Check(ctl.Prompt==null&&ctl.Notice=="已經陳列到視窗了。","same item again → 940, no prompt");
        host.SelectedItemId=503;Click(panel,PersonalShopPanel.Part.SellSlot,1);
        t.Check(ctl.Prompt==null&&ctl.Notice=="裝備中的道具無法交易。","equipped → 932, no prompt");
        // 點有物品的格子：只選取、不開提示。
        host.SelectedItemId=504;Click(panel,PersonalShopPanel.Part.SellSlot,0);
        t.Check(ctl.Prompt==null&&ctl.Setup.Selected==(true,0)&&ctl.Setup.Sell[0]?.ObjectId==500,"click a filled slot → select only");

        // Esc：提示中只取消提示，不關窗；再按才關窗。
        Click(panel,PersonalShopPanel.Part.SellSlot,1);t.Check(ctl.Prompt!=null,"prompt for 504");
        int ends=host.Ends;
        t.Check(Key(panel,Keys.Escape)&&ctl.Prompt==null&&panel.Visible&&host.Ends==ends+1,"Esc cancels the prompt only");
        // 提示中關窗（Cancel 鈕）→ 提示一起取消。
        Click(panel,PersonalShopPanel.Part.SellSlot,1);Click(panel,PersonalShopPanel.Part.Cancel);
        t.Check(!panel.Visible&&ctl.Prompt==null,"closing the window cancels the prompt");
        ctl.Open();

        // ---- 4. 雙擊移除（Pick 515）。
        DoubleClick(panel,PersonalShopPanel.Part.BuySlot,1);
        t.Check(ctl.Setup.Buy[1]==null&&ctl.Prompt==null&&ctl.Notice.StartsWith("已移除收購第 2 格",StringComparison.Ordinal),"double-click removes buy slot 1 and does not open a prompt on the emptied slot",ctl.Notice);
        t.Check(ctl.Setup.Buy[0]!=null&&ctl.Setup.Sell[0]!=null,"other slots kept");

        // ---- 5. 訊息欄：字元輸入（IUiTextInput）。
        Click(panel,PersonalShopPanel.Part.Message,0);Type(panel,"便宜");
        Click(panel,PersonalShopPanel.Part.Message,1);Type(panel,"歡迎");
        t.Check(ctl.Setup.Messages.SequenceEqual(["便宜","歡迎"]),"typed into the focused message edits");
        Type(panel,new string('多',30));t.Equal(20,ctl.Setup.Messages[1].Length,"LimitText 20 enforced while typing");
        ctl.Setup.SetMessage(1,"歡迎");
        Click(panel,PersonalShopPanel.Part.Poly,0);   // 失去訊息欄焦點
        t.Check(!panel.Char('z')&&ctl.Setup.Messages[1]=="歡迎","no prompt and no focused edit → character not taken (chat line gets it)");

        // ---- 6. OK 檢查（sub_DE1AD0）。
        host.Map=303;Click(panel,PersonalShopPanel.Part.Ok);
        t.Check(host.Sent.Count==0&&ctl.Notice=="無法在此開設個人商店。","map 303 → 876, nothing sent");
        host.Map=70;Click(panel,PersonalShopPanel.Part.Ok);t.Check(host.Sent.Count==0,"map 70 → 876");
        host.Map=4;
        ctl.Setup.Set(true,1,new(504,13,"寶石",9_000_000,300));
        Click(panel,PersonalShopPanel.Part.Ok);
        t.Check(host.Sent.Count==0&&ctl.Notice.StartsWith("總共販賣價格無法超過",StringComparison.Ordinal),"sell total > limit → 904, nothing sent",ctl.Notice);
        ctl.Setup.Set(true,1,null);
        Click(panel,PersonalShopPanel.Part.Ok);
        t.Equal(1,host.Sent.Count,"checks pass → one C38 sent");
        var expected=PersonalShopPackets.Open([new(500,7,"測試物品",30,10)],[new(501,9,"樣本",40,1)],PersonalShopPackets.Chat("便宜","歡迎"));
        t.Check(host.Sent[0].SequenceEqual(expected),"sent = C38 type 0 with FF-separated messages",Convert.ToHexString(host.Sent[0]));

        // ---- 7. 提示畫面（聊天輸入列）：設定窗開著且有提示才有內容；沒有 ChatPanel 時不畫。
        t.Check(UiPanelRegistry.Panels.Count(r=>r.Id==PersonalShopPromptPanel.PanelId&&r.Layout==PersonalShopPanel.LayoutName)==1,"prompt panel registered once (auto-discovery)");
        var root=new UiRoot();root.Add(panel);var promptPanel=new PersonalShopPromptPanel(root);
        t.Check(promptPanel is IUiClosablePanel&&promptPanel.Layer==UiLayer.Float,"prompt panel: closable, float layer");
        t.Check(promptPanel.Line==null&&!promptPanel.Visible,"no prompt → nothing");
        ctl.Open();host.SelectedItemId=504;Click(panel,PersonalShopPanel.Part.SellSlot,1);
        t.Equal(ctl.PromptLine,promptPanel.Line,"prompt line follows the controller");
        t.Check(!promptPanel.Visible,"no ChatPanel (no chat bar position) → not shown, no invented position");
        promptPanel.Close();t.Check(ctl.Prompt==null&&panel.Visible,"prompt panel Close (stack / global ESC) cancels the prompt only");

        // ---- 8. 換世代：提示取消、鍵盤還給遊戲。
        Click(panel,PersonalShopPanel.Part.SellSlot,1);ends=host.Ends;
        host.Generation=2;ctl.Update();
        t.Check(ctl.Prompt==null&&host.Ends==ends+1&&!panel.Visible,"generation change cancels the prompt");
    }
}
