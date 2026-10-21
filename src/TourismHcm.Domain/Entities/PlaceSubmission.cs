using TourismHcm.Domain.Enums;

namespace TourismHcm.Domain.Entities;

public class PlaceSubmission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? PlaceId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public PlaceSubmissionStatus Status { get; set; } = PlaceSubmissionStatus.Pending;
    public string? AdminNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    public User User { get; set; } = null!;
    public Place? Place { get; set; }
}
