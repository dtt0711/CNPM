namespace TourismHcm.Domain.Entities;

public class AnalyticsDailyMetric
{
    public Guid Id { get; set; }
    public DateTime DateUtc { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public long Count { get; set; }
}
