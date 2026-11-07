using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace P06;

public enum WeatherKind { Clear, Rain, Snow }
public sealed record AmbienceFrame(uint? Seconds, byte Weather, long AnimationMs) {
    public WeatherKind Kind => Weather switch { >= 1 and <= 3 => WeatherKind.Rain, >= 17 and <= 19 => WeatherKind.Snow, _ => WeatherKind.Clear };
    public int Intensity => Kind == WeatherKind.Clear ? 0 : Weather & 15;
    public TimeSpan? TimeOfDay => Seconds is uint seconds ? TimeSpan.FromSeconds(seconds % 86400) : null;
    public bool Night => Seconds is uint seconds && (seconds % 86400 < 21600 || seconds % 86400 >= 64800);
    /// <summary>Global level L (0–15, 15 = brightest); recomputed only on S123, S_STATUS time and map change.</summary>
    public byte Level { get; init; } = 15;
    public ushort? MapId { get; init; }
    /// <summary>Own S40 flag: 19 (+1) or 20 (+5); 0 when none.</summary>
    public byte OwnLightFlag { get; init; }
    /// <summary>TW16 byte_1542AA1 ∈ {2,4} (+2). The packet that sets this byte is not identified; the session never sets it.</summary>
    public bool NightVision { get; init; }
    /// <summary>Coefficient table mode 0–2 (AM§1(h)). The session uses 0; modes 1/2 depend on unverified flags.</summary>
    public byte TableMode { get; init; }
    /// <summary>AM§1(i): shading runs only while L &lt; 15 (blind mode B is a separate task).</summary>
    public bool Shaded => Level < 15;
    public byte FrameLevel => AmbienceCurve.FrameLevel(Level, MapId, OwnLightFlag, NightVision);
}
/// <summary>A light prepared for the (not yet available) TW16 light map: Source is the source.lmp index k (0–15).</summary>
public sealed record AmbienceLight(uint ObjectId, PointF Center, byte Source);

/// <summary>TW16 day/night rules (docs/reference/ambience-and-map-events.md §1, §3). Pure functions.</summary>
public static class AmbienceCurve {
    /// <summary>sub_EDB7D0: minute-of-day curve 1–15; −1 when ≥ 10 and raining or snowing.</summary>
    public static byte Level(uint seconds, bool precipitation) {
        // gmtime32: values ≥ 0x80000000 first subtract 2019686400 (exactly 23376 days), so time of day is seconds % 86400.
        int m = (int)(seconds % 86400 / 60);
        int level = m <= 360 || m >= 1240 ? 1 : m < 400 ? 15 * (m - 360) / 40 + 1 : m <= 1200 ? 15 : 15 * (1240 - m) / 40 + 1;
        level = Math.Min(level, 15);
        if (level >= 10 && precipitation) level--;
        return (byte)level;
    }
    /// <summary>sub_EFE110 current-map overrides. The byte_15442BC table and dword_138BE64 checks are unverified and not applied.</summary>
    public static byte? MapOverride(ushort? map) => map switch {
        1000 => 6, 2006 => 2, 1931 or 1932 => 15, >= 4995 and <= 5122 => 8, >= 16384 => 8, _ => null };
    /// <summary>sub_EFE110 then sub_F11FA0: base (map or L), +1 if &lt; 15, own flag 19 +1 / 20 +5, clamp 0–15, night vision +2 (cap 15).</summary>
    public static byte FrameLevel(byte level, ushort? map, byte ownLightFlag, bool nightVision) {
        int value = MapOverride(map) ?? level;
        if (value < 15) value++;
        if (ownLightFlag == 19) value += 1; else if (ownLightFlag == 20) value += 5;
        value = Math.Clamp(value, 0, 15);
        if (nightVision) value = Math.Min(15, value + 2);
        return (byte)value;
    }
    /// <summary>sub_F07210 own: &lt; 5 → 5, 19 → 10 (flag 19), 20 → 14 (flag 20), otherwise unchanged.</summary>
    public static (byte Value, byte Flag) OwnLight(byte value) => value switch {
        < 5 => (5, 0), 19 => (10, 19), 20 => (14, 20), _ => (value, 0) };
    /// <summary>sub_F07210 others: 20 → 5, otherwise unchanged.</summary>
    public static byte OtherLight(byte value) => value == 20 ? (byte)5 : value;
    /// <summary>sub_F11FA0 (B == 0): light-map index k = min(15, value).</summary>
    public static byte SourceIndex(byte value) => Math.Min(value, (byte)15);
    /// <summary>sub_F11FA0: no further sources once the sum of k reaches 700.</summary>
    public const int SourceBudget = 700;

