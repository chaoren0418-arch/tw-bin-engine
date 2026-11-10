using System.Text;
using P24.Audio;
using P24.HostAudio;
namespace P06;

// T351（#814，gap-matrix 15.6 R7-L）離線測試：
// ① 地圖 `<`／`[` 單次音效播放中依距離改音量（A091C0 改發聲體名下所有還在播的 sample）；
// ② MIDI 模式遇到 0..8 號系統槽時停播（8F1C30／8F2FB0／8F3B90／8F3D70，docs/reports/issue-737.md）；
// ③ Mss32 響度曲線 vol^(5/3)、中央 0.8122 只當資料（播放路徑不變，真機再決定，M26）。
// 由 --module-tests 執行（名稱 sound-followup-3）。只用合成 music.def 文字、合成地圖特效 JSON、記錄器與替身驅動；
// 不讀原客戶端、不讀 engine/assets、不開音訊裝置、遊戲、網路或 DB。
static class SoundFollowup3Tests {
    const string Name="sound-followup-3";

    // ticks 12 per step = 500 ms; two steps = 1000 ms cycle.
    static MapEffectAppearance Appearance(int id,params string[] events){
        var steps=string.Join(",",events.Select(e=>$"{{\"frame\":0,\"ticks\":12,\"events\":\"{e}\"}}"));
        string json=$"{{\"id\":{id},\"flags\":0,\"tickRate\":24,\"frames\":[{{\"index\":0,\"bounds\":[0,0,0,0],\"blocks\":[{{\"a\":0,\"b\":0,\"z\":0,\"order\":0,\"pixels\":[[0,0,1]]}}]}}],\"sequence\":[{steps}]}}";
        return new(Encoding.UTF8.GetBytes(json));
    }

    sealed class Loops:IAmbientLoopAudio {
        public bool Start(MapAmbientVoice voice,int volume)=>true;
        public void SetVolume(MapAmbientVoice voice,int volume){}
        public void Stop(MapAmbientVoice voice){}
    }
    // 支援改音量的單次音效後端。
    sealed class Sink:IMapOneShotAudio {
        public readonly List<(MapOneShotVoice Voice,int Volume)> Played=[];
        public readonly List<(long Number,int Volume)> Volumes=[];
        public readonly HashSet<long> Done=[];
        public bool Play(MapOneShotVoice voice,int volume){Played.Add((voice,volume));return true;}
        public void SetVolume(MapOneShotVoice voice,int volume)=>Volumes.Add((voice.Number,volume));
        public bool Finished(MapOneShotVoice voice)=>Done.Contains(voice.Number);
    }
    // 只實作 Play 的舊式後端（R6-A 的形狀）：預設 Finished＝true，不被追蹤。
    sealed class PlayOnly:IMapOneShotAudio {
        public int Count;
        public bool Play(MapOneShotVoice voice,int volume){Count++;return true;}
    }
    sealed class Stream:IAudioStream {
        public readonly List<(string[] Files,bool Loop,double Volume)> Started=[];
        public readonly List<(long Handle,double Volume)> Volumes=[];
        public readonly HashSet<long> Done=[];
        public bool Midi;
        long next;
        public long Start(IReadOnlyList<string> candidates,bool loop,double volume){Started.Add((candidates.ToArray(),loop,volume));return ++next;}
        public void SetVolume(long handle,double volume)=>Volumes.Add((handle,volume));
        public void Stop(long handle)=>Done.Add(handle);
        public bool Finished(long handle)=>Done.Contains(handle);
        public bool MidiFallback=>Midi;
    }
    sealed class Driver(string tag,List<string> log):IStreamVoiceDriver {
        public readonly Dictionary<string,string> NextStatus=[];
        public bool Open(string path,string voice){log.Add($"{tag} open {Path.GetFileName(path)}");return true;}
        public bool Volume(string voice,int db){log.Add($"{tag} volume {db}");return true;}
        public bool Run(string voice){log.Add($"{tag} run");return true;}
        public bool Rewind(string voice){log.Add($"{tag} rewind");return true;}
        public string Status(string voice)=>NextStatus.Remove(voice,out var s)?s:"playing";
        public void Close(string voice)=>log.Add($"{tag} close");
    }
    sealed class Recorder:IMusicStreamAudio {
        public readonly List<MusicTrack> Started=[];
        public bool Start(MusicTrack track,double volume){Started.Add(track);return true;}
        public void SetVolume(MusicTrack track,double volume){}
        public void Stop(MusicTrack track){}
        public bool Finished(MusicTrack track)=>false;
    }
    // 合成定義（自編數值，不是原檔內容）：槽 0→82（登入）、槽 8→5（選角）；槽 9／10 用預設 i→i。
    const string Def=
        "System Setting Begin\r\n\tA=82;\r\n\tB=1;\r\n\tC=2;\r\n\tD=3;\r\n\tE=4;\r\n\tF=5;\r\n\tG=6;\r\n\tH=7;\r\n\tI=5;\r\nSystemMenu Exit\r\n\r\n"+
        ";FIELD\r\nLOC 4\r\nMUSIC 30\r\n";
    static MusicContext Scene(MusicScene scene)=>new(scene,4,32700,32800,true,1);

