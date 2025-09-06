using System.Diagnostics;
const string ChromeProcessName = "Chrome";
int sessionid = 0;
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

app.MapGet("/init", () =>
 {
     sessions.Init();
     sessions.Sessions.Add(sessionid, session);
     sessionid++;
     return Results.Content(session.GetQrCodeImage, "text/html");
 });

app.MapGet("/checkauth", () =>
 {
     return Results.Ok();
});
app.Run();

