using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class TranslationRepository : ITranslationRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public TranslationRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PlaceTranslation?> GetAsync(Guid placeId, string lang, CancellationToken ct = default) =>
        _dbContext.PlaceTranslations.FirstOrDefaultAsync(t => t.PlaceId == placeId && t.Lang == lang, ct);

    public async Task<IReadOnlyList<PlaceTranslation>> GetByPlaceIdAsync(Guid placeId, CancellationToken ct = default) =>
        await _dbContext.PlaceTranslations.Where(t => t.PlaceId == placeId).ToListAsync(ct);

    public async Task AddAsync(PlaceTranslation translation, CancellationToken ct = default) =>
        await _dbContext.PlaceTranslations.AddAsync(translation, ct);

    public void Remove(PlaceTranslation translation) => _dbContext.PlaceTranslations.Remove(translation);
}
