namespace TourismHcm.Contracts.Analytics;

public sealed class AnalyticsAdminSummaryDto
{
    /// <summary>Distinct devices with a presence ping in the last few minutes.</summary>
    public int ActiveDevices { get; init; }

    public IReadOnlyList<AnalyticsHourlyMetricDto> Hourly { get; init; } = Array.Empty<AnalyticsHourlyMetricDto>();
}

public sealed class AnalyticsHourlyMetricDto
{
    public DateTimeOffset HourUtc { get; init; }
    public string MetricKey { get; init; } = string.Empty;
    public long Count { get; init; }
}
