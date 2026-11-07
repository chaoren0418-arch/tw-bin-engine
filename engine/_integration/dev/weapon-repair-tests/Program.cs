using System.Diagnostics.Tracing;
using System.Reflection;
using System.Text.Json;
using P06;

static class Checks {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static void Set(object o,string key,object? value){var p=o.GetType().GetProperty(key,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(key,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var stream=new MemoryStream();using var b=new BinaryWriter(stream);write(b);return LoginPackets.Pad(stream.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static byte[] List(uint type=200,params WeaponRepairRow[] rows)=>Packet(b=>{b.Write((byte)83);b.Write(type);b.Write((ushort)rows.Length);foreach(var row in rows){b.Write(row.ObjectId);b.Write(row.Damage);}});
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static void Message(LoginConnection c,ushort id,params string[] values)=>Feed(c,Packet(b=>{b.Write((byte)71);b.Write(id);b.Write((byte)values.Length);foreach(string value in values)Text(b,value);}));
    static void Item(LoginConnection c,uint id=901,byte use=1)=>Feed(c,Packet(b=>{b.Write((byte)15);b.Write(id);b.Write((ushort)0);b.Write(use);b.Write((byte)0);b.Write((ushort)0);b.Write((byte)1);b.Write(1u);b.Write((byte)0);Text(b,"test weapon");b.Write((byte)0);b.Write(new byte[11]);}));
    static void Sender(LoginConnection c,Func<byte[],Func<bool>,Action,Task<bool>> sender)=>Set(c,"sendMovementGate",sender);
    static LoginConnection Seed(List<byte[]> sent){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"CacheEnabled",false);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Feed(c,Packet(b=>{b.Write((byte)87);b.Write((ushort)32601);b.Write((ushort)32940);b.Write(456u);b.Write((ushort)1110);b.Write(new byte[4]);b.Write(0u);b.Write((ushort)0);Text(b,"NPC");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);}));
        c.World.SelectTarget(456);Feed(c,LoginPackets.Pad([5,0]));Item(c);
        Check(c.World.Target!=null,"synthetic NPC selected");
        Sender(c,(p,v,a)=>{if(!v())return Task.FromResult(false);a();sent.Add(p);return Task.FromResult(true);});return c;
    }
    static async Task Ready(LoginConnection c,params WeaponRepairRow[] rows){Check(await c.SendWeaponRepair(true),"query admitted");Feed(c,List(200,rows));}
    static void Reject(Action action,string label){try{action();}catch(ProtocolException){Check(true,label);return;}throw new Exception(label);}
    static async Task Formats(){
        Check(Convert.ToHexString(WeaponRepairPackets.List())=="7600000000000000","C118 has no conditional tail");
        Check(Convert.ToHexString(WeaponRepairPackets.CheckPk())=="3300000000000000","C51 has no conditional tail");
        Check(Convert.ToHexString(WeaponRepairPackets.Repair(0x12345678,0x90abcdef))=="1478563412EFCDAB9000000000000000","C20 item then NPC little endian, padded");
        foreach(var pair in new[]{(0u,1u),(1u,0u)}){try{WeaponRepairPackets.Repair(pair.Item1,pair.Item2);throw new Exception("zero accepted");}catch(ArgumentException){Check(true,"zero C20 identity rejected");}}
        var sent=new List<byte[]>();using var c=Seed(sent);
        Feed(c,List(200,new WeaponRepairRow(901,4)));Check(c.World.WeaponRepairRows.Length==0,"unsolicited list ignored");
        Check(await c.SendWeaponRepair(true),"start list");Feed(c,List(70,new WeaponRepairRow(901,9)));Check(c.World.WeaponRepairPhase=="querying","pet discriminator never consumed");
        Feed(c,List(999,new WeaponRepairRow(901,9)));Check(c.World.WeaponRepairPhase=="querying","unknown discriminator never consumed");
        Feed(c,List(200,new WeaponRepairRow(901,255),new WeaponRepairRow(902,0)));Check(c.World.WeaponRepairRows[0].Cost==51000&&c.World.WeaponRepairRows[1].Damage==0,"multiple rows and byte boundaries");
        int revision=c.World.WeaponRepairRevision;
        Check(!c.World.CanRepairWeapon(902,revision)&&!c.World.CanRepairWeapon(999,revision),"zero damage and absent row rejected");
        foreach(var bad in new[]{List(200,new WeaponRepairRow(0,1)),List(200,new WeaponRepairRow(901,1),new WeaponRepairRow(901,2)),List(200,new WeaponRepairRow(901,1))[..8],List(200,new WeaponRepairRow(901,1)).Concat(new byte[8]).ToArray()}){
            Reject(()=>Feed(c,bad),"malformed list rejected");Check(c.World.WeaponRepairRevision==revision&&c.World.WeaponRepairRows.Length==2,"malformed list atomic");
        }
        var padding=List(200,new WeaponRepairRow(901,1));padding[^1]=1;Reject(()=>Feed(c,padding),"nonzero padding rejected");
        var oversized=List();oversized[5]=1;oversized[6]=8;Reject(()=>Feed(c,oversized),"excessive count rejected");
        c.World.CloseWeaponRepair();await Ready(c);Check(c.World.WeaponRepairRows.Length==0&&c.World.WeaponRepairPhase=="ready","empty list supported");
        await Ready(c,new WeaponRepairRow(901,1));Check(c.World.WeaponRepairRows.Single().Damage==1,"single weapon overload supported");
    }
    static async Task Repair(){
        var sent=new List<byte[]>();using var c=Seed(sent);await Ready(c,new WeaponRepairRow(901,3));
        var before=c.World.Inventory;int revision=c.World.WeaponRepairRevision;
        Check(!await c.SendWeaponRepair(false,901,revision-1),"stale selection rejected");
        Check(await c.SendWeaponRepair(false,901,revision),"repair admitted");
        Check(sent.Last().SequenceEqual(WeaponRepairPackets.Repair(901,456)),"real send uses current object and NPC IDs");
        Check(c.World.Inventory.SequenceEqual(before)&&c.World.WeaponRepairRows.Single().Damage==3,"send does not mutate inventory or damage");
        Check(!await c.SendWeaponRepair(false,901,revision)&&!await c.SendWeaponRepair(true),"no duplicate submission or query while submitted");
        Feed(c,List(200,new WeaponRepairRow(901,5)));Check(c.World.WeaponRepairPhase=="submitted","late list cannot reopen order");
        Message(c,464,"test weapon");Message(c,403,"unrelated");Check(c.World.WeaponRepairPhase=="review"&&c.World.WeaponRepairRows.Length==0&&c.World.WeaponRepairNotice.Contains("修理結果"),"S464 retained across later message; list invalidated only");
        await Ready(c);Check(c.World.WeaponRepairRows.Length==0,"post repair refreshed authoritative list");
        Feed(c,Packet(b=>{b.Write((byte)24);b.Write(901u);Text(b,"test weapon");b.Write(1u);b.Write((byte)2);b.Write((byte)1);b.Write((byte)0);}));
        Check(c.World.Inventory.Single().StatusHex=="0100","existing S24 path remains authoritative for status blob");before=c.World.Inventory;
        await Ready(c,new WeaponRepairRow(901,2));Check(await c.SendWeaponRepair(false,901,c.World.WeaponRepairRevision),"second repair");Message(c,189);
        Check(c.World.WeaponRepairNotice.Contains("金幣不足")&&c.World.Inventory.SequenceEqual(before),"insufficient gold response no predicted debit");
        await Ready(c,new WeaponRepairRow(901,2));Item(c,902);Check(c.World.WeaponRepairPhase=="review","inventory mutation invalidates quote");
        await Ready(c,new WeaponRepairRow(901,2));c.World.SelectTarget(null);Check(!await c.SendWeaponRepair(false,901,c.World.WeaponRepairRevision),"target change invalidates quote");
        c.World.SelectTarget(456);long now=0;Set(c.World,"InteractionClock",(Func<long>)(()=>now));await Ready(c,new WeaponRepairRow(901,2));now=30000;
        Check(c.World.WeaponRepairPhase=="review","quote expires");
        Check(await c.SendWeaponRepair(true),"new query after timeout");now=60000;Check(c.World.WeaponRepairPhase=="review","query timeout");
        Feed(c,List(200,new WeaponRepairRow(901,2)));Check(c.World.WeaponRepairPhase=="review","late timed out reply ignored");
        await Ready(c,new WeaponRepairRow(901,2));c.World.Reset();Check(c.World.WeaponRepairRows.Length==0,"world reset drops rows lazily");
    }
    static async Task Fences(){
        foreach(string mutation in new[]{"cancel","target","close","inventory","dead","distance","map"}){
            using var c=Seed([]);await Ready(c,new WeaponRepairRow(901,2));int revision=c.World.WeaponRepairRevision;
            var release=new TaskCompletionSource();int writes=0;
            Sender(c,async(p,v,a)=>{await release.Task;if(!v())return false;a();writes++;return true;});
            var task=c.SendWeaponRepair(false,901,revision);Check(!await c.SendWeaponRepair(false,901,revision),"inflight duplicate rejected "+mutation);
            switch(mutation){case "cancel":c.Cancel();break;case "target":c.World.SelectTarget(null);break;case "close":c.World.CloseWeaponRepair();break;case "inventory":Item(c,902);break;case "dead":Set(c.World,"Status",c.World.Status! with{Hp=0});break;case "distance":Set(c.World,"Actor",c.World.Actor! with{X=32605});break;case "map":Feed(c,LoginPackets.Pad([206,4,0,0]));break;}
            release.SetResult();Check(!await task&&writes==0,"queued repair fence "+mutation);
        }
        using var query=Seed([]);var gate=new TaskCompletionSource();Sender(query,async(p,v,a)=>{await gate.Task;if(!v())return false;a();return true;});
        var pending=query.SendWeaponRepair(true);query.World.CloseWeaponRepair();gate.SetResult();Check(!await pending,"closed window rejects queued query");
        using var failed=Seed([]);Sender(failed,(p,v,a)=>throw new IOException("synthetic"));Check(!await failed.SendWeaponRepair(true)&&failed.State==LoginState.Faulted,"transport failure disconnects without retry");
    }
    static async Task Pk(){
        var sent=new List<byte[]>();using var c=Seed(sent);Message(c,562,"9");Check(c.CheckPkNotice.Contains("尚未"),"old unsolicited PK not treated as current");
        Check(await c.CheckPk()&&!await c.CheckPk()&&sent.Last().SequenceEqual(WeaponRepairPackets.CheckPk()),"C51 sent once through gate");
        Message(c,562,"bad");Check(c.CheckPkNotice.Contains("等待"),"invalid PK argument ignored");
        Message(c,562,"7");Message(c,403,"unrelated");Check(c.CheckPkNotice=="你的 PK 次數為 7 次。","S71 ID562 captured and displayed despite next message");
        Check(await c.CheckPk(),"repeat explicit PK query");Message(c,562,"0");Check(c.CheckPkNotice=="你的 PK 次數為 0 次。","zero PK count");
        long now=0;Set(c.World,"InteractionClock",(Func<long>)(()=>now));await c.CheckPk();now=30000;Message(c,562,"99");Check(c.CheckPkNotice.Contains("逾時"),"PK timeout rejects late response before UI poll");
        var previous=c.World.LastServerReply;Reject(()=>Feed(c,LoginPackets.Pad([71,50,2,1,65,65,65,65])),"truncated S71 string rejected");Check(ReferenceEquals(previous,c.World.LastServerReply),"malformed S71 does not publish partial PK");
        Check(WeaponRepairPackets.PkText(new(562,["1","2"],"",false))==null&&WeaponRepairPackets.PkText(new(562,["-1"],"",false))==null,"PK exact arity and unsigned count");
        c.Cancel();Check(c.CheckPkNotice.Contains("尚未")&&!await c.CheckPk(),"disconnect clears PK presentation and blocks send");
        using var queued=Seed([]);var gate=new TaskCompletionSource();Sender(queued,async(p,v,a)=>{await gate.Task;if(!v())return false;a();return true;});
        var task=queued.CheckPk();queued.Cancel();gate.SetResult();Check(!await task,"queued PK cancel fence");
    }
    static async Task<int> Main(){try{using var guard=new SocketGuard();await Formats();await Repair();await Fences();await Pk();Console.WriteLine($"PASS {checks} weapon repair checks; no game/network/DB");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    sealed class SocketGuard:EventListener {
        protected override void OnEventSourceCreated(EventSource source){if(source.Name=="System.Net.Sockets")EnableEvents(source,EventLevel.LogAlways);}
        protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("No sockets permitted");
    }
}
