using System.Security.Cryptography;
namespace P06;

// Server action window, original frame schedule, skill reuse and local margin are distinct.
// Speed0/2 retain r30 behavior. Speed1/Brave0 rounds each native frame separately.
public sealed record ActionCadence(int Gfx,int Weapon,int Action,int ServerBaseMs,int NativeBaseMs,int LegacyAnimationMs,int? ReleaseBaseMs,int ReuseMs,int MarginMs,bool Slow) {
    public bool Haste {get;init;}
    public int HasteAnimationMs {get;init;}
    public int? HasteReleaseMs {get;init;}
    public static int HasteFrameMs(int ms)=>ms>=0?checked(3*ms)>>2:throw new ArgumentOutOfRangeException(nameof(ms));
    public int AnimationMs=>Slow?checked(NativeBaseMs*2):Haste?HasteAnimationMs:LegacyAnimationMs;
    public int? ReleaseMs=>Slow?(ReleaseBaseMs is int ms?checked(ms*2):null):Haste?HasteReleaseMs:ReleaseBaseMs;
    public int ServerIntervalMs=>Slow?checked(ServerBaseMs*2):Haste?Math.Max(1,(int)(ServerBaseMs*0.755)):ServerBaseMs;
    public int WaitMs=>Math.Max(Math.Max(ServerIntervalMs,ReuseMs),Slow||Haste?AnimationMs:0)+MarginMs;
    public int SlowWaitMs=>(this with{Slow=true}).WaitMs;
    public int NormalWaitMs=>(this with{Slow=false,Haste=false}).WaitMs;
    // SV§1.2 (TW16 sub_E246E0/sub_A3BC30): spell reuse = own appearance list.spr action 18/19 frame total,
    // speed-scaled (haste x3/4 per frame, slow x2), plus the skill's extra delay. ReuseMs stands in for the
    // native skill-table +56 field, whose source table is unverified. No local margin (native has none); the
    // server action window is only a floor so a shorter list.spr sequence never sends faster than the server accepts.
    public int NativeCastMs=>Slow?checked(NativeBaseMs*2):Haste?HasteAnimationMs:NativeBaseMs;
    public int CastWaitMs=>Math.Max(checked(NativeCastMs+ReuseMs),ServerIntervalMs);
    public int CastSpeedWaitMs(byte speed)=>(this with{Slow=speed==2,Haste=speed==1}).CastWaitMs;
    public bool Matches(int gfx,int weapon)=>Gfx==gfx&&Weapon==weapon;
    public static string Name(int action)=>action switch{1=>"attack",18=>"spellDir",19=>"spellNoDir",_=>throw new ArgumentOutOfRangeException(nameof(action))};
    sealed record Metadata(int Native,int Legacy,int? Release,int HasteNative,int? HasteRelease,int Server);
    static readonly Dictionary<(int Gfx,int Weapon,int Action),Metadata> metadata=[];
    static readonly object gate=new();
    public static ActionCadence Capture(int gfx,int weapon,int action,byte speed,byte brave,int reuse=0,int margin=0){
        if(!NormalSpeed.Supported(speed,brave))throw new NotSupportedException("Unsupported action speed");
        var profile=PlayerProfiles.Get(gfx,weapon)??throw new NotSupportedException("Unsupported action appearance");
        var key=(gfx,weapon,action);Metadata data;
        lock(gate){
            if(!metadata.TryGetValue(key,out data!)){
                string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../assets",profile.Path));
                var bytes=File.ReadAllBytes(path);if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(profile.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Action profile integrity");
                var json=System.Text.Json.JsonDocument.Parse(bytes).RootElement;string name=Name(action);
                var steps=json.GetProperty("sequences").EnumerateArray().Single(s=>s.S("action")==name&&s.I("direction")==0).GetProperty("sequence");
                int native=0,hasteNative=0;int? release=null,hasteRelease=null;
                foreach(var step in steps.EnumerateArray()){
                    if(release==null&&step.TryGetProperty("events",out var events)&&events.GetString()!.Contains('!')){release=native;hasteRelease=hasteNative;}
                    int frameMs=step.TryGetProperty("milliseconds",out var ms)?ms.GetInt32():step.I("ticks")*1000/24;
                    native+=frameMs;hasteNative+=HasteFrameMs(frameMs);
                }
                int legacy=json.GetProperty("actionMs").GetProperty(name).GetInt32();
                if(native<=0||legacy<=0)throw new InvalidDataException("Empty action timing");
                // T461：伺服器間隔照 R1 由同一段 list.spr 影格的 ticks 計算（不讀 manifest 的 serverActionMs）。
                data=new(native,legacy,release,hasteNative,hasteRelease,ActionTiming.ServerMs(ActionTiming.Steps(json,name)));metadata.Add(key,data);
            }
        }
        return new(gfx,weapon,action,data.Server,data.Native,data.Legacy,data.Release,reuse,margin,speed==2){Haste=speed==1,HasteAnimationMs=data.HasteNative,HasteReleaseMs=data.HasteRelease};
    }
    /// <summary>R1：(gfx, 姿勢) 某動作（1、18、19）的伺服器間隔，速度不計；沒有 profile 時丟 InvalidDataException。</summary>
    public static int ServerMs(int gfx,int weapon,int action)=>PlayerProfiles.Get(gfx,weapon) is {} profile?ActionTiming.ServerMs(profile,Name(action)):throw new InvalidDataException("unsupported player action cadence");
    public Frame Frame(ObjectAppearance appearance,int direction,double elapsed){
        string name=Name(Action);if(!Slow&&!Haste)return appearance.At(name,direction,elapsed);
        if(Haste&&!Slow){
            double fastAge=Math.Clamp(elapsed,0,HasteAnimationMs-.00001);var fastSequence=appearance.Sequences[(name,direction)];
            foreach(var step in fastSequence){int ms=HasteFrameMs(step.Milliseconds??step.Ticks*1000/24);if(fastAge<ms)return appearance.Frames[step.Key];fastAge-=ms;}
            return appearance.Frames[fastSequence[^1].Key];
        }
        // Some legacy profiles omit milliseconds: use the original per-frame integer rule.
        double age=Math.Clamp(elapsed/2,0,NativeBaseMs-.00001);var sequence=appearance.Sequences[(name,direction)];
        foreach(var step in sequence){int ms=step.Milliseconds??step.Ticks*1000/24;if(age<ms)return appearance.Frames[step.Key];age-=ms;}
        return appearance.Frames[sequence[^1].Key];
    }
}

// T269 (#643)／SS2§6／FU3§1: skill-table +56 (`delay`, ms) and +60 (`dir`/`direction`) come from SpellList.xml
// (TW16 parser sub_E177C0 FO 0xA16BC0, defaults from constructor sub_E14030 FO 0xA13430: delay 10, direction false).
// The lookup key is the XML `id` = TW16 skill index (0-based) = 3.81 skill number − 1. The local 3.81 client has no
// SpellList.xml; per #533 another local client's copy (default kor25) is read at run time from its Tile.idx/Tile.pak,
// decrypted in memory, and identified by SHA-256. Nothing is copied into the repository.
public sealed record SpellListEntry(int Skill,int DelayMs,bool Direction);
public sealed record SpellListSource(string Root,string Entry,string IndexSha256,string XmlSha256,bool KnownKor25,int Count);
public sealed class SpellListTable(IReadOnlyDictionary<int,SpellListEntry> entries,IReadOnlySet<int> ambiguous,SpellListSource? source) {
    public IReadOnlyDictionary<int,SpellListEntry> Entries=>entries;
    /// <summary>3.81 skill numbers whose XML id appears more than once (the native parser warns); never applied.</summary>
    public IReadOnlySet<int> Ambiguous=>ambiguous;
    public SpellListSource? Source=>source;
}
public static class SpellListCooldowns {
    public const int DefaultDelayMs=10;
    // kor25 `D:\381懶人包kor25(原)` Tile.pak SpellList.xml, decrypted (FU3§0: prefix 5d21a9ba95f294fb).
    public const string Kor25XmlSha256="5d21a9ba95f294fb5813becc7a46f02b9eb6f66b91722bad81218f7a2f1a0cc2";
    public const int MaxXmlBytes=1<<20;
    public static string ClientRoot=>Environment.GetEnvironmentVariable("TW_SPELLLIST_CLIENT")??@"D:\381懶人包kor25(原)";
    static readonly System.Text.RegularExpressions.Regex element=new(@"<p?spell\b([^>]*)>",System.Text.RegularExpressions.RegexOptions.CultureInvariant),
        attribute=new(@"([A-Za-z_][\w\-]*)\s*=\s*(?:""([^""]*)""|'([^']*)')",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    static readonly object gate=new();
    static SpellListTable? table;static bool loaded;
    /// <summary>Why no table is in use (missing client, no entry, unreadable); null when loaded.</summary>
    public static string? Error {get;private set;}
    public static SpellListTable? Table{get{lock(gate){if(!loaded){loaded=true;try{table=Load(ClientRoot);Error=table==null?"SpellList.xml not found":null;}catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException){table=null;Error=ex.Message;}}return table;}}}
    internal static void Use(SpellListTable? value){lock(gate){table=value;loaded=true;Error=value==null?"disabled":null;}}
    // atoi: optional sign and leading digits, otherwise 0.
    static int Atoi(string s){var m=System.Text.RegularExpressions.Regex.Match(s,@"^\s*([+-]?\d{1,9})");return m.Success?int.Parse(m.Groups[1].Value):0;}
    /// <summary>Only ASCII attributes are read; names are CP950 under a UTF-8 declaration, so the text is taken as Latin-1.</summary>
    public static SpellListTable Parse(string xml,SpellListSource? source=null){
        var entries=new Dictionary<int,SpellListEntry>();var ambiguous=new HashSet<int>();
        foreach(System.Text.RegularExpressions.Match m in element.Matches(xml)){
            int? id=null;int delay=DefaultDelayMs;bool direction=false;
            foreach(System.Text.RegularExpressions.Match a in attribute.Matches(m.Groups[1].Value)){
                string name=a.Groups[1].Value,value=a.Groups[2].Success?a.Groups[2].Value:a.Groups[3].Value;
                if(name=="id")id=Atoi(value);else if(name=="delay")delay=Atoi(value);else if(name is "dir" or "direction")direction=value=="true";
            }
            if(id is not int index||index<0||index>254)continue;int skill=index+1;
            if(!entries.TryAdd(skill,new(skill,delay,direction)))ambiguous.Add(skill);
        }
        foreach(int skill in ambiguous)entries.Remove(skill);
        return new(entries,ambiguous,source is null?null:source with{Count=entries.Count});
    }
    /// <summary>Reads "SpellList-c.xml" then "SpellList.xml" (FileManager order) from root\Tile.idx/Tile.pak; null when absent.</summary>
    public static SpellListTable? Load(string root){
        string idxPath=Path.Combine(root,"Tile.idx"),pakPath=Path.Combine(root,"Tile.pak");
        if(!File.Exists(idxPath)||!File.Exists(pakPath))return null;
        var idx=File.ReadAllBytes(idxPath);
        using var pak=new FileStream(pakPath,FileMode.Open,FileAccess.Read,FileShare.Read);
        return FromArchive(root,idx,pak);
    }
    public static SpellListTable? FromArchive(string root,byte[] idx,Stream pak){
        if(idx.Length<4)throw new InvalidDataException("Tile.idx too short");
        uint count=BitConverter.ToUInt32(idx,0);if((long)count*28+4!=idx.Length)throw new InvalidDataException("Tile.idx size is not count×28+4");
        var found=new Dictionary<string,(long Offset,int Size)>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<count;i++){
            int at=4+i*28;var raw=idx.AsSpan(at+4,20);int end=raw.IndexOf((byte)0);if(end<0)end=20;
            string name=System.Text.Encoding.Latin1.GetString(raw[..end]);
            if(name.Equals("SpellList-c.xml",StringComparison.OrdinalIgnoreCase)||name.Equals("SpellList.xml",StringComparison.OrdinalIgnoreCase))
                found.TryAdd(name,(BitConverter.ToUInt32(idx,at),(int)Math.Min(BitConverter.ToUInt32(idx,at+24),int.MaxValue)));
        }
        foreach(string name in new[]{"SpellList-c.xml","SpellList.xml"}){
            if(!found.TryGetValue(name,out var e))continue;
            if(e.Size is <=0 or >MaxXmlBytes||e.Offset+e.Size>pak.Length)throw new InvalidDataException($"{name} entry out of range");
            var blob=new byte[e.Size];pak.Position=e.Offset;pak.ReadExactly(blob);
            var plain=NativeUi.UiXmlSource.IsEncrypted(blob)?NativeUi.UiXmlSource.Decrypt(blob):blob;
            string sha=Convert.ToHexString(SHA256.HashData(plain)).ToLowerInvariant();
            var source=new SpellListSource(root,name,Convert.ToHexString(SHA256.HashData(idx)).ToLowerInvariant(),sha,sha==Kor25XmlSha256,0);
            System.Diagnostics.Trace.WriteLine($"SpellList cooldown source {root}\\Tile.pak:{name} sha256 {sha}");
            return Parse(System.Text.Encoding.Latin1.GetString(plain),source);
        }
        return null;
    }
    /// <summary>
    /// Skill reuse delay (+56) for a 3.81 skill number. Falls back to the current value when there is no table, the
    /// skill is not listed or listed twice, or the XML direction (+60 → action 18/19) disagrees with the action the
    /// engine casts with (the id mapping is then not trusted for this skill).
    /// </summary>
    public static int ReuseMs(int skill,int action,int fallback,SpellListTable? source=null){
        var t=source??Table;
        return t!=null&&t.Entries.TryGetValue(skill,out var e)&&(e.Direction?18:19)==action&&e.DelayMs>=0?e.DelayMs:fallback;
    }
}

