using P06.NativeUi;
namespace P06;

// M7-T（T220／#527）個人商店開店設定窗的離線測試：合成 XML（照 WIN§1.1 的元素名稱與座標自行組出，不是原檔）、合成 surf、
// 合成封包；不開遊戲、網路，也不讀原客戶端。由 --module-tests 自動執行。
static class PersonalShopTests {
    // 只含面板會讀的元素；座標照 WIN§1.1。
    static string Xml(int limit=20,int polys=3,bool dropOk=false){
        string Row(int r)=>$"<List Name=\"Vertical{r}_List\" Width=\"135\" Height=\"33\" Horizontal=\"1\" Gap=\"1\">"+string.Concat(Enumerable.Range(0,4).Select(i=>$"<ItemIcon Name=\"Item{i}_Icon\" Width=\"33\" Height=\"33\" Icon=\"0\" MouseEvent=\"Pick\"/>"))+"</List>";
        string List(string name,int y)=>$"<List Name=\"{name}\" X=\"22\" Y=\"{y}\" Width=\"135\" Height=\"67\" Vertical=\"1\" Gap=\"1\">{Row(0)}{Row(1)}</List>";
        string poly=string.Concat(Enumerable.Range(0,polys).Select(i=>$"<Image Name=\"Poly{i}_Image\" Surf=\"5752\"><CheckButton Name=\"Select_Button\" PushSurf=\"5736;4\" MouseEvent=\"SelectPoly\"/></Image>"));
        return "<?xml version=\"1.0\"?><PersonalShopUI>"
            +"<Window Name = \"PersonalShop_Window\" X = \"0\" Y = \"0\" BaseSurf = \"5734;1\" FrameSurf = \"5735;1\" Moveable = \"18\" KeyboardEvent = \"ClosePersonalShop\">"
            +"<Button Name=\"Close_Button\" X=\"166\" Y=\"13\" BaseSurf=\"5391\" PushSurf=\"5392\"/>"
            +List("SellingItem_List",42)+List("BuyingItem_List",132)
            +$"<Edit Name=\"Message1_Edit\" X=\"18\" Y=\"206\" Width=\"144\" Height=\"16\" LimitText=\"{limit}\"/>"
            +"<Edit Name=\"Message2_Edit\" X=\"18\" Y=\"224\" Width=\"144\" Height=\"16\" LimitText=\"20\"/>"
            +$"<List Name=\"Poly_List\" X=\"17\" Y=\"243\" Width=\"144\" Height=\"62\" Horizontal=\"1\">{poly}</List>"
            +"<Control Name=\"Page_Control\" X=\"0\" Y=\"0\">"
            +"<Button Name=\"Prev1page_Button\" X=\"19\" Y=\"311\" BaseSurf=\"5737;1\" HighlightSurf=\"5738;1\"/>"
            +"<Button Name=\"Prevpage_Button\" X=\"46\" Y=\"311\" BaseSurf=\"5739;1\" HighlightSurf=\"5740;1\"/>"
            +"<Button Name=\"Nextpage_Button\" X=\"119\" Y=\"311\" BaseSurf=\"5742;1\" HighlightSurf=\"5743;1\"/>"
            +"<Button Name=\"Next1page_Button\" X=\"137\" Y=\"311\" BaseSurf=\"5744;1\" HighlightSurf=\"5745;1\"/>"
            +"<Image Name=\"Page_Image\" X=\"66\" Y=\"308\" Surf=\"5741\"><Label Name=\"Page_Label\" Width=\"46\" Height=\"20\" Align=\"center\" Caption=\"0/0\"/></Image>"
            +"</Control>"
            +(dropOk?"":"<Button Name=\"OK_Button\" X=\"30\" Y=\"334\" BaseSurf=\"5749\" HighlightSurf=\"5750\" PushSurf=\"5751;1\"/>")
            +"<Button Name=\"Cancel_Button\" X=\"98\" Y=\"334\" BaseSurf=\"5746\" HighlightSurf=\"5747\" PushSurf=\"5748;1\"/>"
            +"</Window></PersonalShopUI>";
    }
    // 合成 surf：尺寸照 ui_img_dims 量到的原圖尺寸，像素是單色（每個 id 一個顏色），不是原圖。
    static readonly Dictionary<int,(int W,int H)> Sizes=new(){
        [5734]=(184,332),[5735]=(184,332),[5391]=(8,8),[5392]=(8,8),[5736]=(48,62),[5752]=(48,62),[5741]=(46,19),
        [5737]=(22,13),[5738]=(22,13),[5739]=(13,13),[5740]=(13,13),[5742]=(13,13),[5743]=(13,13),[5744]=(22,13),[5745]=(22,13),
        [5746]=(53,17),[5747]=(53,17),[5748]=(54,18),[5749]=(53,17),[5750]=(53,17),[5751]=(54,18)};
    static uint ColorOf(int id)=>0xFF000000u|(uint)((id*2654435761u)&0xFFFFFF);
    static UiSurface? Synthetic(int id){
        if(!Sizes.TryGetValue(id,out var s))return null;
        // 5735（框）整張透明，讓底圖顏色可以被檢查；5736（勾選）只有左上 4×4 不透明。
        var px=Enumerable.Repeat(id==5735?0u:ColorOf(id),s.W*s.H).ToArray();
        if(id==5736){Array.Clear(px);for(int y=0;y<4;y++)for(int x=0;x<4;x++)px[y*s.W+x]=ColorOf(id);}
        return new UiSurface(id,id+".img","synthetic",new UiImage(s.W,s.H,0,0,px));
    }
    static PersonalShopPanel Loaded(string? xml=null,Func<int,UiSurface?>? surf=null){var p=new PersonalShopPanel();p.Load(UiLayout.Parse(xml??Xml(),"PersonalShopUI.xml"),surf??Synthetic);return p;}
    static UiMouseEvent M(UiMouseKind k,Rectangle r,MouseButtons b=MouseButtons.Left)=>new(k,new(r.X+r.Width/2,r.Y+r.Height/2),b,1,0,Keys.None);
    static void Click(PersonalShopPanel p,Rectangle r,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,r),c);p.Mouse(M(UiMouseKind.Up,r),c);}
    static int Argb(UiCanvas c,int x,int y)=>c.Bitmap.GetPixel(x,y).ToArgb();
    static InventoryItem Item(uint id,uint count,ushort icon=7,string name="測試物品")=>new(id,1,0,0,icon,1,count,0,name,"","");

    [WorldModuleTest("personal-shop")]
    static void Run(WorldModuleTestContext t){
        // ---- 1. C38 封包（C_Shop.java 欄位順序）。
        t.Equal("2601000000000000",Convert.ToHexString(PersonalShopPackets.Close()),"close = C38 type 1, padded");
        var sellE=new PersonalShopEntry(0x11223344,7,"a",100,3);var buyE=new PersonalShopEntry(0x55667788,8,"b",2000000000,1);
        var open=PersonalShopPackets.Open([sellE],[buyE],PersonalShopPackets.Chat("AB","C"));
        t.Equal("2600"+"0100"+"44332211"+"64000000"+"03000000"+"0100"+"88776655"+"00943577"+"01000000"+"41424300",
            Convert.ToHexString(open)[..((2+2+12+2+12+4)*2)],"open = C38 0 H(1){D id,D price,D count} H(1){…} chat");
        t.Equal(0,open.Length%8,"open padded to 8");
        t.Equal(40,open.Length,"open length 2+14+14+4 → 34 → pad 40");
        t.Equal("A4A4A4E5"+"00",Convert.ToHexString(PersonalShopPackets.Chat("中","文")),"chat = Big5 line1 + line2 + 00 (join rule 推測)");
        t.Throws<ArgumentException>(()=>PersonalShopPackets.Open([],[],[0]),"empty shop refused");
        t.Throws<ArgumentException>(()=>PersonalShopPackets.Open([sellE with{Price=0}],[],[0]),"price 0 refused (server kicks)");
        t.Throws<ArgumentException>(()=>PersonalShopPackets.Open([sellE with{Count=0}],[],[0]),"count 0 refused (server kicks)");
        t.Throws<ArgumentException>(()=>PersonalShopPackets.Open([],[buyE with{Price=2000000001}],[0]),"buy price > 2e9 refused");
        t.Throws<ArgumentException>(()=>PersonalShopPackets.Open([sellE with{Price=(uint)int.MaxValue+1}],[],[0]),"price > int.MaxValue refused (server reads signed D)");
        t.Throws<ArgumentException>(()=>PersonalShopPackets.Open(Enumerable.Range(1,9).Select(i=>sellE with{ObjectId=(uint)i}).ToArray(),[],[0]),"> 8 sell rows refused");

        // ---- 2. 設定模型。
        var s=new PersonalShopSetup();
        t.Equal("0/0",s.PageLabel,"empty page label 0/0");
        t.Check(s.BuildOpen()==null&&s.Notice.Length>0,"OK with nothing listed is refused locally");
        t.Check(s.Set(true,0,sellE),"put sell slot 0");
        t.Check(!s.Set(true,1,sellE),"same object twice in one list refused");
        t.Check(!s.Set(true,8,sellE with{ObjectId=9}),"slot 8 out of range");
        t.Check(!s.Set(false,0,buyE with{Price=0}),"buy price 0 refused");
        t.Equal("1/1",s.PageLabel,"one page");
        t.Check(!s.GoPage(1)&&s.Page==0,"cannot page past the only page (server cap 8 = one page)");
        t.Check(s.SetMessage(0,new string('甲',20)),"20 chars accepted");
        t.Check(!s.SetMessage(1,new string('乙',21))&&s.Messages[1]=="","21 chars refused");
        t.Check(!s.SetMessage(1,"a\nb"),"control chars refused");
        t.Check(!s.SetMessage(1,"😀"),"non-Big5 refused");
        t.Check(s.SetMessage(1,"歡迎"),"line 2 accepted");
        t.Check(s.SelectPoly(2)&&s.Poly==2&&!s.SelectPoly(3),"poly 0..2 only");
        var built=s.BuildOpen();
        t.Check(built!=null&&built[0]==38&&built[1]==0&&built[2]==1&&built[3]==0,"OK builds C38 with one sell row");
        int tail=2+2+12+2;
        t.Check(built!=null&&built[tail-2]==0&&built[tail-1]==0,"no buy rows → H(0)");
        t.Check(built!=null&&built.Skip(tail).Take(45).SequenceEqual(PersonalShopPackets.Chat(new string('甲',20),"歡迎")),"chat bytes follow the lists; poly is not sent");
        s.Clear();t.Check(s.Sell.All(e=>e==null)&&s.Messages.All(m=>m=="")&&s.Poly==0,"clear");

        // ---- 3. 從背包取候選。
        var w=new WorldSession();
        var inv=(System.Collections.Concurrent.ConcurrentDictionary<uint,InventoryItem>)typeof(WorldSession).GetField("inventory",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(w)!;
        foreach(var it in new[]{Item(500,10),Item(501,1),new InventoryItem(502,0,1,0,2,1,1,1,"+0 $0","","")})inv[it.ObjectId]=it;
        ((System.Collections.Concurrent.ConcurrentDictionary<uint,bool>)typeof(WorldSession).GetField("equipmentStates",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(w)!)[502]=true;
        t.Equal((PersonalShopEntry?)null,w.PersonalShopCandidate(502,30,1,true),"equipped item refused");
        t.Equal(new PersonalShopEntry(500,7,"測試物品",30,10),w.PersonalShopCandidate(500,30,10,true),"sell candidate from inventory");
        t.Equal((PersonalShopEntry?)null,w.PersonalShopCandidate(500,30,11,true),"sell more than held refused");
        t.Equal((PersonalShopEntry?)null,w.PersonalShopCandidate(999,30,1,true),"not in inventory refused");
        t.Check(w.PersonalShopCandidate(501,30,50,false) is {Count:50},"buy sample may ask more than held");
        t.Equal((PersonalShopEntry?)null,w.PersonalShopCandidate(500,0,1,false),"buy price 0 refused");

        // ---- 4. 面板版面（全由 XML 讀出）。
        var p=Loaded();
        t.Equal(PersonalShopPanel.WindowId,p.Id,"id = XML window name");
        t.Equal(UiLayer.Window,p.Layer,"window layer");
        t.Check(!p.Visible,"hidden until opened");
        t.Equal(new Rectangle(0,0,184,352),p.Bounds,"bounds = base 184×332 ∪ OK/Cancel push surf (y 334 + 18)");
        t.Equal(new Rectangle(22,42,33,33),p.PartRect(PersonalShopPanel.Part.SellSlot,0),"sell slot 0 at (22,42)");
        t.Equal(new Rectangle(124,76,33,33),p.PartRect(PersonalShopPanel.Part.SellSlot,7),"sell slot 7 = row 1 col 3: 22+3×34, 42+34");
        t.Equal(new Rectangle(22,132,33,33),p.PartRect(PersonalShopPanel.Part.BuySlot,0),"buy slot 0 at (22,132)");
        t.Equal(new Rectangle(124,166,33,33),p.PartRect(PersonalShopPanel.Part.BuySlot,7),"buy slot 7");
        t.Equal(new Rectangle(18,206,144,16),p.PartRect(PersonalShopPanel.Part.Message,0),"message 1 edit");
        t.Equal(new Rectangle(18,224,144,16),p.PartRect(PersonalShopPanel.Part.Message,1),"message 2 edit");
        t.Equal(20,p.MessageLimit,"LimitText 20");
        t.Equal(new Rectangle(17,243,48,62),p.PartRect(PersonalShopPanel.Part.Poly,0),"poly 0");
        t.Equal(new Rectangle(113,243,48,62),p.PartRect(PersonalShopPanel.Part.Poly,2),"poly 2");
        t.Equal(new Rectangle(166,13,8,8),p.PartRect(PersonalShopPanel.Part.Close),"close button from surf 5391 size");
        t.Equal(new Rectangle(19,311,22,13),p.PartRect(PersonalShopPanel.Part.FirstPage),"first page");
        t.Equal(new Rectangle(137,311,22,13),p.PartRect(PersonalShopPanel.Part.LastPage),"last page");
        t.Equal(new Rectangle(30,334,53,17),p.PartRect(PersonalShopPanel.Part.Ok),"OK");
        t.Equal(new Rectangle(98,334,53,17),p.PartRect(PersonalShopPanel.Part.Cancel),"Cancel");
        t.Equal((int?)5734,PersonalShopPanel.SurfId("5734;1"),"surf N;k → N");
        t.Equal((int?)5391,PersonalShopPanel.SurfId("5391"),"plain surf");
        t.Throws<UiLayoutException>(()=>Loaded(Xml(limit:30)),"LimitText ≠ 20 refused");
        t.Throws<UiLayoutException>(()=>Loaded(Xml(polys:2)),"poly count ≠ 3 refused");
        t.Throws<UiLayoutException>(()=>Loaded(Xml(dropOk:true)),"missing OK_Button → UiLayoutException (no default coordinates)");
        t.Throws<UiLayoutException>(()=>Loaded(Xml().Replace("Name = \"PersonalShop_Window\"","Name = \"Other\"")),"missing window refused");
        t.Throws<UiLayoutException>(()=>Loaded(Xml().Replace("5734;1","x;1")),"bad surf refused");

        // ---- 5. 互動。
        var c=new UiNullCommands();byte[]? sent=null;int closed=0;
        p.Confirmed+=b=>sent=b;p.Closed+=()=>closed++;
        t.Check(!p.Mouse(M(UiMouseKind.Down,p.PartRect(PersonalShopPanel.Part.Ok)!.Value),c),"hidden panel ignores input");
        var setup=new PersonalShopSetup();setup.Set(false,3,buyE);
        p.Open(setup);t.Check(p.Visible,"open");
        var slot=p.PartRect(PersonalShopPanel.Part.BuySlot,3)!.Value;
        Click(p,slot,c);t.Equal(((bool,int)?)(false,3),setup.Selected,"click buy slot 3 selects it");
        Click(p,p.PartRect(PersonalShopPanel.Part.Poly,1)!.Value,c);t.Equal(1,setup.Poly,"click poly 1");
        Click(p,p.PartRect(PersonalShopPanel.Part.Message,1)!.Value,c);t.Equal((int?)1,p.FocusedMessage,"click message 2 focuses it");
        setup.SetMessage(1,"收購");
        t.Check(p.Key(new(Keys.Back,true,false),c)&&setup.Messages[1]=="收","Backspace deletes in the focused edit");
        p.Key(new(Keys.Tab,true,false),c);t.Equal((int?)0,p.FocusedMessage,"Tab moves to the other edit");
        // 按住 OK 後移開再放開：不觸發。
        var ok=p.PartRect(PersonalShopPanel.Part.Ok)!.Value;
        p.Mouse(M(UiMouseKind.Down,ok),c);p.Mouse(M(UiMouseKind.Up,slot),c);t.Check(sent==null,"press OK, release elsewhere → nothing");
        Click(p,ok,c);
        t.Check(sent!=null&&sent[0]==38&&sent[1]==0&&sent[2]==0&&sent[3]==0&&sent[4]==1,"OK → Confirmed with C38 (0 sell, 1 buy)");
        t.Check(p.Visible,"OK does not close by itself (result comes from the server)");
        t.Check(p.Mouse(M(UiMouseKind.Down,new Rectangle(5,100,1,1)),c),"click on empty window area is swallowed");
        t.Check(!p.Mouse(M(UiMouseKind.Down,new Rectangle(400,500,1,1)),c),"outside bounds not handled");
        t.Check(p.IsDragHandle(new(60,5))&&!p.IsDragHandle(new(60,30))&&!p.IsDragHandle(new(169,15)),"drag strip = top 18 px, not the close button");
        p.MoveTo(new(300,100));
        t.Equal(new Rectangle(300,100,184,352),p.Bounds,"move");
        t.Equal(new Rectangle(322,142,33,33),p.PartRect(PersonalShopPanel.Part.SellSlot,0),"parts follow the window");
        t.Check(p.Key(new(Keys.Escape,true,false),c)&&!p.Visible&&closed==1,"Esc closes (KeyboardEvent=ClosePersonalShop, 推測)");
        p.Open(setup);Click(p,p.PartRect(PersonalShopPanel.Part.Cancel)!.Value,c);t.Check(!p.Visible&&closed==2,"Cancel closes");
        p.Open(setup);Click(p,p.PartRect(PersonalShopPanel.Part.Close)!.Value,c);t.Check(!p.Visible&&closed==3,"Close button closes");
        p.Open(setup);p.Update(new UiStaticGameState(InWorld:false));t.Check(!p.Visible&&closed==4,"leaving the world closes");
        t.Check(c.Calls.Count==0,"panel uses no outward command");

        // ---- 6. 繪製（合成 surf）。
        var q=Loaded();var qs=new PersonalShopSetup();qs.Set(true,0,sellE);qs.SelectPoly(1);
        var icon=new Bitmap(33,33);using(var g=Graphics.FromImage(icon))g.Clear(Color.FromArgb(255,1,2,3));
        q.IconSource=id=>id==7?icon:null;q.MoveTo(new(100,50));q.Open(qs);
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(q.Bounds);q.Paint(canvas);canvas.Release();
            t.Equal(unchecked((int)ColorOf(5734)),Argb(canvas,100+5,50+100),"base surf drawn at window origin");
            t.Equal(Color.FromArgb(255,1,2,3).ToArgb(),Argb(canvas,100+22,50+42),"sell slot 0 icon from IconSource");
            t.Equal(unchecked((int)ColorOf(5734)),Argb(canvas,100+22+34,50+42+5),"empty slot draws nothing (base shows)");
            t.Equal(unchecked((int)ColorOf(5736)),Argb(canvas,100+17+48+1,50+243+1),"selected poly 1 gets the check surf");
            t.Equal(unchecked((int)ColorOf(5752)),Argb(canvas,100+17+1,50+243+1),"unselected poly 0 shows only the image");
            t.Equal(unchecked((int)ColorOf(5749)),Argb(canvas,100+30+2,50+334+2),"OK base surf");
            t.Equal(Color.Black.ToArgb(),Argb(canvas,100+184+1,50+10),"nothing outside the window");
            q.Mouse(M(UiMouseKind.Move,q.PartRect(PersonalShopPanel.Part.Ok)!.Value),c);
            canvas.Clear(Color.Black);canvas.Restrict(q.Bounds);q.Paint(canvas);canvas.Release();
            t.Equal(unchecked((int)ColorOf(5750)),Argb(canvas,100+30+2,50+334+2),"hover OK → highlight surf");
            q.Mouse(M(UiMouseKind.Down,q.PartRect(PersonalShopPanel.Part.Ok)!.Value),c);
            canvas.Clear(Color.Black);canvas.Restrict(q.Bounds);q.Paint(canvas);canvas.Release();
            t.Equal(unchecked((int)ColorOf(5751)),Argb(canvas,100+30+2,50+334+2),"pressed OK → push surf");
        }
        // 缺 surf（null）：不畫、不補色塊。
        var bare=Loaded(surf:_=>null);bare.Open();
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(bare.Bounds);bare.Paint(canvas);canvas.Release();
            bool black=true;for(int y=0;y<330;y+=7)for(int x=0;x<180;x+=7){if(x>=66&&x<112&&y>=306&&y<330)continue;black&=Argb(canvas,x,y)==Color.Black.ToArgb();}
            t.Check(black,"all surfs null → only text (page label) is drawn, no placeholder");
            t.Equal(new Rectangle(0,0,0,0),bare.PartRect(PersonalShopPanel.Part.Ok)!.Value with{X=0,Y=0},"null button surf → no click area");
        }
        icon.Dispose();
    }
}
