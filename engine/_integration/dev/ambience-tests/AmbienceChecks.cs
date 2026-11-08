using P06;
using System.Drawing;
using System.Drawing.Imaging;

partial class Checks {
    static byte[] WeatherPacket(byte value) => Packet(b => { b.Write((byte)115); b.Write(value); });
    static byte[] LightPacket(uint id, byte value) => Packet(b => { b.Write((byte)40); b.Write(id); b.Write(value); });
    static void AmbienceReject(LoginConnection c, byte[] packet, string label) {
        bool rejected = false;
        try { Feed(c, packet); } catch (ProtocolException) { rejected = true; }
        Check(rejected, "ambience rejects " + label);
    }
    static ObjectVisual Lamp(uint id, PointF point) => new(id, 0, 0, "synthetic", 0, 0, "breath", "", point, new(0, 0, 20, 30), false, 0);
    static readonly Color AmbienceBase = Color.FromArgb(255, 248, 132, 66);
    static Bitmap AmbienceImage(AmbienceFrame frame, AmbienceLightMap? map = null, WeatherParticles? particles = null, WeatherSprite? sprite = null) {
        var bitmap = new Bitmap(240, 180, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(AmbienceBase);
        AmbiencePainter.Draw(graphics, bitmap, new(20, 20, 200, 140), frame, map, particles, sprite);
        return bitmap;
    }
    static int ChangedPixels(Bitmap bitmap) {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
            if (bitmap.GetPixel(x, y).ToArgb() != AmbienceBase.ToArgb()) count++;
        return count;
    }
    static bool SamePixels(Bitmap a, Bitmap b) {
        for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++)
            if (a.GetPixel(x, y) != b.GetPixel(x, y)) return false;
        return true;
    }
    static Color Shaded(Color c, int f) => Color.FromArgb(c.A, AmbienceCurve.Channel8(c.R, f), AmbienceCurve.Channel8(c.G, f), AmbienceCurve.Channel8(c.B, f));
    static uint Clock(int hour, int minute) => (uint)(hour * 3600 + minute * 60);