    /// <summary>Coefficient f (0–31) for a level, table mode and light-map value (index = light &gt;&gt; 3).</summary>
    public static byte Coefficient(int mode, int level, byte light) =>
        Table[512 * Math.Clamp(mode, 0, 2) + 32 * Math.Clamp(level, 0, 15) + (light >> 3)];
    /// <summary>sub_9786F0 per 5-bit channel: c' = c*f/31 (word_14325C0 = i*j/31); f == 31 leaves the pixel untouched.</summary>
    public static int Channel5(int c5, int f) => c5 * f / 31;
    /// <summary>8-bit pixel channel: reduce to 5 bits (RGB555), apply the table, expand back.</summary>
    public static byte Channel8(byte c, int f) { if (f >= 31) return c; int v = Channel5(c >> 3, f); return (byte)(v << 3 | v >> 2); }

    // TW16 .data VA 0x1374168 (FO 0xF72968): 3 modes × 16 levels × 32 coefficients; 31 = unchanged.
    // BIN SHA-256 e12949cf8912c5d62ef269ba02ca37eb5cabf8f379c3d5e10189279bf09641a2, dumped read-only with
    // docs/reference/rea-scripts/ambience_tables.py. Modes 0/1 byte-identical in TW13081901 (AM§6).
    static readonly byte[] Table = [
        // mode 0
        0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,
        4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,28,29,29,30,30,31,31,
        10,10,11,12,12,13,14,14,15,16,16,17,18,18,19,20,21,21,22,23,23,24,25,25,26,27,27,28,29,29,30,31,
        11,11,12,12,13,14,14,15,16,16,17,18,18,19,20,20,21,22,22,23,24,24,25,26,26,27,28,28,29,30,30,31,
        13,13,14,14,15,15,16,17,17,18,18,19,20,20,21,21,22,23,23,24,24,25,26,26,27,27,28,29,29,30,30,31,
        14,14,15,15,16,16,17,17,18,19,19,20,20,21,21,22,23,23,24,24,25,25,26,26,27,28,28,29,29,30,30,31,
        16,16,17,17,18,18,19,19,20,20,21,21,22,22,23,23,24,24,25,25,26,26,27,27,28,28,29,29,30,30,31,31,
        18,18,18,19,19,20,20,21,21,21,22,22,23,23,24,24,25,25,25,26,26,27,27,28,28,28,29,29,30,30,31,31,
        19,19,19,20,20,21,21,21,22,22,23,23,23,24,24,25,25,25,26,26,27,27,27,28,28,29,29,29,30,30,31,31,
        21,21,21,22,22,22,23,23,23,24,24,24,25,25,25,26,26,26,27,27,27,28,28,28,29,29,29,30,30,30,31,31,
        23,23,23,23,24,24,24,24,25,25,25,26,26,26,26,27,27,27,28,28,28,28,29,29,29,30,30,30,30,31,31,31,
        24,24,24,24,25,25,25,25,26,26,26,26,27,27,27,27,28,28,28,28,29,29,29,29,30,30,30,30,31,31,31,31,
        26,26,26,26,26,26,27,27,27,27,27,28,28,28,28,28,29,29,29,29,29,29,30,30,30,30,30,31,31,31,31,31,
        28,28,28,28,28,28,28,28,29,29,29,29,29,29,29,29,30,30,30,30,30,30,30,30,31,31,31,31,31,31,31,31,
        29,29,29,29,29,29,29,29,29,29,29,30,30,30,30,30,30,30,30,30,30,30,31,31,31,31,31,31,31,31,31,31,
        31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        // mode 1
        0,1,2,4,5,7,8,10,11,13,14,16,17,19,20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,
        4,5,7,8,10,11,13,14,16,17,19,20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,
        8,10,11,13,14,16,17,19,20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        10,11,13,14,16,17,19,20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        12,13,14,16,17,19,20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        14,16,17,19,20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        16,17,19,20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        18,19,20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        20,22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        22,23,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        24,25,26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        26,28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        28,29,30,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
        // mode 2
        0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,
        2,3,4,5,6,7,8,9,10,11,11,12,13,14,15,16,17,18,19,20,21,21,22,23,24,25,26,27,28,29,30,31,
        4,5,6,7,8,8,9,10,11,12,13,13,14,15,16,17,18,19,20,20,21,22,23,24,25,26,26,27,28,29,30,31,
        6,7,8,8,9,10,11,12,12,13,14,15,16,16,17,18,19,20,20,21,22,23,24,24,25,26,27,28,28,29,30,31,
        8,8,9,10,11,12,12,13,14,14,15,16,16,17,18,19,20,20,21,22,23,24,24,25,26,26,27,28,28,29,30,31,
        10,10,11,12,12,13,14,14,15,16,16,17,18,18,19,20,20,21,22,22,23,24,24,25,26,26,27,28,28,29,30,31,
        12,12,13,13,14,14,15,16,16,17,18,18,19,20,20,21,21,22,22,23,24,24,25,26,26,27,28,28,29,30,30,31,
        14,14,15,15,16,16,17,17,18,18,19,19,20,20,21,21,22,22,23,23,24,24,25,26,26,27,28,28,29,30,30,31,
        16,16,17,17,18,18,19,19,20,20,21,21,22,22,23,23,24,24,25,25,26,26,27,27,28,28,29,29,30,30,31,31,
        18,18,19,19,19,20,20,21,21,22,22,22,23,23,24,24,25,25,25,26,26,27,27,28,28,28,29,29,30,30,31,31,
        20,20,21,21,21,22,22,22,23,23,23,24,24,24,25,25,25,26,26,26,27,27,28,28,28,29,29,30,30,30,31,31,
        22,22,22,23,23,23,23,24,24,24,25,25,25,26,26,26,27,27,27,28,28,28,28,29,29,29,30,30,30,31,31,31,
        24,24,24,24,25,25,25,25,26,26,26,26,27,27,27,27,28,28,28,28,29,29,29,29,30,30,30,30,31,31,31,31,
        26,26,26,26,26,26,27,27,27,27,27,27,28,28,28,28,28,29,29,29,29,29,30,30,30,30,30,31,31,31,31,31,
        28,28,28,28,28,28,28,28,28,29,29,29,29,29,29,29,29,29,30,30,30,30,30,30,30,30,31,31,31,31,31,31,
        31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,31,
    ];
}

