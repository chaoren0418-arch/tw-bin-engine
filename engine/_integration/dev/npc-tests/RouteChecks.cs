using P06;
using System.Text.Json;

// Optional local-only check using the existing read-only asset junction. No render/window/game.
static class RouteChecks {
    public static void Run(string assets,string routeFile,string output){
        using var route=JsonDocument.Parse(File.ReadAllBytes(routeFile));var data=route.RootElement;
        int x=data.GetProperty("start")[0].GetInt32(),y=data.GetProperty("start")[1].GetInt32();
        var page=new NpcTemplates(Path.Combine(assets,"dialogs","templates.json")).Render("pandora",[],"潘朵拉","smoke");
        if(!page.Options.Any(o=>o.Enabled&&o.Kind=="action"&&o.Value=="buy"))throw new Exception("Pandora buy option unavailable");
        var cache=new OriginalMapCache(Path.GetFullPath(assets),x,y,0);int steps=0,shifts=0;
        foreach(var leg in data.GetProperty("legs").EnumerateArray()){
            int h=leg.GetProperty("heading").GetInt32();
            for(int n=0;n<leg.GetProperty("count").GetInt32();n++){
                if(!cache.Passable(x,y,h))throw new Exception($"Client cache blocks ({x},{y}) heading {h}");
                x+=MovementRules.Dx[h];y+=MovementRules.Dy[h];steps++;
                if(cache.Move(x,y))shifts++;
            }
            if(x!=leg.GetProperty("end")[0].GetInt32()||y!=leg.GetProperty("end")[1].GetInt32())throw new Exception("Route endpoint mismatch");
        }
        if(Math.Max(Math.Abs(x-32645),Math.Abs(y-32951))>3)throw new Exception("NPC out of talk range");
        File.WriteAllText(output,JsonSerializer.Serialize(new{status="pass",scope="唯讀正式 OriginalMapCache；未開啟遊戲／UI／網路",steps,shifts,pandoraBuyOption=true,end=new[]{x,y},cache=cache.Snapshot()},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS client cache route: {steps} steps, {shifts} shifts, end ({x},{y})");
    }
}
