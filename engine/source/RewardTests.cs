using System.Buffers.Binary;
namespace P06;
public static class RewardTests {
    public static void SelectionReplay(string capture,string output){
        using var j=System.Text.Json.JsonDocument.Parse(File.ReadAllText(capture));var root=j.RootElement;const uint id=2000041706;
        string name=root.GetProperty("final").GetProperty("main").GetProperty("world").GetProperty("actor").GetProperty("Name").GetString()!;
        var w=new WorldSession();int removals=0,reappearances=0;bool removed=false;
        foreach(var row in root.GetProperty("packets").EnumerateArray()){
            var p=row.GetProperty("packet");if(p.GetProperty("kind").GetString()=="seed")continue;
            byte[] b=Convert.FromHexString(p.GetProperty("plainHex").GetString()!);
            bool remove=b[0]==120&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(1))==id;
            bool pack=b[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5))==id;
            if(remove){if(w.Objects.SingleOrDefault(v=>v.ObjectId==id) is not {Dead:false})throw new Exception("original removal precondition");w.SelectTarget(id);}
            w.Receive(b,name);
            if(remove){if(w.TargetId!=null||w.Objects.Any(v=>v.ObjectId==id))throw new Exception("original removal retained target");removed=true;removals++;}
            if(pack&&removed){if(w.TargetId!=null)throw new Exception("original reappearance automatically selected target");w.SelectTarget(id);if(w.TargetId!=id)throw new Exception("explicit fresh selection unavailable");w.SelectTarget(null);removed=false;reappearances++;}
        }
        if(removals<2||reappearances<1)throw new Exception("original repeated teleport evidence absent");
        if(root.GetProperty("sent").EnumerateArray().Any(r=>r.GetProperty("packet").GetProperty("plainHex").GetString()!.StartsWith("E5",StringComparison.Ordinal)))throw new Exception("capture already attacked");
        Json.Save(output,new{status="pass",kind="retained original supervisor UI S55/S120/S87 replay; explicit synthetic click timing, no server login",capture,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(capture))),id,removals,reappearances,assertions=new[]{"S120 clears selected live target","S87 reappearance never auto-selects","only explicit new selection restores target","failure capture has zero attacks"}});
    }
    public static byte[] Status(byte level,uint exp){var p=new byte[56];p[0]=8;p[1]=1;p[5]=level;BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(6),exp);return p;}
    public static void Run(string oracle,string output){
        var checks=new List<string>();void Check(bool x,string n){if(!x)throw new Exception(n);checks.Add(n);}
        var w=new WorldSession();w.Receive(Status(60,416461882),"");
        foreach(var line in File.ReadAllLines(oracle)){
            var v=line.Split('\t');byte level=byte.Parse(v[1]);ulong exp=ulong.Parse(v[2]);byte[] p=Convert.FromHexString(v[3]);
            w.Receive(p,"");Check(w.Status!.Level==level&&w.Status.Exp==(uint)exp,"original serializer "+v[0]+"/"+exp);
            var count=w.ExperienceChanges;w.Receive(p,"");Check(w.ExperienceChanges==count,"duplicate not added "+exp);
            if(exp>uint.MaxValue)Check(w.Experience!.Total==null,"missing high bits unknown "+exp);
            for(int n=1;n<p.Length;n++){bool failed=false;try{w.Receive(p[..n],"");}catch(ProtocolException){failed=true;}Check(failed,"truncated "+exp+"/"+n);}
        }
        w.Receive(Status(60,416461882),"");Check(w.Experience!.Percent==0,"level60 original lower boundary");
        w.Receive(Status(61,452526974),"");Check(w.Experience!.Percent==0,"level61 boundary");
        var stable=w.Experience;long changes=w.ExperienceChanges;
        foreach(var line in File.ReadAllLines(Path.Combine(Path.GetDirectoryName(oracle)!,"notification-oracle.tsv"))){w.Receive(Convert.FromHexString(line.Split('\t')[2]),"");Check(w.Experience==stable&&w.ExperienceChanges==changes&&w.Inventory.Length==0,"original RO notification cannot grant EXP/items "+line.Split('\t')[0]);}
        byte[] Record(uint count)=>[..BitConverter.GetBytes(1900249901u),0,0,0,0,5,0,0,..BitConverter.GetBytes(count),1,..LoginPackets.TextEncoding.GetBytes("$4 ("+count+")"),0,0,10,0,0,0,0,0,0,0,0,0,0];
        var list=LoginPackets.Pad([5,1,..Record(3)]);w.Receive(list,"");Check(w.InventoryChanges.Length==0,"initial list is baseline");
        var update=LoginPackets.Pad([24,..BitConverter.GetBytes(1900249901u),36,52,0,..BitConverter.GetBytes(33u),0]);w.Receive(update,"");w.Receive(update,"");Check(w.Inventory.Single().Count==33&&w.InventoryChanges.Length==1&&w.InventoryChanges[0].Delta==30,"duplicate status cannot add gold twice");
        var add=LoginPackets.Pad([15,..Record(33)]);w.Receive(add,"");w.Receive(add,"");Check(w.Inventory.Single().Count==33&&w.InventoryChanges.Length==1,"duplicate add authoritative count");
        w.Reset();Check(w.Experience==null&&w.LastExperienceChange==null&&w.InventoryChanges.Length==0,"reset clears rewards");
        bool before=false;try{w.Receive(Convert.FromHexString(File.ReadAllLines(oracle)[0].Split('\t')[3]),"");}catch(ProtocolException){before=true;}Check(before,"EXP before status rejected");
        Json.Save(output,new{status="pass",kind="synthetic boundary inputs to original fixed JAR serializers; no real kill claim",checks});
    }
}
