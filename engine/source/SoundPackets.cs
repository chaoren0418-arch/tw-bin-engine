using System.Security.Cryptography;
using System.Text.Json;
using P24.Audio;

namespace P06;

// T173: the first byte is a play mode (sound.md section 3), not a repeat count.
public sealed record ServerSound(byte Mode, ushort SoundId)
{
    public S22Mode? Kind => SoundRules.Mode(Mode);
}
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

    // T173 (sound.md section 3): mode 0 plays once (A05980), mode 1 loops as ambience (A08020),
    // mode 2 stops the id (A05A00) and has no schedule. Loop cycles reuse the WAV duration.
    public static SoundSchedule Schedule(ServerSound packet, double durationMs)
    {
        if (!double.IsFinite(durationMs) || durationMs <= 0 || durationMs > 60000)
            throw new InvalidDataException("S22 WAV duration outside supported range");
        return packet.Kind switch
        {
            S22Mode.Once => new([new SoundFrame(0, 0, [packet.SoundId])], durationMs, false),
            S22Mode.AmbientLoop => new([new SoundFrame(0, 0, [packet.SoundId])], durationMs, true),
            _ => throw new InvalidOperationException($"S22 mode {packet.Mode} has no play schedule")
        };
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
    // S22 has no actor/event identity. Keep combat schedules intact; match only a
    // singleton frame sound within +/-100 ms, one frame to one S22 first play.
    public const int PacketSoundMatchMs = 100;
    readonly Dictionary<(string Key,long Instance,int Frame,long Cycle), (int Id,double At,bool Used)> packetSoundFrames = [];
    readonly Dictionary<long,bool> packetSoundMatches = [];
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
            packetSoundFrames.Clear(); packetSoundMatches.Clear();
            packetMediaEpoch = WorldEpoch;
        }
        long now = PacketMediaClock();
        packetSounds.RemoveAll(s => s.Packet.Kind != S22Mode.AmbientLoop && now - s.At >= 256 * 60000L);
        packetEffects.RemoveAll(e => now >= e.Expires || e.Packet.Target is uint id && PacketEffectPosition(id) == null);
    }

    public PacketSound[] PacketSounds { get { PrunePacketMedia(); return packetSounds.ToArray(); } }

    internal void ReceiveSoundPacket(byte[] plain)
    {
        var packet = SoundPackets.Sound(plain); // Validate fully before changing state.
        EnsurePacketMedia();
        switch (packet.Kind)
        {
            case null:
                PacketMediaNotice = $"S22 音效 {packet.SoundId}，未知模式 {packet.Mode}；忽略";
                return;
            case S22Mode.Stop:
                int stopped = packetSounds.RemoveAll(s => s.Packet.SoundId == packet.SoundId);
                PacketMediaNotice = $"S22 停止音效 {packet.SoundId}（{stopped} 筆）";
                return;
            case S22Mode.AmbientLoop when packetSounds.Any(s => s.Packet.Kind == S22Mode.AmbientLoop && s.Packet.SoundId == packet.SoundId):
                PacketMediaNotice = $"S22 環境音 {packet.SoundId} 已在循環；不重複啟動";
                return;
        }
        packetSounds.Add(new(++packetSoundNumber, WorldEpoch, packet, PacketMediaClock()));
        if (packetSounds.Count > 256) packetSounds.RemoveAt(0);
        PacketMediaNotice = packet.Kind == S22Mode.AmbientLoop
            ? $"S22 環境音 {packet.SoundId} 循環；播放依已核定 WAV"
            : $"S22 音效 {packet.SoundId} 單次；播放依已核定 WAV";
    }

    public CombatSoundInstance[] PacketSoundSnapshot(long now, Func<int, double?> duration)
    {
        var result = new List<CombatSoundInstance>();
        var onceEnds = new Dictionary<ushort, long>();
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
            long start = item.At + PacketSoundMatchMs;
            if (schedule.Loop)
            {
                // Loops run until S22 mode 2, a world reset, or the dispatcher is disabled.
                result.Add(new($"s22/{item.Number}", item.Number, 0, 0, 0, start, long.MaxValue / 4, schedule)
                { Category = SoundCategory.EnvSound, CooldownFirstCycleOnly = true, StillValid = () => WorldEpoch == item.Epoch });
                continue;
            }
            long end = start + (long)Math.Ceiling(schedule.Duration);
            if (now >= end) { packetSounds.Remove(item); continue; }
            // A05EA0: while this id's single-shot instance is still playing, a new mode-0 packet is not played.
            if (onceEnds.TryGetValue(item.Packet.SoundId, out long playing) && start < playing)
            {
                PacketMediaNotice = $"S22 音效 {item.Packet.SoundId} 仍在播放；不重播";
                packetSounds.Remove(item);
                continue;
            }
            onceEnds[item.Packet.SoundId] = end;
            result.Add(new($"s22/{item.Number}", item.Number, 0, 0, 0, start, end, schedule)
            { DrainOnEnd = true, StillValid = () => WorldEpoch == item.Epoch });
        }
        return result.ToArray();
    }

    public CombatSoundInstance[] ComposePacketSounds(IEnumerable<CombatSoundInstance> existing, long now, Func<int,double?> duration)
    {
        var actions = existing.ToArray();
        var sounds = PacketSoundSnapshot(now,duration); // Also clears history at epoch boundaries.
        foreach(var key in packetSoundFrames.Where(p=>p.Value.At<now-2*PacketSoundMatchMs).Select(p=>p.Key).ToArray())
            packetSoundFrames.Remove(key);
        foreach(var key in packetSoundMatches.Keys.Where(n=>!sounds.Any(s=>s.Instance==n)).ToArray())
            packetSoundMatches.Remove(key);
        foreach(var action in actions)
        {
            var schedule=action.Schedule;
            if(now<action.StartedAt||now>=action.EndsAt||schedule.Duration<=0)continue;
            long cycle=schedule.Loop?(long)((now-action.StartedAt)/schedule.Duration):0;
            // Retain recent due frame events even if their action disappears next pump.
            for(long c=Math.Max(0,cycle-1);c<=cycle;c++)foreach(var frame in schedule.Frames)
            {
                double at=action.StartedAt+c*schedule.Duration+frame.At;
                if(frame.Candidates.Length!=1||at>now||at<now-2*PacketSoundMatchMs)continue;
                var key=(action.Key,action.Instance,frame.Index,c);
                if(packetSoundFrames.Count<1024)packetSoundFrames.TryAdd(key,(frame.Candidates[0],at,false));
            }
        }
        var result=new List<CombatSoundInstance>(actions);
        foreach(var sound in sounds)
        {
            if(now<sound.StartedAt)continue; // Wait for either packet arrival order.
            if(sound.Schedule.Loop){result.Add(sound);continue;} // Frame/S22 first-play dedup applies to single plays only.
            if(!packetSoundMatches.TryGetValue(sound.Instance,out bool matched))
            {
                int id=sound.Schedule.Frames[0].Candidates[0];
                double receivedAt=sound.StartedAt-PacketSoundMatchMs;
                var match=packetSoundFrames.Where(p=>!p.Value.Used&&p.Value.Id==id&&Math.Abs(p.Value.At-receivedAt)<=PacketSoundMatchMs)
                    .OrderBy(p=>Math.Abs(p.Value.At-receivedAt)).Take(1).ToArray();
                matched=match.Length!=0;
                if(matched){var row=match[0];packetSoundFrames[row.Key]=row.Value with{Used=true};}
                packetSoundMatches[sound.Instance]=matched;
            }
            result.Add(matched?sound with{Schedule=sound.Schedule with{Frames=sound.Schedule.Frames
                .Select((f,i)=>i==0?f with{Candidates=[]}:f).ToArray()}}:sound);
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
        return login.World.ComposePacketSounds(existing, now, packetSoundAssets.Duration);
    }
}
