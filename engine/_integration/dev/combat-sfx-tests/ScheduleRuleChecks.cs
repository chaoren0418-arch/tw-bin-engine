using P06;
using System.Text.Json;

// T465（#1050）：音效排程規則的合成測試，取代 git 追蹤的 expected-events.json（真實動作／時序資料）。
// 外觀 metadata 全部是這裡編出來的數字（gfx 9001、聲音 id 個位數），只在執行時寫到輸出目錄旁邊再讀回；
// 不讀遊戲素材、不讀 WAV、不開音訊裝置，CI 的 --selection 模式就會跑。
static partial class SelectionChecks
{
    const int SyntheticGfx = 9001;

    // 每個動作 8 個方向都用同一段序列（ObjectAppearance 要求每個動作 8 方向）。
    static readonly (string Action, (int Ticks, int? Ms, string Events)[] Steps)[] SyntheticActions =
    [
        // ticks 比例分配：actionMs.attack = 600、總 ticks 16。第 0 格 ticks 0（不進排程，`<11` 永遠不播）。
        ("attack", [(0, null, "<11"), (2, null, "[21[22"), (4, null, ""), (6, null, "^31"), (4, null, "<0<41!")]),
        // 全部有 milliseconds：直接用毫秒；ms 0 的格不進排程；第 3 格 6 個候選只存前 5 個。
        ("damage", [(1, 50, "<5"), (1, 0, "<6"), (2, 100, "<1<2<3<4<5<6")]),
        // pure／flight 用：第 0 格移到最後。無 actionMs、無 milliseconds → 長度 = 3 ticks × 1000/24 = 125。
        ("fly", [(1, null, "<7"), (1, null, "<8"), (1, null, "")]),
        // breath 是循環動作。
        ("breath", [(3, null, "<9"), (3, null, "")]),
    ];

