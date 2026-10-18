using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;

namespace TourismHcm.Application.Abstractions.Repositories;

public interface IPlaceRepository
{
    Task<Place?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Place?> GetByIdWithTranslationsAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Place place, CancellationToken ct = default);

    void Remove(Place place);

    Task<(IReadOnlyList<Place> Items, int TotalCount)> SearchAsync(
        string? search,
        PlaceCategory? category,
        string? district,
        PlaceStatus? status,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<Place>> GetInBoundingBoxAsync(
        double minLat,
        double maxLat,
        double minLng,
        double maxLng,
        PlaceStatus? status,
        CancellationToken ct = default);

    Task<IReadOnlyList<Place>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<IReadOnlyList<Place>> GetAllPublishedAsync(CancellationToken ct = default);
}
