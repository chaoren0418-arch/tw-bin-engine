using P06.NativeUi;
namespace P06;

// T230（#548）：S250 狀態圖示類子碼（protocol-gaps.md 第 2.3 節 B2 組、第 8 節）。只做協定與資料：
// 有倒數時間的狀態併入 MagicBuffs（狀態列／原生 UI buff-icons 模組從那裡畫），
// 沒有時間的數值與圖示放進本檔的狀態與快照 "packetBoxStatus"，本檔不畫畫面。
// R4-F（T297／#709）：75／82／180 的 PacketBoxStatusIcons 由原生 BuffState（NativeUi/State/BuffState.cs）讀進 STATUS_EFFECT_ICONS，
// 原生 UI 關閉時由 WinForms 狀態列（StatusIcons.cs）畫同一個 effectlist2 圖示。
// 欄位寬度一律依 3.81 伺服器寫法（docs/reference/server-protocol.md），TW16 讀取寬度不同處見各子碼註解。
//
// 子碼  3.81 類別               3.81 欄位（不含 op、子碼）                 處理
// 53    S_PacketBoxCooking      C×6 能力 H(食物) C(類型) C(圖示) H(秒) C(0)    計時：-53000-類型
// 56    S_PacketBox DOLL        H(秒)                                      計時：-3056 魔法娃娃
// 59    S_PacketBoxWaterLife    H(0)                                       移除 effect 57＝技能 170 水之元氣（T265 已驗證）
// 60    S_PacketBoxThirdSpeed   C(秒>>2) C(8)                               計時：-3060 三段加速（4 秒單位，約略）
// 75    S_PacketBoxDk           C(階段 0–3)                                數值＋圖示 effect 135（龍騎士弱點曝光）
// 82    S_PacketBoxExp LEAVES   C(值) C(0) C(0) C(0)                        數值＋圖示 effect 175（殷海薩的祝福，百分比）
// 87    S_Karma                 D(友好度，有號)                              數值
// 114   S_PacketBoxProtection   D(種類 0–6) D(1 開／0 關)                     圖示集合
// 132   S_PacketBox EVASION     C(迴避)                                     數值
// 153   S_PacketBox MAP_LIMIT   D(秒)                                       計時：-3153 地圖限時
// 159   S_PacketBoxMapTimer     D(筆數) {D(順序) S(地圖名) D(分鐘)}          清單
// 160   S_WeaponRange           C(距離；-1 寫成 15)                           數值
// 180   S_PacketBox NONE_TIME   C(1 開／0 關) D(圖示) D(字串 id) H(0)          無時間圖示集合（圖示＝effect id）
//
// T265（#626，R2-L）原版對照：TW16 S_EVENT（op 108，sub_9357E0，VA 0x9357E0／FO 0x534BE0）的子碼跳表在 VA 0x93EC14
// （`jmp [eax*4+0x93EC14]`，子碼 0–0xCC）。以下四個 case 用 capstone 靜態反組譯查得（scratch 腳本，BIN SHA e12949cf…）。
// effect 編號是 Tile.pak effectlist2.xml 的 id（ui-layout.md 第 4 節）；sub_F05A90(秒, effect, 顯示模式, …) 加圖示、
// sub_F059F0(effect, …) 移除、sub_F05B10(秒, effect, …) 加圖示後再把 effect 的 startMsg 印到聊天。
//   59  case VA 0x93AB77：不讀欄位；`if(sub_EFE870(57)) sub_F059F0(57,0,1)` → 只移除 effect 57 WATER_LIFE_S（水之元氣）。
//   75  case VA 0x93B2E2：讀 C(階段)。>0 → 把 effect 135（CHAIN_SWORD_COUNT）的 icon 改成 0xC9B+3−階段（1→3229、2→3228、
//       3→3227），再 sub_F05A90(effect 135 的 duration, 135, 0, −1, 1)；≤0 → sub_F059F0(135,0,1)。
//   82  case VA 0x93B802：讀 D、D、D，組 EinhasadExpEvent（RTTI，事件 0x18）交給 EinhasadExManager；處理函式 sub_735F90
//       → sub_736AC0：值 >0 → sub_F05A90(值, effect, 1, −1, 1)，顯示模式 1＝百分比；effect 平常為 175
//       （STR_STATUS_EFFECT_BLESS_OF_EINHASAD）；值 >200 為 575（旗標開為 576）、管理器旗標 +0x10 開時為 518、
//       全域 0x138BE64＝0x1900 時為 574。≥540 的 id 不在表中，客戶端不建立圖示。值 ≤0 → 移除 175、518、574、575、576。
//       3.81 只寫 C(值) C(0)×3，所以 TW16 的第一個 D 正好等於值；後兩個 D 落在補位與包外，原版不用來決定圖示。
//       旗標 +0x10 由事件 0x15／0x16（id 0x125）切換，引擎沒有這個來源 → 一律當 0（effect 175）。
//   180 case VA 0x938857：解析 sub_C14ED0：C(開關≠0)、D → +8、D → +0xC。開 → sub_F05B10(−1, +8, 0, −1, 1)：
//       **第一個 D 是 effect id**（無期限，並印 effect 的 startMsg）；關 → sub_F059F0(+8,1,1)。封包的字串 id（+0xC）原版不用。
//       3.81 呼叫端（ItemVIPTable、TimedRateController、MapRecoveryIconState）傳的也是 effect id。
// R5-F（T311／#738）：180 開時把 effect 的 startMsg 印進聊天欄（sub_F05B10 → sub_7DC800，色 24）；82 的兩個變體旗標
// （基底地圖 0x1900 → 574；effect 293 顯示中 → 518／576）依 sub_736AC0 接上，見 PacketBoxStatusRules.LeavesEffectFor。
// R7-E（T344／#807）：
//   • 82 變體照原版累積：sub_736AC0 每次只 sub_F05A90 一個變體（新增或更新該 effect 的值），不移除其他變體；值 ≤ 0 才把
//     175／518／574／575／576 全部 sub_F059F0。所以先 175 再換 518，兩個圖示都在（各留最後一次的值）。
//   • 82 的按鈕附加處理（VA 0x736B59）：sub_F05A90 之後，玩家物件 [0x153CE60] ≠ 0 時以 sub_6B2C30(玩家 +0x10 座標) 與
//     [0x138BE60] 呼叫 sub_7362F0（EinhasadExManager +0x24 的 BoostEvent 清單：sub_7361D0 期間判斷＋區域 vtable[0]）；
//     真 → 圖示管理器 sub_521B20() 的 sub_F71FA0(effect, 1) 與 sub_F71800(effect, 字串 4448)；
//     假 → sub_F71FA0(effect, 0) 與 sub_F71800(effect, "")（0x116C80E 空字串）。
//     清單來源是 Tile.pak 的 ExpInfo.xml（sub_7363E0 讀 BoostEvent 的 StartDate／EndDate／DurationMin／IntervalMin／
//     BroadcastMessage 與 Zone 的 Map／Territory，字串在 VA 0x116C804–0x116C8DC）。本機 0-客戶端所有 .pak 都沒有 ExpInfo.xml
//     → 清單為空 → 一律走「假」。本模組只記錄旗標與字串 id（PacketBoxStatusIcon.Boost／BoostText），不畫；
//     判斷式由 LeavesBoostZone 注入（預設 false）。

