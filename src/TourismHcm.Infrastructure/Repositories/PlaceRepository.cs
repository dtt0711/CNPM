using Microsoft.EntityFrameworkCore;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Repositories;

public sealed class PlaceRepository : IPlaceRepository
{
    private readonly TourismHcmDbContext _dbContext;

    public PlaceRepository(TourismHcmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Place?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.Places.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Place?> GetByIdWithTranslationsAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.Places
            .Include(p => p.Translations)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Place place, CancellationToken ct = default) =>
        await _dbContext.Places.AddAsync(place, ct);

    public void Remove(Place place) => _dbContext.Places.Remove(place);

    public async Task<(IReadOnlyList<Place> Items, int TotalCount)> SearchAsync(
        string? search,
        PlaceCategory? category,
        string? district,
        PlaceStatus? status,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbContext.Places.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern) || EF.Functions.Like(p.Address, pattern));
        }

        if (category is not null)
        {
            query = query.Where(p => p.Category == category.Value);
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            query = query.Where(p => p.District == district);
        }

        if (status is not null)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var totalCount = await query.CountAsync(ct);

        query = ApplySort(query, sortBy, descending);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Place>> GetInBoundingBoxAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        PlaceStatus? status,
        CancellationToken ct = default)
    {
        var query = _dbContext.Places.Where(p =>
            p.Latitude >= minLat && p.Latitude <= maxLat &&
            p.Longitude >= minLng && p.Longitude <= maxLng);

        if (status is not null)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Place>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await _dbContext.Places.Where(p => idList.Contains(p.Id)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Place>> GetAllPublishedAsync(CancellationToken ct = default) =>
        await _dbContext.Places.Where(p => p.Status == PlaceStatus.Published).ToListAsync(ct);

    private static IQueryable<Place> ApplySort(IQueryable<Place> query, string? sortBy, bool descending)
    {
        Func<IQueryable<Place>, IOrderedQueryable<Place>> orderBy = sortBy?.Trim().ToLowerInvariant() switch
        {
            "name" => descending ? q => q.OrderByDescending(p => p.Name) : q => q.OrderBy(p => p.Name),
            "rating" => descending ? q => q.OrderByDescending(p => p.AverageRating) : q => q.OrderBy(p => p.AverageRating),
            "updatedat" => descending ? q => q.OrderByDescending(p => p.UpdatedAt) : q => q.OrderBy(p => p.UpdatedAt),
            _ => descending ? q => q.OrderByDescending(p => p.CreatedAt) : q => q.OrderBy(p => p.CreatedAt),
        };

        return orderBy(query);
    }
}
