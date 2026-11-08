using System.Buffers.Binary;
namespace P06;

// M7-H（T209／#515）：血盟推薦。只做協定與資料；TW16 的 MatchMakingUI 分支不建立物件（gap-matrix §4.1），
// op 133 處理函式 sub_B243A0 的畫面未查證，所以本檔不做視窗，只留快照與聊天區文字通知（暫代）。
// 格式依 docs/reference/server-protocol.md（#132）：
//   S0  S_PacketBoxPledgeRecommend（plain[1]＝type）
//     type 0／1  C(type) C(0)                                      0＝登錄或更新推薦成功，1＝撤下推薦成功
//     type 2     C(2) C(0) C(n) n×{D clanId S clanName S leader D loginLevel C typeId C hasHouse C hasCastle C(0) S typeMessage D emblemId}
//                                                                  推薦血盟清單（伺服器已排除自己申請過的血盟）
//     type 3     C(3) C(0) C(n) n×{D clanId C(0) D clanId S … 同 type 2 …}   自己的申請清單
//     type 4     C(4) C(0x82)                                       自己血盟沒有登錄推薦
//                C(4) C(0) C typeId S typeMessage C(n) n×{D objId C(0) C online S name C classType H lawful H level}
//                                                                  自己血盟的推薦內容與申請者
//     type 5／6  C(type) C(actionType) D(recordId) C(acceptType)    操作結果；actionType 0＝成功，131＝失敗
//   C76 C_PledgeRecommendation  C(data) …
//     0 C(clanType) S(typeMessage)   登錄／更新推薦（盟主）     1 撤下推薦（盟主）
//     2 推薦血盟清單                  3 自己的申請清單           4 自己血盟的推薦與申請者（盟主）
//     5 D(clanId) 申請加入（無血盟者）
//     6 D(objId) C(type)  type 1＝接受申請；2／3＝移除申請（盟主；2、3 原服處理相同，差別待驗證）
//   C78 C_PledgeContent  C(15) S(announce)＝血盟公告（≤ 160 字、不含代理字元）；C(16) S(notes)＝自己的成員備註
//     （同一封包的 13／14 是火神精煉，不屬本模組。）
// 伺服器端資格（盟主、無血盟）由原服判斷，不合時直接丟棄、不回任何封包；本引擎只擋得到的部分（有沒有血盟）。

public enum ClanMatchingAction:byte {Register=0,Unregister=1,ListRecommended=2,ListApplied=3,Detail=4,Apply=5,Answer=6}
public sealed record ClanRecommendation(uint ClanId,string ClanName,string Leader,uint LoginLevel,byte TypeId,bool HasHouse,bool HasCastle,string TypeMessage,uint EmblemId);
public sealed record ClanApplicant(uint ObjectId,bool Online,string Name,byte ClassType,short Lawful,ushort Level);
/// <summary>自己血盟的推薦內容；Registered=false 表示伺服器回「沒有登錄」（0x82）。</summary>
public sealed record ClanMatchingDetail(bool Registered,byte TypeId,string TypeMessage,ClanApplicant[] Applicants);
public sealed record ClanMatchingResult(byte Type,byte ActionType,uint RecordId,byte AcceptType){public bool Success=>ActionType==0;}
public sealed record ClanMatchingRequest(ClanMatchingAction Action,byte[] Packet,uint Target=0,byte AnswerType=0);
public sealed record ClanMatchingView(int Generation,int WorldEpoch,uint Actor,bool InClan,long Revision,bool CanSend,string Notice,
    ClanRecommendation[]? Recommended,ClanRecommendation[]? Applied,ClanMatchingDetail? Detail,ClanMatchingResult? LastResult);

public static class ClanMatchingProtocol {
    public const byte SClanMatching=0,CClanMatching=76,CPledgeContent=78,ContentAnnounce=15,ContentNotes=16;
    public const byte NotRegistered=0x82,Failed=131;
    public const int AnnounceMaxChars=160;

