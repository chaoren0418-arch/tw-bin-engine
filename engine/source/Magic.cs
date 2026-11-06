using System.Buffers.Binary;
namespace P06;
public sealed record LearnedMagic(int Id,string Name,bool Supported,string Target,int NominalMp);
public sealed record MagicEffect(long Number,uint Caster,uint Target,int Gfx,int Action,ushort DamageWord,int Heading,ushort SourceX,ushort SourceY,ushort TargetX,ushort TargetY,long At,uint ServerSequence=0,ushort CasterGfx=0){public ActionCadence? Timing{get;init;}public long? ActionAt{get;init;}public int? CasterWeapon{get;init;}}
public static class MagicRules {
    // Verified skill 4 template, ranged=10. Other supported skills keep their targeting rules.
    public static bool InRange(int skill,int x,int y,int targetX,int targetY)=>skill!=4||CombatRules.TileDistance(x,y,targetX,targetY)<=10;
    public static LearnedMagic Describe(int id)=>MagicSpells.Get(id) is { } s?new(id,s.Name,true,s.Target=="none"?"自己":s.Target=="attack"?"指定敵人":"自己／指定對象",s.Mp):new(id,$"原服技能 {id}",false,"尚未支援",0);
    // Original spr_action gfx0 rows18/19, SprTable: framecount*40*24/framerate.
    static readonly Lazy<int> release=new(()=>Json.Read(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets/objects/0.json"))).GetProperty("castReleaseMs").GetProperty("spellDir").GetInt32());
    public static int ReleaseMs=>release.Value;
    public static int CastMs(int id)=>Math.Max(MagicSpells.Get(id)?.ReuseMs??0,MagicSpells.Get(id)?.Action==19?1480:1320);
    public static int CastMsFor(int id,int gfx,int weapon)=>Math.Max(MagicSpells.Get(id)?.ReuseMs??0,PlayerProfiles.Get(gfx,weapon)?.ServerActionMs[MagicSpells.Get(id)?.Action==19?"spellNoDir":"spellDir"]??throw new InvalidDataException("Unsupported player casting cadence"));
    public static int ReleaseFor(int gfx)=>PlayerProfiles.ForGfx(gfx)?.CastReleaseMs["spellDir"]??ReleaseMs;
    public static byte[] Request(int skill,uint target,ushort x,ushort y){if(skill is <1 or >220||MagicSpells.Get(skill) is not { } spell)throw new ArgumentOutOfRangeException(nameof(skill));byte[] b=new byte[16];b[0]=6;b[1]=(byte)((skill-1)>>3);b[2]=(byte)((skill-1)&7);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(3),target);if(spell.Wire=="DHH"){BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(7),x);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(9),y);}return b;}
}
public sealed partial class WorldSession {
    readonly HashSet<int> learnedMagic=[];
    readonly List<MagicEffect> magicEffects=[];
    public LearnedMagic[] LearnedMagic=>learnedMagic.Order().Select(id=>MagicRules.Describe(id) with{Name=SkillCatalog?.Get(id)?.Name??MagicRules.Describe(id).Name}).ToArray();
    public MagicEffect[] MagicEffects=>magicEffects.ToArray();
    public int? SelectedMagicId {get;private set;}
    public bool MagicTargetSelf {get;private set;}=true;
    public bool MagicTargetsOwn(int id)=>MagicSpells.Get(id)?.Target=="none"||MagicSpells.Get(id)?.Target=="buff"&&MagicTargetSelf;
    public bool SelectMagicTarget(uint? id){if(id!=null){var target=Objects.FirstOrDefault(o=>o.ObjectId==id&&!o.Dead&&!o.AppearancePending);if(target==null)return false;SelectTarget(id);}MagicTargetSelf=id==null;MagicSelectionRevision++;MagicRevision++;return true;}
    public void UseSelectedMagicTarget(bool self){MagicTargetSelf=self;MagicSelectionRevision++;MagicRevision++;}
    public int MagicRevision {get;private set;}
    public int MagicSelectionRevision {get;private set;}
    public bool MagicListReceived {get;private set;}
    public long MagicEffectCount {get;private set;}
    public string MagicNotice {get;internal set;}="等待原服已學技能清單";
    public bool SelectMagic(int? id){if(id!=null&&!learnedMagic.Contains(id.Value))return false;SelectedMagicId=id;MagicSelectionRevision++;MagicRevision++;return true;}
    void ResetMagic(){learnedMagic.Clear();magicEffects.Clear();magicBuffs.Clear();PartialAcByte=null;SelectedMagicId=null;MagicTargetSelf=true;MagicListReceived=false;MagicEffectCount=0;MagicRevision++;MagicSelectionRevision++;MagicNotice="等待原服已學技能清單";}
    void ClearMagicEffects(){magicEffects.Clear();MagicSelectionRevision++;}
    void AddMagicEffect(MagicEffect e){
        var caster=Objects.FirstOrDefault(o=>o.ObjectId==e.Caster);
        e=e with{CasterGfx=Actor?.ObjectId==e.Caster?Actor.Gfx:caster?.Gfx??e.CasterGfx,CasterWeapon=Actor?.ObjectId==e.Caster?Actor.Weapon:caster?.Action};
        magicEffects.Add(e);magicEffects.RemoveAll(x=>e.At-x.At>15000);MagicRevision++;
    }
    bool ReceiveMagic(byte opcode,LoginReader r,byte[] plain){
        if(ReceiveMagicStatus(opcode,r,plain))return true;
        if(opcode is 164 or 160){
            // Header32/50/100 is a tier flag, not a byte count. Fixed serializers have28 or32 masks, padded8.
            if(plain.Length is not (32 or 40))throw new ProtocolException("truncated skill masks");
            byte tier=r.C();if(tier is not (32 or 50 or 100))throw new ProtocolException("unknown skill tier");
            int groups=plain.Length==40?32:28;var masks=new byte[groups];for(int i=0;i<groups;i++)masks[i]=r.C();r.Finish();
            for(int row=0;row<groups;row++)for(int bit=0;bit<8;bit++)if((masks[row]&(1<<bit))!=0){int id=row*8+bit+1;if(opcode==164)learnedMagic.Add(id);else learnedMagic.Remove(id);}
            if(SelectedMagicId is int selected&&!learnedMagic.Contains(selected))SelectMagic(null);
            MagicListReceived=true;MagicRevision++;MagicNotice=opcode==164?"原服已學技能已更新":"原服移除技能已更新";InteractionSkillsUpdated();return true;
        }
        if(opcode==55&&plain.Length==8&&MagicSpells.AttachedEffect(BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(5)))){uint target=r.D();ushort gfx=r.H();r.Finish();if(gfx is 189 or 191 or 197){LastItemEffect=new{number=++ItemEffectNumber,actor=target,effect=gfx};InventoryRevision++;}var a=Actor;var o=Objects.FirstOrDefault(v=>v.ObjectId==target);if(a?.ObjectId!=target&&o==null){MagicNotice="收到原服特效，對象目前不在可見世界";return true;}ushort x=a?.ObjectId==target?a.X:o!.X,y=a?.ObjectId==target?a.Y:o!.Y;AddMagicEffect(new(++MagicEffectCount,target,target,gfx,19,0,0,x,y,x,y,Environment.TickCount64));MagicNotice="收到原服技能特效；HP／MP與狀態以原回包為準";return true;}
        if(opcode==30&&plain.Length>1&&plain[1]==18){
            if(plain.Length!=40)throw new ProtocolException("truncated magic envelope");
            int action=r.C();uint caster=r.D(),target=r.D();ushort word=r.H();int heading=r.C();uint number=r.D();ushort gfx=r.H();byte flight=r.C();ushort sx=r.H(),sy=r.H(),tx=r.H(),ty=r.H();uint reserved=r.D();byte tail=r.C();r.Finish();
            if(heading>7||reserved!=0||tail!=0)throw new ProtocolException("invalid magic envelope");
            if(gfx!=167||flight!=0){Unhandled(opcode);MagicNotice="收到未支援魔法特效；保留原封包";return true;}
            if(Actor?.ObjectId==caster)Actor=Actor with{Direction=(byte)heading};else UpdateObject(caster,o=>o with{Direction=(byte)heading});
            ushort casterGfx=Actor?.ObjectId==caster?Actor.Gfx:Objects.FirstOrDefault(o=>o.ObjectId==caster)?.Gfx??ushort.MaxValue;
            AnimateCombat(caster,18,target:target);var castAnimation=ActionFor(caster);AddMagicEffect(new(++MagicEffectCount,caster,target,gfx,action,word,heading,sx,sy,tx,ty,ActionClock(),number,casterGfx){Timing=castAnimation?.Timing,ActionAt=castAnimation?.At});MagicNotice=$"原服光箭回報；傷害欄 {word}（非獨立證明的HP差值）";return true;
        }
        return false;
    }
    public object MagicSnapshot()=>new{received=MagicListReceived,learned=LearnedMagic,selectedId=SelectedMagicId,targetSelf=MagicTargetSelf,effectiveTargetSelf=EffectiveMagicTargetSelf,targetName=MagicTargetName,buffs=MagicBuffs,buffNotice=MagicBuffNotice,ac=CurrentAc,acNotice=AcNotice,partialAcByte=PartialAcByte,revision=MagicRevision,effectCount=MagicEffectCount,effects=MagicEffects,notice=MagicNotice};
}
public sealed partial class LoginConnection {
    long nextMagicAt;bool magicSending;int magicLease;
    public event Action<object>? MagicSent;
    public bool MagicBusy=>magicSending||ActionClock()<nextMagicAt;
    public bool MagicAvailable=>MovementAvailable&&World.SelectedMagicId is int id&&World.LearnedMagic.Any(m=>m.Id==id&&m.Supported)&&(World.MagicTargetsOwn(id)||World.Target is {Dead:false,AppearancePending:false});
    void ResetMagicSend(){magicLease++;magicSending=false;nextMagicAt=0;lastMagicTiming=null;lastMagicAdmission=-1;}
    public async Task<bool> CastSelectedMagic(){
        if(!MagicAvailable||MovementBusy||sendMovementGate==null){World.MagicNotice="未送出：請選已學且支援的魔法、有效目標，並等待動作完成";return false;}
        int id=World.SelectedMagicId!.Value;var a=World.Actor!;var t=World.Target;bool self=World.MagicTargetsOwn(id);uint target=self?a.ObjectId:t!.ObjectId;ushort x=self?a.X:t!.X,y=self?a.Y:t!.Y;
        if(!MagicRules.InRange(id,a.X,a.Y,x,y)){
            World.MagicNotice=$"未送出：目標距離 {CombatRules.TileDistance(a.X,a.Y,x,y)} 格，光箭需在 10 格內；請走近、停下後再施法。";
            return false;
        }
        var spell=MagicSpells.Get(id)!;var timing=ActionCadence.Capture(a.Gfx,a.Weapon,spell.Action,a.Speed,a.Brave,spell.ReuseMs,100);int castMs=timing.WaitMs;int epoch=generation,we=World.WorldEpoch,sel=World.MagicSelectionRevision,ts=World.TargetRevision,input=inputVersion,correction=World.Corrections,appearanceRevision=World.AppearanceRevision,speedRevision=World.SpeedRevision,lease=++magicLease;var sender=sendMovementGate;byte[] bytes=MagicRules.Request(id,target,x,y);magicSending=true;
        bool CompletionCurrent()=>epoch==generation&&lease==magicLease&&we==World.WorldEpoch&&input==inputVersion&&(self||ts==World.TargetRevision&&World.TargetId==target&&World.Target is {Dead:false,AppearancePending:false})&&correction==World.Corrections&&appearanceRevision==World.AppearanceRevision&&!World.SelfAppearancePending&&!World.OwnDead&&World.Status is {Hp:>0}&&World.Actor?.ObjectId==a.ObjectId&&World.Actor?.Gfx==a.Gfx&&World.Actor?.Weapon==a.Weapon;
        try {
            bool sent=await sender(bytes,()=>CompletionCurrent()&&epoch==generation&&we==World.WorldEpoch&&sel==World.MagicSelectionRevision&&input==inputVersion&&correction==World.Corrections&&appearanceRevision==World.AppearanceRevision&&speedRevision==World.SpeedRevision&&MagicAvailable&&World.Actor?.ObjectId==a.ObjectId&&World.Actor?.X==a.X&&World.Actor?.Y==a.Y&&World.Actor?.Gfx==a.Gfx&&World.Actor?.Weapon==a.Weapon&&!combatSending&&ActionAdmissionReady&&(self||ts==World.TargetRevision&&World.TargetId==target&&World.Target?.X==x&&World.Target?.Y==y),()=>{lastMagicTiming=timing;lastMagicAdmission=ActionClock();magicTimingEpoch=World.WorldEpoch;nextMagicAt=lastMagicAdmission+castMs;int dx=Math.Sign(x-a.X),dy=Math.Sign(y-a.Y);int heading=Enumerable.Range(0,8).FirstOrDefault(h=>MovementRules.Dx[h]==dx&&MovementRules.Dy[h]==dy,a.Direction);World.PredictAction(spell.Action,heading,target,timing);World.MagicNotice="單次施法已送出；等待原服結果（不預扣HP／MP）";Set(State,World.MagicNotice);});
            if(!CompletionCurrent())return false;
            if(!sent){World.MagicNotice="未送出：目標已移動或動作狀態已改變；請確認目標後再施放一次。";return false;}nextMagicAt=Math.Max(nextMagicAt,ActionClock()+castMs);MagicSent?.Invoke(new{utc=DateTimeOffset.UtcNow,monotonicMs=Environment.TickCount64,skill=id,target,plainHex=Convert.ToHexString(bytes),worldEpoch=we});return true;
        }catch(Exception ex){if(CompletionCurrent()){Cancel();Set(LoginState.Faulted,"施法傳送失敗："+ex.Message);}return false;}
        finally{if(epoch==generation&&lease==magicLease)magicSending=false;}
    }
}
