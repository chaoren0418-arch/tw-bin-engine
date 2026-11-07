using P06;

partial class Checks {
    static byte[] FishingStart(uint actor=123)=>Packet(b=>{b.Write((byte)158);b.Write(actor);b.Write((byte)71);b.Write((ushort)32602);b.Write((ushort)32940);});
    static byte[] FishingVisual(uint actor=123,byte weapon=0)=>Packet(b=>{b.Write((byte)119);b.Write(actor);b.Write(weapon);});
    static byte[] FishingMessage(ushort id,params string[] args)=>Packet(b=>{b.Write((byte)71);b.Write(id);b.Write((byte)args.Length);foreach(var a in args)Text(b,a);});
    static async Task FishingChecks(){
        Check(Convert.ToHexString(FishingPackets.Cancel())=="3E00000000000000","C62 no conditional tail; exact zero padding");
        using(var c=Seed()){
            TransferInventory(c,1,42,"測試釣竿");c.World.SelectItem(TransferItem);
            Check(Convert.ToHexString(ItemUseCodec.Encode(c.World.SelectedItem!,new ItemUseArguments.Location(0x1234,0x5678)))=="A4785634123412785600000000000000","existing C164 fishing rod DHH with exact padding");
            int sends=0;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(!await c.CancelFishing(c.ReadFishing())&&sends==0,"idle cannot send cancel");
            Feed(c,FishingStart(456));Check(!c.ReadFishing().Active,"other actor fishing ignored");
            Check(c.World.Objects.Single(o=>o.ObjectId==456).ReceivedAction==71,"S158 fishing still reaches existing actor action handler");
            int revision=c.ReadFishing().Revision;
            Feed(c,FishingStart());Check(c.ReadFishing().Active&&c.ReadFishing().Notice.Contains("32602, 32940"),"S158 CDCHH starts own waiting state");
            Check(c.ReadFishing().Revision==revision+1,"Receive observes fishing start exactly once");
            Feed(c,FishingMessage(403,"測試漁獲 (2)"));Check(c.ReadFishing().Notice.Contains("測試漁獲 (2)")&&c.ReadFishing().Active,"S71 HCS result remains waiting, not inferred completion");
            Check(c.World.LastServerReply?.Id==403,"S71 result still reaches inventory message handler");
            var shown=c.ReadFishing();Check(await c.CancelFishing(shown)&&sends==1,"one admitted C62");
            Check(c.ReadFishing() is {Active:true,CancelPending:true},"cancel is pending, never optimistic completion");
            Check(!await c.CancelFishing(shown)&&!await c.CancelFishing(c.ReadFishing())&&sends==1,"duplicate cancel blocked");
            Feed(c,FishingVisual(456));Check(c.ReadFishing().Active,"other actor visual update does not stop own fishing");
            Feed(c,FishingVisual(123,4));Check(!c.ReadFishing().Active&&!c.ReadFishing().CancelPending,"S119 DC confirms own stop");
            Check(c.World.Actor!.Weapon==4,"S119 still updates own appearance through existing handler");
            Feed(c,FishingStart());Feed(c,FishingMessage(1163));Check(!c.ReadFishing().Active&&c.ReadFishing().Notice.Contains("結束"),"S71 H1163 C0 ends fishing");
            foreach(ushort id in new ushort[]{1137,1138}){Feed(c,FishingMessage(id));Check(!c.ReadFishing().Active&&c.ReadFishing().Notice.Length>0,"S71 rejection "+id);}
            string notice=c.ReadFishing().Notice;Feed(c,FishingMessage(403,"其他獲得"));Check(c.ReadFishing().Notice==notice,"generic grants outside fishing do not become fishing results");
            Feed(c,FishingStart());Feed(c,LoginPackets.Pad([206,4,0,0]));Check(!c.ReadFishing().Active&&c.ReadFishing().Notice=="","map epoch clears fishing");
            Feed(c,Pack(123));Feed(c,FishingStart());c.World.Reset();Check(!c.ReadFishing().Active&&c.ReadFishing().Notice=="","reset clears fishing without extra reset hook");
        }
        using(var c=Seed()){
            Feed(c,FishingStart());var initial=c.ReadFishing();
            foreach(byte[] malformed in new[]{FishingStart()[..9],FishingVisual()[..5],FishingMessage(1163)[..3],FishingMessage(403,"測試")[..6],FishingStart().Concat(new byte[8]).ToArray()}){
                bool refused=false;try{Feed(c,malformed);}catch(ProtocolException){refused=true;}
                Check(refused&&c.ReadFishing()==initial,"truncated/overlong packet rejected atomically");
            }
            byte[] padding=FishingStart();padding[^1]=1;bool bad=false;try{Feed(c,padding);}catch(ProtocolException){bad=true;}
            Check(bad&&c.ReadFishing()==initial,"nonzero padding rejected atomically");
            Feed(c,FishingMessage(1137,"unexpected"));Check(c.ReadFishing()==initial,"unexpected message argument count does not change state");
            Feed(c,Packet(b=>{b.Write((byte)158);b.Write(456u);b.Write((byte)70);Text(b,"測試商店");}));
            Check(c.ReadFishing()==initial&&c.World.Objects.Single(o=>o.ObjectId==456).ShopActive,"non-fishing S158 keeps existing shop dispatch");
        }
        foreach(string invalid in new[]{"world","session","death","stop","revision"}){
            using var c=Seed();Feed(c,FishingStart());var shown=c.ReadFishing();int sends=0;
            Sender(c,(b,v,a)=>{
                switch(invalid){
                    case "world":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
                    case "session":Set(c,"generation",c.SessionGeneration+1);break;
                    case "death":Set(c.World,"OwnDead",true);break;
                    case "stop":Feed(c,FishingVisual());break;
                    case "revision":Feed(c,FishingMessage(403,"新回覆"));break;
                }
                if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);
            });
            Check(!await c.CancelFishing(shown)&&sends==0,"queued stale cancel rejected "+invalid);
        }
        using(var c=Seed()){
            long now=100;Set(c.World,"ActionClock",(Func<long>)(()=>now));Feed(c,FishingStart());int sends=0;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);Check(b.SequenceEqual(FishingPackets.Cancel()),"real sender exact C62");sends++;a();return Task.FromResult(true);});
            Check(await c.CancelFishing(c.ReadFishing()),"cancel admitted before timeout");now+=5000;
            Check(c.ReadFishing() is {Active:true,CancelPending:false}&&sends==1,"timeout stays unconfirmed without automatic resend");
            Check(await c.CancelFishing(c.ReadFishing())&&sends==2,"explicit timeout retry allowed");
        }
    }
}
