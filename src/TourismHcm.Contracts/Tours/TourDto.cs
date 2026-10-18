namespace TourismHcm.Contracts.Tours;

public sealed class TourDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<TourStopDto> Stops { get; init; } = Array.Empty<TourStopDto>();
}
