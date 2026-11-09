using P06;
using P06.NativeUi;
using System.Drawing.Imaging;

// ui-party (Issue #471). Offline only: synthetic Tile/Sprite archives under scratch/issue-471/, synthetic PartyView snapshots,
// no real client, no network. XML structure and attribute values copied from uiextra86-c.xml (PARTY_INTERFACE, PARTY_PET_RT_WINDOW).
partial class Checks {
    sealed class PartyTestState(bool inWorld,PartyView? party):IUiGameState,IUiPartyState {
        public bool InWorld=>inWorld;public string? CharacterName=>null;public int? Level=>null;
        public long? Hp=>null;public long? MaxHp=>null;public long? Mp=>null;public long? MaxMp=>null;public long Now=>0;
        public PartyView? Party=>party;
    }
    /// <summary>Stand-in for the future LEFT_WINDOW (ui-left-status): solid 135×125 at (0,475), records clicks.</summary>
    sealed class PartyFakeLeft:IUiPanel {
        public int Clicks;public static readonly Color Fill=Color.FromArgb(255,0,90,0);
        public string Id=>"LEFT_WINDOW";public UiLayer Layer=>UiLayer.Bar;public Rectangle Bounds=>new(0,475,135,125);public bool Visible=>true;
        public void Load(UiLayout layout,SurfCatalog surfs){}
        public void Update(IUiGameState state){}
        public void Paint(UiCanvas canvas){using var b=new SolidBrush(Fill);canvas.Graphics.FillRectangle(b,Bounds);}
        public bool Mouse(UiMouseEvent e,IUiCommands commands){if(e.Kind==UiMouseKind.Down)Clicks++;return true;}
        public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
    }

    const string PartyLayoutXml="""
<?xml version="1.0"?>
<ExtraWindowConfigulation>
<PARTY_INTERFACE x = "0" y = "0">
	<SURF_IMAGE surfName = "SURF_800_PARTY_BASE" surfNumber = "3204" xPos = "0" yPos = "493"/>
	<SURF_IMAGE surfName = "SURF_800_PARTY_BASE" surfNumber = "3204"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_BTN_CHAR_STATUS" surfNumber = "3205" xRPos = "11" yRPos = "481"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_BTN_PARTY_STATUS" surfNumber = "3208" xRPos = "36" yRPos = "481"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_LT_BTN_PET_STATUS" surfNumber = "3298" xRPos = "65" yRPos = "481"/>
	<IMAGE surfName = "T_IMG_PARTY_LT_PET_WIN" surfNumber = "3301" xPos = "0" yPos = "493"/>
	<IMAGE surfName = "SURF_800_PARTY_STATUS_GAUGE" surfNumber = "3211" xPos = "0" yPos = "0"/>
	<IMAGE surfName = "SURF_800_PARTY_STATUS_GAUGE_RED" surfNumber = "3212" xPos = "0" yPos = "0"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_BTN_PARTY_LEAVE_OFF" surfNumber = "3302" xRPos = "70" yRPos = "574"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_BTN_PARTY_BAN_OFF" surfNumber = "3305" xRPos = "90" yRPos = "574"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_BTN_PARTY_DELEGATE_OFF" surfNumber = "3308" xRPos = "109" yRPos = "574"/>
	<IMAGE surfName = "SURF_800_PARTY_CURSOR_SELECTED" surfNumber = "3219" xPos = "0" yPos = "0"/>
</PARTY_INTERFACE>
<PARTY_PET_RT_WINDOW x = "0" y = "0">
	<SURF_IMAGE surfName = "SURF_800_PARTY_RT_WINDOW" surfNumber = "3220" xPos = "485" yPos = "475"/>
	<SURF_IMAGE surfName = "SURF_800_PARTY_RT_WINDOW" surfNumber = "3220"/>
	<IMAGE surfName = "SURF_800_PET_RT_WINDOW" surfNumber = "3221" xPos = "0" yPos = "18"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_RT_BTN_PARTY_STATUS" surfNumber = "3295" xRPos = "11" yRPos = "6"/>
	<STAGE3_BTN surfName = "SURF_800_PET_RT_BTN_PET_STATUS" surfNumber = "3222" xRPos = "36" yRPos = "6"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_BTN_PARTY_LEAVE_OFF" surfNumber = "3302" xRPos = "82" yRPos = "100"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_BTN_PARTY_BAN_OFF" surfNumber = "3305" xRPos = "102" yRPos = "100"/>
	<STAGE3_BTN surfName = "SURF_800_PARTY_BTN_PARTY_DELEGATE_OFF" surfNumber = "3308" xRPos = "122" yRPos = "100"/>
	<IMAGE surfName = "SURF_800_PARTY_CURSOR_SELECTED" surfNumber = "3219" xPos = "0" yPos = "0"/>
	<IMAGE surfName = "SURF_800_PARTY_STATUS_GAUGE_GREY" surfNumber = "3225" xPos = "0" yPos = "0"/>
</PARTY_PET_RT_WINDOW>
</ExtraWindowConfigulation>
""";

