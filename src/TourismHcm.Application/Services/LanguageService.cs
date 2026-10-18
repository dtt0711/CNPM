using Microsoft.Extensions.Options;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Application.Options;
using TourismHcm.Contracts.Narration;
using TourismHcm.Domain.Constants;

namespace TourismHcm.Application.Services;

public sealed class LanguageService : ILanguageService
{
    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [AppLanguages.Vietnamese] = "Tiếng Việt",
        [AppLanguages.English] = "English",
        [AppLanguages.Japanese] = "日本語",
        [AppLanguages.Korean] = "한국어",
        [AppLanguages.Chinese] = "中文"
    };

    private readonly SpeechOptions _speechOptions;

    public LanguageService(IOptions<SpeechOptions> speechOptions)
    {
        _speechOptions = speechOptions.Value;
    }

    public IReadOnlyList<LanguageInfoDto> GetLanguages() =>
        AppLanguages.Supported
            .Select(code => new LanguageInfoDto
            {
                Code = code,
                Name = DisplayNames.GetValueOrDefault(code, code),
                VoiceId = _speechOptions.Voices.GetValueOrDefault(code),
                IsPriority = AppLanguages.Priority.Contains(code, StringComparer.OrdinalIgnoreCase)
            })
            .ToList();
}
