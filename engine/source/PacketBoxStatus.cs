namespace P06;

// T230（#548）：S250 狀態圖示類子碼（protocol-gaps.md 第 2.3 節 B2 組、第 8 節）。只做協定與資料：
// 有倒數時間的狀態併入 MagicBuffs（狀態列／原生 UI buff-icons 模組從那裡畫），
// 沒有時間的數值與圖示放進本檔的狀態與快照 "packetBoxStatus"，不畫畫面。
// 欄位寬度一律依 3.81 伺服器寫法（docs/reference/server-protocol.md），TW16 讀取寬度不同處見各子碼註解。
//
// 子碼  3.81 類別               3.81 欄位（不含 op、子碼）                 處理
// 53    S_PacketBoxCooking      C×6 能力 H(食物) C(類型) C(圖示) H(秒) C(0)    計時：-53000-類型
// 56    S_PacketBox DOLL        H(秒)                                      計時：-3056 魔法娃娃
// 59    S_PacketBoxWaterLife    H(0)                                       移除 170 水之元氣（推定）
// 60    S_PacketBoxThirdSpeed   C(秒>>2) C(8)                               計時：-3060 三段加速（4 秒單位，約略）
// 75    S_PacketBoxDk           C(階段 0–3)                                數值
// 82    S_PacketBoxExp LEAVES   C(值) C(0) C(0) C(0)                        數值
// 87    S_Karma                 D(友好度，有號)                              數值
// 114   S_PacketBoxProtection   D(種類 0–6) D(1 開／0 關)                     圖示集合
// 132   S_PacketBox EVASION     C(迴避)                                     數值
// 153   S_PacketBox MAP_LIMIT   D(秒)                                       計時：-3153 地圖限時
// 159   S_PacketBoxMapTimer     D(筆數) {D(順序) S(地圖名) D(分鐘)}          清單
// 160   S_WeaponRange           C(距離；-1 寫成 15)                           數值
// 180   S_PacketBox NONE_TIME   C(1 開／0 關) D(圖示) D(字串 id) H(0)          無時間圖示集合

/// <summary>S250/114 保護（正義／邪惡／遭遇）圖示種類；名稱取自 3.81 `S_PacketBoxProtection` 常數。</summary>
public enum PacketBoxProtection {JusticeL1=0,JusticeL2=1,JusticeL3=2,EvilL1=3,EvilL2=4,EvilL3=5,Encounter=6}

/// <summary>S250/159 每一筆限時地圖：順序 id、地圖名稱、剩餘分鐘（伺服器以整數除 60）。</summary>
public sealed record PacketBoxMapTime(uint OrderId,string MapName,int Minutes);

/// <summary>S250/180 無時間圖示：圖示 id 與字串表 id（文字由執行期字串表查，不存進 repo）。</summary>
public sealed record PacketBoxNoneTimeIcon(uint IconId,uint StringId);

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
}

public sealed partial class WorldSession {
    readonly object packetBoxStatusGate=new();
    byte? packetBoxDkLevel,packetBoxLeaves,packetBoxEvasion,packetBoxWeaponRange;
    int? packetBoxKarma;
    PacketBoxCookingStats? packetBoxCooking;
    IReadOnlyList<PacketBoxMapTime>? packetBoxMapTimes;
    readonly SortedSet<PacketBoxProtection> packetBoxProtections=[];
    readonly SortedDictionary<uint,PacketBoxNoneTimeIcon> packetBoxNoneTimeIcons=[];
    readonly SortedDictionary<int,int> packetBoxStatusCounts=[];
    int packetBoxDelIcons,packetBoxUnknownProtections;

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
            // 3.81 只在水之元氣（技能 170）結束時送；TW16 交給另函式讀，要刪哪個圖示待驗證。
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
            apply=()=>{lock(packetBoxStatusGate)packetBoxDkLevel=level;};
            break;}
        case PacketBoxStatusRules.Leaves:{
            // S_PacketBoxExp：C(值) C(0)×3。TW16 case 0x52 讀 `ddd`，寬度不同；值的意義（推定為經驗加成量）待驗證。
            byte value=r.C();if(r.C()!=0||r.C()!=0||r.C()!=0)throw new ProtocolException("invalid leaves padding");r.Finish();
            apply=()=>{lock(packetBoxStatusGate)packetBoxLeaves=value;};
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
            apply=()=>{lock(packetBoxStatusGate){if(on==1)packetBoxNoneTimeIcons[icon]=new(icon,text);else packetBoxNoneTimeIcons.Remove(icon);}};
            break;}
        default:return false;
        }
        apply();
        lock(packetBoxStatusGate)packetBoxStatusCounts[sub]=packetBoxStatusCounts.GetValueOrDefault(sub)+1;
        MagicRevision++;
        PacketBoxStatusChanged?.Invoke(sub);
        return true;
    }

    [WorldReset]
    void ResetPacketBoxStatus(){
        lock(packetBoxStatusGate){
            packetBoxDkLevel=packetBoxLeaves=packetBoxEvasion=packetBoxWeaponRange=null;packetBoxKarma=null;
            packetBoxCooking=null;packetBoxMapTimes=null;packetBoxProtections.Clear();packetBoxNoneTimeIcons.Clear();
            packetBoxStatusCounts.Clear();packetBoxDelIcons=0;packetBoxUnknownProtections=0;
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
            ui="data only: timed states feed MagicBuffs; icon drawing belongs to native UI buff-icons",
        };
    }
}
