using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R9-I（T369／#853）離線測試：私商查詢頁（PrivateShopUi.cs 的 UserShopPageModel／UserShopPagePanel 與 ClientForm 接線）。
// 只用合成 HTML（結構同 usershop：#0 店主名＋兩顆帶 userShop0／1 動作的圖）、合成 XML／圖、固定字寬與合成封包；
// 不開遊戲、網路、視窗、伺服器、DB，也不讀客戶端素材。由 --module-tests 執行（群組 usershop-page）。
static class UserShopPageTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Owner=456;

    [WorldModuleTest("usershop-page")]
    static void Run(WorldModuleTestContext t){
        Actions(t);Model(t);Panel(t);Packet(t);Registration(t);Wiring(t);
    }

    // 合成頁：與 usershop 同結構（文字不同、不是客戶端檔案）。
    const string Html="<body><font fg=ffffff><p align=left><var src=\"#0\">的攤位</p></font><br>請選擇<br><p align=center>"+
        "<img src=\"#312\" action=\"userShop0\"></img>&nbsp;<img src=\"#314\" action=\"userShop1\"></img></p></body>";
    static NpcTemplates Templates(params (string Key,string Html)[] pages){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        var d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var (k,h) in pages)d[k]=h;
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,d);
        return t;
    }

    static void Actions(WorldModuleTestContext t){
        t.Equal<bool?>(false,UserShopPageModel.ActionSell("userShop0"),"userShop0 → 向店主買（type 0）");
        t.Equal<bool?>(true,UserShopPageModel.ActionSell("userShop1"),"userShop1 → 賣給店主（type 1）");
        t.Equal<bool?>(true,UserShopPageModel.ActionSell("USERSHOP1"),"動作名不分大小寫");
        foreach(var bad in new[]{"userShop2","userShop","userShop01","buy","",null})
            t.Equal<bool?>(null,UserShopPageModel.ActionSell(bad),"非 userShop0／1 不送："+(bad??"null"));
    }

    static void Model(WorldModuleTestContext t){
        t.Check(UserShopPageModel.Open(null,Owner,1,2,3,"店主","我")==null,"沒有模板表 → null（退回 WinForms）");
        t.Check(UserShopPageModel.Open(Templates(("other","<html>x</html>")),Owner,1,2,3,"店主","我")==null,"沒有 usershop 模板 → null");
        var templates=Templates((UserShopPageModel.Html,Html));
        t.Check(UserShopPageModel.Open(templates,0,1,2,3,"店主","我")==null,"店主 id 0 → null");
        var m=UserShopPageModel.Open(templates,Owner,1,2,3,"阿明","我");
        t.Check(m!=null,"usershop 模板開頁");
        if(m==null)return;
        t.Check(m.Page.Text.StartsWith("阿明的攤位",StringComparison.Ordinal),"#0＝店主名",m.Page.Text);
        t.Equal(2,m.Page.Options.Length,"兩顆圖片鈕");
        t.Check(m.Document!=null,"HTML 文件對得上 → 行內排版（R9-F）");
        t.Equal<bool?>(false,m.Sell(0),"選項 0 → userShop0");
        t.Equal<bool?>(true,m.Sell(1),"選項 1 → userShop1");
        t.Equal<bool?>(null,m.Sell(2),"不存在的選項");
        t.Check(m.Current(Owner,1,2),"同店主、同世界、同目標版本 → 有效");
        t.Check(!m.Current(Owner+1,1,2)&&!m.Current(null,1,2)&&!m.Current(Owner,9,2)&&!m.Current(Owner,1,9),"換目標／世界／目標版本 → 失效");
        var v=m.View(true,false,"");
        t.Check(v.Links.Single(l=>l.Id==0).Enabled&&!v.Links.Single(l=>l.Id==1).Enabled,"可買不可賣 → 只有 userShop0 可按");
        t.Check(v.Number==null&&v.Name==null&&v.Revision==3&&ReferenceEquals(v.Document,m.Document),"頁上沒有數字框／名稱框；版本與文件照帶");
        t.Equal("提示",m.View(true,true,"提示").Notice,"提示文字照帶");
        // 其他動作（不是 userShop0／1）一律停用。
        var odd=UserShopPageModel.Open(Templates((UserShopPageModel.Html,"<body><var src=\"#0\"><img src=\"#312\" action=\"buy\"></img></body>")),Owner,1,2,3,"甲","我");
        t.Check(odd!=null&&odd.View(true,true,"").Links.All(l=>!l.Enabled)&&odd.Sell(0)==null,"非 userShop 動作停用、不送");
    }

    // 元素名稱與座標同 HyperTextPanelTests 的合成 uiextra86（版面數值，不是圖）。
    const string Xml="""
<?xml version="1.0"?>
<root>
<HYPER_TEXT_WINDOW x = "100" y = "50">
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_HYPER_TEXT_BASE" surfNumber = "1968" xPos = "6" yPos = "9"/>
	<SURF_IMAGE surfName = "SURF_RENEWAL_WIN_HYPER_TEXT_FRAME" surfNumber = "1969"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_BTN_QUIT_X" surfNumber = "1962" xPos = "225" yPos = "15"/>
	<IMAGE surfName = "SURF_RENEWAL_WIN_SCROLLBAR_LONG" surfNumber = "1970" xPos = "217" yPos = "51"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber = "2040" xPos = "217" yPos = "275"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber = "2042" xPos = "217" yPos = "37"/>
	<IMAGE_BTN surfName = "SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber = "2044"/>
</HYPER_TEXT_WINDOW>
</root>
""";
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    // 1109／1107＝SurfList 換號後的 312／314（R9-F）；合成圖。
    static UiSurface? Surfaces(int id)=>id switch{
        1968=>Surf(id,220,270,0xFF203040),1969=>Surf(id,240,4,0xFF999999),1962=>Surf(id,12,12,0xFFFF0000),
        1970=>Surf(id,8,220,0xFF111111),2040 or 2042=>Surf(id,8,12,0xFF222222),2044=>Surf(id,8,8,0xFF333333),
        1109=>Surf(id,40,20,0xFF00AA00),1107=>Surf(id,40,20,0xFF0000AA),_=>null};
    sealed class Host:IHyperTextHost {
        public List<string> Calls{get;}=[];
        public void Link(HyperTextView shown,int option)=>Calls.Add($"link:{shown.Revision}:{option}");
        public void Submit(HyperTextView shown,long value)=>Calls.Add("submit");
        public void CancelNumber(HyperTextView shown)=>Calls.Add("cancel");
        public void Close(HyperTextView shown)=>Calls.Add($"close:{shown.Revision}");
    }
    static UiMouseEvent M(UiMouseKind kind,Point p)=>new(kind,p,MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,Point at,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,at),cmd);p.Mouse(M(UiMouseKind.Up,at),cmd);}
    static Point Mid(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);

    static void Panel(WorldModuleTestContext t){
        var m=UserShopPageModel.Open(Templates((UserShopPageModel.Html,Html)),Owner,1,2,7,"阿明","我")!;
        var p=new UserShopPagePanel(s=>s.Length*12);p.Load(UiLayout.Parse(Xml,"synthetic-uiextra86.xml"),Surfaces);
        var h=new Host();p.Page.Attach(h);var cmd=new UiNullCommands();
        t.Check(!p.Visible&&p.Bounds.IsEmpty,"沒有頁時不顯示");
        t.Check(p is IUiClosablePanel&&p is IUiMovablePanel&&p.Layer==UiLayer.Window,"可關閉、可拖曳的 Window 層窗");
        p.Page.Show(m.View(true,true,""));
        t.Check(p.Visible&&!p.Bounds.IsEmpty&&p.Page.Document!=null,"開頁；行內排版");
        var buy=p.Page.LinkRect(0);var sell=p.Page.LinkRect(1);
        t.Check(!buy.IsEmpty&&!sell.IsEmpty&&buy.Width==40&&sell.Width==40,"兩顆圖片鈕畫在文字流裡（1109／1107）",new{buy,sell});
        Click(p,Mid(sell),cmd);
        t.Check(h.Calls.LastOrDefault()=="link:7:1","點 userShop1 → 宿主收到選項 1",h.Calls);
        Click(p,Mid(buy),cmd);
        t.Check(h.Calls.LastOrDefault()=="link:7:0","點 userShop0 → 宿主收到選項 0",h.Calls);
        // 不可按的鈕不送。
        p.Page.Show(m.View(false,true,""));int before=h.Calls.Count;Click(p,Mid(p.Page.LinkRect(0)),cmd);
        t.Equal(before,h.Calls.Count,"不可查詢買入時 userShop0 不動作");
        // ESC 關窗（R9-E sub_875F80）→ 宿主 Close；同一版本不再浮出，Show(null) 後新頁可再開。
        t.Check(p.Key(new UiKeyEvent(Keys.Escape,true,false),cmd)&&!p.Visible&&h.Calls.LastOrDefault()=="close:7","ESC 關頁並通知宿主");
        p.Page.Show(m.View(true,true,""));t.Check(!p.Visible,"關掉的同一頁不再浮出");
        p.Page.Show(null);p.Page.Show(m.View(true,true,""));t.Check(p.Visible,"宿主關頁後可再開");
        p.Close();t.Check(!p.Visible&&h.Calls.LastOrDefault()=="close:7","堆疊／關閉鈕的 Close() 同樣關頁");
        p.Page.Show(null);p.Page.Show(m.View(true,true,""));p.Update(new UiStaticGameState(InWorld:false));t.Check(!p.Visible,"離開世界時關頁");
    }

    // 合成店主（同 PrivateShopFollowupTests 的種子）；點頁上的鈕走原有的 LoginConnection.SendPrivateShop → C47 type＋店主 id。
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Pkt(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        c.World.Receive(Pkt(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Owner);b.Write((ushort)1110);b.Write((byte)70);b.Write((byte)6);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);Text(b,"shopkeeper");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}),"offline");
        c.World.SelectTarget(Owner);c.World.Receive(LoginPackets.Pad([5,0]),"offline");
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static void Packet(WorldModuleTestContext t){
        var sent=new List<byte[]>();var c=Seed(sent);var w=c.World;
        t.Check(w.Target is {ShopActive:true},"合成店主在擺攤");
        var m=UserShopPageModel.Open(Templates((UserShopPageModel.Html,Html)),Owner,w.WorldEpoch,w.TargetRevision,1,w.Target?.Name??"",w.Actor?.Name??"")!;
        t.Check(m.Current(w.TargetId,w.WorldEpoch,w.TargetRevision),"開頁時的店主仍是目前目標");
        var v=c.PrivateShopSnapshot();
        t.Check(v.CanQueryBuy,"可查詢買入 → userShop0 可按");
        bool sell=m.Sell(0)!.Value;
        t.Equal(PrivateShopResult.Accepted,c.SendPrivateShop(true,sell,0,0,v).GetAwaiter().GetResult(),"userShop0 → 送出查詢");
        t.Check(sent.Count==1&&sent[0].Length>=6&&sent[0][0]==47&&sent[0][1]==0&&BitConverter.ToUInt32(sent[0],2)==Owner,"C47 type 0＋店主 id",sent.Select(p=>Convert.ToHexString(p)).ToArray());
        t.Equal(Convert.ToHexString(PrivateShopPackets.Query(Owner,false)),Convert.ToHexString(sent[0]),"與原有 PrivateShopPackets.Query 相同");
        t.Equal("querying",w.PrivateShopPhase,"送出後等待 S140");
        t.Equal(Convert.ToHexString(PrivateShopPackets.Query(Owner,true)),Convert.ToHexString(PrivateShopPackets.Query(Owner,m.Sell(1)!.Value)),"userShop1 → C47 type 1");
        t.Check(PrivateShopPackets.Query(Owner,true)[1]==1,"type 1 位元組");
        w.SelectTarget(null);t.Check(!m.Current(w.TargetId,w.WorldEpoch,w.TargetRevision),"取消選取店主 → 頁失效");
    }

    static void Registration(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Errors.Count==0,"panel auto-discovery has no errors",UiPanelRegistry.Errors);
        t.Equal(1,UiPanelRegistry.Panels.Count(r=>r.Id==UserShopPagePanel.PanelId&&r.Layout==HyperTextPanel.LayoutName),"USERSHOP_HYPER_TEXT registered once on uiextra86");
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==UserShopPagePanel.PanelId).Create(new UiRoot()) is UserShopPagePanel,"registration creates UserShopPagePanel");
        t.Equal(1,UiPanelRegistry.Panels.Count(r=>r.Id==HyperTextPanel.PanelId),"HYPER_TEXT_WINDOW still registered once");
        t.Check(UiPanelRegistry.Panels.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()==UiPanelRegistry.Panels.Count,"registry ids stay unique");
    }

    static void Wiring(WorldModuleTestContext t){
        const BindingFlags D=BindingFlags.Instance|BindingFlags.NonPublic;
        foreach(var name in new[]{"OpenUserShopPage","SyncUserShopPage","RunUserShopLink","CloseUserShopPage","OpenPrivateShopFromTalk","SyncPrivateShop"})
            t.Check(typeof(ClientForm).GetMethod(name,D)!=null,"ClientForm."+name+" present");
        t.Check(typeof(ClientForm).GetMethod("NativeUserShopPage",D)?.ReturnType==typeof(UserShopPagePanel),"native usershop window lookup present");
        var host=typeof(ClientForm).GetNestedType("UserShopPageHost",BindingFlags.NonPublic);
        t.Check(host!=null&&typeof(IHyperTextHost).IsAssignableFrom(host),"host adapter implements IHyperTextHost");
    }
}
