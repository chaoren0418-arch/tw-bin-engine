using System.Reflection;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R4-F（T297／#709）離線測試：S250 75／82／180 進原生 BuffState 與 STATUS_EFFECT_ICONS（圖示、百分比 tooltip、75 改寫圖示、
// 無期限）、預設模式狀態列的 effect 對照（水中呼吸 11）、寵物頁 AC／背包過時重要、召喚控制戒指的本機請求切到寵物分頁。
// 只用依 3.81 伺服器寫法合成的封包、合成的 effectlist2（只有 id／icon／duration 屬性，不是原客戶端內容）與記憶體內假傳送端；
// 不開遊戲、網路、視窗或客戶端素材。
static class StatusAndPetFeedTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Pet=456;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static string Hex(byte[]? b)=>b==null?"(null)":Convert.ToHexString(b);
    static byte[] Box(byte sub,byte[] fields)=>LoginPackets.Pad([250,sub,..fields]);
    static byte[] Dk(byte level)=>Box(75,[level]);
    static byte[] Leaves(byte value)=>Box(82,[value,0,0,0]);
    static byte[] NoneTime(byte on,uint effect,uint text)=>Box(180,[on,..Le32(effect),..Le32(text),..Le16(0)]);
    static byte[] Eva(uint id,int seconds)=>LoginPackets.Pad([126,..Le32(id),..Le16((ushort)seconds)]);
    static byte[] Ability(byte type,byte on)=>P([36,type,on]);
    static byte[] Appear(uint id,ushort x,string name,string owner="",byte hp=100)=>P([87],Le16(x),Le16(32940),Le32(id),Le16((ushort)(id==Me?0:1110)),[0,0,0,0],Le32(0),Le16(0),
        Str(name),Str(""),[0],Le32(0),Str(""),Str(owner),[0,hp,0,0,0,255,255]);
    static byte[] Equip(uint pet,byte listNo,sbyte ac,byte data=2)=>P([250,37,data],Le32(pet),[listNo,unchecked((byte)ac)]);
    static byte[] Bag(uint pet,sbyte ac,params (uint id,byte identified,string name)[] rows)=>P([176],Le32(pet),Le16((ushort)rows.Length),[11],
        rows.SelectMany(r=>(byte[])[..Le32(r.id),2,..Le16(100),1,..Le32(1),r.identified,..Str(r.name)]).ToArray(),[unchecked((byte)ac)]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();

    static WorldSession Own(uint id,Func<long> clock){
        var s=new WorldSession();s.MagicStatusClock=clock;
        typeof(WorldSession).GetProperty("Status")!.SetValue(s,new OwnStatus(id,1,0,10,11,12,13,14,15,100,100,100,100,0,0,0,50,0,0,0,0,0,0));
        return s;
    }
    static LoginConnection Seed(List<byte[]> log,params int[] skills){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":44,\"Charisma\":25,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");c.World.Receive(Appear(Pet,32601,"夥伴","offline",70),"offline");
        var learned=(HashSet<int>)typeof(WorldSession).GetField("learnedMagic",F)!.GetValue(c.World)!;foreach(int s in skills)learned.Add(s);
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }

    // 合成的 effectlist2：id 0–199 依序；只給測試用到的 icon／offIcon／duration（數值是測試自訂，不是原客戶端的值）。
    static BuffEffectTable Table(int dkDuration=60,int dkOffIcon=0){
        var x=new StringBuilder("<effectlist>");
        for(int i=0;i<200;i++){
            x.Append($"<effect id=\"{i}\"");
            switch(i){
            case 11:x.Append(" icon=\"552\"");break;
            case 78:x.Append(" icon=\"7078\" duration=\"-1\"");break;
            case 135:x.Append($" icon=\"7135\" duration=\"{dkDuration}\"");if(dkOffIcon>0)x.Append($" offIcon=\"{dkOffIcon}\"");break;
            case 175:x.Append(" icon=\"7175\" offIcon=\"7176\"");break;
            case 190:x.Append(" icon=\"0\"");break;
            }
            x.Append("/>");
        }
        return BuffEffectTable.Parse(UiLayout.Parse(x.Append("</effectlist>").ToString(),"effectlist2.xml"));
    }
    static BuffIconsPanel Panel(BuffEffectTable table){var p=new BuffIconsPanel();p.Load(table,new BuffIconCatalog([]));return p;}
    static readonly BuffTooltipText Text=new("L","U","O","P");

    [WorldModuleTest("status-pet-feed-buffs")]
    static void Buffs(WorldModuleTestContext t){
        // 1. 字串 id：mode 1 的標籤是 1551（sub_9061E0 case 1，VA 0x9062FD `push 0x60F`，格式 "%s: %d%%"）。
        t.Equal(1551,BuffTooltipText.PercentId,"percent label string 1551");
        t.Check(BuffTooltipText.None.Percent==null,"no string table → no percent label");

        // 2. BuffState 讀 PacketBoxStatusIcons：75／82／180 都進原生 entries。
        long now=5_000_000;var s=Own(Me,()=>now);
        s.Receive(Dk(2),"");s.Receive(Leaves(37),"");s.Receive(NoneTime(1,78,0),"");s.Receive(NoneTime(1,600,0),"");
        var b=BuffState.From(s,null,Text);
        var dk=b.Entries.Single(e=>e.Effect==135);
        t.Check(dk is {DisplayMode:0,IconOverride:3228,DefaultDuration:true,ElapsedMs:0,Seconds:null,Origin:"dk:2"},"75 → effect 135, mode 0, icon 3228, table duration from now",dk);
        var leaves=b.Entries.Single(e=>e.Effect==175);
        t.Check(leaves is {DisplayMode:1,Seconds:37,IconOverride:null,DefaultDuration:false},"82 → effect 175, mode 1, value 37 in the seconds slot",leaves);
        var none=b.Entries.Single(e=>e.Effect==78);
        t.Check(none is {DisplayMode:0,Seconds:-1,DefaultDuration:false},"180 → effect 78, no limit",none);
        t.Check(b.Entries.All(e=>e.Effect!=600),"180 effect ≥ 540 not created");
        t.Check(b.Unmapped.Count==0,"75/82/180 never unmapped",b.Unmapped);
        now+=7_500;
        t.Check(BuffState.From(s,null,Text).Entries.Single(e=>e.Effect==135).ElapsedMs==7_500,"elapsed time since the 75 grows");
        s.Receive(Dk(3),"");
        t.Check(BuffState.From(s,null,Text).Entries.Single(e=>e.Effect==135) is {IconOverride:3227,ElapsedMs:0},"a new 75 rewrites the icon and restarts the duration");

        // 3. 原生面板：圖示（75 改寫）、百分比 tooltip、無期限、倒數。
        var p=Panel(Table());
        p.Update(new UiBuffGameState(new UiStaticGameState(true),BuffState.From(s,null,Text)));
        var placedDk=p.Layout.Single(x=>x.Effect.Id==135);var placedLeaves=p.Layout.Single(x=>x.Effect.Id==175);var placedNone=p.Layout.Single(x=>x.Effect.Id==78);
        t.Equal(3227,placedDk.Icon,"75 draws the rewritten icon, not the table icon 7135");
        t.Equal(7175,placedLeaves.Icon,"82 draws effect 175's icon");
        t.Equal(7078,placedNone.Icon,"180 draws the effect's icon");
        t.Equal("L: 60 U",p.TooltipText(placedDk),"75 tooltip counts the table duration (60 s)");
        t.Equal("P: 37%",p.TooltipText(placedLeaves),"82 tooltip \"%s: %d%%\" with string 1551");
        t.Equal("L: -- U",p.TooltipText(placedNone),"180 tooltip \"--\" (no limit)");
        t.Check(!placedLeaves.Off,"mode 1 never draws offIcon");
        now+=20_000;p.Update(new UiBuffGameState(new UiStaticGameState(true),BuffState.From(s,null,Text)));
        t.Equal("L: 40 U",p.TooltipText(p.Layout.Single(x=>x.Effect.Id==135)),"75 countdown after 20 s");
        now+=41_000;p.Update(new UiBuffGameState(new UiStaticGameState(true),BuffState.From(s,null,Text)));
        var over=p.Layout.Single(x=>x.Effect.Id==135);
        t.Check(over is {Off:false,Icon:3227}&&over.Entry.Expired,"time over without offIcon keeps the icon",over);
        t.Equal("L: -- U",p.TooltipText(over),"time over → \"--\" (sub_9061E0 expired flag)");
        var withOff=Panel(Table(dkOffIcon:7999));withOff.Update(new UiBuffGameState(new UiStaticGameState(true),BuffState.From(s,null,Text)));
        var off=withOff.Layout.Single(x=>x.Effect.Id==135);
        t.Check(off is {Off:true,Icon:7999},"time over with offIcon draws offIcon",off);
        t.Equal("O",withOff.TooltipText(off),"offIcon tooltip = string 2854");
        var forever=Panel(Table(dkDuration:-1));forever.Update(new UiBuffGameState(new UiStaticGameState(true),BuffState.From(s,null,Text)));
        t.Equal("L: -- U",forever.TooltipText(forever.Layout.Single(x=>x.Effect.Id==135)),"duration −1 → no limit");
        var noText=Panel(Table());noText.Update(new UiBuffGameState(new UiStaticGameState(true),BuffState.From(s,null,BuffTooltipText.None)));
        t.Check(noText.Layout.All(x=>noText.TooltipText(x)==null),"no string table → no tooltip text");

        // 4. ApplyDefaultDuration：開始時刻未知 → 不猜秒數（S171 舊行為不變）。
        var def=new BuffEffect(396,1,0,null,0,30,"");
        t.Check(BuffIconsPanel.ApplyDefaultDuration(new(396,null,false,"invisible",DefaultDuration:true),def).Seconds==null,"unknown start → no seconds");
        t.Check(BuffIconsPanel.ApplyDefaultDuration(new(396,null,false,"x",DefaultDuration:true,ElapsedMs:29_999),def) is {Seconds:1,Expired:false},"29.999 s of 30 → 1 s left");
        t.Check(BuffIconsPanel.ApplyDefaultDuration(new(396,null,false,"x",DefaultDuration:true,ElapsedMs:30_999),def) is {Seconds:0,Expired:false},"left 0 is not yet expired (original: < 0)");
        t.Check(BuffIconsPanel.ApplyDefaultDuration(new(396,null,false,"x",DefaultDuration:true,ElapsedMs:31_000),def) is {Seconds:0,Expired:true},"left < 0 → expired");
        t.Check(BuffIconsPanel.ApplyDefaultDuration(new(5,9,false,"x"),def) is {Seconds:9},"entries with their own timer untouched");

        // 5. 移除：75 階段 0、82 值 0、180 關閉 → 原生 entries 消失。
        s.Receive(Dk(0),"");s.Receive(Leaves(0),"");s.Receive(NoneTime(0,78,0),"");
        t.Check(BuffState.From(s,null,Text).Entries.Count==0,"75/82/180 removals clear the native icons");
        s.Receive(Dk(1),"");s.Reset();
        t.Check(BuffState.From(s,null,Text).Entries.Count==0,"reset clears");
    }

    [WorldModuleTest("status-pet-feed-default")]
    static void DefaultMode(WorldModuleTestContext t){
        // 預設模式（原生 UI 關閉）狀態列：水中呼吸帶 effect 11，75／82／180 帶各自的 effect；其他計時狀態沒有查證過的對照 → null。
        long now=1_000_000;var s=Own(Me,()=>now);
        s.Receive(Eva(Me,600),"");
        s.Receive(LoginPackets.Pad([250,56,..Le16(30)]),"");   // 250/56 魔法娃娃（計時，沒有 effect 對照）
        s.Receive(Dk(1),"");s.Receive(Leaves(5),"");s.Receive(NoneTime(1,78,0),"");
        var hud=StatusIconFeed.Read(s);
        var breath=hud.Single(h=>h.Id==TimedBuffRules.WaterBreath);
        t.Check(breath is {Effect:11,IconOverride:null,Seconds:600},"water breath → effect 11 in the default status bar",breath);
        t.Check(hud.Single(h=>h.Id==PacketBoxStatusRules.DollBuff).Effect==null,"timed state without a verified effect keeps the vector badge");
        t.Check(hud.Single(h=>h.Id==StatusIconEntry.PacketBoxId(135)) is {Effect:135,IconOverride:3229},"75 effect 135 with icon 3229");
        t.Check(hud.Single(h=>h.Id==StatusIconEntry.PacketBoxId(175)) is {Effect:175,IconOverride:null},"82 effect 175");
        t.Check(hud.Single(h=>h.Id==StatusIconEntry.PacketBoxId(78)).Effect==78,"180 effect 78");
        t.Equal("水中呼吸 · 600 秒",breath.Detail,"text unchanged");

        // 圖示編號：與原生面板同一張 effectlist2；override 優先；≥ 表筆數或 icon 0 → 不畫圖（退回向量）。
        var table=Table();
        t.Equal((int?)552,StatusIconArt.IconFor(table,11,null),"effect 11 → 552.tbt");
        t.Equal((int?)3229,StatusIconArt.IconFor(table,135,3229),"75 override wins");
        t.Equal((int?)null,StatusIconArt.IconFor(table,575,null),"effect beyond the table → nothing");
        t.Equal((int?)null,StatusIconArt.IconFor(table,190,null),"icon 0 → nothing");
        t.Equal((int?)null,StatusIconArt.IconFor(table,null,null),"no effect → nothing");
        var art=new StatusIconArt(table,new BuffIconCatalog([]));
        t.Check(art.ImageFor(breath,out var bmp)==null&&bmp==null,"missing .tbt → no image (vector fallback, no placeholder)");
    }

    [WorldModuleTest("status-pet-feed-pets")]
    static void Pets(WorldModuleTestContext t){
        var log=new List<byte[]>();using var c=Seed(log);var w=c.World;
        t.Check(c.PetEquipmentSnapshot(Pet)==PetEquipmentView.None,"nothing received → empty view");
        t.Equal((string?)null,PetBagRefresh.Line(null,PetEquipmentView.None),"no bag, no AC → no line");
        // 背包先到：AC 取背包。
        w.Receive(Bag(Pet,-2,(1001,1,"牙"),(1002,1,"皮盔")),"offline");
        var v=c.PetEquipmentSnapshot(Pet);
        t.Check(v is {Ac:-2,BagStale:false,Sequence:null},"bag only → AC from bag",v);
        t.Equal("寵物背包 · AC -2 · 2 項",PetBagRefresh.Line(w.PetInventory(Pet),v),"pet page line uses the bag AC");
        t.Check(!PetBagRefresh.Due(v,null,true),"fresh bag → no refresh");
        // S250/37：AC 改用 37 的值，背包過時。
        w.Receive(Equip(Pet,1,-6),"offline");
        v=c.PetEquipmentSnapshot(Pet);
        t.Check(v is {Ac:-6,BagStale:true,Sequence:not null},"S250/37 → PetAc −6, bag stale",v);
        t.Equal("寵物背包 · AC -6 · 2 項（裝備已變更，清單過時，正在重新查詢）",PetBagRefresh.Line(w.PetInventory(Pet),v),"pet page shows PetAc (S250/37), not the bag's −2");
        t.Check(PetBagRefresh.Due(v,null,true),"stale → refresh due");
        t.Check(!PetBagRefresh.Due(v,null,false),"cannot send → wait");
        t.Check(!PetBagRefresh.Due(v,v.Sequence,true),"same S250/37 already requested → not again");
        t.Equal(0,log.Count,"data layer alone sends nothing");
        // 寵物頁送既有 C103（同 PetUi.SyncPetBagRefresh 的路徑）：背包清掉 → 不再過時、AC 仍是 37 的值。
        var view=c.PetSnapshot(Pet);
        t.Check(view.CanSend,"own nearby pet can be refreshed",view);
        t.Check(Run(c.SendPet(view.Stamp,"refresh")),"refresh admitted");
        t.Equal(Hex(PetPackets.Menu(Pet)),Hex(log.LastOrDefault()),"refresh = C103 D(pet)");
        var after=c.PetEquipmentSnapshot(Pet);
        t.Check(after is {Ac:-6,BagStale:false},"bag cleared while waiting → not stale, AC from S250/37",after);
        t.Check(!PetBagRefresh.Due(after,null,true),"no repeat while waiting for the reply");
        t.Equal("寵物 AC -6（原服 S250/37；背包清單尚未取得或等待原服回覆）",PetBagRefresh.Line(w.PetInventory(Pet),after),"AC still shown without a bag");
        // 新背包到：AC 改回背包值。
        w.Receive(Bag(Pet,-7,(1001,1,"牙"),(1002,3,"皮盔")),"offline");
        t.Check(c.PetEquipmentSnapshot(Pet) is {Ac:-7,BagStale:false},"new bag → bag AC");
        // 下一次 S250/37 是新的序號 → 再要一次。
        w.Receive(Equip(Pet,0,-8),"offline");
        var next=c.PetEquipmentSnapshot(Pet);
        t.Check(next.BagStale&&next.Sequence!=v.Sequence&&PetBagRefresh.Due(next,v.Sequence,true),"a later S250/37 is due again");
        t.Equal(1,log.Count,"only the explicit refresh was sent");
        Set(c,"State",LoginState.Offline);
        t.Check(c.PetEquipmentSnapshot(Pet)==PetEquipmentView.None,"out of world → empty view");
    }

    [WorldModuleTest("status-pet-feed-ring")]
    static void Ring(WorldModuleTestContext t){
        // 召喚控制戒指（S36 type 5）＋施放 51 → 本機請求（Source local）；原生 UI 關閉時也切到寵物分頁，同一個 revision 只切一次。
        var log=new List<byte[]>();using var c=Seed(log,51);var w=c.World;
        w.SelectMagic(51);w.Receive(Ability(5,1),"offline");
        t.Check(!Run(c.CastSelectedMagic())&&log.Count==0,"ring: cast intercepted, nothing sent");
        var v=c.SummonSnapshot();
        t.Check(v is {Open:true,Source:"local"},"ring opens a local summon request",v);
        t.Check(SummonFallback.ShouldReveal(v,false,-1),"native UI off: ring request reveals the pet tab");
        t.Check(!SummonFallback.ShouldReveal(v,false,v.Revision),"revealed once per revision");
        t.Check(!SummonFallback.ShouldReveal(v,true,-1),"native UI on: SummonPanel shows it, no tab switch");
        t.Check(c.CloseSummon(v),"close");
        t.Check(!SummonFallback.ShouldReveal(c.SummonSnapshot(),false,-1),"closed request → no switch");
        t.Check(!SummonFallback.ShouldReveal(v with{Source="other"},false,-1),"unknown source → no switch");
    }
}
