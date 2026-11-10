namespace P06;

public sealed record MarriageView(int Generation,int Epoch,int Revision,string Phase,string Peer,string Partner,string Notice,bool CanPropose,bool CanAnswer);

// d287 contract: docs/protocol/marriage.md. The zero D is not a request ID.
public static class MarriagePackets {
    public static byte[] Propose()=>LoginPackets.Pad([50,0]);
    /// <summary>T316（FU6§4）：C50 `cc` 21／1（TW16 sub_F46690）。沒有目標欄位；3.81 C_Propose c=1：未婚回 662，否則回 S_Message_YN(653)，確認走共用詢問槽。</summary>
    public static byte[] Divorce()=>LoginPackets.Pad([50,1]);
    public static byte[] Answer(bool accept)=>LoginPackets.Pad([121,0,0,0,0,0,0,142,2,(byte)(accept?1:0)]);
    public static bool WeddingArea(ushort? map,int x,int y)=>map==4&&x is >=33974 and <=33976&&y is >=33362 and <=33365;
}

public sealed partial class WorldSession {
    int marriageEpoch=-1,marriageRevision;
    uint marriageActor,marriagePeerId;
    long marriageWait;
    string marriagePhase="idle",marriagePeer="",marriagePartner="",marriageNotice="請在婚禮區域選取相鄰且面對面的角色，再送出求婚。";
    // This profile has no login-time spouse packet; names are only known from S71/655.
    void CheckMarriage(){
        if(marriageEpoch!=WorldEpoch||marriageActor!=Actor?.ObjectId||!Ready){
            if(marriageEpoch!=-1){marriageRevision++;marriagePhase="idle";marriagePeer=marriagePartner="";marriagePeerId=0;marriageNotice="世界已改變；伴侶資料需由伺服器重新確認。";}
            marriageEpoch=-1;return;
        }
        if(marriagePhase is "proposing" or "invited" or "accepting"){
            // R2-J (#624): the received question ("invited") has no local 15 s expiry (only 729/1232/1256/1268 time out, WIN T141);
            // the proposer's and accepter's wait for the server result keeps the engine's 15 s limit.
            if(OwnDead||Status?.Hp<=0||!MarriagePeerPresent||marriagePhase!="invited"&&InteractionClock()-marriageWait>=InteractionTimeoutMs){
                marriageNotice=marriagePhase=="invited"?"角色或對方位置已改變，舊求婚邀請不可回答。":"求婚等待已逾時，或角色／位置已改變。舊邀請不可回答；沒有自動重送。";
                marriagePhase="expired";marriageRevision++;
            }
        }
    }
    bool MarriagePeerPresent=>Actor is {} a&&objects.TryGetValue(marriagePeerId,out var p)&&!p.Dead&&p.Name==marriagePeer
        &&Math.Abs(a.X-p.X)<=1&&Math.Abs(a.Y-p.Y)<=1;
    internal bool MarriageCanPropose {get{CheckMarriage();return marriagePhase is "idle" or "rejected" or "failed" or "expired"
        &&marriagePartner.Length==0&&FacingTradeTarget is {} p&&Actor is {} a
        &&MarriagePackets.WeddingArea(MapId,a.X,a.Y)&&MarriagePackets.WeddingArea(MapId,p.X,p.Y);}}
    internal bool MarriageCanAnswer {get{CheckMarriage();return marriagePhase=="invited"&&MarriagePeerPresent;}}
    internal MarriageView MarriageSnapshot(int generation,bool access){
        CheckMarriage();return new(generation,WorldEpoch,marriageRevision,marriagePhase,marriagePeer,marriagePartner,marriageNotice,access&&MarriageCanPropose,access&&MarriageCanAnswer);
    }
    internal bool CurrentMarriage(MarriageView v){CheckMarriage();return v.Epoch==WorldEpoch&&v.Revision==marriageRevision;}
    void BindMarriage(string peer,uint id,string phase,string notice){
        marriageEpoch=WorldEpoch;marriageActor=Actor!.ObjectId;marriagePeer=peer;marriagePeerId=id;
        marriagePhase=phase;marriageNotice=notice;marriageRevision++;marriageWait=InteractionClock();
    }
    internal void MarriageSent(string command){
        if(command=="propose"){
            var p=FacingTradeTarget!;BindMarriage(p.Name,p.ObjectId,"proposing","求婚已送出，等待對方與伺服器回覆；尚未結婚。");
        }else{
            marriagePhase=command=="accept"?"accepting":"rejected";marriageRevision++;marriageWait=InteractionClock();
            marriageNotice=command=="accept"?"接受已送出，等待伺服器確認結婚。":"已送出拒絕；伺服器不另回覆拒絕方。";
            YesNoHandled(654);
        }
    }
    /// <summary>
    /// T316：聊天指令 /propose、/divorce 送出後（原版只送包、不檢查）。求婚時若面前剛好有角色就照舊記下對象，等 654／655 等結果；
    /// 沒有就只寫提示（對象由伺服器判定）。離婚的確認 653 走共用詢問槽。
    /// </summary>
    internal void MarriageCommandSent(bool divorce){
        CheckMarriage();
        if(!divorce&&FacingTradeTarget is {} p&&Actor!=null&&marriagePhase is "idle" or "rejected" or "failed" or "expired"){
            BindMarriage(p.Name,p.ObjectId,"proposing","求婚已送出（/propose），等待對方與伺服器回覆；尚未結婚。");return;
        }
        if(Actor!=null){marriageEpoch=WorldEpoch;marriageActor=Actor.ObjectId;}
        marriageNotice=divorce?"離婚請求已送出（/divorce）；伺服器會再詢問確認（653），請在輸入列回覆。":"求婚已送出（/propose）；對象由伺服器判定（面前的角色）。";
        marriageRevision++;
    }
    bool ReceiveMarriageYesNo(ushort message,uint reserved,LoginReader r){
        CheckMarriage();
        if(message!=654){
            // Legacy confirmations share TempID on the server. Never answer an older prompt.
            if(marriagePhase=="invited"){marriagePhase="expired";marriageRevision++;marriageNotice="收到其他確認訊息，原求婚邀請已失效。";}
            return false;
        }
        string peer=r.S();r.Finish();
        if(reserved!=0||string.IsNullOrWhiteSpace(peer)||peer.Any(char.IsControl))throw new ProtocolException("marriage invitation");
        // This route skips Trade's non-252 path, but S219/654 still replaces the server's TempID.
        if(TradeState=="invited"){TradeState="attention";TradeRevision++;TradeNotice="原服改送其他確認訊息；舊交易邀請不可再回答，可取消交易。";}
        if(clanInvitation!=null)ClearClanInvitation("收到求婚確認，原入盟申請已失效，請對方重新申請。");
        if(!Ready||OwnDead||Status?.Hp<=0)return true;
        var candidates=Objects.Where(p=>p.Name==peer&&!p.Dead&&(p.Flags&4)!=0&&Actor is {} a&&Math.Abs(p.X-a.X)<=1&&Math.Abs(p.Y-a.Y)<=1).ToArray();
        BindMarriage(peer,candidates.Length==1?candidates[0].ObjectId:0,"invited",peer+" 向你求婚；請在聊天輸入列回覆 Y／N，接受後仍需等待伺服器結果。");
        CheckMarriage();
        // T141: msg 654 takes S_ASK's default branch, i.e. the shared input-line Y/N (YesNoPrompt.cs).
        if(marriagePhase=="invited")OfferYesNo(reserved,message,[peer]);
        return true;
    }
    bool ReceiveMarriageMessage(byte[] plain){
        var r=new LoginReader(plain);r.C();ushort message=r.H();
        if(message is not (655 or 656 or 657 or 658 or 661 or 790))return false;
        int count=r.C();bool named=message is 655 or 656;
        if(count!=(named?1:0))throw new ProtocolException("marriage message arguments");
        string peer=named?r.S():"";r.Finish();
        if(named&&(string.IsNullOrWhiteSpace(peer)||peer.Any(char.IsControl)))throw new ProtocolException("marriage peer name");
        CheckMarriage();
        if(!Ready)return true;
        if(message==655){
            // Only the authoritative result establishes a marriage, never the Yes button or 790.
            marriageEpoch=WorldEpoch;marriageActor=Actor!.ObjectId;marriagePartner=peer;marriagePeer=peer;
            marriagePhase="married";marriageNotice="結婚成功。伴侶："+peer;marriageRevision++;
        }else if(message==656){
            if(marriagePhase!="proposing"||peer!=marriagePeer)return true;
            marriagePhase="rejected";marriageNotice=peer+" 拒絕了求婚。";marriageRevision++;
        }else if(message==790){
            if(marriagePhase is not ("proposing" or "accepting"))return true;
            marriageNotice="伺服器回報婚禮完成，等待伴侶名稱。";marriageRevision++;
        }else{
            if(marriagePhase!="proposing")return true;
            marriagePhase="failed";marriageRevision++;marriageNotice=message switch{657=>"伺服器回報你已經結婚。",658=>"伺服器回報對方已經結婚。",_=>"伺服器要求結婚對象為異性。"};
        }
        LastServerReply=null;LastServerMessage=marriageNotice;return true;
    }
}

