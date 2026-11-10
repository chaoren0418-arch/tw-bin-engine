using System.Text;
using P06.NativeUi;
namespace P06;

// R7-E（T344／#807）離線測試：82 變體照原版累積、82 的按鈕附加處理（VA 0x736B59）、effect 470 的 tooltip 尾段（VA 0x90666D）。
// 依據 docs/reports/issue-807.md（sub_736AC0、sub_7362F0、sub_9061E0 尾段、sub_94E0E0 子碼 72 的靜態反組譯）。
// effectlist2 用合成表（id、icon 屬於測試自訂），不含原客戶端內容；不開遊戲、不連網路。
static class StatusIconFollowup2Tests {
    const uint Me=321;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Box(byte sub,byte[] fields)=>LoginPackets.Pad([250,sub,..fields]);
    static byte[] Leaves(byte value)=>Box(82,[value,0,0,0]);
    static byte[] NoneTime(byte on,uint effect)=>Box(180,[on,..Le32(effect),..Le32(0),..Le16(0)]);
    static string Icons(WorldSession s)=>string.Join(",",s.PacketBoxStatusIcons.Select(i=>$"{i.Effect}/{i.Value?.ToString()??"-"}{(i.Boost?$"/b{i.BoostText}":"")}"));
    static WorldSession Own(){
        var s=new WorldSession();long now=1_000_000;s.MagicStatusClock=()=>now;s.StartMessageEffects=()=>null;
        typeof(WorldSession).GetProperty("Status")!.SetValue(s,new OwnStatus(Me,1,0,10,11,12,13,14,15,100,100,100,100,0,0,0,50,0,0,0,0,0,0));
        typeof(WorldSession).GetProperty("MapId",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)!.SetValue(s,(ushort)4);
        return s;
    }
    static BuffEffectTable Table(){
        var x=new StringBuilder("<effectlist>");
        for(int i=0;i<540;i++)x.Append($"<effect id=\"{i}\" icon=\"{7000+i}\" offIcon=\"{8000+i}\" duration=\"-1\"/>");
        return BuffEffectTable.Parse(UiLayout.Parse(x.Append("</effectlist>").ToString(),"effectlist2.xml"));
    }
    static readonly BuffTooltipText Text=new("L","U","O","P","R","M","D","H","Q","V");

    [WorldModuleTest("status-icon-followup2-leaves")]
    static void LeavesAccumulate(WorldModuleTestContext t){
        var s=Own();
        s.Receive(Leaves(37),"");t.Equal("175/37",Icons(s),"first 82 → 175");
        s.Receive(NoneTime(1,293),"");t.Equal("175/37,518/37,293/-1",Icons(s),"293 → 518 added, 175 kept (sub_736AC0 does not remove)");
        s.Receive(Leaves(150),"");t.Equal("175/37,518/150,293/-1",Icons(s),"82 again → only the chosen variant 518 updated");
        s.Receive(Leaves(201),"");t.Equal("175/37,518/150,293/-1",Icons(s),"> 200 → 576 is past the table, nothing changes");
        t.Check(s.LeavesShownEffects.SequenceEqual([175,518]),"shown variants",s.LeavesShownEffects);
        s.Receive(NoneTime(0,293),"");t.Equal("175/37,518/150",Icons(s),"293 off → re-evaluated with the kept value 201 → 575 (past the table): 175 and 518 untouched");
    }

    [WorldModuleTest("status-icon-followup2-leaves-remove")]
    static void LeavesRemove(WorldModuleTestContext t){
        var s=Own();
        s.Receive(Leaves(20),"");s.Receive(NoneTime(1,293),"");s.Receive(NoneTime(0,293),"");
        t.Equal("175/20,518/20",Icons(s),"293 on/off → 175 updated, 518 stays");
        var snap=WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("packetBoxStatus");
        t.Equal(2,snap.GetProperty("leavesShown").GetArrayLength(),"snapshot leavesShown");
        t.Equal(2,snap.GetProperty("statusIcons").GetArrayLength(),"snapshot statusIcons has both variants");
        var hud=StatusIconFeed.Read(s);
        t.Check(hud.Select(h=>h.Id).SequenceEqual([-1_000_175,-1_000_518]),"WinForms HUD lists both variants with distinct ids",hud.Select(h=>h.Id));
        var b=BuffState.From(s,null,Text);
        t.Check(b.Entries.Count(e=>e.Effect is 175 or 518)==2,"native BuffState carries both variants",b.Entries);
        s.Receive(Leaves(0),"");t.Equal("",Icons(s),"0 removes every variant (175/518/574/575/576)");
        t.Equal(0,s.LeavesShownEffects.Length,"no variant left");
        // 還沒收過 82 時 293 的重算 → 值 0 → 只做移除。
        var fresh=Own();fresh.Receive(NoneTime(1,293),"");t.Equal("293/-1",Icons(fresh),"293 before any 82 adds no variant");
        s.Receive(Leaves(9),"");s.Reset();t.Equal(0,s.LeavesShownEffects.Length,"reset clears variants");
    }

