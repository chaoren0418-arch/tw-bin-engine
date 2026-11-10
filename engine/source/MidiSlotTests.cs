using P24.Audio;
using P24.HostAudio;
namespace P06;

// T396（#911，gap-matrix 15.12.6 R10-L）離線測試：MIDI 模式下非系統槽換曲「0.mid 沒在播才開」，不重頭播。
// 依據：docs/reports/issue-737.md（8F1C30／8F2FB0／8F3B90／8F3D70：參數不在 0..8 且 MIDI 沒在播 → 8F63D0(0)；
// 參數在 0..8 且 MIDI 在播 → 8F5510）、docs/reports/issue-814.md 第 49、61 行（引擎差異）。
// 由 --module-tests 執行（名稱 midi-slot）。只用合成 music.def 文字、記錄器與替身驅動；不讀原客戶端、不開音訊裝置。
static class MidiSlotTests {
    const string Name="midi-slot";

    sealed class Stream:IAudioStream {
        public readonly List<(string[] Files,bool Loop,double Volume)> Started=[];
        public readonly List<(long Handle,double Volume)> Volumes=[];
        public readonly List<long> Stopped=[];
        public readonly HashSet<long> Done=[];
        public bool Midi;
        long next;
        public long Start(IReadOnlyList<string> candidates,bool loop,double volume){Started.Add((candidates.ToArray(),loop,volume));return ++next;}
        public void SetVolume(long handle,double volume)=>Volumes.Add((handle,volume));
        public void Stop(long handle){Stopped.Add(handle);Done.Add(handle);}
        public bool Finished(long handle)=>Done.Contains(handle);
        public bool MidiFallback=>Midi;
    }
    sealed class Driver(string tag,List<string> log):IStreamVoiceDriver {
        public bool Open(string path,string voice){log.Add($"{tag} open {Path.GetFileName(path)}");return true;}
        public bool Volume(string voice,int db){log.Add($"{tag} volume {db}");return true;}
        public bool Run(string voice){log.Add($"{tag} run");return true;}
        public bool Rewind(string voice){log.Add($"{tag} rewind");return true;}
        public string Status(string voice)=>"playing";
        public void Close(string voice)=>log.Add($"{tag} close");
    }
    // 只實作必要成員的後端：Replace 走介面預設（先 Stop 再 Start）。
    sealed class Recorder:IMusicStreamAudio {
        public readonly List<string> Calls=[];
        public bool Start(MusicTrack track,double volume){Calls.Add($"start {track.Music}");return true;}
        public void SetVolume(MusicTrack track,double volume){}
        public void Stop(MusicTrack track)=>Calls.Add($"stop {track.Music}");
        public bool Finished(MusicTrack track)=>false;
    }
    // 合成定義（自編數值，不是原檔內容）：槽 0→82、槽 8→5；槽 9／10 用預設 i→i；地圖 4、5 各一首。
    const string Def=
        "System Setting Begin\r\n\tA=82;\r\n\tB=1;\r\n\tC=2;\r\n\tD=3;\r\n\tE=4;\r\n\tF=5;\r\n\tG=6;\r\n\tH=7;\r\n\tI=5;\r\nSystemMenu Exit\r\n\r\n"+
        ";FIELD\r\nLOC 4\r\nMUSIC 30\r\n\r\n;TOWN\r\nLOC 5\r\nMUSIC 31\r\n";
    static MusicContext World(int map,bool enabled=true)=>new(MusicScene.World,map,32700,32800,enabled,1);

    [WorldModuleTest(Name)]
    static void Run(WorldModuleTestContext t){Adapter(t);KeptFinished(t);NormalMode(t);DefaultReplace(t);EndToEnd(t);}

    // ProductMusicAudio.Replace（MIDI 模式）：非系統槽沿用同一個 handle；系統槽關掉且不開；沒有在播的才開。
    static void Adapter(WorldModuleTestContext t){
        var s=new Stream{Midi=true};var m=new ProductMusicAudio(s);
        MusicTrack region=new(1,30,false,MusicKind.Region),battle=new(2,9,false,MusicKind.Battle,9),back=new(3,30,false,MusicKind.Region);
        t.Check(m.Replace(null,region,1)&&s.Started.Count==1,"沒有在播：開 0.mid（8F63D0(0)）",s.Started);
        m.SetVolume(region,.3);
        t.Check(m.Replace(region,battle,.8)&&s.Started.Count==1&&s.Stopped.Count==0&&m.MidiKept==1,"區域 → 戰鬥（槽 9）：不關不開",new{s.Started,s.Stopped});
        t.Check(s.Volumes[^1]==(1L,.8),"沿用時把音量改成新曲的音量（淡出後回到全音量）",s.Volumes);
        t.Check(!m.Finished(battle)&&m.Finished(region)&&m.Live==1,"handle 改記到新曲目；舊曲目視為已結束");
        m.SetVolume(battle,.5);t.Check(s.Volumes[^1]==(1L,.5),"新曲目改音量下到同一個 handle",s.Volumes);
        t.Check(m.Replace(battle,back,1)&&s.Started.Count==1&&m.MidiKept==2,"戰鬥 → 區域：仍沿用");
        var login=new MusicTrack(4,82,false,MusicKind.Login,0);
        t.Check(!m.Replace(back,login,1)&&s.Stopped.SequenceEqual([1L])&&s.Started.Count==1&&m.MidiSilenced==1&&m.Live==0,"換到槽 0：關掉 0.mid（8F5510）、不開",new{s.Stopped,s.Started});
        t.Check(m.Replace(null,new(5,30,false,MusicKind.Region),1)&&s.Started.Count==2&&m.MidiKept==2,"之後再回區域：0.mid 沒在播，重開");
        m.Stop(new(5,30,false,MusicKind.Region));
        t.Check(s.Stopped.SequenceEqual([1L,2L])&&m.Live==0,"Stop（關音樂／Clear）照舊真的關掉",s.Stopped);
    }

