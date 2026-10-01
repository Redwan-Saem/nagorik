namespace Nagorik.Api.Services;

public static class DhakaTime
{
    private static readonly TimeZoneInfo Tz = Find();

    private static TimeZoneInfo Find()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Bangladesh Standard Time");
        }
    }

    public static DateTime ToLocal(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            Tz
        );
    }
}