/// <summary>Visual state only; never writes ServerTime or the ship clock, and never advances the clock locally.</summary>
public sealed class Ambience {
    readonly Func<uint?> statusTime;
    readonly Func<ushort?> mapId;
    uint? seconds, statusSeen;
    ushort? mapSeen;
    byte weather, level = 15, ownLightFlag, flashSaved;
    int flashes;
    public Ambience() : this(() => null, () => null) { }
    internal Ambience(Func<uint?> statusTime, Func<ushort?> mapId) { this.statusTime = statusTime; this.mapId = mapId; }

    internal void TimeReceived(uint value, long now) { statusSeen = statusTime(); seconds = value; Recalculate(); }
    internal void WeatherReceived(byte value) => weather = value;
    internal void OwnLightReceived(byte flag) => ownLightFlag = flag;
    internal void Clear() { seconds = statusSeen = null; mapSeen = null; weather = ownLightFlag = flashSaved = 0; level = 15; flashes = 0; }
    /// <summary>AM§1(g): first light effect raises L by 5 (cap 15); the last one restores L. Curve writes are suspended meanwhile.</summary>
    public void BeginLightEffect() { if (flashes++ == 0) { flashSaved = level; level = (byte)Math.Min(15, level + 5); } }
    public void EndLightEffect() { if (flashes > 0 && --flashes == 0) level = flashSaved; }

    // S_STATUS (S8) and S206 do not notify this module; their arrival is observed as a change of the
    // session's Status.Time / MapId. An S8 that repeats the previous status time is therefore not re-applied.
    void Observe() {
        var status = statusTime();
        if (status != statusSeen) { statusSeen = status; if (status is uint t) { seconds = t; Recalculate(); } }
        var map = mapId();
        if (map != mapSeen) { mapSeen = map; if (map != null) Recalculate(); }
    }
    // sub_EDC880: write L only while no light effect is active (byte_15442BC assumed 0).
    void Recalculate() {
        if (seconds is not uint s || flashes > 0) return;
        level = AmbienceCurve.Level(s, new AmbienceFrame(s, weather, 0).Intensity > 0);
    }
    public AmbienceFrame At(long now) {
        Observe();
        return new(seconds, weather, now) { Level = seconds == null ? (byte)15 : level, MapId = mapSeen, OwnLightFlag = ownLightFlag };
    }
}

