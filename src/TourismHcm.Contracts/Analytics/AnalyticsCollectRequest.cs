namespace TourismHcm.Contracts.Analytics;

public sealed class AnalyticsCollectRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public Guid SessionId { get; init; }
    public bool Consent { get; init; }
    public IReadOnlyList<AnalyticsEventInput> Events { get; init; } = Array.Empty<AnalyticsEventInput>();
}

public sealed class AnalyticsEventInput
{
    public string EventName { get; init; } = string.Empty;
    public string? PayloadJson { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
}
