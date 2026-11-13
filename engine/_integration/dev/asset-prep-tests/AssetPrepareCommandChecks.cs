using P06.AssetPrep;
using System.Text.Json;

partial class Checks {
    [CheckGroup("asset-prepare-command"),CheckGroup("native-ui-resources",Order=67),CheckGroup(CheckGroups.Default)]
    static void AssetPrepareCommandChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-1053","command-"+Guid.NewGuid().ToString("N"));
        var fixture=SetClient(Path.Combine(root,"合成客戶端"));string engine=Path.Combine(root,"引擎 甲");
        var runner=new AssetPrepOrchestrator(fixture.Recipes);
        AssetPrepSetResult Run(AssetPrepSetRequest r)=>runner.Run(r with{Pins=fixture.Pins,SetPins=fixture.SetPins,EstimatedOutputBytes=16*1024*1024,AvailableSpace=_=>long.MaxValue});
        string clientBefore=ApTreeHash(fixture.Client);
        using var output=new StringWriter();
        var cases=new List<object>();
        int Invoke(string[]? args=null,string? client=null,Func<AssetPrepSetRequest,AssetPrepSetResult>? run=null){
            output.GetStringBuilder().Clear();
            string before=AssetPreparationState.Inspect(engine).State;
            int code=AssetPrepareCommand.Run(args??["--prepare-assets"],engine,client??fixture.Client,output,run:run??Run);
            cases.Add(new{before,code,output=output.ToString()});return code;
        }
        JsonElement Last(){using var doc=JsonDocument.Parse(output.ToString().Trim().Split('\n')[^1]);return doc.RootElement.Clone();}
        Check(AssetPreparationState.Inspect(engine).State=="unprepared"&&!Directory.Exists(engine),"COMMAND missing assets is read-only unprepared");
        Check(Invoke(client:"")==2&&Last().GetProperty("message").GetString()!.Contains("啟動引擎.cmd")&&!Directory.Exists(engine),"COMMAND absent TW_CLIENT_ROOT refuses before writes, names the setting");
        Check(Invoke(client:Path.Combine(root,"absent"))==2&&!Directory.Exists(engine),"COMMAND missing client directory refuses");
        Check(Invoke(["--prepare-assets","--normal-product"])==2&&!Directory.Exists(engine),"COMMAND rejects mixed startup args before UI or writes");
        Check(Invoke(run:r=>Run(r with{Progress=p=>{if(p.Completed==1)throw new OperationCanceledException();}}))==2&&Last().GetProperty("status").GetString()=="interrupted","COMMAND cancelled run reports interrupted");
        Check(AssetPreparationState.Inspect(engine).State=="interrupted"&&!Directory.Exists(Path.Combine(engine,"assets")),"COMMAND interrupted preparation publishes no assets");
        Check(Invoke()==0&&Last().GetProperty("status").GetString()=="prepared"&&Last().GetProperty("reused").GetInt32()>0,"COMMAND retries real recipes and reuses checkpoint");
        Check(AssetPreparationState.Inspect(engine).State=="ready"&&Last().GetProperty("receipt").GetProperty("recipes").GetArrayLength()==13,"COMMAND returns real thirteen-recipe ready receipt");
        foreach(string line in output.ToString().Trim().Split('\n')){
            using var doc=JsonDocument.Parse(line);var j=doc.RootElement;
            Check(!j.GetProperty("uiStarted").GetBoolean()&&!j.GetProperty("networkStarted").GetBoolean()&&!j.GetProperty("servicesStarted").GetBoolean(),"COMMAND progress and final output remain file-only");
        }
        File.WriteAllText(Path.Combine(root,"command-output.jsonl"),output.ToString());
        string receipt=Path.Combine(engine,"assets","prep-receipt.json");byte[] bytes=File.ReadAllBytes(receipt);
        File.AppendAllText(receipt," ");
        Check(AssetPreparationState.Inspect(engine).State=="receipt-mismatch","COMMAND altered receipt is rejected");
        // A zero/result return cannot bypass the post-run receipt read-back.
        Check(Invoke(run:_=>new(AssetPrepOutcome.UpToDate,Path.Combine(engine,"assets"),null!,0,0))==2&&Last().GetProperty("failure").GetString()=="Verification","COMMAND runner success without valid receipt fails closed");
        File.WriteAllBytes(receipt,bytes);
        Check(Invoke()==0&&Last().GetProperty("produced").GetInt32()==0,"COMMAND ready rerun is idempotent");
        Check(ApTreeHash(fixture.Client)==clientBefore,"COMMAND original synthetic client remains read-only");
        File.WriteAllText(Path.Combine(root,"command-cases.json"),JsonSerializer.Serialize(cases,AssetPreparationState.JsonOptions));
    }
}
