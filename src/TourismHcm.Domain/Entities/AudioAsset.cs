using TourismHcm.Domain.Enums;

namespace TourismHcm.Domain.Entities;

public class AudioAsset
{
    public Guid Id { get; set; }
    public Guid PlaceId { get; set; }
    public string Lang { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public double DurationSec { get; set; }
    public AudioAssetStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Place Place { get; set; } = null!;
}
