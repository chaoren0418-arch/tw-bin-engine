using System.Text.Json;
using System.Buffers.Binary;
using System.Security.Cryptography;
namespace P06;
public sealed partial class LoginConnection {
    internal static async Task InventoryRegression(string output){
        var checks=new List<string>();using var c=new LoginConnection();
        var rows=Json.Read(Path.Combine(AppContext.BaseDirectory,"../../fixtures/combat-initialization.json")).GetProperty("rows").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();string selected=CapturedCharacterName(rows);
        byte[] Record(uint id=100,uint count=6,byte use=249)=>[..BitConverter.GetBytes(id),130,0,use,0,81,0,1,..BitConverter.GetBytes(count),1,..LoginPackets.TextEncoding.GetBytes("$238 ("+count+")"),0,2,23,1,10,0,0,0,0,0,0,0,0,0,0];
        byte[] List(params byte[][] records)=>LoginPackets.Pad([5,(byte)records.Length,..records.SelectMany(x=>x)]);
        byte[] Status(uint id,uint count,string name="$238")=>LoginPackets.Pad([24,..BitConverter.GetBytes(id),..LoginPackets.TextEncoding.GetBytes(name),0,..BitConverter.GetBytes(count),1,99]);
        void Feed(byte[] b)=>c.World.Receive(b,selected);
        void Seed(){c.Cancel();bool world=false;foreach(var p in rows){if(p[0]==223)world=true;if(world){c.World.ProbeSent=true;Feed(p);}}Feed(LoginPackets.Pad([123,1,0,0,0]));c.State=LoginState.InWorld;Feed(List(Record()));c.World.SelectItem(100);}
        Seed();if(!c.ItemAvailable||c.World.Inventory.Single().DescriptionId!=130)throw new Exception("inventory decode");
        Feed(Status(100,5));if(c.World.SelectedItem?.Count!=5||c.World.SelectedItem.StatusHex!="63")throw new Exception("status update");
        Feed(LoginPackets.Pad([100,..BitConverter.GetBytes(100u),..LoginPackets.TextEncoding.GetBytes("$238"),0]));if(c.World.SelectedItem!.Name!="$238")throw new Exception("name update");Feed(LoginPackets.Pad([240,..BitConverter.GetBytes(100u),129]));if(c.World.SelectedItem.SupportedUse)throw new Exception("sealed color remains usable");Feed(LoginPackets.Pad([240,..BitConverter.GetBytes(100u),1]));Feed(Status(999,55));if(c.World.Inventory.Length!=1||c.World.UnknownItemUpdates!=1)throw new Exception("unknown update ghost");
        Feed(LoginPackets.Pad([15,..Record(101,2,14)]));if(c.World.Inventory.Length!=2)throw new Exception("add");c.World.SelectItem(101);if(c.ItemAvailable||await c.UseSelectedItem())throw new Exception("unsupported type sent");
        Feed(List(Record(102,1)));if(c.World.Inventory.Length!=1||c.World.SelectedItemId!=null)throw new Exception("replacement list ghost");Feed(List(Record(102,1)));if(c.World.Inventory.Length!=1)throw new Exception("duplicate list");c.World.SelectItem(102);Feed(LoginPackets.Pad([57,..BitConverter.GetBytes(102u)]));Feed(Status(102,4));if(c.World.Inventory.Length!=0||c.World.SelectedItemId!=null)throw new Exception("delete/late update ghost");checks.Add("original packet layouts: initial/add/status/name/color/delete; unknown update ignored; replacement/duplicate list authoritative; depleted selection cleared");
        Seed();foreach(var b in new[]{List(Record())[..12],Status(100,2)[..6],LoginPackets.Pad([57,1,0,0,0])[..3]}){string before=JsonSerializer.Serialize(c.World.Inventory);try{Feed(b);throw new Exception("truncated accepted");}catch(ProtocolException){}if(JsonSerializer.Serialize(c.World.Inventory)!=before)throw new Exception("partial inventory mutation");}checks.Add("truncated inventory packets fail before mutation");
        byte[] longBytes=Enumerable.Range(0,300).Select(n=>(byte)(n%251)).ToArray();byte[] longPacket=LoginPackets.Pad([242,1,..BitConverter.GetBytes(100u),..LoginPackets.TextEncoding.GetBytes("$238"),0,..BitConverter.GetBytes(4u),44,1,..longBytes]);Feed(longPacket);if(c.World.SelectedItem!.Count!=4||c.World.SelectedItem.StatusHex!=Convert.ToHexString(longBytes))throw new Exception("long status decode");string longBefore=JsonSerializer.Serialize(c.World.Inventory);try{Feed(longPacket[..^8]);throw new Exception("truncated long status accepted");}catch(ProtocolException){}if(JsonSerializer.Serialize(c.World.Inventory)!=longBefore)throw new Exception("partial long status mutation");checks.Add("242/sub1 H-length 300-byte status preserved; truncation leaves full inventory unchanged");

        Seed();int admitted=0;c.sendMovementGate=(b,v,a)=>{if(!v())return Task.FromResult(false);admitted++;a();return Task.FromResult(true);};c.nextCombatAt=Environment.TickCount64+2000;if(await c.UseSelectedItem()||admitted!=0)throw new Exception("combat busy entry queued");c.nextCombatAt=0;c.nextMovementAt=Environment.TickCount64+2000;if(await c.UseSelectedItem()||admitted!=0)throw new Exception("movement busy entry queued");checks.Add("combat/movement busy rejected at entry before send gate");
        foreach(string change in new[]{"cancel-input","selection-roundtrip","count-change","remove","map","own-death","disconnect-replace"}){
            Seed();admitted=0;var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);c.sendMovementGate=async(b,v,a)=>{await gate.Task;if(!v())return false;admitted++;a();return true;};var task=c.UseSelectedItem();uint hp=c.World.Status!.Hp;if(c.World.SelectedItem!.Count!=6||c.nextItemAt!=0)throw new Exception("predicted consumption");
            switch(change){case "cancel-input":c.CancelMovementInput();break;case "selection-roundtrip":c.World.SelectItem(null);c.World.SelectItem(100);break;case "count-change":Feed(Status(100,5));break;case "remove":Feed(LoginPackets.Pad([57,..BitConverter.GetBytes(100u)]));break;case "map":Feed(Convert.FromHexString("CE00000000000000"));break;case "own-death":Feed(LoginPackets.Pad([158,..BitConverter.GetBytes(c.World.Actor!.ObjectId),8]));break;case "disconnect-replace":Seed();break;}
            gate.SetResult();if(await task||admitted!=0)throw new Exception("stale delayed item admitted "+change);checks.Add("delayed item gate drops "+change);
        }
        Seed();c.sendMovementGate=(b,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);};uint beforeHp=c.World.Status!.Hp;if(!await c.UseSelectedItem()||await c.UseSelectedItem()||c.World.SelectedItem!.Count!=6||c.World.Status.Hp!=beforeHp)throw new Exception("single use semantics");c.Cancel();if(c.World.Inventory.Length!=0||c.World.SelectedItemId!=null||c.ItemBusy)throw new Exception("cancel retained inventory");checks.Add("one explicit use; rapid repeat rejected; no local consumption or HP prediction; cancel clears inventory/selection/cooldown");
        Json.Save(output,new{status="pass",kind="explicit synthetic inventory packets and delayed gate; original captured P16 world prefix; not real P24 inventory proof",checks});
    }
}
public static class InventoryIconOracle {
    public static void Packets(string input,string output){
        var model=new WorldSession();var snapshots=new List<object>();foreach(string line in File.ReadLines(input)){var parts=line.Split('\t');model.Receive(Convert.FromHexString(parts[1]),"");snapshots.Add(new{packet=parts[0],plainHex=parts[1],inventory=model.Inventory});}
        if(model.Inventory.Length!=1||model.Inventory[0].StatusHex.Length!=600||model.Inventory[0].Count!=3)throw new Exception("original serializer replay");
        Json.Save(output,new{status="pass",kind="fixed original JAR packet serializers with explicit synthetic item subclass, no original game session or DB",snapshots});
    }
    // Independent BinaryReader port of original helper LoadL1Image, retaining byte-skip /2 and RGB555 expansion.
    public static void Run(string output){
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var checks=new List<object>();
        foreach(var icon in Json.Read(Path.Combine(root,"reference/inventory/icons/manifest.json")).GetProperty("icons").EnumerateArray()){
            int id=icon.I("icon");byte[] raw=File.ReadAllBytes(Path.Combine(root,$"reference/inventory/icons/{id}.tbt"));using var reader=new BinaryReader(new MemoryStream(raw));int ox=reader.ReadByte(),oy=reader.ReadByte(),width=reader.ReadByte(),height=reader.ReadByte();ushort[] pixels=new ushort[width*height];
            for(int y=0;y<height;y++){int segments=reader.ReadByte(),x=0;for(int s=0;s<segments;s++){int skip=reader.ReadByte();if(skip%2!=0)throw new Exception("odd byte skip");x+=skip/2;int count=reader.ReadByte();for(int p=0;p<count;p++){if(x>=width)throw new Exception("overflow");pixels[y*width+x++]=reader.ReadUInt16();}}}
            if(reader.BaseStream.Position!=raw.Length)throw new Exception("trailing icon bytes");byte[] rgba=new byte[width*height*4];for(int n=0;n<pixels.Length;n++){int v=pixels[n];if(v==0)continue;for(int channel=0;channel<3;channel++){int c=(v>>(10-channel*5))&31;rgba[n*4+channel]=(byte)((c<<3)|(c>>2));}rgba[n*4+3]=255;}
            string hash=Convert.ToHexString(SHA256.HashData(rgba)).ToLowerInvariant();if(hash!=icon.S("rgbaSha256"))throw new Exception("independent RGBA mismatch");using var png=new Bitmap(Path.Combine(root,$"assets/inventory/{id}.png"));if(png.Width!=width||png.Height!=height)throw new Exception("PNG size");for(int y=0;y<height;y++)for(int x=0;x<width;x++){var c=png.GetPixel(x,y);int pos=(y*width+x)*4;if(c.R!=rgba[pos]||c.G!=rgba[pos+1]||c.B!=rgba[pos+2]||c.A!=rgba[pos+3])throw new Exception("PNG pixels");}
            checks.Add(new{id,ox,oy,width,height,pixels=pixels.Length,rgbaSha256=hash});
        }
        Json.Save(output,new{status="pass",method="independent C# BinaryReader port of original helper LoadL1Image compared with Python extraction and every PNG pixel",checks});
    }
}
