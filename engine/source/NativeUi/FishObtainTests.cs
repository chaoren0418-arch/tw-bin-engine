namespace P06.NativeUi;

// R10-A（T382／#888，FU10§1）離線測試：背包尾段「長度＋內容」、+10 旗標 bit 3 → 釣魚 desc id 表、世界重設清表、FishObtainUI 清單過濾與捲軸。
// 只用合成封包、合成版面、合成 surf；合成的延伸尾段（長度 > 10）只驗程式邏輯，不代表 3.81 會送（3.81 尾段固定 10 byte 全 0）。
// 不開遊戲、網路、視窗，不讀客戶端素材。CI 由 --module-tests 自動發現執行（名稱 fish-obtain）。
static class FishObtainTests {
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    /// <summary>3.81 S_InvList／S_AddItem 的尾段：writeC(10) writeH(0) writeD(0) writeD(0)。</summary>
    static readonly byte[] Tail381=[10,0,0,0,0,0,0,0,0,0,0];
    /// <summary>合成延伸尾段：長度 n、內容第 10 byte＝flag。</summary>
    static byte[] Tail(int n,byte flag){var b=new byte[1+n];b[0]=(byte)n;if(n>10)b[1+10]=flag;return b;}
    static byte[] Record(uint id,ushort desc,uint count,string name,byte[] tail)=>[..Le32(id),..Le16(desc),0,0,..Le16(12),1,..Le32(count),1,..Str(name),0,..tail];
    static byte[] AddItem(uint id,ushort desc,uint count,string name,byte[] tail)=>LoginPackets.Pad([15,..Record(id,desc,count,name,tail)]);
    static byte[] List(params byte[][] records)=>LoginPackets.Pad([5,(byte)records.Length,..records.SelectMany(x=>x)]);
    static void Feed(WorldSession s,byte[] p)=>s.Receive(p,"");

    [WorldModuleTest("fish-obtain")]
    static void Run(WorldModuleTestContext t){
        Registration(t);
        TailParse(t);
        Table(t);
        Panel(t);
    }

