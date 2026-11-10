using System.Buffers.Binary;
namespace P06;

// M7-G（T208／#514）：以物品學魔法（TW16 op 175 S_EXCHANGEABLE_SPELL_LIST，C_EXCHANGEABLE_SPELL／C_EXCHANGE_SPELL）。
// 3.81 伺服器的對應與格式依 docs/reference/server-protocol.md（#132）與 gap-matrix 第 1 節 op 175：
//   C245 C_SkillBuyItem    C(245) D(npcObjId)                        要求清單；原服只收「目前有對話窗的 NPC」且距離 ≤ 5。
//   S13  S_SkillBuyItem    C(13) H(count) count×D(skillIndex)        可用物品學的魔法；skillIndex＝skillId−1，原服已濾掉已學技能；空清單只有 H(0)。
//   C191 C_SkillBuyItemOK  C(191) H(count) count×D(skillIndex)       選好後送出；原服逐筆 +1 換回 skillId。
// 原服規則（C_SkillBuyItemOK.start）：count 必須 1..清單長度；S13 發的是一次性授權票（takeSkills），所以一份清單最多回一次；
// 鬼魂、死亡、傳送中、個人商店中不處理。學會時回 S_AddSkill（引擎的 LearnedMagic 會更新），材料不足回 S_ItemError，
// 未設定材料回 S71 訊息 939。本模組不自行授予技能，也不扣本機背包。
// R2-F（T261／#622，依 SS2§1.1、§1.2）：
//   HyperAct `xchgspell`（sub_585520）是 C245 唯一送出點：NPC 外型＝834 才送 RequestSpellExchangeList()，否則什麼都不送；
//   `buyspell`（sub_581C10）反過來，外型 ≠ 834 才送（金幣學魔法）。兩個字串伺服器都不處理，一律由客戶端轉成封包。
//   S13 空清單：原版改開 NPC 對話窗的 HTML 頁 `HorunEv3`；有清單才開學魔法窗。
// R3-F（T286／#679）：清單改在原生 TRADE_WINDOW 模式 7 顯示（SpellTradeSource.cs，不顯示單價，左鈕送 C191）；暫行 WinForms 窗已拿掉。
//   op 44（S41 金幣學魔法）的模式 7 與空清單換頁（GerengEv3／Sedia1／Siriss1）也在 SpellTradeSource.cs。

public sealed record SpellExchangeRow(int Index,int SkillId,string Name,bool Learned);
public sealed record SpellExchangeOffer(uint? Npc,int[] Indices,long Revision);
public sealed record SpellExchangeView(int Generation,int WorldEpoch,uint Actor,long Revision,bool CanRequest,bool CanSend,
    string Phase,string Notice,uint? Npc,long OfferRevision,SpellExchangeRow[] Rows,int[] PendingSkills,int PendingLearned);

public static class SpellExchangeProtocol {
    public const byte SList=13,CRequest=245,CLearn=191;
    /// <summary>單一清單的上限；原服清單來自各職業技能表，遠小於此值。超過視為封包錯誤。</summary>
    public const int MaxRows=1024;

