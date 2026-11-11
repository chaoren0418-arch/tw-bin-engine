namespace P06.NativeUi;

// R10-T（T407／#924）召喚窗停用格色塊的離線測試（docs/reference/followups-13.md §4、D6–D10）：
// DisableOverSurf="13" 是色塊強度（不是 surf 13）；停用格常駐畫格子大小的 palette[0]（黑）半透明色塊（13/32），
// 在怪物圖之上、徽章之下，與滑鼠無關；可用格才依滑鼠畫 Highlight → Over → Push；停用格不觸發事件、提示仍顯示。
// XML、summonList、.img、desc 文字全部由本檔合成，不讀真實客戶端、不寫檔。由 --module-tests 執行（名稱 summon-disable-overlay）。
static class SummonDisableOverlayTests {
    sealed class Host:ISummonUiHost {
        public SummonUiState State{get;set;}=new(false,0,"",null,null,false);
        public readonly List<string> Calls=[];
        public void Choose(SummonUiState shown,SummonMonster monster)=>Calls.Add($"choose:{monster.Action}");
        public void Close(SummonUiState shown)=>Calls.Add("close");
    }
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Red=0xFFFF0000,Green=0xFF00FF00,Blue=0xFF0000FF,Gray=0xFF808080,White=0xFFFFFFFF,Yellow=0xFFFFFF00,Cyan=0xFF00FFFF,Navy=0xFF000080,Magenta=0xFFFF00FF;

