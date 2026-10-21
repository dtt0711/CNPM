namespace TourismHcm.Contracts.Analytics;

public sealed class AnalyticsAdminSummaryDto
{
    public IReadOnlyList<AnalyticsHourlyMetricDto> Hourly { get; init; } = Array.Empty<AnalyticsHourlyMetricDto>();
}

public sealed class AnalyticsHourlyMetricDto
{
    public DateTimeOffset HourUtc { get; init; }
    public string MetricKey { get; init; } = string.Empty;
    public long Count { get; init; }
}
