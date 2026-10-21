namespace TourismHcm.Domain.Entities;

public class OwnerNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? RelatedSubmissionId { get; set; }

    public User User { get; set; } = null!;
}
