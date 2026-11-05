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
    int stopped;
    readonly record struct Entry(DateTime Utc, bool Incoming, byte Op, int Length, long? Sequence);

    PacketTrace(string path, string? repoRoot)
    {
        // Path discovery, validation, directory creation and all I/O stay off the caller.
        worker = Task.Run(() => WriteAsync(path, repoRoot));
    }

    internal static PacketTrace? FromEnvironment(string? repoRoot = null)
    {
        try
        {
            string? path = Environment.GetEnvironmentVariable("TW_PACKET_TRACE");
            return string.IsNullOrWhiteSpace(path) ? null : new PacketTrace(path, repoRoot);
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
            if (!queue.Writer.TryWrite(new Entry(DateTime.UtcNow, incoming, packet[0], packet.Length, incoming ? sequence : null)))
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
                    string line = JsonSerializer.Serialize(new
                    {
                        utc = row.Utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                        dir = row.Incoming ? "in" : "out", op = (int)row.Op, len = row.Length, seq = row.Sequence
                    });
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
