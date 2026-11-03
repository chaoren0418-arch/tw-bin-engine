using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class Program {
    const string ManifestHash="6199d69faa708ee03994fc97de670a4e44cdcebed8981dce72c51a799890cabe";
    const string ManifestPath="build/product-r28-manifest.json";
    const string Entry="build/client-product-r28/P11.ClientEngine.exe";
    [UnmanagedFunctionPointer(CallingConvention.StdCall,CharSet=CharSet.Unicode)]
    delegate int BrowserVersion([MarshalAs(UnmanagedType.LPWStr)] string? folder,out IntPtr version);
    static int Main(string[] args){
        Console.OutputEncoding=new UTF8Encoding(false);
        try {
            bool check=args.Length>0&&args[0]=="--check-only";
            string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));
            if(args.Length==3&&check&&args[1]=="--root")root=Path.GetFullPath(args[2]);
            else if(args.Length!=0&&!(args.Length==1&&check))throw new InvalidDataException("只接受 --check-only，或 --check-only --root <完整產品目錄>；其他參數不會啟動產品。");
            var receipt=Verify(root);
            if(check){Console.WriteLine(JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));return 0;}
            // This is the only product-launch branch; the read-only check returns above.
            var start=new ProcessStartInfo(Path.Combine(root,Entry)){WorkingDirectory=root,UseShellExecute=false};
            start.ArgumentList.Add("--normal-product");
            using var child=Process.Start(start)??throw new InvalidOperationException("無法啟動已驗證的客戶端。");
            Console.WriteLine("已啟動 r28 客戶端；請在正常登入畫面手動登入。");return 0;
        }catch(Exception ex){Console.Error.WriteLine("啟動檢查失敗："+ex.Message);return 1;}
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
    static object Verify(string root){
        Require(OperatingSystem.IsWindows()&&RuntimeInformation.ProcessArchitecture==Architecture.X64,"此啟動器需要 Windows x64 與 .NET 10 Runtime。");
        string manifest=Resolve(root,ManifestPath);
        Require(File.Exists(manifest),"缺少 r28 產品 manifest；請使用完整交付目錄。");
        Require(Hash(manifest)==ManifestHash,"r28 manifest 已變更或版本不符，未啟動產品。");
        using var document=JsonDocument.Parse(File.ReadAllBytes(manifest));var m=document.RootElement;
        Require(m.GetProperty("schema").GetString()=="p24-normal-product-v1"&&m.GetProperty("entry").GetString()==Entry&&m.GetProperty("profile").GetString()=="p24-real-jar-d287-client2"&&m.GetProperty("port").GetInt32()==42061&&!m.GetProperty("autoLogin").GetBoolean(),"產品版本設定不一致。");
        var arguments=m.GetProperty("arguments").EnumerateArray().Select(x=>x.GetString()).ToArray();
        Require(arguments.SequenceEqual(new[]{"--normal-product"}),"正常入口參數不符。");
        var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int hashed=0;
        foreach(var pin in m.GetProperty("files").EnumerateArray()){
            string relative=pin.GetProperty("path").GetString()!;Require(files.Add(relative),"manifest 有重複檔案。");
            string path=Resolve(root,relative);Require(File.Exists(path),"缺少產品檔案："+relative);
            Require(new FileInfo(path).Length==pin.GetProperty("size").GetInt64()&&Hash(path)==pin.GetProperty("sha256").GetString(),"產品檔案已變更："+relative);hashed++;
        }
        Require(files.Contains(Entry)&&files.Contains("assets/integrity.json"),"manifest 缺少必要入口或素材索引。");
        using var inventory=JsonDocument.Parse(File.ReadAllBytes(Resolve(root,"assets/integrity.json")));
        Require(inventory.RootElement.GetProperty("schema").GetString()=="p06-assets-integrity-v1","素材索引格式不符。");
        int assets=0;var assetPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var pin in inventory.RootElement.GetProperty("files").EnumerateArray()){
            string relative=pin.GetProperty("path").GetString()!;Require(assetPaths.Add(relative),"素材索引有重複檔案。");
            string path=Resolve(Path.Combine(root,"assets"),relative);
            Require(File.Exists(path)&&new FileInfo(path).Length==pin.GetProperty("size").GetInt64(),"缺少或大小不符的素材："+relative);assets++;
        }
        Require(assets==13957,"r28 素材筆數不符。");
        var runtime=DesktopRuntime(root);
        string loaderRelative="build/client-product-r28/runtimes/win-x64/native/WebView2Loader.dll";
        Require(files.Contains(loaderRelative),"manifest 缺少 WebView2 loader。");
        string browser=WebViewVersion(Resolve(root,loaderRelative));
        return new{status="checked-only",revision="r28",executable=Resolve(root,Entry),workingDirectory=root,arguments,manifest,manifestSha256=ManifestHash,hashedRuntimeAndWebFiles=hashed,assetFilesCheckedForPresenceAndSize=assets,assetContentVerification="Pinned integrity manifest; body hashes remain verified by the product when loaded, matching prior entry contract.",runtime,webView2Version=browser,powerShellRequired=false,pathLookupUsed=false,autoLogin=false,uiStarted=false,networkStarted=false,servicesStarted=false};
    }
    static object DesktopRuntime(string root){
        using var config=JsonDocument.Parse(File.ReadAllBytes(Resolve(root,"build/client-product-r28/P11.ClientEngine.runtimeconfig.json")));
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
