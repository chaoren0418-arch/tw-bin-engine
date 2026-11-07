using P06;
using System.Text.Json;

// #433（T165）：B07 音效選擇規則的離線檢查。不讀遊戲素材、不讀 WAV、不開音訊裝置。
// 依據 docs/reference/rea-inventory.md「B07：音效選擇」：`<`＝模式 0、`[`＝模式 1 都走 A060F0 單次；
// `^`＝模式 2 屬 T166 不播放；1 個候選取第 0 項；2～5 個 rand() % n；選中 0 略過不重抽；同 sample 200ms 冷卻。
static class SelectionChecks
{
    sealed class ScriptedRandom(params int[] values) : Random
    {
        readonly Queue<int> queue = new(values);
        public readonly List<int> Calls = [];
        public override int Next(int maxValue)
        {
            Calls.Add(maxValue);
            int v = queue.Count > 0 ? queue.Dequeue() : 0;
            if (v < 0 || v >= maxValue) throw new InvalidOperationException($"scripted {v} outside 0..{maxValue - 1}");
            return v;
        }
        public override int Next() => throw new InvalidOperationException("unexpected Next()");
        public override int Next(int minValue, int maxValue) => throw new InvalidOperationException("unexpected Next(min,max)");
        public override double NextDouble() => throw new InvalidOperationException("unexpected NextDouble");
    }

