using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using P06;

// Same dependency-free console assertions as check-template; no client, assets or sockets.
if (args.Contains("--exit-trace"))
{
    PacketTrace.Out([233, 1, 2, 3]);
    PacketTrace.In([48, 4, 5], 17);
    return 0; // Intentionally no Dispose: exercise the real ProcessExit hook.
}

var repo = new DirectoryInfo(AppContext.BaseDirectory);
while (repo != null && !File.Exists(Path.Combine(repo.FullName, "project-manifest.json"))) repo = repo.Parent;
if (repo == null) throw new Exception("Run tests inside the repository");
string testRoot = Path.Combine(repo.FullName, "scratch", "packet-trace-checks", Guid.NewGuid().ToString("N"));
string fakeRepo = Path.Combine(testRoot, "repo");
string scratch = Path.Combine(fakeRepo, "scratch");
Directory.CreateDirectory(scratch);
string? saved = Environment.GetEnvironmentVariable("TW_PACKET_TRACE");
int assertions = 0;
var cases = new List<object>();
void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
void Case(string name, Action test) { int start = assertions; test(); cases.Add(new { name, assertions = assertions - start, status = "pass" }); }
PacketTrace? Open(string? path)
{
    Environment.SetEnvironmentVariable("TW_PACKET_TRACE", path);
    return PacketTrace.FromEnvironment(fakeRepo);
}
void AssertRow(string line, string direction, int opcode, int length, long? sequence)
{
    using var doc = JsonDocument.Parse(line);
    var row = doc.RootElement;
    Check(row.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "dir", "len", "op", "seq", "utc" }), "exactly five permitted fields");
    Check(DateTime.TryParseExact(row.GetProperty("utc").GetString(), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc) && Math.Abs((DateTime.UtcNow - utc).TotalSeconds) < 30, "UTC ISO-8601 milliseconds");
    Check(row.GetProperty("dir").GetString() == direction, "direction");
    Check(row.GetProperty("op").GetInt32() == opcode && row.GetProperty("len").GetInt32() == length, "plaintext opcode/length");
    Check(sequence.HasValue ? row.GetProperty("seq").GetInt64() == sequence : row.GetProperty("seq").ValueKind == JsonValueKind.Null, "inbound sequence or null");
}

