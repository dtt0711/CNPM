using TourismHcm.Contracts.Favorites;

namespace TourismHcm.Application.Abstractions.Services;

public interface IFavoriteService
{
    Task<IReadOnlyList<FavoriteDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    Task AddAsync(Guid userId, Guid placeId, CancellationToken ct = default);

    Task RemoveAsync(Guid userId, Guid placeId, CancellationToken ct = default);
}