    [WorldModuleTest(Name)]
    static void Run(WorldModuleTestContext t){
        OneShotFollowsDistance(t);OneShotLegacyBackend(t);ProductOneShot(t);ProductOneShotThroughStream(t);
        TrackSlots(t);MidiSystemSlots(t);MidiEndToEnd(t);MilesCurveData(t);
    }

    // ① A091C0：播放在前、調音量在後；之後每次進幀都把發聲體名下還在播的單次 sample 改成新的距離音量。
    static void OneShotFollowsDistance(WorldModuleTestContext t){
        var sink=new Sink();var model=new MapAmbientSound(new Loops(),new Random(7),new SoundCooldown(),sink);
        var ap=Appearance(1200,"<300","");
        model.Update([new("a",100,100,ap,0)],(100,100),10000,true);
        t.Check(sink.Played is [{Volume:100}]&&sink.Volumes.Count==0&&model.OneShotsLive==1,"進幀播一次（100），音量未變不調",new{sink.Played,sink.Volumes});
        long first=sink.Played[0].Voice.Number;

        model.Update([new("a",100,100,ap,500)],(105,100),10500,true);
        t.Check(sink.Volumes.SequenceEqual([(first,MapAmbientSound.Volume(5,0))])&&MapAmbientSound.Volume(5,0)==83,"下一格：還在播的單次音效改成 5 格的 83",sink.Volumes);
        model.Update([new("a",100,100,ap,600)],(110,100),10600,true);
        t.Equal(1,sink.Volumes.Count,"同一格不重算（A091C0 只在進幀時）");

        // 走遠：下一輪新的那一聲先用上一輪的 83，再一起降到 0；不停止。
        model.Update([new("a",100,100,ap,1000)],(130,100),11000,true);
        long second=sink.Played[^1].Voice.Number;
        t.Check(sink.Played.Count==2&&sink.Played[1].Volume==83,"新一聲用上一輪音量",sink.Played);
        t.Check(sink.Volumes.Skip(1).OrderBy(v=>v.Number).SequenceEqual([(first,0),(second,0)]),"兩聲都降到 0（不停止）",sink.Volumes);
        t.Equal(2,model.OneShotsLive,"兩聲都還在追蹤");

        // 第一聲播完：不再追蹤、不再調；第二聲跟著回到 100。
        sink.Done.Add(first);
        model.Update([new("a",100,100,ap,1500)],(100,100),11500,true);
        t.Check(sink.Volumes.Skip(3).SequenceEqual([(second,100)])&&model.OneShotsLive==1,"播完的不再調音量",sink.Volumes);

        // 後端撤下：已播的不屬於目前後端，忘掉，不再呼叫舊後端。
        model.OneShotAudio=null;
        int before=sink.Volumes.Count;
        model.Update([new("a",100,100,ap,2500)],(105,100),12500,true);
        t.Check(sink.Volumes.Count==before&&model.OneShotsLive==0,"後端撤下：不再調舊 voice",sink.Volumes);

        // 物件移除：不再追蹤（單次音效不在槽表，維持不主動停）。
        model.OneShotAudio=sink;
        model.Update([new("a",100,100,ap,3000)],(100,100),13000,true);
        t.Equal(1,model.OneShotsLive,"重新接上後端：新的一聲被追蹤");
        model.Update([],(100,100),13100,true);
        t.Check(model.OneShotsLive==0&&model.Emitters==0,"物件移除：不再追蹤");

        // `[` 也一樣；`^` 循環仍照舊調音量（不受影響）。
        sink=new();model=new(new Loops(),new Random(7),new SoundCooldown(),sink);
        var br=Appearance(1201,"[400","");
        model.Update([new("b",100,100,br,0)],(100,100),20000,true);
        model.Update([new("b",100,100,br,500)],(103,100),20500,true);
        t.Check(sink.Played is [{Voice.Mode:1}]&&sink.Volumes.SequenceEqual([(sink.Played[0].Voice.Number,MapAmbientSound.Volume(3,0))]),"`[` 單次音效同樣跟著距離",sink.Volumes);
    }

