using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WhatsAppClient;

const string ChromeProcessName = "Chrome";
static void KillChrome()
{
    bool found;
    do
    {
        found = false;
        foreach (var proc in Process.GetProcesses())
        {
            if (proc.ProcessName.Contains(ChromeProcessName))
            {
                proc.Kill();
                found = true;
            }
        }
    } while (found);
}

KillChrome();

var builder = WebApplication.CreateBuilder(args);
var sessions = new WhatsAppClient.WhatsAppClient();

var app = builder.Build();
app.UseAPIKeyCheckMiddleware();
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

app.MapGet("/status", async () =>
{
    sessions.Sessions.TryGetValue(Guid.NewGuid(), out var session);
    if (session != null)
        await session.StatusAsync();
});

app.MapGet("/exit", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid id) =>
{
    if (sessions.Sessions.TryGetValue(id, out var session))
    {
        session.Exit();
        sessions.Sessions.Remove(id);
    }
});

app.MapGet("/GetQrCode", async ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid id) =>
{
    if (id == Guid.Empty)
        return Results.Unauthorized();
    if (!sessions.Sessions.TryGetValue(id, out var session))
    {
        session = new WhatsAppClient.WhatsAppClient(id);
        sessions.Sessions.Add(id, session);
    }
    if (session.Logged)
        return Results.BadRequest();
    await session.StatusAsync();
    return Results.Content(session.GetQrCodeImage, "text/html");
});

app.Run();

