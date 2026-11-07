using P06;
using P24.Audio;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics.Tracing;

// T173（#447）音效共用層離線檢查：執行產品 DLL 的 SoundRules／SoundGate／CombatSfxController／
// WorldSession S22 路徑與 P24.Audio 的 AudioCore，只用合成封包、合成位元組與記憶體音訊。
// 不啟動遊戲、GUI、網路、DB、原生音訊，也不讀遊戲素材。
static class Checks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly List<string> labels = [];
    static long now = 10000;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); labels.Add(label); }
    static void Throws<T>(Action action, string label) where T : Exception
    { try { action(); } catch (T) { Check(true, label); return; } throw new Exception("not rejected: " + label); }
    static void Set(object o, string name, object? value)
    { var p = o.GetType().GetProperty(name, F); if (p != null) p.SetValue(o, value); else o.GetType().GetField(name, F)!.SetValue(o, value); }
    static byte[] Packet(Action<BinaryWriter> write) { using var s = new MemoryStream(); using var b = new BinaryWriter(s); write(b); return LoginPackets.Pad(s.ToArray()); }
    // S22 格式（server-protocol.md）：C(mode) H(sound)。
    static byte[] S22(byte mode, ushort id) => Packet(b => { b.Write((byte)22); b.Write(mode); b.Write(id); });
    static WorldSession Session() { var s = new WorldSession(); Set(s, "PacketMediaClock", (Func<long>)(() => now)); return s; }
    static void Feed(WorldSession s, byte[] packet) => s.Receive(packet, "offline");

    static void Distance()
    {
        // sound.md 2.3 換算表（已驗證）。
        foreach (var (dx, dy, vol) in new[] { (0, 0, 100), (1, 0, 100), (0, 1, 100), (5, 0, 83), (8, 0, 56), (7, 7, 32), (11, 0, 16), (12, 0, 0), (0, 12, 0), (14, 0, 0), (10, 5, 0), (0, 15, 0) })
        {
            Check(SoundRules.DistanceVolume(dx, dy) == vol, $"distance ({dx},{dy}) = {vol}");
            Check(SoundRules.DistanceVolume(-dx, -dy) == vol && SoundRules.DistanceVolume(-dx, dy) == vol, $"distance sign-independent ({dx},{dy})");
        }
        for (int dx = -30; dx <= 30; dx++) for (int dy = -30; dy <= 30; dy++)
        {
            int nx = Math.Abs(dx), ny = Math.Abs(dy);
            if (nx + ny > 14) { nx = 14; ny = 0; }
            int expected = Math.Max(0, 100 - 14 * (nx * nx + ny * ny) / 20);
            if (SoundRules.DistanceVolume(dx, dy) != expected) throw new Exception($"distance formula ({dx},{dy})");
            if (Math.Abs(dx) + Math.Abs(dy) > 14 && expected != 0) throw new Exception("manhattan > 14 must be silent");
        }
        Check(true, "distance formula exhaustive -30..30 matches 100-14*(nx^2+ny^2)/20 with manhattan clamp");
        Check(SoundRules.DistanceVolume(int.MinValue, int.MaxValue) == 0, "extreme offsets do not overflow");
        Check(SoundRules.DistanceVolume(7, 6) == 100 - 14 * 85 / 20 && SoundRules.DistanceVolume(7, 8) == 0, "manhattan 13 audible, 15 silent");
    }

    static void Modes()
    {
        Check(SoundRules.Mode(0) == S22Mode.Once && SoundRules.Mode(1) == S22Mode.AmbientLoop && SoundRules.Mode(2) == S22Mode.Stop, "S22 byte 0/1/2 is mode once/loop/stop");
        for (int b = 3; b <= 255; b++) if (SoundRules.Mode((byte)b) != null) throw new Exception("undocumented mode accepted");
        Check(true, "S22 modes 3..255 undocumented -> ignored");
        Check(SoundRules.Category(S22Mode.Once) == SoundCategory.Sound && SoundRules.Category(S22Mode.AmbientLoop) == SoundCategory.EnvSound, "mode 0 SoundVolume, mode 1 EnvSoundVolume");
        foreach (byte mode in new byte[] { 0, 1, 2, 3, 255 }) foreach (ushort id in new ushort[] { 0, 97, 65535 })
            Check(SoundPackets.Sound(S22(mode, id)) == new ServerSound(mode, id), $"S22 decode C(mode)={mode} H(id)={id}");
        Check(SoundPackets.Schedule(new(0, 97), 500) is { Loop: false, Duration: 500, Frames.Length: 1 }, "mode 0 schedule is one play, not repeat count");
        Check(SoundPackets.Schedule(new(1, 97), 500) is { Loop: true, Duration: 500, Frames.Length: 1 }, "mode 1 schedule loops");
        Throws<InvalidOperationException>(() => SoundPackets.Schedule(new(2, 97), 500), "mode 2 has no play schedule");
        Throws<InvalidOperationException>(() => SoundPackets.Schedule(new(7, 97), 500), "unknown mode has no play schedule");
        var bad = S22(0, 97); bad[^1] = 1;
        Throws<ProtocolException>(() => SoundPackets.Sound(bad), "S22 nonzero padding rejected");
        Throws<ProtocolException>(() => SoundPackets.Sound(S22(0, 97)[..2]), "S22 truncated rejected");
    }

    static void Volumes()
    {
        var v = new SoundVolumes();
        foreach (var c in Enum.GetValues<SoundCategory>()) Check(v[c] == 1, $"default {c} full");
        v[SoundCategory.Sound] = .25; v[SoundCategory.EnvSound] = .5; v[SoundCategory.Voice] = .75; v[SoundCategory.Music] = 0;
        Check(v[SoundCategory.Sound] == .25 && v[SoundCategory.EnvSound] == .5 && v[SoundCategory.Voice] == .75 && v[SoundCategory.Music] == 0, "four categories independently adjustable");
        Check(v.Revision == 4, "each change bumps revision once");
        v[SoundCategory.Sound] = .25; Check(v.Revision == 4, "same value does not bump revision");
        Check(Math.Abs(v.Scale(SoundCategory.Sound, 83) - .2075) < 1e-12 && v.Scale(SoundCategory.EnvSound, 100) == .5, "scale = category x distance/100");
        Check(!v.EffectCategoriesSilent, "music zero does not silence effects");
        v[SoundCategory.Sound] = 0; v[SoundCategory.EnvSound] = 0; Check(!v.EffectCategoriesSilent, "voice still audible");
        v[SoundCategory.Voice] = 0; Check(v.EffectCategoriesSilent, "Sound+Env+Voice all zero silences effects (A06020)");
        foreach (double bad in new[] { -0.01, 1.01, double.NaN, double.PositiveInfinity })
            Throws<ArgumentOutOfRangeException>(() => v[SoundCategory.Sound] = bad, $"category volume {bad} rejected");
        Throws<ArgumentOutOfRangeException>(() => _ = v[(SoundCategory)4], "unknown category rejected");
        Throws<ArgumentOutOfRangeException>(() => v.Scale(SoundCategory.Sound, 101), "distance volume > 100 rejected");
    }

    static void Cooldown()
    {
        var c = new SoundCooldown();
        Check(c.TryAcquire(97, 1000), "first play admitted");
        Check(!c.TryAcquire(97, 1199), "same id within 200ms rejected");
        Check(c.TryAcquire(98, 1100), "other id independent");
        Check(c.TryAcquire(97, 1200), "same id at +200ms admitted");
        Check(!c.TryAcquire(97, 1300) && c.TryAcquire(97, 1400), "rejected attempt does not extend window");
        Check(ReferenceEquals(SoundCooldown.Shared, SoundCooldown.Shared), "product cooldown is one global instance");
        for (int i = 0; i < 5000; i++) c.TryAcquire(10000 + i, 2000 + i);
        Check(!c.TryAcquire(14999, 7000) && c.TryAcquire(10000, 7000), "bounded table keeps live entries");
    }

    static void Gate()
    {
        long clock = 0; var v = new SoundVolumes(); var c = new SoundCooldown(); var g = new SoundGate(v, c, () => clock);
        var a = g.Admit(new(97, SoundCategory.Sound));
        Check(a is { Dispatch: true, Status: "Admitted", DistanceVolume: 100, Scale: 1 }, "no position = full volume");
        Check(g.Admit(new(97, SoundCategory.Sound)).Status == "Cooldown", "gate applies global cooldown");
        Check(g.Admit(new(97, SoundCategory.Sound, Cooldown: false)).Dispatch, "cooldown can be skipped (A06020 arg4=0)");
        clock = 200; var d = g.Admit(new(97, SoundCategory.Sound, (5, 0)));
        Check(d.Dispatch && d.DistanceVolume == 83 && Math.Abs(d.Scale - .83) < 1e-12, "distance attenuates scale");
        clock = 400; var far = g.Admit(new(97, SoundCategory.Sound, (12, 0)));
        Check(far.Status == "Inaudible" && !far.Dispatch, "zero volume never dispatches");
        clock = 450; Check(g.Admit(new(97, SoundCategory.Sound)).Status == "Cooldown", "inaudible play still consumed cooldown (original plays muted)");
        clock = 1000; Check(g.Admit(new(55, SoundCategory.Sound, (20, 0), RequireAudible: true)).Status == "OutOfRange", "ambient requires audible source");
        Check(g.Admit(new(55, SoundCategory.Sound)).Dispatch, "out-of-range ambient does not consume cooldown");
        v[SoundCategory.EnvSound] = .5; clock = 2000;
        Check(Math.Abs(g.Admit(new(56, SoundCategory.EnvSound, (5, 0))).Scale - .415) < 1e-12, "category volume multiplies distance");
        v[SoundCategory.Sound] = 0; clock = 3000;
        Check(g.Admit(new(57, SoundCategory.Sound)).Status == "Inaudible", "muted category inaudible");
        v[SoundCategory.EnvSound] = 0; v[SoundCategory.Voice] = 0;
        Check(g.Admit(new(58, SoundCategory.Music)).Status == "CategoriesSilent", "all effect categories zero blocks before cooldown");
        v[SoundCategory.Voice] = 1; Check(g.Admit(new(58, SoundCategory.Sound)).Status == "Inaudible", "CategoriesSilent did not consume cooldown");
        v[SoundCategory.Voice] = 0; v[SoundCategory.Music] = 1;
        Check(g.Admit(new(59, SoundCategory.Music, SkipCategoryCheck: true)).Dispatch, "skip category check (A06020 arg3=1)");
    }

    static CombatSoundInstance Action(string key, uint actor, long at, int id) =>
        new(key, 1, actor, 0, 0, at, at + 1000, new([new(0, 0, [id])], 1000, false));

    static async Task Controller()
    {
        long clock = now; var v = new SoundVolumes(); var g = new SoundGate(v, new SoundCooldown(), () => clock);
        var offsets = new Dictionary<uint, (int, int)> { [1] = (0, 0), [2] = (5, 0), [3] = (12, 0), [4] = (7, 7) };
        var audio = new FakeAudio(); var observed = new List<CombatSoundObservation>();
        var controller = new CombatSfxController(audio, new Random(1), observed.Add)
        { Gate = g, Offset = id => offsets.TryGetValue(id, out var o) ? o : null };
        controller.SetControls(1, false, true);
        void Pump(long at, params CombatSoundInstance[] instances) { clock = at; controller.Pump(1, instances, at, true); }
        Pump(now, Action("actor/1", 1, now, 97), Action("actor/2", 2, now, 97));
        Check(audio.Plays.Count == 1 && audio.Plays[0] == (97, 1d), "same id from two actors at once plays once (global, cross-actor cooldown)");
        Check(observed.Any(o => o.Stage == "gate" && o.Key == "actor/2" && o.Reply.Status == "Cooldown"), "cooldown rejection observable");
        Pump(now + 250, Action("actor/2", 2, now + 250, 98) with { Instance = 2 });
        Check(audio.Plays.Last() == (98, .83), "actor 5 tiles away dispatches scale 0.83");
        Pump(now + 500, Action("actor/3", 3, now + 500, 99));
        Check(audio.Plays.Count == 2 && observed.Any(o => o.Key == "actor/3" && o.Reply.Status == "Inaudible"), "actor 12 tiles away not dispatched");
        v[SoundCategory.Sound] = .5;
        Pump(now + 750, Action("actor/4", 4, now + 750, 100));
        Check(audio.Plays.Last() == (100, .16), "diagonal 7+7 at SoundVolume 0.5 = 0.32 x 0.5");
        Pump(now + 1000, Action("actor/9", 9, now + 1000, 101));
        Check(audio.Plays.Last() == (101, .5), "unknown position falls back to no attenuation");
        await controller.Shutdown();

        var plain = new FakeAudio(); var legacy = new CombatSfxController(plain, new Random(1), _ => { });
        legacy.SetControls(1, false, true);
        legacy.Pump(1, [Action("a", 1, now, 97), Action("b", 2, now, 97)], now, true);
        Check(plain.Plays.Count == 1 && plain.Plays.All(p => p.Scale == 1), "controller without gate keeps #433 per-controller cooldown, full scale");
        legacy.Pump(1, [Action("a", 1, now, 97), Action("b", 2, now, 97), Action("c", 3, now + 200, 97)], now + 200, true);
        Check(plain.Plays.Count == 2, "no-gate cooldown is not doubled with the shared gate");
        await legacy.Shutdown();
    }

    static async Task Packets()
    {
        long clock = now; var v = new SoundVolumes(); var cooldown = new SoundCooldown(); var g = new SoundGate(v, cooldown, () => clock);
        var s = Session(); var audio = new FakeAudio(); var observed = new List<CombatSoundObservation>();
        var controller = new CombatSfxController(audio, new Random(1), observed.Add) { Gate = g, Offset = s.SoundOffset };
        controller.SetControls(1, false, true);
        void Pump(long at, double ms = 500, params CombatSoundInstance[] actions)
        { clock = at; controller.Pump(s.WorldEpoch, s.ComposePacketSounds(actions, at, _ => ms), at, true); }
        long start = now + WorldSession.PacketSoundMatchMs;

        // 模式 0：單次。
        Feed(s, S22(0, 97)); Pump(start); Pump(start + 600); Pump(start + 1200);
        Check(audio.Plays.SequenceEqual(new[] { (97, 1d) }), "mode 0 plays exactly once (not repeat+1)");
        Check(s.PacketSounds.Length == 0, "mode 0 retires after its WAV duration");

        // 模式 0 在同 id 仍在播放時不重播（A05EA0）。
        now += 5000; start = now + WorldSession.PacketSoundMatchMs;
        Feed(s, S22(0, 98)); now += 300; Feed(s, S22(0, 98)); now -= 300;
        Pump(start); Pump(start + 300); Pump(start + 400);
        Check(audio.Plays.Count(p => p.Id == 98) == 1 && s.PacketMediaNotice.Contains("仍在播放"), "mode 0 while same id playing is not replayed");
        now += 600; Feed(s, S22(0, 98)); Pump(now + WorldSession.PacketSoundMatchMs);
        Check(audio.Plays.Count(p => p.Id == 98) == 2, "mode 0 after previous finished plays again");

        // 模式 1：環境音循環，EnvSound 類別；循環輪次不受 200ms 冷卻阻擋。
        now += 5000; start = now + WorldSession.PacketSoundMatchMs; v[SoundCategory.EnvSound] = .5;
        Feed(s, S22(1, 200)); Feed(s, S22(1, 200));
        Check(s.PacketSounds.Count(p => p.Packet.SoundId == 200) == 1 && s.PacketMediaNotice.Contains("已在循環"), "duplicate mode 1 does not start a second loop");
        for (int i = 0; i <= 5; i++) Pump(start + i * 100, 100);
        Check(audio.Plays.Count(p => p.Id == 200) == 6 && audio.Plays.Where(p => p.Id == 200).All(p => p.Scale == .5), "mode 1 loops every WAV cycle at EnvSoundVolume, cooldown only on start");
        Pump(start + 60000, 100); Check(s.PacketSounds.Any(p => p.Packet.SoundId == 200), "loop has no duration end");
        int retired = audio.Retired.Count;

        // 模式 2：停止這個 id。
        Feed(s, S22(2, 200)); Pump(start + 60050, 100);
        Check(!s.PacketSounds.Any(p => p.Packet.SoundId == 200) && audio.Retired.Count == retired + 1 && s.PacketMediaNotice.Contains("停止"), "mode 2 stops the loop and retires its owner");
        int plays = audio.Plays.Count; Pump(start + 60500, 100);
        Check(audio.Plays.Count == plays, "stopped loop never dispatches again");
        Feed(s, S22(2, 4321)); Check(s.PacketMediaNotice.Contains("4321") && s.PacketSounds.Length == 0, "mode 2 for idle id is harmless");

        // 未知模式忽略。
        Feed(s, S22(9, 97)); Check(s.PacketSounds.Length == 0 && s.PacketMediaNotice.Contains("未知模式 9"), "unknown mode ignored with notice");

        // 冷卻跨管線：影格音效與 S22 共用 id 冷卻（不在 ±100ms 去重窗內）。
        // S22 於 t 收到、t+100 播放；同 id 影格音效於 t+250（離收包 250ms，不被去重）仍在冷卻內。
        now += 5000; long receivedAt = now;
        Feed(s, S22(0, 300)); Pump(receivedAt + WorldSession.PacketSoundMatchMs);
        Check(audio.Plays.Count(p => p.Id == 300) == 1, "S22 mode 0 dispatched through the gate");
        Pump(receivedAt + 250, 500, Action("actor/7", 0, receivedAt + 250, 300));
        Check(audio.Plays.Count(p => p.Id == 300) == 1, "frame sound within 200ms of same-id S22 blocked by shared cooldown");
        Check(observed.Any(o => o.Stage == "gate" && o.Key == "actor/7" && o.Reply.Status == "Cooldown"), "cross-pipeline cooldown observable");
        Pump(receivedAt + 400, 500, Action("actor/7", 0, receivedAt + 400, 300) with { Instance = 2 });
        Check(audio.Plays.Count(p => p.Id == 300) == 2, "same id admitted again after 200ms");

        // 世界重設結束循環。
        Feed(s, S22(1, 400)); Pump(now + 10000); s.Reset(); retired = audio.Retired.Count; Pump(now + 10100);
        Check(s.PacketSounds.Length == 0 && audio.Retired.Count >= retired, "world reset clears loops");
        await controller.Shutdown();
    }

    sealed class AmbientRecorder : P24.HostAudio.IAmbientLoopAudio
    {
        public readonly List<MapAmbientVoice> Started = [];
        public bool Start(MapAmbientVoice voice, int volume) { Started.Add(voice); return true; }
        public void SetVolume(MapAmbientVoice voice, int volume) { }
        public void Stop(MapAmbientVoice voice) { }
    }
    static MapEffectAppearance Ambient(int id, string events) => new(JsonSerializer.SerializeToUtf8Bytes(new
    {
        id, flags = 0, tickRate = 24, sequence = new[] { new { frame = 0, ticks = 12, events } },
        frames = new[] { new { index = 0, bounds = new[] { 0, 0, 0, 0 }, blocks = new[] { new { a = 0, b = 0, z = 0, order = 0, pixels = new[] { new[] { 0, 0, 31 } } } } } }
    }));

    // T166 `^` 環境音（main 的 MapAmbientSound）改走共用層：同一公式、同一份 id 冷卻。
    static void AmbientShared()
    {
        for (int dx = -20; dx <= 20; dx++) for (int dy = -20; dy <= 20; dy++)
            if (MapAmbientSound.Volume(dx, dy) != SoundRules.DistanceVolume(dx, dy)) throw new Exception($"ambient volume diverges ({dx},{dy})");
        Check(true, "MapAmbientSound.Volume delegates to SoundRules.DistanceVolume");
        Check(MapAmbientSound.CooldownMs == SoundRules.CooldownMs && MapAmbientSound.Reach == SoundRules.MaxManhattan, "ambient constants come from SoundRules");

        long clock = 20000; var cooldown = new SoundCooldown(); var gate = new SoundGate(new SoundVolumes(), cooldown, () => clock);
        var audio = new AmbientRecorder(); var ambient = new MapAmbientSound(audio, new Random(1), cooldown);
        var self = (X: 100, Y: 100);
        Check(gate.Admit(new(300, SoundCategory.Sound)).Dispatch, "S22/frame sound 300 takes the shared cooldown");
        ambient.Update([new("x", 101, 100, Ambient(1, "^300"), 0)], self, clock + 100, true);
        Check(audio.Started.Count == 0, "^300 within 200ms of same-id gate dispatch blocked (cross-pipeline)");
        ambient.Update([new("y", 102, 100, Ambient(2, "^300"), 0)], self, clock + 200, true);
        Check(audio.Started.Count == 1 && audio.Started[0].SoundId == 300, "^300 admitted at +200ms");
        clock += 250;
        Check(gate.Admit(new(300, SoundCategory.Sound)).Status == "Cooldown", "ambient start also blocks the gate for 200ms");

        var a1 = new AmbientRecorder(); var a2 = new AmbientRecorder();
        var m1 = new MapAmbientSound(a1, new Random(1), cooldown); var m2 = new MapAmbientSound(a2, new Random(1), cooldown);
        m1.Update([new("p", 100, 100, Ambient(3, "^301"), 0)], self, 30000, true);
        m2.Update([new("q", 100, 100, Ambient(3, "^301"), 0)], self, 30050, true);
        Check(a1.Started.Count == 1 && a2.Started.Count == 0, "two ambient models sharing SoundCooldown share one window per id");
        var i1 = new AmbientRecorder(); var i2 = new AmbientRecorder();
        new MapAmbientSound(i1, new Random(1)).Update([new("p", 100, 100, Ambient(3, "^302"), 0)], self, 30000, true);
        new MapAmbientSound(i2, new Random(1)).Update([new("q", 100, 100, Ambient(3, "^302"), 0)], self, 30050, true);
        Check(i1.Started.Count == 1 && i2.Started.Count == 1, "unspecified table stays isolated (offline tests only)");
    }

    static void Offsets()
    {
        var s = Session();
        Check(s.SoundOffset(123) == null, "no self actor -> no offset");
        Set(s, "Actor", JsonSerializer.Deserialize<OwnActor>("{\"ObjectId\":123,\"X\":32600,\"Y\":32940,\"Name\":\"offline\"}"));
        Check(s.SoundOffset(123) == (0, 0), "self emitter offset zero");
        Check(s.SoundOffset(999) == null, "unknown emitter -> no offset");
    }

    static void Core()
    {
        // 合成 1 byte 素材寫在 scratch 底下，pin 依實際位元組計算；不是遊戲素材。
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../voice-scale"));
        if (!root.Contains(Path.DirectorySeparatorChar + "scratch" + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Build these checks under scratch/<task>/tests/bin before running");
        Directory.CreateDirectory(Path.Combine(root, "Sound"));
        byte[] bytes = [0x42]; File.WriteAllBytes(Path.Combine(root, "Sound", "97.wav"), bytes);
        var pin = new AssetPin(97, "wav", "Sound/97.wav", 1, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
        var driver = new GainDriver(); var core = new AudioCore(new AudioAssets(root, [pin]), driver, 8);
        var owner = new AudioOwner("t173", 1);
        core.Register(owner); core.Configure(new(.8, false, true));
        var reply = core.Play(97, owner, null, .5);
        Check(reply.Success && driver.Gains.Last() == .4, "AudioCore applies master gain x voice scale before play");
        core.Configure(new(.6, false, true));
        Check(Math.Abs(driver.Gains.Last() - .3) < 1e-12, "master gain change keeps per-voice scale");
        Check(core.Play(97, owner, null, 1).Success && driver.Gains.Last() == .6, "scale 1 equals previous master-only gain");
        foreach (double bad in new[] { 0, -1, 1.5, double.NaN })
            Check(core.Play(97, owner, null, bad).Status == "InvalidVoiceScale", $"voice scale {bad} rejected");
        core.Retire(owner); core.Shutdown();
    }

    static async Task<int> Main(string[] args)
    {
        try
        {
            using var guard = new SocketGuard();
            Distance(); Modes(); Volumes(); Cooldown(); Gate(); await Controller(); await Packets(); AmbientShared(); Offsets(); Core();
            if (args.Length > 0)
                File.WriteAllText(args[0], JsonSerializer.Serialize(new { status = "pass", checks = labels.Count, scope = "合成封包／位元組與記憶體音訊；非真機", labels }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS {labels.Count} sound rule checks"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    sealed class SocketGuard : EventListener
    {
        protected override void OnEventSourceCreated(EventSource source) { if (source.Name == "System.Net.Sockets") EnableEvents(source, EventLevel.LogAlways); }
        protected override void OnEventWritten(EventWrittenEventArgs e) => throw new Exception("Network forbidden in offline checks");
    }
}

sealed class FakeAudio : IAudioPlayback
{
    public static readonly AudioReply Ok = new(true, "MemoryOnly", null, 0, []);
    public readonly List<(int Id, double Scale)> Plays = []; public readonly List<AudioOwner> Retired = [];
    public Task<AudioReply> RegisterAsync(AudioOwner owner) => Task.FromResult(Ok);
    public Task<AudioReply> RetireAsync(AudioOwner owner) { Retired.Add(owner); return Task.FromResult(Ok); }
    public Task<AudioReply> PlayAsync(int id, AudioOwner owner) => PlayAsync(id, owner, 1);
    public Task<AudioReply> PlayAsync(int id, AudioOwner owner, double scale) { Plays.Add((id, Math.Round(scale, 12))); return Task.FromResult(Ok); }
    public Task<AudioReply> ConfigureAsync(double gain, bool muted, bool enabled) => Task.FromResult(Ok);
    public Task<AudioReply> ShutdownWithinAsync(TimeSpan budget) => Task.FromResult(Ok);
    public Task WhenReleased => Task.CompletedTask;
    public AudioReply LastMaintenance => Ok;
}

sealed class GainDriver : IAudioDriver
{
    public readonly List<double> Gains = [];
    public NativeReply Open(string path, string format, string voice) => new(0, "opened");
    public NativeReply Gain(string voice, double normalized) { Gains.Add(Math.Round(normalized, 12)); return new(0, "gain"); }
    public NativeReply Play(string voice) => new(0, "playing");
    public NativeReply Status(string voice) => new(0, "playing");
    public NativeReply Stop(string voice) => new(0, "stopped");
    public NativeReply Close(string voice) => new(0, "closed");
}
