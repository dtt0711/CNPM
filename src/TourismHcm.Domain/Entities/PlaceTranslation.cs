using TourismHcm.Domain.Enums;

namespace TourismHcm.Domain.Entities;

public class PlaceTranslation
{
    public Guid Id { get; set; }
    public Guid PlaceId { get; set; }
    public string Lang { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public TranslationSource Source { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }

    public Place Place { get; set; } = null!;
}
