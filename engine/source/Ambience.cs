using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace P06;

public enum WeatherKind { Clear, Rain, Snow }
/// <summary>Weather is the last raw S115 byte (diagnostic only); Rain and Snow are the TW16 amounts
/// (dword_15442B0 / dword_15442B4, 0–3) after the map and gfx rules. Rain takes precedence when drawing.</summary>
public sealed record AmbienceFrame(uint? Seconds, byte Weather, long AnimationMs) {
    public byte Rain { get; init; }
    public byte Snow { get; init; }
    public WeatherKind Kind => Rain > 0 ? WeatherKind.Rain : Snow > 0 ? WeatherKind.Snow : WeatherKind.Clear;
    public int Intensity => Rain > 0 ? Rain : Snow;
    public bool Precipitation => Rain > 0 || Snow > 0;
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
    byte weather, rain, snow, level = 15, ownLightFlag, flashSaved;
    int flashes;
    public Ambience() : this(() => null, () => null) { }
    internal Ambience(Func<uint?> statusTime, Func<ushort?> mapId) { this.statusTime = statusTime; this.mapId = mapId; }
    /// <summary>Particle pools (sub_F0A730). Stepped by the frame loop; drawn only when a sprite source exists.</summary>
    public WeatherParticles Particles { get; } = new();

    internal void TimeReceived(uint value, long now) { statusSeen = statusTime(); seconds = value; Recalculate(); }
    /// <summary>sub_94FD10 (AM§2(a)). The map-setting veto (sub_926DA0()->[4]) is unverified and not applied.</summary>
    internal void WeatherReceived(byte value) {
        weather = value;
        (rain, snow) = AmbienceWeather.Decode(value);
        // Step 5: every packet with an amount lowers L by one while L ≥ 10 (no recompute).
        if ((rain | snow) > 0 && level >= 10) level--;
        // Step 6: forced rain 2 on these maps, regardless of the packet.
        if (AmbienceWeather.ForcedRain(mapId())) rain = AmbienceWeather.ForcedRainAmount;
    }
    /// <summary>sub_9493B0 / sub_92C9F0: removing (or action 8 of) a weather object clears its amount.</summary>
    internal void WeatherObjectGone(ushort gfx) {
        if (AmbienceWeather.ClearsSnow(gfx)) snow = 0;
        if (AmbienceWeather.ClearsRain(gfx)) rain = 0;
    }
    internal void OwnLightReceived(byte flag) => ownLightFlag = flag;
    internal void Clear() { seconds = statusSeen = null; mapSeen = null; weather = rain = snow = ownLightFlag = flashSaved = 0; level = 15; flashes = 0; Particles.Reset(); }
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
        level = AmbienceCurve.Level(s, (rain | snow) > 0);
    }
    public AmbienceFrame At(long now) {
        Observe();
        return new(seconds, weather, now) { Level = seconds == null ? (byte)15 : level, MapId = mapSeen, OwnLightFlag = ownLightFlag, Rain = rain, Snow = snow };
    }
}

/// <summary>TW16 S_WEATHER rules (docs/reference/ambience-and-map-events.md §2(a)). Pure functions.</summary>
public static class AmbienceWeather {
    public const byte ForcedRainAmount = 2;
    /// <summary>n = v &amp; 0x0F (n &gt; 3 → 0); high nibble 0x00 = rain, 0x10 = snow, anything else = clear.</summary>
    public static (byte Rain, byte Snow) Decode(byte value) {
        byte n = (byte)(value & 0x0F); if (n > 3) n = 0;
        return (value & 0xF0) switch { 0x00 => (n, (byte)0), 0x10 => ((byte)0, n), _ => ((byte)0, (byte)0) };
    }
    public static bool ForcedRain(ushort? map) => map is >= 7701 and <= 7710 or >= 7771 and <= 7780;
    public static bool ClearsSnow(ushort gfx) => gfx is 10683 or 10632;
    public static bool ClearsRain(ushort gfx) => gfx == 10891;
    /// <summary>Amount 1/2/3 → 128/256/512 rain particles, 48/96/144 snow particles (first n of each pool).</summary>
    public static int RainCount(int amount) => amount switch { 1 => 128, 2 => 256, 3 => 512, _ => 0 };
    public static int SnowCount(int amount) => amount switch { 1 => 48, 2 => 96, 3 => 144, _ => 0 };
}

