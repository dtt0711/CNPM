namespace TourismHcm.Contracts.Maps;

public sealed class MapPackManifestDto
{
    public string PackId { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string Version { get; init; } = "1";
    public IReadOnlyList<MapPackFileDto> Files { get; init; } = Array.Empty<MapPackFileDto>();
}

public sealed class MapPackFileDto
{
    public string Path { get; init; } = string.Empty;
    public string? Sha256 { get; init; }
    public long? SizeBytes { get; init; }
}
