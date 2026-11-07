using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace P06;

public enum WeatherKind { Clear, Rain, Snow }
public sealed record AmbienceFrame(double? Seconds, byte Weather, long AnimationMs) {
    public WeatherKind Kind => Weather switch { >= 1 and <= 3 => WeatherKind.Rain, >= 17 and <= 19 => WeatherKind.Snow, _ => WeatherKind.Clear };
    public int Intensity => Kind == WeatherKind.Clear ? 0 : Weather & 15;
    public TimeSpan? TimeOfDay => Seconds is double seconds ? TimeSpan.FromSeconds(seconds) : null;
    public bool Night => Seconds is double seconds && (seconds < 21600 || seconds >= 64800);
    // Presentation policy: dusk 18–19, dawn 05–06. The server only defines day/night.
    public byte Darkness => Seconds is not double s ? (byte)0 : (byte)Math.Round(140 *
        (s < 18000 ? 1 : s < 21600 ? (21600 - s) / 3600 : s < 64800 ? 0 : s < 68400 ? (s - 64800) / 3600 : 1));
}
public sealed record AmbienceLight(uint ObjectId, PointF Center, float Radius);

/// <summary>Visual clock only; never writes ServerTime or the ship clock.</summary>
public sealed class Ambience {
    uint? seconds;
    long sampleAt;
    byte weather;
    internal void TimeReceived(uint value, long now) { seconds = value; sampleAt = now; }
    internal void WeatherReceived(byte value) => weather = value;
    internal void Clear() { seconds = null; sampleAt = 0; weather = 0; }
    public AmbienceFrame At(long now) => new(seconds is uint s ?
        (s % 86400 + Math.Max(0d, (double)now - sampleAt) * .006) % 86400 : null, weather, now);
}

public sealed partial class WorldSession {
    public Ambience Ambience { get; } = new();
    public WorldSession() {
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
        uint id = reader.D(); byte radius = reader.C(); reader.Finish();
        if (Actor is { } own && own.ObjectId == id) Actor = own with { Light = radius };
        else if (objects.TryGetValue(id, out var other)) objects[id] = other with { Light = radius };
        // Do not remember unknown IDs: a later S87 is a new incarnation with its own Light.
    }
    public AmbienceLight[] AmbienceLights(Func<PointF, PointF> toView, float zoom,
        ObjectVisual? ownVisual, IReadOnlyList<ObjectVisual> visuals) {
        var result = new List<AmbienceLight>();
        if (Actor is { Light: > 0 } own && ownVisual?.ObjectId == own.ObjectId && !SelfAppearancePending)
            result.Add(new(own.ObjectId, toView(ownVisual.Foot), AmbiencePainter.Radius(own.Light, zoom)));
        foreach (var visual in visuals) {
            if (visual.Bounds.IsEmpty || visual.Action == "ground") continue;
            if (objects.TryGetValue(visual.ObjectId, out var other) && other.Light > 0 && !other.AppearancePending)
                result.Add(new(other.ObjectId, toView(visual.Foot), AmbiencePainter.Radius(other.Light, zoom)));
        }
        return result.ToArray();
    }
}

/// <summary>Procedural overlay: no game assets, no changes to ownership/picking buffers.</summary>
public static class AmbiencePainter {
    // The wire is a radius code, not a proven pixel distance. Cap the visual extent only.
    public static float Radius(byte value, float zoom) => Math.Min((int)value, 32) * 24f * zoom;
    public static int ParticleCount(AmbienceFrame frame, Rectangle viewport) =>
        Math.Min(768, (int)Math.Ceiling((double)viewport.Width * viewport.Height / 7000 * frame.Intensity));

    public static void Draw(Graphics graphics, Rectangle viewport, AmbienceFrame frame, IReadOnlyList<AmbienceLight> lights) {
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        var saved = graphics.Save();
        try {
            graphics.SetClip(viewport, CombineMode.Intersect);
            if (frame.Darkness > 0) {
                using var shade = Shade(viewport, frame.Darkness, lights);
                graphics.InterpolationMode = InterpolationMode.Bilinear;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.DrawImage(shade, viewport);
            }
            DrawWeather(graphics, viewport, frame);
        } finally { graphics.Restore(saved); }
    }

    static Bitmap Shade(Rectangle viewport, byte darkness, IReadOnlyList<AmbienceLight> lights) {
        // Quarter resolution is sufficient for the soft illumination field. Bound work to the viewport.
        int width = (viewport.Width + 3) / 4, height = (viewport.Height + 3) / 4;
        float sx = (float)viewport.Width / width, sy = (float)viewport.Height / height;
        var alpha = new byte[width * height]; Array.Fill(alpha, darkness);
        foreach (var light in lights) {
            float radius = light.Radius;
            if (!float.IsFinite(radius) || radius <= 0 || !float.IsFinite(light.Center.X) || !float.IsFinite(light.Center.Y)) continue;
            float cx = light.Center.X - viewport.X, cy = light.Center.Y - viewport.Y;
            if (cx + radius < 0 || cy + radius < 0 || cx - radius > viewport.Width || cy - radius > viewport.Height) continue;
            int left = Math.Clamp((int)((cx - radius) / sx), 0, width - 1), right = Math.Clamp((int)((cx + radius) / sx), 0, width - 1);
            int top = Math.Clamp((int)((cy - radius) / sy), 0, height - 1), bottom = Math.Clamp((int)((cy + radius) / sy), 0, height - 1);
            for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++) {
                double dx = (x + .5) * sx - cx, dy = (y + .5) * sy - cy;
                double distance = Math.Sqrt(dx * dx + dy * dy) / radius;
                // Bright core, smooth edge; overlaps choose the brighter light, independent of order.
                double edge = Math.Clamp((distance - .15) / .85, 0, 1);
                byte value = (byte)Math.Round(darkness * edge * edge * (3 - 2 * edge));
                int index = y * width + x; alpha[index] = Math.Min(alpha[index], value);
            }
        }
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var bits = bitmap.LockBits(new(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try {
            var row = new int[width];
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) row[x] = (alpha[y * width + x] << 24) | 0x081020;
                Marshal.Copy(row, 0, bits.Scan0 + y * bits.Stride, width);
            }
        } finally { bitmap.UnlockBits(bits); }
        return bitmap;
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
