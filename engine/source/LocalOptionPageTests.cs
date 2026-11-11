using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
namespace P06;

// R13-A（T442／#1021，gap-matrix 15.15.5／15.15.6，列 1:144）離線測試：選單開的本機選項頁（LocalOptionPage.cs、
// NpcInteraction.cs 的本機頁模式）。只用自寫的合成模板、記憶體內的 session 與記憶體送出器；不讀客戶端素材、不開遊戲／網路／視窗。
// 由 --module-tests 執行（群組 local-option-page）。
static class LocalOptionPageTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    static void Set(object o,string name,object? value){
        for(var t=o.GetType();t!=null;t=t.BaseType){
            if(t.GetProperty(name,F|BindingFlags.DeclaredOnly) is {} p){p.SetValue(o,value);return;}
            if(t.GetField(name,F|BindingFlags.DeclaredOnly) is {} f){f.SetValue(o,value);return;}
        }
        throw new MissingMemberException(o.GetType().Name,name);
    }
    static NpcTemplates Templates(Dictionary<string,string> rows){
        var t=(NpcTemplates)RuntimeHelpers.GetUninitializedObject(typeof(NpcTemplates));
        typeof(NpcTemplates).GetField("templates",F)!.SetValue(t,new Dictionary<string,string>(rows,StringComparer.OrdinalIgnoreCase));
        return t;
    }
    // 結構仿 option1／11／12／13-c.html（只取連結與動作的寫法；文字是自寫的，不是客戶端檔案）。
    static readonly Dictionary<string,string> Rows=new(){
        ["option1"]="<html>選項<br><a link=\"option11\">環境</a><br><a link=\"option12\">社群</a><br><a link=\"option13\">指令</a><br><a link=\"nopage\">沒有的頁</a><br><a link=\"help\">說明</a></html>",
        ["option11"]="<html>HP/MP <a action=\"showhpmp\"><var src=\"#1023\"></a> 浮動 <a action=\"showfloathpmp\"><var src=\"#1024\"></a><br><img src=\"1\" link=\"option1\"></html>",
        ["option12"]="<html>泡泡 <a action=\"bubbleon\"><var src=\"#1003\"></a> 商店 <a action=\"showMargetMsg\"><var src=\"#1027\"></a><br>"+
            "密語 <a action=\"dowhisper\"><var src=\"#1005\"></a> 買賣 <a action=\"dotradechat\"><var src=\"#1020\"></a> 聊天 <a action=\"dochat\">切換</a> 拒絕 <a action=\"cPartyReject\">切換</a><br><img src=\"1\" link=\"option1\"></html>",
        ["option13"]="<html>交易 <a action=\"accepttrade\"><var src=\"#1019\"></a> 撿取 <a action=\"getall\"><var src=\"#1002\"></a> 分配 <a action=\"randomParty\">切換</a><br><img src=\"1\" link=\"option1\"></html>",
        ["help"]="<html>說明<br><a link=\"option1\">回選項</a></html>",
        ["optionb"]="<html>好友 <a action=\"addbuddy\">新增</a> <a action=\"delbuddy\">刪除</a> 同盟 <a action=\"alliancelist\">名單</a></html>",
        ["monlist"]="<html>變身<br><a action=\"polyA\">甲</a><a link=\"monlist2\">下一頁</a></html>",
        ["monlist2"]="<html>第二頁<br><a action=\"getall\">撿取</a></html>",
    };
    static LoginConnection Seed(){
        var c=new LoginConnection();var w=c.World;
        Set(c,"State",LoginState.InWorld);Set(w,"Entered",true);Set(w,"MapId",(ushort)0);Set(w,"TimeReplies",1);Set(w,"CacheEnabled",false);
        Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>($"{{\"ObjectId\":{Me},\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}}"));
        Set(w,"Actor",JsonSerializer.Deserialize<OwnActor>($"{{\"ObjectId\":{Me},\"Name\":\"測試者\",\"Gfx\":0,\"X\":32600,\"Y\":32940,\"HpPercent\":100}}"));
        w.DialogTemplates=Templates(Rows);
        return c;
    }
    static List<byte[]> Sender(LoginConnection c){
        var sent=new List<byte[]>();
        Func<byte[],Func<bool>,Action,Task<bool>> gate=(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);};
        Set(c,"sendMovementGate",gate);return sent;
    }
    static bool Run(Task<bool> t)=>t.GetAwaiter().GetResult();
    static int Option(WorldSession w,string value)=>w.QueryPage!.Options.First(o=>o.Value==value).Id;
    static bool Click(LoginConnection c,string value)=>Run(c.QueryOption(Option(c.World,value),c.World.QueryRevision,c.World.WorldEpoch));
    static bool NoC125(List<byte[]> sent)=>sent.All(p=>p[0]!=125);

    [WorldModuleTest("local-option-page")]
    static void Run(WorldModuleTestContext t){
        HeadBubbleOptions.SetCached(true);bool frames=HeadBubbleOptions.BubbleFrames;var hp=LocalOptionPage.HpMpPanel;var hpVars=NativeUi.HyperTextOptionVars.HpMp;
        try{Table(t);Links(t);Actions(t);ChatChannels(t);HpMp(t);Modes(t);}
        finally{HeadBubbleOptions.SetCached(null);HeadBubbleOptions.BubbleFrames=frames;LocalOptionPage.HpMpPanel=hp;NativeUi.HyperTextOptionVars.HpMp=hpVars;}
    }

    // 1. 動作表與 Prepare：已查證的 8 個可點；dochat／randomParty／cPartyReject 與不認得的停用並附說明；link 依模板判定。
    static void Table(WorldModuleTestContext t){
        foreach(var (name,act) in new[]{("accepttrade",LocalOptionAct.AcceptTrade),("getall",LocalOptionAct.GetAll),("bubbleon",LocalOptionAct.BubbleOn),("showMargetMsg",LocalOptionAct.ShowMarketMsg),
            ("showhpmp",LocalOptionAct.ShowHpMp),("showfloathpmp",LocalOptionAct.ShowFloatHpMp),("dowhisper",LocalOptionAct.Whisper),("dotradechat",LocalOptionAct.TradeChat)})
            t.Equal(act,LocalOptionPage.Of(name),name+" → 本機動作");
        t.Equal(LocalOptionAct.GetAll,LocalOptionPage.Of(" GetAll "),"大小寫與空白不影響");
        foreach(var name in new[]{"dochat","randomParty","cPartyReject","telcon","infracon","buy","checkpk","alliancelist","windowMode","volume",""})t.Equal(LocalOptionAct.None,LocalOptionPage.Of(name),name+" 不是本組（停用）");
        foreach(var name in new[]{"addbuddy","delbuddy","buddy","surrender","cease","pledgefocusadd"})t.Equal(LocalOptionAct.Client,LocalOptionPage.Of(name),name+" → 沿用引擎既有的客戶端處理");
        t.Equal(LocalOptionAct.None,LocalOptionPage.Of(null),"null → None");
        t.Equal(8,LocalOptionPage.Names.Count,"8 個已查證動作");
        var page=new NpcPage("x",[new(0,"a","link","option11",true),new(1,"b","link","nopage",false),new(2,"c","action","getall",true),new(3,"d","action","dochat",true),new(4,"e","action","getall",false)],"");
        var p=LocalOptionPage.Prepare(page,_=>true);
        t.Check(p.Options[0].Enabled&&!p.Options[1].Enabled,"link 照模板（存在才可點）");
        t.Check(p.Options[2].Enabled&&!p.Options[3].Enabled,"getall 可點、dochat 停用");
        t.Check(!p.Options[4].Enabled,"模板已停用的維持停用");
        t.Equal(LocalOptionPage.DisabledNotice,p.Limitation,"有停用的動作 → 說明");
        t.Equal("",LocalOptionPage.Prepare(new NpcPage("x",[new(0,"a","link","option11",true)],""),_=>true).Limitation,"沒有停用的動作 → 沒有說明");
        t.Check(!LocalOptionPage.Prepare(page,_=>false).Options[2].Enabled,"不可用（例如沒有 HP/MP 視窗）→ 停用");
    }

    // 2. 本機頁模式：link／img link 在本機換頁（仍是本機頁），不送封包；舊版本的點擊失效；沒有的頁不能點。
    static void Links(WorldModuleTestContext t){
        using var c=Seed();var w=c.World;var sent=Sender(c);
        w.ShowLocalOptionPage("option1");
        t.Check(w.QueryVisible&&w.QueryLocal&&w.QueryActionable&&w.QueryHtml=="option1","選單頁 → 本機頁模式");
        t.Equal(4,w.QueryPage!.Options.Count(o=>o.Enabled),"option11／12／13／help 可點");
        t.Check(!w.QueryPage.Options.First(o=>o.Value=="nopage").Enabled,"沒有模板的頁不能點");
        int rev=w.QueryRevision;
        t.Check(Click(c,"option11")&&w.QueryHtml=="option11"&&w.QueryLocal&&w.QueryRevision==rev+1,"<a link> → option11");
        t.Check(!Run(c.QueryOption(0,rev,w.WorldEpoch))&&w.QueryHtml=="option11","舊頁版本的點擊失效");
        t.Check(Click(c,"option1")&&w.QueryHtml=="option1","<img link> → 回 option1");
        t.Check(Click(c,"help")&&w.QueryHtml=="help"&&Click(c,"option1")&&w.QueryHtml=="option1","help ↔ option1");
        t.Check(Click(c,"option13")&&w.QueryHtml=="option13","option1 → option13");
        t.Equal(0,sent.Count,"換頁不送任何封包");
        t.Check(JsonSerializer.SerializeToElement(w.InteractionSnapshot()).GetProperty("query").GetProperty("actionable").GetBoolean(),"快照：本機頁可操作");
    }

    // 3. 本機動作：getall／accepttrade／bubbleon／showfloathpmp 執行後重畫同一頁（var 值跟著變）；停用的動作不送 C125。
    static void Actions(WorldModuleTestContext t){
        using var _t=ServerMessageText.UseTable(null);
        using var c=Seed();var w=c.World;var sent=Sender(c);
        using var vars=NativeUi.HyperTextOptionVars.Use(w);
        w.ShowLocalOptionPage("option13");
        t.Check(w.QueryPage!.Text.Contains("撿取 關"),"全部拾取預設關",w.QueryPage.Text);
        int rev=w.QueryRevision;
        t.Check(Click(c,"getall")&&w.PickupAll,"getall → 全部拾取開（byte_135C1E4）");
        t.Check(w.QueryHtml=="option13"&&w.QueryLocal&&w.QueryRevision==rev+1&&w.QueryPage.Text.Contains("撿取 開"),"重畫目前這頁，值已更新",w.QueryPage.Text);
        bool ask=w.TradeAskWindow;
        t.Check(Click(c,"accepttrade")&&w.TradeAskWindow!=ask,"accepttrade → 交易詢問取反（ToggleAcceptTrade）");
        t.Check(w.QueryHtml=="option13"&&w.QueryPage.Text.Contains("交易 "+(w.TradeAskWindow?"可使用":"無法使用")),"accepttrade 後重畫",w.QueryPage.Text);
        t.Check(NoC125(sent),"getall／accepttrade 不送 C125（accepttrade 只存角色設定）");
        int count=sent.Count;rev=w.QueryRevision;
        t.Check(!Click(c,"randomParty")&&sent.Count==count&&w.QueryRevision==rev,"randomParty 停用：不動作、不送");
        t.Check(!w.QueryPage.Options.First(o=>o.Value=="randomParty").Enabled&&w.QueryPage.Limitation.Contains(LocalOptionPage.DisabledNotice),"停用的照原樣顯示並附說明");

        t.Check(Click(c,"option1")&&Click(c,"option12"),"換到 option12");
        HeadBubbleOptions.BubbleFrames=false;
        t.Check(Click(c,"bubbleon")&&HeadBubbleOptions.BubbleFrames&&w.QueryPage.Text.Contains("泡泡 開"),"bubbleon → 泡泡話框開，重畫",w.QueryPage.Text);
        t.Check(Click(c,"bubbleon")&&!HeadBubbleOptions.BubbleFrames,"再點 → 關");
        count=sent.Count;
        t.Check(!Click(c,"dochat")&&!Click(c,"cPartyReject")&&sent.Count==count,"dochat／cPartyReject 停用（處理函式沒追完／引擎沒有設定）");
        t.Check(NoC125(sent),"本機頁全程不送 C125");

        // optionb：addbuddy 沿用 R10-C 的頁內名稱框（開在這一頁，不重畫、不送包）；alliancelist 停用。
        w.ShowLocalOptionPage("optionb");count=sent.Count;
        t.Check(Click(c,"addbuddy")&&w.HtmlNameInput is {Kind:HtmlNameKind.AddBuddy,Query:true}&&w.QueryHtml=="optionb"&&sent.Count==count,"addbuddy → 588 名稱框（本機頁上）");
        t.Check(!w.QueryPage!.Options.First(o=>o.Value=="alliancelist").Enabled&&!Click(c,"alliancelist")&&sent.Count==count,"alliancelist 停用");
        t.Check(NoC125(sent),"optionb 不送 C125");
    }

    // 4. dowhisper／dotradechat：同 /whisper、/tradechat（sub_F4A0A0／sub_F4A7B0）→ C26 type 2／6，之後重畫。
    static void ChatChannels(WorldModuleTestContext t){
        using var _t=ServerMessageText.UseTable(null);
        using var c=Seed();var w=c.World;var sent=Sender(c);
        using var vars=NativeUi.HyperTextOptionVars.Use(w);
        w.ShowLocalOptionPage("option12");
        t.Check(w.ChatChannels.Whisper&&w.ChatChannels.Trade,"前提：頻道預設開");
        t.Check(Click(c,"dowhisper")&&!w.ChatChannels.Whisper,"dowhisper → 密語關");
        t.Check(sent.Any(p=>p.SequenceEqual(ChatChannelPackets.Toggle(ChatChannelKind.Whisper,false))),"送 C26 type 2 off");
        t.Check(w.QueryHtml=="option12"&&w.QueryPage!.Text.Contains("密語 關"),"重畫，密語 關",w.QueryPage?.Text);
        t.Check(Click(c,"dotradechat")&&!w.ChatChannels.Trade&&sent.Any(p=>p.SequenceEqual(ChatChannelPackets.Toggle(ChatChannelKind.Trade,false))),"dotradechat → 買賣關，送 C26 type 6 off");
        t.Check(Click(c,"dowhisper")&&w.ChatChannels.Whisper&&sent.Any(p=>p.SequenceEqual(ChatChannelPackets.Toggle(ChatChannelKind.Whisper,true))),"再點 → 密語開");
        t.Check(NoC125(sent),"不送 C125");
    }

    // 5. showhpmp：要有原生 HP/MP 視窗；AttachHpMp 同時接上 var 1023／1024（gap-matrix 15.15.6 ③）。
    static void HpMp(WorldModuleTestContext t){
        using var _t=ServerMessageText.UseTable(null);
        LocalOptionPage.HpMpPanel=null;
        t.Check(!LocalOptionPage.Available(LocalOptionAct.ShowHpMp)&&!LocalOptionPage.ToggleShowHpMp(),"沒有 HP/MP 視窗 → showhpmp 停用");
        t.Check(LocalOptionPage.Available(LocalOptionAct.ShowFloatHpMp),"showfloathpmp 只重開頁，不需要視窗");
        using(var c=Seed()){
            var w=c.World;Sender(c);w.ShowLocalOptionPage("option11");
            t.Check(!w.QueryPage!.Options.First(o=>o.Value=="showhpmp").Enabled,"沒有視窗時頁上停用");
        }
        var panel=new NativeUi.HpMpPanel();
        LocalOptionPage.AttachHpMp(()=>panel);
        t.Check(NativeUi.HyperTextOptionVars.HpMp?.Invoke()==panel.Settings,"AttachHpMp → HyperTextOptionVars.HpMp 讀同一個視窗的設定");
        using(var c=Seed()){
            var w=c.World;var sent=Sender(c);
            using var vars=NativeUi.HyperTextOptionVars.Use(w,()=>panel.Settings);
            w.ShowLocalOptionPage("option11");
            t.Check(w.QueryPage!.Text.StartsWith("HP/MP 開",StringComparison.Ordinal),"1023 預設開",w.QueryPage.Text);
            t.Check(Click(c,"showhpmp")&&!panel.Settings.ShowCharacterHpMp&&w.QueryPage.Text.StartsWith("HP/MP 關",StringComparison.Ordinal),"showhpmp → 角色 HP/MP 條關（byte_138BE7C），重畫",w.QueryPage.Text);
            bool show=panel.Settings.Show;int rev=w.QueryRevision;
            t.Check(Click(c,"showfloathpmp")&&panel.Settings.Show==show&&w.QueryRevision==rev+1,"showfloathpmp：不改旗標，只重畫（sub_584B10）");
            t.Equal(0,sent.Count,"不送封包");
        }
    }

    // 6. 模式切換：玩家查詢結果、伺服器頁都不是本機頁；伺服器的空頁不關本機頁；關頁清掉模式。
    static void Modes(WorldModuleTestContext t){
        using var c=Seed();var w=c.World;var sent=Sender(c);
        w.ShowLocalOptionPage("option1");
        w.ShowQueryPage(Me,"option1",[],[]);
        t.Check(!w.QueryLocal&&!w.QueryActionable&&w.QueryPage!.Options.All(o=>!o.Enabled),"玩家查詢結果：選項照舊停用");
        w.ShowLocalOptionPage("option13");
        var close=LoginPackets.Pad([39,..BitConverter.GetBytes(Me),0,0,0,0,0]);
        w.Receive(close,"");
        t.Check(w.QueryVisible&&w.QueryLocal&&w.QueryHtml=="option13","伺服器的空頁不關本機頁");
        var hyper=LoginPackets.Pad([39,..BitConverter.GetBytes(Me),..LoginPackets.TextEncoding.GetBytes("monlist"),0,0,0,0,0]);
        w.Receive(hyper,"");
        t.Check(w.QueryVisible&&!w.QueryLocal&&w.QueryActionable&&w.QueryHtml=="monlist","伺服器頁取代本機頁 → 伺服器頁模式");
        t.Check(Click(c,"monlist2")&&!w.QueryLocal&&w.QueryHtml=="monlist2","伺服器頁的 link 照舊（不變成本機頁）");
        int before=sent.Count;
        Click(c,"getall");
        t.Check(sent.Count==before+1&&sent[^1].SequenceEqual(NpcPackets.Action(Me,"getall")),"伺服器頁上的 getall 照舊送 C125（本組只改本機頁）");
        w.ShowLocalOptionPage("option1");
        t.Check(w.CloseQueryPage()&&!w.QueryLocal&&!w.QueryVisible,"關頁 → 清掉本機頁模式");
        t.Check(!w.RedrawLocalOptionPage("option1",w.WorldEpoch),"關掉後不重畫");
    }
}
