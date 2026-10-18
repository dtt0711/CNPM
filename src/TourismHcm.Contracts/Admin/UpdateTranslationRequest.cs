namespace TourismHcm.Contracts.Admin;

public sealed class UpdateTranslationRequest
{
    public Guid PlaceId { get; init; }
    public string Lang { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}
