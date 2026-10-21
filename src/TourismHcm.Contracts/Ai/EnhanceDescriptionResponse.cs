namespace TourismHcm.Contracts.Ai;

public sealed class EnhanceDescriptionResponse
{
    public string EnhancedText { get; init; } = string.Empty;
    public int UsedToday { get; init; }
    public int? DailyLimit { get; init; }
}
