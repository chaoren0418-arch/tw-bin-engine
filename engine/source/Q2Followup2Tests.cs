using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using P06.NativeUi;
namespace P06;

// T290（#698）T272 補做的離線測試：門的 `]`、名稱旁圖示（FU5§1）、死亡統計 Reset 鈕與冷卻、S15 名稱＋第 6 參數備援、
// UserObtainUI 清單窗、mapDesc 超出範圍改用 desc-c.tbl 4266、座標聊天行照 1784。只用合成封包、合成字串表、合成 XML／圖；
// 不開遊戲、網路、視窗、伺服器、DB。只有本機有原客戶端時才多核對一次 desc-c.tbl 的 pin（CI 沒有客戶端會略過）。
static class Q2Followup2Tests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Own=123;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static object? Get(object o,string name)=>o.GetType().GetField(name,F)?.GetValue(o)??o.GetType().GetProperty(name,F)?.GetValue(o);
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static ServerStringTable Table(int count,params (int id,string text)[] rows){
        var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(count+"\r\n"+string.Join("\r\n",lines)));
    }

    // ---- 合成封包（欄位順序同 Q2FollowupTests：WorldSession.cs case 8、Rewards.cs S113、Inventory.cs ReadItem／S24／S57）----
    static byte[] S8(byte level,uint exp)=>Packet(b=>{
        b.Write((byte)8);b.Write(Own);b.Write(level);b.Write(exp);for(int i=0;i<6;i++)b.Write((byte)10);
        b.Write(100u);b.Write(100u);b.Write(10u);b.Write(10u);b.Write(0u);b.Write(0u);b.Write((byte)0);b.Write((byte)0);
        for(int i=0;i<5;i++)b.Write((short)0);b.Write(0u);
    });
    static byte[] S113(byte level,uint exp)=>Packet(b=>{b.Write((byte)113);b.Write(level);b.Write(exp);b.Write((byte)1);});
    static void Item(BinaryWriter b,uint id,uint count,string name,byte bless=1,ushort icon=80,ushort desc=1342){
        b.Write(id);b.Write(desc);b.Write((byte)0);b.Write((byte)0);b.Write(icon);b.Write(bless);b.Write(count);b.Write((byte)1);Text(b,name);
        b.Write((byte)0);b.Write(new byte[11]);
    }
    static void Adena(BinaryWriter b,uint id,uint count)=>Item(b,id,count,$"$4 ({count})",1,5,0);
    static byte[] S5(params Action<BinaryWriter>[] items)=>Packet(b=>{b.Write((byte)5);b.Write((byte)items.Length);foreach(var i in items)i(b);});
    static byte[] S15(Action<BinaryWriter> item)=>Packet(b=>{b.Write((byte)15);item(b);});
    static byte[] S24(uint id,string name,uint count)=>Packet(b=>{b.Write((byte)24);b.Write(id);Text(b,name);b.Write(count);b.Write((byte)0);});
    static byte[] S57(uint id)=>Packet(b=>{b.Write((byte)57);b.Write(id);});

    [WorldModuleTest("q2-followup-2")]
    static void Run(WorldModuleTestContext t){
        DoorSubEffects(t);NameIcons(t);ResetBaseline(t);NameFallback(t);StatsPanelReset(t);ObtainPanel(t);DescFallback(t);ChatLine(t);
    }

    // 1. 門的 `]`（gap-matrix 13.2 第 8 點、TWP§3.4）：門的播放實例＝開始時間；沒有播放中的動作時不觸發。
    static void DoorSubEffects(WorldModuleTestContext t){
        t.Equal(0L,Renderer.DoorPlaybackStamp(double.MaxValue,5000),"door without playback: stamp 0");
        t.Equal(4750L,Renderer.DoorPlaybackStamp(250,5000),"door playback stamp = now − elapsed");
        var r=(Renderer)RuntimeHelpers.GetUninitializedObject(typeof(Renderer));GC.SuppressFinalize(r);
        var session=new WorldSession();
        var open=new ActionTimeline([new(0,""),new(40,"]6327")],200,false);
        r.ObserveOwnerFrames(session,"o9",9,open,"open:0",double.MaxValue,32600,32900,0,false,1000);
        t.Equal(0,r.SubEffects.Live.Count,"door not playing (elapsed = MaxValue): no `]`");
        r.ObserveOwnerFrames(session,"o9",9,open,"open:"+Renderer.DoorPlaybackStamp(30,1030),30,32600,32900,0,false,1030);
        r.ObserveOwnerFrames(session,"o9",9,open,"open:"+Renderer.DoorPlaybackStamp(60,1060),60,32600,32900,0,false,1060);
        t.Check(r.SubEffects.Live.SingleOrDefault() is {Gfx:6327,X:32600,Y:32900,At:1040},"door open crossing `]6327` spawns one child at the door",r.SubEffects.Live);
        r.ObserveOwnerFrames(session,"o9",9,open,"open:"+Renderer.DoorPlaybackStamp(90,1090),90,32600,32900,0,false,1090);
        t.Equal(1,r.SubEffects.Live.Count,"same playback: no repeat");
        var close=new ActionTimeline([new(0,""),new(20,"]6327")],200,false);
        r.ObserveOwnerFrames(session,"o9",9,close,"close:"+Renderer.DoorPlaybackStamp(50,3050),50,32600,32900,0,false,3050);
        t.Equal(2,r.SubEffects.Live.Count,"a new open/close playback fires again");
    }

    // 2. 名稱旁圖示（FU5§1）：隊長 8264、隊員 2826、主人 773；+181 沒有 surf；等級差外框台版不畫。
    static void NameIcons(WorldModuleTestContext t){
        var name=new PointF(110,197);var emblem=new PointF(90,196);
        var member=NameLayout.HoverIcons(7,name,emblem,"測試",false,true,0,true);
        t.Check(member.Single(i=>i.Kind=="party") is {Surf:NameLayout.PartyMemberSurf}&&NameLayout.PartyMemberSurf==2826,"party member icon = surf 2826",member);
        t.Check(member.Single(i=>i.Kind=="master") is {Surf:773},"master icon = surf 773 (push 305h)",member);
        t.Check(member.Single(i=>i.Kind=="party").Position==new PointF(110+24+2,197)&&member.Single(i=>i.Kind=="master").Position==new PointF(110+24+7,197),"positions unchanged: +2 / +7");
        var leader=NameLayout.HoverIcons(7,name,emblem,"測試",false,true,0,false,true);
        t.Check(leader.Single() is {Kind:"party",Surf:8264},"party leader icon = surf 8264",leader);
        t.Check(NameLayout.HoverIcons(7,name,emblem,"測試",true,false,1,false).Single() is {Kind:"mark181-1",Surf:0},"+181 icon has no surf (3.81 never sends it)");
        t.Equal(509,NameLayout.MasterIconExcludedMap,"master icon excluded on map 509");
        t.Equal(3,NameLayout.TaiwanRegionCode,"GetACP 950 → byte_13CE3FE = 3");
        t.Equal((ushort?)null,NameLayout.LevelDiffOutline(NameLayout.TaiwanRegionCode,10,20),"Taiwan region code never draws the level outline");

        var w=new WorldSession();
        var members=(Dictionary<string,PartyMember>)Get(w,"partyMembers")!;
        members["隊長"]=new(5,"隊長",100);members["隊員"]=new(6,"隊員",100);Set(w,"partyLeader","隊長");
        t.Check(w.IsPartyLeaderId(5)&&!w.IsPartyLeaderId(6)&&!w.IsPartyLeaderId(0),"party leader is the member named by partyLeader");
        var targets=new[]{new NameTarget(5,"隊長",new(100,200,48,64),false),new NameTarget(6,"隊員",new(200,200,48,64),false)};
        t.Check(NameLayout.Plan(w,targets,5,false).Icons.SingleOrDefault() is {Kind:"party",Surf:8264},"hovering the leader: surf 8264");
        t.Check(NameLayout.Plan(w,targets,6,false).Icons.SingleOrDefault() is {Kind:"party",Surf:2826},"hovering a member: surf 2826");
        t.Equal(0,NameLayout.Plan(w,targets,null,false).Icons.Length,"no hover: no icons");
    }

    // 3. Reset（SS§3.1 sub_C0BA30）：清空新物品清單、重建經驗／金幣基準；基準清單不動。
    static void ResetBaseline(WorldModuleTestContext t){
        var w=new WorldSession();w.Receive(S8(1,0),"offline");
        w.Receive(S5(b=>Adena(b,1,1000),b=>Item(b,2,5,"$26 (5)")),"offline");
        w.Receive(S113(2,160),"offline");w.Receive(S15(b=>Item(b,2,8,"$26 (8)")),"offline");w.Receive(S24(1,"$4 (1500)",1500),"offline");
        var v=w.UserPlayStats();
        t.Check(v is {ExpGain:120m,AdenaGain:500,NewItems:2},"before reset: exp 120, gold 500, two new entries",v);
        t.Check(w.ResetUserPlayStatsBaseline(),"reset rebuilds a baseline");
        v=w.UserPlayStats();
        t.Check(v is {ExpGain:0m,AdenaGain:0,NewItems:0,Kills:0,Baseline:true}&&v.Obtained.Count==0,"after reset: everything 0, list empty",v);
        w.Receive(S113(2,195),"offline");t.Equal(20m,w.UserPlayStats().ExpGain,"exp measured from the reset point");
        w.Receive(S15(b=>Item(b,2,9,"$26 (9)")),"offline");
        t.Check(w.UserPlayStats().Obtained.SingleOrDefault() is {Id:2,Gain:4},"baseline list untouched by reset: gain still against the S5 count",w.UserPlayStats().Obtained);
        var empty=new WorldSession();
        t.Check(!empty.ResetUserPlayStatsBaseline()&&empty.UserPlayStats() is {ExpGain:null,Baseline:false},"nothing to rebuild: no invented baseline");
        empty.Receive(S8(3,0),"offline");t.Equal(0m,empty.UserPlayStats().ExpGain,"next S8 still records the baseline");
        using var c=new LoginConnection();
        t.Check(!c.ResetUserPlayStats(),"not in world: reset does nothing");
    }

    // 4. S15 名稱＋第 6 參數備援（FU3§4.2 sub_C038D0），以及清單列的名稱、圖示與順序。
    static void NameFallback(WorldModuleTestContext t){
        var w=new WorldSession();w.Receive(S8(1,0),"offline");
        w.Receive(S5(b=>Item(b,10,5,"$26 (5)",0x81),b=>Item(b,20,5,"$27 (5)",1)),"offline");
        w.Receive(S15(b=>Item(b,11,8,"$26 (8)",0x81)),"offline");
        t.Check(w.UserPlayStats().Obtained.SingleOrDefault() is {Id:11,Gain:3},"new id, same name + 6th parameter with 0x80 → baseline count 5 used (gain 3)",w.UserPlayStats().Obtained);
        w.Receive(S15(b=>Item(b,12,4,"$26 (4)",0x81)),"offline");
        t.Equal(1,w.UserPlayStats().NewItems,"name fallback: at or below the baseline count → not counted");
        w.Receive(S15(b=>Item(b,13,8,"$26 (8)",0x80)),"offline");
        t.Check(w.UserPlayStats().Obtained.Any(e=>e is {Id:13,Gain:8}),"different 6th parameter → no fallback, full count");
        w.Receive(S15(b=>Item(b,21,8,"$27 (8)",1)),"offline");
        t.Check(w.UserPlayStats().Obtained.Any(e=>e is {Id:21,Gain:8}),"flag 0x80 not set → no name fallback, full count");
        var list=w.UserPlayStats().Obtained;
        t.Equal("11,13,21",string.Join(",",list.Select(e=>e.Id)),"entries keep first-added order");
        t.Check(list[0] is {Name:"$26",Icon:80},"entry name without the trailing count, icon from the item",list[0]);
        w.Receive(S57(13),"offline");t.Equal("11,21",string.Join(",",w.UserPlayStats().Obtained.Select(e=>e.Id)),"S57 removes the entry");
        t.Equal("$26",UserPlayStatsRules.BareName("$26 (8)",8),"BareName strips \" (count)\"");
        t.Equal("$26 (8)",UserPlayStatsRules.BareName("$26 (8)",7),"BareName keeps text when the count differs");
    }

    // ---- 合成 XML（結構同 UserPlayUI.xml／UserObtainUI.xml，數字刻意不同）----
    const string StatsXml="""
<?xml version="1.0"?>
<UserPlayStatsUI Toggle="true">
  <Window Name="UserPlayStats_Window" X="0" Y="0" BaseSurf="9001;1" Moveable="20">
    <Button Name="Restart_Button" X="50" Y="30" BaseSurf="9011" MouseEvent="Restart"/>
    <Button Name="Quit_Button" X="50" Y="50" BaseSurf="9011" MouseEvent="Quit"/>
    <Button Name="Cancel_Button" X="50" Y="70" BaseSurf="9011" MouseEvent="Cancel"/>
    <List Name="UserPlayStats_List" X="20" Y="100" Width="140" Height="40" Vertical="1" Gap="0">
      <List Name="RowItem" Width="140" Height="14" Horizontal="1" Gap="3">
        <Label Name="GetItem_Label" Width="50" Height="12" String="3126"/>
        <Label Name="GetItemNumber_Label" Width="30" Height="12" Caption="0"/>
        <Button Name="NewItemList_Button" Y="0" Width="18" BaseSurf="9030" HighlightSurf="9031" PushSurf="9032;1" MouseEvent="OpenNewItemList"/>
      </List>
    </List>
    <Button Name="Reset_Button" X="40" Y="160" BaseSurf="9050" HighlightSurf="9051" PushSurf="9052;1" MouseEvent="Reset"/>
  </Window>
</UserPlayStatsUI>
""";
    const string ObtainXml="""
<?xml version="1.0"?>
<UserObtainUI Toggle="true">
  <Window Name="UserObtain_Window" X="0" Y="0" BaseSurf="9101;1" FrameSurf="9102;1" Moveable="20">
    <Button Name="CloseWindow_Button" X="200" Y="10" BaseSurf="9110" PushSurf="9111" MouseEvent="CloseWindow"/>
    <Label Name="Total_Label" X="30" Y="20" Height="13" Width="20" String="3126;1"/>
    <List Name="ObtainNewItem_List" X="10" Y="40" Width="180" Height="80" Vertical="1" Gap="1">
      <CheckButton Name="Item0_Button" Width="180" Height="30" MouseEvent="ItemsListClick" Activate="0">
        <List Name="Item_List" Width="180" Height="30" Horizontal="1" Gap="2">
          <ItemIcon Name="Item_Image" Width="32" Height="30"/>
          <List Name="ItemInfo_List" Y="2" Width="140" Height="30" Vertical="1" Gap="2">
            <Label Name="ItemName_Label" X="4" Y="4" Width="100" Height="15" Align="left" Caption="example"/>
            <Label Name="ItemCount_Label" X="110" Y="5" Width="20" Align="right" Height="13" BackColor="0,0,0"/>
          </List>
        </List>
      </CheckButton>
      <CheckButton Name="Item1_Button" Width="180" Height="30" MouseEvent="ItemsListClick" Activate="0">
        <List Name="Item_List" Width="180" Height="30" Horizontal="1" Gap="2">
          <ItemIcon Name="Item_Image" Width="32" Height="30"/>
          <List Name="ItemInfo_List" Y="2" Width="140" Height="30" Vertical="1" Gap="2">
            <Label Name="ItemName_Label" X="4" Y="4" Width="100" Height="15" Align="left" Caption="example"/>
            <Label Name="ItemCount_Label" X="110" Y="5" Width="20" Align="right" Height="13" BackColor="0,0,0"/>
          </List>
        </List>
      </CheckButton>
    </List>
    <WheelControl Name="ItemsList_WheelArea" X="10" Y="40" Width="180" Height="80">
      <ScrollBar Name="ItemsList_Scrollbar" X="185" Y="0" MaxValue="9" ChangeValue="1" ScrollStart="12" ScrollHeight="60" Vertical="1" MouseEvent="ItemListScroll">
        <Image Name="Background_Image" Surf="9120;1" X="0" Y="10"/>
        <Button Name="Up_Button" X="0" Y="0" BaseSurf="9121;1" PushSurf="9122;1" MouseEvent="Dummy"/>
        <Button Name="Down_Button" X="0" Y="70" BaseSurf="9123;1" PushSurf="9124;1" MouseEvent="Dummy"/>
        <Button Name="Move_Button" X="0" Y="12" BaseSurf="9125;1" PushSurf="9126;1" MouseEvent="Dummy"/>
      </ScrollBar>
    </WheelControl>
  </Window>
</UserObtainUI>
""";
    static UiSurface Surf(int id,int w,int h,uint argb)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(argb,w*h).ToArray()));
    static Func<int,UiSurface?> Surfaces=>id=>id switch{
        9001=>Surf(9001,200,200,0xFF102030),9011=>Surf(9011,60,16,0xFF404040),
        9030=>Surf(9030,18,12,0xFF00AA00),9031=>Surf(9031,18,12,0xFF00BB00),9032=>Surf(9032,18,12,0xFF00CC00),
        9050=>Surf(9050,40,14,0xFF0000AA),9051=>Surf(9051,40,14,0xFF0000BB),9052=>Surf(9052,40,14,0xFF0000CC),
        9101=>Surf(9101,220,130,0xFF203040),9102=>Surf(9102,220,130,0xFF203041),9110=>Surf(9110,12,12,0xFF500000),9111=>Surf(9111,12,12,0xFF600000),
        9120=>Surf(9120,10,50,0xFF001100),9121=>Surf(9121,10,10,0xFF002200),9122=>Surf(9122,10,10,0xFF003300),
        9123=>Surf(9123,10,10,0xFF004400),9124=>Surf(9124,10,10,0xFF005500),9125=>Surf(9125,10,8,0xFF006600),9126=>Surf(9126,10,8,0xFF007700),
        _=>null};
    static UiMouseEvent M(UiMouseKind kind,int x,int y,int delta=0)=>new(kind,new(x,y),kind is UiMouseKind.Move or UiMouseKind.Wheel?MouseButtons.None:MouseButtons.Left,1,delta,Keys.None);
    static void Click(IUiPanel p,int x,int y,IUiCommands c){p.Mouse(M(UiMouseKind.Move,x,y),c);p.Mouse(M(UiMouseKind.Down,x,y),c);p.Mouse(M(UiMouseKind.Up,x,y),c);}
    sealed class FakeStats:IUserPlayStatsSource {
        public UserPlayStatsView? Current{get;set;}
        public List<string> Calls{get;}=[];
        public void Restart()=>Calls.Add("restart");
        public void Quit()=>Calls.Add("quit");
        public void Dismiss()=>Calls.Add("dismiss");
        public void ShowMessage(string text)=>Calls.Add("message:"+text);
        public void Reset()=>Calls.Add("reset");
    }
    static UserPlayStatsValues Values(params UserObtainEntry[] items)=>new(0m,0,0,items.Length,true,null){Obtained=items};

    // 5. 原生統計窗的 Reset 鈕：可用時才動作、關清單窗、停用 1000 ms。
    static void StatsPanelReset(WorldModuleTestContext t){
        using var scope=ServerMessageText.UseTable(null);
        var p=new UserPlayStatsPanel();p.Load(UiLayout.Parse(StatsXml,"UserPlayUI.xml"),Surfaces);
        t.Check(p.Reset is {Spec.X:40,Spec.Y:160,Base.Surf:9050,Highlight.Surf:9051,Push.Surf:9052},"Reset_Button parsed from the window",p.Reset?.Spec);
        t.Check(p.Layout?.Reset is {MouseEvent:"Reset"},"layout keeps Reset_Button (optional node)");
        var src=new FakeStats{Current=new(true,Values(new UserObtainEntry(1,2,"$26",80)))};p.Source=src;var cmd=new UiNullCommands();
        p.Update(new UiStaticGameState(InWorld:true,Now:10_000));
        int rx=p.Origin.X+45,ry=p.Origin.Y+165;
        int nx=p.Origin.X+p.NewItemList!.Spec.X+5,ny=p.Origin.Y+p.NewItemList.Spec.Y+5;
        Click(p,nx,ny,cmd);t.Check(p.ObtainListOpen&&!src.Calls.Any(c=>c.StartsWith("message:",StringComparison.Ordinal)),"non-empty list: NewItemList opens the obtain window");
        Click(p,rx,ry,cmd);
        t.Check(src.Calls.Count(c=>c=="reset")==1&&!p.ObtainListOpen&&p.ResetReadyAt==11_000,"reset: one call, obtain window closed, disabled until +1000 ms",(src.Calls,p.ObtainListOpen,p.ResetReadyAt));
        p.Update(new UiStaticGameState(InWorld:true,Now:10_500));
        Click(p,rx,ry,cmd);t.Equal(1,src.Calls.Count(c=>c=="reset"),"during the 1000 ms cooldown the button does nothing");
        p.Mouse(M(UiMouseKind.Move,rx,ry),cmd);t.Equal(9050,p.StateSurface(p.Reset!)!.Surf,"cooling down: BaseSurf only (inferred)");
        p.Update(new UiStaticGameState(InWorld:true,Now:11_000));
        t.Equal(9051,p.StateSurface(p.Reset!)!.Surf,"ready again: hover shows HighlightSurf");
        Click(p,rx,ry,cmd);t.Equal(2,src.Calls.Count(c=>c=="reset"),"usable again after 1000 ms");
        Click(p,nx,ny,cmd);t.Check(p.ObtainListOpen,"open again");Click(p,nx,ny,cmd);t.Check(!p.ObtainListOpen,"pressing NewItemList again closes it (Toggle, inferred)");
        Click(p,nx,ny,cmd);src.Current=null;p.Update(new UiStaticGameState(InWorld:true,Now:12_000));
        t.Check(!p.ObtainListOpen,"stats window closed → obtain window closed");
        src.Current=new(true,Values());p.Update(new UiStaticGameState(InWorld:true,Now:12_100));
        Click(p,nx,ny,cmd);t.Check(!p.ObtainListOpen&&src.Calls.Last()=="message:沒有獲得的道具","empty list: 3127, no window");
    }

    // 6. UserObtainUI：統計窗旁擺放、標題、列、捲動、關閉。
    static void ObtainPanel(WorldModuleTestContext t){
        using var scope=ServerMessageText.UseTable(null);
        var stats=new UserPlayStatsPanel();stats.Load(UiLayout.Parse(StatsXml,"UserPlayUI.xml"),Surfaces);
        var src=new FakeStats();stats.Source=src;
        var obtain=new UserObtainPanel(()=>stats);obtain.Load(UiLayout.Parse(ObtainXml,"UserObtainUI.xml"),Surfaces);
        t.Check(UiPanelRegistry.Errors.Count==0&&UiPanelRegistry.Panels.Count(r=>r.Id==UserObtainPanel.PanelId&&r.Layout=="UserObtainUI")==1,"UserObtainUI registered once through [UiPanel]",UiPanelRegistry.Errors);
        t.Check(obtain.Rows.Count==2&&obtain.Rows[0].Row==new Rectangle(10,40,180,30)&&obtain.Rows[1].Row==new Rectangle(10,71,180,30),"rows: list origin, height + gap",obtain.Rows.Select(r=>r.Row));
        t.Check(obtain.Rows[0].Icon==new Rectangle(10,40,32,30)&&obtain.Rows[0].Name==new Point(10+32+2+4,40+2+4)&&obtain.Rows[0].Count==new Rectangle(10+32+2+110,40+2+5,20,13),"row: icon → gap → info list (name, count)",obtain.Rows[0]);
        t.Check(obtain.Up is {At.X:195,At.Y:40}&&obtain.Down is {At.X:195,At.Y:110}&&obtain.Thumb!=null,"scrollbar buttons relative to the wheel area");
        var items=new UserObtainEntry[]{new(1,3,"$26",80),new(2,1,"甲",0),new(3,7,"乙",0)};
        src.Current=new(true,Values(items));var state=new UiStaticGameState(InWorld:true,Now:1000);
        stats.Update(state);obtain.Update(state);
        t.Check(!obtain.Visible&&obtain.Bounds.IsEmpty,"closed until NewItemList opens it");
        var cmd=new UiNullCommands();
        Click(stats,stats.Origin.X+stats.NewItemList!.Spec.X+5,stats.Origin.Y+stats.NewItemList.Spec.Y+5,cmd);
        stats.Update(state);obtain.Update(state);
        t.Check(obtain.Visible&&obtain.Bounds==new Rectangle(stats.Bounds.Right,stats.Bounds.Top,220,130),"opened right of the stats window (fits within 800)",(obtain.Bounds,stats.Bounds));
        t.Equal(new Point(360,150),UserObtainPanel.Beside(new Rectangle(600,150,200,200),new Size(240,300)),"no room on the right → left side");
        t.Equal(new Point(560,150),UserObtainPanel.Beside(new Rectangle(360,150,200,200),new Size(240,300)),"exactly 800 still fits on the right");
        t.Equal("** 取得物品 **",obtain.Title,"title \"** %s **\" + string 3126 (fallback name without string-c.tbl)");
        using(ServerMessageText.UseTable(Table(3200,(3126,"獲得道具"))))t.Equal("** 獲得道具 **",obtain.Title,"title from string-c.tbl 3126");
        t.Check(obtain.Entries.Count==3&&obtain.MaxScroll==1&&obtain.Scroll==0,"three entries, two rows → one scroll step");
        int ox=obtain.Origin.X,oy=obtain.Origin.Y;
        Click(obtain,ox+195+2,oy+110+2,cmd);t.Equal(1,obtain.Scroll,"down button scrolls one row");
        Click(obtain,ox+195+2,oy+110+2,cmd);t.Equal(1,obtain.Scroll,"clamped at the last row");
        t.Equal(new Point(195,40+12+(60-8)),obtain.ThumbAt(),"thumb at the end of the track");
        obtain.Mouse(M(UiMouseKind.Wheel,ox+50,oy+60,120),cmd);t.Equal(0,obtain.Scroll,"wheel up scrolls back");
        using(var canvas=new UiCanvas()){
            canvas.Clear(Color.Black);canvas.Restrict(obtain.Bounds);obtain.Paint(canvas);canvas.Release();
            var px=canvas.Bitmap.GetPixel(ox+5,oy+128);t.Check(px.R==0x20&&px.G==0x30&&px.B==0x41,"base and frame surfaces painted",px);
            var bp=canvas.Bitmap.GetPixel(ox+196,oy+111);t.Check(bp.G==0x44,"down button painted",bp);
        }
        obtain.MoveTo(new(100,100));stats.Update(state);obtain.Update(state);t.Equal(new Point(100,100),obtain.Origin,"dragged position kept while open");
        Click(obtain,100+205,100+15,cmd);
        t.Check(!stats.ObtainListOpen,"close button clears ObtainListOpen");
        stats.Update(state);obtain.Update(state);t.Check(!obtain.Visible&&obtain.Bounds.IsEmpty,"closed");
        Click(stats,stats.Origin.X+stats.NewItemList.Spec.X+5,stats.Origin.Y+stats.NewItemList.Spec.Y+5,cmd);stats.Update(state);obtain.Update(state);
        t.Equal(new Point(stats.Bounds.Right,stats.Bounds.Top),obtain.Origin,"reopened beside the stats window again");
        t.Check(obtain.Key(new UiKeyEvent(Keys.Escape,true,false),cmd)&&!stats.ObtainListOpen,"Esc closes the obtain window (inferred)");
        Click(stats,stats.Origin.X+stats.NewItemList.Spec.X+5,stats.Origin.Y+stats.NewItemList.Spec.Y+5,cmd);
        ((IUiClosablePanel)obtain).Close();t.Check(!stats.ObtainListOpen,"stack eviction (IUiClosablePanel.Close) closes it");
        Click(stats,stats.Origin.X+stats.NewItemList.Spec.X+5,stats.Origin.Y+stats.NewItemList.Spec.Y+5,cmd);stats.Update(state);obtain.Update(state);
        Click(stats,stats.Origin.X+45,stats.Origin.Y+165,cmd);stats.Update(state);obtain.Update(state);
        t.Check(src.Calls.Contains("reset")&&!obtain.Visible,"reset closes the obtain window (sub_C0BA30)");
        t.Throws<UiLayoutException>(()=>new UserObtainPanel(()=>null).Load(UiLayout.Parse(ObtainXml.Replace("ObtainNewItem_List","Other_List"),"x.xml"),Surfaces),"missing ObtainNewItem_List rejected (no invented geometry)");
    }

    const string MapSystemXml="""
<Map_System_Configulation>
  <World_Map><Map id="1" mapDesc="193" mapNum="4"/><Map id="2" mapDesc="999" mapNum="5"/></World_Map>
</Map_System_Configulation>
""";
    static (byte[] Index,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return ([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }

    // 7. mapDesc 超出 MapDesc-c.tbl → desc-c.tbl 4266（SS§4.1 sub_A730A0）。
    static void DescFallback(WorldModuleTestContext t){
        t.Equal("desc-c.tbl",TextArchive.DescFileName,"desc table file name (sub_8CA220 \"desc\")");
        t.Equal(4266,LocationShareProtocol.OutOfRangeMapDescId,"out-of-range mapDesc uses record 4266");
        var invented=Encoding.ASCII.GetBytes("2\r\nA\r\nB");
        var (idx,pak)=Archive(("string-c.tbl",invented),("desc-c.tbl",invented));
        t.Throws<InvalidDataException>(()=>TextArchive.FromBytes(idx,pak).ReadDesc(),"invented desc-c.tbl fails the pin");
        var (idx2,pak2)=Archive(("string-c.tbl",invented));
        t.Throws<InvalidDataException>(()=>TextArchive.FromBytes(idx2,pak2).ReadDesc(),"missing desc-c.tbl rejected");
        var saved=(LocationShareProtocol.Regions,LocationShareProtocol.Strings,LocationShareProtocol.MapDesc,LocationShareProtocol.Desc);
        try{
            LocationShareProtocol.Regions=LocationRegionTable.Parse(MapSystemXml);LocationShareProtocol.MapDesc=Table(300,(193,"亞丁全大陸"));
            LocationShareProtocol.Desc=null;
            t.Equal("地區2",LocationShareProtocol.RegionLabel(2),"without desc-c.tbl: bare region number");
            LocationShareProtocol.Desc=Table(4300,(4266,"強化卷軸箱"),(999,"不該用到"));
            t.Equal("強化卷軸箱",LocationShareProtocol.RegionLabel(2),"mapDesc 999 ≥ MapDesc count → desc-c.tbl 4266 (not desc[999])");
            t.Equal("亞丁全大陸",LocationShareProtocol.RegionLabel(1),"in-range mapDesc still from MapDesc-c.tbl");
            LocationShareProtocol.Desc=Table(4300);
            t.Equal("地區2",LocationShareProtocol.RegionLabel(2),"empty 4266 → bare region number");
            LocationShareProtocol.Desc=Table(100);
            t.Equal("地區2",LocationShareProtocol.RegionLabel(2),"desc table shorter than 4266 → bare region number");
        }finally{(LocationShareProtocol.Regions,LocationShareProtocol.Strings,LocationShareProtocol.MapDesc,LocationShareProtocol.Desc)=saved;}
        // Local client only (CI has none).
        string root=GroundSpriteReader.ClientRoot;
        if(File.Exists(Path.Combine(root,"Text.idx"))&&File.Exists(Path.Combine(root,"Text.pak"))){
            var table=TextArchive.Open(root).ReadDesc();
            t.Check(table.Sha256==TextArchive.DescSha256&&table.Count>LocationShareProtocol.OutOfRangeMapDescId,"client desc-c.tbl pin and record count",table.Count);
            t.Check(ServerMessageText.DisplayText(table[LocationShareProtocol.OutOfRangeMapDescId]).Trim().Length>0,"client desc-c.tbl has text at 4266");
        }
    }

    static byte[] Share(string sender,ushort map,uint zone)=>Packet(b=>{b.Write((byte)250);b.Write((byte)111);Text(b,sender);b.Write(map);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(zone);});

    // 8. 座標聊天行：有 string-c.tbl 時整行照 1784（寄件者、地名）；沒有字串表時保留引擎標示格式。
    static void ChatLine(WorldModuleTestContext t){
        var saved=(LocationShareProtocol.Regions,LocationShareProtocol.Strings,LocationShareProtocol.MapDesc,LocationShareProtocol.Desc);
        try{
            LocationShareProtocol.Regions=LocationRegionTable.Parse(MapSystemXml);LocationShareProtocol.Strings=null;LocationShareProtocol.MapDesc=null;LocationShareProtocol.Desc=null;
            var w=new WorldSession();w.Receive(Share("甲",4,1),"offline");var line=w.PlayerChatLines[^1];
            t.Equal("[座標] 甲：地區1〔字串193〕〔訊息1784〕",PlayerChatProtocol.FormatLine(line),"no string table: labelled engine form kept");
            var view=new PlayerChatView(0,0,0,0,false,"",[line]);var text=PlayerChatProtocol.FormatLine(line);
            var links=LocationSharePresentation.Links(view,text);
            t.Check(links.Length==1&&text.Substring(links[0].Start,links[0].Length)==LocationShareProtocol.Format(line.Location!),"no table: link covers the placeholder part",links);
            LocationShareProtocol.Strings=Table(2200,(1784,"%s從 %s地區發送了座標位置情報。"));LocationShareProtocol.MapDesc=Table(300,(193,"亞丁全大陸"));
            text=PlayerChatProtocol.FormatLine(line);
            t.Equal("甲從 亞丁全大陸地區發送了座標位置情報。",text,"1784 exactly: sender as the first %s, region as the second");
            t.Equal(text,LocationShareProtocol.ChatLine("甲",line.Location!),"ChatLine = FormatLine for a share");
            links=LocationSharePresentation.Links(view,text);
            t.Check(links.Length==1&&links[0].Start==0&&links[0].Length==text.Length&&links[0].Location==line.Location,"link covers the whole 1784 line",links);
            var two=new PlayerChatView(0,0,0,0,false,"",[new PlayerChatLine(0,0,"一般訊息",PlayerChatChannel.Normal),line]);
            var joined="一般訊息\n"+text;var l2=LocationSharePresentation.Links(two,joined);
            t.Check(l2.Length==1&&l2[0].Start==5&&l2[0].Length==text.Length,"link offset after earlier lines",l2);
            w.Receive(Share("乙",4,2),"offline");
            t.Equal("乙從 地區2地區發送了座標位置情報。",PlayerChatProtocol.FormatLine(w.PlayerChatLines[^1]),"unknown region name: bare id inside 1784");
        }finally{(LocationShareProtocol.Regions,LocationShareProtocol.Strings,LocationShareProtocol.MapDesc,LocationShareProtocol.Desc)=saved;}
    }
}
