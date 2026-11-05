using System.Security.Cryptography;
using System.Text.Json;

namespace P24.Audio;

internal sealed record AssetPin(int Id, string Extension, string RelativePath, long? Size, string? Sha256);
internal sealed class AudioAssets(string root, IReadOnlyList<AssetPin> pins)
{
    internal bool Allows(int id) => pins.Any(p => p.Id == id);
    internal string[] Formats(int id) => pins.Where(p => p.Id == id).Select(p => p.Extension).OrderBy(e => e == "mp3" ? 0 : 1).ToArray();
    internal static AudioAssets LoadCombat(string clientRoot)
    {
        using var stream = typeof(AudioAssets).Assembly.GetManifestResourceStream("P24.Audio.CombatSfxPins.json")
            ?? throw new InvalidDataException("Missing combat sound pins");
        using var doc = JsonDocument.Parse(stream);
        var pins = doc.RootElement.GetProperty("wavPins").EnumerateArray().Select(row =>
        {
            int id = row.GetProperty("id").GetInt32();
            string path = row.GetProperty("path").GetString()!;
            long size = row.GetProperty("size").GetInt64();
            string sha = row.GetProperty("sha256").GetString()!;
            if (id <= 0 || path != $"Sound/{id}.wav" || size <= 0 || sha.Length != 64 || !sha.All(Uri.IsHexDigit))
                throw new InvalidDataException("Combat sound pin");
            return new AssetPin(id, "wav", path, size, sha);
        }).ToArray();
        if (pins.Length != 33 || pins.Select(p => p.Id).Distinct().Count() != 33)
            throw new InvalidDataException("Require all 33 issue-47 WAV pins");
        return new(Path.GetFullPath(clientRoot), pins);
    }
    internal static AudioAssets Load(string root, string manifestSha256)
    {
        root = Path.GetFullPath(root);
        var bytes = File.ReadAllBytes(Path.Combine(root, "audio-manifest.json"));
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(manifestSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Audio manifest SHA-256 mismatch");
        using var doc = JsonDocument.Parse(bytes);
        if (doc.RootElement.GetProperty("schema").GetString() != "p24-audio-assets-v2")
            throw new InvalidDataException("Audio manifest schema");
        var pins = new List<AssetPin>();
        foreach (var row in doc.RootElement.GetProperty("assets").EnumerateArray())
        {
            int id = row.GetProperty("id").GetInt32();
            string ext = row.GetProperty("extension").GetString()!;
            string path = row.GetProperty("relativePath").GetString()!;
            bool available = row.GetProperty("available").GetBoolean();
            if (id is not (3332 or 1340) || ext is not ("mp3" or "wav") || path != $"sound/{id}.{ext}")
                throw new InvalidDataException("Audio allowlist/path");
            long? size = available ? row.GetProperty("size").GetInt64() : null;
            string? sha = available ? row.GetProperty("sha256").GetString() : null;
            if (available && (size <= 0 || sha == null || sha.Length != 64 || !sha.All(Uri.IsHexDigit)))
                throw new InvalidDataException("Audio pin");
            pins.Add(new(id, ext, path, size, sha));
        }
        if (pins.Count != 4 || pins.Select(p => (p.Id, p.Extension)).Distinct().Count() != 4)
            throw new InvalidDataException("Require two exact IDs, each with MP3/WAV declaration");
        return new(root, pins);
    }

    internal (FileStream? Lease, AudioEvent? Error, string Path) Acquire(int id, string ext)
    {
        var pin = pins.Single(p => p.Id == id && p.Extension == ext);
        string path = Path.GetFullPath(Path.Combine(root, pin.RelativePath));
        if (pin.Size == null || pin.Sha256 == null)
            return (null, new("SourceUnavailable", ext, "No approved source bytes packaged"), path);
        FileStream? lease = null;
        try
        {
            // Hold a read-only, deny-write/delete lease until native close succeeds.
            lease = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (lease.Length != pin.Size || !Convert.ToHexString(SHA256.HashData(lease)).Equals(pin.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                lease.Dispose();
                return (null, new("IntegrityFailure", ext, "Audio size/SHA-256 mismatch; no fallback"), path);
            }
            lease.Position = 0;
            return (lease, null, path);
        }
        catch (FileNotFoundException ex) { lease?.Dispose(); return (null, new("FileMissing", ext, ex.Message), path); }
        catch (DirectoryNotFoundException ex) { lease?.Dispose(); return (null, new("FileMissing", ext, ex.Message), path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { lease?.Dispose(); return (null, new("FileAccessFailure", ext, ex.Message), path); }
    }
}
