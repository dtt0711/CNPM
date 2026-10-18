using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class FavoriteRepository : IFavoriteRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public FavoriteRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Favorite?> GetAsync(Guid userId, Guid placeId, CancellationToken ct = default) =>
        _dbContext.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.PlaceId == placeId, ct);

    public async Task<IReadOnlyList<Favorite>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _dbContext.Favorites
            .Include(f => f.Place)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Favorite favorite, CancellationToken ct = default) =>
        await _dbContext.Favorites.AddAsync(favorite, ct);

    public void Remove(Favorite favorite) => _dbContext.Favorites.Remove(favorite);
}
