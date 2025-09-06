// WhatsAppClientLib/WhatsAppSession.cs
using System.Text;
using System.Text.Json;
using Net.Codecrete.QrCodeGenerator;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace WhatsAppClientLib;

public sealed class WhatsAppSession : IDisposable
{
    public event Action<Guid>? OnLoggedIn;

    const string MetaFileName = "meta.json";
    static readonly string Root = Path.Combine(AppContext.BaseDirectory, "sessionfolder");

    readonly ChromeDriver _driver;
    readonly long _id;
    public Guid Key { get; }
    public string Dir { get; }
    public string? QrDataUri { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public WhatsAppSession()
    {
        Directory.CreateDirectory(Root);
        _id = NextId();
        Key = Guid.NewGuid();
        Dir = Path.Combine(Root, _id.ToString());
        Directory.CreateDirectory(Dir);
        PersistMeta();
        _driver = CreateDriver(Dir);
        _driver.Navigate().GoToUrl("https://web.whatsapp.com/");
    }

    private WhatsAppSession(long id, Guid key, string dir, bool attachDriver)
    {
        _id = id;
        Key = key;
        Dir = dir;
        if (attachDriver)
        {
            _driver = CreateDriver(Dir);
            _driver.Navigate().GoToUrl("https://web.whatsapp.com/");
        }
    }

    public WhatsAppSession(long id)
    {
        _id = id;
        Dir = Path.Combine(Root, id.ToString());
        if (!Directory.Exists(Dir)) throw new DirectoryNotFoundException("Session dir not found");
        _driver = CreateDriver(Dir);
        _driver.Navigate().GoToUrl("https://web.whatsapp.com/");
    }

    public static WhatsAppSession Open(Guid key)
    {
        foreach (var dir in Directory.Exists(Root) ? Directory.GetDirectories(Root) : Array.Empty<string>())
        {
            var metaPath = Path.Combine(dir, MetaFileName);
            if (!File.Exists(metaPath)) continue;
            try
            {
                var meta = JsonSerializer.Deserialize<SessionMeta>(File.ReadAllText(metaPath));
                if (meta is not null && meta.Key == key)
                    return new WhatsAppSession(meta.Id, meta.Key, dir, attachDriver: true);
            }
            catch { }
        }
        throw new DirectoryNotFoundException("Session not found");
    }

    public WhatsAppSession LoadQr()
    {
        if (IsLoggedIn) throw new InvalidOperationException("Already logged in");
        var el = WaitForQrElement(15);
        var payload = el.GetAttribute("data-ref");
        QrDataUri = EncodeQrAsSvgDataUri(payload);
        _ = Task.Run(AuthWaiter);
        return this;
    }

    public void RefreshLoadQr()
    {
        var el = WaitForQrElement(15);
        var payload = el.GetAttribute("data-ref");
        QrDataUri = EncodeQrAsSvgDataUri(payload);
        _ = Task.Run(AuthWaiter);
    }


    void AuthWaiter()
    {
        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline && !IsLoggedIn)
        {
            try
            {
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(25));
                IsLoggedIn = wait.Until(LoggedIn());
                if (IsLoggedIn)
                {
                    OnLoggedIn?.Invoke(Key);
                    break;
                }
            }
            catch { }
        }
    }

    public Func<IWebDriver, bool> LoggedIn()
    {
        return d =>
        {
            var hasApp = d.FindElements(By.CssSelector("[data-testid='chat-list'],[role='grid']")).Count != 0;
            var noQr = d.FindElements(By.CssSelector("div[data-ref],canvas[aria-label*='Scan']")).Count == 0;
            return hasApp && noQr;
        };
    }
    // DTO
    public record IdxBatch(object[] Items, string? Cursor);
    public record CacheEntry(string Key, string Url, string Method, int Status, Dictionary<string, string> Headers);

    // WhatsAppSession.cs
    public async Task<IdxBatch> ReadIndexedDbBatchAsync(string db, string store, string? cursor, int take)
    {
        var js = (IJavaScriptExecutor)_driver;
        var raw = (string)js.ExecuteAsyncScript(@"
const [dbName, storeName, cursorKey, limit, done] = arguments;
(async () => {
  try {
    const open = indexedDB.open(dbName);
    await new Promise((res,rej)=>{open.onsuccess=()=>res();open.onerror=()=>rej(open.error);});
    const db = open.result;
    const tx = db.transaction(storeName,'readonly');
    const st = tx.objectStore(storeName);
    const res = [];
    let next = null;
    let req = cursorKey ? st.openCursor(IDBKeyRange.lowerBound(cursorKey,true)) : st.openCursor();
    await new Promise((res2,rej2)=>{
      req.onerror = ()=>rej2(req.error);
      req.onsuccess = e=>{
        const c = e.target.result;
        if(!c){res2();return;}
        res.push(c.value);
        next = c.key;
        if(res.length>=limit){res2();return;}
        c.continue();
      };
    });
    done(JSON.stringify({items:res,cursor:next}));
  } catch(e){ done(JSON.stringify({items:[],cursor:null,error:String(e)})); }
})( );
", "wawc", store, cursor, take);
        var doc = JsonDocument.Parse(raw);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().Select(x => JsonSerializer.Deserialize<object>(x.GetRawText())!).ToArray();
        var next = doc.RootElement.TryGetProperty("cursor", out var c) && c.ValueKind != JsonValueKind.Null ? c.ToString() : null;
        return new IdxBatch(items, next);
    }

    public async Task<IReadOnlyList<CacheEntry>> ListCacheEntriesAsync(int take = 200)
    {
        var js = (IJavaScriptExecutor)_driver;
        var raw = (string)js.ExecuteAsyncScript(@"
const [limit, done] = arguments;
(async () => {
  try{
    const names = await caches.keys();
    const out = [];
    for(const n of names){
      const cache = await caches.open(n);
      const reqs = await cache.keys();
      for(const r of reqs){
        const resp = await cache.match(r);
        out.push({
          key: n,
          url: r.url,
          method: r.method || 'GET',
          status: resp ? resp.status : 0,
          headers: Object.fromEntries(resp ? [...resp.headers.entries()] : [])
        });
        if(out.length>=limit) break;
      }
      if(out.length>=limit) break;
    }
    done(JSON.stringify(out));
  }catch(e){ done('[]'); }
})( );
", take);
        return JsonSerializer.Deserialize<List<CacheEntry>>(raw) ?? new();
    }

    public async Task<byte[]> GetCachedAssetAsync(string url)
    {
        var js = (IJavaScriptExecutor)_driver;
        var b64 = (string)js.ExecuteAsyncScript(@"
const [u, done] = arguments;
(async () => {
  try{
    const names = await caches.keys();
    for(const n of names){
      const cache = await caches.open(n);
      const r = await cache.match(u);
      if(r){
        const buf = await r.arrayBuffer();
        let b=''; const bytes = new Uint8Array(buf);
        for(let i=0;i<bytes.length;i++) b+=String.fromCharCode(bytes[i]);
        done(btoa(b));
        return;
      }
    }
    done('');
  }catch(e){ done(''); }
})( );
", url);
        return string.IsNullOrEmpty(b64) ? Array.Empty<byte>() : Convert.FromBase64String(b64);
    }


    public bool CheckIfLoggedIn(int timeoutSec = 25)
    {
        try
        {
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSec));
            IsLoggedIn = wait.Until(LoggedIn());
        }
        catch { IsLoggedIn = false; }
        return IsLoggedIn;
    }

    public void Dispose()
    {
        try { _driver.Quit(); } catch { }
        try { _driver.Dispose(); } catch { }
    }

    static ChromeDriver CreateDriver(string sessionDir)
    {
        if (IsProfileLocked(sessionDir)) throw new InvalidOperationException("Profile is locked by another Chrome");
        var opt = new ChromeOptions();
        opt.AddArgument("--headless=new");
        opt.AddArgument("--no-sandbox");
        opt.AddArgument("--disable-dev-shm-usage");
        opt.AddArgument("--disable-features=SameSiteByDefaultCookies,CookiesWithoutSameSiteMustBeSecure");
        opt.AddArgument("--window-size=1280,900");
        opt.AddArgument($"--user-data-dir={sessionDir}");
        return new ChromeDriver(opt);
    }

    static bool IsProfileLocked(string dir)
    {
        var lf = Path.Combine(dir, "SingletonLock");
        try
        {
            using var _ = File.Open(lf, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (FileNotFoundException) { return false; }
        catch (IOException) { return true; }
        catch { return true; }
    }

    IWebElement WaitForQrElement(int timeoutSec)
    {
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSec));
        return wait.Until(d =>
        {
            var els = d.FindElements(By.CssSelector("div[data-ref]"));
            return els.FirstOrDefault(x => !string.IsNullOrEmpty(x.GetAttribute("data-ref")));
        })!;
    }

    static string EncodeQrAsSvgDataUri(string payload)
    {
        var qr = QrCode.EncodeText(payload, QrCode.Ecc.Medium);
        var svg = qr.ToSvgString(border: 4);
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{b64}";
    }

    long NextId()
    {
        var ids = Directory.GetDirectories(Root)
            .Select(p => new DirectoryInfo(p).Name)
            .Select(n => long.TryParse(n, out var v) ? v : 0)
            .Where(v => v > 0);
        return ids.Any() ? ids.Max() + 1 : 1;
    }

    void PersistMeta()
    {
        var meta = new SessionMeta(_id, Key);
        File.WriteAllText(Path.Combine(Dir, MetaFileName), JsonSerializer.Serialize(meta));
    }

    public async Task<string?> DumpChatHtmlAsync()
    {
        throw new NotImplementedException();
    }

    record SessionMeta(long Id, Guid Key);
}
