using P06.NativeUi;
using System.Drawing.Imaging;
using System.Text.Json;

// ui-menu-buttons (Issue #467). Offline: synthetic Tile/Sprite archives under scratch/issue-467/ (no real client).
// Optional NativeUiMenuButtonsRealClient reads the real client read-only when TW_CLIENT_ROOT is set; output only to scratch/.
partial class Checks {
    static Color Rgb555(ushort v)=>Color.FromArgb(255,(v>>10&31)*255/31,(v>>5&31)*255/31,(v&31)*255/31);

    [CheckGroup("native-ui-menu-buttons",Order=1),CheckGroup(CheckGroups.Default,Order=100)]
    static void NativeUiMenuButtonsChecks(string repo){
        string dir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-467","synthetic");
        Directory.CreateDirectory(dir);foreach(var f in Directory.GetFiles(dir))File.Delete(f);

        // Same element structure as uistatus86-c.xml RIGHT_WINDOW (docs/reference/ui-layout.md §2); numbers are from the doc.
        var spec=new (string Title,int Action,int Surf,int X)[]{("Map",13,3202,2),("Mail",1,3177,21),("Status",2,3179,40),("Spell",3,3181,59),("Item",4,3183,78),("Pledge",5,3185,97),("Option",6,3187,116),("Quit",7,3189,135)};
        string Layout(string buttons)=>"<?xml version=\"1.0\"?><Status><RIGHT_WINDOW x = \"643\" y = \"476\"><MACRO_MAIN_WINDOW><MACRO_FRAME surfNumber = \"3176\" xRPos = \"0\" yRPos = \"0\"/></MACRO_MAIN_WINDOW>"
            +"<BUTTON_2STAGE_LIST><!-- <IMAGE_BUTTON title = \"Help\" action = \"0\" surfNumber = \"2301\" xRPos = \"24\" yRPos = \"91\"/> -->"+buttons+"</BUTTON_2STAGE_LIST>"
            +"<BUTTON_ONOFF_LIST><ONOFF_BUTTON title = \"MACRO_LOCK\" action = \"8\" surfNumber = \"3191\" xRPos = \"-9\" yRPos = \"37\"/></BUTTON_ONOFF_LIST></RIGHT_WINDOW></Status>";
        string good=string.Concat(spec.Select(s=>$"<IMAGE_BUTTON title = \"{s.Title}\" action = \"{s.Action}\" surfNumber = \"{s.Surf}\" xRPos = \"{s.X}\" yRPos = \"91\"/>"));
        string surfList="<?xml version=\"1.0\"?><SurfList>"+string.Concat(Enumerable.Range(3176,28).Select(i=>$"<surf id=\"{i}\" file=\"{i}\"/>"))+"</SurfList>";
        // Normal a: 17×17 colour N(i); pressed a+1: colour P(i). Option normal (3187) and Quit pressed (3190) are left out → null.
        ushort N(int i)=>(ushort)(0x7C00|(i+1)*3);ushort P(int i)=>(ushort)(0x03E0|(i+1)*3);
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        void Put(int surf,byte[] img){string name=surf+".img";items[1+SurfCatalog.NameHash(name)].Add((name,img));}
        for(int i=0;i<spec.Length;i++){
            if(spec[i].Title!="Option")Put(spec[i].Surf,Img(17,17,0,0,N(i)));
            if(spec[i].Title!="Quit")Put(spec[i].Surf+1,Img(17,17,0,0,P(i)));
        }
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        UiRoot Build(string buttons,string tag){
            string d=Path.Combine(dir,tag);Directory.CreateDirectory(d);
            var tile=Archive(d,"Tile",("SurfList.xml",Xml(surfList)),("uistatus86-c.xml",Xml(Layout(buttons))));
            var sprites=Enumerable.Range(0,17).Select(i=>Archive(d,SpriteName(i),items[i].ToArray())).ToArray();
            using var res=new UiResources(d,tile,sprites);
            return UiRoot.Build(res,UiPanelRegistry.Panels.Where(p=>p.Id==MenuButtonsPanel.PanelId).ToArray());
        }

        // --- registry: exactly one line, layout uistatus86 (UiXmlSource picks -c).
        var reg=UiPanelRegistry.Panels.Where(p=>p.Id==MenuButtonsPanel.PanelId).ToArray();
        Check(reg.Length==1&&reg[0].Layout=="uistatus86"&&MenuButtonsPanel.PanelId!="RIGHT_WINDOW","registry: one menu-buttons line on uistatus86, id distinct from the macro frame panel (RIGHT_WINDOW)");
        // Buttons (y=567) lie inside the macro frame 3176 (643,476,157×124); both are Bar layer, so the later registration paints
        // and hit-tests on top (UiRoot.PaintOrder/HitTest). Menu buttons must come after MacroPanel or the frame hides them.
        var ids=UiPanelRegistry.Panels.Select(p=>p.Id).ToList();
        Check(ids.IndexOf(MacroPanel.WindowId)>=0&&ids.IndexOf(MenuButtonsPanel.PanelId)>ids.IndexOf(MacroPanel.WindowId),"registry: menu buttons after MacroPanel (on top of the 3176 frame)");
        Check(MenuButtonsPanel.StateSurfs(3177)==(3177,3178),"V4 (b) 2-stage button: normal a, pressed a+1");

        var ui=Build(good,"good");
        var panel=(MenuButtonsPanel)ui.Find(MenuButtonsPanel.PanelId)!;
        Check(panel.Layer==UiLayer.Bar&&panel.Buttons.Count==8,"8 IMAGE_BUTTON loaded (commented-out Help ignored)");
        Check(panel.Buttons.Select(b=>b.Action.Action).SequenceEqual(new[]{13,1,2,3,4,5,6,7})&&panel.Buttons.Select(b=>b.Action.Title).SequenceEqual(spec.Select(s=>s.Title)),"actions 13,1–7 in XML order with matching titles");
        bool rects=true;
        for(int i=0;i<spec.Length;i++){
            var b=panel.Buttons[i];var loc=new Point(643+spec[i].X,476+91);
            rects&=b.Location==loc&&b.BaseSurf==spec[i].Surf;
            rects&=spec[i].Title=="Option"?b.Rect==null&&b.Normal==null:b.Rect==new Rectangle(loc,new Size(17,17));
        }
        Check(rects,"hit rectangles = (643+xRPos, 476+91) × normal surf size; x = 645/664/683/702/721/740/759/778");
        Check(panel.Bounds==new Rectangle(645,567,778+17-645,17),"panel Bounds = union of button rectangles (does not cover the macro slots)");
        Check(panel.Buttons[^1].Pressed==null&&panel.Buttons[0].Pressed!=null,"missing state surf resolves to null");

        // --- visibility follows InWorld.
        ui.Update(new UiStaticGameState(InWorld:false));
        Check(!panel.Visible&&ui.HitTest(new(650,570))==null,"hidden (and not hit) outside the world");
        ui.Update(new UiStaticGameState(InWorld:true));
        Check(panel.Visible&&ui.HitTest(new(650,570))==panel,"visible and hit in the world");

        // --- paint: normal surf per button; missing surf draws nothing.
        using var canvas=new UiCanvas();
        Color At(int x,int y)=>canvas.Bitmap.GetPixel(x,y);
        ui.Compose(canvas,null);
        bool paint=true;
        for(int i=0;i<spec.Length;i++){
            int x=643+spec[i].X,y=567;
            var want=spec[i].Title=="Option"?Color.Black:Rgb555(N(i));
            paint&=At(x,y).ToArgb()==want.ToArgb()&&At(x+16,y+16).ToArgb()==want.ToArgb();
        }
        Check(paint,"each button drawn 1:1 with its normal surf; Option (null surf) not drawn");
        Check(At(645+17,567).ToArgb()==Color.Black.ToArgb()&&At(645,566).ToArgb()==Color.Black.ToArgb()&&At(645,584).ToArgb()==Color.Black.ToArgb(),"nothing drawn outside the buttons");

        // --- clicks through UiInput: each action → existing IUiCommands.
        var cmd=new UiNullCommands();var input=new UiInput(ui,cmd);
        void Click(int x,int y){input.Mouse(UiMouseKind.Down,new(x,y),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(x,y),MouseButtons.Left);}
        for(int i=0;i<spec.Length;i++)Click(643+spec[i].X+8,567+8);
        Check(cmd.Calls.SequenceEqual(new[]{"dev-tools:小地圖","dev-tools:信件","dev-tools:角色","dev-tools:魔法","dev-tools:背包","dev-tools:血盟","logout"}),"Map/Mail/Status/Spell/Item/Pledge → dev-tools page (transitional); Option has no surf → no click; Quit → Logout: "+string.Join(",",cmd.Calls));
        cmd.Calls.Clear();
        Click(645+17,570);Click(645+18,570);
        Check(cmd.Calls.Count==0,"gap between buttons (inside Bounds) fires nothing");
        Click(645,567);Click(645+16,567+16);
        Check(cmd.Calls.SequenceEqual(new[]{"dev-tools:小地圖","dev-tools:小地圖"}),"rectangle edges inclusive (Rectangle.Contains)");
        cmd.Calls.Clear();
        input.Mouse(UiMouseKind.Down,new(650,570),MouseButtons.Right,1);input.Mouse(UiMouseKind.Up,new(650,570),MouseButtons.Right);
        Check(cmd.Calls.Count==0&&panel.Held==null,"right button does nothing");
        Check(!input.Key(new(Keys.Control|Keys.M,true,false))&&!input.Key(new(Keys.F5,true,false)),"panel takes no keys (Ctrl+M belongs to ui-minimap)");

        // --- pressed state: a+1 while held over the button, normal when dragged off, no click when released off.
        input.Mouse(UiMouseKind.Down,new(670,570),MouseButtons.Left,1);
        ui.Compose(canvas,null);
        Check(panel.Held?.Action.Title=="Mail"&&input.Captured==panel&&At(664,567).ToArgb()==Rgb555(P(1)).ToArgb(),"held Mail shows pressed surf 3178");
        input.Mouse(UiMouseKind.Move,new(700,300));ui.Compose(canvas,null);
        Check(At(664,567).ToArgb()==Rgb555(N(1)).ToArgb(),"dragged off the button → normal surf");
        input.Mouse(UiMouseKind.Move,new(670,575));ui.Compose(canvas,null);
        Check(At(664,567).ToArgb()==Rgb555(P(1)).ToArgb(),"back over the button → pressed again");
        input.Mouse(UiMouseKind.Up,new(690,575),MouseButtons.Left);ui.Compose(canvas,null);
        Check(cmd.Calls.Count==0&&panel.Held==null&&At(664,567).ToArgb()==Rgb555(N(1)).ToArgb(),"released over another button → no click, both normal");
        input.Mouse(UiMouseKind.Down,new(780,570),MouseButtons.Left,1);ui.Compose(canvas,null);
        Check(panel.Held?.Action.Title=="Quit"&&At(778,567).ToArgb()==Color.Black.ToArgb(),"held Quit with missing pressed surf draws nothing (no substitute)");
        ui.Update(new UiStaticGameState(InWorld:false));
        Check(panel.Held==null,"leaving the world drops the held button");
        input.Mouse(UiMouseKind.Up,new(780,570),MouseButtons.Left);
        Check(cmd.Calls.Count==0,"release after leaving the world does not log out");

        // --- strict loading: no invented buttons or actions.
        Throws<UiLayoutException>(()=>Build(good.Replace("action = \"7\"","action = \"99\""),"bad-action"),"unknown action refused","不明的選單 action 99");
        Throws<UiLayoutException>(()=>Build(good.Replace("action = \"6\"","action = \"5\""),"dup-action"),"duplicate action refused","重複");
        Throws<UiLayoutException>(()=>Build("","empty"),"empty BUTTON_2STAGE_LIST refused","沒有任何 IMAGE_BUTTON");
        Throws<UiLayoutException>(()=>Build(good.Replace(" xRPos = \"135\"",""),"no-x"),"missing xRPos refused","xRPos");
    }