public sealed partial class WorldSession {
    public Ambience Ambience { get; }
    public WorldSession() {
        Ambience = new(() => Status?.Time, () => MapId);
        // Reset() clears Entered/ServerTime before ClearObjects -> ResetOpacity.
        // Reuse that existing lifecycle notification, without changing common dispatch/reset code.
        // A map reset retains the global weather/clock; its objects already own their light values.
        OpacityObserved += _ => { if (!Entered && ServerTime == null) Ambience.Clear(); };
    }
    void ReceiveWeather(LoginReader reader) {
        byte value = reader.C(); reader.Finish();
        Ambience.WeatherReceived(value);
    }
    void ReceiveLight(LoginReader reader) {
        uint id = reader.D(); byte value = reader.C(); reader.Finish();
        Ambience.OwnLightReceived(0); // AM§3(a): every S40 clears both own flags first.
        if (Actor is { } own && own.ObjectId == id) {
            var (light, flag) = AmbienceCurve.OwnLight(value);
            Actor = own with { Light = light }; Ambience.OwnLightReceived(flag);
        }
        else if (objects.TryGetValue(id, out var other)) objects[id] = other with { Light = AmbienceCurve.OtherLight(value) };
        // Do not remember unknown IDs: a later S87 is a new incarnation with its own Light.
    }
    /// <summary>Light sources in display coordinates, own first, bounded by the TW16 budget (sum of k ≥ 700 stops).
    /// The ±35/±15 distance filter and action offsets of sub_F11FA0 are not applied (field meaning unverified).</summary>
    public AmbienceLight[] AmbienceLights(Func<PointF, PointF> toView, ObjectVisual? ownVisual, IReadOnlyList<ObjectVisual> visuals) {
        var result = new List<AmbienceLight>(); int budget = 0;
        void Add(uint id, PointF foot, byte value) {
            if (budget >= AmbienceCurve.SourceBudget) return;
            byte k = AmbienceCurve.SourceIndex(value); budget += k; result.Add(new(id, toView(foot), k));
        }
        if (Actor is { Light: > 0 } own && ownVisual?.ObjectId == own.ObjectId && !SelfAppearancePending) Add(own.ObjectId, ownVisual.Foot, own.Light);
        foreach (var visual in visuals) {
            if (visual.Bounds.IsEmpty || visual.Action == "ground") continue;
            if (objects.TryGetValue(visual.ObjectId, out var other) && other.Light > 0 && !other.AppearancePending) Add(other.ObjectId, visual.Foot, other.Light);
        }
        return result.ToArray();
    }
}

/// <summary>Half-resolution light map (TW16 400×240 for an 800×480 view). Empty until source.lmp is available (T167).</summary>
public sealed class AmbienceLightMap {
    public const int Width = 400, Height = 240;
    readonly byte[] values = new byte[Width * Height];
    public byte this[int x, int y] => (uint)x < Width && (uint)y < Height ? values[y * Width + x] : (byte)0;
    public void Clear() => Array.Clear(values);
    /// <summary>sub_97D2F0: stamp a w×h image centred at full-resolution (x, y): left ((x−w)&gt;&gt;1)&amp;~3, top (y−h)&gt;&gt;1; overlaps keep the maximum.</summary>
    public void Stamp(int x, int y, ReadOnlySpan<byte> image, int width, int height) {
        if (width <= 0 || height <= 0 || image.Length < width * height) return;
        int left = ((x - width) >> 1) & ~3, top = (y - height) >> 1;
        for (int j = 0; j < height; j++) {
            int my = top + j; if ((uint)my >= Height) continue;
            for (int i = 0; i < width; i++) {
                int mx = left + i; if ((uint)mx >= Width) continue;
                ref byte cell = ref values[my * Width + mx]; cell = Math.Max(cell, image[j * width + i]);
            }
        }
    }
}

/// <summary>Weather particles and TW16 coefficient shading. No game assets; ownership/picking buffers are untouched.</summary>
public static class AmbiencePainter {
    public static int ParticleCount(AmbienceFrame frame, Rectangle viewport) =>
        Math.Min(768, (int)Math.Ceiling((double)viewport.Width * viewport.Height / 7000 * frame.Intensity));

