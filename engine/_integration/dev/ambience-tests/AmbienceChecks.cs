using P06;
using System.Drawing;

partial class Checks {
    static byte[] WeatherPacket(byte value) => Packet(b => { b.Write((byte)115); b.Write(value); });
    static byte[] LightPacket(uint id, byte value) => Packet(b => { b.Write((byte)40); b.Write(id); b.Write(value); });
    static void AmbienceReject(LoginConnection c, byte[] packet, string label) {
        bool rejected = false;
        try { Feed(c, packet); } catch (ProtocolException) { rejected = true; }
        Check(rejected, "ambience rejects " + label);
    }
    static ObjectVisual Lamp(uint id, PointF point) => new(id, 0, 0, "synthetic", 0, 0, "breath", "", point, new(0, 0, 20, 30), false, 0);
    static Bitmap AmbienceImage(AmbienceFrame frame, params AmbienceLight[] lights) {
        var bitmap = new Bitmap(240, 180);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        AmbiencePainter.Draw(graphics, new(20, 20, 200, 140), frame, lights);
        return bitmap;
    }
    static int ChangedPixels(Bitmap bitmap) {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
            if (bitmap.GetPixel(x, y).ToArgb() != Color.White.ToArgb()) count++;
        return count;
    }
    static bool SamePixels(Bitmap a, Bitmap b) {
        for (int y = 0; y < a.Height; y++) for (int x = 0; x < a.Width; x++)
            if (a.GetPixel(x, y) != b.GetPixel(x, y)) return false;
        return true;
    }
    static void AmbienceChecks() {
        using var c = Seed(); var world = c.World; long now = 10000;
        Set(world, "SceneClock", (Func<long>)(() => now));
        Set(world, "ShipClock", (Func<long>)(() => now));
        Check(world.Ambience.At(now).Seconds == null && world.Ambience.At(now).Darkness == 0, "no S123 means no invented night");
        foreach (uint time in new uint[] { 0, 18000, 19800, 21600, 43200, 64799, 64800, 66600, 68400, 86399, 86400, uint.MaxValue }) {
            Feed(c, TimePacket(time)); var frame = world.Ambience.At(now);
            Check(frame.Seconds == time % 86400 && world.ServerTime == time, "S123 unsigned LE seconds " + time);
            Check(frame.Night == (time % 86400 < 21600 || time % 86400 >= 64800), "server day/night boundary " + time);
        }
        Feed(c, TimePacket(86397)); now += 500;
        Check(world.Ambience.At(now).Seconds == 0 && world.ServerTime == 86397, "sixfold visual midnight wrap leaves authoritative ServerTime intact");
        Check(world.Ambience.At(now - 1000).Seconds == 86397, "monotonic clock reversal cannot run visual clock backwards");
        Feed(c, TimePacket(43200));
        Check(world.Ambience.At(now).Darkness == 0, "noon is unmodified");
        Feed(c, TimePacket(19800)); Check(world.Ambience.At(now).Darkness == 70, "dawn gradual brightness");
        Feed(c, TimePacket(66600)); Check(world.Ambience.At(now).Darkness == 70, "dusk gradual brightness");
        int replies = world.TimeReplies, revision = world.ShipClockRevision;
        world.ProbeSent = true; now += 500; Feed(c, TimePacket(66603));
        Check(world.TimeReplies == replies + 1 && world.ShipClockRevision == revision + 1 && world.ShipServerSeconds == 66603,
            "S123 still advances ready probe and ship sample exactly once");
        now += 500; _ = world.Ambience.At(now);
        Check(world.ServerTime == 66603 && world.ShipServerSeconds == 66603, "visual interpolation cannot alter boat timetable");
        foreach (int code in Enumerable.Range(0, 256)) {
            Feed(c, WeatherPacket((byte)code)); var frame = world.Ambience.At(now);
            var expected = code is >= 1 and <= 3 ? WeatherKind.Rain : code is >= 17 and <= 19 ? WeatherKind.Snow : WeatherKind.Clear;
            Check(frame.Weather == code && frame.Kind == expected && frame.Intensity == (expected == WeatherKind.Clear ? 0 : code & 15), "S115 value " + code);
        }
        Feed(c, WeatherPacket(3)); Feed(c, LightPacket(123, 14)); Feed(c, LightPacket(456, 5));
        Check(world.Actor!.Light == 14 && world.Objects.Single(o => o.ObjectId == 456).Light == 5, "S40 own and peer light");
        foreach (var good in new[] { WeatherPacket(1), LightPacket(123, 2), TimePacket(1) }) {
            for (int length = 0; length < good.Length; length++)
                AmbienceReject(c, good[..length], "opcode " + good[0] + " truncated " + length);
            var padding = (byte[])good.Clone(); padding[^1] = 1;
            AmbienceReject(c, padding, "opcode " + good[0] + " nonzero padding");
            AmbienceReject(c, [.. good, .. new byte[8]], "opcode " + good[0] + " extra block");
        }
        Check(world.Ambience.At(now).Weather == 3 && world.Actor!.Light == 14, "malformed packets do not mutate weather/light");
        Check(world.Ambience.At(now).Seconds == 66606, "malformed S123 does not notify visual clock");
        var peer = Lamp(456, new(50, 60)); var own = Lamp(123, new(100, 110));
        var lamps = world.AmbienceLights(p => new(p.X * 2 + 5, p.Y * 2 + 7), 2, own, [peer]);
        Check(lamps.Length == 2 && lamps[0].Center == new PointF(205, 227) && lamps[1].Center == new PointF(105, 127), "lights follow displayed feet and camera projection");
        Check(lamps[0].Radius == 14 * 48 && lamps[1].Radius == 5 * 48, "light radius follows zoom");
        Check(world.AmbienceLights(p => p, 1, null, [peer with { Bounds = Rectangle.Empty }]).Length == 0, "unrendered actors cannot create phantom lamps");
        Feed(c, LightPacket(456, 0)); Check(world.AmbienceLights(p => p, 1, own, [peer]).Length == 1, "zero extinguishes light");
        Feed(c, LightPacket(uint.MaxValue, 255)); Feed(c, Pack(uint.MaxValue));
        Check(world.Objects.Single(o => o.ObjectId == uint.MaxValue).Light == 0, "unknown high ID cannot leak light into later S87");
        Feed(c, LightPacket(456, 255)); Check(world.Objects.Single(o => o.ObjectId == 456).Light == 255, "full byte radius retained");
        Feed(c, Packet(b => { b.Write((byte)120); b.Write(456u); }));
        Check(world.AmbienceLights(p => p, 1, null, [peer]).Length == 0, "S120 removes lamp even with stale render list");
        Feed(c, Pack(456)); Check(world.Objects.Single(o => o.ObjectId == 456).Light == 0, "object ID reuse inherits fresh S87 light");
        var selfPack = Pack(123); selfPack[13] = 7; Feed(c, selfPack);
        Check(world.Actor!.Light == 7, "S87 light works without prior S40");
        Feed(c, TimePacket(0)); Feed(c, MapPacket(4));
        Check(world.Ambience.At(now).Weather == 3 && world.Ambience.At(now).Seconds == 0, "global weather and time survive S206 map change");
        Check(world.AmbienceLights(p => p, 1, own, [peer]).Length == 0, "map clears own and peer lights");
        world.Reset(); Check(world.Ambience.At(now).Seconds == null && world.Ambience.At(now).Weather == 0, "logout immediately clears clock and weather");
        Feed(c, TimePacket(43200)); Check(world.Ambience.At(now).Weather == 0, "new session time cannot revive previous weather");

        var day = new AmbienceFrame(43200, 0, 10000); var night = day with { Seconds = 0 };
        using var daylight = AmbienceImage(day); using var dark = AmbienceImage(night);
        Check(ChangedPixels(daylight) == 0 && dark.GetPixel(120, 90).R < 150, "actual GDI overlay changes night pixels only");
        Check(dark.GetPixel(0, 0).ToArgb() == Color.White.ToArgb() && dark.GetPixel(225, 165).ToArgb() == Color.White.ToArgb(), "overlay excludes HUD outside viewport");
        var lamp = new AmbienceLight(123, new(120, 90), 60);
        using var lit = AmbienceImage(night, lamp);
        Check(lit.GetPixel(120, 90).R > 245 && lit.GetPixel(155, 90).R > dark.GetPixel(155, 90).R && lit.GetPixel(205, 90) == dark.GetPixel(205, 90), "lamp bright core, soft edge, finite extent");
        using var dayLamp = AmbienceImage(day, lamp); Check(SamePixels(daylight, dayLamp), "daylight ignores lamps");
        var second = new AmbienceLight(456, new(145, 95), 45);
        using var overlapA = AmbienceImage(night, lamp, second); using var overlapB = AmbienceImage(night, second, lamp);
        Check(SamePixels(overlapA, overlapB), "overlapping lamps are order independent");
        using var offscreen = AmbienceImage(night, lamp with { Center = new(-1000, -1000) });
        Check(SamePixels(dark, offscreen), "offscreen light is culled");
        foreach (byte weather in new byte[] { 1, 2, 3, 17, 18, 19 }) {
            var frame = day with { Weather = weather };
            using var first = AmbienceImage(frame); using var later = AmbienceImage(frame with { AnimationMs = 10300 });
            Check(ChangedPixels(first) > 0 && !SamePixels(first, later), "animated weather pixels " + weather);
            Check(first.GetPixel(0, 0).ToArgb() == Color.White.ToArgb(), "weather respects viewport " + weather);
        }
        Check(AmbiencePainter.ParticleCount(day with { Weather = 1 }, new(0, 0, 640, 480)) < AmbiencePainter.ParticleCount(day with { Weather = 3 }, new(0, 0, 640, 480)), "intensity increases particle count");
        Check(AmbiencePainter.ParticleCount(day with { Weather = 3 }, new(0, 0, 7680, 4320)) == 768, "4K/8K weather work is bounded");
        using var unknown = AmbienceImage(day with { Weather = 255 }); Check(SamePixels(daylight, unknown), "unknown weather has no invented particles");
        using var clipImage = new Bitmap(240, 180); using var clipGraphics = Graphics.FromImage(clipImage);
        clipGraphics.Clear(Color.White); clipGraphics.SetClip(new Rectangle(0, 0, 100, 100));
        var originalClip = clipGraphics.ClipBounds; var interpolation = clipGraphics.InterpolationMode;
        AmbiencePainter.Draw(clipGraphics, new(20, 20, 200, 140), night, []);
        Check(clipImage.GetPixel(110, 90).ToArgb() == Color.White.ToArgb() && clipGraphics.ClipBounds == originalClip && clipGraphics.InterpolationMode == interpolation, "respects and restores caller graphics state");
    }
}
