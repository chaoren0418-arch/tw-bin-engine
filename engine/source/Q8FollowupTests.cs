using P06.NativeUi;
namespace P06;

// T426（#970）離線測試：Q8-C 小項（docs/reference/followups-15.md §2.1、§2.7、§2.9、§2.11）。
// ① effect 470 圖示照 vipFlag 選 icon／offIcon（sub_9001F0）；② 原生 UI 的信件結果印到聊天欄（sub_941000）；
// ③ 公告板等回應期間 662 Write 與清單選取不動作（[this+0x40]）；④ Y/N 提示在作用中時 ESC 先給 Y/N（sub_8CA740 第 4 步在第 5 步前）。
// 只用合成表、合成面板與記憶體內狀態，不建 ClientForm、不讀客戶端資源、不開遊戲、不連網路。
static class Q8FollowupTests {
    static readonly IUiGameState InWorld=new UiStaticGameState(InWorld:true);
    static UiSurface Surface(int id,int w,int h)=>new(id,$"{id}.img","synthetic",new UiImage(w,h,0,0,Enumerable.Repeat(0xFF808080u,w*h).ToArray()));
    static UiKeyEvent Esc(bool down=true)=>new(Keys.Escape,down,false);

    // ① sub_9001F0：470 不看時間與模式，只看玩家 +0x328。
    [WorldModuleTest("q8-vip470-icon")]
    static void Vip470Icon(WorldModuleTestContext t){
        var def=new BuffEffect(470,7470,8470,null,0,-1,"VIP");
        foreach(int mode in new[]{0,1,2,6,8,12}){
            t.Check(BuffIconsPanel.IconFor(def,new BuffEntry(470,-1,false,"t",DisplayMode:mode),0)==(8470,true),$"470 mode {mode}, vipFlag 0 → offIcon");
            t.Check(BuffIconsPanel.IconFor(def,new BuffEntry(470,30,true,"t",DisplayMode:mode),3)==(7470,false),$"470 mode {mode}, vipFlag 3, expired → icon");
        }
        var noOff=def with{OffIcon=0};
        t.Check(BuffIconsPanel.IconFor(noOff,new BuffEntry(470,-1,false,"t"),0)==(7470,false),"470 without offIcon in the table → icon (no substitute)");
        // 其他 effect 規則不變（sub_902840：offIcon 存在、時間到、模式 0／6）。
        var other=new BuffEffect(469,7469,8469,null,0,-1,"X");
        t.Check(BuffIconsPanel.IconFor(other,new BuffEntry(469,0,true,"t"),0)==(8469,true),"469 expired mode 0 → offIcon");
        t.Check(BuffIconsPanel.IconFor(other,new BuffEntry(469,0,true,"t",DisplayMode:8),0)==(7469,false),"469 expired mode 8 → icon");
        t.Check(BuffIconsPanel.IconFor(other,new BuffEntry(469,5,false,"t"),1)==(7469,false),"469 not expired → icon; vipFlag ignored");
        t.Check(BuffIconsPanel.IconFor(other,new BuffEntry(469,5,false,"t",IconOverride:9000),0)==(9000,false),"icon override kept");

        // 面板經 Update 走同一條路，tooltip 規則（R7-E）不受影響。
        var x=new System.Text.StringBuilder("<effectlist>");
        for(int i=0;i<480;i++)x.Append($"<effect id=\"{i}\" icon=\"{7000+i}\" offIcon=\"{8000+i}\" duration=\"-1\"/>");
        var table=BuffEffectTable.Parse(UiLayout.Parse(x.Append("</effectlist>").ToString(),"effectlist2.xml"));
        var text=new BuffTooltipText("L","U","O","P","R","M","D","H","Q","V");
        var p=new BuffIconsPanel();p.Load(table,new BuffIconCatalog([]));
        p.Update(new UiBuffGameState(InWorld,new BuffState([new BuffEntry(470,-1,false,"none-time:470")],[],text)));
        var a=p.Layout.Single();
        t.Check(a.Icon==8470&&a.Off,"panel: 3.81 (vipFlag 0) → 470 draws offIcon",a);
        t.Equal("V",p.TooltipText(a),"panel: tooltip still string 3399");
        p.Update(new UiBuffGameState(InWorld,new BuffState([new BuffEntry(470,-1,false,"none-time:470")],[],text,VipFlag:1)));
        var b=p.Layout.Single();
        t.Check(b.Icon==7470&&!b.Off,"panel: vipFlag 1 → icon",b);
        t.Equal((string?)null,p.TooltipText(b),"panel: vipFlag 1 → no tooltip");
    }

