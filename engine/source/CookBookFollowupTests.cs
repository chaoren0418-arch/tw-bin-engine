using System.Reflection;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R4-D（T295／#707）離線測試：recipe.tbl 解析與篩選、S250 52 同等級關窗、開窗料理書、C_USE_ITEM 製作封包、
// COOK_BOOK_WINDOW 清單／捲動／選取／量表／IUiClosablePanel、WinForms 暫行清單。
// 全部用合成資料（食譜名稱、編號、XML、圖都是編的，不複製遊戲資源）；記憶體內假傳送端，不開遊戲、網路，也不讀客戶端素材。
static class CookBookFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Book=7001,OtherBook=7002,Stone=7003;
    static byte[] Cook(byte open,byte value)=>LoginPackets.Pad([250,52,0xdb,0x31,0xdf,0x02,open,value]);
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static UiMouseEvent M(UiMouseKind kind,int x,int y,int clicks=1,int delta=0)=>new(kind,new(x,y),kind is UiMouseKind.Move or UiMouseKind.Wheel?MouseButtons.None:MouseButtons.Left,clicks,delta,Keys.None);
    static void Click(IUiPanel p,int x,int y,IUiCommands cmd){p.Mouse(M(UiMouseKind.Down,x,y),cmd);p.Mouse(M(UiMouseKind.Up,x,y),cmd);}
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    // S15 欄位順序同 Inventory.ReadItem（見 ShopTradeTests）：D id, H desc, C use, C charge, H icon, C bless, D count, C flags, S name, C statusLen, 11 bytes。
    static byte[] Item(uint id,byte use,uint count,string name)=>Packet(b=>{b.Write((byte)15);b.Write(id);b.Write((ushort)0);b.Write(use);b.Write((byte)0);b.Write((ushort)9);b.Write((byte)1);b.Write(count);b.Write((byte)0);b.Write(LoginPackets.TextEncoding.GetBytes(name));b.Write((byte)0);b.Write((byte)0);b.Write(new byte[11]);});
    static byte[] Cp950(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}

    // 編的食譜表（與 recipe.tbl 同格式，內容不是遊戲資料）。
    const string Table="5\r\n\"$901\" \"$900 $901\" 0 3 1 \"$950\" 2 \r\n\"$902\" \"$900 $902\" 9 3 2 \"$951\" 1 \"$952\" 3 \r\n\"$903\" \"$900 $903\" 1 3 0\r\n\"$904\" \"$900 $904\" 80 3 1 \"$953\" 1\r\n\"$905\" \"$900 $905\" 7 3 1 \"$950\" 1\r\n";
    static IReadOnlyList<CookRecipe> Recipes=>CookRecipeTable.Parse(Cp950(Table));
    static string Names(string s)=>System.Text.RegularExpressions.Regex.Replace(s,@"\$(\d+)",m=>"名"+m.Groups[1].Value);

    [WorldModuleTest("cook-book-followup")]
    static void Run(WorldModuleTestContext t){
        Wiring(t);Parse(t);Toggle(t);Send(t);Panel(t);Fallback(t);
    }

    static void Wiring(WorldModuleTestContext t){
        t.Check(typeof(IUiClosablePanel).IsAssignableFrom(typeof(CookBookPanel)),"CookBookPanel implements IUiClosablePanel");
        t.Check(typeof(WorldSession).GetMethod("CloseCookBook",Type.EmptyTypes)==null,"parameterless CloseCookBook removed");
        t.Check(typeof(WorldSession).GetMethod("CloseCookBook",[typeof(long)])!=null,"revision CloseCookBook kept");
        t.Check(typeof(ClientForm).GetMethod("SyncCookBookFallback",F|BindingFlags.DeclaredOnly)!=null,"ClientForm fallback sync declared");
        t.Check(typeof(LoginConnection).GetMethod("CookRecipe",[typeof(long),typeof(int)])!=null,"LoginConnection.CookRecipe present");
    }

    static void Parse(WorldModuleTestContext t){
        var r=Recipes;
        t.Equal(5,r.Count,"5 recipes parsed");
        t.Check(r[0] is {Name:"$901",FullName:"$900 $901",Index:0,Column4:3}&&r[0].Materials.Single()==new CookMaterial("$950",2),"first recipe fields",r[0]);
        t.Check(r[1].Index==9&&r[1].Materials.Count==2&&r[1].Materials[1]==new CookMaterial("$952",3),"two materials",r[1]);
        t.Check(r[2].Materials.Count==0,"zero materials allowed");
        t.Equal(80,r[3].Index,"special index kept");
        t.Throws<InvalidDataException>(()=>CookRecipeTable.Parse(Cp950("3\n\"$1\" \"$1\" 0 3 0\n")),"count mismatch rejected");
        t.Throws<InvalidDataException>(()=>CookRecipeTable.Parse(Cp950("1\n\"$1\" \"$1\" 0 3 2 \"$2\" 1\n")),"material count mismatch rejected");
        t.Throws<InvalidDataException>(()=>CookRecipeTable.Parse(Cp950("2\n\"$1\" \"$1\" 4 3 0\n\"$2\" \"$2\" 4 3 0\n")),"duplicate index rejected");
        t.Throws<InvalidDataException>(()=>CookRecipeTable.Parse(Cp950("1\n\"$1\" \"$1\" 256 3 0\n")),"index beyond one byte rejected");
        t.Throws<InvalidDataException>(()=>CookRecipeTable.Parse(Cp950("1\n\"$1 \"$1\" 0 3 0\n")),"unterminated quote rejected");
        t.Throws<InvalidDataException>(()=>CookRecipeTable.Parse(Cp950("1\n$1 \"$1\" 0 3 0\n")),"unquoted name rejected");
        t.Throws<InvalidDataException>(()=>CookRecipeTable.Parse(Cp950("x\n")),"first line must be a count");

        // 篩選（推測：等級 L 列編號 < 8L）、編號排序、名稱代換。
        var l1=CookRecipeTable.Rows(1,Names,r);
        t.Check(l1.Select(x=>x.Index).SequenceEqual([0,1,7]),"level 1 lists indexes 0..7 in order",l1);
        t.Equal("名901",l1[0].Name,"name resolved through name table");
        t.Equal("名950 ×2",l1[0].Materials,"materials resolved");
        t.Equal("",l1[1].Materials,"no materials → empty text");
        t.Check(CookRecipeTable.Rows(2,Names,r).Select(x=>x.Index).SequenceEqual([0,1,7,9]),"level 2 adds 8..15");
        t.Check(!CookRecipeTable.Rows(11,Names,r).Any(x=>x.Index==80),"special recipe 80 not listed (unverified)");
        t.Equal("名951 ×1、名952 ×3",CookRecipeTable.Rows(2,Names,r).Single(x=>x.Index==9).Materials,"materials joined");
        t.Check(CookRecipeTable.Rows(0,Names,r).Count==0,"level 0 → empty");
        using(CookRecipeTable.UseTable(null))t.Check(CookRecipeTable.Rows(3,Names).Count==0,"no table → empty list");
        using(CookRecipeTable.UseTable(r))t.Equal(3,CookRecipeTable.Rows(1,Names).Count,"table override used");
    }

    static void Toggle(WorldModuleTestContext t){
        var s=new WorldSession();
        s.Receive(Cook(1,2),"");var first=s.CookBook;
        t.Check(first.Open&&first.Level==3,"open level 3",first);
        s.Receive(Cook(1,2),"");
        t.Check(!s.CookBook.Open&&s.CookBook.Revision>first.Revision,"same level while open closes (PBW§2.3)",s.CookBook);
        s.Receive(Cook(1,2),"");t.Check(s.CookBook.Open,"same level while closed opens again");
        s.Receive(Cook(1,3),"");t.Check(s.CookBook is {Open:true,Level:4},"different level switches",s.CookBook);
        t.Equal(0u,s.CookBook.BookObjectId,"no selected cooking book → book unknown");
        t.Check(JsonSerializer.SerializeToElement(s.Snapshot()).GetProperty("extensions").GetProperty("cooking").TryGetProperty("bookObjectId",out _),"snapshot has bookObjectId");
    }

    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }

    static void Send(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;
        w.Receive(Item(Book,248,1,"book a"),"offline");w.Receive(Item(OtherBook,248,1,"book b"),"offline");w.Receive(Item(Stone,0,3,"stone"),"offline");
        t.Equal(3,w.Inventory.Length,"synthetic inventory");
        // 沒選料理書時開窗：兩本料理書 → 不知道是哪一本，不送。
        w.SelectItem(Stone);w.Receive(Cook(1,0),"offline");
        t.Equal(0u,w.CookBook.BookObjectId,"book unknown when a non-book is selected");
        t.Check(w.CookingBookItem(w.CookBook)==null,"two books and none recorded → no target");
        t.Check(!c.CookRecipe(w.CookBook.Revision,0).GetAwaiter().GetResult()&&sent.Count==0,"no book target → nothing sent");
        w.CloseCookBook(w.CookBook.Revision);

        // 選 Book 後開窗：記下那一本。
        w.SelectItem(Book);w.Receive(Cook(1,1),"offline");var open=w.CookBook;
        t.Check(open is {Open:true,Level:2,BookObjectId:Book},"selected cooking book recorded",open);
        t.Equal(Book,w.CookingBookItem(open)?.ObjectId,"recorded book is the target");
        t.Check(!c.CookRecipe(open.Revision-1,3).GetAwaiter().GetResult()&&sent.Count==0,"stale revision → nothing sent");
        t.Check(!c.CookRecipe(open.Revision,256).GetAwaiter().GetResult()&&sent.Count==0,"recipe beyond one byte → nothing sent");
        // 使用者之後選了別的道具：製作時改回選料理書再送。
        w.SelectItem(Stone);
        bool ok=c.CookRecipe(open.Revision,9).GetAwaiter().GetResult();
        t.Check(ok&&sent.Count==1,"cook request sent once",new{ok,sent=sent.Count,notice=w.ItemNotice});
        if(sent.Count==1)t.Equal(Convert.ToHexString(LoginPackets.Pad([164,..BitConverter.GetBytes(Book),1,9])),Convert.ToHexString(sent[0]),"C_USE_ITEM D(book) C(1) C(recipe)");
        t.Equal((uint?)Book,w.SelectedItemId,"book selected for the send");
        t.Check(c.PendingItemUse==null,"pending item use cleared after send");
        t.Check(w.CookBook.Open,"cooking does not close the window (server sends no S250 52 for make)");

        // WorldCookBookSource：清單跟著等級、名稱代換、製作轉給回呼。
        var cooks=new List<(long,int)>();
        using(CookRecipeTable.UseTable(Recipes)){
            var src=new WorldCookBookSource(()=>w,Names,(rev,r)=>{cooks.Add((rev,r));return Task.FromResult(true);});
            var v=src.Current!;
            t.Check(v.Recipes!.Select(x=>x.Index).SequenceEqual([0,1,7,9]),"source lists level 2 recipes",v.Recipes);
            t.Check(ReferenceEquals(v.Recipes,src.Current!.Recipes),"rows cached per revision");
            t.Check(src.Cook(v.Revision,9)&&cooks.SequenceEqual([(v.Revision,9)]),"source forwards cook");
            t.Check(!new WorldCookBookSource(()=>w).Cook(v.Revision,9),"source without callback refuses cook");
            src.Close(v.Revision);t.Check(src.Current==null&&!w.CookBook.Open,"source close closes the session window");
        }
        // 料理書不在背包了：記下的那本找不到就不送（不改送另一本）。
        w.Receive(Cook(1,1),"offline");
        t.Equal(Book,w.CookBook.BookObjectId,"reopen with book selected records it again");
        w.Receive(LoginPackets.Pad([57,..BitConverter.GetBytes(Book)]),"offline");
        t.Check(w.Inventory.All(i=>i.ObjectId!=Book)&&w.CookingBookItem(w.CookBook)==null,"recorded book gone → no target (other book not substituted)");
    }

    // 與 uiextra86 COOK_BOOK_WINDOW 相同的元件種類與名稱，數字刻意不同（不複製遊戲資源）。
    static string CookXml()=>$"""
<?xml version="1.0"?>
<root>
<COOK_BOOK_WINDOW x="10" y="20">
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_COOK_BOOK_BASE" surfNumber="100" xPos="6" yPos="8"/>
	<SURF_IMAGE surfName="SURF_RENEWAL_WIN_COOK_BOOK_FRAME" surfNumber="101"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_BTN_QUIT_X" surfNumber="110" xPos="90" yPos="10"/>
	<IMAGE surfName="SURF_RENEWAL_WIN_SCROLLBAR" surfNumber="120" xPos="80" yPos="30"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_DEC_BUT_OFF" surfNumber="121" xPos="80" yPos="70"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_INC_BUT_OFF" surfNumber="122" xPos="80" yPos="22"/>
	<IMAGE_BTN surfName="SURF_RENEWAL_WIN_KNOB_BUT_OFF" surfNumber="123"/>
{string.Concat(Enumerable.Range(1,11).Select(i=>$"\t<IMAGE surfName=\"SURF_RENEWAL_IMG_COOK_BOOK_{i}\" surfNumber=\"{200+i}\" xPos=\"50\" yPos=\"80\"/>\n"))}	<IMAGE surfName="SURF_RENEWAL_IMG_COOK_BOOK_GUAGE" surfNumber="130" xPos="12" yPos="76"/>
</COOK_BOOK_WINDOW>
</root>
""";
    static Func<int,UiSurface?> CookSurfaces=>id=>id switch{
        100=>Surf(100,96,90,0xFF203040),110=>Surf(110,8,8,0xFF010101),
        120=>Surf(120,8,40,0xFF111111),121 or 122=>Surf(id,8,8,0xFF222222),
        130=>Surf(130,33,6,0xFF445566),
        >200 and <=211=>Surf(id,4,4,0xFF000000|(uint)((id-200)*0x10)),
        _=>null};

    sealed class FakeSource:ICookBookSource {
        public CookBookView? Current{get;set;}
        public List<long> Closed{get;}=[];
        public List<(long Revision,int Recipe)> Cooks{get;}=[];
        public void Close(long revision){Closed.Add(revision);Current=null;}
        public bool Cook(long revision,int recipe){Cooks.Add((revision,recipe));return true;}
    }
    sealed class DefaultSource:ICookBookSource {
        public CookBookView? Current=>null;
        public void Close(long revision){}
    }

    static void Panel(WorldModuleTestContext t){
        var p=new CookBookPanel();p.Load(UiLayout.Parse(CookXml(),"synthetic-uiextra86.xml"),CookSurfaces);var cmd=new UiNullCommands();
        // 清單區：底左緣 6+8=14 到捲軸左緣 80−2=78；上鈕頂 22 到下鈕底 70+8=78 → 56 px／15 = 3 列。
        t.Equal(Rectangle.FromLTRB(14,22,78,78),p.ListLocal,"list area between base, scrollbar and buttons (inferred)");
        t.Equal(3,p.VisibleRows,"3 visible rows");
        t.Check(!((ICookBookSource)new DefaultSource()).Cook(1,1),"default ICookBookSource.Cook refuses");

        var rows=Enumerable.Range(0,5).Select(i=>new CookBookRecipe(i*2,"料理"+i,"")).ToArray();
        var src=new FakeSource{Current=new(1,0,1,rows)};p.Source=src;
        p.Update(new UiStaticGameState(InWorld:true));
        t.Check(p.Visible&&p.Scroll==0&&p.Selected==-1,"opens at top with nothing selected");
        t.Check(p.VisibleRecipes.Select(r=>r.Name).SequenceEqual(["料理0","料理1","料理2"]),"first 3 rows visible");
        t.Equal(2,p.MaxScroll,"max scroll = 5 − 3");
        t.Equal(new Rectangle(10+14,20+22+15,64,15),p.RowRect(1),"row 1 rect");

        // 上下鈕與滾輪。
        int downX=10+80+2,downY=20+70+2,upX=10+80+2,upY=20+22+2;
        Click(p,downX,downY,cmd);t.Equal(1,p.Scroll,"down button scrolls one row");
        Click(p,downX,downY,cmd);Click(p,downX,downY,cmd);t.Equal(2,p.Scroll,"scroll clamps at max");
        Click(p,upX,upY,cmd);t.Equal(1,p.Scroll,"up button scrolls back");
        t.Check(p.Mouse(M(UiMouseKind.Wheel,30,40,0,120),cmd)&&p.Scroll==0,"wheel up scrolls up");
        t.Check(p.Mouse(M(UiMouseKind.Wheel,30,40,0,-120),cmd)&&p.Scroll==1,"wheel down scrolls down");
        t.Check(!p.Mouse(M(UiMouseKind.Wheel,500,500,0,-120),cmd)&&p.Scroll==1,"wheel outside ignored");
        t.Check(src.Cooks.Count==0&&src.Closed.Count==0,"scrolling neither cooks nor closes");

        // 選取與製作（scroll 1：第 0 列是清單索引 1）。
        var row0=p.RowRect(0);var row1=p.RowRect(1);
        Click(p,row0.X+3,row0.Y+3,cmd);
        t.Check(p.Selected==1&&p.SelectedRecipe?.Index==2&&src.Cooks.Count==0,"first click selects",p.Selected);
        Click(p,row1.X+3,row1.Y+3,cmd);t.Check(p.Selected==2&&src.Cooks.Count==0,"click another row moves selection");
        Click(p,row1.X+3,row1.Y+3,cmd);t.Check(src.Cooks.SequenceEqual([(1L,4)]),"second click on the selected row cooks its index",src.Cooks);
        p.Mouse(M(UiMouseKind.Down,row1.X+3,row1.Y+3,clicks:2),cmd);p.Mouse(M(UiMouseKind.Up,row1.X+3,row1.Y+3),cmd);
        t.Equal(2,src.Cooks.Count,"double click cooks once");
        p.Mouse(M(UiMouseKind.Down,row1.X+3,row1.Y+3),cmd);p.Mouse(M(UiMouseKind.Up,row0.X+3,row0.Y+3),cmd);
        t.Check(src.Cooks.Count==2&&p.Selected==2,"press and release on different rows does nothing");
        var below=p.RowRect(3);Click(p,below.X+3,below.Y+3,cmd);
        t.Check(src.Cooks.Count==2&&p.Selected==2&&p.Visible,"click below the rows does nothing");

        // 量表：等級 1 → 33×1/11＝3 px（推測）；之後不畫量表、露出底圖。
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
            var g0=canvas.Bitmap.GetPixel(10+12+1,20+76+1);var g5=canvas.Bitmap.GetPixel(10+12+5,20+76+1);
            t.Check(g0.R==0x44&&g0.G==0x55&&g0.B==0x66,"gauge filled part painted",g0);
            t.Check(g5.R==0x20&&g5.G==0x30&&g5.B==0x40,"gauge beyond level/11 not painted",g5);
        }
        t.Equal(1/11.0,p.GaugeFill,"gauge fill = level / 11");
        src.Current=new(1,10,11,rows);p.Update(new UiStaticGameState(InWorld:true));t.Equal(1.0,p.GaugeFill,"level 11 fills the gauge");
        t.Check(p.Selected==2&&p.Scroll==1,"same revision keeps selection and scroll");

        // 新的一次開窗：捲回頂端、取消選取；清單變短時捲動夾住。
        src.Current=new(2,0,1,rows[..2]);p.Update(new UiStaticGameState(InWorld:true));
        t.Check(p.Scroll==0&&p.Selected==-1&&p.MaxScroll==0,"new revision resets scroll and selection");
        src.Current=new(3,0,1,null);p.Update(new UiStaticGameState(InWorld:true));
        t.Check(p.Visible&&p.Recipes.Count==0&&p.RowAt(new(row0.X+3,row0.Y+3))==-1,"no table → empty list, window still opens");

        // IUiClosablePanel：被 4 層堆疊擠出時與關閉 X 相同。
        ((IUiClosablePanel)p).Close();
        t.Check(!p.Visible&&src.Closed.SequenceEqual([3L]),"IUiClosablePanel.Close closes this revision",src.Closed);
        p.Update(new UiStaticGameState(InWorld:true));t.Check(!p.Visible,"closed revision stays closed");
        ((IUiClosablePanel)p).Close();t.Equal(1,src.Closed.Count,"closing a closed window is a no-op");
    }

    static void Fallback(WorldModuleTestContext t){
        var open=new CookBookState(true,1,2,CookingProtocol.ServerMagic,5,Book);
        t.Check(CookBookFallbackView.Wanted(false,true,open,-1),"native UI off + open book → fallback shown");
        t.Check(!CookBookFallbackView.Wanted(true,true,open,-1),"native UI on → no fallback");
        t.Check(!CookBookFallbackView.Wanted(false,false,open,-1),"not in world → no fallback");
        t.Check(!CookBookFallbackView.Wanted(false,true,open with{Open=false},-1),"closed book → no fallback");
        t.Check(!CookBookFallbackView.Wanted(false,true,open,5),"dismissed revision stays hidden");
        t.Check(CookBookFallbackView.Wanted(false,true,open with{Revision=6},5),"new revision shows again");
        t.Equal("料理書（等級 2，3 道）",CookBookFallbackView.Caption(open,3),"caption");
        var rows=CookRecipeTable.Rows(2,Names,Recipes);
        t.Check(CookBookFallbackView.Cells(rows[3]).SequenceEqual(["9","名902","名951 ×1、名952 ×3"]),"cells: index, name, materials");

        using var window=new CookBookFallbackWindow();
        var cooks=new List<(long,int)>();window.Cook=(rev,r)=>{cooks.Add((rev,r));return Task.CompletedTask;};
        window.SetBook(open,rows,tableLoaded:true);
        t.Check(window.Revision==5&&window.RowCount==4&&window.Cell(3,1)=="名902"&&window.Cell(0,0)=="0","rows filled",new{window.Revision,window.RowCount});
        t.Equal("料理書（等級 2，4 道）",window.Text,"window caption");
        t.Check(window.SelectedRecipe==null,"nothing selected initially");
        window.SelectRow(3);t.Equal((int?)9,window.SelectedRecipe?.Index,"selected row maps to recipe index");
        window.SetBook(open with{Revision=6},rows.Take(1).ToArray(),tableLoaded:false);
        t.Check(window.Revision==6&&window.RowCount==1&&window.SelectedRecipe==null,"new revision replaces rows and clears selection");
    }
}
