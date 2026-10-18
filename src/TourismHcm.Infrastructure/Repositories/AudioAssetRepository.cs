using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class AudioAssetRepository : IAudioAssetRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public AudioAssetRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AudioAsset?> GetAsync(Guid placeId, string lang, CancellationToken ct = default) =>
        _dbContext.AudioAssets.FirstOrDefaultAsync(a => a.PlaceId == placeId && a.Lang == lang, ct);

    public async Task<IReadOnlyList<AudioAsset>> GetByPlaceIdAsync(Guid placeId, CancellationToken ct = default) =>
        await _dbContext.AudioAssets.Where(a => a.PlaceId == placeId).ToListAsync(ct);

    public async Task AddAsync(AudioAsset asset, CancellationToken ct = default) =>
        await _dbContext.AudioAssets.AddAsync(asset, ct);

    public void Remove(AudioAsset asset) => _dbContext.AudioAssets.Remove(asset);
}
