using System.Text.Json;
namespace P06;
public sealed record RealCase(string Account,string Password,string Expected,int Count,string Output,int HoldMs=10000,bool Reconnect=false);
public static class RealLoginTests {
    public static async Task Probe(string config){
        var spec=JsonSerializer.Deserialize<RealCase>(File.ReadAllText(config))!;
        using var c=new LoginConnection();var states=new List<object>();var packets=new List<object>();
        c.Changed+=()=>states.Add(new{utc=DateTimeOffset.UtcNow,snapshot=c.Snapshot()});
        c.PacketReceived+=p=>packets.Add(new{utc=DateTimeOffset.UtcNow,packet=p});
        async Task<Task> Begin(){
            var task=c.Run(42061,"p24-real-jar-d287-client2",spec.Account,spec.Password,10000);
            var limit=DateTime.UtcNow.AddSeconds(15);
            while(!task.IsCompleted&&c.State!=LoginState.CharacterList){if(DateTime.UtcNow>limit)throw new Exception("real login timeout");await Task.Delay(20);}
            return task;
        }
        var running=await Begin();var first=c.Snapshot();
        bool pass=c.State.ToString()==spec.Expected&&c.Characters.Count==spec.Count;
        Json.Save(spec.Output+".ready.json",new{pid=Environment.ProcessId,snapshot=first});
        if(c.State==LoginState.CharacterList)await Task.Delay(spec.HoldMs);
        c.Cancel();await running;var cancelled=c.Snapshot();
        object? second=null;
        if(spec.Reconnect){await Task.Delay(1500);running=await Begin();second=c.Snapshot();pass&=c.State==LoginState.CharacterList&&c.Characters.Count==spec.Count;await Task.Delay(500);c.Cancel();await running;}
        c.Cancel();
        Json.Save(spec.Output,new{status=pass?"pass":"fail",expected=spec.Expected,count=spec.Count,first,cancelled,second,states,packets,pid=Environment.ProcessId});
        if(!pass)throw new Exception("real login did not match expected state; see evidence");
    }
}
