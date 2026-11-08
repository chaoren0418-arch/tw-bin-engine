using System.Buffers.Binary;
namespace P06;

// M7-C（T205／#511）：城堡城主、稅率、金庫。只做協定、資料與文字通知；王冠動畫（圖源待查）與 NPC 對話窗 tax／withdraw／deposit 頁不在本檔。
// 原版行為依 docs/reference/windows-2.md §5.1（T226）：S69 在城堡編號 < 9 時寫入城主表 table[城堡]=物件 id；
// 只有第 1、3–8 格畫王冠，第 2 格不畫；type 0 只寫第 0 格（不會清掉其他格）。
// 格式依 docs/reference/server-protocol.md（#132）：
//   S69  S_CastleMaster  C(69) C(type) D(leaderId)         type＝城堡編號；原服失城送 0，妖魔城（2）改送 8 以便顯示王冠。
//   S185 S_TaxRate       C(185) D(objId) C(10) C(50)       objId＝自己角色 id；後兩個 byte 是可設定稅率下限／上限（TW16 多讀一個 C，落在補零，意義待驗證）。
//   S4   S_Deposit       C(4) D(objId)                     打開「存入金庫」。
//   S141 S_Drawal        C(141) D(objId) D(count)          打開「提領金庫」；count＝可提領上限（原服已截在 2,000,000,000）。
//   C19  C_TaxRate       C(19) D(objId) C(rate)            原服只收 objId＝自己角色 id、10 ≤ rate ≤ 50。
//   C56  C_Deposit       C(56) D(objId) D(count)           原服只收 1 ≤ count ≤ int.MaxValue。
//   C44  C_Drawal        C(44) D(objId) D(count)           原服只收 1 ≤ count ≤ S141 的 count。
// 原服每次詢問只發一張一次性票（takeCurrent），所以每個詢問最多回一次；回覆或取消後就清掉。

public enum CastleOfferKind:byte {TaxRate=1,Deposit=2,Withdraw=3}
/// <summary>城主表一格。Crowned＝原版會畫王冠（第 1、3–8 格）。</summary>
public sealed record CastleCrown(byte Castle,uint Leader,bool Crowned);
public sealed record CastleOffer(CastleOfferKind Kind,uint ObjectId,long Min,long Max,long Revision);
public sealed record CastleView(int Generation,int WorldEpoch,uint Actor,long Revision,bool CanSend,string Notice,CastleOffer? Offer,CastleCrown[] Crowns);

public static class CastleProtocol {
    public const byte SCastleMaster=69,STaxRate=185,SDrawal=141,SDeposit=4,CTaxRate=19,CDrawal=44,CDeposit=56;
    public const long DepositMax=int.MaxValue,WithdrawCap=2_000_000_000;
    // 城堡編號依原服 L1CastleLocation（1 肯特 … 8 狄亞得）。原服把妖魔城城主改送 8（第 2 格原版不畫王冠），所以第 8 格的文字只能照編號，可能其實是妖魔城。
    public static string CastleName(byte id)=>id switch{
        1=>"肯特城",2=>"妖魔城",3=>"風木城",4=>"奇巖城",5=>"海音城",6=>"侏儒城",7=>"亞丁城",8=>"狄亞得要塞",_=>$"城堡 {id}"};

