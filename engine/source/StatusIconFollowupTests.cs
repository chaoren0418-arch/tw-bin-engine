using System.Reflection;
using System.Text;
using P06.NativeUi;
namespace P06;

// R5-F（T311／#738）離線測試：180 的 startMsg、tooltip 模式 2／3／4／5／7／8／9–12、82 變體旗標（574／518／575／576）、字串 id 核對。
// 依據 docs/reports/issue-738.md（sub_9061E0、sub_F05B10、sub_736AC0、sub_F14F90 的靜態反組譯）。
// effectlist2 用合成表（id、icon、startMsg 屬於測試自訂），不含原客戶端內容；不開遊戲、不連網路。
static class StatusIconFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Box(byte sub,byte[] fields)=>LoginPackets.Pad([250,sub,..fields]);
    static byte[] Leaves(byte value)=>Box(82,[value,0,0,0]);
    static byte[] NoneTime(byte on,uint effect,uint text=0)=>Box(180,[on,..Le32(effect),..Le32(text),..Le16(0)]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static string Icons(WorldSession s)=>string.Join(",",s.PacketBoxStatusIcons.Select(i=>$"{i.Effect}/{i.DisplayMode}/{i.Value?.ToString()??"-"}"));

    static WorldSession Own(Func<long> clock){
        var s=new WorldSession();s.MagicStatusClock=clock;
        typeof(WorldSession).GetProperty("Status")!.SetValue(s,new OwnStatus(Me,1,0,10,11,12,13,14,15,100,100,100,100,0,0,0,50,0,0,0,0,0,0));
        return s;
    }

    static BuffEffectTable Table(){
        var x=new StringBuilder("<effectlist>");
        for(int i=0;i<300;i++){
            x.Append($"<effect id=\"{i}\" icon=\"{7000+i}\"");
            switch(i){
            case 78:x.Append(" duration=\"-1\" startMsg=\"5\" startMsgName=\"TEST_78\"");break;
            case 79:x.Append(" duration=\"-1\"");break;
            case 80:x.Append(" duration=\"-1\" startMsg=\"999999\"");break;
            case 293:x.Append(" duration=\"-1\" startMsg=\"6\"");break;
            }
            x.Append("/>");
        }
        return BuffEffectTable.Parse(UiLayout.Parse(x.Append("</effectlist>").ToString(),"effectlist2.xml"));
    }
    static readonly BuffTooltipText Text=new("L","U","O","P","R","M","D","H","Q");
    static string? Tip(int mode,int? seconds,BuffTooltipText? text=null,bool expired=false,bool off=false)=>
        BuffIconsPanel.TooltipText(new BuffEntry(1,seconds,expired,"t",DisplayMode:mode),off,text??Text);

    [WorldModuleTest("status-icon-followup-strings")]
    static void Strings(WorldModuleTestContext t){
        // sub_9061E0 push 的字串 id（VA 0x906259／0x90628E／0x906304／0x906342／0x906330／0x906436／0x90644A／0x90664B）。
        t.Equal(930,BuffTooltipText.LabelId,"mode 0 label 0x3A2");
        t.Equal(931,BuffTooltipText.UnitId,"mode 0 unit 0x3A3");
        t.Equal(2854,BuffTooltipText.OverId,"offIcon text 0xB26");
        t.Equal(1551,BuffTooltipText.PercentId,"mode 1 label 0x60F");
        t.Equal(978,BuffTooltipText.RemainId,"modes 2/4/7/8 label 0x3D2");
        t.Equal(1587,BuffTooltipText.MinuteId,"minute unit 0x633");
        t.Equal(763,BuffTooltipText.DayId,"day unit 0x2FB");
        t.Equal(1630,BuffTooltipText.HourId,"hour unit 0x65E");
        t.Equal(4829,BuffTooltipText.Percent12Id,"mode 12 label 0x12DD");
        var ids=new List<int>();var all=BuffTooltipText.From(id=>{ids.Add(id);return $"#{id}";});
        t.Check(ids.SequenceEqual([930,931,2854,1551,978,1587,763,1630,4829]),"From looks up every id once",ids);
        t.Check(all is {Label:"#930",Unit:"#931",Over:"#2854",Percent:"#1551",Remain:"#978",Minute:"#1587",Day:"#763",Hour:"#1630",Percent12:"#4829"},"From maps ids to fields",all);
        var missing=BuffTooltipText.From(id=>id==BuffTooltipText.Percent12Id?null:"x");
        t.Check(missing.Percent12==null&&missing.Remain=="x","id past the table stays null (4829 is not in the local 3484-record table)");
    }

    [WorldModuleTest("status-icon-followup-tooltip")]
    static void Tooltip(WorldModuleTestContext t){
        // 模式 0／6／1 行為不變。
        t.Equal("L: 12 U",Tip(0,12),"mode 0 seconds");
        t.Equal("L: 12 U",Tip(6,12),"mode 6 = mode 0");
        t.Equal("L: -- U",Tip(0,-1),"mode 0 no limit");
        t.Equal("L: -- U",Tip(0,0,expired:true),"mode 0 time over");
        t.Equal("O",Tip(0,0,expired:true,off:true),"mode 0 offIcon → 2854");
        t.Equal("P: 37%",Tip(1,37),"mode 1 percent with 1551");
        // 模式 2：值＋分（978、值、1587），"%s: %d%s" 無空格。
        t.Equal("R: 15M",Tip(2,15),"mode 2 value + minute unit");
        t.Equal("R: -1M",Tip(2,-1),"mode 2 prints the value as sent");
        // 模式 4：left/60＋1 分。
        t.Equal("R: 3M",Tip(4,125),"mode 4 125 s → 3");
        t.Equal("R: 1M",Tip(4,0),"mode 4 0 s → 1");
        t.Equal("R: 2M",Tip(4,60),"mode 4 60 s → 2");
        // 模式 7：日 時 分（分＋1），三段都印。
        t.Equal("R: 1D 1H 2M",Tip(7,90061),"mode 7 1d 1h 1m 1s");
        t.Equal("R: 0D 0H 1M",Tip(7,0),"mode 7 zero");
        t.Equal("R: 2D 0H 1M",Tip(7,172800),"mode 7 two days");
        // 模式 8：最多兩段非 0（日 時 分 秒），分不加 1，秒用 931。
        t.Equal("R: 1D 1H",Tip(8,90061),"mode 8 keeps the first two non-zero parts");
        t.Equal("R: 1H 1M",Tip(8,3661),"mode 8 hour + minute");
        t.Equal("R: 1D 5U",Tip(8,86405),"mode 8 skips zero hour/minute");
        t.Equal("R: 59U",Tip(8,59),"mode 8 seconds only");
        t.Equal("R: 2M",Tip(8,120),"mode 8 minutes only");
        t.Equal("R:",Tip(8,0),"mode 8 all zero → label only");
        // 模式 12：4829 百分比。
        t.Equal("Q: 7%",Tip(12,7),"mode 12 percent with 4829");
        t.Equal((string?)null,Tip(12,7,new("L","U","O","P","R","M","D","H",null)),"mode 12 without string 4829 → no text");
        // 模式 3 寫空字串、5／9／10／11 不寫 → 不顯示。
        foreach(int m in new[]{3,5,9,10,11,13})t.Equal((string?)null,Tip(m,30),$"mode {m} → no tooltip");
        // 缺字串或缺值 → 不猜。
        t.Equal((string?)null,Tip(2,5,new("L","U","O","P")),"mode 2 without 978/1587");
        t.Equal((string?)null,Tip(7,5,new("L","U","O","P","R","M")),"mode 7 without day/hour units");
        t.Equal((string?)null,Tip(8,5,new(null,null,null,null,"R","M","D","H")),"mode 8 without 931");
        t.Equal((string?)null,Tip(4,null),"mode 4 without a value");
        t.Check(new[]{2,4,7,8,12}.All(m=>Tip(m,30,BuffTooltipText.None)==null),"no string table → no text");

        // 面板經 Placed 走同一條路，且模式 2 以上不畫 offIcon。
        var p=new BuffIconsPanel();p.Load(Table(),new BuffIconCatalog([]));
        var state=new BuffState([new BuffEntry(79,90061,true,"x",DisplayMode:8)],[],Text);
        p.Update(new UiBuffGameState(new UiStaticGameState(true),state));
        var placed=p.Layout.Single();
        t.Check(!placed.Off,"mode 8 never draws offIcon");
        t.Equal("R: 1D 1H",p.TooltipText(placed),"panel tooltip uses mode 8");
    }

    [WorldModuleTest("status-icon-followup-leaves")]
    static void LeavesVariants(WorldModuleTestContext t){
        // sub_736AC0 的選擇順序。
        t.Equal((int?)574,PacketBoxStatusRules.LeavesEffectFor(37,true,false),"base map 0x1900 → 574");
        t.Equal((int?)574,PacketBoxStatusRules.LeavesEffectFor(250,true,true),"base map 0x1900 wins over value and flag");
        t.Equal((int?)575,PacketBoxStatusRules.LeavesEffectFor(201,false,false),"> 200, flag off → 575");
        t.Equal((int?)576,PacketBoxStatusRules.LeavesEffectFor(201,false,true),"> 200, flag on → 576");
        t.Equal((int?)518,PacketBoxStatusRules.LeavesEffectFor(200,false,true),"≤ 200, flag on → 518");
        t.Equal((int?)175,PacketBoxStatusRules.LeavesEffectFor(1,false,false),"≤ 200, flag off → 175");
        t.Equal((int?)null,PacketBoxStatusRules.LeavesEffectFor(0,true,true),"0 → remove");
        t.Equal(293,PacketBoxStatusRules.PcCafeEffect,"flag effect 0x125");
        t.Equal(6400,PacketBoxStatusRules.LeavesMapBase,"0x1900");
        t.Equal(6400,PacketBoxStatusRules.BaseMap(6400),"map < 0x4000 is its own base");
        t.Equal(0x4000,PacketBoxStatusRules.BaseMap(0x41FF),"map ≥ 0x4000 → & 0xFE00");
        t.Equal(0x4200,PacketBoxStatusRules.BaseMap(0x4201),"instance bits cleared");

        long now=1_000_000;var s=Own(()=>now);s.StartMessageEffects=()=>null;
        Set(s,"MapId",(ushort)4);
        s.Receive(Leaves(37),"");t.Equal("175/1/37",Icons(s),"map 4, no flag → 175");
        // R7-E（#807）：變體照原版累積（sub_736AC0 只加／更新選到的變體，不移除其他的），以下期望值隨之更新。
        s.Receive(NoneTime(1,293),"");
        t.Check(s.LeavesPcCafeFlag,"effect 293 on → flag");
        t.Equal("175/1/37,518/1/37,293/0/-1",Icons(s),"effect 293 appears → 82 re-evaluated: 518 added with the kept value, 175 kept");
        var b=BuffState.From(s,null,Text);
        t.Check(b.Entries.Single(e=>e.Effect==518) is {DisplayMode:1,Seconds:37},"native buff state carries 518 as percent",b.Entries);
        s.Receive(Leaves(250),"");t.Equal("175/1/37,518/1/37,293/0/-1",Icons(s),"> 200 with flag → 576, not in the table; others unchanged");
        s.Receive(Leaves(40),"");t.Equal("175/1/37,518/1/40,293/0/-1",Icons(s),"back to 518 (only 518 updated)");
        s.Receive(NoneTime(0,293),"");
        t.Check(!s.LeavesPcCafeFlag,"effect 293 off → flag cleared");
        t.Equal("175/1/40,518/1/40",Icons(s),"effect 293 removed → 175 updated, 518 kept");

        // 基底地圖只在 82 或 293 增減時重算（原版 0x138BE64 只在那兩個時機被讀）。
        Set(s,"MapId",(ushort)6400);
        t.Equal("175/1/40,518/1/40",Icons(s),"map change alone does not re-evaluate");
        s.Receive(Leaves(40),"");t.Equal("175/1/40,518/1/40",Icons(s),"82 on map 6400 → 574, not in the table");
        Set(s,"MapId",(ushort)4);
        s.Receive(NoneTime(1,293),"");t.Equal("175/1/40,518/1/40,293/0/-1",Icons(s),"293 added on map 4 → re-evaluated to 518");
        s.Receive(NoneTime(1,293),"");t.Equal("175/1/40,518/1/40,293/0/-1",Icons(s),"repeated 293 on (no new icon) keeps the evaluation");
        Set(s,"MapId",(ushort)0x4000);
        s.Receive(Leaves(40),"");t.Equal("175/1/40,518/1/40,293/0/-1",Icons(s),"instance map 0x4000 is not 0x1900");
        s.Receive(Leaves(0),"");t.Equal("293/0/-1",Icons(s),"0 removes every variant");
        s.Reset();t.Check(!s.LeavesPcCafeFlag&&Icons(s)=="","reset clears");
    }

    [WorldModuleTest("status-icon-followup-startmsg")]
    static void StartMessage(WorldModuleTestContext t){
        var parsed=Table();
        t.Equal(5,parsed[78]!.StartMsg,"startMsg attribute parsed (record +12)");
        t.Equal(0,parsed[79]!.StartMsg,"no startMsg → 0");
        t.Equal("TEST_78",parsed[78]!.Name,"startMsgName unchanged");

        long now=1_000_000;var s=Own(()=>now);var table=Table();s.StartMessageEffects=()=>table;
        int chat=s.PlayerChatLines.Length;
        s.Receive(NoneTime(1,78,4242),"");
        string? expect=PacketBoxMessageRules.Text(5,[]);
        t.Check(expect!=null,"string 5 resolvable (table or fallback)");
        t.Equal(chat+1,s.PlayerChatLines.Length,"180 on prints one chat line");
        t.Equal(expect,s.PlayerChatLines[^1].Text,"line = startMsg of the effect (not the packet's string id 4242)");
        t.Equal<int?>(24,s.PlayerChatLines[^1].ColorIndex,"sub_7DC800 colour 24");
        t.Check(s.NoneTimeStartMessages.Single() is {Subtype:180,StringId:5},"recorded as subtype 180, string 5",s.NoneTimeStartMessages);
        s.Receive(NoneTime(1,78),"");
        t.Equal(chat+2,s.PlayerChatLines.Length,"every on prints again (existing icon too)");
        s.Receive(NoneTime(0,78),"");
        t.Equal(chat+2,s.PlayerChatLines.Length,"off prints nothing");
        s.Receive(NoneTime(1,79),"");
        t.Equal(chat+2,s.PlayerChatLines.Length,"startMsg 0 prints nothing");
        s.Receive(NoneTime(1,600),"");
        t.Equal(chat+2,s.PlayerChatLines.Length,"effect ≥ 540 prints nothing");
        s.Receive(NoneTime(1,299+1),"");
        t.Equal(chat+2,s.PlayerChatLines.Length,"effect not in the table prints nothing");
        if(ServerMessageText.Table!=null){
            s.Receive(NoneTime(1,80),"");
            t.Equal(chat+2,s.PlayerChatLines.Length,"startMsg past the string table prints nothing");
        }
        s.StartMessageEffects=()=>null;
        s.Receive(NoneTime(1,78),"");
        t.Equal(chat+2,s.PlayerChatLines.Length,"no effect table → nothing printed (no guess)");
        s.StartMessageEffects=()=>throw new UiResourceException("x");
        s.Receive(NoneTime(1,78),"");
        t.Equal(chat+2,s.PlayerChatLines.Length,"table error → nothing printed");
        t.Check(s.NoneTimeIcons.Any(i=>i.IconId==78),"icon still added when the message cannot be printed");
        s.Reset();t.Equal(0,s.NoneTimeStartMessages.Length,"reset clears");
    }
}
