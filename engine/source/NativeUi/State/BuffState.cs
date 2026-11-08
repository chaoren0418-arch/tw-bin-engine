using P06.NativeUi;
namespace P06.NativeUi {
    // ui-buff-icons (Issue #473, docs/design/native-ui.md §5.3). Read-only adapter: existing models → effectlist2.xml effect ids.
    // Every mapping below is a row of docs/reference/ui-layout.md §4 "已驗證：舊封包 → effect id" (V3, #458). The engine keeps
    // some packets only partially (no type byte, no duration, no neighbouring snapshot bytes); where the V3 row needs a value the
    // model does not hold, the buff is NOT mapped and is reported as unmapped with the reason — no icon is guessed (rule 5).
    // No packet parsing here; the sources are WorldSession.MagicBuffs (skill + Source tag), OwnActor.Speed/Brave/Flags and the
    // haste timer of NormalHaste.cs.

    /// <summary>
    /// One status effect to show. Seconds: remaining seconds; −1 = no limit (effect duration −1); null = the engine model has no
    /// timer for it (the server sent one, but it was not kept). Expired = the local timer ran out while the server still reports
    /// the state (V3: the original then draws offIcon). Origin is a diagnostic tag ("haste", "shield:99", …).
    /// </summary>
    public sealed record BuffEntry(int Effect,int? Seconds,bool Expired,string Origin,bool DefaultDuration=false);

    /// <summary>A live state the original would show, but which cannot be mapped to one effect id from the engine model.</summary>
    public sealed record BuffUnmapped(string Origin,string Reason);

    /// <summary>
    /// Tooltip strings from the original string table (string-c.tbl, sub_4EAFF0): 930 label, 931 unit, 2854 "time over".
    /// null = table not available → no tooltip text is shown (never invented).
    /// </summary>
    public sealed record BuffTooltipText(string? Label,string? Unit,string? Over) {
        public static readonly BuffTooltipText None=new(null,null,null);
        public const int LabelId=930,UnitId=931,OverId=2854;
    }

    public sealed record BuffState(IReadOnlyList<BuffEntry> Entries,IReadOnlyList<BuffUnmapped> Unmapped,BuffTooltipText Text) {
        public static readonly BuffState Empty=new([],[],BuffTooltipText.None);

        /// <summary>Finds the buff adapter through the bridge wrappers (HpMp → Macro → LeftStatus → Chat → Buff). null = none.</summary>
        public static BuffState? Of(IUiGameState? state){
            for(int depth=0;state!=null&&depth<8;depth++){
                if(state is IUiBuffState b)return b.Buffs;
                state=state switch{UiHpMpGameState h=>h.Base,UiMacroGameState m=>m.Inner,UiLeftStatusGameState l=>l.Inner,UiChatGameState c=>c.Inner,_=>null};
            }
            return null;
        }

        /// <summary>
        /// In-world snapshot. classId: LoginCharacter.Class of the current character (null = unknown); the class numbering
        /// 2 elf / 3 wizard / 4 dark elf / 6 illusionist is the 3.81 server numbering (推測 that TW16's class test uses the same).
        /// </summary>
        public static BuffState From(WorldSession w,int? classId,BuffTooltipText text){
            var entries=new List<BuffEntry>();var unmapped=new List<BuffUnmapped>();
            if(w.Actor is {} actor){
                // S_SkillHaste 255 → TW16 S_SPEED sub_94C530: mode 1 → 0, mode 2 → 1.
                if(actor.Speed is 1 or 2){
                    int effect=actor.Speed==1?0:1;
                    if(w.SpeedEndsAt is long end){long left=end-w.SpeedNow;entries.Add(new(effect,(int)Math.Max(0,(left+999)/1000),left<=0,"haste"));}
                    else entries.Add(new(effect,null,false,"haste"));
                }
                // S_SkillBrave 67 → TW16 S_EMOTION sub_935260. The engine keeps the mode but drops the seconds.
                if(actor.Brave!=0){
                    var (effect,reason)=BuffEffectRules.Brave(actor.Brave,classId);
                    if(effect is int e)entries.Add(new(e,null,false,$"brave:{actor.Brave}"));else unmapped.Add(new($"brave:{actor.Brave}",reason!));
                }
                // S_Invis 171 → TW16 S_INVISIBLE sub_940330: 396 (dark elf with flag byte_13D4403 → 377); seconds = effect duration.
                if((actor.Flags&2)!=0){
                    if(classId==4)unmapped.Add(new("invisible","黑妖時依旗標 byte_13D4403 決定 377／396，旗標的值引擎沒有"));
                    else if(classId==null)unmapped.Add(new("invisible","職業未知，無法排除黑妖（377／396）"));
                    else entries.Add(new(396,null,false,"invisible",DefaultDuration:true));
                }
            }
            long now=w.MagicStatusClock();
            foreach(var b in w.MagicBuffs){
                int left=b.Remaining(now);if(left<=0)continue;
                var (effect,reason)=BuffEffectRules.Map(b.Source,b.Skill,classId);
                string origin=$"{b.Source}:{b.Skill}";
                if(effect is int e)entries.Add(new(e,left,false,origin));else unmapped.Add(new(origin,reason!));
            }
            // One icon per effect (sub_F05A90 updates an existing effect instead of adding a second one).
            var unique=entries.GroupBy(x=>x.Effect).Select(g=>g.First()).ToArray();
            return new(unique,unmapped,text);
        }
    }