    /// <summary>Weather first, then shading of the target pixels (AM§2(b): rain/snow are darkened with the scene).</summary>
    public static void Draw(Graphics graphics, Bitmap target, Rectangle viewport, AmbienceFrame frame, AmbienceLightMap? lightMap = null) {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        var saved = graphics.Save();
        try {
            graphics.SetClip(viewport, CombineMode.Intersect);
            DrawWeather(graphics, viewport, frame);
            if (frame.Shaded) {
                graphics.Flush(FlushIntention.Sync);
                // Shading writes pixels directly; honour the caller's rectangular clip (identity transform only).
                var area = viewport;
                if (graphics.Transform.IsIdentity) area.Intersect(Rectangle.Round(graphics.VisibleClipBounds));
                Shade(target, viewport, area, frame, lightMap);
            }
        } finally { graphics.Restore(saved); }
    }

    /// <summary>Per-pixel f = table[mode][FrameLevel][light &gt;&gt; 3]; each channel c' = c*f/31 at RGB555 precision. Alpha is kept.</summary>
    public static void Shade(Bitmap target, Rectangle viewport, Rectangle area, AmbienceFrame frame, AmbienceLightMap? lightMap = null) {
        area.Intersect(viewport); area.Intersect(new Rectangle(0, 0, target.Width, target.Height));
        if (area.Width <= 0 || area.Height <= 0 || !frame.Shaded) return;
        int level = frame.FrameLevel, mode = frame.TableMode;
        Span<byte> row = stackalloc byte[32];
        for (int i = 0; i < 32; i++) row[i] = AmbienceCurve.Coefficient(mode, level, (byte)(i << 3));
        if (lightMap == null && row[0] >= 31) return;
        var bits = target.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try {
            var pixels = new int[area.Width];
            for (int y = 0; y < area.Height; y++) {
                IntPtr line = bits.Scan0 + y * bits.Stride;
                Marshal.Copy(line, pixels, 0, pixels.Length);
                int my = (area.Y + y - viewport.Y) * AmbienceLightMap.Height / viewport.Height;
                for (int x = 0; x < pixels.Length; x++) {
                    byte light = lightMap == null ? (byte)0 : lightMap[(area.X + x - viewport.X) * AmbienceLightMap.Width / viewport.Width, my];
                    int f = row[light >> 3]; if (f >= 31) continue;
                    int p = pixels[x];
                    pixels[x] = (int)((uint)p & 0xFF000000) | AmbienceCurve.Channel8((byte)(p >> 16), f) << 16
                        | AmbienceCurve.Channel8((byte)(p >> 8), f) << 8 | AmbienceCurve.Channel8((byte)p, f);
                }
                Marshal.Copy(pixels, 0, line, pixels.Length);
            }
        } finally { target.UnlockBits(bits); }
    }

    static void DrawWeather(Graphics graphics, Rectangle viewport, AmbienceFrame frame) {
        int count = ParticleCount(frame, viewport);
        if (count == 0) return;
        bool rain = frame.Kind == WeatherKind.Rain;
        using var pen = new Pen(Color.FromArgb(155, 183, 209, 235), 1);
        using var snow = new SolidBrush(Color.FromArgb(215, 240, 246, 255));
        double seconds = frame.AnimationMs / 1000d;
        for (int i = 0; i < count; i++) {
            uint seed = unchecked((uint)(i + 1) * 2654435761u);
            double phase = (seed & 65535) / 65536d, drift = (seed >> 16) / 65536d;
            double speed = rain ? 310 + 35 * frame.Intensity : 25 + 8 * frame.Intensity;
            float x = viewport.Left - 16 + (float)Wrap(phase * (viewport.Width + 32) + seconds * (rain ? -100 : 12) + (rain ? 0 : 10 * Math.Sin(seconds + drift * 6)), viewport.Width + 32);
            float y = viewport.Top - 16 + (float)Wrap(drift * (viewport.Height + 32) + seconds * speed, viewport.Height + 32);
            if (rain) graphics.DrawLine(pen, x, y, x - 4, y + 12);
            else graphics.FillEllipse(snow, x, y, 2 + seed % 2, 2 + seed % 2);
        }
    }
    static double Wrap(double value, double period) => (value % period + period) % period;
}
