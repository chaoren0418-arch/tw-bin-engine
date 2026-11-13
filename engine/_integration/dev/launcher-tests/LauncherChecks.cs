using System.Diagnostics;
using System.Text.Json;
using P06.AssetPrep;

// CI only. Linked production launcher, synthetic files and process boundary interception; no UI/network/client.
internal static class LauncherChecks {
    static int checks;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;}
    static void Write(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(value));}
    static object Pin(string root,string relative)=>new{path=relative,size=new FileInfo(Path.Combine(root,relative)).Length,sha256=AssetPreparationState.Hash(Path.Combine(root,relative))};
    static void Package(string root,bool legacy=false){
        string[] files=["build/client-product-dev/P11.ClientEngine.exe","build/client-product-dev/P11.ClientEngine.dll","build/client-product-dev/P11.ClientEngine.runtimeconfig.json","build/client-product-dev/runtimes/win-x64/native/WebView2Loader.dll"];
        foreach(string f in files){string p=Path.Combine(root,f);Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllText(p,"synthetic "+f);}
        Write(Path.Combine(root,"build/product-dev-manifest.json"),new{schema="p24-normal-product-v1",entry=files[0],profile="p24-real-jar-d287-client2",port=42061,autoLogin=false,arguments=new[]{"--normal-product"},files=files.Concat(legacy?["assets/integrity.json"]:Array.Empty<string>()).Select(f=>Pin(root,f)).ToArray()});
    }
    static void Assets(string root,string state="ready",bool legacy=false){
        string assets=Path.Combine(root,"assets");Directory.CreateDirectory(assets);
        File.WriteAllText(Path.Combine(assets,"sample.txt"),"synthetic output");
        if(legacy)Write(Path.Combine(assets,"integrity.json"),new{schema="p06-assets-integrity-v1",files=new[]{Pin(assets,"sample.txt")}});
        else{
            Write(Path.Combine(assets,"prep-receipt.json"),new{schema="tw-asset-prep-set-v1",state,toolVersion="synthetic",recipes=new[]{new{id="synthetic",version=1}},knownLimits=Array.Empty<string>()});
            Write(Path.Combine(assets,"integrity.json"),new{schema="tw-assets-integrity-v2",receiptSha256=AssetPreparationState.Hash(Path.Combine(assets,"prep-receipt.json")),files=new[]{Pin(assets,"sample.txt")}});
        }
    }
    static int Main(string[] args){
        try{
            string root=Path.Combine(Path.GetFullPath(args[0]),"scratch","issue-1053","launcher-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            string client=Path.Combine(root,"原客戶端 空格");Directory.CreateDirectory(client);
            Directory.CreateDirectory(Path.Combine(client,"map"));
            foreach(string container in new[]{"Text","Tile","Sprite"}.Concat(Enumerable.Range(0,16).Select(i=>"Sprite"+i.ToString("D2"))))
                foreach(string ext in new[]{".idx",".pak"})File.WriteAllText(Path.Combine(client,container+ext),"synthetic layout");
            var evidence=new List<object>();
            void Case(string name,Action<string> test){string path=Path.Combine(root,name);Package(path);test(path);}
            (int Code,JsonElement Json,int Calls) Invoke(string path,string[] argv,string? clientRoot=null,Func<ProcessStartInfo,bool,int>? process=null){
                using var output=new StringWriter();int calls=0;
                int code=Program.Run(argv,path,clientRoot,output,(s,p)=>{calls++;return process?.Invoke(s,p)??throw new Exception("Unexpected process");},_=>new{runtime=new{synthetic=true},webView2Version="synthetic"});
                string text=output.ToString();evidence.Add(new{path=Path.GetFileName(path),args=argv,code,output=text,calls});
                JsonElement json=default;if(text.TrimEnd().EndsWith('}')){using var doc=JsonDocument.Parse(text.Trim().Split('\n')[^1]);json=doc.RootElement.Clone();}
                if(json.ValueKind!=JsonValueKind.Undefined)Check(!json.GetProperty("uiStarted").GetBoolean(),"JSON uiStarted=false");
                return(code,json,calls);
            }
            Case("unprepared",p=>{var r=Invoke(p,["--check-only"]);Check(r.Code==2&&r.Json.GetProperty("status").GetString()=="unprepared"&&r.Calls==0&&!Directory.Exists(Path.Combine(p,"runtime")),"clean check performs no preparation");});
            Case("interrupted",p=>{Directory.CreateDirectory(Path.Combine(p,"runtime/assets-staging/text"));var r=Invoke(p,["--check-only"]);Check(r.Code==2&&r.Json.GetProperty("status").GetString()=="interrupted"&&r.Calls==0,"interrupted staging check");});
            Case("partial",p=>{Assets(p,"partial");var r=Invoke(p,["--check-only"]);Check(r.Code==2&&r.Json.GetProperty("status").GetString()=="interrupted","partial receipt check");});
            Case("mismatch",p=>{Assets(p);File.AppendAllText(Path.Combine(p,"assets/prep-receipt.json")," ");var r=Invoke(p,["--check-only"]);Check(r.Code==2&&r.Json.GetProperty("status").GetString()=="receipt-mismatch","receipt SHA mismatch check");});
            Case("corrupt-json",p=>{Directory.CreateDirectory(Path.Combine(p,"assets"));File.WriteAllText(Path.Combine(p,"assets/integrity.json"),"bad");Check(Invoke(p,["--check-only"]).Json.GetProperty("status").GetString()=="receipt-mismatch","invalid JSON is classified");});
            Case("ready",p=>{Assets(p);var r=Invoke(p,["--check-only"],client);Check(r.Code==0&&r.Calls==0&&r.Json.GetProperty("assetReceipt").GetProperty("state").GetString()=="ready","ready check returns receipt without process");});
            Case("output-hash",p=>{Assets(p);File.WriteAllText(Path.Combine(p,"assets/sample.txt"),"synthetic outpuX");Check(Invoke(p,["--check-only"],client).Json.GetProperty("status").GetString()=="receipt-mismatch","same size altered output fails SHA");});
            foreach(string missing in new[]{"",Path.Combine(root,"missing"),"relative"})Case("bad-client-"+checks,p=>{Assets(p);var r=Invoke(p,[],missing);Check(r.Code==1&&r.Calls==0&&r.Json.GetProperty("message").GetString()!.Contains("啟動引擎.cmd"),"portable ready never falls back to developer client");});
            Case("wrong-existing-client",p=>{Assets(p);var r=Invoke(p,[],root);Check(r.Code==1&&r.Calls==0&&r.Json.GetProperty("message").GetString()!.Contains("Text.idx"),"unrelated existing directory is refused before UI");});
            Case("prepare-then-launch",p=>{
                var r=Invoke(p,[],client,(s,prepare)=>{
                    Check(s.WorkingDirectory==p&&!s.UseShellExecute&&s.Environment["TW_CLIENT_ROOT"]==client&&s.Environment["TW_ENGINE_ROOT"]==p,"process roots and environment");
                    if(prepare){Check(s.ArgumentList.SequenceEqual(["--prepare-assets"])&&s.CreateNoWindow&&s.RedirectStandardOutput&&s.RedirectStandardError,"preparation is file-only child");Assets(p);}
                    else Check(s.ArgumentList.SequenceEqual(["--normal-product"]),"only normal login entry after preparation");return 0;
                });Check(r.Code==0&&r.Calls==2,"prepare, recheck, then launch");
            });
            Case("prepare-failed",p=>{var r=Invoke(p,[],client,(_,_)=>2);Check(r.Code==1&&r.Calls==1,"failed prepare never launches UI");});
            Case("false-success",p=>{var r=Invoke(p,[],client,(_,_)=>0);Check(r.Code==1&&r.Calls==1,"zero exit without ready receipt never launches UI");});
            Case("still-partial",p=>{var r=Invoke(p,[],client,(_,_)=>{Assets(p,"partial");return 0;});Check(r.Code==1&&r.Calls==1,"partial after success never launches UI");});
            Case("repair-mismatch",p=>{Assets(p);File.AppendAllText(Path.Combine(p,"assets/prep-receipt.json")," ");var r=Invoke(p,[],client,(_,prepare)=>{if(prepare)Assets(p);return 0;});Check(r.Code==0&&r.Calls==2,"receipt mismatch requests preparation before launch");});
            Case("check-root",p=>{Assets(p);var r=Invoke(root,["--check-only","--root",p],client);Check(r.Code==0&&r.Calls==0,"explicit check root");});
            Case("payload-tamper",p=>{Assets(p);File.AppendAllText(Path.Combine(p,"build/client-product-dev/P11.ClientEngine.dll")," ");var r=Invoke(p,[],client);Check(r.Code==1&&r.Calls==0,"payload checked before process");});
            Case("legacy",p=>{
                Assets(p,legacy:true);Package(p,legacy:true);var r=Invoke(p,["--check-only"]);
                Check(r.Code==0&&r.Calls==0&&r.Json.GetProperty("assetState").GetString()=="legacy-ready"&&r.Json.GetProperty("hashedRuntimeAndWebFiles").GetInt32()==5,"legacy check keeps manifest/presence-size semantics without client env");
                Check(Invoke(p,[],process:(_,prepare)=>{Check(!prepare,"legacy skips prep");return 0;}).Code==0,"legacy still launches normally");
            });
            // CI Windows can create a real junction without symbolic-link privilege. Remove only the link, never recurse.
            if(OperatingSystem.IsWindows())Case("legacy-junction",p=>{
                string target=Path.Combine(root,"legacy-target");Assets(target,legacy:true);string link=Path.Combine(p,"assets");
                var mk=new ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true};mk.ArgumentList.Add("/c");mk.ArgumentList.Add("mklink");mk.ArgumentList.Add("/J");mk.ArgumentList.Add(link);mk.ArgumentList.Add(Path.Combine(target,"assets"));
                using(var child=Process.Start(mk)!){child.WaitForExit();Check(child.ExitCode==0,"create CI junction");}
                try{Package(p,legacy:true);Check(Invoke(p,["--check-only"]).Code==0,"legacy junction check unchanged");}
                finally{Directory.Delete(link,false);}
                Check(File.Exists(Path.Combine(target,"assets/sample.txt")),"junction target preserved");
            });
            Write(Path.Combine(root,"results.json"),new{checks,cases=evidence});Console.WriteLine($"PASS {checks} launcher checks; evidence: {root}");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
