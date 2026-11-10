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

// T280（R3-D，SD§2.3 A3FCBE..A3FD7B → A091C0）：發聲體每次更新的 (vol, ext)。
// nx=|dx|、ny=|dy|（格）；nx+ny>14 時夾成 (14,0)；vol＝100−14·(nx²+ny²)/20（夾 ≥0）；ext＝64−63·(nx+ny)/14（夾 ≥0）。
// A06D60 以 AIL_quick_set_volume(h, 類別音量·vol/100/128, ext/128) 套用；T310 從 Mss32.dll 核定第三參數會轉給
// AIL_set_sample_volume_pan 當 pan（0＝左、0.5＝中央、1＝右），播放層把它換成左右平衡（AudioGain.Balance）。
public readonly record struct EmitterLevel(int Volume, int Ext)
{
    /// <summary>A06C30 的預設 0.5f＝ext 64／128（發聲體在自己身上，或沒有發聲體）。</summary>
    public static EmitterLevel Self => new(100, 64);
    /// <summary>ext／128。名稱沿用 T302；T310 起確定它是 Miles pan，等同 <see cref="Pan"/>。</summary>
    public double ExtraVolume => Pan;
    public double Pan => Math.Clamp(Ext, 0, 128) / 128d;
}
public static class SoundAttenuation
{
    public static EmitterLevel Level(int dx, int dy)
    {
        long nx = Math.Abs((long)dx), ny = Math.Abs((long)dy);
        if (nx + ny > SoundRules.MaxManhattan) { nx = SoundRules.MaxManhattan; ny = 0; }
        long ext = 64 - 63 * (nx + ny) / SoundRules.MaxManhattan;
        return new(SoundRules.DistanceVolume(dx, dy), ext < 0 ? 0 : (int)ext);
    }
    /// <summary>offset＝發聲體相對自身角色的格數；null（沒有發聲體）用 A06C30 預設值。</summary>
    public static EmitterLevel Level((int Dx, int Dy)? offset) => offset is { } o ? Level(o.Dx, o.Dy) : EmitterLevel.Self;
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
        // R9-D（FU9§2.1 第 3 點，已驗證）：原版 Effect 不存對象指標，只看動畫播完才刪；
        // 對象消失不刪特效（不跟隨的留在 Origin、跟隨的在本段位置播完，見 PacketEffectFrames）。
        packetEffects.RemoveAll(e => now >= e.Expires);
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

    // T280（R3-D）：每次更新替每筆播放中的 S22 重算 (vol, ext)（A091C0 的時機：每次更新、播放之後）。
    // S22 封包沒有發聲體欄位（"ch"），原版走全域 A05980／A08020，不掛在角色發聲體上；所以這裡的發聲體就是自己，
    // 結果固定是 EmitterLevel.Self。改變時記 Changes，給快照與播放中改音量（T302，VoiceLevel）使用。
    readonly Dictionary<long, EmitterLevel> packetSoundLevels = [];
    public int PacketSoundLevelChanges { get; private set; }
    public EmitterLevel? PacketSoundLevel(long number) => packetSoundLevels.TryGetValue(number, out var level) ? level : null;
    void UpdatePacketSoundLevels(CombatSoundInstance[] sounds)
    {
        foreach (long gone in packetSoundLevels.Keys.Where(n => !sounds.Any(s => s.Instance == n)).ToArray()) packetSoundLevels.Remove(gone);
        foreach (var sound in sounds)
        {
            var level = SoundAttenuation.Level(sound.Actor == 0 ? null : SoundOffset(sound.Actor));
            if (!packetSoundLevels.TryGetValue(sound.Instance, out var old) || old != level) { packetSoundLevels[sound.Instance] = level; PacketSoundLevelChanges++; }
        }
    }
    // T302（R4-K，SD§2.3 A091C0 → A06D60）：把每次更新重算的 (vol, ext) 真的套到播放中的 voice。
    // S22 用 UpdatePacketSoundLevels 算好的值；有發聲體的戰鬥影格音效照同一公式即時算；
    // 發聲體或自身不在場回 null（A3FCBE 這時不呼叫 A091C0，音量維持原值）。
    // voice 比例＝類別音量·vol/100（A06D60）；主音量由 AudioCore 再乘。ext 交給播放層當 pan 套到裝置（T310）。
    public (double Scale, int Ext)? VoiceLevel(CombatSoundInstance sound, SoundVolumes volumes)
    {
        EmitterLevel level;
        if (sound.Key.StartsWith("s22/", StringComparison.Ordinal))
        {
            if (PacketSoundLevel(sound.Instance) is not { } packet) return null;
            level = packet;
        }
        else if (sound.Actor != 0 && SoundOffset(sound.Actor) is { } offset) level = SoundAttenuation.Level(offset.Dx, offset.Dy);
        else return null;
        return (volumes.Scale(sound.Category, level.Volume), level.Ext);
    }