    static void AmbienceTables() {
        // AM§1(c) sub_EDB7D0, minute → level (hand-derived from 15*(m-360)/40+1 and 15*(1240-m)/40+1).
        var curve = new (int H, int M, byte Level)[] {
            (0, 0, 1), (6, 0, 1), (6, 1, 1), (6, 3, 2), (6, 6, 3), (6, 10, 4), (6, 20, 8), (6, 30, 12), (6, 37, 14), (6, 38, 15), (6, 39, 15),
            (6, 40, 15), (12, 0, 15), (20, 0, 15), (20, 1, 15), (20, 2, 15), (20, 3, 14), (20, 4, 14), (20, 10, 12), (20, 20, 8), (20, 30, 4), (20, 38, 1), (20, 39, 1), (20, 40, 1), (23, 59, 1) };
        foreach (var (h, m, level) in curve) {
            Check(AmbienceCurve.Level(Clock(h, m), false) == level, $"curve {h:00}:{m:00} -> {level}");
            Check(AmbienceCurve.Level(Clock(h, m) + 59, false) == level, $"curve ignores seconds {h:00}:{m:00}");
            Check(AmbienceCurve.Level(Clock(h, m), true) == (level >= 10 ? level - 1 : level), $"curve rain/snow -1 only at >=10 {h:00}:{m:00}");
        }
        for (int m = 0; m < 1440; m++) {
            int l = AmbienceCurve.Level((uint)m * 60, false);
            Check(l is >= 1 and <= 15 && (m is > 360 and < 400 || m is > 1200 and < 1240 || l == (m is >= 400 and <= 1200 ? 15 : 1)), "curve range minute " + m);
        }
        Check(AmbienceCurve.Level(0x80000000u + Clock(6, 20), false) == AmbienceCurve.Level(0x80000000u + Clock(6, 20) - 2019686400u, false) && AmbienceCurve.Level(uint.MaxValue, false) == AmbienceCurve.Level(uint.MaxValue % 86400, false),
            "gmtime32 high values keep time of day");
        // AM§1(e)(f) frame level.
        var frames = new (byte L, ushort? Map, byte Flag, bool Vision, byte Expected)[] {
            (1, 4, 0, false, 2), (15, 4, 0, false, 15), (14, 4, 0, false, 15), (1, 4, 19, false, 3), (1, 4, 20, false, 7), (12, 4, 20, false, 15),
            (1, 4, 0, true, 4), (14, 4, 0, true, 15), (1, 1000, 0, false, 7), (15, 2006, 0, false, 3), (1, 1931, 0, false, 15), (1, 1932, 0, false, 15),
            (1, 4995, 0, false, 9), (1, 5122, 0, false, 9), (1, 5123, 0, false, 2), (1, 16384, 0, false, 9), (1, 65535, 0, false, 9), (1, null, 0, false, 2),
            (0, 4, 0, false, 1), (1, 2006, 20, true, 10) };
        foreach (var (l, mapId, flag, vision, expected) in frames)
            Check(AmbienceCurve.FrameLevel(l, mapId, flag, vision) == expected, $"frame level L{l} map {mapId} flag {flag} vision {vision} -> {expected}");
        // AM§3(a) light conversion.
        foreach (int v in Enumerable.Range(0, 256)) {
            var (own, flag) = AmbienceCurve.OwnLight((byte)v);
            var expected = v < 5 ? (5, 0) : v == 19 ? (10, 19) : v == 20 ? (14, 20) : (v, 0);
            Check(own == expected.Item1 && flag == expected.Item2, "own light " + v);
            Check(AmbienceCurve.OtherLight((byte)v) == (v == 20 ? 5 : v), "other light " + v);
            Check(AmbienceCurve.SourceIndex((byte)v) == Math.Min(v, 15), "light map index " + v);
        }
        // AM§1(h) coefficient table: unlit column per mode, plus row anchors.
        int[][] unlit = [
            [0, 4, 10, 11, 13, 14, 16, 18, 19, 21, 23, 24, 26, 28, 29, 31],
            [0, 4, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 31, 31, 31],
            [0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 31]];
        for (int mode = 0; mode < 3; mode++) for (int level = 0; level < 16; level++) {
            Check(AmbienceCurve.Coefficient(mode, level, 0) == unlit[mode][level] && AmbienceCurve.Coefficient(mode, level, 7) == unlit[mode][level], $"table mode {mode} level {level} unlit");
            Check(AmbienceCurve.Coefficient(mode, level, 255) == 31, $"table mode {mode} level {level} fully lit");
            for (int i = 1; i < 32; i++) Check(AmbienceCurve.Coefficient(mode, level, (byte)(i << 3)) >= AmbienceCurve.Coefficient(mode, level, (byte)(i - 1 << 3)), $"table monotonic {mode}/{level}/{i}");
        }
        Check(AmbienceCurve.Coefficient(0, 2, 8) == 10 && AmbienceCurve.Coefficient(0, 2, 16) == 11 && AmbienceCurve.Coefficient(0, 1, 200) == 28 && AmbienceCurve.Coefficient(1, 0, 24) == 4 && AmbienceCurve.Coefficient(2, 14, 80) == 29,
            "table interior entries");
        // AM§1(i) per-channel pixel vectors: 5-bit c*f/31, expanded back to 8 bits.
        var vectors = new (byte C, int F, byte Out)[] { (255, 31, 255), (255, 10, 82), (255, 0, 0), (248, 10, 82), (8, 10, 0), (24, 16, 8), (128, 13, 49), (66, 4, 8), (132, 21, 82), (7, 30, 0) };
        foreach (var (c, f, o) in vectors) Check(AmbienceCurve.Channel8(c, f) == o, $"channel {c} * {f}/31 -> {o}");
        for (int c5 = 0; c5 < 32; c5++) for (int f = 0; f < 32; f++) Check(AmbienceCurve.Channel5(c5, f) == c5 * f / 31, $"word_14325C0[{c5},{f}]");
        // AM§3(c) light map stamp (synthetic image; source.lmp itself is not available).
        var map = new AmbienceLightMap(); byte[] dot = [10, 20, 30, 40, 50, 60, 70, 80];
        map.Stamp(408, 242, dot, 4, 2);
        Check(map[200, 120] == 10 && map[203, 121] == 80 && map[199, 120] == 0 && map[204, 120] == 0, "stamp at ((x-w)>>1)&~3, (y-h)>>1");
        map.Stamp(408, 242, [99, 0, 0, 0, 0, 0, 0, 0], 4, 2);
        Check(map[200, 120] == 99 && map[201, 120] == 20, "stamp overlap keeps the maximum");
        map.Stamp(-10, -10, dot, 4, 2); map.Stamp(1000, 1000, dot, 4, 2); map.Clear();
        Check(map[200, 120] == 0 && map[-1, 0] == 0 && map[400, 0] == 0, "stamp clipping and clear");
    }

