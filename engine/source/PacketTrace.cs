using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace P06;

// Only scalar metadata crosses the queue. Never retain or serialize packet buffers.
internal sealed class PacketTrace : IDisposable
{
    const int Capacity = 4096;
    static readonly PacketTrace? shared = CreateShared();
    readonly Channel<Entry> queue = Channel.CreateBounded<Entry>(new BoundedChannelOptions(Capacity)
    {
        SingleReader = true, FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });
    readonly Task worker;
    readonly bool combatFields;
    int stopped;
    readonly record struct Entry(DateTime Utc, bool Incoming, byte Op, int Length, long? Sequence, uint? Target, ushort? Damage);

    PacketTrace(string path, string? repoRoot, bool combatFields)
    {
        this.combatFields = combatFields;
        // Path discovery, validation, directory creation and all I/O stay off the caller.
        worker = Task.Run(() => WriteAsync(path, repoRoot));
    }

    internal static PacketTrace? FromEnvironment(string? repoRoot = null)
    {
        try
        {
            string? path = Environment.GetEnvironmentVariable("TW_PACKET_TRACE");
            return string.IsNullOrWhiteSpace(path) ? null : new PacketTrace(path, repoRoot,
                Environment.GetEnvironmentVariable("TW_PACKET_TRACE_COMBAT") == "1");
        }
        catch { return null; }
    }

    static PacketTrace? CreateShared()
    {
        PacketTrace? trace = null;
        try
        {
            trace = FromEnvironment();
            if (trace != null) AppDomain.CurrentDomain.ProcessExit += (_, _) => trace.Dispose();
            return trace;
        }
        catch { trace?.Dispose(); return null; }
    }

    internal static void Out(ReadOnlySpan<byte> packet) => shared?.Record(false, packet);
    internal static void In(ReadOnlySpan<byte> packet, long sequence) => shared?.Record(true, packet, sequence);

    internal void Record(bool incoming, ReadOnlySpan<byte> packet, long? sequence = null)
    {
        if (Volatile.Read(ref stopped) != 0 || packet.IsEmpty) return;
        try
        {
            uint? target = null;
            ushort? damage = null;
            if (combatFields)
            {
                // Direction and complete envelope whitelist. No other payload crosses the queue.
                if (incoming && packet[0] == 30 && packet.Length is 24 or 40)
                {
                    target = BinaryPrimitives.ReadUInt32LittleEndian(packet[6..]);
                    damage = BinaryPrimitives.ReadUInt16LittleEndian(packet[10..]);
                }
                else if (!incoming && packet[0] == 229 && packet.Length == 16)
                    target = BinaryPrimitives.ReadUInt32LittleEndian(packet[1..]);
                else if (!incoming && packet[0] == 6 && packet.Length is 8 or 16
                    && packet[1] == 0 && packet[2] is 0 or 2 or 3 or 7
                    && (packet[2] != 3 || packet.Length >= 16))
                    // Only the four supported D/DHH skills have a target DWORD here.
                    // Other C6 variants can contain names/text instead.
                    target = BinaryPrimitives.ReadUInt32LittleEndian(packet[3..]);
            }
            if (!queue.Writer.TryWrite(new Entry(DateTime.UtcNow, incoming, packet[0], packet.Length,
                incoming ? sequence : null, target, damage)))
                Stop(); // A saturated trace must never wait on the network/game thread.
        }
        catch { Stop(); }
    }

    static string? FindRepoRoot()
    {
        // Use the product location, not the launcher's working directory (often engine/).
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "project-manifest.json")) && Directory.Exists(Path.Combine(dir.FullName, "engine")))
                return dir.FullName;
        return null;
    }

    static void RejectLinks(string path)
    {
        for (string? part = path; part != null; part = Path.GetDirectoryName(part))
        {
            try
            {
                if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Trace path contains a link");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    static string? ResolvePath(string value, string? repoRoot)
    {
        repoRoot ??= FindRepoRoot();
        if (repoRoot == null) return null;
        string root = Path.GetFullPath(repoRoot);
        string scratch = Path.Combine(root, "scratch") + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(value, root);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(scratch, comparison) || path.Length == scratch.Length) return null;
        // Reject NTFS alternate data streams; CreateNew below also refuses existing hard links/files.
        if (path[scratch.Length..].Contains(':')) return null;
        RejectLinks(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RejectLinks(path);
        return path;
    }

    async Task WriteAsync(string value, string? repoRoot)
    {
        try
        {
            string? path = ResolvePath(value, repoRoot);
            if (path == null) return;
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read,
                4096, FileOptions.Asynchronous);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            while (await queue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                for (int i = 0; i < 128 && queue.Reader.TryRead(out var row); i++)
                {
                    var fields = new Dictionary<string, object?>
                    {
                        ["utc"] = row.Utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                        ["dir"] = row.Incoming ? "in" : "out", ["op"] = (int)row.Op,
                        ["len"] = row.Length, ["seq"] = row.Sequence
                    };
                    if (row.Target.HasValue) fields["target"] = row.Target.Value;
                    if (row.Damage.HasValue) fields["damage"] = row.Damage.Value;
                    string line = JsonSerializer.Serialize(fields);
                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                }
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
        catch { /* Optional evidence must not affect the client, even on disk/permission failures. */ }
        finally
        {
            Stop();
            while (queue.Reader.TryRead(out _)) { }
        }
    }

    void Stop()
    {
        Interlocked.Exchange(ref stopped, 1);
        queue.Writer.TryComplete();
    }

    public void Dispose()
    {
        Stop();
        // Normal process exit drains and flushes; a stalled device must not hang shutdown.
        try { worker.Wait(TimeSpan.FromSeconds(2)); } catch { }
    }
}
