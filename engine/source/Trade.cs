namespace P06;
public sealed record TradeOffer(byte Side,ushort Icon,string Name,byte Bless);
public static class TradePackets {
    public static byte[] Invite()=>LoginPackets.Pad([2]); // Server FaceToFace chooses the player; no target ID on wire.
    public static byte[] Answer(uint challenge,bool accept)=>LoginPackets.Pad([121,0,0,..BitConverter.GetBytes(challenge),252,0,(byte)(accept?1:0)]);
    public static byte[] Add(uint item,uint count){if(item==0||count is 0 or >int.MaxValue)throw new ArgumentException("trade item");return LoginPackets.Pad([37,..BitConverter.GetBytes(item),..BitConverter.GetBytes(count)]);}
    public static byte[] Confirm()=>LoginPackets.Pad([71]);
    public static byte[] Cancel()=>LoginPackets.Pad([86]);
}
public sealed partial class WorldSession {
    public string TradeState {get;private set;}="idle";
    public string TradeNotice {get;private set;}="雙方須在相鄰格面對面；選取面前角色後可邀請。";
    public int TradeRevision {get;private set;}
    public int TradeGeneration {get;private set;}
    public string TradePeerName {get;private set;}="";
    public uint? TradePeerId {get;private set;}
    public uint TradeChallenge {get;private set;}
    public TradeOffer[] TradeOffers=>tradeOffers.ToArray();readonly List<TradeOffer> tradeOffers=[];
    int tradeEpoch;bool tradeOwnConfirmed,tradeOpened;
    public bool TradeCancelRequired {get;private set;}
    // R3-E (#674): TRANSACTION_WINDOW is open from S52 (op 88) until the S112 (op 167) result; the native panel reads these.
    public bool TradeOpened=>tradeOpened&&tradeEpoch==WorldEpoch;
    public bool TradeOwnConfirmed=>tradeOwnConfirmed;
    public bool TradeBlocksActions=>TradeState is "inviting" or "invited" or "accepting" or "open" or "adding" or "confirmed" or "attention" or "cancelling";
    public bool TradeCanCancel=>TradeBlocksActions&&TradeState!="cancelling";
    public bool TradeCanAdd=>TradeState=="open"&&TradePeerPresent&&!OwnDead&&Status?.Hp>0;
    public bool TradeCanConfirm=>TradeCanAdd;
    public bool TradePeerPresent=>tradeEpoch==WorldEpoch&&Actor is {} a&&TradePeerId is uint id&&objects.TryGetValue(id,out var peer)&&!peer.Dead&&peer.Name==TradePeerName&&Math.Abs(a.X-peer.X)<=1&&Math.Abs(a.Y-peer.Y)<=1;
    public WorldObject? FacingTradeTarget=>FacingTradePeer(Target);
    /// <summary>t 在面前一格、面向自己、那一格只有它一個活著的玩家（同 3.81 FaceToFace）；否則 null。</summary>
    internal WorldObject? FacingTradePeer(WorldObject? candidate){
        if(!Ready||OwnDead||Status?.Hp<=0||Actor is not {} a||candidate is not {} t||t.Dead||(t.Flags&4)==0)return null;
        if(t.X!=a.X+MovementRules.Dx[a.Direction]||t.Y!=a.Y+MovementRules.Dy[a.Direction]||t.Direction!=(a.Direction+4)%8)return null;
        return Objects.Count(o=>o.X==t.X&&o.Y==t.Y&&(o.Flags&4)!=0&&!o.Dead)==1?t:null;
    }
    /// <summary>R9-C（#847）：面前一格唯一一個活著的玩家（不需選取、不看朝向）；`/trade` 用來判斷 989（對方開著個人商店）。</summary>
    public WorldObject? PlayerAhead {
        get{if(!Ready||OwnDead||Status?.Hp<=0||Actor is not {} a)return null;
            int x=a.X+MovementRules.Dx[a.Direction],y=a.Y+MovementRules.Dy[a.Direction];
            var found=Objects.Where(o=>o.X==x&&o.Y==y&&(o.Flags&4)!=0&&!o.Dead&&o.ObjectId!=a.ObjectId).ToArray();
            return found.Length==1?found[0]:null;}
    }
    internal void ResetTrade(string notice="交易狀態已清除。"){if(TradeState=="invited")YesNoHandled(252);TradeGeneration++;tradeOffers.Clear();TradePeerName="";TradePeerId=null;TradeChallenge=0;tradeOwnConfirmed=false;tradeOpened=false;TradeCancelRequired=false;TradeAutoReject=false;TradeState="idle";TradeRevision++;TradeNotice=notice;}
    public void CheckTrade(){if(TradeBlocksActions&&TradeState is not ("invited" or "attention" or "cancelling")&&!TradePeerPresent){TradeState="attention";TradeRevision++;TradeNotice="交易對象、位置或世界已改變，不能繼續確認；請取消或斷線。";}}
    internal void BeginTradeInvite()=>BeginTradeInvite(FacingTradeTarget!);
    internal void BeginTradeInvite(WorldObject t){ResetTrade();CloseInteraction("已開啟交易操作。");tradeEpoch=WorldEpoch;TradePeerId=t.ObjectId;TradePeerName=t.Name;TradeState="inviting";TradeNotice="已送出面對面交易邀請，等待對方回覆；不會自動重送。";}
    // byte_136E99D（FU7§1.1，已驗證）：玩家的「接受交易詢問」選項，預設 1（sub_998250）。
    // 1：S219/252 走通用 Y/N 詢問；0：不問，直接回覆拒絕 C121（sub_92EAD0 case 252，`chdhcs` 33,0,挑戰值,252,0 → TradePackets.Answer(challenge,false)）。
    // R6-E（#781）：切換入口 `/accepttrade`（PlayerChat.cs）與 HTML 動作 accepttrade（NpcInteraction.cs）；
    // 值跟 C244 角色設定一起存（ChatChannelSettings.AcceptTrade，原版位元 0x40），沒存過就是 1。
    internal bool TradeAskWindow {get=>PersistedChatChannels?.AcceptsTrade??true;set=>RememberChatChannels(ChatChannels with{AcceptTrade=value});}
    // 旗標 0 收到邀請：等連線層送出自動拒絕（借用 TradeCancelRequired 的收包後觸發，LoginConnection.cs:65）。
    internal bool TradeAutoReject {get;private set;}
    // The shared question is live only while this exact invitation (same challenge, same world) is still waiting.
    internal bool TradeYesNoLive(YesNoPrompt p)=>p.Message==252&&TradeState=="invited"&&TradeChallenge==p.RequestId&&tradeEpoch==p.Epoch&&p.Arguments is [var name]&&name==TradePeerName;
    internal void TradeAdmit(string command){if(TradeState=="invited")YesNoHandled(252);TradeRevision++;switch(command){
        case "accept":TradeState="accepting";TradeNotice="已接受，等待原服開啟交易視窗。";break;
        case "reject":TradeCancelRequired=false;TradeState="cancelled";TradeNotice=TradeAutoReject?TradePeerName+" 的交易邀請已依「不接受交易詢問」設定自動拒絕（/accepttrade 可切換）。":"拒絕已送出；本次沒有開啟交易。原服不另回傳拒絕方狀態。";TradeAutoReject=false;break;
        case "cancel":TradeCancelRequired=false;TradeState=tradeOpened?"cancelling":"cancelled";TradeNotice=tradeOpened?"取消已送出，等待原服結果；物品與金額以回包為準。":"取消請求已送出，本地已停止等待尚未開啟的交易；沒有宣稱收到原服確認。";break;
        case "add":TradeState="adding";TradeNotice="物品加入請求已送出，等待原服雙方清單；尚未新增本地報價。";break;
        case "confirm":TradeState="confirmed";tradeOwnConfirmed=true;TradeNotice="你的確認已送出，等待原服最終結果；對方確認狀態未由此協議單獨提供。";break;
    }}
    internal void TradeServerMessage(ushort? code=null,string[]? args=null){if(!TradeBlocksActions)return;TradeRevision++;if(code==253&&TradeState is "inviting" or "attention"&&!tradeOpened&&args is {Length:1}&&args[0]==TradePeerName){TradeState="cancelled";TradeNotice="原服回報對方拒絕交易。";return;}if(TradeState is "inviting" or "adding" or "accepting"){TradeState="attention";TradeNotice=(LastServerMessage??"收到原服訊息")+"；請取消後重新操作，沒有自動重送。";}else TradeNotice=LastServerMessage??TradeNotice;}
    bool ReceiveTrade(byte opcode,LoginReader r){
        if(opcode==219){ushort header=r.H();uint challenge=r.D();ushort message=r.H();if(header!=0)throw new ProtocolException("confirmation envelope");
            if(ReceiveClanWarYesNo(message,challenge,r))return true;
            if(ReceiveMarriageYesNo(message,challenge,r))return true;
            if(message==97){ReceiveClanInvitation(r,challenge);return true;}
            if(message!=252){if(TradeState=="invited"){TradeState="attention";TradeRevision++;TradeNotice="原服改送其他確認訊息；舊交易邀請不可再回答，可取消交易。";}Unhandled(opcode);return true;}
            string peer=r.S();r.Finish();if(!Ready||OwnDead)return true;
            if(TradeBlocksActions){TradeState="attention";TradeRevision++;TradeNotice="收到新邀請時已有交易操作；請取消後重新邀請。";return true;}
            var candidates=Objects.Where(o=>o.Name==peer&&!o.Dead&&Actor is {} a&&Math.Abs(o.X-a.X)<=1&&Math.Abs(o.Y-a.Y)<=1).ToArray();
            ResetTrade();CloseInteraction("收到交易邀請，舊NPC操作已失效。");tradeEpoch=WorldEpoch;TradePeerName=peer;TradePeerId=candidates.Length==1?candidates[0].ObjectId:null;TradeChallenge=challenge;TradeState="invited";
            // M7-K: flag 1 shows the shared Y/N question (chat input line, M7-J).
            // R6-E (FU7§1.1): flag 0 never asks; the connection sends Answer(challenge,false) once (TradeCancelRequired path).
            if(TradeAskWindow){TradeNotice=peer+" 邀請你交易。請在聊天輸入列輸入 Y 接受、N 拒絕後按 Enter。";OfferYesNo(challenge,252,[peer]);}
            else{TradeAutoReject=true;TradeCancelRequired=true;TradeNotice=peer+" 邀請你交易；依「不接受交易詢問」設定自動拒絕中。";}
            return true;
        }
        if(opcode==52){string peer=r.S();r.Finish();if(tradeEpoch!=WorldEpoch||TradeState is not ("inviting" or "accepting")||peer!=TradePeerName)return true;TradeRevision++;tradeOffers.Clear();tradeOwnConfirmed=false;tradeOpened=true;TradeState="open";TradeNotice="原服已開啟交易。逐次加入物品，核對雙方原服清單後才確認。";CheckTrade();return true;}
        if(opcode==35){byte side=r.C();ushort icon=r.H();string name=r.S();byte bless=r.C();r.Finish();if(side>1)throw new ProtocolException("trade side");if(tradeEpoch!=WorldEpoch||TradeState is not ("open" or "adding" or "confirmed" or "attention"))return true;if(tradeOffers.Count>=512)throw new ProtocolException("trade list too large");tradeOffers.Add(new(side,icon,name,bless));TradeRevision++;
            if(tradeOwnConfirmed){TradeState="attention";TradeCancelRequired=true;TradeNotice="確認後收到新的物品清單，原核對已失效；正在要求取消，最終結果以原服回報為準。";}
            else if(side==0&&TradeState=="adding"){TradeState="open";TradeNotice="原服已列入你的物品；請核對雙方清單。";}return true;
        }
        if(opcode==112){byte status=r.C();r.Finish();if(status>1)throw new ProtocolException("trade status");if(!TradeBlocksActions||tradeEpoch!=WorldEpoch)return true;
            // Success is meaningful only after this trade opened and our confirmation was admitted.
            if(status==0&&(!tradeOpened||!tradeOwnConfirmed))return true;
            TradeRevision++;TradeCancelRequired=false;tradeOwnConfirmed=false;TradeState=status==0?"completed":"cancelled";TradeNotice=status==0?"原服回報交易完成；請核對實際背包與金額。":"原服回報交易取消／拒絕；請核對返還的物品。";return true;}
        return false;
    }
    public object TradeSnapshot()=>new{revision=TradeRevision,generation=TradeGeneration,epoch=WorldEpoch,state=TradeState,notice=TradeNotice,peer=TradePeerName,peerId=TradePeerId,challenge=TradeChallenge,offers=TradeOffers.Select(o=>new{side=o.Side,icon=o.Icon,name=o.Name,bless=o.Bless}),canAccept=TradeState=="invited"&&TradePeerPresent&&!TradeAutoReject,canReject=TradeState=="invited",canAdd=TradeCanAdd,canConfirm=TradeCanConfirm,canCancel=TradeCanCancel,ownConfirmed=tradeOwnConfirmed};
}
public sealed partial class LoginConnection {
    bool tradeSending;int tradeSendLease;
    public bool CanInviteTrade=>State==LoginState.InWorld&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy&&World.FacingTradeTarget!=null&&!tradeSending;
    public async Task<bool> Trade(string command,int revision,int epoch,int tradeGeneration,uint item=0,uint count=0){
        bool cancelling=command is "cancel" or "reject";if(State!=LoginState.InWorld||tradeGeneration!=World.TradeGeneration||!cancelling&&revision!=World.TradeRevision||epoch!=World.WorldEpoch)return false;
        if(command=="dismiss"){if(World.TradeBlocksActions)return false;World.ResetTrade();Set(State,World.TradeNotice);return true;}
        World.CheckTrade();if(!cancelling&&revision!=World.TradeRevision||tradeSending&&!cancelling||sendMovementGate==null)return false;
        bool Eligible()=>(cancelling||!World.ActionLocked)&&(command switch{
            "invite"=>CanInviteTrade||tradeSending&&!World.TradeBlocksActions&&!MovementBusy&&World.FacingTradeTarget!=null,
            "accept"=>World.TradeState=="invited"&&!World.TradeAutoReject&&World.TradePeerPresent&&!World.OwnDead&&World.Status?.Hp>0,
            "reject"=>World.TradeState=="invited",
            "cancel"=>World.TradeCanCancel,
            "confirm"=>World.TradeCanConfirm,
            "add"=>World.TradeCanAdd&&count is >0 and <=int.MaxValue&&World.Inventory.Any(i=>i.ObjectId==item&&i.Count>=count&&i.Equipped!=true),
            _=>false});
        if(!Eligible()||command is "invite" or "add" or "confirm"&&MovementBusy)return false;
        var target=World.FacingTradeTarget;uint challenge=World.TradeChallenge;string peer=World.TradePeerName;int session=generation,lease=++tradeSendLease;var sender=sendMovementGate;var selectedItem=World.Inventory.FirstOrDefault(i=>i.ObjectId==item);
        byte[] bytes=command switch{"invite"=>TradePackets.Invite(),"accept"=>TradePackets.Answer(challenge,true),"reject"=>TradePackets.Answer(challenge,false),"add"=>TradePackets.Add(item,count),"confirm"=>TradePackets.Confirm(),_=>TradePackets.Cancel()};tradeSending=true;
        try{bool sent=await sender(bytes,()=>session==generation&&epoch==World.WorldEpoch&&tradeGeneration==World.TradeGeneration&&lease==tradeSendLease&&(cancelling||revision==World.TradeRevision)&&(command!="add"||World.Inventory.FirstOrDefault(i=>i.ObjectId==item)==selectedItem)&&State==LoginState.InWorld&&(cancelling||!World.ActionLocked)&&Eligible()&&(command=="invite"?target==World.FacingTradeTarget:peer==World.TradePeerName&&challenge==World.TradeChallenge),()=>{if(command=="invite")World.BeginTradeInvite();else World.TradeAdmit(command);Set(State,World.TradeNotice);});return sent&&session==generation;}
        catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"交易傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(session==generation&&lease==tradeSendLease){tradeSending=false;if(World.TradeCancelRequired)_=CancelChangedTrade();}}
    }
    // M7-K: Y/N on the chat input line for S219/252 goes through Trade's own admission and C121 (same bytes as YesNoProtocol.Answer).
    internal Task<bool> TradeYesNo(bool accept)=>Trade(accept?"accept":"reject",World.TradeRevision,World.WorldEpoch,World.TradeGeneration);
    // Also runs the flag-0 automatic reject (R6-E): same single C121 as the manual "reject", sent once from the receive loop.
    internal async Task CancelChangedTrade(){if(World.TradeCancelRequired)await Trade(World.TradeAutoReject?"reject":"cancel",World.TradeRevision,World.WorldEpoch,World.TradeGeneration);}

