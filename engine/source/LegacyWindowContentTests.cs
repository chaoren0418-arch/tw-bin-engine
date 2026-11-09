using System.Drawing.Imaging;
using P06.NativeUi;
namespace P06;

// R4-B（T294／#706）離線測試：背包、魔法、倉庫原生窗的內容——共用圖示來源（FU5§4 的 +36＝背包圖示 gfx、487／1617 規則）、
// 格子幾何、分頁與等級鈕的點擊、格子／列的選取、倉庫列拖到確定鈕、圖示與選取框的像素。
// 只用合成 XML／surf／圖示，不讀客戶端、不連線、不開視窗。由 --module-tests 執行（名稱 native-legacy-window-content）。
static class LegacyWindowContentTests {
    static UiMouseEvent M(UiMouseKind kind,Point at)=>new(kind,at,MouseButtons.Left,1,0,Keys.None);
    static void Press(IUiPanel p,Point at,IUiCommands c){p.Mouse(M(UiMouseKind.Down,at),c);}
    static void Drop(IUiPanel p,Point at,IUiCommands c){p.Mouse(M(UiMouseKind.Up,at),c);}
    static void Tap(IUiPanel p,Point at,IUiCommands c){Press(p,at,c);Drop(p,at,c);}
    static Point Mid(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);

    /// <summary>Synthetic icons: solid 32×32 in the kit colour of (icon + 50000), and a log of requested ids.</summary>
    sealed class Icons {
        public readonly List<int> Requested=[];
        readonly HashSet<int> present;
        public Icons(params int[] ids)=>present=[..ids];
        public static int ColourOf(int icon)=>LegacyWindowTestKit.ColourOf(icon+50000);
        public ItemIcon? Get(int icon){
            Requested.Add(icon);
            if(!present.Contains(icon))return null;
            var bmp=new Bitmap(ItemIconSource.IconSize,ItemIconSource.IconSize,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(bmp))g.Clear(Color.FromArgb(ColourOf(icon)));
            return new(icon,bmp,Point.Empty,"synthetic");
        }
        public ItemIconSource Source=>new(Get);
    }
    sealed class WarehouseCommands:IUiCommands,IUiWarehouseCommands {
        public readonly List<string> Calls=[];
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus(){}
        public void Logout(){}
        public void SubmitWarehouse(int revision,IReadOnlyList<ShopChoice> choices)=>Calls.Add($"submit:{revision}:"+string.Join(",",choices.Select(c=>$"{c.Id}x{c.Count}")));
        public void CancelWarehouse(int revision)=>Calls.Add($"cancel:{revision}");
    }

    [WorldModuleTest("native-legacy-window-content")]
    static void Run(WorldModuleTestContext t){
        Shared(t);
        Item(t);
        Spell(t);
        Warehouse(t);
    }