try
{
    Case("unset-no-file", () =>
    {
        using var trace = Open(null);
        Check(trace == null, "unset environment disables trace");
        PacketTrace.Out([1, 2, 3]);
        PacketTrace.In([4, 5], 1);
        Check(!Directory.EnumerateFileSystemEntries(scratch).Any(), "default leaves no files or directories");
    });
    Case("metadata-only-out-in-and-secret", () =>
    {
        const string secret = "FAKE_PASSWORD_T041_NEVER_WRITE";
        byte[] packet = new byte[] { 203 }.Concat(Encoding.UTF8.GetBytes(secret)).ToArray();
        byte[] before = packet.ToArray();
        string path = Path.Combine(scratch, "pair.jsonl");
        using (var trace = Open(Path.Combine("scratch", "pair.jsonl")))
        {
            Check(trace != null, "configured trace created");
            trace!.Record(false, packet, 99);
            trace.Record(true, [48, 0, 7], 42);
            trace.Record(true, []); // No first byte means no opcode/row.
            Check(packet.SequenceEqual(before), "trace does not mutate plaintext");
            Array.Clear(packet); // Worker can only see the captured scalar metadata.
        }
        string text = File.ReadAllText(path);
        var lines = File.ReadAllLines(path);
        Check(lines.Length == 2, "one line per nonempty packet; Dispose drains");
        AssertRow(lines[0], "out", 203, before.Length, null);
        AssertRow(lines[1], "in", 48, 3, 42);
        Check(!text.Contains(secret) && !text.Contains(Convert.ToHexString(before)) && !text.Contains(Convert.ToBase64String(before)), "no plaintext, hex or base64 secret/payload");
        Check(!File.ReadAllBytes(path).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }), "UTF-8 without BOM");
    });
    Case("outside-traversal-prefix-and-invalid-paths", () =>
    {
        string[] paths = [Path.Combine(fakeRepo, "outside.jsonl"), Path.Combine("scratch", "..", "escape.jsonl"),
            Path.Combine(fakeRepo, "scratch-other", "prefix.jsonl"), "scratch", "scratch\0invalid.jsonl",
            Path.Combine("scratch", "stream.jsonl:payload")];
        var before = Directory.GetFiles(testRoot, "*", SearchOption.AllDirectories).Order().ToArray();
        foreach (string path in paths) { using var trace = Open(path); trace?.Record(false, [1, 2, 3]); }
        Check(Directory.GetFiles(testRoot, "*", SearchOption.AllDirectories).Order().SequenceEqual(before), "invalid/outside paths create no files");
    });
    Case("existing-file-and-io-failure-disable-silently", () =>
    {
        string existing = Path.Combine(scratch, "existing.jsonl");
        File.WriteAllText(existing, "preserve-existing-evidence");
        using (var trace = Open(existing)) { trace!.Record(false, [1, 2]); }
        Check(File.ReadAllText(existing) == "preserve-existing-evidence", "never overwrite/append existing file or hard link");
        string blocked = Path.Combine(scratch, "blocked");
        File.WriteAllText(blocked, "parent-is-file");
        using (var trace = Open(Path.Combine(blocked, "trace.jsonl")))
            for (int i = 0; i < 10000; i++) trace!.Record(false, [1, 2]);
        Check(File.ReadAllText(blocked) == "parent-is-file", "I/O failure and queue pressure do not throw or alter parent");
    });
    Case("junction-outside-scratch-rejected", () =>
    {
        string outside = Path.Combine(testRoot, "outside");
        string link = Path.Combine(scratch, "link");
        Directory.CreateDirectory(outside);
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string arg in new[] { "/c", "mklink", "/J", link, outside }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd();
            Check(process.WaitForExit(10000) && process.ExitCode == 0, "test junction created");
        }
        else Directory.CreateSymbolicLink(link, outside);
        try
        {
            using (var trace = Open(Path.Combine(link, "escape.jsonl"))) trace!.Record(false, [1, 2]);
            Check(!Directory.EnumerateFileSystemEntries(outside).Any(), "no writes through junction/symlink");
        }
        finally { Directory.Delete(link); } // Remove only the test link, never its target.
    });
    Case("concurrent-records", () =>
    {
        string path = Path.Combine(scratch, "concurrent.jsonl");
        using (var trace = Open(path)) Parallel.For(0, 4, producer =>
        {
            for (int i = 0; i < 100; i++) trace!.Record(true, [(byte)producer, 7], producer * 100 + i);
        });
        var lines = File.ReadAllLines(path);
        Check(lines.Length == 400, "all records drained without interleaved JSON");
        var sequences = lines.Select(line => { using var d = JsonDocument.Parse(line); return d.RootElement.GetProperty("seq").GetInt64(); }).ToArray();
        Check(sequences.Distinct().Count() == 400, "no duplicate/lost metadata");
    });
    Case("process-exit-flush-and-product-root-resolution", () =>
    {
        string path = Path.Combine(testRoot, "exit.jsonl");
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.Combine(repo.FullName, "engine") };
        start.ArgumentList.Add(typeof(PacketTrace).Assembly.Location);
        start.ArgumentList.Add("--exit-trace");
        start.Environment["TW_PACKET_TRACE"] = Path.GetRelativePath(repo.FullName, path);
        using var process = Process.Start(start)!;
        Check(process.WaitForExit(15000) && process.ExitCode == 0, "metadata-only child exits successfully");
        var lines = File.ReadAllLines(path);
        Check(lines.Length == 2, "ProcessExit drains even on immediate exit");
        AssertRow(lines[0], "out", 233, 4, null);
        AssertRow(lines[1], "in", 48, 3, 17);
    });
    Console.WriteLine(JsonSerializer.Serialize(new { status = "pass", assertions, cases, gameStarted = false, network = false }));
    return 0;
}
finally
{
    Environment.SetEnvironmentVariable("TW_PACKET_TRACE", saved);
    // All paths were generated beneath this repo's scratch; no external resources are touched.
    Directory.Delete(testRoot, recursive: true);
}