/// <summary>S250/114 保護（正義／邪惡／遭遇）圖示種類；名稱取自 3.81 `S_PacketBoxProtection` 常數。</summary>
public enum PacketBoxProtection {JusticeL1=0,JusticeL2=1,JusticeL3=2,EvilL1=3,EvilL2=4,EvilL3=5,Encounter=6}

/// <summary>S250/159 每一筆限時地圖：順序 id、地圖名稱、剩餘分鐘（伺服器以整數除 60）。</summary>
public sealed record PacketBoxMapTime(uint OrderId,string MapName,int Minutes);

/// <summary>
/// S250/180 無時間圖示。IconId 依原版 sub_938857 是 effectlist2 的 effect id（見 <see cref="Effect"/>）；
/// StringId 照收但原版不用（圖示文字取 effect 的 startMsg／tooltip）。
/// </summary>
public sealed record PacketBoxNoneTimeIcon(uint IconId,uint StringId) {
    /// <summary>原版當成 effectlist2 effect id 使用的值（＝封包第一個 D）。</summary>
    public uint Effect=>IconId;
}

/// <summary>
/// B2 子碼依原版換成的一個狀態圖示（只做資料；畫面由 ui-buff-icons 讀 effectlist2 畫）。
/// DisplayMode：sub_F05A90 第 3 參數（0＝秒數 tooltip、1＝百分比）。Value：模式 0 時為秒數（−1＝無期限，null＝取 effect 的
/// 預設 duration，見 DefaultDuration）；模式 1 時為百分比數值。IconOverride：原版改寫 effect 表 icon 的 .tbt 編號（75 用）。
/// Name／ValueText 是給 WinForms 狀態列的中文說明（原生 UI 不用，字串取原客戶端字串表）。
/// ReceivedAt：DefaultDuration 的圖示最近一次加入／更新的時刻（MagicStatusClock，毫秒）；原生 UI 用它從 effect 的 duration 倒數（R4-F）。
/// Boost／BoostText（R7-E，只有 82）：VA 0x736B59 的 sub_F71FA0 旗標與 sub_F71800 字串 id（4448；旗標關時原版設空字串 → null）。
/// </summary>
public sealed record PacketBoxStatusIcon(int Effect,int DisplayMode,int? Value,bool DefaultDuration,int? IconOverride,string Origin,string Name,string ValueText,long? ReceivedAt=null,bool Boost=false,int? BoostText=null);

