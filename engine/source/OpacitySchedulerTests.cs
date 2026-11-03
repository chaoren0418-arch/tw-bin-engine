using System.Text.Json;
namespace P06;
public static class OpacitySchedulerTests {
    public static void Run(string output){
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));
        string vectorPath=Path.Combine(root,"reference/cache/scheduler-r2/native-late-fade-vectors.json");var vectors=Json.Read(vectorPath);
        var rows=new List<object>();int cases=0,steps=0,mismatches=0;object? firstFailure=null;
        const string key="native-one-group";
        foreach(var suite in vectors.GetProperty("cases").EnumerateArray()){
            var catalog=new OpacityCatalog([new OpacityNode("objects",0,suite.I("initialFlags"))],
                [new OpacityGroup(key,"synthetic",0,17,0,[0])],
                new Dictionary<(int,int),OpacityTrigger[]>{[(0,0)]=[new(key,1,0)],[(2,0)]=[new(key,2,0)]});
            var state=new SceneOpacity(catalog);var draw=new Draw(0,0,0,0,"objects",0,0,0,new Tile(2,[new Pixel(0,0,0)]));
            long now=0;int index=0;cases++;
            foreach(var step in suite.GetProperty("steps").EnumerateArray()){
                string action=step.S("action");
                if(action=="advance"){now=step.GetProperty("argument").GetInt64();state.Advance(now);}
                else if(action=="enter")state.Move(Coordinates.MinX+(step.I("argument")==1?0:1),Coordinates.MinY,now);
                else if(action=="leave")state.Move(Coordinates.MinX+2,Coordinates.MinY,now);
                else throw new Exception("Unknown native scheduler action");
                var snapshot=JsonSerializer.SerializeToElement(state.Snapshot());var value=state.Get(draw);var expected=step.GetProperty("expected");
                var timers=snapshot.GetProperty("timers").EnumerateArray().Select(t=>new{at=t.GetProperty("at").GetInt64(),direction=t.S("direction")}).ToArray();
                bool active=snapshot.GetProperty("groups").EnumerateArray().Any(g=>g.GetString()==key);
                long actualTime=snapshot.GetProperty("time").GetInt64();var actual=new{time=actualTime,flags=value.Flags,alpha=value.Alpha,target=value.Target,active,timers};
                bool equal=value.Flags==expected.I("flags")&&value.Alpha==expected.I("alpha")&&value.Target==expected.I("target")&&active==expected.GetProperty("active").GetBoolean()&&actualTime==expected.GetProperty("time").GetInt64()&&JsonSerializer.Serialize(timers)==JsonSerializer.Serialize(expected.GetProperty("timers"));
                var row=new{suite=suite.S("name"),initialFlags=suite.I("initialFlags"),step=index++,action,argument=step.GetProperty("argument").Clone(),equal,actual,expected=expected.Clone()};
                rows.Add(row);steps++;if(!equal){mismatches++;firstFailure??=row;}
            }
        }
        if(cases!=20||steps!=215)throw new Exception("Native scheduler vector coverage changed");
        string dll=typeof(SceneOpacity).Assembly.Location;
        Json.Save(output,new{status=mismatches==0?"pass":"fail",cases,steps,mismatches,firstFailure,productAssembly=dll,productDllSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dll))).ToLowerInvariant(),oracleSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(vectorPath))).ToLowerInvariant(),oracle="Unmodified original scheduler and fade kernels; independent synthetic one-group/node; direct single Advance(now), no historical catch-up helper",rows});
        if(mismatches!=0)throw new Exception("Original scheduler direct-pump mismatch: "+mismatches+" of "+steps);
    }
}