    [WorldModuleTest("status-icon-followup2-boost")]
    static void Boost(WorldModuleTestContext t){
        t.Equal(4448,PacketBoxStatusRules.LeavesBoostText,"VA 0x736B9D push 0x1160");
        var s=Own();
        s.Receive(Leaves(30),"");t.Equal("175/30",Icons(s),"default (no ExpInfo.xml) → no boost flag, no text");
        t.Check(s.PacketBoxStatusIcons.Single() is {Boost:false,BoostText:null},"flag 0 and \"\" → null");
        bool zone=true;s.LeavesBoostZone=()=>zone;
        s.Receive(Leaves(31),"");t.Equal("175/31/b4448",Icons(s),"inside a BoostEvent zone → flag 1 and string 4448");
        var e=BuffState.From(s,null,Text).Entries.Single(x=>x.Effect==175);
        t.Check(e is {Boost:true,BoostText:4448},"BuffEntry carries the flag and text id",e);
        s.Receive(NoneTime(1,293),"");t.Equal("175/31/b4448,518/31/b4448,293/-1",Icons(s),"293 re-evaluation also runs the button step for its variant");
        zone=false;s.Receive(Leaves(32),"");t.Equal("175/31/b4448,518/32,293/-1",Icons(s),"only the chosen variant's button is updated (518 cleared, 175 untouched)");
        s.LeavesBoostZone=()=>throw new UiResourceException("x");
        s.Receive(Leaves(33),"");t.Equal("175/31/b4448,518/33,293/-1",Icons(s),"zone lookup failure → treated as outside");
        var snap=WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("packetBoxStatus").GetProperty("statusIcons");
        t.Check(snap[0].GetProperty("boost").GetBoolean()&&snap[0].GetProperty("boostText").GetInt32()==4448,"snapshot boost fields");
    }

    [WorldModuleTest("status-icon-followup2-vip470")]
    static void Vip470(WorldModuleTestContext t){
        t.Equal(470,BuffIconsPanel.Vip470Effect,"cmp [+0x20], 0x1D6");
        t.Equal(3399,BuffTooltipText.Vip470Id,"push 0xD47");
        t.Equal(10,BuffIconsPanel.TooltipColor,"palette index 10 at entry (imul edx, 4, 0xA)");
        t.Equal(3,BuffIconsPanel.Vip470TooltipColor,"palette index 3 in the 470 tail");
        t.Equal((ushort)0x5F7F,ChatPalette.Raw(10),"palette[10]");
        t.Equal((ushort)0x7C00,ChatPalette.Raw(3),"palette[3]");
        var ids=new List<int>();var all=BuffTooltipText.FromAll(id=>{ids.Add(id);return $"#{id}";});
        t.Equal("#3399",all.Vip470,"FromAll reads string 3399");
        t.Check(ids.Contains(3399)&&ids.Contains(930),"FromAll = From + 3399",ids);
        t.Equal((string?)null,BuffTooltipText.From(id=>"x").Vip470,"From alone keeps its id list");

        string? Tip(int effect,int mode,int? sec,int flag=0,bool off=false,BuffTooltipText? text=null)=>
            BuffIconsPanel.TooltipText(new BuffEntry(effect,sec,false,"t",DisplayMode:mode),off,text??Text,flag);
        foreach(int m in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13})t.Equal("V",Tip(470,m,30),$"470 mode {m}: tail replaces the text with 3399");
        t.Equal("V",Tip(470,0,30,off:true),"470 offIcon text also replaced");
        t.Equal((string?)null,Tip(470,0,30,flag:1),"vipFlag ≠ 0 → strcpy \"\" → no tooltip");
        t.Equal((string?)null,Tip(470,1,30,flag:5),"vipFlag 5 → no tooltip");
        t.Equal((string?)null,Tip(470,0,30,text:new("L","U","O")),"no string 3399 → no text (not invented)");
        t.Equal("L: 30 U",Tip(469,0,30),"other effects keep the case text");
        t.Equal("P: 30%",Tip(175,1,30,flag:1),"vipFlag only matters for 470");
        t.Equal(3,BuffIconsPanel.TooltipColorIndex(new BuffEntry(470,null,false,"t")),"470 colour 3");
        t.Equal(10,BuffIconsPanel.TooltipColorIndex(new BuffEntry(470,null,false,"t"),1),"470 with vipFlag → colour stays 10");
        t.Equal(10,BuffIconsPanel.TooltipColorIndex(new BuffEntry(175,null,false,"t")),"other effects colour 10");

        // 面板經 Placed 走同一條路；VipFlag 從 BuffState 帶入（3.81 不送 S64/72 → 預設 0）。
        var p=new BuffIconsPanel();p.Load(Table(),new BuffIconCatalog([]));
        p.Update(new UiBuffGameState(new UiStaticGameState(true),new BuffState([new BuffEntry(470,-1,false,"none-time:470")],[],Text)));
        t.Equal("V",p.TooltipText(p.Layout.Single()),"panel: 470 tooltip = string 3399");
        p.Update(new UiBuffGameState(new UiStaticGameState(true),new BuffState([new BuffEntry(470,-1,false,"none-time:470")],[],Text,VipFlag:2)));
        t.Equal((string?)null,p.TooltipText(p.Layout.Single()),"panel: VipFlag 2 → no tooltip");
        t.Equal(0,BuffState.Empty.VipFlag,"default VipFlag 0");
    }
}
