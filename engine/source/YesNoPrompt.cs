namespace P06;

// M7-J: shared server question (S219 -> chat input line Y/N + Enter -> C121).
// Sources: docs/reference/windows.md §2.1 / T141, docs/reference/world-objects.md §6.
// The original keeps ONE prompt in the input-line object (sub_AC9600 stores the request id at +788),
// so a newer S219 replaces the older prompt; the server also keeps one TempID per character.
public sealed record YesNoPrompt(int Epoch,int Id,uint RequestId,ushort Message,string[] Arguments,string Text,long? Deadline);
public sealed record YesNoView(int Generation,int Revision,YesNoPrompt? Prompt,string Notice);

public static class YesNoProtocol {
    // S_ASK switch special cases, plus the default branch's 1565 (skipped) and 1906 (special).
    static readonly HashSet<ushort> special=[102,180,289,321,325,479,512,622,252,217,951,1565,1906];
    // Generic in the original, but still owned by engine modules outside this file scope
    // (clan join 97 -> ClanUi, clan war 221/222 -> ClanWar). They keep their current UI until R2-K moves them here.
    // R2-J (#624): resurrection 322 (WO §6: S_ASK reads it and enters sub_4FC710) now uses the shared slot.
    static readonly HashSet<ushort> delegated=[97,221,222];
    // Party, duel, marriage and resurrection 322: queued only after their module admitted the question.
    public static bool ModuleOwned(ushort message)=>message is 953 or 630 or 654 or 322;
    public static bool Special(ushort message)=>special.Contains(message);
    public static bool Generic(ushort message)=>!special.Contains(message)&&!delegated.Contains(message);
    // Only these four have an automatic reply on timeout (windows.md T141).
    public static int? TimeoutMs(ushort message)=>message switch{729=>12000,1232 or 1256 or 1268=>10000,_=>null};
    // C121 H0 D(request id echoed) H(message) C(answer). d287 sends D=0 except party's challenge; never the 622 requestId path.
    public static byte[] Answer(uint requestId,ushort message,bool accept)=>
        LoginPackets.Pad([121,0,0,..BitConverter.GetBytes(requestId),(byte)message,(byte)(message>>8),(byte)(accept?1:0)]);
    // sub_A58380: a leading y/Y (or full-width) accepts; anything else, including empty, declines.
    public static bool Accepts(string typed){string t=typed.TrimStart();return t.Length>0&&t[0] is 'y' or 'Y' or 'ｙ' or 'Ｙ';}
    // Hint appended by the engine (not original text): the original input line shows only the formatted question.
    public const string Hint="（輸入 Y 接受、N 拒絕後按 Enter）";
    // WO §6 / sub_4FC710: the S_ASK message id is a string-c.tbl id, formatted with the S219 arguments (sub_933850).
    // With the table (read-only from the original client at run time) that text is used; colour controls are dropped.
    public static string? TableText(ushort message,string[] args){
        if(ServerMessageText.Table is not {} t||message>=t.Count)return null;
        string text=ServerMessageText.DisplayText(ServerMessageText.Format(t[message],args)).Trim();
        return text.Length>0?text:null;
    }
    // Without the table (e.g. CI) the engine's own wording is used (標示為引擎自製文字).
    public static string FallbackText(ushort message,string[] args){
        string first=args.Length>0?args[0]:"";
        return message switch{
            953 when first.Length>0=>first+" 邀請你加入隊伍。",
            630 when first.Length>0=>first+" 要與你決鬥。",
            654 when first.Length>0=>first+" 向你求婚。",
            252 when first.Length>0=>first+" 邀請你交易。",
            322=>"有人要讓你復活。是否接受？",
            _=>"原服詢問 "+message+(args.Length>0?"："+string.Join("、",args):"。")};
    }
    public static string Text(ushort message,string[] args)=>(TableText(message,args)??FallbackText(message,args))+Hint;
    // Strict decode with its own reader: header 0, D, H, then non-empty strings and zero padding.
    // Returns null for anything else so the existing S219 consumers keep their own validation.
    public static (uint RequestId,ushort Message,string[] Arguments)? Decode(byte[] plain){
        if(plain.Length<9||plain[0]!=219)return null;
        try{
            var r=new LoginReader(plain);r.C();
            if(r.H()!=0)return null;
            uint id=r.D();ushort message=r.H();var args=new List<string>();
            while(r.Remaining>0&&plain[^r.Remaining]!=0){
                if(args.Count>=8)return null;
                string s=r.S();if(s.Any(char.IsControl))return null;args.Add(s);
            }
            r.Finish();return (id,message,args.ToArray());
        }catch(ProtocolException){return null;}
        catch(System.Text.DecoderFallbackException){return null;}
    }
}

