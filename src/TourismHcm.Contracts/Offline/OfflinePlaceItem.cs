namespace TourismHcm.Contracts.Offline;

public sealed class OfflinePlaceItem
{
    public Guid PlaceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int TriggerRadiusMeters { get; init; }
    public IDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
    public IDictionary<string, string> AudioUrls { get; init; } = new Dictionary<string, string>();
}
