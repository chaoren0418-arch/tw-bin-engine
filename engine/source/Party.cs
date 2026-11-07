namespace P06;

public sealed record PartyMember(uint ObjectId,string Name,byte? HpPercent,uint? MapId=null,ushort X=0,ushort Y=0);
public sealed record PartyInvitation(uint Challenge,ushort Message,string Name,int Epoch,long Expires);
public sealed record PartyView(int Generation,int Epoch,int Revision,uint Owner,PartyMember[] Members,string Leader,string Notice,PartyInvitation? Invitation,bool Available,bool CanInvite,bool CanKick);

public static class PartyPackets {
    public static bool ValidName(string name)=>name.Length is >0 and <=52&&!name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c));
    public static byte[] Invite(uint target,byte mode=0){if(target==0||mode>1)throw new ArgumentException("party target/mode");return LoginPackets.Pad([230,mode,..BitConverter.GetBytes(target)]);}
    public static byte[] Leave()=>LoginPackets.Pad([33]);
    public static byte[] List()=>LoginPackets.Pad([43]);
    public static byte[] Kick(string name){if(!ValidName(name))throw new ArgumentException("party name");return LoginPackets.Pad([255,..LoginPackets.TextEncoding.GetBytes(name),0]);}
    public static byte[] Answer(uint challenge,bool accept)=>LoginPackets.Pad([121,0,0,..BitConverter.GetBytes(challenge),0xB9,3,(byte)(accept?1:0)]);
}

