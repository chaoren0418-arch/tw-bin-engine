using P06;
using P06.NativeUi;
using System.Drawing.Imaging;
using System.Text.Json;

// ui-left-status (Issue #464). Offline: synthetic XML/.img under scratch/issue-464/synthetic (never the real client).
// The optional NativeUiLeftStatusRealRender reads the real client only when TW_CLIENT_ROOT is set and writes PNGs to scratch/.
partial class Checks {
    // Relative coordinates of the TW uistatus86-c.xml LEFT_WINDOW (docs/reference/ui-layout.md §2). The panel must read them
    // from the XML at run time; the fixture below is the only place they appear, and a second origin proves nothing is hard-coded.
    static readonly (string Path,int X,int Y,string? Surf)[] LeftFixture=[
        ("MAIN_WINDOW/MAIN_FRAME",0,0,"3168"),
        ("LEVEL_WINDOW/LEVEL_BAR",25,26,"3169"),("LEVEL_WINDOW/LEVEL_NUMBER",43,29,null),("LEVEL_WINDOW/EXP_PERCENT_A",78,29,null),("LEVEL_WINDOW/EXP_PERCENT_B",79,29,null),
        ("LEVEL_WINDOW/LEVEL_BTN",8,23,"5726~5728"),
        ("AC_WINDOW/AC_NUMBER",41,69,null),
        ("ALIGN_WINDOW/CHAOTIC_ICON",10,86,"3170"),("ALIGN_WINDOW/LAWFUL_ICON",10,86,"3171"),("ALIGN_WINDOW/NEUTRAL_ICON",10,86,"3172"),("ALIGN_WINDOW/ALIGN_NUMBER",41,89,null),
        ("CARRY_GAUGE_WINDOW/LOW_GAUGE_BAR",24,107,"3173"),("CARRY_GAUGE_WINDOW/MID_GAUGE_BAR",24,107,"3174"),("CARRY_GAUGE_WINDOW/HIGH_GAUGE_BAR",24,107,"3175"),("CARRY_GAUGE_WINDOW/CARRY_GAUGE_NUMBER",41,109,null),
        ("MR_WINDOW/MR_PERCENTAGE",105,69,null),("SP_WINDOW/SP_NUMBER",105,89,null),
        ("FOOD_GAUGE_WINDOW/FOOD_LOW_GAUGE_BAR",87,107,"3175"),("FOOD_GAUGE_WINDOW/FOOD_GUAGE_BAR",87,107,"3173"),("FOOD_GAUGE_WINDOW/FOOD_MAX_BAR",87,107,"3174"),("FOOD_GAUGE_WINDOW/FOOD_NUMBER",105,109,null),
        ("ADENA_WINDOW/ADENA_BTN",8,42,"3701~3703"),("ADENA_WINDOW/ADENA_VALUE",115,48,null),
    ];
    static string LeftXml(int x,int y,string? omit=null){
        var sb=new System.Text.StringBuilder("<?xml version=\"1.0\"?>\n<MainStatusConfigulation>\n");
        sb.Append($"  <LEFT_WINDOW x = \"{x}\" y = \"{y}\">\n");
        foreach(var g in LeftFixture.Where(f=>f.Path!=omit).GroupBy(f=>f.Path.Split('/')[0])){
            sb.Append($"    <{g.Key}>\n");
            foreach(var f in g)sb.Append($"      <{f.Path.Split('/')[1]}{(f.Surf!=null?$" surfNumber = \"{f.Surf}\"":"")} xRPos = \"{f.X}\" yRPos = \"{f.Y}\"/>\n");
            sb.Append($"    </{g.Key}>\n");
        }
        sb.Append("  </LEFT_WINDOW>\n  <CENTER_WINDOW x=\"129\" y=\"470\"/>\n</MainStatusConfigulation>\n");
        return sb.ToString();
    }
    // Synthetic surfaces: distinct sizes/colours so every pixel probe identifies its source.
    const ushort LsFrame=0x0010,LsLevel=0x03E0,LsChaotic=0x7C00,LsLawful=0x001F,LsNeutral=0x7FFF,LsLow=0x03FF,LsMid=0x7FE0,LsHigh=0x7C1F;
    static readonly Dictionary<int,(int W,int H,ushort C)> LeftSurfs=new(){
        [3168]=(135,125,LsFrame),[3169]=(101,11,LsLevel),[3170]=(16,12,LsChaotic),[3171]=(16,12,LsLawful),[3172]=(16,12,LsNeutral),
        [3173]=(40,5,LsLow),[3174]=(40,5,LsMid),[3175]=(40,5,LsHigh),
    };
    static UiResources LeftResources(string dir,string xml,int[]? noImage=null,int[]? noFileAttr=null){
        if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        var rows=string.Concat(LeftSurfs.Keys.Select(id=>noFileAttr?.Contains(id)==true?$"<surf id=\"{id}\"/>":$"<surf id=\"{id}\" file=\"{id}\"/>"));
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml($"<?xml version=\"1.0\"?><SurfList>{rows}</SurfList>")),("uistatus86-c.xml",Xml(xml)));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (id,(w,h,c)) in LeftSurfs){if(noImage?.Contains(id)==true)continue;string n=$"{id}.img";items[1+SurfCatalog.NameHash(n)].Add((n,Img(w,h,0,0,c)));}
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        return new UiResources(dir,tilePin,spritePins);
    }
    static IUiGameState LeftState(LeftStatusState s)=>new UiLeftStatusGameState(new UiStaticGameState(InWorld:true,Level:s.Level),s);
    static readonly LeftStatusState LeftSample=new(Level:52,ExpPercent:50m,Ac:-37,Mr:45,Lawful:600,Sp:12,Weight:120,Food:112,Adena:1234567);

    static void NativeUiLeftStatusChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-464","synthetic");Directory.CreateDirectory(root);
        int Px(UiCanvas c,int x,int y)=>c.Bitmap.GetPixel(x,y).ToArgb();
        int Argb(ushort p)=>unchecked((int)UiImage.ToArgb(p));
        var reg=UiPanelRegistry.Panels.Single(p=>p.Id=="LEFT_WINDOW");
        Check(reg.Layout=="uistatus86","registry: LEFT_WINDOW reads uistatus86 (-c first)");

        using(var res=LeftResources(Path.Combine(root,"tw"),LeftXml(0,475))){
            var built=UiRoot.Build(res,[reg]);
            var panel=(LeftStatusPanel)built.Find("LEFT_WINDOW")!;
            Check(panel.Layer==UiLayer.Bar&&panel.Origin==new Point(0,475)&&panel.Bounds==new Rectangle(0,475,135,125),"LEFT_WINDOW origin (0,475) from XML; Bounds = frame 3168 size");
            // Every component = XML relative coordinate + (0,475).
            int compared=0;bool all=true;
            foreach(var f in LeftFixture){
                if(!panel.Components.TryGetValue(f.Path,out var at))continue;
                compared++;bool ok=at==new Point(f.X,f.Y+475);all&=ok;
                if(!ok)Console.Error.WriteLine($"DIAG {f.Path} at {at}, want ({f.X},{f.Y+475})");
            }
            Check(all&&compared==20&&panel.Components.Count==20,$"all {compared} components = XML (xRPos,yRPos)+(0,475)");

            // Not in world: invisible, nothing painted.
            built.Update(new UiStaticGameState());
            using(var c=new UiCanvas()){built.Compose(c,null);Check(!panel.Visible&&Px(c,1,476)==Color.Black.ToArgb(),"not in world: LEFT_WINDOW hidden");}

            // In world, nothing known: frame only, every value blank (never estimated).
            built.Update(LeftState(LeftStatusState.Empty));
            using(var c=new UiCanvas()){
                built.Compose(c,null);
                Check(panel.Drawn.Count==1&&panel.Drawn[0].Element==LeftStatusPanel.Frame&&Px(c,1,476)==Argb(LsFrame),"no server values: frame only, no bars, no icon, no text");
            }
            // Fallback adapter: a plain IUiGameState only gives the level.
            built.Update(new UiStaticGameState(InWorld:true,Level:7));
            using(var c=new UiCanvas()){
                built.Compose(c,null);
                Check(panel.Drawn.Count(d=>d.Text!=null)==1&&panel.Drawn.Single(d=>d.Text!=null) is {Element:LeftStatusPanel.LevelNumber,Text:"7"},"plain game state: only the level is shown");
            }
            // Client bridge order (#465 merge): WithHpMpState wraps the left-status state from outside; the snapshot must still come through.
            {
                var snap=LeftStatusState.Empty with{Level=9,Ac=-3,Adena=42};
                var wrapped=new UiHpMpGameState(LeftState(snap),[]);
                Check(LeftStatusState.From(wrapped)==snap&&LeftStatusState.From(new UiHpMpGameState(new UiStaticGameState(InWorld:true,Level:4),[])) is {Level:4,Ac:null,Adena:null},"left-status snapshot reads through UiHpMpGameState (bridge: hpmp outside, left-status inside)");
            }

            // Sample state: every component at its logical position.
            built.Update(LeftState(LeftSample));
            using(var c=new UiCanvas()){
                built.Compose(c,null);
                LeftStatusDraw D(string e)=>panel.Drawn.Single(d=>d.Element==e);
                Check(D(LeftStatusPanel.LevelBar).Rect==new Rectangle(25,501,50,11)&&Px(c,25,501)==Argb(LsLevel)&&Px(c,74,501)==Argb(LsLevel)&&Px(c,75,501)==Argb(LsFrame),"level bar 3169 at (25,501), cropped to 50% of 101 = 50 px");
                var texts=new (string E,string T,int X,int Y)[]{
                    (LeftStatusPanel.LevelNumber,"52",43,504),(LeftStatusPanel.ExpPercent,"50.00%",78,504),(LeftStatusPanel.AcNumber,"-37",41,544),
                    (LeftStatusPanel.MrPercent,"45%",105,544),(LeftStatusPanel.AlignNumber,"600",41,564),(LeftStatusPanel.SpNumber,"12",105,564),
                    (LeftStatusPanel.CarryNumber,"50%",41,584),(LeftStatusPanel.FoodNumber,"49%",105,584)};
                bool tOk=true;foreach(var t in texts){var d=D(t.E);bool ok=d.Text==t.T&&d.Rect.X==t.X&&d.Rect.Y==t.Y;tOk&=ok;if(!ok)Console.Error.WriteLine($"DIAG text {t.E}: {d}");}
                Check(tOk,"level/EXP%/AC/MR%/alignment/SP/weight%/food% text at XML point + (0,475)");
                var adena=D(LeftStatusPanel.AdenaValue);
                Check(adena.Text=="1234567"&&adena.Rect.Right==115&&adena.Rect.Y==523,"adena value ends at ADENA_VALUE x=115, y=48+475");
                bool lit=false;var r=D(LeftStatusPanel.LevelNumber).Rect;for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++)lit|=Px(c,x,y)==Color.White.ToArgb();
                Check(lit,"level text pixels painted");
                Check(D(LeftStatusPanel.CarryMid).Rect==new Rectangle(24,582,20,5)&&Px(c,43,582)==Argb(LsMid)&&Px(c,44,582)==Argb(LsFrame),"weight bar at (24,582) cropped to 120/240 of 40 = 20 px");
                Check(D(LeftStatusPanel.FoodNormal).Rect==new Rectangle(87,582,19,5)&&Px(c,87,582)==Argb(LsLow),"food bar FOOD_GUAGE_BAR (3173) at (87,582), 112/225 of 40 = 19 px");
            }

            // Three alignments → three icon surfs, all at (10, 561).
            foreach(var (lawful,element,color,label) in new[]{(600,LeftStatusPanel.LawfulIcon,LsLawful,"lawful 600"),(500,LeftStatusPanel.LawfulIcon,LsLawful,"lawful 500"),
                (499,LeftStatusPanel.NeutralIcon,LsNeutral,"neutral 499"),(0,LeftStatusPanel.NeutralIcon,LsNeutral,"neutral 0"),(-1,LeftStatusPanel.ChaoticIcon,LsChaotic,"chaotic -1"),(-32768,LeftStatusPanel.ChaoticIcon,LsChaotic,"chaotic -32768")}){
                built.Update(LeftState(LeftSample with{Lawful=lawful}));
                using var c=new UiCanvas();built.Compose(c,null);
                var icons=panel.Drawn.Where(d=>d.Element.StartsWith("ALIGN_WINDOW/",StringComparison.Ordinal)&&d.Surf!=null).ToList();
                Check(icons.Count==1&&icons[0].Element==element&&icons[0].Rect==new Rectangle(10,561,16,12)&&Px(c,10,561)==Argb(color)&&Px(c,25,572)==Argb(color),$"alignment {label} → {element} only, at (10,561)");
            }
            // Three weight bands → three gauge surfs at (24, 582).
            foreach(var (weight,element,color,width) in new[]{(1,LeftStatusPanel.CarryLow,LsLow,0),(6,LeftStatusPanel.CarryLow,LsLow,1),(119,LeftStatusPanel.CarryLow,LsLow,19),(120,LeftStatusPanel.CarryMid,LsMid,20),(239,LeftStatusPanel.CarryMid,LsMid,39),(240,LeftStatusPanel.CarryHigh,LsHigh,40)}){
                built.Update(LeftState(LeftSample with{Weight=weight}));
                using var c=new UiCanvas();built.Compose(c,null);
                var bars=panel.Drawn.Where(d=>d.Element.StartsWith("CARRY_GAUGE_WINDOW/",StringComparison.Ordinal)&&d.Surf!=null).ToList();
                bool ok=width==0?bars.Count==0&&Px(c,24,582)==Argb(LsFrame):bars.Count==1&&bars[0].Element==element&&bars[0].Rect==new Rectangle(24,582,width,5)&&Px(c,24,582)==Argb(color)&&Px(c,24+width,582)==Argb(LsFrame);
                Check(ok,$"weight {weight}/240 → {(width==0?"nothing (0 px)":element+" "+width+" px")}");
            }
            // Food bands: low / normal / max.
            // FoodLow (<3) always crops to 0 px of a 40 px bar, so the LOW surf is chosen but never visible: checked on the rule only.
            Check(LeftStatusState.FoodBandOf(0)==UiGaugeBand.Low&&LeftStatusState.FoodBandOf(2)==UiGaugeBand.Low&&LeftStatusState.FoodBandOf(3)==UiGaugeBand.Mid&&LeftStatusState.FoodBandOf(225)==UiGaugeBand.High,"food band rule: <3 low, 3–224 normal, ≥225 max");
            built.Update(LeftState(LeftSample with{Food=2}));
            using(var c=new UiCanvas()){built.Compose(c,null);Check(!panel.Drawn.Any(d=>d.Element.StartsWith("FOOD_GAUGE_WINDOW/",StringComparison.Ordinal)&&d.Surf!=null)&&panel.Drawn.Any(d=>d.Element==LeftStatusPanel.FoodNumber&&d.Text=="0%"),"food 2: 0 px bar (nothing drawn), number 0%");}
            foreach(var (food,element,color) in new[]{(12,LeftStatusPanel.FoodNormal,LsLow),(224,LeftStatusPanel.FoodNormal,LsLow),(225,LeftStatusPanel.FoodMax,LsMid),(255,LeftStatusPanel.FoodMax,LsMid)}){
                built.Update(LeftState(LeftSample with{Food=food}));
                using var c=new UiCanvas();built.Compose(c,null);
                var bars=panel.Drawn.Where(d=>d.Element.StartsWith("FOOD_GAUGE_WINDOW/",StringComparison.Ordinal)&&d.Surf!=null).ToList();
                Check(bars.Count==1&&bars[0].Element==element&&Px(c,87,582)==Argb(color),$"food {food} → {element}");
            }
            Check(panel.Mouse(new UiMouseEvent(UiMouseKind.Down,new(5,480),MouseButtons.Left,1,0,Keys.None),new UiNullCommands())==false&&built.HitTest(new(5,480))==panel,"click on the box hits the panel (world does not get it); no button behaviour yet");
        }

        // A different origin: everything follows the XML (nothing hard-coded to (0,475)).
        using(var res=LeftResources(Path.Combine(root,"moved"),LeftXml(30,100))){
            var panel=(LeftStatusPanel)UiRoot.Build(res,[reg]).Find("LEFT_WINDOW")!;
            Check(panel.Bounds==new Rectangle(30,100,135,125)&&panel.Components[LeftStatusPanel.CarryHigh]==new Point(54,207)&&panel.Components[LeftStatusPanel.AdenaValue]==new Point(145,148),"origin (30,100) in XML moves every component");
        }

        // Null surfs: the missing component is not drawn; nothing replaces it.
        using(var res=LeftResources(Path.Combine(root,"null-surf"),LeftXml(0,475),noImage:[3169,3174],noFileAttr:[3171])){
            var built=UiRoot.Build(res,[reg]);var panel=(LeftStatusPanel)built.Find("LEFT_WINDOW")!;
            built.Update(LeftState(LeftSample));
            using var c=new UiCanvas();built.Compose(c,null);
            Check(!panel.Drawn.Any(d=>d.Element is LeftStatusPanel.LevelBar or LeftStatusPanel.LawfulIcon or LeftStatusPanel.CarryMid)&&Px(c,25,501)==Argb(LsFrame)&&Px(c,10,561)==Argb(LsFrame)&&Px(c,24,582)==Argb(LsFrame),
                "missing 3169 image, 3171 without file, missing 3174 image → those components not drawn, frame shows through");
            Check(panel.Drawn.Any(d=>d.Element==LeftStatusPanel.LevelNumber)&&panel.Drawn.Any(d=>d.Element==LeftStatusPanel.FoodNormal),"other components still drawn");
        }
        using(var res=LeftResources(Path.Combine(root,"no-frame"),LeftXml(0,475),noImage:[3168])){
            var built=UiRoot.Build(res,[reg]);var panel=(LeftStatusPanel)built.Find("LEFT_WINDOW")!;
            Check(panel.Bounds==new Rectangle(10,501,117,86),"missing frame: Bounds = union of the component surfs that exist (level bar … gauges), nothing invented");
            built.Update(LeftState(LeftSample));
            using var c=new UiCanvas();built.Compose(c,null);
            Check(!panel.Drawn.Any(d=>d.Element==LeftStatusPanel.Frame)&&Px(c,60,540)==Color.Black.ToArgb()&&Px(c,25,501)==Argb(LsLevel),"missing frame: no placeholder background, bar still drawn");
        }
        // Missing node → UiLayoutException (whole native UI refused, never default coordinates).
        using(var res=LeftResources(Path.Combine(root,"no-node"),LeftXml(0,475,omit:"ALIGN_WINDOW/NEUTRAL_ICON"))){
            Throws<UiLayoutException>(()=>UiRoot.Build(res,[reg]),"missing NEUTRAL_ICON node refuses the panel","NEUTRAL_ICON");
        }

        // State adapter rules and formats.
        Check(LeftStatusState.Empty.LevelText+LeftStatusState.Empty.ExpText+LeftStatusState.Empty.AcText+LeftStatusState.Empty.MrText+LeftStatusState.Empty.LawfulText+LeftStatusState.Empty.SpText+LeftStatusState.Empty.WeightText+LeftStatusState.Empty.FoodText+LeftStatusState.Empty.AdenaText=="","unknown values format as blank");
        Check(new LeftStatusState(1,99.999m,null,null,null,null,239,225,0).ExpText=="99.99%"&&new LeftStatusState(1,99.999m,null,null,null,null,239,225,0) is {WeightText:"99%",FoodText:"100%",AdenaText:"0"},"EXP% truncates (never rounds up); weight/food percent floor; 0 adena is a value");
        Check(LeftStatusState.Fill(101,null,100)==0&&LeftStatusState.Fill(101,150m,100)==101&&LeftStatusState.Fill(40,-5m,240)==0,"gauge fill: null → 0, clamped");
        Check(LeftStatusState.IsAdena(new(1,0,0,0,5,1,10,0,"$4 (10)","",""))&&LeftStatusState.IsAdena(new(1,0,0,0,5,1,1,0,"$4","",""))&&!LeftStatusState.IsAdena(new(1,0,0,0,6,1,1,0,"$4","",""))&&!LeftStatusState.IsAdena(new(1,1,0,0,5,1,1,0,"$4","","")),"adena = $4 with DescriptionId 0 / IconId 5");
    }

    /// <summary>Optional: real client (TW_CLIENT_ROOT). Writes PNGs + the component table to scratch/issue-464/real only.</summary>
    static void NativeUiLeftStatusRealRender(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui left-status real render (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-464","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        var layout=res.Xml.Load("uistatus86");
        var panel=new LeftStatusPanel();panel.Load(layout,res.Surfs);
        var root=new UiRoot(res);root.Add(panel);
        var cases=new (string Name,LeftStatusState State)[]{
            ("empty",LeftStatusState.Empty),
            ("sample-lawful-low",LeftSample with{Lawful=1200,Weight=60,Food=225}),
            ("sample-neutral-mid",LeftSample),
            ("sample-chaotic-high",LeftSample with{Lawful=-2000,Weight=240,Food=2,ExpPercent=99.99m}),
        };
        var surfaces=new List<object>();
        foreach(var id in new[]{3168,3169,3170,3171,3172,3173,3174,3175})if(res.Surfs.Get(id) is {} s){surfaces.Add(new{id,s.FileName,s.Archive,s.Width,s.Height,s.Flags});s.Bitmap.Save(Path.Combine(outDir,$"surf-{id}.png"),ImageFormat.Png);}
        var draws=new Dictionary<string,object>();
        foreach(var (name,state) in cases){
            root.Update(LeftState(state));
            using var c=new UiCanvas();root.Compose(c,null);
            c.Bitmap.Save(Path.Combine(outDir,$"canvas-{name}.png"),ImageFormat.Png);
            using(var crop=c.Bitmap.Clone(new Rectangle(0,470,140,130),PixelFormat.Format32bppPArgb))using(var big=new Bitmap(560,520)){
                using(var g=Graphics.FromImage(big)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;g.DrawImage(crop,new Rectangle(0,0,560,520));}
                big.Save(Path.Combine(outDir,$"left-x4-{name}.png"),ImageFormat.Png);
            }
            draws[name]=panel.Drawn.Select(d=>new{d.Element,d.Surf,rect=new[]{d.Rect.X,d.Rect.Y,d.Rect.Width,d.Rect.Height},d.Text}).ToArray();
        }
        Check(panel.Origin==new Point(0,475)&&panel.Bounds==new Rectangle(0,475,135,125),"real uistatus86-c LEFT_WINDOW at (0,475), frame 135×125");
        File.WriteAllText(Path.Combine(outDir,"left-status.json"),JsonSerializer.Serialize(new{client,xml=layout.FileName,origin=new[]{panel.Origin.X,panel.Origin.Y},bounds=new[]{panel.Bounds.X,panel.Bounds.Y,panel.Bounds.Width,panel.Bounds.Height},
            components=panel.Components.ToDictionary(k=>k.Key,k=>new[]{k.Value.X,k.Value.Y}),surfaces,draws},new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
    }
}
