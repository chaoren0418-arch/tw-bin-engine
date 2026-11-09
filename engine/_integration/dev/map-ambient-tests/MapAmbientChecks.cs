using P06;
using P24.HostAudio;
using System.Text.Json;

partial class Checks {
    sealed class AmbientRecorder : IAmbientLoopAudio {
        public readonly List<(MapAmbientVoice Voice, int Volume)> Started = [];
        public readonly List<MapAmbientVoice> Stopped = [];
        public readonly List<(MapAmbientVoice Voice, int Volume)> Volumes = [];
        public bool Accept = true;
        public bool Start(MapAmbientVoice voice, int volume) { if (Accept) Started.Add((voice, volume)); return Accept; }
        public void SetVolume(MapAmbientVoice voice, int volume) => Volumes.Add((voice, volume));
        public void Stop(MapAmbientVoice voice) => Stopped.Add(voice);
        public HashSet<long> Live => Started.Select(s => s.Voice.Number).Except(Stopped.Select(v => v.Number)).ToHashSet();
    }
    static MapEffectAppearance AmbientAppearance(int id, params string[] events) => new(JsonSerializer.SerializeToUtf8Bytes(new {
        id, flags = 0, tickRate = 24,
        sequence = events.Select(e => new { frame = 0, ticks = 12, events = e }).ToArray(),
        frames = new[] { new { index = 0, bounds = new[] { 0, 0, 0, 0 }, blocks = new[] { new { a = 0, b = 0, z = 0, order = 0, pixels = new[] { new[] { 0, 0, 31 } } } } } }
    }));
    static MapAmbientSource Emitter(string key, int x, int y, MapEffectAppearance a, long elapsed) => new(key, x, y, a, elapsed);

