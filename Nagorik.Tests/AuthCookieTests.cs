using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Nagorik.Api.Controllers;
using Nagorik.Api.Models;
namespace Nagorik.Tests;
public class AuthCookieTests
{
    sealed class Env(string name):IHostEnvironment
    {public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="Tests";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    [Theory]
    [InlineData("Development","http",false)]
    [InlineData("Development","https",true)]
    [InlineData("Production","http",true)]
    [InlineData("Staging","https",true)]
    public void LoginIssuesHttpOnlyCookieWithSecureFlagOutsideLocalHttpDevelopment(string environment,string scheme,bool secure)
    {
        var(db,c)=TestDb.Create();using var d=db;using var connection=c;var user=TestDb.AddUser(db);user.PasswordHash=BCrypt.Net.BCrypt.HashPassword("Cookie-fixture-123!",4);db.SaveChanges();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Jwt:Key","isolated-cookie-policy-test-signing-key-32-bytes"}}).Build();
        var context=new DefaultHttpContext();context.Request.Scheme=scheme;
        var controller=new AuthController(db,config,new Env(environment)){ControllerContext=new(){HttpContext=context}};
        Assert.IsType<OkObjectResult>(controller.Login(new LoginDto{Email=user.Email,Password="Cookie-fixture-123!"}));
        var cookie=context.Response.Headers.SetCookie.ToString();Assert.Contains("httponly",cookie,StringComparison.OrdinalIgnoreCase);Assert.Contains("samesite=lax",cookie,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(secure,cookie.Contains("; secure",StringComparison.OrdinalIgnoreCase));
    }
}