    /// <summary>V3 table "舊封包 → effect id" applied to what the engine keeps. (effect, null) or (null, reason).</summary>
    public static class BuffEffectRules {
        public const int ClassElf=2,ClassWizard=3,ClassDarkElf=4,ClassIllusionist=6;
        static (int?,string?) E(int effect)=>(effect,null);
        static (int?,string?) U(string reason)=>(null,reason);

        /// <summary>S_SkillBrave mode → effect (sub_935260). Mode 0 is "remove".</summary>
        public static (int? Effect,string? Reason) Brave(int mode,int? classId)=>mode switch{
            1=>classId is int c?E(c==ClassElf?19:2):U("mode 1 依職業（妖精 19，其他 2），職業未知"),
            3 or 5=>E(2),
            4=>classId is int c?E(c switch{ClassElf=>24,ClassWizard=>42,ClassIllusionist=>153,_=>37}):U("mode 4 依職業（24／42／153／37），職業未知"),
            6=>E(110),
            8=>E(19),
            _=>U($"S_SkillBrave mode {mode} 不在 V3 表"),
        };

        // MagicStatusCatalog.Slots skill → V3 snapshot (250/20, sub_927320) effect. Missing = no row / not read by TW16 / conditional.
        static readonly Dictionary<int,int> Snapshot=new(){
            [32]=45,[71]=48,[78]=43,[104]=33,[47]=51,[56]=50,[55]=40,[158]=46,[167]=52,[153]=55,[134]=54,[133]=53,
            [174]=67,[175]=72,[173]=69,[206]=120,[216]=128,[217]=129,[191]=113,[193]=114,[188]=111,[211]=123,[183]=108,
            // byte 10+11 elemental weapon (engine skill = byte11 + 1): 147/148/154/155/162/165 → 18/21/19/22/20/23.
            [148]=18,[149]=21,[155]=19,[156]=22,[163]=20,[166]=23,
        };
        static readonly Dictionary<int,string> SnapshotConditional=new(){
            [31]="快照 byte 1：TW16 不讀",
            [1017]="快照 byte 61：TW16 不讀",
            [14]="快照 byte 3：byte 182 = 240 時為 480，否則 47；引擎沒有保留 byte 182",
            [111]="快照 byte 17：byte 29 ×4 = 636 時為 68，否則 36；引擎沒有保留 byte 29",
            [176]="快照 byte 23：byte 146–147 = 4400 時為 56，否則 66；引擎沒有保留該值",
            [181]="快照 byte 60：byte 193 = 158 時為 7，否則 107；引擎沒有保留 byte 193",
        };
        // S216 shield icon: engine skill ← icon {5→3, 3→99, 6→151, 7→159, 10→168, other → −1000−icon}; V3 type → effect.
        static readonly Dictionary<int,int> Shield=new(){[99]=5,[3]=482,[151]=6,[159]=7,[168]=8};
        // 250/22 aura: engine skill ← icon; V3 icon → effect.
        static readonly Dictionary<int,int> Aura=new(){[114]=25,[115]=26,[117]=27,[148]=18,[149]=21,[155]=19,[156]=22,[163]=20,[166]=23};
        // 250/154 native weapon gfx: engine skill ← gfx {2165→8, 747→12, 748→21, 2176→48, 2951→107}; V3 gfx → effect.
        static readonly Dictionary<int,int> NativeWeapon=new(){[8]=12,[12]=379,[21]=394,[48]=13,[107]=392};