    // 1. 共用：圖示編號規則、名稱、數量文字、格子與分頁條。
    static void Shared(WorldModuleTestContext t){
        t.Check(ItemIconSource.IconOf(487,0)==1617&&ItemIconSource.IconOf(1617,0)==1617&&ItemIconSource.IconOf(487,1)==487&&ItemIconSource.IconOf(1617,2)==1617&&ItemIconSource.IconOf(87,0)==87,
            "FU5§4 sub_518CD0: gfx 487/1617 with bless 0 → 1617; others as sent");
        t.Check(ItemIconSource.None.Get(5)==null&&new ItemIconSource(_=>throw new Exception("asked")).Get(0)==null,"no archives → no icon; icon 0 is never looked up");
        t.Check(ReferenceEquals(ItemIconSource.FromSprites(null),ItemIconSource.None)&&ReferenceEquals(ItemIconSource.FromRoot(new UiRoot()),ItemIconSource.None),"root without resources → None");

        string[] names=["零","一","二","長劍"];
        t.Equal("長劍 (5)",ItemText.Resolve("$3 (5)",names),"$n resolved through the names table");
        t.Equal("$99",ItemText.Resolve("$99",names),"unknown reference stays");
        t.Equal("$3",ItemText.Resolve("$3",null),"no table → raw string");
        t.Equal("長劍",ItemText.Name("長劍 (5)",5),"trailing (count) removed");
        t.Equal("長劍 (5)",ItemText.Name("長劍 (5)",6),"other counts kept");
        t.Check(ItemText.Count(0)==""&&ItemText.Count(1)==""&&ItemText.Count(500)=="500","count label only for stacks");

        var g=LegacyCellGrid.Fit(6,44,171,235,new(33,33));
        t.Check(g.Columns==5&&g.Rows==5&&g.Origin==new Point(6,57)&&g.Cell==new Size(32,32),"item grid 5×5 from (6,57) with the local ITEM_WINDOW numbers",g);
        t.Equal(new Rectangle(39,90,32,32),g.CellRect(6),"slot 6 = row 1, column 1");
        t.Check(g.SlotAt(new(40,91))==6&&g.SlotAt(new(38,60))==null&&g.SlotAt(new(5,60))==null&&g.SlotAt(new(171,60))==null,"slot hit test; gaps and outside → null");
        t.Check(g.ScrollPositions(0)==0&&g.ScrollPositions(25)==0&&g.ScrollPositions(26)==2&&g.ScrollPositions(31)==3,"scroll positions by row");
        t.Throws<ArgumentOutOfRangeException>(()=>g.CellRect(25),"slot beyond the page");
        t.Check(LegacyCellGrid.Fit(10,10,20,20,new(33,33)).PageSize==0,"no room → empty grid");
        var rows=LegacyCellGrid.Fit(17,47,217,300,new(200,33),200);
        t.Check(rows.Columns==1&&rows.Rows==7&&rows.Origin==new Point(17,58)&&rows.Cell==new Size(200,32),"warehouse list: 7 rows of 200×32 from (17,58)",rows);

        var strip=new Rectangle(27,235,130,20);
        t.Check(LegacyStrip.SegmentAt(strip,4,new(27,240))==0&&LegacyStrip.SegmentAt(strip,4,new(27+65,240))==2&&LegacyStrip.SegmentAt(strip,4,new(156,240))==3&&LegacyStrip.SegmentAt(strip,4,new(160,240))==null,
            "strip segments are equal parts of the image width");
    }

    static LegacyWindowTestKit.SurfaceSet ItemSurfaces()=>LegacyWindowTestKit.Surfaces(new(){
        [2046]=(170,260),[2047]=(200,290),[2037]=(12,12),[2039]=(10,160),[2040]=(10,12),[2042]=(10,12),[2044]=(10,10),
        [2048]=(130,20),[2049]=(130,20),[2050]=(130,20),[2051]=(130,20),[2052]=(22,22),[2054]=(22,22)});

