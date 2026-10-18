namespace TourismHcm.Contracts.Places;

public sealed class UpdatePlaceRequest
{
    public string? Name { get; init; }
    public string? DescriptionVi { get; init; }
    public string? Category { get; init; }
    public string? District { get; init; }
    public string? Address { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public int? TriggerRadiusMeters { get; init; }
    public string? OpeningHours { get; init; }
    public string? TicketPrice { get; init; }
    public string? ImageUrl { get; init; }
    public string? Tags { get; init; }
    public string? Status { get; init; }
}
