namespace P06;

// T172（#446）：`envsound.def` 地圖環境音。依據 docs/reference/sound.md §4.7、§6.3（T163／T171）。
// 主環境音近似循環（A058E0(id,1)）＋進出區域 1 秒淡入淡出（82A800／82A870，40ms 一步）；
// ONE_SOUND 約每 999ms 抽一個群組、再抽候選單次播放（8257D0 → 829FD0），群組冷卻。
// 音效 id 查 sound\N.mp3，失敗再查 sound\N.wav（不查 musicN）；檔案解析由後端負責。
public sealed record EnvSoundVoice(long Number,int SoundId,bool Loop);
public interface IEnvSoundAudio
{
    // volume 是最終比例 0..1（淡入淡出係數 × EnvSoundVolume）。Loop=false 的單次音效由後端在播完後自行釋放。
    bool Start(EnvSoundVoice voice,double volume);
    void SetVolume(EnvSoundVoice voice,double volume);
    void Stop(EnvSoundVoice voice);
}
// Enabled：音效可播（未靜音、在世界中）；Volume：EnvSoundVolume 類別 × 主音量（0..1）；Night 來自遊戲時間（AmbienceFrame.Night）。
public sealed record EnvSoundContext(int Map,int X,int Y,bool Night,bool Enabled,double Volume);

public sealed class MapEnvSound(EnvSoundDefinition definition,IEnvSoundAudio audio,Random random,SoundCooldown? cooldownTable=null)
{
    public const int FadeMs=1000,FadeStepMs=40,FadeSteps=FadeMs/FadeStepMs,OneSoundCheckMs=999;
    readonly SoundCooldown cooldown=cooldownTable??new();
    sealed class Main(EnvSoundVoice voice,long at,bool fadingIn)
    {
        public readonly EnvSoundVoice Voice=voice;
        public long At=at;public bool In=fadingIn;public int From=fadingIn?0:FadeSteps;
        public int Step(long now)=>(int)Math.Clamp((now-At)/FadeStepMs,0,FadeSteps);
        // 目前的淡入淡出階（0..25）。
        public int Level(long now)=>In?Math.Min(FadeSteps,From+Step(now)):Math.Max(0,From-Step(now));
    }
    readonly List<Main> mains=[];
    long number,nextOne=long.MinValue,mainRetryAt=long.MinValue;int mainId=-1;
    public EnvSoundDefinition Definition=>definition;
    public EnvSoundRegion? Region {get;private set;}
    public int MainSound=>mainId;
    public IReadOnlyList<(EnvSoundVoice Voice,int Level)> Mains(long now)=>mains.Select(m=>(m.Voice,m.Level(now))).ToArray();

    public void Update(EnvSoundContext ctx,long now)
    {
        double volume=Math.Clamp(double.IsFinite(ctx.Volume)?ctx.Volume:0,0,1);
        if(!ctx.Enabled){Clear();return;}
        Region=definition.Region(ctx.Map,ctx.X,ctx.Y,ctx.Night);
        int desired=Region is {MainSound:>0} r?r.MainSound:-1;
        var current=mains.LastOrDefault(m=>m.In);
        if(desired!=mainId||desired>0&&current==null&&now>=mainRetryAt)
        {
            // 進出區域：舊主音從目前音量 1 秒淡出，新主音從 0 淡入（推測：兩者同時進行）。
            foreach(var m in mains.Where(m=>m.In)){m.From=m.Level(now);m.In=false;m.At=now;}
            mainId=desired;
            if(desired>0)
            {
                var voice=new EnvSoundVoice(++number,desired,true);
                if(audio.Start(voice,0))mains.Add(new(voice,now,true));
                else mainRetryAt=now+OneSoundCheckMs; // 缺檔：每秒最多重試一次。
            }
        }
        foreach(var m in mains.ToArray())
        {
            int level=m.Level(now);
            if(!m.In&&level==0){audio.Stop(m.Voice);mains.Remove(m);continue;}
            audio.SetVolume(m.Voice,volume*level/FadeSteps);
        }
        if(now<nextOne)return;
        nextOne=now+OneSoundCheckMs;
        if(Region is not {OneSounds.Length:>0} region)return;
        // 8257D0：均勻抽一個群組；829FD0：群組冷卻未到就直接返回，不再抽別的群組。
        var group=region.OneSounds[random.Next(region.OneSounds.Length)];
        if(group.Candidates.Length==0||now<group.NextAt)return;
        int index=0;
        if(group.Candidates.Length>1)do index=random.Next(group.Candidates.Length);while(index==group.LastIndex);
        var candidate=group.Candidates[index];
        if(candidate.Id<=0||volume==0)return;
        if(!cooldown.TryAcquire(candidate.Id,now))return; // A06020 每個 id 全域 200ms。
        if(!audio.Start(new(++number,candidate.Id,false),volume))return;
        // 只有成功播放才更新下次可播時間與前次索引。
        group.NextAt=now+1000L*candidate.Seconds;group.LastIndex=index;
    }

    public void Clear()
    {
        foreach(var m in mains)audio.Stop(m.Voice);
        mains.Clear();mainId=-1;Region=null;mainRetryAt=long.MinValue;
    }
}
