namespace P06;

// Wire order is STR, INT, WIS, DEX, CON, CHA (not character-list order).
public readonly record struct ResetStats(int Str,int Int,int Wis,int Dex,int Con,int Cha) {
    public int[] Values=>[Str,Int,Wis,Dex,Con,Cha];
    public int Sum=>Str+Int+Wis+Dex+Con+Cha;
    public ResetStats Add(int index,int delta){var v=Values;if(index is <0 or >5)throw new ArgumentOutOfRangeException(nameof(index));v[index]+=delta;return From(v);}
    public static ResetStats From(int[] v)=>new(v[0],v[1],v[2],v[3],v[4],v[5]);
}
public sealed record CharResetPacket(byte Kind,int Level,int MaxLevel,int Hp,int Mp,int Ac,ResetStats Stats,int Points);
public static class CharResetPackets {
    public static ResetStats BaseStats(int type){var b=CharacterManagementPackets.BaseStats(type);return new(b[0],b[5],b[3],b[1],b[2],b[4]);}
    public static bool InitialValid(int type,ResetStats stats)=>type is >=0 and <7&&stats.Sum==75&&stats.Values.Zip(BaseStats(type).Values).All(p=>p.First>=p.Second&&p.First<=p.Second+75-BaseStats(type).Sum);
    static byte[] StatsPacket(byte stage,ResetStats stats){if(stats.Values.Any(v=>v is <1 or >35))throw new ArgumentException("reset stats");return LoginPackets.Pad([98,stage,..stats.Values.Select(v=>(byte)v)]);}
    public static byte[] Initial(int type,ResetStats stats)=>InitialValid(type,stats)?StatsPacket(1,stats):throw new ArgumentException("initial allocation");
    public static byte[] Level(byte action,byte finalAttribute=0){if(action>8||finalAttribute>6||action!=8&&finalAttribute!=0)throw new ArgumentException("reset action");return LoginPackets.Pad(action==8?[98,2,8,finalAttribute]:[98,2,action]);}
    public static byte[] Elixir(ResetStats baseline,ResetStats stats,int points)=>points is >0 and <=255&&stats.Sum-baseline.Sum==points&&stats.Values.Zip(baseline.Values).All(p=>p.First>=p.Second)?StatsPacket(3,stats):throw new ArgumentException("elixir allocation");
    public static CharResetPacket Parse(byte[] plain){
        var r=new LoginReader(plain);if(r.C()!=64)throw new ProtocolException("reset opcode");byte kind=r.C();
        CharResetPacket packet;
        switch(kind){
        case 1:
            int hp=r.H(),mp=r.H(),ac=r.C(),max=r.C();
            if(max==0||ac!=10||!(hp==14&&mp==2||hp==16&&mp==1||hp==15&&mp==4||hp==12&&mp==6||hp==12&&mp==3))throw new ProtocolException("reset offer");
            packet=new(kind,1,max,hp,mp,ac,default,0);break;
        case 2:
            int lv=r.C(),maximum=r.C(),health=r.H(),mana=r.H(),armor=unchecked((short)r.H());
            var stats=new ResetStats(r.C(),r.C(),r.C(),r.C(),r.C(),r.C());
            if(lv<1||lv>maximum||stats.Values.Any(v=>v<1))throw new ProtocolException("reset progress");
            packet=new(kind,lv,maximum,health,mana,armor,stats,0);break;
        case 3:packet=new(kind,0,0,0,0,0,default,r.C());break;
        case 4:
            int a=r.C(),b=r.C(),c=r.C();if(r.C()!=0||r.H()!=0)throw new ProtocolException("reset info reserved");
            packet=new(kind,0,0,0,0,0,new(a&15,a>>4,b&15,b>>4,c&15,c>>4),0);break;
        default:throw new ProtocolException("unowned reset subtype");
        }
        r.Finish();return packet;
    }
}
public sealed record CharResetView(int Generation,int Epoch,int Revision,uint ActorId,int Class,string Phase,int Level,int MaxLevel,int Hp,int Mp,int Ac,ResetStats Stats,int Points,ResetStats? OriginalBonus,bool CanAct,string Notice);

