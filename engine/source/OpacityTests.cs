using System.Text.Json;
namespace P06;
public static class OpacityTests {
    public static void Run(string output){
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var vectors=Json.Read(Path.Combine(root,"reference/opacity/native-state-vectors.json"));var state=new SceneOpacity(OpacityCatalog.Shared.Value);var rows=new List<object>();
        static string[] States(JsonElement e)=>e.GetProperty("states").EnumerateArray().Select(x=>x.GetRawText().Replace(" ","").Replace("\r","").Replace("\n","")).Order().ToArray();
        var suites=new[]{vectors}.Concat(Json.Read(Path.Combine(root,"reference/opacity/live-failure-native-vectors.json")).GetProperty("suites").EnumerateArray());
        foreach(var suite in suites){state=new SceneOpacity(OpacityCatalog.Shared.Value);
        foreach(var step in suite.GetProperty("steps").EnumerateArray()){
            var expected=step.GetProperty("expected");OpacityHistoricalPumps.Until(state,expected.GetProperty("time").GetInt64());if(step.S("action")=="move"){var xy=step.A("argument");state.Move(xy[0],xy[1],expected.GetProperty("time").GetInt64());}
            var actual=JsonSerializer.SerializeToElement(state.Snapshot());var a=States(actual);var b=States(expected);bool states=a.SequenceEqual(b);bool groups=actual.GetProperty("groups").EnumerateArray().Select(v=>v.GetString()).Order().SequenceEqual(expected.GetProperty("groups").EnumerateArray().Select(v=>v.GetString()).Order());
            bool timers=JsonSerializer.Serialize(actual.GetProperty("timers"))==JsonSerializer.Serialize(expected.GetProperty("timers"));
            rows.Add(new{action=step.S("action"),expected=expected.Clone(),actual,states,groups,timers});if(!states||!groups||!timers){Json.Save(output,new{status="fail",rows,missing=b.Except(a).Take(10),extra=a.Except(b).Take(10)});throw new Exception("Original x86 opacity mismatch at "+rows.Count);}
        }
        }
        var data=OpacityCatalog.Shared.Value;int duplicate=data.Cells.Values.Count(t=>t.Length>1);if(duplicate==0)throw new Exception("lost trigger multiplicity");
        var reset=new SceneOpacity(data);var empty=JsonSerializer.SerializeToElement(reset.Snapshot());if(empty.GetProperty("states").GetArrayLength()!=0||empty.GetProperty("groups").GetArrayLength()!=0)throw new Exception("fresh state polluted");
        int colorCases=0;for(int k=0;k<32;k++)for(int a=0;a<32;a++)for(int b=0;b<32;b++){int v=(a*(31-k)+k*b)/31;ushort src=(ushort)(b|(b<<5)|(b<<10)),dst=(ushort)(a|(a<<5)|(a<<10));if(Renderer.Weighted(src,dst,k)!=(ushort)(v|(v<<5)|(v<<10)))throw new Exception("integer31 blend");colorCases++;}
        Json.Save(output,new{status="pass",kind="product versus independently executed original x86 state kernels",rows,duplicateCells=duplicate,colorCases,scope="time transitions, old/current cell, multiplicity, overlapping groups, type0/type1, leave/switch/reenter/reset; separate pixel native oracle follows"});
    }
}
