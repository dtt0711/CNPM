namespace TourismHcm.Application.Options;

public sealed class GeofenceOptions
{
    public const string SectionName = "Geofence";

    public int CooldownMinutes { get; set; } = 5;

    public int DefaultRadiusMeters { get; set; } = 30;

    /// <summary>
    /// Maximum radius (meters) used when scanning for nearby places that may overlap the
    /// client's current position. Should be larger than any individual place's trigger radius.
    /// </summary>
    public int MaxScanRadiusMeters { get; set; } = 500;
}
