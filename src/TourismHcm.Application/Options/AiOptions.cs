namespace TourismHcm.Application.Options;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string GeminiApiKey { get; set; } = string.Empty;

    public string GeminiModel { get; set; } = "gemini-2.0-flash";

    /// <summary>Optional ProxyPal / alternate gateway base URL.</summary>
    public string? ProxyPalBaseUrl { get; set; }

    public int OwnerDailyLimit { get; set; } = 10;

    public int TimeoutSeconds { get; set; } = 30;
}
