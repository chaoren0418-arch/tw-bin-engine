namespace P06;
public sealed record MagicBuff(int Skill,string Name,int Seconds,long ReceivedAt,string Source) {
    public int Remaining(long now)=>(int)Math.Max(0,Seconds-Math.Max(0,now-ReceivedAt)/1000);
    public bool Debuff {get;init;}
    public bool Approximate {get;init;}
}
public sealed partial class WorldSession {
    readonly System.Collections.Concurrent.ConcurrentDictionary<int,MagicBuff> magicBuffs=new();
    internal Func<long> MagicStatusClock=()=>Environment.TickCount64;
    public byte? PartialAcByte {get;private set;}
    public int? CurrentAc=>PartialAcByte==null?Status?.Ac:null;
    public string AcNotice=>PartialAcByte!=null?"AC：等待伺服器完整更新":CurrentAc is int ac?$"AC {ac}":"等待原服 AC";
    public MagicBuff[] MagicBuffs {
        get {
            long now=MagicStatusClock();
            foreach(var pair in magicBuffs)
                // Compare-and-remove must not erase a concurrent refresh of the same skill.
                if(pair.Value.Remaining(now)==0&&((ICollection<KeyValuePair<int,MagicBuff>>)magicBuffs).Remove(pair))MagicRevision++;
            return magicBuffs.Values.Where(b=>b.Remaining(now)>0).OrderBy(b=>b.Skill).ToArray();
        }
    }
    public string MagicBuffNotice=>string.Join("；",MagicBuffs.Select(b=>$"{b.Name}：{(b.Approximate?"約":"剩餘 ")}{b.Remaining(MagicStatusClock())} 秒"));
    public bool EffectiveMagicTargetSelf=>SelectedMagicId is int id&&MagicTargetsOwn(id);
    public string MagicTargetName=>SelectedMagicId==null?"尚未選擇技能":EffectiveMagicTargetSelf?"自己（"+(Actor?.Name??"尚未進入世界")+"）":Target?.Name??"尚未選擇對象";
    void ReceiveBuff(int skill,int seconds,string source,bool remove=false,string? name=null,bool approximate=false){
        if(remove||seconds==0){if(magicBuffs.TryGetValue(skill,out var prior)&&(source=="proxy"||prior.Source==source))magicBuffs.TryRemove(skill,out _);}
        else magicBuffs[skill]=new(skill,name??MagicStatusCatalog.Name(skill),seconds,MagicStatusClock(),source){Debuff=MagicStatusCatalog.Debuff(skill),Approximate=approximate};
        MagicRevision++;
    }
    // S_Icons：C 250、C 20、D 204、204 bytes = 210 → 補零 216。
    internal const int IconsSnapshotLength=216;
    // S_PacketBoxActiveSpells：C 250、C 0x14、104 個 C（全不為 72）= 106 bytes，是最短的未補零長度。
    internal const int ActiveSpellsMinLength=106;
    /// 收到並略過的 S_PacketBoxActiveSpells 登入狀態封包數（#1187 診斷用；Reset 不清，跨 session 累計）。
    public int LoginActiveSpellsPackets {get;private set;}
    /// 最近一次 S_PacketBoxActiveSpells 的 plain 長度（含補零）；未收過為 null。
    public int? LastLoginActiveSpellsLength {get;private set;}
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
            int skill=icon switch{5=>3,3=>99,6=>151,7=>159,10=>168,_=>-1000-icon};
            ReceiveBuff(skill,seconds,"shield",name:skill<0?$"防禦狀態 {icon}":null);return true;
        }
        if(opcode!=250)return false;
        if(plain.Length<2)throw new ProtocolException("truncated packetbox subcode");
        int sub=plain[1];
        if(sub is not (20 or 21 or 22 or 34 or 35 or 36 or 40 or 44 or 57 or 154))return false;
        r.C();
        // S250/20 有三個發送者（#1187，docs/protocol/status-icons.md）：依 plain 長度判別。
        //   8   → S_PacketBox(20,type,time) 的 C/H 單筆（下方 sub is 20 or 21 分支）。
        //   216 → S_Icons 快照：D 204 + 204 bytes。
        //   其他且 ≥106 → S_PacketBoxActiveSpells 登入狀態快照：int[104] 逐個寫 C，值==72 改寫 D(秒級時間戳)，
        //         長度 106+3k 再補到 8 的倍數，欄位位置無法無歧義還原，所以只吃掉、不改 buff；後續 S_Icons／個別圖示會補齊。
        if(sub==20&&plain.Length!=8){
            if(plain.Length!=IconsSnapshotLength){
                if(plain.Length<ActiveSpellsMinLength)throw new ProtocolException($"unsupported status snapshot size {plain.Length}");
                LoginActiveSpellsPackets++;LastLoginActiveSpellsLength=plain.Length;return true;
            }
            if(r.D()!=204)throw new ProtocolException($"unsupported status snapshot size {plain.Length}");
            var data=new byte[204];for(int i=0;i<data.Length;i++)data[i]=r.C();r.Finish();
            // Validate the entire packet before mutating any status. Zero slots clear only this source.
            foreach(var slot in MagicStatusCatalog.Slots)
                ReceiveBuff(slot.Skill,data[slot.Index]*slot.Unit,"icons1",approximate:true);
            foreach(int skill in MagicStatusCatalog.ElementalWeapons)
                ReceiveBuff(skill,data[11]+1==skill?data[10]*4:0,"icons1",approximate:true);
            return true;
        }
        if(sub is 20 or 21){
            int seconds=r.H(),type=r.C();r.Finish();
            ReceiveBuff(-sub*1000-type,seconds,$"icon{sub}",name:$"狀態 {sub}/{type}");return true;
        }
        if(sub==22){
            int icon=r.C(),seconds=r.H(),variant=r.C();r.Finish();
            int skill=icon switch{113=>114,114=>115,116=>117,147=>148,148=>149,154=>155,155=>156,162=>163,165=>166,_=>-220000-icon*256-variant};
            ReceiveBuff(skill,seconds,"aura",name:skill<0?$"光環 {icon}/{variant}":null);return true;
        }
        if(sub is 34 or 35 or 36 or 40){
            int seconds=r.H();r.Finish();
            ReceiveBuff(sub==40?68:-3000-sub,seconds,$"packetbox{sub}",name:sub switch{34=>"魔力回復藥水",35=>"變身",36=>"禁言",_=>null});return true;
        }
        if(sub==44){
            uint actor=r.D();int seconds=r.H()*4;r.Finish();
            if(actor!=0&&actor==(Status?.ObjectId??Actor?.ObjectId))ReceiveBuff(167,seconds,"wind-shackle",approximate:true);
            return true;
        }
        if(sub==57){
            if(r.C()!=44)throw new ProtocolException("invalid wisdom potion icon");
            int seconds=r.H();r.Finish();ReceiveBuff(-3057,seconds,"wisdom",name:"慎重藥水");return true;
        }
        int duration=r.H();
        if(plain.Length==8){
            uint gfx=r.D();r.Finish();
            int skill=gfx switch{2165=>8,747=>12,748=>21,2176=>48,2951=>107,_=>0};
            if(skill==0)Unhandled(opcode);else ReceiveBuff(skill,duration,"native-weapon");return true;
        }
        if(plain.Length!=16)throw new ProtocolException("unsupported skill icon length");
        int key=r.H(),reserved=r.H(),remove=r.H();r.Finish();
        if(reserved!=0||remove is not (0 or 1))throw new ProtocolException("invalid skill icon fields");
        int proxy=key is >=518 and <=537?MagicStatusCatalog.ProxySkills[key-518]:-400000-key;
        ReceiveBuff(proxy,duration,"proxy",remove==1,proxy<0?$"技能圖示 {key}":null);return true;
    }
}