/// <summary>One weather particle in the TW16 800×480 screen space. Rain: Layer 0–3 and Stage (splash phase).
/// Snow: Layer is the flake size 0–3 and Stage is unused. Landing is the row where the particle stops or respawns.</summary>
public readonly record struct WeatherParticle(WeatherKind Kind, int Index, int Layer, float X, float Y, int Stage, int Landing);

/// <summary>Draws one particle at a viewport position. TW16 uses Surf −100..−97 for rain layers 0/1 and
/// procedural patterns for rain layers 2/3 and snow; none of that art is available yet (T167), so the painter
/// draws nothing unless a source is supplied.</summary>
public delegate void WeatherSprite(Graphics graphics, WeatherParticle particle, PointF position);

/// <summary>TW16 rain (512, sub_F0E290) and snow (192, sub_F0E0D0) particle pools in 800×480 coordinates.
/// Random numbers use the MSVC rand() LCG (RAND_MAX 0x7FFF) so a seed gives a reproducible sequence.</summary>
public sealed class WeatherParticles {
    public const int Width = 800, Height = 480, RainWrap = 485, RainPool = 512, SnowPool = 192, TickMs = 24, MaxFrameMs = 500;
    struct Drop { public int X, Y, Landing, Stage; }
    struct Flake { public int X, Y, Landing; }
    readonly Drop[] rain = new Drop[RainPool];
    readonly Flake[] snow = new Flake[SnowPool];
    readonly uint initialSeed;
    uint seed;
    int pending;
    public long Ticks { get; private set; }
    public WeatherParticles(uint seed = 1) { initialSeed = seed; Reset(); }

    int Rand() { seed = unchecked(seed * 214013 + 2531011); return (int)(seed >> 16 & 0x7FFF); }

    /// <summary>Rain layer by index within each 256 group: 0–117 → 3, 118–235 → 2, 236–245 → 1, 246–255 → 0.</summary>
    public static int RainLayer(int index) => (index & 255) switch { < 118 => 3, < 236 => 2, < 246 => 1, _ => 0 };
    /// <summary>Layer 3 moves 1 px every 4 ticks, 2 every 3, 1 every 2, 0 every tick.</summary>
    public static int RainDivisor(int layer) => layer + 1;
    /// <summary>Snow size by index: 0–63 → 3, 64–149 → 2, 150–175 → 1, 176–191 → 0.</summary>
    public static int SnowSize(int index) => index switch { < 64 => 3, < 150 => 2, < 176 => 1, _ => 0 };
    /// <summary>Snow period field (50/60/70/80); the update test "accumulated + 100 &gt; period" holds every frame.</summary>
    public static int SnowPeriod(int index) => 50 + 10 * (3 - SnowSize(index));

    int RainLanding(int layer) => layer switch { 3 => Rand() % 250 + 1, 2 => Rand() % 250 + 200, _ => RainWrap };
    int SnowLanding(int size) { int top = (4 - size) * 64; return top + (505 - top) * Rand() / 0x7FFF; }

    /// <summary>sub_F0A730: re-seed and re-initialise both pools.</summary>
    public void Reset() {
        seed = initialSeed; pending = 0; Ticks = 0;
        for (int i = 0; i < RainPool; i++) {
            int layer = RainLayer(i);
            rain[i] = new() { X = Rand() % Width, Y = Rand() % RainWrap, Landing = RainLanding(layer) };
            if (rain[i].Y > rain[i].Landing) rain[i].Y = 0;
        }
        for (int i = 0; i < SnowPool; i++) {
            int landing = SnowLanding(SnowSize(i));
            snow[i] = new() { X = Rand() % Width, Landing = landing, Y = landing > 0 ? Rand() % landing : 0 };
        }
    }

