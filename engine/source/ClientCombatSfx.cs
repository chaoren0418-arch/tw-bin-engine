using P24.Audio;
using System.Text.Json;

namespace P06;

public sealed partial class ClientForm
{
    CombatSfxController? combatSfx;
    CombatSfxScene? combatSfxScene;
    bool combatSfxUnavailable;
    void RecordCombatSfx(CombatSoundObservation item)
    {
        lock (audioLogGate)
        {
            try
            {
                string directory = Path.Combine(WebEvidence, "audio");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "combat-sfx.jsonl"),
                    JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, item }) + Environment.NewLine);
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("Combat audio diagnostics: " + ex.Message); }
        }
    }
    void PumpCombatSfx()
    {
        bool enabled = login.World.Renderable && !login.World.OwnDead && !TerminalAudioState && !webClosing && !Disposing && !IsDisposed;
        if (combatSfx == null && enabled && !combatSfxUnavailable)
        {
            try
            {
                combatSfxScene = new(World.Assets.Root);
                string clientRoot = Environment.GetEnvironmentVariable("TW_CLIENT_ROOT") ?? @"D:\lineage開發\0-客戶端";
                combatSfx = new(AudioPlayer.CreateCombat(clientRoot), Random.Shared, RecordCombatSfx)
                { Gate = SharedSoundGate, Offset = id => login.World.SoundOffset(id) };
            }
            catch (Exception ex)
            {
                combatSfxUnavailable = true;
                RecordCombatSfx(new("create", "", null, null, null, new(false, "Unavailable", null, 0, [new("Exception", "wav", ex.Message)])));
            }
        }
        if (combatSfx == null) return;
        combatSfx.SetControls(masterGain, masterMute, enabled);
        long now = Environment.TickCount64;
        try { combatSfx.Pump(login.World.WorldEpoch, WithPacketSounds(combatSfxScene!.Snapshot(login.World, now), now), now, enabled); }
        catch (Exception ex)
        {
            combatSfx.SetControls(masterGain, masterMute, false);
            combatSfx.Pump(login.World.WorldEpoch, [], now, false);
            RecordCombatSfx(new("advance", "", null, null, null, new(false, "Unavailable", null, 0, [new("Exception", "wav", ex.Message)])));
        }
    }
    void InitCombatSfx()
    {
        login.Changed += PumpCombatSfx;
        login.World.CombatSoundStateChanged += PumpCombatSfx;
        FormClosing += (_, _) => { if (webClosing && combatSfx != null) _ = combatSfx.Shutdown(); };
        FormClosed += (_, _) => { if (combatSfx != null) _ = combatSfx.Shutdown(); };
    }
    Task ShutdownCombatSfx() => combatSfx?.Shutdown() ?? Task.CompletedTask;
}
