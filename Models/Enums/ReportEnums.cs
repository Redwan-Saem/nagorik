namespace Nagorik.Api.Models;

public enum ReportCategory
{
    Complaint,
    Request,
    Emergency,
    WaterLogging,
    RoadDamage,
    PowerOutage,
    Other
}

public enum Authority
{
    Police,
    LocalGovernment,
    CityCorporation,
    DpdcDesco,
    Other
}

public enum ReportStatus
{
    Pending,
    Approved,
    Assigned,
    InProgress,
    Completed,
    Rejected
}