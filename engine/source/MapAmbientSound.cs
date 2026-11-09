using P24.HostAudio;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace P06;

// T166 (#434)：地圖事件 `^` 環境音。依據 docs/reference/sound.md §2（T163）與
// ambience-and-map-events.md §5（T162）：A3EF40 進幀選音 → A06210 槽表 → A06020 冷卻 → A091C0 距離音量。
public sealed record MapAmbientVoice(long Number,int SoundId,string Emitter);
public sealed record MapAmbientSource(string Key,int X,int Y,MapEffectAppearance Appearance,long Elapsed);
public sealed record MapAmbientSlot(int SoundId,MapAmbientVoice[] Voices,int[] Distances);
// 36-byte frame 的 +20 模式（`<`=0、`[`=1、`^`=2）與 +2..+10 候選（最多 5 個，第 6 個起丟棄）。
public sealed record MapAmbientCue(int Mode,int[] Candidates)
{
    public const int Ambient=2;
    static readonly Regex Token=new(@"([<\[\^])(\d+)",RegexOptions.CultureInvariant);
    public static MapAmbientCue Parse(string events)
    {
        int mode=-1;var candidates=new List<int>();
        foreach(Match m in Token.Matches(events??""))
        {
            mode=m.Groups[1].Value switch{"<"=>0,"["=>1,_=>Ambient};
            if(candidates.Count<MapAmbientSound.SourcesPerSound)candidates.Add(int.TryParse(m.Groups[2].Value,out int id)&&id<=ushort.MaxValue?id:0);
        }
        return new(mode,candidates.ToArray());
    }
}

// T173（#447）：距離音量與 200ms 冷卻改走 SoundRules／SoundCooldown 共用層。產品傳 SoundCooldown.Shared
// （與戰鬥音效、S22 同一份、同一時鐘 Environment.TickCount64）；未指定時用獨立表，供離線測試隔離。
public sealed class MapAmbientSound(IAmbientLoopAudio audio,Random random,SoundCooldown? cooldownTable=null)
{
    readonly IAmbientLoopAudio audio=audio;
    public const int SourcesPerSound=5,CooldownMs=SoundRules.CooldownMs,Reach=SoundRules.MaxManhattan;
    readonly SoundCooldown cooldown=cooldownTable??new();
    sealed class Emitter(MapAmbientSource source)
    {
        public readonly MapAmbientSource Source=source;
        public long Step=-1;public int Volume;
        public readonly Dictionary<int,MapAmbientVoice> Voices=[];
    }
    readonly Dictionary<string,Emitter> emitters=new(StringComparer.Ordinal);
    readonly SortedDictionary<int,List<(MapAmbientVoice Voice,int D2)>> slots=[];
    static readonly ConditionalWeakTable<MapEffectAppearance,MapAmbientCue[]> cues=new();
    long voiceNumber;

    public int Emitters=>emitters.Count;
    public MapAmbientSlot[] Slots=>slots.Select(s=>new MapAmbientSlot(s.Key,s.Value.Select(v=>v.Voice).ToArray(),s.Value.Select(v=>v.D2).ToArray())).ToArray();

    // A091C0 前的計算：曼哈頓 > 14 夾成 (14,0)；整數除法；負值夾 0。`127−7·round(√d²)` 是死碼，不使用。
    public static int Volume(int dx,int dy)=>SoundRules.DistanceVolume(dx,dy);
    // 只用來排序槽內音源（A06210 第 3 參數）。單位：格（引擎座標已是原版 x>>1 的尺度）。
    public static int DistanceSquared(int dx,int dy)=>(int)Math.Min(int.MaxValue,(long)dx*dx+(long)dy*dy);
    public static MapAmbientCue[] Cues(MapEffectAppearance appearance)=>cues.GetValue(appearance,a=>a.Sequence.Select(s=>MapAmbientCue.Parse(s.Events)).ToArray());
    public static bool HasAmbient(MapEffectAppearance appearance)=>Cues(appearance).Any(c=>c.Mode==MapAmbientCue.Ambient&&c.Candidates.Length>0);
    // 24 tick/秒，與 MapEffectAppearance.At 同一時鐘；回傳「第幾次進幀」的絕對序號。
    public static long Step(MapEffectAppearance appearance,long elapsed)
    {
        if(elapsed<0)return -1;
        long total=appearance.Sequence.Sum(s=>(long)s.Ticks);
        if(total<=0)return -1;
        long tick=(long)Math.Floor(elapsed*24d/1000);
        long cycle=tick/total,within=tick%total;
        for(int i=0;i<appearance.Sequence.Length;i++)
        {
            if(within<appearance.Sequence[i].Ticks)return cycle*appearance.Sequence.Length+i;
            within-=appearance.Sequence[i].Ticks;
        }
        return cycle*appearance.Sequence.Length+appearance.Sequence.Length-1;
    }

