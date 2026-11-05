using P06;
using P24.Audio;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

string repo = Path.GetFullPath(args[0]), client = args[1], output = args[2];
string assets = Path.Combine(repo, "engine/assets");
int checks = 0;
var cases = new List<object>();
void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
void Case(string name, Action run) { int before = checks; run(); cases.Add(new { name, checks = checks - before, status = "pass" }); }
void Set(object value, string name, object data)
{
    var field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    if (field != null) field.SetValue(value, data);
    else value.GetType().GetProperty(name)!.SetValue(value, data);
}
var objects = new ObjectAssets(assets);
Case("issue47-all-62-melee-actions-and-four-skills", () =>
{
    using var fixture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repo, "engine/_integration/dev/combat-sfx-tests/expected-events.json")));
    Check(fixture.RootElement.GetProperty("actions").GetArrayLength() == 62, "all31 profiles include attack and damage");
    foreach (var action in fixture.RootElement.GetProperty("actions").EnumerateArray())
    {
        string label = action.GetProperty("label").GetString()!;
        if (label is not ("attack" or "damage")) continue;
        int gfx = action.GetProperty("gfx").GetInt32(), weapon = action.GetProperty("weapon").GetInt32();
        var appearance = objects.Get(gfx, weapon)!;
        for (int direction = 0; direction < 8; direction++)
        {
            var sequence = appearance.Sequences[(label, direction)];
            foreach (var entry in action.GetProperty("events").EnumerateArray())
            {
                int index = entry.GetProperty("sequenceIndex").GetInt32();
                Check(sequence[index].Events == entry.GetProperty("rawEvents").GetString(), $"source suffix {gfx}/{weapon}/{label}/{direction}/{index}");
            }
            var schedule = SoundSchedule.Create(appearance, label, direction);
            var played = new List<(int Id, int Frame, long Cycle)>();
            var cursor = new SoundCursor(schedule, new Random(50));
            foreach (var frame in schedule.Frames)
            {
                int before = played.Count;
                cursor.Advance(frame.At - .001, (id, i, cycle) => played.Add((id, i, cycle)));
                Check(before == played.Count, "no event before frame entry");
                cursor.Advance(frame.At, (id, i, cycle) => played.Add((id, i, cycle)));
                Check(played.Count - before == (frame.Candidates.Length == 0 ? 0 : 1), "exactly one candidate on entry");
                if (frame.Candidates.Length > 0) Check(frame.Candidates.Contains(played[^1].Id) && played[^1].Frame == frame.Index, "source frame and candidate");
                before = played.Count;
                cursor.Advance(frame.At, (id, i, cycle) => played.Add((id, i, cycle)));
                Check(played.Count == before, "same timestamp/redraw does not repeat");
            }
        }
    }
    foreach (var skill in new[] { (1, 11722, "heal", 109), (3, 221, "effect", 127), (4, 167, "fly", 97), (8, 2165, "effect", 804) })
    {
        var appearance = new ObjectAppearance(Path.Combine(assets, $"objects/magic-{skill.Item2}.json"));
        var schedule = SoundSchedule.Create(appearance, skill.Item3, 0, pure: skill.Item1 != 4, flight: skill.Item1 == 4);
        var played = new List<int>();
        var cursor = new SoundCursor(schedule, new Random(50));
        Check(schedule.Frames[0].Index == 1, "skill immediate index1 entry " + skill.Item1);
        if (skill.Item1 != 4)
            foreach (var frame in schedule.Frames)
                Check(MagicMotion.PureFrame(appearance, skill.Item3, (long)frame.At).Key == appearance.Sequences[(skill.Item3, 0)][frame.Index].Key, "pure effect sound matches renderer entry");
        cursor.Advance(-1, (id, _, _) => played.Add(id)); Check(played.Count == 0, "before skill start");
        cursor.Advance(0, (id, _, _) => played.Add(id)); Check(played.SequenceEqual(new[] { skill.Item4 }), "skill source ID " + skill.Item1);
        cursor.Advance(0, (id, _, _) => played.Add(id)); Check(played.Count == 1, "skill redraw");
        if (skill.Item1 == 4)
        {
            cursor.Advance(239, (id, _, _) => played.Add(id)); Check(played.Count == 1, "flight before next loop");
            cursor.Advance(240, (id, _, _) => played.Add(id)); Check(played.SequenceEqual(new[] { 97, 97 }), "flight loop entry");
        }
    }
    var impact = SoundSchedule.Create(new(Path.Combine(assets, "objects/magic-219.json")), "impact", 0, pure: true);
    Check(impact.Frames.All(f => f.Candidates.Length == 0), "219 impact has no invented sound");
    Check(SoundSchedule.Candidates("<206!<478<479").Length == 0, "non bracket suffix excluded");
    Check(SoundSchedule.Candidates("[248[703[701!").SequenceEqual(new[] { 248, 703, 701 }), "candidate source order");
});
Case("seed-and-action-cadence", () =>
{
    var schedule = new SoundSchedule([new(4, 0, [248, 703, 701])], 60, true);
    var expected = new Random(50); var cursor = new SoundCursor(schedule, new Random(50)); var played = new List<int>();
    for (int i = 0; i < 10; i++)
    {
        cursor.Advance(i * 60, (id, _, _) => played.Add(id));
        Check(played.Count == i + 1 && played[i] == schedule.Frames[0].Candidates[expected.Next(3)], "injected seed chooses exactly one");
    }
    var appearance = objects.Get(0, 4)!;
    foreach (byte speed in new byte[] { 0, 1, 2 })
    {
        var timing = ActionCadence.Capture(0, 4, 1, speed, 0);
        var timed = SoundSchedule.Create(appearance, "attack", 0, timing);
        foreach (var frame in timed.Frames)
            Check(timing.Frame(appearance, 0, frame.At + .001).Key == appearance.Sequences[("attack", 0)][frame.Index].Key, "normal/haste/slow frame boundary");
    }
});
Case("authority-melee-appearance-flight-owner-cancellation", () =>
{
    var session = new WorldSession();
    var actor = new OwnActor(32590, 32930, 1, 0, 4, 0, 0, 0, 0, 0, "test", "", 0, 0, "", "", 0, 100, 0, 1, 0, 0, 0);
    Set(session, "Actor", actor); Set(session, "ActionClock", (Func<long>)(() => 1000));
    var target = new WorldObject(32596, 32930, 2, 1, 0, 0, 0, 0, 0, 0, "target", "", 0, 0, "", "", 0, 100, 0, 1, 0, 0, 0);
    var worldObjects = (ConcurrentDictionary<uint, WorldObject>)typeof(WorldSession).GetField("objects", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    worldObjects[2] = target;
    var scene = new CombatSfxScene(assets);
    var predicted = new CombatAnimation(1, 1000, true) { Timing = ActionCadence.Capture(0, 4, 1, 0, 0) };
    Set(session, "OwnCombat", predicted);
    Check(scene.Snapshot(session, 1000).Length == 0, "prediction does not dispatch sound");
    typeof(WorldSession).GetMethod("PredictAction", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(session, [1, 0, (uint)2, predicted.Timing!]);
    long predictedInstance = session.OwnCombat!.Instance;
    byte[] packet = new byte[24]; packet[0] = 30; packet[1] = 1;
    BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(2), 1); BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(6), 2);
    BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(10), 5);
    session.Receive(packet, "test");
    Check(session.OwnCombat!.Instance == predictedInstance && !session.OwnCombat.Predicted, "actual prediction acknowledgement shares authority instance");
    var snapshot = scene.Snapshot(session, 1000);
    Check(snapshot.Length == 2, "actual S30 authority creates attack and damage");
    Check(snapshot.Single(s => s.Actor == 1).Schedule.Frames.SelectMany(f => f.Candidates).SequenceEqual(new[] { 248, 703, 701 }), "attacker weapon4 attack");
    Check(snapshot.Single(s => s.Actor == 2).Schedule.Frames.SelectMany(f => f.Candidates).SequenceEqual(new[] { 1546, 1547 }), "target gfx1 damage");
    long instance = session.OwnCombat!.Instance;
    Check(scene.Snapshot(session, 1001).Single(s => s.Actor == 1).Instance == instance, "authority instance stable");
    Check((predicted with { Predicted = false }).Instance == predicted.Instance, "prediction acknowledgement retains instance identity");
    var effects = (List<MagicEffect>)typeof(WorldSession).GetField("magicEffects", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
    effects.Add(new(1, 1, 2, 167, 18, 5, 2, 32590, 32930, 32596, 32930, 1000, CasterGfx: 0) { CasterWeapon = 4, ActionAt = 1000, Timing = ActionCadence.Capture(0, 4, 18, 0, 0) });
    long release = 1000 + effects[0].Timing!.ReleaseMs!.Value;
    Check(!scene.Snapshot(session, release - 1).Any(s => s.Key.StartsWith("magic")), "flight awaits existing release marker");
    var flight = scene.Snapshot(session, release).Single(s => s.Key.EndsWith("/fly"));
    Check(flight.Schedule.Frames[0].Candidates.SequenceEqual(new[] { 97 }) && flight.StartedAt == release, "flight97 enters index1 at release");
    var impact = scene.Snapshot(session, flight.EndsAt).Single(s => s.Key.EndsWith("/impact"));
    Check(impact.Schedule.Frames.All(f => f.Candidates.Length == 0), "arrival separate from silent219");
    worldObjects[2] = target with { Dead = true };
    Check(!scene.Snapshot(session, release).Any(s => s.Key.StartsWith("magic")), "target death cancels magic owner");
    worldObjects[2] = target;
    Check(!scene.Snapshot(session, release).Any(s => s.Key.StartsWith("magic")), "dead effect cannot revive");
    effects.Clear(); effects.Add(new(2, 1, 1, 221, 19, 0, 0, actor.X, actor.Y, actor.X, actor.Y, 1000, CasterGfx: 0) { CasterWeapon = 4 });
    Set(session, "Actor", actor with { Weapon = 0 });
    Check(!scene.Snapshot(session, 1000).Any(s => s.Key.StartsWith("magic")), "expired appearance owner rejected");
    Set(session, "OwnDead", true); Check(!scene.Snapshot(session, 1000).Any(s => s.Actor == 1), "own death removes own sound");
    session.Reset(); Check(scene.Snapshot(session, 1000).Length == 0, "disconnect/reset cancels scene");
});
Case("controller-dedup-cancel-and-delayed-register", () =>
{
    var fake = new FakeAudio(); var observed = new ConcurrentQueue<CombatSoundObservation>();
    var controller = new CombatSfxController(fake, new Random(50), observed.Enqueue);
    var instance = new CombatSoundInstance("actor/1", 1, 1, 0, 4, 100, 1000, new([new(1, 0, [109]), new(2, 50, [127])], 900, false));
    controller.SetControls(.3, false, true);
    controller.Pump(1, [instance], 100, true); controller.Pump(1, [instance], 100, true);
    Check(fake.Plays.Count == 1, "redraw/timer no replay");
    controller.Pump(1, [instance], 149, true); Check(fake.Plays.Count == 1, "next frame pending");
    controller.Pump(1, [instance], 150, true); Check(fake.Plays.Count == 2, "second frame once");
    controller.Pump(2, [], 151, true); Check(fake.Retired.Count == 1, "map epoch retires");
    controller.Pump(2, [instance with { Instance = 2 }], 152, true);
    controller.Pump(2, [], 153, false); Check(fake.Retired.Count == 2, "death/disconnect gate retires");
    controller.Pump(2, [instance with { Instance = 2 }], 154, true); Check(fake.Plays.Count == 4, "cancelled instance cannot resume");
    controller.Pump(2, [instance with { Instance = 3 }], 1001, true); Check(fake.Plays.Count == 4, "expired action no catchup playback");
    fake.Delay = true;
    controller.Pump(3, [instance], 100, true);
    controller.Pump(3, [], 101, false);
    fake.Pending.SetResult(FakeAudio.Ok);
    Check(SpinWait.SpinUntil(() => observed.Any(o => o.Reply.Status == "CancelledBeforeDispatch"), 2000), "late registration observable cancellation");
    Check(fake.Plays.Count == 4, "late registration never plays");
    controller.Shutdown().GetAwaiter().GetResult(); Check(fake.Shutdowns == 1, "shutdown requested");
});
Case("authoritative-death-map-and-local-disconnect-events", () =>
{
    using var connection = new LoginConnection();
    var session = connection.World;
    var actor = new OwnActor(32590, 32930, 1, 0, 4, 0, 0, 0, 0, 0, "test", "", 0, 0, "", "", 0, 100, 0, 1, 0, 0, 0);
    Set(session, "Actor", actor); Set(session, "ActionClock", (Func<long>)(() => 1000));
    var scene = new CombatSfxScene(assets); var fake = new FakeAudio();
    var controller = new CombatSfxController(fake, new Random(50), _ => { });
    controller.SetControls(.5, false, true);
    void Pump() => controller.Pump(session.WorldEpoch, scene.Snapshot(session, 1000), 1000, !session.OwnDead);
    session.CombatSoundStateChanged += Pump; connection.Changed += Pump;
    session.Receive([158, 1, 0, 0, 0, 1, 0, 0], "test");
    session.Receive([158, 1, 0, 0, 0, 8, 0, 0], "test");
    Check(session.OwnDead && fake.Retired.Count == 1, "S158 death immediately retires action owner");
    Set(session, "OwnDead", false); session.Receive([158, 1, 0, 0, 0, 1, 0, 0], "test");
    int epoch = session.WorldEpoch;
    session.Receive([206, 4, 0, 0, 0, 0, 0, 0], "test");
    Check(session.WorldEpoch != epoch && fake.Retired.Count == 2, "S206 map immediately retires action owner");
    Set(session, "Actor", actor); session.Receive([158, 1, 0, 0, 0, 1, 0, 0], "test");
    connection.Cancel();
    Check(connection.State == LoginState.Cancelled && fake.Retired.Count == 3, "offline Cancel reset and Changed retire owner");
    controller.Shutdown().GetAwaiter().GetResult();
});
Case("33-wav-pins-controls-missing-integrity-cleanup-and-6016-fallback", () =>
{
    var catalog = AudioAssets.LoadCombat(client);
    using var pins = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repo, "engine/audio/Resources/combat-sfx-pins.json")));
    foreach (var pin in pins.RootElement.GetProperty("wavPins").EnumerateArray())
    {
        var acquired = catalog.Acquire(pin.GetProperty("id").GetInt32(), "wav");
        Check(acquired.Error == null && acquired.Lease != null, "original WAV size/SHA readonly verified"); acquired.Lease?.Dispose();
    }
    var driver = new FakeDriver(); var core = new AudioCore(catalog, driver, 32); var owner = new AudioOwner("test", 1);
    Check(core.Register(owner).Success && core.Configure(new(.3, false, true)).Success, "owner and volume");
    Check(core.Play(109, owner).Status == "StartedWav" && driver.Gains.Last() == .3, "pinned WAV uses configured gain before play");
    Check(core.Play(3332, owner).Status == "IdNotAllowed", "combat catalog cannot substitute6016");
    Check(core.Configure(new(.2, true, true)).Status == "DispatchSuppressed" && core.Count == 0, "mute stops voices");
    Check(core.Play(109, owner).Status == "DispatchSuppressed", "mute refuses new sound");
    core.Configure(new(0, false, true)); Check(core.Play(109, owner).Success && driver.Gains.Last() == 0, "zero gain observable");
    driver.Completed = true; Check(core.Poll().Success && core.Count == 0, "completed voice cleanup"); driver.Completed = false;
    core.Play(109, owner); driver.FailClose = true;
    Check(core.Retire(owner).RemainingVoices == 1, "close failure retains voice lease");
    Check(core.Play(109, owner).Status == "StaleOrUnknownOwner", "retired owner refused");
    driver.FailClose = false; Check(core.Poll().RemainingVoices == 0, "cleanup retry releases voice");
    var missing = AudioAssets.LoadCombat(Path.Combine(repo, "scratch/issue-50/absent-client"));
    var missingCore = new AudioCore(missing, driver, 32); missingCore.Register(owner); missingCore.Configure(new(1, false, true));
    Check(missingCore.Play(109, owner).Status == "FileMissing", "missing WAV explicitly refuses");
    var row = pins.RootElement.GetProperty("wavPins")[0];
    string path = row.GetProperty("path").GetString()!, sha = row.GetProperty("sha256").GetString()!; long size = row.GetProperty("size").GetInt64();
    var bad = new AudioCore(new(client, [new(97, "wav", path, size, new string('0', 64))]), driver, 32);
    bad.Register(owner); bad.Configure(new(1, false, true)); int opens = driver.Opens;
    Check(bad.Play(97, owner).Status == "IntegrityFailure" && driver.Opens == opens, "hash mismatch never opens or falls back");
    var fallbackDriver = new FakeDriver { FailMp3 = true };
    var fallback = new AudioCore(new(client, [new(3332, "mp3", path, size, sha), new(3332, "wav", path, size, sha)]), fallbackDriver, 32);
    fallback.Register(owner); fallback.Configure(new(.5, false, true));
    Check(fallback.Play(3332, owner).Status == "StartedWavFallback" && fallbackDriver.Opens == 2, "existing6016 MP3-open fallback remains");
    Check(fallback.Shutdown().RemainingVoices == 0, "fallback voice released");
});
Case("audio-player-owner-and-mute-fence", () =>
{
    foreach (bool mute in new[] { false, true })
    {
        var driver = new FakeDriver { BlockOpen = true };
        var core = new AudioCore(AudioAssets.LoadCombat(client), driver, 32);
        var player = AudioPlayer.ForTests(core); var owner = new AudioOwner("async", 1);
        player.ConfigureAsync(.4, false, true).GetAwaiter().GetResult(); player.RegisterAsync(owner).GetAwaiter().GetResult();
        var play = player.PlayAsync(109, owner); Check(driver.Entered.Wait(2000), "worker entered fake native open");
        var fence = mute ? player.ConfigureAsync(.4, true, true) : player.RetireAsync(owner);
        driver.Continue.Set();
        Check(play.GetAwaiter().GetResult().Status == "DispatchSuppressed", "inflight owner/control fence blocks Run");
        fence.GetAwaiter().GetResult(); Check(driver.Plays == 0 && core.Count == 0, "fence cleans opened voice");
        Check(player.ShutdownWithinAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult().Success, "worker releases");
    }
});
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(new
{
    status = "pass", checks, cases,
    productSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(WorldSession).Assembly.Location))).ToLowerInvariant(),
    scope = "已驗證（離線），真實 metadata/WAV 唯讀驗證，managed fake driver 無實際播放，待 #52 真機聽音"
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS {cases.Count} cases / {checks} checks");