    // 等級字在右上 (12,2)，徽章在左下 (1,14)；extra＝額外的 ListItem 屬性（HighlightSurf／PushSurf 測試用）。
    static string Item(string name,int baseSurf,string tooltip,string disableOver,string extra)=>
        $"<ListItem Name=\"{name}\" BaseSurf=\"{baseSurf}\" OverSurf=\"30;4\" DisableOverSurf=\"{disableOver}\" {extra} MouseEvent=\"X\"><Label Name=\"Level_Label\" X=\"12\" Y=\"2\" Width=\"6\" Height=\"6\" Caption=\"80\"/><Image Name=\"LevelLabel_Image\" X=\"1\" Y=\"14\" Surf=\"31;1\"/><Tooltip Name=\"Desc_Tooltip\" X=\"1\" Y=\"1\" {tooltip} BackColor=\"0,0,0\"/></ListItem>";
    static string Layout(string disableOver="13",string extra="")=>"<?xml version=\"1.0\"?><SummonUI Toggle=\"true\"><Image Name=\"SummonDrag_Image\" X=\"0\" Y=\"0\" Surf=\"0;2;13\" Activate=\"0\"/>"+
        "<Window Name=\"SummonMonster_Window\" X=\"40\" Y=\"30\" BaseSurf=\"1;1\" FrameSurf=\"2;1\" Moveable=\"12\">"+
        "<Button Name=\"CloseWindow_Button\" X=\"90\" Y=\"2\" BaseSurf=\"3\" PushSurf=\"4\"/>"+
        "<Image Name=\"SummonTitle_Image\" X=\"5\" Y=\"14\" Surf=\"5;1\"/>"+
        "<List Name=\"QuickSummonLevel_List\" X=\"5\" Y=\"20\" Width=\"60\" Height=\"20\" Horizontal=\"1\" Gap=\"2\">"+
        string.Concat(Enumerable.Range(0,3).Select(i=>Item($"QuickSummon{i}_Button",20,"Desc=\"13714\"",disableOver,"")))+"</List>"+
        "<List Name=\"Level_List\" X=\"5\" Y=\"50\" Width=\"60\" Height=\"60\" Vertical=\"1\" Gap=\"3\">"+
        string.Concat(Enumerable.Range(0,6).Select(r=>$"<List Name=\"LevelSub{r}_List\" Width=\"60\" Height=\"7\" Horizontal=\"1\" Gap=\"4\"><Button Name=\"Level0_Button\" BaseSurf=\"{40+2*r};1\" PushSurf=\"60;1\"/><Button Name=\"Level1_Button\" BaseSurf=\"{41+2*r};1\" PushSurf=\"60;1\"/></List>"))+"</List>"+
        "<Control Name=\"SummonLevelList_Control\" X=\"4\" Y=\"45\" Activate=\"0\"><List Name=\"SummonLevel_List\" X=\"1\" Y=\"2\" Width=\"60\" Height=\"40\" Vertical=\"1\" Gap=\"0\">"+
        string.Concat(Enumerable.Range(0,2).Select(r=>$"<List Name=\"SummonLevel{r}_List\" Width=\"60\" Height=\"18\" Horizontal=\"1\" Gap=\"0\">"+
            string.Concat(Enumerable.Range(0,3).Select(c=>Item($"Summon{c}_Button",21,"Text=\"Summon Monster\"",disableOver,extra)))+"</List>"))+"</List></Control>"+
        "<Button Name=\"Ok_Button\" X=\"10\" Y=\"110\" BaseSurf=\"6\" PushSurf=\"8;1\"/>"+
        "<Button Name=\"Cancel_Button\" X=\"40\" Y=\"110\" BaseSurf=\"9\" PushSurf=\"10;1\"/>"+
        "<Button Name=\"Back_Button\" X=\"40\" Y=\"110\" BaseSurf=\"11\" PushSurf=\"12;1\" Activate=\"0\"/>"+
        "<Label Name=\"Charisma_Label\" X=\"70\" Y=\"14\" Width=\"16\" Height=\"8\" Caption=\"0\"/>"+
        "<Image Name=\"CharismaText_Image\" X=\"60\" Y=\"15\" Surf=\"14;1\"/><Image Name=\"CharismaLabel_Image\" X=\"69\" Y=\"13\" Surf=\"15;1\"/>"+
        "</Window></SummonUI>";
    // 等級 28：id 1（action 7、minCha 8、surf 100）、id 2（action 263、minCha 30、surf 102）；其餘等級各 1 隻。
    static string List()=>"<?xml version=\"1.0\"?><summonlist>"+
        "<summon level=\"28\"><monster id=\"1\" level=\"28\" action=\"7\" desc=\"13683\" surf=\"100\" minCha=\"8\"/>"+
        "<monster id=\"2\" level=\"28\" action=\"263\" desc=\"13684\" surf=\"102\" minCha=\"30\"/></summon>"+
        string.Concat(Enumerable.Range(1,11).Select(i=>$"<summon level=\"{28+4*i}\"><monster id=\"{10+i}\" level=\"{28+4*i}\" action=\"{7+i}\" desc=\"0\" surf=\"{110+i}\" minCha=\"8\"/></summon>"))+
        "</summonlist>";
    // surf 13 故意存在（黃色）：DisableOverSurf 的 13 不可以被當成 surf 畫出來。
    static Dictionary<int,UiSurface> Surfaces(){
        var d=new Dictionary<int,UiSurface>{[1]=Surface(1,100,120,Gray),[3]=Surface(3,8,8,Red),[6]=Surface(6,20,8,Blue),[9]=Surface(9,20,8,Red),[11]=Surface(11,20,8,Green),
            [13]=Surface(13,18,18,Yellow),[20]=Surface(20,18,16,Blue),[21]=Surface(21,18,18,Red),[30]=Surface(30,9,9,White),
            [31]=Surface(31,4,4,Cyan),[100]=Surface(100,6,6,Green),[102]=Surface(102,6,6,Navy),
            [70]=Surface(70,18,18,Magenta),[72]=Surface(72,4,4,Cyan)};
        for(int i=40;i<52;i++)d[i]=Surface(i,10,7,Blue);
        return d;
    }
    static readonly Dictionary<int,string> Desc=new(){[13683]="Goblin",[13684]="Hob^(cha 30)",[13714]="Mark a monster"};
    static SummonPanel Panel(Host host,ISummonQuickStore? store=null,string? layout=null){
        var map=Surfaces();var p=new SummonPanel{QuickStore=store??new SummonQuickMemory(),DescText=id=>Desc.GetValueOrDefault(id)};
        p.Load(UiLayout.Parse(layout??Layout(),"SummonUI.xml"),UiLayout.Parse(List(),"summonList.xml"),id=>map.GetValueOrDefault(id));
        p.Attach(host);return p;
    }
    static readonly IUiGameState World=new UiStaticGameState(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Down(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Down,at),c);
    static void Up(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Up,at),c);
    static void Move(SummonPanel p,Point at,UiNullCommands c)=>p.Mouse(M(UiMouseKind.Move,at),c);
    static void Click(SummonPanel p,Point at,UiNullCommands c){Down(p,at,c);Up(p,at,c);}
    static Point C(SummonPanel p,string key){var r=p.PartRect(key);return new(r.X+r.Width/2,r.Y+r.Height/2);}
    static Bitmap Paint(SummonPanel p,UiCanvas canvas){canvas.Clear(Color.Blue);canvas.Restrict(p.Bounds);p.Paint(canvas);return canvas.Bitmap;}
    static Color Rgb(uint v)=>Color.FromArgb(unchecked((int)v));
    /// <summary>黑色色塊（不透明度 a/255）疊在 c 上的結果。</summary>
    internal static Color Darkened(Color c,int strength=13){
        int a=SummonPanel.DisableOverAlpha(strength);int K(int v)=>v*(255-a)/255;
        return Color.FromArgb(K(c.R),K(c.G),K(c.B));
    }
    internal static bool Near(Color a,Color b,int tol=2)=>Math.Abs(a.R-b.R)<=tol&&Math.Abs(a.G-b.G)<=tol&&Math.Abs(a.B-b.B)<=tol;
    static Point P(Rectangle r,int x,int y)=>new(r.X+x,r.Y+y);
    static Color At(Bitmap b,Point p)=>b.GetPixel(p.X,p.Y);

    [WorldModuleTest("summon-disable-overlay")]
    static void Run(WorldModuleTestContext t){
        Strength(t);Overlay(t);NoEvents(t);UsableOrder(t);QuickSlot(t);
    }

    // 1. D6：強度 → 不透明度；13 不是 surf 編號；非數字 → 版面錯誤。
    static void Strength(WorldModuleTestContext t){
        t.Equal(0,SummonPanel.DisableOverAlpha(0),"strength 0 → no overlay");
        t.Equal(104,SummonPanel.DisableOverAlpha(13),"strength 13 → 13/32 of 255 (rounded)");
        t.Equal(239,SummonPanel.DisableOverAlpha(30),"strength 30 → 30/32");
        t.Equal(255,SummonPanel.DisableOverAlpha(31),"strength 31 → solid (sub_B44890 ≥ 31)");
        t.Equal(255,SummonPanel.DisableOverAlpha(99),"strength above 31 → solid");
        t.Equal(Color.Black.ToArgb(),SummonPanel.DisableOverColor.ToArgb(),"palette[0] placeholder = black (inferred)");
        var item=UiLayout.Parse("<SummonUI><ListItem Name=\"A\" BaseSurf=\"1\" DisableOverSurf=\"13\"/><ListItem Name=\"B\" BaseSurf=\"1\" DisableOverSurf=\"x\"/></SummonUI>","SummonUI.xml").Root.ChildrenNamed("ListItem").ToList();
        t.Equal(13,SummonPanel.DisableOverStrength(item[0]),"DisableOverSurf=\"13\" → strength 13");
        t.Throws<UiLayoutException>(()=>SummonPanel.DisableOverStrength(item[1]),"non-number DisableOverSurf rejected (no guess)");
        var map=Surfaces();
        t.Throws<UiLayoutException>(()=>new SummonPanel().Load(UiLayout.Parse(Layout(disableOver:"abc"),"SummonUI.xml"),UiLayout.Parse(List(),"summonList.xml"),id=>map.GetValueOrDefault(id)),"panel load rejects a bad DisableOverSurf");
    }

    // 2. D7：停用格常駐色塊（不看滑鼠）；怪物圖之上、徽章之下；surf 13 不會被畫出來。
    static void Overlay(WorldModuleTestContext t){
        var host=new Host{State=new(true,1,"server",30,20,true)};var p=Panel(host);var cmd=new UiNullCommands();
        p.Update(World);Click(p,C(p,"level:11"),cmd);
        t.Equal((int?)28,p.SelectedLevel,"level 28 page");
        t.Check(p.Enabled(p.Monsters[0])&&!p.Enabled(p.Monsters[1]),"monster 1 usable, monster 2 disabled by charisma (20 < 30)");
        var r0=p.PartRect("monster:0");var r1=p.PartRect("monster:1");
        using var canvas=new UiCanvas();
        Move(p,new(41,140),cmd);t.Check(p.Hover==null,"mouse away from the slots");
        var bmp=Paint(p,canvas);
        t.Check(Near(At(bmp,P(r1,12,16)),Darkened(Rgb(Red))),"disabled slot without hover: base under the overlay",At(bmp,P(r1,12,16)));
        t.Check(Near(At(bmp,P(r1,1,1)),Darkened(Rgb(Navy))),"monster surf is under the overlay",At(bmp,P(r1,1,1)));
        t.Equal(Rgb(Cyan).ToArgb(),At(bmp,P(r1,2,15)).ToArgb(),"badge is drawn above the overlay");
        t.Equal(Rgb(Red).ToArgb(),At(bmp,P(r0,12,16)).ToArgb(),"usable slot: no overlay");
        t.Equal(Rgb(Green).ToArgb(),At(bmp,P(r0,1,1)).ToArgb(),"usable slot: monster surf untouched");
        t.Check(Near(At(bmp,P(r1,17,17)),Darkened(Rgb(Red))),"overlay covers the whole slot (bottom-right pixel)",At(bmp,P(r1,17,17)));
        var outside=new Point(r1.Right,r1.Bottom);
        t.Equal(Rgb(Gray).ToArgb(),At(bmp,outside).ToArgb(),"overlay stays inside the slot rectangle");

        Move(p,C(p,"monster:1"),cmd);t.Equal("monster:1",p.Hover,"hover on the disabled slot");
        bmp=Paint(p,canvas);
        t.Check(Near(At(bmp,P(r1,12,16)),Darkened(Rgb(Red))),"disabled slot under the mouse: same overlay, no surf 13 (yellow) and no OverSurf",At(bmp,P(r1,12,16)));
        t.Check(Near(At(bmp,P(r1,5,7)),Darkened(Rgb(Red))),"disabled slot under the mouse: no OverSurf (white)",At(bmp,P(r1,5,7)));

        var strong=Panel(new Host{State=new(true,1,"server",30,20,true)},layout:Layout(disableOver:"31"));strong.Update(World);Click(strong,C(strong,"level:11"),cmd);
        bmp=Paint(strong,canvas);
        t.Equal(Color.Black.ToArgb(),At(bmp,P(strong.PartRect("monster:1"),12,16)).ToArgb(),"strength 31 → solid palette[0] block");
        var none=Panel(new Host{State=new(true,1,"server",30,20,true)},layout:Layout(disableOver:"0"));none.Update(World);Click(none,C(none,"level:11"),cmd);
        bmp=Paint(none,canvas);
        t.Equal(Rgb(Red).ToArgb(),At(bmp,P(none.PartRect("monster:1"),12,16)).ToArgb(),"strength 0 → nothing drawn");
    }

    // 3. D8：停用格不觸發事件（不召喚、不拖曳），提示仍顯示（FU13§4.3）。
    static void NoEvents(WorldModuleTestContext t){
        var host=new Host{State=new(true,1,"server",30,20,true)};var p=Panel(host);var cmd=new UiNullCommands();
        p.Update(World);Click(p,C(p,"level:11"),cmd);
        t.Check(p.HitKey(C(p,"monster:1"))==null,"disabled slot is not a hit target");
        Down(p,C(p,"monster:1"),cmd);
        t.Check(p.Dragging==null,"press on the disabled slot does not start a drag");
        Move(p,new(300,300),cmd);t.Check(p.DragImage==null,"…and no drag image");
        Up(p,new(300,300),cmd);
        Click(p,C(p,"monster:1"),cmd);
        t.Check(host.Calls.Count==0&&p.Visible,"click on the disabled slot sends nothing and keeps the window",host.Calls);
        Move(p,C(p,"monster:1"),cmd);
        t.Check(p.Tooltip is {Lines:["Hob","(cha 30)"]},"disabled slot still shows its tooltip",p.Tooltip);
        Click(p,C(p,"monster:0"),cmd);
        t.Equal("choose:7",host.Calls.LastOrDefault(),"usable slot next to it still summons");
    }

    // 4. D10：可用格 Base → Highlight（滑鼠在上且沒按著）→ Over（滑鼠在上）→ Push（按著）；Over 在怪物圖之上。
    static void UsableOrder(WorldModuleTestContext t){
        var cmd=new UiNullCommands();using var canvas=new UiCanvas();
        // SummonUI 的格子沒有 Highlight／Push：滑鼠在上只畫 Over（9×9 白），在怪物圖之上。
        var plain=Panel(new Host{State=new(true,1,"server",30,20,true)});plain.Update(World);Click(plain,C(plain,"level:11"),cmd);
        var r0=plain.PartRect("monster:0");
        Move(plain,C(plain,"monster:0"),cmd);var bmp=Paint(plain,canvas);
        t.Equal(Rgb(White).ToArgb(),At(bmp,P(r0,1,1)).ToArgb(),"hover: OverSurf drawn above the monster surf (monster = BaseSurf in the original)");
        t.Equal(Rgb(Red).ToArgb(),At(bmp,P(r0,12,16)).ToArgb(),"no HighlightSurf in XML → nothing else drawn");

        var p=Panel(new Host{State=new(true,1,"server",30,20,true)},layout:Layout(extra:"HighlightSurf=\"70\" PushSurf=\"72;1\""));p.Update(World);Click(p,C(p,"level:11"),cmd);
        r0=p.PartRect("monster:0");
        Move(p,new(41,140),cmd);bmp=Paint(p,canvas);
        t.Equal(Rgb(Red).ToArgb(),At(bmp,P(r0,12,16)).ToArgb(),"no hover: no Highlight");
        Move(p,C(p,"monster:0"),cmd);bmp=Paint(p,canvas);
        t.Equal(Rgb(Magenta).ToArgb(),At(bmp,P(r0,12,16)).ToArgb(),"hover, not pressed: Highlight");
        t.Equal(Rgb(White).ToArgb(),At(bmp,P(r0,1,1)).ToArgb(),"hover: Over drawn after Highlight");
        Down(p,C(p,"monster:0"),cmd);bmp=Paint(p,canvas);
        t.Equal(Rgb(Red).ToArgb(),At(bmp,P(r0,12,16)).ToArgb(),"pressed: no Highlight");
        t.Equal(Rgb(White).ToArgb(),At(bmp,P(r0,5,7)).ToArgb(),"pressed under the mouse: Over still drawn");
        t.Equal(Rgb(Cyan).ToArgb(),At(bmp,P(r0,1,1)).ToArgb(),"pressed: Push drawn last (above Over)");
        // 停用格即使有 Highlight／Push 也只畫色塊。
        var r1=p.PartRect("monster:1");Move(p,new(41,140),cmd);Up(p,new(41,140),cmd);Move(p,C(p,"monster:1"),cmd);bmp=Paint(p,canvas);
        t.Check(Near(At(bmp,P(r1,12,16)),Darkened(Rgb(Red))),"disabled slot ignores Highlight under the mouse",At(bmp,P(r1,12,16)));
    }

    // 5. 快速格裡的怪物停用時同樣畫色塊（推測：同一套 XmlListItem 規則）；空快速格不畫。
    static void QuickSlot(WorldModuleTestContext t){
        var store=new SummonQuickMemory();store.Save([2,null,1]);
        var p=Panel(new Host{State=new(true,1,"server",30,20,true)},store);p.Update(World);
        using var canvas=new UiCanvas();var bmp=Paint(p,canvas);
        var q0=p.PartRect("quick:0");var q1=p.PartRect("quick:1");var q2=p.PartRect("quick:2");
        t.Check(Near(At(bmp,P(q0,1,1)),Darkened(Rgb(Navy))),"quick slot with a disabled monster: overlay above the monster surf",At(bmp,P(q0,1,1)));
        t.Check(Near(At(bmp,P(q0,5,12)),Darkened(Rgb(Gray))),"…covering the whole slot (window base under it)",At(bmp,P(q0,5,12)));
        t.Equal(Rgb(Blue).ToArgb(),At(bmp,P(q1,5,12)).ToArgb(),"empty quick slot: plain base, no overlay");
        t.Equal(Rgb(Green).ToArgb(),At(bmp,P(q2,1,1)).ToArgb(),"quick slot with a usable monster: no overlay");
    }
}
