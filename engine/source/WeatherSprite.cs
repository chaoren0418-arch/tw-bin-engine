using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
namespace P06;

/// <summary>One decoded TW16 .img: u16 width, height, flags, colour key, then width×height RGB555 (AM§3.1).</summary>
public sealed record WeatherImage(int Width, int Height, ushort Flags, ushort ColorKey, ushort[] Pixels) {
    public static WeatherImage Decode(ReadOnlySpan<byte> data, string name = "img") {
        if (data.Length < 8) throw new InvalidDataException($"{name}: .img 標頭截斷");
        int w = BinaryPrimitives.ReadUInt16LittleEndian(data), h = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        if (w is <= 0 or > 64 || h is <= 0 or > 64 || data.Length != 8 + w * h * 2) throw new InvalidDataException($"{name}: .img 尺寸 {w}×{h} 與長度 {data.Length} 不符");
        var pixels = new ushort[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(8 + 2 * i, 2));
        return new(w, h, BinaryPrimitives.ReadUInt16LittleEndian(data[4..]), BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), pixels);
    }
    /// <summary>The colour key is transparent. All four rain images have flags 0 and key 0 (AM§3.1); treating key 0 as
    /// transparent even with flags 0 is 推測 (the native sprite blit for these surfs is not decompiled).</summary>
    public bool Transparent(ushort pixel) => pixel == ColorKey;
}

/// <summary>TW16 weather particle art (AM§2(c)(d), §3.1). Rain layer 0 draws Surf 924/925 and layer 1 Surf 926/927,
/// alternating by particle index parity (TW16 VA 0xF0E66D / 0xF0E6AE: add 924 / add 926). Rain layers 2/3 and snow are
/// procedural (sub_975C40 / sub_9755E0): the documented shapes are used — layer 3 a 5-point cross, layer 2 a 4×4 pattern
/// of 12 points, snow pattern 3 / 2 with 11 / 14 points, all white-weighted — but the exact pixel offsets and blend of
/// those native routines are not dumped, so their geometry is 推測. Rain images are read at run time from the original
/// client (read-only, pinned by SHA-256); nothing is copied into the repo.</summary>
public sealed class WeatherSpriteSet {
    public const int FirstRainSurf = 924;
    /// <summary>Snow uses blend parameter 25 (sub_9755E0(…, size, 25)); rain splashes use 16/stage, centre +12.</summary>
    public const int SnowWeight = 25, SplashWeight = 16, SplashCentreBonus = 12;
    // AM§3.1: Sprite.pak entries 924..927.img (sizes 10×10, 9×9, 9×9, 7×7; flags and colour key 0).
    static readonly (string Name, string Sha256, int Width, int Height)[] RainPins = [
        ("924.img", "7872d5b398fb348781f5d89c87c6ffba5ec8d4b565c8f761db49e4df7d7c2844", 10, 10),
        ("925.img", "11cb8eff92406733c4a9c1671568797192773b3817a48278aeee893ac6bcda6f", 9, 9),
        ("926.img", "c1be786e7cd80756237b30587f0f83dd9f625d9cfb99cb1069e8eb21c1a73930", 9, 9),
        ("927.img", "778c773692ed0342e2b9ab86de3ca5190e10de3df211f01a683f52640ccb5120", 7, 7)];

    readonly WeatherImage?[] rain;
    readonly Bitmap?[] rainBitmaps;
    readonly Dictionary<(int Pattern, int Weight), Bitmap> patterns = [];
    public WeatherSpriteSet(IReadOnlyList<WeatherImage?> rainImages) {
        if (rainImages.Count != 4) throw new ArgumentException("需要 924–927 四張雨圖（可為 null）", nameof(rainImages));
        rain = rainImages.ToArray(); rainBitmaps = rain.Select(i => i == null ? null : ToBitmap(i)).ToArray();
        Sprite = Draw;
    }
    /// <summary>No rain images: layers 0/1 are skipped (no substitute), procedural layers still draw.</summary>
    public static WeatherSpriteSet ProceduralOnly { get; } = new([null, null, null, null]);
    public WeatherSprite Sprite { get; }
    public bool HasRainImages => rain.All(i => i != null);
    public WeatherImage? RainImage(int surf) => surf is >= FirstRainSurf and < FirstRainSurf + 4 ? rain[surf - FirstRainSurf] : null;

    /// <summary>Surf for a rain particle: layer 0 → 924/925, layer 1 → 926/927 by index parity; 0 for procedural layers.</summary>
    public static int RainSurf(WeatherParticle p) => p.Kind != WeatherKind.Rain ? 0 : p.Layer switch {
        0 => FirstRainSurf + (p.Index & 1), 1 => FirstRainSurf + 2 + (p.Index & 1), _ => 0 };
    /// <summary>Splash weight 16/stage (stage ≥ 1); a falling procedural drop (stage 0) is not drawn (推測: 16/0 is undefined).</summary>
    public static int SplashWeightAt(int stage) => stage <= 0 ? 0 : SplashWeight / stage;

    /// <summary>Points of a procedural pattern relative to the particle position, with a centre flag.
    /// Pattern 3/2 = rain layers 3/2 (100 + layer) or snow sizes 3/2 (200 + size). Geometry 推測 (see class summary).</summary>
    public static (int X, int Y, bool Centre)[] Pattern(int pattern) => pattern switch {
        103 => [(0, 0, true), (-1, 0, false), (1, 0, false), (0, -1, false), (0, 1, false)],
        102 => [.. from y in Enumerable.Range(0, 4) from x in Enumerable.Range(0, 4)
                   where !((x == 0 || x == 3) && (y == 0 || y == 3))
                   select (x - 1, y - 1, x is 1 or 2 && y is 1 or 2)],
        203 => [(1, 0, false), (2, 0, false), (0, 1, false), (1, 1, true), (2, 1, false), (0, 2, false), (1, 2, true), (2, 2, false), (3, 2, false), (1, 3, false), (2, 3, false)],
        202 => [(1, 0, false), (2, 0, false), .. from y in Enumerable.Range(1, 3) from x in Enumerable.Range(0, 4) select (x, y, x is 1 or 2 && y is 1 or 2)],
        _ => [] };

