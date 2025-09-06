using WhatsAppClientLib;
using WhatsAppWeb;

WhatsAppSession? session = null;
ChromeProcessManager.CaptureInitialProcesses();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(5080);
});
var app = builder.Build();

app.MapGet("/init/{*tail}", () =>
{
    if (session is null)
    {
        session = new WhatsAppSession().LoadQr();
        session.OnLoggedIn += key => Console.WriteLine($"Session {key} logged in");
    }
    else if (!session.IsLoggedIn && string.IsNullOrEmpty(session.QrDataUri))
    {
        session.RefreshLoadQr();
    }

    var html = session.IsLoggedIn
        ? $@"<!doctype html><meta charset=\"utf-8\"><h3>Authorized</h3><p>Guid: {session.Key}</p>"
        : $@"<!doctype html><meta charset=\"utf-8\"><h3>Scan QR</h3><p>Guid: {session.Key}</p>" +
          $@"<img width=228 height=228 src=\"{session.QrDataUri}\" alt=\"QR\">";

    return Results.Text(html, "text/html");
});

app.MapGet("/status/{id}", (long id) =>
{
    session ??= new WhatsAppSession(id);
    var status = session.CheckIfLoggedIn() ? 1 : 0;
    return Results.Ok(status);
});

app.MapGet("/{guid}/{id}", (string guid, long id) =>
{
    return Results.Ok();
});

app.MapGet("/kill", (string guid, long id) =>
{
    ChromeProcessManager.KillExtraProcesses();
    return Results.Ok();
});

app.MapGet("/wa/idx/{id:long}/{store}", async (long id, string store, string? cursor, int take) =>
{
    using var s = new WhatsAppSession(id);
    if (!s.CheckIfLoggedIn(10)) return Results.Unauthorized();
    var batch = await s.ReadIndexedDbBatchAsync("wawc", store, cursor, take <= 0 ? 200 : take);
    return Results.Json(batch);
});

app.MapGet("/wa/cache/{id:long}", async (long id, int take = 200) =>
{
    using var s = new WhatsAppSession(id);
    if (!s.CheckIfLoggedIn(10)) return Results.Unauthorized();
    var list = await s.ListCacheEntriesAsync(take <= 0 ? 200 : take);
    return Results.Json(list);
});

app.MapGet("/wa/cache/{id:long}/asset", async (long id, string url) =>
{
    using var s = new WhatsAppSession(id);
    if (!s.CheckIfLoggedIn(10)) return Results.Unauthorized();
    var bytes = await s.GetCachedAssetAsync(url);
    if (bytes.Length == 0) return Results.NotFound();
    return Results.File(bytes, "application/octet-stream");
});

app.Run();
