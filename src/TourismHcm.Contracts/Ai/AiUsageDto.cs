namespace TourismHcm.Contracts.Ai;

public sealed class AiUsageDto
{
    public int UsedToday { get; init; }
    public int? DailyLimit { get; init; }
    public bool IsUnlimited { get; init; }
}
