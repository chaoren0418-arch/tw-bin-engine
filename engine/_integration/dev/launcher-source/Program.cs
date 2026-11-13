using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using P06.AssetPrep;

internal static class Program {
        const string ManifestPath="build/product-dev-manifest.json";
    const string Entry="build/client-product-dev/P11.ClientEngine.exe";
    [UnmanagedFunctionPointer(CallingConvention.StdCall,CharSet=CharSet.Unicode)]
    delegate int BrowserVersion([MarshalAs(UnmanagedType.LPWStr)] string? folder,out IntPtr version);
    static int Main(string[] args){
        Console.OutputEncoding=new UTF8Encoding(false);
        return Run(args,Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..")),Environment.GetEnvironmentVariable("TW_CLIENT_ROOT"),Console.Out);
    }
    // Dependencies can be replaced by the offline CI harness, never by user-facing command-line options.
    internal static int Run(string[] args,string defaultRoot,string? clientRoot,TextWriter output,
        Func<ProcessStartInfo,bool,int>? process=null,Func<string,object>? platform=null){
        void Write(object value)=>output.WriteLine(JsonSerializer.Serialize(value,AssetPreparationState.JsonOptions));
        try {
            bool check=args.Length>0&&args[0]=="--check-only";
            string root=Path.GetFullPath(defaultRoot);
            // T411 (#948): --evidence <dir> is forwarded to the product (which checks the directory is inside the project folder).
            // The product's working directory is the product root, so a relative path is made absolute against the caller's directory first.
            string? evidence=args.Length==2&&args[0]=="--evidence"&&!string.IsNullOrWhiteSpace(args[1])&&!args[1].StartsWith("--",StringComparison.Ordinal)?Path.GetFullPath(args[1]):null;
            if(args.Length==3&&check&&args[1]=="--root")root=Path.GetFullPath(args[2]);
            else if(args.Length!=0&&!(args.Length==1&&check)&&evidence==null)throw new InvalidDataException("只接受 --check-only、--check-only --root <完整產品目錄>，或 --evidence <證據目錄>；其他參數不會啟動產品。");
            var product=VerifyProduct(root);
            var assets=AssetPreparationState.Inspect(root,product.LegacyIntegritySha256);
            if(check){
                // A clean extraction can report its state even before WebView2 is installed. Never starts preparation.
                if(!assets.Ready){Write(CheckReceipt(root,product,assets,null));return 2;}
                if(assets.State!="legacy-ready")AssetPreparationState.RequireClientRoot(clientRoot);
                Write(CheckReceipt(root,product,assets,(platform??VerifyPlatform)(root)));return 0;
            }
            // Portable input is explicit, including when assets are already ready (providers still read the client).
            string? client=assets.State=="legacy-ready"?clientRoot:AssetPreparationState.RequireClientRoot(clientRoot);
            if(!assets.Ready){
                Write(new{status="preparing",assetState=assets.State,message=assets.Message,uiStarted=false,networkStarted=false,servicesStarted=false});
                var prepare=new ProcessStartInfo(Path.Combine(root,Entry)){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
                prepare.ArgumentList.Add("--prepare-assets");
                prepare.Environment["TW_CLIENT_ROOT"]=client!;prepare.Environment["TW_ENGINE_ROOT"]=root;
                int code=process!=null?process(prepare,true):RunPreparation(prepare,output);
                if(code!=0)throw new InvalidDataException($"素材準備未完成（結束碼 {code}）；請依上方訊息修正後重試，已完成項目可續跑。"+AssetPreparationState.ClientRootHint);
                // Exit code alone is never enough: read back the committed receipt and every v2 output hash.
                assets=AssetPreparationState.Inspect(root,product.LegacyIntegritySha256);
                if(assets.State!="ready")throw new InvalidDataException("準備程序結束但沒有可用的 ready 收據："+assets.Message);
                product=VerifyProduct(root);
            }
            _=(platform??VerifyPlatform)(root);
            var start=new ProcessStartInfo(Path.Combine(root,Entry)){WorkingDirectory=root,UseShellExecute=false};
            start.ArgumentList.Add("--normal-product");
            start.Environment["TW_ENGINE_ROOT"]=root;
            if(client!=null)start.Environment["TW_CLIENT_ROOT"]=client;
            if(evidence!=null){start.ArgumentList.Add("--evidence");start.ArgumentList.Add(evidence);}
            if(process!=null){if(process(start,false)!=0)throw new InvalidOperationException("無法啟動已驗證的客戶端。");}
            else{using var child=Process.Start(start)??throw new InvalidOperationException("無法啟動已驗證的客戶端。");}
            output.WriteLine("已啟動 dev 客戶端；請在正常登入畫面手動登入。");return 0;
        }catch(Exception ex){Write(new{status="failed",message="啟動檢查失敗："+ex.Message,uiStarted=false,networkStarted=false,servicesStarted=false});return 1;}
    }
    static int RunPreparation(ProcessStartInfo start,TextWriter output){
        using var child=Process.Start(start)??throw new InvalidOperationException("無法啟動素材準備程序。");
        // Drain both pipes concurrently. The WinExe's file-only mode inherits these redirected handles.
        async Task Pump(StreamReader reader){while(await reader.ReadLineAsync() is {} line)lock(output)output.WriteLine(line);}
        Task streams=Task.WhenAll(Pump(child.StandardOutput),Pump(child.StandardError));
        child.WaitForExit();streams.GetAwaiter().GetResult();return child.ExitCode;
    }
    static string Hash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();}
    static string Resolve(string root,string relative){
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))throw new InvalidDataException("產品檔案路徑無效。");
        string path=Path.GetFullPath(Path.Combine(root,relative));
        string prefix=Path.TrimEndingDirectorySeparator(root)+Path.DirectorySeparatorChar;
        if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("產品檔案路徑超出產品目錄。");
        return path;
    }
    static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
    sealed record ProductVerification(string Manifest,string ManifestHash,int Hashed,string? LegacyIntegritySha256,string[] Arguments);
    static ProductVerification VerifyProduct(string root){
        string manifest=Resolve(root,ManifestPath);
        Require(File.Exists(manifest),"缺少 dev 產品 manifest；請使用完整交付目錄。");
                // dev: manifest 本身不再以編譯期常數釘住；完整性由 manifest 內每個檔案的 size/sha256 核對保證。
        string manifestHash=Hash(manifest);
        using var document=JsonDocument.Parse(File.ReadAllBytes(manifest));var m=document.RootElement;
        Require(m.GetProperty("schema").GetString()=="p24-normal-product-v1"&&m.GetProperty("entry").GetString()==Entry&&m.GetProperty("profile").GetString()=="p24-real-jar-d287-client2"&&m.GetProperty("port").GetInt32()==42061&&!m.GetProperty("autoLogin").GetBoolean(),"產品版本設定不一致。");
        var arguments=m.GetProperty("arguments").EnumerateArray().Select(x=>x.GetString()!).ToArray();
        Require(arguments.SequenceEqual(new[]{"--normal-product"}),"正常入口參數不符。");
        var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int hashed=0;string? legacyHash=null;
        foreach(var pin in m.GetProperty("files").EnumerateArray()){
            string relative=pin.GetProperty("path").GetString()!;Require(files.Add(relative),"manifest 有重複檔案。");
            // Generated v2 assets are validated by their receipt, not pinned into the distributable manifest.
            // Preserve the package pin as the mandatory gate for legacy v1 (including development junctions).
            if(relative=="assets/integrity.json"){legacyHash=pin.GetProperty("sha256").GetString();continue;}
            string path=Resolve(root,relative);Require(File.Exists(path),"缺少產品檔案："+relative);
            Require(new FileInfo(path).Length==pin.GetProperty("size").GetInt64()&&Hash(path)==pin.GetProperty("sha256").GetString(),"產品檔案已變更："+relative);hashed++;
        }
        Require(files.Contains(Entry)&&files.Contains("build/client-product-dev/P11.ClientEngine.dll")&&files.Contains("build/client-product-dev/P11.ClientEngine.runtimeconfig.json"),"manifest 缺少必要入口或執行期設定。");
        string loaderRelative="build/client-product-dev/runtimes/win-x64/native/WebView2Loader.dll";
        Require(files.Contains(loaderRelative),"manifest 缺少 WebView2 loader。");
        return new(manifest,manifestHash,hashed,legacyHash,arguments);
    }
    static object VerifyPlatform(string root){
        Require(OperatingSystem.IsWindows()&&RuntimeInformation.ProcessArchitecture==Architecture.X64,"此啟動器需要 Windows x64 與 .NET 10 Runtime。");
        return new{runtime=DesktopRuntime(root),webView2Version=WebViewVersion(Resolve(root,"build/client-product-dev/runtimes/win-x64/native/WebView2Loader.dll"))};
    }
    static object CheckReceipt(string root,ProductVerification product,AssetPreparationStatus assets,object? platform){
        var environment=platform==null?default:JsonSerializer.SerializeToElement(platform);
        return new{status=assets.Ready?"checked-only":assets.State,revision="dev",executable=Resolve(root,Entry),workingDirectory=root,arguments=product.Arguments,manifest=product.Manifest,manifestSha256=product.ManifestHash,hashedRuntimeAndWebFiles=product.Hashed+(assets.State=="legacy-ready"?1:0),assetFilesCheckedForPresenceAndSize=assets.Files,assetContentVerification=assets.State=="legacy-ready"?"Pinned integrity manifest; body hashes remain verified by the product when loaded, matching prior entry contract.":"Prepared receipt SHA-256 and every indexed output size/SHA-256.",assetState=assets.State,message=assets.Message,assetReceipt=assets.Receipt,assetReceiptSha256=assets.ReceiptSha256,runtime=platform==null?(JsonElement?)null:environment.GetProperty("runtime"),webView2Version=platform==null?(JsonElement?)null:environment.GetProperty("webView2Version"),powerShellRequired=false,pathLookupUsed=false,autoLogin=false,uiStarted=false,networkStarted=false,servicesStarted=false};
    }
    static object DesktopRuntime(string root){
        using var config=JsonDocument.Parse(File.ReadAllBytes(Resolve(root,"build/client-product-dev/P11.ClientEngine.runtimeconfig.json")));
        var frameworks=config.RootElement.GetProperty("runtimeOptions").GetProperty("frameworks").EnumerateArray().ToArray();
        Require(frameworks.Length==2&&frameworks.All(x=>x.GetProperty("version").GetString()=="10.0.0")&&frameworks.Select(x=>x.GetProperty("name").GetString()).ToHashSet().SetEquals(new[]{"Microsoft.NETCore.App","Microsoft.WindowsDesktop.App"}),"產品 runtime 要求不是已支援的 .NET 10。");
        Require(Environment.Version.Major==10,"需要 .NET 10 Runtime。");
        string core=RuntimeEnvironment.GetRuntimeDirectory();var installation=new DirectoryInfo(core).Parent?.Parent?.Parent?.FullName??throw new InvalidDataException("無法辨識目前 .NET 安裝。");
        string desktop=Path.Combine(installation,"shared","Microsoft.WindowsDesktop.App");
        var candidates=Directory.Exists(desktop)?Directory.GetDirectories(desktop).Select(p=>(Path:p,Version:Version.TryParse(Path.GetFileName(p),out var v)?v:null)).Where(x=>x.Version is {Major:10}&&x.Version>=new Version(10,0,0)&&File.Exists(Path.Combine(x.Path,"System.Windows.Forms.dll"))).OrderByDescending(x=>x.Version).ToArray():[];
        Require(candidates.Length>0,"找不到 .NET 10 Windows Desktop Runtime；已安裝一般 .NET Runtime 不等於 Desktop Runtime。");
        return new{architecture="x64",loadedCoreVersion=Environment.Version.ToString(),loadedCoreDirectory=core,desktopVersion=candidates[0].Version!.ToString(),desktopDirectory=candidates[0].Path};
    }
    static string WebViewVersion(string loader){
        IntPtr library=NativeLibrary.Load(loader);
        try {
            var query=Marshal.GetDelegateForFunctionPointer<BrowserVersion>(NativeLibrary.GetExport(library,"GetAvailableCoreWebView2BrowserVersionString"));
            int hr=query(null,out var value);
            try{Require(hr>=0&&value!=IntPtr.Zero,"找不到可用 WebView2 Runtime，HRESULT=0x"+hr.ToString("X8"));string version=Marshal.PtrToStringUni(value)??"";Require(version.Length>0,"WebView2 Runtime 版本為空。");return version;}
            finally{if(value!=IntPtr.Zero)Marshal.FreeCoTaskMem(value);}
        }finally{NativeLibrary.Free(library);}
    }
}