    public const int MasterSlots=9;
    public static bool CrownSlot(byte castle)=>castle is 1 or >= 3 and <= 8;
    public static (byte Castle,uint Leader) DecodeCastleMaster(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SCastleMaster)throw new ProtocolException("castle master opcode");
        byte castle=r.C();uint leader=r.D();r.Finish();
        return (castle,leader);
    }
    public static (uint ObjectId,byte Min,byte Max) DecodeTaxRate(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=STaxRate)throw new ProtocolException("tax rate opcode");
        uint id=r.D();byte min=r.C(),max=r.C();r.Finish();
        if(id==0||min>max)throw new ProtocolException("tax rate fields");
        return (id,min,max);
    }
    public static uint DecodeDeposit(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SDeposit)throw new ProtocolException("deposit opcode");
        uint id=r.D();r.Finish();if(id==0)throw new ProtocolException("deposit object");
        return id;
    }
    public static (uint ObjectId,long Count) DecodeDrawal(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SDrawal)throw new ProtocolException("drawal opcode");
        uint id=r.D();int count=unchecked((int)r.D());r.Finish();
        if(id==0||count<0)throw new ProtocolException("drawal fields");
        return (id,count);
    }

    static byte[] IdAndCount(byte opcode,uint id,long count){
        var p=new byte[9];p[0]=opcode;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(1),id);BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(5),checked((int)count));
        return LoginPackets.Pad(p);
    }
    /// <summary>依目前詢問組出回覆；數值不在詢問的範圍內丟 ArgumentException（原服會直接丟棄，不回任何訊息）。</summary>
    public static byte[] Reply(CastleOffer offer,long value){
        if(value<offer.Min||value>offer.Max)throw new ArgumentException(offer.Kind==CastleOfferKind.TaxRate
            ?$"稅率必須在 {offer.Min}%～{offer.Max}% 之間。":$"金額必須在 {offer.Min:N0}～{offer.Max:N0} 之間。");
        return offer.Kind switch{
            CastleOfferKind.TaxRate=>TaxRate(offer.ObjectId,(byte)value),
            CastleOfferKind.Deposit=>IdAndCount(CDeposit,offer.ObjectId,value),
            CastleOfferKind.Withdraw=>IdAndCount(CDrawal,offer.ObjectId,value),
            _=>throw new ArgumentException("未知城堡操作。")};
    }
    public static byte[] TaxRate(uint id,byte rate){
        var p=new byte[6];p[0]=CTaxRate;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(1),id);p[5]=rate;
        return LoginPackets.Pad(p);
    }
    public static byte[] Deposit(uint id,long count)=>IdAndCount(CDeposit,id,count);
    public static byte[] Withdraw(uint id,long count)=>IdAndCount(CDrawal,id,count);

    public static string Prompt(CastleOffer offer)=>offer.Kind switch{
        CastleOfferKind.TaxRate=>$"城堡管家詢問稅率設定（可設定 {offer.Min}%～{offer.Max}%）。",
        CastleOfferKind.Deposit=>"城堡管家詢問要存入金庫的金幣數量。",
        _=>$"城堡管家詢問要從金庫提領的金幣數量（最多 {offer.Max:N0}）。"};
    public static string Sent(CastleOfferKind kind,long value)=>kind switch{
        CastleOfferKind.TaxRate=>$"已送出稅率設定 {value}%，等待伺服器處理。",
        CastleOfferKind.Deposit=>$"已送出存入金庫 {value:N0} 金幣，等待伺服器處理。",
        _=>$"已送出從金庫提領 {value:N0} 金幣，等待伺服器處理。"};
}

public sealed partial class WorldSession {
    readonly uint[] castleMasters=new uint[CastleProtocol.MasterSlots];
    CastleOffer? castleOffer;long castleRevision;string castleNotice="";
    public CastleOffer? CastleOffer=>castleOffer;
    public long CastleRevision=>castleRevision;
    /// <summary>城主表第 1–8 格中有物件 id 的格子（第 0 格是失城通知寫入處，不列出）。</summary>
    public CastleCrown[] CastleCrowns=>Enumerable.Range(1,CastleProtocol.MasterSlots-1).Where(i=>castleMasters[i]!=0)
        .Select(i=>new CastleCrown((byte)i,castleMasters[i],CastleProtocol.CrownSlot((byte)i))).ToArray();
    public uint CastleMaster(byte castle)=>castle<CastleProtocol.MasterSlots?castleMasters[castle]:0;
    /// <summary>原版是否會在這個物件頭上畫王冠（物件 id 等於第 1、3–8 格之一）。</summary>
    public bool HasCastleCrown(uint objectId)=>objectId!=0&&CastleCrowns.Any(c=>c.Crowned&&c.Leader==objectId);

