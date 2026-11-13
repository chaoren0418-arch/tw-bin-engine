using System.Text.Json;

namespace P06.AssetPrep;

/// <summary>File-only entry. Dispatched before ProductEntry, WinForms, assets readers or any network code.</summary>
public static class AssetPrepareCommand {
    public static bool TryRun(string[] args,out int exitCode){
        exitCode=0;
        if(!args.Contains("--prepare-assets"))return false;
        Console.OutputEncoding=new System.Text.UTF8Encoding(false);
        using var cancel=new CancellationTokenSource();
        ConsoleCancelEventHandler handler=(_,e)=>{e.Cancel=true;cancel.Cancel();};
        Console.CancelKeyPress+=handler;
        try{
            exitCode=Run(args,Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..")),
                Environment.GetEnvironmentVariable("TW_CLIENT_ROOT"),Console.Out,cancel.Token);
        }finally{Console.CancelKeyPress-=handler;}
        return true;
    }

    // The injectable runner is for offline CI with synthetic clients; no CLI switch can override product pins/recipes.
    public static int Run(string[] args,string engineRoot,string? clientRoot,TextWriter output,CancellationToken cancel=default,
        Func<AssetPrepSetRequest,AssetPrepSetResult>? run=null){
        void Write(object value)=>output.WriteLine(JsonSerializer.Serialize(value,AssetPreparationState.JsonOptions));
        try{
            if(args.Length!=1||args[0]!="--prepare-assets")throw new AssetPrepException(AssetPrepFailure.InvalidRequest,"--prepare-assets 必須單獨使用，不接受登入、UI 或診斷參數。");
            string client=AssetPreparationState.RequireClientRoot(clientRoot);
            var request=new AssetPrepSetRequest(client,engineRoot){Cancel=cancel,Progress=p=>Write(new{status="preparing",progress=p,uiStarted=false,networkStarted=false,servicesStarted=false})};
            var result=(run??(r=>AssetPrepOrchestrator.Product().Run(r)))(request);
            var state=AssetPreparationState.Inspect(engineRoot);
            if(state.State!="ready")throw new AssetPrepException(AssetPrepFailure.Verification,state.Message);
            Write(new{status="prepared",assetState=state.State,result.Outcome,result.Produced,result.Reused,receipt=state.Receipt,receiptSha256=state.ReceiptSha256,uiStarted=false,networkStarted=false,servicesStarted=false});
            return 0;
        }catch(OperationCanceledException){
            Write(new{status="interrupted",message="素材準備已取消；已完成的項目保留供下次續跑。",uiStarted=false,networkStarted=false,servicesStarted=false});return 2;
        }catch(Exception ex){
            string message=ex.Message;
            if(ex is AssetPrepException {Failure:AssetPrepFailure.ClientRoot or AssetPrepFailure.MissingContainer or AssetPrepFailure.ContainerMismatch or AssetPrepFailure.CorruptContainer or AssetPrepFailure.MissingEntry})message+=" "+AssetPreparationState.ClientRootHint;
            Write(new{status="prepare-failed",failure=ex is AssetPrepException e?e.Failure.ToString():ex.GetType().Name,message,uiStarted=false,networkStarted=false,servicesStarted=false});return 2;
        }
    }
}
