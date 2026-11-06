using P06;
using System.Text.Json;

partial class Checks {
    static byte[] Sale(uint npc=456,params (uint Id,uint Price)[] rows)=>Packet(b=>{
        b.Write((byte)65);b.Write(npc);b.Write((ushort)rows.Length);
        foreach(var r in rows){b.Write(r.Id);b.Write(r.Price);}
    });
    static byte[] Buyback(ushort declared,params (byte Order,uint Id,uint Count,uint Price)[] rows)=>Packet(b=>{
        b.Write((byte)140);b.Write((byte)1);b.Write(456u);b.Write(declared);
        foreach(var r in rows){b.Write(r.Order);b.Write(r.Id);b.Write(r.Count);b.Write(r.Price);}
    });
    static void Equip(LoginConnection c,bool equipped)=>Feed(c,Packet(b=>{
        b.Write((byte)64);b.Write((byte)66);b.Write(901u);b.Write((byte)1);b.Write((byte)(equipped?1:0));
    }));
    static void Weapon(LoginConnection c)=>Feed(c,Packet(b=>{
        b.Write((byte)15);b.Write(901u);b.Write((ushort)1);b.Write((byte)1);b.Write((byte)0);
        b.Write((ushort)1);b.Write((byte)1);b.Write(1u);b.Write((byte)0);Text(b,"weapon");b.Write((byte)0);b.Write(new byte[11]);
    }));
    static void Bad(LoginConnection c,byte[] packet,string label){
        bool rejected=false;try{Feed(c,packet);}catch(ProtocolException){rejected=true;}Check(rejected,label);
    }
    static async Task QuerySale(LoginConnection c,params (uint Id,uint Price)[] rows){
        Check(await Npc(c,"talk"),"sale talk");Feed(c,Dialog());Page(c,"sell");
        Check(await Npc(c,"option"),"sale action");Feed(c,Sale(456,rows));
    }
    static async Task QueryBuyback(LoginConnection c,ushort declared,params (byte Order,uint Id,uint Count,uint Price)[] rows){
        Check(await c.SendPrivateShop(true,true,0,0,c.PrivateShopSnapshot())==PrivateShopResult.Accepted,"buyback query");
        Feed(c,Buyback(declared,rows));
    }
    static async Task SellTests(){
        var sent=new List<byte[]>();
        using(var c=Seed(sent)){
            Item(c,900,100,true);Item(c,901,10);Item(c,902,3);
            await QuerySale(c,(901,2),(902,5));
            Check(sent[^1][0]==125&&LoginPackets.TextEncoding.GetString(sent[^1],5,4)=="sell","C125 sale query wire");
            Check(c.World.ShopRows[0] is {Id:901,Icon:124,Price:2,Available:10},"S65 inventory presentation");
            foreach(var choices in new ShopChoice[][]{[],[new ShopChoice(901,0)],[new ShopChoice(901,11)],[new ShopChoice(999,1)],[new ShopChoice(901,1),new ShopChoice(901,1)],[new ShopChoice(901,uint.MaxValue)]})
                Check(!await Npc(c,"shop",0,choices),"NPC invalid sale rejected");
            Check(await Npc(c,"shop",0,new ShopChoice(901,3),new ShopChoice(902,2)),"NPC multiple sale rows accepted");
            var p=sent[^1];Check(p.Length==24&&p[0]==161&&D(p,1)==456&&p[5]==1&&p[6]==2&&p[7]==0&&D(p,8)==901&&D(p,12)==3&&D(p,16)==902&&D(p,20)==2,"NPC D item/D count wire");
            Check(Held(c,901)==10&&Held(c,900)==100&&c.World.InteractionKind=="submitted","NPC no predicted sale");
            Check(!await Npc(c,"shop",0,new ShopChoice(901,3)),"NPC duplicate sale blocked");
            Feed(c,Sale(456,(901,2)));Check(c.World.InteractionKind=="submitted","late S65 cannot reopen sale");
            Count(c,901,7);Check(Held(c,900)==100&&Held(c,901)==7,"NPC item update independent of currency");
            Count(c,900,116);Check(Held(c,900)==116&&!c.World.InteractionNotice.Contains("已確認成交"),"NPC income authority only");
        }
        using(var c=Seed([])){
            Item(c,901,3);await Npc(c,"talk");Feed(c,Sale(999,(901,2)));Check(c.World.InteractionKind=="waiting","wrong NPC sale ignored");
            Feed(c,Sale());Check(c.World.InteractionKind=="sell"&&!c.World.CanSubmitService&&!await Npc(c,"shop",0,new ShopChoice(901,1)),"empty NPC sale disabled");
        }
        using(var c=Seed([])){
            Weapon(c);Equip(c,true);await QuerySale(c,(901,2),(999,3));
            Check(c.World.ShopRows.All(r=>r.Available==0)&&!await Npc(c,"shop",0,new ShopChoice(901,1)),"authoritative equipped and missing NPC items disabled");
        }
        using(var c=Seed([])){
            Item(c,901,10);await QuerySale(c,(901,2));Count(c,901,9);
            Check(c.World.InteractionKind=="closed"&&!await Npc(c,"shop",0,new ShopChoice(901,1)),"inventory delta invalidates NPC quote");
        }
        using(var c=Seed([])){
            Item(c,901,10);await QuerySale(c,(901,1200000000));
            Check(c.World.ShopRows[0].Available==1&&!await Npc(c,"shop",0,new ShopChoice(901,2)),"NPC price overflow disabled");
        }
        using(var c=Seed([])){
            long now=0;Set(c.World,"InteractionClock",(Func<long>)(()=>now));Item(c,901,10);await QuerySale(c,(901,2));now=30000;
            Check(!await Npc(c,"shop",0,new ShopChoice(901,1))&&c.World.InteractionKind=="closed","NPC quote expires without resending");
        }
        using(var c=Seed([])){
            Weapon(c);await QuerySale(c,(901,2));
            Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)((p,v,a)=>{Equip(c,true);return Task.FromResult(v());}));
            Check(!await Npc(c,"shop",0,new ShopChoice(901,1)),"NPC equip race rejected at final send gate");
        }
        using(var c=Seed([])){
            Item(c,901,10);await QuerySale(c,(901,2));
            Feed(c,LoginPackets.Pad([119,..BitConverter.GetBytes(789u),1]));
            Check(c.World.ValidateService([new(901,1)]),"unrelated actor appearance does not stale NPC sale");
            c.World.SelectTarget(null);Check(!await Npc(c,"shop",0,new ShopChoice(901,1)),"NPC sale cannot follow changed target");
        }
        using(var c=Seed([])){
            await Npc(c,"talk");
            Bad(c,Sale(456,(0,1)),"S65 zero item rejected");Bad(c,Sale(456,(901,1),(901,2)),"S65 duplicates rejected");
            Bad(c,Sale(456,(901,uint.MaxValue)),"S65 signed price rejected");Bad(c,Sale(456,(901,1))[..8],"S65 truncation rejected");
            var bad=Sale(456,(901,1));bad[^1]=1;Bad(c,bad,"S65 nonzero padding rejected");
            Check(c.World.InteractionKind=="waiting"&&c.World.ShopRows.Length==0,"bad S65 applies no partial rows");
        }
        sent=[];
        using(var c=Seed(sent,true)){
            Item(c,901,10);Item(c,902,3);Item(c,900,100,true);
            await QueryBuyback(c,1,(0,901,20,3),(0,902,20,3));
            Check(sent[^1][0]==47&&sent[^1][1]==1&&D(sent[^1],2)==456,"C47 mode1 wire");
            Check(c.World.PrivateShopRows.Length==2&&c.World.PrivateShopRows[1].Maximum==3,"one offer expands to multiple inventory rows");
            Check(c.ChoosePrivateShop(1,0,c.PrivateShopSnapshot()),"choose second matching stack");
            foreach(uint count in new uint[]{0,4,0x1000000,uint.MaxValue})
                Check(await c.SendPrivateShop(false,true,1,count,c.PrivateShopSnapshot())==PrivateShopResult.NotSent,"invalid buyback count rejected");
            Check(await c.SendPrivateShop(false,true,1,3,c.PrivateShopSnapshot())==PrivateShopResult.Accepted,"buyback send");
            var p=sent[^1];Check(p.Length==16&&p[5]==1&&p[6]==1&&p[7]==0&&D(p,8)==902&&D(p,12)==3,"buyback owned item/CH count/order wire");
            Check(Held(c,902)==3&&Held(c,900)==100,"buyback does not predict inventory or money");
            Check(await c.SendPrivateShop(false,true,1,3,c.PrivateShopSnapshot())==PrivateShopResult.NotSent,"buyback double submit blocked");
            Feed(c,Buyback(1,(0,902,20,3)));Check(c.World.PrivateShopPhase=="submitted","late buyback list ignored");
            Feed(c,LoginPackets.Pad([57,..BitConverter.GetBytes(902u)]));Check(Held(c,902)==0&&Held(c,900)==100&&c.World.PrivateShopPhase=="review","buyback S57 authority independent of income");
            Count(c,900,109);Check(Held(c,900)==109,"buyback income only from S24");
        }
        using(var c=Seed([],true)){
            Item(c,901,20000000);await QueryBuyback(c,8,(7,901,20000000,0));
            var row=c.World.PrivateShopRows.Single();Check(row.Maximum==0xFFFFFF,"buyback 24-bit maximum");
            var p=PrivateShopPackets.Submit(456,true,row,0x123456);Check(p[12]==0x56&&p[13]==0x34&&p[14]==0x12&&p[15]==7,"CH quantity and nonzero order remain distinct");
        }
        using(var c=Seed([],true)){
            Weapon(c);Equip(c,true);await QueryBuyback(c,1,(0,901,10,2),(0,999,10,2));
            Check(c.World.PrivateShopRows.All(r=>r.Maximum==0),"equipped/missing buyback items disabled");
        }
        using(var c=Seed([],true)){
            Item(c,901,5);await QueryBuyback(c,1,(0,901,5,700000000));
            Check(c.World.PrivateShopRows.Single().Maximum==2,"buyback 2 billion server total cap");
        }
        using(var c=Seed([],true)){
            Item(c,901,5);await c.SendPrivateShop(true,true,0,0,c.PrivateShopSnapshot());Count(c,901,4);Feed(c,Buyback(1,(0,901,5,2)));
            Check(c.World.PrivateShopPhase=="stale"&&!c.PrivateShopSnapshot().CanQuerySell,"inventory change while querying quarantines buyback reply");
        }
        using(var c=Seed([],true)){
            Weapon(c);await QueryBuyback(c,1,(0,901,1,2));c.ChoosePrivateShop(0,0,c.PrivateShopSnapshot());
            Set(c,"sendMovementGate",(Func<byte[],Func<bool>,Action,Task<bool>>)((p,v,a)=>{Equip(c,true);return Task.FromResult(v());}));
            Check(await c.SendPrivateShop(false,true,0,1,c.PrivateShopSnapshot())==PrivateShopResult.NotSent,"buyback equipment race rejected at final gate");
        }
        using(var c=Seed([],true)){
            Item(c,901,5);await QueryBuyback(c,1,(0,901,5,2));
            Feed(c,LoginPackets.Pad([119,..BitConverter.GetBytes(789u),1]));
            Check(c.ChoosePrivateShop(0,0,c.PrivateShopSnapshot()),"unrelated actor appearance does not stale buyback");
            var stamp=c.PrivateShopSnapshot();Feed(c,LoginPackets.Pad([120,..BitConverter.GetBytes(456u)]));
            Check(await c.SendPrivateShop(false,true,0,1,stamp)==PrivateShopResult.NotSent,"removed buyback owner cannot receive old sale");
        }
        using(var c=Seed([],true)){
            Item(c,901,5);await c.SendPrivateShop(true,true,0,0,c.PrivateShopSnapshot());
            Bad(c,Buyback(1,(0,901,5,2),(0,901,5,2)),"buyback duplicate identity rejected");
            Bad(c,Buyback(1,(0,0,5,2)),"buyback zero item rejected");Bad(c,Buyback(0,(0,901,5,2)),"buyback zero declaration with rows rejected");
            Bad(c,Buyback(1,(0,901,5,uint.MaxValue)),"buyback negative price rejected");
            var bad=Buyback(1,(0,901,5,2));bad[^1]=1;Bad(c,bad,"buyback invalid padding rejected");
            Feed(c,Buyback(1));Check(c.World.PrivateShopPhase=="empty","nonempty offers with no inventory matches valid empty result");
        }
        using(var c=Seed([],true)){
            long now=0;Set(c.World,"PrivateShopClock",(Func<long>)(()=>now));Item(c,901,5);await QueryBuyback(c,1,(0,901,5,2));c.ChoosePrivateShop(0,0,c.PrivateShopSnapshot());now=30000;
            Check(await c.SendPrivateShop(false,true,0,1,c.PrivateShopSnapshot())==PrivateShopResult.NotSent,"buyback stale quote timeout");
        }
        Check(NpcWebCommand.Parse(JsonDocument.Parse("{\"v\":1,\"id\":1,\"cmd\":\"npcShop\",\"revision\":0,\"epoch\":0,\"targetRevision\":0,\"rows\":[{\"id\":901,\"count\":2}]}").RootElement).Choices[0]==new ShopChoice(901,2),"NPC web sale command contract");
        Check(PrivateShopCommand.Parse(JsonDocument.Parse("{\"v\":1,\"id\":1,\"cmd\":\"privateSubmit\",\"generation\":0,\"epoch\":0,\"revision\":0,\"targetRevision\":0,\"actorId\":123,\"ownerId\":456,\"row\":0,\"count\":2,\"sell\":true}").RootElement).Sell,"private web buyback command contract");
    }
}
