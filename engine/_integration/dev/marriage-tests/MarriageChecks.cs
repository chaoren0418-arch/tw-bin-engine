using P06;

partial class Checks {
    static byte[] MarriageInvite(string peer="伴侶",uint reserved=0,ushort message=654)=>Packet(b=>{
        b.Write((byte)219);b.Write((ushort)0);b.Write(reserved);b.Write(message);Text(b,peer);
    });
    static byte[] MarriageMessage(ushort message,params string[] names)=>Packet(b=>{
        b.Write((byte)71);b.Write(message);b.Write((byte)names.Length);foreach(var name in names)Text(b,name);
    });
    static LoginConnection MarriageSeed(){
        var c=Seed();Set(c.World,"MapId",(ushort)4);
        Set(c.World,"Actor",c.World.Actor! with{X=33975,Y=33364,Direction=0});
        Feed(c,Packet(b=>{
            b.Write((byte)87);b.Write((ushort)33975);b.Write((ushort)33363);b.Write(456u);b.Write((ushort)1);
            b.Write((byte)0);b.Write((byte)4);b.Write(new byte[2]);b.Write(0u);b.Write((ushort)0);
            Text(b,"伴侶");Text(b,"");b.Write((byte)4);b.Write(0u);Text(b,"");Text(b,"");
            b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
        }));c.World.SelectTarget(456);return c;
    }
    static void MarriageReject(Action action,string label){try{action();}catch(ProtocolException){Check(true,label);return;}throw new Exception("marriage malformed accepted: "+label);}
    static async Task MarriageChecks(){
        Check(Convert.ToHexString(MarriagePackets.Propose())=="3200000000000000","marriage C50 subtype zero only");
        Check(Convert.ToHexString(MarriagePackets.Answer(true))=="790000000000008E0201000000000000","marriage C121 Yes legacy H0 D0 H654 C1");
        Check(Convert.ToHexString(MarriagePackets.Answer(false))=="790000000000008E0200000000000000","marriage C121 No legacy H0 D0 H654 C0");
        foreach(var xy in new[]{(33974,33362),(33976,33365)})Check(MarriagePackets.WeddingArea(4,xy.Item1,xy.Item2),"marriage area inclusive boundary");
        foreach(var xy in new[]{(33973,33362),(33977,33362),(33974,33361),(33974,33366)})Check(!MarriagePackets.WeddingArea(4,xy.Item1,xy.Item2),"marriage outside area");
        Check(!MarriagePackets.WeddingArea(0,33975,33364),"marriage wrong map");
        using(var c=MarriageSeed()){
            int sent=0;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);});
            Check(c.MarriageSnapshot().CanPropose,"marriage native view enables selected facing player");
            var stamp=c.MarriageSnapshot();Check(await c.SendMarriage("propose",stamp)&&sent==1,"marriage proposal admitted once");
            Check(!await c.SendMarriage("propose",stamp)&&sent==1,"marriage stale revision and duplicate blocked");
            Check(c.MarriageSnapshot().Partner=="","marriage send never predicts success");
            Feed(c,MarriageMessage(790));Check(c.MarriageSnapshot().Partner=="","marriage 790 has no invented partner");
            Feed(c,MarriageMessage(655,"伴侶"));Check(c.MarriageSnapshot() is {Phase:"married",Partner:"伴侶"}&&c.World.LastServerMessage!.Contains("伴侶"),"marriage S71/655 displays authoritative CP950 partner");
        }
        foreach(bool accept in new[]{false,true})using(var c=MarriageSeed()){
            Feed(c,MarriageInvite());var stamp=c.MarriageSnapshot();Check(stamp is {Phase:"invited",Peer:"伴侶",CanAnswer:true},"marriage S219/654 legacy invitation");
            byte[]? wire=null;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);wire=b;a();return Task.FromResult(true);});
            Check(await c.SendMarriage(accept?"accept":"reject",stamp)&&wire!.SequenceEqual(MarriagePackets.Answer(accept)),"marriage answer exact wire "+accept);
            Check(!await c.SendMarriage("accept",stamp)&&c.MarriageSnapshot().Partner=="","marriage answer single use and no premature success "+accept);
            if(accept){Feed(c,MarriageMessage(655,"伴侶"));Check(c.MarriageSnapshot().Partner=="伴侶","marriage recipient success");}
        }
        foreach(ushort message in new ushort[]{656,657,658,661})using(var c=MarriageSeed()){
            Sender(c,(_,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});
            await c.SendMarriage("propose",c.MarriageSnapshot());
            Feed(c,message==656?MarriageMessage(message,"伴侶"):MarriageMessage(message));
            Check(c.MarriageSnapshot().Phase==(message==656?"rejected":"failed")&&c.MarriageSnapshot().Partner=="","marriage failure message "+message);
        }
        foreach(var change in new[]{"map","reset","death","remove","timeout","other-yn","new-invite","generation","movement"})using(var c=MarriageSeed()){
            long now=0;Set(c.World,"InteractionClock",(Func<long>)(()=>now));Feed(c,MarriageInvite());var stamp=c.MarriageSnapshot();int sent=0;
            Sender(c,(_,v,a)=>{
                switch(change){
                    case "map":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
                    case "reset":c.World.Reset();break;
                    case "death":Set(c.World,"OwnDead",true);break;
                    case "remove":Feed(c,LoginPackets.Pad([120,200,1,0,0]));break;
                    case "timeout":now=WorldSession.InteractionTimeoutMs;break;
                    case "other-yn":Feed(c,MarriageInvite("交易者",7,252));break;
                    case "new-invite":Feed(c,MarriageInvite());break;
                    case "generation":Set(c,"generation",stamp.Generation+1);break;
                    case "movement":Feed(c,Move(456,33975,33363,0));Feed(c,Move(456,33975,33362,0));break;
                }
                if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);
            });
            Check(!await c.SendMarriage("accept",stamp)&&sent==0,"marriage queued stale answer blocked: "+change);
        }
        using(var c=MarriageSeed()){
            Sender(c,(_,v,a)=>{Feed(c,LoginPackets.Pad([122,200,1,0,0,2]));if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});
            Check(!await c.SendMarriage("propose",c.MarriageSnapshot()),"marriage facing changes before send");
        }
        using(var c=MarriageSeed()){
            Feed(c,MarriageInvite("伴侶",17,252));Check(c.World.TradeState=="invited","marriage keeps existing trade dispatch");
            Feed(c,MarriageMessage(125));Check(c.World.LastServerMessage!=null,"marriage keeps unrelated S71 dispatch");
        }
        using(var c=MarriageSeed()){
            foreach(var packet in new[]{MarriageInvite(),MarriageMessage(655,"伴侶"),MarriageMessage(656,"伴侶"),MarriageMessage(657),MarriageMessage(658),MarriageMessage(661),MarriageMessage(790)}){
                for(int length=0;length<packet.Length;length++){
                    var cut=packet[..length];MarriageReject(()=>Feed(c,cut),"marriage truncation "+packet[0]+"/"+length);
                }
                var bad=packet.ToArray();bad[^1]=1;MarriageReject(()=>Feed(c,bad),"marriage nonzero padding");
            }
            MarriageReject(()=>Feed(c,MarriageInvite(reserved:1)),"marriage rejects requestId envelope");
            MarriageReject(()=>Feed(c,MarriageInvite("")),"marriage empty inviter");
            MarriageReject(()=>Feed(c,MarriageMessage(655)),"marriage missing partner");
            MarriageReject(()=>Feed(c,MarriageMessage(655,"a","b")),"marriage too many partners");
            MarriageReject(()=>Feed(c,MarriageMessage(790,"a")),"marriage unexpected arguments");
        }
    }
}
