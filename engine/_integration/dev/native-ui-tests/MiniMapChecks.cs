using P06;
using P06.NativeUi;
using System.Drawing.Imaging;

// ui-minimap (Issue #470). Offline only: every archive, XML, .img and png here is synthesized under scratch/issue-470/;
// the real client is never read. The few real MapSystem numbers below are copied coordinates, not game assets.
partial class Checks {
    sealed class MapTestState(bool inWorld,UiMapSnapshot? map):IUiGameState,IUiMapSource {
        public bool InWorld=>inWorld;
        public string? CharacterName=>null;public int? Level=>null;public long? Hp=>null;public long? MaxHp=>null;public long? Mp=>null;public long? MaxMp=>null;
        public long Now=>0;
        public UiMapSnapshot? ReadMap()=>map;
    }
    static Color MiniRgb555(ushort p)=>Color.FromArgb(unchecked((int)UiImage.ToArgb(p)));
    static bool Same(Bitmap b,int x,int y,Color c)=>b.GetPixel(x,y).ToArgb()==c.ToArgb();
    /// <summary>w×h image whose border (thickness t) is colour and whose interior is the transparent key.</summary>
    static byte[] Hollow(int w,int h,int t,ushort colour){
        const ushort key=0x0001;var px=new ushort[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)px[y*w+x]=x<t||y<t||x>=w-t||y>=h-t?colour:key;
        return Img(w,h,1,key,px);
    }
    static Color PngColour(int x,int y)=>Color.FromArgb(255,x&255,y&255,77);

