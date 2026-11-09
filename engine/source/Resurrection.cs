namespace P06;

public sealed record ResurrectionView(int Generation,int Epoch,int Revision,string Phase,ushort Message,string Notice,bool CanAnswer);
sealed record MoveLockHold(int Epoch,long Position,long Until);

// d287 contract: docs/protocol/resurrection.md. The zero D is not a request ID.
public static class ResurrectionPackets {
    public static bool IsPrompt(ushort message)=>message is 321 or 322;
    public static byte[] Answer(ushort message,bool accept){
        if(!IsPrompt(message))throw new ArgumentOutOfRangeException(nameof(message));
        return LoginPackets.Pad([121,0,0,0,0,0,0,(byte)message,(byte)(message>>8),(byte)(accept?1:0)]);
    }
}

public sealed partial class WorldSession {
    // Engine safety policy, not a server value: S241 has no unlock form, and this client never sends C246.
    public const int MoveLockTimeoutMs=5000;
    internal Func<long> MoveLockClock=()=>Environment.TickCount64;
    volatile MoveLockHold? moveLock;
    // Pure on purpose: the send gate and UI poll this from outside the receive path.
    public bool MoveLocked=>moveLock is {} hold&&hold.Epoch==WorldEpoch&&hold.Position==AuthoritativePositionRevision&&MoveLockClock()<hold.Until;

    void ReceiveMoveLock(LoginReader r){
        byte mode=r.C();r.Finish();
        if(mode!=0){Unhandled(241);return;}
        if(!Ready)return;
        bool held=MoveLocked;
        moveLock=new(WorldEpoch,AuthoritativePositionRevision,MoveLockClock()+MoveLockTimeoutMs);
        if(held)return;
        // Invalidate queued requests even if the hold ends before their send gate.
        AppearanceRevision++;
        MotionNotice="原服要求鎖定移動；等待位置校正，輸入已停止。";
    }

    int resurrectionEpoch=-1,resurrectionRevision;
    ushort resurrectionMessage;
    string resurrectionPhase="idle",resurrectionNotice="";
    void SetResurrection(string phase,string notice){resurrectionPhase=phase;resurrectionNotice=notice;resurrectionRevision++;}
    // Recovery's S85 path clears OwnDead, so a prompt never outlives the death it was sent for.
    void CheckResurrection(){
        if(resurrectionPhase is not ("invited" or "accepting"))return;
        if(resurrectionEpoch!=WorldEpoch)SetResurrection("expired","世界已更新，舊復活確認已失效。");
        else if(!OwnDead)SetResurrection("resurrected","角色已恢復；外觀與生命值以原服回報為準。");
        else if(RestartRequested&&resurrectionPhase=="invited")SetResurrection("expired","已要求死亡後重新開始，舊復活確認已失效。");
    }
    // Own reader, before Party and Duel consume their messages: every S219 replaces the shared Yes-No slot.
    // Returns nothing so Trade's existing non-252 path still expires older trade, marriage and clan prompts.
    void ObserveResurrectionPrompt(byte[] plain){
        if(plain.Length<9)return; // Too short for an envelope; the existing S219 readers reject it.
        var r=new LoginReader(plain);r.C();
        ushort header=r.H();uint reserved=r.D();ushort message=r.H();
        if(!ResurrectionPackets.IsPrompt(message)){
            CheckResurrection();
            if(header==0&&resurrectionPhase=="invited")SetResurrection("expired","收到其他確認對話，舊復活確認已失效。");
            return;
        }
        r.Finish();
        if(header!=0||reserved!=0)throw new ProtocolException("resurrection confirmation envelope");
        CheckResurrection();
        if(!Ready||!OwnDead||RestartRequested){if(resurrectionPhase=="invited")SetResurrection("expired","角色狀態已改變，舊復活確認已失效。");return;}
        resurrectionEpoch=WorldEpoch;resurrectionMessage=message;
        SetResurrection("invited",message==322?"有人要讓你復活；請在聊天輸入列回覆 Y／N。":"有人要讓你復活。是否接受？");
    }
    internal ResurrectionView ResurrectionSnapshot(int generation,bool access){
        CheckResurrection();
        return new(generation,WorldEpoch,resurrectionRevision,resurrectionPhase,resurrectionMessage,resurrectionNotice,access&&resurrectionPhase=="invited");
    }
    internal bool ResurrectionCurrent(ResurrectionView v){
        CheckResurrection();
        return v.Epoch==WorldEpoch&&v.Revision==resurrectionRevision&&v.Message==resurrectionMessage&&resurrectionPhase=="invited";
    }
    internal void ResurrectionSent(bool accept){
        SetResurrection(accept?"accepting":"rejected",accept?"已接受復活，等待原服回報；不會自動重送。":"已拒絕復活；原服不另回覆，仍可選擇重新開始。");
        YesNoHandled(resurrectionMessage);
    }
    internal string ResurrectionNotice=>resurrectionNotice;
    // R2-J (#624): 322 is answered from the shared input-line slot (YesNoPrompt.cs, WO §6); 321 keeps this module's panel
    // because S_ASK lists it as a special case (WIN §2.1). The slot asks this before queuing and on every read.
    internal bool ResurrectionAwaitsAnswer(ushort message){CheckResurrection();return resurrectionPhase=="invited"&&resurrectionMessage==message&&resurrectionEpoch==WorldEpoch;}
    internal static bool ResurrectionUsesPanel(ResurrectionView v)=>v.Phase=="invited"&&v.Message!=322;
}

