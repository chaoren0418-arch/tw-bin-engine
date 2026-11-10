namespace P06;

public sealed record ResurrectionView(int Generation,int Epoch,int Revision,string Phase,ushort Message,string Notice,bool CanAnswer);
sealed record MoveLockHold(int Epoch,long Position);

// d287 contract: docs/protocol/resurrection.md. The zero D is not a request ID.
public static class ResurrectionPackets {
    public static bool IsPrompt(ushort message)=>message is 321 or 322;
    public static byte[] Answer(ushort message,bool accept){
        if(!IsPrompt(message))throw new ArgumentOutOfRangeException(nameof(message));
        return LoginPackets.Pad([121,0,0,0,0,0,0,(byte)message,(byte)(message>>8),(byte)(accept?1:0)]);
    }
}

public sealed partial class WorldSession {
    // S241 has no unlock form. R5-D (T309/#736, gap-matrix 14.6): the engine's own 5-second safety limit is gone; the lock
    // lasts until the next authoritative own position (S87), a world rebuild or Reset. Item use (e.g. a teleport scroll)
    // and restart do not depend on MovementAvailable, so a character is never stuck without a way out.
    volatile MoveLockHold? moveLock;
    /// <summary>S241 lock alone (the emote hold is separate).</summary>
    public bool ServerMoveLocked=>moveLock is {} hold&&hold.Epoch==WorldEpoch&&hold.Position==AuthoritativePositionRevision;
    // Pure on purpose: the send gate and UI poll this from outside the receive path.
    // R10-G (T391/#906, gap-matrix 1:92, AN§1): the emote hold (byte_153CE6A, EmoteHeld) and the 700 ms tail
    // (byte_153CE69, EmoteSettling) only pause held-input continuation (0xDF5401–0xDF5416, checked before sub_A4C270(0)).
    // They are no longer part of MoveLocked, so MovementAvailable (attack, magic, doors, portals) is not blocked and
    // ClientLogin's "unsupported-movement-state" no longer clears held input; the held step waits in
    // LoginConnection.RequestMovement (OnlineMovement.cs) instead.
    public bool MoveLocked=>ServerMoveLocked;

    void ReceiveMoveLock(LoginReader r){
        byte mode=r.C();r.Finish();
        if(mode!=0){Unhandled(241);return;}
        if(!Ready)return;
        bool held=ServerMoveLocked;
        moveLock=new(WorldEpoch,AuthoritativePositionRevision);
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
        // R7-B: both 321 and 322 are answered from the shared input line (WO §6).
        SetResurrection("invited","有人要讓你復活；請在聊天輸入列回覆 Y／N。");
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
    // R2-J (#624) moved 322 to the shared input-line slot (YesNoPrompt.cs). R7-B (T341/#804): 321 too. S_ASK lists 321 as a
    // special case only because it reads no string (WIN §2.1); WO §6 (verified) shows 321 going straight to sub_4FC710's
    // input line and 322 reading one `s` first. The slot asks this before queuing and on every read.
    internal bool ResurrectionAwaitsAnswer(ushort message){CheckResurrection();return resurrectionPhase=="invited"&&resurrectionMessage==message&&resurrectionEpoch==WorldEpoch;}
    /// <summary>No resurrection prompt opens a module panel any more (R7-B); kept so callers can assert it.</summary>
    internal static bool ResurrectionUsesPanel(ResurrectionView v)=>false;
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
    // R7-B (T341/#804): the WinForms 321 panel is gone. Both resurrection prompts use the shared input line (YesNoPrompt.cs),
    // which has its own WinForms entry under --classic-ui (prompt label + YES／NO buttons) and the native YesNoPanel otherwise.
    // Recovery.cs still calls this during start-up; there is nothing left to build.
    void InitResurrection(){}
}
