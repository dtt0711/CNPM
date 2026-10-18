using Mapster;
using TourismHcm.Application.Abstractions.Repositories;
using TourismHcm.Application.Abstractions.Services;
using TourismHcm.Contracts.Favorites;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Exceptions;

namespace TourismHcm.Application.Services;

public sealed class FavoriteService : IFavoriteService
{
    private readonly IFavoriteRepository _favoriteRepository;
    private readonly IPlaceRepository _placeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public FavoriteService(
        IFavoriteRepository favoriteRepository,
        IPlaceRepository placeRepository,
        IUnitOfWork unitOfWork)
    {
        _favoriteRepository = favoriteRepository;
        _placeRepository = placeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<FavoriteDto>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var favorites = await _favoriteRepository.GetByUserIdAsync(userId, ct);
        return favorites.Adapt<List<FavoriteDto>>();
    }

    public async Task AddAsync(Guid userId, Guid placeId, CancellationToken ct = default)
    {
        _ = await _placeRepository.GetByIdAsync(placeId, ct)
            ?? throw new NotFoundException("Place", placeId);

        var existing = await _favoriteRepository.GetAsync(userId, placeId, ct);
        if (existing is not null)
        {
            return;
        }

        await _favoriteRepository.AddAsync(new Favorite
        {
            UserId = userId,
            PlaceId = placeId,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid userId, Guid placeId, CancellationToken ct = default)
    {
        var existing = await _favoriteRepository.GetAsync(userId, placeId, ct);
        if (existing is null)
        {
            return;
        }

        _favoriteRepository.Remove(existing);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