    static string WriteSyntheticAppearance(string directory)
    {
        var frames = new List<object>();
        var sequences = new List<object>();
        foreach (var (action, steps) in SyntheticActions)
        {
            for (int i = 0; i < steps.Length; i++)
                frames.Add(new { key = $"{action}-{i}", bounds = new[] { 0, 0, 1, 1 }, blocks = Array.Empty<object>() });
            for (int direction = 0; direction < 8; direction++)
                sequences.Add(new
                {
                    action, direction,
                    sequence = steps.Select((s, i) => s.Ms is int ms
                        ? (object)new { key = $"{action}-{i}", ticks = s.Ticks, milliseconds = ms, events = s.Events }
                        : new { key = $"{action}-{i}", ticks = s.Ticks, events = s.Events }).ToArray()
                });
        }
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "synthetic-appearance.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            gfx = SyntheticGfx, sprite = 0, stepMs = 400,
            actionMs = new Dictionary<string, double> { ["attack"] = 600 },
            frames, sequences
        }));
        return path;
    }

    static bool Same(SoundSchedule schedule, (int Index, double At, int[] Candidates)[] expected) =>
        schedule.Frames.Length == expected.Length &&
        schedule.Frames.Zip(expected).All(p => p.First.Index == p.Second.Index && Math.Abs(p.First.At - p.Second.At) < 1e-6 &&
                                               p.First.Candidates.SequenceEqual(p.Second.Candidates));

    static void ScheduleCases(string output, Action<string, Action> Case, Action<bool, string> Check)
    {
        var appearance = new ObjectAppearance(WriteSyntheticAppearance(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "synthetic")));
        using var raw = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "synthetic", "synthetic-appearance.json")));

        Case("synthetic-schedule-ticks-proportional-and-zero-length", () =>
        {
            var expected = new (int, double, int[])[] { (1, 0, [21, 22]), (2, 75, []), (3, 225, []), (4, 450, [0, 41]) };
            for (int direction = 0; direction < 8; direction++)
            {
                var schedule = SoundSchedule.Create(appearance, "attack", direction);
                Check(Same(schedule, expected), $"attack/{direction} ticks share actionMs, zero-tick frame skipped, ^ frame silent");
                Check(schedule.Duration == 600 && !schedule.Loop, "attack duration equals actionMs, not looping");
                foreach (var frame in schedule.Frames)
                    Check(appearance.At("attack", direction, frame.At + .001).Key == appearance.Sequences[("attack", direction)][frame.Index].Key,
                        "schedule frame entry agrees with renderer frame");
            }
        });

        Case("synthetic-schedule-milliseconds-and-candidate-limit", () =>
        {
            var schedule = SoundSchedule.Create(appearance, "damage", 0);
            Check(Same(schedule, [(0, 0, [5]), (2, 50, [1, 2, 3, 4, 5])]), "milliseconds used directly, 0ms frame skipped, 6th candidate dropped");
            Check(schedule.Duration == 150, "milliseconds duration");
        });

        Case("synthetic-schedule-pure-flight-and-loop", () =>
        {
            double third = 125d / 3;
            var pure = SoundSchedule.Create(appearance, "fly", 0, pure: true);
            Check(Same(pure, [(1, 0, [8]), (2, third, []), (0, 2 * third, [7])]) && !pure.Loop, "pure effect starts at index1, index0 moves last");
            var flight = SoundSchedule.Create(appearance, "fly", 0, flight: true);
            Check(Same(flight, [(1, 0, [8]), (2, 60, []), (0, 120, [7])]) && flight.Loop && flight.Duration == 180, "flight 60ms steps and loops");
            var played = new List<(int Id, int Frame, long Cycle)>();
            var cursor = new SoundCursor(flight, new ScriptedRandom());
            cursor.Advance(0, (id, f, c) => played.Add((id, f, c)));
            cursor.Advance(59, (id, f, c) => played.Add((id, f, c)));
            Check(played.SequenceEqual(new[] { (8, 1, 0L) }), "flight index1 at entry only");
            cursor.Advance(180, (id, f, c) => played.Add((id, f, c)));
            Check(played.SequenceEqual(new[] { (8, 1, 0L), (7, 0, 0L), (8, 1, 1L) }), "flight loop next cycle");
            var breath = SoundSchedule.Create(appearance, "breath", 0);
            Check(breath.Loop && Same(breath, [(0, 0, [9]), (1, 125, [])]) && breath.Duration == 250, "breath loops by action name");
        });

        Case("synthetic-schedule-slow-and-haste-cadence", () =>
        {
            // 每格毫秒 = ticks × 1000 / 24（整數）：0、83、166、250、166。
            var normal = new ActionCadence(SyntheticGfx, 0, 1, 600, 665, 600, null, 0, 0, false);
            var slow = normal with { Slow = true };
            var haste = normal with { Haste = true, HasteAnimationMs = 497 };
            var slowSchedule = SoundSchedule.Create(appearance, "attack", 0, slow);
            Check(Same(slowSchedule, [(1, 0, [21, 22]), (2, 166, []), (3, 498, []), (4, 998, [0, 41])]) && slowSchedule.Duration == 1330, "slow doubles integer frame ms");
            var hasteSchedule = SoundSchedule.Create(appearance, "attack", 0, haste);
            Check(Same(hasteSchedule, [(1, 0, [21, 22]), (2, 62, []), (3, 186, []), (4, 373, [0, 41])]) && hasteSchedule.Duration == 497, "haste (3*ms)>>2 per frame");
            foreach (var (timing, schedule) in new[] { (slow, slowSchedule), (haste, hasteSchedule) })
                foreach (var frame in schedule.Frames)
                    Check(timing.Frame(appearance, 0, frame.At + .001).Key == appearance.Sequences[("attack", 0)][frame.Index].Key, "cadence frame boundary agrees with renderer");
        });

        Case("synthetic-cursor-entry-once-and-zero-skip", () =>
        {
            var schedule = SoundSchedule.Create(appearance, "attack", 0);
            var random = new ScriptedRandom(1, 0);
            var played = new List<(int Id, int Frame)>();
            var cursor = new SoundCursor(schedule, random);
            foreach (var frame in schedule.Frames)
            {
                int before = played.Count;
                cursor.Advance(frame.At - .001, (id, i, _) => played.Add((id, i)));
                Check(played.Count == before, "no event before frame entry");
                cursor.Advance(frame.At, (id, i, _) => played.Add((id, i)));
                cursor.Advance(frame.At, (id, i, _) => played.Add((id, i)));
            }
            cursor.Advance(10_000, (id, i, _) => played.Add((id, i)));
            Check(played.SequenceEqual(new[] { (22, 1) }), "rand%2 pick once; ^ silent; picked zero skipped without reroll; no replay after end");
            Check(random.Calls.SequenceEqual(new[] { 2, 2 }), "rand called once per multi-candidate frame");
        });

        Case("local-oracle-agrees-with-rules-on-synthetic-data", () =>
        {
            // 選用的本機真資料對照（Program.cs）靠 LocalScheduleOracle；先在合成資料上確認它和規則一致。
            foreach (var suffix in new[] { "<1513", "[248[703[701!", "<206!<478<479", "^5", "[5^6", "^5<6", "<1<2<3<4<5<6<7",
                                           "<1<2<3<4<5^6", "<0<9", "]12>{}!", "", "<99999", "<0001", "<x<3" })
                Check(LocalScheduleOracle.Tokens(suffix).SequenceEqual(SoundSchedule.Candidates(suffix)), "oracle tokens " + suffix);
            foreach (var (action, _) in SyntheticActions)
                for (int direction = 0; direction < 8; direction++)
                {
                    var expected = LocalScheduleOracle.Expected(raw.RootElement, action, direction).Select(f => (f.Index, f.At, f.Candidates)).ToArray();
                    Check(Same(SoundSchedule.Create(appearance, action, direction), expected), $"oracle schedule {action}/{direction}");
                    Check(LocalScheduleOracle.RawEvents(raw.RootElement, action, direction)
                        .SequenceEqual(appearance.Sequences[(action, direction)].Select(s => s.Events)), "oracle raw events");
                }
        });
    }
}
