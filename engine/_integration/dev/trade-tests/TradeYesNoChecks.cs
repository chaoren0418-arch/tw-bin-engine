using P06;

// M7-K (#517): S219/252 trade invitation -> M7-J shared input-line Y/N -> Trade's own C121 admission.
// WIN§2.1: flag byte_136E99D=1 shows the generic question; flag 0 auto-replies in the original (value unverified,
// so the engine keeps the manual accept/reject there). Synthetic packets and an in-memory sender; no game, server, DB or assets.
partial class Checks {
    static byte[] TradeAsk(uint challenge=12,string peer="target")=>YesNoAsk(252,challenge,0,peer);
    static async Task TradeYesNoChecks(){
        foreach(bool yes in new[]{false,true})
            Check(TradePackets.Answer(12,yes).SequenceEqual(YesNoProtocol.Answer(12,252,yes)),"trade C121 equals shared yes-no envelope "+yes);
        Check(YesNoProtocol.Special(252)&&!YesNoProtocol.Generic(252),"trade 252 stays a WIN§2.1 special id");

        foreach(bool yes in new[]{false,true})using(var c=Seed()){
            var sent=YesNoWire(c);Feed(c,TradeAsk());var v=c.YesNoSnapshot();
            Check(c.World.TradeState=="invited"&&c.World.TradeChallenge==12&&c.World.TradePeerName=="target","trade S219/252 admitted as invitation "+yes);
            Check(v.Prompt is {Message:252,RequestId:12,Deadline:null}&&v.Prompt.Text.Contains("target")&&v.Prompt.Text.Contains("邀請你交易"),"trade invitation queued on input line "+yes);
            Check(await c.AnswerYesNo(v,yes?"y":"n")&&sent.Count==1&&sent[0].SequenceEqual(TradePackets.Answer(12,yes)),"trade Y/N sends exact C121 "+yes);
            Check(c.World.TradeState==(yes?"accepting":"cancelled")&&c.YesNoSnapshot().Prompt==null,"trade Y/N goes through trade admission "+yes);
            Check(!await c.AnswerYesNo(v,"y")&&sent.Count==1,"trade input-line answer single use "+yes);
            if(yes){Feed(c,Packet(b=>{b.Write((byte)52);Text(b,"target");}));Check(c.World.TradeState=="open","trade opens only on server S52 after Y");}
        }
        using(var c=Seed()){
            var sent=YesNoWire(c);Feed(c,TradeAsk());
            Feed(c,Move(456,32610,32940,0));
            Check(!c.World.TradePeerPresent&&!await c.AnswerYesNo(c.YesNoSnapshot(),"y")&&sent.Count==0,"trade accept refused when peer not adjacent");
            Check(c.YesNoSnapshot().Prompt?.Message==252,"trade refused accept keeps question for N");
            Check(await c.AnswerYesNo(c.YesNoSnapshot(),"n")&&sent.Single().SequenceEqual(TradePackets.Answer(12,false))&&c.World.TradeState=="cancelled","trade reject still allowed away from peer");
        }
        using(var c=Seed()){
            var sent=YesNoWire(c);Feed(c,TradeAsk());
            Check(await c.Trade("accept",c.World.TradeRevision,c.World.WorldEpoch,c.World.TradeGeneration)&&c.YesNoSnapshot().Prompt==null&&sent.Count==1,"trade module accept clears input-line question");
        }
        using(var c=Seed()){
            YesNoWire(c);Feed(c,TradeAsk());Feed(c,LoginPackets.Pad([112,1]));
            Check(c.World.TradeState=="cancelled"&&c.YesNoSnapshot().Prompt==null,"trade server cancel expires question");
            Feed(c,TradeAsk(13));Check(c.YesNoSnapshot().Prompt?.RequestId==13,"trade new invitation after result queued");
            Feed(c,PartyYesNo());Check(c.World.TradeState=="idle"&&c.YesNoSnapshot().Prompt?.Message==953,"party question replaces trade question");
        }
        using(var c=Seed()){
            var sent=YesNoWire(c);Feed(c,TradeAsk());var old=c.YesNoSnapshot();Feed(c,TradeAsk(13,"other"));
            Check(c.World.TradeState=="attention"&&c.YesNoSnapshot().Prompt==null&&!await c.AnswerYesNo(old,"y")&&sent.Count==0,"second invitation during trade not queued and old one dead");
        }
        using(var c=Seed()){
            var sent=YesNoWire(c);Feed(c,TradeAsk());var v=c.YesNoSnapshot();Set(c.World,"WorldEpoch",c.World.WorldEpoch+1);
            Check(c.YesNoSnapshot().Prompt==null&&!await c.AnswerYesNo(v,"y")&&sent.Count==0,"trade question dies with world epoch");
        }
        using(var c=Seed()){
            Set(c.World,"TimeReplies",0);Feed(c,TradeAsk());Check(c.YesNoSnapshot().Prompt==null&&c.World.TradeState=="idle","trade not queued before world ready");
        }
        using(var c=Seed()){
            long now=0;Set(c.World,"YesNoClock",(Func<long>)(()=>now));var sent=YesNoWire(c);Feed(c,TradeAsk());now=600000;
            Check(!await c.YesNoTick()&&sent.Count==0&&c.YesNoSnapshot().Prompt?.Message==252,"trade question has no timed auto reply");
        }
        // Flag 0 path: original auto-replies with an unverified value; engine keeps the pre-M7-K manual buttons.
        using(var c=Seed()){
            var sent=YesNoWire(c);Set(c.World,"TradeAskWindow",false);Feed(c,TradeAsk());
            Check(c.World.TradeState=="invited"&&c.YesNoSnapshot().Prompt==null&&sent.Count==0,"trade flag0 no question and no invented auto reply");
            Check(await c.Trade("reject",c.World.TradeRevision,c.World.WorldEpoch,c.World.TradeGeneration)&&sent.Single().SequenceEqual(TradePackets.Answer(12,false)),"trade flag0 manual reject unchanged");
        }
    }
}
