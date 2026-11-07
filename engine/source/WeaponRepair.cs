using System.Buffers.Binary;
using System.Globalization;
namespace P06;

public sealed record WeaponRepairRow(uint ObjectId, byte Damage) {
    public uint Cost => Damage * 200u;
}
public static class WeaponRepairPackets {
    public static byte[] List() => LoginPackets.Pad([118]);
    public static byte[] CheckPk() => LoginPackets.Pad([51]);
    public static byte[] Repair(uint item, uint npc) {
        if (item == 0 || npc == 0) throw new ArgumentException("repair identity");
        return LoginPackets.Pad([20, ..BitConverter.GetBytes(item), ..BitConverter.GetBytes(npc)]);
    }
    // D200 is the price/discriminator; D70 belongs to S_PetList.
    public static bool IsRepairList(byte[] plain) => plain.Length >= 5 && plain[0] == 83 && BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(1)) == 200;
    public static WeaponRepairRow[] ReadList(LoginReader r) {
        if (r.D() != 200) throw new ProtocolException("repair discriminator");
        int count = r.H();
        if (count > 2048 || count * 5 > r.Remaining) throw new ProtocolException("repair count");
        var rows = new WeaponRepairRow[count];var ids = new HashSet<uint>();
        for (int i = 0; i < count; i++) {
            uint id = r.D();byte damage = r.C();
            if (id == 0 || !ids.Add(id)) throw new ProtocolException("repair identity");
            rows[i] = new(id, damage);
        }
        r.Finish();return rows;
    }
    public static string? PkText(ServerMessageReport? reply) => reply is { Id: 562, Arguments.Length: 1 } &&
        uint.TryParse(reply.Arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint count)
        ? $"你的 PK 次數為 {count} 次。" : null;
}

public sealed partial class WorldSession {
    WeaponRepairRow[] repairRows = [];
    string repairPhase = "closed", repairNotice = "選取附近 NPC 後查詢損壞武器。";
    int repairEpoch = -1, repairTargetRevision, repairInventoryRevision, repairRevision;
    uint? repairNpc;
    long repairStarted;
    ServerMessageReport? repairReply;
    bool repairObserving;
    public WeaponRepairRow[] WeaponRepairRows { get { CheckWeaponRepair();return repairRows.ToArray(); } }
    public string WeaponRepairPhase { get { CheckWeaponRepair();return repairPhase; } }
    public string WeaponRepairNotice { get { CheckWeaponRepair();return repairNotice; } }
    public int WeaponRepairRevision { get { CheckWeaponRepair();return repairRevision; } }
    public bool NearbyRepairTarget => NearbyTarget && Actor is {} a && Target is {} t && !t.ShopActive &&
        Math.Abs(a.X-t.X) <= 3 && Math.Abs(a.Y-t.Y) <= 3;
    public void CloseWeaponRepair() { repairRows=[];repairPhase="closed";repairNotice="修理清單已關閉；請重新查詢。";repairRevision++; }
    void CheckWeaponRepair() {
        if (repairPhase == "closed") return;
        if (repairEpoch != WorldEpoch || repairTargetRevision != TargetRevision || repairNpc != TargetId || !NearbyRepairTarget || TradeBlocksActions || ActionLocked) {
            CloseWeaponRepair();return;
        }
        if (repairPhase is "querying" or "ready" or "submitted" && InteractionClock()-repairStarted >= 30000) {
            repairRows=[];repairPhase="review";repairRevision++;repairNotice="修理清單已逾時；結果尚未確認，請核對背包後重新查詢。";return;
        }
        if (repairPhase == "submitted" && !ReferenceEquals(repairReply, LastServerReply) && LastServerReply is { Id: 464 or 189 } reply) {
            repairRows=[];repairPhase="review";repairRevision++;
            repairNotice=reply.Id==189 ? "原服回報金幣不足；請重新查詢。" : "原服回報修理結果；請重新查詢損壞清單確認。";
        }
        if (repairPhase == "ready" && repairInventoryRevision != InventoryContentRevision) {
            repairRows=[];repairPhase="review";repairRevision++;repairNotice="背包已改變；請重新查詢修理清單。";
        }
    }
    internal void BeginWeaponRepair() {
        // This existing event runs after every packet, including S71. Observe each
        // message before a later packet replaces LastServerReply; no extra S71 dispatch.
        if(!repairObserving){CombatSoundStateChanged+=CheckWeaponRepair;repairObserving=true;}
        repairEpoch=WorldEpoch;repairTargetRevision=TargetRevision;repairNpc=TargetId;
        repairStarted=InteractionClock();repairRows=[];repairPhase="querying";repairRevision++;
        repairNotice="已要求損壞武器清單，等待原服回覆。";
    }
    void ReceiveWeaponRepair(LoginReader r) {
        var rows=WeaponRepairPackets.ReadList(r);CheckWeaponRepair();
        if (repairPhase != "querying") return;
        repairRows=rows;repairInventoryRevision=InventoryContentRevision;repairPhase="ready";repairRevision++;
        repairNotice=rows.Length==0 ? "原服沒有列出損壞武器。" : "每點損壞修理費 200 金幣；選取一件提交，扣款與結果以原服回報為準。";
    }
    public bool CanRepairWeapon(uint item, int revision) {
        CheckWeaponRepair();return revision==repairRevision && repairPhase=="ready" &&
            repairRows.Any(r=>r.ObjectId==item && r.Damage>0) && inventory.TryGetValue(item,out var held) && held.Count>0 && held.UseType==1;
    }
    internal void SubmitWeaponRepair() {
        repairReply=LastServerReply;repairPhase="submitted";repairStarted=InteractionClock();repairRevision++;
        repairNotice="修理請求已送出一次，尚未確認完成；不會自動重送。";
    }
}