public sealed partial class LoginConnection {
    int resurrectionSendingGeneration=-1;
    bool ResurrectionAccess=>State==LoginState.InWorld&&World.Ready&&!World.TeleportPending&&sendMovementGate!=null;
    public ResurrectionView ResurrectionSnapshot()=>CaptureWorld(()=>World.ResurrectionSnapshot(generation,ResurrectionAccess&&resurrectionSendingGeneration!=generation));
    public async Task<bool> AnswerResurrection(ResurrectionView shown,bool accept){
        int session=generation;
        bool Eligible()=>shown.Generation==generation&&session==generation&&ResurrectionAccess&&World.ResurrectionCurrent(shown);
        if(resurrectionSendingGeneration==session||!CaptureWorld(Eligible))return false;
        var sender=sendMovementGate!;resurrectionSendingGeneration=session;
        try{
            bool sent=await sender(ResurrectionPackets.Answer(shown.Message,accept),()=>CaptureWorld(Eligible),()=>{World.ResurrectionSent(accept);Set(State,World.ResurrectionNotice);});
            return sent&&session==generation;
        }catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"復活確認傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(resurrectionSendingGeneration==session)resurrectionSendingGeneration=-1;}
    }
}

public sealed partial class ClientForm {
    readonly Panel resurrectionPanel=new(){Size=new(410,84),BackColor=Color.FromArgb(32,40,32),Visible=false};
    readonly Label resurrectionNotice=new(){Location=new(10,8),Size=new(390,30),ForeColor=Color.White};
    readonly Button resurrectionYes=new(){Text="接受復活",Location=new(60,42),Size=new(130,32)},resurrectionNo=new(){Text="拒絕",Location=new(220,42),Size=new(130,32)};
    ResurrectionView? resurrectionShown;
    void InitResurrection(){
        resurrectionPanel.Controls.AddRange([resurrectionNotice,resurrectionYes,resurrectionNo]);Controls.Add(resurrectionPanel);
        resurrectionYes.Click+=async(_,_)=>await AnswerResurrectionPrompt(true);
        resurrectionNo.Click+=async(_,_)=>await AnswerResurrectionPrompt(false);
        var refresh=new System.Windows.Forms.Timer{Interval=150};
        refresh.Tick+=(_,_)=>SyncResurrection();
        FormClosed+=(_,_)=>refresh.Dispose();refresh.Start();
    }
    void SyncResurrection(){
        var view=login.ResurrectionSnapshot();resurrectionShown=view;
        resurrectionPanel.Visible=login.State==LoginState.InWorld&&WorldSession.ResurrectionUsesPanel(view);
        if(!resurrectionPanel.Visible)return;
        // Directly below the death panel, so "restart" stays reachable while the prompt is open.
        resurrectionPanel.Location=new(Math.Max(0,(ClientSize.Width-resurrectionPanel.Width)/2),262);
        resurrectionNotice.Text=view.Notice;resurrectionYes.Enabled=resurrectionNo.Enabled=view.CanAnswer;resurrectionPanel.BringToFront();
    }
    async Task AnswerResurrectionPrompt(bool accept){
        if(resurrectionShown is not {} view)return;
        ClearInput("resurrection-answer");await login.AnswerResurrection(view,accept);SyncResurrection();SyncRecovery();
    }
}
