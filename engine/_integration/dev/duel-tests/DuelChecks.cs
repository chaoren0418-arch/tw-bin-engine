using P06;

partial class Checks {
    static byte[] DuelPrompt(string name="target",ushort message=630,uint token=0,ushort header=0)=>Packet(b=>{
        b.Write((byte)219);b.Write(header);b.Write(token);b.Write(message);Text(b,name);
    });
    static byte[] DuelState(uint peer=456,uint self=123)=>Packet(b=>{b.Write((byte)250);b.Write((byte)5);b.Write(peer);b.Write(self);});
    static byte[] DuelMessage(ushort code,params string[] args)=>Packet(b=>{b.Write((byte)71);b.Write(code);b.Write((byte)args.Length);foreach(var s in args)Text(b,s);});
    static LoginConnection DuelSeed(){
        var c=Seed();
        // PC flag, east/west headings. Only synthetic records, no game assets.
        Set(c.World,"Actor",c.World.Actor! with{Direction=2});
        Call(c.World,"UpdateObject",456u,(Func<WorldObject,WorldObject>)(o=>o with{Flags=4,Direction=6}));
        return c;
    }
    static Task<bool> SendDuel(LoginConnection c,string command){var v=c.World.DuelSnapshot;return c.Duel(command,v.Revision,v.Epoch);}
    static void BadDuel(LoginConnection c,byte[] packet,string label){
        var before=c.World.DuelSnapshot;bool refused=false;
        try{Feed(c,packet);}catch(ProtocolException){refused=true;}
        Check(refused&&c.World.DuelSnapshot==before,"duel malformed atomic: "+label);
    }
    [CheckGroup("duel",Order=2),CheckGroup("combat-admission",Order=5)]
    static async Task DuelChecks(){
        Check(DuelPackets.Invite().SequenceEqual(new byte[]{5,0,0,0,0,0,0,0}),"C5 no conditional tail / zero padding");
        foreach(bool yes in new[]{false,true})Check(DuelPackets.Answer(yes).SequenceEqual(new byte[]{121,0,0,0,0,0,0,118,2,(byte)(yes?1:0),0,0,0,0,0,0}),"C121 H0 D0 H630 answer "+yes);
        using(var c=DuelSeed()){
            var sent=new List<byte[]>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});
            Check(c.CanInviteDuel&&await SendDuel(c,"invite"),"facing invitation admitted");
            Check(sent.Count==1&&sent[0].SequenceEqual(DuelPackets.Invite())&&!c.World.IsDuelOpponent(456),"invite cannot predict active opponent");
            Check(!await SendDuel(c,"invite"),"no repeat invitation while awaiting server");
            Feed(c,DuelMessage(631,"other"));Check(c.World.DuelSnapshot.Phase=="inviting","unrelated rejection cannot finish invitation");
            Feed(c,DuelMessage(631,"target"));Check(c.World.DuelSnapshot.Phase=="rejected","S71/631 matches invited peer");
            Check(await SendDuel(c,"invite"),"manual invitation after server rejection");
            Feed(c,DuelState());Check(c.World.IsDuelOpponent(456)&&!c.World.IsDuelOpponent(123),"S250/5 authoritative opponent only");
            Feed(c,DuelMessage(633));Check(c.World.IsDuelOpponent(456),"status text cannot remove authoritative active state");
            Feed(c,DuelState(0,0));Check(c.World.DuelSnapshot.Phase=="ended"&&!c.World.IsDuelOpponent(456),"S250/5 zero zero ends duel");
        }
        foreach(bool yes in new[]{false,true})using(var c=DuelSeed()){
            Feed(c,DuelPrompt("邀請者"));var invitation=c.World.DuelSnapshot;byte[]? sent=null;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent=b;a();return Task.FromResult(true);});
            Check(invitation.Peer=="邀請者"&&invitation.Phase=="invited","S219 CP950 player name");
            Check(await SendDuel(c,yes?"accept":"reject")&&sent!.SequenceEqual(DuelPackets.Answer(yes)),"invite answer exact packet "+yes);
            Check(!await c.Duel("accept",invitation.Revision,invitation.Epoch)&&!c.World.IsDuelOpponent(456),"answer once, no predicted active state "+yes);
            if(yes){Feed(c,DuelState());Check(c.World.IsDuelOpponent(456),"accepted invitation waits for server start");}
        }
        foreach(string mutation in new[]{"target","turn","move","world","cancel","prompt"})using(var c=DuelSeed()){
            int sent=0;Sender(c,(b,v,a)=>{
                switch(mutation){
                    case "target":c.World.SelectTarget(null);break;
                    case "turn":Feed(c,Packet(w=>{w.Write((byte)122);w.Write(456u);w.Write((byte)1);}));break;
                    case "move":Feed(c,Move(123,32600,32940,2));break;
                    case "world":Feed(c,LoginPackets.Pad([206,0,0,0]));break;
                    case "cancel":c.Cancel();break;
                    case "prompt":Feed(c,DuelPrompt());break;
                }
                if(!v())return Task.FromResult(false);sent++;a();return Task.FromResult(true);
            });
            Check(!await SendDuel(c,"invite")&&sent==0,"queued invite rechecks "+mutation);
        }
        foreach(string mutation in new[]{"replacement","other-dialog","world","death","locked","trade"})using(var c=DuelSeed()){
            Feed(c,DuelPrompt());int sends=0;
            Sender(c,(b,v,a)=>{
                switch(mutation){
                    case "replacement":Feed(c,DuelPrompt("new peer"));break;
                    case "other-dialog":Feed(c,DuelPrompt("trader",252,12));break;
                    case "world":c.World.Reset();break;
                    case "death":Set(c.World,"OwnDead",true);break;
                    case "locked":Set(c.World,"TeleportState","awaiting-world");break;
                    case "trade":Set(c.World,"TradeState","open");break;
                }
                if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);
            });
            Check(!await SendDuel(c,"accept")&&sends==0,"queued answer invalidated by "+mutation);
        }
        using(var c=DuelSeed()){
            foreach(var sample in new[]{DuelPrompt(),DuelState(),DuelMessage(631,"target"),DuelMessage(633)}){
                for(int n=0;n<sample.Length;n++)BadDuel(c,sample[..n],"truncated "+sample[0]+"/"+n);
                var dirty=sample.ToArray();dirty[^1]=255;BadDuel(c,dirty,"nonzero padding");
                BadDuel(c,[..sample,..new byte[8]],"extra zero block");
            }
            BadDuel(c,DuelPrompt(header:1),"header");BadDuel(c,DuelPrompt(token:1),"nonzero token");BadDuel(c,DuelPrompt(""),"empty name");
            BadDuel(c,DuelState(0,123),"half zero");BadDuel(c,DuelState(123,123),"self peer");BadDuel(c,DuelMessage(631),"missing rejection peer");
            Feed(c,DuelState(456,999));Check(c.World.DuelSnapshot.Phase=="idle","wrong self ignored");
            Feed(c,DuelState(999,123));Check(c.World.IsDuelOpponent(999),"server opponent does not need local invitation/visibility");
            c.World.Reset();Check(!c.World.IsDuelOpponent(999)&&c.World.DuelSnapshot.Phase=="idle","reset clears marker and dialog by epoch");
        }
        foreach(ushort code in new ushort[]{91,93,633,634})using(var c=DuelSeed()){
            Sender(c,(_,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});await SendDuel(c,"invite");
            Feed(c,code==91?DuelMessage(code,"target"):DuelMessage(code));
            Check(c.World.DuelSnapshot.Phase=="unconfirmed"&&c.World.LastServerReply?.Id==code,"S71 failure remains in existing dispatch "+code);
        }
        using(var c=DuelSeed()){
            Feed(c,DuelPrompt());Feed(c,DuelPrompt("trader",252,12));
            Check(c.World.TradeState=="invited"&&c.World.DuelSnapshot.Phase=="expired","trade subtype retains reader and expires duel answer");
            Feed(c,Packet(b=>{b.Write((byte)250);b.Write((byte)35);b.Write((ushort)60);}));
            Check(c.World.MagicBuffs.Any(b=>b.Source=="packetbox35"),"non-duel packetbox still dispatched");
        }
        using(var c=DuelSeed()){
            Feed(c,DuelState());var active=c.World.DuelSnapshot;
            Feed(c,DuelMessage(125));
            Check(c.World.LastServerReply?.Id==125&&c.World.DuelSnapshot==active,"unrelated S71 keeps original message dispatch and duel state");
            Feed(c,Pack(456,32610));int sends=0;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(!await c.Attack()&&sends==0&&c.World.IsDuelOpponent(456),"authoritative duel marker cannot bypass existing attack range admission");
        }
        using(var c=DuelSeed()){
            Feed(c,DuelState());Feed(c,LoginPackets.Pad([206,0,0,0]));
            Check(!c.World.IsDuelOpponent(456),"map reconstruction clears old marker");
        }
        using(var c=DuelSeed()){
            Sender(c,(_,_,_)=>throw new IOException("synthetic failure"));
            Check(!await SendDuel(c,"invite")&&c.State==LoginState.Faulted&&!c.World.IsDuelOpponent(456),"send failure disconnects without retry");
        }
    }
}