public sealed partial class LoginConnection {
    bool marriageSending;
    bool MarriageAccess=>State==LoginState.InWorld&&World.Ready&&!World.OwnDead&&World.Status?.Hp>0
        &&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy;
    public MarriageView MarriageSnapshot()=>CaptureWorld(()=>World.MarriageSnapshot(generation,MarriageAccess&&!marriageSending));
    public async Task<bool> SendMarriage(string command,MarriageView view){
        bool Eligible()=>view.Generation==generation&&World.CurrentMarriage(view)&&MarriageAccess
            &&(command=="propose"?World.MarriageCanPropose:command is "accept" or "reject"&&World.MarriageCanAnswer);
        if(marriageSending||sendMovementGate==null||!CaptureWorld(Eligible))return false;
        var target=World.FacingTradeTarget;var actor=World.Actor;var sender=sendMovementGate;
        byte[] packet=command=="propose"?MarriagePackets.Propose():MarriagePackets.Answer(command=="accept");
        marriageSending=true;
        try{return await sender(packet,()=>CaptureWorld(()=>Eligible()&&(command!="propose"||target==World.FacingTradeTarget&&actor==World.Actor)),
            ()=>{World.MarriageSent(command);Set(State,World.MarriageSnapshot(generation,false).Notice);});}
        catch(Exception){if(view.Generation==generation){Cancel();Set(LoginState.Faulted,"求婚傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{marriageSending=false;}
    }
}
// M7-J: no dedicated Form or HUD button (windows.md T141). The S219/654 question is answered from the
// chat input line via YesNoPrompt.cs.
// T316（FU6§4）：原版入口是 HTML 動作 propose／divorce（HyperActPropose sub_5838F0、HyperActDivorce sub_582730）與
// 聊天指令 /propose、/divorce（「/求婚」字串 788、「/離婚」字串 789；Commandpropose、Commanddivorce）。執行函式只送包、不檢查狀態，
// 對象由伺服器決定（面前的角色）。3.81 的 HTML 範本沒有這兩個連結，所以 3.81 世代實際的入口是聊天指令（PlayerChat.cs）。
// 下面的「求婚」頁是 R2-J（#624）留下的引擎自訂入口，不是原版畫面；它走有本機條件的 SendMarriage("propose")。
public sealed partial class LoginConnection {
    /// <summary>Proposal to the adjacent facing player in the wedding area; for the HTML action hook (R2-F) and the transitional page.</summary>
    public Task<bool> ProposeMarriage()=>SendMarriage("propose",MarriageSnapshot());
}

public sealed partial class ClientForm {
    readonly Label marriageEntryNotice=new(){Dock=DockStyle.Fill,ForeColor=Color.White,Padding=new(10)};
    readonly Button marriagePropose=new(){Text="向面前角色求婚",AutoSize=true,AccessibleName="向面前角色求婚"};
    bool marriageProposing;
    TabPage MarriageEntryPage(){
        var page=new TabPage("求婚"){BackColor=Color.FromArgb(23,38,41)};
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Top,Height=44};buttons.Controls.Add(marriagePropose);
        page.Controls.AddRange([marriageEntryNotice,buttons]);page.Enter+=(_,_)=>LeaveGameFocus("marriage");
        marriagePropose.Click+=async(_,_)=>await ProposeMarriageClick();
        return page;
    }
    async Task ProposeMarriageClick(){
        if(marriageProposing||World.Walking)return;
        ClearInput("marriage-propose");marriageProposing=true;
        try{await login.ProposeMarriage();}finally{marriageProposing=false;}
        if(!IsDisposed)SyncMarriageEntry();
    }
    void SyncMarriageEntry(){
        var view=login.MarriageSnapshot();
        marriageEntryNotice.Text=(view.Partner.Length>0?"伴侶："+view.Partner+"\n":"")+view.Notice+
            "\n\n求婚：在婚禮區域站在對方相鄰一格並互相面對。對方的回覆走聊天輸入列 Y／N；結果以原服回報為準。\n（此頁是引擎自訂入口；原版從聊天指令 /propose（/求婚）或 NPC 對話的求婚動作送出，離婚用 /divorce（/離婚）。）";
        marriagePropose.Enabled=login.State==LoginState.InWorld&&!World.Walking&&!marriageProposing&&view.CanPropose;
    }
}
