namespace P06;

// M7-B（T204／#510）：S36 Ability（能力旗標）與 S37 SPMR（SP／MR）的接收與快照。
// 依據：docs/reference/server-protocol.md〈S_Ability〉〈S_SPMR〉；gap-matrix 第 1 節 op 122（TW16 S_CHANGE_ABILITY → 3.81 S36）。
// 只做協定與資料；狀態窗等畫面由原生 UI 模組處理。旗標存原始 type 值；type 1／2／5 的名稱見下方 AbilityRules（T265）。
//
// 封包（BasePacket 補 0 到 8 的倍數）：
//   S36：C(36) C(type) C(equipped?1:0)                       → 3 bytes，補成 8
//   S37 buildPacket／isLogin：C(37) D(sp) D(mr)（ConfigClient.isROClient＝true，本產品 CLIENT_LOGIN_TYPE=2）→ 9 bytes，補成 16
//   S37 無參數建構子：C(37) C(50) C(100)（不分 ROClient）    → 3 bytes，補成 8
public sealed record AbilityFlag(int Type,bool Equipped,int Updates);
public sealed record SpMrStatus(int Sp,int Mr,string Form,int Updates);

// T265（#626，R2-L）：
// - S37 SP／MR 由 NativeUi/State/LeftStatusState.From 讀 SpMr 接到左狀態框（#598）；本檔不另存。
// - S36 原版：TW16 S_CHANGE_ABILITY（op 122，sub_932300，VA 0x932300／FO 0x531700）以 `cc` 讀 type、值，
//   type（有號 byte）< 6 才呼叫 sub_9E3260(值)，寫進 0x14EBFA0＋type×12 的 6 格表；**不加狀態圖示，也不進左狀態框**。
//   type 的意義取自 3.81 呼叫端 L1EquipmentSlot：1＝傳送控制戒指（20288）、2＝變形控制戒指（20281）、5＝道具 20284（名稱待驗證）。
public static class AbilityRules {
    /// <summary>原版表的格數；type ≥ 6 原版忽略。</summary>
    public const int OriginalSlots=6;
    public static bool KeptByOriginal(int type)=>type is >=0 and <OriginalSlots;
    public static string? Name(int type)=>type switch{1=>"傳送控制戒指",2=>"變形控制戒指",5=>"道具 20284",_=>null};
}

/// <summary>T276（#667）：S36 type 1 開的「選傳送目的地」請求。Skill＝施放的技能（5 指定傳送／69 集體傳送）。</summary>
public sealed record TeleportDestinationRequest(long Revision,int Skill);

/// <summary>T276（#667）：控制戒指開窗的常數（followups-3.md 第 3 節，皆為 BIN 已驗證值）。</summary>
public static class RingWindowRules {
    public const int TeleportRing=1,SummonRing=5,SummonSkill=51;
    public const ushort TeleportBlockedMessage=276,SummonLevelMessage=743;
    /// <summary>sub_BE0F10 只接受技能索引 4（指定傳送）、68（集體傳送）＝技能 5／69。</summary>
    public static bool TeleportSkill(int skill)=>skill is 5 or 69;
    /// <summary>
    /// dword_138BE60（目前地圖）是 5、6、83、84 或 ≥ 0x4000 → 276「在此無法使用傳送。」。
    /// R7-C（#805）：依 #736（sub_E08A20），276 只在直接送 C6 的路徑（0xE08BA9）；戒指與妖精的開窗路徑（0xE08B22 起）不查。
    /// 直接施放路徑不在本檔，這裡只保留規則常數。
    /// </summary>
    public static bool TeleportBlocked(int map)=>map is 5 or 6 or 83 or 84||map>=0x4000;
}

public sealed partial class WorldSession {
    readonly object abilityLock=new();
    readonly SortedDictionary<int,AbilityFlag> abilityFlags=[];
    int abilityUpdates;
    SpMrStatus? spMr;

    /// <summary>S36 收到過的能力旗標（以 type 排序）。</summary>
    public AbilityFlag[] AbilityFlags {get{lock(abilityLock)return [..abilityFlags.Values];}}
    /// <summary>type 目前是否為 equipped＝1；沒收過回 null。</summary>
    public bool? AbilityEquipped(int type){lock(abilityLock)return abilityFlags.TryGetValue(type,out var f)?f.Equipped:null;}
    /// <summary>最近一次 S37 的 SP／MR；沒收過回 null。</summary>
    public SpMrStatus? SpMr {get{lock(abilityLock)return spMr;}}

    [WorldReceive(36)]
    bool ReceiveAbility(byte[] plain){
        var r=new LoginReader(plain);r.C();
        int type=r.C(),equipped=r.C();r.Finish();
        if(equipped>1)throw new ProtocolException($"invalid ability equipped flag {equipped}");
        lock(abilityLock){
            abilityUpdates++;
            int prior=abilityFlags.TryGetValue(type,out var old)?old.Updates:0;
            abilityFlags[type]=new(type,equipped==1,prior+1);
        }
        return true;
    }

    [WorldReceive(37)]
    bool ReceiveSpMr(byte[] plain){
        var r=new LoginReader(plain);r.C();
        int sp,mr;string form;
        // 長度 16＝ROClient 的 D／D；長度 8＝無參數建構子的 C／C。其他長度由 Finish 擋下。
        if(plain.Length>8){sp=unchecked((int)r.D());mr=unchecked((int)r.D());form="dword";}
        else {sp=r.C();mr=r.C();form="byte";}
        r.Finish();
        lock(abilityLock)spMr=new(sp,mr,form,(spMr?.Updates??0)+1);
        return true;
    }

