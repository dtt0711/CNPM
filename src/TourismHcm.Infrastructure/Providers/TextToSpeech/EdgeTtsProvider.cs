using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Infrastructure.Providers.TextToSpeech;

/// <summary>
/// Text-to-speech via the Python Edge-TTS sidecar (<c>POST /tts</c>).
/// </summary>
public sealed class EdgeTtsProvider : ITextToSpeechProvider
{
    private readonly HttpClient _http;
    private readonly ProvidersOptions _options;
    private readonly ILogger<EdgeTtsProvider> _logger;

    public EdgeTtsProvider(HttpClient http, IOptions<ProvidersOptions> options, ILogger<EdgeTtsProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Edge";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<byte[]> SynthesizeAsync(string text, string lang, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new ProviderUnavailableException(ProviderName, "Providers BaseUrl is not configured.");
        }

        var normalizedLang = AppLanguages.Normalize(lang);
        using var request = new HttpRequestMessage(HttpMethod.Post, "tts")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { text, lang = normalizedLang }),
                Encoding.UTF8,
                "application/json"),
        };

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError("Edge-TTS sidecar returned {Status}: {Detail}", (int)response.StatusCode, detail);
                throw new ProviderUnavailableException(ProviderName, $"Edge-TTS failed with HTTP {(int)response.StatusCode}.");
            }

            return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (ProviderUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Edge-TTS sidecar request failed");
            throw new ProviderUnavailableException(ProviderName, "Edge-TTS sidecar is unavailable.");
        }
    }
}
