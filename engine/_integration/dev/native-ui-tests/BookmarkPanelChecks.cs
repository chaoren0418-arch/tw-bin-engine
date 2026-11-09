using P06;
using P06.NativeUi;
using System.Drawing.Imaging;

// ui-bookmark (Issue #472). Offline only: synthetic Tile/Sprite archives under scratch/issue-472/synthetic, synthetic
// bookmark snapshots, no real client, no network. The XML below keeps BookmarkUI.xml's structure and attribute values
// (combo list shortened from 67 to 3 items, tooltips dropped); surf sizes copy the real images' width/height only (no pixels).
partial class Checks {
    const string BookmarkLayoutXml="""
<?xml version="1.0"?>
<BookmarkWindowUI Toggle="true">
	<Window Name = "Bookmark_Window" X = "0" Y = "0" FrameSurf = "4313;1" Moveable = "20" KeyboardEvent = "BookmarkKeyBoard" MouseEvent = "Dummy">
	  <Image Name ="Base_Image" Surf="4312;0" X ="6" Y = "9"/>
		<Button Name = "Exit_Button" X = "249" Y = "15" BaseSurf = "3667" PushSurf = "3668" DisableSurf ="3480" MouseEvent = "CloseWindow"/>
		<Image Name ="QuickMoveListTitle_Image" Surf="4314;1" X ="24" Y = "46"/>
		<Image Name ="MemoryZoneListTitle_Image" Surf="4315;1" X ="24" Y = "145"/>
		<ComboBox Name = "ZoneFilter_Combobox" X = "129" Y = "141" Width = "105" Height = "134" EnableEvent = "ZoneFilterComboBox">
	    <Label Name = "Text_Label" String = "878" X="20" Y = "0" Width = "50" Height = "20" HAlign="center"/>
	    <Button Name = "Expand_Button" X = "0" Y = "0" BaseSurf = "4317" HighlightSurf = "4318" MouseEvent = "ZoneFilterComboExpandButton"/>
	    <UpDownList Name = "Expand_List" Y = "19" PageSize = "7" Width = "55" Height = "114" Vertical = "1">
				<Image Name = "Background_Image" X = "-2" Surf = "4319"/>
				<CheckButton Name = "Item0_Button" Caption = "ALL" PushSurf = "4320;4" Height = "19" MouseEvent = "ZoneFilterComboListItem"/>
				<CheckButton Name = "Item1_Button" Caption = "temp1" PushSurf = "4320;4" Height = "19" MouseEvent = "ZoneFilterComboListItem"/>
				<CheckButton Name = "Item2_Button" Caption = "temp2" PushSurf = "4320;4" Height = "19" MouseEvent = "ZoneFilterComboListItem"/>
			</UpDownList>
			<WheelControl Name="Expand_List_Wheel" X="0" Y="19" Width="100" Height="90">
        <ScrollBar Name = "Down_Scrollbar" X = "87" Y = "0" MaxValue = "2" ChangeValue = "1" ScrollStart = "16" ScrollHeight = "84" Vertical = "1" Activate="0">
          <Image Name = "Background_Image" Surf = "4352"/>
          <Button Name = "Up_Button" X = "3" Y ="3" BaseSurf = "4346" PushSurf = "4347" MouseEvent = "Dummy"/>
          <Button Name = "Down_Button" X = "3" Y = "100" BaseSurf = "4348" PushSurf = "4349" MouseEvent = "Dummy"/>
          <Button Name = "Move_Button" X = "3" Y = "16" BaseSurf = "4350;1" PushSurf = "4351;1" MouseEvent = "Dummy"/>
        </ScrollBar>
      </WheelControl>
		</ComboBox>
		<MovableList Name="QuickMove_List" X = "23" Y ="62" Width="120" Height="75" Vertical = "1" MouseEvent="QuickMoveList">
				<CheckButton Name = "Item0_Button" String = "0" PushSurf = "4345;4" Activate = "1" Height = "15" MouseEvent = "QuickList"/>
  			<CheckButton Name = "Item1_Button" String = "1" PushSurf = "4345;4" Activate = "1" Height = "15" MouseEvent = "QuickList"/>
  			<CheckButton Name = "Item2_Button" String = "2" PushSurf = "4345;4" Activate = "1" Height = "15" MouseEvent = "QuickList"/>
  			<CheckButton Name = "Item3_Button" String = "3" PushSurf = "4345;4" Activate = "1" Height = "15" MouseEvent = "QuickList"/>
  			<CheckButton Name = "Item4_Button" String = "4" PushSurf = "4345;4" Activate = "1" Height = "15" MouseEvent = "QuickList"/>
		</MovableList>
		<Button Name="Sorting_Button" BaseSurf="5131" PushSurf="5133;1" HighlightSurf="5132" X="182" Y="42" MouseEvent="Sorting"/>
		<Button Name="RandomTeleport_Button" BaseSurf="5128" PushSurf="5130;1" HighlightSurf="5129" X="129" Y="42" MouseEvent="RandomTeleport"/>
		<MovableList Name = "MemoryPlace_List" X = "22" Y = "162" Width = "200" Height = "204" Vertical = "1" KeyboardEvent = "MemoryPlaceKeyBoard">
ROWS
		</MovableList>
		<List Name = "CloningMemoryPlace_List" X = "22" Y = "162" Width = "200" Height = "204" Vertical = "1" Activate="0">
			<CheckButton Name = "Item0_Button" Caption = "temp0" PushSurf = "4336;4" Activate = "0" Height = "17" MouseEvent = "CloningMemoryPlaceListMouse"/>
		</List>
		<WheelControl Name="MemoryPlaceList_WheelArea" X="23" Y="163" Width="200" Height="205">
      <ScrollBar Name = "MemoryPlaceList_Scrollbar" X = "218" Y = "-5" MaxValue = "9" ChangeValue = "1" ScrollStart = "16" ScrollHeight = "182" Vertical = "1" MouseEvent="Dummy">
        <Image Name = "Background_Image" Surf = "4316;1" X ="0" Y="14"/>
        <Button Name = "Up_Button" X = "0" Y ="0" BaseSurf = "4178;1" PushSurf = "4179;1" MouseEvent = "Dummy"/>
        <Button Name = "Down_Button" X = "0" Y = "196" BaseSurf = "4180;1" PushSurf = "4181;1" MouseEvent = "Dummy"/>
        <Button Name = "Move_Button" X = "0" Y = "16" BaseSurf = "4182;1" PushSurf = "4183;1" MouseEvent = "Dummy"/>
      </ScrollBar>
    </WheelControl>
		<Control Name="ColorPalette" X = "19" Y="368">
PALETTE
  		<Image Name="ColorPaletteImage" Surf="4321" X="0" Y="0"/>
		</Control>
		<Button Name="Confirm_Button" BaseSurf="4337" PushSurf="4339;1" HighlightSurf="4338" DisableSurf="4340" X="67" Y="395" MouseEvent="Confirm"/>
		<Button Name="Cancel_Button" BaseSurf="4341" PushSurf="4343;1" HighlightSurf="4342" DisableSurf="4344" X="134" Y="395" MouseEvent="Cancel"/>
		<Label Name="BookmarkCount_Label" X="200" Y="397" Width="20" Height="15" Caption="30/60"/>
	</Window>
</BookmarkWindowUI>
""";
    static string BookmarkXml(string? replace=null,string? with=null){
        string rows=string.Concat(Enumerable.Range(0,12).Select(i=>$"\t\t\t<CheckButton Name = \"Item{i}_Button\" Caption = \"temp{i}\" PushSurf = \"4336;4\" Activate = \"0\" Height = \"17\" MouseEvent = \"MemoryPlaceListMouse\"/>\n"));
        string palette=string.Concat(Enumerable.Range(0,12).Select(i=>$"\t\t<Button Name=\"ColorPalette{i}_Button\" BaseSurf=\"{4322+i}\" PushSurf=\"4335;1\" X=\"{4+16*i}\" Y=\"3\" MouseEvent=\"ColorPalette\"/>\n"))
            +"\t\t<Button Name=\"ColorPalette12_Button\" BaseSurf=\"4334\" PushSurf=\"4334;1\" X=\"196\" Y=\"3\"/>\n";
        string xml=BookmarkLayoutXml.Replace("ROWS",rows).Replace("PALETTE",palette);
        return replace==null?xml:xml.Replace(replace,with);
    }
    /// <summary>Real width/height of every surf the window uses (rendered locally with ui_img_render.py, #472 report).</summary>
    static readonly Dictionary<int,(int W,int H)> BookmarkSurfSizes=new(){
        [4313]=(263,420),[4312]=(251,407),[3667]=(8,8),[3668]=(8,8),[4314]=(79,12),[4315]=(79,12),[4317]=(106,18),[4318]=(106,18),
        [4319]=(106,115),[4320]=(88,20),[4345]=(209,15),[5131]=(52,17),[5132]=(52,17),[5133]=(53,18),[5128]=(52,17),[5129]=(52,17),[5130]=(53,18),
        [4336]=(209,17),[4316]=(12,182),[4178]=(12,14),[4179]=(12,14),[4180]=(12,14),[4181]=(12,14),[4182]=(11,16),[4183]=(11,16),
        [4321]=(215,21),[4335]=(17,17),[4337]=(53,17),[4338]=(53,17),[4339]=(54,18),[4340]=(53,17),[4341]=(53,17),[4342]=(53,17),[4343]=(54,18),[4344]=(53,17),
        [4322]=(15,15),[4323]=(15,15),[4324]=(15,15),[4325]=(15,15),[4326]=(15,15),[4327]=(15,15),[4328]=(15,15),[4329]=(15,15),[4330]=(15,15),
        [4331]=(15,15),[4332]=(15,15),[4333]=(15,15),[4334]=(15,15),
    };
    /// <summary>Distinct opaque colour per surf (RGB555) so painting can be checked by pixel.</summary>
    static ushort BookmarkColor(int id)=>id switch{4313=>0x0010,4312=>0x0200,4336=>0x7C00,4345=>0x03E0,4320=>0x7FE0,4321=>0x4210,4335=>0x7C1F,_=>(ushort)(0x0400|(id&0x3FF))};
    static Color BookmarkArgb(int id)=>Color.FromArgb(unchecked((int)UiImage.ToArgb(BookmarkColor(id))));

