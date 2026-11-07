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
    static Bitmap AmbienceImage(AmbienceFrame frame, AmbienceLightMap? map = null) {
        var bitmap = new Bitmap(240, 180, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(AmbienceBase);
        AmbiencePainter.Draw(graphics, bitmap, new(20, 20, 200, 140), frame, map);
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
        // Rain at recompute time: only S123/S_STATUS/map change apply it (weather packet alone does not).
        Feed(c, TimePacket(Clock(12, 0))); Feed(c, WeatherPacket(2));
        Check(world.Ambience.At(now).Level == 15, "weather alone does not recompute");
        Feed(c, TimePacket(Clock(12, 0))); Check(world.Ambience.At(now).Level == 14 && world.Ambience.At(now).FrameLevel == 15, "rain -1 at recompute, offset by frame +1");
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
            Check(frame.Weather == code && frame.Kind == expected && frame.Intensity == (expected == WeatherKind.Clear ? 0 : code & 15), "S115 value " + code);
        }
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
        foreach (byte weather in new byte[] { 1, 2, 3, 17, 18, 19 }) {
            var frame = day with { Weather = weather };
            using var first = AmbienceImage(frame); using var later = AmbienceImage(frame with { AnimationMs = 10300 });
            Check(ChangedPixels(first) > 0 && !SamePixels(first, later), "animated weather pixels " + weather);
            Check(first.GetPixel(0, 0) == AmbienceBase, "weather respects viewport " + weather);
        }
        using (var wetNight = AmbienceImage(night with { Weather = 3 })) {
            bool allDark = true;
            for (int y = 20; y < 160; y++) for (int x = 20; x < 220; x++) if (wetNight.GetPixel(x, y).R > 82 + 4) allDark = false;
            Check(allDark, "rain is drawn before shading, so it is darkened too");
        }
        Check(AmbiencePainter.ParticleCount(day with { Weather = 1 }, new(0, 0, 640, 480)) < AmbiencePainter.ParticleCount(day with { Weather = 3 }, new(0, 0, 640, 480)), "intensity increases particle count");
        Check(AmbiencePainter.ParticleCount(day with { Weather = 3 }, new(0, 0, 7680, 4320)) == 768, "4K/8K weather work is bounded");
        using var unknown = AmbienceImage(day with { Weather = 255 }); Check(SamePixels(daylight, unknown), "unknown weather has no invented particles");
        using var clipImage = new Bitmap(240, 180, PixelFormat.Format32bppArgb); using var clipGraphics = Graphics.FromImage(clipImage);
        clipGraphics.Clear(AmbienceBase); clipGraphics.SetClip(new Rectangle(0, 0, 100, 100));
        var originalClip = clipGraphics.ClipBounds; var interpolation = clipGraphics.InterpolationMode;
        AmbiencePainter.Draw(clipGraphics, clipImage, new(20, 20, 200, 140), night);
        Check(clipImage.GetPixel(110, 90) == AmbienceBase && clipImage.GetPixel(50, 50) == Shaded(AmbienceBase, 10) && clipGraphics.ClipBounds == originalClip && clipGraphics.InterpolationMode == interpolation,
            "respects and restores caller graphics state");
    }
}
