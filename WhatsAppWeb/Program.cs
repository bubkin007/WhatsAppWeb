using System.Diagnostics;
using WhatsAppClientLib;
WhatsAppSession? s = null;
List<int> imutablechrome = [];

var ChromeProcessName = "chrome";
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(5080);
});
var app = builder.Build();
Chrome();
// Хендлер
app.MapGet("/init/{*tail}", () =>
{
    if (s is null)
    {
        s = new WhatsAppSession().LoadQr(); // LoadQr должен вернуть this
        s.OnLoggedIn += key => Console.WriteLine($"Session {key} logged in");
    }
    else if (!s.IsLoggedIn && string.IsNullOrEmpty(s.QrDataUri))
    {
        s.RefreshLoadQr();
    }

    var html = s.IsLoggedIn
        ? $@"<!doctype html><meta charset=""utf-8""><h3>Authorized</h3><p>Guid: {s.Key}</p>"
        : $@"<!doctype html><meta charset=""utf-8""><h3>Scan QR</h3><p>Guid: {s.Key}</p>
<img width=228 height=228 src=""{s.QrDataUri}"" alt=""QR"">";

    return Results.Text(html, "text/html");
});



app.MapGet("/status/{id}", (long id) =>
{
    var status = 0;
    s ??= new WhatsAppSession(id);
    status = s.CheckIfLoggedIn() ? 1 : 0;
    return Results.Ok(status);
});

app.MapGet("/{string}/{id}", (string guid, long id) =>
{

    return Results.Ok();
});
app.MapGet("/kill", async (string guid, long id) =>
{
    await KillChrome();
    return Results.Ok();
});
// IndexedDB
app.MapGet("/wa/idx/{id:long}/{store}", async (long id, string store, string? cursor, int take) =>
{
    using var s = new WhatsAppSession(id);
    if (!s.CheckIfLoggedIn(10)) return Results.Unauthorized();
    var batch = await s.ReadIndexedDbBatchAsync("wawc", store, cursor, take <= 0 ? 200 : take);
    return Results.Json(batch);
});

// Cache list
app.MapGet("/wa/cache/{id:long}", async (long id, int take=200) =>
{
    using var s = new WhatsAppSession(id);
    if (!s.CheckIfLoggedIn(10)) return Results.Unauthorized();
    var list = await s.ListCacheEntriesAsync(take <= 0 ? 200 : take);
    return Results.Json(list);
});

// Cache asset
app.MapGet("/wa/cache/{id:long}/asset", async (long id, string url) =>
{
    using var s = new WhatsAppSession(id);
    if (!s.CheckIfLoggedIn(10)) return Results.Unauthorized();
    var bytes = await s.GetCachedAssetAsync(url);
    if (bytes.Length == 0) return Results.NotFound();
    return Results.File(bytes, "application/octet-stream");
});

app.Run();

void Chrome()
{
    foreach (var proc in Process.GetProcesses())
    {
        if (proc.ProcessName.Contains(ChromeProcessName))
        {
            imutablechrome.Add(proc.Id);
                    _ = proc.MainModule;
        }

    }
}
async Task KillChrome()
{
    bool found;
    do
    {
        found = false;
        foreach (var proc in Process.GetProcesses())
        {
            if (!imutablechrome.Contains(proc.Id))
            {
                if (proc.ProcessName.Contains(ChromeProcessName))
                {

                    proc.Kill();
                    found = true;
                }
            }
        }
    } while (found);
}