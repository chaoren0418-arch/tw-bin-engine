using System.Buffers.Binary;

namespace P06;

public enum ClanCommand { Create, Join, Leave, Ban, Emblem, Members }
public sealed record ClanUpdate(uint ObjectId,string Name,string Title,uint Reserved,byte Rank);
public sealed record ClanEmblem(uint Id,int[] Pixels);
public sealed record ClanAttention(ushort Type,string[] Names);
public sealed record ClanAnnouncement(string Name,string Leader,uint EmblemId,uint Created,string Text);
public sealed record ClanMember(string Name,byte Rank,byte Level,string Note,uint ObjectId,byte Class,bool? Online=null);
public sealed record ClanInvitation(long Revision,uint PeerId,string Name,long ExpiresAt);
public sealed record ClanView(int Session,int Epoch,uint Actor,bool CanSend,string Name,string Notice,string[] Attention,
    ClanInvitation? Invitation,ClanAnnouncement? Announcement,ClanMember[] Members);

// Wire evidence and known server ID mismatch: docs/protocol/clan.md.
public static class ClanProtocol {
    // d287 C_Attr discards the zero D; it is not a requestId/confirmation token.
    public static byte[] Answer(bool accept)=>LoginPackets.Pad([121,0,0,0,0,0,0,97,0,(byte)(accept?1:0)]);
    public static ClanAnnouncement DecodeAnnouncement(byte[] packet){
        var r=Reader(packet,167);var value=new ClanAnnouncement(r.S(),r.S(),r.D(),r.D(),FixedText(r,478));r.Finish();return value;
    }
    public static ClanMember[] DecodeMembers(byte[] packet){
        var r=Reader(packet,170);if(r.H()!=1)throw new ProtocolException("clan members header");
        int count=r.C();var members=new ClanMember[count];var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<count;i++){
            var m=new ClanMember(r.S(),r.C(),r.C(),FixedText(r,62),r.D(),r.C());
            if(string.IsNullOrWhiteSpace(m.Name)||m.ObjectId==0||!names.Add(m.Name))throw new ProtocolException("clan member identity");members[i]=m;
        }
        r.Finish();return members;
    }
    public static string[] DecodeOnlineMembers(byte[] packet){
        var r=Reader(packet,250);if(r.C()!=171)throw new ProtocolException("clan online subcode");
        int count=r.H();if(count>255)throw new ProtocolException("clan online count");
        var names=new string[count];var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<count;i++){names[i]=r.S();if(string.IsNullOrWhiteSpace(names[i])||!unique.Add(names[i])||r.C()!=0)throw new ProtocolException("clan online member");}
        r.Finish();return names;
    }
    static string FixedText(LoginReader r,int length){
        var bytes=new byte[length];for(int i=0;i<length;i++)bytes[i]=r.C();int end=Array.IndexOf(bytes,(byte)0);
        return LoginPackets.TextEncoding.GetString(bytes,0,end<0?length:end);
    }
    public static byte[] Encode(ClanCommand command,string name="",uint emblemId=0){
        byte opcode=command switch{ClanCommand.Create=>222,ClanCommand.Join=>194,ClanCommand.Leave=>61,ClanCommand.Ban=>69,ClanCommand.Emblem=>72,ClanCommand.Members=>68,_=>throw new ArgumentException("未知血盟操作。")};
        if(command is ClanCommand.Create or ClanCommand.Ban){
            if(command==ClanCommand.Create&&name.Length>16&&!name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))throw new ArgumentException("血盟名稱最多 16 字。");
            return LoginPackets.Pad([opcode,..NameBytes(name),0]);
        }
        if(command==ClanCommand.Emblem){
            if(emblemId==0)throw new ArgumentException("尚未收到可查詢的盟徽識別碼。");
            byte[] packet=new byte[5];packet[0]=opcode;BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1),emblemId);return LoginPackets.Pad(packet);
        }
        return LoginPackets.Pad([opcode]);
    }
    static byte[] NameBytes(string name){
        if(string.IsNullOrWhiteSpace(name)||name.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))throw new ArgumentException("名稱不可為空白或含控制字元。");
        byte[] text;
        try{text=LoginPackets.TextEncoding.GetBytes(name);}catch(System.Text.EncoderFallbackException){throw new ArgumentException("名稱含遊戲字集無法表示的字元。");}
        // Client input budget; final name/role/level policy belongs to the server.
        if(text.Length>32)throw new ArgumentException("名稱最多 32 個 Big5 位元組。");
        return text;
    }

    // R2-K: C63 C_Rank data 1 = C(1) C(rank) S(name) (server-protocol.md C_Rank; TW16 C_RANK_CONTROL op 10).
    // The server accepts 2–10 (C_Rank.rank, message 2149 otherwise); leader and membership are checked there (785/518/201).
    // Never send other data values from here: 5 changes HP/food, 9 is the dungeon timer list (player-query.md).
    public const byte RankOpcode=63,RankChange=1,MinRank=2,MaxRank=10;
    // Names from the C_Rank.rank source comment; 2–6 and 7–10 are two numbering generations of the same titles.
    public static string RankName(byte rank)=>rank switch{2 or 7=>"一般",3=>"副君主",4 or 10=>"聯盟君主",5 or 8=>"修習騎士",6 or 9=>"守護騎士",_=>"階級 "+rank};
    public static byte[] EncodeRank(byte rank,string name){
        if(rank is <MinRank or >MaxRank)throw new ArgumentException("階級只能是 2–10。");
        return LoginPackets.Pad([RankOpcode,RankChange,rank,..NameBytes(name),0]);
    }
    public static ClanUpdate DecodeUpdate(byte[] packet){
        var r=Reader(packet,72);var value=new ClanUpdate(r.D(),r.S(),r.S(),r.D(),r.C());r.Finish();
        if(value.ObjectId==0)throw new ProtocolException("clan update identity");return value;
    }
    public static ClanAttention DecodeAttention(byte[] packet){
        var r=Reader(packet,200);ushort type=r.H();var names=new List<string>();
        if(type==2){uint count=r.D();if(count>256)throw new ProtocolException("clan attention count");for(uint i=0;i<count;i++)names.Add(r.S());}
        if(r.H()!=0)throw new ProtocolException("clan attention trailer");r.Finish();return new(type,names.ToArray());
    }
    static LoginReader Reader(byte[] packet,byte opcode){var r=new LoginReader(packet);if(r.C()!=opcode)throw new ProtocolException("clan opcode");return r;}

    // Decode only a bounded, uncompressed 16x12 BMP from S118 memory. No image/file loader.
    // Unsupported encodings are reported, never replaced by a fabricated emblem.
    public static ClanEmblem DecodeEmblem(byte[] packet){
        var r=Reader(packet,118);uint id=r.D();if(id==0)throw new ProtocolException("emblem identity");
        var data=packet.AsSpan(5);
        // TW16 S_EMBLEM payload: 384 bytes, 16x12 RGB555, value 0 transparent (sub_96E790). Only when not a BMP.
        // R2-K: the server stores the whole C18 body (ClientBasePacket.readByte), so an uploaded emblem may come back with
        // the uploader's transport padding after the 384 bytes; sub_96E790 reads 384 and ignores the rest. Only zeros may follow.
        if(data.Length is >=384 and <=400&&!(data[0]=='B'&&data[1]=='M')&&data[384..].IndexOfAnyExcept((byte)0)<0)
            return new(id,ClanEmblemProtocol.DecodeRgb555(data[..384]));
        var pixels=DecodeBmp(data,out int length);
        // The BMP size delimits its payload; only transport zero-padding may follow it.
        for(int i=0;i<length;i++)r.C();r.Finish();
        return new(id,pixels);
    }
    // Uncompressed 16x12 BMP (1/4/8/16/24/32 bit) → 192 ARGB pixels. Shared by S118 and the R2-K C18 upload file.
    // length = the BMP's own size field; bytes after it are not part of the image.
    public static int[] DecodeBmp(ReadOnlySpan<byte> data,out int length){
        if(data.Length<54||data.Length>65536||data[0]!='B'||data[1]!='M')throw new ProtocolException("unsupported emblem: expected BMP");
        uint size=BinaryPrimitives.ReadUInt32LittleEndian(data[2..]),offset=BinaryPrimitives.ReadUInt32LittleEndian(data[10..]);
        uint header=BinaryPrimitives.ReadUInt32LittleEndian(data[14..]);
        int width=BinaryPrimitives.ReadInt32LittleEndian(data[18..]),height=BinaryPrimitives.ReadInt32LittleEndian(data[22..]);
        ushort planes=BinaryPrimitives.ReadUInt16LittleEndian(data[26..]),bits=BinaryPrimitives.ReadUInt16LittleEndian(data[28..]);
        uint compression=BinaryPrimitives.ReadUInt32LittleEndian(data[30..]);
        if(size<54||size>data.Length||header!=40||width!=16||height is not (12 or -12)||planes!=1||compression!=0||bits is not (1 or 4 or 8 or 16 or 24 or 32))throw new ProtocolException("unsupported emblem BMP layout");
        int stride=((width*bits+31)/32)*4;
        uint palette=bits<=8?BinaryPrimitives.ReadUInt32LittleEndian(data[46..]):0;
        if(bits<=8&&palette==0)palette=1u<<bits;
        if(palette>(1u<<Math.Min((int)bits,8))||offset<54+palette*4||offset+(uint)(stride*12)>size)throw new ProtocolException("truncated emblem BMP pixels/palette");
        length=(int)size;
        var pixels=new int[192];
        for(int y=0;y<12;y++)for(int x=0;x<16;x++){
            int row=(int)offset+(height>0?11-y:y)*stride,red,green,blue;
            if(bits<=8){int index=bits switch{8=>data[row+x],4=>(data[row+x/2]>>(x%2==0?4:0))&15,_=>(data[row+x/8]>>(7-x%8))&1};if(index>=palette)throw new ProtocolException("emblem palette index");int p=54+index*4;blue=data[p];green=data[p+1];red=data[p+2];}
            else if(bits==16){ushort p=BinaryPrimitives.ReadUInt16LittleEndian(data[(row+x*2)..]);red=((p>>10)&31)*255/31;green=((p>>5)&31)*255/31;blue=(p&31)*255/31;}
            else{int p=row+x*(bits/8);blue=data[p];green=data[p+1];red=data[p+2];}
            pixels[y*16+x]=unchecked((int)0xff000000)|(red<<16)|(green<<8)|blue;
        }
        return pixels;
    }
}

