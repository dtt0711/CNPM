using TourismHcm.Contracts.Narration;

namespace TourismHcm.Application.Abstractions.Services;

public interface ILanguageService
{
    IReadOnlyList<LanguageInfoDto> GetLanguages();
}
