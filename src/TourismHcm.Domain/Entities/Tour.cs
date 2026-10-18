namespace TourismHcm.Domain.Entities;

public class Tour
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<TourStop> Stops { get; set; } = new List<TourStop>();
}

public class TourStop
{
    public Guid Id { get; set; }
    public Guid TourId { get; set; }
    public Guid PlaceId { get; set; }
    public int Order { get; set; }

    public Tour Tour { get; set; } = null!;
    public Place Place { get; set; } = null!;
}