    sealed class FakeBookmarkHost:IBookmarkUiHost {
        public BookmarkUiState State{get;set;}=BookmarkUiState.Empty;
        public List<uint> Deleted{get;}=[];
        public void Delete(BookmarkUiState shown,uint id)=>Deleted.Add(id);
    }
    static BookmarkUiState FakeBookmarks(int count,bool canEdit=true,Func<int,ushort>? map=null,Func<int,bool>? pending=null){
        var items=Enumerable.Range(0,count).Select(i=>new Bookmark((uint)(100+i),$"點{(char)('Z'-i%26)}{i:00}",map?.Invoke(i)??(ushort)(i%3==0?4:0),(ushort)(32000+i),(ushort)(32700+i),pending?.Invoke(i)??false)).ToArray();
        return BookmarkUiState.From(new BookmarkView(1,1,7,99,canEdit,50,"",items));
    }

    [CheckGroup("native-ui-bookmark",Order=1),CheckGroup("map-continuity",Order=61),CheckGroup(CheckGroups.Default,Order=127)]
    static void NativeUiBookmarkChecks(string repo){
        Check(UiPanelRegistry.Panels.Count(r=>r.Id==BookmarkWindowPanel.PanelId&&r.Layout==BookmarkWindowPanel.LayoutName)==1&&UiPanelRegistry.Errors.Count==0,"ui-bookmark registered once (BookmarkUI.xml, Bookmark_Window)");

        // --- layout read straight from the XML (UI§6 table).
        var xml=UiLayout.Parse(BookmarkXml(),"BookmarkUI.xml");
        var l=BookmarkWindowLayout.Read(xml);
        Check(l.Origin==Point.Empty&&l.Moveable==20&&l.Frame==4313&&l.BaseAt==new Point(6,9)&&l.Base==4312,"window: frame 4313, base 4312 at (6,9), Moveable 20 (UI§6)");
        Check(l.Exit.At==new Point(249,15)&&l.Exit.Base==3667&&l.Exit.Push==3668,"exit button (249,15) 3667/3668");
        Check(l.Quick==new Rectangle(23,62,120,75)&&l.QuickRows==5&&l.QuickRowHeight==15,"quick-move list (23,62) 120×75, 5 rows × 15");
        Check(l.Memory==new Rectangle(22,162,200,204)&&l.MemoryRows==12&&l.MemoryRowHeight==17&&l.MemoryRowPush==4336,"memory list (22,162) 200×204, 12 rows × 17, selection 4336");
        Check(l.Wheel==new Rectangle(23,163,200,205)&&l.Scroll==new Point(218,-5)&&l.ScrollStart==16&&l.ScrollHeight==182,"scroll bar at wheel (23,163) + (218,-5), track 16..198");
        Check(l.Combo==new Rectangle(129,141,105,134)&&l.ComboRowHeight==19&&l.ComboRows==6&&l.ComboPageSize==7,"zone combo (129,141) 105×134; list rows 19 px, 6 fit the 114 px box (PageSize 7 kept)");
        Check(l.Random.At==new Point(129,42)&&l.Sorting.At==new Point(182,42)&&l.Random.Highlight==5129&&l.Sorting.Push==5133,"random teleport (129,42), sorting (182,42)");
        Check(l.Palette==new Point(19,368)&&l.Colors.Count==13&&l.Colors.Select((c,i)=>c.At.X==4+16*i&&c.At.Y==3).All(x=>x)&&l.Colors[0].Base==4322&&l.Colors[12].Base==4334,"palette (19,368): 13 swatches, pitch 16");
        Check(l.Confirm.At==new Point(67,395)&&l.Cancel.At==new Point(134,395)&&l.Count==new Rectangle(200,397,20,15),"confirm (67,395), cancel (134,395), count label (200,397)");
        bool Throws(string what,string with){try{BookmarkWindowLayout.Read(UiLayout.Parse(BookmarkXml(what,with),"BookmarkUI.xml"));return false;}catch(UiLayoutException){return true;}}
        Check(Throws("Name=\"Confirm_Button\"","Name=\"Okay\""),"missing Confirm_Button rejected (no invented layout)");
        Check(Throws("Name=\"ColorPalette12_Button\"","Name=\"Extra\""),"palette without 13 swatches rejected");
        Check(Throws("Caption = \"temp3\" PushSurf = \"4336;4\" Activate = \"0\" Height = \"17\"","Caption = \"temp3\" PushSurf = \"4336;4\" Activate = \"0\" Height = \"18\""),"uneven memory rows rejected");
        Check(Throws("BookmarkWindowUI","OtherUI"),"wrong root element rejected");
        Check(Throws("Name = \"Bookmark_Window\"","Name = \"Other_Window\""),"wrong window name rejected");

        // --- synthetic archives → UiResources → SurfCatalog (same path as the product).
        string dir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-472","synthetic");
        if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        string surfList="<?xml version=\"1.0\"?><SurfList>"+string.Concat(BookmarkSurfSizes.Keys.Select(id=>$"<surf id=\"{id}\" file=\"{id}\"/>"))+"</SurfList>";
        var tilePin=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)),("BookmarkUI.xml",Xml(BookmarkXml())));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (id,(w,h)) in BookmarkSurfSizes){string name=id+".img";items[1+SurfCatalog.NameHash(name)].Add((name,Img(w,h,0,0,BookmarkColor(id))));}
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var spritePins=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        using var res=new UiResources(dir,tilePin,spritePins);
        var ui=UiRoot.Build(res,UiPanelRegistry.Panels.Where(r=>r.Id==BookmarkWindowPanel.PanelId).ToArray());
        var p=(BookmarkWindowPanel)ui.Find(BookmarkWindowPanel.PanelId)!;
        var host=new FakeBookmarkHost();p.Attach(host);
        var world=new UiStaticGameState(true);var cmds=new UiNullCommands();
        Check(p.Layer==UiLayer.Window&&p.Bounds==new Rectangle(0,0,263,420)&&!p.Visible,"window layer, 263×420 from frame 4313, starts closed (Toggle window)");
        p.Show();ui.Update(world);
        Check(p.Visible&&p.Rows.Count==0&&p.Data.CountText=="","no snapshot → empty list, blank count (no invented values)");

        // --- list, scrolling, selection.
        host.State=FakeBookmarks(20);ui.Update(world);
        Check(p.Rows.Count==20&&p.MaxTop==8&&p.Top==0&&p.Data.CountText=="20/50","20 bookmarks: 12 visible, MaxTop 8, count 20/50");
        Check(p.Rows.Select(r=>r.Id).SequenceEqual(Enumerable.Range(100,20).Select(i=>(uint)i)),"default order = server order");
        Point Mid(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);
        void Click(Point at,MouseButtons b=MouseButtons.Left){p.Mouse(new(UiMouseKind.Down,at,b,1,0,Keys.None),cmds);p.Mouse(new(UiMouseKind.Up,at,b,1,0,Keys.None),cmds);}
        void Wheel(Point at,int delta)=>p.Mouse(new(UiMouseKind.Wheel,at,MouseButtons.None,0,delta,Keys.None),cmds);
        Check(p.UpRect==new Rectangle(241,158,12,14)&&p.DownRect==new Rectangle(241,354,12,14)&&p.TrackRect==new Rectangle(241,174,11,182),"scroll buttons/track at absolute XML points, sized by their surfs");
        Click(Mid(p.DownRect));Check(p.Top==1,"down button scrolls one row");
        Click(Mid(p.UpRect));Click(Mid(p.UpRect));Check(p.Top==0,"up button scrolls back and clamps at 0");
        Wheel(Mid(p.MemoryRowRect(3)),-240);Check(p.Top==2,"wheel down 2 notches over the list → Top 2");
        Wheel(Mid(p.MemoryRowRect(3)),-1200);Check(p.Top==p.MaxTop,"wheel clamps at MaxTop");
        Wheel(new(60,100),240);Check(p.Top==p.MaxTop,"wheel outside the list area does not scroll");
        var t=p.TrackRect;
        p.Mouse(new(UiMouseKind.Down,new(t.X+5,t.Y),MouseButtons.Left,1,0,Keys.None),cmds);
        Check(p.Top==0,"press at the track top → Top 0");
        p.Mouse(new(UiMouseKind.Move,new(t.X+5,t.Bottom-1),MouseButtons.Left,0,0,Keys.None),cmds);
        Check(p.Top==p.MaxTop,"dragging the thumb to the bottom → MaxTop");
        p.Mouse(new(UiMouseKind.Up,new(t.X+5,t.Bottom-1),MouseButtons.Left,1,0,Keys.None),cmds);
        Check(p.ThumbY==16+182-16,"thumb drawn at the bottom of the track");
        p.ScrollTo(2);Click(Mid(p.MemoryRowRect(3)));
        Check(p.SelectedId==105&&p.Selected?.Name==p.Rows[5].Name,"click on list row 3 at Top 2 selects bookmark #5");
        Click(Mid(p.MemoryRowRect(0)),MouseButtons.Right);Check(p.SelectedId==105,"right button does not select");
        p.ScrollTo(8);Check(p.SelectedId==105,"selection survives scrolling");

        // --- painting: frame, base, selection only on the selected row; quick list stays empty.
        using(var canvas=new UiCanvas()){
            p.ScrollTo(2);ui.Compose(canvas,null);
            int Px(int x,int y)=>canvas.Bitmap.GetPixel(x,y).ToArgb();
            Check(Px(2,200)==BookmarkArgb(4313).ToArgb()&&Px(10,12)==BookmarkArgb(4312).ToArgb(),"frame 4313 at the window origin, base 4312 at (6,9) on top");
            var sel=p.MemoryRowRect(3);
            Check(Px(sel.X+150,sel.Y+1)==BookmarkArgb(4336).ToArgb()&&Px(p.MemoryRowRect(4).X+150,p.MemoryRowRect(4).Y+1)!=BookmarkArgb(4336).ToArgb(),"selection 4336 drawn under the selected row only");
            Check(Enumerable.Range(0,5).All(i=>Px(p.QuickRowRect(i).X+100,p.QuickRowRect(i).Y+7)!=BookmarkArgb(4345).ToArgb()),"quick-move list: content unverified → nothing drawn");
            Check(Px(19+4+7,368+3+7)==BookmarkArgb(4322).ToArgb()&&Px(19+1,368+1)==BookmarkArgb(4321).ToArgb(),"palette strip 4321 under the swatches (swatch 4322 visible)");
            Check(Px(300,300)==Color.Black.ToArgb(),"nothing outside the window");
        }

        // --- Delete key → host (T124 path); pending / not editable / no selection → not handled.
        Check(p.Key(new(Keys.Delete,true,false),cmds)&&host.Deleted.SequenceEqual(new uint[]{105}),"Delete on the selected row → host.Delete(#105)");
        Check(!p.Key(new(Keys.Delete,true,true),cmds)&&!p.Key(new(Keys.Delete,false,false),cmds)&&!p.Key(new(Keys.A,true,false),cmds),"auto-repeat, key-up and other keys are not handled");
        host.State=FakeBookmarks(20,pending:i=>i==5);ui.Update(world);host.Deleted.Clear();
        Check(!p.Key(new(Keys.Delete,true,false),cmds)&&host.Deleted.Count==0,"pending delete row cannot be deleted again");
        host.State=FakeBookmarks(20,canEdit:false);ui.Update(world);
        Check(!p.Key(new(Keys.Delete,true,false),cmds)&&host.Deleted.Count==0,"CanEdit false (walking, busy, dead…) → no delete");
        host.State=FakeBookmarks(4);ui.Update(world);
        Check(p.SelectedId==null&&p.Top==0&&p.MaxTop==0,"list shrinks: selection of a vanished row dropped, Top clamped");
        Check(!p.Key(new(Keys.Delete,true,false),cmds),"no selection → Delete goes to the game");

        // --- sort and zone filter (local view only).
        host.State=FakeBookmarks(20);ui.Update(world);
        Click(Mid(p.SortRect));
        Check(p.SortByName&&p.Rows.Select(r=>r.Name).SequenceEqual(p.Rows.Select(r=>r.Name).Order(StringComparer.Ordinal)),"sorting button → name order");
        Click(Mid(p.SortRect));Check(!p.SortByName&&p.Rows[0].Id==100,"sorting again → server order");
        Check(p.FilterOptions.SequenceEqual(new ushort?[]{null,0,4}),"filter options: ALL + distinct map ids");
        Click(Mid(p.ComboExpandRect));Check(p.ComboOpen,"expand button opens the zone list");
        Click(Mid(p.ComboRowRect(2)));
        Check(!p.ComboOpen&&p.Filter==4&&p.Rows.Count==7&&p.Rows.All(r=>r.MapId==4),"choosing map 4 filters the list (7 rows)");
        Click(Mid(p.ComboExpandRect));Click(Mid(p.MemoryRowRect(0)));
        Check(!p.ComboOpen&&p.SelectedId==null,"press outside the open list only closes it (no row selected)");
        Click(Mid(p.ComboExpandRect));Click(Mid(p.ComboRowRect(0)));Check(p.Filter==null&&p.Rows.Count==20,"ALL restores the full list");

        // --- buttons: random teleport is inert; palette is a local choice; confirm/cancel/exit close.
        Click(Mid(p.RandomRect));Check(p.Visible&&host.Deleted.Count==0&&cmds.Calls.Count==0,"random teleport: effect unverified → no action");
        Click(Mid(p.ColorRect(5)));Check(p.SelectedColor==5,"palette swatch 5 selected (local only, no packet)");
        Click(Mid(p.CancelRect));Check(!p.Visible&&p.SelectedColor==null,"cancel closes and drops the palette choice");
        p.Show();Click(Mid(p.ConfirmRect));Check(!p.Visible,"confirm closes");
        p.Show();Click(Mid(p.ExitRect));Check(!p.Visible,"exit button closes");
        p.Show();p.Mouse(new(UiMouseKind.Down,Mid(p.ExitRect),MouseButtons.Left,1,0,Keys.None),cmds);p.Mouse(new(UiMouseKind.Up,Mid(p.SortRect),MouseButtons.Left,1,0,Keys.None),cmds);
        Check(p.Visible&&!p.SortByName,"press on one button, release on another → nothing");

        // --- drag through UiInput: only the 20-px top strip, clamped inside 800×600.
        {
            var input=new UiInput(ui,cmds);ui.Update(world);
            Check(p.IsDragHandle(new(100,10))&&!p.IsDragHandle(new(100,25))&&!p.IsDragHandle(Mid(p.ExitRect)),"drag handle = top 20 px, not the exit button");
            void Drag(Point from,Point to){input.Mouse(UiMouseKind.Down,from,MouseButtons.Left,1);input.Mouse(UiMouseKind.Move,to);input.Mouse(UiMouseKind.Up,to,MouseButtons.Left,1);}
            Drag(new(100,10),new(300,110));Check(p.Bounds==new Rectangle(200,100,263,420),"drag moves the window by the mouse delta");
            Drag(new(300,110),new(2000,2000));Check(p.Bounds.Location==new Point(800-263,600-420),"drag past the bottom-right → clamped inside the canvas");
            Drag(new(600,190),new(-500,-500));Check(p.Bounds.Location==Point.Empty,"drag past the top-left → clamped at (0,0)");
            Drag(new(100,100),new(400,400));Check(p.Bounds.Location==Point.Empty,"press below the strip does not drag");
            Check(input.Mouse(UiMouseKind.Down,new(100,100),MouseButtons.Left,1).Route==UiRoute.Panel,"click inside the window never reaches the world");
            input.Mouse(UiMouseKind.Up,new(100,100),MouseButtons.Left,1);
        }

        // --- leaving the world hides the window and clears the list; missing frame disables the window.
        ui.Update(new UiStaticGameState(false));Check(!p.Visible&&p.Rows.Count==0,"not in world → hidden, empty");
        var bare=new BookmarkWindowPanel();bare.Load(xml,id=>id==4313?null:res.Surfs.Get(id));bare.Show();
        Check(!bare.Visible&&bare.Bounds.IsEmpty,"frame surf missing → window disabled (no invented size)");

        NativeUiBookmarkRealClient(repo);
    }

    /// <summary>Optional (TW_CLIENT_ROOT): real Tile/Sprite; PNG and coordinate table only under scratch/issue-472/real.</summary>
    static void NativeUiBookmarkRealClient(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui bookmark real client render (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-472","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        var ui=UiRoot.Build(res,UiPanelRegistry.Panels.Where(r=>r.Id==BookmarkWindowPanel.PanelId).ToArray());
        var p=(BookmarkWindowPanel)ui.Find(BookmarkWindowPanel.PanelId)!;
        var host=new FakeBookmarkHost{State=FakeBookmarks(16)};p.Attach(host);p.Show();
        var world=new UiStaticGameState(true);ui.Update(world);
        p.MoveTo(new(268,40));p.ScrollTo(1);
        var cmds=new UiNullCommands();
        void Click(Rectangle r){var m=new Point(r.X+r.Width/2,r.Y+r.Height/2);p.Mouse(new(UiMouseKind.Down,m,MouseButtons.Left,1,0,Keys.None),cmds);p.Mouse(new(UiMouseKind.Up,m,MouseButtons.Left,1,0,Keys.None),cmds);}
        Click(p.MemoryRowRect(2));Click(p.ColorRect(3));
        using var canvas=new UiCanvas();
        ui.Compose(canvas,null);canvas.Bitmap.Save(Path.Combine(outDir,"bookmark-window.png"),ImageFormat.Png);
        Click(p.ComboExpandRect);ui.Compose(canvas,null);canvas.Bitmap.Save(Path.Combine(outDir,"bookmark-window-combo.png"),ImageFormat.Png);
        File.WriteAllText(Path.Combine(outDir,"bookmark-coordinates.json"),System.Text.Json.JsonSerializer.Serialize(new{
            client,bounds=p.Bounds.ToString(),exit=p.ExitRect.ToString(),sort=p.SortRect.ToString(),random=p.RandomRect.ToString(),
            confirm=p.ConfirmRect.ToString(),cancel=p.CancelRect.ToString(),count=p.CountRect.ToString(),
            quickRows=Enumerable.Range(0,5).Select(i=>p.QuickRowRect(i).ToString()),memoryRows=Enumerable.Range(0,12).Select(i=>p.MemoryRowRect(i).ToString()),
            up=p.UpRect.ToString(),down=p.DownRect.ToString(),track=p.TrackRect.ToString(),combo=p.ComboExpandRect.ToString(),comboRows=Enumerable.Range(0,6).Select(i=>p.ComboRowRect(i).ToString()),
            colors=Enumerable.Range(0,13).Select(i=>p.ColorRect(i).ToString())},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Check(File.Exists(Path.Combine(outDir,"bookmark-window.png")),"real client: bookmark window rendered to scratch/issue-472/real");
    }
}