public sealed partial class WorldSession {
    readonly object yesNoGate=new();
    YesNoPrompt? yesNo;
    int yesNoRevision,yesNoIds;
    string yesNoNotice="";
    internal Func<long> YesNoClock=()=>Environment.TickCount64;
    void ClearYesNo(string notice){if(yesNo==null)return;yesNo=null;yesNoNotice=notice;yesNoRevision++;}
    // Called from ReceiveParty for every packet before any S219 consumer moves its reader.
    void ObserveYesNo(byte opcode,byte[] plain){
        if(opcode!=219||YesNoProtocol.Decode(plain) is not {} ask)return;
        lock(yesNoGate){
            ClearYesNo("收到新的伺服器詢問，舊詢問已失效。");
            if(!Ready)return;
            if(ask.Message!=322&&(!YesNoProtocol.Generic(ask.Message)||YesNoProtocol.ModuleOwned(ask.Message)))return;
        }
        // 322: ObserveResurrectionPrompt (WorldSession.cs, before ReceiveParty) has already admitted or ignored it.
        if(ask.Message==322&&!ResurrectionAwaitsAnswer(322))return;
        OfferYesNo(ask.RequestId,ask.Message,ask.Arguments);
    }
    internal void OfferYesNo(uint requestId,ushort message,string[] args){
        lock(yesNoGate){
            long? deadline=YesNoProtocol.TimeoutMs(message) is int ms?YesNoClock()+ms:null;
            yesNo=new(WorldEpoch,++yesNoIds,requestId,message,args.ToArray(),YesNoProtocol.Text(message,args),deadline);
            yesNoNotice=yesNo.Text;yesNoRevision++;
        }
    }
    // A module answered (its own UI or this prompt): the input line no longer holds a question.
    internal void YesNoHandled(ushort message){lock(yesNoGate)if(yesNo?.Message==message)ClearYesNo("已回覆伺服器詢問。");}
    bool YesNoModuleLive(YesNoPrompt p)=>p.Message switch{
        953=>partyInvitation is {} i&&i.Challenge==p.RequestId&&i.Epoch==WorldEpoch,
        630=>DuelSnapshot.Phase=="invited",
        654=>MarriagePhaseAfterCheck()=="invited",
        252=>TradeYesNoLive(p), // M7-K: Trade offers 252 itself when byte_136E99D is 1.
        322=>ResurrectionAwaitsAnswer(322),
        _=>true};
    string MarriagePhaseAfterCheck(){CheckMarriage();return marriagePhase;}
    // Module state is read outside yesNoGate: Duel takes duelGate, and modules call YesNoHandled.
    internal YesNoPrompt? CurrentYesNo {get{
        YesNoPrompt? q;lock(yesNoGate)q=yesNo;
        if(q==null)return null;
        if(q.Message==953)CheckParty();
        string? reason=q.Epoch!=WorldEpoch?"世界已更新，舊詢問已失效。":!YesNoModuleLive(q)?"對應的邀請已失效。":null;
        lock(yesNoGate){
            if(!ReferenceEquals(yesNo,q))return yesNo;
            if(reason!=null){ClearYesNo(reason);return null;}
            return q;
        }
    }}
    internal void ExpireYesNo(YesNoPrompt p){lock(yesNoGate)if(ReferenceEquals(yesNo,p))ClearYesNo("詢問已逾時。");}
    internal int YesNoRevision=>yesNoRevision;
    internal string YesNoNotice=>yesNoNotice;
    internal bool YesNoCurrent(YesNoPrompt p)=>ReferenceEquals(CurrentYesNo,p);
    internal void YesNoAnswered(YesNoPrompt p,bool accept,bool timeout=false){
        lock(yesNoGate){if(!ReferenceEquals(yesNo,p))return;yesNo=null;yesNoRevision++;
            yesNoNotice=timeout?"詢問逾時，已自動回覆拒絕（回覆值為推測）。":accept?"已回覆接受，結果以原服回報為準。":"已回覆拒絕。";}
    }
    // Returns the prompt only once, when its 729/1232/1256/1268 deadline has passed.
    internal YesNoPrompt? ExpiredYesNo(){
        lock(yesNoGate){
            if(yesNo is not {Deadline: long due} p||YesNoClock()<due)return null;
            if(p.Epoch!=WorldEpoch){ClearYesNo("世界已更新，舊詢問已失效。");return null;}
            return p;
        }
    }
}

