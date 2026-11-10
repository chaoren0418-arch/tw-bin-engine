using System.Reflection;
using System.Text;
using P06.NativeUi;
namespace P06;

// R7-D（T343／#806）離線測試：客戶端自己處理的 HTML 動作（NpcInteraction.cs 的 HtmlClientAction）與 HYPER_TEXT_WINDOW 的名稱框。
//   declarewar（門檻 25、475）→ C227 type 0；surrender／cease（785／478、輸入框 578／579）→ C227 type 2／3；
//   pledgefocusadd（輸入框 3361）→ C129 子碼 0；propose／divorce → C50 0／1。依據 FU6§1.1、§3.2、§4.1。
// 只用依 3.81／TW16 欄位合成的封包、合成字串表（假文字）、合成 XML／圖與記憶體送出閘門；不開遊戲、網路、DB，也不讀客戶端素材。由 --module-tests 執行。
static class HtmlActionTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Npc=806;
    const string Me="r7d-self",MyClan="紅血盟",Castle="黑騎士血盟",Enemy="藍血盟";
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static byte[] Dialog(uint id,string html,params string[] args)=>Packet(b=>{b.Write((byte)39);b.Write(id);Text(b,html);b.Write((ushort)(args.Length>0?1:0));b.Write((ushort)args.Length);foreach(var a in args)Text(b,a);});
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static string[] Chat(WorldSession s)=>s.PlayerChatLines.Select(l=>l.Text).ToArray();
    static bool Same(byte[] a,byte[] b)=>a.SequenceEqual(b);

    static ServerStringTable Strings(){
        var lines=new string[3484];for(int i=0;i<lines.Length;i++)lines[i]="";
        lines[475]="T475";lines[478]="T478";lines[785]="T785";lines[232]="T232";lines[272]="T272";lines[234]="T234";
        lines[578]="T578";lines[579]="T579";lines[3361]="T3361";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(lines.Length+"\r\n"+string.Join("\r\n",lines)));
    }

    /// <summary>王族（職業 0）、血盟君主（階級 4）、等級 30；面前一格有 NPC 並已選取。</summary>
    static LoginConnection Seed(List<byte[]> sent,byte cls=0,byte rank=4,byte level=30,string clan=MyClan){
        var c=new LoginConnection();c.SeedQ4(Me,cls,clan,rank,0,level,sent);
        // S87 NPC 相鄰一格（欄位同 trade-window 測試）。
        c.World.Receive(Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Npc);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,"NPC");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}),"offline");
        c.World.SelectTarget(Npc);
        return c;
    }
    /// <summary>對話 → S39（帶參數）→ 換成指定選項的頁（本測試不載入範本，用反射放頁）。</summary>
    static bool OpenNpcPage(LoginConnection c,string[] args,params (int id,string action)[] options){
        if(!c.World.CanStartInteraction)c.World.CloseInteraction();
        if(!Wait(c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision)))return false;
        c.World.Receive(Dialog(Npc,"adguard6",args),"offline");
        Set(c.World,"InteractionPage",new NpcPage("城門守衛",options.Select(o=>new NpcOption(o.id,o.action,"action",o.action,true)).ToArray(),""));
        return c.World.InteractionKind=="dialog";
    }
    static bool Option(LoginConnection c,int id)=>Wait(c.Interact("option",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision,id));
    static readonly string[] GuardArgs=["守衛",Castle,"國王"];

    [WorldModuleTest("html-actions")]
    static void Run(WorldModuleTestContext t){
        Names(t);
        using(ServerMessageText.UseTable(Strings())){DeclareWar(t);SurrenderCease(t);PledgeFocusOnServerPage(t);}
        Marriage(t);Fallthrough(t);
        NameBoxPanel(t);Wiring(t);
    }

    static void Names(WorldModuleTestContext t){
        t.Equal("declarewar,surrender,cease,pledgefocusadd,propose,divorce",string.Join(",",HtmlActions.Names),"本組六個 HyperAct 名稱");
        t.Equal<string?>("declarewar",HtmlActions.Normalize(" DeclareWar "),"名稱去空白、不分大小寫");
        t.Equal<string?>(null,HtmlActions.Normalize("war"),"war 不在 TW16 HyperAct 表（FU6§3.2）→ 不是本組");
        t.Equal<string?>(null,HtmlActions.Normalize("pledgefocus"),"pledgefocus 沒追 → 不是本組");
        t.Check(HtmlActions.PromptText(HtmlNameKind.Surrender)==578&&HtmlActions.PromptText(HtmlNameKind.Cease)==579&&HtmlActions.PromptText(HtmlNameKind.PledgeFocusAdd)==3361,"輸入框 578／579／3361");
        t.Equal(1,HtmlActions.DeclareWarArgument,"declarewar 取 HTML 參數 1");
        t.Equal(new ClanWarProtocol.Decision(false,475),HtmlActions.CheckDeclare(new(0,4,0,24,MyClan,[]),Castle),"等級 24 → 475（不是 232）");
        t.Equal(new ClanWarProtocol.Decision(true,null),HtmlActions.CheckDeclare(new(0,4,0,25,MyClan,[]),Castle),"等級 25 → 送");
    }

    // ---- declarewar：城門守衛頁 #1＝城盟名，sub_F46290(名稱, 25) ----
    static void DeclareWar(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        t.Check(OpenNpcPage(c,GuardArgs,(1,"declarewar")),"NPC 頁開啟（帶 3 個參數）");
        int count=sent.Count;
        t.Check(Option(c,1)&&sent.Count==count+1&&Same(sent[^1],ClanWarProtocol.Encode(ClanWarCommand.Declare,Castle)),"declarewar → C227 type 0＋參數 1，不送 C125",sent.Count>0?Convert.ToHexString(sent[^1]):"");
        t.Equal("dialog",c.World.InteractionKind,"送出後頁面維持");

        c.World.Q4SetLevel(24);count=sent.Count;int lines=c.World.PlayerChatLines.Length;
        t.Check(!Option(c,1)&&sent.Count==count,"等級 24：不送");
        t.Check(c.World.PlayerChatLines.Length==lines+1&&Chat(c.World)[^1]=="T475","等級不足 → 聊天欄印 475",Chat(c.World).LastOrDefault());
        c.World.Q4SetLevel(30);c.Q4SetClass(2);
        t.Check(!Option(c,1)&&sent.Count==count&&Chat(c.World)[^1]=="T478","非王族 → 478");
        c.Q4SetClass(0);c.World.Q4SetActor(a=>a with{Rank=7});
        t.Check(!Option(c,1)&&sent.Count==count&&Chat(c.World)[^1]=="T785","階級 7 → 785");
        c.World.Q4SetActor(a=>a with{Rank=10,Gfx=2});lines=c.World.PlayerChatLines.Length;
        t.Check(!Option(c,1)&&sent.Count==count&&c.World.PlayerChatLines.Length==lines,"變身中（外型 > 1）：不送也不提示");
        c.World.Q4SetActor(a=>a with{Gfx=0});

        // 頁上沒有參數 1：名稱空白，不送也不提示。
        t.Check(OpenNpcPage(c,["守衛"],(1,"declarewar")),"沒有參數 1 的頁");
        count=sent.Count;lines=c.World.PlayerChatLines.Length;
        t.Check(!Option(c,1)&&sent.Count==count&&c.World.PlayerChatLines.Length==lines,"沒有城盟名：不送");

        var none=new List<byte[]>();using var free=Seed(none,clan:"");
        t.Check(OpenNpcPage(free,GuardArgs,(1,"declarewar")),"沒有血盟的王族");
        count=none.Count;
        t.Check(!Option(free,1)&&none.Count==count&&Chat(free.World)[^1]=="T272","沒有血盟 → 272");
    }

    // ---- surrender／cease：本機檢查後開名稱框，確定才送 ----
    static void SurrenderCease(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        t.Check(OpenNpcPage(c,[],(1,"surrender"),(2,"cease")),"血盟頁");
        int count=sent.Count,before=c.World.InteractionRevision;
        t.Check(Option(c,1)&&sent.Count==count,"surrender：開框，不送包");
        var p=c.World.HtmlNameInput;
        t.Check(p is {Kind:HtmlNameKind.Surrender,Query:false,PromptId:578,Prompt:"T578"},"名稱框 578（字串表）",p);
        t.Check(c.World.InteractionRevision==before+1&&p!.PageRevision==c.World.InteractionRevision,"開框時頁版本 +1");
        t.Check(c.HtmlNameCanSend,"可送");
        t.Check(!Wait(c.SubmitHtmlName(p.Revision+1,Enemy))&&c.World.HtmlNameInput!=null&&sent.Count==count,"版本不符：不送、框保留");
        t.Check(Wait(c.SubmitHtmlName(p.Revision,"  "+Enemy+" "))&&sent.Count==count+1&&Same(sent[^1],LoginPackets.Pad([227,2,..LoginPackets.TextEncoding.GetBytes(Enemy),0])),"確定 → C227 type 2（去頭尾空白）");
        t.Check(c.World.HtmlNameInput==null&&c.World.InteractionKind=="dialog","確定後框關、頁維持");

        t.Check(Option(c,2)&&c.World.HtmlNameInput is {Kind:HtmlNameKind.Cease,PromptId:579,Prompt:"T579"},"cease → 名稱框 579");
        count=sent.Count;
        t.Check(!Wait(c.SubmitHtmlName(c.World.HtmlNameInput!.Revision,""))&&sent.Count==count&&c.World.HtmlNameInput==null,"空白名稱：框關、不送（名稱非空才送）");
        t.Check(Option(c,2)&&Wait(c.SubmitHtmlName(c.World.HtmlNameInput!.Revision,Enemy))&&Same(sent[^1],LoginPackets.Pad([227,3,..LoginPackets.TextEncoding.GetBytes(Enemy),0])),"cease 確定 → C227 type 3");
        t.Check(Option(c,1)&&c.CancelHtmlName(c.World.HtmlNameInput!.Revision)&&c.World.HtmlNameInput==null&&sent.Count==count+1,"取消：只關框");
        t.Check(!c.CancelHtmlName(12345),"沒有框時取消回 false");

        // 頁換掉（新的 S39）或關對話時框失效。
        t.Check(Option(c,1)&&c.World.HtmlNameInput!=null,"再開框");
        c.World.Receive(Dialog(Npc,"other"),"offline");
        t.Check(c.World.HtmlNameInput==null,"NPC 換頁 → 框失效");
        Set(c.World,"InteractionPage",new NpcPage("血盟",[new NpcOption(1,"surrender","action","surrender",true)],""));
        t.Check(Option(c,1)&&c.World.HtmlNameInput!=null,"再開框 2");
        c.World.CloseInteraction();
        t.Check(c.World.HtmlNameInput==null,"關對話 → 框失效");

        // 本機檢查：階級不是 4／10 → 785、不開框；非王族 → 什麼都不做。
        t.Check(OpenNpcPage(c,[],(1,"surrender"),(2,"cease")),"血盟頁 2");
        c.World.Q4SetActor(a=>a with{Rank=0});int lines=c.World.PlayerChatLines.Length;
        t.Check(!Option(c,1)&&c.World.HtmlNameInput==null&&Chat(c.World)[^1]=="T785"&&c.World.PlayerChatLines.Length==lines+1,"階級 0 → 785，不開框");
        c.World.Q4SetActor(a=>a with{Rank=10});c.Q4SetClass(5);lines=c.World.PlayerChatLines.Length;
        t.Check(!Option(c,2)&&c.World.HtmlNameInput==null&&c.World.PlayerChatLines.Length==lines,"非王族：不開框也不提示");
        c.Q4SetClass(0);
        t.Check(Option(c,2)&&c.World.HtmlNameInput!=null,"階級 10 → 開框");
    }

    // ---- pledgefocusadd：伺服器主動頁（血盟選項頁 optionplp），框 3361 → C129 子碼 0；沒有本機檢查 ----
    static void PledgeFocusOnServerPage(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent,cls:3,rank:7);
        c.World.Receive(Dialog(Q4FollowupTests.Own,"optionplp"),"offline");
        t.Check(c.World.QueryVisible&&c.World.QueryActionable,"自己 id 的 S39 → 伺服器主動頁（可操作）");
        Set(c.World,"QueryPage",new NpcPage("血盟選項",[new NpcOption(1,"新增關注","action","pledgefocusadd",true),new NpcOption(2,"投降","action","surrender",true)],""));
        int count=sent.Count,before=c.World.QueryRevision;
        t.Check(Wait(c.QueryOption(1,c.World.QueryRevision,c.World.WorldEpoch))&&sent.Count==count,"pledgefocusadd：開框，不送 C125");
        var p=c.World.HtmlNameInput;
        t.Check(p is {Kind:HtmlNameKind.PledgeFocusAdd,Query:true,PromptId:3361,Prompt:"T3361"}&&c.World.QueryRevision==before+1&&c.World.QueryVisible,"名稱框 3361，開在伺服器主動頁，頁不關",p);
        t.Check(Wait(c.SubmitHtmlName(p!.Revision,"甲盟"))&&sent.Count==count+1&&Same(sent[^1],ClanProtocol.EncodeAttentionAdd("甲盟")),"確定 → C129 子碼 0（不查職業與階級）");
        t.Check(c.World.HtmlNameInput==null&&c.World.QueryVisible,"框關、頁維持");
        // 同一頁的 surrender：職業 3 → 什麼都不做。
        count=sent.Count;
        t.Check(!Wait(c.QueryOption(2,c.World.QueryRevision,c.World.WorldEpoch))&&sent.Count==count&&c.World.HtmlNameInput==null&&c.World.QueryVisible,"伺服器頁的 surrender 也走本機檢查（非王族不動作），不送 C125");
        t.Check(Wait(c.QueryOption(1,c.World.QueryRevision,c.World.WorldEpoch))&&c.World.HtmlNameInput!=null,"再開框");
        c.World.CloseQueryPage();
        t.Check(c.World.HtmlNameInput==null,"關伺服器頁 → 框失效");
    }

    // ---- propose／divorce：只送 C50，不檢查 ----
    static void Marriage(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent,cls:2,rank:0,clan:"");
        t.Check(OpenNpcPage(c,[],(1,"propose"),(2,"divorce")),"頁上有 propose／divorce");
        int count=sent.Count;
        t.Check(Option(c,1)&&sent.Count==count+1&&Same(sent[^1],LoginPackets.Pad([50,0])),"propose → C50 0（沒有目標欄位）");
        t.Check(c.MarriageSnapshot().Notice.Contains("/propose"),"提示同 /propose",c.MarriageSnapshot().Notice);
        t.Check(Option(c,2)&&sent.Count==count+2&&Same(sent[^1],LoginPackets.Pad([50,1])),"divorce → C50 1");
        t.Check(c.MarriageSnapshot().Notice.Contains("653"),"離婚提示提到 653");
    }

    // ---- 不是本組的動作照舊送 C125 ----
    static void Fallthrough(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        t.Check(OpenNpcPage(c,GuardArgs,(1,"askwartime")),"其他動作");
        int count=sent.Count;
        t.Check(Option(c,1)&&sent.Count==count+1&&Same(sent[^1],NpcPackets.Action(Npc,"askwartime")),"askwartime → C125");
        t.Check(c.World.HtmlNameInput==null,"沒有框");
    }

    // ---- HYPER_TEXT_WINDOW 的名稱框 ----
    sealed class Host:IHyperTextHost {
        public List<string> Calls{get;}=[];
        public void Link(HyperTextView shown,int option)=>Calls.Add("link:"+option);
        public void Submit(HyperTextView shown,long value)=>Calls.Add("submit:"+value);
        public void CancelNumber(HyperTextView shown)=>Calls.Add("cancel-number");
        public void Close(HyperTextView shown)=>Calls.Add("close");
        public void SubmitName(HyperTextView shown,string name)=>Calls.Add($"name:{shown.Name?.Revision}:{name}");
        public void CancelName(HyperTextView shown)=>Calls.Add($"name-cancel:{shown.Name?.Revision}");
    }
    sealed class OldHost:IHyperTextHost {
        public void Link(HyperTextView shown,int option){}
        public void Submit(HyperTextView shown,long value){}
        public void CancelNumber(HyperTextView shown){}
        public void Close(HyperTextView shown){}
    }
    static string Xml()=>"""
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
    static UiSurface Surf(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF203040u,w*h).ToArray()));
    static Func<int,UiSurface?> Surfaces()=>id=>id switch{1968=>Surf(id,220,270),1969=>Surf(id,240,4),1962=>Surf(id,12,12),1970=>Surf(id,8,220),2040 or 2042=>Surf(id,8,12),2044=>Surf(id,8,8),_=>null};
    static UiMouseEvent M(UiMouseKind kind,Point at)=>new(kind,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,Rectangle r,IUiCommands cmd){var at=new Point(r.X+r.Width/2,r.Y+r.Height/2);p.Mouse(M(UiMouseKind.Down,at),cmd);p.Mouse(M(UiMouseKind.Up,at),cmd);}
    static HyperTextView Page(int rev,HyperTextNameBox? name,HyperTextNumberBox? number=null)=>new(HyperTextSource.Npc,rev,"血盟選項",[new HyperTextLink(1,"宣告投降",true)],"",number,name);
    static UiKeyEvent K(Keys k)=>new(k,true,false);

    static void NameBoxPanel(WorldModuleTestContext t){
        var p=new HyperTextPanel(s=>s.Length*12);p.Load(UiLayout.Parse(Xml(),"synthetic-uiextra86.xml"),Surfaces());var h=new Host();p.Attach(h);var cmd=new UiNullCommands();
        p.Show(Page(1,null));
        t.Check(p.NameRect.IsEmpty&&!p.NameFocused&&p.Rows.All(r=>r.Kind!=HyperTextPanel.RowKind.Name),"沒有名稱框時沒有名稱列");
        p.Show(Page(2,new(7,"請輸入要投降的血盟名稱。",true)));
        t.Equal("Text,Text,Link,Text,Text,Name",string.Join(",",p.Rows.Select(r=>r.Kind)),"名稱框：空行、提示（換行）、名稱列");
        t.Check(p.NameFocused&&p.NameInput==""&&!p.NameRect.IsEmpty,"新框取得焦點");
        t.Check(p.NameRect.Width==HyperTextPanel.NameInputWidth&&p.NameCancelRect.Right-p.NameRect.Left==HyperTextPanel.TextWidth,"框＋確定＋取消＝192 寬");
        t.Check(p.Char('藍')&&p.Char(' ')&&p.Char('\t')&&p.Char('盟')&&p.NameInput=="藍盟","收一般字；空白與控制字元吃掉不收",p.NameInput);
        t.Check(p.Key(K(Keys.Back),cmd)&&p.NameInput=="藍","Backspace 刪一字");
        foreach(char ch in "一二三四五六七八九")p.Char(ch);
        t.Equal("藍一二三四五六七",p.NameInput,"最多 16 個 Big5 位元組（8 個中文字）");
        t.Check(p.Key(K(Keys.Enter),cmd)&&h.Calls[^1]=="name:7:藍一二三四五六七"&&!p.NameFocused,"Enter → host.SubmitName，放開焦點");
        t.Check(!p.Key(K(Keys.Enter),cmd),"放開後 Enter 不吃（交給聊天）");
        Click(p,p.NameRect,cmd);
        t.Check(p.NameFocused,"點框取得焦點");
        t.Check(p.Key(K(Keys.Escape),cmd)&&h.Calls[^1]=="name-cancel:7","Esc → host.CancelName");
        Click(p,p.NameSubmitRect,cmd);
        t.Equal("name:7:藍一二三四五六七",h.Calls[^1],"點「確定」→ SubmitName");
        Click(p,p.NameCancelRect,cmd);
        t.Equal("name-cancel:7",h.Calls[^1],"點「取消」→ CancelName");
        p.Show(Page(2,new(7,"請輸入要投降的血盟名稱。",false)));
        int calls=h.Calls.Count;Click(p,p.NameSubmitRect,cmd);
        t.Check(!p.CanSubmitName&&h.Calls.Count==calls&&p.NameInput=="藍一二三四五六七","宿主說不能送：確定無效、字保留");
        p.Show(Page(3,new(8,"T3361",true)));
        t.Check(p.NameInput==""&&p.NameFocused,"新的框（版本不同）清空並取得焦點");
        Click(p,p.LineRect(0),cmd);
        t.Check(!p.NameFocused&&!p.Char('x')&&p.NameInput=="","點窗內其他地方放開焦點，字不收");
        // 數字框與名稱框同時：名稱框取得焦點，數字框讓出。
        p.Show(Page(4,new(9,"T578",true),new(HyperTextNumberKind.Amount,1,1,10,1,true,true,"1–10")));
        t.Check(p.NameFocused&&!p.InputFocused,"同時有數字框：名稱框優先取得焦點");
        p.Show(Page(4,null));
        t.Check(p.NameRect.IsEmpty&&!p.NameFocused,"框關掉後名稱列消失");
        using var canvas=new UiCanvas();
        p.Show(Page(5,new(10,"T579",true)));p.Char('甲');
        canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
        var box=p.NameRect;
        t.Equal(Color.Black.ToArgb(),canvas.Bitmap.GetPixel(box.Right-6,box.Y+box.Height/2).ToArgb(),"名稱框內部填黑（蓋住底圖）");
        // 舊宿主沒實作名稱方法：預設不做事、不丟例外。
        var old=new HyperTextPanel(s=>s.Length*12);old.Load(UiLayout.Parse(Xml(),"o.xml"),Surfaces());old.Attach(new OldHost());
        old.Show(Page(1,new(1,"T578",true)));
        t.Check(old.Key(K(Keys.Enter),cmd)&&!old.NameFocused,"舊宿主：Enter 走預設 SubmitName（不做事）");
    }

    static void Wiring(WorldModuleTestContext t){
        var host=typeof(ClientForm).GetNestedType("NativeHyperTextHost",BindingFlags.NonPublic);
        t.Check(host?.GetMethod("SubmitName",F)?.DeclaringType==host&&host?.GetMethod("CancelName",F)?.DeclaringType==host,"NpcUi 的原生宿主實作 SubmitName／CancelName");
        t.Check(typeof(ClientForm).GetMethod("HtmlNameBox",F)!=null&&typeof(ClientForm).GetMethod("AddHtmlNamePrompt",F)!=null,"NpcUi 把名稱框交給原生窗；原生 UI 關閉時有 WinForms 備援");
        t.Check(typeof(LoginConnection).GetMethod("HtmlClientAction",F)!=null,"HTML 動作分派存在");
    }
}
