using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Nagorik.Api.Authorization;
using Nagorik.Api.Models;

namespace Nagorik.Tests;

public sealed class LoadsheddingValidationTests
{
    [Theory]
    [InlineData("Resident",false)] [InlineData("Admin",false)] [InlineData("Official",false)]
    [InlineData("Crew",false)] [InlineData("Dispatcher",false)]
    [InlineData("UtilityOfficial",true)] [InlineData("DPDCOfficial",true)] [InlineData("DESCOOfficial",true)]
    public void OnlyUtilityRolesCanManageSchedules(string role,bool allowed) => Assert.Equal(allowed,
        UtilityScheduleAccess.IsOfficial(new(new ClaimsIdentity([new Claim(ClaimTypes.Role,role)],"test"))));

    [Fact]
    public void InvalidPeriodDuplicateZonesAndMissingFieldsFailValidation()
    {
        var request=new GenerateScheduleRequest();var errors=new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request,new(request),errors,true));
        request.StartDate=new(2026,10,23);request.EndDate=new(2026,10,22);request.AvailableCapacityMw=20;
        request.Zones=[new(){ZoneId=1,DemandMw=10}];errors.Clear();
        Assert.False(Validator.TryValidateObject(request,new(request),errors,true));
        request.EndDate=request.StartDate;request.Zones.Add(new(){ZoneId=1,DemandMw=10});errors.Clear();
        Assert.False(Validator.TryValidateObject(request,new(request),errors,true));
        request.Zones.RemoveAt(1);errors.Clear();Assert.True(Validator.TryValidateObject(request,new(request),errors,true));
    }

    [Fact]
    public void PublicationRequiresExplicitConfirmationAndRevision()
    {
        var errors=new List<ValidationResult>();var request=new PublishScheduleRequest();
        Assert.False(Validator.TryValidateObject(request,new(request),errors,true));
        request.ExpectedRevision=1;request.Confirm=true;errors.Clear();Assert.True(Validator.TryValidateObject(request,new(request),errors,true));
    }
}