/// <summary>S250/53 最近一筆料理封包附帶的能力值與食物值（伺服器值：食物＝飽食度×10−250，下限 0）。</summary>
public sealed record PacketBoxCookingStats(int Type,byte IconKind,byte Str,byte Int,byte Wis,byte Dex,byte Con,byte Cha,ushort Food);

public static class PacketBoxStatusRules {
    public const byte Opcode=250;
    public const byte Cooking=53,Doll=56,DelIcon=59,Cake=60,Dk=75,Leaves=82,Karma=87,Protection=114,Evasion=132,MapLimitTimer=153,MapTimer=159,WeaponRange=160,NoneTimeIcon=180;
    public static readonly byte[] Subtypes=[Cooking,Doll,DelIcon,Cake,Dk,Leaves,Karma,Protection,Evasion,MapLimitTimer,MapTimer,WeaponRange,NoneTimeIcon];
    // MagicBuffs 的鍵：沒有技能 id 的狀態沿用 MagicStatus 的負數慣例（-3000-子碼；20/21 的 -子碼×1000-類型）。
    public const int DollBuff=-3056,CakeBuff=-3060,MapLimitBuff=-3153,WaterLifeSkill=170;
    public static int CookingBuff(int type)=>-Cooking*1000-type;
    public const byte CakeType=8;
    // S_PacketBoxCooking 第 11 個欄位：類型 7＝0x24、類型 54＝0x2A、其他＝0x26。
    public static bool KnownCookingIconKind(int type,byte kind)=>kind==(type switch{7=>0x24,54=>0x2A,_=>0x26});

