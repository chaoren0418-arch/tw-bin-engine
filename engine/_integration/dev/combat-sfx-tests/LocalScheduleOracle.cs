using System.Text.Json;

// T465（#1050）：取代原本 git 追蹤的 expected-events.json。
// 有本機 assets 時，直接從本機 player profile 的原始 JSON 即時推導每個動作的音效排程 expected，
// 不經過被測的 SoundSchedule／ObjectAppearance（token 掃描與時間累加都在這裡另寫一份），
// 再拿去和 SoundSchedule.Create 的結果逐幀比對。repo 裡不留任何真實動作／時序資料。
static class LocalScheduleOracle
{
    public sealed record ExpectedFrame(int Index, double At, int[] Candidates);

    // B07（docs/reference/rea-inventory.md）：`<`／`[`／`^` 後接數字是聲音 token，依序最多存 5 個，
    // 第 6 個起讀掉不存但仍改模式；模式取最後一個 token；`^`（模式 2）整格不播；超過 16 位元的值存 0。
    public static int[] Tokens(string? suffix)
    {
        var ids = new List<int>();
        char mode = '\0';
        string s = suffix ?? "";
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if ((c == '<' || c == '[' || c == '^') && i + 1 < s.Length && char.IsAsciiDigit(s[i + 1]))
            {
                int j = i + 1; long value = 0;
                while (j < s.Length && char.IsAsciiDigit(s[j])) { value = Math.Min(value * 10 + (s[j] - '0'), 1_000_000L); j++; }
                mode = c;
                if (ids.Count < 5) ids.Add(value <= ushort.MaxValue ? (int)value : 0);
                i = j;
            }
            else i++;
        }
        return mode == '^' ? [] : ids.ToArray();
    }

    sealed record Step(string Key, int Ticks, int? Milliseconds, string Events);

    static Step[] Steps(JsonElement root, string action, int direction) =>
        root.GetProperty("sequences").EnumerateArray()
            .Single(s => s.GetProperty("action").GetString() == action && s.GetProperty("direction").GetInt32() == direction)
            .GetProperty("sequence").EnumerateArray()
            .Select(t => new Step(t.GetProperty("key").GetString()!, t.GetProperty("ticks").GetInt32(),
                t.TryGetProperty("milliseconds", out var ms) ? ms.GetInt32() : null,
                t.TryGetProperty("events", out var e) ? e.GetString() ?? "" : ""))
            .ToArray();

    public static string[] RawEvents(JsonElement root, string action, int direction) =>
        Steps(root, action, direction).Select(s => s.Events).ToArray();

    // 一般速度（無 ActionCadence）、非 pure／flight 的排程：全部影格有 milliseconds 時用毫秒；
    // 否則以動作總長（profile 的 actionMs，缺時取方向 0 的 ticks × 1000/24）按 ticks 比例分配。
    // 長度 0 的影格沒有可見停留，不進排程。
    public static List<ExpectedFrame> Expected(JsonElement root, string action, int direction)
    {
        var steps = Steps(root, action, direction);
        bool allMs = steps.All(s => s.Milliseconds != null);
        double totalTicks = steps.Sum(s => s.Ticks);
        double actionMs;
        if (root.TryGetProperty("actionMs", out var table) && table.TryGetProperty(action, out var v)) actionMs = v.GetDouble();
        else if (action == "walk") actionMs = root.GetProperty("stepMs").GetInt32();
        else actionMs = Steps(root, action, 0).Sum(s => s.Ticks) * 1000d / 24;
        var frames = new List<ExpectedFrame>();
        double at = 0;
        for (int i = 0; i < steps.Length; i++)
        {
            double duration = allMs ? steps[i].Milliseconds!.Value : actionMs * steps[i].Ticks / totalTicks;
            if (duration > 0) frames.Add(new(i, at, Tokens(steps[i].Events)));
            at += duration;
        }
        return frames;
    }
}
