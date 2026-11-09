using P24.Audio;
using P24.HostAudio;

namespace P06;

// T252（#613）離線測試：串流層（替身 driver、同步命令）、三個產品轉接器、進入世界時安裝、類別音量與播放中距離音量。
// 不開音訊裝置、不讀遊戲素材、不連網路；檔案存在檢查以假清單代替。
static class ProductSoundTests
{
    sealed class FakeDriver : IStreamVoiceDriver
    {
        public readonly Dictionary<string, (string Path, int Db, bool Running)> Voices = [];
        public readonly Dictionary<string, string> NextStatus = [];
        public readonly List<string> Log = [];
        public bool FailOpen;
        public bool Open(string path, string voice) { Log.Add("open " + Path.GetFileName(path)); if (FailOpen) return false; Voices[voice] = (path, 0, false); return true; }
        public bool Volume(string voice, int db) { if (!Voices.TryGetValue(voice, out var v)) return false; Voices[voice] = v with { Db = db }; return true; }
        public bool Run(string voice) { if (!Voices.TryGetValue(voice, out var v)) return false; Voices[voice] = v with { Running = true }; Log.Add("run " + voice); return true; }
        public bool Rewind(string voice) { Log.Add("rewind " + voice); return Voices.ContainsKey(voice); }
        public string Status(string voice) => NextStatus.Remove(voice, out var s) ? s : "playing";
        public void Close(string voice) { Log.Add("close " + voice); Voices.Remove(voice); }
    }
    const string Root = @"X:\client\sound";
    static AudioStreamPlayer Player(FakeDriver d, params string[] files) =>
        AudioStreamPlayer.ForTests(Root, d, p => files.Any(f => string.Equals(Path.Combine(Root, f), p, StringComparison.OrdinalIgnoreCase)));

    [WorldModuleTest("product-sound-stream")]
    static void Stream(WorldModuleTestContext t)
    {
        var d = new FakeDriver(); var p = Player(d, "music7.mp3", "55.wav", "56.mp3", "56.wav");
        long m = p.Start(["music7.mp3"], false, 1);
        t.Check(m != 0 && d.Voices.Count == 1 && d.Voices.Values.Single().Running && d.Voices.Values.Single().Db == 0, "啟動：開檔、全音量 0 dB、Run");
        t.Equal(0L, p.Start(["music8.mp3"], false, 1), "缺檔回 0，不開檔");
        t.Equal(0L, p.Start([@"..\secret.mp3", @"C:\x.wav", "a/b.mp3"], true, 1), "不接受路徑或跳出 sound 目錄");
        long w = p.Start(ProductEnvSoundAudio.Files(55), true, .5);
        t.Check(w != 0 && d.Voices.Values.Any(v => v.Path.EndsWith("55.wav", StringComparison.Ordinal)), "mp3 缺檔改 wav（sound.md 表 8）");
        long both = p.Start(ProductEnvSoundAudio.Files(56), true, .5);
        t.Check(d.Voices.Values.Any(v => v.Path.EndsWith("56.mp3", StringComparison.Ordinal)) && !d.Voices.Values.Any(v => v.Path.EndsWith("56.wav", StringComparison.Ordinal)), "mp3 與 wav 都在時先用 mp3");
        t.Equal(-602, d.Voices["p24s" + w].Db, "0.5 → −6.02 dB（AudioGain）");
        p.SetVolume(w, 0); t.Equal(-10000, d.Voices["p24s" + w].Db, "SetVolume 0 → −100 dB，不停止");
        p.SetVolume(w, 1); t.Equal(0, d.Voices["p24s" + w].Db, "播放中改音量");
        d.NextStatus["p24s" + w] = "ended"; p.Poll();
        t.Check(!p.Finished(w) && d.Log.Contains("rewind p24s" + w) && p.Stats.Looped == 1, "循環音播完：倒帶重播，不結束");
        d.NextStatus["p24s" + m] = "ended"; p.Poll();
        t.Check(p.Finished(m) && !d.Voices.ContainsKey("p24s" + m) && p.Stats.Ended == 1, "單次音樂播完：Finished、關閉 graph");
        p.Stop(both); t.Check(p.Finished(both) && !d.Voices.ContainsKey("p24s" + both), "Stop 立即關閉");
        d.NextStatus["p24s" + w] = "failed"; p.Poll(); t.Check(p.Finished(w) && p.Stats.Failed == 1, "播放錯誤：釋放並記錄");
        d.FailOpen = true; long bad = p.Start(["music7.mp3"], false, 1);
        t.Check(bad != 0 && p.Finished(bad) && p.Stats.Failed == 2, "開檔失敗：handle 立即視為結束");
        d.FailOpen = false;
        var small = AudioStreamPlayer.ForTests(Root, new FakeDriver(), _ => true, capacity: 2);
        small.Start(["1.wav"], true, 1); small.Start(["2.wav"], true, 1);
        t.Equal(0L, small.Start(["3.wav"], true, 1), "容量滿：拒播，不搶占");
        p.Start(["music7.mp3"], true, 1); p.Dispose();
        t.Check(d.Voices.Count == 0 && p.Live == 0 && p.Start(["music7.mp3"], false, 1) == 0, "Dispose 關閉全部、之後不再啟動");
    }

