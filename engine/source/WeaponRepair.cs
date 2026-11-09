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

    // M7-S（T219／#526）：修理改走 NPC 對話與 TRADE_WINDOW 模式 6（WIN§2 T134）。
    // 推測（字串證據）：TW16 HyperAct 名稱表 FO 0xDA78AC 的 "fix"（與已證實的 "checkpk"、"exitghost" 同一區），
    // 由客戶端自己處理並送無欄位的 C118，不送 C125。
    public const string FixAction = "fix";
    /// <summary>TRADE_WINDOW 修理模式（sub_A59780／FO 0x658B80，WIN§2 T134，已驗證）。</summary>
    public const int TradeWindowMode = 6;
    /// <summary>
    /// 清單為空時的 NPC 對話頁（WIN§2 T134：touma3／gayle5，已驗證兩個頁名）。原版依哪個條件選頁沒有追；
    /// 這裡以「這位 NPC 上一個對話頁名的前綴」決定（推測），對不上就不開頁，只留文字通知。
    /// </summary>
    public static string? EmptyListPage(string? dialogHtml) =>
        dialogHtml is null ? null :
        dialogHtml.StartsWith("touma", StringComparison.OrdinalIgnoreCase) ? "touma3" :
        dialogHtml.StartsWith("gayle", StringComparison.OrdinalIgnoreCase) ? "gayle5" : null;
    /// <summary>舊介面（原生 UI 關閉時）在 NPC 對話頁列出可修武器用的選項值；引擎內部用，不送給伺服器。</summary>
    public const string ItemOptionPrefix = "fix-item:";
    public static string ItemOption(uint item, int revision) => $"{ItemOptionPrefix}{item}:{revision}";
    public static bool TryItemOption(string value, out uint item, out int revision) {
        item = 0; revision = -1;
        if (!value.StartsWith(ItemOptionPrefix, StringComparison.Ordinal)) return false;
        var parts = value[ItemOptionPrefix.Length..].Split(':');
        return parts.Length == 2 && uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out item) && item != 0 &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out revision);
    }
}

