namespace TourismHcm.Domain.Entities;

public class AnalyticsDevice
{
    public Guid Id { get; set; }
    public string DeviceKeyHash { get; set; } = string.Empty;
    public bool ConsentAnalytics { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public ICollection<AnalyticsSession> Sessions { get; set; } = new List<AnalyticsSession>();
}
