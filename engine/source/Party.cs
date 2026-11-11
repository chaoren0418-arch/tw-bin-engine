using System.Runtime.CompilerServices;
using P06.NativeUi;
namespace P06;

/// <summary>
/// T423（#967，FU15§2.2、§2.3）：原生面板的「聊天輸入列提示」確認入口（原版 sub_AC98E0：提示字串放進輸入列，答一次回呼一次）。
/// 接在 R10-F 的 <see cref="ChatPanel.LinePrompts"/> 上：問的時候把自己的提示放在前面（輸入列只有一個提示，新的取代舊的），
/// 答完或 Esc 取消後把原來的來源（TradeUi.cs 的交易 140／刪除／1028）還回去。提示是本機答覆（Answer 不是 null），
/// 答覆直接交給發問的面板，不經聊天送出。Live 回 false 時（離開世界、對象不在了）提示自己消失，不送。
/// </summary>
public static class ChatLineAsk {
    sealed class Slot { public Func<ChatLinePrompt?>? Prev,Hook; public ChatLinePrompt? Current; public Func<bool>? Live; }
    static readonly ConditionalWeakTable<ChatPanel,Slot> slots=new();
    /// <summary>可用的原生聊天窗（CENTER_WINDOW）；沒有就是 null（呼叫端保留原本的入口）。</summary>
    public static ChatPanel? Chat(Func<string,IUiPanel?>? find)=>find?.Invoke(ChatPanel.WindowName) as ChatPanel;
    /// <summary>在聊天輸入列問 text；回答（Enter）交給 answer，Esc 不送。回傳 true＝提示已放上輸入列。</summary>
    public static bool Ask(ChatPanel chat,object key,string text,Func<string,bool> answer,Func<bool>? live=null){
        var s=slots.GetValue(chat,_=>new Slot());
        s.Hook??=()=>Get(chat,s);
        if(!ReferenceEquals(chat.LinePrompts,s.Hook)){s.Prev=chat.LinePrompts;chat.LinePrompts=s.Hook;}
        ChatLinePrompt? p=null;
        p=new ChatLinePrompt(key,text,typed=>Done(chat,s,p)&&answer(typed),()=>Done(chat,s,p));
        s.Current=p;s.Live=live;
        return true;
    }
    /// <summary>目前由這個入口放上的提示（null＝沒有）。</summary>
    public static ChatLinePrompt? Pending(ChatPanel chat)=>slots.TryGetValue(chat,out var s)&&s.Current is {} p&&s.Live?.Invoke()!=false?p:null;
    static ChatLinePrompt? Get(ChatPanel chat,Slot s){
        if(s.Current is {} p){if(s.Live?.Invoke()!=false)return p;Done(chat,s,p);}
        return s.Prev?.Invoke();
    }
    static bool Done(ChatPanel chat,Slot s,ChatLinePrompt? p){
        if(p==null||!ReferenceEquals(s.Current,p))return false;
        s.Current=null;s.Live=null;
        if(ReferenceEquals(chat.LinePrompts,s.Hook))chat.LinePrompts=s.Prev;
        s.Prev=null;
        return true;
    }
}

public sealed record PartyMember(uint ObjectId,string Name,byte? HpPercent,uint? MapId=null,ushort X=0,ushort Y=0);
// R2-J (#624): no local expiry. The original keeps the question in the input line until a newer S219 replaces it or it is answered.
public sealed record PartyInvitation(uint Challenge,ushort Message,string Name,int Epoch);
public sealed record PartyView(int Generation,int Epoch,int Revision,uint Owner,PartyMember[] Members,string Leader,string Notice,PartyInvitation? Invitation,bool Available,bool CanInvite,bool CanKick);

