namespace TourismHcm.Domain.Helpers;

public static class GeoHelper
{
    private const double EarthRadiusMeters = 6371000d;

    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    public static (double MinLat, double MaxLat, double MinLng, double MaxLng) BoundingBox(
        double lat, double lng, double radiusMeters)
    {
        var latDelta = radiusMeters / EarthRadiusMeters * (180d / Math.PI);
        var lngDelta = radiusMeters / (EarthRadiusMeters * Math.Cos(DegreesToRadians(lat))) * (180d / Math.PI);
        return (lat - latDelta, lat + latDelta, lng - lngDelta, lng + lngDelta);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
