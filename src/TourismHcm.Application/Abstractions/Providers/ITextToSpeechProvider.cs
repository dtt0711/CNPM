namespace TourismHcm.Application.Abstractions.Providers;

public interface ITextToSpeechProvider
{
    bool IsConfigured { get; }

    string ProviderName { get; }

    Task<byte[]> SynthesizeAsync(string text, string lang, CancellationToken ct = default);
}
