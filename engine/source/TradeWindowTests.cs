using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// M7-S（T219／#526）離線測試：合成封包＋合成 XML／圖（不讀真實客戶端），記憶體內假傳送端；不開遊戲、網路、視窗或素材。
static class TradeWindowTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Npc=456,Weapon=901;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static byte[] RepairList(params (uint id,byte damage)[] rows)=>Packet(b=>{b.Write((byte)83);b.Write(200u);b.Write((ushort)rows.Length);foreach(var (id,damage) in rows){b.Write(id);b.Write(damage);}});
    static byte[] Dialog(uint npc,string html)=>Packet(b=>{b.Write((byte)39);b.Write(npc);Text(b,html);b.Write((ushort)0);b.Write((ushort)0);});
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        // S87 NPC 相鄰一格（欄位同 weapon-repair-tests）；S5 空背包＋S15 一把武器（use 1）。
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(Npc);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,"NPC");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
        c.World.SelectTarget(Npc);Feed(c,LoginPackets.Pad([5,0]));
        Feed(c,Packet(b=>{b.Write((byte)15);b.Write(Weapon);b.Write((ushort)0);b.Write((byte)1);b.Write((byte)0);b.Write((ushort)0);b.Write((byte)1);b.Write(1u);b.Write((byte)0);Text(b,"test weapon");b.Write((byte)0);b.Write(new byte[11]);}));
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Talk(LoginConnection c)=>c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision).GetAwaiter().GetResult();
    static bool Option(LoginConnection c,int id)=>c.Interact("option",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision,id).GetAwaiter().GetResult();
    static void FixPage(LoginConnection c)=>Set(c.World,"InteractionPage",new NpcPage("修理武器嗎？",[new NpcOption(1,"修理","action",WeaponRepairPackets.FixAction,true),new NpcOption(2,"離開","action","quit",true)],""));

    [WorldModuleTest("trade-window")]
    static void Run(WorldModuleTestContext t){
        Registration(t);Protocol(t);
    }

    static void Registration(WorldModuleTestContext t){
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e.Kind=="observe"&&e.Opcode==39&&e.Subtype==-1&&e.Method=="WorldSession.ObserveRepairDialog"),"S39 dialog observer registered");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetWeaponRepairDialog"),"repair dialog reset registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);
        t.Check(UiPanelRegistry.Panels.Count(p=>p.Id==TradeWindowPanel.PanelId&&p.Layout=="uiextra86")==1,"TRADE_WINDOW registered once on uiextra86");
        // 專用 Form 與 HUD 鈕已移除（WIN§2 T134）。
        var form=typeof(ClientForm);
        t.Check(form.GetMethod("OpenWeaponRepair",F)==null&&form.GetField("weaponRepairWindow",F)==null,"weapon repair Form removed");
        t.Equal("touma3",WeaponRepairPackets.EmptyListPage("touma1"),"touma prefix → touma3");
        t.Equal("gayle5",WeaponRepairPackets.EmptyListPage("GAYLE2"),"gayle prefix → gayle5 (case-insensitive)");
        t.Equal<string?>(null,WeaponRepairPackets.EmptyListPage("smith1"),"other pages → no empty page");
        t.Check(WeaponRepairPackets.TryItemOption(WeaponRepairPackets.ItemOption(Weapon,7),out uint item,out int rev)&&item==Weapon&&rev==7,"item option round trip");
        t.Check(!WeaponRepairPackets.TryItemOption("fix-item:0:1",out _,out _)&&!WeaponRepairPackets.TryItemOption("fix-item:1",out _,out _)&&!WeaponRepairPackets.TryItemOption("fix",out _,out _),"bad item options rejected");
    }

    static void Protocol(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        t.Check(c.World.Target!=null&&c.World.InventoryLoaded,"seed: NPC selected, inventory loaded");
        // 未接線時 fix 不在本機處理（照舊走 C125）。
        t.Check(!c.World.HandleRepairOption(WeaponRepairPackets.FixAction),"unwired fix not consumed");
        c.WireWeaponRepairDialog();c.WireWeaponRepairDialog();

        // 1. touma：對話 → fix → C118 → 空清單 → touma3。
        t.Check(Talk(c),"talk sent");Feed(c,Dialog(Npc,"touma1"));
        t.Equal("dialog",c.World.InteractionKind,"dialog open");
        FixPage(c);int before=sent.Count;
        t.Check(Option(c,1),"fix option handled");
        t.Equal(before+1,sent.Count,"fix sends exactly one packet (wired once)");
        t.Equal(Convert.ToHexString(WeaponRepairPackets.List()),Convert.ToHexString(sent[^1]),"fix sends C118, not C125");
        t.Equal("querying",c.World.WeaponRepairPhase,"repair querying");
        Feed(c,RepairList());
        t.Equal<string?>("touma3",c.World.WeaponRepairEmptyPage,"empty list opens touma3");
        t.Equal("dialog",c.World.InteractionKind,"empty page is an NPC dialog page");
        t.Check(c.World.WeaponRepairWindow()==null,"empty list: TRADE_WINDOW stays closed");

        // 2. gayle：同一位 NPC 換頁後 → gayle5。
        Feed(c,Dialog(Npc,"gayle1"));FixPage(c);t.Check(Option(c,1),"gayle fix handled");Feed(c,RepairList());
        t.Equal<string?>("gayle5",c.World.WeaponRepairEmptyPage,"empty list opens gayle5");

        // 3. 其他頁名：不推定頁面，對話頁不換。
        Feed(c,Dialog(Npc,"smith1"));FixPage(c);t.Check(Option(c,1),"other fix handled");int pageRevision=c.World.InteractionRevision;Feed(c,RepairList());
        t.Equal<string?>(null,c.World.WeaponRepairEmptyPage,"unknown page prefix: no empty page");
        t.Equal(pageRevision,c.World.InteractionRevision,"unknown page prefix: dialog untouched");

        // 4. 舊介面（原生窗未接）：清單列成對話頁選項，選一件送 C20。
        Feed(c,Dialog(Npc,"touma1"));FixPage(c);t.Check(Option(c,1),"fix for rows");Feed(c,RepairList((Weapon,3)));
        var options=c.World.InteractionPage?.Options??[];
        t.Check(options.Length==1&&options[0].Kind=="action"&&options[0].Enabled&&options[0].Label.Contains("600"),"legacy page lists repairable weapon with cost",options);
        var view=c.World.WeaponRepairWindow();
        t.Check(view is {Mode:6}&&view.Rows.Single() is {Id:Weapon,Enabled:true,Detail:"600"},"TRADE_WINDOW mode 6 view",view);
        before=sent.Count;t.Check(Option(c,options[0].Id),"legacy item option handled");
        t.Equal(Convert.ToHexString(WeaponRepairPackets.Repair(Weapon,Npc)),Convert.ToHexString(sent[^1]),"item option sends C20 item+NPC");
        t.Equal(before+1,sent.Count,"one C20");
        t.Equal("submitted",c.World.WeaponRepairPhase,"repair submitted");
        t.Check(!Option(c,options[0].Id)||sent.Count==before+1,"stale option never resends");
        t.Equal(before+1,sent.Count,"no duplicate C20");

        // 5. 原生窗已接：清單不改對話頁，只給 TRADE_WINDOW。
        c.World.CloseWeaponRepair();c.World.WeaponRepairNativeWindow=true;
        Feed(c,Dialog(Npc,"touma1"));FixPage(c);t.Check(Option(c,1),"fix with native window");pageRevision=c.World.InteractionRevision;
        Feed(c,RepairList((Weapon,2)));
        t.Equal(pageRevision,c.World.InteractionRevision,"native window: dialog page untouched");
        t.Check(c.World.WeaponRepairWindow(s=>"<"+s+">") is {Rows:[{Name:"<test weapon>",Detail:"400"}]},"view resolves names through caller");
        c.World.CloseWeaponRepair();
        t.Check(c.World.WeaponRepairWindow()==null,"closed repair → no view");

        // 6. 其他動作照舊送 C125。
        Feed(c,Dialog(Npc,"touma1"));FixPage(c);before=sent.Count;t.Check(Option(c,2),"quit option sent");
        t.Equal(125,(int)sent[^1][0],"non-repair action still C125");
    }

    // 面板測試（合成 XML／圖）已隨 R2-E（#623）改版移到 ShopTradeTests.cs。
}
