namespace TourismHcm.Application.Abstractions.Providers;

public interface IAiAdvisorProvider
{
    bool IsConfigured { get; }

    string ProviderName { get; }

    Task<string> EnhanceDescriptionAsync(string sourceText, string lang, CancellationToken ct = default);
}