    sealed class MailHost:IMailPanelHost {
        public MailView? View{get;set;}
        public readonly List<string> Lines=[];
        public Task<bool> Run(MailView shown,string command,byte box,uint id,string receiver="",string subject="",string body="")=>Task.FromResult(true);
        public void ChatNotice(string text)=>Lines.Add(text);
    }
    static MailView Mail(MailStamp s,int rev,string notice)=>new(s,rev,[],null,"",notice);

    // ② sub_941000：信件結果印在聊天欄，不在窗內。
    [WorldModuleTest("q8-mail-notice-chat")]
    static void MailNotice(WorldModuleTestContext t){
        var s1=new MailStamp(1,1,100);var s2=new MailStamp(1,2,100);
        var r=new MailNoticeRelay();
        t.Equal((string?)null,r.Next(Mail(s1,1,"舊訊息"),false),"first snapshot = baseline, nothing printed");
        t.Equal((string?)null,r.Next(Mail(s1,1,"舊訊息"),false),"same notice → nothing");
        t.Equal("伺服器已確認刪除信件。",r.Next(Mail(s1,2,"伺服器已確認刪除信件。"),false),"new notice → printed once");
        t.Equal((string?)null,r.Next(Mail(s1,3,"伺服器已確認刪除信件。"),false),"same text again (e.g. headers changed) → not repeated");
        t.Equal((string?)null,r.Next(Mail(s1,4,"已送出信件操作；等待伺服器回包。"),true),"change while a request is in flight → absorbed");
        t.Equal("伺服器已確認刪除信件。",r.Next(Mail(s1,5,"伺服器已確認刪除信件。"),false),"reply after the acknowledgement → printed");
        r.Absorb(Mail(s1,6,"收到無法解析的信件回包"));
        t.Equal((string?)null,r.Next(Mail(s1,6,"收到無法解析的信件回包"),false),"absorbed notice is not printed later");
        t.Equal((string?)null,r.Next(Mail(s2,1,"尚未要求信件清單。"),false),"new identity (stamp) → baseline only");
        t.Equal((string?)null,r.Next(Mail(s2,2,""),false),"empty notice → nothing");

        // 面板：窗沒開也照樣轉到聊天欄；Host 沒接時不做事；離開世界不做事。
        var host=new MailHost{View=Mail(s1,1,"尚未要求信件清單。")};
        var panel=new MailPanel(){Host=host};
        panel.Update(InWorld);
        t.Equal(0,host.Lines.Count,"first frame: baseline");
        host.View=Mail(s1,2,"伺服器已確認存入保管箱。");panel.Update(InWorld);
        t.Check(!panel.Visible&&host.Lines.SequenceEqual(["伺服器已確認存入保管箱。"]),"window closed: result still printed to the chat line",host.Lines);
        panel.Update(InWorld);
        t.Equal(1,host.Lines.Count,"next frame: not repeated");
        host.View=Mail(s1,3,"伺服器回報無法寄出信件（一般信可能是收件匣已滿）。");panel.Update(new UiStaticGameState(InWorld:false));
        t.Equal(1,host.Lines.Count,"not in the world → nothing");
        t.Equal(24,ClientForm.MailChatColor,"sub_7DC850 palette[24]");
    }

    sealed class BoardHost:IBoardPanelHost {
        public BoardView? Board{get;set;}
        public readonly List<string> Pressed=[];
        public void Press(string command)=>Pressed.Add(command);
    }
    static BoardView Board(string kind,bool canAct)=>new(1,1,1,1,1,0x55,kind,"",[new(1,"a","d","t")],null,0,canAct,false,false);

