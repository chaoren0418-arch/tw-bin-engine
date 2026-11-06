using P24.Audio;
using System.Text.Json;
internal static class NativeSessionChecks
{
    internal static void Run(string client,string output)
    {
        var player=AudioPlayer.ForTests(new(AudioAssets.LoadCombat(client),new FakeDriver(),32),new SilentAudioSession());
        var stages=new List<object>();string[]? identities=null;
        try {
            foreach(var (name,mute) in new[]{("enabled",false),("muted",true),("restored",false)}){
                var reply=player.ConfigureAsync(.5,mute,true).GetAwaiter().GetResult();
                if(!reply.Success)throw new Exception(JsonSerializer.Serialize(reply));
                var readings=Meter71.Sample((uint)Environment.ProcessId,2);
                if(readings.Any(r=>r.sessions<1||r.peak!=0))throw new Exception("Expected existing own session with zero PCM peak: "+name);
                identities??=readings[0].instances;
                if(readings.Any(r=>!r.instances.Order().SequenceEqual(identities.Order())))throw new Exception("Session rebuilt: "+name);
                stages.Add(new{name,readings});
            }
        }finally{
            if(!player.ShutdownWithinAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult().Success)throw new Exception("Native session cleanup incomplete");
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{status="pass",scope="獨立測試程序，實際零PCM stream；不執行遊戲、不播放遊戲音效",stages},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("PASS native session: 60 readings, present and zero; identity unchanged across mute/restore");
    }
}
