using System.Security.Claims;
using Nagorik.Api.Models;
namespace Nagorik.Api.Services;
public static class DashboardCatalog
{
    public static DashboardViewModel For(ClaimsPrincipal user)
    {
        var work=new List<DashboardCard>();
        if(user.IsInRole("Resident"))work.AddRange([
            new("Report a city issue","Submit a located issue with a photo and track its status.","/Reports/Create"),
            new("SOS assistance","Review an assistance request, share a location and follow its status.","/sos"),
            new("My reports","Review your submitted reports and progress history.","/Reports/Mine"),
            new("Utility anomaly","Report a utility problem with its provider and evidence.","/reports/utility-anomalies/create"),
            new("Loadshedding plan","Check your zone's published seven-day outage plan.","/resident/loadshedding"),
            new("My areas and alerts","Choose areas and notification preferences.","/resident/areas"),
            new("Noise concerns","Submit a recent noise observation and follow city review.","/resident/concerns/noise"),
            new("Construction concerns","Report a suspected site concern for inspection review.","/resident/concerns/construction"),
            new("Notification settings","Manage notification preferences and device subscription.","/Notifications/Settings")]);
        if(user.IsInRole("Dispatcher"))work.AddRange([
            new("Dispatcher risk map","Inspect risk factors, located reports, drainage and crew locations.","/dispatcher/map"),
            new("Flagged drainage","Review blocked drainage and assign an available crew.","/dispatcher/flagged-drainage"),
            new("Verify public markers","Review reports before publishing them on the public map.","/dispatcher/report-verification")]);
        if(user.IsInRole("Crew"))work.Add(new("My crew tasks","Review assigned work and update its status with optional location capture.","/crew/tasks"));
        if(Authorization.OfficialAccess.IsOfficial(user))work.AddRange([
            new("City reports","Filter and inspect resident reports and their current status.","/official/reports"),
            new("Waste schedule management","Draft and publish sourced collection windows.","/official/services/waste"),
            new("Review noise concerns","Investigate private noise concerns and record a disposition.","/official/concerns/noise"),
            new("Review construction concerns","Record inspection review and explain the outcome.","/official/concerns/construction")]);
        if(Authorization.UtilityScheduleAccess.IsOfficial(user))work.Add(new("Loadshedding management","Generate, review, edit and publish an outage plan.","/official/loadshedding"));
        if(user.IsInRole("UtilityOfficial")||user.IsInRole("WASAOfficial")||user.IsInRole("Admin"))work.Add(new("Water schedule management","Draft and publish sourced water supply windows.","/official/services/water"));
        if(user.IsInRole("Admin"))work.AddRange([
            new("Geographic and transport data","Manage sourced boundaries, metrics and curated transport catalogs.","/admin/data"),
            new("Risk thresholds","Manage rule thresholds and explicitly evaluate current inputs.","/admin/risk-thresholds"),
            new("Risk alert diagnostics","Inspect delivery and retry/suppression status.","/admin/risk-alert-deliveries"),
            new("Waste schedule management","Manage published collection plans.","/official/services/waste"),
            new("Review noise concerns","Inspect the city review queue.","/official/concerns/noise"),
            new("Review construction concerns","Inspect construction review history.","/official/concerns/construction")]);
        DashboardCard[] publicCards=[
            new("Area risk map","Explore rule-based area risk and publicly verified issue markers.","/map"),
            new("Transport routes and fares","Compare available catalog routes and estimated BDT fares.","/transport"),
            new("Recent bus occupancy","Check recent community observations for a bus segment.","/transport/occupancy"),
            new("Water supply schedule","Read your zone's published planned supply windows.","/services/water"),
            new("Waste collection schedule","Check published pickup windows and collection notes.","/services/waste"),
            new("Area air quality","Inspect attributed regional AQI and particulate estimates.","/air-quality")];
        return new(user.Identity?.IsAuthenticated==true,string.Join(", ",user.FindAll(ClaimTypes.Role).Select(x=>x.Value).Distinct()),work.DistinctBy(x=>x.Href).ToArray(),publicCards);
    }
}
