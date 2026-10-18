using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Places;

namespace TourismHcm.Application.Abstractions.Services;

public interface IPlaceService
{
    Task<PlaceDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<PlaceDto>> SearchAsync(PlaceQueryRequest query, CancellationToken ct = default);

    Task<PlaceDetailDto> CreateAsync(CreatePlaceRequest request, CancellationToken ct = default);

    Task<PlaceDetailDto> UpdateAsync(Guid id, UpdatePlaceRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<NearbyPlaceDto>> GetNearbyAsync(
        double latitude, double longitude, double radiusMeters, int? limit, CancellationToken ct = default);
}
