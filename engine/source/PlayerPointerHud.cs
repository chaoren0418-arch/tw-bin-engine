namespace P06;

public static class PlayerPointerHudFeedback {
    public static string Submitted(string kind)=>kind switch {
        "attack"=>"單次攻擊請求已送出，結果依伺服器回報。",
        "talk"=>"單次交談請求已送出，等待 NPC 回覆。",
        "pickup"=>"單次拾取 1 件請求已送出，等待地面與背包回報。",
        "private-shop"=>"已選取個人商店店主；請明確查詢買入或售出清單。",
        _=>""
    };
    public static string? Service(string kind)=>kind=="talk"?"NPC":kind=="private-shop"?"個人商店":null;
}
public sealed partial class ClientForm {
    string lastPlayerPointerHudNotice="";
    void InitPlayerPointerHud(){
        PlayerPointerActionSubmitted+=(kind,id)=>{
            if(PlayerPointerHudFeedback.Service(kind) is {} page)ShowPlayerService(page);
            // Opening the normal service panel may cancel input; retain the admitted action notice.
            lastPlayerPointerHudNotice=PlayerPointerNotice;
            playerHudNotice=PlayerPointerHudFeedback.Submitted(kind);
            playerHudNoticeUntil=Environment.TickCount64+5000;
            SyncPlayerHud();
        };
    }
    void SyncPlayerPointerHud(){
        if(playerPointer==null)return;
        string notice=PlayerPointerNotice;
        if(notice==lastPlayerPointerHudNotice)return;
        lastPlayerPointerHudNotice=notice;
        if(playerHudSending)return;
        playerHudNotice=notice;playerHudNoticeUntil=Environment.TickCount64+4000;
    }
}