    // Optional (TW_CLIENT_ROOT set): real uistatus86-c.xml + Sprite*.pak, read-only. PNG/JSON only under scratch/issue-467/real.
    [CheckGroup("native-ui-menu-buttons",Order=2)]
    static void NativeUiMenuButtonsRealClient(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui menu buttons real client (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-467","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        var layout=res.Xml.Load("uistatus86");
        var panel=new MenuButtonsPanel();panel.Load(layout,res.Surfs);panel.Update(new UiStaticGameState(InWorld:true));
        var expected=new (string,int,int,int)[]{("Map",13,3202,645),("Mail",1,3177,664),("Status",2,3179,683),("Spell",3,3181,702),("Item",4,3183,721),("Pledge",5,3185,740),("Option",6,3187,759),("Quit",7,3189,778)};
        Check(panel.Buttons.Select(b=>(b.Action.Title,b.Action.Action,b.BaseSurf,b.Location.X)).SequenceEqual(expected)&&panel.Buttons.All(b=>b.Location.Y==567&&b.Rect!=null&&b.Pressed!=null),"real uistatus86-c.xml: 8 buttons at y=567, x=645…778, surfs 3202/3177–3189 and their a+1 all present");
        using var canvas=new UiCanvas();canvas.Clear(Color.Black);
        // Test-side only: the macro frame belongs to ui-macro-bar; drawn here so the buttons can be compared in place.
        var frame=layout.Root.Child("RIGHT_WINDOW/MACRO_MAIN_WINDOW/MACRO_FRAME");
        canvas.DrawSurface(res.Surfs.Get(frame.Surfs()[0]),643+frame.Int("xRPos"),476+frame.Int("yRPos"));
        canvas.Restrict(panel.Bounds);panel.Paint(canvas);canvas.Release();
        canvas.Bitmap.Save(Path.Combine(outDir,"menu-buttons-800x600.png"),ImageFormat.Png);
        using(var crop=canvas.Bitmap.Clone(new Rectangle(643,476,157,124),PixelFormat.Format32bppPArgb))crop.Save(Path.Combine(outDir,"right-window-crop.png"),ImageFormat.Png);
        File.WriteAllText(Path.Combine(outDir,"menu-buttons.json"),JsonSerializer.Serialize(new{xml=layout.FileName,bounds=panel.Bounds,buttons=panel.Buttons.Select(b=>new{b.Action.Title,b.Action.Action,b.BaseSurf,rect=b.Rect,normal=b.Normal?.FileName,normalArchive=b.Normal?.Archive,pressed=b.Pressed?.FileName,b.Action.DevToolsPage,b.Action.Logout})},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("native-ui menu buttons real client → "+outDir);
    }
}