    // ── T265 原版對照（見檔頭）。effect id＝effectlist2.xml，id ≥ EffectTableCount 的圖示原版不建立。
    public const int EffectTableCount=540;
    public const int ModeSeconds=0,ModePercent=1;
    /// <summary>59：只移除 effect 57 WATER_LIFE_S；引擎中對應的 MagicBuffs 鍵是技能 170 水之元氣（3.81 L1SkillId.WATER_LIFE）。</summary>
    public const int WaterLifeEffect=57;
    /// <summary>75：effect 135；icon 改為 DkIconBase − 階段（0xC9B＋3＝3230）。秒數取 effect 135 的預設 duration。</summary>
    public const int DkEffect=135,DkIconBase=0xC9B+3;
    public static int DkIcon(int level)=>DkIconBase-level;
    /// <summary>82：effect 175（模式 1 百分比）；值 &gt; LeavesMax 時為 575（不在表中，不建立圖示）。</summary>
    public const int LeavesEffect=175,LeavesOverflowEffect=575,LeavesMax=200;
    /// <summary>82 值 ≤ 0 時原版移除的 effect（sub_736AC0 尾段）。</summary>
    public static readonly int[] LeavesRemoved=[175,518,574,575,576];
    /// <summary>82 的 effect（兩個變體旗標都關）：值 ≤ 0 → null（移除）。</summary>
    public static int? LeavesEffectFor(int value)=>LeavesEffectFor(value,false,false);
    /// <summary>
    /// R5-F（T311／#738）82 的 effect，sub_736AC0（VA 0x736AC0／FO 0x335EC0）逐條：值 ≤ 0 → null（移除 LeavesRemoved）；
    /// 全域 0x138BE64＝0x1900（mapBase6400）→ 574（不看值）；值 &gt; 200 → 旗標開 576、關 575；其他 → 旗標開 518、關 175。
    /// 旗標＝EinhasadExManager +0x10：事件 0x15（圖示新增，sub_F6CB50 → sub_F6D120 push 0x15）的 effect＝0x125（293）時設 1
    /// （handler VA 0x736070），事件 0x16 時清 0（VA 0x7360C0），兩者之後都重跑 sub_736AC0。
    /// </summary>
    public static int? LeavesEffectFor(int value,bool mapBase6400,bool pcCafe)=>value<=0?null
        :mapBase6400?LeavesMapEffect
        :value>LeavesMax?(pcCafe?LeavesOverflowPcCafeEffect:LeavesOverflowEffect)
        :pcCafe?LeavesPcCafeEffect:LeavesEffect;
    /// <summary>82 變體：574（地圖基底 0x1900）、518（旗標開、值 ≤ 200）、576（旗標開、值 &gt; 200）。</summary>
    public const int LeavesMapEffect=574,LeavesPcCafeEffect=518,LeavesOverflowPcCafeEffect=576;
    /// <summary>旗標來源 effect 293（本機 effectlist2：STR_STATUS_EFFECT_PC_CAFE_CHRISTER，kind neutrality）；引擎中只有 180 會送出它。</summary>
    public const int PcCafeEffect=0x125;
    /// <summary>0x138BE64 的比較值（sub_F09D74 存的是 sub_F027F0(地圖)＝基底地圖）。</summary>
    public const int LeavesMapBase=0x1900;
    /// <summary>
    /// 基底地圖 sub_F14F90（VA 0xF14F90）：先查客戶端的地圖對照表（std::map，來源未展開，本模組沒有 → 待驗證），
    /// 查不到時 地圖 &lt; 0x4000 → 原值，否則 地圖 &amp; 0xFE00（已驗證）。
    /// </summary>
    public static int BaseMap(int map)=>map<0x4000?map:map&0xFE00;
    /// <summary>R7-E：VA 0x736B9D `push 0x1160` → 區域判斷為真時 sub_F71800 設的字串（string-c.tbl 4448；本機表 3484 筆沒有）。</summary>
    public const int LeavesBoostText=0x1160;
    /// <summary>180：無期限（sub_F05B10 第一個參數 −1）、模式 0。</summary>
    public const int NoLimit=-1;
    /// <summary>
    /// R5-F：180 開時 sub_F05B10 的第 5 參數為 1 → effect 表 +12（startMsg，string-c.tbl id）≠ 0 時 sub_7DC800(startMsg, 色 24, 外框色 14)
    /// 印進聊天欄（id ≥ 字串表筆數時不印；sub_A11280 的過濾條件未展開）。每次「開」都印，已存在的圖示也一樣。
    /// </summary>
    public const int StartMessageColorIndex=24;
}

public sealed partial class WorldSession {
    readonly object packetBoxStatusGate=new();
    byte? packetBoxDkLevel,packetBoxLeaves,packetBoxEvasion,packetBoxWeaponRange;
    long? packetBoxDkAt;   // 最近一次 75（階段 >0）的時刻：原版每次都以 effect 135 的 duration 重新 sub_F05A90（更新既有圖示）
    int? packetBoxKarma;
    PacketBoxCookingStats? packetBoxCooking;
    IReadOnlyList<PacketBoxMapTime>? packetBoxMapTimes;
    readonly SortedSet<PacketBoxProtection> packetBoxProtections=[];
    readonly SortedDictionary<uint,PacketBoxNoneTimeIcon> packetBoxNoneTimeIcons=[];
    readonly SortedDictionary<int,int> packetBoxStatusCounts=[];
    int packetBoxDelIcons,packetBoxUnknownProtections;
    // R5-F：82 最近一次評估（82 本身、或 effect 293 的新增／移除）時的基底地圖；原版只在這兩個時機重算，不因換地圖重算。
    int? packetBoxLeavesMapBase;
    // R7-E：82 已建立的變體（effect → 最後一次的值、按鈕附加旗標）。原版不移除舊變體，值 ≤ 0 才全清。
    readonly SortedDictionary<int,(byte Value,bool Boost)> packetBoxLeavesShown=[];
    readonly List<PacketBoxMessageLine> packetBoxStartMessages=[];
    int packetBoxStartMessagesDropped;

