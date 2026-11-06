namespace P06;
public sealed partial class ClientForm {
    void OnlineDown(Keys key){
        if(key==Keys.F12&&evidence!=null){CaptureEvidence("manual");return;}
        if(key==Keys.Space){ClearInput("stop");return;}
        if(key==Keys.M){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-magic");Input.Down(key);if(!World.Walking)_=CastFromInput();}return;}
        if(key==Keys.U){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-item");Input.Down(key);if(!World.Walking)_=login.UseSelectedItem();}return;}
        if(key==Keys.P){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-pickup");Input.Down(key);if(!World.Walking)_=NativeGroundQuantity(true);}return;}
        if(key==Keys.G){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-attack");Input.Down(key);_=AttackFromInput();}return;}
        if(login.CombatBusy||!login.MovementAvailable){RejectKeyboardMovement(login.MovementBlockReason);return;}
        bool fresh=!Input.Held.Contains(key);Input.Down(key);
        if(key>=Keys.D1&&key<=Keys.D8){if(fresh&&!World.Walking)_=login.Turn(key-Keys.D1);return;}
        OnlineTick();
    }
    void SetSingleActionNotice(string notice){
        // ClearInput already cancelled the pointer; do not let that older notice hide this result.
        lastPlayerPointerHudNotice=PlayerPointerNotice;
        playerHudNotice=notice;playerHudNoticeUntil=Environment.TickCount64+6000;
    }
    async Task AttackFromInput(){
        int epoch=login.World.WorldEpoch,selection=login.World.TargetRevision;await login.Attack();
        if(epoch!=login.World.WorldEpoch||selection!=login.World.TargetRevision)return;
        SetSingleActionNotice(login.World.CombatNotice);
        SyncPlayerHud();PublishWebState();
    }
    async Task CastFromInput(){
        int epoch=login.World.WorldEpoch,selection=login.World.TargetRevision,skill=login.World.MagicSelectionRevision;await login.CastSelectedMagic();
        if(epoch!=login.World.WorldEpoch||selection!=login.World.TargetRevision||skill!=login.World.MagicSelectionRevision)return;
        SetSingleActionNotice(login.World.MagicNotice);
        SyncPlayerHud();PublishWebState();
    }
    void RejectKeyboardMovement(string notice){
        Input.Clear();SetSingleActionNotice(notice);SyncPlayerHud();PublishWebState();
    }
    void OnlineTick(){
        if(Input.Direction is not int direction)return;
        if(!login.MovementAvailable||login.CombatBusy||login.MagicBusy||login.GroundBusy||login.ItemBusy){
            RejectKeyboardMovement(login.MovementBlockReason);return;
        }
        if(World.Walking||login.MovementBusy)return;
        _=MoveFromKeyboard(direction);
    }
    async Task MoveFromKeyboard(int direction){
        int epoch=login.World.WorldEpoch;var result=await login.RequestMovement(direction,true,World.Walking);
        if(epoch!=login.World.WorldEpoch||result.Sent)return;
        RejectKeyboardMovement(result.Notice);
    }
    async Task OnlineWeb(int id,string cmd,int d){if(cmd=="attack"){bool attacked=await login.Attack();Reply(id,attacked?"單次攻擊已送出，等待原服回報":login.World.CombatNotice);PublishWebState();return;}var result=await login.RequestMovement(d,cmd=="move",World.Walking);Reply(id,result.Notice);PublishWebState();}
}
