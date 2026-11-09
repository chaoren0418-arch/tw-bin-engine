namespace P06;

// T173（#447）：音效共用規則層。依據 docs/reference/sound.md（T163／#431）的「已驗證」項目。
// 戰鬥影格音效、S22 封包、之後的 ^ 環境音（T166）與介面音效都經由 SoundGate 判定。
// 文件標「推測」的項目刻意不實作：Miles 第三參數 ext（extra volume）、x 座標雙倍精度（>>1）。

/// <summary>sound.md 4.1 的 4 類音量（lineage.cfg 的鍵名）。</summary>
public enum SoundCategory { Sound = 0, EnvSound = 1, Voice = 2, Music = 3 }

/// <summary>S22 第一個 byte 是播放模式，不是重複次數（sound.md 第 3 節）。</summary>
public enum S22Mode { Once = 0, AmbientLoop = 1, Stop = 2 }

/// <summary>4 類音量各自可調，值為 0..1 的正規化比例。原版類別音量的數值範圍未查證，故不綁定 0..100 或 0..127。</summary>
public sealed class SoundVolumes
{
    readonly double[] levels = [1, 1, 1, 1];
    public int Revision { get; private set; }
    public double this[SoundCategory category]
    {
        get => levels[Index(category)];
        set
        {
            if (!double.IsFinite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            int i = Index(category);
            if (levels[i] == value) return;
            levels[i] = value; Revision++;
        }
    }
    static int Index(SoundCategory category) => category is >= SoundCategory.Sound and <= SoundCategory.Music
        ? (int)category : throw new ArgumentOutOfRangeException(nameof(category));
    /// <summary>A06020：「略過類別檢查」為 0 時，音效、環境、語音三類都是 0 就不播。</summary>
    public bool EffectCategoriesSilent => levels[0] == 0 && levels[1] == 0 && levels[2] == 0;
    /// <summary>A06D60：類別音量 × 距離音量 / 100。</summary>
    public double Scale(SoundCategory category, int distanceVolume)
    {
        if (distanceVolume is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(distanceVolume));
        return this[category] * distanceVolume / 100d;
    }
}

public static class SoundRules
{
    public const int CooldownMs = 200;
    public const int MaxManhattan = 14;

    /// <summary>
    /// sound.md 2.3（A3FCBE..A3FD7B → A091C0）：nx=|dx|、ny=|dy|（格）；nx+ny&gt;14 時夾成 (14,0)；
    /// vol = 100 − 14·(nx²+ny²)/20（整數除法），負值夾為 0。
    /// x 是否需先 &gt;&gt;1 屬推測（sound.md 2.2），這裡 dx、dy 直接以格計。
    /// </summary>
    public static int DistanceVolume(int dx, int dy)
    {
        long nx = Math.Abs((long)dx), ny = Math.Abs((long)dy);
        if (nx + ny > MaxManhattan) { nx = MaxManhattan; ny = 0; }
        long volume = 100 - 14 * (nx * nx + ny * ny) / 20;
        return volume < 0 ? 0 : (int)volume;
    }

    /// <summary>未列在文件的模式值（3..255）回 null，由呼叫端忽略。</summary>
    public static S22Mode? Mode(byte value) => value <= 2 ? (S22Mode)value : null;

    /// <summary>模式 0 是 SoundVolume（A05980），模式 1 是 EnvSoundVolume（A08020）。</summary>
    public static SoundCategory Category(S22Mode mode) => mode == S22Mode.AmbientLoop ? SoundCategory.EnvSound : SoundCategory.Sound;
}

/// <summary>
/// sound.md 2.6／2.7：200ms 冷卻記在以 id 為鍵的全域 sample 快取物件上，
/// 每個音效 id 一份，跨角色、跨管線共用。<see cref="Shared"/> 是產品的單一實例。
/// </summary>
public sealed class SoundCooldown
{
    public static SoundCooldown Shared { get; } = new();
    readonly Dictionary<int, long> next = [];
    readonly object gate = new();
    public bool TryAcquire(int id, long now)
    {
        lock (gate)
        {
            if (next.TryGetValue(id, out long until) && now < until) return false;
            next[id] = now + SoundRules.CooldownMs;
            if (next.Count > 4096) foreach (int key in next.Where(p => p.Value <= now).Select(p => p.Key).ToArray()) next.Remove(key);
            return true;
        }
    }
    public void Clear() { lock (gate) next.Clear(); }
}

/// <param name="Offset">發聲體相對自身角色的格數；null 表示沒有位置（介面音效、S22），音量 100。</param>
/// <param name="Cooldown">是否套用 200ms 冷卻（A06020 第 4 參數）。</param>
/// <param name="SkipCategoryCheck">A06020 第 3 參數。</param>
/// <param name="RequireAudible">^ 的前提：發聲體音量為 0 時不啟動（A06210），也不佔用冷卻。</param>
public sealed record SoundRequest(int Id, SoundCategory Category, (int Dx, int Dy)? Offset = null,
    bool Cooldown = true, bool SkipCategoryCheck = false, bool RequireAudible = false);

public sealed record SoundAdmission(bool Dispatch, string Status, int DistanceVolume, double Scale);

/// <summary>所有音效派送前的單一判定點。</summary>
public sealed class SoundGate(SoundVolumes volumes, SoundCooldown cooldown, Func<long> clock)
{
    public SoundVolumes Volumes => volumes;
    public SoundAdmission Admit(SoundRequest request)
    {
        if (!request.SkipCategoryCheck && volumes.EffectCategoriesSilent) return new(false, "CategoriesSilent", 0, 0);
        int volume = request.Offset is { } o ? SoundRules.DistanceVolume(o.Dx, o.Dy) : 100;
        if (request.RequireAudible && volume == 0) return new(false, "OutOfRange", 0, 0);
        if (request.Cooldown && !cooldown.TryAcquire(request.Id, clock())) return new(false, "Cooldown", volume, 0);
        // 原版 <、[ 在音量 0 時仍會播放（靜音）並佔用冷卻；這裡不送出無聲 voice，但冷卻同樣已佔用。
        double scale = volumes.Scale(request.Category, volume);
        return scale <= 0 ? new(false, "Inaudible", volume, 0) : new(true, "Admitted", volume, scale);
    }
}

public sealed partial class ClientForm
{
    // 產品內唯一的類別音量與判定點；冷卻用全域 SoundCooldown.Shared。延遲建立，不依賴 Form 建構子。
    SoundVolumes? soundVolumes;
    SoundGate? soundGate;
    // T252（#613）：只有產品 ClientForm 會 Arm 背景音樂／環境音／`^` 後端；真正建立在第一次進入世界組畫面時（ProductSoundBackends.cs）。
    readonly bool productSoundArmed = ProductSound.Arm();
    // 戰鬥音效、S22 與背景音樂／環境音共用同一份 4 類音量。
    internal SoundVolumes SoundVolumes => soundVolumes ??= ClientSoundScene.Volumes;
    internal SoundGate SharedSoundGate => soundGate ??= new(SoundVolumes, SoundCooldown.Shared, () => Environment.TickCount64);
}

public sealed partial class WorldSession
{
    /// <summary>發聲體相對自身角色的格數；任何一方不在場時回 null。</summary>
    public (int Dx, int Dy)? SoundOffset(uint source)
    {
        if (Actor is not { } self || SelfAppearancePending) return null;
        var position = PacketEffectPosition(source);
        return position is { } p ? (p.X - self.X, p.Y - self.Y) : null;
    }
}
