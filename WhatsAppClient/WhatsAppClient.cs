using System.Reflection;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager.Helpers;

namespace WhatsAppClient;

public class WhatsAppClient
{
    public Dictionary<Guid, WhatsAppClient> Sessions { get; }
    private readonly Guid _sessionId;
    public bool Logged { get; private set; }
    private readonly IWebDriver _driver;
    private string _qrCode;
    private readonly string _whatsAppUrl = "https://web.whatsapp.com/";
    private readonly string _sessionFolder;
    public readonly string MessageUrl = "https://web.whatsapp.com/send/?phone={destination_number}&text={text}&type=phone_number&app_absent=0";
    private bool _qrCodeReady;
    private const string HeadlessArgument = "--headless";
    private const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
    private string _userDataDirArgument;

    public WhatsAppClient()
    {
        Sessions = new Dictionary<Guid, WhatsAppClient>();
        SessionLoader();
    }

    public WhatsAppClient(Guid sessionId, WebDriver driver)
    {
        _sessionId = sessionId;
        _driver = driver;
        CheckPhone().Wait();
    }

    public WhatsAppClient(Guid sessionId)
    {
        _sessionId = sessionId;
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _sessionFolder = Path.Combine(assemblyDirectory!, "sessionfolder", sessionId.ToString());
        Directory.CreateDirectory(_sessionFolder);
        _userDataDirArgument = "--user-data-dir=" + _sessionFolder;
        ChromeOptions option = new();
        option.AddArgument(HeadlessArgument);
        option.AddArgument("--user-agent=" + UserAgent);
        option.AddArgument(_userDataDirArgument);
        _driver = new ChromeDriver(option);
        _driver.Navigate().GoToUrl(_whatsAppUrl);
        new WebDriverWait(_driver, TimeSpan.FromSeconds(60)).Until(d => ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState").Equals("complete"));
        _qrCodeReady = false;
        CheckQrCodeExistance().Wait();
        CheckPhone().Wait();
    }

    void SessionLoader()
    {
        var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var sessionList = Directory.GetDirectories(Path.Combine(assemblyDirectory!, "sessionfolder"));
        foreach (var session in sessionList)
        {
            var dir = new DirectoryInfo(session).Name;
            var sessionId = Guid.Parse(dir);
            var driver = new WhatsAppClient(sessionId);
            Sessions.Add(sessionId, driver);
        }
    }

    public async Task StatusAsync()
    {
        await CheckPhone();
        await CheckQrCodeExistance();
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
                var canvasElement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.ClassName("selectable-text")));
                Logged = true;
                canvasElement.Click();
                await Task.Delay(1000);
                canvasElement.Clear();
                await Task.Delay(1000);
                canvasElement.Click();
                await Task.Delay(1000);
                canvasElement.SendKeys("You");
                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.CssSelector("div[aria-label='Search results.']")));
                await Task.Delay(2000);
                var span = _driver.FindElements(By.CssSelector("span[aria-label='']"));
                foreach (var element in span)
                {
                    if (element.Text.Contains('+'))
                    {
                        Logged = true;
                        var phone = element.Text;
                    }
                }
            }
            catch
            {
            }
        } while (!Logged);
    }

    private async Task RequestQrCode()
    {
        if (_qrCode != null)
        {
            return;
        }
        var wait = new WebDriverWait(_driver, new TimeSpan(0, 0, 10));
        var canvasElement = wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.TagName("canvas")));
        var toDataURLscript = "return arguments[0].toDataURL('image/png');";
        IJavaScriptExecutor js = (IJavaScriptExecutor)_driver;
        var freshBase64Image = (string)js.ExecuteScript(toDataURLscript, canvasElement);
        if (_qrCode != freshBase64Image)
        {
            _qrCode = freshBase64Image;
        }
    }

    private async Task CheckQrCodeExistance()
    {
        do
        {
            try
            {
                var wait = new WebDriverWait(_driver, new TimeSpan(0, 0, 10));
                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementExists(By.TagName("canvas")));
                _qrCodeReady = true;
                Logged = false;
                return;
            }
            catch
            {
                Screenshot ss = ((ITakesScreenshot)_driver).GetScreenshot();
                var time = DateTime.Now.ToString("hh:mm:ss_dd.MM.yyyy");
                ss.SaveAsFile(Path.Combine(_sessionFolder, "debug", $"Image{time}.png"));
            }
        } while (!_qrCodeReady);
    }

    public string GetQrCodeImage => $"<!DOCTYPE html><html><body><img src='{_qrCode}'/></body></html>";
}

