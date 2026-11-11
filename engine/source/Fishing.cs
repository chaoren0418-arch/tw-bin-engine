namespace P06;

public sealed record FishingView(int Generation,int Epoch,uint Actor,int Revision,bool Active,bool CancelPending,string Notice);

/// <summary>T434（#992，G11）：1516 回答「是」之後還能不能收竿——同一次連線、同一個世界、同一個角色，而且仍在釣魚、還沒送收竿。</summary>
public static class FishingQuitRules {
    public static bool StillCurrent(FishingView asked,FishingView now)=>
        now is {Active:true,CancelPending:false}&&now.Generation==asked.Generation&&now.Epoch==asked.Epoch&&now.Actor==asked.Actor;
}

public static class FishingPackets {
    // d287 C_FishClick does not read any fields; it only cancels fishing.
    public static byte[] Cancel()=>LoginPackets.Pad([62]);
}

/// <summary>T329 (#767): own fishing state after a change (start, notice, cancel sent, end).</summary>
public sealed class FishingWindowEventArgs(uint actor,bool active,bool cancelPending,string notice,int revision):EventArgs {
    public uint Actor=>actor;
    public bool Active=>active;
    public bool CancelPending=>cancelPending;
    public string Notice=>notice;
    public int Revision=>revision;
}

// T329 (#767): open-window event for the native fishing window (NativeUi/FishingPanel). With --classic-ui (or when the
// reference FishingUI.xml is unavailable) nobody listens and the legacy FishingHud button stays the only entry.
// Raised on the thread that received the packet (network) or sent the cancel; handlers must only queue.
public static class FishingWindow {
    public static event EventHandler<FishingWindowEventArgs>? Changed;
    internal static void Raise(FishingWindowEventArgs e){
        if(Changed is not {} handlers)return;
        foreach(EventHandler<FishingWindowEventArgs> h in handlers.GetInvocationList()){
            try{h(null,e);}catch(Exception ex){NativeUi.UiResourceLog.Write("fishing-ui 開窗事件失敗（舊式釣魚鈕照舊）："+ex.Message);}
        }
    }
    /// <summary>QuitFishing of the native window: the legacy HUD's C62 path (UI thread). false = nothing sent.</summary>
    public static bool Quit()=>ClientForm.QuitFishingFromNativeUi();
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
        RaiseFishingWindow();
    }
    void RaiseFishingWindow()=>FishingWindow.Raise(new(fishingActor,fishingActive,fishingCancelPending,fishingNotice,fishingRevision));
    void EndFishing(string notice){fishingActive=fishingCancelPending=false;fishingRevision++;fishingNotice=notice;}

    // Observe with an independent reader: shared S71/S119/S158 still reach their existing handlers.
    // No S250 fishing subtype exists on the verified d287 path (docs/protocol/fishing.md).
    internal void ObserveFishing(byte[] plain){
        int before=fishingRevision;
        try{ObserveFishingCore(plain);}
        finally{if(fishingRevision!=before)RaiseFishingWindow();}   // T329 (#767): open-window event
    }
    void ObserveFishingCore(byte[] plain){
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
        Click+=async(_,_)=>{if(owner is {} form&&view is {} shown){Enabled=false;if(!await form.ConfirmCancelFishingHud(shown))Enabled=form.ReadFishingHud() is {Active:true,CancelPending:false};}};
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
    /// <summary>
    /// T434（#992，classic-ui-audit G11）：classic HUD 的收竿鈕照原生 QuitFishing（sub_E659A0）先問 1516，「是」才送 C62；
    /// 回答期間釣魚已結束或已送收竿（view 不再是目前的）就不送。原生 UI 開著時不問（原生窗自己問）。
    /// </summary>
    internal async Task<bool> ConfirmCancelFishingHud(FishingView view){
        if(!NativeUiActive&&!ClassicConfirm.Ask(this,P06.NativeUi.FishingQuitText.Confirm))return false;
        var now=ReadFishingHud();
        if(!FishingQuitRules.StillCurrent(view,now))return false;
        await CancelFishingHud(now);return true;
    }
    /// <summary>T329 (#767): the native window's QuitFishing → the same C62 path as the HUD button. UI thread only.</summary>
    internal static bool QuitFishingFromNativeUi(){
        foreach(var form in Application.OpenForms.OfType<ClientForm>()){
            if(form.IsDisposed||!form.NativeUiActive)continue;
            var view=form.ReadFishingHud();
            if(!view.Active||view.CancelPending)return false;
            _=form.CancelFishingHud(view);return true;
        }
        return false;
    }
}
