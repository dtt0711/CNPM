namespace TourismHcm.Domain.Entities;

public class Favorite
{
    public Guid UserId { get; set; }
    public Guid PlaceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Place Place { get; set; } = null!;
}