    // ---- 解析 ----
    /// <summary>type 2 每列在 hasCastle 之後有一個固定 C(0)，再接 typeMessage。</summary>
    static ClanRecommendation RecommendRow(LoginReader r){
        uint id=r.D();string name=r.S(),leader=r.S();uint level=r.D();byte type=r.C();bool house=r.C()!=0,castle=r.C()!=0;r.C();
        string message=r.S();uint emblem=r.D();
        if(id==0)throw new ProtocolException("clan matching clan id");
        return new(id,name,leader,level,type,house,castle,message,emblem);
    }
    public static (byte Type,bool Posted) DecodeStatus(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SClanMatching)throw new ProtocolException("clan matching opcode");
        byte type=r.C();if(type>1)throw new ProtocolException("clan matching status type");
        r.C();r.Finish();return (type,type==0);
    }
    public static ClanRecommendation[] DecodeRecommended(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SClanMatching||r.C()!=2)throw new ProtocolException("clan matching list type");
        r.C();int n=r.C();var rows=new ClanRecommendation[n];
        for(int i=0;i<n;i++)rows[i]=RecommendRow(r);
        r.Finish();return rows;
    }
    public static ClanRecommendation[] DecodeApplied(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SClanMatching||r.C()!=3)throw new ProtocolException("clan matching applied type");
        r.C();int n=r.C();var rows=new ClanRecommendation[n];
        for(int i=0;i<n;i++){
            uint key=r.D();r.C();var row=RecommendRow(r);
            if(row.ClanId!=key)throw new ProtocolException("clan matching applied key");
            rows[i]=row;
        }
        r.Finish();return rows;
    }
    public static ClanMatchingDetail DecodeDetail(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SClanMatching||r.C()!=4)throw new ProtocolException("clan matching detail type");
        byte flag=r.C();
        if(flag==NotRegistered){r.Finish();return new(false,0,"",[]);}
        if(flag!=0)throw new ProtocolException("clan matching detail flag");
        byte type=r.C();string message=r.S();int n=r.C();var list=new ClanApplicant[n];
        for(int i=0;i<n;i++){
            uint id=r.D();r.C();bool online=r.C()!=0;string name=r.S();byte cls=r.C();short lawful=unchecked((short)r.H());ushort level=r.H();
            if(id==0)throw new ProtocolException("clan matching applicant id");
            list[i]=new(id,online,name,cls,lawful,level);
        }
        r.Finish();return new(true,type,message,list);
    }
    public static ClanMatchingResult DecodeResult(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SClanMatching)throw new ProtocolException("clan matching opcode");
        byte type=r.C();if(type is not (5 or 6))throw new ProtocolException("clan matching result type");
        var value=new ClanMatchingResult(type,r.C(),r.D(),r.C());r.Finish();return value;
    }

    // ---- 送出 ----
    static void Text(string value,bool allowEmpty,int maxChars,string label){
        if(value==null||(!allowEmpty&&value.Length==0)||value.Length>maxChars||value.Any(char.IsSurrogate)||value.Any(c=>char.IsControl(c)))
            throw new ArgumentException($"{label}不可空白、不可含控制字元或罕用字，且最多 {maxChars} 字。");
        LoginPackets.TextEncoding.GetByteCount(value); // 不能以 CP950 編碼時丟 EncoderFallbackException（ArgumentException 子類別）
    }
    static ClanMatchingRequest Simple(ClanMatchingAction a)=>new(a,LoginPackets.Pad([CClanMatching,(byte)a]));
    /// <summary>C76 0：登錄或更新自己血盟的推薦（盟主）。說明文字長度上限原版待查，這裡只擋空白以外的明顯錯誤（≤ 160 字）。</summary>
    public static ClanMatchingRequest Register(byte clanType,string message){
        Text(message,true,AnnounceMaxChars,"推薦說明");
        return new(ClanMatchingAction.Register,LoginPackets.Pad([CClanMatching,0,clanType,..LoginPackets.TextEncoding.GetBytes(message),0]));
    }
    public static ClanMatchingRequest Unregister()=>Simple(ClanMatchingAction.Unregister);
    public static ClanMatchingRequest ListRecommended()=>Simple(ClanMatchingAction.ListRecommended);
    public static ClanMatchingRequest ListApplied()=>Simple(ClanMatchingAction.ListApplied);
    public static ClanMatchingRequest Detail()=>Simple(ClanMatchingAction.Detail);
    /// <summary>C76 5：申請加入血盟（原服只收無血盟者、clanId≠0）。</summary>
    public static ClanMatchingRequest Apply(uint clanId){
        if(clanId==0)throw new ArgumentException("血盟編號不可為 0。");
        var p=new byte[6];p[0]=CClanMatching;p[1]=5;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(2),clanId);
        return new(ClanMatchingAction.Apply,LoginPackets.Pad(p),clanId);
    }
    /// <summary>C76 6：盟主處理申請；answer 1＝接受，2／3＝移除申請。</summary>
    public static ClanMatchingRequest Answer(uint objectId,byte answer){
        if(objectId==0)throw new ArgumentException("申請者編號不可為 0。");
        if(answer is not (1 or 2 or 3))throw new ArgumentException("處理方式只能是 1（接受）、2 或 3（移除）。");
        var p=new byte[7];p[0]=CClanMatching;p[1]=6;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(2),objectId);p[6]=answer;
        return new(ClanMatchingAction.Answer,LoginPackets.Pad(p),objectId,answer);
    }
    /// <summary>C78 15：修改血盟公告（原服限 160 字、不得含代理字元；可為空字串）。</summary>
    public static byte[] Announce(string text){
        Text(text,true,AnnounceMaxChars,"血盟公告");
        return LoginPackets.Pad([CPledgeContent,ContentAnnounce,..LoginPackets.TextEncoding.GetBytes(text),0]);
    }
    /// <summary>C78 16：修改自己的成員備註（原服無長度檢查；原版輸入上限待查，這裡暫用 160 字）。</summary>
    public static byte[] Notes(string text){
        Text(text,true,AnnounceMaxChars,"成員備註");
        return LoginPackets.Pad([CPledgeContent,ContentNotes,..LoginPackets.TextEncoding.GetBytes(text),0]);
    }

    // ---- 文字 ----
    public static string ClassName(byte type)=>type switch{0=>"王族",1=>"騎士",2=>"妖精",3=>"法師",4=>"黑暗妖精",5=>"龍騎士",6=>"幻術師",_=>$"職業 {type}"};
    public static string ResultText(ClanMatchingResult r,string target)=>(r.Type,r.AcceptType,r.Success) switch{
        (5,_,true)=>$"已送出加入 {target} 的申請。",
        (5,_,false)=>$"加入 {target} 的申請失敗。",
        (6,1,true)=>$"已接受 {target} 加入血盟。",
        (6,1,false)=>$"無法接受 {target} 的申請。",
        (6,_,true)=>$"已移除 {target} 的申請。",
        _=>$"無法移除 {target} 的申請。"};
    public static string Sent(ClanMatchingRequest q)=>q.Action switch{
        ClanMatchingAction.Register=>"已送出血盟推薦登錄，等待伺服器處理。",
        ClanMatchingAction.Unregister=>"已送出撤下血盟推薦，等待伺服器處理。",
        ClanMatchingAction.ListRecommended=>"正在查詢推薦血盟清單。",
        ClanMatchingAction.ListApplied=>"正在查詢自己的申請清單。",
        ClanMatchingAction.Detail=>"正在查詢本血盟的推薦內容與申請者。",
        ClanMatchingAction.Apply=>$"已送出加入血盟 #{q.Target} 的申請，等待伺服器處理。",
        _=>q.AnswerType==1?$"已送出接受申請者 #{q.Target}，等待伺服器處理。":$"已送出移除申請者 #{q.Target}，等待伺服器處理。"};
}

