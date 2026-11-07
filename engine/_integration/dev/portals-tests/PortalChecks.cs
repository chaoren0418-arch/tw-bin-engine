using P06;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

partial class Checks {
    static void PortalFixture(){
        string fixtureRoot=Path.GetFullPath("scratch/issue-377/synthetic-maps");
        mapFixture=Path.Combine(fixtureRoot,"assets");
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(fixtureRoot,"build","offline-checks")+Path.DirectorySeparatorChar);
        MakeMapFixture();
        string manifestPath=Path.Combine(mapFixture,"maps/manifest.json");
        var manifest=JsonNode.Parse(File.ReadAllText(manifestPath))!;
        foreach(var block in manifest["blocks"]!.AsArray()){
            string name=block!["name"]!.GetValue<string>();
            int bx=Convert.ToInt32(name[..4],16),by=Convert.ToInt32(name[4..8],16);
            int x=2*(32601-(bx-0x7e00)*64),y=32940-(by-0x7e00)*64;
            if(x is <0 or >=128||y is <0 or >=64)continue;
            string path=Path.Combine(mapFixture,"maps",block["path"]!.GetValue<string>());
            byte[] old=File.ReadAllBytes(path);
            // Replace zero portal count immediately before the unchanged effect tail.
            byte[] bytes=[..old[..^10],1,0,3,65,66,67,(byte)x,(byte)y,0,0,1,0,0,0,..old[^8..]];
            File.WriteAllBytes(path,bytes);block["sha256"]=Convert.ToHexString(SHA256.HashData(bytes));
        }
        File.WriteAllText(manifestPath,manifest.ToJsonString());
        string integrityPath=Path.Combine(mapFixture,"integrity.json");var integrity=JsonNode.Parse(File.ReadAllText(integrityPath))!;
        integrity["files"]!.AsArray().Single(p=>p!["path"]!.GetValue<string>()=="maps/manifest.json")!["sha256"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifestPath)));
        File.WriteAllText(integrityPath,integrity.ToJsonString());
    }
    static LoginConnection PortalSeed(ushort x=32600,ushort y=32940){
        var c=MapSeed(0,x,y);Set(c,"MovementClock",(Func<long>)(()=>mapNow));
        Set(c,"PortalDelay",(Func<int,Task>)(ms=>{mapNow+=ms;return Task.CompletedTask;}));return c;
    }
    static HashSet<(int,int)> PortalCells(byte[] bytes,bool seg)=>(HashSet<(int,int)>)typeof(LoginConnection).Assembly.GetType("P06.PortalCatalog")!.GetMethod("ReadCells",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[bytes,seg])!;
    static void PortalFormats(){
        Check(PortalPackets.Enter(0x1234,0xabcd).SequenceEqual(new byte[]{219,0x34,0x12,0xcd,0xab,0,0,0}),"C219 HH little-endian and zero padding");
        Check(PortalPackets.Enter(0,65535).SequenceEqual(new byte[]{219,0,0,255,255,0,0,0}),"C219 coordinate boundaries");
        foreach(var (x,y) in new[]{(-1,0),(65536,0),(0,-1),(0,65536)}){
            bool rejected=false;try{PortalPackets.Enter(x,y);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"C219 rejects out-of-range coordinate");
        }
        foreach(bool seg in new[]{false,true}){
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            writer.Write(new byte[8192*(seg?2:4)]);writer.Write((ushort)1);writer.Write(new byte[seg?4:6]);
            writer.Write(new byte[8192*(seg?1:2)]);writer.Write(1);writer.Write((ushort)7);writer.Write((ushort)1);writer.Write(new byte[seg?5:7]);
            writer.Write(1);writer.Write(new byte[5]);if(seg){writer.Write((byte)1);writer.Write((byte)0);}else{writer.Write(1);writer.Write(0);}
            writer.Write((ushort)2);long first=stream.Position;
            foreach(var (x,y) in new[]{(0,0),(126,63)}){writer.Write((byte)3);writer.Write(new byte[]{65,66,67});writer.Write((byte)x);writer.Write((byte)y);writer.Write((ushort)9);writer.Write(123);}
            byte[] bytes=stream.ToArray();Check(PortalCells(bytes,seg).SetEquals(new[]{(0,0),(126,63)}),"portal records after variable sections "+seg);
            foreach(int n in new[]{0,8192*(seg?2:4),bytes.Length-1,(int)first+4}){
                bool rejected=false;try{PortalCells(bytes[..n],seg);}catch(TargetInvocationException ex) when(ex.InnerException is IOException or InvalidDataException){rejected=true;}Check(rejected,"truncated map portal rejected "+seg+"/"+n);
            }
            bytes[(int)first+4]=128;bool bad=false;try{PortalCells(bytes,seg);}catch(TargetInvocationException ex) when(ex.InnerException is IOException or InvalidDataException){bad=true;}Check(bad,"portal local x bounds "+seg);
        }
    }
    static async Task PortalChecks(){
        PortalFormats();PortalFixture();
        for(int heading=0;heading<8;heading++){
            using var c=PortalSeed((ushort)(32601-MovementRules.Dx[heading]),(ushort)(32940-MovementRules.Dy[heading]));var sent=new List<byte[]>();bool delayed=false;
            Set(c,"PortalDelay",(Func<int,Task>)(ms=>{Check(ms>0&&c.MovementBusy&&sent[^1][0]==29,"arrival waits after C29, owns movement lease "+heading);delayed=true;mapNow+=ms;return Task.CompletedTask;}));
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
            Check(await c.Move(heading)&&delayed&&sent.Count==2&&sent[0][0]==29&&sent[1].SequenceEqual(PortalPackets.Enter(32601,32940)),"actual move sends exactly C29 then C219 "+heading);
            Check(c.World.MapId==0&&!c.World.TeleportPending,"request does not predict destination or lock teleport "+heading);
            mapNow+=10000;Check(await c.Turn(heading)&&sent.Count==3&&sent[^1][0]==225,"turn on portal never resends "+heading);
            mapNow+=10000;Check(await c.Move(2)&&sent.Count==4&&sent[^1][0]==29,"ordinary tile has no C219 despite attribute 3 "+heading);
            mapNow+=10000;Check(await c.Move(6)&&sent.Count==6&&sent[^1][0]==219,"leave and reenter retries once "+heading);
        }
        foreach(string change in new[]{"correction","teleport","map","cancel","dead","trade"})foreach(bool atGate in new[]{false,true}){
            using var c=PortalSeed();int portals=0;
            void Change(){switch(change){case "correction":Feed(c,Pack(123,32601,32940));break;case "teleport":Feed(c,TeleportPacket());break;case "map":Arrive(c,1,32601,32940);break;case "cancel":c.Cancel();break;case "dead":Set(c.World,"OwnDead",true);break;case "trade":Set(c.World,"TradeState","open");break;}}
            Set(c,"PortalDelay",(Func<int,Task>)(_=>{if(!atGate)Change();return Task.CompletedTask;}));
            Sender(c,(b,v,a)=>{if(b[0]==219&&atGate)Change();if(!v())return Task.FromResult(false);if(b[0]==219)portals++;a();return Task.FromResult(true);});
            await c.Move(2);Check(portals==0,"stale arrival sends no C219 "+change+" gate="+atGate);
        }
        using(var c=PortalSeed()){
            var sent=new List<byte[]>();var arrival=new TaskCompletionSource();Set(c,"PortalDelay",(Func<int,Task>)(_=>arrival.Task));
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
            var move=c.Move(2);Check(!move.IsCompleted&&!await c.Move(2)&&sent.Count==1,"second movement blocked while portal arrival waits");
            arrival.SetResult();await move;Feed(c,TeleportPacket());await (Task)Invoke(c,"AcknowledgeTeleport")!;
            Check(sent.Select(b=>b[0]).SequenceEqual(new byte[]{29,219,52}),"C219 reuses existing S149 / C52 handshake");
            Arrive(c,1,32669,32802);Check(c.World.TeleportState=="arrived","existing S206 / S87 chooses authoritative destination");
        }
        using(var c=PortalSeed()){
            int sent=0;Sender(c,(_,_,_)=>{sent++;return Task.FromResult(false);});
            Check(!await c.Move(2)&&sent==1,"rejected C29 cannot schedule portal");
        }
        // Source changes after cache load must never authorize a portal.
        using(var c=PortalSeed()){
            string path=Path.Combine(mapFixture,"maps/manifest.json");byte[] original=File.ReadAllBytes(path);File.WriteAllText(path,"{}");
            try{var sent=new List<byte>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b[0]);a();return Task.FromResult(true);});await c.Move(2);Check(sent.SequenceEqual(new byte[]{29})&&c.World.MotionNotice.Contains("無法核驗"),"manifest hash drift fails closed without disconnect");}
            finally{File.WriteAllBytes(path,original);}
        }
    }
}
