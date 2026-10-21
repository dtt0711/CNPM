namespace TourismHcm.Application.Options;

public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    /// <summary>
    /// "Edge" (sidecar) or "Azure". The value "Fake" is ignored outside Testing.
    /// </summary>
    public string Provider { get; set; } = "Edge";

    public string AzureKey { get; set; } = string.Empty;

    public string AzureRegion { get; set; } = string.Empty;

    /// <summary>
    /// Maps a language code (e.g. "vi") to a provider-specific voice identifier.
    /// </summary>
    public Dictionary<string, string> Voices { get; set; } = new();
}
