using System.Reflection;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R10-C（T387／#902）離線測試（名稱 friend-host，由 --module-tests 執行）：
//   產品宿主 LoginFriendWindowHost（FriendPanels.Host）：好友 Delete → 1813 是 → C202（本地移除、不送 C4）；封鎖 Delete → C171；
//   信件封鎖不送；好友雙擊 → 聊天列 `"名稱 `；HyperAct `addbuddy` → 588 輸入框 → C207＋C4（名稱錯誤印 53）。依據 FU9§9.2、gap-matrix 15.12.6。
// 只用依 3.81 欄位合成的封包、合成字串表、合成 XML（FriendPanelTests.Xml）與記憶體送出閘門；不開遊戲、網路、DB、視窗，也不讀客戶端素材。
static class FriendHostTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123,Npc=902;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Op(byte op,string name)=>P([op],Str(name));
    static byte[] Query=>LoginPackets.Pad([4]);
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static T Wait<T>(Task<T> task)=>task.GetAwaiter().GetResult();
    static string Hex(List<byte[]> sent)=>string.Join(" ",sent.Select(Convert.ToHexString));
    static byte[] Appear(uint id,ushort x,string name)=>P([87],Le16(x),Le16(32940),Le32(id),Le16(0),[0,0,0,0],Le32(0),Le16(0),Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    /// <summary>S39 "buddy"（3.81 S_Buddy）：D owner、S "buddy"、H 2、H 2、S 全部名單、S 在線名單。</summary>
    static byte[] BuddyList(uint owner,string all,string online)=>P([39],Le32(owner),Str("buddy"),Le16(2),Le16(2),Str(all),Str(online));
    /// <summary>S250/17 整份封鎖名單：C 數量、S 名稱…</summary>
    static byte[] ExcludeList(params string[] names)=>P([new byte[]{250,17,(byte)names.Length},..names.Select(Str)]);

    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");   // 自己的 S87 名稱要等於選中的角色名（WorldSession 會核對）
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(b,valid,apply)=>{if(!valid())return Task.FromResult(false);sent.Add(b);apply();return Task.FromResult(true);};
        Set(c,"sendMovementGate",sender);
        c.World.Receive(BuddyList(Me,"甲 乙 丙","甲"),"offline");
        c.World.Receive(ExcludeList("丁"),"offline");
        return c;
    }
    static ServerStringTable Strings(params (int id,string text)[] rows){
        var lines=new string[3484];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(lines.Length+"\r\n"+string.Join("\r\n",lines)));
    }

    [WorldModuleTest("friend-host")]
    static void Run(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){
            HostAdapter(t);PanelToPackets(t);Registration(t);DoubleClick(t);
            AddBuddyInput(t);AddBuddyOnServerPage(t);
        }
        using(ServerMessageText.UseTable(Strings((588,"T588"))))AddBuddyPromptFromTable(t);
        Wiring(t);
    }

    // 1. 轉接本身：Friend → C202（只送一包，本地移除）；Exclude → C171（切換）；MailExclude、名稱不合法、不在世界 → 不送。
    static void HostAdapter(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);var w=c.World;
        t.Check(w.BuddyLoaded&&w.Buddies.Select(b=>b.Name).SequenceEqual(["甲","乙","丙"])&&w.IsExcluded("丁"),"合成名單：好友 甲乙丙、封鎖 丁",w.Buddies);
        var h=new LoginFriendWindowHost(c);
        int rev=w.BuddyRevision;
        t.Check(h.Remove(FriendListKind.Friend,"乙")&&sent.Count==1&&sent[0].SequenceEqual(Op(202,"乙")),"Friend 乙 → C202（不接 C4）",Hex(sent));
        t.Check(!w.Buddies.Any(b=>b.Name=="乙")&&w.Buddies.Length==2&&w.BuddyRevision>rev,"送出後本地移除 乙（FU9§9.2 sub_B18F10）",w.Buddies);
        t.Check(new LoginFriendListSource(c).Read(FriendListKind.Friend).Rows.Select(r=>r.Name).SequenceEqual(["甲","丙"]),"原生窗的清單同步少了 乙");
        t.Check(h.Remove(FriendListKind.Exclude,"丁")&&sent.Count==2&&sent[1].SequenceEqual(Op(171,"丁"))&&w.ExcludePending,"Exclude 丁 → C171（3.81 切換＝解除）",Hex(sent));
        t.Check(!h.Remove(FriendListKind.Exclude,"丁")&&sent.Count==2,"封鎖切換等待回覆中：不再送");
        w.Receive(P([250,19],Str("丁")),"offline");
        t.Check(!w.ExcludePending&&!w.IsExcluded("丁"),"S250/19 回覆後移出封鎖名單");
        t.Check(!h.Remove(FriendListKind.MailExclude,"戊")&&sent.Count==2,"MailExclude 不送（3.81 不支援）");
        t.Check(!h.Remove(FriendListKind.Friend,"甲 乙")&&!h.Remove(FriendListKind.Friend,"")&&sent.Count==2,"名稱不合法：不送、不丟例外");
        Set(c,"State",LoginState.CharacterList);
        t.Check(!h.Remove(FriendListKind.Friend,"甲")&&sent.Count==2,"不在世界：不送");
    }

    // 2. 面板 → 宿主 → 封包：FriendUI 選 甲、Delete、1813 是 → C202；否 → 不送；ExcludeUI Delete → C171。
    static IUiGameState State(bool inWorld=true)=>
        new UiChatGameState(new UiStaticGameState(InWorld:inWorld),new(inWorld?new PlayerChatView(1,1,Me,1,true,"",[]):null,false));
    static UiKeyEvent K(Keys k)=>new(k,true,false);
    static UiMouseEvent M(UiMouseKind kind,Point at,int clicks=1)=>new(kind,at,MouseButtons.Left,clicks,0,Keys.None);
    // 沒有 surf：窗在 XML 原點 (0,0)，清單 (25,50)，列 182×16（同 FriendWindowTests）。
    static Point Row(int i)=>new(25+10,50+16*i+5);
    static void Click(FriendPanel p,Point at,IUiCommands c,int clicks=1){p.Mouse(M(UiMouseKind.Down,at,clicks),c);p.Mouse(M(UiMouseKind.Up,at,clicks),c);}
    static FriendPanel Panel(UiRoot root,FriendWindowSpec s,IFriendListSource src,IFriendWindowHost host){
        var p=new FriendPanel(s,()=>src,()=>host,root.Find);
        p.Load(UiLayout.Parse(FriendPanelTests.Xml(s),s.Layout+".xml"),_=>null);root.Add(p);p.Show();p.Update(State());return p;
    }
    static void PanelToPackets(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        var root=new UiRoot();var src=new LoginFriendListSource(c);var host=new LoginFriendWindowHost(c);var cmd=new UiNullCommands();
        var f=Panel(root,FriendWindowSpec.Friend,src,host);var e=Panel(root,FriendWindowSpec.Exclude,src,host);var m=Panel(root,FriendWindowSpec.MailExclude,src,host);
        Click(f,Row(0),cmd);
        t.Check(f.SelectedName=="甲"&&f.Key(K(Keys.Delete),cmd)&&f.PendingDelete is {Name:"甲"}&&sent.Count==0,"FriendUI Delete → 1813，先不送",f.PendingDelete);
        t.Check(f.AnswerDelete(f.PendingDelete!.Revision,false)&&sent.Count==0,"1813 否 → 不送");
        f.Key(K(Keys.Delete),cmd);
        t.Check(f.AnswerDelete(f.PendingDelete!.Revision,true)&&sent.Count==1&&sent[0].SequenceEqual(Op(202,"甲")),"1813 是 → C202 甲",Hex(sent));
        f.Update(State());
        t.Check(f.Rows.Select(r=>r.Name).SequenceEqual(["乙","丙"])&&f.SelectedName==null,"好友窗下一格就少了 甲（本地移除）",f.Rows);
        Click(e,Row(0),cmd);
        t.Check(e.SelectedName=="丁"&&e.Key(K(Keys.Delete),cmd)&&e.PendingDelete==null&&sent.Count==2&&sent[1].SequenceEqual(Op(171,"丁")),"ExcludeUI Delete → 直接 C171 丁",Hex(sent));
        c.World.ToggleMailExclude("戊");m.Update(State());Click(m,Row(0),cmd);
        t.Check(m.SelectedName=="戊"&&!m.Key(K(Keys.Delete),cmd)&&sent.Count==2,"MailExcludeUI Delete → 不送");
    }

    // 3. 註冊工廠讀 FriendPanels.Host：設了產品宿主，自動發現建出的窗就會送（測試結束還原靜態值）。
    static void Registration(WorldModuleTestContext t){
        var oldSource=FriendPanels.Source;var oldHost=FriendPanels.Host;
        try{
            var sent=new List<byte[]>();using var c=Seed(sent);
            FriendPanels.Source=new LoginFriendListSource(c);FriendPanels.Host=new LoginFriendWindowHost(c);
            var root=new UiRoot();var reg=UiPanelRegistry.Panels.Single(r=>r.Id==FriendWindowSpec.Friend.Id);
            var p=reg.Create(root) as FriendPanel;
            t.Check(p!=null&&typeof(IUiClosablePanel).IsAssignableFrom(typeof(FriendPanel)),"自動發現建出 FriendPanel（IUiClosablePanel）");
            p!.Load(UiLayout.Parse(FriendPanelTests.Xml(FriendWindowSpec.Friend),"FriendUI.xml"),_=>null);root.Add(p);p.Show();p.Update(State());
            var cmd=new UiNullCommands();Click(p,Row(2),cmd);p.Key(K(Keys.Delete),cmd);
            t.Check(p.PendingDelete is {Name:"丙"}&&p.AnswerDelete(p.PendingDelete.Revision,true)&&sent.Count==1&&sent[0].SequenceEqual(Op(202,"丙")),"註冊的窗經 FriendPanels.Host 送 C202",Hex(sent));
        }finally{FriendPanels.Source=oldSource;FriendPanels.Host=oldHost;}
    }

    // 4. 好友雙擊 → 聊天列 `"名稱 `（不送包）；用產品的清單來源。
    sealed class ChatCommands:IUiChatCommands {
        public void ShowDevTools(string? page=null){}
        public void ReturnGameFocus(){}
        public void Logout(){}
        public bool BeginChatInput()=>true;
        public void EndChatInput(){}
        public Task<bool> SendChat(string text,PlayerChatView displayed)=>Task.FromResult(true);
    }
    static void DoubleClick(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=Seed(sent);
        var root=new UiRoot();var chat=new ChatPanel();root.Add(chat);chat.Update(State());
        var f=Panel(root,FriendWindowSpec.Friend,new LoginFriendListSource(c),new LoginFriendWindowHost(c));var cmd=new ChatCommands();
        Click(f,Row(1),cmd);Click(f,Row(1),cmd,clicks:2);
        t.Check(chat.Input.Open&&chat.Input.Text=="\"乙 "&&sent.Count==0,"雙擊 乙 → 聊天列 \"乙 ，不送包",chat.Input.Text);
    }

    // 5. HyperAct addbuddy（NPC 頁）→ 588 輸入框 → C207＋C4；名稱錯誤印 53；空白不送；不送 C125。
    static byte[] Dialog(uint id,string html)=>P([39],Le32(id),Str(html),Le16(0),Le16(0));
    static byte[] NpcAppear()=>P([87],Le16(32601),Le16(32940),Le32(Npc),Le16(1110),new byte[4],Le32(0),Le16(0),Str("NPC"),Str(""),[4],Le32(0),Str(""),Str(""),[0,255,0,0,0,255,255]);
    static LoginConnection SeedNpc(List<byte[]> sent){
        var c=new LoginConnection();c.SeedQ4("r10c-self",2,"",0,0,30,sent);
        c.World.Receive(NpcAppear(),"offline");c.World.SelectTarget(Npc);return c;
    }
    static bool OpenNpcPage(LoginConnection c,params (int id,string action)[] options){
        if(!c.World.CanStartInteraction)c.World.CloseInteraction();
        if(!Wait(c.Interact("talk",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision)))return false;
        c.World.Receive(Dialog(Npc,"r10c"),"offline");
        Set(c.World,"InteractionPage",new NpcPage("好友",options.Select(o=>new NpcOption(o.id,o.action,"action",o.action,true)).ToArray(),""));
        return c.World.InteractionKind=="dialog";
    }
    static bool Option(LoginConnection c,int id)=>Wait(c.Interact("option",c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision,id));
    static void AddBuddyInput(WorldModuleTestContext t){
        t.Check(BuddyHyperActs.IsAddBuddy(" AddBuddy ")&&!BuddyHyperActs.IsAddBuddy("addbuddy 甲")&&!BuddyHyperActs.IsAddBuddy("buddy"),"addbuddy 名稱去空白、不分大小寫");
        t.Equal(588,HtmlActions.PromptText(HtmlNameKind.AddBuddy),"輸入框 588");
        t.Equal("在好友清單上增加誰？",BuddyHyperActs.AddInputFallback,"588 原文（0-客戶端 string-c.tbl）");
        t.Equal(BuddyHyperActs.AddInputFallback,BuddyHyperActs.AddInputPrompt,"沒有字串表 → 588 原文");
        t.Equal<string?>(null,HtmlActions.Normalize("addbuddy"),"R7-D 六個血盟動作不變");

        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        t.Check(OpenNpcPage(c,(1,"addbuddy"),(2," AddBuddy ")),"NPC 頁開啟");
        int count=sent.Count,before=c.World.InteractionRevision;
        t.Check(Option(c,1)&&sent.Count==count,"addbuddy：開框，不送 C125",Hex(sent));
        var p=c.World.HtmlNameInput;
        t.Check(p is {Kind:HtmlNameKind.AddBuddy,Query:false,PromptId:588,Prompt:"在好友清單上增加誰？"}&&c.World.InteractionRevision==before+1,"名稱框 588，頁版本 +1",p);
        t.Check(c.HtmlNameCanSend,"可送（好友存取，不看血盟）");
        t.Check(Wait(c.SubmitHtmlName(p!.Revision," 丙 "))&&sent.Count==count+2&&sent[count].SequenceEqual(Op(207,"丙"))&&sent[count+1].SequenceEqual(Query),"確定 → C207 丙＋C4（去頭尾空白）",Hex(sent));
        t.Check(c.World.HtmlNameInput==null&&c.World.InteractionKind=="dialog"&&c.World.BuddyStale,"框關、頁維持、名單待確認");

        count=sent.Count;int lines=c.World.PlayerChatLines.Length;
        t.Check(Option(c,2)&&c.World.HtmlNameInput is {Kind:HtmlNameKind.AddBuddy},"大小寫不同的 AddBuddy 也開框");
        t.Check(!Wait(c.SubmitHtmlName(c.World.HtmlNameInput!.Revision,"甲\t乙"))&&sent.Count==count&&c.World.HtmlNameInput==null,"名稱不合法：框關、不送");
        t.Check(c.World.PlayerChatLines.Length==lines+1&&c.World.PlayerChatLines[^1].Text.StartsWith("無效的名字。"),"名稱不合法 → 聊天欄印 53",c.World.PlayerChatLines.LastOrDefault()?.Text);
        t.Check(Option(c,1)&&!Wait(c.SubmitHtmlName(c.World.HtmlNameInput!.Revision,"  "))&&sent.Count==count&&c.World.HtmlNameInput==null,"空白名稱：框關、不送");
        t.Check(Option(c,1)&&c.CancelHtmlName(c.World.HtmlNameInput!.Revision)&&c.World.HtmlNameInput==null&&sent.Count==count,"取消：只關框");
        t.Check(Option(c,1)&&c.World.HtmlNameInput!=null,"再開框");
        c.World.CloseInteraction();
        t.Check(c.World.HtmlNameInput==null,"關對話 → 框失效");
    }

    // 6. 伺服器主動頁上的 addbuddy：同樣開框（Query），頁不關。
    static void AddBuddyOnServerPage(WorldModuleTestContext t){
        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        c.World.Receive(Dialog(Q4FollowupTests.Own,"r10c-query"),"offline");
        t.Check(c.World.QueryVisible&&c.World.QueryActionable,"自己 id 的 S39 → 伺服器主動頁");
        Set(c.World,"QueryPage",new NpcPage("好友",[new NpcOption(1,"新增好友","action","addbuddy",true)],""));
        int count=sent.Count;
        t.Check(Wait(c.QueryOption(1,c.World.QueryRevision,c.World.WorldEpoch))&&sent.Count==count,"addbuddy：開框，不送 C125",Hex(sent));
        var p=c.World.HtmlNameInput;
        t.Check(p is {Kind:HtmlNameKind.AddBuddy,Query:true,PromptId:588}&&c.World.QueryVisible,"名稱框 588 開在伺服器主動頁，頁不關",p);
        t.Check(Wait(c.SubmitHtmlName(p!.Revision,"甲"))&&sent.Count==count+2&&sent[count].SequenceEqual(Op(207,"甲"))&&sent[count+1].SequenceEqual(Query),"確定 → C207＋C4",Hex(sent));
    }

    static void AddBuddyPromptFromTable(WorldModuleTestContext t){
        t.Equal("T588",BuddyHyperActs.AddInputPrompt,"有字串表 → 照表");
        var sent=new List<byte[]>();using var c=SeedNpc(sent);
        t.Check(OpenNpcPage(c,(1,"addbuddy"))&&Option(c,1)&&c.World.HtmlNameInput is {Prompt:"T588"},"名稱框提示照字串表",c.World.HtmlNameInput);
    }

    // 7. 產品接線：ClientForm.InitBuddyUi 設 FriendPanels.Host（ClientForm 不能離線建，只核對型別與方法存在）。
    static void Wiring(WorldModuleTestContext t){
        t.Check(typeof(IFriendWindowHost).IsAssignableFrom(typeof(LoginFriendWindowHost)),"LoginFriendWindowHost 實作 IFriendWindowHost");
        t.Check(typeof(ClientForm).GetMethod("InitBuddyUi",F)!=null&&typeof(ClientForm).GetMethod("ToggleNativeFriendWindow",F)!=null,"InitBuddyUi 與過渡開窗入口仍在（--classic-ui 的 WinForms 好友分頁照舊）");
        t.Check(typeof(LoginConnection).GetMethod("SendBuddy")?.GetParameters().Length==4,"SendBuddy 多一個可省略的 localRemove");
    }
}
