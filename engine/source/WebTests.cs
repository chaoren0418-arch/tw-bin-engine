using System.Text.Json;
namespace P06;
public sealed partial class ClientForm {
    async Task WebDemo(string scenario){
        if(scenario=="ground-layout"){await GroundLayoutDemo();return;}
        if(scenario=="inventory-layout"){await InventoryLayoutDemo();return;}
        if(scenario=="receive-load"){await ReceiveLoadDemo();return;}
        var checks=new List<object>();void Check(string name,bool pass){checks.Add(new{name,pass});if(!pass)throw new Exception(name);}
        async Task Script(string js){await web!.ExecuteScriptAsync(js);await Task.Delay(90);}
        async Task<bool> JsBool(string js)=>await web!.ExecuteScriptAsync(js)=="true";
        int Count(string kind)=>webJournal?.Count(kind)??0;
        try{
            if(scenario=="close-init"){Check("close invoked while real initialization incomplete",initializing is {IsCompleted:false});await ClosePanel();Check("initialization cancellation disposed controller",web==null&&!webReady);Check("initialization cancellation logged",Count("initialization-cancelled")>0);}
            else if(webFault!=null){
                if(webFault=="page-failed")await Until(()=>Count("navigation-completed")>0&&webNotice.Text.StartsWith("本機頁面載入失敗"),15000);
                else await Until(()=>Count("initialization-error")>0,15000);
                Check("observed actual failure remains visible without ready bridge",!webReady&&webNotice.Visible);ReturnGameFocus();HandleDown(Keys.D2);HandleUp(Keys.D2);Check("offline controller usable after panel failure",World.Direction==1);CaptureEvidence("web-failure");}
            else {
                await Until(()=>webReady,15000);await Task.Delay(200);Check("actual DOM receives model state",await JsBool("lastState.direction===5 && lastState.x===32588"));CaptureEvidence("web-ready");
                int before=World.Direction;int commands=Count("bridge-command");
                await web!.ExecuteScriptAsync("document.querySelector('#d2').click()");await Task.Delay(90);
                if(networkMode){Check("DOM request did not optimistically mutate",World.Direction==before&&network.Busy);CaptureEvidence("web-pending");await Until(()=>network.Confirmed==1);}
                else await Until(()=>World.Direction==2);
                await Task.Delay(120);Check("one DOM click dispatched once",Count("bridge-command")==commands+1);Check("DOM confirmed native heading",await JsBool("lastState.direction===2"));CaptureEvidence("web-confirmed");
                await Script("document.getElementById('combatResult').textContent='原服攻擊回報 #99999：1900240001 → 2000041347，傷害欄 15（非 HP 扣除量）'");
                Check("long original combat feedback wraps within panel",await JsBool("document.documentElement.scrollWidth<=document.documentElement.clientWidth"));
                await using(var png=File.Create(Path.Combine(WebEvidence,"web-combat-wrap.png")))await web!.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,png);
                int rejected=Count("bridge-rejected");await Script("for(const m of [null,{},[],{v:2,id:20,cmd:'ready'},{v:1,id:21,cmd:'shell'},{v:1,id:22,cmd:'face',direction:8},{v:1,id:23,cmd:'face',direction:'3'},{v:1,id:24,cmd:'face',direction:3,extra:1},{v:1,id:25,cmd:'face',direction:2.5}])chrome.webview.postMessage(m)");
                Check("nine invalid DOM messages rejected",Count("bridge-rejected")==rejected+9);
                DispatchWeb("https://untrusted.example/","{\"v\":1,\"id\":1,\"cmd\":\"face\",\"direction\":0}");DispatchWeb(LocalPage,new string('x',1025));DispatchWeb(LocalPage,"{\"v\":1,\"v\":1,\"id\":2,\"cmd\":\"ready\"}");
                Check("origin oversized duplicate rejected (host validation harness)",Count("bridge-rejected")==rejected+12&&World.Direction==2);
                int blocked=Count("navigation-blocked");web.CoreWebView2.Navigate("https://example.com/");await Task.Delay(100);Check("external navigation cancelled",Count("navigation-blocked")==blocked+1&&web.CoreWebView2.Source==LocalPage);
                await Script("window.open('https://example.com/');");Check("new window blocked",Count("new-window-blocked")>=1);
                if(networkMode){ProcessDevToolsKey(Keys.Control|Keys.Shift|Keys.F8,false);HandleUp(Keys.F8);}
                ReturnGameFocus();HandleDown(Keys.Up);await Task.Delay(60);Check("movement starts",World.Walking&&Input.Held.Count>0);
                web.Focus();await Script("document.querySelector('#notes').focus()");Check("real control focus clears held intent",Input.Held.Count==0&&World.Intent==null&&!GameInputAllowed);await Until(()=>!World.Walking);long steps=World.Steps;await Task.Delay(750);Check("current step finishes then stops",World.Steps==steps);
                await Script("document.querySelector('#game').click()");Check("page focus command restores game gate",GameInputAllowed);HandleDown(Keys.Right);HandleUp(Keys.Right);await Until(()=>!World.Walking);Check("movement resumes",World.Steps==steps+1);
                for(int i=0;i<3;i++){await ClosePanel();Check($"close {i} clears held",Input.Held.Count==0&&web==null);await OpenPanel();await Until(()=>webReady,15000);ClientSize=new(1050+i*40,720+i*10);await Task.Delay(80);int c=Count("bridge-command");await Script($"document.querySelector('#d{i}').click()");Check($"cycle {i} exactly one dispatch",Count("bridge-command")==c+1&&World.Direction==i);}
                CaptureEvidence("web-reopened");Check("page remains live after resize",await JsBool("lastState.direction===2"));
            }
            Json.Save(Path.Combine(WebEvidence,"web-test-result.json"),new{status="pass",scenario,fault=webFault,checks,nativeExternalInput=false,kind="real WebView2 DOM and WinForms controller harness",pid=Environment.ProcessId});
        }catch(Exception ex){NetworkExitCode=4;Json.Save(Path.Combine(WebEvidence,"web-test-result.json"),new{status="fail",scenario,fault=webFault,checks,error=ex.ToString()});}
        finally{Close();}
    }
}
