namespace TourismHcm.Contracts.Ai;

public sealed class EnhanceDescriptionRequest
{
    public string SourceText { get; init; } = string.Empty;
    public string Lang { get; init; } = "vi";
}
