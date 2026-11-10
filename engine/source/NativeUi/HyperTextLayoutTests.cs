using System.Reflection;
using System.Runtime.CompilerServices;
namespace P06.NativeUi;

// R9-F（T366／#850）離線測試：HyperText 行內排版器（NativeUi/HyperTextLayout.cs）與 HYPER_TEXT_WINDOW 的 Rich 列。
// 依據 docs/reference/followups-9.md 第 10.1 節、第 16.2 節 R9-F。只用自寫的合成 HTML、合成圖與固定字寬（每字 12 px）；
// 不讀客戶端素材、不開遊戲／網路／視窗。由 --module-tests 執行（群組 hypertext-layout）。
static class HyperTextLayoutTests {
    [WorldModuleTest("hypertext-layout")]
    static void Run(WorldModuleTestContext t){
        Parity(t);Colors(t);Styles(t);Options(t);Vars(t);Layout(t);Resolve(t);PanelRich(t);PanelHover(t);PanelFallback(t);PanelScroll(t);Session(t);
    }

    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int Measure(string s)=>s.Length*12;
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint BuyColor=0xFF00FF00,TallColor=0xFF0000FF;
    static Func<int,UiSurface?> Images=id=>id switch{1109=>Surf(id,46,30,BuyColor),4000=>Surf(id,40,40,TallColor),_=>null};

    // 自寫的頁（格式仿 Text.pak 的 NPC 頁：<font fg>、<p align>、行內 <a>、<img src="#312" action="buy">）。
    const string Shop="<body><font fg=ffffff><p align=left><name>: </p></font><br>歡迎，<username>。請選擇：<a action=\"buy\">購買物品</a>或<a link=\"next\">下一頁</a>。<br>"
        +"<p align=center><img src=\"#312\" action=\"buy\"></img></p><br>這是<font fg=ffaaff>特別</font>的<b>粗體</b><i>斜體</i><u>底線</u>。</body>";
    const string Nested="<html>甲<a action=\"x\">乙<a action=\"y\">丙</a>丁</html>";
    const string VarsHtml="<var src=\"#0\"> <var src=\"#1\"> <var src=\"#5\"> <var src=\"abc\">";
    const string Blocks="<center>中</center><p align=right>右</p>\n\n\n<br><br><br>尾<img src=\"#331\" link=\"next\"><img src=\"p12\">";
    const string Tokens="取得 $1234 一個。<a action=\"ok\">確定 $1234</a>";