    // ③ [this+0x40]：等清單／讀文回應期間 662 Write 不開發文窗。
    [WorldModuleTest("q8-board-waiting")]
    static void BoardWaiting(WorldModuleTestContext t){
        t.Check(BoardPanelSet.IsWaiting(Board("list-wait",false))&&BoardPanelSet.IsWaiting(Board("read-wait",false)),"list-wait / read-wait = waiting");
        t.Check(!BoardPanelSet.IsWaiting(Board("list",true))&&!BoardPanelSet.IsWaiting(Board("article",true))&&!BoardPanelSet.IsWaiting(null),"list / article / none = not waiting");
        t.Check(!BoardPanelSet.IsWaiting(Board("submitted",false)),"write/delete sent (submitted) is not the list/read wait");

        var host=new BoardHost{Board=Board("list-wait",false)};
        var set=new BoardPanelSet(host);set.LoadSurfaces(id=>Surface(id,24,16));
        var list=set.List;var cmd=new UiNullCommands();
        int write=list.Buttons.ToList().FindIndex(b=>b.Role==BoardButtonRole.Write);
        var at=list.ButtonRect(write).Location+new Size(2,2);
        void Click(){list.Mouse(new(UiMouseKind.Down,at,MouseButtons.Left,1,0,Keys.None),cmd);list.Mouse(new(UiMouseKind.Up,at,MouseButtons.Left,1,0,Keys.None),cmd);}
        list.Update(InWorld);
        t.Check(list.Visible&&set.Waiting,"list window up while waiting for the list");
        Click();
        t.Check(!host.Pressed.Contains("write"),"662 Write while waiting → nothing",host.Pressed);
        host.Board=Board("list",true);list.Update(InWorld);Click();
        t.Check(host.Pressed.SequenceEqual(["write"]),"after the list arrives → Write goes to the host",host.Pressed);
    }

    sealed class YesNoSource:IYesNoSource {
        public YesNoPanelView? Current{get;set;}
        public readonly List<(int,bool)> Answers=[];
        public void Answer(int revision,bool accept){Answers.Add((revision,accept));Current=null;}
    }
    sealed class EscWindow:IUiClosablePanel {
        public string Id=>"XML_WIN";public UiLayer Layer=>UiLayer.Window;
        public bool Open{get;set;}
        public Rectangle Bounds=>Open?new(100,100,200,150):Rectangle.Empty;public bool Visible=>Open;
        public void Load(UiLayout layout,SurfCatalog surfs){}public void Update(IUiGameState state){}public void Paint(UiCanvas canvas){}
        public bool Mouse(UiMouseEvent e,IUiCommands commands)=>Bounds.Contains(e.Location);
        public bool Key(UiKeyEvent e,IUiCommands commands){if(!Open||!e.Down||e.KeyData!=Keys.Escape)return false;Close();return true;}
        public void Close()=>Open=false;
    }

    // ④ sub_8CA740：聊天輸入列的提示（第 4 步）先於 XML 視窗（第 5 步）。
    [WorldModuleTest("q8-esc-yesno-first")]
    static void EscYesNoFirst(WorldModuleTestContext t){
        var r=new UiRoot();var src=new YesNoSource();var yn=new YesNoPanel{Source=src};var win=new EscWindow{Open=true};
        r.Add(yn);r.Add(win);
        var input=new UiInput(r,new UiNullCommands()){Viewport=UiPresenter.Compute(new(800,600))};
        r.Update(InWorld);
        input.Mouse(UiMouseKind.Down,new(150,150),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(150,150),MouseButtons.Left);
        t.Check(input.Focused==win,"XML window has the key focus");
        src.Current=new(7,"要加入嗎？");r.Update(InWorld);
        t.Check(!input.Key(Esc(false))&&src.Answers.Count==0&&win.Open,"ESC release: nothing");
        t.Check(input.Key(Esc()),"ESC taken");
        t.Check(src.Answers.SequenceEqual([(7,false)])&&win.Open,"prompt up → ESC refuses the prompt first; the focused window stays open",src.Answers);
        r.Update(InWorld);
        t.Check(input.Key(Esc())&&!win.Open&&src.Answers.Count==1,"no prompt → ESC goes to the focused window as before");
        win.Open=true;r.Update(InWorld);
        input.Mouse(UiMouseKind.Down,new(150,150),MouseButtons.Left,1);input.Mouse(UiMouseKind.Up,new(150,150),MouseButtons.Left);
        src.Current=new(8,"?");r.Update(InWorld);
        t.Check(!input.Key(new(Keys.A,true,false))&&src.Answers.Count==1&&win.Open,"other keys are not taken by the prompt");
    }
}
