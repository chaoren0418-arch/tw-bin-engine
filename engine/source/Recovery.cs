namespace P06;

public sealed partial class WorldSession {
    public bool RestartRequested {get;internal set;}
    bool recoveryAppearancePending;
    internal void ResetRecovery(){RestartRequested=false;recoveryAppearancePending=false;}
    void CompleteRecoveryAppearance(){if(!recoveryAppearancePending)return;recoveryAppearancePending=false;SelfAppearancePending=false;}
    bool ReceiveRecovery(byte opcode,LoginReader r){
        if(opcode!=85)return false;
        uint id=r.D();byte type=r.C();r.D();uint gfx=r.D();r.Finish();
        if(type!=0||gfx>ushort.MaxValue){Unhandled(opcode);return true;}
        if(Actor?.ObjectId==id&&OwnDead){
            CancelPickupPresentation("resurrection");pendingAction=null;OwnCombat=null;OwnDead=false;RestartRequested=false;
            Actor=Actor with{Gfx=(ushort)gfx};AppearanceRevision++;
            // S85 has no weapon or HP. Wait for S119 and positive authoritative HP.
            recoveryAppearancePending=true;SelfAppearancePending=true;
            CombatNotice="收到復活回報；等待角色外觀與生命值同步。";
        }else UpdateObject(id,o=>o.Dead?o with{Dead=false,Combat=null,ReceivedAction=null,Gfx=(ushort)gfx,MotionAt=0}:o);
        return true;
    }
}

public sealed partial class LoginConnection {
    bool restartSending;
    public bool CanRestart=>State==LoginState.InWorld&&World.OwnDead&&World.Actor!=null&&!World.RestartRequested&&!restartSending&&!World.TeleportPending&&sendMovementGate!=null;
    public async Task<bool> RestartAfterDeath(){
        if(!CanRestart)return false;
        int session=generation,epoch=World.WorldEpoch,appearance=World.AppearanceRevision;
        uint id=World.Actor!.ObjectId;var sender=sendMovementGate!;restartSending=true;
        bool Current()=>session==generation&&epoch==World.WorldEpoch&&appearance==World.AppearanceRevision&&World.Actor?.ObjectId==id;
        try{
            bool sent=await sender(LoginPackets.Pad([177]),()=>Current()&&State==LoginState.InWorld&&World.OwnDead&&!World.RestartRequested&&!World.TeleportPending,()=>{
                World.RestartRequested=true;CancelMovementInput();World.CombatNotice="已要求死亡後重新開始，等待伺服器恢復；不會自動重送。";Set(State,World.CombatNotice);
            });
            return sent&&session==generation;
        }catch(Exception ex){if(Current()){Cancel();Set(LoginState.Faulted,"重新開始傳送失敗："+ex.Message);}return false;}
        finally{if(session==generation)restartSending=false;}
    }
}

public sealed partial class ClientForm {
    readonly Panel recoveryPanel=new(){Size=new(410,96),BackColor=Color.FromArgb(40,32,32),Visible=false};
    readonly Label recoveryNotice=new(){Location=new(10,8),Size=new(390,38),ForeColor=Color.White};
    readonly Button restartButton=new(){Text="死亡後重新開始",Location=new(110,52),Size=new(190,32)};
    void InitRecovery(){
        recoveryPanel.Controls.AddRange([recoveryNotice,restartButton]);Controls.Add(recoveryPanel);
        restartButton.Click+=async(_,_)=>{ClearInput("restart-after-death");await login.RestartAfterDeath();SyncRecovery();};
    }
    void SyncRecovery(){
        recoveryPanel.Visible=login.State==LoginState.InWorld&&(login.World.OwnDead||login.World.Status?.Hp==0);
        if(!recoveryPanel.Visible)return;
        recoveryPanel.Location=new(Math.Max(0,(ClientSize.Width-recoveryPanel.Width)/2),160);
        recoveryNotice.Text=login.World.RestartRequested?"已送出重新開始，等待伺服器恢復。":"角色已死亡，移動與戰鬥已停止。可選擇重新開始。";
        restartButton.Enabled=login.CanRestart;recoveryPanel.BringToFront();
    }
}