    // 沿用前先確認 0.mid 還在播：串流層已結束（例如 MCI 通知失敗而關閉）就照 Start 重開。
    static void KeptFinished(WorldModuleTestContext t){
        var s=new Stream{Midi=true};var m=new ProductMusicAudio(s);
        MusicTrack a=new(1,30,false,MusicKind.Region),b=new(2,10,false,MusicKind.Battle,10);
        m.Replace(null,a,1);s.Done.Add(1);
        t.Check(m.Replace(a,b,1)&&s.Started.Count==2&&m.MidiKept==0&&!m.Finished(b),"已結束的 0.mid：重開，不算沿用",s.Started);
    }

    // 一般模式（有 music*.mp3）：照舊先停舊的再開新的。
    static void NormalMode(WorldModuleTestContext t){
        var s=new Stream();var m=new ProductMusicAudio(s);
        MusicTrack a=new(1,30,false,MusicKind.Region),b=new(2,9,false,MusicKind.Battle,9);
        m.Replace(null,a,1);
        t.Check(m.Replace(a,b,1)&&s.Stopped.SequenceEqual([1L])&&s.Started.Count==2&&s.Started[1].Files.SequenceEqual(["music9.mp3"])&&m.MidiKept==0,"一般模式：停 music30、開 music9",new{s.Stopped,s.Started});
        t.Check(m.Replace(b,new(3,82,false,MusicKind.Login,0),1)&&s.Started.Count==3&&m.MidiSilenced==0,"一般模式：槽 0 照播");
    }

    // 介面預設 Replace＝先 Stop 再 Start，其他後端（記錄器、DemoChecks 的 MusicRecorder）行為不變。
    static void DefaultReplace(WorldModuleTestContext t){
        var rec=new Recorder();var bgm=new BackgroundMusic(MusicDefinition.Parse(Def),rec,new Random(1));
        bgm.Update(World(4),0);bgm.Attack(false,100);
        t.Check(rec.Calls.SequenceEqual(["start 30","stop 30","start 9"]),"預設：停舊開新，順序同原本 StopCurrent＋Start",rec.Calls);
    }

    // 端到端：BackgroundMusic → ProductMusicAudio → AudioStreamPlayer（MIDI 模式，替身 MCI）。
    static void EndToEnd(WorldModuleTestContext t){
        var log=new List<string>();var ds=new Driver("ds",log);var mci=new Driver("mci",log);
        var player=AudioStreamPlayer.ForTests(@"X:\client\sound",new RoutingStreamDriver(ds,mci),f=>f.EndsWith("0.mid",StringComparison.OrdinalIgnoreCase),midiFallback:true);
        var music=new ProductMusicAudio(player);var m=new BackgroundMusic(MusicDefinition.Parse(Def),music,new Random(1));
        int Opens()=>log.Count(l=>l=="mci open 0.mid");int Closes()=>log.Count(l=>l=="mci close");
        m.Update(World(4),0);
        t.Check(Opens()==1&&player.Live==1&&m.Current is {Music:30},"世界：開 0.mid",string.Join(",",log));
        m.Attack(false,100);
        t.Check(Opens()==1&&Closes()==0&&m.Current is {Kind:MusicKind.Battle,Music:9},"打怪（槽 9）：不重頭播",string.Join(",",log));
        m.Attack(true,200);
        t.Check(Opens()==1&&Closes()==0&&m.Current is {Music:10},"打玩家（槽 10）：不重頭播");
        // 15 秒沒戰鬥 → 淡出（0.8 秒）→ 回區域曲：仍是同一條 0.mid。
        for(long now=300;now<=17000;now+=100)m.Update(World(4),now);
        t.Check(Opens()==1&&Closes()==0&&m.Current is {Kind:MusicKind.Region,Music:30}&&!m.Fading,"戰鬥結束淡回區域：不重頭播",string.Join(",",log.TakeLast(6)));
        t.Check(log.Where(l=>l.StartsWith("mci volume",StringComparison.Ordinal)).Last()=="mci volume 0","淡出後回到全音量（0 dB）",string.Join(",",log.TakeLast(6)));
        // 換到另一筆區域記錄（候選不同）→ 淡出切換：仍是同一條 0.mid。
        for(long now=17100;now<=18500;now+=100)m.Update(World(5),now);
        t.Check(Opens()==1&&Closes()==0&&m.Current is {Music:31}&&player.Live==1,"換區域：不重頭播",string.Join(",",log.TakeLast(6)));
        t.Check(music.MidiKept==4,"沿用 4 次（槽 9、槽 10、回區域、換區域）",music.MidiKept);
        // 系統槽 0：關掉；回世界：0.mid 沒在播 → 重開。
        m.Update(new(MusicScene.Login,0,0,0,true,1),18600);
        t.Check(Closes()==1&&Opens()==1&&player.Live==0&&m.Current==null,"登入（槽 0）：關掉、不開",string.Join(",",log.TakeLast(4)));
        m.Update(World(4),18700);
        t.Check(Opens()==2&&player.Live==1,"回世界：重開 0.mid");
        // 關音樂（A06A10）：真的停掉。
        m.Update(World(4,enabled:false),18800);
        t.Check(Closes()==2&&player.Live==0&&m.Current==null,"關音樂：停 0.mid",string.Join(",",log.TakeLast(4)));
        player.Dispose();
    }
}
