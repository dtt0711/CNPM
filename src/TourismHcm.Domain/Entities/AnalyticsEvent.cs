namespace TourismHcm.Domain.Entities;

public class AnalyticsEvent
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public bool ConsentAnalytics { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public AnalyticsSession Session { get; set; } = null!;
}