    /// <summary>
    /// R9-C（#847，FU9§9.5）：`/trade`、`/交換`（399）→ sub_F43510：面前一格是開著個人商店的玩家時印 989、不送；否則送 C2（3.81 C_Trade，
    /// 伺服器 FaceToFace 找對象，台版不用選目標）。面前有面向自己的玩家時，本機交易狀態照 WinForms 鈕一樣進入「邀請中」；
    /// 沒有時原版照送，伺服器回覆訊息，本機狀態不動。已有交易流程時不送（避免蓋掉正在進行的交易，引擎自加）。
    /// </summary>
    internal ChatCommandPlan TradeChatCommand(){
        var ahead=World.PlayerAhead;
        if(ahead is {ShopActive:true})return ChatCommandPlan.Refuse(ChatFollowupCommands.ShopTradeText);
        if(World.TradeBlocksActions||tradeSending)return ChatCommandPlan.Refuse("交易進行中，未送出新的交易邀請。");
        var peer=World.FacingTradePeer(ahead);uint? peerId=peer?.ObjectId;
        return new(peer!=null?"已送出面對面交易邀請（"+peer.Name+"），等待對方回覆；不會自動重送。":"已送出交易請求；面前沒有面向你的玩家，結果以原服回覆為準。",
            [TradePackets.Invite()],
            ()=>{if(peer!=null&&World.FacingTradePeer(World.PlayerAhead) is {} now&&now.ObjectId==peerId)World.BeginTradeInvite(now);},
            ()=>!World.TradeBlocksActions&&World.PlayerAhead is not {ShopActive:true}&&(peerId==null||World.FacingTradePeer(World.PlayerAhead)?.ObjectId==peerId));
    }
    /// <summary>
    /// R9-C（FU9§9.4）：`/shop`、`/商店`（907）→ sub_F480C0：自己擺攤中送 C38 type 1 關店並印 903；不在擺攤就切換個人商店設定窗（既有入口）。
    /// 釣魚中印 2120 的分支沒做：引擎沒有釣魚狀態（報告列為後續）。
    /// </summary>
    internal ChatCommandPlan ShopChatCommand(){
        // 擺攤中＝自己的 S119 動作是 70（同 Equipment.cs 對其他物件的 ShopActive 判斷；原版看 `& 0x400`，推測同義）。
        if(World.Actor is {Weapon:70})return ChatCommandPlan.Send(ChatFollowupCommands.ShopClosedText,null,PersonalShopPackets.Close());
        return ChatCommandPlan.Do("已切換個人商店設定窗。",()=>{
            if(ClientForm.OfferChatWindow(World,ChatWindowRequest.PersonalShop))return true;
            playerChatNotice="個人商店設定窗未開啟（原生 UI 未啟動）。";return false;});
    }