public sealed partial class LoginConnection {
    int yesNoSendingGeneration=-1;
    public YesNoView YesNoSnapshot()=>CaptureWorld(()=>{var p=World.CurrentYesNo;return new YesNoView(generation,World.YesNoRevision,p,World.YesNoNotice);});
    public Task<bool> AnswerYesNo(YesNoView shown,string typed)=>AnswerYesNo(shown,YesNoProtocol.Accepts(typed));
    public async Task<bool> AnswerYesNo(YesNoView shown,bool accept){
        if(shown.Prompt is not {} p||shown.Generation!=generation||!CaptureWorld(()=>World.YesNoCurrent(p)))return false;
        string command=accept?"accept":"reject";
        switch(p.Message){
            case 953:{var v=PartySnapshot();return v.Invitation?.Challenge==p.RequestId&&await SendParty(command,v);}
            case 630:{var d=World.DuelSnapshot;return await Duel(command,d.Revision,d.Epoch);}
            case 654:return await SendMarriage(command,MarriageSnapshot());
            case 252:return await TradeYesNo(accept);
            case 322:{var r=ResurrectionSnapshot();return r is {Message:322,Phase:"invited"}&&await AnswerResurrection(r,accept);}
        }
        return await SendYesNo(p,accept,false);
    }
    // Polled by the UI timer; sends the automatic reply for the four timed messages.
    public async Task<bool> YesNoTick(){
        if(CaptureWorld(()=>World.ExpiredYesNo()) is not {} p)return false;
        if(State!=LoginState.InWorld||sendMovementGate==null){CaptureWorld(()=>{World.ExpireYesNo(p);return true;});return false;}
        return await SendYesNo(p,false,true);
    }
    async Task<bool> SendYesNo(YesNoPrompt p,bool accept,bool timeout){
        int session=generation;
        bool Eligible()=>session==generation&&State==LoginState.InWorld&&World.Ready&&World.WorldEpoch==p.Epoch&&(timeout?CaptureWorld(()=>ReferenceEquals(World.ExpiredYesNo(),p)):CaptureWorld(()=>World.YesNoCurrent(p)));
        if(yesNoSendingGeneration==session||sendMovementGate==null||!Eligible())return false;
        var sender=sendMovementGate;yesNoSendingGeneration=session;
        try{
            bool sent=await sender(YesNoProtocol.Answer(p.RequestId,p.Message,accept),Eligible,()=>{World.YesNoAnswered(p,accept,timeout);Set(State,World.YesNoNotice);});
            return sent&&session==generation;
        }catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"詢問回覆傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(yesNoSendingGeneration==session)yesNoSendingGeneration=-1;}
    }
}