    // 舊式後端（只有 Play）：預設 Finished＝true，模型不追蹤，行為等同 R6-A。
    static void OneShotLegacyBackend(WorldModuleTestContext t){
        var sink=new PlayOnly();var model=new MapAmbientSound(new Loops(),new Random(7),new SoundCooldown(),sink);
        var ap=Appearance(1202,"<500","");
        model.Update([new("c",100,100,ap,0)],(100,100),30000,true);
        model.Update([new("c",100,100,ap,500)],(106,100),30500,true);
        t.Check(sink.Count==1&&model.OneShotsLive==0&&model.OneShotsPlayed==1,"只實作 Play 的後端：播一次、不追蹤");
    }

    // 產品後端：voice → handle；SetVolume＝距離音量/100 × 類別音量；類別變動時還在播的也跟著變；播完不再送。
    static void ProductOneShot(WorldModuleTestContext t){
        var s=new Stream();var audio=new ProductOneShotAudio(s);
        MapOneShotVoice v1=new(1,300,"a",0),v2=new(2,301,"a",1);
        t.Check(audio.Play(v1,83)&&!audio.Finished(v1),"開播、未播完");
        audio.SetVolume(v1,50);
        t.Check(s.Volumes is [(1,var a)]&&Math.Abs(a-.5)<1e-9,"播放中改距離音量 50 → 0.5",s.Volumes);
        audio.SetScale(.5);
        t.Check(s.Volumes.Count==2&&s.Volumes[1].Handle==1&&Math.Abs(s.Volumes[1].Volume-.25)<1e-9,"類別音量 0.5：還在播的變 0.25",s.Volumes);
        audio.SetScale(.5);t.Equal(2,s.Volumes.Count,"類別音量不變：不重送");
        audio.SetVolume(v2,40);t.Equal(2,s.Volumes.Count,"沒播過的 voice：不送");
        audio.SetVolume(v1,0);
        t.Check(s.Volumes.Count==3&&s.Volumes[2].Volume==0&&s.Done.Count==0,"距離音量 0：降到無聲、不停止",s.Volumes);
        s.Done.Add(1);
        t.Check(audio.Finished(v1)&&audio.Live==0,"串流層播完：Finished、Live 0");
        audio.SetVolume(v1,90);t.Equal(3,s.Volumes.Count,"播完後不再送");
    }

    // 經真的 AudioStreamPlayer（替身驅動）：改音量真的下到驅動；播完（Poll 看到 ended）後模型不再追蹤。
    static void ProductOneShotThroughStream(WorldModuleTestContext t){
        var log=new List<string>();var ds=new Driver("ds",log);
        var player=AudioStreamPlayer.ForTests(@"X:\client\sound",ds,f=>f.EndsWith("300.wav",StringComparison.OrdinalIgnoreCase));
        var audio=new ProductOneShotAudio(player);
        var model=new MapAmbientSound(new Loops(),new Random(7),new SoundCooldown(),audio);
        var ap=Appearance(1203,"<300","");
        model.Update([new("d",100,100,ap,0)],(100,100),40000,true);
        t.Check(log.Contains("ds open 300.wav")&&log.Contains("ds volume 0")&&model.OneShotsLive==1,"單次音效開播（mp3 缺、改 wav；音量 100 → 0 dB）",string.Join(",",log));
        model.Update([new("d",100,100,ap,500)],(105,100),40500,true);
        int db=(int)Math.Round(2000*Math.Log10(.83));
        t.Check(log[^1]==$"ds volume {db}","播放中改音量真的下到驅動（83 → 20·log10(0.83)）",string.Join(",",log));
        ds.NextStatus["p24s1"]="ended";player.Poll();
        t.Check(log[^1]=="ds close"&&audio.Live==0,"播完：串流層關閉",string.Join(",",log));
        // 下一輪：新的一聲（handle 2）開播，播完的第一聲被剔除；距離變化只調新的那一聲。
        int count=log.Count;
        model.Update([new("d",100,100,ap,1000)],(110,100),41000,true);
        var after=log.Skip(count).ToArray();
        int db10=(int)Math.Round(2000*Math.Log10(MapAmbientSound.Volume(10,0)/100d));
        t.Check(after.SequenceEqual(["ds open 300.wav",$"ds volume {db}","ds run",$"ds volume {db10}"])&&model.OneShotsLive==1,"播完的那一聲不再調音量，只調新的一聲",string.Join(",",after));
        player.Dispose();
    }