    sealed class FakeStream : IAudioStream
    {
        public readonly Dictionary<long, (string[] Files, bool Loop, double Volume)> Live = [];
        public readonly HashSet<string> Missing = [];
        public readonly HashSet<long> Done = [];
        long next;
        public long Last => next;
        public (string[] Files, bool Loop, double Volume) Newest => Live[next];
        public long Start(IReadOnlyList<string> candidates, bool loop, double volume)
        { if (candidates.All(Missing.Contains)) return 0; Live[++next] = (candidates.ToArray(), loop, volume); return next; }
        public void SetVolume(long handle, double volume) { if (Live.TryGetValue(handle, out var v)) Live[handle] = v with { Volume = volume }; }
        public void Stop(long handle) => Live.Remove(handle);
        public bool Finished(long handle) => !Live.ContainsKey(handle) || Done.Contains(handle);
    }

    [WorldModuleTest("product-sound-adapters")]
    static void Adapters(WorldModuleTestContext t)
    {
        var s = new FakeStream();
        var music = new ProductMusicAudio(s);
        var track = new MusicTrack(1, 12, false, MusicKind.Region);
        t.Check(music.Start(track, .25) && s.Live[1].Files.SequenceEqual(["music12.mp3"]) && !s.Live[1].Loop && s.Live[1].Volume == .25, "音樂：sound\\musicN.mp3、單次、音量照傳");
        music.SetVolume(track, .1); t.Equal(.1, s.Live[1].Volume, "音樂淡出改音量");
        t.Check(!music.Finished(track), "音樂播放中");
        s.Done.Add(1); t.Check(music.Finished(track), "音樂自然播完");
        music.Stop(track); t.Check(s.Live.Count == 0 && music.Live == 0, "音樂停止");
        s.Missing.Add("music13.mp3"); t.Check(!music.Start(new(2, 13, true, MusicKind.CharacterSelect), 1), "缺檔：Start 回 false（BackgroundMusic 每秒重試）");

        var env = new ProductEnvSoundAudio(s);
        var main = new EnvSoundVoice(1, 300, true);
        t.Check(env.Start(main, 0) && s.Newest.Files.SequenceEqual(["300.mp3", "300.wav"]) && s.Newest.Loop, "環境主音：循環、先 mp3 再 wav");
        long mainHandle = s.Last;
        env.SetVolume(main, .4); t.Equal(.4, s.Live[mainHandle].Volume, "環境主音淡入");
        var one = new EnvSoundVoice(2, 301, false);
        t.Check(env.Start(one, .3) && !s.Newest.Loop, "ONE_SOUND 單次");
        s.Done.Add(s.Last); env.Start(new(3, 302, false), .3);
        t.Equal(2, env.Live, "播完的單次音效在下次 Start 時清掉");
        env.Stop(main); t.Check(!s.Live.ContainsKey(mainHandle), "環境主音停止");

        var amb = new ProductAmbientLoopAudio(s);
        var voice = new MapAmbientVoice(1, 77, "e");
        amb.SetScale(.5);
        t.Check(amb.Start(voice, 80) && s.Newest.Loop && Math.Abs(s.Newest.Volume - .4) < 1e-12, "^：循環、80/100 × 類別 0.5 = 0.4");
        long ah = s.Last;
        amb.SetVolume(voice, 20); t.Check(Math.Abs(s.Live[ah].Volume - .1) < 1e-12, "^ 播放中隨距離改音量（A091C0）");
        amb.SetScale(1); t.Check(Math.Abs(s.Live[ah].Volume - .2) < 1e-12, "類別／主音量變動立即套到在播的循環");
        amb.SetScale(0); t.Check(s.Live.ContainsKey(ah) && s.Live[ah].Volume == 0, "靜音：降到 0，不停止");
        amb.Stop(voice); t.Check(!s.Live.ContainsKey(ah) && amb.Live == 0, "^ 停止");
        t.Equal(0d, ProductAmbientLoopAudio.Final(150, double.NaN), "非法值夾住");
    }

    [WorldModuleTest("product-sound-install")]
    static void Install(WorldModuleTestContext t)
    {
        var before = ClientSoundScene.Volumes[SoundCategory.EnvSound];
        try
        {
            ProductSound.Disarm();
            var w = new WorldSession();
            ClientSoundScene.Observe(w, 0);
            t.Check(ProductSound.Stream == null && MapAmbientSound.Backend == null && ClientSoundScene.Music == null, "未 Arm：no-op（測試、--module-tests 不開裝置）");
            t.Equal(0d, MapAmbientSound.Scale(w), "沒有 6016 host（主音量）時 ^ 類別音量為 0");
            var s = new FakeStream(); int loads = 0;
            var defs = new SoundDefinitions(null, null, "合成");
            ProductSound.ArmForTests(_ => s, _ => { loads++; return defs; });
            ProductSound.Pump(false);
            t.Check(ProductSound.Stream == null, "Arm 後尚未進入世界：不建立");
            ProductSound.Pump(true);
            t.Check(ProductSound.Stream == s && ReferenceEquals(MapAmbientSound.Backend, ProductSound.Ambient), "進入世界：建立串流並設定 MapAmbientSound.Backend");
            for (int i = 0; i < 200 && loads == 0; i++) Thread.Sleep(5);
            Thread.Sleep(20); ProductSound.Pump(true);
            t.Check(loads == 1 && ProductSound.Status.StartsWith("已接上", StringComparison.Ordinal), "定義檔讀完後 Configure", ProductSound.Status);
            ProductSound.Pump(true); t.Equal(1, loads, "只安裝一次");
            ClientSoundScene.Volumes[SoundCategory.EnvSound] = .5;
            t.Equal(.5, ClientSoundScene.Volumes[SoundCategory.EnvSound], "共用類別音量");
        }
        finally { ProductSound.Disarm(); ClientSoundScene.Volumes[SoundCategory.EnvSound] = before; }
        t.Check(MapAmbientSound.Backend == null && ProductSound.Stream == null, "Disarm 還原");
    }
}
