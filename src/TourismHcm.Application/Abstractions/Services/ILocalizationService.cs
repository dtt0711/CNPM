using TourismHcm.Contracts.Localization;

namespace TourismHcm.Application.Abstractions.Services;

public interface ILocalizationService
{
    Task<OnDemandLocalizationResult> OnDemandAsync(Guid placeId, string lang, CancellationToken ct = default);

    Task<WarmupResult> WarmupAsync(string lang, IReadOnlyList<Guid>? placeIds, CancellationToken ct = default);

    Task<HotsetResult> PrepareHotsetAsync(double lat, double lng, string lang, CancellationToken ct = default);
}