    static void NativeUiMiniMapChecks(string repo){
        string dir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-470","synthetic");
        Directory.CreateDirectory(dir);foreach(var f in Directory.GetFiles(dir))File.Delete(f);

        // --- MapSystem ratio / baseLoc / basePixel conversion (TW16 sub_A67070), synthetic rows.
        {
            var e=new FieldMapEntry(4,10100,24f,new(32800,32800),new(200,150));
            Check(e.ToPixel(32800,32800)==new Point(200,150),"baseLoc maps to basePixel");
            Check(e.ToPixel(32801,32800)==new Point(201,150)&&e.ToPixel(32802,32800)==new Point(202,149),"ratio 24: +x → X+1, Y−0.5 (truncated toward zero)");
            Check(e.ToPixel(32800,32802)==new Point(202,151)&&e.ToPixel(32799,32800)==new Point(199,150)&&e.ToPixel(32790,32800)==new Point(190,155),"ratio 24: +y → X+1, Y+0.5; negative deltas truncate toward zero");
            Check(e.PngName=="10100.png","png name is \"<pngNum>.png\"");
            var half=e with{Ratio=12f};
            Check(half.ToPixel(32801,32801)==new Point(204,150),"ratio 12 doubles the scale");
            var world=e with{Ratio=144f};
            Check(world.ToPixel(32806,32800)==new Point(201,150)&&world.ToPixel(32812,32800)==new Point(202,149),"ratio 144 is 1/6 of ratio 24");
            // Same isometric step as the engine's own scene projection (Coordinates.Screen), divided by ratio/… i.e. by 24 at ratio 24.
            bool iso=true;var rng=new Random(470);
            for(int i=0;i<500;i++){
                int x=32800+rng.Next(-900,900),y=32800+rng.Next(-900,900);
                var s=Coordinates.Screen(x,y);var s0=Coordinates.Screen(32800,32800);
                var want=new Point(200+(int)((s.X-s0.X)/24f),150+(int)((s.Y-s0.Y)/24f));
                iso&=e.ToPixel(x,y)==want;
            }
            Check(iso,"ratio 24 conversion equals the engine isometric projection / 24 (500 random tiles)");
            // Real rows (numbers only): Aden field 10101.png is 3260×1660, Aden world 10001.png is 550×316.
            var adenField=new FieldMapEntry(4,10101,24f,new(32887,32652),new(929,998));
            var adenWorld=new FieldMapEntry(1,10001,144f,new(32887,32652),new(149,185));
            (int X,int Y)[] towns=[(34073,33143),(32616,32785),(34063,32274),(33601,33246),(33430,32813)];
            Check(towns.All(t=>new Rectangle(0,0,3260,1660).Contains(adenField.ToPixel(t.X,t.Y)))&&towns.All(t=>new Rectangle(0,0,550,316).Contains(adenWorld.ToPixel(t.X,t.Y))),
                "real Aden/Gludin/Oren/Heine/Giran coordinates land inside both real png sizes (f = 24/ratio)");
            Check(adenField.ToPixel(34073,33143)==new Point(2606,651)&&adenWorld.ToPixel(34073,33143)==new Point(428,128),"Aden town: exact pixels at ratio 24 and 144");
        }

        // --- 8-direction arrow surf selection.
        {
            Check(Enumerable.Range(0,8).All(h=>MapViewLayout.ArrowSurf(2818,h)==2818+h),"heading 0–7 → surf 2818–2825");
            Check(MapViewLayout.ArrowSurf(2818,8)==null&&MapViewLayout.ArrowSurf(2818,-1)==null&&MapViewLayout.ArrowSurf(2818,255)==null,"heading outside 0–7 → no arrow (no guessed image)");
        }

        // --- view geometry (sub_A695E0): player always at origin+(80,60); source window clamped into the image.
        {
            var v=MapViewLayout.Compute(new(0,0),new(200,150),new(400,300),false);
            Check(v.Source==new Rectangle(120,90,160,120)&&v.ToCanvas(v.Player)==new Point(80,60)&&v.ArrowAt==new Point(75,56),"centre: source 160×120 around the player, arrow at (75,56)");
            Check(v.Markers==Rectangle.FromLTRB(123,93,277,207),"Field_Map markers inside the source inset by 3");
            var left=MapViewLayout.Compute(new(5,250),new(10,10),new(400,300),false);
            Check(left.Source==new Rectangle(0,0,160,120)&&left.ToCanvas(new(10,10))==new Point(85,310)&&left.ToCanvas(new(0,0))==new Point(75,300),"top-left edge: source clamped to 0, player still centred (image shifted)");
            var right=MapViewLayout.Compute(new(0,0),new(395,295),new(400,300),false);
            Check(right.Source==new Rectangle(240,180,160,120)&&right.ToCanvas(new(395,295))==new Point(80,60),"bottom-right edge: source clamped to the image end");
            var bar=MapViewLayout.Compute(new(0,0),new(200,150),new(400,300),true);
            Check(bar.Markers==Rectangle.FromLTRB(135,108,265,206),"bar minimap markers: inset (15,18,15,4)");
            Check(v.ImageClip==new Rectangle(3,3,154,114),"image clip = view inset by 3");
        }

        // --- synthetic client: Tile (SurfList, uistatus86-c.xml, MapSystem-c.xml) + 17 Sprite archives with .img and one png.
        const ushort Base=0x001F,FrameC=0x7C00,FieldFrame=0x03E0,Title=0x7FE0,Exit=0x7C1F,Zoom=0x03FF,DungeonTitle=0x4210,Party=0x56B5;
        static ushort ArrowC(int k)=>(ushort)(((k+1)<<10)|0x001F);
        int[] ids=[3274,3275,2815,2816,2801,2803,2888,2826,..Enumerable.Range(2818,8)];
        string surfList="<?xml version=\"1.0\"?><SurfList>"+string.Concat(ids.Select(i=>$"<surf id=\"{i}\" file=\"{i}\"/>"))+"</SurfList>";
        string status="<?xml version=\"1.0\"?><MainStatusConfigulation>\n  <MINI_MAP_WINDOW x = \"485\" y = \"475\">\n    <MAIN_WINDOW>\n      <MINI_MAP_BASE surfNumber = \"3274\" xRPos = \"12\" yRPos = \"20\"/>\n      <MINI_MAP_FRAME surfNumber = \"3275\" xRPos = \"0\" yRPos = \"0\"/>\n    </MAIN_WINDOW>\n  </MINI_MAP_WINDOW>\n</MainStatusConfigulation>";
        string Common(bool withCommon)=>withCommon?"<Common_UI><USER_POINT_IMG surfNum = \"2818\"/><PARTY_POINT_IMG surfNum = \"2826\"/></Common_UI>":"";
        string MapSystem(bool withCommon=true)=>"<?xml version=\"1.0\"?>\n<!--Map System Configulation-->\n<Map_System_Configulation>\n"+
            "<Field_Map pos = \"5,250\" transparency = \"20\">"+
            "<TITLE_IMG surfNum = \"2815\" pos = \"96,1\"/><FRAME_IMG surfNum = \"2816\" pos = \"0,0\"/><EXIT_IMG surfNum = \"2801\" pos = \"146,6\"/><ZOOM_IMG surfNum = \"2803\" pos = \"129,6\"/>"+
            "<Map mapNum = \"4\" pngNum = \"10100\" ratio = \"24.0\" baseLoc = \"32800,32800\" basePixel= \"200,150\"/>"+
            "<Map mapNum = \"300\" pngNum = \"10118\" ratio = \"24.0\" baseLoc = \"32896 32523\" basePixel= \"118,83\"/>"+
            "<Map mapNum = \"1003\" pngNum = \"10157\" ratio = \"24.0\" bbaseLoc = \"32894 32797\" basePixel= \"345,136\"/>"+
            "<Map mapNum = \"7\" pngNum = \"10199\" ratio = \"0\" baseLoc = \"1,1\" basePixel= \"1,1\"/>"+
            "<Map mapNum = \"8\" pngNum = \"10198\" ratio = \"24.0\" baseLoc = \"1,1\" basePixel= \"1,1\"/>"+
            "<Map mapNum = \"8\" pngNum = \"10197\" ratio = \"24.0\" baseLoc = \"2,2\" basePixel= \"2,2\"/>"+
            "<Map mapNum = \"9\" pngNum = \"10109\" ratio = \"24.0\" baseLoc = \"32800,32800\" basePixel= \"200,150\"/>"+
            "<Map mapNum = \"5001\" pngNum = \"10100\" ratio = \"24.0\" baseLoc = \"32800,32800\" basePixel= \"200,150\"/>"+
            "</Field_Map>\n<Dungern_Map pos = \"5,250\" transparency = \"7\"><TITLE_IMG surfNum = \"2888\" pos = \"113,1\"/><FRAME_IMG surfNum = \"2816\" pos = \"0,0\"/><EXIT_IMG surfNum = \"2801\" pos = \"146,6\"/>"+
            "<Map mapNum = \"1\"><Portal loc = \"32217,32227\" mapDesc = \"309\" color = \"20,20,20\"/></Map></Dungern_Map>\n"+
            "<Restricted_Map><Map_Range mapRange = \"5001~5134\"/><Map_Range mapRange = \"16384~65535\"/></Restricted_Map>\n"+Common(withCommon)+"\n</Map_System_Configulation>";

        byte[] png;
        using(var bmp=new Bitmap(400,300,PixelFormat.Format24bppRgb)){
            for(int y=0;y<300;y++)for(int x=0;x<400;x++)bmp.SetPixel(x,y,PngColour(x,y));
            using var ms=new MemoryStream();bmp.Save(ms,ImageFormat.Png);png=ms.ToArray();
        }
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        void Put(string name,byte[] data)=>items[1+SurfCatalog.NameHash(name)].Add((name,data));
        Put("3274.img",Img(135,101,0,0,Base));
        Put("3275.img",Hollow(158,125,2,FrameC));
        Put("2816.img",Hollow(161,121,1,FieldFrame));
        Put("2815.img",Img(10,5,0,0,Title));Put("2801.img",Img(8,8,0,0,Exit));Put("2803.img",Img(8,8,0,0,Zoom));Put("2888.img",Img(12,5,0,0,DungeonTitle));
        Put("2826.img",Img(6,6,0,0,Party));
        for(int k=0;k<8;k++)Put($"{2818+k}.img",Img(9,9,0,0,ArrowC(k)));
        Put("10100.png",png);
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)),("uistatus86-c.xml",Xml(status)),("MapSystem-c.xml",Xml(MapSystem())));
        Check(SurfCatalog.NameHash("10100.png")==5,"png lookup: \"10100.png\" hashes to Sprite05 (real file lives there)");

        // --- MapSystem parse: valid rows, malformed rows rejected (never repaired), restricted ranges, Common_UI.
        {
            var data=MapSystemData.Parse(UiLayout.Parse(MapSystem(),"MapSystem-c.xml"));
            Check(data.FieldMaps.Keys.Order().SequenceEqual(new[]{4,9,5001}),"usable Field_Map rows: 4, 9, 5001");
            Check(data.Rejected.Count==4&&data.Rejected.Any(r=>r.Contains("300"))&&data.Rejected.Any(r=>r.Contains("1003"))&&data.Rejected.Any(r=>r.Contains("mapNum 7"))&&data.Rejected.Count(r=>r.Contains("mapNum 8"))==1,
                "rejected: space-separated baseLoc (300), missing baseLoc (1003), ratio 0 (7), duplicate mapNum (8, both dropped)");
            Check(data.IsRestricted(5001)&&data.IsRestricted(5134)&&data.IsRestricted(65535)&&!data.IsRestricted(5135)&&!data.IsRestricted(4),"Restricted_Map ranges, including 16384~65535");
            Check(data.Find(5001)==null&&data.Find(4)?.PngNum==10100&&data.Find(77)==null,"restricted rows are not used; unlisted map → null");
            Check(data.UserPointSurf==2818&&data.PartyPointSurf==2826,"Common_UI: USER_POINT 2818, PARTY_POINT 2826");
            Check(data.Field.Pos==new Point(5,250)&&data.Field.Zoom?.Pos==new Point(129,6)&&data.Field.Exit.Pos==new Point(146,6)&&data.Dungeon.Zoom==null&&data.Dungeon.Title.Surf==2888,"Field_Map / Dungern_Map chrome");
            Throws<UiLayoutException>(()=>MapSystemData.Parse(UiLayout.Parse(MapSystem(false),"MapSystem-c.xml")),"missing Common_UI refuses the layout","Common_UI");
            Check(MapSystemData.TryPair(" 5 , 250 ")==new Point(5,250)&&MapSystemData.TryPair("32896 32523")==null&&MapSystemData.TryPair("1,2,3")==null,"pos pairs: x,y only");
        }

        using var res=new UiResources(dir,tilePin,spritePins);
        var root=UiRoot.Build(res,UiPanelRegistry.Panels.Where(r=>r.Id==MiniMapPanel.PanelId).ToArray());   // other modules need their own fixtures
        var mini=(MiniMapPanel)root.Find(MiniMapPanel.PanelId)!;
        var field=(FieldMapPanel)root.Find(FieldMapPanel.PanelId)!;
        var group=root.SlotGroup(UiSlotGroup.Bar485);
        Check(UiPanelRegistry.Panels.Count(r=>r.Id==MiniMapPanel.PanelId)==1&&ReferenceEquals(mini.Field,field)&&ReferenceEquals(field.Map,mini.Map),"registry: one line builds MINI_MAP_WINDOW and its Field_Map window (shared MapState)");
        Check(mini.Layer==UiLayer.Bar&&field.Layer==UiLayer.Window,"layers: minimap on the bar, Field_Map a window");
        Check(mini.Bounds==new Rectangle(485,475,158,125)&&mini.BaseRect==new Rectangle(497,495,135,101)&&mini.ViewOrigin==new Point(497,500),"MINI_MAP_WINDOW (485,475) 158×125, base 135×101 at +12,+20, view at base+(0,5)");
        Check(group.Current==MiniMapPanel.PanelId&&mini.Visible,"BAR_485 starts on the minimap (mode 0)");
        Check(field.Bounds==Rectangle.Empty&&field.Visible&&!mini.Map.FieldOpen,"Field_Map starts closed (empty bounds, still offered keys)");

        var input=new UiInput(root,new UiNullCommands());
        var cmds=new UiNullCommands();
        using var canvas=new UiCanvas();
        Bitmap Frame(IUiGameState s){root.Update(s);root.Compose(canvas,null);return canvas.Bitmap;}
        Color Png(int x,int y)=>PngColour(x,y);

        // --- not in world: base + frame only.
        {
            var b=Frame(new MapTestState(false,null));
            Check(Same(b,540,530,MiniRgb555(Base))&&Same(b,485,475,MiniRgb555(FrameC))&&Same(b,642,599,MiniRgb555(FrameC)),"outside the world: base 3274 and frame 3275 only");
            Check(mini.Map.Kind==MapViewKind.None&&root.Faulted.Count==0,"no map kind, nothing faulted");
        }

        // --- in world on a listed map: png centred on the player, arrow by heading, party dots inside the marker rect.
        var self=new UiMapSnapshot(4,32800,32800,3,[new("near",32810,32800),new("edge",32765,32765)]);
        {
            var b=Frame(new MapTestState(true,self));
            Check(mini.Map.Kind==MapViewKind.Field&&mini.Map.Image?.Size==new Size(400,300),"listed map: png 10100 decoded from Sprite05");
            Check(Same(b,540,530,Png(163,120))&&Same(b,600,590,Png(223,180)),"png drawn so the player's pixel (200,150) sits at view origin+(80,60) = (577,560)");
            Check(Same(b,575,559,MiniRgb555(ArrowC(3)))&&Same(b,572,556,MiniRgb555(ArrowC(3)))&&Same(b,580,564,MiniRgb555(ArrowC(3)))&&!Same(b,581,559,MiniRgb555(ArrowC(3))),"heading 3 → arrow 2821, 9×9 at (572,556)");
            Check(Same(b,586,554,MiniRgb555(Party))&&Same(b,584,552,MiniRgb555(Party))&&Same(b,589,557,MiniRgb555(Party)),"party member at +10 tiles: dot 2826 centred on its png pixel (210,145)");
            Check(Same(b,507,560,Png(130,150)),"party member outside the minimap marker rect is not drawn");
            Check(Same(b,485,475,MiniRgb555(FrameC))&&Same(b,486,560,MiniRgb555(FrameC)),"frame 3275 over the png; png visible inside the hollow frame");
            Check(Same(b,499,560,MiniRgb555(Base))&&Same(b,500,560,Png(123,150)),"png clipped to the view inset by 3 (x ≥ 500)");
            foreach(int h in new[]{0,7}){
                var bh=Frame(new MapTestState(true,self with{Heading=h}));
                Check(Same(bh,575,559,MiniRgb555(ArrowC(h))),$"heading {h} → arrow {2818+h}");
            }
            var none=Frame(new MapTestState(true,self with{Heading=9,Party=[]}));
            Check(Same(none,575,559,Png(198,149)),"unknown heading: no arrow drawn");
            var moved=Frame(new MapTestState(true,self with{X=32801,Party=[]}));
            Check(Same(moved,540,530,Png(164,120)),"moving +1 tile in x moves the image by the converted pixel (201,150)");
        }

        // --- unlisted / restricted / missing png: frame only, no exception.
        {
            var b=Frame(new MapTestState(true,self with{MapId=77}));
            Check(mini.Map.Kind==MapViewKind.Dungeon&&Same(b,540,530,MiniRgb555(Base))&&Same(b,575,559,MiniRgb555(Base)),"unlisted map: base + frame only (no png, no arrow)");
            b=Frame(new MapTestState(true,self with{MapId=5001}));
            Check(mini.Map.Kind==MapViewKind.Restricted&&Same(b,540,530,MiniRgb555(Base)),"restricted map: base + frame only even though a row exists");
            b=Frame(new MapTestState(true,self with{MapId=9}));
            Check(mini.Map.Kind==MapViewKind.Field&&mini.Map.Image==null&&Same(b,540,530,MiniRgb555(Base))&&root.Faulted.Count==0,"listed map whose png is missing: frame only, panel not faulted");
            Check(UiResourceLog.Recent().Any(l=>l.Contains("10109.png")),"missing png logged once");
        }

        // --- keys: F5 is not a map key; Ctrl+M toggles Field_Map in the world only.
        {
            Frame(new MapTestState(true,self));
            Check(!input.Key(new(Keys.F5,true,false))&&!input.Key(new(Keys.F5,false,false))&&!mini.Map.FieldOpen,"F5 is not taken by the native minimap (stays a macro key for the game)");
            Check(!mini.Key(new(Keys.M,true,false),cmds)&&!field.Key(new(Keys.M,true,false),cmds),"plain M is not the map key");
            Check(input.Key(new(Keys.Control|Keys.M,true,false))&&mini.Map.FieldOpen&&field.Bounds==new Rectangle(5,250,161,121),"Ctrl+M opens Field_Map at (5,250) 161×121");
            Check(input.Key(new(Keys.Control|Keys.M,true,true))&&mini.Map.FieldOpen,"auto-repeat does not toggle again");
            Check(input.Key(new(Keys.Control|Keys.M,false,false))&&mini.Map.FieldOpen,"key-up is taken but does not toggle");
            var b=Frame(new MapTestState(true,self));
            Check(Same(b,5,250,MiniRgb555(FieldFrame))&&Same(b,165,370,MiniRgb555(FieldFrame)),"Field_Map frame 2816");
            Check(Same(b,45,280,Png(160,120))&&Same(b,7,253,Color.Black)&&Same(b,8,253,Png(123,93)),"Field_Map png view: player at (85,310), clipped to the view inset by 3");
            Check(Same(b,101,251,MiniRgb555(Title))&&Same(b,151,256,MiniRgb555(Exit))&&Same(b,134,256,MiniRgb555(Zoom)),"title 2815, exit 2801, zoom 2803 at their XML positions");
            Check(Same(b,83,309,MiniRgb555(ArrowC(3)))&&Same(b,96,306,MiniRgb555(Party)),"Field_Map arrow and party dot");
            Check(input.Key(new(Keys.Control|Keys.M,true,false))&&!mini.Map.FieldOpen&&field.Bounds==Rectangle.Empty,"Ctrl+M again closes it");
            Frame(new MapTestState(false,null));
            Check(!input.Key(new(Keys.Control|Keys.M,true,false))&&!mini.Map.FieldOpen,"outside the world Ctrl+M is not used");
            Frame(new MapTestState(true,self));
            Check(mini.Map.ToggleField()&&mini.Map.FieldOpen,"MapState.ToggleField (for the Map button, action 13) opens it");
            Frame(new MapTestState(false,null));
            Check(!mini.Map.FieldOpen,"leaving the world closes Field_Map");
        }

        // --- mouse: exit closes, zoom is taken without action, window drag, minimap swallows clicks.
        {
            Frame(new MapTestState(true,self));mini.Map.ToggleField();Frame(new MapTestState(true,self));
            var r=input.Mouse(UiMouseKind.Down,new(152,257),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(152,257),MouseButtons.Left,1);
            Check(r.Route==UiRoute.Panel&&!mini.Map.FieldOpen,"exit button 2801 closes Field_Map");
            mini.Map.ToggleField();
            r=input.Mouse(UiMouseKind.Down,new(135,257),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(135,257),MouseButtons.Left,1);
            Check(r.Route==UiRoute.Panel&&mini.Map.FieldOpen,"zoom button is taken; the large map is not part of this module");
            input.Mouse(UiMouseKind.Down,new(60,300),MouseButtons.Left,1);input.Mouse(UiMouseKind.Move,new(160,320));input.Mouse(UiMouseKind.Up,new(160,320),MouseButtons.Left);
            Check(field.Bounds==new Rectangle(105,270,161,121),"Field_Map is draggable (moved by +100,+20)");
            var b=Frame(new MapTestState(true,self));
            Check(Same(b,105,270,MiniRgb555(FieldFrame))&&Same(b,145,300,Png(160,120)),"dragged window paints at its new place");
            mini.Map.CloseField();
            r=input.Mouse(UiMouseKind.Down,new(520,520),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(520,520),MouseButtons.Left,1);
            Check(r.Route==UiRoute.Panel&&!mini.Map.FieldOpen,"a click on the minimap stays in the UI (character does not walk) and opens nothing");
            Check(input.Mouse(UiMouseKind.Down,new(5+80,250+60),MouseButtons.Left,1).Route==UiRoute.World,"closed Field_Map is not hit");
        }

        // --- unlisted map uses the Dungern_Map chrome.
        {
            Frame(new MapTestState(true,self with{MapId=77}));mini.Map.ToggleField();
            var b=Frame(new MapTestState(true,self with{MapId=77}));
            Check(field.Chrome?.Title.Surf==2888&&field.ZoomRect==Rectangle.Empty&&Same(b,105+113,271,MiniRgb555(DungeonTitle))&&!Same(b,235,277,MiniRgb555(Zoom)),"unlisted map: Dungern_Map title 2888, no zoom button, no png");
            mini.Map.CloseField();
        }

        // --- BAR_485 mutual exclusion with another member (synthetic stand-in for EX_MACRO_WINDOW).
        {
            var ex=new FakePanel("EX_MACRO_WINDOW",UiLayer.Bar,new(485,475,158,125),fill:Color.FromArgb(255,1,2,3));
            group.Add(ex.Id);ex.VisibleWhen=()=>group.IsShown(ex.Id);root.Add(ex);
            Check(group.Members.SequenceEqual(new[]{"MINI_MAP_WINDOW","EX_MACRO_WINDOW"})&&group.IsShown(MiniMapPanel.PanelId),"V5 order: minimap first, still shown");
            Check(group.Cycle()=="EX_MACRO_WINDOW"&&!mini.Visible&&root.HitTest(new(540,530))==ex,"Alt+F3 order: extended macro replaces the minimap (not hit, not visible)");
            var b=Frame(new MapTestState(true,self));
            Check(Same(b,540,530,Color.FromArgb(255,1,2,3))&&Same(b,575,559,Color.FromArgb(255,1,2,3)),"only the shown member paints in the slot");
            Check(input.Key(new(Keys.Control|Keys.M,true,false))&&mini.Map.FieldOpen,"Ctrl+M still works while the minimap slot shows another member");
            mini.Map.CloseField();
            Check(group.Cycle()==MiniMapPanel.PanelId&&mini.Visible&&root.HitTest(new(540,530))==mini,"cycle back: minimap shown and hit again");
            group.Cycle();group.Reset();
            Check(group.Current==MiniMapPanel.PanelId,"Reset (enter game) → minimap");
        }
        Check(root.Faulted.Count==0,"no native panel faulted during the minimap checks");
        mini.Map.Dispose();
    }
}
