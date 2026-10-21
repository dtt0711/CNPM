namespace TourismHcm.Contracts.Places;

public sealed class CreatePlaceRequest
{
    public string Name { get; init; } = string.Empty;
    public string DescriptionVi { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Ward { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int? TriggerRadiusMeters { get; init; }
    public string OpeningHours { get; init; } = string.Empty;
    public string TicketPrice { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
}