public sealed partial class WorldSession {
    readonly Dictionary<string,PartyMember> partyMembers=new(StringComparer.Ordinal);
    string partyLeader="",partyNotice="請選取場景中的玩家，再邀請組隊。";
    PartyInvitation? partyInvitation;
    uint partyOwner;
    int partyRevision;
    internal Func<long> PartyClock=()=>Environment.TickCount64;
    void ClearParty(string notice){partyMembers.Clear();partyLeader="";partyInvitation=null;partyNotice=notice;partyRevision++;}
    void CheckParty(){
        uint own=Status?.ObjectId??0;
        if(!Entered||partyOwner!=own){if(partyOwner!=0||partyMembers.Count>0||partyInvitation!=null)ClearParty("隊伍狀態已清除。");partyOwner=own;}
        if(partyInvitation is {} invite&&(invite.Epoch!=WorldEpoch||PartyClock()>=invite.Expires||OwnDead)){
            partyInvitation=null;partyRevision++;partyNotice="組隊邀請已失效，請對方重新邀請。";
        }
    }
    internal PartyView PartySnapshot(int generation,bool available){
        CheckParty();
        return new(generation,WorldEpoch,partyRevision,partyOwner,partyMembers.Values.OrderBy(m=>m.Name,StringComparer.Ordinal).ToArray(),partyLeader,partyNotice,partyInvitation,available,
            available&&partyInvitation==null&&(partyMembers.Count==0||partyLeader==Actor?.Name),available&&partyLeader.Length>0&&partyLeader==Actor?.Name);
    }
    static string PartyName(LoginReader r){string name=r.S();if(!PartyPackets.ValidName(name))throw new ProtocolException("party member name");return name;}
    static byte PartyHp(LoginReader r){byte hp=r.C();if(hp>100&&hp!=255)throw new ProtocolException("party HP ratio");return hp;}
    static PartyMember PartyRow(LoginReader r){uint id=r.D();string name=PartyName(r);byte hp=PartyHp(r);uint map=r.D();ushort x=r.H(),y=r.H();if(id==0)throw new ProtocolException("party identity");return new(id,name,hp==255?null:hp,map,x,y);}
    void PutPartyMember(PartyMember member){
        if(member.ObjectId==partyOwner||member.Name==Actor?.Name)return;
        foreach(string key in partyMembers.Where(p=>p.Value.ObjectId!=0&&p.Value.ObjectId==member.ObjectId&&p.Key!=member.Name).Select(p=>p.Key).ToArray())partyMembers.Remove(key);
        partyMembers[member.Name]=member;
    }
    void ReceivePartyHp(byte[] plain){
        var r=new LoginReader(plain);r.C();
        uint id=r.D();byte hp=PartyHp(r);r.Finish();if(id==0)throw new ProtocolException("party HP identity");CheckParty();
        if(id==partyOwner&&hp==255){ClearParty("原服已移除隊伍資料。");return;}
        var member=partyMembers.Values.FirstOrDefault(m=>m.ObjectId==id);
        if(member==null)return; // HPMeter also serves non-party characters; never invent a roster from it.
        if(hp==255)partyMembers.Remove(member.Name);else partyMembers[member.Name]=member with{HpPercent=hp};
        partyRevision++;
    }
    // Uses its own reader: an unrelated shared opcode must reach the original module untouched.
    internal bool ReceiveParty(byte opcode,byte[] plain){
        // Observe every packet before dispatch, including EnterGame while Entered is still false.
        // This clears a prior login even if no HUD snapshot was read between Reset and re-entry.
        CheckParty();
        if(opcode is not (250 or 219 or 39 or 71))return false;
        var r=new LoginReader(plain);r.C();
        if(opcode==250){
            byte sub=r.C();if(sub is not (104 or 105 or 106 or 110))return false;
            if(sub is 104 or 105){
                int count=sub==104?r.C():1;if(count>7)throw new ProtocolException("party member count");
                var rows=new List<PartyMember>();for(int i=0;i<count;i++)rows.Add(PartyRow(r));r.Finish();
                if(rows.Select(m=>m.ObjectId).Distinct().Count()!=count||rows.Select(m=>m.Name).Distinct().Count()!=count)throw new ProtocolException("duplicate party member");
                if(partyMembers.Keys.Union(rows.Where(m=>m.ObjectId!=partyOwner&&m.Name!=Actor?.Name).Select(m=>m.Name)).Count()>7)throw new ProtocolException("party capacity");
                // The inspected server excludes the newly joining pc even when broadcasting to older members.
                // Merge 104; C43/S39 is the explicit authoritative membership refresh.
                foreach(var row in rows)PutPartyMember(row);
                if(sub==105)partyLeader=Actor?.Name??"";
                partyInvitation=null;partyNotice="已收到原服隊伍資料。";
            }else if(sub==106){uint id=r.D();string leader=PartyName(r);r.Finish();if(id==0)throw new ProtocolException("party leader identity");partyLeader=leader;partyNotice=leader+" 成為新的隊長。";}
            else {
                int count=r.C();if(count>7)throw new ProtocolException("party location count");
                var rows=new List<(uint Id,uint Map,ushort X,ushort Y)>();for(int i=0;i<count;i++)rows.Add((r.D(),r.D(),r.H(),r.H()));r.Finish();
                if(rows.Any(m=>m.Id==0)||rows.Select(m=>m.Id).Distinct().Count()!=count)throw new ProtocolException("party location identity");
                foreach(var row in rows){var member=partyMembers.Values.FirstOrDefault(m=>m.ObjectId==row.Id);if(member!=null)partyMembers[member.Name]=member with{MapId=row.Map,X=row.X,Y=row.Y};}
            }
            partyRevision++;return true;
        }
        if(opcode==219){
            ushort header=r.H();uint challenge=r.D();ushort message=r.H();
            if(message!=953){if(partyInvitation!=null){partyInvitation=null;partyRevision++;partyNotice="原服已改送其他確認對話，舊組隊邀請失效。";}return false;}
            string name=PartyName(r);r.Finish();if(header!=0)throw new ProtocolException("party invitation envelope");
            ReceiveMarriageYesNo(message,challenge,r); // Returning true skips Trade, the usual route that expires an older proposal.
            if(Ready&&!OwnDead){
                if(TradeState=="invited")ResetTrade("收到組隊確認對話，舊交易邀請已失效。");
                if(clanInvitation!=null)ClearClanInvitation("收到組隊確認對話，舊入盟申請已失效。");
                partyInvitation=new(challenge,message,name,WorldEpoch,PartyClock()+30000);partyNotice=name+" 邀請你加入隊伍。";partyRevision++;
            }return true;
        }
        if(opcode==39){
            uint id=r.D();string page=r.S();if(page!="party")return false;
            ushort type=r.H(),count=r.H();if(type!=1||count!=2)throw new ProtocolException("party list header");
            string leader=PartyName(r),names=r.S();r.Finish();
            string[] members=names.Split(' ',StringSplitOptions.RemoveEmptyEntries);
            if(members.Length is <1 or >8||members.Any(n=>!PartyPackets.ValidName(n))||members.Distinct().Count()!=members.Length||!members.Contains(leader))throw new ProtocolException("party list members");
            if(id!=partyOwner||Actor==null)return true;
            if(!members.Contains(Actor.Name))throw new ProtocolException("party list omits self");
            var next=new List<PartyMember>();foreach(string name in members){if(name==Actor.Name)continue;
                var old=partyMembers.GetValueOrDefault(name);var matches=Objects.Where(o=>o.Name==name&&(o.Flags&4)!=0).ToArray();
                next.Add(old??new(matches.Length==1?matches[0].ObjectId:0,name,null));
            }
            partyMembers.Clear();foreach(var row in next)PutPartyMember(row);partyLeader=leader;partyInvitation=null;partyRevision++;partyNotice="已更新原服隊員名單；未提供的 HP 顯示未知。";return true;
        }
        ushort code=r.H();if(code is <414 or >426)return false;
        int argc=r.C();if(argc>8)throw new ProtocolException("party message arguments");var args=new string[argc];for(int i=0;i<argc;i++)args[i]=r.S();r.Finish();
        if(code is 418 or 419 or 425&&argc==0)ClearParty(code==419?"你已被移出隊伍。":code==425?"原服回覆：目前沒有隊伍。":"原服已解散隊伍。");
        else if(code==420&&argc==1){if(args[0]==Actor?.Name)ClearParty("你已離開隊伍。");else {partyMembers.Remove(args[0]);partyRevision++;partyNotice=args[0]+" 已離開隊伍。";}}
        else {partyNotice=code switch{415=>"對方已加入其他隊伍。",416=>"只有隊長能邀請隊員。",417=>"隊伍已滿。",423=>"對方拒絕了組隊邀請。",424=>"原服通知有隊員加入；可按更新名單核對。",426=>"指定角色不在隊伍中。",_=>"收到原服隊伍訊息："+code};partyRevision++;}
        return false; // Preserve the existing server-message/chat presentation.
    }
    internal bool PartyCurrent(PartyView v){CheckParty();return v.Owner!=0&&v.Owner==partyOwner&&v.Epoch==WorldEpoch&&v.Revision==partyRevision;}
    internal void PartySent(string command){if(command is "accept" or "reject")partyInvitation=null;partyNotice="已送出"+(command switch{"invite"=>"組隊邀請","accept"=>"接受邀請","reject"=>"拒絕邀請","leave"=>"離隊請求","kick"=>"踢除請求",_=>"名單查詢"})+"；隊員與 HP 以原服回覆為準。";partyRevision++;}
}

