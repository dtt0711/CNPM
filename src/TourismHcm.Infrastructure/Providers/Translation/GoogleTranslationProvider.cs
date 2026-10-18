using Google.Cloud.Translation.V2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Infrastructure.Providers.Translation;

/// <summary>
/// Translation provider backed by the Google Cloud Translation (Basic/v2) API. When no API key
/// is configured, <see cref="IsConfigured"/> is false and any translation attempt throws
/// <see cref="ProviderUnavailableException"/> so callers (and ultimately the API) surface a 503.
/// </summary>
public sealed class GoogleTranslationProvider : ITranslationProvider
{
    // Google Translate expects specific locale codes for some of our supported languages.
    private static readonly Dictionary<string, string> LanguageCodeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [AppLanguages.Vietnamese] = "vi",
        [AppLanguages.English] = "en",
        [AppLanguages.Japanese] = "ja",
        [AppLanguages.Korean] = "ko",
        [AppLanguages.Chinese] = "zh-CN",
    };

    private readonly TranslationOptions _options;
    private readonly ILogger<GoogleTranslationProvider> _logger;
    private readonly Lazy<TranslationClient?> _client;

    public GoogleTranslationProvider(IOptions<TranslationOptions> options, ILogger<GoogleTranslationProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _client = new Lazy<TranslationClient?>(CreateClient);
    }

    public string ProviderName => "Google";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (!IsConfigured || _client.Value is null)
        {
            throw new ProviderUnavailableException(ProviderName, "Google Translate API key is not configured.");
        }

        var mappedSource = MapLanguageCode(sourceLang);
        var mappedTarget = MapLanguageCode(targetLang);

        try
        {
            var result = await _client.Value.TranslateTextAsync(
                text, mappedTarget, mappedSource, cancellationToken: ct);

            return result.TranslatedText;
        }
        catch (Exception ex) when (ex is not ProviderUnavailableException)
        {
            _logger.LogError(ex, "Google Translate request failed for {Source}->{Target}", mappedSource, mappedTarget);
            throw new ProviderUnavailableException(ProviderName, "Google Translate request failed.");
        }
    }

    private static string MapLanguageCode(string lang)
    {
        var normalized = AppLanguages.Normalize(lang);
        return LanguageCodeMap.TryGetValue(normalized, out var mapped) ? mapped : normalized;
    }

    private TranslationClient? CreateClient() =>
        string.IsNullOrWhiteSpace(_options.ApiKey) ? null : TranslationClient.CreateFromApiKey(_options.ApiKey);
}