// W2-E (#558, windows-2.md §5.3 / §9): InputWindow #1 (ctor sub_A57590, FO 0x656990) has the text buttons "YES" at
// (361, 567) and "NO" at (401, 567), 800×600 screen coordinates (已驗證, static). The button size is not in the source
// we read: 32×14 is 推定 (enough for the word at the 12 px font); the click answers like Y/N + Enter (same C121 path).
public static class YesNoButtons {
    public static readonly Point YesOrigin=new(361,567),NoOrigin=new(401,567);
    public static readonly Size Size=new(32,14);   // 推定
    public static string Label(bool accept)=>accept?"YES":"NO";
    public static Rectangle Logical(bool accept)=>new(accept?YesOrigin:NoOrigin,Size);
    /// <summary>Logical 800×600 point → true YES, false NO, null neither.</summary>
    public static bool? Hit(Point logical)=>Logical(true).Contains(logical)?true:Logical(false).Contains(logical)?false:null;
    /// <summary>Native canvas: the logical rectangle through the presenter's scale and offset.</summary>
    public static Rectangle Client(bool accept,P06.NativeUi.UiViewport view){
        var r=Logical(accept);var a=view.ToClient(r.Location);var b=view.ToClient(new(r.Right,r.Bottom));
        return Rectangle.FromLTRB(a.X,a.Y,b.X,b.Y);
    }
    /// <summary>Legacy HUD (no 800×600 canvas): keep the original order and 40 px pitch, at the right end of the prompt line.</summary>
    public static Rectangle Legacy(bool accept,Rectangle line){
        int pitch=NoOrigin.X-YesOrigin.X,x=line.Right-pitch-Size.Width-4+(accept?0:pitch);
        return new(x,line.Y+(line.Height-Size.Height)/2,Size.Width,Size.Height);
    }
}

