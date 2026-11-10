using P06;
using System.Text.Json;
using System.Windows.Forms;

partial class Checks {
    // Every synthetic server packet enters the product's WorldSession.Receive via Feed.
    static byte[] RezPrompt(ushort message=321,ushort header=0,uint token=0)=>Packet(b=>{b.Write((byte)219);b.Write(header);b.Write(token);b.Write(message);});
    static byte[] MoveLockPacket(byte mode=0)=>LoginPackets.Pad([241,mode]);
    static void RezSender(LoginConnection c,List<byte[]> sent)=>Sender(c,(b,valid,admit)=>{if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);});
    static LoginConnection RezDead(){var c=Seed();Feed(c,Hp(0));Feed(c,Death(123));return c;}
    static void RezBad(LoginConnection c,byte[] bytes,string label){
        string before=JsonSerializer.Serialize(c.ResurrectionSnapshot());bool rejected=false;
        try{Feed(c,bytes);}catch(ProtocolException){rejected=true;}
        Check(rejected,label+" rejected");Check(before==JsonSerializer.Serialize(c.ResurrectionSnapshot()),label+" atomic");
    }
    static void MoveLockBad(LoginConnection c,byte[] bytes,string label){
        bool rejected=false;try{Feed(c,bytes);}catch(ProtocolException){rejected=true;}
        Check(rejected&&!c.World.MoveLocked,label+" rejected without locking");
    }
    static async Task<bool> RezDrift(LoginConnection c,int heading)=>await (Task<bool>)typeof(LoginConnection).GetMethod("DriftProbe",F)!.Invoke(c,[heading])!;
    static int RezUnhandled(LoginConnection c,byte opcode)=>((System.Collections.Concurrent.ConcurrentDictionary<byte,int>)typeof(WorldSession).GetField("unhandled",F)!.GetValue(c.World)!).GetValueOrDefault(opcode);

    [CheckGroup("resurrection")]
    static async Task ResurrectionChecks(){
        CombatAdmissionFixture(); // Authored metadata, never original game assets or sockets.
        await ResurrectionWire();await ResurrectionAnswers();await ResurrectionInvalidation();await ResurrectionRaces();ResurrectionPanel();await MoveLockInput();await MoveLockRelease();
        // Authored all-open terrain, so the existing death flow also runs without game assets (for example in CI).
        File.WriteAllBytes(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets/terrain0.bin")),Enumerable.Repeat((byte)3,Coordinates.Columns*Coordinates.Rows).ToArray());
        await DeathAndRecovery();await RecoveryRaces();KeyboardBusy123();
    }
    static Task ResurrectionWire(){
        Check(Convert.ToHexString(RezPrompt(321))=="DB00000000000041"+"0100000000000000","S219/321 H0 D0 H vector");
        Check(Convert.ToHexString(RezPrompt(322))=="DB00000000000042"+"0100000000000000","S219/322 H0 D0 H vector");
        Check(Convert.ToHexString(ResurrectionPackets.Answer(321,true))=="79000000000000410101000000000000","C121 321 accept vector");
        Check(Convert.ToHexString(ResurrectionPackets.Answer(321,false))=="79000000000000410100000000000000","C121 321 reject vector");
        Check(Convert.ToHexString(ResurrectionPackets.Answer(322,true))=="79000000000000420101000000000000","C121 322 accept vector");
        Check(Convert.ToHexString(ResurrectionPackets.Answer(322,false))=="79000000000000420100000000000000","C121 322 reject vector");
        Check(Convert.ToHexString(MoveLockPacket())=="F100000000000000","S241 C0 vector");
        Check(Convert.ToHexString(Resurrection())=="557B0000000015030000000000000000","S85 D C D D vector");
        foreach(ushort message in new ushort[]{0,97,252,320,323,622,654}){
            bool rejected=false;try{ResurrectionPackets.Answer(message,true);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"answer encoder refuses non-resurrection message "+message);
        }
        return Task.CompletedTask;
    }
    static async Task ResurrectionAnswers(){
        foreach(ushort message in new ushort[]{321,322}){
            using var c=RezDead();var sent=new List<byte[]>();RezSender(c,sent);
            Feed(c,RezPrompt(message));var v=c.ResurrectionSnapshot();
            Check(v is {Phase:"invited",CanAnswer:true}&&v.Message==message,"S219 opens prompt "+message);
            Check(await c.AnswerResurrection(v,true)&&sent.Count==1&&sent[0].SequenceEqual(ResurrectionPackets.Answer(message,true)),"accept sends one C121 "+message);
            Check(c.World.OwnDead&&!c.MovementAvailable&&c.ResurrectionSnapshot().Phase=="accepting","accept is not a local resurrection "+message);
            Check(!await c.AnswerResurrection(v,true)&&!await c.AnswerResurrection(c.ResurrectionSnapshot(),true)&&sent.Count==1,"answer cannot replay "+message);
            Check(c.CanRestart,"restart stays reachable when the server does not answer "+message);
            Feed(c,Resurrection());
            Check(c.ResurrectionSnapshot() is {Phase:"resurrected",CanAnswer:false}&&!c.World.OwnDead,"S85 completes accepted prompt "+message);
            Check(c.World.SelfAppearancePending&&!c.MovementAvailable,"existing S85 wait for HP and S119 preserved "+message);
            Feed(c,Hp(50));Feed(c,Weapon(0));Check(c.MovementAvailable,"existing S85 plus HP/S119 restores movement "+message);
            Check(sent.Count==1,"recovery does not auto-send "+message);
        }
        using(var c=RezDead()){
            var sent=new List<byte[]>();RezSender(c,sent);Feed(c,RezPrompt(322));
            Check(await c.AnswerResurrection(c.ResurrectionSnapshot(),false)&&sent.Single().SequenceEqual(ResurrectionPackets.Answer(322,false)),"reject sends one C121");
            Check(c.ResurrectionSnapshot().Phase=="rejected"&&c.World.OwnDead&&c.CanRestart,"reject keeps death and restart");
            Check(await c.RestartAfterDeath()&&sent.Count==2&&sent[1].SequenceEqual(LoginPackets.Pad([177])),"reject then existing C177 restart");
        }
        using(var c=Seed()){
            var sent=new List<byte[]>();RezSender(c,sent);Feed(c,RezPrompt());
            Check(c.ResurrectionSnapshot() is {Phase:"idle",CanAnswer:false},"living role ignores prompt");
            Set(c.World,"TimeReplies",0);Feed(c,Hp(0));Feed(c,Death(123));Feed(c,RezPrompt());
            Check(c.ResurrectionSnapshot().Phase=="idle","prompt before world ready ignored");
        }
        using(var c=RezDead()){
            Feed(c,RezPrompt());Check(c.ResurrectionSnapshot() is {Phase:"invited",CanAnswer:false},"no sender means not answerable");
            Check(!await c.AnswerResurrection(c.ResurrectionSnapshot(),true),"no sender refused");
        }
    }
    static async Task ResurrectionInvalidation(){
        foreach(var (message,token) in new (ushort,uint)[]{(953,77),(630,0),(252,77),(654,0),(97,0)}){
            using var c=RezDead();var sent=new List<byte[]>();RezSender(c,sent);
            Feed(c,RezPrompt());var v=c.ResurrectionSnapshot();Feed(c,PartyYesNo(message,"其他角色",token));
            Check(c.ResurrectionSnapshot() is {Phase:"expired",CanAnswer:false},"S219 shared slot expires prompt "+message);
            Check(!await c.AnswerResurrection(v,true)&&sent.Count==0,"expired prompt cannot answer "+message);
        }
        using(var c=RezDead()){
            var sent=new List<byte[]>();RezSender(c,sent);
            Feed(c,RezPrompt(321));var old=c.ResurrectionSnapshot();Feed(c,RezPrompt(322));var fresh=c.ResurrectionSnapshot();
            Check(fresh.Message==322&&fresh.Revision>old.Revision,"newer prompt replaces older message");
            Check(!await c.AnswerResurrection(old,true)&&sent.Count==0,"older prompt view cannot answer");
            Check(await c.AnswerResurrection(fresh,true)&&sent.Single().SequenceEqual(ResurrectionPackets.Answer(322,true)),"newest message answered");
        }
        using(var c=RezDead()){
            var sent=new List<byte[]>();RezSender(c,sent);Feed(c,RezPrompt());var v=c.ResurrectionSnapshot();
            Check(await c.RestartAfterDeath(),"restart admitted with open prompt");
            Check(c.ResurrectionSnapshot().Phase=="expired"&&!await c.AnswerResurrection(v,true)&&sent.Count==1,"restart closes prompt so a live role cannot answer");
            Feed(c,RezPrompt());Check(c.ResurrectionSnapshot().Phase=="expired","prompt after restart request ignored");
        }
        using(var c=RezDead()){
            Feed(c,RezPrompt());Feed(c,Resurrection(999));Check(c.ResurrectionSnapshot().Phase=="invited","foreign S85 keeps own prompt");
            Feed(c,Packet(b=>{b.Write((byte)85);b.Write(123u);b.Write((byte)1);b.Write(789u);b.Write(0u);}));
            Check(c.ResurrectionSnapshot().Phase=="invited"&&c.World.OwnDead&&RezUnhandled(c,85)==1,"unknown S85 type keeps prompt and stays unhandled");
            Feed(c,Resurrection());Check(c.ResurrectionSnapshot().Phase=="resurrected","unanswered prompt closes on S85");
        }
        using(var c=RezDead()){
            var sent=new List<byte[]>();RezSender(c,sent);Feed(c,RezPrompt());var v=c.ResurrectionSnapshot();
            Call(c.World,"ClearObjects");Check(c.ResurrectionSnapshot().Phase=="expired"&&!await c.AnswerResurrection(v,true)&&sent.Count==0,"world rebuild expires prompt");
        }
        using(var c=RezDead()){
            var sent=new List<byte[]>();RezSender(c,sent);Feed(c,RezPrompt());var v=c.ResurrectionSnapshot();
            c.World.Reset();Check(c.ResurrectionSnapshot().Phase=="expired"&&!await c.AnswerResurrection(v,true)&&sent.Count==0,"reset expires prompt");
        }
        using(var c=RezDead()){
            Feed(c,RezPrompt());
            RezBad(c,RezPrompt(321,header:1),"prompt nonzero header");
            RezBad(c,RezPrompt(322,token:77),"prompt requestId token");
            RezBad(c,Packet(b=>{b.Write((byte)219);b.Write((ushort)0);b.Write(0u);b.Write((ushort)322);Text(b,"多餘");}),"prompt trailing text");
            var padded=RezPrompt();padded[^1]=1;RezBad(c,padded,"prompt nonzero padding");
            var prompt=RezPrompt();for(int n=1;n<prompt.Length;n++)RezBad(c,prompt[..n],"prompt truncated "+n);
            RezBad(c,Packet(b=>{b.Write((byte)219);b.Write((ushort)1);b.Write(0u);b.Write((ushort)252);Text(b,"x");}),"unrelated bad envelope");
            Check(c.ResurrectionSnapshot().Phase=="invited","malformed S219 keeps live prompt");
        }
        using(var c=Seed()){
            // Observer only: 321 still reaches Trade's existing non-252 route.
            Feed(c,PartyYesNo(252,"target"));Check(c.World.TradeState=="invited","trade fixture invited");
            int unhandled=RezUnhandled(c,219);Feed(c,RezPrompt());
            Check(c.World.TradeState=="attention"&&RezUnhandled(c,219)==unhandled+1,"prompt keeps Trade invalidation and unhandled count");
        }
        using(var c=MarriageSeed()){
            Feed(c,MarriageInvite());Feed(c,RezPrompt());Check(c.MarriageSnapshot() is {Phase:"expired",CanAnswer:false},"prompt expires older marriage invitation");
            Feed(c,PartyYesNo());Feed(c,RezPrompt());Check(c.PartySnapshot().Invitation==null,"prompt expires older party invitation");
        }
    }
    static async Task ResurrectionRaces(){
        using(var c=RezDead()){
            var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,valid,admit)=>{await release.Task;if(!valid())return false;sends++;admit();return true;});
            Feed(c,RezPrompt());var v=c.ResurrectionSnapshot();var pending=c.AnswerResurrection(v,true);
            Check(c.ResurrectionSnapshot().CanAnswer==false&&!await c.AnswerResurrection(v,false),"concurrent answer blocked");
            Feed(c,Resurrection());release.SetResult(true);
            Check(!await pending&&sends==0,"S85 before send gate cancels queued C121");
        }
        using(var c=RezDead()){
            var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,valid,admit)=>{await release.Task;if(!valid())return false;sends++;admit();return true;});
            Feed(c,RezPrompt());var pending=c.AnswerResurrection(c.ResurrectionSnapshot(),true);
            Feed(c,PartyYesNo(630,"決鬥者",0));release.SetResult(true);
            Check(!await pending&&sends==0,"newer S219 before send gate cancels queued C121");
        }
        using(var c=RezDead()){
            var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,valid,admit)=>{await release.Task;if(!valid())return false;sends++;admit();return true;});
            Feed(c,RezPrompt());var pending=c.AnswerResurrection(c.ResurrectionSnapshot(),true);
            c.Cancel();release.SetResult(true);Check(!await pending&&sends==0,"cancel before send gate cancels queued C121");
        }
        using(var c=RezDead()){
            Sender(c,(_,_,_)=>throw new IOException("offline fixture"));Feed(c,RezPrompt());
            Check(!await c.AnswerResurrection(c.ResurrectionSnapshot(),true)&&c.State==LoginState.Faulted,"send failure faults without retry");
        }
    }
    static void ResurrectionPanel(){
        using var c=RezDead();var sent=new List<byte[]>();RezSender(c,sent);var form=KeyboardForm(c);
        var panel=new Panel();var notice=new Label();var yes=new Button();var no=new Button();
        Set(form,"resurrectionPanel",panel);Set(form,"resurrectionNotice",notice);Set(form,"resurrectionYes",yes);Set(form,"resurrectionNo",no);
        Call(form,"SyncResurrection");Check(!panel.Visible,"panel hidden without prompt");
        Feed(c,RezPrompt());Call(form,"SyncResurrection");
        Check(panel.Visible&&yes.Enabled&&no.Enabled&&notice.Text.Contains("復活"),"panel shows prompt with both answers");
        Feed(c,Resurrection());Call(form,"SyncResurrection");Check(!panel.Visible,"panel closes after S85");
    }
    static async Task MoveLockInput(){
        using(var c=Seed()){
            long now=10000;Set(c,"MovementClock",(Func<long>)(()=>now));
            var sent=new List<byte[]>();RezSender(c,sent);
            Check(await RezDrift(c,2)&&sent.Count==1&&sent[0][0]==29,"unlocked fixture sends C29");now+=10000;
            sent.Clear();Feed(c,Pack(123));int revision=c.World.AppearanceRevision;
            Feed(c,MoveLockPacket());Check(c.World.MoveLocked&&!c.MovementAvailable&&c.World.AppearanceRevision==revision+1,"S241 locks movement and invalidates queued input");
            Check(c.World.MotionNotice.Contains("鎖定移動"),"S241 explains the lock");
            Check(!await c.Move(2)&&!await c.Turn(2)&&!await RezDrift(c,2)&&sent.Count==0,"locked API sends zero C29/C225");
            Check(c.World.MotionNotice.Contains("不能移動"),"refusal comes from the lock, not terrain");
            var pointer=new PlayerPointerController(c,()=>true,()=>false,_=>true);
            Check(!await pointer.ActAsync(456,false)&&sent.Count==0,"locked pointer action denied");
            var form=KeyboardForm(c);form.Input.Down(Keys.Up);Call(form,"OnlineTick");
            Check(sent.Count==0,"locked keyboard sends zero C29");CheckKeyboardRefusal(form,"不能移動","move lock keyboard");
            revision=c.World.AppearanceRevision;Feed(c,MoveLockPacket());Check(c.World.AppearanceRevision==revision&&c.World.MoveLocked,"repeated S241 keeps one lock");
            Feed(c,Pack(123));Check(!c.World.MoveLocked&&c.MovementAvailable&&sent.Count==0,"S87 releases without auto-send");
            Check(await RezDrift(c,2)&&sent.Count==1&&sent[0][0]==29,"fresh C29 after release");
        }
        using(var c=Seed()){
            var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,valid,admit)=>{await release.Task;if(!valid())return false;sends++;admit();return true;});
            var pending=RezDrift(c,2);Feed(c,MoveLockPacket());Feed(c,Pack(123));
            Check(!c.World.MoveLocked,"lock already over (S87) when queued send resumes");release.SetResult(true);
            Check(!await pending&&sends==0,"queued C29 from before the lock stays cancelled");
        }
        using(var c=Seed()){
            var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,valid,admit)=>{await release.Task;if(!valid())return false;sends++;admit();return true;});
            var pending=RezDrift(c,2);Feed(c,MoveLockPacket());release.SetResult(true);
            Check(!await pending&&sends==0,"held lock blocks queued C29 at the send gate");
        }
        using(var c=RezDead()){
            var sent=new List<byte[]>();RezSender(c,sent);Feed(c,MoveLockPacket());Feed(c,RezPrompt());
            Check(await c.AnswerResurrection(c.ResurrectionSnapshot(),true)&&sent.Count==1,"move lock does not block resurrection answer");
        }
    }
    static async Task MoveLockRelease(){
        using(var c=Seed()){
            // R5-D (T309/#736): no engine time limit any more; only S87, world rebuild or Reset release the lock.
            Feed(c,MoveLockPacket());Feed(c,MoveLockPacket());Check(c.World.MoveLocked&&!c.MovementAvailable,"lock has no local time limit");
            Feed(c,Pack(123));Check(!c.World.MoveLocked&&c.MovementAvailable,"S87 releases a lock");
        }
        using(var c=Seed()){Feed(c,MoveLockPacket());Call(c.World,"ClearObjects");Check(!c.World.MoveLocked,"world rebuild releases lock");}
        using(var c=Seed()){Feed(c,MoveLockPacket());c.World.Reset();Check(!c.World.MoveLocked,"reset releases lock");}
        using(var c=Seed()){Feed(c,MoveLockPacket());Feed(c,Paralysis(7));Check(c.World.MoveLocked,"lone S202/7 is not a position correction");}
        using(var c=Seed()){
            Feed(c,MoveLockPacket(1));Check(!c.World.MoveLocked&&RezUnhandled(c,241)==1,"undefined S241 mode stays unhandled");
            MoveLockBad(c,[241],"S241 truncated");
            var padded=MoveLockPacket();padded[^1]=1;MoveLockBad(c,padded,"S241 nonzero padding");
            MoveLockBad(c,[..MoveLockPacket(),..new byte[8]],"S241 extra block");
            Set(c.World,"TimeReplies",0);Feed(c,MoveLockPacket());Check(!c.World.MoveLocked,"S241 before world ready ignored");
        }
        await Task.CompletedTask;
    }
}
