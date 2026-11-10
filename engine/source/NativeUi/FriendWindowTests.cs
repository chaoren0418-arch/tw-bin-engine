using P06.NativeUi;
namespace P06;

// R9-H（T368／#852）好友／封鎖窗事件的離線測試（名稱 friend-window，由 --module-tests 執行）：
//   FriendUI Delete → 1813 Y/N → 是 → C202（經 IFriendWindowHost）；ExcludeUI Delete → 直接 C171；MailExcludeUI Delete 不送；
//   FriendUI 雙擊 → 聊天輸入列 `"名稱 `；Esc 關本窗；1813 提示畫在聊天輸入列（FriendDeletePromptPanel）。
// 只用合成 XML（FriendPanelTests.Xml）、合成狀態與記憶體內面板；不讀客戶端素材、不連線、不開視窗。
static class FriendWindowTests {
    sealed class Source:IFriendListSource {
        public string[] Friends=["甲","乙","丙"],Excluded=["丁"],Mail=["戊"];
        public FriendListData Read(FriendListKind kind)=>new((kind switch{FriendListKind.Friend=>Friends,FriendListKind.Exclude=>Excluded,_=>Mail})
            .Select(n=>new FriendListRow(n)).ToArray());
    }
    sealed class Host:IFriendWindowHost {
        public readonly List<string> Calls=[];
        public bool Accept=true;
        public bool Remove(FriendListKind kind,string name){Calls.Add(kind+":"+name);return Accept;}
    }
    sealed class ChatCommands:IUiChatCommands {
        public readonly List<string> Calls=[];
        public bool Allow=true;
        public void ShowDevTools(string? page=null)=>Calls.Add("dev-tools:"+page);
        public void ReturnGameFocus()=>Calls.Add("game-focus");
        public void Logout()=>Calls.Add("logout");
        public bool BeginChatInput(){Calls.Add("begin");return Allow;}
        public void EndChatInput()=>Calls.Add("end");
        public Task<bool> SendChat(string text,PlayerChatView displayed){Calls.Add("send:"+text);return Task.FromResult(true);}
    }
    sealed class Ask(YesNoPanelView? v):IYesNoSource {
        public YesNoPanelView? Current{get;set;}=v;
        public void Answer(int revision,bool accept)=>Current=null;
    }

    static IUiGameState State(bool inWorld=true,bool canSend=true)=>
        new UiChatGameState(new UiStaticGameState(InWorld:inWorld),new(inWorld?new PlayerChatView(1,1,123,1,canSend,"",[]):null,false));
    static UiKeyEvent K(Keys k,bool down=true,bool repeat=false)=>new(k,down,repeat);
    static UiMouseEvent M(UiMouseKind kind,Point at,int clicks=1)=>new(kind,at,MouseButtons.Left,clicks,0,Keys.None);
    // Row i (no surfaces: window at the XML origin (0,0); list at (25,50), rows 182×16).
    static Point Row(int i)=>new(25+10,50+16*i+5);
    static void Click(FriendPanel p,Point at,IUiCommands c,int clicks=1){p.Mouse(M(UiMouseKind.Down,at,clicks),c);p.Mouse(M(UiMouseKind.Up,at,clicks),c);}

    sealed record Rig(UiRoot Root,FriendPanel Friend,FriendPanel Exclude,FriendPanel Mail,FriendDeletePromptPanel Prompt,ChatPanel Chat,YesNoPanel YesNo,Host Host,Source Source);
    static Rig Make(Host? host=null){
        var src=new Source();var h=host??new Host();var root=new UiRoot();
        FriendPanel P(FriendWindowSpec s){
            var p=new FriendPanel(s,()=>src,()=>h,root.Find);
            p.Load(UiLayout.Parse(FriendPanelTests.Xml(s),s.Layout+".xml"),_=>null);root.Add(p);return p;
        }
        var chat=new ChatPanel();root.Add(chat);
        var yes=new YesNoPanel();root.Add(yes);
        var f=P(FriendWindowSpec.Friend);var e=P(FriendWindowSpec.Exclude);var m=P(FriendWindowSpec.MailExclude);
        var prompt=new FriendDeletePromptPanel(root.Find);root.Add(prompt);
        return new(root,f,e,m,prompt,chat,yes,h,src);
    }
    static void Frame(Rig r,IUiGameState s){r.Chat.Update(s);r.YesNo.Update(s);r.Friend.Update(s);r.Exclude.Update(s);r.Mail.Update(s);r.Prompt.Update(s);}

