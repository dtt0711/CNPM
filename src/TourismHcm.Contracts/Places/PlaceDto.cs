namespace TourismHcm.Contracts.Places;

public record PlaceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Ward { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int TriggerRadiusMeters { get; init; }
    public string OpeningHours { get; init; } = string.Empty;
    public string TicketPrice { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string[] Tags { get; init; } = Array.Empty<string>();
    public double AverageRating { get; init; }
    public int ReviewCount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
