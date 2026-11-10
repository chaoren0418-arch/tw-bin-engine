using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
namespace P06.NativeUi;

// T353 (#816, R7-O) widget-slot 離線測試：WidgetSlot.xml／WidgetSubSlot.xml 外觀（CornerFrame、ButtonLock、ButtonMode）。
// 版面 XML、surf、png、idx／pak 全部是合成資料（記憶體或測試輸出目錄下的暫存檔）；不讀真實客戶端或參考客戶端、不啟動遊戲、
// 不連網路。座標與圖號照 kor25 WidgetSlot.xml 的值（gap-matrix 15.2 第 3 點），但檔案本身不放進 repo。CI 由 --module-tests 自動發現執行。
static class WidgetSlotTests {
    const uint FrameC=0xFF101010,ExC=0xFF202020,MarkC=0xFFFFFF00,TabC=0xFF004000,
        OldLockedC=0xFF400000,OldOpenC=0xFF800000,LockedC=0xFF0000C0,OpenC=0xFF0000FF,CornerC=0xFF00C0C0,
        ModeC=0xFFC000C0,ModePushC=0xFFFF00FF;
    static UiSurface S(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Dictionary<int,UiSurface> Surfs()=>new(){
        [3176]=S(3176,157,124,FrameC),[3226]=S(3226,158,125,ExC),[2331]=S(2331,36,36,MarkC),
        [3193]=S(3193,20,12,TabC),[3194]=S(3194,20,12,TabC),[3196]=S(3196,20,12,TabC),[3197]=S(3197,20,12,TabC),[3199]=S(3199,20,12,TabC),[3200]=S(3200,20,12,TabC),
        [3191]=S(3191,16,30,OldLockedC),[3192]=S(3192,16,30,OldOpenC),
        [3292]=S(3292,24,23,CornerC),[3293]=S(3293,11,16,LockedC),[3294]=S(3294,11,16,OpenC),
    };
    static Dictionary<int,UiSurface> Pngs()=>new(){[3350]=S(3350,14,14,ModeC),[3351]=S(3351,14,14,ModePushC)};

    // Synthetic uistatus86 RIGHT_WINDOW / EX_MACRO_WINDOW (same values as uistatus86-c.xml, see MacroChecks).
    const string Host="""
        <?xml version="1.0"?>
        <MainStatusConfigulation>
          <RIGHT_WINDOW x = "643" y = "476">
            <MACRO_MAIN_WINDOW><MACRO_FRAME surfNumber = "3176" xRPos = "0" yRPos = "0"/></MACRO_MAIN_WINDOW>
            <BUTTON_ONOFF_LIST><ONOFF_BUTTON title = "MACRO_LOCK" action = "8" surfNumber = "3191" xRPos = "-9" yRPos = "37"/></BUTTON_ONOFF_LIST>
            <BUTTON_TAB_LIST>
              <TAB_BUTTON title = "MACRO_SELECT" action = "10" surfNumber = "3193" xRPos = "15" yRPos = "5"/>
              <TAB_BUTTON title = "MACRO_SELECT" action = "11" surfNumber = "3196" xRPos = "37" yRPos = "5"/>
              <TAB_BUTTON title = "MACRO_SELECT" action = "12" surfNumber = "3199" xRPos = "60" yRPos = "5"/>
            </BUTTON_TAB_LIST>
            <ETC>
              <SELECT_MARK surfNumber = "2331" xRPos = "12" yRPos = "19"/>
              <MACRO_ICON surfNumber = "" xRPos = "14" yRPos = "21"/>
            </ETC>
          </RIGHT_WINDOW>
          <EX_MACRO_WINDOW x = "485" y = "475">
            <MAIN_WINDOW><EX_MACRO_FRAME surfNumber = "3226" xRPos = "0" yRPos = "0"/></MAIN_WINDOW>
          </EX_MACRO_WINDOW>
        </MainStatusConfigulation>
        """;
    // Only the elements R7-O uses, with the kor25 values (slots / tabs / tooltips left out).
    static string MainXml(string window="Name=\"Slotwindow\" X=\"643\" Y=\"476\" BaseSurf=\"3176;1\"",string extra="")=>$"""
        <?xml version="1.0"?>
        <WidgetSlot Name="WidgetSlot">
          <Window {window} Activate="1" MostTop="1">
            <Control Name="Shortcuts" X="10" Y="18" Width="153" Height="78"/>
            <Button Name="ButtonLock" X="0" Y="47" BaseSurf="3293;1" PushSurf="3294;1" MouseEvent="LockButton"/>
            <Image Name="CornerFrame" X="0" Y="0" Surf="3292;1"/>
            <Button Name="ButtonMode" X="-6" Y="22" BasePng="3350;1" PushPng="3351;1" MouseEvent="ModeChange"/>
            {extra}
          </Window>
        </WidgetSlot>
        """;
    const string Sub="""
        <?xml version="1.0"?>
        <WidgetSubSlot Name="WidgetSubSlot">
          <Window Name="SubSlotWindow" X="485" Y="475" BaseSurf="3226;1" Activate="1" MostTop="1">
            <Control Name="Shortcuts" X="8" Y="18" Width="166" Height="120"/>
            <Button Name="ButtonLock" X="149" Y="37" BaseSurf="3191;1" PushSurf="3192;1" MouseEvent="LockButton"/>
            <Button Name="ButtonMode" X="152" Y="23" BasePng="3350;1" PushPng="3351;1" MouseEvent="ModeChange"/>
          </Window>
        </WidgetSubSlot>
        """;

    sealed class State(MacroBook book):IUiGameState,IUiMacroState {
        public bool InWorld=>true;public string? CharacterName=>null;public int? Level=>null;
        public long? Hp=>null;public long? MaxHp=>null;public long? Mp=>null;public long? MaxMp=>null;public long Now=>0;
        public MacroBook Macros=>book;public MacroInventory? Inventory=>null;public bool InputAllowed=>true;public Bitmap? ItemIcon(ushort id)=>null;
    }
    static UiLayout L(string xml,string name)=>UiLayout.Parse(xml,name);
    static MacroPanel MainPanel(string? widget,Dictionary<int,UiSurface>? surfs=null,Dictionary<int,UiSurface>? pngs=null){
        var p=new MacroPanel();var s=surfs??Surfs();var g=pngs??Pngs();
        p.Load(L(Host,"uistatus86-c.xml"),id=>s.GetValueOrDefault(id),widget==null?null:L(widget,"WidgetSlot.xml"),id=>g.GetValueOrDefault(id));
        return p;
    }
    static ExMacroPanel SubPanel(string? widget,UiSlotGroup group,Dictionary<int,UiSurface>? pngs=null){
        var p=new ExMacroPanel(group);var s=Surfs();var g=pngs??Pngs();
        p.Load(L(Host,"uistatus86-c.xml"),id=>s.GetValueOrDefault(id),widget==null?null:L(widget,"WidgetSubSlot.xml"),id=>g.GetValueOrDefault(id));
        return p;
    }
    static UiMouseEvent M(UiMouseKind k,int x,int y)=>new(k,new(x,y),MouseButtons.Left,1,0,Keys.None);
    static int Pixel(IUiPanel p,int x,int y){using var c=new UiCanvas();c.Clear(Color.Black);p.Paint(c);return c.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("widget-slot")]
    static void Run(WorldModuleTestContext t){
        Registration(t);
        Classic(t);
        MainWidget(t);
        SubWidget(t);
        Refused(t);
        MissingImages(t);
        ProductPath(t);
    }

    static void Registration(WorldModuleTestContext t){
        foreach(var id in new[]{MacroPanel.WindowId,ExMacroPanel.WindowId}){
            var regs=UiPanelRegistry.Panels.Where(r=>r.Id==id).ToArray();
            t.Check(regs.Length==1&&regs[0].Layout=="uistatus86",$"{id} still registered once on uistatus86 (Build never needs WidgetSlot.xml)",string.Join(",",regs.Select(r=>r.Layout)));
        }
        t.Check(!UiPanelRegistry.Errors.Any(e=>e.Contains(nameof(MacroPanel))),"no discovery error for the macro panels");
        var (layout,png,error)=WidgetSlotLayout.Open(null,WidgetSlotLayout.MainFile);
        t.Check(layout==null&&error==null&&png(3350)==null,"no UiResources → no WidgetSlot.xml, no error");
    }

    // Without WidgetSlot.xml nothing changes (MacroChecks keeps asserting the uistatus86 look).
    static void Classic(WorldModuleTestContext t){
        var p=MainPanel(null);
        t.Check(p.Widget==null&&p.WidgetStatus=="absent","no WidgetSlot.xml → uistatus86 look",p.WidgetStatus);
        t.Equal((3191,3192),p.LockImages,"classic lock images 3191/3192");
        t.Equal(new Rectangle(634,513,16,30),p.LockRect,"classic MACRO_LOCK at (643−9, 476+37)");
        t.Equal(new Rectangle(634,476,166,124),p.Bounds,"classic bounds = frame ∪ lock");
        var book=new MacroBook();p.Update(new State(book));
        t.Equal(Argb(OldOpenC),Pixel(p,636,520),"classic unlocked → 3192");
        t.Equal(Argb(FrameC),Pixel(p,645,478),"classic: no corner frame drawn");
        var ex=SubPanel(null,new UiSlotGroup(UiSlotGroup.Bar485));
        t.Check(ex.Widget==null&&ex.LockRect.IsEmpty&&ex.Bounds==new Rectangle(485,475,158,125),"classic extended box: frame only");
    }

    static void MainWidget(WorldModuleTestContext t){
        var p=MainPanel(MainXml());
        t.Check(p.Widget!=null&&p.WidgetStatus=="WidgetSlot.xml","WidgetSlot.xml applied",p.WidgetStatus);
        var w=p.Widget!;
        t.Check(w.Origin==new Point(643,476)&&w.Missing.Count==0,"window origin (643,476), every image found",string.Join(",",w.Missing));
        t.Equal(new Rectangle(643,476,24,23),w.CornerRect,"CornerFrame 3292 at Window+(0,0)");
        t.Check(w.CornerRef is {Id:3292,IsSurf:true},"CornerFrame is surf 3292");
        t.Equal((3293,3294),p.LockImages,"main lock button 3293 (locked) / 3294 (unlocked)");
        t.Equal(new Rectangle(643,523,11,16),p.LockRect,"ButtonLock at Window+(0,47)");
        t.Check(w.Mode is {Rect:var mr,BaseRef.Id:3350,PushRef.Id:3351,BaseRef.IsSurf:false,MouseEvent:WidgetSlotLayout.ModeChangeEvent}&&mr==new Rectangle(637,498,14,14),"ButtonMode png 3350/3351 at Window+(−6,22)",w.Mode?.Rect);
        t.Equal(new Rectangle(637,476,163,124),p.Bounds,"bounds = frame ∪ lock ∪ corner ∪ mode (the old lock column x<637 is no longer the box)");
        t.Check(p.TabRects.Count==3&&p.SelectOrigin==new Point(655,495),"tabs and select mark stay on uistatus86");

        var book=new MacroBook();var state=new State(book);p.Update(state);var cmd=new UiNullCommands();
        t.Equal(Argb(OpenC),Pixel(p,645,530),"unlocked → PushSurf 3294");
        t.Equal(Argb(CornerC),Pixel(p,660,483),"CornerFrame drawn over tab 1 (XML order: PageButton, ButtonLock, CornerFrame)");
        t.Equal(Argb(ModeC),Pixel(p,640,500),"ButtonMode BasePng 3350");
        t.Equal(Color.Black.ToArgb(),Pixel(p,636,530),"old MACRO_LOCK spot is not drawn by the main box any more");
        t.Check(!p.Mouse(M(UiMouseKind.Down,635,530),cmd),"click at the old lock spot is not taken");
        t.Check(p.Mouse(M(UiMouseKind.Down,645,530),cmd)&&book.Locked,"ButtonLock click → locked");
        t.Equal(Argb(LockedC),Pixel(p,645,530),"locked → BaseSurf 3293");
        p.Mouse(M(UiMouseKind.Down,645,530),cmd);t.Check(!book.Locked,"second click → unlocked");
        int page=book.Page;bool locked=book.Locked;
        t.Check(p.Mouse(M(UiMouseKind.Down,640,500),cmd)&&p.ModePressed,"ButtonMode press is taken");
        t.Equal(Argb(ModePushC),Pixel(p,640,500),"pressed → PushPng 3351");
        t.Check(book.Page==page&&book.Locked==locked,"ModeChange not wired: page and lock unchanged (M30)");
        t.Check(UiResourceLog.Recent().Any(l=>l.Contains(WidgetSlotLayout.ModeChangeEvent)&&l.Contains("M30")),"ModeChange logged as not wired");
        t.Check(p.Mouse(M(UiMouseKind.Up,700,560),cmd)&&!p.ModePressed,"release anywhere ends the press");
        t.Equal(Argb(ModeC),Pixel(p,640,500),"released → BasePng again");
        t.Check(!p.Mouse(M(UiMouseKind.Up,640,500),cmd),"a stray release is not taken");
    }

    static void SubWidget(WorldModuleTestContext t){
        var group=new UiSlotGroup(UiSlotGroup.Bar485);
        var p=SubPanel(Sub,group);
        t.Check(p.Widget!=null&&p.WidgetStatus=="WidgetSubSlot.xml","WidgetSubSlot.xml applied",p.WidgetStatus);
        t.Equal(new Rectangle(634,512,16,30),p.LockRect,"sub ButtonLock 3191/3192 at Window+(149,37)");
        t.Check(p.Widget!.Lock is {BaseRef.Id:3191,PushRef.Id:3192}&&p.Widget.CornerRef==null,"sub lock images, no CornerFrame in WidgetSubSlot");
        t.Check(p.Widget.Mode?.Rect==new Rectangle(637,498,14,14),"sub ButtonMode at Window+(152,23) = same spot as the main one",p.Widget.Mode?.Rect);
        t.Equal(new Rectangle(485,475,166,125),p.Bounds,"bounds = frame ∪ lock ∪ mode");
        var book=new MacroBook();p.Update(new State(book));var cmd=new UiNullCommands();
        t.Check(p.Visible,"only BAR_485 member → shown");
        t.Equal(Argb(OldOpenC),Pixel(p,640,530),"unlocked → 3192 (the same surf the uistatus86 MACRO_LOCK uses)");
        t.Check(p.Mouse(M(UiMouseKind.Down,640,530),cmd)&&book.Locked,"sub lock toggles the shared MacroBook lock (推測)");
        t.Equal(Argb(OldLockedC),Pixel(p,640,530),"locked → 3191");
        t.Check(p.Mouse(M(UiMouseKind.Down,640,500),cmd)&&p.ModePressed&&book.Locked,"sub ButtonMode press: lock unchanged");
        p.Mouse(M(UiMouseKind.Up,640,500),cmd);
        t.Check(!p.ModePressed&&p.SlotAt(new(495,495))==0&&p.Mouse(M(UiMouseKind.Down,495,495),cmd)&&book.SelectionInExtended,"slots still work");
    }

    static void Refused(WorldModuleTestContext t){
        foreach(var (xml,label) in new[]{
            (MainXml("Name=\"Slotwindow\" X=\"600\" Y=\"476\" BaseSurf=\"3176;1\""),"window moved"),
            (MainXml("Name=\"Slotwindow\" X=\"643\" Y=\"476\" BaseSurf=\"9999;1\""),"other BaseSurf"),
            (MainXml("Name=\"Other\" X=\"643\" Y=\"476\" BaseSurf=\"3176;1\""),"other window name"),
            (MainXml(extra:"<Image Name=\"CornerFrame\" Surf=\"3292;1\"/>"),"duplicate CornerFrame"),
            (MainXml(extra:"").Replace("Surf=\"3292;1\"","Surf=\"x\""),"malformed Surf"),
        }){
            var p=MainPanel(xml);
            t.Check(p.Widget==null&&p.WidgetStatus.StartsWith("refused: ",StringComparison.Ordinal)&&p.LockImages==(3191,3192)&&p.LockRect==new Rectangle(634,513,16,30),$"{label} → refused, uistatus86 look kept",p.WidgetStatus);
        }
        var sub=SubPanel(MainXml(),new UiSlotGroup(UiSlotGroup.Bar485));
        t.Check(sub.Widget==null&&sub.WidgetStatus.StartsWith("refused: ",StringComparison.Ordinal),"WidgetSlot window given to the extended box → refused",sub.WidgetStatus);
        var none=MainPanel(MainXml().Replace("<Image Name=\"CornerFrame\" X=\"0\" Y=\"0\" Surf=\"3292;1\"/>","").Replace("<Button Name=\"ButtonMode\"","<Other Name=\"ButtonMode\""));
        t.Check(none.Widget is {CornerRef:null,Mode:null}&&none.LockImages==(3293,3294),"absent CornerFrame / ButtonMode → simply not drawn");
    }

    static void MissingImages(WorldModuleTestContext t){
        var surfs=Surfs();surfs.Remove(3292);surfs.Remove(3293);surfs.Remove(3294);
        var p=MainPanel(MainXml(),surfs,[]);
        var w=p.Widget!;
        t.Check(w.Missing.Count==5&&w.Missing.Any(m=>m.StartsWith("surf 3292",StringComparison.Ordinal))&&w.Missing.Any(m=>m.StartsWith("png 3350",StringComparison.Ordinal)),"missing images listed",string.Join(",",w.Missing));
        t.Check(p.LockRect.IsEmpty&&w.CornerRect.IsEmpty&&w.Mode!.Rect.IsEmpty&&p.Bounds==p.FrameRect,"missing images → no rect, no substitute (the old 3191 lock is not put back)");
        var book=new MacroBook();p.Update(new State(book));
        t.Check(!p.Mouse(M(UiMouseKind.Down,645,530),new UiNullCommands())&&!book.Locked,"no lock image → no lock click");
        t.Equal(Color.Black.ToArgb(),Pixel(p,636,520),"nothing drawn in the lock columns");
        var onlyBase=MainPanel(MainXml(),null,new(){[3350]=Pngs()[3350]});
        onlyBase.Update(new State(new MacroBook()));onlyBase.Mouse(M(UiMouseKind.Down,640,500),new UiNullCommands());
        t.Equal(Argb(ModeC),Pixel(onlyBase,640,500),"PushPng missing → pressed button keeps its BasePng");
    }

    // ---- product path: UiResources with a synthetic main client and a synthetic reference client on disk.
    static byte[] PngBytes(int w,int h,Color c){
        using var bmp=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)bmp.SetPixel(x,y,c);
        using var ms=new MemoryStream();bmp.Save(ms,ImageFormat.Png);return ms.ToArray();
    }
    static (byte[] Idx,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var idx=new MemoryStream();using var pak=new MemoryStream();using var w=new BinaryWriter(idx);
        w.Write((uint)files.Length);
        foreach(var (name,data) in files){
            w.Write((uint)pak.Position);var raw=new byte[20];Encoding.ASCII.GetBytes(name).CopyTo(raw,0);w.Write(raw);w.Write((uint)data.Length);pak.Write(data);
        }
        w.Flush();return (idx.ToArray(),pak.ToArray());
    }
    static string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    // Only the files this test created are removed (never a recursive delete).
    sealed class Scratch:IDisposable {
        public string Dir{get;}
        readonly List<string> files=[];readonly List<string> dirs=[];
        public Scratch(string name){
            string parent=Path.Combine(AppContext.BaseDirectory,"module-tests-tmp");
            Dir=Path.Combine(parent,name+"-"+Environment.ProcessId);
            if(!Directory.Exists(parent)){Directory.CreateDirectory(parent);dirs.Add(parent);}
            if(!Directory.Exists(Dir)){Directory.CreateDirectory(Dir);dirs.Insert(0,Dir);}
        }
        public string Sub(string name){string p=Path.Combine(Dir,name);if(!Directory.Exists(p)){Directory.CreateDirectory(p);dirs.Insert(0,p);}return p;}
        public void Write(string sub,string name,byte[] data){string p=Path.Combine(Sub(sub),name);File.WriteAllBytes(p,data);files.Add(p);}
        public void Dispose(){
            foreach(var f in files)try{File.Delete(f);}catch(IOException){}catch(UnauthorizedAccessException){}
            foreach(var d in dirs)try{if(Directory.Exists(d)&&!Directory.EnumerateFileSystemEntries(d).Any())Directory.Delete(d,false);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }

    static void ProductPath(WorldModuleTestContext t){
        using var s=new Scratch("widget-slot");
        string main=s.Sub("main"),refRoot=s.Sub("ref");
        var (mIdx,mPak)=Archive(("Other-c.xml",Encoding.UTF8.GetBytes("<a/>")));
        s.Write("main","Tile.idx",mIdx);s.Write("main","Tile.pak",mPak);
        var pins=Enumerable.Range(0,17).Select(i=>new ClientArchivePin(RefClientSurfFallback.ArchiveName(i),"0","0",0)).ToArray();
        using var res=new UiResources(main,new ClientArchivePin("Tile",Sha(mIdx),Sha(mPak),mPak.Length),pins);

        var (none,_,noneError)=WidgetSlotLayout.Open(res,WidgetSlotLayout.MainFile);
        t.Check(none==null&&noneError==null,"main client without WidgetSlot.xml and no reference → uistatus86 look, no error");

        // Reference client: WidgetSlot.xml / broken WidgetSubSlot.xml in Tile, 3350.png in its hashed Sprite archive.
        var (rIdx,rPak)=Archive(("WidgetSlot.xml",Encoding.UTF8.GetBytes(MainXml())),("WidgetSubSlot.xml",Encoding.UTF8.GetBytes("<WidgetSubSlot><Window")));
        s.Write("ref","Tile.idx",rIdx);s.Write("ref","Tile.pak",rPak);
        var png=PngBytes(14,14,Color.FromArgb(255,192,0,192));
        string arch=RefClientSurfFallback.ArchiveName(OptionPngSource.Candidates(3350).First(c=>c.Name=="3350.png").Archive);
        var (pIdx,pPak)=Archive(("3350.png",png));
        s.Write("ref",arch+".idx",pIdx);s.Write("ref",arch+".pak",pPak);
        res.Xml.Reference=new RefClientXmlSource(refRoot,"test");

        var (layout,pngs,error)=WidgetSlotLayout.Open(res,WidgetSlotLayout.MainFile);
        t.Check(layout!=null&&error==null&&layout.FileName=="WidgetSlot.xml","WidgetSlot.xml served by the reference Tile.pak",error);
        t.Check(pngs(3350) is {Width:14,Archive:var a}&&a=="ref:"+arch,"ButtonMode 3350.png through OptionPngSource from the reference Sprite archive");
        t.Check(pngs(3351)==null,"3351.png absent → null (not drawn)");
        t.Check(UiResourceLog.Recent().Any(l=>l.StartsWith("option-png 3350 -> ref ",StringComparison.Ordinal)&&l.Contains(Sha(png))),"png payload SHA-256 logged");
        var p=new MacroPanel(res);
        p.Load(L(Host,"uistatus86-c.xml"),id=>Surfs().GetValueOrDefault(id),layout,pngs);
        t.Check(p.Widget?.Mode?.Base!=null&&p.Widget.Missing.Count==1&&p.Widget.Missing[0].StartsWith("png 3351",StringComparison.Ordinal),"panel draws the served png, lists the missing push png",p.Widget==null?p.WidgetStatus:string.Join(",",p.Widget.Missing));

        var (bad,_,badError)=WidgetSlotLayout.Open(res,WidgetSlotLayout.SubFile);
        t.Check(bad==null&&badError!=null,"malformed WidgetSubSlot.xml → refused with a reason",badError);
        var ex=new ExMacroPanel(new UiSlotGroup(UiSlotGroup.Bar485),res);
        ex.Load(L(Host,"uistatus86-c.xml"),id=>Surfs().GetValueOrDefault(id),null,_=>null,badError);
        t.Check(ex.Widget==null&&ex.WidgetStatus.StartsWith("refused: ",StringComparison.Ordinal),"extended box keeps the uistatus86 look",ex.WidgetStatus);
    }
}
