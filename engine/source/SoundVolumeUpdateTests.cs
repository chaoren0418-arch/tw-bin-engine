using System.Security.Cryptography;
using P24.Audio;
namespace P06;

// T302（#714，R4-K）離線測試：播放中隨距離改音量。
// 依據：SD§2.3（docs/reference/sound.md，A3FCBE..A3FD7B → A091C0 → A06D60）、gap-matrix 13.5 R4-K、第 1 節 op 91。
// 只用假驅動（記錄 Gain 呼叫，不開 DirectShow／音訊裝置）、假 IAudioPlayback、合成封包與自編的位元組檔（不是遊戲素材），不連網路。
static class SoundVolumeUpdateTests {
    const string Name="sound-volume-update";
    const int Sid=4242; // 自編 id，對應下面的合成檔。

    sealed class Driver:IAudioDriver {
        public readonly List<(string Voice,double Gain)> Gains=[];
        public bool FailGain;
        public NativeReply Open(string path,string format,string voice)=>new(0,"ok");
        public NativeReply Gain(string voice,double normalized){Gains.Add((voice,normalized));return FailGain?new(0x80004005,"fake gain failure"):new(0,"ok");}
        public NativeReply Play(string voice)=>new(0,"ok");
        public NativeReply Status(string voice)=>new(0,"playing");
        public NativeReply Stop(string voice)=>new(0,"ok");
        public NativeReply Close(string voice)=>new(0,"ok");
        public double Last=>Gains[^1].Gain;
    }
    // 沒有覆寫 SetVoiceLevelAsync：走介面預設（Unsupported）。
    class Basic:IAudioPlayback {
        public readonly List<string> Calls=[];
        static Task<AudioReply> Ok(string s)=>Task.FromResult(new AudioReply(true,s,null,0,[]));
        public Task<AudioReply> RegisterAsync(AudioOwner owner){Calls.Add("register");return Ok("Registered");}
        public Task<AudioReply> RetireAsync(AudioOwner owner){Calls.Add("retire");return Ok("OwnerRetired");}
        public Task<AudioReply> PlayAsync(int id,AudioOwner owner){Calls.Add("play");return Ok("StartedWav");}
        public Task<AudioReply> ConfigureAsync(double gain,bool muted,bool enabled)=>Ok("ControlsApplied");
        public Task<AudioReply> ShutdownWithinAsync(TimeSpan budget)=>Ok("Released");
        public Task WhenReleased=>Task.CompletedTask;
        public AudioReply LastMaintenance{get;}=new(true,"NotStarted",null,0,[]);
    }
    sealed class Recording:Basic,IAudioPlayback {
        public readonly List<(AudioOwner Owner,double Scale,int Ext)> Levels=[];
        public Task<AudioReply> SetVoiceLevelAsync(AudioOwner owner,double scale,int ext){Levels.Add((owner,scale,ext));return Task.FromResult(new AudioReply(true,"LevelApplied",null,0,[]));}
    }

