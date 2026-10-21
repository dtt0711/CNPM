namespace TourismHcm.Domain.Entities;

public class AiUsageLimit
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime DateUtc { get; set; }
    public int Count { get; set; }

    public User User { get; set; } = null!;
}
