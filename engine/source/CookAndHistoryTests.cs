using System.Reflection;
using P06.NativeUi;
namespace P06;

// R3-C（T279／#672）離線測試：COOK_BOOK_WINDOW 原生面板、料理窗資料來源轉接、血盟倉庫紀錄的事件收件匣與暫行清單。
// 合成封包、合成 XML／圖；不開遊戲、網路，也不讀客戶端素材。
static class CookAndHistoryTests {
    static byte[] Cook(byte open,byte value)=>LoginPackets.Pad([250,52,0xdb,0x31,0xdf,0x02,open,value]);
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] Record(string who,byte type,string item,uint count,uint minutes)=>[..Str(who),type,..Str(item),..Le32(count),..Le32(minutes)];
    static byte[] History(params byte[][] records)=>LoginPackets.Pad([250,117,..Le32((uint)records.Length),..records.SelectMany(e=>e)]);
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static UiMouseEvent M(UiMouseKind kind,int x,int y)=>new(kind,new(x,y),kind==UiMouseKind.Move?MouseButtons.None:MouseButtons.Left,1,0,Keys.None);
    static void Click(IUiPanel p,int x,int y,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,x,y),cmd);p.Mouse(M(UiMouseKind.Up,x,y),cmd);}

    [WorldModuleTest("cook-and-history")]
    static void Run(WorldModuleTestContext t){
        Registration(t);Panel(t);Source(t);Inbox(t);Rows(t);
    }

    static void Registration(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Errors.Count==0,"panel auto-discovery has no errors",UiPanelRegistry.Errors);
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id==CookBookPanel.PanelId&&p.Layout=="uiextra86")==1,"COOK_BOOK_WINDOW registered once on uiextra86");
        t.Check(UiPanelRegistry.Panels.Single(p=>p.Id==CookBookPanel.PanelId).Create(new UiRoot()) is CookBookPanel,"registration creates CookBookPanel");
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        t.Check(typeof(ClientForm).GetMethod("OnClientSizeChanged",F)!=null,"ClientForm hook for cook/history UI declared");
        t.Check(typeof(ClientForm).GetMethod("AttachNativeCookBook",F)!=null&&typeof(ClientForm).GetMethod("SyncCookAndHistory",F)!=null,"ClientForm wiring methods present");
        t.Check(CookingProtocol.LevelSurfLast-CookingProtocol.LevelSurfFirst+1==CookBookPanel.LevelImages,"11 level images match Cooking constants");
    }

    // 與 uiextra86 COOK_BOOK_WINDOW 相同的元件種類與名稱，數字刻意不同（不複製遊戲資源）。
    static string CookXml(int levels=11,string extra="")=>$"""
<?xml version="1.0"?>
<root>
<COOK_BOOK_WINDOW x="10" y="20">
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_COOK_BOOK_BASE" surfNumber="100" xPos="6" yPos="8"/>
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_COOK_BOOK_FRAME" surfNumber="101"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_QUIT_X" surfNumber="110" xPos="90" yPos="10"/>
	<IMAGE surfName="SURF_RENEWAL_WIN_SCROLLBAR" surfNumber="120" xPos="80" yPos="30"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber="121" xPos="80" yPos="70"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber="122" xPos="80" yPos="22"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber="123"/>
{string.Concat(Enumerable.Range(1,levels).Select(i=>$"\t<IMAGE surfName=\"SURF_RENEWAL_IMG_COOK_BOOK_{i}\" surfNumber=\"{200+i}\" xPos=\"50\" yPos=\"80\"/>\n"))}	<IMAGE surfName="SURF_RENEWAL_IMG_COOK_BOOK_GUAGE" surfNumber="130" xPos="12" yPos="76"/>
{extra}</COOK_BOOK_WINDOW>
<DRAGON_GATE_WINDOW x="0" y="0"/>
</root>
""";
    static Func<int,UiSurface?> CookSurfaces(bool missingLevel=false)=>id=>id switch{
        100=>Surf(100,96,90,0xFF203040),101=>Surf(101,120,120,0xFF999999),110=>Surf(110,8,8,0xFF010101),
        120=>Surf(120,8,40,0xFF111111),121 or 122=>Surf(id,8,8,0xFF222222),123=>Surf(123,8,8,0xFF333333),
        130=>Surf(130,30,6,0xFF445566),
        >200 and <=211=>missingLevel&&id==203?null:Surf(id,20,10,0xFF000000|(uint)((id-200)*0x10)),
        _=>null};

    sealed class FakeSource:ICookBookSource {
        public CookBookView? Current{get;set;}
        public List<long> Closed{get;}=[];
        public void Close(long revision){Closed.Add(revision);Current=null;}
    }

    static void Panel(WorldModuleTestContext t){
        var layout=UiLayout.Parse(CookXml(),"synthetic-uiextra86.xml");
        var p=new CookBookPanel();p.Load(layout,CookSurfaces());
        t.Equal(new Point(10,20),p.Origin,"origin = XML x/y");
        // Drawn union: base (6,8) 96×90 → x 6..102, y 8..98; close X (90,10) 8×8 within; frame/knob have no coordinates.
        t.Equal(new Rectangle(10+6,20+8,96,90),p.Bounds,"bounds = drawn elements");
        t.Equal(new Point(90,10),p.CloseButton!.Offset,"close X offset from XML");
        t.Equal(new Point(80,30),p.ScrollBar!.Offset,"scrollbar offset from XML");
        t.Equal(new Point(12,76),p.Gauge!.Offset,"gauge offset from XML");
        t.Equal(11,p.Levels.Count,"11 level images");
        t.Check(p.Levels.Select(l=>l.Surf).SequenceEqual(Enumerable.Range(201,11)),"level images in XML order 1..11");
        t.Check(!p.Visible,"hidden without source");
        t.Throws<UiLayoutException>(()=>new CookBookPanel().Load(UiLayout.Parse(CookXml(levels:10),"a.xml"),CookSurfaces()),"missing 11th level image throws");
        t.Throws<UiLayoutException>(()=>new CookBookPanel().Load(UiLayout.Parse(CookXml(extra:"\t<IMAGE surfName=\"SURF_RENEWAL_IMG_COOK_BOOK_12\" surfNumber=\"212\" xPos=\"1\" yPos=\"1\"/>\n"),"b.xml"),CookSurfaces()),"12th level image throws");
        t.Throws<UiLayoutException>(()=>new CookBookPanel().Load(UiLayout.Parse(CookXml().Replace(" xPos=\"80\" yPos=\"30\"",""),"c.xml"),CookSurfaces()),"scrollbar without coordinates throws");
        t.Throws<UiLayoutException>(()=>new CookBookPanel().Load(UiLayout.Parse("<root/>","d.xml"),CookSurfaces()),"missing COOK_BOOK_WINDOW throws");

        var src=new FakeSource();p.Source=src;var cmd=new UiNullCommands();
        src.Current=new(5,2,3);
        p.Update(new UiStaticGameState(InWorld:false));t.Check(!p.Visible,"hidden outside the world");
        p.Update(new UiStaticGameState(InWorld:true));t.Check(p.Visible,"visible in world with open cook book");
        t.Equal((int?)203,p.LevelImage?.Surf,"value 2 → level image 3 (inferred)");

        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            var basePx=canvas.Bitmap.GetPixel(10+6+1,20+8+1);
            t.Check(basePx.R==0x20&&basePx.G==0x30&&basePx.B==0x40,"base painted at origin + offset",basePx);
            var level=canvas.Bitmap.GetPixel(10+50+1,20+80+1);
            t.Equal(Color.FromArgb(unchecked((int)(0xFF000000|3*0x10))).ToArgb(),level.ToArgb(),"only the selected level image painted on top");
            var gauge=canvas.Bitmap.GetPixel(10+12+1,20+76+1);
            t.Check(gauge.R==0x44&&gauge.G==0x55&&gauge.B==0x66,"gauge painted",gauge);
            t.Equal(Color.Black.ToArgb(),canvas.Bitmap.GetPixel(10+5,20+7).ToArgb(),"frame without coordinates not painted");
        }

        src.Current=new(6,10,11);p.Update(new UiStaticGameState(InWorld:true));t.Equal((int?)211,p.LevelImage?.Surf,"value 10 → last level image");
        src.Current=new(7,11,12);p.Update(new UiStaticGameState(InWorld:true));t.Check(p.Visible&&p.LevelImage==null,"value beyond 11 images: window open, no level image");

        // Interaction: clicks on the body are captured; only the close X closes.
        t.Check(p.Mouse(M(UiMouseKind.Down,10+20,20+20),cmd),"click on body captured");
        p.Mouse(M(UiMouseKind.Up,10+20,20+20),cmd);t.Check(p.Visible&&src.Closed.Count==0,"body click does not close");
        Click(p,10+80+2,20+22+2,cmd);Click(p,10+80+2,20+70+2,cmd);t.Check(p.Visible&&src.Closed.Count==0,"up/down buttons do nothing (no recipe list yet)");
        t.Check(!p.Mouse(M(UiMouseKind.Down,500,500),cmd),"click outside not captured");
        p.Mouse(M(UiMouseKind.Up,500,500),cmd);
        p.Mouse(M(UiMouseKind.Down,10+90+2,20+10+2),cmd);p.Mouse(M(UiMouseKind.Up,10+300,20+300),cmd);
        t.Check(p.Visible,"release outside close X does not close");
        Click(p,10+90+2,20+10+2,cmd);
        t.Check(!p.Visible&&src.Closed.SequenceEqual([7L]),"close X closes this revision",src.Closed);
        t.Check(!p.Key(new(Keys.Escape,true,false),cmd),"keys not handled (Esc unverified)");
        src.Current=new(7,11,12);p.Update(new UiStaticGameState(InWorld:true));t.Check(!p.Visible,"same revision stays closed");
        src.Current=new(9,0,1);p.Update(new UiStaticGameState(InWorld:true));t.Check(p.Visible,"new S250 52 reopens");

        var m=new CookBookPanel();m.Load(layout,CookSurfaces(missingLevel:true));m.Source=new FakeSource{Current=new(1,2,3)};m.Update(new UiStaticGameState(InWorld:true));
        t.Check(m.Visible&&m.LevelImage is {Surface:null},"missing level surf: element kept, not drawn");
        using(var canvas=new UiCanvas()){canvas.Clear(Color.Black);canvas.Restrict(m.Bounds);m.Paint(canvas);canvas.Release();
            t.Check(canvas.Bitmap.GetPixel(10+50+1,20+80+1).R==0x20,"missing level surf leaves base visible");}
    }

    static void Source(WorldModuleTestContext t){
        var s=new WorldSession();var src=new WorldCookBookSource(()=>s);
        t.Check(src.Current==null,"closed cook book → no view");
        s.Receive(Cook(1,4),"");
        var v=src.Current;
        t.Check(v is {Value:4,Level:5},"S250 52 → view value/level",v);
        s.Receive(Cook(1,6),"");
        src.Close(v!.Revision);
        t.Check(s.CookBook.Open&&s.CookBook.Level==7,"stale close does not close a newer window",s.CookBook);
        src.Close(src.Current!.Revision);
        t.Check(!s.CookBook.Open&&src.Current==null,"close current revision closes",s.CookBook);
        t.Check(!s.CloseCookBook(s.CookBook.Revision),"revision close on closed window is no-op");
        s.Receive(Cook(1,1),"");s.Reset();t.Check(src.Current==null,"Reset → no view");

        // Panel + real source end to end.
        var p=new CookBookPanel();p.Load(UiLayout.Parse(CookXml(),"e.xml"),CookSurfaces());p.Source=src;var cmd=new UiNullCommands();
        s.Receive(Cook(1,0),"");p.Update(new UiStaticGameState(InWorld:true));
        t.Check(p.Visible&&p.LevelImage?.Surf==201,"session S250 52 opens native panel");
        Click(p,10+90+2,20+10+2,cmd);
        t.Check(!s.CookBook.Open,"close X closes the session cook book (no packet)");
        p.Update(new UiStaticGameState(InWorld:true));t.Check(!p.Visible,"stays closed");
        s.Receive(Cook(1,0),"");p.Update(new UiStaticGameState(InWorld:true));t.Check(p.Visible,"same value reopens with new revision");
    }

    static void Inbox(WorldModuleTestContext t){
        var s=new WorldSession();using var inbox=new ClanWarehouseHistoryInbox(s);
        t.Check(inbox.Take()==null,"empty inbox");
        s.Receive(History(Record("A",0,"x",1,2)),"");
        s.Receive(History(Record("B",1,"y",3,4),Record("C",0,"z",5,6)),"");
        t.Equal(2,inbox.Received,"two non-empty lists received");
        var h=inbox.Take();
        t.Check(h is {Records.Count:2}&&h.Records[0].CharName=="B","take returns the latest list",h);
        t.Check(inbox.Take()==null,"take empties inbox");
        s.Receive(History(),"");
        t.Check(inbox.Take()==null&&inbox.Received==2,"0-record list (message 1889) does not reach the list window");
        s.Receive(History(Record("D",0,"w",1,1)),"");inbox.Clear();
        t.Check(inbox.Take()==null,"Clear drops pending list");
        inbox.Dispose();
        s.Receive(History(Record("E",0,"v",1,1)),"");
        t.Check(inbox.Take()==null&&inbox.Received==3,"disposed inbox unsubscribed");
    }

    static void Rows(WorldModuleTestContext t){
        var h=new ClanWarehouseHistory([new("盟主甲",0,"$4",50,3),new("Bob",1,"屠龍劍",-1,int.MinValue)]);
        var rows=ClanWarehouseHistoryView.Rows(h,n=>n=="$4"?"金幣":n);
        t.Equal(2,rows.Count,"two rows");
        t.Equal(new ClanWarehouseHistoryRow("盟主甲","類型 0","金幣","50","3 分鐘前"),rows[0],"row 0 text (name code resolved)");
        t.Equal(new ClanWarehouseHistoryRow("Bob","類型 1","屠龍劍","-1",$"{int.MinValue} 分鐘前"),rows[1],"row 1 raw values kept");
        t.Equal("$4",ClanWarehouseHistoryView.Rows(h)[0].ItemName,"no resolver keeps name code");
        t.Equal(5,ClanWarehouseHistoryView.Columns.Length,"five columns");
        using var w=new ClanWarehouseHistoryWindow();
        w.SetRows(rows);
        t.Equal(2,w.RowCount,"window lists two rows");
        t.Equal("金幣",w.Cell(0,2),"window item cell");
        t.Equal("3 分鐘前",w.Cell(0,4),"window minutes cell");
        t.Check(w.Text.Contains("2 筆"),"title shows record count",w.Text);
        w.SetRows([]);t.Equal(0,w.RowCount,"SetRows replaces the whole list");
    }
}
