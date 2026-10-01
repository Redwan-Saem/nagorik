using OpenQA.Selenium;
using Xunit;

public class ServiceWorkerTests : UiTestBase
{
    [Fact]
    public void ServiceWorker_ContainsShowNotification()
    {
        Driver.Navigate().GoToUrl($"{BaseUrl}/sw.js");

        Assert.Contains(
            "showNotification",
            Driver.PageSource);
    }
}