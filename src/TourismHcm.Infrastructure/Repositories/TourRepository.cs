using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class TourRepository : ITourRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public TourRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Tour?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.Tours.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tour?> GetByIdWithStopsAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.Tours
            .Include(t => t.Stops.OrderBy(s => s.Order))
            .ThenInclude(s => s.Place)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Tour>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _dbContext.Tours
            .Include(t => t.Stops.OrderBy(s => s.Order))
            .ThenInclude(s => s.Place)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Tour tour, CancellationToken ct = default) =>
        await _dbContext.Tours.AddAsync(tour, ct);

    public void Remove(Tour tour) => _dbContext.Tours.Remove(tour);
}
