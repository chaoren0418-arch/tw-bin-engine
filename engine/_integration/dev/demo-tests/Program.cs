using P06;
using System.Reflection;
using System.Text.Json;
using System.Buffers.Binary;
using System.Diagnostics.Tracing;

// Synthetic packets and memory-only transport, executing the built product DLL.
// Never calls the game entry point, creates a connection or shows a window.
class Checks {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static int checks;static List<string> labels=[];
    static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;labels.Add(label);}
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F)!.Invoke(o,args);
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] bytes)=>c.World.Receive(bytes,"offline");
    static byte[] Pack(uint id,ushort x=32600,ushort y=32940,byte action=0)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)(id==123?0:1110));b.Write(action);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);Text(b,id==123?"offline":"target");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static byte[] Move(uint id,ushort x,ushort y,byte h)=>Packet(b=>{b.Write((byte)10);b.Write(id);b.Write(x);b.Write(y);b.Write(h);b.Write((byte)128);});
    static LoginConnection Seed(){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        Feed(c,Pack(123));Feed(c,Pack(456,32601));c.World.SelectTarget(456);
        return c;
    }
    static void Sender(LoginConnection c,Func<byte[],Func<bool>,Action,Task<bool>> sender)=>Set(c,"sendMovementGate",sender);
    static async Task Combat(){
        foreach(bool pointer in new[]{false,true}){
            using var c=Seed();int sends=0;byte[]? sent=null;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;sent=b;a();return Task.FromResult(true);});
            var controller=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(controller.Select(456)&&controller.Notice.Contains("已選取"),"pointer selection feedback "+pointer);
            Check(pointer?await controller.ActAsync(456,false):await c.Attack(),"selected target attack admitted "+pointer);
            Check(sends==1&&sent![0]==229&&BinaryPrimitives.ReadUInt32LittleEndian(sent.AsSpan(1))==456,"one C229 target id "+pointer);
            Check(!await c.Attack()&&sends==1&&c.World.CombatNotice.Contains("冷卻"),"cooldown preserved "+pointer);
        }
        using(var c=Seed()){
            int sends=0;bool moved=false;
            Sender(c,(b,v,a)=>{if(!moved){moved=true;Feed(c,Move(456,32601,32940,2));}if(!v())return Task.FromResult(false);sends++;Check(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(5))==32602,"retry uses latest target x");a();return Task.FromResult(true);});
            Check(!await c.Attack()&&sends==0&&c.World.CombatNotice.Contains("目標已移動"),"moving target refusal is explicit");
            Check(await c.Attack()&&sends==1,"explicit manual retry succeeds without changing selection");
        }
        using(var c=Seed()){
            Sender(c,(_,_,_)=>throw new Exception("invalid target reached sender"));c.World.SelectTarget(null);
            Check(!await c.Attack()&&c.World.CombatNotice.Contains("未選取"),"no target message");
            Feed(c,Pack(456,32601,32940,8));c.World.SelectTarget(456);Check(!await c.Attack(),"dead target rejected");
        }
    }
    static async Task Magic(){
        using var c=Seed();var learned=new byte[32];learned[0]=164;learned[1]=32;learned[2]=8;Feed(c,learned);
        Check(c.World.SelectMagic(4),"select learned light arrow");c.World.SelectTarget(null);
        Check(!await c.CastSelectedMagic(),"light arrow requires selected enemy");
        Check(c.World.SelectMagicTarget(456)&&!c.World.EffectiveMagicTargetSelf,"explicit enemy selection");
        int sends=0;bool moved=false;
        Sender(c,(b,v,a)=>{if(!moved){moved=true;Feed(c,Move(456,32601,32940,2));}if(!v())return Task.FromResult(false);sends++;
            Check(b[0]==6&&b[1]==0&&b[2]==3,"C6 skill 4 encoding");Check(BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(3))==456,"C6 enemy object id");
            Check(BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(7))==32602,"C6 latest enemy x");a();return Task.FromResult(true);});
        Check(!await c.CastSelectedMagic()&&sends==0&&c.World.MagicNotice.Contains("目標已移動"),"spell moving target refusal");
        Check(await c.CastSelectedMagic()&&sends==1,"manual light arrow retry");
        Check(!await c.CastSelectedMagic()&&sends==1,"spell cooldown preserved");
        Check(c.World.Status!.Mp==100,"no predicted MP deduction");
        Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));Check(!c.World.SelectMagicTarget(456),"removed enemy cannot be selected");
    }
    static async Task Quantity(){
        using var c=Seed();var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");
        Feed(c,Packet(b=>{b.Write((byte)15);b.Write(900101u);b.Write(row.InventoryDesc);b.Write(row.WireUseType);b.Write((byte)0);b.Write(row.InventoryIcon);b.Write((byte)1);b.Write(10u);b.Write((byte)0);Text(b,row.CountName(10));b.Write((byte)0);b.Write(new byte[11]);}));
        c.World.SelectItem(900101);var selection=c.World.GroundQuantity(false);int revision=c.World.InventoryContentRevision;
        Sender(c,(_,_,_)=>throw new Exception("invalid quantity reached sender"));
        foreach(var text in new[]{"0","11","-1","2147483648",""}){
            Feed(c,LoginPackets.Pad([71,125,0,0]));Check(c.World.LastServerMessage!=null,"seed old 125 "+text);
            Check(!await c.SendGroundQuantity(false,text,selection),"invalid count blocked "+text);
            Check(c.World.LastServerMessage==null&&c.World.LastServerReply==null,"old 125 cleared "+text);
            Check(c.World.ItemNotice.Contains("數量不合法")&&c.World.GroundNotice==c.World.ItemNotice,"native inventory receives quantity error "+text);
            Check(c.World.InventoryContentRevision==revision&&c.World.CurrentGroundQuantity(false,selection),"error preserves item snapshot "+text);
        }
    }
    static void GroundPacket(LoginConnection c,uint count=10)=>Feed(c,Packet(b=>{
        var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(900102u);b.Write(row.GroundGfx);b.Write(new byte[4]);b.Write(count);b.Write((ushort)0);
        Text(b,row.CountName(count));Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)255);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    }));
    static async Task PickupCorrection(){
        foreach(bool pickupError in new[]{true,false})foreach(string invalid in new[]{"0","11"}){
            using var c=Seed();GroundPacket(c);Check(c.World.SelectGround(900102),"select ground before correction "+pickupError+invalid);
            var form=(ClientForm)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));GC.SuppressFinalize(form);Set(form,"login",c);
            Call(form,"SyncQuantitySelection",true);
            var snapshot=(GroundQuantitySelection)typeof(ClientForm).GetField("pickupSelection",F)!.GetValue(form)!;
            if(!pickupError){
                var row=GroundCatalog.Rows.Single(r=>r.Id=="meat");
                Feed(c,Packet(b=>{b.Write((byte)15);b.Write(900101u);b.Write(row.InventoryDesc);b.Write(row.WireUseType);b.Write((byte)0);b.Write(row.InventoryIcon);b.Write((byte)1);b.Write(10u);b.Write((byte)0);Text(b,row.CountName(10));b.Write((byte)0);b.Write(new byte[11]);}));
                c.World.SelectItem(900101);
            }
            var errorSelection=c.World.GroundQuantity(pickupError);
            int sends=0;byte[]? sent=null;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;sent=b;a();return Task.FromResult(true);});
            Feed(c,LoginPackets.Pad([71,125,0,0]));
            Check(!await c.SendGroundQuantity(pickupError,invalid,errorSelection)&&sends==0,"invalid quantity sends no packet "+pickupError+invalid);
            Check(c.World.GroundNotice.Contains("數量不合法")&&c.World.LastServerMessage==null&&c.World.LastServerReply==null,"quantity notice replaces old 125 "+pickupError+invalid);
            Call(form,"SyncQuantitySelection",true);
            var retained=(GroundQuantitySelection)typeof(ClientForm).GetField("pickupSelection",F)!.GetValue(form)!;
            Check(ReferenceEquals(snapshot,retained),"native UI retains displayed selection "+pickupError+invalid);
            Check(await c.SendGroundQuantity(true,"1",retained),"corrected pickup succeeds without reselection "+pickupError+invalid);
            Check(sends==1&&sent![0]==112&&BinaryPrimitives.ReadUInt32LittleEndian(sent.AsSpan(5))==900102&&BinaryPrimitives.ReadUInt32LittleEndian(sent.AsSpan(9))==1,"exactly one C112 with selected id and corrected count "+pickupError+invalid);
            Check(c.World.SelectedGround!.Count==10,"pickup waits for server count "+pickupError+invalid);
        }
        foreach(string change in new[]{"count","aba","remove","reselect","queued"}){
            using var c=Seed();GroundPacket(c);c.World.SelectGround(900102);var snapshot=c.World.GroundQuantity(true);int sends=0;
            Check(!await c.SendGroundQuantity(true,"0",snapshot),"seed quantity error before real change "+change);
            if(change=="count"||change=="aba")GroundPacket(c,9);
            if(change=="aba")GroundPacket(c,10);
            if(change=="remove")Feed(c,Packet(b=>{b.Write((byte)120);b.Write(900102u);}));
            if(change=="reselect")c.World.SelectGround(900102);
            Sender(c,(b,v,a)=>{if(change=="queued")GroundPacket(c,9);if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(!await c.SendGroundQuantity(true,"1",snapshot)&&sends==0,"real ground change still rejects stale pickup "+change);
        }
    }
    static void Position(){
        using var c=Seed();var w=c.World;long serial=w.AuthoritativePositionRevision;
        Check(w.AuthoritativeMapId==0&&w.AuthoritativeActor!.X==32600&&w.AuthoritativePositionOpcode==87,"S87 server position");
        Call(w,"Predict",32601,32940,2,true);
        Check(w.Actor!.X==32601&&w.AuthoritativeActor!.X==32600&&w.AuthoritativePositionRevision==serial&&w.PositionPredictedSinceAuthority,"local prediction cannot update authority");
        Check(w.AuthoritativePositionText.Contains("尚無位置回包"),"stale authority is explicit");
        Feed(c,Move(456,32601,32940,2));Check(w.AuthoritativePositionRevision==serial,"peer move cannot update own authority");
        Feed(c,Move(123,32600,32940,2));Check(w.AuthoritativeActor!.X==32601&&w.AuthoritativePositionOpcode==10&&w.AuthoritativePositionRevision==serial+1&&!w.PositionPredictedSinceAuthority,"own S10 updates authority");
        foreach(byte h in Enumerable.Range(0,8)){Feed(c,Move(123,32600,32940,h));Check(w.AuthoritativeActor!.X==32600+MovementRules.Dx[h]&&w.AuthoritativeActor.Y==32940+MovementRules.Dy[h],"server eight-direction coordinate "+h);}
        Feed(c,LoginPackets.Pad([206,4,0,0]));Check(w.AuthoritativeActor==null&&w.AuthoritativeMapId==null,"map transition clears old coordinates");
        Feed(c,Pack(123,32605,32942));Check(w.AuthoritativeMapId==4&&w.AuthoritativeActor!.X==32605,"map plus self pack binds new authority");
        w.Reset();Check(w.AuthoritativeActor==null&&w.AuthoritativePositionText.Contains("等待"),"logout clears authority");
    }
    static void NativeTargetUi(){
        using var c=Seed();
        // Only call the actual target-list sync method on a constructor-free host;
        // real ComboBox, synthetic pixel ownership; no Form/game construction or Show.
        var form=(ClientForm)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClientForm));
        using var list=new ComboBox();Set(form,"login",c);Set(form,"magicEnemyList",list);
        Set(form,"objectVisuals",Array.Empty<ObjectVisual>());Call(form,"SyncMagicEnemies");
        Check(list.Items.Count==1&&list.SelectedIndex==0,"native empty target list initializes safely");
        Set(form,"objectVisuals",new[]{new ObjectVisual(456,1110,1110,"target",32601,32940,"walk","0",new(),new(),true,640)});
        Set(form,"objectPatch",new Patch(new(0,0,1,1),new ushort[1],new[]{true},new[]{0},[],new uint?[]{456}));
        Call(form,"SyncMagicEnemies");Check(list.Items.Count==2&&list.SelectedIndex==1,"native list selects visible enemy object id");
        object option=list.Items[1]!;Call(form,"SyncMagicEnemies");Check(ReferenceEquals(option,list.Items[1]),"native unchanged list preserves options");
        Feed(c,Pack(456,32601,32940,8));Call(form,"SyncMagicEnemies");Check(list.Items.Count==1,"native dead target filtered");
        var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);Set(form,"playerPointer",pointer);
        pointer.Cancel("single-attack");Call(form,"SetSingleActionNotice","未送出：未選取有效目標");Call(form,"SyncPlayerPointerHud");
        Check((string)typeof(ClientForm).GetField("playerHudNotice",F)!.GetValue(form)! =="未送出：未選取有效目標","pointer cancellation cannot overwrite attack result");
        pointer.Cancel("single-magic");Call(form,"SetSingleActionNotice","單次施法已送出");Call(form,"SyncPlayerPointerHud");
        Check((string)typeof(ClientForm).GetField("playerHudNotice",F)!.GetValue(form)! =="單次施法已送出","pointer cancellation cannot overwrite cast result");
        GC.SuppressFinalize(form);
    }
    static async Task<int> Main(string[] args){try{
        AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY",Path.Combine(Path.GetFullPath(args[0]),"engine","build","offline-checks")+Path.DirectorySeparatorChar);
        using var guard=new SocketGuard();NativeTargetUi();await Combat();await Magic();await Quantity();await PickupCorrection();Position();
        File.WriteAllText(args[1],JsonSerializer.Serialize(new{status="pass",checks,scope="本次產品 DLL；合成封包與記憶體 sender；非真機",labels},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"PASS {checks} DEMO checks");return 0;
    }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    sealed class SocketGuard:EventListener {protected override void OnEventSourceCreated(EventSource source){if(source.Name=="System.Net.Sockets")EnableEvents(source,EventLevel.LogAlways);}protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("Network not allowed in offline regression");}
}
