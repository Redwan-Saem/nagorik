namespace Nagorik.Api.Models
{
    public enum ReportCategory
    {
        Complaint,
        Request,
        Emergency
    }

    public enum Authority
    {
        Police,
        LocalGovernment,
        Other
    }

    public enum ReportStatus
    {
        Pending,
        Approved,
        Rejected
    }
}