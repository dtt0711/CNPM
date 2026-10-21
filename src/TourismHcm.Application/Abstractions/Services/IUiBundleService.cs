using TourismHcm.Contracts.UiBundles;

namespace TourismHcm.Application.Abstractions.Services;

public interface IUiBundleService
{
    Task<UiBundleDto> GetBundleAsync(string locale, CancellationToken ct = default);
}
