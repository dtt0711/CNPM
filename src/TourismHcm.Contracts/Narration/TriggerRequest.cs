namespace TourismHcm.Contracts.Narration;

public sealed record TriggerRequest
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Lang { get; init; } = "vi";
    public string? ClientId { get; init; }
}
