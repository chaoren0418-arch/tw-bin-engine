namespace P06;

public sealed record FishingView(int Generation,int Epoch,uint Actor,int Revision,bool Active,bool CancelPending,string Notice);

public static class FishingPackets {
    // d287 C_FishClick does not read any fields; it only cancels fishing.
    public static byte[] Cancel()=>LoginPackets.Pad([62]);
}

public sealed partial class WorldSession {
    int fishingEpoch=-1,fishingRevision;
    uint fishingActor;
    bool fishingActive,fishingCancelPending;
    long fishingCancelAt;
    string fishingNotice="";
    void CheckFishing(){
        uint actor=Actor?.ObjectId??0;
        if(fishingEpoch!=WorldEpoch||fishingActor!=actor){
            fishingEpoch=WorldEpoch;fishingActor=actor;fishingRevision++;
            fishingActive=fishingCancelPending=false;
            fishingNotice="";
        }
        if(fishingCancelPending&&ActionClock()-fishingCancelAt>=5000){
            fishingCancelPending=false;fishingRevision++;
            fishingNotice="收竿尚未確認；可手動再按收竿。";
        }
    }
    public FishingView ReadFishing(int generation){
        CheckFishing();return new(generation,WorldEpoch,fishingActor,fishingRevision,fishingActive,fishingCancelPending,fishingNotice);
    }
    internal bool CurrentFishing(FishingView view){
        CheckFishing();return view.Epoch==WorldEpoch&&view.Actor==fishingActor&&view.Revision==fishingRevision&&fishingActive&&!fishingCancelPending;
    }
    internal void FishingCancelSent(){
        fishingCancelPending=true;fishingCancelAt=ActionClock();fishingRevision++;
        fishingNotice="收竿請求已送出，等待伺服器確認。";
    }
    void EndFishing(string notice){fishingActive=fishingCancelPending=false;fishingRevision++;fishingNotice=notice;}

    // Observe with an independent reader: shared S71/S119/S158 still reach their existing handlers.
    // No S250 fishing subtype exists on the verified d287 path (docs/protocol/fishing.md).
    internal void ObserveFishing(byte[] plain){
        CheckFishing();
        var r=new LoginReader(plain);byte opcode=r.C();
        if(opcode==158){
            uint id=r.D();byte action=r.C();if(action!=71)return;
            ushort x=r.H(),y=r.H();r.Finish();
            if(id!=fishingActor||id==0)return;
            fishingActive=true;fishingCancelPending=false;fishingRevision++;
            fishingNotice=$"已開始釣魚（{x}, {y}），等待伺服器回報。";
        }else if(opcode==119){
            uint id=r.D();r.C();r.Finish();
            if(fishingActive&&id==fishingActor)EndFishing("釣魚已結束（收到角色外觀更新）。");
        }else if(opcode==71){
            ushort message=r.H();
            if(message is not (1137 or 1138 or 1163 or 403))return;
            int count=r.C();var args=new string[count];for(int i=0;i<count;i++)args[i]=r.S();r.Finish();
            if(fishingActor==0)return;
            if(message==403){
                // 403 is a generic grant, without a fishing correlation ID. Do not label it a catch.
                if(fishingActive&&count==1&&!string.IsNullOrWhiteSpace(args[0])){
                    fishingNotice="釣魚期間收到："+ServerMessageText.Decode(message,args).Text+"（仍等待後續回報）";
                    fishingRevision++;
                }
            }else if(count==0){
                if(message==1163)EndFishing("釣魚已經結束了。");
                else {fishingNotice=message==1137?"釣魚必須有魚餌。":"無法在這個地區使用釣竿。";fishingRevision++;}
            }
        }
    }
}

public sealed partial class LoginConnection {
    public FishingView ReadFishing()=>World.ReadFishing(generation);
    public async Task<bool> CancelFishing(FishingView view){
        bool Current()=>generation==view.Generation&&State==LoginState.InWorld&&World.Ready&&!World.OwnDead
            &&World.Status?.Hp>0&&World.CurrentFishing(view);
        if(!Current()||sendMovementGate==null||itemSending)return false;
        var sender=sendMovementGate;int lease=++itemLease;
        bool Owner()=>generation==view.Generation&&World.WorldEpoch==view.Epoch&&lease==itemLease;
        itemSending=true;
        try{return await sender(FishingPackets.Cancel(),()=>Owner()&&Current(),()=>World.FishingCancelSent())&&Owner();}
        catch(Exception ex){if(Owner()){Cancel();Set(LoginState.Faulted,"收竿傳送失敗："+ex.Message);}return false;}
        finally{if(Owner())itemSending=false;}
    }
}

// One HUD mount; all fishing control state and events stay in this module.
public sealed class FishingHud:Button {
    readonly ToolTip tips=new(){AutoPopDelay=20000,ShowAlways=true};
    ClientForm? owner;FishingView? view;
    public FishingHud(){
        Width=96;Height=30;Margin=new(3,2,3,2);FlatStyle=FlatStyle.Flat;
        BackColor=PlayerHudPainter.Panel;ForeColor=Color.White;
        Click+=async(_,_)=>{if(owner is {} form&&view is {} shown){Enabled=false;await form.CancelFishingHud(shown);}};
    }
    public static void Update(FlowLayoutPanel menu,Label notice,ClientForm? form){
        var control=menu.Controls.OfType<FishingHud>().FirstOrDefault();
        if(control==null){control=new();menu.Controls.Add(control);}
        control.owner=form;control.view=form?.ReadFishingHud();var state=control.view;
        control.Text=state?.CancelPending==true?"等待收竿":state?.Active==true?"釣魚中／收竿":"釣魚狀態";
        control.Enabled=state is {Active:true,CancelPending:false};
        control.AccessibleName=control.Text+"："+state?.Notice;
        control.tips.SetToolTip(control,control.AccessibleName);
        if(state is {Notice.Length:>0}){notice.Text=state.Notice;notice.AccessibleName=state.Notice;control.tips.SetToolTip(notice,state.Notice);}
    }
    protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
}

public sealed partial class ClientForm {
    internal FishingView ReadFishingHud()=>login.ReadFishing();
    internal async Task CancelFishingHud(FishingView view){ClearInput("fishing-cancel");await login.CancelFishing(view);}
}