    // 2. 背包：格子、圖示、數量、選取、分頁、捲動。
    static void Item(WorldModuleTestContext t){
        var icons=new Icons(5,7,1617);
        var p=new ItemWindowPanel(icons.Source);p.Load(UiLayout.Parse(ItemWindowPanel.TestXml,"uimagicitem.xml"),ItemSurfaces().Get);
        t.Check(p.Grid.Columns==5&&p.Grid.Rows==5&&p.Grid.Origin==new Point(6,57),"ITEM_WINDOW grid derived from base/scrollbar/arrow/tab positions",p.Grid);
        InventoryItem Inv(uint id,byte use,ushort icon,uint count=1,byte bless=1)=>new(id,1,use,0,icon,bless,count,1,$"$3 ({count})","","");
        // 30 items: 1–2 equipment (use 1), the rest use 0; item 3 = gfx 487 bless 0 (→ 1617); item 4 = 500 arrows.
        var inv=Enumerable.Range(1,30).Select(k=>Inv((uint)k,(byte)(k<=2?1:0),(ushort)(k==3?487:k%2==0?7:5),k==4?500u:1u,(byte)(k==3?0:1))).ToArray();
        var picks=new List<uint?>();uint? gameSelected=null;
        UiItemWindowData Data()=>new(inv,gameSelected,id=>{picks.Add(id);gameSelected=id;return true;});
        var basic=new UiStaticGameState(InWorld:true);
        IUiGameState S()=>new UiHpMpGameState(new UiItemWindowGameState(basic,Data()),[]);
        var cmd=new UiNullCommands();
        p.Open();p.Update(S());
        t.Check(p.Visible&&p.PageItems.Count==25&&p.PageItems[0].ObjectId==1,"page 1 shows 25 of 30 items in server order");
        p.ResolveName=s=>ItemText.Resolve(s,["零","一","二","長劍"]);
        t.Check(p.NameOf(inv[0])=="長劍"&&p.NameOf(inv[3])=="長劍","item name: $3 resolved, (count) removed");

        // Scroll by row: 30 items = 6 rows → 2 positions.
        Tap(p,Mid(p.ScrollDownRect),cmd);
        t.Check(p.ScrollOffset==1&&p.PageItems.Count==25&&p.PageItems[0].ObjectId==6,"down arrow → one row (5 items) further");
        Tap(p,Mid(p.ScrollDownRect),cmd);t.Equal(1,p.ScrollOffset,"clamped at the last row");
        Tap(p,Mid(p.ScrollUpRect),cmd);t.Equal(0,p.ScrollOffset,"up arrow back");

        // Cell click → WorldSession-style selector; game selection drives the frame.
        Tap(p,Mid(p.CellRect(3)),cmd);
        t.Check(picks.SequenceEqual(new uint?[]{4})&&cmd.Calls.Count==0,"click on slot 3 selects item 4 through the game model, no command");
        p.Update(S());t.Equal((uint?)4,p.SelectedId,"selection read back from the game state");
        Tap(p,new(p.CellRect(0).Right,p.CellRect(0).Y+5),cmd);
        t.Equal(1,picks.Count,"click in the gap between cells selects nothing");

        // Pixels: icons in cells (bless rule), missing icon draws nothing, selected cell framed.
        using(var canvas=new UiCanvas()){
            var root=new UiRoot();root.Add(p);root.Update(S());root.Compose(canvas,null);
            t.Equal(Icons.ColourOf(5),canvas.Bitmap.GetPixel(p.CellRect(0).X+10,p.CellRect(0).Y+8).ToArgb(),"item 1 icon 5 drawn in slot 0");
            t.Equal(Icons.ColourOf(1617),canvas.Bitmap.GetPixel(p.CellRect(2).X+10,p.CellRect(2).Y+8).ToArgb(),"item 3 (gfx 487, bless 0) drawn with icon 1617");
            t.Equal(Icons.ColourOf(7),canvas.Bitmap.GetPixel(p.CellRect(1).X+10,p.CellRect(1).Y+8).ToArgb(),"item 2 icon 7 drawn in slot 1");
            t.Check(icons.Requested.Contains(1617)&&!icons.Requested.Contains(487),"icon source asked for 1617, never 487");
            int frame=canvas.Bitmap.GetPixel(p.CellRect(3).X,p.CellRect(3).Y+16).ToArgb();
            t.Equal(Color.FromArgb(255,230,120).ToArgb(),frame,"selected slot 3 has the selection frame");
        }
        var bare=new ItemWindowPanel();bare.Load(UiLayout.Parse(ItemWindowPanel.TestXml,"uimagicitem.xml"),ItemSurfaces().Get);bare.Open();
        using(var canvas=new UiCanvas()){
            var root=new UiRoot();root.Add(bare);root.Update(new UiItemWindowGameState(basic,new UiItemWindowData(inv)));root.Compose(canvas,null);
            t.Equal(LegacyWindowTestKit.ColourOf(2047),canvas.Bitmap.GetPixel(bare.CellRect(0).X+10,bare.CellRect(0).Y+8).ToArgb(),"no icon source → the window image shows through (no placeholder)");
        }
        // Without a selector (offline / pre-bridge state) the window keeps its own selection.
        Tap(bare,Mid(bare.CellRect(1)),cmd);t.Equal((uint?)2,bare.SelectedId,"no game selector → local selection");
        bare.Update(new UiItemWindowGameState(basic,new UiItemWindowData(inv[2..])));t.Equal((uint?)null,bare.SelectedId,"item gone → local selection dropped");

        // Tabs: clicking a quarter of the strip; 任務 empty, 其他 = not equipment.
        var strip=p.TabStripRect;
        Tap(p,new(strip.X+strip.Width*3/4+3,strip.Y+5),cmd);
        t.Check(p.Tab==3&&p.TabItems.Count==28&&p.TabItems.All(i=>!EquipmentRules.IsEquipment(i.UseType)),"tab strip last quarter → 其他 (28 non-equipment)");
        Tap(p,new(strip.X+strip.Width/4+3,strip.Y+5),cmd);
        t.Check(p.Tab==1&&p.TabItems.All(i=>EquipmentRules.IsEquipment(i.UseType)),"second quarter → 裝備");
        Tap(p,new(strip.X+strip.Width/2+3,strip.Y+5),cmd);
        t.Check(p.Tab==2&&p.TabItems.Count==0&&p.PageItems.Count==0,"third quarter → 任務 (empty in 3.81)");
        Tap(p,new(strip.X+2,strip.Y+5),cmd);t.Equal(0,p.Tab,"first quarter → 全部");
        t.Check(!p.IsDragHandle(Mid(p.CellRect(12)))&&!p.IsDragHandle(new(strip.X+5,strip.Y+5)),"cells and the tab strip are not drag handles");

        t.Throws<UiLayoutException>(()=>new ItemWindowPanel().Load(UiLayout.Parse(ItemWindowPanel.TestXml.Replace("xPos = \"171\" yPos = \"58\"","xPos = \"10\" yPos = \"58\""),"uimagicitem.xml"),ItemSurfaces().Get),
            "scrollbar left of the base → no room for cells → UiLayoutException");
    }

