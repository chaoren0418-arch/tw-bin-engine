using System.Text.Json;
namespace P06;
public static class CacheTests {
    public static int Dispatch(string root){
        var rows=Json.Read(Path.Combine(root,"reference/cache/native-dispatch-vectors.json")).GetProperty("rows");int count=0;
        foreach(var row in rows.EnumerateArray())foreach(ushort source in new ushort[]{0x1257,0x6b2a})foreach(ushort destination in new ushort[]{0x4a31,0x21ce}){
            var calls=row.GetProperty("dispatch");ushort expected=destination;if(calls.GetArrayLength()!=0){var call=calls[0];expected=call[0].GetString() switch{"screen"=>Renderer.ScreenBlend(source,destination),"multiply"=>Renderer.MultiplyBlend(source,destination),"half"=>Renderer.Blend(source,destination),"opaque"=>source,"weighted"=>Renderer.Weighted(source,destination,call[1].GetInt32()),_=>throw new Exception("unknown native dispatch")};}
            var draw=new Draw(0,0,0,1,"objects",0,0,0,new Tile(2,[new Pixel(0,0,source)]));ushort[] colors=[destination];bool[] mask=[true];Renderer.Blit(draw,new Rectangle(draw.Px,draw.Py,1,1),colors,mask,null,null,new OpacityValue(row.I("flags"),row.I("alpha"),0));if(colors[0]!=expected)throw new Exception("native opacity/pixel dispatch mismatch "+row.GetRawText());count++;
        }return count;
    }
    public static void Session(string output){
        Directory.CreateDirectory(output);string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));int dispatch=Dispatch(root);var capture=Json.Read(Path.Combine(root,"fixtures/p17-inventory-generation1.json"));var packets=capture.GetProperty("packets").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();string name=LoginConnection.CapturedCharacterName(packets);uint id=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(packets.First(p=>p[0]==8).AsSpan(1));
        var model=new WorldSession{CacheEnabled=true};long now=0;model.SceneClock=()=>now;bool entered=false;
        foreach(var original in packets){if(original[0]==223)entered=true;if(!entered)continue;byte[] p=(byte[])original.Clone();if(p[0]==87&&System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(p.AsSpan(5))==id){System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(1),32588);System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(3),32981);}model.ProbeSent=true;model.Receive(p,name);}
        if(!model.Renderable||model.MapCache==null)throw new Exception("cache session initialization");var assets=new Assets(Path.Combine(root,"assets"));using var renderer=new Renderer(assets);var world=new World(assets){AllowStreaming=true};var catalog=new ObjectAssets(assets.Root);var rows=new List<object>();
        foreach(int round in new[]{1,2}){var plan=Json.Read(Path.Combine(root,$"reference/cache/live-round{round}.json"));int i=0;
            foreach(var step in plan.GetProperty("legs").EnumerateArray().SelectMany(l=>l.GetProperty("steps").EnumerateArray())){now+=640;var from=step.A("source");var to=step.A("destination");if(model.Actor!.X!=from[0]||model.Actor.Y!=from[1]||!model.MapCache!.Passable(from[0],from[1],step.I("heading")))throw new Exception("cache planned terrain");model.Predict(to[0],to[1],step.I("heading"),true);if(++i%11!=0&&i!=plan.I("totalMoves"))continue;world.Teleport(to[0],to[1]);world.Face(step.I("heading"));var patch=renderer.ComposeObjects(world,model,catalog,out var visuals,out var missing,testTime:now);var center=world.DisplayPosition;var area=new Rectangle((int)center.X-320,(int)center.Y-220,640,400);var evidence=renderer.CaptureCacheFrame(patch,model.Opacity,area,Path.Combine(output,$"round{round}-step{i}"));using var bmp=renderer.SceneView(patch,area);bmp.Save(Path.Combine(output,$"round{round}-step{i}.png"));rows.Add(new{round,step=i,cache=model.MapCache.Snapshot(),evidence});}
        }
        model.Receive(Convert.FromHexString("CE04000000000000"),name);if(model.MapCache!=null||model.Opacity!=null||model.Renderable)throw new Exception("map generation leak");model.Reset();if(model.MapCache!=null||model.Opacity!=null)throw new Exception("cancel generation leak");var missingCache=new OriginalMapCache(assets.Root,0,0);if(missingCache.Scene.Count!=32768||missingCache.Scene.Any(c=>c.Layer!="ground"||c.Tile.Type is not (0 or 1)))throw new Exception("missing original fallback");
        Json.Save(Path.Combine(output,"result.json"),new{status="pass",kind="offline synthetic replay of original packets with explicit changed test coordinates; no live login",rows,originalDispatchPixelCases=dispatch,mapAndCancelClear=true,missingOriginalCells=32768});
    }
    public static void Opacity(string output){
        Directory.CreateDirectory(output);string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var vectors=Json.Read(Path.Combine(root,"reference/cache/native-cache-opacity-vectors.json"));var initial=vectors.A("initial");var cache=new OriginalMapCache(Path.Combine(root,"assets"),initial[0],initial[1]);var state=new SceneOpacity(cache.Catalog);var rows=new List<object>();
        static string[] States(JsonElement e)=>e.GetProperty("states").EnumerateArray().Select(x=>JsonSerializer.Serialize(x)).Order().ToArray();
        foreach(var step in vectors.GetProperty("steps").EnumerateArray()){
            var expected=step.GetProperty("expected");OpacityHistoricalPumps.Until(state,expected.GetProperty("time").GetInt64());switch(step.S("action")){case "move":var p=step.A("argument");state.Move(p[0],p[1],expected.GetProperty("time").GetInt64());break;case "tick":break;case "shift":cache.Shift(step.I("argument"));state.Rebind(cache.Catalog);break;}
            var actual=JsonSerializer.SerializeToElement(state.Snapshot());bool states=States(actual).SequenceEqual(States(expected));bool groups=actual.GetProperty("groups").EnumerateArray().Select(v=>v.GetString()).Order().SequenceEqual(expected.GetProperty("groups").EnumerateArray().Select(v=>v.GetString()).Order());bool timers=JsonSerializer.Serialize(actual.GetProperty("timers"))==JsonSerializer.Serialize(expected.GetProperty("timers"));rows.Add(new{action=step.S("action"),states,groups,timers,actual,expected=expected.Clone()});if(!states||!groups||!timers){Json.Save(Path.Combine(output,"result.json"),new{status="fail",rows,missing=States(expected).Except(States(actual)).Take(20),extra=States(actual).Except(States(expected)).Take(20)});throw new Exception("Original moving-cache opacity mismatch "+rows.Count);}
        }
        var ownCache=new OriginalMapCache(Path.Combine(root,"assets"),32588,32997);var ownState=new SceneOpacity(ownCache.Catalog);ownState.Move(32588,32997,0);OpacityHistoricalPumps.Until(ownState,500);OpacityScenario.VerifyStationaryObserver(ownState,ownCache);
        Json.Save(Path.Combine(output,"result.json"),new{status="pass",rows,originalStationaryObserverStates=143,observerUsesOwnColdCache=true});
    }
    public static void Pixels(string output){
        Directory.CreateDirectory(output);string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var expected=Json.Read(Path.Combine(root,"reference/cache/cache-pixel-goldens.json"));var assets=new Assets(Path.Combine(root,"assets"));using var renderer=new Renderer(assets);var cache=new OriginalMapCache(assets.Root,32588,32981);var rows=new List<object>();
        foreach(var step in expected.GetProperty("cases").EnumerateArray()){
            if(step.GetProperty("direction").ValueKind!=JsonValueKind.Null)cache.Shift(step.I("direction"));renderer.RenderCache(cache);var b=renderer.Bounds;if(!new[]{b.X,b.Y,b.Width,b.Height}.SequenceEqual(step.A("bounds")))throw new Exception("Original cache bounds");
            byte[] rgb=new byte[renderer.SceneColors.Length*2];Buffer.BlockCopy(renderer.SceneColors,0,rgb,0,rgb.Length);byte[] mask=renderer.SceneMask.Select(v=>(byte)(v?1:0)).ToArray();var refRgb=File.ReadAllBytes(Path.Combine(root,"reference/cache",step.S("rgb")));var refMask=File.ReadAllBytes(Path.Combine(root,"reference/cache",step.S("mask")));bool colors=rgb.AsSpan().SequenceEqual(refRgb),masks=mask.AsSpan().SequenceEqual(refMask);rows.Add(new{step=step.I("step"),pixels=mask.Length,colors,masks,bounds=b});Json.Save(Path.Combine(output,"result.json"),new{status=colors&&masks?"in-progress":"fail",rows});if(!colors||!masks)throw new Exception("Cache full original pixels or mask mismatch");
            var actor=step.I("step")==1?new[]{32588,33003}:new[]{32588,32981};var c=Coordinates.Screen(actor[0],actor[1]);var area=new Rectangle((int)c.X-320,(int)c.Y-220,640,400);var empty=new Patch(Rectangle.Empty,[],[],[],[]);using var bitmap=renderer.SceneView(empty,area);bitmap.Save(Path.Combine(output,$"step{step.I("step")}-viewport.png"));
        }
        Json.Save(Path.Combine(output,"result.json"),new{status="pass",rows});
    }
    public static void Run(string output){
        Directory.CreateDirectory(output);string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../..")),assets=Path.Combine(root,"assets");var vectors=Json.Read(Path.Combine(root,"reference/cache/native-control-vectors.json"));
        foreach(var v in vectors.GetProperty("cold").EnumerateArray()){var p=v.A("world");var o=OriginalMapCache.ColdOrigin(p[0],p[1]);if(!new[]{o.Item1,o.Item2}.SequenceEqual(v.A("origin")))throw new Exception("cold original mismatch");}
        foreach(var v in vectors.GetProperty("guard").EnumerateArray()){var p=v.A("world");var o=v.A("origin");int? want=v.GetProperty("direction").ValueKind==JsonValueKind.Null?null:v.I("direction");if(OriginalMapCache.Direction(o[0],o[1],p[0],p[1])!=want)throw new Exception("guard original mismatch");}
        var cases=Json.Read(Path.Combine(root,"reference/cache/scene-vectors.json"));var results=new List<object>();
        foreach(var suite in cases.GetProperty("suites").EnumerateArray()){
            var initial=suite.A("initial");var cache=new OriginalMapCache(assets,initial[0],initial[1]);var opacity=new SceneOpacity(cache.Catalog);int step=0;
            foreach(var v in suite.GetProperty("steps").EnumerateArray()){
                if(v.GetProperty("direction").ValueKind!=JsonValueKind.Null){cache.Shift(v.I("direction"));opacity.Rebind(cache.Catalog);}
                var actual=JsonSerializer.SerializeToElement(cache.SourceNodes());var wanted=Json.Read(Path.Combine(root,"reference/cache",v.S("nodes")));bool equal=actual.GetRawText()==JsonSerializer.Serialize(wanted);
                Json.Save(Path.Combine(output,$"{suite.S("name")}-{step}-cache.json"),cache.Snapshot());if(!equal){Json.Save(Path.Combine(output,$"{suite.S("name")}-{step}-actual.json"),actual);throw new Exception("Independent source cell/node mismatch "+suite.S("name")+":"+step);}
                results.Add(new{suite=suite.S("name"),step,nodes=actual.GetArrayLength(),equal});step++;
            }
        }
        Json.Save(Path.Combine(output,"result.json"),new{status="pass",cold=vectors.GetProperty("cold").GetArrayLength(),guard=vectors.GetProperty("guard").GetArrayLength(),results});
    }
}
