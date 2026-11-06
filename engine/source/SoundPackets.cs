using System.Security.Cryptography;
using System.Text.Json;
using P24.Audio;

namespace P06;

public sealed record ServerSound(byte Repeat, ushort SoundId);
public sealed record ServerEffect(uint? Target, ushort X, ushort Y, ushort GfxId, ushort? Time);

// #132: lengths include opcode and zero padding, but exclude the transport header.
public static class SoundPackets
{
    public static ServerSound Sound(byte[] plain)
    {
        var r = new LoginReader(plain);
        if (r.C() != 22) throw new ProtocolException("expected S22");
        var result = new ServerSound(r.C(), r.H());
        r.Finish();
        return result;
    }

    public static ServerEffect Effect(byte[] plain)
    {
        var r = new LoginReader(plain);
        byte opcode = r.C();
        ServerEffect result;
        if (opcode == 106) result = new(null, r.H(), r.H(), r.H(), null);
        else if (opcode == 55 && plain.Length is 8 or 16)
        {
            uint target = r.D();
            ushort gfx = r.H();
            result = new(target, 0, 0, gfx, plain.Length == 16 ? r.H() : null);
        }
        else throw new ProtocolException("expected S55 (8/16 bytes) or S106");
        r.Finish();
        return result;
    }

    // Product policy pending M6 smoke: repeat means additional plays; zero is one play.
    // Use WAV duration rather than dispatching every repetition simultaneously.
    public static SoundSchedule Schedule(ServerSound packet, double durationMs)
    {
        if (!double.IsFinite(durationMs) || durationMs <= 0 || durationMs > 60000)
            throw new InvalidDataException("S22 WAV duration outside supported range");
        return new(Enumerable.Range(0, packet.Repeat + 1)
            .Select(i => new SoundFrame(i, i * durationMs, [packet.SoundId])).ToArray(),
            (packet.Repeat + 1) * durationMs, false);
    }
}

// Read only the already-approved combat WAVs. AudioPlayer independently rechecks its pins.
public sealed class PacketSoundAssets(string clientRoot)
{
    readonly Dictionary<int, double?> durations = [];
    public double? Duration(int id)
    {
        if (durations.TryGetValue(id, out var cached)) return cached;
        double? duration = null;
        try
        {
            using var pins = typeof(AudioPlayer).Assembly.GetManifestResourceStream("P24.Audio.CombatSfxPins.json")!;
            using var doc = JsonDocument.Parse(pins);
            var rows = doc.RootElement.GetProperty("wavPins").EnumerateArray().Where(p => p.GetProperty("id").GetInt32() == id).ToArray();
            if (rows.Length == 1)
            {
                var pin = rows[0];
                using var stream = File.OpenRead(Path.Combine(clientRoot, "Sound", id + ".wav"));
                if (stream.Length != pin.GetProperty("size").GetInt64() ||
                    !Convert.ToHexString(SHA256.HashData(stream)).Equals(pin.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("S22 WAV integrity");
                stream.Position = 0;
                duration = WaveDuration(stream);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        { System.Diagnostics.Trace.WriteLine("S22 source unavailable: " + ex.Message); }
        durations[id] = duration;
        return duration;
    }

    public static double WaveDuration(Stream stream)
    {
        using var r = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        if (r.ReadUInt32() != 0x46464952 || r.ReadUInt32() + 8L != stream.Length || r.ReadUInt32() != 0x45564157)
            throw new InvalidDataException("S22 RIFF/WAVE header");
        uint rate = 0, data = 0;
        while (stream.Position + 8 <= stream.Length)
        {
            uint chunk = r.ReadUInt32(), size = r.ReadUInt32();
            long end = stream.Position + size + (size & 1);
            if (end > stream.Length) throw new InvalidDataException("S22 WAV chunk");
            if (chunk == 0x20746d66 && size >= 16)
            {
                if (r.ReadUInt16() != 1) throw new InvalidDataException("S22 requires PCM timing");
                r.ReadUInt16(); r.ReadUInt32(); rate = r.ReadUInt32();
            }
            if (chunk == 0x61746164) data = size;
            stream.Position = end;
        }
        double result = rate == 0 ? 0 : 1000d * data / rate;
        if (result <= 0 || result > 60000) throw new InvalidDataException("S22 WAV timing");
        return result;
    }
}

public sealed record PacketSound(long Number, int Epoch, ServerSound Packet, long At);

public sealed partial class WorldSession
{
    readonly List<PacketSound> packetSounds = [];
    long packetSoundNumber;
    int packetMediaEpoch = -1;
    bool packetMediaSubscribed;
    internal Func<long> PacketMediaClock { get; set; } = () => Environment.TickCount64;
    public string PacketMediaNotice { get; private set; } = "";

    void EnsurePacketMedia()
    {
        if (!packetMediaSubscribed)
        {
            CombatSoundStateChanged += PrunePacketMedia;
            packetMediaSubscribed = true;
        }
        PrunePacketMedia();
    }

    void PrunePacketMedia()
    {
        if (packetMediaEpoch != WorldEpoch)
        {
            packetSounds.Clear(); packetEffects.Clear(); PacketMediaNotice = "";
            packetMediaEpoch = WorldEpoch;
        }
        long now = PacketMediaClock();
        packetSounds.RemoveAll(s => now - s.At >= 256 * 60000L);
        packetEffects.RemoveAll(e => now >= e.Expires || e.Packet.Target is uint id && PacketEffectPosition(id) == null);
    }

    public PacketSound[] PacketSounds { get { PrunePacketMedia(); return packetSounds.ToArray(); } }

    internal void ReceiveSoundPacket(byte[] plain)
    {
        var packet = SoundPackets.Sound(plain); // Validate fully before changing state.
        EnsurePacketMedia();
        packetSounds.Add(new(++packetSoundNumber, WorldEpoch, packet, PacketMediaClock()));
        if (packetSounds.Count > 256) packetSounds.RemoveAt(0);
        PacketMediaNotice = $"S22 音效 {packet.SoundId}，repeat={packet.Repeat}；播放依已核定 WAV";
    }

    public CombatSoundInstance[] PacketSoundSnapshot(long now, Func<int, double?> duration)
    {
        var result = new List<CombatSoundInstance>();
        foreach (var item in PacketSounds)
        {
            double? ms = duration(item.Packet.SoundId);
            if (ms == null)
            {
                PacketMediaNotice = $"S22 音效 {item.Packet.SoundId} 缺少已核定 WAV／PCM 時長；未播放";
                packetSounds.Remove(item);
                continue;
            }
            var schedule = SoundPackets.Schedule(item.Packet, ms.Value);
            long end = item.At + (long)Math.Ceiling(schedule.Duration);
            if (now >= end) { packetSounds.Remove(item); continue; }
            result.Add(new($"s22/{item.Number}", item.Number, 0, 0, 0, item.At, end, schedule)
            { DrainOnEnd = true, StillValid = () => WorldEpoch == item.Epoch });
        }
        return result.ToArray();
    }
}

public sealed partial class ClientForm
{
    PacketSoundAssets? packetSoundAssets;
    IEnumerable<CombatSoundInstance> WithPacketSounds(IEnumerable<CombatSoundInstance> existing, long now)
    {
        packetSoundAssets ??= new(Environment.GetEnvironmentVariable("TW_CLIENT_ROOT") ?? @"D:\lineage開發\0-客戶端");
        return existing.Concat(login.World.PacketSoundSnapshot(now, packetSoundAssets.Duration));
    }
}
