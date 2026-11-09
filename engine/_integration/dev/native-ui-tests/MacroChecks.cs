using P06;
using P06.NativeUi;
using System.Drawing.Imaging;

// ui-macro-bar (Issue #466). Offline only: synthetic Tile/Sprite archives under scratch/issue-466/, synthetic items and spells,
// no real client, no network. Coordinates asserted here are the verified ones from docs/reference/ui-layout.md §2 and V5 (a).
partial class Checks {
    sealed class MacroTestState(bool inWorld,MacroBook book,MacroInventory? inventory,bool inputAllowed,Func<ushort,Bitmap?>? icons):IUiGameState,IUiMacroState {
        public bool InWorld=>inWorld;public string? CharacterName=>null;public int? Level=>null;
        public long? Hp=>null;public long? MaxHp=>null;public long? Mp=>null;public long? MaxMp=>null;public long Now=>0;
        public MacroBook Macros=>book;public MacroInventory? Inventory=>inventory;public bool InputAllowed=>inputAllowed;
        public Bitmap? ItemIcon(ushort id)=>icons?.Invoke(id);
    }

    // Synthetic RIGHT_WINDOW / EX_MACRO_WINDOW with the same element structure and attribute values as uistatus86-c.xml.
    const string MacroLayoutXml="""
<?xml version="1.0"?>
<MainStatusConfigulation>
  <RIGHT_WINDOW x = "643" y = "476">
    <MACRO_MAIN_WINDOW><MACRO_FRAME surfNumber = "3176" xRPos = "0" yRPos = "0"/></MACRO_MAIN_WINDOW>
    <BUTTON_2STAGE_LIST><IMAGE_BUTTON title = "Map" action = "13" surfNumber = "3202" xRPos = "2" yRPos = "91"/></BUTTON_2STAGE_LIST>
    <BUTTON_ONOFF_LIST><ONOFF_BUTTON title = "MACRO_LOCK" action = "8" surfNumber = "3191" xRPos = "-9" yRPos = "37"/></BUTTON_ONOFF_LIST>
    <BUTTON_TAB_LIST>
      <TAB_BUTTON title = "MACRO_SELECT" action = "10" surfNumber = "3193" xRPos = "15" yRPos = "5"/>
      <TAB_BUTTON title = "MACRO_SELECT" action = "11" surfNumber = "3196" xRPos = "37" yRPos = "5"/>
      <TAB_BUTTON title = "MACRO_SELECT" action = "12" surfNumber = "3199" xRPos = "60" yRPos = "5"/>
    </BUTTON_TAB_LIST>
    <ETC>
      <SELECT_MARK surfName = "SURF_RENEWAL_MAIN_MACRO_SELECT" surfNumber = "2331" xRPos = "12" yRPos = "19"/>
      <MACRO_ICON surfName = "" surfNumber = "" xRPos = "14" yRPos = "21"/>
    </ETC>
  </RIGHT_WINDOW>
  <EX_MACRO_WINDOW x = "485" y = "475">
    <MAIN_WINDOW><EX_MACRO_FRAME surfNumber = "3226" xRPos = "0" yRPos = "0"/></MAIN_WINDOW>
  </EX_MACRO_WINDOW>
</MainStatusConfigulation>
""";

