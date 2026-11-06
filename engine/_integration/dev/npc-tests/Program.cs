using P06;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics.Tracing;
using System.Buffers.Binary;

// Runs production packet decoders/state machines with synthetic packets and an in-memory sender.
// No game entry point, socket, UI, server, DB or game resources.
partial class Checks {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static readonly List<string> passed=[];
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);passed.Add(label);}
    static void Set(object o,string key,object? value){var p=o.GetType().GetProperty(key,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(key,F)!.SetValue(o,value);}
    static byte[] Packet(Action<BinaryWriter> write){using var s=new MemoryStream();using var b=new BinaryWriter(s);write(b);return LoginPackets.Pad(s.ToArray());}
    static void Text(BinaryWriter b,string s){b.Write(LoginPackets.TextEncoding.GetBytes(s));b.Write((byte)0);}
    static void Feed(LoginConnection c,byte[] p)=>c.World.Receive(p,"offline");
    static uint D(byte[] p,int n)=>BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(n));
    static byte[] Object(uint id,ushort x=32601,byte heading=6,byte action=0,byte flags=4)=>Packet(b=>{
        b.Write((byte)87);b.Write(x);b.Write((ushort)32940);b.Write(id);b.Write((ushort)1110);b.Write(action);b.Write(heading);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);Text(b,"peer");Text(b,"");b.Write(flags);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    });
    static LoginConnection Seed(List<byte[]> sent,bool shop=false){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Hp\":100,\"MaxHp\":100}"));
        Set(c.World,"Actor",JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Direction\":2,\"Name\":\"offline\"}"));
        Feed(c,Object(456,action:shop?(byte)70:(byte)0));c.World.SelectTarget(456);Feed(c,LoginPackets.Pad([5,0]));
        Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)((p,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(p);a();return Task.FromResult(true);}));return c;
    }
    static Task<bool> Npc(LoginConnection c,string cmd,int option=0,params ShopChoice[] rows)=>c.Interact(cmd,c.World.InteractionRevision,c.World.WorldEpoch,c.World.TargetRevision,option,rows);
    static Task<bool> Trade(LoginConnection c,string cmd,uint item=0,uint count=0)=>c.Trade(cmd,c.World.TradeRevision,c.World.WorldEpoch,c.World.TradeGeneration,item,count);
    static byte[] Shop(uint npc=456,byte op=70)=>Packet(b=>{b.Write(op);b.Write(npc);b.Write((ushort)1);b.Write(op==70?7u:901u);if(op==70)b.Write((ushort)124);b.Write(2u);if(op==70){Text(b,"$23");b.Write((byte)0);b.Write((ushort)7);}});
    static byte[] Dialog()=>Packet(b=>{b.Write((byte)39);b.Write(456u);Text(b,"synthetic");b.Write((ushort)0);b.Write((ushort)0);});
    static void Page(LoginConnection c,params string[] actions)=>Set(c.World,"InteractionPage",new NpcPage("合成對話",actions.Select((s,i)=>new NpcOption(i,s,"action",s,true)).ToArray(),""));
    static void Item(LoginConnection c,uint id,uint count,bool gold=false)=>Feed(c,Packet(b=>{
        var r=GroundCatalog.Rows.Single(x=>x.Id==(gold?"gold":"meat"));b.Write((byte)15);b.Write(id);b.Write(r.InventoryDesc);b.Write(r.WireUseType);b.Write((byte)0);b.Write(r.InventoryIcon);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,r.CountName(count));b.Write((byte)0);b.Write(new byte[11]);
    }));
    static void Count(LoginConnection c,uint id,uint count)=>Feed(c,Packet(b=>{b.Write((byte)24);b.Write(id);Text(b,"updated");b.Write(count);b.Write((byte)0);}));
    static uint Held(LoginConnection c,uint id)=>c.World.Inventory.FirstOrDefault(i=>i.ObjectId==id)?.Count??0;
    static async Task OpenShop(LoginConnection c,string action="buy"){
        Check(await Npc(c,"talk"),"C34 talk admitted");Feed(c,Dialog());Page(c,action);Check(await Npc(c,"option"),"C125 "+action);Feed(c,Shop(op:action=="buy"?(byte)70:(byte)65));
    }
    static async Task Purchase(){
        var sent=new List<byte[]>();using var c=Seed(sent);Item(c,900,100,true);await OpenShop(c);
        Check(c.World.CanSubmitShop&&c.World.ShopRows[0].Id==7,"S70 uses returned order id");
        Check(!await Npc(c,"shop",0,new ShopChoice(7,0))&&!await Npc(c,"shop",0,new ShopChoice(7,uint.MaxValue))&&!await Npc(c,"shop",0,new ShopChoice(40056,1)),"invalid NPC orders rejected");
        Check(await Npc(c,"shop",0,new ShopChoice(7,10)),"NPC purchase admitted");var p=sent.Last();Check(p[0]==161&&D(p,1)==456&&p[5]==0&&p[6]==1&&p[7]==0&&D(p,8)==7&&D(p,12)==10,"C161 purchase wire");
        Check(Held(c,900)==100&&Held(c,901)==0,"purchase does not predict inventory");Check(!await Npc(c,"shop",0,new ShopChoice(7,10)),"double submission rejected");
        Count(c,900,80);Item(c,901,10);Check(Held(c,900)==80&&Held(c,901)==10,"S24 gold and S15 meat settle independently");
        Check(await Npc(c,"close"),"close purchase result");await OpenShop(c,"sell");Check(await Npc(c,"shop",0,new ShopChoice(901,1))&&sent.Last()[5]==1&&D(sent.Last(),8)==901,"sell uses inventory object id");
        Count(c,901,9);Count(c,900,82);Check(Held(c,901)==9&&Held(c,900)==82,"sale authority updates");
    }
    static async Task Warehouse(){
        var sent=new List<byte[]>();using var c=Seed(sent);Item(c,900,100,true);Item(c,901,10);
        Check(await Npc(c,"talk"),"warehouse talk");Feed(c,Dialog());Page(c,"deposit","retrieve");int before=sent.Count;
        Check(await Npc(c,"option",0)&&sent.Count==before&&c.World.InteractionKind=="deposit","deposit opens from inventory without request");
        Check(!await Npc(c,"shop",0,new ShopChoice(901,11)),"deposit excess rejected");Check(await Npc(c,"shop",0,new ShopChoice(901,10)),"deposit send");Check(sent.Last()[5]==2&&D(sent.Last(),8)==901&&D(sent.Last(),12)==10,"C161 deposit identity/count");
        Check(Held(c,901)==10,"deposit waits for server");Feed(c,LoginPackets.Pad([57,..BitConverter.GetBytes(901u)]));Check(Held(c,901)==0,"S57 deposited item removed");
        await Npc(c,"close");await Npc(c,"talk");Feed(c,Dialog());Page(c,"retrieve");Check(await Npc(c,"option"),"retrieve query admitted");
        byte[] list=Packet(b=>{b.Write((byte)176);b.Write(456u);b.Write((ushort)1);b.Write((byte)3);b.Write(990u);b.Write((byte)7);b.Write((ushort)124);b.Write((byte)1);b.Write(10u);b.Write((byte)1);Text(b,"$23 (10)");b.Write(30u);});Feed(c,list);
        Check(c.World.ShopRows.Single() is {Id:990,Available:10,Price:30},"S176 object id count fee");Check(!await Npc(c,"shop",0,new ShopChoice(990,11)),"withdraw excess rejected");Check(await Npc(c,"shop",0,new ShopChoice(990,10)),"withdraw send");
        Check(sent.Last()[5]==3&&D(sent.Last(),8)==990,"withdraw uses warehouse object id");Item(c,902,10);Count(c,900,70);Check(Held(c,902)==10&&Held(c,900)==70,"withdraw meat restored and fee paid once per row");
        Feed(c,list);Check(c.World.InteractionKind=="submitted","late warehouse list does not reopen");
    }
    static byte[] PrivateList(uint owner=456)=>Packet(b=>{b.Write((byte)140);b.Write((byte)0);b.Write(owner);b.Write((ushort)3);b.Write((byte)2);b.Write((byte)1);b.Write((ushort)124);b.Write(10u);b.Write(3u);Text(b,"$23 (10)");b.Write((byte)0);});
    static async Task Private(){
        var sent=new List<byte[]>();using var c=Seed(sent,true);Item(c,900,100,true);
        Check(await c.SendPrivateShop(true,false,0,0,c.PrivateShopSnapshot())==PrivateShopResult.Accepted,"private query");Check(sent.Last()[0]==47&&sent.Last()[1]==0&&D(sent.Last(),2)==456,"C47 owner");
        Feed(c,PrivateList(789));Check(c.World.PrivateShopPhase=="querying","wrong private owner ignored");Feed(c,PrivateList());var row=c.World.PrivateShopRows.Single();Check(row.Order==2&&row.Maximum==10,"S140 preserves sparse order");
        Check(c.ChoosePrivateShop(row.Id,1,c.PrivateShopSnapshot()),"private choose");Check(await c.SendPrivateShop(false,false,row.Id,11,c.PrivateShopSnapshot())==PrivateShopResult.NotSent,"private excess rejected");
        Check(await c.SendPrivateShop(false,false,row.Id,1,c.PrivateShopSnapshot())==PrivateShopResult.Accepted,"private purchase");Check(sent.Last()[0]==161&&sent.Last()[5]==0&&D(sent.Last(),8)==2&&D(sent.Last(),12)==1,"private C161 uses sparse order");
        Check(Held(c,900)==100,"private does not predict money");Item(c,901,1);Count(c,900,97);Check(Held(c,901)==1&&Held(c,900)==97&&c.World.PrivateShopPhase=="review","private authority settles");Feed(c,PrivateList());Check(c.World.PrivateShopPhase=="review","private late list ignored");
    }
    static byte[] OpenTrade()=>Packet(b=>{b.Write((byte)52);Text(b,"peer");});
    static byte[] Offer(byte side)=>Packet(b=>{b.Write((byte)35);b.Write(side);b.Write((ushort)124);Text(b,"$23");b.Write((byte)1);});
    static async Task DirectTrade(){
        var sa=new List<byte[]>();var sb=new List<byte[]>();using var a=Seed(sa);using var b=Seed(sb);Item(a,901,1);
        Check(await Trade(a,"invite")&&sa.Last()[0]==2,"C2 face to face invite");
        Feed(b,Packet(w=>{w.Write((byte)219);w.Write((ushort)0);w.Write(99u);w.Write((ushort)252);Text(w,"peer");}));
        Check(await Trade(b,"accept"),"recipient accepts S219 invitation");Check(sb.Last()[0]==121&&D(sb.Last(),3)==99&&sb.Last()[7]==252&&sb.Last()[9]==1,"C121 challenge preserved");
        Feed(a,OpenTrade());Feed(b,OpenTrade());Check(a.World.TradeCanAdd&&b.World.TradeCanAdd,"both S52 windows opened");
        Check(!await Trade(a,"add",901,2)&&await Trade(a,"add",901,1),"trade quantity checked");Check(sa.Last()[0]==37&&D(sa.Last(),1)==901&&D(sa.Last(),5)==1,"C37 item and quantity");
        Check(a.World.TradeOffers.Length==0&&Held(a,901)==1,"trade add waits for authority");Feed(a,Offer(0));Feed(b,Offer(1));Check(a.World.TradeOffers.Single().Side==0&&b.World.TradeOffers.Single().Side==1,"both offer sides");
        Check(await Trade(a,"confirm")&&await Trade(b,"confirm")&&sa.Last()[0]==71&&sb.Last()[0]==71,"both C71 confirmations");
        Feed(a,LoginPackets.Pad([57,..BitConverter.GetBytes(901u)]));Item(b,902,1);Feed(a,LoginPackets.Pad([112,0]));Feed(b,LoginPackets.Pad([112,0]));Check(a.World.TradeState=="completed"&&b.World.TradeState=="completed"&&Held(a,901)==0&&Held(b,902)==1,"two offline clients complete one item transfer");
    }
    static async Task MeatCycle(){
        var sent=new List<byte[]>();using var c=Seed(sent);Item(c,901,10);var meat=GroundCatalog.Rows.Single(r=>r.Id=="meat");
        foreach(uint amount in new uint[]{1,10}){
            c.World.SelectItem(901);Check(await c.DropSelected(amount),"drop meat "+amount);Check(sent.Last()[0]==25&&D(sent.Last(),5)==901&&D(sent.Last(),9)==amount,"C25 meat wire "+amount);Check(Held(c,901)==10,"drop awaits inventory "+amount);
            Item(c,901,10-amount);Feed(c,Packet(w=>{w.Write((byte)87);w.Write((ushort)32600);w.Write((ushort)32940);w.Write(903u);w.Write(meat.GroundGfx);w.Write(new byte[4]);w.Write(amount);w.Write((ushort)0);Text(w,meat.CountName(amount));Text(w,"");w.Write((byte)0);w.Write(0u);Text(w,"");Text(w,"");w.Write((byte)0);w.Write((byte)255);w.Write(new byte[3]);w.Write((byte)255);w.Write((byte)255);}));
            Check(c.World.SelectGround(903)&&c.World.SelectedGround!.Count==amount,"S87 dropped ground "+amount);await Task.Delay(LoginConnection.GroundClickGuardMs+20);
            Check(await c.PickupSelected(amount),"pickup meat "+amount);Check(sent.Last()[0]==112&&D(sent.Last(),5)==903&&D(sent.Last(),9)==amount,"C112 meat wire "+amount);
            Item(c,901,10);Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(903u)]));// No map/renderer is loaded in this protocol-only test.
            Set(c.World,"CacheEnabled",true);Feed(c,LoginPackets.Pad([158,..BitConverter.GetBytes(123u),15]));Set(c.World,"CacheEnabled",false);Check(Held(c,901)==10&&c.World.Ground.Length==0,"meat cycle authority restored "+amount);await Task.Delay(LoginConnection.GroundClickGuardMs+20);
        }
    }
    static async Task Regression(){
        using var t=Seed([]);await Trade(t,"invite");Feed(t,LoginPackets.Pad([112,0]));Check(t.World.TradeState!="completed","S112 success before S52 is not completion");
        var sent=new List<byte[]>();using var c=Seed(sent);await OpenShop(c);await Npc(c,"shop",0,new ShopChoice(7,1));Feed(c,Shop());Check(c.World.InteractionKind=="submitted"&&!c.World.CanSubmitShop,"duplicate S70 cannot reopen submitted purchase");

    }
    static async Task NegativeCases(){
        using(var c=Seed([])){
            await Npc(c,"talk");Feed(c,Shop(789));Check(c.World.InteractionKind=="waiting","wrong NPC list ignored");
            Feed(c,Shop());Check(c.World.ValidateShop([new(7,1)]),"direct S70 after talk supported");
            int rev=c.World.InteractionRevision;Check(!c.World.ValidateShop([new(7,1),new(7,1)]),"duplicate shop rows rejected");
            c.World.SelectTarget(null);Check(!await c.Interact("shop",rev,c.World.WorldEpoch,c.World.TargetRevision,0,[new(7,1)]),"target change invalidates shop");
        }
        using(var c=Seed([])){
            await Npc(c,"talk");Feed(c,Dialog());Page(c,"retrieve");await Npc(c,"option");Feed(c,Shop());
            Check(c.World.InteractionKind=="waiting","S70 cannot replace waiting warehouse list");
            Feed(c,Packet(b=>{b.Write((byte)176);b.Write(456u);b.Write((ushort)0);b.Write((byte)4);b.Write(30u);}));
            Check(c.World.InteractionKind=="closed"&&!c.World.CanSubmitService,"unsupported warehouse kind blocked");
        }
        using(var c=Seed([])){
            await Trade(c,"invite");Feed(c,OpenTrade());Feed(c,LoginPackets.Pad([112,0]));Check(c.World.TradeState=="open","S112 before own confirmation ignored");
            Check(await Trade(c,"confirm"),"confirm before changed offer");Feed(c,Offer(1));Check(c.World.TradeCancelRequired&&!c.World.TradeCanConfirm,"changed offer after confirm requires cancellation");
            Check(await Trade(c,"cancel"),"trade cancellation admitted");Feed(c,LoginPackets.Pad([112,1]));Check(c.World.TradeState=="cancelled"&&!c.World.TradeBlocksActions,"S112 cancelled unlocks actions");
        }
        using(var c=Seed([])){
            await Trade(c,"invite");Feed(c,Packet(b=>{b.Write((byte)71);b.Write((ushort)253);b.Write((byte)1);Text(b,"peer");}));Check(c.World.TradeState=="cancelled","peer refusal closes invitation");
        }
        using(var c=Seed([],true)){
            long now=0;Set(c.World,"PrivateShopClock",(Func<long>)(()=>now));await c.SendPrivateShop(true,false,0,0,c.PrivateShopSnapshot());now=16000;
            var view=c.PrivateShopSnapshot();Check(!view.CanQueryBuy&&c.World.PrivateShopPhase=="timeout","private query timeout quarantines late response");Feed(c,PrivateList());Check(c.World.PrivateShopPhase=="timeout","timed out private list ignored");
        }
        using(var c=Seed([],true)){
            await c.SendPrivateShop(true,false,0,0,c.PrivateShopSnapshot());Feed(c,PrivateList());c.ChoosePrivateShop(0,1,c.PrivateShopSnapshot());var stamp=c.PrivateShopSnapshot();
            Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));Check(await c.SendPrivateShop(false,false,0,1,stamp)==PrivateShopResult.NotSent,"removed private owner cannot receive stale purchase");
        }
        using(var c=Seed([])){
            Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)((p,v,a)=>{Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));return Task.FromResult(v());}));
            Check(!await Npc(c,"talk"),"target removed at final send gate rejected");
        }
    }
    static async Task<int> Main(string[] args){try{using var guard=new SocketGuard();if(args.Length>0&&args[0]=="--route"){RouteChecks.Run(args[1],args[2],args[3]);return 0;}await Purchase();await Warehouse();await Private();await DirectTrade();await MeatCycle();await Regression();await NegativeCases();await SellTests();Console.WriteLine($"PASS {passed.Count} NPC checks (synthetic, no network/UI/resources)");if(args.Length>0)File.WriteAllText(args[0],JsonSerializer.Serialize(new{status="pass",checks=passed.Count,scope="合成封包，正式產品 DLL；未執行遊戲／網路／DB",passed},new JsonSerializerOptions{WriteIndented=true}));return 0;}catch(Exception ex){Console.Error.WriteLine($"after {passed.Count} assertions: {ex}");return 1;}}
    sealed class SocketGuard:EventListener {protected override void OnEventSourceCreated(EventSource s){if(s.Name=="System.Net.Sockets")EnableEvents(s,EventLevel.LogAlways);}protected override void OnEventWritten(EventWrittenEventArgs e)=>throw new Exception("Network forbidden in offline checks");}
}
