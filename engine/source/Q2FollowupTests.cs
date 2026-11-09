using System.Reflection;
using System.Text;
using P06.NativeUi;
namespace P06;

// T272（#649）Q-2 補做的離線測試：死亡統計值（基準相減、獲得道具規則、殺怪 0、地監時間）、開窗送 C63 C(9)、
// 發送座標的 1784／1785／2139 與 MapDesc-c.tbl 地名。只用合成封包、合成字串表、合成 XML／圖與假送出閘門；
// 不開遊戲、網路、視窗、伺服器、DB。只有本機有原客戶端時才多核對一次 MapDesc-c.tbl 的 pin 與筆數（CI 沒有客戶端會略過）。
// 王冠（R2-I）的圖源、錨點與隱身條件已在 #618 完成，測試在 ObjectDrawTests.Crowns。
static class Q2FollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Own=123;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static ServerStringTable Table(int count,params (int id,string text)[] rows){
        var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(count+"\r\n"+string.Join("\r\n",lines)));
    }

    // ---- 合成封包（欄位順序同 WorldSession.cs case 8、Rewards.cs S113、Inventory.cs ReadItem／S24／S57、PacketBoxStatus.cs 159）----
    static byte[] S8(byte level,uint exp)=>Packet(b=>{
        b.Write((byte)8);b.Write(Own);b.Write(level);b.Write(exp);for(int i=0;i<6;i++)b.Write((byte)10);
        b.Write(100u);b.Write(100u);b.Write(10u);b.Write(10u);b.Write(0u);b.Write(0u);b.Write((byte)0);b.Write((byte)0);
        for(int i=0;i<5;i++)b.Write((short)0);b.Write(0u);
    });
    static byte[] S113(byte level,uint exp)=>Packet(b=>{b.Write((byte)113);b.Write(level);b.Write(exp);b.Write((byte)1);});
    static void Item(BinaryWriter b,uint id,uint count,string name,ushort icon=80,ushort desc=1342){
        b.Write(id);b.Write(desc);b.Write((byte)0);b.Write((byte)0);b.Write(icon);b.Write((byte)1);b.Write(count);b.Write((byte)1);Text(b,name);
        b.Write((byte)0);b.Write(new byte[11]);
    }
    static void Adena(BinaryWriter b,uint id,uint count)=>Item(b,id,count,$"$4 ({count})",5,0);
    static byte[] S5(params Action<BinaryWriter>[] items)=>Packet(b=>{b.Write((byte)5);b.Write((byte)items.Length);foreach(var i in items)i(b);});
    static byte[] S15(Action<BinaryWriter> item)=>Packet(b=>{b.Write((byte)15);item(b);});
    static byte[] S24(uint id,string name,uint count)=>Packet(b=>{b.Write((byte)24);b.Write(id);Text(b,name);b.Write(count);b.Write((byte)0);});
    static byte[] S57(uint id)=>Packet(b=>{b.Write((byte)57);b.Write(id);});
    static byte[] MapTimer(params (uint order,string name,uint minutes)[] maps)=>Packet(b=>{
        b.Write((byte)250);b.Write((byte)159);b.Write((uint)maps.Length);foreach(var (o,n,m) in maps){b.Write(o);Text(b,n);b.Write(m);}
    });

    [WorldModuleTest("q2-followup")]
    static void Run(WorldModuleTestContext t){
        Registration(t);Baseline(t);Obtain(t);Format(t);DungeonRequest(t);Panel(t);Location(t);MapDescTable(t);
    }

    static void Registration(WorldModuleTestContext t){
        var e=WorldExtensionRegistry.Default.Entries;
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);
        foreach(var (op,method) in new[]{(8,"ObserveStatsBaseline"),(5,"ObserveObtainBaseline"),(15,"ObserveObtainAdd"),(24,"ObserveObtainChange"),(57,"ObserveObtainRemove")})
            t.Check(e.Any(x=>x.Kind=="observe"&&x.Opcode==op&&x.Method=="WorldSession."+method),$"S{op} observer {method} registered through M7-A");
        t.Check(e.Any(x=>x.Kind=="reset"&&x.Method=="WorldSession.ResetUserPlayStats"),"[WorldReset] ResetUserPlayStats registered");
        t.Check(e.Any(x=>x.Kind=="snapshot"&&x.Key=="userPlayStats"),"userPlayStats snapshot registered");
        t.Equal("3F09000000000000",Convert.ToHexString(UserPlayStatsRules.DungeonTimeRequest()),"C63 C(9) request = opcode 63, subtype 9 (SS2§14)");
    }

    // 經驗：(等級×100＋經驗%) 差；只用進入世界後第一個 S8；Reset 後重記。
    static void Baseline(WorldModuleTestContext t){
        var w=new WorldSession();
        t.Check(w.UserPlayStats() is {ExpGain:null,AdenaGain:null,Baseline:false,Kills:0},"no baseline before S8/S5");
        w.Receive(S8(1,0),"offline");
        t.Equal(0m,w.UserPlayStats().ExpGain,"first S8 = baseline (level 1, 0%)");
        w.Receive(S113(2,160),"offline");   // level 2: 125–300 → (160−125)/175 = 20%
        t.Equal(120m,w.UserPlayStats().ExpGain,"S113 level 2 at 20%: (2×100+20) − (1×100+0) = 120");
        w.Receive(S8(2,160),"offline");
        t.Equal(120m,w.UserPlayStats().ExpGain,"later S8 does not move the baseline");
        t.Equal("120.0000%",UserPlayStatsText.Value(w.UserPlayStats(),UserPlayStatsText.ExpRow),"exp row \"%.4f%%\"");
        t.Equal("0",UserPlayStatsText.Value(w.UserPlayStats(),UserPlayStatsText.KillRow),"kill row always 0 (3.81 never sends S250/61)");
        w.Reset();
        t.Check(w.UserPlayStats() is {ExpGain:null},"Reset clears the baseline");
        w.Receive(S8(2,160),"offline");t.Equal(0m,w.UserPlayStats().ExpGain,"first S8 after reset is the new baseline");
        w.Receive(S113(2,195),"offline");t.Equal(20m,w.UserPlayStats().ExpGain,"same level: percent difference only");
        t.Equal("20.0000%",UserPlayStatsText.Value(w.UserPlayStats(),UserPlayStatsText.ExpRow),"four decimals");
        // Level outside the verified table → percent unknown → no number (no synthesized value).
        var x=new WorldSession();x.Receive(S8(1,0),"offline");x.Receive(S113(250,5),"offline");
        t.Equal(null,UserPlayStatsText.Value(x.UserPlayStats(),UserPlayStatsText.ExpRow),"unknown percent → placeholder");
    }

    // 獲得道具（FU3§4.2）與金幣（3.81 金幣是背包物品）。
    static void Obtain(WorldModuleTestContext t){
        var w=new WorldSession();w.Receive(S8(1,0),"offline");
        w.Receive(S5(b=>Adena(b,1,1000),b=>Item(b,2,5,"$26 (5)")),"offline");
        var v=w.UserPlayStats();
        t.Check(v is {Baseline:true,NewItems:0,AdenaGain:0},"S5 builds the baseline (items and gold)",v);
        w.Receive(S15(b=>Item(b,2,8,"$26 (8)")),"offline");t.Equal(1,w.UserPlayStats().NewItems,"S15 above baseline counts (difference)");
        w.Receive(S15(b=>Item(b,3,1,"$238")),"offline");t.Equal(2,w.UserPlayStats().NewItems,"S15 of an id not in the baseline counts");
        w.Receive(S15(b=>Item(b,2,4,"$26 (4)")),"offline");t.Equal(2,w.UserPlayStats().NewItems,"S15 at or below baseline: not counted, list unchanged (sub_C038D0)");
        w.Receive(S24(2,"$26 (5)",5),"offline");t.Equal(1,w.UserPlayStats().NewItems,"S24 back to baseline removes the entry");
        w.Receive(S24(1,"$4 (1500)",1500),"offline");
        v=w.UserPlayStats();t.Check(v.NewItems==2&&v.AdenaGain==500,"S24 above baseline on a baseline-only id adds it; gold gain 1500 − 1000",v);
        t.Equal("500",UserPlayStatsText.Value(v,UserPlayStatsText.AdenaRow),"adena row");
        t.Equal("2",UserPlayStatsText.Value(v,UserPlayStatsText.ItemRow),"item row = new-list entries, not quantities");
        w.Receive(S24(9,"x",3),"offline");t.Equal(2,w.UserPlayStats().NewItems,"S24 of an id in neither list: nothing");
        w.Receive(S57(3),"offline");t.Equal(1,w.UserPlayStats().NewItems,"S57 removes from the new list");
        w.Receive(S24(1,"$4 (1200000)",1200000),"offline");
        t.Equal("1,199,000",UserPlayStatsText.Value(w.UserPlayStats(),UserPlayStatsText.AdenaRow),"\"%d\" + sub_88EA30 (inferred thousands separator)");
        w.Receive(S24(1,"$4 (10)",10),"offline");
        t.Check(w.UserPlayStats() is {AdenaGain:-990,NewItems:0},"gold below baseline is negative; adena entry removed",w.UserPlayStats());
        t.Throws<ProtocolException>(()=>w.Receive(S57(3)[..4],"offline"),"malformed packet still rejected by the core handler");
        t.Equal(0,w.UserPlayStats().NewItems,"malformed packet changes nothing");
        w.Reset();t.Check(w.UserPlayStats() is {Baseline:false,NewItems:0,AdenaGain:null},"Reset clears inventory baseline and new list");
    }

    static void Format(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){
            t.Equal("120分",UserPlayStatsText.DungeonTime(120),"minutes + 1587 fallback");
            t.Equal("9999999分",UserPlayStatsText.DungeonTime(9_999_999),"below 10,000,000 still minutes");
            t.Equal("無限",UserPlayStatsText.DungeonTime(10_000_000),"≥ 10,000,000 → 3344 fallback");
            t.Equal("沒有獲得的道具",UserPlayStatsText.Text(UserPlayStatsText.NoItemsMessage),"3127 fallback");
            t.Equal("查看道具 變動詳細內容",UserPlayStatsText.Text(UserPlayStatsText.NewItemTooltip),"3131 fallback");
        }
        using(ServerMessageText.UseTable(Table(3400,(1587,"分X"),(3344,"\\f1無X"),(3127,"表3127"),(3131,"表3131")))){
            t.Equal("7分X",UserPlayStatsText.DungeonTime(7),"1587 from string-c.tbl");
            t.Equal("無X",UserPlayStatsText.DungeonTime(20_000_000),"3344 from string-c.tbl, colour control removed");
            t.Check(UserPlayStatsText.Text(3127)=="表3127"&&UserPlayStatsText.Text(3131)=="表3131","3127 / 3131 from string-c.tbl");
        }
        t.Equal(null,UserPlayStatsText.Value(null,UserPlayStatsText.KillRow),"no values → placeholder");
        t.Equal(null,UserPlayStatsText.Value(new(null,null,0,0,false,null),UserPlayStatsText.ItemRow),"no inventory baseline → item placeholder");
        t.Equal(null,UserPlayStatsText.Value(new(1m,1,0,0,true,null),3999),"unknown row id → placeholder");
    }

    static void DungeonRequest(WorldModuleTestContext t){
        var sent=new List<byte[]>();
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        using var c=new LoginConnection();
        t.Check(!c.SyncUserPlayStatsWindow(true).GetAwaiter().GetResult()&&sent.Count==0,"not in world → nothing sent");
        t.Equal(null,c.UserPlayStatsSnapshot(),"not in world → no values");
        Set(c,"State",LoginState.InWorld);Set(c,"sendMovementGate",sender);
        t.Check(!c.SyncUserPlayStatsWindow(true).GetAwaiter().GetResult()&&sent.Count==0,"window already counted as shown: no repeat on the same opening");
        c.SyncUserPlayStatsWindow(false).GetAwaiter().GetResult();
        t.Check(c.SyncUserPlayStatsWindow(true).GetAwaiter().GetResult(),"closed → open sends");
        t.Equal("3F09000000000000",Convert.ToHexString(sent.Single()),"one C63 C(9)");
        t.Check(!c.SyncUserPlayStatsWindow(true).GetAwaiter().GetResult()&&sent.Count==1,"staying open sends nothing more");
        c.SyncUserPlayStatsWindow(false).GetAwaiter().GetResult();c.SyncUserPlayStatsWindow(true).GetAwaiter().GetResult();
        t.Equal(2,sent.Count,"each new opening sends once (vtable sub_C0AED0)");
        t.Check(sent.All(p=>p[0]==63),"never sends op 55/41 or 55/44 (3.81 ignores them)");
        var w=c.World;w.Receive(MapTimer((1,"古魯丁地監",120),(2,"象牙塔",10_000_000)),"offline");
        var v=c.UserPlayStatsSnapshot();
        t.Check(v?.DungeonTimes is {Count:2} d&&d[0].MapName=="古魯丁地監"&&d[1].Minutes==10_000_000,"S250/159 reply reaches the stats values",v);
    }

    // Same schema as UserPlayUI.xml (NewItemList_Button, DungeonTime_*), deliberately different numbers.
    const string Xml="""
<?xml version="1.0"?>
<UserPlayStatsUI Toggle="true">
  <Window Name="UserPlayStats_Window" X="0" Y="0" BaseSurf="9001;1" Moveable="20">
    <Button Name="Restart_Button" X="50" Y="30" BaseSurf="9011" MouseEvent="Restart"/>
    <Button Name="Quit_Button" X="50" Y="50" BaseSurf="9011" MouseEvent="Quit"/>
    <Button Name="Cancel_Button" X="50" Y="70" BaseSurf="9011" MouseEvent="Cancel"/>
    <List Name="UserPlayStats_List" X="20" Y="100" Width="140" Height="40" Vertical="1" Gap="0">
      <List Name="RowExp" Width="140" Height="14" Horizontal="1" Gap="3">
        <Image Name="Icon_Image" Surf="9020;1" Y="2"/>
        <Label Name="Exp_Label" Width="50" Height="12" String="3123"/>
        <Label Name="ExpNumber_Label" Width="40" Height="12" Caption="0.0%"/>
      </List>
      <List Name="RowItem" Width="140" Height="14" Horizontal="1" Gap="3">
        <Image Name="Icon_Image" Surf="9020;1" Y="2"/>
        <Label Name="GetItem_Label" Width="50" Height="12" String="3126"/>
        <Label Name="GetItemNumber_Label" Width="30" Height="12" Caption="0"/>
        <Button Name="NewItemList_Button" Y="-3" Width="18" BaseSurf="9030" HighlightSurf="9031" PushSurf="9032;1" MouseEvent="OpenNewItemList">
          <Tooltip Name="Desc_Tooltip" X="-100" Y="15" String="3131" Area="1"/>
        </Button>
      </List>
    </List>
    <Image Name="DungeonTime_Image" Surf="9040;1" X="21" Y="140"/>
    <List Name="DungeonTime_List" X="20" Y="150" Width="140" Height="30" Vertical="1" Gap="0">
      <List Name="Sub1" Width="140" Height="15" Horizontal="1" Gap="2">
        <Image Name="Icon_Image" Surf="9041;1" Y="1"/>
        <Label Name="DungeonName_Label" Width="60" Height="13" Caption="G" FontColor="1,2,3"/>
        <Label Name="DungeonTime_Label" Width="50" Height="13" Caption="0" FontColor="4,5,6"/>
      </List>
      <List Name="Sub2" Width="140" Height="15" Horizontal="1" Gap="2">
        <Label Name="DungeonName_Label" Width="60" Height="13" Caption=""/>
        <Label Name="DungeonTime_Label" Width="50" Height="13" Caption=""/>
      </List>
    </List>
  </Window>
</UserPlayStatsUI>
""";
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Func<int,UiSurface?> Surfaces=>id=>id switch{
        9001=>Surf(9001,200,200,0xFF102030),9011=>Surf(9011,60,16,0xFF404040),9020=>Surf(9020,6,6,0xFFAA0000),
        9030=>Surf(9030,18,16,0xFF00AA00),9031=>Surf(9031,18,16,0xFF00BB00),9032=>Surf(9032,18,16,0xFF00CC00),
        9040=>Surf(9040,8,8,0xFF0000DD),9041=>Surf(9041,5,5,0xFF0000EE),_=>null};
    static UiMouseEvent M(UiMouseKind kind,int x,int y)=>new(kind,new(x,y),kind==UiMouseKind.Move?MouseButtons.None:MouseButtons.Left,1,0,Keys.None);
    sealed class FakeStats:IUserPlayStatsSource {
        public UserPlayStatsView? Current{get;set;}
        public List<string> Calls{get;}=[];
        public void Restart()=>Calls.Add("restart");
        public void Quit()=>Calls.Add("quit");
        public void Dismiss()=>Calls.Add("dismiss");
        public void ShowMessage(string text)=>Calls.Add("message:"+text);
    }

    static void Panel(WorldModuleTestContext t){
        using var scope=ServerMessageText.UseTable(null);
        var p=new UserPlayStatsPanel();p.Load(UiLayout.Parse(Xml,"UserPlayUI.xml"),Surfaces);
        t.Equal(new Point(300,150),p.Origin,"origin centred");
        // RowItem (index 1): x = 20 + 6 + 3 + 50 + 3 + 30 + 3 = 115; y = 100 + 14 − 3 = 111.
        t.Check(p.NewItemList is {Spec.X:115,Spec.Y:111,Base.Surf:9030,Highlight.Surf:9031,Push.Surf:9032},"NewItemList after the value column, row gap, Y −3",p.NewItemList?.Spec);
        t.Equal(new UserPlayStatsPanel.Tooltip(-100,15,3131),p.NewItemTooltip,"tooltip 3131 relative to the button");
        t.Check(p.DungeonImage?.Surf==9040&&p.DungeonImageAt==new Point(21,140)&&p.DungeonRows.Count==2,"DungeonTime image and two rows");
        var (icon,name,time)=p.DungeonRowRects(0);
        t.Check(icon==new Point(320,301)&&name==new Rectangle(327,300,60,15)&&time==new Rectangle(389,300,50,15),"dungeon row: icon → gap → name → gap → time",(icon,name,time));
        t.Equal(new Rectangle(320,315,60,15),p.DungeonRowRects(1).Name,"second dungeon row one row lower, no icon");

        var src=new FakeStats();p.Source=src;var cmd=new UiNullCommands();
        src.Current=new(true);p.Update(new UiStaticGameState(InWorld:true));
        t.Check(p.ValueText(0)==UserPlayStatsPanel.ValuePlaceholder&&p.ValueText(1)==UserPlayStatsPanel.ValuePlaceholder,"no values → placeholders");
        t.Equal(("G","0"),p.DungeonText(0),"before S250/159: XML default captions");
        src.Current=new(true,new(12.5m,300,0,0,true,[new(1,"古魯丁地監",120)]));p.Update(new UiStaticGameState(InWorld:true));
        t.Check(p.ValueText(0)=="12.5000%"&&p.ValueText(1)=="0","row values from UserPlayStatsValues",(p.ValueText(0),p.ValueText(1)));
        t.Equal(("古魯丁地監","120分"),p.DungeonText(0),"S250/159 entry: name and minutes + 1587");
        t.Equal(("",""),p.DungeonText(1),"rows past the list are blank (inferred)");
        int bx=300+115+5,by=150+111+5;
        p.Mouse(M(UiMouseKind.Move,bx,by),cmd);t.Check(p.Hover==p.NewItemList&&p.StateSurface(p.NewItemList!)!.Surf==9031,"hover → HighlightSurf (tooltip shown)");
        p.Mouse(M(UiMouseKind.Down,bx,by),cmd);t.Equal(9032,p.StateSurface(p.NewItemList!)!.Surf,"pressed → PushSurf");
        p.Mouse(M(UiMouseKind.Up,bx,by),cmd);t.Equal("message:沒有獲得的道具",src.Calls.Last(),"empty new list → system message 3127 (sub_C0B470)");
        src.Current=new(true,new(0m,0,0,2,true,null));p.Update(new UiStaticGameState(InWorld:true));int calls=src.Calls.Count;
        p.Mouse(M(UiMouseKind.Down,bx,by),cmd);p.Mouse(M(UiMouseKind.Up,bx,by),cmd);
        t.Equal(calls,src.Calls.Count,"non-empty list: no message (UserObtainUI list window not implemented)");
        p.Mouse(M(UiMouseKind.Move,0,0),cmd);   // hover off: no tooltip text over the checked pixels
        using var canvas=new UiCanvas();canvas.Clear(Color.Black);canvas.Restrict(p.Bounds);p.Paint(canvas);canvas.Release();
        var px=canvas.Bitmap.GetPixel(300+21+1,150+140+1);t.Check(px.B==0xDD&&px.R==0,"DungeonTime_Image painted",px);
        var ip=canvas.Bitmap.GetPixel(320+1,301+1);t.Check(ip.B==0xEE&&ip.R==0,"dungeon row icon painted",ip);
        // The existing schema without the optional nodes still loads (UserPlayStatsTests StatsXml).
        var bare=new UserPlayStatsPanel();bare.Load(UiLayout.Parse(Xml.Replace("NewItemList_Button","Other_Button").Replace("DungeonTime_List","Other_List").Replace("DungeonTime_Image","Other_Image"),"x.xml"),Surfaces);
        t.Check(bare.NewItemList==null&&bare.DungeonRows.Count==0&&bare.DungeonImage==null,"optional nodes absent → not drawn");
    }

    const string MapSystemXml="""
<Map_System_Configulation>
  <World_Map><Map id="1" mapDesc="193" mapNum="4"/><Map id="2" mapDesc="999" mapNum="5"/></World_Map>
  <Village_Map><Map id="103" mapDesc="167" mapNum="4"/></Village_Map>
</Map_System_Configulation>
""";
    static byte[] Share(string sender,ushort map,uint zone)=>Packet(b=>{b.Write((byte)250);b.Write((byte)111);Text(b,sender);b.Write(map);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(zone);});

    static void Location(WorldModuleTestContext t){
        var (savedRegions,savedStrings,savedDesc)=(LocationShareProtocol.Regions,LocationShareProtocol.Strings,LocationShareProtocol.MapDesc);
        try{
            LocationShareProtocol.Regions=LocationRegionTable.Parse(MapSystemXml);LocationShareProtocol.Strings=null;LocationShareProtocol.MapDesc=null;
            t.Equal("地區1〔字串193〕〔訊息1784〕",LocationShareProtocol.Format(new(4,1,2,1)),"without tables: numbered placeholders kept");
            t.Equal("〔訊息2139〕",LocationShareProtocol.ListUnavailableText,"2139 placeholder without table");
            t.Check(LocationShareProtocol.PromptText.StartsWith("〔訊息1785〕",StringComparison.Ordinal),"1785 placeholder without table");
            // string-c.tbl 167 is an unrelated message (SS§4.1): it must never be used as a region name.
            LocationShareProtocol.Strings=Table(2200,(1784,"%s從 %s地區發送了座標位置情報。"),(1785,"請輸入要接受的角色名稱。"),(2139,"該區域的位置無法進行傳送。"),(167,"\\f1不相干訊息"),(193,"不相干193"));
            t.Equal("地區1〔字串193〕",LocationShareProtocol.RegionLabel(1),"string-c.tbl alone never names a region");
            LocationShareProtocol.MapDesc=Table(300,(193,"亞丁全大陸"),(167,"奇岩村莊"));
            t.Equal("亞丁全大陸",LocationShareProtocol.RegionLabel(1),"world region name from MapDesc-c.tbl");
            t.Equal("奇岩村莊",LocationShareProtocol.RegionLabel(103),"village region (id ≥ 100) name from MapDesc-c.tbl");
            t.Equal("地區2",LocationShareProtocol.RegionLabel(2),"mapDesc past MapDesc-c.tbl → bare id (desc-c.tbl 4266 not loaded)");
            t.Equal("地區77",LocationShareProtocol.RegionLabel(77),"unknown region → bare id");
            t.Equal("從 亞丁全大陸地區發送了座標位置情報。",LocationShareProtocol.Format(new(4,1,2,1)),"1784 with the region as the second %s; sender comes from the chat prefix");
            t.Equal("甲從 乙地區%",LocationShareProtocol.FormatC("%s從 %s地區%%","甲","乙"),"C-style %s fill, %% → %");
            t.Equal("從 地區",LocationShareProtocol.FormatC("%s從 %s地區"),"missing arguments → empty");
            t.Equal("該區域的位置無法進行傳送。",LocationShareProtocol.ListUnavailableText,"2139 from string-c.tbl");
            try{LocationShareProtocol.Encode("",new(0,0,0,0));t.Check(false,"empty recipient rejected");}
            catch(ArgumentException ex){t.Check(ex.Message.StartsWith("請輸入要接受的角色名稱。",StringComparison.Ordinal),"1785 prompt from string-c.tbl",ex.Message);}
            var w=new WorldSession();w.Receive(Share("甲",4,1),"offline");
            var line=w.PlayerChatLines[^1];
            t.Equal("[座標] 甲：從 亞丁全大陸地區發送了座標位置情報。",PlayerChatProtocol.FormatLine(line),"received chat line");
            var view=new PlayerChatView(0,0,0,0,false,"",[line]);
            var links=LocationSharePresentation.Links(view,PlayerChatProtocol.FormatLine(line));
            t.Check(links.Length==1&&PlayerChatProtocol.FormatLine(line).Substring(links[0].Start,links[0].Length)==LocationShareProtocol.Format(line.Location!),"link still covers the 1784 text",links);
            w.LocationListObjectPresent=false;w.Receive(Share("乙",4,1),"offline");
            t.Check(w.PlayerChatLines[^1].Text=="該區域的位置無法進行傳送。"&&w.PlayerChatLines[^1].Location==null,"+16 = 0 shows 2139 text from the table");
        }finally{LocationShareProtocol.Regions=savedRegions;LocationShareProtocol.Strings=savedStrings;LocationShareProtocol.MapDesc=savedDesc;}
    }

    static (byte[] Index,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return ([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }

    static void MapDescTable(WorldModuleTestContext t){
        t.Equal("MapDesc-c.tbl",TextArchive.MapDescFileName,"CMapSystemXML loads MapDesc-c.tbl (sub_A61D30 this+0x48)");
        var invented=Encoding.ASCII.GetBytes("2\r\nA\r\nB");
        var (idx,pak)=Archive(("string-c.tbl",invented),("mapdesc-c.tbl",invented));
        t.Throws<InvalidDataException>(()=>TextArchive.FromBytes(idx,pak).ReadMapDesc(),"invented MapDesc-c.tbl fails the pin (no substitute table)");
        var (idx2,pak2)=Archive(("string-c.tbl",invented));
        t.Throws<InvalidDataException>(()=>TextArchive.FromBytes(idx2,pak2).ReadMapDesc(),"missing MapDesc-c.tbl rejected");
        // Local client only (CI has none): pinned table opens and has the 375 records SS§4.1 lists.
        string root=GroundSpriteReader.ClientRoot;
        if(File.Exists(Path.Combine(root,"Text.idx"))&&File.Exists(Path.Combine(root,"Text.pak"))){
            var table=TextArchive.Open(root).ReadMapDesc();
            t.Check(table.Count==375&&table.Sha256==TextArchive.MapDescSha256,"client MapDesc-c.tbl pin and record count",table.Count);
            t.Check(ServerMessageText.DisplayText(table[193]).Trim().Length>0,"client MapDesc-c.tbl has a name at 193");
        }
    }
}
