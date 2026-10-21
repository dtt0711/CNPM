using System.Text.Json;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.UiBundles;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Enums;

namespace TourismHcm.Application.Services;

public sealed class UiBundleService : IUiBundleService
{
    private static readonly HashSet<string> ReadyLocales = new(StringComparer.OrdinalIgnoreCase)
    {
        AppLanguages.English, AppLanguages.Vietnamese, "ja", "ko", "zh"
    };

    private readonly IPlatformRepository _platformRepository;

    public UiBundleService(IPlatformRepository platformRepository)
    {
        _platformRepository = platformRepository;
    }

    public async Task<UiBundleDto> GetBundleAsync(string locale, CancellationToken ct = default)
    {
        var normalized = AppLanguages.Normalize(locale);
        var fromDb = await _platformRepository.GetUiTranslationBundleAsync(normalized, ct);
        if (fromDb is not null && fromDb.Status == UiTranslationBundleStatus.Ready)
        {
            return new UiBundleDto
            {
                Locale = normalized,
                Status = UiTranslationBundleStatus.Ready.ToString(),
                PayloadJson = fromDb.PayloadJson,
                IsFallback = false
            };
        }

        if (ReadyLocales.Contains(normalized))
        {
            var payload = BuildEmbeddedPayload(normalized);
            return new UiBundleDto
            {
                Locale = normalized,
                Status = UiTranslationBundleStatus.Ready.ToString(),
                PayloadJson = payload,
                IsFallback = false
            };
        }

        var english = BuildEmbeddedPayload(AppLanguages.English);
        return new UiBundleDto
        {
            Locale = normalized,
            Status = UiTranslationBundleStatus.Pending.ToString(),
            PayloadJson = english,
            IsFallback = true
        };
    }

    private static string BuildEmbeddedPayload(string locale)
    {
        var obj = new Dictionary<string, object>
        {
            ["locale"] = locale,
            ["status"] = "ready",
            ["app"] = new Dictionary<string, string>
            {
                ["title"] = "Tourism HCM",
                ["tagline"] = locale switch
                {
                    _ when string.Equals(locale, AppLanguages.Vietnamese, StringComparison.OrdinalIgnoreCase)
                        => "Khám phá TP.HCM",
                    _ => "Discover Ho Chi Minh City"
                }
            }
        };

        return JsonSerializer.Serialize(obj);
    }
}