    /// <summary>
    /// R5-F：180 的 startMsg 查表來源（effectlist2）。預設是 StatusIconArt.SharedEffectTable（原客戶端 Tile.pak，讀不到為 null → 不印、計入 dropped）；
    /// 測試換成合成表。
    /// </summary>
    internal Func<BuffEffectTable?> StartMessageEffects{get;set;}=StatusIconArt.SharedEffectTable;
    /// <summary>R5-F：180 印進聊天欄的 startMsg（最近 32 行，子碼 180）。</summary>
    public PacketBoxMessageLine[] NoneTimeStartMessages{get{lock(packetBoxStatusGate)return [..packetBoxStartMessages];}}
    /// <summary>R5-F：82 的變體旗標 +0x10（effect 293 正在顯示）。</summary>
    public bool LeavesPcCafeFlag{get{lock(packetBoxStatusGate)return packetBoxNoneTimeIcons.ContainsKey(PacketBoxStatusRules.PcCafeEffect);}}
    int? CurrentBaseMap()=>MapId is ushort m?PacketBoxStatusRules.BaseMap(m):null;
    /// <summary>
    /// R7-E：VA 0x736B59 的 sub_7362F0（玩家是否在 ExpInfo.xml 某個 BoostEvent 的期間與區域內）。本機客戶端沒有 ExpInfo.xml，
    /// 原版清單為空 → 一律 false；測試或日後接上 ExpInfo.xml 時替換。
    /// </summary>
    internal Func<bool> LeavesBoostZone{get;set;}=()=>false;
    /// <summary>R7-E：82 目前建立的變體 effect（依 id），各自保留最後一次的值。</summary>
    public int[] LeavesShownEffects{get{lock(packetBoxStatusGate)return [..packetBoxLeavesShown.Keys];}}

    /// <summary>
    /// sub_736AC0（R5-F 選變體＋R7-E 累積與按鈕附加）。呼叫端持有 packetBoxStatusGate。值 ≤ 0（或還沒收過 82）→ 移除全部變體；
    /// 否則只新增／更新選到的那一個（≥ 540 原版不建立），其他變體不動。
    /// </summary>
    void EvaluateLeavesLocked(bool boost){
        if(packetBoxLeaves is not byte v||PacketBoxStatusRules.LeavesEffectFor(v,packetBoxLeavesMapBase==PacketBoxStatusRules.LeavesMapBase,
               packetBoxNoneTimeIcons.ContainsKey(PacketBoxStatusRules.PcCafeEffect)) is not int e){
            packetBoxLeavesShown.Clear();return;
        }
        if(e<PacketBoxStatusRules.EffectTableCount)packetBoxLeavesShown[e]=(v,boost);
    }
    bool LeavesBoostNow(){try{return LeavesBoostZone();}catch(Exception ex) when(ex is UiResourceException or UiLayoutException or IOException or InvalidDataException){return false;}}

    /// <summary>B2 子碼套用後觸發（參數＝子碼），給狀態列／原生 UI 重新讀取。</summary>
    public event Action<int>? PacketBoxStatusChanged;

    public byte? PacketBoxDkLevel{get{lock(packetBoxStatusGate)return packetBoxDkLevel;}}
    public byte? LeavesValue{get{lock(packetBoxStatusGate)return packetBoxLeaves;}}
    public int? Karma{get{lock(packetBoxStatusGate)return packetBoxKarma;}}
    public byte? EvasionRate{get{lock(packetBoxStatusGate)return packetBoxEvasion;}}
    public byte? WeaponRange{get{lock(packetBoxStatusGate)return packetBoxWeaponRange;}}
    public PacketBoxCookingStats? CookingStats{get{lock(packetBoxStatusGate)return packetBoxCooking;}}
    public IReadOnlyList<PacketBoxMapTime>? MapTimes{get{lock(packetBoxStatusGate)return packetBoxMapTimes;}}
    public PacketBoxProtection[] Protections{get{lock(packetBoxStatusGate)return [..packetBoxProtections];}}
    public PacketBoxNoneTimeIcon[] NoneTimeIcons{get{lock(packetBoxStatusGate)return [..packetBoxNoneTimeIcons.Values];}}