    public static int[] DecodeList(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=SList)throw new ProtocolException("skill buy item opcode");
        int count=r.H();if(count>MaxRows)throw new ProtocolException("skill buy item count");
        var indices=new int[count];var seen=new HashSet<int>();
        for(int i=0;i<count;i++){
            uint index=r.D();
            if(index>=int.MaxValue||!seen.Add((int)index))throw new ProtocolException("skill buy item index");
            indices[i]=(int)index;
        }
        r.Finish();return indices;
    }
    public const string ExchangeAction="xchgspell",BuyAction="buyspell",EmptyPage="HorunEv3";
    public const ushort ExchangeNpcGfx=834;
    /// <summary>xchgspell 只對外型 834 的 NPC 送 C245；buyspell 只對外型 ≠ 834 的 NPC 送（SS2§1.1）。其他動作不受影響。</summary>
    public static bool ActionAllowed(string action,ushort npcGfx)=>action switch{
        ExchangeAction=>npcGfx==ExchangeNpcGfx,BuyAction=>npcGfx!=ExchangeNpcGfx,_=>true};
    public static byte[] RequestList(uint npc){
        if(npc==0)throw new ArgumentException("NPC 物件 id 不可為 0。");
        var p=new byte[5];p[0]=CRequest;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(1),npc);
        return LoginPackets.Pad(p);
    }
    public static byte[] Learn(IReadOnlyList<int> indices){
        if(indices.Count is <1 or >MaxRows||indices.Any(i=>i<0||i>=int.MaxValue)||indices.Distinct().Count()!=indices.Count)throw new ArgumentException("魔法選擇不正確。");
        var p=new byte[3+4*indices.Count];p[0]=CLearn;BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(1),(ushort)indices.Count);
        for(int i=0;i<indices.Count;i++)BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(3+4*i),indices[i]);
        return LoginPackets.Pad(p);
    }
}

public sealed partial class WorldSession {
    SpellExchangeOffer? spellExchangeOffer;uint? spellExchangeRequestNpc;int[] spellExchangePending=[];
    string spellExchangePhase="closed",spellExchangeNotice="";long spellExchangeRevision;
    public SpellExchangeOffer? SpellExchangeOffer=>spellExchangeOffer;
    public long SpellExchangeRevision=>spellExchangeRevision;
    public string SpellExchangePhase=>spellExchangePhase;
    public string SpellExchangeNotice=>spellExchangeNotice;

    string SpellExchangeName(int skillId)=>SkillCatalog?.Get(skillId)?.Name is {Length:>0} name?name:MagicRules.Describe(skillId).Name;
    bool SpellLearned(int skillId)=>learnedMagic.Contains(skillId);
    public SpellExchangeRow[] SpellExchangeRows=>spellExchangeOffer is {} o
        ?o.Indices.Select(i=>new SpellExchangeRow(i,i+1,SpellExchangeName(i+1),SpellLearned(i+1))).ToArray():[];
    /// <summary>最近一次送出的選擇中，已由原服 S_AddSkill 回報學會的數量。</summary>
    public int SpellExchangeLearnedCount=>spellExchangePending.Count(SpellLearned);

    void SpellExchangeChange(string phase,string notice){spellExchangePhase=phase;spellExchangeNotice=notice;spellExchangeRevision++;}

    [WorldReceive(SpellExchangeProtocol.SList)]
    bool ReceiveSpellExchangeList(byte[] plain){
        var indices=SpellExchangeProtocol.DecodeList(plain);
        // 原服只在收到 C245 後送 S13；沒要求過也照收（清單本身就是原服發的授權），只是不知道是哪個 NPC。
        uint? npc=spellExchangeRequestNpc;spellExchangeRequestNpc=null;
        if(indices.Length==0){
            spellExchangeOffer=null;SpellExchangeChange("empty","原服目前沒有可以用物品學習的魔法（已學的不會列出）。");
            // 原版（SS2§1.2）：空清單改開 NPC 對話窗的 HorunEv3 頁。只在要清單的那個 NPC 對話仍有效時換頁。
            CheckInteraction();
            if(npc is uint n&&InteractionValid&&InteractionNpc==n&&InteractionKind=="dialog")ShowPage(SpellExchangeProtocol.EmptyPage);
            return true;
        }
        spellExchangeOffer=new(npc,indices,spellExchangeRevision+1);
        SpellExchangeChange("list",$"原服列出 {indices.Length} 項可用物品學習的魔法。勾選後送出一次；需要的材料由原服檢查與扣除，未自行授予技能。");
        return true;
    }

