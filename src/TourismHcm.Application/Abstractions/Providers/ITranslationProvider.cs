namespace TourismHcm.Application.Abstractions.Providers;

public interface ITranslationProvider
{
    bool IsConfigured { get; }

    string ProviderName { get; }

    Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default);
}
