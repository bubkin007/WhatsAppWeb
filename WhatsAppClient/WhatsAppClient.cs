
using System.Text;
using Net.Codecrete.QrCodeGenerator;
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
    public Dictionary<long, WhatsAppClient> Sessions { get; }
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
    private string _qrCodeb64;
    private bool activesession;
    public WhatsAppClient()
    {
        _sessionFolder = Path.Combine(AppContext.BaseDirectory, "sessionfolder");
        if (!Path.Exists(_sessionFolder)) Directory.CreateDirectory(_sessionFolder);
        Sessions = [];
        SessionLoader();
    }

    public WhatsAppClient(Guid sessionId)
    {
        _sessionFolder = Path.Combine(AppContext.BaseDirectory, "sessionfolder", sessionId.ToString());
        _userDataDirArgument = "--user-data-dir=" + _sessionFolder;
        new DriverManager().SetUpDriver(new ChromeConfig(), VersionResolveStrategy.MatchingBrowser);
        ChromeOptions option = new();
        //option.AddArgument(HeadlessArgument);
        option.AddArgument("--user-agent=" + UserAgent);
        option.AddArgument(_userDataDirArgument);
        try
        {
            _driver = new ChromeDriver(option);
            _driver.Navigate().GoToUrl(_whatsAppUrl);
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
            var element = wait.Until(d =>
            {
                var el = d.FindElements(By.CssSelector("div[data-ref]")).FirstOrDefault(e => !string.IsNullOrEmpty(e.GetAttribute("data-ref"))); return el;
            });
            string payload = element.GetAttribute("data-ref");
            var qr = QrCode.EncodeText(payload, QrCode.Ecc.Medium);
            string svg = qr.ToSvgString(border: 4);
            _qrCodeb64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
            string dataUri = $"data:image/svg+xml;base64,{_qrCodeb64}";
        }
        catch { activesession = false; }
    }

    void SessionLoader()
    {
        var sessionList = Directory.GetDirectories(_sessionFolder);
        foreach (var session in sessionList)
        {
            var dir = new DirectoryInfo(session).Name;
            var sessionId = long.Parse(dir);
            var driver = new WhatsAppClient();
            if (driver.activesession) Sessions.Add(sessionId, driver);
        }
    }
public   void Init()
    {
       var sessionId = 1;
        Directory.CreateDirectory(_sessionFolder+sessionId.ToString());
        _userDataDirArgument = "--user-data-dir=" + _sessionFolder;
        new DriverManager().SetUpDriver(new ChromeConfig(), VersionResolveStrategy.MatchingBrowser);
        ChromeOptions option = new();
        //option.AddArgument(HeadlessArgument);
        option.AddArgument("--user-agent=" + UserAgent);
        option.AddArgument(_userDataDirArgument);
        try
        {
            var _driverp = new ChromeDriver(option);
            _driverp.Navigate().GoToUrl(_whatsAppUrl);
            var wait = new WebDriverWait(_driverp, TimeSpan.FromSeconds(10));
            var element = wait.Until(d =>
            {
                var el = d.FindElements(By.CssSelector("div[data-ref]"))
                                  .FirstOrDefault(e => !string.IsNullOrEmpty(e.GetAttribute("data-ref"))); return el;
            });
            string payload = element.GetAttribute("data-ref");
            var qr = QrCode.EncodeText(payload, QrCode.Ecc.Medium);
            string svg = qr.ToSvgString(border: 4);
            _qrCodeb64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
            string dataUri = $"data:image/svg+xml;base64,{_qrCodeb64}";
        }
        catch { }
    }
    public void Exit()
    {
        _driver.Close();
    }
    public string GetQrCodeImage => $@"<!doctype html><html><head><meta charset=""utf-8""></head><body><a>{_sessionId}</a>  <img width=228px height=228px src=""data:image/+xml;base64,{_qrCodeb64}"" alt=""QR"" /></body></html>";
}