public sealed partial class LoginConnection {
    int partySendingGeneration=-1;
    bool PartyAccess=>State==LoginState.InWorld&&World.Ready&&!World.OwnDead&&World.Status?.Hp>0&&!World.ActionLocked&&!World.TradeBlocksActions&&!MovementBusy;
    public PartyView PartySnapshot()=>CaptureWorld(()=>World.PartySnapshot(SessionGeneration,PartyAccess&&partySendingGeneration!=SessionGeneration));
    public async Task<bool> SendParty(string command,PartyView shown,string name="",uint target=0,byte mode=0){
        if(shown.Generation!=SessionGeneration||!World.PartyCurrent(shown)||!PartyAccess||partySendingGeneration==SessionGeneration||sendMovementGate==null)return false;
        var invite=shown.Invitation;var chosen=World.Target;
        bool Eligible()=>command switch{
            "list"=>true,"leave"=>shown.Members.Length>0,
            "invite"=>shown.CanInvite&&chosen!=null&&chosen.ObjectId==target&&target!=shown.Owner&&(chosen.Flags&4)!=0&&!chosen.Dead&&World.Target==chosen&&mode<=1,
            "kick"=>shown.CanKick&&shown.Members.Any(m=>m.Name==name),
            "accept" or "reject"=>invite!=null,
            _=>false};
        if(!Eligible())return false;
        byte[] packet;try{packet=command switch{"list"=>PartyPackets.List(),"leave"=>PartyPackets.Leave(),"invite"=>PartyPackets.Invite(target,mode),"kick"=>PartyPackets.Kick(name),_=>PartyPackets.Answer(invite!.Challenge,command=="accept")};}catch(ArgumentException){return false;}
        int gen=SessionGeneration;partySendingGeneration=gen;var sender=sendMovementGate;
        try{return await sender(packet,()=>gen==SessionGeneration&&PartyAccess&&World.PartyCurrent(shown)&&Eligible(),()=>{World.PartySent(command);Set(State,"隊伍請求已送出。");})&&gen==SessionGeneration;}
        catch(Exception){if(gen==SessionGeneration){Cancel();Set(LoginState.Faulted,"組隊傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(partySendingGeneration==gen)partySendingGeneration=-1;}
    }
}