    /// <summary>
    /// T265：75／82／180 依原版換成的 effectlist2 圖示（順序：75、82、180 依 effect id）。原版不建立的（effect ≥ 540）不列出。
    /// 給 ui-buff-icons（#473）與 WinForms 狀態列讀；改變時觸發 <see cref="PacketBoxStatusChanged"/>，MagicRevision 也會遞增。
    /// </summary>
    public PacketBoxStatusIcon[] PacketBoxStatusIcons{get{lock(packetBoxStatusGate){
        var list=new List<PacketBoxStatusIcon>();
        if(packetBoxDkLevel is byte lv&&lv>0)
            list.Add(new(PacketBoxStatusRules.DkEffect,PacketBoxStatusRules.ModeSeconds,null,true,PacketBoxStatusRules.DkIcon(lv),$"dk:{lv}","弱點曝光",$"第 {lv} 階",packetBoxDkAt));
        // R5-F：82 依變體旗標選 175／518／574／575／576（≥ 540 的不建立）。R7-E：照原版累積，換變體時舊變體保留（依 effect id 列出，
        // 各帶最後一次的值），值 ≤ 0 才一起移除。
        foreach(var (le,(v,boost)) in packetBoxLeavesShown)
            list.Add(new(le,PacketBoxStatusRules.ModePercent,v,false,null,$"leaves:{v}"+(le==PacketBoxStatusRules.LeavesEffect?"":$":{le}"),"殷海薩的祝福",$"{v}%",
                Boost:boost,BoostText:boost?PacketBoxStatusRules.LeavesBoostText:null));
        foreach(var i in packetBoxNoneTimeIcons.Values)
            if(i.Effect<PacketBoxStatusRules.EffectTableCount)
                list.Add(new((int)i.Effect,PacketBoxStatusRules.ModeSeconds,PacketBoxStatusRules.NoLimit,false,null,$"none-time:{i.Effect}",$"狀態 {i.Effect}","無期限"));
        return [..list];
    }}}

