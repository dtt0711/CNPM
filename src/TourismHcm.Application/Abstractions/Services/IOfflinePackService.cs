using TourismHcm.Contracts.Offline;

namespace TourismHcm.Application.Abstractions.Services;

public interface IOfflinePackService
{
    Task<OfflinePackManifest> BuildManifestAsync(IEnumerable<string>? langs = null, CancellationToken ct = default);
}
