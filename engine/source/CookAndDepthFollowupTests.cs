using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R6-F（T336／#782）離線測試：料理窗 4 欄圖示格（34 px、一頁 24 格、一次捲 4 格）、滑過提示框（寬 142、每行 12 px）、
// 篩選（表內順序、≥48 排除、前 8×L 筆）、量表只在倒數中畫、1167／1100 提示，以及 10147／10145 疊層深度每格推進時讀當下的計數。
// 依據 docs/reference/followups-7.md 第 3 節、第 4.2 節與 docs/reports/issue-782.md 的 IDA 核對。
// 全部用合成資料（食譜、字串、XML、圖、list.spr 條目都是編的，不複製遊戲資源）；不開遊戲、網路、DB，也不讀原客戶端。
static class CookAndDepthFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;

    [WorldModuleTest("cook-and-depth-followup")]
    static void Run(WorldModuleTestContext t){
        Constants(t);Filter(t);Grid(t);Tooltip(t);Edges(t);PaintCells(t);Countdown(t);Notices(t);DepthCursor(t);DepthPerFrame(t);DepthTerminalReset(t);
    }

    // ---- 共用 ----
    static byte[] Cp950(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static UiMouseEvent M(UiMouseKind kind,Point p,int clicks=1,int delta=0)=>new(kind,p,kind is UiMouseKind.Move or UiMouseKind.Wheel?MouseButtons.None:MouseButtons.Left,clicks,delta,Keys.None);
    static void Click(IUiPanel p,Point at,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,at),cmd);p.Mouse(M(UiMouseKind.Up,at),cmd);}
    static Point Mid(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
    static void Set(object o,string name,object? value){
        var ty=o.GetType();
        if(ty.GetProperty(name,F) is {} p&&p.CanWrite){p.SetValue(o,value);return;}
        (ty.GetField(name,F)??ty.GetField($"<{name}>k__BackingField",F))!.SetValue(o,value);
    }
    static object? Get(object o,string name)=>o.GetType().GetField(name,F)!.GetValue(o);
    /// <summary>合成字串表：count 筆，指定的 id 放指定的字（內容是編的）。</summary>
    static ServerStringTable Strings(int count,Dictionary<int,string> text)=>
        ServerStringTable.Parse(Cp950(count+"\n"+string.Join("\n",Enumerable.Range(0,count).Select(i=>text.GetValueOrDefault(i,"")))));

    // 與 uiextra86 COOK_BOOK_WINDOW 相同的元件種類與名稱；座標借用 WIN2§7 的相對位置，圖是合成的單色塊（不複製遊戲資源）。
    static string CookXml(int x=10,int y=20)=>$"""
<?xml version="1.0"?>
<root>
<COOK_BOOK_WINDOW x="{x}" y="{y}">
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_COOK_BOOK_BASE" surfNumber="100" xPos="6" yPos="8"/>
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_COOK_BOOK_FRAME" surfNumber="101"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_QUIT_X" surfNumber="110" xPos="179" yPos="15"/>
	<IMAGE surfName="SURF_RENEWAL_WIN_SCROLLBAR" surfNumber="120" xPos="171" yPos="55"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber="121" xPos="171" yPos="222"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber="122" xPos="171" yPos="41"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber="123"/>
{string.Concat(Enumerable.Range(1,11).Select(i=>$"\t<IMAGE surfName=\"SURF_RENEWAL_IMG_COOK_BOOK_{i}\" surfNumber=\"{200+i}\" xPos=\"116\" yPos=\"257\"/>\n"))}	<IMAGE surfName="SURF_RENEWAL_IMG_COOK_BOOK_GUAGE" surfNumber="130" xPos="35" yPos="251"/>
</COOK_BOOK_WINDOW>
</root>
""";
    const uint BaseArgb=0xFF203040,GaugeArgb=0xFF445566,FrameArgb=0xFF00FF00,IconArgb=0xFFFF0000;
    static Func<int,UiSurface?> Surfaces(bool frame=true)=>id=>id switch{
        100=>Surf(100,200,260,BaseArgb),110=>Surf(110,8,8,0xFF010101),
        120=>Surf(120,8,160,0xFF111111),121 or 122=>Surf(id,8,8,0xFF222222),
        130=>Surf(130,50,8,GaugeArgb),24 when frame=>Surf(24,36,36,FrameArgb),
        >200 and <=211=>Surf(id,20,10,0xFF000000|(uint)((id-200)*0x10)),
        _=>null};

    sealed class FakeSource:ICookBookSource {
        public CookBookView? Current{get;set;}
        public List<long> Closed{get;}=[];
        public List<(long Revision,int Recipe)> Cooks{get;}=[];
        public void Close(long revision){Closed.Add(revision);Current=null;}
        public bool Cook(long revision,int recipe){Cooks.Add((revision,recipe));return true;}
    }
    /// <summary>30 道合成料理：第 i 道編號 i，前 24 道帶說明字串 id（CookingProtocol.RecipeTexts）。</summary>
    static CookBookRecipe[] Rows(int n=30)=>Enumerable.Range(0,n).Select(i=>new CookBookRecipe(i,"料理"+i,"甲 ×1",CookingProtocol.IconAt(i),CookingProtocol.TextAt(i,i),3)).ToArray();
    static (CookBookPanel Panel,FakeSource Source) Open(IReadOnlyList<CookBookRecipe> rows,int x=10,int y=20,int gauge=0,bool frame=true,ItemIconSource? icons=null){
        var p=new CookBookPanel(icons);p.Load(UiLayout.Parse(CookXml(x,y),"synthetic-uiextra86.xml"),Surfaces(frame));
        var src=new FakeSource{Current=new(1,0,1,rows,gauge)};p.Source=src;p.Update(new UiStaticGameState(InWorld:true));
        return (p,src);
    }

    // ---- 已驗證常數 ----
    static void Constants(WorldModuleTestContext t){
        t.Check(CookBookPanel.CellPitch==34&&CookBookPanel.Columns==4&&CookBookPanel.PageCells==24&&CookBookPanel.ScrollStep==4,"sub_88C0E0 this[8]/this[9]=34, 4 columns, 24 cells, scroll 4 (sub_A5AA10 / sub_A5BA50 / sub_A5E6F0)");
        t.Check(CookBookPanel.GridLeft==29&&CookBookPanel.GridTop==31,"cells start at window +29, +31 (sub_A5BA50)");
        t.Equal(Rectangle.FromLTRB(29,31,165,233),CookBookPanel.ListLocal,"list rect (29,31)-(165,233) from sub_8C7C80");
        t.Check(CookBookPanel.TooltipWidth==142&&CookBookPanel.LineHeight==12&&CookBookPanel.WrapBytes==23,"tooltip 142 wide, 12 px lines, 23-byte wrap");
        t.Check(CookingProtocol.RecipeIcons.Length==24&&CookingProtocol.RecipeTexts.Length==24,"dword_137A0C0: 24 (level, icon, text) triples");
        t.Check(CookingProtocol.RecipeIcons[0]==2612&&CookingProtocol.RecipeIcons[23]==2946&&CookingProtocol.RecipeTexts[0]==1144&&CookingProtocol.RecipeTexts[8]==1211&&CookingProtocol.RecipeTexts[23]==1401,"table values read from .data FO 0xF788C0");
        t.Check(CookingProtocol.NeedBookText==1167&&CookingProtocol.BusyText==1100&&CookingProtocol.NoticeColorIndex==24,"sub_A5D2A0 notices 1167 / 1100, colour 24");
        t.Check(CookingProtocol.GaugeWidth==50&&CookingProtocol.GaugeHeight==8,"gauge 50 wide, 8 high (sub_A5C5E0)");
    }

    // ---- 篩選：表內順序、≥48 排除、最多 24 格、前 8×L 筆；圖示／說明照位置 ----
    static void Filter(WorldModuleTestContext t){
        // 30 筆：0..9、50（≥48）、10..28。合成表，不是遊戲資料。
        var idx=Enumerable.Range(0,10).Append(50).Concat(Enumerable.Range(10,19)).ToArray();
        var table=idx.Select(i=>new CookRecipe("$"+i,"$"+i,i,4,[new("$9",1)])).ToList();
        t.Check(CookRecipeTable.Rows(1,null,table).Select(r=>r.Index).SequenceEqual(Enumerable.Range(0,8)),"level 1: first 8 rows");
        var l3=CookRecipeTable.Rows(3,null,table);
        t.Check(l3.Select(r=>r.Index).SequenceEqual(Enumerable.Range(0,24)),"level 3: 24 rows, 50 skipped",l3.Select(r=>r.Index));
        t.Equal(24,CookRecipeTable.Rows(11,null,table).Count,"capacity 24 even at level 11");
        t.Check(!CookRecipeTable.Rows(11,null,table).Any(r=>r.Index>=48),"≥48 never listed");
        t.Check(CookRecipeTable.Rows(0,null,table).Count==0,"level 0 → empty");
        t.Check(l3[10] is {Index:10,Icon:2726,TextId:1213,Seconds:4},"icon / text by list position (after the skipped row), seconds = column 4",l3[10]);
        // 表內順序（不依編號排序）：位置 0 是編號 30 → 圖示照位置 0、說明 id 0（編號 ≥24）。
        var shuffled=new List<CookRecipe>{new("$a","$a",30,2,[]),new("$b","$b",1,2,[]),new("$c","$c",0,2,[])};
        var s=CookRecipeTable.Rows(1,null,shuffled);
        t.Check(s.Select(r=>r.Index).SequenceEqual([30,1,0]),"table order kept",s);
        t.Check(s[0] is {Icon:2612,TextId:0}&&s[1] is {Icon:2578,TextId:1145},"index ≥24 has no text id; icon still by position",s);
        t.Equal(0,CookingProtocol.IconAt(24),"no icon past 24");
    }

    // ---- 格子、捲動、選取、製作 ----
    static void Grid(WorldModuleTestContext t){
        var (p,src)=Open(Rows());var cmd=new UiNullCommands();
        t.Equal(new Rectangle(10+29,20+31,34,34),p.CellRect(0),"cell 0 at window +29,+31, 34×34");
        t.Equal(new Rectangle(10+29+34,20+31+34,34,34),p.CellRect(5),"cell 5 = column 1, row 1");
        t.Equal(new Rectangle(10+29+3*34,20+31+5*34,34,34),p.CellRect(23),"cell 23 = column 3, row 5");
        t.Equal(24,p.VisibleRecipes.Count,"24 cells per page");
        t.Equal(8,p.MaxScroll,"30 recipes → 8 rows → max scroll 2 rows × 4");
        t.Equal(5,p.CellAt(Mid(p.CellRect(5))),"hit cell 5");
        t.Equal(-1,p.CellAt(new(10+29-2,20+31+2)),"left of the grid → none");

        var down=Mid(p.At(p.Down));var up=Mid(p.At(p.Up));
        Click(p,down,cmd);t.Equal(4,p.Scroll,"down = one row (4 cells)");
        Click(p,down,cmd);Click(p,down,cmd);t.Equal(8,p.Scroll,"clamped at max");
        t.Equal(8+5,p.CellAt(Mid(p.CellRect(5))),"hit after scroll");
        t.Check(p.Mouse(M(UiMouseKind.Wheel,Mid(p.CellRect(0)),0,120),cmd)&&p.Scroll==4,"wheel up one row");
        Click(p,up,cmd);t.Equal(0,p.Scroll,"up button");
        t.Check(!p.Mouse(M(UiMouseKind.Wheel,new(700,550),0,-120),cmd)&&p.Scroll==0,"wheel outside ignored");

        Click(p,Mid(p.CellRect(2)),cmd);t.Check(p.Selected==2&&src.Cooks.Count==0,"first click selects");
        Click(p,Mid(p.CellRect(3)),cmd);t.Check(p.Selected==3&&src.Cooks.Count==0,"click another cell moves selection");
        Click(p,Mid(p.CellRect(3)),cmd);t.Check(src.Cooks.SequenceEqual([(1L,3)]),"click on the selected cell cooks it",src.Cooks);
        p.Mouse(M(UiMouseKind.Down,Mid(p.CellRect(3)),clicks:2),cmd);p.Mouse(M(UiMouseKind.Up,Mid(p.CellRect(3))),cmd);
        t.Equal(2,src.Cooks.Count,"double click cooks once");
        p.Mouse(M(UiMouseKind.Down,Mid(p.CellRect(3))),cmd);p.Mouse(M(UiMouseKind.Up,Mid(p.CellRect(4))),cmd);
        t.Check(src.Cooks.Count==2&&p.Selected==3,"press and release on different cells does nothing");

        // 短清單：空格不能點；新的一次開窗重設。
        var (q,qs)=Open(Rows(5));
        t.Equal(0,q.MaxScroll,"5 recipes fit one page");
        Click(q,Mid(q.CellRect(7)),cmd);t.Check(q.Selected==-1&&qs.Cooks.Count==0,"empty cell does nothing");
        Click(q,Mid(q.CellRect(1)),cmd);qs.Current=new(2,0,1,Rows(5));q.Update(new UiStaticGameState(InWorld:true));
        t.Check(q.Selected==-1&&q.Scroll==0&&q.Hovered==-1,"new revision resets selection, scroll and hover");
    }

    // ---- 提示框 ----
    static void Tooltip(WorldModuleTestContext t){
        var text=new Dictionary<int,string>{[1145]="甲乙丙丁戊己庚辛壬癸子丑寅卯辰巳",[1399]="甲乙丙丁戊己庚辛壬癸子丑寅卯辰巳"};
        using var strings=ServerMessageText.UseTable(Strings(1402,text));
        var (p,_)=Open(Rows());var cmd=new UiNullCommands();
        t.Check(p.Tooltip==null&&p.Bounds==p.WindowBounds,"no hover → no tooltip");
        var c1=p.CellRect(1);
        p.Mouse(M(UiMouseKind.Move,Mid(c1)),cmd);
        var tip=p.Tooltip!;
        t.Check(tip is {Detailed:false,Index:1}&&tip.Text==new Point(c1.X,c1.Y+32)&&tip.Lines.SequenceEqual(["料理1"]),"hover, not selected → name at (cell x, cell y + 32)",tip);
        // 選取後同一格：提示框，x＝清單左緣，寬 142，高 12＋12×行數＋10；說明 23 byte 一行。
        Click(p,Mid(c1),cmd);tip=p.Tooltip!;
        t.Check(CookBookPanel.Wrap(text[1145],23).SequenceEqual(["甲乙丙丁戊己庚辛壬癸子","丑寅卯辰巳"]),"23-byte wrap keeps double-byte characters whole");
        t.Check(tip.Detailed&&tip.Lines.SequenceEqual(["料理1","甲乙丙丁戊己庚辛壬癸子","丑寅卯辰巳"]),"selected + hovered → name and description lines",tip.Lines);
        int listTop=20+31;
        t.Equal(new Rectangle(10+29,c1.Y+32,142,12+12*2+10),tip.Box,"box at list left, below the cell");
        t.Equal(new Point(10+29+5,c1.Y+32+5),tip.Text,"text from +5,+5");
        t.Check(p.Bounds.Contains(tip.Box),"bounds include the tooltip");
        // 下半部（y ＞ 清單頂＋161）：往上放 40＋12×(行數＋1)。
        var c21=p.CellRect(21);Click(p,Mid(c21),cmd);p.Mouse(M(UiMouseKind.Move,Mid(c21)),cmd);tip=p.Tooltip!;
        int y=c21.Y+32;t.Check(y>listTop+161,"row 5 is below the limit");
        t.Equal(new Rectangle(10+29,y-40-12*(2+1),142,46),tip.Box,"lower half: box moved above the cell");
        // 離開格子：沒有提示，Bounds 回到窗。
        p.Mouse(M(UiMouseKind.Move,new(10+8,20+12)),cmd);
        t.Check(p.Tooltip==null&&p.Bounds==p.WindowBounds,"leaving the cells clears the tooltip");
        // 說明 id 0（編號 ≥24）：選取時也只畫名稱。
        var (q,_)=Open(Rows());Click(q,Mid(q.CellRect(1)),cmd);
        for(int i=0;i<3;i++)Click(q,Mid(q.At(q.Down)),cmd);
        var c=q.CellRect(24-q.Scroll+1);Click(q,Mid(c),cmd);q.Mouse(M(UiMouseKind.Move,Mid(c)),cmd);
        t.Check(q.Selected==25&&q.Tooltip is {Detailed:false},"no text id → name only even when selected",q.Tooltip);
    }

    // ---- 名稱靠右／靠下的界線；字串表讀不到 ----
    static void Edges(WorldModuleTestContext t){
        var cmd=new UiNullCommands();
        var rows=new[]{new CookBookRecipe(0,"料理料理料理料理","乙 ×2",0,1144)};
        var (p,_)=Open(rows,x:700,y:400);var c0=p.CellRect(0);
        p.Mouse(M(UiMouseKind.Move,Mid(c0)),cmd);
        int width=6*16;
        t.Check(c0.X+width>796&&p.Tooltip!.Text.X==796-width,"name moved left of 796",p.Tooltip);
        var (q,_)=Open(Rows(),x:10,y:420);var c16=q.CellRect(16);
        q.Mouse(M(UiMouseKind.Move,Mid(c16)),cmd);
        t.Check(c16.Y+44>600&&q.Tooltip!.Text.Y==c16.Y-12,"near the bottom: name above the cell",q.Tooltip);
        using(ServerMessageText.UseTable(null)){
            t.Check(CookBookPanel.DescriptionLines(rows[0]).SequenceEqual(["材料:乙 ×2"]),"no string table → materials (engine text)");
        }
        t.Check(CookBookPanel.DescriptionLines(rows[0] with{TextId=0}).Count==0,"text id 0 → no lines");
    }

    // ---- 畫：圖示、選取框、量表 ----
    static void PaintCells(WorldModuleTestContext t){
        var bmp=new Bitmap(ItemIconSource.IconSize,ItemIconSource.IconSize);using(var g=Graphics.FromImage(bmp))g.Clear(Color.FromArgb(unchecked((int)IconArgb)));
        var icons=new ItemIconSource(i=>i==2612?new ItemIcon(i,bmp,Point.Empty,"synthetic"):null);
        var cmd=new UiNullCommands();
        static Color Px(CookBookPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y);}
        static bool Is(Color c,uint argb)=>(c.ToArgb()&0xFFFFFF)==(int)(argb&0xFFFFFF);

        var (p,src)=Open(Rows(),icons:icons);var c0=p.CellRect(0);var g0=p.At(p.Gauge);
        t.Check(Is(Px(p,c0.X+5,c0.Y+5),IconArgb),"icon 2612 drawn in cell 0");
        t.Check(Is(Px(p,p.CellRect(1).X+5,p.CellRect(1).Y+5),BaseArgb),"missing icon draws nothing");
        t.Check(Is(Px(p,c0.X-1,c0.Y-1),BaseArgb),"no selection → no frame");
        Click(p,Mid(c0),cmd);p.Mouse(M(UiMouseKind.Move,new(10+8,20+12)),cmd);
        t.Check(Is(Px(p,c0.X-1,c0.Y-1),FrameArgb),"selected cell: surf 24 at cell −1");
        t.Check(Is(Px(p,c0.X+5,c0.Y+5),IconArgb),"icon over the frame");
        t.Check(Is(Px(p,g0.X+1,g0.Y+1),BaseArgb),"no countdown → no gauge (3.81)");
        src.Current=src.Current! with{GaugePixels=20};p.Update(new UiStaticGameState(InWorld:true));
        t.Check(Is(Px(p,g0.X+19,g0.Y+1),GaugeArgb)&&Is(Px(p,g0.X+21,g0.Y+1),BaseArgb),"countdown gauge cropped to 20 px");
        var (q,_)=Open(Rows(),frame:false);Click(q,Mid(q.CellRect(0)),cmd);
        t.Check(Is(Px(q,q.CellRect(0).X-1,q.CellRect(0).Y-1),0xFFFFFF00),"surf 24 missing → 1 px yellow outline (engine stand-in)");
    }

    // ---- 倒數 ----
    static void Countdown(WorldModuleTestContext t){
        var cd=new CookCountdown(3,1000);
        t.Check(cd.Remaining(1000)==6&&cd.Busy(1000)&&cd.GaugePixels(1000)==50,"start: remaining = seconds × 2, full gauge");
        t.Check(cd.Remaining(2500)==3&&cd.GaugePixels(2500)==50*(3>>1)/3,"every 500 ms −1; width = 50×(rem>>1)/total");
        t.Check(cd.Remaining(4000)==0&&cd.GaugePixels(4000)==0&&cd.Busy(4000),"remaining 0: no gauge, still busy until the next tick");
        t.Check(!cd.Busy(4500)&&!new CookCountdown(0,0).Busy(0),"total cleared one tick later; 0 seconds never busy");
        t.Check(CookingProtocol.GaugePixels(5,0)==0&&CookingProtocol.GaugePixels(0,3)==0,"no total / no remaining → 0");

        var w=new WorldSession();long clock=1000;
        w.Receive(LoginPackets.Pad([250,52,0xdb,0x31,0xdf,0x02,1,0]),"");
        using(CookRecipeTable.UseTable([new CookRecipe("$1","$1",0,3,[])])){
            var src=new WorldCookBookSource(()=>w,null,null,()=>clock);
            t.Check(src.Current is {GaugePixels:0}&&w.CookingCountdown==null,"3.81: no countdown → gauge 0");
            t.Check(src.Current!.Recipes![0] is {Icon:2612,TextId:1144,Seconds:3},"source rows carry icon / text / seconds");
            w.BeginCookCountdown(3,1000);
            t.Equal(50,src.Current!.GaugePixels,"countdown → gauge width");
            clock=2500;t.Equal(16,src.Current!.GaugePixels,"gauge shrinks");
            w.Reset();t.Check(w.CookingCountdown==null,"reset clears the countdown");
        }
    }

    // ---- 1167／1100 ----
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static byte[] Item(uint id,byte use,uint count,string name)=>Packet(b=>{b.Write((byte)15);b.Write(id);b.Write((ushort)0);b.Write(use);b.Write((byte)0);b.Write((ushort)9);b.Write((byte)1);b.Write(count);b.Write((byte)0);b.Write(LoginPackets.TextEncoding.GetBytes(name));b.Write((byte)0);b.Write((byte)0);b.Write(new byte[11]);});
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static void Notices(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;
        w.Receive(Item(7001,248,1,"book a"),"offline");w.Receive(Item(7002,248,1,"book b"),"offline");w.Receive(Item(7003,0,3,"stone"),"offline");
        w.SelectItem(7003);w.Receive(LoginPackets.Pad([250,52,0xdb,0x31,0xdf,0x02,1,0]),"offline");
        using(ServerMessageText.UseTable(null)){
            t.Check(!c.CookRecipe(w.CookBook.Revision,0).GetAwaiter().GetResult()&&sent.Count==0,"book unknown → nothing sent");
            var line=w.PlayerChatLines[^1];
            t.Check(line.Text==CookingProtocol.NeedBookFallback&&line.ColorIndex==24&&w.ItemNotice==line.Text,"1167 in the chat line, colour 24 (local string-c text without a table)",line);
        }
        using(ServerMessageText.UseTable(Strings(1200,new(){[1167]="合成一一六七",[1100]="合成一一零零"}))){
            c.CookRecipe(w.CookBook.Revision,0).GetAwaiter().GetResult();
            t.Equal("合成一一六七",w.PlayerChatLines[^1].Text,"1167 read from the string table");
            w.CloseCookBook(w.CookBook.Revision);
            w.SelectItem(7001);w.Receive(LoginPackets.Pad([250,52,0xdb,0x31,0xdf,0x02,1,0]),"offline");
            w.BeginCookCountdown(60,Environment.TickCount64);
            t.Check(!c.CookRecipe(w.CookBook.Revision,0).GetAwaiter().GetResult()&&sent.Count==0,"countdown busy → nothing sent");
            t.Check(w.PlayerChatLines[^1] is {Text:"合成一一零零",ColorIndex:24},"1100 in the chat line",w.PlayerChatLines[^1]);
            w.BeginCookCountdown(0,0);
            t.Check(c.CookRecipe(w.CookBook.Revision,0).GetAwaiter().GetResult()&&sent.Count==1,"no countdown → C_USE_ITEM sent",new{sent=sent.Count,w.ItemNotice});
        }
    }

    // ---- 10147／10145 疊層深度 ----
    const int Release=500,Frame=83,BlockZ=10;
    static byte[] Attack(ushort gfx)=>Packet(b=>{
        b.Write((byte)30);b.Write((byte)18);b.Write(123u);b.Write(456u);b.Write((ushort)7);
        b.Write((byte)3);b.Write(338u);b.Write(gfx);b.Write((byte)0);
        b.Write((ushort)32600);b.Write((ushort)32940);b.Write((ushort)32603);b.Write((ushort)32942);b.Write(0u);b.Write((byte)0);
    });
    static WorldSession World(){
        var w=new WorldSession();Set(w,"MapId",(ushort)0);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":200,\"Mp\":80,\"MaxMp\":100}")!);
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"Name\":\"offline\",\"Gfx\":65000,\"X\":32600,\"Y\":32940,\"HpPercent\":50,\"Brave\":1}")!);
        var objects=(System.Collections.Concurrent.ConcurrentDictionary<uint,WorldObject>)Get(w,"objects")!;
        objects[456]=JsonSerializer.Deserialize<WorldObject>("{\"ObjectId\":456,\"Gfx\":65000,\"X\":32603,\"Y\":32942,\"HpByte\":75,\"Brave\":1}")!;
        w.Ambience.TimeReceived(0,0);
        return w;
    }
    /// <summary>把每個魔法的收包時刻改成 at[i]，並給施法者固定的 `!` 時刻（release＝At＋500）。</summary>
    static void Times(WorldSession w,params long[] at){
        var list=(List<MagicEffect>)Get(w,"magicEffects")!;
        for(int i=0;i<list.Count;i++){
            long a=i<at.Length?at[i]:list[i].At;
            list[i]=list[i] with{At=a,Timing=new ActionCadence(w.Actor!.Gfx,w.Actor.Weapon,list[i].Action,1000,1000,1000,Release,0,0,false),ActionAt=a};
        }
    }
    // 合成 list.spr 條目（不是遊戲資料）：10147 長 12 格（每格 83 ms），10145 段 3 格，109 kind 7 終點 10147。
    static readonly Dictionary<int,string> Rows109=new(){
        [10147]="#10147 1 rise\n0.effect(0 12,"+string.Join(" ",Enumerable.Range(0,12).Select(i=>$"0.{i}:2"))+")\n109.effect(6 -1)\n",
        [10145]="#10145 1 segment\n0.effect(0 3,0.0:2 0.1:2 0.2:2)\n109.effect(7 10147)\n",
    };
    static Renderer Bare(){
        var r=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(r);
        foreach(string name in new[]{"magicFlights","projectileFlights","projectileHits","effectResourceErrors","magicAppearances","projectileFrames"}){
            var f=typeof(Renderer).GetField(name,F)!;f.SetValue(r,Activator.CreateInstance(f.FieldType));
        }
        var catalog=new EffectSpriteCatalog(Path.Combine(AppContext.BaseDirectory,"cook-depth-no-client"));
        var effects=(Dictionary<int,EffectSprite>)Get(catalog,"effects")!;var sprites=(Dictionary<(int,int,int),Frame[]>)Get(catalog,"sprites")!;
        foreach(var (gfx,row) in Rows109){
            var e=EffectSprite.Parse(gfx,row);effects[gfx]=e;
            sprites[(e.Sprite,0,e.TileType)]=Enumerable.Range(0,e.Steps.Max(s=>s.Frame)+1).Select(i=>new Frame($"0:{i}",[0,0,0,0],[new Block(0,0,BlockZ,0,new(e.TileType,[]))])).ToArray();
        }
        Set(r,"effectSprites",catalog);Set(r,"magicEpoch",-1);Set(r,"projectileEpoch",-1);
        return r;
    }
    static int[] Z(Renderer r,WorldSession w,long now){
        var draws=new List<Draw>();
        foreach(string m in new[]{"ComposeProjectile","ComposeMagic"})typeof(Renderer).GetMethod(m,F)!.Invoke(r,[w,draws,now]);
        return draws.Where(x=>x.Layer=="magic").Select(x=>x.Z).ToArray();
    }

    static void DepthCursor(WorldModuleTestContext t){
        var e=EffectSprite.Parse(10145,Rows109[10145]);
        t.Check(EarthDepth.Cursor(e,0)==0&&EarthDepth.Cursor(e,Frame-1)==0&&EarthDepth.Cursor(e,Frame)==1&&EarthDepth.Cursor(e,2*Frame)==2,"cursor = display ordinal");
        t.Check(EarthDepth.Cursor(e,-5)==0&&EarthDepth.Cursor(e,100000)==2,"clamped to the animation");
    }

    // 活著的 10147 在下一次推進時才看到新的計數；同一格內不變。
    static void DepthPerFrame(WorldModuleTestContext t){
        // 兩個封包先收（Magic.cs 會丟掉比最新一筆早 15 s 以上的效果，所以先收再改時刻）；第二個 10147 的收包時刻在第一個的第 0 格期間。
        var w=World();var r=Bare();long release=1000+Release;
        w.Receive(Attack(10147),"offline");w.Receive(Attack(10147),"offline");Times(w,1000,release+30);
        Z(r,w,1000);t.Equal(29,r.EarthDepthCounter,"built on receipt: +29 (the second one not yet received)");
        t.Check(Z(r,w,release) is [BlockZ+29],"first frame reads 29");
        // 第二個建立（+29 → 58），它自己還沒開始畫。
        var z=Z(r,w,release+40);
        t.Check(r.EarthDepthCounter==58&&z is [BlockZ+29],"same frame: depth unchanged although the counter moved",new{r.EarthDepthCounter,z});
        z=Z(r,w,release+Frame+5);
        t.Check(z is [BlockZ+58],"next frame advance reads the current counter (58)",z);
    }

    // kind 7 終點：+29、第一格讀到、歸 0；之後所有活著的 10147／10145 下一格回到原本的 z。
    static void DepthTerminalReset(WorldModuleTestContext t){
        var w=World();var r=Bare();
        w.Receive(Attack(10147),"offline");w.Receive(Attack(10145),"offline");Times(w,1000,1000);
        Z(r,w,1000);long release=1000+Release;
        var z=Z(r,w,release);
        t.Check(z.Length>=2&&z.All(x=>x==BlockZ+29),"kind 6 and the first segment both read 29",z);
        var number=w.MagicEffects.Single(e=>e.Gfx==10145).Number;var plan=r.EruptionPlanFor(number)!;
        t.Check(plan.Chains&&plan.TerminalOffset+Frame<12*Frame,"terminal comes while the kind 6 10147 still plays",plan);
        long terminal=release+plan.TerminalOffset;
        z=Z(r,w,terminal);
        t.Check(z.Contains(BlockZ+58)&&r.EarthDepthCounter==0,"terminal first frame reads 58 (+29 included), then the counter is 0",new{z,r.EarthDepthCounter});
        z=Z(r,w,terminal+Frame);
        t.Check(z.Length>=2&&z.All(x=>x==BlockZ),"after the reset every live 10147 / 10145 is back to its own z on its next frame",z);
    }
}
