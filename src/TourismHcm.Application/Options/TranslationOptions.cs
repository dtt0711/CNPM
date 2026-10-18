namespace TourismHcm.Application.Options;

public sealed class TranslationOptions
{
    public const string SectionName = "Translation";

    /// <summary>
    /// "Google", "Azure" or "Fake".
    /// </summary>
    public string Provider { get; set; } = "Fake";

    public string ApiKey { get; set; } = string.Empty;

    public string? Endpoint { get; set; }
}
