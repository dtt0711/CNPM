using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Domain.Constants;

namespace TourismHcm.Infrastructure.Providers.Translation;

/// <summary>
/// Deterministic stub registered only in the Testing environment (unit/integration hosts).
/// </summary>
public sealed class FakeTranslationProvider : ITranslationProvider
{
    public string ProviderName => "Fake";

    public bool IsConfigured => true;

    public Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        var normalizedTarget = AppLanguages.Normalize(targetLang);

        // English passes through close to verbatim (as a stand-in for a real translation),
        // other languages get a readable language-tagged prefix so tests/dev UIs can tell the
        // stubbed text apart from genuine machine-translated content.
        var translated = string.Equals(normalizedTarget, AppLanguages.English, StringComparison.OrdinalIgnoreCase)
            ? text
            : $"[{normalizedTarget}] {text}";

        return Task.FromResult(translated);
    }
}
