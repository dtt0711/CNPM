using TourismHcm.Domain.Enums;

namespace TourismHcm.Domain.Entities;

public class UiTranslationBundle
{
    public Guid Id { get; set; }
    public string Namespace { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public UiTranslationBundleStatus Status { get; set; } = UiTranslationBundleStatus.Pending;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
