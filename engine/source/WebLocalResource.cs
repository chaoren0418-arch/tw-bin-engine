namespace P06;

public static class WebLocalResource
{
    public static void Install(Microsoft.Web.WebView2.Core.CoreWebView2 core,
        EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2WebResourceRequestedEventArgs> handler)
    {
        core.AddWebResourceRequestedFilter("*",Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        core.WebResourceRequested+=handler;
    }
    public static (int Status, byte[]? Body, string Headers) Read(string stageRoot, string request)
    {
        const string prefix = "https://p08.example/";
        if (request.StartsWith(prefix + "icons/", StringComparison.Ordinal))
        {
            var icon = WebIconResource.Read(Path.Combine(stageRoot, "assets"), request);
            return (icon.Status, icon.Body, Headers("image/png"));
        }
        string name = request.StartsWith(prefix, StringComparison.Ordinal) ? request[prefix.Length..] : "";
        string? mime = name switch {
            "index.html" => "text/html; charset=utf-8",
            "panel.css" => "text/css; charset=utf-8",
            "panel.js" or "login.js" or "interaction.js" or "trade.js" or "bonus-stats.js" or "private-shop.js" => "text/javascript; charset=utf-8",
            _ => null
        };
        if (mime == null) return (403, null, Headers("text/plain"));
        try { return (200, File.ReadAllBytes(Path.Combine(stageRoot, "web", name)), Headers(mime)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return (404, null, Headers("text/plain")); }
    }
    static string Headers(string mime) => "Content-Type: " + mime + "\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff";
}
