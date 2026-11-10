namespace P06.NativeUi;

// W2-C（T232／#550）agit-map-panel 離線測試：surf 全部是合成單色圖，不讀真實客戶端、不啟動遊戲、不連網路。
static class AgitMapPanelTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const uint Map689=0xFF808080,Map801=0xFF404040,Marker=0xFFFF0000,Marker7=0xFF00FF00,Close=0xFF0000FF,Action=0xFFFFFF00,Right=0xFFFFFFFF;
    static Dictionary<int,UiSurface> Surfaces()=>new(){
        [689]=Surface(689,300,320,Map689),[741]=Surface(741,300,320,Map689),[801]=Surface(801,300,320,Map801),[1482]=Surface(1482,300,320,Map689),
        [176]=Surface(176,4,4,Marker),[34]=Surface(34,4,4,Marker7),[160]=Surface(160,40,30,Right),[164]=Surface(164,40,30,Right),
        [678]=Surface(678,30,14,Close),[674]=Surface(674,30,14,Action),
    };
    sealed class Source{public AgitMapOpenRequest? Current;long rev;public void Open(int region,int number)=>Current=new(++rev,new(0x1234,(uint)(region<<16|number)));}
    static AgitMapPanel Panel(Source src,Dictionary<int,UiSurface>? surfs=null){var map=surfs??Surfaces();var p=new AgitMapPanel(()=>src.Current);p.Load(id=>map.GetValueOrDefault(id));return p;}
    static IUiGameState At(long now,bool inWorld=true)=>new UiStaticGameState(InWorld:inWorld,Now:now);
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static void Click(AgitMapPanel p,Point at,UiNullCommands c){p.Mouse(M(UiMouseKind.Down,at),c);p.Mouse(M(UiMouseKind.Up,at),c);}
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static int Pixel(AgitMapPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();

    [WorldModuleTest("agit-map-panel")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊與 BIN 常數（sprites-and-strings.md 2.1）
        t.Check(UiPanelRegistry.Panels.Count(r=>r.Id==AgitMapPanel.PanelId)==1,"AgitMap_Window registered once");
        t.Equal("null,1482,null,null,689,741,741,801,1482,null",string.Join(",",Enumerable.Range(0,10).Select(r=>AgitMapLayout.MapSurf(r)?.ToString()??"null")),"map surf table dword_135DA34 (region 1 = 8; 0/2/3/9 none)");
        t.Equal("45,11,0,67,6,6",string.Join(",",new[]{4,5,6,7,8,1}.Select(AgitMapLayout.MarkerCount)),"marker counts 45/11/0/67/6, region 1 = 8");
        t.Equal(new Point(14,140),AgitMapLayout.MarkerAt(4,1),"region 4 #1");
        t.Equal(new Point(208,230),AgitMapLayout.MarkerAt(4,45),"region 4 #45");
        t.Equal(new Point(184,236),AgitMapLayout.MarkerAt(5,11),"region 5 #11");
        t.Equal(new Point(68,141),AgitMapLayout.MarkerAt(7,67),"region 7 #67");
        t.Equal(new Point(109,245),AgitMapLayout.MarkerAt(8,6),"region 8 #6");
        t.Equal(AgitMapLayout.MarkerAt(8,3),AgitMapLayout.MarkerAt(1,3),"region 1 uses region 8 table");
        t.Equal(Point.Empty,AgitMapLayout.MarkerAt(5,12),"number past the data → (0,0) like the original");
        t.Equal(Point.Empty,AgitMapLayout.MarkerAt(6,1),"region 6 all (0,0)");
        t.Equal<Point?>(null,AgitMapLayout.MarkerAt(4,0),"number 0 → no marker");
        t.Equal<Point?>(null,AgitMapLayout.MarkerAt(4,71),"number 71 → no marker");
        t.Equal(34,AgitMapLayout.MarkerSurfFor(7),"region 7 marker surf 34");
        t.Equal(176,AgitMapLayout.MarkerSurfFor(4),"other regions marker surf 176");
        t.Check(AgitMapLayout.MarkerOn(0)&&AgitMapLayout.MarkerOn(799)&&!AgitMapLayout.MarkerOn(800)&&!AgitMapLayout.MarkerOn(999)&&AgitMapLayout.MarkerOn(1000),"blink on 800 / off 200");

        // 2. 沒有請求不顯示；S187 區域 4 編號 1 → 開窗
        var src=new Source();var p=Panel(src);var cmd=new UiNullCommands();
        t.Check(p.Usable,"all surfs present → usable");
        p.Update(At(5000));
        t.Check(!p.Visible&&p.Bounds.IsEmpty,"hidden without S187");
        src.Open(4,1);p.Update(At(5000));
        t.Check(p.Visible,"S187 region 4 opens");
        t.Equal(new Rectangle(0,0,352,320),p.Bounds,"bounds = union of map 300×320 and right images at x 312");
        t.Equal(new Rectangle(213,297,30,14),p.CloseRect,"close button 678 at (213,297)");
        t.Equal(new Rectangle(90,299,30,14),p.ActionRect,"button 674 at (90,299)");
        t.Equal(Argb(Map689),Pixel(p,5,5),"map 689 drawn at origin");
        t.Equal(Argb(Marker),Pixel(p,15,141),"marker 176 at (14,140) while on");
        t.Equal(Argb(Right),Pixel(p,320,60),"surf 160 at (312,56)");
        t.Equal(Argb(Right),Pixel(p,320,115),"surf 164 at (312,110)");
        t.Equal(Argb(Close),Pixel(p,215,300),"button 678 drawn");
        t.Equal(Argb(Action),Pixel(p,92,301),"button 674 drawn");
        p.Update(At(5850));
        t.Equal(Argb(Map689),Pixel(p,15,141),"marker off at 850 ms");
        p.Update(At(6000));
        t.Equal(Argb(Marker),Pixel(p,15,141),"marker back on at 1000 ms");

        // 3. 區域 7 → 地圖 801、標記 34；超出筆數畫在 (0,0)；編號 0 不畫標記
        src.Open(7,67);p.Update(At(7000));
        t.Equal(Argb(Map801),Pixel(p,5,5),"region 7 map 801");
        t.Equal(Argb(Marker7),Pixel(p,69,142),"region 7 marker 34 at (68,141)");
        src.Open(5,20);p.Update(At(8000));
        t.Equal((new Point(0,0),176),p.Marker,"region 5 #20 → marker at (0,0)");
        src.Open(4,0);p.Update(At(9000));
        t.Check(p.Visible&&p.Marker==null,"number 0: window open, no marker");

        // 4. 不支援的區域不開窗
        foreach(int r in new[]{0,2,3,9}){src.Open(r,1);p.Update(At(10000));t.Check(!p.Visible,$"region {r} does not open");}

        // 5. 674 不接動作；678 關窗，同一請求不重開，下一個 S187 重開
        src.Open(8,2);p.Update(At(11000));
        Click(p,Center(p.ActionRect),cmd);
        t.Check(p.Visible&&cmd.Calls.Count==0,"674 click: no action (待驗證)",cmd.Calls);
        p.Mouse(M(UiMouseKind.Down,Center(p.ActionRect)),cmd);p.Mouse(M(UiMouseKind.Up,Center(p.CloseRect)),cmd);
        t.Check(p.Visible,"press 674, release on 678 → not closed");
        Click(p,Center(p.CloseRect),cmd);
        t.Check(!p.Visible&&cmd.Calls.SequenceEqual(["game-focus"]),"678 closes and returns game focus",cmd.Calls);
        p.Update(At(11100));
        t.Check(!p.Visible,"same request stays closed");
        src.Open(8,2);p.Update(At(11200));
        t.Check(p.Visible,"next S187 reopens");
        t.Check(p.Mouse(M(UiMouseKind.Down,new(100,100)),cmd),"click on the map is consumed");
        p.Update(At(11300,inWorld:false));
        t.Check(!p.Visible,"leaving the world hides the window");

        // 6. 缺 surf → 停用
        var missing=Surfaces();missing.Remove(674);
        var q=Panel(src,missing);src.Open(4,1);q.Update(At(0));
        t.Check(!q.Usable&&!q.Visible,"missing surf 674 → panel disabled");

        // 7. 與 WorldSession 串接：S187 送出開窗事件，Reset 清掉
        AgitMapWindow.Clear();
        var s=new WorldSession();
        s.Receive(P06.LoginPackets.Pad([P06.AgitPackets.HouseMapOpcode,0x78,0x56,0x34,0x12,0x05,0x00,0x07,0x00]),"");
        t.Check(AgitMapWindow.Current is {Map:{ObjectId:0x12345678,Region:7,Number:5}},"S187 → AgitMapWindow.Current",AgitMapWindow.Current);
        long rev=AgitMapWindow.Current?.Revision??0;
        s.Receive(P06.LoginPackets.Pad([P06.AgitPackets.HouseMapOpcode,0x78,0x56,0x34,0x12,0x05,0x00,0x07,0x00]),"");
        t.Check(AgitMapWindow.Current?.Revision==rev+1,"each S187 is a new open request");
        var real=new AgitMapPanel();real.Load(id=>Surfaces().GetValueOrDefault(id));real.Update(At(0));
        t.Check(real.Visible&&real.MapSurf==801,"default source reads AgitMapWindow.Current");
        s.Reset();
        t.Check(AgitMapWindow.Current==null,"WorldSession.Reset clears the request");
        real.Update(At(10));
        t.Check(!real.Visible,"window closes after reset");
    }
}
