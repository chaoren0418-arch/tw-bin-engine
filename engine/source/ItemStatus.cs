using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace P06;

public sealed record ItemStatusAttribute(byte Tag,string Key,string Label,long? Value=null,string? Text=null);

// See docs/protocol/item-status.md. This stream has no universal tag length:
// stop at the first unknown/incomplete field instead of guessing the next boundary.
public sealed class ItemStatus {
    public IReadOnlyList<ItemStatusAttribute> Attributes {get;}
    public bool HasDetails {get;}
    public bool Complete {get;}
    public int? ErrorOffset {get;}
    public string? Error {get;}
    public string UnparsedHex {get;}
    public int? Enchantment=>(int?)Number("enchantment");
    public int? Damage=>(int?)Number("damage");
    public long? Number(string key)=>Attributes.LastOrDefault(a=>a.Key==key)?.Value;
    private ItemStatus(List<ItemStatusAttribute> attributes,bool hasDetails,int? offset,string? error,string remaining){
        Attributes=attributes.AsReadOnly();HasDetails=hasDetails;Complete=offset==null;ErrorOffset=offset;Error=error;UnparsedHex=remaining;
    }
    public static ItemStatus Parse(string hex){
        byte[] bytes;
        try{bytes=Convert.FromHexString(hex);}catch(FormatException){return new([],hex.Length>0,0,"invalid-hex",hex);}
        var attributes=new List<ItemStatusAttribute>();int offset=0;
        ItemStatus Stop(int start,string error)=>new(attributes,bytes.Length>0,start,error,Convert.ToHexString(bytes.AsSpan(start)));
        void Add(byte tag,string key,string label,long value)=>attributes.Add(new(tag,key,label,value));
        while(offset<bytes.Length){
            int start=offset;byte tag=bytes[offset++];
            int length=tag switch{
                0x01 or 0x13 or 0x15 or 0x16=>7,
                0x17=>5,
                0x02 or 0x03 or 0x05 or 0x06 or 0x07 or >=0x08 and <=0x0d or 0x11 or >=0x1b and <=0x1e or 0x20=>1,
                0x0e or 0x0f=>2,
                0x04 or 0x12=>0,
                0x27=>-2,
                _=>-1
            };
            if(length==-1)return Stop(start,"unknown-tag");
            if(length==-2){
                int end=Array.IndexOf(bytes,(byte)0,offset);
                if(end<0)return Stop(start,"truncated-text");
                string text;
                try{text=LoginPackets.TextEncoding.GetString(bytes,offset,end-offset);}
                catch(DecoderFallbackException){return Stop(start,"invalid-text");}
                attributes.Add(new(tag,"description","說明",Text:text));offset=end+1;continue;
            }
            if(bytes.Length-offset<length)return Stop(start,"truncated-tag");
            var data=bytes.AsSpan(offset,length);offset+=length;
            switch(tag){
            case 0x01:
                Add(tag,"damageSmall","小型傷害",data[0]);Add(tag,"damageLarge","大型傷害",data[1]);
                Add(tag,"material","材質代碼",data[2]);Add(tag,"weight","重量",BinaryPrimitives.ReadUInt32LittleEndian(data[3..]));break;
            case 0x13:
                Add(tag,"armorClassMagnitude","基礎防禦幅度",data[0]);Add(tag,"material","材質代碼",data[1]);
                // Byte 3 is an armor format marker (0x27 / pet 0xff), not a string tag.
                Add(tag,"armorMarker","防具格式",data[2]);Add(tag,"weight","重量",BinaryPrimitives.ReadUInt32LittleEndian(data[3..]));break;
            case 0x15:case 0x16:
                Add(tag,tag==0x15?"nutrition":"lightRange",tag==0x15?"營養":"照明範圍",BinaryPrimitives.ReadUInt16LittleEndian(data));
                Add(tag,"material","材質代碼",data[2]);Add(tag,"weight","重量",BinaryPrimitives.ReadUInt32LittleEndian(data[3..]));break;
            case 0x17:
                Add(tag,"material","材質代碼",data[0]);Add(tag,"weight","重量",BinaryPrimitives.ReadUInt32LittleEndian(data[1..]));break;
            case 0x02:Add(tag,"enchantment","強化",(sbyte)data[0]);break;
            case 0x03:Add(tag,"damage","損傷度",data[0]);break;
            case 0x04:Add(tag,"twoHanded","雙手武器",1);break;
            case 0x05:Add(tag,"hit","命中",(sbyte)data[0]);break;
            case 0x06:Add(tag,"bonusDamage","額外傷害",(sbyte)data[0]);break;
            case 0x07:Add(tag,"classMask","職業遮罩",data[0]);break;
            case >=0x08 and <=0x0d:
                string[] keys=["strength","dexterity","constitution","wisdom","intelligence","charisma"];
                string[] labels=["力量","敏捷","體質","精神","智力","魅力"];
                Add(tag,keys[tag-8],labels[tag-8],(sbyte)data[0]);break;
            case 0x0e:Add(tag,"hp","體力上限",BinaryPrimitives.ReadInt16LittleEndian(data));break;
            case 0x0f:
                int resistance=0;
                // 0x21 qualifies the preceding H value, including MR's 0xd6 marker.
                if(offset<bytes.Length&&bytes[offset]==0x21){
                    if(offset+1>=bytes.Length)return Stop(start,"truncated-resistance");
                    byte kind=bytes[offset+1];
                    if(kind!=0xd6&&(kind<1||kind>6))return Stop(start,"unknown-resistance");
                    resistance=kind==0xd6?0:kind;offset+=2;
                }
                string[] resistKeys=["magicResistance","freezeResistance","stoneResistance","sleepResistance","blindResistance","stunResistance","sustainResistance"];
                string[] resistLabels=["抗魔","寒冰耐性","石化耐性","睡眠耐性","暗黑耐性","昏迷耐性","支撐耐性"];
                Add(tag,resistKeys[resistance],resistLabels[resistance],BinaryPrimitives.ReadInt16LittleEndian(data));break;
            case 0x11:Add(tag,"spellPower","魔攻",(sbyte)data[0]);break;
            case 0x12:Add(tag,"haste","加速效果",1);break;
            case >=0x1b and <=0x1e:
                string[] elementKeys=["fireResistance","waterResistance","windResistance","earthResistance"];
                string[] elementLabels=["火屬性抗性","水屬性抗性","風屬性抗性","地屬性抗性"];
                Add(tag,elementKeys[tag-0x1b],elementLabels[tag-0x1b],(sbyte)data[0]);break;
            case 0x20:Add(tag,"mp","魔力上限",(sbyte)data[0]);break;
            }
        }
        return new(attributes,bytes.Length>0,null,null,"");
    }
    public static string Tooltip(InventoryItem item,bool? equipped,ushort? slot){
        var lines=new List<string>();
        if(EquipmentRules.IsEquipment(item.UseType))lines.Add(equipped==true?"已裝備"+(slot is {} s?$"（槽位 {s}）":""):equipped==false?"未裝備":"裝備狀態待同步");
        lines.Add((item.Identified?"已鑑定":"未鑑定")+"；"+(item.Tradable?"可交易":"不可交易")+"；"+(item.Deletable?"可刪除":"不可刪除")+(item.Sealed?"；已封印":""));
        var status=item.Status;
        if(!status.HasDetails)lines.Add("尚無詳細屬性資料");
        foreach(var a in status.Attributes){
            if(a.Key=="armorMarker")continue;
            string text=a.Text is {} t?Regex.Replace(t,@"\\f.",""):a.Key is "twoHanded" or "haste"?a.Label:$"{a.Label}：{a.Value}";
            lines.Add(new string(text.Where(c=>!char.IsControl(c)).ToArray()));
        }
        if(!status.Complete)lines.Add("部分屬性尚未解析");
        return string.Join("\n",lines);
    }
}