    // BackgroundMusic 帶出「明確系統槽」：登入 0、選角 8、戰鬥 9／10；區域選曲 -1。
    static void TrackSlots(WorldModuleTestContext t){
        var rec=new Recorder();var m=new BackgroundMusic(MusicDefinition.Parse(Def),rec,new Random(1));
        m.Update(Scene(MusicScene.Login),0);
        t.Check(rec.Started[^1] is {Music:82,Slot:0,Kind:MusicKind.Login},"登入：槽 0",rec.Started);
        m.Update(Scene(MusicScene.CharacterSelect),100);
        t.Check(rec.Started[^1] is {Music:5,Slot:8,Loop:true},"選角：槽 8",rec.Started);
        m.Update(Scene(MusicScene.World),200);
        t.Check(rec.Started[^1] is {Music:30,Slot:-1,Kind:MusicKind.Region},"區域選曲：-1",rec.Started);
        m.Attack(false,300);
        t.Check(rec.Started[^1] is {Music:9,Slot:9,Kind:MusicKind.Battle},"打怪：槽 9",rec.Started);
        m.Attack(true,400);
        t.Check(rec.Started[^1] is {Music:10,Slot:10,Kind:MusicKind.Battle},"打玩家：槽 10",rec.Started);
        t.Equal(-1,new MusicTrack(1,12,false,MusicKind.Region).Slot,"舊建構式：Slot 預設 -1");
    }

    // ProductMusicAudio：MIDI 模式遇到 0..8 號系統槽不播；其他照舊（串流層再換成 0.mid）。一般模式不受影響。
    static void MidiSystemSlots(WorldModuleTestContext t){
        t.Check(Enumerable.Range(0,9).All(AudioStreamPlayer.SilencesMidi)&&!AudioStreamPlayer.SilencesMidi(-1)&&!AudioStreamPlayer.SilencesMidi(9)&&!AudioStreamPlayer.SilencesMidi(10),"0..8 停播；-1、9、10 不停");
        IAudioStream plain=new Stream();
        t.Check(!plain.MidiFallback,"IAudioStream 預設不是 MIDI 模式");
        var s=new Stream{Midi=true};var music=new ProductMusicAudio(s);
        t.Check(!music.Start(new(1,82,false,MusicKind.Login,0),1)&&!music.Start(new(2,5,true,MusicKind.CharacterSelect,8),1)&&s.Started.Count==0&&music.MidiSilenced==2,"MIDI 模式：槽 0、8 不開",s.Started);
        t.Check(music.Start(new(3,30,false,MusicKind.Region),1)&&music.Start(new(4,9,false,MusicKind.Battle,9),1)&&s.Started.Count==2,"MIDI 模式：區域、戰鬥照開",s.Started);
        var n=new Stream();var normal=new ProductMusicAudio(n);
        t.Check(normal.Start(new(1,82,false,MusicKind.Login,0),1)&&n.Started.Count==1&&normal.MidiSilenced==0,"一般模式：槽 0 照播");
    }

