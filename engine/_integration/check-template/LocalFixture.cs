using System.Security.Cryptography;
using System.Text.Json;

internal static class LocalFixture
{
    public const string Sha256 = "825673b11feb6b28c6d34663cd53266d0ca2209230b1b1cfa4c0a80125263383";
    public static byte[]? Load(string[] args)
    {
        if ((args.Length != 3 && args.Length != 5) || args[1] != "--assets-root" || (args.Length == 5 && args[3] != "--product-receipt"))
            return Stop("blocked", "Usage: <output-root> --assets-root <local-assets-root> [--product-receipt <dev-receipt>]", 2);
        string path = Path.Combine(args[2], "maps", "effects", "6016.json");
        try
        {
            if (!File.Exists(path)) return Stop("skipped", "Missing local assets/maps/effects/6016.json", 77);
            byte[] bytes = File.ReadAllBytes(path);
            if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != Sha256)
                return Stop("blocked", "6016 SHA-256 mismatch", 2);
            Directory.CreateDirectory(Path.Combine(args[0], "evidence"));
            return bytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Stop("blocked", "Cannot read local fixture: " + ex.GetType().Name, 2);
        }
    }
    static byte[]? Stop(string status, string reason, int code)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { status, reason, testsExecuted = false }));
        Environment.ExitCode = code;
        return null;
    }
}
