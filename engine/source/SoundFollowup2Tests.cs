using System.Security.Cryptography;
using P24.Audio;
using P24.HostAudio;
using P24.MapEffectEvents;
namespace P06;

// T310（#737，R5-E）離線測試：ext 當 Miles pan 套到裝置、IEffectAudio 改音量入口與 6016 每次更新、music.def 的 MIDI 後備與聲像。
// 依據：SD§2.3（docs/reference/sound.md）、gap-matrix 14.6 R5-E、docs/reports/issue-737.md 的 Mss32.dll／TW1608262200.bin 靜態核對。
// 只用假驅動（記錄 Gain／Balance，不開 DirectShow、MCI 或音訊裝置）、自編位元組檔（不是遊戲素材）、假檔案清單；不連網路。
static class SoundFollowup2Tests {
    const string Name="sound-followup-2";
    const int Sid=4343; // 自編 id，對應下面的合成檔。

    sealed class Driver:IAudioDriver {
        public readonly List<string> Log=[];
        public readonly List<(string Voice,int Balance)> Balances=[];
        public double LastGain=-1;
        public bool FailBalance;
        public NativeReply Open(string path,string format,string voice){Log.Add("open");return new(0,"ok");}
        public NativeReply Gain(string voice,double normalized){LastGain=normalized;Log.Add("gain");return new(0,"ok");}
        public NativeReply Balance(string voice,int value){Log.Add("balance");if(FailBalance)return new(0x80004005,"fake balance failure");Balances.Add((voice,value));return new(0,"ok");}
        public NativeReply Play(string voice){Log.Add("play");return new(0,"ok");}
        public NativeReply Status(string voice)=>new(0,"playing");
        public NativeReply Stop(string voice)=>new(0,"ok");
        public NativeReply Close(string voice)=>new(0,"ok");
    }
    static AudioAssets Assets(){
        string root=Path.Combine(AppContext.BaseDirectory,"sound-followup-2-tests");
        byte[] bytes="synthetic-pan-test"u8.ToArray();
        string rel=$"sound/{Sid}.wav";
        string path=Path.Combine(root,rel);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(!File.Exists(path)||!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))File.WriteAllBytes(path,bytes);
        return new(root,[new AssetPin(Sid,"wav",rel,bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)))]);
    }
    static EffectOwner Owner(int ordinal,long generation=1)=>new(Guid.Parse("00000000-0000-0000-0000-000000000737"),new(4,"b0",ordinal),generation);
    static EffectFrameState Frame(EffectOwner owner,int x,int y)=>new(owner,x,y,0,0,0,0,0);

    [WorldModuleTest(Name)]
    static void Run(WorldModuleTestContext t){PanLaw(t);CorePan(t);CoreStoredLevel(t);EffectEntry(t);EffectTracker(t);EffectWorldLevel(t);Midi(t);MidiPlayer(t);MusicPan(t);}

    // Mss32.dll 0x2A9E0：左＝(1−p)^0.3、右＝p^0.3（p＝ext/128）；中央兩邊 0.5^0.3＝0.8122522（常數 0x2114BF70）。
    static void PanLaw(WorldModuleTestContext t){
        var (l,r)=AudioGain.PanLevels(64);
        t.Check(Math.Abs(l-0.8122522)<1e-6&&Math.Abs(r-0.8122522)<1e-6,"ext 64：左右都是 0.5^0.3（與 Mss32 常數相同）",new{l,r});
        t.Equal(0,AudioGain.Balance(64),"中央：balance 0（不改既有響度）");
        t.Equal(-10000,AudioGain.Balance(0),"ext 0（pan 0＝最左）：右聲道全關");
        t.Equal(10000,AudioGain.Balance(128),"ext 128（pan 1＝最右）：左聲道全關");
        int expected=(int)Math.Round(2000*Math.Log10(Math.Pow(42/128d,.3)/Math.Pow(86/128d,.3)));
        t.Equal(expected,AudioGain.Balance(42),"ext 42（5 格）：右聲道衰減 20·log10(右/左)");
        t.Check(AudioGain.Balance(1)<AudioGain.Balance(42)&&AudioGain.Balance(42)<0,"距離愈遠（ext 愈小）愈偏左",new{b1=AudioGain.Balance(1),b42=AudioGain.Balance(42)});
        t.Check(!AudioGain.ValidExt(-1)&&!AudioGain.ValidExt(129)&&AudioGain.ValidExt(0)&&AudioGain.ValidExt(128),"ext 範圍 0..128");
        t.Equal(.5,new EmitterLevel(100,64).Pan,"EmitterLevel.Pan＝ext/128");
    }

    // AudioCore.SetLevel：ext 變了才下 Balance；同值不重下；回中央也下（0）；失敗保留 voice、下次重試。
    static void CorePan(WorldModuleTestContext t){
        var d=new Driver();var core=new AudioCore(Assets(),d,8);var owner=new AudioOwner("pan",1);
        try{
            core.Register(owner);core.Configure(new(1,false,true));
            t.Check(core.Play(Sid,owner,null,1).Success&&!d.Log.Contains("balance"),"新 voice 是中央：開播不下 Balance");
            var r=core.SetLevel(owner,.5,42);
            t.Check(r.Success&&d.Balances.Count==1&&d.Balances[0].Balance==AudioGain.Balance(42),"ext 42：下 Balance",new{r.Status,d.Balances});
            t.Check(core.AppliedPans(owner).SequenceEqual([42]),"記下已套用的 pan");
            core.SetLevel(owner,.4,42);
            t.Equal(1,d.Balances.Count,"ext 不變：只改 Gain，不重下 Balance");
            core.SetLevel(owner,.4,64);
            t.Check(d.Balances.Count==2&&d.Balances[^1].Balance==0,"回到中央：下 Balance 0");
            d.FailBalance=true;
            var bad=core.SetLevel(owner,.3,10);
            t.Check(!bad.Success&&bad.Status=="PanFailed"&&core.Count==1&&core.AppliedPans(owner).SequenceEqual([64]),"Balance 失敗：回 PanFailed、voice 保留（Gain 已受控）",bad.Status);
            t.Check(bad.Events.Any(e=>e.Stage=="PanFailed"),"記下 PanFailed 事件");
            d.FailBalance=false;
            t.Check(core.SetLevel(owner,.3,10).Success&&core.AppliedPans(owner).SequenceEqual([10]),"下次更新重試成功");
        }finally{core.Shutdown();}
    }

    // 發聲體保存上一輪 (vol, ext)（A091C0 寫 +12／+16）；之後開播的 voice 直接用它（A06C30、A06AE0），Play 前就下 Balance。
    static void CoreStoredLevel(WorldModuleTestContext t){
        var d=new Driver();var core=new AudioCore(Assets(),d,8);var owner=new AudioOwner("stored",1);
        try{
            core.Register(owner);core.Configure(new(.8,false,true));
            t.Equal("NoLiveVoice",core.SetLevel(owner,.25,20).Status,"還沒有 voice：仍記下這一輪的值");
            d.Log.Clear();
            t.Check(core.Play(Sid,owner,null,1).Success,"開播");
            t.Check(Math.Abs(d.LastGain-.2)<1e-12,"新 voice 用保存的比例：0.8 × 0.25",d.LastGain);
            int balance=d.Log.IndexOf("balance"),play=d.Log.IndexOf("play");
            t.Check(balance>=0&&balance<play&&d.Balances[^1].Balance==AudioGain.Balance(20),"Play 之前先套保存的 pan",string.Join(",",d.Log));
            t.Check(core.Levels(owner).SequenceEqual([(.25,20)])&&core.AppliedPans(owner).SequenceEqual([20]),"voice 記下保存的值");
            core.Retire(owner);var next=owner with{Generation=2};core.Register(next);
            core.Play(Sid,next,null,.5);
            t.Check(Math.Abs(d.LastGain-.4)<1e-12&&core.Levels(next).SequenceEqual([(.5,64)]),"換代（Retire→Register）：不沿用上一代的值",d.LastGain);
        }finally{core.Shutdown();}
    }

    sealed class PlainEffectAudio:IEffectAudio {
        static Task<EffectAudioReply> Ok(string s)=>Task.FromResult(new EffectAudioReply(true,s));
        public Task<EffectAudioReply> RegisterAsync(EffectOwner owner)=>Ok("Registered");
        public Task<EffectAudioReply> RetireAsync(EffectOwner owner)=>Ok("OwnerRetired");
        public Task<EffectAudioReply> PlayAsync(int id,EffectOwner owner)=>Ok("StartedWav");
        public Task<EffectAudioReply> ConfigureAsync(double gain,bool muted,bool enabled)=>Ok("ControlsApplied");
        public Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan budget)=>Ok("Released");
        public Task WhenReleased=>Task.CompletedTask;
        public EffectAudioReply LastMaintenance=>new(true,"NotStarted");
    }
    sealed class LevelEffectAudio:IEffectAudio,IEffectAudioLevel {
        readonly PlainEffectAudio inner=new();
        public readonly List<(EffectOwner Owner,double Scale,int Ext)> Levels=[];
        public Task<EffectAudioReply> SetVoiceLevelAsync(EffectOwner owner,double scale,int ext){Levels.Add((owner,scale,ext));return Task.FromResult(new EffectAudioReply(true,"LevelApplied"));}
        public Task<EffectAudioReply> RegisterAsync(EffectOwner owner)=>inner.RegisterAsync(owner);
        public Task<EffectAudioReply> RetireAsync(EffectOwner owner)=>inner.RetireAsync(owner);
        public Task<EffectAudioReply> PlayAsync(int id,EffectOwner owner)=>inner.PlayAsync(id,owner);
        public Task<EffectAudioReply> ConfigureAsync(double gain,bool muted,bool enabled)=>inner.ConfigureAsync(gain,muted,enabled);
        public Task<EffectAudioReply> ShutdownWithinAsync(TimeSpan budget)=>inner.ShutdownWithinAsync(budget);
        public Task WhenReleased=>Task.CompletedTask;
        public EffectAudioReply LastMaintenance=>inner.LastMaintenance;
    }

    // IEffectAudio 的改音量入口：AudioApiTransport 直通 AudioPlayer（真的改到 voice 的 Gain 與 Balance）；沒有能力的實作回 Unsupported。
    static void EffectEntry(WorldModuleTestContext t){
        var d=new Driver();var player=AudioPlayer.ForTests(new AudioCore(Assets(),d,8));
        IEffectAudio audio=new AudioApiTransport(player);var owner=Owner(0);
        try{
            audio.RegisterAsync(owner).Wait();audio.ConfigureAsync(.5,false,true).Wait();
            t.Check(audio.PlayAsync(Sid,owner).Result.Success,"6016 transport 開播");
            var r=audio.SetVoiceLevelAsync(owner,.6,30).Result;
            t.Check(r.Success&&r.Status=="LevelApplied"&&Math.Abs(d.LastGain-.3)<1e-12,"經 IEffectAudio 入口改音量：0.5 × 0.6",new{r.Status,d.LastGain});
            t.Check(d.Balances.Count==1&&d.Balances[0].Balance==AudioGain.Balance(30),"ext 30 套成 Balance");
            t.Equal("StaleOrUnknownOwner",audio.SetVoiceLevelAsync(Owner(0,9),.5,64).Result.Status,"代數不符：拒絕");
            t.Equal("InvalidExt",audio.SetVoiceLevelAsync(owner,.5,200).Result.Status,"ext > 128：拒絕");
            t.Equal("Unsupported",new PlainEffectAudio().SetVoiceLevelAsync(owner,.5,64).Result.Status,"沒有 IEffectAudioLevel 的實作：Unsupported");
        }finally{t.Equal("Released",audio.ShutdownWithinAsync(TimeSpan.FromSeconds(5)).Result.Status,"關閉釋放");}
    }

    // EffectVoiceLevels：值變才送；null 不送；夾範圍；消失的 owner 忘掉；換 transport 重來；沒有 transport 不送。
    static void EffectTracker(WorldModuleTestContext t){
        var audio=new LevelEffectAudio();var levels=new EffectVoiceLevels();
        EffectOwner a=Owner(1),b=Owner(2);
        var frames=new[]{Frame(a,10,10),Frame(b,20,20)};
        t.Equal(2,levels.Apply(audio,frames,f=>(.5,f.X==10?40:50)),"第一次：兩個 owner 都送");
        t.Equal(0,levels.Apply(audio,frames,f=>(.5,f.X==10?40:50)),"值不變：不送");
        t.Equal(1,levels.Apply(audio,frames,f=>f.X==10?(.4,40):(.5,50)),"只有一個變：送一筆");
        t.Equal(0,levels.Apply(audio,frames,_=>null),"null（自身不在場）：不送");
        t.Equal(1,levels.Apply(audio,[frames[0]],_=>(3.0,999)),"夾範圍後值變：送");
        t.Check(audio.Levels[^1] is (_,1d,128),"夾成 1、128");
        t.Equal(1,levels.Apply(audio,frames,f=>f.X==10?(1.0,128):(.5,50)),"b 消失後再出現：視為新的，重送");
        var other=new LevelEffectAudio();
        t.Equal(2,levels.Apply(other,frames,f=>f.X==10?(1.0,128):(.5,50)),"換 transport：全部重送");
        t.Equal(0,levels.Apply(null,frames,_=>(.1,1)),"沒有 transport：不送");
        t.Equal(2,levels.Apply(other,frames,f=>f.X==10?(1.0,128):(.5,50)),"transport 回來：重送");
        t.Equal(2,levels.Apply(new PlainEffectAudio(),frames,_=>(.2,2)),"沒有能力的 transport：入口回 Unsupported，不拋例外");
    }

    // WorldSession.EffectVoiceLevel：6016 發聲體＝特效所在格，照 SD§2.3；類別 SoundVolume；自身不在場回 null。
    static void EffectWorldLevel(WorldModuleTestContext t){
        var volumes=new SoundVolumes();volumes[SoundCategory.Sound]=.5;volumes[SoundCategory.EnvSound]=.1;
        var w=new WorldSession();var owner=Owner(3);
        t.Equal<(double,int)?>(null,w.EffectVoiceLevel(Frame(owner,32710,32805),volumes),"自身不在場：null");
        w.SeedSoundFollowup(123,32705,32805);
        t.Equal<(double,int)?>((.5*83/100d,42),w.EffectVoiceLevel(Frame(owner,32710,32805),volumes),"特效 5 格：vol 83、ext 42、SoundVolume");
        t.Equal<(double,int)?>((.5,64),w.EffectVoiceLevel(Frame(owner,32705,32805),volumes),"同一格：100、64（中央）");
        t.Equal<(double,int)?>((0d,1),w.EffectVoiceLevel(Frame(owner,32705,32825),volumes),"曼哈頓 > 14：vol 0、ext 1");
        // 串起來：自身移動 → 下一次更新把新 (vol, ext) 經入口送出。
        var audio=new LevelEffectAudio();var levels=new EffectVoiceLevels();var frames=new[]{Frame(owner,32710,32805)};
        levels.Apply(audio,frames,f=>w.EffectVoiceLevel(f,volumes));
        w.SeedSoundFollowup(123,32708,32805);
        t.Equal(1,levels.Apply(audio,frames,f=>w.EffectVoiceLevel(f,volumes)),"自身移到 2 格：送一次");
        t.Check(audio.Levels[^1] is (_,var s,55)&&Math.Abs(s-.49)<1e-12,"新值 0.5 × 98/100、ext 55",audio.Levels[^1]);
    }

    // MIDI 後備（8F63D0、8F6340）：沒有任何 music*.mp3 時，音樂要求一律換成 sound\0.mid 循環；其他要求不變。
    static void Midi(WorldModuleTestContext t){
        var r=AudioStreamPlayer.Route(["music7.mp3"],false,true);
        t.Check(r.Midi&&r.Loop&&r.Candidates.SequenceEqual(["0.mid"]),"MIDI 模式：music7.mp3 → 0.mid、循環");
        var e=AudioStreamPlayer.Route(["55.mp3","55.wav"],false,true);
        t.Check(!e.Midi&&!e.Loop&&e.Candidates.SequenceEqual(["55.mp3","55.wav"]),"MIDI 模式：環境音不受影響");
        var n=AudioStreamPlayer.Route(["music7.mp3"],false,false);
        t.Check(!n.Midi&&!n.Loop&&n.Candidates.SequenceEqual(["music7.mp3"]),"一般模式：不換");
        t.Check(AudioStreamPlayer.Route(["Music12.MP3"],false,true).Midi,"檔名不分大小寫（客戶端有 Music1012.mp3）");
        string root=Path.Combine(AppContext.BaseDirectory,"sound-followup-2-midi");
        string withMusic=Path.Combine(root,"with-music"),noMusic=Path.Combine(root,"no-music");
        Directory.CreateDirectory(withMusic);Directory.CreateDirectory(noMusic);
        foreach(var f in new[]{Path.Combine(withMusic,"Music3.mp3"),Path.Combine(noMusic,"55.mp3"),Path.Combine(noMusic,"0.mid")})
            if(!File.Exists(f))File.WriteAllBytes(f,[]);
        t.Check(!AudioStreamPlayer.NeedsMidi(withMusic),"有 music*.mp3：不切 MIDI");
        t.Check(AudioStreamPlayer.NeedsMidi(noMusic),"只有一般音效 mp3：切 MIDI（byte_13D3F88=1）");
        t.Check(!AudioStreamPlayer.NeedsMidi(Path.Combine(root,"missing")),"目錄不存在：不切");
    }

    sealed class StreamDriver(string tag,List<string> log):IStreamVoiceDriver {
        public readonly Dictionary<string,string> NextStatus=[];
        public bool Open(string path,string voice){log.Add($"{tag} open {Path.GetFileName(path)}");return true;}
        public bool Volume(string voice,int db){log.Add($"{tag} volume {db}");return true;}
        public bool Run(string voice){log.Add($"{tag} run");return true;}
        public bool Rewind(string voice){log.Add($"{tag} rewind");return true;}
        public string Status(string voice)=>NextStatus.Remove(voice,out var s)?s:"playing";
        public void Close(string voice)=>log.Add($"{tag} close");
    }
    // 串流層：MIDI 走 MCI 分流（這裡用替身）、播完倒回重播；新音樂要求取代舊的 MIDI（單一 MCI 裝置）。
    static void MidiPlayer(WorldModuleTestContext t){
        var log=new List<string>();var ds=new StreamDriver("ds",log);var mci=new StreamDriver("mci",log);
        const string Root=@"X:\client\sound";
        var p=AudioStreamPlayer.ForTests(Root,new RoutingStreamDriver(ds,mci),f=>f.EndsWith("0.mid",StringComparison.OrdinalIgnoreCase)||f.EndsWith("55.wav",StringComparison.OrdinalIgnoreCase),midiFallback:true);
        t.Check(p.MidiFallback,"ForTests 可指定 MIDI 模式");
        long m=p.Start(["music7.mp3"],false,.5);
        t.Check(m!=0&&log.Contains("mci open 0.mid")&&log.Contains("mci run")&&!log.Any(l=>l.StartsWith("ds ",StringComparison.Ordinal)),"音樂要求 → MCI 開 0.mid 播放",string.Join(",",log));
        long w=p.Start(["55.mp3","55.wav"],false,1);
        t.Check(w!=0&&log.Contains("ds open 55.wav"),"音效仍走 DirectShow");
        mci.NextStatus["p24s"+m]="ended";p.Poll();
        t.Check(log.Contains("mci rewind")&&!p.Finished(m)&&p.Stats.Looped==1,"0.mid 播完：倒回重播（8F6340 MCI_PLAY from 0）");
        log.Clear();
        long m2=p.Start(["music9.mp3"],false,.5);
        t.Check(m2!=0&&p.Finished(m)&&!p.Finished(m2)&&log.IndexOf("mci close")>=0&&log.IndexOf("mci close")<log.IndexOf("mci open 0.mid"),"新音樂要求：先關舊 MIDI 再開新的",string.Join(",",log));
        p.Stop(m);
        t.Check(!p.Finished(m2),"再停舊 handle：不影響新的");
        p.Dispose();
        var normal=AudioStreamPlayer.ForTests(Root,new RoutingStreamDriver(ds,mci),_=>false);
        t.Equal(0L,normal.Start(["music7.mp3"],false,1),"一般模式缺 music7.mp3：照舊回 0，不改播 MIDI");
    }

    // music.def 沒有聲像欄位；TW16 開串流後一律 AIL_set_sample_volume_pan(…, 0.5)（常數 0x115FFD0＝0.5，FO 0xD5EBD0）＝中央。
    // 串流層不下 Balance，DirectShow renderer 預設 balance 0＝中央，與原版相同。這裡只確認 0.5 對應的 balance 是 0。
    static void MusicPan(WorldModuleTestContext t){
        t.Equal(0,AudioGain.Balance((int)(0.5*128)),"音樂串流固定 pan 0.5 → balance 0");
    }
}
