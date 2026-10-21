namespace TourismHcm.Domain.Entities;

public class AnalyticsSession
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public bool ConsentAnalytics { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }

    public AnalyticsDevice Device { get; set; } = null!;
    public ICollection<AnalyticsEvent> Events { get; set; } = new List<AnalyticsEvent>();
}
