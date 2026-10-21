namespace TourismHcm.Application.Options;

public sealed class TranslationOptions
{
    public const string SectionName = "Translation";

    /// <summary>
    /// "Free" (sidecar deep-translator) or "Google" (Cloud API). "Fake" is ignored outside Testing.
    /// </summary>
    public string Provider { get; set; } = "Free";

    public string ApiKey { get; set; } = string.Empty;

    public string? Endpoint { get; set; }
}
