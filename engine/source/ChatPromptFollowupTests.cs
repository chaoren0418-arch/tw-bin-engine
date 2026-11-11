using System.Reflection;
using System.Text;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// T423（#967，FU15§2.2、§2.3，Q8-A）離線測試：合成 XML／surf、合成封包、記憶體內假傳送端；不開遊戲、網路、視窗或素材。
// 涵蓋：ChatLineAsk（聊天輸入列提示入口，接在 R10-F ChatPanel.LinePrompts 上：放在原來來源前面、答完或 Esc 後還原）；
// ModifyMemo → 聊天輸入列 3325，Enter 送 C78 16、空白不送、超過 60 byte 印 3328 不送、離開世界提示消失；沒有聊天窗時保留窗內編輯；
// 委任 → 聊天輸入列 4051（帶隊員名稱），Y 才送 "delegate"，送完清選取，N／Esc 不送；沒有聊天窗時照舊直接送；
// 產品端：WorldPledgeSource.MemoNotice 把 3328 印進聊天紀錄，WinForms 上限改 60。
static class ChatPromptFollowupTests {
    [WorldModuleTest("chat-prompt-followup")]
    static void Run(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(null)){Entry(t);Texts(t);Memo(t);Delegate(t);Product(t);}
        TableTexts(t);
    }

    static ServerStringTable Table(params (int id,string text)[] rows){
        int count=rows.Max(r=>r.id)+1;var lines=new string[count];Array.Fill(lines,"");foreach(var (id,text) in rows)lines[id]=text;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ServerStringTable.Parse(Encoding.GetEncoding(950).GetBytes(count+"\n"+string.Join("\n",lines)+"\n"));
    }

    // 1. ChatLineAsk：本機提示、放在原來來源前面、答完／取消後還原、Live false 自己消失、新的取代舊的。
    static void Entry(WorldModuleTestContext t){
        var chat=new ChatPanel();
        var trade=new ChatLinePrompt("trade","要給幾個？",_=>true);
        Func<ChatLinePrompt?> prev=()=>trade;chat.LinePrompts=prev;
        var answers=new List<string>();
        t.Check(ChatLineAsk.Ask(chat,"k1","問題一",s=>{answers.Add(s);return true;}),"ask placed");
        var p=chat.LinePrompts?.Invoke();
        t.Check(p is {Local:true,Text:"問題一"},"own prompt first, answered locally",p);
        t.Check(ReferenceEquals(ChatLineAsk.Pending(chat),p),"pending = own prompt");
        t.Check(p!.Answer!("y")&&answers.SequenceEqual(["y"]),"answer goes to the asker",answers);
        t.Check(ReferenceEquals(chat.LinePrompts,prev),"after the answer the original source is back");
        t.Check(!p.Answer!("y")&&answers.Count==1,"one answer only");
        // Esc（Cancel）：不回答、還原。
        ChatLineAsk.Ask(chat,"k2","問題二",s=>{answers.Add(s);return true;});
        chat.LinePrompts!.Invoke()!.Cancel!();
        t.Check(answers.Count==1&&ReferenceEquals(chat.LinePrompts,prev)&&ChatLineAsk.Pending(chat)==null,"Esc cancels, nothing answered, source restored");
        // 新的取代舊的（輸入列只有一個提示）。
        ChatLineAsk.Ask(chat,"k3","舊",s=>{answers.Add("old:"+s);return true;});var old=chat.LinePrompts!.Invoke()!;
        ChatLineAsk.Ask(chat,"k4","新",s=>{answers.Add("new:"+s);return true;});
        t.Equal("新",chat.LinePrompts!.Invoke()!.Text,"newer prompt replaces the older one");
        t.Check(!old.Answer!("y")&&answers.Count==1,"older prompt no longer answers");
        chat.LinePrompts!.Invoke()!.Cancel!();
        t.Check(ReferenceEquals(chat.LinePrompts,prev),"restored to the first source, not to our own hook");
        // Live false：提示消失，回到原來來源。
        bool live=true;ChatLineAsk.Ask(chat,"k5","會消失",_=>true,()=>live);
        t.Equal("會消失",chat.LinePrompts!.Invoke()!.Text,"live prompt shown");
        live=false;
        t.Check(ReferenceEquals(chat.LinePrompts!.Invoke(),trade)&&ReferenceEquals(chat.LinePrompts,prev),"dead prompt dropped, trade prompt again");
        // 原來沒有來源：還原成 null（TradeUi.cs 的 ??= 下次仍接得上）。
        var bare=new ChatPanel();ChatLineAsk.Ask(bare,"k","x",_=>true);bare.LinePrompts!.Invoke()!.Cancel!();
        t.Check(bare.LinePrompts==null,"no previous source → null again");
        t.Check(ChatLineAsk.Chat(null)==null&&ChatLineAsk.Chat(_=>null)==null&&ChatLineAsk.Chat(id=>id==ChatPanel.WindowName?bare:null)==bare,"Chat() finds CENTER_WINDOW");
    }

    // 2. 字串：沒有表時用本機表 3325／3328 原文；4051 本機表沒有，用參考客戶端原文並帶入名稱。
    static void Texts(WorldModuleTestContext t){
        t.Equal((3325,3328,60),(PledgePanel.MemoPromptMessage,PledgePanel.MemoTooLongMessage,PledgePanel.MemoInputLimit),"ids 3325 / 3328, input limit 60");
        t.Equal(62,PledgePanel.MemoByteLimit,"62 stays the buffer / S250 169 size");
        t.Equal("請輸入目錄內容。(最多60個字/Ese=取消)",PledgePanel.MemoPromptText,"3325 without a table");
        t.Equal("目錄內容太長。(最多60個字)",PledgePanel.MemoTooLongText,"3328 without a table");
        t.Equal(4051,PartyPackets.DelegateConfirmMessage,"id 4051");
        t.Equal("要委任二號 為組隊隊長嗎?",PartyPackets.DelegateConfirmText("二號"),"4051 fallback with the member name");
        t.Equal((string?)null,PledgePanel.MemoPromptError(new string('甲',30)),"30 CJK = 60 bytes ok");
        t.Equal(PledgePanel.MemoTooLongText,PledgePanel.MemoPromptError(new string('甲',31)),"31 CJK = 62 bytes → 3328");
        t.Equal(PledgePanel.MemoTooLongText,PledgePanel.MemoPromptError(new string('a',61)),"61 ASCII → 3328");
        t.Check(PledgePanel.MemoPromptError("a\tb") is {} e&&e!=PledgePanel.MemoTooLongText,"control char rejected by the C78 16 rule");
    }
    static void TableTexts(WorldModuleTestContext t){
        using(ServerMessageText.UseTable(Table((3325,"\\f1請輸入內容"),(3328,"太長"),(4051,"委任%s？")))){
            t.Equal("請輸入內容",PledgePanel.MemoPromptText,"3325 from the table, colour code removed");
            t.Equal("太長",PledgePanel.MemoTooLongText,"3328 from the table");
            t.Equal("委任Bob？",PartyPackets.DelegateConfirmText("Bob"),"4051 from the table, %s = name");
        }
        using(ServerMessageText.UseTable(Table((3325,"x")))){
            t.Equal("要委任Bob 為組隊隊長嗎?",PartyPackets.DelegateConfirmText("Bob"),"table without 4051 (local 3,484 rows) → fallback");
        }
    }

    // ---- 3. ModifyMemo（合成 PledgeUI.xml，同 PledgeMemoTests 的版面）----
    const string Xml="""
<?xml version="1.0"?>
<PledgeUI Toggle="true">
  <Window Name = "Noticeuserinfo_Window" X = "0" Y = "0" BaseSurf = "4512;1" Moveable = "20">
    <Button Name = "Closeui_Button" X = "260" Y = "13" BaseSurf = "4429"/>
    <Control Name = "Pledgename_Control" X = "19" Y = "25" Width = "189" Height = "20">
      <Label Name = "Pledge_Label" X = "17" Y="-2" Width = "169" Height = "20"/>
    </Control>
    <Button Name = "ModifyMemo_Button" X = "217" Y = "24" BaseSurf = "4452" MouseEvent = "ModifyMemo"/>
    <Button Name = "Pledgenotice_Button" X = "16" Y = "41" BaseSurf = "4455">
      <Button Name = "Noticetri_Button" X = "200" Y = "0" BaseSurf = "4456"/>
    </Button>
    <Control Name = "Notice_Control" X = "21" Y = "63" Width = "225" Height = "112">
      <MultiLabel Name = "Notice_Label" Width = "220" Height = "109"/>
    </Control>
    <List Name = "Sort_List" X = "16" Y = "181" Width = "208" Height = "21" Horizontal = "1">
      <Button Name = "Classsort_Button" BaseSurf = "4458"/>
    </List>
    <List Name = "Userinfo_List" X = "21" Y = "202" Width = "214" Height = "222" Vertical = "1">
      <List Name = "Userinfo0_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1">
        <Image Name = "Class_Image" Surf = "4546;4" Width = "19"/>
        <Label Name = "Name_Label" Width = "74" Height = "15"/>
        <Label Name = "Rank_Label" Width = "35" Height = "15"/>
        <Label Name = "Memo_Label" Width = "97" Height = "15"/>
      </List>
    </List>
    <ScrollBar Name = "Userinfo_Scrollbar" X = "255" Y = "67" Width = "12" Height = "293" ScrollStart = "14" ScrollHeight = "262">
      <Image Name = "Background_Image" Y = "14" Surf = "4443;1"/>
      <Button Name = "Up_Button" BaseSurf = "4446;1"/>
      <Button Name = "Move_Button" Y = "14" BaseSurf = "4448;1"/>
      <Button Name = "Down_Button" Y = "277" BaseSurf = "4444;1"/>
    </ScrollBar>
  </Window>
  <Window Name = "Nonoticeuserinfo_Window" X = "0" Y = "0" BaseSurf = "4450;1" Moveable = "20">
    <Button Name = "Closeui_Button" X = "260" Y = "13" BaseSurf = "4429"/>
    <Control Name = "Pledgename_Control" X = "19" Y = "25" Width = "189" Height = "20">
      <Label Name = "Pledge_Label" X = "17" Y = "-2" Width = "169" Height = "20"/>
    </Control>
    <Button Name = "ModifyMemo_Button" X = "217" Y = "24" BaseSurf = "4452" MouseEvent = "ModifyMemo"/>
    <Button Name = "Pledgenotice_Button" X = "16" Y = "41" BaseSurf = "4455">
      <Button Name = "Noticetri_Button" X = "200" Y = "0" BaseSurf = "4494"/>
    </Button>
    <List Name = "Sort_List" X = "16" Y = "61" Width = "208" Height = "21" Horizontal = "1">
      <Button Name = "Classsort_Button" BaseSurf = "4458"/>
    </List>
    <List Name = "Userinfo_List" X = "21" Y = "82" Width = "204" Height = "339" Vertical = "1">
      <List Name = "Userinfo0_List" Width = "204" Height = "15" Horizontal = "1" Gap = "1">
        <Image Name = "Class_Image" Surf = "4546;4" Width = "19"/>
        <Label Name = "Name_Label" Width = "74" Height = "15"/>
        <Label Name = "Rank_Label" Width = "35" Height = "15"/>
        <Label Name = "Memo_Label" Width = "97" Height = "15"/>
      </List>
    </List>
    <ScrollBar Name = "Userinfo_Scrollbar" X = "255" Y = "67" Width = "12" Height = "293" ScrollStart = "14" ScrollHeight = "262">
      <Image Name = "Background_Image" Y = "14" Surf = "4443;1"/>
      <Button Name = "Up_Button" BaseSurf = "4446;1"/>
      <Button Name = "Move_Button" Y = "14" BaseSurf = "4448;1"/>
      <Button Name = "Down_Button" Y = "277" BaseSurf = "4444;1"/>
    </ScrollBar>
  </Window>
</PledgeUI>
""";
    static UiSurface Img(int surf,int w,int h)=>new(surf,surf+".img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF204060u,w*h).ToArray()));
    static readonly Dictionary<int,(int w,int h)> Sizes=new(){
        [4450]=(277,423),[4512]=(277,423),[4429]=(12,12),[4452]=(32,21),[4455]=(200,16),[4456]=(12,16),[4494]=(12,16),
        [4458]=(20,21),[4443]=(12,263),[4446]=(12,14),[4448]=(12,20),[4444]=(12,14),
    };
    static UiSurface? Get(int surf)=>Sizes.TryGetValue(surf,out var s)?Img(surf,s.w,s.h):null;
    static UiMouseEvent Click(Point p)=>new(UiMouseKind.Down,p,MouseButtons.Left,1,0,Keys.None);
    static IUiGameState PledgeWorld(bool inWorld,string? me)=>new UiPledgeGameState(new UiStaticGameState(InWorld:inWorld,CharacterName:me),
        new PledgeWindowState(1,1,"紅血盟",["Alice","Bob"],new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){["Bob"]="補師"}));

    sealed class FakeSource:IPledgeWindowSource {
        public readonly List<string> Memos=[],Notices=[];
        public bool? ShowEmblem=>null;
        public ClanEmblem? Emblem=>null;
        public bool SendNotice(string text)=>false;
        public bool SetRank(string member,byte rank)=>false;
        public bool SetShowEmblem(bool show)=>false;
        public bool UploadEmblem()=>false;
        public bool SendMemo(string text){Memos.Add(text);return true;}
        public void MemoNotice(string text)=>Notices.Add(text);
    }

    static void Memo(WorldModuleTestContext t){
        var layout=UiLayout.Parse(Xml,"PledgeUI.xml");var cmd=new UiNullCommands();
        var chat=new ChatPanel();var src=new FakeSource();
        var p=new PledgePanel(id=>id==ChatPanel.WindowName?chat:null);p.Load(layout,Get);p.Source=src;
        p.Update(PledgeWorld(true,"Bob"));
        t.Check(p.Visible,"pledge window open");
        // 按 ModifyMemo：不進窗內編輯，提示 3325 放上聊天輸入列。
        t.Check(p.Mouse(Click(new(220,28)),cmd)&&!p.EditingMemo,"ModifyMemo does not edit inside the window");
        var q=chat.LinePrompts?.Invoke();
        t.Check(q is {Local:true}&&q.Text==PledgePanel.MemoPromptText,"3325 on the chat input line",q);
        // 空白 Enter：不送。
        t.Check(!q!.Answer!("")&&src.Memos.Count==0&&src.Notices.Count==0,"blank answer → nothing sent");
        t.Check(chat.LinePrompts?.Invoke()==null,"prompt closed after the answer");
        // 超過 60 byte：3328 印到聊天欄，不送。
        p.AskMemo();q=chat.LinePrompts!.Invoke()!;
        t.Check(!q.Answer!(new string('甲',31))&&src.Memos.Count==0,"62 bytes → not sent");
        t.Check(src.Notices.SequenceEqual([PledgePanel.MemoTooLongText]),"3328 printed in the chat log",src.Notices);
        // 60 byte：送 C78 16，本機備註不變（等 169）。
        p.AskMemo();q=chat.LinePrompts!.Invoke()!;
        string sixty=new string('甲',30);
        t.Check(q.Answer!(sixty)&&src.Memos.SequenceEqual([sixty]),"60 bytes → C78 16 sent",src.Memos);
        t.Check(p.Status.Contains("已送出成員備註"),"status: sent, memo column waits for 169",p.Status);
        // Esc：不送。
        p.AskMemo();chat.LinePrompts!.Invoke()!.Cancel!();
        t.Check(src.Memos.Count==1&&chat.LinePrompts==null,"Esc → nothing sent, prompt gone");
        // 離開世界：提示消失。
        p.AskMemo();p.Update(PledgeWorld(false,"Bob"));
        t.Check(chat.LinePrompts?.Invoke()==null&&src.Memos.Count==1,"leaving the world drops the prompt");
        // 換角色：舊提示失效。
        p.Update(PledgeWorld(true,"Bob"));p.AskMemo();p.Update(PledgeWorld(true,"Alice"));
        t.Check(chat.LinePrompts?.Invoke()==null,"another character drops the prompt");
        // 沒有聊天窗：照舊在窗內編輯（後備）。
        var w=new PledgePanel();w.Load(layout,Get);w.Source=src;w.Update(PledgeWorld(true,"Bob"));
        t.Check(!w.AskMemo()&&w.Mouse(Click(new(220,28)),cmd)&&w.EditingMemo,"no chat window → R7-K in-window editing");
        // 沒有 Source／角色名：不問。
        var n=new PledgePanel(id=>id==ChatPanel.WindowName?chat:null);n.Load(layout,Get);n.Update(PledgeWorld(true,"Bob"));
        t.Check(!n.AskMemo()&&chat.LinePrompts==null,"no source → no prompt");
        t.Equal(0,cmd.Calls.Count,"no UI commands");
    }

    // ---- 4. 委任 ----
    static void Delegate(WorldModuleTestContext t){
        var log=new List<(string Cmd,string Name)>();
        var cmd=new UiMacroCommands(new UiPartyCommands(new UiNullCommands(),(c,v,n)=>log.Add((c,n))),(_,_)=>{});   // product nesting
        var m=new PartyMember[]{new(11,"Alpha",100),new(12,"Bravo",50),new(0,"Charlie",null)};
        PartyView V(bool leader=true)=>new(1,1,1,77,m,leader?"Me":"Alpha","",null,true,true,leader);
        var chat=new ChatPanel();
        var panel=new PartyPanel(null,id=>id==ChatPanel.WindowName?chat:null);
        void Sync(PartyView v,bool inWorld=true)=>panel.Update(new UiPartyGameState(new UiStaticGameState(InWorld:inWorld),v));
        Sync(V());panel.SelectMember("Bravo");
        t.Check(panel.FireDelegate(cmd)&&log.Count==0,"delegate button → asks first, nothing sent");
        var q=chat.LinePrompts?.Invoke();
        t.Check(q is {Local:true}&&q.Text=="要委任Bravo 為組隊隊長嗎?","4051 with the member name on the chat input line",q);
        // N：不送，提示收起，選取保留。
        t.Check(!q!.Answer!("n")&&log.Count==0&&chat.LinePrompts==null&&panel.Selected=="Bravo","N → nothing sent, prompt gone");
        // 空白：不送。
        panel.FireDelegate(cmd);t.Check(!chat.LinePrompts!.Invoke()!.Answer!("")&&log.Count==0,"blank → nothing sent");
        // Esc：不送。
        panel.FireDelegate(cmd);chat.LinePrompts!.Invoke()!.Cancel!();
        t.Check(log.Count==0&&chat.LinePrompts==null,"Esc → nothing sent");
        // Y：送 delegate Bravo，清掉選取。
        panel.FireDelegate(cmd);
        t.Check(chat.LinePrompts!.Invoke()!.Answer!("y")&&log.SequenceEqual([("delegate","Bravo")]),"Y → \"delegate\" Bravo",log);
        t.Equal((string?)null,panel.Selected,"selection cleared after sending (sub_956F60(-1))");
        // 問了之後不再是隊長：提示消失，不送。
        panel.SelectMember("Alpha");panel.FireDelegate(cmd);Sync(V(leader:false));
        t.Check(chat.LinePrompts?.Invoke()==null&&log.Count==1,"leadership lost → prompt dropped");
        // 不能委任的對象：不問也不送。
        Sync(V());panel.SelectMember("Charlie");
        t.Check(!panel.FireDelegate(cmd)&&chat.LinePrompts==null&&log.Count==1,"member without object id → no prompt");
        // 沒有聊天窗：照舊直接送（後備）。
        var direct=new PartyPanel();direct.Update(new UiPartyGameState(new UiStaticGameState(InWorld:true),V()));direct.SelectMember("Alpha");
        t.Check(direct.FireDelegate(cmd)&&log[^1]==("delegate","Alpha"),"no chat window → sent directly",log);
        // Interaction 沒有 AskDelegate（PartyRtPanel、R7-J 測試）：行為不變。
        var act=new PartyLayout.Interaction();act.Select("Bravo");
        t.Check(act.Fire(PartyLayout.Delegate,V(),cmd)&&log[^1]==("delegate","Bravo"),"Interaction without AskDelegate unchanged");
    }

    // ---- 5. 產品端：WorldPledgeSource.MemoNotice → 聊天紀錄 ----
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static void Product(WorldModuleTestContext t){
        var c=new LoginConnection();
        try{
            Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)4);Set(c.World,"TimeReplies",1);
            Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
            Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\",\"ClanId\":5,\"Clan\":\"紅血盟\"}"));
            var sent=new List<byte[]>();
            Func<byte[],Func<bool>,Action,Task<bool>> sender=(p,valid,apply)=>{if(!valid())return Task.FromResult(false);apply();sent.Add(p);return Task.FromResult(true);};
            Set(c,"sendMovementGate",sender);
            IPledgeWindowSource s=new WorldPledgeSource(c,()=>false);
            int lines=c.World.PlayerChatLines.Length;
            s.MemoNotice(PledgePanel.MemoTooLongText);
            t.Equal(lines+1,c.World.PlayerChatLines.Length,"3328 adds one chat log line");
            t.Equal(PledgePanel.MemoTooLongText,c.World.PlayerChatLines[^1].Text,"chat log text = 3328");
            t.Equal(0,sent.Count,"nothing sent for the notice");
            t.Check(s.SendMemo(new string('甲',30))&&sent.Count==1,"60-byte memo sent through C78 16");
        }finally{c.Dispose();}
    }
}
