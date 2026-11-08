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
    // M7-N（WO §4）：原版 sub_A43B50 在幽靈中對自己的外觀變更（S_RESURRECT、變身等）直接略過。
    // 呼叫點在 Polymorph.cs／Recovery.cs（不屬本組可改範圍），此處只提供判斷，供後續接上。
    // 自身 S87 不受影響：它是原服權威狀態（含幽靈旗標本身）。
    internal bool GhostSkipsSelfAppearance(uint objectId)=>IsGhost&&Actor?.ObjectId==objectId;
}

// M7-N（WO §4）：byte_13CE51E==2 時原版本機直接擋下的功能。門、NPC、移動仍照常；拾取由 M7-L（Ground.cs）擋。
public static class GhostLocalLimits {
    public const string Notice="幽靈模式中無法使用快捷、攻擊或道具／技能。";
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
            "幽靈模式：本機停用快捷、攻擊與拾取；門、NPC 與移動交原服判定。";
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
    // 送 C173 只保留給 HyperText 動作 exitghost（WO §4；入口在 NPC 對話，尚待 HTML 證據接上），沒有常駐按鈕。
    public bool GhostLocalBlocked=>State==LoginState.InWorld&&World.IsGhost;
}

public sealed partial class ClientForm {
    internal GhostModeView? ReadGhostMode()=>PlayerHudActive?login.GhostModeSnapshot():null;
}

// HUD 不再提供「離開幽靈」鈕（WO §4 #3）。幽靈中把快照標為忙碌：F5–F12 快捷槽與「使用一次」都不會送出（WO §4 #4）。
internal static class GhostModeHud {
    internal static PlayerHudSnapshot Update(PlayerHudView hud,FlowLayoutPanel menu,PlayerHudSnapshot state)=>
        Apply(state,(hud.FindForm() as ClientForm)?.ReadGhostMode());
    internal static PlayerHudSnapshot Apply(PlayerHudSnapshot state,GhostModeView? view)=>
        view is {Active:true}&&view.Stamp==state.Stamp
            ?state with{State="幽靈模式 · 快捷與攻擊停用",Busy=true,Notice=view.Notice+(state.Notice.Length>0?" · "+state.Notice:"")}
            :state;
}
