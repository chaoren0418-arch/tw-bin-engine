namespace P06.NativeUi;

// R9-M（T373／#857）召喚窗收尾的離線測試（followups-9.md §3、gap-matrix 15.11）：點怪物立即召喚並關窗、OK＝關、
// 停用＝等級公式或魅力、快速 3 格（本機保存、按下即召喚、從怪物格拖進來）、desc 提示與 `^` 換行、徽章 4944＋type、ESC。
// XML、summonList、.img、desc 文字全部由本檔合成（元素名稱照 SummonUI.xml 的結構，座標、尺寸與文字是測試自訂值），不讀真實客戶端、
// 不寫檔（快速格用記憶體保存；檔案格式只測字串轉換）。由 --module-tests 執行（名稱 summon-window）。
static class SummonWindowTests {
    sealed class Host:ISummonUiHost {
        public SummonUiState State{get;set;}=new(false,0,"",null,null,false);
        public readonly List<string> Calls=[];
        public void Choose(SummonUiState shown,SummonMonster monster)=>Calls.Add($"choose:{shown.Revision}:{monster.Action}");
        public void Close(SummonUiState shown)=>Calls.Add($"close:{shown.Revision}");
    }
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Red=0xFFFF0000,Green=0xFF00FF00,Blue=0xFF0000FF,Gray=0xFF808080,White=0xFFFFFFFF,Yellow=0xFFFFFF00,Cyan=0xFF00FFFF,Magenta=0xFFFF00FF,Navy=0xFF000080;

