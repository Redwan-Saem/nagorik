using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Nagorik.Api.Data;
using Nagorik.Api.Models;

public class TransportCatalogTests
{
    sealed class Env(string name):IHostEnvironment
    { public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="Tests";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider(); }
    [Fact] public async Task SamplesNeverSeedProductionAndDevelopmentIsIdempotent()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;var config=new ConfigurationBuilder().Build();
        await DevelopmentTransportSeeder.SeedAsync(db,new Env("Production"),config);Assert.Empty(db.TransportStops);
        await DevelopmentTransportSeeder.SeedAsync(db,new Env("Development"),config);await DevelopmentTransportSeeder.SeedAsync(db,new Env("Development"),config);
        Assert.Equal(5,await db.TransportStops.CountAsync());Assert.Equal(16,await db.TransportLinks.CountAsync());Assert.All(db.TransportLinks,x=>Assert.True(x.IsSample));
    }
    [Fact] public async Task InvalidLinksAndFareRulesFailDatabaseConstraints()
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;
        db.TransportFareRules.Add(new(){Mode="Bus",PerKmBdt=-1});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        db.ChangeTracker.Clear();db.TransportStops.Add(new(){Name="Bad",Latitude=100,Longitude=90});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }
}
