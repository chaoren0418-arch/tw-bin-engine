using System.Security.Cryptography;
using System.Text.Json;

namespace P24.MapEffectEvents;

/// <summary>Only the accepted r27 6016 metadata shape, not a general event parser.</summary>
public sealed class Effect6016Definition
{
    public const int EffectId = 6016;
    /// <summary>Whole-file SHA-256 of the accepted maps/effects/6016.json. One value for the product (ClientEffect6016) and the
    /// asset preparer's output anchor (asset-recipe.md §1.2, §3.5: never changed; a prepared file must be byte-identical).</summary>
    public const string AcceptedMetadataSha256 = "825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383";
    public const int TickRate = 24;
    public const int TicksPerStep = 5;
    public const int StepCount = 12;
    public const int EventIndex = 1;
    public const int CooldownMs = 200;
    private static readonly int[] Frames = [11, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    private static readonly int[] Candidates = [3332, 1340, 1340];
    public static IReadOnlyList<int> SequenceFrames { get; } = Array.AsReadOnly(Frames);
    public static IReadOnlyList<int> CandidateSlots { get; } = Array.AsReadOnly(Candidates);
    public string MetadataSha256 { get; }
    private Effect6016Definition(string sha256) => MetadataSha256 = sha256;

    /// <param name="expectedSha256">A trusted pinned hash; never derive it from untrusted input.</param>
    public static Effect6016Definition FromR27Metadata(byte[] bytes, string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (expectedSha256 is null || expectedSha256.Length != 64 ||
            !expectedSha256.All(Uri.IsHexDigit)) throw new ArgumentException("Expected SHA-256 required.", nameof(expectedSha256));
        string actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("6016 metadata SHA-256 mismatch.");
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        if (root.GetProperty("id").GetInt32() != EffectId || root.GetProperty("tickRate").GetInt32() != TickRate ||
            root.GetProperty("flags").GetInt32() != 0) throw new InvalidDataException("6016 identity/timing/flags mismatch.");
        var sequence = root.GetProperty("sequence").EnumerateArray().ToArray();
        if (sequence.Length != StepCount) throw new InvalidDataException("6016 step count mismatch.");
        for (int i = 0; i < StepCount; i++)
            if (sequence[i].GetProperty("frame").GetInt32() != Frames[i] ||
                sequence[i].GetProperty("ticks").GetInt32() != TicksPerStep ||
                sequence[i].GetProperty("events").GetString() != (i == EventIndex ? "<3332<1340<1340" : ""))
                throw new InvalidDataException($"6016 step {i} differs from accepted contract.");
        var frameIndexes = root.GetProperty("frames").EnumerateArray()
            .Select(f => f.GetProperty("index").GetInt32()).ToArray();
        if (!frameIndexes.SequenceEqual(Enumerable.Range(0, StepCount)))
            throw new InvalidDataException("6016 frame index order mismatch.");
        return new(actual.ToLowerInvariant());
    }

    public static long StepAt(long startedAtMs, long nowMs)
    {
        if (startedAtMs < 0 || nowMs < startedAtMs) throw new ArgumentOutOfRangeException(nameof(nowMs));
        // Avoid floating drift and elapsed*24 overflow, even for a long.MaxValue clock jump.
        return (long)(((Int128)nowMs - startedAtMs) * TickRate / 1000 / TicksPerStep);
    }
    public static int FrameAtStep(long step) => Frames[(int)(step % StepCount)];
}