public static class PartyPackets {
    public static bool ValidName(string name)=>name.Length is >0 and <=52&&!name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c));
    public static byte[] Invite(uint target,byte mode=0){if(target==0||mode>1)throw new ArgumentException("party target/mode");return LoginPackets.Pad([230,mode,..BitConverter.GetBytes(target)]);}
    public static byte[] Leave()=>LoginPackets.Pad([33]);
    public static byte[] List()=>LoginPackets.Pad([43]);
    public static byte[] Kick(string name){if(!ValidName(name))throw new ArgumentException("party name");return LoginPackets.Pad([255,..LoginPackets.TextEncoding.GetBytes(name),0]);}
    /// <summary>R7-J (#812): C230 C(3) D(memberObjectId) = leader transfer (docs/protocol/party.md:21). Map/screen range and leadership are the server's call (1695／1697).</summary>
    public const byte DelegateType=3;
    public static byte[] Delegate(uint member){if(member==0)throw new ArgumentException("party delegate target");return LoginPackets.Pad([230,DelegateType,..BitConverter.GetBytes(member)]);}
    /// <summary>Object id the delegate button may send for this member name; 0 = not eligible (not leader, unknown member, id unknown, self).</summary>
    public static uint DelegateTarget(PartyView shown,string name){
        if(!shown.CanKick)return 0;
        var m=shown.Members.FirstOrDefault(x=>x.Name==name);
        return m==null||m.ObjectId==0||m.ObjectId==shown.Owner?0:m.ObjectId;
    }
    /// <summary>
    /// T423（#967，FU15§2.2）：委任前 sub_954C90 用字串 4051 格式化隊員名稱，在聊天輸入列問；回答 Y 才送 C230 3。
    /// 本機 string-c.tbl（3,484 筆）沒有 4051；參考客戶端 kor25／pmc／c760／ly 都是「要委任%s 為組隊隊長嗎?」，表裡沒有時用這句。
    /// </summary>
    public const int DelegateConfirmMessage=4051;
    public const string DelegateConfirmFallback="要委任%s 為組隊隊長嗎?";
    public static string DelegateConfirmText(string name){
        string template=ServerMessageText.Table is {} t&&DelegateConfirmMessage<t.Count&&ServerMessageText.DisplayText(t[DelegateConfirmMessage]).Trim() is {Length:>0} s?s:DelegateConfirmFallback;
        int at=template.IndexOf("%s",StringComparison.Ordinal);
        return at<0?template:string.Concat(template.AsSpan(0,at),name,template.AsSpan(at+2));
    }
    public static byte[] Answer(uint challenge,bool accept)=>LoginPackets.Pad([121,0,0,..BitConverter.GetBytes(challenge),0xB9,3,(byte)(accept?1:0)]);
    /// <summary>R9-C（#847，FU9§9.1）：C230 C(2) S(名稱)＝聊天隊伍邀請（3.81 C_CreateParty type 2；原版 `/chatinvite 名稱` 送 TW16 op 42 type 2 `ccs`）。</summary>
    public const byte ChatInviteType=2;
    public static byte[] ChatInvite(string name){
        if(!ValidName(name))throw new ArgumentException("聊天隊伍邀請格式：/chatinvite 名稱（名稱不可含空白，最多 52 字）");
        try{return LoginPackets.Pad([230,ChatInviteType,..LoginPackets.TextEncoding.GetBytes(name),0]);}
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("名稱含有遊戲字集無法表示的字元。");}
    }
}

/// <summary>
/// R9-C（#847，FU9§9.1、Q7-8）：`/invite` 不帶名稱 → sub_9550C0 進入選目標模式（字串 179「請選擇對象。」，範圍 30 格）；
/// 點到人後 sub_954FC0 檢查對象是有效玩家才送 type 0／1（1 是 byte_13CE519 為真時，本引擎固定 0）。
/// 隱身判斷（sub_A42C30，推測）沒做，3.81 伺服器自己會擋。游標 179 與範圍單位（格）是推測，同 SpellTargeting。
/// </summary>
public sealed record PartyInvitePick(int Epoch,uint Owner);
public static class PartyInviteTargeting {
    public const int Range=30,Cursor=ChatFollowupCommands.SelectTarget;
}

