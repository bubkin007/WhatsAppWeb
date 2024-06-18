using Microsoft.AspNetCore.Mvc;
var builder = WebApplication.CreateBuilder(args);

var WhatsAppWebSessions = new WhatsAppWeb();
var app = builder.Build();
app.UseHttpsRedirection();
app.MapGet("/status", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid customHeader, CancellationToken token) =>
{
    WhatsAppWebSessions.WhatsAppWebSessions.TryGetValue(customHeader, out WhatsAppWeb session);
    session.Status();
});
app.MapGet("/exit", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid customHeader, CancellationToken token) =>
{
    WhatsAppWebSessions.WhatsAppWebSessions.TryGetValue(customHeader, out WhatsAppWeb session);
    session.Exit();
    WhatsAppWebSessions.WhatsAppWebSessions.Remove(customHeader);
});
app.MapGet("/GetQrCode", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid customHeader, CancellationToken token) =>
{
    if (customHeader == null)
    {
        return Results.Unauthorized();
    }
    WhatsAppWebSessions.WhatsAppWebSessions.TryGetValue(customHeader, out WhatsAppWeb session);
    if (session == null)
    {
        session = new WhatsAppWeb(customHeader);
        WhatsAppWebSessions.WhatsAppWebSessions.Add(customHeader, session);
    }
    if (session._logged == true)
    {
        return Results.BadRequest();
    }
    session.Status();
    return Results.Content(session.GetQrCodeImage, "text/html");
});
app.Run();