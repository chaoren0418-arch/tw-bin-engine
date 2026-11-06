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

    public static (int Status, byte[]? Body) Read(string assetsRoot, string request)
    {
        if (!TryId(request, out ushort id)) return (request.StartsWith("https://p08.example/icons/", StringComparison.Ordinal) ? 404 : 403, null);
        try
        {
            var root = new DirectoryInfo(assetsRoot);
            if (!root.Exists) return (404, null);
            // engine/assets is the explicit local resource boundary, including its approved junction.
            string inventory = Path.Combine(root.FullName, "inventory");
            string file = Path.Combine(inventory, id.ToString(CultureInfo.InvariantCulture) + ".png");
            foreach (string path in new[] { inventory, file })
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return (403, null);
            byte[] bytes = File.ReadAllBytes(file);
            if (!bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return (404, null);
            return (200, bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (404, null);
        }
    }
}
