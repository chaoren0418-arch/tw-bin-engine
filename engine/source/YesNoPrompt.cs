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
    // Generic in the original, but still owned by engine modules outside M7-J's file scope
    // (clan join 97 -> ClanUi, clan war 221/222 -> ClanWar, resurrection 322 -> Resurrection).
    // They keep their current UI until their own tasks move them here.
    static readonly HashSet<ushort> delegated=[97,221,222,322];
    // Party, duel and marriage: queued only after their module admitted the invitation.
    public static bool ModuleOwned(ushort message)=>message is 953 or 630 or 654;
    public static bool Special(ushort message)=>special.Contains(message);
    public static bool Generic(ushort message)=>!special.Contains(message)&&!delegated.Contains(message);
    // Only these four have an automatic reply on timeout (windows.md T141).
    public static int? TimeoutMs(ushort message)=>message switch{729=>12000,1232 or 1256 or 1268=>10000,_=>null};
    // C121 H0 D(request id echoed) H(message) C(answer). d287 sends D=0 except party's challenge; never the 622 requestId path.
    public static byte[] Answer(uint requestId,ushort message,bool accept)=>
        LoginPackets.Pad([121,0,0,..BitConverter.GetBytes(requestId),(byte)message,(byte)(message>>8),(byte)(accept?1:0)]);
    // sub_A58380: a leading y/Y (or full-width) accepts; anything else, including empty, declines.
    public static bool Accepts(string typed){string t=typed.TrimStart();return t.Length>0&&t[0] is 'y' or 'Y' or 'ｙ' or 'Ｙ';}
    // Engine wording: the local string table text is not loaded for S219 (標示為引擎自製文字).
    public static string Text(ushort message,string[] args){
        string first=args.Length>0?args[0]:"";
        string body=message switch{
            953 when first.Length>0=>first+" 邀請你加入隊伍。",
            630 when first.Length>0=>first+" 要與你決鬥。",
            654 when first.Length>0=>first+" 向你求婚。",
            _=>"原服詢問 "+message+(args.Length>0?"："+string.Join("、",args):"。")};
        return body+"（輸入 Y 接受、N 拒絕後按 Enter）";
    }
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
            if(!YesNoProtocol.Generic(ask.Message)||YesNoProtocol.ModuleOwned(ask.Message)||!Ready)return;
        }
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
    void InitYesNoUi(){
        Controls.Add(yesNoLine);
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
        yesNoLine.Visible=show;if(!show){yesNoShown=-1;return;}
        int width=Math.Min(640,Math.Max(200,ClientSize.Width-16));
        yesNoLine.SetBounds(8,Math.Max(0,ClientSize.Height-bar.Height-yesNoLine.Height-6),width,yesNoLine.Height);yesNoLine.BringToFront();
    }
    static TextBox? FocusedTextBox(Control root){
        foreach(Control c in root.Controls){if(c is TextBox t&&t.Focused)return t;if(c.ContainsFocus&&FocusedTextBox(c) is {} f)return f;}
        return null;
    }
}
