using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Review?> GetByUserAndPlaceAsync(Guid userId, Guid placeId, CancellationToken ct = default);

    Task<(IReadOnlyList<Review> Items, int TotalCount)> GetByPlaceIdAsync(
        Guid placeId, int page, int pageSize, CancellationToken ct = default);

    Task AddAsync(Review review, CancellationToken ct = default);

    void Remove(Review review);

    Task<(double Average, int Count)> GetStatsForPlaceAsync(Guid placeId, CancellationToken ct = default);
}
