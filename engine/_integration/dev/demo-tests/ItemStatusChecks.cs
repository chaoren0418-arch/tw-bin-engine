using P06;
using System.Text.Json;

partial class Checks {
    static byte[] StatusBlob(Action<BinaryWriter> write){using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);write(writer);return stream.ToArray();}
    [CheckGroup("item-status"),CheckGroup(CheckGroups.Default,Order=20)]
    static void ItemStatusChecks(){
        // Hand-authored wire contracts, not production serializer output or server assets.
        byte[] weapon=[1,8,12,10,100,0,0,0,2,253,3,7,4,7,127,8,254,9,2,10,3,11,4,12,5,13,6,14,0,255,15,30,0,33,214,15,251,255,33,1,17,253,18,27,10,28,246,29,20,30,30,32,251];
        var status=ItemStatus.Parse(Convert.ToHexString(weapon));
        Check(status.Complete&&status.Enchantment==-3&&status.Damage==7,"status signed enchantment and unsigned damage");
        Check(status.Number("damageSmall")==8&&status.Number("damageLarge")==12&&status.Number("weight")==100&&status.Number("material")==10,"weapon composite fields");
        Check(status.Number("strength")==-2&&status.Number("charisma")==6&&status.Number("hp")==-256&&status.Number("mp")==-5&&status.Number("spellPower")==-3,"signed ability C/H values");
        Check(status.Number("magicResistance")==30&&status.Number("freezeResistance")==-5,"qualified resistance is not mistaken for MR");
        Check(status.Number("fireResistance")==10&&status.Number("waterResistance")==-10&&status.Number("windResistance")==20&&status.Number("earthResistance")==30,"four elemental resistances");
        Check(status.Number("twoHanded")==1&&status.Number("haste")==1&&status.Number("classMask")==127,"presence tags and unsigned class mask");
        foreach(byte marker in new byte[]{0x27,0xff}){
            var armor=ItemStatus.Parse(Convert.ToHexString([19,9,6,marker,255,255,255,255,2,6,3,200]));
            Check(armor.Complete&&armor.Number("armorClassMagnitude")==9&&armor.Number("weight")==uint.MaxValue&&armor.Enchantment==6&&armor.Damage==200,"armor marker consumed inside header "+marker);
        }
        foreach(byte tag in new byte[]{21,22}){
            var detail=ItemStatus.Parse(Convert.ToHexString([tag,0,1,3,25,0,0,0]));
            Check(detail.Complete&&detail.Number(tag==21?"nutrition":"lightRange")==256&&detail.Number("weight")==25,"food/light composite "+tag);
        }
        Check(ItemStatus.Parse("170504000000").Number("weight")==4,"generic material/weight");
        for(byte kind=1;kind<=6;kind++){
            var resistance=ItemStatus.Parse(Convert.ToHexString([15,1,0,33,kind]));
            Check(resistance.Complete&&resistance.Number("magicResistance")==null&&resistance.Attributes.Single().Value==1,"resistance qualifier "+kind);
        }
        Check(ItemStatus.Parse("0F1400").Number("magicResistance")==20,"unqualified MR");
        Check(ItemStatus.Parse("05FF0602").Number("hit")==-1,"pet hit/damage tags");
        foreach(byte[] blob in new byte[][]{[1,8,9,10,20,0,0,0],[19,4,6,39,5,0,0,0],[21,5,0,1,2,0,0,0],[22,5,0,1,2,0,0,0],[23,1,2,0,0,0],[2,7],[3,7],[14,5,0],[15,5,0],[39,65,0]}){
            for(int size=1;size<blob.Length;size++){
                var broken=ItemStatus.Parse(Convert.ToHexString(blob[..size]));
                Check(!broken.Complete&&broken.ErrorOffset==0&&broken.Attributes.Count==0&&broken.UnparsedHex==Convert.ToHexString(blob[..size]),"truncated tag retains raw bytes "+blob[0]+"/"+size);
            }
        }
        var unknown=ItemStatus.Parse("0206FE0309");
        Check(!unknown.Complete&&unknown.Enchantment==6&&unknown.Damage==null&&unknown.ErrorOffset==2&&unknown.UnparsedHex=="FE0309","unknown tag stops without resync");
        Check(!ItemStatus.Parse("0F010021").Complete&&!ItemStatus.Parse("0F01002199").Complete,"incomplete/unknown resistance qualifier");
        Check(!ItemStatus.Parse("2700FF").Complete&&!ItemStatus.Parse("27A400").Complete&&!ItemStatus.Parse("Z1").Complete,"invalid string/hex guarded");
        Check(ItemStatus.Parse("").Complete&&!ItemStatus.Parse("").HasDetails&&ItemStatus.Parse("").Enchantment==null,"empty does not invent zero bonuses");

        using var connection=new LoginConnection();var world=connection.World;
        void Row(BinaryWriter b,uint id,byte use,string name,byte flags,byte[] blob){
            b.Write(id);b.Write((ushort)1);b.Write(use);b.Write((byte)0);b.Write((ushort)1);b.Write((byte)1);b.Write(1u);b.Write(flags);Text(b,name);b.Write((byte)blob.Length);b.Write(blob);b.Write(new byte[]{10,0,0,0,0,0,0,0,0,0,0});
        }
        void Equip(uint id,byte slot,bool equipped)=>Feed(connection,Packet(b=>{b.Write((byte)64);b.Write((byte)66);b.Write(id);b.Write(slot);b.Write(equipped);}));
        void FullSlots(params (uint Id,ushort Slot)[] slots)=>Feed(connection,Packet(b=>{b.Write((byte)64);b.Write((byte)65);b.Write((byte)slots.Length);foreach(var s in slots){b.Write(s.Id);b.Write(s.Slot);b.Write((ushort)0);}}));
        void Update(byte opcode,uint id,byte[] blob,string name="name")=>Feed(connection,Packet(b=>{b.Write(opcode);if(opcode==242)b.Write((byte)1);b.Write(id);Text(b,name);b.Write(2u);if(opcode==242)b.Write((ushort)blob.Length);else b.Write((byte)blob.Length);b.Write(blob);}));
        Feed(connection,Packet(b=>{b.Write((byte)5);b.Write((byte)2);Row(b,101,1,"fake ($9)",1,weapon);Row(b,102,2,"fake ($117)",47,[]);}));
        var item=world.Inventory.Single(i=>i.ObjectId==101);
        Check(item.Equipped==null&&world.ItemEquipped(item)==null&&ItemStatus.Tooltip(item,item.Equipped,null).Contains("待同步"),"S5 names never establish equipment");
        Check(item.Status.Enchantment==-3&&item.Tradable&&item.Identified,"S5 structured status and flags");
        var sealedItem=world.Inventory.Single(i=>i.ObjectId==102);
        Check(sealedItem.Sealed&&!sealedItem.Tradable&&!sealedItem.Deletable&&ItemStatus.Tooltip(sealedItem,null,null).Contains("不可交易"),"flags establish sealed/trade/delete independently of empty blob");
        Equip(101,9,true);
        Check(item.Equipped==true&&world.ItemEquipped(item)==true&&world.ItemEquipmentSlot(101)==9&&sealedItem.Equipped==null,"absolute S64 before full list updates existing item references only");
        Equip(101,9,true);Equip(101,9,false);
        Check(item.Equipped==false&&world.ItemEquipmentSlot(101)==null,"duplicate equip then absolute unequip");
        FullSlots((102,300));
        Check(item.Equipped==false&&sealedItem.Equipped==true&&world.ItemEquipmentSlot(102)==300,"full slots replace state and retain H-sized slot");
        Feed(connection,Packet(b=>{b.Write((byte)100);b.Write(101u);Text(b,"new fake ($9)");}));
        Check(world.Inventory.Single(i=>i.ObjectId==101).Equipped==false,"name update cannot override slots");
        FullSlots((101,7));
        Update(24,101,[2,9,3,4]);item=world.Inventory.Single(i=>i.ObjectId==101);
        Check(item.Status.Enchantment==9&&item.Status.Damage==4&&item.Equipped==true&&sealedItem.Equipped==false,"S24 refreshes parsed values and preserves slot binding");
        string tooltip=ItemStatus.Tooltip(item,item.Equipped,world.ItemEquipmentSlot(101));
        Check(tooltip.Contains("強化：9")&&tooltip.Contains("損傷度：4")&&tooltip.Contains("槽位 7"),"tooltip uses new status and authoritative slot");
        byte[] longBlob=StatusBlob(b=>{b.Write((byte)2);b.Write((byte)10);for(int i=0;i<40;i++){b.Write((byte)39);Text(b,"火屬性說明");}});
        Update(242,101,longBlob);item=world.Inventory.Single(i=>i.ObjectId==101);
        Check(longBlob.Length>255&&item.Status.Complete&&item.Status.Enchantment==10&&item.Status.Attributes.Count(a=>a.Key=="description")==40&&item.StatusHex==Convert.ToHexString(longBlob),"S242 H-length and repeated Big5 descriptions");
        Check(ItemStatus.Tooltip(item,true,7).Contains("火屬性說明"),"Big5 text reaches tooltip");
        Update(24,101,[2,6,250,3,8]);item=world.Inventory.Single(i=>i.ObjectId==101);
        Check(!item.Status.Complete&&item.Status.Enchantment==6&&item.Status.Damage==null&&ItemStatus.Tooltip(item,true,7).Contains("部分屬性"),"unknown blob is retained without losing inventory row");
        Update(24,101,[]);item=world.Inventory.Single(i=>i.ObjectId==101);
        Check(!item.Status.HasDetails&&item.Status.Enchantment==null&&item.Equipped==true,"empty S24 clears old detail without changing equipment");
        Feed(connection,Packet(b=>{b.Write((byte)15);Row(b,103,0,"fake ($9)",0,[23,1,1,0,0,0]);}));
        Check(world.Inventory.Single(i=>i.ObjectId==103).Equipped==null,"non-equipment has no equipped state");
        string before=JsonSerializer.Serialize(world.Inventory);
        try{Feed(connection,[24,101,0,0,0,0,1,0,0,0,10,2]);throw new Exception("short inventory envelope accepted");}catch(ProtocolException){}
        Check(JsonSerializer.Serialize(world.Inventory)==before,"truncated envelope does not partially mutate inventory");
        FullSlots();Check(item.Equipped==false,"empty full list clears slots");
        world.Reset();Check(item.Equipped==null&&world.ItemEquipmentSlot(101)==null,"session reset clears authoritative evidence");
        Equip(104,2,true);
        Feed(connection,Packet(b=>{b.Write((byte)15);Row(b,104,1,"no suffix",1,[2,5]);}));
        Check(world.Inventory.Single().Equipped==true,"slots arriving before S15 are honored");
    }
}