    [CheckGroup("map-ambient"),CheckGroup("map-continuity",Order=210),CheckGroup(CheckGroups.Default,Order=440)]
    static void MapAmbientChecks() {
        // 音量：sound.md §2.3 對照表；曼哈頓 > 14 夾成 (14,0)。
        foreach (var (dx, dy, v) in new[] { (0, 0, 100), (1, 0, 100), (5, 0, 83), (-5, 0, 83), (0, 8, 56), (7, 7, 32), (11, 0, 16), (12, 0, 0), (15, 0, 0), (8, 6, 30), (8, 7, 0), (int.MinValue, 0, 0) })
            Check(MapAmbientSound.Volume(dx, dy) == v, $"ambient volume ({dx},{dy})={v}");
        Check(MapAmbientSound.DistanceSquared(3, -4) == 25 && MapAmbientSound.DistanceSquared(int.MinValue, 0) == int.MaxValue, "d² squared tiles, saturated");

        // token：模式取最後一個符號、最多 5 個候選、0 保留給選擇器略過。
        var cue = MapAmbientCue.Parse("^3219"); Check(cue.Mode == 2 && cue.Candidates.SequenceEqual([3219]), "^ single candidate");
        cue = MapAmbientCue.Parse("5<3332<1340<1340"); Check(cue.Mode == 0 && cue.Candidates.SequenceEqual([3332, 1340, 1340]), "< three candidates (6016 shape)");
        cue = MapAmbientCue.Parse("^1^2^3^4^5^6"); Check(cue.Mode == 2 && cue.Candidates.Length == 5 && cue.Candidates[^1] == 5, "sixth candidate dropped");
        cue = MapAmbientCue.Parse("[97"); Check(cue.Mode == 1, "[ is not ambient");
        cue = MapAmbientCue.Parse(""); Check(cue.Mode == -1 && cue.Candidates.Length == 0, "no token");
        cue = MapAmbientCue.Parse("^99999"); Check(cue.Candidates.SequenceEqual([0]), "id beyond word range becomes skipped 0");

        var two = AmbientAppearance(1036, "^100", "");
        Check(MapAmbientSound.Step(two, 0) == 0 && MapAmbientSound.Step(two, 499) == 0 && MapAmbientSound.Step(two, 500) == 1 && MapAmbientSound.Step(two, 1000) == 2 && MapAmbientSound.Step(two, -1) == -1, "24 tick/s absolute frame entry");
        Check(MapAmbientSound.HasAmbient(two) && !MapAmbientSound.HasAmbient(AmbientAppearance(1, "[97", "<3")), "ambient detection");

        // 進幀、循環不重播、冷卻、槽表。
        var audio = new AmbientRecorder(); var model = new MapAmbientSound(audio, new Random(7)); long now = 10000;
        var self = (X: 100, Y: 100);
        model.Update([Emitter("a", 100, 100, two, 0)], self, now, true);
        Check(audio.Started.Count == 1 && audio.Started[0].Voice.SoundId == 100 && audio.Started[0].Volume == 100, "first sighting enters frame and starts loop at distance volume");
        model.Update([Emitter("a", 100, 100, two, 100)], self, now + 100, true);
        model.Update([Emitter("a", 100, 100, two, 1000)], self, now + 1000, true);
        Check(audio.Started.Count == 1 && audio.Stopped.Count == 0, "looping voice is not replayed on later ^ frame");

        var shared = AmbientAppearance(1055, "^200", "");
        model.Update([Emitter("a", 100, 100, two, 1000), Emitter("c1", 101, 100, shared, 0), Emitter("c2", 102, 100, shared, 0)], self, now += 2000, true);
        Check(audio.Started.Count(s => s.Voice.SoundId == 200) == 1, "200ms cooldown is per sound id and global");
        model.Update([Emitter("a", 100, 100, two, 1000), Emitter("c1", 101, 100, shared, 0), Emitter("c2", 102, 100, shared, 1000)], self, now + 199, true);
        Check(audio.Started.Count(s => s.Voice.SoundId == 200) == 1, "cooldown still blocks at +199");
        model.Update([Emitter("a", 100, 100, two, 1000), Emitter("c1", 101, 100, shared, 0), Emitter("c2", 102, 100, shared, 2000)], self, now + 200, true);
        Check(audio.Started.Count(s => s.Voice.SoundId == 200) == 2, "cooldown expires at +200 on next ^ frame");
        model.Update([], self, now += 1000, true);
        Check(audio.Live.Count == 0 && model.Slots.Length == 0 && model.Emitters == 0, "removed objects stop every voice and empty the slot table");

        // 5 槽：由遠到近加入 d=6..2，再加 d=1 換掉最遠者；更遠或等距不換入。
        audio = new(); model = new(audio, new Random(7)); now = 50000;
        var sources = new List<MapAmbientSource>();
        foreach (int d in new[] { 6, 5, 4, 3, 2, 1 }) {
            sources.Add(Emitter("e" + d, 100 + d, 100, two, 0));
            model.Update(sources.ToArray(), self, now += 300, true);
        }
        var slot = model.Slots.Single();
        Check(slot.SoundId == 100 && slot.Voices.Length == 5 && slot.Distances.SequenceEqual([1, 4, 9, 16, 25]), "slot holds five nearest sorted by d²");
        Check(audio.Stopped.Count == 1 && audio.Stopped[0].Emitter == "e6", "closer source evicts farthest (A06E10)");
        sources.Add(Emitter("e5b", 100, 105, two, 0)); model.Update(sources.ToArray(), self, now += 300, true);
        sources.Add(Emitter("e7", 107, 100, two, 0)); model.Update(sources.ToArray(), self, now += 300, true);
        Check(audio.Started.Count == 6 && model.Slots.Single().Voices.All(v => v.Emitter != "e5b" && v.Emitter != "e7"), "full slot rejects equal or farther source");
        sources.RemoveAll(s => s.Key == "e3"); model.Update(sources.ToArray(), self, now += 300, true);
        Check(model.Slots.Single().Distances.SequenceEqual([1, 4, 16, 25]) && audio.Stopped.Last().Emitter == "e3", "removal compacts slot");
        sources.Add(Emitter("e5c", 100, 95, two, 1000)); model.Update(sources.ToArray(), self, now += 300, true);
        Check(model.Slots.Single().Distances.SequenceEqual([1, 4, 16, 25, 25]) && model.Slots.Single().Voices[^1].Emitter == "e5c", "equal distance appended after existing (stable)");

        // 距離音量：走遠只降到 0，不停止；音量 0 的物件不啟動。
        int stops = audio.Stopped.Count;
        var far = (X: 140, Y: 100);
        model.Update(sources.Select(s => s with { Elapsed = s.Elapsed + 500 }).ToArray(), far, now += 300, true);
        Check(audio.Volumes.Count > 0 && audio.Volumes.All(v => v.Volume == 0) && audio.Stopped.Count == stops && audio.Live.Count == 5, "walking away lowers volume to 0 without stopping");
        model.Update(sources.Select(s => s with { Elapsed = s.Elapsed + 1000 }).ToArray(), self, now += 300, true);
        Check(audio.Volumes.Count(v => v.Volume == 100) == 1 && audio.Volumes.Any(v => v.Volume == 83), "returning restores per-source distance volume");
        var lonely = AmbientAppearance(1630, "^300", "");
        model.Update([Emitter("z", 140, 100, lonely, 0)], self, now += 300, true);
        Check(!audio.Started.Any(s => s.Voice.SoundId == 300) && audio.Live.Count == 0, "zero-volume emitter never starts; others removed");

        // 啟動失敗、靜音、沒有自身角色、抽到 0、換圖。
        audio = new() { Accept = false }; model = new(audio, new Random(7)); now = 90000;
        model.Update([Emitter("x", 100, 100, two, 0)], self, now, true);
        Check(model.Slots.Length == 0, "failed start leaves slot table unchanged");
        audio = new(); model = new(audio, new Random(7));
        model.Update([Emitter("x", 100, 100, two, 0)], self, now, false);
        model.Update([Emitter("x", 100, 100, two, 0)], self, now, true);
        Check(audio.Started.Count == 0, "muted start is skipped until next frame entry");
        model.Update([Emitter("x", 100, 100, two, 1000)], self, now + 1000, true);
        Check(audio.Started.Count == 1, "audible again on next ^ frame (mute did not consume cooldown)");
        model.Update([Emitter("x", 100, 100, two, 1000), Emitter("y", 101, 100, AmbientAppearance(1648, "^400"), 0)], null, now + 5000, true);
        Check(audio.Started.Count == 1, "no self actor: no selection");
        model.Update([Emitter("x", 100, 100, AmbientAppearance(1648, "^0"), 0)], self, now + 6000, true);
        Check(audio.Stopped.Count == 1 && audio.Started.Count == 1 && model.Slots.Length == 0, "appearance change stops old voice; candidate 0 is skipped");
        model.Update([Emitter("m", 100, 100, AmbientAppearance(1684, "^500^501^502"), 0)], self, now + 7000, true);
        Check(audio.Started.Count == 2 && audio.Started[^1].Voice.SoundId is 500 or 501 or 502, "multiple candidates choose one");
        model.Clear(); Check(audio.Live.Count == 0 && model.Emitters == 0, "Clear stops all");

        // 跨越多幀只補一個循環。
        audio = new(); model = new(audio, new Random(7));
        var every = AmbientAppearance(1702, "^600", "^601", "^602");
        model.Update([Emitter("s", 100, 100, every, 0)], self, 0, true);
        model.Update([Emitter("s", 100, 100, every, 60000)], self, 60000, true);
        Check(audio.Started.Select(s => s.Voice.SoundId).OrderBy(i => i).SequenceEqual([600, 601, 602]), "frame catch-up bounded to one cycle");

        // 產品掛載：沒有循環後端時不建立狀態；沒有地圖快取時沒有音源。
        using var c = Seed(); var world = c.World;
        MapAmbientSound.Backend = null; MapAmbientSound.Observe(world, 1);
        Check(MapAmbientSound.Of(world) == null, "no loop backend: hook is inert");
        var backend = new AmbientRecorder(); MapAmbientSound.Backend = backend;
        try {
            MapAmbientSound.Observe(world, 1);
            Check(MapAmbientSound.Of(world) is { Emitters: 0 } && MapAmbientSound.Sources(world, 1).Length == 0 && backend.Started.Count == 0, "session without map effects has no ambient sources");
        } finally { MapAmbientSound.Backend = null; }
    }
}
