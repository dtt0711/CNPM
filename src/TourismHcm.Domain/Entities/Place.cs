using TourismHcm.Domain.Enums;

namespace TourismHcm.Domain.Entities;

public class Place
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DescriptionVi { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public PlaceCategory Category { get; set; }
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int TriggerRadiusMeters { get; set; } = 30;
    public string OpeningHours { get; set; } = string.Empty;
    public string TicketPrice { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public PlaceStatus Status { get; set; } = PlaceStatus.Published;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<PlaceTranslation> Translations { get; set; } = new List<PlaceTranslation>();
    public ICollection<AudioAsset> AudioAssets { get; set; } = new List<AudioAsset>();
    public ICollection<AudioGenerationTask> AudioTasks { get; set; } = new List<AudioGenerationTask>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<TourStop> TourStops { get; set; } = new List<TourStop>();
}
