using System.Text.Json;
namespace P06;
// Test controller only: original59/18 vectors modeled a pump at every deadline.
// Production Advance(now) must never call this helper or catch up missed pumps.
public static class OpacityHistoricalPumps {
    public static void Until(SceneOpacity state,long target){
        int pumps=0;
        while(true){
            var snapshot=JsonSerializer.SerializeToElement(state.Snapshot());
            var deadlines=snapshot.GetProperty("timers").EnumerateArray().Select(t=>t.GetProperty("at").GetInt64()).ToArray();
            if(deadlines.Length==0||deadlines.Min()>target)break;
            if(++pumps>10000)throw new Exception("Historical test pump exceeded bound");
            state.Advance(deadlines.Min());
        }
        state.Advance(target);
    }
}
