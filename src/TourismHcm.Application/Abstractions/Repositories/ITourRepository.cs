using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface ITourRepository
{
    Task<Tour?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Tour?> GetByIdWithStopsAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Tour>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Tour tour, CancellationToken ct = default);

    void Remove(Tour tour);
}
