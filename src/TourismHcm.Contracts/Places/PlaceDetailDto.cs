namespace TourismHcm.Contracts.Places;

public sealed record PlaceDetailDto : PlaceDto
{
    public IReadOnlyList<PlaceTranslationDto> Translations { get; init; } = Array.Empty<PlaceTranslationDto>();
    public IReadOnlyList<string> AvailableAudioLanguages { get; init; } = Array.Empty<string>();
}

public sealed record PlaceTranslationDto
{
    public string Lang { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; init; }
}
