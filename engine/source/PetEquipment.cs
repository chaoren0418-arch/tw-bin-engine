namespace P06;

// T250（#611，PBW-E）：S250 子碼 37 GUI_PET_EQUIPMENT——寵物穿脫裝備後更新該寵物的 AC 與裝備欄（packetbox-windows.md §2.1、§6）。
// 3.81 S_PetEquipment：C(250) C(37) C(data) D(petId) C(listNo) C(petAc)；與 TW16 case 0x25 讀的 `cdcc` 同寬（已驗證）。
//   data   ＝客戶端 C104（C_UsePetItem）第一個 byte 原樣回傳（本引擎只送 2／0x16，見 PetPackets.UseItem）。
//   listNo ＝寵物背包（S176 類型 11）的索引。
//   petAc  ＝穿脫後寵物 AC，以有號 byte 解讀（同 PetBag.Ac）。
// 原版收到後送 C_CHECK_INVENTORY（TW16 op 160）要寵物背包；對應的 3.81 C99 C_Check 只記 log、不回任何封包，
// 3.81 也不送 S176 類型 11 回應它，所以本模組**不回送 C99，也不自動送任何封包**（只存資料）。
// 原版沒有開窗；裝備欄的 Equipped 位元在 S250/37 裡沒有，本模組只記「哪一格變了」並把已知背包標為過時，
// 由使用者在寵物面板按「重新查詢」（PetUi／Pets.cs 既有 C103 入口）取得新背包。Pets.cs 的狀態不改。

/// <summary>最近一次 S250/37 的結果。ItemId／ItemName 是收到時已知背包該格的物品（背包未知或索引超出時為 null）。</summary>
public sealed record PetEquipmentUpdate(uint PetId,byte Data,byte ListNo,sbyte Ac,long Sequence,long Revision,uint? ItemId,string? ItemName){
    /// <summary>data 是否為本引擎 C104 會送的值（2＝武器／防具；0x16）。不是也照收（原版不檢查）。</summary>
    public bool KnownData=>Data is 2 or 0x16;
}

public static class PetEquipmentPackets {
    public const byte Opcode=250,Subtype=37;
    /// <summary>解碼 S250/37（含 0 padding）。petId 0 視為格式錯誤。</summary>
    public static (byte Data,uint PetId,byte ListNo,sbyte Ac) Decode(byte[] plain){
        var r=new LoginReader(plain);
        if(r.C()!=Opcode||r.C()!=Subtype)throw new ProtocolException("not S250/37");
        byte data=r.C();uint pet=r.D();byte listNo=r.C();sbyte ac=unchecked((sbyte)r.C());r.Finish();
        if(pet==0)throw new ProtocolException("pet equipment identity");
        return (data,pet,listNo,ac);
    }
}

public sealed partial class WorldSession {
    readonly Dictionary<uint,PetEquipmentUpdate> petEquipment=new();
    readonly Dictionary<uint,long> petBagSequence=new();
    long petEquipmentSequence,petEquipmentRevision;int petEquipmentEpoch=-1,petEquipmentIgnored;

    void CheckPetEquipment(){
        if(petEquipmentEpoch!=WorldEpoch){
            petEquipmentEpoch=WorldEpoch;
            if(petEquipment.Count>0||petBagSequence.Count>0){petEquipment.Clear();petBagSequence.Clear();petEquipmentRevision++;}
        }
        foreach(uint id in petEquipment.Keys.ToArray())if(!IsOwnedCompanion(id)){petEquipment.Remove(id);petBagSequence.Remove(id);petEquipmentRevision++;}
    }

    [WorldReceive(250,Subtype=37)]
    bool ReceivePetEquipment(byte[] plain){
        var (data,pet,listNo,ac)=PetEquipmentPackets.Decode(plain);
        CheckPetEquipment();
        if(!IsOwnedCompanion(pet)){petEquipmentIgnored++;petEquipmentRevision++;return true;}
        var bag=PetInventory(pet);
        var item=bag!=null&&listNo<bag.Items.Length?bag.Items[listNo]:null;
        petEquipmentRevision++;
        petEquipment[pet]=new(pet,data,listNo,ac,++petEquipmentSequence,petEquipmentRevision,item?.ItemId,item?.Name);
        return true;
    }

    // 只觀察 S176 類型 11（寵物背包）到達的先後，用來判斷 AC 取哪一份較新；不消化、不影響 Pets.cs 的處理。
    [WorldReceive(176,Observe=true)]
    void ObservePetBagOrder(byte[] plain){
        if(plain.Length<8||plain[7]!=11)return;
        uint pet=BitConverter.ToUInt32(plain,1);if(pet==0)return;
        CheckPetEquipment();
        petBagSequence[pet]=++petEquipmentSequence;petEquipmentRevision++;
    }

    public long PetEquipmentRevision{get{CheckPetEquipment();return petEquipmentRevision;}}
    /// <summary>該寵物最近一次 S250/37；沒有收過或寵物已不在為 null。</summary>
    public PetEquipmentUpdate? PetEquipmentState(uint petId){CheckPetEquipment();return petEquipment.GetValueOrDefault(petId);}
    /// <summary>寵物背包（S176）是否比最近一次 S250/37 舊（裝備欄的 Equipped 位元可能已不正確，需重新查詢）。</summary>
    public bool PetBagStale(uint petId){
        CheckPetEquipment();
        return petEquipment.TryGetValue(petId,out var e)&&PetInventory(petId)!=null&&petBagSequence.GetValueOrDefault(petId)<e.Sequence;
    }
    /// <summary>寵物目前 AC：S250/37 與 S176 背包尾端 AC 取較新的一份；都沒有為 null。</summary>
    public sbyte? PetAc(uint petId){
        CheckPetEquipment();
        var bag=PetInventory(petId);
        if(petEquipment.TryGetValue(petId,out var e)&&(bag==null||petBagSequence.GetValueOrDefault(petId)<e.Sequence))return e.Ac;
        return bag?.Ac;
    }

    [WorldReset]
    void ResetPetEquipment(){petEquipment.Clear();petBagSequence.Clear();petEquipmentSequence=0;petEquipmentIgnored=0;petEquipmentEpoch=-1;petEquipmentRevision++;}

    [WorldSnapshot("petEquipment")]
    object? PetEquipmentSnapshot(){
        CheckPetEquipment();
        return new{
            revision=petEquipmentRevision,ignored=petEquipmentIgnored,
            pets=petEquipment.Values.OrderBy(e=>e.PetId).Select(e=>new{petId=e.PetId,data=e.Data,knownData=e.KnownData,listNo=e.ListNo,ac=e.Ac,
                itemId=e.ItemId,itemName=e.ItemName,bagStale=PetBagStale(e.PetId)}).ToArray(),
            reply="none: TW16 sends C_CHECK_INVENTORY (3.81 C99) here, but the 3.81 server never answers it; engine sends nothing",
        };
    }
}
