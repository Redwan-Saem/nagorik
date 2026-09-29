using OpenQA.Selenium; using OpenQA.Selenium.Chrome; using OpenQA.Selenium.Support.UI;

public abstract class UiTestBase : IDisposable
{
    protected static readonly string BaseUrl = Environment.GetEnvironmentVariable("NAGORIK_URL") ?? "http://localhost:5000";
    private readonly List<IWebDriver> _drivers = new();
    protected IWebDriver Driver;
    protected WebDriverWait Wait => new(Driver, TimeSpan.FromSeconds(10));

    protected UiTestBase() { Driver = CreateDriver(); }

    protected virtual void ConfigureChrome(ChromeOptions o) { }

    protected IWebDriver CreateDriver()
    {
        var o = new ChromeOptions();
        o.AddArgument("--window-size=1280,900");
        // o.AddArgument("--headless=new");   // uncomment to run without a visible window
        ConfigureChrome(o);
        var d = new ChromeDriver(o);
        _drivers.Add(d);
        return d;
    }

    protected IWebDriver CreateLoggedInDriver(string email)
    {
        var d = CreateDriver(); LoginAs(d, email); return d;
    }

    protected void LoginAs(IWebDriver d, string email, string password = "Test@12345")
    {
        d.Navigate().GoToUrl($"{BaseUrl}/Identity/Account/Login");
        d.FindElement(By.Id("Input_Email")).SendKeys(email);       // adjust ids to your NAG-021 login page
        d.FindElement(By.Id("Input_Password")).SendKeys(password);
        d.FindElement(By.Id("login-submit")).Click();
        new WebDriverWait(d, TimeSpan.FromSeconds(10)).Until(x => !x.Url.Contains("Login"));
    }

    public void Dispose() { foreach (var d in _drivers) d.Quit(); }
}