using P06;

partial class Checks {
    static byte[] ResetOffer(int max=51)=>LoginPackets.Pad([64,1,14,0,2,0,10,(byte)max]);
    static byte[] ResetProgress(int level,int max,ResetStats s)=>Packet(b=>{b.Write((byte)64);b.Write((byte)2);b.Write((byte)level);b.Write((byte)max);b.Write((ushort)500);b.Write((ushort)100);b.Write((short)-12);foreach(int n in s.Values)b.Write((byte)n);});
    static LoginConnection ResetSeed(int max=51){
        var c=Seed();Set(c,"characters",new List<LoginCharacter>{new("offline","",0,0,0,100,100,0,(byte)max,13,10,10,11,13,10,0,0,0)});
        Feed(c,ResetOffer(max));return c;
    }
    static ResetStats InitialReset()=>new(18,13,11,10,10,13);
    static async Task CharResetChecks(){
        // Exact complete C98 layouts, including all nested C2 actions and final selector.
        var initial=InitialReset();
        Check(CharResetPackets.Initial(0,initial).SequenceEqual(new byte[]{98,1,18,13,11,10,10,13}),"reset C1 stat wire order");
        for(byte action=0;action<=8;action++)for(byte final=0;final<=(action==8?6:0);final++){
            var expected=LoginPackets.Pad(action==8?new byte[]{98,2,8,final}:new byte[]{98,2,action});
            Check(CharResetPackets.Level(action,final).SequenceEqual(expected),$"reset C2 action {action} final {final}");
        }
        Check(CharResetPackets.Elixir(initial,initial.Add(1,2),2).SequenceEqual(new byte[]{98,3,18,15,11,10,10,13}),"reset C3 absolute stats");
        for(int type=0;type<7;type++){
            var basis=CharResetPackets.BaseStats(type);Check(CharResetPackets.InitialValid(type,basis.Add(0,75-basis.Sum)),"reset seven class budget "+type);
            Check(!CharResetPackets.InitialValid(type,basis.Add(0,76-basis.Sum)),"reset budget overspend "+type);
            Check(!CharResetPackets.InitialValid(type,basis.Add(0,74-basis.Sum)),"reset budget underspend "+type);
        }
        var packets=new[]{ResetOffer(),ResetProgress(50,51,initial),LoginPackets.Pad([64,3,2]),new byte[]{64,4,0x32,0x54,0x76,0,0,0}};
        foreach(var packet in packets){
            Check(CharResetPackets.Parse(packet).Kind==packet[1],"reset S64 kind "+packet[1]);
            for(int n=0;n<packet.Length;n++){
                bool rejected=false;try{CharResetPackets.Parse(packet[..n]);}catch(ProtocolException){rejected=true;}
                Check(rejected,$"reset truncated S{packet[1]} length {n}");
            }
            bool extra=false;try{CharResetPackets.Parse([..packet,0]);}catch(ProtocolException){extra=true;}Check(extra,"reset overlong rejected "+packet[1]);
        }
        Check(CharResetPackets.Parse(packets[1]).Ac==-12,"reset signed AC");
        Check(CharResetPackets.Parse(packets[3]).Stats==new ResetStats(2,3,4,5,6,7),"reset original bonus nibbles");
        foreach(byte sub in new byte[]{0,0x0c,0x2a,0x3c,0xff}){
            using var c=Seed();Feed(c,LoginPackets.Pad([64,sub]));Check(c.CharResetSnapshot().Phase=="idle","reset leaves foreign S64 untouched "+sub);
        }
        var badPad=LoginPackets.Pad([64,3,2]);badPad[^1]=1;
        try{CharResetPackets.Parse(badPad);Check(false,"reset bad padding accepted");}catch(ProtocolException){Check(true,"reset bad padding rejected");}
        using(var c=ResetSeed()){
            int sends=0;byte[]? wire=null;Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;wire=b;a();return Task.FromResult(true);});
            var stamp=c.CharResetSnapshot();
            Check(!await c.SendCharReset(stamp,"level",step:7)&&sends==0,"reset level forbidden before initial");
            Check(!await c.SendCharReset(stamp,"initial",initial.Add(0,1))&&sends==0,"reset invalid initial sends nothing");
            // Reset can be offered while the server freezes the player; ordinary action lock must not deadlock it.
            Feed(c,LoginPackets.Pad([202,4]));Check(c.World.ActionLocked,"reset freeze fixture");
            Check(await c.SendCharReset(stamp,"initial",initial)&&wire!.SequenceEqual(CharResetPackets.Initial(0,initial)),"reset initial admitted under freeze");
            Check(!await c.SendCharReset(stamp,"initial",initial)&&sends==1,"reset stale and duplicate initial refused");
            Feed(c,ResetOffer());Check(c.CharResetSnapshot().Phase=="waiting","reset duplicate offer cannot release wait");
            Feed(c,ResetProgress(1,51,initial.Add(0,1)));Check(c.CharResetSnapshot().Phase=="waiting","reset mismatched response not admitted");
            Feed(c,ResetProgress(1,51,initial));Check(c.CharResetSnapshot().Phase=="level","reset initial acknowledged");
            int level=1;
            while(level<50){int step=level+10<=50?7:0;Check(await c.SendCharReset(c.CharResetSnapshot(),"level",step:step),"reset level request "+level);int next=level+(step==7?10:1);Feed(c,ResetProgress(level,51,initial));Check(c.CharResetSnapshot().Phase=="waiting","reset duplicate progress no release "+level);Feed(c,ResetProgress(next,51,initial));level=next;}
            stamp=c.CharResetSnapshot();Check(!await c.SendCharReset(stamp,"level",step:7),"reset ten levels cannot cross bonus boundary");
            Check(!await c.SendCharReset(stamp,"level",step:0),"reset level 51 requires attribute");
            Check(await c.SendCharReset(stamp,"level",step:6),"reset level 51 selected CHA");initial=initial.Add(5,1);Feed(c,ResetProgress(51,51,initial));
            Check(c.CharResetSnapshot().Points==0,"reset all level points accounted");
            Check(await c.SendCharReset(c.CharResetSnapshot(),"finish")&&wire!.SequenceEqual(CharResetPackets.Level(8,0)),"reset finish does not add an extra point");
            Feed(c,LoginPackets.Pad([64,3,2]));Check(c.CharResetSnapshot().Phase=="elixir","reset server elixir budget");
            stamp=c.CharResetSnapshot();Check(!await c.SendCharReset(stamp,"elixir",initial.Add(0,3)),"reset elixir overspend refused");
            Check(!await c.SendCharReset(stamp,"elixir",initial.Add(0,-1).Add(1,3)),"reset elixir cannot redistribute base");
            Check(await c.SendCharReset(stamp,"elixir",initial.Add(0,2))&&wire!.SequenceEqual(CharResetPackets.Elixir(initial,initial.Add(0,2),2)),"reset elixir final send");
            Check(!await c.SendCharReset(c.CharResetSnapshot(),"elixir",initial.Add(0,2)),"reset final duplicate suppressed");
            Feed(c,LoginPackets.Pad([206,4,0,0]));Check(!c.CharResetSnapshot().CanAct&&c.CharResetSnapshot().Phase=="ended","reset map retires completed flow");
        }
        using(var c=ResetSeed()){
            long now=0;Set(c.World,"CharResetClock",(Func<long>)(()=>now));Sender(c,(_,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});
            Check(await c.SendCharReset(c.CharResetSnapshot(),"initial",InitialReset()),"reset timeout fixture");now=15000;
            Check(c.CharResetSnapshot().Phase=="uncertain"&&!c.CharResetSnapshot().CanAct,"reset timeout never permits retry");
            Feed(c,ResetProgress(1,51,InitialReset()));Check(c.CharResetSnapshot().Phase=="uncertain","reset late response cannot reenable");
        }
        foreach(bool cancel in new[]{false,true})using(var c=ResetSeed()){
            int sends=0;Sender(c,(_,v,a)=>{if(cancel)c.Cancel();else Feed(c,LoginPackets.Pad([206,4,0,0]));if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(!await c.SendCharReset(c.CharResetSnapshot(),"initial",InitialReset())&&sends==0,"reset sender rechecks lifecycle "+cancel);
        }
        using(var c=Seed()){
            Feed(c,packets[3]);Check(c.CharResetSnapshot().Phase=="idle"&&c.CharResetSnapshot().OriginalBonus!=null,"reset info does not authorize C98");
            c.Cancel();Check(c.CharResetSnapshot().OriginalBonus==null,"reset info cleared across session");
        }
        using(var c=ResetSeed()){
            var old=c.CharResetSnapshot();Feed(c,LoginPackets.Pad([206,0xe5,3,0]));
            Check(c.CharResetSnapshot().Phase=="initial"&&!c.CharResetSnapshot().CanAct,"reset initial offer survives NPC map arrival but waits for actor");
            Feed(c,Pack(123));Check(c.CharResetSnapshot().CanAct,"reset initial offer enabled after arrival");
            Sender(c,(_,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});
            Check(!await c.SendCharReset(old,"initial",InitialReset()),"reset pre-arrival stamp expired");
            Check(await c.SendCharReset(c.CharResetSnapshot(),"initial",InitialReset()),"reset post-arrival initial allowed");
        }
        using(var c=ResetSeed()){
            var status=c.World.Status!;c.World.Reset();Set(c.World,"Status",status with{});
            Check(c.CharResetSnapshot().Phase=="ended"&&!c.CharResetSnapshot().CanAct,"reset old unsent offer cannot survive new login of same character");
        }
        using(var c=ResetSeed(1)){
            Sender(c,(_,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});
            Check(await c.SendCharReset(c.CharResetSnapshot(),"initial",InitialReset()),"reset max-level-one initial");Feed(c,ResetProgress(1,1,InitialReset()));
            Check(!await c.SendCharReset(c.CharResetSnapshot(),"level",step:0),"reset cannot overshoot max level");
            Check(await c.SendCharReset(c.CharResetSnapshot(),"finish"),"reset no-level finish");
            Check(c.CharResetSnapshot().Phase=="finishing","reset finish waits for server, no optimistic success");
        }
        using(var c=ResetSeed()){
            var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(_,v,a)=>{await release.Task;if(!v())return false;sends++;a();return true;});
            var stamp=c.CharResetSnapshot();var pending=c.SendCharReset(stamp,"initial",InitialReset());
            Check(!pending.IsCompleted&&!await c.SendCharReset(stamp,"initial",InitialReset()),"reset concurrent queued send suppressed");
            release.SetResult(true);Check(await pending&&sends==1,"reset exactly one queued C98 admitted");
        }
        using(var c=ResetSeed()){
            Sender(c,(_,_,_)=>throw new IOException("synthetic transport failure"));
            Check(!await c.SendCharReset(c.CharResetSnapshot(),"initial",InitialReset())&&c.State==LoginState.Faulted&&!c.CharResetSnapshot().CanAct,"reset transport failure retires session without retry");
        }
        foreach(var bad in new[]{InitialReset().Add(0,-1),InitialReset().Add(0,1),InitialReset().Add(0,18)}){
            bool rejected=false;try{CharResetPackets.Elixir(InitialReset(),bad,2);}catch(ArgumentException){rejected=true;}Check(rejected,"reset invalid elixir totals or cap "+bad.Str);
        }
    }
}