    [WorldReceive(250,Subtype=53)][WorldReceive(250,Subtype=56)][WorldReceive(250,Subtype=59)][WorldReceive(250,Subtype=60)]
    [WorldReceive(250,Subtype=75)][WorldReceive(250,Subtype=82)][WorldReceive(250,Subtype=87)][WorldReceive(250,Subtype=114)]
    [WorldReceive(250,Subtype=132)][WorldReceive(250,Subtype=153)][WorldReceive(250,Subtype=159)][WorldReceive(250,Subtype=160)]
    [WorldReceive(250,Subtype=180)]
    bool ReceivePacketBoxStatus(byte[] plain){
        // 先完整解析與驗證整包，再改狀態；格式錯誤丟 ProtocolException，不留半套狀態。
        var r=new LoginReader(plain);
        if(r.C()!=PacketBoxStatusRules.Opcode)throw new ProtocolException("not S250");
        int sub=r.C();
        Action apply;
        switch(sub){
        case PacketBoxStatusRules.Cooking:{
            byte str=r.C(),intel=r.C(),wis=r.C(),dex=r.C(),con=r.C(),cha=r.C();ushort food=r.H();
            int type=r.C();byte kind=r.C();int seconds=r.H();byte tail=r.C();r.Finish();
            if(tail!=0)throw new ProtocolException("invalid cooking icon tail");
            if(!PacketBoxStatusRules.KnownCookingIconKind(type,kind))throw new ProtocolException("invalid cooking icon kind");
            apply=()=>{
                ReceiveBuff(PacketBoxStatusRules.CookingBuff(type),seconds,"cooking",name:$"料理 {type}");
                lock(packetBoxStatusGate)packetBoxCooking=new(type,kind,str,intel,wis,dex,con,cha,food);
            };
            break;}
        case PacketBoxStatusRules.Doll:{
            // TW16 case 0x38 讀 `hhd`；3.81 只寫 H(秒)，其餘落在 0 padding。
            int seconds=r.H();r.Finish();
            apply=()=>ReceiveBuff(PacketBoxStatusRules.DollBuff,seconds,"doll",name:"魔法娃娃");
            break;}
        case PacketBoxStatusRules.DelIcon:{
            // 3.81 只在水之元氣（技能 170）結束時送。原版 case 0x3B（VA 0x93AB77）不讀欄位，只在 effect 57 WATER_LIFE_S
            // 存在時移除它（已驗證，T265）；引擎中 effect 57 對應的鍵是技能 170，所以只刪這一個鍵，不論來源。
            if(r.H()!=0)throw new ProtocolException("invalid del-icon field");
            r.Finish();
            apply=()=>{
                if(magicBuffs.TryRemove(PacketBoxStatusRules.WaterLifeSkill,out _))MagicRevision++;
                lock(packetBoxStatusGate)packetBoxDelIcons++;
            };
            break;}
        case PacketBoxStatusRules.Cake:{
            int units=r.C();byte type=r.C();r.Finish();
            if(type!=PacketBoxStatusRules.CakeType)throw new ProtocolException("invalid third-speed type");
            apply=()=>ReceiveBuff(PacketBoxStatusRules.CakeBuff,units*4,"cake",name:"三段加速",approximate:true);
            break;}
        case PacketBoxStatusRules.Dk:{
            byte level=r.C();r.Finish();
            long at=MagicStatusClock();
            apply=()=>{lock(packetBoxStatusGate){packetBoxDkLevel=level;packetBoxDkAt=level>0?at:null;}};
            break;}
        case PacketBoxStatusRules.Leaves:{
            // S_PacketBoxExp：C(值) C(0)×3。TW16 case 0x52 讀 `ddd`，第一個 D 正好是值；原版把值當 effect 175 的百分比（T265）。
            // 3.81 的值＝殷海薩祝福經驗 ÷ LeavesSet.EXP。
            byte value=r.C();if(r.C()!=0||r.C()!=0||r.C()!=0)throw new ProtocolException("invalid leaves padding");r.Finish();
            int? mapBase=CurrentBaseMap();
            apply=()=>{bool boost=value>0&&LeavesBoostNow();lock(packetBoxStatusGate){packetBoxLeaves=value;packetBoxLeavesMapBase=mapBase;EvaluateLeavesLocked(boost);}};
            break;}
        case PacketBoxStatusRules.Karma:{
            int karma=unchecked((int)r.D());r.Finish();
            apply=()=>{lock(packetBoxStatusGate)packetBoxKarma=karma;};
            break;}
        case PacketBoxStatusRules.Protection:{
            uint model=r.D(),on=r.D();r.Finish();
            if(on>1)throw new ProtocolException("invalid protection switch");
            apply=()=>{lock(packetBoxStatusGate){
                if(model>(uint)PacketBoxProtection.Encounter){packetBoxUnknownProtections++;return;}
                var p=(PacketBoxProtection)model;if(on==1)packetBoxProtections.Add(p);else packetBoxProtections.Remove(p);
            }};
            break;}
        case PacketBoxStatusRules.Evasion:{
            byte er=r.C();r.Finish();
            apply=()=>{lock(packetBoxStatusGate)packetBoxEvasion=er;};
            break;}
        case PacketBoxStatusRules.MapLimitTimer:{
            int seconds=unchecked((int)r.D());r.Finish();
            if(seconds<0)throw new ProtocolException("negative map limit timer");
            apply=()=>ReceiveBuff(PacketBoxStatusRules.MapLimitBuff,seconds,"map-limit",name:"地圖限時");
            break;}
        case PacketBoxStatusRules.MapTimer:{
            uint count=r.D();
            // 每筆至少 9 byte（D＋空字串＋D），先擋掉不可能的筆數，避免巨大配置。
            if(count>(uint)(r.Remaining/9))throw new ProtocolException("map timer count exceeds packet");
            var list=new List<PacketBoxMapTime>((int)count);
            for(int i=0;i<count;i++){uint order=r.D();string name=r.S();int minutes=unchecked((int)r.D());list.Add(new(order,name,minutes));}
            r.Finish();
            apply=()=>{lock(packetBoxStatusGate)packetBoxMapTimes=list;};
            break;}
        case PacketBoxStatusRules.WeaponRange:{
            // TW16 case 0xA0 讀 `ccc`；3.81 只寫一個 C。
            byte range=r.C();r.Finish();
            apply=()=>{lock(packetBoxStatusGate)packetBoxWeaponRange=range;};
            break;}
        case PacketBoxStatusRules.NoneTimeIcon:{
            byte on=r.C();uint icon=r.D(),text=r.D();if(r.H()!=0)throw new ProtocolException("invalid none-time icon tail");r.Finish();
            if(on>1)throw new ProtocolException("invalid none-time icon switch");
            int? mapBase=CurrentBaseMap();
            apply=()=>{
                bool boost=icon==PacketBoxStatusRules.PcCafeEffect&&LeavesBoostNow();
                lock(packetBoxStatusGate){
                    bool had=packetBoxNoneTimeIcons.ContainsKey(icon);
                    if(on==1)packetBoxNoneTimeIcons[icon]=new(icon,text);else packetBoxNoneTimeIcons.Remove(icon);
                    // R5-F：effect 293 新增（事件 0x15）或移除（0x16）時原版重跑 sub_736AC0 → 以當下的基底地圖重算 82。
                    // R7-E：重跑也是累積（新變體加入，舊變體保留）。
                    if(icon==PacketBoxStatusRules.PcCafeEffect&&had!=(on==1)){packetBoxLeavesMapBase=mapBase;EvaluateLeavesLocked(boost);}
                }
                if(on==1&&icon<PacketBoxStatusRules.EffectTableCount)PrintNoneTimeStartMessage((int)icon);
            };
            break;}
        default:return false;
        }
        apply();
        lock(packetBoxStatusGate)packetBoxStatusCounts[sub]=packetBoxStatusCounts.GetValueOrDefault(sub)+1;
        MagicRevision++;
        PacketBoxStatusChanged?.Invoke(sub);
        return true;
    }

