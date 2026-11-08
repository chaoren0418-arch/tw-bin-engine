namespace P06;

// M7-B（T204／#510）：S36 Ability（能力旗標）與 S37 SPMR（SP／MR）的接收與快照。
// 依據：docs/reference/server-protocol.md〈S_Ability〉〈S_SPMR〉；gap-matrix 第 1 節 op 122（TW16 S_CHANGE_ABILITY → 3.81 S36）。
// 只做協定與資料；狀態窗等畫面由原生 UI 模組處理。旗標 type 的意義在 reference 文件中未查證，所以只存原始 type 值，不命名。
//
// 封包（BasePacket 補 0 到 8 的倍數）：
//   S36：C(36) C(type) C(equipped?1:0)                       → 3 bytes，補成 8
//   S37 buildPacket／isLogin：C(37) D(sp) D(mr)（ConfigClient.isROClient＝true，本產品 CLIENT_LOGIN_TYPE=2）→ 9 bytes，補成 16
//   S37 無參數建構子：C(37) C(50) C(100)（不分 ROClient）    → 3 bytes，補成 8
public sealed record AbilityFlag(int Type,bool Equipped,int Updates);
public sealed record SpMrStatus(int Sp,int Mr,string Form,int Updates);

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

    [WorldReset]
    void ResetAbilityStatus(){lock(abilityLock){abilityFlags.Clear();abilityUpdates=0;spMr=null;}}

    [WorldSnapshot("ability")]
    object? AbilitySnapshot(){
        lock(abilityLock)return new{
            flags=abilityFlags.Values.Select(f=>new{type=f.Type,equipped=f.Equipped,updates=f.Updates}).ToArray(),
            active=abilityFlags.Values.Where(f=>f.Equipped).Select(f=>f.Type).ToArray(),
            updates=abilityUpdates,
            spMr=spMr==null?null:new{sp=spMr.Sp,mr=spMr.Mr,form=spMr.Form,updates=spMr.Updates},
        };
    }
}
