namespace TourismHcm.Contracts.Reviews;

public sealed class CreateReviewRequest
{
    public Guid PlaceId { get; init; }
    public int Rating { get; init; }
    public string? Comment { get; init; }
}
