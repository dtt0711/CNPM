using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface IAudioAssetRepository
{
    Task<AudioAsset?> GetAsync(Guid placeId, string lang, CancellationToken ct = default);

    Task<IReadOnlyList<AudioAsset>> GetByPlaceIdAsync(Guid placeId, CancellationToken ct = default);

    Task AddAsync(AudioAsset asset, CancellationToken ct = default);

    void Remove(AudioAsset asset);
}
