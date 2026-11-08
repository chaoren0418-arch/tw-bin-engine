using P06;

// M7-J (#516): shared S219 question -> chat input line Y/N -> C121.
// Synthetic packets and an in-memory sender against the product DLL; no game, server, DB or assets.
partial class Checks {
    static byte[] YesNoAsk(ushort message,uint request=0,ushort header=0,params string[] args)=>Packet(b=>{
        b.Write((byte)219);b.Write(header);b.Write(request);b.Write(message);foreach(var s in args)Text(b,s);
    });
    static List<byte[]> YesNoWire(LoginConnection c){
        var sent=new List<byte[]>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});return sent;
    }
    static async Task YesNoPromptChecks(){
        // Wire format: the shared answer equals each module's existing d287 C121.
        foreach(bool yes in new[]{false,true}){
            Check(YesNoProtocol.Answer(77,953,yes).SequenceEqual(PartyPackets.Answer(77,yes)),"yes-no C121 equals party 953 envelope "+yes);
            Check(YesNoProtocol.Answer(0,630,yes).SequenceEqual(DuelPackets.Answer(yes)),"yes-no C121 equals duel 630 envelope "+yes);
            Check(YesNoProtocol.Answer(0,654,yes).SequenceEqual(MarriagePackets.Answer(yes)),"yes-no C121 equals marriage 654 envelope "+yes);
        }
        Check(Convert.ToHexString(YesNoProtocol.Answer(0,1000,true))=="79000000000000E80301000000000000","yes-no C121 H0 D0 H C padded");
        // sub_A58380: leading y/Y (full-width too) accepts; everything else declines.
        foreach(var t in new[]{"y","Y","yes","ｙ","Ｙ"," y"})Check(YesNoProtocol.Accepts(t),"yes-no accepts "+t);
        foreach(var t in new[]{"","n","N","no","x","是"})Check(!YesNoProtocol.Accepts(t),"yes-no declines "+t);
        // WIN§2.1 special ids; timeouts only 729/1232/1256/1268.
        foreach(ushort m in new ushort[]{102,180,289,321,325,479,512,622,252,217,951,1565,1906})Check(YesNoProtocol.Special(m)&&!YesNoProtocol.Generic(m),"yes-no special id "+m);
        foreach(ushort m in new ushort[]{953,954,630,654,729,1000})Check(YesNoProtocol.Generic(m),"yes-no generic id "+m);
        Check(YesNoProtocol.TimeoutMs(729)==12000&&YesNoProtocol.TimeoutMs(1232)==10000&&YesNoProtocol.TimeoutMs(1256)==10000&&YesNoProtocol.TimeoutMs(1268)==10000,"yes-no timed ids");
        foreach(ushort m in new ushort[]{953,630,654,321,322,1000})Check(YesNoProtocol.TimeoutMs(m)==null,"yes-no no timeout "+m);
        Check(typeof(LoginConnection).Assembly.GetType("P06.MarriageUi")==null,"marriage dedicated Form removed");

        using(var c=Seed()){
            var sent=YesNoWire(c);
            Feed(c,YesNoAsk(1000,0,0,"甲方"));var v=c.YesNoSnapshot();
            Check(v.Prompt is {Message:1000,RequestId:0}&&v.Prompt.Text.Contains("甲方")&&v.Prompt.Deadline==null,"yes-no generic S219 queued with argument");
            Check(await c.AnswerYesNo(v,"Y")&&sent.Count==1&&sent[0].SequenceEqual(YesNoProtocol.Answer(0,1000,true)),"yes-no Y+Enter sends one C121 accept");
            Check(c.YesNoSnapshot().Prompt==null&&!await c.AnswerYesNo(v,"y")&&sent.Count==1,"yes-no answer is single use");
            Feed(c,YesNoAsk(1001));Check(await c.AnswerYesNo(c.YesNoSnapshot(),"")&&sent[^1].SequenceEqual(YesNoProtocol.Answer(0,1001,false)),"yes-no non-Y input declines");
        }
        using(var c=Seed()){
            var sent=YesNoWire(c);
            Feed(c,YesNoAsk(1000));var old=c.YesNoSnapshot();Feed(c,YesNoAsk(1001));
            Check(c.YesNoSnapshot().Prompt?.Message==1001&&!await c.AnswerYesNo(old,"y")&&sent.Count==0,"yes-no newer S219 replaces the single input-line prompt");
            Feed(c,YesNoAsk(252,12,0,"target"));
            Check(c.YesNoSnapshot().Prompt==null&&c.World.TradeState=="invited","yes-no special 252 clears prompt and stays with trade");
            Feed(c,YesNoAsk(1000));Set(c.World,"WorldEpoch",c.World.WorldEpoch+1);
            Check(c.YesNoSnapshot().Prompt==null,"yes-no world epoch invalidates prompt");
            Feed(c,YesNoAsk(1000));var stamp=c.YesNoSnapshot();Set(c,"generation",stamp.Generation+1);
            Check(!await c.AnswerYesNo(stamp,"y")&&sent.Count==0,"yes-no old generation cannot answer");
        }
        using(var c=Seed()){
            Feed(c,YesNoAsk(1000));var before=c.YesNoSnapshot();bool refused=false;
            try{Feed(c,YesNoAsk(1001,0,1));}catch(ProtocolException){refused=true;}
            Check(refused&&c.YesNoSnapshot().Prompt?.Message==1000,"yes-no malformed envelope leaves prompt untouched");
            // 322 stays with Resurrection.cs (its observer expects no argument); the shared prompt is only cleared.
            Feed(c,YesNoAsk(322));Check(c.YesNoSnapshot().Prompt==null,"yes-no delegated 322 not queued here");
        }
        using(var c=Seed()){
            Set(c.World,"TimeReplies",0);Feed(c,YesNoAsk(1000));Check(c.YesNoSnapshot().Prompt==null,"yes-no not queued before world ready");
        }
        foreach(var (message,ms) in new[]{((ushort)729,12000),((ushort)1232,10000),((ushort)1256,10000),((ushort)1268,10000)})using(var c=Seed()){
            long now=0;Set(c.World,"YesNoClock",(Func<long>)(()=>now));var sent=YesNoWire(c);
            Feed(c,YesNoAsk(message));Check(c.YesNoSnapshot().Prompt?.Deadline==ms,"yes-no deadline "+message);
            now=ms-1;Check(!await c.YesNoTick()&&sent.Count==0,"yes-no no early reply "+message);
            now=ms;Check(await c.YesNoTick()&&sent.Count==1&&sent[0].SequenceEqual(YesNoProtocol.Answer(0,message,false))&&c.YesNoSnapshot().Prompt==null,"yes-no timeout auto reply "+message);
            Check(!await c.YesNoTick()&&sent.Count==1,"yes-no timeout reply once "+message);
        }
        using(var c=Seed()){
            long now=0;Set(c.World,"YesNoClock",(Func<long>)(()=>now));var sent=YesNoWire(c);
            Feed(c,YesNoAsk(1000));now=600000;Check(!await c.YesNoTick()&&c.YesNoSnapshot().Prompt!=null&&sent.Count==0,"yes-no untimed prompt never auto replies");
        }
        // Party / duel / marriage are answered through the shared prompt, via each module's own send path.
        foreach(bool yes in new[]{false,true})using(var c=Seed()){
            var sent=YesNoWire(c);Feed(c,PartyYesNo());var v=c.YesNoSnapshot();
            Check(v.Prompt is {Message:953,RequestId:77}&&v.Prompt.Text.Contains("邀請者"),"yes-no party 953 queued with challenge");
            Check(await c.AnswerYesNo(v,yes?"y":"n")&&sent.Single().SequenceEqual(PartyPackets.Answer(77,yes)),"yes-no party answer exact C121 "+yes);
            Check(c.PartySnapshot().Invitation==null&&c.YesNoSnapshot().Prompt==null,"yes-no party invitation consumed "+yes);
        }
        using(var c=Seed()){
            var sent=YesNoWire(c);Feed(c,PartyYesNo());
            Check(await c.SendParty("accept",c.PartySnapshot())&&c.YesNoSnapshot().Prompt==null,"yes-no party module answer clears input-line prompt");
            long now=0;Set(c.World,"PartyClock",(Func<long>)(()=>now));Feed(c,PartyYesNo());now=30000;
            Check(c.YesNoSnapshot().Prompt==null&&!await c.AnswerYesNo(c.YesNoSnapshot(),"y")&&sent.Count==1,"yes-no follows party local invitation expiry");
        }
        foreach(bool yes in new[]{false,true})using(var c=DuelSeed()){
            var sent=YesNoWire(c);Feed(c,DuelPrompt("邀請者"));var v=c.YesNoSnapshot();
            Check(v.Prompt is {Message:630}&&v.Prompt.Text.Contains("邀請者"),"yes-no duel 630 queued");
            Check(await c.AnswerYesNo(v,yes?"Y":"N")&&sent.Single().SequenceEqual(DuelPackets.Answer(yes)),"yes-no duel answer exact C121 "+yes);
            Check(c.World.DuelSnapshot.Phase==(yes?"accepting":"rejected")&&c.YesNoSnapshot().Prompt==null&&!c.World.IsDuelOpponent(456),"yes-no duel waits for server "+yes);
        }
        using(var c=DuelSeed()){
            Feed(c,DuelPrompt());Feed(c,DuelPrompt("trader",252,12));
            Check(c.YesNoSnapshot().Prompt==null&&c.World.DuelSnapshot.Phase=="expired","yes-no duel prompt replaced by trade");
        }
        foreach(bool yes in new[]{false,true})using(var c=MarriageSeed()){
            var sent=YesNoWire(c);Feed(c,MarriageInvite());var v=c.YesNoSnapshot();
            Check(v.Prompt is {Message:654,RequestId:0}&&v.Prompt.Deadline==null,"yes-no marriage 654 default branch queued");
            Check(await c.AnswerYesNo(v,yes?"y":"n")&&sent.Single().SequenceEqual(MarriagePackets.Answer(yes)),"yes-no marriage answer exact C121 "+yes);
            Check(c.MarriageSnapshot().Phase==(yes?"accepting":"rejected")&&c.MarriageSnapshot().Partner==""&&c.YesNoSnapshot().Prompt==null,"yes-no marriage no premature success "+yes);
        }
        using(var c=MarriageSeed()){
            var sent=YesNoWire(c);Feed(c,MarriageInvite());Feed(c,PartyYesNo());
            Check(c.YesNoSnapshot().Prompt?.Message==953&&c.MarriageSnapshot().Phase=="expired","yes-no party replaces marriage prompt");
            Feed(c,MarriageInvite());Check(c.YesNoSnapshot().Prompt?.Message==654&&c.PartySnapshot().Invitation==null,"yes-no marriage replaces party prompt");
            Set(c.World,"OwnDead",true);Check(!await c.AnswerYesNo(c.YesNoSnapshot(),"y")&&sent.Count==0,"yes-no marriage keeps module admission");
        }
    }
}
