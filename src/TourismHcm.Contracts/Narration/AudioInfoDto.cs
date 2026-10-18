namespace TourismHcm.Contracts.Narration;

public sealed class AudioInfoDto
{
    public bool Available { get; init; }
    public string? Url { get; init; }
    public double DurationSec { get; init; }
    public string Status { get; init; } = string.Empty;
}
