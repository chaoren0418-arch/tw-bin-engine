using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text;
namespace P06.NativeUi;

// T330 (#768) option-panel 離線測試（選項窗）：版面 XML、png、surf、idx／pak 全部是合成資料（記憶體或測試輸出目錄下的暫存檔），
// 不讀真實客戶端或參考客戶端、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class OptionPanelTests {
    const uint BaseC=0xFF202020,FrameC=0xFF00FFFF,TabC=0xFF008000,TabPushC=0xFF00FF00,HelpC=0xFF800080,CloseC=0xFF0000FF,HomeC=0xFFFF0000,
        CheckC=0xFFFFFF00,GameC=0xFFFF8000,ExpandC=0xFF808000,ScrollC=0xFF404080,MoveC=0xFFC0C0C0;
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Dictionary<int,UiSurface> Pngs()=>new(){
        [901]=Surface(901,200,160,BaseC),[902]=Surface(902,4,4,FrameC),[910]=Surface(910,20,10,TabC),[912]=Surface(912,20,10,TabPushC),
        [913]=Surface(913,10,10,HelpC),[920]=Surface(920,20,20,HomeC),[930]=Surface(930,12,12,CheckC),[940]=Surface(940,20,20,GameC),
        [950]=Surface(950,10,10,ExpandC),[960]=Surface(960,100,2,ScrollC),[961]=Surface(961,6,12,MoveC),
    };
    static Dictionary<int,UiSurface> Surfs()=>new(){[801]=Surface(801,10,10,CloseC)};

    // Synthetic layout with the element kinds of OptionUI.xml (not the real file): tabs, Help (with the original's
    // "HighlihgtPng" spelling and a Tooltip), Close surf, pages with Image / CheckButton+Label / nested Control+RadioButton
    // (no Y) / ComboBox+UpDownList / ScrollBar / BoardImage / List.
    static string Xml(string window="Name=\"Option\" X=\"100\" Y=\"50\" BasePng=\"901;1\" FramePng=\"902;1\" Moveable=\"30\"",string shortcut="Width=\"200\" Height=\"160\"")=>$"""
        <?xml version="1.0"?>
        <OptionUI>
          <Window {window} MouseEvent="MouseOptionUIWindow">
            <Button Name="GameBtn" X="10" Y="40" BasePng="910" HighlightPng="911" PushPng="912" MouseEvent="GameBtn"/>
            <Button Name = "Help_Button" X = "10" Y = "140" BasePng = "913;1" HighlihgtPng = "914;1" MouseEvent="HelpBtn">
              <Tooltip Name = "Desc_Tooltip" Y="20" String = "1"/>
            </Button>
            <Button Name="Close" X="180" Y="5" BaseSurf="801;1" PushSurf="802;1" MouseEvent="CloseBtn"/>
            <Control Name="Home" X="0" Y="0" Activate="0" EnableEvent = "HomeEnable">
              <Image X="60" Y="20" Png="920;1"/>
              <Image X="60" Y="60" Png="999;1"/>
              <CheckButton Name = "ShowNpcName" X = "60" Y="100" BasePng = "930" PushPng = "931" Width="80" Height = "12" MouseEvent = "ShowNpcNameBtn">
                <Label Name = "Text_Label" X = "18" Y="1" Desc="18806" FontColor="171,163,131"/>
              </CheckButton>
            </Control>
            <Control Name="Game" X="0" Y="0" Activate="0" EnableEvent = "GameEnable">
              <Image X="60" Y="20" Png="940;1"/>
              <Control Name = "Radio_Control" X = "70" Y = "70">
                <RadioButton X = "20" BasePng = "930" PushPng = "931" MouseEvent = "RadioBtn" Tag="1"/>
              </Control>
              <ComboBox Name = "Mode_Combobox" X = "60" Y = "110">
                <Label Name = "Text_Label" Align = "center" X = "0" Y = "0" Width = "50" Height = "18"/>
                <Button Name = "Expand_Button" X = "0" Y = "0" BasePng = "950" MouseEvent = "ComboDown"/>
                <UpDownList Name = "Expand_List" X = "0" Y = "18" Width = "50" Height = "40"/>
              </ComboBox>
              <ScrollBar Name="VolumeScrollBar" X="60" Y="140" Width="100" Height="12" Horizontal="1">
                <Image Name = "Background_Image" Y="6" Png = "960"/>
                <Button Name="Move_Button" X="0" Y="0" BasePng="961" PushPng = "961"/>
              </ScrollBar>
              <BoardImage X="150" Y= "30" Width="2" Height="50" CustomColor="10,20,30" CustomAlpha="255"/>
            </Control>
            <Control Name="Shortcut" X="0" Y="0" {shortcut} EnableEvent = "ShortcutEnable">
              <List Name = "Shortcut_List" X = "5" Y = "5" Width = "100" Height = "100" Vertical = "1">
                <Control Name = "ShortcutInfo0" X="4" Width="200" Height="17"><Label Name="ShortcutName_Label" Caption="name0"/></Control>
              </List>
            </Control>
          </Window>
        </OptionUI>
        """;
    static UiLayout Layout(string xml)=>UiLayout.Parse(xml,"OptionUI-c.xml");
    static OptionPanel Panel(string? xml=null,Dictionary<int,UiSurface>? pngs=null){
        var p=new OptionPanel();var png=pngs??Pngs();var surf=Surfs();
        p.Load(Layout(xml??Xml()),id=>surf.GetValueOrDefault(id),id=>png.GetValueOrDefault(id));
        return p;
    }
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(OptionPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static int Pixel(OptionPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();
    static string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    [WorldModuleTest("option-panel")]
    static void Run(WorldModuleTestContext t){
        Registration(t);
        Parsing(t);
        Painting(t);
        Input(t);
        Unavailable(t);
        PngDecode(t);
        ProductPath(t);
    }

    static void Registration(WorldModuleTestContext t){
        var regs=UiPanelRegistry.Panels.Where(r=>r.Id==OptionPanel.PanelId).ToArray();
        t.Check(regs.Length==1&&regs[0].Layout==OptionPanel.HostLayout,"OptionUI registered once, on the host layout (Build never needs OptionUI.xml)",string.Join(",",regs.Select(r=>r.Layout)));
        t.Check(!UiPanelRegistry.Errors.Any(e=>e.Contains(nameof(OptionPanel))),"no discovery error for OptionPanel");
        var panel=regs[0].Create(new UiRoot());
        t.Check(panel is OptionPanel&&panel is IUiClosablePanel&&panel is IUiMovablePanel&&panel.Layer==UiLayer.Window,"window layer, closable, movable");
        t.Check(!panel.Visible,"closed until opened");
    }

    static void Parsing(WorldModuleTestContext t){
        var n=UiLayout.Parse("<a A=\"10814;1\" B=\"10817\" C=\"x\" D=\"1;2;3\" E=\"0\" F=\"5;z\"/>","t.xml").Root;
        t.Equal(new OptionImageRef("A",10814,1),OptionPanel.ImageAttr(n,"A"),"\"10814;1\" → id 10814, flag 1");
        t.Equal(new OptionImageRef("B",10817,0),OptionPanel.ImageAttr(n,"B"),"\"10817\" → flag 0");
        t.Check(OptionPanel.ImageAttr(n,"Z")==null,"absent → null");
        foreach(var bad in new[]{"C","D","E","F"})t.Throws<UiLayoutException>(()=>OptionPanel.ImageAttr(n,bad),$"{bad} malformed → UiLayoutException");
        t.Check(new OptionImageRef("BaseSurf",6125,1).IsSurf&&!new OptionImageRef("BasePng",1,0).IsSurf,"Surf attributes vs Png attributes");

        // Png names: "<id>c.png" then "<id>.png", Sprite{h} then Sprite (h = byte sum % 16; kor25 10814.png is in Sprite01).
        var c=OptionPngSource.Candidates(10814).ToArray();
        t.Equal("10814c.png,10814c.png,10814.png,10814.png",string.Join(",",c.Select(x=>x.Name)),"c.png first, then .png");
        t.Equal<(int,string)>((2,"10814.png"),c[2],"10814.png → Sprite01 (archive index 1+1)");
        t.Equal(0,c[3].Archive,"then Sprite");
        t.Equal<(int,string)>((7,"10816c.png"),OptionPngSource.Candidates(10816).First(),"10816c.png → Sprite06");

        var p=Panel();
        t.Check(p.Available&&p.Unavailable==null,"synthetic layout usable",p.Unavailable);
        t.Equal("OptionUI-c.xml",p.LayoutFile,"layout file recorded");
        t.Equal(new Point(100,50),p.Origin,"window X/Y");
        t.Equal(new Size(200,160),p.Size,"size = BasePng image");
        t.Equal(30,p.MoveableHeight,"Moveable=30");
        t.Equal("Home,Game,Shortcut",string.Join(",",p.Pages.Select(x=>x.Name)),"top-level Controls are pages, XML order");
        t.Equal("GameEnable",p.Pages[1].EnableEvent,"EnableEvent kept");
        t.Equal("GameBtn,Help_Button,Close",string.Join(",",p.WindowPieces.Select(x=>x.Name)),"window-level buttons");
        t.Check(p.WindowPieces[2].Image is {IsSurf:true,Id:801}&&p.WindowPieces[2].Surface!=null&&p.WindowPieces[2].PushSurface==null,"Close uses BaseSurf 801; missing PushSurf 802 → null");
        t.Check(p.WindowPieces[1].PushImage==null,"Help: misspelt HighlihgtPng is not a push image");
        t.Check(p.Missing.Contains("png 999 (OptionUI/Window/Control[0]/Image[1])"),"missing png listed with its path",string.Join(" | ",p.Missing));
        t.Check(p.Missing.Any(m=>m.StartsWith("surf 802 ",StringComparison.Ordinal))&&p.Missing.Any(m=>m.StartsWith("png 931 ",StringComparison.Ordinal)),"missing push images listed");
        t.Check(!p.Missing.Any(m=>m.StartsWith("png 911 ",StringComparison.Ordinal)),"HighlightPng is not loaded (no hover events)");
        var game=p.Pages[1].Pieces;
        t.Equal(new Point(90,70),game.Single(x=>x.MouseEvent=="RadioBtn").Offset,"nested Control offsets add up; missing Y = 0");
        t.Equal(new Point(60,146),game.Single(x=>x.Name=="Background_Image").Offset,"ScrollBar child: X absent = 0, Y=6 relative");
        t.Check(game.Single(x=>x.Kind==OptionPieceKind.Board) is {DeclaredSize:{Width:2,Height:50},Alpha:255} b&&b.Color.ToArgb()==Color.FromArgb(10,20,30).ToArgb(),"BoardImage colour, alpha and size");
        var label=p.Pages[0].Pieces.Single(x=>x.Kind==OptionPieceKind.Label);
        t.Check(label is {TextKind:"Desc",TextValue:"18806"}&&label.Offset==new Point(78,101)&&label.Color.ToArgb()==Color.FromArgb(171,163,131).ToArgb(),"label inside CheckButton: Desc id, colour, offset relative to the button");
        t.Check(p.Skipped.Any(s=>s.Contains("UpDownList"))&&p.Skipped.Any(s=>s.Contains("List[0]"))&&p.Skipped.Any(s=>s.Contains("Tooltip")),"collapsed list, runtime rows and tooltip are not built",string.Join(" | ",p.Skipped));
        t.Check(p.Skipped.Any(s=>s.Contains("ComboBox[0]/Label[0]")),"label without Desc/String (Caption / runtime text) not built");
        t.Check(game.Single(x=>x.MouseEvent=="RadioBtn").HitLocal.Size==new Size(12,12),"no declared size → hit box = image");
        t.Check(p.Pages[0].Pieces.Single(x=>x.Name=="ShowNpcName").HitLocal.Size==new Size(80,12),"declared Width/Height → hit box");

        t.Throws<UiLayoutException>(()=>Panel(Xml("Name=\"Other\" X=\"1\" Y=\"1\" BasePng=\"901\"")),"Window Name must be Option");
        t.Throws<UiLayoutException>(()=>Panel(Xml("Name=\"Option\" Y=\"1\" BasePng=\"901\"")),"window X missing → no invented origin");
        t.Throws<UiLayoutException>(()=>Panel(Xml("Name=\"Option\" X=\"1\" Y=\"1\" BasePng=\"abc\"")),"bad BasePng");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("CustomColor=\"10,20,30\"","CustomColor=\"10,20\"")),"bad CustomColor");
        t.Throws<UiLayoutException>(()=>Panel(Xml().Replace("<Control Name=\"Shortcut\"","<Control Name=\"Game\"")),"duplicate page name");
    }

    static void Painting(WorldModuleTestContext t){
        var p=Panel();
        p.Update(new UiStaticGameState(InWorld:true));
        t.Check(!p.Visible,"not open by itself");
        t.Check(p.Open()&&p.Visible&&p.ActivePage?.Name=="Home","Open → Home page");
        t.Equal(new Rectangle(100,50,200,160),p.Bounds,"bounds = origin + base size");
        t.Equal(Argb(FrameC),Pixel(p,101,51),"FramePng at the window origin, over the base");
        t.Equal(Argb(BaseC),Pixel(p,150,100),"base 901");
        t.Equal(Argb(TabC),Pixel(p,115,95),"tab GameBtn normal while Home is shown");
        t.Equal(Argb(HelpC),Pixel(p,112,192),"Help button");
        t.Equal(Argb(CloseC),Pixel(p,285,60),"Close surf 801 at (180,5)");
        t.Equal(Argb(HomeC),Pixel(p,165,75),"Home image 920");
        t.Equal(Argb(BaseC),Pixel(p,165,115),"missing png 999 not drawn (no substitute)");
        t.Equal(Argb(CheckC),Pixel(p,162,152),"check button 930");
        t.Equal(Argb(BaseC),Pixel(p,250,100),"Game page board not drawn on Home");
        var calls=new List<string>();
        p.TextResolver=(kind,id)=>{calls.Add(kind+":"+id);return null;};
        Pixel(p,0,0);
        t.Equal("Desc:18806",string.Join(",",calls),"labels ask the resolver; null → not drawn");

        t.Check(p.ShowPage("Game")&&p.ActivePage?.Name=="Game","ShowPage");
        t.Equal(Argb(TabPushC),Pixel(p,115,95),"tab of the shown page drawn with PushPng");
        t.Equal(Argb(GameC),Pixel(p,165,75),"Game image 940 replaces Home image");
        t.Equal(Argb(CheckC),Pixel(p,192,122),"radio button at (90,70)");
        t.Equal(Argb(ExpandC),Pixel(p,162,162),"combo expand button");
        t.Equal(Argb(MoveC),Pixel(p,162,192),"scroll thumb at the bar origin");
        t.Equal(Argb(ScrollC),Pixel(p,180,196),"scroll background at Y+6");
        t.Equal(Color.FromArgb(10,20,30).ToArgb(),Pixel(p,250,100),"BoardImage fill");
        t.Check(!p.ShowPage("Nope")&&p.ActivePage?.Name=="Game","unknown page ignored");
        p.Close();
        t.Check(!p.ShowPage("Home"),"ShowPage needs an open window");
    }

    static void Input(WorldModuleTestContext t){
        var p=Panel();var cmd=new UiNullCommands();
        t.Check(!p.Mouse(M(UiMouseKind.Down,new(150,100)),cmd),"closed window takes no input");
        p.Open();
        Click(p,new(115,95),cmd);
        t.Equal("Game",p.ActivePage?.Name,"click tab GameBtn → Game page");
        Click(p,new(115,95),cmd);
        t.Equal("Game",p.ActivePage?.Name,"clicking the shown tab keeps it");
        p.ShowPage("Home");
        Click(p,new(200,152),cmd);
        t.Check(p.Visible&&p.ActivePage?.Name=="Home"&&p.Unhandled.Contains("ShowNpcNameBtn"),"check button (declared 80×12 hit box) → recorded as not wired, nothing else changes",string.Join(",",p.Unhandled));
        Click(p,new(112,192),cmd);
        t.Check(p.Unhandled.Contains("HelpBtn"),"Help not wired");
        t.Check(p.Mouse(M(UiMouseKind.Down,new(150,100)),cmd)&&p.Mouse(M(UiMouseKind.Up,new(150,100)),cmd),"clicks inside are swallowed");
        t.Check(!p.Mouse(M(UiMouseKind.Down,new(10,10)),cmd),"clicks outside pass through");
        p.Mouse(M(UiMouseKind.Down,new(285,60)),cmd);
        p.Mouse(M(UiMouseKind.Move,new(150,100)),cmd);
        p.Mouse(M(UiMouseKind.Up,new(150,100)),cmd);
        t.Check(p.Visible,"Close released outside → still open");
        Click(p,new(285,60),cmd);
        t.Check(!p.Visible,"Close (CloseBtn) closes");
        t.Check(cmd.Calls.Count==0,"no commands issued (local window)");

        p.Open();
        t.Check(p.IsDragHandle(new(150,60))&&!p.IsDragHandle(new(150,85))&&!p.IsDragHandle(new(285,60))&&!p.IsDragHandle(new(50,60)),"drag strip = top 30 px, not on buttons, inside only");
        p.MoveTo(new(10,20));
        t.Check(p.Origin==new Point(10,20)&&p.Bounds==new Rectangle(10,20,200,160)&&p.ButtonAt(new(195,30))?.Name=="Close","move keeps child offsets");
        p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible,"leaving the world closes");

        var root=new UiRoot();root.Add(p);
        t.Equal(UiToggleResult.Opened,root.Toggle(p,()=>p.Open()),"UiRoot.Toggle opens");
        t.Check(root.WindowStack.Contains(p),"in the window stack");
        t.Equal(UiToggleResult.Closed,root.Toggle(p,()=>p.Open()),"open again = close (IUiClosablePanel)");
        t.Check(p.Toggle()&&p.Visible&&p.Toggle()&&!p.Visible,"own Toggle");
    }

    static void Unavailable(WorldModuleTestContext t){
        var none=new OptionPanel();
        var noSprites=Enumerable.Range(0,17).Select(i=>new ClientArchive(".",new(RefClientSurfFallback.ArchiveName(i)))).ToArray();
        none.Load(UiLayout.Parse("<host/>","uistatus86-c.xml"),new SurfCatalog(new UiXmlSource(new ClientArchive(".",new("Tile"))),noSprites));
        t.Check(!none.Available&&none.Unavailable!=null&&!none.Open()&&!none.Visible,"no UiResources → unavailable, never opens");

        var noBase=Pngs();noBase.Remove(901);
        var p=Panel(pngs:noBase);
        t.Check(p.Available&&p.Size==new Size(200,160)&&p.Missing.Any(m=>m.StartsWith("png 901 ",StringComparison.Ordinal)),"base png missing → size from the declared Control Width/Height");
        p.Open();
        t.Equal(Argb(HomeC),Pixel(p,165,75),"pieces still drawn without the base");
        t.Equal(Color.Black.ToArgb(),Pixel(p,150,100),"no substitute background");

        var bare=Panel(Xml(shortcut:""),noBase);
        t.Check(!bare.Available&&!bare.Open()&&bare.Bounds.IsEmpty,"no base and no declared size → unavailable",bare.Unavailable);
    }

    static byte[] PngBytes(int w,int h,Func<int,int,Color> pixel){
        using var bmp=new Bitmap(w,h,PixelFormat.Format32bppArgb);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)bmp.SetPixel(x,y,pixel(x,y));
        using var ms=new MemoryStream();bmp.Save(ms,ImageFormat.Png);return ms.ToArray();
    }

    static void PngDecode(WorldModuleTestContext t){
        var bytes=PngBytes(3,2,(x,y)=>x==2&&y==1?Color.FromArgb(0,0,0,0):x==1?Color.FromArgb(255,0,0,255):Color.FromArgb(255,255,0,0));
        var img=OptionPngSource.Decode(bytes,"t.png");
        t.Check(img.Width==3&&img.Height==2,"size");
        t.Equal(0xFFFF0000u,img.Argb[0],"opaque red");
        t.Equal(0xFF0000FFu,img.Argb[1],"opaque blue");
        t.Equal(0u,img.Argb[5],"transparent pixel → 0 (premultiplied)");
        t.Throws<UiResourceException>(()=>OptionPngSource.Decode(Encoding.ASCII.GetBytes("<not a png>"),"x"),"not a PNG");
        t.Throws<UiResourceException>(()=>OptionPngSource.Decode(bytes[..20],"x"),"truncated PNG");
        var src=new OptionPngSource([],(string?)null);
        t.Check(src.Get(10814)==null&&src.Get(0)==null&&src.Served.Count==0,"no archives, no reference → null");
    }

    static (byte[] Idx,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var idx=new MemoryStream();using var pak=new MemoryStream();using var w=new BinaryWriter(idx);
        w.Write((uint)files.Length);
        foreach(var (name,data) in files){
            w.Write((uint)pak.Position);var raw=new byte[20];Encoding.ASCII.GetBytes(name).CopyTo(raw,0);w.Write(raw);w.Write((uint)data.Length);pak.Write(data);
        }
        w.Flush();return (idx.ToArray(),pak.ToArray());
    }

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
        public string WriteIn(string sub,string name,byte[] data){string p=Path.Combine(Sub(sub),name);File.WriteAllBytes(p,data);files.Add(p);return p;}
        public void Dispose(){
            foreach(var f in files)try{File.Delete(f);}catch(IOException){}catch(UnauthorizedAccessException){}
            foreach(var d in dirs)try{if(Directory.Exists(d)&&!Directory.EnumerateFileSystemEntries(d).Any())Directory.Delete(d,false);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }

    static string Arch(string name)=>RefClientSurfFallback.ArchiveName(1+SurfCatalog.NameHash(name));

    // Product path: UiResources (synthetic main client without OptionUI.xml) + reference client Tile/Sprite on disk.
    static void ProductPath(WorldModuleTestContext t){
        using var s=new Scratch("option-panel");
        string main=s.Sub("main"),refRoot=s.Sub("ref");
        var (mIdx,mPak)=Archive(("Other-c.xml",Encoding.UTF8.GetBytes("<a/>")));
        s.WriteIn("main","Tile.idx",mIdx);s.WriteIn("main","Tile.pak",mPak);
        // Local Sprite archive with 920.png (main client wins over the reference for the same name).
        var localPng=PngBytes(2,2,(_,_)=>Color.FromArgb(255,1,2,3));
        var (lIdx,lPak)=Archive(("920.png",localPng));
        string lArch=Arch("920.png");
        s.WriteIn("main",lArch+".idx",lIdx);s.WriteIn("main",lArch+".pak",lPak);
        var pins=Enumerable.Range(0,17).Select(i=>RefClientSurfFallback.ArchiveName(i)).Select(n=>new ClientArchivePin(n)).ToArray();
        using var res=new UiResources(main,new ClientArchivePin("Tile"),pins);

        var noRef=new OptionPanel(res);
        noRef.Load(UiLayout.Parse("<host/>","uistatus86-c.xml"),res.Surfs);
        t.Check(!noRef.Available&&noRef.Unavailable!.Contains("OptionUI")&&!noRef.Open(),"no reference client → unavailable (Build is not affected)",noRef.Unavailable);

        // Reference client: plain OptionUI-c.xml in Tile, 901c.png in its hashed Sprite archive, a decoy 920.png.
        string xml=Xml().Replace("BaseSurf=\"801;1\" PushSurf=\"802;1\"","BasePng=\"913\"");   // no surf → no SurfList.xml needed
        var (rIdx,rPak)=Archive(("OptionUI-c.xml",Encoding.UTF8.GetBytes(xml)));
        s.WriteIn("ref","Tile.idx",rIdx);s.WriteIn("ref","Tile.pak",rPak);
        var basePng=PngBytes(200,160,(_,_)=>Color.FromArgb(255,32,32,32));
        var decoy=PngBytes(2,2,(_,_)=>Color.FromArgb(255,200,0,0));
        string bArch=Arch("901c.png");
        var groups=new[]{(bArch,"901c.png",basePng),(lArch,"920.png",decoy)}.GroupBy(g=>g.Item1);
        var written=new List<(string Path,byte[] Data)>();
        foreach(var g in groups){
            var (idx,pak)=Archive(g.Select(x=>(x.Item2,x.Item3)).ToArray());
            written.Add((s.WriteIn("ref",g.Key+".idx",idx),idx));written.Add((s.WriteIn("ref",g.Key+".pak",pak),pak));
        }
        res.Xml.Reference=new RefClientXmlSource(refRoot,"test");
        var p=new OptionPanel(res);
        p.Load(UiLayout.Parse("<host/>","uistatus86-c.xml"),res.Surfs);
        t.Check(p.Available&&p.LayoutFile=="OptionUI-c.xml","OptionUI-c.xml served by the reference Tile.pak",p.Unavailable);
        t.Check(p.BaseImage?.Surface is {Archive:var a,Width:200}&&a=="ref:"+bArch,"base png from the reference client (hashed archive, c.png)",p.BaseImage?.Surface?.Archive);
        var home=p.Pages[0].Pieces.First(x=>x.Kind==OptionPieceKind.Image);
        t.Check(home.Surface is {Archive:var la,Width:2}&&la==lArch,"920.png from the local pinned archive, not the reference decoy",home.Surface?.Archive);
        string sha=Sha(basePng);
        t.Check(UiResourceLog.Recent().Any(l=>l.StartsWith("option-png 901 -> ref ",StringComparison.Ordinal)&&l.Contains(sha)&&l.Contains("(idx ")),"payload and idx SHA-256 logged");
        t.Check(res.Xml.Reference!.Served.Any(x=>x.ResolvedName=="OptionUI-c.xml"),"XML source records the served file");
        t.Check(written.All(w=>File.ReadAllBytes(w.Path).AsSpan().SequenceEqual(w.Data)),"reference files untouched");
    }
}
