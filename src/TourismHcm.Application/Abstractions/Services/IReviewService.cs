using TourismHcm.Contracts.Common;
using TourismHcm.Contracts.Reviews;

namespace TourismHcm.Application.Abstractions.Services;

public interface IReviewService
{
    Task<ReviewDto> CreateOrUpdateAsync(Guid userId, CreateReviewRequest request, CancellationToken ct = default);

    Task<PagedResult<ReviewDto>> GetByPlaceAsync(Guid placeId, int page, int pageSize, CancellationToken ct = default);

    Task DeleteAsync(Guid userId, Guid reviewId, bool isAdmin, CancellationToken ct = default);
}
