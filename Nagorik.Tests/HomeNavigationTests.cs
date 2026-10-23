using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Controllers;

namespace Nagorik.Tests;

public sealed class HomeNavigationTests
{
    [Theory]
    [InlineData("Admin", "/admin/risk-thresholds")]
    [InlineData("Dispatcher", "/dispatcher/map")]
    [InlineData("Crew", "/crew/tasks")]
    [InlineData("Official", "/official/reports")]
    [InlineData("CityOfficial", "/official/reports")]
    [InlineData("Resident", "/map")]
    [InlineData(null, "/map")]
    public void HomeHasLocalDestinationForEveryRole(string? role, string expected)
    {
        var context = new DefaultHttpContext();
        if (role != null) context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)], "test"));
        var controller = new HomeController { ControllerContext = new ControllerContext { HttpContext = context } };
        Assert.Equal(expected, Assert.IsType<RedirectResult>(controller.Index()).Url);
        Assert.Equal("/map", Assert.IsType<RedirectResult>(new PredictionsController().Details(1)).Url);
    }
}
