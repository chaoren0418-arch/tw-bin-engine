namespace P06;
public static class EquipmentRules {
    // Fixed C_ItemUSe bytecode1210: weapon1903 / armor1941, no suffix reads.
    // getPacketUseType preserves these types; 26/27 and target-bearing types are excluded.
    public static bool IsArmor(byte type)=>type is 2 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 37 or 40 or 43 or 44 or 45 or 47 or 48 or 49 or 50 or 51 or 52 or 53 or 54 or 70;
    public static bool IsEquipment(byte type)=>type==1||IsArmor(type);
}
public sealed record EquipmentSlot(uint ObjectId,ushort Slot);
public sealed partial class WorldSession {
    readonly System.Collections.Concurrent.ConcurrentDictionary<uint,ushort> equipmentSlots=new();
    readonly System.Collections.Concurrent.ConcurrentDictionary<uint,bool> equipmentStates=new();
    public bool EquipmentListReceived {get;private set;}
    public int EquipmentRevision {get;private set;}
    public EquipmentSlot[] EquipmentSlots=>equipmentSlots.OrderBy(x=>x.Key).Select(x=>new EquipmentSlot(x.Key,x.Value)).ToArray();
    public bool? ItemEquipped(InventoryItem item)=>!EquipmentRules.IsEquipment(item.UseType)?null:equipmentStates.TryGetValue(item.ObjectId,out bool equipped)?equipped:EquipmentListReceived?false:item.Equipped;
    public string ItemUseAction(InventoryItem item)=>item.SupportedEquipment?(ItemEquipped(item)==true?"卸下":"穿上"):item.UseAction;
    public string ItemUseState(InventoryItem item)=>item.SupportedEquipment?(ItemEquipped(item)==true?"已裝備；可單次卸下":"未裝備；可單次穿上"):item.UseState;
    uint? pendingEquipmentItem;int pendingEquipmentEpoch;string pendingEquipmentState="";
    public bool EquipmentReplyPending=>pendingEquipmentItem!=null&&pendingEquipmentEpoch==WorldEpoch;
    string EquipmentReplyState(uint id)=>string.Join(",",EquipmentSlots.Select(s=>$"{s.ObjectId}:{s.Slot}"))+"|"+(inventory.TryGetValue(id,out var i)?ItemEquipped(i)?.ToString():"missing")+"|"+Actor?.Weapon;
    internal void MarkItemUseSent(InventoryItem item){
        LastServerMessage=null;LastServerReply=null;pendingEquipmentItem=item.SupportedEquipment?item.ObjectId:null;pendingEquipmentEpoch=WorldEpoch;
        pendingEquipmentState=item.SupportedEquipment?EquipmentReplyState(item.ObjectId):"";
        if(MarkBasicSupplySent(item))return;
        ItemNotice=item.SupportedEquipment?ItemUseAction(item)+"請求已送出；等待原服裝備與屬性回報":item.SupportedHastePotion?"單次使用已送出；等待原服消耗與速度回報":"單次使用已送出；等待原服消耗與 HP 回報";
    }
    void EquipmentMessageReceived(){
        if(!EquipmentReplyPending){pendingEquipmentItem=null;return;}
        bool unchanged=EquipmentReplyState(pendingEquipmentItem!.Value)==pendingEquipmentState;pendingEquipmentItem=null;
        ItemNotice=unchanged?"已收到原服回覆；目前裝備狀態未變，未確認本次穿卸成功。":"已收到原服回覆；裝備狀態已有更新，請核對原服回報。";
    }
    void EquipmentStateReceived(uint? id=null){if(EquipmentReplyPending&&(id==null||id==pendingEquipmentItem)){pendingEquipmentItem=null;ItemNotice="已收到原服裝備狀態更新；請核對目前裝備。";}}
    void ResetEquipment(){pendingEquipmentItem=null;pendingEquipmentState="";equipmentSlots.Clear();equipmentStates.Clear();EquipmentListReceived=false;EquipmentRevision++;}
    // Fixed JAR S_EquipmentWindow offsets 10..75: S64/65 list, S64/66 absolute update.
    // Kept separately from inventory-name evidence: no local click toggles or attribute inference.
    bool ReceiveEquipment(byte opcode,LoginReader r,byte[] plain){
        if(opcode==119){
            uint id=r.D();byte weapon=r.C();r.Finish();
            if(Actor?.ObjectId==id){Actor=Actor with{Weapon=weapon};AppearanceRevision++;}
            else UpdateObject(id,o=>o with{Action=weapon,DoorAction=o.Gfx==2346?null:o.DoorAction,ShopActive=!o.Dead&&weapon==70,ShopAt=weapon==70?(o.ShopActive?o.ShopAt:Environment.TickCount64):0,ShopText=weapon==70?o.ShopText:"",ReceivedAction=o.ReceivedAction==70&&weapon!=70?null:o.ReceivedAction,Combat=weapon==70?null:o.Combat});
            EquipmentRevision++;return true;
        }
        if(opcode!=64||plain.Length<2||plain[1] is not (65 or 66))return false;
        byte kind=r.C();
        if(kind==65){
            int count=r.C();var next=new Dictionary<uint,ushort>();
            for(int i=0;i<count;i++){
                uint id=r.D();ushort slot=r.H(),reserved=r.H();
                if(id==0||reserved!=0||!next.TryAdd(id,slot))throw new ProtocolException("invalid equipment list");
            }
            r.Finish();equipmentSlots.Clear();equipmentStates.Clear();foreach(var x in next){equipmentSlots[x.Key]=x.Value;equipmentStates[x.Key]=true;}EquipmentListReceived=true;EquipmentStateReceived();
        }else{
            uint id=r.D();byte slot=r.C(),equipped=r.C();r.Finish();
            if(id==0||equipped>1)throw new ProtocolException("invalid equipment state");
            if(equipped==1)equipmentSlots[id]=slot;else equipmentSlots.TryRemove(id,out _);equipmentStates[id]=equipped==1;EquipmentStateReceived(id);
        }
        EquipmentRevision++;return true;
    }
    public object EquipmentSnapshot()=>new{replyPending=EquipmentReplyPending,listReceived=EquipmentListReceived,revision=EquipmentRevision,slots=EquipmentSlots};
}
