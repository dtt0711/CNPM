using TourismHcm.Domain.Entities;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface IFavoriteRepository
{
    Task<Favorite?> GetAsync(Guid userId, Guid placeId, CancellationToken ct = default);

    Task<IReadOnlyList<Favorite>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Favorite favorite, CancellationToken ct = default);

    void Remove(Favorite favorite);
}
