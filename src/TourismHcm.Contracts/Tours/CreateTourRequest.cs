namespace TourismHcm.Contracts.Tours;

public sealed class CreateTourRequest
{
    public string Name { get; init; } = string.Empty;
    public List<Guid> PlaceIds { get; init; } = new();
    public double? StartLatitude { get; init; }
    public double? StartLongitude { get; init; }
}
