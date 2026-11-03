namespace P06;
public sealed partial class ClientForm {
    HeadingConnection network=null!;WireLog wireLog=null!;int? fixturePort;bool networkMode;
    public int NetworkExitCode {get;private set;}
    int heartbeats,modeEpoch;
    string NetworkStatus=>network==null?"":$"{(networkMode?"測試通訊（站立轉向）":"離線模式")} • {network.State} • {network.Detail} • 等待={network.Busy} • 確認={network.Confirmed}\n"+
        "F6 連線/重連　F7 斷線/取消　F8 離線　僅 127.0.0.1 自有 fixture • TW16 轉向切片；P13 登入另用獨立真實服／參考 profile";
    void InitNetwork(string? port,string? scenario) {
        fixturePort=port==null?null:int.Parse(port);networkMode=fixturePort!=null;wireLog=new(evidence);
        network=new(wireLog);network.Changed+=()=>{if(!IsDisposed)RefreshScene();};
        network.Applied+=(d,seq)=>{if(!networkMode||World.Walking)throw new ProtocolException("model is not in stationary fixture state");
            var before=World.Snapshot();World.Face(d);wireLog.Add("model-applied",new{sequence=seq,before,after=World.Snapshot()});RefreshScene();Update();wireLog.Add("paint-submitted",new{sequence=seq,direction=World.Direction,frame=World.Frame.Key});};
        timer.Tick+=(_,_)=>heartbeats++;
        Shown+=async(_,_)=>{if(networkMode)await network.Connect(fixturePort!.Value);if(scenario!=null)await NetworkDemo(scenario);};
    }
    bool NetworkKey(Keys key) {
        if(key==Keys.F6){if(Input.Held.Add(key))_ = EnterNetwork();return true;}
        if(key==Keys.F7){modeEpoch++;network.Disconnect();Input.Clear();return true;}
        if(key==Keys.F8){modeEpoch++;network.Disconnect("explicit offline mode");networkMode=false;ClearInput("offline-mode");RefreshScene();return true;}
        if(!networkMode)return false;
        if(key>=Keys.D1&&key<=Keys.D8){if(Input.Held.Add(key)){wireLog.Add("ui-input",new{key=key.ToString(),direction=key-Keys.D1,model=World.Snapshot()});_ = network.Request(key-Keys.D1);}return true;}
        if(key is Keys.F or Keys.F12)return false;
        wireLog.Add("ui-input-blocked",new{key=key.ToString(),reason="stationary heading slice"});return true;
    }
    async Task EnterNetwork() {
        if(fixturePort==null){wireLog.Add("no-port");return;}
        int transition=++modeEpoch;network.Disconnect("mode transition");ClearInput("enter-network");networkMode=true;
        while(World.Walking&&!IsDisposed&&transition==modeEpoch)await Task.Delay(20);
        if(!IsDisposed&&transition==modeEpoch&&networkMode)await network.Connect(fixturePort.Value);
    }
    async Task Until(Func<bool> condition,int timeout=5000){var sw=System.Diagnostics.Stopwatch.StartNew();while(!condition()){if(sw.ElapsedMilliseconds>timeout)throw new Exception("UI condition timed out");await Task.Delay(10);}}
    async Task NetworkDemo(string scenario) {
        // Actual WinForms loop/controller harness, explicitly distinct from native keyboard evidence.
        var checks=new List<object>();void Check(string name,bool ok){checks.Add(new{name,pass=ok});if(!ok)throw new Exception(name);}
        try {
            if(evidence==null)throw new Exception("network demo requires --evidence");await Until(()=>webReady,15000);await Task.Delay(150);CaptureEvidence("01-before");
            if(scenario=="refused") {Check("connection refused is visible",network.State=="Faulted");Check("no model change",World.Direction==5);CaptureEvidence("02-refused");}
            else if(scenario=="unsolicited") {await Until(()=>network.State=="Faulted");Check("unsolicited response rejected",network.Detail.Contains("unsolicited"));CaptureEvidence("02-unsolicited");}
            else {
                Check("connected",network.Ready);int start=heartbeats;int before=World.Direction;
                HandleDown(Keys.D3);HandleUp(Keys.D3);await Task.Delay(80);
                if(scenario is "normal" or "timeout" or "cancel" or "close-pending") {Check("no optimistic model mutation",World.Direction==before);Check("request remains pending",network.Busy);CaptureEvidence("02-pending");}
                if(scenario=="close-pending"){Check("pending close",network.Busy);CaptureEvidence("03-close-pending");}
                else if(scenario=="cancel") {
                    network.Disconnect("demo cancellation");await Task.Delay(800);Check("cancel cleared pending",!network.Busy&&network.State=="Disconnected");Check("late response not applied beyond server delay",World.Direction==before);CaptureEvidence("03-cancelled");
                    await network.Connect(fixturePort!.Value);Check("reconnect for stale generation test",network.Ready);
                    HandleDown(Keys.D1);HandleUp(Keys.D1);await Task.Delay(60);int abandoned=network.Generation;
                    network.Disconnect("cancel pending and immediately reconnect");await network.Connect(fixturePort.Value);Check("new generation",network.Generation>abandoned);
                    HandleDown(Keys.D8);HandleUp(Keys.D8);await Until(()=>network.Confirmed==1);await Task.Delay(800);
                    Check("only new generation applied",World.Direction==7&&network.Confirmed==1&&network.Ready);CaptureEvidence("04-cancel-reconnect");
                }
                else if(scenario=="normal") {
                    await Until(()=>network.Confirmed==1);Check("response drives model",World.Direction==2);Check("UI ticked while pending",heartbeats-start>=5);Check("no displacement",World.X==32588&&World.Y==32928&&World.Steps==0);CaptureEvidence("03-confirmed");
                    foreach(int d in new[]{0,1,3,4,5,6,7,2}){int n=network.Confirmed;HandleDown(Keys.D1+d);HandleUp(Keys.D1+d);await Until(()=>network.Confirmed==n+1);Check($"direction {d} confirmed",World.Direction==d);CaptureEvidence($"direction-{d}");}
                    network.Disconnect("demo reconnect");Check("disconnect visible",network.State=="Disconnected");int generation=network.Generation;
                    await network.Connect(fixturePort!.Value);Check("reconnect reset",network.Ready&&network.Confirmed==0&&network.Generation>generation);
                    HandleDown(Keys.D5);HandleUp(Keys.D5);await Until(()=>network.Confirmed==1);Check("fresh stream after reset",World.Direction==4);CaptureEvidence("04-reconnected");
                    HandleDown(Keys.F8);HandleUp(Keys.F8);HandleDown(Keys.Up);await Task.Delay(70);HandleUp(Keys.Up);await Until(()=>!World.Walking);Check("offline movement retained",World.Steps==1&&World.X==32589&&World.Y==32927);CaptureEvidence("05-offline");
                    foreach(var cancel in new[]{Keys.F7,Keys.F8}) {
                        HandleDown(Keys.F8);HandleUp(Keys.F8);HandleDown(Keys.Up);await Task.Delay(50);HandleUp(Keys.Up);
                        Check("mode switch begins during active step",World.Walking);HandleDown(Keys.F6);HandleUp(Keys.F6);
                        HandleDown(Keys.F6);HandleUp(Keys.F6);HandleDown(cancel);HandleUp(cancel);int cancelledGeneration=network.Generation;
                        await Until(()=>!World.Walking);await Task.Delay(120);
                        Check($"walking F6 twice then {cancel} cannot reconnect",network.State=="Disconnected"&&network.Generation==cancelledGeneration);
                        Check($"{cancel} preserves mode",networkMode==(cancel==Keys.F7));
                    }
                    CaptureEvidence("06-mode-cancel-regression");
                } else {
                    await Until(()=>network.State=="Faulted");Check("failed connection leaves model unchanged",World.Direction==before);Check("error clears pending",!network.Busy);await Until(()=>heartbeats>start+2,1000);Check("UI responsive within 1 second",heartbeats>start+2);
                    if(scenario=="timeout")Check("specific timeout",network.Detail.Contains("timeout"));
                    if(scenario=="truncate")Check("specific truncation",network.Detail.Contains("truncated EOF"));
                    if(scenario=="drop")Check("specific disconnect",network.Detail.Contains("disconnected"));CaptureEvidence("03-fault");
                }
            }
            Json.Save(Path.Combine(evidence,"network-ui-result.json"),new{status="pass",scenario,checks,pid=Environment.ProcessId,heartbeats,kind="in-process WinForms controller harness; real TCP",nativeExternalInput=false});
        }catch(Exception ex){NetworkExitCode=3;Json.Save(Path.Combine(evidence!,"network-ui-result.json"),new{status="fail",scenario,checks,error=ex.ToString(),pid=Environment.ProcessId});}
        finally{Close();}
    }
}
