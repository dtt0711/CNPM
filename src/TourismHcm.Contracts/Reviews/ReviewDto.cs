namespace TourismHcm.Contracts.Reviews;

public sealed class ReviewDto
{
    public Guid Id { get; init; }
    public Guid PlaceId { get; init; }
    public Guid UserId { get; init; }
    public string UserDisplayName { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
