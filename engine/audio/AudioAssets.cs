using System.Security.Cryptography;
using System.Text.Json;
// T460：本檔已不讀 audio-manifest.json；JSON 只剩 combat SFX 的嵌入 pin（LoadCombat）。

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
    // T460（#1045）：6016 地圖特效的兩個聲音不再讀 assets/audio-manifest.json（那份帶研究機絕對路徑），改成程式內 pin，
    // 執行期由 TW_CLIENT_ROOT/Sound/<id>.<ext> 唯讀直讀。載入順序 mp3 → wav；MP3 兩個原客戶端 Sound 目錄都沒有（#97），
    // 宣告為不可用＝SourceUnavailable 後同 ID WAV fallback（0xA05F70：A053E0 回 0 再讀 sound/%d.wav）。
    // WAV 的大小與 SHA-256 與舊 manifest（schema p24-audio-assets-v2，SHA 87c6cd68…）逐字相同；pin 不符不 fallback。
    // host 端以版本字串對契約（取代舊的 manifest 整檔 SHA）；pin 或載入規則改動時一定要改這個字串並重跑 6016 回歸。
    internal const string Effect6016Contract = "p24-audio-assets-v3-client-sound";
    internal static readonly IReadOnlyList<AssetPin> Effect6016Pins =
    [
        new(3332, "mp3", "Sound/3332.mp3", null, null),
        new(3332, "wav", "Sound/3332.wav", 24748, "973e0a3598edd23c2a24f6900643e43a9155fe36906b939d2d8e24374abeff2a"),
        new(1340, "mp3", "Sound/1340.mp3", null, null),
        new(1340, "wav", "Sound/1340.wav", 14322, "30b4ce04d5ca914cdbfd80c7ee9b9cdbe3bf79487e609b2a9d32f6546a90f9c3"),
    ];
    /// <summary>6016 的產品 catalog：程式內 pin＋原客戶端根目錄。</summary>
    internal static AudioAssets LoadEffect6016(string clientRoot) => FromClientRoot(clientRoot, Effect6016Pins);
    /// <summary>
    /// 以原客戶端根目錄（TW_CLIENT_ROOT）建立 catalog。根目錄必須是既有目錄（不存在＝建立失敗，不是逐次缺檔）；
    /// pin 一律是 <c>Sound/&lt;id&gt;.&lt;mp3|wav&gt;</c> 的相對路徑，每個 ID 都要同時宣告 mp3 與 wav。個別檔案缺少時在播放時回 FileMissing。
    /// </summary>
    internal static AudioAssets FromClientRoot(string clientRoot, IReadOnlyList<AssetPin> pins)
    {
        if (string.IsNullOrWhiteSpace(clientRoot)) throw new InvalidDataException("Client root (TW_CLIENT_ROOT) is not set");
        string root = Path.GetFullPath(clientRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException($"Client root (TW_CLIENT_ROOT) not found: {root}");
        foreach (var pin in pins)
        {
            if (pin.Id <= 0 || pin.Extension is not ("mp3" or "wav") || pin.RelativePath != $"Sound/{pin.Id}.{pin.Extension}")
                throw new InvalidDataException("Audio allowlist/path");
            bool available = pin.Size != null || pin.Sha256 != null;
            if (available && (pin.Size is not > 0 || pin.Sha256 is not { Length: 64 } sha || !sha.All(Uri.IsHexDigit)))
                throw new InvalidDataException("Audio pin");
        }
        var ids = pins.Select(p => p.Id).Distinct().ToArray();
        if (ids.Length == 0 || pins.Count != ids.Length * 2 || pins.Select(p => (p.Id, p.Extension)).Distinct().Count() != pins.Count)
            throw new InvalidDataException("Require each ID with exactly one MP3 and one WAV declaration");
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