    static void AmbienceChecks() {
        AmbienceTables();
        using var c = Seed(); var world = c.World; long now = 10000;
        Set(world, "SceneClock", (Func<long>)(() => now));
        Set(world, "ShipClock", (Func<long>)(() => now));
        // Seed() installs an own status with Time = 0: S_STATUS time is a clock source too.
        Check(world.Ambience.At(now).Seconds == 0 && world.Ambience.At(now).Level == 1, "S_STATUS time sets the clock");
        using (var fresh = new LoginConnection())
            Check(fresh.World.Ambience.At(now).Seconds == null && !fresh.World.Ambience.At(now).Shaded, "no time source means no invented night");
        foreach (uint time in new uint[] { 0, 18000, 19800, 21600, 22800, 43200, 64799, 64800, 72000, 73200, 74400, 86399, 86400, uint.MaxValue }) {
            Feed(c, TimePacket(time)); var frame = world.Ambience.At(now);
            Check(frame.Seconds == time && world.ServerTime == time, "S123 unsigned LE seconds " + time);
            Check(frame.Night == (time % 86400 < 21600 || time % 86400 >= 64800), "server day/night boundary " + time);
            Check(frame.Level == AmbienceCurve.Level(time, false), "S123 recomputes level " + time);
        }
        Feed(c, TimePacket(Clock(6, 20))); byte dawn = world.Ambience.At(now).Level;
        now += 3_600_000;
        Check(world.Ambience.At(now).Seconds == Clock(6, 20) && world.Ambience.At(now).Level == dawn && dawn == 8, "clock does not advance locally");
        Feed(c, TimePacket(Clock(12, 0))); Check(!world.Ambience.At(now).Shaded, "noon is unmodified");
        Feed(c, TimePacket(Clock(20, 20))); Check(world.Ambience.At(now).Level == 8 && world.Ambience.At(now).FrameLevel == 9, "dusk level and +1 frame level");
        // The ship clock needs a consistent preceding sample (66600 + 500 ms * 0.006 = 66603).
        Feed(c, TimePacket(66600));
        int replies = world.TimeReplies, revision = world.ShipClockRevision;
        world.ProbeSent = true; now += 500; Feed(c, TimePacket(66603));
        Check(world.TimeReplies == replies + 1 && world.ShipClockRevision == revision + 1 && world.ShipServerSeconds == 66603,
            "S123 still advances ready probe and ship sample exactly once");
        now += 500; _ = world.Ambience.At(now);
        Check(world.ServerTime == 66603 && world.ShipServerSeconds == 66603, "ambience cannot alter boat timetable");
        // S_STATUS time (rounded to 300 s by the server) is applied when it changes; HP/MP updates keep it.
        Set(world, "Status", world.Status! with { Time = Clock(6, 30) });
        Check(world.Ambience.At(now).Seconds == Clock(6, 30) && world.Ambience.At(now).Level == 12, "S_STATUS time change recomputes");
        Set(world, "Status", world.Status! with { Hp = 50 });
        Feed(c, TimePacket(Clock(6, 35))); Set(world, "Status", world.Status! with { Hp = 60 });
        Check(world.Ambience.At(now).Seconds == Clock(6, 35), "status without a new time does not roll back S123");
        // AM§2(a) step 5: each S115 with an amount lowers L ≥ 10 by one (no recompute); the curve then applies its own −1.
        Feed(c, TimePacket(Clock(12, 0))); Feed(c, WeatherPacket(2));
        Check(world.Ambience.At(now).Level == 14, "S115 rain lowers L >= 10 by one");
        Feed(c, WeatherPacket(0x12)); Check(world.Ambience.At(now).Level == 13, "every S115 with an amount lowers L again (snow too)");
        Feed(c, WeatherPacket(0)); Feed(c, WeatherPacket(4)); Feed(c, WeatherPacket(0x20));
        Check(world.Ambience.At(now).Level == 13, "clear, n > 3 and unknown kinds do not lower L");
        Feed(c, WeatherPacket(2));
        Feed(c, TimePacket(Clock(12, 0))); Check(world.Ambience.At(now).Level == 14 && world.Ambience.At(now).FrameLevel == 15, "rain -1 at recompute, offset by frame +1");
        Feed(c, TimePacket(Clock(20, 20))); Feed(c, WeatherPacket(3));
        Check(world.Ambience.At(now).Level == 8, "S115 leaves L < 10 unchanged");
        Feed(c, TimePacket(Clock(20, 3))); Feed(c, WeatherPacket(1));
        Check(world.Ambience.At(now).Level == 12, "dusk L 13 (curve 14 - 1) lowered to 12 by S115");
        Feed(c, TimePacket(Clock(12, 0)));
        Feed(c, WeatherPacket(0)); Feed(c, MapPacket(5)); Check(world.Ambience.At(now).Level == 15 && world.Ambience.At(now).MapId == 5, "map change recomputes");
        Feed(c, MapPacket(2006)); Feed(c, TimePacket(Clock(0, 0))); Check(world.Ambience.At(now).FrameLevel == 3, "map override in frame");
        Feed(c, MapPacket(4)); Feed(c, TimePacket(Clock(0, 0)));
        // AM§1(g) light effect +5.
        world.Ambience.BeginLightEffect(); Check(world.Ambience.At(now).Level == 6, "light effect raises L by 5");
        world.Ambience.BeginLightEffect(); Feed(c, TimePacket(Clock(12, 0))); Check(world.Ambience.At(now).Level == 6, "curve does not overwrite during effect");
        world.Ambience.EndLightEffect(); Check(world.Ambience.At(now).Level == 6, "effect count not yet zero");
        world.Ambience.EndLightEffect(); world.Ambience.EndLightEffect(); Check(world.Ambience.At(now).Level == 1, "last effect restores saved L");
        Feed(c, TimePacket(Clock(0, 0)));

        foreach (int code in Enumerable.Range(0, 256)) {
            Feed(c, WeatherPacket((byte)code)); var frame = world.Ambience.At(now);
            var expected = code is >= 1 and <= 3 ? WeatherKind.Rain : code is >= 17 and <= 19 ? WeatherKind.Snow : WeatherKind.Clear;
            int n = (code & 15) <= 3 ? code & 15 : 0;
            Check(frame.Weather == code && frame.Kind == expected && frame.Intensity == (expected == WeatherKind.Clear ? 0 : code & 15)
                && frame.Rain == ((code & 0xF0) == 0 ? n : 0) && frame.Snow == ((code & 0xF0) == 0x10 ? n : 0)
                && AmbienceWeather.Decode((byte)code) == (frame.Rain, frame.Snow), "S115 value " + code);
            Check(frame.Level == 1, "S115 at night leaves L " + code);
        }
        // AM§2(a) step 6: maps 7701–7710 and 7771–7780 force rain 2 on every S115, whatever it says.
        foreach (ushort mapId in new ushort[] { 7700, 7701, 7705, 7710, 7711, 7770, 7771, 7780, 7781 }) {
            bool forced = mapId is >= 7701 and <= 7710 or >= 7771 and <= 7780;
            Check(AmbienceWeather.ForcedRain(mapId) == forced, "forced rain map table " + mapId);
            Feed(c, MapPacket(4)); Feed(c, WeatherPacket(0)); Feed(c, MapPacket(mapId));
            Check(world.Ambience.At(now).Rain == 0, "map change alone does not start forced rain " + mapId);
            foreach (byte code in new byte[] { 0, 3, 0x13, 0x20, 0x0F }) {
                Feed(c, WeatherPacket(code)); var frame = world.Ambience.At(now); var (r, s) = AmbienceWeather.Decode(code);
                Check(frame.Rain == (forced ? 2 : r) && frame.Snow == s && frame.Kind == (forced || r > 0 ? WeatherKind.Rain : s > 0 ? WeatherKind.Snow : WeatherKind.Clear),
                    $"map {mapId} S115 {code:X2} rain/snow");
            }
        }
        Check(!AmbienceWeather.ForcedRain(null), "no map, no forced rain");
        Feed(c, MapPacket(4)); Feed(c, TimePacket(Clock(12, 0))); Feed(c, MapPacket(7701)); _ = world.Ambience.At(now); Feed(c, WeatherPacket(0));
        Check(world.Ambience.At(now).Level == 15 && world.Ambience.At(now).Rain == 2, "forced rain does not lower L by itself (step 5 uses the packet amount)");
        Feed(c, MapPacket(4)); Feed(c, WeatherPacket(0)); Feed(c, TimePacket(Clock(0, 0)));
        // AM§2(a): removing (S120) or action 8 (S158) of gfx 10683/10632 clears snow, 10891 clears rain.
        static byte[] WeatherObject(uint id, ushort gfx) { var p = Pack(id); p[9] = (byte)gfx; p[10] = (byte)(gfx >> 8); return p; }
        static byte[] Remove(uint id) => Packet(b => { b.Write((byte)120); b.Write(id); });
        static byte[] Act(uint id, byte action) => Packet(b => { b.Write((byte)158); b.Write(id); b.Write(action); });
        var observe = typeof(WorldSession).GetMethod("ObserveWeatherObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        void ObserveAction(uint id, byte action) => observe.Invoke(world, [(byte)158, Act(id, action)]);
        var paddedAction = Act(777, 8); paddedAction[^1] = 1; // nonzero padding: ignored by the observer
        foreach (var (gfx, clearsRain, clearsSnow) in new (ushort, bool, bool)[] { (10683, false, true), (10632, false, true), (10891, true, false), (1110, false, false), (10682, false, false), (10892, false, false) }) {
            Check(AmbienceWeather.ClearsRain(gfx) == clearsRain && AmbienceWeather.ClearsSnow(gfx) == clearsSnow, "gfx table " + gfx);
            // S158 action 8 continues into AnimateCombat, which needs the sprite catalogue (not in the offline checkout):
            // feed the observe-only hook directly with the synthetic S158; S120 goes through the full dispatch.
            foreach (var gone in new Action<uint>[] { id => Feed(c, Remove(id)), id => ObserveAction(id, 8) }) {
                // Rain and snow cannot both arrive by S115; use the 0x10 packet for snow and a forced-rain map for both.
                Feed(c, MapPacket(7701)); Feed(c, WeatherObject(777, gfx)); Feed(c, WeatherObject(778, gfx)); Feed(c, WeatherPacket(0x13));
                Check(world.Ambience.At(now).Rain == 2 && world.Ambience.At(now).Snow == 3, "forced map gives rain and snow " + gfx);
                gone(777); var frame = world.Ambience.At(now);
                Check(frame.Rain == (clearsRain ? 0 : 2) && frame.Snow == (clearsSnow ? 0 : 3), $"gfx {gfx} gone clears its weather");
                Feed(c, Remove(778)); Feed(c, MapPacket(4)); Feed(c, WeatherPacket(0));
            }
        }
        Feed(c, MapPacket(7701)); Feed(c, WeatherObject(777, 10891)); Feed(c, WeatherPacket(0x13));
        foreach (byte action in new byte[] { 0, 1, 7, 9, 19, 70 }) ObserveAction(777, action);
        Check(world.Ambience.At(now).Rain == 2, "other S158 actions keep weather");
        observe.Invoke(world, [(byte)158, paddedAction]); observe.Invoke(world, [(byte)158, Act(777, 8)[..6]]);
        Check(world.Ambience.At(now).Rain == 2, "malformed S158 action 8 is ignored by the observer");
        Feed(c, Remove(999)); Check(world.Ambience.At(now).Rain == 2, "unknown removed ID keeps weather");
        var malformed = Remove(777); malformed[^1] = 1; AmbienceReject(c, malformed, "S120 nonzero padding");
        Check(world.Ambience.At(now).Rain == 2 && world.Objects.Any(o => o.ObjectId == 777), "malformed S120 keeps weather and object");
        Feed(c, Remove(777)); Check(world.Ambience.At(now).Rain == 0 && world.Ambience.At(now).Snow == 3, "S120 of gfx 10891 clears rain only");
        Feed(c, MapPacket(4)); Feed(c, WeatherPacket(0));
        // S40 conversion: own <5 -> 5, 19 -> 10 (+1), 20 -> 14 (+5); others 20 -> 5.
        Feed(c, Pack(123)); Feed(c, Pack(456, 32601));
        Feed(c, WeatherPacket(3)); Feed(c, LightPacket(123, 14)); Feed(c, LightPacket(456, 5));
        Check(world.Actor!.Light == 14 && world.Objects.Single(o => o.ObjectId == 456).Light == 5 && world.Ambience.At(now).OwnLightFlag == 0, "S40 own and peer light");
        Feed(c, LightPacket(123, 0)); Check(world.Actor!.Light == 5, "own light below 5 becomes 5");
        Feed(c, LightPacket(123, 20)); Check(world.Actor!.Light == 14 && world.Ambience.At(now).OwnLightFlag == 20 && world.Ambience.At(now).FrameLevel == 7, "own 20 -> 14 and +5");
        Feed(c, LightPacket(123, 19)); Check(world.Actor!.Light == 10 && world.Ambience.At(now).OwnLightFlag == 19 && world.Ambience.At(now).FrameLevel == 3, "own 19 -> 10 and +1");
        Feed(c, LightPacket(456, 20)); Check(world.Objects.Single(o => o.ObjectId == 456).Light == 5 && world.Ambience.At(now).OwnLightFlag == 0, "peer 20 -> 5; every S40 clears own flags");
        Feed(c, LightPacket(123, 14));
        foreach (var good in new[] { WeatherPacket(1), LightPacket(123, 2), TimePacket(1) }) {
            for (int length = 0; length < good.Length; length++)
                AmbienceReject(c, good[..length], "opcode " + good[0] + " truncated " + length);
            var padding = (byte[])good.Clone(); padding[^1] = 1;
            AmbienceReject(c, padding, "opcode " + good[0] + " nonzero padding");
            AmbienceReject(c, [.. good, .. new byte[8]], "opcode " + good[0] + " extra block");
        }
        Check(world.Ambience.At(now).Weather == 3 && world.Actor!.Light == 14, "malformed packets do not mutate weather/light");
        Check(world.Ambience.At(now).Seconds == 0, "malformed S123 does not notify clock");
        var peer = Lamp(456, new(50, 60)); var own = Lamp(123, new(100, 110));
        var lamps = world.AmbienceLights(p => new(p.X * 2 + 5, p.Y * 2 + 7), own, [peer]);
        Check(lamps.Length == 2 && lamps[0].Center == new PointF(205, 227) && lamps[1].Center == new PointF(105, 127), "lights follow displayed feet and camera projection");
        Check(lamps[0].Source == 14 && lamps[1].Source == 5, "light map index k");
        Check(world.AmbienceLights(p => p, null, [peer with { Bounds = Rectangle.Empty }]).Length == 0, "unrendered actors cannot create phantom lamps");
        Feed(c, LightPacket(456, 0)); Check(world.AmbienceLights(p => p, own, [peer]).Length == 1, "peer zero has no source");
        Feed(c, LightPacket(uint.MaxValue, 255)); Feed(c, Pack(uint.MaxValue));
        Check(world.Objects.Single(o => o.ObjectId == uint.MaxValue).Light == 0, "unknown high ID cannot leak light into later S87");
        Feed(c, LightPacket(456, 255)); Check(world.Objects.Single(o => o.ObjectId == 456).Light == 255 && world.AmbienceLights(p => p, null, [peer])[0].Source == 15, "full byte retained, index capped at 15");
        var crowd = Enumerable.Range(1000, 60).Select(i => Lamp((uint)i, new(i, 0))).ToArray();
        foreach (var lamp in crowd) { Feed(c, Pack(lamp.ObjectId, (ushort)(32000 + lamp.ObjectId))); Feed(c, LightPacket(lamp.ObjectId, 15)); }
        var budgeted = world.AmbienceLights(p => p, own, crowd);
        Check(budgeted.Length == 1 + 46 && budgeted.Sum(l => l.Source) == 14 + 46 * 15, "source budget stops once sum of k reaches 700");
        Feed(c, Packet(b => { b.Write((byte)120); b.Write(456u); }));
        Check(world.AmbienceLights(p => p, null, [peer]).Length == 0, "S120 removes lamp even with stale render list");
        Feed(c, Pack(456)); Check(world.Objects.Single(o => o.ObjectId == 456).Light == 0, "object ID reuse inherits fresh S87 light");
        var selfPack = Pack(123); selfPack[13] = 7; Feed(c, selfPack);
        Check(world.Actor!.Light == 7, "S87 light works without prior S40");
        Feed(c, TimePacket(0)); Feed(c, MapPacket(4));
        Check(world.Ambience.At(now).Weather == 3 && world.Ambience.At(now).Seconds == 0, "global weather and time survive S206 map change");
        Check(world.AmbienceLights(p => p, own, [peer]).Length == 0, "map clears own and peer lights");
        world.Reset(); Check(world.Ambience.At(now).Seconds == null && world.Ambience.At(now).Weather == 0 && world.Ambience.At(now).OwnLightFlag == 0 && !world.Ambience.At(now).Shaded, "logout immediately clears clock, weather and flags");
        Feed(c, TimePacket(43200)); Check(world.Ambience.At(now).Weather == 0, "new session time cannot revive previous weather");

        // Pixels: coefficient shading of a synthetic bitmap (base colour 248,132,66).
        var day = new AmbienceFrame(Clock(12, 0), 0, 10000); var night = day with { Level = 1, MapId = 4 };
        using var daylight = AmbienceImage(day); using var dark = AmbienceImage(night);
        Check(ChangedPixels(daylight) == 0, "daylight leaves pixels untouched");
        Check(night.FrameLevel == 2 && dark.GetPixel(120, 90) == Shaded(AmbienceBase, 10) && dark.GetPixel(120, 90) == Color.FromArgb(255, 82, 41, 16), "night pixel = base * 10/31 per channel");
        Check(dark.GetPixel(20, 20) == dark.GetPixel(219, 159) && dark.GetPixel(19, 20) == AmbienceBase && dark.GetPixel(220, 159) == AmbienceBase && dark.GetPixel(0, 0) == AmbienceBase, "shading limited to viewport");
        foreach (var (frame, f) in new[] { (night with { NightVision = true }, 13), (night with { MapId = 1000 }, 18), (night with { OwnLightFlag = 20 }, 18), (night with { Level = 13 }, 29), (night with { TableMode = 2 }, 4) }) {
            using var image = AmbienceImage(frame);
            Check(image.GetPixel(120, 90) == Shaded(AmbienceBase, f), $"pixel vector level {frame.FrameLevel} mode {frame.TableMode} f {f}");
        }
        using (var full = AmbienceImage(night with { Level = 14 })) Check(ChangedPixels(full) == 0, "frame level 15 (L 14 + 1) leaves pixels untouched");
        var lit = new AmbienceLightMap(); lit.Stamp(400, 240, Enumerable.Repeat((byte)255, 40 * 20).ToArray(), 40, 20);
        using (var lamp = AmbienceImage(night, lit))
            Check(lamp.GetPixel(120, 90) == AmbienceBase && lamp.GetPixel(60, 40) == dark.GetPixel(60, 40), "light map value 255 leaves pixel unshaded, elsewhere unchanged");
        var half = new AmbienceLightMap(); half.Stamp(400, 240, Enumerable.Repeat((byte)128, 400 * 240).ToArray(), 400, 240);
        using (var mid = AmbienceImage(night, half)) Check(mid.GetPixel(120, 90) == Shaded(AmbienceBase, AmbienceCurve.Coefficient(0, 2, 128)) && AmbienceCurve.Coefficient(0, 2, 128) == 21, "light map value 128 uses table column 16");
        using (var translucent = new Bitmap(4, 4, PixelFormat.Format32bppArgb)) {
            translucent.SetPixel(1, 1, Color.FromArgb(128, 255, 255, 255));
            AmbiencePainter.Shade(translucent, new(0, 0, 4, 4), new(0, 0, 4, 4), night);
            Check(translucent.GetPixel(1, 1).A == 128, "alpha is preserved");
        }
        // AM§2(b): no rain/snow art yet (T167) — without a sprite source nothing is drawn, whatever the weather.
        var particles = new WeatherParticles(7);
        foreach (var (rainAmount, snowAmount) in new (byte, byte)[] { (1, 0), (2, 0), (3, 0), (0, 1), (0, 2), (0, 3), (2, 3) }) {
            var frame = day with { Rain = rainAmount, Snow = snowAmount };
            particles.Step(frame, 240);
            using (var none = AmbienceImage(frame, null, particles)) Check(ChangedPixels(none) == 0, $"no sprite source, no particles {rainAmount}/{snowAmount}");
            int drawn = 0; var kinds = new HashSet<WeatherKind>();
            using var withSprite = AmbienceImage(frame, null, particles, (g, particle, at) => { drawn++; kinds.Add(particle.Kind); g.FillRectangle(Brushes.White, at.X, at.Y, 2, 2); });
            Check(drawn == AmbiencePainter.ParticleCount(frame) && kinds.SetEquals([rainAmount > 0 ? WeatherKind.Rain : WeatherKind.Snow]), $"sprite source receives every active particle {rainAmount}/{snowAmount}");
            Check(ChangedPixels(withSprite) > 0 && withSprite.GetPixel(0, 0) == AmbienceBase && withSprite.GetPixel(239, 179) == AmbienceBase, $"synthetic sprite stays in viewport {rainAmount}/{snowAmount}");
        }
        using (var clear = AmbienceImage(day, null, particles, (g, _, at) => g.FillRectangle(Brushes.White, at.X, at.Y, 2, 2)))
            Check(ChangedPixels(clear) == 0, "clear weather draws nothing even with a sprite source");
        // Rain (synthetic white sprite) is drawn before shading, so it is darkened with the scene: white * 10/31.
        using (var wetNight = AmbienceImage(night with { Rain = 3 }, null, particles, (g, _, at) => g.FillRectangle(Brushes.White, at.X, at.Y, 2, 2))) {
            bool allDark = true, anyDrop = false; var darkWhite = Shaded(Color.White, 10);
            for (int y = 20; y < 160; y++) for (int x = 20; x < 220; x++) {
                var pixel = wetNight.GetPixel(x, y);
                if (pixel.R > 82) allDark = false;
                if (pixel == darkWhite) anyDrop = true;
            }
            Check(allDark && anyDrop && darkWhite.R == 82, "rain is drawn before shading, so it is darkened too");
        }
        foreach (var (amount, rainCount, snowCount) in new[] { (0, 0, 0), (1, 128, 48), (2, 256, 96), (3, 512, 144), (4, 0, 0) }) {
            Check(AmbienceWeather.RainCount(amount) == rainCount && AmbienceWeather.SnowCount(amount) == snowCount, "fixed particle counts " + amount);
            Check(AmbiencePainter.ParticleCount(day with { Rain = (byte)amount }) == rainCount && AmbiencePainter.ParticleCount(day with { Snow = (byte)amount }) == snowCount, "painter count " + amount);
        }
        Check(AmbiencePainter.ParticleCount(day with { Rain = 1, Snow = 3 }) == 128, "rain has priority over snow");
        WeatherParticleChecks();        using var clipImage = new Bitmap(240, 180, PixelFormat.Format32bppArgb); using var clipGraphics = Graphics.FromImage(clipImage);
        clipGraphics.Clear(AmbienceBase); clipGraphics.SetClip(new Rectangle(0, 0, 100, 100));
        var originalClip = clipGraphics.ClipBounds; var interpolation = clipGraphics.InterpolationMode;
        AmbiencePainter.Draw(clipGraphics, clipImage, new(20, 20, 200, 140), night);
        Check(clipImage.GetPixel(110, 90) == AmbienceBase && clipImage.GetPixel(50, 50) == Shaded(AmbienceBase, 10) && clipGraphics.ClipBounds == originalClip && clipGraphics.InterpolationMode == interpolation,
            "respects and restores caller graphics state");
    }

    static double CircularDistance(double a, double b) { double d = ((a - b) % 800 + 800) % 800; return Math.Min(d, 800 - d); }
    static void WeatherParticleChecks() {
        // AM§2(c) rain layers by index within each 256 group, and movement divisors (every 4/3/2/1 ticks for layer 3/2/1/0).
        foreach (var (index, layer) in new[] { (0, 3), (117, 3), (118, 2), (235, 2), (236, 1), (245, 1), (246, 0), (255, 0), (256, 3), (373, 3), (374, 2), (502, 0), (511, 0) })
            Check(WeatherParticles.RainLayer(index) == layer, $"rain layer of {index}");
        for (int layer = 0; layer < 4; layer++) Check(WeatherParticles.RainDivisor(layer) == layer + 1, "rain divisor " + layer);
        // AM§2(d) snow sizes and period field.
        foreach (var (index, size, period) in new[] { (0, 3, 50), (63, 3, 50), (64, 2, 60), (149, 2, 60), (150, 1, 70), (175, 1, 70), (176, 0, 80), (191, 0, 80) })
            Check(WeatherParticles.SnowSize(index) == size && WeatherParticles.SnowPeriod(index) == period, $"snow size/period of {index}");

        var rain = new AmbienceFrame(0, 0, 0) { Rain = 3 }; var snow = new AmbienceFrame(0, 0, 0) { Snow = 2 };
        var a = new WeatherParticles(5); var b = new WeatherParticles(5);
        var initial = a.Active(rain).ToArray();
        Check(initial.Length == 512 && initial.Select(p => p.Index).SequenceEqual(Enumerable.Range(0, 512)), "rain 3 uses the whole 512 pool in order");
        Check(new WeatherParticles(5).Active(rain with { Rain = 1 }).Select(p => p.Index).SequenceEqual(Enumerable.Range(0, 128)), "rain 1 uses the first 128");
        Check(initial.All(p => p.X >= 0 && p.X < 800 && p.Y >= 0 && p.Y < 485 && p.Stage == 0 && p.Layer == WeatherParticles.RainLayer(p.Index)), "rain initial positions in 800x485");
        Check(initial.Where(p => p.Layer == 3).All(p => p.Y <= 250) && initial.Where(p => p.Layer == 2).All(p => p.Y <= 449), "splash layers start above their landing range");
        Check(initial.SequenceEqual(b.Active(rain)), "same seed, same pool");
        // 24 ms ticks; frame time capped at 500 ms.
        a.Step(rain, 23); Check(a.Ticks == 0 && a.Active(rain).SequenceEqual(initial), "23 ms is below one tick");
        a.Step(rain, 1); Check(a.Ticks == 1, "24 ms makes one tick");
        a.Step(rain, 10_000); Check(a.Ticks == 1 + 20, "frame time capped at 500 ms (20 ticks)");
        a.Step(rain, 4); Check(a.Ticks == 22, "capped remainder carries over (20 + 4 ms)");
        // Per-layer speed over 12 ticks (288 ms): layer 0 → 12 px, 1 → 6, 2 → 4, 3 → 3 (while not landed).
        b.Step(rain, 288); var moved = b.Active(rain).ToArray();
        float sway = (float)(Math.Sin(12 * Math.PI / 144) * 8);
        foreach (var layer in new[] { 0, 1, 2, 3 }) {
            int step = 12 / (layer + 1);
            var falling = initial.Where(p => p.Layer == layer && (layer < 2 || p.Y + step <= p.Landing)).ToArray();
            Check(falling.Length > 0 && falling.All(p => moved[p.Index].Y == (p.Y + step) % 485 && moved[p.Index].Stage == 0), $"layer {layer} falls {step} px in 12 ticks");
            Check(initial.Where(p => p.Layer == layer).All(p => CircularDistance(moved[p.Index].X, p.X + sway) < 1e-3),
                $"layer {layer} x = spawn x + sin(ticks*pi/144)*8");
        }
        Check(Math.Abs(sway - 8 * Math.Sin(Math.PI / 12)) < 1e-6, "sway after 12 ticks");
        // Camera compensation adds to x (mod 800) without consuming ticks.
        var shifted = new WeatherParticles(5); shifted.Step(rain, 0, 30);
        Check(shifted.Ticks == 0 && shifted.Active(rain).Zip(initial).All(z => CircularDistance(z.First.X, z.Second.X + 30) < 1e-3 && z.First.Y == z.Second.Y), "camera shift moves rain x");
        // Long run: bounds, splash stages and respawn.
        var run = new WeatherParticles(9); int maxStage3 = 0, maxStage2 = 0; bool respawned = false; var previous = run.Active(rain).ToArray();
        double maxSway = 0;
        for (int frame = 0; frame < 600; frame++) {
            run.Step(rain, 500); var now = run.Active(rain).ToArray();
            maxSway = Math.Max(maxSway, Math.Abs(Math.Sin(run.Ticks * Math.PI / 144) * 8));
            Check(now.All(p => p.X >= 0 && p.X < 800 && p.Y >= 0 && p.Y < 485 && p.Y <= p.Landing && p.Stage < 5 * (4 - p.Layer)), "rain stays in 800x485, at or above its landing");
            maxStage3 = Math.Max(maxStage3, now.Where(p => p.Layer == 3).Max(p => p.Stage));
            maxStage2 = Math.Max(maxStage2, now.Where(p => p.Layer == 2).Max(p => p.Stage));
            if (now.Zip(previous).Any(z => z.First.Layer >= 2 && z.Second.Stage > 0 && z.First.Stage == 0 && z.First.Y < z.Second.Y)) respawned = true;
            Check(now.Where(p => p.Layer == 3).All(p => p.Y <= 250) && now.Where(p => p.Layer == 2).All(p => p.Y <= 449), "splashes stop at their landing row");
            Check(now.Where(p => p.Layer < 2).All(p => p.Stage == 0), "streak layers never land (landing 485, y wraps at 485)");
            previous = now;
        }
        Check(maxStage3 == 4 && maxStage2 == 9 && respawned, $"splash stage grows to 5*(4-layer)-1 then respawns (layer 3 {maxStage3}, layer 2 {maxStage2})");
        Check(maxSway <= 8 && maxSway > 7.9, "sway amplitude 8 px");
        var reset = new WeatherParticles(5); reset.Step(rain, 5000); reset.Reset();
        Check(reset.Ticks == 0 && reset.Active(rain).SequenceEqual(initial), "Reset re-initialises the pool from the seed");

        // AM§2(d) snow: once per frame, x -= rand % (3n) (x <= 0 → 800), y += 5n, new landing in [(4-size)*64, 505].
        var flakes = new WeatherParticles(11); var before = flakes.Active(snow).ToArray();
        Check(before.Length == 96 && before.Select(p => p.Index).SequenceEqual(Enumerable.Range(0, 96)) && before.All(p => p.Kind == WeatherKind.Snow && p.Layer == WeatherParticles.SnowSize(p.Index)), "snow 2 uses the first 96");
        Check(new WeatherParticles(11).Active(snow with { Snow = 3 }).Count() == 144 && new WeatherParticles(11).Active(snow with { Snow = 1 }).Count() == 48, "snow counts 48/144");
        flakes.Step(rain, 500); Check(flakes.Active(snow).SequenceEqual(before), "snow does not move while it rains");
        flakes.Step(snow, 0); var after = flakes.Active(snow).ToArray();
        Check(after.Zip(before).All(z => (z.First.Y == z.Second.Y + 10 || z.First.Y == 0) && (z.First.X == 800 || (int)(z.Second.X - z.First.X) is >= 0 and < 6)), "snow step: y + 5n, x - rand % 3n (n = 2)");
        var low = new WeatherParticles(11); low.Step(snow with { Snow = 1 }, 0);
        Check(low.Active(snow with { Snow = 1 }).Zip(new WeatherParticles(11).Active(snow with { Snow = 1 })).All(z => z.First.Y == z.Second.Y + 5 || z.First.Y == 0), "snow n = 1 moves 5 px");
        bool wrappedX = false, wrappedY = false; int maxY = 0;
        for (int frame = 0; frame < 2000; frame++) {
            var last = flakes.Active(snow).ToArray(); flakes.Step(snow, 16); var next = flakes.Active(snow).ToArray();
            if (next.Zip(last).Any(z => z.First.X == 800 && z.Second.X <= 5)) wrappedX = true;
            if (next.Zip(last).Any(z => z.First.Y == 0 && z.Second.Y > 0)) wrappedY = true;
            maxY = Math.Max(maxY, (int)next.Max(p => p.Y));
            Check(next.All(p => p.X > 0 && p.X <= 800 && p.Y >= 0 && p.Y < p.Landing && p.Landing >= (4 - p.Layer) * 64 && p.Landing <= 505), "snow stays in (0,800] and above a landing in [(4-size)*64, 505]");
        }
        Check(wrappedX && wrappedY && maxY >= 400, "snow wraps left edge to 800 and respawns at y 0 after its landing");
        var empty = new WeatherParticles(); Check(!empty.Active(new AmbienceFrame(0, 0, 0)).Any(), "clear weather has no active particles");
    }
}