namespace TourismHcm.Contracts.Maps;

public sealed class MapOfflineOptionsDto
{
    public string DefaultScope { get; init; } = string.Empty;
    public IReadOnlyList<string> AvailablePacks { get; init; } = Array.Empty<string>();
}
