using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// T276（#667）ring-windows 離線測試：控制戒指開窗（S36 type 1／5）與盟屋地圖 674 鈕。
// 只用合成封包（欄位依 server-protocol.md 的 S_Ability、S_Bookmarks、S_HouseMap、C_NPCAction）、記憶體內假傳送端與合成單色 surf；
// 不開遊戲、網路、視窗或客戶端素材。
static class RingWindowsTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Board=0x12345678;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static string Hex(byte[]? b)=>b==null?"(null)":Convert.ToHexString(b);
    static byte[] Ability(byte type,byte on)=>P([36,type,on]);
    static byte[] Appear(uint id,ushort x,string name)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),
        Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static OwnStatus Status(int level)=>JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{Me},\"Level\":{level},\"Charisma\":25,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}}")!;
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();

    static LoginConnection Seed(List<byte[]> log,params int[] skills){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",Status(44));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");
        var learned=(HashSet<int>)typeof(WorldSession).GetField("learnedMagic",F)!.GetValue(c.World)!;foreach(int s in skills)learned.Add(s);
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }

    [WorldModuleTest("ring-windows")]
    static void Rules(WorldModuleTestContext t){
        // 1. 規則常數（followups-3.md 第 3 節）。
        t.Check(RingWindowRules.TeleportSkill(5)&&RingWindowRules.TeleportSkill(69)&&!RingWindowRules.TeleportSkill(4)&&!RingWindowRules.TeleportSkill(51),"teleport skills 5／69 only (sub_BE0F10 index 4／68)");
        t.Check(new[]{5,6,83,84,0x4000,0x7FFF}.All(RingWindowRules.TeleportBlocked)&&!new[]{0,4,7,82,85,0x3FFF}.Any(RingWindowRules.TeleportBlocked),"blocked maps 5／6／83／84／≥0x4000");
        t.Check(SummonCatalog.RingLevelAllows(28,"7")&&SummonCatalog.RingLevelAllows(28,"263")&&SummonCatalog.RingLevelAllows(28,"519"),"lv 28: action 7／263／519 allowed");
        t.Check(!SummonCatalog.RingLevelAllows(28,"8")&&!SummonCatalog.RingLevelAllows(31,"264")&&SummonCatalog.RingLevelAllows(32,"520"),"lv 28／31 refuse 8／264; lv 32 allows 520");
        t.Check(SummonCatalog.RingLevelAllows(72,"18")&&!SummonCatalog.RingLevelAllows(71,"18"),"(lv−28)/4+7 ≥ action%256");
        t.Check(!SummonCatalog.RingLevelAllows(99,"x")&&!SummonCatalog.RingLevelAllows(99,"0"),"non-numeric／zero action refused");

        // 2. 674 封包：C125 D(board) S("select") S("%u" 區域<<16|編號)。7<<16|5＝458757。
        t.Equal("7D78563412"+"73656C65637400"+"34353837353700"+"0000000000",Hex(AgitSelectPackets.Select(Board,7u<<16|5)),"C125 D(board) S(select) S(458757), padded to 24");
        t.Throws<ArgumentException>(()=>AgitSelectPackets.Select(0,5),"board id 0 rejected");
    }

    [WorldModuleTest("ring-windows-teleport")]
    static void Teleport(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log,5,69);var w=c.World;
        w.Receive(P([92],Str("家"),Le16(4),Le16(32600),Le16(32900),Le32(77)),"offline");
        t.Check(w.Bookmarks.TryGet(77) is {MapId:4},"bookmark 77 seeded");

        // 沒戴戒指：不攔截（回到原本施放流程；完整施放閘門需要素材，不在此測）。
        w.SelectMagic(5);
        t.Check(!w.TryOpenRingWindow()&&w.TeleportDestination==null&&log.Count==0,"no ring: no window, no interception");
        // 戴傳送控制戒指（S36 type 1＝1）：開選目的地窗，不送 C6。
        w.Receive(Ability(1,1),"offline");
        t.Check(!Run(c.CastSelectedMagic())&&log.Count==0,"ring: cast intercepted, nothing sent");
        var q=w.TeleportDestination;
        t.Check(q is {Skill:5},"destination request opened for skill 5",q);
        t.Check(!Run(c.CastSelectedMagic())&&w.TeleportDestination==q,"window already open: same request, not reopened");
        t.Equal(5,JsonSerializer.SerializeToElement(typeof(WorldSession).GetMethod("AbilitySnapshot",F)!.Invoke(w,null)).GetProperty("teleportDestination").GetProperty("skill").GetInt32(),"snapshot shows the request");
        // 選定書籤 → 3.81 HD（書籤地圖、書籤 id），不是 TW16 的 hhh。
        var target=w.TeleportDestinationTarget(q!.Revision,77);
        t.Check(target is {Kind:SkillTargetKind.Bookmark,MapId:4,BookmarkId:77},"target = bookmark map 4 + id 77",target);
        t.Equal("06000404004D00000000000000000000",Hex(SkillCast.Request(5,target!)),"C6 skill 5 HD(map 4, bookmark 77)");
        t.Check(w.TeleportDestinationTarget(q.Revision,78)==null,"unknown bookmark refused");
        t.Check(w.TeleportDestinationTarget(q.Revision+1,77)==null,"stale revision refused");
        t.Check(!Run(c.SendTeleportDestination(q.Revision+1,77))&&log.Count==0,"stale send refused, nothing sent");
        // 選取技能被換掉時，選目的地會把技能設回請求的技能。
        w.SelectMagic(69);t.Check(w.TeleportDestinationTarget(q.Revision,77) is not null&&w.SelectedMagicId==5,"destination restores the requested skill");
        // ESC／取消：關窗，不送。
        t.Check(c.CloseTeleportDestination(q.Revision)&&w.TeleportDestination==null&&!c.CloseTeleportDestination(q.Revision),"cancel closes once");
        // 集體傳送 69 同樣開窗。
        w.SelectMagic(69);Run(c.CastSelectedMagic());
        t.Check(w.TeleportDestination is {Skill:69}&&log.Count==0,"skill 69 opens the window too");
        c.CloseTeleportDestination(w.TeleportDestination!.Revision);
        // 限制地圖：R7-C（#805，#736 0xE08B22）開窗路徑不查限制地圖、不顯示 276，照樣開窗、不送。
        Set(w,"MapId",(ushort)84);
        t.Check(!Run(c.CastSelectedMagic())&&w.TeleportDestination is {Skill:69}&&log.Count==0,"blocked map: window still opens, nothing sent");
        t.Check(w.MagicNotice!=ServerMessageText.Decode(RingWindowRules.TeleportBlockedMessage,[]).Text,"blocked map: no message 276");
        c.CloseTeleportDestination(w.TeleportDestination!.Revision);
        // 卸下戒指：回到原本流程。
        Set(w,"MapId",(ushort)0);w.Receive(Ability(1,0),"offline");
        t.Check(!w.TryOpenRingWindow()&&w.TeleportDestination==null,"ring removed: no interception");
        // Reset 清掉請求。
        w.Receive(Ability(1,1),"offline");w.SelectMagic(5);Run(c.CastSelectedMagic());
        t.Check(w.TeleportDestination!=null,"reopened");
        w.Reset();
        t.Check(w.TeleportDestination==null,"reset clears the request");
    }

    [WorldModuleTest("ring-windows-summon")]
    static void Summon(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log,51);var w=c.World;
        w.SelectMagic(51);
        t.Check(!w.TryOpenRingWindow()&&w.SummonRequestState==null,"no ring: no summon window");
        // 戴召喚控制戒指（S36 type 5＝1）：施放 51 改開本機召喚窗，不送封包。
        w.Receive(Ability(5,1),"offline");
        t.Check(!Run(c.CastSelectedMagic())&&log.Count==0,"ring: cast intercepted, nothing sent");
        var q=w.SummonRequestState;
        t.Check(q is {Source:"local",ObjectId:Me},"local summon request opened (native SummonPanel shows it)",q);
        t.Check(!Run(c.CastSelectedMagic())&&w.SummonRequestState==q,"window already open: not reopened");
        var v=c.SummonSnapshot();
        t.Check(v.Open&&v.Source=="local"&&v.SkillLearned,"summon view open, skill 51 learned",v);
        // 743：等級 28 選 action 8（32 級）→ 不送、顯示 743、窗與選擇保留。
        Set(w,"Status",Status(28));v=c.SummonSnapshot();
        t.Check(!Run(c.SendSummonChoice(v,"8"))&&log.Count==0,"level too low: nothing sent");
        t.Equal(ServerMessageText.Decode(RingWindowRules.SummonLevelMessage,[]).Text,w.SummonNotice,"level too low shows message 743");
        t.Check(w.SummonRequestState==q&&c.SummonSnapshot().Revision==v.Revision,"request and revision kept after 743");
        t.Check(w.RingSummonAllowed("263"),"level 28 allows action 263 (263 % 256 = 7)");
        t.Check(c.CloseSummon(c.SummonSnapshot()),"close");
        w.Receive(Ability(5,0),"offline");
        t.Check(!w.TryOpenRingWindow()&&w.SummonRequestState==null,"ring removed: no interception");
    }

    sealed class Host:IAgitMapHost{public List<AgitMapOpenRequest> Calls=[];public void Select(AgitMapOpenRequest shown)=>Calls.Add(shown);}
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF808080u,w*h).ToArray()));
    static UiMouseEvent M(UiMouseKind k,Point at)=>new(k,at,MouseButtons.Left,1,0,Keys.None);
    static Point Center(Rectangle r)=>new(r.X+r.Width/2,r.Y+r.Height/2);

    [WorldModuleTest("ring-windows-agit")]
    static void Agit(WorldModuleTestContext t){
        // 1. 面板：按下＋放開都在 674 → 宿主收到這一次的 S187；按下 674、放開在別處 → 不送。678 照舊關窗。
        var surfs=AgitMapLayout.AllSurfs.Distinct().ToDictionary(id=>id,id=>Surface(id,id is 678 or 674?30:id is 176 or 34?4:300,id is 678 or 674?14:id is 176 or 34?4:320));
        AgitMapOpenRequest? current=new(1,new(Board,7u<<16|5));
        var p=new AgitMapPanel(()=>current);p.Load(id=>surfs.GetValueOrDefault(id));var host=new Host();p.Attach(host);var cmd=new UiNullCommands();
        p.Update(new UiStaticGameState(InWorld:true,Now:0));
        t.Check(p.Visible&&p.Host==host,"panel visible with host");
        p.Mouse(M(UiMouseKind.Down,Center(p.ActionRect)),cmd);p.Mouse(M(UiMouseKind.Up,Center(p.ActionRect)),cmd);
        t.Check(host.Calls.Count==1&&host.Calls[0]==current&&p.Visible,"674 click → host.Select(shown); window stays open",host.Calls);
        p.Mouse(M(UiMouseKind.Down,Center(p.ActionRect)),cmd);p.Mouse(M(UiMouseKind.Up,new(5,5)),cmd);
        t.Check(host.Calls.Count==1,"press 674, release elsewhere → nothing");
        p.Mouse(M(UiMouseKind.Down,Center(p.CloseRect)),cmd);p.Mouse(M(UiMouseKind.Up,Center(p.CloseRect)),cmd);
        t.Check(!p.Visible&&host.Calls.Count==1,"678 still closes without select");

        // 2. 送出端：C125 select 一次；S187 被取代後舊請求不送。
        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;
        w.Receive(P([187],Le32(Board),Le32(7u<<16|5)),"offline");
        var shown=new AgitMapOpenRequest(1,w.AgitHouseMap!);
        t.Check(Run(c.SendAgitSelect(shown)),"select admitted");
        t.Equal(Hex(AgitSelectPackets.Select(Board,7u<<16|5)),Hex(log.LastOrDefault()),"sent C125 D(board) S(select) S(458757)");
        w.Receive(P([187],Le32(Board),Le32(4u<<16|2)),"offline");
        t.Check(!Run(c.SendAgitSelect(shown))&&log.Count==1,"superseded S187: old request not sent");
        t.Check(!Run(c.SendAgitSelect(new(2,new(Board,9u<<16|1))))&&log.Count==1,"out-of-range house not sent");
        t.Check(!Run(c.SendAgitSelect(new(3,new(0,4u<<16|2))))&&log.Count==1,"board 0 not sent");
        w.Reset();
    }
}
