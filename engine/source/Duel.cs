namespace P06;

public static class DuelPackets {
    // C_Fight uses FaceToFace on the server and reads no fields.
    public static byte[] Invite()=>LoginPackets.Pad([5]);
    public static byte[] Answer(bool accept)=>LoginPackets.Pad([121,0,0,0,0,0,0,118,2,(byte)(accept?1:0)]);
}

public sealed record DuelView(int Revision,int Epoch,string Phase,string Peer,uint? OpponentId,string Notice);

public sealed partial class WorldSession {
    readonly object duelGate=new();
    DuelView duel=new(0,-1,"idle","",null,"雙方相鄰並面對面後，可邀請決鬥。");
    // Epoch invalidation also covers Reset/logout and same-map reconstruction, without a shared reset hook.
    public DuelView DuelSnapshot {
        get {lock(duelGate){
            if(duel.Epoch!=WorldEpoch)duel=new(duel.Revision+1,WorldEpoch,"idle","",null,"世界已更新，舊決鬥操作與標記已清除。");
            return duel;
        }}
    }
    public bool IsDuelOpponent(uint id)=>DuelSnapshot is {Phase:"active",OpponentId: {} opponent}&&opponent==id;
    internal void DuelAdmitted(string command,string peer){
        lock(duelGate){var current=DuelSnapshot;
            duel=current with{Revision=current.Revision+1,Peer=peer,OpponentId=null,
                Phase=command switch{"invite"=>"inviting","accept"=>"accepting",_=>"rejected"},
                Notice=command switch{"invite"=>"邀請已送出，等待對方與伺服器回覆。","accept"=>"接受已送出，等待伺服器確認開始決鬥。",_=>"拒絕已送出；伺服器不另回覆拒絕方。"}};
        }
        if(command!="invite")YesNoHandled(630);
    }
    // Own reader: unrelated packetbox/message subtypes keep their existing dispatch cursor.
    bool ReceiveDuel(byte opcode,byte[] plain){
        if(opcode is not (219 or 250 or 71))return false;
        var r=new LoginReader(plain);r.C();
        if(opcode==219){
            ushort header=r.H();uint token=r.D();ushort message=r.H();
            if(message!=630){lock(duelGate){var old=DuelSnapshot;if(old.Phase=="invited")duel=old with{Revision=old.Revision+1,Phase="expired",Notice="收到其他確認對話，舊決鬥邀請不可再回答。"};}return false;}
            string peer=r.S();r.Finish();
            if(header!=0||token!=0||string.IsNullOrWhiteSpace(peer)||peer.Any(char.IsControl))throw new ProtocolException("invalid duel invitation");
            if(!Ready||OwnDead)return true;
            lock(duelGate){var old=DuelSnapshot;
                if(old.Phase=="active")return true;
                duel=new(old.Revision+1,WorldEpoch,"invited",peer,null,peer+" 要與你決鬥，是否接受？");
            }
            OfferYesNo(token,message,[peer]); // Outside duelGate: the shared prompt reads DuelSnapshot under its own gate.
            return true;
        }
        if(opcode==250){
            if(r.C()!=5)return false;
            uint peer=r.D(),self=r.D();r.Finish();
            if((peer==0)!=(self==0)||peer!=0&&peer==self)throw new ProtocolException("invalid duel identities");
            if(!Ready||peer!=0&&Status?.ObjectId!=self)return true;
            lock(duelGate){var old=DuelSnapshot;
                duel=new(old.Revision+1,WorldEpoch,peer==0?"ended":"active",
                    peer==0?old.Peer:objects.TryGetValue(peer,out var actor)?actor.Name:"物件 "+peer,
                    peer==0?null:peer,peer==0?"伺服器已確認結束決鬥。":"伺服器已確認決鬥；對手顯示「決鬥對手」標記。");
            }
            return true;
        }
        ushort code=r.H();
        if(code is not (91 or 93 or 631 or 633 or 634))return false;
        int count=r.C();var args=new string[count];for(int i=0;i<count;i++)args[i]=r.S();r.Finish();
        if(count!=(code is 91 or 631?1:0))throw new ProtocolException("invalid duel message arguments");
        lock(duelGate){var old=DuelSnapshot;
            if(old.Phase is "inviting" or "accepting" or "invited"){
                if(code is 91 or 631&&args[0]!=old.Peer)return false;
                duel=old with{Revision=old.Revision+1,Phase=code==631?"rejected":"unconfirmed",OpponentId=null,
                    Notice=code switch{631=>args[0]+" 拒絕決鬥。",633=>"伺服器回報你已在決鬥中；尚未收到本次對手狀態。",634=>"伺服器回報對方已在決鬥中。",91=>"伺服器回報對方沒有面對你。",_=>"伺服器回報面前沒有人。"}};
            }
        }
        return false; // S71 remains visible through the existing server-message handler.
    }
}

public sealed partial class LoginConnection {
    int duelSendingGeneration=-1;
    public bool CanInviteDuel=>DuelEligible("invite");
    bool DuelEligible(string command){
        if(State!=LoginState.InWorld||!World.Ready||World.OwnDead||World.Status?.Hp<=0||World.ActionLocked||World.TradeBlocksActions||MovementBusy)return false;
        var view=World.DuelSnapshot;
        return command switch{
            "invite"=>(view.Phase is "idle" or "ended" or "rejected" or "expired" or "unconfirmed")&&World.FacingTradeTarget!=null,
            "accept" or "reject"=>view.Phase=="invited",
            _=>false};
    }
    public async Task<bool> Duel(string command,int revision,int epoch){
        var view=World.DuelSnapshot;
        if(duelSendingGeneration==generation||sendMovementGate==null||view.Revision!=revision||view.Epoch!=epoch||!DuelEligible(command))return false;
        int session=generation,targetRevision=World.TargetRevision;
        var target=command=="invite"?World.FacingTradeTarget:null;
        var own=World.Actor;var sender=sendMovementGate;duelSendingGeneration=session;
        bool Valid()=>generation==session&&World.DuelSnapshot.Revision==revision&&World.WorldEpoch==epoch&&DuelEligible(command)&&
            (command!="invite"||World.TargetRevision==targetRevision&&World.FacingTradeTarget==target&&World.Actor==own);
        try{
            bool sent=await sender(command=="invite"?DuelPackets.Invite():DuelPackets.Answer(command=="accept"),Valid,
                ()=>{World.DuelAdmitted(command,target?.Name??view.Peer);Set(State,World.DuelSnapshot.Notice);});
            return sent&&session==generation;
        }catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"決鬥操作傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(duelSendingGeneration==session)duelSendingGeneration=-1;}
    }
}