        /// <summary>MagicBuff (Source tag + skill key, see MagicStatus.cs / TimedBuffs.cs / PacketBoxStatus.cs) → effect.</summary>
        public static (int? Effect,string? Reason) Map(string source,int skill,int? classId){
            switch(source){
            case "bless-of-eva":return skill==TimedBuffRules.WaterBreath?E(TimedBuffRules.WaterBreathEffect):U("未知的 S126 鍵");
            case "shield":
                if(Shield.TryGetValue(skill,out int s))return E(s);
                return skill<=-1000?E(4):U("未知的 S216 鍵");
            case "strup":
                // Engine: type 2 → dress key 109, any other type → 42. V3: type 3 → 34, other → 9.
                return skill==TimedBuffRules.DressMighty?E(9):U("S166 type 3 為 34、其他為 9；引擎沒有保留 type（type ≠ 2）");
            case "dexup":
                return skill==TimedBuffRules.DressDexterity?E(10):U("S188 type 3 為 35、其他為 10；引擎沒有保留 type（type ≠ 2）");
            case "icons1":
                if(Snapshot.TryGetValue(skill,out int n))return E(n);
                if(skill==171)return classId is int c?E(c!=0?58:27):U("快照 byte 26 依職業（非 0 → 58，0 → 27），職業未知");
                return U(SnapshotConditional.GetValueOrDefault(skill)??"快照位置不在 V3 表");
            case "aura":
                if(Aura.TryGetValue(skill,out int a))return E(a);
                if(skill<=-220000){
                    int v=-skill-220000,icon=v/256,variant=v%256;
                    if(icon==119)return E(28);
                    if(icon==221)return variant switch{1=>E(78),2=>E(77),6=>E(132),_=>U($"250/22 icon 221 variant {variant} 不在 V3 表")};
                    return U($"250/22 icon {icon} 不在 V3 表");
                }
                return U("未知的 250/22 鍵");
            case "packetbox34":return U("250/34 type 1 為 38、type 3 為 481（地圖 2465 為 91）；引擎沒有保留 type");
            case "packetbox35":return U("250/35 變身依目前外形查 graphic（查不到為 39），本模組未實作外形查表");
            case "packetbox36":return E(63);
            case "packetbox40":return E(41);
            case "wind-shackle":return E(52);
            case "wisdom":return E(3);
            case "native-weapon":return NativeWeapon.TryGetValue(skill,out int g)?E(g):U("未知的 250/154 gfx 鍵");
            case "proxy":{
                int i=Array.IndexOf(MagicStatusCatalog.ProxySkills,skill);
                return i>=0?E(518+i):U("250/154 代理 key 不在 518–537");
            }
            case "icon20":case "icon21":return U("250/21：TW16 讀 4 個 byte，與伺服器格式不同（V3 待驗證）");
            default:return U($"來源 {source} 不在 V3 表");
            }
        }
    }

    /// <summary>A game state that also carries the buff snapshot.</summary>
    public interface IUiBuffState { BuffState Buffs { get; } }

    /// <summary>Decorator: the shared IUiGameState plus the buff snapshot. Innermost wrapper of the bridge chain.</summary>
    public sealed class UiBuffGameState(IUiGameState inner,BuffState buffs):IUiGameState,IUiBuffState {
        public IUiGameState Inner=>inner;
        public BuffState Buffs=>buffs;
        public bool InWorld=>inner.InWorld;
        public string? CharacterName=>inner.CharacterName;
        public int? Level=>inner.Level;
        public long? Hp=>inner.Hp;
        public long? MaxHp=>inner.MaxHp;
        public long? Mp=>inner.Mp;
        public long? MaxMp=>inner.MaxMp;
        public long Now=>inner.Now;
    }
}

namespace P06 {
    public sealed partial class WorldSession {
        /// <summary>Haste timer end (NormalHaste.cs speedDisplayUntil) on the SpeedClock; null = no timer was sent.</summary>
        internal long? SpeedEndsAt=>speedDisplayUntil;
        internal long SpeedNow=>SpeedClock();
    }
    public sealed partial class ClientForm {
        /// <summary>Adds the buff snapshot (ui-buff-icons #473). Tooltip strings come from the original string table only.</summary>
        IUiGameState WithBuffState(IUiGameState state){
            if(!state.InWorld)return new UiBuffGameState(state,BuffState.Empty);
            var w=login.World;
            int? cls=login.Characters.FirstOrDefault(c=>c.Name==w.Actor?.Name)?.Class;
            var t=ServerMessageText.Table;
            string? S(int id)=>t!=null&&id<t.Count?ServerMessageText.DisplayText(t[id]):null;
            var text=t==null?BuffTooltipText.None:new BuffTooltipText(S(BuffTooltipText.LabelId),S(BuffTooltipText.UnitId),S(BuffTooltipText.OverId));
            return new UiBuffGameState(state,BuffState.From(w,cls,text));
        }
    }
}
