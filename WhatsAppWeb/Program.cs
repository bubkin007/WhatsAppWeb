using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;



var chrome = false;
do{
    Process[] proct = Process.GetProcesses();
    chrome = false;
foreach (var procc in proct){
    if(procc.ProcessName.Contains("Chrome"))
    {
      procc.Kill();
      chrome = true;
    }
}
}while(chrome);

var builder = WebApplication.CreateBuilder(args);
var WhatsAppWebSessions = new WhatsAppWeb();
var kek = true;
do{

    foreach (var session in WhatsAppWebSessions._WhatsAppWebSessions){
        if(session.Value._logged){
            kek = false;
        }
    }
}while(kek);

var proc = Process.GetProcesses();
Process[] s = Process.GetProcessesByName("Chrome");
foreach (var procc in proc){

    if(procc.ProcessName.Contains("Chrome"))
    {
      var memory = procc.PrivateMemorySize64;
    }
}


var app = builder.Build();
app.UseAPIKeyCheckMiddleware();
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path}");
    await next();
});
app.MapGet("/status", async handler  =>
{
    WhatsAppWebSessions._WhatsAppWebSessions.TryGetValue(Guid.NewGuid(), out WhatsAppWeb session);
    session.Status();
});
app.MapGet("/exit", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid customHeader, CancellationToken token) =>
{
    WhatsAppWebSessions._WhatsAppWebSessions.TryGetValue(customHeader, out WhatsAppWeb session);
    session.Exit();
    WhatsAppWebSessions._WhatsAppWebSessions.Remove(customHeader);
});
app.MapGet("/GetQrCode", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid customHeader, CancellationToken token) =>
{
    if (customHeader == null)
    {
        return Results.Unauthorized();
    }
    WhatsAppWebSessions._WhatsAppWebSessions.TryGetValue(customHeader, out WhatsAppWeb session);
    if (session == null)
    {
        session = new WhatsAppWeb(customHeader);
        WhatsAppWebSessions._WhatsAppWebSessions.Add(customHeader, session);
    }
    if (session._logged == true)
    {
        return Results.BadRequest();
    }
    session.Status();
    return Results.Content(session.GetQrCodeImage, "text/html");
});
app.Run();