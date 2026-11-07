using System.Buffers.Binary;
using System.Text.Json;
namespace P06;
public static class EquipmentTests {
    public static void Run(string oracle,string output){
        var checks=new List<string>();void Check(bool ok,string s){if(!ok)throw new Exception(s);checks.Add(s);}
        var w=new WorldSession();
        foreach(var line in File.ReadAllLines(oracle)){
            var cells=line.Split('\t');byte[] bytes=Convert.FromHexString(cells[2]);
            w.Receive(bytes,"");string before=JsonSerializer.Serialize(w.EquipmentSlots);w.Receive(bytes,"");
            Check(JsonSerializer.Serialize(w.EquipmentSlots)==before,"absolute duplicate "+cells[0]+cells[1]);
            for(int n=1;n<bytes.Length;n++){
                before=JsonSerializer.Serialize(w.EquipmentSlots);bool bad=false;
                try{w.Receive(bytes[..n],"");}catch(ProtocolException){bad=true;}
                // S64 without its subtype is retained as unknown; every identifiable short variant must fail.
                if(n>=2||bytes[0]==119)Check(bad,"truncated "+cells[0]+cells[1]+"/"+n);
                Check(JsonSerializer.Serialize(w.EquipmentSlots)==before,"no partial mutation "+n);
            }
        }
        Check(w.EquipmentSlots.Length==3&&w.EquipmentListReceived,"original list decoded");
        w.Reset();Check(w.EquipmentSlots.Length==0&&!w.EquipmentListReceived,"reset clears equipment");
        var raw=Json.Read(Path.Combine(AppContext.BaseDirectory,"../../fixtures/combat-initialization.json")).GetProperty("rows").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();
        string selected=LoginConnection.CapturedCharacterName(raw);uint oldId=BinaryPrimitives.ReadUInt32LittleEndian(raw.First(x=>x[0]==8).AsSpan(1));
        foreach(var original in raw){if(original[0] is not (8 or 87))continue;var b=(byte[])original.Clone();int offset=b[0]==8?1:5;if(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset))!=oldId)continue;BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset),1900210001);w.Receive(b,selected);}
        foreach(var line in File.ReadAllLines(oracle).Where(x=>x.StartsWith("visual\t",StringComparison.Ordinal))){var v=line.Split('\t');w.Receive(Convert.FromHexString(v[2]),selected);Check(w.Actor!.Weapon==byte.Parse(v[1]),"original S119 updates own weapon "+v[1]);}
        Check(!w.SupportedAppearance,"unknown original weapon255 disables own appearance");
        var observer=new WorldSession();var pack=(byte[])raw.First(x=>x[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(x.AsSpan(5))==oldId).Clone();BinaryPrimitives.WriteUInt32LittleEndian(pack.AsSpan(5),1900210001);observer.Receive(pack,selected);
        foreach(var line in File.ReadAllLines(oracle).Where(x=>x.StartsWith("visual\t",StringComparison.Ordinal))){var v=line.Split('\t');observer.Receive(Convert.FromHexString(v[2]),selected);Check(observer.Objects.Single().Action==byte.Parse(v[1]),"original S119 updates observed weapon "+v[1]);}
        foreach(var v in new[]{(1,2,"+0 $0"," ($9)"),(1,4,"+0 $3"," ($9)"),(2,86,"+0 $164"," ($117)"),(25,97,"+0 $778"," ($117)")}){
            var item=new InventoryItem(1,0,(byte)v.Item1,0,(ushort)v.Item2,1,1,1,v.Item3,"","");
            Check(item.SupportedEquipment&&item.Equipped==false,"original fingerprint "+v.Item3);
            Check((item with{Name=item.Name+v.Item4}).Equipped==true,"original name marker "+v.Item3);
            Check(!(item with{IconId=65535}).SupportedEquipment,"unknown identity refused");
        }
        var a=new ObjectAssets(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets")));
        foreach(int weapon in new[]{4,20,50})foreach(int d in Enumerable.Range(0,8))foreach(string action in new[]{"walk","breath","attack","damage","die"}){
            var app=a.Get(0,weapon)!;Check(app.Supports(action,d),"original mapped action "+weapon+"/"+action+"/"+d);
            if(action!="die")Check(app.At(action,d,0).Key!=a.Get(0)!.At(action,d,0).Key,"armed frame differs from bare hands");
        }
        Check(ReferenceEquals(a.Get(0,62),a.Get(0)),"missing gauntlet stance falls back to bare hands (actions-and-names 3.4)");
        Json.Save(output,new{status="pass",kind="explicit synthetic original serializer vectors and original resource mappings; not live gameplay",checks});
    }
}