    public string TradeAskNotice {get;private set;}="";
    /// <summary>
    /// R6-E（FU7§1.1）：`/accepttrade` 與 HTML 動作 accepttrade：交易詢問旗標取反，不送包（原版只改本機旗標）。
    /// 本引擎接著把新值跟 C244 角色設定一起存（位元 0x40）；舊版設定格式不覆寫，只在本場有效。
    /// </summary>
    public async Task<bool> ToggleAcceptTrade(PlayerHudStamp stamp){
        if(!PlayerHudActions.Current(this,stamp))return false;
        bool next=!World.TradeAskWindow;World.TradeAskWindow=next;
        string done="交易詢問："+(next?ChatChannelCommands.OnText+"（收到交易邀請時詢問）":ChatChannelCommands.OffText+"（收到交易邀請時自動拒絕）");
        TradeAskNotice=done;
        var latest=World.LatestCharacterConfig;
        if(latest is {Editable:false}){TradeAskNotice=done+"；舊版角色設定格式未支援，本次不保存";return true;}
        var config=(latest??CharacterConfig.FromSlots(new PlayerHudBinding?[CharacterConfig.SlotCount])).WithChannels(World.ChatChannels);
        bool saved=await SaveCharacterConfig(stamp,config);
        if(PlayerHudActions.Current(this,stamp)){TradeAskNotice=saved?done+"；已保存到角色設定":done+"；角色設定未保存，下次登入可能還原";Set(State,TradeAskNotice);}
        return true;
    }
}

/// <summary>R6-E（FU7§1.1、§1.4）：`/accepttrade` 只註冊英文名（sub_F56320 沒有字串表名），沒有參數。英文比對不分大小寫（推測，同其他英文指令）。</summary>
public static class TradeAskCommand {
    public const string Name="accepttrade";
    public static bool Is(string input)=>input.Length>1&&input[0]=='/'&&string.Equals(input[1..].TrimEnd(),Name,StringComparison.OrdinalIgnoreCase);
}
