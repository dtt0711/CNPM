using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Infrastructure.Providers.Ai;

public sealed class GeminiAiAdvisorProvider : IAiAdvisorProvider
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiAiAdvisorProvider> _logger;

    public GeminiAiAdvisorProvider(HttpClient http, IOptions<AiOptions> options, ILogger<GeminiAiAdvisorProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));
    }

    public string ProviderName => string.IsNullOrWhiteSpace(_options.ProxyPalBaseUrl) ? "Gemini" : "ProxyPal";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.GeminiApiKey) || !string.IsNullOrWhiteSpace(_options.ProxyPalBaseUrl);

    public async Task<string> EnhanceDescriptionAsync(string sourceText, string lang, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new ProviderUnavailableException(ProviderName, "Gemini/ProxyPal API key is not configured.");
        }

        var prompt =
            $"You enhance tourism place descriptions. Language: {lang}. " +
            "Do NOT invent facts. You MAY add positive adjectives. Target 200-300 words.\n\n" +
            $"Source:\n{sourceText}";

        try
        {
            if (!string.IsNullOrWhiteSpace(_options.ProxyPalBaseUrl))
            {
                return await CallProxyPalAsync(prompt, ct).ConfigureAwait(false);
            }

            return await CallGeminiAsync(prompt, ct).ConfigureAwait(false);
        }
        catch (ProviderUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI enhance failed");
            throw new ProviderUnavailableException(ProviderName, "AI enhance request failed.");
        }
    }

    private async Task<string> CallGeminiAsync(string prompt, CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(_options.GeminiModel) ? "gemini-2.0-flash" : _options.GeminiModel;
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_options.GeminiApiKey}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } },
            },
        };

        using var response = await _http.PostAsJsonAsync(url, body, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogError("Gemini HTTP {Status}: {Detail}", (int)response.StatusCode, detail);
            throw new ProviderUnavailableException(ProviderName, $"Gemini failed with HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ProviderUnavailableException(ProviderName, "Gemini returned empty text.");
        }

        return text.Trim();
    }

    private async Task<string> CallProxyPalAsync(string prompt, CancellationToken ct)
    {
        var url = _options.ProxyPalBaseUrl!.TrimEnd('/') + "/v1/chat/completions";
        var body = new
        {
            model = _options.GeminiModel,
            messages = new[] { new { role = "user", content = prompt } },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(_options.GeminiApiKey))
        {
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _options.GeminiApiKey);
        }

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderUnavailableException(ProviderName, $"ProxyPal failed with HTTP {(int)response.StatusCode}.");
        }

        var parsed = await response.Content.ReadFromJsonAsync<ProxyPalResponse>(cancellationToken: ct)
            .ConfigureAwait(false);
        var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ProviderUnavailableException(ProviderName, "ProxyPal returned empty text.");
        }

        return text.Trim();
    }

    private sealed class ProxyPalResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; init; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Msg? Message { get; init; }
    }

    private sealed class Msg
    {
        [JsonPropertyName("content")]
        public string? Content { get; init; }
    }
}
