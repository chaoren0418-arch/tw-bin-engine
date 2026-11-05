namespace P06;
public sealed partial class ClientForm {
    void OnlineDown(Keys key){
        if(key==Keys.F12&&evidence!=null){CaptureEvidence("manual");return;}
        if(key==Keys.Space){ClearInput("stop");return;}
        if(key==Keys.M){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-magic");Input.Down(key);if(!World.Walking)_=login.CastSelectedMagic();}return;}
        if(key==Keys.U){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-item");Input.Down(key);if(!World.Walking)_=login.UseSelectedItem();}return;}
        if(key==Keys.P){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-pickup");Input.Down(key);if(!World.Walking)_=NativeGroundQuantity(true);}return;}
        if(key==Keys.G){bool first=!Input.Held.Contains(key);if(first){ClearInput("single-attack");Input.Down(key);_=login.Attack();}return;}
        if(login.CombatBusy)return;
        if(!login.MovementAvailable)return;
        bool fresh=!Input.Held.Contains(key);Input.Down(key);
        if(key>=Keys.D1&&key<=Keys.D8){if(fresh&&!World.Walking)_=login.Turn(key-Keys.D1);return;}
        OnlineTick();
    }
    void OnlineTick(){if(!login.MovementAvailable||World.Walking||login.MovementBusy)return;if(Input.Direction is int d)_=login.Move(d);}
    async Task OnlineWeb(int id,string cmd,int d){if(cmd=="attack"){bool attacked=!World.Walking&&await login.Attack();Reply(id,attacked?"單次攻擊已送出，等待原服回報":login.World.CombatNotice);PublishWebState();return;}var result=await login.RequestMovement(d,cmd=="move",World.Walking);Reply(id,result.Notice);PublishWebState();}
}
