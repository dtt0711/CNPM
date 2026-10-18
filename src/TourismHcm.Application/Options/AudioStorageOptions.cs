namespace TourismHcm.Application.Options;

public sealed class AudioStorageOptions
{
    public const string SectionName = "AudioStorage";

    public string RootPath { get; set; } = "audio-storage";

    public string PublicBaseUrl { get; set; } = "/audio";
}