sealed class FakeAudio : IAudioPlayback
{
    public static AudioReply Ok = new(true, "FakeSuccess", null, 0, []);
    public List<int> Plays = []; public List<AudioOwner> Retired = []; public int Shutdowns; public bool Delay;
    public TaskCompletionSource<AudioReply> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<AudioReply> RegisterAsync(AudioOwner owner) => Delay ? Pending.Task : Task.FromResult(Ok);
    public Task<AudioReply> RetireAsync(AudioOwner owner) { Retired.Add(owner); return Task.FromResult(Ok); }
    public Task<AudioReply> PlayAsync(int id, AudioOwner owner) { Plays.Add(id); return Task.FromResult(Ok); }
    public Task<AudioReply> ConfigureAsync(double gain, bool muted, bool enabled) => Task.FromResult(Ok);
    public Task<AudioReply> ShutdownWithinAsync(TimeSpan budget) { Shutdowns++; return Task.FromResult(Ok); }
    public Task WhenReleased => Task.CompletedTask;
    public AudioReply LastMaintenance => Ok;
}
sealed class FakeDriver : IAudioDriver
{
    public int Opens, Plays; public bool Completed, FailClose, FailMp3, BlockOpen; public List<double> Gains = [];
    public ManualResetEventSlim Entered = new(), Continue = new();
    public NativeReply Open(string path, string format, string voice)
    {
        Opens++; if (BlockOpen) { Entered.Set(); Continue.Wait(TimeSpan.FromSeconds(5)); }
        return FailMp3 && format == "mp3" ? new(1, "explicit fake open failure") : new(0, "opened");
    }
    public NativeReply Gain(string voice, double normalized) { Gains.Add(normalized); return new(0, "gain"); }
    public NativeReply Play(string voice) { Plays++; return new(0, "playing"); }
    public NativeReply Status(string voice) => new(0, Completed ? "stopped" : "playing");
    public NativeReply Stop(string voice) => new(0, "stopped");
    public NativeReply Close(string voice) => FailClose ? new(1, "explicit fake close failure") : new(0, "closed");
}
