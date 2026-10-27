using BackendTray.Agent;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

// 前端 port：提供 Vue 的靜態檔（SPA），並把 /api 等路徑轉發給後端 API。
// 由 BackendTray 服務啟動時，會以 --config 指定服務產生的路由設定檔。
var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration["config"] is { Length: > 0 } configFile)
    builder.Configuration.AddJsonFile(configFile, optional: false, reloadOnChange: false);

builder.AddTrayAgent();
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

var webRoot = Path.GetFullPath(app.Configuration["Gateway:WebRoot"] ?? "wwwroot", app.Environment.ContentRootPath);
var hasFrontend = File.Exists(Path.Combine(webRoot, "index.html"));

app.MapGet("/_gateway/health", () => Results.Ok(new { status = "ok", frontend = hasFrontend }));

if (hasFrontend)
{
    var files = new PhysicalFileProvider(webRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files, OnPrepareResponse = SetCacheHeaders });

    // 靜態檔要在路由之前處理，否則所有請求都會先被 SPA fallback 接走。
    app.UseRouting();
    app.MapReverseProxy();
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files, OnPrepareResponse = SetCacheHeaders });
}
else
{
    app.UseRouting();
    app.MapReverseProxy();
    app.MapFallback(() => Results.Content(PlaceholderPage.Html, "text/html; charset=utf-8"));
}

app.Run();

// index.html 每次都要重新驗證，更新後使用者才會拿到新版；assets 的檔名含 hash，可以長期快取。
static void SetCacheHeaders(StaticFileResponseContext context)
{
    var headers = context.Context.Response.Headers;
    if (context.Context.Request.Path.StartsWithSegments("/assets"))
        headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
    else if (context.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase))
        headers[HeaderNames.CacheControl] = "no-cache";
}

internal static class PlaceholderPage
{
    public const string Html = """
        <!doctype html>
        <html lang="zh-Hant">
        <head><meta charset="utf-8"><title>BackendTray</title></head>
        <body style="font-family: system-ui, sans-serif; margin: 3rem; color: #333">
          <h1>前端尚未部署</h1>
          <p>把 Vue 專案 <code>npm run build</code> 的產出放到 <code>frontend/dist</code>，重新打包安裝後就會顯示在這裡。</p>
          <p>後端 API 已可經由 <code>/api/...</code> 存取。</p>
        </body>
        </html>
        """;
}
