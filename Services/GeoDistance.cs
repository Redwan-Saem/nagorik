namespace Nagorik.Api.Services;

public static class GeoDistance
{
    public static double Meters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;

        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRad(lat1)) *
            Math.Cos(ToRad(lat2)) *
            Math.Sin(dLon / 2) *
            Math.Sin(dLon / 2);

        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    static double ToRad(double d) =>
        d * Math.PI / 180;
}