    [WorldModuleTest("friend-window")]
    static void Run(WorldModuleTestContext t){
        Registration(t);FriendDelete(t);ExcludeDelete(t);Escape(t);DoubleClick(t);
    }

    // 1. 註冊：提示面板自動發現；三窗的工廠帶 root 查找；1813 原文、密語前綴。
    static void Registration(WorldModuleTestContext t){
        t.Check(UiPanelRegistry.Errors.Count==0,"[UiPanel] registrations valid",UiPanelRegistry.Errors);
        var reg=UiPanelRegistry.Panels.SingleOrDefault(r=>r.Id==FriendDeletePromptPanel.PanelId);
        t.Check(reg!=null&&reg.Create(new UiRoot()) is FriendDeletePromptPanel{Layer:UiLayer.Float},"1813 prompt panel registered (Float)");
        foreach(var s in FriendWindowSpec.All)
            t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==s.Id).Create(new UiRoot()) is FriendPanel,"registration still creates FriendPanel "+s.Id);
        t.Check(typeof(IUiClosablePanel).IsAssignableFrom(typeof(FriendPanel)),"FriendPanel stays IUiClosablePanel");
        t.Equal(1813,FriendPanels.DeleteConfirmId,"Y/N string id 1813");
        t.Equal("要刪除你所選擇的好友嗎?",FriendPanels.DeleteConfirmFallback,"1813 text (0-客戶端 string-c.tbl)");
        using(ServerMessageText.UseTable(null))t.Equal(FriendPanels.DeleteConfirmFallback,FriendPanels.DeleteConfirmText,"no string table → 1813 fallback");
        t.Equal("\"甲 ",FriendPanels.WhisperPrefix("甲"),"whisper prefix \"name + space");
        t.Check(PlayerChatProtocol.Valid(FriendPanels.WhisperPrefix("甲")+"你好"),"prefix + text is a valid whisper line");
    }

    // 2. FriendUI Delete（4142）→ 1813 Y/N → 是 → C202；否／Esc／過期／名單變更 → 不送。
    static void FriendDelete(WorldModuleTestContext t){
        using var table=ServerMessageText.UseTable(null);
        var r=Make();var c=new UiNullCommands();var f=r.Friend;
        Frame(r,State());
        t.Check(!f.Key(K(Keys.Delete),c),"closed window: Delete not taken");
        f.Show();Frame(r,State());
        t.Check(!f.Key(K(Keys.Delete),c)&&f.PendingDelete==null,"nothing selected: Delete not taken");
        Click(f,Row(1),c);
        t.Equal("乙",f.SelectedName,"row 1 selected");
        t.Check(!f.Key(K(Keys.Delete,repeat:true),c),"repeat ignored");
        t.Check(!f.Key(K(Keys.Delete|Keys.Shift),c),"modifier ignored");
        t.Check(f.Key(K(Keys.Delete),c)&&f.PendingDelete is {Name:"乙"} p1&&p1.Text==FriendPanels.DeleteConfirmFallback,"Delete opens 1813 for 乙",f.PendingDelete);
        t.Check(r.Host.Calls.Count==0,"asking sends nothing");
        Frame(r,State());
        t.Check(r.Prompt.Visible&&r.Prompt.Bounds==YesNoButtons.Line&&r.Prompt.Current==f.PendingDelete,"prompt on the chat input line (192,581,280,12)");
        t.Check(!r.Prompt.Mouse(M(UiMouseKind.Down,new(200,585)),c),"prompt does not take the mouse");
        t.Check(!r.Prompt.Key(K(Keys.A),c)&&f.PendingDelete!=null,"other keys pass");
        t.Check(r.Prompt.Key(K(Keys.N),c)&&f.PendingDelete==null&&r.Host.Calls.Count==0,"N → no packet");
        Frame(r,State());t.Check(!r.Prompt.Visible&&r.Prompt.Bounds.IsEmpty,"prompt gone after answer");

        f.Key(K(Keys.Delete),c);Frame(r,State());
        t.Check(r.Prompt.Key(K(Keys.Escape),c)&&f.PendingDelete==null&&r.Host.Calls.Count==0&&f.Visible,"Esc on the prompt → no packet, window stays");
        f.Key(K(Keys.Delete),c);Frame(r,State());
        t.Check(r.Prompt.Key(K(Keys.Y),c)&&f.PendingDelete==null,"Y answers");
        t.Check(r.Host.Calls.SequenceEqual(["Friend:乙"]),"Y → Remove(Friend, 乙) once (C202)",r.Host.Calls);

        // 過期 revision、重複回答
        f.Key(K(Keys.Delete),c);var old=f.PendingDelete!;
        f.Key(K(Keys.Delete),c);var now=f.PendingDelete!;
        t.Check(now.Revision>old.Revision,"new ask → new revision");
        t.Check(!f.AnswerDelete(old.Revision,true)&&f.PendingDelete==now,"stale revision rejected");
        t.Check(f.AnswerDelete(now.Revision,false)&&!f.AnswerDelete(now.Revision,true),"answered once only");
        t.Equal(1,r.Host.Calls.Count,"still one Remove");

        // 伺服器詢問（YesNoPanel）顯示中：提示讓位，詢問保留
        var ask=new Ask(new(7,"伺服器詢問"));r.YesNo.Source=ask;
        f.Key(K(Keys.Delete),c);Frame(r,State());
        t.Check(r.YesNo.Visible&&!r.Prompt.Visible&&!r.Prompt.Key(K(Keys.Y),c)&&f.PendingDelete!=null,"server Y/N showing → 1813 prompt yields");
        ask.Current=null;Frame(r,State());
        t.Check(r.Prompt.Visible,"server Y/N gone → 1813 prompt back");

        // 聊天輸入列開著：Y／N／Esc 給聊天列；Delete 也不攔
        r.Chat.Input.Begin(false);
        t.Check(!r.Prompt.Key(K(Keys.Y),c)&&!r.Prompt.Key(K(Keys.Escape),c)&&f.PendingDelete!=null,"chat input open → prompt takes no keys");
        r.Chat.Input.Close();
        f.AnswerDelete(f.PendingDelete!.Revision,false);
        r.Chat.Input.Begin(false);
        t.Check(!f.Key(K(Keys.Delete),c)&&f.PendingDelete==null,"chat input open → Delete not taken");
        r.Chat.Input.Close();

        // 名單變更、關窗、離開世界 → 詢問作廢，不送
        f.Key(K(Keys.Delete),c);var pending=f.PendingDelete!;
        r.Source.Friends=["甲","丙"];Frame(r,State());
        t.Check(f.PendingDelete==null&&f.SelectedName==null,"name left the list → ask dropped");
        t.Check(!f.AnswerDelete(pending.Revision,true),"dropped ask cannot be answered");
        Click(f,Row(0),c);f.Key(K(Keys.Delete),c);f.Hide();
        t.Check(f.PendingDelete==null,"window closed → ask dropped");
        f.Show();Frame(r,State());Click(f,Row(0),c);f.Key(K(Keys.Delete),c);Frame(r,State(inWorld:false));
        t.Check(f.PendingDelete==null&&!r.Prompt.Visible,"left the world → ask dropped");
        t.Equal(1,r.Host.Calls.Count,"no extra Remove");

        // 沒有宿主：Delete 不詢問
        var bare=new FriendPanel(FriendWindowSpec.Friend,()=>r.Source);
        bare.Load(UiLayout.Parse(FriendPanelTests.Xml(FriendWindowSpec.Friend),"FriendUI.xml"),_=>null);
        bare.Show();bare.Update(State());Click(bare,Row(0),c);
        t.Check(bare.SelectedName=="甲"&&!bare.Key(K(Keys.Delete),c)&&bare.PendingDelete==null,"no host → Delete not taken");
    }

    // 3. ExcludeUI Delete → 直接 C171（不確認）；MailExcludeUI Delete → 不送（3.81 不支援）。
    static void ExcludeDelete(WorldModuleTestContext t){
        var r=Make();var c=new UiNullCommands();
        r.Exclude.Show();r.Mail.Show();Frame(r,State());
        Click(r.Exclude,Row(0),c);
        t.Check(r.Exclude.Key(K(Keys.Delete),c)&&r.Exclude.PendingDelete==null,"Exclude Delete taken, no Y/N");
        t.Check(r.Host.Calls.SequenceEqual(["Exclude:丁"]),"Exclude Delete → Remove(Exclude, 丁) (C171)",r.Host.Calls);
        Frame(r,State());t.Check(!r.Prompt.Visible,"no 1813 prompt for ExcludeUI");
        Click(r.Mail,Row(0),c);
        t.Check(r.Mail.SelectedName=="戊"&&!r.Mail.Key(K(Keys.Delete),c),"MailExclude Delete not taken");
        t.Equal(1,r.Host.Calls.Count,"MailExclude sends nothing");
        Click(r.Exclude,Row(0),c,2);
        t.Check(!r.Chat.Input.Open,"ExcludeUI double-click: no whisper prefix");
    }

    // 4. Esc（27）：關本窗；未開時不攔（交給全域 ESC）。
    static void Escape(WorldModuleTestContext t){
        var r=Make();var c=new UiNullCommands();
        foreach(var p in new[]{r.Friend,r.Exclude,r.Mail}){
            Frame(r,State());
            t.Check(!p.Key(K(Keys.Escape),c),p.Id+": closed → Esc not taken");
            p.Show();
            t.Check(!p.Key(K(Keys.Escape,down:false),c)&&p.Visible,p.Id+": key up ignored");
            t.Check(p.Key(K(Keys.Escape),c)&&!p.Visible,p.Id+": Esc closes the window");
        }
        t.Check(r.Host.Calls.Count==0,"Esc sends nothing");
    }

    // 5. FriendUI 雙擊（515）→ 聊天輸入列 `"名稱 `，不送封包；之後 Enter 走既有 SendChat。
    static void DoubleClick(WorldModuleTestContext t){
        var r=Make();var c=new ChatCommands();var f=r.Friend;
        f.Show();Frame(r,State());
        Click(f,Row(0),c);
        t.Check(!r.Chat.Input.Open&&c.Calls.Count==0,"single click: select only");
        Click(f,Row(2),c,2);
        t.Check(!r.Chat.Input.Open&&f.SelectedName=="丙","double-click landing on another row: select only");
        Click(f,Row(2),c,2);
        t.Check(c.Calls.SequenceEqual(["begin"])&&r.Chat.Input.Open&&r.Chat.Input.Text=="\"丙 ","double-click → chat input \"丙 ",c.Calls);
        t.Check(r.Host.Calls.Count==0,"no packet");
        foreach(char ch in "嗨")r.Chat.Char(ch);
        r.Chat.Key(K(Keys.Enter),c);
        t.Check(c.Calls.Contains("send:\"丙 嗨"),"Enter sends the whisper through the existing chat path",c.Calls);
        r.Chat.Key(K(Keys.Enter,down:false),c);

        // 已開輸入列：草稿被取代、不再呼叫 BeginChatInput
        c.Calls.Clear();if(!r.Chat.Input.Open)r.Chat.Input.Begin(false);
        r.Chat.Input.Clear();foreach(char ch in "草稿")r.Chat.Input.Type(ch);
        Click(f,Row(0),c);Click(f,Row(0),c,2);
        t.Check(r.Chat.Input.Text=="\"甲 "&&!c.Calls.Contains("begin"),"open input: draft replaced",c.Calls);
        r.Chat.Input.Close();r.Chat.Input.Clear();

        // 不能發話、聊天列不讓開、沒有聊天面板 → 什麼都不做
        Frame(r,State(canSend:false));c.Calls.Clear();
        t.Check(!f.Whisper("甲",c)&&!r.Chat.Input.Open&&c.Calls.Count==0,"cannot send → no prefix");
        Frame(r,State());c.Allow=false;
        t.Check(!f.Whisper("甲",c)&&!r.Chat.Input.Open,"chat may not open → no prefix");
        c.Allow=true;
        var alone=new FriendPanel(FriendWindowSpec.Friend,()=>r.Source);alone.Update(State());
        t.Check(!alone.Whisper("甲",c),"no root finder → no prefix");
    }
}
