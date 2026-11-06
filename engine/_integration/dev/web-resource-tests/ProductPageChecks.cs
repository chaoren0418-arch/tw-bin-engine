using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using P06;

// Independent WebView2 host. Never constructs ClientForm or invokes the game entry point.
static class ProductPageChecks
{
    const string Page = "https://p08.example/index.html";
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static int Run(string scratchRoot, string outputPath, string engineRoot, ushort? localIcon)
    {
        string root = Path.GetFullPath(scratchRoot);
        string output = Path.GetFullPath(outputPath);
        string engine = Path.GetFullPath(engineRoot);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string iconRoot = engine;
        ushort iconId = localIcon ?? 42;
        if (localIcon == null)
        {
            iconRoot = Path.Combine(root, "fixture");
            Directory.CreateDirectory(Path.Combine(iconRoot, "assets", "inventory"));
            using var bitmap = new Bitmap(2, 2);
            bitmap.SetPixel(0, 0, Color.Red);
            bitmap.SetPixel(1, 1, Color.Blue);
            bitmap.Save(Path.Combine(iconRoot, "assets", "inventory", "42.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        string iconUrl = $"https://p08.example/icons/{iconId}.png";
        var expectedIcon = WebLocalResource.Read(iconRoot, iconUrl);
        if (expectedIcon.Status != 200 || expectedIcon.Body == null)
            throw new IOException("Icon prerequisite failed: " + iconUrl);

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        using var form = new Form { ShowInTaskbar = false, Opacity = 0, Width = 400, Height = 800 };
        var cycles = new List<object>();
        var requests = new List<object>();
        var clock = Stopwatch.StartNew();
        string? runtime = null;
        int exit = 1;
        form.Shown += async (_, _) =>
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "runtime"),
                    new CoreWebView2EnvironmentOptions("--disable-background-networking --disable-component-update"));
                runtime = env.BrowserVersionString;
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    using var view = new WebView2 { Dock = DockStyle.Fill };
                    form.Controls.Add(view);
                    await view.EnsureCoreWebView2Async(env).WaitAsync(TimeSpan.FromSeconds(20));
                    view.ZoomFactor = 96d / form.DeviceDpi;
                    var core = view.CoreWebView2;
                    var ready = new TaskCompletionSource();
                    var navigation = new TaskCompletionSource();
                    var clicked = new TaskCompletionSource();
                    var decoded = new TaskCompletionSource<JsonElement>();
                    var iconResponse = new TaskCompletionSource();
                    var commands = new List<string>();
                    var resources = new Dictionary<string, int>();
                    double readyAtMs = 0;
                    WebLocalResource.Install(core, (_, e) =>
                    {
                        // CI uses a generated PNG. Local acceptance can read an existing icon in place.
                        string stage = e.Request.Uri == iconUrl ? iconRoot : engine;
                        var resource = WebLocalResource.Read(stage, e.Request.Uri);
                        resources[e.Request.Uri] = resource.Status;
                        requests.Add(new { cycle, uri = e.Request.Uri, resource.Status });
                        e.Response = env.CreateWebResourceResponse(
                            resource.Body is { } bytes ? new MemoryStream(bytes, false) : null,
                            resource.Status, resource.Status == 200 ? "OK" : "Rejected", resource.Headers);
                    });
                    core.NavigationStarting += (_, e) => { if (e.Uri != Page) e.Cancel = true; };
                    core.NewWindowRequested += (_, e) => e.Handled = true;
                    core.NavigationCompleted += (_, e) =>
                    {
                        if (e.IsSuccess && e.HttpStatusCode == 200) navigation.TrySetResult();
                        else navigation.TrySetException(new Exception($"Navigation {e.HttpStatusCode}/{e.WebErrorStatus}"));
                    };
                    core.WebResourceResponseReceived += async (_, e) =>
                    {
                        if (e.Request.Uri != iconUrl) return;
                        try
                        {
                            Require(e.Response.StatusCode == 200, "icon HTTP 200");
                            Require(e.Response.Headers.GetHeader("Content-Type") == "image/png", "icon MIME");
                            Require(e.Response.Headers.GetHeader("Cache-Control") == "no-store", "icon cache policy");
                            using var content = await e.Response.GetContentAsync();
                            using var bytes = new MemoryStream();
                            await content.CopyToAsync(bytes);
                            Require(bytes.ToArray().SequenceEqual(expectedIcon.Body), "browser received original icon bytes");
                            iconResponse.TrySetResult();
                        }
                        catch (Exception ex) { iconResponse.TrySetException(ex); }
                    };
                    core.WebMessageReceived += (_, e) =>
                    {
                        if (e.Source != Page) return;
                        using var document = JsonDocument.Parse(e.WebMessageAsJson);
                        var message = document.RootElement;
                        if (message.TryGetProperty("testIcon", out var testIcon))
                        {
                            decoded.TrySetResult(message.Clone());
                            return;
                        }
                        string command = message.GetProperty("cmd").GetString()!;
                        commands.Add(command);
                        if (command == "ready")
                        {
                            readyAtMs = clock.Elapsed.TotalMilliseconds;
                            ready.TrySetResult();
                        }
                        if (commands.Count == 4) clicked.TrySetResult();
                    };
                    core.Navigate(Page);
                    await Task.WhenAll(ready.Task, navigation.Task).WaitAsync(TimeSpan.FromSeconds(20));
                    Require(await view.ExecuteScriptAsync("""
                        document.querySelector('h1').textContent === '角色控制面板' &&
                        document.querySelectorAll('#directions button').length === 8 &&
                        document.querySelectorAll('#moves button').length === 8 &&
                        document.querySelector('#notes') !== null &&
                        [...document.styleSheets].some(s => s.href.endsWith('/panel.css')) &&
                        getComputedStyle(document.documentElement).backgroundColor === 'rgb(19, 35, 42)'
                        """) == "true", "product DOM, JS controls and CSS exist");
                    foreach (string name in new[] { "index.html", "panel.css", "login.js", "interaction.js",
                        "trade.js", "bonus-stats.js", "private-shop.js", "panel.js" })
                        Require(resources.GetValueOrDefault("https://p08.example/" + name) == 200, name + " HTTP 200");

                    form.Width = 520;
                    await view.ExecuteScriptAsync("""
                        document.querySelector('#notes').value = 'F9 宿主回歸';
                        document.querySelector('#d2').click();
                        document.querySelector('#stop').click();
                        document.querySelector('#game').click();
                        """);
                    await clicked.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Require(commands.SequenceEqual(new[] { "ready", "face", "stop", "focusGame" }),
                        "each actual product button dispatches exactly once");
                    await view.ExecuteScriptAsync($$"""
                        const testIcon = new Image();
                        testIcon.id = 'host-test-icon';
                        testIcon.src = {{JsonSerializer.Serialize(iconUrl)}};
                        document.body.prepend(testIcon);
                        testIcon.decode().then(() => chrome.webview.postMessage({testIcon:true,
                            width:testIcon.naturalWidth,height:testIcon.naturalHeight}))
                            .catch(e => chrome.webview.postMessage({testIcon:true,error:String(e)}));
                        """);
                    await Task.WhenAll(decoded.Task, iconResponse.Task).WaitAsync(TimeSpan.FromSeconds(10));
                    var dimensions = await decoded.Task;
                    Require(!dimensions.TryGetProperty("error", out _) && dimensions.GetProperty("width").GetInt32() > 0
                        && dimensions.GetProperty("height").GetInt32() > 0, "icon decodes in product page");
                    Require(await view.ExecuteScriptAsync("document.querySelector('#notes').value === 'F9 宿主回歸'") == "true",
                        "product input retains edited value after resize");
                    using (var png = File.Create(Path.Combine(root, $"product-page-{cycle}.png")))
                        await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, png);
                    cycles.Add(new { cycle, readyAtMs, commands, icon = dimensions });
                    core.Stop();
                    form.Controls.Remove(view);
                }
                exit = 0;
            }
            catch (Exception ex)
            {
                cycles.Add(new { error = ex.ToString() });
                Console.Error.WriteLine(ex);
            }
            finally { form.Close(); }
        };
        using var timeout = new System.Windows.Forms.Timer { Interval = 90000 };
        timeout.Tick += (_, _) => { Console.Error.WriteLine("Product page host timeout"); form.Close(); };
        timeout.Start();
        Application.Run(form);
        var sources = Directory.GetFiles(Path.Combine(engine, "web"))
            .OrderBy(Path.GetFileName).Select(path => new { name = Path.GetFileName(path), sha256 = Sha(path) }).ToArray();
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            status = exit == 0 ? "pass" : "fail",
            scope = "獨立真實 WebView2 載入 engine/web，未啟動遊戲或連接服務",
            iconSource = localIcon == null ? "合成 2x2 PNG" : "唯讀既有本機圖示",
            iconUrl, iconSha256 = Convert.ToHexString(SHA256.HashData(expectedIcon.Body)), runtime,
            productDllSha256 = Sha(typeof(WebLocalResource).Assembly.Location), sources, cycles, requests
        }, JsonOptions));
        Console.WriteLine(exit == 0 ? "PASS product HTML/DOM/CSS/commands/icon/reopen" : "FAIL product page");
        return exit;
    }

    static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
