using System.Reflection;
using System.Text;
namespace P06;

// R11-C（T417／#959）離線測試（名稱 buddy-hyperact，由 --module-tests 執行）：
//   好友 HyperAct（FU9§9.2 第 357 行）：`buddy`／`refreshbuddy` → 只送 C4；`delbuddy` → 589 輸入框 → C202＋C4；
//   `addbuddy` 588 照舊（R10-C）；不是本組的動作照舊送 C125。NPC 頁（ClientHyperAction）與伺服器主動頁（QueryOption）都測。
// 只用依 3.81 欄位合成的封包、合成字串表與記憶體送出閘門；不開遊戲、網路、DB、視窗，也不讀客戶端素材。
static class BuddyHyperActTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Npc=959;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Op(byte op,string name)=>P([op],Str(name));
    static byte[] Query=>LoginPackets.Pad([4]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static string Hex(List<byte[]> sent)=>string.Join(" ",sent.Select(Convert.ToHexString));
    static byte[] Dialog(uint id,string html)=>P([39],Le32(id),Str(html),Le16(0),Le16(0));
    static byte[] NpcAppear()=>P([87],Le16(32601),Le16(32940),Le32(Npc),Le16(1110),new byte[4],Le32(0),Le16(0),Str("NPC"),Str(""),[4],Le32(0),Str(""),Str(""),[0,255,0,0,0,255,255]);
    /// <summary>S39 "buddy"（3.81 S_Buddy）：D owner、S "buddy"、H 2、H 2、S 全部名單、S 在線名單。</summary>
    static byte[] BuddyList(uint owner,string all,string online)=>P([39],Le32(owner),Str("buddy"),Le16(2),Le16(2),Str(all),Str(online));
    static ServerStringTable Strings(params (int id,string text)[] rows){
        var lines=new string[3484];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(lines.Length+"\r\n"+string.Join("\r\n",lines)));
    }

    static LoginConnection SeedNpc(List<byte[]> sent){
        var c=new LoginConnection();c.SeedQ4("r11c-self",2,"",0,0,30,sent);
        c.World.Receive(NpcAppear(),"offline");c.World.SelectTarget(Npc);return c;
    }
    static bool OpenNpcPage(LoginConnection c,params (int id,string action)[] options){
        if(!c.World.CanStartInteraction)c.World.CloseInteraction();
        if(!Wait(c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision)))return false;
        c.World.Receive(Dialog(Npc,"r11c"),"offline");
        Set(c.World,"InteractionPage",new NpcPage("好友",options.Select(o=>new NpcOption(o.id,o.action,"action",o.action,true)).ToArray(),""));
        return c.World.InteractionKind=="dialog";
    }
    static bool Option(LoginConnection c,int id)=>Wait(c.Interact("option",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision,id));
    static bool OpenServerPage(LoginConnection c,params (int id,string action)[] options){
        c.World.Receive(Dialog(Q4FollowupTests.Own,"r11c-query"),"offline");
        Set(c.World,"QueryPage",new NpcPage("好友",options.Select(o=>new NpcOption(o.id,o.action,"action",o.action,true)).ToArray(),""));
        return c.World.QueryVisible&&c.World.QueryActionable;
    }
    static bool QueryOption(LoginConnection c,int id)=>Wait(c.QueryOption(id,c.World.QueryRevision,c.World.WorldEpoch));

    [WorldModuleTest("buddy-hyperact")]
    static void Run(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){
            Classify(t);NpcQuery(t);NpcDelete(t);ServerPage(t);NotOurs(t);Blocked(t);
        }
        using(ServerMessageText.UseTable(Strings((588,"T588"),(589,"T589"))))PromptFromTable(t);
    }

    // 1. 名稱分派與 589 原文。
    static void Classify(WorldModuleTestContext t){
        t.Check(BuddyHyperActs.Classify("buddy")==BuddyHyperAct.Query&&BuddyHyperActs.Classify(" RefreshBuddy ")==BuddyHyperAct.Query,"buddy／refreshbuddy → C4（去空白、不分大小寫）");
        t.Check(BuddyHyperActs.Classify("DelBuddy")==BuddyHyperAct.Delete&&BuddyHyperActs.Classify("addbuddy")==BuddyHyperAct.Add,"delbuddy → 589、addbuddy → 588");
        t.Check(BuddyHyperActs.Classify("buddylist") is null&&BuddyHyperActs.Classify("delbuddy 甲") is null&&BuddyHyperActs.Classify("") is null&&BuddyHyperActs.Classify(null) is null,"其他字串不是本組");
        t.Check(BuddyHyperActs.IsAddBuddy("addbuddy")&&!BuddyHyperActs.IsAddBuddy("delbuddy"),"IsAddBuddy 不變");
        t.Equal(589,HtmlActions.PromptText(HtmlNameKind.DelBuddy),"輸入框 589");
        t.Equal(588,HtmlActions.PromptText(HtmlNameKind.AddBuddy),"輸入框 588 不變");
        t.Equal(3361,HtmlActions.PromptText(HtmlNameKind.PledgeFocusAdd),"血盟關注 3361 不變");
        t.Equal("在好友清單上刪除誰？",BuddyHyperActs.DelInputFallback,"589 原文（0-客戶端 string-c.tbl sha256 4ebbc1a7…2d43e9）");
        t.Equal(BuddyHyperActs.DelInputFallback,BuddyHyperActs.DelInputPrompt,"沒有字串表 → 589 原文");
        t.Check(BuddyHyperActs.IsInput(HtmlNameKind.DelBuddy)&&BuddyHyperActs.IsInput(HtmlNameKind.AddBuddy)&&!BuddyHyperActs.IsInput(HtmlNameKind.Cease),"好友輸入框只有 588／589");
        t.Equal<string?>(null,HtmlActions.Normalize("delbuddy"),"R7-D 六個血盟動作不含好友");
    }

    // 2. NPC 頁 buddy／refreshbuddy → 只送 C4（不送 C125）；回覆 S39 buddy 更新名單。
    static void NpcQuery(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        t.Check(OpenNpcPage(c,(1,"buddy"),(2,"refreshbuddy"),(3," Buddy ")),"NPC 頁開啟");
        int count=sent.Count;
        t.Check(Option(c,1)&&sent.Count==count+1&&sent[^1].SequenceEqual(Query),"buddy → C4，不送 C125",Hex(sent));
        t.Check(c.World.InteractionKind=="dialog"&&c.World.HtmlNameInput==null,"頁維持、不開框");
        t.Check(Option(c,2)&&sent.Count==count+2&&sent[^1].SequenceEqual(Query),"refreshbuddy → C4",Hex(sent));
        t.Check(Option(c,3)&&sent.Count==count+3&&sent[^1].SequenceEqual(Query),"大小寫／空白不同也送 C4",Hex(sent));
        c.World.Receive(BuddyList(Q4FollowupTests.Own,"甲 乙 ","甲 "),"offline");
        t.Check(c.World.BuddyLoaded&&c.World.Buddies.Select(b=>b.Name).SequenceEqual(["甲","乙"]),"C4 的回覆 S39 buddy 更新名單",c.World.Buddies);
    }

    // 3. NPC 頁 delbuddy → 589 框 → C202＋C4；名稱錯誤印 53；空白不送；取消只關框。
    static void NpcDelete(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        t.Check(OpenNpcPage(c,(1,"delbuddy"),(2,"addbuddy")),"NPC 頁開啟");
        int count=sent.Count,before=c.World.InteractionRevision;
        t.Check(Option(c,1)&&sent.Count==count,"delbuddy：開框，不送 C125",Hex(sent));
        var p=c.World.HtmlNameInput;
        t.Check(p is {Kind:HtmlNameKind.DelBuddy,Query:false,PromptId:589,Prompt:"在好友清單上刪除誰？"}&&c.World.InteractionRevision==before+1,"名稱框 589，頁版本 +1",p);
        t.Check(c.HtmlNameCanSend,"可送（好友存取，不看血盟）");
        t.Check(Wait(c.SubmitHtmlName(p!.Revision," 乙 "))&&sent.Count==count+2&&sent[count].SequenceEqual(Op(202,"乙"))&&sent[count+1].SequenceEqual(Query),"確定 → C202 乙＋C4（去頭尾空白）",Hex(sent));
        t.Check(c.World.HtmlNameInput==null&&c.World.InteractionKind=="dialog"&&c.World.BuddyStale,"框關、頁維持、名單待確認");

        count=sent.Count;int lines=c.World.PlayerChatLines.Length;
        t.Check(Option(c,1)&&!Wait(c.SubmitHtmlName(c.World.HtmlNameInput!.Revision,"甲\t乙"))&&sent.Count==count&&c.World.HtmlNameInput==null,"名稱不合法：框關、不送");
        t.Check(c.World.PlayerChatLines.Length==lines+1&&c.World.PlayerChatLines[^1].Text.StartsWith("無效的名字。"),"名稱不合法 → 聊天欄印 53（比照 588，推測）",c.World.PlayerChatLines.LastOrDefault()?.Text);
        t.Check(Option(c,1)&&!Wait(c.SubmitHtmlName(c.World.HtmlNameInput!.Revision,"  "))&&sent.Count==count&&c.World.HtmlNameInput==null,"空白名稱：框關、不送");
        t.Check(Option(c,1)&&c.CancelHtmlName(c.World.HtmlNameInput!.Revision)&&c.World.HtmlNameInput==null&&sent.Count==count,"取消：只關框");
        t.Check(Option(c,2)&&c.World.HtmlNameInput is {Kind:HtmlNameKind.AddBuddy,PromptId:588},"同一頁的 addbuddy 照舊開 588");
        t.Check(Wait(c.SubmitHtmlName(c.World.HtmlNameInput!.Revision,"丙"))&&sent.Count==count+2&&sent[count].SequenceEqual(Op(207,"丙"))&&sent[count+1].SequenceEqual(Query),"588 確定 → C207＋C4 不變",Hex(sent));
        t.Check(Option(c,1)&&c.World.HtmlNameInput!=null,"再開 589 框");
        c.World.CloseInteraction();
        t.Check(c.World.HtmlNameInput==null,"關對話 → 框失效");
    }

    // 4. 伺服器主動頁：buddy → C4（頁不關）；delbuddy → 589 框（Query）→ C202＋C4。
    static void ServerPage(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        t.Check(OpenServerPage(c,(1,"buddy"),(2,"delbuddy"),(3,"refreshbuddy")),"自己 id 的 S39 → 伺服器主動頁");
        int count=sent.Count;
        t.Check(QueryOption(c,1)&&sent.Count==count+1&&sent[^1].SequenceEqual(Query)&&c.World.QueryVisible,"buddy → C4，頁不關",Hex(sent));
        t.Check(QueryOption(c,3)&&sent.Count==count+2&&sent[^1].SequenceEqual(Query),"refreshbuddy → C4",Hex(sent));
        count=sent.Count;
        t.Check(QueryOption(c,2)&&sent.Count==count,"delbuddy：開框，不送 C125",Hex(sent));
        var p=c.World.HtmlNameInput;
        t.Check(p is {Kind:HtmlNameKind.DelBuddy,Query:true,PromptId:589}&&c.World.QueryVisible,"名稱框 589 開在伺服器主動頁，頁不關",p);
        t.Check(Wait(c.SubmitHtmlName(p!.Revision,"甲"))&&sent.Count==count+2&&sent[count].SequenceEqual(Op(202,"甲"))&&sent[count+1].SequenceEqual(Query),"確定 → C202＋C4",Hex(sent));
    }

    // 5. 不是本組的動作照舊送 C125。
    static void NotOurs(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        t.Check(OpenNpcPage(c,(1,"buddylist")),"NPC 頁開啟");
        int count=sent.Count;
        t.Check(Option(c,1)&&sent.Count==count+1&&sent[^1].SequenceEqual(NpcPackets.Action(Npc,"buddylist")),"buddylist（不是好友 HyperAct）→ C125",Hex(sent));
    }

    // 6. 不能存取名單時：buddy 不送、也不落到 C125；589 框確定不送、框保留。
    static void Blocked(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        t.Check(OpenServerPage(c,(1,"buddy"),(2,"delbuddy")),"伺服器主動頁");
        t.Check(QueryOption(c,2)&&c.World.HtmlNameInput is {Kind:HtmlNameKind.DelBuddy},"先開 589 框");
        int count=sent.Count;var gate=c.GetType().GetField("sendMovementGate",F)!;var old=gate.GetValue(c);
        gate.SetValue(c,null);
        t.Check(!c.HtmlNameCanSend&&!Wait(c.SubmitHtmlName(c.World.HtmlNameInput!.Revision,"甲"))&&c.World.HtmlNameInput!=null&&sent.Count==count,"沒有送出閘門：不能送、框保留");
        gate.SetValue(c,old);
        Set(c.World,"TeleportState","ack-pending");   // TeleportPending＝true
        t.Check(!c.HtmlNameCanSend&&!QueryOption(c,1)&&sent.Count==count,"傳送中（名單不能存取）：buddy 不送、不落到 C125",Hex(sent));
    }

    // 7. 589 框提示照字串表。
    static void PromptFromTable(WorldModuleTestContext t){
        t.Equal("T589",BuddyHyperActs.DelInputPrompt,"有字串表 → 照表");
        t.Equal("T588",BuddyHyperActs.AddInputPrompt,"588 也照表");
        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        t.Check(OpenNpcPage(c,(1,"delbuddy"))&&Option(c,1)&&c.World.HtmlNameInput is {Prompt:"T589",PromptId:589},"名稱框提示照字串表",c.World.HtmlNameInput);
    }
}
