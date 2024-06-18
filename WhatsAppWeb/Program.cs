using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

Dictionary<Guid, WhatsAppWeb> WhatsAppWebSessions = new();

SessionLoader();

void SessionLoader()
{var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
var sessionlist =  Directory.GetDirectories(assemblyDirectory + "/sessionfolder");
foreach(var session in sessionlist){
    var dir = new DirectoryInfo(session).Name;
WhatsAppWebSessions.Add(Guid.Parse(dir),new WhatsAppWeb(Guid.Parse(dir)));
}
}
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseHttpsRedirection();
app.MapGet("/status", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid customHeader, CancellationToken token) =>
{
    WhatsAppWebSessions.TryGetValue(customHeader, out WhatsAppWeb session);
    session.Status();
});


app.MapGet("/exit", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid customHeader, CancellationToken token) =>
{
    WhatsAppWebSessions.TryGetValue(customHeader, out WhatsAppWeb session);
    session.Exit();
    WhatsAppWebSessions.Remove(customHeader);
});


app.MapGet("/GetQrCode", ([FromHeader(Name = "X-CUSTOM-HEADER")] Guid customHeader, CancellationToken token) =>
{
    if (customHeader == null)
    {
        return Results.Unauthorized();
    }
    WhatsAppWebSessions.TryGetValue(customHeader, out WhatsAppWeb session);
    if (session == null)
    {
        session = new WhatsAppWeb(customHeader);
        WhatsAppWebSessions.Add(customHeader, session);
    }
    if (session._logged == true)
    {
        return Results.BadRequest();
    }
    session.Status();
    return Results.Content(session.GetQrCodeImage, "text/html");
});
app.Run();
public class WhatsAppWeb
{
    private Guid _sessionId;
    public bool _logged = false;
    private readonly IWebDriver _driver;
    private string _base64image;
    private readonly string _whatsappURL = "https://web.whatsapp.com/;";
    private readonly string _FolderPathToStoreSession;
    private readonly int _qrcoderefreshtime = 55;
    public readonly string  messageurl = "https://web.whatsapp.com/send/?phone={destanation_number]}&text={text}&type=phone_number&app_absent=0";
     private readonly long phonenumber;

     private readonly int process;
    public WhatsAppWeb(Guid sessionId)
    {
        _sessionId = sessionId;
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _FolderPathToStoreSession = assemblyDirectory + "/sessionfolder" + $"/{sessionId}";
        Directory.CreateDirectory(_FolderPathToStoreSession);
        ChromeOptions option = new();
        //option.AddArguments("--headless");
        option.AddArgument("--user-data-dir=" + _FolderPathToStoreSession);
        _driver = new ChromeDriver(option);
        _driver.Navigate().GoToUrl(_whatsappURL + "/");
        bool wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60)).Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete")); 
    }
    public async void Status(){
       await CheckPhone();
       await CheckQrCodeExistanse();
    }
    public void Exit()
    {
        _driver.Close();
    }

    private async Task  CheckPhone()
    {
        do
        {
            try
            {
                var wait = new WebDriverWait(_driver, new TimeSpan(0, 0, 2));
                var canvaselement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.ClassName("selectable-text")));
                _logged = true;
                canvaselement.Click();
                                Task.Delay(1000).Wait();
                canvaselement.Clear();
                                Task.Delay(1000).Wait();
                canvaselement.Click();
                                Task.Delay(1000).Wait();
                canvaselement.SendKeys("You");
                var you = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.CssSelector("div[aria-label='Search results.']")));
                               Task.Delay(2000).Wait();
                var span = _driver.FindElements(By.CssSelector("span[aria-label='']"));
                foreach(var element in span){
                    if(element.Text.Contains('+')){
                        var phone = element.Text;
                    };
                }
            }
            catch(Exception ex)
            {

            }
        } while (!_logged);
    }

    private async Task RequestQrCode()
    {
        if (_base64image != null)
        {
            return;
        }
        //qrexpired?
        var wait = new WebDriverWait(_driver, new TimeSpan(0, 0, 10));
        var canvaselement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.TagName("canvas")));
        var toDataURLscript = "return arguments[0].toDataURL('image/png');";
        IJavaScriptExecutor js = (IJavaScriptExecutor)_driver;
        var freshbase64image = (string)js.ExecuteScript(toDataURLscript, canvaselement);
        if (_base64image != freshbase64image)
        {
            _base64image = freshbase64image;
        }
    }

    private async Task CheckQrCodeExistanse()
    {
        try
        {
            var wait = new WebDriverWait(_driver, new TimeSpan(0, 0, 2));
            var canvaselement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.TagName("canvas")));
            _logged = false;
            return;
        }
        catch
        {

        }
    }
    public string GetQrCodeImage => $@"<!DOCTYPE html><html><body><img src='{_base64image}'/></body></html>";
}