    // T310（R5-E）：6016 地圖特效的發聲體就是特效所在格（EffectFrameState.X／Y，與角色同一座標系）。
    // 照 SD§2.3 同一公式；類別是 SoundVolume（影格 `<`／`[` 走 A060F0）。自身不在場回 null（不更新）。
    public (double Scale, int Ext)? EffectVoiceLevel(P24.MapEffectEvents.EffectFrameState frame, SoundVolumes volumes)
    {
        if (Actor is not { } self || SelfAppearancePending) return null;
        var level = SoundAttenuation.Level(frame.X - self.X, frame.Y - self.Y);
        return (volumes.Scale(SoundCategory.Sound, level.Volume), level.Ext);
    }

    [WorldSnapshot("packetSoundLevels")]
    object? PacketSoundLevelSnapshot() => packetSoundLevels.Count == 0 ? null
        : packetSoundLevels.OrderBy(p => p.Key).Select(p => new { number = p.Key, volume = p.Value.Volume, ext = p.Value.Ext }).ToArray();

    // T280（FU4§6）：頭目曲的兩個事件。觀察器先於物件處理器執行，物件表仍是封包前的狀態。
    // S87 物件出現（sub_A50F20）：不是自己、不在死亡狀態（動作 ≠ 8）→ SPAWN。格式錯誤留給原處理器。
    [WorldReceive(87, Observe = true)]
    void ObserveBossAppear(byte[] plain)
    {
        ushort gfx; byte action;
        try { var r = new LoginReader(plain); r.C(); r.H(); r.H(); uint id = r.D(); gfx = r.H(); action = r.C(); if (id == 0 || id == Status?.ObjectId) return; }
        catch (ProtocolException) { return; }
        if (action != 8) ClientSoundScene.BossAppeared(gfx, Environment.TickCount64);
    }
    // S158 動作 8（S_ACTION 92CB70）→ DEATH；gfx 取自物件表。
    [WorldReceive(158, Observe = true)]
    internal void ObserveBossDeath(byte[] plain)
    {
        uint id;
        try { var r = new LoginReader(plain); r.C(); id = r.D(); if (r.C() != 8) return; }
        catch (ProtocolException) { return; }
        if (objects.TryGetValue(id, out var o)) ClientSoundScene.BossDied(o.Gfx, Environment.TickCount64);
    }

