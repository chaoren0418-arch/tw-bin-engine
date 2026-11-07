using P06;
using System.Diagnostics.Tracing;
using System.Reflection;
using System.Text.Json;

// Synthetic wire vectors against the actual built DLL; never starts the product.
static class Checks {
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
    static void Reject(Action action,string label){try{action();}catch(ProtocolException){checks++;return;}throw new Exception("accepted: "+label);}
    static byte[] Pad(byte[] fields){var padded=new byte[(fields.Length+7)&~7];fields.CopyTo(padded,0);return padded;}
    // Independent model of prepareForWrite -> writeByte (appends 00) -> getBytes.
    static byte[] Wrap(byte[] original)=>Pad([..original,0]);
    static void Finish(byte[] bytes,int consumed){var r=new LoginReader(bytes);for(int i=0;i<consumed;i++)r.C();r.Finish();Check(r.Remaining==0,"fully consumed");}
    static byte[] Own(string name,string title="",string clan=""){
        using var s=new MemoryStream();using var w=new BinaryWriter(s);
        w.Write((byte)87);w.Write((ushort)32600);w.Write((ushort)32940);w.Write(123u);
        w.Write((ushort)0);w.Write(new byte[4]);w.Write(0u);w.Write((ushort)0);
        w.Write(System.Text.Encoding.ASCII.GetBytes(name));w.Write((byte)0);
        w.Write(System.Text.Encoding.ASCII.GetBytes(title));w.Write((byte)0);
        w.Write((byte)4);w.Write(0u);
        w.Write(System.Text.Encoding.ASCII.GetBytes(clan));w.Write((byte)0);w.Write((byte)0);
        w.Write(new byte[]{176,255,0,0,0,255,255});return s.ToArray();
    }
    static void Vectors(){
        Check(Convert.ToHexString(Pad([202,7]))=="CA07000000000000","literal original vector");
        Check(Convert.ToHexString(Wrap(Pad([202,7])))=="CA070000000000000000000000000000","literal N+8 vector");
        List<byte[]> vectors=[[202,7],[206,4,0,0],[206,4,0,1],[119,123,0,0,0,4]];
        for(int n=0;n<8;n++)vectors.Add(Own("offline"+new string('x',n)));
        vectors.Add(Own("offline","test title","test clan"));
        foreach(var fields in vectors){
            byte[] original=Pad(fields),wrapped=Wrap(original);
            Check(wrapped.Length==original.Length+8&&wrapped.AsSpan(0,original.Length).SequenceEqual(original),"wrapper length/prefix");
            Finish(original,fields.Length);Finish(wrapped,fields.Length);
            for(int n=0;n<=original.Length+24;n++){
                if(n==original.Length||n==wrapped.Length)continue;
                byte[] invalid=new byte[n];fields.AsSpan(0,Math.Min(n,fields.Length)).CopyTo(invalid);
                Reject(()=>Finish(invalid,fields.Length),"truncated or overlong packet");
            }
            // Reject corruption in both the original padding and every wrapper byte.
            for(int i=fields.Length;i<wrapped.Length;i++){
                byte[] invalid=(byte[])wrapped.Clone();invalid[i]=1;
                Reject(()=>Finish(invalid,fields.Length),"nonzero tail");
            }
            for(int i=fields.Length;i<original.Length;i++){
                byte[] invalid=(byte[])original.Clone();invalid[i]=1;
                Reject(()=>Finish(invalid,fields.Length),"nonzero original padding");
            }
            Reject(()=>Finish(Wrap(wrapped),fields.Length),"double wrapper");
        }
        for(int opcode=0;opcode<256;opcode++){
            if(opcode is 202 or 206 or 119 or 87)continue;
            foreach(int length in new[]{2,4,6,8,9,16}){
                byte[] fields=new byte[length];fields[0]=(byte)opcode;
                Finish(Pad(fields),length);
                Reject(()=>Finish(Wrap(Pad(fields)),length),"unrelated opcode");
            }
        }
        for(int mode=0;mode<256;mode++)if(mode!=7)Reject(()=>Finish(Wrap(Pad([202,(byte)mode])),2),"other paralysis mode");
        Reject(()=>Finish(Wrap(Pad([206,4,0,2])),4),"non-boolean map variant");
        foreach(var fields in new byte[][]{[202],[202,7,0],[206,4,0],[206,4,0,0,0],[119,123,0,0,0],[119,123,0,0,0,0,0]})
            Reject(()=>Finish(Wrap(Pad(fields)),fields.Length),"wrong field boundary");
        var own=Own("offline");
        foreach(int at in new[]{12,30,own.Length-4,own.Length-3,own.Length-2,own.Length-1}){
            var invalid=(byte[])own.Clone();invalid[at]=at==12?(byte)8:(byte)2;
            Reject(()=>Finish(Wrap(Pad(invalid)),invalid.Length),"non-own S87 shape");
        }
        var owner=Own("offline").ToList();owner.Insert(owner.Count-8,(byte)'x');
        Reject(()=>Finish(Wrap(Pad(owner.ToArray())),owner.Count),"nonempty owner");
        var cut=Own("offline");cut[28]=1;cut[29]=1;
        Reject(()=>Finish(Wrap(Pad(cut)),cut.Length),"wrong string boundaries");
    }
    static void Flow(bool wrapped){
        var world=new WorldSession{CacheEnabled=false};
        typeof(WorldSession).GetProperty("Status")!.SetValue(world,JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123}"));
        void Feed(byte[] fields)=>world.Receive(wrapped?Wrap(Pad(fields)):Pad(fields),"offline");
        world.Receive(Pad([149,235,167,1,0,0,0]),"offline");
        Check(world.TeleportAckPending&&world.ActionLocked,"teleport started");
        Feed([202,7]);Check(world.TeleportState=="recovery"&&world.ActionLocked,"unlock waits for world");
        Feed([206,0,0,0]);Check(world.MapId==0&&world.Actor==null&&world.TeleportState=="awaiting-own"&&world.ActionLocked,"map waits for own");
        Feed(Own("offline"));Check(world.Actor?.ObjectId==123&&world.TeleportState=="arrived"&&!world.ActionLocked,"own completes recovery");
        Feed([119,123,0,0,0,4]);Check(world.Actor?.Weapon==4,"visual update applied");
        // Malformed wrapper must fail before state mutation in the existing handlers.
        int generation=world.TeleportGeneration;byte[] bad=Wrap(Pad([206,4,0,0]));bad[^1]=1;
        Reject(()=>world.Receive(bad,"offline"),"corrupt map wrapper");
        Check(world.MapId==0&&world.Actor?.Weapon==4&&world.TeleportGeneration==generation,"rejection preserves world");
        Reject(()=>world.Receive(Wrap(Pad([149,235,167,1,0,0,0])),"offline"),"S149 is not wrapped by server");
        Reject(()=>world.Receive(Wrap(Pad([202,6])),"offline"),"lock mode is not recovery");
        world.Reset();world.CacheEnabled=false;
        typeof(WorldSession).GetProperty("Status")!.SetValue(world,JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123}"));
        Feed([202,7]);Check(world.TeleportState=="idle","unlock accepted without prior S149");
        Feed([206,0,0,0]);
        byte[] other=Own("other");other[5]=124;Feed(other);
        Check(world.Actor==null&&world.TeleportState=="awaiting-own"&&world.Objects.Single().ObjectId==124,"matching wire shape cannot impersonate own identity");
        Feed(Own("offline"));Check(world.TeleportState=="arrived"&&!world.ActionLocked,"early abort recovers without S149");
    }
    static int Main(string[] args){try{
        using var guard=new SocketGuard();Vectors();Flow(false);Flow(true);
        if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{status="pass",checks,scope="產品 DLL、合成原包與 N+8 包；無遊戲、網路或 DB"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {checks} teleport recovery checks");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    sealed class SocketGuard:EventListener {
        protected override void OnEventSourceCreated(EventSource source){if(source.Name=="System.Net.Sockets")EnableEvents(source,EventLevel.LogAlways);}
        protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("Network not allowed in offline checks");
    }
}