    internal void SpellExchangeRequested(uint npc){
        spellExchangeRequestNpc=npc;spellExchangeOffer=null;
        SpellExchangeChange("waiting","已要求可用物品學習的魔法清單，等待原服回包；不會自動重送。");
    }
    /// <summary>為目前清單組 C191；清單已換掉（revision 不同）回 null；選擇不合法丟 ArgumentException。</summary>
    internal byte[]? SpellExchangeRequest(long revision,IReadOnlyList<int> indices){
        if(spellExchangeOffer is not {} offer||offer.Revision!=revision)return null;
        if(indices.Count<1)throw new ArgumentException("請至少選擇一項魔法。");
        if(indices.Count>offer.Indices.Length)throw new ArgumentException("選擇數量超過清單。");
        if(indices.Distinct().Count()!=indices.Count)throw new ArgumentException("同一項魔法不可重複選擇。");
        foreach(int i in indices){
            if(!offer.Indices.Contains(i))throw new ArgumentException($"技能 {i+1} 不在原服清單內。");
            if(SpellLearned(i+1))throw new ArgumentException($"「{SpellExchangeName(i+1)}」已經學會。");
        }
        return SpellExchangeProtocol.Learn(indices);
    }
    internal void SpellExchangeSent(long revision,IReadOnlyList<int> indices){
        if(spellExchangeOffer is not {} offer||offer.Revision!=revision)return;
        // 原服的授權票只用一次：送出後清單作廢，要再學須重新與 NPC 對話取得清單。
        spellExchangeOffer=null;spellExchangePending=indices.Select(i=>i+1).ToArray();
        SpellExchangeChange("submitted",$"已送出 {indices.Count} 項學習請求；等待原服回報已學技能，材料不足或未開放時原服不會授予。不會自動重送。");
    }
    public bool CloseSpellExchange(long revision){
        if(revision!=spellExchangeRevision||spellExchangePhase=="closed")return false;
        spellExchangeOffer=null;spellExchangeRequestNpc=null;SpellExchangeChange("closed","已關閉以物品學魔法。");return true;
    }
    internal SpellExchangeView SpellExchangeSnapshot(int generation,bool canRequest,bool canSend)=>new(generation,WorldEpoch,Actor?.ObjectId??0,spellExchangeRevision,
        canRequest,canSend&&spellExchangeOffer!=null,spellExchangePhase,SpellExchangePendingNotice,spellExchangeOffer?.Npc,spellExchangeOffer?.Revision??0,SpellExchangeRows,spellExchangePending,SpellExchangeLearnedCount);
    string SpellExchangePendingNotice=>spellExchangePhase=="submitted"&&spellExchangePending.Length>0
        ?spellExchangeNotice+$"\n原服已學技能回報：本次選取已出現 {SpellExchangeLearnedCount}/{spellExchangePending.Length} 項。":spellExchangeNotice;

    [WorldReset]
    void ResetSpellExchange(){spellExchangeOffer=null;spellExchangeRequestNpc=null;spellExchangePending=[];spellExchangePhase="closed";spellExchangeNotice="";spellExchangeRevision++;}
    [WorldSnapshot("spellExchange")]
    object? SpellExchangeSnapshotModel()=>new{revision=spellExchangeRevision,phase=spellExchangePhase,notice=SpellExchangePendingNotice,
        offer=spellExchangeOffer is {} o?new{npc=o.Npc,revision=o.Revision,rows=SpellExchangeRows.Select(r=>new{index=r.Index,skillId=r.SkillId,name=r.Name,learned=r.Learned}).ToArray()}:null,
        pending=spellExchangePending,learned=SpellExchangeLearnedCount};
}