    void Draw(Graphics graphics, WeatherParticle particle, PointF position) {
        int x = (int)MathF.Floor(position.X), y = (int)MathF.Floor(position.Y);
        if (particle.Kind == WeatherKind.Rain) {
            int surf = RainSurf(particle);
            if (surf != 0) { if (rainBitmaps[surf - FirstRainSurf] is { } image) graphics.DrawImageUnscaled(image, x, y); return; }
            int weight = SplashWeightAt(particle.Stage);
            if (weight > 0 && PatternBitmap(100 + particle.Layer, weight) is { } splash) graphics.DrawImageUnscaled(splash, x - 1, y - 1);
        } else if (particle.Kind == WeatherKind.Snow && PatternBitmap(200 + particle.Layer, SnowWeight) is { } flake) graphics.DrawImageUnscaled(flake, x, y);
    }

    /// <summary>White at weight w/31 over the scene (c' = c + (31 − c)·w/31), centre points +12, capped at 31.</summary>
    Bitmap? PatternBitmap(int pattern, int weight) {
        if (patterns.TryGetValue((pattern, weight), out var hit)) return hit;
        var points = Pattern(pattern); if (points.Length == 0) return null;
        int ox = pattern == 102 || pattern == 103 ? 1 : 0;
        var bitmap = new Bitmap(4 + ox, 4 + ox, PixelFormat.Format32bppPArgb);
        foreach (var (px, py, centre) in points) {
            int w = Math.Min(31, weight + (centre && pattern < 200 ? SplashCentreBonus : 0));
            int a = w * 255 / 31; bitmap.SetPixel(px + ox, py + ox, Color.FromArgb(a, 255, 255, 255));
        }
        return patterns[(pattern, weight)] = bitmap;
    }

    static Bitmap ToBitmap(WeatherImage image) {
        var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
        var bits = bitmap.LockBits(new(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try {
            var row = new int[image.Width];
            for (int y = 0; y < image.Height; y++) {
                for (int x = 0; x < image.Width; x++) { ushort p = image.Pixels[y * image.Width + x]; row[x] = image.Transparent(p) ? 0 : Renderer.Argb(p); }
                Marshal.Copy(row, 0, bits.Scan0 + y * bits.Stride, image.Width);
            }
        } finally { bitmap.UnlockBits(bits); }
        return bitmap;
    }

    /// <summary>Read 924..927.img from the original client's Sprite.idx/Sprite.pak (TW_CLIENT_ROOT, read-only). Verifies the
    /// Sprite.idx SHA-256 and Sprite.pak length (same pins as Source.lmp), a single case-insensitive entry per name, the
    /// payload SHA-256 and the image size. Any failure throws; the caller then draws no rain image.</summary>
    public static WeatherSpriteSet Load(string? clientRoot = null) {
        string root = Path.GetFullPath(clientRoot ?? GroundSpriteReader.ClientRoot);
        byte[] index = File.ReadAllBytes(Path.Combine(root, "Sprite.idx"));
        if (!Convert.ToHexString(SHA256.HashData(index)).Equals(AmbienceLightImages.IndexSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Sprite.idx pin 不符");
        uint count = BitConverter.ToUInt32(index, 0);
        if (index.Length != 4 + 28L * count) throw new InvalidDataException("Sprite.idx 長度不符");
        var found = new (long Offset, int Size)?[RainPins.Length];
        for (int i = 0; i < count; i++) {
            int at = 4 + 28 * i, end = Array.IndexOf(index, (byte)0, at + 4, 20);
            if (end < 0) end = at + 24;
            string name = System.Text.Encoding.ASCII.GetString(index, at + 4, end - at - 4);
            int slot = Array.FindIndex(RainPins, p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (slot < 0) continue;
            if (found[slot] != null) throw new InvalidDataException($"Sprite.idx 有重複的 {RainPins[slot].Name}");
            found[slot] = (BitConverter.ToUInt32(index, at), checked((int)BitConverter.ToUInt32(index, at + 24)));
        }
        using var pak = new FileStream(Path.Combine(root, "Sprite.pak"), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (pak.Length != AmbienceLightImages.PakSize) throw new InvalidDataException("Sprite.pak 長度不符");
        var images = new WeatherImage?[RainPins.Length];
        for (int slot = 0; slot < RainPins.Length; slot++) {
            var pin = RainPins[slot];
            if (found[slot] is not { } entry) throw new InvalidDataException($"Sprite.idx 找不到 {pin.Name}");
            if (entry.Size is < 8 or > 64 * 1024 || entry.Offset + entry.Size > pak.Length) throw new InvalidDataException($"{pin.Name} 範圍不符");
            pak.Position = entry.Offset; var payload = new byte[entry.Size]; pak.ReadExactly(payload);
            if (!Convert.ToHexString(SHA256.HashData(payload)).Equals(pin.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{pin.Name} payload pin 不符");
            var image = WeatherImage.Decode(payload, pin.Name);
            if (image.Width != pin.Width || image.Height != pin.Height) throw new InvalidDataException($"{pin.Name} 尺寸不符");
            images[slot] = image;
        }
        return new(images);
    }
}
