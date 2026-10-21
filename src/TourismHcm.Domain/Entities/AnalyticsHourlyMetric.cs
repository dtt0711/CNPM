namespace TourismHcm.Domain.Entities;

public class AnalyticsHourlyMetric
{
    public Guid Id { get; set; }
    public DateTimeOffset HourUtc { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public long Count { get; set; }
}
