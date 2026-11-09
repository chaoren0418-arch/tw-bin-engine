using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R3-F（T286／#679）離線測試：op 44（S41）／op 175（S13）→ TRADE_WINDOW 模式 7 → C39／C191；op 44 空清單依 NPC 外型換頁。
// 只用合成封包、合成模板／技能表與記憶體送出器；不開遊戲、網路、視窗，也不讀客戶端素材。
static class SpellTradeTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Npc=456,Gold=903;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static byte[] Appear(uint id,ushort x,ushort gfx)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(gfx),[0,0,0,0],Le32(0),Le16(0),Str(id==Me?"offline":"target"),Str(""),[4],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static byte[] Hyper(uint id,string html)=>P([39],Le32(id),Str(html),Le16(0),Le16(0));
    // S15 欄位順序同 Inventory.ReadItem（同 ShopTradeTests.Item）。
    static byte[] Item(uint id,ushort icon,uint count,string name)=>P([15],Le32(id),Le16(0),[0,0],Le16(icon),[1],Le32(count),[0],Str(name),[0],new byte[11]);
    /// <summary>3.81 S41 S_SkillBuy：C(41) D(基價) H(n) n×D(索引)。</summary>
    static byte[] BuyList(uint basis,params uint[] indices)=>P([SpellTradeProtocol.SBuyList],Le32(basis),Le16((ushort)indices.Length),indices.SelectMany(Le32).ToArray());
    /// <summary>3.81 S41 空清單只送 H(0)（SS2§1.2）。</summary>
    static byte[] EmptyBuyList()=>P([SpellTradeProtocol.SBuyList],Le16(0));
    static byte[] ExchangeList(params uint[] indices)=>P([SpellExchangeProtocol.SList],Le16((ushort)indices.Length),indices.SelectMany(Le32).ToArray());
    static byte[] AddSkill(params int[] ids){var p=new byte[30];p[0]=164;p[1]=32;foreach(int id in ids)p[2+(id-1)/8]|=(byte)(1<<((id-1)%8));return LoginPackets.Pad(p);}

    static NpcTemplates Templates(){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
            ["npcmenu"]="<html>選單<br><a action=\"buyspell\">以金幣學魔法</a><a action=\"xchgspell\">以物品學魔法</a></html>",
        });
        return t;
    }
    // 合成技能表：技能 1、4（等級 1）、9（等級 2）、17（等級 3）；價格依據 NpcServices 的 SkillCatalog.Price。
    static SkillCatalog Catalog(){
        var s=(SkillCatalog)RuntimeHelpers.GetUninitializedObject(typeof(SkillCatalog));
        typeof(SkillCatalog).GetField("entries",F)!.SetValue(s,new Dictionary<int,SkillTemplate>{[1]=new(1,"初級治癒術",1),[4]=new(4,"光箭",1),[9]=new(9,"中級治癒術",2),[17]=new(17,"極光雷電",3)});
        return s;
    }
    static LoginConnection Seed(out List<byte[]> sent,ushort npcGfx=1110){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,0),"offline");c.World.Receive(Appear(Npc,32601,npcGfx),"offline");
        c.World.SelectTarget(Npc);c.World.DialogTemplates=Templates();c.World.SkillCatalog=Catalog();
        c.World.Receive(LoginPackets.Pad([5,0]),"offline");c.World.Receive(Item(Gold,5,12345,"$4"),"offline");
        var log=new List<byte[]>();sent=log;
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();
    static void OpenMenu(LoginConnection c){c.World.BeginTalk();c.World.Receive(Hyper(Npc,"npcmenu"),"offline");}
    static bool Choose(LoginConnection c,string action){var w=c.World;int id=w.InteractionPage!.Options.Single(o=>o.Value==action).Id;return Run(c.Interact("option",w.InteractionRevision,w.WorldEpoch,w.TargetRevision,id));}
    static string Hex(byte[] b)=>Convert.ToHexString(b);

    [WorldModuleTest("spell-trade")]
    static void Tests(WorldModuleTestContext t){
        Protocol(t);GoldList(t);GoldEmpty(t);ItemList(t);Composite(t);
    }

    // 1. 註冊、單價公式、版本奇偶、S41 解碼。
    static void Protocol(WorldModuleTestContext t){
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e.Kind=="observe"&&e.Opcode==41&&e.Subtype==-1&&e.Method=="WorldSession.ObserveSpellBuyList"),"S41 observer registered (M7-A)");
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetSpellBuy"),"spell-buy reset registered");
        t.Check(!entries.Any(e=>e.Kind=="receive"&&e.Opcode==41),"S41 not consumed by an extension (NpcServices keeps parsing it)");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);
        // 單價＝基價×((索引>>3)+1)²（TWP§1.1）。
        t.Equal(6000u,SpellTradeProtocol.BuyPrice(6000,0),"index 0 → level 1");
        t.Equal(6000u,SpellTradeProtocol.BuyPrice(6000,7),"index 7 → level 1");
        t.Equal(24000u,SpellTradeProtocol.BuyPrice(6000,8),"index 8 → level 2");
        t.Equal(54000u,SpellTradeProtocol.BuyPrice(6000,16),"index 16 → level 3");
        t.Equal(uint.MaxValue,SpellTradeProtocol.BuyPrice(int.MaxValue,255),"overflow clamps");
        t.Check(!SpellTradeProtocol.IsExchangeRevision(SpellTradeProtocol.BuyRevision(5))&&SpellTradeProtocol.IsExchangeRevision(SpellTradeProtocol.ExchangeRevision(5)),"op 44 even, op 175 odd");
        t.Check(SpellTradeProtocol.BuyRevision(5)!=SpellTradeProtocol.BuyRevision(6)&&SpellTradeProtocol.ExchangeRevision(5)!=SpellTradeProtocol.ExchangeRevision(6),"revisions stay distinct");
        t.Check(SpellTradeProtocol.TryDecodeBuyList(BuyList(6000,0,8)) is {Basis:6000} d&&d.Indices.SequenceEqual([0,8]),"S41 decoded");
        t.Check(SpellTradeProtocol.TryDecodeBuyList(EmptyBuyList()) is {Indices.Length:0},"3.81 empty S41 (H(0) only) decodes as empty");
        t.Check(SpellTradeProtocol.TryDecodeBuyList(BuyList(6000,3,3))==null,"duplicate index → null (main parser reports it)");
        t.Check(SpellTradeProtocol.TryDecodeBuyList(P([41],Le32(6000),Le16(3),Le32(1),Le32(2)))==null,"truncated → null (15 bytes pad to 16; third D runs past the end)");
        t.Check(SpellTradeProtocol.TryDecodeBuyList(ExchangeList(1))==null,"other opcode → null");
        // 空清單頁依外型（SS2§1.2）。
        t.Equal("GerengEv3",WorldSession.SpellBuyEmptyPage(237),"gfx 237 → GerengEv3");
        t.Equal("Sedia1",WorldSession.SpellBuyEmptyPage(2820),"gfx 2820 → Sedia1");
        t.Equal("Siriss1",WorldSession.SpellBuyEmptyPage(1110),"other gfx → Siriss1");
        t.Equal("Siriss1",WorldSession.SpellBuyEmptyPage(null),"unknown gfx → Siriss1");
    }

    // 2. op 44：buyspell → C145 → S41 → 模式 7（顯示單價、TOTAL）→ 左鈕送 C39。
    static void GoldList(WorldModuleTestContext t){
        using var c=Seed(out var sent);var w=c.World;var src=new SpellTradeSource(c,()=>false);
        OpenMenu(c);t.Check(src.Current==null,"dialog only → no window");
        t.Check(Choose(c,"buyspell"),"buyspell sends C145");
        t.Equal(Hex(ServicePackets.SkillList(Npc)),Hex(sent.Single()),"C145 = C(145) D(npc)");
        c.World.Receive(AddSkill(4),"offline");
        w.Receive(BuyList(6000,0,3,8,16),"offline");
        t.Equal("skills",w.InteractionKind,"S41 still parsed by NpcServices");
        var view=src.Current;
        t.Check(view is {Mode:TradeWindowPanel.ModeSpell,ShowPrice:true,CanSubmit:true,Holding:12345},"S41 → TRADE_WINDOW mode 7, price shown, holding = adena",view);
        t.Check(view!=null&&!SpellTradeProtocol.IsExchangeRevision(view.Revision)&&view.Revision==SpellTradeProtocol.BuyRevision(w.InteractionRevision),"op 44 revision tied to the interaction");
        t.Equal("0,3,8,16",string.Join(",",view!.Items.Select(i=>i.Id)),"rows = server indices in order");
        t.Equal("6000,6000,24000,54000",string.Join(",",view.Items.Select(i=>i.Price)),"price = basis × ((index>>3)+1)²");
        t.Equal("初級治癒術,光箭,中級治癒術,極光雷電",string.Join(",",view.Items.Select(i=>i.Name)),"names from skill table (skillId = index + 1)");
        t.Equal("1,0,1,1",string.Join(",",view.Items.Select(i=>i.Maximum)),"quantity limit 1; learned skill 4 disabled");
        t.Check(view.Items[1] is {Enabled:false}&&view.Items.All(i=>i.Icon==0),"learned row disabled; no item icon");
        // 舊路徑仍驗證：已學、數量 ≠ 1、版本不符都不送。
        src.Submit([new(3,1)],view.Revision);t.Equal(1,sent.Count,"learned skill never sent");
        src.Submit([new(0,2)],view.Revision);t.Equal(1,sent.Count,"count 2 never sent");
        src.Submit([new(0,1)],view.Revision+2);t.Equal(1,sent.Count,"stale revision never sent");
        src.Submit([new(0,1)],view.Revision|1);t.Equal(1,sent.Count,"op 175 revision with no item list never sent");
        src.Submit([new(0,1),new(8,1)],view.Revision);
        t.Equal(2,sent.Count,"one C39");
        t.Equal("27020000000000080000000000000000",Hex(sent[^1]),"C39 = C(39) H(2) D(0) D(8), padded");
        t.Equal(Hex(ServicePackets.Learn([new(0,1),new(8,1)])),Hex(sent[^1]),"same packet as the legacy list");
        t.Equal("submitted",w.InteractionKind,"interaction submitted");
        t.Check(src.Current==null,"window closes after submit");
        src.Submit([new(16,1)],view.Revision);t.Equal(2,sent.Count,"no resend");
        // 重複的 S41（沒有新的 C145）不再開窗。
        w.Receive(BuyList(6000,0),"offline");t.Check(src.Current==null,"unsolicited S41 opens nothing");
        // 關閉：CANCEL／X 結束對話，不送封包。
        w.CloseInteraction();OpenMenu(c);Choose(c,"buyspell");w.Receive(BuyList(100,0),"offline");
        t.Check(src.Current is {Items.Length:1},"second list");int n=sent.Count;
        src.Close();t.Equal("closed",w.InteractionKind,"close ends the interaction");t.Equal(n,sent.Count,"close sends nothing");t.Check(src.Current==null,"no view after close");
        // Reset 清掉基價綁定。
        OpenMenu(c);Choose(c,"buyspell");w.Receive(BuyList(100,0),"offline");w.Reset();t.Check(src.Current==null,"reset clears the window");
    }

    // 3. op 44 空清單：依 NPC 外型開頁；NpcServices 不再蓋成空的學魔法清單。
    static void GoldEmpty(WorldModuleTestContext t){
        foreach(var (gfx,page) in new[]{((ushort)237,"GerengEv3"),((ushort)2820,"Sedia1"),((ushort)1110,"Siriss1")}){
            using var c=Seed(out var sent,gfx);var w=c.World;var src=new SpellTradeSource(c,()=>false);
            OpenMenu(c);Choose(c,"buyspell");
            w.Receive(EmptyBuyList(),"offline");
            t.Check(w.InteractionKind=="dialog"&&w.InteractionPage?.Text=="缺少此對話的原始繁中模板："+page,$"empty S41 at gfx {gfx} opens {page}",w.InteractionPage?.Text);
            t.Check(src.Current==null&&w.ShopRows.Length==0,$"gfx {gfx}: no window, no rows");
            w.Receive(BuyList(6000,0),"offline");t.Equal("dialog",w.InteractionKind,$"gfx {gfx}: later S41 does not replace the page");
        }
        // 基價不為 0 的空清單（D(基價) H(0)）也一樣。
        using(var c=Seed(out _)){var w=c.World;OpenMenu(c);Choose(c,"buyspell");w.Receive(BuyList(6000),"offline");
            t.Equal("缺少此對話的原始繁中模板：Siriss1",w.InteractionPage?.Text,"D(6000) H(0) also opens the page");}
        // 沒要求清單時的空 S41 不換頁。
        using(var c=Seed(out _)){var w=c.World;OpenMenu(c);string before=w.InteractionPage!.Text;w.Receive(EmptyBuyList(),"offline");
            t.Equal(before,w.InteractionPage!.Text,"unrequested empty S41 keeps the page");}
        // 格式錯誤：觀察器不動作，主分派照舊丟例外。
        using(var c=Seed(out _)){var w=c.World;OpenMenu(c);Choose(c,"buyspell");
            t.Throws<ProtocolException>(()=>w.Receive(BuyList(6000,3,3),"offline"),"bad S41 surfaces protocol error");
            t.Equal("waiting",w.InteractionKind,"bad S41 changes nothing");}
    }

    // 4. op 175：S13 → 模式 7（不顯示單價、無 TOTAL，已學的不列）→ 左鈕送 C191。
    static void ItemList(WorldModuleTestContext t){
        using var c=Seed(out var sent,834);var w=c.World;var src=new SpellTradeSource(c,()=>false);
        OpenMenu(c);t.Check(Choose(c,"xchgspell"),"xchgspell sends C245");
        w.Receive(AddSkill(10),"offline");
        w.Receive(ExchangeList(5,9,12),"offline");
        var view=src.Current;
        t.Check(view is {Mode:TradeWindowPanel.ModeSpell,ShowPrice:false,Holding:null,CanSubmit:true},"S13 → TRADE_WINDOW mode 7, no price, no total",view);
        t.Check(view!=null&&SpellTradeProtocol.IsExchangeRevision(view.Revision),"op 175 revision odd");
        t.Equal("5,12",string.Join(",",view!.Items.Select(i=>i.Id)),"already learned skill 10 (index 9) skipped (SS2§1.2)");
        t.Check(view.Items.All(i=>i is {Price:0,Maximum:1,Enabled:true,Icon:0}),"rows: price 0, limit 1");
        int n=sent.Count;
        src.Submit([new(5,1)],view.Revision^1);t.Equal(n,sent.Count,"op 44 revision never sends C191");
        src.Submit([new(99,1)],view.Revision);t.Equal(n,sent.Count,"index not in list never sent");
        src.Submit([new(5,1),new(12,1)],view.Revision);
        t.Equal(n+1,sent.Count,"one C191");
        t.Equal("BF020005000000"+"0C000000"+"0000000000",Hex(sent[^1]),"C191 = C(191) H(2) D(5) D(12), padded");
        t.Equal("submitted",w.SpellExchangePhase,"phase submitted");
        t.Check(src.Current==null,"window closes after submit (one-time ticket)");
        // 關閉：CANCEL／X 關清單，不送封包。
        w.Receive(ExchangeList(7),"offline");t.Check(src.Current is {Items.Length:1},"new list");n=sent.Count;
        src.Close();t.Equal("closed",w.SpellExchangePhase,"close clears the offer");t.Equal(n,sent.Count,"close sends nothing");t.Check(src.Current==null,"no view after close");
        // 不在世界中不給內容。
        Set(c,"State",LoginState.Faulted);w.Receive(ExchangeList(8),"offline");t.Check(src.Current==null,"not in world → no window");
    }

    // 5. 與面板、其他來源共用同一個窗：學魔法優先；模式 7 左鈕 BUY。
    static void Composite(WorldModuleTestContext t){
        using var c=Seed(out var sent,834);var w=c.World;var spell=new SpellTradeSource(c,()=>false);
        var shop=new ShopTradeSource(c,s=>s,()=>false);var both=new TradeWindowSources(spell,shop);
        t.Check(both.Current==null,"nothing to show");
        w.Receive(ExchangeList(2),"offline");
        t.Check(both.Current is {Mode:TradeWindowPanel.ModeSpell,ShowPrice:false},"composite shows the spell list");
        both.Close();t.Equal("closed",w.SpellExchangePhase,"composite close forwarded to spell source");
        // 忙碌（走路中）不送。
        w.Receive(ExchangeList(2),"offline");var busy=new SpellTradeSource(c,()=>true);var v=busy.Current!;int n=sent.Count;
        busy.Submit([new(2,1)],v.Revision);t.Equal(n,sent.Count,"busy → nothing sent");
    }
}
