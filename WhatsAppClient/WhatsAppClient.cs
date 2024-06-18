
using System;
using System.Reflection;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

public class WhatsAppWeb
{
    public Dictionary<Guid, WhatsAppWeb> _WhatsAppWebSessions;
    private Guid _sessionId;
    public bool _logged = false;
    private readonly IWebDriver _driver;
    private string _base64image;
    private readonly string _whatsappURL = "https://web.whatsapp.com/;";
    private readonly string _FolderPathToStoreSession;
    private readonly int _qrcoderefreshtime = 55;
    public readonly string messageurl = "https://web.whatsapp.com/send/?phone={destanation_number]}&text={text}&type=phone_number&app_absent=0";
    private readonly long phonenumber;
    private bool _QrCodeReady;
    private readonly int process;

    public WhatsAppWeb()
    {
        _WhatsAppWebSessions = new();
        SessionLoader();
    }

    public WhatsAppWeb(Guid sessionId, WebDriver driver)
    {
        _sessionId = sessionId;
        _driver = driver;
        CheckPhone();
    }
    public WhatsAppWeb(Guid sessionId)
    {
        _sessionId = sessionId;
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _FolderPathToStoreSession = assemblyDirectory + "/sessionfolder" + $"/{sessionId}";
        Directory.CreateDirectory(_FolderPathToStoreSession);
        ChromeOptions option = new();
         option.AddArguments("--headless");
        option.AddArgument("--user-agent=Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");  //last PC Chrome
        option.AddArgument("--user-data-dir=" + _FolderPathToStoreSession);
        _driver = new ChromeDriver(option);
        _driver.Navigate().GoToUrl(_whatsappURL + "/");
        bool wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(60)).Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
        _QrCodeReady = false;
        CheckQrCodeExistanse();
        CheckPhone();
    }

    void SessionLoader()
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var sessionlist = Directory.GetDirectories(assemblyDirectory + "/sessionfolder");
        foreach (var session in sessionlist)
        {
            var dir = new DirectoryInfo(session).Name;
            var sessionid = Guid.Parse(dir);
            var driver = new WhatsAppWeb(sessionid);
            _WhatsAppWebSessions.Add(Guid.Parse(dir), driver);
        }
    }
    public async void Status()
    {

        //qr and phone need to be paralel 
        await CheckPhone();
        await CheckQrCodeExistanse();
    }
    public void Exit()
    {
        _driver.Close();
    }

    private async Task CheckPhone()
    {
        do
        {
            try
            {
                var wait = new WebDriverWait(_driver, new TimeSpan(0, 0, 10));
                var canvaselement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.ClassName("selectable-text")));
                _logged = true;
                canvaselement.Click();
                Task.Delay(1000).Wait();
                canvaselement.Clear();
                Task.Delay(1000).Wait();
                canvaselement.Click();
                Task.Delay(1000).Wait();
                canvaselement.SendKeys("You");
                var you = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.CssSelector("div[aria-label='Search results.']")));
                Task.Delay(2000).Wait();
                var span = _driver.FindElements(By.CssSelector("span[aria-label='']"));
                foreach (var element in span)
                {
                    if (element.Text.Contains('+'))
                    {
                        _logged = true;
                        var phone = element.Text;
                    };
                }
            }
            catch (Exception ex)
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
        do
        {
            try
            {
                var wait = new WebDriverWait(_driver, new TimeSpan(0, 0, 10));
                var canvaselement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.TagName("canvas")));
                _QrCodeReady = true;
                _logged = false;
                return;
            }
            catch (Exception ex)
            {
                Screenshot ss = ((ITakesScreenshot)_driver).GetScreenshot();
                var time  = DateTime.Now.ToString("hh:mm:ss_dd.MM.yyyy");
                //check folder here 
                ss.SaveAsFile(Path.Combine(_FolderPathToStoreSession,"debug",$"Image{DateTime.Now.ToString("hh:mm:ss_dd.MM.yyyy")}.png"));
            }
        } while (!_QrCodeReady);
    }
    public string GetQrCodeImage => $@"<!DOCTYPE html><html><body><img src='{_base64image}'/></body></html>";
}