public sealed partial class LoginConnection {
    int repairSendingGeneration=-1, pkSendingGeneration=-1, pkEpoch=-1;
    long pkStarted;
    ServerMessageReport? pkPrevious;
    string pkNotice="尚未查詢 PK 次數。";
    bool pkWaiting, pkObserving;
    public bool CanQueryWeaponRepair => RepairAccess && World.NearbyRepairTarget && World.InventoryLoaded &&
        World.WeaponRepairPhase is not ("querying" or "submitted");
    bool RepairAccess => State==LoginState.InWorld && World.Ready && !World.OwnDead && World.Status?.Hp>0 &&
        !World.ActionLocked && !World.TradeBlocksActions && !MovementBusy && !interactionSending &&
        repairSendingGeneration!=generation;
    public async Task<bool> SendWeaponRepair(bool query, uint item=0, int revision=-1) {
        if (!RepairAccess || sendMovementGate==null || !World.NearbyRepairTarget ||
            (query ? !CanQueryWeaponRepair : !World.CanRepairWeapon(item,revision))) return false;
        int session=generation, epoch=World.WorldEpoch, target=World.TargetRevision, requestRevision=World.WeaponRepairRevision;
        uint npc=World.TargetId!.Value;var sender=sendMovementGate;
        byte[] bytes=query ? WeaponRepairPackets.List() : WeaponRepairPackets.Repair(item,npc);
        repairSendingGeneration=session;
        bool Valid() => session==generation && epoch==World.WorldEpoch && target==World.TargetRevision && requestRevision==World.WeaponRepairRevision && State==LoginState.InWorld &&
            World.NearbyRepairTarget && !World.ActionLocked && !World.TradeBlocksActions && !MovementBusy && !interactionSending &&
            (query ? World.WeaponRepairPhase is not ("querying" or "submitted") : World.CanRepairWeapon(item,revision));
        try { return await sender(bytes,Valid,()=>{if(query)World.BeginWeaponRepair();else World.SubmitWeaponRepair();}) && session==generation; }
        catch(Exception) { if(session==generation){Cancel();Set(LoginState.Faulted,"修理傳送失敗，已斷線；不會重送。");}return false; }
        finally { if(repairSendingGeneration==session)repairSendingGeneration=-1; }
    }
    public string CheckPkNotice {
        get {
            if(pkEpoch!=World.WorldEpoch || State!=LoginState.InWorld) {pkWaiting=false;return "尚未查詢 PK 次數。";}
            if(pkWaiting && World.InteractionClock()-pkStarted>=30000) {pkNotice="PK 查詢逾時，請重新查詢。";pkWaiting=false;}
            if(pkWaiting && !ReferenceEquals(pkPrevious,World.LastServerReply) && WeaponRepairPackets.PkText(World.LastServerReply) is {} text) {pkNotice=text;pkWaiting=false;}
            return pkNotice;
        }
    }
    public async Task<bool> CheckPk() {
        _=CheckPkNotice;
        if(State!=LoginState.InWorld || !World.Ready || World.ActionLocked || pkWaiting || pkSendingGeneration==generation || sendMovementGate==null) return false;
        if(!pkObserving){World.CombatSoundStateChanged+=()=>{_=CheckPkNotice;};pkObserving=true;}
        int session=generation,epoch=World.WorldEpoch;var sender=sendMovementGate;pkSendingGeneration=session;
        try { return await sender(WeaponRepairPackets.CheckPk(),()=>session==generation && epoch==World.WorldEpoch && State==LoginState.InWorld && World.Ready && !World.ActionLocked,
            ()=>{pkPrevious=World.LastServerReply;pkEpoch=epoch;pkStarted=World.InteractionClock();pkWaiting=true;pkNotice="已要求 PK 次數，等待原服訊息。";}) && session==generation; }
        catch(Exception) {if(session==generation){Cancel();Set(LoginState.Faulted,"PK 查詢傳送失敗，已斷線；不會重送。");}return false;}
        finally {if(pkSendingGeneration==session)pkSendingGeneration=-1;}
    }
}
