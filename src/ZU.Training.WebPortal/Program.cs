using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// البوابة تخدم الصفحات الثابتة وتُمرّر مسارات /api إلى خدمة الـAPI على نفس الأصل،
// فيبقى ملف تعريف الجلسة (Cookie) صالحاً دون الحاجة إلى CORS.
var apiBaseUrl = (builder.Configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5098").TrimEnd('/');
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    UseCookies = false,
    AutomaticDecompression = DecompressionMethods.None,
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        // صفحات ومنطق الواجهة لا تُخزَّن مؤقتاً حتى تظهر التحديثات فوراً.
        if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            context.File.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok", api = apiBaseUrl }));

// وكيل شفّاف لمسارات الـAPI: يمرّر الطريقة والترويسات والكوكيز والجسم ويعيد الاستجابة كما هي.
app.Map("/api/{**path}", async (HttpContext context, IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("api");
    var path = context.Request.Path + context.Request.QueryString;
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path);

    if (context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
    {
        request.Content = new StreamContent(context.Request.Body);
        if (!string.IsNullOrEmpty(context.Request.ContentType))
            request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
    }

    foreach (var header in context.Request.Headers)
    {
        if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
            header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
            header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
            header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)) continue;
        if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    request.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.ToString());
    request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
    if (context.Request.Cookies.Count > 0)
        request.Headers.TryAddWithoutValidation("Cookie",
            string.Join("; ", context.Request.Cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));

    try
    {
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;

        foreach (var header in response.Headers.Concat(response.Content.Headers))
        {
            if (header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)) continue;
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var cookie in response.Headers.TryGetValues("Set-Cookie", out var values)
                     ? values
                     : Enumerable.Empty<string>())
            context.Response.Headers.Append("Set-Cookie", cookie);

        await using var stream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
        await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
    catch (OperationCanceledException)
    {
        if (!context.Response.HasStarted) context.Response.StatusCode = 499;
    }
    catch (HttpRequestException)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync("{\"message\":\"خدمة الـAPI غير متاحة حالياً.\"}");
    }
});

app.MapFallbackToFile("index.html");

app.Run();