    static byte[] MacroImg(int w,int h,ushort flags,ushort key,Func<int,int,ushort> pixel){
        var b=new byte[8+w*h*2];BitConverter.GetBytes((ushort)w).CopyTo(b,0);BitConverter.GetBytes((ushort)h).CopyTo(b,2);
        BitConverter.GetBytes(flags).CopyTo(b,4);BitConverter.GetBytes(key).CopyTo(b,6);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)BitConverter.GetBytes(pixel(x,y)).CopyTo(b,8+(y*w+x)*2);
        return b;
    }
    static UiResources MacroResources(string dir,IReadOnlyDictionary<int,byte[]> images,string layout){
        if(Directory.Exists(dir))foreach(var f in Directory.GetFiles(dir))File.Delete(f);
        string surfList="<?xml version=\"1.0\"?><SurfList>"+string.Concat(images.Keys.Select(id=>$"<surf id=\"{id}\" file=\"{id}\"/>"))+"</SurfList>";
        var tile=Archive(dir,"Tile",("SurfList.xml",Xml(surfList)),("uistatus86-c.xml",Xml(layout)));
        var items=Enumerable.Range(0,17).Select(_=>new List<(string,byte[])>()).ToArray();
        foreach(var (id,img) in images){string name=id+".img";items[1+SurfCatalog.NameHash(name)].Add((name,img));}
        for(int i=0;i<17;i++)items[i].Add(($"pad{i}.img",Img(1,1,0,0)));
        var sprites=Enumerable.Range(0,17).Select(i=>Archive(dir,SpriteName(i),items[i].ToArray())).ToArray();
        return new UiResources(dir,tile,sprites);
    }

    /// <summary>The real registry minus LEFT_WINDOW (#464): these synthetic uistatus86 layouts have no LEFT_WINDOW node and UiRoot.Build is all-or-nothing.</summary>
    static IReadOnlyList<UiPanelRegistration> MacroRegistry=>UiPanelRegistry.Panels.Where(p=>string.Equals(p.Layout,"uistatus86",StringComparison.OrdinalIgnoreCase)&&p.Id is not ("LEFT_WINDOW" or "CENTER_WINDOW" or MiniMapPanel.PanelId)).ToArray();   // synthetic layout has none of these windows (#464, #468, MINI_MAP_WINDOW #470); only uistatus86 panels — other layouts (uiextra86 #526, FriendUI #529, effectlist2 #473, …) are not in the synthetic Tile.pak

    static void NativeUiMacroChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-466","synthetic");Directory.CreateDirectory(root);
        // RGB555 colours (R high): every image distinct so pixels identify the drawn surf.
        const ushort Frame=0x0010,TabN=0x0200,TabS=0x4000,LockClosed=0x0210,LockOpen=0x4200,Mark=0x7FE0,Ex=0x4010,Key=0x324C;
        var full=new Dictionary<int,byte[]>{
            [3176]=MacroImg(157,124,0,0,(_,_)=>Frame),
            [2331]=MacroImg(36,36,1,Key,(x,y)=>x==0||y==0||x==35||y==35?Mark:Key),   // hollow mark, colour-keyed inside
            [3191]=MacroImg(12,30,0,0,(_,_)=>LockClosed),[3192]=MacroImg(12,30,0,0,(_,_)=>LockOpen),
            [3226]=MacroImg(158,125,0,0,(_,_)=>Ex),
        };
        foreach(int a in new[]{3193,3196,3199}){full[a]=MacroImg(20,12,0,0,(_,_)=>TabN);full[a+1]=MacroImg(20,12,0,0,(_,_)=>TabS);full[a+2]=MacroImg(20,12,0,0,(_,_)=>0x7C00);}
        static int Rgb(ushort v)=>Color.FromArgb(255,(v>>10&31)*255/31,(v>>5&31)*255/31,(v&31)*255/31).ToArgb();

        using var res=MacroResources(Path.Combine(root,"full"),full,MacroLayoutXml);
        // Uses the real registry: the #466 registrations must build from uistatus86.
        var ui=UiRoot.Build(res,MacroRegistry);
        var main=(MacroPanel)ui.Find(MacroPanel.WindowId)!;var ex=(ExMacroPanel)ui.Find(ExMacroPanel.WindowId)!;
        Check(main!=null&&ex!=null&&main.Layer==UiLayer.Bar&&ex.Layer==UiLayer.Bar,"registry builds RIGHT_WINDOW and EX_MACRO_WINDOW on the bar layer");

        // --- geometry (ui-layout.md §2 + V5 (a)).
        Check(main.FrameRect==new Rectangle(643,476,157,124),"frame 3176 at (643,476) 157×124");
        Check(main.LockRect==new Rectangle(634,513,12,30),"MACRO_LOCK at (643−9, 476+37) = (634,513)");
        Check(main.Bounds==new Rectangle(634,476,166,124),"bounds = frame ∪ lock button");
        Check(main.TabRects.SequenceEqual(new[]{new Rectangle(658,481,20,12),new Rectangle(680,481,20,12),new Rectangle(703,481,20,12)}),"3 tabs at x=15/37/60, y=5 relative");
        Check(main.IconOrigin==new Point(657,497)&&main.SelectOrigin==new Point(655,495),"MACRO_ICON (657,497), SELECT_MARK (655,495)");
        var icons=new[]{new Point(657,497),new Point(691,497),new Point(725,497),new Point(759,497),new Point(657,531),new Point(691,531),new Point(725,531),new Point(759,531)};
        Check(Enumerable.Range(0,8).Select(main.SlotIcon).SequenceEqual(icons),"8 icon positions = V5 table (F5–F8 top row, F9–F12 bottom row)");
        Check(main.SlotAt(new(655,495))==0&&main.SlotAt(new(688,528))==0&&main.SlotAt(new(689,495))==1&&main.SlotAt(new(655,529))==4&&main.SlotAt(new(790,562))==7,"click → slot (mx−655)/34 + 4·((my−495)/34)");
        Check(main.SlotAt(new(654,500))<0&&main.SlotAt(new(791,500))<0&&main.SlotAt(new(700,563))<0&&main.SlotAt(new(700,494))<0,"outside the 4×2 grid → no slot");
        Check(ex.Bounds==new Rectangle(485,475,158,125)&&ex.IconOrigin==new Point(495,495),"extended frame 3226 at (485,475), icons from (495,495)");
        Check(ex.SlotIcon(11)==new Point(495+102,495+68)&&ex.SlotAt(new(495,495))==0&&ex.SlotAt(new(630,596))==11&&ex.SlotAt(new(494,495))<0&&ex.SlotAt(new(495,597))<0,"extended 4×3 grid, pitch 34");
        Check(ui.PaintOrder().ToList().IndexOf(main)>ui.PaintOrder().ToList().IndexOf(ex),"RIGHT_WINDOW above EX_MACRO_WINDOW (lock button at x=634 overlaps the slot)");
        var group=ui.SlotGroup(UiSlotGroup.Bar485);
        Check(group.Members.Contains(ExMacroPanel.WindowId)&&group.IsShown(ExMacroPanel.WindowId),"extended macro is a BAR_485 member (only member → shown)");

        // --- synthetic items / spells.
        var stamp=new PlayerHudStamp(3,5,77);
        var potion=new PlayerHudBinding(PlayerHudKind.Item,1001);var junk=new PlayerHudBinding(PlayerHudKind.Item,1002);var gone=new PlayerHudBinding(PlayerHudKind.Item,9999);
        var heal=new PlayerHudBinding(PlayerHudKind.Magic,3);var unsupported=new PlayerHudBinding(PlayerHudKind.Magic,40);
        MacroInventory Inv(PlayerHudStamp s,bool busy=false)=>new(s,
            [new(potion,"合成藥水",10,5,true),new(junk,"合成不可用道具",11,1,false)],
            [new(heal,"合成治癒術",null,null,true),new(unsupported,"合成未支援魔法",null,null,false)],busy);
        var book=new MacroBook();book.Observe(stamp);
        Check(book.Assign(0,0,potion)&&book.Assign(0,1,heal)&&book.Assign(0,2,junk)&&book.Assign(0,3,gone)&&book.Assign(0,4,unsupported),"bind page 1 (F5–F9)");
        Check(book.Assign(1,0,heal)&&book.Assign(2,7,potion)&&book.AssignExtended(11,heal),"bind page 2 F5, page 3 F12, extended slot 12");
        Throws<ArgumentException>(()=>book.Assign(0,5,new PlayerHudBinding(PlayerHudKind.Item,0)),"id 0 binding rejected");
        Throws<ArgumentOutOfRangeException>(()=>book.Assign(3,0,potion),"page 4 does not exist");
        Throws<ArgumentOutOfRangeException>(()=>book.Assign(0,8,potion),"slot 9 does not exist");
        Check(book.Persisted.Bindings[0]==potion&&book.Persisted.Bindings[1]==heal,"page 1 is stored in the CharacterConfig slots");

        var log=new List<(PlayerHudStamp,PlayerHudBinding)>();var cmd=new UiMacroCommands(new UiNullCommands(),(s,b)=>log.Add((s,b)));
        Bitmap icon=new(32,32,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(icon))g.Clear(Color.FromArgb(255,200,0,0));
        MacroTestState S(bool inWorld=true,bool input=true,MacroInventory? inv=null)=>new(inWorld,book,inv??Inv(stamp),input,id=>id==10?icon:null);
        var input=new UiInput(ui,cmd){Viewport=UiPresenter.Compute(new(800,600))};
        bool K(Keys k,bool down=true,bool repeat=false)=>input.Key(new UiKeyEvent(k,down,repeat));
        PlayerHudBinding? Last()=>log.Count==0?null:log[^1].Item2;

        ui.Update(S(inWorld:false));
        Check(!main.Visible&&!ex.Visible&&!K(Keys.F5)&&log.Count==0,"outside the world: hidden, F5 not taken");
        ui.Update(S());
        Check(main.Visible&&ex.Visible,"in the world: both shown");
        Check(K(Keys.F5)&&log.Count==1&&Last()==potion&&log[0].Item1==stamp,"F5 → page 1 slot 1 → item, with the displayed stamp");
        Check(K(Keys.F6)&&log.Count==2&&Last()==heal,"F6 → spell");
        Check(K(Keys.F7)&&log.Count==2,"F7 → unsupported item: key taken, nothing sent");
        Check(K(Keys.F8)&&log.Count==2,"F8 → item no longer in the inventory: nothing sent");
        Check(K(Keys.F9)&&log.Count==2,"F9 → spell not supported: nothing sent");
        Check(K(Keys.F12)&&log.Count==2,"F12 → empty slot: key taken, nothing sent");
        Check(K(Keys.F5,repeat:true)&&log.Count==2,"auto-repeat swallowed without activation");
        Check(K(Keys.F5,down:false)&&log.Count==2,"key-up swallowed");
        Check(!K(Keys.Control|Keys.F5)&&!K(Keys.Alt|Keys.F3)&&log.Count==2,"Ctrl+F5 / Alt+F3 left to other handlers");
        Check(!K(Keys.F4)&&log.Count==2,"F4 (pickup) is not a macro key");
        Check(book.Selected==7&&!book.SelectionInExtended,"last F-key moved the select mark");
        Check(K(Keys.F2)&&book.Page==1&&K(Keys.F5)&&Last()==heal&&log.Count==3,"F2 → page 2, F5 → page 2 slot 1");
        Check(K(Keys.F3)&&book.Page==2&&K(Keys.F12)&&Last()==potion&&log.Count==4,"F3 → page 3, F12 → page 3 slot 8 (real slot 23)");
        Check(K(Keys.F1)&&book.Page==0,"F1 → page 1");
        ui.Update(S(input:false));
        Check(!K(Keys.F5)&&!K(Keys.F2)&&log.Count==4&&book.Page==0,"no game keyboard focus (chat/dev tools): F-keys not used");
        ui.Update(S(inv:Inv(stamp,busy:true)));
        Check(K(Keys.F5)&&log.Count==4,"busy: nothing sent");
        ui.Update(S(inv:Inv(stamp with{Actor=78})));
        Check(K(Keys.F5)&&log.Count==4,"inventory of another character: nothing sent");
        ui.Update(S());

        // --- mouse: tabs, select, double click, lock.
        UiMouseResult Click(int x,int y,int clicks=1){var r=input.Mouse(UiMouseKind.Down,new(x,y),MouseButtons.Left,clicks);input.Mouse(UiMouseKind.Up,new(x,y),MouseButtons.Left);return r;}
        Check(Click(685,485).Route==UiRoute.Panel&&book.Page==1,"tab 2 click → page 2");
        Click(708,485);Check(book.Page==2,"tab 3 click → page 3");Click(660,485);Check(book.Page==0,"tab 1 click → page 1");
        Click(700,500);Check(book.Selected==1&&log.Count==4,"single click selects slot 2 without using it");
        Click(700,500,2);Check(log.Count==5&&Last()==heal,"double click uses slot 2");
        Click(640,520);Check(book.Locked,"MACRO_LOCK click → locked");
        Check(!book.Assign(0,0,heal)&&book.Binding(0,0)==potion&&!book.AssignExtended(0,heal),"locked: bindings cannot change");
        Check(K(Keys.F5)&&Last()==potion,"locked: slots still usable");
        Click(640,520);Check(!book.Locked&&book.Assign(0,7,heal)&&book.Assign(0,7,null),"unlock → bindings editable again");
        Click(630,596);Check(book.SelectionInExtended&&book.SelectedExtended==11,"extended click selects and moves the mark there");
        int before=log.Count;Click(630,596,2);Check(log.Count==before+1&&Last()==heal,"extended double click uses extended slot 12");
        group.Show(ExMacroPanel.WindowId);
        // Product nesting since #465: WithHpMpState(NativeMacroGameState(…)) — macro panels must see through UiHpMpGameState.
        int nested=log.Count;ui.Update(new UiHpMpGameState(S(),[]));
        Check(MacroBook.StateOf(new UiHpMpGameState(S(),[]))!=null&&main.Visible&&K(Keys.F5)&&log.Count==nested+1&&Last()==potion,"macro state found inside UiHpMpGameState: F5 still activates");
        log.RemoveAt(log.Count-1);ui.Update(S());

        // --- paint.
        {
            book.Select(4);
            using var canvas=new UiCanvas();ui.Compose(canvas,null);
            int At(int x,int y)=>canvas.Bitmap.GetPixel(x,y).ToArgb();
            Check(At(645,595)==Rgb(Frame)&&At(799,476)==Rgb(Frame),"frame drawn 1:1 at (643,476)");
            Check(At(660,485)==Rgb(TabS)&&At(685,485)==Rgb(TabN)&&At(708,485)==Rgb(TabN),"current tab uses a+1, others a");
            Check(At(660,500)==Color.FromArgb(255,200,0,0).ToArgb(),"slot 1 shows the item icon at (657,497)");
            Check(At(695,500)==Rgb(Frame),"spell slot: no verified icon → nothing drawn over the frame");
            Check(At(725+5,497+5)==Rgb(Frame),"unsupported item without icon → nothing drawn");
            bool yellow=false;for(int y=510;y<535;y++)for(int x=665;x<690;x++){var c=canvas.Bitmap.GetPixel(x,y);yellow|=c.R>200&&c.G>200&&c.B<80;}
            Check(yellow,"item count 5 drawn in palette[13] yellow near (677,517)");
            Check(At(655,529)==Rgb(Mark)&&At(690,564)==Rgb(Mark)&&At(670,545)==Rgb(Frame),"select mark on slot 5 at (655,529), colour-keyed inside");
            Check(At(655,495)!=Rgb(Mark),"no mark on the previous slot");
            Check(At(636,520)==Rgb(LockOpen),"unlocked: open padlock a+1 (3192)");
            Check(At(490,480)==Rgb(Ex)&&At(495,495)!=Rgb(Mark),"extended frame drawn; mark is in the main box");
            book.ToggleLock();book.SelectExtended(0);ui.Compose(canvas,null);
            Check(At(636,520)==Rgb(LockClosed)&&At(493,493)==Rgb(Mark)&&At(655,529)!=Rgb(Mark),"locked: closed padlock a (3191); mark moves to extended slot 1 at (493,493)");
            book.ToggleLock();
            ui.Update(S(inWorld:false));ui.Compose(canvas,null);
            Check(At(700,560)==Color.Black.ToArgb()&&At(490,480)==Color.Black.ToArgb(),"outside the world nothing is drawn");
            ui.Update(S());
        }

        // --- per character, and the CharacterConfig round trip / migration of the old F1–F4 blobs.
        {
            book.Observe(stamp with{Actor=78});
            Check(book.Page==0&&Enumerable.Range(0,8).All(k=>book.Binding(0,k)==null&&book.Binding(1,k)==null)&&book.Extended(11)==null,"another character starts empty");
            book.Observe(stamp);
            Check(book.Binding(0,0)==potion&&book.Binding(1,0)==heal&&book.Binding(2,7)==potion&&book.Extended(11)==heal,"back to the first character: all pages kept for the session");
            var blob=CharacterConfig.FromSlots(book.PageBindings(0));
            Check(blob.Data.Length==CharacterConfig.EncodedLength(8)&&new CharacterConfig(blob.Data).Slots!.SequenceEqual(book.PageBindings(0)),"page 1 round-trips through TWCC v1 (8 slots)");
            byte[] legacy=new byte[CharacterConfig.EncodedLength(4)];"TWCC"u8.CopyTo(legacy);legacy[4]=1;legacy[5]=4;
            legacy[6]=1;BitConverter.GetBytes(1001u).CopyTo(legacy,7);legacy[11]=2;BitConverter.GetBytes(3u).CopyTo(legacy,12);
            var migrated=MacroBook.MigrateStored(new CharacterConfig(legacy))!;
            var fresh=new MacroBook();fresh.Observe(stamp);
            for(int i=0;i<CharacterConfig.SlotCount;i++)fresh.Persisted.Assign(i,migrated[i]);   // what CharacterConfigHud does on login
            Check(fresh.Binding(0,0)==potion&&fresh.Binding(0,1)==heal&&Enumerable.Range(2,6).All(k=>fresh.Binding(0,k)==null),"old 4-slot blob (F1–F4 era) → page 1 F5/F6…, F9–F12 empty");
            Check(Enumerable.Range(0,8).All(k=>fresh.Binding(1,k)==null&&fresh.Binding(2,k)==null),"pages 2–3 are never filled from storage");
            Check(MacroBook.MigrateStored(new CharacterConfig([1,2,3,4,5,6,7]))==null,"unknown stored format → not migrated (kept on the server)");
            var s2=new MacroTestState(true,fresh,Inv(stamp),true,null);var log2=new List<PlayerHudBinding>();
            Check(MacroBook.TryActivate(s2,fresh.Binding(0,0),new UiMacroCommands(new UiNullCommands(),(_,b)=>log2.Add(b)))&&log2.SequenceEqual(new[]{potion}),"migrated F1 binding fires on F5");
            Check(!MacroBook.TryActivate(s2,potion,new UiNullCommands()),"commands without macro support → nothing");
        }

        // --- missing surfs: nothing drawn, no placeholder; extended box hidden without its frame.
        {
            var partial=full.Where(kv=>kv.Key is not (3191 or 3192 or 3226)).ToDictionary(kv=>kv.Key,kv=>kv.Value);
            using var res2=MacroResources(Path.Combine(root,"partial"),partial,MacroLayoutXml);
            var ui2=UiRoot.Build(res2,MacroRegistry);
            var m2=(MacroPanel)ui2.Find(MacroPanel.WindowId)!;var e2=(ExMacroPanel)ui2.Find(ExMacroPanel.WindowId)!;
            ui2.Update(S());
            Check(m2.LockRect.IsEmpty&&m2.Bounds==m2.FrameRect&&m2.Visible&&!e2.Visible,"missing lock/extended surf → no lock rect, extended box hidden");
            using var canvas=new UiCanvas();ui2.Compose(canvas,null);
            Check(canvas.Bitmap.GetPixel(636,520).ToArgb()==Color.Black.ToArgb()&&canvas.Bitmap.GetPixel(490,480).ToArgb()==Color.Black.ToArgb(),"missing surfs draw nothing");
            var r2=new UiInput(ui2,cmd){Viewport=UiPresenter.Compute(new(800,600))};
            Check(r2.Mouse(UiMouseKind.Down,new(636,520),MouseButtons.Left,1).Route==UiRoute.World,"no lock image → that point is not the macro box");
        }

        // --- malformed layouts are refused (no invented coordinates).
        {
            var surfs=res.Surfs;
            var noTab=UiLayout.Parse(MacroLayoutXml.Replace("action = \"12\"","action = \"9\""),"bad-tab.xml");
            Throws<UiLayoutException>(()=>new MacroPanel().Load(noTab,surfs),"tab action outside 10–12 refused");
            var noLock=UiLayout.Parse(MacroLayoutXml.Replace("MACRO_LOCK","OTHER"),"bad-lock.xml");
            Throws<UiLayoutException>(()=>new MacroPanel().Load(noLock,surfs),"missing MACRO_LOCK refused","MACRO_LOCK");
            var noFrame=UiLayout.Parse(MacroLayoutXml.Replace("<MACRO_FRAME","<OTHER_FRAME"),"bad-frame.xml");
            Throws<UiLayoutException>(()=>new MacroPanel().Load(noFrame,surfs),"missing MACRO_FRAME refused","缺少節點");
        }
        icon.Dispose();
        NativeUiMacroRealClient(repo);
    }

    /// <summary>Optional (TW_CLIENT_ROOT): real Tile/Sprite, real registry; PNG and coordinate table only under scratch/issue-466/real.</summary>
    static void NativeUiMacroRealClient(string repo){
        string? client=Environment.GetEnvironmentVariable("TW_CLIENT_ROOT");
        if(string.IsNullOrEmpty(client)){Console.WriteLine("SKIP native-ui macro real client render (TW_CLIENT_ROOT not set)");return;}
        string outDir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-466","real");Directory.CreateDirectory(outDir);
        using var res=UiResources.Open(client);
        var ui=UiRoot.Build(res,MacroRegistry);
        var main=(MacroPanel)ui.Find(MacroPanel.WindowId)!;var ex=(ExMacroPanel)ui.Find(ExMacroPanel.WindowId)!;
        var book=new MacroBook();var stamp=new PlayerHudStamp(1,1,1);book.Observe(stamp);
        ui.Update(new MacroTestState(true,book,new MacroInventory(stamp,[],[],false),true,null));
        Check(main.FrameRect==new Rectangle(643,476,157,124),"real 3176 frame = (643,476) 157×124");
        Check(ex.Bounds==new Rectangle(485,475,158,125),"real 3226 extended frame = (485,475) 158×125");
        using var canvas=new UiCanvas();
        foreach(var (page,name) in new[]{(0,"page1"),(1,"page2"),(2,"page3")}){
            book.SetPage(page);ui.Compose(canvas,null);canvas.Bitmap.Save(Path.Combine(outDir,$"macro-{name}.png"),ImageFormat.Png);
        }
        book.ToggleLock();book.SetPage(0);book.Select(5);ui.Compose(canvas,null);canvas.Bitmap.Save(Path.Combine(outDir,"macro-locked-select6.png"),ImageFormat.Png);
        File.WriteAllText(Path.Combine(outDir,"macro-coordinates.json"),System.Text.Json.JsonSerializer.Serialize(new{
            client,frame=main.FrameRect.ToString(),bounds=main.Bounds.ToString(),lockRect=main.LockRect.ToString(),tabs=main.TabRects.Select(r=>r.ToString()),
            icons=Enumerable.Range(0,8).Select(k=>main.SlotIcon(k).ToString()),marks=Enumerable.Range(0,8).Select(k=>main.SlotMark(k).ToString()),
            extended=new{bounds=ex.Bounds.ToString(),icons=Enumerable.Range(0,12).Select(k=>ex.SlotIcon(k).ToString())}},
            new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    }
}