    // 端到端：BackgroundMusic → ProductMusicAudio → AudioStreamPlayer（MIDI 模式，替身 MCI）。
    // 世界 → 0.mid；回登入（槽 0）→ 關掉 0.mid、不重開（8F5510）；選角（槽 8）同樣不播；再進世界 → 0.mid。
    static void MidiEndToEnd(WorldModuleTestContext t){
        var log=new List<string>();var ds=new Driver("ds",log);var mci=new Driver("mci",log);
        var player=AudioStreamPlayer.ForTests(@"X:\client\sound",new RoutingStreamDriver(ds,mci),f=>f.EndsWith("0.mid",StringComparison.OrdinalIgnoreCase),midiFallback:true);
        var music=new ProductMusicAudio(player);var m=new BackgroundMusic(MusicDefinition.Parse(Def),music,new Random(1));
        t.Check(((IAudioStream)player).MidiFallback,"AudioStreamPlayer.MidiFallback 經介面可見");
        m.Update(Scene(MusicScene.World),0);
        t.Check(log.Contains("mci open 0.mid")&&player.Live==1,"世界：MCI 開 0.mid",string.Join(",",log));
        log.Clear();
        m.Update(Scene(MusicScene.Login),100);
        t.Check(log.Count>0&&log[^1]=="mci close"&&!log.Any(l=>l.Contains(" open ",StringComparison.Ordinal))&&player.Live==0&&m.Current==null,"登入（槽 0）：關掉 MIDI、不重開",string.Join(",",log));
        int quiet=log.Count;
        m.Update(Scene(MusicScene.Login),1200);
        t.Check(log.Count==quiet&&player.Live==0&&music.MidiSilenced==2,"每秒重試仍不播",string.Join(",",log));
        m.Update(Scene(MusicScene.CharacterSelect),1300);
        t.Check(log.Count==quiet&&player.Live==0&&music.MidiSilenced==3,"選角（槽 8）：不播",string.Join(",",log));
        m.Update(Scene(MusicScene.World),1400);
        t.Check(log.Contains("mci open 0.mid")&&player.Live==1,"再進世界：重開 0.mid",string.Join(",",log));
        log.Clear();
        m.Attack(false,1500);
        t.Check(log.Contains("mci open 0.mid")&&player.Live==1,"戰鬥（槽 9）：仍是 0.mid",string.Join(",",log));
        player.Dispose();
    }

    // ③ 資料：Mss32 0x2A9E0 的 v＝vol^(5/3)、中央 0.5^0.3；播放路徑沒有換（AudioGain.Decibels 照舊）。
    static void MilesCurveData(WorldModuleTestContext t){
        t.Check(Math.Abs(AudioGain.MilesCenter-0.8122522)<1e-6,"中央係數 0.5^0.3＝0.8122522（RVA 0x4BF70）",AudioGain.MilesCenter);
        var (l,r)=AudioGain.MilesLevels(1,.5);
        t.Check(l==r&&Math.Abs(l-0.8122522)<1e-6,"vol 1、pan 0.5：左右都是 0.8122");
        (l,r)=AudioGain.MilesLevels(.5,.5);
        t.Check(Math.Abs(l-Math.Pow(.5,5d/3)*0.8122522)<1e-6,"vol 0.5：v＝0.5^(5/3)",l);
        (l,r)=AudioGain.MilesLevels(1,0);
        t.Check(l==1&&r==0,"pan 0：只有左聲道",new{l,r});
        (l,r)=AudioGain.MilesLevels(.8,42/128d);
        t.Check(Math.Abs(l-Math.Pow(1-42/128d,.3)*Math.Pow(.8,5d/3))<1e-9&&Math.Abs(r-Math.Pow(42/128d,.3)*Math.Pow(.8,5d/3))<1e-9&&l>r,"偏一邊：左＝(1−pan)^0.3·v、右＝pan^0.3·v",new{l,r});
        t.Equal(-181,AudioGain.MilesDecibels(AudioGain.MilesCenter),"中央係數換成 1/100 dB 約 −1.81 dB");
        t.Equal(-10000,AudioGain.MilesDecibels(0),"0 → −10000");
        bool threw=false;try{AudioGain.MilesLevels(1.5,.5);}catch(ArgumentOutOfRangeException){threw=true;}
        t.Check(threw,"範圍外 vol 拋出");
        t.Check(AudioGain.Decibels(1)==0&&AudioGain.Decibels(.5)==(int)Math.Round(2000*Math.Log10(.5)),"播放路徑沒換曲線（真機再決定）");
    }
}