// Wire identities, not cast permissions or stat modifiers. See docs/protocol/status-icons.md.
public static class MagicStatusCatalog {
    public static readonly int[] ProxySkills=[148,149,155,156,163,166,3,99,151,159,168,26,110,42,109,114,115,8,48,12];
    public static readonly int[] ElementalWeapons=[148,149,155,156,163,166];
    public static readonly (int Index,int Skill,int Unit)[] Slots=[
        (0,32,4),(1,31,4),(3,14,4),(4,71,4),(5,78,4),(7,104,4),(8,47,4),(9,56,4),
        (17,111,4),(18,55,4),(19,158,4),(20,167,4),(21,153,4),(22,134,4),(23,176,16),
        (24,133,4),(26,171,4),(30,174,4),(31,175,4),(32,173,4),(52,206,16),(53,216,16),
        (54,217,16),(55,191,4),(56,193,4),(57,188,4),(58,211,4),(59,183,4),(60,181,16),(61,1017,4)];
    public static bool Debuff(int skill)=>skill is 47 or 56 or 71 or 133 or 153 or 167 or 173 or 174 or 183 or 188 or 193 or 217 or -3036;
    public static string Name(int skill)=>skill switch{
        3=>"保護罩",8=>"神聖武器",12=>"擬似魔法武器",14=>"負重強化",21=>"鎧甲護持",26=>"敏捷提升",31=>"魔法屏障",32=>"冥想術",
        42=>"力量提升",47=>"弱化術",48=>"祝福魔法武器",55=>"狂暴術",56=>"疾病術",68=>"聖結界",71=>"藥水霜化",78=>"絕對屏障",
        99=>"暗影防護",104=>"毒性抵抗",107=>"暗影之牙",109=>"力量提昇",110=>"敏捷提昇",111=>"閃避提升",114=>"灼熱靈氣",115=>"閃亮靈氣",117=>"勇猛靈氣",
        133=>"弱化屬性",134=>"鏡反射",148=>"火焰武器",149=>"風之神射",151=>"大地防護",153=>"魔法消除",155=>"烈炎氣息",156=>"暴風之眼",158=>"生命之泉",159=>"大地的祝福",
        163=>"烈炎武器",166=>"暴風神射",167=>"風之枷鎖",168=>"鋼鐵防護",170=>"水之元氣",171=>"屬性之火",173=>"污濁之水",174=>"精準射擊",175=>"烈焰之魂",176=>"能量激發",
        181=>"龍之護鎧",183=>"護衛毀滅",188=>"恐懼無助",191=>"致命身軀",193=>"驚悚死神",206=>"專注",211=>"耐力",216=>"洞察",217=>"恐慌",1017=>"生命之樹果實",
        // S250 B2 子碼（PacketBoxStatus.cs）沒有技能 id 的計時狀態。
        -3056=>"魔法娃娃",-3060=>"三段加速",-3153=>"地圖限時",_=>$"狀態 {skill}"};
}
