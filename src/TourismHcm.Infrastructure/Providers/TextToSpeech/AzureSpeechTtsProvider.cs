using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Application.Options;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Infrastructure.Providers.TextToSpeech;

/// <summary>
/// Text-to-speech provider backed by Azure Cognitive Services Speech SDK. When no subscription
/// key/region is configured, <see cref="IsConfigured"/> is false and synthesis attempts throw
/// <see cref="ProviderUnavailableException"/> so the API surfaces a 503 instead of a fake voice.
/// </summary>
public sealed class AzureSpeechTtsProvider : ITextToSpeechProvider
{
    // Reasonable default neural voices per supported language, used when no explicit mapping
    // is configured in SpeechOptions.Voices.
    private static readonly Dictionary<string, string> DefaultVoices = new(StringComparer.OrdinalIgnoreCase)
    {
        [AppLanguages.Vietnamese] = "vi-VN-HoaiMyNeural",
        [AppLanguages.English] = "en-US-JennyNeural",
        [AppLanguages.Japanese] = "ja-JP-NanamiNeural",
        [AppLanguages.Korean] = "ko-KR-SunHiNeural",
        [AppLanguages.Chinese] = "zh-CN-XiaoxiaoNeural",
    };

    private readonly SpeechOptions _options;
    private readonly ILogger<AzureSpeechTtsProvider> _logger;

    public AzureSpeechTtsProvider(IOptions<SpeechOptions> options, ILogger<AzureSpeechTtsProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string ProviderName => "Azure";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.AzureKey) && !string.IsNullOrWhiteSpace(_options.AzureRegion);

    public async Task<byte[]> SynthesizeAsync(string text, string lang, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            throw new ProviderUnavailableException(ProviderName, "Azure Speech key/region is not configured.");
        }

        var normalizedLang = AppLanguages.Normalize(lang);
        var voice = _options.Voices.TryGetValue(normalizedLang, out var configuredVoice)
            ? configuredVoice
            : DefaultVoices.GetValueOrDefault(normalizedLang, DefaultVoices[AppLanguages.English]);

        var speechConfig = SpeechConfig.FromSubscription(_options.AzureKey, _options.AzureRegion);
        speechConfig.SpeechSynthesisVoiceName = voice;
        speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3);

        using var synthesizer = new SpeechSynthesizer(speechConfig, null);

        ct.ThrowIfCancellationRequested();

        using var result = await synthesizer.SpeakTextAsync(text).ConfigureAwait(false);

        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
        {
            return result.AudioData;
        }

        if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
            _logger.LogError(
                "Azure speech synthesis canceled: {ErrorCode} {ErrorDetails}", cancellation.ErrorCode, cancellation.ErrorDetails);
        }

        throw new ProviderUnavailableException(ProviderName, $"Speech synthesis failed with reason '{result.Reason}'.");
    }
}