    /// <summary>Advance one rendered frame. elapsedMs is capped at 500 (dword_1544378). shiftX is the camera
    /// compensation in 800-wide screen pixels, added to every rain x. Rain advances in 24 ms ticks;
    /// snow advances once per call.</summary>
    public void Step(AmbienceFrame frame, long elapsedMs, int shiftX = 0) {
        int rainCount = AmbienceWeather.RainCount(frame.Rain), snowCount = AmbienceWeather.SnowCount(frame.Snow);
        if (frame.Rain > 0) {
            pending += (int)Math.Clamp(elapsedMs, 0, MaxFrameMs);
            if (shiftX != 0) for (int i = 0; i < rainCount; i++) rain[i].X = Mod(rain[i].X + shiftX, Width);
            for (; pending >= TickMs; pending -= TickMs) {
                Ticks++;
                for (int i = 0; i < rainCount; i++) {
                    int layer = RainLayer(i);
                    if (Ticks % RainDivisor(layer) != 0) continue;
                    ref var drop = ref rain[i];
                    if (drop.Y >= drop.Landing) {
                        // Landed: y stays, the stage grows; respawn once y + stage − 5*(4 − layer) ≥ landing.
                        drop.Stage++;
                        if (drop.Y + drop.Stage - 5 * (4 - layer) >= drop.Landing) {
                            drop.X = Rand() % Width; drop.Y = 0; drop.Stage = 0; drop.Landing = RainLanding(layer);
                        }
                    } else drop.Y = (drop.Y + 1) % RainWrap;
                }
            }
        } else pending = 0;
        if (frame.Rain == 0 && frame.Snow > 0) {
            int n = frame.Snow;
            for (int i = 0; i < snowCount; i++) {
                ref var flake = ref snow[i];
                flake.X -= Rand() % (3 * n);
                if (flake.X <= 0) flake.X = Width;
                flake.Y += 5 * n;
                if (flake.Y >= flake.Landing) { flake.Y = 0; flake.Landing = SnowLanding(SnowSize(i)); }
            }
        }
    }

    /// <summary>Active particles for the frame (rain has priority over snow), in 800×480 coordinates.
    /// Rain x includes the ±8 px sine sway sin(ticks·π/144)·8 and wraps at 800.</summary>
    public IEnumerable<WeatherParticle> Active(AmbienceFrame frame) {
        if (frame.Rain > 0) {
            float sway = (float)(Math.Sin(Ticks * Math.PI / 144) * 8);
            for (int i = 0; i < AmbienceWeather.RainCount(frame.Rain); i++) {
                var drop = rain[i];
                float x = (float)ModF(drop.X + sway, Width); if (x >= Width) x = 0; // float rounding of 799.99…
                yield return new(WeatherKind.Rain, i, RainLayer(i), x, Math.Min(drop.Y, drop.Landing), drop.Stage, drop.Landing);
            }
        } else if (frame.Snow > 0) {
            for (int i = 0; i < AmbienceWeather.SnowCount(frame.Snow); i++)
                yield return new(WeatherKind.Snow, i, SnowSize(i), snow[i].X, snow[i].Y, 0, snow[i].Landing);
        }
    }
    static int Mod(int value, int period) => (value % period + period) % period;
    static double ModF(double value, double period) => (value % period + period) % period;
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
    /// <summary>Observe-only hook for S120 (remove object) and S158 action 8, called before the object handlers
    /// so the departing object's gfx is still known. Malformed packets are left to those handlers.</summary>
    void ObserveWeatherObject(byte opcode, byte[] plain) {
        uint id;
        try {
            var reader = new LoginReader(plain); reader.C(); id = reader.D();
            if (opcode == 158 && reader.C() != 8) return;
            reader.Finish();
        } catch (ProtocolException) { return; }
        if (objects.TryGetValue(id, out var gone)) Ambience.WeatherObjectGone(gone.Gfx);
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
    /// <summary>Fixed TW16 counts (AM§2(c)(d)), independent of the viewport size: rain has priority over snow.</summary>
    public static int ParticleCount(AmbienceFrame frame) =>
        frame.Rain > 0 ? AmbienceWeather.RainCount(frame.Rain) : AmbienceWeather.SnowCount(frame.Snow);

    /// <summary>Weather first, then shading of the target pixels (AM§2(b): rain/snow are darkened with the scene).</summary>
    public static void Draw(Graphics graphics, Bitmap target, Rectangle viewport, AmbienceFrame frame, AmbienceLightMap? lightMap = null,
        WeatherParticles? particles = null, WeatherSprite? sprite = null) {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        var saved = graphics.Save();
        try {
            graphics.SetClip(viewport, CombineMode.Intersect);
            DrawWeather(graphics, viewport, frame, particles, sprite);
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

    /// <summary>Particles in TW16 800×480 coordinates, scaled to the viewport. Without a sprite source nothing is drawn:
    /// the original rain/snow art is not available (T167) and no substitute is used.</summary>
    static void DrawWeather(Graphics graphics, Rectangle viewport, AmbienceFrame frame, WeatherParticles? particles, WeatherSprite? sprite) {
        if (particles == null || sprite == null || !frame.Precipitation) return;
        float sx = (float)viewport.Width / WeatherParticles.Width, sy = (float)viewport.Height / WeatherParticles.Height;
        foreach (var particle in particles.Active(frame))
            sprite(graphics, particle, new(viewport.X + particle.X * sx, viewport.Y + particle.Y * sy));
    }
}