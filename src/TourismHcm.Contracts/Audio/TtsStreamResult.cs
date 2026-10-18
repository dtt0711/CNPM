namespace TourismHcm.Contracts.Audio;

public sealed class TtsStreamResult
{
    public Stream Content { get; init; } = Stream.Null;
    public string ContentType { get; init; } = "audio/mpeg";
    public double DurationSec { get; init; }
    public bool FromCache { get; init; }
}