// Chat input line prompt. Layout of the original line (800x600 at (192,581), 280x12) is 推測 to be the
// chat input; this keeps the existing chat TextBox and only shows the question text above the status bar.
public sealed partial class ClientForm {
    bool yesNoUiReady,yesNoAnswering;
    readonly Label yesNoLine=new(){AutoSize=false,Height=24,Visible=false,BackColor=Color.FromArgb(20,20,20),ForeColor=Color.FromArgb(255,230,160),
        TextAlign=ContentAlignment.MiddleLeft,Padding=new(6,0,6,0),AccessibleName="伺服器詢問（聊天輸入列 Y／N）"};
    // OnLoad, OnShown, OnVisibleChanged and OnCreateControl belong to BuddyUi, LocationShare, DuelUi and ClanUi.
    protected override void OnHandleCreated(EventArgs e){
        base.OnHandleCreated(e);
        if(!yesNoUiReady){yesNoUiReady=true;Shown+=(_,_)=>InitYesNoUi();}
    }
    // Not selectable: a click never takes focus from the chat TextBox or the game (the original buttons sit on the input line).
    sealed class YesNoButton:Button {
        public YesNoButton(bool accept){
            SetStyle(ControlStyles.Selectable,false);TabStop=false;Visible=false;Text=YesNoButtons.Label(accept);
            FlatStyle=FlatStyle.Flat;FlatAppearance.BorderColor=Color.FromArgb(120,110,80);BackColor=Color.FromArgb(40,36,28);
            ForeColor=Color.FromArgb(255,230,160);Padding=Padding.Empty;Margin=Padding.Empty;Font=new Font(SystemFonts.DefaultFont.FontFamily,7.5f);
            AccessibleName="伺服器詢問 "+Text;
        }
    }
    readonly YesNoButton yesNoYes=new(true),yesNoNo=new(false);
    YesNoView? yesNoView;
    Task YesNoClick(bool accept)=>yesNoView is {Prompt: not null} view?AnswerYesNoShown(view,accept,"yes-no-button"):Task.CompletedTask;
    // Answers exactly the given prompt; a newer S219 or a new session makes it a no-op (AnswerYesNo checks).
    internal async Task<bool> AnswerYesNoShown(YesNoView view,bool accept,string reason){
        if(yesNoAnswering||view.Prompt==null)return false;
        ClearInput(reason);yesNoAnswering=true;bool sent=false;
        try{sent=await login.AnswerYesNo(view,accept);if(!sent&&!IsDisposed)yesNoLine.Text="未送出：詢問已失效或目前不能回覆。";}
        finally{yesNoAnswering=false;}
        if(!IsDisposed)SyncYesNo();
        return sent;
    }
    // R2-J: with the native canvas the YES／NO buttons are the YesNoPanel (IUiPanel); the WinForms buttons stay only for the legacy HUD.
    P06.NativeUi.YesNoPanel? AttachYesNoPanel(){
        if(!NativeUiActive||nativeUiRoot?.Find(P06.NativeUi.YesNoPanel.PanelId) is not P06.NativeUi.YesNoPanel panel||nativeUiRoot.Faulted.Contains(panel))return null;
        panel.Source??=new YesNoPanelSource(this);
        return panel;
    }
    sealed class YesNoPanelSource(ClientForm form):P06.NativeUi.IYesNoSource {
        public P06.NativeUi.YesNoPanelView? Current{get{
            if(form.login.State!=LoginState.InWorld)return null;
            var v=form.login.YesNoSnapshot();return v.Prompt==null?null:new(v.Revision,v.Prompt.Text);
        }}
        public void Answer(int revision,bool accept){
            var v=form.login.YesNoSnapshot();
            if(v.Prompt!=null&&v.Revision==revision)_=form.AnswerYesNoShown(v,accept,"yes-no-panel");
        }
    }
    void InitYesNoUi(){
        Controls.Add(yesNoLine);Controls.Add(yesNoYes);Controls.Add(yesNoNo);
        yesNoYes.Click+=async(_,_)=>await YesNoClick(true);
        yesNoNo.Click+=async(_,_)=>await YesNoClick(false);
        // KeyPreview: runs before the chat TextBox, so a handled Enter never reaches chat Submit.
        KeyDown+=async(_,e)=>{
            if(e.KeyCode!=Keys.Enter||e.Modifiers!=Keys.None||!PlayerChatFocused)return;
            var view=login.YesNoSnapshot();if(view.Prompt==null||FocusedTextBox(this) is not {} box)return;
            e.Handled=e.SuppressKeyPress=true;if(yesNoAnswering)return;
            string typed=box.Text;box.Clear();ClearInput("yes-no-answer");yesNoAnswering=true;
            try{if(!await login.AnswerYesNo(view,typed)&&!IsDisposed)yesNoLine.Text="未送出：詢問已失效或目前不能回覆。";}
            finally{yesNoAnswering=false;}
            if(!IsDisposed)SyncYesNo();
        };
        var refresh=new System.Windows.Forms.Timer{Interval=150};
        refresh.Tick+=async(_,_)=>{SyncYesNo();await login.YesNoTick();};
        FormClosed+=(_,_)=>refresh.Dispose();refresh.Start();
    }
    int yesNoShown=-1;
    void SyncYesNo(){
        var view=login.YesNoSnapshot();
        bool show=login.State==LoginState.InWorld&&view.Prompt!=null;
        if(show&&view.Revision!=yesNoShown){yesNoShown=view.Revision;yesNoLine.Text=view.Prompt!.Text+(PlayerChatFocused?"":"　按 Enter 開啟輸入列。");}
        bool panel=AttachYesNoPanel()!=null;
        yesNoLine.Visible=show;yesNoYes.Visible=yesNoNo.Visible=show&&!panel;if(!show){yesNoShown=-1;yesNoView=null;return;}
        yesNoView=view;
        int width=Math.Min(640,Math.Max(200,ClientSize.Width-16));
        yesNoLine.SetBounds(8,Math.Max(0,ClientSize.Height-bar.Height-yesNoLine.Height-6),width,yesNoLine.Height);yesNoLine.BringToFront();
        if(panel)return;
        // Native canvas without a usable YesNoPanel (faulted): keep the WinForms buttons at the panel's client rectangle.
        var native=NativeUiActive?NativeUiViewport():(P06.NativeUi.UiViewport?)null;
        foreach(var (b,accept) in new[]{(yesNoYes,true),(yesNoNo,false)}){
            b.Bounds=native is {} v?YesNoButtons.Client(accept,v):YesNoButtons.Legacy(accept,yesNoLine.Bounds);b.BringToFront();
        }
    }
    static TextBox? FocusedTextBox(Control root){
        foreach(Control c in root.Controls){if(c is TextBox t&&t.Focused)return t;if(c.ContainsFocus&&FocusedTextBox(c) is {} f)return f;}
        return null;
    }
}