public sealed partial class WorldSession {
    internal Func<long> ActionClock=()=>Environment.TickCount64;
    sealed record PendingAction(CombatAnimation Animation,int Epoch,int Appearance,int Correction,uint Target);
    PendingAction? pendingAction;
    internal CombatAnimation? ActionFor(uint id)=>Actor?.ObjectId==id?OwnCombat:Objects.FirstOrDefault(o=>o.ObjectId==id)?.Combat;
    internal void PredictAction(int action,int heading,uint target,ActionCadence timing){
        if(Actor is not {} actor)return;
        CancelPickupPresentation("predicted-action");Actor=actor with{Direction=(byte)heading};var animation=new CombatAnimation(action,ActionClock(),true){Timing=timing};OwnCombat=animation;
        pendingAction=new(animation,WorldEpoch,AppearanceRevision,Corrections,target);
    }
    CombatAnimation CaptureCombat(uint id,int action,bool predicted,uint? target){
        if(Actor is {} own&&own.ObjectId==id&&action is 1 or 18 or 19&&pendingAction is {} pending){
            pendingAction=null;
            if(!OwnDead&&pending.Epoch==WorldEpoch&&pending.Appearance==AppearanceRevision&&pending.Correction==Corrections&&pending.Animation.Action==action&&(target==null||target==pending.Target)&&pending.Animation.Timing?.Matches(own.Gfx,own.Weapon)==true)
                return pending.Animation with{Predicted=false};
        }
        ActionCadence? timing=null;
        if(action is 1 or 18 or 19){
            var other=Objects.FirstOrDefault(o=>o.ObjectId==id);
            int gfx=Actor?.ObjectId==id?Actor.Gfx:other?.Gfx??-1,weapon=Actor?.ObjectId==id?Actor.Weapon:other?.Action??-1;
            byte speed=Actor?.ObjectId==id?Actor.Speed:other?.Speed??255,brave=Actor?.ObjectId==id?Actor.Brave:other?.Brave??255;
            if(NormalSpeed.Supported(speed,brave)&&PlayerProfiles.Get(gfx,weapon)!=null)timing=ActionCadence.Capture(gfx,weapon,action,speed,brave,margin:action==1?CombatRules.ClockMargin(weapon):100);
        }
        return new(action,ActionClock(),predicted){Timing=timing};
    }
}

public sealed partial class LoginConnection {
    internal Func<long> ActionClock=()=>Environment.TickCount64;
    ActionCadence? lastCombatTiming,lastMagicTiming;long lastCombatAdmission=-1,lastMagicAdmission=-1;int combatTimingEpoch,magicTimingEpoch;
    void ExtendActionCooldown(){
        if(World.Actor is not {Brave:0} actor||actor.Speed is not (0 or 2))return;
        if(lastCombatTiming is {} combat&&lastCombatAdmission>=0&&combatTimingEpoch==World.WorldEpoch)nextCombatAt=Math.Max(nextCombatAt,lastCombatAdmission+(actor.Speed==2?combat.SlowWaitMs:combat.NormalWaitMs));
        if(lastMagicTiming is {} magic&&lastMagicAdmission>=0&&magicTimingEpoch==World.WorldEpoch)nextMagicAt=Math.Max(nextMagicAt,lastMagicAdmission+magic.CastSpeedWaitMs(actor.Speed));
    }
    bool ActionAdmissionReady=>!movementSending&&!itemSending&&!groundSending&&!GroundBusy&&!ItemBusy&&MovementClock()>=MovementReadyAt&&ActionClock()>=nextCombatAt&&ActionClock()>=nextMagicAt;
}