    // sources：目前存在的地圖特效物件；不在清單中的物件視為已移除（含換圖、離開快取區），其音源立即停止。
    // audible：A06020 的類別音量檢查（全部為 0 或靜音時不啟動）；只影響啟動，不影響已在播的循環。
    public void Update(IReadOnlyList<MapAmbientSource> sources,(int X,int Y)? self,long now,bool audible)
    {
        var present=new HashSet<string>(sources.Select(s=>s.Key),StringComparer.Ordinal);
        foreach(var gone in emitters.Values.Where(e=>!present.Contains(e.Source.Key)).ToArray())Release(gone);
        foreach(var source in sources)
        {
            if(emitters.TryGetValue(source.Key,out var emitter)&&(!ReferenceEquals(emitter.Source.Appearance,source.Appearance)||emitter.Source.X!=source.X||emitter.Source.Y!=source.Y))
            {Release(emitter);emitter=null;} // 換圖：同一鍵換了外觀或位置，舊物件已不存在。
            if(emitter==null)
            {
                emitter=new(source){Volume=self is {} p?Volume(p.X-source.X,p.Y-source.Y):0}; // 推測：新物件以目前距離當作「上一輪」音量。
                emitters[source.Key]=emitter;
            }
            long step=Step(source.Appearance,source.Elapsed);
            if(step<0)continue;
            if(emitter.Step>=0&&step<emitter.Step){emitter.Step=step;continue;} // 時鐘倒退只重新對齊，不補觸發。
            int length=source.Appearance.Sequence.Length;
            long first=emitter.Step<0?step:Math.Max(emitter.Step+1,step-length+1);
            for(long k=first;k<=step;k++)Enter(emitter,(int)(k%length),self,now,audible);
            emitter.Step=step;
        }
        foreach(var empty in slots.Where(s=>s.Value.Count==0).Select(s=>s.Key).ToArray())slots.Remove(empty);
    }

    public void Clear(){foreach(var emitter in emitters.Values.ToArray())Release(emitter);slots.Clear();}

    void Enter(Emitter emitter,int index,(int X,int Y)? self,long now,bool audible)
    {
        if(self is not {} p)return; // A3EF40：自身角色不存在時不選音、不調音量。
        int dx=p.X-emitter.Source.X,dy=p.Y-emitter.Source.Y;
        var cue=Cues(emitter.Source.Appearance)[index];
        if(cue.Mode==MapAmbientCue.Ambient&&cue.Candidates.Length is >=1 and <=SourcesPerSound)
        {
            int id=cue.Candidates.Length==1?cue.Candidates[0]:cue.Candidates[random.Next(cue.Candidates.Length)];
            if(id!=0)Admit(emitter,id,DistanceSquared(dx,dy),now,audible); // 抽到 0 略過，不重抽。
        }
        // A091C0：播放在前、調音量在後，所以新音源先用上一輪的音量啟動。走遠只降到 0，不停止。
        int volume=Volume(dx,dy);
        if(volume==emitter.Volume)return;
        emitter.Volume=volume;
        foreach(var voice in emitter.Voices.Values)audio.SetVolume(voice,volume);
    }

