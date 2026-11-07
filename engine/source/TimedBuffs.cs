namespace P06;
// Server formats and the verified/inferred split: docs/protocol/timed-buffs.md.
public static class TimedBuffRules {
    public const int PhysicalEnchantStr=42,DressMighty=109,PhysicalEnchantDex=26,DressDexterity=110;
    // Item status with no skill id; negative keys are the MagicStatus convention for those.
    public const int WaterBreath=-3126;
    // Inferred: the wave shape is not taken from the original client. Calibrate at M6 smoke.
    public static Point ShakeOffset(int level,long elapsedMs){
        if(level is not (1 or 2 or 3))return Point.Empty;
        double seconds=elapsedMs/1000.0;int amplitude=level*2;
        return new((int)Math.Round(amplitude*Math.Sin(seconds*2*Math.PI/1.2)),(int)Math.Round(amplitude*.5*Math.Sin(seconds*2*Math.PI/1.7)));
    }
}
public sealed partial class WorldSession {
    int liquorLevel,liquorEpoch;long liquorAt;bool liquorWatchesSpeed;
    // S206 clears the client's visual state and the server resends S103; Reset also moves the epoch.
    public int LiquorLevel=>liquorEpoch==WorldEpoch?liquorLevel:0;
    public Point LiquorShake=>TimedBuffRules.ShakeOffset(LiquorLevel,MagicStatusClock()-liquorAt);
    void ReceiveStrup(LoginReader r){
        int seconds=r.H();byte strength=r.C(),weight=r.C(),type=r.C();r.Finish();
        if(Status!=null)Status=Status with{Strength=strength,Weight=weight}; // Full S8 supplies both otherwise.
        ReceiveAbilityUp(TimedBuffRules.PhysicalEnchantStr,TimedBuffRules.DressMighty,"strup",seconds,type);
    }
    void ReceiveDexup(LoginReader r){
        int seconds=r.H();byte dexterity=r.C(),type=r.C();r.Finish();
        if(Status!=null)Status=Status with{Dexterity=dexterity};
        ReceiveAbilityUp(TimedBuffRules.PhysicalEnchantDex,TimedBuffRules.DressDexterity,"dexup",seconds,type);
    }
    // Type is the bonus: dress skills send 2, physical enchant its configured value, then 1 on stop.
    // A physical enchant configured to 2 starts in the dress slot, so a stop falls back to the other slot.
    void ReceiveAbilityUp(int physical,int dress,string source,int seconds,byte type){
        int skill=type==2?dress:physical;
        if(seconds==0&&!magicBuffs.ContainsKey(skill))skill=skill==dress?physical:dress;
        ReceiveBuff(skill,seconds,source);
    }
    void ReceiveLiquor(LoginReader r){
        uint id=r.D();byte type=r.C();r.Finish();
        // Type 8 is the third-speed cake state, sent for any object; it is not drunkenness.
        if(type>3){Unhandled(103);return;}
        if(id!=(Status?.ObjectId??Actor?.ObjectId))return;
        // The server ends drunkenness on haste without any S103.
        if(!liquorWatchesSpeed){liquorWatchesSpeed=true;SpeedChanged+=()=>{if(Actor?.Speed==1)liquorLevel=0;};}
        liquorLevel=type;liquorEpoch=WorldEpoch;liquorAt=MagicStatusClock();
    }
    void ReceiveBlessOfEva(LoginReader r){
        uint id=r.D();int seconds=r.H();r.Finish();
        if((Status?.ObjectId??Actor?.ObjectId) is uint own&&own!=id)return;
        ReceiveBuff(TimedBuffRules.WaterBreath,seconds,"bless-of-eva",name:"水中呼吸");
    }
    void ReceiveDelSkill(LoginReader r,byte[] plain){
        // Same frame as S164: tier flag, 28 or 32 masks, padded to 8. Decode fully before mutating.
        if(plain.Length is not (32 or 40))throw new ProtocolException("truncated skill masks");
        byte tier=r.C();if(tier is not (32 or 50 or 100))throw new ProtocolException("unknown skill tier");
        int groups=plain.Length==40?32:28;var removed=new List<int>();
        for(int row=0;row<groups;row++){byte mask=r.C();for(int bit=0;bit<8;bit++)if((mask&(1<<bit))!=0)removed.Add(row*8+bit+1);}
        r.Finish();RemoveLearnedMagic(removed);
    }
}
public sealed partial class ClientForm {
    // Program.OnPaint raises Paint from base.OnPaint before it draws, on the same Graphics.
    protected override void CreateHandle(){Paint-=PaintLiquorShake;Paint+=PaintLiquorShake;base.CreateHandle();}
    void PaintLiquorShake(object? sender,PaintEventArgs e){if(OnlineMode&&login.World.Renderable)Renderer.ApplyScreenShake(e.Graphics,login.World.LiquorShake);}
}