    void CastleLine(string text){
        castleNotice=text;castleRevision++;
        playerChatLines.Enqueue(new(++playerChatSequence,0,"[城堡] "+text));
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
    string CastleLeaderName(uint leader)=>objects.TryGetValue(leader,out var o)&&!string.IsNullOrWhiteSpace(o.Name)?PlayerChatProtocol.Display(o.Name):$"#{leader}";

    [WorldReceive(CastleProtocol.SCastleMaster)]
    bool ReceiveCastleMaster(byte[] plain){
        var (castle,leader)=CastleProtocol.DecodeCastleMaster(plain);
        // 原版：編號 ≥ 9 不寫入；同值重送不另行通知。
        if(castle>=CastleProtocol.MasterSlots||castleMasters[castle]==leader){castleRevision++;return true;}
        castleMasters[castle]=leader;
        // 第 0 格不畫王冠也不清別格（原服失城通知）；本引擎只給文字，不推測王冠會消失。
        if(castle==0)CastleLine($"伺服器通知：{CastleLeaderName(leader)} 的血盟失去城堡。");
        else if(leader==0)CastleLine($"{CastleProtocol.CastleName(castle)}目前沒有城主。");
        else CastleLine($"{CastleLeaderName(leader)} 成為{CastleProtocol.CastleName(castle)}城主。");
        return true;
    }
    void OpenCastleOffer(CastleOfferKind kind,uint id,long min,long max){
        castleOffer=new(kind,id,min,max,castleRevision+1);
        CastleLine(CastleProtocol.Prompt(castleOffer));
    }
    [WorldReceive(CastleProtocol.STaxRate)]
    bool ReceiveCastleTaxRate(byte[] plain){var (id,min,max)=CastleProtocol.DecodeTaxRate(plain);OpenCastleOffer(CastleOfferKind.TaxRate,id,min,max);return true;}
    [WorldReceive(CastleProtocol.SDeposit)]
    bool ReceiveCastleDeposit(byte[] plain){OpenCastleOffer(CastleOfferKind.Deposit,CastleProtocol.DecodeDeposit(plain),1,CastleProtocol.DepositMax);return true;}
    [WorldReceive(CastleProtocol.SDrawal)]
    bool ReceiveCastleDrawal(byte[] plain){
        var (id,count)=CastleProtocol.DecodeDrawal(plain);
        if(count<1){castleOffer=null;CastleLine("城堡金庫目前沒有可以提領的金幣。");return true;}
        OpenCastleOffer(CastleOfferKind.Withdraw,id,1,Math.Min(count,CastleProtocol.WithdrawCap));return true;
    }

    /// <summary>為目前詢問組回覆封包；詢問已換掉（revision 不同）或角色不符時回 null。</summary>
    internal byte[]? CastleRequest(long revision,uint actor,long value){
        if(castleOffer is not {} offer||offer.Revision!=revision)return null;
        if(actor==0||actor!=offer.ObjectId)return null;
        return CastleProtocol.Reply(offer,value);
    }
    internal void CastleSent(long revision,long value){
        if(castleOffer is not {} offer||offer.Revision!=revision)return;
        castleOffer=null;CastleLine(CastleProtocol.Sent(offer.Kind,value));
    }
    /// <summary>不回覆就關掉詢問（原服的票會自然失效，不送任何封包）。</summary>
    public bool CancelCastleOffer(long revision){
        if(castleOffer is not {} offer||offer.Revision!=revision)return false;
        castleOffer=null;castleNotice="已取消城堡操作。";castleRevision++;return true;
    }
    internal CastleView CastleSnapshot(int generation,bool canSend)=>new(generation,WorldEpoch,Actor?.ObjectId??0,castleRevision,canSend&&castleOffer!=null,castleNotice,castleOffer,CastleCrowns);

    [WorldReset]
    void ResetCastle(){Array.Clear(castleMasters);castleOffer=null;castleNotice="";castleRevision++;}
    [WorldSnapshot("castle")]
    object? CastleSnapshotModel()=>new{revision=castleRevision,notice=castleNotice,
        offer=castleOffer is {} o?new{kind=o.Kind.ToString(),objectId=o.ObjectId,min=o.Min,max=o.Max,revision=o.Revision}:null,
        masters=CastleCrowns.Select(c=>new{castle=c.Castle,leader=c.Leader,crowned=c.Crowned,name=CastleProtocol.CastleName(c.Castle)}).ToArray(),lostNotice=castleMasters[0]};
}

public sealed partial class LoginConnection {
    int castleSendingGeneration=-1;
    bool CastleAccess=>State==LoginState.InWorld&&World.Ready&&World.Actor is {} a&&World.Status is {Hp:>0} s&&a.ObjectId==s.ObjectId
        &&!World.OwnDead&&!World.TeleportPending&&!World.ActionLocked;
    public CastleView CastleSnapshot()=>CaptureWorld(()=>World.CastleSnapshot(generation,CastleAccess&&castleSendingGeneration!=generation));
    public bool CancelCastleOffer(CastleView v)=>CaptureWorld(()=>v.Generation==generation&&v.WorldEpoch==World.WorldEpoch&&v.Offer is {} o&&World.CancelCastleOffer(o.Revision));
    /// <summary>回覆目前的城堡詢問（稅率 C19／存入 C56／提領 C44）。v 必須是目前的快照；value 不在範圍內不送。</summary>
    public async Task<bool> SendCastleReply(CastleView v,long value){
        if(v.Offer is not {} offer)return false;
        bool Valid()=>v.Generation==generation&&v.WorldEpoch==World.WorldEpoch&&v.Actor==World.Actor?.ObjectId&&CastleAccess&&World.CastleOffer?.Revision==offer.Revision;
        if(castleSendingGeneration==generation||sendMovementGate==null||!CaptureWorld(Valid))return false;
        byte[]? packet;
        try{packet=CaptureWorld(()=>World.Actor is {} own?World.CastleRequest(offer.Revision,own.ObjectId,value):null);}
        catch(ArgumentException){return false;}
        if(packet==null)return false;
        var sender=sendMovementGate;int mine=generation;castleSendingGeneration=mine;
        try{return await sender(packet,()=>CaptureWorld(Valid),()=>{World.CastleSent(offer.Revision,value);Changed?.Invoke();})&&v.Generation==generation;}
        catch(Exception){if(mine==generation){Cancel();Set(LoginState.Faulted,"城堡操作傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(castleSendingGeneration==mine)castleSendingGeneration=-1;}
    }
}