    static UiResources PartyResources(string dir,IReadOnlyDictionary<int,byte[]> images,string layout){
        if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        string surfList="<?xml version=\"1.0\"?><SurfList>"+string.Concat(images.Keys.Select(id=>$"<surf id=\"{id}\" file=\"{id}\"/>"))+"</SurfList>";
        var tile=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)),("uiextra86-c.xml",Xml(layout)));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (id,img) in images){string name=id+".img";items[1+SurfCatalog.NameHash(name)].Add((name,img));}
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var sprites=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        return new UiResources(dir,tile,sprites);
    }

    [CheckGroup("native-ui-party"),CheckGroup(CheckGroups.Default,Order=120)]
    static void NativeUiPartyChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-471","synthetic");Directory.CreateDirectory(root);
        const ushort Base=0x0010,Pet=0x0014,Rt=0x0200,RtPet=0x0204,TabN=0x4000,TabD=0x4010,BtnN=0x4200,BtnD=0x4210,
            Gauge=0x2108,Red=0x7C00,Cursor=0x7FE0,Key=0x324C;
        var full=new Dictionary<int,byte[]>{
            [3204]=MacroImg(135,107,0,0,(_,_)=>Base),[3301]=MacroImg(135,107,0,0,(_,_)=>Pet),
            [3220]=MacroImg(158,125,0,0,(_,_)=>Rt),[3221]=MacroImg(158,107,0,0,(_,_)=>RtPet),
            [3211]=MacroImg(60,10,0,0,(_,_)=>Gauge),[3212]=MacroImg(58,8,0,0,(_,_)=>Red),[3225]=MacroImg(58,8,0,0,(_,_)=>0x0421),
            [3219]=MacroImg(64,27,1,Key,(x,y)=>x==0||y==0||x==63||y==26?Cursor:Key),   // hollow cursor, colour-keyed inside
        };
        foreach(var (a,w) in new[]{(3205,25),(3208,28),(3298,26),(3295,27),(3222,26)}){full[a]=MacroImg(w,11,0,0,(_,_)=>TabN);full[a+1]=MacroImg(w,11,0,0,(_,_)=>TabD);full[a+2]=MacroImg(w,11,0,0,(_,_)=>0x7C1F);}
        foreach(int a in new[]{3302,3305,3308}){full[a]=MacroImg(20,22,0,0,(_,_)=>BtnN);full[a+1]=MacroImg(20,22,0,0,(_,_)=>BtnD);full[a+2]=MacroImg(20,22,0,0,(_,_)=>0x7C1F);}
        static int Rgb(ushort v)=>Color.FromArgb(255,(v>>10&31)*255/31,(v>>5&31)*255/31,(v&31)*255/31).ToArgb();

        // --- registry: two entries on uiextra86, after any LEFT_WINDOW entry.
        var regs=UiPanelRegistry.Panels.Where(r=>r.Id is PartyPanel.WindowId or PartyRtPanel.WindowId).ToArray();
        Check(regs.Length==2&&regs.All(r=>r.Layout=="uiextra86"),"registry: PARTY_INTERFACE + PARTY_PET_RT_WINDOW from uiextra86");
        var ids=UiPanelRegistry.Panels.Select(r=>r.Id).ToList();
        Check(!ids.Contains("LEFT_WINDOW")||ids.IndexOf("LEFT_WINDOW")<ids.IndexOf(PartyPanel.WindowId),"registry: LEFT_WINDOW (if registered) comes before PARTY_INTERFACE, so the party base paints over it");

        using var res=PartyResources(Path.Combine(root,"full"),full,PartyLayoutXml);
        var left=new PartyFakeLeft();
        var ui=UiRoot.Build(res,[new("LEFT_WINDOW","uiextra86",_=>left),..regs]);
        var lt=(PartyPanel)ui.Find(PartyPanel.WindowId)!;var rt=(PartyRtPanel)ui.Find(PartyRtPanel.WindowId)!;
        Check(lt.Layer==UiLayer.Bar&&rt.Layer==UiLayer.Bar,"both party panels on the bar layer");

        // --- geometry (ui-layout.md §6 + uiextra86-c.xml).
        Check(lt.TabRects.SequenceEqual(new[]{new Rectangle(11,481,25,11),new Rectangle(36,481,28,11),new Rectangle(65,481,26,11)}),"LT tabs 3205/3208/3298 at x=11/36/65, y=481");
        Check(lt.BaseRect==new Rectangle(0,493,135,107),"base 3204 at (0,493) 135×107");
        Check(lt.ButtonRects.SequenceEqual(new[]{new Rectangle(70,574,20,22),new Rectangle(90,574,20,22),new Rectangle(109,574,20,22)}),"leave/ban/delegate at y=574, x=70/90/109");
        Check(lt.Current==PartyPanel.Page.Character&&lt.Bounds==new Rectangle(11,481,80,11),"default character tab: bounds = tab strip only");
        Check(lt.Cell(0)==new Rectangle(4,493,64,27)&&lt.Cell(1)==new Rectangle(68,493,64,27)&&lt.Cell(2)==new Rectangle(4,519,64,27)&&lt.Cell(6)==new Rectangle(4,571,64,27)&&lt.Cell(7).IsEmpty,"7 member cells 64×27, 2 columns, pitch 64×26 (cell 8 = buttons)");
        Check(lt.ButtonRects.All(b=>Rectangle.Intersect(b,new Rectangle(68,571,64,27))==b),"buttons sit inside the 8th cell");
        Check(lt.GaugeRect(0)==new Rectangle(6,508,60,10),"gauge 3211 (60×10) at cell + (2,15)");
        Check(rt.Bounds==new Rectangle(485,475,158,125),"RT frame 3220 at (485,475) 158×125");
        Check(rt.TabRects.SequenceEqual(new[]{new Rectangle(496,481,27,11),new Rectangle(521,481,26,11)}),"RT tabs relative to the frame: y=481 like the LT tabs");
        Check(rt.ButtonRects.SequenceEqual(new[]{new Rectangle(567,575,20,22),new Rectangle(587,575,20,22),new Rectangle(607,575,20,22)}),"RT buttons at frame + (82/102/122, 100)");
        Check(rt.ButtonRects.All(b=>Rectangle.Intersect(b,rt.Cell(1) with{Y=rt.Cell(1).Y+78})==b),"RT buttons inside the RT 8th cell");
        Check(rt.Cell(0)==new Rectangle(500,493,64,27)&&rt.Cell(1)==new Rectangle(565,493,64,27)&&rt.Cell(6)==new Rectangle(500,571,64,27)&&rt.PetOrigin==new Point(485,493),"RT cells from frame + (15,18), pitch 65×26; pet base at frame + (0,18)");

        // --- HP ratio → gauge width (58 px red bar).
        Check(PartyLayout.FillWidth(58,100)==58&&PartyLayout.FillWidth(58,50)==29&&PartyLayout.FillWidth(58,1)==0&&PartyLayout.FillWidth(58,99)==57&&PartyLayout.FillWidth(58,0)==0&&PartyLayout.FillWidth(58,null)==0,"red width = floor(58·hp/100); HP unknown → 0");
        Check(lt.FillRect(0,100)==new Rectangle(7,509,58,8)&&lt.FillRect(1,50)==new Rectangle(71,509,29,8)&&lt.FillRect(2,null).IsEmpty&&lt.FillRect(2,0).IsEmpty,"fill rect one pixel inside the gauge");
        Check(rt.FillRect(0,75)==new Rectangle(503,509,43,8),"RT fill uses the same 58 px rule");

        // --- state and commands.
        var members=new PartyMember[]{new(11,"Alpha",100),new(12,"Bravo",50),new(13,"Charlie",null),new(14,"Delta",0)};
        PartyView View(bool available=true,bool canKick=true,PartyMember[]? m=null)=>new(1,1,1,77,m??members,"Me","",null,available,true,canKick);
        var log=new List<(string Cmd,string Name)>();
        var cmd=new UiMacroCommands(new UiPartyCommands(new UiNullCommands(),(c,v,n)=>log.Add((c,n))),(_,_)=>{});   // product nesting
        var input=new UiInput(ui,cmd){Viewport=UiPresenter.Compute(new(800,600))};
        UiMouseResult Click(int x,int y){var r=input.Mouse(UiMouseKind.Down,new(x,y),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(x,y),MouseButtons.Left);return r;}

        ui.Update(new PartyTestState(false,View()));
        Check(!lt.Visible&&!rt.Visible,"outside the world: hidden");
        ui.Update(new PartyTestState(true,null));
        Check(!lt.Visible,"no party snapshot: hidden");
        var leftState=LeftStatusState.Empty with{Level=42};
        var nested=new UiHpMpGameState(new UiMacroGameState(new UiLeftStatusGameState(new UiPartyGameState(new UiStaticGameState(true),View()),leftState),new MacroBook(),null,false),[]);
        Check(PartyStates.StateOf(nested)?.Party!=null&&PartyStates.CommandsOf(cmd)!=null&&MacroBook.StateOf(nested)!=null&&LeftStatusState.From(nested).Level==42,"product nesting HpMp→Macro→LeftStatus→Party: party, macro and left status all found");
        Check(PartyStates.CommandsOf(new UiNullCommands())==null,"commands without party support → none");
        ui.Update(nested);
        Check(lt.Visible&&rt.Visible,"in the world: LT tabs shown; RT shown as the only BAR_485 member");

        // --- tab switching and layering against LEFT_WINDOW.
        Check(Click(20,550).Route==UiRoute.Panel&&left.Clicks==1,"character tab: (20,550) reaches LEFT_WINDOW");
        Click(40,485);Check(lt.Current==PartyPanel.Page.Party&&lt.Bounds==new Rectangle(0,481,135,119),"party tab click → party page, bounds = tabs ∪ base");
        Check(Click(120,520).Route==UiRoute.Panel&&left.Clicks==1&&ui.HitTest(new(120,520))==lt,"party page: the base takes clicks over LEFT_WINDOW");
        Check(ui.HitTest(new(5,478))==left,"above the tab row LEFT_WINDOW still gets clicks");
        {
            using var canvas=new UiCanvas();ui.Compose(canvas,null);
            int At(int x,int y)=>canvas.Bitmap.GetPixel(x,y).ToArgb();
            Check(At(2,560)==Rgb(Base)&&At(134,599)==Rgb(Base),"party base drawn over LEFT_WINDOW (0,493)–(134,599)");
            Check(At(2,480)==PartyFakeLeft.Fill.ToArgb()&&At(5,490)==PartyFakeLeft.Fill.ToArgb(),"LEFT_WINDOW still visible above the base (y<493) and left of the tabs");
            Check(At(12,482)==Rgb(TabN)&&At(37,482)==Rgb(TabD)&&At(66,482)==Rgb(TabN),"selected tab uses a+1, others a");
            Check(At(7,509)==Rgb(Red)&&At(64,516)==Rgb(Red),"Alpha 100 %: full 58 px red bar");
            Check(At(71,509)==Rgb(Red)&&At(99,509)==Rgb(Red)&&At(100,509)==Rgb(Gauge)&&At(70,508)==Rgb(Gauge),"Bravo 50 %: red 29 px, then gauge frame");
            Check(At(7,535)==Rgb(Gauge)&&At(64,535)==Rgb(Gauge),"Charlie HP unknown: frame only, no red");
            Check(At(71,535)==Rgb(Gauge),"Delta 0 %: frame only");
            Check(At(7,561)==Rgb(Base),"no 5th member: cell left empty");
            Check(At(72,580)==Rgb(BtnN)&&At(92,580)==Rgb(BtnN)&&At(111,580)==Rgb(BtnN),"three buttons drawn");
            bool text=false;for(int y=495;y<507;y++)for(int x=6;x<60;x++){var c=canvas.Bitmap.GetPixel(x,y);text|=c.R>200&&c.G>200&&c.B>200;}
            Check(text,"member name drawn above the gauge");
            Check(At(490,480)==Rgb(Rt)&&At(503,509)==Rgb(Red)&&At(560,509)==Rgb(Red),"RT frame and Alpha's bar drawn");
        }

        // --- selection and the existing party actions.
        Click(80,500);Check(lt.Selected=="Bravo"&&rt.Selected==null,"click on cell 2 selects Bravo (LT only)");
        Click(80,530);Check(lt.Selected=="Delta","cell 4 → Delta");
        Click(80,560);Check(lt.Selected==null,"empty cell clears the selection");
        Click(91,580);Check(log.Count==0,"ban without a selection → nothing sent");
        Click(80,500);Click(91,580);Check(log.Count==1&&log[0]==("kick","Bravo"),"ban → existing \"kick\" with the selected name");
        Click(71,580);Check(log.Count==2&&log[1]==("leave",""),"leave → existing \"leave\"");
        Click(110,580);Check(log.Count==2,"delegate → no existing action, nothing sent");
        input.Mouse(UiMouseKind.Down,new(71,580),MouseButtons.Left,1);
        {using var canvas=new UiCanvas();ui.Compose(canvas,null);Check(canvas.Bitmap.GetPixel(72,580).ToArgb()==Rgb(BtnD),"pressed button shows a+1");}
        input.Mouse(UiMouseKind.Up,new(30,520),MouseButtons.Left);Check(log.Count==2,"release outside the pressed button → nothing sent");
        ui.Update(new PartyTestState(true,View(canKick:false)));Click(91,580);Check(log.Count==2&&lt.Selected=="Bravo","not leader (CanKick false) → ban sends nothing");
        ui.Update(new PartyTestState(true,View(available:false)));Click(71,580);Check(log.Count==2,"party actions unavailable (busy) → leave sends nothing");
        ui.Update(new PartyTestState(true,View(m:[members[0]])));Check(lt.Selected==null,"selected member left the party → selection cleared");
        ui.Update(new PartyTestState(true,View(m:[])));Click(71,580);Check(log.Count==2,"no members → leave sends nothing");
        ui.Update(new PartyTestState(true,View()));

        // --- pet tab, back to character tab; RT tabs.
        Click(70,485);Check(lt.Current==PartyPanel.Page.Pet,"pet tab → pet page");
        {using var canvas=new UiCanvas();ui.Compose(canvas,null);Check(canvas.Bitmap.GetPixel(2,560).ToArgb()==Rgb(Pet)&&canvas.Bitmap.GetPixel(66,482).ToArgb()==Rgb(TabD),"pet page draws 3301 over LEFT_WINDOW, pet tab pressed");}
        Click(15,485);Check(lt.Current==PartyPanel.Page.Character&&ui.HitTest(new(20,550))==left,"character tab → LEFT_WINDOW uncovered again");
        {using var canvas=new UiCanvas();ui.Compose(canvas,null);Check(canvas.Bitmap.GetPixel(2,560).ToArgb()==PartyFakeLeft.Fill.ToArgb()&&canvas.Bitmap.GetPixel(12,482).ToArgb()==Rgb(TabD),"character page: LEFT_WINDOW visible, char tab pressed");}
        Click(525,485);Check(rt.Current==PartyRtPanel.Page.Pet,"RT pet tab");
        {using var canvas=new UiCanvas();ui.Compose(canvas,null);Check(canvas.Bitmap.GetPixel(490,500).ToArgb()==Rgb(RtPet)&&canvas.Bitmap.GetPixel(503,509).ToArgb()==Rgb(RtPet),"RT pet page: 3221 at (485,493), no member bars");}
        Click(500,485);Check(rt.Current==PartyRtPanel.Page.Party,"RT party tab");
        Click(580,500);Click(588,585);Check(rt.Selected=="Bravo"&&log.Count==3&&log[2]==("kick","Bravo"),"RT: select Bravo, ban → kick");

        // --- BAR_485 slot and Alt+F3.
        var group=ui.SlotGroup(UiSlotGroup.Bar485);
        group.Add("MINI_MAP_WINDOW");
        Check(group.Members[0]=="MINI_MAP_WINDOW"&&group.IsShown(PartyRtPanel.WindowId),"RT is a BAR_485 member after the minimap (V5 order)");
        Check(input.Key(new UiKeyEvent(Keys.Alt|Keys.F3,true,false))&&group.IsShown("MINI_MAP_WINDOW")&&!rt.Visible,"Alt+F3 cycles BAR_485 → RT hidden");
        Check(input.Key(new UiKeyEvent(Keys.Alt|Keys.F3,true,true))&&group.IsShown("MINI_MAP_WINDOW"),"auto-repeat does not cycle again");
        Check(input.Key(new UiKeyEvent(Keys.Alt|Keys.F3,true,false))&&rt.Visible,"Alt+F3 again → RT shown");
        Check(!input.Key(new UiKeyEvent(Keys.F3,true,false)),"plain F3 is not taken by the party panel");

        // --- missing surfs: nothing drawn, no placeholder.
        {
            var partial=full.Where(kv=>kv.Key is not (3211 or 3212 or 3220)).ToDictionary(kv=>kv.Key,kv=>kv.Value);
            using var res2=PartyResources(Path.Combine(root,"partial"),partial,PartyLayoutXml);
            var ui2=UiRoot.Build(res2,regs);
            var lt2=(PartyPanel)ui2.Find(PartyPanel.WindowId)!;var rt2=(PartyRtPanel)ui2.Find(PartyRtPanel.WindowId)!;
            ui2.Update(new PartyTestState(true,View()));lt2.SetPage(PartyPanel.Page.Party);
            Check(!rt2.Visible&&lt2.Visible&&lt2.GaugeRect(0).IsEmpty&&lt2.FillRect(0,100).IsEmpty,"missing gauges/RT frame → no gauge rects, RT hidden");
            using var canvas=new UiCanvas();ui2.Compose(canvas,null);
            Check(canvas.Bitmap.GetPixel(10,512).ToArgb()==Rgb(Base)&&canvas.Bitmap.GetPixel(490,480).ToArgb()==Color.Black.ToArgb(),"missing surfs draw nothing");
        }

        // --- malformed layouts are refused (no invented coordinates).
        {
            var surfs=res.Surfs;
            Throws<UiLayoutException>(()=>new PartyPanel().Load(UiLayout.Parse(PartyLayoutXml.Replace("SURF_800_PARTY_BTN_PARTY_BAN_OFF","X"),"bad.xml"),surfs),"missing ban button refused","SURF_800_PARTY_BTN_PARTY_BAN_OFF");
            Throws<UiLayoutException>(()=>new PartyPanel().Load(UiLayout.Parse(PartyLayoutXml.Replace("surfNumber = \"3204\" xPos = \"0\" yPos = \"493\"","surfNumber = \"3204\""),"bad.xml"),surfs),"base without position refused");
            Throws<UiLayoutException>(()=>new PartyRtPanel(new UiSlotGroup("T")).Load(UiLayout.Parse(PartyLayoutXml.Replace("<PARTY_INTERFACE","<OTHER_INTERFACE").Replace("</PARTY_INTERFACE>","</OTHER_INTERFACE>"),"bad.xml"),surfs),"RT without PARTY_INTERFACE gauges refused","缺少節點");
        }
        NativeUiPartyRealClient(repo);
    }

    /// <summary>Optional (TW_CLIENT_ROOT): real Tile/Sprite; PNG and coordinate table only under scratch/issue-471/real.</summary>
    static void NativeUiPartyRealClient(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui party real client render (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-471","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        // LEFT_WINDOW included (registry order) so the PNGs show the party base over the left status frame.
        var regs=UiPanelRegistry.Panels.Where(r=>r.Id is "LEFT_WINDOW" or PartyPanel.WindowId or PartyRtPanel.WindowId).ToArray();
        var ui=UiRoot.Build(res,regs);
        var lt=(PartyPanel)ui.Find(PartyPanel.WindowId)!;var rt=(PartyRtPanel)ui.Find(PartyRtPanel.WindowId)!;
        Check(lt.BaseRect==new Rectangle(0,493,135,107)&&rt.Bounds==new Rectangle(485,475,158,125),"real 3204 = (0,493) 135×107, real 3220 = (485,475) 158×125");
        Check(lt.GaugeRect(0).Size==new Size(60,10)&&lt.FillRect(0,100).Size==new Size(58,8),"real gauge 3211 60×10, red 3212 58×8");
        var members=new PartyMember[]{new(1,"Tester1",100),new(2,"Tester2",50),new(3,"Tester3",null),new(4,"Tester4",0),new(5,"Tester5",25),new(6,"Tester6",75),new(7,"Tester7",10)};
        ui.Update(new PartyTestState(true,new(1,1,1,9,members,"Me","",null,true,true,true)));
        using var canvas=new UiCanvas();
        lt.SetPage(PartyPanel.Page.Party);ui.Compose(canvas,null);canvas.Bitmap.Save(Path.Combine(outDir,"party-lt-party.png"),ImageFormat.Png);
        lt.SetPage(PartyPanel.Page.Pet);rt.SetPage(PartyRtPanel.Page.Pet);ui.Compose(canvas,null);canvas.Bitmap.Save(Path.Combine(outDir,"party-pet.png"),ImageFormat.Png);
        lt.SetPage(PartyPanel.Page.Character);rt.SetPage(PartyRtPanel.Page.Party);ui.Compose(canvas,null);canvas.Bitmap.Save(Path.Combine(outDir,"party-lt-char-rt-party.png"),ImageFormat.Png);
        File.WriteAllText(Path.Combine(outDir,"party-coordinates.json"),System.Text.Json.JsonSerializer.Serialize(new{
            client,lt=new{tabs=lt.TabRects.Select(r=>r.ToString()),baseRect=lt.BaseRect.ToString(),buttons=lt.ButtonRects.Select(r=>r.ToString()),cells=Enumerable.Range(0,7).Select(k=>lt.Cell(k).ToString()),gauges=Enumerable.Range(0,7).Select(k=>lt.GaugeRect(k).ToString())},
            rt=new{bounds=rt.Bounds.ToString(),tabs=rt.TabRects.Select(r=>r.ToString()),buttons=rt.ButtonRects.Select(r=>r.ToString()),cells=Enumerable.Range(0,7).Select(k=>rt.Cell(k).ToString()),pet=rt.PetOrigin.ToString()}},
            new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
}
