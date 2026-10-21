using TourismHcm.Contracts.Maps;

namespace TourismHcm.Application.Abstractions.Services;

public interface IMapPackService
{
    Task<MapOfflineOptionsDto> GetOfflineOptionsAsync(CancellationToken ct = default);

    Task<MapPackManifestDto> GetOfflineManifestAsync(string packId, CancellationToken ct = default);

    Task<IReadOnlyList<MapPackManifestDto>> ListPacksAsync(CancellationToken ct = default);
}