public sealed partial class WorldSession {
    readonly Dictionary<string,PartyMember> partyMembers=new(StringComparer.Ordinal);
    string partyLeader="",partyNotice="請選取場景中的玩家，再邀請組隊。";
    PartyInvitation? partyInvitation;
    uint partyOwner;
    int partyRevision;
    void ClearParty(string notice){partyMembers.Clear();partyLeader="";partyInvitation=null;partyNotice=notice;partyRevision++;}
    void CheckParty(){
        uint own=Status?.ObjectId??0;
        if(!Entered||partyOwner!=own){if(partyOwner!=0||partyMembers.Count>0||partyInvitation!=null)ClearParty("隊伍狀態已清除。");partyOwner=own;}
        if(partyInvitation is {} invite&&(invite.Epoch!=WorldEpoch||OwnDead)){
            partyInvitation=null;partyRevision++;partyNotice="組隊邀請已失效（世界已更新或角色死亡），請對方重新邀請。";
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
        ObserveYesNo(opcode,plain); // M7-J: every S219 replaces the shared input-line prompt first.
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
                partyInvitation=new(challenge,message,name,WorldEpoch);partyNotice=name+" 邀請你加入隊伍；請在聊天輸入列回覆 Y／N。";partyRevision++;
                OfferYesNo(challenge,message,[name]);
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
    // ---- R9-C：/invite 的選目標模式（只有狀態；畫面上的游標與點擊接線見 docs/reports/issue-847.md「給下一棒」） ----
    public PartyInvitePick? PartyInvitePick {get;private set;}
    /// <summary>選目標模式有效：同一世界、同一角色。</summary>
    public bool PartyInvitePicking=>PartyInvitePick is {} p&&p.Epoch==WorldEpoch&&p.Owner!=0&&p.Owner==Status?.ObjectId;
    internal void BeginPartyInvitePick(){PartyInvitePick=new(WorldEpoch,Status?.ObjectId??0);partyNotice=ChatFollowupCommands.SelectTargetText;partyRevision++;}
    public void CancelPartyInvitePick(){if(PartyInvitePick==null)return;PartyInvitePick=null;partyRevision++;}
    /// <summary>sub_954FC0 的對象檢查：玩家旗標、活著、不是自己、在 30 格內（切比雪夫距離，推測）。</summary>
    internal WorldObject? PartyInviteCandidate(uint id){
        if(Actor is not {} a||id==0||id==a.ObjectId||id==Status?.ObjectId)return null;
        var o=Objects.FirstOrDefault(x=>x.ObjectId==id);
        return o is {Dead:false,AppearancePending:false}&&(o.Flags&4)!=0&&Math.Max(Math.Abs(o.X-a.X),Math.Abs(o.Y-a.Y))<=PartyInviteTargeting.Range?o:null;
    }
    /// <summary>T423（#967）：聊天輸入列提示的本機結果訊息印在聊天欄（例如備註超長的 3328；sub_7DC850，字色未接，推測）。</summary>
    internal void ChatLineAskNotice(string text){
        if(text.Length==0)return;
        playerChatLines.Enqueue(new(++playerChatSequence,0,PlayerChatProtocol.Display(text)));
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
    internal bool PartyCurrent(PartyView v){CheckParty();return v.Owner!=0&&v.Owner==partyOwner&&v.Epoch==WorldEpoch&&v.Revision==partyRevision;}
    internal void PartySent(string command){if(command is "accept" or "reject"){partyInvitation=null;YesNoHandled(953);}partyNotice="已送出"+(command switch{"invite"=>"組隊邀請","accept"=>"接受邀請","reject"=>"拒絕邀請","leave"=>"離隊請求","kick"=>"踢除請求","delegate"=>"委任隊長請求",_=>"名單查詢"})+"；隊員與 HP 以原服回覆為準。";partyRevision++;}
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
            // R7-J: leader only, a shown member with a known object id (S39 name-only rows have 0) that is not ourselves.
            "delegate"=>PartyPackets.DelegateTarget(shown,name)!=0,
            "accept" or "reject"=>invite!=null,
            _=>false};
        if(!Eligible())return false;
        byte[] packet;try{packet=command switch{"list"=>PartyPackets.List(),"leave"=>PartyPackets.Leave(),"invite"=>PartyPackets.Invite(target,mode),"kick"=>PartyPackets.Kick(name),"delegate"=>PartyPackets.Delegate(PartyPackets.DelegateTarget(shown,name)),_=>PartyPackets.Answer(invite!.Challenge,command=="accept")};}catch(ArgumentException){return false;}
        int gen=SessionGeneration;partySendingGeneration=gen;var sender=sendMovementGate;
        try{return await sender(packet,()=>gen==SessionGeneration&&PartyAccess&&World.PartyCurrent(shown)&&Eligible(),()=>{World.PartySent(command);Set(State,"隊伍請求已送出。");})&&gen==SessionGeneration;}
        catch(Exception){if(gen==SessionGeneration){Cancel();Set(LoginState.Faulted,"組隊傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(partySendingGeneration==gen)partySendingGeneration=-1;}
    }

    /// <summary>
    /// R9-C（#847，FU9§9.0、§9.1）：組隊聊天指令。
    /// - `/invite`（410「邀請隊員」）不帶名稱：進選目標模式（179），點中玩家後由 PickPartyInviteTarget 送 C230 type 0。
    ///   帶名稱：原版送 TW16 op 42 type 4／5，3.81 C_CreateParty 沒有這兩種，不送，只提示。
    /// - `/chatinvite 名稱`（957）→ C230 type 2；不帶名稱時原版開 960 輸入框，這裡印 960 並提示格式。
    /// - `/banish 名稱`（411）→ C255（以名稱踢人，C_BanParty）。
    /// - `/outparty`（412）→ C33 離隊（原版 `c` 119）。
    /// 隊長與人數等條件由伺服器判斷（原版也不在本機擋，回覆 416／417／426 等）。
    /// </summary>
    internal ChatCommandPlan PartyChatCommand(ChatFollowupCommand c){
        switch(c.Kind){
        case ChatFollowupKind.Invite:
            if(c.Argument.Length>0)return ChatCommandPlan.Refuse("3.81 不支援以名稱邀請組隊（原版 op 42 type 4／5 沒有對應），未送出；請輸入 /invite 後點選玩家。");
            return ChatCommandPlan.Do(ChatFollowupCommands.SelectTargetText+"（點選要邀請的玩家）",()=>{World.BeginPartyInvitePick();return true;});
        case ChatFollowupKind.ChatInvite:
            if(c.Argument.Length==0)return ChatCommandPlan.Refuse(ChatFollowupCommands.ChatInvitePromptText+"（格式：/chatinvite 名稱）");
            return ChatCommandPlan.Send("聊天隊伍邀請已送出；結果以伺服器回傳為準。",null,PartyPackets.ChatInvite(c.Argument));
        case ChatFollowupKind.Banish:
            if(c.Argument.Length==0)return ChatCommandPlan.Refuse("格式：/banish 名稱");
            if(!PartyPackets.ValidName(c.Argument))return ChatCommandPlan.Refuse("驅逐隊員格式：/banish 名稱（名稱不可含空白，最多 52 字）");
            return ChatCommandPlan.Send("驅逐隊員請求已送出；結果以伺服器回傳為準。",()=>World.PartySent("kick"),PartyPackets.Kick(c.Argument));
        default:
            return ChatCommandPlan.Send("離隊請求已送出；結果以伺服器回傳為準。",()=>World.PartySent("leave"),PartyPackets.Leave());
        }
    }
    /// <summary>
    /// R9-C：選目標模式中點中物件（sub_954FC0）：選目標模式結束；對象是 30 格內的有效玩家才送 C230 type 0（物件 id），否則不送。
    /// 回傳是否送出。不在選目標模式回 false、什麼都不做。
    /// </summary>
    public async Task<bool> PickPartyInviteTarget(uint id){
        if(!World.PartyInvitePicking){World.CancelPartyInvitePick();return false;}
        World.CancelPartyInvitePick();
        if(World.PartyInviteCandidate(id) is not {} target){Set(State,"選取的對象不是可邀請的玩家，未送出組隊邀請。");return false;}
        if(!PartyAccess||partySendingGeneration==SessionGeneration||sendMovementGate==null)return false;
        byte[] packet=PartyPackets.Invite(target.ObjectId,0);
        int gen=SessionGeneration,epoch=World.WorldEpoch;partySendingGeneration=gen;var sender=sendMovementGate;
        try{return await sender(packet,()=>gen==SessionGeneration&&epoch==World.WorldEpoch&&PartyAccess&&World.PartyInviteCandidate(target.ObjectId)!=null,()=>{World.PartySent("invite");Set(State,"隊伍請求已送出。");})&&gen==SessionGeneration;}
        catch(Exception){if(gen==SessionGeneration){Cancel();Set(LoginState.Faulted,"組隊傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(partySendingGeneration==gen)partySendingGeneration=-1;}
    }
}
