using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Infrastructure.Providers.Translation;

/// <summary>
/// Free Google Translate via the Python deep-translator sidecar (<c>POST /translate</c>).
/// </summary>
public sealed class FreeGoogleTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly ProvidersOptions _options;
    private readonly ILogger<FreeGoogleTranslationProvider> _logger;

    public FreeGoogleTranslationProvider(
        HttpClient http,
        IOptions<ProvidersOptions> options,
        ILogger<FreeGoogleTranslationProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Free";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new ProviderUnavailableException(ProviderName, "Providers BaseUrl is not configured.");
        }

        var payload = new
        {
            text,
            source_lang = AppLanguages.Normalize(sourceLang),
            target_lang = AppLanguages.Normalize(targetLang),
        };

        try
        {
            using var response = await _http.PostAsJsonAsync("translate", payload, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError("Translate sidecar returned {Status}: {Detail}", (int)response.StatusCode, detail);
                throw new ProviderUnavailableException(ProviderName, $"Translate failed with HTTP {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<TranslateResponse>(cancellationToken: ct)
                .ConfigureAwait(false);

            if (body is null || string.IsNullOrWhiteSpace(body.TranslatedText))
            {
                throw new ProviderUnavailableException(ProviderName, "Translate sidecar returned an empty payload.");
            }

            return body.TranslatedText;
        }
        catch (ProviderUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translate sidecar request failed");
            throw new ProviderUnavailableException(ProviderName, "Translate sidecar is unavailable.");
        }
    }

    private sealed class TranslateResponse
    {
        [JsonPropertyName("translated_text")]
        public string TranslatedText { get; init; } = string.Empty;
    }
}
