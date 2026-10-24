using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

public sealed class ServiceWorkerTests:IAsyncLifetime
{
    private WebApplication app=null!;private ChromeDriver? browser;private string url="";
    public async Task InitializeAsync()
    {
        var repo=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ContentRootPath=repo,WebRootPath=Path.Combine(repo,"wwwroot"),EnvironmentName="Development"});
        builder.Logging.ClearProviders();builder.WebHost.UseUrls("http://127.0.0.1:0");app=builder.Build();app.UseStaticFiles();
        app.MapGet("/worker-fixture",()=>Results.Content("<!doctype html><html><body>Isolated worker fixture</body></html>","text/html"));
        app.MapGet("/api/private-fixture",()=>Results.Json(new{value="Fixture API response must not be cached"}));
        await app.StartAsync();url=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }
    [Fact]public void WorkerActivatesAndCachesOnlyItsPublicShellAssets()
    {
        var options=new ChromeOptions();options.AddArgument("--headless=new");options.AddArgument("--window-size=1280,900");
        var service=ChromeDriverService.CreateDefaultService();service.HideCommandPromptWindow=true;browser=new ChromeDriver(service,options,TimeSpan.FromSeconds(90));
        browser.Navigate().GoToUrl(url+"/worker-fixture");
        Assert.Equal("ready",browser.ExecuteAsyncScript("const done=arguments[arguments.length-1];navigator.serviceWorker.register('/sw.js').then(()=>navigator.serviceWorker.ready).then(()=>done('ready'),e=>done(e.name));"));
        new WebDriverWait(browser,TimeSpan.FromSeconds(30)).Until(_=>browser.ExecuteScript("return !!navigator.serviceWorker.controller") is true);
        Assert.Equal("Fixture API response must not be cached",browser.ExecuteAsyncScript("const done=arguments[arguments.length-1];fetch('/api/private-fixture').then(r=>r.json()).then(x=>done(x.value));"));
        var paths=Assert.IsType<string>(browser.ExecuteAsyncScript("const done=arguments[arguments.length-1];caches.open('nagorik-resident-schedule-shell-v1').then(c=>c.keys()).then(keys=>done(keys.map(k=>new URL(k.url).pathname).join('|')));"));
        Assert.Contains("/resident-loadshedding-offline.html",paths);Assert.Contains("/js/resident-loadshedding.js",paths);Assert.DoesNotContain("/api/",paths);Assert.DoesNotContain("/worker-fixture",paths);
    }
    public async Task DisposeAsync(){browser?.Quit();await app.StopAsync();await app.DisposeAsync();}
}
