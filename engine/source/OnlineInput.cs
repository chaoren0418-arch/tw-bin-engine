namespace P06;
public sealed partial class ClientForm {
    void OnlineDown(Keys key){
        if(key==Keys.F12&&evidence!=null){CaptureEvidence("manual");return;}
        if(key==Keys.Space){ClearInput("stop");return;}
        if(login.World.AfflictionInputLocked){RejectKeyboardMovement("異常狀態中，請等待原服解除後再操作。");return;}
        // M7-N（WO §4）：幽靈中原版本機擋道具／技能快捷與攻擊；不送封包。P（拾取）由 M7-L 擋，移動照常。
        if((key is Keys.M or Keys.U or Keys.G)&&login.GhostLocalBlocked){RejectKeyboardMovement(GhostLocalLimits.Notice);return;}
        if(key==Keys.M){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-magic");Input.Down(key);if(!World.Walking)_=CastFromInput();}return;}
        if(key==Keys.U){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-item");Input.Down(key);if(!World.Walking)_=login.UseSelectedItem();}return;}
        if(key==Keys.P){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-pickup");Input.Down(key);if(!World.Walking)_=NativeGroundQuantity(true);}return;}
        if(key==Keys.G){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-attack");Input.Down(key);_=AttackFromInput();}return;}
        if(login.CombatBusy||!login.MovementAvailable){RejectKeyboardMovement(login.MovementBlockReason);return;}
        bool fresh=!Input.Held.Contains(key);Input.Down(key);
        if(key>=Keys.D1&&key<=Keys.D8){if(fresh&&!World.Walking)_=login.Turn(key-Keys.D1);return;}
        OnlineTick();
    }
    // R6-D：F4＝Action_Loot（sub_500640，UI§2 V5 (b)）。原生 UI 與舊 HUD 都走這裡；和 P 鍵同一條 LootNearby，
    // 轉向、整疊、不問數量、不走近、與點擊共用 986／600 ms 節流都在 Ground.cs 的 SendGround。
    bool PlayerLootKeyActive=>OnlineMode&&login.State==LoginState.InWorld;
    void LootKeyFromInput(Keys key){
        if(playerPointer?.Active==true)StopPlayerPointer("鍵盤操作");
        if(Input.Held.Contains(key))return;   // 自動重複不再觸發；放開（HandleUp）後才算下一次
        if(login.World.AfflictionInputLocked){RejectKeyboardMovement("異常狀態中，請等待原服解除後再操作。");return;}
        ClearInput("single-pickup");Input.Down(key);
        if(!World.Walking)_=LootFromInput();
    }
    async Task LootFromInput(){
        int epoch=login.World.WorldEpoch;bool sent=await login.LootNearby();
        if(epoch!=login.World.WorldEpoch)return;
        if(!sent)SetSingleActionNotice(login.World.GroundNotice);
        SyncPlayerHud();PublishWebState();
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
    /// <summary>
    /// R2-D（#615）：HyperText 動作入口，給 R2-F 的 NPC 對話 HTML 呼叫。true＝已處理（目前只有 exitghost，WO §4），呼叫端不要再送 C_DIALOG。
    /// </summary>
    internal bool HyperAction(string action){
        if(!login.TryHyperAction(action,out var sending))return false;
        ClearInput("hyper-"+GhostModePackets.ExitAction);_=FinishHyperAction(sending);
        return true;
    }
    async Task FinishHyperAction(Task<bool> sending){
        bool sent=await sending;
        var ghost=login.GhostModeSnapshot();
        SetSingleActionNotice(sent?"已送出離開幽靈請求（C173），等待原服更新。":ghost.Active?ghost.Notice:"目前不是幽靈模式，未送出。");
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
    async Task OnlineWeb(int id,string cmd,int d){if(cmd=="attack"&&login.GhostLocalBlocked){Reply(id,GhostLocalLimits.Notice);PublishWebState();return;}if(cmd=="attack"){bool attacked=await login.Attack();Reply(id,attacked?"單次攻擊已送出，等待原服回報":login.World.CombatNotice);PublishWebState();return;}var result=await login.RequestMovement(d,cmd=="move",World.Walking);Reply(id,result.Notice);PublishWebState();}
}
