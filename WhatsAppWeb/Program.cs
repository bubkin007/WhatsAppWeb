using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WhatsAppClient;

const string ChromeProcessName = "Chrome";
const string CustomHeaderName = "X-CUSTOM-HEADER";
const string StatusEndpoint = "/status";
const string ExitEndpoint = "/exit";
const string QrCodeEndpoint = "/GetQrCode";
const string HtmlContentType = "text/html";
const string RequestLogTemplate = "Request: {0} {1}";

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

var builder = WebApplication.CreateBuilder(args);
var sessions = new WhatsAppClient.WhatsAppClient();
var app = builder.Build();

KillChrome();
app.UseAPIKeyCheckMiddleware();
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    Console.WriteLine(string.Format(RequestLogTemplate, context.Request.Method, context.Request.Path));
    await next();
});

app.MapGet(StatusEndpoint, async () =>
{
    var id = Guid.NewGuid();
    WhatsAppClient.WhatsAppClient? session;
    sessions.Sessions.TryGetValue(id, out session);
    if (session != null)
        await session.StatusAsync();
});

app.MapGet(ExitEndpoint, ([FromHeader(Name = CustomHeaderName)] Guid id) =>
{
    WhatsAppClient.WhatsAppClient? session;
    if (sessions.Sessions.TryGetValue(id, out session))
    {
        session.Exit();
        sessions.Sessions.Remove(id);
    }
});

app.MapGet(QrCodeEndpoint, async ([FromHeader(Name = CustomHeaderName)] Guid id) =>
{
    if (id == Guid.Empty)
        return Results.Unauthorized();
    WhatsAppClient.WhatsAppClient? session;
    if (!sessions.Sessions.TryGetValue(id, out session))
    {
        session = new WhatsAppClient.WhatsAppClient(id);
        sessions.Sessions.Add(id, session);
    }
    if (session.Logged)
        return Results.BadRequest();
    await session.StatusAsync();
    return Results.Content(session.GetQrCodeImage, HtmlContentType);
});

app.Run();