    // T276（#667）：S36 type 1／5 的開窗事件。依據 docs/reference/followups-3.md 第 3 節。
    // 施放（不帶參數的 CastSelectedMagic）先問這裡：回 true＝已開窗或已顯示訊息，**不送 C6**。
    //   type 1（sub_E08A20）：技能 5／69＋戴傳送控制戒指 → 開書籤窗的「選目的地」模式。
    //     R7-C（#805）：開窗前**不查**限制地圖、不顯示 276（#736：0xE08B22 起的開窗路徑不查，276 只在直接送 C6 的 0xE08BA9）。
    //     選定後仍以 3.81 的 HD（地圖、書籤 id）送 C6（RingWindows.cs），不照抄 TW16 的 hhh；限制地圖由伺服器 TELEPORT 判斷。
    //   type 5（sub_DF8A50）：技能 51＋戴召喚控制戒指 → 開既有的本機召喚窗（OpenLocalSummon，窗已開就不重開）。
    // 妖精 Lv ≥ 50＋byte_13A931C 的分支在 RingWindows.cs（R5-D／#736）；選項的 UI 等 Q7。
    // 沒做（待驗證）：技能索引 130 的排除（5／69 的索引是 4／68，不會碰到）。
    TeleportDestinationRequest? teleportDestination;long teleportDestinationRevision;
    /// <summary>目前的「選傳送目的地」請求；沒開為 null。</summary>
    public TeleportDestinationRequest? TeleportDestination {get{lock(abilityLock)return teleportDestination;}}
    internal bool TryOpenRingWindow(){
        if(SelectedMagicId is not int id||!learnedMagic.Contains(id))return false;
        if(RingWindowRules.TeleportSkill(id)&&AbilityEquipped(RingWindowRules.TeleportRing)==true){
            lock(abilityLock){if(teleportDestination==null)teleportDestination=new(++teleportDestinationRevision,id);}
            MagicNotice="已開啟傳送目的地選擇（傳送控制戒指）；選定書籤後才送出。";
            return true;
        }
        if(id==RingWindowRules.SummonSkill&&AbilityEquipped(RingWindowRules.SummonRing)==true){
            if(SummonRequestState==null&&!OpenLocalSummon())return false;
            MagicNotice="已開啟召喚怪物選擇（召喚控制戒指）；選定後才送出。";
            return true;
        }
        return false;
    }
    /// <summary>
    /// 選定書籤 → 技能目標（HD：書籤地圖、書籤 id）；請求過期、書籤不存在或已送出刪除時 null。會把選取技能設回請求的技能。
    /// R10-J（T394／#909）：bookmarkId 0＝隨機傳送鈕（原版名稱 487、座標 0、id 0 的假書籤）→ HD(地圖 0, 書籤 0)。
    /// 已驗證（3.81 原始碼）：C_UseSkill.java 對 5／69 的 H 讀了就丟；TELEPORT.java／MASS_TELEPORT.java 書籤查不到時 randomLocation(200)
    /// （目前地圖不可傳送時回 276，由伺服器判斷）。
    /// </summary>
    internal SkillCastTarget? TeleportDestinationTarget(long revision,uint bookmarkId){
        if(TeleportDestination is not {} q||q.Revision!=revision)return null;
        ushort map;
        if(bookmarkId==NativeUi.BookmarkWindowPanel.RandomTeleportId)map=0;
        else if(Bookmarks.TryGet(bookmarkId) is {} b)map=b.MapId;
        else return null;
        if(SelectedMagicId!=q.Skill&&!SelectMagic(q.Skill))return null;
        return new(SkillTargetKind.Bookmark,MapId:map,BookmarkId:bookmarkId);
    }
    /// <summary>取消（ESC／取消鈕）或送出後關閉；revision 不符時不動。</summary>
    public bool CloseTeleportDestination(long revision){lock(abilityLock){if(teleportDestination?.Revision!=revision)return false;teleportDestination=null;return true;}}
    /// <summary>召喚窗送出前的等級檢查（sub_C00890）；不通過時顯示 743、不送。</summary>
    internal bool RingSummonAllowed(string action){
        if(Status?.Level is byte level&&NativeUi.SummonCatalog.RingLevelAllows(level,action))return true;
        // 不動 summonRevision：請求的 revision 必須維持，窗不關、選擇保留（原版也只跳訊息）。
        summonNotice=MagicNotice=ServerMessageText.Decode(RingWindowRules.SummonLevelMessage,[]).Text;
        return false;
    }

    [WorldReset]
    void ResetAbilityStatus(){lock(abilityLock){abilityFlags.Clear();abilityUpdates=0;spMr=null;teleportDestination=null;}}

    [WorldSnapshot("ability")]
    object? AbilitySnapshot(){
        lock(abilityLock)return new{
            flags=abilityFlags.Values.Select(f=>new{type=f.Type,equipped=f.Equipped,updates=f.Updates,name=AbilityRules.Name(f.Type),keptByOriginal=AbilityRules.KeptByOriginal(f.Type)}).ToArray(),
            active=abilityFlags.Values.Where(f=>f.Equipped).Select(f=>f.Type).ToArray(),
            updates=abilityUpdates,
            spMr=spMr==null?null:new{sp=spMr.Sp,mr=spMr.Mr,form=spMr.Form,updates=spMr.Updates},
            teleportDestination=teleportDestination is {} q?new{revision=q.Revision,skill=q.Skill}:null,
        };
    }
}
