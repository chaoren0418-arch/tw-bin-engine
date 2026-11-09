using System.Globalization;
using P06.NativeUi;
namespace P06;

// T276（#667）：控制戒指開窗（S36 type 1／5）與盟屋地圖 674 鈕的送出端與 ClientForm 轉接。
// 依據：docs/reference/followups-3.md 第 3、4 節。開窗判斷在 AbilityStatus.cs（WorldSession.TryOpenRingWindow），
// 本檔只把「選定之後」接到既有送包路徑：
//   - 傳送目的地：選定書籤 → 既有 CastSelectedMagic，目標 SkillTargetKind.Bookmark（3.81 C6 的 HD＝書籤地圖、書籤 id）。
//     不照抄 TW16 sub_BE0F10 的 hhh（地圖、x、y）：3.81 C_UseSkill 對技能 5／69 讀 H＋D 並以書籤 id 查 CharBookTable。
//   - 召喚：沿用既有本機召喚請求（Pets.cs SendSummonChoice，C6 D(action)），送出前的 743 等級檢查在 AbilityStatus.cs。
//   - 674：C125 D(S187 物件 id) S("select") S(十進位盟屋編號)；回包 S39 agsel 由既有 NPC 對話處理（NpcInteraction.cs）。

/// <summary>盟屋地圖 674 鈕的封包（TW16 sub_542BF0 `cdss` 227 ＝ 3.81 C125 C_NPCAction，Npc_AuctionBoard 多讀一個 S）。</summary>
public static class AgitSelectPackets {
    public const string Action="select";
    /// <summary>C(125) D(board) S("select") S("%u" houseNumber)。houseNumber＝S187 原值（區域&lt;&lt;16 | 編號）。</summary>
    public static byte[] Select(uint board,uint houseNumber){
        if(board==0)throw new ArgumentException("拍賣看板物件 id 不可為 0",nameof(board));
        return LoginPackets.Pad([125,..BitConverter.GetBytes(board),..LoginPackets.TextEncoding.GetBytes(Action),0,
            ..LoginPackets.TextEncoding.GetBytes(houseNumber.ToString(CultureInfo.InvariantCulture)),0]);
    }
}

public sealed partial class LoginConnection {
    bool TryOpenRingWindow()=>State==LoginState.InWorld&&CaptureWorld(()=>World.TryOpenRingWindow());
    /// <summary>目前的選傳送目的地請求（不在世界中為 null）。</summary>
    public TeleportDestinationRequest? TeleportDestinationSnapshot()=>State==LoginState.InWorld?World.TeleportDestination:null;
    public bool CloseTeleportDestination(long revision)=>World.CloseTeleportDestination(revision);

    int teleportDestinationSending=-1;
    /// <summary>
    /// 對 revision 這一次的請求，以書籤 bookmarkId 施放一次（C6 HD）。送出成功才關窗；失敗保留窗，可再選一次。
    /// 請求過期、書籤不存在、已送出刪除或正在送出時不送。
    /// </summary>
    public async Task<bool> SendTeleportDestination(long revision,uint bookmarkId){
        if(State!=LoginState.InWorld||teleportDestinationSending==generation)return false;
        var target=CaptureWorld(()=>World.TeleportDestinationTarget(revision,bookmarkId));
        if(target==null)return false;
        int session=generation;teleportDestinationSending=session;
        try{
            bool cast=await CastSelectedMagic(target);
            if(cast&&session==generation)World.CloseTeleportDestination(revision);
            return cast&&session==generation;
        }
        finally{if(teleportDestinationSending==session)teleportDestinationSending=-1;}
    }

    /// <summary>674 鈕：對 shown 這一次的 S187 送一次 C125 select。S187 已被新的取代、互動送出中或動作鎖定時不送。</summary>
    public async Task<bool> SendAgitSelect(AgitMapOpenRequest shown){
        var map=shown.Map;
        if(State!=LoginState.InWorld||map.ObjectId==0||!map.InOriginalRange||World.AgitHouseMap!=map||interactionSending||MovementBusy
            ||sendMovementGate is not {} sender||World.ActionLocked||World.TradeBlocksActions)return false;
        byte[] packet=AgitSelectPackets.Select(map.ObjectId,map.HouseNumber);
        int session=generation;interactionSending=true;
        bool Valid()=>session==generation&&State==LoginState.InWorld&&World.AgitHouseMap==map&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy;
        try{return await sender(packet,Valid,()=>{})&&session==generation;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"盟屋拍賣查詢傳送失敗，已斷線；不會自動重送。");}return false;}
        finally{if(session==generation)interactionSending=false;}
    }
}

public sealed partial class ClientForm {
    long ringDestinationShown=-1;
    /// <summary>由 BookmarkUi.cs 的計時器呼叫：接上 674 鈕宿主、同步書籤窗的選目的地模式（新請求時切到書籤分頁）。</summary>
    void SyncRingWindows(){
        if(NativeUiRoot?.Find(AgitMapPanel.PanelId) is AgitMapPanel agit&&agit.Host==null)agit.Attach(new AgitMapHost(this));
        if(bookmarkPanel is not {} panel||panel.IsDisposed)return;
        var q=login.TeleportDestinationSnapshot();
        panel.UpdateDestination(q);
        if(q!=null&&q.Revision!=ringDestinationShown){
            ringDestinationShown=q.Revision;
            if(panel.Parent is TabPage page&&normalPanels!=null)normalPanels.SelectedTab=page;
        }
    }
    sealed class AgitMapHost(ClientForm form):IAgitMapHost {
        bool sending;
        public void Select(AgitMapOpenRequest shown){
            if(sending||form.World.Walking)return;
            sending=true;form.ClearInput("agit-select");_ = Run();
            async Task Run(){try{await form.login.SendAgitSelect(shown);}finally{sending=false;}}
        }
    }
}
