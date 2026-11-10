using System.Text.Json;
namespace P06.NativeUi;

// T323 (#761) agit-auction-ui 離線測試：只用合成版面、合成單色圖與合成 S156 封包；不讀主客戶端或參考客戶端的
// AgitAuctionUI.xml、不啟動遊戲、不連網路。CI 由 --module-tests 自動發現執行。
static class AgitAuctionPanelTests {
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.png","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));

    // Synthetic layout with the element structure of the reference AgitAuctionUI.xml (own coordinates and image numbers).
    const string Xml="""
        <?xml version="1.0"?>
        <AgitAuctionUI>
          <Window Name = "Bid_Window" X="0" Y="0" BaseSurf = "800;1" Moveable = "20" MouseEvent="Dummy">
            <Edit Name = "Price_Edit" X = "65" Y = "64" Width = "56" Height = "13" OnlyNumber = "1"/>
          </Window>
          <Window Name = "AuctionInfo_Window" X = "10" Y = "10" MouseEvent = "Dummy">
            <MultiLabel Name = "Info_Label" X = "10" Y = "0" Width = "260" Height = "90"/>
          </Window>
          <Window Name = "Agitmap_Window" X = "100" Y = "50" Width = "300" Height = "200" Moveable = "20" BaseSurf = "900" FrameSurf = "901;1" MouseEvent = "Dummy">
            <Button Name = "Exit_Button" X = "280" Y = "4" BasePng = "910;1" PushPng = "911;1" MouseEvent = "Exit_AgitWin"/>
            <Control Name = "Auction_List" X = "2" Y = "100">
              <Button Name = "Name_Button" X = "0" Y = "2" BasePng = "920" PushPng = "921" MouseEvent = "Name_AuctionList"/>
              <List Name = "Items_List" X = "0" Y = "21" Width = "207" Height = "36" Vertical = "1" Gap = "0">
                <CheckButton Name = "Item0_Button" Width ="180" Height="12" PushCheck = "0" MouseEvent = "Item_AuctionList">
                  <Label Name="Name_Label" X = "0" Y = "0" Width = "77" Height = "12" Align = "center" Caption = "Name;1"/>
                  <Label Name="Price_Label" X = "79" Y = "0" Width = "58" Height = "12" Align = "right" Caption = "Price;1"/>
                  <Label Name="Deadline_Label" X = "140" Y = "0" Width = "40" Height = "12" Align = "center" Caption = "Deadline;1"/>
                </CheckButton>
                <CheckButton Name = "Item1_Button" Width ="180" Height="12" PushCheck = "0" MouseEvent = "Item_AuctionList">
                  <Label Name="Name_Label" X = "0" Y = "0" Width = "77" Height = "12" Align = "center" Caption = "Name;1"/>
                  <Label Name="Price_Label" X = "79" Y = "0" Width = "58" Height = "12" Align = "right" Caption = "Price;1"/>
                </CheckButton>
                <CheckButton Name = "Item2_Button" Width ="180" Height="12" PushCheck = "0" MouseEvent = "Item_AuctionList">
                  <Label Name="Name_Label" X = "0" Y = "0" Width = "77" Height = "12" Align = "center" Caption = "Name;1"/>
                  <Image Name = "Mark_Image" Png = "970"/>
                </CheckButton>
              </List>
              <ScrollBar Name = "ItemList_Scrollbar" X = "183" Y = "22" Width = "11" Height = "40" ScrollStart = "10" ScrollHeight = "20" Vertical = "1" MouseEvent = "Scroll_AuctionList">
                <Button Name = "Up_Button" Y = "0" BasePng = "930" PushPng = "930" MouseEvent = "Scroll_AuctionList"/>
                <Button Name = "Move_Button" Y = "10" BasePng = "931;1" PushPng = "931;1" MouseEvent = "Scroll_AuctionList"/>
                <Button Name = "Down_Button" Y = "30" BasePng = "932" PushPng = "999" MouseEvent = "Scroll_AuctionList"/>
              </ScrollBar>
              <Image Name = "Back_Image" Png = "940"/>
            </Control>
            <Control Name = "MapImage_Control">
              <Control Name = "Gludiomap_Image">
                <Button Name = "Agithouse0_Image" X = "220" Y = "30" BasePng = "950;1" HighlightPng = "951;1" PushPng = "952;1" MouseEvent = "Auction_AgitWin"/>
                <Button Name = "Agithouse1_Image" X = "240" Y = "30" BasePng = "950;1" HighlightPng = "951;1" PushPng = "952;1" MouseEvent = "Auction_AgitWin"/>
                <Button Name = "Agithouse2_Image" X = "260" Y = "30" BasePng = "950;1" HighlightPng = "951;1" PushPng = "952;1" MouseEvent = "Auction_AgitWin"/>
                <Button Name = "Agithouse3_Image" X = "200" Y = "160" BasePng = "950;1" HighlightPng = "951;1" PushPng = "952;1" MouseEvent = "Auction_AgitWin"/>
              </Control>
              <Control Name = "Giranmap_Image">
                <Button Name = "Agithouse0_Image" X = "220" Y = "60" BasePng = "950;1" HighlightPng = "951;1" PushPng = "952;1" MouseEvent = "Auction_AgitWin"/>
              </Control>
            </Control>
            <Button Name = "List_Button" X = "2" Y = "176" BasePng = "960" PushPng = "961" MouseEvent = "List_AgitWin">
              <Label Name = "Button_Name" Y = "2" Width = "100" Height = "20" Align = "center" String = "4391;1"/>
            </Button>
            <Control Name = "Back_Control" Width = "300" Height = "200" MouseEvent = "MapClick_AgitWin"/>
            <Image Name = "ExitBack_Image" X = "270" Y = "2" Png = "915;1"/>
          </Window>
        </AgitAuctionUI>
        """;

    const uint Base=0xFF102030,Frame=0xFF0A0A0A,Exit=0xFF00FF00,ExitBack=0xFF003300,Header=0xFF999999,Back=0xFF203040,
        Arrow=0xFF0000FF,Thumb=0xFF00FFFF,House=0xFF800000,HouseHi=0xFFAA0000,HousePush=0xFFFF0000,ListBtn=0xFF404000;
    static readonly Dictionary<int,UiSurface> Surfs=new(){[900]=Surface(900,300,200,Base),[901]=Surface(901,4,4,Frame)};
    static readonly Dictionary<int,UiSurface> Pngs=new(){
        [910]=Surface(910,12,12,Exit),[911]=Surface(911,12,12,0xFF00AA00),[915]=Surface(915,30,16,ExitBack),
        [920]=Surface(920,70,16,Header),[921]=Surface(921,70,16,0xFF777777),[940]=Surface(940,207,70,Back),
        [930]=Surface(930,11,10,Arrow),[931]=Surface(931,11,5,Thumb),[932]=Surface(932,11,10,Arrow),
        [950]=Surface(950,8,8,House),[951]=Surface(951,8,8,HouseHi),[952]=Surface(952,8,8,HousePush),
        [960]=Surface(960,100,20,ListBtn),[961]=Surface(961,100,20,0xFF606000)};   // 999 deliberately missing (970 is in a row, never looked up)
    static UiLayout Layout(string text=Xml)=>UiLayout.Parse(text,"AgitAuctionUI.xml");
    static AgitAuctionPanel Loaded(Func<AgitAuctionOpenRequest?> src,Func<int,UiSurface?>? surf=null,string text=Xml){
        var p=new AgitAuctionPanel(null,src);p.Load(Layout(text),surf??(id=>Surfs.GetValueOrDefault(id)),id=>Pngs.GetValueOrDefault(id));return p;
    }
    sealed class Source{public AgitAuctionOpenRequest? Current;long rev=100;public void Open(AgitHouseList l)=>Current=new(++rev,l);}
    static AgitHouseEntry H(int region,int number,string name,int price=1000,byte month=12,byte day=25)=>new((uint)(region<<16|number),name,78,month,day,price);
    static AgitHouseList Gludio()=>new(0x1234,[H(8,1,"甲屋",2_000_000),H(8,2,"乙屋",-1,1,3),H(8,3,"丙屋"),H(8,9,"丁屋"),H(8,4,"戊屋")]);
    static readonly UiStaticGameState World=new(InWorld:true);
    static UiMouseEvent M(UiMouseKind k,int x,int y,MouseButtons b=MouseButtons.Left,int delta=0)=>new(k,new Point(x,y),b,1,delta,Keys.None);
    static void Click(AgitAuctionPanel p,int x,int y,UiNullCommands? c=null){c??=new();p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
    static void Click(AgitAuctionPanel p,Rectangle r,UiNullCommands? c=null)=>Click(p,r.X+r.Width/2,r.Y+r.Height/2,c);
    static int Pixel(AgitAuctionPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static int Argb(uint v)=>Color.FromArgb(unchecked((int)v)).ToArgb();
    static AgitAuctionPiece Piece(AgitAuctionPanel p,string name,int region=0)=>p.Pieces.First(x=>x.Name==name&&x.Region==region);

    [WorldModuleTest("agit-auction-ui")]
    static void Run(WorldModuleTestContext t){
        try{
            LoadLayout(t);
            Broken(t);
            OpenAndRows(t);
            MouseAndPaint(t);
            Observer(t);
            Registration(t);
        }finally{AgitAuctionWindow.Clear();}
    }

    static void LoadLayout(WorldModuleTestContext t){
        var p=Loaded(()=>null);
        t.Check(p.Available,"synthetic layout usable",p.Unavailable);
        t.Check(!p.Visible&&p.Bounds.IsEmpty,"closed after load");
        t.Equal(new Point(100,50),p.Origin,"window X/Y");
        t.Equal(new Size(300,200),p.Size,"declared Width/Height");
        t.Equal(20,p.MoveableHeight,"Moveable");
        t.Check(p.BaseImage?.Base?.Id==900&&p.FrameImage?.Base is {Id:901,Flag:1,IsSurf:true},"BaseSurf / FrameSurf");
        t.Equal(3,p.RowCount,"three list rows");
        var cell=p.Pieces.Single(x=>x.Kind==AgitAuctionPieceKind.Cell&&x.Row==2&&x.Name==AgitAuctionPanel.NameCell);
        t.Equal(new Point(2,100+21+24),cell.Offset,"rows stack by Height + Gap (control + list + n×12)");
        var price=p.Pieces.Single(x=>x.Kind==AgitAuctionPieceKind.Cell&&x.Row==0&&x.Name==AgitAuctionPanel.PriceCell);
        t.Check(price.Offset==new Point(2+79,121)&&price.Align=="right"&&price.DeclaredSize==new Size(58,12),"price cell geometry");
        t.Equal(new AgitAuctionScroll(new Point(185,122),10,20),p.Scroll,"scroll bar track");
        t.Equal(new Point(185,122+30),Piece(p,"Down_Button").Offset,"scroll buttons relative to the bar");
        t.Check(p.Regions.OrderBy(r=>r).SequenceEqual([4,8]),"town controls → regions (Giran 4, Gludio 8)");
        var houses=p.Pieces.Where(x=>x.HouseId!=0).ToList();
        t.Check(houses.Count==5&&houses.Count(x=>x.Region==8)==4,"house buttons per town");
        t.Check(p.Pieces.Where(x=>x.InList).Select(x=>x.Name).Contains("Back_Image")&&!Piece(p,"List_Button").InList&&p.Pieces.Single(x=>x.Kind==AgitAuctionPieceKind.Row&&x.Row==0).InList,"Auction_List pieces tagged");
        t.Equal(8u<<16|2,Piece(p,"Agithouse1_Image",8).HouseId,"Agithouse1 = house number 2");
        t.Check(Piece(p,"Exit_Button").HighlightSurface==null&&Piece(p,"Agithouse0_Image",8).HighlightSurface!=null,"highlight images");
        var label=Piece(p,"Button_Name");
        t.Check(label.Kind==AgitAuctionPieceKind.Label&&label.TextKind=="String"&&label.TextValue=="4391;1"&&label.Offset==new Point(2,178),"List_Button label keeps its String id");
        t.Check(p.Missing.Count==1&&p.Missing[0].StartsWith("png 999 ",StringComparison.Ordinal)&&p.Missing[0].EndsWith("/@PushPng)",StringComparison.Ordinal),"missing image listed",p.Missing);
        t.Check(p.Skipped.Any(s=>s.Contains("CheckButton[2]/Image[0]")),"row images not built (970 not looked up)",p.Skipped);
        t.Check(p.Skipped.Any(s=>s.Contains("Bid_Window"))&&p.Skipped.Any(s=>s.Contains("AuctionInfo_Window")),"other windows skipped",p.Skipped);
        t.Check(p.Skipped.Any(s=>s.Contains("點擊底層")),"Back_Control skipped",p.Skipped);
        // Reverse XML order: ExitBack_Image (last child) painted first, Exit_Button (first child) last.
        int I(string n)=>p.Pieces.ToList().FindIndex(x=>x.Name==n);
        t.Check(I("ExitBack_Image")<I("List_Button")&&I("List_Button")<I("Exit_Button")&&I("Back_Image")<I("Name_Button"),"children painted in reverse XML order");
        t.Check(I("List_Button")<I("Button_Name"),"button label painted right after its button");
        t.Check(AgitAuctionPanel.ImageAttr(Layout().Root.ChildrenNamed("Window").First(),"BaseSurf")==new AgitAuctionImageRef("BaseSurf",800,1),"id;flag parsed");
    }

    static void Broken(WorldModuleTestContext t){
        t.Throws<UiLayoutException>(()=>Loaded(()=>null,text:Xml.Replace("\"Agitmap_Window\"","\"Other\"")),"no Agitmap_Window");
        t.Throws<UiLayoutException>(()=>Loaded(()=>null,text:Xml.Replace("BasePng = \"920\"","BasePng = \"x\"")),"bad image attr");
        t.Throws<UiLayoutException>(()=>Loaded(()=>null,text:Xml.Replace("Name = \"Item1_Button\" Width =\"180\" Height=\"12\"","Name = \"Item1_Button\" Width =\"180\"")),"row without Height");
        t.Throws<UiLayoutException>(()=>Loaded(()=>null,text:"<AgitAuctionUI><Window Name=\"Agitmap_Window\" X=\"0\" Y=\"0\" Width=\"10\" Height=\"10\"/></AgitAuctionUI>"),"no list rows");
        t.Throws<UiLayoutException>(()=>Loaded(()=>null,text:Xml.Replace("Name = \"Giranmap_Image\"","Name = \"Gludiomap_Image\"")),"duplicate town");
        t.Throws<UiLayoutException>(()=>Loaded(()=>null,text:Xml.Replace("X = \"100\" Y = \"50\" Width","Y = \"50\" Width")),"window X missing → no invented origin");
        var noSize=Loaded(()=>null,id=>id==900?null:Surfs.GetValueOrDefault(id),Xml.Replace("Width = \"300\" Height = \"200\" Moveable","Moveable"));
        t.Check(!noSize.Available&&noSize.Unavailable!.Contains("底圖缺")&&!noSize.Open(),"no declared size and no BaseSurf → unavailable, no substitute");
        var noBase=Loaded(()=>null,id=>id==900?null:Surfs.GetValueOrDefault(id));
        t.Check(noBase.Available&&noBase.Missing.Any(m=>m.StartsWith("surf 900 ",StringComparison.Ordinal)),"declared size: missing BaseSurf is listed and not drawn");
        var src=new Source();src.Open(Gludio());
        var host=new AgitAuctionPanel(null,()=>src.Current);host.Load(Layout(),(SurfCatalog)null!);   // returns before touching the catalog
        host.Update(World);
        t.Check(!host.Available&&host.Unavailable=="沒有 UiResources"&&!host.Visible,"product Load without resources → unavailable, S156 does not open it");
    }

    static void OpenAndRows(WorldModuleTestContext t){
        var src=new Source();var p=Loaded(()=>src.Current);
        p.Update(World);
        t.Check(!p.Visible&&!p.Open(),"no S156 → stays closed");
        src.Open(Gludio());
        t.Check(!p.Visible,"opens only in Update");
        p.Update(World);
        t.Check(p.Visible&&p.Shown==src.Current&&p.ActiveRegion==8,"S156 opens on the town of the houses");
        t.Equal(new Rectangle(100,50,300,200),p.Bounds,"bounds");
        t.Check(p.RowHouse(0)?.Name=="甲屋"&&p.RowHouse(2)?.Name=="丙屋"&&p.RowHouse(3)==null,"rows show the first houses");
        t.Equal(2,p.MaxScroll,"5 houses, 3 rows");
        t.Equal("2000000",AgitAuctionPanel.CellText(p.Houses[0],AgitAuctionPanel.PriceCell),"price digits");
        t.Equal("-1",AgitAuctionPanel.CellText(p.Houses[1],AgitAuctionPanel.PriceCell),"signed price as sent");
        t.Equal("1/3",AgitAuctionPanel.CellText(p.Houses[1],AgitAuctionPanel.DeadlineCell),"deadline month/day");
        t.Equal("乙屋",AgitAuctionPanel.CellText(p.Houses[1],AgitAuctionPanel.NameCell),"name");
        t.Check(p.ScrollBy(5)&&p.ScrollOffset==2&&!p.ScrollBy(1)&&p.RowHouse(0)?.Name=="丙屋","scroll clamps to MaxScroll");
        t.Check(p.Select(8u<<16|1)&&p.ScrollOffset==0&&p.SelectedHouse==(8u<<16|1),"select scrolls the house into view");
        t.Check(!p.Select(8u<<16|7)&&p.SelectedHouse==(8u<<16|1),"house not listed → not selectable");
        src.Open(new(5,[H(4,1,"奇岩屋")]));p.Update(World);
        t.Check(p.Visible&&p.ActiveRegion==4&&p.Houses.Count==1&&p.SelectedHouse==null&&p.MaxScroll==0,"new S156 replaces the list and the town");
        src.Open(new(6,[H(6,1,"未知")]));p.Update(World);
        t.Check(p.Visible&&p.ActiveRegion==0&&!p.Pieces.Any(x=>x.HouseId!=0&&x.Region==p.ActiveRegion),"region without a town Control → no house buttons");
        src.Open(new(7,[]));p.Update(World);
        t.Check(p.Visible&&p.RowHouse(0)==null,"empty S156 opens an empty list");
        p.Close();
        t.Check(!p.Visible,"closed");
        p.Update(World);
        t.Check(!p.Visible,"same request does not reopen after close");
        t.Check(p.Toggle()&&p.Visible,"Toggle re-opens the last list");
        p.Update(World);
        t.Check(p.Visible,"stays open after re-open");
        p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible,"leaving the world closes");
        src.Open(Gludio());p.Update(World);
        src.Current=null;p.Update(World);
        t.Check(!p.Visible&&p.Shown==null,"request cleared (WorldSession.Reset) closes");
    }

    static void MouseAndPaint(WorldModuleTestContext t){
        var src=new Source();var p=Loaded(()=>src.Current);var cmd=new UiNullCommands();
        src.Open(Gludio());p.Update(World);
        t.Equal(Argb(Base),Pixel(p,100+150,50+60),"BaseSurf drawn");
        t.Equal(Argb(Frame),Pixel(p,101,51),"FrameSurf at the window origin, over the base");
        t.Equal(Argb(Exit),Pixel(p,100+281,50+5),"Exit_Button above ExitBack_Image");
        t.Equal(Argb(ExitBack),Pixel(p,100+271,50+3),"ExitBack_Image drawn");
        t.Equal(Argb(Header),Pixel(p,100+3,50+103),"header button above Back_Image");
        t.Equal(Argb(Back),Pixel(p,100+100,50+165),"Back_Image under the list");
        t.Equal(Argb(House),Pixel(p,100+221,50+31),"Gludio house button drawn");
        t.Equal(Argb(Base),Pixel(p,100+221,50+61),"other town (Giran) not drawn");
        t.Equal(Argb(ListBtn),Pixel(p,100+3,50+177),"List_Button drawn");
        // Thumb: track y 122+10, travel 20−5=15; offset 0 → top.
        var thumb=Piece(p,"Move_Button");
        t.Equal(new Rectangle(100+185,50+132,11,5),p.At(thumb),"thumb at ScrollStart");
        Click(p,p.At(Piece(p,"Down_Button")),cmd);
        t.Check(p.ScrollOffset==1&&p.At(thumb).Y==50+132+15/2,"Down button scrolls one row and moves the thumb");
        p.Mouse(M(UiMouseKind.Wheel,100+50,50+130,MouseButtons.None,120),cmd);
        t.Equal(0,p.ScrollOffset,"wheel up scrolls back");
        p.Mouse(M(UiMouseKind.Wheel,100+50,50+130,MouseButtons.None,-120),cmd);p.Mouse(M(UiMouseKind.Wheel,100+50,50+130,MouseButtons.None,-120),cmd);
        t.Check(p.ScrollOffset==2&&p.At(thumb).Y==50+132+15,"wheel down to the end, thumb at the bottom");
        Click(p,p.At(Piece(p,"Up_Button")),cmd);
        t.Equal(1,p.ScrollOffset,"Up button");
        // Agithouse3 (house 4) lies under the list background: hidden and not clickable while the list is shown.
        t.Equal(Argb(Back),Pixel(p,100+201,50+161),"list background drawn over the town map");
        Click(p,100+201,50+161,cmd);
        t.Check(p.SelectedHouse==null,"house under the list background does not take the click");
        // Row 1 at offset 1 = third house.
        var row1=p.Pieces.Single(x=>x.Kind==AgitAuctionPieceKind.Row&&x.Row==1);
        Click(p,p.At(row1),cmd);
        t.Check(p.SelectedHouse==(8u<<16|3)&&p.Unhandled.Contains("select"),"row click selects that house (select packet not wired, logged)");
        t.Equal(Argb(HousePush),Pixel(p,100+261,50+31),"selected house drawn with PushPng");
        p.Mouse(M(UiMouseKind.Move,100+241,50+31,MouseButtons.None),cmd);
        t.Equal(Argb(HouseHi),Pixel(p,100+241,50+31),"hovered house uses HighlightPng");
        Click(p,100+241,50+31,cmd);
        t.Check(p.SelectedHouse==(8u<<16|2)&&p.ScrollOffset==1,"house button selects the listed house");
        var beforeRows=p.Pieces.Single(x=>x.Kind==AgitAuctionPieceKind.Row&&x.Row==2);
        p.ScrollBy(5);Click(p,p.At(beforeRows),cmd);
        t.Equal(8u<<16|4,p.SelectedHouse,"row under scroll offset 2");
        Click(p,p.At(Piece(p,"Name_Button")),cmd);
        t.Check(p.Unhandled.Contains("Name_AuctionList")&&p.Visible,"sort header not wired (logged)");
        Click(p,p.At(Piece(p,"List_Button")),cmd);
        t.Check(!p.ListVisible&&!p.Unhandled.Contains("List_AgitWin"),"List_Button hides the list");
        t.Equal(Argb(HousePush),Pixel(p,100+201,50+161),"map visible without the list: selected house 4 pushed");
        t.Equal(Argb(Base),Pixel(p,100+100,50+165),"list background hidden");
        Click(p,100+201,50+161,cmd);
        t.Check(p.SelectedHouse==(8u<<16|4)&&p.Visible,"house reachable while the list is hidden");
        var row0=p.Pieces.Single(x=>x.Kind==AgitAuctionPieceKind.Row&&x.Row==0);
        t.Check(p.ButtonAt(new Point(p.At(row0).X+1,p.At(row0).Y+1))==null,"hidden rows take no clicks");
        Click(p,p.At(Piece(p,"List_Button")),cmd);
        t.Check(p.ListVisible,"List_Button shows the list again");
        Click(p,p.At(Piece(p,"List_Button")),cmd);
        src.Open(Gludio());p.Update(World);
        t.Check(p.ListVisible&&p.SelectedHouse==null&&p.ScrollOffset==0,"a new S156 shows the list again");
        t.Check(p.IsDragHandle(new Point(100+50,50+5))&&!p.IsDragHandle(new Point(100+281,50+5))&&!p.IsDragHandle(new Point(100+50,50+30)),"drag strip = Moveable, buttons excluded");
        p.MoveTo(new Point(10,10));
        t.Equal(new Rectangle(10,10,300,200),p.Bounds,"moved");
        p.Mouse(M(UiMouseKind.Down,10+281,10+5),cmd);p.Mouse(M(UiMouseKind.Move,10+100,10+100),cmd);p.Mouse(M(UiMouseKind.Up,10+100,10+100),cmd);
        t.Check(p.Visible,"release outside the button does not fire");
        Click(p,10+281,10+5,cmd);
        t.Check(!p.Visible&&cmd.Calls.Contains("game-focus"),"Exit closes and returns game focus");
        t.Check(!p.Mouse(M(UiMouseKind.Down,50,50),cmd),"closed window ignores mouse");
        IUiClosablePanel closable=p;p.Open();closable.Close();
        t.Check(!p.Visible,"IUiClosablePanel.Close");
        var resolved=Loaded(()=>src.Current);resolved.TextResolver=(k,v)=>k=="String"&&v=="4391;1"?"清單":null;
        resolved.Update(World);
        t.Check(resolved.Visible&&resolved.TextResolver("String","4391;1")=="清單","TextResolver hook for String labels");
    }

    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Entry(uint id,string name,ushort area,byte month,byte day,uint price)=>[..Le32(id),..LoginPackets.TextEncoding.GetBytes(name),0,..Le16(area),month,day,..Le32(price)];
    static byte[] HouseList(uint board,params byte[][] entries)=>LoginPackets.Pad([AgitPackets.HouseListOpcode,..Le32(board),..Le16((ushort)entries.Length),..entries.SelectMany(e=>e)]);

    static void Observer(WorldModuleTestContext t){
        var reg=WorldExtensionRegistry.Default;
        t.Check(reg.Errors.Count==0,"default registry has no errors",reg.Errors);
        t.Check(reg.Entries.Any(e=>e.Kind=="observe"&&e.Opcode==156&&e.Method=="WorldSession.ObserveAgitAuctionWindow"),"S156 observer registered");
        t.Check(reg.Entries.Any(e=>e.Kind=="receive"&&e.Opcode==156&&e.Method=="WorldSession.ReceiveAgitHouseList"),"S156 still consumed by Agit.cs");
        AgitAuctionWindow.Clear();
        var s=new WorldSession();
        long before=AgitAuctionWindow.Request(new(0,[])).Revision;AgitAuctionWindow.Clear();
        s.Receive(HouseList(0x55,Entry(8u<<16|1,"甲屋",78,12,25,2_000_000),Entry(8u<<16|2,"乙屋",120,1,3,0xFFFF_FFFF)),"");
        var req=AgitAuctionWindow.Current;
        t.Check(req!=null&&req.Revision>before&&req.List.BoardId==0x55&&req.List.Houses.Count==2&&req.List.Houses[1].Price==-1,"S156 posts an open request");
        t.Check(s.AgitHouseList?.Houses.Count==2,"Agit.cs data unchanged");
        var snap=WorldModuleTestContext.SnapshotJson(s);
        t.Check(!snap.GetProperty("unhandled").TryGetProperty("0x9C",out _),"S156 not counted unhandled");
        var w=snap.GetProperty("extensions").GetProperty("agitAuctionWindow");
        t.Check(w.GetProperty("revision").GetInt64()==req!.Revision&&w.GetProperty("houses").GetInt32()==2,"snapshot");
        var truncList=LoginPackets.Pad([AgitPackets.HouseListOpcode,..Le32(1),..Le16(2),..Entry(1,"x",1,1,1,1)]);
        t.Throws<ProtocolException>(()=>s.Receive(truncList,""),"malformed S156 still rejected by Agit.cs");
        t.Check(AgitAuctionWindow.Current==req,"malformed S156 posts nothing");
        var p=new AgitAuctionPanel(null);p.Load(Layout(),id=>Surfs.GetValueOrDefault(id),id=>Pngs.GetValueOrDefault(id));p.Update(World);
        t.Check(p.Visible&&p.Shown==req,"default source reads AgitAuctionWindow.Current");
        s.Reset();
        t.Check(AgitAuctionWindow.Current==null,"WorldSession.Reset clears the request");
        p.Update(World);
        t.Check(!p.Visible,"panel closes after reset");
        t.Equal(JsonValueKind.Null,WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("agitAuctionWindow").ValueKind,"snapshot null after reset");
    }

    static void Registration(WorldModuleTestContext t){
        var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==AgitAuctionPanel.PanelId);
        t.Check(reg!=null&&reg.Layout==AgitAuctionPanel.HostLayout,"[UiPanel] discovered with host layout uistatus86");
        t.Check(reg?.Create(new UiRoot()) is AgitAuctionPanel {Layer:UiLayer.Window} and IUiClosablePanel and IUiMovablePanel,"window, closable, movable");
        t.Check(UiPanelRegistry.Errors.Count==0,"no discovery errors",UiPanelRegistry.Errors);
    }
}
