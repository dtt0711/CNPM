namespace TourismHcm.Contracts.Offline;

public sealed class OfflinePackManifest
{
    public string Version { get; init; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<OfflinePlaceItem> Places { get; init; } = Array.Empty<OfflinePlaceItem>();
}
