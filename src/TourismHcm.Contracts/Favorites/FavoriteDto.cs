namespace TourismHcm.Contracts.Favorites;

public sealed class FavoriteDto
{
    public Guid PlaceId { get; init; }
    public string PlaceName { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
