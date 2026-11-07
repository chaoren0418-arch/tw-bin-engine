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
    public bool TradeBlocksActions=>TradeState is "inviting" or "invited" or "accepting" or "open" or "adding" or "confirmed" or "attention" or "cancelling";
    public bool TradeCanCancel=>TradeBlocksActions&&TradeState!="cancelling";
    public bool TradeCanAdd=>TradeState=="open"&&TradePeerPresent&&!OwnDead&&Status?.Hp>0;
    public bool TradeCanConfirm=>TradeCanAdd;
    public bool TradePeerPresent=>tradeEpoch==WorldEpoch&&Actor is {} a&&TradePeerId is uint id&&objects.TryGetValue(id,out var peer)&&!peer.Dead&&peer.Name==TradePeerName&&Math.Abs(a.X-peer.X)<=1&&Math.Abs(a.Y-peer.Y)<=1;
    public WorldObject? FacingTradeTarget {
        get{if(!Ready||OwnDead||Status?.Hp<=0||Actor is not {} a||Target is not {} t||t.Dead||(t.Flags&4)==0)return null;
            if(t.X!=a.X+MovementRules.Dx[a.Direction]||t.Y!=a.Y+MovementRules.Dy[a.Direction]||t.Direction!=(a.Direction+4)%8)return null;
            return Objects.Count(o=>o.X==t.X&&o.Y==t.Y&&(o.Flags&4)!=0&&!o.Dead)==1?t:null;}
    }
    internal void ResetTrade(string notice="交易狀態已清除。"){TradeGeneration++;tradeOffers.Clear();TradePeerName="";TradePeerId=null;TradeChallenge=0;tradeOwnConfirmed=false;tradeOpened=false;TradeCancelRequired=false;TradeState="idle";TradeRevision++;TradeNotice=notice;}
    public void CheckTrade(){if(TradeBlocksActions&&TradeState is not ("invited" or "attention" or "cancelling")&&!TradePeerPresent){TradeState="attention";TradeRevision++;TradeNotice="交易對象、位置或世界已改變，不能繼續確認；請取消或斷線。";}}
    internal void BeginTradeInvite(){var t=FacingTradeTarget!;ResetTrade();CloseInteraction("已開啟交易操作。");tradeEpoch=WorldEpoch;TradePeerId=t.ObjectId;TradePeerName=t.Name;TradeState="inviting";TradeNotice="已送出面對面交易邀請，等待對方回覆；不會自動重送。";}
    internal void TradeAdmit(string command){TradeRevision++;switch(command){
        case "accept":TradeState="accepting";TradeNotice="已接受，等待原服開啟交易視窗。";break;
        case "reject":TradeCancelRequired=false;TradeState="cancelled";TradeNotice="拒絕已送出；本次沒有開啟交易。原服不另回傳拒絕方狀態。";break;
        case "cancel":TradeCancelRequired=false;TradeState=tradeOpened?"cancelling":"cancelled";TradeNotice=tradeOpened?"取消已送出，等待原服結果；物品與金額以回包為準。":"取消請求已送出，本地已停止等待尚未開啟的交易；沒有宣稱收到原服確認。";break;
        case "add":TradeState="adding";TradeNotice="物品加入請求已送出，等待原服雙方清單；尚未新增本地報價。";break;
        case "confirm":TradeState="confirmed";tradeOwnConfirmed=true;TradeNotice="你的確認已送出，等待原服最終結果；對方確認狀態未由此協議單獨提供。";break;
    }}
    internal void TradeServerMessage(ushort? code=null,string[]? args=null){if(!TradeBlocksActions)return;TradeRevision++;if(code==253&&TradeState is "inviting" or "attention"&&!tradeOpened&&args is {Length:1}&&args[0]==TradePeerName){TradeState="cancelled";TradeNotice="原服回報對方拒絕交易。";return;}if(TradeState is "inviting" or "adding" or "accepting"){TradeState="attention";TradeNotice=(LastServerMessage??"收到原服訊息")+"；請取消後重新操作，沒有自動重送。";}else TradeNotice=LastServerMessage??TradeNotice;}
    bool ReceiveTrade(byte opcode,LoginReader r){
        if(opcode==219){ushort header=r.H();uint challenge=r.D();ushort message=r.H();if(header!=0)throw new ProtocolException("confirmation envelope");
            if(ReceiveMarriageYesNo(message,challenge,r))return true;
            if(message==97){ReceiveClanInvitation(r,challenge);return true;}
            if(message!=252){if(TradeState=="invited"){TradeState="attention";TradeRevision++;TradeNotice="原服改送其他確認訊息；舊交易邀請不可再回答，可取消交易。";}Unhandled(opcode);return true;}
            string peer=r.S();r.Finish();if(!Ready||OwnDead)return true;
            if(TradeBlocksActions){TradeState="attention";TradeRevision++;TradeNotice="收到新邀請時已有交易操作；請取消後重新邀請。";return true;}
            var candidates=Objects.Where(o=>o.Name==peer&&!o.Dead&&Actor is {} a&&Math.Abs(o.X-a.X)<=1&&Math.Abs(o.Y-a.Y)<=1).ToArray();
            ResetTrade();CloseInteraction("收到交易邀請，舊NPC操作已失效。");tradeEpoch=WorldEpoch;TradePeerName=peer;TradePeerId=candidates.Length==1?candidates[0].ObjectId:null;TradeChallenge=challenge;TradeState="invited";TradeNotice=peer+" 邀請你交易。接受前請核對角色名稱；也可拒絕。";return true;
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
    public object TradeSnapshot()=>new{revision=TradeRevision,generation=TradeGeneration,epoch=WorldEpoch,state=TradeState,notice=TradeNotice,peer=TradePeerName,peerId=TradePeerId,challenge=TradeChallenge,offers=TradeOffers.Select(o=>new{side=o.Side,icon=o.Icon,name=o.Name,bless=o.Bless}),canAccept=TradeState=="invited"&&TradePeerPresent,canReject=TradeState=="invited",canAdd=TradeCanAdd,canConfirm=TradeCanConfirm,canCancel=TradeCanCancel,ownConfirmed=tradeOwnConfirmed};
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
            "accept"=>World.TradeState=="invited"&&World.TradePeerPresent&&!World.OwnDead&&World.Status?.Hp>0,
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
    async Task CancelChangedTrade(){if(World.TradeCancelRequired)await Trade("cancel",World.TradeRevision,World.WorldEpoch,World.TradeGeneration);}
}
