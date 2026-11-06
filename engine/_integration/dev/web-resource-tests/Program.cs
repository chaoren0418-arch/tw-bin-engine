using P06;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Text.Json;
using System.Security.Cryptography;
class Checks {
    [STAThread] static int Main(string[] args){
        if(args.Length>=4&&args[2]=="--product-page")
            return ProductPageChecks.Run(args[0],args[1],args[3],args.Length>4?ushort.Parse(args[4]):null);
        string root=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
        string stage=Path.Combine(root,"stage");Directory.CreateDirectory(Path.Combine(stage,"assets/inventory"));Directory.CreateDirectory(Path.Combine(stage,"web"));
        using(var png=new Bitmap(2,2)){png.SetPixel(0,0,Color.Red);png.SetPixel(1,1,Color.Blue);png.Save(Path.Combine(stage,"assets/inventory/42.png"),System.Drawing.Imaging.ImageFormat.Png);}
        var expected=File.ReadAllBytes(Path.Combine(stage,"assets/inventory/42.png"));
        foreach(string url in new[]{"https://p08.example/icons/bad.png","https://p08.example/icons/65536.png","https://p08.example/icons/042.png","https://p08.example/icons/999.png"})
            if(WebLocalResource.Read(stage,url).Status!=404)throw new Exception("Expected404 "+url);
        var direct=WebLocalResource.Read(stage,"https://p08.example/icons/42.png");
        if(direct.Status!=200||!expected.SequenceEqual(direct.Body!)||!direct.Headers.Contains("image/png"))throw new Exception("PNG resolver");
        File.WriteAllText(Path.Combine(stage,"web/index.html"),"""
<!doctype html><img id="icon" src="/icons/42.png"><script>
(async()=>{const r=await fetch('/icons/42.png');const bytes=Array.from(new Uint8Array(await r.arrayBuffer()));
const bad=await fetch('/icons/65536.png');const missing=await fetch('/icons/999.png');const i=document.getElementById('icon');await i.decode();
chrome.webview.postMessage({status:r.status,mime:r.headers.get('content-type'),cache:r.headers.get('cache-control'),bytes,bad:bad.status,missing:missing.status,width:i.naturalWidth});})().catch(e=>chrome.webview.postMessage({error:String(e)}));
</script>
""");
        using var form=new Form{ShowInTaskbar=false,Opacity=0,Width=240,Height=160};
        using var view=new WebView2{Dock=DockStyle.Fill};form.Controls.Add(view);
        using var timeout=new System.Windows.Forms.Timer{Interval=20000};int exit=1;var requests=new List<string>();
        timeout.Tick+=(_,_)=>{Console.Error.WriteLine("WebView timeout");form.Close();};
        form.Shown+=async(_,_)=>{try{
            var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(root,"runtime"),new CoreWebView2EnvironmentOptions("--disable-background-networking --disable-component-update"));
            await view.EnsureCoreWebView2Async(env);var core=view.CoreWebView2;
            WebLocalResource.Install(core,(_,e)=>{requests.Add(e.Request.Uri);var r=WebLocalResource.Read(stage,e.Request.Uri);e.Response=env.CreateWebResourceResponse(r.Body is {} b?new MemoryStream(b,false):null,r.Status,r.Status==200?"OK":"Rejected",r.Headers);});
            core.WebMessageReceived+=(_,e)=>{try{
                using var json=JsonDocument.Parse(e.WebMessageAsJson);var v=json.RootElement;
                if(v.GetProperty("status").GetInt32()!=200||v.GetProperty("bad").GetInt32()!=404||v.GetProperty("missing").GetInt32()!=404||v.GetProperty("width").GetInt32()!=2||v.GetProperty("mime").GetString()!="image/png"||v.GetProperty("cache").GetString()!="no-store"||!v.GetProperty("bytes").EnumerateArray().Select(x=>x.GetByte()).SequenceEqual(expected))throw new Exception(e.WebMessageAsJson);
                File.WriteAllText(output,JsonSerializer.Serialize(new{status="pass",scope="真正WebView2載入合成HTML及PNG；非遊戲",pngSha256=Convert.ToHexString(SHA256.HashData(expected)),requests,result=v.Clone()},new JsonSerializerOptions{WriteIndented=true}));exit=0;
            }catch(Exception ex){Console.Error.WriteLine(ex);}finally{form.Close();}};
            core.Navigate("https://p08.example/index.html");
        }catch(Exception ex){Console.Error.WriteLine(ex);form.Close();}};
        timeout.Start();Application.Run(form);Console.WriteLine(exit==0?"PASS WebView2 PNG bytes/MIME/decode/404/cache":"FAIL WebView2");return exit;
    }
}