    static void Registration(WorldModuleTestContext t){
        var e=WorldExtensionRegistry.Default.Entries;
        t.Check(e.Any(x=>x.Kind=="reset"&&x.Method.EndsWith(".ResetFishObtainTable",StringComparison.Ordinal)),"[WorldReset] ResetFishObtainTable registered");
        t.Check(e.Any(x=>x.Kind=="snapshot"&&x.Key=="fishObtain"),"snapshot key fishObtain registered");
        t.Equal(0,WorldExtensionRegistry.Default.Errors.Count,"no extension registration errors");
        var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==FishObtainPanel.PanelId);
        t.Check(reg?.Create(new UiRoot()) is FishObtainPanel and IUiClosablePanel,"FishObtainUI still discovered, closable");
    }

    static void TailParse(WorldModuleTestContext t){
        // sub_518C80：只看前 n byte（最多 24），+10 & 8。
        t.Check(!FishObtainTable.Flag(10,new byte[10]),"n=10 (3.81): +10 out of range → no flag");
        t.Check(!FishObtainTable.Flag(10,[0,0,0,0,0,0,0,0,0,0,8]),"n=10 ignores a byte beyond n");
        t.Check(FishObtainTable.Flag(11,[0,0,0,0,0,0,0,0,0,0,8]),"n=11, +10 bit 3 → flag");
        t.Check(!FishObtainTable.Flag(11,[0,0,0,0,0,0,0,0,0,0,0xF7]),"other bits of +10 → no flag");
        t.Check(!FishObtainTable.Flag(18,[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0xFF,0xFF,0xFF,0xFF]),"+14 dword masked by 0 → no flag");
        t.Equal(-32767,FishObtainTable.Entry(0x8001),"entry = movsx of the 16-bit desc id");

        var s=new WorldSession();
        Feed(s,AddItem(100,1342,3,"$26 (3)",Tail381));
        var a=s.Inventory.Single();
        t.Check(!a.FishObtainFlag&&a.TailHex=="0A00000000000000000000","3.81 tail: TailHex unchanged (length + 10 bytes), no flag",a.TailHex);
        t.Equal(0,s.FishObtainIds.Length,"3.81 S15 leaves the table empty");
        // 既有合成封包用 11 byte 0（長度 0）：照舊讀 10 byte，不改既有解析。
        var legacy=new WorldSession();Feed(legacy,AddItem(101,10,1,"$23",new byte[11]));
        t.Check(legacy.Inventory.Single() is {FishObtainFlag:false,TailHex:"0000000000000000000000"},"length 0 + 10 bytes still parsed as before");
        // 長度 > 10：讀滿 n byte，後面的欄位對齊不受影響。
        var ext=new WorldSession();
        Feed(ext,List(Record(200,500,1,"fish",Tail(30,8)),Record(201,501,2,"plain",Tail381),Record(202,502,1,"other",Tail(12,0))));
        var items=ext.Inventory;
        t.Equal(3,items.Length,"S5 with mixed tail lengths parsed (n=30, 10, 12)");
        t.Check(items[0] is {ObjectId:200,FishObtainFlag:true}&&items[0].TailHex.Length==2*31,"n=30: 30 bytes read, flag set",items[0].TailHex);
        t.Check(items[1] is {ObjectId:201,DescriptionId:501,Count:2,FishObtainFlag:false},"next record after n=30 aligned");
        t.Check(items[2] is {ObjectId:202,FishObtainFlag:false}&&items[2].TailHex.Length==2*13,"n=12 without bit 3 → no flag");
        t.Check(ext.FishObtainIds.SequenceEqual([500]),"only the flagged desc id enters the table",ext.FishObtainIds);
        var broken=new WorldSession();
        t.Throws<ProtocolException>(()=>Feed(broken,AddItem(300,1,1,"x",[20,0,0,0])),"truncated long tail rejected");
        t.Equal(0,broken.FishObtainIds.Length,"rejected packet adds nothing");
    }

    static void Table(WorldModuleTestContext t){
        var s=new WorldSession();
        Feed(s,List(Record(1,700,5,"$700 (5)",Tail(11,8)),Record(2,701,1,"$701",Tail381)));
        t.Check(s.FishObtainIds.SequenceEqual([700]),"S5: flagged item desc id added",s.FishObtainIds);
        t.Check(s.FishObtainItems.Select(i=>i.ObjectId).SequenceEqual([1u]),"inventory filtered by the table");
        Feed(s,AddItem(3,701,1,"$701",Tail381));
        t.Check(s.FishObtainContains(700)&&!s.FishObtainContains(701),"S15 without flag adds nothing");
        Feed(s,AddItem(4,0x8001,1,"neg",Tail(11,8)));Feed(s,AddItem(4,0x8001,2,"neg",Tail(11,8)));
        t.Check(s.FishObtainIds.SequenceEqual([700,-32767,-32767]),"S15 flagged: sign-extended, not de-duplicated",s.FishObtainIds);
        t.Check(s.FishObtainContains(0x8001),"lookup uses the same sign extension");
        // 同 desc id、沒旗標的另一件物品也會列出（sub_E65050 只比 desc id）。
        Feed(s,AddItem(5,700,9,"$700 (9)",Tail381));
        t.Check(s.FishObtainItems.Select(i=>i.ObjectId).SequenceEqual([1u,4u,5u]),"list = every inventory item whose desc id is in the table");
        Feed(s,LoginPackets.Pad([57,..Le32(1)]));
        t.Check(s.FishObtainItems.Select(i=>i.ObjectId).SequenceEqual([4u,5u])&&s.FishObtainContains(700),"S57 removes the row, the table keeps its id");
        // 新的 S5 取代背包，但表不清（只在世界重設清）。
        Feed(s,List(Record(9,1,1,"x",Tail381)));
        t.Equal(3,s.FishObtainIds.Length,"replacement S5 does not clear the table");
        var json=WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("fishObtain");
        t.Equal(3,json.GetProperty("count").GetInt32(),"snapshot count");
        t.Check(FishObtainTable.Latest.Count==3,"Latest published on add");
        int rev=s.FishObtainRevision;
        s.Reset();
        t.Check(s.FishObtainIds.Length==0&&s.FishObtainRevision>rev&&s.FishObtainItems.Length==0,"world reset clears the table");
        t.Equal(0,FishObtainTable.Latest.Count,"Latest published on reset");
        t.Equal(0,FishObtainPanel.Filter(null,[1]).Count,"no inventory → empty list");
    }

    // ---------------------------------------------------------------- FishObtainUI 清單
    static UiSurface Surface(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    const string Row="""
              <CheckButton Name = "Item{0}_Button" Width ="186" Height="35" MouseEvent = "ItemsListClick" Activate = "0">
                <List Name = "Item_List" Width = "186" Height = "35" Horizontal = "1" Gap = "1">
                  <ItemIcon Name = "Item_Image" Width = "34" Height = "35"/>
                  <List Name = "ItemInfo_List" Y="2" Width="150" Height="35" Vertical ="1" Gap="1">
                    <Label Name="ItemName_Label" X = "4" Y = "4" Width = "140" Height = "15" Align="left"/>
                    <Label Name="ItemCount_Label" X = "130" Y = "5" Width = "19" Align="right" Height = "13" BackColor="0,0,0"/>
                  </List>
                </List>
              </CheckButton>
        """;
    // 參考客戶端 FishObtainUI.xml 的結構與座標（FU9§6.2、本任務核對 SHA-256 01d94c44…096a），surf 換成合成編號、只留 2 列。
    static string Xml=>$"""
        <?xml version="1.0"?>
        <FishObtainUI Toggle="true">
          <Window Name="UserObtain_Window" X="0" Y="0" BaseSurf = "900;1" FrameSurf="901;1" Moveable = "20" MouseEvent = "Dummy" KeyboardEvent="FishKeyboard">
            <Button Name = "CloseWindow_Button" X = "225" Y = "15" BaseSurf = "902" PushSurf = "903" MouseEvent = "CloseFishWindow"/>
            <Label Name="Total_Label" X = "30" Y="20" Height="13" Width="20" String="3674;1"/>
            <List Name = "ObtainNewItem_List" X = "18" Y = "37" Width = "194" Height = "250" Vertical = "1" Gap = "1">
        {string.Format(Row,0)}
        {string.Format(Row,1)}
            </List>
            <WheelControl Name="ItemsList_WheelArea" X = "18" Y = "37" Width = "194" Height = "250">
              <ScrollBar Name = "ItemsList_Scrollbar" X = "199" Y = "0" ScrollStart = "16" ScrollHeight = "224" Vertical = "1" MouseEvent="FishItemListScroll">
                <Image Name = "Background_Image" Surf = "904;1" X ="0" Y="14"/>
                <Button Name = "Up_Button" X = "0" Y ="0" BaseSurf = "905;1" PushSurf = "906;1" MouseEvent = "Dummy"/>
                <Button Name = "Down_Button" X = "0" Y = "236" BaseSurf = "907;1" PushSurf = "908;1" MouseEvent = "Dummy"/>
                <Button Name = "Move_Button" X = "0" Y = "16" BaseSurf = "909;1" PushSurf = "910;1" MouseEvent = "Dummy"/>
              </ScrollBar>
            </WheelControl>
          </Window>
        </FishObtainUI>
        """;
    const uint Base=0xFF203040,ThumbColor=0xFFFFFF00;
    static readonly Dictionary<int,UiSurface> Surfs=new(){
        [900]=Surface(900,240,300,Base),[901]=Surface(901,8,8,0xFF304050),[902]=Surface(902,10,10,0xFF00FF00),[903]=Surface(903,10,10,0xFF008800),
        [904]=Surface(904,12,200,0xFFFF00FF),[905]=Surface(905,12,14,0xFF0000FF),[906]=Surface(906,12,14,0xFF000088),
        [907]=Surface(907,12,14,0xFF00FFFF),[908]=Surface(908,12,14,0xFF008888),[909]=Surface(909,12,10,ThumbColor),[910]=Surface(910,12,10,0xFF888800)};
    static UiSurface? Get(int id)=>Surfs.TryGetValue(id,out var s)?s:null;
    static UiMouseEvent M(UiMouseKind k,int x,int y,int delta=0)=>new(k,new Point(x,y),MouseButtons.Left,1,delta,Keys.None);
    static void Click(IUiPanel p,int x,int y){var c=new UiNullCommands();p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
    static uint Pixel(IUiPanel p,int x,int y){using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();return (uint)canvas.Bitmap.GetPixel(x,y).ToArgb();}
    static IUiGameState State(IReadOnlyList<InventoryItem>? items)=>new UiItemWindowGameState(new UiStaticGameState(InWorld:true),new UiItemWindowData(items));
    static InventoryItem Item(uint id,ushort desc,uint count)=>new(id,desc,0,0,12,1,count,1,"$"+desc,"","0A00000000000000000000");

    static void Panel(WorldModuleTestContext t){
        IReadOnlyList<int> table=[];
        var p=new FishObtainPanel(null,null,()=>table);p.Load(UiLayout.Parse(Xml,"FishObtainUI.xml"),Get);
        t.Check(p.Available&&p.Missing.Count==0&&p.Skipped.Count==0,"synthetic full layout: usable, nothing missing or skipped",new{p.Unavailable,p.Missing,p.Skipped});
        t.Equal(2,p.Rows.Count,"rows from CheckButtons");
        t.Check(p.Rows[0] is {Row:{X:18,Y:37,Width:186,Height:35},Icon:{Width:34,Height:35}}&&p.Rows[1].Row.Y==37+35+1,"row geometry = UserObtainUI (Gap 1)",p.Rows);
        t.Check(p.Rows[0].Name==new Point(18+34+1+4,37+2+4)&&p.Rows[0].Count==new Rectangle(18+34+1+130,37+2+5,19,13),"name / count label positions");
        t.Check(p.Up is {At:{X:217,Y:37}}&&p.Down is {At:{X:217,Y:37+236}}&&p.Thumb is {At:{X:217,Y:37+16}}&&p.ScrollBackAt==new Point(217,37+14),"scrollbar inside WheelControl (18,37)+(199,0)");
        t.Check(p.ScrollStart==16&&p.ScrollHeight==224,"ScrollStart / ScrollHeight");

        var items=new[]{Item(1,700,5),Item(2,701,1),Item(3,700,2),Item(4,702,1),Item(5,700,1)};
        t.Check(p.Open(Rectangle.Empty)&&p.Visible,"opens");
        p.Update(State(items));
        t.Equal(0,p.Entries.Count,"empty table (3.81) → empty list");
        t.Equal(Base,Pixel(p,18+5,37+5),"empty list: row area shows the base only");
        t.Equal(ThumbColor,Pixel(p,217+2,37+16+2),"empty list: scrollbar thumb at the top");
        p.Update(State(null));
        t.Equal(0,p.Entries.Count,"no inventory yet → empty");

        table=[700,-32767];
        p.Update(State(items));
        t.Check(p.Entries.Select(i=>i.ObjectId).SequenceEqual([1u,3u,5u]),"table hit → listed in inventory order",p.Entries.Select(i=>i.ObjectId));
        t.Equal(1,p.MaxScroll,"3 entries, 2 rows → MaxScroll 1");
        Click(p,217+2,37+236+2);
        t.Equal(1,p.Scroll,"Down button scrolls");
        t.Equal(ThumbColor,Pixel(p,217+2,37+16+(224-10)+2),"thumb at the bottom when scrolled to the end");
        Click(p,217+2,37+236+2);
        t.Equal(1,p.Scroll,"clamped at MaxScroll");
        p.Mouse(M(UiMouseKind.Wheel,100,100,120),new UiNullCommands());
        t.Equal(0,p.Scroll,"wheel up scrolls back");
        Click(p,217+2,37+2);
        t.Equal(0,p.Scroll,"Up at the top stays");
        p.Update(State([..items,Item(6,0x8001,1)]));
        t.Equal(4,p.Entries.Count,"sign-extended entry matches desc 0x8001");
        p.Update(State([Item(1,700,0)]));
        t.Equal(0,p.Entries.Count,"count 0 not listed");

        table=[];p.Update(State(items));
        t.Check(p.Entries.Count==0&&p.Scroll==0,"table cleared → list empty, scroll clamped");
        p.Update(new UiStaticGameState(InWorld:false));
        t.Check(!p.Visible&&p.Entries.Count==0,"leaving the world closes and empties");

        // 產品路徑：WorldSession → FishObtainTable.Latest → 清單。
        var s=new WorldSession();s.Reset();
        Feed(s,List(Record(10,800,3,"$800 (3)",Tail(11,8)),Record(11,801,1,"$801",Tail381)));
        var q=new FishObtainPanel(null,null,()=>FishObtainTable.Latest);q.Load(UiLayout.Parse(Xml,"FishObtainUI.xml"),Get);q.Open(Rectangle.Empty);
        q.Update(State(s.Inventory));
        t.Check(q.Entries.Select(i=>i.ObjectId).SequenceEqual([10u]),"session table → panel list");
        s.Reset();q.Update(State(s.Inventory));
        t.Equal(0,q.Entries.Count,"world reset → empty list");
        var none=new FishObtainPanel();none.Load(UiLayout.Parse(Xml,"x.xml"),Get);none.Open(Rectangle.Empty);none.Update(State(items));
        t.Equal(0,none.Entries.Count,"no table source → empty");
    }
}
