namespace TourismHcm.Contracts.Audio;

public sealed class AudioPackManifestDto
{
    public string Lang { get; init; } = string.Empty;
    public IReadOnlyList<AudioPackFileDto> Files { get; init; } = Array.Empty<AudioPackFileDto>();
}

public sealed class AudioPackFileDto
{
    public Guid PlaceId { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
}