    static AudioAssets Assets(){
        string root=Path.Combine(AppContext.BaseDirectory,"sound-volume-update-tests");
        Directory.CreateDirectory(root);
        byte[] bytes="synthetic-voice-level-test"u8.ToArray();
        string rel=$"sound/{Sid}.wav";
        string path=Path.Combine(root,rel);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if(!File.Exists(path)||!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))File.WriteAllBytes(path,bytes);
        return new(root,[new AssetPin(Sid,"wav",rel,bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)))]);
    }
    static SoundSchedule Once(int id)=>new([new SoundFrame(0,0,[id])],500,false);
    static byte[] Pack(uint id,ushort x,ushort y)=>LoginPackets.Pad(Build(b=>{
        static void Text(BinaryWriter b,string text){b.Write(LoginPackets.TextEncoding.GetBytes(text));b.Write((byte)0);}
        b.Write((byte)87);b.Write(x);b.Write(y);b.Write(id);b.Write((ushort)1);b.Write((byte)0);b.Write(new byte[3]);b.Write(0u);b.Write((ushort)0);
        Text(b,"emitter");Text(b,"");b.Write((byte)0);b.Write(0u);Text(b,"");Text(b,"");b.Write((byte)0);b.Write((byte)100);b.Write(new byte[3]);b.Write((byte)255);b.Write((byte)255);
    }));
    static byte[] Build(Action<BinaryWriter> write){using var s=new MemoryStream();using(var b=new BinaryWriter(s))write(b);return s.ToArray();}

    [WorldModuleTest(Name)]
    static void Run(WorldModuleTestContext t){Core(t);Player(t);Controller(t);WorldLevels(t);EndToEnd(t);}

    // AudioCore：播放中改音量會重下 Gain（主音量 × voice 比例），之後主音量變動沿用新比例；0 只靜音不停；失敗就釋放。
    static void Core(WorldModuleTestContext t){
        var d=new Driver();var core=new AudioCore(Assets(),d,8);var owner=new AudioOwner("lv",1);
        try{
            core.Register(owner);core.Configure(new(.5,false,true));
            var play=core.Play(Sid,owner,null,1);
            t.Check(play.Success&&d.Last==.5,"開播：主音量 0.5 × 比例 1",play.Status);
            var r=core.SetLevel(owner,.32,1);
            t.Check(r.Success&&r.Status=="LevelApplied"&&Math.Abs(d.Last-.16)<1e-12,"播放中改音量：0.5 × 0.32",new{r.Status,d.Last});
            t.Check(core.Levels(owner).SequenceEqual([(.32,1)]),"voice 記下新比例與 ext");
            core.Configure(new(1,false,true));
            t.Check(Math.Abs(d.Last-.32)<1e-12,"主音量改 1：沿用播放中的新比例",d.Last);
            t.Equal("StaleOrUnknownOwner",core.SetLevel(owner with{Generation=2},.5,64).Status,"代數不符：拒絕");
            t.Equal("InvalidVoiceScale",core.SetLevel(owner,1.5,64).Status,"比例 > 1：拒絕");
            t.Equal("InvalidVoiceScale",core.SetLevel(owner,double.NaN,64).Status,"比例 NaN：拒絕");
            t.Equal("InvalidExt",core.SetLevel(owner,.5,129).Status,"ext > 128：拒絕");
            t.Check(core.SetLevel(owner,0,0).Success&&d.Last==0&&core.Count==1,"vol 夾 0：靜音但不停");
            core.Finish(owner);
            t.Check(core.SetLevel(owner,.6,20).Status=="LevelApplied"&&Math.Abs(d.Last-.6)<1e-12,"收尾中（Finish）的尾音也跟著改");
            t.Equal("StaleOrUnknownOwner",core.SetLevel(new AudioOwner("other",1),.5,64).Status,"未登錄 owner：拒絕");
            d.FailGain=true;
            var bad=core.SetLevel(owner,.7,30);
            t.Check(!bad.Success&&bad.Status=="GainFailed"&&core.Count==0,"Gain 失敗：釋放 voice，不留不受控音量",bad.Status);
            d.FailGain=false;
            var other=new AudioOwner("empty",1);core.Register(other);
            t.Check(core.SetLevel(other,.5,64) is {Success:true,Status:"NoLiveVoice"},"沒有播放中的 voice：成功、不下 Gain");
        }finally{core.Shutdown();}
    }

    // AudioPlayer：經傳輸執行緒套用；同步拒絕過期 owner。
    static void Player(WorldModuleTestContext t){
        var d=new Driver();var player=AudioPlayer.ForTests(new AudioCore(Assets(),d,8));var owner=new AudioOwner("player",1);
        try{
            t.Equal("StaleOrUnknownOwner",player.SetVoiceLevelAsync(owner,.5,64).Result.Status,"未登錄：同步拒絕");
            player.RegisterAsync(owner).Wait();player.ConfigureAsync(.8,false,true).Wait();
            t.Check(player.PlayAsync(Sid,owner,1).Result.Success,"開播");
            t.Equal("InvalidExt",player.SetVoiceLevelAsync(owner,.5,-1).Result.Status,"ext < 0：同步拒絕");
            var r=player.SetVoiceLevelAsync(owner,.5,42).Result;
            t.Check(r.Success&&Math.Abs(d.Last-.4)<1e-12,"傳輸層套用：0.8 × 0.5",new{r.Status,d.Last});
            IAudioPlayback basic=new Basic();
            t.Equal("Unsupported",basic.SetVoiceLevelAsync(owner,.5,64).Result.Status,"介面預設：Unsupported");
        }finally{t.Equal("Released",player.ShutdownWithinAsync(TimeSpan.FromSeconds(5)).Result.Status,"關閉釋放");}
    }

    // CombatSfxController.ApplyLevels：值變才送；null 不送；夾範圍；收尾中的實例也送；退休後不送。
    static void Controller(WorldModuleTestContext t){
        var audio=new Recording();var seen=new List<CombatSoundObservation>();
        var c=new CombatSfxController(audio,new Random(1),seen.Add);
        var a=new CombatSoundInstance("a",1,0,0,0,0,1000,Once(Sid));
        c.Pump(1,[a],0,true);
        t.Check(audio.Calls.Contains("play"),"開播");
        t.Equal(1,c.ApplyLevels(_=>(.5,40)),"第一次：送出");
        t.Check(audio.Levels.Count==1&&audio.Levels[0] is (_,.5,40),"送出 0.5、40");
        t.Equal(0,c.ApplyLevels(_=>(.5,40)),"值不變：不送");
        t.Equal(0,c.ApplyLevels(_=>null),"null（發聲體不在場）：不送");
        t.Equal(1,c.ApplyLevels(_=>(2.0,300)),"值變：送");
        t.Check(audio.Levels[^1] is (_,1d,128),"夾成 1、128");
        t.Check(seen.Any(o=>o.Stage=="level"&&o.Key=="a"&&o.Reply.Success),"記錄 level 觀察");
        var drain=new CombatSoundInstance("d",2,0,0,0,0,100,Once(Sid)){DrainOnEnd=true};
        c.Pump(1,[a,drain],0,true);c.Pump(1,[a,drain],150,true);
        int before=audio.Levels.Count;
        c.ApplyLevels(s=>s.Key=="d"?(.3,10):(1.0,128));
        t.Check(audio.Levels.Count==before+1&&audio.Levels[^1] is (_,.3,10),"收尾中的實例也改音量");
        c.Pump(1,[],200,false);
        t.Equal(0,c.ApplyLevels(_=>(.1,1)),"停用（退休＋取消收尾）後：不送");
        c.Shutdown().Wait();
        t.Equal(0,c.ApplyLevels(_=>(.2,2)),"關閉後：不送");
    }

    // WorldSession.VoiceLevel：S22＝Self（100、64）× 類別音量；有發聲體照 SD§2.3 算；不在場回 null。
    static void WorldLevels(WorldModuleTestContext t){
        var volumes=new SoundVolumes();volumes[SoundCategory.Sound]=.5;volumes[SoundCategory.EnvSound]=.25;
        var w=new WorldSession();long clock=1000;w.PacketMediaClock=()=>clock;
        w.ReceiveSoundPacket(LoginPackets.Pad([22,0,55,0]));
        w.ReceiveSoundPacket(LoginPackets.Pad([22,1,56,0]));
        var sounds=w.ComposePacketSounds([],1200,_=>500);
        var once=sounds.Single(s=>!s.Schedule.Loop);var loop=sounds.Single(s=>s.Schedule.Loop);
        t.Equal<(double,int)?>((.5,64),w.VoiceLevel(once,volumes),"S22 模式 0：SoundVolume × 100/100、ext 64");
        t.Equal<(double,int)?>((.25,64),w.VoiceLevel(loop,volumes),"S22 模式 1：EnvSoundVolume");
        clock=5000;w.ComposePacketSounds([],5000,_=>500);
        t.Equal<(double,int)?>(null,w.VoiceLevel(once,volumes),"S22 播完：null（不更新）");

        var v=new WorldSession();v.SeedSoundFollowup(123,32705,32805);
        v.Receive(Pack(902,32710,32805),Name);
        var hit=new CombatSoundInstance("combat/902",1,902,1,0,0,1000,Once(Sid));
        t.Equal<(double,int)?>((.5*83/100d,42),v.VoiceLevel(hit,volumes),"發聲體 5 格：vol 83、ext 42");
        v.SeedSoundFollowup(123,32708,32805);
        t.Equal<(double,int)?>((.5*98/100d,55),v.VoiceLevel(hit,volumes),"自身移到 2 格：vol 98、ext 55");
        v.SeedSoundFollowup(123,32730,32805);
        t.Equal<(double,int)?>((0d,1),v.VoiceLevel(hit,volumes),"曼哈頓 > 14：vol 夾 0、ext 1");
        t.Equal<(double,int)?>(null,v.VoiceLevel(hit with{Actor=999},volumes),"發聲體不在場：null");
        t.Equal<(double,int)?>(null,v.VoiceLevel(hit with{Actor=0},volumes),"沒有發聲體的非 S22：null");
    }

    // 串起來：S22 播放中，類別音量改了 → 下一次更新把新比例送到播放層。
    static void EndToEnd(WorldModuleTestContext t){
        var volumes=new SoundVolumes();
        var w=new WorldSession();long clock=1000;w.PacketMediaClock=()=>clock;
        w.ReceiveSoundPacket(LoginPackets.Pad([22,1,57,0]));
        var audio=new Recording();var c=new CombatSfxController(audio,new Random(1),_=>{});
        var sounds=w.ComposePacketSounds([],1200,_=>500);
        c.Pump(w.WorldEpoch,sounds,1200,true);
        c.ApplyLevels(s=>w.VoiceLevel(s,volumes));
        t.Check(audio.Levels.Count==1&&audio.Levels[0] is (_,1d,64),"S22 循環：1、64");
        volumes[SoundCategory.EnvSound]=.4;
        sounds=w.ComposePacketSounds([],1400,_=>500);c.Pump(w.WorldEpoch,sounds,1400,true);
        t.Equal(1,c.ApplyLevels(s=>w.VoiceLevel(s,volumes)),"類別音量變動：送一次");
        t.Check(audio.Levels[^1] is (_,.4,64),"新比例 0.4");
        t.Equal(0,c.ApplyLevels(s=>w.VoiceLevel(s,volumes)),"沒變：不送");
        c.Shutdown().Wait();
    }
}
