using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
namespace P06;

// R2-F（T261／#622）NPC 對話面板收尾的離線測試：城堡頁數字框（C19／C56／C44）、拿掉「[城堡]」聊天行、web 的 S136／城堡數字框指令、
// exitghost 接到 NPC 頁與伺服器頁動作、xchgspell／buyspell 依 NPC 外型 834 分流、S13 空清單開 HorunEv3。
// 只用合成封包、合成模板與記憶體送出器；不開遊戲、網路、視窗，也不讀客戶端素材。
static class NpcPageTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Npc=456;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    // S87（欄位同 AmountPromptTests.Appear）；flags 0x80＝幽靈（只用在自己）。
    static byte[] Appear(uint id,ushort x,ushort gfx,byte flags=0)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(gfx),[0,0,0,0],Le32(0),Le16(0),Str(id==Me?"offline":"target"),Str(""),[flags],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    // 3.81 S_HYPERTEXT：C(39) D(objid) S(htmlid) H(flag) H(count) S…
    static byte[] Hyper(uint id,string html)=>P([39],Le32(id),Str(html),Le16(0),Le16(0));
    static byte[] TaxRate(uint id)=>P([CastleProtocol.STaxRate],Le32(id),[10,50]);
    static byte[] Deposit(uint id)=>P([CastleProtocol.SDeposit],Le32(id));
    static byte[] Drawal(uint id,uint count)=>P([CastleProtocol.SDrawal],Le32(id),Le32(count));
    static byte[] Amount(uint id,int max,string cmd)=>P([AmountPromptProtocol.SInputAmount],Le32(id),Le32(0),Le32(0),Le32(0),Le32((uint)max),Le16(0),Str("request"),Str(cmd));
    static byte[] SpellList(params uint[] indices)=>P([SpellExchangeProtocol.SList],Le16((ushort)indices.Length),indices.SelectMany(Le32).ToArray());

    static NpcTemplates Templates(){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
            ["npcmenu"]="<html>選單<br><a action=\"exitghost\">離開幽靈</a><a action=\"xchgspell\">以物品學魔法</a><a action=\"buyspell\">以金幣學魔法</a><a action=\"hello\">一般動作</a></html>",
            ["horunev3"]="<html>沒有可以學習的魔法。</html>",
            ["ghostpage"]="<html>伺服器頁<br><a action=\"exitghost\">離開幽靈</a></html>",
            ["tax"]="<html>稅率設定 <var src=\"#0\"></html>",
        });
        return t;
    }
    static LoginConnection Seed(out List<byte[]> sent,ushort npcGfx=1110,bool ghost=false){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,0,(byte)(ghost?0x84:0x04)),"offline");c.World.Receive(Appear(Npc,32601,npcGfx),"offline");
        c.World.SelectTarget(Npc);c.World.DialogTemplates=Templates();
        var log=new List<byte[]>();sent=log;
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);log.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        return c;
    }
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();
    static void OpenMenu(LoginConnection c){c.World.BeginTalk();c.World.Receive(Hyper(Npc,"npcmenu"),"offline");}
    static int OptionId(WorldSession w,string action)=>w.InteractionPage!.Options.Single(o=>o.Value==action).Id;
    static bool Choose(LoginConnection c,string action){var w=c.World;return Run(c.Interact("option",w.InteractionRevision,w.WorldEpoch,w.TargetRevision,OptionId(w,action)));}
    static string Hex(byte[] b)=>Convert.ToHexString(b);

    [WorldModuleTest("npc-page")]
    static void Tests(WorldModuleTestContext t){
        Castle(t);AmountWeb(t);WebCommands(t);ExitGhost(t);SpellActions(t);SpellEmpty(t);
    }

    // 1. 城堡頁：數字框只在頁開著時能送；範圍、版本、取消；不寫聊天區。
    static void Castle(WorldModuleTestContext t){
        using var c=Seed(out var sent);var w=c.World;
        t.Check(w.Ready&&w.Actor?.ObjectId==Me,"seed: own actor ready");
        w.Receive(TaxRate(Me),"offline");
        var o=w.CastlePageOffer;
        t.Check(o is {Kind:CastleOfferKind.TaxRate,Min:10,Max:50},"S185 tax page carries the numeric box offer",o);
        t.Equal(10L,CastleProtocol.Initial(o!),"tax box initial = min 10 (3.81 sends no third C, SS2§8)");
        t.Equal("稅率 10%～50%",CastleProtocol.Range(o!),"tax range text");
        int epoch=w.WorldEpoch,page=w.QueryRevision;
        t.Check(!Run(c.SendCastlePageReply(epoch+1,page,o!.Revision,30)),"stale world epoch not sent");
        t.Check(!Run(c.SendCastlePageReply(epoch,page+1,o.Revision,30)),"stale page revision not sent");
        t.Check(!Run(c.SendCastlePageReply(epoch,page,o.Revision+1,30)),"stale offer revision not sent");
        t.Check(!Run(c.SendCastlePageReply(epoch,page,o.Revision,9))&&!Run(c.SendCastlePageReply(epoch,page,o.Revision,51)),"tax outside 10..50 not sent");
        t.Equal(0,sent.Count,"nothing sent for invalid replies");
        t.Check(Run(c.SendCastlePageReply(epoch,page,o.Revision,30)),"valid tax reply sent");
        t.Equal(1,sent.Count,"exactly one packet");
        t.Equal(Hex(CastleProtocol.TaxRate(Me,30)),Hex(sent[0]),"C19 = C(19) D(self) C(30)");
        t.Check(w.CastlePageOffer==null&&w.QueryPage==null,"page and box closed after send");
        t.Check(!Run(c.SendCastlePageReply(epoch,page,o.Revision,30))&&sent.Count==1,"no second reply for the same offer");
        t.Equal(CastleProtocol.Sent(CastleOfferKind.TaxRate,30),w.CastleNotice,"sent notice kept in castle snapshot");

        // 存入：頁被使用者關掉後，頁內數字框不能再送（詢問本身仍在，原服票自然失效）。
        w.Receive(Deposit(Me),"offline");
        var dep=w.CastlePageOffer!;t.Check(dep is {Kind:CastleOfferKind.Deposit,Min:1,Max:int.MaxValue},"S4 deposit box 1..int.MaxValue",dep);
        page=w.QueryRevision;w.CloseQueryPage();
        t.Check(w.CastlePageOffer==null&&w.CastleOffer!=null,"closed page hides the box, offer remains");
        t.Check(!Run(c.SendCastlePageReply(epoch,page,dep.Revision,100)),"closed page cannot send");
        // 提領：取消鈕不送封包、關頁。
        w.Receive(Drawal(Me,5000),"offline");
        var wd=w.CastlePageOffer!;page=w.QueryRevision;
        t.Check(wd is {Kind:CastleOfferKind.Withdraw,Max:5000},"S141 withdraw box capped at treasury",wd);
        t.Check(!c.CancelCastlePage(epoch,page+1,wd.Revision),"cancel with stale page refused");
        t.Check(c.CancelCastlePage(epoch,page,wd.Revision),"cancel accepted");
        t.Check(w.CastleOffer==null&&w.QueryPage==null&&sent.Count==1,"cancel closes page without sending");
        // 提領 0：只有頁，沒有數字框。
        w.Receive(Drawal(Me,0),"offline");
        t.Check(w.CastlePage!=null&&w.CastlePageOffer==null,"empty treasury page has no numeric box");
        // 提領頁送出 C44。
        w.Receive(Drawal(Me,5000),"offline");wd=w.CastlePageOffer!;
        t.Check(Run(c.SendCastlePageReply(w.WorldEpoch,w.QueryRevision,wd.Revision,5000)),"withdraw at max sent");
        t.Equal(Hex(CastleProtocol.Withdraw(Me,5000)),Hex(sent[^1]),"C44 bytes");
        t.Equal(0,w.PlayerChatLines.Length,"no [城堡] chat line for S185/S4/S141 or replies");
        w.Receive(P([CastleProtocol.SCastleMaster,4],Le32(Me)),"offline");
        t.Equal(0,w.PlayerChatLines.Length,"no [城堡] chat line for S69");
        t.Check(w.CastleNotice.Contains("奇巖城"),"S69 notice stays in castle snapshot",w.CastleNotice);
    }

    // 2. web 的 S136 數字框：版本（世界、對話頁、詢問）全部要對。
    static void AmountWeb(WorldModuleTestContext t){
        using var c=Seed(out var sent);var w=c.World;
        w.BeginTalk();w.Receive(Amount(Npc,10,"make1"),"offline");
        var o=w.AmountPromptCurrent!;int epoch=w.WorldEpoch,page=w.InteractionRevision;
        t.Check(o is {Lower:1,Max:10},"S136 prompt current",o);
        t.Check(!Run(c.SendAmountPromptReply(epoch+1,page,o.Revision,3)),"stale epoch not sent");
        t.Check(!Run(c.SendAmountPromptReply(epoch,page+1,o.Revision,3)),"stale dialog page not sent");
        t.Check(!Run(c.SendAmountPromptReply(epoch,page,o.Revision+1,3)),"stale prompt not sent");
        t.Check(!Run(c.SendAmountPromptReply(epoch,page,o.Revision,11)),"value above max not sent");
        t.Equal(0,sent.Count,"nothing sent yet");
        t.Check(Run(c.SendAmountPromptReply(epoch,page,o.Revision,3)),"valid amount sent");
        t.Equal(Hex(AmountPromptProtocol.Reply(Npc,3,"make1")),Hex(sent.Single()),"C11 bytes");
        t.Check(!Run(c.SendAmountPromptReply(epoch,page,o.Revision,3))&&sent.Count==1,"no second reply");
    }

    // 3. web 指令格式。
    static void WebCommands(WorldModuleTestContext t){
        NpcWebCommand Parse(string json)=>NpcWebCommand.Parse(JsonDocument.Parse(json).RootElement);
        var a=Parse("{\"v\":1,\"id\":2,\"cmd\":\"npcAmount\",\"revision\":7,\"epoch\":1,\"targetRevision\":3,\"prompt\":5,\"value\":9}");
        t.Check(a is {Command:"amount",Revision:7,Epoch:1,Prompt:5,Value:9},"npcAmount parsed",a);
        var k=Parse("{\"v\":1,\"id\":2,\"cmd\":\"npcCastle\",\"revision\":4,\"epoch\":1,\"targetRevision\":0,\"prompt\":8,\"value\":30}");
        t.Check(k is {Command:"castle",Revision:4,Prompt:8,Value:30},"npcCastle parsed",k);
        var x=Parse("{\"v\":1,\"id\":2,\"cmd\":\"npcCastleCancel\",\"revision\":4,\"epoch\":1,\"targetRevision\":0,\"prompt\":8}");
        t.Check(x is {Command:"castle-cancel",Prompt:8,Value:0},"npcCastleCancel parsed",x);
        t.Throws<FormatException>(()=>Parse("{\"v\":1,\"id\":2,\"cmd\":\"npcAmount\",\"revision\":7,\"epoch\":1,\"targetRevision\":3,\"prompt\":5}"),"npcAmount requires value");
        t.Throws<FormatException>(()=>Parse("{\"v\":1,\"id\":2,\"cmd\":\"npcAmount\",\"revision\":7,\"epoch\":1,\"targetRevision\":3,\"prompt\":5,\"value\":0}"),"npcAmount value 0 rejected");
        t.Throws<FormatException>(()=>Parse("{\"v\":1,\"id\":2,\"cmd\":\"npcCastle\",\"revision\":7,\"epoch\":1,\"targetRevision\":3,\"prompt\":5,\"value\":2147483648}"),"npcCastle value above int.MaxValue rejected");
        t.Throws<FormatException>(()=>Parse("{\"v\":1,\"id\":2,\"cmd\":\"npcCastleCancel\",\"revision\":7,\"epoch\":1,\"targetRevision\":3,\"prompt\":0}"),"prompt 0 rejected");
        t.Throws<FormatException>(()=>Parse("{\"v\":1,\"id\":2,\"cmd\":\"npcCastleCancel\",\"revision\":7,\"epoch\":1,\"targetRevision\":3,\"prompt\":5,\"value\":1}"),"npcCastleCancel rejects extra value");
    }

    // 4. exitghost：NPC 頁與伺服器頁的動作都交給 R2-D 的 TryHyperAction；不送 C125。
    static void ExitGhost(WorldModuleTestContext t){
        using(var c=Seed(out var sent,ghost:true)){
            var w=c.World;OpenMenu(c);
            t.Check(w.IsGhost&&w.InteractionKind=="dialog","seed: ghost in NPC dialog");
            t.Check(Choose(c,"exitghost"),"exitghost option sends while ghost");
            t.Equal(Hex(GhostModePackets.Exit()),Hex(sent.Single()),"NPC page exitghost → C173 only");
            t.Equal("dialog",w.InteractionKind,"dialog not put into waiting by a client-side action");
            t.Check(!Choose(c,"exitghost")&&sent.Count==1,"repeat while awaiting server sends nothing");
        }
        using(var c=Seed(out var sent)){
            OpenMenu(c);
            t.Check(!Choose(c,"exitghost")&&sent.Count==0,"exitghost when not a ghost: nothing sent (no C125)");
            t.Check(Choose(c,"hello")&&Hex(sent.Single())==Hex(NpcPackets.Action(Npc,"hello")),"ordinary action still sends C125");
        }
        using(var c=Seed(out var sent,ghost:true)){
            var w=c.World;w.Receive(Hyper(Me,"ghostpage"),"offline");
            t.Check(w.QueryVisible&&w.QueryActionable,"server page shown");
            t.Check(Run(c.QueryOption(0,w.QueryRevision,w.WorldEpoch)),"server page exitghost sends");
            t.Equal(Hex(GhostModePackets.Exit()),Hex(sent.Single()),"server page exitghost → C173 only");
            t.Check(w.QueryPage==null,"server page closed after send");
        }
        using(var c=Seed(out var sent)){
            var w=c.World;w.Receive(Hyper(Me,"ghostpage"),"offline");
            t.Check(!Run(c.QueryOption(0,w.QueryRevision,w.WorldEpoch))&&sent.Count==0&&w.QueryPage!=null,"server page exitghost outside ghost: nothing sent, page kept");
        }
    }

    // 5. xchgspell／buyspell 依 NPC 外型 834 分流（SS2§1.1）。
    static void SpellActions(WorldModuleTestContext t){
        t.Check(SpellExchangeProtocol.ActionAllowed("xchgspell",834)&&!SpellExchangeProtocol.ActionAllowed("xchgspell",1110),"xchgspell only for gfx 834");
        t.Check(!SpellExchangeProtocol.ActionAllowed("buyspell",834)&&SpellExchangeProtocol.ActionAllowed("buyspell",1110),"buyspell only for gfx ≠ 834");
        t.Check(SpellExchangeProtocol.ActionAllowed("hello",834),"other actions unaffected");
        using(var c=Seed(out var sent,npcGfx:834)){
            var w=c.World;OpenMenu(c);
            t.Equal<ushort?>(834,w.InteractionNpcGfx,"dialog NPC gfx");
            t.Check(!Choose(c,"buyspell")&&sent.Count==0,"buyspell at gfx 834 sends nothing");
            t.Check(Choose(c,"xchgspell"),"xchgspell at gfx 834 sends");
            t.Equal(Hex(SpellExchangeProtocol.RequestList(Npc)),Hex(sent.Single()),"C245 = C(245) D(npc)");
            t.Equal("waiting",w.SpellExchangePhase,"spell exchange waits for S13");
        }
        using(var c=Seed(out var sent)){
            var w=c.World;OpenMenu(c);
            t.Check(!Choose(c,"xchgspell")&&sent.Count==0,"xchgspell at other gfx sends nothing");
            t.Check(Choose(c,"buyspell"),"buyspell at other gfx sends");
            t.Equal(Hex(ServicePackets.SkillList(Npc)),Hex(sent.Single()),"buyspell keeps the existing gold skill-list request");
        }
    }

    // 6. S13 空清單開 HorunEv3；有清單不換頁（清單交給學魔法窗）。
    static void SpellEmpty(WorldModuleTestContext t){
        using(var c=Seed(out var sent,npcGfx:834)){
            var w=c.World;OpenMenu(c);Choose(c,"xchgspell");
            w.Receive(SpellList(),"offline");
            t.Equal("empty",w.SpellExchangePhase,"empty list phase");
            t.Check(w.InteractionKind=="dialog"&&w.InteractionPage?.Text=="沒有可以學習的魔法。","empty S13 opens HorunEv3 page in the NPC dialog",w.InteractionPage?.Text);
        }
        using(var c=Seed(out var sent,npcGfx:834)){
            var w=c.World;OpenMenu(c);Choose(c,"xchgspell");string before=w.InteractionPage!.Text;
            w.Receive(SpellList(3,8),"offline");
            t.Check(w.SpellExchangePhase=="list"&&w.SpellExchangeOffer?.Indices.Length==2,"non-empty list offered");
            t.Equal(before,w.InteractionPage!.Text,"non-empty list keeps the dialog page");
        }
        using(var c=Seed(out var sent,npcGfx:834)){
            var w=c.World;w.Receive(SpellList(),"offline");
            t.Equal("closed",w.InteractionKind,"unrequested empty S13 opens no page");
        }
    }
}