    // 3. 魔法：等級鈕切頁、列文字、選取。
    static void Spell(WorldModuleTestContext t){
        var sizes=new Dictionary<int,(int,int)>{[2056]=(170,260),[2057]=(200,290),[2037]=(12,12),[2039]=(10,160),[2040]=(10,12),[2042]=(10,12),[2044]=(10,10)};
        for(int s=2058;s<=2090;s++)sizes[s]=(150,18);
        var p=new SpellWindowPanel();p.Load(UiLayout.Parse(SpellWindowPanel.TestXml(),"uimagicitem.xml"),LegacyWindowTestKit.Surfaces(sizes).Get);
        t.Check(p.Rows.Columns==1&&p.Rows.Rows==10&&p.Rows.Origin==new Point(6,60)&&p.Rows.Cell==new Size(165,15),"spell rows: 10 × 16 px below the level strip",p.Rows);
        t.Check(SpellWindowPanel.PageOf(1)==0&&SpellWindowPanel.PageOf(8)==0&&SpellWindowPanel.PageOf(9)==1&&SpellWindowPanel.PageOf(80)==9&&SpellWindowPanel.PageOf(0)==-1,"page = C164 mask row (id−1)/8");
        LearnedMagic L(int id)=>new(id,$"合成技能 {id}",id!=10,"自己",id);
        var learned=new[]{L(1),L(2),L(9),L(10),L(17)};
        var picks=new List<int?>();int? selected=null;
        IUiGameState S(LegacySpellClass? c)=>new UiSpellWindowGameState(new UiStaticGameState(InWorld:true),new UiSpellWindowData(c,learned,selected,id=>{picks.Add(id);selected=id;return true;}));
        var cmd=new UiNullCommands();
        p.Open();p.Update(S(null));
        t.Check(p.PageSpells.Count==0,"no class → no level button → no rows");
        p.Update(S(LegacySpellClass.General));
        t.Check(p.PageSpells.Select(m=>m.Id).SequenceEqual(new[]{1,2}),"level 1 page = ids 1–8");
        var strip=p.LevelRect;   // 150 px, 10 general levels → 15 px each
        Tap(p,new(strip.X+15+2,strip.Y+5),cmd);
        t.Check(p.SelectedLevel==1&&p.PageSpells.Select(m=>m.Id).SequenceEqual(new[]{9,10}),"second level segment → page 2 (ids 9–16)");
        Tap(p,new(strip.X+15*9+2,strip.Y+5),cmd);
        t.Check(p.SelectedLevel==9&&p.PageSpells.Count==0,"tenth segment → level 10, nothing learned there");
        Tap(p,new(strip.X+15*2+2,strip.Y+5),cmd);t.Check(p.SelectedLevel==2&&p.PageSpells.Single().Id==17,"third segment → id 17");
        p.Update(S(LegacySpellClass.Elf));
        t.Equal(0,p.SelectedLevel,"class change resets the level");
        Tap(p,new(strip.X+strip.Width-2,strip.Y+5),cmd);t.Equal(10,p.SelectedLevel,"elf strip has 11 segments: last → level 11");
        p.SetLevel(1);
        Tap(p,Mid(p.RowRect(1)),cmd);
        t.Check(picks.SequenceEqual(new int?[]{10})&&cmd.Calls.Count==0,"row click selects spell 10 through the game model, no command");
        p.Update(S(LegacySpellClass.Elf));t.Equal((int?)10,p.SelectedId,"selection read back");
        Tap(p,Mid(p.RowRect(5)),cmd);t.Equal(1,picks.Count,"empty row selects nothing");
        t.Check(p.Rows.ScrollPositions(8)==0,"a level page holds at most 8 spells → fits 10 rows, no scroll");
        t.Check(!p.IsDragHandle(Mid(p.RowRect(0)))&&!p.IsDragHandle(new(strip.X+3,strip.Y+3)),"rows and the level strip are not drag handles");
        using(var canvas=new UiCanvas()){
            var root=new UiRoot();root.Add(p);root.Update(S(LegacySpellClass.Elf));root.Compose(canvas,null);
            var r=p.RowRect(1);
            t.Equal(Color.FromArgb(255,230,120).ToArgb(),canvas.Bitmap.GetPixel(r.X,r.Y+7).ToArgb(),"selected row framed");
            var row0=p.RowRect(0);bool text=false;
            for(int x=row0.X+3;x<row0.X+60&&!text;x++)for(int y=row0.Y;y<row0.Bottom&&!text;y++)text=canvas.Bitmap.GetPixel(x,y).ToArgb()!=LegacyWindowTestKit.ColourOf(2057);
            t.Check(text,"row 0 has text drawn over the window image");
        }
    }

