namespace P06;

public sealed record GhostModeView(PlayerHudStamp Stamp,int Revision,bool Active,bool CanExit,bool Waiting,string Notice);

public static class GhostModePackets {
    // d287 C_ExitGhost reads no payload. Padding belongs to the existing wire transport.
    public static byte[] Exit()=>LoginPackets.Pad([173]);
}

public sealed partial class WorldSession {
    public bool IsGhost=>Actor is {} a&&a.ObjectId==Status?.ObjectId&&(a.Flags&0x80)!=0;
    public int GhostRevision {get;private set;}
    // Called only after the complete self S87 and its identity/heading have been validated.
    // No guessed gfx, death transition, NPC permission or local clear on C173.
    void ReceiveGhostState(OwnActor next){if(Actor==null||Actor.ObjectId!=next.ObjectId||((Actor.Flags^next.Flags)&0x80)!=0)GhostRevision++;}
}

public sealed partial class LoginConnection {
    GhostModeView? ghostSending,ghostSubmitted;
    long ghostSubmittedAt;
    internal Func<long> GhostClock=()=>Environment.TickCount64;
    bool CurrentGhost(GhostModeView v)=>v.Stamp==PlayerHudActions.Stamp(this)&&v.Revision==World.GhostRevision&&World.IsGhost;
    // Exit must remain possible when the supplied map/gfx has no local renderer.
    bool GhostExitAccess=>State==LoginState.InWorld&&World.Ready&&World.IsGhost&&!World.TeleportPending;
    public GhostModeView GhostModeSnapshot(){
        bool active=State==LoginState.InWorld&&World.IsGhost;
        bool sending=ghostSending is {} queued&&CurrentGhost(queued);
        bool submitted=ghostSubmitted is {} sent&&CurrentGhost(sent);
        bool waiting=sending||submitted&&GhostClock()-ghostSubmittedAt<15000;
        string notice=!active?"":sending?"幽靈模式：正在送出離開請求。":submitted?
            waiting?"幽靈模式：已要求離開，等待原服更新。":"幽靈模式：離開結果尚未確認，可手動重試。":
            "幽靈模式：外觀與動作依原服狀態；可要求離開。";
        return new(PlayerHudActions.Stamp(this),World.GhostRevision,active,GhostExitAccess&&!waiting&&sendMovementGate!=null,waiting,notice);
    }
    public async Task<bool> ExitGhost(GhostModeView shown){
        if(!shown.Active||!shown.CanExit||!CurrentGhost(shown)||!GhostModeSnapshot().CanExit||sendMovementGate==null)return false;
        var sender=sendMovementGate;var attempt=shown with{};ghostSending=attempt;
        try{
            bool Valid()=>ReferenceEquals(ghostSending,attempt)&&CurrentGhost(attempt)&&GhostExitAccess;
            bool sent=await sender(GhostModePackets.Exit(),Valid,()=>{ghostSubmitted=attempt;ghostSubmittedAt=GhostClock();});
            return sent&&shown.Stamp.Generation==SessionGeneration;
        }catch(Exception){
            if(shown.Stamp.Generation==SessionGeneration){Cancel();Set(LoginState.Faulted,"離開幽靈模式傳送失敗，已斷線；沒有自動重送。");}
            return false;
        }finally{if(ReferenceEquals(ghostSending,attempt))ghostSending=null;}
    }
}

public sealed partial class ClientForm {
    internal GhostModeView? ReadGhostMode()=>PlayerHudActive?login.GhostModeSnapshot():null;
    internal async Task ExitGhostFromHud(GhostModeView shown){
        if(!PlayerHudActive||!PlayerHudActions.Current(login,shown.Stamp))return;
        ClearInput("exit-ghost");
        try{await login.ExitGhost(shown);}finally{if(!IsDisposed){SyncPlayerHud();PublishWebState();}}
    }
}

// One HUD mount; the view retains the state actually shown when the player clicks.
internal sealed class GhostModeHud {
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlayerHudView,GhostModeHud> mounts=new();
    readonly Button exit=new(){Text="離開幽靈",AccessibleName="離開幽靈模式",Width=94,Height=30,Margin=new(3,2,3,2),FlatStyle=FlatStyle.Flat,BackColor=PlayerHudPainter.Panel,ForeColor=Color.White,Visible=false};
    GhostModeView? shown;
    GhostModeHud(PlayerHudView hud,FlowLayoutPanel menu){
        menu.Controls.Add(exit);
        exit.Click+=async(_,_)=>{if(shown is {} view&&hud.FindForm() is ClientForm form)await form.ExitGhostFromHud(view);};
    }
    internal static PlayerHudSnapshot Update(PlayerHudView hud,FlowLayoutPanel menu,PlayerHudSnapshot state){
        var mount=mounts.GetValue(hud,_=>new(hud,menu));
        var view=(hud.FindForm() as ClientForm)?.ReadGhostMode();
        if(view?.Stamp!=state.Stamp)view=null;
        mount.shown=view;mount.exit.Visible=view?.Active==true;mount.exit.Enabled=view?.CanExit==true;
        mount.exit.AccessibleDescription=view?.Notice??"";
        return view?.Active==true?state with{State="幽靈模式 · 行動由原服判定",Notice=view.Notice+(state.Notice.Length>0?" · "+state.Notice:"")}:state;
    }
}
