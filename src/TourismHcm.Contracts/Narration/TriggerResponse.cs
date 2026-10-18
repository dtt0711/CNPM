namespace TourismHcm.Contracts.Narration;

public sealed class TriggerResponse
{
    public Guid PlaceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public double DistanceMeters { get; init; }

    /// <summary>
    /// True when the visitor entered the geofence and narration should play (no active cooldown).
    /// </summary>
    public bool Triggered { get; init; }

    /// <summary>
    /// True when the same clientId+placeId was already triggered within the cooldown window.
    /// </summary>
    public bool CooldownActive { get; init; }

    public NarrationResponse Narration { get; init; } = null!;
}
