using System.Globalization;
namespace P06;

// Only the configured assets root may be a junction. Descendants must be ordinary files.
public static class WebIconResource
{
    public static bool TryId(string request, out ushort id)
    {
        id = 0;
        const string prefix = "https://p08.example/icons/";
        if (!request.StartsWith(prefix, StringComparison.Ordinal) || !request.EndsWith(".png", StringComparison.Ordinal)) return false;
        var digits = request.AsSpan(prefix.Length, request.Length - prefix.Length - 4);
        if (digits.Length == 0) return false;
        foreach (char c in digits) if (c < '0' || c > '9') return false;
        return ushort.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out id)
            && request == prefix + id.ToString(CultureInfo.InvariantCulture) + ".png";
    }

    static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>
    /// T469 (#1054): a file in assets/inventory wins. Only when that file (or the inventory directory, or the assets
    /// root) does not exist is <paramref name="generate"/> asked for a PNG made on this machine (WebIconGenerator: the
    /// ItemIconSource .tbt decode). The request never reaches the file system or the generator unless it is exactly
    /// "https://p08.example/icons/&lt;0..65535&gt;.png"; a reparse point below the assets root is 403 and is not replaced
    /// by a generated image; an existing file that is unreadable or not a PNG stays 404.
    /// </summary>
    public static (int Status, byte[]? Body) Read(string assetsRoot, string request, Func<ushort, byte[]?>? generate = null)
    {
        if (!TryId(request, out ushort id)) return (request.StartsWith("https://p08.example/icons/", StringComparison.Ordinal) ? 404 : 403, null);
        try
        {
            var root = new DirectoryInfo(assetsRoot);
            bool present = root.Exists;
            if (present)
            {
                // engine/assets is the explicit local resource boundary, including its approved junction.
                string inventory = Path.Combine(root.FullName, "inventory");
                string file = Path.Combine(inventory, id.ToString(CultureInfo.InvariantCulture) + ".png");
                foreach (string path in new[] { inventory, file })
                {
                    FileAttributes attributes;
                    try { attributes = File.GetAttributes(path); }
                    catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { present = false; break; }
                    if ((attributes & FileAttributes.ReparsePoint) != 0) return (403, null);
                }
                if (present)
                {
                    byte[] bytes = File.ReadAllBytes(file);
                    return bytes.AsSpan().StartsWith(PngSignature) ? (200, bytes) : (404, null);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (404, null);
        }
        byte[]? made = generate?.Invoke(id);
        return made != null && made.AsSpan().StartsWith(PngSignature) ? (200, made) : (404, null);
    }
}
