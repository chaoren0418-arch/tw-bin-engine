namespace P06;
public sealed record MagicBuff(int Skill,string Name,int Seconds,long ReceivedAt,string Source) {
    public int Remaining(long now)=>(int)Math.Max(0,Seconds-Math.Max(0,now-ReceivedAt)/1000);
}
public sealed partial class WorldSession {
    readonly System.Collections.Concurrent.ConcurrentDictionary<int,MagicBuff> magicBuffs=new();
    internal Func<long> MagicStatusClock=()=>Environment.TickCount64;
    public byte? PartialAcByte {get;private set;}
    public int? CurrentAc=>PartialAcByte==null?Status?.Ac:null;
    public string AcNotice=>PartialAcByte!=null?"AC：等待伺服器完整更新":CurrentAc is int ac?$"AC {ac}":"等待原服 AC";
    public MagicBuff[] MagicBuffs=>magicBuffs.Values.OrderBy(b=>b.Skill).ToArray();
    public string MagicBuffNotice=>string.Join("；",MagicBuffs.Select(b=>b.Name+"："+(b.Remaining(MagicStatusClock())>0?$"剩餘 {b.Remaining(MagicStatusClock())} 秒":"期限已到，等待原服更新")));
    public bool EffectiveMagicTargetSelf=>SelectedMagicId is int id&&MagicTargetsOwn(id);
    public string MagicTargetName=>SelectedMagicId==null?"尚未選擇技能":EffectiveMagicTargetSelf?"自己（"+(Actor?.Name??"尚未進入世界")+"）":Target?.Name??"尚未選擇對象";
    void ReceiveBuff(int skill,int seconds,string source,bool remove=false){
        if(remove||seconds==0){if(magicBuffs.TryGetValue(skill,out var prior)&&(source=="proxy"||prior.Source==source))magicBuffs.TryRemove(skill,out _);}
        else magicBuffs[skill]=new(skill,MagicSpells.Get(skill)!.Name,seconds,MagicStatusClock(),source);
        MagicRevision++;
    }
    bool ReceiveMagicStatus(byte opcode,LoginReader r,byte[] plain){
        if(opcode==174){
            if(plain.Length!=16)throw new ProtocolException("truncated own defence");
            // ConfigClient.isROClient is true for this product's CLIENT_LOGIN_TYPE=2.
            // L1Character permits AC below -211, so a C field cannot recover the full integer.
            byte wireAc=r.C();short fire=unchecked((short)r.H()),water=unchecked((short)r.H()),wind=unchecked((short)r.H()),earth=unchecked((short)r.H());r.Finish();
            PartialAcByte=wireAc;
            if(Status!=null)Status=Status with{Fire=fire,Water=water,Wind=wind,Earth=earth};
            MagicRevision++;return true;
        }
        if(opcode==216){
            if(plain.Length!=8)throw new ProtocolException("truncated shield icon");
            int seconds=r.H(),icon=r.C();r.Finish();
            if(icon==5)ReceiveBuff(3,seconds,"shield");else Unhandled(opcode);return true;
        }
        if(opcode!=250||plain.Length<2||plain[1]!=154)return false;
        r.C();int duration=r.H();
        // In this pinned normal path S_MagicEquipment is only used to clear the native icon.
        if(plain.Length==8){uint gfx=r.D();r.Finish();if(gfx==2165&&duration==0)ReceiveBuff(8,0,"native-weapon");else Unhandled(opcode);return true;}
        if(plain.Length!=16)throw new ProtocolException("unsupported skill icon length");
        int key=r.H(),reserved=r.H(),remove=r.H();r.Finish();
        if(reserved!=0||remove is not (0 or 1))throw new ProtocolException("invalid skill icon fields");
        int skill=key switch{524=>3,535=>8,_=>0};if(skill!=0)ReceiveBuff(skill,duration,"proxy",remove==1);else Unhandled(opcode);return true;
    }
}