    void Admit(Emitter emitter,int id,int d2,long now,bool audible)
    {
        if(emitter.Volume==0)return; // A06210 前提：發聲體目前音量為 0（太遠）不啟動。
        if(!slots.TryGetValue(id,out var slot))slots[id]=slot=[];
        if(slot.Count>=SourcesPerSound&&d2>=slot[^1].D2)return; // 已滿：只有更近的才換入。
        if(emitter.Voices.ContainsKey(id))return; // 同一物件同 id 已在循環，不重播。
        if(!audible)return; // A06020 類別音量檢查。
        if(!cooldown.TryAcquire(id,now))return; // 每個 id 全域 200ms（SoundCooldown）。
        var voice=new MapAmbientVoice(++voiceNumber,id,emitter.Source.Key);
        if(!audio.Start(voice,emitter.Volume))return; // 啟動失敗不動槽表。
        if(slot.Count>=SourcesPerSound)
        {
            var farthest=slot[^1];slot.RemoveAt(slot.Count-1);
            audio.Stop(farthest.Voice); // A06E10
            if(emitters.TryGetValue(farthest.Voice.Emitter,out var owner))owner.Voices.Remove(id);
        }
        slot.Add((voice,d2));emitter.Voices[id]=voice;
        for(int i=slot.Count-1;i>0&&slot[i].D2<slot[i-1].D2;i--)(slot[i],slot[i-1])=(slot[i-1],slot[i]); // 穩定的泡泡排序。
    }

    // A04A20／A04BF0：物件移除時停掉它的每個音源，並從槽表移除、後面往前補。
    void Release(Emitter emitter)
    {
        foreach(var voice in emitter.Voices.Values)
        {
            if(slots.TryGetValue(voice.SoundId,out var slot))slot.RemoveAll(v=>v.Voice==voice);
            audio.Stop(voice);
        }
        emitter.Voices.Clear();
        emitters.Remove(emitter.Source.Key);
    }

    // 產品掛載：Renderer.ComposeMapEffects 每次組畫面呼叫一次。沒有循環播放後端時不建立任何狀態。
    public static IAmbientLoopAudio? Backend {get;set;}
    static readonly ConditionalWeakTable<WorldSession,MapAmbientSound> sessions=new();
    public static MapAmbientSound? Of(WorldSession session)=>sessions.TryGetValue(session,out var model)?model:null;
    public static void Observe(WorldSession session,long now)
    {
        if(!sessions.TryGetValue(session,out var model))
        {
            if(Backend is not {} backend)return;
            model=new(backend,Random.Shared,SoundCooldown.Shared);sessions.Add(session,model);
        }
        var sources=Sources(session,now);
        if(sources.Length>0||model.Emitters>0)model.audio.SetScale(Scale(session)); // T252：先套類別音量，已在播的循環也跟著變。
        model.Update(sources,session.Actor is {} actor?(actor.X,actor.Y):null,now,sources.Length>0&&Audible(session));
    }
    // 音量與靜音沿用 6016 host 已同步的主控制值（ClientForm 維護）。
    // A06020：音效／環境／語音三類都是 0 時不啟動。
    static bool Audible(WorldSession session)=>session.Renderable&&session.Effects6016 is {Closed:false,Muted:false} host&&host.Gain>0&&!ClientSoundScene.Volumes.EffectCategoriesSilent;
    // T252：EnvSoundVolume × 主音量；靜音、關閉或不可繪製時為 0（已在播的循環降到無聲，不停止）。
    public static double Scale(WorldSession session)=>session.Renderable&&session.Effects6016 is {Closed:false,Muted:false} host&&host.Gain>0?ClientSoundScene.Volumes[SoundCategory.EnvSound]*host.Gain:0;
    public static MapAmbientSource[] Sources(WorldSession session,long now)
    {
        if(session.MapCache is not {} cache||!session.Renderable)return [];
        var result=new List<MapAmbientSource>();
        foreach(var block in cache.Slots.OfType<OriginalMapBlock>())
        {
            if(block.EffectsAt<0)continue;
            for(int ordinal=0;ordinal<block.Effects.Length;ordinal++)
            {
                var effect=block.Effects[ordinal];
                if(effect.Id==6016||!HasAmbient(effect.Appearance))continue; // 6016 由 Effect6016Controller 負責。
                result.Add(new($"{session.WorldEpoch}/{cache.MapId}/{block.Name}@{block.Generation}:{ordinal}",effect.X,effect.Y,effect.Appearance,now-block.EffectsAt));
            }
        }
        return result.ToArray();
    }
}
