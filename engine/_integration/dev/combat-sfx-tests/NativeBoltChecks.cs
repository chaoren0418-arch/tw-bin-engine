using P06;
using P24.Audio;
using System.Reflection;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text.Json;
// Synthetic S30 only; no LoginConnection, ClientForm, socket, server or game process.
internal static class NativeBoltChecks
{
    static void Set(object o,string name,object value){var p=o.GetType().GetProperty(name);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(o,value);}
    internal static void Run(string assets,string client,string output)
    {
        var world=new WorldSession();
        var actor=new OwnActor(32590,32930,1,0,4,0,0,0,0,0,"test","",0,0,"","",0,100,0,1,0,0,0);
        Set(world,"Actor",actor);
        var objects=(ConcurrentDictionary<uint,WorldObject>)typeof(WorldSession).GetField("objects",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(world)!;
        objects[2]=new(32590,32930,2,1,0,0,0,0,0,0,"target","",0,0,"","",0,100,0,1,0,0,0);
        var audio=AudioPlayer.CreateCombat(client);var events=new ConcurrentQueue<CombatSoundObservation>();
        var controller=new CombatSfxController(audio,new Random(123),events.Enqueue);var scene=new CombatSfxScene(assets);
        var stages=new List<object>();
        try {
            foreach(var (name,muted) in new[]{("audible",false),("muted",true),("restored",false)}){
                controller.SetControls(.5,muted,true);
                if(!audio.ConfigureAsync(.5,muted,true).GetAwaiter().GetResult().Success)throw new Exception("Native controls failed");
                byte[] b=new byte[40];b[0]=30;b[1]=18;
                BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(2),1);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(6),2);BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(17),167);
                for(int i=20;i<=26;i+=2)BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(i),(ushort)(i%4==0?32590:32930));
                world.Receive(b,"test");
                var pump=Task.Run(async()=>{long until=Environment.TickCount64+3500;while(Environment.TickCount64<until){long now=Environment.TickCount64;controller.Pump(world.WorldEpoch,scene.Snapshot(world,now),now,true);await Task.Delay(20);}});
                var samples=Meter71.Sample((uint)Environment.ProcessId,4);pump.GetAwaiter().GetResult();
                float max=samples.Max(x=>x.peak??-1);
                if(samples.Any(x=>x.sessions==0)|| (muted?max!=0:max<=0))throw new Exception("Native bolt peak failed "+name+" peak="+max);
                stages.Add(new{name,muted,max,samples});
            }
        }finally{controller.Shutdown().GetAwaiter().GetResult();if(!audio.ShutdownWithinAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult().Success)throw new Exception("Native bolt cleanup incomplete");}
        File.WriteAllText(output,JsonSerializer.Serialize(new{status="pass",scope="獨立離線宿主：合成S30→真實scene/controller→pin97 WAV→本程序session量測；非遊戲驗收",stages,events},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        Console.WriteLine("PASS native bolt: audible >0, mute =0, restored >0; all own-session samples present");
    }
}
