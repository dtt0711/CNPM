namespace TourismHcm.Contracts.Places;

public sealed record NearbyPlaceDto : PlaceDto
{
    public double DistanceMeters { get; init; }
}