public sealed partial class WorldSession {
    int clanEpoch=-1;
    string[] clanAttention=[];string clanNotice="";
    ClanAnnouncement? clanAnnouncement;ClanMember[] clanMembers=[];HashSet<string>? clanOnline;
    ClanInvitation? clanInvitation;long clanInvitationRevision;bool clanSubscribed;
    int clanInviteTradeRevision,clanInviteInteractionRevision,clanInviteUnhandled;
    string clanInviteOwnClan="",clanInvitePeerClan="",clanInviteMessage="";ServerMessageReport? clanInviteReply;
    void ClearClanMembers(){clanAnnouncement=null;clanMembers=[];clanOnline=null;}
    void EnsureClanEpoch(){
        if(!clanSubscribed){CombatSoundStateChanged+=CheckClanState;clanSubscribed=true;}
        if(clanEpoch==WorldEpoch)return;clanEpoch=WorldEpoch;clanAttention=[];clanNotice="";clanInvitation=null;RetireClanYesNo(clanInvitePrompt,"世界已更新，舊詢問已失效。");clanInvitePrompt=0;ClearClanMembers();
    }
    // R2-K (#625): message 97 (and clan war 221/222) are generic S_ASK questions in the original (WIN §2.1), so the
    // question now sits in the shared input-line slot (YesNoPrompt.cs). The module still admits it first and keeps its
    // own guards; the slot holds the question only while the module's state is live, tracked by the slot prompt id.
    int clanInvitePrompt;
    int OfferClanYesNo(ushort message,string[] args){OfferYesNo(0,message,args);lock(yesNoGate)return yesNo is {} p&&p.Message==message?p.Id:0;}
    bool ClanYesNoHeld(int id){lock(yesNoGate)return id!=0&&yesNo?.Id==id;}
    void RetireClanYesNo(int id,string notice){lock(yesNoGate)if(id!=0&&yesNo?.Id==id)ClearYesNo(notice);}
    void CheckClanState(){
        EnsureClanEpoch();
        if(clanAnnouncement!=null&&clanAnnouncement.Name!=Actor?.Clan)ClearClanMembers();
        if(clanInvitation is not {} invite)return;
        bool changedMessage=(!ReferenceEquals(clanInviteReply,LastServerReply)||clanInviteMessage!=(LastServerMessage??""))&&
            !(LastServerReply is {} reply?ServerStatusMessages.IsInformational(reply.Id,reply.Arguments):ServerStatusMessages.IsInformational(LastServerMessage??""));
        if(!Ready||OwnDead||TeleportPending||ActionClock()>=invite.ExpiresAt||!objects.TryGetValue(invite.PeerId,out var peer)||peer.Dead||peer.Name!=invite.Name||peer.Clan!=clanInvitePeerClan||Actor?.Clan!=clanInviteOwnClan||
            TradeRevision!=clanInviteTradeRevision||InteractionRevision!=clanInviteInteractionRevision||unhandled.GetValueOrDefault((byte)219)!=clanInviteUnhandled||changedMessage)
            ClearClanInvitation("入盟申請已失效或被其他操作取代，請重新申請。");
        // Answered on the input line (Y/N + Enter or YES/NO: YesNoPrompt sends the same C121 H0 D0 H97 C), or replaced
        // by a newer S219: the slot no longer holds this question, so the module stops offering its own answer too.
        else if(!ClanYesNoHeld(clanInvitePrompt)){clanInvitation=null;clanInvitePrompt=0;clanNotice="入盟申請已在輸入列回覆或被新的詢問取代；結果以伺服器回報為準。";}
    }
    public ClanInvitation? PendingClanInvitation {get{CheckClanState();return clanInvitation;}}
    public ClanAnnouncement? ClanAnnouncement {get{CheckClanState();return clanAnnouncement;}}
    public ClanMember[] ClanMembers {get{CheckClanState();return clanMembers.Select(m=>m with{Online=clanOnline==null?null:clanOnline.Contains(m.Name)}).ToArray();}}
    internal void ClearClanInvitation(string notice){clanInvitation=null;clanNotice=notice;RetireClanYesNo(clanInvitePrompt,notice);clanInvitePrompt=0;}
    void ReceiveClanInvitation(LoginReader r,uint reserved){
        string name=r.S();r.Finish();EnsureClanEpoch();
        ClearClanInvitation("收到無法確認對象的入盟申請，請對方重新申請。");
        // Preserve Trade's prior non-252 invalidation when this new route takes over message 97.
        if(TradeState=="invited"){TradeState="attention";TradeRevision++;TradeNotice="原服改送其他確認訊息；舊交易邀請不可再回答，可取消交易。";}
        // Only d287's zero-D, single-string message 97 is supported.
        if(reserved!=0||!Ready||OwnDead||TradeBlocksActions||string.IsNullOrWhiteSpace(name))return;
        var candidates=Objects.Where(o=>o.Name==name&&!o.Dead&&(o.Flags&4)!=0).ToArray();if(candidates.Length!=1)return;
        var peer=candidates[0];clanInviteOwnClan=Actor!.Clan;clanInvitePeerClan=peer.Clan;
        clanInviteTradeRevision=TradeRevision;clanInviteInteractionRevision=InteractionRevision;clanInviteUnhandled=unhandled.GetValueOrDefault((byte)219);
        clanInviteReply=LastServerReply;clanInviteMessage=LastServerMessage??"";
        clanInvitation=new(++clanInvitationRevision,peer.ObjectId,name,ActionClock()+300000);clanNotice=name+" 申請加入血盟，請在聊天輸入列輸入 Y 接受、N 拒絕後按 Enter。";
        clanInvitePrompt=OfferClanYesNo(97,[name]);
    }
    void ReceiveClanAnnouncement(byte[] packet){
        var value=ClanProtocol.DecodeAnnouncement(packet);EnsureClanEpoch();ClearClanMembers();
        if(Ready&&value.Name.Length>0&&value.Name==Actor?.Clan){clanAnnouncement=value;ObserveClanEmblemId(Actor.ObjectId,value.EmblemId);}
    }
    void ReceiveClanMembers(byte[] packet){var value=ClanProtocol.DecodeMembers(packet);CheckClanState();if(clanAnnouncement!=null)clanMembers=value;}
    void ReceiveClanOnlineMembers(byte[] packet){var value=ClanProtocol.DecodeOnlineMembers(packet);CheckClanState();if(clanAnnouncement!=null)clanOnline=new(value,StringComparer.OrdinalIgnoreCase);}
    public string[] ClanAttentionNames {get{EnsureClanEpoch();return (string[])clanAttention.Clone();}}
    public string ClanNotice {get{EnsureClanEpoch();return clanNotice;}}
    public string[] ClanWarEnemies {get{CheckClanWar();return CurrentClanWarEnemies();}}
    public bool IsClanWarEnemy(string clan)=>clan.Length>0&&ClanWarEnemies.Contains(clan,StringComparer.OrdinalIgnoreCase);
    public ClanEmblem? FindClanEmblem(uint id)=>Emblems.Find(id);
    void ReceiveClanUpdate(byte[] packet){
        var update=ClanProtocol.DecodeUpdate(packet);EnsureClanEpoch();
        // S72's D is reserved, not a clan/emblem ID. Clear old identity on membership change.
        if(Actor is {} a&&a.ObjectId==update.ObjectId){Actor=a with{Clan=update.Name,ClanId=a.Clan==update.Name&&update.Name.Length>0?a.ClanId:0,Rank=update.Rank,Title=update.Title};clanNotice=update.Name.Length==0?"伺服器已清除你的血盟。":"伺服器已更新血盟："+update.Name;}
        else if(objects.ContainsKey(update.ObjectId))UpdateObject(update.ObjectId,o=>o with{Clan=update.Name,ClanId=o.Clan==update.Name&&update.Name.Length>0?o.ClanId:0,Rank=update.Rank,Title=update.Title});
    }
    void ReceiveClanEmblem(byte[] packet){
        EnsureClanEpoch();
        try{var emblem=ClanProtocol.DecodeEmblem(packet);Emblems.Store(emblem);clanNotice="已收到伺服器盟徽。";}
        catch(ProtocolException){if(packet.Length>=5)Emblems.Reject(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(1)));clanNotice="收到無法解析的盟徽；保留血盟名稱，未顯示圖像。";}
    }
    void ReceiveClanAttention(byte[] packet){var value=ClanProtocol.DecodeAttention(packet);EnsureClanEpoch();if(value.Type==2)clanAttention=value.Names;}
}

