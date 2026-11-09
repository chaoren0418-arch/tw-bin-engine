using System.Reflection;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// T288（#689）Q3 補做的離線測試（FU4§2、§4、§5、§7）：血盟倉庫紀錄的三行文字（字串 1880–1889）與原生窗
// （HYPER_TEXT_WINDOW 版面、寫死的關閉鈕 9183 與標題列、16 行、捲動）、隱身只限同隊、酒醉固定全振幅。
// 只用合成封包、合成字串表、合成 XML／圖；不開遊戲、網路、視窗、伺服器、DB，也不讀客戶端素材。
static class Q3FollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Party=456,Clan=789,Stranger=900;

    [WorldModuleTest("q3-followup")]
    static void Run(WorldModuleTestContext t){
        HistoryText(t);HistoryTable(t);Registration(t);Panel(t);Invisible(t);Liquor(t);
    }

    // ---- 1. 紀錄文字（FU4§5；沒有字串表時用內建原文）----
    static void HistoryText(WorldModuleTestContext t){
        using var noTable=ServerMessageText.UseTable(null);
        var deposit=new ClanWarehouseRecord("甲",0,"$12",3,5);
        var withdraw=new ClanWarehouseRecord("乙",1,"$4",1000,59);
        t.Equal("甲將道具 存放至血盟倉庫中。",ClanWarehouseHistoryText.ActionLine(deposit),"type 0 = deposit (1880 + 1883)");
        t.Equal("乙將金幣 從血盟倉庫領出。",ClanWarehouseHistoryText.ActionLine(withdraw),"type 1 = withdraw, $4 = adena (1881 + 1882)");
        t.Equal("[短劍]x(3)",ClanWarehouseHistoryText.ItemLineText(deposit,n=>n=="$12"?"短劍":n),"1884 item name resolved + count");
        t.Equal("[$12]x(3)",ClanWarehouseHistoryText.ItemLineText(deposit),"no resolver keeps the name code");
        t.Equal("[1000] 金幣",ClanWarehouseHistoryText.ItemLineText(withdraw,_=>"不該用到"),"1885 adena count, name not resolved");
        t.Equal("(動作時間已過：59分)",ClanWarehouseHistoryText.TimeLine(59),"< 60 minutes → 1886");
        t.Equal("(動作時間已過：1小時)",ClanWarehouseHistoryText.TimeLine(60),"60 minutes → 1887 hours");
        t.Equal("(動作時間已過：23小時)",ClanWarehouseHistoryText.TimeLine(24*60-1),"1439 minutes → 23 hours");
        t.Equal("(動作時間已過：1日)",ClanWarehouseHistoryText.TimeLine(24*60),"1440 minutes → 1888 days");
        t.Equal("(動作時間已過：0分)",ClanWarehouseHistoryText.TimeLine(0),"0 minutes");
        t.Equal("目前沒有血盟倉庫使用的記錄。",ClanWarehouseHistoryText.EmptyText,"1889 empty text");
        var lines=ClanWarehouseHistoryText.Lines(new([deposit,withdraw]));
        t.Equal(8,lines.Count,"two records = 2 × (3 text + 1 blank) lines");
        t.Check(lines[3]==""&&lines[7]==""&&lines[4]=="乙將金幣 從血盟倉庫領出。"&&lines[6]=="(動作時間已過：59分)","record layout: 3 lines then a blank",lines);
        t.Equal("x%y",ClanWarehouseHistoryText.CFormat("x%%y"),"%% → %");
        t.Equal("a %",ClanWarehouseHistoryText.CFormat("a %s%"),"missing argument → empty; trailing % kept");
        t.Equal("%x5",ClanWarehouseHistoryText.CFormat("%x%d",5),"unknown % sequence kept, %d formatted");
        t.Equal("名稱%s",ClanWarehouseHistoryText.CFormat("%s","名稱%s"),"arguments are not re-parsed");
        t.Equal("存放",ClanWarehouseHistoryView.TypeText(0),"legacy list type 0");
        t.Equal("領出",ClanWarehouseHistoryView.TypeText(1),"legacy list type 1");
    }

    static ServerStringTable Table(int count,params (int id,string text)[] rows){
        var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(count+"\r\n"+string.Join("\r\n",lines)));
    }

    // ---- 2. 文字從執行期字串表讀（合成表，不是原版內容）----
    static void HistoryTable(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(Table(1890,(1880,"\\f3%s放了 %s"),(1883,"〔存〕"),(1884,"<%s>*%d"),(1886,"%d分前")))){
            var r=new ClanWarehouseRecord("丙",0,"x",2,7);
            t.Equal("丙放了 〔存〕",ClanWarehouseHistoryText.ActionLine(r),"1880／1883 from the table, colour control removed");
            t.Equal("<x>*2",ClanWarehouseHistoryText.ItemLineText(r),"1884 from the table");
            t.Equal("7分前",ClanWarehouseHistoryText.TimeLine(7),"1886 from the table");
            t.Equal("(動作時間已過：2小時)",ClanWarehouseHistoryText.TimeLine(120),"blank 1887 in the table → built-in text");
        }
        using(ServerMessageText.UseTable(Table(1000))){
            t.Equal("目前沒有血盟倉庫使用的記錄。",ClanWarehouseHistoryText.EmptyText,"table shorter than 1889 → built-in text");
        }
    }

    // ---- 3. 原生窗 ----
    static void Registration(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Errors.Count==0,"panel auto-discovery has no errors",UiPanelRegistry.Errors);
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id==ClanWarehouseHistoryPanel.PanelId&&p.Layout=="uiextra86")==1,"clan warehouse history window registered once on uiextra86");
        t.Check(UiPanelRegistry.Panels.Single(p=>p.Id==ClanWarehouseHistoryPanel.PanelId).Create(new UiRoot()) is ClanWarehouseHistoryPanel,"registration creates ClanWarehouseHistoryPanel");
        t.Check(UiPanelRegistry.Panels.Select(p=>p.Id).Distinct().Count()==UiPanelRegistry.Panels.Count,"registry ids stay unique");
        const BindingFlags D=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        t.Check(typeof(ClientForm).GetMethod("SyncCookAndHistory",D)!=null,"ClientForm history wiring present");
    }

    // 元素名稱與座標同本機 uiextra86-c.xml 的 HYPER_TEXT_WINDOW（版面數值，不是圖）；窗原點刻意改成 (100,50) 以驗證位移。
    static string Xml(string extra="")=>$"""
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
{extra}</HYPER_TEXT_WINDOW>
<COOK_BOOK_WINDOW x="0" y="0"/>
</root>
""";
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    // 合成圖：框 1969 只做 240×4 的頂邊（避免蓋住底圖）；with9183＝本機有 9183 的情形。
    static Func<int,UiSurface?> Surfaces(bool with9183=false)=>id=>id switch{
        1968=>Surf(id,220,270,0xFF203040),1969=>Surf(id,240,4,0xFF999999),1962=>Surf(id,12,12,0xFF010101),
        1970=>Surf(id,8,220,0xFF111111),2040 or 2042=>Surf(id,8,12,0xFF222222),2044=>Surf(id,8,8,0xFF333333),
        9183 when with9183=>Surf(id,14,14,0xFFFF0000),_=>null};
    static UiMouseEvent M(UiMouseKind kind,int x,int y,int delta=0)=>new(kind,new(x,y),kind is UiMouseKind.Move or UiMouseKind.Wheel?MouseButtons.None:MouseButtons.Left,1,delta,Keys.None);
    static void Click(IUiPanel p,int x,int y,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,x,y),cmd);p.Mouse(M(UiMouseKind.Up,x,y),cmd);}
    static ClanWarehouseHistoryPage Page(int records){
        using var noTable=ServerMessageText.UseTable(null);
        var h=new ClanWarehouseHistory(Enumerable.Range(0,records).Select(i=>new ClanWarehouseRecord("角色"+i,(byte)(i%2),"$4",i,i)).ToArray());
        return new(ClanWarehouseHistoryText.Lines(h),ClanWarehouseHistoryText.EmptyText);
    }

    static void Panel(WorldModuleTestContext t){
        var layout=UiLayout.Parse(Xml(),"synthetic-uiextra86.xml");
        var p=new ClanWarehouseHistoryPanel();p.Load(layout,Surfaces());var cmd=new UiNullCommands();
        t.Check(p.Id==ClanWarehouseHistoryPanel.PanelId&&p.Layer==UiLayer.Window&&!p.Visible,"window layer, hidden until S250/117");
        t.Equal(new Point(100,50),p.Origin,"origin = XML x/y (推測)");
        t.Equal(Point.Empty,p.Frame!.Offset,"frame 1969 at the window origin (sub_542370)");
        t.Equal(new Point(6,9),p.Base!.Offset,"base 1968 (6,9)");
        t.Check(p.ScrollBar!.Offset==new Point(217,51)&&p.Up!.Offset==new Point(217,37)&&p.Down!.Offset==new Point(217,275),"scrollbar 1970 / up 2042 / down 2040 from XML");
        t.Check(p.CloseButton is {Surf:9183,Surface:null}&&p.CloseButton.Offset==new Point(215,9),"close button hard-coded 9183 at (215,9); missing locally → not drawn");
        t.Equal(new Rectangle(100+215,50+9,12,12),p.CloseRect,"close hit area at (215,9), size of XML 1962 (推測)");
        // 框 (0,0,240,4)、底 (6,9)–(226,279)、下鈕到 287、標題列 (1,1)–(239,20)、關閉 (215,9,12,12)。
        t.Equal(new Rectangle(100,50,240,287),p.Bounds,"bounds = drawn elements + title bar + close area");
        t.Throws<UiLayoutException>(()=>new ClanWarehouseHistoryPanel().Load(UiLayout.Parse("<root/>","a.xml"),Surfaces()),"missing HYPER_TEXT_WINDOW throws");
        t.Throws<UiLayoutException>(()=>new ClanWarehouseHistoryPanel().Load(UiLayout.Parse(Xml().Replace(" xPos = \"217\" yPos = \"51\"",""),"b.xml"),Surfaces()),"scrollbar without coordinates throws");
        t.Throws<UiLayoutException>(()=>new ClanWarehouseHistoryPanel().Load(UiLayout.Parse(Xml("\t<IMAGE surfName = \"SURF_RENEWAL_WIN_SCROLLBAR_LONG\" surfNumber = \"1\" xPos = \"1\" yPos = \"1\"/>\n"),"c.xml"),Surfaces()),"duplicate element throws");

        // 開窗、16 行、行位置。
        p.Show(Page(5));
        t.Check(p.Visible&&p.Scroll==0,"S250/117 opens at line 0");
        t.Equal(20,p.Page!.Lines.Count,"5 records = 20 lines");
        t.Equal(ClanWarehouseHistoryPanel.VisibleLines,p.VisibleText.Count,"16 lines on screen");
        t.Equal(4,p.MaxScroll,"scroll range 0..lines−16");
        t.Equal(new Point(100+21,50+44),p.LineAt(0),"line 0 at (21,44)");
        t.Equal(new Point(100+21,50+44+15*15),p.LineAt(15),"line 15 at (21,44+15·15)");
        t.Equal(Color.FromArgb(255,255,255,255).ToArgb(),ClanWarehouseHistoryPanel.TextColor.ToArgb(),"text colour = palette 14 (white)");

        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            var basePx=canvas.Bitmap.GetPixel(100+7,50+10);
            t.Check(basePx.R==0x20&&basePx.G==0x30&&basePx.B==0x40,"base painted at origin + (6,9)",basePx);
            t.Equal(Color.FromArgb(unchecked((int)0xFF999999)).ToArgb(),canvas.Bitmap.GetPixel(100+1,50+1).ToArgb(),"frame painted at the origin");
            // (218,95) 是第 3 行（空白行，y 139–153）的高度，不會被字蓋到。
            t.Equal(Color.FromArgb(unchecked((int)0xFF111111)).ToArgb(),canvas.Bitmap.GetPixel(100+218,50+95).ToArgb(),"scrollbar painted");
            var closePx=canvas.Bitmap.GetPixel(100+216,50+10);
            t.Check(closePx.R==0x20&&closePx.B==0x40,"missing 9183: nothing drawn over the base at (215,9)",closePx);
            t.Check(canvas.Bitmap.GetPixel(100+226,50+16).ToArgb()==Color.Black.ToArgb(),"XML close X 1962 (225,15) not drawn");
        }
        var with9183=new ClanWarehouseHistoryPanel();with9183.Load(layout,Surfaces(with9183:true));with9183.Show(Page(1));
        t.Equal(new Rectangle(100+215,50+9,14,14),with9183.CloseRect,"with 9183: hit area = its image");
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(with9183.Bounds);with9183.Paint(canvas);canvas.Release();
            t.Equal(Color.Red.ToArgb(),canvas.Bitmap.GetPixel(100+216,50+10).ToArgb(),"with 9183: close button drawn at (215,9)");
        }

        // 捲動：滾輪、上下鈕、範圍。
        t.Check(p.Mouse(M(UiMouseKind.Wheel,100+50,50+100,-120),cmd)&&p.Scroll==1,"wheel down scrolls one line");
        p.Mouse(M(UiMouseKind.Wheel,100+50,50+100,120),cmd);t.Equal(0,p.Scroll,"wheel up scrolls back");
        p.Mouse(M(UiMouseKind.Wheel,100+50,50+100,120),cmd);t.Equal(0,p.Scroll,"not below 0");
        t.Check(!p.Mouse(M(UiMouseKind.Wheel,700,500,-120),cmd),"wheel outside not taken");
        for(int i=0;i<10;i++)Click(p,100+220,50+280,cmd);
        t.Equal(4,p.Scroll,"down button stops at lines−16");
        t.Equal(p.Page!.Lines[4],p.VisibleText[0],"first visible line = scroll position");
        Click(p,100+220,50+40,cmd);t.Equal(3,p.Scroll,"up button scrolls one line");
        p.Mouse(M(UiMouseKind.Down,100+220,50+40),cmd);p.Mouse(M(UiMouseKind.Up,100+100,50+100),cmd);
        t.Equal(3,p.Scroll,"release outside the button does nothing");
        t.Check(p.Mouse(M(UiMouseKind.Down,100+100,50+150),cmd),"click on the body captured");p.Mouse(M(UiMouseKind.Up,100+100,50+150),cmd);
        t.Check(p.Visible,"body click keeps the window");
        t.Check(!p.Mouse(M(UiMouseKind.Down,700,500),cmd),"click outside not captured");p.Mouse(M(UiMouseKind.Up,700,500),cmd);
        t.Check(!p.Key(new UiKeyEvent(Keys.Escape,true,false),cmd),"keys not taken (sub_8CA740 vtable +40 not expanded)");

        // 拖曳（標題列）。
        t.Check(p.IsDragHandle(new(100+100,50+5)),"title bar (1,1)–(239,20) drags");
        t.Check(!p.IsDragHandle(new(100+218,50+12)),"close area does not drag");
        t.Check(!p.IsDragHandle(new(100+100,50+30)),"below the title bar does not drag");
        p.MoveTo(new(300,100));
        t.Check(p.Bounds.Location==new Point(300,100)&&p.LineAt(0)==new Point(321,144)&&p.CloseRect.Location==new Point(515,109),"lines and close follow the move");

        // 關閉鈕、新清單重開、離開世界。
        Click(p,515+5,109+5,cmd);
        t.Check(!p.Visible,"close button closes (local action, no packet)");
        p.Show(Page(2));t.Check(p.Visible&&p.Scroll==0&&p.MaxScroll==0,"new S250/117 reopens at line 0; 8 lines fit");
        p.Update(new UiStaticGameState(InWorld:true));t.Check(p.Visible,"stays open in the world");
        p.Update(new UiStaticGameState(InWorld:false));t.Check(!p.Visible,"leaving the world closes");
        p.Show(Page(1));((IUiClosablePanel)p).Close();t.Check(!p.Visible,"stack eviction closes (IUiClosablePanel)");

        // 沒有紀錄：字串 1889 在 (21,73)。
        p.Show(Page(0));
        t.Check(p.Visible&&p.Page!.Lines.Count==0&&p.VisibleText.Count==0&&p.Page.EmptyText=="目前沒有血盟倉庫使用的記錄。","empty page keeps 1889 text");
        using(var canvas=new UiCanvas()){canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();}
        t.Check(true,"empty page paints");
    }

    // ---- 4. 隱身：只有同隊（FU4§4）----
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    // S87（欄位同 AmbienceWiringTests.Appear），帶 flags 與血盟 id。
    static byte[] Appear(uint id,ushort x,byte flags=0,uint clan=0)=>P([87],Le16(x),Le16(32940),Le32(id),Le16((ushort)(id==Me?0:1110)),[0,0,0,0],Le32(0),Le16(0),
        Str(id==Me?"offline":"target"+id),Str(""),[flags],Le32(clan),Str(clan==0?"":"clan"),Str(""),[0,100,0,0,0,255,255]);
    static void Set(object o,string name,object? value){
        for(var t=o.GetType();t!=null;t=t.BaseType){
            if(t.GetProperty(name,F|BindingFlags.DeclaredOnly) is {} p&&p.CanWrite){p.SetValue(o,value);return;}
            if(t.GetField(name,F|BindingFlags.DeclaredOnly) is {} f){f.SetValue(o,value);return;}
            if(t.GetField($"<{name}>k__BackingField",F|BindingFlags.DeclaredOnly) is {} b){b.SetValue(o,value);return;}
        }
        throw new MissingMemberException(o.GetType().Name,name);
    }
    static void Invisible(WorldModuleTestContext t){
        var w=new WorldSession();Set(w,"Entered",true);Set(w,"MapId",(ushort)4);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        w.Receive(Appear(Me,32600,0,55),"offline");
        w.Receive(Appear(Party,32601,2,0),"offline");w.Receive(Appear(Clan,32602,2,55),"offline");w.Receive(Appear(Stranger,32603,2,0),"offline");
        var members=(System.Collections.IDictionary)typeof(WorldSession).GetField("partyMembers",F)!.GetValue(w)!;
        members["target456"]=new PartyMember(Party,"target456",100);
        Draw D(uint id)=>new(0,0,0,0,"actor",0,-1,-1,new Tile(0,[new Pixel(0,0,31)]),id);
        t.Check(w.InvisiblePeerVisible(Party)&&w.InvisibilityOpacity(D(Party),null) is {Half:true,Hidden:false},"party member: 50% blend");
        t.Check(!w.InvisiblePeerVisible(Clan)&&w.InvisibilityOpacity(D(Clan),null) is {Hidden:true},"same clan (55), not in party: hidden (no clan exception)");
        t.Check(w.InvisibilityOpacity(D(Stranger),null) is {Hidden:true},"stranger hidden");
        members["target789"]=new PartyMember(Clan,"target789",100);
        t.Check(w.InvisiblePeerVisible(Clan),"clan member who joins the party becomes visible");
    }

    // ---- 5. 酒醉：產品固定全振幅（16 位元路徑，FU4§2）----
    static void Liquor(WorldModuleTestContext t){
        t.Check(TimedBuffRules.LiquorFullWave(),"product hook = 16-bit path (full amplitude)");
        bool same=true;
        for(int level=1;level<=3;level++)for(int phase=0;phase<200;phase+=13)for(int row=0;row<100;row+=7)
            same&=TimedBuffRules.LiquorOffset(level,phase,row)==TimedBuffRules.ScanlineOffset(level,phase,row);
        t.Check(same,"product offset never halved");
    }
}