    // 格子：等級字在右上 (12,2)，徽章在左下 (1,14)，避免和 OverSurf／怪物圖的取樣點重疊。
    static string Item(string name,int baseSurf,string caption,string tooltip)=>
        $"<ListItem Name=\"{name}\" BaseSurf=\"{baseSurf}\" OverSurf=\"30;4\" DisableOverSurf=\"13\" MouseEvent=\"X\"><Label Name=\"Level_Label\" X=\"12\" Y=\"2\" Width=\"6\" Height=\"6\" Caption=\"{caption}\"/><Image Name=\"LevelLabel_Image\" X=\"1\" Y=\"14\" Surf=\"31;1\"/><Tooltip Name=\"Desc_Tooltip\" X=\"1\" Y=\"1\" {tooltip} Area=\"0\" HAlign=\"center\" VAlign=\"bottom\" FontColor=\"255,255,255\" BackColor=\"0,0,0\"/></ListItem>";
    static string Layout()=>"<?xml version=\"1.0\"?><SummonUI Toggle=\"true\"><Image Name=\"SummonDrag_Image\" X=\"0\" Y=\"0\" Surf=\"0;2;13\" Activate=\"0\"/>"+
        "<Window Name=\"SummonMonster_Window\" X=\"40\" Y=\"30\" BaseSurf=\"1;1\" FrameSurf=\"2;1\" Moveable=\"12\">"+
        "<Button Name=\"CloseWindow_Button\" X=\"90\" Y=\"2\" BaseSurf=\"3\" PushSurf=\"4\"/>"+
        "<Image Name=\"SummonTitle_Image\" X=\"5\" Y=\"14\" Surf=\"5;1\"/>"+
        "<List Name=\"QuickSummonLevel_List\" X=\"5\" Y=\"20\" Width=\"60\" Height=\"20\" Horizontal=\"1\" Gap=\"2\">"+
        string.Concat(Enumerable.Range(0,3).Select(i=>Item($"QuickSummon{i}_Button",20,"?","Desc=\"13714\"")))+"</List>"+
        "<List Name=\"Level_List\" X=\"5\" Y=\"50\" Width=\"60\" Height=\"60\" Vertical=\"1\" Gap=\"3\">"+
        string.Concat(Enumerable.Range(0,6).Select(r=>$"<List Name=\"LevelSub{r}_List\" Width=\"60\" Height=\"7\" Horizontal=\"1\" Gap=\"4\"><Button Name=\"Level0_Button\" BaseSurf=\"{40+2*r};1\" PushSurf=\"60;1\"/><Button Name=\"Level1_Button\" BaseSurf=\"{41+2*r};1\" PushSurf=\"60;1\"/></List>"))+"</List>"+
        "<Control Name=\"SummonLevelList_Control\" X=\"4\" Y=\"45\" Activate=\"0\"><List Name=\"SummonLevel_List\" X=\"1\" Y=\"2\" Width=\"60\" Height=\"40\" Vertical=\"1\" Gap=\"0\">"+
        string.Concat(Enumerable.Range(0,2).Select(r=>$"<List Name=\"SummonLevel{r}_List\" Width=\"60\" Height=\"18\" Horizontal=\"1\" Gap=\"0\">"+
            string.Concat(Enumerable.Range(0,3).Select(c=>Item($"Summon{c}_Button",21,"80","Text=\"Summon Monster\"")))+"</List>"))+"</List></Control>"+
        "<Button Name=\"Ok_Button\" X=\"10\" Y=\"110\" BaseSurf=\"6\" HighlistSurf=\"7\" PushSurf=\"8;1\"/>"+
        "<Button Name=\"Cancel_Button\" X=\"40\" Y=\"110\" BaseSurf=\"9\" PushSurf=\"10;1\"/>"+
        "<Button Name=\"Back_Button\" X=\"40\" Y=\"110\" BaseSurf=\"11\" PushSurf=\"12;1\" Activate=\"0\"/>"+
        "<Label Name=\"Charisma_Label\" X=\"70\" Y=\"14\" Width=\"16\" Height=\"8\" Caption=\"0\"/>"+
        "<Image Name=\"CharismaText_Image\" X=\"60\" Y=\"15\" Surf=\"14;1\"/><Image Name=\"CharismaLabel_Image\" X=\"69\" Y=\"13\" Surf=\"15;1\"/>"+
        "</Window></SummonUI>";
    // 12 個等級 28、32 … 72（同真實清單的等級公式：等級 L 的 action%256＝(L−28)/4+7）。等級 28 有 3 隻：
    // id 1（action 7、minCha 8）、id 2（action 263、minCha 30、type 2）、id 3（action 519、minCha 8）。
    static string List()=>"<?xml version=\"1.0\"?><summonlist>"+
        "<summon level=\"28\"><monster id=\"1\" level=\"28\" action=\"7\" desc=\"13683\" surf=\"100\" minCha=\"8\"/>"+
        "<monster id=\"2\" level=\"28\" action=\"263\" desc=\"13684\" surf=\"102\" minCha=\"30\" type=\"2\"/>"+
        "<monster id=\"3\" level=\"28\" action=\"519\" desc=\"0\" surf=\"103\" minCha=\"8\"/></summon>"+
        string.Concat(Enumerable.Range(1,11).Select(i=>$"<summon level=\"{28+4*i}\"><monster id=\"{10+i}\" level=\"{28+4*i}\" action=\"{7+i}\" desc=\"0\" surf=\"{110+i}\" minCha=\"8\"/></summon>"))+
        "</summonlist>";
    static Dictionary<int,UiSurface> Surfaces(){
        var d=new Dictionary<int,UiSurface>{[1]=Surface(1,100,120,Gray),[3]=Surface(3,8,8,Red),[6]=Surface(6,20,8,Blue),[9]=Surface(9,20,8,Red),[11]=Surface(11,20,8,Green),
            [13]=Surface(13,18,18,Yellow),[20]=Surface(20,18,16,Blue),[21]=Surface(21,18,18,Red),[30]=Surface(30,18,18,White),
            [31]=Surface(31,4,4,Cyan),[33]=Surface(33,4,4,Magenta),[100]=Surface(100,6,6,Green),[102]=Surface(102,6,6,Navy)};
        for(int i=40;i<52;i++)d[i]=Surface(i,10,7,Blue);
        return d;
    }
    static readonly Dictionary<int,string> Desc=new(){[13683]="Goblin^(cha 8/1)",[13684]="哈柏哥布林^(魅力 30/1)",[13714]="Mark a monster"};
    static SummonPanel Panel(Host host,ISummonQuickStore store,bool desc=true){
        var map=Surfaces();var p=new SummonPanel{QuickStore=store};
        if(desc)p.DescText=id=>Desc.GetValueOrDefault(id);
        p.Load(UiLayout.Parse(Layout(),"SummonUI.xml"),UiLayout.Parse(List(),"summonList.xml"),id=>map.GetValueOrDefault(id));
        p.Attach(host);return p;
    }
    static readonly IUiGameState World=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Down(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Down,at),c);
    static void Up(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Up,at),c);
    static void Move(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Move,at),c);
    static void Click(SummonPanel p,Point at,UiNullCommands c){Down(p,at,c);Up(p,at,c);}
    static void Drag(SummonPanel p,Point from,Point to,UiNullCommands c){Down(p,from,c);Move(p,to,c);Up(p,to,c);}
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static Point C(SummonPanel p,string key)=>Center(p.PartRect(key));
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();
    static Bitmap Paint(SummonPanel p,UiCanvas canvas){canvas.Clear(Color.Blue);canvas.Restrict(p.Bounds);p.Paint(canvas);return canvas.Bitmap;}
    static UiKeyEvent Key(Keys k,bool down=true,bool repeat=false)=>new(k,down,repeat);

    [WorldModuleTest("summon-window")]
    static void Run(WorldModuleTestContext t){
        Disable(t);ClickSummons(t);OkAndEsc(t);QuickSlots(t);Tooltip(t);QuickFile(t);
    }

    // 1. 停用＝等級公式（(等級−28)/4+7 < action%256）或魅力 < minCha；停用格不能點；滑鼠移上去：可用格 OverSurf、停用格 DisableOverSurf；徽章＝4944＋type。
    static void Disable(WorldModuleTestContext t){
        var p=Panel(new Host{State=new(true,1,"server",30,20,true)},new SummonQuickMemory());var cmd=new UiNullCommands();
        var m=p.Monsters;
        t.Check(m[0].Type==0&&m[1].Type==2,"summonList type parsed (missing → 0)",m[1]);
        t.Check(SummonCatalog.Usable(30,20,m[0])&&!SummonCatalog.Usable(30,20,m[1])&&SummonCatalog.Usable(30,30,m[1]),"charisma below minCha disables");
        var lv32=m.First(x=>x.Level==32);
        t.Check(!SummonCatalog.Usable(31,99,lv32)&&SummonCatalog.Usable(32,99,lv32),"level formula: lv 31 refuses action 8, lv 32 allows it");
        t.Check(!SummonCatalog.Usable(null,20,m[0])&&!SummonCatalog.Usable(30,null,m[0]),"no level or no charisma from the server → disabled (not guessed)");
        t.Throws<UiLayoutException>(()=>SummonCatalog.Parse(UiLayout.Parse(List().Replace("type=\"2\"","type=\"-1\""),"summonList.xml")),"negative type rejected");

        p.Update(World);Click(p,C(p,"level:11"),cmd);
        t.Equal((int?)28,p.SelectedLevel,"level 28 page");
        t.Equal("monster:0",p.HitKey(C(p,"monster:0")),"usable monster clickable");
        t.Check(p.HitKey(C(p,"monster:1"))==null,"monster disabled by charisma is not clickable (inferred, M39)");
        Click(p,C(p,"monster:1"),cmd);t.Check(p.Visible,"click on the disabled slot does nothing");

        using var canvas=new UiCanvas();
        Move(p,C(p,"monster:1"),cmd);t.Equal("monster:1",p.Hover,"hover tracks the disabled slot too");
        var bmp=Paint(p,canvas);var r1=p.PartRect("monster:1");var r0=p.PartRect("monster:0");
        // R10-T（#924，FU13§4）：DisableOverSurf="13" 是色塊強度，不是 surf 13（黃）；停用格常駐 13/32 黑色色塊。
        t.Check(SummonDisableOverlayTests.Near(bmp.GetPixel(r1.X+12,r1.Y+16),SummonDisableOverlayTests.Darkened(Color.FromArgb(Argb(Red)))),"disabled slot under the mouse: 13/32 overlay, not surf 13",bmp.GetPixel(r1.X+12,r1.Y+16));
        t.Equal(Argb(Red),bmp.GetPixel(r0.X+12,r0.Y+16).ToArgb(),"other slot: no over surf");
        t.Equal(Argb(Magenta),bmp.GetPixel(r1.X+2,r1.Y+15).ToArgb(),"badge = LevelLabel_Image surf + type (31+2)");
        t.Equal(Argb(Cyan),bmp.GetPixel(r0.X+2,r0.Y+15).ToArgb(),"badge for type 0 = XML surf (31)");
        t.Check(SummonDisableOverlayTests.Near(bmp.GetPixel(r1.X+1,r1.Y+1),SummonDisableOverlayTests.Darkened(Color.FromArgb(Argb(Navy)))),"monster surf drawn in its slot (under the overlay)",bmp.GetPixel(r1.X+1,r1.Y+1));
        Move(p,C(p,"monster:0"),cmd);bmp=Paint(p,canvas);
        t.Equal(Argb(White),bmp.GetPixel(r0.X+12,r0.Y+16).ToArgb(),"usable slot under the mouse: OverSurf");
        t.Check(SummonDisableOverlayTests.Near(bmp.GetPixel(r1.X+12,r1.Y+16),SummonDisableOverlayTests.Darkened(Color.FromArgb(Argb(Red)))),"hover moved away: disabled slot keeps its overlay (FU13§4.2)",bmp.GetPixel(r1.X+12,r1.Y+16));
    }

    // 2. 怪物格：按下＋放開（沒拖曳）＝立即召喚並關窗；不呼叫宿主 Close（送出是非同步的）。
    static void ClickSummons(WorldModuleTestContext t){
        var host=new Host{State=new(true,1,"server",30,20,true)};var p=Panel(host,new SummonQuickMemory());var cmd=new UiNullCommands();
        p.Update(World);Click(p,C(p,"level:11"),cmd);
        Down(p,C(p,"monster:2"),cmd);t.Check(host.Calls.Count==0&&p.Dragging?.Id==3,"press on a monster starts a drag candidate, nothing sent yet");
        Up(p,C(p,"monster:2"),cmd);
        t.Check(host.Calls.SequenceEqual(["choose:1:519"]),"release without dragging summons that monster",host.Calls);
        t.Check(!p.Visible,"…and the window closes at once");
        p.Update(World);t.Check(!p.Visible,"same request stays closed");
        host.State=host.State with{Revision=2};p.Update(World);
        t.Check(p.Visible&&p.SelectedLevel==null,"next request opens on the level page");
        Click(p,C(p,"level:11"),cmd);
        Down(p,C(p,"monster:0"),cmd);Move(p,new(41,140),cmd);Move(p,C(p,"monster:0"),cmd);Up(p,C(p,"monster:0"),cmd);
        t.Check(host.Calls.Count==1&&p.Visible,"dragged out and back: no summon",host.Calls);
        Down(p,C(p,"monster:0"),cmd);Up(p,C(p,"monster:2"),cmd);
        t.Check(host.Calls.Count==1&&p.Visible,"press on one monster, release on another: no summon",host.Calls);
        var detached=new SummonPanel{QuickStore=new SummonQuickMemory()};
        t.Check(!detached.Key(Key(Keys.Escape),cmd),"panel without a request ignores ESC");
    }

    // 3. OK＝只關窗（不送）；ESC（27）＝關窗；重複鍵、放開、其他鍵不吃。
    static void OkAndEsc(WorldModuleTestContext t){
        var host=new Host{State=new(true,1,"local",30,20,true)};var p=Panel(host,new SummonQuickMemory());var cmd=new UiNullCommands();
        p.Update(World);Click(p,C(p,"level:11"),cmd);
        Click(p,C(p,"ok"),cmd);
        t.Check(host.Calls.SequenceEqual(["close:1"])&&!p.Visible,"OK closes only, nothing sent",host.Calls);
        host.State=host.State with{Revision=2};p.Update(World);
        t.Check(!p.Key(Key(Keys.Escape,repeat:true),cmd)&&!p.Key(Key(Keys.Escape,down:false),cmd)&&!p.Key(Key(Keys.Enter),cmd)&&!p.Key(Key(Keys.Escape|Keys.Shift),cmd),"repeat / release / other keys not taken");
        t.Check(p.Visible,"…window still open");
        t.Check(p.Key(Key(Keys.Escape),cmd)&&!p.Visible,"ESC closes the window");
        t.Equal("close:2",host.Calls.LastOrDefault(),"ESC closes the shown request through the host");
        t.Check(!p.Key(Key(Keys.Escape),cmd)&&host.Calls.Count==2,"closed window no longer takes ESC");
    }

    // 4. 快速 3 格：從怪物格拖進來（存本機）、同一隻已在別格時互換、按下＝直接召喚；有內容時畫怪物圖；重開時讀回。
    static void QuickSlots(WorldModuleTestContext t){
        var store=new SummonQuickMemory();var host=new Host{State=new(true,4,"server",30,20,true)};var p=Panel(host,store);var cmd=new UiNullCommands();
        p.Update(World);
        t.Check(p.Quick.All(q=>q==null),"no saved slots → all empty");
        Down(p,C(p,"quick:2"),cmd);Up(p,C(p,"quick:2"),cmd);t.Check(host.Calls.Count==0&&p.Visible,"press on an empty quick slot does nothing");
        Click(p,C(p,"level:11"),cmd);
        Drag(p,C(p,"monster:0"),C(p,"quick:1"),cmd);
        t.Check(p.Quick[1]?.Id==1&&p.Quick[0]==null&&p.Quick[2]==null,"monster dragged into quick slot 1");
        t.Check(store.Saves==1&&store.Load().SequenceEqual(new int?[]{null,1,null}),"quick slots saved locally",store.Load());
        t.Check(host.Calls.Count==0&&p.Visible,"dropping does not summon or close");
        Drag(p,C(p,"monster:2"),new(41,140),cmd);
        t.Check(store.Saves==1&&p.Visible&&host.Calls.Count==0,"drop outside the quick slots changes nothing");
        Drag(p,C(p,"monster:2"),C(p,"quick:0"),cmd);
        Drag(p,C(p,"monster:0"),C(p,"quick:0"),cmd);
        t.Check(p.Quick[0]?.Id==1&&p.Quick[1]?.Id==3&&store.Saves==3,"same monster dropped on another slot swaps the two slots (inferred)",p.Quick);
        Drag(p,C(p,"monster:0"),C(p,"quick:0"),cmd);t.Equal(3,store.Saves,"dropping on its own slot saves nothing");

        using(var canvas=new UiCanvas()){
            Move(p,new(41,140),cmd);var bmp=Paint(p,canvas);var q0=p.PartRect("quick:0");var q1=p.PartRect("quick:1");var q2=p.PartRect("quick:2");
            t.Equal(Argb(Green),bmp.GetPixel(q0.X+1,q0.Y+1).ToArgb(),"quick slot with content draws the monster surf");
            t.Equal(Argb(Gray),bmp.GetPixel(q0.X+3,q0.Y+10).ToArgb(),"…without the empty-slot base (inferred)");
            t.Equal(Argb(Blue),bmp.GetPixel(q1.X+3,q1.Y+10).ToArgb(),"monster whose surf is missing (103): slot base drawn, no placeholder");
            t.Equal(Argb(Blue),bmp.GetPixel(q2.X+3,q2.Y+10).ToArgb(),"empty quick slot: XML BaseSurf");
        }

        Down(p,C(p,"quick:0"),cmd);
        t.Check(host.Calls.SequenceEqual(["choose:4:7"])&&!p.Visible,"press on a quick slot summons it at once and closes",host.Calls);
        Up(p,C(p,"quick:0"),cmd);t.Equal(1,host.Calls.Count,"the release after it does nothing more");

        var again=Panel(new Host{State=new(true,1,"server",30,20,true)},store);
        t.Check(again.Quick[0]?.Id==1&&again.Quick[1]?.Id==3&&again.Quick[2]==null,"saved slots restored on load",again.Quick);
        var stale=new SummonQuickMemory();stale.Save([999,2,null]);
        var host2=new Host{State=new(true,1,"server",30,20,true)};var q=Panel(host2,stale);
        t.Check(q.Quick[0]==null&&q.Quick[1]?.Id==2,"unknown saved id → empty slot");
        q.Update(World);Down(q,C(q,"quick:1"),cmd);
        t.Check(host2.Calls.Count==0&&q.Visible,"quick slot whose monster is disabled (charisma) does not summon");
        host2.State=host2.State with{Revision=2,Charisma=30,CanSend=false};q.Update(World);Down(q,C(q,"quick:1"),cmd);
        t.Check(host2.Calls.Count==0&&q.Visible,"host cannot send → no summon, window stays");
        host2.State=host2.State with{Revision=3,CanSend=true};q.Update(World);Down(q,C(q,"quick:1"),cmd);
        t.Check(host2.Calls.SequenceEqual(["choose:3:263"]),"enough charisma → quick slot summons",host2.Calls);
    }

    // 5. 提示：desc-c.tbl 第 desc 行，`^` 強制換行（最多 64 行）；框寬＝最寬一行 6 px×位元組 +20 進位到 4 的倍數，框高＝12×（行數＋1）。
    static void Tooltip(WorldModuleTestContext t){
        t.Check(SummonPanel.TooltipLines("a^b^^c").SequenceEqual(["a","b","","c"]),"^ = forced line break (empty lines kept)");
        t.Equal(64,SummonPanel.TooltipLines(string.Join("^",Enumerable.Repeat("x",70))).Length,"at most 64 lines");
        t.Equal(new Size(76,36),SummonPanel.TooltipSize(["Goblin","(cha 8/1)"]),"9 bytes → 54+20=74 → 76; 2 lines → 36");
        t.Equal(new Size(80,24),SummonPanel.TooltipSize(["哈柏哥布林"]),"double-byte: 10 bytes → 60+20=80");
        t.Equal(Color.FromArgb(189,190,189).ToArgb(),SummonPanel.TooltipTextColor.ToArgb(),"text colour 0xBDF7 (RGB565)");

        var host=new Host{State=new(true,1,"server",30,20,true)};var p=Panel(host,new SummonQuickMemory());var cmd=new UiNullCommands();
        p.Update(World);
        Move(p,C(p,"quick:0"),cmd);
        t.Check(p.Tooltip is {Lines:["Mark a monster"]} tq&&tq.Box.Size==new Size(104,24),"empty quick slot: XML Desc 13714",p.Tooltip);
        Click(p,C(p,"level:11"),cmd);
        Move(p,C(p,"monster:0"),cmd);
        var r=p.PartRect("monster:0");
        t.Check(p.Tooltip is {Lines:["Goblin","(cha 8/1)"]},"monster slot: desc 13683 split at ^",p.Tooltip);
        t.Equal(new Rectangle(r.X+(r.Width-76)/2,r.Bottom,76,36),p.Tooltip?.Box??Rectangle.Empty,"box centred under the slot (HAlign center, VAlign bottom; inferred)");
        t.Check(p.Bounds.Contains(p.Tooltip!.Box)&&p.Bounds!=p.Window,"Bounds covers the tooltip while it is shown");
        using(var canvas=new UiCanvas()){
            var tip=p.Tooltip!.Box;var bmp=Paint(p,canvas);
            t.Equal(Color.Black.ToArgb(),bmp.GetPixel(tip.X+1,tip.Bottom-2).ToArgb(),"tooltip background = XML BackColor 0,0,0");
        }
        Move(p,C(p,"monster:1"),cmd);t.Check(p.Tooltip is {Lines:["哈柏哥布林","(魅力 30/1)"]},"disabled slot still shows its tooltip",p.Tooltip);
        Move(p,C(p,"monster:2"),cmd);t.Check(p.Tooltip==null,"desc 0 with no text → no tooltip");
        Move(p,new(41,140),cmd);t.Check(p.Tooltip==null&&p.Bounds==p.Window,"mouse away → no tooltip, Bounds = window");
        // 提示框夾在 800×600 內。
        p.MoveTo(new(0,500));Move(p,C(p,"monster:0"),cmd);
        var box=p.Tooltip?.Box??Rectangle.Empty;
        t.Check(box.X==0&&box.Bottom==600&&box.Size==new Size(76,36),"tooltip clamped inside 800×600",box);
        var noText=Panel(new Host{State=new(true,1,"server",30,20,true)},new SummonQuickMemory(),desc:false);noText.Update(World);
        Move(noText,C(noText,"quick:0"),cmd);t.Check(noText.Tooltip==null,"no desc table → no tooltip (nothing invented)");
    }

    // 6. 本機設定檔格式（只測字串轉換，不寫檔）。
    static void QuickFile(WorldModuleTestContext t){
        string json=SummonQuickFile.Serialize([null,1,null]);
        t.Equal("{\"v\":1,\"quick\":[null,1,null]}",json,"serialized form");
        t.Check(SummonQuickFile.Parse(json).SequenceEqual(new int?[]{null,1,null}),"round trip");
        t.Check(SummonQuickFile.Parse("{\"v\":1,\"quick\":[]}").Length==0,"empty list ok");
        foreach(var bad in new[]{"{\"v\":2,\"quick\":[]}","[]","not json","{\"v\":1,\"quick\":[1,2,3,4]}","{\"v\":1,\"quick\":[\"a\"]}","{\"v\":1}"})
            t.Throws<FormatException>(()=>SummonQuickFile.Parse(bad),"rejected: "+bad);
        t.Throws<ArgumentException>(()=>SummonQuickFile.Serialize([1,2,3,4]),"more than 3 slots rejected");
        t.Check(SummonQuickFile.DefaultPath.EndsWith(Path.Combine("runtime","user",SummonQuickFile.FileName),StringComparison.OrdinalIgnoreCase),"default file under runtime/user",SummonQuickFile.DefaultPath);
    }
}