    static LegacyWindowTestKit.SurfaceSet WarehouseSurfaces()=>LegacyWindowTestKit.Surfaces(new(){
        [3001]=(220,320),[3002]=(250,360),[3031]=(14,14),[3024]=(10,220),[3025]=(10,12),[3027]=(10,12),[3029]=(10,10),
        [3007]=(60,14),[3014]=(50,18),[3017]=(50,18),[3008]=(30,12),[3009]=(30,12),[3010]=(9,9),[3012]=(9,9),
        [3020]=(180,20),[3021]=(180,20),[3022]=(180,20),[3023]=(180,20)});

    // 4. 倉庫：列、圖示、點選、拖到確定鈕、分頁點擊、捲動。
    static void Warehouse(WorldModuleTestContext t){
        var icons=new Icons(5,7);
        var p=new WarehouseWindowPanel(icons.Source){ResolveName=s=>ItemText.Resolve(s,["零","箭","長劍"])};
        p.Load(UiLayout.Parse(WarehouseWindowPanel.TestXml,"uiextra86-c.xml"),WarehouseSurfaces().Get);
        t.Check(p.RowGrid.Rows==7&&p.RowGrid.Origin==new Point(17,58)&&p.RowGrid.Cell==new Size(200,32),"WAREHOUSE_WINDOW rows from tab/scrollbar/arrow positions",p.RowGrid);
        var rows=Enumerable.Range(1,10).Select(k=>new ShopRow((uint)(100+k),(ushort)(k%2==0?7:5),30,k==1?"$1 (500)":"$2",k==1?500u:(uint)k,"")).ToArray();
        IUiGameState S(UiWarehouseView v)=>new UiWarehouseGameState(new UiStaticGameState(InWorld:true),v);
        var view=new UiWarehouseView("withdraw",4,rows,30);
        var cmd=new WarehouseCommands();
        p.Update(S(view));
        t.Check(p.Visible&&p.VisibleRows.Count==7&&p.VisibleRows[0].Id==101,"first 7 of 10 rows visible");
        t.Check(p.NameOf(rows[0])=="箭"&&p.NameOf(rows[1])=="長劍","row names resolved, (count) suffix removed");
        Tap(p,Mid(p.ScrollDownRect),cmd);Tap(p,Mid(p.ScrollDownRect),cmd);Tap(p,Mid(p.ScrollDownRect),cmd);Tap(p,Mid(p.ScrollDownRect),cmd);
        t.Check(p.ScrollOffset==3&&p.VisibleRows[0].Id==104,"scroll by row, clamped at rows − 7");
        Tap(p,Mid(p.ScrollUpRect),cmd);Tap(p,Mid(p.ScrollUpRect),cmd);Tap(p,Mid(p.ScrollUpRect),cmd);

        // Click toggles a row (whole count); press on one row and release on another does nothing.
        Tap(p,Mid(p.RowRect(0)),cmd);
        t.Check(p.Selection.TryGetValue(101,out var c)&&c==500&&cmd.Calls.Count==0,"click row 0 → 101 × 500 selected, nothing sent");
        Tap(p,Mid(p.RowRect(0)),cmd);t.Check(p.Selection.Count==0,"second click clears it");
        Press(p,Mid(p.RowRect(1)),cmd);Drop(p,Mid(p.RowRect(2)),cmd);
        t.Check(p.Selection.Count==0&&cmd.Calls.Count==0,"release on another row → nothing");
        t.Check(!p.IsDragHandle(Mid(p.RowRect(3))),"rows are not drag handles");

        // Drag a row onto OK: selects it and submits once; already-selected rows are submitted with it.
        Tap(p,Mid(p.RowRect(2)),cmd);
        Press(p,Mid(p.RowRect(0)),cmd);Drop(p,Mid(p.OkRect),cmd);
        t.Check(cmd.Calls.SequenceEqual(new[]{"submit:4:101x500,103x3"})&&p.Selection.Count==0,"row 0 dragged to OK → submit 101×500 with the selected 103×3, in server order");
        Press(p,Mid(p.RowRect(1)),cmd);Drop(p,new(400,400),cmd);
        t.Check(cmd.Calls.Count==1&&p.Selection.Count==0,"released outside the window (captured) → nothing");
        var plain=new UiNullCommands();
        Press(p,Mid(p.RowRect(1)),plain);Drop(p,Mid(p.OkRect),plain);
        t.Check(plain.Calls.SequenceEqual(new[]{"dev-tools:npc"})&&p.Selection.ContainsKey(102),"no warehouse commands → NPC page (transition), selection kept");
        p.Select(102,0);

        // Sorting keeps the drag mapping on the drawn row.
        Tap(p,Mid(p.AmountSortRect),cmd);Tap(p,Mid(p.AmountSortRect),cmd);
        t.Equal(101u,p.VisibleRows[0].Id,"amount descending → 500 arrows first");
        Tap(p,Mid(p.RowRect(1)),cmd);t.Check(p.Selection.ContainsKey(110),"click maps to the sorted row (110 × 10)");

        // Tabs: clickable quarters (rows are not split: S176 rows carry no use type, 待驗證).
        var strip=p.TabStripRect;
        Tap(p,new(strip.X+strip.Width*3/4+3,strip.Y+5),cmd);t.Check(p.Tab==3&&p.Rows.Count==10,"tab strip last quarter → tab 3, every row still listed");

        // Pixels: icons per row, selected row framed and drawn.
        using(var canvas=new UiCanvas()){
            var root=new UiRoot();root.Add(p);root.Update(S(view));root.Compose(canvas,null);
            var r0=p.RowRect(0);var r1=p.RowRect(1);
            t.Equal(Icons.ColourOf(5),canvas.Bitmap.GetPixel(r0.X+10,r0.Y+8).ToArgb(),"row 0 (101) icon 5");
            t.Equal(Icons.ColourOf(7),canvas.Bitmap.GetPixel(r1.X+10,r1.Y+8).ToArgb(),"row 1 (110) icon 7");
            t.Equal(Color.FromArgb(255,230,120).ToArgb(),canvas.Bitmap.GetPixel(r1.Right-1,r1.Y+16).ToArgb(),"selected row framed");
        }
        // A new list resets selection and scroll.
        p.Update(S(new UiWarehouseView("deposit",5,rows[..3],0)));
        t.Check(p.Selection.Count==0&&p.ScrollOffset==0&&p.VisibleRows.Count==3,"new revision → fresh list");
    }
}
