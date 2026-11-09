using P06.NativeUi;
using System.Drawing.Imaging;
using System.Text.Json;

// ui-hpmp (Issue #465). Offline: every archive and .img is synthesized under scratch/issue-465/; numbers are the V1 tables
// (docs/reference/ui-layout.md §2 V1). The real client is only read by NativeUiHpMpRealClientChecks when TW_CLIENT_ROOT is set.
partial class Checks {
    // Synthetic RGB555 colours.
    const ushort HmFrame=0x03E0,HmHp=0x7C00,HmMp=0x001F,HmButton=0x7FE0,HmButtonDown=0x7FFF,HmWarn=0x7C1F,HmBase=0x5294;
    static int HmArgb(ushort p)=>unchecked((int)UiImage.ToArgb(p));

    static UiResources HpMpSyntheticResources(string dir,IReadOnlyDictionary<int,(int W,int H,ushort Px)> imgs){
        if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        string surfList="<?xml version=\"1.0\"?><SurfList>"+string.Concat(imgs.Keys.Select(id=>$"<surf id=\"{id}\" file=\"{id}\"/>"))+"</SurfList>";
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)),("uistatus86-c.xml",Xml("<?xml version=\"1.0\"?><Status><LEFT_WINDOW x=\"0\" y=\"475\" surfNumber=\"3168\"/></Status>")));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (id,(w,h,px)) in imgs){string name=id+".img";items[1+SurfCatalog.NameHash(name)].Add((name,Img(w,h,0,0,px)));}
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        return new UiResources(dir,tilePin,spritePins);
    }

    [CheckGroup("native-ui-hpmp",Order=1),CheckGroup(CheckGroups.Default,Order=60)]
    static void NativeUiHpMpChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-465","synthetic");
        Directory.CreateDirectory(root);

        // --- V1 layout tables.
        {
            var d=HpMpLayout.For(1,2,false);
            Check(d.Frame==2355&&d.FrameSize==new Size(378,48)&&d.Hp==2365&&d.HpBar==new Rectangle(9,11,141,20)&&d.Mp==2358&&d.MpBar==new Rectangle(183,11,136,20),"V1 default (horizontal, long, normal): frame 2355 378×48, HP 2365 (9,11) 141×20, MP 2358 (183,11) 136×20");
            Check(d.ShapeButton==2368&&d.ShapeButtonRect==new Rectangle(135,3,22,7)&&d.LengthButton==2371&&d.LengthButtonRect==new Rectangle(177,3,22,7)&&d.DragArea==Rectangle.FromLTRB(152,0,182,30),"V1 default buttons 2368 (135,3) / 2371 (177,3) 22×7, drag strip (152,0)–(182,30)");
            Check(d.Warn==2937&&d.WarnAt==new Point(6,8),"V1 low-HP frame horizontal normal 2935+s at (+6,+8)");
            var h0=HpMpLayout.For(1,0,false);var h1t=HpMpLayout.For(1,1,true);
            Check(h0.Frame==2353&&h0.FrameSize.Width==265&&h0.Hp==2359&&h0.HpBar.Width==85&&h0.Mp==2356&&h0.MpBar==new Rectangle(133,11,80,20)&&h0.ShapeButtonRect.X==85&&h0.LengthButtonRect.X==127,"V1 horizontal status 0");
            Check(h1t.Frame==2909&&h1t.FrameSize==new Size(282,34)&&h1t.Hp==2917&&h1t.HpBar==new Rectangle(9,11,116,14)&&h1t.Mp==2912&&h1t.MpBar==new Rectangle(157,11,116,14)&&h1t.Warn==2942,"V1 horizontal status 1 thick");
            var v2=HpMpLayout.For(0,2,false);var v0t=HpMpLayout.For(0,0,true);
            Check(v2.Frame==2334&&v2.FrameSize==new Size(263,48)&&v2.Hp==2344&&v2.HpBar==new Rectangle(30,9,174,15)&&v2.Mp==2337&&v2.MpBar==new Rectangle(30,22,174,15)&&v2.ShapeButton==2347&&v2.ShapeButtonRect==new Rectangle(19,0,21,8)&&v2.LengthButton==2350&&v2.LengthButtonRect==new Rectangle(36,0,26,8)&&v2.DragArea==Rectangle.FromLTRB(0,5,33,38)&&v2.Warn==2934&&v2.WarnAt==new Point(27,6),"V1 vertical status 2 normal");
            Check(v0t.Frame==2890&&v0t.FrameSize==new Size(141,46)&&v0t.Hp==2896&&v0t.HpBar==new Rectangle(30,9,102,14)&&v0t.Mp==2893&&v0t.MpBar==new Rectangle(30,26,102,14)&&v0t.ShapeButton==2905&&v0t.DragArea==Rectangle.FromLTRB(0,9,33,42)&&v0t.Warn==2938,"V1 vertical status 0 thick");
            bool inside=true;
            foreach(int shape in new[]{0,1})foreach(int s in new[]{0,1,2})foreach(bool t in new[]{false,true}){
                var l=HpMpLayout.For(shape,s,t);var f=new Rectangle(Point.Empty,l.FrameSize);
                inside&=f.Contains(l.HpBar)&&f.Contains(l.MpBar)&&f.Contains(l.ShapeButtonRect)&&f.Contains(l.LengthButtonRect)&&(shape==0||!l.HpBar.IntersectsWith(l.MpBar));
            }
            Check(inside,"all 12 layouts: bars and buttons inside the frame; horizontal HP/MP do not overlap");
            Check(Rectangle.Intersect(HpMpLayout.For(0,1,false).HpBar,HpMpLayout.For(0,1,false).MpBar).Height==2&&!HpMpLayout.For(0,1,true).HpBar.IntersectsWith(HpMpLayout.For(0,1,true).MpBar),"V1 table as recorded: vertical normal HP (y 9, h 15) and MP (y 22) share 2 rows; thick (y 26) does not");
        }

        // --- settings (ClientConfig #10/#11 defaults, load rule).
        {
            var s=HpMpSettings.Default;
            Check(s.ShowCharacterHpMp&&s.Show&&s.Status==2&&s.Shape==1&&!s.Thickness&&s.ShowRecovery&&s.ShowPercent,"descriptor defaults: showCharacterHPMP, show, Status2, shape 1, thin, recovery, percent");
            Check(s.InitialPosition()==new Point(135,465),"(−1,−1) → (135, 465)");
            Check((s with{PositionX=900,PositionY=600}).InitialPosition()==new Point(100,50),"stored position x % 800, y % 550");
            Check((s with{Status=7,Shape=5}).Normalized() is {Status:2,Shape:1},"out-of-range status/shape normalized");
        }

        // --- panel geometry, buttons, drag.
        {
            var p=new HpMpPanel();
            Check(p.Id=="HpMpWindow"&&p.Layer==UiLayer.Float&&p.Bounds==new Rectangle(135,465,378,48),"panel default bounds (135,465) 378×48 on the float layer");
            Check(!p.Visible,"not visible before entering the world");
            p.Update(new UiStaticGameState(true,"t",1,10,20,5,10,0));Check(p.Visible,"visible in world when FloatingHPMP.show");
            var hidden=new HpMpPanel(HpMpSettings.Default with{Show=false});hidden.Update(new UiStaticGameState(true));Check(!hidden.Visible,"FloatingHPMP.show=false hides the window");
            var right=new HpMpPanel(HpMpSettings.Default with{PositionX=700,PositionY=100});
            Check(right.Bounds.Location==new Point(799-378,100),"re-layout: x + frame width > 799 → x = 799 − width");
            var cmd=new UiNullCommands();
            Point shapeBtn=new(135+136,465+4),lengthBtn=new(135+178,465+4),drag=new(135+160,465+20);
            Check(p.ButtonAt(shapeBtn)==HpMpPanel.Button.Shape&&p.ButtonAt(lengthBtn)==HpMpPanel.Button.Length&&p.ButtonAt(drag)==HpMpPanel.Button.None,"button hit rectangles");
            Check(p.IsDragHandle(drag)&&!p.IsDragHandle(shapeBtn)&&!p.IsDragHandle(new(135+20,465+40)),"drag strip only (buttons and bars are not drag handles)");
            UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
            p.Mouse(M(UiMouseKind.Down,lengthBtn),cmd);Check(p.Pressed==HpMpPanel.Button.Length,"press shows the pressed state");
            p.Mouse(M(UiMouseKind.Up,lengthBtn),cmd);
            Check(p.Settings.Status==0&&p.Bounds==new Rectangle(135,465,265,48)&&p.Pressed==HpMpPanel.Button.None,"length button: status 2 → 0, frame 265×48");
            p.CycleLength();Check(p.Settings.Status==1,"status 0 → 1");p.CycleLength();Check(p.Settings.Status==2,"status 1 → 2 (cycle of 3)");
            p.Mouse(M(UiMouseKind.Down,shapeBtn),cmd);p.Mouse(M(UiMouseKind.Up,new(10,10)),cmd);
            Check(p.Settings.Shape==1,"release outside the button does not click");
            p.Mouse(M(UiMouseKind.Down,shapeBtn),cmd);p.Mouse(M(UiMouseKind.Up,shapeBtn),cmd);
            Check(p.Settings.Shape==0&&p.Bounds==new Rectangle(135,465,263,48)&&p.Layout.Frame==2334,"shape button: horizontal → vertical (2334 263×48)");
            p.ToggleShape();Check(p.Settings.Shape==1&&p.Layout.Frame==2355,"shape toggles back");
            p.Apply(p.Settings with{Thickness=true});Check(p.Layout.Frame==2910&&p.Bounds.Size==new Size(334,34),"thickness switches to the thick set (2910 334×34)");
            p.Apply(p.Settings with{Thickness=false});
            p.MoveTo(new(500,580));Check(p.Bounds.Location==new Point(799-378,550)&&p.Settings.PositionX==421&&p.Settings.PositionY==550,"drag: y ≤ 550, x ≤ 799 − width, position kept in settings");
            Check(cmd.Calls.Count==0,"buttons only change the window (no commands, no packets)");

            // Through UiRoot/UiInput: drag on the strip moves the window; the head-bar panel never takes world clicks.
            var ui=new UiRoot();var win=new HpMpPanel();var heads=new HpMpHeadBarPanel(()=>win.Settings);ui.Add(heads);ui.Add(win);
            var st=new UiHpMpGameState(new UiStaticGameState(true,"t",1,50,100,20,40,0),[]);ui.Update(st);
            var input=new UiInput(ui,cmd);
            Check(input.Mouse(UiMouseKind.Down,new(400,100),MouseButtons.Left,1).Route==UiRoute.World,"head-bar panel (world layer) does not swallow world clicks");
            Check(ui.HitTest(new(135+160,465+20))==win,"window is hit on the float layer");
            input.Mouse(UiMouseKind.Down,new(135+160,465+20),MouseButtons.Left,1);Check(input.Dragging,"drag starts on the strip");
            input.Mouse(UiMouseKind.Move,new(135+160-100,465+20-200));input.Mouse(UiMouseKind.Up,new(135+160-100,465+20-200));
            Check(win.Bounds.Location==new Point(35,265)&&!input.Dragging,"dragged by (−100, −200) in logical pixels");
            input.Mouse(UiMouseKind.Down,new(35+178,265+4),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(35+178,265+4),MouseButtons.Left,1);
            Check(win.Settings.Status==0,"length button through UiInput (capture → release)");
        }

        // --- value text, percent rules, fill widths (server values only).
        {
            Check(HpMpPanel.FillWidth(141,50,100)==70&&HpMpPanel.FillWidth(141,100,100)==141&&HpMpPanel.FillWidth(141,0,100)==0&&HpMpPanel.FillWidth(141,150,100)==141&&HpMpPanel.FillWidth(141,-5,100)==0,"fill width = w × value / max, clamped");
            Check(HpMpPanel.FillWidth(141,null,100)==null&&HpMpPanel.FillWidth(141,5,null)==null&&HpMpPanel.FillWidth(141,5,0)==null,"no server value → no fill (never estimated)");
            Check(HpMpPanel.ValueText(37,120)=="37/120"&&HpMpPanel.ValueText(null,120)==null,"\"%d/%d\" text; blank when not sent");
            Check(HpMpPanel.PercentText(1,3)=="33%"&&HpMpPanel.PercentText(99,100)=="99%"&&HpMpPanel.PercentText(100,100)=="100%"&&HpMpPanel.PercentText(0,10)=="0%"&&HpMpPanel.PercentText(995,1000)=="100%"&&HpMpPanel.PercentText(989,1000)=="98%","percent: ≤ 99 rounds down, > 99 rounds up");
            Check(!new HpMpPanel(HpMpSettings.Default with{Status=0}).PercentShown&&new HpMpPanel().PercentShown&&!new HpMpPanel(HpMpSettings.Default with{ShowPercent=false}).PercentShown,"percent hidden at status 0 or when showPercent is off");
        }

        // --- low-HP frame timing.
        {
            var p=new HpMpPanel();
            UiStaticGameState S(long hp,long now)=>new(true,"t",1,hp,100,0,10,now);
            p.Update(S(80,0));Check(p.WarnCounter==0,"HP 80% → no warning");
            p.Update(S(35,1000));Check(p.WarnCounter==6&&!p.WarnVisible,"HP 35% (≤ 35%) → counter 6 (even = not drawn)");
            p.Update(S(35,1300));Check(p.WarnCounter==5&&p.WarnVisible,"300 ms later → 5 (odd = drawn)");
            p.Update(S(35,1600));Check(p.WarnCounter==4&&!p.WarnVisible,"blinks every 300 ms");
            p.Update(S(35,3000));Check(p.WarnCounter==0,"same HP value does not restart; counter runs out");
            p.Update(S(20,3100));Check(p.WarnCounter==6,"a new low HP value restarts the counter");
            p.Update(S(36,3200));p.Update(S(36,9000));Check(p.WarnCounter==0,"36% is above the threshold");
            p.Update(new UiStaticGameState(true,"t",1,null,null,null,null,9100));Check(p.WarnCounter==0,"no server HP → no warning");
        }

        // --- paint with synthetic surfaces.
        {
            var imgs=new Dictionary<int,(int,int,ushort)>{
                [2355]=(378,48,HmFrame),[2365]=(141,20,HmHp),[2358]=(136,20,HmMp),[2368]=(22,7,HmButton),[2369]=(22,7,HmButtonDown),[2371]=(22,7,HmButton),[2937]=(163,33,HmWarn),
                [2910]=(378,48,HmFrame),[930]=(44,5,HmBase),[931]=(44,5,HmHp),[932]=(44,5,HmMp)};
            using var res=HpMpSyntheticResources(Path.Combine(root,"paint"),imgs);
            var p=new HpMpPanel(HpMpSettings.Default with{ShowPercent=false});p.Load(null!,res.Surfs);
            using var canvas=new UiCanvas();
            void Frame(IUiPanel panel){canvas.Clear(Color.Black);canvas.Restrict(panel.Bounds);panel.Paint(canvas);canvas.Release();}
            int Px(int x,int y)=>canvas.Bitmap.GetPixel(x,y).ToArgb();
            p.Update(new UiStaticGameState(true,"t",1,50,100,25,100,0));Frame(p);
            int hx=135+9,hy=465+11+17,mx=135+183;
            Check(Px(135,465)==HmArgb(HmFrame)&&Px(135+377,465+47)==HmArgb(HmFrame)&&Px(135+378,465)==Color.Black.ToArgb(),"frame 2355 drawn 1:1 at (135,465)");
            Check(Px(hx+70,hy)==HmArgb(HmFrame)&&Px(hx+71,hy)==HmArgb(HmHp)&&Px(hx+140,hy)==HmArgb(HmHp),"HP 50%: right part [71,141) drawn, shrinks toward the right");
            Check(Px(mx,hy)==HmArgb(HmMp)&&Px(mx+33,hy)==HmArgb(HmMp)&&Px(mx+34,hy)==HmArgb(HmFrame),"MP 25%: left part [0,34) drawn");
            Check(Px(135+135,465+3)==HmArgb(HmButton)&&Px(135+177,465+3)==HmArgb(HmButton),"buttons at (135,3) and (177,3)");
            int lit=0;for(int x=hx+30;x<hx+110;x++)for(int y=465+11;y<465+11+14;y++)if(Px(x,y)==Color.White.ToArgb())lit++;
            Check(lit>0,"\"%d/%d\" drawn in white over the HP bar");
            p.Mouse(new(UiMouseKind.Down,new(135+136,465+4),MouseButtons.Left,1,0,Keys.None),new UiNullCommands());Frame(p);
            Check(Px(135+135,465+3)==HmArgb(HmButtonDown),"pressed button uses n+1");
            p.Mouse(new(UiMouseKind.Up,new(0,0),MouseButtons.Left,1,0,Keys.None),new UiNullCommands());
            p.Update(new UiStaticGameState(true,"t",1,null,null,null,null,0));Frame(p);
            Check(Px(hx+140,hy)==HmArgb(HmFrame)&&Px(mx,hy)==HmArgb(HmFrame),"no server HP/MP → bars blank (frame only)");
            p.Update(new UiStaticGameState(true,"t",1,30,100,0,100,10));p.Update(new UiStaticGameState(true,"t",1,30,100,0,100,320));Frame(p);
            Check(p.WarnVisible&&Px(135+6,465+8)==HmArgb(HmWarn),"low-HP frame 2937 at (+6,+8) when the counter is odd");
            p.Apply(p.Settings with{Status=1});Frame(p);
            Check(Px(135,465)==Color.Black.ToArgb()&&p.Bounds.Size==new Size(320,48),"missing surf (2354 not in the synthetic set) draws nothing, no placeholder; bounds fall back to the V1 size");
            p.Apply(p.Settings with{Status=2,Thickness=true});
            Check(p.Layout.Frame==2910&&p.Bounds.Size==new Size(378,48),"bounds follow the drawn frame surface (Taiwan 2910c.img is 378×48, V1 plain 334×34)");

            // Over-head bars.
            var heads=new HpMpHeadBarPanel();heads.Load(null!,res.Surfs);
            var self=new HpMpHeadTarget(1,true,new Rectangle(300,200,48,64));
            var other=new HpMpHeadTarget(2,false,new Rectangle(500,300,40,60),HpPercent:50);
            heads.Update(new UiHpMpGameState(new UiStaticGameState(true,"t",1,33,100,100,100,0),[self,other]));
            var plan=heads.Plan().ToList();
            Check(heads.Layer==UiLayer.World&&heads.Bounds==UiRoot.WorldRect,"head bars live on the world layer");
            Check(plan.Count==2&&plan[0].At==new Point(300+24-24,200-12)&&plan[0].Hp==33&&plan[0].Mp==100&&plan[1].At==new Point(500+20-24,300-12)&&plan[1].Hp==50&&plan[1].Mp==null,"anchor bbox centre −24, top −12; own HP% from server HP/MaxHP; others HP only");
            Frame(heads);
            var a=plan[1].At;
            Check(Px(a.X+21,a.Y+1)==HmArgb(HmHp)&&Px(a.X+22,a.Y+1)==HmArgb(HmBase)&&Px(a.X,a.Y+4)==HmArgb(HmBase)&&Px(a.X,a.Y+5)==Color.Black.ToArgb(),"other: HP fill 44×50% = 22 px, 4 rows over base 930; no MP bar");
            var o=plan[0].At;
            Check(Px(o.X+13,o.Y)==HmArgb(HmHp)&&Px(o.X+14,o.Y)==HmArgb(HmBase)&&Px(o.X+43,o.Y+4)==HmArgb(HmMp)&&Px(o.X+43,o.Y+7)==HmArgb(HmMp)&&Px(o.X,o.Y+8)==HmArgb(HmBase),"self: HP 33% = 14 px; MP 100% at y+4 (overlaps HP base by 1 row)");
            var hideSelf=new HpMpHeadBarPanel(()=>HpMpSettings.Default with{ShowCharacterHpMp=false});
            hideSelf.Update(new UiHpMpGameState(new UiStaticGameState(true,"t",1,33,100,100,100,0),[self,other]));
            Check(hideSelf.Plan().Select(x=>x.Target.ObjectId).SequenceEqual([2u]),"showCharacterHPMP=false hides only the own bars");
            var edge=new HpMpHeadBarPanel();
            edge.Update(new UiHpMpGameState(new UiStaticGameState(true,"t",1,10,100,null,null,0),[
                new(3,false,new Rectangle(0,100,20,40),HpPercent:10),          // x = −14 → off
                new(4,false,new Rectangle(400,620,40,60),HpPercent:10),        // y = 608 > 600 → off (others)
                new(5,true,new Rectangle(400,620,40,60)),                      // self: no y ≤ 600 rule
                new(6,false,new Rectangle(400,5,40,60),HpPercent:10)]));       // y = −7 → off
            Check(edge.Plan().Select(x=>x.Target.ObjectId).SequenceEqual([5u]),"on-screen rule: x ≥ 0, y ≥ 0, x ≤ 800; y ≤ 600 only for others");
            Check(new HpMpHeadBarPanel().Plan().Any()==false,"no IUiHpMpState → no head bars");
            var noMax=new HpMpHeadBarPanel();noMax.Update(new UiHpMpGameState(new UiStaticGameState(true,"t",1,10,null,null,null,0),[self]));
            Check(!noMax.Plan().Any(),"self without server MaxHP/MaxMP → nothing drawn");
            Check(HpMpHeadBarPanel.OwnPercent(150,100)==100&&HpMpHeadBarPanel.OwnPercent(-3,100)==0&&HpMpHeadBarPanel.OwnPercent(199,200)==99,"own percent clamped 0–100 and truncated");
        }

        // --- registry and Build on synthetic pinned resources.
        {
            var regs=UiPanelRegistry.Panels.Where(r=>r.Id is HpMpPanel.PanelId or HpMpHeadBarPanel.PanelId).ToArray();
            Check(regs.Length==2&&regs.All(r=>r.Layout=="uistatus86"),"registry: HpMpWindow + MiniHpMpBar");
            using var res=HpMpSyntheticResources(Path.Combine(root,"build"),new Dictionary<int,(int,int,ushort)>{[2355]=(378,48,HmFrame)});
            var built=UiRoot.Build(res,regs);
            var win=(HpMpPanel)built.Find(HpMpPanel.PanelId)!;var heads=built.Find(HpMpHeadBarPanel.PanelId)!;
            win.Apply(win.Settings with{ShowCharacterHpMp=false});
            heads.Update(new UiHpMpGameState(new UiStaticGameState(true,"t",1,5,10,5,10,0),[new(1,true,new(300,300,40,40))]));
            Check(!((HpMpHeadBarPanel)heads).Plan().Any(),"head bars follow the window's showCharacterHPMP setting");
            var state=new UiHpMpGameState(new UiStaticGameState(true,"n",9,1,2,3,4,5),[]);
            Check(state.InWorld&&state.CharacterName=="n"&&state.Level==9&&state.Hp==1&&state.MaxHp==2&&state.Mp==3&&state.MaxMp==4&&state.Now==5,"UiHpMpGameState forwards every IUiGameState member");
        }
    }

    /// <summary>Optional: TW_CLIENT_ROOT set → check every V1 surf size against the real client and render PNGs into scratch/ only.</summary>
    [CheckGroup("native-ui-hpmp",Order=2)]
    static void NativeUiHpMpRealClientChecks(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui hpmp real client checks (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-465","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        var rows=new List<object>();var mismatches=new List<string>();
        // V1 measured the plain <n>.img; the catalog prefers the Taiwan <n>c.img (V4 (e)). Check V1 against the plain file and
        // record what is actually drawn (the c version) so the differences are visible in the report.
        var cDiffers=new List<string>();
        void Expect(int surf,int w,int h,string what){
            var s=res.Surfs.Get(surf);
            var file=res.Surfs.Files.TryGetValue(surf,out var f)?f:null;
            var plain=file==null?default:res.Surfs.Candidates(file).Where(c=>!c.Name.EndsWith("c.img",StringComparison.Ordinal)).Select(c=>(c.Archive,Entry:c.Archive.Find(c.Name))).FirstOrDefault(c=>c.Entry!=null);
            UiImage? plainImg=plain.Entry==null?null:UiImage.Decode(plain.Archive.Read(plain.Entry));
            rows.Add(new{surf,what,v1=$"{w}×{h}",plain=plainImg==null?null:$"{plainImg.Width}×{plainImg.Height}",drawn=s==null?null:$"{s.Width}×{s.Height}",drawnFile=s?.FileName,archive=s?.Archive,flags=s?.Flags});
            if(s==null||plainImg==null||plainImg.Width!=w||plainImg.Height!=h)mismatches.Add($"{surf} {what}: V1 {w}×{h}, plain {(plainImg==null?"missing":$"{plainImg.Width}×{plainImg.Height}")}, drawn {(s==null?"missing":s.FileName)}");
            if(s!=null&&(s.Width!=w||s.Height!=h))cDiffers.Add($"{surf} {what}: drawn {s.FileName} {s.Width}×{s.Height} ≠ V1 {w}×{h}");
        }
        foreach(int shape in new[]{0,1})foreach(int st in new[]{0,1,2})foreach(bool t in new[]{false,true}){
            var l=HpMpLayout.For(shape,st,t);string tag=$"shape{shape}/s{st}/{(t?"thick":"normal")}";
            Expect(l.Frame,l.FrameSize.Width,l.FrameSize.Height,tag+" frame");
            Expect(l.Hp,l.HpBar.Width,l.HpBar.Height,tag+" HP");
            Expect(l.Mp,l.MpBar.Width,l.MpBar.Height,tag+" MP");
            Expect(l.ShapeButton,l.ShapeButtonRect.Width,l.ShapeButtonRect.Height,tag+" shape button");
            Expect(l.LengthButton,l.LengthButtonRect.Width,l.LengthButtonRect.Height,tag+" length button");
        }
        foreach(int s in new[]{930,931,932})Expect(s,44,5,"head bar");
        // Render: default window and both shapes with sample (synthetic, labelled) values; head bars over a black world.
        var samples=new List<object>();
        foreach(var (name,settings) in new[]{("default",HpMpSettings.Default),("vertical",HpMpSettings.Default with{Shape=0}),("thick",HpMpSettings.Default with{Thickness=true}),("short",HpMpSettings.Default with{Status=0}),("thick-vertical",HpMpSettings.Default with{Thickness=true,Shape=0})}){
            var p=new HpMpPanel(settings);p.Load(null!,res.Surfs);
            var heads=new HpMpHeadBarPanel(()=>p.Settings);heads.Load(null!,res.Surfs);
            var state=new UiHpMpGameState(new UiStaticGameState(true,"sample",1,123,456,78,90,0),[new(1,true,new(376,236,48,64)),new(2,false,new(500,250,40,60),HpPercent:64)]);
            p.Update(state);heads.Update(state);
            using var canvas=new UiCanvas();canvas.Clear(Color.Black);
            foreach(IUiPanel panel in new IUiPanel[]{heads,p}){canvas.Restrict(panel.Bounds);panel.Paint(canvas);}
            canvas.Release();
            string file=Path.Combine(outDir,$"hpmp-{name}.png");canvas.Bitmap.Save(file,ImageFormat.Png);
            samples.Add(new{name,file,bounds=p.Bounds,layout=p.Layout,heads=heads.Plan().Select(x=>new{x.Target.ObjectId,x.Target.Self,x.At,x.Hp,x.Mp})});
        }
        File.WriteAllText(Path.Combine(outDir,"hpmp-real.json"),JsonSerializer.Serialize(new{client,sampleValues="HP 123/456, MP 78/90 (synthetic, not server data)",rows,mismatches,cDiffers,samples},new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        Check(mismatches.Count==0,"real client: every V1 surf present and its plain .img has the V1 size ("+rows.Count+" rows; "+cDiffers.Count+" drawn c.img differ)"+(mismatches.Count>0?": "+string.Join("; ",mismatches):""));
    }
}