    public static void Run(string output)
    {
        int checks = 0;
        var cases = new List<object>();
        void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
        void Case(string name, Action run) { int before = checks; run(); cases.Add(new { name, checks = checks - before, status = "pass" }); }

        Case("parser-modes-and-candidate-limit", () =>
        {
            Check(SoundSchedule.Candidates("<1513").SequenceEqual(new[] { 1513 }), "single < is mode0 sound");
            Check(SoundSchedule.Candidates("[248").SequenceEqual(new[] { 248 }), "single [ is mode1 sound");
            Check(SoundSchedule.Candidates("<206!<478<479").SequenceEqual(new[] { 206, 478, 479 }), "< tokens keep source order, ! is a flag");
            Check(SoundSchedule.Candidates("[248[703[701!").SequenceEqual(new[] { 248, 703, 701 }), "[ tokens keep source order");
            Check(SoundSchedule.Candidates("<3332<1340<1340").SequenceEqual(new[] { 3332, 1340, 1340 }), "duplicate candidates kept (TW13 #6016)");
            Check(SoundSchedule.Candidates("<11[22").SequenceEqual(new[] { 11, 22 }), "mixed < [ both A060F0");
            Check(SoundSchedule.Candidates("^5").Length == 0, "^ mode2 excluded (T166)");
            Check(SoundSchedule.Candidates("[5^6").Length == 0, "last token ^ makes frame mode2");
            Check(SoundSchedule.Candidates("^5<6").SequenceEqual(new[] { 5, 6 }), "last token < makes frame mode0");
            Check(SoundSchedule.Candidates("<1<2<3<4<5<6<7").SequenceEqual(new[] { 1, 2, 3, 4, 5 }), "6th candidate onward dropped");
            Check(SoundSchedule.Candidates("<1<2<3<4<5^6").Length == 0, "dropped 6th token still sets mode");
            Check(SoundSchedule.Candidates("<0").SequenceEqual(new[] { 0 }), "zero kept for skip-without-reroll");
            Check(SoundSchedule.Candidates("<0<9").SequenceEqual(new[] { 0, 9 }), "zero kept among candidates");
            Check(SoundSchedule.Candidates("]12>{}!").Length == 0, "] > { } ! are not sound tokens");
            Check(SoundSchedule.Candidates("").Length == 0 && SoundSchedule.Candidates(null!).Length == 0, "empty suffix");
            Check(SoundSchedule.Candidates("<99999").SequenceEqual(new[] { 0 }), "value beyond word stored as zero (skipped)");
        });

        Case("selection-rand-mod-n-and-zero-skip", () =>
        {
            var r = new ScriptedRandom();
            Check(SoundSchedule.Select([], r) == null && r.Calls.Count == 0, "no candidate skips without rand");
            Check(SoundSchedule.Select([1513], r) == 1513 && r.Calls.Count == 0, "one candidate takes index0 without rand");
            Check(SoundSchedule.Select([0], r) == null && r.Calls.Count == 0, "single zero skipped");
            Check(SoundSchedule.Select([1, 2, 3, 4, 5, 6], r) == null && r.Calls.Count == 0, ">5 candidates skipped");
            for (int n = 2; n <= 5; n++)
                for (int pick = 0; pick < n; pick++)
                {
                    var s = new ScriptedRandom(pick);
                    int[] c = Enumerable.Range(1, n).Select(i => i * 100).ToArray();
                    Check(SoundSchedule.Select(c, s) == c[pick] && s.Calls.SequenceEqual(new[] { n }), $"rand%{n} pick {pick}");
                }
            var zero = new ScriptedRandom(0, 1);
            Check(SoundSchedule.Select([0, 9], zero) == null && zero.Calls.Count == 1, "selected zero skipped, no reroll");
            var nonzero = new ScriptedRandom(1);
            Check(SoundSchedule.Select([0, 9], nonzero) == 9, "non-zero pick among zero");
        });

        Case("cursor-dispatches-angle-and-bracket-once-per-entry", () =>
        {
            var schedule = new SoundSchedule([
                new(0, 0, SoundSchedule.Candidates("<1513")),
                new(1, 100, SoundSchedule.Candidates("[248[703")),
                new(2, 200, SoundSchedule.Candidates("<0")),
                new(3, 300, SoundSchedule.Candidates("^97")),
                new(4, 400, SoundSchedule.Candidates("<0<1517"))], 500, false);
            var random = new ScriptedRandom(1, 0);
            var played = new List<(int Id, int Frame)>();
            var cursor = new SoundCursor(schedule, random);
            cursor.Advance(0, (id, f, _) => played.Add((id, f)));
            Check(played.SequenceEqual(new[] { (1513, 0) }) && random.Calls.Count == 0, "< footstep frame plays id at entry");
            cursor.Advance(0, (id, f, _) => played.Add((id, f)));
            Check(played.Count == 1, "redraw does not repeat");
            cursor.Advance(100, (id, f, _) => played.Add((id, f)));
            Check(played[^1] == (703, 1) && random.Calls.SequenceEqual(new[] { 2 }), "[ pair uses rand%2");
            cursor.Advance(300, (id, f, _) => played.Add((id, f)));
            Check(played.Count == 2, "<0 and ^ frames silent");
            cursor.Advance(400, (id, f, _) => played.Add((id, f)));
            Check(played.Count == 2 && random.Calls.SequenceEqual(new[] { 2, 2 }), "picked zero skipped without reroll");
        });

        Case("controller-200ms-same-sample-cooldown", () =>
        {
            var fake = new FakeAudio(); var observed = new List<CombatSoundObservation>();
            var controller = new CombatSfxController(fake, new Random(1), observed.Add);
            controller.SetControls(1, false, true);
            CombatSoundInstance Make(string key, long start, int id) =>
                new(key, 1, 1, 0, 0, start, start + 1000, new([new(0, 0, [id])], 1000, false));
            var a = Make("a", 100, 1513); var b = Make("b", 100, 1513); var c = Make("c", 100, 248);
            controller.Pump(1, [a, b, c], 100, true);
            Check(fake.Plays.OrderBy(x => x).SequenceEqual(new[] { 248, 1513 }), "same sample same tick plays once; other sample unaffected");
            Check(observed.Count(o => o.Reply.Status == "Cooldown" && o.SoundId == 1513) == 1, "cooldown observable");
            var d = Make("d", 299, 1513);
            controller.Pump(1, [a, b, c, d], 299, true);
            Check(fake.Plays.Count(x => x == 1513) == 1, "199ms later still cooling");
            var e = Make("e", 300, 1513);
            controller.Pump(1, [a, b, c, d, e], 300, true);
            Check(fake.Plays.Count(x => x == 1513) == 2, "200ms later plays again");
            controller.Pump(1, [a, b, c, d, e], 600, true);
            Check(fake.Plays.Count(x => x == 1513) == 2, "cooled frame is not backfilled");
            controller.Shutdown().GetAwaiter().GetResult();
        });

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            status = "pass", checks, cases,
            scope = "已驗證（離線）：B07 `<`／`[` 選擇規則與 200ms 冷卻；合成候選，不讀素材／WAV，未真機聽音"
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS selection {cases.Count} cases / {checks} checks");
    }
}