public sealed partial class WorldSession {
    ClanRecommendation[]? clanRecommended,clanApplied;ClanMatchingDetail? clanMatchingDetail;ClanMatchingResult? clanMatchingResult;
    long clanMatchingRevision;string clanMatchingNotice="";
    public ClanRecommendation[]? ClanRecommended=>clanRecommended;
    public ClanRecommendation[]? ClanApplied=>clanApplied;
    public ClanMatchingDetail? ClanMatchingDetail=>clanMatchingDetail;
    public ClanMatchingResult? ClanMatchingResult=>clanMatchingResult;
    public long ClanMatchingRevision=>clanMatchingRevision;

    void ClanMatchingLine(string text){
        clanMatchingNotice=text;clanMatchingRevision++;
        playerChatLines.Enqueue(new(++playerChatSequence,0,"[血盟推薦] "+text));
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
    string ClanMatchingClanName(uint id)=>
        (clanRecommended?.FirstOrDefault(c=>c.ClanId==id)??clanApplied?.FirstOrDefault(c=>c.ClanId==id)) is {} c&&c.ClanName.Length>0?PlayerChatProtocol.Display(c.ClanName):$"血盟 #{id}";
    string ClanMatchingApplicantName(uint id)=>clanMatchingDetail?.Applicants.FirstOrDefault(a=>a.ObjectId==id) is {Name.Length:>0} a?PlayerChatProtocol.Display(a.Name):$"#{id}";

    [WorldReceive(ClanMatchingProtocol.SClanMatching)]
    bool ReceiveClanMatching(byte[] plain){
        if(plain.Length<2)throw new ProtocolException("clan matching truncated");
        switch(plain[1]){
            case 0: case 1:{
                var (_,posted)=ClanMatchingProtocol.DecodeStatus(plain);
                // 原服登錄／撤下成功後不另送 type 4；本機的推薦內容已不準，清掉讓 UI 重查。
                clanMatchingDetail=posted?null:new(false,0,"",[]);
                ClanMatchingLine(posted?"血盟推薦已登錄。":"血盟推薦已撤下。");return true;}
            case 2:
                clanRecommended=ClanMatchingProtocol.DecodeRecommended(plain);
                ClanMatchingLine($"收到推薦血盟清單，共 {clanRecommended.Length} 個。");return true;
            case 3:
                clanApplied=ClanMatchingProtocol.DecodeApplied(plain);
                ClanMatchingLine($"收到自己的申請清單，共 {clanApplied.Length} 個。");return true;
            case 4:
                clanMatchingDetail=ClanMatchingProtocol.DecodeDetail(plain);
                ClanMatchingLine(clanMatchingDetail.Registered?$"本血盟的推薦已登錄，申請者 {clanMatchingDetail.Applicants.Length} 人。":"本血盟目前沒有登錄推薦。");return true;
            case 5: case 6:{
                var r=ClanMatchingProtocol.DecodeResult(plain);clanMatchingResult=r;
                string target=r.Type==5?ClanMatchingClanName(r.RecordId):ClanMatchingApplicantName(r.RecordId);
                if(r.Success&&r.Type==5&&clanRecommended!=null)clanRecommended=clanRecommended.Where(c=>c.ClanId!=r.RecordId).ToArray();
                if(r.Success&&r.Type==6&&clanMatchingDetail is {Registered:true} d)clanMatchingDetail=d with{Applicants=d.Applicants.Where(a=>a.ObjectId!=r.RecordId).ToArray()};
                ClanMatchingLine(ClanMatchingProtocol.ResultText(r,target));return true;}
            default:return false; // 未知 type 計入 unhandled
        }
    }

    internal void ClanMatchingSent(ClanMatchingRequest q){ClanMatchingLine(ClanMatchingProtocol.Sent(q));}
    internal void ClanContentSent(byte kind){ClanMatchingLine(kind==ClanMatchingProtocol.ContentAnnounce?"已送出血盟公告修改，等待伺服器處理。":"已送出成員備註修改，等待伺服器處理。");}
    /// <summary>本機能判斷的資格：申請加入要沒有血盟；盟主操作與公告／備註要有血盟。是否為盟主由原服判斷。</summary>
    internal bool ClanMatchingAllowed(ClanMatchingAction a)=>Actor is {} own&&a switch{
        ClanMatchingAction.Apply=>own.ClanId==0,
        ClanMatchingAction.ListRecommended or ClanMatchingAction.ListApplied=>true,
        _=>own.ClanId!=0};
    internal ClanMatchingView ClanMatchingSnapshot(int generation,bool canSend)=>new(generation,WorldEpoch,Actor?.ObjectId??0,Actor is {ClanId:not 0},
        clanMatchingRevision,canSend,clanMatchingNotice,clanRecommended,clanApplied,clanMatchingDetail,clanMatchingResult);

    [WorldReset]
    void ResetClanMatching(){clanRecommended=clanApplied=null;clanMatchingDetail=null;clanMatchingResult=null;clanMatchingNotice="";clanMatchingRevision++;}
    [WorldSnapshot("clanMatching")]
    object? ClanMatchingSnapshotModel()=>new{revision=clanMatchingRevision,notice=clanMatchingNotice,
        recommended=clanRecommended,applied=clanApplied,detail=clanMatchingDetail,
        lastResult=clanMatchingResult is {} r?new{type=r.Type,actionType=r.ActionType,recordId=r.RecordId,acceptType=r.AcceptType,success=r.Success}:null};
}

public sealed partial class LoginConnection {
    int clanMatchingSendingGeneration=-1;
    bool ClanMatchingAccess=>State==LoginState.InWorld&&World.Ready&&World.Actor is {} a&&World.Status is {Hp:>0} s&&a.ObjectId==s.ObjectId
        &&!World.OwnDead&&!World.TeleportPending&&!World.ActionLocked;
    public ClanMatchingView ClanMatchingSnapshot()=>CaptureWorld(()=>World.ClanMatchingSnapshot(generation,ClanMatchingAccess&&clanMatchingSendingGeneration!=generation));
    /// <summary>送 C76（血盟推薦）。v 必須是目前的快照；本機資格不合（例如有血盟卻申請加入）不送。</summary>
    public Task<bool> SendClanMatching(ClanMatchingView v,ClanMatchingRequest request)=>
        SendClanMatchingPacket(v,request.Packet,()=>World.ClanMatchingAllowed(request.Action),()=>World.ClanMatchingSent(request));
    /// <summary>送 C78 15（血盟公告）或 16（成員備註）；需有血盟。</summary>
    public Task<bool> SendClanContent(ClanMatchingView v,byte kind,string text){
        byte[] packet;
        try{packet=kind switch{ClanMatchingProtocol.ContentAnnounce=>ClanMatchingProtocol.Announce(text),ClanMatchingProtocol.ContentNotes=>ClanMatchingProtocol.Notes(text),_=>throw new ArgumentException("kind")};}
        catch(ArgumentException){return Task.FromResult(false);}
        return SendClanMatchingPacket(v,packet,()=>World.Actor is {ClanId:not 0},()=>World.ClanContentSent(kind));
    }
    async Task<bool> SendClanMatchingPacket(ClanMatchingView v,byte[] packet,Func<bool> allowed,Action sent){
        bool Valid()=>v.Generation==generation&&v.WorldEpoch==World.WorldEpoch&&v.Actor==World.Actor?.ObjectId&&ClanMatchingAccess&&allowed();
        if(clanMatchingSendingGeneration==generation||sendMovementGate==null||!CaptureWorld(Valid))return false;
        var sender=sendMovementGate;int mine=generation;clanMatchingSendingGeneration=mine;
        try{return await sender(packet,()=>CaptureWorld(Valid),()=>{sent();Changed?.Invoke();})&&v.Generation==generation;}
        catch(Exception){if(mine==generation){Cancel();Set(LoginState.Faulted,"血盟推薦操作傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(clanMatchingSendingGeneration==mine)clanMatchingSendingGeneration=-1;}
    }
}