    /// <summary>R5-F：180 開 → effect 的 startMsg（string-c.tbl）進聊天欄，色 24。表或字串讀不到、startMsg 0 → 不印（不猜文字）。</summary>
    void PrintNoneTimeStartMessage(int effect){
        BuffEffectTable? table;
        try{table=StartMessageEffects();}catch(Exception ex) when(ex is UiResourceException or UiLayoutException or IOException or InvalidDataException){table=null;}
        int id=table?[effect]?.StartMsg??0;
        string? text=id>0?PacketBoxMessageRules.Text(id,[]):null;
        lock(packetBoxStatusGate){
            if(text==null){if(table==null||id>0)packetBoxStartMessagesDropped++;return;}
            packetBoxStartMessages.Add(new(PacketBoxStatusRules.NoneTimeIcon,id,[],text));
            if(packetBoxStartMessages.Count>32)packetBoxStartMessages.RemoveAt(0);
        }
        playerChatLines.Enqueue(new(++playerChatSequence,0,text){ColorIndex=PacketBoxStatusRules.StartMessageColorIndex});
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }

    [WorldReset]
    void ResetPacketBoxStatus(){
        lock(packetBoxStatusGate){
            packetBoxDkLevel=packetBoxLeaves=packetBoxEvasion=packetBoxWeaponRange=null;packetBoxKarma=null;packetBoxDkAt=null;
            packetBoxCooking=null;packetBoxMapTimes=null;packetBoxProtections.Clear();packetBoxNoneTimeIcons.Clear();
            packetBoxStatusCounts.Clear();packetBoxDelIcons=0;packetBoxUnknownProtections=0;
            packetBoxLeavesMapBase=null;packetBoxLeavesShown.Clear();packetBoxStartMessages.Clear();packetBoxStartMessagesDropped=0;
        }
    }

    [WorldSnapshot("packetBoxStatus")]
    object? PacketBoxStatusSnapshot(){
        lock(packetBoxStatusGate)return new{
            packets=packetBoxStatusCounts.ToDictionary(x=>x.Key.ToString(),x=>x.Value),
            dkLevel=packetBoxDkLevel,leaves=packetBoxLeaves,karma=packetBoxKarma,evasion=packetBoxEvasion,weaponRange=packetBoxWeaponRange,
            cooking=packetBoxCooking is {} c?new{type=c.Type,iconKind=c.IconKind,str=c.Str,@int=c.Int,wis=c.Wis,dex=c.Dex,con=c.Con,cha=c.Cha,food=c.Food}:null,
            mapTimes=packetBoxMapTimes?.Select(m=>new{orderId=m.OrderId,mapName=m.MapName,minutes=m.Minutes}).ToArray(),
            protections=packetBoxProtections.Select(p=>p.ToString()).ToArray(),
            unknownProtections=packetBoxUnknownProtections,
            noneTimeIcons=packetBoxNoneTimeIcons.Values.Select(i=>new{iconId=i.IconId,stringId=i.StringId}).ToArray(),
            delIcons=packetBoxDelIcons,
            leavesMapBase=packetBoxLeavesMapBase,leavesPcCafe=packetBoxNoneTimeIcons.ContainsKey(PacketBoxStatusRules.PcCafeEffect),
            startMessages=packetBoxStartMessages.Select(l=>new{stringId=l.StringId,text=l.Text}).ToArray(),startMessagesDropped=packetBoxStartMessagesDropped,
            leavesShown=packetBoxLeavesShown.Keys.ToArray(),
            statusIcons=PacketBoxStatusIcons.Select(i=>new{effect=i.Effect,mode=i.DisplayMode,value=i.Value,defaultDuration=i.DefaultDuration,iconOverride=i.IconOverride,origin=i.Origin,boost=i.Boost,boostText=i.BoostText}).ToArray(),
            ui="data only: timed states feed MagicBuffs; 75/82/180 effect ids in statusIcons; icon drawing belongs to native UI buff-icons",
        };
    }
}
