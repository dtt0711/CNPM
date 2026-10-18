using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class ReviewRepository : IReviewRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public ReviewRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Review?> GetByUserAndPlaceAsync(Guid userId, Guid placeId, CancellationToken ct = default) =>
        _dbContext.Reviews.FirstOrDefaultAsync(r => r.UserId == userId && r.PlaceId == placeId, ct);

    public async Task<(IReadOnlyList<Review> Items, int TotalCount)> GetByPlaceIdAsync(
        Guid placeId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _dbContext.Reviews.Include(r => r.User).Where(r => r.PlaceId == placeId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Review review, CancellationToken ct = default) =>
        await _dbContext.Reviews.AddAsync(review, ct);

    public void Remove(Review review) => _dbContext.Reviews.Remove(review);

    public async Task<(double Average, int Count)> GetStatsForPlaceAsync(Guid placeId, CancellationToken ct = default)
    {
        var query = _dbContext.Reviews.Where(r => r.PlaceId == placeId);
        var count = await query.CountAsync(ct);
        if (count == 0)
        {
            return (0d, 0);
        }

        var average = await query.AverageAsync(r => r.Rating, ct);
        return (average, count);
    }
}