    static NpcTemplates Templates(params (string Key,string Html)[] pages){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        var d=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["next"]="<html>下一頁</html>"};
        foreach(var (k,h) in pages)d[k]=h;
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,d);
        return t;
    }

    // 1. 純文字與選項數和 NpcTemplates.Render 完全相同（面板靠這個對上 HyperTextView）。
    static void Parity(WorldModuleTestContext t){
        t.Check(NpcTemplateMarkup.Available,"NpcTemplates 的模板欄位可讀（反射）");
        var templates=Templates(("shop",Shop),("nested",Nested),("vars",VarsHtml),("blocks",Blocks),("tokens",Tokens));
        string[] args=["甲^乙^丙",""];
        foreach(var key in new[]{"shop","nested","vars","blocks","tokens"}){
            var page=templates.Render(key,args,"商人","勇者");
            var html=NpcTemplateMarkup.Markup(templates,key);
            t.Check(html!=null,$"{key}：讀得到原始 HTML");
            var doc=HyperTextMarkup.Parse(html??"",args,"商人","勇者");
            t.Equal(page.Text,doc.Plain,$"{key}：純文字同 NpcTemplates");
            t.Equal(page.Options.Length,doc.LinkCount,$"{key}：選項數同 NpcTemplates");
        }
        t.Equal((string?)null,NpcTemplateMarkup.Markup(templates,"nope"),"沒有的頁回 null");
    }

    // 2. fg／bg 只收十六進位數字；位元組順序 Rgb（推測）／Bgr；連結常數 0x00B0FFFF。
    static void Colors(WorldModuleTestContext t){
        t.Equal((uint?)0xFFFFAF,HyperTextColors.Parse("ffffaf"),"ffffaf");
        t.Equal((uint?)0x98FB98,HyperTextColors.Parse(" 98FB98 "),"大寫、去空白");
        t.Equal((uint?)0x0FFF00,HyperTextColors.Parse("fff00"),"5 位數照數值（模板有 fg=fff00）");
        t.Equal((uint?)null,HyperTextColors.Parse("#ffffff"),"不收 #");
        t.Equal((uint?)null,HyperTextColors.Parse("0xffffff"),"不收 0x");
        // R10-B（FU11 D7）：空字串＝有效、值 0（黑）；沒有屬性＝null；只有空白仍無效；超過 6 位維持截位（引擎差異，註明）。
        t.Equal((uint?)0,HyperTextColors.Parse(""),"空字串＝0（黑）");
        t.Equal((uint?)null,HyperTextColors.Parse(null),"沒有屬性不改顏色");
        t.Equal((uint?)null,HyperTextColors.Parse("  "),"只有空白無效");
        t.Equal((uint?)0x345678,HyperTextColors.Parse("12345678"),"超過 6 位取低 24 位（引擎差異）");
        t.Equal(Color.FromArgb(255,0x98,0xFB,0x98).ToArgb(),HyperTextColors.Decode(0x98FB98,HyperTextColorOrder.Rgb).ToArgb(),"Rgb：0xRRGGBB（只留作對照）");
        t.Equal(Color.FromArgb(255,0x00,0x00,0xFF).ToArgb(),HyperTextColors.Decode(0xFF0000,HyperTextColorOrder.Bgr).ToArgb(),"Bgr：0x00BBGGRR");
        // R10-B（FU11 D1）：預設 COLORREF。
        t.Equal(HyperTextColorOrder.Bgr,HyperTextColors.Order,"預設 Bgr（COLORREF，FU11 已驗證）");
        t.Equal(Color.FromArgb(255,0xFF,0xFF,0xB0).ToArgb(),HyperTextColors.Link.ToArgb(),"連結色 0x00B0FFFF＝RGB(FF,FF,B0) 淡黃");
        t.Equal(Color.FromArgb(255,0xFF,0xB4,0x00).ToArgb(),HyperTextColors.Decode(0x00B4FF).ToArgb(),"helpcom 的 00b4ff＝橘 RGB(FF,B4,00)");
        t.Equal(Color.FromArgb(255,0xAF,0xFF,0xFF).ToArgb(),HyperTextColors.Decode(0xFFFFAF).ToArgb(),"ffffaf＝淡青藍 RGB(AF,FF,FF)");
        t.Equal(Color.FromArgb(255,0xEE,0xED,0xEE).ToArgb(),HyperTextColors.Default.ToArgb(),"預設字色 RGB(EE,ED,EE)");
        t.Equal(Color.FromArgb(255,0x0A,0xF0,0x32).ToArgb(),HyperTextColors.Hover.ToArgb(),"hover 色 RGB(0A,F0,32)");
        t.Equal(Color.FromArgb(255,0x6E,0x6E,0x46).ToArgb(),HyperTextColors.Underline.ToArgb(),"底線色 RGB(6E,6E,46)");
        t.Equal((uint?)0,Doc("<font fg=\"\">黑</font>").Nodes[0].Style.Fg,"<font fg=\"\"> 設成 0（黑）");
    }

    static HyperTextDocument Doc(string html,params string[] args)=>HyperTextMarkup.Parse(html,args,"商人","勇者");
    static HyperTextNode TextNode(HyperTextDocument d,string text)=>d.Nodes.First(n=>n.Kind==HyperTextNodeKind.Text&&n.Text==text);

    // 3. 樣式：<a> 設連結常數色，<a> 之後的 <font fg> 才蓋過；<b>/<i>/<u>、bg、face、Size。
    static void Styles(WorldModuleTestContext t){
        var d=Doc("<font fg=ff0000>紅<a action=\"a\">連</a><a action=\"b\">前<font fg=00ff00>綠</font>後</a></font><font bg=000080 face=\"細明體\" size=\"12\">底</font><b><i><u>全</u></i></b>平");
        t.Equal((uint?)0xFF0000,TextNode(d,"紅").Style.Fg,"font fg");
        t.Equal((uint?)HyperTextColors.LinkValue,TextNode(d,"連").Style.Fg,"<a> 內＝連結常數（蓋過外層 font）");
        t.Equal((uint?)HyperTextColors.LinkValue,TextNode(d,"前").Style.Fg,"第二個連結開頭同樣是常數");
        t.Equal((uint?)0x00FF00,TextNode(d,"綠").Style.Fg,"<a> 之後的 <font fg> 蓋過連結色");
        t.Equal((uint?)HyperTextColors.LinkValue,TextNode(d,"後").Style.Fg,"</font> 後回連結色");
        var bg=TextNode(d,"底").Style;
        t.Check(bg.Bg==0x000080&&bg.Face=="細明體"&&bg.Size==12,"bg／face／Size 存成資料",bg);
        var all=TextNode(d,"全").Style;t.Check(all.Bold&&all.Italic&&all.Underline,"b／i／u",all);
        var plain=TextNode(d,"平").Style;t.Check(!plain.Bold&&!plain.Italic&&!plain.Underline&&plain.Fg==null,"關閉後還原",plain);
        t.Equal((uint?)null,Doc("<font fg=#ff0000>無</font>").Nodes[0].Style.Fg,"無效 fg 不改顏色");
        // R10-B（FU11 D5）：帶 link／action／url 的 <a> 也有底線；不帶的 <a> 沒有；</a> 後清掉。
        var u=Doc("<a action=\"a\">動</a><a link=\"b\">連</a><a url=\"c\">網</a><a>空</a>外");
        t.Check(TextNode(u,"動").Style.Underline&&TextNode(u,"連").Style.Underline&&TextNode(u,"網").Style.Underline,"帶 action／link／url 的 <a> 有底線");
        t.Check(!TextNode(u,"空").Style.Underline&&!TextNode(u,"外").Style.Underline,"沒屬性的 <a> 與 </a> 之後沒有底線");
    }

    // 4. 選項編號：關閉的 <a> 與帶 link／action 的 <img> 依序 0,1,2…；巢狀 <a> 前一個不成選項；SurfList 換號；pN。
    static void Options(WorldModuleTestContext t){
        var d=Doc("<a action=\"a\">一</a><img src=\"#312\" action=\"buy\"><img src=\"#314\" action=\"sell\"><img src=\"#331\" link=\"next\" tooltip=\"說明\"><img src=\"#1005\"><img src=\"p7\"><a link=\"b\">二</a>");
        t.Equal(5,d.LinkCount,"5 個選項（裝飾圖不算）");
        t.Equal(0,TextNode(d,"一").Link,"第一個 <a>＝0");
        var imgs=d.Nodes.Where(n=>n.Kind==HyperTextNodeKind.Image).ToArray();
        t.Equal("1:1109:購買,2:1107:出售,3:601:原圖示 #331,-1:1005:,-1::",string.Join(",",imgs.Select(n=>$"{n.Link}:{n.Surf}:{n.Label}")),"圖：選項 id、SurfList 換號（312→1109、314→1107、331→601）、標籤");
        t.Equal("說明",imgs[2].Tooltip,"tooltip 存成資料");
        t.Equal((int?)7,imgs[4].Png,"pN＝PngList");
        t.Equal(4,TextNode(d,"二").Link,"最後一個 <a>＝4");
        var n=Doc(Nested);
        t.Equal(1,n.LinkCount,"巢狀 <a>：只有後一個成選項");
        t.Check(TextNode(n,"乙").Link==-1&&TextNode(n,"丙").Link==0&&TextNode(n,"丁").Link==-1,"乙 不可點、丙＝0、丁 在 <a> 外");
    }

    // 5. <var>：第 N 個資料字串、換第一個 ^、textwidth、<var img>。
    static void Vars(WorldModuleTestContext t){
        var d=Doc(VarsHtml,"甲^乙^丙","");
        t.Check(d.Nodes.Any(n=>n.Raw=="甲^乙^丙"&&n.Text=="甲 乙^丙"),"換第一個 ^（Raw 不變，與 NpcTemplates 對得上）");
        t.Check(d.Nodes.Any(n=>n.Text=="〔缺少參數 #5〕")&&d.Nodes.Any(n=>n.Text=="〔不支援的參數〕"),"缺參數、不支援的 src 同 NpcTemplates");
        t.Equal("商人",Doc("<var src=\"#0\">").Nodes[0].Text,"缺 #0 用對話對象名稱");
        var w=Doc("<var src=\"#0\" textwidth=\"60\">|","ab");
        t.Equal(60,w.Nodes[0].MinWidth,"textwidth 存成最小寬");
        var line=HyperTextLayout.Build(w,192,Measure,Images,15)[0];
        t.Equal(60,line.Pieces.Single(p=>p.Text=="|").X,"ab（24 px）補到 60 px，後面的字從 60 開始");
        var img=Doc("<var img src=\"#0\"><var img src=\"#1\">","4000","abc");
        t.Check(img.Nodes[0] is {Kind:HyperTextNodeKind.Image,Surf:4000,Raw:"4000"},"<var img>：數字＝圖號",img.Nodes[0]);
        t.Check(img.Nodes[1] is {Kind:HyperTextNodeKind.Text,Text:"abc"},"<var img>：不是數字照字顯示",img.Nodes[1]);
    }

    // 6. 排版：行內連結、逐字換行、段落與 <br>、置中／靠右、圖的列高、缺圖。
    static void Layout(WorldModuleTestContext t){
        var lines=HyperTextLayout.Build(Doc(Shop),192,Measure,Images,15);
        t.Equal("商人: ||歡迎，勇者。請選擇：購買物品或下|一頁。|||這是特別的粗體斜體底線。",string.Join("|",lines.Select(l=>l.Text)),"列：名稱段、<br> 空列、連結排在文字流裡並跨列、圖列、空列、樣式列");
        var two=lines[2].Pieces.Where(p=>p.Link>=0).Select(p=>$"{p.Link}:{p.Text}@{p.X}");
        t.Equal("0:購買物品@120,1:下@180",string.Join(",",two),"同一列：連結 0 接在「請選擇：」之後、連結 1 從列尾開始");
        t.Equal("1:一頁@0",string.Join(",",lines[3].Pieces.Where(p=>p.Link>=0).Select(p=>$"{p.Link}:{p.Text}@{p.X}")),"連結 1 換到下一列");
        var img=lines[4];
        t.Check(img.Height==30&&img.Align==HyperTextAlign.Center&&img.Pieces.Single() is {X:73,Width:46,Link:2,Image:not null},"圖列：高 30、置中 (192−46)/2＝73、選項 2",img);
        t.Equal(15,lines[0].Height,"一般列高 15");
        var b=HyperTextLayout.Build(Doc(Blocks),192,Measure,_=>null,15);
        t.Equal("中|右||||尾原圖示 #331",string.Join("|",b.Select(l=>l.Text)),"center／p 段落、原始 \\n 不換行、三個 <br>；缺圖的可點圖畫成標籤、缺圖的裝飾圖不畫");
        t.Equal(90,b[0].Pieces[0].X,"置中：(192−12)/2");
        t.Equal(180,b[1].Pieces[0].X,"靠右：192−12");
        var wrap=HyperTextLayout.Build(Doc(new string('字',20)),192,Measure,_=>null,15);
        t.Equal("16,4",string.Join(",",wrap.Select(l=>l.Text.Length)),"逐字換行：192 px＝16 字");
        var space=HyperTextLayout.Build(Doc(new string('字',16)+"  後"),192,Measure,_=>null,15);
        t.Equal("後",space[1].Text,"列首空白收掉");
    }

    // 7. $ 名稱展開後的文字也對得上（宿主先做 ResolveNpcText）。
    static void Resolve(WorldModuleTestContext t){
        var d=Doc(Tokens);
        t.Equal("取得 $1234 一個。確定 $1234",d.Plain,"Plain 保留 $N");
        var r=d.ResolveTo("取得 紅色藥水 一個。確定 紅色藥水");
        t.Check(r!=null&&r.Nodes.Any(n=>n.Link==0&&n.Text=="確定 紅色藥水"),"展開值套到連結字",r?.Nodes);
        t.Check(ReferenceEquals(d,d.ResolveTo(d.Plain)),"文字相同回原文件");
        t.Check(d.ResolveTo("別的字")==null,"對不上回 null");
        t.Check(Doc("沒有記號").ResolveTo("別的字")==null,"沒有 $N 又不相同回 null");
        var docs=new HyperTextDocuments();docs.Register(d);docs.Register(Doc("第二頁"));docs.Register(Doc(Tokens));
        t.Equal(2,docs.Count,"同文字重登只留一份");
        t.Check(docs.Find("取得 紅色藥水 一個。確定 紅色藥水") is {LinkCount:1},"登記表依展開後的文字找回");
        for(int i=0;i<HyperTextDocuments.Capacity+4;i++)docs.Register(Doc("頁"+i));
        t.Equal(HyperTextDocuments.Capacity,docs.Count,"最多留 16 份");
        t.Check(docs.Find(d.Plain)==null,"最舊的被擠掉");
    }

    // ---- 面板 ----
    sealed class Host:IHyperTextHost {
        public List<string> Calls{get;}=[];
        public void Link(HyperTextView shown,int option)=>Calls.Add($"link:{option}");
        public void Submit(HyperTextView shown,long value)=>Calls.Add($"submit:{value}");
        public void CancelNumber(HyperTextView shown)=>Calls.Add("cancel");
        public void Close(HyperTextView shown)=>Calls.Add("close");
    }
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
    static Func<int,UiSurface?> Surfaces=id=>id switch{
        1968=>Surf(id,220,270,0xFF203040),1969=>Surf(id,240,4,0xFF999999),1962=>Surf(id,12,12,0xFFFF00FF),
        1970=>Surf(id,8,220,0xFF111111),2040 or 2042=>Surf(id,8,12,0xFF222222),2044=>Surf(id,8,8,0xFF333333),_=>Images(id)};
    static (HyperTextPanel p,Host h) New(){
        var p=new HyperTextPanel(Measure){Documents=new()};p.Load(UiLayout.Parse(Xml,"synthetic-uiextra86.xml"),Surfaces);var h=new Host();p.Attach(h);return (p,h);
    }
    static UiMouseEvent M(UiMouseKind kind,Point at,int delta=0)=>new(kind,at,kind==UiMouseKind.Wheel?MouseButtons.None:MouseButtons.Left,1,delta,Keys.None);
    static void Click(IUiPanel p,Point at,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,at),cmd);p.Mouse(M(UiMouseKind.Up,at),cmd);}
    static Point Mid(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static HyperTextView View(HyperTextDocument doc,int rev,HyperTextLink[] links,bool attach=true)=>new(HyperTextSource.Npc,rev,doc.Plain,links,"",null,null,attach?doc:null);
    static HyperTextLink[] ShopLinks(bool nextEnabled=true)=>[new(0,"購買物品",true),new(1,"下一頁",nextEnabled),new(2,"購買",true)];

    // 8. Rich 列：連結不另起一列；點字或圖的連結；停用的不可點；畫出圖與底色。
    static void PanelRich(WorldModuleTestContext t){
        var cmd=new UiNullCommands();var (p,h)=New();var doc=Doc(Shop);
        p.Show(View(doc,1,ShopLinks(nextEnabled:false)));
        t.Check(p.Document!=null,"文件對上（選項 0..2）");
        t.Equal("Rich,Rich,Rich,Rich,Rich,Spacer,Rich,Rich",string.Join(",",p.Rows.Select(r=>r.Kind)),"7 列本文；30 px 的圖列後接一個 Spacer；沒有 Link 列");
        t.Check(p.TextArea==new Rectangle(121,94,192,240),"本文區 (21,44) 起 192×240",p.TextArea);
        var buy=p.LinkRect(0);
        t.Check(buy==new Rectangle(121+120,94+30,48,15),"連結 0 在第 3 列 x=120",buy);
        Click(p,Mid(buy),cmd);
        t.Equal("link:0",string.Join(",",h.Calls),"點行內連結 → Link(0)");
        h.Calls.Clear();Click(p,Mid(p.LinkRect(1)),cmd);
        t.Equal(0,h.Calls.Count,"停用的連結不可點");
        var img=p.LinkRect(2);
        t.Check(img==new Rectangle(121+73,94+60,46,30),"圖連結：第 5 列置中",img);
        Click(p,Mid(img),cmd);
        t.Equal("link:2",string.Join(",",h.Calls),"點可點的圖 → Link(2)");
        h.Calls.Clear();Click(p,new Point(121+5,94+60+5),cmd);
        t.Equal(0,h.Calls.Count,"圖列的空白處不是連結");
        using var canvas=new UiCanvas();p.Paint(canvas);
        t.Equal(Color.FromArgb(unchecked((int)BuyColor)).ToArgb(),canvas.Bitmap.GetPixel(img.X+10,img.Y+10).ToArgb(),"圖畫在排好的位置");
        t.Equal(Color.FromArgb(255,0xFF,0xFF,0xB0).ToArgb(),HyperTextPanel.RichLinkColor.ToArgb(),"行內連結色＝0x00B0FFFF 的 COLORREF 解碼 RGB(FF,FF,B0)");
        t.Equal(ChatPalette.Color(ChatFormat.Yellow).ToArgb(),HyperTextPanel.LinkColor.ToArgb(),"舊排法的連結色不變");
        t.Equal(ChatPalette.Color(ChatFormat.White).ToArgb(),HyperTextPanel.TextColor.ToArgb(),"舊排法的本文色不變");
        // R10-B（FU11 D5）：連結（action）底線固定 RGB(6E,6E,46)，畫在列底上 2 px。
        t.Equal(Color.FromArgb(255,0x6E,0x6E,0x46).ToArgb(),canvas.Bitmap.GetPixel(buy.X+5,buy.Y+15-2).ToArgb(),"連結底線色固定 RGB(6E,6E,46)");
        // 底色（R10-B：bg 0000ff 是 COLORREF → RGB(FF,00,00)）
        var (bp,_)=New();var bgDoc=Doc("<font bg=0000ff><var src=\"#0\" textwidth=\"60\"></font>","字");
        bp.Show(View(bgDoc,2,[]));
        using var c2=new UiCanvas();bp.Paint(c2);
        t.Equal(Color.FromArgb(255,255,0,0).ToArgb(),c2.Bitmap.GetPixel(121+50,94+7).ToArgb(),"bg 畫在字（含 textwidth 補白）的範圍底下（COLORREF 解讀）");
        // R10-B（FU11 D6）：同列有 40 px 的圖時，bg 占整列高。
        var (tp,_)=New();var tallDoc=Doc("<font bg=00ff00>字</font><var img src=\"#0\">","4000");
        tp.Show(View(tallDoc,3,[]));
        t.Equal(40,tp.Lines[0].Height,"有圖的列高 40");
        using var c3=new UiCanvas();tp.Paint(c3);
        t.Equal(Color.FromArgb(255,0,255,0).ToArgb(),c3.Bitmap.GetPixel(121+6,94+30).ToArgb(),"bg 用整列高（列底 30 px 處仍有底色）");
        t.Equal(Color.FromArgb(unchecked((int)TallColor)).ToArgb(),c3.Bitmap.GetPixel(121+12+5,94+30).ToArgb(),"圖照畫在字後面");
    }

    // 8b. R10-B（FU11 D2–D4）：預設字色、hover 色（只對文字連結）、停用連結維持灰（引擎自訂）。
    static void PanelHover(WorldModuleTestContext t){
        var cmd=new UiNullCommands();var (p,_)=New();
        p.Show(View(Doc(Shop),1,ShopLinks(nextEnabled:false)));
        HyperTextPiece Piece(int link)=>p.Lines.SelectMany(l=>l.Pieces).First(x=>x.Link==link);
        HyperTextPiece Plain(string text)=>p.Lines.SelectMany(l=>l.Pieces).First(x=>x.Text==text);
        int Argb(Color c)=>c.ToArgb();
        var link=Color.FromArgb(255,0xFF,0xFF,0xB0);var hover=Color.FromArgb(255,0x0A,0xF0,0x32);
        t.Equal(Argb(Color.FromArgb(255,0xEE,0xED,0xEE)),Argb(p.PieceColor(Plain("歡迎，"))),"沒有 fg 的字＝RGB(EE,ED,EE)");
        t.Equal(Argb(HyperTextPanel.RichTextColor),Argb(Color.FromArgb(255,0xEE,0xED,0xEE)),"RichTextColor＝EEEDEE");
        t.Equal(-1,p.HoverLink,"一開始沒有 hover");
        t.Equal(Argb(link),Argb(p.PieceColor(Piece(0))),"連結 0 平時淡黃");
        p.Mouse(M(UiMouseKind.Move,Mid(p.LinkRect(0))),cmd);
        t.Equal(0,p.HoverLink,"滑鼠移到連結 0 的字上");
        t.Equal(Argb(hover),Argb(p.PieceColor(Piece(0))),"hover 中的文字連結＝RGB(0A,F0,32)");
        t.Equal(Argb(HyperTextPanel.DisabledColor),Argb(p.PieceColor(Piece(1))),"停用的連結維持灰（引擎自訂）");
        p.Mouse(M(UiMouseKind.Move,Mid(p.LinkRect(1))),cmd);
        t.Equal(-1,p.HoverLink,"停用的連結不 hover（引擎自訂）");
        t.Equal(Argb(link),Argb(p.PieceColor(Piece(0))),"移開後恢復淡黃");
        p.Mouse(M(UiMouseKind.Move,Mid(p.LinkRect(2))),cmd);
        t.Equal(-1,p.HoverLink,"圖片連結不 hover（FU11§1 #8）");
        p.Mouse(M(UiMouseKind.Move,Mid(p.LinkRect(0))),cmd);p.Mouse(M(UiMouseKind.Move,new Point(5,5)),cmd);
        t.Equal(-1,p.HoverLink,"移出本文區清成 −1");
        // 跨列連結：滑在後半段，前半段也一起變色（同一個選項 id）。
        var (q,_)=New();q.Show(View(Doc(Shop),2,ShopLinks()));
        var halves=q.Lines.SelectMany(l=>l.Pieces).Where(x=>x.Link==1).ToArray();
        t.Equal(2,halves.Length,"連結 1 跨兩列");
        q.Mouse(M(UiMouseKind.Move,new Point(121+6,94+45+7)),cmd);
        t.Equal(1,q.HoverLink,"滑在第二列的「一頁」");
        t.Check(halves.All(x=>Argb(q.PieceColor(x))==Argb(hover)),"整個連結（兩段）都畫 hover 色");
        // <a> 內再開 <font fg> 時 hover 仍蓋過（原版 hover 判斷在 attr fg 之前）。
        var (f,_)=New();var fd=Doc("<a action=\"a\">前<font fg=0000ff>藍</font></a>");f.Show(View(fd,3,[new(0,"前藍",true)]));
        var blue=f.Lines[0].Pieces.First(x=>x.Text=="藍");
        t.Equal(Argb(Color.FromArgb(255,0xFF,0,0)),Argb(f.PieceColor(blue)),"<a> 內 <font fg=0000ff>＝COLORREF 紅");
        f.Mouse(M(UiMouseKind.Move,Mid(f.LinkRect(0))),cmd);
        t.Equal(Argb(hover),Argb(f.PieceColor(blue)),"hover 蓋過 <a> 內的 font fg");
        f.Show(View(fd,4,[new(0,"前藍",true)]));
        t.Equal(-1,f.HoverLink,"換頁清掉 hover");
    }

    // 9. 對不上就退回舊排法；登記表找回文件。
    static void PanelFallback(WorldModuleTestContext t){
        var (p,_)=New();var doc=Doc(Shop);
        p.Show(View(doc,1,[new(0,"購買物品",true),new(1,"下一頁",true)]));
        t.Check(p.Document==null&&p.Rows.Any(r=>r.Kind==HyperTextPanel.RowKind.Link),"選項數不同 → 舊排法（Link 列）");
        p.Show(View(doc,2,[new(0,"a",true),new(1,"b",true),new(5,"c",true)]));
        t.Check(p.Document==null,"選項 id 不是 0..n−1 → 舊排法");
        p.Show(new HyperTextView(HyperTextSource.Npc,3,"別的字",ShopLinks(),"",null,null,doc));
        t.Check(p.Document==null,"文字對不上 → 舊排法");
        p.Show(View(doc,4,ShopLinks(),attach:false));
        t.Check(p.Document==null,"沒有帶文件、登記表也沒有 → 舊排法");
        p.Documents.Register(doc);
        p.Show(View(doc,5,ShopLinks(),attach:false));
        t.Check(p.Document!=null&&p.Rows[0].Kind==HyperTextPanel.RowKind.Rich,"登記表依文字找回 → Rich 列");
        p.Show(new HyperTextView(HyperTextSource.Npc,6,doc.Plain,ShopLinks(),"提示",new(HyperTextNumberKind.Amount,1,1,10,1,true,true,"1～10"),null,doc));
        t.Equal("Rich,Text,Range,Number,Text,Notice",string.Join(",",p.Rows.Skip(7).Select(r=>r.Kind)),"數字框與提示照舊接在本文（最後一個 Rich 列）之後");
    }

    // 10. 高列捲動：圖從上方延伸下來時仍畫、仍可點（裁在本文區內）。
    static void PanelScroll(WorldModuleTestContext t){
        var cmd=new UiNullCommands();var (p,h)=New();
        var doc=Doc("<a action=\"t\"><var img src=\"#0\"></a><br>"+string.Join("<br>",Enumerable.Range(1,20).Select(i=>"列"+i)),"4000");
        p.Show(View(doc,1,[new(0,"圖",true)]));
        t.Equal("Rich,Spacer,Spacer,Rich",string.Join(",",p.Rows.Take(4).Select(r=>r.Kind)),"40 px 圖列占 3 格");
        t.Equal(23-16,p.MaxScroll,"捲動範圍以格計");
        p.Mouse(M(UiMouseKind.Wheel,new Point(150,150),-120),cmd);
        t.Equal(1,p.Scroll,"滾輪下捲 1 格");
        var r=p.LinkRect(0);
        t.Check(r==new Rectangle(121,94,40,25),"捲過 15 px 後圖只剩 25 px 在本文區內",r);
        Click(p,Mid(r),cmd);
        t.Equal("link:0",string.Join(",",h.Calls),"露出的部分仍可點");
        h.Calls.Clear();Click(p,new Point(121+5,94-5),cmd);
        t.Equal(0,h.Calls.Count,"本文區外（被裁掉的部分）不可點");
        p.Mouse(M(UiMouseKind.Wheel,new Point(150,150),-120),cmd);p.Mouse(M(UiMouseKind.Wheel,new Point(150,150),-120),cmd);
        t.Equal(Rectangle.Empty,p.LinkRect(0),"整張捲出去後不在畫面上");
    }

    // 11. WorldSession.RenderHyperText 登記文件：純文字與選項數都對上才登記；沒有模板不登記。
    static void Session(WorldModuleTestContext t){
        using var c=new LoginConnection();var w=c.World;
        w.DialogTemplates=Templates(("shop",Shop));var docs=new HyperTextDocuments();w.HyperTextDocuments=docs;
        var render=typeof(WorldSession).GetMethod("RenderHyperText",F);
        t.Check(render!=null,"RenderHyperText 存在");
        if(render==null)return;
        var page=(NpcPage)render.Invoke(w,["shop",Array.Empty<string>(),"商人",null])!;
        t.Check(w.LastHyperTextDocument is {} d&&d.Plain==page.Text&&d.LinkCount==page.Options.Length,"登記的文件與頁一致");
        t.Equal(1,docs.Count,"登記一份");
        t.Check(docs.Find(page.Text)!=null,"面板可依頁文字找回");
        render.Invoke(w,["missing",Array.Empty<string>(),"商人",new[]{"退回文字"}]);
        t.Check(w.LastHyperTextDocument==null&&docs.Count==1,"沒有模板（退回文字）不登記");
    }
}
