using Microsoft.EntityFrameworkCore;
using Nagorik.Api.Models;
using Nagorik.Api.Services;
public class CivicCaseTests
{
    sealed class Clock:TimeProvider{public override DateTimeOffset GetUtcNow()=>new(2026,10,23,16,0,0,TimeSpan.Zero);}
    internal static CivicCaseInput Input(string kind)=>new(){Subtype=kind=="Noise"?"Commercial":"Dust",Description="A persistent observed disturbance near the landmark.",Address="Dhanmondi fixture landmark",Latitude=23.75,Longitude=90.37,ObservedAtUtc=new Clock().GetUtcNow().UtcDateTime,SubmissionToken=Guid.NewGuid().ToString()};
    [Theory][InlineData("Noise")][InlineData("Construction")]
    public async Task IdempotentPrivateConcernsHaveRevisionCheckedReviewHistory(string kind)
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;TestDb.AddUser(db,1);var official=TestDb.AddUser(db,2);official.Role="Official";TestDb.AddUser(db,3);await db.SaveChangesAsync();var s=new CivicCaseService(db,new Clock());var input=Input(kind);
        var row=await s.SubmitAsync(kind,input,1);Assert.Equal(row.Id,(await s.SubmitAsync(kind,input,1)).Id);Assert.Single(db.CivicCases);
        input.Description="Changed submitted content for the same request token.";Assert.Equal(409,(await Assert.ThrowsAsync<TransportProblem>(()=>s.SubmitAsync(kind,input,1))).Status);
        Assert.Equal(404,(await Assert.ThrowsAsync<TransportProblem>(()=>s.DetailAsync(kind,row.Id,3,false))).Status);
        Assert.Equal(403,(await Assert.ThrowsAsync<TransportProblem>(()=>s.ReviewAsync(kind,row.Id,new(){ExpectedRevision=1,State="UnderReview",Note="Investigation has started."},3))).Status);
        await s.ReviewAsync(kind,row.Id,new(){ExpectedRevision=1,State="UnderReview",Note="Investigation has started."},2);
        Assert.Equal(409,(await Assert.ThrowsAsync<TransportProblem>(()=>s.ReviewAsync(kind,row.Id,new(){ExpectedRevision=1,State="Resolved",Note="Inspection has concluded."},2))).Status);
        await s.ReviewAsync(kind,row.Id,new(){ExpectedRevision=2,State="Resolved",Note="Inspection has concluded."},2);
        var detail=await s.DetailAsync(kind,row.Id,1,false);Assert.Equal("Resolved",detail.Case.State);Assert.Equal(2,detail.Reviews.Count);Assert.Null(detail.Case.ReportedDecibels);
    }
    [Theory][InlineData("Noise")][InlineData("Construction")]
    public async Task MissingTokenFutureObservationsAndOutsideLocationsAreRejected(string kind)
    {
        var(db,c)=TestDb.Create();await using var d=db;await using var connection=c;TestDb.AddUser(db);var s=new CivicCaseService(db,new Clock());var input=Input(kind);input.SubmissionToken="";
        Assert.Equal(400,(await Assert.ThrowsAsync<TransportProblem>(()=>s.SubmitAsync(kind,input,1))).Status);
        input=Input(kind);input.ObservedAtUtc=input.ObservedAtUtc!.Value.AddDays(1);await Assert.ThrowsAsync<TransportProblem>(()=>s.SubmitAsync(kind,input,1));
        input=Input(kind);input.Latitude=double.NaN;await Assert.ThrowsAsync<TransportProblem>(()=>s.SubmitAsync(kind,input,1));Assert.Empty(db.CivicCases);
    }
}
