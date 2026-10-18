namespace TourismHcm.Application.Options;

public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    /// <summary>
    /// "Azure" or "Fake".
    /// </summary>
    public string Provider { get; set; } = "Fake";

    public string AzureKey { get; set; } = string.Empty;

    public string AzureRegion { get; set; } = string.Empty;

    /// <summary>
    /// Maps a language code (e.g. "vi") to a provider-specific voice identifier.
    /// </summary>
    public Dictionary<string, string> Voices { get; set; } = new();
}
