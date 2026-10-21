namespace TourismHcm.Application.Options;

/// <summary>HTTP sidecar hosting Edge-TTS and free Google Translate (deep-translator).</summary>
public sealed class ProvidersOptions
{
    public const string SectionName = "Providers";

    /// <summary>Base URL of the Python providers service, e.g. http://providers:8090</summary>
    public string BaseUrl { get; set; } = "http://localhost:8090";

    public int TimeoutSeconds { get; set; } = 60;
}
