namespace TourismHcm.Contracts.Tours;

public sealed class TourStopDto
{
    public Guid Id { get; init; }
    public Guid PlaceId { get; init; }
    public string PlaceName { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int Order { get; init; }
}
