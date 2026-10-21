namespace TourismHcm.Contracts.Localization;

public sealed class OnDemandLocalizationResult
{
    public Guid PlaceId { get; init; }

    public string Lang { get; init; } = string.Empty;

    public string Status { get; init; } = "ready";

    public string? AudioUrl { get; init; }

    public Guid? TaskId { get; init; }

    public int? RetryAfterSeconds { get; init; }
}

public sealed class WarmupResult
{
    public string Lang { get; init; } = string.Empty;

    public int Queued { get; init; }

    public int AlreadyReady { get; init; }
}

public sealed class HotsetResult
{
    public string Lang { get; init; } = string.Empty;

    public IReadOnlyList<Guid> PlaceIds { get; init; } = [];

    public int ReadyCount { get; init; }

    public bool IsReady { get; init; }
}