public sealed partial class WorldSession {
    CharResetPacket? resetPacket;
    ResetStats? resetOriginalBonus;
    string resetPhase="idle";
    int resetEpoch,resetRevision,resetExpectedLevel,resetInfoEpoch;
    uint resetInfoOwner;
    uint resetOwner;
    OwnStatus? resetOfferIdentity;
    ResetStats resetExpectedStats;
    long resetSentAt;
    internal Func<long> CharResetClock=()=>Environment.TickCount64;
    internal void CheckCharReset(){
        if(resetInfoEpoch!=WorldEpoch||resetInfoOwner!=Status?.ObjectId)resetOriginalBonus=null;
        if(resetPacket==null)return;
        // Native NPC sends the offer immediately after starting its teleport to map 997.
        // Bind the unsent offer to that arrival; once C98 is admitted, any world change retires it.
        if(resetPhase=="initial"&&resetEpoch!=WorldEpoch&&ReferenceEquals(resetOfferIdentity,Status)){resetEpoch=WorldEpoch;resetRevision++;}
        if(resetEpoch!=WorldEpoch||resetOwner!=Status?.ObjectId){resetPacket=null;resetOriginalBonus=null;resetPhase="ended";resetRevision++;return;}
        if(resetPhase is "waiting" or "finishing" or "saving"&&CharResetClock()-resetSentAt>=15000){resetPhase="uncertain";resetRevision++;}
    }
    void ReceiveCharReset(byte[] plain){
        var p=CharResetPackets.Parse(plain);CheckCharReset();
        if(p.Kind==4){resetOriginalBonus=p.Stats;resetInfoEpoch=WorldEpoch;resetInfoOwner=Status?.ObjectId??0;resetRevision++;return;}
        if(p.Kind==1){
            if(Status==null||Status.ObjectId==0)return;
            if(resetPacket!=null&&resetPhase is not ("review" or "ended"))return;
            resetPacket=p;resetEpoch=WorldEpoch;resetOwner=Status.ObjectId;resetOfferIdentity=Status;resetPhase="initial";resetRevision++;
            CharResetUi.Offer(this);return;
        }
        if(resetPacket==null)return;
        if(p.Kind==2&&resetPhase=="waiting"&&p.MaxLevel==resetPacket.MaxLevel&&p.Level==resetExpectedLevel&&p.Stats==resetExpectedStats){resetPacket=p;resetPhase="level";resetRevision++;}
        else if(p.Kind==3&&resetPhase=="finishing"&&p.Points>0){resetPacket=resetPacket with{Points=p.Points};resetPhase="elixir";resetRevision++;}
    }
    internal CharResetView ResetView(int gen,int type,bool access){
        CheckCharReset();var p=resetPacket;var stats=resetPhase=="initial"&&type is >=0 and <7?CharResetPackets.BaseStats(type):p?.Stats??default;
        return new(gen,WorldEpoch,resetRevision,Status?.ObjectId??0,type,resetPhase,p?.Level??0,p?.MaxLevel??0,p?.Hp??0,p?.Mp??0,p?.Ac??0,stats,resetPhase=="initial"?75-stats.Sum:resetPhase=="elixir"?p!.Points:Math.Max(0,(p?.MaxLevel??0)-Math.Max(50,p?.Level??0)),resetOriginalBonus,access&&p!=null&&type is >=0 and <7&&resetPhase is "initial" or "level" or "elixir",resetPhase switch{
            "initial"=>"分配出生能力後確認；原服收到後即開始重置。",
            "level"=>"逐步恢復等級；51 級起每級選擇一項能力。",
            "elixir"=>"分配原服提供的萬能藥點數後確認。",
            "waiting" or "finishing" or "saving"=>"等待原服回覆；不會自動重送。",
            "uncertain"=>"回覆逾時，結果未知；請重新登入核對，不要重送。",
            "review"=>"收到角色狀態更新；請核對能力與重新登入後的保存結果。",
            _=>"此重置流程已結束或失效；請核對原服狀態。"});
    }
    internal void AdmitReset(string command,ResetStats stats,int step){
        resetSentAt=CharResetClock();
        resetPhase=command=="finish"?"finishing":command=="elixir"?"saving":"waiting";
        resetExpectedLevel=command=="initial"?1:resetPacket!.Level+(step==7?10:1);resetExpectedStats=stats;resetRevision++;
    }
}
public sealed partial class LoginConnection {
    int resetSendingGeneration=-1;
    bool ResetAccess=>State is LoginState.InWorld or LoginState.EnteringWorld&&World.Status is {Hp:>0}&&!World.OwnDead&&!World.TeleportPending&&!World.TradeBlocksActions&&!MovementBusy&&!interactionSending;
    public CharResetView CharResetSnapshot(){
        int type=characters.FirstOrDefault(c=>c.Name==(World.Actor?.Name??worldSelection))?.Class??-1;
        return World.ResetView(generation,type,ResetAccess&&resetSendingGeneration!=generation);
    }
    public async Task<bool> SendCharReset(CharResetView stamp,string command,ResetStats stats=default,int step=0){
        var v=CharResetSnapshot();
        bool Current(){var now=CharResetSnapshot();return ResetAccess&&now.Generation==stamp.Generation&&now.Epoch==stamp.Epoch&&now.ActorId==stamp.ActorId&&now.Revision==stamp.Revision&&now.Class==stamp.Class;}
        if(!v.CanAct||!Current()||sendMovementGate==null)return false;
        byte[] bytes;ResetStats expected=v.Stats;
        try{
            switch(command){
            case "initial" when v.Phase=="initial":bytes=CharResetPackets.Initial(v.Class,stats);expected=stats;break;
            case "level" when v.Phase=="level"&&v.Level<v.MaxLevel:
                if(step==7){if(v.MaxLevel-v.Level<10||v.Level+10>50)return false;}
                else if(v.Level<50){if(step!=0)return false;}
                else {if(step is <1 or >6||v.Stats.Values[step-1]>=35)return false;expected=v.Stats.Add(step-1,1);}
                bytes=CharResetPackets.Level((byte)step);break;
            case "finish" when v.Phase=="level"&&v.Level==v.MaxLevel:
                // All level points were assigned on the way to MaxLevel; final attribute 0 adds none.
                bytes=CharResetPackets.Level(8,0);break;
            case "elixir" when v.Phase=="elixir":bytes=CharResetPackets.Elixir(v.Stats,stats,v.Points);break;
            default:return false;
            }
        }catch(ArgumentException){return false;}
        int session=generation;resetSendingGeneration=session;
        try{bool sent=await sendMovementGate(bytes,Current,()=>World.AdmitReset(command,expected,step));return sent&&generation==session;}
        catch(Exception){if(generation==session){Cancel();Set(LoginState.Faulted,"重置請求傳送失敗；請重新登入核對，沒有自動重送。");}return false;}
        finally{if(resetSendingGeneration==session)resetSendingGeneration=-1;}
    }
}