    public CombatSoundInstance[] ComposePacketSounds(IEnumerable<CombatSoundInstance> existing, long now, Func<int,double?> duration)
    {
        var actions = existing.ToArray();
        var sounds = PacketSoundSnapshot(now,duration); // Also clears history at epoch boundaries.
        UpdatePacketSoundLevels(sounds);
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

// T310（R5-E，SD§2.3 A091C0）：6016 地圖特效每次更新的播放中音量。值變了才經 IEffectAudio 改音量入口送出；
// 消失的 owner 忘掉；回 null 的不送。換了 transport（主機重建）就整個重來。回傳本次送出的筆數。
public sealed class EffectVoiceLevels
{
    readonly Dictionary<P24.MapEffectEvents.EffectOwner, (double Scale, int Ext)> sent = [];
    IEffectAudio? target;
    public int Sent { get; private set; }
    public EffectAudioReply? LastFailure { get; private set; }
    public int Apply(IEffectAudio? audio, IEnumerable<P24.MapEffectEvents.EffectFrameState> frames,
        Func<P24.MapEffectEvents.EffectFrameState, (double Scale, int Ext)?> level)
    {
        if (!ReferenceEquals(audio, target)) { sent.Clear(); target = audio; }
        if (audio == null) return 0;
        var live = new HashSet<P24.MapEffectEvents.EffectOwner>();
        int count = 0;
        foreach (var frame in frames)
        {
            live.Add(frame.Owner);
            if (level(frame) is not { } next) continue;
            next = (double.IsFinite(next.Scale) ? Math.Clamp(next.Scale, 0, 1) : 0, Math.Clamp(next.Ext, 0, 128));
            if (sent.TryGetValue(frame.Owner, out var old) && old == next) continue;
            sent[frame.Owner] = next; count++;
            Task<EffectAudioReply> task;
            try { task = audio.SetVoiceLevelAsync(frame.Owner, next.Scale, next.Ext); }
            catch (Exception ex) { LastFailure = new(false, "HostCallFailed", ex.Message); continue; }
            _ = task.ContinueWith(t =>
            {
                var reply = t.IsCompletedSuccessfully ? t.Result : new EffectAudioReply(false, "TaskFailed", t.Exception?.Message);
                // NoLiveVoice／StaleOrUnknownOwner 是正常情況（特效此刻沒在發聲、或剛換代），不算失敗。
                if (!reply.Success && reply.Status is not ("NoLiveVoice" or "StaleOrUnknownOwner" or "Disposed")) LastFailure = reply;
            }, TaskScheduler.Default);
        }
        foreach (var gone in sent.Keys.Where(o => !live.Contains(o)).ToArray()) sent.Remove(gone);
        Sent += count;
        return count;
    }
}

public sealed partial class ClientForm
{
    PacketSoundAssets? packetSoundAssets;
    // 延遲建立：離線測試會用 GetUninitializedObject 建 ClientForm（不跑欄位初始化），仍要能走過這條路徑。
    EffectVoiceLevels? effect6016Levels;
    // T310：6016 主機與 PumpCombatSfx 同在 UI 計時器執行緒（Program.cs 的 Tick：Pump6016 → PumpCombatSfx），Frames() 的 lane 檢查成立。
    void Apply6016Levels(SoundVolumes volumes)
    {
        var levels = effect6016Levels ??= new();
        if (login.World.Effects6016 is not { Closed: false } host) { levels.Apply(null, [], _ => null); return; }
        try { levels.Apply(P24.HostAudio.AudioApiTransport.Current, host.Frames(), frame => login.World.EffectVoiceLevel(frame, volumes)); }
        catch (InvalidOperationException ex) { System.Diagnostics.Trace.WriteLine("6016 level: " + ex.Message); }
    }
    IEnumerable<CombatSoundInstance> WithPacketSounds(IEnumerable<CombatSoundInstance> existing, long now)
    {
        packetSoundAssets ??= new(Environment.GetEnvironmentVariable("TW_CLIENT_ROOT") ?? @"D:\lineage開發\0-客戶端");
        var composed = login.World.ComposePacketSounds(existing, now, packetSoundAssets.Duration);
        // T302：每次更新（A091C0 的時機）把重算後的音量套到上一輪已在播的 voice；這一輪新開的由 SoundGate 給初始音量。
        var volumes = SoundVolumes;
        combatSfx?.ApplyLevels(sound => login.World.VoiceLevel(sound, volumes));
        Apply6016Levels(volumes);
        return composed;
    }
}