public sealed partial class LoginConnection {
    int spellExchangeSendingGeneration=-1;
    // 原服 C245／C191 都會擋鬼魂、死亡、傳送中與個人商店；這裡先在本機擋，避免送出原服必然丟棄的封包。
    bool SpellExchangeAccess=>State==LoginState.InWorld&&World.Ready&&World.Actor is {} a&&World.Status is {Hp:>0} s&&a.ObjectId==s.ObjectId
        &&!World.OwnDead&&!World.IsGhost&&!World.TeleportPending&&!World.ActionLocked&&!World.TradeBlocksActions;
    // C245 另需「目前有對話窗的 NPC」（原服 UiOperationContext.hasDialog）與距離 ≤ 5（InteractionValid 已含 NearbyTarget）。
    bool SpellExchangeDialogOpen=>World.InteractionValid&&World.InteractionKind=="dialog"&&World.InteractionNpc is uint;
    bool SpellExchangeIdle=>spellExchangeSendingGeneration!=generation&&!interactionSending&&!MovementBusy;
    public SpellExchangeView SpellExchangeSnapshot()=>CaptureWorld(()=>World.SpellExchangeSnapshot(generation,
        SpellExchangeAccess&&SpellExchangeIdle&&SpellExchangeDialogOpen,SpellExchangeAccess&&SpellExchangeIdle));
    public bool CloseSpellExchange(SpellExchangeView v)=>CaptureWorld(()=>v.Generation==generation&&v.WorldEpoch==World.WorldEpoch&&World.CloseSpellExchange(v.Revision));

    /// <summary>對目前對話中的 NPC 送 C245 要求清單。產品入口：NPC 頁動作 xchgspell（NpcInteraction.cs，外型 834 才送）；
    /// 學魔法窗（TRADE_WINDOW 模式 7）在收到非空清單時由 SpellTradeSource 顯示；openWindow 已無作用（R3-F 拿掉暫行視窗），保留給既有呼叫端。</summary>
    public async Task<bool> RequestSpellExchangeList(bool openWindow=true){
        _=openWindow;
        if(!SpellExchangeIdle||sendMovementGate==null)return false;
        uint npc=0;int epoch=0,targetRevision=0;
        if(!CaptureWorld(()=>{if(!SpellExchangeAccess||!SpellExchangeDialogOpen)return false;npc=World.InteractionNpc!.Value;epoch=World.WorldEpoch;targetRevision=World.TargetRevision;return true;}))return false;
        bool Valid()=>CaptureWorld(()=>SpellExchangeAccess&&SpellExchangeDialogOpen&&World.InteractionNpc==npc&&World.WorldEpoch==epoch&&World.TargetRevision==targetRevision);
        return await SendSpellExchange(SpellExchangeProtocol.RequestList(npc),Valid,()=>World.SpellExchangeRequested(npc));
    }
    /// <summary>對 v 的清單送 C191（勾選的 skillIndex）。v 必須是目前的快照；選擇不合法或清單已作廢不送。</summary>
    public async Task<bool> LearnSpellExchange(SpellExchangeView v,IReadOnlyList<int> indices){
        if(v.OfferRevision==0||!SpellExchangeIdle||sendMovementGate==null)return false;
        int[] chosen=indices.ToArray();
        bool Valid()=>CaptureWorld(()=>v.Generation==generation&&v.WorldEpoch==World.WorldEpoch&&v.Actor==World.Actor?.ObjectId&&SpellExchangeAccess&&World.SpellExchangeOffer?.Revision==v.OfferRevision);
        if(!Valid())return false;
        byte[]? packet;
        try{packet=CaptureWorld(()=>World.SpellExchangeRequest(v.OfferRevision,chosen));}
        catch(ArgumentException){return false;}
        if(packet==null)return false;
        return await SendSpellExchange(packet,Valid,()=>World.SpellExchangeSent(v.OfferRevision,chosen));
    }
    async Task<bool> SendSpellExchange(byte[] packet,Func<bool> valid,Action admit){
        var sender=sendMovementGate!;int mine=generation;spellExchangeSendingGeneration=mine;
        try{return await sender(packet,valid,()=>{CaptureWorld(()=>{admit();return true;});Changed?.Invoke();})&&mine==generation;}
        catch(Exception){if(mine==generation){Cancel();Set(LoginState.Faulted,"以物品學魔法傳送失敗，已斷線；不會重送。");}return false;}
        finally{if(spellExchangeSendingGeneration==mine)spellExchangeSendingGeneration=-1;}
    }
}
