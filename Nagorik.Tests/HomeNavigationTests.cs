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
    [InlineData("UtilityOfficial", "/official/loadshedding")]
    [InlineData("DPDCOfficial", "/official/loadshedding")]
    [InlineData("DESCOOfficial", "/official/loadshedding")]
    [InlineData(null, "/map")]
    public void HomeShowsApplicableLocalDestinationsForEveryRole(string? role, string expected)
    {
        var context = new DefaultHttpContext();
        if (role != null) context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, role)], "test"));
        var controller = new HomeController { ControllerContext = new ControllerContext { HttpContext = context } };
        var model=Assert.IsType<Nagorik.Api.Models.DashboardViewModel>(Assert.IsType<ViewResult>(controller.Index()).Model);
        Assert.Contains(model.Work.Concat(model.Public),x=>x.Href==expected);
        Assert.All(model.Work.Concat(model.Public),x=>{Assert.StartsWith("/",x.Href);Assert.False(x.Href.StartsWith("//"));});
        if(role==null)Assert.Empty(model.Work);
        if(role=="Resident")Assert.DoesNotContain(model.Work,x=>x.Href.StartsWith("/admin/")||x.Href.StartsWith("/official/"));
        Assert.Equal("/map", Assert.IsType<RedirectResult>(new PredictionsController().Details(1)).Url);
    }
}