/// <summary>TRADE_WINDOW 一列（原生面板只顯示，不解析封包）。</summary>
public sealed record TradeWindowRow(uint Id, string Name, string Detail, bool Enabled);
/// <summary>TRADE_WINDOW 目前內容：Mode 是原版 TradeWindow 模式（目前只有修理 6）；Revision 變了代表是新清單。</summary>
public sealed record TradeWindowView(int Mode, int Revision, TradeWindowRow[] Rows, string Notice);

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
    public void CloseWeaponRepair() { EndWeaponRepairBatch();repairRows=[];repairPhase="closed";repairNotice="修理清單已關閉；請重新查詢。";repairRevision++; }
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
        EndWeaponRepairBatch();
        repairEpoch=WorldEpoch;repairTargetRevision=TargetRevision;repairNpc=TargetId;
        repairStarted=InteractionClock();repairRows=[];repairPhase="querying";repairRevision++;
        repairNotice="已要求損壞武器清單，等待原服回覆。";
    }
    void ReceiveWeaponRepair(LoginReader r) {
        var rows=WeaponRepairPackets.ReadList(r);CheckWeaponRepair();
        if (repairPhase != "querying") return;
        repairRows=rows;repairInventoryRevision=InventoryContentRevision;repairPhase="ready";repairRevision++;
        repairNotice=rows.Length==0 ? "原服沒有列出損壞武器。" : "每點損壞修理費 200 金幣；選取一件提交，扣款與結果以原服回報為準。";
        PresentWeaponRepair();
    }

    // ---- M7-S（T219／#526）：NPC 對話與 TRADE_WINDOW 模式 6 ----
    string? repairDialogHtml;uint? repairDialogNpc;
    /// <summary>空清單時開過的對話頁（touma3／gayle5）；沒開頁為 null。給測試與診斷用。</summary>
    public string? WeaponRepairEmptyPage {get;private set;}
    /// <summary>true＝原生 TRADE_WINDOW 已接上，清單只在原生窗顯示；false＝舊介面，清單改列在 NPC 對話頁的選項。</summary>
    public bool WeaponRepairNativeWindow {get;set;}
    /// <summary>對話頁的 fix 動作（送 C118）；由 LoginConnection.WireWeaponRepairDialog 接上。</summary>
    internal event Action? WeaponRepairRequested;
    /// <summary>舊介面對話頁上選了一件武器（送 C20）。</summary>
    internal event Action<uint,int>? WeaponRepairItemChosen;

    // S39 只觀察：記下每位 NPC 最後一個對話頁名，給空清單選 touma3／gayle5。C(39) D(npc) S(html) …
    [WorldReceive(39,Observe=true)]
    void ObserveRepairDialog(byte[] plain) {
        if (plain.Length < 6) return;
        uint npc=BinaryPrimitives.ReadUInt32LittleEndian(plain.AsSpan(1));
        int end=Array.IndexOf(plain,(byte)0,5);if(npc==0||end<=5)return;
        repairDialogNpc=npc;repairDialogHtml=LoginPackets.TextEncoding.GetString(plain,5,end-5);
    }
    [WorldReset]
    void ResetWeaponRepairDialog() {repairDialogHtml=null;repairDialogNpc=null;WeaponRepairEmptyPage=null;}

    bool RepairDialogOpen => InteractionValid && InteractionNpc==repairNpc && InteractionKind=="dialog";
    void PresentWeaponRepair() {
        WeaponRepairEmptyPage=null;
        if (!RepairDialogOpen) return;
        if (repairRows.Length==0) {
            // WIN§2 T134：清單為空時改開 NPC 對話頁（HYPER_TEXT_WINDOW；引擎目前是 npcPanel）。
            if (WeaponRepairPackets.EmptyListPage(repairDialogNpc==repairNpc?repairDialogHtml:null) is {} page) {ShowPage(page);WeaponRepairEmptyPage=page;}
            return;
        }
        if (WeaponRepairNativeWindow) return;
        // 舊介面過渡：原生 UI 關閉時沒有 TRADE_WINDOW，把可修武器列成同一個 NPC 對話頁的選項（不是原版畫面）。
        var options=repairRows.Select((row,i)=>new NpcOption(i+1,$"{RepairItemName(row.ObjectId)}　損壞 {row.Damage}　{row.Cost:N0} 金幣","action",
            WeaponRepairPackets.ItemOption(row.ObjectId,repairRevision),CanRepairWeapon(row.ObjectId,repairRevision))).ToArray();
        ClearNpcService();InteractionRevision++;ShopRows=[];
        InteractionPage=new NpcPage("選擇要修理的武器。",options,"原版是 TRADE_WINDOW 修理模式 6；原生 UI 關閉時改列在對話頁（過渡）。"+repairNotice);
        InteractionNotice=InteractionPage.Limitation;
    }
    string RepairItemName(uint id) => inventory.TryGetValue(id,out var item) ? item.Name : "未持有物件 "+id;

    /// <summary>NpcServices.OpenDeposit 轉來的本地對話動作：fix → C118；fix-item → C20。true＝已在本機處理，不送 C125。</summary>
    internal bool HandleRepairOption(string value) {
        if (string.Equals(value,WeaponRepairPackets.FixAction,StringComparison.OrdinalIgnoreCase)) {
            if (WeaponRepairRequested==null) return false;
            WeaponRepairRequested();return true;
        }
        if (!WeaponRepairPackets.TryItemOption(value,out uint item,out int revision)) return false;
        WeaponRepairItemChosen?.Invoke(item,revision);return true;
    }

    /// <summary>TRADE_WINDOW 模式 6 的內容；沒有可修清單時為 null（窗不開）。名稱經 names 轉換（例如 $ 名稱表）。</summary>
    public TradeWindowView? WeaponRepairWindow(Func<string,string>? names=null) {
        CheckWeaponRepair();
        if (repairPhase!="ready" || repairRows.Length==0) return null;
        var rows=repairRows.Select(row=>new TradeWindowRow(row.ObjectId,(names??(s=>s))(RepairItemName(row.ObjectId)),$"{row.Cost:N0}",CanRepairWeapon(row.ObjectId,repairRevision))).ToArray();
        return new(WeaponRepairPackets.TradeWindowMode,repairRevision,rows,repairNotice);
    }
    public bool CanRepairWeapon(uint item, int revision) {
        CheckWeaponRepair();return revision==repairRevision && repairPhase=="ready" &&
            repairRows.Any(r=>r.ObjectId==item && r.Damage>0) && inventory.TryGetValue(item,out var held) && held.Count>0 && held.UseType==1;
    }
    internal void SubmitWeaponRepair() => SubmitWeaponRepair([]);
    // R4-C（T303／#715）：TRADE_WINDOW 模式 6 多選時，原版把每一列各送一包 C20（TWP§1.6，sub_A2BD10 的迴圈）。
    // 第一包照舊驗證「清單版本＋每件都可修」並進入 submitted；後面幾包只認同一批（批號），批號在清單關閉或重新查詢時失效，
    // 所以原服在中途回 464／189（狀態轉 review）時，剩下的包仍照原版一次送完；換目標、交易、離開世界則停止。
    int repairBatch;uint[] repairBatchItems=[];int repairBatchSent;
    /// <summary>目前這一批修理（多選）已送出的包數與總數；沒有批次時為 (0, 0)。給 UI 提示與測試用。</summary>
    public (int Sent,int Total) WeaponRepairBatchProgress => (repairBatchItems.Length==0?0:repairBatchSent, repairBatchItems.Length);
    internal int SubmitWeaponRepair(IReadOnlyList<uint> batch) {
        repairReply=LastServerReply;repairPhase="submitted";repairStarted=InteractionClock();repairRevision++;
        repairBatch++;repairBatchItems=batch.Count>1?batch.ToArray():[];repairBatchSent=batch.Count>1?1:0;
        repairNotice=batch.Count>1 ? $"修理請求逐件送出中（每件一包 C20，共 {batch.Count} 件）；尚未確認完成，不會自動重送。" : "修理請求已送出一次，尚未確認完成；不會自動重送。";
        return repairBatch;
    }
    /// <summary>同一批的後續包：批號沒變（清單沒關、沒重新查詢）、是這批的物品、背包仍持有。</summary>
    internal bool WeaponRepairBatchLive(int batch, uint item) {
        CheckWeaponRepair();
        return batch==repairBatch && repairBatchItems.Contains(item) && repairPhase is "submitted" or "review" &&
            inventory.TryGetValue(item,out var held) && held.Count>0;
    }
    internal void WeaponRepairBatchSent(int batch) {
        if(batch!=repairBatch||repairBatchItems.Length==0)return;
        repairBatchSent=Math.Min(repairBatchItems.Length,repairBatchSent+1);
        if(repairPhase=="submitted")repairNotice=$"修理請求已逐件送出 {repairBatchSent}／{repairBatchItems.Length} 包（每件一包 C20）；尚未確認完成，不會自動重送。";
    }
    void EndWeaponRepairBatch() {repairBatch++;repairBatchItems=[];repairBatchSent=0;}
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
    /// <summary>
    /// R4-C（T303／#715）：TRADE_WINDOW 模式 6 多選——每一列各送一包 C20（TWP§1.6）。只有一列時同 SendWeaponRepair。
    /// 第一包驗證整批（同一清單版本、每件都可修）；之後每包只驗連線／世界／目標／NPC 距離與批號（WorldSession.WeaponRepairBatchLive）。
    /// 回傳 true＝每一包都送出了；中途停止回 false（已送出的包不撤回，結果以原服回報為準）。
    /// </summary>
    public async Task<bool> SendWeaponRepairBatch(IReadOnlyList<uint> items, int revision) {
        var list=items.Where(i=>i!=0).Distinct().ToArray();
        if (list.Length==0) return false;
        if (list.Length==1) return await SendWeaponRepair(false,list[0],revision);
        if (!RepairAccess || sendMovementGate==null || !World.NearbyRepairTarget || list.Any(i=>!World.CanRepairWeapon(i,revision))) return false;
        int session=generation, epoch=World.WorldEpoch, target=World.TargetRevision, requestRevision=World.WeaponRepairRevision, batch=-1;
        uint npc=World.TargetId!.Value;var sender=sendMovementGate;
        repairSendingGeneration=session;
        bool Common() => session==generation && epoch==World.WorldEpoch && target==World.TargetRevision && State==LoginState.InWorld &&
            World.NearbyRepairTarget && World.TargetId==npc && !World.ActionLocked && !World.TradeBlocksActions && !MovementBusy && !interactionSending;
        try {
            for (int k=0; k<list.Length; k++) {
                uint item=list[k];bool first=k==0;
                bool Valid() => Common() && (first ? requestRevision==World.WeaponRepairRevision && list.All(i=>World.CanRepairWeapon(i,revision)) : World.WeaponRepairBatchLive(batch,item));
                bool sent=await sender(WeaponRepairPackets.Repair(item,npc),Valid,()=>{if(first)batch=World.SubmitWeaponRepair(list);else World.WeaponRepairBatchSent(batch);});
                if (!sent || session!=generation) return false;
            }
            return true;
        }
        catch(Exception) { if(session==generation){Cancel();Set(LoginState.Faulted,"修理傳送失敗，已斷線；不會重送。");}return false; }
        finally { if(repairSendingGeneration==session)repairSendingGeneration=-1; }
    }
    bool repairDialogWired;
    /// <summary>
    /// 把 NPC 對話頁的修理動作接到既有的送出路徑（M7-S）：fix → SendWeaponRepair(true)（C118），
    /// 舊介面的武器選項 → SendWeaponRepair(false,…)（C20）。重複呼叫只接一次。
    /// </summary>
    public void WireWeaponRepairDialog() {
        if (repairDialogWired) return;
        repairDialogWired=true;
        World.WeaponRepairRequested+=()=>_=SendWeaponRepair(true);
        World.WeaponRepairItemChosen+=(item,revision)=>_=SendWeaponRepair(false,item,revision);
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