public sealed partial class LoginConnection {
    int clanSendingGeneration=-1;string clanSendNotice="";int clanNoticeGeneration=-1;
    bool ClanAccess=>State==LoginState.InWorld&&World.Ready&&World.Status is {Hp:>0}&&World.Actor!=null&&!World.OwnDead&&!World.TeleportPending&&!World.ActionLocked&&!World.TradeBlocksActions;
    public ClanView ClanSnapshot()=>CaptureWorld(()=>{
        var invitation=World.PendingClanInvitation;
        return new ClanView(generation,World.WorldEpoch,World.Actor?.ObjectId??0,ClanAccess&&clanSendingGeneration!=generation,World.Actor?.Clan??"",World.ClanNotice+(clanNoticeGeneration==generation?"\n"+clanSendNotice:""),World.ClanAttentionNames,invitation,World.ClanAnnouncement,World.ClanMembers);
    });
    public async Task<bool> AnswerClanInvitation(ClanView view,bool accept){
        bool Current()=>ClanAccess&&view.Session==generation&&view.Epoch==World.WorldEpoch&&view.Actor==World.Actor?.ObjectId&&
            view.Invitation!=null&&view.Invitation==World.PendingClanInvitation;
        if(!Current()||clanSendingGeneration==generation||sendMovementGate==null)return false;
        int session=generation;var sender=sendMovementGate;clanSendingGeneration=session;
        try{
            bool sent=await sender(ClanProtocol.Answer(accept),Current,()=>World.ClearClanInvitation(accept?"已接受入盟申請；等待伺服器更新。":"已拒絕入盟申請。"));
            if(session!=generation||view.Epoch!=World.WorldEpoch)return false;Changed?.Invoke();return sent;
        }catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"入盟回覆傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(clanSendingGeneration==session)clanSendingGeneration=-1;}
    }
    public async Task<bool> SendClan(ClanCommand command,ClanView view,string name="",uint emblemId=0){
        bool Current()=>ClanAccess&&view.Session==generation&&view.Epoch==World.WorldEpoch&&view.Actor==World.Actor?.ObjectId&&view.Actor==World.Status?.ObjectId;
        if(!Current()||clanSendingGeneration==generation||sendMovementGate==null)return false;
        byte[] packet;
        try{packet=ClanProtocol.Encode(command,name,emblemId);}catch(ArgumentException ex){clanNoticeGeneration=generation;clanSendNotice=ex.Message;return false;}
        int session=generation;var sender=sendMovementGate;clanSendingGeneration=session;
        // Face-to-face requests carry no target. Reject queued requests if either participant moved.
        var actor=World.Actor;var peer=World.FacingTradeTarget;
        if(command==ClanCommand.Join&&(peer==null||MovementBusy)){clanSendingGeneration=-1;clanNoticeGeneration=session;clanSendNotice="請選取相鄰且面對面的招募者，停止移動後申請。";return false;}
        try{
            bool sent=await sender(packet,()=>Current()&&(command!=ClanCommand.Join||!MovementBusy&&actor==World.Actor&&peer==World.FacingTradeTarget),()=>{if(World.PendingClanInvitation!=null)World.ClearClanInvitation("血盟操作已取代舊入盟申請。");});
            if(session!=generation||view.Epoch!=World.WorldEpoch)return false;
            clanNoticeGeneration=session;clanSendNotice=sent?"血盟請求已送出；結果以伺服器回報為準。":"角色或場景已變更，請求未送出。";Changed?.Invoke();return sent;
        }catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,"血盟請求傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(clanSendingGeneration==session)clanSendingGeneration=-1;}
    }
    // R2-K: C63 rank change and C18 emblem upload share SendClan's guards (one request at a time, same session/world/actor).
    // The engine only checks that the player has a clan; leader rights and membership stay with the server (219/785/518/201).
    async Task<bool> SendClanPacket(ClanView view,Func<byte[]> build,string label){
        bool Current()=>ClanAccess&&view.Session==generation&&view.Epoch==World.WorldEpoch&&view.Actor==World.Actor?.ObjectId&&view.Actor==World.Status?.ObjectId&&view.Name.Length>0&&view.Name==World.Actor?.Clan;
        if(!Current()||clanSendingGeneration==generation||sendMovementGate==null){
            if(view.Session==generation&&view.Name.Length==0){clanNoticeGeneration=generation;clanSendNotice="你沒有血盟，不能"+label+"。";}
            return false;
        }
        byte[] packet;
        try{packet=build();}catch(ArgumentException ex){clanNoticeGeneration=generation;clanSendNotice=ex.Message;return false;}
        int session=generation;var sender=sendMovementGate;clanSendingGeneration=session;
        try{
            bool sent=await sender(packet,Current,()=>{});
            if(session!=generation||view.Epoch!=World.WorldEpoch)return false;
            clanNoticeGeneration=session;clanSendNotice=sent?label+"請求已送出；結果以伺服器回報為準。":"角色或場景已變更，請求未送出。";Changed?.Invoke();return sent;
        }catch(Exception){if(session==generation){Cancel();Set(LoginState.Faulted,label+"請求傳送失敗，已斷線；沒有自動重送。");}return false;}
        finally{if(clanSendingGeneration==session)clanSendingGeneration=-1;}
    }
    public Task<bool> SendClanRank(ClanView view,string name,byte rank)=>SendClanPacket(view,()=>ClanProtocol.EncodeRank(rank,name),"設定階級");
    public Task<bool> UploadClanEmblem(ClanView view,int[] pixels)=>SendClanPacket(view,()=>ClanEmblemProtocol.EncodeUpload(pixels),"上傳盟徽");
}
