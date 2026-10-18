namespace TourismHcm.Contracts.Narration;

public sealed class NarrationResponse
{
    public Guid PlaceId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string RequestedLang { get; init; } = string.Empty;
    public string ActualLang { get; init; } = string.Empty;
    public bool IsFallback { get; init; }
    public string Text { get; init; } = string.Empty;
    public AudioInfoDto Audio { get; init; } = new();
}
