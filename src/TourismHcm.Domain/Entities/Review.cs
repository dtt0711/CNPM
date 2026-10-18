namespace TourismHcm.Domain.Entities;

public class Review
{
    public Guid Id { get; set; }
    public Guid PlaceId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Place Place { get; set; } = null!;
    public User User { get; set; } = null!;
}
