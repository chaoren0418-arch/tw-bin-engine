using System.Security.Cryptography;
using System.Text.Json;

namespace P24.Audio;

internal sealed record AssetPin(int Id, string Extension, string RelativePath, long? Size, string? Sha256);
internal sealed class AudioAssets(string root, IReadOnlyList<AssetPin> pins)
{
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
